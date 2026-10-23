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
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
#if BETA
        MonitorForm form;
        try { form = new MonitorForm(); }
        catch (Exception error) { MessageBox.Show(error.Message, ApplicationBranding.ProductName, MessageBoxButtons.OK, MessageBoxIcon.Error); return; }
        Application.Run(form);
#else
        Application.Run(new MonitorForm());
#endif
    }
}

#if BETA
internal static class ApplicationBranding
{
    #if V2_BETA
    internal const string ProductName = ApplicationChannel.ProductName;
#elif RELEASE_V1
    internal const string ProductName = "HomeCam Monitor";
#else
    internal const string ProductName = "HomeCamMonitor Beta";
#endif
    private static readonly Icon icon = LogoVariants.LoadIcon(0);
    internal static Icon WindowIcon => icon;

    internal static void ApplyWindowIcon(Form form)
    {
#if STORE_PACKAGE && V2_BETA
        _ = WindowIconTheme.Bind(form);
#else
        form.Icon = WindowIcon;
#endif
    }

}
#endif

internal sealed class Settings
{
    public List<CameraEntry> Cameras { get; set; } = [];
#if V2_BETA
    public string ReolinkHelperPath { get; set; } = Path.Combine(AppContext.BaseDirectory, "tools", "neolink", "neolink.exe");
#endif
    public int SelectedCamera { get; set; }
    public bool AlwaysOnTop { get; set; } = true;
    public bool StartWithWindows { get; set; }
    public int Left { get; set; } = -1;
    public int Top { get; set; } = -1;
    public int Width { get; set; } = 480;
    public int Height { get; set; } = 270;
#if BETA
    // Retained serialized field for compatibility with existing settings.
    public bool BetaWindowLoggingEnabled { get; set; }
    public string LastMonitorDeviceName { get; set; } = "";
    public int MonitorOffsetX { get; set; }
    public int MonitorOffsetY { get; set; }
    public string StartBehavior { get; set; } = "Last";
    public int StartCameraIndex { get; set; }
    public bool LastGridMode { get; set; }
    public int ToolbarSizePercent { get; set; } = 100;
    public bool AutoScaleToolbar { get; set; }
    public bool ShowGridCameraNames { get; set; } = true;
    public bool ShowEmptyCameraLogo { get; set; } = true;
    public bool ShowEmptyFourthFieldBorder { get; set; }
#if V2_BETA
    // Legacy master switch is ignored on import; only per-camera choices gate events.
    [System.Text.Json.Serialization.JsonIgnore]
    public bool MotionDetectionEnabled
    {
        get => Cameras.Any(c => c.Active && (c.MotionEnabled && (!c.Direct.Enabled || c.LocalMotion.Enabled) ||
            !c.Direct.Enabled && !c.Reolink.Enabled && (c.PersonEnabled ?? !string.IsNullOrWhiteSpace(c.PersonEntityId))));
        set { }
    }
#else
    public bool MotionDetectionEnabled { get; set; } = true;
#endif
    public DateTime? MotionActionsPausedUntilUtc { get; set; }
    public int MotionForegroundSeconds { get; set; } = 10;
    public int MotionIndicatorSeconds { get; set; } = 2;
    public bool HighlightMotionInGrid { get; set; }
    public bool MinimizeWhenInactive { get; set; }
    public bool MinimizeAfterManualRestore { get; set; }
    public bool ManualRestoreKeepForeground { get; set; } = true;
    public int ManualRestoreSeconds { get; set; } = 10;
    public bool RestorePreviousCameraAfterMotion { get; set; } = true;
    public bool DirectHomeAssistantEnabled { get; set; }
    public string HomeAssistantUrl { get; set; } = "";
    public string HomeAssistantToken { get; set; } = "";
    public string MotionEntityId { get; set; } = "binary_sensor.camera_einfahrt_bewegung";
    public string MotionCameraName { get; set; } = "Einfahrt";
    public bool IgnoreHomeAssistantCertificateErrors { get; set; }
    public bool PerCameraMotionConfigured { get; set; }
    public int MotionRetentionDays { get; set; } = 7;
    public int SnapshotPreRollSeconds { get; set; }
    public int VideoPreRollSeconds { get; set; }
    public string ManualSnapshotFolder { get; set; } = "";
    public string ManualVideoFolder { get; set; } = "";
    public string MotionSnapshotFolder { get; set; } = "";
    public string MotionVideoFolder { get; set; } = "";
    public int SettingsWindowWidth { get; set; } = 980;
    public int SettingsWindowHeight { get; set; } = 780;
#endif
}

internal sealed class CameraEntry
#if V2_BETA
    : System.Text.Json.Serialization.IJsonOnDeserialized
#endif
{
#if V2_BETA
    public bool Active { get; set; } = true;
    public bool RecordingEnabled { get; set; } = true;
#else
    [System.Text.Json.Serialization.JsonIgnore]
    internal bool Active => true;
#endif
    [System.Text.Json.Serialization.JsonIgnore]
    internal bool CanRecord => Active
#if V2_BETA
        && RecordingEnabled && (!Direct.Enabled || LocalMotion.Enabled)
#endif
        ;
    public string Name { get; set; } = UiText.T("Kamera");
#if V2_BETA
    [System.Text.Json.Serialization.JsonIgnore]
#endif
    public string StreamUrl { get; set; } = "";
#if V2_BETA
    // Runtime URLs may contain credentials for mpv/FFmpeg. Persist only the
    // clean direct URL; rebuild its runtime form from the DPAPI-protected field.
    [System.Text.Json.Serialization.JsonPropertyName("StreamUrl")]
    public string StoredStreamUrl
    {
        get
        {
            if (Direct?.Enabled != true) return HomeAssistantRuntimeSource ?? StreamUrl;
            return Uri.TryCreate(Direct.RtspUrl, UriKind.Absolute, out var url)
                ? new UriBuilder(url) { UserName = "", Password = "" }.Uri.AbsoluteUri : "";
        }
        set => StreamUrl = value;
    }
    void System.Text.Json.Serialization.IJsonOnDeserialized.OnDeserialized()
    {
        if (Direct?.Enabled == true) StreamUrl = Direct.StreamUrlOrEmpty();
    }
    public ReolinkCamera Reolink { get; set; } = new();
    public DirectCamera Direct { get; set; } = new();
    public string HomeAssistantStreamUrl { get; set; } = "";
    public string HomeAssistantEntityId { get; set; } = "";
    private LocalMotionSettings localMotion = new();
    public LocalMotionSettings LocalMotion { get => localMotion; set => localMotion = value ?? new(); }
    [System.Text.Json.Serialization.JsonIgnore]
    internal string? HomeAssistantRuntimeSource { get; set; }
#endif
    [System.Text.Json.Serialization.JsonIgnore]
    internal bool IsAvailable => Active && Uri.TryCreate(StreamUrl, UriKind.Absolute, out _);
#if BETA
    [System.Text.Json.Serialization.JsonIgnore]
    internal bool ReceivesHomeAssistantEvents => Active
#if V2_BETA
        && !Reolink.Enabled && !Direct.Enabled
#endif
        ;
    [System.Text.Json.Serialization.JsonIgnore]
    internal bool SupportsPersonDetection => ReceivesHomeAssistantEvents && !string.IsNullOrWhiteSpace(PersonEntityId);
    [System.Text.Json.Serialization.JsonIgnore]
    internal bool UsesExternalMotion => MotionEnabled
#if V2_BETA
        && !LocalMotion.Enabled
#endif
        ;
#endif
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
    internal static string Folder => SettingsLocation.CurrentFolder;
    private static readonly string StableFileName = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "HomeCamMonitor", "settings.json");
#else
    private static readonly string Folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "HomeCamMonitor");
#endif
    private static string FileName => Path.Combine(Folder, "settings.json");
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    public static Settings Load()
    {
#if BETA
        using (var reset = Registry.CurrentUser.OpenSubKey(SettingsLocation.RegistryPath, true))
        {
            if (reset?.GetValue("ResetSettings") is int requested && requested == 1)
            {
                var fresh = CreateForNewInstallation();
                Save(fresh);
                reset.DeleteValue("ResetSettings", false);
                return fresh;
            }
        }
        if (!SettingsLocation.SameFolder(Folder, SettingsLocation.DefaultFolder) && !File.Exists(FileName))
            throw new IOException(UiText.F("Die Einstellungsdatei ist nicht erreichbar: {0}\nBitte den gewählten Ordner bzw. die Netzwerkverbindung prüfen.", FileName));
#endif
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
#if BETA && !V2_BETA
            if (File.Exists(StableFileName))
            {
                var imported = JsonSerializer.Deserialize<Settings>(File.ReadAllText(StableFileName)) ?? new Settings();
                MigratePerCameraMotion(imported);
                return imported;
            }
#endif
        }
        catch
        {
#if BETA
            if (!SettingsLocation.SameFolder(Folder, SettingsLocation.DefaultFolder))
                throw new IOException(UiText.F("Die Einstellungen im gewählten Ordner konnten nicht geladen werden: {0}", FileName));
#endif
        }
#if BETA
        return CreateForNewInstallation();
#else
        return new Settings();
#endif
    }
#if BETA
    // Apply revised defaults only when neither Beta nor Stable settings were loaded.
    internal static Settings CreateForNewInstallation() => new()
    {
        AlwaysOnTop = false,
        ToolbarSizePercent = 90,
        AutoScaleToolbar = true,
        ShowGridCameraNames = false,
        MinimizeWhenInactive = true,
        MotionIndicatorSeconds = 1,
        #if V2_BETA
        DirectHomeAssistantEnabled = false,
#else
        DirectHomeAssistantEnabled = true,
#endif
        PerCameraMotionConfigured = true
    };
#endif
    public static void Save(Settings value)
    {
        Directory.CreateDirectory(Folder);
#if BETA
        var temporary = FileName + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try { File.WriteAllText(temporary, JsonSerializer.Serialize(value, JsonOptions)); File.Move(temporary, FileName, true); }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
#else
        File.WriteAllText(FileName, JsonSerializer.Serialize(value, JsonOptions));
#endif
    }
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

#if BETA
// WinForms TopMost and BringToFront call SetWindowPos without NOACTIVATE.
// Keep the native z-order separate from keyboard activation for automatic UI.
internal abstract class NonActivatingForm : Form
{
    private bool topMost;
    public new bool TopMost
    {
        get => topMost;
        set
        {
            topMost = value;
            if (IsHandleCreated)
                NativeMethods.SetWindowPos(Handle, value ? NativeMethods.HwndTopMost : new IntPtr(-2), 0, 0, 0, 0,
                    NativeMethods.SwpNoMove | NativeMethods.SwpNoSize | NativeMethods.SwpNoActivate);
        }
    }
    public new void BringToFront()
    {
        if (IsHandleCreated)
            NativeMethods.SetWindowPos(Handle, IntPtr.Zero, 0, 0, 0, 0,
                NativeMethods.SwpNoMove | NativeMethods.SwpNoSize | NativeMethods.SwpNoActivate);
    }
    protected override CreateParams CreateParams
    {
        get
        {
            var parameters = base.CreateParams;
            parameters.ExStyle = topMost ? parameters.ExStyle | 8 : parameters.ExStyle & ~8;
            return parameters;
        }
    }
}
internal sealed partial class MonitorForm : NonActivatingForm
#else
internal sealed partial class MonitorForm : Form
#endif
{
#if BETA
#if NEXT_BETA
    internal int VideoTransitionStatus { get; private set; } = int.MinValue;
#endif
#if V2_BETA
    protected override void OnLayout(LayoutEventArgs eventArgs)
    {
        // Windows gives a minimized popup a 160x28 client area. Docking mpv's
        // host to it shrinks the swapchain and exposes its expansion on restore.
        if (WindowState == FormWindowState.Minimized) return;
        base.OnLayout(eventArgs);
    }
#endif
    protected override void OnHandleCreated(EventArgs eventArgs)
    {
        base.OnHandleCreated(eventArgs);
#if NEXT_BETA
        // The video also participates in Windows' opening/restore transition.
        // Disable that transition before this HWND first becomes visible.
        VideoTransitionStatus = NativeMethods.DisableVideoTransitions(Handle);
#endif
        if (WindowState == FormWindowState.Minimized && settings.StartBehavior == "Minimized" && HasUsableCamera())
            startupTaskbarPreview ??= new TaskbarPreview(this, CaptureStartupPreviewAsync, size => offlinePlaceholder.CreatePreview(size));
        windowDiagnostics?.Dispose();
        windowDiagnostics = settings.BetaWindowLoggingEnabled ? new NativeWindowDiagnostics(Handle) : null;
    }

    protected override void OnHandleDestroyed(EventArgs eventArgs)
    {
        startupTaskbarPreview?.Dispose(); startupTaskbarPreview = null;
        base.OnHandleDestroyed(eventArgs);
    }

    protected override bool ShowWithoutActivation => true;
    protected override CreateParams CreateParams
    {
        get
        {
            var parameters = base.CreateParams;
            // The monitor has its own controls and no native title area.
            parameters.Style = (parameters.Style & ~0x00C40000) | unchecked((int)0x80000000); // WS_POPUP, no caption/frame
            return parameters;
        }
    }
#endif
    private Panel video = new() { Dock = DockStyle.Fill, BackColor = Color.Black };
#if BETA
    private Panel standbyVideo = new() { Dock = DockStyle.Fill, BackColor = Color.Black };
    private readonly CameraPlaceholderPanel offlinePlaceholder = new() { Dock = DockStyle.Fill, Visible = false };
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
    private readonly Dictionary<Process, string> motionProcessCameras = [];
    private readonly Panel cameraGrid = new()
    {
        Dock = DockStyle.Fill,
        BackColor = Color.Black,
        Padding = new Padding(0),
        Visible = false
    };
    private readonly List<GridPlayerSlot> gridSlots = [];
    private readonly System.Windows.Forms.Timer cameraLayoutTimer = new() { Interval = 250 };
    private NativeWindowDiagnostics? windowDiagnostics;
    private TaskbarPreview? startupTaskbarPreview;
    private readonly StreamDiagnostics streamDiagnostics = new();
    private SettingsForm? preparedSettingsDialog;
    private Settings? preparedSettingsSource;
    private string? preparedSettingsFingerprint;
    private readonly System.Windows.Forms.Timer settingsPreparationTimer = new() { Interval = 1000 };
    private readonly System.Windows.Forms.Timer videoDecorationTimer = new() { Interval = 50 };
    private CancellationTokenSource? refreshCancellation;
    private CancellationTokenSource? initialPlayerReveal;
    private CancellationTokenSource? initialGridReveal;
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
    private readonly Dictionary<CameraEntry, MotionPreRoll> preRollBuffers = [];
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
#endif

#if BETA
    public MonitorForm() : this(SettingsStore.Load()) { }
    internal MonitorForm(Settings initialSettings)
    {
        settings = initialSettings;
#else
    public MonitorForm()
    {
        settings = SettingsStore.Load();
#endif
#if BETA
        ApplicationBranding.ApplyWindowIcon(this);
        offlinePlaceholder.SetLogoVariant(0);
        CleanupMotionStorage();
        // Windows uses the title for taskbar previews and Alt+Tab. CreateParams
        // and non-client painting keep the camera window itself captionless.
        Text = ApplicationBranding.ProductName;
        AccessibleName = ApplicationBranding.ProductName;
#else
        #if V2_BETA
        Text = ApplicationBranding.ProductName;
#else
        Text = "HomeCamMonitor for Homeassistant";
#endif
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
        Controls.Add(offlinePlaceholder);
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
                manualRestoreTimer.Stop();
                return;
            }
#endif
#if BETA
            startupTaskbarPreview?.Dispose(); startupTaskbarPreview = null;
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
                ArmManualRestoreTimer();
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
        manualRestoreTimer.Tick += (_, _) => CheckManualRestoreTimeout();
        motionIndicatorTimer.Interval = Math.Clamp(settings.MotionIndicatorSeconds, 1, 10) * 1000;
        motionIndicatorTimer.Tick += (_, _) => HideMotionIndicator();
        motionPauseTimer.Tick += (_, _) => ScheduleMotionPauseExpiry();
        ScheduleMotionPauseExpiry();
        cameraLayoutTimer.Tick += (_, _) =>
        {
            cameraLayoutTimer.Stop();
            AlignCameraSurfaces();
        };
        videoDecorationTimer.Tick += (_, _) =>
        {
            // mpv creates its HWND asynchronously, even when a stream never opens.
            // Sanitize it while connecting too, rather than waiting for a frame.
            foreach (var surface in gridSlots.SelectMany(slot => new[] { slot.ActiveSurface, slot.SpareSurface })
                .Concat(new[] { video, standbyVideo }))
                if (surface.IsHandleCreated) NativeMethods.PrepareVideoChildren(surface.Handle);
        };
        videoDecorationTimer.Start();
#endif
#if BETA
        settingsPreparationTimer.Tick += (_, _) => PrepareSettingsWhenIdle();
#endif
        FormClosing += (_, _) => CloseMonitor();
#if BETA
        // Set the native startup state before the first handle/show. Minimizing
        // from Shown briefly displays the normal camera window on the desktop.
        if (settings.StartBehavior == "Minimized" && HasUsableCamera())
        {
            TopMost = false;
            wasMinimized = true;
            WindowState = FormWindowState.Minimized;
        }
#endif
        ApplyRoundedCorners();
    }


    private void InitializeMonitor()
    {
#if BETA
        NativeMethods.SetDiagnosticCaptionColor(Handle, Color.Black);
#endif
        if (!File.Exists(Path.Combine(AppContext.BaseDirectory, "mpv.exe")))
        {
            MessageBox.Show(this, UiText.T("mpv.exe fehlt. Bitte den vollständigen Ordner aus dem GitHub-Artefakt entpacken."), "HomeCam Monitor", MessageBoxButtons.OK, MessageBoxIcon.Error);
            Close(); return;
        }
        // Set ownership and geometry before any overlay becomes visible.
        // Showing an unpositioned layered form can flash its default window
        // at the desktop origin before PositionOverlays runs.
        dragSurface = new DragSurfaceForm(this) { Owner = this, Bounds = Bounds };
        toolbar = new ToolbarForm(this) { Owner = this };
#if BETA
        toolbar.SetSizePercent(GetToolbarSizePercent(settings, Width));
#endif
        CreateResizeGrips();
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
            slot.Placeholder.ContextMenuStrip = cameraContextMenu;
        }
        dragSurface.ContextMenuStrip = cameraContextMenu;
        toolbar.ContextMenuStrip = cameraContextMenu;
        foreach (var resizeGrip in resizeGrips) resizeGrip.ContextMenuStrip = cameraContextMenu;
        motionIndicator = new MotionIndicatorForm { Owner = this };
#endif
        NormalizeCameraSelection();
#if V2_BETA
        if (!HasUsableCamera() && settings.Cameras.Count == 0) OpenSettings();
#else
        if (!HasUsableCamera()) OpenSettings();
#endif
        if (closing) return;
#if BETA
        if (settings.StartBehavior == "Camera" && settings.Cameras.Count > 0)
            settings.SelectedCamera = Math.Clamp(settings.StartCameraIndex, 0, settings.Cameras.Count - 1);
        NormalizeCameraSelection();
#endif
        UpdateToolbar(); PositionOverlays();
#if !BETA
        toolbar.Show(this);
#endif
#if V2_BETA
        ConfigureReolink();
        ConfigureHomeAssistantStreams();
        ConfigureLocalMotion();
#endif
        StartPlayer(); latencyTimer.Start(); controlsTimer.Start();
#if BETA
        if ((settings.StartBehavior == "Grid" || settings.StartBehavior == "Last" && settings.LastGridMode) &&
            settings.Cameras.Count(camera => camera.IsAvailable) >= 2)
            ToggleGridView();
        if (settings.StartBehavior == "Minimized" && WindowState != FormWindowState.Minimized) MinimizeWindow();
        RestartMotionIntegration();
        UpdatePreRollBuffers();
        settingsPreparationTimer.Start();
#endif
    }

    private void CloseMonitor()
    {
        closing = true; latencyTimer.Stop(); restartTimer.Stop(); controlsTimer.Stop();
#if BETA
        settingsPreparationTimer.Stop(); settingsPreparationTimer.Dispose();
        preparedSettingsDialog?.Dispose(); preparedSettingsDialog = null;
        startupTaskbarPreview?.Dispose(); startupTaskbarPreview = null;
        // Remove all visible windows before shutting down native video windows.
        // mpv teardown can otherwise briefly expose unpainted surfaces.
        toolbar?.Hide();
        dragSurface?.Hide();
        motionIndicator?.Hide();
        foreach (var grip in resizeGrips) grip.Hide();
        Hide();
        CancelSeamlessRefresh();
        StopRecordingForClose();
        foreach (var buffer in preRollBuffers.Values) buffer.Dispose();
        preRollBuffers.Clear();
        foreach (var capture in motionProcesses.ToArray())
            try { if (!capture.HasExited) capture.Kill(true); } catch { }
        StopGridPlayers();
        windowDiagnostics?.Mark("closing");
        windowDiagnostics?.Dispose(); windowDiagnostics = null;
        videoDecorationTimer.Stop(); videoDecorationTimer.Dispose();
        manualRestoreTimer.Dispose(); motionRestoreTimer.Stop(); motionIndicatorTimer.Stop(); motionPauseTimer.Stop(); cameraLayoutTimer.Stop(); StopMotionIntegration(); cameraContextMenu?.Dispose(); motionIndicator?.Close();
#endif
#if V2_BETA
        reolink?.Dispose(); reolink = null;
        StopLocalMotion();
        homeAssistantStreams?.Dispose(); homeAssistantStreams = null;
#endif
        SaveWindow(); StopPlayer(); toolbar?.Close(); dragSurface?.Close(); foreach (var grip in resizeGrips) grip.Close();
    }

#if BETA
    private Rectangle CameraViewport
    {
        get
        {
#if V2_BETA
            // A first player may start before the minimized window has ever
            // been restored. Its viewport must use the eventual normal size.
            if (WindowState == FormWindowState.Minimized)
                return new Rectangle(Point.Empty, RestoreBounds.Size);
#endif
            return ClientRectangle;
        }
    }
#endif
    private void StartPlayer()
    {
#if BETA
        if (gridMode)
        {
            StartGridPlayers();
            return;
        }
#endif
#if BETA
        if (!closing && !HasUsableCamera())
        {
            video.Hide(); standbyVideo.Hide();
            offlinePlaceholder.SetEmpty(settings.ShowEmptyCameraLogo, settings.ShowEmptyFourthFieldBorder, -1);
            offlinePlaceholder.Show(); offlinePlaceholder.BringToFront();
            return;
        }
#endif
        if (closing || !HasUsableCamera() || player is { HasExited: false }) return;
        var camera = settings.Cameras[settings.SelectedCamera];
#if V2_BETA
        if (camera.Reolink.Enabled && reolink?.IsStreamReady(camera) != true)
        {
            video.Hide(); standbyVideo.Hide();
            offlinePlaceholder.Configure(camera.Name);
            offlinePlaceholder.SetConnecting();
            offlinePlaceholder.Show(); offlinePlaceholder.BringToFront();
            return;
        }
#endif
#if BETA
        // Keep the application title stable; the selected camera is in the toolbar.
#else
        #if V2_BETA
        Text = $"{ApplicationBranding.ProductName} – {camera.Name}";
#else
        Text = $"HomeCamMonitor for Homeassistant – {camera.Name}";
#endif
#endif
        intentionalStop = false;
#if BETA
        // Keep mpv's native placeholder behind an opaque black surface until
        // its first video frame is ready.
        standbyVideo.Bounds = CameraViewport;
        standbyVideo.BringToFront();
        video.Hide();
        standbyVideo.Refresh();
        offlinePlaceholder.Configure(camera.Name);
        offlinePlaceholder.Show();
        offlinePlaceholder.BringToFront();
#endif
#if NEXT_BETA
        // Let mpv render at the final host size while the opaque placeholder
        // still covers it. A hidden host delays native resize/presentation.
        video.Bounds = CameraViewport;
        video.Show();
        offlinePlaceholder.BringToFront();
#endif
        var start = new ProcessStartInfo(Path.Combine(AppContext.BaseDirectory, "mpv.exe")) { UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden };
        foreach (var argument in new[]
        {
            $"--wid={video.Handle.ToInt64()}", "--no-terminal", "--really-quiet", "--no-audio", "--no-osc",
            "--no-border", "--profile=low-latency", "--cache=no", "--demuxer-lavf-o=rtsp_transport=tcp",
            "--hwdec=auto-safe", "--vo=gpu-next", "--gpu-api=d3d11", "--scale=ewa_lanczossharp",
            "--cscale=ewa_lanczossharp", "--dscale=mitchell", "--interpolation=no", "--window-dragging=yes", "--keep-open=no",
            $"--input-ipc-server=\\\\.\\pipe\\{pipeName}", camera.StreamUrl
        }) start.ArgumentList.Add(argument);
#if V2_BETA
        ReolinkPlayback.Configure(start, camera.Reolink.Enabled);
#endif
        try
        {
            player = Process.Start(start) ?? throw new InvalidOperationException(UiText.T("mpv konnte nicht gestartet werden."));
            player.EnableRaisingEvents = true; player.Exited += PlayerExited;
#if BETA
            initialPlayerReveal?.Cancel();
            initialPlayerReveal?.Dispose();
            initialPlayerReveal = new CancellationTokenSource();
            var startedPlayer = player;
            var diagnosticCameraIndex = settings.SelectedCamera;
            streamDiagnostics.Record(diagnosticCameraIndex, "start");
            _ = RevealWhenReadyAsync(startedPlayer, pipeName, initialPlayerReveal.Token,
                () => ReferenceEquals(player, startedPlayer) && !gridMode,
                () => { streamDiagnostics.Record(diagnosticCameraIndex, "ready"); NativeMethods.PrepareVideoChildren(video.Handle); video.Show(); video.BringToFront(); offlinePlaceholder.SetConnecting(); offlinePlaceholder.Hide(); },
                () => { offlinePlaceholder.SetOffline(); RestartPlayer(); }, diagnosticCameraIndex);
            if (TopMost && !sentToBackground)
#endif
                NativeMethods.SetWindowPos(Handle, NativeMethods.HwndTopMost, 0, 0, 0, 0,
                    NativeMethods.SwpNoMove | NativeMethods.SwpNoSize | NativeMethods.SwpNoActivate);
        }
        catch (Exception exception)
        {
#if BETA
            streamDiagnostics.Record(settings.SelectedCamera, "start-failed");
            offlinePlaceholder.SetOffline();
            restartTimer.Start();
#else
            MessageBox.Show(this, $"Der Kamerastream konnte nicht gestartet werden.\n\n{exception.Message}", "HomeCam Monitor", MessageBoxButtons.OK, MessageBoxIcon.Warning);
#endif
        }
    }

    private void PlayerExited(object? sender, EventArgs eventArgs)
    {
        if (closing || intentionalStop) return;
        try { BeginInvoke(new Action(() =>
        {
            if (closing || !ReferenceEquals(sender, player)) return;
#if BETA
            streamDiagnostics.Record(settings.SelectedCamera, "exited");
            offlinePlaceholder.SetOffline();
            offlinePlaceholder.Show();
            offlinePlaceholder.BringToFront();
#endif
            restartTimer.Start();
        })); } catch { }
    }

    private void StopPlayer()
    {
#if BETA
        CancelSeamlessRefresh();
        initialPlayerReveal?.Cancel();
        initialPlayerReveal?.Dispose();
        initialPlayerReveal = null;
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
        // Cover and paint before stopping mpv: stopping it can block the UI
        // briefly and otherwise exposes its native window during teardown.
        offlinePlaceholder.Bounds = CameraViewport;
        if (HasUsableCamera()) offlinePlaceholder.Configure(settings.Cameras[settings.SelectedCamera].Name);
        offlinePlaceholder.Show();
        offlinePlaceholder.BringToFront();
        offlinePlaceholder.Refresh();
        video.Hide();
        standbyVideo.Hide();
        standbyVideo.BringToFront();
        standbyVideo.Refresh();
        if (offlinePlaceholder.Visible) offlinePlaceholder.BringToFront();
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
        target.Hide();
        target.CreateControl();
        var start = new ProcessStartInfo(Path.Combine(AppContext.BaseDirectory, "mpv.exe"))
        {
            UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden
        };
        var arguments = new List<string>
        {
            $"--wid={target.Handle.ToInt64()}", "--no-terminal", "--really-quiet", "--no-audio", "--no-osc",
            "--no-border", "--profile=low-latency", "--cache=no", "--demuxer-lavf-o=rtsp_transport=tcp",
            "--hwdec=auto-safe", "--vo=gpu-next", "--gpu-api=d3d11", "--scale=ewa_lanczossharp",
            "--cscale=ewa_lanczossharp", "--dscale=mitchell", "--interpolation=no"
        };
        if (grid) arguments.AddRange(["--keepaspect-window=no", "--panscan=1.0"]);
        else arguments.Add("--window-dragging=yes");
        arguments.Add("--keep-open=no");
        arguments.Add("--input-ipc-server=" + @"\\.\pipe\" + ipcName);
        arguments.Add(streamUrl);
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
#if V2_BETA
        ReolinkPlayback.Configure(start, settings.Cameras.Any(camera => camera.Reolink.Enabled && camera.StreamUrl == streamUrl));
#endif
        return Process.Start(start) ?? throw new InvalidOperationException(UiText.T("mpv konnte nicht gestartet werden."));
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
        nextSurface.Bounds = CameraViewport;
        var newPipeName = $"HomeCamMonitor-Refresh-{Environment.ProcessId}-{Guid.NewGuid():N}";
        Process? replacement = null;
        try
        {
            replacement = StartRefreshPlayer(nextSurface, settings.Cameras[cameraIndex].StreamUrl, newPipeName, false);
            if (!await WaitForFirstFrameAsync(replacement, newPipeName, cancellationToken, startupTimeoutSeconds: InitialStreamTimeout(cameraIndex)) ||
                replacement.HasExited || cancellationToken.IsCancellationRequested || closing || gridMode ||
                player != oldPlayer || settings.SelectedCamera != cameraIndex) return;

            NativeMethods.PrepareVideoChildren(nextSurface.Handle);
            nextSurface.Show();
            nextSurface.BringToFront();
            video = nextSurface;
            standbyVideo = oldSurface;
            pipeName = newPipeName;
            player = replacement;
            replacement.EnableRaisingEvents = true;
            replacement.Exited += PlayerExited;
            var watchedPlayer = replacement;
            var watchedCameraIndex = settings.SelectedCamera;
            _ = WatchPlaybackAsync(watchedPlayer, newPipeName, initialPlayerReveal?.Token ?? CancellationToken.None,
                () => ReferenceEquals(player, watchedPlayer) && !gridMode,
                () => { offlinePlaceholder.SetOffline(); offlinePlaceholder.Show(); offlinePlaceholder.BringToFront(); RestartPlayer(); }, watchedCameraIndex);
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
                if (!await WaitForFirstFrameAsync(replacement, newPipeName, cancellationToken, startupTimeoutSeconds: InitialStreamTimeout(cameraIndex)) ||
                    replacement.HasExited || cancellationToken.IsCancellationRequested || closing || !gridMode ||
                    slot.Player != oldPlayer || slot.CameraIndex != cameraIndex) continue;

                NativeMethods.PrepareVideoChildren(nextSurface.Handle);
                nextSurface.Show();
                nextSurface.BringToFront();
                slot.ActiveSurface = nextSurface;
                slot.SpareSurface = oldSurface;
                slot.Player = replacement;
                replacement.EnableRaisingEvents = true;
                replacement.Exited += GridPlayerExited;
                var watchedPlayer = replacement;
                _ = WatchPlaybackAsync(watchedPlayer, newPipeName, initialGridReveal?.Token ?? CancellationToken.None,
                    () => gridMode && ReferenceEquals(slot.Player, watchedPlayer),
                    () =>
                    {
                        slot.ActiveSurface.Hide(); slot.SpareSurface.Hide();
                        slot.Placeholder.SetOffline(); slot.Placeholder.Show(); slot.Placeholder.BringToFront();
                        DisposePlayer(slot.Player, GridPlayerExited); slot.Player = null; restartTimer.Start();
                    }, cameraIndex);
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

    private async Task RevealWhenReadyAsync(Process started, string ipcName,
        CancellationToken cancellationToken, Func<bool> stillCurrent, Action reveal, Action? unavailable = null, int cameraIndex = -1)
    {
        try
        {
            while (!closing && !started.HasExited && stillCurrent())
            {
                if (await WaitForFirstFrameAsync(started, ipcName, cancellationToken, startupTimeoutSeconds: InitialStreamTimeout(cameraIndex)))
                {
                    if (!closing && !cancellationToken.IsCancellationRequested && stillCurrent())
                    {
                        reveal();
                        await WatchPlaybackAsync(started, ipcName, cancellationToken, stillCurrent, unavailable, cameraIndex);
                    }
                    return;
                }
                if (!closing && !cancellationToken.IsCancellationRequested && stillCurrent())
                {
                    streamDiagnostics.Record(cameraIndex, "connect-failed");
                    unavailable?.Invoke();
                    if (unavailable is not null) return;
                }
                await Task.Delay(1000, cancellationToken);
            }
        }
        catch (OperationCanceledException) { }
        catch (InvalidOperationException) { }
    }

    private async Task WatchPlaybackAsync(Process process, string ipcName, CancellationToken token,
        Func<bool> stillCurrent, Action? unavailable, int cameraIndex = -1)
    {
        // A running process is not proof of a live stream: RTSP can stall indefinitely.
        try
        {
            if (!await WaitForFirstFrameAsync(process, ipcName, token, monitorContinuously: true) &&
                !closing && !token.IsCancellationRequested && stillCurrent())
            {
                streamDiagnostics.Record(cameraIndex, "stalled-or-disconnected");
                unavailable?.Invoke();
            }
        }
        catch (OperationCanceledException) { }
        catch (InvalidOperationException) { }
    }

    private int InitialStreamTimeout(int cameraIndex)
    {
#if V2_BETA
        if (cameraIndex >= 0 && cameraIndex < settings.Cameras.Count && settings.Cameras[cameraIndex].Reolink.Enabled) return 30;
#endif
        return 12;
    }

    private static async Task<bool> WaitForFirstFrameAsync(Process process, string ipcName,
        CancellationToken cancellationToken, bool monitorContinuously = false, int startupTimeoutSeconds = 12)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(startupTimeoutSeconds));
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
                command = new object[] { "observe_property", 1, "time-pos" }, request_id = 1
            }));
            var progress = new PlaybackProgress();
            while (!process.HasExited)
            {
                var line = await reader.ReadLineAsync(timeout.Token);
                if (line is null) return false;
                using var response = JsonDocument.Parse(line);
                var root = response.RootElement;
                if (root.TryGetProperty("event", out var eventName))
                {
                    var name = eventName.GetString();
                    if (name == "property-change" && root.TryGetProperty("name", out var property) &&
                        property.GetString() == "time-pos" && root.TryGetProperty("data", out var position) &&
                        position.ValueKind == JsonValueKind.Number && position.TryGetDouble(out var value))
                    {
                        if (progress.Observe(value))
                        {
                            if (!monitorContinuously) return true;
                            timeout.CancelAfter(TimeSpan.FromSeconds(12));
                        }
                    }
                    if (name is "end-file" or "shutdown") return false;
                }

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
        if (gridMode) { toolbar?.Flash(UiText.T("Kamera doppelt anklicken")); return; }
#endif
        var available = settings.Cameras.Select((camera, index) => (camera, index)).Where(c => c.camera.IsAvailable).Select(c => c.index).ToArray();
        if (available.Length < 2) return;
        var position = Array.IndexOf(available, settings.SelectedCamera);
        settings.SelectedCamera = available[(position + direction + available.Length) % available.Length];
        SettingsStore.Save(settings); UpdateToolbar(); RestartPlayer();
    }

    internal async Task SaveSnapshotAsync()
    {
#if BETA
        RegisterUserInteraction();
        if (gridMode) { toolbar?.Flash(UiText.T("Kamera doppelt anklicken")); return; }
#endif
        string? path = null;
        try
        {
#if BETA
            var folder = RecordingStorage.Resolve(settings.ManualSnapshotFolder, RecordingStorage.ManualSnapshots);
#else
            var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "HomeCam Monitor");
#endif
            Directory.CreateDirectory(folder);
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

            toolbar?.Flash(UiText.T("Gespeichert"));
        }
        catch (Exception exception)
        {
            suppressToolbar = false;
            if (toolbar is { Visible: false }) { toolbar.Show(this); PositionOverlays(); }
            MessageBox.Show(this, UiText.F("Der Snapshot konnte nicht gespeichert werden.\n\nZiel: {0}\n\n{1}", path, exception.Message), UiText.T("Snapshot fehlgeschlagen"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

#if BETA
    internal async Task ToggleRecordingAsync()
    {
        RegisterUserInteraction();
        if (gridMode) { toolbar?.Flash(UiText.T("Kamera doppelt anklicken")); return; }
        try
        {
            if (recording)
            {
                await StopRecordingAsync();
                return;
            }

            if (!HasUsableCamera()) return;
            var folder = RecordingStorage.Resolve(settings.ManualVideoFolder, RecordingStorage.ManualVideos);
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
            recordingPlayer = Process.Start(start) ?? throw new InvalidOperationException(UiText.T("Der Aufnahmeprozess konnte nicht gestartet werden."));
            recording = true;
            latencyTimer.Stop();
            RefreshRecordingIndicator();
            toolbar?.Flash(UiText.T("Aufnahme läuft"));
        }
        catch (Exception exception)
        {
            recording = false;
            RefreshRecordingIndicator();
            latencyTimer.Start();
            MessageBox.Show(this, UiText.F("Die Aufnahme konnte nicht gestartet oder beendet werden.\n\nZiel: {0}\n\n{1}", recordingPath, exception.Message), UiText.T("Aufnahme fehlgeschlagen"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
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
            toolbar?.Flash(UiText.T("Aufnahme gespeichert"));
            return;
        }

        if (!string.IsNullOrWhiteSpace(completedPath) && File.Exists(completedPath)) File.Delete(completedPath);
        MessageBox.Show(this, UiText.T("Die Kameraaufnahme enthielt keine Videodaten und wurde entfernt."), UiText.T("Aufnahme fehlgeschlagen"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
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

#if BETA
    private void PrepareSettingsWhenIdle()
    {
        if (closing || activeSettingsDialog is not null || nativeMoveOrResize ||
            motionRestoreTimer.Enabled || NativeMethods.GetIdleMilliseconds() < 1500) return;
        settingsPreparationTimer.Stop();
        preparedSettingsDialog?.Dispose(); preparedSettingsDialog = null;
        try
        {
            var dialog = new SettingsForm(settings);
            preparedSettingsDialog = dialog;
            dialog.PrepareForDisplay();
            preparedSettingsSource = settings;
            preparedSettingsFingerprint = SettingsForm.GetDisplayFingerprint(settings);
        }
        catch
        {
            preparedSettingsDialog?.Dispose(); preparedSettingsDialog = null;
            // Opening remains available through the normal construction path.
        }
    }

    private SettingsForm TakeSettingsDialog()
    {
        var dialog = preparedSettingsDialog;
        preparedSettingsDialog = null;
        if (dialog is not null && !dialog.IsDisposed && ReferenceEquals(preparedSettingsSource, settings) &&
            preparedSettingsFingerprint == SettingsForm.GetDisplayFingerprint(settings)) return dialog;
        dialog?.Dispose();
        var fresh = new SettingsForm(settings);
        fresh.PrepareForDisplay();
        return fresh;
    }
#endif

#if STORE_PACKAGE
    private bool storeSettingsOpening;
    internal async void OpenSettings()
    {
        if (storeSettingsOpening) return;
        storeSettingsOpening = true;
        try { settings.StartWithWindows = await PackagedStartup.IsEnabledAsync(); }
        catch (Exception error)
        {
            MessageBox.Show(this, UiText.T("Der Windows-Autostartstatus konnte nicht gelesen werden: ") + error.Message,
                "HomeCam Monitor", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        finally { storeSettingsOpening = false; }
#else
    internal void OpenSettings()
    {
#endif
#if BETA
        windowDiagnostics?.Mark("settings requested before dialog construction");
#endif
#if BETA
        RegisterUserInteraction();
#endif
        Settings? changedSettings = null;
        // Build and theme the dialog while the camera and toolbar remain visible.
#if BETA
        settingsPreparationTimer.Stop();
        using var dialog = TakeSettingsDialog();
#else
        using var dialog = new SettingsForm(settings);
#endif
#if BETA
        dialog.CanRestore = () => !recording && activeMotionCapture.Count == 0;
        dialog.BuildDiagnostics = () => DiagnosticExport.Serialize(settings, streamDiagnostics.Snapshot(), new
        {
            GridMode = gridMode, Fullscreen = fullscreen, WindowState = WindowState.ToString(),
            Recording = recording, MotionCaptures = activeMotionCapture.Count, ReconnectPending = restartTimer.Enabled,
            Players = gridMode ? gridSlots.Select(slot => new { CameraIndex = slot.CameraIndex, Running = IsPlayerRunning(slot.Player) }).ToArray()
                : new[] { new { CameraIndex = settings.SelectedCamera, Running = IsPlayerRunning(player) } }
        });
        windowDiagnostics?.Mark("settings constructed");
        dialog.Shown += (_, _) => windowDiagnostics?.Mark("settings shown");
#endif
#if BETA
        // Resolve CenterParent ourselves before the dialog has a visible
        // native window, then keep Windows from animating it out of the owner.
        dialog.StartPosition = FormStartPosition.Manual;
        var dialogArea = Screen.FromControl(this).WorkingArea;
        dialog.Location = new Point(
            Math.Clamp(Left + (Width - dialog.Width) / 2, dialogArea.Left,
                Math.Max(dialogArea.Left, dialogArea.Right - dialog.Width)),
            Math.Clamp(Top + (Height - dialog.Height) / 2, dialogArea.Top,
                Math.Max(dialogArea.Top, dialogArea.Bottom - dialog.Height)));
        var disableTransitions = 1;
        NativeMethods.DwmSetWindowAttribute(dialog.Handle, 3, ref disableTransitions, sizeof(int));
#endif
        suppressToolbar = true;
#if BETA
        activeSettingsDialog = dialog;
        HideMotionIndicator();
#endif
        toolbar?.Hide();
        dragSurface?.Hide();
        foreach (var resizeGrip in resizeGrips) resizeGrip.Hide();
        TopMost = false;

        try
        {
#if BETA
            activeSettingsDialog = dialog;
#endif
            if (dialog.ShowDialog(this) == DialogResult.OK) changedSettings = dialog.Result;
        }
        finally
        {
#if BETA
            activeSettingsDialog = null;
            settingsPreparationTimer.Start();
#endif
            suppressToolbar = false;
            TopMost = (changedSettings?.AlwaysOnTop ?? settings.AlwaysOnTop) || (manualRestoreTimer.Enabled && (changedSettings?.ManualRestoreKeepForeground ?? settings.ManualRestoreKeepForeground));
            PositionOverlays();
            lastCursorMovement = DateTime.UtcNow;
        }

        if (changedSettings is null) return;
#if BETA
        if (!dialog.RestoredFromFile && changedSettings.MotionDetectionEnabled != settings.MotionDetectionEnabled)
            changedSettings.MotionActionsPausedUntilUtc = null;
#endif
        var previousSettings = settings;
        ApplicationBranding.ApplyWindowIcon(this);
        offlinePlaceholder.SetLogoVariant(0);
        foreach (var slot in gridSlots) slot.Placeholder.SetLogoVariant(0);
        settings = changedSettings; settings.SelectedCamera = Math.Clamp(settings.SelectedCamera, 0, Math.Max(0, settings.Cameras.Count - 1));
        NormalizeCameraSelection();
#if BETA
        if (recording && previousSettings.SelectedCamera >= 0 && previousSettings.SelectedCamera < previousSettings.Cameras.Count &&
            !settings.Cameras.Any(c => c.Active && c.Name.Equals(previousSettings.Cameras[previousSettings.SelectedCamera].Name, StringComparison.OrdinalIgnoreCase)))
        { StopRecordingForClose(); RefreshRecordingIndicator(); }
        foreach (var pair in motionProcessCameras.ToArray())
            if (!settings.Cameras.Any(c => c.CanRecord && c.Name.Equals(pair.Value, StringComparison.OrdinalIgnoreCase)))
                try { if (!pair.Key.HasExited) pair.Key.Kill(true); } catch (InvalidOperationException) { }
        if (!dialog.RestoredFromFile) settings.LastGridMode = gridMode;
        if (settings.BetaWindowLoggingEnabled && windowDiagnostics is null)
            windowDiagnostics = new NativeWindowDiagnostics(Handle);
        else if (!settings.BetaWindowLoggingEnabled && windowDiagnostics is not null)
        {
            windowDiagnostics.Mark("logging disabled in settings");
            windowDiagnostics.Dispose(); windowDiagnostics = null;
        }
        var streamsChanged = previousSettings.SelectedCamera != settings.SelectedCamera ||
            previousSettings.Cameras.Count != settings.Cameras.Count ||
            previousSettings.Cameras.Where((camera, index) =>
                camera.Active != settings.Cameras[index].Active ||
                !string.Equals(camera.StreamUrl, settings.Cameras[index].StreamUrl, StringComparison.Ordinal))
                .Any();
        var sensorsChanged = previousSettings.Cameras.Count != settings.Cameras.Count ||
            previousSettings.Cameras.Where((camera, index) =>
                camera.Active != settings.Cameras[index].Active ||
#if V2_BETA
                camera.Reolink.Enabled != settings.Cameras[index].Reolink.Enabled ||
                camera.Direct.Enabled != settings.Cameras[index].Direct.Enabled ||
#endif
                camera.MotionEnabled != settings.Cameras[index].MotionEnabled ||
                camera.PersonEnabled != settings.Cameras[index].PersonEnabled ||
                !string.Equals(camera.MotionEntityId, settings.Cameras[index].MotionEntityId, StringComparison.Ordinal) ||
                !string.Equals(camera.PersonEntityId, settings.Cameras[index].PersonEntityId, StringComparison.Ordinal))
                .Any();
        var reconnectMotion = previousSettings.MotionDetectionEnabled != settings.MotionDetectionEnabled ||
            previousSettings.DirectHomeAssistantEnabled != settings.DirectHomeAssistantEnabled ||
            previousSettings.HomeAssistantUrl != settings.HomeAssistantUrl ||
            previousSettings.HomeAssistantToken != settings.HomeAssistantToken ||
            previousSettings.IgnoreHomeAssistantCertificateErrors != settings.IgnoreHomeAssistantCertificateErrors ||
            sensorsChanged;
        foreach (var slot in gridSlots)
        {
            if (slot.CameraIndex < 0)
            {
                slot.Placeholder.SetEmpty(settings.ShowEmptyCameraLogo, settings.ShowEmptyFourthFieldBorder, gridSlots.IndexOf(slot));
                continue;
            }
            if (slot.CameraIndex >= settings.Cameras.Count) continue;
            slot.Name.Text = settings.Cameras[slot.CameraIndex].Name;
            slot.Name.Visible = settings.ShowGridCameraNames;
        }
        UpdateGridMotionBorders();
#endif
#if STORE_PACKAGE
        if (previousSettings.StartWithWindows != settings.StartWithWindows)
        {
            try { settings.StartWithWindows = await PackagedStartup.SetEnabledAsync(settings.StartWithWindows); }
            catch (Exception error)
            {
                settings.StartWithWindows = previousSettings.StartWithWindows;
                try { settings.StartWithWindows = await PackagedStartup.IsEnabledAsync(); } catch { }
                MessageBox.Show(this, error.Message, UiText.T("HomeCam Monitor – Autostart"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
#endif
        SettingsStore.Save(settings);
#if !STORE_PACKAGE
        if (previousSettings.StartWithWindows != settings.StartWithWindows)
            ConfigureAutostart(settings.StartWithWindows);
#endif
        UpdateToolbar(); PositionOverlays();
#if BETA
#if V2_BETA
        streamsChanged |= ConfigureReolink();
        ConfigureHomeAssistantStreams();
        ConfigureLocalMotion();
#endif
        UpdatePreRollBuffers();
        if (streamsChanged) RestartPlayer();
        else if (!gridMode && !HasUsableCamera()) StartPlayer();
        ScheduleMotionPauseExpiry();
        if (reconnectMotion) RestartMotionIntegration();
        var restoredGrid = settings.LastGridMode;
        if (dialog.RestoredFromFile && dialog.ApplyWindowState)
        {
            var restoredBounds = RestoreWindowBounds(settings, Screen.AllScreens.Select(screen => (screen.DeviceName, screen.WorkingArea)).ToArray());
            if (fullscreen) ToggleFullscreen();
            WindowState = FormWindowState.Normal;
            Bounds = restoredBounds;
            SaveWindow();
        }
        if (dialog.RestoredFromFile && dialog.ApplyViewState && gridMode != restoredGrid)
        {
            if (gridMode) ExitGridView(); else ToggleGridView();
        }
#else
        RestartPlayer();
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
#if BETA
        UpdateEmptyFieldBorderVisibility();
#endif
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
            var placeholder = new CameraPlaceholderPanel { Dock = DockStyle.Fill, Visible = false };
            host.Controls.Add(placeholder);
            activeSurface.BringToFront();
            var name = new Label
            {
                AutoSize = true,
                Text = UiText.T("Kamera"),
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
                SpareSurface = spareSurface, Name = name, BorderParts = borders, Placeholder = placeholder };
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

    private void UpdateEmptyFieldBorderVisibility()
    {
        offlinePlaceholder.SuppressOuterBorder = fullscreen;
        foreach (var slot in gridSlots) slot.Placeholder.SuppressOuterBorder = fullscreen;
    }

    private void LayoutCameraGrid()
    {
        UpdateEmptyFieldBorderVisibility();
        var bounds = GetCameraGridBounds(cameraGrid.ClientSize);
        for (var index = 0; index < gridSlots.Count; index++)
            gridSlots[index].Host.Bounds = bounds[index];
    }

    private void AlignCameraSurfaces()
    {
        if (closing || IsDisposed) return;
#if V2_BETA
        if (WindowState == FormWindowState.Minimized) return;
#endif
        var visibleArea = ClientRectangle;
        if (video.Bounds != visibleArea) video.Bounds = visibleArea;
        if (standbyVideo.Bounds != visibleArea) standbyVideo.Bounds = visibleArea;
        if (offlinePlaceholder.Bounds != visibleArea) offlinePlaceholder.Bounds = visibleArea;
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
            toolbar?.Flash(UiText.T("Mindestens 2 Kameras"));
            return;
        }

        if (recording)
        {
            toolbar?.Flash(UiText.T("Aufnahme zuerst beenden"));
            return;
        }

        StopPlayer();
        offlinePlaceholder.Hide();
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
        // Changing the camera layout must preserve fullscreen and its saved restore bounds.
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
        initialGridReveal ??= new CancellationTokenSource();
        var cameras = settings.Cameras
            .Select((camera, index) => (Camera: camera, Index: index))
            .Where(item => item.Camera.IsAvailable)
            .Take(4)
            .ToList();

        intentionalGridStop = false;
        for (var index = 0; index < gridSlots.Count; index++)
        {
            var slot = gridSlots[index];
            if (index < cameras.Count && slot.CameraIndex == cameras[index].Index &&
                slot.Player is { HasExited: false }) continue;
            DisposePlayer(slot.Player, GridPlayerExited);
            slot.Player = null;
            slot.CameraIndex = -1;
            slot.Name.Text = "";
            slot.Name.Visible = false;
            if (index >= cameras.Count)
            {
                slot.ActiveSurface.Hide(); slot.SpareSurface.Hide();
                slot.Placeholder.SetEmpty(settings.ShowEmptyCameraLogo, settings.ShowEmptyFourthFieldBorder, gridSlots.IndexOf(slot));
                slot.Placeholder.Show(); slot.Placeholder.BringToFront();
                continue;
            }

            var camera = cameras[index];
            slot.CameraIndex = camera.Index;
            slot.Placeholder.Configure(camera.Camera.Name);
            slot.Placeholder.Show();
            slot.Placeholder.BringToFront();
            slot.Name.Text = camera.Camera.Name;
            slot.Name.Visible = settings.ShowGridCameraNames;
            slot.SpareSurface.Bounds = slot.Host.ClientRectangle;
            slot.SpareSurface.BringToFront();
            slot.Placeholder.BringToFront();
            slot.ActiveSurface.Hide();
            slot.SpareSurface.Hide();
            slot.Placeholder.Refresh();
#if V2_BETA
            if (camera.Camera.Reolink.Enabled && reolink?.IsStreamReady(camera.Camera) != true)
            {
                slot.Placeholder.SetConnecting();
                continue;
            }
#endif
            slot.ActiveSurface.CreateControl();
#if NEXT_BETA
            slot.ActiveSurface.Bounds = slot.Host.ClientRectangle;
            slot.ActiveSurface.Show();
            slot.Placeholder.BringToFront();
#endif
            var gridPipeName = $"HomeCamMonitor-Grid-{Environment.ProcessId}-{Guid.NewGuid():N}";
            var start = new ProcessStartInfo(Path.Combine(AppContext.BaseDirectory, "mpv.exe"))
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            };
            foreach (var argument in new[]
            {
                $"--wid={slot.ActiveSurface.Handle.ToInt64()}", "--no-terminal", "--really-quiet", "--no-audio", "--no-osc",
                "--no-border", "--profile=low-latency", "--cache=no", "--demuxer-lavf-o=rtsp_transport=tcp",
                "--hwdec=auto-safe", "--vo=gpu-next", "--gpu-api=d3d11", "--scale=ewa_lanczossharp",
                "--cscale=ewa_lanczossharp", "--dscale=mitchell", "--interpolation=no",
                "--keepaspect-window=no", "--panscan=1.0", "--keep-open=no",
                "--input-ipc-server=" + @"\\.\pipe\" + gridPipeName, camera.Camera.StreamUrl
            }) start.ArgumentList.Add(argument);
#if V2_BETA
            ReolinkPlayback.Configure(start, camera.Camera.Reolink.Enabled);
#endif

            try
            {
                slot.Player = Process.Start(start) ?? throw new InvalidOperationException(UiText.T("mpv konnte nicht gestartet werden."));
                slot.Player.EnableRaisingEvents = true;
                slot.Player.Exited += GridPlayerExited;
                var startedPlayer = slot.Player;
                var currentIndex = camera.Index;
                streamDiagnostics.Record(currentIndex, "start");
                _ = RevealWhenReadyAsync(startedPlayer, gridPipeName, initialGridReveal.Token,
                    () => gridMode && ReferenceEquals(slot.Player, startedPlayer) && slot.CameraIndex == currentIndex,
                    () =>
                    {
                        streamDiagnostics.Record(currentIndex, "ready");
                        slot.Placeholder.SetConnecting();
                        NativeMethods.PrepareVideoChildren(slot.ActiveSurface.Handle);
                        slot.ActiveSurface.Show();
                        slot.Placeholder.Hide();
                        slot.ActiveSurface.BringToFront();
                        if (slot.Name.Visible) slot.Name.BringToFront();
                        UpdateGridMotionBorders();
                    },
                    () =>
                    {
                        slot.ActiveSurface.Hide();
                        slot.SpareSurface.Hide();
                        slot.Placeholder.SetOffline();
                        slot.Placeholder.Show();
                        slot.Placeholder.BringToFront();
                        DisposePlayer(slot.Player, GridPlayerExited);
                        slot.Player = null;
                        restartTimer.Start();
                    }, currentIndex);
                if (slot.Name.Visible) slot.Name.BringToFront();
            }
            catch
            {
                streamDiagnostics.Record(camera.Index, "start-failed");
                slot.Player?.Dispose();
                slot.Player = null;
                slot.Placeholder.SetOffline();
                restartTimer.Start();
                slot.Name.Text = camera.Camera.Name + UiText.T(" – Stream nicht verfügbar");
            }
        }
    }

    private void StopGridPlayers()
    {
        CancelSeamlessRefresh();
        initialGridReveal?.Cancel();
        initialGridReveal?.Dispose();
        initialGridReveal = null;
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
        try { BeginInvoke(new Action(() =>
        {
            if (closing || !gridMode) return;
            var slot = gridSlots.FirstOrDefault(item => ReferenceEquals(item.Player, sender));
            if (slot is null) return;
            streamDiagnostics.Record(slot.CameraIndex, "exited");
            slot.ActiveSurface.Hide();
            slot.SpareSurface.Hide();
            slot.Placeholder.SetOffline();
            slot.Placeholder.Show();
            slot.Placeholder.BringToFront();
            restartTimer.Start();
        })); } catch { }
    }
#endif

#if BETA
    private ContextMenuStrip CreateCameraContextMenu()
    {
        var menu = new HomeCamRoundedContextMenuStrip
        {
            BackColor = Color.FromArgb(28, 28, 31),
            ForeColor = Color.White,
            Renderer = new HomeCamDarkMenuRenderer(),
            ShowImageMargin = true,
            Padding = new Padding(4)
        };
        menu.Opening += (_, _) =>
        {
            RegisterUserInteraction();
            PopulateCameraContextMenu(menu);
        };
        ConfigureContextMenuCorners(menu);
        return menu;
    }

    private static void ConfigureContextMenuCorners(ToolStripDropDown popup)
    {
        popup.Opened += (_, _) => ApplyContextMenuCorners(popup);
    }

    private static void ApplyContextMenuCorners(ToolStripDropDown popup)
    {
        // A popup can be recreated after its items change. Apply the shape at
        // its final size each time it opens, including for child menus.
        var oldRegion = popup.Region;
        var diameter = Math.Max(12, popup.DeviceDpi * 12 / 96);
        var shape = NativeMethods.CreateRoundRectRgn(0, 0, popup.Width + 1, popup.Height + 1,
            diameter, diameter);
        popup.Region = Region.FromHrgn(shape);
        oldRegion?.Dispose();
        NativeMethods.DeleteObject(shape);
    }

    private static bool IsPlayerRunning(Process? process)
    {
        try { return process is not null && !process.HasExited; } catch { return false; }
    }

    private void ReconnectCurrentStream(int? gridSlotIndex)
    {
        if (closing) return;
        if (!gridMode) { if (HasUsableCamera()) { streamDiagnostics.Record(settings.SelectedCamera, "manual-reconnect"); RestartPlayer(); } return; }
        if (!gridSlotIndex.HasValue || gridSlotIndex.Value < 0 || gridSlotIndex.Value >= gridSlots.Count) return;
        var slot = gridSlots[gridSlotIndex.Value];
        if (slot.CameraIndex < 0) return;
        streamDiagnostics.Record(slot.CameraIndex, "manual-reconnect");
        CancelSeamlessRefresh();
        var oldPlayer = slot.Player;
        slot.Player = null;
        slot.ActiveSurface.Hide(); slot.SpareSurface.Hide();
        slot.Placeholder.SetConnecting(); slot.Placeholder.Show(); slot.Placeholder.BringToFront();
        slot.Placeholder.Refresh();
        DisposePlayer(oldPlayer, GridPlayerExited);
        StartGridPlayers();
    }

    private void PopulateCameraContextMenu(ContextMenuStrip menu)
    {
        menu.Items.Clear();
        int? targetSlot = null;
        if (gridMode)
        {
            var pointer = Cursor.Position;
            for (var index = 0; index < gridSlots.Count; index++)
                if (gridSlots[index].Host.RectangleToScreen(gridSlots[index].Host.ClientRectangle).Contains(pointer))
                    targetSlot = index;
        }
        var reconnect = new ToolStripMenuItem(UiText.T("Aktuellen Stream neu verbinden"))
        {
            Enabled = gridMode ? targetSlot.HasValue && gridSlots[targetSlot.Value].CameraIndex >= 0 : HasUsableCamera()
        };
        reconnect.Click += (_, _) => ReconnectCurrentStream(targetSlot);
        menu.Items.Add(reconnect);
        menu.Items.Add(new ToolStripSeparator());
        var alwaysOnTop = new ToolStripMenuItem(UiText.T("Immer im Vordergrund"))
        {
            Checked = settings.AlwaysOnTop,
            CheckOnClick = true
        };
        alwaysOnTop.Click += (_, _) => SetAlwaysOnTop(alwaysOnTop.Checked);
        menu.Items.Add(alwaysOnTop);

#if !V2_BETA
        var motionEnabled = new ToolStripMenuItem("Bewegungserkennung aktiv")
        {
            Checked = settings.MotionDetectionEnabled,
            CheckOnClick = true
        };
        motionEnabled.Click += (_, _) => SetMotionDetectionEnabled(motionEnabled.Checked);
        menu.Items.Add(motionEnabled);
#endif

        var paused = MotionActionsArePaused();
        var pause = new ToolStripMenuItem(paused
            ? UiText.F("Bewegungsaktionen pausiert bis {0:HH:mm}", settings.MotionActionsPausedUntilUtc!.Value.ToLocalTime())
            : UiText.T("Bewegungsaktionen pausieren"))
        {
            DropDown = new HomeCamRoundedDropDownMenu()
        };
        foreach (var (label, minutes) in new[] { (UiText.T("15 Minuten"), 15), (UiText.T("30 Minuten"), 30), (UiText.T("1 Stunde"), 60) })
        {
            var item = new ToolStripMenuItem(label) { Enabled = settings.MotionDetectionEnabled };
            item.Click += (_, _) => PauseMotionActions(TimeSpan.FromMinutes(minutes));
            pause.DropDownItems.Add(item);
        }
#if !V2_BETA
        var untilManual = new ToolStripMenuItem("Bis manuell aktiviert")
        {
            Checked = !settings.MotionDetectionEnabled,
            Enabled = settings.MotionDetectionEnabled
        };
        untilManual.Click += (_, _) => SetMotionDetectionEnabled(false);
        pause.DropDownItems.Add(untilManual);
#endif
        pause.DropDownItems.Add(new ToolStripSeparator());
        var endPause = new ToolStripMenuItem(UiText.T("Pause beenden"))
        {
            Enabled = paused
#if !V2_BETA
                || !settings.MotionDetectionEnabled
#endif
        };
        endPause.Click += (_, _) =>
        {
#if V2_BETA
            ClearMotionPause();
#else
            if (!settings.MotionDetectionEnabled) SetMotionDetectionEnabled(true);
            else ClearMotionPause();
#endif
        };
        pause.DropDownItems.Add(endPause);
        ConfigureContextMenuCorners(pause.DropDown);
        menu.Items.Add(pause);
        menu.Items.Add(new ToolStripSeparator());

        var duration = new ToolStripMenuItem(UiText.F("Vordergrunddauer: {0} Sekunden", settings.MotionForegroundSeconds))
        {
            Enabled = settings.MotionDetectionEnabled && !settings.AlwaysOnTop,
            DropDown = new HomeCamRoundedDropDownMenu()
        };
        var values = new[] { 3, 5, 10, 15, 30, 60 };
        foreach (var seconds in values.Append(settings.MotionForegroundSeconds).Distinct().OrderBy(value => value))
        {
            var item = new ToolStripMenuItem(UiText.F("{0} Sekunden", seconds))
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
        ConfigureContextMenuCorners(duration.DropDown);
        menu.Items.Add(duration);
    }

    internal void RegisterUserInteraction()
    {
        if (manualRestoreTimer.Enabled) ArmManualRestoreTimer();
        if (!motionRestoreTimer.Enabled) return;
        motionRestoreTimer.Stop();
        cameraBeforeMotion = null;
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

#if !V2_BETA
    private void SetMotionDetectionEnabled(bool enabled)
    {
        settings.MotionDetectionEnabled = enabled;
        settings.MotionActionsPausedUntilUtc = null;
        motionPauseTimer.Stop();
        motionRestoreTimer.Stop();
        cameraBeforeMotion = null;
        SettingsStore.Save(settings);
        RestartMotionIntegration();
        UpdatePreRollBuffers();
    }
#endif

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
        toolbar?.Hide(); dragSurface?.Hide();
        HideMotionIndicator();
        foreach (var resizeGrip in resizeGrips) resizeGrip.Hide();
        TopMost = false;
        NativeMethods.SetWindowPos(Handle, NativeMethods.HwndBottom, 0, 0, 0, 0,
            NativeMethods.SwpNoMove | NativeMethods.SwpNoSize | NativeMethods.SwpNoActivate);
    }

    internal void MinimizeWindow()
    {
        RegisterUserInteraction();
        sentToBackground = false;
        toolbar?.Hide();
        dragSurface?.Hide();
        HideMotionIndicator();
        foreach (var resizeGrip in resizeGrips) resizeGrip.Hide();
        TopMost = false;
        wasMinimized = true;
        WindowState = FormWindowState.Minimized;
    }

    private async Task<Bitmap?> CaptureStartupPreviewAsync(CancellationToken token)
    {
        if (closing || WindowState != FormWindowState.Minimized || player is null || offlinePlaceholder.Visible) return null;
        var currentPlayer = player;
        var currentPipe = pipeName;
        var cameraIndex = settings.SelectedCamera;
        var frame = await TaskbarPreview.CaptureFrameAsync(currentPipe, token);
        if (closing || token.IsCancellationRequested || !ReferenceEquals(player, currentPlayer) ||
            pipeName != currentPipe || settings.SelectedCamera != cameraIndex || offlinePlaceholder.Visible)
        { frame?.Dispose(); return null; }
        return frame;
    }

    private void RestoreWindowAfterMinimize()
    {
        if (closing || WindowState != FormWindowState.Normal) return;
        sentToBackground = false;
        nativeMoveOrResize = false;
        TopMost = settings.AlwaysOnTop || motionRestoreTimer.Enabled || ManualForegroundActive;
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
        sentToBackground = false; TopMost = settings.AlwaysOnTop || ManualForegroundActive;
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
        if (gridMode) { toolbar.CameraName = UiText.T("4 Kameras"); return; }
#endif
        toolbar.CameraName = HasUsableCamera() ? settings.Cameras[settings.SelectedCamera].Name : UiText.T("Keine aktive Kamera");
    }
    private void UpdateToolbarVisibility()
    {
#if BETA
        if (toolbar is null || toolbar.IsDisposed || suppressToolbar || sentToBackground) return;
        if (WindowState == FormWindowState.Minimized) { toolbar.Hide(); return; }
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
            var grip = new ResizeGripForm(this, definition.Hit, definition.Cursor) { Owner = this };
            resizeGrips.Add(grip);
        }
    }

    private void PositionOverlays()
    {
        if (toolbar is null || toolbar.IsDisposed) return;
#if BETA
        if (activeSettingsDialog is not null || WindowState == FormWindowState.Minimized)
        {
            toolbar.Hide();
            dragSurface?.Hide();
            motionIndicator?.Hide();
            foreach (var resizeGrip in resizeGrips) resizeGrip.Hide();
            return;
        }
#endif
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
    private bool HasUsableCamera() => settings.SelectedCamera >= 0 && settings.SelectedCamera < settings.Cameras.Count && settings.Cameras[settings.SelectedCamera].IsAvailable;
    private void NormalizeCameraSelection()
    {
        if (HasUsableCamera()) return;
        var available = settings.Cameras.FindIndex(camera => camera.IsAvailable);
        if (available >= 0) settings.SelectedCamera = available;
    }
    private static void ConfigureAutostart(bool enabled)
    {
        using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true);
#if BETA
        const string name = ApplicationChannel.StorageName;
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
#if V2_BETA
        // UID and HA events coexist; V2 does not bind the legacy HTTP listener.
        if (!settings.DirectHomeAssistantEnabled) return;
#endif
        if (settings.DirectHomeAssistantEnabled)
        {
            if (!string.IsNullOrWhiteSpace(settings.HomeAssistantUrl) &&
                !string.IsNullOrWhiteSpace(settings.HomeAssistantToken) &&
                settings.Cameras.Any(camera => camera.ReceivesHomeAssistantEvents && (
                    (camera.UsesExternalMotion && !string.IsNullOrWhiteSpace(camera.MotionEntityId)) ||
                    ((camera.PersonEnabled ?? !string.IsNullOrWhiteSpace(camera.PersonEntityId)) &&
                     !string.IsNullOrWhiteSpace(camera.PersonEntityId)))))
                _ = Task.Run(() => ListenToHomeAssistantAsync(motionCancellation.Token));
            return;
        }
#if !V2_BETA
        StartMotionListener();
#endif
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
            MessageBox.Show(this, UiText.F("Die Bewegungserkennung konnte Port 8765 nicht öffnen.\n\n{0}", exception.Message), "HomeCam Monitor Beta", MessageBoxButtons.OK, MessageBoxIcon.Warning);
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
                throw new InvalidDataException(UiText.T("Home Assistant erwartet keine Anmeldung."));
        }

        await SendHomeAssistantMessageAsync(socket, new { type = "auth", access_token = settings.HomeAssistantToken }, cancellationToken);
        using (var authResult = await ReceiveHomeAssistantMessageAsync(socket, cancellationToken))
        {
            if (!authResult.RootElement.TryGetProperty("type", out var type) || type.GetString() != "auth_ok")
                throw new UnauthorizedAccessException(UiText.T("Home-Assistant-Anmeldung fehlgeschlagen."));
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

    internal static async Task SendHomeAssistantMessageAsync(WebSocket socket, object message, CancellationToken cancellationToken)
    {
        var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(message));
        await socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, cancellationToken);
    }

    internal static async Task<JsonDocument> ReceiveHomeAssistantMessageAsync(WebSocket socket, CancellationToken cancellationToken)
    {
        using var content = new MemoryStream();
        var buffer = new byte[4096];
        WebSocketReceiveResult result;
        do
        {
            result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);
            if (result.MessageType == WebSocketMessageType.Close) throw new IOException(UiText.T("Home Assistant hat die Verbindung beendet."));
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
                    return UiText.T("Unerwartete Antwort von Home Assistant.");
            }

            await SendHomeAssistantMessageAsync(socket, new { type = "auth", access_token = token }, timeout.Token);
            using (var authResult = await ReceiveHomeAssistantMessageAsync(socket, timeout.Token))
            {
                if (!authResult.RootElement.TryGetProperty("type", out var type) || type.GetString() != "auth_ok")
                    return UiText.T("Anmeldung fehlgeschlagen – bitte Langzeit-Token prüfen.");
            }

            await SendHomeAssistantMessageAsync(socket, new { id = 1, type = "get_states" }, timeout.Token);
            using var states = await ReceiveHomeAssistantMessageAsync(socket, timeout.Token);
            if (!states.RootElement.TryGetProperty("success", out var success) || !success.GetBoolean())
                return UiText.T("Home Assistant konnte die Entitäten nicht liefern.");
            if (!states.RootElement.TryGetProperty("result", out var result) || result.ValueKind != JsonValueKind.Array)
                return UiText.T("Home Assistant hat keine Entitäten geliefert.");
            foreach (var expected in new[] { entityId.Trim(), personEntityId.Trim() }.Where(id => id.Length > 0))
                if (!result.EnumerateArray().Any(state => state.TryGetProperty("entity_id", out var id) &&
                    string.Equals(id.GetString(), expected, StringComparison.OrdinalIgnoreCase)))
                    return UiText.F("Entität nicht gefunden: {0}", expected);
            return null;
        }
        catch (OperationCanceledException) { return UiText.T("Zeitüberschreitung – HA-Adresse oder Netzwerk prüfen."); }
        catch (Exception exception)
        {
            var detail = exception.InnerException?.Message ?? exception.Message;
            return detail.Contains("certificate", StringComparison.OrdinalIgnoreCase) || detail.Contains(UiText.T("Zertifikat"), StringComparison.OrdinalIgnoreCase)
                ? UiText.T("Zertifikatsfehler – gültigen HA-Namen verwenden oder die lokale Zertifikatsausnahme aktivieren.")
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

        var matchingCamera = settings.Cameras.FirstOrDefault(camera => camera.ReceivesHomeAssistantEvents && camera.UsesExternalMotion &&
            !string.IsNullOrWhiteSpace(camera.MotionEntityId) &&
            string.Equals(camera.MotionEntityId.Trim(), entity.GetString(), StringComparison.OrdinalIgnoreCase));
        if (matchingCamera is not null) { cameraName = matchingCamera.Name; return true; }
        matchingCamera = settings.Cameras.FirstOrDefault(camera => camera.SupportsPersonDetection &&
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
                var matchingCamera = settings.Cameras.FirstOrDefault(item => item.ReceivesHomeAssistantEvents && item.UsesExternalMotion && string.Equals(camera, item.Name, StringComparison.OrdinalIgnoreCase));
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

    private void HandleMotion(string cameraName, bool local = false)
    {
#if V2_BETA
        var sourceCamera = settings.Cameras.FirstOrDefault(c => c.Name.Equals(cameraName, StringComparison.OrdinalIgnoreCase));
        if (sourceCamera is null || sourceCamera.LocalMotion.Enabled != local) return;
#endif
        if (!settings.MotionDetectionEnabled) return;
        if (!settings.Cameras.Any(camera => camera.Active && camera.MotionEnabled && string.Equals(camera.Name, cameraName, StringComparison.OrdinalIgnoreCase))) return;
        ShowMotionIndicator(cameraIndex: settings.Cameras.FindIndex(camera =>
            string.Equals(camera.Name, cameraName, StringComparison.OrdinalIgnoreCase)));
        if (MotionActionsArePaused()) return;
        var motionCamera = settings.Cameras.FirstOrDefault(camera =>
            camera.MotionEnabled && string.Equals(camera.Name, cameraName, StringComparison.OrdinalIgnoreCase));
        if (motionCamera is not null) StartMotionCapture(motionCamera);
        if (activeSettingsDialog is not null) return;
        if (settings.AlwaysOnTop) return;
        var cameraIndex = settings.Cameras.FindIndex(camera => string.Equals(camera.Name, cameraName, StringComparison.OrdinalIgnoreCase));
        if (cameraIndex < 0) { toolbar?.Flash(UiText.F("{0} fehlt", cameraName)); return; }
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
        var camera = settings.Cameras.FirstOrDefault(item => item.SupportsPersonDetection &&
            (item.PersonEnabled ?? !string.IsNullOrWhiteSpace(item.PersonEntityId)) &&
            string.Equals(item.Name, cameraName, StringComparison.OrdinalIgnoreCase));
        if (camera is null) return;
        ShowMotionIndicator(personDetected: true);
        if (MotionActionsArePaused()) return;
        StartMotionCapture(camera, personDetected: true);
    }

    private void UpdatePreRollBuffers()
    {
        foreach (var buffer in preRollBuffers.Values) buffer.Dispose();
        preRollBuffers.Clear();
        if (!settings.MotionDetectionEnabled || (settings.SnapshotPreRollSeconds == 0 && settings.VideoPreRollSeconds == 0)) return;
        foreach (var camera in settings.Cameras.Where(c =>
            c.IsAvailable && c.CanRecord &&
            ((settings.SnapshotPreRollSeconds > 0 && (c.SupportsPersonDetection && settings.DirectHomeAssistantEnabled && c.PersonEnabled == true || c.MotionEnabled && c.MotionAction is "Snapshot" or "Both")) ||
             (settings.VideoPreRollSeconds > 0 && c.MotionEnabled && c.MotionAction is "Video" or "Both"))))
            preRollBuffers[camera] = new MotionPreRoll(camera.StreamUrl);
    }

    private async void StartMotionCapture(CameraEntry camera, bool personDetected = false)
    {
        if (!camera.CanRecord) return;
        if (!personDetected && camera.MotionAction is not ("Snapshot" or "Video" or "Both")) return;
        var captureKey = camera.Name + (personDetected ? "|Person" : "|Motion");
        if (activeMotionCapture.Contains(captureKey)) return;
        // The HTTP endpoint can be called more than once for the same event.
        if (lastMotionCapture.TryGetValue(captureKey, out var last) &&
            DateTime.UtcNow - last < TimeSpan.FromSeconds(2)) return;
        lastMotionCapture[captureKey] = DateTime.UtcNow;
        activeMotionCapture.Add(captureKey);
        var triggeredUtc = DateTime.UtcNow;
        preRollBuffers.TryGetValue(camera, out var preRoll);
        var snapshotBefore = settings.SnapshotPreRollSeconds;
        var videoBefore = settings.VideoPreRollSeconds;
        try
        {
            var snapshotFolder = RecordingStorage.ResolveMotionSnapshots(settings.MotionSnapshotFolder);
            var videoFolder = RecordingStorage.Resolve(settings.MotionVideoFolder, RecordingStorage.MotionDefault);
            CleanupMotionStorage();
            var safeName = string.Concat(camera.Name.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
            var baseName = $"{safeName}_{(personDetected ? "Person_" : "")}{DateTime.Now:yyyy-MM-dd_HH-mm-ss-fff}_{Guid.NewGuid():N}";
            var captureSnapshot = (personDetected || camera.MotionAction is "Snapshot" or "Both") ? CaptureSnapshotAsync() : Task.CompletedTask;
            var recordVideo = (!personDetected && camera.MotionAction is "Video" or "Both") ? RecordVideoAsync() : Task.CompletedTask;
            await Task.WhenAll(captureSnapshot, recordVideo);

            async Task CaptureSnapshotAsync()
            {
                if (!settings.Cameras.Any(c => c.CanRecord && c.Name.Equals(camera.Name, StringComparison.OrdinalIgnoreCase))) return;
                Directory.CreateDirectory(snapshotFolder);
                var path = Path.Combine(snapshotFolder, baseName + ".png");
                if (preRoll is not null && snapshotBefore > 0 &&
                    await preRoll.CaptureAsync(triggeredUtc, snapshotBefore, 0, path, snapshot: true)) { RecordingStorage.Track(path); return; }
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
                using var process = Process.Start(start) ?? throw new InvalidOperationException(UiText.T("FFmpeg konnte nicht gestartet werden."));
                motionProcesses.Add(process); motionProcessCameras[process] = camera.Name;
                try
                {
                    var errors = process.StandardError.ReadToEndAsync();
                    using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(25));
                    try { await process.WaitForExitAsync(timeout.Token); }
                    catch (OperationCanceledException)
                    {
                        if (!process.HasExited) process.Kill(true);
                        throw new IOException(UiText.T("Der Snapshot hat das Zeitlimit überschritten."));
                    }
                    if (process.ExitCode != 0 || !File.Exists(path) || new FileInfo(path).Length < 64)
                        throw new IOException(UiText.T("Kein Bild vom Stream: ") + (await errors).Trim());
                    using var image = Image.FromFile(path);
                    if (image.Width < 1 || image.Height < 1) throw new IOException(UiText.T("Das Einzelbild ist leer."));
                    RecordingStorage.Track(path);
                }
                catch { if (File.Exists(path)) File.Delete(path); throw; }
                finally { motionProcesses.Remove(process); motionProcessCameras.Remove(process); }
            }
            async Task RecordVideoAsync()
            {
                if (!settings.Cameras.Any(c => c.CanRecord && c.Name.Equals(camera.Name, StringComparison.OrdinalIgnoreCase))) return;
                Directory.CreateDirectory(videoFolder);
                var path = Path.Combine(videoFolder, baseName + ".mkv");
                if (preRoll is not null && videoBefore > 0)
                {
                    activeMotionRecordings++; RefreshRecordingIndicator();
                    try
                    {
                        if (await preRoll.CaptureAsync(triggeredUtc, videoBefore,
                            camera.MotionVideoSeconds is 15 or 30 or 60 ? camera.MotionVideoSeconds : 30, path, snapshot: false)) { RecordingStorage.Track(path); return; }
                    }
                    finally { activeMotionRecordings--; if (!closing) RefreshRecordingIndicator(); }
                }
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
                using var process = Process.Start(start) ?? throw new InvalidOperationException(UiText.T("FFmpeg konnte nicht gestartet werden."));
                motionProcesses.Add(process); motionProcessCameras[process] = camera.Name;
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
                        throw new IOException(UiText.T("Die Videoaufnahme hat das Zeitlimit überschritten."));
                    }
                    if (process.ExitCode != 0 || !File.Exists(path) || new FileInfo(path).Length < 4096)
                        throw new IOException(UiText.T("Kein gültiges Video: ") + (await errors).Trim());
                    RecordingStorage.Track(path);
                }
                catch { if (File.Exists(path)) File.Delete(path); throw; }
                finally
                {
                    motionProcesses.Remove(process); motionProcessCameras.Remove(process);
                    activeMotionRecordings--;
                    if (!closing) RefreshRecordingIndicator();
                }
            }
        }
        catch (Exception exception)
        {
            if (!closing && settings.Cameras.Any(c => c.CanRecord && c.Name.Equals(camera.Name, StringComparison.OrdinalIgnoreCase)))
                toolbar?.Flash(UiText.F("Bewegung: {0}", exception.Message));
        }
        finally { activeMotionCapture.Remove(captureKey); }
    }

    private void CleanupMotionStorage()
    {
        // Legacy cleanup only recognizes our uniquely named automatic captures.
        DeleteExpiredMotionFiles(RecordingStorage.MotionDefault, settings.MotionRetentionDays);
        RecordingStorage.Cleanup(settings.MotionRetentionDays);
    }

    internal static void DeleteExpiredMotionFiles(string folder, int days)
    {
        if (days <= 0 || !Directory.Exists(folder)) return; // 0 means unlimited.
        var cutoff = DateTime.UtcNow.AddDays(-days);
        foreach (var file in Directory.EnumerateFiles(folder))
        {
            if (Path.GetExtension(file) is not (".png" or ".mkv")) continue;
            if (!RecordingStorage.IsMotionFile(file)) continue;
            try { if (File.GetLastWriteTimeUtc(file) < cutoff) File.Delete(file); }
            catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
    }

    private void ShowMotionIndicator(bool personDetected = false, int cameraIndex = -1)
    {
        if (activeSettingsDialog is not null) { HideMotionIndicator(); return; }
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
        sentToBackground = false;
        NativeMethods.ShowWindowAsync(Handle, NativeMethods.SwShowNoActivate);
        TopMost = true;
        NativeMethods.SetWindowPos(Handle, NativeMethods.HwndTopMost, 0, 0, 0, 0,
            NativeMethods.SwpNoMove | NativeMethods.SwpNoSize | NativeMethods.SwpNoActivate | NativeMethods.SwpShowWindow);
        PositionOverlays();
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
            cameraIndex < 0 || cameraIndex >= settings.Cameras.Count || !settings.Cameras[cameraIndex.Value].IsAvailable || settings.SelectedCamera == cameraIndex) return;
        settings.SelectedCamera = cameraIndex.Value;
        SettingsStore.Save(settings);
        UpdateToolbar();
        RestartPlayer();
    }

    private void MinimizeAfterMotion()
    {
        sentToBackground = true;
        sentToBackgroundAt = DateTime.UtcNow;
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
#if BETA
        if (startupTaskbarPreview?.HandleMessage(ref message) == true) return;
        if (message.Msg == NativeMethods.WmNcPaint)
        {
            // Never let Windows draw a stale caption over the embedded players
            // while the grid or its video windows are being rearranged.
            message.Result = IntPtr.Zero;
            return;
        }
#endif
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
        public required CameraPlaceholderPanel Placeholder { get; init; }
        public required Panel[] BorderParts { get; init; }
        public int CameraIndex { get; set; } = -1;
        public Process? Player { get; set; }
    }
#endif
}

#if BETA
internal sealed class NativeWindowDiagnostics : IDisposable
{
    private readonly IntPtr monitor;
    private readonly System.Threading.Timer timer;
    private readonly object sync = new();
    private readonly string logPath;
    private string previous = "";
    private bool disposed;
    private readonly DateTime started = DateTime.UtcNow;
    private delegate bool WindowCallback(IntPtr window, IntPtr parameter);
    [StructLayout(LayoutKind.Sequential)] private struct WindowRectangle { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")] private static extern bool EnumWindows(WindowCallback callback, IntPtr parameter);
    [DllImport("user32.dll")] private static extern bool EnumChildWindows(IntPtr parent, WindowCallback callback, IntPtr parameter);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassName(IntPtr window, StringBuilder name, int capacity);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr window, out WindowRectangle rectangle);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr window);
    [DllImport("user32.dll")] private static extern IntPtr GetParent(IntPtr window);

    public NativeWindowDiagnostics(IntPtr monitor)
    {
        this.monitor = monitor;
        var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), ApplicationChannel.StorageName);
        logPath = Path.Combine(folder, "window-diagnostics.log");
        try
        {
            Directory.CreateDirectory(folder);
            if (File.Exists(logPath)) File.Move(logPath, logPath + ".previous", true);
            File.WriteAllText(logPath, $"Beta 46 window diagnostics; pid={Environment.ProcessId}; UTC={started:O}\nNo window titles, stream URLs, credentials or screen contents are recorded.\n");
        }
        catch { }
        // Independent of the UI thread, so dialog construction cannot hide the transient window.
        timer = new System.Threading.Timer(_ => Sample(), null, 0, 100);
    }

    public void Mark(string message)
    {
        lock (sync) if (!disposed) Write("EVENT " + message);
    }

    private void Write(string message)
    {
        try
        {
            if (new FileInfo(logPath).Length > 4 * 1024 * 1024)
                File.Move(logPath, logPath + ".previous", true);
            File.AppendAllText(logPath, $"{DateTime.UtcNow:O} +{(DateTime.UtcNow - started).TotalMilliseconds:F0}ms {message}\n");
        }
        catch { }
    }

    private void Sample()
    {
        if (!System.Threading.Monitor.TryEnter(sync)) return;
        try
        {
            if (disposed) return;
            GetWindowRect(monitor, out var area);
            var lines = new List<string>();
            void Add(IntPtr window)
            {
                GetWindowThreadProcessId(window, out var pid);
                GetWindowRect(window, out var rect);
                var name = new StringBuilder(256);
                GetClassName(window, name, name.Capacity);
                lines.Add($"hwnd={window.ToInt64():X} parent={GetParent(window).ToInt64():X} pid={pid} class={name} visible={IsWindowVisible(window)} style={NativeMethods.GetWindowStyle(window, -16):X8} exstyle={NativeMethods.GetWindowStyle(window, -20):X8} rect={rect.Left},{rect.Top},{rect.Right},{rect.Bottom}");
            }
            EnumWindows((window, _) =>
            {
                GetWindowThreadProcessId(window, out var pid);
                var name = new StringBuilder(256);
                GetClassName(window, name, name.Capacity);
                GetWindowRect(window, out var rect);
                var intersects = rect.Right > area.Left && rect.Left < area.Right && rect.Bottom > area.Top && rect.Top < area.Bottom;
                if (pid == Environment.ProcessId || (intersects && (name.ToString().Equals("Ghost", StringComparison.OrdinalIgnoreCase) || name.ToString().StartsWith("mpv", StringComparison.OrdinalIgnoreCase))))
                {
                    Add(window);
                    EnumChildWindows(window, (child, unused) => { Add(child); return true; }, IntPtr.Zero);
                }
                return true;
            }, IntPtr.Zero);
            var state = string.Join("\n", lines);
            if (state != previous) { previous = state; Write("WINDOWS\n" + state); }
        }
        catch { }
        finally { System.Threading.Monitor.Exit(sync); }
    }

    public void Dispose()
    {
        lock (sync) { disposed = true; timer.Dispose(); }
    }
}

internal sealed class PlaybackProgress
{
    private double? previous;
    internal bool Observe(double position)
    {
        if (!double.IsFinite(position)) return false;
        var advanced = previous.HasValue && Math.Abs(position - previous.Value) > 0.001;
        previous = position;
        return advanced;
    }
}

internal sealed class CameraPlaceholderPanel : Panel
{
    private Image? logo;
    private string cameraName = "";
    private bool offline;
    private bool empty;
    private bool showEmptyLogo;
    private bool showEmptyOuterBorder;
    private int emptyGridIndex = 3;
    private bool suppressOuterBorder;
    public bool SuppressOuterBorder
    {
        get => suppressOuterBorder;
        set { if (suppressOuterBorder == value) return; suppressOuterBorder = value; Invalidate(); }
    }

    public CameraPlaceholderPanel()
    {
        BackColor = Color.Black;
        ForeColor = Color.White;
        DoubleBuffered = true;
        ResizeRedraw = true;
        using var resource = typeof(CameraPlaceholderPanel).Assembly
            .GetManifestResourceStream("HomeCamMonitor.OfflineLogo");
        if (resource is not null)
        {
            using var source = Image.FromStream(resource);
            logo = new Bitmap(source);
        }
        SetLogoVariant(0);
    }

    public void SetLogoVariant(int variant)
    {
        logo?.Dispose();
        logo = LogoVariants.LoadImage(variant);
        BackColor = LogoVariants.Normalize(variant) == 2 ? Color.White : Color.Black;
        ForeColor = LogoVariants.Normalize(variant) == 2 ? Color.Black : Color.White;
        Invalidate();
        if (false)
        {
        }
    }

    public void Configure(string name)
    {
        empty = false;
        if (cameraName != name) offline = false;
        cameraName = name;
        Invalidate();
    }

    public void SetEmpty(bool showLogo, bool showOuterBorder = false, int gridIndex = 3)
    {
        empty = true; showEmptyLogo = showLogo; showEmptyOuterBorder = showOuterBorder; emptyGridIndex = gridIndex; Invalidate();
    }

    public void SetOffline() { offline = true; Invalidate(); }
    public void SetConnecting() { offline = false; Invalidate(); }

    internal Bitmap CreatePreview(Size size)
    {
        using var copy = new CameraPlaceholderPanel { Size = size };
        copy.cameraName = cameraName; copy.offline = offline; copy.empty = empty;
        copy.showEmptyLogo = showEmptyLogo;
        copy.BackColor = BackColor; copy.ForeColor = ForeColor;
        var bitmap = new Bitmap(size.Width, size.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        copy.DrawToBitmap(bitmap, new Rectangle(Point.Empty, size));
        return bitmap;
    }

    protected override void OnPaint(PaintEventArgs eventArgs)
    {
        base.OnPaint(eventArgs);
        if (empty && showEmptyOuterBorder && !SuppressOuterBorder && Width >= 20 && Height >= 20)
        {
            // Mirror the outside corner to match this tile's position in the 2x2 grid.
            var inset = 2f * DeviceDpi / 96f;
            var right = Width - inset;
            var bottom = Height - inset;
            var radius = Math.Max(4f, 7f * DeviceDpi / 96f - inset);
            using var outline = new System.Drawing.Drawing2D.GraphicsPath();
            outline.AddLine(right, 0, right, bottom - radius);
            outline.AddArc(right - 2 * radius, bottom - 2 * radius, 2 * radius, 2 * radius, 0, 90);
            outline.AddLine(right - radius, bottom, 0, bottom);
            using var transform = new System.Drawing.Drawing2D.Matrix(
                emptyGridIndex % 2 == 0 ? -1 : 1, 0, 0, emptyGridIndex < 2 ? -1 : 1,
                emptyGridIndex % 2 == 0 ? Width : 0, emptyGridIndex < 2 ? Height : 0);
            outline.Transform(transform);
            if (emptyGridIndex < 0)
            {
                outline.Reset();
                outline.AddArc(inset, inset, 2 * radius, 2 * radius, 180, 90);
                outline.AddArc(right - 2 * radius, inset, 2 * radius, 2 * radius, 270, 90);
                outline.AddArc(right - 2 * radius, bottom - 2 * radius, 2 * radius, 2 * radius, 0, 90);
                outline.AddArc(inset, bottom - 2 * radius, 2 * radius, 2 * radius, 90, 90);
                outline.CloseFigure();
            }
            using var pen = new Pen(Color.FromArgb(64, 64, 64), 2f * DeviceDpi / 96f);
            var smoothing = eventArgs.Graphics.SmoothingMode;
            eventArgs.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            eventArgs.Graphics.DrawPath(pen, outline);
            eventArgs.Graphics.SmoothingMode = smoothing;
        }
        if (Width < 40 || Height < 40) return;
        var iconSize = Math.Clamp(Math.Min(Width / 4, Height / 3), 20, 96);
        var textHeight = Math.Clamp(Height / 8, 14, 25);
        if (empty && !showEmptyLogo) return;
        var totalHeight = iconSize + 8 + (empty ? textHeight : 2 * textHeight);
        var top = Math.Max(4, (Height - totalHeight) / 2);
        if (logo is not null)
        {
            eventArgs.Graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
            eventArgs.Graphics.DrawImage(logo, (Width - iconSize) / 2, top, iconSize, iconSize);
        }
        if (empty)
        {
            using var brandFont = new Font("Segoe UI", Math.Clamp(Height / 28f, 8f, 11f));
            TextRenderer.DrawText(eventArgs.Graphics, "HomeCamMonitor", brandFont,
                new Rectangle(6, top + iconSize + 8, Math.Max(1, Width - 12), textHeight), ForeColor,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter
                | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);
            return;
        }
        using var statusFont = new Font("Segoe UI", Math.Clamp(Height / 24f, 9f, 13f));
        using var nameFont = new Font("Segoe UI", Math.Clamp(Height / 28f, 8f, 11f));
        const TextFormatFlags flags = TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter
            | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix;
        TextRenderer.DrawText(eventArgs.Graphics, offline ? UiText.T("Kamera offline") : UiText.T("Verbinde …"), statusFont,
            new Rectangle(6, top + iconSize + 8, Math.Max(1, Width - 12), textHeight), ForeColor, flags);
        TextRenderer.DrawText(eventArgs.Graphics, cameraName, nameFont,
            new Rectangle(6, top + iconSize + 8 + textHeight, Math.Max(1, Width - 12), textHeight),
            Color.FromArgb(165, 165, 165), flags);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) logo?.Dispose();
        base.Dispose(disposing);
    }
}

internal sealed class MotionIndicatorForm : OverlayForm
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

internal sealed class HomeCamRoundedContextMenuStrip : ContextMenuStrip
{
}

internal sealed class HomeCamRoundedDropDownMenu : ToolStripDropDownMenu
{
    public HomeCamRoundedDropDownMenu()
    {
        BackColor = Color.FromArgb(28, 28, 31);
        ForeColor = Color.White;
        Renderer = new HomeCamDarkMenuRenderer();
        ShowImageMargin = true;
        Padding = new Padding(4);
    }
}

internal sealed class HomeCamDarkMenuRenderer : ToolStripProfessionalRenderer
{
    public HomeCamDarkMenuRenderer() : base(new HomeCamDarkColorTable()) { RoundedEdges = false; }

    protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs eventArgs)
    {
        if (eventArgs.ToolStrip is not ToolStripDropDown)
        {
            base.OnRenderToolStripBorder(eventArgs);
            return;
        }

        var bounds = eventArgs.ToolStrip.ClientRectangle;
        if (bounds.Width < 14 || bounds.Height < 14) return;
        var graphicsState = eventArgs.Graphics.Save();
        eventArgs.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        using var outline = new System.Drawing.Drawing2D.GraphicsPath();
        const float radius = 6f;
        var right = bounds.Right - 1.5f;
        var bottom = bounds.Bottom - 1.5f;
        outline.AddArc(0.5f, 0.5f, radius * 2, radius * 2, 180, 90);
        outline.AddArc(right - radius * 2, 0.5f, radius * 2, radius * 2, 270, 90);
        outline.AddArc(right - radius * 2, bottom - radius * 2, radius * 2, radius * 2, 0, 90);
        outline.AddArc(0.5f, bottom - radius * 2, radius * 2, radius * 2, 90, 90);
        outline.CloseFigure();
        using var pen = new Pen(Color.FromArgb(78, 78, 84));
        eventArgs.Graphics.DrawPath(pen, outline);
        eventArgs.Graphics.Restore(graphicsState);
    }

    protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs eventArgs)
    {
        eventArgs.TextColor = eventArgs.Item?.Enabled != false ? Color.White : Color.FromArgb(125, 125, 130);
        base.OnRenderItemText(eventArgs);
    }

    protected override void OnRenderArrow(ToolStripArrowRenderEventArgs eventArgs)
    {
        eventArgs.ArrowColor = eventArgs.Item?.Enabled != false ? Color.White : Color.FromArgb(125, 125, 130);
        if (eventArgs.Direction != ArrowDirection.Right)
        {
            base.OnRenderArrow(eventArgs);
            return;
        }
        var state = eventArgs.Graphics.Save();
        eventArgs.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        eventArgs.Graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
        var centerX = eventArgs.ArrowRectangle.Left + eventArgs.ArrowRectangle.Width / 2f;
        var centerY = eventArgs.ArrowRectangle.Top + eventArgs.ArrowRectangle.Height / 2f;
        using var pen = new Pen(eventArgs.ArrowColor, 1.6f)
        {
            StartCap = System.Drawing.Drawing2D.LineCap.Round,
            EndCap = System.Drawing.Drawing2D.LineCap.Round,
            LineJoin = System.Drawing.Drawing2D.LineJoin.Round
        };
        eventArgs.Graphics.DrawLines(pen, new[]
        {
            new PointF(centerX - 2f, centerY - 3f),
            new PointF(centerX + 1.5f, centerY),
            new PointF(centerX - 2f, centerY + 3f)
        });
        eventArgs.Graphics.Restore(state);
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

// Strip the native caption at handle creation as well as through
// FormBorderStyle. Layered overlays must never acquire a temporary title area.
#if BETA
internal abstract class OverlayForm : NonActivatingForm
#else
internal abstract class OverlayForm : Form
#endif
{
#if BETA
    protected override void WndProc(ref Message message)
    {
        if (message.Msg == NativeMethods.WmNcPaint || message.Msg == 0x0086)
        {
            message.Result = (IntPtr)1;
            return;
        }
        if (message.Msg == NativeMethods.WmNcCalcSize)
        {
            message.Result = IntPtr.Zero;
            return;
        }
        base.WndProc(ref message);
    }
#endif
    protected override CreateParams CreateParams
    {
        get
        {
            var parameters = base.CreateParams;
            parameters.Style = (parameters.Style & ~0x00C40000) | unchecked((int)0x80000000); // WS_POPUP, no caption/frame
            return parameters;
        }
    }
}

internal sealed class DragSurfaceForm : OverlayForm
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

internal sealed class ResizeGripForm : OverlayForm
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

internal sealed class ToolbarForm : OverlayForm
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
        ShowAlways = true,
        OwnerDraw = true,
        BackColor = Color.FromArgb(28, 28, 31),
        ForeColor = Color.White
    };
    protected override bool ShowWithoutActivation => true;
    public string CameraName { set => name.Text = value; }
    public ToolbarForm(MonitorForm monitor)
    {
        toolTips.Draw += (_, eventArgs) =>
        {
            using var background = new SolidBrush(Color.FromArgb(28, 28, 31));
            using var outline = new Pen(Color.FromArgb(70, 70, 78));
            eventArgs.Graphics.FillRectangle(background, eventArgs.Bounds);
            eventArgs.Graphics.DrawRectangle(outline, 0, 0,
                eventArgs.Bounds.Width - 1, eventArgs.Bounds.Height - 1);
            TextRenderer.DrawText(eventArgs.Graphics, eventArgs.ToolTipText, eventArgs.Font,
                Rectangle.Inflate(eventArgs.Bounds, -3, -1), Color.White,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
        };
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
        name = Item(UiText.T("Kamera"), 32, null, 64);
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
        toolTips.SetToolTip(previous, UiText.T("Vorherige Kamera"));
        toolTips.SetToolTip(name, UiText.T("Aktuelle Kamera"));
        toolTips.SetToolTip(next, UiText.T("Nächste Kamera"));
        toolTips.SetToolTip(snapshot, UiText.T("Snapshot speichern"));
#if BETA
        toolTips.SetToolTip(grid, UiText.T("4-Kamera-Raster"));
        toolTips.SetToolTip(recording, UiText.T("Aufnahme starten"));
#endif
        toolTips.SetToolTip(settings, UiText.T("Einstellungen öffnen"));
#if BETA
        toolTips.SetToolTip(lastAction, UiText.T("Minimieren"));
#else
        toolTips.SetToolTip(lastAction, "Vollbild ein/aus");
#endif
        toolTips.SetToolTip(close, UiText.T("HomeCam Monitor beenden"));
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

    protected override void SetVisibleCore(bool value)
    {
        if (value && !Visible)
        {
            // Creating handles alone does not paint a layered window. Keep
            // its first presentation transparent until the form and every
            // child have synchronously painted their dark content.
            var targetOpacity = Opacity;
            Opacity = 0;
            _ = Handle;
            SetSizePercent(sizePercent, force: true);
            foreach (Control control in Controls) _ = control.Handle;
            try
            {
                base.SetVisibleCore(true);
                Refresh();
                foreach (Control control in Controls)
                    if (control.Visible) control.Refresh();
            }
            finally { Opacity = targetOpacity; }
            return;
        }
        base.SetVisibleCore(value);
    }

    protected override void OnHandleCreated(EventArgs eventArgs)
    {
        base.OnHandleCreated(eventArgs);
        var darkMode = 1;
        NativeMethods.DwmSetWindowAttribute(Handle, NativeMethods.DwmWindowAttribute.UseImmersiveDarkMode,
            ref darkMode, sizeof(int));
        NativeMethods.DisableDwmBorder(Handle);
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
        var item = new Label { Text = text, Left = x, Top = 0, Width = width, Height = 34, Padding = new Padding(0, 4, 0, 0), TextAlign = ContentAlignment.MiddleCenter, ForeColor = Color.White, BackColor = Color.FromArgb(20, 20, 20), Font = new Font("Segoe UI Symbol", text == UiText.T("Kamera") ? 9 : 12), Cursor = Cursors.Hand };
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
        toolTips.SetToolTip(grid, active ? UiText.T("Einzelansicht") : UiText.T("4-Kamera-Raster"));
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
        toolTips.SetToolTip(recording, !active ? UiText.T("Aufnahme starten") : manuallyStoppable
            ? UiText.T("Aufnahme beenden und speichern") : UiText.T("Automatische Bewegungsaufnahme läuft"));
    }
#endif
}

internal static class NativeMethods
{
    public const int WmNcCalcSize = 0x0083, WmNcHitTest = 0x0084, WmNcPaint = 0x0085, WmNcLButtonDown = 0x00A1, WmSysCommand = 0x0112, ScMove = 0xF010, HtCaption = 2;
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
    [DllImport("user32.dll")] public static extern IntPtr GetFocus();
    [StructLayout(LayoutKind.Sequential)] private struct LastInputInfo { public uint Size; public uint Time; }
    [DllImport("user32.dll")] private static extern bool GetLastInputInfo(ref LastInputInfo info);
    internal static uint GetIdleMilliseconds()
    {
        var info = new LastInputInfo { Size = 8 };
        return GetLastInputInfo(ref info) ? unchecked((uint)Environment.TickCount - info.Time) : 0;
    }
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
    private delegate bool ChildWindowCallback(IntPtr window, IntPtr parameter);
    [DllImport("user32.dll")] private static extern bool EnumChildWindows(IntPtr parent, ChildWindowCallback callback, IntPtr parameter);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongW")] internal static extern int GetWindowStyle(IntPtr window, int index);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongW")] private static extern int SetWindowStyle(IntPtr window, int index, int value);
#if NEXT_BETA
    [DllImport("user32.dll")] private static extern IntPtr GetParent(IntPtr window);
    [DllImport("user32.dll")] private static extern bool GetClientRect(IntPtr window, out VideoRectangle rectangle);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr window, out VideoRectangle rectangle);
    [DllImport("user32.dll")] private static extern int MapWindowPoints(IntPtr from, IntPtr to, ref VideoRectangle rectangle, uint count);
    [StructLayout(LayoutKind.Sequential)]
    private struct VideoRectangle { internal int Left, Top, Right, Bottom; }
    internal static int DisableVideoTransitions(IntPtr window)
    {
        var disabled = 1;
        return DwmSetWindowAttribute(window, 3, ref disabled, sizeof(int));
    }
#endif
    internal static void PrepareVideoChildren(IntPtr parent)
    {
        // mpv owns these HWNDs, not WinForms. Remove native caption/border before
        // revealing their hidden parent, including the very first camera switch.
        SetDiagnosticCaptionColor(parent, Color.Black);
        EnumChildWindows(parent, (window, _) =>
        {
#if NEXT_BETA
            DisableVideoTransitions(window);
            // Only mpv's immediate host child fills the panel. Its nested GPU
            // windows retain the player's own layout.
            if (GetParent(window) == parent && GetClientRect(parent, out var host) &&
                host.Right > 0 && host.Bottom > 0 && GetWindowRect(window, out var child))
            {
                MapWindowPoints(IntPtr.Zero, parent, ref child, 2);
                if (child.Left != 0 || child.Top != 0 || child.Right != host.Right || child.Bottom != host.Bottom)
                    SetWindowPos(window, IntPtr.Zero, 0, 0, host.Right, host.Bottom, SwpNoActivate | 0x0004);
            }
#endif
            const int styleIndex = -16;
            var style = GetWindowStyle(window, styleIndex);
            if ((style & 0x00C40000) != 0)
                SetWindowStyle(window, styleIndex, style & ~0x00C40000);
            DisableOverlayDecoration(window);
            SetDiagnosticCaptionColor(window, Color.Black);
            var dark = 1;
            DwmSetWindowAttribute(window, (int)DwmWindowAttribute.UseImmersiveDarkMode, ref dark, sizeof(int));
            if ((style & 0x00C40000) != 0) SetWindowPos(window, IntPtr.Zero, 0, 0, 0, 0, SwpNoMove | SwpNoSize | SwpNoActivate | 0x0004 | 0x0020);
            return true;
        }, IntPtr.Zero);
    }

    internal static void SetDiagnosticCaptionColor(IntPtr window, Color color)
    {
        // Keep any compositor fallback caption black; video pixels are unaffected.
        var captionColor = color.R | (color.G << 8) | (color.B << 16);
        DwmSetWindowAttribute(window, 35, ref captionColor, sizeof(int));
        var dark = 1;
        DwmSetWindowAttribute(window, 20, ref dark, sizeof(int));
    }

    public static void DisableOverlayDecoration(IntPtr window)
    {
        DisableDwmBorder(window);
        SetDiagnosticCaptionColor(window, Color.Black);
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
#if BETA
    protected override CreateParams CreateParams
    {
        get
        {
            var parameters = base.CreateParams;
#if !V2_BETA
            parameters.ExStyle |= 0x02000000;
#endif
            // V2 uses standard form buffering. WS_EX_COMPOSITED continually
            // repaints the native controls in the complete settings dialog.
            return parameters;
        }
    }
    internal static string GetDisplayFingerprint(Settings value)
    {
        var node = System.Text.Json.Nodes.JsonNode.Parse(JsonSerializer.Serialize(value))!.AsObject();
        foreach (var key in new[] { "SelectedCamera", "Left", "Top", "Width", "Height", "LastMonitorDeviceName",
            "MonitorOffsetX", "MonitorOffsetY", "LastGridMode", "MotionActionsPausedUntilUtc" }) node.Remove(key);
        return node.ToJsonString() + "|" + SettingsStore.Folder;
    }
    private List<Control>? displayLayouts;
#if V2_BETA
    private bool settingsPresented;
    private bool settingsPresentationPending;
    private void PresentSettings()
    {
        try
        {
            PerformLayout();
            SettingsPresentation.PaintAll(Handle);
        }
        finally
        {
            Opacity = 1;
            SettingsPresentation.SetAlpha(Handle, 255);
            settingsPresentationPending = false;
            settingsPresented = true;
        }
    }
    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        // Shown can be posted after SetVisibleCore returns. Its handlers also
        // set the final monitor bounds, so present only after those have run.
        if (TopLevel && settingsPresentationPending) PresentSettings();
    }
#endif
    private List<Control> CollectDisplayControls()
    {
        var controls = new List<Control>();
        void Collect(Control parent)
        {
            controls.Add(parent);
            foreach (Control child in parent.Controls) Collect(child);
        }
        Collect(this);
        return controls;
    }
    protected override void SetVisibleCore(bool value)
    {
#if V2_BETA
        if (value && !Visible && TopLevel)
        {
            Opacity = 0;
            PrepareForDisplay();
            SettingsPresentation.SetAlpha(Handle, 0);
            settingsPresentationPending = true;
            try
            {
                base.SetVisibleCore(true);
                if (settingsPresented && settingsPresentationPending) PresentSettings();
            }
            catch
            {
                Opacity = 1;
                SettingsPresentation.SetAlpha(Handle, 255);
                settingsPresentationPending = false;
                throw;
            }
            return;
        }
#endif
        if (value && !Visible)
        {
            displayLayouts = CollectDisplayControls();
            foreach (var control in displayLayouts) control.SuspendLayout();
        }
        try { base.SetVisibleCore(value); }
        finally { FinishDisplayLayout(); }
    }
    protected override void OnLoad(EventArgs eventArgs)
    {
        try { base.OnLoad(eventArgs); }
        finally { FinishDisplayLayout(); }
    }
    private void FinishDisplayLayout()
    {
        if (displayLayouts is not { } controls) return;
        displayLayouts = null;
        for (var index = controls.Count - 1; index >= 0; index--) controls[index].ResumeLayout(false);
        // OnLoad runs after child creation, before the native window is shown.
        // One pass now replaces repeated nested AutoSize passes during creation.
        PerformLayout();
    }
    internal void PrepareForDisplay()
    {
        // Build native controls while their ancestor is still hidden. Do not
        // call Show, change opacity, or run the visible/Shown lifecycle.
        var controls = CollectDisplayControls();
        foreach (var control in controls) control.SuspendLayout();
        try { foreach (var control in controls) _ = control.Handle; }
        finally
        {
            for (var index = controls.Count - 1; index >= 0; index--) controls[index].ResumeLayout(false);
            PerformLayout();
        }
    }
#endif

    private readonly DataGridView cameras = new SettingsCameraGrid() { Dock = DockStyle.Fill, AllowUserToAddRows = true, AllowUserToDeleteRows = true, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, RowHeadersVisible = false, SelectionMode = DataGridViewSelectionMode.FullRowSelect };
    private readonly CheckBox top = new() { Text = UiText.T("Immer im Vordergrund"), AutoSize = true };
    private readonly CheckBox autostart = new() { Text = UiText.T("Mit Windows starten"), AutoSize = true };
#if BETA
    private readonly ComboBox startBehavior = new DarkComboBox() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 180 };
    private readonly ComboBox startCamera = new DarkComboBox() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 180 };
    private readonly NumericUpDown toolbarSize = new() { Minimum = 50, Maximum = 100, Increment = 5, Width = 60 };
    private readonly CheckBox autoScaleToolbar = new() { Text = UiText.T("Bedienleiste automatisch skalieren"), AutoSize = true };
    private readonly CheckBox showEmptyFourthFieldBorder = new() { Text = UiText.T("Außenrahmen"), AutoSize = true };
    private readonly CheckBox showGridCameraNames = new() { Text = UiText.T("Kameranamen im 4er-Raster anzeigen"), AutoSize = true };
    private readonly CheckBox motionDetection = new() { Name = "DetectionEnabled", Text = UiText.T("Bewegungs- und Personenerkennung aktiv"), AutoSize = true };
    private readonly CheckBox manualRestoreKeepForeground = new() { Text = UiText.T("Nach manuellem Öffnen im Vordergrund halten"), AutoSize = true };
    private readonly CheckBox minimizeAfterManualRestore = new() { Text = UiText.T("Nach manuellem Öffnen automatisch minimieren"), AutoSize = true };
    private readonly NumericUpDown manualRestoreSeconds = new() { Minimum = 3, Maximum = 300, Value = 10, Width = 60 };
    private readonly CheckBox minimizeWhenInactive = new() { Text = UiText.T("Bei Inaktivität minimieren"), AutoSize = true };
    private readonly CheckBox restorePreviousCamera = new() { Text = UiText.T("Vorherige Kamera wiederherstellen"), AutoSize = true };
    private readonly NumericUpDown motionSeconds = new() { Minimum = 3, Maximum = 300, Value = 10, Width = 60 };
    private readonly NumericUpDown indicatorSeconds = new() { Minimum = 1, Maximum = 10, Value = 2, Width = 60 };
    private readonly CheckBox highlightMotionInGrid = new() { Text = UiText.T("Bewegung im 4er-Raster hervorheben"), AutoSize = true };
    private readonly CheckBox directHomeAssistant = new() { Text = UiText.T("Direkt mit Home Assistant verbinden (empfohlen)"), AutoSize = true };
    private readonly TextBox homeAssistantUrl = new() { Width = 300 };
    private readonly TextBox homeAssistantToken = new() { Width = 300, UseSystemPasswordChar = true };
    internal static bool HomeAssistantEventsIncomplete(bool enabled, IReadOnlyList<CameraEntry> entries, string address, string token)
    {
        if (!enabled || !entries.Any(camera => camera.ReceivesHomeAssistantEvents && (camera.UsesExternalMotion || camera.PersonEnabled == true))) return false;
        return !Uri.TryCreate(address, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) || string.IsNullOrWhiteSpace(token) ||
            !entries.Any(camera => camera.ReceivesHomeAssistantEvents &&
                ((camera.UsesExternalMotion && !string.IsNullOrWhiteSpace(camera.MotionEntityId)) ||
                 (camera.PersonEnabled == true && !string.IsNullOrWhiteSpace(camera.PersonEntityId))));
    }
    private readonly TextBox motionEntityId = new() { Width = 300 };
    private readonly TextBox personEntityId = new() { Width = 300 };
    private readonly Label selectedMotionCamera = new() { AutoSize = true, Text = UiText.T("Keine Kamera ausgewählt"), Anchor = AnchorStyles.Left };
    private readonly ComboBox motionAction = new DarkComboBox() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 200 };
    private readonly ComboBox motionVideoSeconds = new DarkComboBox() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 120 };
    private readonly ComboBox motionRetention = new DarkComboBox() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 115 };
    private readonly CheckBox ignoreHomeAssistantCertificateErrors = new() { Text = UiText.T("Ungültiges HA-Zertifikat erlauben (nur lokales Netzwerk)"), AutoSize = true };
    private readonly Button testHomeAssistant = new() { Text = UiText.T("Verbindung testen"), AutoSize = true };
    private readonly Label homeAssistantStatus = new() { AutoSize = true, MaximumSize = new Size(600, 0), Margin = new Padding(10, 6, 3, 0) };
#endif
    public Settings Result { get; private set; }
#if BETA
    internal Dictionary<string, long> ConstructionTimings { get; } = [];
    public bool RestoredFromFile { get; private set; }
    public Func<bool>? CanRestore { get; set; }
    public Func<string>? BuildDiagnostics { get; set; }
    public bool ApplyWindowState { get; private set; } = true;
    public bool ApplyViewState { get; private set; } = true;
#endif
    public SettingsForm(Settings current, string? initialSettingsFolder = null)
    {
#if BETA
        var constructionTimer = Stopwatch.StartNew();
        void Measure(string stage) { ConstructionTimings[stage] = constructionTimer.ElapsedMilliseconds; constructionTimer.Restart(); }
        DoubleBuffered = true;
        SuspendLayout();
        cameras.SuspendLayout();
        var layoutBatches = new List<Control>();
#endif
        T Batch<T>(T control) where T : Control
        {
#if BETA
            control.SuspendLayout(); layoutBatches.Add(control);
#endif
            return control;
        }
#if BETA
        ApplicationBranding.ApplyWindowIcon(this);
#endif
#if STORE_PACKAGE && V2_BETA
        // The owned dialog uses the monitor's taskbar button. It must not keep
        // a second member alive while that group is refreshed for a theme change.
        ShowInTaskbar = false;
#endif
        Result = current; Text = UiText.T("HomeCam Monitor – Einstellungen"); StartPosition = FormStartPosition.CenterParent; MinimizeBox = false;
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
        HandleCreated += (_, _) =>
        {
            var darkTitleBar = 1;
            NativeMethods.DwmSetWindowAttribute(Handle, NativeMethods.DwmWindowAttribute.UseImmersiveDarkMode,
                ref darkTitleBar, sizeof(int));
        };
        Shown += (_, _) =>
        {
            var available = Screen.FromControl(Owner ?? this).WorkingArea;
            MinimumSize = new Size(Math.Min(MinimumSize.Width, available.Width), Math.Min(MinimumSize.Height, available.Height));
            var width = Math.Min(Width, available.Width);
            var height = Math.Min(Height, available.Height);
            Bounds = new Rectangle(Math.Clamp(Left, available.Left, available.Right - width),
                Math.Clamp(Top, available.Top, available.Bottom - height), width, height);
            Activate(); BringToFront();
        };
#else
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        ClientSize = new Size(760, 390);
#endif
        cameras.Columns.Add(new DataGridViewTextBoxColumn { Name = "CameraName", HeaderText = "Name", FillWeight = 24 });
        cameras.Columns.Add(new DataGridViewTextBoxColumn { Name = "StreamUrl", HeaderText = UiText.T("RTSP-/HTTP-Streamadresse"), FillWeight = 64 });
#if BETA
        cameras.Columns.Add(new DataGridViewCheckBoxColumn { Name = "MotionEnabled", HeaderText = UiText.T("Bewegung"), FillWeight = 12 });
        cameras.Columns.Add(new DataGridViewCheckBoxColumn { Name = "PersonEnabled", HeaderText = "Person", ToolTipText = UiText.T("Speichert einen Snapshot je Personen-Ereignis. Unabhängig von der Aufnahmeaktion bei Bewegung."), FillWeight = 12 });
        cameras.Columns.Add(new DataGridViewTextBoxColumn { Name = "MotionEntityId", Visible = false });
        cameras.Columns.Add(new DataGridViewTextBoxColumn { Name = "MotionAction", Visible = false });
        cameras.Columns.Add(new DataGridViewTextBoxColumn { Name = "MotionVideoSeconds", Visible = false });
        cameras.Columns.Add(new DataGridViewTextBoxColumn { Name = "PersonEntityId", Visible = false });
        foreach (var camera in current.Cameras) cameras.Rows.Add(camera.Name,
#if V2_BETA
            camera.StoredStreamUrl,
#else
            camera.StreamUrl,
#endif
            camera.MotionEnabled,
            camera.PersonEnabled ?? !string.IsNullOrWhiteSpace(camera.PersonEntityId), camera.MotionEntityId,
            camera.MotionAction, camera.MotionVideoSeconds, camera.PersonEntityId);
#if V2_BETA
        cameras.Columns.Add(new DataGridViewCheckBoxColumn { Name = "Active", HeaderText = UiText.T("Aktiv"), FillWeight = 10, DisplayIndex = 0 });
        cameras.Columns.Add(new DataGridViewTextBoxColumn { Name = "HomeAssistantStreamUrl", Visible = false });
        cameras.Columns.Add(new DataGridViewCheckBoxColumn { Name = "RecordingEnabled", HeaderText = UiText.T("Aufnahmen"), FillWeight = 14 });
        cameras.Columns.Add(new DataGridViewTextBoxColumn { Name = "DirectConnection", Visible = false, ValueType = typeof(DirectCamera) });
        cameras.Columns.Add(new DataGridViewTextBoxColumn { Name = "HomeAssistantEntityId", Visible = false });
        cameras.Columns.Add(new DataGridViewTextBoxColumn { Name = "LocalMotion", Visible = false, ValueType = typeof(LocalMotionSettings) });
        cameras.Columns["RecordingEnabled"].ToolTipText = UiText.T("Automatische Aufnahmen für diese Kamera erlauben und Aufnahmeoptionen unten anzeigen. Ohne Haken bleiben die Optionen gespeichert.");
        foreach (DataGridViewColumn column in cameras.Columns) column.SortMode = DataGridViewColumnSortMode.NotSortable;
        for (var i = 0; i < current.Cameras.Count; i++)
        {
            cameras.Rows[i].Cells["Active"].Value = current.Cameras[i].Active;
            cameras.Rows[i].Cells["RecordingEnabled"].Value = current.Cameras[i].RecordingEnabled;
            cameras.Rows[i].Cells["HomeAssistantStreamUrl"].Value = current.Cameras[i].HomeAssistantStreamUrl;
            cameras.Rows[i].Cells["HomeAssistantEntityId"].Value = current.Cameras[i].HomeAssistantEntityId;
            cameras.Rows[i].Cells["LocalMotion"].Value = current.Cameras[i].LocalMotion.Clone();
            cameras.Rows[i].Cells["DirectConnection"].Value = current.Cameras[i].Direct.Clone();
        }
        cameras.DefaultValuesNeeded += (_, e) => e.Row.Cells["Active"].Value = true;
        cameras.RowsAdded += (_, e) =>
        {
            for (var i = e.RowIndex; i < e.RowIndex + e.RowCount; i++)
                if (!cameras.Rows[i].IsNewRow && cameras.Rows[i].Cells["Active"].Value is null) cameras.Rows[i].Cells["Active"].Value = true;
        };
#endif
#else
        foreach (var camera in current.Cameras) cameras.Rows.Add(camera.Name, camera.StreamUrl);
#endif
        top.Checked = current.AlwaysOnTop; autostart.Checked = current.StartWithWindows;
#if BETA
        startBehavior.Items.AddRange([UiText.T("Wie zuletzt"), UiText.T("Minimiert starten"), UiText.T("Mit Kamera starten"), UiText.T("Raster starten")]);
        startBehavior.SelectedIndex = current.StartBehavior switch { "Minimized" => 1, "Camera" => 2, "Grid" => 3, _ => 0 };
        foreach (var camera in current.Cameras) startCamera.Items.Add(camera.Name);
        if (startCamera.Items.Count > 0)
            startCamera.SelectedIndex = Math.Clamp(current.StartCameraIndex, 0, startCamera.Items.Count - 1);
        toolbarSize.Value = Math.Clamp(current.ToolbarSizePercent, 50, 100);
        autoScaleToolbar.Checked = current.AutoScaleToolbar;
        showGridCameraNames.Checked = current.ShowGridCameraNames;
        showEmptyFourthFieldBorder.Checked = current.ShowEmptyFourthFieldBorder;
        toolbarSize.Enabled = !autoScaleToolbar.Checked;
        autoScaleToolbar.CheckedChanged += (_, _) => toolbarSize.Enabled = !autoScaleToolbar.Checked;
#if V2_BETA
        motionDetection.Checked = true;
#else
        motionDetection.Checked = current.MotionDetectionEnabled;
#endif
        minimizeWhenInactive.Checked = current.MinimizeWhenInactive;
        minimizeAfterManualRestore.Checked = current.MinimizeAfterManualRestore;
        manualRestoreKeepForeground.Checked = current.ManualRestoreKeepForeground;
        manualRestoreSeconds.Value = Math.Clamp(current.ManualRestoreSeconds, 3, 300);
        manualRestoreSeconds.Enabled = minimizeAfterManualRestore.Checked || manualRestoreKeepForeground.Checked;
        minimizeAfterManualRestore.CheckedChanged += (_, _) => manualRestoreSeconds.Enabled = minimizeAfterManualRestore.Checked || manualRestoreKeepForeground.Checked;
        manualRestoreKeepForeground.CheckedChanged += (_, _) => manualRestoreSeconds.Enabled = minimizeAfterManualRestore.Checked || manualRestoreKeepForeground.Checked;
        restorePreviousCamera.Checked = current.RestorePreviousCameraAfterMotion;
        motionSeconds.Value = Math.Clamp(current.MotionForegroundSeconds, 3, 300);
        indicatorSeconds.Value = Math.Clamp(current.MotionIndicatorSeconds, 1, 10);
        highlightMotionInGrid.Checked = current.HighlightMotionInGrid;
        directHomeAssistant.Checked = current.DirectHomeAssistantEnabled;
        homeAssistantUrl.Text = current.HomeAssistantUrl;
        homeAssistantToken.Text = current.HomeAssistantToken;
        ignoreHomeAssistantCertificateErrors.Checked = current.IgnoreHomeAssistantCertificateErrors;
        motionAction.Items.AddRange([UiText.T("Keine"), "Snapshot", UiText.T("Videoaufnahme"), UiText.T("Snapshot + Videoaufnahme")]);
        motionVideoSeconds.Items.AddRange([UiText.T("15 Sekunden"), UiText.T("30 Sekunden"), UiText.T("60 Sekunden")]);
        motionRetention.Items.AddRange([UiText.T("1 Tag"), UiText.T("3 Tage"), UiText.T("7 Tage"), UiText.T("14 Tage"), UiText.T("30 Tage"), UiText.T("Unbegrenzt")]);
        var retentionValues = new[] { 1, 3, 7, 14, 30, 0 };
        motionRetention.SelectedIndex = Math.Max(0, Array.IndexOf(retentionValues, current.MotionRetentionDays));
        var selectedCameraRow = -1;
        var movingCamera = false;
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
            if (movingCamera) return;
            StoreSelectedCameraMotion();
            selectedCameraRow = cameras.CurrentRow?.Index ?? -1;
            if (selectedCameraRow < 0 || selectedCameraRow >= cameras.Rows.Count || cameras.Rows[selectedCameraRow].IsNewRow)
            {
                selectedMotionCamera.Text = UiText.T("Keine Kamera ausgewählt");
                motionEntityId.Text = "";
                personEntityId.Text = "";
                motionAction.SelectedIndex = 0;
                motionVideoSeconds.SelectedIndex = 1;
                return;
            }
            selectedMotionCamera.Text = Convert.ToString(cameras.Rows[selectedCameraRow].Cells[0].Value)?.Trim() is { Length: > 0 } name ? name : UiText.T("Neue Kamera");
            motionEntityId.Text = Convert.ToString(cameras.Rows[selectedCameraRow].Cells[4].Value)?.Trim() ?? "";
            personEntityId.Text = Convert.ToString(cameras.Rows[selectedCameraRow].Cells[7].Value)?.Trim() ?? "";
            motionAction.SelectedIndex = Convert.ToString(cameras.Rows[selectedCameraRow].Cells[5].Value) switch { "Snapshot" => 1, "Video" => 2, "Both" => 3, _ => 0 };
            motionVideoSeconds.SelectedIndex = Convert.ToInt32(cameras.Rows[selectedCameraRow].Cells[6].Value ?? 30) switch { 15 => 0, 60 => 2, _ => 1 };
            motionVideoSeconds.Enabled = motionAction.SelectedIndex is 2 or 3;
        }
        motionAction.SelectedIndexChanged += (_, _) => motionVideoSeconds.Enabled = motionAction.SelectedIndex is 2 or 3;
        cameras.SelectionChanged += (_, _) => LoadSelectedCameraMotion();
        cameras.CurrentCellChanged += (_, _) => LoadSelectedCameraMotion();
        cameras.CurrentCellDirtyStateChanged += (_, _) => { if (cameras.IsCurrentCellDirty) cameras.CommitEdit(DataGridViewDataErrorContexts.Commit); };
        void RefreshStartCameraChoices()
        {
#if V2_BETA
            if (movingCamera) return;
#endif
            var selected = startCamera.SelectedIndex;
            startCamera.Items.Clear();
            foreach (DataGridViewRow row in cameras.Rows)
            {
                if (row.IsNewRow) continue;
                var name = Convert.ToString(row.Cells[0].Value)?.Trim() ?? "";
                var url = Convert.ToString(row.Cells[1].Value)?.Trim() ?? "";
                if (name.Length > 0 || url.Length > 0) startCamera.Items.Add(name.Length > 0 ? name : UiText.T("Neue Kamera"));
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
#if V2_BETA
            // HA stream access is independent of the selected motion source.
            var directEnabled = true;
#else
            var directEnabled = motionDetection.Checked && directHomeAssistant.Checked;
#endif
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
#if V2_BETA
        var scrollArea = Batch(new ModernSettingsScrollArea { Name = "SettingsScrollArea", Dock = DockStyle.Fill });
#else
        var scrollArea = Batch(new SettingsScrollPanel { Name = "SettingsScrollArea", Dock = DockStyle.Fill, AutoScroll = true });
#endif
        var table = Batch(new SettingsLayoutPanel { Name = "SettingsContent", Dock = DockStyle.Top, AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink, Padding = new Padding(14), ColumnCount = 1, RowCount = 8 });
#if V2_BETA
        scrollArea.SetContent(table);
#else
        scrollArea.Controls.Add(table);
#endif
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 180));
        for (var row = 1; row < table.RowCount; row++) table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
#else
        var table = Batch(new SettingsLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(14), ColumnCount = 1, RowCount = 5 });
        table.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
#endif
        table.Controls.Add(cameras, 0, 0);
#if BETA
        table.Controls.Add(new Label { Name = "CameraHint", Text = UiText.T("Kamera anklicken, Sensoren unten eintragen und Bewegung oder Person in der Liste aktivieren."), AutoSize = true, ForeColor = SystemColors.GrayText, Margin = new Padding(3, 5, 3, 5) }, 0, 1);
#else
        table.Controls.Add(new Label { Text = "Beispiel: rtsp://192.168.x.x:8554/Einfahrt", AutoSize = true, ForeColor = SystemColors.GrayText }, 0, 1);
#endif
#if BETA
        var options = Batch(new SettingsLayoutPanel { Name = "Options", Dock = DockStyle.Top, AutoSize = true, ColumnCount = 1, RowCount = 3, Margin = new Padding(0) });
        options.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        options.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        var generalOptions = Batch(new SettingsFlowPanel { Dock = DockStyle.Top, AutoSize = true, WrapContents = false, Margin = new Padding(0) });
        generalOptions.Controls.Add(top);
        generalOptions.Controls.Add(autostart);
        options.Controls.Add(generalOptions, 0, 0);

        var toolbarOptions = Batch(new SettingsFlowPanel { Dock = DockStyle.Top, AutoSize = true, WrapContents = false, Margin = new Padding(0) });
        toolbarOptions.Controls.Add(new Label { Text = UiText.T("Bedienleiste:"), AutoSize = true, Margin = new Padding(3, 4, 3, 0) });
        toolbarOptions.Controls.Add(toolbarSize);
        toolbarOptions.Controls.Add(new Label { Text = "%", AutoSize = true, Margin = new Padding(3, 4, 3, 0) });
        toolbarOptions.Controls.Add(autoScaleToolbar);
        options.Controls.Add(toolbarOptions, 0, 1);
        var cameraAppearance = Batch(new SettingsFlowPanel { Name = "CameraAppearance", Dock = DockStyle.Top, AutoSize = true, WrapContents = true, Margin = new Padding(0) });
        cameraAppearance.Controls.Add(showGridCameraNames);
        cameraAppearance.Controls.Add(showEmptyFourthFieldBorder);
        options.Controls.Add(cameraAppearance, 0, 2);

#else
        var options = Batch(new SettingsFlowPanel { Dock = DockStyle.Fill, AutoSize = true });
        options.Controls.Add(top);
        options.Controls.Add(autostart);
#endif
#if BETA
        var startupOptions = Batch(new SettingsFlowPanel { Name = "StartupOptions", Dock = DockStyle.Fill, AutoSize = true });
        startupOptions.Controls.Add(new Label { Text = UiText.T("Beim Start:"), AutoSize = true, Margin = new Padding(3, 4, 3, 0) });
        startupOptions.Controls.Add(startBehavior);
        var startCameraLabel = new Label { Text = UiText.T("Kamera:"), AutoSize = true, Margin = new Padding(18, 4, 3, 0) };
        startupOptions.Controls.Add(startCameraLabel);
        startupOptions.Controls.Add(startCamera);
        void UpdateStartCameraOption()
        {
            startCameraLabel.Visible = startBehavior.SelectedIndex == 2;
            startCamera.Visible = startBehavior.SelectedIndex == 2;
        }
        startBehavior.SelectedIndexChanged += (_, _) => UpdateStartCameraOption();
        UpdateStartCameraOption();
        var generalGroup = Batch(new GroupBox { Text = UiText.T("Allgemeine Einstellungen"), Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(10) });
        var generalFields = Batch(new SettingsLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 1, RowCount = 2 });
        generalFields.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        generalFields.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        generalFields.Controls.Add(options, 0, 0);
        generalFields.Controls.Add(startupOptions, 0, 1);
        generalGroup.Controls.Add(generalFields);
        table.Controls.Add(generalGroup, 0, 2);

        var activityOptions = Batch(new SettingsLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 1, RowCount = 4 });
        for (var row = 0; row < activityOptions.RowCount; row++)
            activityOptions.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        var minimizedStart = new CheckBox { Name = "MinimizedStart", Text = UiText.T("Minimiert starten"), AutoSize = true,
            Checked = startBehavior.SelectedIndex == 1 };
        var lastNormalStart = startBehavior.SelectedIndex == 1 ? 0 : startBehavior.SelectedIndex;
        var syncingStart = false;
        startBehavior.SelectedIndexChanged += (_, _) =>
        {
            if (syncingStart) return;
            if (startBehavior.SelectedIndex != 1) lastNormalStart = startBehavior.SelectedIndex;
            syncingStart = true;
            try { minimizedStart.Checked = startBehavior.SelectedIndex == 1; }
            finally { syncingStart = false; }
        };
        minimizedStart.CheckedChanged += (_, _) =>
        {
            if (syncingStart) return;
            syncingStart = true;
            try { startBehavior.SelectedIndex = minimizedStart.Checked ? 1 : lastNormalStart; }
            finally { syncingStart = false; }
        };
        var activityStartOptions = Batch(new SettingsFlowPanel { Dock = DockStyle.Top, AutoSize = true, WrapContents = true, Margin = new Padding(0) });
#if !V2_BETA
        activityStartOptions.Controls.Add(motionDetection);
#endif
        activityStartOptions.Controls.Add(minimizedStart);
        activityOptions.Controls.Add(activityStartOptions, 0, 0);

        var motionOptions = Batch(new SettingsFlowPanel { Dock = DockStyle.Top, AutoSize = true, WrapContents = false, Margin = new Padding(0) });
        motionOptions.Controls.Add(minimizeWhenInactive);
        motionOptions.Controls.Add(restorePreviousCamera);
        motionOptions.Controls.Add(new Label { Text = UiText.T("Vordergrunddauer:"), AutoSize = true, Margin = new Padding(18, 4, 3, 0) });
        motionOptions.Controls.Add(motionSeconds);
        motionOptions.Controls.Add(new Label { Text = UiText.T("Sekunden"), AutoSize = true, Margin = new Padding(3, 4, 3, 0) });
        activityOptions.Controls.Add(motionOptions, 0, 1);

        var indicatorOptions = Batch(new SettingsFlowPanel { Dock = DockStyle.Top, AutoSize = true, WrapContents = false, Margin = new Padding(0) });
        indicatorOptions.Controls.Add(new Label { Text = UiText.T("Aktivitätssymbole anzeigen:"), AutoSize = true, Margin = new Padding(3, 4, 3, 0) });
        indicatorOptions.Controls.Add(indicatorSeconds);
        indicatorOptions.Controls.Add(new Label { Text = UiText.T("Sekunden"), AutoSize = true, Margin = new Padding(3, 4, 12, 0) });
        indicatorOptions.Controls.Add(highlightMotionInGrid);
        activityOptions.Controls.Add(indicatorOptions, 0, 2);
        var manualOptions = Batch(new SettingsFlowPanel { Dock = DockStyle.Top, AutoSize = true, WrapContents = true, Margin = new Padding(0) });
        manualOptions.Controls.Add(manualRestoreKeepForeground);
        manualOptions.Controls.Add(minimizeAfterManualRestore);
        manualOptions.Controls.Add(manualRestoreSeconds);
        manualOptions.Controls.Add(new Label { Text = UiText.T("Sekunden (pausiert bei Mauszeiger im Fenster)"), AutoSize = true });
        activityOptions.Controls.Add(manualOptions, 0, 3);

        var activityGroup = Batch(new GroupBox { Text = UiText.T("Bewegung und Aktivitätsanzeige"), Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(10) });
        activityGroup.Controls.Add(activityOptions);
        table.Controls.Add(activityGroup, 0, 3);
#else
        table.Controls.Add(options, 0, 2);
#endif
#if BETA
        var snapshotPreRoll = new DarkComboBox { Name = "SnapshotPreRoll", DropDownStyle = ComboBoxStyle.DropDownList, Width = 115 };
        var videoPreRoll = new DarkComboBox { Name = "VideoPreRoll", DropDownStyle = ComboBoxStyle.DropDownList, Width = 115 };
        var preRollValues = new[] { 0, 1, 3, 5 };
        snapshotPreRoll.Items.AddRange([UiText.T("Aus"), UiText.T("1 Sekunde"), UiText.T("3 Sekunden"), UiText.T("5 Sekunden")]);
        videoPreRoll.Items.AddRange([UiText.T("Aus"), UiText.T("1 Sekunde"), UiText.T("3 Sekunden"), UiText.T("5 Sekunden")]);
        snapshotPreRoll.SelectedIndex = Math.Max(0, Array.IndexOf(preRollValues, current.SnapshotPreRollSeconds));
        videoPreRoll.SelectedIndex = Math.Max(0, Array.IndexOf(preRollValues, current.VideoPreRollSeconds));
        var captureOptions = Batch(new SettingsFlowPanel { Dock = DockStyle.Top, AutoSize = true, WrapContents = false, Margin = new Padding(0) });
        captureOptions.Controls.Add(motionAction);
        captureOptions.Controls.Add(motionVideoSeconds);
        var homeAssistantGroup = Batch(new GroupBox { Text = UiText.T("Bewegung pro Kamera und Home Assistant"), Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(10) });
        var homeAssistantFields = Batch(new SettingsLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2, RowCount = 8 });
        homeAssistantFields.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        homeAssistantFields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        homeAssistantFields.Controls.Add(directHomeAssistant, 0, 0);
        homeAssistantFields.SetColumnSpan(directHomeAssistant, 2);
        homeAssistantFields.Controls.Add(new Label { Text = UiText.T("HA-Adresse:"), AutoSize = true, Anchor = AnchorStyles.Left }, 0, 1);
        homeAssistantFields.Controls.Add(homeAssistantUrl, 1, 1);
        homeAssistantFields.Controls.Add(new Label { Text = UiText.T("Langzeit-Token:"), AutoSize = true, Anchor = AnchorStyles.Left }, 0, 2);
        homeAssistantFields.Controls.Add(homeAssistantToken, 1, 2);
        homeAssistantFields.Controls.Add(ignoreHomeAssistantCertificateErrors, 0, 3);
        homeAssistantFields.SetColumnSpan(ignoreHomeAssistantCertificateErrors, 2);
        var testRow = Batch(new SettingsFlowPanel { Dock = DockStyle.Top, AutoSize = true, WrapContents = false });
        testRow.Controls.Add(testHomeAssistant);
        testRow.Controls.Add(homeAssistantStatus);
        homeAssistantFields.Controls.Add(testRow, 0, 4);
        homeAssistantFields.SetColumnSpan(testRow, 2);
        homeAssistantFields.Controls.Add(new Label { Text = UiText.T("Ausgewählte Kamera:"), AutoSize = true, Anchor = AnchorStyles.Left }, 0, 5);
        homeAssistantFields.Controls.Add(selectedMotionCamera, 1, 5);
        homeAssistantFields.Controls.Add(new Label { Text = UiText.T("Bewegungs-Entität:"), AutoSize = true, Anchor = AnchorStyles.Left }, 0, 6);
        homeAssistantFields.Controls.Add(motionEntityId, 1, 6);
        homeAssistantFields.Controls.Add(new Label { Text = UiText.T("Personen-Entität (Snapshot):"), AutoSize = true, Anchor = AnchorStyles.Left }, 0, 7);
        homeAssistantFields.Controls.Add(personEntityId, 1, 7);
        var recordingGroup = Batch(new GroupBox { Name = "MotionCaptureOptions", Text = UiText.T("Aufnahmen"), Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(10, 18, 10, 10) });
        var recordingFields = Batch(new SettingsLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2, RowCount = 9 });
        recordingFields.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        recordingFields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        var selectedCaptureHint = new Label { Name = "MotionActionHint", Text = UiText.T("Bei Bewegung (ausgewählte Kamera)"), AutoSize = true };
        recordingFields.Controls.Add(selectedCaptureHint, 0, 0); recordingFields.SetColumnSpan(selectedCaptureHint, 2);
        recordingFields.Controls.Add(captureOptions, 0, 1); recordingFields.SetColumnSpan(captureOptions, 2);
        var personCaptureHint = new Label { Name = "PersonCaptureHint", Text = UiText.T("Bei Personenerkennung: Snapshot je Ereignis – aktiviert durch „Person“ oben."), AutoSize = true,
            MaximumSize = new Size(340, 0), Margin = new Padding(3, 8, 3, 3) };
        recordingFields.Controls.Add(personCaptureHint, 0, 2); recordingFields.SetColumnSpan(personCaptureHint, 2);
        var globalCaptureHint = new Label { Text = UiText.T("Automatische Aufnahmen (alle Kameras)"), AutoSize = true, Margin = new Padding(3, 14, 3, 6) };
        recordingFields.Controls.Add(globalCaptureHint, 0, 3); recordingFields.SetColumnSpan(globalCaptureHint, 2);
        recordingFields.Controls.Add(new Label { Text = UiText.T("Aufbewahrung:"), AutoSize = true, Anchor = AnchorStyles.Left }, 0, 4);
        recordingFields.Controls.Add(motionRetention, 1, 4);
        recordingFields.Controls.Add(new Label { Text = UiText.T("Snapshot-Vorlauf:"), AutoSize = true, Anchor = AnchorStyles.Left }, 0, 5);
        recordingFields.Controls.Add(snapshotPreRoll, 1, 5);
        var snapshotPreRollHint = new Label { Name = "SnapshotPreRollHint",
            Text = UiText.T("Gilt für Snapshots bei Bewegung und bei Personenerkennung. „Keine“ deaktiviert nur Aufnahmen bei Bewegung."),
            AutoSize = true, MaximumSize = new Size(340, 0), Margin = new Padding(3, 4, 3, 8) };
        recordingFields.Controls.Add(snapshotPreRollHint, 0, 6); recordingFields.SetColumnSpan(snapshotPreRollHint, 2);
        recordingFields.Controls.Add(new Label { Text = UiText.T("Video-Vorlauf:"), AutoSize = true, Anchor = AnchorStyles.Left }, 0, 7);
        recordingFields.Controls.Add(videoPreRoll, 1, 7);
        var preRollHint = new Label { Text = UiText.T("Der Vorlauf benötigt einen kurzen Pufferaufbau. Bis dahin erfolgt die normale Aufnahme."), AutoSize = true, MaximumSize = new Size(340, 0), Margin = new Padding(3, 10, 3, 3) };
        recordingFields.Controls.Add(preRollHint, 0, 8); recordingFields.SetColumnSpan(preRollHint, 2);
        recordingGroup.Controls.Add(recordingFields);
        var motionColumns = Batch(new SettingsLayoutPanel { Name = "MotionSettingsColumns", Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2, RowCount = 1, Margin = new Padding(0) });
        motionColumns.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55));
        motionColumns.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45));
        motionColumns.Controls.Add(homeAssistantFields, 0, 0);
        motionColumns.Controls.Add(recordingGroup, 1, 0);
        homeAssistantGroup.Controls.Add(motionColumns);
#if V2_BETA
        var reolinkEditor = new ReolinkEditor(cameras, current);
        reolinkEditor.Activated += () => motionDetection.Checked = true;
        motionDetection.Text = UiText.T("Bewegungs- und Personenerkennung aktiv");
        Text = ApplicationBranding.ProductName + UiText.T(" – Einstellungen");
        var connectionTabs = new V2ConnectionTabs(cameras, reolinkEditor, homeAssistantFields,
            directHomeAssistant, motionDetection, personEntityId, motionEntityId, homeAssistantUrl, homeAssistantToken, ignoreHomeAssistantCertificateErrors);
        var externalMotionFields = new SettingsLayoutPanel { Name = "ExternalMotionFields", Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2 };
        externalMotionFields.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        externalMotionFields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        externalMotionFields.Controls.Add(directHomeAssistant, 0, 0); externalMotionFields.SetColumnSpan(directHomeAssistant, 2);
        foreach (var field in homeAssistantFields.Controls.Cast<Control>().Where(c => homeAssistantFields.GetRow(c) >= 5).ToArray())
        {
            var column = homeAssistantFields.GetColumn(field); var row = homeAssistantFields.GetRow(field) - 4;
            externalMotionFields.Controls.Add(field, column, row);
        }
        var localMotionEditor = new LocalMotionEditor(cameras, homeAssistantUrl, homeAssistantToken, ignoreHomeAssistantCertificateErrors, externalMotionFields);
        connectionTabs.SourceChanged += localMotionEditor.LoadSelection;
        localMotionEditor.Changed += connectionTabs.RefreshCapabilities;
        var reolinkAndRecording = new SettingsLayoutPanel { Dock = DockStyle.Top, AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink, ColumnCount = 1, RowCount = 2 };
        reolinkAndRecording.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        reolinkAndRecording.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        reolinkAndRecording.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        reolinkAndRecording.Controls.Add(new ConnectionSettingsSection(connectionTabs), 0, 0);
        reolinkAndRecording.RowCount = 3; reolinkAndRecording.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        reolinkAndRecording.Controls.Add(localMotionEditor, 0, 1);
        reolinkAndRecording.Controls.Add(recordingGroup, 0, 2);
        table.Controls.Add(reolinkAndRecording, 0, 4);
        table.SetRow(generalGroup, 3);
        table.SetRow(activityGroup, 4);
        table.SetRow(reolinkAndRecording, 2);
        void UpdateRecordingSection()
        {
            if (movingCamera) return;
            var row = cameras.CurrentRow;
            var show = row is { IsNewRow: false } && Convert.ToBoolean(row.Cells["RecordingEnabled"].Value ?? false);
            recordingGroup.Text = UiText.F("Aufnahmen – {0}", selectedMotionCamera.Text);
            if (recordingGroup.Visible != show) recordingGroup.Visible = show;
            recordingGroup.Enabled = row is { IsNewRow: false } && Convert.ToBoolean(row.Cells["Active"].Value ?? true);
        }
        cameras.SelectionChanged += (_, _) => UpdateRecordingSection();
        cameras.CurrentCellChanged += (_, _) => UpdateRecordingSection();
        cameras.CellValueChanged += (_, e) =>
        {
            if (e.ColumnIndex == cameras.Columns["RecordingEnabled"].Index || e.ColumnIndex == cameras.Columns["Active"].Index)
                UpdateRecordingSection();
        };
        UpdateRecordingSection();
        var cameraTools = (Control)table.Controls.Find("CameraHint", false).Single();
        table.Controls.Remove(cameraTools);
        var cameraOrder = Batch(new SettingsFlowPanel { Dock = DockStyle.Top, AutoSize = true, WrapContents = true });
        var moveUp = new Button { Name = "MoveCameraUp", Text = UiText.T("Nach oben"), AutoSize = true };
        var moveDown = new Button { Name = "MoveCameraDown", Text = UiText.T("Nach unten"), AutoSize = true };
        var deleteCamera = new Button { Name = "DeleteCamera", Text = UiText.T("Kamera löschen"), AutoSize = true };
        cameraOrder.Controls.AddRange([moveUp, moveDown, deleteCamera, cameraTools]);
        cameras.AllowUserToDeleteRows = false;
        table.Controls.Add(cameraOrder, 0, 1);
        cameras.ScrollBars = ScrollBars.None;
        void UpdateCameraTableHeight()
        {
            if (movingCamera) return;
            // Show every camera row; one common dark scrollbar handles the
            // entire settings page instead of a second scrollbar in the grid.
            var height = cameras.ColumnHeadersHeight + cameras.Rows.Cast<DataGridViewRow>().Sum(r => r.Height) + 4;
            table.RowStyles[0].Height = Math.Max(130, height);
        }
        cameras.RowsAdded += (_, _) => UpdateCameraTableHeight();
        cameras.RowsRemoved += (_, _) => UpdateCameraTableHeight();
        UpdateCameraTableHeight();
        void UpdateOrderButtons()
        {
            if (movingCamera) return;
            var index = cameras.CurrentRow is { IsNewRow: false } row ? row.Index : -1;
            moveUp.Enabled = index > 0;
            moveDown.Enabled = index >= 0 && index < cameras.Rows.Count - 2;
            deleteCamera.Enabled = index >= 0;
        }
        void MoveCamera(int offset)
        {
            if (cameras.CurrentRow is not { IsNewRow: false } row) return;
            var target = row.Index + offset;
            if (target < 0 || target >= cameras.Rows.Count - 1) return;
            cameras.EndEdit(); StoreSelectedCameraMotion();
            var startName = Convert.ToString(startCamera.SelectedItem);
            scrollArea.PreserveView(() =>
            {
                movingCamera = true;
                selectedCameraRow = -1;
                try
                {
                    using (CameraGridUpdate.Begin(cameras))
                        reolinkEditor.Reorder(() =>
                        {
                            cameras.Rows.Remove(row);
                            cameras.Rows.Insert(target, row);
                            cameras.CurrentCell = row.Cells["CameraName"];
                            row.Selected = true;
                        });
                }
                finally { movingCamera = false; }
                UpdateCameraTableHeight();
                RefreshStartCameraChoices();
                if (startName is not null && startCamera.Items.Contains(startName)) startCamera.SelectedItem = startName;
                LoadSelectedCameraMotion();
                connectionTabs.RefreshAfterReorder(); localMotionEditor.LoadSelection();
                UpdateRecordingSection(); UpdateOrderButtons();
            });
        }
        moveUp.Click += (_, _) => MoveCamera(-1);
        moveDown.Click += (_, _) => MoveCamera(1);
        void DeleteSelectedCamera()
        {
            if (cameras.CurrentRow is not { IsNewRow: false } row) return;
            cameras.EndEdit(); StoreSelectedCameraMotion();
            var index = row.Index;
            var startName = Convert.ToString(startCamera.SelectedItem);
            scrollArea.PreserveView(() =>
            {
                movingCamera = true;
                selectedCameraRow = -1;
                try
                {
                    using (CameraGridUpdate.Begin(cameras))
                        reolinkEditor.Reorder(() =>
                        {
                            cameras.Rows.Remove(row);
                            cameras.ClearSelection();
                            var next = cameras.Rows[Math.Min(index, cameras.Rows.Count - 1)];
                            cameras.CurrentCell = next.Cells["CameraName"];
                            if (!next.IsNewRow) next.Selected = true;
                        });
                }
                finally { movingCamera = false; }
                UpdateCameraTableHeight(); RefreshStartCameraChoices();
                if (startName is not null && startCamera.Items.Contains(startName)) startCamera.SelectedItem = startName;
                LoadSelectedCameraMotion(); connectionTabs.RefreshAfterRemoval(); localMotionEditor.LoadSelection();
                UpdateRecordingSection(); UpdateOrderButtons();
            });
        }
        deleteCamera.Click += (_, _) => DeleteSelectedCamera();
        cameras.KeyDown += (_, e) =>
        {
            if (e.KeyCode != Keys.Delete || e.Modifiers != Keys.None || cameras.IsCurrentCellInEditMode) return;
            DeleteSelectedCamera(); e.Handled = true; e.SuppressKeyPress = true;
        };
        cameras.SelectionChanged += (_, _) => UpdateOrderButtons();
        cameras.CurrentCellChanged += (_, _) => UpdateOrderButtons();
        UpdateOrderButtons();
        personCaptureHint.Text = UiText.T("Personen-Snapshots benötigen eine unterstützte Home-Assistant-Personen-Entität. Bei Reolink-UID ist „Person“ gesperrt.");
        homeAssistantGroup.Visible = false;
        if (cameraTools is Label cameraHint)
            cameraHint.Text = UiText.T("Kamera auswählen und Verbindung unter Reolink oder Home Assistant einrichten. „Aktiv“ schaltet die Kamera vollständig ein oder aus.");
#else
        table.Controls.Add(homeAssistantGroup, 0, 4);
#endif
#endif
#if BETA
        var storageGroup = Batch(new GroupBox { Name = "RecordingStorage", Text = UiText.T("Speicherpfade"), Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(10) });
        var storageFields = Batch(new SettingsLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 3 });
        storageFields.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        storageFields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        storageFields.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        TextBox AddStoragePath(string label, string value, string fallback, int row)
        {
            var field = new TextBox { Name = "StoragePath" + row, Dock = DockStyle.Fill, Text = RecordingStorage.Resolve(value, fallback) };
            var browse = new Button { Text = UiText.T("Durchsuchen …"), AutoSize = true };
            browse.Click += (_, _) =>
            {
                using var dialog = new FolderBrowserDialog { SelectedPath = field.Text, Description = label };
                if (dialog.ShowDialog(this) == DialogResult.OK) field.Text = dialog.SelectedPath;
            };
            storageFields.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left }, 0, row);
            storageFields.Controls.Add(field, 1, row); storageFields.Controls.Add(browse, 2, row);
            return field;
        }
        var manualSnapshotPath = AddStoragePath(UiText.T("Manuelle Snapshots:"), current.ManualSnapshotFolder, RecordingStorage.ManualSnapshots, 0);
        var manualVideoPath = AddStoragePath(UiText.T("Manuelle Videos:"), current.ManualVideoFolder, RecordingStorage.ManualVideos, 1);
        var motionSnapshotPath = AddStoragePath(UiText.T("Snapshots bei Bewegung:"), RecordingStorage.ResolveMotionSnapshots(current.MotionSnapshotFolder), RecordingStorage.MotionSnapshots, 2);
        var motionVideoPath = AddStoragePath(UiText.T("Videos bei Bewegung:"), current.MotionVideoFolder, RecordingStorage.MotionDefault, 3);
        var settingsPath = AddStoragePath(UiText.T("Einstellungen:"), initialSettingsFolder ?? SettingsStore.Folder, SettingsLocation.DefaultFolder, 4);
        settingsPath.Name = "SettingsStoragePath";
        var storageHint = new Label { Text = UiText.T("Die Aufbewahrungsfrist gilt nur für automatische Bewegungsaufnahmen. Manuelle Aufnahmen bleiben erhalten."), AutoSize = true, MaximumSize = new Size(700, 0) };
        storageFields.Controls.Add(storageHint, 0, 5); storageFields.SetColumnSpan(storageHint, 3);
        var settingsPathHint = new Label { Text = UiText.T("Beim Speichern wird die settings.json in den gewählten Ordner mitgenommen."), AutoSize = true, MaximumSize = new Size(700, 0) };
        storageFields.Controls.Add(settingsPathHint, 0, 6); storageFields.SetColumnSpan(settingsPathHint, 3);
        storageGroup.Controls.Add(storageFields); table.Controls.Add(storageGroup, 0, 5);
        // Existing diagnostics remain available; the release uses a neutral heading.
        var betaLogging = new CheckBox { Name = "BetaWindowLogging", Text = UiText.T("Fensterprotokollierung aktivieren"), AutoSize = true, Checked = current.BetaWindowLoggingEnabled };
        var betaFields = Batch(new SettingsFlowPanel { Dock = DockStyle.Top, AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false });
        betaFields.Controls.Add(betaLogging);
        betaFields.Controls.Add(new Label { Text = UiText.T("Zur Fehlersuche bei Darstellungsproblemen. Änderungen gelten nach dem Speichern."), AutoSize = true });
        var betaGroup = Batch(new GroupBox { Name = "BetaDiagnostics", Text = ApplicationBranding.ProductName.Contains("Beta", StringComparison.OrdinalIgnoreCase) ? "BETA" : UiText.T("Darstellungsdiagnose"), Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(10, 18, 10, 10) });
        betaGroup.Controls.Add(betaFields);
        table.Controls.Add(betaGroup, 0, 7);
        var backupGroup = Batch(new GroupBox { Name = "SettingsBackup", Text = UiText.T("Einstellungen sichern und wiederherstellen"), Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(10) });
        var backupFields = Batch(new SettingsFlowPanel { Dock = DockStyle.Top, AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false });
        var backupButtons = Batch(new SettingsFlowPanel { AutoSize = true, WrapContents = false });
        var exportSettings = new Button { Name = "ExportSettings", Text = UiText.T("Einstellungen sichern …"), AutoSize = true };
        var importSettings = new Button { Name = "ImportSettings", Text = UiText.T("Einstellungen wiederherstellen …"), AutoSize = true };
        backupButtons.Controls.Add(exportSettings); backupButtons.Controls.Add(importSettings);
        backupFields.Controls.Add(backupButtons);
        backupFields.Controls.Add(new Label { Text = UiText.T("Sichert die gespeicherten Einstellungen einschließlich Kameras, Speicherpfaden und HA-Token."), AutoSize = true, MaximumSize = new Size(700, 0) });
        backupGroup.Controls.Add(backupFields); table.Controls.Add(backupGroup, 0, 6);
        var maintenanceButtons = Batch(new SettingsFlowPanel { AutoSize = true, WrapContents = false });
        var resetScope = new DarkComboBox { Name = "SettingsResetScope", DropDownStyle = ComboBoxStyle.DropDownList, Width = 235 };
        resetScope.Items.AddRange([UiText.T("Fensterposition und Größe"), UiText.T("Anzeige und Bedienung"), UiText.T("Kameraeinstellungen"), UiText.T("Alle Einstellungen")]);
        resetScope.SelectedIndex = 0;
        var resetSettings = new Button { Name = "ResetSettings", Text = UiText.T("Zurücksetzen …"), AutoSize = true };
        var exportDiagnostics = new Button { Name = "ExportDiagnostics", Text = UiText.T("Diagnose exportieren …"), AutoSize = true };
        maintenanceButtons.Controls.AddRange([resetScope, resetSettings, exportDiagnostics]);
        backupFields.Controls.Add(maintenanceButtons);
        var resetHint = new Label { Name = "SettingsResetHint", AutoSize = true, MaximumSize = new Size(700, 0),
            Margin = new Padding(3, 4, 3, 5) };
        backupFields.Controls.Add(resetHint);
        var resetTips = new ToolTip { AutoPopDelay = 15000, InitialDelay = 500, ReshowDelay = 100, ShowAlways = true };
        Disposed += (_, _) => resetTips.Dispose();
        void UpdateResetHint()
        {
            var explanation = SettingsReset.Describe((SettingsResetScope)resetScope.SelectedIndex);
            resetHint.Text = explanation;
            resetTips.SetToolTip(resetScope, explanation);
            resetTips.SetToolTip(resetSettings, explanation);
        }
        resetScope.SelectedIndexChanged += (_, _) => UpdateResetHint();
        UpdateResetHint();
        backupFields.Controls.Add(new Label { Text = UiText.T("Zurücksetzen gilt sofort nach Bestätigung. Aufnahmedateien und der gewählte Einstellungsordner bleiben erhalten."), AutoSize = true, MaximumSize = new Size(700, 0) });
        backupFields.Controls.Add(new Label { Text = UiText.T("Diagnose: Versionen, bereinigte Einstellungen und Stream-Ereignisse der laufenden Sitzung, ohne Zugangsdaten."), AutoSize = true, MaximumSize = new Size(700, 0) });
        backupGroup.Text = UiText.T("Einstellungen sichern, zurücksetzen und Diagnose");
        resetSettings.Click += (_, _) =>
        {
            var scope = (SettingsResetScope)resetScope.SelectedIndex;
            var explanation = SettingsReset.Describe(scope);
            if (MessageBox.Show(this, explanation + UiText.T("\n\nUngespeicherte Änderungen werden verworfen. Aufnahmedateien bleiben erhalten.\nJetzt zurücksetzen?"), UiText.T("Einstellungen zurücksetzen"), MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;
            try
            {
                if (CanRestore?.Invoke() == false) throw new InvalidOperationException(UiText.T("Bitte laufende Aufnahmen zuerst beenden und danach zurücksetzen."));
                var reset = SettingsReset.Apply(current, scope);
                SettingsStore.Save(reset);
                Result = reset; RestoredFromFile = true;
                ApplyWindowState = scope is SettingsResetScope.Window or SettingsResetScope.All;
                ApplyViewState = scope is SettingsResetScope.Cameras or SettingsResetScope.All;
                DialogResult = DialogResult.OK; Close();
            }
            catch (Exception error) { MessageBox.Show(this, error.Message, UiText.T("Zurücksetzen fehlgeschlagen"), MessageBoxButtons.OK, MessageBoxIcon.Warning); }
        };
        exportDiagnostics.Click += (_, _) =>
        {
            using var file = new SaveFileDialog { Filter = UiText.T("HomeCamMonitor-Diagnose (*.json)|*.json"), DefaultExt = "json", AddExtension = true,
                FileName = $"HomeCamMonitor-Diagnose_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.json" };
            if (file.ShowDialog(this) != DialogResult.OK) return;
            try
            {
                var report = BuildDiagnostics?.Invoke() ?? DiagnosticExport.Serialize(current, new StreamDiagnostics().Snapshot(), new { });
                DiagnosticExport.Write(file.FileName, report);
                MessageBox.Show(this, UiText.T("Diagnose wurde exportiert."), UiText.T("Diagnose exportieren"), MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception error) { MessageBox.Show(this, error.Message, UiText.T("Diagnoseexport fehlgeschlagen"), MessageBoxButtons.OK, MessageBoxIcon.Warning); }
        };
        exportSettings.Click += (_, _) =>
        {
            using var file = new SaveFileDialog { Filter = UiText.T("HomeCamMonitor-Einstellungen (*.json)|*.json"), DefaultExt = "json", AddExtension = true, FileName = $"HomeCamMonitor-Einstellungen_{Environment.MachineName}_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.json" };
            if (file.ShowDialog(this) != DialogResult.OK) return;
            try { SettingsBackup.Write(file.FileName, current); MessageBox.Show(this, UiText.T("Einstellungen wurden gesichert."), UiText.T("Einstellungen sichern"), MessageBoxButtons.OK, MessageBoxIcon.Information); }
            catch (Exception error) { MessageBox.Show(this, error.Message, UiText.T("Sicherung fehlgeschlagen"), MessageBoxButtons.OK, MessageBoxIcon.Warning); }
        };
        importSettings.Click += (_, _) =>
        {
            using var file = new OpenFileDialog { Filter = UiText.T("HomeCamMonitor-Einstellungen (*.json)|*.json"), CheckFileExists = true };
            if (file.ShowDialog(this) != DialogResult.OK) return;
            try
            {
                var restored = SettingsBackup.Read(file.FileName);
                if (MessageBox.Show(this, UiText.F("Die aktuellen Einstellungen durch diese Sicherung mit {0} Kameras ersetzen?\nUngespeicherte Änderungen werden verworfen. Aufnahmen bleiben erhalten.", restored.Cameras.Count), UiText.T("Einstellungen wiederherstellen"), MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;
                if (CanRestore?.Invoke() == false) throw new InvalidOperationException(UiText.T("Bitte laufende Aufnahmen zuerst beenden und danach die Einstellungen wiederherstellen."));
                SettingsStore.Save(restored);
                Result = restored; RestoredFromFile = true; DialogResult = DialogResult.OK; Close();
            }
            catch (Exception error) { MessageBox.Show(this, error.Message, UiText.T("Wiederherstellung fehlgeschlagen"), MessageBoxButtons.OK, MessageBoxIcon.Warning); }
        };
        var footer = Batch(new SettingsLayoutPanel { Name = "SettingsFooter", Dock = DockStyle.Bottom, AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink, Padding = new Padding(14, 6, 14, 6), ColumnCount = 2, RowCount = 1 });
        footer.Paint += (_, paint) =>
        {
            using var separator = new Pen(Color.FromArgb(65, 65, 69));
            paint.Graphics.DrawLine(separator, 0, 0, footer.ClientSize.Width - 1, 0);
        };
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        footer.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        footer.Controls.Add(new Label { Text = $"Version {Application.ProductVersion.Split('+')[0]}", AutoSize = true,
            ForeColor = SystemColors.GrayText, Anchor = AnchorStyles.Left, Margin = new Padding(3, 0, 3, 0) }, 0, 0);
#else
        table.Controls.Add(new Label { Text = $"Version {Application.ProductVersion.Split('+')[0]}", AutoSize = true, ForeColor = SystemColors.GrayText, Anchor = AnchorStyles.Left }, 0, 3);
#endif
#if BETA
        var buttons = Batch(new SettingsFlowPanel { Anchor = AnchorStyles.Right, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Margin = Padding.Empty, WrapContents = false, FlowDirection = FlowDirection.RightToLeft });
#else
        var buttons = Batch(new SettingsFlowPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft });
#endif
        var ok = new Button { Text = UiText.T("Speichern"), DialogResult = DialogResult.OK, AutoSize = true };
        buttons.Controls.Add(ok); buttons.Controls.Add(new Button { Text = UiText.T("Abbrechen"), DialogResult = DialogResult.Cancel, AutoSize = true });
#if BETA
        footer.Controls.Add(buttons, 1, 0);
#else
        table.Controls.Add(buttons, 0, 4);
#endif
#if BETA
        Controls.Add(scrollArea);
        Controls.Add(footer);
        Measure("controls");
        ApplyDarkTheme(this);
#if V2_BETA
        connectionTabs.UpdateTabAppearance();
#endif
        Measure("theme");
#else
        Controls.Add(table);
#endif
        AcceptButton = ok; CancelButton = buttons.Controls[1] as Button;
#if BETA
        testHomeAssistant.Click += async (_, _) =>
        {
            homeAssistantStatus.ForeColor = SystemColors.GrayText;
            homeAssistantStatus.Text = UiText.T("Verbindung wird geprüft …");
            testHomeAssistant.Enabled = false;
            try
            {
                var selectedRow = selectedCameraRow >= 0 && selectedCameraRow < cameras.Rows.Count
                    ? cameras.Rows[selectedCameraRow] : null;
                var motionId = selectedRow is not null && Convert.ToBoolean(selectedRow.Cells[2].Value ?? false)
                    ? motionEntityId.Text.Trim() : "";
                var personId = selectedRow is not null && Convert.ToBoolean(selectedRow.Cells[3].Value ?? false)
                    ? personEntityId.Text.Trim() : "";
#if V2_BETA
                if (selectedRow is not null && LocalMotionSettings.ForRow(selectedRow).Enabled) motionId = "";
#endif
                if (!Uri.TryCreate(homeAssistantUrl.Text.Trim(), UriKind.Absolute, out var testUri) ||
                    (testUri.Scheme != Uri.UriSchemeHttp && testUri.Scheme != Uri.UriSchemeHttps) ||
                    string.IsNullOrWhiteSpace(homeAssistantToken.Text)
#if !V2_BETA
                    || (motionId.Length == 0 && personId.Length == 0)
#endif
                    )
                {
                    homeAssistantStatus.ForeColor = Color.Firebrick;
                    homeAssistantStatus.Text =
#if V2_BETA
                        UiText.T("Gültige HA-Adresse und Langzeit-Token eintragen.");
#else
                        "Adresse, Token und mindestens einen aktivierten Sensor eintragen.";
#endif
                    return;
                }
                var error = await MonitorForm.TestHomeAssistantConnectionAsync(homeAssistantUrl.Text.Trim(), homeAssistantToken.Text.Trim(),
                    motionId, ignoreHomeAssistantCertificateErrors.Checked, personId);
                homeAssistantStatus.ForeColor = error is null ? Color.ForestGreen : Color.Firebrick;
                homeAssistantStatus.Text = error is null ? (motionId.Length == 0 && personId.Length == 0 ? UiText.T("Verbunden – Anmeldung erfolgreich.") : UiText.T("Verbunden – eingetragene Sensoren gefunden.")) : error;
            }
            finally { testHomeAssistant.Enabled =
#if V2_BETA
                true;
#else
                directHomeAssistant.Checked && motionDetection.Checked;
#endif
            }
        };
#endif
        ok.Click += (_, _) =>
        {
#if BETA
            StoreSelectedCameraMotion();
#endif
#if V2_BETA
            reolinkEditor.Store();
#endif
            var entries = ReadCameras();
#if V2_BETA
            try
            {
                foreach (var camera in entries) camera.LocalMotion.Validate();
                foreach (var camera in entries.Where(c => c.Active)) camera.Reolink.Validate();
                foreach (var camera in entries.Where(c => c.Active && c.Direct.Enabled)) camera.StreamUrl = camera.Direct.StreamUrl();
                if (entries.Any(c => c.Active && c.Reolink.Enabled) && !File.Exists(reolinkEditor.HelperPath))
                    throw new ArgumentException(UiText.T("Bitte den Reolink-Verbindungsdienst (neolink.exe) auswählen."));
                if (entries.Select(c => c.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count() != entries.Count)
                    throw new ArgumentException(UiText.T("Bitte jeder Kamera einen eindeutigen Namen geben."));
            }
            catch (ArgumentException error)
            {
                MessageBox.Show(this, error.Message, UiText.T("Reolink-Konfiguration"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                DialogResult = DialogResult.None; return;
            }
#endif
#if BETA
            if (entries.Any(c => c.Active && !Uri.TryCreate(c.StreamUrl, UriKind.Absolute, out _)))
#else
            if (entries.Count == 0 || entries.Any(c => !Uri.TryCreate(c.StreamUrl, UriKind.Absolute, out _)))
#endif
            {
                MessageBox.Show(this, UiText.T("Bitte gültige Streamadressen eintragen."), UiText.T("Ungültige Kamera"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                DialogResult = DialogResult.None; return;
            }
#if BETA
            if (startBehavior.SelectedIndex == 2 && (startCamera.SelectedIndex < 0 || startCamera.SelectedIndex >= entries.Count))
            {
                MessageBox.Show(this, UiText.T("Bitte eine Startkamera auswählen."), UiText.T("Startverhalten"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                DialogResult = DialogResult.None; return;
            }
            if (HomeAssistantEventsIncomplete(directHomeAssistant.Checked, entries, homeAssistantUrl.Text.Trim(), homeAssistantToken.Text))
            {
                MessageBox.Show(this, UiText.T("Bitte HA-Adresse und Langzeit-Token eintragen sowie für mindestens eine aktivierte Kamera eine Sensor-Entität hinterlegen."), UiText.T("Home Assistant unvollständig"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                DialogResult = DialogResult.None; return;
            }
#endif
            #if BETA
            try
            {
                foreach (var field in new[] { manualSnapshotPath, manualVideoPath, motionSnapshotPath, motionVideoPath })
                    _ = RecordingStorage.Resolve(field.Text, RecordingStorage.MotionDefault);
                _ = SettingsLocation.Normalize(settingsPath.Text);
            }
            catch (ArgumentException error)
            {
                MessageBox.Show(this, error.Message, UiText.T("Ungültiger Speicherpfad"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                DialogResult = DialogResult.None; return;
            }
            #endif
            Result = new Settings
            {
#if V2_BETA
                ReolinkHelperPath = reolinkEditor.HelperPath,
#endif
                Cameras = entries, SelectedCamera = Math.Max(0, entries.FindIndex(c => current.SelectedCamera >= 0 && current.SelectedCamera < current.Cameras.Count && c.Name.Equals(current.Cameras[current.SelectedCamera].Name, StringComparison.OrdinalIgnoreCase))),
                AlwaysOnTop = top.Checked, StartWithWindows = autostart.Checked,
                Left = current.Left, Top = current.Top, Width = current.Width, Height = current.Height,
#if BETA
                BetaWindowLoggingEnabled = betaLogging.Checked,
                LastMonitorDeviceName = current.LastMonitorDeviceName,
                MonitorOffsetX = current.MonitorOffsetX, MonitorOffsetY = current.MonitorOffsetY,
                LastGridMode = current.LastGridMode,
                StartBehavior = startBehavior.SelectedIndex switch { 1 => "Minimized", 2 => "Camera", 3 => "Grid", _ => "Last" },
                StartCameraIndex = Math.Max(0, startCamera.SelectedIndex),
                ToolbarSizePercent = (int)toolbarSize.Value,
                AutoScaleToolbar = autoScaleToolbar.Checked,
                ShowGridCameraNames = showGridCameraNames.Checked,
                ShowEmptyCameraLogo = current.ShowEmptyCameraLogo,
                ShowEmptyFourthFieldBorder = showEmptyFourthFieldBorder.Checked,
#if !V2_BETA
                MotionDetectionEnabled = motionDetection.Checked,
#endif
                MotionActionsPausedUntilUtc = current.MotionActionsPausedUntilUtc,
                MotionForegroundSeconds = (int)motionSeconds.Value,
                MotionIndicatorSeconds = (int)indicatorSeconds.Value,
                HighlightMotionInGrid = highlightMotionInGrid.Checked,
                MinimizeWhenInactive = minimizeWhenInactive.Checked,
                MinimizeAfterManualRestore = minimizeAfterManualRestore.Checked,
                ManualRestoreKeepForeground = manualRestoreKeepForeground.Checked,
                ManualRestoreSeconds = (int)manualRestoreSeconds.Value,
                RestorePreviousCameraAfterMotion = restorePreviousCamera.Checked,
                DirectHomeAssistantEnabled = directHomeAssistant.Checked,
                HomeAssistantUrl = homeAssistantUrl.Text.Trim(),
                HomeAssistantToken = homeAssistantToken.Text.Trim(),
                IgnoreHomeAssistantCertificateErrors = ignoreHomeAssistantCertificateErrors.Checked,
                PerCameraMotionConfigured = true,
                MotionRetentionDays = retentionValues[motionRetention.SelectedIndex],
                SnapshotPreRollSeconds = preRollValues[snapshotPreRoll.SelectedIndex],
                VideoPreRollSeconds = preRollValues[videoPreRoll.SelectedIndex],
                ManualSnapshotFolder = manualSnapshotPath.Text.Trim(),
                ManualVideoFolder = manualVideoPath.Text.Trim(),
                MotionSnapshotFolder = motionSnapshotPath.Text.Trim(),
                MotionVideoFolder = motionVideoPath.Text.Trim(),
                SettingsWindowWidth = ClientSize.Width,
                SettingsWindowHeight = ClientSize.Height
#endif
            };
#if BETA
            try
            {
                var target = SettingsLocation.Normalize(settingsPath.Text);
                var changingFolder = !SettingsLocation.SameFolder(target, SettingsStore.Folder);
                var exists = changingFolder && File.Exists(Path.Combine(target, "settings.json"));
                if (exists && MessageBox.Show(this, UiText.T("Im Zielordner existiert bereits eine settings.json. Diese durch die aktuellen Einstellungen ersetzen?"), UiText.T("Einstellungsordner wechseln"), MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) != DialogResult.Yes)
                { DialogResult = DialogResult.None; return; }
                SettingsLocation.Move(Result, target, exists);
            }
            catch (Exception error)
            {
                MessageBox.Show(this, error.Message, UiText.T("Einstellungen konnten nicht gespeichert werden"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                DialogResult = DialogResult.None;
            }
#endif
        };
#if BETA
        Measure("events");
        cameras.ResumeLayout(false);
        for (var index = layoutBatches.Count - 1; index >= 0; index--) layoutBatches[index].ResumeLayout(true);
        ResumeLayout(true);
        Measure("layout");
#endif
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
#if V2_BETA
            entry.Reolink = (row.Tag as ReolinkCamera ?? new ReolinkCamera()).Clone();
            entry.Active = Convert.ToBoolean(row.Cells["Active"].Value ?? true);
            entry.RecordingEnabled = Convert.ToBoolean(row.Cells["RecordingEnabled"].Value ?? false);
            entry.HomeAssistantStreamUrl = Convert.ToString(row.Cells["HomeAssistantStreamUrl"].Value) ?? "";
            entry.HomeAssistantEntityId = Convert.ToString(row.Cells["HomeAssistantEntityId"].Value) ?? "";
            entry.LocalMotion = LocalMotionSettings.ForRow(row).Clone();
            entry.Direct = DirectCamera.ForRow(row).Clone();
#endif
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
    internal static void ApplyDarkTheme(Control root)
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
#if V2_BETA
                    label.BackColor = background;
#else
                    label.BackColor = Color.Transparent;
#endif
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
