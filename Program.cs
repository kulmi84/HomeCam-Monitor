using System.Diagnostics;
using System.IO.Pipes;
#if BETA
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
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
    public bool DirectHomeAssistantEnabled { get; set; }
    public string HomeAssistantUrl { get; set; } = "http://192.168.9.8:8123";
    public string HomeAssistantToken { get; set; } = "";
    public string MotionEntityId { get; set; } = "binary_sensor.camera_einfahrt_bewegung";
    public string MotionCameraName { get; set; } = "Einfahrt";
    public bool IgnoreHomeAssistantCertificateErrors { get; set; }
    public bool PerCameraMotionConfigured { get; set; }
#endif
}

internal sealed class CameraEntry
{
    public string Name { get; set; } = "Kamera";
    public string StreamUrl { get; set; } = "";
#if BETA
    public bool MotionEnabled { get; set; }
    public string MotionEntityId { get; set; } = "";
#endif
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
            if (File.Exists(FileName))
            {
                var loaded = JsonSerializer.Deserialize<Settings>(File.ReadAllText(FileName)) ?? new Settings();
#if BETA
                MigratePerCameraMotion(loaded);
#endif
                return loaded;
            }
#if BETA
            if (File.Exists(StableFileName))
            {
                var imported = JsonSerializer.Deserialize<Settings>(File.ReadAllText(StableFileName)) ?? new Settings();
                MigratePerCameraMotion(imported);
                return imported;
            }
#endif
        }
        catch { }
        return new Settings();
    }
    public static void Save(Settings value) { Directory.CreateDirectory(Folder); File.WriteAllText(FileName, JsonSerializer.Serialize(value, JsonOptions)); }
#if BETA
    private static void MigratePerCameraMotion(Settings value)
    {
        if (value.PerCameraMotionConfigured) return;
        var camera = value.Cameras.FirstOrDefault(item => string.Equals(item.Name, value.MotionCameraName, StringComparison.OrdinalIgnoreCase))
            ?? value.Cameras.FirstOrDefault(item => string.Equals(item.Name, "Einfahrt", StringComparison.OrdinalIgnoreCase));
        if (camera is not null)
        {
            camera.MotionEnabled = true;
            camera.MotionEntityId = value.MotionEntityId;
        }
        value.PerCameraMotionConfigured = true;
        Save(value);
    }
#endif
}

internal sealed class MonitorForm : Form
{
#if BETA
    protected override bool ShowWithoutActivation => true;
#endif
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
    private bool wasMinimized;
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
    private CancellationTokenSource motionCancellation = new();
    private readonly System.Windows.Forms.Timer motionRestoreTimer = new();
    private readonly System.Windows.Forms.Timer motionIndicatorTimer = new();
    private TcpListener? motionListener;
    private ClientWebSocket? homeAssistantSocket;
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
        Resize += (_, _) =>
        {
#if BETA
            if (WindowState == FormWindowState.Minimized)
            {
                wasMinimized = true;
                return;
            }
#endif
            KeepCameraAspectRatio();
            ApplyRoundedCorners();
            if (!nativeMoveOrResize) PositionOverlays();
#if BETA
            if (wasMinimized && WindowState == FormWindowState.Normal)
            {
                wasMinimized = false;
                BeginInvoke(new Action(RestoreWindowAfterMinimize));
            }
#endif
        };
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
        RestartMotionIntegration();
#endif
    }

    private void CloseMonitor()
    {
        closing = true; latencyTimer.Stop(); restartTimer.Stop(); controlsTimer.Stop();
#if BETA
        motionRestoreTimer.Stop(); motionIndicatorTimer.Stop(); StopMotionIntegration(); cameraContextMenu?.Dispose(); motionIndicator?.Close();
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
            using var dialog = new SettingsForm(settings);
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
#if BETA
        RestartMotionIntegration();
#endif
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
        var menu = new ContextMenuStrip
        {
            BackColor = Color.FromArgb(28, 28, 31),
            ForeColor = Color.White,
            Opacity = 0.94,
            Renderer = new HomeCamDarkMenuRenderer(),
            ShowImageMargin = true,
            Padding = new Padding(4)
        };
        menu.Opening += (_, _) =>
        {
            RegisterUserInteraction();
            PopulateCameraContextMenu(menu);
        };
        menu.Opened += (_, _) =>
        {
            menu.Region?.Dispose();
            var shape = NativeMethods.CreateRoundRectRgn(0, 0, menu.Width + 1, menu.Height + 1, 12, 12);
            menu.Region = Region.FromHrgn(shape);
            NativeMethods.DeleteObject(shape);
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

        var motionEnabled = new ToolStripMenuItem("Bewegungserkennung aktiv")
        {
            Checked = settings.MotionDetectionEnabled,
            CheckOnClick = true
        };
        motionEnabled.Click += (_, _) => SetMotionDetectionEnabled(motionEnabled.Checked);
        menu.Items.Add(motionEnabled);
        menu.Items.Add(new ToolStripSeparator());

        var duration = new ToolStripMenuItem($"Vordergrunddauer: {settings.MotionForegroundSeconds} Sekunden")
        {
            Enabled = settings.MotionDetectionEnabled && !settings.AlwaysOnTop
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

    private void SetMotionDetectionEnabled(bool enabled)
    {
        settings.MotionDetectionEnabled = enabled;
        motionRestoreTimer.Stop();
        previousForegroundWindow = IntPtr.Zero;
        SettingsStore.Save(settings);
        RestartMotionIntegration();
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

    internal void MinimizeWindow()
    {
        RegisterUserInteraction();
        sentToBackground = false;
        previousForegroundWindow = IntPtr.Zero;
        toolbar?.Hide();
        dragSurface?.Hide();
        HideMotionIndicator();
        foreach (var resizeGrip in resizeGrips) resizeGrip.Hide();
        TopMost = false;
        wasMinimized = true;
        WindowState = FormWindowState.Minimized;
    }

    private void RestoreWindowAfterMinimize()
    {
        if (closing || WindowState != FormWindowState.Normal) return;
        sentToBackground = false;
        nativeMoveOrResize = false;
        TopMost = settings.AlwaysOnTop;
        lastCursorPosition = Cursor.Position;
        lastCursorMovement = DateTime.UtcNow;
        if (toolbar is not null && !toolbar.IsDisposed && Bounds.Contains(Cursor.Position)) toolbar.Show(this);
        PositionOverlays();
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
    private void RestartMotionIntegration()
    {
        StopMotionIntegration();
        if (closing || !settings.MotionDetectionEnabled) return;
        motionCancellation = new CancellationTokenSource();
        if (settings.DirectHomeAssistantEnabled &&
            !string.IsNullOrWhiteSpace(settings.HomeAssistantUrl) &&
            !string.IsNullOrWhiteSpace(settings.HomeAssistantToken) &&
            settings.Cameras.Any(camera => camera.MotionEnabled && !string.IsNullOrWhiteSpace(camera.MotionEntityId)))
        {
            _ = Task.Run(() => ListenToHomeAssistantAsync(motionCancellation.Token));
            return;
        }
        StartMotionListener();
    }

    private void StopMotionIntegration()
    {
        try { motionCancellation.Cancel(); } catch { }
        try { motionListener?.Stop(); } catch { }
        motionListener = null;
        try { homeAssistantSocket?.Abort(); homeAssistantSocket?.Dispose(); } catch { }
        homeAssistantSocket = null;
        motionCancellation.Dispose();
    }

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

    private async Task ListenToHomeAssistantAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await ConnectAndMonitorHomeAssistantAsync(cancellationToken);
            }
            catch (OperationCanceledException) { break; }
            catch (ObjectDisposedException) { break; }
            catch
            {
                if (cancellationToken.IsCancellationRequested) break;
                try { await Task.Delay(5000, cancellationToken); } catch (OperationCanceledException) { break; }
            }
        }
    }

    private async Task ConnectAndMonitorHomeAssistantAsync(CancellationToken cancellationToken)
    {
        using var socket = new ClientWebSocket();
        ConfigureHomeAssistantSocket(socket, settings.IgnoreHomeAssistantCertificateErrors);
        homeAssistantSocket = socket;
        await socket.ConnectAsync(BuildHomeAssistantWebSocketUri(settings.HomeAssistantUrl), cancellationToken);

        using (var authRequired = await ReceiveHomeAssistantMessageAsync(socket, cancellationToken))
        {
            if (!authRequired.RootElement.TryGetProperty("type", out var type) || type.GetString() != "auth_required")
                throw new InvalidDataException("Home Assistant erwartet keine Anmeldung.");
        }

        await SendHomeAssistantMessageAsync(socket, new { type = "auth", access_token = settings.HomeAssistantToken }, cancellationToken);
        using (var authResult = await ReceiveHomeAssistantMessageAsync(socket, cancellationToken))
        {
            if (!authResult.RootElement.TryGetProperty("type", out var type) || type.GetString() != "auth_ok")
                throw new UnauthorizedAccessException("Home-Assistant-Anmeldung fehlgeschlagen.");
        }

        await SendHomeAssistantMessageAsync(socket, new { id = 1, type = "subscribe_events", event_type = "state_changed" }, cancellationToken);
        while (socket.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
        {
            using var message = await ReceiveHomeAssistantMessageAsync(socket, cancellationToken);
            if (!TryGetMotionCamera(message.RootElement, out var cameraName)) continue;
            if (!closing) BeginInvoke(new Action(() => HandleMotion(cameraName)));
        }
    }

    internal static Uri BuildHomeAssistantWebSocketUri(string address)
    {
        var source = new Uri(address.Trim().TrimEnd('/'), UriKind.Absolute);
        var builder = new UriBuilder(source)
        {
            Scheme = source.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase) ? "wss" : "ws",
            Path = source.AbsolutePath.TrimEnd('/') + "/api/websocket"
        };
        return builder.Uri;
    }

    private static void ConfigureHomeAssistantSocket(ClientWebSocket socket, bool ignoreCertificateErrors)
    {
        if (ignoreCertificateErrors)
            socket.Options.RemoteCertificateValidationCallback = (_, _, _, _) => true;
    }

    internal static async Task SendHomeAssistantMessageAsync(ClientWebSocket socket, object message, CancellationToken cancellationToken)
    {
        var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(message));
        await socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, cancellationToken);
    }

    internal static async Task<JsonDocument> ReceiveHomeAssistantMessageAsync(ClientWebSocket socket, CancellationToken cancellationToken)
    {
        using var content = new MemoryStream();
        var buffer = new byte[4096];
        WebSocketReceiveResult result;
        do
        {
            result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);
            if (result.MessageType == WebSocketMessageType.Close) throw new IOException("Home Assistant hat die Verbindung beendet.");
            content.Write(buffer, 0, result.Count);
        } while (!result.EndOfMessage);
        content.Position = 0;
        return await JsonDocument.ParseAsync(content, cancellationToken: cancellationToken);
    }

    internal static async Task<string?> TestHomeAssistantConnectionAsync(string address, string token, string entityId, bool ignoreCertificateErrors)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(12));
        try
        {
            using var socket = new ClientWebSocket();
            ConfigureHomeAssistantSocket(socket, ignoreCertificateErrors);
            await socket.ConnectAsync(BuildHomeAssistantWebSocketUri(address), timeout.Token);

            using (var authRequired = await ReceiveHomeAssistantMessageAsync(socket, timeout.Token))
            {
                if (!authRequired.RootElement.TryGetProperty("type", out var type) || type.GetString() != "auth_required")
                    return "Unerwartete Antwort von Home Assistant.";
            }

            await SendHomeAssistantMessageAsync(socket, new { type = "auth", access_token = token }, timeout.Token);
            using (var authResult = await ReceiveHomeAssistantMessageAsync(socket, timeout.Token))
            {
                if (!authResult.RootElement.TryGetProperty("type", out var type) || type.GetString() != "auth_ok")
                    return "Anmeldung fehlgeschlagen – bitte Langzeit-Token prüfen.";
            }

            await SendHomeAssistantMessageAsync(socket, new { id = 1, type = "get_states" }, timeout.Token);
            using var states = await ReceiveHomeAssistantMessageAsync(socket, timeout.Token);
            if (!states.RootElement.TryGetProperty("success", out var success) || !success.GetBoolean())
                return "Home Assistant konnte die Entitäten nicht liefern.";
            if (!states.RootElement.TryGetProperty("result", out var result) || result.ValueKind != JsonValueKind.Array ||
                !result.EnumerateArray().Any(state => state.TryGetProperty("entity_id", out var id) &&
                    string.Equals(id.GetString(), entityId.Trim(), StringComparison.OrdinalIgnoreCase)))
                return $"Entität nicht gefunden: {entityId.Trim()}";
            return null;
        }
        catch (OperationCanceledException) { return "Zeitüberschreitung – HA-Adresse oder Netzwerk prüfen."; }
        catch (Exception exception)
        {
            var detail = exception.InnerException?.Message ?? exception.Message;
            return detail.Contains("certificate", StringComparison.OrdinalIgnoreCase) || detail.Contains("Zertifikat", StringComparison.OrdinalIgnoreCase)
                ? "Zertifikatsfehler – gültigen HA-Namen verwenden oder die lokale Zertifikatsausnahme aktivieren."
                : detail;
        }
    }

    private bool TryGetMotionCamera(JsonElement root, out string cameraName)
    {
        cameraName = "";
        if (!root.TryGetProperty("type", out var type) || type.GetString() != "event" ||
            !root.TryGetProperty("event", out var eventElement) ||
            !eventElement.TryGetProperty("data", out var data) ||
            !data.TryGetProperty("entity_id", out var entity) ||
            !data.TryGetProperty("new_state", out var newState) || newState.ValueKind == JsonValueKind.Null ||
            !newState.TryGetProperty("state", out var newValue) || newValue.GetString() != "on") return false;

        if (data.TryGetProperty("old_state", out var oldState) && oldState.ValueKind != JsonValueKind.Null &&
            oldState.TryGetProperty("state", out var oldValue) && oldValue.GetString() == "on") return false;

        var matchingCamera = settings.Cameras.FirstOrDefault(camera => camera.MotionEnabled &&
            !string.IsNullOrWhiteSpace(camera.MotionEntityId) &&
            string.Equals(camera.MotionEntityId.Trim(), entity.GetString(), StringComparison.OrdinalIgnoreCase));
        if (matchingCamera is null) return false;
        cameraName = matchingCamera.Name;
        return true;
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
                var matchingCamera = settings.Cameras.FirstOrDefault(item => item.MotionEnabled && string.Equals(camera, item.Name, StringComparison.OrdinalIgnoreCase));
                var accepted = matchingCamera is not null;
                if (accepted && !closing) BeginInvoke(new Action(() => HandleMotion(matchingCamera!.Name)));
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
        if (cameraIndex < 0) { toolbar?.Flash($"{cameraName} fehlt"); return; }
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
        var activeBeforeShow = NativeMethods.GetForegroundWindow();
        sentToBackground = false;
        NativeMethods.ShowWindowAsync(Handle, NativeMethods.SwShowNoActivate);
        TopMost = true;
        NativeMethods.SetWindowPos(Handle, NativeMethods.HwndTopMost, 0, 0, 0, 0,
            NativeMethods.SwpNoMove | NativeMethods.SwpNoSize | NativeMethods.SwpNoActivate | NativeMethods.SwpShowWindow);
        PositionOverlays();
        BeginInvoke(new Action(() =>
        {
            if (activeBeforeShow != IntPtr.Zero && activeBeforeShow != Handle && NativeMethods.GetForegroundWindow() == Handle)
                NativeMethods.SetForegroundWindow(activeBeforeShow);
        }));
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

internal sealed class HomeCamDarkMenuRenderer : ToolStripProfessionalRenderer
{
    public HomeCamDarkMenuRenderer() : base(new HomeCamDarkColorTable()) { RoundedEdges = true; }

    protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs eventArgs)
    {
        eventArgs.TextColor = eventArgs.Item?.Enabled != false ? Color.White : Color.FromArgb(125, 125, 130);
        base.OnRenderItemText(eventArgs);
    }

    protected override void OnRenderArrow(ToolStripArrowRenderEventArgs eventArgs)
    {
        eventArgs.ArrowColor = eventArgs.Item?.Enabled != false ? Color.White : Color.FromArgb(125, 125, 130);
        base.OnRenderArrow(eventArgs);
    }

    protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs eventArgs)
    {
        var rectangle = eventArgs.ImageRectangle;
        using var pen = new Pen(Color.White, 2f)
        {
            StartCap = System.Drawing.Drawing2D.LineCap.Round,
            EndCap = System.Drawing.Drawing2D.LineCap.Round
        };
        eventArgs.Graphics.DrawLines(pen,
        new Point[]
        {
            new Point(rectangle.Left + 3, rectangle.Top + rectangle.Height / 2),
            new Point(rectangle.Left + 7, rectangle.Bottom - 4),
            new Point(rectangle.Right - 2, rectangle.Top + 3)
        });
    }
}

internal sealed class HomeCamDarkColorTable : ProfessionalColorTable
{
    private static readonly Color Background = Color.FromArgb(28, 28, 31);
    private static readonly Color Hover = Color.FromArgb(58, 58, 64);
    public override Color ToolStripDropDownBackground => Background;
    public override Color MenuBorder => Color.FromArgb(78, 78, 84);
    public override Color MenuItemBorder => Color.FromArgb(82, 82, 90);
    public override Color MenuItemSelected => Hover;
    public override Color MenuItemSelectedGradientBegin => Hover;
    public override Color MenuItemSelectedGradientEnd => Hover;
    public override Color MenuItemPressedGradientBegin => Hover;
    public override Color MenuItemPressedGradientEnd => Hover;
    public override Color ImageMarginGradientBegin => Background;
    public override Color ImageMarginGradientMiddle => Background;
    public override Color ImageMarginGradientEnd => Background;
    public override Color SeparatorDark => Color.FromArgb(72, 72, 78);
    public override Color SeparatorLight => Color.FromArgb(72, 72, 78);
    public override Color CheckBackground => Hover;
    public override Color CheckSelectedBackground => Hover;
    public override Color CheckPressedBackground => Hover;
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
    private readonly ToolTip toolTips = new()
    {
        InitialDelay = 450,
        ReshowDelay = 100,
        AutoPopDelay = 5000,
        ShowAlways = true
    };
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
        var settings = Item("\uE713", 160, (_, _) => monitor.OpenSettings());
        settings.Font = new Font("Segoe MDL2 Assets", 14);
#if BETA
        var lastAction = Item("↓", 192, (_, _) => monitor.MinimizeWindow());
#else
        var lastAction = Item("⛶", 192, (_, _) => monitor.ToggleFullscreen());
#endif
        var close = Item("\uE8BB", 224, (_, _) => monitor.Close());
        close.Font = new Font("Segoe MDL2 Assets", 13);
        foreach (var icon in new[] { snapshot, settings, lastAction, close })
        {
            icon.Top = 0;
            icon.Height = 34;
            icon.TextAlign = ContentAlignment.MiddleCenter;
        }
        Controls.AddRange([previous, name, next, snapshot, settings, lastAction, close]);
        toolTips.SetToolTip(previous, "Vorherige Kamera");
        toolTips.SetToolTip(name, "Aktuelle Kamera");
        toolTips.SetToolTip(next, "Nächste Kamera");
        toolTips.SetToolTip(snapshot, "Snapshot speichern");
        toolTips.SetToolTip(settings, "Einstellungen öffnen");
#if BETA
        toolTips.SetToolTip(lastAction, "Minimieren");
#else
        toolTips.SetToolTip(lastAction, "Vollbild ein/aus");
#endif
        toolTips.SetToolTip(close, "HomeCam Monitor beenden");
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
    public enum DwmWindowAttribute { UseImmersiveDarkMode = 20, WindowCornerPreference = 33 }
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
    private readonly CheckBox directHomeAssistant = new() { Text = "Direkt mit Home Assistant verbinden (empfohlen)", AutoSize = true };
    private readonly TextBox homeAssistantUrl = new() { Width = 300 };
    private readonly TextBox homeAssistantToken = new() { Width = 300, UseSystemPasswordChar = true };
    private readonly TextBox motionEntityId = new() { Width = 300 };
    private readonly Label selectedMotionCamera = new() { AutoSize = true, Text = "Keine Kamera ausgewählt", Anchor = AnchorStyles.Left };
    private readonly CheckBox ignoreHomeAssistantCertificateErrors = new() { Text = "Ungültiges HA-Zertifikat erlauben (nur lokales Netzwerk)", AutoSize = true };
    private readonly Button testHomeAssistant = new() { Text = "Verbindung testen", AutoSize = true };
    private readonly Label homeAssistantStatus = new() { AutoSize = true, MaximumSize = new Size(600, 0), Margin = new Padding(10, 6, 3, 0) };
#endif
    public Settings Result { get; private set; }
    public SettingsForm(Settings current)
    {
        Result = current; Text = "HomeCam Monitor – Einstellungen"; StartPosition = FormStartPosition.CenterParent; MinimizeBox = false;
#if BETA
        FormBorderStyle = FormBorderStyle.Sizable;
        MaximizeBox = true;
        MinimumSize = new Size(780, 650);
        var workingArea = Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 1024, 768);
        ClientSize = new Size(Math.Min(840, workingArea.Width - 40), Math.Min(680, workingArea.Height - 60));
        cameras.MinimumSize = new Size(0, 170);
        BackColor = Color.FromArgb(24, 24, 27);
        ForeColor = Color.FromArgb(242, 242, 244);
        Opacity = 0.97;
        Shown += (_, _) =>
        {
            var darkTitleBar = 1;
            NativeMethods.DwmSetWindowAttribute(Handle, NativeMethods.DwmWindowAttribute.UseImmersiveDarkMode,
                ref darkTitleBar, sizeof(int));
            Activate();
            BringToFront();
        };
#else
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        ClientSize = new Size(760, 390);
#endif
        cameras.Columns.Add(new DataGridViewTextBoxColumn { Name = "CameraName", HeaderText = "Name", FillWeight = 24 });
        cameras.Columns.Add(new DataGridViewTextBoxColumn { Name = "StreamUrl", HeaderText = "RTSP-/HTTP-Streamadresse", FillWeight = 64 });
#if BETA
        cameras.Columns.Add(new DataGridViewCheckBoxColumn { Name = "MotionEnabled", HeaderText = "Bewegung", FillWeight = 12 });
        cameras.Columns.Add(new DataGridViewTextBoxColumn { Name = "MotionEntityId", Visible = false });
        foreach (var camera in current.Cameras) cameras.Rows.Add(camera.Name, camera.StreamUrl, camera.MotionEnabled, camera.MotionEntityId);
#else
        foreach (var camera in current.Cameras) cameras.Rows.Add(camera.Name, camera.StreamUrl);
#endif
        top.Checked = current.AlwaysOnTop; autostart.Checked = current.StartWithWindows;
#if BETA
        motionDetection.Checked = current.MotionDetectionEnabled;
        minimizeWhenInactive.Checked = current.MinimizeWhenInactive;
        motionSeconds.Value = Math.Clamp(current.MotionForegroundSeconds, 3, 300);
        directHomeAssistant.Checked = current.DirectHomeAssistantEnabled;
        homeAssistantUrl.Text = current.HomeAssistantUrl;
        homeAssistantToken.Text = current.HomeAssistantToken;
        ignoreHomeAssistantCertificateErrors.Checked = current.IgnoreHomeAssistantCertificateErrors;
        var selectedCameraRow = -1;
        void StoreSelectedCameraMotion()
        {
            if (selectedCameraRow >= 0 && selectedCameraRow < cameras.Rows.Count && !cameras.Rows[selectedCameraRow].IsNewRow)
                cameras.Rows[selectedCameraRow].Cells[3].Value = motionEntityId.Text.Trim();
        }
        void LoadSelectedCameraMotion()
        {
            StoreSelectedCameraMotion();
            selectedCameraRow = cameras.CurrentRow?.Index ?? -1;
            if (selectedCameraRow < 0 || selectedCameraRow >= cameras.Rows.Count || cameras.Rows[selectedCameraRow].IsNewRow)
            {
                selectedMotionCamera.Text = "Keine Kamera ausgewählt";
                motionEntityId.Text = "";
                return;
            }
            selectedMotionCamera.Text = Convert.ToString(cameras.Rows[selectedCameraRow].Cells[0].Value)?.Trim() is { Length: > 0 } name ? name : "Neue Kamera";
            motionEntityId.Text = Convert.ToString(cameras.Rows[selectedCameraRow].Cells[3].Value)?.Trim() ?? "";
        }
        cameras.SelectionChanged += (_, _) => LoadSelectedCameraMotion();
        cameras.CurrentCellDirtyStateChanged += (_, _) => { if (cameras.IsCurrentCellDirty) cameras.CommitEdit(DataGridViewDataErrorContexts.Commit); };
        if (cameras.Rows.Count > 0) { cameras.Rows[0].Selected = true; cameras.CurrentCell = cameras.Rows[0].Cells[0]; }
        LoadSelectedCameraMotion();
        void UpdateMotionOptions()
        {
            var enabled = motionDetection.Checked && !top.Checked;
            motionSeconds.Enabled = enabled;
            minimizeWhenInactive.Enabled = enabled;
            directHomeAssistant.Enabled = motionDetection.Checked;
            var directEnabled = motionDetection.Checked && directHomeAssistant.Checked;
            homeAssistantUrl.Enabled = directEnabled;
            homeAssistantToken.Enabled = directEnabled;
            motionEntityId.Enabled = directEnabled;
            ignoreHomeAssistantCertificateErrors.Enabled = directEnabled;
            testHomeAssistant.Enabled = directEnabled;
        }
        UpdateMotionOptions();
        top.CheckedChanged += (_, _) => UpdateMotionOptions();
        motionDetection.CheckedChanged += (_, _) => UpdateMotionOptions();
        directHomeAssistant.CheckedChanged += (_, _) => UpdateMotionOptions();
#endif
#if BETA
        var table = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(14), ColumnCount = 1, RowCount = 6 };
        table.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        for (var row = 1; row < table.RowCount; row++) table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
#else
        var table = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(14), ColumnCount = 1, RowCount = 5 };
        table.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
#endif
        table.Controls.Add(cameras, 0, 0);
#if BETA
        table.Controls.Add(new Label { Text = "Kamera anklicken, Bewegungs-Entität unten eintragen und die Spalte Bewegung aktivieren.", AutoSize = true, ForeColor = SystemColors.GrayText }, 0, 1);
#else
        table.Controls.Add(new Label { Text = "Beispiel: rtsp://192.168.x.x:8554/Einfahrt", AutoSize = true, ForeColor = SystemColors.GrayText }, 0, 1);
#endif
        var options = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true }; options.Controls.Add(top); options.Controls.Add(autostart);
#if BETA
        options.Controls.Add(motionDetection);
        options.Controls.Add(minimizeWhenInactive);
        options.Controls.Add(new Label { Text = "Vordergrunddauer:", AutoSize = true, Margin = new Padding(18, 4, 3, 0) });
        options.Controls.Add(motionSeconds);
        options.Controls.Add(new Label { Text = "Sekunden", AutoSize = true, Margin = new Padding(3, 4, 3, 0) });
#endif
        table.Controls.Add(options, 0, 2);
#if BETA
        var homeAssistantGroup = new GroupBox { Text = "Bewegung direkt aus Home Assistant", Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(10) };
        var homeAssistantFields = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2, RowCount = 7 };
        homeAssistantFields.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        homeAssistantFields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        homeAssistantFields.Controls.Add(directHomeAssistant, 0, 0);
        homeAssistantFields.SetColumnSpan(directHomeAssistant, 2);
        homeAssistantFields.Controls.Add(new Label { Text = "HA-Adresse:", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 1);
        homeAssistantFields.Controls.Add(homeAssistantUrl, 1, 1);
        homeAssistantFields.Controls.Add(new Label { Text = "Langzeit-Token:", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 2);
        homeAssistantFields.Controls.Add(homeAssistantToken, 1, 2);
        homeAssistantFields.Controls.Add(new Label { Text = "Bewegungs-Entität:", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 3);
        homeAssistantFields.Controls.Add(motionEntityId, 1, 3);
        homeAssistantFields.Controls.Add(new Label { Text = "Ausgewählte Kamera:", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 4);
        homeAssistantFields.Controls.Add(selectedMotionCamera, 1, 4);
        homeAssistantFields.Controls.Add(ignoreHomeAssistantCertificateErrors, 0, 5);
        homeAssistantFields.SetColumnSpan(ignoreHomeAssistantCertificateErrors, 2);
        var testRow = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, WrapContents = false };
        testRow.Controls.Add(testHomeAssistant);
        testRow.Controls.Add(homeAssistantStatus);
        homeAssistantFields.Controls.Add(testRow, 0, 6);
        homeAssistantFields.SetColumnSpan(testRow, 2);
        homeAssistantGroup.Controls.Add(homeAssistantFields);
        table.Controls.Add(homeAssistantGroup, 0, 3);
#endif
#if BETA
        table.Controls.Add(new Label { Text = $"Version {Application.ProductVersion.Split('+')[0]}", AutoSize = true, ForeColor = SystemColors.GrayText, Anchor = AnchorStyles.Left }, 0, 4);
#else
        table.Controls.Add(new Label { Text = $"Version {Application.ProductVersion.Split('+')[0]}", AutoSize = true, ForeColor = SystemColors.GrayText, Anchor = AnchorStyles.Left }, 0, 3);
#endif
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft }; var ok = new Button { Text = "Speichern", DialogResult = DialogResult.OK, AutoSize = true };
        buttons.Controls.Add(ok); buttons.Controls.Add(new Button { Text = "Abbrechen", DialogResult = DialogResult.Cancel, AutoSize = true });
#if BETA
        table.Controls.Add(buttons, 0, 5);
#else
        table.Controls.Add(buttons, 0, 4);
#endif
        Controls.Add(table); AcceptButton = ok; CancelButton = buttons.Controls[1] as Button;
#if BETA
        ApplyDarkTheme(this);
#endif
#if BETA
        testHomeAssistant.Click += async (_, _) =>
        {
            homeAssistantStatus.ForeColor = SystemColors.GrayText;
            homeAssistantStatus.Text = "Verbindung wird geprüft …";
            testHomeAssistant.Enabled = false;
            try
            {
                if (!Uri.TryCreate(homeAssistantUrl.Text.Trim(), UriKind.Absolute, out var testUri) ||
                    (testUri.Scheme != Uri.UriSchemeHttp && testUri.Scheme != Uri.UriSchemeHttps) ||
                    string.IsNullOrWhiteSpace(homeAssistantToken.Text) || string.IsNullOrWhiteSpace(motionEntityId.Text))
                {
                    homeAssistantStatus.ForeColor = Color.Firebrick;
                    homeAssistantStatus.Text = "Adresse, Token und Entität vollständig eintragen.";
                    return;
                }
                var error = await MonitorForm.TestHomeAssistantConnectionAsync(homeAssistantUrl.Text.Trim(), homeAssistantToken.Text.Trim(),
                    motionEntityId.Text.Trim(), ignoreHomeAssistantCertificateErrors.Checked);
                homeAssistantStatus.ForeColor = error is null ? Color.ForestGreen : Color.Firebrick;
                homeAssistantStatus.Text = error is null ? "Verbunden – Bewegungssensor gefunden." : error;
            }
            finally { testHomeAssistant.Enabled = directHomeAssistant.Checked && motionDetection.Checked; }
        };
#endif
        ok.Click += (_, _) =>
        {
#if BETA
            StoreSelectedCameraMotion();
#endif
            var entries = ReadCameras();
            if (entries.Count == 0 || entries.Any(c => !Uri.TryCreate(c.StreamUrl, UriKind.Absolute, out _)))
            {
                MessageBox.Show(this, "Bitte gültige Streamadressen eintragen.", "Ungültige Kamera", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                DialogResult = DialogResult.None; return;
            }
#if BETA
            if (directHomeAssistant.Checked &&
                (!Uri.TryCreate(homeAssistantUrl.Text.Trim(), UriKind.Absolute, out var haUri) ||
                 (haUri.Scheme != Uri.UriSchemeHttp && haUri.Scheme != Uri.UriSchemeHttps) ||
                 string.IsNullOrWhiteSpace(homeAssistantToken.Text) ||
                 !entries.Any(camera => camera.MotionEnabled && !string.IsNullOrWhiteSpace(camera.MotionEntityId))))
            {
                MessageBox.Show(this, "Bitte HA-Adresse und Langzeit-Token eintragen sowie für mindestens eine aktivierte Kamera eine Bewegungs-Entität hinterlegen.", "Home Assistant unvollständig", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                DialogResult = DialogResult.None; return;
            }
#endif
            Result = new Settings
            {
                Cameras = entries, SelectedCamera = Math.Clamp(current.SelectedCamera, 0, entries.Count - 1),
                AlwaysOnTop = top.Checked, StartWithWindows = autostart.Checked,
                Left = current.Left, Top = current.Top, Width = current.Width, Height = current.Height,
#if BETA
                MotionDetectionEnabled = motionDetection.Checked,
                MotionForegroundSeconds = (int)motionSeconds.Value,
                MinimizeWhenInactive = minimizeWhenInactive.Checked,
                DirectHomeAssistantEnabled = directHomeAssistant.Checked,
                HomeAssistantUrl = homeAssistantUrl.Text.Trim(),
                HomeAssistantToken = homeAssistantToken.Text.Trim(),
                IgnoreHomeAssistantCertificateErrors = ignoreHomeAssistantCertificateErrors.Checked,
                PerCameraMotionConfigured = true
#endif
            };
        };
    }
    private List<CameraEntry> ReadCameras()
    {
        var result = new List<CameraEntry>();
        foreach (DataGridViewRow row in cameras.Rows)
        {
            if (row.IsNewRow) continue;
            var name = Convert.ToString(row.Cells[0].Value)?.Trim() ?? "";
            var url = Convert.ToString(row.Cells[1].Value)?.Trim() ?? "";
            if (name.Length == 0 && url.Length == 0) continue;
            var entry = new CameraEntry { Name = name, StreamUrl = url };
#if BETA
            entry.MotionEnabled = Convert.ToBoolean(row.Cells[2].Value ?? false);
            entry.MotionEntityId = Convert.ToString(row.Cells[3].Value)?.Trim() ?? "";
#endif
            result.Add(entry);
        }
        return result;
    }
#if BETA
    private static void ApplyDarkTheme(Control root)
    {
        var background = Color.FromArgb(24, 24, 27);
        var surface = Color.FromArgb(35, 35, 39);
        var field = Color.FromArgb(43, 43, 48);
        var border = Color.FromArgb(72, 72, 78);
        var text = Color.FromArgb(242, 242, 244);
        var muted = Color.FromArgb(164, 164, 170);

        root.BackColor = background;
        root.ForeColor = text;
        foreach (Control control in root.Controls)
        {
            switch (control)
            {
                case DataGridView grid:
                    grid.EnableHeadersVisualStyles = false;
                    grid.BackgroundColor = surface;
                    grid.BorderStyle = BorderStyle.FixedSingle;
                    grid.GridColor = border;
                    grid.DefaultCellStyle.BackColor = field;
                    grid.DefaultCellStyle.ForeColor = text;
                    grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(70, 70, 78);
                    grid.DefaultCellStyle.SelectionForeColor = Color.White;
                    grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(31, 31, 35);
                    grid.ColumnHeadersDefaultCellStyle.ForeColor = text;
                    grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = Color.FromArgb(31, 31, 35);
                    grid.RowHeadersDefaultCellStyle.BackColor = Color.FromArgb(31, 31, 35);
                    break;
                case TextBox textBox:
                    textBox.BackColor = field;
                    textBox.ForeColor = text;
                    textBox.BorderStyle = BorderStyle.FixedSingle;
                    break;
                case NumericUpDown numeric:
                    numeric.BackColor = field;
                    numeric.ForeColor = text;
                    numeric.BorderStyle = BorderStyle.FixedSingle;
                    break;
                case Button button:
                    button.FlatStyle = FlatStyle.Flat;
                    button.BackColor = Color.FromArgb(48, 48, 54);
                    button.ForeColor = text;
                    button.FlatAppearance.BorderColor = border;
                    button.FlatAppearance.MouseOverBackColor = Color.FromArgb(62, 62, 69);
                    button.FlatAppearance.MouseDownBackColor = Color.FromArgb(72, 72, 80);
                    break;
                case CheckBox checkBox:
                    checkBox.FlatStyle = FlatStyle.Flat;
                    checkBox.BackColor = background;
                    checkBox.ForeColor = text;
                    break;
                case GroupBox groupBox:
                    groupBox.BackColor = background;
                    groupBox.ForeColor = text;
                    break;
                case Label label:
                    label.BackColor = Color.Transparent;
                    label.ForeColor = label.ForeColor == SystemColors.GrayText ? muted : text;
                    break;
                default:
                    control.BackColor = background;
                    control.ForeColor = text;
                    break;
            }
            if (control.HasChildren) ApplyDarkTheme(control);
        }
    }
#endif
}
