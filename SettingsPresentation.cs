#if V2_BETA
using System.Runtime.InteropServices;
namespace HomeCamMonitor;

internal static class SettingsPresentation
{
    // Keep the native child controls on a redirected, opaque window surface.
    // Unlike WS_EX_COMPOSITED this does not change descendant paint ordering.
    internal static bool SetAlpha(IntPtr window, byte alpha)
    {
        var style = GetWindowLong(window, -20);
        if ((style & 0x00080000) == 0) SetWindowLong(window, -20, style | 0x00080000);
        return SetLayeredWindowAttributes(window, 0, alpha, 2);
    }

    internal static void PaintAll(IntPtr window) => RedrawWindow(window, IntPtr.Zero, IntPtr.Zero, 0x0585);
    internal static void SetDrawing(IntPtr window, bool enabled) => SendMessage(window, 0x000B, enabled ? new IntPtr(1) : IntPtr.Zero, IntPtr.Zero);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongW")] private static extern int GetWindowLong(IntPtr window, int index);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongW")] private static extern int SetWindowLong(IntPtr window, int index, int value);
    [DllImport("user32.dll")] private static extern bool SetLayeredWindowAttributes(IntPtr window, uint color, byte alpha, uint flags);
    [DllImport("user32.dll")] internal static extern bool GetLayeredWindowAttributes(IntPtr window, out uint color, out byte alpha, out uint flags);
    [DllImport("user32.dll")] private static extern bool RedrawWindow(IntPtr window, IntPtr rectangle, IntPtr region, uint flags);
    [DllImport("user32.dll")] private static extern IntPtr SendMessage(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);
}
#endif
