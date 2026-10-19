namespace HomeCamMonitor;

internal static class LogoVariants
{
    internal static int Selected { get; set; }
    internal static int Normalize(int value) => value is >= 0 and <= 2 ? value : 0;
    internal static string[] Labels => [UiText.T("Weiß auf Schwarz (Standard)"), UiText.T("Weiß – transparent"), UiText.T("Schwarz – transparent")];
    internal static Bitmap LoadImage(int variant)
    {
        var suffix = Normalize(variant) switch { 1 => "White", 2 => "Black", _ => "Default" };
        using var stream = typeof(LogoVariants).Assembly.GetManifestResourceStream("HomeCamMonitor.Logo." + suffix)
            ?? typeof(LogoVariants).Assembly.GetManifestResourceStream("HomeCamMonitor.OfflineLogo")
            ?? throw new InvalidOperationException("HomeCam logo resource missing.");
        using var source = new Bitmap(stream);
        return (Bitmap)source.Clone();
    }
    internal static Icon LoadIcon(int variant)
    {
        var suffix = Normalize(variant) switch { 1 => "White", 2 => "Black", _ => "Default" };
        using var stream = typeof(LogoVariants).Assembly.GetManifestResourceStream("HomeCamMonitor.Icon." + suffix)
            ?? typeof(LogoVariants).Assembly.GetManifestResourceStream("HomeCamMonitor.ApplicationIcon")
            ?? throw new InvalidOperationException("HomeCam icon resource missing.");
        using var source = new Icon(stream, 32, 32);
        return (Icon)source.Clone();
    }
}
