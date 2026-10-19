#if V2_BETA
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
namespace HomeCamMonitor;

internal sealed class ReolinkCamera
{
    public bool Enabled { get; set; }
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Uid { get; set; } = "";
    public string Username { get; set; } = "admin";
    public string ProtectedPassword { get; set; } = "";
    public string Stream { get; set; } = "subStream";
    internal string MountName => "camera_" + Id;
    internal string StreamUrl(int port) => $"rtsp://127.0.0.1:{port}/{MountName}/{Stream}";
    internal ReolinkCamera Clone() => JsonSerializer.Deserialize<ReolinkCamera>(JsonSerializer.Serialize(this))!;
    internal void Validate()
    {
        if (!Enabled) return;
        if (!Regex.IsMatch(Id ?? "", "^[a-f0-9]{32}$") ||
            !Regex.IsMatch(Uid ?? "", "^[A-Za-z0-9]{10,32}$") ||
            string.IsNullOrWhiteSpace(Username) || Username.Length > 128 ||
            Stream is not ("mainStream" or "subStream") || string.IsNullOrEmpty(ProtectedPassword))
            throw new ArgumentException(UiText.T("Bitte Kamera-UID, Kamera-Benutzername und Passwort vollständig eintragen."));
        try { _ = CameraPassword.Unprotect(ProtectedPassword); }
        catch (Exception e) when (e is CryptographicException or FormatException)
        { throw new ArgumentException(UiText.T("Das Kamera-Passwort ist auf diesem Windows-Konto nicht lesbar. Bitte erneut eingeben.")); }
    }
}

internal static class CameraPassword
{
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("HomeCamMonitor-V2-Reolink");
    internal static string Protect(string password) => Convert.ToBase64String(
        ProtectedData.Protect(Encoding.UTF8.GetBytes(password), Entropy, DataProtectionScope.CurrentUser));
    internal static string Unprotect(string protectedPassword) => Encoding.UTF8.GetString(
        ProtectedData.Unprotect(Convert.FromBase64String(protectedPassword), Entropy, DataProtectionScope.CurrentUser));
}

internal static class ReolinkConfig
{
    // Keep Unicode scalar values literal. JSON's UTF-16 surrogate escapes are
    // not valid TOML Unicode escapes, so JSON serialization alone is unsuitable.
    internal static string Quote(string value)
    {
        var text = new StringBuilder("\"");
        foreach (var rune in value.EnumerateRunes())
        {
            text.Append(rune.Value switch
            {
                34 => "\\\"", 92 => "\\\\", 8 => "\\b", 9 => "\\t", 10 => "\\n", 12 => "\\f", 13 => "\\r",
                < 32 or 127 => "\\u" + rune.Value.ToString("X4"),
                _ => rune.ToString()
            });
        }
        return text.Append('"').ToString();
    }
    internal static string Build(IEnumerable<CameraEntry> cameras, int rtspPort, int mqttPort, string mqttPassword, string discoveryMethod = "cellular")
    {
        if (discoveryMethod is not ("relay" or "cellular" or "debug")) throw new ArgumentException(UiText.T("Ungültiger UID-Verbindungsmodus."));
        var enabled = cameras.Where(c => c.Active && c.Reolink.Enabled).ToArray();
        var text = new StringBuilder();
        text.AppendLine("bind = \"127.0.0.1\"");
        text.AppendLine($"bind_port = {rtspPort}");
        if (enabled.Length == 0) text.AppendLine("cameras = []");
        text.AppendLine("[mqtt]");
        text.AppendLine("broker_addr = \"127.0.0.1\"");
        text.AppendLine($"port = {mqttPort}");
        text.AppendLine($"credentials = [\"homecam-v2\", {Quote(mqttPassword)}]");
        var ids = new HashSet<string>();
        foreach (var camera in enabled)
        {
            var config = camera.Reolink;
            config.Validate();
            if (!ids.Add(config.Id)) throw new ArgumentException(UiText.T("Jede Reolink-Kamera benötigt eine eigene interne Kennung."));
            text.AppendLine("[[cameras]]");
            text.AppendLine($"name = {Quote(config.MountName)}");
            text.AppendLine($"uid = {Quote(config.Uid)}");
            text.AppendLine($"username = {Quote(config.Username)}");
            text.AppendLine($"password = {Quote(CameraPassword.Unprotect(config.ProtectedPassword))}");
            text.AppendLine($"discovery = {Quote(discoveryMethod)}");
            text.AppendLine($"stream = {Quote(config.Stream)}");
            text.AppendLine("use_splash = false");
            text.AppendLine("[cameras.mqtt]");
            text.AppendLine("enable_motion = true");
            text.AppendLine("enable_preview = false");
            text.AppendLine("enable_battery = false");
            text.AppendLine("enable_light = false");
        }
        return text.ToString();
    }
}

internal sealed class ReolinkEvents
{
    private readonly Dictionary<string, CameraEntry> cameras;
    private readonly HashSet<string> motion = new();
    private readonly object gate = new();
    internal ReolinkEvents(IEnumerable<CameraEntry> entries) => cameras = entries.Where(c => c.Active && c.Reolink.Enabled)
        .ToDictionary(c => c.Reolink.MountName, StringComparer.Ordinal);
    internal event Action<CameraEntry>? Motion;
    internal event Action<CameraEntry, string>? Status;
    internal void Accept(string topic, string payload, bool retained)
    {
        var parts = topic.Split('/');
        if (parts.Length < 3 || parts[0] != "neolink" || !cameras.TryGetValue(parts[1], out var camera)) return;
        if (parts.Length == 3 && parts[2] == "status")
        {
            if (payload is not ("connected" or "disconnected")) return;
            if (payload == "disconnected") lock (gate) motion.Remove(parts[1]);
            Status?.Invoke(camera, payload == "connected" ? UiText.T("Verbunden") : UiText.T("Verbinde neu …"));
        }
        if (parts.Length != 4 || parts[2] != "status" || parts[3] != "motion") return;
        bool trigger = false;
        lock (gate)
        {
            if (payload == "on") trigger = motion.Add(parts[1]) && !retained;
            else if (payload is "off" or "unknown") motion.Remove(parts[1]);
        }
        if (trigger && camera.MotionEnabled) Motion?.Invoke(camera);
    }
    internal void Reset() { lock (gate) motion.Clear(); }
}
#endif
