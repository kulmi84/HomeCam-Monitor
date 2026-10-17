using HomeCamMonitor;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
namespace V2Isolation;

internal static partial class Check
{
    private sealed class InlineProgress<T>(Action<T> report) : IProgress<T> { public void Report(T value) => report(value); }
    private static void TestDiscovery()
    {
        Task.Run(TestDiscoveryProtocols).GetAwaiter().GetResult();
        using var dialog = new CameraDiscoveryForm();
        var ip = IPAddress.Parse("192.168.42.10");
        var port = new NetworkCameraCandidate(ip, "", "", "", "one", 554, false);
        dialog.AddCandidate(port); dialog.AddCandidate(port with { RtspService = true });
        var onvif = port with { Name = "Kamera", Manufacturer = "Test", DeviceUrl = "http://192.168.42.10/onvif/device_service" };
        if (CameraDiscoveryForm.ManufacturerText(onvif) != "Test" ||
            CameraDiscoveryForm.ManufacturerText(onvif with { Manufacturer = "" }) != "Noch nicht abgefragt" ||
            CameraDiscoveryForm.ManufacturerText(port) != "Ohne ONVIF unbekannt") throw new Exception("Manufacturer discovery states are misleading.");
        dialog.AddCandidate(onvif); dialog.AddCandidate(port);
        dialog.AddCandidate(onvif with { Address = IPAddress.Parse("192.168.42.11") });
        var grid = (DataGridView)dialog.Controls.Find("DiscoveredCameras", true).Single();
        if (grid.Rows.Count != 1 || ((NetworkCameraCandidate)grid.Rows[0].Tag!).DeviceUrl != onvif.DeviceUrl ||
            dialog.Controls.Find("ImportDiscoveredCamera", true).Single().Enabled || dialog.Result is not null)
            throw new Exception("Discovery deduplication or unverified import gating failed.");
        var old = new CameraEntry { Name = "Vorhanden", StreamUrl = "rtsp://example.invalid/old", MotionEntityId = "binary_sensor.old", PersonEntityId = "binary_sensor.person" };
        using var settings = new SettingsForm(new Settings { Cameras = [old], MotionRetentionDays = 0 }, SettingsLocation.DefaultFolder);
        settings.PrepareForDisplay(); var cameras = FindGrid(settings);
        var tabs = (V2ConnectionTabs)settings.Controls.Find("CameraConnectionTabs", true).Single();
        List<CameraEntry> Read() => (List<CameraEntry>)typeof(SettingsForm).GetMethod("ReadCameras", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.Invoke(settings, null)!;
        var before = JsonSerializer.Serialize(Read()[0]);
        var entry = new CameraEntry { Name = "Gefunden", Direct = new DirectCamera { Enabled = true, DeviceUrl = onvif.DeviceUrl,
            RtspUrl = "rtsp://192.168.42.10:554/live", Username = "test", ProtectedPassword = CameraPassword.Protect("new-test-secret"), DiscoveryId = "urn:uuid:test-camera" } };
        if (!tabs.ImportDiscovered(entry) || tabs.ImportDiscovered(entry)) throw new Exception("Discovered import is missing or duplicates a camera.");
        var moved = new CameraEntry { Name = "Nicht überschreiben", Direct = entry.Direct.Clone() };
        moved.Direct.DeviceUrl = "http://192.168.42.11/onvif/device_service"; moved.Direct.RtspUrl = "rtsp://192.168.42.11/live";
        if (tabs.ImportDiscovered(moved)) throw new Exception("Same ONVIF identity imported twice after an address change.");
        var read = Read();
        if (read.Count != 2 || JsonSerializer.Serialize(read[0]) != before || read[1].Direct.ProtectedPassword != entry.Direct.ProtectedPassword ||
            read[1].MotionEnabled || read[1].RecordingEnabled || old.Direct.Enabled)
            throw new Exception("Discovery import changed an existing camera or lost protected credentials.");
        var backup = SettingsBackup.Serialize(new Settings { Cameras = read });
        if (backup.Contains("new-test-secret") || backup.Contains("test@192.168.42.10")) throw new Exception("Direct runtime credentials leaked into saved settings.");
        var restored = SettingsBackup.Parse(backup);
        if (!new Uri(restored.Cameras[1].StreamUrl).UserInfo.Contains("new-test-secret") ||
            CameraPassword.Unprotect(restored.Cameras[1].Direct.ProtectedPassword) != "new-test-secret")
            throw new Exception("Protected runtime URL was not restored after loading clean settings.");
        var legacy = System.Text.Json.Nodes.JsonNode.Parse(backup)!;
        legacy["Settings"]!["Cameras"]![1]!["StreamUrl"] = read[1].StreamUrl;
        var migrated = SettingsBackup.Parse(legacy.ToJsonString());
        if (SettingsBackup.Serialize(migrated).Contains("new-test-secret")) throw new Exception("Legacy direct runtime URL was not cleaned on resave.");
        using var host = new RenderHost { Opacity = 0, ShowInTaskbar = false, Location = new System.Drawing.Point(-30000, -30000),
            StartPosition = FormStartPosition.Manual, ClientSize = new System.Drawing.Size(1050, 820) };
        dialog.TopLevel = false; dialog.FormBorderStyle = FormBorderStyle.None; dialog.Dock = DockStyle.Fill;
        host.Controls.Add(dialog); host.Show(); dialog.Show(); Pump(150);
        using var image = new System.Drawing.Bitmap(dialog.Width, dialog.Height);
        dialog.DrawToBitmap(image, new System.Drawing.Rectangle(0, 0, image.Width, image.Height));
        image.Save(Path.Combine(Environment.CurrentDirectory, "v2-discovery.png"));
        dialog.Hide(); host.Hide();
        Console.WriteLine("Discovery UI deduplication, unverified import lock, protected import and existing camera preservation passed.");
    }
    private static async Task TestDiscoveryProtocols()
    {
        var scope = CameraNetworkRange.Parse("192.168.42.123/24");
        if (scope.Count != 254 || scope.Contains(IPAddress.Parse("192.168.42.0")) || !scope.Contains(IPAddress.Parse("192.168.42.254")) ||
            CameraNetworkRange.Parse("192.168.42.10").Count != 1 || CameraNetworkRange.Parse("192.168.42.0/31").Count != 2)
            throw new Exception("CIDR host range is incorrect.");
        foreach (var invalid in new[] { "192.168.0.0/19", "8.8.8.0/24", "192.168.1.2/33", "::1", "192.168.1.2/24/3" })
        {
            try { CameraNetworkRange.Parse(invalid); throw new Exception("Accepted invalid network range."); } catch (ArgumentException) { }
        }
        if (!NetworkCameraDiscovery.ParsePorts("554,8554,554").SequenceEqual(new[] { 554, 8554 })) throw new Exception("Port dedup failed.");
        try { NetworkCameraDiscovery.ParsePorts("554,0"); throw new Exception("Invalid port accepted."); } catch (ArgumentException) { }
        var loop = CameraNetworkRange.Parse("127.0.0.1", true); const string id = "urn:uuid:test";
        string Reply(string relation, string url) => $"<s:Envelope xmlns:s='http://www.w3.org/2003/05/soap-envelope' xmlns:a='http://schemas.xmlsoap.org/ws/2004/08/addressing' xmlns:d='http://schemas.xmlsoap.org/ws/2005/04/discovery'><s:Header><a:RelatesTo>{relation}</a:RelatesTo></s:Header><s:Body><d:ProbeMatches><d:ProbeMatch><a:EndpointReference><a:Address>urn:uuid:camera</a:Address></a:EndpointReference><d:Scopes>onvif://www.onvif.org/name/Test%20Camera</d:Scopes><d:XAddrs>{url}</d:XAddrs></d:ProbeMatch></d:ProbeMatches></s:Body></s:Envelope>";
        var requests = new HashSet<string> { id };
        var match = NetworkCameraDiscovery.ParseMatches(Encoding.UTF8.GetBytes(Reply(id, "http://127.0.0.1/onvif/device_service")), IPAddress.Loopback, loop, requests);
        if (match.Count != 1 || match[0].Name != "Test Camera") throw new Exception("ONVIF scopes/XAddr parsing failed.");
        foreach (var xml in new[] { Reply("wrong", "http://127.0.0.1/onvif/device_service"), Reply(id, "http://192.168.42.1/onvif/device_service"),
            Reply(id, "http://user:secret@127.0.0.1/onvif/device_service"), "<!DOCTYPE x [<!ENTITY test SYSTEM 'file:///test'>]><x>&test;</x>" })
            if (NetworkCameraDiscovery.ParseMatches(Encoding.UTF8.GetBytes(xml), IPAddress.Loopback, loop, requests).Count != 0)
                throw new Exception("Unsolicited, redirected or unsafe discovery packet accepted.");
        using (var responder = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0)))
        {
            var serve = Task.Run(async () =>
            {
                var packet = await responder.ReceiveAsync();
                var request = XDocument.Parse(Encoding.UTF8.GetString(packet.Buffer));
                var messageId = request.Descendants().Single(e => e.Name.LocalName == "MessageID").Value;
                var bytes = Encoding.UTF8.GetBytes(Reply(messageId, "http://127.0.0.1/onvif/device_service"));
                await responder.SendAsync(bytes, packet.RemoteEndPoint); await responder.SendAsync(bytes, packet.RemoteEndPoint);
            });
            var received = new List<NetworkCameraCandidate>();
            await NetworkCameraDiscovery.Discover(IPAddress.Loopback, loop, new InlineProgress<NetworkCameraCandidate>(received.Add), CancellationToken.None,
                (IPEndPoint)responder.Client.LocalEndPoint!, TimeSpan.FromSeconds(1)); await serve;
            if (received.Count != 1) throw new Exception("Actual UDP discovery does not deduplicate replies.");
        }
        using (var cancel = new CancellationTokenSource(70))
        {
            var watch = System.Diagnostics.Stopwatch.StartNew();
            try { await NetworkCameraDiscovery.Discover(IPAddress.Loopback, loop, new InlineProgress<NetworkCameraCandidate>(_ => { }), cancel.Token, new IPEndPoint(IPAddress.Loopback, 65520)); throw new Exception("Discovery ignored cancel."); }
            catch (OperationCanceledException) { }
            if (watch.Elapsed.TotalSeconds > 2) throw new Exception("Discovery cancellation too slow.");
        }
        async Task<NetworkCameraCandidate?> Port(string response)
        {
            var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start();
            var server = Task.Run(async () =>
            {
                using var client = await listener.AcceptTcpClientAsync(); var stream = client.GetStream();
                var bytes = new byte[1024]; await stream.ReadAsync(bytes);
                await stream.WriteAsync(Encoding.ASCII.GetBytes(response));
            });
            try { var result = await NetworkCameraDiscovery.ProbePort(IPAddress.Loopback, ((IPEndPoint)listener.LocalEndpoint).Port, CancellationToken.None); await server; return result; }
            finally { listener.Stop(); }
        }
        if (await Port("HTTP/1.1 200 OK\r\n\r\n") is not { RtspService: false } ||
            await Port("RTSP/1.0 401 Unauthorized\r\nCSeq: 1\r\n\r\n") is not { RtspService: true, DeviceUrl.Length: 0 })
            throw new Exception("Open HTTP port confused with RTSP or auth challenge treated as a stream.");
        var stalled = new TcpListener(IPAddress.Loopback, 0); stalled.Start();
        try
        {
            using var cancel = new CancellationTokenSource(80);
            try { await NetworkCameraDiscovery.ScanPorts(loop, [((IPEndPoint)stalled.LocalEndpoint).Port], new InlineProgress<NetworkCameraCandidate>(_ => { }),
                new InlineProgress<int>(_ => { }), cancel.Token); throw new Exception("Port scan ignored cancellation."); }
            catch (OperationCanceledException) { }
        }
        finally { stalled.Stop(); }
        var original = Environment.GetEnvironmentVariable("HOMECAM_FAKE_FRAME_TEST");
        try
        {
            foreach (var mode in new[] { "empty", "frame" })
            {
                Environment.SetEnvironmentVariable("HOMECAM_FAKE_FRAME_TEST", mode);
                var result = await CameraStreamTest.DecodeFrame("rtsp://127.0.0.1/test", CancellationToken.None, Environment.ProcessPath!);
                if (result != (mode == "frame")) throw new Exception("Exit zero without a decoded frame was accepted as video.");
            }
            Environment.SetEnvironmentVariable("HOMECAM_FAKE_FRAME_TEST", "wait");
            using var cancel = new CancellationTokenSource(100);
            try { await CameraStreamTest.DecodeFrame("rtsp://127.0.0.1/test", cancel.Token, Environment.ProcessPath!); throw new Exception("Video test ignored cancellation."); }
            catch (OperationCanceledException) { }
        }
        finally { Environment.SetEnvironmentVariable("HOMECAM_FAKE_FRAME_TEST", original); }
        await TestDiscoveryMetadata();
        Console.WriteLine("WS-Discovery UDP/dedup/correlation, XML limits, scope validation, RTSP-vs-HTTP, scan cancellation and decoded-frame verification passed.");
    }
    private static async Task TestDiscoveryMetadata()
    {
        async Task Serve(string[] responses, Func<OnvifClient, string, Task> check)
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start();
            var url = $"http://127.0.0.1:{((IPEndPoint)listener.LocalEndpoint).Port}/onvif/device_service";
            var serving = Task.Run(async () =>
            {
                foreach (var payload in responses)
                {
                    using var socket = await listener.AcceptTcpClientAsync(timeout.Token); var stream = socket.GetStream();
                    var header = new List<byte>(); var one = new byte[1];
                    while (header.Count < 16000)
                    {
                        if (await stream.ReadAsync(one, timeout.Token) == 0) throw new IOException(); header.Add(one[0]);
                        if (header.Count >= 4 && header.TakeLast(4).SequenceEqual(new byte[] { 13, 10, 13, 10 })) break;
                    }
                    var length = int.Parse(Encoding.ASCII.GetString(header.ToArray()).Split("\r\n").Single(s => s.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase)).Split(':')[1]);
                    var body = new byte[length]; await stream.ReadExactlyAsync(body, timeout.Token);
                    var bytes = Encoding.UTF8.GetBytes("<s:Envelope xmlns:s='http://www.w3.org/2003/05/soap-envelope' xmlns:tds='http://www.onvif.org/ver10/device/wsdl' xmlns:tt='http://www.onvif.org/ver10/schema'><s:Body>" + payload + "</s:Body></s:Envelope>");
                    await stream.WriteAsync(Encoding.ASCII.GetBytes($"HTTP/1.1 200 OK\r\nContent-Type: application/soap+xml\r\nContent-Length: {bytes.Length}\r\nConnection: close\r\n\r\n"), timeout.Token);
                    await stream.WriteAsync(bytes, timeout.Token);
                }
            });
            try { using var client = new OnvifClient("test", "test-secret", true); await check(client, url); await serving; }
            finally { timeout.Cancel(); listener.Stop(); }
        }
        await Serve(["<tds:GetDeviceInformationResponse><tds:Manufacturer>Test Hersteller</tds:Manufacturer><tds:Model>Modell 1</tds:Model></tds:GetDeviceInformationResponse>"], async (client, url) =>
        {
            var info = await client.Information(url, CancellationToken.None);
            if (info.Manufacturer != "Test Hersteller" || info.Model != "Modell 1") throw new Exception("ONVIF manufacturer/model metadata lost.");
        });
        await Serve(["<tds:GetSystemDateAndTimeResponse/>", "<tds:GetCapabilitiesResponse><tds:Capabilities><tt:Media><tt:XAddr>http://192.168.99.99/media</tt:XAddr></tt:Media></tds:Capabilities></tds:GetCapabilitiesResponse>"], async (client, url) =>
        {
            try { await client.Profiles(url, CancellationToken.None); throw new Exception("Discovery forwarded credentials to another host."); }
            catch (InvalidDataException) { }
        });
    }
    private static async Task DiscoverLocal()
    {
        var network = CameraNetwork.Available().FirstOrDefault() ?? throw new Exception("No active IPv4 adapter.");
        var scope = CameraNetworkRange.Parse($"{network.Address}/{Math.Max(network.Prefix, 24)}");
        var count = 0;
        await NetworkCameraDiscovery.Discover(network.Address, scope, new InlineProgress<NetworkCameraCandidate>(_ => Interlocked.Increment(ref count)), CancellationToken.None);
        Console.WriteLine($"MK-PC20 local ONVIF discovery completed: {count} response(s). No port scan or credential requests were made.");
    }
}
