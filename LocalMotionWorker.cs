#if V2_BETA
using System.Diagnostics;
using System.Threading.Channels;
namespace HomeCamMonitor;
internal sealed class LocalMotionWorker : IDisposable
{
    internal const string ImageFilter = "scale=320:180:force_original_aspect_ratio=decrease,pad=320:180:(ow-iw)/2:(oh-ih)/2,format=gray";
    private readonly CameraEntry camera;
    private readonly Action<MotionAnalysis> report;
    private readonly CancellationTokenSource cancel = new();
    internal Task Completion { get; }
    internal LocalMotionWorker(CameraEntry camera, Action<MotionAnalysis> report, string? executable = null)
    { this.camera = camera; this.report = report; Completion = Task.Run(() => Run(executable)); }
    internal static ProcessStartInfo StartInfo(string url, string? executable = null, bool preview = false)
    {
        var start = new ProcessStartInfo(executable ?? Path.Combine(AppContext.BaseDirectory, "ffmpeg.exe"))
        { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var arg in new[] { "-nostdin", "-hide_banner", "-loglevel", "error", "-threads", "1", "-filter_threads", "1" }) start.ArgumentList.Add(arg);
        if (Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme is "rtsp" or "rtsps")
            foreach (var arg in new[] { "-rtsp_transport", "tcp", "-fflags", "+nobuffer" }) start.ArgumentList.Add(arg);
        if (Uri.TryCreate(url, UriKind.Absolute, out var file) && file.IsFile) start.ArgumentList.Add("-re");
        foreach (var arg in new[] { "-i", url, "-an", "-sn", "-dn", "-vf", preview ? ImageFilter : "fps=5," + ImageFilter }) start.ArgumentList.Add(arg);
        if (preview) { start.ArgumentList.Add("-frames:v"); start.ArgumentList.Add("1"); }
        foreach (var arg in new[] { "-pix_fmt", "gray", "-threads", "1", "-f", "rawvideo", "pipe:1" }) start.ArgumentList.Add(arg);
        return start;
    }
    private async Task Run(string? executable)
    {
        while (!cancel.IsCancellationRequested)
        {
            try
            {
                camera.LocalMotion.Validate();
                using var model = new LocalMotionModel(camera.LocalMotion);
                using var session = CancellationTokenSource.CreateLinkedTokenSource(cancel.Token);
                using var process = Process.Start(StartInfo(camera.StreamUrl, executable)) ?? throw new IOException();
                using var kill = session.Token.Register(() => { try { if (!process.HasExited) process.Kill(true); } catch (InvalidOperationException) { } });
                var errors = Discard(process.StandardError);
                var latest = Channel.CreateBounded<(byte[] Pixels, TimeSpan Time)>(new BoundedChannelOptions(1) { FullMode = BoundedChannelFullMode.DropOldest, SingleWriter = true, SingleReader = true });
                var clock = Stopwatch.StartNew();
                async Task Read()
                {
                    try
                    {
                        while (!session.IsCancellationRequested)
                        {
                            var bytes = new byte[LocalMotionModel.FrameBytes];
                            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(session.Token); deadline.CancelAfter(TimeSpan.FromSeconds(25));
                            await process.StandardOutput.BaseStream.ReadExactlyAsync(bytes, deadline.Token).ConfigureAwait(false);
                            latest.Writer.TryWrite((bytes, clock.Elapsed));
                        }
                    }
                    catch (Exception e) when (e is IOException or OperationCanceledException or ObjectDisposedException) { }
                    finally { latest.Writer.TryComplete(); }
                }
                var reading = Read();
                try
                {
                    var lastReport = TimeSpan.MinValue; var wasActive = false;
                    await foreach (var image in latest.Reader.ReadAllAsync(cancel.Token).ConfigureAwait(false))
                    {
                        if (clock.Elapsed - image.Time > TimeSpan.FromSeconds(1)) continue;
                        var result = model.Analyze(image.Pixels, image.Time);
                        LocalMotionStatus.Set(camera.LocalMotion.Id, result.WarmingUp ? "Hintergrund wird eingelernt …" : result.Active ? "Bewegung erkannt" : "Lokale Analyse aktiv – keine Bewegung");
                        if (result.Triggered || result.Active != wasActive || result.Active && image.Time - lastReport >= TimeSpan.FromSeconds(1))
                        { report(result); lastReport = image.Time; wasActive = result.Active; }
                    }
                }
                finally
                {
                    session.Cancel();
                    if (!process.HasExited) { try { process.Kill(true); } catch (InvalidOperationException) { } }
                    await process.WaitForExitAsync().ConfigureAwait(false); await reading.ConfigureAwait(false); await errors.ConfigureAwait(false);
                }
                if (!cancel.IsCancellationRequested) LocalMotionStatus.Set(camera.LocalMotion.Id, "Kein aktuelles Videobild – Verbindung wird erneut versucht.");
            }
            catch (OperationCanceledException) when (cancel.IsCancellationRequested) { break; }
            catch (Exception e) when (e is OpenCvSharp.OpenCVException or DllNotFoundException or TypeInitializationException or BadImageFormatException)
            { LocalMotionStatus.Set(camera.LocalMotion.Id, "OpenCV konnte nicht geladen werden. Installation und Windows-Medienkomponenten prüfen."); break; }
            catch (Exception e) when (e is IOException or ArgumentException or System.ComponentModel.Win32Exception or InvalidOperationException)
            { LocalMotionStatus.Set(camera.LocalMotion.Id, "Analyse nicht verfügbar. Stream und Einstellungen prüfen."); }
            if (!cancel.IsCancellationRequested)
            {
                report(new MotionAnalysis(false, false, false, 0));
                try { await Task.Delay(2000, cancel.Token).ConfigureAwait(false); } catch (OperationCanceledException) { break; }
            }
        }
    }
    private static async Task Discard(StreamReader reader)
    {
        var buffer = new char[2048];
        try { while (await reader.ReadAsync(buffer).ConfigureAwait(false) > 0) { } } catch (IOException) { }
    }
    internal static async Task<byte[]> Preview(string url, CancellationToken token, string? executable = null)
    {
        using var process = Process.Start(StartInfo(url, executable, true)) ?? throw new IOException();
        using var kill = token.Register(() => { try { if (!process.HasExited) process.Kill(true); } catch (InvalidOperationException) { } });
        var errors = Discard(process.StandardError);
        try { var bytes = new byte[LocalMotionModel.FrameBytes]; await process.StandardOutput.BaseStream.ReadExactlyAsync(bytes, token).ConfigureAwait(false); return bytes; }
        finally { if (!process.HasExited) { try { process.Kill(true); } catch (InvalidOperationException) { } } await process.WaitForExitAsync().ConfigureAwait(false); await errors.ConfigureAwait(false); }
    }
    public void Dispose() => cancel.Cancel();
}
internal sealed partial class MonitorForm
{
    private readonly List<LocalMotionWorker> localMotionWorkers = [];
    private int localMotionGeneration;
    private void StopLocalMotion()
    {
        localMotionGeneration++;
        foreach (var worker in localMotionWorkers) worker.Dispose();
        localMotionWorkers.Clear();
    }
    private void ConfigureLocalMotion()
    {
        StopLocalMotion(); var generation = localMotionGeneration;
        foreach (var camera in settings.Cameras.Where(c => c.IsAvailable && c.MotionEnabled && c.LocalMotion.Enabled))
        {
            var worker = new LocalMotionWorker(camera, result =>
            {
                if (closing || !IsHandleCreated) return;
                try { BeginInvoke(new Action(() =>
                {
                    if (closing || generation != localMotionGeneration) return;
                    if (result.Triggered) HandleMotion(camera.Name, local: true);
                    else if (result.Active) ShowMotionIndicator(cameraIndex: settings.Cameras.IndexOf(camera));
                })); } catch (InvalidOperationException) { }
            });
            localMotionWorkers.Add(worker);
        }
    }
}
#endif
