param(
    [Parameter(Mandatory)][string]$Runtime,
    [Parameter(Mandatory)][string]$SourcesDirectory,
    [string]$AuditLock = (Join-Path $PSScriptRoot '../../v2-component-review.lock.json')
)
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$runtimePath = (Resolve-Path -LiteralPath $Runtime).Path
$sourcesPath = (Resolve-Path -LiteralPath $SourcesDirectory).Path
$review = Get-Content -LiteralPath $AuditLock -Raw | ConvertFrom-Json
if ($review.schemaVersion -ne 1 -or $review.componentSourceCoverageVerified -ne $true) { throw 'V2 component review is incomplete.' }
function Assert-Hash([string]$Base, [string]$Relative, [string]$Expected) {
    $path = [IO.Path]::GetFullPath((Join-Path $Base $Relative))
    if (-not $path.StartsWith($Base.TrimEnd('\', '/') + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw 'Audit path escapes its root.' }
    if (-not (Test-Path -LiteralPath $path -PathType Leaf) -or (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -ne $Expected) { throw "Missing or unreviewed file: $Relative" }
}
foreach ($file in $review.binaries) { Assert-Hash $runtimePath $file.path $file.sha256 }
foreach ($archive in $review.sources) { Assert-Hash $sourcesPath $archive.file $archive.sha256 }
$gst = Get-Content -LiteralPath (Join-Path $root $review.gstreamerInventory) -Raw | ConvertFrom-Json
foreach ($file in $gst.files) { Assert-Hash $runtimePath ('tools/gstreamer/' + $file.path) $file.sha256 }
$actual = @(Get-ChildItem -LiteralPath (Join-Path $runtimePath 'tools/gstreamer') -File -Recurse)
if ($actual.Count -ne @($gst.files).Count) { throw 'Unexpected GStreamer runtime file set.' }
$licenses = Get-Content -LiteralPath (Join-Path $root $review.licenseInventory) -Raw | ConvertFrom-Json
foreach ($file in $licenses) { Assert-Hash $runtimePath $file.path $file.sha256 }
Write-Output "V2 components verified: $($review.binaries.Count) binaries, $($gst.files.Count) GStreamer files, $($licenses.Count) notices, $($review.sources.Count) source archives."
Write-Output 'Before public distribution, publish these exact source archives permanently alongside the installer and ZIP.'
