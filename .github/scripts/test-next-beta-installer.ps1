param([Parameter(Mandatory=$true)][string]$BetaDirectory)
$ErrorActionPreference = 'Stop'
$beta = (Resolve-Path -LiteralPath $BetaDirectory).Path
$version = Split-Path $beta -Leaf
$setup = Join-Path $beta "HomeCamMonitor-V2-NextBeta-$version-Setup.exe"
$source = Join-Path $beta 'installer-source'
$testSource = Join-Path $source 'NextBetaGuardTest.cs'
@'
using System;
using System.IO;
internal static class NextBetaGuardTest {
    public static void Main() {
        string root = Path.Combine(Path.GetTempPath(), "HomeCam-NextBeta-Guard-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try {
            foreach (string name in new[] { "HomeCamMonitor.exe", "HomeCamMonitor-Beta.exe", "HomeCamMonitor-V2-Beta.exe", "AppxManifest.xml" }) {
                string guard = Path.Combine(root, name);
                File.WriteAllText(guard, "preserve");
                bool rejected = false;
                try { V2BetaInstaller.NormalizeDirectory(root); } catch (IOException) { rejected = true; }
                if (!rejected || File.ReadAllText(guard) != "preserve") throw new Exception("Protected installation accepted: " + name);
                File.Delete(guard);
            }
            File.WriteAllText(Path.Combine(root, "stable.txt"), "preserve");
            string manifest = Path.Combine(root, V2BetaUninstaller.Manifest);
            File.WriteAllLines(manifest, new[] { "HomeCamMonitor-V2-Beta/v1", "stable.txt" });
            bool blocked = false;
            try { V2BetaUninstaller.RemoveFiles(root); } catch (IOException) { blocked = true; }
            if (!blocked || File.ReadAllText(Path.Combine(root, "stable.txt")) != "preserve") throw new Exception("Stable manifest was accepted.");
            if (V2BetaUninstaller.RegistryPath != @"Software\Microsoft\Windows\CurrentVersion\Uninstall\HomeCamMonitor-V2-NextBeta") throw new Exception("Shared uninstall registry key.");
            Console.WriteLine("PASS: stable/V1/Store directories protected; stable manifest rejected; independent uninstall registration.");
        } finally {
            // Only the uniquely named directory created above is removed.
            Directory.Delete(root, true);
        }
    }
}
'@ | Set-Content -LiteralPath $testSource -Encoding utf8
$compiler = Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
$testExe = Join-Path $source 'NextBetaGuardTest.exe'
& $compiler /nologo /target:exe /main:NextBetaGuardTest /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Core.dll /reference:System.IO.Compression.dll /reference:System.IO.Compression.FileSystem.dll "/out:$testExe" $testSource (Join-Path $source 'V2BetaInstaller.cs') (Join-Path $source 'V2BetaUninstaller.cs')
if ($LASTEXITCODE -ne 0) { throw 'Guard test compilation failed.' }
& $testExe
if ($LASTEXITCODE -ne 0) { throw 'Stable installation protection failed.' }
$shortTestTemp = (Resolve-Path (Join-Path $PSScriptRoot '../../..')).Path
$result = Start-Process -FilePath $setup -ArgumentList '--verify-setup' -WindowStyle Hidden -PassThru -Wait -Environment @{ TEMP = $shortTestTemp; TMP = $shortTestTemp } -RedirectStandardError (Join-Path $beta 'installer-test-error.txt')
if ($result.ExitCode -ne 0) { throw "Installer verification failed: $($result.ExitCode)" }
'PASS: embedded payload installation, update, shortcuts, settings preservation and uninstall self-test.'
