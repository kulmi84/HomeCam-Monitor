namespace HomeCamMonitor;

// Keep ComboBox's selection/data API; replace the light native popup in V2.
internal sealed class DarkComboBox : ComboBox
{
#if V2_BETA
    private ToolStripDropDown? popup;
    internal bool IsListOpen => popup is { Visible: true };
    internal DarkComboBox()
    {
        DrawMode = DrawMode.OwnerDrawFixed; FlatStyle = FlatStyle.Flat;
        BackColor = Color.FromArgb(25, 25, 29); ForeColor = Color.White;
        ItemHeight = Font.Height + 6;
    }
    protected override void OnFontChanged(EventArgs e) { base.OnFontChanged(e); ItemHeight = Font.Height + 6; }
    protected override void OnDrawItem(DrawItemEventArgs e)
    {
        using var brush = new SolidBrush(BackColor); e.Graphics.FillRectangle(brush, e.Bounds);
        var text = e.Index >= 0 && e.Index < Items.Count ? GetItemText(Items[e.Index]) : Text;
        TextRenderer.DrawText(e.Graphics, text, Font, new Rectangle(e.Bounds.X + 5, e.Bounds.Y, Math.Max(0, e.Bounds.Width - 8), e.Bounds.Height),
            Enabled ? ForeColor : Color.FromArgb(125, 125, 132), TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
    }
    protected override void WndProc(ref Message message)
    {
        if (DropDownStyle == ComboBoxStyle.DropDownList && message.Msg is 0x0201 or 0x0203)
        { if (Enabled) { Focus(); if (IsListOpen) popup!.Close(); else OpenList(); } return; }
        if (DropDownStyle == ComboBoxStyle.DropDownList && message.Msg == 0x0202) return;
        base.WndProc(ref message);
        if (message.Msg is 0x000F or 0x0317 or 0x0318)
        {
            if (message.Msg != 0x000F && message.WParam != IntPtr.Zero)
            { using var graphics = Graphics.FromHdc(message.WParam); DrawChrome(graphics); }
            else { using var graphics = Graphics.FromHwnd(Handle); DrawChrome(graphics); }
        }
    }
    private void DrawChrome(Graphics graphics)
    {
        using var surface = new SolidBrush(Color.FromArgb(25, 25, 29)); graphics.FillRectangle(surface, ClientRectangle);
        var width = Math.Max(22, 24 * DeviceDpi / 96);
        var text = SelectedIndex >= 0 ? GetItemText(SelectedItem) : Text;
        TextRenderer.DrawText(graphics, text, Font, new Rectangle(6, 1, Math.Max(0, Width - width - 8), Math.Max(0, Height - 2)),
            Enabled ? ForeColor : Color.FromArgb(125, 125, 132), TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
        var rectangle = new Rectangle(Math.Max(0, Width - width), 1, width - 1, Math.Max(0, Height - 2));
        using var background = new SolidBrush(Color.FromArgb(25, 25, 29)); graphics.FillRectangle(background, rectangle);
        using var border = new Pen(Focused || IsListOpen ? Color.FromArgb(100, 100, 110) : Color.FromArgb(62, 62, 69));
        graphics.DrawRectangle(border, 0, 0, Math.Max(0, Width - 1), Math.Max(0, Height - 1));
        using var arrow = new SolidBrush(Enabled ? Color.FromArgb(205, 205, 212) : Color.FromArgb(95, 95, 102));
        var x = Width - width / 2; var y = Height / 2;
        graphics.FillPolygon(arrow, new Point[] { new(x - 4, y - 2), new(x + 4, y - 2), new(x, y + 2) });
    }
    protected override bool ProcessCmdKey(ref Message message, Keys keyData)
    {
        if (keyData is Keys.F4 or Keys.Space || keyData == (Keys.Alt | Keys.Down))
        { if (IsListOpen) popup!.Close(); else OpenList(); return true; }
        return base.ProcessCmdKey(ref message, keyData);
    }
    internal void OpenList()
    {
        if (!Enabled || IsListOpen || Items.Count == 0) return;
        var list = CreateList();
        list.Show(this, new Point(0, Height));
        var area = (ModernSettingsScrollArea)((ToolStripControlHost)list.Items[0]).Control;
        area.AutoScrollPosition = new Point(0, Math.Max(0, SelectedIndex) * Math.Max(ItemHeight, Font.Height + 8));
        area.Controls.Find("DropdownItems", true).Single().Focus(); Invalidate();
    }
    internal ToolStripDropDown CreateList()
    {
        var rowHeight = Math.Max(ItemHeight, Font.Height + 8);
        var height = Math.Min(Items.Count, 10) * rowHeight;
        var area = new ModernSettingsScrollArea { Size = new Size(Math.Max(Width, DropDownWidth), height) };
        var items = new PopupItems(this, area, rowHeight) { Name = "DropdownItems", Height = Items.Count * rowHeight, Font = Font, TabStop = true };
        area.SetContent(items);
        var list = new ToolStripDropDown { AutoSize = false, Padding = new Padding(1), Size = new Size(area.Width + 2, area.Height + 2),
            Renderer = new DarkPopupRenderer(), BackColor = Color.FromArgb(18, 18, 22) };
        list.Items.Add(new ToolStripControlHost(area) { AutoSize = false, Size = area.Size, Margin = Padding.Empty, Padding = Padding.Empty });
        popup = list;
        list.Closed += (_, _) =>
        {
            if (ReferenceEquals(popup, list)) popup = null;
            if (!IsDisposed) Invalidate();
            // ToolStripDropDown still uses its HWND while Close unwinds.
            // Dispose on the next UI turn, after that native call has returned.
            if (list.IsHandleCreated && !list.IsDisposed) list.BeginInvoke(new Action(list.Dispose));
        };
        return list;
    }
    protected override void OnMouseWheel(MouseEventArgs e)
    {
        if (!IsListOpen)
            for (var ancestor = Parent; ancestor is not null; ancestor = ancestor.Parent)
                if (ancestor is ModernSettingsScrollArea area) { area.ScrollWheel(e); return; }
        base.OnMouseWheel(e);
    }
    private void Commit(int index)
    {
        SelectedIndex = index; popup?.Close(); if (!IsDisposed) Focus();
    }
    protected override void Dispose(bool disposing) { if (disposing) popup?.Dispose(); base.Dispose(disposing); }

    private sealed class DarkPopupRenderer : ToolStripProfessionalRenderer
    {
        protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e) => e.Graphics.Clear(Color.FromArgb(18, 18, 22));
        protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
        { using var pen = new Pen(Color.FromArgb(62, 62, 69)); e.Graphics.DrawRectangle(pen, 0, 0, e.ToolStrip.Width - 1, e.ToolStrip.Height - 1); }
    }
    private sealed class PopupItems : Control
    {
        private readonly DarkComboBox owner;
        private readonly ModernSettingsScrollArea area;
        private readonly int rowHeight;
        private int selected;
        internal PopupItems(DarkComboBox owner, ModernSettingsScrollArea area, int rowHeight)
        {
            this.owner = owner; this.area = area; this.rowHeight = rowHeight;
            selected = Math.Max(0, owner.SelectedIndex); DoubleBuffered = true;
            AccessibleRole = AccessibleRole.List;
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(Color.FromArgb(20, 20, 24));
            var first = Math.Max(0, e.ClipRectangle.Top / rowHeight);
            var last = Math.Min(owner.Items.Count - 1, e.ClipRectangle.Bottom / rowHeight);
            for (var i = first; i <= last; i++)
            {
                var row = new Rectangle(0, i * rowHeight, Width, rowHeight);
                if (i == selected) { using var brush = new SolidBrush(Color.FromArgb(53, 53, 62)); e.Graphics.FillRectangle(brush, row); }
                row.Inflate(-7, 0);
                TextRenderer.DrawText(e.Graphics, owner.GetItemText(owner.Items[i]), Font, row, Color.FromArgb(242, 242, 246),
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
            }
        }
        protected override void OnMouseMove(MouseEventArgs e)
        { base.OnMouseMove(e); var next = Math.Clamp(e.Y / rowHeight, 0, owner.Items.Count - 1); if (selected != next) { selected = next; Invalidate(); } }
        protected override void OnMouseDown(MouseEventArgs e) { if (e.Button == MouseButtons.Left) owner.Commit(Math.Clamp(e.Y / rowHeight, 0, owner.Items.Count - 1)); base.OnMouseDown(e); }
        protected override void OnMouseWheel(MouseEventArgs e) { area.ScrollWheel(e); base.OnMouseWheel(e); }
        protected override bool IsInputKey(Keys keyData) => keyData is Keys.Up or Keys.Down or Keys.Home or Keys.End or Keys.PageUp or Keys.PageDown || base.IsInputKey(keyData);
        protected override void OnKeyDown(KeyEventArgs e)
        {
            switch (e.KeyCode)
            {
                case Keys.Up: selected--; break;
                case Keys.Down: selected++; break;
                case Keys.Home: selected = 0; break;
                case Keys.End: selected = owner.Items.Count - 1; break;
                case Keys.PageUp: selected -= 10; break;
                case Keys.PageDown: selected += 10; break;
                case Keys.Enter: owner.Commit(selected); e.Handled = true; return;
                case Keys.Escape: owner.popup?.Close(); owner.Focus(); e.Handled = true; return;
                default: base.OnKeyDown(e); return;
            }
            selected = Math.Clamp(selected, 0, owner.Items.Count - 1);
            var top = selected * rowHeight;
            if (top < area.VerticalOffset) area.AutoScrollPosition = new Point(0, top);
            else if (top + rowHeight > area.VerticalOffset + area.ClientSize.Height) area.AutoScrollPosition = new Point(0, top + rowHeight - area.ClientSize.Height);
            Invalidate(); e.Handled = true;
        }
    }
#endif
}
