param(
    [Parameter(Mandatory = $true)]
    [string]$Version
)

$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$publish = Join-Path $root 'publish-beta'
$downloads = Join-Path $root 'beta-downloads'
$zipPath = Join-Path $env:RUNNER_TEMP "HomeCamMonitor-Beta-v$Version-win-x64.zip"
$setupPath = Join-Path $downloads 'HomeCamMonitor-Beta-Setup.exe'

if (-not (Test-Path $publish)) { throw "Beta-Ausgabe fehlt: $publish" }
New-Item -ItemType Directory -Force -Path $downloads | Out-Null
Compress-Archive -Path (Join-Path $publish '*') -DestinationPath $zipPath -CompressionLevel Optimal -Force

$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$installerSource = Join-Path $PSScriptRoot 'BetaInstaller.cs'
$icon = Join-Path $root 'assets\HomeCamMonitor-Beta.ico'
if (-not (Test-Path $compiler)) { throw "C#-Compiler fehlt: $compiler" }
& $compiler /nologo /target:winexe /platform:x64 /optimize+ /reference:System.Windows.Forms.dll `
    /reference:System.IO.Compression.dll /reference:System.IO.Compression.FileSystem.dll `
    "/win32icon:$icon" "/resource:$zipPath,HomeCamMonitor.Beta.zip" "/out:$setupPath" $installerSource
if ($LASTEXITCODE -ne 0 -or -not (Test-Path $setupPath)) { throw 'Die selbstextrahierende Beta konnte nicht erstellt werden.' }
Remove-Item $zipPath -Force
