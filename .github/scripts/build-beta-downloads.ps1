param(
    [Parameter(Mandatory = $true)]
    [string]$Version
)

$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$publish = Join-Path $root 'publish-beta'
$downloads = Join-Path $root 'beta-downloads'
$zipName = "HomeCamMonitor-Beta-v$Version-win-x64.zip"
$zipPath = Join-Path $downloads $zipName
$setupPath = Join-Path $downloads 'HomeCamMonitor-Beta-Setup.exe'

if (-not (Test-Path $publish)) { throw "Beta-Ausgabe fehlt: $publish" }
New-Item -ItemType Directory -Force -Path $downloads | Out-Null
Compress-Archive -Path (Join-Path $publish '*') -DestinationPath $zipPath -CompressionLevel Optimal -Force

$installerFiles = Join-Path $downloads 'installer'
New-Item -ItemType Directory -Force -Path $installerFiles | Out-Null
Copy-Item $zipPath (Join-Path $installerFiles 'HomeCamMonitor-Beta.zip') -Force

$installScript = @'
$ErrorActionPreference = 'Stop'
$target = 'C:\github_mk\HomeCamMonitor-Beta'
$archive = Join-Path $PSScriptRoot 'HomeCamMonitor-Beta.zip'
Get-Process -Name 'HomeCamMonitor-Beta' -ErrorAction SilentlyContinue | Stop-Process -Force
New-Item -ItemType Directory -Force -Path $target | Out-Null
Expand-Archive -Path $archive -DestinationPath $target -Force
Start-Process (Join-Path $target 'HomeCamMonitor-Beta.exe')
'@
Set-Content -Path (Join-Path $installerFiles 'Install-Beta.ps1') -Value $installScript -Encoding UTF8
Set-Content -Path (Join-Path $installerFiles 'Install-Beta.cmd') -Value '@powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Install-Beta.ps1"' -Encoding ASCII

$sedPath = Join-Path $downloads 'HomeCamMonitor-Beta.sed'
$sed = @"
[Version]
Class=IEXPRESS
SEDVersion=3
[Options]
PackagePurpose=InstallApp
ShowInstallProgramWindow=0
HideExtractAnimation=0
UseLongFileName=1
InsideCompressed=0
CAB_FixedSize=0
CAB_ResvCodeSigning=0
RebootMode=N
InstallPrompt=
DisplayLicense=
FinishMessage=
TargetName=$setupPath
FriendlyName=HomeCam Monitor Beta
AppLaunched=Install-Beta.cmd
PostInstallCmd=<None>
AdminQuietInstCmd=
UserQuietInstCmd=
SourceFiles=SourceFiles
[SourceFiles]
SourceFiles0=$installerFiles\
[SourceFiles0]
%FILE0%=
%FILE1%=
%FILE2%=
[Strings]
FILE0=HomeCamMonitor-Beta.zip
FILE1=Install-Beta.ps1
FILE2=Install-Beta.cmd
"@
Set-Content -Path $sedPath -Value $sed -Encoding ASCII
$iexpress = Start-Process -FilePath "$env:WINDIR\System32\iexpress.exe" -ArgumentList '/N', '/Q', "`"$sedPath`"" -Wait -PassThru
if ($iexpress.ExitCode -ne 0 -or -not (Test-Path $setupPath)) { throw 'Die selbstextrahierende Beta konnte nicht erstellt werden.' }

Remove-Item $installerFiles -Recurse -Force
Remove-Item $sedPath -Force
$hashes = Get-FileHash $setupPath, $zipPath -Algorithm SHA256
$hashes | ForEach-Object { "{0}  {1}" -f $_.Hash.ToLowerInvariant(), (Split-Path $_.Path -Leaf) } |
    Set-Content (Join-Path $downloads 'SHA256SUMS.txt') -Encoding ASCII
