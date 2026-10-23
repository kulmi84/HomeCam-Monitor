using HomeCamMonitor;
using System.Runtime.InteropServices;

namespace V2Isolation;
internal static partial class Check
{
    [DllImport("user32.dll", EntryPoint = "GetWindowRect")]
    private static extern bool GetVideoTestRect(IntPtr window, out TestVideoRect rectangle);
    [StructLayout(LayoutKind.Sequential)]
    private struct TestVideoRect { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll", EntryPoint = "ShowWindow")]
    private static extern bool ShowVideoTestWindow(IntPtr window, int command);

    private static void TestRestoreLayout()
    {
        var initial = SettingsStore.CreateForNewInstallation();
        initial.MotionRetentionDays = 0; initial.Width = 640; initial.Height = 360;
        using var monitor = new MonitorForm(initial);
        var video = (Panel)typeof(MonitorForm).GetField("video", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(monitor)!;
        // Native visibility exercises WM_SIZE without running camera setup or
        // touching saved settings through the normal Shown/Closing lifecycle.
        ShowVideoTestWindow(monitor.Handle, 4);
        Application.DoEvents();
        var original = video.Size;
        Console.WriteLine($"Before: form={monitor.Size}, client={monitor.ClientSize}, video={video.Size}");
        ShowVideoTestWindow(monitor.Handle, 6);
        Application.DoEvents();
        Console.WriteLine($"Minimized: state={monitor.WindowState}, form={monitor.Size}, client={monitor.ClientSize}, video={video.Size}");
        var minimized = video.Size;
        ShowVideoTestWindow(monitor.Handle, 4);
        Application.DoEvents();
        Console.WriteLine($"Restored: state={monitor.WindowState}, form={monitor.Size}, client={monitor.ClientSize}, video={video.Size}");
        ShowVideoTestWindow(monitor.Handle, 0);
        if (minimized != original || video.Size != original)
            throw new Exception("Minimizing resized the embedded video host; restore must preserve its render target.");
        Console.WriteLine("PASS: video host dimensions retained while minimized and restored.");
        TestManualRestoreMinimization();
    }

    private static void TestNextBetaPresentation()
    {
#if STORE_PACKAGE
#if NEXT_BETA
        if (PackagedStartup.TaskId != "HomeCamMonitorNextBetaStartup") throw new Exception("Beta startup task identity is incorrect.");
#else
        if (PackagedStartup.TaskId != "HomeCamMonitorStartup") throw new Exception("Stable startup task identity changed.");
#endif
#endif
#if NEXT_BETA
        if (TaskbarIconProperties.WindowGroupId == "MarcinKulmaczewski.HomeCamMonitor.ThemedTaskbar")
            throw new Exception("Beta shares the stable Store taskbar group.");
        var initial = SettingsStore.CreateForNewInstallation();
        initial.MotionRetentionDays = 0;
        using (var monitor = new MonitorForm(initial))
        {
            // Handle creation alone must disable the transition, before Show.
            _ = monitor.Handle;
            if (monitor.VideoTransitionStatus != 0)
                throw new Exception($"Windows did not accept disabled camera transitions: {monitor.VideoTransitionStatus:X8}.");
        }
        using var hostForm = new Form { ClientSize = new Size(640, 360) };
        using var host = new Panel { Dock = DockStyle.Fill };
        using var child = new Panel { Bounds = new Rectangle(75, 30, 80, 40) };
        using var nested = new Panel { Bounds = new Rectangle(9, 8, 20, 15) };
        hostForm.Controls.Add(host); host.Controls.Add(child); child.Controls.Add(nested);
        _ = hostForm.Handle;
        _ = host.Handle;
        _ = child.Handle;
        _ = nested.Handle;
        NativeMethods.PrepareVideoChildren(host.Handle);
        if (!GetVideoTestRect(child.Handle, out var rect)) throw new Exception("Cannot read native video size.");
        var actual = host.RectangleToClient(Rectangle.FromLTRB(rect.Left, rect.Top, rect.Right, rect.Bottom));
        if (actual != host.ClientRectangle || nested.Bounds != new Rectangle(9, 8, 20, 15))
            throw new Exception($"Native video host sizing is wrong or nested GPU layout changed: actual={actual}, host={host.ClientRectangle}, nested={nested.Bounds}.");
        Console.WriteLine("PASS: transitions disabled before showing camera; native video child fills host; nested layout retained; separate Store/Beta group.");
#else
        if (TaskbarIconProperties.WindowGroupId != "MarcinKulmaczewski.HomeCamMonitor.ThemedTaskbar")
            throw new Exception("Stable Store taskbar group changed.");
        Console.WriteLine("PASS: stable Store taskbar group unchanged.");
#endif
    }
    private static void TestManualRestoreMinimization()
    {
        const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        var settings = SettingsStore.CreateForNewInstallation();
        settings.MotionRetentionDays = 0;
        settings.AlwaysOnTop = false;
        if (!settings.ManualRestoreKeepForeground) throw new Exception("Manual foreground must default to enabled.");
        settings.ManualRestoreKeepForeground = false;
        settings.Width = 640; settings.Height = 360;
        using var monitor = new MonitorForm(settings);
        var timer = (System.Windows.Forms.Timer)typeof(MonitorForm).GetField("manualRestoreTimer", flags)!.GetValue(monitor)!;
        void Arm() => typeof(MonitorForm).GetMethod("ArmManualRestoreTimer", flags)!.Invoke(monitor, null);
        void Tick(bool pointerInside = false) => monitor.CheckManualRestoreTimeout(pointerInside);
        ShowVideoTestWindow(monitor.Handle, 4);
        Arm();
        if (timer.Enabled) throw new Exception("Manual minimization must default to disabled.");
        settings.MinimizeAfterManualRestore = true;
        settings.ManualRestoreKeepForeground = true;
        settings.ManualRestoreSeconds = 7;
        ShowVideoTestWindow(monitor.Handle, 6);
        ShowVideoTestWindow(monitor.Handle, 9);
        Application.DoEvents();
        if (!timer.Enabled || timer.Interval != 7000 || !monitor.TopMost || (NativeMethods.GetWindowStyle(monitor.Handle, -20) & 8) == 0) throw new Exception("Taskbar restore did not arm manual timeout.");

        try
        {

            Tick(true);
            if (!timer.Enabled || !monitor.TopMost || monitor.WindowState == FormWindowState.Minimized) throw new Exception("Pointer inside must postpone minimization.");
            monitor.RegisterUserInteraction();
            if (!timer.Enabled || !monitor.TopMost) throw new Exception("Interaction must retain foreground and restart the manual timeout.");
            settings.AlwaysOnTop = true;
            Tick();
            if (timer.Enabled || monitor.WindowState == FormWindowState.Minimized) throw new Exception("Always on top must prevent minimization.");
            settings.AlwaysOnTop = false;
            Arm();

            Tick();
            if (monitor.WindowState != FormWindowState.Minimized || timer.Enabled || monitor.TopMost) throw new Exception("Expired manual timeout must minimize.");
            // Foreground without minimization must release the native topmost flag on expiry.
            settings.MinimizeAfterManualRestore = false;
            ShowVideoTestWindow(monitor.Handle, 9);
            Application.DoEvents();
            if (!monitor.TopMost) throw new Exception("Foreground-only option did not keep window on top.");
            Tick();
            if (monitor.TopMost || (NativeMethods.GetWindowStyle(monitor.Handle, -20) & 8) != 0 || monitor.WindowState == FormWindowState.Minimized)
                throw new Exception("Foreground-only expiry must release topmost without minimizing.");
            // Minimization also remains usable with foreground explicitly disabled.
            settings.ManualRestoreKeepForeground = false;
            settings.MinimizeAfterManualRestore = true;
            ShowVideoTestWindow(monitor.Handle, 6);
            ShowVideoTestWindow(monitor.Handle, 9);
            Application.DoEvents();
            if (monitor.TopMost || !timer.Enabled) throw new Exception("Disabled foreground must retain only the minimization timer.");
            Tick();
            if (monitor.WindowState != FormWindowState.Minimized) throw new Exception("Minimization without foreground failed.");
            var copy = System.Text.Json.JsonSerializer.Deserialize<Settings>(System.Text.Json.JsonSerializer.Serialize(settings))!;
            if (!copy.MinimizeAfterManualRestore || copy.ManualRestoreSeconds != 7 || copy.ManualRestoreKeepForeground) throw new Exception("Manual timeout settings did not round-trip.");
            if (!SettingsReset.Apply(settings, SettingsResetScope.Display).ManualRestoreKeepForeground) throw new Exception("Display reset must enable default foreground protection.");
            if (SettingsReset.Apply(settings, SettingsResetScope.Display).MinimizeAfterManualRestore) throw new Exception("Display reset must clear manual timeout.");
        }
        finally { ShowVideoTestWindow(monitor.Handle, 0); }
        Console.WriteLine("PASS: optional manual timeout, restore, pointer protection, interaction, always-on-top, minimization and settings persistence.");
    }
}
