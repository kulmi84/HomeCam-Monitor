#if V2_BETA
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
namespace HomeCamMonitor;

internal sealed record CameraNetwork(string Name, IPAddress Address, int Prefix)
{
    public override string ToString() => $"{Name} · {Address}/{Prefix}";
    internal static IReadOnlyList<CameraNetwork> Available() => NetworkInterface.GetAllNetworkInterfaces()
        .Where(n => n.OperationalStatus == OperationalStatus.Up && n.NetworkInterfaceType is not (NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel))
        .OrderByDescending(n => n.GetIPProperties().GatewayAddresses.Count > 0)
        .SelectMany(n => n.GetIPProperties().UnicastAddresses.Where(a => a.Address.AddressFamily == AddressFamily.InterNetwork)
            .Select(a => new CameraNetwork(n.Name, a.Address, a.PrefixLength))).ToArray();
}

internal sealed class CameraNetworkRange
{
    private readonly uint first;
    private readonly uint last;
    internal int Count => checked((int)(last - first + 1));
    internal string Key => $"{first}:{last}";
    private CameraNetworkRange(uint first, uint last) { this.first = first; this.last = last; }
    private static uint Number(IPAddress ip) { var b = ip.GetAddressBytes(); return (uint)b[0] << 24 | (uint)b[1] << 16 | (uint)b[2] << 8 | b[3]; }
    private static IPAddress Address(uint value) => new(new[] { (byte)(value >> 24), (byte)(value >> 16), (byte)(value >> 8), (byte)value });
    internal bool Contains(IPAddress ip) => ip.AddressFamily == AddressFamily.InterNetwork && Number(ip) >= first && Number(ip) <= last;
    internal IEnumerable<IPAddress> Hosts() { for (var n = first; ; n++) { yield return Address(n); if (n == last) break; } }
    internal static CameraNetworkRange Parse(string value, bool allowLoopback = false)
    {
        var parts = value.Trim().Split('/');
        if (parts.Length > 2 || !IPAddress.TryParse(parts[0], out var ip) || ip.AddressFamily != AddressFamily.InterNetwork ||
            !int.TryParse(parts.Length == 2 ? parts[1] : "32", out var prefix) || prefix is < 20 or > 32)
            throw new ArgumentException(UiText.T("IPv4-Adresse oder CIDR-Bereich /20 bis /32 eingeben, z. B. 192.168.189.0/24 (höchstens 4096 Adressen)."));
        var b = ip.GetAddressBytes();
        if (!(b[0] == 10 || b[0] == 172 && b[1] is >= 16 and <= 31 || b[0] == 192 && b[1] == 168 ||
            b[0] == 169 && b[1] == 254 || b[0] == 100 && b[1] is >= 64 and <= 127 || allowLoopback && b[0] == 127))
            throw new ArgumentException(UiText.T("Bitte einen lokalen privaten IPv4-Bereich auswählen."));
        var mask = prefix == 32 ? uint.MaxValue : uint.MaxValue << (32 - prefix);
        var network = Number(ip) & mask; var broadcast = network | ~mask;
        return new CameraNetworkRange(prefix < 31 ? network + 1 : network, prefix < 31 ? broadcast - 1 : broadcast);
    }
}

internal sealed record NetworkCameraCandidate(IPAddress Address, string Name, string Manufacturer, string DeviceUrl, string Identity, int Port, bool RtspService)
{
    internal string Status => DeviceUrl.Length > 0 ? UiText.T("ONVIF gefunden") : RtspService ? UiText.T("RTSP-Dienst – Stream noch ungeprüft") : UiText.T("Port offen – RTSP unbestätigt");
}

internal static class NetworkCameraDiscovery
{
    private static readonly XNamespace Discovery = "http://schemas.xmlsoap.org/ws/2005/04/discovery";
    internal static string Probe(string id) => $"<s:Envelope xmlns:s=\"http://www.w3.org/2003/05/soap-envelope\" xmlns:a=\"http://schemas.xmlsoap.org/ws/2004/08/addressing\" xmlns:d=\"{Discovery}\" xmlns:dn=\"http://www.onvif.org/ver10/network/wsdl\"><s:Header><a:MessageID>{id}</a:MessageID><a:To>s:urn</a:To><a:Action>{Discovery}/Probe</a:Action></s:Header><s:Body><d:Probe><d:Types>dn:NetworkVideoTransmitter</d:Types></d:Probe></s:Body></s:Envelope>"
        .Replace("s:urn", "urn:schemas-xmlsoap-org:ws:2005:04:discovery");
    internal static IReadOnlyList<NetworkCameraCandidate> ParseMatches(byte[] bytes, IPAddress sender, CameraNetworkRange range, ISet<string> requests)
    {
        if (bytes.Length > 65536 || !range.Contains(sender)) return [];
        try
        {
            using var reader = XmlReader.Create(new MemoryStream(bytes), new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 65536 });
            var document = XDocument.Load(reader);
            var relation = document.Descendants().FirstOrDefault(e => e.Name.LocalName == "RelatesTo")?.Value.Trim();
            if (relation is null || !requests.Contains(relation)) return [];
            var found = new List<NetworkCameraCandidate>();
            foreach (var match in document.Descendants(Discovery + "ProbeMatch").Take(32))
            {
                var urls = (match.Element(Discovery + "XAddrs")?.Value ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
                var url = urls.FirstOrDefault(s => Uri.TryCreate(s, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https" && uri.UserInfo.Length == 0 &&
                    IPAddress.TryParse(uri.Host, out var host) && host.Equals(sender) && range.Contains(host));
                if (url is null) continue;
                var scopes = (match.Element(Discovery + "Scopes")?.Value ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
                string Scope(string type) => Clean(scopes.FirstOrDefault(s => s.StartsWith("onvif://www.onvif.org/" + type + "/", StringComparison.OrdinalIgnoreCase))?.Split('/').LastOrDefault() ?? "");
                var identity = match.Descendants().FirstOrDefault(e => e.Name.LocalName == "Address")?.Value;
                if (string.IsNullOrWhiteSpace(identity)) identity = sender.ToString();
                var name = Scope("name");
                found.Add(new(sender, name.Length > 0 ? name : Scope("hardware"), Scope("manufacturer"), url, Clean(identity), 0, false));
            }
            return found;
        }
        catch (Exception e) when (e is XmlException or UriFormatException or ArgumentException) { return []; }
    }
    private static string Clean(string text) => new(Uri.UnescapeDataString(text).Where(c => !char.IsControl(c)).Take(128).ToArray());
    internal static async Task Discover(IPAddress localAddress, CameraNetworkRange range, IProgress<NetworkCameraCandidate> progress, CancellationToken token,
        IPEndPoint? target = null, TimeSpan? duration = null)
    {
        token.ThrowIfCancellationRequested();
        using var socket = new UdpClient(new IPEndPoint(localAddress, 0));
        socket.Client.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastInterface, localAddress.GetAddressBytes());
        socket.Ttl = 1;
        socket.Client.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastTimeToLive, 1);
        target ??= new IPEndPoint(IPAddress.Parse("239.255.255.250"), 3702);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
        deadline.CancelAfter(duration ?? TimeSpan.FromSeconds(7));
        var id = "urn:uuid:" + Guid.NewGuid();
        var ids = new HashSet<string>(StringComparer.Ordinal) { id };
        var request = Encoding.UTF8.GetBytes(Probe(id));
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var identities = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        async Task Receive()
        {
            try
            {
                while (true)
                {
                    UdpReceiveResult packet;
                    try { packet = await socket.ReceiveAsync(deadline.Token).ConfigureAwait(false); }
                    catch (SocketException e) when (e.SocketErrorCode is SocketError.ConnectionReset or SocketError.ConnectionRefused) { continue; }
                    foreach (var found in ParseMatches(packet.Buffer, packet.RemoteEndPoint.Address, range, ids))
                        if (!seen.Contains(found.Address.ToString()) && !identities.Contains(found.Identity))
                        { seen.Add(found.Address.ToString()); identities.Add(found.Identity); progress.Report(found); }
                }
            }
            catch (OperationCanceledException) when (!token.IsCancellationRequested) { }
        }
        var receive = Receive();
        try
        {
            for (var attempt = 0; attempt < 3; attempt++)
            {
                await socket.SendAsync(request, target, deadline.Token).ConfigureAwait(false);
                await Task.Delay(200, deadline.Token).ConfigureAwait(false);
            }
            await receive.ConfigureAwait(false);
        }
        finally { deadline.Cancel(); try { await receive.ConfigureAwait(false); } catch (OperationCanceledException) { } }
        token.ThrowIfCancellationRequested();
    }
    internal static int[] ParsePorts(string text)
    {
        var fields = text.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (fields.Length is 0 or > 16 || fields.Any(s => !int.TryParse(s, out var n) || n is < 1 or > 65535))
            throw new ArgumentException(UiText.T("Bis zu 16 TCP-Ports mit Komma trennen, z. B. 554,8554,10554."));
        return fields.Select(int.Parse).Distinct().ToArray();
    }
    internal static bool IsRtspResponse(string text) => Regex.IsMatch(text, @"^RTSP/(?:1\.0|2\.0) [1-5][0-9]{2}(?: |\r?$)", RegexOptions.CultureInvariant);
    internal static async Task<NetworkCameraCandidate?> ProbePort(IPAddress address, int port, CancellationToken token, int timeoutMs = 1200, IPAddress? source = null)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token); timeout.CancelAfter(timeoutMs);
        using var client = new TcpClient();
        var open = false;
        try
        {
            if (source is not null) client.Client.Bind(new IPEndPoint(source, 0));
            await client.ConnectAsync(address, port, timeout.Token); open = true;
            var request = Encoding.ASCII.GetBytes($"OPTIONS rtsp://{address}:{port}/ RTSP/1.0\r\nCSeq: 1\r\nUser-Agent: HomeCamMonitor\r\n\r\n");
            var stream = client.GetStream(); await stream.WriteAsync(request, timeout.Token);
            var bytes = new byte[4096]; var used = 0;
            while (used < bytes.Length)
            {
                var count = await stream.ReadAsync(bytes.AsMemory(used), timeout.Token);
                if (count == 0) break; used += count;
                if (Array.IndexOf(bytes, (byte)'\n', 0, used) >= 0) break;
            }
            return new(address, "", "", "", address.ToString(), port, IsRtspResponse(Encoding.ASCII.GetString(bytes, 0, used)));
        }
        catch (Exception e) when (e is SocketException or IOException or OperationCanceledException)
        {
            token.ThrowIfCancellationRequested();
            return open ? new(address, "", "", "", address.ToString(), port, false) : null;
        }
    }
    internal static async Task ScanPorts(CameraNetworkRange range, int[] ports, IProgress<NetworkCameraCandidate> progress, IProgress<int> completed, CancellationToken token, IPAddress? source = null)
    {
        var done = 0;
        await Parallel.ForEachAsync(range.Hosts().SelectMany(ip => ports.Select(port => (ip, port))),
            new ParallelOptions { CancellationToken = token, MaxDegreeOfParallelism = 32 }, async (item, ct) =>
            {
                var found = await ProbePort(item.ip, item.port, ct, source: source);
                if (found is not null) progress.Report(found);
                completed.Report(Interlocked.Increment(ref done));
            });
    }
}
#endif
