using HomeCamMonitor;
using System.Globalization;
using System.Text.Json;

namespace V2Isolation;
internal static partial class Check
{
    private static void TestLocalization()
    {
        var old = CultureInfo.CurrentUICulture;
        try
        {
            var settings = new Settings { Cameras = [new() { Name = "Meine Kamera", StreamUrl = "rtsp://camera.example/live" }] };
            var original = JsonSerializer.Serialize(settings);
            foreach (var language in new[] { "de-DE", "en-US", "en-GB", "fr-FR" })
            {
                CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(language);
                var english = language != "de-DE";
                using var form = new SettingsForm(settings, SettingsLocation.DefaultFolder);
                static IEnumerable<Control> All(Control parent) => parent.Controls.Cast<Control>().SelectMany(c => new[] { c }.Concat(All(c)));
                var labels = All(form).Select(c => c.Text).ToArray();
                if (!labels.Contains(english ? "Save" : "Speichern") || !labels.Contains(english ? "Cancel" : "Abbrechen")) throw new Exception("Settings buttons did not follow UI culture: " + language + " / " + string.Join(";",labels.Where(t=>t.Length>0).Take(20)));
                if (english && labels.Any(t => t is "Speichern" or "Abbrechen" or "Kamera löschen")) throw new Exception("German text leaked into English UI.");
                if (UiText.F("{0} Sekunden", 3) != (english ? "3 seconds" : "3 Sekunden")) throw new Exception("Formatted translation failed.");
                if (ReolinkDiagnostics.Classify("Logging in") != (english ? "Camera reached – logging in … [LOGIN]" : "Kamera erreicht – Anmeldung läuft … [LOGIN]")) throw new Exception("Reolink status did not follow UI culture.");
                if (JsonSerializer.Serialize(settings) != original) throw new Exception("Rendering translated UI changed stored settings.");
            }
            using var stream = typeof(UiText).Assembly.GetManifestResourceStream("HomeCamMonitor.Strings.en.tsv")!;
            using var reader = new StreamReader(stream);
            var count = 0;
            while (reader.ReadLine() is { } line)
            {
                var parts = line.Split('\t', 2);
                if (parts.Length != 2 || string.IsNullOrWhiteSpace(parts[1])) throw new Exception("Missing translation.");
                var german = parts[0].Replace("\\n", "\n");
                var english = parts[1].Replace("\\n", "\n");
                // Every formatted translation must preserve the argument indices and formats.
                var pattern = @"\{\d+(?:,[^}:]+)?(?::[^}]+)?\}";
                string Placeholders(string text) => string.Join("|", System.Text.RegularExpressions.Regex.Matches(text, pattern).Select(m => m.Value).Order());
                if (Placeholders(german) != Placeholders(english)) throw new Exception("Translation changed placeholders: " + german);
                if (german.Contains("{0}")) { CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en-US"); if (UiText.T(german) != english) throw new Exception("Catalog not embedded correctly."); }
                count++;
            }
            Console.WriteLine($"PASS: {count} translations, placeholders, German/English UI, regional/fallback cultures, Reolink status and settings preservation.");
        }
        finally { CultureInfo.CurrentUICulture = old; }
    }
}
