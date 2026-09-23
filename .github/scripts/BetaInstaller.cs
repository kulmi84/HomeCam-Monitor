using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Windows.Forms;

[assembly: AssemblyTitle("HomeCam Monitor Beta Setup")]
[assembly: AssemblyProduct("HomeCam Monitor Beta")]
[assembly: AssemblyVersion("0.2.0.11")]
[assembly: AssemblyFileVersion("0.2.0.11")]

internal static class BetaInstaller
{
    private const string TargetDirectory = @"C:\github_mk\HomeCamMonitor-Beta";

    [STAThread]
    private static void Main()
    {
        try
        {
            foreach (Process process in Process.GetProcessesByName("HomeCamMonitor-Beta"))
            {
                try
                {
                    process.Kill();
                    process.WaitForExit(5000);
                }
                catch { }
                finally { process.Dispose(); }
            }

            Directory.CreateDirectory(TargetDirectory);
            string targetRoot = Path.GetFullPath(TargetDirectory).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            Assembly assembly = Assembly.GetExecutingAssembly();
            using (Stream payload = assembly.GetManifestResourceStream("HomeCamMonitor.Beta.zip"))
            {
                if (payload == null) throw new InvalidOperationException("Das eingebettete Beta-Paket fehlt.");
                using (ZipArchive archive = new ZipArchive(payload, ZipArchiveMode.Read))
                {
                    foreach (ZipArchiveEntry entry in archive.Entries)
                    {
                        string relativePath = entry.FullName.Replace('/', Path.DirectorySeparatorChar);
                        string destination = Path.GetFullPath(Path.Combine(targetRoot, relativePath));
                        if (!destination.StartsWith(targetRoot, StringComparison.OrdinalIgnoreCase))
                            throw new InvalidDataException("Ungültiger Pfad im Beta-Paket.");

                        if (String.IsNullOrEmpty(entry.Name))
                        {
                            Directory.CreateDirectory(destination);
                            continue;
                        }

                        string parent = Path.GetDirectoryName(destination);
                        if (!String.IsNullOrEmpty(parent)) Directory.CreateDirectory(parent);
                        using (Stream source = entry.Open())
                        using (FileStream output = new FileStream(destination, FileMode.Create, FileAccess.Write, FileShare.None))
                            source.CopyTo(output);
                    }
                }
            }

            Process.Start(new ProcessStartInfo
            {
                FileName = Path.Combine(TargetDirectory, "HomeCamMonitor-Beta.exe"),
                WorkingDirectory = TargetDirectory,
                UseShellExecute = true
            });
        }
        catch (Exception exception)
        {
            MessageBox.Show(
                "Die Beta konnte nicht nach " + TargetDirectory + " entpackt werden.\n\n" + exception.Message,
                "HomeCam Monitor Beta",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }
}
