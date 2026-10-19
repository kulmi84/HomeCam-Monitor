#if V2_BETA
using System.Drawing.Drawing2D;
namespace HomeCamMonitor;

// One stable content surface, moved as a whole. Native AutoScroll/ScrollWindowEx
// is deliberately not used, so scrolling cannot relayout or copy child fields.
internal sealed class ModernSettingsScrollArea : Panel
{
    // Buffer native child HWNDs together, not just each managed panel separately.
    private sealed class SettingsViewport : Panel
    {
        internal SettingsViewport() { DoubleBuffered = true; ResizeRedraw = true; }
        protected override CreateParams CreateParams
        {
            get { var value = base.CreateParams; value.ExStyle |= 0x02000000; return value; }
        }
    }
    private readonly Panel viewport = new SettingsViewport { Name = "SettingsViewport", Dock = DockStyle.Fill };
    private readonly Panel surface = new SettingsScrollPanel();
    private readonly DarkSettingsScrollBar bar = new() { Dock = DockStyle.Right };
    private Control? content;
    private bool updating;
    private int wheelRemainder;
    private int viewHoldDepth;
    internal int VerticalOffset => bar.Value;
    internal void ScrollWheel(MouseEventArgs e) => OnMouseWheel(e);
    public new Point AutoScrollPosition { get => new(0, -bar.Value); set => bar.Value = Math.Abs(value.Y); }
    internal void PreserveView(Action change)
    {
        var offset = bar.Value;
        var draw = viewHoldDepth == 0 && viewport.IsHandleCreated && viewport.Visible;
        viewHoldDepth++;
        content?.SuspendLayout();
        if (draw) SettingsPresentation.SetDrawing(viewport.Handle, false);
        try { change(); }
        finally
        {
            try
            {
                content?.ResumeLayout(true);
                viewHoldDepth--;
                UpdateMetrics();
                bar.Value = offset;
            }
            finally
            {
                if (draw)
                {
                    SettingsPresentation.SetDrawing(viewport.Handle, true);
                    SettingsPresentation.PaintAll(viewport.Handle);
                }
            }
        }
    }

    internal ModernSettingsScrollArea()
    {
        DoubleBuffered = true;
        AutoScroll = false;
        Controls.Add(viewport); Controls.Add(bar);
        viewport.Controls.Add(surface);
        bar.ValueChanged += (_, _) => MoveSurface();
        bar.MouseWheel += (_, e) => OnMouseWheel(e);
        viewport.SizeChanged += (_, _) => UpdateMetrics();
        bar.Width = Math.Max(14, 16 * DeviceDpi / 96);
    }

    internal void SetContent(Control value)
    {
        content = value;
        value.Dock = DockStyle.Top;
        surface.Controls.Add(value);
        value.SizeChanged += (_, _) => UpdateMetrics();
        WatchFocus(value);
        UpdateMetrics();
    }

    private void WatchFocus(Control control)
    {
        if (control is TextBoxBase or ComboBox or UpDownBase or ButtonBase or DataGridView)
            control.Enter += (_, _) => RevealControl(control);
        control.ControlAdded += (_, e) => { if (e.Control is { } added) WatchFocus(added); };
        foreach (Control child in control.Controls) WatchFocus(child);
    }

    private void RevealControl(Control control)
    {
        if (viewHoldDepth > 0 || !Visible || !control.Visible || !IsHandleCreated || !control.IsHandleCreated) return;
        var top = viewport.PointToClient(control.PointToScreen(Point.Empty)).Y + bar.Value;
        if (top < bar.Value || control.Height > viewport.ClientSize.Height) bar.Value = Math.Max(0, top - 8);
        else if (top + control.Height > bar.Value + viewport.ClientSize.Height)
            bar.Value = top + control.Height - viewport.ClientSize.Height + 8;
    }

    private void UpdateMetrics()
    {
        if (viewHoldDepth > 0 || updating || content is null || IsDisposed) return;
        updating = true;
        try
        {
            var width = Math.Max(1, viewport.ClientSize.Width);
            if (surface.Width != width) surface.Width = width;
            content.PerformLayout();
            surface.Height = content.Height;
            bar.SetMetrics(content.Height, viewport.ClientSize.Height);
        }
        finally { updating = false; }
        MoveSurface();
    }

    private void MoveSurface()
    {
        if (viewHoldDepth > 0 || updating || surface.Top == -bar.Value) return;
        surface.Top = -bar.Value;
        // WM_SETREDRAW toggles WS_VISIBLE and exposes partially painted child
        // controls while scrolling. Keep the viewport visible and compose one frame.
        viewport.Invalidate(true);
        viewport.Update();
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        wheelRemainder += e.Delta;
        var steps = wheelRemainder / 120;
        wheelRemainder %= 120;
        var lines = SystemInformation.MouseWheelScrollLines;
        var distance = lines < 0 ? viewport.ClientSize.Height : lines * Font.Height;
        bar.Value -= steps * distance;
        if (e is HandledMouseEventArgs handled) handled.Handled = true;
        base.OnMouseWheel(e);
    }
}

internal sealed class DarkSettingsScrollBar : Control
{
    private int value;
    private int maximum;
    private int contentHeight = 1;
    private int viewportHeight;
    private int dragOffset;
    private bool dragging;
    private bool hovered;
    internal event EventHandler? ValueChanged;
    internal int Value
    {
        get => value;
        set
        {
            var next = Math.Clamp(value, 0, maximum);
            if (this.value == next) return;
            this.value = next; Invalidate(); ValueChanged?.Invoke(this, EventArgs.Empty);
        }
    }
    internal DarkSettingsScrollBar()
    {
        Name = "ModernVerticalScrollBar"; AccessibleName = UiText.T("Einstellungen vertikal scrollen");
        AccessibleRole = AccessibleRole.ScrollBar; TabStop = true;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.Selectable, true);
    }
    internal void SetMetrics(int content, int viewport)
    {
        contentHeight = Math.Max(1, content); viewportHeight = Math.Max(0, viewport);
        maximum = Math.Max(0, contentHeight - viewportHeight);
        Value = value; Enabled = maximum > 0; Invalidate();
    }
    private Rectangle Thumb
    {
        get
        {
            var trackHeight = Math.Max(1, Height - 8);
            var height = Math.Min(trackHeight, Math.Max(28 * DeviceDpi / 96, (int)((long)trackHeight * viewportHeight / contentHeight)));
            var top = 4 + (maximum == 0 ? 0 : (int)((long)(trackHeight - height) * value / maximum));
            return new Rectangle(4, top, Math.Max(4, Width - 8), height);
        }
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(Color.FromArgb(10, 10, 12));
        if (maximum == 0) return;
        var thumb = Thumb;
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var diameter = Math.Min(thumb.Width, thumb.Height);
        using var path = new GraphicsPath();
        path.AddArc(thumb.Left, thumb.Top, diameter, diameter, 180, 90);
        path.AddArc(thumb.Right - diameter, thumb.Top, diameter, diameter, 270, 90);
        path.AddArc(thumb.Right - diameter, thumb.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(thumb.Left, thumb.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        using var brush = new SolidBrush(dragging || hovered || Focused ? Color.FromArgb(93, 93, 101) : Color.FromArgb(53, 53, 59));
        e.Graphics.FillPath(brush, path);
    }
    protected override void OnMouseEnter(EventArgs e) { hovered = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { hovered = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left) return;
        Focus();
        if (Thumb.Contains(e.Location)) { dragging = true; dragOffset = e.Y - Thumb.Top; Capture = true; }
        else Value += e.Y < Thumb.Top ? -viewportHeight : viewportHeight;
        Invalidate();
    }
    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (dragging) Value = (int)((long)Math.Clamp(e.Y - dragOffset - 4, 0, Math.Max(1, Height - 8 - Thumb.Height)) * maximum / Math.Max(1, Height - 8 - Thumb.Height));
    }
    protected override void OnMouseUp(MouseEventArgs e) { dragging = false; Capture = false; Invalidate(); base.OnMouseUp(e); }
    protected override void OnMouseCaptureChanged(EventArgs e) { if (!Capture) dragging = false; base.OnMouseCaptureChanged(e); }
    protected override bool IsInputKey(Keys keyData) => keyData is Keys.Up or Keys.Down or Keys.PageUp or Keys.PageDown or Keys.Home or Keys.End || base.IsInputKey(keyData);
    protected override void OnKeyDown(KeyEventArgs e)
    {
        switch (e.KeyCode)
        {
            case Keys.Up: Value -= Font.Height * 3; break;
            case Keys.Down: Value += Font.Height * 3; break;
            case Keys.PageUp: Value -= viewportHeight; break;
            case Keys.PageDown: Value += viewportHeight; break;
            case Keys.Home: Value = 0; break;
            case Keys.End: Value = maximum; break;
            default: base.OnKeyDown(e); return;
        }
        e.Handled = true;
    }
}
#endif
