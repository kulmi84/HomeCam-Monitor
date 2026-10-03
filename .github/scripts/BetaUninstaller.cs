using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Microsoft.Win32;

internal static class BetaUninstaller
{
    internal const string RegistryPath = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\HomeCamMonitor-Beta";
    internal const string Manifest = ".homecam-installed-files";
    [STAThread]
    private static int Main(string[] args)
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        try
        {
            if (args.Length == 2 && args[0] == "--remove-files") { RemoveFiles(args[1]); return 0; }
            string target = args.Length == 2 && args[0] == "--uninstall-ui" ? args[1] : AppDomain.CurrentDomain.BaseDirectory;
            if (args.Length == 0)
            {
                // Run from a temporary copy so the installed executable is not locked.
                string copy = Path.Combine(Path.GetTempPath(), "HomeCam-Uninstall-" + Guid.NewGuid().ToString("N") + ".exe");
                File.Copy(Application.ExecutablePath, copy);
                Process.Start(new ProcessStartInfo(copy, "--uninstall-ui \"" + target.TrimEnd('\\') + "\"") { UseShellExecute = true });
                return 0;
            }
            using (Form form = new Form { Text = "HomeCamMonitor Beta deinstallieren", ClientSize = new Size(560, 190), StartPosition = FormStartPosition.CenterScreen, FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false, MinimizeBox = false, Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath) })
            {
                var label = new Label { Text = "HomeCamMonitor Beta entfernen?\nSnapshots und Videoaufnahmen bleiben immer erhalten.", AutoSize = true, Location = new Point(20, 20) };
                var removeSettings = new CheckBox { Text = "Einstellungen ebenfalls löschen", AutoSize = true, Location = new Point(20, 80), Checked = false };
                var remove = new Button { Text = "Deinstallieren", AutoSize = true, Location = new Point(315, 130) };
                var cancel = new Button { Text = "Abbrechen", AutoSize = true, Location = new Point(440, 130) };
                cancel.Click += delegate { form.Close(); };
                remove.Click += delegate
                {
                    remove.Enabled = false;
                    try
                    {
                        StopApp(target);
                        try { RemoveFiles(target); }
                        catch (UnauthorizedAccessException)
                        {
                            using (Process process = Process.Start(new ProcessStartInfo(Application.ExecutablePath, "--remove-files \"" + target.TrimEnd('\\') + "\"") { UseShellExecute = true, Verb = "runas" }))
                            {
                                process.WaitForExit();
                                if (process.ExitCode != 0) throw new IOException("Programmdateien konnten nicht entfernt werden.");
                            }
                        }
                        Finish(removeSettings.Checked);
                        MessageBox.Show(form, "HomeCamMonitor Beta wurde entfernt.\nAufnahmen bleiben erhalten.", form.Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
                        form.Close();
                    }
                    catch (Exception error) { remove.Enabled = true; MessageBox.Show(form, error.Message, form.Text, MessageBoxButtons.OK, MessageBoxIcon.Error); }
                };
                form.Controls.AddRange(new Control[] { label, removeSettings, remove, cancel });
                Application.Run(form);
            }
            return 0;
        }
        catch (Exception error) { MessageBox.Show(error.Message, "HomeCamMonitor Beta", MessageBoxButtons.OK, MessageBoxIcon.Error); return 1; }
    }
    private static void StopApp(string directory)
    {
        string executable = Path.GetFullPath(Path.Combine(directory, "HomeCamMonitor-Beta.exe"));
        foreach (Process process in Process.GetProcessesByName("HomeCamMonitor-Beta"))
        using (process)
        {
            if (!String.Equals(process.MainModule.FileName, executable, StringComparison.OrdinalIgnoreCase)) continue;
            process.CloseMainWindow();
            if (!process.WaitForExit(5000)) throw new IOException("Bitte HomeCamMonitor Beta vor der Deinstallation schließen.");
        }
    }
    internal static void RemoveFiles(string directory)
    {
        string root = Path.GetFullPath(directory).TrimEnd('\\') + "\\";
        if (root.Equals(Path.GetPathRoot(root), StringComparison.OrdinalIgnoreCase) ||
            Directory.Exists(root) && (File.GetAttributes(root) & FileAttributes.ReparsePoint) != 0)
            throw new IOException("Dieser Installationsordner kann nicht sicher deinstalliert werden.");
        string manifest = Path.Combine(root, Manifest);
        if (!File.Exists(manifest)) throw new IOException("Installationsliste fehlt. Es werden keine Dateien gelöscht.");
        string[] entries = File.ReadAllLines(manifest);
        if (entries.Length == 0 || entries[0] != "HomeCamMonitor-Beta/v1") throw new IOException("Ungültige Installationsliste.");
        string[] files = entries.Skip(1).Select(relative =>
        {
            if (Path.IsPathRooted(relative) || relative.Split('\\', '/').Contains("..")) throw new IOException("Ungültiger Dateipfad.");
            string full = Path.GetFullPath(Path.Combine(root, relative));
            if (!full.StartsWith(root, StringComparison.OrdinalIgnoreCase)) throw new IOException("Ungültiger Dateipfad.");
            for (string parent = Path.GetDirectoryName(full); parent != null && parent.StartsWith(root, StringComparison.OrdinalIgnoreCase); parent = Path.GetDirectoryName(parent))
                if (Directory.Exists(parent) && (File.GetAttributes(parent) & FileAttributes.ReparsePoint) != 0) throw new IOException("Verknüpfter Programmordner wird nicht gelöscht.");
            return full;
        }).ToArray();
        foreach (string file in files)
        {
            // Never remove user recordings, even if a modified manifest lists them.
            if (new[] { ".png", ".jpg", ".jpeg", ".mkv", ".mp4", ".avi" }.Contains(Path.GetExtension(file).ToLowerInvariant()) || Path.GetFileName(file).Equals("settings.json", StringComparison.OrdinalIgnoreCase)) continue;
            if (File.Exists(file)) File.Delete(file);
        }
        File.Delete(manifest);
        foreach (string parent in files.Select(Path.GetDirectoryName).Distinct().OrderByDescending(p => p.Length))
            if (Directory.Exists(parent) && !Directory.EnumerateFileSystemEntries(parent).Any()) Directory.Delete(parent);
        if (Directory.Exists(root) && !Directory.EnumerateFileSystemEntries(root).Any()) Directory.Delete(root);
    }
    private static void Finish(bool deleteSettings)
    {
        string menu = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "HomeCamMonitor Beta");
        foreach (string name in new[] { "HomeCamMonitor Beta.lnk", "Installationsordner.lnk", "Deinstallieren.lnk" })
        {
            string path = Path.Combine(menu, name); if (File.Exists(path)) File.Delete(path);
        }
        if (Directory.Exists(menu) && !Directory.EnumerateFileSystemEntries(menu).Any()) Directory.Delete(menu);
        string desktop = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "HomeCamMonitor Beta.lnk");
        if (File.Exists(desktop)) File.Delete(desktop);
        Registry.CurrentUser.DeleteSubKeyTree(RegistryPath, false);
        Registry.CurrentUser.DeleteSubKeyTree(@"Software\HomeCamMonitor-Beta\Setup", false);
        using (RegistryKey run = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true))
            if (run != null) run.DeleteValue("HomeCamMonitor-Beta", false);
        if (deleteSettings)
        {
            using (RegistryKey location = Registry.CurrentUser.OpenSubKey(@"Software\HomeCamMonitor-Beta", true))
            {
                string customFolder = location == null ? null : location.GetValue("SettingsFolder") as string;
                if (!String.IsNullOrWhiteSpace(customFolder) && Path.IsPathRooted(customFolder))
                {
                    string customSettings = Path.Combine(Path.GetFullPath(customFolder), "settings.json");
                    if (File.Exists(customSettings)) File.Delete(customSettings);
                }
                if (location != null) location.DeleteValue("SettingsFolder", false);
            }
            string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "HomeCamMonitor-Beta");
            foreach (string name in new[] { "settings.json", "window-diagnostics.log", "motion-recordings.json", "motion-recordings.json.tmp" })
            { string path = Path.Combine(folder, name); if (File.Exists(path)) File.Delete(path); }
        }
    }
}
