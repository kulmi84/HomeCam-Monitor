#if BETA
using System.Text.Json;
namespace HomeCamMonitor;

internal static class SettingsBackup
{
    internal static string Serialize(Settings settings) => JsonSerializer.Serialize(new
    {
        Format = "HomeCamMonitor.Settings", Version = 1, CreatedUtc = DateTime.UtcNow,
        Settings = settings
    }, new JsonSerializerOptions { WriteIndented = true });

    internal static Settings Parse(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object) throw new InvalidDataException("Keine gültige Einstellungsdatei.");
        var payload = root;
        if (root.TryGetProperty("Format", out var format))
        {
            if (format.GetString() != "HomeCamMonitor.Settings" || !root.TryGetProperty("Version", out var version) ||
                !version.TryGetInt32(out var number) || number != 1 || !root.TryGetProperty("Settings", out payload))
                throw new InvalidDataException("Dieses Sicherungsformat wird nicht unterstützt.");
        }
        // Also accept HomeCamMonitor's existing settings.json files.
        if (payload.ValueKind != JsonValueKind.Object || !payload.TryGetProperty("Cameras", out var cameras) || cameras.ValueKind != JsonValueKind.Array)
            throw new InvalidDataException("Die Datei enthält keine HomeCamMonitor-Einstellungen.");
        var settings = payload.Deserialize<Settings>() ?? throw new InvalidDataException("Leere Einstellungsdatei.");
        if (settings.Cameras is null || settings.Cameras.Count > 1000 || settings.Cameras.Any(camera => camera is null ||
            camera.Name is null || !Uri.TryCreate(camera.StreamUrl, UriKind.Absolute, out _) ||
            camera.MotionEntityId is null || camera.PersonEntityId is null || camera.MotionAction is not ("None" or "Snapshot" or "Video" or "Both") ||
            camera.MotionVideoSeconds is not (15 or 30 or 60)))
            throw new InvalidDataException("Ungültige Kamerakonfiguration in der Datei.");
        if (settings.ToolbarSizePercent is < 50 or > 100 || settings.Width is < 240 or > 20000 || settings.Height is < 150 or > 20000 ||
            settings.MotionForegroundSeconds is < 3 or > 300 || settings.MotionIndicatorSeconds is < 1 or > 10 ||
            settings.StartBehavior is not ("Last" or "Minimized" or "Camera" or "Grid") ||
            settings.SnapshotPreRollSeconds is not (0 or 1 or 3 or 5) || settings.VideoPreRollSeconds is not (0 or 1 or 3 or 5) ||
            settings.MotionRetentionDays is not (0 or 1 or 3 or 7 or 14 or 30) || settings.HomeAssistantToken is null || settings.HomeAssistantUrl is null ||
            settings.MotionCameraName is null || settings.MotionEntityId is null || settings.LastMonitorDeviceName is null)
            throw new InvalidDataException("Ungültige Einstellungswerte in der Datei.");
        if (settings.HomeAssistantUrl.Length > 0 && (!Uri.TryCreate(settings.HomeAssistantUrl, UriKind.Absolute, out var ha) || ha.Scheme is not ("http" or "https")))
            throw new InvalidDataException("Ungültige Home-Assistant-Adresse.");
        foreach (var path in new[] { settings.ManualSnapshotFolder, settings.ManualVideoFolder, settings.MotionSnapshotFolder, settings.MotionVideoFolder })
        {
            if (path is null) throw new InvalidDataException("Ungültiger Speicherpfad.");
            _ = RecordingStorage.Resolve(path, RecordingStorage.MotionDefault);
        }
        settings.SelectedCamera = Math.Clamp(settings.SelectedCamera, 0, Math.Max(0, settings.Cameras.Count - 1));
        settings.StartCameraIndex = Math.Clamp(settings.StartCameraIndex, 0, Math.Max(0, settings.Cameras.Count - 1));
        settings.LastGridMode &= settings.Cameras.Count >= 2;
        if (!settings.PerCameraMotionConfigured)
        {
            var camera = settings.Cameras.FirstOrDefault(c => c.Name.Equals(settings.MotionCameraName, StringComparison.OrdinalIgnoreCase));
            if (camera is not null) { camera.MotionEnabled = true; camera.MotionEntityId = settings.MotionEntityId; }
            settings.PerCameraMotionConfigured = true;
        }
        return settings;
    }

    internal static Settings Read(string path)
    {
        if (new FileInfo(path).Length > 2 * 1024 * 1024) throw new InvalidDataException("Die Einstellungsdatei ist zu groß.");
        return Parse(File.ReadAllText(path));
    }

    internal static void Write(string path, Settings settings)
    {
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try { File.WriteAllText(temporary, Serialize(settings)); File.Move(temporary, path, true); }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
#endif
