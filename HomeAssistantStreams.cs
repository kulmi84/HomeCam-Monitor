#if V2_BETA
using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
namespace HomeCamMonitor;

// Keep bearer tokens and expiring HLS URLs in memory. Players only see a
// loopback endpoint; saved camera configuration contains the HA entity ID.
internal sealed class HomeAssistantStreams : IDisposable
{
    private readonly TcpListener listener = new(IPAddress.Loopback, 0);
    private readonly CancellationTokenSource lifetime = new();
    private readonly SemaphoreSlim slots = new(16);
    private readonly HttpClient http;
    private readonly Uri origin;
    private readonly string token;
    private readonly bool ignoreCertificate;
    private readonly string route = Convert.ToHexString(RandomNumberGenerator.GetBytes(24));
    private readonly HashSet<string> entities;
    private readonly string address;
    private readonly Task accepting;
    internal HomeAssistantStreams(string address, string token, bool ignoreCertificate, IEnumerable<string> entities)
    {
        origin = HaApi.Address(address); this.address = address; this.token = token; this.ignoreCertificate = ignoreCertificate;
        this.entities = entities.ToHashSet(StringComparer.Ordinal);
        var handler = new HttpClientHandler { AllowAutoRedirect = false, UseProxy = false };
        if (ignoreCertificate) handler.ServerCertificateCustomValidationCallback = (_, _, _, _) => true;
        http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(40) };
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        listener.Start(); accepting = Accept();
    }
    internal string Url(string entity) => $"http://127.0.0.1:{((IPEndPoint)listener.LocalEndpoint).Port}/{route}/camera/{entity}/index.m3u8";
    private async Task Accept()
    {
        try
        {
            while (!lifetime.IsCancellationRequested)
            {
                await slots.WaitAsync(lifetime.Token).ConfigureAwait(false);
                TcpClient client;
                try { client = await listener.AcceptTcpClientAsync(lifetime.Token).ConfigureAwait(false); }
                catch { slots.Release(); throw; }
                _ = Serve(client);
            }
        }
        catch (Exception e) when (e is OperationCanceledException or ObjectDisposedException or SocketException) { }
    }
    internal Uri SafeUri(Uri parent, string path)
    {
        if (!Uri.TryCreate(parent, path, out var uri) || uri.Scheme != origin.Scheme || uri.Host != origin.Host || uri.Port != origin.Port ||
            uri.UserInfo.Length > 0 || !uri.AbsolutePath.StartsWith(origin.AbsolutePath.TrimEnd('/') + "/api/hls/", StringComparison.Ordinal))
            throw new InvalidDataException("HA-Stream verweist auf eine unerwartete Adresse.");
        return uri;
    }
    internal string Resource(Uri parent, string path)
    {
        var uri = SafeUri(parent, path);
        return $"/{route}/resource/" + Convert.ToBase64String(Encoding.UTF8.GetBytes(uri.AbsoluteUri)).TrimEnd('=').Replace('+', '-').Replace('/', '_') + "/" + Uri.EscapeDataString(Path.GetFileName(uri.AbsolutePath));
    }
    internal string Playlist(Uri parent, string text) => string.Join("\n", text.Split('\n').Select(line =>
    {
        var value = line.TrimEnd('\r');
        if (value.StartsWith('#')) return Regex.Replace(value, "URI=\"([^\"]+)\"", match => "URI=\"" + Resource(parent, match.Groups[1].Value) + "\"");
        return value.Length == 0 ? value : Resource(parent, value);
    }));
    private async Task Serve(TcpClient client)
    {
        using (client)
        using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token))
        {
            timeout.CancelAfter(TimeSpan.FromSeconds(50)); var cancel = timeout.Token;
            try
            {
                using var network = client.GetStream();
                var header = new List<byte>(); var one = new byte[1];
                while (header.Count < 16384)
                {
                    if (await network.ReadAsync(one, cancel).ConfigureAwait(false) == 0) return;
                    header.Add(one[0]);
                    if (header.Count >= 4 && header.TakeLast(4).SequenceEqual(new byte[] { 13, 10, 13, 10 })) break;
                }
                var lines = Encoding.ASCII.GetString(header.ToArray()).Split("\r\n"); var request = lines[0].Split(' ');
                if (header.Count >= 16384 || request.Length != 3 || request[0] != "GET" || !request[1].StartsWith('/' + route + "/", StringComparison.Ordinal))
                { await Error(network, cancel).ConfigureAwait(false); return; }
                var path = request[1][(route.Length + 2)..]; Uri upstream;
                if (path.StartsWith("camera/", StringComparison.Ordinal) && path.EndsWith("/index.m3u8", StringComparison.Ordinal))
                {
                    var entity = path[7..^11];
                    if (!entities.Contains(entity)) { await Error(network, cancel).ConfigureAwait(false); return; }
                    using var api = new HaApi(); await api.Connect(address, token, ignoreCertificate, cancel).ConfigureAwait(false);
                    var result = await api.Request("camera/stream", cancel, entity).ConfigureAwait(false);
                    var streamPath = result is { } value ? HaApi.Text(value, "url") : "";
                    if (streamPath.Length == 0) throw new InvalidDataException("Kein HA-Stream.");
                    upstream = SafeUri(origin, streamPath);
                }
                else if (path.StartsWith("resource/", StringComparison.Ordinal))
                {
                    var encoded = path[9..].Split('/')[0].Replace('-', '+').Replace('_', '/');
                    encoded = encoded.PadRight((encoded.Length + 3) / 4 * 4, '=');
                    upstream = SafeUri(origin, Encoding.UTF8.GetString(Convert.FromBase64String(encoded)));
                }
                else { await Error(network, cancel).ConfigureAwait(false); return; }
                using var outgoing = new HttpRequestMessage(HttpMethod.Get, upstream);
                var range = lines.FirstOrDefault(l => l.StartsWith("Range:", StringComparison.OrdinalIgnoreCase));
                if (range is not null && RangeHeaderValue.TryParse(range[6..].Trim(), out var parsed)) outgoing.Headers.Range = parsed;
                using var response = await http.SendAsync(outgoing, HttpCompletionOption.ResponseHeadersRead, cancel).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode) { await Error(network, cancel).ConfigureAwait(false); return; }
                var type = response.Content.Headers.ContentType?.MediaType ?? "application/octet-stream";
                if (upstream.AbsolutePath.EndsWith(".m3u8", StringComparison.OrdinalIgnoreCase) || type.Contains("mpegurl", StringComparison.OrdinalIgnoreCase))
                {
                    if (response.Content.Headers.ContentLength > 2 * 1024 * 1024) throw new InvalidDataException();
                    await response.Content.LoadIntoBufferAsync(2 * 1024 * 1024).WaitAsync(cancel).ConfigureAwait(false);
                    var text = await response.Content.ReadAsStringAsync(cancel).ConfigureAwait(false);
                    if (!text.StartsWith("#EXTM3U", StringComparison.Ordinal)) throw new InvalidDataException();
                    var bytes = Encoding.UTF8.GetBytes(Playlist(upstream, text));
                    await network.WriteAsync(Encoding.ASCII.GetBytes($"HTTP/1.1 200 OK\r\nContent-Type: application/vnd.apple.mpegurl\r\nContent-Length: {bytes.Length}\r\nConnection: close\r\n\r\n"), cancel).ConfigureAwait(false);
                    await network.WriteAsync(bytes, cancel).ConfigureAwait(false);
                }
                else
                {
                    var length = response.Content.Headers.ContentLength is { } size ? $"Content-Length: {size}\r\n" : "";
                    var contentRange = response.Content.Headers.ContentRange is { } cr ? $"Content-Range: {cr}\r\n" : "";
                    await network.WriteAsync(Encoding.ASCII.GetBytes($"HTTP/1.1 {(int)response.StatusCode} OK\r\nContent-Type: {type}\r\n{length}{contentRange}Connection: close\r\n\r\n"), cancel).ConfigureAwait(false);
                    await response.Content.CopyToAsync(network, cancel).ConfigureAwait(false);
                }
            }
            catch (Exception e) when (e is IOException or SocketException or HttpRequestException or OperationCanceledException or InvalidDataException or ArgumentException or ObjectDisposedException or System.Net.WebSockets.WebSocketException or System.Text.Json.JsonException) { }
            finally { slots.Release(); }
        }
    }
    private static async Task Error(Stream stream, CancellationToken cancel) => await stream.WriteAsync(Encoding.ASCII.GetBytes("HTTP/1.1 502 Bad Gateway\r\nContent-Length: 0\r\nConnection: close\r\n\r\n"), cancel).ConfigureAwait(false);
    public void Dispose() { lifetime.Cancel(); listener.Stop(); http.Dispose(); }
}
internal sealed partial class MonitorForm
{
    private HomeAssistantStreams? homeAssistantStreams;
    private void ConfigureHomeAssistantStreams()
    {
        homeAssistantStreams?.Dispose(); homeAssistantStreams = null;
        var cameras = settings.Cameras.Where(c => c.Active && !c.Reolink.Enabled && !c.Direct.Enabled && HaApi.Entity(c.StoredStreamUrl) is not null).ToArray();
        if (cameras.Length == 0) return;
        try { homeAssistantStreams = new HomeAssistantStreams(settings.HomeAssistantUrl, settings.HomeAssistantToken, settings.IgnoreHomeAssistantCertificateErrors,
            cameras.Select(c => HaApi.Entity(c.StoredStreamUrl)!)); }
        catch (Exception e) when (e is ArgumentException or SocketException or FormatException) { return; }
        foreach (var camera in cameras)
        {
            camera.HomeAssistantRuntimeSource = camera.StoredStreamUrl;
            camera.StreamUrl = homeAssistantStreams.Url(HaApi.Entity(camera.HomeAssistantRuntimeSource)!);
        }
    }
}
#endif
