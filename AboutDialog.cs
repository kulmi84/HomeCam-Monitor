#if BETA
using System.ComponentModel;
using System.Diagnostics;

namespace HomeCamMonitor;

internal sealed class AboutDialog : Form
{
    internal const string ProjectUrl = "https://github.com/kulmi84/HomeCam-Monitor";
    internal const string IssuesUrl = ProjectUrl + "/issues/new";
    internal const string GuideUrl = ProjectUrl + "#readme";
    internal const string NoticesUrl = ProjectUrl + "/blob/main/THIRD-PARTY-NOTICES-V2.md";

    internal AboutDialog()
    {
        Text = UiText.T("Über HomeCam Monitor");
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false; MinimizeBox = false; ShowInTaskbar = false;
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(540, 350);
        Font = new Font("Segoe UI", 9);
        ApplicationBranding.ApplyWindowIcon(this);
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 9, Padding = new Padding(22) };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.Controls.Add(new Label { Text = ApplicationBranding.ProductName, AutoSize = true,
            Font = new Font("Segoe UI", 15, FontStyle.Bold), Margin = new Padding(0, 0, 0, 8) }, 0, 0);
        var fileVersion = FileVersionInfo.GetVersionInfo(Application.ExecutablePath).FileVersion ?? "";
        layout.Controls.Add(new Label { Name = "AboutVersion", AutoSize = true,
            Text = UiText.F("Version {0} · Build {1}", Application.ProductVersion.Split('+')[0], fileVersion),
            Margin = new Padding(0, 0, 0, 6) }, 0, 1);
        layout.Controls.Add(new Label { Name = "AboutAuthor", AutoSize = true,
            Text = UiText.F("Entwickelt von {0}", "Marcin Kulmaczewski"),
            Margin = new Padding(0, 0, 0, 18) }, 0, 2);
        AddLink("AboutProject", "Projekt auf GitHub", () => OpenLink(this, ProjectUrl), 3);
        AddLink("AboutIssues", "Fehler melden / Verbesserung vorschlagen", () => OpenLink(this, IssuesUrl), 4);
        AddLink("AboutGuide", "Anleitung öffnen", () => OpenLink(this, GuideUrl), 5);
        AddLink("AboutLicenses", "Lizenz- und Komponentenhinweise", ShowNotices, 6);
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        for (var row = 2; row < 7; row++) layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        var close = new Button { Text = UiText.T("Schließen"), AutoSize = true, DialogResult = DialogResult.Cancel,
            Anchor = AnchorStyles.Right, Margin = new Padding(0, 12, 0, 0) };
        layout.Controls.Add(close, 0, 8);
        Controls.Add(layout); AcceptButton = close; CancelButton = close;
        SettingsForm.ApplyDarkTheme(this);
        foreach (var link in layout.Controls.OfType<LinkLabel>())
        { link.LinkColor = Color.LightSkyBlue; link.ActiveLinkColor = Color.White; link.VisitedLinkColor = Color.LightSkyBlue; }

        void AddLink(string name, string text, Action action, int row)
        {
            var link = new LinkLabel { Name = name, Text = UiText.T(text), AutoSize = true,
                Margin = new Padding(0, 0, 0, 12), TabStop = true };
            link.LinkClicked += (_, _) => action();
            layout.Controls.Add(link, 0, row);
        }
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        var darkMode = 1;
        NativeMethods.DwmSetWindowAttribute(Handle, NativeMethods.DwmWindowAttribute.UseImmersiveDarkMode, ref darkMode, sizeof(int));
        NativeMethods.SetDiagnosticCaptionColor(Handle, Color.FromArgb(24, 24, 27));
    }

    private void ShowNotices()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "THIRD-PARTY-NOTICES-V2.md");
        if (!File.Exists(path)) { OpenLink(this, NoticesUrl); return; }
        try
        {
            using var viewer = new Form { Text = UiText.T("Lizenz- und Komponentenhinweise"),
                StartPosition = FormStartPosition.CenterParent, Size = new Size(800, 560), MinimizeBox = false, ShowInTaskbar = false };
            var text = new RichTextBox { Dock = DockStyle.Fill, ReadOnly = true, DetectUrls = true,
                Text = File.ReadAllText(path), Font = new Font("Segoe UI", 9), BackColor = Color.FromArgb(24, 24, 27),
                ForeColor = Color.Gainsboro, BorderStyle = BorderStyle.None };
            text.LinkClicked += (_, args) => { if (Uri.TryCreate(args.LinkText, UriKind.Absolute, out var uri) && uri.Scheme == "https") OpenLink(viewer, uri.AbsoluteUri); };
            viewer.Controls.Add(text); viewer.ShowDialog(this);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        { MessageBox.Show(this, UiText.T("Die Lizenzhinweise konnten nicht geöffnet werden."), Text, MessageBoxButtons.OK, MessageBoxIcon.Warning); }
    }

    private static void OpenLink(IWin32Window owner, string address)
    {
        try { Process.Start(new ProcessStartInfo(address) { UseShellExecute = true }); }
        catch (Exception error) when (error is Win32Exception or InvalidOperationException)
        { MessageBox.Show(owner, UiText.F("Der Link konnte nicht geöffnet werden:\n{0}", address), UiText.T("Link öffnen"), MessageBoxButtons.OK, MessageBoxIcon.Warning); }
    }
}
#endif
