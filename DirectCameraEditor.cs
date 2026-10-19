#if V2_BETA
using System.Diagnostics;
namespace HomeCamMonitor;

internal sealed class DirectCameraEditor : GroupBox
{
    private readonly DataGridView cameras;
    private readonly CheckBox enabled = new() { Name = "UseDirectCamera", Text = UiText.T("Für ausgewählte Kamera verwenden"), AutoSize = true };
    private readonly TextBox address = new() { Name = "OnvifAddress", Dock = DockStyle.Fill, PlaceholderText = UiText.T("http://Kamera-IP:Port/onvif/device_service") };
    private readonly TextBox username = new() { Name = "DirectUsername", Dock = DockStyle.Fill };
    private readonly TextBox password = new() { Name = "DirectPassword", Dock = DockStyle.Fill, UseSystemPasswordChar = true };
    private readonly TextBox stream = new() { Name = "DirectRtspUrl", Dock = DockStyle.Fill, PlaceholderText = UiText.T("rtsp://Kamera-IP:554/Stream-Pfad") };
    private readonly ComboBox profiles = new DarkComboBox { Name = "OnvifProfiles", Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly Label status = new() { Name = "DirectConnectionStatus", AutoSize = true, MaximumSize = new Size(650, 0) };
    private readonly Button find = new() { Name = "ReadOnvifProfiles", Text = UiText.T("ONVIF-Profile laden"), AutoSize = true };
    private readonly Button test = new() { Name = "TestDirectStream", Text = UiText.T("Stream testen"), AutoSize = true };
    private readonly CancellationTokenSource closed = new();
    private bool loading;
    private DataGridViewRow? selected;
    internal event Action? ActivateRequested;
    internal event Action? AddRequested;
    internal event Action? ConfigurationChanged;
    internal event Action? SearchRequested;
    internal DirectCameraEditor(DataGridView cameras)
    {
        this.cameras = cameras;
        Name = "DirectCameraConnection"; Text = UiText.T("ONVIF / RTSP – direkte Netzwerkverbindung");
        Dock = DockStyle.Top; AutoSize = true; Padding = new Padding(10, 18, 10, 10);
        var fields = new SettingsLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2 };
        fields.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        var actions = new SettingsFlowPanel { Dock = DockStyle.Top, AutoSize = true, WrapContents = true };
        var add = new Button { Name = "AddDirectCamera", Text = UiText.T("Netzwerkkamera hinzufügen"), AutoSize = true };
        var search = new Button { Name = "SearchNetworkCameras", Text = UiText.T("Kameras im Netzwerk suchen"), AutoSize = true };
        search.Click += (_, _) => SearchRequested?.Invoke();
        actions.Controls.AddRange([search, add, enabled]); fields.Controls.Add(actions, 0, 0); fields.SetColumnSpan(actions, 2);
        void Field(string label, Control control, int row) { fields.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left }, 0, row); fields.Controls.Add(control, 1, row); }
        Field(UiText.T("ONVIF-Adresse:"), address, 1); Field(UiText.T("Kamera-Benutzer:"), username, 2); Field(UiText.T("Kamera-Passwort:"), password, 3);
        Field(UiText.T("ONVIF-Profil:"), profiles, 4); Field(UiText.T("RTSP-Adresse:"), stream, 5);
        var buttons = new SettingsFlowPanel { Dock = DockStyle.Top, AutoSize = true, WrapContents = true };
        buttons.Controls.AddRange([find, test]); fields.Controls.Add(buttons, 1, 6);
        fields.Controls.Add(status, 1, 7);
        var hint = new Label { AutoSize = true, MaximumSize = new Size(700, 0), Margin = new Padding(3, 8, 3, 4),
            Text = UiText.T("Die Kamera muss vom PC erreichbar sein. ONVIF und RTSP müssen an der Kamera freigegeben sein. ") +
                UiText.T("ONVIF liest die verfügbaren Bildprofile; alternativ die RTSP-Adresse direkt eintragen. ") +
                UiText.T("Ein leeres Passwortfeld behält das gespeicherte Passwort. Diese Verbindungsart überträgt derzeit das Bild, keine Bewegungs- oder Personenereignisse.") };
        fields.Controls.Add(hint, 0, 8); fields.SetColumnSpan(hint, 2); Controls.Add(fields);
        add.Click += (_, _) => AddRequested?.Invoke();
        enabled.CheckedChanged += (_, _) =>
        {
            if (loading) return;
            if (enabled.Checked) ActivateRequested?.Invoke();
            else if (cameras.CurrentRow is { IsNewRow: false } row && DirectCamera.ForRow(row).Enabled)
            { loading = true; enabled.Checked = true; loading = false; }
        };
        foreach (var field in new[] { address, username, password, stream }) field.TextChanged += (_, _) => Store();
        profiles.SelectedIndexChanged += (_, _) =>
        {
            if (loading || cameras.CurrentRow is not { IsNewRow: false } row || profiles.SelectedItem is not OnvifProfile profile) return;
            DirectCamera.ForRow(row).ProfileToken = profile.Token; stream.Text = profile.StreamUrl; Store();
        };
        find.Click += async (_, _) => await ReadProfiles();
        test.Click += async (_, _) => await TestStream();
        cameras.CurrentCellChanged += (_, _) => LoadSelection();
        cameras.SelectionChanged += (_, _) => LoadSelection();
        LoadSelection();
    }
    internal void Store()
    {
        if (loading || cameras.CurrentRow is not { IsNewRow: false } row) return;
        var config = DirectCamera.ForRow(row);
        config.DeviceUrl = address.Text.Trim(); config.Username = username.Text.Trim(); config.RtspUrl = stream.Text.Trim();
        if (password.Text.Length > 0) config.ProtectedPassword = CameraPassword.Protect(password.Text);
        if (config.Enabled) row.Cells["StreamUrl"].Value = config.StreamUrlOrEmpty();
        ConfigurationChanged?.Invoke();
    }
    internal void LoadSelection()
    {
        if (loading || CameraGridUpdate.IsActive(cameras)) return;
        loading = true;
        try
        {
            var row = cameras.CurrentRow is { IsNewRow: false } current ? current : null;
            var config = row is null ? new DirectCamera() : DirectCamera.ForRow(row);
            enabled.Enabled = row is not null; enabled.Checked = row is not null && config.Enabled;
            if (ReferenceEquals(row, selected) && row is not null) return;
            selected = row;
            address.Text = config.DeviceUrl; username.Text = config.Username; password.Text = "";
            password.PlaceholderText = config.ProtectedPassword.Length > 0 ? UiText.T("Gespeichert – zum Ändern eingeben") : UiText.T("Kamera-Passwort");
            stream.Text = config.RtspUrl; profiles.Items.Clear();
            if (config.ProfileToken.Length > 0) { profiles.Items.Add(new OnvifProfile(config.ProfileToken, config.ProfileToken, config.RtspUrl)); profiles.SelectedIndex = 0; }
            status.Text = row is null ? UiText.T("Keine Kamera ausgewählt") : config.Enabled ? UiText.T("Direkte Bildverbindung ausgewählt") : UiText.T("Direkte Verbindung nicht aktiviert");
            foreach (var control in new Control[] { address, username, password, stream, profiles, find, test }) control.Enabled = row is not null;
        }
        finally { loading = false; }
    }
    private async Task ReadProfiles()
    {
        Store();
        if (cameras.CurrentRow is not { IsNewRow: false } row) return;
        var config = DirectCamera.ForRow(row).Clone();
        find.Enabled = false; status.Text = UiText.T("ONVIF-Profile werden gelesen …");
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(closed.Token); timeout.CancelAfter(TimeSpan.FromSeconds(60));
            using var client = new OnvifClient(config.Username, config.ProtectedPassword.Length == 0 ? "" : CameraPassword.Unprotect(config.ProtectedPassword));
            var values = await client.Profiles(config.DeviceUrl, timeout.Token);
            if (IsDisposed || !ReferenceEquals(cameras.CurrentRow, row)) return;
            var current = DirectCamera.ForRow(row);
            if (current.DeviceUrl != config.DeviceUrl || current.Username != config.Username || current.ProtectedPassword != config.ProtectedPassword)
            { status.Text = UiText.T("Verbindungsdaten wurden geändert. ONVIF-Profile bitte erneut laden."); return; }
            loading = true;
            try { profiles.Items.Clear(); foreach (var value in values) profiles.Items.Add(value); }
            finally { loading = false; }
            profiles.SelectedIndex = 0;
            status.Text = UiText.F("{0} ONVIF-Profile gefunden. Profil auswählen und direkte Verbindung aktivieren.", values.Count);
        }
        catch (OperationCanceledException) { if (!IsDisposed && ReferenceEquals(cameras.CurrentRow, row)) status.Text = UiText.T("ONVIF-Anfrage beendet oder Zeitlimit erreicht."); }
        catch (Exception e) { if (!IsDisposed && ReferenceEquals(cameras.CurrentRow, row)) status.Text = e is ArgumentException or InvalidDataException ? e.Message : UiText.T("ONVIF-Verbindung fehlgeschlagen. Adresse, Port, Freigabe und Zugangsdaten prüfen."); }
        finally { if (!IsDisposed) find.Enabled = cameras.CurrentRow is { IsNewRow: false }; }
    }
    private async Task TestStream()
    {
        Store();
        if (cameras.CurrentRow is not { IsNewRow: false } row) return;
        test.Enabled = false; status.Text = UiText.T("Bild wird geprüft …");
        try
        {
            var url = DirectCamera.ForRow(row).StreamUrl();
            var received = await CameraStreamTest.DecodeFrame(url, closed.Token);
            if (!IsDisposed && ReferenceEquals(cameras.CurrentRow, row)) status.Text = received ? UiText.T("Bild empfangen – RTSP-Verbindung funktioniert.") : UiText.T("Kein Bild empfangen. Streamadresse, RTSP-Freigabe und Kamera-Zugangsdaten prüfen.");
        }
        catch (Exception) { if (!IsDisposed && ReferenceEquals(cameras.CurrentRow, row)) status.Text = UiText.T("Kein Bild empfangen oder Zeitlimit erreicht. Adresse und Zugangsdaten prüfen."); }
        finally { if (!IsDisposed) test.Enabled = cameras.CurrentRow is { IsNewRow: false }; }
    }
    protected override void Dispose(bool disposing) { if (disposing) { closed.Cancel(); closed.Dispose(); } base.Dispose(disposing); }
}
#endif
