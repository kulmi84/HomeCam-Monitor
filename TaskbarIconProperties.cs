#if BETA
using System.Runtime.InteropServices;
using System.Text;

namespace HomeCamMonitor;

// Explorer may use the group's relaunch icon instead of WM_GETICON.
internal static class TaskbarIconProperties
{
    // A registered package AUMID makes Explorer prefer the package logo over
    // the window's themed icon, even without a pin. Use a stable window group
    // while keeping the real package AUMID exclusively for activation.
    internal const string WindowGroupId = "MarcinKulmaczewski.HomeCamMonitor.ThemedTaskbar";
    private static readonly Guid propertySet = new("9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3");
    internal static string? CurrentAppId()
    {
        uint length = 0;
        if (GetCurrentApplicationUserModelId(ref length, null) != 122 || length == 0) return null;
        var value = new StringBuilder((int)length);
        return GetCurrentApplicationUserModelId(ref length, value) == 0 ? value.ToString() : null;
    }

    internal static string RelaunchCommand(string appId) =>
        $"\"{Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe")}\" shell:AppsFolder\\{appId}";

    internal static void Apply(IntPtr window, string appId, string iconPath)
    {
        WithStore(window, store => SetRelaunchProperties((id, text) => Write(store, id, text), appId, iconPath));
    }

    internal static void SetRelaunchProperties(Action<uint, string> set, string appId, string iconPath)
    {
        // Activate with the registered package AUMID through AppsFolder.
        // Never save a direct WindowsApps EXE as the relaunch command.
        set(2, RelaunchCommand(appId));
        set(4, ApplicationBranding.ProductName);
        set(3, iconPath + ",0");
        // Setting ID notifies the taskbar. All relaunch properties must
        // already contain the new values when Explorer handles that update.
        set(5, WindowGroupId);
    }

    internal static string? Read(IntPtr window, uint id)
    {
        string? result = null;
        WithStore(window, store =>
        {
            var key = new PropertyKey(propertySet, id);
            Marshal.ThrowExceptionForHR(store.GetValue(ref key, out var value));
            try { result = value.Type == 31 ? Marshal.PtrToStringUni(value.Pointer) : null; }
            finally { PropVariantClear(ref value); }
        });
        return result;
    }

    internal static void Clear(IntPtr window)
    {
        WithStore(window, store =>
        {
            foreach (var id in new uint[] { 3, 2, 4, 5 })
            {
                var key = new PropertyKey(propertySet, id);
                var empty = new PropVariant();
                Marshal.ThrowExceptionForHR(store.SetValue(ref key, ref empty));
            }
        });
    }

    internal sealed class Lifetime : NativeWindow
    {
        protected override void WndProc(ref Message message)
        {
            if (message.Msg == 0x0002) // WM_DESTROY: clear properties while HWND is still valid.
            {
                try { Clear(message.HWnd); }
                catch (COMException) { }
            }
            base.WndProc(ref message);
        }
    }

    private static void Write(IPropertyStore store, uint id, string text)
    {
        var key = new PropertyKey(propertySet, id);
        var value = new PropVariant { Type = 31, Pointer = Marshal.StringToCoTaskMemUni(text) };
        try { Marshal.ThrowExceptionForHR(store.SetValue(ref key, ref value)); }
        finally { Marshal.FreeCoTaskMem(value.Pointer); }
    }

    private static void WithStore(IntPtr window, Action<IPropertyStore> action)
    {
        var iid = typeof(IPropertyStore).GUID;
        Marshal.ThrowExceptionForHR(SHGetPropertyStoreForWindow(window, ref iid, out var store));
        try { action(store); }
        finally { Marshal.ReleaseComObject(store); }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PropertyKey(Guid format, uint id)
    {
        public Guid Format = format;
        public uint Id = id;
    }

    [StructLayout(LayoutKind.Explicit, Size = 24)]
    private struct PropVariant
    {
        [FieldOffset(0)] public ushort Type;
        [FieldOffset(8)] public IntPtr Pointer;
    }

    [ComImport, Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IPropertyStore
    {
        [PreserveSig] int GetCount(out uint count);
        [PreserveSig] int GetAt(uint index, out PropertyKey key);
        [PreserveSig] int GetValue(ref PropertyKey key, out PropVariant value);
        [PreserveSig] int SetValue(ref PropertyKey key, ref PropVariant value);
        [PreserveSig] int Commit();
    }

    [DllImport("shell32.dll")]
    private static extern int SHGetPropertyStoreForWindow(IntPtr window, ref Guid iid, [MarshalAs(UnmanagedType.Interface)] out IPropertyStore store);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetCurrentApplicationUserModelId(ref uint length, StringBuilder? value);
    [DllImport("ole32.dll")]
    private static extern int PropVariantClear(ref PropVariant value);
}
#endif
