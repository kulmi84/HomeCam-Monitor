using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Win32;

[assembly: AssemblyTitle("HomeCamMonitor Beta Setup")]
[assembly: AssemblyProduct("HomeCamMonitor Beta")]
[assembly: AssemblyVersion("0.5.0.20")]
[assembly: AssemblyFileVersion("0.5.0.20")]

internal static class BetaInstaller
{
    internal const string Executable = "HomeCamMonitor-Beta.exe";
    internal const string ApplicationVersion = "0.5.0-beta.20";
    private const string RegistryPath = @"Software\HomeCamMonitor-Beta\Setup";

    [STAThread]
    private static int Main(string[] args)
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        try
        {
            if (args.Length == 2 && args[0] == "--uninstall-ui") return BetaUninstaller.Run(args);
            if (args.Length == 2 && args[0] == "--remove-files") { BetaUninstaller.RemoveFiles(args[1]); return 0; }
            if (args.Length == 1 && args[0] == "--verify-setup") { Verify(); return 0; }
            if (args.Length == 2 && args[0] == "--install-files") { Install(args[1]); return 0; }
            Application.Run(new SetupForm());
            return 0;
        }
        catch (Exception exception)
        {
            if (args.Length > 0)
            {
                if (args[0] == "--install-files") MessageBox.Show(exception.Message, "HomeCamMonitor Beta Setup", MessageBoxButtons.OK, MessageBoxIcon.Error);
                else Console.Error.WriteLine(exception);
                return 1;
            }
            MessageBox.Show(exception.Message, "HomeCamMonitor Beta Setup", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return 1;
        }
    }

    internal static string DefaultDirectory()
    {
        using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RegistryPath))
        {
            string previous = key == null ? null : key.GetValue("InstallDirectory") as string;
            if (!String.IsNullOrWhiteSpace(previous) && File.Exists(Path.Combine(previous, Executable))) return previous;
        }
        return FreshInstallDirectory();
    }

    internal static string FreshInstallDirectory()
    {
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "HomeCamMonitor");
    }

    internal static string ExistingInstallation(string selected)
    {
        string saved;
        using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RegistryPath))
            saved = key == null ? null : key.GetValue("InstallDirectory") as string;
        string registered;
        using (RegistryKey key = Registry.CurrentUser.OpenSubKey(BetaUninstaller.RegistryPath))
            registered = key == null ? null : key.GetValue("InstallLocation") as string;
        return FindInstallation(new[] { selected, saved, registered, FreshInstallDirectory() });
    }

    internal static string FindInstallation(IEnumerable<string> candidates)
    {
        foreach (string candidate in candidates)
        {
            if (String.IsNullOrWhiteSpace(candidate)) continue;
            try
            {
                string path = NormalizeDirectory(candidate);
                if (File.Exists(Path.Combine(path, Executable))) return path;
            }
            catch (ArgumentException) { } catch (NotSupportedException) { } catch (PathTooLongException) { }
        }
        return null;
    }

    internal static void StartUninstall(string directory)
    {
        string copy = Path.Combine(Path.GetTempPath(), "HomeCam-Setup-Uninstall-" + Guid.NewGuid().ToString("N") + ".exe");
        File.Copy(Application.ExecutablePath, copy);
        Process.Start(new ProcessStartInfo(copy, "--uninstall-ui \"" + directory.TrimEnd('\\') + "\"") { UseShellExecute = true });
    }

    internal static string NormalizeDirectory(string directory)
    {
        if (String.IsNullOrWhiteSpace(directory) || !Path.IsPathRooted(directory))
            throw new ArgumentException("Bitte einen vollständigen Installationspfad wählen.");
        string full = Path.GetFullPath(directory.Trim()).TrimEnd(Path.DirectorySeparatorChar);
        if (String.Equals(full, Path.GetPathRoot(full).TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Bitte einen Programmordner wählen, nicht das Laufwerksverzeichnis.");
        return full;
    }

    internal static void InstallWithElevation(string directory)
    {
        directory = NormalizeDirectory(directory);
        try
        {
            // Test write access before stopping or replacing the application.
            Directory.CreateDirectory(directory);
            string probe = Path.Combine(directory, ".homecam-write-" + Guid.NewGuid().ToString("N"));
            using (File.Create(probe)) { }
            File.Delete(probe);
            Install(directory);
            return;
        }
        catch (UnauthorizedAccessException) { }
        using (Process elevated = Process.Start(new ProcessStartInfo
        {
            FileName = Application.ExecutablePath,
            Arguments = "--install-files \"" + directory + "\"",
            UseShellExecute = true,
            Verb = "runas"
        }))
        {
            if (elevated == null) throw new IOException("Installation konnte nicht gestartet werden.");
            elevated.WaitForExit();
            if (elevated.ExitCode != 0) throw new IOException("Installation mit Administratorrechten fehlgeschlagen.");
        }
    }

    internal static void Install(string directory)
    {
        directory = NormalizeDirectory(directory);
        string executable = Path.Combine(directory, Executable);
        var installedFiles = new List<string> { "HomeCamMonitor-Beta/v1" };
        foreach (Process process in Process.GetProcessesByName("HomeCamMonitor-Beta"))
        {
            try
            {
                if (String.Equals(process.MainModule.FileName, executable, StringComparison.OrdinalIgnoreCase))
                {
                    process.Kill();
                    if (!process.WaitForExit(5000)) throw new IOException("HomeCam Monitor konnte nicht beendet werden.");
                }
            }
            catch (Win32Exception) { }
            catch (InvalidOperationException) { }
            finally { process.Dispose(); }
        }
        Directory.CreateDirectory(directory);
        string targetRoot = directory + Path.DirectorySeparatorChar;
        using (Stream payload = Assembly.GetExecutingAssembly().GetManifestResourceStream("HomeCamMonitor.Beta.zip"))
        {
            if (payload == null) throw new InvalidOperationException("Das eingebettete Beta-Paket fehlt.");
            using (ZipArchive archive = new ZipArchive(payload, ZipArchiveMode.Read))
                foreach (ZipArchiveEntry entry in archive.Entries)
                {
                    string destination = Path.GetFullPath(Path.Combine(targetRoot, entry.FullName.Replace('/', Path.DirectorySeparatorChar)));
                    if (!destination.StartsWith(targetRoot, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Ungültiger Paketpfad.");
                    if (String.IsNullOrEmpty(entry.Name)) { Directory.CreateDirectory(destination); continue; }
                    Directory.CreateDirectory(Path.GetDirectoryName(destination));
                    using (Stream source = entry.Open())
                    using (FileStream output = new FileStream(destination, FileMode.Create, FileAccess.Write, FileShare.None)) source.CopyTo(output);
                    installedFiles.Add(entry.FullName);
                }
        }
        if (!File.Exists(executable)) throw new IOException("Die Programmdatei fehlt im Paket.");
        File.WriteAllLines(Path.Combine(directory, BetaUninstaller.Manifest), installedFiles);
    }

    internal static void CreateShortcut(string shortcutPath, string target, string directory, string icon)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(shortcutPath));
        object shell = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell", true));
        object shortcut = null;
        try
        {
            shortcut = shell.GetType().InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, shell, new object[] { shortcutPath });
            Type type = shortcut.GetType();
            type.InvokeMember("TargetPath", BindingFlags.SetProperty, null, shortcut, new object[] { target });
            type.InvokeMember("WorkingDirectory", BindingFlags.SetProperty, null, shortcut, new object[] { directory });
            type.InvokeMember("IconLocation", BindingFlags.SetProperty, null, shortcut, new object[] { icon + ",0" });
            type.InvokeMember("Save", BindingFlags.InvokeMethod, null, shortcut, null);
        }
        finally
        {
            if (shortcut != null) Marshal.FinalReleaseComObject(shortcut);
            Marshal.FinalReleaseComObject(shell);
        }
    }

    internal static void FinishInstall(string directory, bool desktop, bool startMenuEntries)
    {
        string executable = Path.Combine(directory, Executable);
        string startMenu = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "HomeCamMonitor Beta");
        string uninstaller = Path.Combine(directory, "HomeCamMonitor-Beta-Uninstall.exe");
        ConfigureStartMenu(startMenu, directory, startMenuEntries);
        if (desktop) CreateShortcut(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "HomeCamMonitor Beta.lnk"), executable, directory, executable);
        using (RegistryKey key = Registry.CurrentUser.CreateSubKey(RegistryPath)) key.SetValue("InstallDirectory", directory);
        using (RegistryKey key = Registry.CurrentUser.CreateSubKey(BetaUninstaller.RegistryPath))
        {
            key.SetValue("DisplayName", "HomeCamMonitor Beta");
            key.SetValue("DisplayVersion", ApplicationVersion);
            key.SetValue("DisplayIcon", executable + ",0");
            key.SetValue("InstallLocation", directory);
            key.SetValue("UninstallString", "\"" + uninstaller + "\"");
            key.SetValue("NoModify", 1, RegistryValueKind.DWord);
            key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
        }
    }

    internal static void ConfigureStartMenu(string startMenu, string directory, bool enabled)
    {
        string executable = Path.Combine(directory, Executable);
        if (enabled)
        {
            CreateShortcut(Path.Combine(startMenu, "HomeCamMonitor Beta.lnk"), executable, directory, executable);
            CreateShortcut(Path.Combine(startMenu, "Installationsordner.lnk"), directory, directory, executable);
            CreateShortcut(Path.Combine(startMenu, "Deinstallieren.lnk"), Path.Combine(directory, "HomeCamMonitor-Beta-Uninstall.exe"), directory, executable);
        }
        else
        {
            foreach (string name in new[] { "HomeCamMonitor Beta.lnk", "Installationsordner.lnk", "Deinstallieren.lnk" })
            {
                string path = Path.Combine(startMenu, name); if (File.Exists(path)) File.Delete(path);
            }
            if (Directory.Exists(startMenu) && Directory.GetFileSystemEntries(startMenu).Length == 0) Directory.Delete(startMenu);
        }
    }

    private static void Verify()
    {
        if (FreshInstallDirectory() != Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "HomeCamMonitor"))
            throw new Exception("Fresh install directory is incorrect.");
        string temp = Path.Combine(Path.GetTempPath(), "HomeCam-Setup-Test-" + Guid.NewGuid().ToString("N"));
        try
        {
            string target = Path.Combine(temp, "Ordner mit Leerzeichen");
            Install(target);
            File.WriteAllText(Path.Combine(target, "user-file.txt"), "preserve");
            Install(target);
            if (File.ReadAllText(Path.Combine(target, "user-file.txt")) != "preserve") throw new Exception("Update removed user files.");
            if (FindInstallation(new[] { Path.Combine(temp, "missing"), target }) != target || FindInstallation(new[] { temp }) != null)
                throw new Exception("Existing installation detection failed.");
            using (Icon icon = Icon.ExtractAssociatedIcon(Path.Combine(target, Executable)))
                if (icon == null) throw new Exception("Program icon missing.");
            string link = Path.Combine(temp, "Startmenu", "HomeCamMonitor Beta.lnk");
            CreateShortcut(link, Path.Combine(target, Executable), target, Path.Combine(target, Executable));
            object shell = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell", true));
            object shortcut = shell.GetType().InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, shell, new object[] { link });
            try
            {
                string actual = (string)shortcut.GetType().InvokeMember("TargetPath", BindingFlags.GetProperty, null, shortcut, null);
                if (!String.Equals(actual, Path.Combine(target, Executable), StringComparison.OrdinalIgnoreCase)) throw new Exception("Shortcut target incorrect.");
            }
            finally { Marshal.FinalReleaseComObject(shortcut); Marshal.FinalReleaseComObject(shell); }
            string testMenu = Path.Combine(temp, "Optional menu");
            ConfigureStartMenu(testMenu, target, true);
            if (Directory.GetFiles(testMenu, "*.lnk").Length != 3) throw new Exception("Start menu entries are missing.");
            File.WriteAllText(Path.Combine(testMenu, "user-file.txt"), "preserve");
            ConfigureStartMenu(testMenu, target, false);
            if (Directory.GetFiles(testMenu, "*.lnk").Length != 0 || !File.Exists(Path.Combine(testMenu, "user-file.txt")))
                throw new Exception("Disabling start menu entries affected unrelated files.");
            using (SetupForm form = new SetupForm())
            {
                form.Show(); Application.DoEvents();
                using (Bitmap bitmap = new Bitmap(form.Width, form.Height))
                {
                    form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
                    bitmap.Save(Path.Combine(Path.GetTempPath(), "homecam-setup.png"));
                }
                form.Close();
            }
            using (SetupForm form = new SetupForm(target))
            {
                form.Show(); Application.DoEvents();
                if (!form.Controls.Find("Uninstall", true)[0].Enabled ||
                    ((CheckBox)form.Controls.Find("ResetSettings", true)[0]).Checked ||
                    !((CheckBox)form.Controls.Find("StartMenuEntries", true)[0]).Checked)
                    throw new Exception("Installed setup options have incorrect defaults.");
                using (Bitmap bitmap = new Bitmap(form.Width, form.Height))
                {
                    form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
                    bitmap.Save(Path.Combine(Path.GetTempPath(), "homecam-setup-existing.png"));
                }
                form.Close();
            }
            string resetFolder = Path.Combine(temp, "Settings reset"); Directory.CreateDirectory(resetFolder);
            foreach (string name in new[] { "settings.json", "motion-recordings.json", "keep-video.mkv", "snapshot.png" })
                File.WriteAllText(Path.Combine(resetFolder, name), "preserve");
            BetaUninstaller.DeleteSettingsFiles(resetFolder, false);
            if (File.Exists(Path.Combine(resetFolder, "settings.json")) || Directory.GetFiles(resetFolder).Length != 3)
                throw new Exception("Settings reset removed recording files or the retention ledger.");
            string testRegistry = @"Software\HomeCamMonitor-Uninstall-Test-" + Guid.NewGuid().ToString("N");
            try
            {
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(testRegistry + @"\Setup")) key.SetValue("InstallDirectory", target);
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(testRegistry + @"\Uninstall")) key.SetValue("InstallLocation", target);
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(testRegistry)) key.SetValue("SettingsFolder", resetFolder);
                BetaUninstaller.RemoveInstallRegistration(testRegistry + @"\Setup", testRegistry + @"\Uninstall");
                using (RegistryKey setup = Registry.CurrentUser.OpenSubKey(testRegistry + @"\Setup"))
                using (RegistryKey uninstall = Registry.CurrentUser.OpenSubKey(testRegistry + @"\Uninstall"))
                using (RegistryKey settings = Registry.CurrentUser.OpenSubKey(testRegistry))
                    if (setup != null || uninstall != null || (string)settings.GetValue("SettingsFolder") != resetFolder)
                        throw new Exception("Uninstall left its install path behind or removed retained settings.");
            }
            finally { Registry.CurrentUser.DeleteSubKeyTree(testRegistry, false); }
            File.WriteAllText(Path.Combine(target, "keep-video.mkv"), "user video");
            File.WriteAllText(Path.Combine(target, "settings.json"), "user settings");
            BetaUninstaller.RemoveFiles(target);
            if (File.Exists(Path.Combine(target, Executable))) throw new Exception("Uninstall left executable behind.");
            if (!File.Exists(Path.Combine(target, "keep-video.mkv")) || !File.Exists(Path.Combine(target, "settings.json")) || !File.Exists(Path.Combine(target, "user-file.txt")))
                throw new Exception("Uninstall removed user files.");
        }
        finally { if (Directory.Exists(temp)) Directory.Delete(temp, true); }
    }
}

internal sealed class SetupForm : Form
{
    private readonly TextBox folder = new TextBox { Dock = DockStyle.Fill };
    private readonly Button install = new Button { Text = "Installieren", AutoSize = true };
    private readonly Button browse = new Button { Text = "Durchsuchen …", AutoSize = true };
    private readonly CheckBox desktop = new CheckBox { Text = "Desktop-Verknüpfung erstellen", AutoSize = true, Checked = true };
    private readonly CheckBox launch = new CheckBox { Text = "HomeCam Monitor nach der Installation starten", AutoSize = true, Checked = true };
    private readonly CheckBox startMenu = new CheckBox { Name = "StartMenuEntries", Text = "Startmenü-Einträge erstellen", AutoSize = true, Checked = true };
    private readonly Label status = new Label { Text = "", AutoSize = true };
    private readonly Label existing = new Label { AutoSize = true, MaximumSize = new Size(600, 0) };
    private readonly CheckBox reset = new CheckBox { Name = "ResetSettings", Text = "Einstellungen zurücksetzen (Kameras und Zugangsdaten löschen)", AutoSize = true, Checked = false };
    private readonly Button uninstall = new Button { Name = "Uninstall", Text = "Deinstallieren …", AutoSize = true };
    private string existingDirectory;
    private bool busy;

    internal SetupForm(string initialDirectory = null)
    {
        Text = "HomeCamMonitor Beta – Setup";
        Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(690, 390); MinimumSize = new Size(706, 429);
        MaximizeBox = false;
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(20), ColumnCount = 2, RowCount = 12 };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        for (int row = 0; row < 11; row++) layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var title = new Label { Text = "HomeCamMonitor Beta installieren", Font = new Font("Segoe UI", 14), AutoSize = true };
        layout.Controls.Add(title, 0, 0); layout.SetColumnSpan(title, 2);
        layout.Controls.Add(new Label { Text = "Installationsverzeichnis", AutoSize = true, Margin = new Padding(3, 14, 3, 6) }, 0, 1);
        folder.Text = initialDirectory ?? BetaInstaller.DefaultDirectory();
        layout.Controls.Add(folder, 0, 2); layout.Controls.Add(browse, 1, 2);
        layout.Controls.Add(desktop, 0, 3); layout.SetColumnSpan(desktop, 2);
        layout.Controls.Add(launch, 0, 4); layout.SetColumnSpan(launch, 2);
        layout.Controls.Add(startMenu, 0, 5); layout.SetColumnSpan(startMenu, 2);
        layout.Controls.Add(existing, 0, 6); layout.SetColumnSpan(existing, 2);
        layout.Controls.Add(reset, 0, 7); layout.SetColumnSpan(reset, 2);
        layout.Controls.Add(new Label { Text = "Snapshots und Videoaufnahmen bleiben erhalten.", AutoSize = true }, 0, 8); layout.SetColumnSpan(layout.GetControlFromPosition(0, 8), 2);
        layout.Controls.Add(uninstall, 0, 9); layout.Controls.Add(install, 1, 9);
        layout.Controls.Add(status, 0, 10); layout.SetColumnSpan(status, 2);
        var version = new Label { Name = "SetupVersion", Text = "Version " + BetaInstaller.ApplicationVersion,
            AutoSize = true, ForeColor = SystemColors.GrayText, Anchor = AnchorStyles.Left | AnchorStyles.Bottom };
        layout.Controls.Add(version, 0, 11); layout.SetColumnSpan(version, 2);
        Controls.Add(layout); AcceptButton = install;
        folder.TextChanged += delegate { RefreshExisting(); };
        RefreshExisting();
        uninstall.Click += delegate
        {
            try { BetaInstaller.StartUninstall(existingDirectory); Close(); }
            catch (Exception error) { MessageBox.Show(this, error.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error); }
        };
        browse.Click += delegate
        {
            using (var dialog = new FolderBrowserDialog { Description = "Installationsverzeichnis auswählen", SelectedPath = folder.Text })
                if (dialog.ShowDialog(this) == DialogResult.OK) folder.Text = dialog.SelectedPath;
        };
        install.Click += delegate
        {
            string target;
            try { target = BetaInstaller.NormalizeDirectory(folder.Text); }
            catch (Exception error) { MessageBox.Show(this, error.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
            bool makeDesktop = desktop.Checked;
            bool makeStartMenu = startMenu.Checked;
            bool resetSettings = reset.Checked;
            if (resetSettings && MessageBox.Show(this, "Alle Beta-Einstellungen einschließlich Kameras und Zugangsdaten zurücksetzen?\nSnapshots und Videoaufnahmen bleiben erhalten.", Text, MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;
            string previous = existingDirectory;
            busy = true; install.Enabled = browse.Enabled = folder.Enabled = desktop.Enabled = launch.Enabled = startMenu.Enabled = reset.Enabled = uninstall.Enabled = false;
            status.Text = "Installation läuft …";
            var worker = new BackgroundWorker();
            worker.DoWork += delegate
            {
                if (resetSettings && previous != null) BetaUninstaller.StopApp(previous);
                BetaInstaller.InstallWithElevation(target);
                BetaInstaller.FinishInstall(target, makeDesktop, makeStartMenu);
                if (resetSettings)
                {
                    BetaUninstaller.DeleteSettings(false);
                    using (RegistryKey key = Registry.CurrentUser.CreateSubKey(@"Software\HomeCamMonitor-Beta"))
                        key.SetValue("ResetSettings", 1, RegistryValueKind.DWord);
                }
            };
            worker.RunWorkerCompleted += delegate(object sender, RunWorkerCompletedEventArgs result)
            {
                busy = false; worker.Dispose();
                if (result.Error != null)
                {
                    status.Text = "Installation fehlgeschlagen.";
                    install.Enabled = browse.Enabled = folder.Enabled = desktop.Enabled = launch.Enabled = startMenu.Enabled = true;
                    RefreshExisting();
                    MessageBox.Show(this, "Installation nach " + target + " fehlgeschlagen.\n\n" + result.Error.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
                if (launch.Checked) Process.Start(new ProcessStartInfo { FileName = Path.Combine(target, BetaInstaller.Executable), WorkingDirectory = target, UseShellExecute = true });
                MessageBox.Show(this, makeStartMenu ? "HomeCamMonitor Beta wurde installiert. Die Einträge befinden sich im Startmenü unter HomeCamMonitor Beta." : "HomeCamMonitor Beta wurde installiert.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
                Close();
            };
            worker.RunWorkerAsync();
        };
        FormClosing += delegate(object sender, FormClosingEventArgs e) { if (busy) e.Cancel = true; };
    }
    private void RefreshExisting()
    {
        existingDirectory = BetaInstaller.ExistingInstallation(folder.Text);
        existing.Text = existingDirectory == null ? "Keine bestehende Installation gefunden." : "Vorhandene Installation: " + existingDirectory;
        uninstall.Enabled = existingDirectory != null && File.Exists(Path.Combine(existingDirectory, BetaUninstaller.Manifest));
        reset.Enabled = existingDirectory != null || BetaUninstaller.HasSettings();
        if (!reset.Enabled) reset.Checked = false;
        install.Text = existingDirectory == null ? "Installieren" : "Installieren / Aktualisieren";
        if (existingDirectory != null && !uninstall.Enabled) existing.Text += "\nFür die Deinstallation diese ältere Beta zuerst aktualisieren.";
    }
}
