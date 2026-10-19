using System.Globalization;
using System.Reflection;

namespace HomeCamMonitor;

// Display text only. Protocol values, stored settings and user camera names are not translated.
internal static class UiText
{
    private static readonly Lazy<IReadOnlyDictionary<string, string>> English = new(LoadEnglish);
    internal static bool IsEnglish => CultureInfo.CurrentUICulture.TwoLetterISOLanguageName != "de";
    internal static string T(string german) => IsEnglish && English.Value.TryGetValue(german, out var text) ? text : german;
    internal static string F(string german, params object?[] values) => string.Format(CultureInfo.CurrentCulture, T(german), values);

    private static IReadOnlyDictionary<string, string> LoadEnglish()
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        using var stream = typeof(UiText).Assembly.GetManifestResourceStream("HomeCamMonitor.Strings.en.tsv");
        if (stream is null) return result;
        using var reader = new StreamReader(stream);
        while (reader.ReadLine() is { } line)
        {
            if (line.Length == 0) continue;
            var parts = line.Split('\t', 2);
            if (parts.Length != 2) throw new InvalidDataException("Invalid UI translation catalog.");
            static string Decode(string text) => text.Replace("\\n", "\n").Replace("\\r", "\r");
            if (!result.TryAdd(Decode(parts[0]), Decode(parts[1]))) throw new InvalidDataException("Duplicate UI translation key.");
        }
        return result;
    }
}
