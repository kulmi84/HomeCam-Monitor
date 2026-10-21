using HomeCamMonitor;
using System.Globalization;
using System.Text.Json;

namespace V2Isolation;
internal static partial class Check
{
    private static void TestReolinkHelperPaths()
    {
        var current = @"C:\Apps\HomeCam\tools\neolink\neolink.exe";
        var oldStore = @"C:\Program Files\WindowsApps\MarcinKulmaczewski.HomeCamMonitor_2.0.12.0_x64__ecn324sqw90jr\tools\neolink\neolink.exe";
        var newStore = oldStore.Replace("2.0.12.0", "2.0.15.0");
        var custom = @"D:\CameraTools\neolink.exe";
        foreach (var configured in new[] { "", oldStore, oldStore.ToUpperInvariant(), oldStore.Replace('\\', '/'), @"C:\Missing\neolink.exe" })
            if (ReolinkHelper.Resolve(configured, current, path => path != @"C:\Missing\neolink.exe") != current)
                throw new Exception("Old/missing packaged helper was retained.");
        if (ReolinkHelper.Resolve(oldStore, newStore, _ => true) != newStore)
            throw new Exception("Store update kept a prior package executable.");
        if (ReolinkHelper.Resolve(custom, current, _ => true) != custom)
            throw new Exception("An explicit external helper was discarded.");
        foreach (var code in new[] { 0xC0000022u, 0xC0000135u })
            if (ReolinkDiagnostics.HelperFailure(unchecked((int)code)) is not { } failure || !ReolinkDiagnostics.IsFailure(failure))
                throw new Exception("Terminal loader failure is retried or not reported.");
        if (ReolinkDiagnostics.HelperFailure(0) is not null) throw new Exception("Successful helper classified as failure.");
        Console.WriteLine("PASS: existing stale WindowsApps paths migrate for Store updates and local installs; explicit helpers retained; terminal loader failures classified.");
    }

    private static void TestLogoVariants()
    {
        // Old configuration fields must not restore removed appearance controls.
        var settings = JsonSerializer.Deserialize<Settings>("{\"LogoVariant\":2,\"IconVariant\":1,\"TaskbarIconTheme\":2}")!;
        var saved = JsonSerializer.Serialize(settings);
        if (saved.Contains("Variant") || saved.Contains("TaskbarIconTheme")) throw new Exception("Obsolete icon setting is still persisted.");
        var originalCulture = CultureInfo.CurrentUICulture;
        try
        {
            foreach (var language in new[] { "de-DE", "en-US" })
            {
                CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(language);
                using var form = new SettingsForm(settings, SettingsLocation.DefaultFolder);
                static IEnumerable<Control> All(Control c) => c.Controls.Cast<Control>().SelectMany(x => new[] { x }.Concat(All(x)));
                if (All(form).Any(c => c.Name is "LogoVariant" or "ProgramIconVariant" or "LogoVariantPreview" or "ProgramIconOptions" or "SaveProgramIcon" or "CreateIconShortcut" or "TaskbarIconTheme"))
                    throw new Exception("Removed logo/icon controls are still present.");
                if (All(form).Any(c => c.Text == UiText.T("Logo in leeren Kamerafeldern anzeigen")))
                    throw new Exception("Logo option is still displayed.");
            }
        }
        finally { CultureInfo.CurrentUICulture = originalCulture; }
        for (var variant = 0; variant < 3; variant++)
        {
            var suffix = variant switch { 1 => "White", 2 => "Black", _ => "Default" };
            using var resource = typeof(LogoVariants).Assembly.GetManifestResourceStream("HomeCamMonitor.Icon." + suffix)!;
            using var bytes = new MemoryStream(); resource.CopyTo(bytes);
            var ico = bytes.ToArray();
            if (BitConverter.ToUInt16(ico, 2) != 1 || BitConverter.ToUInt16(ico, 4) != 7 || ico[6 + 6 * 16] != 0)
                throw new Exception("Separate ICO resource is missing its seven sizes, including 256 pixels.");
            using var icon = LogoVariants.LoadIcon(variant);
            if (icon.Width != 32) throw new Exception("Windows cannot load the icon resource.");
        }
        if (ApplicationBranding.WindowIcon.Width != 32) throw new Exception("Standard application icon is missing.");
        Console.WriteLine("PASS: legacy icon fields ignored, no logo/icon customization in German/English settings, three complete Windows ICO resources.");
    }
}
