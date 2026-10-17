
namespace HomeCamMonitor;

// Buffer the scrolling surface itself. Buffering only the form leaves nested
// panels erasing their backgrounds separately while Windows moves child HWNDs.
internal sealed class SettingsScrollPanel : Panel
{
    internal SettingsScrollPanel()
    {
        DoubleBuffered = true;
        ResizeRedraw = true;
    }

    protected override void OnScroll(ScrollEventArgs e)
    {
        base.OnScroll(e);
        // A scroll can expose old child-window pixels. Repaint the complete
        // subtree once at its final position rather than leaving copied pixels.
        Invalidate(true);
#if V2_BETA
        if (!batchingScroll && Visible && IsHandleCreated) SettingsPresentation.PaintAll(Handle);
#endif
    }

#if V2_BETA
    private bool batchingScroll;
    protected override void WndProc(ref Message message)
    {
        // Native scrolling moves child HWNDs separately. Keep the previous
        // frame until all moves finish, then paint the final positions once.
        if (AutoScroll && Visible && !batchingScroll && message.Msg is 0x0114 or 0x0115 or 0x020A)
        {
            var window = Handle;
            var previousPosition = DisplayRectangle.Location;
            batchingScroll = true;
            SettingsPresentation.SetDrawing(window, false);
            try { base.WndProc(ref message); }
            finally
            {
                SettingsPresentation.SetDrawing(window, true);
                batchingScroll = false;
                if (IsHandleCreated && !IsDisposed && DisplayRectangle.Location != previousPosition)
                    SettingsPresentation.PaintAll(window);
            }
            return;
        }
        base.WndProc(ref message);
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        var previousPosition = DisplayRectangle.Location;
        base.OnMouseWheel(e);
        if (DisplayRectangle.Location == previousPosition) return;
        Invalidate(true);
        if (!batchingScroll && Visible && IsHandleCreated) SettingsPresentation.PaintAll(Handle);
    }

    private Control? lastScrollFocus;
    protected override Point ScrollToControl(Control activeControl)
    {
        // A layout pass must not drag the same focused field back into view
        // after the user deliberately scrolled away. New keyboard focus still
        // follows the normal accessibility/tab-navigation behavior.
        if (ReferenceEquals(lastScrollFocus, activeControl)) return DisplayRectangle.Location;
        lastScrollFocus = activeControl;
        return base.ScrollToControl(activeControl);
    }
#endif
}

internal sealed class SettingsCameraGrid : DataGridView
{
    internal SettingsCameraGrid()
    {
#if V2_BETA
        DoubleBuffered = true;
#endif
    }
#if V2_BETA
    protected override void OnCellPainting(DataGridViewCellPaintingEventArgs e)
    {
        base.OnCellPainting(e);
        if (e.Handled || e.RowIndex < 0 || e.ColumnIndex < 0 ||
            Rows[e.RowIndex].Cells[e.ColumnIndex] is not DataGridViewCheckBoxCell cell || !cell.ReadOnly) return;
        e.Paint(e.ClipBounds, e.PaintParts & ~DataGridViewPaintParts.ContentForeground);
        var size = Math.Max(11, 13 * DeviceDpi / 96);
        var box = new Rectangle(e.CellBounds.X + (e.CellBounds.Width - size) / 2,
            e.CellBounds.Y + (e.CellBounds.Height - size) / 2, size, size);
        using var fill = new SolidBrush(Color.FromArgb(55, 55, 59));
        using var border = new Pen(Color.FromArgb(100, 100, 106));
        e.Graphics!.FillRectangle(fill, box);
        e.Graphics.DrawRectangle(border, box.X, box.Y, box.Width - 1, box.Height - 1);
        // Retain the stored preference visibly, even while the camera is off.
        if (Convert.ToBoolean(cell.Value ?? false))
        {
            using var tick = new Pen(Color.FromArgb(135, 135, 141), Math.Max(1.5f, 2f * DeviceDpi / 96));
            e.Graphics.DrawLines(tick, new Point[] { new Point(box.X + size / 4, box.Y + size / 2),
                new Point(box.X + size * 5 / 12, box.Y + size * 3 / 4),
                new Point(box.X + size * 4 / 5, box.Y + size / 4) });
        }
        e.Handled = true;
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        if (ScrollBars == ScrollBars.None)
        {
            for (var ancestor = Parent; ancestor is not null; ancestor = ancestor.Parent)
                if (ancestor is ModernSettingsScrollArea area) { area.ScrollWheel(e); return; }
        }
        base.OnMouseWheel(e);
    }
#endif
}

internal sealed class SettingsLayoutPanel : TableLayoutPanel
{
    internal SettingsLayoutPanel() { DoubleBuffered = true; ResizeRedraw = true; }
}

internal sealed class SettingsFlowPanel : FlowLayoutPanel
{
    internal SettingsFlowPanel() { DoubleBuffered = true; ResizeRedraw = true; }
}

