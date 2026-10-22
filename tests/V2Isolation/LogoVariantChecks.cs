using HomeCamMonitor;
using System.Globalization;
using System.Text.Json;

namespace V2Isolation;
internal static partial class Check
{
    private sealed class IconTestForm : Form
    {
        internal void RecreateWindow() => RecreateHandle();
    }

    private static void TestWindowIconTheme()
    {
        const string appId = "HomeCamMonitor.IconTheme.Test";
        var processAppId = TaskbarIconProperties.CurrentAppId();
        var cacheFolder = Path.Combine(AppContext.BaseDirectory, "icon-cache-test-" + Guid.NewGuid().ToString("N"));
        var cachePath = Path.Combine(cacheFolder, "Current.ico");
        if (TaskbarIconProperties.WindowGroupId == appId || TaskbarIconProperties.WindowGroupId.Length > 128 ||
            TaskbarIconProperties.WindowGroupId.Contains('!') || TaskbarIconProperties.WindowGroupId.Contains(' '))
            throw new Exception("Taskbar window group must be an independent stable desktop ID.");
        var shellValues = new Dictionary<uint, string>();
        var notificationSnapshots = new List<string>();
        foreach (var iconPath in new[] { "white.ico", "black.ico", "white.ico" })
        {
            TaskbarIconProperties.SetRelaunchProperties((id, value) =>
            {
                shellValues[id] = value;
                if (id != 5) return;
                if (!shellValues.TryGetValue(3, out var icon) || icon != iconPath + ",0" ||
                    !shellValues.ContainsKey(2) || !shellValues.ContainsKey(4))
                    throw new Exception("Explorer is notified before the NEW relaunch icon/command/name are ready.");
                notificationSnapshots.Add(icon);
            }, appId, iconPath);
        }
        if (notificationSnapshots.Count != 3) throw new Exception("A theme update did not notify Explorer.");
        void CheckTaskbar(Form form, bool light)
        {
            var path = Path.Combine(AppContext.BaseDirectory, light ? "HomeCam-Schwarz-transparent.ico" : "HomeCam-Weiss-transparent.ico");
            if (!File.Exists(path)) throw new Exception("Taskbar ICO file missing.");
            if (TaskbarIconProperties.Read(form.Handle, 3) != cachePath + ",0" || !File.ReadAllBytes(cachePath).SequenceEqual(File.ReadAllBytes(path)))
                throw new Exception("Taskbar group icon not updated.");
            if (TaskbarIconProperties.Read(form.Handle, 5) != TaskbarIconProperties.WindowGroupId ||
                TaskbarIconProperties.Read(form.Handle, 2) != TaskbarIconProperties.RelaunchCommand(appId) ||
                TaskbarIconProperties.Read(form.Handle, 4) != ApplicationBranding.ProductName)
                throw new Exception("Independent window group / registered package activation not retained.");
            if (!TaskbarIconProperties.RelaunchCommand(appId).EndsWith("shell:AppsFolder\\" + appId) ||
                TaskbarIconProperties.RelaunchCommand(appId).Contains("WindowsApps"))
                throw new Exception("Taskbar shortcut would bypass package activation.");
        }
        static void CheckIcon(Form form, bool light)
        {
            // Inspect the native HWND icon, not just the managed property.
            var handle = GetWindowIcon(form.Handle, 0x007F, (IntPtr)1, IntPtr.Zero);
            if (handle == IntPtr.Zero) throw new Exception("HWND has no large icon.");
            using var bitmap = Icon.FromHandle(handle).ToBitmap();
            if (bitmap.GetPixel(0, 0).A != 0) throw new Exception("Window icon has an opaque background.");
            var visible = 0;
            for (var y = 0; y < bitmap.Height; y++)
                for (var x = 0; x < bitmap.Width; x++)
                {
                    var p = bitmap.GetPixel(x, y);
                    if (p.A < 240) continue;
                    visible++;
                    if (light ? p.R > 10 || p.G > 10 || p.B > 10 : p.R < 245 || p.G < 245 || p.B < 245)
                        throw new Exception("Wrong icon color for the system taskbar theme.");
                }
            if (visible == 0) throw new Exception("Window icon is empty.");
        }

        foreach (var initiallyLight in new[] { false, true })
        {
            var light = initiallyLight;
            using var form = new IconTestForm();
            using var controller = new WindowIconTheme(form, () => light, appId, taskbarIconPath: cachePath);
            CheckIcon(form, light);
            CheckTaskbar(form, light);
            form.RecreateWindow();
            CheckIcon(form, light);
            CheckTaskbar(form, light);
            light = !light;
            var deadline = DateTime.UtcNow.AddSeconds(2.5);
            while (DateTime.UtcNow < deadline) { Application.DoEvents(); Thread.Sleep(20); }
            CheckIcon(form, light);
            CheckTaskbar(form, light);
            light = !light;
            controller.Refresh();
            CheckTaskbar(form, light);
            CheckIcon(form, light);
            // Repeated changes retain the same resource path and group, updating full ICO bytes.
            for (var change = 0; change < 4; change++)
            {
                light = !light;
                controller.Refresh();
                CheckTaskbar(form, light);
                CheckIcon(form, light);
            }
            form.RecreateWindow();
            CheckTaskbar(form, light);
            form.Dispose();
            controller.Refresh();
        }
        using (var form = new Form())
        {
            using (var controller = new WindowIconTheme(form, () => false, appId, taskbarIconPath: cachePath))
                CheckTaskbar(form, false);
            if (TaskbarIconProperties.Read(form.Handle, 3) is not null ||
                TaskbarIconProperties.Read(form.Handle, 5) is not null)
                throw new Exception("Window taskbar properties were not cleared on disposal.");
        }
#if STORE_PACKAGE
        using (var settings = new SettingsForm(SettingsStore.CreateForNewInstallation(), SettingsLocation.DefaultFolder))
        {
            CheckIcon(settings, WindowIconTheme.ReadLightTaskbar());
            if (settings.ShowInTaskbar || TaskbarIconProperties.Read(settings.Handle, 5) is not null)
                throw new Exception("Settings dialog retained a separate taskbar group member.");
        }
#endif
        if (TaskbarIconProperties.CurrentAppId() != processAppId)
            throw new Exception("Window grouping changed the process/package AppUserModelID.");
        using (var form = new Form())
        {
            if (!ReferenceEquals(WindowIconTheme.Bind(form), WindowIconTheme.Bind(form)))
                throw new Exception("Reapplying settings created a second icon timer for the same window.");
        }
        if (!File.Exists(cachePath)) throw new Exception("Closing the window deleted the icon needed by pins.");
        File.Delete(cachePath); Directory.Delete(cacheFolder);
        Console.WriteLine("PASS: stable cached ICO contents, native window and taskbar property-store icons in both themes, after HWND recreation and timer-driven changes; package activation retained; disposal safe.");
    }

    [System.Runtime.InteropServices.DllImport("user32.dll", EntryPoint = "SendMessageW")]
    private static extern IntPtr GetWindowIcon(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);

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
