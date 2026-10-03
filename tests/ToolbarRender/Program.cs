using System.Drawing;
using System.Windows.Forms;
using HomeCamMonitor;

namespace ToolbarRender;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        if (args.Length > 0 && args[0] == "--pre-roll-check")
        {
            Task.Run(async () =>
            {
                var source = args[1]; var output = args[2]; Directory.CreateDirectory(output);
                using var buffer = new MotionPreRoll(source);
                await Task.Delay(8000);
                var trigger = DateTime.UtcNow;
                var snapshot = Path.Combine(output, "pre-roll.png");
                if (!await buffer.CaptureAsync(trigger, 3, 0, snapshot, true))
                    throw new InvalidOperationException("Snapshot pre-roll buffer did not warm up.");
                using var img = Image.FromFile(snapshot);
                if (img.Width != 320 || img.Height != 180) throw new InvalidOperationException("Buffered snapshot is invalid.");
                if (!await buffer.CaptureAsync(trigger, 3, 2, Path.Combine(output, "pre-roll.mkv"), false))
                    throw new InvalidOperationException("Video pre-roll buffer is missing.");
                if (new FileInfo(Path.Combine(output, "pre-roll.mkv")).Length < 4096)
                    throw new InvalidOperationException("Buffered video is empty.");
            }).GetAwaiter().GetResult();
            return;
        }
        ApplicationConfiguration.Initialize();
        var backupFixture = SettingsStore.CreateForNewInstallation();
        backupFixture.Cameras.Add(new CameraEntry { Name = "Testkamera", StreamUrl = "rtsp://127.0.0.1/Test", MotionEnabled = true, PersonEnabled = true,
            MotionEntityId = "binary_sensor.test_motion", PersonEntityId = "binary_sensor.test_person", MotionAction = "Both", MotionVideoSeconds = 60 });
        backupFixture.HomeAssistantToken = "test-token";
        backupFixture.ManualSnapshotFolder = Path.Combine(Path.GetTempPath(), "manual photos");
        backupFixture.MotionVideoFolder = @"\\nas\share\motion";
        backupFixture.MotionRetentionDays = 14;
        backupFixture.SnapshotPreRollSeconds = 3;
        var roundtrip = SettingsBackup.Parse(SettingsBackup.Serialize(backupFixture));
        if (System.Text.Json.JsonSerializer.Serialize(roundtrip) != System.Text.Json.JsonSerializer.Serialize(backupFixture))
            throw new InvalidOperationException("Backup round trip changed configuration fields.");
        var legacySettings = SettingsBackup.Parse(System.Text.Json.JsonSerializer.Serialize(backupFixture));
        if (legacySettings.Cameras[0].PersonEntityId != "binary_sensor.test_person" || legacySettings.HomeAssistantToken != "test-token")
            throw new InvalidOperationException("Existing settings.json import lost camera/token configuration.");
        foreach (var invalid in new[] { "{}", "[]", "{\"Cameras\":null}", "{\"Cameras\":[null]}",
            "{\"Format\":\"Other\",\"Version\":1,\"Settings\":{\"Cameras\":[]}}", "{\"Cameras\":[],\"ToolbarSizePercent\":10000}" })
        {
            var rejected = false;
            try { SettingsBackup.Parse(invalid); } catch { rejected = true; }
            if (!rejected) throw new InvalidOperationException("Invalid backup was accepted.");
        }
        if (SettingsBackup.Parse(SettingsBackup.Serialize(SettingsStore.CreateForNewInstallation())).Cameras.Count != 0)
            throw new InvalidOperationException("Empty-camera backup cannot be restored.");
        if (RecordingStorage.Resolve("", RecordingStorage.ManualSnapshots) != RecordingStorage.ManualSnapshots)
            throw new InvalidOperationException("Default snapshot storage changed.");
        var storageTest = Path.Combine(Path.GetTempPath(), "HomeCam-Storage-Test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(storageTest);
        try
        {
            var locationKey = @"Software\HomeCamMonitor-Test-" + Guid.NewGuid().ToString("N");
            try
            {
                var oldSettingsFile = Path.Combine(storageTest, "source-settings.json");
                var newSettingsFile = Path.Combine(storageTest, "new folder", "settings.json");
                File.WriteAllText(oldSettingsFile, "original");
                SettingsLocation.Relocate(oldSettingsFile, newSettingsFile, System.Text.Json.JsonSerializer.Serialize(backupFixture), () =>
                {
                    if (!File.Exists(oldSettingsFile) || !File.Exists(newSettingsFile)) throw new InvalidOperationException("Source removed before destination committed.");
                    using var location = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(locationKey);
                    location.SetValue("SettingsFolder", Path.GetDirectoryName(newSettingsFile)!);
                }, false);
                if (File.Exists(oldSettingsFile) || SettingsLocation.ReadFolder(locationKey) != Path.GetDirectoryName(newSettingsFile) ||
                    SettingsBackup.Read(newSettingsFile).HomeAssistantToken != "test-token")
                    throw new InvalidOperationException("Settings migration or persisted location did not survive reloading.");
                var failedTarget = Path.Combine(storageTest, "failed", "settings.json");
                Directory.CreateDirectory(Path.GetDirectoryName(failedTarget)!); File.WriteAllText(failedTarget, "existing target");
                var failedAsExpected = false;
                try { SettingsLocation.Relocate(newSettingsFile, failedTarget, "replacement", () => throw new IOException("test failure"), true); }
                catch (IOException) { failedAsExpected = true; }
                if (!failedAsExpected || !File.Exists(newSettingsFile) || File.ReadAllText(failedTarget) != "existing target" ||
                    SettingsLocation.ReadFolder(locationKey) != Path.GetDirectoryName(newSettingsFile))
                    throw new InvalidOperationException("Failed settings migration changed the original or target configuration.");
            }
            finally { Microsoft.Win32.Registry.CurrentUser.DeleteSubKeyTree(locationKey, false); }
            var backupFile = Path.Combine(storageTest, "settings-backup.json");
            SettingsBackup.Write(backupFile, backupFixture);
            if (SettingsBackup.Read(backupFile).MotionVideoFolder != backupFixture.MotionVideoFolder || Directory.GetFiles(storageTest, "*.tmp").Length != 0)
                throw new InvalidOperationException("Backup file write/read did not preserve the storage path.");
            var automaticCapture = Path.Combine(storageTest, "Test_2026-01-01_00-00-00-000_" + new string('a', 32) + ".png");
            var manual = Path.Combine(storageTest, "Test_2026-01-01_00-00-00.png");
            var foreign = Path.Combine(storageTest, "other.mkv");
            foreach (var file in new[] { automaticCapture, manual, foreign }) { File.WriteAllText(file, "test"); File.SetLastWriteTimeUtc(file, DateTime.UtcNow.AddDays(-30)); }
            var tracked = new HashSet<string> { automaticCapture, manual, foreign };
            RecordingStorage.CleanupPaths(tracked, DateTime.UtcNow.AddDays(-7));
            if (File.Exists(automaticCapture) || !File.Exists(manual) || !File.Exists(foreign))
                throw new InvalidOperationException("Retention deleted manual/foreign recordings or missed automatic recordings.");
        }
        finally { Directory.Delete(storageTest, true); }
        var output = args.Length == 0 ? "toolbar-render" : args[0];
        Directory.CreateDirectory(output);
        using (var videoHost = new Panel())
        using (var nativeVideo = new Form { TopLevel = false, FormBorderStyle = FormBorderStyle.FixedSingle })
        {
            videoHost.Controls.Add(nativeVideo);
            videoHost.CreateControl();
            var nativeHandle = nativeVideo.Handle;
            NativeMethods.PrepareVideoChildren(videoHost.Handle);
            if ((NativeMethods.GetWindowStyle(nativeHandle, -16) & 0x00C40000) != 0)
                throw new InvalidOperationException("Embedded video still has a native caption/border.");
        }
        var freshDefaults = SettingsStore.CreateForNewInstallation();
        if (freshDefaults.AlwaysOnTop || freshDefaults.ToolbarSizePercent != 90 || !freshDefaults.AutoScaleToolbar ||
            freshDefaults.ShowGridCameraNames || !freshDefaults.MinimizeWhenInactive || freshDefaults.MotionIndicatorSeconds != 1 ||
            !freshDefaults.DirectHomeAssistantEnabled || freshDefaults.HomeAssistantUrl != "https://192.168.19.9:8123" ||
            freshDefaults.Cameras.Count != 0 || freshDefaults.HomeAssistantToken.Length != 0 ||
            freshDefaults.ShowEmptyFourthFieldBorder || !freshDefaults.ShowEmptyCameraLogo ||
            freshDefaults.SnapshotPreRollSeconds != 0 || freshDefaults.VideoPreRollSeconds != 0)
            throw new InvalidOperationException("Fresh-install defaults do not match the agreed settings.");
        var existing = System.Text.Json.JsonSerializer.Deserialize<Settings>(
            "{\"AlwaysOnTop\":true,\"ToolbarSizePercent\":75,\"HomeAssistantUrl\":\"http://existing:8123\"}")!;
        if (!existing.AlwaysOnTop || existing.ToolbarSizePercent != 75 || existing.HomeAssistantUrl != "http://existing:8123")
            throw new InvalidOperationException("Existing settings were overridden by fresh-install defaults.");
        if (new Settings().SnapshotPreRollSeconds != 0 || new Settings().VideoPreRollSeconds != 0)
            throw new InvalidOperationException("Pre-roll must be disabled by default.");
        var segments = new[] { new MotionPreRoll.Segment("early", DateTime.UnixEpoch, DateTime.UnixEpoch.AddSeconds(1)),
            new MotionPreRoll.Segment("later", DateTime.UnixEpoch.AddSeconds(1), DateTime.UnixEpoch.AddSeconds(2)) };
        if (MotionPreRoll.SnapshotSegment(segments, DateTime.UnixEpoch.AddSeconds(0.5))?.Path != "early" ||
            MotionPreRoll.SnapshotSegment(segments, DateTime.UnixEpoch.AddSeconds(-1)) is not null)
            throw new InvalidOperationException("Pre-roll snapshot selection does not match the requested past time.");
        var progress = new PlaybackProgress();
        if (progress.Observe(1) || progress.Observe(1) || !progress.Observe(2) ||
            progress.Observe(double.NaN) || progress.Observe(2) || !progress.Observe(0))
            throw new InvalidOperationException("Playback progress incorrectly accepts stalled/invalid timestamps.");
        foreach (var size in new[] { new Size(160, 90), new Size(480, 270), new Size(1920, 1080) })
        {
            using var placeholder = new CameraPlaceholderPanel { Size = size };
            placeholder.Configure("Testkamera mit langem Namen");
            placeholder.SetOffline();
            using var bitmap = new Bitmap(size.Width, size.Height);
            placeholder.DrawToBitmap(bitmap, new Rectangle(Point.Empty, size));
            bitmap.Save(Path.Combine(output, $"offline-{size.Width}x{size.Height}.png"));
            placeholder.SetEmpty(true);
            using var emptyLogo = new Bitmap(size.Width, size.Height);
            placeholder.DrawToBitmap(emptyLogo, new Rectangle(Point.Empty, size));
            emptyLogo.Save(Path.Combine(output, $"empty-logo-{size.Width}x{size.Height}.png"));
            placeholder.SetEmpty(false);
            using var emptyBlack = new Bitmap(size.Width, size.Height);
            placeholder.DrawToBitmap(emptyBlack, new Rectangle(Point.Empty, size));
            for (var y = 0; y < size.Height; y++)
                for (var x = 0; x < size.Width; x++)
                    if (emptyBlack.GetPixel(x, y).ToArgb() != Color.Black.ToArgb())
                        throw new InvalidOperationException("Empty camera field with logo disabled is not fully black.");
            placeholder.SetEmpty(false, true);
            using var borderedEmpty = new Bitmap(size.Width, size.Height);
            placeholder.DrawToBitmap(borderedEmpty, new Rectangle(Point.Empty, size));
            borderedEmpty.Save(Path.Combine(output, $"empty-border-{size.Width}x{size.Height}.png"));
            if (borderedEmpty.GetPixel(size.Width - 2, size.Height / 2).R < 40 ||
                borderedEmpty.GetPixel(size.Width / 2, size.Height - 2).R < 40)
                throw new InvalidOperationException("Empty tile outside border is missing.");
            if (borderedEmpty.GetPixel(2, size.Height / 2).ToArgb() != Color.Black.ToArgb() ||
                borderedEmpty.GetPixel(size.Width / 2, 2).ToArgb() != Color.Black.ToArgb())
                throw new InvalidOperationException("Empty tile border leaks onto interior grid edges.");
            placeholder.SetEmpty(false, true, -1);
            using var singleBorder = new Bitmap(size.Width, size.Height);
            placeholder.DrawToBitmap(singleBorder, new Rectangle(Point.Empty, size));
            singleBorder.Save(Path.Combine(output, $"empty-border-single-{size.Width}x{size.Height}.png"));
            if (singleBorder.GetPixel(2, size.Height / 2).R < 40 ||
                singleBorder.GetPixel(size.Width - 2, size.Height / 2).R < 40 ||
                singleBorder.GetPixel(size.Width / 2, 2).R < 40 ||
                singleBorder.GetPixel(size.Width / 2, size.Height - 2).R < 40)
                throw new InvalidOperationException("Single empty field outside border is incomplete.");
            placeholder.SuppressOuterBorder = true;
            using var fullscreenEmpty = new Bitmap(size.Width, size.Height);
            placeholder.DrawToBitmap(fullscreenEmpty, new Rectangle(Point.Empty, size));
            for (var y = 0; y < size.Height; y++)
                for (var x = 0; x < size.Width; x++)
                    if (fullscreenEmpty.GetPixel(x, y).ToArgb() != Color.Black.ToArgb())
                        throw new InvalidOperationException("Outside border is visible in fullscreen.");
            placeholder.SuppressOuterBorder = false;
            for (var gridIndex = 0; gridIndex < 4; gridIndex++)
            {
                placeholder.SetEmpty(false, true, gridIndex);
                using var corner = new Bitmap(size.Width, size.Height);
                placeholder.DrawToBitmap(corner, new Rectangle(Point.Empty, size));
                corner.Save(Path.Combine(output, $"empty-border-slot-{gridIndex}-{size.Width}x{size.Height}.png"));
                var outerX = gridIndex % 2 == 0 ? 2 : size.Width - 2;
                var innerX = gridIndex % 2 == 0 ? size.Width - 2 : 2;
                var outerY = gridIndex < 2 ? 2 : size.Height - 2;
                var innerY = gridIndex < 2 ? size.Height - 2 : 2;
                if (corner.GetPixel(outerX, size.Height / 2).R < 40 ||
                    corner.GetPixel(size.Width / 2, outerY).R < 40 ||
                    corner.GetPixel(innerX, size.Height / 2).ToArgb() != Color.Black.ToArgb() ||
                    corner.GetPixel(size.Width / 2, innerY).ToArgb() != Color.Black.ToArgb())
                    throw new InvalidOperationException("Empty tile border does not follow its outside grid edges.");
            }
            placeholder.Configure("Testkamera mit langem Namen");
            placeholder.SetOffline();
            using var offlineAfterBorder = new Bitmap(size.Width, size.Height);
            placeholder.DrawToBitmap(offlineAfterBorder, new Rectangle(Point.Empty, size));
            if (offlineAfterBorder.GetPixel(size.Width - 2, size.Height / 2).ToArgb() != Color.Black.ToArgb())
                throw new InvalidOperationException("Empty tile border persists on a configured camera.");
        }

        using var toolbar = new ToolbarForm(null!);
        toolbar.CameraName = "Einfahrt";
        toolbar.Show();
        Application.DoEvents();
        Save(toolbar, output, "fresh-100");
        toolbar.SetSizePercent(90);
        Application.DoEvents();
        Save(toolbar, output, "live-90");
        toolbar.SetSizePercent(50);
        Application.DoEvents();
        Save(toolbar, output, "live-50");
        toolbar.SetSizePercent(100);
        Application.DoEvents();
        Save(toolbar, output, "restored-100");
        toolbar.Hide();
        toolbar.SetSizePercent(50);
        toolbar.Show();
        Application.DoEvents();
        Save(toolbar, output, "hidden-then-50");

        using var restarted = new ToolbarForm(null!);
        restarted.CameraName = "Einfahrt";
        restarted.SetSizePercent(50);
        restarted.Show();
        Application.DoEvents();
        Save(restarted, output, "restart-50");

        foreach (var percent in new[] { 100, 50, 100 })
        {
            var expectedHeight = (int)Math.Round(34 * percent / 100d);
            using var fresh = new ToolbarForm(null!);
            fresh.SetSizePercent(percent);
            fresh.Show();
            Application.DoEvents();
            if (fresh.Height != expectedHeight || fresh.ClientSize.Height != expectedHeight)
                throw new InvalidOperationException($"Initial {percent}%: window {fresh.Height}px, client {fresh.ClientSize.Height}px, expected {expectedHeight}px");
        }
        if (toolbar.Height != 17 || restarted.Height != 17)
            throw new InvalidOperationException($"50% after toggle: {toolbar.Height}px; after restart: {restarted.Height}px; expected 17px");
        AssertSameImage(output, "fresh-100", "restored-100");
        AssertSameImage(output, "live-50", "restart-50");
        AssertSameImage(output, "live-50", "hidden-then-50");

        foreach (var size in new[] { new Size(180, 100), new Size(181, 101), new Size(752, 473) })
        {
            var tiles = MonitorForm.GetCameraGridBounds(size);
            if (tiles.Length != 4 ||
                tiles[0].Right != tiles[1].Left || tiles[2].Right != tiles[3].Left ||
                tiles[0].Bottom != tiles[2].Top || tiles[1].Bottom != tiles[3].Top ||
                tiles[0].Width != tiles[2].Width || tiles[1].Width != tiles[3].Width ||
                tiles[0].Height != tiles[1].Height || tiles[2].Height != tiles[3].Height ||
                tiles[1].Right != size.Width || tiles[3].Bottom != size.Height)
                throw new InvalidOperationException($"Grid tiles do not meet at the same center for {size}");
        }
        var tileBorders = MonitorForm.GetGridMotionBorderBounds(new Size(180, 100));
        if (tileBorders.Length != 4 ||
            tileBorders.Any(border => border.Left < 2 || border.Top < 2 ||
                border.Right > 178 || border.Bottom > 98))
            throw new InvalidOperationException("Motion borders must stay inside their camera tile");

        var automatic = new Settings { AutoScaleToolbar = true, ToolbarSizePercent = 75 };
        foreach (var (width, expected) in new[] { (480, 100), (340, 100), (300, 87), (240, 68), (480, 100) })
        {
            var actual = MonitorForm.GetToolbarSizePercent(automatic, width);
            if (actual != expected)
                throw new InvalidOperationException($"Automatic toolbar size at {width}px: {actual}%, expected {expected}%");
            toolbar.SetSizePercent(actual);
            if (toolbar.Width > width - 20)
                throw new InvalidOperationException($"Toolbar {toolbar.Width}px does not fit a {width}px camera window");
        }
        automatic.AutoScaleToolbar = false;
        if (MonitorForm.GetToolbarSizePercent(automatic, 240) != 75)
            throw new InvalidOperationException("Manual toolbar size was not restored when automatic scaling was disabled");

        var displays = new (string DeviceName, Rectangle WorkingArea)[]
        {
            (@"\\.\DISPLAY1", new Rectangle(0, 0, 1920, 1040)),
            (@"\\.\DISPLAY2", new Rectangle(-1600, 0, 1600, 900))
        };
        var savedWindow = new Settings
        {
            Left = -1400, Top = 100, Width = 480, Height = 270,
            LastMonitorDeviceName = @"\\.\DISPLAY2", MonitorOffsetX = 200, MonitorOffsetY = 100
        };
        if (MonitorForm.RestoreWindowBounds(savedWindow, displays) != new Rectangle(-1400, 100, 480, 270))
            throw new InvalidOperationException("Window on the monitor left of the primary display was not restored");
        var changedLayout = new (string DeviceName, Rectangle WorkingArea)[]
        {
            displays[0], (@"\\.\DISPLAY2", new Rectangle(1920, 0, 1600, 900))
        };
        if (MonitorForm.RestoreWindowBounds(savedWindow, changedLayout) != new Rectangle(2120, 100, 480, 270))
            throw new InvalidOperationException("Window did not follow its monitor after the display layout changed");
        savedWindow.Left = -3000;
        if (MonitorForm.RestoreWindowBounds(savedWindow, [displays[0]]) != new Rectangle(720, 385, 480, 270))
            throw new InvalidOperationException("Missing display did not fall back to the primary screen");

        using var monitor = new MonitorForm();
        if (monitor.Text != "HomeCamMonitor Beta")
            throw new InvalidOperationException("The Windows application title is missing.");
        if (!ReferenceEquals(monitor.Icon, ApplicationBranding.WindowIcon))
            throw new InvalidOperationException("Main window does not use the HomeCamMonitor icon.");
        using (var layoutMonitor = new MonitorForm())
        {
            const System.Reflection.BindingFlags privateInstance = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            typeof(MonitorForm).GetField("settings", privateInstance)!.SetValue(layoutMonitor, new Settings());
            var originalBounds = layoutMonitor.Bounds;
            layoutMonitor.ToggleFullscreen();
            var fullscreenBounds = layoutMonitor.Bounds;
            typeof(MonitorForm).GetField("gridMode", privateInstance)!.SetValue(layoutMonitor, true);
            typeof(MonitorForm).GetMethod("ExitGridView", privateInstance)!.Invoke(layoutMonitor, new object?[] { null });
            if (layoutMonitor.Bounds != fullscreenBounds || !(bool)typeof(MonitorForm).GetField("fullscreen", privateInstance)!.GetValue(layoutMonitor)!)
                throw new InvalidOperationException("Grid-to-single transition exited fullscreen.");
            layoutMonitor.ToggleFullscreen();
            if (layoutMonitor.Bounds != originalBounds)
                throw new InvalidOperationException("Fullscreen restore bounds were lost after leaving the grid.");
        }
        using (var drag = new DragSurfaceForm(monitor))
        using (var grip = new ResizeGripForm(monitor, NativeMethods.HtRight, Cursors.SizeWE))
        using (var hiddenToolbar = new ToolbarForm(monitor))
        using (var indicator = new MotionIndicatorForm())
        {
            foreach (var window in new Form[] { monitor, drag, grip, hiddenToolbar, indicator })
            {
                window.HandleCreated += (_, _) =>
                {
                    var style = NativeMethods.GetWindowStyle(window.Handle, -16);
                    if ((style & 0x00C40000) != 0 || (style & unchecked((int)0x80000000)) == 0)
                        throw new InvalidOperationException($"{window.GetType().Name} acquired a native caption during creation.");
                };
                var handle = window.Handle;
                var style = NativeMethods.GetWindowStyle(handle, -16);
                if ((style & 0x00C40000) != 0)
                    throw new InvalidOperationException($"Hidden {window.GetType().Name} has a native caption.");
            }
        }
        if (OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000) &&
            (monitor.Region is not null || monitor.ClientSize != monitor.Size))
            throw new InvalidOperationException($"Rounded borderless window is inset: window {monitor.Size}, client {monitor.ClientSize}");
        foreach (var width in new[] { 640, 641, 640, 641, 640 })
        {
            monitor.Bounds = new Rectangle(200, 150, width, 360);
            Application.DoEvents();
            var expected = (int)Math.Round(width * 9d / 16d);
            if (Math.Abs(monitor.Height - expected) > 1)
                throw new InvalidOperationException($"Camera window grew while resizing: {monitor.Bounds}, expected height {expected}");
            foreach (var surface in monitor.Controls.OfType<Panel>())
                if (surface.Bounds != monitor.ClientRectangle)
                    throw new InvalidOperationException($"Camera surface is inset after resizing: {surface.Bounds} versus {monitor.ClientRectangle}");
            var cameraGrid = monitor.Controls.OfType<Panel>()
                .Single(panel => panel.Controls.OfType<Panel>().Count() == 4);
            var tiles = MonitorForm.GetCameraGridBounds(cameraGrid.ClientSize);
            for (var index = 0; index < tiles.Length; index++)
                if (cameraGrid.Controls[index].Bounds != tiles[index])
                    throw new InvalidOperationException($"Camera tile {index} is offset after resizing");
        }
        var previousHeight = monitor.Height;
        for (var move = 0; move < 5; move++)
        {
            monitor.Location = new Point(200 + move, 150 + move);
            Application.DoEvents();
            if (monitor.Height != previousHeight)
                throw new InvalidOperationException("Camera window grew while moving");
        }

        using var settingsForm = new SettingsForm(new Settings
        {
            Cameras = [new CameraEntry { Name = "Einfahrt", StreamUrl = "rtsp://127.0.0.1:8554/Einfahrt",
                PersonEntityId = "binary_sensor.einfahrt_person" }]
        });
        if (!ReferenceEquals(settingsForm.Icon, ApplicationBranding.WindowIcon))
            throw new InvalidOperationException("Settings window does not use the HomeCamMonitor icon.");
        settingsForm.Show();
        Application.DoEvents();
        var storagePaths = AllControls(settingsForm).OfType<TextBox>().Where(field => field.Name.StartsWith("StoragePath")).ToArray();
        if (storagePaths.Length != 4 || storagePaths.Any(field => string.IsNullOrWhiteSpace(field.Text)))
            throw new InvalidOperationException("Four independent storage path controls are missing.");
        var expectedMotionSnapshots = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "HomeCam Monitor", "Bewegung");
        if (settingsForm.Controls.Find("StoragePath2", true).OfType<TextBox>().Single().Text != expectedMotionSnapshots ||
            RecordingStorage.ResolveMotionSnapshots("") != expectedMotionSnapshots ||
            RecordingStorage.ResolveMotionSnapshots(RecordingStorage.MotionDefault + Path.DirectorySeparatorChar) != expectedMotionSnapshots)
            throw new InvalidOperationException("Movement snapshots still use the old Videos default.");
        var customMotionSnapshots = Path.Combine(Path.GetTempPath(), "HomeCam-custom-snapshots");
        if (RecordingStorage.ResolveMotionSnapshots(customMotionSnapshots) != customMotionSnapshots ||
            settingsForm.Controls.Find("StoragePath3", true).OfType<TextBox>().Single().Text != RecordingStorage.MotionDefault)
            throw new InvalidOperationException("Correcting snapshot defaults changed a custom folder or the video default.");
        var settingsStoragePath = settingsForm.Controls.Find("SettingsStoragePath", true).OfType<TextBox>().Single();
        if (settingsStoragePath.Text != SettingsStore.Folder)
            throw new InvalidOperationException("The current settings storage folder is not displayed.");
        using (var settingsImage = new Bitmap(settingsForm.Width, settingsForm.Height))
        {
            settingsForm.DrawToBitmap(settingsImage, new Rectangle(Point.Empty, settingsImage.Size));
            settingsImage.Save(Path.Combine(output, "settings.png"));
        }
        var cameraTable = AllControls(settingsForm).OfType<DataGridView>().Single();
        if (Convert.ToBoolean(cameraTable.Rows[0].Cells["MotionEnabled"].Value) ||
            !Convert.ToBoolean(cameraTable.Rows[0].Cells["PersonEnabled"].Value))
            throw new InvalidOperationException("An existing person sensor must stay enabled independently of motion");
        var cameraHint = settingsForm.Controls.Find("CameraHint", true).Single();
        var options = settingsForm.Controls.Find("Options", true).Single();
        var startup = settingsForm.Controls.Find("StartupOptions", true).Single();
        var generalGroup = AllControls(settingsForm).OfType<GroupBox>()
            .Single(group => group.Text == "Allgemeine Einstellungen");
        var activityGroup = AllControls(settingsForm).OfType<GroupBox>()
            .Single(group => group.Text == "Bewegung und Aktivitätsanzeige");
        var motionGroup = AllControls(settingsForm).OfType<GroupBox>()
            .Single(group => group.Text == "Bewegung pro Kamera und Home Assistant");
        static int TopOnScreen(Control control) => control.PointToScreen(Point.Empty).Y;
        static int BottomOnScreen(Control control) => TopOnScreen(control) + control.Height;
        if (BottomOnScreen(cameraTable) > TopOnScreen(cameraHint) ||
            BottomOnScreen(cameraHint) > TopOnScreen(generalGroup) ||
            BottomOnScreen(options) > TopOnScreen(startup) ||
            BottomOnScreen(generalGroup) > TopOnScreen(activityGroup) ||
            BottomOnScreen(activityGroup) > TopOnScreen(motionGroup))
            throw new InvalidOperationException($"Settings rows overlap: table={BottomOnScreen(cameraTable)}, hint={TopOnScreen(cameraHint)}..{BottomOnScreen(cameraHint)}, general={TopOnScreen(generalGroup)}..{BottomOnScreen(generalGroup)}, options={BottomOnScreen(options)}, startup={TopOnScreen(startup)}, motion={TopOnScreen(motionGroup)}");
        var selectedCamera = AllControls(motionGroup).OfType<Label>()
            .Single(label => label.Text == "Ausgewählte Kamera:");
        var recordingGroup = settingsForm.Controls.Find("MotionCaptureOptions", true).Single();
        var actionLabel = AllControls(recordingGroup).OfType<Label>()
            .Single(label => label.Text == "Für die ausgewählte Kamera");
        if (recordingGroup.PointToScreen(Point.Empty).X <= selectedCamera.PointToScreen(Point.Empty).X)
            throw new InvalidOperationException("Recording options must be in the right-hand column.");
        var indicatorLabel = AllControls(activityGroup).OfType<Label>()
            .Single(label => label.Text == "Aktivitätssymbole anzeigen:");
        if (!AllControls(activityGroup).OfType<CheckBox>().Any(check => check.Text == "Bewegungserkennung aktiv") ||
            indicatorLabel.Parent is null)
            throw new InvalidOperationException("Activity controls must be grouped together");
        var action = AllControls(motionGroup).OfType<ComboBox>()
            .Single(combo => combo.Items.Contains("Snapshot + Videoaufnahme"));
        var videoSeconds = AllControls(motionGroup).OfType<ComboBox>()
            .Single(combo => combo.Items.Contains("30 Sekunden"));
        if (videoSeconds.Width < 110)
            throw new InvalidOperationException("The video duration selection is too narrow");
        if (BottomOnScreen(actionLabel) > TopOnScreen(action))
            throw new InvalidOperationException("Motion action is not below the selected camera");
        var seconds = AllControls(settingsForm)
            .OfType<Label>().Single(label => label.Text == "Sekunden" &&
                label.Parent!.Controls.OfType<NumericUpDown>().Any(number => number.Maximum == 300));
        var duration = seconds.Parent!.Controls.OfType<NumericUpDown>().Single();
        if (seconds.Top > duration.Bottom || seconds.Bottom < duration.Top)
            throw new InvalidOperationException("Seconds label wrapped away from the duration field");
        var gridHighlight = AllControls(settingsForm).OfType<CheckBox>()
            .Single(check => check.Text == "Bewegung im 4er-Raster hervorheben");
        var indicatorDuration = AllControls(settingsForm).OfType<NumericUpDown>()
            .Single(number => number.Maximum == 10);
        if (gridHighlight.Checked || indicatorDuration.Value != 2)
            throw new InvalidOperationException("Grid highlighting must be disabled and indicators shown for two seconds by default");
        var buttons = AllControls(settingsForm).OfType<FlowLayoutPanel>()
            .Single(panel => panel.Controls.OfType<Button>().Any(button => button.Text == "Speichern"));
        var buttonPosition = settingsForm.PointToClient(buttons.PointToScreen(Point.Empty));
        var bottomGap = settingsForm.ClientSize.Height - buttonPosition.Y - buttons.Height;
        if (bottomGap < 6 || bottomGap > 14 || buttons.Parent?.Name != "SettingsFooter")
            throw new InvalidOperationException("Settings buttons are clipped or missing their fixed bottom spacing.");
        var screenArea = Screen.FromControl(settingsForm).WorkingArea;
        if (!screenArea.Contains(settingsForm.Bounds))
            throw new InvalidOperationException("Settings window extends beyond the screen working area.");
        var scrollArea = (ScrollableControl)settingsForm.Controls.Find("SettingsScrollArea", true).Single();
        var betaGroup = settingsForm.Controls.Find("BetaDiagnostics", true).Single();
        foreach (var height in new[] { settingsForm.ClientSize.Height, 610 })
        {
            settingsForm.ClientSize = new Size(settingsForm.ClientSize.Width, height);
            Application.DoEvents();
            scrollArea.AutoScrollPosition = new Point(0, scrollArea.VerticalScroll.Maximum);
            Application.DoEvents();
            var betaBounds = new Rectangle(scrollArea.PointToClient(betaGroup.PointToScreen(Point.Empty)), betaGroup.Size);
            if (!scrollArea.ClientRectangle.Contains(betaBounds) || betaBounds.Bottom > scrollArea.ClientSize.Height - 8)
                throw new InvalidOperationException("The bottom BETA settings group cannot be scrolled fully into view.");
        }
        using (var bottomImage = new Bitmap(settingsForm.Width, settingsForm.Height))
        {
            settingsForm.DrawToBitmap(bottomImage, new Rectangle(Point.Empty, bottomImage.Size));
            bottomImage.Save(Path.Combine(output, "settings-bottom.png"));
        }
        settingsForm.ClientSize = new Size(1260, 1000);
        Application.DoEvents();
        using var wideSettings = new Bitmap(settingsForm.Width, settingsForm.Height);
        settingsForm.DrawToBitmap(wideSettings, new Rectangle(Point.Empty, wideSettings.Size));
        wideSettings.Save(Path.Combine(output, "settings-wide.png"));
        var storageGroup = settingsForm.Controls.Find("RecordingStorage", true).Single();
        scrollArea.ScrollControlIntoView(storageGroup);
        Application.DoEvents();
        buttonPosition = settingsForm.PointToClient(buttons.PointToScreen(Point.Empty));
        if (buttonPosition.Y < 0 || buttonPosition.Y + buttons.Height > settingsForm.ClientSize.Height - 6)
            throw new InvalidOperationException("Scrolling settings moved the save/cancel buttons out of view.");
        foreach (var field in storagePaths)
        {
            var row = ((TableLayoutPanel)field.Parent!).GetRow(field);
            var browse = ((TableLayoutPanel)field.Parent!).GetControlFromPosition(2, row)!;
            if (field.Right > browse.Left || field.Width < 200)
                throw new InvalidOperationException("Storage path fields overlap the browse buttons.");
        }
        using var storageImage = new Bitmap(settingsForm.Width, settingsForm.Height);
        settingsForm.DrawToBitmap(storageImage, new Rectangle(Point.Empty, storageImage.Size));
        storageImage.Save(Path.Combine(output, "settings-storage.png"));
        var backupGroup = settingsForm.Controls.Find("SettingsBackup", true).Single();
        scrollArea.ScrollControlIntoView(backupGroup);
        Application.DoEvents();
        if (backupGroup.Controls.Find("ExportSettings", true).Length != 1 || backupGroup.Controls.Find("ImportSettings", true).Length != 1)
            throw new InvalidOperationException("Backup and restore controls are missing.");
        using var backupImage = new Bitmap(settingsForm.Width, settingsForm.Height);
        settingsForm.DrawToBitmap(backupImage, new Rectangle(Point.Empty, backupImage.Size));
        backupImage.Save(Path.Combine(output, "settings-backup.png"));
    }

    private static IEnumerable<Control> AllControls(Control parent)
    {
        foreach (Control child in parent.Controls)
        {
            yield return child;
            foreach (var descendant in AllControls(child)) yield return descendant;
        }
    }

    private static void Save(ToolbarForm toolbar, string output, string label)
    {
        using var bitmap = new Bitmap(toolbar.Width, toolbar.Height);
        toolbar.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
        bitmap.Save(Path.Combine(output, label + ".png"));
        File.WriteAllLines(Path.Combine(output, label + ".txt"),
            [ $"Form: {toolbar.Bounds}", .. toolbar.Controls.Cast<Control>()
                .Where(control => control.Visible)
                .OrderBy(control => control.Left)
                .Select(control => $"{control.Text}: {control.Bounds}; font {control.Font.Size} {control.Font.Unit}") ]);
    }

    private static void AssertSameImage(string output, string left, string right)
    {
        if (!File.ReadAllBytes(Path.Combine(output, left + ".png"))
            .SequenceEqual(File.ReadAllBytes(Path.Combine(output, right + ".png"))))
            throw new InvalidOperationException($"Toolbar rendering differs: {left} versus {right}");
    }
}
