#if V2_BETA
using System.Text.Json;
namespace HomeCamMonitor;

internal sealed class DirectCamera
{
    public bool Enabled { get; set; }
    public string DeviceUrl { get; set; } = "";
    public string Username { get; set; } = "admin";
    public string ProtectedPassword { get; set; } = "";
    public string RtspUrl { get; set; } = "";
    public string ProfileToken { get; set; } = "";
    public string DiscoveryId { get; set; } = "";
    internal DirectCamera Clone() => JsonSerializer.Deserialize<DirectCamera>(JsonSerializer.Serialize(this))!;
    internal static DirectCamera ForRow(DataGridViewRow row)
    {
        if (row.Cells["DirectConnection"].Value is DirectCamera config) return config;
        var fresh = new DirectCamera(); row.Cells["DirectConnection"].Value = fresh; return fresh;
    }
    internal string StreamUrl()
    {
        if (!Uri.TryCreate(RtspUrl, UriKind.Absolute, out var uri) || uri.Scheme is not ("rtsp" or "rtsps"))
            throw new ArgumentException(UiText.T("Bitte eine gültige RTSP-Streamadresse eintragen oder über ONVIF ermitteln."));
        if (string.IsNullOrWhiteSpace(Username)) return uri.AbsoluteUri;
        string password;
        try { password = ProtectedPassword.Length == 0 ? "" : CameraPassword.Unprotect(ProtectedPassword); }
        catch (Exception e) when (e is System.Security.Cryptography.CryptographicException or FormatException)
        { throw new ArgumentException(UiText.T("Das Kamera-Passwort ist auf diesem Windows-Konto nicht lesbar. Bitte erneut eingeben.")); }
        var builder = new UriBuilder(uri) { UserName = Uri.EscapeDataString(Username), Password = Uri.EscapeDataString(password) };
        return builder.Uri.AbsoluteUri;
    }
    internal string StreamUrlOrEmpty() { try { return StreamUrl(); } catch (ArgumentException) { return ""; } }
}
#endif
