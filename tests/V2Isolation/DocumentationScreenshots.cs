using HomeCamMonitor;
using System.Drawing;
namespace V2Isolation;
internal static partial class Check
{
    private static void CaptureDocumentationScreenshots()
    {
        // Documentation uses fabricated cameras only. Never load user settings.
        var settings = new Settings { MotionRetentionDays = 7, DirectHomeAssistantEnabled = true,
            ReolinkHelperPath = @"C:\Program Files\HomeCamMonitor-V2-Beta\tools\neolink\neolink.exe",
            HomeAssistantUrl = "https://homeassistant.example:8123", HomeAssistantToken = "DEMO-NOT-A-REAL-TOKEN",
            Cameras = [
                new() { Name = "Einfahrt", StreamUrl = "rtsp://homeassistant.example:8554/einfahrt", MotionEnabled = true, MotionEntityId = "binary_sensor.einfahrt_bewegung", PersonEntityId = "binary_sensor.einfahrt_person", PersonEnabled = true, MotionAction = "Snapshot" },
                new() { Name = "Garten", StreamUrl = "rtsp://127.0.0.1:18554/garten", MotionEnabled = true, Reolink = new ReolinkCamera { Enabled = true, Uid = "DEMO0123456789AB", Username = "demo" } },
                new() { Name = "Garage", StreamUrl = "rtsp://camera.example:554/stream", MotionEnabled = true, MotionAction = "Both", Direct = new DirectCamera { Enabled = true, RtspUrl = "rtsp://camera.example:554/stream" }, LocalMotion = new LocalMotionSettings { Enabled = true, Zones = [new MotionZone(.1,.2,.6,.6)] } }
            ] };
        var output = Path.Combine(Environment.CurrentDirectory, "docs", "screenshots"); Directory.CreateDirectory(output);
        using var host = new RenderHost { Opacity = 0, ShowInTaskbar = false, StartPosition = FormStartPosition.Manual, Location = new Point(-30000,-30000), ClientSize = new Size(1160,900) };
        using var form = new SettingsForm(settings, SettingsLocation.DefaultFolder) { TopLevel = false, FormBorderStyle = FormBorderStyle.None, Dock = DockStyle.Fill };
        host.Controls.Add(form); host.Show(); form.Show(); Pump(200);
        var grid = FindGrid(form);
        var scroll = (ModernSettingsScrollArea)form.Controls.Find("SettingsScrollArea",true).Single();
        var connections = (ConnectionSettingsSection)form.Controls.Find("ConnectionSettingsSection",true).Single();
        var details = (MotionDetailsSection)form.Controls.Find("MotionDetailsSection",true).Single();
        void Save(string name, int offset = 0)
        {
            scroll.AutoScrollPosition = new Point(0,offset); Pump(100); using var bitmap = new Bitmap(form.Width,form.Height);
            form.DrawToBitmap(bitmap,new Rectangle(Point.Empty,bitmap.Size)); bitmap.Save(Path.Combine(output,name));
        }
        grid.CurrentCell = grid.Rows[0].Cells[0]; Save("v2-beta37-overview.png");
        connections.Expanded = true; Save("v2-beta37-homeassistant.png"); connections.Expanded = false;
        grid.CurrentCell = grid.Rows[1].Cells[0]; connections.Expanded = true; Save("v2-beta37-reolink.png"); connections.Expanded = false;
        grid.CurrentCell = grid.Rows[2].Cells[0]; details.Expanded = true;
        // Synthetic preview, generated entirely in memory, no network/camera access.
        var pixels = new byte[320*180];
        for (var y=0;y<180;y++) for(var x=0;x<320;x++) pixels[y*320+x] = (byte)(35 + x/4 + y/6);
        var canvas = (MotionZoneCanvas)form.Controls.Find("LocalMotionZones",true).Single(); canvas.SetFrame(pixels);
        Save("v2-beta37-local-motion.png",180);
        Console.WriteLine("Four Beta37 screenshots captured with synthetic data and preview; no user settings or camera access.");
    }
}
