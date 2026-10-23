#if BETA
namespace HomeCamMonitor;

internal sealed partial class MonitorForm
{
    private readonly System.Windows.Forms.Timer manualRestoreTimer = new();

    private void ArmManualRestoreTimer()
    {
        manualRestoreTimer.Stop();
        if (!settings.MinimizeAfterManualRestore || settings.AlwaysOnTop ||
            WindowState == FormWindowState.Minimized || motionRestoreTimer.Enabled) return;
        manualRestoreTimer.Interval = Math.Clamp(settings.ManualRestoreSeconds, 3, 300) * 1000;
        manualRestoreTimer.Start();
    }

    private void CheckManualRestoreTimeout() => CheckManualRestoreTimeout(Bounds.Contains(Cursor.Position));

    internal void CheckManualRestoreTimeout(bool pointerInside)
    {
        manualRestoreTimer.Stop();
        if (!settings.MinimizeAfterManualRestore || settings.AlwaysOnTop || closing ||
            WindowState == FormWindowState.Minimized || motionRestoreTimer.Enabled) return;
        if (activeSettingsDialog is { IsDisposed: false, Visible: true } || pointerInside)
        {
            ArmManualRestoreTimer();
            return;
        }
        MinimizeWindow();
    }
}
#endif
