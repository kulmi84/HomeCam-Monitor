#if V2_BETA
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using MQTTnet;
using MQTTnet.Protocol;
using MQTTnet.Server;
namespace HomeCamMonitor;

internal sealed class ReolinkBridge : IDisposable
{
    private readonly CameraEntry[] cameras;
    private readonly string executable;
    private readonly string runtimeRoot;
    private readonly string discoveryMethod;
    private readonly ReolinkEvents events;
    private readonly CancellationTokenSource cancellation = new();
    private Process? child;
    private volatile bool credentialsRejected;
    private readonly object gate = new();
    internal int RtspPort { get; } = FreePort();
    internal Task Completion { get; private set; } = Task.CompletedTask;
    internal event Action<CameraEntry>? Motion;
    internal event Action<CameraEntry, string>? Status;
    internal event Action<CameraEntry>? StreamReady;
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, byte> ready = new();
    private readonly ReolinkBridge[] workers = [];
    internal bool IsStreamReady(CameraEntry camera) => workers.Length > 0
        ? workers.Any(worker => worker.IsStreamReady(camera)) : ready.ContainsKey(camera.Reolink.Id);
    internal string StreamUrl(CameraEntry camera) => workers.Length > 0
        ? workers.First(worker => worker.cameras.Any(entry => entry.Reolink.Id == camera.Reolink.Id)).StreamUrl(camera)
        : camera.Reolink.StreamUrl(RtspPort);
    internal ReolinkBridge(IEnumerable<CameraEntry> entries, string executable, string? runtimeRoot = null, string discoveryMethod = "cellular")
    {
        cameras = entries.Where(c => c.Active && c.Reolink.Enabled).ToArray();
        if (cameras.Select(camera => camera.Reolink.Id).Distinct(StringComparer.Ordinal).Count() != cameras.Length)
            throw new ArgumentException(UiText.T("Jede Reolink-Kamera benötigt eine eigene interne Kennung."));
        this.executable = ReolinkHelper.Resolve(executable);
        this.discoveryMethod = discoveryMethod;
        this.runtimeRoot = runtimeRoot ?? Path.Combine(SettingsLocation.DefaultFolder, "reolink-runtime");
        events = new ReolinkEvents(cameras);
        events.Motion += camera => Motion?.Invoke(camera);
        events.Status += (camera, status) => Status?.Invoke(camera, status);
        if (cameras.Length > 1)
        {
            // A bad camera must not restart the UID/video service of another camera.
            workers = cameras.Select(camera => new ReolinkBridge([camera], executable, this.runtimeRoot, discoveryMethod)).ToArray();
            foreach (var worker in workers)
            {
                worker.Motion += camera => Motion?.Invoke(camera);
                worker.Status += (camera, status) => Status?.Invoke(camera, status);
                worker.StreamReady += camera => StreamReady?.Invoke(camera);
            }
        }
        else foreach (var camera in cameras) camera.StreamUrl = StreamUrl(camera);
    }
    internal void Start()
    {
        if (cameras.Length == 0) { Completion = Task.CompletedTask; return; }
        if (workers.Length == 0) Completion = Task.Run(RunAsync);
        else
        {
            foreach (var worker in workers) worker.Start();
            Completion = Task.WhenAll(workers.Select(worker => worker.Completion));
        }
    }
    internal static int FreePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try { return ((IPEndPoint)listener.LocalEndpoint).Port; }
        finally { listener.Stop(); }
    }
    private void Report(string text)
    {
        if (!cancellation.IsCancellationRequested)
            foreach (var camera in cameras) Status?.Invoke(camera, text);
    }
    private async Task RunAsync()
    {
        MqttServer? server = null;
        string? directory = null;
        FileStream? sessionLock = null;
        var token = cancellation.Token;
        try
        {
            if (!File.Exists(executable)) { Report(UiText.T("Reolink-Verbindungsdienst fehlt")); return; }
            using var lifetime = new ReolinkProcessLifetime();
            var mqttPort = FreePort();
            while (mqttPort == RtspPort) mqttPort = FreePort();
            var password = Guid.NewGuid().ToString("N");
            var options = new MqttServerOptionsBuilder().WithDefaultEndpoint()
                .WithDefaultEndpointBoundIPAddress(IPAddress.Loopback)
                .WithDefaultEndpointBoundIPV6Address(IPAddress.IPv6Loopback)
                .WithDefaultEndpointPort(mqttPort).Build();
            server = new MqttFactory().CreateMqttServer(options);
            server.ValidatingConnectionAsync += e =>
            {
                if (e.UserName != "homecam-v2" || e.Password != password)
                    e.ReasonCode = MqttConnectReasonCode.BadUserNameOrPassword;
                return Task.CompletedTask;
            };
            // Inspect publications, not retained subscription replays. Neolink
            // publishes live motion values with the retain bit set as well.
            server.InterceptingPublishAsync += e =>
            {
                if (!token.IsCancellationRequested)
                    events.Accept(e.ApplicationMessage.Topic, Encoding.UTF8.GetString(e.ApplicationMessage.PayloadSegment), false);
                e.ProcessPublish = false; // Camera status is consumed here; controls are not exposed.
                return Task.CompletedTask;
            };
            await server.StartAsync();
            RemoveAbandonedSessions(runtimeRoot);
            directory = Path.Combine(runtimeRoot, "session-" + Guid.NewGuid().ToString("N"));
            CreatePrivateDirectory(directory);
            sessionLock = new FileStream(Path.Combine(directory, "session.lock"), FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None);
            var config = Path.Combine(directory, "connection.toml");
            await File.WriteAllTextAsync(config, ReolinkConfig.Build(cameras, RtspPort, mqttPort, password, discoveryMethod), new UTF8Encoding(false), token);
            var retrySeconds = 2;
            while (!token.IsCancellationRequested)
            {
                Report(UiText.T("Verbinde über UID …"));
                ready.Clear();
                events.Reset();
                using var process = new Process { StartInfo = StartInfo(executable, config) };
                lock (gate)
                {
                    token.ThrowIfCancellationRequested();
                    if (!process.Start()) throw new IOException(UiText.T("Reolink-Verbindungsdienst konnte nicht starten."));
                    lifetime.Attach(process);
                    child = process;
                }
                // Never display or persist helper output: it can contain UIDs,
                // addresses, credentials or the generated configuration.
                var output = ReadDiagnosticsAsync(process.StandardOutput, token);
                var errors = ReadDiagnosticsAsync(process.StandardError, token);
                var started = DateTime.UtcNow;
                try { await process.WaitForExitAsync(token); }
                finally
                {
                    lock (gate)
                    {
                        try { if (!process.HasExited) process.Kill(true); } catch (InvalidOperationException) { }
                        child = null;
                    }
                    try { await Task.WhenAll(output, errors); } catch (OperationCanceledException) { }
                }
                if (token.IsCancellationRequested) break;
                if (credentialsRejected) break;
                if (ReolinkDiagnostics.HelperFailure(process.ExitCode) is { } helperFailure)
                { Report(helperFailure); break; }
                Report(UiText.T("Verbindung unterbrochen – neuer Versuch …"));
                retrySeconds = DateTime.UtcNow - started > TimeSpan.FromMinutes(1) ? 2 : Math.Min(30, retrySeconds * 2);
                await Task.Delay(TimeSpan.FromSeconds(retrySeconds), token);
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (System.ComponentModel.Win32Exception error) when (error.NativeErrorCode == 5)
        { Report(UiText.T("Reolink-Verbindungsdienst: Zugriff verweigert [HELPER-ACCESS]")); }
        catch (ArgumentException) { Report(UiText.T("Kamera-Konfiguration prüfen")); }
        catch (Exception) { Report(UiText.T("Reolink-Verbindung konnte nicht gestartet werden")); }
        finally
        {
            if (server is not null) { try { await server.StopAsync(); } catch { } server.Dispose(); }
            if (directory is not null)
            {
                sessionLock?.Dispose();
                try { File.Delete(Path.Combine(directory, "connection.toml")); File.Delete(Path.Combine(directory, "session.lock")); Directory.Delete(directory); }
                catch (IOException) { } catch (UnauthorizedAccessException) { }
            }
        }
    }
    internal static void RemoveAbandonedSessions(string root)
    {
        if (!Directory.Exists(root)) return;
        foreach (var folder in Directory.EnumerateDirectories(root, "session-*"))
        {
            try
            {
                if ((File.GetAttributes(folder) & FileAttributes.ReparsePoint) != 0) continue;
                var lockPath = Path.Combine(folder, "session.lock");
                // An active session keeps its lock open exclusively.
                using (new FileStream(lockPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) { }
                File.Delete(Path.Combine(folder, "connection.toml")); File.Delete(lockPath);
                Directory.Delete(folder); // Never remove unknown files or recurse.
            }
            catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
    }
    internal static ProcessStartInfo StartInfo(string executable, string config)
    {
        var info = new ProcessStartInfo(executable)
        {
            UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden,
            RedirectStandardOutput = true, RedirectStandardError = true,
            WorkingDirectory = Path.GetDirectoryName(Path.GetFullPath(executable))!
        };
        info.ArgumentList.Add("mqtt-rtsp");
        info.ArgumentList.Add("--config");
        info.ArgumentList.Add(config);
        var gst = Path.Combine(AppContext.BaseDirectory, "tools", "gstreamer");
        if (Directory.Exists(Path.Combine(gst, "bin")))
        {
            info.Environment.TryGetValue("PATH", out var previousPath);
            info.Environment["PATH"] = Path.Combine(gst, "bin") + Path.PathSeparator + previousPath;
            info.Environment["GST_PLUGIN_PATH_1_0"] = Path.Combine(gst, "lib", "gstreamer-1.0");
            info.Environment["GST_PLUGIN_SCANNER_1_0"] = Path.Combine(gst, "libexec", "gstreamer-1.0", "gst-plugin-scanner.exe");
            info.Environment["GST_REGISTRY_1_0"] = Path.Combine(SettingsLocation.DefaultFolder, "gstreamer-registry.bin");
        }
        info.Environment["RUST_LOG"] = "info";
        return info;
    }
    internal static void CreatePrivateDirectory(string path)
    {
        var security = new DirectorySecurity();
        var sid = WindowsIdentity.GetCurrent().User!;
        security.SetAccessRuleProtection(true, false);
        security.AddAccessRule(new FileSystemAccessRule(sid, FileSystemRights.FullControl,
            InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow));
        new DirectoryInfo(path).Create(security);
    }
    private async Task ReadDiagnosticsAsync(StreamReader reader, CancellationToken token)
    {
        var buffer = new char[2048];
        var line = new StringBuilder();
        int read;
        while ((read = await reader.ReadAsync(buffer.AsMemory(), token)) > 0)
        {
            for (var i = 0; i < read; i++)
            {
                if (buffer[i] == '\n')
                {
                    var raw = line.ToString();
                    if (raw.Contains("Avaliable at ", StringComparison.Ordinal))
                        foreach (var camera in cameras)
                            if (raw.Contains("/" + camera.Reolink.MountName + "/", StringComparison.Ordinal) &&
                                ready.TryAdd(camera.Reolink.Id, 0) && !token.IsCancellationRequested)
                                StreamReady?.Invoke(camera);
                    var text = ReolinkDiagnostics.Classify(raw);
                    if (text?.Contains("[AUTH]", StringComparison.Ordinal) == true) credentialsRejected = true;
                    if (text is not null && !token.IsCancellationRequested)
                    {
                        // A single-camera test also receives unscoped discovery errors.
                        // With several cameras only explicitly named messages are routed.
                        foreach (var camera in cameras)
                            if (cameras.Length == 1 || raw.Contains(camera.Reolink.MountName, StringComparison.Ordinal))
                                Status?.Invoke(camera, text);
                    }
                    line.Clear();
                }
                else if (line.Length < 16384) line.Append(buffer[i]);
            }
        }
    }
    public void Dispose()
    {
        foreach (var worker in workers) worker.Dispose();
        cancellation.Cancel();
        lock (gate)
        {
            try { if (child is { HasExited: false }) child.Kill(true); } catch (InvalidOperationException) { }
        }
    }
}
#endif
