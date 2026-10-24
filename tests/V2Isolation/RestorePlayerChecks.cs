using HomeCamMonitor;
using System.Diagnostics;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;

namespace V2Isolation;
internal static partial class Check
{
    private static void TestMotionPictureInPicture()
    {
        const System.Reflection.BindingFlags hidden = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        var type = typeof(MonitorForm);
        var initial = SettingsStore.CreateForNewInstallation();
        initial.MotionRetentionDays = 0;
        initial.AlwaysOnTop = true;
        initial.Cameras.Add(new CameraEntry { Name = "Main", Active = true, MotionEnabled = true });
        initial.Cameras.Add(new CameraEntry { Name = "Motion", Active = true, MotionEnabled = true });
        initial.SelectedCamera = 0;
        if (!initial.MotionPictureInPictureEnabled || !JsonSerializer.Deserialize<Settings>("{}")!.MotionPictureInPictureEnabled)
            throw new Exception("New and existing settings should enable picture in picture by default.");
        using var monitor = new MonitorForm(initial);
        type.GetField("fullscreen", hidden)!.SetValue(monitor, true);
        initial.MotionPictureInPictureEnabled = false;
        var roundTrip = JsonSerializer.Deserialize<Settings>(JsonSerializer.Serialize(initial))!;
        if (roundTrip.MotionPictureInPictureEnabled) throw new Exception("Disabled preview setting was not preserved.");
        type.GetMethod("HandleMotion", hidden)!.Invoke(monitor, new object[] { "Motion", false });
        if (type.GetField("motionPreview", hidden)!.GetValue(monitor) is not null)
            throw new Exception("Disabled picture in picture created a preview.");
        initial.MotionPictureInPictureEnabled = true;
        type.GetMethod("HandleMotion", hidden)!.Invoke(monitor, new object[] { "Motion", false });
        if (initial.SelectedCamera != 0) throw new Exception("Motion replaced the fullscreen camera.");
        var preview = (MotionPreviewWindow?)type.GetField("motionPreview", hidden)!.GetValue(monitor);
        if (preview is null || monitor.PointToClient(preview.Location).X + preview.Width > monitor.ClientSize.Width || monitor.PointToClient(preview.Location).Y + preview.Height > monitor.ClientSize.Height)
            throw new Exception("Fullscreen motion preview is missing or outside the window.");
        // A second motion arrives while the owned preview window is already visible.
        type.GetMethod("ShowMotionPreview", hidden)!.Invoke(monitor, new object[] { 1 });
        if (!preview.Visible) throw new Exception("Repeated motion hid the preview.");
        if (preview.Padding != Padding.Empty || preview.BackColor == Color.Red)
            throw new Exception("Picture in picture must not retain the red preview border.");
        void CheckRoundedPreview()
        {
            if (preview.CornerResult == 0 && preview.Region is not null)
                throw new Exception("Native rounding must not use a pixelated window region.");
            if (preview.CornerResult != 0 && (preview.Region is null || preview.Region.IsVisible(0,0)))
                throw new Exception("Fallback rounded corners are missing.");
        }
        CheckRoundedPreview();
        monitor.ClientSize = new Size(1280, 720);
        type.GetMethod("LayoutMotionPreview", hidden)!.Invoke(monitor, null);
        CheckRoundedPreview();
        ShowVideoTestWindow(monitor.Handle, 4);
        Application.DoEvents();
        var before = preview.Bounds;
        var center = preview.PointToScreen(new Point(preview.Width / 2, preview.Height / 2));
        if (!monitor.BeginMotionPreviewInteraction(center)) throw new Exception("Preview drag did not start.");
        monitor.UpdateMotionPreviewInteraction(new Point(center.X - 100, center.Y - 80));
        monitor.EndMotionPreviewInteraction(false);
        if (preview.Left >= before.Left || preview.Top >= before.Top) throw new Exception("Preview did not move.");
        var moved = preview.Bounds;
        var corner = preview.PointToScreen(new Point(preview.Width - 12, preview.Height - 12));
        if (!monitor.BeginMotionPreviewInteraction(corner)) throw new Exception("Preview resize did not start.");
        monitor.UpdateMotionPreviewInteraction(new Point(corner.X + 50, corner.Y + 30));
        monitor.EndMotionPreviewInteraction(false);
        if (preview.Width <= moved.Width || preview.Height <= moved.Height) throw new Exception("Preview did not resize.");
        var saved = JsonSerializer.Deserialize<Settings>(JsonSerializer.Serialize(initial))!;
        if (saved.MotionPreviewX != initial.MotionPreviewX || saved.MotionPreviewWidth != initial.MotionPreviewWidth) throw new Exception("Preview geometry did not persist.");
        monitor.ExpireMotionPreview(true);
        if (!preview.Visible) throw new Exception("Pointer inside must preserve preview.");
        using var menu = (ContextMenuStrip)type.GetMethod("CreateCameraContextMenu", hidden)!.Invoke(monitor, null)!;
        type.GetMethod("PopulateCameraContextMenu", hidden)!.Invoke(monitor, new object[] { menu });
                var pipMenu = menu.Items.OfType<ToolStripMenuItem>().Single(item => item.Text == UiText.T("Bild im Bild"));
        if (!pipMenu.DropDownItems.OfType<ToolStripMenuItem>().Any(item => item.Text == UiText.T("Aktiviert") && item.Checked) ||
            !pipMenu.DropDownItems.OfType<ToolStripMenuItem>().Any(item => item.Text == UiText.T("Größe ändern …")))
            throw new Exception("Preview submenu or resize action missing.");
        var timer = (System.Windows.Forms.Timer)type.GetField("motionPreviewTimer", hidden)!.GetValue(monitor)!;
        if (!timer.Enabled) throw new Exception("Preview expiry was not armed.");
        if ((bool)type.GetMethod("TryOpenMotionPreview", hidden)!.Invoke(monitor, new object[] { new Point(-10000, -10000) })!)
            throw new Exception("A click outside the preview was consumed.");
        if (!(bool)type.GetField("fullscreen", hidden)!.GetValue(monitor)!)
            throw new Exception("Preview handling exited fullscreen.");
        monitor.ExpireMotionPreview(false);
        if (preview.Visible) throw new Exception("Preview must expire after pointer leaves.");
        ShowVideoTestWindow(monitor.Handle, 0);
        if (timer.Enabled || type.GetField("motionPreviewPlayer", hidden)!.GetValue(monitor) is not null)
            throw new Exception("Preview cleanup failed.");
        monitor.BeginMotionPreviewSizing();
        if (!preview.Visible || type.GetField("motionPreviewPlayer", hidden)!.GetValue(monitor) is not null)
            throw new Exception("Resize editor must open without camera playback.");
        monitor.ExpireMotionPreview(false);
        if (!preview.Visible || timer.Enabled) throw new Exception("Resize editor must not expire when pointer leaves.");
        var editorStart = preview.Size;
        var rightEdge = preview.PointToScreen(new Point(preview.Width-8, preview.Height/2));
        if (!monitor.BeginMotionPreviewInteraction(rightEdge)) throw new Exception("Editor edge was not interactive.");
        monitor.UpdateMotionPreviewInteraction(new Point(rightEdge.X-60,rightEdge.Y));
        monitor.EndMotionPreviewInteraction(false);
        if (preview.Width >= editorStart.Width || preview.Height >= editorStart.Height)
            throw new Exception("Editor edge drag did not resize proportionally.");
        if (!preview.Visible || timer.Enabled) throw new Exception("Editor closed or resumed expiry after resizing.");
        var input = (Form)type.GetField("motionPreviewInput",hidden)!.GetValue(monitor)!;
        if (input.Bounds != preview.Bounds) throw new Exception("Preview input overlay did not follow resizing.");
        // User adjusts, clicks outside, then motion shows the saved preview again.
        monitor.RegisterUserInteraction();
        if (preview.Visible) throw new Exception("Outside interaction did not finish the adjustment.");
        type.GetMethod("ShowMotionPreview",hidden)!.Invoke(monitor,new object[]{1});
        type.GetMethod("ShowMotionPreview",hidden)!.Invoke(monitor,new object[]{1});
        if (!preview.Visible) throw new Exception("Motion did not reopen the adjusted preview.");
        type.GetMethod("HideMotionPreview",hidden)!.Invoke(monitor,null);
        input.Dispose(); preview.Dispose();
        timer.Dispose();
        Console.WriteLine("Fullscreen motion preserves selection, creates an inset, and cleans up playback.");
    }

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
    private static void TestMotionPreviewPlayer(string executable, string fixture)
    {
        File.Copy(Path.GetFullPath(executable),Path.Combine(AppContext.BaseDirectory,"mpv.exe"),true);
        const System.Reflection.BindingFlags flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
        var settings=new Settings { MotionRetentionDays=0, Width=1280,Height=720, Cameras=[
            new() { Name="Main",Active=true }, new() { Name="Preview",Active=true,StreamUrl=new Uri(Path.GetFullPath(fixture)).AbsoluteUri } ] };
        using var monitor=new MonitorForm(settings);
        typeof(MonitorForm).GetField("fullscreen",flags)!.SetValue(monitor,true);
        ShowVideoTestWindow(monitor.Handle,4);
        try
        {
            typeof(MonitorForm).GetMethod("ShowMotionPreview",flags)!.Invoke(monitor,new object[]{1});
            var preview=(MotionPreviewWindow)typeof(MonitorForm).GetField("motionPreview",flags)!.GetValue(monitor)!;
            var host=(Panel)typeof(MonitorForm).GetField("motionPreviewVideo",flags)!.GetValue(monitor)!;
            var pipe=(string)typeof(MonitorForm).GetField("motionPreviewPipeName",flags)!.GetValue(monitor)!;
            var watch=Stopwatch.StartNew(); Size dimensions;
            do { PumpFor(100); dimensions=ReadPlayerDimensions(pipe); } while(dimensions.IsEmpty && watch.Elapsed<TimeSpan.FromSeconds(15));
            if(dimensions!=host.ClientSize) throw new Exception($"Initial PiP target mismatch: {dimensions} versus {host.ClientSize}.");
            var oldSize=preview.Size;
            var edge=preview.PointToScreen(new Point(preview.Width-8,preview.Height/2));
            if(!monitor.BeginMotionPreviewInteraction(edge))throw new Exception("Live preview resize failed to start.");
            monitor.UpdateMotionPreviewInteraction(new Point(edge.X-80,edge.Y));
            monitor.EndMotionPreviewInteraction(false); PumpFor(400);
            dimensions=ReadPlayerDimensions(pipe);
            if(preview.Width>=oldSize.Width || dimensions!=host.ClientSize)throw new Exception($"Live PiP resize did not update render target: {dimensions}, host={host.ClientSize}.");
            typeof(MonitorForm).GetMethod("ShowMotionPreview",flags)!.Invoke(monitor,new object[]{1});
            typeof(MonitorForm).GetMethod("ShowMotionPreview",flags)!.Invoke(monitor,new object[]{1});
            if (!preview.Visible) throw new Exception("Repeated live motion hid the preview.");
            monitor.BeginMotionPreviewSizing();
            if(typeof(MonitorForm).GetField("motionPreviewPlayer",flags)!.GetValue(monitor)is not null)throw new Exception("Editor retained live player.");
            monitor.ExpireMotionPreview(false);
            if(!preview.Visible)throw new Exception("Editor expired without a camera stream.");
            Console.WriteLine($"PASS: real mpv/D3D11 PiP resized from {oldSize} to {preview.Size}; render target follows host; editor is independent of playback.");
        }
        finally
        {
            typeof(MonitorForm).GetMethod("HideMotionPreview",flags)!.Invoke(monitor,null);
            ((Form)typeof(MonitorForm).GetField("motionPreviewInput",flags)!.GetValue(monitor)!).Dispose();
            ((Form)typeof(MonitorForm).GetField("motionPreview",flags)!.GetValue(monitor)!).Dispose();
            ShowVideoTestWindow(monitor.Handle,0);
        }
    }

    private static void CaptureMotionPreviewEditor(string output)
    {
        const System.Reflection.BindingFlags flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
        using var monitor=new MonitorForm(new Settings { Width=1920,Height=1080,MotionRetentionDays=0 });
        typeof(MonitorForm).GetField("fullscreen",flags)!.SetValue(monitor,true);
        ShowVideoTestWindow(monitor.Handle,4);
        monitor.BeginMotionPreviewSizing(); Application.DoEvents();
        var preview=(MotionPreviewWindow)typeof(MonitorForm).GetField("motionPreview",flags)!.GetValue(monitor)!;
        using var bitmap=new Bitmap(preview.ClientSize.Width,preview.ClientSize.Height);
        preview.DrawToBitmap(bitmap,new Rectangle(Point.Empty,bitmap.Size)); bitmap.Save(output);
        typeof(MonitorForm).GetMethod("HideMotionPreview",flags)!.Invoke(monitor,null);
        ((Form)typeof(MonitorForm).GetField("motionPreviewInput",flags)!.GetValue(monitor)!).Dispose(); preview.Dispose();
        ShowVideoTestWindow(monitor.Handle,0);
    }}
