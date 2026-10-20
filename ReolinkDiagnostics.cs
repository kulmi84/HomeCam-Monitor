#if V2_BETA
namespace HomeCamMonitor;

// Only fixed categories leave this parser. Raw helper lines can contain UIDs,
// account names or passwords and are never displayed or written to disk.
internal static class ReolinkDiagnostics
{
    internal static string? HelperFailure(int exitCode) => unchecked((uint)exitCode) switch
    {
        0xC0000022 => UiText.T("Reolink-Verbindungsdienst: Zugriff verweigert (0xC0000022) [HELPER-ACCESS]"),
        0xC0000135 => UiText.T("GStreamer-Laufzeit fehlt [HELPER-RUNTIME]"),
        _ => null
    };
    internal static string? Classify(string line)
    {
        if (Environment.GetEnvironmentVariable("HOMECAM_DIAG_TRACE") == "1" &&
            System.Text.RegularExpressions.Regex.IsMatch(line.TrimEnd('\r'), @"^HCM_TRACE (INPUT ts_us=\d{1,20} bytes=\d{1,10} key=[01]|OUTPUT ts_us=\d{1,20} bytes=\d{1,10}|LAG count=\d{1,20}|BC type=\d{1,3} number=\d{1,5} bytes=\d{1,10})$")) return line.TrimEnd('\r');
        if (line.Contains("Relay success", StringComparison.Ordinal)) return UiText.T("UID-Verbindung über Relay hergestellt [RELAY-ACTIVE]");
        if (line.Contains("Map success", StringComparison.Ordinal)) return UiText.T("UID-Verbindung über direkte P2P-Strecke hergestellt [P2P-ACTIVE]");
        if (line.Contains("Remote discovery success", StringComparison.Ordinal)) return UiText.T("UID-Verbindung über entfernte Erkennung hergestellt [REMOTE-ACTIVE]");
        if (line.Contains("Login credentials were not accepted", StringComparison.OrdinalIgnoreCase) ||
            line.Contains("CameraLoginFail", StringComparison.Ordinal) || line.Contains("Credential error", StringComparison.OrdinalIgnoreCase))
            return UiText.T("Anmeldung abgelehnt: Kamera-Benutzername und Kamera-Passwort prüfen [AUTH]");
        if (line.Contains("Registration with reolink servers failed", StringComparison.OrdinalIgnoreCase) ||
            line.Contains("No reolink registers returned valid device data", StringComparison.OrdinalIgnoreCase) || line.Contains("RegisterError", StringComparison.Ordinal))
            return UiText.T("UID-Registrierung beim Reolink-Dienst fehlgeschlagen [UID-REG]");
        if (line.Contains("DiscoveryTimeout", StringComparison.Ordinal) || line.Contains("Timed out while waiting for camera reply", StringComparison.OrdinalIgnoreCase))
            return UiText.T("UID-Verbindung: keine Antwort von Kamera oder Vermittlungsdienst [UID-TIMEOUT]");
        if (line.Contains("Unknown encryption", StringComparison.OrdinalIgnoreCase) || line.Contains("UnknownEncryption", StringComparison.Ordinal))
            return UiText.T("Kamera verwendet eine nicht unterstützte Verschlüsselung [PROTOCOL]");
        if (line.Contains("Failed to parse", StringComparison.OrdinalIgnoreCase) || line.Contains("Failed to validate", StringComparison.OrdinalIgnoreCase))
            return UiText.T("Verbindungskonfiguration ungültig [CONFIG]");
        if (line.Contains("Connected and logged in", StringComparison.OrdinalIgnoreCase)) return UiText.T("Verbunden");
        if (line.Contains("Logging in", StringComparison.OrdinalIgnoreCase)) return UiText.T("Kamera erreicht – Anmeldung läuft … [LOGIN]");
        if (line.Contains("Trying relay discovery", StringComparison.OrdinalIgnoreCase)) return UiText.T("Verbindung über Reolink-Relay wird versucht … [RELAY]");
        if (line.Contains("Trying map discovery", StringComparison.OrdinalIgnoreCase)) return UiText.T("Direkte P2P-Verbindung wird versucht … [P2P]");
        if (line.Contains("Trying remote discovery", StringComparison.OrdinalIgnoreCase)) return UiText.T("UID wurde gefunden – Verbindung wird aufgebaut … [UID-FOUND]");
        if (line.Contains("Trying local discovery", StringComparison.OrdinalIgnoreCase)) return UiText.T("UID wird gesucht … [UID-SEARCH]");
        return null;
    }
    internal static bool IsFailure(string text) => text.Contains("[AUTH]", StringComparison.Ordinal) ||
        text.Contains("[HELPER-ACCESS]", StringComparison.Ordinal) || text.Contains("[HELPER-RUNTIME]", StringComparison.Ordinal) ||
        text.Contains("[CONFIG]", StringComparison.Ordinal) || text.Contains("[PROTOCOL]", StringComparison.Ordinal);
}
#endif
