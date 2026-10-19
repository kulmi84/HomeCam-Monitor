param(
    [Parameter(Mandatory)][string]$Package,
    [Parameter(Mandatory)][string]$Inventory,
    [string]$PackageVersion = ''
)
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
if (-not $PackageVersion) { $PackageVersion = & (Join-Path $PSScriptRoot 'get-v2-version.ps1') }
$identity = Import-PowerShellDataFile (Join-Path $root 'packaging/store/identity.psd1')
$expected = Get-Content -LiteralPath $Inventory -Raw | ConvertFrom-Json
$expectedHashes = @{}
foreach ($item in $expected) { $expectedHashes[$item.path] = $item.sha256 }
$noticeHash = (Get-FileHash -LiteralPath (Join-Path $root 'THIRD-PARTY-NOTICES-V2.md') -Algorithm SHA256).Hash.ToLowerInvariant()
if ($expectedHashes['THIRD-PARTY-NOTICES.md'] -ne $noticeHash) { throw 'Package contains outdated third-party notices' }
$componentAudit = Get-Content -LiteralPath (Join-Path $root 'docs/audit/v2-2.0.0/status.json') -Raw | ConvertFrom-Json
foreach ($binary in $componentAudit.binaries) {
    if ($expectedHashes[$binary.path] -ne $binary.sha256) { throw "Component audit mismatch: $($binary.path)" }
}
$pluginAudit = Get-Content -LiteralPath (Join-Path $root 'docs/audit/v2-2.0.0/gstreamer-plugins.json') -Raw | ConvertFrom-Json
foreach ($plugin in $pluginAudit.plugins) {
    if ($expectedHashes['tools/gstreamer/lib/gstreamer-1.0/' + $plugin.file] -ne $plugin.sha256) {
        throw "Plugin audit mismatch: $($plugin.file)"
    }
}
$zip = [IO.Compression.ZipFile]::OpenRead((Resolve-Path -LiteralPath $Package).Path)
$licenseAudit = Get-Content -LiteralPath (Join-Path $root 'docs/audit/v2-2.0.0/approved-license-inventory.json') -Raw | ConvertFrom-Json
foreach ($license in $licenseAudit) {
    if ($expectedHashes[$license.path] -ne $license.sha256) { $zip.Dispose(); throw "License inventory mismatch: $($license.path)" }
}
try {
    $entries = @{}
    # OPC uses escaped part names, e.g. libstdc%2B%2B-6.dll.
    foreach ($entry in $zip.Entries) { $entries[[Uri]::UnescapeDataString($entry.FullName.Replace('\', '/'))] = $entry }
    foreach ($item in $expected) {
        $entry = $entries[$item.path]
        if (-not $entry) { throw "Missing packaged file: $($item.path)" }
        $stream = $entry.Open()
        $sha = [Security.Cryptography.SHA256]::Create()
        try { $hash = [Convert]::ToHexString($sha.ComputeHash($stream)).ToLowerInvariant() }
        finally { $sha.Dispose(); $stream.Dispose() }
        if ($hash -ne $item.sha256) { throw "Packaged bytes differ: $($item.path)" }
    }
    $unexpected = @($entries.Keys | Where-Object {
        $_ -notin $expected.path -and $_ -notin @('AppxBlockMap.xml', '[Content_Types].xml', 'AppxSignature.p7x', 'AppxMetadata/CodeIntegrity.cat')
    })
    if ($unexpected.Count) { throw "Unexpected package entries: $($unexpected -join ', ')" }
    $reader = [IO.StreamReader]::new($entries['AppxManifest.xml'].Open())
    try { [xml]$manifest = $reader.ReadToEnd() } finally { $reader.Dispose() }
    if ([version]$PackageVersion -ge [version]'2.0.12.0') {
        $versionCheck = Join-Path $root ('store-artifacts/version-check-' + [guid]::NewGuid().ToString('N'))
        New-Item -ItemType Directory -Path $versionCheck -Force | Out-Null
        foreach ($name in @('HomeCamMonitor-V2-Beta.exe', 'HomeCamMonitor-V2-Beta.dll')) {
            $target = Join-Path $versionCheck $name
            $inputStream = $entries[$name].Open()
            $outputStream = [IO.File]::Create($target)
            try { $inputStream.CopyTo($outputStream) } finally { $outputStream.Dispose(); $inputStream.Dispose() }
            $info = [Diagnostics.FileVersionInfo]::GetVersionInfo($target)
            if ($info.ProductVersion -ne $PackageVersion -or $info.FileVersion -ne $PackageVersion) {
                throw "Packaged application version differs from Store version: $name"
            }
        }
    }
    if ($manifest.Package.Identity.Name -ne $identity.IdentityName -or
        $manifest.Package.Identity.Publisher -ne $identity.Publisher -or
        $manifest.Package.Properties.PublisherDisplayName -ne $identity.PublisherDisplayName -or
        $manifest.Package.Identity.Version -ne $PackageVersion -or
        $manifest.Package.Identity.ProcessorArchitecture -ne 'x64' -or
        $manifest.Package.Properties.DisplayName -ne 'HomeCam Monitor V2') { throw 'Store identity/version mismatch' }
    if ([version]$PackageVersion -ge [version]'2.0.8.0') {
        $shortcut = @($manifest.Package.Applications.Application.Extensions.Extension | Where-Object Category -eq 'windows.shortcut')
        if ($shortcut.Count -ne 1 -or $shortcut[0].Executable -ne 'HomeCamMonitor-V2-Beta.exe' -or
            $shortcut[0].Shortcut.File -ne '$(Desktop)\HomeCam Monitor V2.lnk' -or
            $shortcut[0].Shortcut.Icon -ne 'StoreAssets\HomeCamMonitor.ico') { throw 'Desktop shortcut declaration incorrect' }
        $iconHash = (Get-FileHash -LiteralPath (Join-Path $root 'assets/HomeCamMonitor-V2-Beta.ico')).Hash.ToLowerInvariant()
        if ($expectedHashes['StoreAssets/HomeCamMonitor.ico'] -ne $iconHash) { throw 'Desktop shortcut icon differs from approved artwork' }
        if (-not $entries.ContainsKey('resources.pri')) { throw 'Windows icon resource index missing' }
    }
    if ([version]$PackageVersion -ge [version]'2.0.1.0') {
        $startup = $manifest.Package.Applications.Application.Extensions.Extension.StartupTask
        if ($startup.TaskId -ne 'HomeCamMonitorStartup' -or $startup.Enabled -ne 'false' -or
            -not $entries.ContainsKey('HomeCamMonitor.StoreBuild.json')) { throw 'Store startup declaration/build marker missing' }
    }
    if ([version]$PackageVersion -ge [version]'2.0.2.0') {
        $paths = @($manifest.Package.Extensions.Extension.LoaderSearchPathOverride.LoaderSearchPathEntry | ForEach-Object { $_.GetAttribute('FolderPath') })
        if ($paths.Count -ne 2 -or $paths -notcontains '' -or $paths -notcontains 'tools\gstreamer\bin' -or
            -not $entries.ContainsKey('tools/gstreamer/bin/glib-2.0-0.dll')) { throw 'MSIX GLib/GStreamer loader path missing' }
        if ($manifest.Package.Dependencies.PackageDependency.Name -ne 'Microsoft.VCLibs.140.00.UWPDesktop' -or
            $manifest.Package.Dependencies.PackageDependency.MinVersion -ne '14.0.33321.0') { throw 'VC++ runtime framework dependency missing' }
    }
    if ([version]$PackageVersion -ge [version]'2.0.4.0') {
        $languages = @($manifest.Package.Resources.Resource | ForEach-Object { $_.Language })
        if ($languages -notcontains 'de-de' -or $languages -notcontains 'en-us') { throw 'German/English package language declarations missing' }
    }
    if ($entries.Keys | Where-Object { $_ -match '(?i)(?:-Uninstall|-Setup)\.exe$|(^|/)settings\.json$|\.pfx$' }) {
        throw 'Installer or private settings/key material must not be packaged'
    }
} finally { $zip.Dispose() }
$packageFile = Get-Item -LiteralPath $Package
$packageHash = (Get-FileHash -LiteralPath $Package -Algorithm SHA256).Hash.ToLowerInvariant()
$report = [ordered]@{
    status = 'PACKAGE_CONTENT_VALIDATED_NOT_RELEASE_READY'
    package = $packageFile.Name
    bytes = $packageFile.Length
    sha256 = $packageHash
    storeId = '9P6668481MGG'
    identity = $identity.IdentityName
    payloadFilesVerified = @($expected).Count
    signaturePresent = $entries.ContainsKey('AppxSignature.p7x')
    componentBinariesMatched = @($componentAudit.binaries).Count
    pluginsMatched = @($pluginAudit.plugins).Count
    licenseFilesMatched = @($licenseAudit).Count
    componentReleaseReady = ($componentAudit.componentSourceCoverageVerified -and $componentAudit.publicReleaseReady -and $componentAudit.publicSourceAssetsPublished)
    validation = @('Every payload file matches SHA-256 inventory', 'Manifest identity/version', 'No installer or settings.json/PFX')
    notTested = @('This verifier does not run installation, camera, upgrade/uninstall or Windows App Certification Kit tests; consult docs/store-vm-test-results.md for separately reported VM results')
    blockers = @('VM regression check for this exact candidate and uninstall test', 'Windows App Certification Kit', 'Public privacy policy and Store submission metadata', 'Final distribution review including Store-specific runtime components')
}
$report | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $packageFile.DirectoryName 'validation.json') -Encoding utf8
($packageHash + '  ' + $packageFile.Name) | Set-Content -LiteralPath (Join-Path $packageFile.DirectoryName 'SHA256SUMS.txt') -Encoding ascii
Write-Output "PASS: $(@($expected).Count) packaged payload hashes, Store identity, version, excluded files. Local installation and release checks remain open."
