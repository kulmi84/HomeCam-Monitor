using HomeCamMonitor;
using System.Text.Json;
using System.Text.RegularExpressions;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Server;
namespace V2Isolation;
internal static partial class Check
{
    [STAThread]
    private static void Main(string[] args)
    {
        if (args.Contains("--english"))
        {
            System.Globalization.CultureInfo.CurrentUICulture = System.Globalization.CultureInfo.GetCultureInfo("en-US");
            System.Globalization.CultureInfo.DefaultThreadCurrentUICulture = System.Globalization.CultureInfo.CurrentUICulture;
            args = args.Where(a => a != "--english").ToArray();
        }
        if (args.Contains("-progress") && Environment.GetEnvironmentVariable("HOMECAM_FAKE_FRAME_TEST") is { } frameMode)
        {
            if (frameMode == "wait") Thread.Sleep(30000);
            Console.WriteLine(frameMode == "frame" ? "frame=1" : "frame=0"); return;
        }
        Application.EnableVisualStyles();
        if (args.Contains("--window-icon-theme")) { TestWindowIconTheme(); return; }
        if (args.Contains("--helper-paths")) { TestReolinkHelperPaths(); return; }
        if (args.Contains("--logo-variants")) { TestLogoVariants(); return; }
        if (args.Length == 1 && args[0] == "--localization") { TestLocalization(); return; }
#if RELEASE_V2
        if (ApplicationBranding.ProductName != "HomeCam Monitor V2") throw new Exception("Final V2 branding is incorrect.");
#endif
        if (args.Length == 1 && args[0] == "--docs-screenshots") { CaptureDocumentationScreenshots(); return; }
        if (args.Length == 1 && args[0] == "--local-motion") { TestLocalMotion(); return; }
        if (args.Length == 1 && args[0] == "--ha-discovery") { TestHomeAssistantDiscovery(); return; }
        if (args.Length == 2 && args[0] == "--decode-local")
        {
            if (!Uri.TryCreate(args[1], UriKind.Absolute, out var local) || !System.Net.IPAddress.TryParse(local.Host, out var ip) || !System.Net.IPAddress.IsLoopback(ip)) throw new Exception("Loopback URL required.");
            if (!CameraStreamTest.DecodeFrame(args[1], CancellationToken.None, Path.Combine(Environment.CurrentDirectory, "publish-v2-beta", "ffmpeg.exe")).GetAwaiter().GetResult()) throw new Exception("No decoded test frame.");
            Console.WriteLine("Real ffmpeg decoded a frame from the local synthetic RTSP server."); return;
        }
        if (args.Length == 1 && args[0] == "--discovery") { TestDiscovery(); return; }
        if (args.Length == 1 && args[0] == "--discover-local") { Task.Run(DiscoverLocal).GetAwaiter().GetResult(); return; }
        if (args.Length == 1 && args[0] == "--uid-playback") { TestUidPlaybackOptions(); return; }
        if (args.Length >= 3 && args[0] == "--diagnose-saved")
        { DiagnoseSaved(args[1], args[2], args.Length == 4 ? args[3] : null).GetAwaiter().GetResult(); return; }
        if (args.Length == 3 && args[0] == "mqtt-rtsp" && args[1] == "--config")
        { FakeHelper(args[2]).GetAwaiter().GetResult(); return; }
        if (args.Length == 3 && args[0] == "--helper-smoke")
        { HelperSmoke(args[1], args[2]).GetAwaiter().GetResult(); return; }
        if (args.Length == 1 && args[0] == "--settings-paint") { SettingsPaint(); return; }
        if (args.Length == 1 && args[0] == "--settings-interactions") { TestRecordingAndOrder(); return; }
        if (args.Length == 1 && args[0] == "--settings-presentation") { TestSettingsPresentation(); return; }
        if (args.Length == 1 && args[0] == "--settings-tabs") { TestCameraAvailabilityAndSources(); return; }
        if (args.Length == 1 && args[0] == "--direct") { TestDirectConnection(); return; }
        TestStorageDefaults();
        if (args.Contains("--storage-defaults")) return;
        if (ApplicationChannel.StorageName != "HomeCamMonitor-V2-Beta" ||
            SettingsLocation.RegistryPath != @"Software\HomeCamMonitor-V2-Beta" ||
            Path.GetFileName(SettingsLocation.DefaultFolder) != ApplicationChannel.DefaultSettingsFolderName)
            throw new Exception("V2 settings identity is not isolated.");
        if (Path.GetFileName(RecordingStorage.ManualSnapshots) != ApplicationChannel.RecordingFolderName ||
            Path.GetFileName(RecordingStorage.ManualVideos) != ApplicationChannel.RecordingFolderName)
            throw new Exception("V2 recording folders overlap with V1.");
        var settings = SettingsStore.CreateForNewInstallation();
        if (settings.DirectHomeAssistantEnabled || settings.MotionDetectionEnabled || settings.Cameras.Count != 0)
            throw new Exception("V2 must start without Home Assistant or legacy event listeners.");
        Console.WriteLine("V2 isolation and fresh installation defaults passed.");
        var priorTrace = Environment.GetEnvironmentVariable("HOMECAM_DIAG_TRACE");
        try
        {
            Environment.SetEnvironmentVariable("HOMECAM_DIAG_TRACE", "1");
            if (ReolinkDiagnostics.Classify("HCM_TRACE INPUT ts_us=123 bytes=456 key=1\r") != "HCM_TRACE INPUT ts_us=123 bytes=456 key=1" ||
                ReolinkDiagnostics.Classify("HCM_TRACE INPUT ts_us=123 bytes=456 key=1 private-password") is not null ||
                ReolinkDiagnostics.Classify("private-password HCM_TRACE LAG count=1") is not null)
                throw new Exception("Numeric stream diagnostics must reject non-whitelisted data.");
        }
        finally { Environment.SetEnvironmentVariable("HOMECAM_DIAG_TRACE", priorTrace); }
        foreach (var marker in new[] { "CameraLoginFail", "DiscoveryTimeout", "RegisterError", "Logging in", "Trying relay discovery", "UnknownEncryption" })
        {
            var classified = ReolinkDiagnostics.Classify(marker + " private-uid private-password");
            if (classified is null || classified.Contains("private-", StringComparison.Ordinal))
                throw new Exception("Diagnostic classification must omit raw helper data.");
        }
        if (ReolinkDiagnostics.Classify("unrecognized private-password") is not null ||
            !ReolinkDiagnostics.IsFailure(ReolinkDiagnostics.Classify("CameraLoginFail")!))
            throw new Exception("Diagnostic failure classification failed.");
        var secret = "test password \\\"\nGrüße\t💡";
        var encrypted = CameraPassword.Protect(secret);
        if (encrypted.Contains(secret, StringComparison.Ordinal) || CameraPassword.Unprotect(encrypted) != secret)
            throw new Exception("Password protection failed.");
        var camera = new CameraEntry { Name = "Einfahrt", MotionEnabled = true,
            Reolink = new ReolinkCamera { Enabled = true, Uid = "FAKE0123456789AB", ProtectedPassword = encrypted } };
        var config = ReolinkConfig.Build([camera], 18554, 18883, "gateway-secret");
        if (!config.Contains("bind = \"127.0.0.1\"") || !config.Contains("discovery = \"cellular\"") ||
            !config.Contains(ReolinkConfig.Quote(secret)) || camera.Reolink.StreamUrl(18554).Contains(camera.Reolink.Uid))
            throw new Exception("Unsafe UID config or local proxy URL.");
        var values = new ReolinkEvents([camera]);
        var count = 0;
        values.Motion += _ => count++;
        var topic = "neolink/" + camera.Reolink.MountName + "/status/motion";
        values.Accept(topic, "on", true); // Retained replay is not an event.
        values.Accept(topic, "off", false);
        values.Accept(topic, "on", false);
        values.Accept(topic, "on", false);
        values.Accept("neolink/unknown/status/motion", "on", false);
        values.Accept(topic, "off", false);
        values.Accept(topic, "on", false);
        if (count != 2) throw new Exception("Motion edge handling failed.");
        camera.StreamUrl = camera.Reolink.StreamUrl(18554);
        var backup = SettingsBackup.Parse(SettingsBackup.Serialize(new Settings { Cameras = [camera] }));
        if (CameraPassword.Unprotect(backup.Cameras[0].Reolink.ProtectedPassword) != secret)
            throw new Exception("Backup lost protected Reolink credentials.");
        using (var form = new SettingsForm(new Settings { Cameras = [camera] }, SettingsLocation.DefaultFolder))
        {
            form.PrepareForDisplay();
            if (form.Controls.Find("ReolinkConnection", true).Length != 1)
                throw new Exception("Reolink settings are missing.");
            var grid = FindGrid(form);
            if (!grid.Columns["PersonEnabled"].Visible || !grid.Rows[0].Cells["PersonEnabled"].ReadOnly)
                throw new Exception("UID camera person filter must remain disabled.");
            if (form.Controls.Find("CameraConnectionTabs", true).Single() is not V2ConnectionTabs { TabCount: 3 })
                throw new Exception("Reolink/HA tabs missing.");
            using var preview = new RenderHost { ShowInTaskbar = false, StartPosition = FormStartPosition.Manual,
                Location = new System.Drawing.Point(-30000, -30000), Size = new System.Drawing.Size(980, 480), Opacity = 0,
                BackColor = System.Drawing.Color.FromArgb(24, 24, 27) };
            preview.Controls.Add(form.Controls.Find("ReolinkConnection", true)[0]);
            preview.Show(); Application.DoEvents();
            using var bitmap = new System.Drawing.Bitmap(preview.Width, preview.Height);
            preview.DrawToBitmap(bitmap, new System.Drawing.Rectangle(0, 0, bitmap.Width, bitmap.Height));
            bitmap.Save(Path.Combine(Environment.CurrentDirectory, "v2-settings.png"));
            preview.Hide();
        }
        Task.Run(() => TestBridge(camera)).GetAwaiter().GetResult();
        Task.Run(() => TestBridge(camera, true)).GetAwaiter().GetResult();
        using (var fresh = new SettingsForm(SettingsStore.CreateForNewInstallation(), SettingsLocation.DefaultFolder))
        {
            fresh.PrepareForDisplay();
            var grid = FindGrid(fresh);
            var index = grid.Rows.Add("Neue Reolink", "");
            grid.CurrentCell = grid.Rows[index].Cells[0];
            var group = fresh.Controls.Find("ReolinkConnection", true)[0];
            var connection = FindCheckbox(group);
            if (!connection.Enabled) throw new Exception("A newly added camera cannot enable UID access.");
            connection.Checked = true;
            if (!Convert.ToBoolean(grid.Rows[index].Cells["MotionEnabled"].Value) ||
                fresh.Controls.Find("DetectionEnabled", true).Length != 0)
                throw new Exception("Enabling UID did not activate the camera's motion detection.");
        }
        foreach (var useButton in new[] { false, true })
        using (var empty = new SettingsForm(SettingsStore.CreateForNewInstallation(), SettingsLocation.DefaultFolder))
        {
            empty.PrepareForDisplay();
            var group = empty.Controls.Find("ReolinkConnection", true)[0];
            var connection = FindCheckbox(group);
            if (!connection.Enabled) throw new Exception("UID must be available with an empty camera list.");
            if (useButton) typeof(Button).GetMethod("OnClick", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.Invoke(group.Controls.Find("AddReolinkCamera", true)[0], [EventArgs.Empty]);
            else connection.Checked = true;
            var grid = FindGrid(empty);
            var rows = grid.Rows.Cast<DataGridViewRow>().Where(r => !r.IsNewRow).ToArray();
            if (rows.Length != 1 || rows[0].Tag is not ReolinkCamera { Enabled: true } || !connection.Checked)
                throw new Exception("Adding the first Reolink camera failed.");
            var editor = (ReolinkEditor)group;
            editor.Store();
            grid.CurrentCell = rows[0].Cells["StreamUrl"];
            if (!connection.Checked) throw new Exception("Selecting a cell disabled Reolink.");
            using var host = new RenderHost { ShowInTaskbar = false, Opacity = 0,
                StartPosition = FormStartPosition.Manual, Location = new System.Drawing.Point(-30000, -30000),
                Size = new System.Drawing.Size(1000, 850) };
            var scroll = empty.Controls.Find("SettingsScrollArea", true)[0];
            host.Controls.Add(scroll); host.Show();
            Pump(200);
            var bounds = group.Bounds;
            var changes = 0;
            group.SizeChanged += (_, _) => changes++;
            group.LocationChanged += (_, _) => changes++;
            Pump(2200);
            if (group.Bounds != bounds || changes != 0) throw new Exception("Idle status updates make the Reolink layout jump.");
            host.Hide();
        }
        TestCameraAvailabilityAndSources();
        TestPerCameraDetection();
        TestDirectConnection();
        TestRecordingAndOrder();
        TestUidPlaybackOptions();
        TestDiscovery();
        TestSettingsPresentation();
        TestHomeAssistantConnection();
        SettingsPaint();
        Console.WriteLine("Password encryption, UID config, motion events, backup, settings and local gateway integration passed.");
    }
    private sealed class RenderHost : Form { protected override bool ShowWithoutActivation => true; }
    private static void TestHomeAssistantConnection()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(12));
        var listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
        listener.Start();
        var port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
        var server = Task.Run(async () =>
        {
            using var client = await listener.AcceptTcpClientAsync(timeout.Token);
            using var stream = client.GetStream();
            using var reader = new StreamReader(stream, System.Text.Encoding.ASCII, false, 1024, true);
            string? key = null; var size = 0;
            while (await reader.ReadLineAsync(timeout.Token) is { Length: > 0 } line)
            {
                size += line.Length; if (size > 8192) throw new Exception("Oversized test handshake.");
                if (line.StartsWith("Sec-WebSocket-Key:", StringComparison.OrdinalIgnoreCase)) key = line.Split(':', 2)[1].Trim();
            }
            if (key is null) throw new Exception("HA client did not request WebSocket upgrade.");
            var accept = Convert.ToBase64String(System.Security.Cryptography.SHA1.HashData(System.Text.Encoding.ASCII.GetBytes(key + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11")));
            await stream.WriteAsync(System.Text.Encoding.ASCII.GetBytes("HTTP/1.1 101 Switching Protocols\r\nUpgrade: websocket\r\nConnection: Upgrade\r\nSec-WebSocket-Accept: " + accept + "\r\n\r\n"), timeout.Token);
            using var socket = System.Net.WebSockets.WebSocket.CreateFromStream(stream, true, null, Timeout.InfiniteTimeSpan);
            async Task Send(string json) => await socket.SendAsync(System.Text.Encoding.UTF8.GetBytes(json), System.Net.WebSockets.WebSocketMessageType.Text, true, timeout.Token);
            async Task<JsonDocument> Receive()
            {
                var bytes = new byte[4096];
                var result = await socket.ReceiveAsync(new ArraySegment<byte>(bytes), timeout.Token);
                if (!result.EndOfMessage) throw new Exception("Unexpected fragmented test message.");
                return JsonDocument.Parse(bytes.AsMemory(0, result.Count));
            }
            await Send("{\"type\":\"auth_required\"}");
            using var auth = await Receive();
            if (auth.RootElement.GetProperty("access_token").GetString() != "test-token") throw new Exception("HA authentication token was not sent.");
            await Send("{\"type\":\"auth_ok\"}");
            using var subscription = await Receive();
            if (subscription.RootElement.GetProperty("type").GetString() != "subscribe_events" || subscription.RootElement.GetProperty("event_type").GetString() != "state_changed")
                throw new Exception("V2 did not subscribe to HA events.");
        });
        using var monitor = new MonitorForm(new Settings { MotionRetentionDays = 0, MotionDetectionEnabled = true, DirectHomeAssistantEnabled = true,
            HomeAssistantUrl = $"http://127.0.0.1:{port}", HomeAssistantToken = "test-token", Cameras = [
                new CameraEntry { Name = "HA", MotionEnabled = true, MotionEntityId = "binary_sensor.motion", StreamUrl = "rtsp://example.test/ha" },
                new CameraEntry { Name = "UID", Reolink = new ReolinkCamera { Enabled = true } } ] });
        var restart = typeof(MonitorForm).GetMethod("RestartMotionIntegration", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        var stop = typeof(MonitorForm).GetMethod("StopMotionIntegration", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        try
        {
            restart.Invoke(monitor, null);
            server.WaitAsync(timeout.Token).GetAwaiter().GetResult();
            if (typeof(MonitorForm).GetField("motionListener", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(monitor) is not null)
                throw new Exception("V2 unexpectedly opened the legacy HA listener.");
        }
        finally { stop.Invoke(monitor, null); listener.Stop(); }
        Console.WriteLine("Home Assistant WebSocket authentication/subscription with mixed sources passed.");
    }
    private static void TestCameraAvailabilityAndSources()
    {
        if (!JsonSerializer.Deserialize<CameraEntry>("{\"Name\":\"Old\",\"StreamUrl\":\"rtsp://example.test/old\"}")!.Active)
            throw new Exception("Existing cameras must migrate as active.");
        var ha = new CameraEntry { Name = "HA", StreamUrl = "rtsp://example.test/ha", PersonEntityId = "binary_sensor.person",
            MotionEntityId = "binary_sensor.motion", MotionEnabled = true, PersonEnabled = true };
        var uid = new CameraEntry { Name = "UID", StreamUrl = "rtsp://127.0.0.1:18554/camera", PersonEntityId = "binary_sensor.stale",
            Reolink = new ReolinkCamera { Enabled = true, Uid = "FAKE0123456789AB", ProtectedPassword = CameraPassword.Protect("test") } };
        var inactive = new CameraEntry { Name = "Inactive", Active = false, Reolink = uid.Reolink.Clone() };
        inactive.Reolink.Id = Guid.NewGuid().ToString("N");
        if (inactive.IsAvailable || inactive.ReceivesHomeAssistantEvents || uid.SupportsPersonDetection || !ha.SupportsPersonDetection)
            throw new Exception("Camera availability/capability routing failed.");
        var disabledConfig = ReolinkConfig.Build([inactive], 18554, 18830, "test");
        if (disabledConfig.Contains(inactive.Reolink.Uid)) throw new Exception("Disabled camera leaked into helper config.");
        using (var stopped = new ReolinkBridge([inactive], "missing-helper.exe"))
        { stopped.Start(); if (!stopped.Completion.IsCompletedSuccessfully) throw new Exception("Disabled camera started a helper."); }
        var restored = SettingsBackup.Parse(SettingsBackup.Serialize(new Settings { Cameras = [inactive] }));
        if (restored.Cameras[0].Active) throw new Exception("Backup reactivated an inactive camera.");
        using (var monitor = new MonitorForm(new Settings { MotionRetentionDays = 0, Cameras = [ha, uid, inactive], DirectHomeAssistantEnabled = true }))
        {
            var route = typeof(MonitorForm).GetMethod("TryGetCameraEvent", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
            bool Accepts(string entity, bool person)
            {
                using var message = JsonDocument.Parse(JsonSerializer.Serialize(new { type = "event", @event = new { data = new {
                    entity_id = entity, old_state = new { state = "off" }, new_state = new { state = "on" } } } }));
                object[] arguments = [message.RootElement, "", false];
                return (bool)route.Invoke(monitor, arguments)! && (bool)arguments[2] == person && (string)arguments[1] == ha.Name;
            }
            if (!Accepts(ha.MotionEntityId, false) || !Accepts(ha.PersonEntityId, true) || Accepts(uid.PersonEntityId, true))
                throw new Exception("HA event routing mixed camera sources or lost person detection.");
            ha.Active = false;
            if (Accepts(ha.MotionEntityId, false) || Accepts(ha.PersonEntityId, true)) throw new Exception("Disabled camera accepted HA events.");
            ha.Active = true;
        }
        using var form = new SettingsForm(new Settings { Cameras = [ha, uid, inactive], DirectHomeAssistantEnabled = true,
            MotionDetectionEnabled = true }, SettingsLocation.DefaultFolder);
        form.PrepareForDisplay();
        var grid = FindGrid(form);
        var tabs = (V2ConnectionTabs)form.Controls.Find("CameraConnectionTabs", true).Single();
        var editor = (ReolinkEditor)form.Controls.Find("ReolinkConnection", true).Single();
        grid.CurrentCell = grid.Rows[0].Cells[0]; tabs.RefreshCapabilities();
        if (grid.Rows[0].Cells["PersonEnabled"].ReadOnly || !grid.Rows[1].Cells["PersonEnabled"].ReadOnly || !grid.Rows[2].Cells["PersonEnabled"].ReadOnly)
            throw new Exception("Person capability UI does not follow per-camera source/active state.");
        FindCheckbox(editor).Checked = true;
        editor.Store();
        if (Convert.ToString(grid.Rows[0].Cells["HomeAssistantStreamUrl"].Value) != ha.StreamUrl || !grid.Rows[0].Cells["PersonEnabled"].ReadOnly)
            throw new Exception("Switch to UID lost HA address or enabled unsupported person detection.");
        editor.UseHomeAssistant();
        if (Convert.ToString(grid.Rows[0].Cells["StreamUrl"].Value) != ha.StreamUrl || grid.Rows[0].Cells["PersonEnabled"].ReadOnly)
            throw new Exception("Switch back to HA did not restore address/person capability.");
        grid.Rows[0].Cells["Active"].Value = false;
        if (!grid.Rows[0].Cells["PersonEnabled"].ReadOnly) throw new Exception("Inactive camera person control remains editable.");
        foreach (var column in new[] { "MotionEnabled", "PersonEnabled", "RecordingEnabled" })
            if (!grid.Rows[0].Cells[column].ReadOnly || !grid.Rows[2].Cells[column].ReadOnly)
                throw new Exception("Inactive camera options remain editable.");
        if (grid.Rows[0].Cells["Active"].ReadOnly || grid.Rows[1].Cells["MotionEnabled"].ReadOnly ||
            grid.Rows[1].Cells["RecordingEnabled"].ReadOnly)
            throw new Exception("Activation or supported UID options were disabled.");
        grid.Rows[0].Cells["Active"].Value = true;
        if (grid.Rows[0].Cells["MotionEnabled"].ReadOnly || grid.Rows[0].Cells["RecordingEnabled"].ReadOnly || grid.Rows[0].Cells["PersonEnabled"].ReadOnly)
            throw new Exception("Reactivation does not restore supported camera options.");
        grid.Rows[0].Cells["Active"].Value = false;
        var read = (List<CameraEntry>)typeof(SettingsForm).GetMethod("ReadCameras", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.Invoke(form, null)!;
        if (read[0].Active || !read[1].Active || !read[0].PersonEnabled.GetValueOrDefault() || read[0].HomeAssistantStreamUrl != ha.StreamUrl)
            throw new Exception("Settings save lost active state, inactive preferences or HA stream address.");
        using (var preview = new RenderHost { ShowInTaskbar = false, StartPosition = FormStartPosition.Manual,
            Location = new System.Drawing.Point(-30000, -30000), Size = new System.Drawing.Size(1000, 490), Opacity = 0 })
        {
            preview.Controls.Add(tabs); tabs.Show(); preview.Show();
            for (var tab = 0; tab < 3; tab++)
            {
                tabs.SelectedIndex = tab; Pump(150);
                var idleLayouts = 0;
                LayoutEventHandler watch = (_, _) => idleLayouts++;
                tabs.Layout += watch; Pump(1100); tabs.Layout -= watch;
                if (idleLayouts > 2) throw new Exception("Connection tabs continuously lay out while idle.");
                using var bitmap = new System.Drawing.Bitmap(preview.Width, preview.Height);
                preview.DrawToBitmap(bitmap, new System.Drawing.Rectangle(0, 0, bitmap.Width, bitmap.Height));
                bitmap.Save(Path.Combine(Environment.CurrentDirectory, tab == 0 ? "v2-tabs-reolink.png" : tab == 1 ? "v2-tabs-ha.png" : "v2-tabs-direct.png"));
            }
            preview.Hide();
        }
        Console.WriteLine("Camera activation, source switching, capability UI and migration passed.");
    }
    private static void SettingsPaint()
    {
        using var host = new RenderHost { ShowInTaskbar = false, Opacity = 0, StartPosition = FormStartPosition.Manual,
            Location = new System.Drawing.Point(-30000, -30000), Size = new System.Drawing.Size(1100, 900) };
        using var form = new SettingsForm(SettingsStore.CreateForNewInstallation(), SettingsLocation.DefaultFolder)
            { TopLevel = false, FormBorderStyle = FormBorderStyle.None };
        host.Controls.Add(form); host.Show(); form.Show(); Pump(500);
        var probes = new List<PaintProbe>();
        void Watch(Control control)
        {
            probes.Add(new PaintProbe(control));
            foreach (Control child in control.Controls) Watch(child);
        }
        Watch(form);
        var boundsChanges = 0;
        form.Layout += (_, _) => boundsChanges++;
        Pump(3000);
        var counts = probes.Where(p => p.Count != 0).OrderByDescending(p => p.Count).ToArray();
        Console.WriteLine("Visible settings layout passes: " + boundsChanges);
        foreach (var probe in counts.Take(12)) Console.WriteLine(probe.Description + ": " + probe.Count);
        if (counts.Any(p => p.Count > 12)) throw new Exception("Settings controls continuously repaint while idle.");
        using var bitmap = new System.Drawing.Bitmap(form.Width, form.Height);
        form.DrawToBitmap(bitmap, new System.Drawing.Rectangle(0, 0, bitmap.Width, bitmap.Height));
        bitmap.Save(Path.Combine(Environment.CurrentDirectory, "v2-dialog.png"));
        foreach (var probe in probes) probe.ReleaseHandle();
        form.Hide(); host.Hide();
    }
    private static void TestRecordingAndOrder()
    {
        var uid = new ReolinkCamera { Enabled = true, Uid = "FAKE0123456789AB", ProtectedPassword = CameraPassword.Protect("test") };
        var cameras = Enumerable.Range(0, 5).Select(i => new CameraEntry {
            Name = "Kamera " + i, StreamUrl = "rtsp://example.invalid/" + i,
            MotionEntityId = "binary_sensor.motion_" + i, PersonEntityId = "binary_sensor.person_" + i,
            MotionEnabled = true, PersonEnabled = true, MotionAction = i == 0 ? "Both" : "Snapshot",
            RecordingEnabled = i != 1, Reolink = i == 4 ? uid : new ReolinkCamera()
        }).ToList();
        using var host = new RenderHost { ShowInTaskbar = false, Opacity = 0, StartPosition = FormStartPosition.Manual,
            Location = new System.Drawing.Point(-30000, -30000), Size = new System.Drawing.Size(1100, 760) };
        using var form = new SettingsForm(new Settings { Cameras = cameras, DirectHomeAssistantEnabled = true, MotionRetentionDays = 0 }, SettingsLocation.DefaultFolder)
            { TopLevel = false, FormBorderStyle = FormBorderStyle.None, Dock = DockStyle.Fill };
        host.Controls.Add(form); form.PrepareForDisplay(); host.Show(); form.Show(); Pump(300);
        var grid = FindGrid(form);
        var connections = (ConnectionSettingsSection)form.Controls.Find("ConnectionSettingsSection", true).Single();
        var toggle = (Button)form.Controls.Find("ToggleConnectionSettings", true).Single();
        var connectionTabs = (V2ConnectionTabs)form.Controls.Find("CameraConnectionTabs", true).Single();
        var haButton = (Button)form.Controls.Find("HomeAssistantTabButton", true).Single();
        var uidButton = (Button)form.Controls.Find("ReolinkTabButton", true).Single();
        var general = form.Controls.Find("SettingsContent", true).Single().Controls.Cast<Control>()
            .Single(c => c.Text == UiText.T("Allgemeine Einstellungen"));
        if (connections.PointToScreen(System.Drawing.Point.Empty).Y >= general.PointToScreen(System.Drawing.Point.Empty).Y)
            throw new Exception("Connection settings are not directly below the camera list.");
        if (haButton.Text != "Home Assistant" || haButton.BackColor.GetBrightness() > .3 || haButton.ForeColor.GetBrightness() < .8)
            throw new Exception("Connection buttons did not return to their original dark style.");
        connectionTabs.SelectedIndex = 0;
        if (!toggle.Text.Contains("Home Assistant") || haButton.Font.Bold || !uidButton.Font.Bold)
            throw new Exception("Browsing another tab changes the source summary or fails to mark the opened page.");
        var beforeCollapse = connections.Height;
        if (connections.Expanded || connectionTabs.Visible || !toggle.Text.StartsWith("▶"))
            throw new Exception("Connection settings must be collapsed when the dialog opens.");
        toggle.PerformClick(); Pump(50);
        beforeCollapse = connections.Height;
        for (var repeat = 0; repeat < 3; repeat++)
        {
            toggle.PerformClick(); Pump(50);
            if (connections.Expanded || connectionTabs.Visible || connections.Height >= beforeCollapse || !toggle.Text.Contains("Home Assistant"))
                throw new Exception("Connection settings do not collapse or lose the source summary.");
            grid.CurrentCell = grid.Rows[4].Cells[0]; Pump(50);
            if (connections.Expanded || !toggle.Text.Contains("Reolink-UID"))
                throw new Exception("Collapsed connection section does not follow camera selection.");
            toggle.PerformClick(); Pump(50);
            if (!connections.Expanded || !connectionTabs.Visible || uidButton.Text != "Reolink" || uidButton.BackColor.GetBrightness() > .3 || !toggle.Text.Contains("Reolink-UID"))
                throw new Exception("Connection settings do not reopen with dark buttons and the correct source summary.");
            grid.CurrentCell = grid.Rows[0].Cells[0];
        }
        using (var image = new System.Drawing.Bitmap(grid.Width, grid.Height))
        {
            grid.DrawToBitmap(image, new System.Drawing.Rectangle(0, 0, image.Width, image.Height));
            var bounds = grid.GetCellDisplayRectangle(grid.Columns["PersonEnabled"].Index, 4, false);
            var greyPixels = 0;
            for (var y = bounds.Top; y < bounds.Bottom; y++)
                for (var x = bounds.Left; x < bounds.Right; x++)
                    if (image.GetPixel(x, y).ToArgb() == System.Drawing.Color.FromArgb(55, 55, 59).ToArgb()) greyPixels++;
            if (greyPixels < 20) throw new Exception("Unsupported checkbox is not visibly painted grey.");
        }
        if (grid.Columns["Active"].DisplayIndex != 0 ||
            grid.Columns["RecordingEnabled"].DisplayIndex <= grid.Columns["PersonEnabled"].DisplayIndex)
            throw new Exception("Active must be first; recordings must follow the other checkboxes.");
        var section = form.Controls.Find("MotionCaptureOptions", true).Single();
        grid.CurrentCell = grid.Rows[0].Cells[0]; Pump(100);
        if (!section.Visible) throw new Exception("Recording options hidden for enabled camera.");
        grid.CurrentCell = grid.Rows[1].Cells[0]; Pump(100);
        if (section.Visible) throw new Exception("Recording options shown for disabled camera.");
        grid.Rows[1].Cells["RecordingEnabled"].Value = true; Pump(100);
        if (!section.Visible) throw new Exception("Recording checkbox does not show options immediately.");
        grid.Rows[1].Cells["RecordingEnabled"].Value = false;
        grid.CurrentCell = grid.Rows[4].Cells[0];
        var up = (Button)form.Controls.Find("MoveCameraUp", true).Single();
        var down = (Button)form.Controls.Find("MoveCameraDown", true).Single();
        var reorderScroll = (ModernSettingsScrollArea)form.Controls.Find("SettingsScrollArea", true).Single();
        var tabs = (V2ConnectionTabs)form.Controls.Find("CameraConnectionTabs", true).Single();
        // Keep a manually chosen page while crossing cameras with different
        // sources and recording settings. Intermediate row removal is invisible.
        tabs.SelectedIndex = 1;
        up.Focus();
        reorderScroll.AutoScrollPosition = new System.Drawing.Point(0, 40); Pump(100);
        var offset = reorderScroll.VerticalOffset;
        var gridBounds = grid.Bounds;
        var sectionChanges = 0; var gridChanges = 0; var pageChanges = 0;
        section.VisibleChanged += (_, _) => sectionChanges++;
        grid.SizeChanged += (_, _) => gridChanges++;
        foreach (var name in new[] { "ReolinkTab", "HomeAssistantTab", "DirectCameraTab" })
            form.Controls.Find(name, true).Single().VisibleChanged += (_, _) => pageChanges++;
        var selectedRow = grid.CurrentRow;
        foreach (var button in new[] { up, up, up, up, down, down, down, down, up, up })
        {
            button.PerformClick(); Pump(50);
            if (reorderScroll.VerticalOffset != offset || grid.Bounds != gridBounds ||
                tabs.SelectedIndex != 1 || grid.CurrentRow != selectedRow || sectionChanges != 0 || gridChanges != 0 || pageChanges != 0)
                throw new Exception("Camera reorder moved the view, resized the table, or switched detail sections.");
        }
        var read = (List<CameraEntry>)typeof(SettingsForm).GetMethod("ReadCameras", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.Invoke(form, null)!;
        if (read[2].Name != "Kamera 4" || read[2].Reolink.Id != uid.Id || read[2].Reolink.ProtectedPassword != uid.ProtectedPassword ||
            read[0].MotionAction != "Both" || read[2].PersonEntityId != "binary_sensor.person_4" || read[1].RecordingEnabled)
            throw new Exception("Camera reorder changed camera settings or credentials.");
        var restored = SettingsBackup.Parse(SettingsBackup.Serialize(new Settings { Cameras = read }));
        if (restored.Cameras[1].CanRecord || !restored.Cameras[2].CanRecord) throw new Exception("Recording toggle lost in backup.");
        var probes = new List<PaintProbe>();
        void Watch(Control control) { probes.Add(new PaintProbe(control)); foreach (Control child in control.Controls) Watch(child); }
        Watch(form);
        var scroll = (ModernSettingsScrollArea)form.Controls.Find("SettingsScrollArea", true).Single();
        if (!scroll.Contains(grid)) throw new Exception("Camera grid must share the common scrolling surface.");
        var gridPosition = grid.PointToScreen(System.Drawing.Point.Empty);
        var gridY = gridPosition.Y + scroll.VerticalOffset;
        var widths = grid.Columns.Cast<DataGridViewColumn>().Select(c => c.Width).ToArray();
        for (var i = 0; i < 6; i++)
        {
            scroll.AutoScrollPosition = new System.Drawing.Point(0, i % 2 == 0 ? 450 : 0); Pump(100);
            if (grid.PointToScreen(System.Drawing.Point.Empty).Y + scroll.VerticalOffset != gridY || grid.PointToScreen(System.Drawing.Point.Empty).X != gridPosition.X ||
                !widths.SequenceEqual(grid.Columns.Cast<DataGridViewColumn>().Select(c => c.Width)))
                throw new Exception("Camera grid changes geometry independently of the common scroll offset.");
        }
        host.ClientSize = new System.Drawing.Size(900, 680); Pump(200);
        var bar = (DarkSettingsScrollBar)form.Controls.Find("ModernVerticalScrollBar", true).Single();
        scroll.AutoScrollPosition = System.Drawing.Point.Empty;
        void Mouse(string method, int y) => typeof(DarkSettingsScrollBar).GetMethod(method,
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.Invoke(bar,
            [new MouseEventArgs(MouseButtons.Left, 1, bar.Width / 2, y, 0)]);
        Mouse("OnMouseDown", 8); Mouse("OnMouseMove", bar.Height - 8); Mouse("OnMouseUp", bar.Height - 8);
        if (scroll.VerticalOffset <= 0 || bar.Capture) throw new Exception("Dark scrollbar dragging failed.");
        typeof(DarkSettingsScrollBar).GetMethod("OnKeyDown", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .Invoke(bar, [new KeyEventArgs(Keys.Home)]);
        if (scroll.VerticalOffset != 0) throw new Exception("Dark scrollbar keyboard navigation failed.");
        using (var barImage = new System.Drawing.Bitmap(bar.Width, bar.Height))
        {
            bar.DrawToBitmap(barImage, new System.Drawing.Rectangle(0, 0, bar.Width, bar.Height));
            var track = barImage.GetPixel(0, bar.Height / 2);
            if (track.R > 20 || track.G > 20 || track.B > 20) throw new Exception("Scrollbar track is not black.");
        }
        foreach (var probe in probes) probe.Count = 0;
        Pump(2500);
        if (probes.Any(p => p.Count > 12)) throw new Exception("Five-camera settings keep repainting after scroll/resize.");
        Console.WriteLine("Five-camera recording visibility, reorder/credentials, backup and scroll/resize idle repaint passed.");
        scroll.AutoScrollPosition = System.Drawing.Point.Empty; Pump(100);
        using var bitmap = new System.Drawing.Bitmap(form.Width, form.Height);
        form.DrawToBitmap(bitmap, new System.Drawing.Rectangle(0, 0, bitmap.Width, bitmap.Height));
        bitmap.Save(Path.Combine(Environment.CurrentDirectory, "v2-settings-beta11.png"));
        foreach (var probe in probes) probe.ReleaseHandle();
        var delete = (Button)form.Controls.Find("DeleteCamera", true).Single();
        List<CameraEntry> ReadRows() => (List<CameraEntry>)typeof(SettingsForm).GetMethod("ReadCameras",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.Invoke(form, null)!;
        // Visiting a HA camera initializes its retained HA-address cache.
        foreach (DataGridViewRow row in grid.Rows)
            if (!row.IsNewRow) grid.CurrentCell = row.Cells[0];
        // Remove a middle UID row, then the first row and finally all remaining
        // cameras. Survivor settings must never inherit the deleted editor values.
        foreach (var index in new[] { 2, 0, 2, 1, 0 })
        {
            grid.CurrentCell = grid.Rows[index].Cells[0]; Pump(50);
            var expected = ReadRows(); expected.RemoveAt(index);
            if (!delete.Enabled) throw new Exception("Delete button disabled for a camera.");
            delete.PerformClick(); Pump(50);
            var actual = ReadRows();
            if (JsonSerializer.Serialize(actual) != JsonSerializer.Serialize(expected))
            {
                var differences = new List<string>();
                for (var i = 0; i < Math.Min(actual.Count, expected.Count); i++)
                {
                    var a = JsonSerializer.SerializeToElement(actual[i]); var b = JsonSerializer.SerializeToElement(expected[i]);
                    foreach (var property in a.EnumerateObject())
                        if (property.Value.GetRawText() != b.GetProperty(property.Name).GetRawText()) differences.Add(i + ":" + property.Name);
                }
                throw new Exception("Deleting camera " + index + " changed survivor fields: " + string.Join(", ", differences));
            }
        }
        if (grid.Rows.Count != 1 || !grid.Rows[0].IsNewRow || delete.Enabled || section.Visible || cameras.Count != 5)
            throw new Exception("Deleting the final camera did not clear selection or mutated original settings before Save.");
        delete.PerformClick();
        Console.WriteLine("Camera deletion: middle/first/last, survivor credentials, empty list and unsaved isolation passed.");
        form.Hide(); host.Hide();
    }
    private static void TestSettingsPresentation()
    {
        for (var attempt = 0; attempt < 2; attempt++)
        {
            var settings = new Settings { MotionRetentionDays = 0, DirectHomeAssistantEnabled = true,
                Cameras = Enumerable.Range(0, 5).Select(i => new CameraEntry { Name = "Test " + i,
                    StreamUrl = "rtsp://example.invalid/" + i, MotionEnabled = i == 0, LocalMotion = new LocalMotionSettings { Enabled = i == 0 } }).ToList() };
            using var form = new SettingsForm(settings, SettingsLocation.DefaultFolder) {
                ShowInTaskbar = false, StartPosition = FormStartPosition.Manual,
                Location = new System.Drawing.Point(-30000, -30000) };
            if (attempt == 1) form.PrepareForDisplay();
            Exception? failure = null;
            var paintedWhileHidden = false;
            var scroll = (ModernSettingsScrollArea)form.Controls.Find("SettingsScrollArea", true).Single();
            form.Controls.Find("CameraHint", true).Single().Paint += (_, _) =>
            {
                if (SettingsPresentation.GetLayeredWindowAttributes(form.Handle, out _, out var alpha, out _) && alpha == 0)
                    paintedWhileHidden = true;
            };
            form.Shown += (_, _) =>
            {
                // The production Shown handler clamps to a monitor. Move back
                // off screen before the first opaque presentation to avoid
                // interrupting the user's desktop during the test.
                form.Location = new System.Drawing.Point(-30000, -30000);
                if (!SettingsPresentation.GetLayeredWindowAttributes(form.Handle, out _, out var alpha, out _) || alpha != 0)
                    failure = new Exception("Settings became visible before their first complete paint.");
                form.BeginInvoke(new Action(() =>
                {
                    try
                    {
                        var layered = SettingsPresentation.GetLayeredWindowAttributes(form.Handle, out _, out var finalAlpha, out _);
                        if (!paintedWhileHidden || !layered || finalAlpha != 255)
                            throw new Exception($"Settings were not painted before becoming fully opaque (paint={paintedWhileHidden}, layered={layered}, alpha={finalAlpha}).");
                        using var probe = new PaintProbeScope(scroll);
                        var grid = FindGrid(form);
                        using var gridProbe = new PaintProbeScope(grid);
                        var gridPaints = gridProbe.Count;
                        SendMessage(grid.Handle, 0x020A, new IntPtr(unchecked(-120 << 16)), IntPtr.Zero);
                        if (scroll.AutoScrollPosition.Y >= 0) throw new Exception("Mouse wheel did not scroll settings details.");
                        if (!IsWindowVisible(scroll.Handle)) throw new Exception("Scroll drawing transaction did not restore the visible panel.");
                        var field = form.Controls.Find("HomeAssistantCameraStream", true).Single();
                        var connections = (ConnectionSettingsSection)form.Controls.Find("ConnectionSettingsSection", true).Single();
                        if (connections.Expanded || field.Visible)
                            throw new Exception("Native settings opening must keep connections collapsed.");
                        connections.Expanded = true; Pump(70);
                        var motionDetails = (MotionDetailsSection)form.Controls.Find("MotionDetailsSection", true).Single();
                        motionDetails.Expanded = true; Pump(70);
                        var viewport = form.Controls.Find("SettingsViewport", true).Single();
                        var viewportMessages = new PaintProbe(viewport);
                        try
                        {
                            IEnumerable<Control> Fields(Control root)
                            {
                                foreach (Control child in root.Controls)
                                {
                                    if (child.Visible && child is TextBoxBase or ComboBox or UpDownBase or ButtonBase) yield return child;
                                    foreach (var nested in Fields(child)) yield return nested;
                                }
                            }
                            var fields = Fields(scroll).Distinct().ToDictionary(c => c, c => (X: c.PointToScreen(System.Drawing.Point.Empty).X, Y: c.PointToScreen(System.Drawing.Point.Empty).Y + scroll.VerticalOffset, Size: c.Size));
                            var timer = System.Diagnostics.Stopwatch.StartNew();
                            for (var step = 0; step < 24; step++)
                            {
                                scroll.AutoScrollPosition = new System.Drawing.Point(0, (step * 137) % 900); Pump(10);
                                if (!IsWindowVisible(viewport.Handle)) throw new Exception("Viewport disappears while scrolling.");
                                foreach (var pair in fields)
                                {
                                    var point = pair.Key.PointToScreen(System.Drawing.Point.Empty);
                                    if (point.X != pair.Value.X || point.Y + scroll.VerticalOffset != pair.Value.Y || pair.Key.Size != pair.Value.Size)
                                        throw new Exception("A settings field wobbles during rapid scroll with expanded local analysis.");
                                }
                            }
                            if (viewportMessages.RedrawChanges != 0) throw new Exception("Scrolling toggles viewport redraw/visibility.");
                            if (timer.ElapsedMilliseconds > 8000) throw new Exception("Rapid scrolling blocks the settings UI.");
                        }
                        finally { viewportMessages.ReleaseHandle(); }
                        scroll.AutoScrollPosition = System.Drawing.Point.Empty;
                        var fieldY = field.PointToScreen(System.Drawing.Point.Empty).Y - scroll.AutoScrollPosition.Y;
                        var fieldX = field.PointToScreen(System.Drawing.Point.Empty).X;
                        var fieldWidth = field.Width;
                        for (var i = 0; i < 6; i++)
                        {
                            scroll.AutoScrollPosition = new System.Drawing.Point(0, i % 2 == 0 ? 500 : 0);
                            Pump(70);
                            var point = field.PointToScreen(System.Drawing.Point.Empty);
                            if (point.Y - scroll.AutoScrollPosition.Y != fieldY || point.X != fieldX || field.Width != fieldWidth)
                                throw new Exception("Settings fields move or resize independently of the scroll offset.");
                        }

                        scroll.AutoScrollPosition = new System.Drawing.Point(0, 400);
                        var retainedPosition = scroll.VerticalOffset;
                        form.PerformLayout(); Pump(100);
                        if (scroll.VerticalOffset != retainedPosition) throw new Exception("Layout pulls the common scroll position back.");
                        var before = probe.Count;
                        Pump(1200);
                        if (probe.Count - before > 6) throw new Exception("Opaque settings surface continually repaints while idle.");
                    }
                    catch (Exception error) { failure = error; }
                    finally { form.DialogResult = DialogResult.OK; form.Close(); }
                }));
            };
            form.ShowDialog();
            if (failure is not null) throw failure;
        }
        Console.WriteLine("Native modal settings: prepared/unprepared opening paints before opaque display; scroll and idle repaint passed.");
    }
    private sealed class PaintProbeScope : IDisposable
    {
        private readonly PaintProbe probe;
        internal int Count => probe.Count;
        internal PaintProbeScope(Control control) { probe = new PaintProbe(control); }
        public void Dispose() => probe.ReleaseHandle();
    }
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr window);
    private sealed class PaintProbe : NativeWindow
    {
        internal int Count;
        internal int RedrawChanges;
        internal string Description;
        internal PaintProbe(Control control)
        {
            Description = control.GetType().Name + "/" + control.Name;
            AssignHandle(control.Handle);
        }
        protected override void WndProc(ref Message message)
        {
            if (message.Msg is 0x000F or 0x0014) Count++;
            if (message.Msg == 0x000B) RedrawChanges++;
            base.WndProc(ref message);
        }
    }
    private static void Pump(int milliseconds)
    {
        var watch = System.Diagnostics.Stopwatch.StartNew();
        while (watch.ElapsedMilliseconds < milliseconds) { Application.DoEvents(); Thread.Sleep(10); }
    }
    private static CheckBox FindCheckbox(Control root)
    {
        foreach (Control control in root.Controls)
        {
            if (control is CheckBox check) return check;
            if (control.HasChildren) { try { return FindCheckbox(control); } catch (InvalidOperationException) { } }
        }
        throw new InvalidOperationException("UID checkbox missing.");
    }
    private static DataGridView FindGrid(Control root)
    {
        foreach (Control control in root.Controls)
        {
            if (control is DataGridView grid) return grid;
            if (control.HasChildren) { try { return FindGrid(control); } catch (InvalidOperationException) { } }
        }
        throw new InvalidOperationException("Camera grid not found.");
    }
    private static async Task TestBridge(CameraEntry camera, bool isolateFailure = false)
    {
        var root = Path.Combine(Environment.CurrentDirectory, "gateway-test-" + Guid.NewGuid().ToString("N"));
        var failed = new CameraEntry { Name = "Failure isolation", Reolink = new ReolinkCamera {
            Enabled = true, Uid = "FAKEFAIL012345AB", ProtectedPassword = camera.Reolink.ProtectedPassword } };
        using var bridge = new ReolinkBridge(isolateFailure ? [camera, failed] : [camera], Environment.ProcessPath!, root);
        var motion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var streamReady = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var authenticationFailure = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        if (bridge.IsStreamReady(camera)) throw new Exception("New bridge cannot have a ready stream.");
        bridge.StreamReady += _ => streamReady.TrySetResult();
        var statuses = new List<string>();
        bridge.Motion += _ => motion.TrySetResult();
        bridge.Status += (entry, status) =>
        {
            lock (statuses) statuses.Add(status);
            if (entry.Reolink.Id == failed.Reolink.Id && status.Contains("[AUTH]", StringComparison.Ordinal)) authenticationFailure.TrySetResult();
        };
        bridge.Start();
        try
        {
            await motion.Task.WaitAsync(TimeSpan.FromSeconds(20));
            await streamReady.Task.WaitAsync(TimeSpan.FromSeconds(5));
            if (!bridge.IsStreamReady(camera)) throw new Exception("Real stream publication was not detected.");
            if (isolateFailure)
            {
                await authenticationFailure.Task.WaitAsync(TimeSpan.FromSeconds(5));
                if (bridge.IsStreamReady(failed) || bridge.StreamUrl(camera) == bridge.StreamUrl(failed) || camera.StreamUrl != bridge.StreamUrl(camera))
                    throw new Exception("Camera failure/RTSP endpoint isolation failed.");
            }
            lock (statuses) if (!statuses.Contains(UiText.T("Verbunden"))) throw new Exception("Camera status did not reach connected.");
        }
        finally
        {
            bridge.Dispose();
            await bridge.Completion.WaitAsync(TimeSpan.FromSeconds(10));
            if (Directory.Exists(root) && Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories).Any())
                throw new Exception("Runtime credentials were not removed on stop.");
            if (Directory.Exists(root)) Directory.Delete(root);
        }
    }
    private static async Task FakeHelper(string path)
    {
        var text = File.ReadAllText(path);
        if (text.Contains("FAKEFAIL012345AB", StringComparison.Ordinal))
        {
            Console.Error.WriteLine("CameraLoginFail");
            return;
        }
        var section = text.Split("[mqtt]", 2)[1];
        var port = int.Parse(Regex.Match(section, @"port = (\d+)").Groups[1].Value);
        var pass = JsonSerializer.Deserialize<string>(Regex.Match(section, "credentials = \\[\"homecam-v2\", (.+)\\]").Groups[1].Value)!;
        var name = JsonSerializer.Deserialize<string>(Regex.Match(text, "name = (.+)").Groups[1].Value)!;
        using var rejected = new MqttFactory().CreateMqttClient();
        try
        {
            await rejected.ConnectAsync(new MqttClientOptionsBuilder().WithTcpServer("127.0.0.1", port)
                .WithCredentials("homecam-v2", "wrong-password").Build());
            throw new Exception("Local event gateway accepted incorrect credentials.");
        }
        catch (MQTTnet.Adapter.MqttConnectingFailedException) { }
        using var client = new MqttFactory().CreateMqttClient();
        await client.ConnectAsync(new MqttClientOptionsBuilder().WithTcpServer("127.0.0.1", port)
            .WithCredentials("homecam-v2", pass).Build());
        Console.WriteLine($"INFO fake: Avaliable at /{name}/subStream");
        foreach (var (suffix, payload) in new[] { ("status", "connected"), ("status/motion", "off"), ("status/motion", "on") })
            await client.PublishAsync(new MqttApplicationMessageBuilder().WithTopic($"neolink/{name}/{suffix}")
                .WithPayload(payload).WithRetainFlag().WithQualityOfServiceLevel(MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce).Build());
        await Task.Delay(TimeSpan.FromMinutes(1));
    }
    private static async Task DiagnoseSaved(string settingsPath, string gst, string? helperOverride)
    {
        var settings = JsonSerializer.Deserialize<Settings>(File.ReadAllText(settingsPath))!;
        var cameraIndex = int.TryParse(Environment.GetEnvironmentVariable("HOMECAM_DIAG_CAMERA"), out var requested) ? requested : 0;
        var camera = settings.Cameras.Where(c => c.Reolink.Enabled).ElementAt(cameraIndex);
        if (Environment.GetEnvironmentVariable("HOMECAM_DIAG_MAIN") == "1") camera.Reolink.Stream = "mainStream";
        Environment.SetEnvironmentVariable("PATH", Path.Combine(gst, "bin") + ";" + Environment.GetEnvironmentVariable("PATH"));
        Environment.SetEnvironmentVariable("GST_PLUGIN_PATH_1_0", Path.Combine(gst, "lib", "gstreamer-1.0"));
        Environment.SetEnvironmentVariable("GST_PLUGIN_SCANNER_1_0", Path.Combine(gst, "libexec", "gstreamer-1.0", "gst-plugin-scanner.exe"));
        Environment.SetEnvironmentVariable("GST_REGISTRY_1_0", Path.Combine(Environment.CurrentDirectory, "helper-smoke-registry.bin"));
        using var bridge = new ReolinkBridge([camera], helperOverride ?? settings.ReolinkHelperPath,
            Path.Combine(Environment.CurrentDirectory, "gateway-test-diagnose"),
            Environment.GetEnvironmentVariable("HOMECAM_DIAG_DISCOVERY") ?? "cellular");
        var streamReady = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        bridge.StreamReady += _ => streamReady.TrySetResult();
        var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var connected = false;
        string? last = null;
        var elapsed = System.Diagnostics.Stopwatch.StartNew();
        bridge.Status += (_, text) =>
        {
            if (text == UiText.T("Verbunden")) connected = true;
            if (last != text) { Console.WriteLine($"{elapsed.Elapsed.TotalSeconds:F1}s: {text}"); last = text; }
            if (text == UiText.T("Verbunden") || ReolinkDiagnostics.IsFailure(text)) done.TrySetResult();
        };
        bridge.Start();
        try
        {
            await done.Task.WaitAsync(TimeSpan.FromSeconds(125));
            if (connected)
            {
                try { await streamReady.Task.WaitAsync(TimeSpan.FromSeconds(45)); }
                catch (TimeoutException) { Console.WriteLine("Video endpoint not ready after 45 seconds."); return; }
                Console.WriteLine($"Video endpoint ready at {elapsed.Elapsed.TotalSeconds:F1}s");
                if (Environment.GetEnvironmentVariable("HOMECAM_DIAG_MPV_TERMINAL") == "1")
                {
                    var reverse = Environment.GetEnvironmentVariable("HOMECAM_DIAG_REVERSE") == "1";
                    await ProbeMpvTerminal(camera, !reverse);
                    await ProbeMpvTerminal(camera, reverse);
                    return;
                }
                using (var socket = new System.Net.Sockets.TcpClient())
                {
                    await socket.ConnectAsync(System.Net.IPAddress.Loopback, bridge.RtspPort);
                    var request = System.Text.Encoding.ASCII.GetBytes($"DESCRIBE {camera.StreamUrl} RTSP/1.0\r\nCSeq: 1\r\nAccept: application/sdp\r\n\r\n");
                    await socket.GetStream().WriteAsync(request);
                    using var reader = new StreamReader(socket.GetStream());
                    var first = await reader.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(15));
                    Console.WriteLine("Direct protocol reply: " + (first is not null && System.Text.RegularExpressions.Regex.IsMatch(first,@"^RTSP/1\.0 \d{3} [A-Za-z ]+$") ? first : "Unexpected protocol response"));
                }
                if (Environment.GetEnvironmentVariable("HOMECAM_DIAG_MPV") == "1")
                {
                    await ProbeMpv(camera);
                    return;
                }
                var info = new System.Diagnostics.ProcessStartInfo(Path.Combine(Environment.CurrentDirectory, "publish-v2-beta", "ffmpeg.exe"))
                    { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
                foreach (var key in new[] { "http_proxy", "https_proxy", "all_proxy", "HTTP_PROXY", "HTTPS_PROXY", "ALL_PROXY" }) info.Environment.Remove(key);
                var seconds = int.TryParse(Environment.GetEnvironmentVariable("HOMECAM_DIAG_STABILITY_SECONDS"), out var duration) ? Math.Clamp(duration, 1, 300) : 0;
                foreach (var arg in new[] { "-v", "error", "-rtsp_transport", "tcp", "-i", camera.StreamUrl, "-an", seconds > 0 ? "-t" : "-frames:v", seconds > 0 ? seconds.ToString() : "1", "-progress", "pipe:1", "-nostats", "-f", "null", "-" }) info.ArgumentList.Add(arg);
                using var video = System.Diagnostics.Process.Start(info)!;
                var errors = video.StandardError.ReadToEndAsync();
                var output = ReadVideoProgress(video.StandardOutput);
                try { await video.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(seconds + 45)); }
                catch (TimeoutException) { video.Kill(true); await video.WaitForExitAsync(); Console.WriteLine("Video decode timeout."); }
                var diagnostic = await errors; var progress = await output;
                Console.WriteLine("Video decode exit code: " + video.ExitCode);
                Console.WriteLine($"Decoded frames: {progress.Frames}; video seconds: {progress.Seconds:F1}; longest progress gap: {progress.Gap:F1}s; first decoded progress: {progress.First:F1}s");
                Console.WriteLine($"Total diagnostic elapsed: {elapsed.Elapsed.TotalSeconds:F1}s");
                Console.WriteLine("Invalid-data error count: " + Regex.Matches(diagnostic, "Invalid data", RegexOptions.IgnoreCase).Count);
                foreach (var marker in new[] { "Invalid NAL", "non-existing PPS", "decode_slice_header", "missing picture", "RTP: missed", "max delay reached", "corrupt decoded frame", "reference picture missing", "no frame", "Invalid level prefix", "error while decoding" })
                    Console.WriteLine($"Decoder category {marker}: {Regex.Matches(diagnostic, Regex.Escape(marker), RegexOptions.IgnoreCase).Count}");
                foreach (var marker in new[] { "400 Bad Request", "404 Not Found", "401 Unauthorized", "503 Service Unavailable", "Connection refused", "Invalid data", "timed out", "Server returned 5", "method DESCRIBE failed" })
                    if (diagnostic.Contains(marker, StringComparison.OrdinalIgnoreCase)) Console.WriteLine("Video category: " + marker);
            }
        }
        catch (TimeoutException) { Console.WriteLine("Diagnostic time limit reached."); }
        finally { bridge.Dispose(); await bridge.Completion; }
    }
    private static async Task ProbeMpv(CameraEntry camera)
    {
        var pipe = "HomeCam-Mpv-Probe-" + Guid.NewGuid().ToString("N");
        var info = new System.Diagnostics.ProcessStartInfo(Path.Combine(Environment.CurrentDirectory, "publish-v2-beta", "mpv.exe"))
            { UseShellExecute = false, CreateNoWindow = true, WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden };
        foreach (var arg in new[] { "--vo=null", "--no-terminal", "--really-quiet", "--no-audio", "--profile=low-latency", "--cache=no", "--demuxer-lavf-o=rtsp_transport=tcp", @"--input-ipc-server=\\.\pipe\" + pipe, camera.StreamUrl }) info.ArgumentList.Add(arg);
        using var player = System.Diagnostics.Process.Start(info)!;
        var method = typeof(MonitorForm).GetMethod("WaitForFirstFrameAsync", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
        var clock = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            var first = await (Task<bool>)method.Invoke(null, [player, pipe, CancellationToken.None, false, 30])!;
            Console.WriteLine($"MPV first moving frame: {first}; elapsed {clock.Elapsed.TotalSeconds:F1}s");
            if (!first) return;
            using var duration = new CancellationTokenSource(TimeSpan.FromSeconds(180));
            try
            {
                var healthy = await (Task<bool>)method.Invoke(null, [player, pipe, duration.Token, true, 12])!;
                Console.WriteLine("MPV playback watchdog ended early: " + healthy);
            }
            catch (OperationCanceledException) when (duration.IsCancellationRequested)
            { Console.WriteLine("MPV remained active for 180 seconds with the actual playback watchdog."); }
        }
        catch (UnauthorizedAccessException)
        { Console.WriteLine("MPV IPC is unavailable in this environment; no player-watchdog verdict."); }
        finally { if (!player.HasExited) player.Kill(true); await player.WaitForExitAsync(); }
    }
    private static async Task ProbeMpvTerminal(CameraEntry camera, bool lowLatency)
    {
        var info = new System.Diagnostics.ProcessStartInfo(Path.Combine(Environment.CurrentDirectory, "publish-v2-beta", "mpv.exe"))
            { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var arg in new[] { "--no-config", "--vo=null", "--no-audio", "--input-terminal=no", "--terminal=yes", "--term-osd=force", "--term-status-msg=HCM_PROGRESS=${=time-pos}", "--msg-level=all=error,statusline=status", "--demuxer-lavf-o=rtsp_transport=tcp", "--length=45" }) info.ArgumentList.Add(arg);
        if (lowLatency) { info.ArgumentList.Add("--profile=low-latency"); info.ArgumentList.Add("--cache=no"); }
        else {
            info.ArgumentList.Add("--cache=yes"); info.ArgumentList.Add("--demuxer-max-bytes=16MiB");
            if (Environment.GetEnvironmentVariable("HOMECAM_DIAG_FASTPROBE") == "1") {
                info.ArgumentList.Add("--demuxer-lavf-probe-info=nostreams");
                info.ArgumentList.Add("--demuxer-lavf-analyzeduration=0.1");
            }
        }
        info.ArgumentList.Add(camera.StreamUrl);
        using var process = System.Diagnostics.Process.Start(info)!;
        var watch = System.Diagnostics.Stopwatch.StartNew();
        double first = -1, last = -1, previous = -1, gap = 0;
        int updates = 0;
        var sync = new object();
        async Task Read(StreamReader reader)
        {
            var line = new System.Text.StringBuilder();
            var chars = new char[1024]; int count;
            while ((count = await reader.ReadAsync(chars.AsMemory())) > 0)
                for (var i = 0; i < count; i++)
                    if (chars[i] is '\r' or '\n')
                    {
                        var match = Regex.Match(line.ToString(), @"HCM_PROGRESS=(\d+(?:\.\d+)?)"); line.Clear();
                        if (match.Success && double.TryParse(match.Groups[1].Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var position))
                            lock (sync)
                                if (previous < 0 || Math.Abs(position - previous) > .001)
                                {
                                    var now = watch.Elapsed.TotalSeconds;
                                    if (first < 0) { first = now; Console.WriteLine($"MPV {(lowLatency ? "low-latency" : "buffered")} first progress at {now:F1}s"); }
                                    if (last >= 0) gap = Math.Max(gap, now - last);
                                    last = now; previous = position; updates++;
                                }
                    }
                    else if (line.Length < 16384) line.Append(chars[i]);
        }
        var output = Read(process.StandardOutput); var errors = Read(process.StandardError);
        try { await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(85)); }
        catch (TimeoutException) { process.Kill(true); await process.WaitForExitAsync(); }
        await Task.WhenAll(output, errors);
        Console.WriteLine($"MPV {(lowLatency ? "low-latency" : "buffered")}: exit {process.ExitCode}; updates {updates}; first {first:F1}s; gap {gap:F1}s; elapsed {watch.Elapsed.TotalSeconds:F1}s");
    }
    private static async Task<(long Frames, double Seconds, double Gap, double First)> ReadVideoProgress(StreamReader reader)
    {
        var clock = System.Diagnostics.Stopwatch.StartNew();
        long frames = 0, micros = 0;
        double first = -1, previous = -1, gap = 0, reported = 0;
        string? line;
        while ((line = await reader.ReadLineAsync()) is not null)
        {
            if (line.StartsWith("frame=", StringComparison.Ordinal) && long.TryParse(line.AsSpan(6), out var next) && next > frames)
            {
                var now = clock.Elapsed.TotalSeconds;
                if (first < 0) first = now;
                if (previous >= 0) gap = Math.Max(gap, now - previous);
                previous = now; frames = next;
                if (now - reported >= 15)
                {
                    Console.WriteLine($"Video progress at {now:F1}s: {frames} frames; max gap {gap:F1}s");
                    reported = now;
                }
            }
            if (line.StartsWith("out_time_us=", StringComparison.Ordinal) && long.TryParse(line.AsSpan(12), out var time)) micros = time;
        }
        return (frames, micros / 1000000d, gap, first);
    }
    private static async Task HelperSmoke(string executable, string gst)
    {
        var rtspPort = ReolinkBridge.FreePort();
        var mqttPort = ReolinkBridge.FreePort();
        while (rtspPort == mqttPort) mqttPort = ReolinkBridge.FreePort();
        using var server = new MqttFactory().CreateMqttServer(new MQTTnet.Server.MqttServerOptionsBuilder()
            .WithDefaultEndpoint().WithDefaultEndpointBoundIPAddress(System.Net.IPAddress.Loopback)
            .WithDefaultEndpointBoundIPV6Address(System.Net.IPAddress.IPv6Loopback).WithDefaultEndpointPort(mqttPort).Build());
        await server.StartAsync();
        var path = Path.Combine(Environment.CurrentDirectory, "helper-smoke.toml");
        var camera = new CameraEntry { Name = "Schema test only", Reolink = new ReolinkCamera { Enabled = true,
            Uid = "FAKE0123456789AB", ProtectedPassword = CameraPassword.Protect("quotes\"\\ newline\nGrüße 💡 \u007f") } };
        // Disabled fake camera verifies TOML parsing without contacting UID servers.
        File.WriteAllText(path, ReolinkConfig.Build([camera], rtspPort, mqttPort, "smoke-test-only")
            .Replace("[[cameras]]", "[[cameras]]\nenabled = false"));
        var info = ReolinkBridge.StartInfo(Path.GetFullPath(executable), path);
        info.Environment["PATH"] = Path.Combine(Path.GetFullPath(gst), "bin") + ";" + Environment.GetEnvironmentVariable("PATH");
        info.Environment["GST_PLUGIN_PATH_1_0"] = Path.Combine(Path.GetFullPath(gst), "lib", "gstreamer-1.0");
        info.Environment["GST_PLUGIN_SCANNER_1_0"] = Path.Combine(Path.GetFullPath(gst), "libexec", "gstreamer-1.0", "gst-plugin-scanner.exe");
        info.Environment["GST_REGISTRY_1_0"] = Path.Combine(Environment.CurrentDirectory, "helper-smoke-registry.bin");
        using var process = System.Diagnostics.Process.Start(info)!;
        using var lifetime = new ReolinkProcessLifetime();
        lifetime.Attach(process);
        var output = process.StandardOutput.ReadToEndAsync(); var errors = process.StandardError.ReadToEndAsync();
        try
        {
            bool connected = false;
            for (var i = 0; i < 50; i++)
            {
                if (process.HasExited) throw new Exception("Real Neolink helper exited: " + await errors);
                using var client = new System.Net.Sockets.TcpClient();
                try { await client.ConnectAsync(System.Net.IPAddress.Loopback, rtspPort); connected = true; break; }
                catch (System.Net.Sockets.SocketException) { await Task.Delay(100); }
            }
            if (!connected)
            {
                if (!process.HasExited) process.Kill(true);
                await process.WaitForExitAsync();
                throw new Exception("The real Neolink RTSP endpoint did not start. Synthetic test diagnostics: " + await errors);
            }
            Console.WriteLine("Real Neolink 0.6.2 accepts the generated config and starts RTSP with its private GStreamer runtime.");
        }
        finally
        {
            if (!process.HasExited) process.Kill(true);
            await process.WaitForExitAsync(); await Task.WhenAll(output, errors);
            await server.StopAsync(); File.Delete(path);
        }
    }
}
