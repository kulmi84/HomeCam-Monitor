using System.Diagnostics;
using System.IO.Pipes;
#if BETA
using System.Net;
using System.Net.Sockets;
#endif
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using Microsoft.Win32;

namespace HomeCamMonitor;

internal static class Program
{
    [STAThread]
    private static void Main() { ApplicationConfiguration.Initialize(); Application.Run(new MonitorForm()); }
}

internal sealed class Settings
{
    public List<CameraEntry> Cameras { get; set; } = [];
    public int SelectedCamera { get; set; }
    public bool AlwaysOnTop { get; set; } = true;
    public bool StartWithWindows { get; set; }
    public int Left { get; set; } = -1;
    public int Top { get; set; } = -1;
    public int Width { get; set; } = 480;
    public int Height { get; set; } = 300;
#if BETA
    public bool MotionDetectionEnabled { get; set; } = true;
    public int MotionForegroundSeconds { get; set; } = 10;
    public bool MinimizeWhenInactive { get; set; }
#endif
}

internal sealed class CameraEntry
{
    public string Name { get; set; } = "Kamera";
    public string StreamUrl { get; set; } = "";
}

internal static class SettingsStore
{
#if BETA
    private static readonly string Folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "HomeCamMonitor-Beta");
    private static readonly string StableFileName = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "HomeCamMonitor", "settings.json");
#else
    private static readonly string Folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "HomeCamMonitor");
#endif
    private static readonly string FileName = Path.Combine(Folder, "settings.json");
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    public static Settings Load()
    {
        try
        {
            if (File.Exists(FileName)) return JsonSerializer.Deserialize<Settings>(File.ReadAllText(FileName)) ?? new Settings();
#if BETA
            if (File.Exists(StableFileName)) return JsonSerializer.Deserialize<Settings>(File.ReadAllText(StableFileName)) ?? new Settings();
#endif
        }
        catch { }
        return new Settings();
    }
    public static void Save(Settings value) { Directory.CreateDirectory(Folder); File.WriteAllText(FileName, JsonSerializer.Serialize(value, JsonOptions)); }
}

internal sealed class MonitorForm : Form
{
    private readonly Panel video = new() { Dock = DockStyle.Fill, BackColor = Color.Black };
    private readonly System.Windows.Forms.Timer latencyTimer = new() { Interval = 5 * 60 * 1000 };
    private readonly System.Windows.Forms.Timer restartTimer = new() { Interval = 2000 };
    private readonly System.Windows.Forms.Timer controlsTimer = new() { Interval = 150 };
    private readonly string pipeName = $"HomeCamMonitor-{Environment.ProcessId}";
    private Settings settings;
    private ToolbarForm? toolbar;
    private DragSurfaceForm? dragSurface;
    private readonly List<ResizeGripForm> resizeGrips = [];
    private Process? player;
    private bool closing;
    private bool intentionalStop;
    private bool fullscreen;
    private bool adjustingAspectRatio;
    private bool suppressToolbar;
    private bool nativeMoveOrResize;
#if BETA
    private bool sentToBackground;
    private DateTime sentToBackgroundAt = DateTime.MinValue;
    private ContextMenuStrip? cameraContextMenu;
    private MotionIndicatorForm? motionIndicator;
    private bool motionIndicatorVisible;
#endif
    private Point lastCursorPosition;
    private DateTime lastCursorMovement = DateTime.UtcNow;
    private Rectangle windowedBounds;
#if BETA
    private readonly CancellationTokenSource motionCancellation = new();
    private readonly System.Windows.Forms.Timer motionRestoreTimer = new();
    private readonly System.Windows.Forms.Timer motionIndicatorTimer = new();
    private TcpListener? motionListener;
    private IntPtr previousForegroundWindow;
#endif

    public MonitorForm()
    {
        settings = SettingsStore.Load();
#if BETA
        Text = "HomeCam Monitor Beta";
#else
        Text = "HomeCam Monitor";
#endif
        BackColor = Color.Black;
        FormBorderStyle = FormBorderStyle.None;
        MinimumSize = new Size(240, 150);
        var initialWidth = Math.Max(240, settings.Width);
        ClientSize = new Size(initialWidth, Math.Max(150, (int)Math.Round(initialWidth * 9d / 16d)));
        if (settings.Left >= 0 && settings.Top >= 0) { StartPosition = FormStartPosition.Manual; Location = new Point(settings.Left, settings.Top); }
#if BETA
        TopMost = settings.AlwaysOnTop;
#else
        TopMost = true;
#endif
        Controls.Add(video);
        Shown += (_, _) => InitializeMonitor();
        Move += (_, _) => { if (!nativeMoveOrResize) PositionOverlays(); };
        Resize += (_, _) => { KeepCameraAspectRatio(); ApplyRoundedCorners(); if (!nativeMoveOrResize) PositionOverlays(); };
#if BETA
        Activated += (_, _) => RestoreFromBackground();
#endif
        video.DoubleClick += (_, _) => ToggleFullscreen();
        latencyTimer.Tick += (_, _) => RestartPlayer();
        restartTimer.Tick += (_, _) => { restartTimer.Stop(); StartPlayer(); };
        controlsTimer.Tick += (_, _) => UpdateToolbarVisibility();
#if BETA
        motionRestoreTimer.Interval = Math.Clamp(settings.MotionForegroundSeconds, 3, 300) * 1000;
        motionRestoreTimer.Tick += (_, _) => RestoreAfterMotion();
        motionIndicatorTimer.Interval = 1000;
        motionIndicatorTimer.Tick += (_, _) => HideMotionIndicator();
#endif
        FormClosing += (_, _) => CloseMonitor();
        ApplyRoundedCorners();
    }

    private void InitializeMonitor()
    {
        if (!File.Exists(Path.Combine(AppContext.BaseDirectory, "mpv.exe")))
        {
            MessageBox.Show(this, "mpv.exe fehlt. Bitte den vollständigen Ordner aus dem GitHub-Artefakt entpacken.", "HomeCam Monitor", MessageBoxButtons.OK, MessageBoxIcon.Error);
            Close(); return;
        }
        dragSurface = new DragSurfaceForm(this); dragSurface.Show(this);
        toolbar = new ToolbarForm(this); toolbar.Show(this); CreateResizeGrips();
#if BETA
        cameraContextMenu = CreateCameraContextMenu();
        video.ContextMenuStrip = cameraContextMenu;
        dragSurface.ContextMenuStrip = cameraContextMenu;
        toolbar.ContextMenuStrip = cameraContextMenu;
        foreach (var resizeGrip in resizeGrips) resizeGrip.ContextMenuStrip = cameraContextMenu;
        motionIndicator = new MotionIndicatorForm();
        motionIndicator.Show(this);
        motionIndicator.Hide();
#endif
        if (!HasUsableCamera()) OpenSettings();
        UpdateToolbar(); PositionOverlays(); StartPlayer(); latencyTimer.Start(); controlsTimer.Start();
#if BETA
        StartMotionListener();
#endif
    }

    private void CloseMonitor()
    {
        closing = true; latencyTimer.Stop(); restartTimer.Stop(); controlsTimer.Stop();
#if BETA
        motionRestoreTimer.Stop(); motionIndicatorTimer.Stop(); motionCancellation.Cancel(); motionListener?.Stop(); cameraContextMenu?.Dispose(); motionIndicator?.Close();
#endif
        SaveWindow(); StopPlayer(); toolbar?.Close(); dragSurface?.Close(); foreach (var grip in resizeGrips) grip.Close();
    }

    private void StartPlayer()
    {
        if (closing || !HasUsableCamera() || player is { HasExited: false }) return;
        var camera = settings.Cameras[settings.SelectedCamera];
#if BETA
        Text = $"HomeCam Monitor Beta – {camera.Name}";
#else
        Text = $"HomeCam Monitor – {camera.Name}";
#endif
        intentionalStop = false;
        var start = new ProcessStartInfo(Path.Combine(AppContext.BaseDirectory, "mpv.exe")) { UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden };
        foreach (var argument in new[]
        {
            $"--wid={video.Handle.ToInt64()}", "--no-terminal", "--really-quiet", "--no-audio", "--no-osc",
            "--profile=low-latency", "--cache=no", "--demuxer-lavf-o=rtsp_transport=tcp",
            "--hwdec=auto-safe", "--vo=gpu-next", "--gpu-api=d3d11", "--scale=ewa_lanczossharp",
            "--cscale=ewa_lanczossharp", "--dscale=mitchell", "--interpolation=no", "--window-dragging=yes", "--keep-open=no",
            $"--input-ipc-server=\\\\.\\pipe\\{pipeName}", camera.StreamUrl
        }) start.ArgumentList.Add(argument);
        try
        {
            player = Process.Start(start) ?? throw new InvalidOperationException("mpv konnte nicht gestartet werden.");
            player.EnableRaisingEvents = true; player.Exited += PlayerExited;
#if BETA
            if (TopMost && !sentToBackground)
#endif
                NativeMethods.SetWindowPos(Handle, NativeMethods.HwndTopMost, 0, 0, 0, 0,
                    NativeMethods.SwpNoMove | NativeMethods.SwpNoSize | NativeMethods.SwpNoActivate);
        }
        catch (Exception exception)
        {
            MessageBox.Show(this, $"Der Kamerastream konnte nicht gestartet werden.\n\n{exception.Message}", "HomeCam Monitor", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void PlayerExited(object? sender, EventArgs eventArgs)
    {
        if (closing || intentionalStop) return;
        try { BeginInvoke(new Action(() => { if (!closing) restartTimer.Start(); })); } catch { }
    }

    private void StopPlayer()
    {
        intentionalStop = true; var current = player; player = null;
        if (current is null) return;
        try { current.Exited -= PlayerExited; if (!current.HasExited) { current.Kill(true); current.WaitForExit(2000); } current.Dispose(); } catch { }
    }

    private void RestartPlayer()
    {
        if (closing) return;
        StopPlayer(); video.Invalidate(); restartTimer.Stop(); restartTimer.Start();
    }

    internal void SelectRelativeCamera(int direction)
    {
#if BETA
        RegisterUserInteraction();
#endif
        if (settings.Cameras.Count < 2) return;
        settings.SelectedCamera = (settings.SelectedCamera + direction + settings.Cameras.Count) % settings.Cameras.Count;
        SettingsStore.Save(settings); UpdateToolbar(); RestartPlayer();
    }

    internal async Task SaveSnapshotAsync()
    {
#if BETA
        RegisterUserInteraction();
#endif
        string? path = null;
        try
        {
            var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "HomeCam Monitor"); Directory.CreateDirectory(folder);
            var cameraName = string.Concat(settings.Cameras[settings.SelectedCamera].Name.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
            path = Path.Combine(folder, $"{cameraName}_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.png");

            try
            {
                await SendCommandAsync(new object[] { "screenshot-to-file", path, "video" });
                for (var attempt = 0; attempt < 10 && !File.Exists(path); attempt++) await Task.Delay(100);
            }
            catch { }

            if (!File.Exists(path))
            {
                suppressToolbar = true; toolbar?.Hide();
                await Task.Delay(80);
                using var bitmap = new Bitmap(video.ClientSize.Width, video.ClientSize.Height);
                using (var graphics = Graphics.FromImage(bitmap))
                    graphics.CopyFromScreen(video.PointToScreen(Point.Empty), Point.Empty, video.ClientSize);
                bitmap.Save(path, System.Drawing.Imaging.ImageFormat.Png);
                suppressToolbar = false; toolbar?.Show(this);
                PositionOverlays();
            }

            toolbar?.Flash("Gespeichert");
        }
        catch (Exception exception)
        {
            suppressToolbar = false;
            if (toolbar is { Visible: false }) { toolbar.Show(this); PositionOverlays(); }
            MessageBox.Show(this, $"Der Snapshot konnte nicht gespeichert werden.\n\nZiel: {path}\n\n{exception.Message}", "Snapshot fehlgeschlagen", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private async Task SendCommandAsync(object[] command)
    {
        using var pipe = new NamedPipeClientStream(".", pipeName, PipeDirection.Out, PipeOptions.Asynchronous);
        await pipe.ConnectAsync(2000); var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { command }) + "\n");
        await pipe.WriteAsync(bytes); await pipe.FlushAsync();
    }

    internal void OpenSettings()
    {
#if BETA
        RegisterUserInteraction();
#endif
        Settings? changedSettings = null;
        suppressToolbar = true;
        toolbar?.Hide();
        dragSurface?.Hide();
        foreach (var resizeGrip in resizeGrips) resizeGrip.Hide();
        TopMost = false;

        try
        {
            using var dialog = new SettingsForm(settings) { TopMost = true };
            if (dialog.ShowDialog(this) == DialogResult.OK) changedSettings = dialog.Result;
        }
        finally
        {
            suppressToolbar = false;
            TopMost = changedSettings?.AlwaysOnTop ?? settings.AlwaysOnTop;
            PositionOverlays();
            lastCursorMovement = DateTime.UtcNow;
        }

        if (changedSettings is null) return;
        settings = changedSettings; settings.SelectedCamera = Math.Clamp(settings.SelectedCamera, 0, settings.Cameras.Count - 1);
        SettingsStore.Save(settings); ConfigureAutostart(settings.StartWithWindows); UpdateToolbar(); RestartPlayer();
    }

    internal void BeginMove()
    {
#if BETA
        RegisterUserInteraction();
#endif
        if (fullscreen) return;
        nativeMoveOrResize = true;
        toolbar?.Hide();
        dragSurface?.Hide();
        foreach (var resizeGrip in resizeGrips) resizeGrip.Hide();

        NativeMethods.ReleaseCapture();
        NativeMethods.SendMessage(Handle, NativeMethods.WmNcLButtonDown, (IntPtr)NativeMethods.HtCaption, IntPtr.Zero);

        // SendMessage kehrt erst zurück, wenn das Verschieben beendet wurde.
        // Der Fallback stellt die Overlays auch dann wieder her, wenn Windows
        // ausnahmsweise keine WM_EXITSIZEMOVE-Nachricht liefert.
        if (nativeMoveOrResize)
        {
            nativeMoveOrResize = false;
            PositionOverlays();
            lastCursorMovement = DateTime.UtcNow;
        }
    }

    internal void BeginManualResize()
    {
#if BETA
        RegisterUserInteraction();
#endif
        if (fullscreen) return;
        nativeMoveOrResize = true;
        toolbar?.Hide();
        dragSurface?.Hide();
    }

    internal void ResizeFromGrip(int hitTest, Rectangle startBounds, Point startCursor, Point currentCursor)
    {
        if (fullscreen) return;

        var deltaX = currentCursor.X - startCursor.X;
        var deltaY = currentCursor.Y - startCursor.Y;
        var horizontalWidth = hitTest is NativeMethods.HtLeft or NativeMethods.HtTopLeft or NativeMethods.HtBottomLeft
            ? startBounds.Width - deltaX
            : startBounds.Width + deltaX;
        var verticalHeight = hitTest is NativeMethods.HtTop or NativeMethods.HtTopLeft or NativeMethods.HtTopRight
            ? startBounds.Height - deltaY
            : startBounds.Height + deltaY;
        var verticalWidth = (int)Math.Round(verticalHeight * 16d / 9d);

        var usesHorizontal = hitTest is NativeMethods.HtLeft or NativeMethods.HtRight;
        var usesVertical = hitTest is NativeMethods.HtTop or NativeMethods.HtBottom;
        var desiredWidth = usesHorizontal ? horizontalWidth :
            usesVertical ? verticalWidth :
            Math.Abs(horizontalWidth - startBounds.Width) >= Math.Abs(verticalWidth - startBounds.Width)
                ? horizontalWidth : verticalWidth;

        var minimumWidth = Math.Max(MinimumSize.Width, (int)Math.Ceiling(MinimumSize.Height * 16d / 9d));
        var width = Math.Max(minimumWidth, desiredWidth);
        var height = (int)Math.Round(width * 9d / 16d);
        var left = hitTest is NativeMethods.HtLeft or NativeMethods.HtTopLeft or NativeMethods.HtBottomLeft
            ? startBounds.Right - width : startBounds.Left;
        var top = hitTest is NativeMethods.HtTop or NativeMethods.HtTopLeft or NativeMethods.HtTopRight
            ? startBounds.Bottom - height : startBounds.Top;

        Bounds = new Rectangle(left, top, width, height);
    }

    internal void EndManualResize()
    {
        if (!nativeMoveOrResize) return;
        nativeMoveOrResize = false;
        PositionOverlays();
        lastCursorMovement = DateTime.UtcNow;
    }

    internal void ToggleFullscreen()
    {
#if BETA
        RegisterUserInteraction();
#endif
        if (!fullscreen) { windowedBounds = Bounds; fullscreen = true; Bounds = Screen.FromControl(this).Bounds; }
        else { fullscreen = false; Bounds = windowedBounds; }
        ApplyRoundedCorners(); PositionOverlays();
    }

#if BETA
    private ContextMenuStrip CreateCameraContextMenu()
    {
        var menu = new ContextMenuStrip();
        menu.Opening += (_, _) =>
        {
            RegisterUserInteraction();
            PopulateCameraContextMenu(menu);
        };
        return menu;
    }

    private void PopulateCameraContextMenu(ContextMenuStrip menu)
    {
        menu.Items.Clear();
        var alwaysOnTop = new ToolStripMenuItem("Immer im Vordergrund")
        {
            Checked = settings.AlwaysOnTop,
            CheckOnClick = true
        };
        alwaysOnTop.Click += (_, _) => SetAlwaysOnTop(alwaysOnTop.Checked);
        menu.Items.Add(alwaysOnTop);

        var duration = new ToolStripMenuItem($"Vordergrunddauer: {settings.MotionForegroundSeconds} Sekunden")
        {
            Enabled = !settings.AlwaysOnTop
        };
        var values = new[] { 3, 5, 10, 15, 30, 60 };
        foreach (var seconds in values.Append(settings.MotionForegroundSeconds).Distinct().OrderBy(value => value))
        {
            var item = new ToolStripMenuItem($"{seconds} Sekunden")
            {
                Checked = seconds == settings.MotionForegroundSeconds
            };
            item.Click += (_, _) =>
            {
                settings.MotionForegroundSeconds = seconds;
                motionRestoreTimer.Interval = seconds * 1000;
                SettingsStore.Save(settings);
            };
            duration.DropDownItems.Add(item);
        }
        menu.Items.Add(duration);
    }

    internal void RegisterUserInteraction()
    {
        if (!motionRestoreTimer.Enabled) return;
        motionRestoreTimer.Stop();
        previousForegroundWindow = IntPtr.Zero;
        TopMost = settings.AlwaysOnTop;
        PositionOverlays();
    }

    private void SetAlwaysOnTop(bool enabled)
    {
        settings.AlwaysOnTop = enabled;
        motionRestoreTimer.Stop();
        sentToBackground = false;
        TopMost = enabled;
        SettingsStore.Save(settings);
        PositionOverlays();
    }

    internal void SendToBackground()
    {
        sentToBackground = true;
        sentToBackgroundAt = DateTime.UtcNow;
        motionRestoreTimer.Stop();
        var previous = previousForegroundWindow;
        previousForegroundWindow = IntPtr.Zero;
        toolbar?.Hide(); dragSurface?.Hide();
        HideMotionIndicator();
        foreach (var resizeGrip in resizeGrips) resizeGrip.Hide();
        TopMost = false;
        NativeMethods.SetWindowPos(Handle, NativeMethods.HwndBottom, 0, 0, 0, 0,
            NativeMethods.SwpNoMove | NativeMethods.SwpNoSize | NativeMethods.SwpNoActivate);
        if (previous != IntPtr.Zero && previous != Handle) NativeMethods.SetForegroundWindow(previous);
    }

    private void RestoreFromBackground()
    {
        if (!sentToBackground) return;
        // Das Herabstufen eines TopMost-Fensters kann selbst kurz Activated
        // ausloesen. Nur eine spaetere echte Benutzeraktivierung darf es
        // wiederherstellen.
        if (DateTime.UtcNow - sentToBackgroundAt < TimeSpan.FromSeconds(1))
        {
            TopMost = false;
            NativeMethods.SetWindowPos(Handle, NativeMethods.HwndBottom, 0, 0, 0, 0,
                NativeMethods.SwpNoMove | NativeMethods.SwpNoSize | NativeMethods.SwpNoActivate);
            return;
        }
        sentToBackground = false; TopMost = settings.AlwaysOnTop;
        lastCursorMovement = DateTime.UtcNow; PositionOverlays();
    }
#endif

    private void KeepCameraAspectRatio()
    {
        if (fullscreen || adjustingAspectRatio || WindowState != FormWindowState.Normal) return;
        var targetHeight = Math.Max(MinimumSize.Height, (int)Math.Round(ClientSize.Width * 9d / 16d));
        if (Math.Abs(ClientSize.Height - targetHeight) <= 1) return;
        adjustingAspectRatio = true;
        ClientSize = new Size(ClientSize.Width, targetHeight);
        adjustingAspectRatio = false;
    }

    private void UpdateToolbar() { if (toolbar is not null && HasUsableCamera()) toolbar.CameraName = settings.Cameras[settings.SelectedCamera].Name; }
    private void UpdateToolbarVisibility()
    {
#if BETA
        if (toolbar is null || toolbar.IsDisposed || suppressToolbar || sentToBackground) return;
#else
        if (toolbar is null || toolbar.IsDisposed || suppressToolbar) return;
#endif
        if (nativeMoveOrResize)
        {
            if (toolbar.Visible) toolbar.Hide();
            return;
        }
        var cursor = Cursor.Position;
        var overWindow = Bounds.Contains(cursor);
        var overToolbar = toolbar.Visible && toolbar.Bounds.Contains(cursor);
        if (cursor != lastCursorPosition)
        {
            lastCursorPosition = cursor;
            if (overWindow || overToolbar) lastCursorMovement = DateTime.UtcNow;
        }

        var shouldShow = overToolbar || (overWindow && DateTime.UtcNow - lastCursorMovement < TimeSpan.FromSeconds(2));
        if (shouldShow && !toolbar.Visible) { toolbar.Show(this); PositionOverlays(); }
        else if (!shouldShow && toolbar.Visible) toolbar.Hide();
    }
    private void CreateResizeGrips()
    {
        var definitions = new (int Hit, Cursor Cursor)[]
        {
            (NativeMethods.HtTop, Cursors.SizeNS), (NativeMethods.HtBottom, Cursors.SizeNS),
            (NativeMethods.HtLeft, Cursors.SizeWE), (NativeMethods.HtRight, Cursors.SizeWE),
            (NativeMethods.HtTopLeft, Cursors.SizeNWSE), (NativeMethods.HtTopRight, Cursors.SizeNESW),
            (NativeMethods.HtBottomLeft, Cursors.SizeNESW), (NativeMethods.HtBottomRight, Cursors.SizeNWSE)
        };
        foreach (var definition in definitions)
        {
            var grip = new ResizeGripForm(this, definition.Hit, definition.Cursor); resizeGrips.Add(grip); grip.Show(this);
        }
    }

    private void PositionOverlays()
    {
        if (toolbar is null || toolbar.IsDisposed) return;
#if BETA
        if (sentToBackground)
        {
            toolbar.Hide(); dragSurface?.Hide();
            foreach (var resizeGrip in resizeGrips) resizeGrip.Hide();
            return;
        }
#endif
        if (dragSurface is not null && !dragSurface.IsDisposed)
        {
            dragSurface.Bounds = Bounds;
            dragSurface.TopMost = TopMost;
            dragSurface.Visible = true;
        }
        toolbar.Location = new Point(Left + Math.Max(0, (Width - toolbar.Width) / 2), Top + Height - toolbar.Height - 10); toolbar.TopMost = TopMost;
#if BETA
        if (motionIndicator is not null && !motionIndicator.IsDisposed)
        {
            motionIndicator.Location = new Point(Right - motionIndicator.Width - 12, Top + 12);
            motionIndicator.TopMost = TopMost;
            motionIndicator.Visible = motionIndicatorVisible;
            if (motionIndicatorVisible) motionIndicator.BringToFront();
        }
#endif
        const int edge = 7, corner = 16;
        var bounds = new[]
        {
            new Rectangle(Left + corner, Top, Math.Max(1, Width - 2 * corner), edge),
            new Rectangle(Left + corner, Bottom - edge, Math.Max(1, Width - 2 * corner), edge),
            new Rectangle(Left, Top + corner, edge, Math.Max(1, Height - 2 * corner)),
            new Rectangle(Right - edge, Top + corner, edge, Math.Max(1, Height - 2 * corner)),
            new Rectangle(Left, Top, corner, corner), new Rectangle(Right - corner, Top, corner, corner),
            new Rectangle(Left, Bottom - corner, corner, corner), new Rectangle(Right - corner, Bottom - corner, corner, corner)
        };
        for (var index = 0; index < resizeGrips.Count; index++)
        {
            resizeGrips[index].Bounds = bounds[index]; resizeGrips[index].TopMost = TopMost; resizeGrips[index].Visible = !fullscreen;
            if (!fullscreen) resizeGrips[index].BringToFront();
        }
        if (toolbar.Visible) toolbar.BringToFront();
    }
    private bool HasUsableCamera() => settings.Cameras.Count > 0 && settings.SelectedCamera >= 0 && settings.SelectedCamera < settings.Cameras.Count && Uri.TryCreate(settings.Cameras[settings.SelectedCamera].StreamUrl, UriKind.Absolute, out _);
    private static void ConfigureAutostart(bool enabled)
    {
        using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true);
#if BETA
        const string name = "HomeCamMonitor-Beta";
#else
        const string name = "HomeCamMonitor";
#endif
        if (enabled) key?.SetValue(name, $"\"{Application.ExecutablePath}\""); else key?.DeleteValue(name, false);
    }

#if BETA
    private void StartMotionListener()
    {
        try
        {
            motionListener = new TcpListener(IPAddress.Any, 8765);
            motionListener.Start();
            _ = Task.Run(() => ListenForMotionAsync(motionCancellation.Token));
        }
        catch (Exception exception)
        {
            MessageBox.Show(this, $"Die Bewegungserkennung konnte Port 8765 nicht öffnen.\n\n{exception.Message}", "HomeCam Monitor Beta", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private async Task ListenForMotionAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested && motionListener is not null)
        {
            try
            {
                using var client = await motionListener.AcceptTcpClientAsync(cancellationToken);
                using var stream = client.GetStream();
                using var reader = new StreamReader(stream, Encoding.ASCII, false, 1024, true);
                var requestLine = await reader.ReadLineAsync(cancellationToken) ?? "";
                var parts = requestLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                var camera = parts.Length >= 2 ? ReadCameraParameter(parts[1]) : null;
                var accepted = string.Equals(camera, "Einfahrt", StringComparison.OrdinalIgnoreCase);
                if (accepted && !closing) BeginInvoke(new Action(() => HandleMotion("Einfahrt")));
                var response = accepted ? "HTTP/1.1 204 No Content\r\nConnection: close\r\n\r\n" : "HTTP/1.1 404 Not Found\r\nConnection: close\r\nContent-Length: 0\r\n\r\n";
                await stream.WriteAsync(Encoding.ASCII.GetBytes(response), cancellationToken);
            }
            catch (OperationCanceledException) { break; }
            catch (ObjectDisposedException) { break; }
            catch { if (!cancellationToken.IsCancellationRequested) await Task.Delay(500, cancellationToken); }
        }
    }

    private static string? ReadCameraParameter(string target)
    {
        if (!target.StartsWith("/motion", StringComparison.OrdinalIgnoreCase)) return null;
        var query = target.IndexOf('?');
        if (query < 0) return null;
        foreach (var item in target[(query + 1)..].Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var pair = item.Split('=', 2);
            if (pair.Length == 2 && string.Equals(pair[0], "camera", StringComparison.OrdinalIgnoreCase))
                return Uri.UnescapeDataString(pair[1].Replace('+', ' '));
        }
        return null;
    }

    private void HandleMotion(string cameraName)
    {
        if (!settings.MotionDetectionEnabled) return;
        ShowMotionIndicator();
        if (settings.AlwaysOnTop) return;
        var cameraIndex = settings.Cameras.FindIndex(camera => string.Equals(camera.Name, cameraName, StringComparison.OrdinalIgnoreCase));
        if (cameraIndex < 0) { toolbar?.Flash("Einfahrt fehlt"); return; }
        if (!motionRestoreTimer.Enabled) previousForegroundWindow = NativeMethods.GetForegroundWindow();
        if (settings.SelectedCamera != cameraIndex)
        {
            settings.SelectedCamera = cameraIndex; SettingsStore.Save(settings); UpdateToolbar(); RestartPlayer();
        }
        motionRestoreTimer.Stop();
        motionRestoreTimer.Interval = Math.Clamp(settings.MotionForegroundSeconds, 3, 300) * 1000;
        ForceToForeground();
        if (!settings.AlwaysOnTop) motionRestoreTimer.Start();
    }

    private void ShowMotionIndicator()
    {
        motionIndicatorVisible = true;
        motionIndicatorTimer.Stop();
        motionIndicatorTimer.Interval = 1000;
        motionIndicatorTimer.Start();
        PositionOverlays();
    }

    private void HideMotionIndicator()
    {
        motionIndicatorTimer.Stop();
        motionIndicatorVisible = false;
        motionIndicator?.Hide();
    }

    private void ForceToForeground()
    {
        sentToBackground = false;
        if (WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Normal;
        TopMost = true;
        NativeMethods.ShowWindowAsync(Handle, NativeMethods.SwShowNoActivate);
        NativeMethods.SetWindowPos(Handle, NativeMethods.HwndTopMost, 0, 0, 0, 0,
            NativeMethods.SwpNoMove | NativeMethods.SwpNoSize | NativeMethods.SwpNoActivate | NativeMethods.SwpShowWindow);
        PositionOverlays();
    }

    private void RestoreAfterMotion()
    {
        motionRestoreTimer.Stop();
        if (settings.MinimizeWhenInactive) MinimizeAfterMotion();
        else SendToBackground();
    }

    private void MinimizeAfterMotion()
    {
        sentToBackground = true;
        sentToBackgroundAt = DateTime.UtcNow;
        previousForegroundWindow = IntPtr.Zero;
        toolbar?.Hide(); dragSurface?.Hide();
        HideMotionIndicator();
        foreach (var resizeGrip in resizeGrips) resizeGrip.Hide();
        TopMost = false;
        WindowState = FormWindowState.Minimized;
    }
#endif
    private void ApplyRoundedCorners()
    {
        Region?.Dispose(); if (fullscreen) { Region = null; return; }
        var radius = Math.Max(12, DeviceDpi * 14 / 96); var handle = NativeMethods.CreateRoundRectRgn(0, 0, Width + 1, Height + 1, radius, radius);
        Region = Region.FromHrgn(handle); NativeMethods.DeleteObject(handle);
        var preference = fullscreen ? (int)NativeMethods.DwmWindowCornerPreference.DoNotRound : (int)NativeMethods.DwmWindowCornerPreference.Round;
        NativeMethods.DwmSetWindowAttribute(Handle, NativeMethods.DwmWindowAttribute.WindowCornerPreference,
            ref preference, sizeof(int));
    }
    private void SaveWindow()
    {
        var value = fullscreen ? windowedBounds : Bounds; settings.Left = value.Left; settings.Top = value.Top; settings.Width = value.Width; settings.Height = value.Height; SettingsStore.Save(settings);
    }
    protected override void WndProc(ref Message message)
    {
        if (message.Msg == NativeMethods.WmNcCalcSize && message.WParam != IntPtr.Zero)
        {
            message.Result = IntPtr.Zero;
            return;
        }

        if (message.Msg == NativeMethods.WmEnterSizeMove)
        {
            nativeMoveOrResize = true;
            toolbar?.Hide(); dragSurface?.Hide();
            foreach (var resizeGrip in resizeGrips) resizeGrip.Hide();
        }

        base.WndProc(ref message);
        if (message.Msg == NativeMethods.WmExitSizeMove)
        {
            nativeMoveOrResize = false;
            PositionOverlays();
            lastCursorMovement = DateTime.UtcNow;
            return;
        }
        if (message.Msg != NativeMethods.WmNcHitTest || fullscreen || WindowState != FormWindowState.Normal) return;
        var p = PointToClient(Cursor.Position); var grip = Math.Max(8, DeviceDpi * 8 / 96); var left = p.X < grip; var right = p.X >= ClientSize.Width - grip; var top = p.Y < grip; var bottom = p.Y >= ClientSize.Height - grip;
        if (left && top) message.Result = (IntPtr)NativeMethods.HtTopLeft; else if (right && top) message.Result = (IntPtr)NativeMethods.HtTopRight;
        else if (left && bottom) message.Result = (IntPtr)NativeMethods.HtBottomLeft; else if (right && bottom) message.Result = (IntPtr)NativeMethods.HtBottomRight;
        else if (left) message.Result = (IntPtr)NativeMethods.HtLeft; else if (right) message.Result = (IntPtr)NativeMethods.HtRight;
        else if (top) message.Result = (IntPtr)NativeMethods.HtTop; else if (bottom) message.Result = (IntPtr)NativeMethods.HtBottom;
    }
}

#if BETA
internal sealed class MotionIndicatorForm : Form
{
    protected override bool ShowWithoutActivation => true;
    protected override CreateParams CreateParams
    {
        get
        {
            var parameters = base.CreateParams;
            parameters.ExStyle |= 0x00000020 | 0x08000000 | 0x00000080;
            return parameters;
        }
    }

    public MotionIndicatorForm()
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        AutoScaleMode = AutoScaleMode.None;
        ClientSize = new Size(18, 18);
        BackColor = Color.Black;
        TransparencyKey = Color.Black;
        TopMost = true;
    }

    protected override void OnPaint(PaintEventArgs eventArgs)
    {
        base.OnPaint(eventArgs);
        eventArgs.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        eventArgs.Graphics.ScaleTransform(0.50f, 0.50f);
        using var whiteBrush = new SolidBrush(Color.White);
        using var blackPen = new Pen(Color.Black, 6.2f)
        {
            StartCap = System.Drawing.Drawing2D.LineCap.Round,
            EndCap = System.Drawing.Drawing2D.LineCap.Round,
            LineJoin = System.Drawing.Drawing2D.LineJoin.Round
        };
        using var whitePen = new Pen(Color.White, 3.4f)
        {
            StartCap = System.Drawing.Drawing2D.LineCap.Round,
            EndCap = System.Drawing.Drawing2D.LineCap.Round,
            LineJoin = System.Drawing.Drawing2D.LineJoin.Round
        };

        // Kräftige, laufende Silhouette: weiß mit schwarzer Kontur, damit das
        // Symbol sowohl auf hellen als auch auf dunklen Kamerabildern sichtbar ist.
        eventArgs.Graphics.FillEllipse(Brushes.Black, 17, 2, 11, 11);
        eventArgs.Graphics.FillEllipse(whiteBrush, 19, 4, 7, 7);
        var limbs = new[]
        {
            new[] { new PointF(19, 13), new PointF(15, 21), new PointF(8, 29) },
            new[] { new PointF(16, 20), new PointF(23, 24), new PointF(27, 31) },
            new[] { new PointF(18, 15), new PointF(11, 14), new PointF(6, 19) },
            new[] { new PointF(20, 14), new PointF(25, 17), new PointF(31, 13) }
        };
        foreach (var limb in limbs) eventArgs.Graphics.DrawLines(blackPen, limb);
        foreach (var limb in limbs) eventArgs.Graphics.DrawLines(whitePen, limb);
    }
}
#endif

internal sealed class DragSurfaceForm : Form
{
    private Point mouseDownPosition;
    private bool dragPending;
    protected override bool ShowWithoutActivation => true;
    public DragSurfaceForm(MonitorForm monitor)
    {
        FormBorderStyle = FormBorderStyle.None; ShowInTaskbar = false; StartPosition = FormStartPosition.Manual;
        BackColor = Color.Black; Opacity = 0.01; TopMost = true; Cursor = Cursors.Default;
#if BETA
        MouseDown += (_, eventArgs) =>
        {
            if (eventArgs.Button == MouseButtons.Left) monitor.RegisterUserInteraction();
        };
#endif
        MouseDown += (_, eventArgs) =>
        {
            if (eventArgs.Button != MouseButtons.Left) return;
            mouseDownPosition = eventArgs.Location;
            dragPending = true;
        };
        MouseMove += (_, eventArgs) =>
        {
            if (!dragPending || eventArgs.Button != MouseButtons.Left) return;
            var dragSize = SystemInformation.DragSize;
            if (Math.Abs(eventArgs.X - mouseDownPosition.X) < dragSize.Width / 2 &&
                Math.Abs(eventArgs.Y - mouseDownPosition.Y) < dragSize.Height / 2) return;
            dragPending = false;
            monitor.BeginMove();
        };
        MouseUp += (_, _) => dragPending = false;
        DoubleClick += (_, _) => monitor.ToggleFullscreen();
    }
}

internal sealed class ResizeGripForm : Form
{
    private bool resizing;
    private Rectangle startBounds;
    private Point startCursor;

    protected override bool ShowWithoutActivation => true;
    public ResizeGripForm(MonitorForm monitor, int hitTest, Cursor cursor)
    {
        FormBorderStyle = FormBorderStyle.None; ShowInTaskbar = false; StartPosition = FormStartPosition.Manual;
        BackColor = Color.Black; Opacity = 0.01; TopMost = true; Cursor = cursor;
        MouseDown += (_, eventArgs) =>
        {
            if (eventArgs.Button != MouseButtons.Left) return;
            resizing = true;
            startBounds = monitor.Bounds;
            startCursor = System.Windows.Forms.Cursor.Position;
            Capture = true;
            monitor.BeginManualResize();
        };
        MouseMove += (_, eventArgs) =>
        {
            if (!resizing || eventArgs.Button != MouseButtons.Left) return;
            monitor.ResizeFromGrip(hitTest, startBounds, startCursor, System.Windows.Forms.Cursor.Position);
        };
        MouseUp += (_, eventArgs) =>
        {
            if (eventArgs.Button != MouseButtons.Left || !resizing) return;
            resizing = false;
            Capture = false;
            monitor.EndManualResize();
        };
        MouseCaptureChanged += (_, _) =>
        {
            if (!resizing) return;
            resizing = false;
            monitor.EndManualResize();
        };
    }
}

internal sealed class ToolbarForm : Form
{
    private readonly Label name;
    private readonly Label note;
    protected override bool ShowWithoutActivation => true;
    public string CameraName { set => name.Text = value; }
    public ToolbarForm(MonitorForm monitor)
    {
        FormBorderStyle = FormBorderStyle.None; ShowInTaskbar = false; BackColor = Color.FromArgb(20, 20, 20); Opacity = 0.78;
        ClientSize = new Size(256, 34); StartPosition = FormStartPosition.Manual; TopMost = true;
        var previous = Item("‹", 0, (_, _) => monitor.SelectRelativeCamera(-1));
        name = Item("Kamera", 32, null, 64); var next = Item("›", 96, (_, _) => monitor.SelectRelativeCamera(1));
        var snapshot = Item("\uEB9F", 128, async (_, _) => await monitor.SaveSnapshotAsync());
        snapshot.Font = new Font("Segoe MDL2 Assets", 15);
        var settings = Item("⚙", 160, (_, _) => monitor.OpenSettings());
#if BETA
        var lastAction = Item("↓", 192, (_, _) => monitor.SendToBackground());
#else
        var lastAction = Item("⛶", 192, (_, _) => monitor.ToggleFullscreen());
#endif
        var close = Item("×", 224, (_, _) => monitor.Close());
        Controls.AddRange([previous, name, next, snapshot, settings, lastAction, close]);
        note = new Label { AutoSize = true, ForeColor = Color.White, BackColor = Color.FromArgb(20, 20, 20), Visible = false }; Controls.Add(note);
        var shape = NativeMethods.CreateRoundRectRgn(0, 0, Width + 1, Height + 1, 14, 14); Region = Region.FromHrgn(shape); NativeMethods.DeleteObject(shape);
    }
    private static Label Item(string text, int x, EventHandler? click, int width = 32)
    {
        var item = new Label { Text = text, Left = x, Top = 1, Width = width, Height = 32, TextAlign = ContentAlignment.MiddleCenter, ForeColor = Color.White, BackColor = Color.FromArgb(20, 20, 20), Font = new Font("Segoe UI Symbol", text == "Kamera" ? 9 : 15), Cursor = Cursors.Hand };
        if (click is not null) item.Click += click; return item;
    }
    public async void Flash(string text)
    {
        note.Text = text; note.Left = (Width - note.PreferredWidth) / 2; note.Top = 0; note.Visible = true; await Task.Delay(1600); if (!IsDisposed) note.Visible = false;
    }
}

internal static class NativeMethods
{
    public const int WmNcCalcSize = 0x0083, WmNcHitTest = 0x0084, WmNcLButtonDown = 0x00A1, WmSysCommand = 0x0112, ScMove = 0xF010, HtCaption = 2;
    public const int WmEnterSizeMove = 0x0231, WmExitSizeMove = 0x0232;
    public static readonly IntPtr HwndTopMost = new(-1);
#if BETA
    public static readonly IntPtr HwndBottom = new(1);
#endif
    public const uint SwpNoSize = 0x0001, SwpNoMove = 0x0002, SwpNoActivate = 0x0010;
    public const uint SwpShowWindow = 0x0040;
    public const int SwShowNoActivate = 4;
    public const int HtLeft = 10, HtRight = 11, HtTop = 12, HtTopLeft = 13, HtTopRight = 14, HtBottom = 15, HtBottomLeft = 16, HtBottomRight = 17;
    [DllImport("user32.dll")] public static extern bool ReleaseCapture();
    [DllImport("user32.dll")] public static extern IntPtr SendMessage(IntPtr window, int message, IntPtr parameter, IntPtr data);
    [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr window, IntPtr insertAfter, int x, int y, int width, int height, uint flags);
#if BETA
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr window);
    [DllImport("user32.dll")] public static extern bool ShowWindowAsync(IntPtr window, int command);
#endif
    [DllImport("gdi32.dll")] public static extern IntPtr CreateRoundRectRgn(int left, int top, int right, int bottom, int width, int height);
    [DllImport("gdi32.dll")] public static extern bool DeleteObject(IntPtr handle);
    public enum DwmWindowAttribute { WindowCornerPreference = 33 }
    public enum DwmWindowCornerPreference { Default = 0, DoNotRound = 1, Round = 2, RoundSmall = 3 }
    [DllImport("dwmapi.dll")] public static extern int DwmSetWindowAttribute(IntPtr window, DwmWindowAttribute attribute, ref int value, int size);
}

internal sealed class SettingsForm : Form
{
    private readonly DataGridView cameras = new() { Dock = DockStyle.Fill, AllowUserToAddRows = true, AllowUserToDeleteRows = true, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, RowHeadersVisible = false, SelectionMode = DataGridViewSelectionMode.FullRowSelect };
    private readonly CheckBox top = new() { Text = "Immer im Vordergrund", AutoSize = true };
    private readonly CheckBox autostart = new() { Text = "Mit Windows starten", AutoSize = true };
#if BETA
    private readonly CheckBox motionDetection = new() { Text = "Bewegungserkennung aktiv", AutoSize = true };
    private readonly CheckBox minimizeWhenInactive = new() { Text = "Bei Inaktivität minimieren", AutoSize = true };
    private readonly NumericUpDown motionSeconds = new() { Minimum = 3, Maximum = 300, Value = 10, Width = 60 };
#endif
    public Settings Result { get; private set; }
    public SettingsForm(Settings current)
    {
        Result = current; Text = "HomeCam Monitor – Einstellungen"; FormBorderStyle = FormBorderStyle.FixedDialog; StartPosition = FormStartPosition.CenterParent; MaximizeBox = false; MinimizeBox = false; ClientSize = new Size(760, 390);
        cameras.Columns.Add(new DataGridViewTextBoxColumn { Name = "CameraName", HeaderText = "Name", FillWeight = 25 }); cameras.Columns.Add(new DataGridViewTextBoxColumn { Name = "StreamUrl", HeaderText = "RTSP-/HTTP-Streamadresse", FillWeight = 75 });
        foreach (var camera in current.Cameras) cameras.Rows.Add(camera.Name, camera.StreamUrl); top.Checked = current.AlwaysOnTop; autostart.Checked = current.StartWithWindows;
#if BETA
        motionDetection.Checked = current.MotionDetectionEnabled;
        minimizeWhenInactive.Checked = current.MinimizeWhenInactive;
        motionSeconds.Value = Math.Clamp(current.MotionForegroundSeconds, 3, 300);
        void UpdateMotionOptions()
        {
            var enabled = motionDetection.Checked && !top.Checked;
            motionSeconds.Enabled = enabled;
            minimizeWhenInactive.Enabled = enabled;
        }
        UpdateMotionOptions();
        top.CheckedChanged += (_, _) => UpdateMotionOptions();
        motionDetection.CheckedChanged += (_, _) => UpdateMotionOptions();
#endif
        var table = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(14), ColumnCount = 1, RowCount = 5 }; table.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); table.Controls.Add(cameras, 0, 0);
        table.Controls.Add(new Label { Text = "Beispiel: rtsp://192.168.x.x:8554/Einfahrt", AutoSize = true, ForeColor = SystemColors.GrayText }, 0, 1);
        var options = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true }; options.Controls.Add(top); options.Controls.Add(autostart);
#if BETA
        options.Controls.Add(motionDetection);
        options.Controls.Add(minimizeWhenInactive);
        options.Controls.Add(new Label { Text = "Vordergrunddauer:", AutoSize = true, Margin = new Padding(18, 4, 3, 0) });
        options.Controls.Add(motionSeconds);
        options.Controls.Add(new Label { Text = "Sekunden", AutoSize = true, Margin = new Padding(3, 4, 3, 0) });
#endif
        table.Controls.Add(options, 0, 2);
        table.Controls.Add(new Label { Text = $"Version {Application.ProductVersion.Split('+')[0]}", AutoSize = true, ForeColor = SystemColors.GrayText, Anchor = AnchorStyles.Left }, 0, 3);
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft }; var ok = new Button { Text = "Speichern", DialogResult = DialogResult.OK, AutoSize = true };
        buttons.Controls.Add(ok); buttons.Controls.Add(new Button { Text = "Abbrechen", DialogResult = DialogResult.Cancel, AutoSize = true }); table.Controls.Add(buttons, 0, 4); Controls.Add(table); AcceptButton = ok; CancelButton = buttons.Controls[1] as Button;
        ok.Click += (_, _) =>
        {
            var entries = ReadCameras();
            if (entries.Count == 0 || entries.Any(c => !Uri.TryCreate(c.StreamUrl, UriKind.Absolute, out _)))
            {
                MessageBox.Show(this, "Bitte gültige Streamadressen eintragen.", "Ungültige Kamera", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                DialogResult = DialogResult.None; return;
            }
            Result = new Settings
            {
                Cameras = entries, SelectedCamera = Math.Clamp(current.SelectedCamera, 0, entries.Count - 1),
                AlwaysOnTop = top.Checked, StartWithWindows = autostart.Checked,
                Left = current.Left, Top = current.Top, Width = current.Width, Height = current.Height,
#if BETA
                MotionDetectionEnabled = motionDetection.Checked,
                MotionForegroundSeconds = (int)motionSeconds.Value,
                MinimizeWhenInactive = minimizeWhenInactive.Checked
#endif
            };
        };
    }
    private List<CameraEntry> ReadCameras()
    {
        var result = new List<CameraEntry>(); foreach (DataGridViewRow row in cameras.Rows) { if (row.IsNewRow) continue; var name = Convert.ToString(row.Cells[0].Value)?.Trim() ?? ""; var url = Convert.ToString(row.Cells[1].Value)?.Trim() ?? ""; if (name.Length > 0 || url.Length > 0) result.Add(new CameraEntry { Name = name, StreamUrl = url }); } return result;
    }
}
