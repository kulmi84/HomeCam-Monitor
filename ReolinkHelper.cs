#if V2_BETA
namespace HomeCamMonitor;

internal static class ReolinkHelper
{
    internal static string BundledPath => Path.Combine(AppContext.BaseDirectory, "tools", "neolink", "neolink.exe");

    internal static string Resolve(string? configured) => Resolve(configured, BundledPath, File.Exists);

    internal static string Resolve(string? configured, string bundled, Func<string, bool> exists)
    {
        var path = configured?.Trim() ?? "";
        // Versioned Store paths can still exist while belonging to another package
        // version/installation. Existence is not proof that we may execute them.
        var normalized = path.Replace('/', '\\');
        var isPackagedHelper = normalized.Contains("\\WindowsApps\\MarcinKulmaczewski.HomeCamMonitor_", StringComparison.OrdinalIgnoreCase)
            && normalized.EndsWith("\\tools\\neolink\\neolink.exe", StringComparison.OrdinalIgnoreCase);
        if (path.Length == 0 || isPackagedHelper || !exists(path)) return bundled;
        return path; // Preserve a deliberate external helper while it exists.
    }
}
#endif
