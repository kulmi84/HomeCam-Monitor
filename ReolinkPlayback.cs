#if V2_BETA
using System.Diagnostics;
namespace HomeCamMonitor;

internal static class ReolinkPlayback
{
    internal static void Configure(ProcessStartInfo start, bool uid)
    {
        if (!uid) return;
        // The existing lavf-o assignment replaces the low-latency profile's
        // fflags entry. Restore it after all common arguments have been added.
        start.ArgumentList.Add("--demuxer-lavf-o-add=fflags=+nobuffer");
    }
}
#endif
