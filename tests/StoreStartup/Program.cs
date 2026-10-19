using HomeCamMonitor;
using Windows.ApplicationModel;


var disabled = new FakeTask(StartupTaskState.Disabled);
Require(await PackagedStartup.SetEnabledAsync(true, disabled), "Enable failed");
Require(disabled.EnableCalls == 1 && disabled.DisableCalls == 0, "Enable must request Windows once");
Require(!await PackagedStartup.SetEnabledAsync(false, disabled), "Disable failed");
Require(disabled.DisableCalls == 1, "Disable must call Windows once");
Require(!await PackagedStartup.SetEnabledAsync(false, disabled) && disabled.DisableCalls == 1, "Already disabled must be a no-op");

foreach (var state in new[] { StartupTaskState.DisabledByUser, StartupTaskState.DisabledByPolicy })
{
    var blocked = new FakeTask(state);
    await MustReject(true, blocked);
    Require(blocked.EnableCalls == 0 && blocked.DisableCalls == 0, "User/policy decision must not be overridden");
}
var managed = new FakeTask(StartupTaskState.EnabledByPolicy);
await MustReject(false, managed);
Require(managed.DisableCalls == 0, "EnabledByPolicy must not be disabled");
Require(await PackagedStartup.SetEnabledAsync(true, managed) && managed.EnableCalls == 0, "Managed enabled state must be a no-op");
var denied = new FakeTask(StartupTaskState.Disabled) { EnableResult = StartupTaskState.DisabledByPolicy };
await MustReject(true, denied);
Require(denied.EnableCalls == 1, "Windows rejection must be reported after the request");
Console.WriteLine("PASS: startup enable/disable, unchanged states, Windows rejection, user and policy protections. No real startup settings were changed.");

static void Require(bool value, string message) { if (!value) throw new Exception(message); }
static async Task MustReject(bool enabled, FakeTask task)
{
    try { await PackagedStartup.SetEnabledAsync(enabled, task); }
    catch (InvalidOperationException) { return; }
    throw new Exception("Blocked startup operation was accepted");
}

sealed class FakeTask(StartupTaskState state) : PackagedStartup.ITask
{
    public StartupTaskState State { get; private set; } = state;
    public StartupTaskState EnableResult { get; init; } = StartupTaskState.Enabled;
    public int EnableCalls { get; private set; }
    public int DisableCalls { get; private set; }
    public void Disable() { DisableCalls++; State = StartupTaskState.Disabled; }
    public Task<StartupTaskState> RequestEnableAsync() { EnableCalls++; State = EnableResult; return Task.FromResult(State); }
}
