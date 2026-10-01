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
    public int Height { get; set; } = 270;
#if BETA
    public string LastMonitorDeviceName { get; set; } = "";
    public int MonitorOffsetX { get; set; }
    public int MonitorOffsetY { get; set; }
    public string StartBehavior { get; set; } = "Last";
    public int StartCameraIndex { get; set; }
    public bool LastGridMode { get; set; }
    public int ToolbarSizePercent { get; set; } = 100;
    public bool AutoScaleToolbar { get; set; }
    public bool MotionDetectionEnabled { get; set; } = true;
    public DateTime? MotionActionsPausedUntilUtc { get; set; }
    public int MotionForegroundSeconds { get; set; } = 10;
    public int MotionIndicatorSeconds { get; set; } = 2;
    public bool HighlightMotionInGrid { get; set; }
    public bool MinimizeWhenInactive { get; set; }
    public bool RestorePreviousCameraAfterMotion { get; set; } = true;
    public bool DirectHomeAssistantEnabled { get; set; }
    public string HomeAssistantUrl { get; set; } = "http://192.168.9.8:8123";
    public string HomeAssistantToken { get; set; } = "";
    public string MotionEntityId { get; set; } = "binary_sensor.camera_einfahrt_bewegung";
    public string MotionCameraName { get; set; } = "Einfahrt";
    public bool IgnoreHomeAssistantCertificateErrors { get; set; }
    public bool PerCameraMotionConfigured { get; set; }
    public int MotionRetentionDays { get; set; } = 7;
    public int SettingsWindowWidth { get; set; } = 980;
    public int SettingsWindowHeight { get; set; } = 780;
#endif
}

internal sealed class CameraEntry
{
    public string Name { get; set; } = "Kamera";
    public string StreamUrl { get; set; } = "";
#if BETA
    public bool MotionEnabled { get; set; }
    // Null preserves the enabled state of person sensors configured before the separate checkbox existed.
    public bool? PersonEnabled { get; set; }
    public string MotionEntityId { get; set; } = "";
    public string PersonEntityId { get; set; } = "";
    public string MotionAction { get; set; } = "None";
    public int MotionVideoSeconds { get; set; } = 30;
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
    private Panel video = new() { Dock = DockStyle.Fill, BackColor = Color.Black };
#if BETA
    private Panel standbyVideo = new() { Dock = DockStyle.Fill, BackColor = Color.Black };
#endif
    private readonly System.Windows.Forms.Timer latencyTimer = new() { Interval = 5 * 60 * 1000 };
    private readonly System.Windows.Forms.Timer restartTimer = new() { Interval = 2000 };
    private readonly System.Windows.Forms.Timer controlsTimer = new() { Interval = 150 };
    private string pipeName = $"HomeCamMonitor-{Environment.ProcessId}";
#if BETA
    private readonly string recordingPipeName = $"HomeCamMonitor-Recording-{Environment.ProcessId}";
    private readonly Dictionary<string, DateTime> lastMotionCapture = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> activeMotionCapture = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<Process> motionProcesses = [];
    private readonly Panel cameraGrid = new()
    {
        Dock = DockStyle.Fill,
        BackColor = Color.Black,
        Padding = new Padding(0),
        Visible = false
    };
    private readonly List<GridPlayerSlot> gridSlots = [];
    private readonly System.Windows.Forms.Timer cameraLayoutTimer = new() { Interval = 250 };
    private CancellationTokenSource? refreshCancellation;
#endif
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
    private bool recording;
    private int activeMotionRecordings;
    private string? recordingPath;
    private Process? recordingPlayer;
    private int? cameraBeforeMotion;
    private bool gridMode;
    private bool intentionalGridStop;
    private bool wasMinimized;
    private bool sentToBackground;
    private DateTime sentToBackgroundAt = DateTime.MinValue;
    private ContextMenuStrip? cameraContextMenu;
    private MotionIndicatorForm? motionIndicator;
    private int gridHighlightedCameraIndex = -1;
    private SettingsForm? activeSettingsDialog;
    private bool motionIndicatorVisible;
#endif
    private Point lastCursorPosition;
    private DateTime lastCursorMovement = DateTime.UtcNow;
    private Rectangle windowedBounds;
#if BETA
    private CancellationTokenSource motionCancellation = new();
    private readonly System.Windows.Forms.Timer motionRestoreTimer = new();
    private readonly System.Windows.Forms.Timer motionIndicatorTimer = new();
    private readonly System.Windows.Forms.Timer motionPauseTimer = new();
    private TcpListener? motionListener;
    private ClientWebSocket? homeAssistantSocket;
    private IntPtr previousForegroundWindow;
#endif

    public MonitorForm()
    {
        settings = SettingsStore.Load();
#if BETA
        DeleteExpiredMotionFiles(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyVideos), "HomeCam Monitor", "Bewegung"), settings.MotionRetentionDays);
        Text = "HomeCamMonitor for Homeassistant Beta";
#else
        Text = "HomeCamMonitor for Homeassistant";
#endif
        BackColor = Color.Black;
        FormBorderStyle = FormBorderStyle.None;
        MinimumSize = new Size(240, 150);
        var initialWidth = Math.Max(240, settings.Width);
        var initialHeight = Math.Max(150, settings.Height);
        ClientSize = new Size(initialWidth, initialHeight);
#if BETA
        StartPosition = FormStartPosition.Manual;
        Bounds = RestoreWindowBounds(settings, Screen.AllScreens.Select(screen => (screen.DeviceName, screen.WorkingArea)).ToArray());
#else
        if (settings.Left >= 0 && settings.Top >= 0) { StartPosition = FormStartPosition.Manual; Location = new Point(settings.Left, settings.Top); }
#endif
#if BETA
        TopMost = settings.AlwaysOnTop;
#else
        TopMost = true;
#endif
#if BETA
        Controls.Add(standbyVideo);
#endif
        Controls.Add(video);
#if BETA
        video.BringToFront();
        InitializeCameraGrid();
        Controls.Add(cameraGrid);
#endif
        Shown += (_, _) =>
        {
            InitializeMonitor();
#if BETA
            if (!closing && !IsDisposed)
            {
                AlignCameraSurfaces();
                ScheduleCameraLayout();
            }
#endif
        };
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
#if BETA
            AlignCameraSurfaces();
            ScheduleCameraLayout();
            UpdateToolbarScale();
#endif
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
        video.MouseDoubleClick += (sender, eventArgs) => HandleSurfaceDoubleClick(((Control)sender!).PointToScreen(eventArgs.Location));
#if BETA
        standbyVideo.MouseDoubleClick += (sender, eventArgs) => HandleSurfaceDoubleClick(((Control)sender!).PointToScreen(eventArgs.Location));
#endif
#if BETA
        latencyTimer.Tick += async (_, _) => await RefreshSeamlesslyAsync();
#else
        latencyTimer.Tick += (_, _) => RestartPlayer();
#endif
        restartTimer.Tick += (_, _) => { restartTimer.Stop(); StartPlayer(); };
        controlsTimer.Tick += (_, _) => UpdateToolbarVisibility();
#if BETA
        motionRestoreTimer.Interval = Math.Clamp(settings.MotionForegroundSeconds, 3, 300) * 1000;
        motionRestoreTimer.Tick += (_, _) => RestoreAfterMotion();
        motionIndicatorTimer.Interval = Math.Clamp(settings.MotionIndicatorSeconds, 1, 10) * 1000;
        motionIndicatorTimer.Tick += (_, _) => HideMotionIndicator();
        motionPauseTimer.Tick += (_, _) => ScheduleMotionPauseExpiry();
        ScheduleMotionPauseExpiry();
        cameraLayoutTimer.Tick += (_, _) =>
        {
            cameraLayoutTimer.Stop();
            AlignCameraSurfaces();
        };
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
        toolbar = new ToolbarForm(this);
#if BETA
        toolbar.SetSizePercent(GetToolbarSizePercent(settings, Width));
#endif
        toolbar.Show(this); CreateResizeGrips();
#if BETA
        cameraContextMenu = CreateCameraContextMenu();
        video.ContextMenuStrip = cameraContextMenu;
        standbyVideo.ContextMenuStrip = cameraContextMenu;
        cameraGrid.ContextMenuStrip = cameraContextMenu;
        foreach (var slot in gridSlots)
        {
            slot.Host.ContextMenuStrip = cameraContextMenu;
            slot.ActiveSurface.ContextMenuStrip = cameraContextMenu;
            slot.SpareSurface.ContextMenuStrip = cameraContextMenu;
            slot.Name.ContextMenuStrip = cameraContextMenu;
        }
        dragSurface.ContextMenuStrip = cameraContextMenu;
        toolbar.ContextMenuStrip = cameraContextMenu;
        foreach (var resizeGrip in resizeGrips) resizeGrip.ContextMenuStrip = cameraContextMenu;
        motionIndicator = new MotionIndicatorForm();
        motionIndicator.Show(this);
        motionIndicator.Hide();
#endif
        if (!HasUsableCamera()) OpenSettings();
        if (closing) return;
#if BETA
        if (settings.StartBehavior == "Camera" && settings.Cameras.Count > 0)
            settings.SelectedCamera = Math.Clamp(settings.StartCameraIndex, 0, settings.Cameras.Count - 1);
#endif
        UpdateToolbar(); PositionOverlays(); StartPlayer(); latencyTimer.Start(); controlsTimer.Start();
#if BETA
        if ((settings.StartBehavior == "Grid" || settings.StartBehavior == "Last" && settings.LastGridMode) &&
            settings.Cameras.Count(camera => Uri.TryCreate(camera.StreamUrl, UriKind.Absolute, out _)) >= 2)
            ToggleGridView();
        if (settings.StartBehavior == "Minimized") MinimizeWindow();
        RestartMotionIntegration();
#endif
    }

    private void CloseMonitor()
    {
        closing = true; latencyTimer.Stop(); restartTimer.Stop(); controlsTimer.Stop();
#if BETA
        CancelSeamlessRefresh();
        StopRecordingForClose();
        foreach (var capture in motionProcesses.ToArray())
            try { if (!capture.HasExited) capture.Kill(true); } catch { }
        StopGridPlayers();
        motionRestoreTimer.Stop(); motionIndicatorTimer.Stop(); motionPauseTimer.Stop(); cameraLayoutTimer.Stop(); StopMotionIntegration(); cameraContextMenu?.Dispose(); motionIndicator?.Close();
#endif
        SaveWindow(); StopPlayer(); toolbar?.Close(); dragSurface?.Close(); foreach (var grip in resizeGrips) grip.Close();
    }

    private void StartPlayer()
    {
#if BETA
        if (gridMode)
        {
            StartGridPlayers();
            return;
        }
#endif
        if (closing || !HasUsableCamera() || player is { HasExited: false }) return;
        var camera = settings.Cameras[settings.SelectedCamera];
#if BETA
        Text = $"HomeCamMonitor for Homeassistant Beta – {camera.Name}";
#else
        Text = $"HomeCamMonitor for Homeassistant – {camera.Name}";
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
#if BETA
        CancelSeamlessRefresh();
#endif
        intentionalStop = true; var current = player; player = null;
        if (current is null) return;
        try { current.Exited -= PlayerExited; if (!current.HasExited) { current.Kill(true); current.WaitForExit(2000); } current.Dispose(); } catch { }
    }

    private void RestartPlayer()
    {
        if (closing) return;
#if BETA
        if (gridMode)
        {
            StopGridPlayers();
            cameraGrid.Invalidate();
            restartTimer.Stop();
            restartTimer.Start();
            return;
        }
#endif
#if BETA
        CancelSeamlessRefresh();
#endif
        StopPlayer(); video.Invalidate(); restartTimer.Stop(); restartTimer.Start();
    }


#if BETA
    private async Task RefreshSeamlesslyAsync()
    {
        if (closing || refreshCancellation is not null) return;
        using var cancellation = new CancellationTokenSource();
        refreshCancellation = cancellation;
        try
        {
            if (gridMode) await RefreshGridSequentiallyAsync(cancellation.Token);
            else await RefreshSingleCameraAsync(cancellation.Token);
        }
        catch (OperationCanceledException) { }
        catch { /* Keep the current player visible and retry on the next interval. */ }
        finally
        {
            if (ReferenceEquals(refreshCancellation, cancellation)) refreshCancellation = null;
        }
    }

    private void CancelSeamlessRefresh() => refreshCancellation?.Cancel();

    private Process StartRefreshPlayer(Panel target, string streamUrl, string ipcName, bool grid)
    {
        target.CreateControl();
        var start = new ProcessStartInfo(Path.Combine(AppContext.BaseDirectory, "mpv.exe"))
        {
            UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden
        };
        var arguments = new List<string>
        {
            $"--wid={target.Handle.ToInt64()}", "--no-terminal", "--really-quiet", "--no-audio", "--no-osc",
            "--profile=low-latency", "--cache=no", "--demuxer-lavf-o=rtsp_transport=tcp",
            "--hwdec=auto-safe", "--vo=gpu-next", "--gpu-api=d3d11", "--scale=ewa_lanczossharp",
            "--cscale=ewa_lanczossharp", "--dscale=mitchell", "--interpolation=no"
        };
        if (grid) arguments.AddRange(["--keepaspect-window=no", "--panscan=1.0"]);
        else arguments.Add("--window-dragging=yes");
        arguments.Add("--keep-open=no");
        arguments.Add("--input-ipc-server=" + @"\\.\pipe\" + ipcName);
        arguments.Add(streamUrl);
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        return Process.Start(start) ?? throw new InvalidOperationException("mpv konnte nicht gestartet werden.");
    }

    private static void DisposePlayer(Process? process, EventHandler? exitedHandler = null)
    {
        if (process is null) return;
        try
        {
            if (exitedHandler is not null) process.Exited -= exitedHandler;
            if (!process.HasExited) { process.Kill(true); process.WaitForExit(2000); }
        }
        catch { }
        finally { process.Dispose(); }
    }

    private async Task RefreshSingleCameraAsync(CancellationToken cancellationToken)
    {
        if (!HasUsableCamera() || player is null || player.HasExited) return;
        var oldPlayer = player;
        var cameraIndex = settings.SelectedCamera;
        var oldSurface = video;
        var nextSurface = standbyVideo;
        nextSurface.Bounds = ClientRectangle;
        var newPipeName = $"HomeCamMonitor-Refresh-{Environment.ProcessId}-{Guid.NewGuid():N}";
        Process? replacement = null;
        try
        {
            replacement = StartRefreshPlayer(nextSurface, settings.Cameras[cameraIndex].StreamUrl, newPipeName, false);
            if (!await WaitForFirstFrameAsync(replacement, newPipeName, cancellationToken) ||
                replacement.HasExited || cancellationToken.IsCancellationRequested || closing || gridMode ||
                player != oldPlayer || settings.SelectedCamera != cameraIndex) return;

            nextSurface.BringToFront();
            video = nextSurface;
            standbyVideo = oldSurface;
            pipeName = newPipeName;
            player = replacement;
            replacement.EnableRaisingEvents = true;
            replacement.Exited += PlayerExited;
            replacement = null;
            DisposePlayer(oldPlayer, PlayerExited);
            oldSurface.Invalidate();
        }
        finally { DisposePlayer(replacement); }
    }

    private async Task RefreshGridSequentiallyAsync(CancellationToken cancellationToken)
    {
        foreach (var slot in gridSlots)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!gridMode || slot.CameraIndex < 0 || slot.CameraIndex >= settings.Cameras.Count ||
                slot.Player is null || slot.Player.HasExited) continue;
            var oldPlayer = slot.Player;
            var cameraIndex = slot.CameraIndex;
            var oldSurface = slot.ActiveSurface;
            var nextSurface = slot.SpareSurface;
            nextSurface.Bounds = slot.Host.ClientRectangle;
            var newPipeName = $"HomeCamMonitor-GridRefresh-{Environment.ProcessId}-{Guid.NewGuid():N}";
            Process? replacement = null;
            try
            {
                replacement = StartRefreshPlayer(nextSurface, settings.Cameras[cameraIndex].StreamUrl, newPipeName, true);
                if (!await WaitForFirstFrameAsync(replacement, newPipeName, cancellationToken) ||
                    replacement.HasExited || cancellationToken.IsCancellationRequested || closing || !gridMode ||
                    slot.Player != oldPlayer || slot.CameraIndex != cameraIndex) continue;

                nextSurface.BringToFront();
                slot.ActiveSurface = nextSurface;
                slot.SpareSurface = oldSurface;
                slot.Player = replacement;
                replacement.EnableRaisingEvents = true;
                replacement.Exited += GridPlayerExited;
                replacement = null;
                slot.Name.BringToFront();
                UpdateGridMotionBorders();
                DisposePlayer(oldPlayer, GridPlayerExited);
                oldSurface.Invalidate();
            }
            catch (OperationCanceledException) { throw; }
            catch { /* A failed replacement leaves this camera's original stream running. */ }
            finally { DisposePlayer(replacement); }
        }
    }

    private static async Task<bool> WaitForFirstFrameAsync(Process process, string ipcName,
        CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(12));
        try
        {
            using var pipe = new NamedPipeClientStream(".", ipcName, PipeDirection.InOut, PipeOptions.Asynchronous);
            while (!pipe.IsConnected)
            {
                if (process.HasExited) return false;
                try { await pipe.ConnectAsync(500, timeout.Token); }
                catch (TimeoutException) { }
            }
            using var writer = new StreamWriter(pipe, new UTF8Encoding(false), 1024, true) { AutoFlush = true };
            using var reader = new StreamReader(pipe, Encoding.UTF8, false, 1024, true);
            await writer.WriteLineAsync(JsonSerializer.Serialize(new
            {
                command = new[] { "get_property", "video-frame-info" }, request_id = 1
            }));
            while (!process.HasExited)
            {
                var line = await reader.ReadLineAsync(timeout.Token);
                if (line is null) return false;
                using var response = JsonDocument.Parse(line);
                var root = response.RootElement;
                if (root.TryGetProperty("event", out var eventName))
                {
                    var name = eventName.GetString();
                    if (name == "playback-restart") return true;
                    if (name is "end-file" or "shutdown") return false;
                }
                if (root.TryGetProperty("request_id", out var requestId) && requestId.GetInt32() == 1 &&
                    root.TryGetProperty("error", out var error) && error.GetString() == "success" &&
                    root.TryGetProperty("data", out var frame) && frame.ValueKind == JsonValueKind.Object)
                    return true;
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) { }
        catch (IOException) { }
        catch (TimeoutException) { }
        return false;
    }
#endif

    internal void SelectRelativeCamera(int direction)
    {
#if BETA
        RegisterUserInteraction();
        if (gridMode) { toolbar?.Flash("Kamera doppelt anklicken"); return; }
#endif
        if (settings.Cameras.Count < 2) return;
        settings.SelectedCamera = (settings.SelectedCamera + direction + settings.Cameras.Count) % settings.Cameras.Count;
        SettingsStore.Save(settings); UpdateToolbar(); RestartPlayer();
    }

    internal async Task SaveSnapshotAsync()
    {
#if BETA
        RegisterUserInteraction();
        if (gridMode) { toolbar?.Flash("Kamera doppelt anklicken"); return; }
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

#if BETA
    internal async Task ToggleRecordingAsync()
    {
        RegisterUserInteraction();
        if (gridMode) { toolbar?.Flash("Kamera doppelt anklicken"); return; }
        try
        {
            if (recording)
            {
                await StopRecordingAsync();
                return;
            }

            if (!HasUsableCamera()) return;
            var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyVideos), "HomeCam Monitor");
            Directory.CreateDirectory(folder);
            var cameraName = string.Concat(settings.Cameras[settings.SelectedCamera].Name.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
            recordingPath = Path.Combine(folder, $"{cameraName}_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.mkv");
            var camera = settings.Cameras[settings.SelectedCamera];
            var start = new ProcessStartInfo(Path.Combine(AppContext.BaseDirectory, "mpv.exe"))
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            };
            foreach (var argument in new[]
            {
                "--no-terminal", "--really-quiet", "--no-audio", "--vo=null", "--cache=no",
                "--demuxer-lavf-o=rtsp_transport=tcp", $"--stream-record={recordingPath}",
                $"--input-ipc-server=\\\\.\\pipe\\{recordingPipeName}", camera.StreamUrl
            }) start.ArgumentList.Add(argument);
            recordingPlayer = Process.Start(start) ?? throw new InvalidOperationException("Der Aufnahmeprozess konnte nicht gestartet werden.");
            recording = true;
            latencyTimer.Stop();
            RefreshRecordingIndicator();
            toolbar?.Flash("Aufnahme läuft");
        }
        catch (Exception exception)
        {
            recording = false;
            RefreshRecordingIndicator();
            latencyTimer.Start();
            MessageBox.Show(this, $"Die Aufnahme konnte nicht gestartet oder beendet werden.\n\nZiel: {recordingPath}\n\n{exception.Message}", "Aufnahme fehlgeschlagen", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private async Task StopRecordingAsync()
    {
        var current = recordingPlayer;
        var completedPath = recordingPath;
        recordingPlayer = null;
        recording = false;
        RefreshRecordingIndicator();
        latencyTimer.Start();

        if (current is not null)
        {
            try
            {
                if (!current.HasExited)
                {
                    await SendCommandToPipeAsync(recordingPipeName, new object[] { "quit" });
                    await Task.Run(() => current.WaitForExit(5000));
                    if (!current.HasExited) current.Kill(true);
                }
            }
            catch
            {
                try { if (!current.HasExited) current.Kill(true); } catch { }
            }
            finally { current.Dispose(); }
        }

        recordingPath = null;
        if (!string.IsNullOrWhiteSpace(completedPath) && File.Exists(completedPath) && new FileInfo(completedPath).Length > 0)
        {
            toolbar?.Flash("Aufnahme gespeichert");
            return;
        }

        if (!string.IsNullOrWhiteSpace(completedPath) && File.Exists(completedPath)) File.Delete(completedPath);
        MessageBox.Show(this, "Die Kameraaufnahme enthielt keine Videodaten und wurde entfernt.", "Aufnahme fehlgeschlagen", MessageBoxButtons.OK, MessageBoxIcon.Warning);
    }

    private void StopRecordingForClose()
    {
        var current = recordingPlayer;
        recordingPlayer = null;
        recording = false;
        try
        {
            if (current is not null && !current.HasExited)
            {
                SendCommandToPipeAsync(recordingPipeName, new object[] { "quit" }).GetAwaiter().GetResult();
                if (!current.WaitForExit(5000)) current.Kill(true);
            }
        }
        catch { try { if (current is { HasExited: false }) current.Kill(true); } catch { } }
        finally { current?.Dispose(); }
    }

    private void RefreshRecordingIndicator() => toolbar?.SetRecording(recording || activeMotionRecordings > 0, recording);
#endif

    private async Task SendCommandAsync(object[] command)
        => await SendCommandToPipeAsync(pipeName, command);

    private static async Task SendCommandToPipeAsync(string targetPipeName, object[] command)
    {
        using var pipe = new NamedPipeClientStream(".", targetPipeName, PipeDirection.Out, PipeOptions.Asynchronous);
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
#if BETA
            activeSettingsDialog = dialog;
#endif
            if (dialog.ShowDialog(this) == DialogResult.OK) changedSettings = dialog.Result;
        }
        finally
        {
#if BETA
            activeSettingsDialog = null;
#endif
            suppressToolbar = false;
            TopMost = changedSettings?.AlwaysOnTop ?? settings.AlwaysOnTop;
            PositionOverlays();
            lastCursorMovement = DateTime.UtcNow;
        }

        if (changedSettings is null) return;
#if BETA
        if (changedSettings.MotionDetectionEnabled != settings.MotionDetectionEnabled)
            changedSettings.MotionActionsPausedUntilUtc = null;
#endif
        settings = changedSettings; settings.SelectedCamera = Math.Clamp(settings.SelectedCamera, 0, settings.Cameras.Count - 1);
#if BETA
        settings.LastGridMode = gridMode;
#endif
        SettingsStore.Save(settings); ConfigureAutostart(settings.StartWithWindows);
        UpdateToolbar(); PositionOverlays(); RestartPlayer();
#if BETA
        ScheduleMotionPauseExpiry();
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
            SaveWindow();
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
#if BETA
        AlignCameraSurfaces();
        ScheduleCameraLayout();
#endif
        PositionOverlays();
        lastCursorMovement = DateTime.UtcNow;
        SaveWindow();
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

    internal void HandleSurfaceDoubleClick(Point _)
    {
#if BETA
        RegisterUserInteraction();
#endif
        ToggleFullscreen();
    }

#if BETA
    private void InitializeCameraGrid()
    {
        cameraGrid.Resize += (_, _) => LayoutCameraGrid();

        for (var index = 0; index < 4; index++)
        {
            var host = new Panel { Margin = new Padding(0), BackColor = Color.Black };
            var activeSurface = new Panel { Dock = DockStyle.Fill, BackColor = Color.Black };
            var spareSurface = new Panel { Dock = DockStyle.Fill, BackColor = Color.Black };
            host.Controls.Add(spareSurface);
            host.Controls.Add(activeSurface);
            activeSurface.BringToFront();
            var name = new Label
            {
                AutoSize = true,
                Text = "Kamera",
                ForeColor = Color.White,
                BackColor = Color.FromArgb(36, 36, 36),
                Font = new Font("Segoe UI", 8.5f),
                Padding = new Padding(5, 2, 5, 2),
                Location = new Point(6, 6)
            };
            host.Controls.Add(name);
            var borders = Enumerable.Range(0, 4)
                .Select(_ => new Panel { BackColor = Color.White, Visible = false, TabStop = false })
                .ToArray();
            host.Controls.AddRange(borders);
            var slot = new GridPlayerSlot { Host = host, ActiveSurface = activeSurface,
                SpareSurface = spareSurface, Name = name, BorderParts = borders };
            host.Resize += (_, _) => LayoutGridMotionBorder(slot);
            LayoutGridMotionBorder(slot);
            cameraGrid.Controls.Add(host);
            gridSlots.Add(slot);
        }
        LayoutCameraGrid();
    }

    internal static Rectangle[] GetCameraGridBounds(Size size)
    {
        // Split each axis once so all four tiles meet at exactly the same pixel.
        var middleX = size.Width / 2;
        var middleY = size.Height / 2;
        return
        [
            new Rectangle(0, 0, middleX, middleY),
            new Rectangle(middleX, 0, size.Width - middleX, middleY),
            new Rectangle(0, middleY, middleX, size.Height - middleY),
            new Rectangle(middleX, middleY, size.Width - middleX, size.Height - middleY)
        ];
    }

    private void LayoutCameraGrid()
    {
        var bounds = GetCameraGridBounds(cameraGrid.ClientSize);
        for (var index = 0; index < gridSlots.Count; index++)
            gridSlots[index].Host.Bounds = bounds[index];
    }

    private void AlignCameraSurfaces()
    {
        if (closing || IsDisposed) return;
        var visibleArea = ClientRectangle;
        if (video.Bounds != visibleArea) video.Bounds = visibleArea;
        if (standbyVideo.Bounds != visibleArea) standbyVideo.Bounds = visibleArea;
        if (cameraGrid.Bounds != visibleArea) cameraGrid.Bounds = visibleArea;
        LayoutCameraGrid();
    }

    private void ScheduleCameraLayout()
    {
        if (closing || IsDisposed || !IsHandleCreated) return;
        cameraLayoutTimer.Stop();
        cameraLayoutTimer.Start();
    }

    internal static Rectangle[] GetGridMotionBorderBounds(Size size)
    {
        // Four narrow strips are children of the camera tile, inset from its edges.
        const int inset = 2, stroke = 3;
        var width = Math.Max(1, size.Width - 2 * inset);
        var height = Math.Max(1, size.Height - 2 * inset - 2 * stroke);
        return
        [
            new Rectangle(inset, inset, width, stroke),
            new Rectangle(inset, Math.Max(inset, size.Height - inset - stroke), width, stroke),
            new Rectangle(inset, inset + stroke, stroke, height),
            new Rectangle(Math.Max(inset, size.Width - inset - stroke), inset + stroke, stroke, height)
        ];
    }

    private static void LayoutGridMotionBorder(GridPlayerSlot slot)
    {
        var bounds = GetGridMotionBorderBounds(slot.Host.ClientSize);
        for (var index = 0; index < bounds.Length; index++)
            slot.BorderParts[index].Bounds = bounds[index];
    }

    private void UpdateGridMotionBorders()
    {
        foreach (var slot in gridSlots)
        {
            var active = motionIndicatorVisible && gridMode && settings.HighlightMotionInGrid &&
                gridHighlightedCameraIndex >= 0 && slot.CameraIndex == gridHighlightedCameraIndex;
            foreach (var strip in slot.BorderParts)
            {
                strip.Visible = active;
                if (active) strip.BringToFront();
            }
            if (active) slot.Name.BringToFront();
        }
    }

    internal void ToggleGridView()
    {
        RegisterUserInteraction();
        if (gridMode)
        {
            ExitGridView();
            return;
        }

        if (settings.Cameras.Count(camera => Uri.TryCreate(camera.StreamUrl, UriKind.Absolute, out _)) < 2)
        {
            toolbar?.Flash("Mindestens 2 Kameras");
            return;
        }

        if (recording)
        {
            toolbar?.Flash("Aufnahme zuerst beenden");
            return;
        }

        StopPlayer();
        gridMode = true;
        settings.LastGridMode = true;
        SettingsStore.Save(settings);
        video.Hide();
        cameraGrid.Show();
        AlignCameraSurfaces();
        ScheduleCameraLayout();
        cameraGrid.BringToFront();
        toolbar?.SetGridMode(true);
        UpdateToolbar();
        StartGridPlayers();
        PositionOverlays();
    }

    private void ExitGridView(int? cameraIndex = null)
    {
        gridHighlightedCameraIndex = -1;
        UpdateGridMotionBorders();
        StopGridPlayers();
        gridMode = false;
        settings.LastGridMode = false;
        SettingsStore.Save(settings);
        if (fullscreen)
        {
            fullscreen = false;
            Bounds = windowedBounds;
            ApplyRoundedCorners();
        }
        if (cameraIndex.HasValue)
        {
            settings.SelectedCamera = cameraIndex.Value;
            SettingsStore.Save(settings);
        }
        cameraGrid.Hide();
        video.Show();
        video.BringToFront();
        toolbar?.SetGridMode(false);
        UpdateToolbar();
        StartPlayer();
        PositionOverlays();
    }

    private void StartGridPlayers()
    {
        if (closing || !gridMode) return;
        StopGridPlayers();
        var cameras = settings.Cameras
            .Select((camera, index) => (Camera: camera, Index: index))
            .Where(item => Uri.TryCreate(item.Camera.StreamUrl, UriKind.Absolute, out _))
            .Take(4)
            .ToList();

        intentionalGridStop = false;
        for (var index = 0; index < gridSlots.Count; index++)
        {
            var slot = gridSlots[index];
            slot.CameraIndex = -1;
            slot.Name.Text = "";
            slot.Name.Visible = false;
            if (index >= cameras.Count) continue;

            var camera = cameras[index];
            slot.CameraIndex = camera.Index;
            slot.Name.Text = camera.Camera.Name;
            slot.Name.Visible = true;
            slot.ActiveSurface.CreateControl();
            var start = new ProcessStartInfo(Path.Combine(AppContext.BaseDirectory, "mpv.exe"))
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            };
            foreach (var argument in new[]
            {
                $"--wid={slot.ActiveSurface.Handle.ToInt64()}", "--no-terminal", "--really-quiet", "--no-audio", "--no-osc",
                "--profile=low-latency", "--cache=no", "--demuxer-lavf-o=rtsp_transport=tcp",
                "--hwdec=auto-safe", "--vo=gpu-next", "--gpu-api=d3d11", "--scale=ewa_lanczossharp",
                "--cscale=ewa_lanczossharp", "--dscale=mitchell", "--interpolation=no",
                "--keepaspect-window=no", "--panscan=1.0", "--keep-open=no",
                camera.Camera.StreamUrl
            }) start.ArgumentList.Add(argument);

            try
            {
                slot.Player = Process.Start(start) ?? throw new InvalidOperationException("mpv konnte nicht gestartet werden.");
                slot.Player.EnableRaisingEvents = true;
                slot.Player.Exited += GridPlayerExited;
                slot.Name.BringToFront();
            }
            catch
            {
                slot.Player?.Dispose();
                slot.Player = null;
                slot.Name.Text = camera.Camera.Name + " – Stream nicht verfügbar";
            }
        }
    }

    private void StopGridPlayers()
    {
        CancelSeamlessRefresh();
        intentionalGridStop = true;
        foreach (var slot in gridSlots)
        {
            var current = slot.Player;
            slot.Player = null;
            if (current is null) continue;
            try
            {
                current.Exited -= GridPlayerExited;
                if (!current.HasExited) { current.Kill(true); current.WaitForExit(2000); }
                current.Dispose();
            }
            catch { }
        }
        intentionalGridStop = false;
    }

    private void GridPlayerExited(object? sender, EventArgs eventArgs)
    {
        if (closing || intentionalGridStop || !gridMode) return;
        try { BeginInvoke(new Action(() => { if (!closing && gridMode) { restartTimer.Stop(); restartTimer.Start(); } })); } catch { }
    }
#endif

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

        var paused = MotionActionsArePaused();
        var pause = new ToolStripMenuItem(paused
            ? $"Bewegungsaktionen pausiert bis {settings.MotionActionsPausedUntilUtc!.Value.ToLocalTime():HH:mm}"
            : "Bewegungsaktionen pausieren");
        foreach (var (label, minutes) in new[] { ("15 Minuten", 15), ("30 Minuten", 30), ("1 Stunde", 60) })
        {
            var item = new ToolStripMenuItem(label) { Enabled = settings.MotionDetectionEnabled };
            item.Click += (_, _) => PauseMotionActions(TimeSpan.FromMinutes(minutes));
            pause.DropDownItems.Add(item);
        }
        var untilManual = new ToolStripMenuItem("Bis manuell aktiviert")
        {
            Checked = !settings.MotionDetectionEnabled,
            Enabled = settings.MotionDetectionEnabled
        };
        untilManual.Click += (_, _) => SetMotionDetectionEnabled(false);
        pause.DropDownItems.Add(untilManual);
        pause.DropDownItems.Add(new ToolStripSeparator());
        var endPause = new ToolStripMenuItem("Pause beenden") { Enabled = paused };
        endPause.Click += (_, _) => ClearMotionPause();
        pause.DropDownItems.Add(endPause);
        menu.Items.Add(pause);
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
        cameraBeforeMotion = null;
        previousForegroundWindow = IntPtr.Zero;
        TopMost = settings.AlwaysOnTop;
        PositionOverlays();
    }

    private void SetAlwaysOnTop(bool enabled)
    {
        settings.AlwaysOnTop = enabled;
        motionRestoreTimer.Stop();
        cameraBeforeMotion = null;
        sentToBackground = false;
        TopMost = enabled;
        SettingsStore.Save(settings);
        PositionOverlays();
    }

    private void SetMotionDetectionEnabled(bool enabled)
    {
        settings.MotionDetectionEnabled = enabled;
        settings.MotionActionsPausedUntilUtc = null;
        motionPauseTimer.Stop();
        motionRestoreTimer.Stop();
        cameraBeforeMotion = null;
        previousForegroundWindow = IntPtr.Zero;
        SettingsStore.Save(settings);
        RestartMotionIntegration();
    }

    private bool MotionActionsArePaused()
    {
        if (!settings.MotionDetectionEnabled || settings.MotionActionsPausedUntilUtc is null) return false;
        if (settings.MotionActionsPausedUntilUtc.Value > DateTime.UtcNow) return true;
        ClearMotionPause();
        return false;
    }

    private void PauseMotionActions(TimeSpan duration)
    {
        if (!settings.MotionDetectionEnabled) return;
        settings.MotionActionsPausedUntilUtc = DateTime.UtcNow.Add(duration);
        SettingsStore.Save(settings);
        ScheduleMotionPauseExpiry();
    }

    private void ClearMotionPause()
    {
        motionPauseTimer.Stop();
        if (settings.MotionActionsPausedUntilUtc is null) return;
        settings.MotionActionsPausedUntilUtc = null;
        SettingsStore.Save(settings);
    }

    private void ScheduleMotionPauseExpiry()
    {
        motionPauseTimer.Stop();
        if (settings.MotionActionsPausedUntilUtc is not DateTime until) return;
        var remaining = until - DateTime.UtcNow;
        if (remaining <= TimeSpan.Zero) { ClearMotionPause(); return; }
        motionPauseTimer.Interval = (int)Math.Clamp(Math.Ceiling(remaining.TotalMilliseconds), 1, int.MaxValue);
        motionPauseTimer.Start();
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
#if BETA
        if (OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000))
        {
            // Keep the rounded borderless window at a stable 16:9 outer size.
            var targetHeight = Math.Max(MinimumSize.Height, (int)Math.Round(Width * 9d / 16d));
            if (Math.Abs(Height - targetHeight) <= 1) return;
            adjustingAspectRatio = true;
            try { Bounds = new Rectangle(Left, Top, Width, targetHeight); }
            finally { adjustingAspectRatio = false; }
            return;
        }
#endif
        var targetHeightLegacy = Math.Max(MinimumSize.Height, (int)Math.Round(ClientSize.Width * 9d / 16d));
        if (Math.Abs(ClientSize.Height - targetHeightLegacy) <= 1) return;
        adjustingAspectRatio = true;
        try { ClientSize = new Size(ClientSize.Width, targetHeightLegacy); }
        finally { adjustingAspectRatio = false; }
    }

    private void UpdateToolbar()
    {
        if (toolbar is null) return;
#if BETA
        if (gridMode) { toolbar.CameraName = "4 Kameras"; return; }
#endif
        if (HasUsableCamera()) toolbar.CameraName = settings.Cameras[settings.SelectedCamera].Name;
    }
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
        UpdateToolbarScale();
#endif
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
            var highlightedSlot = motionIndicatorVisible && gridMode && settings.HighlightMotionInGrid && gridHighlightedCameraIndex >= 0
                ? gridSlots.FirstOrDefault(slot => slot.CameraIndex == gridHighlightedCameraIndex) : null;
            var highlightBounds = highlightedSlot?.Host.RectangleToScreen(highlightedSlot.Host.ClientRectangle);
            UpdateGridMotionBorders();
            motionIndicator.Location = highlightBounds.HasValue
                ? new Point(highlightBounds.Value.Right - motionIndicator.Width - 10, highlightBounds.Value.Top + 10)
                : new Point(Right - motionIndicator.Width - 12, Top + 12);
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
#if BETA
    internal static int GetToolbarSizePercent(Settings current, int windowWidth)
    {
        if (!current.AutoScaleToolbar) return Math.Clamp(current.ToolbarSizePercent, 50, 100);
        // Leave ten pixels on each side. At the smallest supported camera width
        // the entire toolbar still fits without clipping.
        return Math.Clamp((int)Math.Floor((windowWidth - 20) * 100d / 320), 50, 100);
    }

    private void UpdateToolbarScale()
    {
        if (toolbar is null || toolbar.IsDisposed) return;
        toolbar.SetSizePercent(GetToolbarSizePercent(settings, Width));
    }
#endif
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
        if (settings.DirectHomeAssistantEnabled)
        {
            if (!string.IsNullOrWhiteSpace(settings.HomeAssistantUrl) &&
                !string.IsNullOrWhiteSpace(settings.HomeAssistantToken) &&
                settings.Cameras.Any(camera =>
                    (camera.MotionEnabled && !string.IsNullOrWhiteSpace(camera.MotionEntityId)) ||
                    ((camera.PersonEnabled ?? !string.IsNullOrWhiteSpace(camera.PersonEntityId)) &&
                     !string.IsNullOrWhiteSpace(camera.PersonEntityId))))
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
            if (!TryGetCameraEvent(message.RootElement, out var cameraName, out var personDetected)) continue;
            if (!closing) BeginInvoke(new Action(() =>
            {
                if (personDetected) HandlePersonDetected(cameraName);
                else HandleMotion(cameraName);
            }));
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

    internal static async Task<string?> TestHomeAssistantConnectionAsync(string address, string token, string entityId, bool ignoreCertificateErrors, string personEntityId = "")
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
            if (!states.RootElement.TryGetProperty("result", out var result) || result.ValueKind != JsonValueKind.Array)
                return "Home Assistant hat keine Entitäten geliefert.";
            foreach (var expected in new[] { entityId.Trim(), personEntityId.Trim() }.Where(id => id.Length > 0))
                if (!result.EnumerateArray().Any(state => state.TryGetProperty("entity_id", out var id) &&
                    string.Equals(id.GetString(), expected, StringComparison.OrdinalIgnoreCase)))
                    return $"Entität nicht gefunden: {expected}";
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

    private bool TryGetCameraEvent(JsonElement root, out string cameraName, out bool personDetected)
    {
        cameraName = "";
        personDetected = false;
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
        if (matchingCamera is not null) { cameraName = matchingCamera.Name; return true; }
        matchingCamera = settings.Cameras.FirstOrDefault(camera =>
            (camera.PersonEnabled ?? !string.IsNullOrWhiteSpace(camera.PersonEntityId)) &&
            !string.IsNullOrWhiteSpace(camera.PersonEntityId) &&
            string.Equals(camera.PersonEntityId.Trim(), entity.GetString(), StringComparison.OrdinalIgnoreCase));
        if (matchingCamera is null) return false;
        cameraName = matchingCamera.Name;
        personDetected = true;
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
        ShowMotionIndicator(cameraIndex: settings.Cameras.FindIndex(camera =>
            string.Equals(camera.Name, cameraName, StringComparison.OrdinalIgnoreCase)));
        if (MotionActionsArePaused()) return;
        var motionCamera = settings.Cameras.FirstOrDefault(camera =>
            camera.MotionEnabled && string.Equals(camera.Name, cameraName, StringComparison.OrdinalIgnoreCase));
        if (motionCamera is not null) StartMotionCapture(motionCamera);
        if (settings.AlwaysOnTop) return;
        var cameraIndex = settings.Cameras.FindIndex(camera => string.Equals(camera.Name, cameraName, StringComparison.OrdinalIgnoreCase));
        if (cameraIndex < 0) { toolbar?.Flash($"{cameraName} fehlt"); return; }
        if (!motionRestoreTimer.Enabled) previousForegroundWindow = NativeMethods.GetForegroundWindow();
        if (gridMode)
        {
            cameraBeforeMotion = null;
            motionRestoreTimer.Stop();
            motionRestoreTimer.Interval = Math.Clamp(settings.MotionForegroundSeconds, 3, 300) * 1000;
            ForceToForeground();
            motionRestoreTimer.Start();
            return;
        }
        if (settings.RestorePreviousCameraAfterMotion && cameraBeforeMotion is null && settings.SelectedCamera != cameraIndex)
            cameraBeforeMotion = settings.SelectedCamera;
        if (!settings.RestorePreviousCameraAfterMotion) cameraBeforeMotion = null;
        if (settings.SelectedCamera != cameraIndex)
        {
            settings.SelectedCamera = cameraIndex; SettingsStore.Save(settings); UpdateToolbar(); RestartPlayer();
        }
        motionRestoreTimer.Stop();
        motionRestoreTimer.Interval = Math.Clamp(settings.MotionForegroundSeconds, 3, 300) * 1000;
        ForceToForeground();
        if (!settings.AlwaysOnTop) motionRestoreTimer.Start();
    }

    private void HandlePersonDetected(string cameraName)
    {
        if (!settings.MotionDetectionEnabled) return;
        var camera = settings.Cameras.FirstOrDefault(item =>
            (item.PersonEnabled ?? !string.IsNullOrWhiteSpace(item.PersonEntityId)) &&
            string.Equals(item.Name, cameraName, StringComparison.OrdinalIgnoreCase));
        if (camera is null) return;
        ShowMotionIndicator(personDetected: true);
        if (MotionActionsArePaused()) return;
        StartMotionCapture(camera, personDetected: true);
    }

    private async void StartMotionCapture(CameraEntry camera, bool personDetected = false)
    {
        if (!personDetected && camera.MotionAction is not ("Snapshot" or "Video" or "Both")) return;
        var captureKey = camera.Name + (personDetected ? "|Person" : "|Motion");
        if (activeMotionCapture.Contains(captureKey)) return;
        // The HTTP endpoint can be called more than once for the same event.
        if (lastMotionCapture.TryGetValue(captureKey, out var last) &&
            DateTime.UtcNow - last < TimeSpan.FromSeconds(2)) return;
        lastMotionCapture[captureKey] = DateTime.UtcNow;
        activeMotionCapture.Add(captureKey);
        try
        {
            var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyVideos), "HomeCam Monitor", "Bewegung");
            Directory.CreateDirectory(folder);
            DeleteExpiredMotionFiles(folder, settings.MotionRetentionDays);
            var safeName = string.Concat(camera.Name.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
            var baseName = $"{safeName}_{(personDetected ? "Person_" : "")}{DateTime.Now:yyyy-MM-dd_HH-mm-ss-fff}_{Guid.NewGuid():N}";
            var captureSnapshot = (personDetected || camera.MotionAction is "Snapshot" or "Both") ? CaptureSnapshotAsync() : Task.CompletedTask;
            var recordVideo = (!personDetected && camera.MotionAction is "Video" or "Both") ? RecordVideoAsync() : Task.CompletedTask;
            await Task.WhenAll(captureSnapshot, recordVideo);

            async Task CaptureSnapshotAsync()
            {
                var path = Path.Combine(folder, baseName + ".png");
                var start = new ProcessStartInfo(Path.Combine(AppContext.BaseDirectory, "ffmpeg.exe"))
                {
                    UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true
                };
                foreach (var argument in new[] { "-nostdin", "-hide_banner", "-loglevel", "error" }) start.ArgumentList.Add(argument);
                if (Uri.TryCreate(camera.StreamUrl, UriKind.Absolute, out var streamUri) && streamUri.Scheme == "rtsp")
                {
                    start.ArgumentList.Add("-rtsp_transport");
                    start.ArgumentList.Add("tcp");
                }
                foreach (var argument in new[] { "-i", camera.StreamUrl, "-map", "0:v:0",
                    "-frames:v", "1", "-an", "-f", "image2", "-y", path }) start.ArgumentList.Add(argument);
                using var process = Process.Start(start) ?? throw new InvalidOperationException("FFmpeg konnte nicht gestartet werden.");
                motionProcesses.Add(process);
                try
                {
                    var errors = process.StandardError.ReadToEndAsync();
                    using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(25));
                    try { await process.WaitForExitAsync(timeout.Token); }
                    catch (OperationCanceledException)
                    {
                        if (!process.HasExited) process.Kill(true);
                        throw new IOException("Der Snapshot hat das Zeitlimit überschritten.");
                    }
                    if (process.ExitCode != 0 || !File.Exists(path) || new FileInfo(path).Length < 64)
                        throw new IOException("Kein Bild vom Stream: " + (await errors).Trim());
                    using var image = Image.FromFile(path);
                    if (image.Width < 1 || image.Height < 1) throw new IOException("Das Einzelbild ist leer.");
                }
                catch { if (File.Exists(path)) File.Delete(path); throw; }
                finally { motionProcesses.Remove(process); }
            }
            async Task RecordVideoAsync()
            {
                var path = Path.Combine(folder, baseName + ".mkv");
                var start = new ProcessStartInfo(Path.Combine(AppContext.BaseDirectory, "ffmpeg.exe"))
                {
                    UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true
                };
                foreach (var argument in new[] { "-nostdin", "-hide_banner", "-loglevel", "error" }) start.ArgumentList.Add(argument);
                if (Uri.TryCreate(camera.StreamUrl, UriKind.Absolute, out var streamUri) && streamUri.Scheme == "rtsp")
                {
                    start.ArgumentList.Add("-rtsp_transport");
                    start.ArgumentList.Add("tcp");
                }
                foreach (var argument in new[] { "-i", camera.StreamUrl,
                    "-t", (camera.MotionVideoSeconds is 15 or 30 or 60 ? camera.MotionVideoSeconds : 30).ToString(System.Globalization.CultureInfo.InvariantCulture),
                    "-map", "0:v:0", "-an", "-c:v", "copy", "-f", "matroska", "-y", path }) start.ArgumentList.Add(argument);
                using var process = Process.Start(start) ?? throw new InvalidOperationException("FFmpeg konnte nicht gestartet werden.");
                motionProcesses.Add(process);
                activeMotionRecordings++;
                RefreshRecordingIndicator();
                try
                {
                    var errors = process.StandardError.ReadToEndAsync();
                    using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(camera.MotionVideoSeconds + 30));
                    try { await process.WaitForExitAsync(timeout.Token); }
                    catch (OperationCanceledException)
                    {
                        if (!process.HasExited) process.Kill(true);
                        throw new IOException("Die Videoaufnahme hat das Zeitlimit überschritten.");
                    }
                    if (process.ExitCode != 0 || !File.Exists(path) || new FileInfo(path).Length < 4096)
                        throw new IOException("Kein gültiges Video: " + (await errors).Trim());
                }
                catch { if (File.Exists(path)) File.Delete(path); throw; }
                finally
                {
                    motionProcesses.Remove(process);
                    activeMotionRecordings--;
                    if (!closing) RefreshRecordingIndicator();
                }
            }
        }
        catch (Exception exception)
        {
            if (!closing) toolbar?.Flash($"Bewegung: {exception.Message}");
        }
        finally { activeMotionCapture.Remove(captureKey); }
    }

    internal static void DeleteExpiredMotionFiles(string folder, int days)
    {
        if (days <= 0 || !Directory.Exists(folder)) return; // 0 means unlimited.
        var cutoff = DateTime.UtcNow.AddDays(-days);
        foreach (var file in Directory.EnumerateFiles(folder))
        {
            if (Path.GetExtension(file) is not (".png" or ".mkv")) continue;
            try { if (File.GetLastWriteTimeUtc(file) < cutoff) File.Delete(file); }
            catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
    }

    private void ShowMotionIndicator(bool personDetected = false, int cameraIndex = -1)
    {
        if (motionIndicator is not null) motionIndicator.PersonDetected = personDetected;
        gridHighlightedCameraIndex = !personDetected && gridMode && settings.HighlightMotionInGrid
            ? cameraIndex : -1;
        motionIndicatorVisible = true;
        motionIndicatorTimer.Stop();
        motionIndicatorTimer.Interval = Math.Clamp(settings.MotionIndicatorSeconds, 1, 10) * 1000;
        motionIndicatorTimer.Start();
        PositionOverlays();
    }

    private void HideMotionIndicator()
    {
        motionIndicatorTimer.Stop();
        motionIndicatorVisible = false;
        gridHighlightedCameraIndex = -1;
        motionIndicator?.Hide();
        UpdateGridMotionBorders();
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
        if (activeSettingsDialog is { IsDisposed: false, Visible: true } || Bounds.Contains(Cursor.Position))
        {
            motionRestoreTimer.Interval = Math.Clamp(settings.MotionForegroundSeconds, 3, 300) * 1000;
            motionRestoreTimer.Start();
            return;
        }
        RestoreCameraAfterMotion();
        if (settings.MinimizeWhenInactive) MinimizeAfterMotion();
        else SendToBackground();
    }

    private void RestoreCameraAfterMotion()
    {
        var cameraIndex = cameraBeforeMotion;
        cameraBeforeMotion = null;
        if (!settings.RestorePreviousCameraAfterMotion || gridMode || cameraIndex is null ||
            cameraIndex < 0 || cameraIndex >= settings.Cameras.Count || settings.SelectedCamera == cameraIndex) return;
        settings.SelectedCamera = cameraIndex.Value;
        SettingsStore.Save(settings);
        UpdateToolbar();
        RestartPlayer();
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
#if BETA
        NativeMethods.DisableDwmBorder(Handle);
        if (OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000))
        {
            // A GDI window region prevents DWM from anti-aliasing the corners.
            Region?.Dispose();
            Region = null;
            var systemCorners = fullscreen ? (int)NativeMethods.DwmWindowCornerPreference.DoNotRound
                : (int)NativeMethods.DwmWindowCornerPreference.Round;
            NativeMethods.DwmSetWindowAttribute(Handle, NativeMethods.DwmWindowAttribute.WindowCornerPreference,
                ref systemCorners, sizeof(int));
            return;
        }
#endif
        Region?.Dispose(); if (fullscreen) { Region = null; return; }
        var radius = Math.Max(12, DeviceDpi * 14 / 96); var handle = NativeMethods.CreateRoundRectRgn(0, 0, Width + 1, Height + 1, radius, radius);
        Region = Region.FromHrgn(handle); NativeMethods.DeleteObject(handle);
        var preference = fullscreen ? (int)NativeMethods.DwmWindowCornerPreference.DoNotRound : (int)NativeMethods.DwmWindowCornerPreference.Round;
        NativeMethods.DwmSetWindowAttribute(Handle, NativeMethods.DwmWindowAttribute.WindowCornerPreference,
            ref preference, sizeof(int));
    }
    private void SaveWindow()
    {
        if (WindowState == FormWindowState.Minimized)
        {
            SettingsStore.Save(settings);
            return;
        }
        var value = fullscreen ? windowedBounds : Bounds;
        settings.Left = value.Left; settings.Top = value.Top; settings.Width = value.Width; settings.Height = value.Height;
#if BETA
        var screen = Screen.FromRectangle(value);
        settings.LastMonitorDeviceName = screen.DeviceName;
        settings.MonitorOffsetX = value.Left - screen.WorkingArea.Left;
        settings.MonitorOffsetY = value.Top - screen.WorkingArea.Top;
        settings.LastGridMode = gridMode;
#endif
        SettingsStore.Save(settings);
    }
#if BETA
    internal static Rectangle RestoreWindowBounds(Settings saved, IReadOnlyList<(string DeviceName, Rectangle WorkingArea)> screens)
    {
        var width = Math.Max(240, saved.Width);
        var height = Math.Max(150, saved.Height);
        if (screens.Count == 0) return new Rectangle(0, 0, width, height);

        var preferred = screens.FirstOrDefault(screen =>
            !string.IsNullOrEmpty(saved.LastMonitorDeviceName) &&
            string.Equals(screen.DeviceName, saved.LastMonitorDeviceName, StringComparison.OrdinalIgnoreCase));
        var hasPreferred = preferred.WorkingArea.Width > 0;
        var oldBounds = new Rectangle(saved.Left, saved.Top, width, height);
        var target = hasPreferred ? preferred.WorkingArea :
            screens.FirstOrDefault(screen => screen.WorkingArea.IntersectsWith(oldBounds)).WorkingArea;
        if (target.Width <= 0) target = screens[0].WorkingArea;

        var left = hasPreferred ? target.Left + saved.MonitorOffsetX : saved.Left;
        var top = hasPreferred ? target.Top + saved.MonitorOffsetY : saved.Top;
        if (saved.Left == -1 && saved.Top == -1 && !hasPreferred)
        {
            left = target.Left + (target.Width - width) / 2;
            top = target.Top + (target.Height - height) / 2;
        }
        // A disconnected monitor must never leave the camera window invisible.
        if (!hasPreferred && !target.IntersectsWith(new Rectangle(left, top, width, height)))
        {
            left = target.Left + (target.Width - width) / 2;
            top = target.Top + (target.Height - height) / 2;
        }
        return new Rectangle(
            Math.Clamp(left, target.Left, Math.Max(target.Left, target.Right - width)),
            Math.Clamp(top, target.Top, Math.Max(target.Top, target.Bottom - height)),
            Math.Min(width, target.Width), Math.Min(height, target.Height));
    }
#endif
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
#if BETA
            AlignCameraSurfaces();
            ScheduleCameraLayout();
#endif
            PositionOverlays();
            lastCursorMovement = DateTime.UtcNow;
            SaveWindow();
            return;
        }
        if (message.Msg != NativeMethods.WmNcHitTest || fullscreen || WindowState != FormWindowState.Normal) return;
        var p = PointToClient(Cursor.Position); var grip = Math.Max(8, DeviceDpi * 8 / 96); var left = p.X < grip; var right = p.X >= ClientSize.Width - grip; var top = p.Y < grip; var bottom = p.Y >= ClientSize.Height - grip;
        if (left && top) message.Result = (IntPtr)NativeMethods.HtTopLeft; else if (right && top) message.Result = (IntPtr)NativeMethods.HtTopRight;
        else if (left && bottom) message.Result = (IntPtr)NativeMethods.HtBottomLeft; else if (right && bottom) message.Result = (IntPtr)NativeMethods.HtBottomRight;
        else if (left) message.Result = (IntPtr)NativeMethods.HtLeft; else if (right) message.Result = (IntPtr)NativeMethods.HtRight;
        else if (top) message.Result = (IntPtr)NativeMethods.HtTop; else if (bottom) message.Result = (IntPtr)NativeMethods.HtBottom;
    }

#if BETA
    private sealed class GridPlayerSlot
    {
        public required Panel Host { get; init; }
        public required Panel ActiveSurface { get; set; }
        public required Panel SpareSurface { get; set; }
        public required Label Name { get; init; }
        public required Panel[] BorderParts { get; init; }
        public int CameraIndex { get; set; } = -1;
        public Process? Player { get; set; }
    }
#endif
}

#if BETA
internal sealed class MotionIndicatorForm : Form
{
    private bool personDetected;
    public bool PersonDetected
    {
        get => personDetected;
        set { if (personDetected == value) return; personDetected = value; Invalidate(); }
    }

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

        if (personDetected)
        {
            // Ruhende Person als Gegenstück zum laufenden Bewegungssymbol.
            eventArgs.Graphics.FillEllipse(Brushes.Black, 13, 1, 11, 11);
            eventArgs.Graphics.FillEllipse(whiteBrush, 15, 3, 7, 7);
            var body = new[]
            {
                new[] { new PointF(18, 13), new PointF(18, 23) },
                new[] { new PointF(18, 16), new PointF(10, 21) },
                new[] { new PointF(18, 16), new PointF(26, 21) },
                new[] { new PointF(18, 23), new PointF(13, 32) },
                new[] { new PointF(18, 23), new PointF(23, 32) }
            };
            foreach (var line in body) eventArgs.Graphics.DrawLines(blackPen, line);
            foreach (var line in body) eventArgs.Graphics.DrawLines(whitePen, line);
            return;
        }

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
        var graphicsState = eventArgs.Graphics.Save();
        eventArgs.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        eventArgs.Graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
        using var pen = new Pen(Color.White, Math.Max(1.5f, rectangle.Height / 10f))
        {
            StartCap = System.Drawing.Drawing2D.LineCap.Round,
            EndCap = System.Drawing.Drawing2D.LineCap.Round,
            LineJoin = System.Drawing.Drawing2D.LineJoin.Round
        };
        eventArgs.Graphics.DrawLines(pen,
        new PointF[]
        {
            new(rectangle.Left + rectangle.Width * 0.20f, rectangle.Top + rectangle.Height * 0.53f),
            new(rectangle.Left + rectangle.Width * 0.43f, rectangle.Top + rectangle.Height * 0.74f),
            new(rectangle.Left + rectangle.Width * 0.82f, rectangle.Top + rectangle.Height * 0.27f)
        });
        eventArgs.Graphics.Restore(graphicsState);
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
        HandleCreated += (_, _) => NativeMethods.DisableOverlayDecoration(Handle);
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
        MouseDoubleClick += (_, eventArgs) => monitor.HandleSurfaceDoubleClick(PointToScreen(eventArgs.Location));
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
#if BETA
        HandleCreated += (_, _) => NativeMethods.DisableOverlayDecoration(Handle);
#endif
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
#if BETA
    private readonly Dictionary<Control, (Rectangle Bounds, string FontFamily, float FontSize, FontStyle FontStyle, GraphicsUnit FontUnit)> originalLayout = [];
    private int sizePercent = 100;
    private readonly Label grid;
    private readonly Label recording;
    private readonly System.Windows.Forms.Timer recordingBlinkTimer = new() { Interval = 500 };
    private bool recordingIndicatorActive;
    private bool recordingBlinkVisible = true;
#endif
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
#if BETA
        // The toolbar has its own pixel-based layout. WinForms font autoscaling
        // otherwise changes its bounds once the handle is created (and again on DPI changes).
        AutoScaleMode = AutoScaleMode.None;
        ClientSize = new Size(320, 34); StartPosition = FormStartPosition.Manual; TopMost = true;
#else
        ClientSize = new Size(256, 34); StartPosition = FormStartPosition.Manual; TopMost = true;
#endif
        var previous = Item("", 0, (_, _) => monitor.SelectRelativeCamera(-1));
        previous.Paint += (_, eventArgs) => DrawChevronIcon(eventArgs.Graphics, previous.ClientRectangle, false);
        name = Item("Kamera", 32, null, 64);
        var next = Item("", 96, (_, _) => monitor.SelectRelativeCamera(1));
        next.Paint += (_, eventArgs) => DrawChevronIcon(eventArgs.Graphics, next.ClientRectangle, true);
#if BETA
        grid = Item("", 128, (_, _) => monitor.ToggleGridView());
        grid.Paint += (_, eventArgs) => DrawGridIcon(eventArgs.Graphics, grid.ClientRectangle);
        var snapshot = Item("\uEB9F", 160, async (_, _) => await monitor.SaveSnapshotAsync());
        snapshot.Font = new Font("Segoe MDL2 Assets", 12);
        recording = Item("●", 192, async (_, _) => await monitor.ToggleRecordingAsync());
        recording.Font = new Font("Segoe UI Symbol", 9);
        recording.ForeColor = Color.White;
        recordingBlinkTimer.Tick += (_, _) =>
        {
            recordingBlinkVisible = !recordingBlinkVisible;
            recording.ForeColor = recordingBlinkVisible ? Color.Red : BackColor;
        };
        Disposed += (_, _) => recordingBlinkTimer.Dispose();
        var settings = Item("\uE713", 224, (_, _) => monitor.OpenSettings());
#else
        var snapshot = Item("\uEB9F", 128, async (_, _) => await monitor.SaveSnapshotAsync());
        snapshot.Font = new Font("Segoe MDL2 Assets", 12);
        var settings = Item("\uE713", 160, (_, _) => monitor.OpenSettings());
#endif
        settings.Font = new Font("Segoe MDL2 Assets", 12);
#if BETA
        var lastAction = Item("\uE921", 256, (_, _) => monitor.MinimizeWindow());
        var close = Item("\uE8BB", 288, (_, _) => monitor.Close());
#else
        var lastAction = Item("⛶", 192, (_, _) => monitor.ToggleFullscreen());
        var close = Item("\uE8BB", 224, (_, _) => monitor.Close());
#endif
#if BETA
        lastAction.Font = new Font("Segoe MDL2 Assets", 10);
#else
        lastAction.Font = new Font("Segoe UI Symbol", 12);
#endif
        close.Font = new Font("Segoe MDL2 Assets", 9);
#if BETA
        foreach (var icon in new[] { grid, snapshot, recording, settings, lastAction, close })
#else
        foreach (var icon in new[] { snapshot, settings, lastAction, close })
#endif
        {
            icon.Top = 0;
            icon.Height = 34;
            icon.TextAlign = ContentAlignment.MiddleCenter;
        }
#if BETA
        Controls.AddRange([previous, name, next, grid, snapshot, recording, settings, lastAction, close]);
#else
        Controls.AddRange([previous, name, next, snapshot, settings, lastAction, close]);
#endif
        toolTips.SetToolTip(previous, "Vorherige Kamera");
        toolTips.SetToolTip(name, "Aktuelle Kamera");
        toolTips.SetToolTip(next, "Nächste Kamera");
        toolTips.SetToolTip(snapshot, "Snapshot speichern");
#if BETA
        toolTips.SetToolTip(grid, "4-Kamera-Raster");
        toolTips.SetToolTip(recording, "Aufnahme starten");
#endif
        toolTips.SetToolTip(settings, "Einstellungen öffnen");
#if BETA
        toolTips.SetToolTip(lastAction, "Minimieren");
#else
        toolTips.SetToolTip(lastAction, "Vollbild ein/aus");
#endif
        toolTips.SetToolTip(close, "HomeCam Monitor beenden");
        note = new Label { AutoSize = true, ForeColor = Color.White, BackColor = Color.FromArgb(20, 20, 20), Visible = false }; Controls.Add(note);
#if BETA
        foreach (Control control in Controls)
            originalLayout[control] = (control.Bounds, control.Font.FontFamily.Name,
                control.Font.Size, control.Font.Style, control.Font.Unit);
#endif
#if !BETA
        var shape = NativeMethods.CreateRoundRectRgn(0, 0, Width + 1, Height + 1, 14, 14); Region = Region.FromHrgn(shape); NativeMethods.DeleteObject(shape);
#endif
    }
#if BETA
    public void SetSizePercent(int percent, bool force = false)
    {
        percent = Math.Clamp(percent, 50, 100);
        if (!force && sizePercent == percent) return;
        sizePercent = percent;
        var factor = sizePercent / 100f;
        SuspendLayout();
        foreach (var (control, layout) in originalLayout)
        {
            if (control == note) continue;
            var left = (int)Math.Round(layout.Bounds.Left * factor);
            var right = (int)Math.Round(layout.Bounds.Right * factor);
            control.Bounds = new Rectangle(
                left, 0, right - left, (int)Math.Round(34 * factor));
            control.Padding = Padding.Empty;
            var previousFont = control.Font;
            control.Font = new Font(layout.FontFamily, layout.FontSize * factor,
                layout.FontStyle, layout.FontUnit);
            previousFont.Dispose();
            control.Invalidate();
        }
        ClientSize = new Size((int)Math.Round(320 * factor), (int)Math.Round(34 * factor));
        ResumeLayout();
        Invalidate(true);
    }

    protected override void OnShown(EventArgs eventArgs)
    {
        base.OnShown(eventArgs);
        // WinForms applies a minimum form height during the first Show().
        // Reapply the requested size once the native window exists so the
        // background and the scaled controls have the same height on startup.
        SetSizePercent(sizePercent, force: true);
    }

    protected override void OnHandleCreated(EventArgs eventArgs)
    {
        base.OnHandleCreated(eventArgs);
        const int windowCornerPreference = 33;
        var roundCorners = 2;
        if (NativeMethods.DwmSetWindowAttribute(Handle, windowCornerPreference, ref roundCorners, sizeof(int)) != 0)
        {
            var shape = NativeMethods.CreateRoundRectRgn(0, 0, Width + 1, Height + 1, 14, 14);
            Region = Region.FromHrgn(shape);
            NativeMethods.DeleteObject(shape);
        }
    }
#endif
    private static Label Item(string text, int x, EventHandler? click, int width = 32)
    {
        var item = new Label { Text = text, Left = x, Top = 0, Width = width, Height = 34, Padding = new Padding(0, 4, 0, 0), TextAlign = ContentAlignment.MiddleCenter, ForeColor = Color.White, BackColor = Color.FromArgb(20, 20, 20), Font = new Font("Segoe UI Symbol", text == "Kamera" ? 9 : 12), Cursor = Cursors.Hand };
        if (click is not null) item.Click += click; return item;
    }
    private static void DrawChevronIcon(Graphics graphics, Rectangle bounds, bool pointsRight)
    {
        var state = graphics.Save();
        graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
        var scale = bounds.Width / 32f;
        using var pen = new Pen(Color.White, 1.7f * scale)
        {
            StartCap = System.Drawing.Drawing2D.LineCap.Round,
            EndCap = System.Drawing.Drawing2D.LineCap.Round,
            LineJoin = System.Drawing.Drawing2D.LineJoin.Round
        };
        var centerX = bounds.Left + bounds.Width / 2f;
#if BETA
        var centerY = bounds.Top + bounds.Height / 2f;
#else
        var centerY = bounds.Top + bounds.Height / 2f + 2f * scale;
#endif
        var direction = pointsRight ? 1f : -1f;
        graphics.DrawLines(pen,
        [
            new PointF(centerX - direction * 3f * scale, centerY - 6f * scale),
            new PointF(centerX + direction * 3f * scale, centerY),
            new PointF(centerX - direction * 3f * scale, centerY + 6f * scale)
        ]);
        graphics.Restore(state);
    }
    public async void Flash(string text)
    {
        note.Text = text; note.Left = (Width - note.PreferredWidth) / 2; note.Top = 0; note.Visible = true; await Task.Delay(1600); if (!IsDisposed) note.Visible = false;
    }
#if BETA
    private static void DrawGridIcon(Graphics graphics, Rectangle bounds)
    {
        graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        var scale = bounds.Width / 32f;
        using var pen = new Pen(Color.White, 1.35f * scale);
        var size = (int)Math.Round(5 * scale);
        var gap = (int)Math.Round(3 * scale);
        var left = (bounds.Width - size * 2 - gap) / 2;
        var top = (bounds.Height - size * 2 - gap) / 2;
        graphics.DrawRectangle(pen, left, top, size, size);
        graphics.DrawRectangle(pen, left + size + gap, top, size, size);
        graphics.DrawRectangle(pen, left, top + size + gap, size, size);
        graphics.DrawRectangle(pen, left + size + gap, top + size + gap, size, size);
    }

    public void SetGridMode(bool active)
    {
        grid.BackColor = active ? Color.FromArgb(62, 62, 68) : Color.FromArgb(20, 20, 20);
        toolTips.SetToolTip(grid, active ? "Einzelansicht" : "4-Kamera-Raster");
        grid.Invalidate();
    }

    public void SetRecording(bool active, bool manuallyStoppable = true)
    {
        recording.Text = "●";
        if (recordingIndicatorActive != active)
        {
            recordingIndicatorActive = active;
            recordingBlinkVisible = true;
            if (active) recordingBlinkTimer.Start(); else recordingBlinkTimer.Stop();
            recording.ForeColor = active ? Color.Red : Color.White;
        }
        toolTips.SetToolTip(recording, !active ? "Aufnahme starten" : manuallyStoppable
            ? "Aufnahme beenden und speichern" : "Automatische Bewegungsaufnahme läuft");
    }
#endif
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
    [DllImport("dwmapi.dll")] public static extern int DwmSetWindowAttribute(IntPtr window, int attribute, ref int value, int valueSize);
#endif
    [DllImport("gdi32.dll")] public static extern IntPtr CreateRoundRectRgn(int left, int top, int right, int bottom, int width, int height);
    [DllImport("gdi32.dll")] public static extern bool DeleteObject(IntPtr handle);
    public enum DwmWindowAttribute { NonClientRenderingPolicy = 2, UseImmersiveDarkMode = 20, WindowCornerPreference = 33, BorderColor = 34 }
    public enum DwmWindowCornerPreference { Default = 0, DoNotRound = 1, Round = 2, RoundSmall = 3 }
    [DllImport("dwmapi.dll")] public static extern int DwmSetWindowAttribute(IntPtr window, DwmWindowAttribute attribute, ref int value, int size);
    public static void DisableDwmBorder(IntPtr window)
    {
        // DWMWA_COLOR_NONE keeps Windows 11 corners rounded without the system outline.
        var noBorder = unchecked((int)0xFFFFFFFE);
        DwmSetWindowAttribute(window, DwmWindowAttribute.BorderColor, ref noBorder, sizeof(int));
    }
#if BETA
    public static void DisableOverlayDecoration(IntPtr window)
    {
        DisableDwmBorder(window);
        // The nearly transparent drag and resize windows need no DWM non-client
        // rendering, which can leave a separate shadow outside the camera image.
        var disabled = 1; // DWMNCRP_DISABLED
        DwmSetWindowAttribute(window, (int)DwmWindowAttribute.NonClientRenderingPolicy,
            ref disabled, sizeof(int));
    }
#endif
}

internal sealed class SettingsForm : Form
{
    private readonly DataGridView cameras = new() { Dock = DockStyle.Fill, AllowUserToAddRows = true, AllowUserToDeleteRows = true, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, RowHeadersVisible = false, SelectionMode = DataGridViewSelectionMode.FullRowSelect };
    private readonly CheckBox top = new() { Text = "Immer im Vordergrund", AutoSize = true };
    private readonly CheckBox autostart = new() { Text = "Mit Windows starten", AutoSize = true };
#if BETA
    private readonly ComboBox startBehavior = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 180 };
    private readonly ComboBox startCamera = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 180 };
    private readonly NumericUpDown toolbarSize = new() { Minimum = 50, Maximum = 100, Increment = 5, Width = 60 };
    private readonly CheckBox autoScaleToolbar = new() { Text = "Bedienleiste automatisch skalieren", AutoSize = true };
    private readonly CheckBox motionDetection = new() { Text = "Bewegungserkennung aktiv", AutoSize = true };
    private readonly CheckBox minimizeWhenInactive = new() { Text = "Bei Inaktivität minimieren", AutoSize = true };
    private readonly CheckBox restorePreviousCamera = new() { Text = "Vorherige Kamera wiederherstellen", AutoSize = true };
    private readonly NumericUpDown motionSeconds = new() { Minimum = 3, Maximum = 300, Value = 10, Width = 60 };
    private readonly NumericUpDown indicatorSeconds = new() { Minimum = 1, Maximum = 10, Value = 2, Width = 60 };
    private readonly CheckBox highlightMotionInGrid = new() { Text = "Bewegung im 4er-Raster hervorheben", AutoSize = true };
    private readonly CheckBox directHomeAssistant = new() { Text = "Direkt mit Home Assistant verbinden (empfohlen)", AutoSize = true };
    private readonly TextBox homeAssistantUrl = new() { Width = 300 };
    private readonly TextBox homeAssistantToken = new() { Width = 300, UseSystemPasswordChar = true };
    private readonly TextBox motionEntityId = new() { Width = 300 };
    private readonly TextBox personEntityId = new() { Width = 300 };
    private readonly Label selectedMotionCamera = new() { AutoSize = true, Text = "Keine Kamera ausgewählt", Anchor = AnchorStyles.Left };
    private readonly ComboBox motionAction = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 200 };
    private readonly ComboBox motionVideoSeconds = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 120 };
    private readonly ComboBox motionRetention = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 115 };
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
        ClientSize = new Size(
            Math.Clamp(current.SettingsWindowWidth, 760, Math.Max(760, workingArea.Width - 40)),
            Math.Clamp(current.SettingsWindowHeight, 610, Math.Max(610, workingArea.Height - 60)));
        FormClosed += (_, _) =>
        {
            current.SettingsWindowWidth = ClientSize.Width;
            current.SettingsWindowHeight = ClientSize.Height;
            // Window geometry is independent of whether the setting changes were saved.
            if (DialogResult != DialogResult.OK) SettingsStore.Save(current);
        };
        cameras.MinimumSize = new Size(0, 130);
        BackColor = Color.FromArgb(24, 24, 27);
        ForeColor = Color.FromArgb(242, 242, 244);
        Opacity = 1.0;
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
        cameras.Columns.Add(new DataGridViewCheckBoxColumn { Name = "PersonEnabled", HeaderText = "Person", FillWeight = 12 });
        cameras.Columns.Add(new DataGridViewTextBoxColumn { Name = "MotionEntityId", Visible = false });
        cameras.Columns.Add(new DataGridViewTextBoxColumn { Name = "MotionAction", Visible = false });
        cameras.Columns.Add(new DataGridViewTextBoxColumn { Name = "MotionVideoSeconds", Visible = false });
        cameras.Columns.Add(new DataGridViewTextBoxColumn { Name = "PersonEntityId", Visible = false });
        foreach (var camera in current.Cameras) cameras.Rows.Add(camera.Name, camera.StreamUrl, camera.MotionEnabled,
            camera.PersonEnabled ?? !string.IsNullOrWhiteSpace(camera.PersonEntityId), camera.MotionEntityId,
            camera.MotionAction, camera.MotionVideoSeconds, camera.PersonEntityId);
#else
        foreach (var camera in current.Cameras) cameras.Rows.Add(camera.Name, camera.StreamUrl);
#endif
        top.Checked = current.AlwaysOnTop; autostart.Checked = current.StartWithWindows;
#if BETA
        startBehavior.Items.AddRange(["Wie zuletzt", "Minimiert starten", "Mit Kamera starten", "Raster starten"]);
        startBehavior.SelectedIndex = current.StartBehavior switch { "Minimized" => 1, "Camera" => 2, "Grid" => 3, _ => 0 };
        foreach (var camera in current.Cameras) startCamera.Items.Add(camera.Name);
        if (startCamera.Items.Count > 0)
            startCamera.SelectedIndex = Math.Clamp(current.StartCameraIndex, 0, startCamera.Items.Count - 1);
        toolbarSize.Value = Math.Clamp(current.ToolbarSizePercent, 50, 100);
        autoScaleToolbar.Checked = current.AutoScaleToolbar;
        toolbarSize.Enabled = !autoScaleToolbar.Checked;
        autoScaleToolbar.CheckedChanged += (_, _) => toolbarSize.Enabled = !autoScaleToolbar.Checked;
        motionDetection.Checked = current.MotionDetectionEnabled;
        minimizeWhenInactive.Checked = current.MinimizeWhenInactive;
        restorePreviousCamera.Checked = current.RestorePreviousCameraAfterMotion;
        motionSeconds.Value = Math.Clamp(current.MotionForegroundSeconds, 3, 300);
        indicatorSeconds.Value = Math.Clamp(current.MotionIndicatorSeconds, 1, 10);
        highlightMotionInGrid.Checked = current.HighlightMotionInGrid;
        directHomeAssistant.Checked = current.DirectHomeAssistantEnabled;
        homeAssistantUrl.Text = current.HomeAssistantUrl;
        homeAssistantToken.Text = current.HomeAssistantToken;
        ignoreHomeAssistantCertificateErrors.Checked = current.IgnoreHomeAssistantCertificateErrors;
        motionAction.Items.AddRange(["Keine", "Snapshot", "Videoaufnahme", "Snapshot + Videoaufnahme"]);
        motionVideoSeconds.Items.AddRange(["15 Sekunden", "30 Sekunden", "60 Sekunden"]);
        motionRetention.Items.AddRange(["1 Tag", "3 Tage", "7 Tage", "14 Tage", "30 Tage", "Unbegrenzt"]);
        var retentionValues = new[] { 1, 3, 7, 14, 30, 0 };
        motionRetention.SelectedIndex = Math.Max(0, Array.IndexOf(retentionValues, current.MotionRetentionDays));
        var selectedCameraRow = -1;
        void StoreSelectedCameraMotion()
        {
            if (selectedCameraRow >= 0 && selectedCameraRow < cameras.Rows.Count && !cameras.Rows[selectedCameraRow].IsNewRow)
            {
                cameras.Rows[selectedCameraRow].Cells[4].Value = motionEntityId.Text.Trim();
                cameras.Rows[selectedCameraRow].Cells[5].Value = motionAction.SelectedIndex switch { 1 => "Snapshot", 2 => "Video", 3 => "Both", _ => "None" };
                cameras.Rows[selectedCameraRow].Cells[6].Value = motionVideoSeconds.SelectedIndex switch { 0 => 15, 2 => 60, _ => 30 };
                cameras.Rows[selectedCameraRow].Cells[7].Value = personEntityId.Text.Trim();
            }
        }
        void LoadSelectedCameraMotion()
        {
            StoreSelectedCameraMotion();
            selectedCameraRow = cameras.CurrentRow?.Index ?? -1;
            if (selectedCameraRow < 0 || selectedCameraRow >= cameras.Rows.Count || cameras.Rows[selectedCameraRow].IsNewRow)
            {
                selectedMotionCamera.Text = "Keine Kamera ausgewählt";
                motionEntityId.Text = "";
                personEntityId.Text = "";
                motionAction.SelectedIndex = 0;
                motionVideoSeconds.SelectedIndex = 1;
                return;
            }
            selectedMotionCamera.Text = Convert.ToString(cameras.Rows[selectedCameraRow].Cells[0].Value)?.Trim() is { Length: > 0 } name ? name : "Neue Kamera";
            motionEntityId.Text = Convert.ToString(cameras.Rows[selectedCameraRow].Cells[4].Value)?.Trim() ?? "";
            personEntityId.Text = Convert.ToString(cameras.Rows[selectedCameraRow].Cells[7].Value)?.Trim() ?? "";
            motionAction.SelectedIndex = Convert.ToString(cameras.Rows[selectedCameraRow].Cells[5].Value) switch { "Snapshot" => 1, "Video" => 2, "Both" => 3, _ => 0 };
            motionVideoSeconds.SelectedIndex = Convert.ToInt32(cameras.Rows[selectedCameraRow].Cells[6].Value ?? 30) switch { 15 => 0, 60 => 2, _ => 1 };
            motionVideoSeconds.Enabled = motionAction.SelectedIndex is 2 or 3;
        }
        motionAction.SelectedIndexChanged += (_, _) => motionVideoSeconds.Enabled = motionAction.SelectedIndex is 2 or 3;
        cameras.SelectionChanged += (_, _) => LoadSelectedCameraMotion();
        cameras.CurrentCellDirtyStateChanged += (_, _) => { if (cameras.IsCurrentCellDirty) cameras.CommitEdit(DataGridViewDataErrorContexts.Commit); };
        void RefreshStartCameraChoices()
        {
            var selected = startCamera.SelectedIndex;
            startCamera.Items.Clear();
            foreach (DataGridViewRow row in cameras.Rows)
            {
                if (row.IsNewRow) continue;
                var name = Convert.ToString(row.Cells[0].Value)?.Trim() ?? "";
                var url = Convert.ToString(row.Cells[1].Value)?.Trim() ?? "";
                if (name.Length > 0 || url.Length > 0) startCamera.Items.Add(name.Length > 0 ? name : "Neue Kamera");
            }
            if (startCamera.Items.Count > 0) startCamera.SelectedIndex = Math.Clamp(selected, 0, startCamera.Items.Count - 1);
        }
        cameras.CellValueChanged += (_, eventArgs) => { if (eventArgs.ColumnIndex is 0 or 1) RefreshStartCameraChoices(); };
        cameras.RowsRemoved += (_, _) => RefreshStartCameraChoices();
        if (cameras.Rows.Count > 0) { cameras.Rows[0].Selected = true; cameras.CurrentCell = cameras.Rows[0].Cells[0]; }
        LoadSelectedCameraMotion();
        void UpdateMotionOptions()
        {
            var enabled = motionDetection.Checked && !top.Checked;
            motionSeconds.Enabled = enabled;
            minimizeWhenInactive.Enabled = enabled;
            restorePreviousCamera.Enabled = enabled;
            directHomeAssistant.Enabled = motionDetection.Checked;
            var directEnabled = motionDetection.Checked && directHomeAssistant.Checked;
            homeAssistantUrl.Enabled = directEnabled;
            homeAssistantToken.Enabled = directEnabled;
            motionEntityId.Enabled = directEnabled;
            personEntityId.Enabled = directEnabled;
            ignoreHomeAssistantCertificateErrors.Enabled = directEnabled;
            testHomeAssistant.Enabled = directEnabled;
        }
        UpdateMotionOptions();
        top.CheckedChanged += (_, _) => UpdateMotionOptions();
        motionDetection.CheckedChanged += (_, _) => UpdateMotionOptions();
        directHomeAssistant.CheckedChanged += (_, _) => UpdateMotionOptions();
#endif
#if BETA
        var table = new TableLayoutPanel { Dock = DockStyle.Fill, AutoScroll = true, Padding = new Padding(14), ColumnCount = 1, RowCount = 7 };
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 180));
        for (var row = 1; row < table.RowCount; row++) table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
#else
        var table = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(14), ColumnCount = 1, RowCount = 5 };
        table.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
#endif
        table.Controls.Add(cameras, 0, 0);
#if BETA
        table.Controls.Add(new Label { Name = "CameraHint", Text = "Kamera anklicken, Sensoren unten eintragen und Bewegung oder Person in der Liste aktivieren.", AutoSize = true, ForeColor = SystemColors.GrayText, Margin = new Padding(3, 5, 3, 5) }, 0, 1);
#else
        table.Controls.Add(new Label { Text = "Beispiel: rtsp://192.168.x.x:8554/Einfahrt", AutoSize = true, ForeColor = SystemColors.GrayText }, 0, 1);
#endif
#if BETA
        var options = new TableLayoutPanel { Name = "Options", Dock = DockStyle.Top, AutoSize = true, ColumnCount = 1, RowCount = 2, Margin = new Padding(0) };
        options.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        options.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        var generalOptions = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, WrapContents = false, Margin = new Padding(0) };
        generalOptions.Controls.Add(top);
        generalOptions.Controls.Add(autostart);
        options.Controls.Add(generalOptions, 0, 0);

        var toolbarOptions = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, WrapContents = false, Margin = new Padding(0) };
        toolbarOptions.Controls.Add(new Label { Text = "Bedienleiste:", AutoSize = true, Margin = new Padding(3, 4, 3, 0) });
        toolbarOptions.Controls.Add(toolbarSize);
        toolbarOptions.Controls.Add(new Label { Text = "%", AutoSize = true, Margin = new Padding(3, 4, 3, 0) });
        toolbarOptions.Controls.Add(autoScaleToolbar);
        options.Controls.Add(toolbarOptions, 0, 1);
#else
        var options = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true };
        options.Controls.Add(top);
        options.Controls.Add(autostart);
#endif
#if BETA
        var startupOptions = new FlowLayoutPanel { Name = "StartupOptions", Dock = DockStyle.Fill, AutoSize = true };
        startupOptions.Controls.Add(new Label { Text = "Beim Start:", AutoSize = true, Margin = new Padding(3, 4, 3, 0) });
        startupOptions.Controls.Add(startBehavior);
        var startCameraLabel = new Label { Text = "Kamera:", AutoSize = true, Margin = new Padding(18, 4, 3, 0) };
        startupOptions.Controls.Add(startCameraLabel);
        startupOptions.Controls.Add(startCamera);
        void UpdateStartCameraOption()
        {
            startCameraLabel.Visible = startBehavior.SelectedIndex == 2;
            startCamera.Visible = startBehavior.SelectedIndex == 2;
        }
        startBehavior.SelectedIndexChanged += (_, _) => UpdateStartCameraOption();
        UpdateStartCameraOption();
        var generalGroup = new GroupBox { Text = "Allgemeine Einstellungen", Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(10) };
        var generalFields = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 1, RowCount = 2 };
        generalFields.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        generalFields.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        generalFields.Controls.Add(options, 0, 0);
        generalFields.Controls.Add(startupOptions, 0, 1);
        generalGroup.Controls.Add(generalFields);
        table.Controls.Add(generalGroup, 0, 2);

        var activityOptions = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 1, RowCount = 3 };
        for (var row = 0; row < activityOptions.RowCount; row++)
            activityOptions.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        activityOptions.Controls.Add(motionDetection, 0, 0);

        var motionOptions = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, WrapContents = false, Margin = new Padding(0) };
        motionOptions.Controls.Add(minimizeWhenInactive);
        motionOptions.Controls.Add(restorePreviousCamera);
        motionOptions.Controls.Add(new Label { Text = "Vordergrunddauer:", AutoSize = true, Margin = new Padding(18, 4, 3, 0) });
        motionOptions.Controls.Add(motionSeconds);
        motionOptions.Controls.Add(new Label { Text = "Sekunden", AutoSize = true, Margin = new Padding(3, 4, 3, 0) });
        activityOptions.Controls.Add(motionOptions, 0, 1);

        var indicatorOptions = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, WrapContents = false, Margin = new Padding(0) };
        indicatorOptions.Controls.Add(new Label { Text = "Aktivitätssymbole anzeigen:", AutoSize = true, Margin = new Padding(3, 4, 3, 0) });
        indicatorOptions.Controls.Add(indicatorSeconds);
        indicatorOptions.Controls.Add(new Label { Text = "Sekunden", AutoSize = true, Margin = new Padding(3, 4, 12, 0) });
        indicatorOptions.Controls.Add(highlightMotionInGrid);
        activityOptions.Controls.Add(indicatorOptions, 0, 2);

        var activityGroup = new GroupBox { Text = "Bewegung und Aktivitätsanzeige", Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(10) };
        activityGroup.Controls.Add(activityOptions);
        table.Controls.Add(activityGroup, 0, 3);
#else
        table.Controls.Add(options, 0, 2);
#endif
#if BETA
        var captureOptions = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, WrapContents = false, Margin = new Padding(0) };
        captureOptions.Controls.Add(motionAction);
        captureOptions.Controls.Add(motionVideoSeconds);
        var homeAssistantGroup = new GroupBox { Text = "Bewegung pro Kamera und Home Assistant", Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(10) };
        var homeAssistantFields = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2, RowCount = 10 };
        homeAssistantFields.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        homeAssistantFields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        homeAssistantFields.Controls.Add(directHomeAssistant, 0, 0);
        homeAssistantFields.SetColumnSpan(directHomeAssistant, 2);
        homeAssistantFields.Controls.Add(new Label { Text = "HA-Adresse:", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 1);
        homeAssistantFields.Controls.Add(homeAssistantUrl, 1, 1);
        homeAssistantFields.Controls.Add(new Label { Text = "Langzeit-Token:", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 2);
        homeAssistantFields.Controls.Add(homeAssistantToken, 1, 2);
        homeAssistantFields.Controls.Add(ignoreHomeAssistantCertificateErrors, 0, 3);
        homeAssistantFields.SetColumnSpan(ignoreHomeAssistantCertificateErrors, 2);
        var testRow = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, WrapContents = false };
        testRow.Controls.Add(testHomeAssistant);
        testRow.Controls.Add(homeAssistantStatus);
        homeAssistantFields.Controls.Add(testRow, 0, 4);
        homeAssistantFields.SetColumnSpan(testRow, 2);
        homeAssistantFields.Controls.Add(new Label { Text = "Ausgewählte Kamera:", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 5);
        homeAssistantFields.Controls.Add(selectedMotionCamera, 1, 5);
        homeAssistantFields.Controls.Add(new Label { Text = "Bewegungs-Entität:", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 6);
        homeAssistantFields.Controls.Add(motionEntityId, 1, 6);
        homeAssistantFields.Controls.Add(new Label { Text = "Personen-Entität (Snapshot):", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 7);
        homeAssistantFields.Controls.Add(personEntityId, 1, 7);
        homeAssistantFields.Controls.Add(new Label { Text = "Aufzeichnung bei Bewegung:", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 8);
        homeAssistantFields.Controls.Add(captureOptions, 1, 8);
        homeAssistantFields.Controls.Add(new Label { Text = "Aufbewahrung (alle Kameras):", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 9);
        homeAssistantFields.Controls.Add(motionRetention, 1, 9);
        homeAssistantGroup.Controls.Add(homeAssistantFields);
        table.Controls.Add(homeAssistantGroup, 0, 4);
#endif
#if BETA
        table.Controls.Add(new Label { Text = $"Version {Application.ProductVersion.Split('+')[0]}", AutoSize = true, ForeColor = SystemColors.GrayText, Anchor = AnchorStyles.Left }, 0, 5);
#else
        table.Controls.Add(new Label { Text = $"Version {Application.ProductVersion.Split('+')[0]}", AutoSize = true, ForeColor = SystemColors.GrayText, Anchor = AnchorStyles.Left }, 0, 3);
#endif
#if BETA
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, WrapContents = false, FlowDirection = FlowDirection.RightToLeft };
#else
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
#endif
        var ok = new Button { Text = "Speichern", DialogResult = DialogResult.OK, AutoSize = true };
        buttons.Controls.Add(ok); buttons.Controls.Add(new Button { Text = "Abbrechen", DialogResult = DialogResult.Cancel, AutoSize = true });
#if BETA
        table.Controls.Add(buttons, 0, 6);
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
                var selectedRow = selectedCameraRow >= 0 && selectedCameraRow < cameras.Rows.Count
                    ? cameras.Rows[selectedCameraRow] : null;
                var motionId = selectedRow is not null && Convert.ToBoolean(selectedRow.Cells[2].Value ?? false)
                    ? motionEntityId.Text.Trim() : "";
                var personId = selectedRow is not null && Convert.ToBoolean(selectedRow.Cells[3].Value ?? false)
                    ? personEntityId.Text.Trim() : "";
                if (!Uri.TryCreate(homeAssistantUrl.Text.Trim(), UriKind.Absolute, out var testUri) ||
                    (testUri.Scheme != Uri.UriSchemeHttp && testUri.Scheme != Uri.UriSchemeHttps) ||
                    string.IsNullOrWhiteSpace(homeAssistantToken.Text) ||
                    (motionId.Length == 0 && personId.Length == 0))
                {
                    homeAssistantStatus.ForeColor = Color.Firebrick;
                    homeAssistantStatus.Text = "Adresse, Token und mindestens einen aktivierten Sensor eintragen.";
                    return;
                }
                var error = await MonitorForm.TestHomeAssistantConnectionAsync(homeAssistantUrl.Text.Trim(), homeAssistantToken.Text.Trim(),
                    motionId, ignoreHomeAssistantCertificateErrors.Checked, personId);
                homeAssistantStatus.ForeColor = error is null ? Color.ForestGreen : Color.Firebrick;
                homeAssistantStatus.Text = error is null ? "Verbunden – eingetragene Sensoren gefunden." : error;
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
            if (startBehavior.SelectedIndex == 2 && (startCamera.SelectedIndex < 0 || startCamera.SelectedIndex >= entries.Count))
            {
                MessageBox.Show(this, "Bitte eine Startkamera auswählen.", "Startverhalten", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                DialogResult = DialogResult.None; return;
            }
            if (directHomeAssistant.Checked && entries.Any(camera => camera.MotionEnabled || camera.PersonEnabled == true) &&
                (!Uri.TryCreate(homeAssistantUrl.Text.Trim(), UriKind.Absolute, out var haUri) ||
                 (haUri.Scheme != Uri.UriSchemeHttp && haUri.Scheme != Uri.UriSchemeHttps) ||
                 string.IsNullOrWhiteSpace(homeAssistantToken.Text) ||
                 !entries.Any(camera =>
                    (camera.MotionEnabled && !string.IsNullOrWhiteSpace(camera.MotionEntityId)) ||
                    (camera.PersonEnabled == true && !string.IsNullOrWhiteSpace(camera.PersonEntityId)))))
            {
                MessageBox.Show(this, "Bitte HA-Adresse und Langzeit-Token eintragen sowie für mindestens eine aktivierte Kamera eine Sensor-Entität hinterlegen.", "Home Assistant unvollständig", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                DialogResult = DialogResult.None; return;
            }
#endif
            Result = new Settings
            {
                Cameras = entries, SelectedCamera = Math.Clamp(current.SelectedCamera, 0, entries.Count - 1),
                AlwaysOnTop = top.Checked, StartWithWindows = autostart.Checked,
                Left = current.Left, Top = current.Top, Width = current.Width, Height = current.Height,
#if BETA
                LastMonitorDeviceName = current.LastMonitorDeviceName,
                MonitorOffsetX = current.MonitorOffsetX, MonitorOffsetY = current.MonitorOffsetY,
                LastGridMode = current.LastGridMode,
                StartBehavior = startBehavior.SelectedIndex switch { 1 => "Minimized", 2 => "Camera", 3 => "Grid", _ => "Last" },
                StartCameraIndex = Math.Max(0, startCamera.SelectedIndex),
                ToolbarSizePercent = (int)toolbarSize.Value,
                AutoScaleToolbar = autoScaleToolbar.Checked,
                MotionDetectionEnabled = motionDetection.Checked,
                MotionActionsPausedUntilUtc = current.MotionActionsPausedUntilUtc,
                MotionForegroundSeconds = (int)motionSeconds.Value,
                MotionIndicatorSeconds = (int)indicatorSeconds.Value,
                HighlightMotionInGrid = highlightMotionInGrid.Checked,
                MinimizeWhenInactive = minimizeWhenInactive.Checked,
                RestorePreviousCameraAfterMotion = restorePreviousCamera.Checked,
                DirectHomeAssistantEnabled = directHomeAssistant.Checked,
                HomeAssistantUrl = homeAssistantUrl.Text.Trim(),
                HomeAssistantToken = homeAssistantToken.Text.Trim(),
                IgnoreHomeAssistantCertificateErrors = ignoreHomeAssistantCertificateErrors.Checked,
                PerCameraMotionConfigured = true,
                MotionRetentionDays = retentionValues[motionRetention.SelectedIndex],
                SettingsWindowWidth = ClientSize.Width,
                SettingsWindowHeight = ClientSize.Height
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
            entry.PersonEnabled = Convert.ToBoolean(row.Cells[3].Value ?? false);
            entry.MotionEntityId = Convert.ToString(row.Cells[4].Value)?.Trim() ?? "";
            entry.MotionAction = Convert.ToString(row.Cells[5].Value) ?? "None";
            entry.MotionVideoSeconds = Convert.ToInt32(row.Cells[6].Value ?? 30);
            entry.PersonEntityId = Convert.ToString(row.Cells[7].Value)?.Trim() ?? "";
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
                case ComboBox combo:
                    combo.BackColor = field;
                    combo.ForeColor = text;
                    combo.FlatStyle = FlatStyle.Flat;
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
