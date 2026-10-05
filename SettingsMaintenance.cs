#if BETA
using System.Text.Json;
using System.Text.Json.Nodes;
namespace HomeCamMonitor;

internal enum SettingsResetScope { Window, Display, Cameras, All }

internal static class SettingsReset
{
    internal static Settings Apply(Settings current, SettingsResetScope scope)
    {
        var result = JsonSerializer.Deserialize<Settings>(JsonSerializer.Serialize(current))!;
        var defaults = SettingsStore.CreateForNewInstallation();
        if (scope == SettingsResetScope.All) return defaults;
        string[] properties = scope switch
        {
            SettingsResetScope.Window => [nameof(Settings.Left), nameof(Settings.Top), nameof(Settings.Width), nameof(Settings.Height),
                nameof(Settings.LastMonitorDeviceName), nameof(Settings.MonitorOffsetX), nameof(Settings.MonitorOffsetY),
                nameof(Settings.SettingsWindowWidth), nameof(Settings.SettingsWindowHeight)],
            SettingsResetScope.Display => [nameof(Settings.AlwaysOnTop), nameof(Settings.StartWithWindows), nameof(Settings.StartBehavior),
                nameof(Settings.StartCameraIndex), nameof(Settings.ToolbarSizePercent), nameof(Settings.AutoScaleToolbar),
                nameof(Settings.ShowGridCameraNames), nameof(Settings.ShowEmptyCameraLogo), nameof(Settings.ShowEmptyFourthFieldBorder),
                nameof(Settings.MotionForegroundSeconds), nameof(Settings.MotionIndicatorSeconds), nameof(Settings.HighlightMotionInGrid),
                nameof(Settings.MinimizeWhenInactive), nameof(Settings.RestorePreviousCameraAfterMotion)],
            SettingsResetScope.Cameras => [nameof(Settings.Cameras), nameof(Settings.SelectedCamera), nameof(Settings.StartCameraIndex),
                nameof(Settings.LastGridMode), nameof(Settings.StartBehavior), nameof(Settings.DirectHomeAssistantEnabled),
                nameof(Settings.HomeAssistantUrl), nameof(Settings.HomeAssistantToken), nameof(Settings.MotionEntityId),
                nameof(Settings.MotionCameraName), nameof(Settings.IgnoreHomeAssistantCertificateErrors), nameof(Settings.PerCameraMotionConfigured),
                nameof(Settings.MotionActionsPausedUntilUtc)],
            _ => throw new ArgumentOutOfRangeException(nameof(scope))
        };
        foreach (var name in properties)
        {
            var property = typeof(Settings).GetProperty(name)!;
            property.SetValue(result, property.GetValue(defaults));
        }
        if (scope == SettingsResetScope.Cameras)
        {
            result.DirectHomeAssistantEnabled = false;
            result.HomeAssistantUrl = ""; result.MotionEntityId = ""; result.MotionCameraName = "";
        }
        return result;
    }
}

// Only controlled event codes are recorded. No process command lines, URLs or exception text.
internal sealed class StreamDiagnostics
{
    private readonly DateTime startedUtc = DateTime.UtcNow;
    private readonly Queue<StreamEvent> events = new();
    private readonly Dictionary<string, int> counts = new();
    internal void Record(int cameraIndex, string code)
    {
        if (code is not ("start" or "ready" or "start-failed" or "exited" or "connect-failed" or "stalled-or-disconnected" or "manual-reconnect"))
            throw new ArgumentException("Unknown stream event", nameof(code));
        counts[code] = counts.GetValueOrDefault(code) + 1;
        events.Enqueue(new StreamEvent(DateTime.UtcNow, cameraIndex, code));
        while (events.Count > 200) events.Dequeue();
    }
    internal object Snapshot() => new { StartedUtc = startedUtc, Counts = new Dictionary<string, int>(counts), Events = events.ToArray() };
    private sealed record StreamEvent(DateTime Utc, int CameraIndex, string Code);
}

internal static class DiagnosticExport
{
    // Export addresses only as scheme/host/port: credentials, path, query and fragments may contain secrets.
    internal static string SafeAddress(string address)
    {
        if (!Uri.TryCreate(address, UriKind.Absolute, out var uri)) return "[nicht angegeben oder ungültig]";
        if (uri.Scheme is not ("rtsp" or "rtsps" or "http" or "https")) return "[lokale Datei oder anderes Protokoll]";
        var safe = new UriBuilder(uri) { UserName = "", Password = "", Path = "", Query = "", Fragment = "" };
        return safe.Uri.GetLeftPart(UriPartial.Authority);
    }
    internal static string Serialize(Settings settings, object streamState, object runtime)
    {
        var values = JsonSerializer.SerializeToNode(settings)!.AsObject();
        values.Remove(nameof(Settings.Cameras));
        values.Remove(nameof(Settings.HomeAssistantToken));
        // Free text and local paths are omitted rather than guessing where users stored credentials.
        foreach (var name in values.Select(pair => pair.Key).ToArray())
        {
            if (values[name] is JsonValue value && value.TryGetValue<string>(out _)) values.Remove(name);
        }
        var report = new
        {
            Format = "HomeCamMonitor.Diagnostics", Version = 1, CreatedUtc = DateTime.UtcNow,
            HomeCamVersion = Application.ProductVersion.Split('+')[0], WindowsVersion = Environment.OSVersion.VersionString,
            Architecture = System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture.ToString(),
            Settings = values, StartBehavior = settings.StartBehavior is "Last" or "Minimized" or "Camera" or "Grid" ? settings.StartBehavior : "Unknown",
            HomeAssistantAddress = SafeAddress(settings.HomeAssistantUrl), HomeAssistantTokenConfigured = settings.HomeAssistantToken.Length > 0,
            Cameras = settings.Cameras.Select((camera, index) => new
            {
                Index = index, StreamAddress = SafeAddress(camera.StreamUrl), camera.MotionEnabled, camera.PersonEnabled,
                MotionSensorConfigured = camera.MotionEntityId.Length > 0, PersonSensorConfigured = camera.PersonEntityId.Length > 0,
                MotionAction = camera.MotionAction is "None" or "Snapshot" or "Video" or "Both" ? camera.MotionAction : "Unknown", camera.MotionVideoSeconds
            }).ToArray(),
            Runtime = runtime, Streams = streamState,
            Privacy = "Keine Namen, Speicherpfade, HA-Token, Zugangsdaten, URL-Pfade/-Parameter, Rohprotokolle oder Bilder. Ereignisse gelten nur für diese Sitzung; maximal 200 letzte Ereignisse."
        };
        return JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true });
    }
    internal static void Write(string path, string content)
    {
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try { File.WriteAllText(temporary, content); File.Move(temporary, path, true); }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
#endif
