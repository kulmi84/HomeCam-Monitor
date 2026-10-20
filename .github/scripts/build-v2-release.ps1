param(
    [string]$Dotnet = 'dotnet',
    [string]$RuntimeTemplate = 'publish-v2-beta',
    [string]$PublishDirectory = 'publish-v2-release',
    [string]$DownloadsDirectory = 'v2-release-downloads',
    [string]$AuditedSourcesDirectory = ''
)
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$template = (Resolve-Path (Join-Path $root $RuntimeTemplate)).Path
$publish = Join-Path $root $PublishDirectory
$downloads = Join-Path $root $DownloadsDirectory
$version = & (Join-Path $PSScriptRoot 'get-v2-version.ps1')
if ($AuditedSourcesDirectory) {
    & (Join-Path $PSScriptRoot 'verify-v2-component-set.ps1') -Runtime $template -SourcesDirectory $AuditedSourcesDirectory
}
foreach ($file in @('mpv.exe', 'ffmpeg.exe', 'tools/neolink/neolink.exe', 'tools/gstreamer/bin/gstreamer-1.0-0.dll')) {
    if (-not (Test-Path -LiteralPath (Join-Path $template $file))) { throw "V2 runtime missing: $file" }
}
New-Item -ItemType Directory -Force -Path $publish, $downloads | Out-Null
# Reuse the exact tested Beta runtime, never download mutable latest binaries here.
Get-ChildItem -LiteralPath $template | Where-Object Name -notin @('README-V2-Beta.md', 'HomeCamMonitor.StoreBuild.json', 'AppxManifest.xml', 'resources.pri', 'StoreAssets') | Copy-Item -Destination $publish -Recurse -Force
& $Dotnet publish (Join-Path $root 'HomeCamMonitor.V2.Beta.csproj') -c Release -r win-x64 --self-contained true -p:FinalRelease=true -p:PublishSingleFile=false -o $publish
if ($LASTEXITCODE -ne 0) { throw 'Final V2 publish failed.' }
$executable = Join-Path $publish 'HomeCamMonitor-V2-Beta.exe'
$info = [Diagnostics.FileVersionInfo]::GetVersionInfo($executable)
if ($info.ProductVersion -ne $version -or $info.ProductName -ne 'HomeCam Monitor V2') { throw 'Incorrect final application branding/version.' }
& (Join-Path $root 'tests/ExeIconResources.ps1') -Executable $executable
Copy-Item -LiteralPath (Join-Path $root 'docs') -Destination $publish -Recurse -Force
$readme = [IO.File]::ReadAllText((Join-Path $root "docs/release-v$version.md")).Replace('(release-notes-v2-beta37.md)', '(docs/release-notes-v2-beta37.md)').Replace('(reolink-v2-components.md)', '(docs/reolink-v2-components.md)')
$readme = $readme.Replace('(v2.0.0.md)', '(docs/v2.0.0.md)').Replace('(screenshots/', '(docs/screenshots/')
$readme = $readme.Replace('(third-party-release-audit-v2.md)', '(docs/third-party-release-audit-v2.md)')
[IO.File]::WriteAllText((Join-Path $publish 'README-V2.md'), $readme, [Text.UTF8Encoding]::new($false))
# Local legacy builds cannot inherit the audited candidate's source coverage.
$auditedMedia = ((Get-FileHash -LiteralPath (Join-Path $publish 'mpv.exe')).Hash -eq 'bb51c0d176ea76c26777cd05f92d65a724ef96253599e5ab3bf0c1af7245ede9') -and
    ((Get-FileHash -LiteralPath (Join-Path $publish 'ffmpeg.exe')).Hash -eq '0cdeb8e922e75e2f9c39cf85cab79cff2c899df6dc703e77bcb5b849193498d1')
$notice = if ($auditedMedia) { 'THIRD-PARTY-NOTICES-V2.md' } else { 'THIRD-PARTY-NOTICES-V2-LOCAL-LEGACY.md' }
Copy-Item -LiteralPath (Join-Path $root $notice) -Destination (Join-Path $publish 'THIRD-PARTY-NOTICES.md') -Force
Copy-Item -LiteralPath (Join-Path $root $notice) -Destination (Join-Path $publish 'THIRD-PARTY-NOTICES-V2.md') -Force
Copy-Item -LiteralPath (Join-Path $root 'README.md') -Destination $publish -Force

# Same installer implementation and deployed IDs as Beta37: update in place,
# preserving settings, DPAPI secrets, autostart and the V1 directory guard.
$generated = Join-Path $downloads 'installer-source'
New-Item -ItemType Directory -Force -Path $generated | Out-Null
foreach ($name in @('V2BetaInstaller.cs', 'V2BetaUninstaller.cs')) {
    $text = [IO.File]::ReadAllText((Join-Path $PSScriptRoot $name))
    $text = $text.Replace('HomeCam Monitor V2 Beta', 'HomeCam Monitor V2')
    $text = $text.Replace('Beta-Einstellungen', 'V2-Einstellungen').Replace('Beta-Paket', 'V2-Paket').Replace('für V2 Beta', 'für V2')
    $text = $text.Replace('2.0.0-beta.37', $version).Replace('"2.0.0.37"', '"' + $version + '"')
    [IO.File]::WriteAllText((Join-Path $generated $name), $text, [Text.UTF8Encoding]::new($false))
}
$compiler = Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
$icon = Join-Path $root 'assets/HomeCamMonitor-V2-Beta.ico'
$installerSource = Join-Path $generated 'V2BetaInstaller.cs'
$uninstallerSource = Join-Path $generated 'V2BetaUninstaller.cs'
$uninstaller = Join-Path $publish 'HomeCamMonitor-V2-Beta-Uninstall.exe'
& $compiler /nologo /define:RELEASE_V2 /target:winexe /platform:x64 /optimize+ /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Core.dll "/win32icon:$icon" "/out:$uninstaller" $uninstallerSource
if ($LASTEXITCODE -ne 0) { throw 'Final uninstaller build failed.' }
$zip = Join-Path $downloads "HomeCamMonitor-V2-$version-win-x64.zip"
Compress-Archive -Path (Join-Path $publish '*') -DestinationPath $zip -CompressionLevel Optimal -Force
$setup = Join-Path $downloads "HomeCamMonitor-V2-$version-Setup.exe"
& $compiler /nologo /define:RELEASE_V2 /target:winexe /platform:x64 /optimize+ /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Core.dll /reference:System.IO.Compression.dll /reference:System.IO.Compression.FileSystem.dll "/win32icon:$icon" "/resource:$zip,HomeCamMonitor.V2.Beta.zip" "/out:$setup" $installerSource $uninstallerSource /main:V2BetaInstaller
if ($LASTEXITCODE -ne 0) { throw 'Final installer build failed.' }
if ([Diagnostics.FileVersionInfo]::GetVersionInfo($setup).FileVersion -ne $version) { throw 'Incorrect final installer version.' }
# Inventory is evidence of the actual package, not a source/license clearance.
$inventory = Get-ChildItem -LiteralPath $publish -File -Recurse | ForEach-Object {
    [pscustomobject]@{ path = $_.FullName.Substring($publish.Length + 1).Replace('\', '/'); sha256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant(); bytes = $_.Length }
}
$inventory | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $downloads 'package-inventory.json') -Encoding utf8
Get-FileHash -LiteralPath $setup, $zip -Algorithm SHA256 | ForEach-Object { $_.Hash.ToLowerInvariant() + '  ' + [IO.Path]::GetFileName($_.Path) } | Set-Content -LiteralPath (Join-Path $downloads 'SHA256SUMS.txt') -Encoding ascii
if ($AuditedSourcesDirectory) {
    Write-Output "Created audited V2 $version in $downloads. Publish its three exact source archives permanently alongside public binaries."
} else {
    Write-Output "Created local V2 $version in $downloads. This build has no completed component-source gate."
}
