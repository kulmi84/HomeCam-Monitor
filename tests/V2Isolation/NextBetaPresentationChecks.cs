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
}
