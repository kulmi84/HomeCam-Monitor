#if V2_BETA
namespace HomeCamMonitor;
internal sealed record HaCameraSelection(string EntityId, string Name, string Motion, string Person, string Source, bool UpdateSelected);
internal sealed class HomeAssistantDiscoveryForm : Form
{
    private readonly string address, token, existingStream;
    private readonly bool ignoreCertificate, canUpdate;
    private readonly DarkComboBox cameras = new() { Name = "HaDiscoveredCameras", Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly DarkComboBox motion = new() { Name = "HaMotionSensors", Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly DarkComboBox person = new() { Name = "HaPersonSensors", Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly DarkComboBox source = new() { Name = "HaDiscoveredSource", Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly TextBox stream = new() { Name = "HaDiscoveredStream", Dock = DockStyle.Fill };
    private readonly Label status = new() { Name = "HaDiscoveryStatus", AutoSize = true, MaximumSize = new Size(820, 0) };
    private readonly Button refresh = new() { Text = UiText.T("Kameras und Sensoren suchen"), AutoSize = true };
    private readonly Button test = new() { Name = "TestHaDiscoveredStream", Text = UiText.T("Verbindung testen"), AutoSize = true };
    private readonly Button take = new() { Name = "ImportHaDiscoveredCamera", Text = UiText.T("Als neue Kamera übernehmen"), AutoSize = true, Enabled = false };
    private readonly Button update = new() { Name = "ApplyHaDiscoveredSensors", Text = UiText.T("Sensoren zu ausgewählter Kamera übernehmen"), AutoSize = true };
    private readonly Button cancel = new() { Text = UiText.T("Vorgang abbrechen"), AutoSize = true, Enabled = false };
    private readonly CancellationTokenSource lifetime = new();
    private CancellationTokenSource? operation;
    private HaCatalog? catalog;
    private bool busy, verified;
    internal HaCameraSelection? Result { get; private set; }
    internal HomeAssistantDiscoveryForm(string address, string token, bool ignoreCertificate, string existingStream, bool canUpdate, bool autoSearch = true)
    {
        this.address = address; this.token = token; this.ignoreCertificate = ignoreCertificate; this.existingStream = existingStream; this.canUpdate = canUpdate;
        Name = "HomeAssistantDiscovery"; Text = UiText.T("Home Assistant – Kameras und Sensoren"); ApplicationBranding.ApplyWindowIcon(this);
        Size = new Size(920, 530); MinimumSize = new Size(800, 500); StartPosition = FormStartPosition.CenterParent; ShowInTaskbar = false;
        HandleCreated += (_, _) => { var dark = 1; NativeMethods.DwmSetWindowAttribute(Handle, NativeMethods.DwmWindowAttribute.UseImmersiveDarkMode, ref dark, sizeof(int)); };
        var layout = new SettingsLayoutPanel { Dock = DockStyle.Fill, AutoScroll = true, Padding = new Padding(12), ColumnCount = 2 };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        void Field(string label, Control control, int row) { layout.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left }, 0, row); layout.Controls.Add(control, 1, row); }
        var hint = new Label { Text = UiText.T("Kameras und binäre Sensoren aus dem angegebenen Home Assistant. Vorschläge gelten nur bei eindeutiger Zuordnung zum selben Gerät; bitte prüfen."), AutoSize = true, MaximumSize = new Size(820, 0) };
        layout.Controls.Add(hint, 0, 0); layout.SetColumnSpan(hint, 2);
        Field(UiText.T("Kamera:"), cameras, 1); Field(UiText.T("Bewegung:"), motion, 2); Field("Person:", person, 3); Field(UiText.T("Bildquelle:"), source, 4); Field(UiText.T("Streamadresse:"), stream, 5);
        source.Items.AddRange(["HA-Stream (HLS)", UiText.T("Vorhandene / eigene RTSP-/HTTP-Adresse")]);
        source.SelectedIndex = existingStream.Length == 0 || HaApi.Entity(existingStream) is not null ? 0 : 1;
        stream.Text = existingStream; stream.Enabled = source.SelectedIndex == 1;
        var actions = new SettingsFlowPanel { AutoSize = true, Dock = DockStyle.Top }; actions.Controls.AddRange([refresh, test, cancel]);
        layout.Controls.Add(actions, 0, 6); layout.SetColumnSpan(actions, 2);
        layout.Controls.Add(status, 0, 7); layout.SetColumnSpan(status, 2);
        var footer = new SettingsFlowPanel { AutoSize = true, Dock = DockStyle.Top, WrapContents = true };
        var close = new Button { Text = UiText.T("Schließen"), AutoSize = true, DialogResult = DialogResult.Cancel };
        footer.Controls.AddRange([take, update, close]); layout.Controls.Add(footer, 0, 8); layout.SetColumnSpan(footer, 2);
        Controls.Add(layout); CancelButton = close;
        cameras.SelectedIndexChanged += (_, _) => SelectCamera();
        source.SelectedIndexChanged += (_, _) => { stream.Enabled = !busy && source.SelectedIndex == 1; verified = false; SetBusy(busy); };
        stream.TextChanged += (_, _) => { verified = false; SetBusy(busy); };
        refresh.Click += async (_, _) => await Run(async ct => { LoadCatalog(await HaApi.Discover(address, token, ignoreCertificate, ct)); });
        test.Click += async (_, _) => await Run(Test);
        cancel.Click += (_, _) => operation?.Cancel();
        take.Click += (_, _) => Finish(false); update.Click += (_, _) => Finish(true);
        SettingsForm.ApplyDarkTheme(this); SetBusy(false);
        if (autoSearch) Shown += async (_, _) => await Run(async ct => { LoadCatalog(await HaApi.Discover(address, token, ignoreCertificate, ct)); });
    }
    internal void LoadCatalog(HaCatalog value)
    {
        if (IsDisposed || lifetime.IsCancellationRequested) return;
        catalog = value; cameras.Items.Clear(); motion.Items.Clear(); person.Items.Clear();
        foreach (var camera in value.Cameras) cameras.Items.Add(camera);
        var none = new HaEntity("", "", "", "", ""); motion.Items.Add(none); person.Items.Add(none);
        foreach (var sensor in value.Sensors) { motion.Items.Add(sensor); person.Items.Add(sensor); }
        motion.SelectedIndex = person.SelectedIndex = 0;
        if (canUpdate) cameras.Items.Add(new HaEntity("", UiText.T("Nur Sensoren für die ausgewählte Kamera"), "unknown", "", ""));
        if (cameras.Items.Count > 0) cameras.SelectedIndex = value.Cameras.ToList().FindIndex(c => c.Available) is var first && first >= 0 ? first : 0;
        status.Text = UiText.F("{0} Kamera(s), {1} binäre Sensoren. ", value.Cameras.Count, value.Sensors.Count) +
            (value.RegistryAvailable ? UiText.T("Vorschläge anhand der Gerätezuordnung; ohne eindeutigen Treffer bitte selbst wählen.") : UiText.T("Geräteregister nicht verfügbar (ggf. Benutzerrechte). Sensoren bitte selbst auswählen."));
    }
    private void SelectCamera()
    {
        verified = false;
        if (catalog is null || cameras.SelectedItem is not HaEntity camera) return;
        motion.SelectedItem = catalog.Suggest(camera, false) ?? motion.Items[0];
        person.SelectedItem = catalog.Suggest(camera, true) ?? person.Items[0];
        SetBusy(busy);
    }
    private void SetBusy(bool value)
    {
        busy = value;
        foreach (Control c in new Control[] { cameras, motion, person, source }) c.Enabled = !value;
        stream.Enabled = !value && source.SelectedIndex == 1; refresh.Enabled = !value; cancel.Enabled = value;
        test.Enabled = !value && cameras.SelectedItem is HaEntity { Available: true };
        take.Enabled = !value && verified && cameras.SelectedItem is HaEntity { Available: true };
        update.Enabled = !value && canUpdate && cameras.SelectedItem is HaEntity;
    }
    private async Task Run(Func<CancellationToken, Task> action)
    {
        if (busy) return;
        using var active = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token); active.CancelAfter(TimeSpan.FromSeconds(60)); operation = active;
        SetBusy(true); status.Text = UiText.T("Home Assistant wird abgefragt …");
        try { await action(active.Token); }
        catch (OperationCanceledException) { if (!IsDisposed) status.Text = UiText.T("Abgebrochen oder Zeitlimit erreicht."); }
        catch (Exception e) when (e is InvalidDataException or ArgumentException or IOException or System.Net.Http.HttpRequestException or System.Net.WebSockets.WebSocketException or System.Text.Json.JsonException or System.ComponentModel.Win32Exception)
        { if (!IsDisposed) status.Text = e is ArgumentException or InvalidDataException ? e.Message : UiText.T("HA-Verbindung oder Videotest fehlgeschlagen. Adresse, Token, Zertifikat und Freigaben prüfen."); }
        finally { operation = null; if (!IsDisposed) SetBusy(false); }
    }
    private string Source()
    {
        if (cameras.SelectedItem is not HaEntity camera) throw new ArgumentException(UiText.T("Kamera auswählen."));
        if (source.SelectedIndex == 0) return HaApi.Source(camera.Id);
        if (!Uri.TryCreate(stream.Text.Trim(), UriKind.Absolute, out var uri) || uri.Scheme is not ("rtsp" or "rtsps" or "http" or "https"))
            throw new ArgumentException(UiText.T("Eine gültige RTSP-/HTTP-Streamadresse eintragen."));
        return uri.AbsoluteUri;
    }
    private async Task Test(CancellationToken cancel)
    {
        verified = false; var url = Source();
        using var bridge = source.SelectedIndex == 0 ? new HomeAssistantStreams(address, token, ignoreCertificate, [((HaEntity)cameras.SelectedItem!).Id]) : null;
        if (bridge is not null) url = bridge.Url(((HaEntity)cameras.SelectedItem!).Id);
        status.Text = UiText.T("Videobild wird geprüft …");
        var success = await CameraStreamTest.DecodeFrame(url, cancel); cancel.ThrowIfCancellationRequested(); verified = success;
        status.Text = success ? UiText.T("Videobild empfangen. Kamera kann übernommen werden; Sensorvorschläge prüfen.") : UiText.T("Kein Videobild. HA-Stream-Unterstützung prüfen oder vorhandene RTSP-/HTTP-Adresse verwenden.");
    }
    private void Finish(bool updateOnly)
    {
        if (busy || cameras.SelectedItem is not HaEntity camera || !updateOnly && !verified || updateOnly && !canUpdate) return;
        Result = new HaCameraSelection(camera.Id, camera.Name, (motion.SelectedItem as HaEntity)?.Id ?? "", (person.SelectedItem as HaEntity)?.Id ?? "", updateOnly ? existingStream : Source(), updateOnly);
        DialogResult = DialogResult.OK; Close();
    }
    protected override void OnFormClosing(FormClosingEventArgs e) { lifetime.Cancel(); base.OnFormClosing(e); }
    protected override void Dispose(bool disposing) { if (disposing && !lifetime.IsCancellationRequested) lifetime.Cancel(); base.Dispose(disposing); }
}
#endif
