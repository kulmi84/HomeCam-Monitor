$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$releaseVersion = & (Join-Path $root '.github/scripts/get-v2-version.ps1')
$fixture = Join-Path $root ('store-artifacts/test-fixture-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $fixture -Force | Out-Null
@'
using System.Reflection;
[assembly: AssemblyProduct("HomeCam Monitor V2")]
[assembly: AssemblyInformationalVersion("RELEASE_VERSION")]
[assembly: AssemblyFileVersion("RELEASE_VERSION")]
class Fixture { static void Main() {} }
'@.Replace('RELEASE_VERSION', $releaseVersion) | Set-Content -LiteralPath (Join-Path $fixture 'Fixture.cs')
$compiler = Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
& $compiler /nologo /target:winexe "/out:$fixture/HomeCamMonitor-V2-Beta.exe" (Join-Path $fixture 'Fixture.cs')
if ($LASTEXITCODE -ne 0) { throw 'Fixture compilation failed' }
foreach ($name in @('HomeCamMonitor-V2-Beta.runtimeconfig.json', 'coreclr.dll', 'hostfxr.dll',
    'mpv.exe', 'ffmpeg.exe', 'OpenCvSharpExtern.dll', 'tools/neolink/neolink.exe',
    'tools/gstreamer/bin/gstreamer-1.0-0.dll', 'tools/gstreamer/bin/glib-2.0-0.dll', 'THIRD-PARTY-NOTICES.md', 'licenses/test.txt',
    'HomeCamMonitor-V2-Beta-Uninstall.exe')) {
    $target = Join-Path $fixture $name
    New-Item -ItemType Directory -Path ([IO.Path]::GetDirectoryName($target)) -Force | Out-Null
    'TEST FIXTURE ONLY - not a runtime' | Set-Content -LiteralPath $target
}
$buildScript = Join-Path $root '.github/scripts/build-store-package.ps1'
$result = & $buildScript -PublishDirectory $fixture -IdentityName 'Example.HomeCamMonitor' `
    -Publisher 'CN=Test & Publisher' -PublisherDisplayName 'Test & Publisher' -StageOnly
$line = @($result | Where-Object { $_ -like 'Prepared: *' })[0]
if (-not $line) { throw 'Missing staging output' }
$testOutput = $line.Substring(10)
'SYNTHETIC STAGING TEST ONLY. Not a working application.' | Set-Content (Join-Path $testOutput 'TEST-FIXTURE.txt')
$payload = Join-Path $testOutput 'payload'
[xml]$xml = Get-Content (Join-Path $payload 'AppxManifest.xml') -Raw
if ($xml.Package.Identity.Publisher -ne 'CN=Test & Publisher') { throw 'XML escaping failed' }
if ([version]$xml.Package.Identity.Version -le [version]'2.0.0.0') { throw 'Upgrade package must have a higher version' }
if ($xml.Package.Applications.Application.Extensions.Extension.StartupTask.TaskId -ne 'HomeCamMonitorStartup' -or
    $xml.Package.Applications.Application.Extensions.Extension.StartupTask.Enabled -ne 'false') { throw 'Startup must be opt-in' }
$shortcut = @($xml.Package.Applications.Application.Extensions.Extension | Where-Object Category -eq 'windows.shortcut')
if ($shortcut.Count -ne 1 -or $shortcut[0].Executable -ne 'HomeCamMonitor-V2-Beta.exe' -or
    $shortcut[0].Shortcut.File -ne '$(Desktop)\HomeCam Monitor V2.lnk' -or
    $shortcut[0].Shortcut.Icon -ne 'StoreAssets\HomeCamMonitor.ico') { throw 'Desktop shortcut declaration incorrect' }
if ((Get-FileHash (Join-Path $payload 'StoreAssets/HomeCamMonitor.ico')).Hash -ne
    (Get-FileHash (Join-Path $root 'assets/HomeCamMonitor-V2-Beta.ico')).Hash) { throw 'Desktop shortcut icon does not match confirmed icon' }
$paths = @($xml.Package.Extensions.Extension.LoaderSearchPathOverride.LoaderSearchPathEntry | ForEach-Object { $_.GetAttribute('FolderPath') })
if ($paths.Count -ne 2 -or $paths -notcontains '' -or $paths -notcontains 'tools\gstreamer\bin') { throw 'Packaged DLL search must include root and GStreamer bin' }
if ($xml.Package.Dependencies.PackageDependency.Name -ne 'Microsoft.VCLibs.140.00.UWPDesktop') { throw 'VC++ framework dependency missing' }
if (Test-Path (Join-Path $payload 'HomeCamMonitor-V2-Beta-Uninstall.exe')) { throw 'Uninstaller leaked' }
if ((Get-FileHash (Join-Path $payload 'THIRD-PARTY-NOTICES.md')).Hash -ne
    (Get-FileHash (Join-Path $root 'THIRD-PARTY-NOTICES-V2.md')).Hash) { throw 'V1 notices must be replaced by V2 notices' }
Add-Type -AssemblyName System.Drawing
foreach ($entry in @{'StoreLogo.png'=50; 'Square44x44Logo.png'=44; 'Square150x150Logo.png'=150}.GetEnumerator()) {
    $image = [Drawing.Image]::FromFile((Join-Path $payload ('StoreAssets/' + $entry.Key)))
    try {
        if ($image.Width -ne $entry.Value -or $image.Height -ne $entry.Value) { throw 'Wrong asset size' }
    } finally { $image.Dispose() }
}
foreach ($size in @(16,20,24,32,40,44,48,64,96,256)) {
    $taskAsset = Join-Path $payload "StoreAssets/Square44x44Logo.targetsize-$size`_altform-unplated.png"
    $taskImage = [Drawing.Bitmap]::new($taskAsset)
    try {
        if ($taskImage.Width -ne $size -or $taskImage.Height -ne $size -or $taskImage.GetPixel(0,0).A -ne 0) { throw 'Taskbar icon must have exact size and transparent corners' }
    } finally { $taskImage.Dispose() }
}
$inventory = Get-Content (Join-Path $testOutput 'inventory.json') -Raw | ConvertFrom-Json
foreach ($entry in $inventory) {
    $hash = (Get-FileHash -LiteralPath (Join-Path $payload $entry.path) -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($hash -ne $entry.sha256) { throw "Wrong hash: $($entry.path)" }
}
if (-not ($inventory | Where-Object path -eq 'AppxManifest.xml')) { throw 'Missing manifest inventory' }
# Missing required runtime must fail before creating any output.
$rejected = $false
try {
    & $buildScript -PublishDirectory (Join-Path $fixture 'licenses') -IdentityName 'Example.HomeCamMonitor' `
        -Publisher 'CN=Test' -PublisherDisplayName 'Test' -StageOnly
} catch { $rejected = $_.Exception.Message -like '*runtime missing*' }
if (-not $rejected) { throw 'Incomplete runtime accepted' }
Write-Output 'PASS: synthetic staging, XML escaping, uninstaller exclusion, image dimensions, inventory hashes, incomplete-runtime rejection. No real application was packed or run.'
