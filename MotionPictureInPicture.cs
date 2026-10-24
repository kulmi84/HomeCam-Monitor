#if BETA
using System.Diagnostics;
namespace HomeCamMonitor;

internal sealed partial class MonitorForm
{
    private MotionPreviewWindow? motionPreview;
    private MotionPreviewInputWindow? motionPreviewInput;
    private Panel? motionPreviewVideo;
    private CameraPlaceholderPanel? motionPreviewPlaceholder;
    private Process? motionPreviewPlayer;
    private string? motionPreviewPipeName;
    private CancellationTokenSource? motionPreviewCancellation;
    private readonly System.Windows.Forms.Timer motionPreviewTimer = new();
    private int motionPreviewCamera = -1;
    private bool motionPreviewEditing;
    private bool motionPreviewInteracting;
    private int motionPreviewResizeEdges;
    private Point motionPreviewPointer;
    private Rectangle motionPreviewStart;

    private Rectangle MotionPreviewClientBounds => motionPreview is null ? Rectangle.Empty :
        new Rectangle(PointToClient(motionPreview.Location), motionPreview.Size);

    private void EnsureMotionPreview()
    {
        if (motionPreview is not null) return;
        motionPreview = new MotionPreviewWindow { Owner = this };
        motionPreviewVideo = new Panel { Dock = DockStyle.Fill, BackColor = Color.Black };
        motionPreviewPlaceholder = new CameraPlaceholderPanel { Dock = DockStyle.Fill };
        motionPreview.Controls.Add(motionPreviewVideo);
        motionPreview.Controls.Add(motionPreviewPlaceholder);
        motionPreviewInput = new MotionPreviewInputWindow(this) { Owner = motionPreview };
        motionPreviewTimer.Tick += (_, _) => ExpireMotionPreview(IsMotionPreviewAt(Cursor.Position));
        motionPreviewPlaceholder.Paint += (_, e) =>
        {
            if (!motionPreviewEditing) return;
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using var pen = new Pen(Color.FromArgb(180,180,188), 2);
            var right = motionPreviewPlaceholder.Width - 12; var bottom = motionPreviewPlaceholder.Height - 12;
            for (var i = 0; i < 3; i++) e.Graphics.DrawLine(pen, right - 16 + i*6, bottom, right, bottom - 16 + i*6);
            TextRenderer.DrawText(e.Graphics, UiText.T("Ziehen zum Verschieben · Ränder zum Vergrößern"),
                Font, new Rectangle(12, Math.Max(0,bottom-32), Math.Max(1,right-12),22), Color.Silver,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.EndEllipsis);
        };
    }

    private void PresentMotionPreview()
    {
        LayoutMotionPreview();
        motionPreview!.TopMost = TopMost;
        if (!motionPreview.Visible) motionPreview.Show(this);
        motionPreviewInput!.TopMost = TopMost;
        motionPreviewInput.ContextMenuStrip = cameraContextMenu;
        if (!motionPreviewInput.Visible) motionPreviewInput.Show(motionPreview);
        motionPreview.BringToFront(); motionPreviewInput.BringToFront();
    }

    private void ShowMotionPreview(int cameraIndex)
    {
        if (motionPreviewEditing || cameraIndex == settings.SelectedCamera) return;
        EnsureMotionPreview();
        motionPreviewTimer.Stop();
        motionPreviewTimer.Interval = Math.Clamp(settings.MotionForegroundSeconds, 3, 300) * 1000;
        motionPreviewTimer.Start();
        PresentMotionPreview();
        if (motionPreviewCamera == cameraIndex && IsPlayerRunning(motionPreviewPlayer)) return;
        StopMotionPreviewPlayer(); motionPreviewCamera = cameraIndex;
        var camera = settings.Cameras[cameraIndex];
        motionPreviewPlaceholder!.Configure(camera.Name);
        motionPreviewPlaceholder.Show(); motionPreviewPlaceholder.BringToFront();
        motionPreviewVideo!.Show();
        var pipe = $"HomeCamMonitor-PiP-{Environment.ProcessId}-{Guid.NewGuid():N}";
        motionPreviewPipeName = pipe;
        var start = new ProcessStartInfo(Path.Combine(AppContext.BaseDirectory, "mpv.exe"))
        { UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden };
        foreach (var argument in new[]
        {
            $"--wid={motionPreviewVideo.Handle.ToInt64()}", "--no-terminal", "--really-quiet", "--no-audio",
            "--no-osc", "--no-border", "--profile=low-latency", "--cache=no",
            "--demuxer-lavf-o=rtsp_transport=tcp", "--hwdec=auto-safe", "--vo=gpu-next",
            "--gpu-api=d3d11", "--keepaspect-window=no", "--keep-open=no",
            "--input-ipc-server=" + @"\\.\pipe\" + pipe, camera.StreamUrl
        }) start.ArgumentList.Add(argument);
#if V2_BETA
        ReolinkPlayback.Configure(start, camera.Reolink.Enabled);
#endif
        try
        {
            motionPreviewCancellation = new CancellationTokenSource();
            var started = Process.Start(start) ?? throw new InvalidOperationException();
            motionPreviewPlayer = started;
            _ = RevealWhenReadyAsync(started, pipe, motionPreviewCancellation.Token,
                () => fullscreen && !gridMode && ReferenceEquals(motionPreviewPlayer, started),
                () =>
                {
                    NativeMethods.PrepareVideoChildren(motionPreviewVideo!.Handle);
                    motionPreviewPlaceholder.Hide(); motionPreviewVideo.BringToFront(); motionPreview!.BringToFront();
                },
                () => { StopMotionPreviewPlayer(); motionPreviewPlaceholder!.SetOffline(); motionPreviewPlaceholder.Show(); motionPreviewPlaceholder.BringToFront(); },
                cameraIndex);
        }
        catch { StopMotionPreviewPlayer(); motionPreviewPlaceholder.SetOffline(); }
    }

    private static double ValidFraction(double value, double fallback) => double.IsFinite(value) && value > 0 ? Math.Clamp(value,.05,1) : fallback;

    private void LayoutMotionPreview()
    {
        if (motionPreview is null) return;
        var available = ClientSize;
        if (available.Width < 1 || available.Height < 1) return;
        var margin = Math.Max(12, available.Width / 50);
        var width = Math.Clamp((int)(available.Width*ValidFraction(settings.MotionPreviewWidth,.31)), Math.Min(160,available.Width),available.Width);
        var height = Math.Clamp((int)(available.Height*ValidFraction(settings.MotionPreviewHeight,.31)),Math.Min(90,available.Height),available.Height);
        var x = settings.MotionPreviewX < 0 || !double.IsFinite(settings.MotionPreviewX) ? available.Width-width-margin : (int)(available.Width*settings.MotionPreviewX);
        var y = settings.MotionPreviewY < 0 || !double.IsFinite(settings.MotionPreviewY) ? available.Height-height-margin : (int)(available.Height*settings.MotionPreviewY);
        var location = PointToScreen(new Point(Math.Clamp(x,0,available.Width-width),Math.Clamp(y,0,available.Height-height)));
        motionPreview.Bounds = new Rectangle(location,new Size(width,height));
        motionPreview.ApplyCorners();
        if (motionPreviewInput is not null) motionPreviewInput.Bounds = motionPreview.Bounds;
        if (motionPreview.Visible) { motionPreview.BringToFront(); motionPreviewInput?.BringToFront(); }
    }

    internal void BeginMotionPreviewSizing()
    {
        if (gridMode) return;
        if (!fullscreen) ToggleFullscreen();
        HideMotionPreview(); EnsureMotionPreview();
        motionPreviewEditing = true;
        motionPreviewPlaceholder!.SetLogoVariant(0);
        motionPreviewPlaceholder.SetEmpty(true);
        motionPreviewVideo!.Hide(); motionPreviewPlaceholder.Show(); motionPreviewPlaceholder.BringToFront();
        PresentMotionPreview();
    }

    private void FinishMotionPreviewSizing()
    {
        if (!motionPreviewEditing) return;
        SettingsStore.Save(settings);
        HideMotionPreview();
    }

    protected override bool ProcessCmdKey(ref Message message, Keys keyData)
    {
        if (motionPreviewEditing && keyData == Keys.Escape) { FinishMotionPreviewSizing(); return true; }
        return base.ProcessCmdKey(ref message,keyData);
    }

    private void StopMotionPreviewPlayer()
    {
        motionPreviewCancellation?.Cancel(); motionPreviewCancellation?.Dispose(); motionPreviewCancellation = null;
        var previous = motionPreviewPlayer; motionPreviewPlayer = null; motionPreviewPipeName = null; DisposePlayer(previous);
    }

    internal bool IsMotionPreviewAt(Point point) => fullscreen && !gridMode && motionPreview is { Visible:true } && motionPreview.Bounds.Contains(point);

    private bool TryOpenMotionPreview(Point point)
    {
        if (!IsMotionPreviewAt(point)) return false;
        if (motionPreviewEditing) return true;
        var camera = motionPreviewCamera;
        if (camera < 0 || camera >= settings.Cameras.Count || !settings.Cameras[camera].IsAvailable) return true;
        RegisterUserInteraction(); settings.SelectedCamera = camera; SettingsStore.Save(settings); UpdateToolbar(); RestartPlayer(); return true;
    }

    private void HideMotionPreview()
    {
        motionPreviewEditing = false; motionPreviewInteracting = false; motionPreviewTimer.Stop();
        motionPreviewInput?.Hide(); motionPreview?.Hide(); StopMotionPreviewPlayer(); motionPreviewCamera = -1;
    }

    private void SetMotionPreviewEnabled(bool enabled)
    {
        settings.MotionPictureInPictureEnabled = enabled;
        if (!enabled) HideMotionPreview();
        SettingsStore.Save(settings);
    }

    internal void ExpireMotionPreview(bool pointerInside)
    {
        if (motionPreviewEditing) { motionPreviewTimer.Stop(); return; }
        if (pointerInside || motionPreviewInteracting || cameraContextMenu is { Visible:true })
        {
            motionPreviewTimer.Stop(); motionPreviewTimer.Interval = Math.Clamp(settings.MotionForegroundSeconds,3,300)*1000; motionPreviewTimer.Start(); return;
        }
        HideMotionPreview();
    }

    private int MotionPreviewEdges(Point point)
    {
        if (motionPreview is null) return 0;
        var p = motionPreview.PointToClient(point); var grip = Math.Max(22,DeviceDpi*26/96);
        return (p.X < grip ? 1 : p.X >= motionPreview.Width-grip ? 2 : 0) |
            (p.Y < grip ? 4 : p.Y >= motionPreview.Height-grip ? 8 : 0);
    }

    internal Cursor MotionPreviewCursor(Point point)
    {
        if (!IsMotionPreviewAt(point)) return Cursors.Default;
        return MotionPreviewEdges(point) switch { 1 or 2 => Cursors.SizeWE, 4 or 8 => Cursors.SizeNS,
            5 or 10 => Cursors.SizeNWSE, 6 or 9 => Cursors.SizeNESW, _ => Cursors.SizeAll };
    }

    internal bool BeginMotionPreviewInteraction(Point point)
    {
        if (!IsMotionPreviewAt(point)) return false;
        motionPreviewInteracting = true; motionPreviewResizeEdges = MotionPreviewEdges(point);
        motionPreviewPointer = point; motionPreviewStart = MotionPreviewClientBounds; return true;
    }

    internal bool UpdateMotionPreviewInteraction(Point point)
    {
        if (!motionPreviewInteracting || motionPreview is null) return false;
        var dx = point.X-motionPreviewPointer.X; var dy = point.Y-motionPreviewPointer.Y;
        var bounds = motionPreviewStart;
        if (motionPreviewResizeEdges != 0)
        {
            var left = (motionPreviewResizeEdges & 1) != 0; var top = (motionPreviewResizeEdges & 4) != 0;
            var horizontal = (motionPreviewResizeEdges & 3) != 0; var vertical = (motionPreviewResizeEdges & 12) != 0;
            var ratio = (double)motionPreviewStart.Width/Math.Max(1,motionPreviewStart.Height);
            var requestedWidth = horizontal ? motionPreviewStart.Width + (left ? -dx : dx) :
                (int)((motionPreviewStart.Height+(top ? -dy : dy))*ratio);
            if (horizontal && vertical && Math.Abs(dy*ratio)>Math.Abs(dx)) requestedWidth=(int)((motionPreviewStart.Height+(top ? -dy : dy))*ratio);
            var maxWidth=left ? motionPreviewStart.Right : ClientSize.Width-motionPreviewStart.Left;
            var maxHeight=top ? motionPreviewStart.Bottom : ClientSize.Height-motionPreviewStart.Top;
            maxWidth=Math.Max(1,Math.Min(maxWidth,(int)(maxHeight*ratio)));
            bounds.Width=Math.Clamp(requestedWidth,Math.Min(160,maxWidth),maxWidth); bounds.Height=Math.Max(1,(int)Math.Round(bounds.Width/ratio));
            if (left) bounds.X=motionPreviewStart.Right-bounds.Width;
            if (top) bounds.Y=motionPreviewStart.Bottom-bounds.Height;
        }
        else { bounds.X+=dx; bounds.Y+=dy; }
        bounds.X=Math.Clamp(bounds.X,0,Math.Max(0,ClientSize.Width-bounds.Width));
        bounds.Y=Math.Clamp(bounds.Y,0,Math.Max(0,ClientSize.Height-bounds.Height));
        settings.MotionPreviewX=(double)bounds.X/Math.Max(1,ClientSize.Width);
        settings.MotionPreviewY=(double)bounds.Y/Math.Max(1,ClientSize.Height);
        settings.MotionPreviewWidth=(double)bounds.Width/Math.Max(1,ClientSize.Width);
        settings.MotionPreviewHeight=(double)bounds.Height/Math.Max(1,ClientSize.Height);
        LayoutMotionPreview(); return true;
    }

    internal void EndMotionPreviewInteraction(bool save=true)
    {
        if (!motionPreviewInteracting) return;
        motionPreviewInteracting=false;
        if (save) SettingsStore.Save(settings);
        ExpireMotionPreview(true);
    }
}

internal sealed class MotionPreviewWindow : Form
{
    internal int CornerResult { get; private set; } = int.MinValue;
    protected override bool ShowWithoutActivation => true;
    internal MotionPreviewWindow()
    {
        FormBorderStyle=FormBorderStyle.Sizable; ControlBox=false; ShowInTaskbar=false;
        StartPosition=FormStartPosition.Manual; BackColor=Color.Black; Padding=Padding.Empty;
        AutoScaleMode=AutoScaleMode.None;
    }
    protected override void OnHandleCreated(EventArgs e) { base.OnHandleCreated(e); ApplyCorners(); }
    internal void ApplyCorners()
    {
        if (!IsHandleCreated) return;
        var preference=2;
        CornerResult=NativeMethods.DwmSetWindowAttribute(Handle,33,ref preference,sizeof(int));
        NativeMethods.DisableDwmBorder(Handle);
        NativeMethods.SetDiagnosticCaptionColor(Handle,Color.Black);
        if (CornerResult == 0) { var old=Region; Region=null; old?.Dispose(); }
        else if (Width>0 && Height>0)
        {
            var shape=NativeMethods.CreateRoundRectRgn(0,0,Width,Height,20,20); var old=Region;
            try { Region=Region.FromHrgn(shape); } finally { NativeMethods.DeleteObject(shape); } old?.Dispose();
        }
    }
    protected override void WndProc(ref Message m)
    {
        if (m.Msg==NativeMethods.WmNcCalcSize || m.Msg==NativeMethods.WmNcPaint) { m.Result=IntPtr.Zero; return; }
        base.WndProc(ref m);
    }
}

internal sealed class MotionPreviewInputWindow : OverlayForm
{
    protected override bool ShowWithoutActivation => true;
    internal MotionPreviewInputWindow(MonitorForm monitor)
    {
        FormBorderStyle=FormBorderStyle.None; ShowInTaskbar=false; StartPosition=FormStartPosition.Manual;
        BackColor=Color.Black; Opacity=.01;
        HandleCreated+=(_,_)=>NativeMethods.DisableOverlayDecoration(Handle);
        MouseDown+=(_,e)=> { if(e.Button==MouseButtons.Left && monitor.BeginMotionPreviewInteraction(PointToScreen(e.Location))) Capture=true; };
        MouseMove+=(_,e)=> { var p=PointToScreen(e.Location); if(!monitor.UpdateMotionPreviewInteraction(p)) Cursor=monitor.MotionPreviewCursor(p); };
        MouseUp+=(_,_)=> { monitor.EndMotionPreviewInteraction(); Capture=false; };
        MouseCaptureChanged+=(_,_)=> { if(!Capture) monitor.EndMotionPreviewInteraction(); };
        MouseDoubleClick+=(_,e)=> { if(e.Button==MouseButtons.Left) monitor.HandleSurfaceDoubleClick(PointToScreen(e.Location)); };
    }
}
#endif