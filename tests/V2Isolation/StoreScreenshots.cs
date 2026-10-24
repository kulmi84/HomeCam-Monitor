using HomeCamMonitor;
using System.Collections;
using System.Reflection;

namespace V2Isolation;
internal static partial class Check
{
    private static void CaptureStoreScreenshots(string scenesPath, string output)
    {
        Directory.CreateDirectory(output);
        using var sheet = new Bitmap(scenesPath);
        var frames = Enumerable.Range(0, 4).Select(i => sheet.Clone(new Rectangle(i % 2 * sheet.Width / 2,
            i / 2 * sheet.Height / 2, sheet.Width / 2, sheet.Height / 2), System.Drawing.Imaging.PixelFormat.Format32bppArgb)).ToArray();
        var settings = new Settings { MotionDetectionEnabled = true, MotionRetentionDays = 0, MotionForegroundSeconds = 300, Width = 1920, Height = 1080,
            AlwaysOnTop = false, ShowGridCameraNames = true, Cameras = [
                new() { Name = "Einfahrt", StreamUrl = "rtsp://demo.invalid/einfahrt", Active = true, MotionEnabled = true },
                new() { Name = "Garten", StreamUrl = "rtsp://demo.invalid/garten", Active = true, MotionEnabled = true },
                new() { Name = "Eingang", StreamUrl = "rtsp://demo.invalid/eingang", Active = true, MotionEnabled = true },
                new() { Name = "Terrasse", StreamUrl = "rtsp://demo.invalid/terrasse", Active = true, MotionEnabled = true } ] };
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        using var monitor = new MonitorForm(settings);
        object Field(string name) => typeof(MonitorForm).GetField(name, flags)!.GetValue(monitor)!;
        void Set(string name, object value) => typeof(MonitorForm).GetField(name, flags)!.SetValue(monitor, value);
        void Call(string name) => typeof(MonitorForm).GetMethod(name, flags)!.Invoke(monitor, null);
        // Suppress application startup: render controls with demo frames without launching cameras or loading settings.
        var events = (System.ComponentModel.EventHandlerList)typeof(System.ComponentModel.Component).GetProperty("Events", flags)!.GetValue(monitor)!;
        var shownKey = typeof(Form).GetFields(BindingFlags.Static | BindingFlags.NonPublic).Single(f => f.Name.Contains("shown", StringComparison.OrdinalIgnoreCase)).GetValue(null)!;
        events.RemoveHandler(shownKey, events[shownKey]);
        monitor.ClientSize = new Size(1920, 1080);
        monitor.Show();
        Pump(100);
        Set("fullscreen", true);
        var video = (Panel)Field("video");
        ((Control)Field("offlinePlaceholder")).Hide();
        ((Control)Field("standbyVideo")).Hide();
        video.BackgroundImage = frames[0]; video.BackgroundImageLayout = ImageLayout.Stretch; video.Show(); video.BringToFront();
        using var toolbar = new ToolbarForm(monitor);
        toolbar.SetSizePercent(100); toolbar.CameraName = "Einfahrt";


        void Save(string name)
        {
            monitor.PerformLayout(); Pump(100);
            using var bitmap = new Bitmap(1920, 1080);
            monitor.DrawToBitmap(bitmap, new Rectangle(0, 0, 1920, 1080));
            using var bar = new Bitmap(toolbar.Width, toolbar.Height);
            using (var barGraphics = Graphics.FromImage(bar))
            {
                barGraphics.Clear(toolbar.BackColor);
                foreach (Control control in toolbar.Controls)
                {
                    if (control.Width <= 0 || control.Height <= 0 || control is not Label) continue;
                    using var item = new Bitmap(control.Width, control.Height);
                    control.DrawToBitmap(item, new Rectangle(Point.Empty, item.Size));
                    barGraphics.DrawImageUnscaled(item, control.Location);
                }
            }
            using var graphics = Graphics.FromImage(bitmap);
            if ((bool)Field("gridMode"))
            {
                foreach (var slot in ((IEnumerable)Field("gridSlots")).Cast<object>())
                {
                    var host = (Control)slot.GetType().GetProperty("Host")!.GetValue(slot)!;
                    var label = (Control)slot.GetType().GetProperty("Name")!.GetValue(slot)!;
                    using var caption = new Bitmap(label.Width, label.Height);
                    label.DrawToBitmap(caption, new Rectangle(Point.Empty, caption.Size));
                    graphics.DrawImageUnscaled(caption, host.Left + label.Left, host.Top + label.Top);
                }
            }
            if (name == "03-bild-im-bild.png")
            {
                var preview = (Form)Field("motionPreview");
                                var previewBounds = new Rectangle(monitor.PointToClient(preview.Location),preview.Size);
                var shape = NativeMethods.CreateRoundRectRgn(previewBounds.Left,previewBounds.Top,previewBounds.Right,previewBounds.Bottom,20,20);
                using var clip = Region.FromHrgn(shape); NativeMethods.DeleteObject(shape);
                var state = graphics.Save(); graphics.SetClip(clip, System.Drawing.Drawing2D.CombineMode.Replace);
                graphics.DrawImage(frames[1], previewBounds);
                graphics.Restore(state);
            }
            var barX = (1920 - bar.Width) / 2;
            var barY = 1080 - bar.Height - 10;
            var rounding = NativeMethods.CreateRoundRectRgn(0, 0, bar.Width, bar.Height, 14, 14);
            using var barClip = Region.FromHrgn(rounding); NativeMethods.DeleteObject(rounding);
            barClip.Translate(barX, barY);
            var savedGraphics = graphics.Save(); graphics.SetClip(barClip, System.Drawing.Drawing2D.CombineMode.Replace);
            using var attributes = new System.Drawing.Imaging.ImageAttributes();
            attributes.SetColorMatrix(new System.Drawing.Imaging.ColorMatrix { Matrix33 = (float)toolbar.Opacity });
            graphics.DrawImage(bar, new Rectangle(barX, barY, bar.Width, bar.Height), 0, 0, bar.Width, bar.Height, GraphicsUnit.Pixel, attributes);
            graphics.Restore(savedGraphics);
            using var framed = new Bitmap(1920,1080);
            using (var frameGraphics = Graphics.FromImage(framed))
            {
                frameGraphics.Clear(Color.FromArgb(44,44,47));
                frameGraphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                var bounds = new Rectangle(8,4,1904,1071);
                const int diameter = 18;
                using var outline = new System.Drawing.Drawing2D.GraphicsPath();
                outline.AddArc(bounds.Left,bounds.Top,diameter,diameter,180,90);
                outline.AddArc(bounds.Right-diameter,bounds.Top,diameter,diameter,270,90);
                outline.AddArc(bounds.Right-diameter,bounds.Bottom-diameter,diameter,diameter,0,90);
                outline.AddArc(bounds.Left,bounds.Bottom-diameter,diameter,diameter,90,90);
                outline.CloseFigure();
                var frameState = frameGraphics.Save();
                frameGraphics.SetClip(outline);
                frameGraphics.DrawImage(bitmap,bounds);
                frameGraphics.Restore(frameState);
                using var border = new Pen(Color.FromArgb(155,155,163),1.5f);
                frameGraphics.DrawPath(border,outline);
            }
            framed.Save(Path.Combine(output, name));
        }
        Save("01-einzelkamera.png");
        Set("gridMode", true);
        video.Hide();
        var grid = (Control)Field("cameraGrid"); grid.Show(); grid.BringToFront();
        Call("LayoutCameraGrid");
        var slots = ((IEnumerable)Field("gridSlots")).Cast<object>().ToArray();
        for (var i = 0; i < 4; i++)
        {
            var type = slots[i].GetType();
            Control Get(string n) => (Control)type.GetProperty(n)!.GetValue(slots[i])!;
            Get("Placeholder").Hide(); Get("SpareSurface").Hide();
            var surface = Get("ActiveSurface"); surface.BackgroundImage = frames[i]; surface.BackgroundImageLayout = ImageLayout.Stretch;
            surface.Show(); surface.BringToFront();
            Get("Name").Text = settings.Cameras[i].Name; Get("Name").Show(); Get("Name").BringToFront();
        }
        toolbar.SetGridMode(true); toolbar.CameraName = "4 Kameras";
        Save("02-vier-kameras.png");
        Set("gridMode", false); grid.Hide(); video.Show(); video.BringToFront(); toolbar.SetGridMode(false); toolbar.CameraName = "Einfahrt";
        typeof(MonitorForm).GetMethod("ShowMotionPreview", flags)!.Invoke(monitor, new object[] { 1 });
        ((Control)Field("motionPreviewPlaceholder")).Hide();
        var inset = (Panel)Field("motionPreviewVideo"); inset.BackgroundImage = frames[1]; inset.BackgroundImageLayout = ImageLayout.Stretch; inset.BringToFront();
        Save("03-bild-im-bild.png");
        Call("HideMotionPreview");
        settings.MotionRetentionDays = 7;
        settings.Cameras[0].MotionAction = "Snapshot";
        settings.Cameras[1].Reolink = new ReolinkCamera { Enabled = true, Uid = "DEMO0123456789AB", Username = "demo" };
        settings.Cameras[2].Direct = new DirectCamera { Enabled = true, RtspUrl = "rtsp://demo.invalid/eingang" };
        using var host = new RenderHost { Opacity = 0, ShowInTaskbar = false, StartPosition = FormStartPosition.Manual,
            Location = new Point(-30000,-30000), ClientSize = new Size(1180,900) };
        using var form = new SettingsForm(settings, SettingsLocation.DefaultFolder)
            { TopLevel = false, FormBorderStyle = FormBorderStyle.None, Dock = DockStyle.Fill };
        host.Controls.Add(form); host.Show(); form.Show(); Pump(300); form.PerformLayout();
        using var settingsPanel = new Bitmap(1180,900);
        form.DrawToBitmap(settingsPanel, new Rectangle(Point.Empty, settingsPanel.Size));
        using var settingsBitmap = new Bitmap(1920,1080);
        using (var graphics = Graphics.FromImage(settingsBitmap))
        {
            graphics.DrawImage(frames[0], new Rectangle(0,0,1920,1080));
            var x = (1920-settingsPanel.Width)/2; var y=(1080-settingsPanel.Height)/2;
            var handle=NativeMethods.CreateRoundRectRgn(x,y,x+settingsPanel.Width,y+settingsPanel.Height,16,16);
            using var clip = Region.FromHrgn(handle); NativeMethods.DeleteObject(handle);
            graphics.SetClip(clip, System.Drawing.Drawing2D.CombineMode.Replace);
            graphics.DrawImageUnscaled(settingsPanel,x,y);
        }
        settingsBitmap.Save(Path.Combine(output, "04-einstellungen.png"));
        foreach (var frame in frames) frame.Dispose();
        Console.WriteLine("Store screenshots rendered from real application controls and fictional camera frames. No user configuration or camera was read.");
    }
    private static void CaptureAboutScreenshot(string path)
    {
        using var host = new RenderHost { Opacity = 0, ShowInTaskbar = false,
            StartPosition = FormStartPosition.Manual, Location = new Point(-30000,-30000), ClientSize = new Size(540,350) };
        using var about = new AboutDialog { TopLevel = false, FormBorderStyle = FormBorderStyle.None, Dock = DockStyle.Fill };
        host.Controls.Add(about); host.Show(); about.Show(); Pump(150);
        using var bitmap = new Bitmap(540,350);
        about.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
        bitmap.Save(path);
        Console.WriteLine("About dialog rendered; external links were not opened.");
    }}
