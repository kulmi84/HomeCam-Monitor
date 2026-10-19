#if V2_BETA
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Xml;
using System.Xml.Linq;
namespace HomeCamMonitor;

internal sealed record OnvifProfile(string Token, string Name, string StreamUrl)
{
    public override string ToString() => Name;
}

internal sealed class OnvifClient : IDisposable
{
    private static readonly XNamespace Soap = "http://www.w3.org/2003/05/soap-envelope";
    private static readonly XNamespace Device = "http://www.onvif.org/ver10/device/wsdl";
    private static readonly XNamespace Media = "http://www.onvif.org/ver10/media/wsdl";
    private static readonly XNamespace Schema = "http://www.onvif.org/ver10/schema";
    private static readonly XNamespace Security = "http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-secext-1.0.xsd";
    private static readonly XNamespace Utility = "http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-utility-1.0.xsd";
    private readonly HttpClient client;
    private readonly string username;
    private readonly string password;
    private TimeSpan clockOffset;
    private readonly bool sameHostOnly;
    internal OnvifClient(string username, string password, bool sameHostOnly = false)
    {
        this.username = username; this.password = password;
        this.sameHostOnly = sameHostOnly;
        client = new HttpClient(new HttpClientHandler { Credentials = new NetworkCredential(username, password),
            AllowAutoRedirect = false, UseProxy = false }) { Timeout = TimeSpan.FromSeconds(15), MaxResponseContentBufferSize = 2 * 1024 * 1024 };
    }
    internal async Task<(string Manufacturer, string Model)> Information(string address, CancellationToken token)
    {
        var response = await Call(DeviceAddress(address), Device + "GetDeviceInformation", new XElement(Device + "GetDeviceInformation"), token);
        string Value(string name) => new((response.Descendants(Device + name).FirstOrDefault()?.Value ?? "").Where(c => !char.IsControl(c)).Take(128).ToArray());
        return (Value("Manufacturer"), Value("Model"));
    }
    internal static Uri DeviceAddress(string address)
    {
        var text = address.Trim();
        if (!text.Contains("://", StringComparison.Ordinal)) text = "http://" + text;
        if (!Uri.TryCreate(text, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https") ||
            uri.UserInfo.Length > 0 || string.IsNullOrEmpty(uri.Host)) throw new ArgumentException(UiText.T("Bitte eine gültige ONVIF-Adresse mit Port eintragen."));
        return uri.AbsolutePath == "/" ? new Uri(uri, "/onvif/device_service") : uri;
    }
    internal async Task<IReadOnlyList<OnvifProfile>> Profiles(string address, CancellationToken token)
    {
        var device = DeviceAddress(address);
        try
        {
            var time = await Call(device, Device + "GetSystemDateAndTime", new XElement(Device + "GetSystemDateAndTime"), token, false);
            var utc = time.Descendants().FirstOrDefault(x => x.Name.LocalName == "UTCDateTime");
            if (utc is not null)
            {
                int Part(string name) => int.Parse(utc.Descendants().First(x => x.Name.LocalName == name).Value, System.Globalization.CultureInfo.InvariantCulture);
                clockOffset = new DateTime(Part("Year"), Part("Month"), Part("Day"), Part("Hour"), Part("Minute"), Part("Second"), DateTimeKind.Utc) - DateTime.UtcNow;
            }
        }
        catch (Exception e) when (e is HttpRequestException or InvalidDataException or InvalidOperationException or FormatException) { }
        var capabilities = await Call(device, Device + "GetCapabilities", new XElement(Device + "GetCapabilities", new XElement(Device + "Category", "Media")), token);
        var mediaAddress = capabilities.Descendants(Schema + "Media").Elements(Schema + "XAddr").FirstOrDefault()?.Value;
        if (!Uri.TryCreate(mediaAddress, UriKind.Absolute, out var media) || media.Scheme is not ("http" or "https") || media.UserInfo.Length > 0)
            throw new InvalidDataException(UiText.T("Die Kamera meldet keinen unterstützten ONVIF-Mediendienst."));
        if (sameHostOnly && !media.Host.Equals(device.Host, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException(UiText.T("Der gemeldete Mediendienst liegt auf einem anderen Gerät. Adresse bitte manuell prüfen."));
        var response = await Call(media, Media + "GetProfiles", new XElement(Media + "GetProfiles"), token);
        var result = new List<OnvifProfile>();
        foreach (var profile in response.Descendants(Media + "Profiles").Take(64))
        {
            var id = profile.Attribute("token")?.Value;
            if (string.IsNullOrWhiteSpace(id)) continue;
            var name = profile.Element(Schema + "Name")?.Value ?? id;
            var uriResponse = await Call(media, Media + "GetStreamUri", new XElement(Media + "GetStreamUri",
                new XElement(Media + "StreamSetup", new XElement(Schema + "Stream", "RTP-Unicast"),
                    new XElement(Schema + "Transport", new XElement(Schema + "Protocol", "RTSP"))), new XElement(Media + "ProfileToken", id)), token);
            var stream = uriResponse.Descendants(Schema + "Uri").FirstOrDefault()?.Value;
            if (Uri.TryCreate(stream, UriKind.Absolute, out var parsed) && parsed.Scheme is "rtsp" or "rtsps")
            {
                // Keep credentials in the protected password field, not in the displayed address.
                var clean = new UriBuilder(parsed) { UserName = "", Password = "" }.Uri.AbsoluteUri;
                result.Add(new OnvifProfile(id, name, clean));
            }
        }
        if (result.Count == 0) throw new InvalidDataException(UiText.T("Die Kamera meldet keine unterstützten RTSP-Profile."));
        return result;
    }
    private async Task<XDocument> Call(Uri endpoint, XName action, XElement body, CancellationToken token, bool authenticate = true)
    {
        var header = new XElement(Soap + "Header");
        if (authenticate && username.Length > 0)
        {
            var nonce = RandomNumberGenerator.GetBytes(20);
            var created = (DateTime.UtcNow + clockOffset).ToString("yyyy-MM-ddTHH:mm:ss.fffZ", System.Globalization.CultureInfo.InvariantCulture);
            var digest = Convert.ToBase64String(SHA1.HashData(nonce.Concat(Encoding.UTF8.GetBytes(created + password)).ToArray()));
            header.Add(new XElement(Security + "Security", new XAttribute(Soap + "mustUnderstand", "true"),
                new XElement(Security + "UsernameToken", new XElement(Security + "Username", username),
                    new XElement(Security + "Password", new XAttribute("Type", "http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-username-token-profile-1.0#PasswordDigest"), digest),
                    new XElement(Security + "Nonce", new XAttribute("EncodingType", "http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-soap-message-security-1.0#Base64Binary"), Convert.ToBase64String(nonce)),
                    new XElement(Utility + "Created", created))));
        }
        var envelope = new XElement(Soap + "Envelope", header, new XElement(Soap + "Body", body));
        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint) { Content = new StringContent(envelope.ToString(SaveOptions.DisableFormatting), Encoding.UTF8) };
        request.Content.Headers.ContentType = MediaTypeHeaderValue.Parse($"application/soap+xml; charset=utf-8; action=\"{action.NamespaceName}/{action.LocalName}\"");
        using var response = await client.SendAsync(request, token).ConfigureAwait(false);
        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden) throw new InvalidDataException(UiText.T("Die Kamera lehnt die ONVIF-Anmeldung ab. Kamera-Benutzer und Passwort prüfen."));
        if (!response.IsSuccessStatusCode) throw new InvalidDataException(UiText.F("ONVIF-Anfrage fehlgeschlagen (HTTP {0}).", (int)response.StatusCode));
        var xml = await response.Content.ReadAsStringAsync(token).ConfigureAwait(false);
        using var reader = XmlReader.Create(new StringReader(xml), new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 2 * 1024 * 1024 });
        var document = XDocument.Load(reader);
        if (document.Descendants(Soap + "Fault").Any()) throw new InvalidDataException(UiText.T("Die Kamera konnte die ONVIF-Anfrage nicht ausführen. ONVIF-Freigabe und Benutzerrechte prüfen."));
        return document;
    }
    public void Dispose() => client.Dispose();
}
#endif
