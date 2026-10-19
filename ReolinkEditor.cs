#if V2_BETA
namespace HomeCamMonitor;

internal sealed class ReolinkEditor : GroupBox
{
    private readonly DataGridView cameras;
    private DataGridViewRow? selected;
    private readonly CheckBox enabled = new() { Text = UiText.T("Direkt über Reolink-UID verbinden"), AutoSize = true };
    private readonly TextBox uid = new() { Dock = DockStyle.Fill };
    private readonly TextBox username = new() { Dock = DockStyle.Fill };
    private readonly TextBox password = new() { Dock = DockStyle.Fill, UseSystemPasswordChar = true };
    private readonly ComboBox stream = new DarkComboBox() { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly Label status = new() { AutoSize = true };
    private readonly Button test = new() { Text = UiText.T("Verbindung testen"), AutoSize = true };
    private readonly TextBox helper = new() { Dock = DockStyle.Fill };
    private readonly System.Windows.Forms.Timer timer = new() { Interval = 1000 };
    private bool loading;
    private ReolinkBridge? testBridge;
    private readonly CancellationTokenSource closed = new();
    internal string HelperPath => helper.Text.Trim();
    internal event Action? Activated;
    internal event Action? ConnectionChanged;
    internal void UseHomeAssistant() { Store(); enabled.Checked = false; Store(); ConnectionChanged?.Invoke(); }
    internal void SelectNonUid(bool direct, string streamUrl)
    {
        Store();
        if (selected is null) return;
        loading = true;
        try
        {
            enabled.Checked = false;
            var config = selected.Tag as ReolinkCamera ?? new ReolinkCamera();
            config.Enabled = false; selected.Tag = config;
            DirectCamera.ForRow(selected).Enabled = direct;
            selected.Cells["StreamUrl"].ReadOnly = direct;
            selected.Cells["StreamUrl"].Value = streamUrl;
            SetEnabled(); UpdateStatus();
        }
        finally { loading = false; }
        ConnectionChanged?.Invoke();
    }
    internal void Reorder(Action action)
    {
        Store(); loading = true;
        try { action(); }
        finally { loading = false; }
        SelectCamera();
        ConnectionChanged?.Invoke();
    }
    internal ReolinkEditor(DataGridView cameras, Settings settings)
    {
        this.cameras = cameras;
        cameras.Columns["StreamUrl"].HeaderText = "Stream / Reolink";
        cameras.CellFormatting += (_, e) =>
        {
            if (e.RowIndex >= 0 && e.ColumnIndex == cameras.Columns["StreamUrl"].Index &&
                cameras.Rows[e.RowIndex].Tag is ReolinkCamera { Enabled: true } config)
            {
                e.Value = config.Stream == "mainStream" ? UiText.T("Reolink – hohe Auflösung") : UiText.T("Reolink – flüssig");
                e.FormattingApplied = true;
            }
        };
        Name = "ReolinkConnection";
        Text = UiText.T("Reolink – direkte UID-Verbindung");
        Dock = DockStyle.Top; AutoSize = true; AutoSizeMode = AutoSizeMode.GrowAndShrink; Padding = new Padding(10, 18, 10, 10);
        for (var i = 0; i < settings.Cameras.Count; i++)
            cameras.Rows[i].Tag = settings.Cameras[i].Reolink.Clone();
        helper.Text = settings.ReolinkHelperPath;
        stream.Items.AddRange([UiText.T("Flüssig (Substream)"), UiText.T("Hohe Auflösung (Hauptstream)")]);
        var fields = new SettingsLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2 };
        fields.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        fields.Controls.Add(enabled, 0, 0); fields.SetColumnSpan(enabled, 2);
        var addCamera = new Button { Name = "AddReolinkCamera", Text = UiText.T("Reolink-Kamera hinzufügen"), AutoSize = true };
        var connectionOptions = new SettingsFlowPanel { Dock = DockStyle.Top, AutoSize = true, WrapContents = true };
        fields.Controls.Remove(enabled);
        connectionOptions.Controls.Add(addCamera); connectionOptions.Controls.Add(enabled);
        fields.Controls.Add(connectionOptions, 0, 0); fields.SetColumnSpan(connectionOptions, 2);
        addCamera.Click += (_, _) => AddCamera();
        void Field(string label, Control control, int row)
        {
            fields.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left }, 0, row);
            fields.Controls.Add(control, 1, row);
        }
        Field(UiText.T("Kamera-UID:"), uid, 1);
        Field(UiText.T("Kamera-Benutzername:"), username, 2);
        Field(UiText.T("Kamera-Passwort:"), password, 3);
        Field(UiText.T("Bildqualität:"), stream, 4);
        Field(UiText.T("Verbindung:"), status, 5);
        var statusRow = new SettingsFlowPanel { Dock = DockStyle.Top, AutoSize = true, WrapContents = true };
        fields.Controls.Remove(status);
        statusRow.Controls.Add(test); statusRow.Controls.Add(status);
        fields.Controls.Add(statusRow, 1, 5);
        test.Click += async (_, _) =>
        {
            Store();
            if (selected?.Tag is not ReolinkCamera config || !config.Enabled) return;
            try { config.Validate(); }
            catch (ArgumentException error) { status.Text = error.Message; return; }
            test.Enabled = false; timer.Stop();
            var entry = new CameraEntry { Name = UiText.T("Verbindungstest"), Reolink = config.Clone() };
            var done = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            using var bridge = new ReolinkBridge([entry], HelperPath);
            testBridge = bridge;
            bridge.Status += (_, text) =>
            {
                if (!IsDisposed && IsHandleCreated)
                    BeginInvoke(new Action(() => { if (!IsDisposed && testBridge == bridge) status.Text = text; }));
                if (text == UiText.T("Verbunden") || ReolinkDiagnostics.IsFailure(text) || text.Contains("fehlt", StringComparison.Ordinal) || text.Contains("konnte nicht", StringComparison.Ordinal) || text.Contains("prüfen", StringComparison.Ordinal))
                    done.TrySetResult(text);
            };
            status.Text = UiText.T("Verbindung wird geprüft …");
            bridge.Start();
            try { status.Text = await done.Task.WaitAsync(TimeSpan.FromSeconds(120), closed.Token); }
            catch (TimeoutException) { status.Text = UiText.T("Zeitlimit nach 120 s. Letzter Schritt: ") + status.Text; }
            catch (OperationCanceledException) { }
            finally
            {
                bridge.Dispose(); testBridge = null;
                await bridge.Completion;
                if (!IsDisposed) SetEnabled();
            }
            // Keep the test result visible until another camera is selected.
        };
        var browse = new Button { Text = UiText.T("Auswählen …"), AutoSize = true };
        var helperRow = new SettingsLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2 };
        helperRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        helperRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        helperRow.Controls.Add(helper, 0, 0); helperRow.Controls.Add(browse, 1, 0);
        Field(UiText.T("Verbindungsdienst:"), helperRow, 6);
        browse.Click += (_, _) =>
        {
            using var dialog = new OpenFileDialog { Filter = "Neolink (neolink.exe)|neolink.exe", CheckFileExists = true };
            if (dialog.ShowDialog(this) == DialogResult.OK) helper.Text = dialog.FileName;
        };
        var hint = new Label { AutoSize = true, MaximumSize = new Size(700, 0), Margin = new Padding(3, 8, 3, 4),
            Text = UiText.T("UID: Reolink-App → Geräteinformationen. Benutzer und Passwort gehören zur Kamera. ") +
                UiText.T("Nach Speichern startet die Verbindung automatisch. Ein leeres Passwortfeld behält das gespeicherte Passwort. ") +
                UiText.T("Über UID wird allgemeine Bewegung übertragen. Personenereignisse sind über Home Assistant mit geeigneter Entität möglich. Fahrzeugfilter sind nicht enthalten.") };
        fields.Controls.Add(hint, 0, 7); fields.SetColumnSpan(hint, 2);
        Controls.Add(fields);
        cameras.SelectionChanged += (_, _) => SelectCamera();
        cameras.CurrentCellChanged += (_, _) => SelectCamera();
        cameras.RowsAdded += (_, _) => SelectCamera();
        cameras.CellValueChanged += (_, e) => { if (!loading && !CameraGridUpdate.IsActive(cameras) && e.ColumnIndex == cameras.Columns["Active"].Index) { SetEnabled(); UpdateStatus(); } };
        enabled.CheckedChanged += (_, _) =>
        {
            if (loading) return;
            if (enabled.Checked && selected is null) { AddCamera(); return; }
            SetEnabled();
            if (enabled.Checked && selected is not null)
            {
                if (string.IsNullOrWhiteSpace(Convert.ToString(selected.Cells["CameraName"].Value)))
                    selected.Cells["CameraName"].Value = "Reolink " + (selected.Index + 1);
                selected.Cells["MotionEnabled"].Value = true;
                Store();
                Activated?.Invoke();
            }
            Store();
            ConnectionChanged?.Invoke();
        };
        timer.Tick += (_, _) => UpdateStatus();
        timer.Start();
        SelectCamera();
    }
    internal void Store()
    {
        if (loading) return;
        if (selected is null || selected.IsNewRow || selected.DataGridView != cameras) return;
        var config = selected.Tag as ReolinkCamera ?? new ReolinkCamera();
        if (!config.Enabled && enabled.Checked && !DirectCamera.ForRow(selected).Enabled && cameras.Columns.Contains("HomeAssistantStreamUrl"))
            selected.Cells["HomeAssistantStreamUrl"].Value = Convert.ToString(selected.Cells["StreamUrl"].Value) ?? "";
        if (enabled.Checked) DirectCamera.ForRow(selected).Enabled = false;
        config.Enabled = enabled.Checked;
        config.Uid = uid.Text.Trim(); config.Username = username.Text.Trim();
        config.Stream = stream.SelectedIndex == 1 ? "mainStream" : "subStream";
        if (password.Text.Length > 0) config.ProtectedPassword = CameraPassword.Protect(password.Text);
        selected.Tag = config;
        var cell = selected.Cells["StreamUrl"];
        cell.ReadOnly = config.Enabled || DirectCamera.ForRow(selected).Enabled;
        if (config.Enabled)
        {
            var port = Uri.TryCreate(Convert.ToString(cell.Value), UriKind.Absolute, out var uri) &&
                uri.Host == "127.0.0.1" && uri.AbsolutePath.Contains(config.MountName, StringComparison.Ordinal) ? uri.Port : 18554;
            cell.Value = config.StreamUrl(port);
        }
        else if (Convert.ToString(cell.Value)?.Contains(config.MountName, StringComparison.Ordinal) == true)
            cell.Value = Convert.ToString(selected.Cells["HomeAssistantStreamUrl"].Value) ?? "";
    }
    private void SelectCamera()
    {
        if (loading) return;
        var next = cameras.CurrentRow is { IsNewRow: false } row ? row : null;
        if (ReferenceEquals(next, selected) && selected is not null) return;
        Store();
        selected = next;
        var config = selected?.Tag as ReolinkCamera ?? new ReolinkCamera();
        loading = true;
        try
        {
            enabled.Enabled = true;
            enabled.Checked = selected is not null && config.Enabled;
            uid.Text = config.Uid; username.Text = config.Username; password.Text = "";
            password.PlaceholderText = config.ProtectedPassword.Length > 0 ? UiText.T("Gespeichert – zum Ändern eingeben") : UiText.T("Kamera-Passwort");
            stream.SelectedIndex = config.Stream == "mainStream" ? 1 : 0;
            if (selected is not null)
            {
                selected.Tag = config;
                selected.Cells["StreamUrl"].ReadOnly = config.Enabled || DirectCamera.ForRow(selected).Enabled;
            }
            SetEnabled(); UpdateStatus();
        }
        finally { loading = false; }
        if (testBridge is null) timer.Start();
    }
    private void AddCamera()
    {
        Store();
        loading = true;
        DataGridViewRow row;
        try
        {
            var names = cameras.Rows.Cast<DataGridViewRow>().Where(r => !r.IsNewRow)
                .Select(r => Convert.ToString(r.Cells["CameraName"].Value)).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var number = 1;
            while (names.Contains("Reolink " + number)) number++;
            var config = new ReolinkCamera { Enabled = true };
            var index = cameras.Rows.Add("Reolink " + number, config.StreamUrl(18554), true, false, "", "None", 30, "", true, "");
            row = cameras.Rows[index]; row.Tag = config;
            cameras.CurrentCell = row.Cells["CameraName"];
            row.Selected = true;
        }
        finally { loading = false; }
        selected = null;
        SelectCamera();
        Activated?.Invoke();
        uid.Focus();
    }
    private void SetEnabled()
    {
        foreach (var control in new Control[] { uid, username, password, stream }) control.Enabled = enabled.Checked && selected is not null;
        test.Enabled = enabled.Checked && selected is not null && Convert.ToBoolean(selected.Cells["Active"].Value ?? true);
    }
    private void UpdateStatus()
    {
        var text = selected is not null && !Convert.ToBoolean(selected.Cells["Active"].Value ?? true) ? UiText.T("Kamera deaktiviert") :
            selected?.Tag is ReolinkCamera { Enabled: true } config
            ? ReolinkStatus.Read(config.Id) : UiText.T("UID-Verbindung nicht aktiviert");
        if (status.Text != text) status.Text = text;
    }
    protected override void Dispose(bool disposing)
    {
        if (disposing) { closed.Cancel(); testBridge?.Dispose(); timer.Stop(); timer.Dispose(); }
        base.Dispose(disposing);
    }
}

internal static class ReolinkStatus
{
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, string> Values = new();
    internal static void Write(string id, string status) => Values[id] = status;
    internal static string Read(string id) => Values.GetValueOrDefault(id, UiText.T("Nach Speichern verbinden …"));
}

internal sealed partial class MonitorForm
{
    private ReolinkBridge? reolink;
    private string? reolinkConfiguration;
    private bool ConfigureReolink()
    {
        var bundled = Path.Combine(AppContext.BaseDirectory, "tools", "neolink", "neolink.exe");
        if (!File.Exists(settings.ReolinkHelperPath) && File.Exists(bundled)) settings.ReolinkHelperPath = bundled;
        var signature = System.Text.Json.JsonSerializer.Serialize(new
        {
            settings.ReolinkHelperPath,
            Cameras = settings.Cameras.Where(c => c.Active && c.Reolink.Enabled).Select(c => new { c.Name, c.MotionEnabled, c.Reolink }).ToArray()
        });
        if (reolink is not null && signature == reolinkConfiguration)
        {
            foreach (var camera in settings.Cameras.Where(c => c.Active && c.Reolink.Enabled))
                camera.StreamUrl = reolink.StreamUrl(camera);
            return false;
        }
        reolinkConfiguration = signature;
        reolink?.Dispose(); reolink = null;
        if (!settings.Cameras.Any(c => c.Active && c.Reolink.Enabled)) return true;
        ReolinkBridge bridge;
        try { bridge = new ReolinkBridge(settings.Cameras, settings.ReolinkHelperPath); }
        catch (ArgumentException)
        {
            foreach (var camera in settings.Cameras.Where(c => c.Active && c.Reolink.Enabled))
                ReolinkStatus.Write(camera.Reolink.Id, UiText.T("Kamera-Konfiguration prüfen"));
            return true;
        }
        reolink = bridge;
        bridge.StreamReady += camera =>
        {
            if (closing || !IsHandleCreated) return;
            try
            {
                BeginInvoke(new Action(() =>
                {
                    if (closing || !ReferenceEquals(reolink, bridge)) return;
                    if (gridMode || (HasUsableCamera() && settings.Cameras[settings.SelectedCamera].Reolink.Id == camera.Reolink.Id))
                        StartPlayer();
                }));
            }
            catch (InvalidOperationException) { }
        };
        bridge.Status += (camera, status) =>
        {
            if (ReferenceEquals(reolink, bridge)) ReolinkStatus.Write(camera.Reolink.Id, status);
        };
        bridge.Motion += camera =>
        {
            if (closing || !IsHandleCreated) return;
            try
            {
                BeginInvoke(new Action(() =>
                {
                    if (!closing && ReferenceEquals(reolink, bridge)) HandleMotion(camera.Name);
                }));
            }
            catch (InvalidOperationException) { }
        };
        bridge.Start();
        return true;
    }
}
#endif
