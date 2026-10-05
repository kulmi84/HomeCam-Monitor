#if BETA
using System.Drawing.Imaging;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;

namespace HomeCamMonitor;

// Used only until the first restore of a minimized startup. DWM otherwise has
// no rendered window to cache. Normal windows keep Windows' native preview.
internal sealed class TaskbarPreview : IDisposable
{
    internal const int ThumbnailMessage = 0x0323;
    internal const int LivePreviewMessage = 0x0326;
    private readonly Form owner;
    private readonly IntPtr window;
    private readonly Func<CancellationToken, Task<Bitmap?>> capture;
    private readonly Func<Size, Bitmap> fallback;
    private readonly System.Windows.Forms.Timer timer = new() { Interval = 1000 };
    private readonly CancellationTokenSource cancellation = new();
    private Bitmap? frame;
    private bool updating;
    private bool disposed;
    private DateTime lastRequest;
    private DateTime lastUpdate;
    internal int LastSubmissionResult { get; private set; }

    internal TaskbarPreview(Form owner, Func<CancellationToken, Task<Bitmap?>> capture, Func<Size, Bitmap> fallback)
    {
        this.owner = owner; window = owner.Handle;
        this.capture = capture; this.fallback = fallback;
        var enabled = 1;
        NativeMethods.DwmSetWindowAttribute(window, 7, ref enabled, sizeof(int)); // FORCE_ICONIC_REPRESENTATION
        NativeMethods.DwmSetWindowAttribute(window, 10, ref enabled, sizeof(int)); // HAS_ICONIC_BITMAP
        timer.Tick += async (_, _) =>
        {
            if (DateTime.UtcNow - lastRequest > TimeSpan.FromSeconds(3)) { timer.Stop(); return; }
            await UpdateAsync();
        };
    }

    internal bool HandleMessage(ref Message message)
    {
        if (disposed || message.Msg is not (ThumbnailMessage or LivePreviewMessage)) return false;
        lastRequest = DateTime.UtcNow;
        var live = message.Msg == LivePreviewMessage;
        var size = live ? owner.RestoreBounds.Size : new Size(
            (int)((message.LParam.ToInt64() >> 16) & 0xffff), (int)(message.LParam.ToInt64() & 0xffff));
        if (size.Width > 0 && size.Height > 0)
        {
            // Reply synchronously; waiting for IPC here makes Windows time out.
            using var source = frame is null ? fallback(new Size(480, 270)) : null;
            using var bitmap = Scale(frame ?? source!, size);
            var handle = bitmap.GetHbitmap(Color.Black);
            try
            {
                if (live) LastSubmissionResult = DwmSetIconicLivePreviewBitmap(window, handle, IntPtr.Zero, 0);
                else LastSubmissionResult = DwmSetIconicThumbnail(window, handle, 0);
            }
            finally { NativeMethods.DeleteObject(handle); }
        }
        message.Result = IntPtr.Zero;
        timer.Start();
        _ = UpdateAsync();
        return true;
    }

    private async Task UpdateAsync()
    {
        if (updating || disposed || DateTime.UtcNow - lastUpdate < TimeSpan.FromSeconds(1)) return;
        lastUpdate = DateTime.UtcNow;
        updating = true;
        try
        {
            var next = await capture(cancellation.Token);
            if (disposed) { next?.Dispose(); return; }
            frame?.Dispose(); frame = next;
            DwmInvalidateIconicBitmaps(window);
        }
        catch (OperationCanceledException) { }
        catch (IOException) { }
        catch (TimeoutException) { }
        catch { /* Preview failure must never interrupt the camera or the UI. */ }
        finally { updating = false; }
    }

    internal static Bitmap Scale(Image source, Size maximum)
    {
        var ratio = Math.Min(maximum.Width / (double)source.Width, maximum.Height / (double)source.Height);
        var result = new Bitmap(Math.Max(1, (int)(source.Width * ratio)), Math.Max(1, (int)(source.Height * ratio)), PixelFormat.Format32bppArgb);
        using var graphics = Graphics.FromImage(result);
        graphics.Clear(Color.Black);
        graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
        graphics.DrawImage(source, new Rectangle(Point.Empty, result.Size));
        return result;
    }

    internal static async Task<Bitmap?> CaptureFrameAsync(string pipeName, CancellationToken cancellationToken)
    {
        var path = Path.Combine(Path.GetTempPath(), $"HomeCam-Preview-{Guid.NewGuid():N}.jpg");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(2000);
        try
        {
            using var pipe = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
            await pipe.ConnectAsync(timeout.Token);
            using var writer = new StreamWriter(pipe, new UTF8Encoding(false), 1024, true) { AutoFlush = true };
            using var reader = new StreamReader(pipe, Encoding.UTF8, false, 1024, true);
            await writer.WriteLineAsync(JsonSerializer.Serialize(new { command = new object[] { "screenshot-to-file", path, "video" }, request_id = 17 }));
            while (await reader.ReadLineAsync(timeout.Token) is { } line)
            {
                using var response = JsonDocument.Parse(line);
                if (!response.RootElement.TryGetProperty("request_id", out var id) || id.GetInt32() != 17) continue;
                if (!response.RootElement.TryGetProperty("error", out var error) || error.GetString() != "success") return null;
                using var image = Image.FromFile(path);
                return Scale(image, new Size(800, 450));
            }
            return null;
        }
        finally { try { File.Delete(path); } catch (IOException) { } }
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true; cancellation.Cancel(); timer.Dispose(); frame?.Dispose(); frame = null;
        var disabled = 0;
        NativeMethods.DwmSetWindowAttribute(window, 7, ref disabled, sizeof(int));
        NativeMethods.DwmSetWindowAttribute(window, 10, ref disabled, sizeof(int));
        DwmInvalidateIconicBitmaps(window);
        cancellation.Dispose();
    }

    [DllImport("dwmapi.dll")] private static extern int DwmSetIconicThumbnail(IntPtr window, IntPtr bitmap, uint flags);
    [DllImport("dwmapi.dll")] private static extern int DwmSetIconicLivePreviewBitmap(IntPtr window, IntPtr bitmap, IntPtr offset, uint flags);
    [DllImport("dwmapi.dll")] private static extern int DwmInvalidateIconicBitmaps(IntPtr window);
}
#endif
