#if BETA
namespace HomeCamMonitor;

internal static class ApplicationChannel
{
#if V2_BETA
    // Keep startup, registry and bookkeeping identity compatible with V2 Betas.
    // Fresh-install folder names are separate from this internal identity.
#if NEXT_BETA
    internal const string StorageName = "HomeCamMonitor-V2-NextBeta";
#else
    internal const string StorageName = "HomeCamMonitor-V2-Beta";
#endif
#if RELEASE_V2
    internal const string DefaultSettingsFolderName = "HomeCamMonitor-V2";
    internal const string RecordingFolderName = "HomeCam Monitor V2";
    internal const string ProductName = "HomeCam Monitor V2";
#else
    internal const string DefaultSettingsFolderName = StorageName;
#if NEXT_BETA
    internal const string RecordingFolderName = "HomeCam Monitor V2 Next Beta";
    internal const string ProductName = "HomeCam Monitor V2 Next Beta";
#else
    internal const string RecordingFolderName = "HomeCam Monitor V2 Beta";
    internal const string ProductName = "HomeCam Monitor V2 Beta";
#endif
#endif
#else
    internal const string StorageName = "HomeCamMonitor-Beta";
    internal const string DefaultSettingsFolderName = StorageName;
    internal const string RecordingFolderName = "HomeCam Monitor";
#endif
    internal const string RegistryPath = @"Software\" + StorageName;
}
#endif
