$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
[xml]$properties = Get-Content -LiteralPath (Join-Path $root 'Directory.Build.props') -Raw
$version = $properties.SelectSingleNode('/Project/PropertyGroup/HomeCamV2Version').InnerText
if ($version -notmatch '^\d+\.\d+\.\d+\.\d+$' -or [version]$version -lt [version]'2.0.0.0') {
    throw 'HomeCamV2Version must be a four-part V2 release version.'
}
$version
