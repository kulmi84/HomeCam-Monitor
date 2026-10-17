param(
    [Parameter(Mandatory = $true)]
    [string]$Version
)

$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$publish = Join-Path $root 'publish-v2-beta'
$downloads = Join-Path $root 'v2-beta-downloads'
$zipPath = Join-Path $env:RUNNER_TEMP "HomeCamMonitor-V2-Beta-v$Version-win-x64.zip"
$setupPath = Join-Path $downloads 'HomeCamMonitor-V2-Beta-Setup.exe'

if (-not (Test-Path $publish)) { throw "Beta-Ausgabe fehlt: $publish" }
New-Item -ItemType Directory -Force -Path $downloads | Out-Null
New-Item -ItemType Directory -Force -Path (Join-Path $publish 'licenses') | Out-Null
Copy-Item (Join-Path $root 'licenses/opencvsharp-4.13.0') (Join-Path $publish 'licenses') -Recurse -Force
Copy-Item (Join-Path $root 'THIRD-PARTY-NOTICES.md') $publish -Force
if (-not (Test-Path (Join-Path $publish 'OpenCvSharpExtern.dll'))) { throw 'OpenCV-Laufzeit fehlt im V2-Paket.' }
 $compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
 $icon = Join-Path $root 'assets\HomeCamMonitor-V2-Beta.ico'
 $uninstaller = Join-Path $publish 'HomeCamMonitor-V2-Beta-Uninstall.exe'
 & $compiler /nologo /target:winexe /platform:x64 /optimize+ /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Core.dll `
     "/win32icon:$icon" "/out:$uninstaller" (Join-Path $PSScriptRoot 'V2BetaUninstaller.cs')
 if ($LASTEXITCODE -ne 0) { throw 'Deinstallationsprogramm konnte nicht erstellt werden.' }
Compress-Archive -Path (Join-Path $publish '*') -DestinationPath $zipPath -CompressionLevel Optimal -Force

$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$installerSource = Join-Path $PSScriptRoot 'V2BetaInstaller.cs'
$icon = Join-Path $root 'assets\HomeCamMonitor-V2-Beta.ico'
if (-not (Test-Path $compiler)) { throw "C#-Compiler fehlt: $compiler" }
& $compiler /nologo /target:winexe /platform:x64 /optimize+ /reference:System.Windows.Forms.dll /reference:System.Drawing.dll `
    /reference:System.IO.Compression.dll /reference:System.IO.Compression.FileSystem.dll `
    /reference:System.Core.dll "/win32icon:$icon" "/resource:$zipPath,HomeCamMonitor.V2.Beta.zip" "/out:$setupPath" $installerSource (Join-Path $PSScriptRoot 'V2BetaUninstaller.cs') /main:V2BetaInstaller
if ($LASTEXITCODE -ne 0 -or -not (Test-Path $setupPath)) { throw 'Die selbstextrahierende Beta konnte nicht erstellt werden.' }
Remove-Item $zipPath -Force
