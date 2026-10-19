#if V2_BETA
namespace HomeCamMonitor;

internal sealed class V2ConnectionTabs : UserControl
{
    private readonly Panel uidPage = new SettingsScrollPanel { Name = "ReolinkTab", AutoSize = true, Dock = DockStyle.Top, Padding = new Padding(0, 5, 0, 0) };
    private readonly Panel haPage = new SettingsScrollPanel { Name = "HomeAssistantTab", AutoSize = true, Dock = DockStyle.Top, Padding = new Padding(0, 5, 0, 0) };
    private readonly Button uidTab = new() { Name = "ReolinkTabButton", Text = "Reolink", AutoSize = true };
    private readonly Button haTab = new() { Name = "HomeAssistantTabButton", Text = "Home Assistant", AutoSize = true };
    private readonly Panel directPage = new SettingsScrollPanel { Name = "DirectCameraTab", AutoSize = true, Dock = DockStyle.Top, Padding = new Padding(0, 5, 0, 0) };
    private readonly Button directTab = new() { Name = "DirectCameraTabButton", Text = "ONVIF / RTSP", AutoSize = true };
    private readonly DirectCameraEditor directEditor;
    private int selectedIndex = -1;
    internal int TabCount => 3;
    internal event Action? SourceChanged;
    internal string ConnectionSummary
    {
        get
        {
            var row = cameras.CurrentRow;
            if (row is null || row.IsNewRow) return UiText.T("Keine Kamera ausgewählt");
            var source = row.Tag is ReolinkCamera { Enabled: true } ? "Reolink-UID" : DirectCamera.ForRow(row).Enabled ? "ONVIF / RTSP" : "Home Assistant";
            return $"{Convert.ToString(row.Cells["CameraName"].Value)} · {source}" +
                (Convert.ToBoolean(row.Cells["Active"].Value ?? true) ? "" : UiText.T(" · Kamera deaktiviert"));
        }
    }
    internal void UpdateTabAppearance()
    {
        var buttons = new[] { uidTab, haTab, directTab };
        var names = new[] { "Reolink", "Home Assistant", "ONVIF / RTSP" };
        for (var i = 0; i < buttons.Length; i++)
        {
            var button = buttons[i];
            button.UseVisualStyleBackColor = false;
            button.FlatStyle = FlatStyle.Flat;
            button.Text = names[i];
            button.BackColor = Color.FromArgb(48, 48, 54);
            button.ForeColor = Color.FromArgb(242, 242, 244);
            button.FlatAppearance.BorderColor = Color.FromArgb(77, 77, 85);
            button.FlatAppearance.MouseOverBackColor = Color.FromArgb(62, 62, 69);
            button.FlatAppearance.MouseDownBackColor = Color.FromArgb(72, 72, 80);
            button.Font = i == selectedIndex ? selectedFont : Font;
            button.AccessibleDescription = i == selectedIndex ? UiText.T("Geöffnete Einstellungen.") : UiText.T("Einstellungen öffnen.");
        }
    }
    internal int SelectedIndex
    {
        get => selectedIndex;
        set
        {
            if (value is < 0 or > 2) throw new ArgumentOutOfRangeException(nameof(value));
            if (selectedIndex == value) return;
            SuspendLayout();
            try
            {
                selectedIndex = value;
                uidPage.Visible = value == 0; haPage.Visible = value == 1;
                directPage.Visible = value == 2;
                uidTab.Font = value == 0 ? selectedFont : Font;
                haTab.Font = value == 1 ? selectedFont : Font;
                directTab.Font = value == 2 ? selectedFont : Font;
                uidTab.AccessibleDescription = value == 0 ? UiText.T("Ausgewählt") : "";
                haTab.AccessibleDescription = value == 1 ? UiText.T("Ausgewählt") : "";
                directTab.AccessibleDescription = value == 2 ? UiText.T("Ausgewählt") : "";
                UpdateTabAppearance();
            }
            finally { ResumeLayout(true); }
        }
    }
    private readonly Font selectedFont;
    private Control SelectedTab { set => SelectedIndex = ReferenceEquals(value, haPage) ? 1 : 0; }
    private readonly DataGridView cameras;
    private readonly ReolinkEditor reolink;
    private readonly CheckBox homeAssistantEvents;
    private readonly CheckBox detection;
    private readonly TextBox personEntity;
    private readonly TextBox motionEntity;
    private readonly CheckBox useHomeAssistant = new() { Name = "UseHomeAssistantCamera", Text = UiText.T("Für ausgewählte Kamera verwenden"), AutoSize = true };
    private readonly TextBox streamUrl = new() { Name = "HomeAssistantCameraStream", Dock = DockStyle.Fill };
    private bool loading;

    internal V2ConnectionTabs(DataGridView cameras, ReolinkEditor reolink, Control haFields,
        CheckBox homeAssistantEvents, CheckBox detection, TextBox personEntity, TextBox motionEntity, TextBox haAddress, TextBox haToken, CheckBox ignoreCertificate)
    {
        this.cameras = cameras; this.reolink = reolink; this.homeAssistantEvents = homeAssistantEvents;
        this.detection = detection; this.personEntity = personEntity; this.motionEntity = motionEntity;
        Name = "CameraConnectionTabs"; Dock = DockStyle.Top; AutoSize = true;
        AccessibleRole = AccessibleRole.PageTabList;
        AutoSizeMode = AutoSizeMode.GrowAndShrink; DoubleBuffered = true;
        selectedFont = new Font(Font, FontStyle.Bold);
        Disposed += (_, _) => selectedFont.Dispose();
        var header = new SettingsFlowPanel { Dock = DockStyle.Top, AutoSize = true, WrapContents = false, Margin = Padding.Empty };
        uidTab.AccessibleRole = haTab.AccessibleRole = directTab.AccessibleRole = AccessibleRole.PageTab;
        uidTab.Margin = haTab.Margin = directTab.Margin = new Padding(0, 0, 4, 0);
        header.Controls.AddRange([uidTab, haTab, directTab]);
        var layout = new SettingsLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 1, RowCount = 4, Margin = Padding.Empty };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (var row = 0; row < 4; row++) layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.Controls.Add(header, 0, 0); layout.Controls.Add(uidPage, 0, 1); layout.Controls.Add(haPage, 0, 2);
        layout.Controls.Add(directPage, 0, 3);
        Controls.Add(layout);
        uidTab.Click += (_, _) => SelectedIndex = 0;
        haTab.Click += (_, _) => SelectedIndex = 1;
        directTab.Click += (_, _) => SelectedIndex = 2;
        directEditor = new DirectCameraEditor(cameras);
        directPage.Controls.Add(directEditor);
        directEditor.ActivateRequested += () =>
        {
            if (cameras.CurrentRow is not { IsNewRow: false } row) return;
            loading = true;
            try { reolink.SelectNonUid(true, DirectCamera.ForRow(row).StreamUrlOrEmpty()); }
            finally { loading = false; }
            LoadSelection();
        };
        directEditor.ConfigurationChanged += () => RefreshCapabilities();
        directEditor.SearchRequested += () =>
        {
            using var search = new CameraDiscoveryForm();
            if (search.ShowDialog(FindForm()) == DialogResult.OK && search.Result is { } camera)
            {
                if (!ImportDiscovered(camera)) MessageBox.Show(FindForm(), UiText.T("Diese Kamera ist bereits eingerichtet. Der vorhandene Eintrag wurde ausgewählt und bleibt unverändert."),
                    UiText.T("Kamera bereits vorhanden"), MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        };
        directEditor.AddRequested += () =>
        {
            reolink.Store();
            var names = cameras.Rows.Cast<DataGridViewRow>().Where(r => !r.IsNewRow).Select(r => Convert.ToString(r.Cells["CameraName"].Value)).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var number = 1; while (names.Contains(UiText.T("Netzwerk ") + number)) number++;
            var index = cameras.Rows.Add(UiText.T("Netzwerk ") + number, "", false, false, "", "None", 30, "", true, "");
            var row = cameras.Rows[index]; row.Tag = new ReolinkCamera(); DirectCamera.ForRow(row).Enabled = true;
            cameras.CurrentCell = row.Cells["CameraName"]; row.Selected = true;
            reolink.SelectNonUid(true, ""); LoadSelection(); SelectedIndex = 2;
        };
        cameras.CellFormatting += (_, e) =>
        {
            if (e.RowIndex >= 0 && e.ColumnIndex == cameras.Columns["StreamUrl"].Index && !cameras.Rows[e.RowIndex].IsNewRow && DirectCamera.ForRow(cameras.Rows[e.RowIndex]).Enabled)
            { e.Value = UiText.T("ONVIF / RTSP – direkt"); e.FormattingApplied = true; }
        };
        uidPage.Controls.Add(reolink);
        homeAssistantEvents.Text = UiText.T("Bewegungs- und Personenereignisse von Home Assistant empfangen");
        homeAssistantEvents.Name = "HomeAssistantEventsEnabled";
        var haLayout = new SettingsLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 1, RowCount = 5 };
        haLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (var row = 0; row < 5; row++) haLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        var add = new Button { Name = "AddHomeAssistantCamera", Text = UiText.T("HA-Kamera hinzufügen"), AutoSize = true };
        var source = new SettingsFlowPanel { Dock = DockStyle.Top, AutoSize = true, WrapContents = true };
        var searchHa = new Button { Name = "SearchHomeAssistantCameras", Text = UiText.T("Kameras und Sensoren suchen"), AutoSize = true };
        source.Controls.Add(add); source.Controls.Add(searchHa); source.Controls.Add(useHomeAssistant);
        searchHa.Click += (_, _) =>
        {
            var selected = cameras.CurrentRow is { IsNewRow: false } candidate && candidate.Tag is not ReolinkCamera { Enabled: true } && !DirectCamera.ForRow(candidate).Enabled;
            using var search = new HomeAssistantDiscoveryForm(haAddress.Text.Trim(), haToken.Text.Trim(), ignoreCertificate.Checked,
                selected ? streamUrl.Text.Trim() : "", selected);
            if (search.ShowDialog(FindForm()) == DialogResult.OK && search.Result is { } result)
            {
                if (!ImportHomeAssistant(result)) MessageBox.Show(FindForm(), UiText.T("Diese HA-Kamera ist bereits eingerichtet. Der vorhandene Eintrag wurde ausgewählt und bleibt unverändert."), UiText.T("Kamera bereits vorhanden"), MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        };
        haLayout.Controls.Add(source, 0, 0);
        haLayout.Controls.Add(new Label { Text = UiText.T("RTSP-/HTTP-Streamadresse dieser Kamera:"), AutoSize = true }, 0, 1);
        haLayout.Controls.Add(streamUrl, 0, 2);
        haLayout.Controls.Add(new Label { Text = UiText.T("Streamadresse wie in V1 eintragen. Sensoren werden unter Bewegungserkennung zugeordnet."),
            AutoSize = true, MaximumSize = new Size(700, 0), Margin = new Padding(3, 5, 3, 10) }, 0, 3);
        haLayout.Controls.Add(haFields, 0, 4);
        var haGroup = new GroupBox { Name = "HomeAssistantConnection", Text = UiText.T("Home Assistant – Verbindung"),
            Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(10, 18, 10, 10) };
        haGroup.Controls.Add(haLayout);
        haPage.Controls.Add(haGroup);
        add.Click += (_, _) =>
        {
            reolink.Store();
            var names = cameras.Rows.Cast<DataGridViewRow>().Where(r => !r.IsNewRow)
                .Select(r => Convert.ToString(r.Cells["CameraName"].Value)).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var number = 1; while (names.Contains("HA " + number)) number++;
            var index = cameras.Rows.Add("HA " + number, "", false, false, "", "None", 30, "", true, "");
            cameras.Rows[index].Tag = new ReolinkCamera();
            cameras.CurrentCell = cameras.Rows[index].Cells["CameraName"];
            cameras.Rows[index].Selected = true;
            LoadSelection(); SelectedTab = haPage; streamUrl.Focus();
        };
        useHomeAssistant.CheckedChanged += (_, _) =>
        {
            if (loading || cameras.CurrentRow is not { IsNewRow: false } row) return;
            if (useHomeAssistant.Checked)
            {
                loading = true;
                try { reolink.SelectNonUid(false, Convert.ToString(row.Cells["HomeAssistantStreamUrl"].Value) ?? ""); }
                finally { loading = false; }
                LoadSelection();
            }
            else if (row.Tag is not ReolinkCamera { Enabled: true })
            { loading = true; useHomeAssistant.Checked = true; loading = false; }
            RefreshCapabilities();
        };
        streamUrl.TextChanged += (_, _) =>
        {
            if (loading || cameras.CurrentRow is not { IsNewRow: false } row) return;
            row.Cells["HomeAssistantStreamUrl"].Value = streamUrl.Text.Trim();
            if (row.Tag is not ReolinkCamera { Enabled: true } && !DirectCamera.ForRow(row).Enabled) row.Cells["StreamUrl"].Value = streamUrl.Text.Trim();
        };
        cameras.SelectionChanged += (_, _) => LoadSelection();
        cameras.CurrentCellChanged += (_, _) => LoadSelection();
        reolink.ConnectionChanged += LoadSelection;
        cameras.CellValueChanged += (_, e) =>
        {
            if (loading) return;
            if (e.RowIndex >= 0 && e.ColumnIndex == cameras.Columns["StreamUrl"].Index &&
                ReferenceEquals(cameras.CurrentRow, cameras.Rows[e.RowIndex]) && cameras.Rows[e.RowIndex].Tag is not ReolinkCamera { Enabled: true }) LoadSelection(false);
            if (e.RowIndex >= 0 && e.ColumnIndex == cameras.Columns["MotionEnabled"].Index && cameras.Rows[e.RowIndex] is { IsNewRow: false } motionRow &&
                Convert.ToBoolean(motionRow.Cells["MotionEnabled"].Value ?? false) && DirectCamera.ForRow(motionRow).Enabled)
                LocalMotionSettings.ForRow(motionRow).Enabled = true;
            RefreshCapabilities();
        };
        personEntity.TextChanged += (_, _) => RefreshCapabilities();
        homeAssistantEvents.CheckedChanged += (_, _) => RefreshCapabilities();
        detection.CheckedChanged += (_, _) => RefreshCapabilities();
        LoadSelection();
    }

    private void LoadSelection() => LoadSelection(true);
    internal bool ImportDiscovered(CameraEntry entry)
    {
        reolink.Store();
        static string Identity(string value) => Uri.TryCreate(value, UriKind.Absolute, out var uri)
            ? uri.GetComponents(UriComponents.SchemeAndServer | UriComponents.PathAndQuery, UriFormat.UriEscaped).TrimEnd('/') : "";
        var streamKey = Identity(entry.Direct.RtspUrl); var deviceKey = Identity(entry.Direct.DeviceUrl);
        foreach (DataGridViewRow existing in cameras.Rows)
        {
            if (existing.IsNewRow) continue;
            var config = DirectCamera.ForRow(existing);
            if (streamKey.Length > 0 && Identity(config.RtspUrl) == streamKey || deviceKey.Length > 0 && Identity(config.DeviceUrl) == deviceKey ||
                !string.IsNullOrEmpty(entry.Direct.DiscoveryId) && StringComparer.OrdinalIgnoreCase.Equals(config.DiscoveryId, entry.Direct.DiscoveryId))
            { cameras.CurrentCell = existing.Cells["CameraName"]; existing.Selected = true; LoadSelection(); return false; }
        }
        var direct = entry.Direct.Clone(); var url = direct.StreamUrl();
        var index = cameras.Rows.Add(entry.Name, url, false, false, "", "None", 30, "", true, "", false);
        var row = cameras.Rows[index]; row.Tag = new ReolinkCamera(); row.Cells["DirectConnection"].Value = direct;
        cameras.CurrentCell = row.Cells["CameraName"]; row.Selected = true;
        reolink.SelectNonUid(true, url); LoadSelection(); SelectedIndex = 2;
        return true;
    }
    internal bool ImportHomeAssistant(HaCameraSelection selection)
    {
        reolink.Store();
        DataGridViewRow row;
        if (selection.UpdateSelected)
        {
            if (cameras.CurrentRow is not { IsNewRow: false } selected || selected.Tag is ReolinkCamera { Enabled: true } || DirectCamera.ForRow(selected).Enabled) return false;
            row = selected;
        }
        else
        {
            foreach (DataGridViewRow existing in cameras.Rows)
            {
                if (existing.IsNewRow) continue;
                if (Convert.ToString(existing.Cells["HomeAssistantEntityId"].Value) == selection.EntityId ||
                    HaApi.Entity(Convert.ToString(existing.Cells["HomeAssistantStreamUrl"].Value) ?? "") == selection.EntityId ||
                    StringComparer.OrdinalIgnoreCase.Equals(Convert.ToString(existing.Cells["StreamUrl"].Value), selection.Source))
                { cameras.CurrentCell = existing.Cells["CameraName"]; existing.Selected = true; LoadSelection(); return false; }
            }
            var index = cameras.Rows.Add(selection.Name, selection.Source, false, false, "", "None", 30, "", true, "", false);
            row = cameras.Rows[index]; row.Tag = new ReolinkCamera();
            row.Cells["HomeAssistantStreamUrl"].Value = selection.Source;
            row.Cells["HomeAssistantEntityId"].Value = selection.EntityId;
            cameras.CurrentCell = row.Cells["CameraName"]; row.Selected = true;
        }
        row.Cells["MotionEntityId"].Value = selection.Motion; row.Cells["PersonEntityId"].Value = selection.Person;
        row.Cells["MotionEnabled"].Value = selection.Motion.Length > 0; row.Cells["PersonEnabled"].Value = selection.Person.Length > 0;
        motionEntity.Text = selection.Motion; personEntity.Text = selection.Person;
        if (selection.Motion.Length > 0 || selection.Person.Length > 0) homeAssistantEvents.Checked = true;
        LoadSelection(); SelectedIndex = 1; RefreshCapabilities();
        return true;
    }
    internal void RefreshAfterReorder() => LoadSelection(false);
    internal void RefreshAfterRemoval() => LoadSelection(true);
    private void LoadSelection(bool followSource)
    {
        if (loading || CameraGridUpdate.IsActive(cameras)) return;
        loading = true;
        try
        {
            var row = cameras.CurrentRow is { IsNewRow: false } current ? current : null;
            var uid = row?.Tag is ReolinkCamera { Enabled: true };
            var direct = row is not null && DirectCamera.ForRow(row).Enabled;
            useHomeAssistant.Enabled = row is not null;
            useHomeAssistant.Checked = row is not null && !uid && !direct;
            streamUrl.Enabled = row is not null;
            if (row is not null && !uid && !direct)
                row.Cells["HomeAssistantStreamUrl"].Value = Convert.ToString(row.Cells["StreamUrl"].Value) ?? "";
            streamUrl.Text = row is null ? "" : Convert.ToString(row.Cells["HomeAssistantStreamUrl"].Value) ?? "";
            if (followSource) SelectedIndex = direct ? 2 : row is not null && !uid ? 1 : 0;
            directEditor.LoadSelection();
        }
        finally { loading = false; }
        RefreshCapabilities();
    }

    private bool SupportsPerson(DataGridViewRow row) => !row.IsNewRow && Convert.ToBoolean(row.Cells["Active"].Value ?? true) &&
        row.Tag is not ReolinkCamera { Enabled: true } && !DirectCamera.ForRow(row).Enabled && homeAssistantEvents.Checked && detection.Checked &&
        !string.IsNullOrWhiteSpace(ReferenceEquals(row, cameras.CurrentRow) ? personEntity.Text : Convert.ToString(row.Cells["PersonEntityId"].Value));

    internal void RefreshCapabilities()
    {
        if (loading || CameraGridUpdate.IsActive(cameras)) return;
        foreach (DataGridViewRow row in cameras.Rows)
        {
            var cell = row.Cells["PersonEnabled"];
            cell.ReadOnly = !SupportsPerson(row);
            cell.ToolTipText = cell.ReadOnly ? UiText.T("Benötigt eine aktive HA-Kamera, aktivierte HA-Ereignisse und eine Personen-Entität. Reolink-UID unterstützt derzeit nur allgemeine Bewegung.") : UiText.T("Snapshot bei einem Personenereignis speichern.");
            var active = !row.IsNewRow && Convert.ToBoolean(row.Cells["Active"].Value ?? true);
            var direct = !row.IsNewRow && DirectCamera.ForRow(row).Enabled && !LocalMotionSettings.ForRow(row).Enabled;
            row.Cells["MotionEnabled"].ReadOnly = !active;
            row.Cells["MotionEnabled"].ToolTipText = !active ? UiText.T("Kamera zuerst aktivieren.") : DirectCamera.ForRow(row).Enabled ? UiText.T("Lokale Bildanalyse für diese Netzwerkkamera aktivieren.") : UiText.T("Bewegungsereignisse verarbeiten.");
            row.Cells["RecordingEnabled"].ReadOnly = !active || direct;
            row.Cells["RecordingEnabled"].ToolTipText = !active ? UiText.T("Kamera zuerst aktivieren.") : direct ? UiText.T("Automatische Ereignisaufnahmen benötigen Bewegungs- oder Personenereignisse. Manuelle Aufnahmen sind weiterhin möglich.") : UiText.T("Automatische Aufnahmen für diese Kamera konfigurieren.");
        }
        var selectedHa = cameras.CurrentRow is { IsNewRow: false } selected && selected.Tag is not ReolinkCamera { Enabled: true } && !DirectCamera.ForRow(selected).Enabled;
        motionEntity.Enabled = personEntity.Enabled = selectedHa && homeAssistantEvents.Checked && detection.Checked;
        cameras.InvalidateColumn(cameras.Columns["PersonEnabled"].Index);
        cameras.InvalidateColumn(cameras.Columns["MotionEnabled"].Index);
        cameras.InvalidateColumn(cameras.Columns["RecordingEnabled"].Index);
        UpdateTabAppearance();
        SourceChanged?.Invoke();
    }
}
#endif
