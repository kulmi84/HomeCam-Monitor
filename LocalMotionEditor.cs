#if V2_BETA
using OpenCvSharp;
using Point = System.Drawing.Point;
using Size = System.Drawing.Size;
namespace HomeCamMonitor;
internal sealed class MotionZoneCanvas : Control
{
    private Bitmap? image;
    private Point? dragStart;
    private Point dragEnd;
    internal List<MotionZone> Zones { get; set; } = [];
    internal event Action? ZonesChanged;
    internal MotionZoneCanvas() { DoubleBuffered = true; Height = 200; Dock = DockStyle.Top; BackColor = Color.Black; Cursor = Cursors.Cross; }
    private Rectangle Content
    {
        get { var width = Math.Min(Width, Height * 16 / 9); var height = width * 9 / 16; return new Rectangle((Width - width) / 2, (Height - height) / 2, width, height); }
    }
    internal void SetFrame(byte[] pixels)
    {
        using var mat = new Mat(LocalMotionModel.Height, LocalMotionModel.Width, MatType.CV_8UC1);
        System.Runtime.InteropServices.Marshal.Copy(pixels, 0, mat.Data, pixels.Length);
        Cv2.ImEncode(".png", mat, out var png);
        using var memory = new MemoryStream(png); using var source = Image.FromStream(memory);
        image?.Dispose(); image = new Bitmap(source); Invalidate();
    }
    internal void ClearImage() { image?.Dispose(); image = null; dragStart = null; Invalidate(); }
    private Point Clamp(Point point) { var c = Content; return new Point(Math.Clamp(point.X, c.Left, c.Right), Math.Clamp(point.Y, c.Top, c.Bottom)); }
    private MotionZone Zone(Point a, Point b)
    {
        var c = Content;
        return new MotionZone((double)(Math.Min(a.X, b.X) - c.Left) / c.Width, (double)(Math.Min(a.Y, b.Y) - c.Top) / c.Height,
            (double)Math.Abs(a.X - b.X) / c.Width, (double)Math.Abs(a.Y - b.Y) / c.Height);
    }
    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (image is null || !Content.Contains(e.Location)) return;
        if (e.Button == MouseButtons.Left && Zones.Count < 16) { dragStart = Clamp(e.Location); dragEnd = dragStart.Value; Capture = true; }
        if (e.Button == MouseButtons.Right)
        {
            var c = Content; var x = (double)(e.X - c.Left) / c.Width; var y = (double)(e.Y - c.Top) / c.Height;
            var index = Zones.FindLastIndex(z => x >= z.X && y >= z.Y && x <= z.X + z.Width && y <= z.Y + z.Height);
            if (index >= 0) { Zones.RemoveAt(index); ZonesChanged?.Invoke(); Invalidate(); }
        }
    }
    protected override void OnMouseMove(MouseEventArgs e) { base.OnMouseMove(e); if (dragStart is null) return; dragEnd = Clamp(e.Location); Invalidate(); }
    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (dragStart is not { } start) return;
        var zone = Zone(start, Clamp(e.Location)); dragStart = null; Capture = false;
        if (zone.Valid && zone.Width >= 0.01 && zone.Height >= 0.01) { Zones.Add(zone); ZonesChanged?.Invoke(); }
        Invalidate();
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e); var c = Content; if (c.Width <= 0 || c.Height <= 0) return;
        if (image is not null) e.Graphics.DrawImage(image, c);
        else TextRenderer.DrawText(e.Graphics, UiText.T("Vorschaubild laden, um Bereiche einzuzeichnen"), Font, ClientRectangle, Color.LightGray, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        using var pen = new Pen(Color.DeepSkyBlue, 2); using var fill = new SolidBrush(Color.FromArgb(35, Color.DeepSkyBlue));
        void Draw(MotionZone z) { var r = new RectangleF(c.Left + (float)z.X * c.Width, c.Top + (float)z.Y * c.Height, (float)z.Width * c.Width, (float)z.Height * c.Height); e.Graphics.FillRectangle(fill, r); e.Graphics.DrawRectangle(pen, r.X, r.Y, r.Width, r.Height); }
        foreach (var zone in Zones) Draw(zone);
        if (dragStart is { } start) Draw(Zone(start, dragEnd));
    }
    protected override void Dispose(bool disposing) { if (disposing) image?.Dispose(); base.Dispose(disposing); }
}
internal sealed class LocalMotionEditor : GroupBox
{
    private sealed record MotionSourceOption(bool Local, string Caption)
    {
        public override string ToString() => Caption;
    }
    private readonly DataGridView cameras;
    private readonly TextBox haAddress, haToken;
    private readonly CheckBox ignoreCertificate;
    private readonly DarkComboBox mode = new() { Name = "MotionSource", DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
    private readonly NumericUpDown sensitivity = new() { Name = "LocalMotionSensitivity", Minimum = 1, Maximum = 100, Value = 50, Width = 80 };
    private readonly NumericUpDown area = new() { Name = "LocalMotionMinimumArea", Minimum = .05m, Maximum = 50, Value = 1, DecimalPlaces = 2, Increment = .25m, Width = 80 };
    private readonly NumericUpDown duration = new() { Name = "LocalMotionDuration", Minimum = .2m, Maximum = 10, Value = .8m, DecimalPlaces = 1, Increment = .2m, Width = 80 };
    private readonly NumericUpDown hold = new() { Name = "LocalMotionHold", Minimum = .5m, Maximum = 30, Value = 2, DecimalPlaces = 1, Increment = .5m, Width = 80 };
    private readonly MotionZoneCanvas canvas = new() { Name = "LocalMotionZones" };
    private readonly Label status = new() { Name = "LocalMotionStatus", AutoSize = true, MaximumSize = new Size(800, 0) };
    private readonly Label zonesLabel = new() { AutoSize = true };
    private readonly SettingsLayoutPanel options = new() { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2 };
    private readonly Button preview = new() { Name = "LoadMotionPreview", Text = UiText.T("Vorschaubild laden"), AutoSize = true };
    private readonly System.Windows.Forms.Timer timer = new() { Interval = 1000 };
    private readonly Control externalFields;
    private readonly MotionDetailsSection details;
    private int shownMode = -1;
    private bool loading;
    private DataGridViewRow? loadedRow;
    private CancellationTokenSource? previewCancel;
    internal event Action? Changed;
    internal LocalMotionEditor(DataGridView cameras, TextBox haAddress, TextBox haToken, CheckBox ignoreCertificate, Control externalFields)
    {
        this.externalFields = externalFields;
        this.cameras = cameras; this.haAddress = haAddress; this.haToken = haToken; this.ignoreCertificate = ignoreCertificate;
        Name = "LocalMotionSettings"; Text = UiText.T("Bewegungserkennung"); AutoSize = true; Dock = DockStyle.Top; Padding = new Padding(10, 18, 10, 10);
        var root = new SettingsLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 1 };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); root.Controls.Add(mode, 0, 0);
        options.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); options.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        void Field(string label, Control input, int row) { options.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left }, 0, row); options.Controls.Add(input, 1, row); }
        Field(UiText.T("Empfindlichkeit (höher = empfindlicher):"), sensitivity, 0); Field(UiText.T("Mindestfläche (% des überwachten Bereichs):"), area, 1);
        Field(UiText.T("Mindestdauer (Sekunden):"), duration, 2); Field(UiText.T("Nachlauf bis zum nächsten Ereignis (Sekunden):"), hold, 3);
        var tools = new SettingsFlowPanel { Dock = DockStyle.Top, AutoSize = true };
        var clear = new Button { Text = UiText.T("Gesamtes Bild überwachen"), AutoSize = true };
        tools.Controls.AddRange([preview, clear, zonesLabel]); options.Controls.Add(tools, 0, 4); options.SetColumnSpan(tools, 2);
        options.Controls.Add(canvas, 0, 5); options.SetColumnSpan(canvas, 2);
        var hint = new Label { Text = UiText.T("Linke Maustaste: Bereich aufziehen. Rechtsklick: Bereich entfernen. Ohne Bereiche wird das ganze Bild überwacht. Maximal 16 Bereiche.\n320 × 180 Pixel, maximal 5 Bilder/s, Einlernphase nach Start/Abbruch. Allgemeine Bewegung, keine Personenerkennung. Zusätzlicher Decoder benötigt CPU-Leistung.\nMarkierung und Aufnahmeaktionen verwenden die Optionen dieser Kamera. „Bewegung“ oben muss aktiv sein; Aufnahmen zusätzlich freigeben."), AutoSize = true, MaximumSize = new Size(850, 0) };
        options.Controls.Add(hint, 0, 6); options.SetColumnSpan(hint, 2);
        var body = new SettingsLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 1 };
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        body.Controls.Add(options, 0, 0); body.Controls.Add(externalFields, 0, 1); body.Controls.Add(status, 0, 2);
        details = new MotionDetailsSection(body); root.Controls.Add(details, 0, 1); Controls.Add(root);
        mode.SelectedIndexChanged += (_, _) =>
        {
            if (loading || cameras.CurrentRow is not { IsNewRow: false } row) return;
            var config = LocalMotionSettings.ForRow(row); config.Enabled = mode.SelectedItem is MotionSourceOption { Local: true };
            if (config.Enabled) row.Cells["MotionEnabled"].Value = true;
            UpdateDetails(config); Store(); Changed?.Invoke();
        };
        foreach (var input in new[] { sensitivity, area, duration, hold }) input.ValueChanged += (_, _) => Store();
        canvas.ZonesChanged += Store; clear.Click += (_, _) => { canvas.Zones.Clear(); canvas.Invalidate(); Store(); };
        preview.Click += async (_, _) => await LoadPreview();
        cameras.SelectionChanged += (_, _) => LoadSelection(); cameras.CurrentCellChanged += (_, _) => LoadSelection();
        cameras.CellValueChanged += (_, e) => { if (!loading && e.RowIndex >= 0 && e.ColumnIndex >= 0 && ReferenceEquals(cameras.CurrentRow, cameras.Rows[e.RowIndex]) && cameras.Columns[e.ColumnIndex].Name is "Active" or "CameraName" or "MotionEnabled") LoadSelection(); };
        cameras.CellValueChanged += (_, e) => { if (e.RowIndex >= 0 && e.ColumnIndex >= 0 && ReferenceEquals(cameras.CurrentRow, cameras.Rows[e.RowIndex]) && cameras.Columns[e.ColumnIndex].Name == "StreamUrl") { previewCancel?.Cancel(); canvas.ClearImage(); } };
        timer.Tick += (_, _) => RefreshStatus(); timer.Start(); LoadSelection();
    }
    internal void LoadSelection()
    {
        if (loading || CameraGridUpdate.IsActive(cameras)) return;
        var row = cameras.CurrentRow is { IsNewRow: false } current ? current : null;
        if (!ReferenceEquals(row, loadedRow)) { previewCancel?.Cancel(); canvas.ClearImage(); loadedRow = row; details.Expanded = false; }
        loading = true;
        try
        {
            Enabled = row is not null && Convert.ToBoolean(row.Cells["Active"].Value ?? true);
            var show = Enabled && row is not null && Convert.ToBoolean(row.Cells["MotionEnabled"].Value ?? false);
            Visible = show;
            if (!show) { details.Expanded = false; previewCancel?.Cancel(); canvas.ClearImage(); }
            var config = row is null ? new LocalMotionSettings() : LocalMotionSettings.ForRow(row);
            config.Validate(); Text = UiText.T("Bewegungserkennung – ") + (row is null ? UiText.T("Keine Kamera ausgewählt") : Convert.ToString(row.Cells["CameraName"].Value));
            var direct = row is not null && DirectCamera.ForRow(row).Enabled;
            if (direct && show) config.Enabled = true;
            UpdateSourceChoices(row, direct);
            mode.SelectedIndex = direct ? 0 : config.Enabled ? 1 : 0;
            sensitivity.Value = config.Sensitivity; area.Value = (decimal)config.MinimumAreaPercent;
            duration.Value = (decimal)config.MinimumSeconds; hold.Value = (decimal)config.HoldSeconds;
            canvas.Zones = config.Zones.ToList(); UpdateDetails(config); zonesLabel.Text = config.Zones.Count == 0 ? UiText.T("Gesamtes Bild") : config.Zones.Count + UiText.T(" Bereich(e)");
        }
        finally { loading = false; }
        RefreshStatus();
    }
    private void UpdateSourceChoices(DataGridViewRow? row, bool direct)
    {
        var external = row?.Tag is ReolinkCamera { Enabled: true }
            ? UiText.T("Bewegungserkennung der Reolink-Kamera") : UiText.T("Externer Home-Assistant-Sensor");
        var choices = direct
            ? new[] { new MotionSourceOption(true, UiText.T("Lokale Bildanalyse (OpenCV/MOG2)")) }
            : new[] { new MotionSourceOption(false, external), new MotionSourceOption(true, UiText.T("Lokale Bildanalyse (OpenCV/MOG2)")) };
        if (mode.Items.Cast<MotionSourceOption>().SequenceEqual(choices)) return;
        mode.Items.Clear(); mode.Items.AddRange(choices.Cast<object>().ToArray());
    }
    private void UpdateDetails(LocalMotionSettings config)
    {
        var currentMode = config.Enabled ? 1 : 0;
        if (shownMode != currentMode) { details.Expanded = false; shownMode = currentMode; previewCancel?.Cancel(); }
        options.Visible = config.Enabled;
        var row = cameras.CurrentRow;
        var ha = row is { IsNewRow: false } && row.Tag is not ReolinkCamera { Enabled: true } && !DirectCamera.ForRow(row).Enabled;
        externalFields.Visible = !config.Enabled && ha;
        details.Caption = config.Enabled ? UiText.T("Lokale Bildanalyse – Einstellungen und Bereiche") : row?.Tag is ReolinkCamera { Enabled: true } ? UiText.T("Reolink-Kamera – Bewegungsereignisse") : UiText.T("Home Assistant – Einstellungen und Sensoren");
    }
    private void Store()
    {
        if (loading || cameras.CurrentRow is not { IsNewRow: false } row) return;
        var config = LocalMotionSettings.ForRow(row); config.Sensitivity = (int)sensitivity.Value; config.MinimumAreaPercent = (double)area.Value;
        config.MinimumSeconds = (double)duration.Value; config.HoldSeconds = (double)hold.Value; config.Zones = canvas.Zones.ToList();
        zonesLabel.Text = config.Zones.Count == 0 ? UiText.T("Gesamtes Bild") : config.Zones.Count + UiText.T(" Bereich(e)"); Changed?.Invoke(); RefreshStatus();
    }
    private void RefreshStatus()
    {
        if (cameras.CurrentRow is not { IsNewRow: false } row) { status.Text = UiText.T("Kamera auswählen."); return; }
        var config = LocalMotionSettings.ForRow(row);
        status.Text = config.Enabled ? (!Convert.ToBoolean(row.Cells["Active"].Value ?? true) || !Convert.ToBoolean(row.Cells["MotionEnabled"].Value ?? false) ? UiText.T("Lokale Analyse ausgeschaltet: Kamera bzw. Bewegung ist deaktiviert.") : LocalMotionStatus.Get(config.Id)) : (row.Tag is ReolinkCamera { Enabled: true } ? UiText.T("Bewegungsereignisse werden direkt über Reolink-UID empfangen. Eine Sensorzuordnung ist nicht erforderlich.") : DirectCamera.ForRow(row).Enabled ? UiText.T("Diese direkte Bildverbindung liefert keine externen Ereignisse. Für Bewegung bitte lokale Bildanalyse auswählen.") : UiText.T("Externe HA-Sensoren verwenden die bestehende Home-Assistant-Verbindung. Personenereignisse bleiben auch bei lokaler Bildanalyse unabhängig nutzbar."));
    }
    private async Task LoadPreview()
    {
        if (cameras.CurrentRow is not { IsNewRow: false } row) return;
        previewCancel?.Cancel(); using var active = new CancellationTokenSource(TimeSpan.FromSeconds(25)); previewCancel = active; preview.Enabled = false;
        try
        {
            var url = Convert.ToString(row.Cells["StreamUrl"].Value) ?? "";
            if (DirectCamera.ForRow(row).Enabled) url = DirectCamera.ForRow(row).StreamUrl();
            using var bridge = HaApi.Entity(url) is { } entity ? new HomeAssistantStreams(haAddress.Text, haToken.Text, ignoreCertificate.Checked, [entity]) : null;
            if (bridge is not null) url = bridge.Url(HaApi.Entity(url)!);
            var pixels = await LocalMotionWorker.Preview(url, active.Token); active.Token.ThrowIfCancellationRequested();
            if (!IsDisposed && ReferenceEquals(row, cameras.CurrentRow)) canvas.SetFrame(pixels);
        }
        catch (Exception e) when (e is OperationCanceledException or IOException or ArgumentException or System.ComponentModel.Win32Exception or OpenCVException or DllNotFoundException or TypeInitializationException)
        { if (!IsDisposed) status.Text = UiText.T("Vorschaubild nicht verfügbar. Kamera zuerst speichern/verbinden und erneut versuchen."); }
        finally { if (ReferenceEquals(previewCancel, active)) previewCancel = null; if (!IsDisposed) preview.Enabled = true; }
    }
    protected override void Dispose(bool disposing) { if (disposing) { previewCancel?.Cancel(); timer.Dispose(); } base.Dispose(disposing); }
}
#endif
