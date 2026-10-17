#if V2_BETA
namespace HomeCamMonitor;

internal sealed class ConnectionSettingsSection : UserControl
{
    private readonly V2ConnectionTabs tabs;
    private readonly Button toggle = new() { Name = "ToggleConnectionSettings", AutoSize = true,
        Dock = DockStyle.Top, TextAlign = ContentAlignment.MiddleLeft, AccessibleRole = AccessibleRole.PushButton };
    private bool expanded;
    internal bool Expanded
    {
        get => expanded;
        set
        {
            if (expanded == value) return;
            void Change()
            {
                SuspendLayout();
                try { expanded = value; tabs.Visible = value; UpdateSummary(); }
                finally { ResumeLayout(true); }
            }
            for (var ancestor = Parent; ancestor is not null; ancestor = ancestor.Parent)
                if (ancestor is ModernSettingsScrollArea scroll) { scroll.PreserveView(Change); return; }
            Change();
        }
    }
    internal ConnectionSettingsSection(V2ConnectionTabs tabs)
    {
        this.tabs = tabs;
        tabs.Visible = false;
        Name = "ConnectionSettingsSection"; Dock = DockStyle.Top;
        AutoSize = true; AutoSizeMode = AutoSizeMode.GrowAndShrink; Margin = Padding.Empty;
        var layout = new SettingsLayoutPanel { Dock = DockStyle.Top, AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink, ColumnCount = 1, RowCount = 2, Margin = Padding.Empty };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.Controls.Add(toggle, 0, 0); layout.Controls.Add(tabs, 0, 1); Controls.Add(layout);
        toggle.Click += (_, _) => Expanded = !Expanded;
        tabs.SourceChanged += UpdateSummary;
        UpdateSummary();
    }
    private void UpdateSummary()
    {
        toggle.Text = (expanded ? "▼" : "▶") + " Verbindungseinstellungen – " + tabs.ConnectionSummary;
        toggle.AccessibleDescription = expanded ? "Verbindungseinstellungen einklappen" : "Verbindungseinstellungen ausklappen";
    }
}
internal sealed class MotionDetailsSection : UserControl
{
    private readonly Button toggle = new() { Name = "ToggleMotionDetails", AutoSize = true, Dock = DockStyle.Top, TextAlign = ContentAlignment.MiddleLeft };
    private readonly Control content;
    private string caption = "Einstellungen";
    private bool expanded;
    internal bool Expanded
    {
        get => expanded;
        set
        {
            if (expanded == value) return;
            void Change()
            {
                SuspendLayout();
                try { expanded = value; content.Visible = value; UpdateCaption(); }
                finally { ResumeLayout(true); }
            }
            for (var ancestor = Parent; ancestor is not null; ancestor = ancestor.Parent)
                if (ancestor is ModernSettingsScrollArea scroll) { scroll.PreserveView(Change); return; }
            Change();
        }
    }
    internal string Caption { set { caption = value; UpdateCaption(); } }
    internal MotionDetailsSection(Control content)
    {
        this.content = content; content.Visible = false;
        Name = "MotionDetailsSection"; Dock = DockStyle.Top; AutoSize = true; AutoSizeMode = AutoSizeMode.GrowAndShrink;
        var layout = new SettingsLayoutPanel { Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, ColumnCount = 1, RowCount = 2 };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.Controls.Add(toggle, 0, 0); layout.Controls.Add(content, 0, 1); Controls.Add(layout);
        toggle.Click += (_, _) => Expanded = !Expanded; UpdateCaption();
    }
    private void UpdateCaption()
    {
        toggle.Text = (expanded ? "▼" : "▶") + " " + caption;
        toggle.AccessibleDescription = expanded ? "Bewegungseinstellungen einklappen" : "Bewegungseinstellungen ausklappen";
    }
}
#endif
