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

        using var settingsForm = new SettingsForm(new Settings
        {
            Cameras = [new CameraEntry { Name = "Einfahrt", StreamUrl = "rtsp://127.0.0.1:8554/Einfahrt" }]
        });
        settingsForm.Show();
        Application.DoEvents();
        var cameraTable = AllControls(settingsForm).OfType<DataGridView>().Single();
        var cameraHint = settingsForm.Controls.Find("CameraHint", true).Single();
        var options = settingsForm.Controls.Find("Options", true).Single();
        var startup = settingsForm.Controls.Find("StartupOptions", true).Single();
        var generalGroup = AllControls(settingsForm).OfType<GroupBox>()
            .Single(group => group.Text == "Allgemeine Einstellungen");
        var motionGroup = AllControls(settingsForm).OfType<GroupBox>()
            .Single(group => group.Text == "Bewegung pro Kamera und Home Assistant");
        static int TopOnScreen(Control control) => control.PointToScreen(Point.Empty).Y;
        static int BottomOnScreen(Control control) => TopOnScreen(control) + control.Height;
        if (BottomOnScreen(cameraTable) > TopOnScreen(cameraHint) ||
            BottomOnScreen(cameraHint) > TopOnScreen(generalGroup) ||
            BottomOnScreen(options) > TopOnScreen(startup) ||
            BottomOnScreen(generalGroup) > TopOnScreen(motionGroup))
            throw new InvalidOperationException("Settings rows overlap or the camera hint is hidden");
        var selectedCamera = AllControls(motionGroup).OfType<Label>()
            .Single(label => label.Text == "Ausgewählte Kamera:");
        var action = AllControls(motionGroup).OfType<ComboBox>()
            .Single(combo => combo.Items.Contains("Snapshot + Videoaufnahme"));
        if (BottomOnScreen(selectedCamera) > TopOnScreen(action))
            throw new InvalidOperationException("Motion action is not below the selected camera");
        var seconds = AllControls(settingsForm)
            .OfType<Label>().Single(label => label.Text == "Sekunden");
        var duration = seconds.Parent!.Controls.OfType<NumericUpDown>().Single();
        if (seconds.Top > duration.Bottom || seconds.Bottom < duration.Top)
            throw new InvalidOperationException("Seconds label wrapped away from the duration field");
        var buttons = AllControls(settingsForm).OfType<FlowLayoutPanel>()
            .Single(panel => panel.Controls.OfType<Button>().Any(button => button.Text == "Speichern"));
        if (settingsForm.ClientSize.Height - buttons.Bottom > 25)
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
