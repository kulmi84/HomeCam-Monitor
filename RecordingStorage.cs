#if BETA
using System.Text.Json;
using System.Text.RegularExpressions;
namespace HomeCamMonitor;

internal static class RecordingStorage
{
    internal static string ManualSnapshots => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "HomeCam Monitor");
    internal static string ManualVideos => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyVideos), "HomeCam Monitor");
    internal static string MotionDefault => Path.Combine(ManualVideos, "Bewegung");
    private static string Ledger => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "HomeCamMonitor-Beta", "motion-recordings.json");
    internal static string Resolve(string value, string fallback)
    {
        if (string.IsNullOrWhiteSpace(value)) return fallback;
        if (!Path.IsPathFullyQualified(value.Trim())) throw new ArgumentException("Bitte einen vollständigen Speicherpfad wählen.");
        return Path.GetFullPath(value.Trim());
    }
    internal static bool IsMotionFile(string file) => Regex.IsMatch(Path.GetFileName(file), @"_\d{4}-\d{2}-\d{2}_\d{2}-\d{2}-\d{2}-\d{3}_[0-9a-f]{32}\.(png|mkv)$", RegexOptions.IgnoreCase);
    private static HashSet<string> Read()
    {
        try { return new(JsonSerializer.Deserialize<string[]>(File.ReadAllText(Ledger)) ?? [], StringComparer.OrdinalIgnoreCase); }
        catch { return new(StringComparer.OrdinalIgnoreCase); }
    }
    private static void Write(HashSet<string> paths)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Ledger)!);
        var temporary = Ledger + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(paths));
        File.Move(temporary, Ledger, true);
    }
    internal static void Track(string path)
    {
        try { var paths = Read(); paths.Add(Path.GetFullPath(path)); Write(paths); }
        catch (IOException) { } catch (UnauthorizedAccessException) { }
    }
    internal static void Cleanup(int days)
    {
        if (days <= 0) return;
        var paths = Read();
        CleanupPaths(paths, DateTime.UtcNow.AddDays(-days));
        try { Write(paths); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }
    internal static void CleanupPaths(HashSet<string> paths, DateTime cutoff)
    {
        foreach (var path in paths.ToArray())
        {
            if (!IsMotionFile(path)) { paths.Remove(path); continue; }
            try
            {
                if (!File.Exists(path)) { paths.Remove(path); continue; }
                if (File.GetLastWriteTimeUtc(path) < cutoff) { File.Delete(path); paths.Remove(path); }
            }
            catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
    }
}
#endif
