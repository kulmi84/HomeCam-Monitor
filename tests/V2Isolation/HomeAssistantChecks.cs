using HomeCamMonitor;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
namespace V2Isolation;
internal static partial class Check
{
    private const string HaStates = """
        [{"entity_id":"camera.front","state":"idle","attributes":{"friendly_name":"Einfahrt"}},
        {"entity_id":"camera.offline","state":"unavailable","attributes":{}},
        {"entity_id":"binary_sensor.front_motion","state":"off","attributes":{"device_class":"motion","friendly_name":"Bewegung"}},
        {"entity_id":"binary_sensor.front_person","state":"on","attributes":{"friendly_name":"Person"}},
        {"entity_id":"binary_sensor.other_motion","state":"off","attributes":{"device_class":"motion"}},
        {"entity_id":"sensor.temperature","state":"20","attributes":{}}]
        """;
    private const string HaRegistry = """
        [{"entity_id":"camera.front","device_id":"front"},{"entity_id":"binary_sensor.front_motion","device_id":"front"},
        {"entity_id":"binary_sensor.front_person","device_id":"front"},{"entity_id":"binary_sensor.other_motion","device_id":"other"}]
        """;
    private sealed class FakeHa : IDisposable
    {
        private readonly TcpListener listener = new(IPAddress.Loopback, 0);
        private readonly CancellationTokenSource lifetime = new();
        internal string Address { get; }
        internal bool DenyRegistry, Stall, WrongIdSeen, SawBearer;
        internal byte[] Video = [];
        internal FakeHa() { listener.Start(); Address = $"http://127.0.0.1:{((IPEndPoint)listener.LocalEndpoint).Port}/"; _ = Accept(); }
        private async Task Accept()
        {
            try { while (!lifetime.IsCancellationRequested) { var client = await listener.AcceptTcpClientAsync(lifetime.Token); _ = Serve(client); } }
            catch (OperationCanceledException) { }
        }
        private async Task Serve(TcpClient client)
        {
            using (client)
            try
            {
                using var stream = client.GetStream(); var buffer = new List<byte>(); var one = new byte[1];
                while (buffer.Count < 16000)
                {
                    if (await stream.ReadAsync(one, lifetime.Token) == 0) return; buffer.Add(one[0]);
                    if (buffer.Count >= 4 && buffer.TakeLast(4).SequenceEqual(new byte[] {13,10,13,10})) break;
                }
                var header = Encoding.ASCII.GetString(buffer.ToArray()); var path = header.Split(' ')[1];
                if (path == "/api/websocket")
                {
                    var key = header.Split("\r\n").Single(s => s.StartsWith("Sec-WebSocket-Key:", StringComparison.OrdinalIgnoreCase)).Split(':')[1].Trim();
                    var accept = Convert.ToBase64String(SHA1.HashData(Encoding.ASCII.GetBytes(key + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11")));
                    await stream.WriteAsync(Encoding.ASCII.GetBytes($"HTTP/1.1 101 Switching Protocols\r\nUpgrade: websocket\r\nConnection: Upgrade\r\nSec-WebSocket-Accept: {accept}\r\n\r\n"), lifetime.Token);
                    using var socket = WebSocket.CreateFromStream(stream, true, null, TimeSpan.FromSeconds(20));
                    await MonitorForm.SendHomeAssistantMessageAsync(socket, new { type = "auth_required" }, lifetime.Token);
                    using var auth = await MonitorForm.ReceiveHomeAssistantMessageAsync(socket, lifetime.Token);
                    var valid = HaApi.Text(auth.RootElement, "access_token") == "test-ha-secret";
                    await MonitorForm.SendHomeAssistantMessageAsync(socket, new { type = valid ? "auth_ok" : "auth_invalid" }, lifetime.Token);
                    if (!valid) return;
                    while (!lifetime.IsCancellationRequested)
                    {
                        using var command = await MonitorForm.ReceiveHomeAssistantMessageAsync(socket, lifetime.Token);
                        var root = command.RootElement; var id = root.GetProperty("id").GetInt32(); var type = HaApi.Text(root, "type");
                        if (Stall) { await Task.Delay(Timeout.Infinite, lifetime.Token); return; }
                        object? result = type switch
                        {
                            "get_states" => JsonSerializer.Deserialize<JsonElement>(HaStates),
                            "config/entity_registry/list" => JsonSerializer.Deserialize<JsonElement>(HaRegistry),
                            "camera/stream" => new { url = "/api/hls/test/master.m3u8" },
                            _ => null
                        };
                        await MonitorForm.SendHomeAssistantMessageAsync(socket, new { type = "event", id = id + 100 }, lifetime.Token);
                        if (type == "camera/stream" && HaApi.Text(root, "entity_id") != "camera.front") WrongIdSeen = true;
                        await MonitorForm.SendHomeAssistantMessageAsync(socket, new { type = "result", id, success = !(DenyRegistry && type == "config/entity_registry/list"), result }, lifetime.Token);
                    }
                }
                else
                {
                    SawBearer |= header.Contains("Authorization: Bearer test-ha-secret", StringComparison.OrdinalIgnoreCase);
                    var playlist = path.EndsWith("master.m3u8") ? "#EXTM3U\n#EXT-X-STREAM-INF:BANDWIDTH=200000\nmedia.m3u8\n" : "#EXTM3U\n#EXT-X-TARGETDURATION:2\n#EXT-X-MEDIA-SEQUENCE:0\n#EXTINF:2,\nsegment.ts\n#EXT-X-ENDLIST\n";
                    var bytes = path.EndsWith(".ts") ? Video : Encoding.UTF8.GetBytes(playlist);
                    var type = path.EndsWith(".ts") ? "video/mp2t" : "application/vnd.apple.mpegurl";
                    await stream.WriteAsync(Encoding.ASCII.GetBytes($"HTTP/1.1 200 OK\r\nContent-Type: {type}\r\nContent-Length: {bytes.Length}\r\nConnection: close\r\n\r\n"), lifetime.Token);
                    await stream.WriteAsync(bytes, lifetime.Token);
                }
            }
            catch (Exception e) when (e is IOException or OperationCanceledException or WebSocketException or ObjectDisposedException) { }
        }
        public void Dispose() { lifetime.Cancel(); listener.Stop(); }
    }
    private static void TestHomeAssistantDiscovery()
    {
        Task.Run(TestHaProtocol).GetAwaiter().GetResult();
        var catalog = HaApi.Parse(JsonSerializer.Deserialize<JsonElement>(HaStates), JsonSerializer.Deserialize<JsonElement>(HaRegistry));
        var camera = catalog.Cameras.Single(c => c.Id == "camera.front");
        if (catalog.Cameras.Count != 2 || catalog.Sensors.Count != 3 || catalog.Suggest(camera, false)?.Id != "binary_sensor.front_motion" ||
            catalog.Suggest(camera, true)?.Id != "binary_sensor.front_person" || catalog.Cameras.Single(c => c.Id == "camera.offline").Available)
            throw new Exception("HA discovery filtering, availability or device suggestions failed.");
        var ambiguous = catalog with { Sensors = catalog.Sensors.Concat([new HaEntity("binary_sensor.front_motion2", "Motion", "off", "front", "motion")]).ToArray() };
        if (ambiguous.Suggest(camera, false) is not null || HaApi.Parse(JsonSerializer.Deserialize<JsonElement>(HaStates), null).Suggest(camera, false) is not null)
            throw new Exception("Ambiguous/unknown device association was guessed.");
        var old = new CameraEntry { Name = "Bestehend", StreamUrl = "rtsp://example.invalid/old", HomeAssistantStreamUrl = "rtsp://example.invalid/old", PersonEnabled = true, MotionEntityId = "binary_sensor.old", PersonEntityId = "binary_sensor.old_person" };
        var before = JsonSerializer.Serialize(old);
        using var settings = new SettingsForm(new Settings { Cameras = [old], MotionRetentionDays = 0 }, SettingsLocation.DefaultFolder);
        var tabs = (V2ConnectionTabs)settings.Controls.Find("CameraConnectionTabs", true).Single();
        var selection = new HaCameraSelection(camera.Id, camera.Name, "binary_sensor.front_motion", "binary_sensor.front_person", HaApi.Source(camera.Id), false);
        if (!tabs.ImportHomeAssistant(selection) || tabs.ImportHomeAssistant(selection with { Source = "rtsp://other.invalid/live" })) throw new Exception("HA import duplicate detection failed.");
        var read = (List<CameraEntry>)typeof(SettingsForm).GetMethod("ReadCameras", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.Invoke(settings, null)!;
        if (read.Count != 2 || JsonSerializer.Serialize(read[0]) != before || read[1].HomeAssistantEntityId != camera.Id || read[1].Reolink.Enabled || read[1].Direct.Enabled ||
            read[1].MotionEntityId != selection.Motion || read[1].PersonEntityId != selection.Person || !read[1].MotionEnabled || read[1].PersonEnabled != true || JsonSerializer.Serialize(old) != before)
            throw new Exception("HA import overwrote existing configuration or lost source/sensors.");
        using (var monitor = new MonitorForm(new Settings { Cameras = [read[1]], HomeAssistantUrl = "http://127.0.0.1:1", HomeAssistantToken = "test", MotionRetentionDays = 0 }))
        {
            typeof(MonitorForm).GetMethod("ConfigureHomeAssistantStreams", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.Invoke(monitor, null);
            if (!read[1].StreamUrl.StartsWith("http://127.0.0.1:") || read[1].StoredStreamUrl != selection.Source)
                throw new Exception("Runtime HA source was not attached or its persistent identity changed.");
            ((IDisposable?)typeof(MonitorForm).GetField("homeAssistantStreams", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(monitor))?.Dispose();
        }
        var entry = read[1]; entry.HomeAssistantRuntimeSource = selection.Source; entry.StreamUrl = "http://127.0.0.1:12345/runtime-secret/index.m3u8";
        var backup = SettingsBackup.Serialize(new Settings { Cameras = read });
        if (backup.Contains("runtime-secret") || SettingsBackup.Parse(backup).Cameras[1].StreamUrl != selection.Source) throw new Exception("Temporary HLS URL persisted or entity source lost.");
        using var dialog = new HomeAssistantDiscoveryForm("http://127.0.0.1:1", "test", false, "", false, false);
        dialog.LoadCatalog(catalog);
        if (dialog.Controls.Find("ImportHaDiscoveredCamera", true).Single().Enabled || dialog.Controls.Find("ApplyHaDiscoveredSensors", true).Single().Enabled)
            throw new Exception("HA import allowed before a video frame or wrong update target.");
        using var host = new RenderHost { Opacity = 0, ShowInTaskbar = false, Location = new System.Drawing.Point(-30000, -30000),
            StartPosition = FormStartPosition.Manual, ClientSize = new System.Drawing.Size(920, 530) };
        dialog.TopLevel = false; dialog.FormBorderStyle = FormBorderStyle.None; dialog.Dock = DockStyle.Fill;
        host.Controls.Add(dialog); host.Show(); dialog.Show(); Pump(100);
        using var image = new System.Drawing.Bitmap(dialog.Width, dialog.Height); dialog.DrawToBitmap(image, new System.Drawing.Rectangle(0, 0, image.Width, image.Height));
        image.Save(Path.Combine(Environment.CurrentDirectory, "v2-ha-discovery.png"));
        Console.WriteLine("HA catalog/sensor suggestions, ambiguity, offline cameras, protected runtime source, duplicate import and existing configuration preservation passed.");
    }
    private static async Task TestHaProtocol()
    {
        using var server = new FakeHa();
        var catalog = await HaApi.Discover(server.Address, "test-ha-secret", false, CancellationToken.None);
        if (catalog.Cameras.Count != 2) throw new Exception("Authenticated HA catalog missing.");
        server.DenyRegistry = true;
        if ((await HaApi.Discover(server.Address, "test-ha-secret", false, CancellationToken.None)).RegistryAvailable) throw new Exception("HA registry permission failure not handled.");
        try { await HaApi.Discover(server.Address, "wrong", false, CancellationToken.None); throw new Exception("Invalid HA token accepted."); } catch (InvalidDataException) { }
        using (var bridge = new HomeAssistantStreams(server.Address, "test-ha-secret", false, ["camera.front"]))
        using (var http = new HttpClient())
        {
            var first = await http.GetStringAsync(bridge.Url("camera.front"));
            if (server.WrongIdSeen || !server.SawBearer || !first.Contains("/resource/") || first.Contains(server.Address) || !first.Contains(".m3u8")) throw new Exception("HA authenticated HLS bridge/playlist rewrite failed.");
            var origin = new Uri(server.Address + "api/hls/test/master.m3u8");
            foreach (var bad in new[] { "http://example.invalid/api/hls/x", "/api/states", "//example.invalid/api/hls/x" })
            { try { bridge.SafeUri(origin, bad); throw new Exception("HA token could leave stream origin."); } catch (InvalidDataException) { } }
            var tags = bridge.Playlist(origin, "#EXTM3U\n#EXT-X-MAP:URI=\"init.mp4\"\n#EXT-X-KEY:METHOD=AES-128,URI=\"key\"\nsegment.ts\n");
            if (tags.Contains("URI=\"init.mp4") || !tags.Contains("/segment.ts")) throw new Exception("HLS tags or extension lost.");
            await TestHaVideo(server, bridge);
        }
        server.Stall = true;
        using var cancel = new CancellationTokenSource(150);
        try { await HaApi.Discover(server.Address, "test-ha-secret", false, cancel.Token); throw new Exception("HA discovery cancellation ignored."); } catch (OperationCanceledException) { }
        Console.WriteLine("HA WebSocket auth/result correlation, permissions fallback, cancellation and HLS origin confinement passed.");
    }
    private static async Task TestHaVideo(FakeHa server, HomeAssistantStreams bridge)
    {
        var executable = Path.Combine(Environment.CurrentDirectory, "publish-v2-beta", "ffmpeg.exe");
        if (!File.Exists(executable)) throw new Exception("Bundled FFmpeg required for HA video test.");
        var fixture = Path.Combine(Environment.CurrentDirectory, "ha-test-" + Guid.NewGuid().ToString("N") + ".ts");
        try
        {
            var start = new System.Diagnostics.ProcessStartInfo(executable) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true };
            foreach (var arg in new[] { "-hide_banner", "-loglevel", "error", "-f", "lavfi", "-i", "color=c=blue:s=160x90:r=25", "-t", "2", "-c:v", "mpeg2video", "-f", "mpegts", fixture }) start.ArgumentList.Add(arg);
            using var ffmpeg = System.Diagnostics.Process.Start(start)!; var errors = ffmpeg.StandardError.ReadToEndAsync(); await ffmpeg.WaitForExitAsync(); await errors;
            if (ffmpeg.ExitCode != 0) throw new Exception("Cannot create HLS test fixture.");
            server.Video = await File.ReadAllBytesAsync(fixture);
            if (!await CameraStreamTest.DecodeFrame(bridge.Url("camera.front"), CancellationToken.None, executable)) throw new Exception("No video decoded through authenticated HA HLS proxy.");
            Console.WriteLine("Real FFmpeg decoded video via authenticated HA WebSocket/HLS bridge and rewritten playlists.");
        }
        finally { File.Delete(fixture); }
    }
}
