#if V2_BETA
using System.Net.WebSockets;
using System.Text.Json;
using System.Text.RegularExpressions;
namespace HomeCamMonitor;

internal sealed record HaEntity(string Id, string Name, string State, string DeviceId, string DeviceClass)
{
    internal bool Available => State is not ("unavailable" or "unknown");
    public override string ToString() => Id.Length == 0 ? "Keine Zuordnung" : $"{Name} — {Id} ({State})";
}
internal sealed record HaCatalog(IReadOnlyList<HaEntity> Cameras, IReadOnlyList<HaEntity> Sensors, bool RegistryAvailable)
{
    internal HaEntity? Suggest(HaEntity camera, bool person)
    {
        if (camera.DeviceId.Length == 0) return null;
        var matches = Sensors.Where(s => s.Available && s.DeviceId == camera.DeviceId && (person
            ? Regex.IsMatch(s.Id + " " + s.Name, @"person|people|human|mensch", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)
            : s.DeviceClass == "motion" || Regex.IsMatch(s.Id + " " + s.Name, @"motion|bewegung|movement|\bpir\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))).ToArray();
        return matches.Length == 1 ? matches[0] : null;
    }
}
internal sealed class HaApi : IDisposable
{
    private readonly ClientWebSocket socket = new();
    private int nextId;
    internal static Uri Address(string address)
    {
        if (!Uri.TryCreate(address.Trim().TrimEnd('/') + "/", UriKind.Absolute, out var uri) ||
            uri.Scheme is not ("http" or "https") || uri.UserInfo.Length > 0 || uri.Query.Length > 0 || uri.Fragment.Length > 0)
            throw new ArgumentException("Eine gültige HTTP-/HTTPS-Adresse von Home Assistant eintragen.");
        return uri;
    }
    internal async Task Connect(string address, string token, bool ignoreCertificate, CancellationToken cancel)
    {
        var baseUri = Address(address);
        if (string.IsNullOrWhiteSpace(token)) throw new ArgumentException("Den Home-Assistant-Langzeit-Token eintragen.");
        if (ignoreCertificate) socket.Options.RemoteCertificateValidationCallback = (_, _, _, _) => true;
        await socket.ConnectAsync(MonitorForm.BuildHomeAssistantWebSocketUri(baseUri.AbsoluteUri), cancel).ConfigureAwait(false);
        using var required = await Receive(cancel).ConfigureAwait(false);
        if (Text(required.RootElement, "type") != "auth_required") throw new InvalidDataException("Unerwartete HA-Anmeldung.");
        await MonitorForm.SendHomeAssistantMessageAsync(socket, new { type = "auth", access_token = token }, cancel).ConfigureAwait(false);
        using var auth = await Receive(cancel).ConfigureAwait(false);
        if (Text(auth.RootElement, "type") != "auth_ok") throw new InvalidDataException("HA-Anmeldung fehlgeschlagen. Langzeit-Token prüfen.");
    }
    private async Task<JsonDocument> Receive(CancellationToken cancel)
    {
        using var memory = new MemoryStream(); var buffer = new byte[8192];
        WebSocketReceiveResult result;
        do
        {
            result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), cancel).ConfigureAwait(false);
            if (result.MessageType != WebSocketMessageType.Text || memory.Length + result.Count > 8 * 1024 * 1024)
                throw new InvalidDataException("Ungültige oder zu große HA-Antwort.");
            memory.Write(buffer, 0, result.Count);
        } while (!result.EndOfMessage);
        return JsonDocument.Parse(memory.ToArray());
    }
    internal async Task<JsonElement?> Request(string type, CancellationToken cancel, string? entity = null, bool optional = false)
    {
        var id = ++nextId;
        var command = new Dictionary<string, object> { ["id"] = id, ["type"] = type };
        if (entity is not null) { command["entity_id"] = entity; command["format"] = "hls"; }
        await MonitorForm.SendHomeAssistantMessageAsync(socket, command, cancel).ConfigureAwait(false);
        while (true)
        {
            using var message = await Receive(cancel).ConfigureAwait(false); var root = message.RootElement;
            if (!root.TryGetProperty("id", out var responseId) || responseId.GetInt32() != id) continue;
            if (!root.TryGetProperty("success", out var success) || success.ValueKind != JsonValueKind.True)
            {
                if (optional) return null;
                throw new InvalidDataException(type == "camera/stream" ? "Home Assistant stellt für diese Kamera keinen HLS-Stream bereit. Alternativ eine vorhandene RTSP-/HTTP-Adresse verwenden." : "Home Assistant konnte die Entitäten nicht liefern. Benutzerrechte prüfen.");
            }
            return root.TryGetProperty("result", out var value) ? value.Clone() : null;
        }
    }
    internal static async Task<HaCatalog> Discover(string address, string token, bool ignoreCertificate, CancellationToken cancel)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancel); timeout.CancelAfter(TimeSpan.FromSeconds(35));
        using var api = new HaApi(); await api.Connect(address, token, ignoreCertificate, timeout.Token).ConfigureAwait(false);
        var states = await api.Request("get_states", timeout.Token).ConfigureAwait(false);
        var registry = await api.Request("config/entity_registry/list", timeout.Token, optional: true).ConfigureAwait(false);
        return Parse(states ?? throw new InvalidDataException("Keine HA-Entitäten geliefert."), registry);
    }
    internal static HaCatalog Parse(JsonElement states, JsonElement? registry)
    {
        if (states.ValueKind != JsonValueKind.Array) throw new InvalidDataException("Ungültige HA-Entitätenliste.");
        var devices = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (registry is { ValueKind: JsonValueKind.Array } entries)
            foreach (var entry in entries.EnumerateArray()) devices[Text(entry, "entity_id")] = Text(entry, "device_id");
        var entities = new Dictionary<string, HaEntity>(StringComparer.OrdinalIgnoreCase);
        foreach (var state in states.EnumerateArray())
        {
            var id = Text(state, "entity_id");
            if (!Regex.IsMatch(id, @"^(camera|binary_sensor)\.[a-z0-9_]+$", RegexOptions.CultureInvariant)) continue;
            var attributes = state.TryGetProperty("attributes", out var a) ? a : default;
            var name = Text(attributes, "friendly_name");
            entities[id] = new HaEntity(id, name.Length > 0 ? name : id, Text(state, "state"), devices.GetValueOrDefault(id, ""), Text(attributes, "device_class"));
        }
        return new HaCatalog(entities.Values.Where(e => e.Id.StartsWith("camera.")).OrderBy(e => e.Name).ToArray(),
            entities.Values.Where(e => e.Id.StartsWith("binary_sensor.")).OrderBy(e => e.Name).ToArray(), registry is { ValueKind: JsonValueKind.Array });
    }
    internal static string Text(JsonElement value, string key) => value.ValueKind == JsonValueKind.Object && value.TryGetProperty(key, out var child) && child.ValueKind == JsonValueKind.String ? child.GetString() ?? "" : "";
    internal static string Source(string id) => "homeassistant://" + id;
    internal static string? Entity(string source) => Regex.IsMatch(source, @"^homeassistant://camera\.[a-z0-9_]+/?$", RegexOptions.CultureInvariant) ? source[16..].TrimEnd('/') : null;
    public void Dispose() => socket.Dispose();
}
#endif
