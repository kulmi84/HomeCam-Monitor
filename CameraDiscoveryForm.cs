#if V2_BETA
namespace HomeCamMonitor;

internal sealed class CameraDiscoveryForm : Form
{
    private readonly ComboBox networks = new DarkComboBox { Name = "DiscoveryNetwork", DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
    private readonly TextBox range = new() { Name = "DiscoveryRange", Dock = DockStyle.Fill };
    private readonly TextBox ports = new() { Name = "DiscoveryPorts", Text = "554,8554,10554", Dock = DockStyle.Fill };
    private readonly Button discover = new() { Name = "StartCameraDiscovery", Text = "Kameras im Netzwerk suchen", AutoSize = true };
    private readonly Button scan = new() { Name = "ScanRtspPorts", Text = "RTSP-Ports gezielt prüfen", AutoSize = true };
    private readonly Button cancel = new() { Name = "CancelCameraDiscovery", Text = "Suche abbrechen", AutoSize = true, Enabled = false };
    private readonly SettingsCameraGrid results = new() { Name = "DiscoveredCameras", Dock = DockStyle.Top, AllowUserToAddRows = false,
        AllowUserToDeleteRows = false, ReadOnly = true, RowHeadersVisible = false, MultiSelect = false, SelectionMode = DataGridViewSelectionMode.FullRowSelect,
        AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, ScrollBars = ScrollBars.None };
    private readonly TextBox device = new() { Name = "DiscoveredDeviceUrl", ReadOnly = true, Dock = DockStyle.Fill };
    private readonly TextBox username = new() { Name = "DiscoveryUsername", Text = "admin", Dock = DockStyle.Fill };
    private readonly TextBox password = new() { Name = "DiscoveryPassword", UseSystemPasswordChar = true, Dock = DockStyle.Fill };
    private readonly ComboBox profiles = new DarkComboBox { Name = "DiscoveredProfiles", DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
    private readonly TextBox stream = new() { Name = "DiscoveredStreamUrl", Dock = DockStyle.Fill, PlaceholderText = "RTSP-Pfad wählen oder eintragen; ein offener Port bestätigt noch keinen Stream." };
    private readonly Button load = new() { Name = "LoadDiscoveredProfiles", Text = "ONVIF-Profile laden", AutoSize = true };
    private readonly Button test = new() { Name = "TestDiscoveredStream", Text = "Verbindung testen", AutoSize = true };
    private readonly Button take = new() { Name = "ImportDiscoveredCamera", Text = "Kamera übernehmen", AutoSize = true, Enabled = false };
    private readonly Label status = new() { Name = "DiscoveryStatus", Text = "Netzwerkadapter und Bereich wählen. Es wird erst nach Klick gesucht.", AutoSize = true, MaximumSize = new Size(850, 0) };
    private readonly Dictionary<string, DataGridViewRow> found = new(StringComparer.OrdinalIgnoreCase);
    private readonly CancellationTokenSource closed = new();
    private CancellationTokenSource? operation;
    private CameraNetworkRange? searchedRange;
    private DirectCamera? verified;
    private bool busy;
    internal CameraEntry? Result { get; private set; }
    private NetworkCameraCandidate? Selected => results.CurrentRow?.Tag as NetworkCameraCandidate;

    internal CameraDiscoveryForm()
    {
        Text = "Kameras im Netzwerk suchen"; Name = "CameraDiscovery";
        StartPosition = FormStartPosition.CenterParent; Size = new Size(1050, 820); MinimumSize = new Size(900, 720);
        ShowInTaskbar = false; Icon = ApplicationBranding.WindowIcon;
        var root = new SettingsLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 5, Padding = new Padding(12) };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize)); root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        for (var i = 0; i < 3; i++) root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        var scope = new SettingsLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2 };
        scope.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); scope.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        void Field(TableLayoutPanel panel, string label, Control control, int row)
        { panel.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left }, 0, row); panel.Controls.Add(control, 1, row); }
        Field(scope, "Netzwerkadapter:", networks, 0); Field(scope, "IPv4-Bereich (CIDR):", range, 1); Field(scope, "RTSP-Ports (TCP):", ports, 2);
        var actions = new SettingsFlowPanel { Dock = DockStyle.Top, AutoSize = true, WrapContents = true };
        actions.Controls.AddRange([discover, scan, cancel]); scope.Controls.Add(actions, 0, 3); scope.SetColumnSpan(actions, 2);
        scope.Controls.Add(new Label { Text = "ONVIF: lokale Multicast-Suche. Hersteller/Modell werden nach Anmeldung über „ONVIF-Profile laden“ abgefragt. Portsuche: nur gewählter Bereich (max. 4096 IPv4-Adressen).", AutoSize = true,
            MaximumSize = new Size(850, 0) }, 0, 4); scope.SetColumnSpan(scope.GetControlFromPosition(0, 4)!, 2);
        root.Controls.Add(scope, 0, 0);
        foreach (var name in new[] { "Name / Modell", "Hersteller", "IP-Adresse", "Fund / Prüfstatus" }) results.Columns.Add(name, name);
        results.Columns[3].FillWeight = 180;
        var scrolling = new ModernSettingsScrollArea { Dock = DockStyle.Fill, Name = "DiscoveryResultsScroll" };
        scrolling.SetContent(results); root.Controls.Add(scrolling, 0, 1);
        results.RowsAdded += (_, _) => results.Height = results.ColumnHeadersHeight + results.Rows.Cast<DataGridViewRow>().Sum(r => r.Height) + 4;
        var details = new SettingsLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2 };
        details.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); details.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        Field(details, "ONVIF-Adresse:", device, 0); Field(details, "Kamera-Benutzer:", username, 1); Field(details, "Kamera-Passwort:", password, 2);
        Field(details, "Stream-Profil:", profiles, 3); Field(details, "RTSP-Adresse:", stream, 4);
        var detailActions = new SettingsFlowPanel { Dock = DockStyle.Top, AutoSize = true };
        detailActions.Controls.AddRange([load, test]); details.Controls.Add(detailActions, 1, 5);
        root.Controls.Add(details, 0, 2); root.Controls.Add(status, 0, 3);
        var footer = new SettingsFlowPanel { AutoSize = true, Dock = DockStyle.Top, FlowDirection = FlowDirection.RightToLeft };
        var close = new Button { Text = "Schließen", AutoSize = true, DialogResult = DialogResult.Cancel };
        footer.Controls.AddRange([close, take]); root.Controls.Add(footer, 0, 4);
        Controls.Add(root); CancelButton = close;
        networks.SelectedIndexChanged += (_, _) =>
        {
            if (networks.SelectedItem is CameraNetwork network) range.Text = $"{network.Address}/{Math.Max(network.Prefix, 24)}";
        };
        try { foreach (var network in CameraNetwork.Available()) networks.Items.Add(network); if (networks.Items.Count > 0) networks.SelectedIndex = 0; }
        catch (System.Net.NetworkInformation.NetworkInformationException) { status.Text = "Netzwerkadapter konnten nicht gelesen werden."; }
        discover.Click += async (_, _) => await Run(Discover);
        scan.Click += async (_, _) => await Run(Scan);
        cancel.Click += (_, _) => operation?.Cancel();
        results.SelectionChanged += (_, _) => LoadSelection();
        profiles.SelectedIndexChanged += (_, _) => { if (profiles.SelectedItem is OnvifProfile profile) stream.Text = profile.StreamUrl; };
        foreach (var field in new[] { username, password, stream }) field.TextChanged += (_, _) => { verified = null; take.Enabled = false; };
        load.Click += async (_, _) => await Run(LoadProfiles);
        test.Click += async (_, _) => await Run(Test);
        take.Click += (_, _) =>
        {
            if (verified is null || Selected is not { } selected) return;
            Result = new CameraEntry { Name = selected.Name.Length > 0 ? selected.Name : "Netzwerk " + selected.Address,
                Active = true, MotionEnabled = false, PersonEnabled = false, RecordingEnabled = false, Direct = verified.Clone() };
            Result.StreamUrl = Result.Direct.StreamUrl(); DialogResult = DialogResult.OK; Close();
        };
        SettingsForm.ApplyDarkTheme(this);
        SetBusy(false);
    }
    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        var darkTitleBar = 1;
        NativeMethods.DwmSetWindowAttribute(Handle, NativeMethods.DwmWindowAttribute.UseImmersiveDarkMode,
            ref darkTitleBar, sizeof(int));
    }
    internal static string ManufacturerText(NetworkCameraCandidate camera) => camera.Manufacturer.Length > 0
        ? camera.Manufacturer : camera.DeviceUrl.Length > 0 ? "Noch nicht abgefragt" : "Ohne ONVIF unbekannt";
    private void LoadSelection()
    {
        verified = null; profiles.Items.Clear(); password.Clear(); stream.Clear();
        device.Text = Selected?.DeviceUrl ?? "";
        if (Selected is { Port: > 0 } selected) stream.Text = $"rtsp://{selected.Address}:{selected.Port}/";
        SetBusy(busy);
    }
    private void SetBusy(bool value)
    {
        busy = value;
        foreach (var control in new Control[] { networks, range, ports, results, device, username, password, profiles, stream }) control.Enabled = !value;
        discover.Enabled = !value && networks.SelectedItem is CameraNetwork;
        scan.Enabled = !value && networks.SelectedItem is CameraNetwork; cancel.Enabled = value;
        load.Enabled = !value && Selected?.DeviceUrl.Length > 0;
        test.Enabled = !value && Selected is not null;
        take.Enabled = !value && verified is not null;
    }
    private async Task Run(Func<CancellationToken, Task> action)
    {
        if (busy) return;
        using var current = CancellationTokenSource.CreateLinkedTokenSource(closed.Token);
        operation = current; SetBusy(true);
        try { await action(current.Token); }
        catch (OperationCanceledException) { if (!IsDisposed) status.Text = "Vorgang abgebrochen; bisherige Funde bleiben erhalten."; }
        catch (Exception e) when (e is ArgumentException or InvalidDataException or IOException or System.ComponentModel.Win32Exception or System.Net.Http.HttpRequestException or System.Security.Cryptography.CryptographicException or System.Xml.XmlException or InvalidOperationException)
        { if (!IsDisposed) status.Text = e is ArgumentException or InvalidDataException ? e.Message : "Netzwerkzugriff oder Anmeldung fehlgeschlagen. Erreichbarkeit, Freigabe und Kamera-Zugangsdaten prüfen."; }
        finally { operation = null; if (!IsDisposed) SetBusy(false); }
    }
    private IProgress<NetworkCameraCandidate> Updates(CancellationToken token) => new Progress<NetworkCameraCandidate>(candidate =>
    {
        if (!IsDisposed && !token.IsCancellationRequested) AddCandidate(candidate);
    });
    internal void AddCandidate(NetworkCameraCandidate candidate)
    {
        var key = candidate.Address.ToString();
        if (!found.ContainsKey(key) && candidate.DeviceUrl.Length > 0 && found.Values.Any(r =>
            r.Tag is NetworkCameraCandidate previous && previous.DeviceUrl.Length > 0 && StringComparer.OrdinalIgnoreCase.Equals(previous.Identity, candidate.Identity))) return;
        if (found.TryGetValue(key, out var row))
        {
            var previous = (NetworkCameraCandidate)row.Tag!;
            if (previous.DeviceUrl.Length > 0 || candidate.DeviceUrl.Length == 0 && (previous.RtspService || !candidate.RtspService)) return;
        }
        else { var index = results.Rows.Add(); row = results.Rows[index]; found.Add(key, row); }
        row.Tag = candidate;
        row.SetValues(candidate.Name.Length > 0 ? candidate.Name : "Noch unbekannt", ManufacturerText(candidate), key, candidate.Status);
        if (ReferenceEquals(results.CurrentRow, row)) LoadSelection();
    }
    private async Task Discover(CancellationToken token)
    {
        if (networks.SelectedItem is not CameraNetwork network) throw new ArgumentException("Netzwerkadapter auswählen.");
        var scope = CameraNetworkRange.Parse(range.Text); searchedRange = scope;
        found.Clear(); results.Rows.Clear(); verified = null;
        status.Text = "ONVIF-Suche läuft …";
        await NetworkCameraDiscovery.Discover(network.Address, scope, Updates(token), token);
        token.ThrowIfCancellationRequested();
        status.Text = found.Count == 0 ? "Keine ONVIF-Antwort. Optional den Bereich mit „RTSP-Ports gezielt prüfen“ untersuchen. Multicast erreicht andere VLANs üblicherweise nicht." : $"ONVIF-Suche beendet: {found.Count} Gerät(e). Für Hersteller und Stream-Profile: Gerät wählen, Kamera-Zugangsdaten eintragen und „ONVIF-Profile laden“ klicken.";
    }
    private async Task Scan(CancellationToken token)
    {
        if (networks.SelectedItem is not CameraNetwork network) throw new ArgumentException("Netzwerkadapter auswählen.");
        var scope = CameraNetworkRange.Parse(range.Text); var selectedPorts = NetworkCameraDiscovery.ParsePorts(ports.Text);
        if (searchedRange is not null && scope.Key != searchedRange.Key)
        { found.Clear(); results.Rows.Clear(); }
        searchedRange = scope;
        var total = scope.Count * selectedPorts.Length;
        status.Text = $"Portsuche läuft: {scope.Count} Adressen, {selectedPorts.Length} Port(s) …";
        var count = new Progress<int>(n => { if (!IsDisposed && !token.IsCancellationRequested && (n % 16 == 0 || n == total)) status.Text = $"Portsuche: {n}/{total} geprüft. {found.Count} Kandidat(en)."; });
        var updates = Updates(token); // Capture the UI synchronization context before Task.Run.
        await Task.Run(() => NetworkCameraDiscovery.ScanPorts(scope, selectedPorts, updates, count, token, network.Address), token);
        token.ThrowIfCancellationRequested();
        status.Text = $"Portsuche beendet: {found.Count} Kandidat(en). Offene Ports sind keine bestätigten Streams. RTSP-Pfad eintragen und Verbindung testen.";
    }
    private async Task LoadProfiles(CancellationToken token)
    {
        if (Selected is not { DeviceUrl.Length: > 0 } selected) return;
        status.Text = "ONVIF-Anmeldung und Profile werden geprüft …"; verified = null;
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(token); limit.CancelAfter(TimeSpan.FromSeconds(60));
        token = limit.Token;
        using var client = new OnvifClient(username.Text.Trim(), password.Text, true);
        var values = await client.Profiles(selected.DeviceUrl, token);
        token.ThrowIfCancellationRequested();
        profiles.Items.Clear(); foreach (var profile in values) profiles.Items.Add(profile); profiles.SelectedIndex = 0;
        var metadataStatus = "Hersteller konnte nicht abgefragt werden.";
        try
        {
            var info = await client.Information(selected.DeviceUrl, token); token.ThrowIfCancellationRequested();
            var updated = selected with { Manufacturer = info.Manufacturer.Length > 0 ? info.Manufacturer : selected.Manufacturer, Name = selected.Name.Length > 0 ? selected.Name : info.Model };
            results.CurrentRow!.Tag = updated;
            results.CurrentRow.Cells[0].Value = updated.Name; results.CurrentRow.Cells[1].Value = updated.Manufacturer.Length > 0 ? updated.Manufacturer : "Nicht gemeldet";
            metadataStatus = updated.Manufacturer.Length > 0 ? "Hersteller: " + updated.Manufacturer + "." : "Die Kamera meldet keinen Hersteller.";
        }
        catch (Exception e) when (e is InvalidDataException or System.Net.Http.HttpRequestException) { } // Device metadata is optional.
        status.Text = $"{values.Count} Profil(e) geladen. {metadataStatus} Profil wählen und „Verbindung testen“ ausführen.";
    }
    private DirectCamera Configuration()
    {
        if (Selected is not { } selected || !Uri.TryCreate(stream.Text.Trim(), UriKind.Absolute, out var url) || url.Scheme is not ("rtsp" or "rtsps") ||
            !System.Net.IPAddress.TryParse(url.Host, out var address) || !address.Equals(selected.Address))
            throw new ArgumentException("Eine RTSP-Adresse mit dem Stream-Pfad dieses Geräts eintragen oder ein ONVIF-Profil wählen.");
        if (url.UserInfo.Length > 0) throw new ArgumentException("Zugangsdaten bitte ausschließlich in Benutzer- und Passwortfeld eintragen.");
        return new DirectCamera { Enabled = true, DeviceUrl = selected.DeviceUrl, Username = username.Text.Trim(), ProtectedPassword = CameraPassword.Protect(password.Text),
            RtspUrl = url.AbsoluteUri, ProfileToken = (profiles.SelectedItem as OnvifProfile)?.Token ?? "", DiscoveryId = selected.DeviceUrl.Length > 0 ? selected.Identity : "" };
    }
    private async Task Test(CancellationToken token)
    {
        verified = null; var config = Configuration(); status.Text = "Videobild wird geprüft (max. 25 Sekunden) …";
        var success = await CameraStreamTest.DecodeFrame(config.StreamUrl(), token); token.ThrowIfCancellationRequested();
        if (success) { verified = config; status.Text = "Videobild empfangen – Stream bestätigt. Die Kamera kann übernommen werden."; results.CurrentRow!.Cells[3].Value = "Videostream bestätigt"; }
        else status.Text = "Kein Videobild empfangen. Pfad, Kamera-Zugangsdaten und RTSP-Freigabe prüfen.";
    }
    protected override void OnFormClosing(FormClosingEventArgs e) { if (!closed.IsCancellationRequested) closed.Cancel(); base.OnFormClosing(e); }
    protected override void Dispose(bool disposing) { if (disposing) { if (!closed.IsCancellationRequested) closed.Cancel(); closed.Dispose(); } base.Dispose(disposing); }
}
#endif
