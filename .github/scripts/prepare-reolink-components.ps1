param([string]$Publish = 'publish-v2-beta', [string]$Cache = 'reolink-component-cache', [string]$PrebuiltNeolinkExecutable = '')
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$publishPath = Join-Path $root $Publish
$cachePath = Join-Path $root $Cache
$lock = Get-Content (Join-Path $root 'reolink-components.lock.json') -Raw | ConvertFrom-Json
New-Item -ItemType Directory -Force -Path $cachePath | Out-Null
function Get-VerifiedComponent([string]$Url, [string]$Hash, [string]$File) {
    $path = Join-Path $cachePath $File
    if (-not (Test-Path -LiteralPath $path)) { Invoke-WebRequest $Url -OutFile $path }
    if ((Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -ne $Hash) { throw "Prüfsumme für $File stimmt nicht." }
    return $path
}
$gst = Get-VerifiedComponent $lock.gstreamer.url $lock.gstreamer.sha256 'gstreamer.msi'
$lessmsi = Get-VerifiedComponent $lock.lessmsi.url $lock.lessmsi.sha256 'lessmsi.zip'
$source = Get-VerifiedComponent $lock.neolink.sourceUrl $lock.neolink.sourceSha256 'neolink-source.zip'
Expand-Archive -LiteralPath $lessmsi -DestinationPath (Join-Path $cachePath 'lessmsi') -Force
$extractor = Join-Path $cachePath 'lessmsi/lessmsi.exe'
$gstExtract = (Join-Path $cachePath 'gst-extracted') + [IO.Path]::DirectorySeparatorChar
& $extractor x $gst $gstExtract | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'GStreamer konnte nicht lokal entpackt werden.' }
$gstRoot = Join-Path $gstExtract 'SourceDir/PFiles64/gstreamer/1.0/msvc_x86_64'
if (-not (Test-Path (Join-Path $gstRoot 'bin/gstreamer-1.0-0.dll'))) { throw 'GStreamer-Laufzeit fehlt im Paket.' }
$neolinkTarget = Join-Path $publishPath 'tools/neolink'
$gstTarget = Join-Path $publishPath 'tools/gstreamer'
$licenseTarget = Join-Path $publishPath 'licenses'
New-Item -ItemType Directory -Force -Path $neolinkTarget, $gstTarget, $licenseTarget | Out-Null
Get-ChildItem -LiteralPath $gstRoot | Copy-Item -Destination $gstTarget -Recurse -Force
Copy-Item -LiteralPath $source -Destination (Join-Path $licenseTarget 'neolink-0.6.2-source.zip')
$streamOutput = Join-Path $cachePath 'neolink-stream3'
& (Join-Path $PSScriptRoot 'build-neolink-stream.ps1') -SourceArchive $source -GStreamerRoot $gstTarget -OutputDirectory $streamOutput -BuildDirectory (Join-Path $cachePath ('stream-build-' + [Guid]::NewGuid().ToString('N'))) -PrebuiltExecutable $PrebuiltNeolinkExecutable
Copy-Item -LiteralPath (Join-Path $streamOutput 'neolink.exe') -Destination $neolinkTarget
Copy-Item -LiteralPath (Join-Path $streamOutput 'neolink-0.6.2-homecam-stream3-source.zip') -Destination $licenseTarget
Copy-Item -LiteralPath (Join-Path $root 'docs/reolink-v2-components.md') -Destination $licenseTarget
Copy-Item -LiteralPath (Join-Path $root 'reolink-components.lock.json') -Destination $publishPath
Copy-Item -LiteralPath (Join-Path $root 'docs/v2-beta.md') -Destination (Join-Path $publishPath 'README-V2-Beta.md')
Copy-Item -LiteralPath (Join-Path $root 'LICENSE.txt'), (Join-Path $root 'THIRD-PARTY-NOTICES.md') -Destination $publishPath
Get-ChildItem -LiteralPath (Join-Path $root 'licenses') | Copy-Item -Destination $licenseTarget -Recurse -Force
# Check the actual executable with its private runtime, without camera credentials.
$originalPath = $env:PATH
try {
    $env:PATH = (Join-Path $gstTarget 'bin') + [IO.Path]::PathSeparator + $env:PATH
    $output = & (Join-Path $neolinkTarget 'neolink.exe') --version
    if ($LASTEXITCODE -ne 0 -or $output -notmatch '^neolink 0\.6\.2$') { throw 'Reolink-Verbindungsdienst konnte nicht gestartet werden.' }
} finally { $env:PATH = $originalPath }
