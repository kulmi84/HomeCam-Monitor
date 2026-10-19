param(
    [Parameter(Mandatory)][string]$RuntimeTemplate,
    [string]$Dotnet = 'dotnet'
)
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$template = (Resolve-Path -LiteralPath $RuntimeTemplate).Path
foreach ($file in @('mpv.exe', 'ffmpeg.exe', 'tools/neolink/neolink.exe', 'tools/gstreamer/bin/gstreamer-1.0-0.dll')) {
    if (-not (Test-Path -LiteralPath (Join-Path $template $file))) { throw "Runtime template missing: $file" }
}
$publish = Join-Path $root ('store-artifacts/publish-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $publish -Force | Out-Null
Get-ChildItem -LiteralPath $template | Copy-Item -Destination $publish -Recurse -Force
& $Dotnet publish (Join-Path $root 'HomeCamMonitor.V2.Beta.csproj') -c Release -r win-x64 --self-contained true `
    -p:FinalRelease=true -p:StoreRelease=true -p:PublishSingleFile=false -o $publish
if ($LASTEXITCODE -ne 0) { throw 'Store application publish failed' }
if (-not (Test-Path -LiteralPath (Join-Path $publish 'HomeCamMonitor.StoreBuild.json'))) { throw 'Store build marker missing' }
Write-Output "Store publish: $publish"
