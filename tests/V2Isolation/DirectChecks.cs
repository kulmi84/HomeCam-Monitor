using HomeCamMonitor;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
namespace V2Isolation;

internal static partial class Check
{
    private static void TestPerCameraDetection()
    {
        var camera = new CameraEntry { Name = "Motion", StreamUrl = "rtsp://example.invalid/camera", MotionEnabled = true };
        var settings = new Settings { Cameras = [camera], MotionRetentionDays = 0 };
        var node = System.Text.Json.Nodes.JsonNode.Parse(SettingsBackup.Serialize(settings))!;
        node["Settings"]!["MotionDetectionEnabled"] = false;
        var migrated = SettingsBackup.Parse(node.ToJsonString());
        if (!migrated.MotionDetectionEnabled) throw new Exception("Legacy disabled master still overrides camera motion checkbox.");
        camera.MotionEnabled = false;
        if (settings.MotionDetectionEnabled) throw new Exception("All camera checkboxes off should disable detection.");
        camera.PersonEnabled = true; camera.PersonEntityId = "binary_sensor.person";
        if (!settings.MotionDetectionEnabled) throw new Exception("Person-only camera detection was disabled.");
        camera.Active = false;
        if (settings.MotionDetectionEnabled) throw new Exception("Inactive camera enabled detection.");
        camera.Active = true; camera.Direct.Enabled = true;
        if (settings.MotionDetectionEnabled) throw new Exception("Image-only direct camera enabled event detection.");
        camera.Direct.Enabled = false;
        if (JsonDocument.Parse(SettingsBackup.Serialize(settings)).RootElement.GetProperty("Settings").TryGetProperty("MotionDetectionEnabled", out _))
            throw new Exception("Removed master switch remains in V2 backups.");
        using var form = new SettingsForm(settings, SettingsLocation.DefaultFolder);
        if (form.Controls.Find("DetectionEnabled", true).Length != 0) throw new Exception("Master switch remains in settings UI.");
        using var monitor = new MonitorForm(settings);
        using var menu = new ContextMenuStrip();
        typeof(MonitorForm).GetMethod("PopulateCameraContextMenu", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.Invoke(monitor, [menu]);
        if (menu.Items.Cast<ToolStripItem>().Any(i => i.Text == "Bewegungserkennung aktiv") ||
            menu.Items.OfType<ToolStripMenuItem>().SelectMany(i => i.DropDownItems.Cast<ToolStripItem>()).Any(i => i.Text == "Bis manuell aktiviert"))
            throw new Exception("Removed permanent master switch remains in context menu.");
        Console.WriteLine("Per-camera-only detection, legacy master migration, person-only/disabled/direct routing and menu cleanup passed.");
    }
    private static void TestDirectConnection()
    {
        Task.Run(TestOnvifProtocol).GetAwaiter().GetResult();
        using (var empty = new SettingsForm(SettingsStore.CreateForNewInstallation(), SettingsLocation.DefaultFolder))
        {
            empty.PrepareForDisplay();
            typeof(Button).GetMethod("OnClick", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
                .Invoke(empty.Controls.Find("AddDirectCamera", true).Single(), [EventArgs.Empty]);
            var added = FindGrid(empty).Rows.Cast<DataGridViewRow>().Where(r => !r.IsNewRow).ToArray();
            if (added.Length != 1 || !DirectCamera.ForRow(added[0]).Enabled ||
                !((CheckBox)empty.Controls.Find("UseDirectCamera", true).Single()).Checked ||
                ((V2ConnectionTabs)empty.Controls.Find("CameraConnectionTabs", true).Single()).SelectedIndex != 2)
                throw new Exception("Adding the first direct camera did not activate its source/tab.");
        }
        var camera = new CameraEntry { Name = "Mixed", StreamUrl = "rtsp://example.invalid/ha", HomeAssistantStreamUrl = "rtsp://example.invalid/ha",
            MotionEnabled = true, PersonEnabled = true, PersonEntityId = "binary_sensor.person", MotionAction = "Both",
            Reolink = new ReolinkCamera { Uid = "FAKE0123456789AB", ProtectedPassword = CameraPassword.Protect("uid-secret") } };
        using var form = new SettingsForm(new Settings { Cameras = [camera], DirectHomeAssistantEnabled = true, MotionDetectionEnabled = true }, SettingsLocation.DefaultFolder);
        form.PrepareForDisplay();
        var grid = FindGrid(form);
        var tabs = (V2ConnectionTabs)form.Controls.Find("CameraConnectionTabs", true).Single();
        if (tabs.TabCount != 3) throw new Exception("Direct camera tab missing.");
        TextBox Field(string name) => (TextBox)form.Controls.Find(name, true).Single();
        Field("DirectUsername").Text = "direct-user";
        Field("DirectPassword").Text = "password :&@ Grüße";
        Field("DirectRtspUrl").Text = "rtsp://example.invalid:554/direct?profile=sub";
        Field("OnvifAddress").Text = "http://example.invalid:8000/onvif/device_service";
        ((CheckBox)form.Controls.Find("UseDirectCamera", true).Single()).Checked = true;
        var direct = DirectCamera.ForRow(grid.Rows[0]);
        if (!direct.Enabled || ((ReolinkCamera)grid.Rows[0].Tag!).Enabled || tabs.SelectedIndex != 2 ||
            grid.Rows[0].Cells["MotionEnabled"].ReadOnly || !grid.Rows[0].Cells["PersonEnabled"].ReadOnly ||
            !grid.Rows[0].Cells["RecordingEnabled"].ReadOnly || grid.Rows[0].Cells["Active"].ReadOnly)
            throw new Exception("Direct source activation/capability UI failed.");
        var url = new Uri(Convert.ToString(grid.Rows[0].Cells["StreamUrl"].Value)!);
        if (!Uri.UnescapeDataString(url.UserInfo).Equals("direct-user:password :&@ Grüße", StringComparison.Ordinal)) throw new Exception("Direct RTSP credentials were not escaped correctly.");
        var editor = (ReolinkEditor)form.Controls.Find("ReolinkConnection", true).Single();
        FindCheckbox(editor).Checked = true;
        if (direct.Enabled || !((ReolinkCamera)grid.Rows[0].Tag!).Enabled || Convert.ToString(grid.Rows[0].Cells["HomeAssistantStreamUrl"].Value) != camera.StreamUrl)
            throw new Exception("Direct to UID switch lost source separation or HA URL.");
        ((CheckBox)form.Controls.Find("UseHomeAssistantCamera", true).Single()).Checked = true;
        if (direct.Enabled || ((ReolinkCamera)grid.Rows[0].Tag!).Enabled || Convert.ToString(grid.Rows[0].Cells["StreamUrl"].Value) != camera.StreamUrl)
            throw new Exception("Switch back to Home Assistant lost its stream.");
        ((CheckBox)form.Controls.Find("UseDirectCamera", true).Single()).Checked = true;
        var read = (List<CameraEntry>)typeof(SettingsForm).GetMethod("ReadCameras", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.Invoke(form, null)!;
        var backup = SettingsBackup.Parse(SettingsBackup.Serialize(new Settings { Cameras = read }));
        if (!backup.Cameras[0].Direct.Enabled || backup.Cameras[0].ReceivesHomeAssistantEvents ||
            CameraPassword.Unprotect(backup.Cameras[0].Direct.ProtectedPassword) != "password :&@ Grüße" ||
            backup.Cameras[0].HomeAssistantStreamUrl != camera.StreamUrl)
            throw new Exception("Direct camera backup/restore changed credentials, source or HA cache.");
        var report = DiagnosticExport.Serialize(new Settings { Cameras = read }, new { }, new { });
        if (report.Contains("password", StringComparison.OrdinalIgnoreCase) || report.Contains("direct-user", StringComparison.Ordinal))
            throw new Exception("Direct camera diagnostics exposed credentials.");
        TestDarkDropdown(form);
        Console.WriteLine("ONVIF protocol, three-source switching, direct credentials/backup/privacy and dark dropdown rendering passed.");
    }
    private static async Task TestOnvifProtocol()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        const string secret = "onvif-secret & Grüße";
        var serve = Task.Run(async () =>
        {
            try
            {
                for (var requestIndex = 0; requestIndex < 5; requestIndex++)
                {
                    using var socket = await listener.AcceptTcpClientAsync(timeout.Token);
                    await using var stream = socket.GetStream();
                    var header = new List<byte>();
                    var one = new byte[1];
                    while (header.Count < 16000)
                    {
                        if (await stream.ReadAsync(one, timeout.Token) == 0) throw new IOException("Truncated request.");
                        header.Add(one[0]);
                        if (header.Count >= 4 && header.TakeLast(4).SequenceEqual(new byte[] { 13, 10, 13, 10 })) break;
                    }
                    var headers = Encoding.ASCII.GetString(header.ToArray());
                    var length = int.Parse(headers.Split("\r\n").Single(line => line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase)).Split(':')[1]);
                    var bodyBytes = new byte[length]; await stream.ReadExactlyAsync(bodyBytes, timeout.Token);
                    var body = XDocument.Parse(Encoding.UTF8.GetString(bodyBytes));
                    var operation = body.Descendants().Single(x => x.Name.LocalName == "Body").Elements().Single();
                    if (requestIndex > 0)
                    {
                        string Value(string name) => body.Descendants().Single(x => x.Name.LocalName == name).Value;
                        if (Value("Username") != "test-user") throw new Exception("ONVIF username missing.");
                        var expected = Convert.ToBase64String(SHA1.HashData(Convert.FromBase64String(Value("Nonce")).Concat(Encoding.UTF8.GetBytes(Value("Created") + secret)).ToArray()));
                        if (Value("Password") != expected || Encoding.UTF8.GetString(bodyBytes).Contains(secret)) throw new Exception("ONVIF WS-Security digest failed.");
                    }
                    var payload = operation.Name.LocalName switch
                    {
                        "GetSystemDateAndTime" => "<tds:GetSystemDateAndTimeResponse/>",
                        "GetCapabilities" => $"<tds:GetCapabilitiesResponse><tds:Capabilities><tt:Media><tt:XAddr>http://127.0.0.1:{port}/media</tt:XAddr></tt:Media></tds:Capabilities></tds:GetCapabilitiesResponse>",
                        "GetProfiles" => "<trt:GetProfilesResponse><trt:Profiles token=\"main\"><tt:Name>Hauptstream</tt:Name></trt:Profiles><trt:Profiles token=\"sub\"><tt:Name>Substream</tt:Name></trt:Profiles></trt:GetProfilesResponse>",
                        "GetStreamUri" => $"<trt:GetStreamUriResponse><trt:MediaUri><tt:Uri>rtsp://127.0.0.1:554/{operation.Descendants().Single(x => x.Name.LocalName == "ProfileToken").Value}</tt:Uri></trt:MediaUri></trt:GetStreamUriResponse>",
                        _ => throw new Exception("Unexpected ONVIF operation.")
                    };
                    var xml = $"<s:Envelope xmlns:s=\"http://www.w3.org/2003/05/soap-envelope\" xmlns:tds=\"http://www.onvif.org/ver10/device/wsdl\" xmlns:trt=\"http://www.onvif.org/ver10/media/wsdl\" xmlns:tt=\"http://www.onvif.org/ver10/schema\"><s:Body>{payload}</s:Body></s:Envelope>";
                    var bytes = Encoding.UTF8.GetBytes(xml);
                    var response = Encoding.ASCII.GetBytes($"HTTP/1.1 200 OK\r\nContent-Type: application/soap+xml; charset=utf-8\r\nContent-Length: {bytes.Length}\r\nConnection: close\r\n\r\n");
                    await stream.WriteAsync(response, timeout.Token); await stream.WriteAsync(bytes, timeout.Token);
                }
            }
            finally { listener.Stop(); }
        }, timeout.Token);
        using var client = new OnvifClient("test-user", secret);
        var profiles = await client.Profiles($"http://127.0.0.1:{port}", timeout.Token);
        await serve;
        if (profiles.Count != 2 || profiles[1].Token != "sub" || new Uri(profiles[1].StreamUrl).AbsolutePath != "/sub" || new Uri(profiles[1].StreamUrl).Port != 554)
            throw new Exception("ONVIF profiles or RTSP URL parsing failed.");
    }
    private static void TestDarkDropdown(SettingsForm form)
    {
        IEnumerable<Control> All(Control parent) { foreach (Control child in parent.Controls) { yield return child; foreach (var descendant in All(child)) yield return descendant; } }
        var combos = All(form).OfType<ComboBox>().ToArray();
        if (combos.Length < 9 || combos.Any(c => c is not DarkComboBox)) throw new Exception("Some dropdowns still use native light styling.");
        using var combo = new DarkComboBox { Width = 220, DropDownStyle = ComboBoxStyle.DropDownList };
        for (var i = 0; i < 20; i++) combo.Items.Add("Auswahl " + i);
        combo.SelectedIndex = 0; combo.CreateControl();
        using var bitmap = new System.Drawing.Bitmap(combo.Width, combo.Height);
        combo.DrawToBitmap(bitmap, new System.Drawing.Rectangle(0, 0, combo.Width, combo.Height));
        var arrow = bitmap.GetPixel(combo.Width - 5, 5);
        if (arrow.R > 70 || arrow.G > 70 || arrow.B > 70) throw new Exception("Dropdown arrow background remains light.");
        combo.Enabled = false;
        combo.DrawToBitmap(bitmap, new System.Drawing.Rectangle(0, 0, combo.Width, combo.Height));
        arrow = bitmap.GetPixel(combo.Width - 5, 5);
        if (arrow.R > 70 || arrow.G > 70 || arrow.B > 70) throw new Exception("Disabled dropdown arrow background remains light.");
        combo.Enabled = true;
        using var popup = combo.CreateList();
        popup.Opacity = 0; popup.Show(new System.Drawing.Point(-30000, -30000)); Pump(100); popup.PerformLayout();
        using var listImage = new System.Drawing.Bitmap(popup.Width, popup.Height);
        popup.DrawToBitmap(listImage, new System.Drawing.Rectangle(0, 0, popup.Width, popup.Height));
        var background = listImage.GetPixel(3, listImage.Height - 5);
        if (background.R > 70 || background.G > 70 || background.B > 70) throw new Exception("Dropdown list background remains light.");
        listImage.Save(Path.Combine(Environment.CurrentDirectory, "v2-dropdown.png"));
        var list = ((ToolStripControlHost)popup.Items[0]).Control.Controls.Find("DropdownItems", true).Single();
        typeof(Control).GetMethod("OnKeyDown", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.Invoke(list, [new KeyEventArgs(Keys.End)]);
        typeof(Control).GetMethod("OnKeyDown", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.Invoke(list, [new KeyEventArgs(Keys.Enter)]);
        if (combo.SelectedIndex != 19) throw new Exception("Dark dropdown keyboard selection failed.");
    }
}
