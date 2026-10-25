param([string]$Dotnet = 'dotnet')
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
[xml]$project = Get-Content -LiteralPath (Join-Path $root 'HomeCamMonitor.V2.Beta.csproj') -Raw
$betaProperties = $project.Project.PropertyGroup | Where-Object AssemblyName -eq 'HomeCamMonitor-V2-NextBeta'
$version = [string]$betaProperties.Version
$fileVersion = [string]$betaProperties.FileVersion
if ($version -notmatch '^2\.\d+\.\d+-beta\.\d+$' -or $fileVersion -notmatch '^2\.\d+\.\d+\.\d+$') { throw 'Explicit NextBeta versions required.' }
$baseVersion = & (Join-Path $PSScriptRoot 'get-v2-version.ps1')
$output = Join-Path (Split-Path $root) "Beta/$version"
$payload = Join-Path $output 'payload'
$generated = Join-Path $output 'installer-source'
$template = Join-Path (Split-Path $root) "Releases/$baseVersion/HomeCamMonitor-V2-$baseVersion-win-x64.zip"
if (-not (Test-Path -LiteralPath $template)) { throw "Stable runtime template missing: $template" }
if (Test-Path $output) { throw "Output already exists: $output. Preserve it or choose a new beta version." }
New-Item -ItemType Directory -Path $payload, $generated -Force | Out-Null
Expand-Archive -LiteralPath $template -DestinationPath $payload
# Remove only predecessor app binaries from this newly created beta payload.
Get-ChildItem -LiteralPath $payload -File | Where-Object { $_.Name -like 'HomeCamMonitor-V2-Beta*' -or $_.Name -eq 'HomeCamMonitor.StoreBuild.json' -or $_.Name -eq '.homecam-v2-installed-files' } | Remove-Item
& $Dotnet publish (Join-Path $root 'HomeCamMonitor.V2.Beta.csproj') -c Release -r win-x64 --self-contained true -p:NextBeta=true -p:NuGetAudit=false --ignore-failed-sources -o $payload
if ($LASTEXITCODE -ne 0) { throw 'Next Beta publish failed.' }
foreach ($name in @('V2BetaInstaller.cs', 'V2BetaUninstaller.cs')) {
    $text = [IO.File]::ReadAllText((Join-Path $PSScriptRoot $name))
    $text = $text.Replace('HomeCamMonitor-V2-Beta', 'HomeCamMonitor-V2-NextBeta').Replace('HomeCam Monitor V2 Beta', 'HomeCam Monitor V2 Next Beta')
    $text = $text.Replace('.homecam-v2-installed-files', '.homecam-nextbeta-installed-files')
    $text = $text.Replace('2.0.0-beta.37', $version).Replace('"2.0.0.37"', '"' + $fileVersion + '"')
    $text = $text.Replace('HomeCam-Setup-Test-', 'HCM-').Replace('Ordner mit Leerzeichen', 'Test Ordner')
    if ($name -eq 'V2BetaInstaller.cs') {
        $guard = 'File.Exists(Path.Combine(full, "HomeCamMonitor-Beta.exe"))'
        $text = $text.Replace($guard, $guard + ' || File.Exists(Path.Combine(full, "HomeCamMonitor-V2-Beta.exe")) || File.Exists(Path.Combine(full, "AppxManifest.xml"))')
    }
    [IO.File]::WriteAllText((Join-Path $generated $name), $text, [Text.UTF8Encoding]::new($false))
}
$compiler = Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
$icon = Join-Path $root 'assets/HomeCamMonitor-V2-Beta.ico'
$installerSource = Join-Path $generated 'V2BetaInstaller.cs'
$uninstallerSource = Join-Path $generated 'V2BetaUninstaller.cs'
$uninstaller = Join-Path $payload 'HomeCamMonitor-V2-NextBeta-Uninstall.exe'
& $compiler /nologo /target:winexe /platform:x64 /optimize+ /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Core.dll "/win32icon:$icon" "/out:$uninstaller" $uninstallerSource
if ($LASTEXITCODE -ne 0) { throw 'Beta uninstaller build failed.' }
@"
HomeCam Monitor V2 Next Beta $version

Separate Installation neben der Store-Version. Eigene Einstellungen und
Standard-Aufnahmeordner; Kameras werden in der Beta neu eingerichtet.
Die Store-Version bleibt erhalten. Keine stabile Konfiguration importieren
und fuer den Test eigene Aufnahmeziele verwenden.

Entwicklungsgrundlage: HomeCam Monitor V2 $baseVersion.
Diese erste Beta uebernimmt den Funktionsumfang der stabilen Grundlage.
"@ | Set-Content -LiteralPath (Join-Path $payload 'README-NextBeta.txt') -Encoding utf8
$zip = Join-Path $output "HomeCamMonitor-V2-NextBeta-$version-win-x64.zip"
Compress-Archive -Path (Join-Path $payload '*') -DestinationPath $zip -CompressionLevel Optimal
$setup = Join-Path $output "HomeCamMonitor-V2-NextBeta-$version-Setup.exe"
& $compiler /nologo /target:winexe /platform:x64 /optimize+ /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Core.dll /reference:System.IO.Compression.dll /reference:System.IO.Compression.FileSystem.dll "/win32icon:$icon" "/resource:$zip,HomeCamMonitor.V2.Beta.zip" "/out:$setup" $installerSource $uninstallerSource /main:V2BetaInstaller
if ($LASTEXITCODE -ne 0) { throw 'Beta installer build failed.' }
Get-FileHash -LiteralPath $setup, $zip -Algorithm SHA256 | ForEach-Object { $_.Hash.ToLowerInvariant() + '  ' + [IO.Path]::GetFileName($_.Path) } | Set-Content -LiteralPath (Join-Path $output 'SHA256SUMS.txt') -Encoding ascii
Write-Output "Created $setup"
