#if BETA
namespace HomeCamMonitor;

internal static class ApplicationChannel
{
#if V2_BETA
    internal const string StorageName = "HomeCamMonitor-V2-Beta";
    internal const string RecordingFolderName = "HomeCam Monitor V2 Beta";
    internal const string ProductName = "HomeCam Monitor V2 Beta";
#else
    internal const string StorageName = "HomeCamMonitor-Beta";
    internal const string RecordingFolderName = "HomeCam Monitor";
#endif
    internal const string RegistryPath = @"Software\" + StorageName;
}
#endif
