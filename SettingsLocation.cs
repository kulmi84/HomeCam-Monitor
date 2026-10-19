#if BETA
using Microsoft.Win32;
using System.Text.Json;
namespace HomeCamMonitor;
internal static class SettingsLocation
{
    internal const string RegistryPath = ApplicationChannel.RegistryPath;
    internal static string DefaultFolder => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), ApplicationChannel.DefaultSettingsFolderName);
    internal static string CurrentFolder => ReadFolder(RegistryPath);
    internal static string ReadFolder(string registryPath)
    {
        using var key = Registry.CurrentUser.OpenSubKey(registryPath);
        var value = key?.GetValue("SettingsFolder") as string;
        // Keep explicit user locations and existing V2/Beta installations.
        // Only a fresh final installation uses the new, non-Beta default.
        var previousDefault = string.Equals(registryPath, RegistryPath, StringComparison.OrdinalIgnoreCase)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), ApplicationChannel.StorageName)
            : null;
        return ResolveFolder(value, DefaultFolder, previousDefault);
    }
    internal static string ResolveFolder(string? configured, string defaultFolder, string? previousDefault)
    {
        if (!string.IsNullOrWhiteSpace(configured)) return Normalize(configured);
        if (File.Exists(Path.Combine(defaultFolder, "settings.json"))) return defaultFolder;
        if (previousDefault is not null && File.Exists(Path.Combine(previousDefault, "settings.json"))) return previousDefault;
        return defaultFolder;
    }
    internal static string Normalize(string folder)
    {
        var full = RecordingStorage.Resolve(folder, DefaultFolder);
        return Path.TrimEndingDirectorySeparator(full);
    }
    internal static bool SameFolder(string first, string second) => string.Equals(Normalize(first), Normalize(second), StringComparison.OrdinalIgnoreCase);
    internal static void Move(Settings settings, string folder, bool overwrite)
    {
        folder = Normalize(folder);
        if (SameFolder(folder, CurrentFolder)) { SettingsStore.Save(settings); return; }
        var target = folder;
        Relocate(Path.Combine(CurrentFolder, "settings.json"), Path.Combine(target, "settings.json"),
            JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true }), () =>
            {
                using var key = Registry.CurrentUser.CreateSubKey(RegistryPath);
                key.SetValue("SettingsFolder", target);
            }, overwrite);
    }
    internal static void Relocate(string source, string target, string json, Action commitLocation, bool overwrite)
    {
        var previous = File.Exists(target) ? File.ReadAllBytes(target) : null;
        if (previous is not null && !overwrite) throw new IOException(UiText.T("Im Zielordner gibt es bereits eine settings.json."));
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        var temporary = target + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temporary, json);
            File.Move(temporary, target, true);
            try { commitLocation(); }
            catch
            {
                if (previous is null) File.Delete(target); else File.WriteAllBytes(target, previous);
                throw;
            }
            // The new configuration and startup location are committed before removing the old file.
            try
            {
                if (File.Exists(source)) File.Delete(source);
                // Different Windows paths may point to the same file through a junction.
                if (!File.Exists(target)) File.WriteAllText(target, json);
            }
            catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
#endif
