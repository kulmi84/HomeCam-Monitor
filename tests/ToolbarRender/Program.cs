using System.Drawing;
using System.Windows.Forms;
using HomeCamMonitor;

namespace ToolbarRender;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
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
        settingsForm.Show();
        Application.DoEvents();
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
        var actionLabel = AllControls(motionGroup).OfType<Label>()
            .Single(label => label.Text == "Aufzeichnung bei Bewegung:");
        var motionFields = (TableLayoutPanel)actionLabel.Parent!;
        if (motionFields.GetPositionFromControl(actionLabel).Column != 0 ||
            motionFields.GetPositionFromControl(actionLabel).Row !=
            motionFields.GetPositionFromControl(actionLabel.Parent.Controls.OfType<FlowLayoutPanel>()
                .Single(panel => panel.Controls.OfType<ComboBox>().Any(combo => combo.Items.Contains("Snapshot + Videoaufnahme")))).Row)
            throw new InvalidOperationException("Recording label must start the camera action row");
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
        if (BottomOnScreen(selectedCamera) > TopOnScreen(action))
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
        if (buttons.Bottom <= settingsForm.ClientSize.Height && settingsForm.ClientSize.Height - buttons.Bottom > 25)
            throw new InvalidOperationException("Unused space remains below the settings buttons");
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
