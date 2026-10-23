#if BETA
namespace HomeCamMonitor;

internal sealed partial class MonitorForm
{
    private readonly System.Windows.Forms.Timer manualRestoreTimer = new();
    private bool ManualForegroundActive => manualRestoreTimer.Enabled && settings.ManualRestoreKeepForeground;

    private void ArmManualRestoreTimer()
    {
        var wasForeground = ManualForegroundActive;
        manualRestoreTimer.Stop();
        if ((!settings.MinimizeAfterManualRestore && !settings.ManualRestoreKeepForeground) || settings.AlwaysOnTop ||
            WindowState == FormWindowState.Minimized || motionRestoreTimer.Enabled)
        {
            if (wasForeground) TopMost = settings.AlwaysOnTop || motionRestoreTimer.Enabled;
            return;
        }
        manualRestoreTimer.Interval = Math.Clamp(settings.ManualRestoreSeconds, 3, 300) * 1000;
        manualRestoreTimer.Start();
        TopMost = settings.ManualRestoreKeepForeground;
        PositionOverlays();
    }

    private void CheckManualRestoreTimeout() => CheckManualRestoreTimeout(Bounds.Contains(Cursor.Position));

    internal void CheckManualRestoreTimeout(bool pointerInside)
    {
        manualRestoreTimer.Stop();
        if (settings.AlwaysOnTop || closing || WindowState == FormWindowState.Minimized || motionRestoreTimer.Enabled)
        {
            if (WindowState != FormWindowState.Minimized) TopMost = settings.AlwaysOnTop || motionRestoreTimer.Enabled;
            return;
        }
        if ((settings.MinimizeAfterManualRestore || settings.ManualRestoreKeepForeground) &&
            (activeSettingsDialog is { IsDisposed: false, Visible: true } || pointerInside))
        {
            ArmManualRestoreTimer();
            return;
        }
        TopMost = false;
        PositionOverlays();
        if (settings.MinimizeAfterManualRestore) MinimizeWindow();
    }
}
#endif
