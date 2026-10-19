using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace HomeCamMonitor;

// Render the original artwork once at the current display size, with enough
// breathing room and a checkerboard that makes transparent variants visible.
internal sealed class LogoPreview : PictureBox
{
    internal bool ShowWhiteFrame { get; set; }

    internal LogoPreview()
    {
        Name = "LogoVariantPreview";
        Size = new Size(56, 56);
        Margin = new Padding(8, 0, 3, 0);
        DoubleBuffered = true;
        SetStyle(ControlStyles.ResizeRedraw, true);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(BackColor);
        if (Image is not { } image) return;

        var inset = Math.Max(1, (int)Math.Round(8 * DeviceDpi / 96f));
        var available = Math.Min(ClientSize.Width, ClientSize.Height) - 2 * inset;
        if (available <= 0) return;
        var scale = Math.Min((float)available / image.Width, (float)available / image.Height);
        var width = Math.Max(1, (int)Math.Round(image.Width * scale));
        var height = Math.Max(1, (int)Math.Round(image.Height * scale));
        var bounds = new Rectangle((ClientSize.Width - width) / 2, (ClientSize.Height - height) / 2, width, height);

        if (ShowWhiteFrame)
        {
            // Approximately 1 mm, scaled with the Windows display DPI.
            var thickness = Math.Max(1, (int)Math.Round(DeviceDpi / 25.4f));
            var frame = Rectangle.Inflate(bounds, thickness, thickness);
            e.Graphics.FillRectangle(Brushes.White, frame);
        }

        var tileSize = Math.Max(1, (int)Math.Round(5 * DeviceDpi / 96f));
        using var light = new SolidBrush(Color.FromArgb(144, 144, 144));
        using var dark = new SolidBrush(Color.FromArgb(112, 112, 112));
        for (var y = bounds.Top; y < bounds.Bottom; y += tileSize)
            for (var x = bounds.Left; x < bounds.Right; x += tileSize)
                e.Graphics.FillRectangle(((x - bounds.Left) / tileSize + (y - bounds.Top) / tileSize) % 2 == 0 ? light : dark,
                    x, y, Math.Min(tileSize, bounds.Right - x), Math.Min(tileSize, bounds.Bottom - y));

        var state = e.Graphics.Save();
        try
        {
            e.Graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            e.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
            using var attributes = new ImageAttributes();
            attributes.SetWrapMode(WrapMode.TileFlipXY);
            e.Graphics.DrawImage(image, bounds, 0, 0, image.Width, image.Height, GraphicsUnit.Pixel, attributes);
        }
        finally { e.Graphics.Restore(state); }
    }
}
