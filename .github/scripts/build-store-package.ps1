param(
    [Parameter(Mandatory)][string]$PublishDirectory,
    [Parameter(Mandatory)][ValidatePattern('^[A-Za-z0-9.-]{3,50}$')][string]$IdentityName,
    [Parameter(Mandatory)][ValidateNotNullOrEmpty()][string]$Publisher,
    [Parameter(Mandatory)][ValidateNotNullOrEmpty()][string]$PublisherDisplayName,
    [string]$PackageVersion = '',
    [string]$MakeAppx = '',
    [switch]$StageOnly
)
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
if (-not $PackageVersion) { $PackageVersion = & (Join-Path $PSScriptRoot 'get-v2-version.ps1') }
if ($PackageVersion -notmatch '^\d+\.\d+\.\d+\.\d+$') { throw 'PackageVersion must have four numeric parts.' }
$source = (Resolve-Path -LiteralPath $PublishDirectory).Path
if ($Publisher -notmatch '^CN=') { throw 'Use the exact Package/Identity/Publisher value from Partner Center (CN=...).' }
$required = @('HomeCamMonitor-V2-Beta.exe', 'HomeCamMonitor-V2-Beta.runtimeconfig.json',
    'HomeCam-Standard.ico', 'HomeCam-Weiss-transparent.ico', 'HomeCam-Schwarz-transparent.ico', 'Icons-anpassen.txt',
    'coreclr.dll', 'hostfxr.dll', 'mpv.exe', 'ffmpeg.exe', 'OpenCvSharpExtern.dll',
    'tools/neolink/neolink.exe', 'tools/gstreamer/bin/gstreamer-1.0-0.dll', 'tools/gstreamer/bin/glib-2.0-0.dll',
    'THIRD-PARTY-NOTICES.md', 'licenses')
foreach ($file in $required) {
    if (-not (Test-Path -LiteralPath (Join-Path $source $file))) { throw "Prepared self-contained V2 runtime missing: $file" }
}
$info = [Diagnostics.FileVersionInfo]::GetVersionInfo((Join-Path $source $required[0]))
if ($info.ProductVersion -ne $PackageVersion -or $info.FileVersion -ne $PackageVersion -or $info.ProductName -ne 'HomeCam Monitor V2') {
    throw 'Final application product/file versions must match PackageVersion; do not mix V1, Beta or stale application binaries.'
}
if (-not $StageOnly) {
    if ([version]$PackageVersion -ge [version]'2.0.16.0') {
        & (Join-Path $root 'tests/ExeIconResources.ps1') -Executable (Join-Path $source 'HomeCamMonitor-V2-Beta.exe')
    }
    $markerPath = Join-Path $source 'HomeCamMonitor.StoreBuild.json'
    if (-not (Test-Path -LiteralPath $markerPath) -or
        (Get-Content -LiteralPath $markerPath -Raw | ConvertFrom-Json).startupTaskId -ne 'HomeCamMonitorStartup') {
        throw 'Publish the application with -p:FinalRelease=true -p:StoreRelease=true before building the Store package.'
    }
    if (-not $MakeAppx) {
        $sdk = Join-Path ${env:ProgramFiles(x86)} 'Windows Kits/10/bin'
        $MakeAppx = Get-ChildItem -Path "$sdk/*/x64/makeappx.exe" -ErrorAction SilentlyContinue |
            Sort-Object { [version]$_.Directory.Parent.Name } -Descending |
            Select-Object -First 1 -ExpandProperty FullName
    }
    if (-not $MakeAppx -or -not (Test-Path -LiteralPath $MakeAppx)) { throw 'Windows SDK MakeAppx.exe is required; use -StageOnly to prepare files without packing.' }
}
# Each invocation has a fresh staging directory. Never delete or mutate a runtime.
$output = Join-Path $root ('store-artifacts/' + [Guid]::NewGuid().ToString('N'))
$stage = Join-Path $output 'payload'
New-Item -ItemType Directory -Path $stage -Force | Out-Null
Get-ChildItem -LiteralPath $source | Where-Object {
    $_.Name -notmatch '(?i)(?:-Uninstall|-Setup)\.exe$' -and $_.Name -notin @('AppxManifest.xml', 'AppxSignature.p7x', 'AppxBlockMap.xml', '[Content_Types].xml', 'StoreAssets')
} | Copy-Item -Destination $stage -Recurse -Force
# The historical V1 notices describe a different media binary set.
Copy-Item -LiteralPath (Join-Path $root 'THIRD-PARTY-NOTICES-V2.md') -Destination (Join-Path $stage 'THIRD-PARTY-NOTICES.md') -Force
Copy-Item -LiteralPath (Join-Path $root 'THIRD-PARTY-NOTICES-V2.md') -Destination $stage -Force
New-Item -ItemType Directory -Path (Join-Path $stage 'docs/audit') -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $root 'docs/third-party-release-audit-v2.md') -Destination (Join-Path $stage 'docs') -Force
Copy-Item -LiteralPath (Join-Path $root 'docs/audit/v2-2.0.0') -Destination (Join-Path $stage 'docs/audit') -Recurse -Force
Copy-Item -LiteralPath (Join-Path $root 'packaging/store/STORE-COMPONENTS.md') -Destination $stage -Force
Copy-Item -LiteralPath (Join-Path $root 'packaging/store/windows-sdk-notices') -Destination (Join-Path $stage 'licenses/windows-sdk-net-ref') -Recurse -Force
$template = [IO.File]::ReadAllText((Join-Path $root 'packaging/store/AppxManifest.xml.template'))
foreach ($pair in @{
    IdentityName = $IdentityName; Publisher = $Publisher; PublisherDisplayName = $PublisherDisplayName; PackageVersion = $PackageVersion
}.GetEnumerator()) {
    $template = $template.Replace('{{' + $pair.Key + '}}', [Security.SecurityElement]::Escape($pair.Value))
}
[xml]$manifest = $template
[IO.File]::WriteAllText((Join-Path $stage 'AppxManifest.xml'), $manifest.OuterXml, [Text.UTF8Encoding]::new($false))
# Derive Store images from the existing application icon.
Add-Type -AssemblyName System.Drawing
$assets = Join-Path $stage 'StoreAssets'
New-Item -ItemType Directory -Path $assets | Out-Null
Copy-Item -LiteralPath (Join-Path $root 'assets/HomeCamMonitor-V2-Beta.ico') -Destination (Join-Path $assets 'HomeCamMonitor.ico')
$icon = [Drawing.Icon]::new((Join-Path $root 'assets/HomeCamMonitor-V2-Beta.ico'), 256, 256)
$bitmap = $icon.ToBitmap()
$whiteIcon = [Drawing.Icon]::new((Join-Path $root 'assets/homecam-icon-White.ico'), 256, 256)
$blackIcon = [Drawing.Icon]::new((Join-Path $root 'assets/homecam-icon-Black.ico'), 256, 256)
$whiteBitmap = $whiteIcon.ToBitmap()
$blackBitmap = $blackIcon.ToBitmap()
try {
    $storeImages = @{'StoreLogo.png' = 50; 'Square44x44Logo.png' = 44; 'Square150x150Logo.png' = 150}
    # Explorer selects these package resources for grouped/pinned Store apps.
    # Use a transparent white mark on dark taskbars and black on light taskbars.
    foreach ($size in @(16,20,24,30,32,36,40,44,48,60,64,72,80,96,256)) {
        $storeImages["Square44x44Logo.targetsize-$size`_altform-unplated.png"] = $size
        $storeImages["Square44x44Logo.targetsize-$size`_altform-lightunplated.png"] = $size
        $storeImages["Square44x44Logo.targetsize-$size.png"] = $size
    }
    foreach ($asset in $storeImages.GetEnumerator()) {
        $image = [Drawing.Bitmap]::new($asset.Value, $asset.Value)
        $graphics = [Drawing.Graphics]::FromImage($image)
        try {
            $graphics.Clear([Drawing.Color]::Transparent)
            $graphics.InterpolationMode = [Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
            $artwork = if ($asset.Key -like '*_altform-lightunplated.png') { $blackBitmap }
                elseif ($asset.Key.StartsWith('Square44x44Logo')) { $whiteBitmap }
                else { $bitmap }
            $graphics.DrawImage($artwork, 0, 0, $asset.Value, $asset.Value)
            $image.Save((Join-Path $assets $asset.Key), [Drawing.Imaging.ImageFormat]::Png)
        } finally { $graphics.Dispose(); $image.Dispose() }
    }
} finally {
    $bitmap.Dispose(); $icon.Dispose()
    $whiteBitmap.Dispose(); $whiteIcon.Dispose()
    $blackBitmap.Dispose(); $blackIcon.Dispose()
}
if (-not $StageOnly) {
    $makePri = Join-Path ([IO.Path]::GetDirectoryName((Resolve-Path $MakeAppx).Path)) 'makepri.exe'
    if (-not (Test-Path -LiteralPath $makePri)) { throw 'MakePRI is required to index qualified taskbar assets.' }
    $priConfig = Join-Path $output 'priconfig.xml'
    & $makePri createconfig /cf $priConfig /dq en-US /pv 10.0.0 /o *> (Join-Path $output 'makepri-config.log')
    if ($LASTEXITCODE -ne 0) { throw 'MakePRI configuration failed' }
    $resourceList = Join-Path $stage 'taskbar.resfiles'
    Get-ChildItem -LiteralPath $assets -Filter *.png | ForEach-Object { 'StoreAssets\' + $_.Name } | Set-Content -LiteralPath $resourceList -Encoding utf8
    [xml]$config = Get-Content -LiteralPath $priConfig -Raw
    $config.resources.index.SetAttribute('startIndexAt', 'taskbar.resfiles')
    foreach ($node in @($config.resources.index.SelectNodes('indexer-config'))) { [void]$config.resources.index.RemoveChild($node) }
    $node = $config.CreateElement('indexer-config')
    $node.SetAttribute('type', 'resfiles'); $node.SetAttribute('qualifierDelimiter', '.')
    [void]$config.resources.index.AppendChild($node)
    $config.Save($priConfig)
    & $makePri new /pr $stage /cf $priConfig /of (Join-Path $stage 'resources.pri') /o *> (Join-Path $output 'makepri.log')
    if ($LASTEXITCODE -ne 0) { throw 'Taskbar resource indexing failed' }
    Remove-Item -LiteralPath $resourceList
}
$inventory = Get-ChildItem -LiteralPath $stage -File -Recurse | ForEach-Object {
    [pscustomobject]@{ path = [IO.Path]::GetRelativePath($stage, $_.FullName).Replace('\', '/'); sha256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant() }
}
$inventory | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $output 'inventory.json') -Encoding utf8
if (-not $StageOnly) {
    # V2 notices and audit records apply only to the recorded runtime bytes.
    $hashes = @{}
    foreach ($entry in $inventory) { $hashes[$entry.path] = $entry.sha256 }
    $audit = Get-Content -LiteralPath (Join-Path $root 'docs/audit/v2-2.0.0/status.json') -Raw | ConvertFrom-Json
    foreach ($binary in $audit.binaries) {
        if ($hashes[$binary.path] -ne $binary.sha256) { throw "V2 audit binary mismatch: $($binary.path)" }
    }
    $licenses = Get-Content -LiteralPath (Join-Path $root 'docs/audit/v2-2.0.0/approved-license-inventory.json') -Raw | ConvertFrom-Json
    foreach ($license in $licenses) {
        if ($hashes[$license.path] -ne $license.sha256) { throw "V2 license inventory mismatch: $($license.path)" }
    }
    $plugins = Get-Content -LiteralPath (Join-Path $root 'docs/audit/v2-2.0.0/gstreamer-plugins.json') -Raw | ConvertFrom-Json
    foreach ($plugin in $plugins.plugins) {
        if ($hashes['tools/gstreamer/lib/gstreamer-1.0/' + $plugin.file] -ne $plugin.sha256) {
            throw "V2 audit plugin mismatch: $($plugin.file)"
        }
    }
    $package = Join-Path $output "HomeCamMonitor-$PackageVersion-x64.msix"
    $log = Join-Path $output 'makeappx.log'
    & $MakeAppx pack /d $stage /p $package /o *> $log
    if ($LASTEXITCODE -ne 0) { throw "MakeAppx validation/packing failed. See $log" }
    Get-FileHash -LiteralPath $package -Algorithm SHA256 | ForEach-Object {
        $_.Hash.ToLowerInvariant() + '  ' + [IO.Path]::GetFileName($_.Path)
    } | Set-Content -LiteralPath (Join-Path $output 'SHA256SUMS.txt') -Encoding ascii
}
Write-Output "Prepared: $output"
Write-Warning 'Engineering candidate only. Exact-candidate VM tests, Store-specific distribution review and Store certification remain open. Unsigned packages require test signing before local installation.'
