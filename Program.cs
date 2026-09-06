using System.Text.Json;
using System.Runtime.InteropServices;
using LibVLCSharp.Shared;
using LibVLCSharp.WinForms;
using Microsoft.Win32;

namespace HomeCamMonitor;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        Core.Initialize();
        Application.Run(new MonitorForm());
    }
}

internal sealed class Settings
{
    public List<CameraEntry> Cameras { get; set; } =
    [
        new() { Name = "Einfahrt", StreamUrl = "rtsp://ha:PASSWORT@192.168.189.206:554/h264Preview_01_sub" },
        new() { Name = "Garten", StreamUrl = "rtsp://ha:PASSWORT@192.168.189.207:554/h264Preview_01_sub" }
    ];
    public int SelectedCamera { get; set; }
    public bool AlwaysOnTop { get; set; } = true;
    public bool StartWithWindows { get; set; }
    public int Left { get; set; } = -1;
    public int Top { get; set; } = -1;
    public int Width { get; set; } = 480;
    public int Height { get; set; } = 300;

}

internal sealed class CameraEntry
{
    public string Name { get; set; } = "Kamera";
    public string StreamUrl { get; set; } = "";
}

internal static class SettingsStore
{
    private static readonly string Folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "HomeCamMonitor");
    private static readonly string FileName = Path.Combine(Folder, "settings.json");
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static Settings Load()
    {
        try { return JsonSerializer.Deserialize<Settings>(File.ReadAllText(FileName)) ?? new Settings(); }
        catch { return new Settings(); }
    }

    public static void Save(Settings settings)
    {
        Directory.CreateDirectory(Folder);
        File.WriteAllText(FileName, JsonSerializer.Serialize(settings, JsonOptions));
    }
}

internal sealed class MonitorForm : Form
{
    private readonly VideoView video = new() { Dock = DockStyle.Fill, BackColor = Color.Black };
    private readonly FlowLayoutPanel cameraBar = new()
    {
        AutoSize = true, BackColor = Color.Transparent,
        FlowDirection = FlowDirection.LeftToRight, WrapContents = false, Padding = new Padding(4)
    };
    private readonly ToolTip toolTip = new();
    private readonly System.Windows.Forms.Timer watchdog = new() { Interval = 2000 };
    private readonly LibVLC vlc;
    private readonly MediaPlayer player;
    private Settings settings;
    private DateTime lastHealthyPlayback = DateTime.UtcNow;
    private DateTime restartAllowed = DateTime.MinValue;
    private bool restarting;
    private bool closing;
    private bool fullscreen;
    private Rectangle windowedBounds;

    public MonitorForm()
    {
        settings = SettingsStore.Load();
        Text = "HomeCam Monitor";
        BackColor = Color.Black;
        FormBorderStyle = FormBorderStyle.None;
        MinimumSize = new Size(240, 150);
        ClientSize = new Size(Math.Max(240, settings.Width), Math.Max(150, settings.Height));
        if (settings.Left >= 0 && settings.Top >= 0)
        {
            StartPosition = FormStartPosition.Manual;
            Location = new Point(settings.Left, settings.Top);
        }
        TopMost = settings.AlwaysOnTop;

        vlc = new LibVLC("--no-audio", "--rtsp-tcp", "--network-caching=350", "--clock-jitter=0", "--clock-synchro=0");
        player = new MediaPlayer(vlc) { EnableHardwareDecoding = true };
        video.MediaPlayer = player;
        Controls.Add(video);
        Controls.Add(cameraBar);
        BuildCameraButtons();
        PositionCameraBar();
        cameraBar.BringToFront();

        var menu = new ContextMenuStrip();
        menu.Items.Add("Snapshot speichern", null, (_, _) => SaveSnapshot());
        menu.Items.Add("Stream neu laden", null, (_, _) => RestartStream());
        menu.Items.Add("Einstellungen …", null, (_, _) => OpenSettings());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Beenden", null, (_, _) => Close());
        video.ContextMenuStrip = menu;
        video.DoubleClick += (_, _) => ToggleFullscreen();
        video.MouseDown += VideoMouseDown;
        video.MouseMove += VideoMouseMove;
        Resize += (_, _) => PositionCameraBar();

        player.EncounteredError += (_, _) => ScheduleRestart();
        player.Stopped += (_, _) => { if (!restarting && !IsDisposed) ScheduleRestart(); };
        player.Playing += (_, _) => lastHealthyPlayback = DateTime.UtcNow;
        watchdog.Tick += (_, _) => CheckStream();
        Shown += (_, _) => FirstStart();
        FormClosing += (_, _) => { closing = true; SaveWindow(); };
    }

    private void FirstStart()
    {
        if (!HasUsableCamera())
            OpenSettings();
        if (HasUsableCamera())
        {
            StartStream();
            watchdog.Start();
        }
    }

    private void StartStream()
    {
        lastHealthyPlayback = DateTime.UtcNow;
        var camera = settings.Cameras[settings.SelectedCamera];
        Text = $"HomeCam Monitor – {camera.Name}";
        using var media = new Media(vlc, new Uri(camera.StreamUrl));
        media.AddOption(":rtsp-tcp");
        media.AddOption(":network-caching=350");
        media.AddOption(":no-audio");
        player.Play(media);
    }

    private async void RestartStream()
    {
        if (restarting || closing || IsDisposed || DateTime.UtcNow < restartAllowed) return;
        restarting = true;
        restartAllowed = DateTime.UtcNow.AddSeconds(3);
        try
        {
            player.Stop();
            await Task.Delay(500);
            if (!IsDisposed) StartStream();
        }
        finally { restarting = false; }
    }

    private void ScheduleRestart()
    {
        if (closing || IsDisposed || !IsHandleCreated) return;
        BeginInvoke(new Action(RestartStream));
    }

    private void CheckStream()
    {
        if (player.IsPlaying)
        {
            lastHealthyPlayback = DateTime.UtcNow;
            return;
        }
        if (DateTime.UtcNow - lastHealthyPlayback >= TimeSpan.FromSeconds(10))
        {
            lastHealthyPlayback = DateTime.UtcNow;
            RestartStream();
        }
    }

    private void OpenSettings()
    {
        using var dialog = new SettingsForm(settings);
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        settings = dialog.Result;
        if (settings.SelectedCamera < 0 || settings.SelectedCamera >= settings.Cameras.Count)
            settings.SelectedCamera = 0;
        TopMost = settings.AlwaysOnTop;
        BuildCameraButtons();
        SettingsStore.Save(settings);
        ConfigureAutostart(settings.StartWithWindows);
        if (HasUsableCamera()) RestartStream();
    }

    private bool HasUsableCamera() => settings.Cameras.Count > 0
        && settings.SelectedCamera >= 0
        && settings.SelectedCamera < settings.Cameras.Count
        && IsUsableStreamUrl(settings.Cameras[settings.SelectedCamera].StreamUrl);

    private static bool IsUsableStreamUrl(string value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri)
        && (uri.Scheme.Equals("rtsp", StringComparison.OrdinalIgnoreCase)
            || uri.Scheme.Equals("http", StringComparison.OrdinalIgnoreCase)
            || uri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase))
        && !value.Contains("PASSWORT", StringComparison.OrdinalIgnoreCase);

    private void BuildCameraButtons()
    {
        cameraBar.Controls.Clear();
        var previous = CreateOverlayButton("\uE76B", "Vorherige Kamera");
        previous.Click += (_, _) => SelectRelativeCamera(-1);
        cameraBar.Controls.Add(previous);

        var cameraName = new Label
        {
            Text = settings.Cameras.Count > 0 && settings.SelectedCamera < settings.Cameras.Count
                ? settings.Cameras[settings.SelectedCamera].Name : "Kamera",
            AutoSize = true, Height = 32, Padding = new Padding(6, 8, 6, 0),
            ForeColor = Color.White, BackColor = Color.Transparent
        };
        cameraBar.Controls.Add(cameraName);

        var next = CreateOverlayButton("\uE76C", "Nächste Kamera");
        next.Click += (_, _) => SelectRelativeCamera(1);
        cameraBar.Controls.Add(next);

        var snapshot = CreateOverlayButton("\uE722", "Snapshot speichern");
        snapshot.Click += (_, _) => SaveSnapshot();
        cameraBar.Controls.Add(snapshot);

        var options = CreateOverlayButton("\uE713", "Einstellungen");
        options.Click += (_, _) => OpenSettings();
        cameraBar.Controls.Add(options);

        var close = CreateOverlayButton("\uE711", "Schließen");
        close.Click += (_, _) => Close();
        cameraBar.Controls.Add(close);
        PositionCameraBar();
    }

    private Button CreateOverlayButton(string symbol, string hint)
    {
        var button = new Button
        {
            Text = symbol, Font = new Font("Segoe MDL2 Assets", 12),
            Size = new Size(34, 32), Margin = new Padding(1),
            FlatStyle = FlatStyle.Flat, ForeColor = Color.White,
            BackColor = Color.Transparent, TabStop = false, UseVisualStyleBackColor = false
        };
        button.FlatAppearance.BorderSize = 0;
        button.FlatAppearance.MouseOverBackColor = Color.FromArgb(75, 75, 75);
        button.FlatAppearance.MouseDownBackColor = Color.FromArgb(0, 100, 170);
        toolTip.SetToolTip(button, hint);
        return button;
    }

    private void PositionCameraBar()
    {
        cameraBar.Location = new Point(
            Math.Max(8, (ClientSize.Width - cameraBar.Width) / 2),
            Math.Max(8, ClientSize.Height - cameraBar.Height - 8));
        cameraBar.BringToFront();
    }

    private void VideoMouseDown(object? sender, MouseEventArgs eventArgs)
    {
        if (eventArgs.Button != MouseButtons.Left || fullscreen) return;
        var hitTest = GetEdgeHitTest(eventArgs.Location);
        if (hitTest == NativeMethods.HtClient && eventArgs.Y <= 28)
            hitTest = NativeMethods.HtCaption;
        if (hitTest == NativeMethods.HtClient) return;

        NativeMethods.ReleaseCapture();
        NativeMethods.SendMessage(Handle, NativeMethods.WmNcLeftButtonDown, (IntPtr)hitTest, IntPtr.Zero);
    }

    private void VideoMouseMove(object? sender, MouseEventArgs eventArgs)
    {
        if (fullscreen) { video.Cursor = Cursors.Default; return; }
        video.Cursor = GetEdgeHitTest(eventArgs.Location) switch
        {
            NativeMethods.HtLeft or NativeMethods.HtRight => Cursors.SizeWE,
            NativeMethods.HtTop or NativeMethods.HtBottom => Cursors.SizeNS,
            NativeMethods.HtTopLeft or NativeMethods.HtBottomRight => Cursors.SizeNWSE,
            NativeMethods.HtTopRight or NativeMethods.HtBottomLeft => Cursors.SizeNESW,
            _ => Cursors.Default
        };
    }

    private int GetEdgeHitTest(Point cursor)
    {
        const int grip = 8;
        var left = cursor.X <= grip;
        var right = cursor.X >= video.ClientSize.Width - grip;
        var topEdge = cursor.Y <= grip;
        var bottom = cursor.Y >= video.ClientSize.Height - grip;

        if (left && topEdge) return NativeMethods.HtTopLeft;
        if (right && topEdge) return NativeMethods.HtTopRight;
        if (left && bottom) return NativeMethods.HtBottomLeft;
        if (right && bottom) return NativeMethods.HtBottomRight;
        if (left) return NativeMethods.HtLeft;
        if (right) return NativeMethods.HtRight;
        if (topEdge) return NativeMethods.HtTop;
        if (bottom) return NativeMethods.HtBottom;
        return NativeMethods.HtClient;
    }

    private void SelectRelativeCamera(int direction)
    {
        if (settings.Cameras.Count < 2) return;
        var index = (settings.SelectedCamera + direction + settings.Cameras.Count) % settings.Cameras.Count;
        SelectCamera(index);
    }

    private void SelectCamera(int index)
    {
        if (index < 0 || index >= settings.Cameras.Count || index == settings.SelectedCamera) return;
        settings.SelectedCamera = index;
        SettingsStore.Save(settings);
        BuildCameraButtons();
        RestartStream();
    }

    private static void ConfigureAutostart(bool enabled)
    {
        using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true);
        if (enabled)
            key?.SetValue("HomeCamMonitor", $"\"{Application.ExecutablePath}\"");
        else
            key?.DeleteValue("HomeCamMonitor", false);
    }

    private void SaveSnapshot()
    {
        if (!HasUsableCamera() || !player.IsPlaying)
        {
            MessageBox.Show(this, "Der Kamerastream läuft noch nicht.", "Snapshot nicht möglich",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        try
        {
            var pictures = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
            var folder = Path.Combine(pictures, "HomeCam Monitor");
            Directory.CreateDirectory(folder);

            var cameraName = string.Concat(settings.Cameras[settings.SelectedCamera].Name
                .Select(character => Path.GetInvalidFileNameChars().Contains(character) ? '_' : character));
            var fileName = $"{cameraName}_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.png";
            var filePath = Path.Combine(folder, fileName);

            if (!player.TakeSnapshot(0, filePath, 0, 0))
                throw new InvalidOperationException("LibVLC konnte kein Bild speichern.");

            ShowSnapshotFeedback(fileName);
        }
        catch (Exception exception)
        {
            MessageBox.Show(this, $"Der Snapshot konnte nicht gespeichert werden.\n\n{exception.Message}",
                "Snapshot fehlgeschlagen", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private async void ShowSnapshotFeedback(string fileName)
    {
        Text = $"HomeCam Monitor – Snapshot gespeichert: {fileName}";
        await Task.Delay(2500);
        if (!IsDisposed && HasUsableCamera())
            Text = $"HomeCam Monitor – {settings.Cameras[settings.SelectedCamera].Name}";
    }

    private void ToggleFullscreen()
    {
        SuspendLayout();
        if (!fullscreen)
        {
            windowedBounds = Bounds;
            fullscreen = true;
            cameraBar.Visible = false;
            WindowState = FormWindowState.Normal;
            Bounds = Screen.FromControl(this).Bounds;
        }
        else
        {
            fullscreen = false;
            Bounds = windowedBounds;
            cameraBar.Visible = true;
            PositionCameraBar();
        }
        ResumeLayout(true);
    }

    private void SaveWindow()
    {
        watchdog.Stop();
        if (fullscreen)
        {
            settings.Left = windowedBounds.Left;
            settings.Top = windowedBounds.Top;
            settings.Width = windowedBounds.Width;
            settings.Height = windowedBounds.Height;
        }
        else if (WindowState == FormWindowState.Normal)
        {
            settings.Left = Left;
            settings.Top = Top;
            settings.Width = ClientSize.Width;
            settings.Height = ClientSize.Height;
        }
        SettingsStore.Save(settings);
        player.Stop();
        player.Dispose();
        vlc.Dispose();
    }

    protected override void WndProc(ref Message message)
    {
        const int wmNcHitTest = 0x0084;
        base.WndProc(ref message);
        if (message.Msg != wmNcHitTest || fullscreen || message.Result.ToInt32() != NativeMethods.HtClient) return;

        var cursor = PointToClient(Cursor.Position);
        const int grip = 7;
        var left = cursor.X <= grip;
        var right = cursor.X >= ClientSize.Width - grip;
        var topEdge = cursor.Y <= grip;
        var bottom = cursor.Y >= ClientSize.Height - grip;

        if (left && topEdge) message.Result = (IntPtr)NativeMethods.HtTopLeft;
        else if (right && topEdge) message.Result = (IntPtr)NativeMethods.HtTopRight;
        else if (left && bottom) message.Result = (IntPtr)NativeMethods.HtBottomLeft;
        else if (right && bottom) message.Result = (IntPtr)NativeMethods.HtBottomRight;
        else if (left) message.Result = (IntPtr)NativeMethods.HtLeft;
        else if (right) message.Result = (IntPtr)NativeMethods.HtRight;
        else if (topEdge) message.Result = (IntPtr)NativeMethods.HtTop;
        else if (bottom) message.Result = (IntPtr)NativeMethods.HtBottom;
        else if (cursor.Y <= 28) message.Result = (IntPtr)NativeMethods.HtCaption;
    }
}

internal static class NativeMethods
{
    public const int WmNcLeftButtonDown = 0x00A1;
    public const int HtClient = 1;
    public const int HtCaption = 2;
    public const int HtLeft = 10;
    public const int HtRight = 11;
    public const int HtTop = 12;
    public const int HtTopLeft = 13;
    public const int HtTopRight = 14;
    public const int HtBottom = 15;
    public const int HtBottomLeft = 16;
    public const int HtBottomRight = 17;

    [DllImport("user32.dll")]
    public static extern bool ReleaseCapture();

    [DllImport("user32.dll")]
    public static extern IntPtr SendMessage(IntPtr window, int message, IntPtr parameter, IntPtr data);
}

internal sealed class SettingsForm : Form
{
    private readonly DataGridView cameras = new()
    {
        Dock = DockStyle.Fill, AllowUserToAddRows = true, AllowUserToDeleteRows = true,
        AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
        RowHeadersVisible = false, SelectionMode = DataGridViewSelectionMode.FullRowSelect
    };
    private readonly CheckBox top = new() { Text = "Immer im Vordergrund", AutoSize = true };
    private readonly CheckBox autostart = new() { Text = "Mit Windows starten", AutoSize = true };
    public Settings Result { get; private set; }

    public SettingsForm(Settings current)
    {
        Result = current;
        Text = "HomeCam Monitor – Einstellungen";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MaximizeBox = false;
        MinimizeBox = false;
        ClientSize = new Size(760, 390);

        cameras.Columns.Add(new DataGridViewTextBoxColumn { Name = "CameraName", HeaderText = "Name", FillWeight = 25 });
        cameras.Columns.Add(new DataGridViewTextBoxColumn { Name = "StreamUrl", HeaderText = "RTSP-/HTTP-Streamadresse", FillWeight = 75 });
        foreach (var camera in current.Cameras)
            cameras.Rows.Add(camera.Name, camera.StreamUrl);
        top.Checked = current.AlwaysOnTop; autostart.Checked = current.StartWithWindows;

        var table = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(14), ColumnCount = 1, RowCount = 5 };
        table.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        table.Controls.Add(cameras, 0, 0);
        var hint = new Label
        {
            Text = "Pro Kamera einen Namen und die vollständige RTSP-, HTTP- oder HTTPS-Adresse eintragen.",
            AutoSize = true,
            ForeColor = SystemColors.GrayText
        };
        table.Controls.Add(hint, 0, 1);
        var options = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true };
        options.Controls.Add(top); options.Controls.Add(autostart);
        table.Controls.Add(options, 0, 2);
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
        var ok = new Button { Text = "Speichern", DialogResult = DialogResult.OK, AutoSize = true };
        var cancel = new Button { Text = "Abbrechen", DialogResult = DialogResult.Cancel, AutoSize = true };
        buttons.Controls.Add(ok); buttons.Controls.Add(cancel);
        table.Controls.Add(buttons, 0, 4);
        Controls.Add(table);
        AcceptButton = ok; CancelButton = cancel;
        ok.Click += (_, e) =>
        {
            var entries = ReadCameras();
            if (entries.Count == 0 || entries.Any(c => !IsSupportedUrl(c.StreamUrl)))
            {
                MessageBox.Show(this, "Bitte für jede Kamera einen Namen und eine vollständige RTSP-, HTTP- oder HTTPS-Streamadresse eintragen.", "Ungültige Kamera", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                DialogResult = DialogResult.None;
                return;
            }
            SaveResult(current, entries);
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
            if (name.Length > 0 || url.Length > 0) result.Add(new CameraEntry { Name = name, StreamUrl = url });
        }
        return result;
    }

    private static bool IsSupportedUrl(string value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri)
        && (uri.Scheme.Equals("rtsp", StringComparison.OrdinalIgnoreCase)
            || uri.Scheme.Equals("http", StringComparison.OrdinalIgnoreCase)
            || uri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase))
        && !value.Contains("PASSWORT", StringComparison.OrdinalIgnoreCase);

    private void SaveResult(Settings old, List<CameraEntry> entries)
    {
        Result = new Settings
        {
            Cameras = entries, SelectedCamera = Math.Clamp(old.SelectedCamera, 0, entries.Count - 1), AlwaysOnTop = top.Checked,
            StartWithWindows = autostart.Checked, Left = old.Left, Top = old.Top,
            Width = old.Width, Height = old.Height
        };
    }
}
