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
}
