using HomeCamMonitor;

namespace V2Isolation;

internal static partial class Check
{
    private static void TestStorageDefaults()
    {
#if RELEASE_V2
        const string expectedSettings = "HomeCamMonitor-V2";
        const string expectedRecordings = "HomeCam Monitor V2";
#else
        const string expectedSettings = "HomeCamMonitor-V2-Beta";
        const string expectedRecordings = "HomeCam Monitor V2 Beta";
#endif
        if (Path.GetFileName(SettingsLocation.DefaultFolder) != expectedSettings ||
            Path.GetFileName(RecordingStorage.ManualSnapshots) != expectedRecordings ||
            Path.GetFileName(RecordingStorage.ManualVideos) != expectedRecordings ||
            Path.GetDirectoryName(RecordingStorage.MotionSnapshots) != RecordingStorage.ManualSnapshots ||
            Path.GetDirectoryName(RecordingStorage.MotionDefault) != RecordingStorage.ManualVideos)
            throw new Exception("Fresh-install storage defaults have incorrect branding.");

        var root = Path.Combine(Path.GetTempPath(), "HomeCamStorageDefaults-" + Guid.NewGuid().ToString("N"));
        var fresh = Path.Combine(root, "Final");
        var previous = Path.Combine(root, "PreviousBeta");
        var custom = Path.Combine(root, "UserSelected");
        try
        {
            Directory.CreateDirectory(previous);
            // Left-over empty directories are not an existing configuration.
            if (SettingsLocation.ResolveFolder(null, fresh, previous) != fresh)
                throw new Exception("Clean installation did not select the final default.");
            File.WriteAllText(Path.Combine(previous, "settings.json"), "{}");
            if (SettingsLocation.ResolveFolder(null, fresh, previous) != previous)
                throw new Exception("Upgrade lost the existing Beta configuration.");
            if (SettingsLocation.ResolveFolder(custom, fresh, previous) != custom)
                throw new Exception("Explicit settings folder was replaced.");
            Directory.CreateDirectory(fresh);
            File.WriteAllText(Path.Combine(fresh, "settings.json"), "{}");
            if (SettingsLocation.ResolveFolder(null, fresh, previous) != fresh)
                throw new Exception("Existing final configuration must take priority.");
            var settings = new Settings { ManualSnapshotFolder = custom, ManualVideoFolder = custom,
                MotionSnapshotFolder = custom, MotionVideoFolder = custom };
            var roundTrip = System.Text.Json.JsonSerializer.Deserialize<Settings>(System.Text.Json.JsonSerializer.Serialize(settings))!;
            if (RecordingStorage.Resolve(roundTrip.ManualSnapshotFolder, RecordingStorage.ManualSnapshots) != custom ||
                RecordingStorage.Resolve(roundTrip.ManualVideoFolder, RecordingStorage.ManualVideos) != custom ||
                RecordingStorage.ResolveMotionSnapshots(roundTrip.MotionSnapshotFolder) != custom ||
                RecordingStorage.Resolve(roundTrip.MotionVideoFolder, RecordingStorage.MotionDefault) != custom)
                throw new Exception("Saved custom recording paths were replaced.");

            using var form = new SettingsForm(SettingsStore.CreateForNewInstallation(), fresh);
            static IEnumerable<Control> All(Control c) => c.Controls.Cast<Control>().SelectMany(x => new[] { x }.Concat(All(x)));
            var paths = All(form).OfType<TextBox>().Select(c => c.Text).ToArray();
            foreach (var expected in new[] { RecordingStorage.ManualSnapshots, RecordingStorage.ManualVideos,
                RecordingStorage.MotionSnapshots, RecordingStorage.MotionDefault, fresh })
                if (!paths.Contains(expected)) throw new Exception("Fresh settings UI does not display the correct path: " + expected);
        }
        finally
        {
            // This directory is created exclusively for this test.
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
        Console.WriteLine("PASS: fresh-install storage paths, all five settings fields, existing Beta/final settings, explicit locations and saved recording paths.");
    }
}
