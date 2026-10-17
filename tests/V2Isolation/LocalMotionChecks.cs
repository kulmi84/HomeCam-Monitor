using HomeCamMonitor;
using System.Text.Json;
namespace V2Isolation;
internal static partial class Check
{
    private static byte[] MotionFrame(bool moving = false, int x = 40, bool bright = false)
    {
        var frame = Enumerable.Repeat((byte)(bright ? 250 : 40), LocalMotionModel.FrameBytes).ToArray();
        if (moving) for (var y = 55; y < 100; y++) for (var xx = x; xx < x + 55; xx++) frame[y * LocalMotionModel.Width + xx] = 220;
        return frame;
    }
    private static void TestLocalMotion()
    {
        var config = new LocalMotionSettings { Enabled = true, MinimumSeconds = .6, HoldSeconds = 1, MinimumAreaPercent = 1 };
        using (var model = new LocalMotionModel(config))
        {
            for (var i = 0; i < 31; i++) if (model.Analyze(MotionFrame(), TimeSpan.FromSeconds(i * .2)).Triggered) throw new Exception("Warmup/static image triggered motion.");
            var triggers = 0; var active = false;
            for (var i = 31; i < 46; i++) { var result = model.Analyze(MotionFrame(true, 40 + (i - 31) * 2), TimeSpan.FromSeconds(i * .2)); triggers += result.Triggered ? 1 : 0; active |= result.Active; }
            if (triggers != 1 || !active) throw new Exception($"MOG2 motion/dwell/edge gate failed: {triggers}");
            for (var i = 46; i < 65; i++) if (model.Analyze(MotionFrame(), TimeSpan.FromSeconds(i * .2)).Triggered) throw new Exception("Return to background retriggered.");
        }
        using (var model = new LocalMotionModel(withZones()))
        {
            for (var i = 0; i < 31; i++) model.Analyze(MotionFrame(), TimeSpan.FromSeconds(i * .2));
            for (var i = 31; i < 50; i++) if (model.Analyze(MotionFrame(true), TimeSpan.FromSeconds(i * .2)).Triggered) throw new Exception("Motion outside ROI triggered.");
        }
        LocalMotionSettings withZones() { var c = config.Clone(); c.Zones = [new MotionZone(.7, 0, .3, 1)]; return c; }
        using (var model = new LocalMotionModel(config))
        {
            for (var i = 0; i < 31; i++) model.Analyze(MotionFrame(), TimeSpan.FromSeconds(i * .2));
            for (var i = 31; i < 50; i++) if (model.Analyze(MotionFrame(bright: true), TimeSpan.FromSeconds(i * .2)).Triggered) throw new Exception("Global exposure change triggered.");
        }
        var camera = new CameraEntry { Name = "Direkt", StreamUrl = "rtsp://example.invalid/test", MotionEnabled = true, Direct = new DirectCamera { Enabled = true, RtspUrl = "rtsp://example.invalid/test" }, LocalMotion = config.Clone() };
        var settings = new Settings { Cameras = [camera], MotionRetentionDays = 0 };
        var localHa = new CameraEntry { Name = "Local HA", StreamUrl = "rtsp://example.invalid/ha", MotionEnabled = true, LocalMotion = config.Clone() };
        if (SettingsForm.HomeAssistantEventsIncomplete(true, [localHa], "", "")) throw new Exception("Local analysis still requires HA sensors/credentials.");
        localHa.PersonEnabled = true;
        if (!SettingsForm.HomeAssistantEventsIncomplete(true, [localHa], "http://example.invalid", "test-token")) throw new Exception("Local analysis bypassed enabled external person sensor validation.");
        localHa.PersonEntityId = "binary_sensor.person";
        if (SettingsForm.HomeAssistantEventsIncomplete(true, [localHa], "http://example.invalid", "test-token")) throw new Exception("Valid person sensor rejected alongside local analysis.");
        var externalHa = new CameraEntry { Name = "External HA", MotionEnabled = true };
        if (!SettingsForm.HomeAssistantEventsIncomplete(true, [localHa, externalHa], "", "")) throw new Exception("Mixed local/external cameras bypassed HA validation.");
        externalHa.MotionEntityId = "binary_sensor.motion";
        if (SettingsForm.HomeAssistantEventsIncomplete(true, [localHa, externalHa], "http://example.invalid", "test-token")) throw new Exception("Valid mixed sources rejected.");
        externalHa.Active = false; localHa.PersonEnabled = false;
        if (SettingsForm.HomeAssistantEventsIncomplete(true, [localHa, externalHa], "", "")) throw new Exception("Inactive external camera blocks local analysis save.");
        Console.WriteLine("HA save validation: local-only, independent person sensor, mixed sources and inactive external cameras passed.");
        if (!settings.MotionDetectionEnabled || !camera.CanRecord || camera.UsesExternalMotion || camera.SupportsPersonDetection) throw new Exception("Direct local motion routing/capabilities failed.");
        camera.LocalMotion.Enabled = false;
        if (settings.MotionDetectionEnabled || camera.CanRecord) throw new Exception("Image-only direct behavior changed.");
        var external = new CameraEntry { Name = "Alt", StreamUrl = "rtsp://example.invalid/old", MotionEnabled = true, MotionEntityId = "binary_sensor.old" };
        if (!external.UsesExternalMotion || external.LocalMotion.Enabled) throw new Exception("External sensor default changed.");
        var legacy = JsonSerializer.Deserialize<CameraEntry>("{\"Name\":\"Alt\",\"StreamUrl\":\"rtsp://example.invalid/old\",\"MotionEnabled\":true}")!;
        if (legacy.LocalMotion.Enabled || !legacy.UsesExternalMotion) throw new Exception("Legacy camera opted into local analysis.");
        camera.LocalMotion = config.Clone(); camera.LocalMotion.Zones = [new MotionZone(.1,.2,.4,.5)];
        var restored = SettingsBackup.Parse(SettingsBackup.Serialize(settings)).Cameras[0];
        if (!restored.LocalMotion.Enabled || restored.LocalMotion.Zones.Count != 1 || restored.LocalMotion.Id != camera.LocalMotion.Id) throw new Exception("Local motion backup lost settings/ROI.");
        using var form = new SettingsForm(settings, SettingsLocation.DefaultFolder);
        var grid = FindGrid(form); var editor = (LocalMotionEditor)form.Controls.Find("LocalMotionSettings", true).Single();
        if (!Convert.ToBoolean(grid.Rows[0].Cells["MotionEnabled"].FormattedValue)) throw new Exception("Direct local motion checkbox still drawn as disabled/unchecked.");
        if (grid.Rows[0].Cells["MotionEnabled"].ReadOnly || grid.Rows[0].Cells["RecordingEnabled"].ReadOnly || !grid.Rows[0].Cells["PersonEnabled"].ReadOnly)
            throw new Exception("Direct local motion controls not enabled or person enabled incorrectly.");
        using var host = new RenderHost { Opacity = 0, ShowInTaskbar = false, StartPosition = FormStartPosition.Manual, Location = new System.Drawing.Point(-30000,-30000), ClientSize = new System.Drawing.Size(900,620) };
        host.Controls.Add(editor); host.Show(); editor.Show(); Pump(100);
        var details = (MotionDetailsSection)editor.Controls.Find("MotionDetailsSection", true).Single();
        var localSensitivity = editor.Controls.Find("LocalMotionSensitivity", true).Single();
        if (details.Expanded || localSensitivity.Visible) throw new Exception("Motion details must start collapsed.");
        details.Expanded = true; Pump(30);
        if (!localSensitivity.Visible) throw new Exception("Expanded local motion details are missing.");
        using var image = new System.Drawing.Bitmap(editor.Width, editor.Height); editor.DrawToBitmap(image, new System.Drawing.Rectangle(0,0,image.Width,image.Height)); image.Save(Path.Combine(Environment.CurrentDirectory,"v2-local-motion.png"));
        var retained = JsonSerializer.Serialize(LocalMotionSettings.ForRow(grid.Rows[0]));
        grid.Rows[0].Cells["MotionEnabled"].Value = false; Pump(30);
        if (editor.Visible) throw new Exception("Motion editor visible with motion unchecked.");
        grid.Rows[0].Cells["MotionEnabled"].Value = true; Pump(30);
        if (!editor.Visible) throw new Exception("Motion editor did not return after enabling motion.");
        grid.Rows[0].Cells["Active"].Value = false; Pump(30);
        if (editor.Visible) throw new Exception("Motion editor visible for inactive camera.");
        grid.Rows[0].Cells["Active"].Value = true; Pump(30);
        if (!editor.Visible || JsonSerializer.Serialize(LocalMotionSettings.ForRow(grid.Rows[0])) != retained) throw new Exception("Visibility toggle lost motion configuration.");
        host.Hide();
        var source = (ComboBox)editor.Controls.Find("MotionSource",true).Single();
        host.Show(); details.Expanded = true;
        if (source.Items.Count != 1 || source.SelectedIndex != 0 || !source.Text.StartsWith("Lokale Bildanalyse")) throw new Exception("Direct RTSP offers unsupported external event source.");
        grid.Rows[0].Cells["MotionEnabled"].Value = false;
        grid.Rows[0].Cells["MotionEnabled"].Value = true;
        if (!LocalMotionSettings.ForRow(grid.Rows[0]).Enabled || source.SelectedIndex != 0) throw new Exception("Direct motion checkbox cannot restore local analysis while editor is hidden.");
        if (details.Expanded) throw new Exception("Re-enabled motion details must start collapsed.");
        host.Hide();
        TestExternalMotionLayout();
        TestMotionSourceChoices();
        var before = JsonSerializer.Serialize(settings); LocalMotionSettings.ForRow(grid.Rows[0]).Sensitivity = 90;
        if (JsonSerializer.Serialize(settings) != before) throw new Exception("Unsaved local motion edits leaked into real settings.");
        File.WriteAllText(Path.Combine(Environment.CurrentDirectory,"../opencv-build-information.txt"),OpenCvSharp.Cv2.GetBuildInformation());
        Task.Run(TestLocalMotionWorker).GetAwaiter().GetResult();
        Console.WriteLine("Actual OpenCV/MOG2: warmup, static frames, motion/dwell/hold, ROI, exposure suppression, external defaults, direct routing, backup and unsaved isolation passed.");
    }
    private static void TestMotionSourceChoices()
    {
        var cameras = new List<CameraEntry> {
            new() { Name = "HA", StreamUrl = "rtsp://example.invalid/ha", MotionEnabled = true, MotionEntityId = "binary_sensor.keep" },
            new() { Name = "UID", StreamUrl = "rtsp://127.0.0.1:18554/test", MotionEnabled = true, Reolink = new ReolinkCamera { Enabled = true, Uid = "FAKE0123456789AB" } },
            new() { Name = "Direct", StreamUrl = "rtsp://example.invalid/direct", MotionEnabled = true, Direct = new DirectCamera { Enabled = true, RtspUrl = "rtsp://example.invalid/direct" }, LocalMotion = new LocalMotionSettings { Enabled = true } }
        };
        using var form = new SettingsForm(new Settings { Cameras = cameras }, SettingsLocation.DefaultFolder);
        var grid = FindGrid(form);
        var mode = (ComboBox)form.Controls.Find("MotionSource", true).Single();
        for (var pass = 0; pass < 2; pass++)
        {
            grid.CurrentCell = grid.Rows[0].Cells[0];
            if (mode.Items.Count != 2 || Convert.ToString(mode.Items[0]) != "Externer Home-Assistant-Sensor") throw new Exception("HA source label incorrect after camera switch.");
            mode.SelectedIndex = 1;
            if (!LocalMotionSettings.ForRow(grid.Rows[0]).Enabled) throw new Exception("HA local selection mapping failed.");
            mode.SelectedIndex = 0;
            if (LocalMotionSettings.ForRow(grid.Rows[0]).Enabled || Convert.ToString(grid.Rows[0].Cells["MotionEntityId"].Value) != "binary_sensor.keep") throw new Exception("External HA mapping or retained sensor failed.");
            grid.CurrentCell = grid.Rows[1].Cells[0];
            if (mode.Items.Count != 2 || Convert.ToString(mode.Items[0]) != "Bewegungserkennung der Reolink-Kamera") throw new Exception("UID source label incorrectly mentions HA.");
            mode.SelectedIndex = 1;
            if (!LocalMotionSettings.ForRow(grid.Rows[1]).Enabled) throw new Exception("UID local selection mapping failed.");
            mode.SelectedIndex = 0;
            if (LocalMotionSettings.ForRow(grid.Rows[1]).Enabled) throw new Exception("UID camera events selection failed.");
            grid.CurrentCell = grid.Rows[2].Cells[0];
            if (mode.Items.Count != 1 || mode.SelectedIndex != 0 || !LocalMotionSettings.ForRow(grid.Rows[2]).Enabled) throw new Exception("Direct source mapping failed after camera switch.");
        }
        Console.WriteLine("Source-specific HA/UID labels, RTSP local-only choice, camera switching and preserved sensors passed.");
    }
    private static void TestExternalMotionLayout()
    {
        var settings = new Settings { Cameras = [new CameraEntry { Name = "HA layout", StreamUrl = "rtsp://example.invalid/ha", MotionEnabled = true, MotionEntityId = "binary_sensor.layout_motion", PersonEntityId = "binary_sensor.layout_person" }] };
        using var form = new SettingsForm(settings, SettingsLocation.DefaultFolder);
        var editor = (LocalMotionEditor)form.Controls.Find("LocalMotionSettings", true).Single();
        var tabs = (V2ConnectionTabs)form.Controls.Find("CameraConnectionTabs", true).Single();
        var fields = editor.Controls.Find("ExternalMotionFields", true).Single();
        var details = (MotionDetailsSection)editor.Controls.Find("MotionDetailsSection", true).Single();
        if (tabs.Contains(fields) || !editor.Contains(fields)) throw new Exception("External sensors were not moved out of connection settings.");
        var entities = fields.Controls.OfType<TextBox>().Select(c => c.Text).ToArray();
        if (!entities.Contains("binary_sensor.layout_motion") || !entities.Contains("binary_sensor.layout_person")) throw new Exception("Sensor relocation lost existing mappings.");
        using var host = new RenderHost { Opacity = 0, ShowInTaskbar = false, StartPosition = FormStartPosition.Manual, Location = new System.Drawing.Point(-30000,-30000), ClientSize = new System.Drawing.Size(900,620) };
        host.Controls.Add(editor); host.Show(); editor.Show(); Pump(30);
        if (details.Expanded || fields.Visible) throw new Exception("External details must start collapsed.");
        details.Expanded = true; Pump(30);
        if (!fields.Visible) throw new Exception("HA sensor fields are missing from external details.");
        ((ComboBox)editor.Controls.Find("MotionSource", true).Single()).SelectedIndex = 1;
        details.Expanded = true; Pump(30);
        if (fields.Visible) throw new Exception("External sensor fields leaked into local details.");
        ((ComboBox)editor.Controls.Find("MotionSource", true).Single()).SelectedIndex = 0;
        if (details.Expanded || !entities.SequenceEqual(fields.Controls.OfType<TextBox>().Select(c => c.Text))) throw new Exception("Changing source lost sensors or expanded details.");
        Console.WriteLine("Collapsed local/external details, matching source fields and retained HA sensor mappings passed.");
    }
    private static async Task TestLocalMotionWorker()
    {
        var executable = Path.Combine(Environment.CurrentDirectory,"publish-v2-beta","ffmpeg.exe");
        var fixture = Path.Combine(Environment.CurrentDirectory,"local-motion-test-" + Guid.NewGuid().ToString("N") + ".mkv");
        try
        {
            var start = new System.Diagnostics.ProcessStartInfo(executable) { UseShellExecute=false, CreateNoWindow=true, RedirectStandardError=true };
            foreach(var arg in new[]{"-hide_banner","-loglevel","error","-f","lavfi","-i","color=c=0x282828:s=320x180:r=5:d=12","-vf","drawbox=x=40:y=55:w=55:h=45:color=white:t=fill:enable='between(t,6,9)'","-c:v","ffv1","-y",fixture}) start.ArgumentList.Add(arg);
            using(var process=System.Diagnostics.Process.Start(start)!) { var errors=process.StandardError.ReadToEndAsync();await process.WaitForExitAsync();await errors;if(process.ExitCode!=0)throw new Exception("Cannot create local motion fixture."); }
            var camera = new CameraEntry {Name="Synthetic",StreamUrl=fixture,MotionEnabled=true,LocalMotion=new LocalMotionSettings {Enabled=true,MinimumSeconds=.6}};
            var triggered=new TaskCompletionSource<MotionAnalysis>(TaskCreationOptions.RunContinuationsAsynchronously);
            var worker=new LocalMotionWorker(camera,result=>{if(result.Triggered)triggered.TrySetResult(result);},executable);
            try { await triggered.Task.WaitAsync(TimeSpan.FromSeconds(22)); }
            finally { worker.Dispose();await worker.Completion.WaitAsync(TimeSpan.FromSeconds(5)); }
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var pixels=await LocalMotionWorker.Preview(fixture,timeout.Token,executable);
            if(pixels.Length!=LocalMotionModel.FrameBytes)throw new Exception("Wrong preview size.");
            Console.WriteLine("Real FFmpeg -> latest-frame sampler -> native MOG2 event, memory-only preview and worker shutdown passed.");
        }
        finally { File.Delete(fixture); }
    }}
