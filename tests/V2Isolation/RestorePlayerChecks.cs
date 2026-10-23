using HomeCamMonitor;
using System.Diagnostics;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;

namespace V2Isolation;
internal static partial class Check
{
    private static void PumpFor(int milliseconds)
    {
        var watch = Stopwatch.StartNew();
        while (watch.ElapsedMilliseconds < milliseconds) { Application.DoEvents(); Thread.Sleep(10); }
    }

    private static async Task<Size> ReadPlayerDimensionsAsync(string name)
    {
        using var timeout = new CancellationTokenSource(3000);
        using var pipe = new NamedPipeClientStream(".", name, PipeDirection.InOut, PipeOptions.Asynchronous);
        await pipe.ConnectAsync(timeout.Token).ConfigureAwait(false);
        using var writer = new StreamWriter(pipe, new UTF8Encoding(false), 1024, true) { AutoFlush = true };
        using var reader = new StreamReader(pipe, Encoding.UTF8, false, 1024, true);
        await writer.WriteLineAsync("{\"command\":[\"get_property\",\"osd-dimensions\"],\"request_id\":17}").ConfigureAwait(false);
        while (await reader.ReadLineAsync(timeout.Token).ConfigureAwait(false) is { } line)
        {
            using var document = JsonDocument.Parse(line);
            var root = document.RootElement;
            if (!root.TryGetProperty("request_id", out var id) || id.GetInt32() != 17) continue;
            if (root.GetProperty("error").GetString() != "success") return Size.Empty;
            var data = root.GetProperty("data");
            return new Size(data.GetProperty("w").GetInt32(), data.GetProperty("h").GetInt32());
        }
        return Size.Empty;
    }

    private static Size ReadPlayerDimensions(string name)
    {
        var task = ReadPlayerDimensionsAsync(name);
        while (!task.IsCompleted) PumpFor(10);
        try { return task.GetAwaiter().GetResult(); }
        catch (OperationCanceledException) { return Size.Empty; }
        catch (TimeoutException) { return Size.Empty; }
    }

    private static void TestRestorePlayer(string executable, string fixture, string output)
    {
        Directory.CreateDirectory(output);
        var trace = new List<string>();
        var initial = SettingsStore.CreateForNewInstallation();
        initial.MotionRetentionDays = 0; initial.Width = 640; initial.Height = 360;
        using var monitor = new MonitorForm(initial);
        const System.Reflection.BindingFlags hidden = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        var video = (Panel)typeof(MonitorForm).GetField("video", hidden)!.GetValue(monitor)!;
        ShowVideoTestWindow(monitor.Handle, 4);
        video.Show(); video.BringToFront();
        var name = "HomeCam-Restore-Fixture-" + Guid.NewGuid().ToString("N");
        var start = new ProcessStartInfo(Path.GetFullPath(executable)) { UseShellExecute = false, CreateNoWindow = true };
        foreach (var argument in new[] { $"--wid={video.Handle.ToInt64()}", "--no-terminal", "--really-quiet", "--no-audio", "--no-osc", "--no-border",
            "--profile=low-latency", "--cache=no", "--hwdec=auto-safe", "--vo=gpu-next", "--gpu-api=d3d11", "--loop-file=inf",
            "--input-ipc-server=" + @"\\.\pipe\" + name, "--log-file=" + Path.GetFullPath(Path.Combine(output, "mpv-restore.log")), Path.GetFullPath(fixture) })
            start.ArgumentList.Add(argument);
        using var player = Process.Start(start) ?? throw new Exception("Fixture player did not start.");
        try
        {
            var deadline = Stopwatch.StartNew();
            Size rendered;
            do { PumpFor(100); rendered = ReadPlayerDimensions(name); } while (rendered != video.ClientSize && deadline.Elapsed < TimeSpan.FromSeconds(10));
            if (rendered != video.ClientSize) throw new Exception($"Fixture did not reach native target size: {rendered} / {video.ClientSize}");
            var target = video.ClientSize;
            for (var cycle = 0; cycle < 4; cycle++)
            {
                ShowVideoTestWindow(monitor.Handle, 6); PumpFor(150);
                // Also exercise a pending deferred layout while minimized.
                typeof(MonitorForm).GetMethod("AlignCameraSurfaces", hidden)!.Invoke(monitor, null);
                NativeMethods.PrepareVideoChildren(video.Handle);
                rendered = ReadPlayerDimensions(name);
                trace.Add($"cycle={cycle} minimized host={video.ClientSize} mpv={rendered}");
                if (video.ClientSize != target || rendered != target) throw new Exception(trace[^1]);
                if (cycle % 2 == 0) ShowVideoTestWindow(monitor.Handle, 9); // SW_RESTORE, as with a taskbar restore.
                else typeof(MonitorForm).GetMethod("ForceToForeground", hidden)!.Invoke(monitor, null);
                PumpFor(150);
                rendered = ReadPlayerDimensions(name);
                trace.Add($"cycle={cycle} restored host={video.ClientSize} mpv={rendered}");
                if (video.ClientSize != target || rendered != target || monitor.WindowState != FormWindowState.Normal) throw new Exception(trace[^1]);
            }
            Console.WriteLine(string.Join(Environment.NewLine, trace));
            Console.WriteLine("PASS: actual mpv/D3D11 render target retained over manual and motion restore, including deferred minimized layout.");
        }
        finally
        {
            File.WriteAllLines(Path.Combine(output, "restore-dimensions.txt"), trace);
            if (!player.HasExited) { player.Kill(true); player.WaitForExit(3000); }
            ShowVideoTestWindow(monitor.Handle, 0);
        }
    }

    private static void TestInitialPlayer(string executable, string fixture)
    {
        File.Copy(Path.GetFullPath(executable), Path.Combine(AppContext.BaseDirectory, "mpv.exe"), true);
        const System.Reflection.BindingFlags hidden = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        var settings = SettingsStore.CreateForNewInstallation();
        settings.MotionRetentionDays = 0; settings.Width = 640; settings.Height = 360;
        settings.StartBehavior = "Minimized";
        settings.Cameras.Add(new CameraEntry { Name = "Local fixture", StreamUrl = new Uri(Path.GetFullPath(fixture)).AbsoluteUri });
        using var monitor = new MonitorForm(settings);
        var video = (Panel)typeof(MonitorForm).GetField("video", hidden)!.GetValue(monitor)!;
        var name = (string)typeof(MonitorForm).GetField("pipeName", hidden)!.GetValue(monitor)!;
        var expected = monitor.RestoreBounds.Size;
        try
        {
            ShowVideoTestWindow(monitor.Handle, 7); // First-ever show, minimized and without activation.
            typeof(MonitorForm).GetMethod("StartPlayer", hidden)!.Invoke(monitor, null);
            var watch = Stopwatch.StartNew();
            Size rendered;
            do { PumpFor(100); rendered = ReadPlayerDimensions(name); } while (rendered.IsEmpty && watch.Elapsed < TimeSpan.FromSeconds(10));
            Console.WriteLine($"Initial minimized: expected={expected}, host={video.Size}, mpv={rendered}");
            if (video.Size != expected || rendered != expected) throw new Exception("First player launch used the minimized window dimensions.");
            ShowVideoTestWindow(monitor.Handle, 9); PumpFor(150);
            rendered = ReadPlayerDimensions(name);
            if (video.Size != expected || rendered != expected) throw new Exception("First restore changed the initial render target.");
            Console.WriteLine("PASS: actual StartPlayer uses full target size before first-ever restore from minimized startup.");
        }
        finally
        {
            typeof(MonitorForm).GetMethod("StopPlayer", hidden)!.Invoke(monitor, null);
            ShowVideoTestWindow(monitor.Handle, 0);
        }
    }
}
