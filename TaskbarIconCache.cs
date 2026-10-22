#if BETA
using System.Runtime.InteropServices;

namespace HomeCamMonitor;

internal static class TaskbarIconCache
{
    internal static string DefaultPath
    {
        get
        {
#if STORE_PACKAGE
            // Use a physical package-data path that Explorer can also open,
            // rather than a logical AppData path subject to MSIX redirection.
            var folder = Windows.Storage.ApplicationData.Current.LocalFolder.Path;
#else
            var folder = SettingsLocation.DefaultFolder;
#endif
            return Path.Combine(folder, "Taskbar", "Current.ico");
        }
    }

    internal static int Write(string source, string destination)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        if (!File.Exists(destination)) File.Copy(source, destination);
        // Retrieve the OLD cached image index before replacing its contents.
        if (SHGetFileInfo(destination, 0, out var info, (uint)Marshal.SizeOf<FileInfo>(), 0x4000) == IntPtr.Zero)
            throw new IOException("Shell icon cache index unavailable.");
        File.Copy(source, destination, true);
        return info.Index;
    }

    internal static void Refresh(string path, int index)
    {
        SHUpdateImage(path, 0, 0, index);
        SHChangeNotify(0x2000, 0x1005, path, IntPtr.Zero);
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct FileInfo
    {
        public IntPtr Icon;
        public int Index;
        public uint Attributes;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string Name;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)] public string Type;
    }
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr SHGetFileInfo(string path, uint attributes, out FileInfo info, uint size, uint flags);
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern void SHUpdateImage(string path, int iconIndex, uint flags, int imageIndex);
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern void SHChangeNotify(uint events, uint flags, string path, IntPtr unused);
}
#endif
