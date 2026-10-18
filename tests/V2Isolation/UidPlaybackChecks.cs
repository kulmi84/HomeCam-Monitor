using System.Diagnostics;
using HomeCamMonitor;
namespace V2Isolation;

internal static partial class Check
{
    private static void TestUidPlaybackOptions()
    {
        var runtime = Environment.GetEnvironmentVariable("HOMECAM_TEST_RUNTIME") ?? Path.Combine(Environment.CurrentDirectory, "publish-v2-beta");
        var player = Path.Combine(runtime, "mpv.exe");
        var script = Path.Combine(Path.GetTempPath(), "homecam-player-options-" + Guid.NewGuid().ToString("N") + ".lua");
        File.WriteAllText(script, "mp.msg.info('HCM_OPTIONS=' .. mp.get_property('options/demuxer-lavf-o')); mp.commandv('quit')");
        try
        {
            foreach (var uid in new[] { false, true })
            {
                var start = new ProcessStartInfo(player) { UseShellExecute = false, CreateNoWindow = true,
                    RedirectStandardOutput = true, RedirectStandardError = true };
                foreach (var arg in new[] { "--no-config", "--idle=yes", "--no-input-terminal", "--profile=low-latency",
                    "--demuxer-lavf-o=rtsp_transport=tcp", "--script=" + script }) start.ArgumentList.Add(arg);
                ReolinkPlayback.Configure(start, uid);
                using var process = Process.Start(start)!;
                var output = process.StandardOutput.ReadToEndAsync(); var errors = process.StandardError.ReadToEndAsync();
                if (!process.WaitForExit(10000)) { process.Kill(true); throw new Exception("Player option test timed out."); }
                var actual = output.GetAwaiter().GetResult() + errors.GetAwaiter().GetResult();
                if (process.ExitCode != 0 || !actual.Contains("rtsp_transport=tcp") || actual.Contains("fflags=+nobuffer") != uid)
                    throw new Exception("Effective player options lost TCP, UID no-buffer setting or non-UID isolation.");
            }
        }
        finally { File.Delete(script); }
        Console.WriteLine("Actual mpv: UID retains no-buffer flag and TCP; HA/direct options unchanged.");
    }
}
