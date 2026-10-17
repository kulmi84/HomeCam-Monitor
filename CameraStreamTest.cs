#if V2_BETA
using System.Diagnostics;
namespace HomeCamMonitor;

internal static class CameraStreamTest
{
    internal static async Task<bool> DecodeFrame(string url, CancellationToken token, string? executable = null)
    {
        token.ThrowIfCancellationRequested();
        var start = new ProcessStartInfo(executable ?? Path.Combine(AppContext.BaseDirectory, "ffmpeg.exe"))
        { UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true, RedirectStandardOutput = true };
        foreach (var arg in new[] { "-nostdin", "-hide_banner", "-loglevel", "error" }) start.ArgumentList.Add(arg);
        if (Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme is "rtsp" or "rtsps")
            foreach (var arg in new[] { "-rtsp_transport", "tcp" }) start.ArgumentList.Add(arg);
        foreach (var arg in new[] { "-i", url,
            "-frames:v", "1", "-an", "-progress", "pipe:1", "-nostats", "-f", "null", "-" }) start.ArgumentList.Add(arg);
        using var process = Process.Start(start) ?? throw new IOException("Videotest konnte nicht gestartet werden.");
        var decoded = false;
        async Task Progress()
        {
            while (await process.StandardOutput.ReadLineAsync() is { } line)
                if (line.StartsWith("frame=", StringComparison.Ordinal) && int.TryParse(line.AsSpan(6).Trim(), out var frames) && frames > 0) decoded = true;
        }
        async Task DiscardErrors()
        {
            var buffer = new char[2048];
            while (await process.StandardError.ReadAsync(buffer) > 0) { }
        }
        var output = Progress(); var errors = DiscardErrors();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token); timeout.CancelAfter(TimeSpan.FromSeconds(25));
        try { await process.WaitForExitAsync(timeout.Token); }
        catch (OperationCanceledException) when (!token.IsCancellationRequested) { return false; }
        finally
        {
            if (!process.HasExited) { try { process.Kill(true); } catch (InvalidOperationException) { } }
            await process.WaitForExitAsync(); await Task.WhenAll(output, errors);
        }
        token.ThrowIfCancellationRequested();
        return process.ExitCode == 0 && decoded;
    }
}
#endif
