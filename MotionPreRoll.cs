#if BETA
using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace HomeCamMonitor;

// Opt-in, per-camera rolling buffer. Only finalized segments are ever read.
internal sealed class MotionPreRoll : IDisposable
{
    private readonly string folder = Path.Combine(Path.GetTempPath(), "HomeCamMonitor-PreRoll", Guid.NewGuid().ToString("N"));
    private readonly string streamUrl;
    private readonly CancellationTokenSource stop = new();
    private readonly HashSet<Process> exports = [];
    private Process? recorder;
    internal sealed record Segment(string Path, DateTime StartUtc, DateTime EndUtc);

    internal MotionPreRoll(string streamUrl)
    {
        this.streamUrl = streamUrl;
        Directory.CreateDirectory(folder);
        _ = MaintainAsync();
    }

    private static ProcessStartInfo Command(IEnumerable<string> arguments)
    {
        var start = new ProcessStartInfo(Path.Combine(AppContext.BaseDirectory, "ffmpeg.exe"))
        { UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true };
        foreach (var arg in new[] { "-nostdin", "-hide_banner", "-loglevel", "error" }.Concat(arguments)) start.ArgumentList.Add(arg);
        return start;
    }

    private async Task MaintainAsync()
    {
        while (!stop.IsCancellationRequested)
        {
            try
            {
                foreach (var file in Directory.GetFiles(folder)) File.Delete(file);
                var args = new List<string>();
                if (Uri.TryCreate(streamUrl, UriKind.Absolute, out var uri) && uri.Scheme == "rtsp") args.AddRange(["-rtsp_transport", "tcp", "-rw_timeout", "12000000"]);
                if (File.Exists(streamUrl)) args.AddRange(["-re", "-stream_loop", "-1"]);
                args.AddRange(["-i", streamUrl, "-map", "0:v:0", "-an", "-c:v", "mpeg4", "-q:v", "2",
                    "-pix_fmt", "yuv420p", "-r", "15", "-g", "15", 
                    "-f", "segment", "-segment_time", "1", "-reset_timestamps", "1", "-segment_list", Path.Combine(folder, "segments.csv"),
                    "-segment_list_type", "csv", "-segment_list_size", "20", "-y", Path.Combine(folder, "%09d.mkv")]);
                recorder = Process.Start(Command(args)) ?? throw new IOException("Vorlauf-Puffer konnte nicht starten.");
                var errors = recorder.StandardError.ReadToEndAsync();
                while (!stop.IsCancellationRequested && !recorder.HasExited)
                {
                    foreach (var segment in ReadSegments().Where(s => s.EndUtc < DateTime.UtcNow.AddSeconds(-15)))
                        try { File.Delete(segment.Path); } catch (IOException) { }
                    // Prune segments no longer present in the rolling manifest as well.
                    foreach (var file in Directory.GetFiles(folder, "*.mkv").Where(f => File.GetLastWriteTimeUtc(f) < DateTime.UtcNow.AddSeconds(-30)))
                        try { File.Delete(file); } catch (IOException) { }
                    await Task.Delay(500, stop.Token);
                }
                if (!recorder.HasExited) recorder.Kill(true);
                await recorder.WaitForExitAsync();
                await errors;
            }
            catch (OperationCanceledException) { break; }
            catch (Exception) when (!stop.IsCancellationRequested) { }
            finally
            {
                try { if (recorder is not null && !recorder.HasExited) recorder.Kill(true); } catch { }
                recorder?.Dispose(); recorder = null;
            }
            try { await Task.Delay(2000, stop.Token); } catch (OperationCanceledException) { break; }
        }
        try { Directory.Delete(folder, true); } catch { }
    }

    internal static Segment? ParseSegment(string line, string folder)
    {
        var parts = line.Split(',');
        if (parts.Length != 3 || !double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var start) ||
            !double.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var end) || end <= start) return null;
        var path = Path.Combine(folder, Path.GetFileName(parts[0].Trim('"')));
        if (!File.Exists(path)) return null;
        var finished = File.GetLastWriteTimeUtc(path);
        return new Segment(path, finished.AddSeconds(-(end - start)), finished);
    }

    private List<Segment> ReadSegments()
    {
        try
        {
            using var file = new FileStream(Path.Combine(folder, "segments.csv"), FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var reader = new StreamReader(file);
            var list = new List<Segment>();
            while (reader.ReadLine() is { } line)
                if (ParseSegment(line, folder) is { } segment) list.Add(segment);
            return list.OrderBy(s => s.StartUtc).ToList();
        }
        catch (IOException) { return []; }
        catch (UnauthorizedAccessException) { return []; }
    }

    internal static Segment? SnapshotSegment(IEnumerable<Segment> segments, DateTime target) =>
        segments.Where(s => s.StartUtc <= target && s.EndUtc >= target).OrderBy(s => s.StartUtc).FirstOrDefault();

    // False means buffer is still warming up: the caller uses normal live capture.
    internal async Task<bool> CaptureAsync(DateTime triggeredUtc, int before, int after, string output, bool snapshot)
    {
        var target = triggeredUtc.AddSeconds(-before);
        var available = ReadSegments();
        var chosen = SnapshotSegment(available, target);
        if (chosen is null) return false;
        var work = Path.Combine(Path.GetTempPath(), "HomeCamMonitor-PreRoll", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(work);
        try
        {
            if (snapshot)
            {
                var copy = Path.Combine(work, "snapshot.mkv");
                File.Copy(chosen.Path, copy);
                await ExportAsync(["-ss", Seconds((target - chosen.StartUtc).TotalSeconds), "-i", copy,
                    "-frames:v", "1", "-an", "-y", output]);
            }
            else
            {
                var end = triggeredUtc.AddSeconds(after);
                var saved = new SortedDictionary<DateTime, string>();
                var deadline = end.AddSeconds(15);
                var latest = DateTime.MinValue;
                while (latest < end)
                {
                    stop.Token.ThrowIfCancellationRequested();
                    foreach (var segment in ReadSegments().Where(s => s.EndUtc >= chosen.StartUtc && s.StartUtc <= end))
                    {
                        if (saved.ContainsKey(segment.StartUtc)) continue;
                        var copy = Path.Combine(work, $"{saved.Count:D5}.mkv");
                        File.Copy(segment.Path, copy);
                        saved.Add(segment.StartUtc, copy);
                        latest = segment.EndUtc > latest ? segment.EndUtc : latest;
                    }
                    if (DateTime.UtcNow > deadline) throw new IOException("Vorlauf-Aufnahme: Stream liefert keine weiteren Bilder.");
                    if (latest < end) await Task.Delay(250, stop.Token);
                }
                var manifest = Path.Combine(work, "concat.txt");
                File.WriteAllLines(manifest, saved.Values.Select(f => "file '" + f.Replace("\\", "/").Replace("'", "'\\''") + "'"), new UTF8Encoding(false));
                await ExportAsync(["-f", "concat", "-safe", "0", "-i", manifest,
                    "-ss", Seconds((target - saved.Keys.First()).TotalSeconds), "-t", Seconds(before + after),
                    "-an", "-c:v", "mpeg4", "-q:v", "2", "-y", output]);
            }
            if (!File.Exists(output) || new FileInfo(output).Length < 64) throw new IOException("Vorlauf-Aufnahme ist leer.");
            return true;
        }
        catch { try { File.Delete(output); } catch { } throw; }
        finally { try { Directory.Delete(work, true); } catch { } }
    }

    private static string Seconds(double value) => Math.Max(0, value).ToString("0.###", CultureInfo.InvariantCulture);
    private async Task ExportAsync(IEnumerable<string> arguments)
    {
        stop.Token.ThrowIfCancellationRequested();
        using var process = Process.Start(Command(arguments)) ?? throw new IOException("Vorlauf-Export konnte nicht starten.");
        exports.Add(process);
        try
        {
            var error = process.StandardError.ReadToEndAsync();
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(stop.Token);
            timeout.CancelAfter(TimeSpan.FromSeconds(90));
            await process.WaitForExitAsync(timeout.Token);
            if (process.ExitCode != 0) throw new IOException("Vorlauf-Export fehlgeschlagen: " + (await error).Trim());
        }
        finally
        {
            try { if (!process.HasExited) process.Kill(true); } catch { }
            exports.Remove(process);
        }
    }

    public void Dispose()
    {
        stop.Cancel();
        try { if (recorder is not null && !recorder.HasExited) recorder.Kill(true); } catch { }
        foreach (var export in exports.ToArray()) try { if (!export.HasExited) export.Kill(true); } catch { }
    }
}
#endif
