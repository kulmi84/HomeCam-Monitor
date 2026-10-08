param([switch]$ReviewOnly)
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$publish = Join-Path $root 'publish'
$downloads = Join-Path $root 'release-downloads'
$lock = Get-Content (Join-Path $root 'third-party.lock.json') -Raw | ConvertFrom-Json
# A review build never implies release clearance. Final packaging requires the
# exact corresponding source archives, dependencies and build recipes.
if (-not $ReviewOnly) {
    if (-not $lock.mpv.sourceCoverageVerified -or -not $lock.ffmpeg.sourceCoverageVerified) {
        throw 'Finales Paket gesperrt: korrespondierende Fremdkomponenten-Quellen sind noch nicht vollständig geprüft. -ReviewOnly erstellt ausschließlich ein Prüfpaket.'
    }
    $manifestPath = Join-Path $root 'sources-manifest.json'
    if (-not (Test-Path -LiteralPath $manifestPath)) { throw 'sources-manifest.json fehlt.' }
    $manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
    if ($manifest.mpvBinarySha256 -ne $lock.mpv.sha256 -or $manifest.ffmpegBinarySha256 -ne $lock.ffmpeg.sha256 -or
        -not $manifest.includesAllDependencies -or -not $manifest.includesBuildScripts -or
        -not $manifest.archives) { throw 'Quellennachweise sind unvollständig oder passen nicht zu den Binaries.' }
    foreach ($archive in $manifest.archives) {
        if ([IO.Path]::IsPathRooted($archive.path) -or ($archive.path -split '[/\\]') -contains '..') {
            throw 'Quellenpaket muss innerhalb des Repository-Arbeitsordners liegen.'
        }
        $source = Join-Path $root $archive.path
        if (-not (Test-Path -LiteralPath $source) -or (Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash -ne $archive.sha256) {
            throw "Quellenpaket fehlt oder hat falsche Prüfsumme: $($archive.path)"
        }
    }
}
foreach ($required in @('HomeCamMonitor.exe','mpv.exe','ffmpeg.exe','LICENSE.txt','THIRD-PARTY-NOTICES.md','licenses/mpv-Copyright.txt','licenses/FFmpeg-binary-LICENSE.txt')) {
    if (-not (Test-Path -LiteralPath (Join-Path $publish $required))) { throw "Paketbestandteil fehlt: $required" }
}
if ((Get-Item (Join-Path $publish 'HomeCamMonitor.exe')).VersionInfo.ProductVersion -ne '1.0.0') { throw 'Programmversion muss 1.0.0 sein.' }
New-Item -ItemType Directory -Force -Path $downloads | Out-Null
$suffix = if ($ReviewOnly) { '-Review' } else { '' }
$zipPath = Join-Path $downloads "HomeCamMonitor-v1.0.0-win-x64$suffix.zip"
$setupPath = Join-Path $downloads "HomeCamMonitor-v1.0.0-Setup$suffix.exe"
$compiler = Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
$icon = Join-Path $root 'assets/HomeCamMonitor.ico'
$uninstaller = Join-Path $publish 'HomeCamMonitor-Uninstall.exe'
$uninstallerSource = Join-Path $PSScriptRoot 'ReleaseUninstaller.cs'
$installerSource = Join-Path $PSScriptRoot 'ReleaseInstaller.cs'
& $compiler /nologo /target:winexe /platform:x64 /optimize+ /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Core.dll "/win32icon:$icon" "/out:$uninstaller" $uninstallerSource
if ($LASTEXITCODE -ne 0) { throw 'Deinstallationsprogramm konnte nicht erstellt werden.' }
if ($ReviewOnly) {
    'PRÜFPAKET – noch nicht zur Weitergabe oder Veröffentlichung freigegeben. Fremdkomponenten-Quellenprüfung ausstehend.' |
        Set-Content (Join-Path $publish 'REVIEW-ONLY.txt') -Encoding utf8
} elseif (Test-Path -LiteralPath (Join-Path $publish 'REVIEW-ONLY.txt')) {
    Remove-Item -LiteralPath (Join-Path $publish 'REVIEW-ONLY.txt')
}
Compress-Archive -Path (Join-Path $publish '*') -DestinationPath $zipPath -CompressionLevel Optimal -Force
& $compiler /nologo /target:winexe /platform:x64 /optimize+ /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Core.dll /reference:System.IO.Compression.dll /reference:System.IO.Compression.FileSystem.dll "/win32icon:$icon" "/resource:$zipPath,HomeCamMonitor.Release.zip" "/resource:$(Join-Path $root 'LICENSE.txt'),HomeCamMonitor.License" "/out:$setupPath" $installerSource $uninstallerSource /main:ReleaseInstaller
if ($LASTEXITCODE -ne 0) { throw 'Setup konnte nicht erstellt werden.' }
if (-not $ReviewOnly) {
    foreach ($archive in $manifest.archives) { Copy-Item -LiteralPath (Join-Path $root $archive.path) -Destination $downloads }
    Copy-Item -LiteralPath $manifestPath -Destination $downloads
}
Get-ChildItem -LiteralPath $downloads -File | Where-Object { $_.Name -ne 'SHA256SUMS.txt' } |
    ForEach-Object { "{0}  {1}" -f (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant(), $_.Name } |
    Set-Content (Join-Path $downloads 'SHA256SUMS.txt') -Encoding ascii
