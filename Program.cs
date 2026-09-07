using System.Runtime.InteropServices;
using System.Text.Json;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using Microsoft.Win32;

namespace HomeCamMonitor;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new MonitorForm());
    }
}

internal sealed class Settings
{
    public List<CameraEntry> Cameras { get; set; } =
    [
        new() { Name = "Einfahrt", StreamUrl = "rtsp://192.168.9.8:8554/Einfahrt" },
        new() { Name = "Garten", StreamUrl = "rtsp://192.168.9.8:8554/Garten" }
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
    public static void Save(Settings value)
    {
        Directory.CreateDirectory(Folder);
        File.WriteAllText(FileName, JsonSerializer.Serialize(value, JsonOptions));
    }
}

internal sealed class MonitorForm : Form
{
    private readonly WebView2 browser = new() { Dock = DockStyle.Fill, DefaultBackgroundColor = Color.Black };
    private Settings settings;
    private bool browserReady;
    private bool fullscreen;
    private bool closing;
    private Rectangle windowedBounds;
    private DateTime reloadAllowed = DateTime.MinValue;
    private bool pictureInPicture;
    private bool restoringPictureInPicture;

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
        Controls.Add(browser);
        Shown += async (_, _) => await InitializeAsync();
        Resize += (_, _) => ApplyRoundedCorners();
        FormClosing += (_, _) => { closing = true; SaveWindow(); };
        ApplyRoundedCorners();
    }

    private async Task InitializeAsync()
    {
        try
        {
            await browser.EnsureCoreWebView2Async();
            browser.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
            browser.CoreWebView2.Settings.AreDevToolsEnabled = false;
            browser.CoreWebView2.Settings.IsZoomControlEnabled = false;
            browser.CoreWebView2.Settings.IsStatusBarEnabled = false;
            browser.CoreWebView2.WebMessageReceived += BrowserMessageReceived;
            browser.CoreWebView2.NavigationCompleted += async (_, _) => await RestorePictureInPictureAsync();
            await browser.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(PlayerScript);
            browserReady = true;
            if (!HasUsableCamera()) OpenSettings();
            if (HasUsableCamera()) NavigateToSelectedCamera();
        }
        catch (Exception exception)
        {
            MessageBox.Show(this, $"Der WebRTC-Player konnte nicht gestartet werden.\n\n{exception.Message}",
                "HomeCam Monitor", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void NavigateToSelectedCamera()
    {
        if (!browserReady || !HasUsableCamera()) return;
        var camera = settings.Cameras[settings.SelectedCamera];
        Text = $"HomeCam Monitor – {camera.Name}";
        browser.CoreWebView2.Navigate(CreateViewerUrl(camera));
    }

    private static string CreateViewerUrl(CameraEntry camera)
    {
        var source = new Uri(camera.StreamUrl);
        if (source.Scheme is "http" or "https" && source.AbsolutePath.EndsWith("stream.html", StringComparison.OrdinalIgnoreCase))
            return camera.StreamUrl;
        var streamName = Uri.UnescapeDataString(source.AbsolutePath.Trim('/'));
        if (streamName.Length == 0) throw new InvalidOperationException("In der Streamadresse fehlt der go2rtc-Streamname.");
        return $"http://{source.Host}:1984/stream.html?src={Uri.EscapeDataString(streamName)}&mode=mse&background=true&title={Uri.EscapeDataString(camera.Name)}";
    }

    private void BrowserMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs eventArgs)
    {
        var action = eventArgs.TryGetWebMessageAsString();
        BeginInvoke(new Action(() => HandleAction(action)));
    }

    private void HandleAction(string action)
    {
        if (closing) return;
        if (HandlePointerAction(action)) return;
        switch (action)
        {
            case "previous": SelectRelativeCamera(-1); break;
            case "next": SelectRelativeCamera(1); break;
            case "snapshot": _ = SaveSnapshotAsync(); break;
            case "settings": OpenSettings(); break;
            case "close": Close(); break;
            case "fullscreen": ToggleFullscreen(); break;
            case "pipEntered": EnterPictureInPicture(); break;
            case "pipLeft": LeavePictureInPicture(); break;
            case "stalled": ReloadStream(); break;
        }
    }

    private void EnterPictureInPicture()
    {
        pictureInPicture = true;
        restoringPictureInPicture = false;
        SaveWindow();
        ShowInTaskbar = false;
        Hide();
    }

    private void LeavePictureInPicture()
    {
        if (closing || !pictureInPicture || restoringPictureInPicture) return;
        pictureInPicture = false;
        ShowInTaskbar = true;
        Show();
        Activate();
    }

    private bool HandlePointerAction(string action)
    {
        var parts = action.Split('|');
        if (parts.Length != 3 || !int.TryParse(parts[1], out var screenX) || !int.TryParse(parts[2], out var screenY))
            return false;

        switch (parts[0])
        {
            case "moveStart":
                BeginNativeWindowOperation(NativeMethods.ScMove + NativeMethods.HtCaption);
                return true;
            case "resizeStart":
                BeginNativeWindowOperation(NativeMethods.ScSize + NativeMethods.WmszBottomRight);
                return true;
            case "moveTo":
            case "resizeTo":
            case "moveEnd":
            case "resizeEnd":
                return true;
            default:
                return false;
        }
    }

    private void BeginNativeWindowOperation(int command)
    {
        if (fullscreen) return;
        NativeMethods.ReleaseCapture();
        NativeMethods.SendMessage(Handle, NativeMethods.WmSysCommand, (IntPtr)command, IntPtr.Zero);
    }

    private void SelectRelativeCamera(int direction)
    {
        if (settings.Cameras.Count < 2) return;
        settings.SelectedCamera = (settings.SelectedCamera + direction + settings.Cameras.Count) % settings.Cameras.Count;
        SettingsStore.Save(settings);
        NavigateToSelectedCamera();
    }

    private void ReloadStream()
    {
        if (!browserReady || DateTime.UtcNow < reloadAllowed) return;
        reloadAllowed = DateTime.UtcNow.AddSeconds(8);
        restoringPictureInPicture = pictureInPicture;
        browser.CoreWebView2.Reload();
    }

    private async Task RestorePictureInPictureAsync()
    {
        if (!restoringPictureInPicture || closing) return;

        try
        {
            for (var attempt = 0; attempt < 40; attempt++)
            {
                var ready = await browser.ExecuteScriptAsync("Boolean(document.querySelector('video')?.readyState >= 1)");
                if (string.Equals(ready, "true", StringComparison.OrdinalIgnoreCase)) break;
                await Task.Delay(250);
            }

            var parameters = JsonSerializer.Serialize(new
            {
                expression = "document.querySelector('video').requestPictureInPicture()",
                userGesture = true,
                awaitPromise = true
            });
            await browser.CoreWebView2.CallDevToolsProtocolMethodAsync("Runtime.evaluate", parameters);
        }
        catch
        {
            restoringPictureInPicture = false;
            pictureInPicture = false;
            ShowInTaskbar = true;
            Show();
            Activate();
        }
    }

    private async Task SaveSnapshotAsync()
    {
        try
        {
            var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "HomeCam Monitor");
            Directory.CreateDirectory(folder);
            var cameraName = string.Concat(settings.Cameras[settings.SelectedCamera].Name
                .Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
            var fileName = $"{cameraName}_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.png";
            await browser.ExecuteScriptAsync("document.getElementById('hcm-controls').style.visibility='hidden'");
            await Task.Delay(80);
            await using (var stream = File.Create(Path.Combine(folder, fileName)))
                await browser.CoreWebView2.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png, stream);
            await browser.ExecuteScriptAsync("document.getElementById('hcm-controls').style.visibility='visible';window.hcmFlash('Gespeichert')");
        }
        catch (Exception exception)
        {
            MessageBox.Show(this, $"Der Snapshot konnte nicht gespeichert werden.\n\n{exception.Message}",
                "Snapshot fehlgeschlagen", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void OpenSettings()
    {
        using var dialog = new SettingsForm(settings);
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        settings = dialog.Result;
        settings.SelectedCamera = Math.Clamp(settings.SelectedCamera, 0, settings.Cameras.Count - 1);
        TopMost = settings.AlwaysOnTop;
        SettingsStore.Save(settings);
        ConfigureAutostart(settings.StartWithWindows);
        NavigateToSelectedCamera();
    }

    private bool HasUsableCamera() => settings.Cameras.Count > 0 && settings.SelectedCamera >= 0
        && settings.SelectedCamera < settings.Cameras.Count
        && Uri.TryCreate(settings.Cameras[settings.SelectedCamera].StreamUrl, UriKind.Absolute, out _);

    private static void ConfigureAutostart(bool enabled)
    {
        using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true);
        if (enabled) key?.SetValue("HomeCamMonitor", $"\"{Application.ExecutablePath}\"");
        else key?.DeleteValue("HomeCamMonitor", false);
    }

    private void ToggleFullscreen()
    {
        if (!fullscreen)
        {
            windowedBounds = Bounds; fullscreen = true; WindowState = FormWindowState.Normal;
            Bounds = Screen.FromControl(this).Bounds;
        }
        else { fullscreen = false; Bounds = windowedBounds; }
        ApplyRoundedCorners();
    }

    private void ApplyRoundedCorners()
    {
        Region?.Dispose();
        if (fullscreen)
        {
            Region = null;
            return;
        }

        var radius = Math.Max(12, DeviceDpi * 14 / 96);
        var regionHandle = NativeMethods.CreateRoundRectRgn(0, 0, Width + 1, Height + 1, radius, radius);
        Region = Region.FromHrgn(regionHandle);
        NativeMethods.DeleteObject(regionHandle);
    }

    private void SaveWindow()
    {
        var value = fullscreen ? windowedBounds : Bounds;
        settings.Left = value.Left; settings.Top = value.Top; settings.Width = value.Width; settings.Height = value.Height;
        SettingsStore.Save(settings);
    }

    protected override void WndProc(ref Message message)
    {
        base.WndProc(ref message);
        if (message.Msg != NativeMethods.WmNcHitTest || fullscreen || WindowState != FormWindowState.Normal) return;

        var cursor = PointToClient(Cursor.Position);
        var grip = Math.Max(8, DeviceDpi * 8 / 96);
        var left = cursor.X < grip;
        var right = cursor.X >= ClientSize.Width - grip;
        var top = cursor.Y < grip;
        var bottom = cursor.Y >= ClientSize.Height - grip;

        if (left && top) message.Result = (IntPtr)NativeMethods.HtTopLeft;
        else if (right && top) message.Result = (IntPtr)NativeMethods.HtTopRight;
        else if (left && bottom) message.Result = (IntPtr)NativeMethods.HtBottomLeft;
        else if (right && bottom) message.Result = (IntPtr)NativeMethods.HtBottomRight;
        else if (left) message.Result = (IntPtr)NativeMethods.HtLeft;
        else if (right) message.Result = (IntPtr)NativeMethods.HtRight;
        else if (top) message.Result = (IntPtr)NativeMethods.HtTop;
        else if (bottom) message.Result = (IntPtr)NativeMethods.HtBottom;
    }

    private const string PlayerScript = """
      document.addEventListener('DOMContentLoaded',()=>{
        const s=document.createElement('style');s.textContent=`html,body{width:100%;height:100%;margin:0;overflow:hidden;background:#000}video-stream,video{width:100%!important;height:100%!important;max-width:none!important;object-fit:contain!important}video::-webkit-media-controls{display:none!important}#hcm-controls{position:fixed;z-index:2147483647;left:50%;bottom:10px;transform:translateX(-50%);display:flex;align-items:center;gap:2px;padding:4px 7px;border-radius:12px;background:rgba(0,0,0,.34);backdrop-filter:blur(4px);color:#fff;font:13px 'Segoe UI';user-select:none}#hcm-controls button{width:32px;height:30px;padding:0;border:0;border-radius:8px;background:transparent;color:#fff;font:18px 'Segoe Fluent Icons','Segoe MDL2 Assets';cursor:pointer}#hcm-controls button:hover{background:rgba(255,255,255,.18)}#hcm-name{min-width:58px;text-align:center;white-space:nowrap;padding:0 4px}#hcm-note{position:fixed;left:50%;bottom:58px;transform:translateX(-50%);padding:5px 10px;border-radius:8px;background:rgba(0,0,0,.55);color:#fff;font:13px 'Segoe UI';display:none}`;document.head.appendChild(s);
        const p=new URLSearchParams(location.search),bar=document.createElement('div');bar.id='hcm-controls';bar.innerHTML=`<button data-a="move" title="Verschieben">&#xE7C2;</button><button data-a="previous" title="Vorherige Kamera">&#xE76B;</button><span id="hcm-name"></span><button data-a="next" title="Nächste Kamera">&#xE76C;</button><button data-a="snapshot" title="Snapshot">&#xE722;</button><button data-a="settings" title="Einstellungen">&#xE713;</button><button data-a="pip" title="Bild im Bild">&#xE91B;</button><button data-a="close" title="Schließen">&#xE711;</button>`;bar.querySelector('#hcm-name').textContent=p.get('title')||p.get('src')||'Kamera';document.body.appendChild(bar);
        const note=document.createElement('div');note.id='hcm-note';document.body.appendChild(note);let operation=null,pointerId=0;
        bar.addEventListener('pointerdown',async e=>{const b=e.target.closest('button');if(!b)return;e.preventDefault();const a=b.dataset.a;if(a==='pip'){const v=document.querySelector('video');if(v&&document.pictureInPictureEnabled)await v.requestPictureInPicture();return}if(a==='move'){operation=a;pointerId=e.pointerId;b.setPointerCapture(e.pointerId);chrome.webview.postMessage(`${a}Start|${Math.round(e.screenX)}|${Math.round(e.screenY)}`)}else chrome.webview.postMessage(a)});
        bar.addEventListener('pointermove',e=>{if(operation&&e.pointerId===pointerId)chrome.webview.postMessage(`${operation}To|${Math.round(e.screenX)}|${Math.round(e.screenY)}`)});
        const end=e=>{if(operation&&e.pointerId===pointerId){chrome.webview.postMessage(`${operation}End|${Math.round(e.screenX)}|${Math.round(e.screenY)}`);operation=null}};bar.addEventListener('pointerup',end);bar.addEventListener('pointercancel',end);
        document.addEventListener('dblclick',e=>{if(!e.target.closest('#hcm-controls'))chrome.webview.postMessage('fullscreen')});window.hcmFlash=t=>{note.textContent=t;note.style.display='block';setTimeout(()=>note.style.display='none',1800)};
        document.addEventListener('enterpictureinpicture',()=>chrome.webview.postMessage('pipEntered'));
        document.addEventListener('leavepictureinpicture',()=>chrome.webview.postMessage('pipLeft'));
        let lt=-1,lp=Date.now(),sent=false;setInterval(()=>{const v=document.querySelector('video');if(v&&v.readyState>=2&&v.currentTime>lt){lt=v.currentTime;lp=Date.now();sent=false}else if(!sent&&Date.now()-lp>12000){sent=true;chrome.webview.postMessage('stalled')}},2000);
      });
      """;
}

internal static class NativeMethods
{
    public const int WmNcHitTest = 0x0084;
    public const int WmSysCommand = 0x0112;
    public const int ScMove = 0xF010;
    public const int ScSize = 0xF000;
    public const int HtCaption = 2;
    public const int WmszBottomRight = 8;
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

    [DllImport("gdi32.dll")]
    public static extern IntPtr CreateRoundRectRgn(int left, int top, int right, int bottom, int width, int height);

    [DllImport("gdi32.dll")]
    public static extern bool DeleteObject(IntPtr handle);
}

internal sealed class SettingsForm : Form
{
    private readonly DataGridView cameras = new() { Dock = DockStyle.Fill, AllowUserToAddRows = true, AllowUserToDeleteRows = true, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, RowHeadersVisible = false, SelectionMode = DataGridViewSelectionMode.FullRowSelect };
    private readonly CheckBox top = new() { Text = "Immer im Vordergrund", AutoSize = true };
    private readonly CheckBox autostart = new() { Text = "Mit Windows starten", AutoSize = true };
    public Settings Result { get; private set; }

    public SettingsForm(Settings current)
    {
        Result = current; Text = "HomeCam Monitor – Einstellungen"; FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent; MaximizeBox = false; MinimizeBox = false; ClientSize = new Size(760, 390);
        cameras.Columns.Add(new DataGridViewTextBoxColumn { Name = "CameraName", HeaderText = "Name", FillWeight = 25 });
        cameras.Columns.Add(new DataGridViewTextBoxColumn { Name = "StreamUrl", HeaderText = "go2rtc-Streamadresse", FillWeight = 75 });
        foreach (var camera in current.Cameras) cameras.Rows.Add(camera.Name, camera.StreamUrl);
        top.Checked = current.AlwaysOnTop; autostart.Checked = current.StartWithWindows;
        var table = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(14), ColumnCount = 1, RowCount = 5 };
        table.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); table.Controls.Add(cameras, 0, 0);
        table.Controls.Add(new Label { Text = "Beispiel: rtsp://192.168.9.8:8554/Einfahrt", AutoSize = true, ForeColor = SystemColors.GrayText }, 0, 1);
        var options = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true }; options.Controls.Add(top); options.Controls.Add(autostart); table.Controls.Add(options, 0, 2);
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
        var ok = new Button { Text = "Speichern", DialogResult = DialogResult.OK, AutoSize = true };
        buttons.Controls.Add(ok); buttons.Controls.Add(new Button { Text = "Abbrechen", DialogResult = DialogResult.Cancel, AutoSize = true }); table.Controls.Add(buttons, 0, 4);
        Controls.Add(table); AcceptButton = ok; CancelButton = buttons.Controls[1] as Button;
        ok.Click += (_, _) => { var entries = ReadCameras(); if (entries.Count == 0 || entries.Any(c => !Uri.TryCreate(c.StreamUrl, UriKind.Absolute, out _))) { MessageBox.Show(this, "Bitte gültige go2rtc-Streamadressen eintragen.", "Ungültige Kamera", MessageBoxButtons.OK, MessageBoxIcon.Warning); DialogResult = DialogResult.None; return; } Result = new Settings { Cameras = entries, SelectedCamera = Math.Clamp(current.SelectedCamera, 0, entries.Count - 1), AlwaysOnTop = top.Checked, StartWithWindows = autostart.Checked, Left = current.Left, Top = current.Top, Width = current.Width, Height = current.Height }; };
    }

    private List<CameraEntry> ReadCameras()
    {
        var result = new List<CameraEntry>(); foreach (DataGridViewRow row in cameras.Rows) { if (row.IsNewRow) continue; var name = Convert.ToString(row.Cells[0].Value)?.Trim() ?? ""; var url = Convert.ToString(row.Cells[1].Value)?.Trim() ?? ""; if (name.Length > 0 || url.Length > 0) result.Add(new CameraEntry { Name = name, StreamUrl = url }); } return result;
    }
}
