#if STORE_PACKAGE
using Windows.ApplicationModel;

namespace HomeCamMonitor;

internal static class PackagedStartup
{
#if NEXT_BETA
    internal const string TaskId = "HomeCamMonitorNextBetaStartup";
#else
    internal const string TaskId = "HomeCamMonitorStartup";
#endif

    internal interface ITask
    {
        StartupTaskState State { get; }
        void Disable();
        Task<StartupTaskState> RequestEnableAsync();
    }

    private sealed class WindowsTask(StartupTask task) : ITask
    {
        public StartupTaskState State => task.State;
        public void Disable() => task.Disable();
        public async Task<StartupTaskState> RequestEnableAsync() => await task.RequestEnableAsync();
    }

    internal static async Task<bool> IsEnabledAsync()
    {
        var task = await StartupTask.GetAsync(TaskId);
        return IsEnabled(task.State);
    }

    private static bool IsEnabled(StartupTaskState state) =>
        state is StartupTaskState.Enabled or StartupTaskState.EnabledByPolicy;

    internal static async Task<bool> SetEnabledAsync(bool enabled)
    {
        var task = await StartupTask.GetAsync(TaskId);
        return await SetEnabledAsync(enabled, new WindowsTask(task));
    }

    internal static async Task<bool> SetEnabledAsync(bool enabled, ITask task)
    {
        if (enabled == IsEnabled(task.State)) return enabled;
        if (task.State is StartupTaskState.DisabledByPolicy or StartupTaskState.EnabledByPolicy)
            throw new InvalidOperationException(UiText.T("Der Windows-Autostart wird durch eine Systemrichtlinie verwaltet."));
        if (!enabled)
        {
            task.Disable();
            return IsEnabled(task.State);
        }
        if (task.State == StartupTaskState.DisabledByUser)
            throw new InvalidOperationException(UiText.T("Der Autostart wurde in Windows deaktiviert. Bitte aktiviere HomeCam Monitor unter Windows-Einstellungen → Apps → Autostart erneut."));
        var result = await task.RequestEnableAsync();
        if (!IsEnabled(result))
            throw new InvalidOperationException(UiText.T("Windows hat den Autostart nicht aktiviert. Bitte prüfe Windows-Einstellungen → Apps → Autostart."));
        return true;
    }
}
#endif
