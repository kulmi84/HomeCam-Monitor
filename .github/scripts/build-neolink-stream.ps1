param(
    [Parameter(Mandatory)][string]$SourceArchive,
    [Parameter(Mandatory)][string]$GStreamerRoot,
    [Parameter(Mandatory)][string]$OutputDirectory,
    [Parameter(Mandatory)][string]$BuildDirectory,
    [string]$PrebuiltExecutable = '',
    [string]$MsvcBin = ''
)
$ErrorActionPreference = 'Stop'
$project = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$lock = Get-Content (Join-Path $project 'reolink-components.lock.json') -Raw | ConvertFrom-Json
if ((Get-FileHash -LiteralPath $SourceArchive).Hash -ne $lock.neolink.sourceSha256) { throw 'Unexpected upstream source archive.' }
$build = [IO.Path]::GetFullPath($BuildDirectory)
if (Test-Path -LiteralPath $build) { throw 'Use a new build directory; existing files are never removed.' }
New-Item -ItemType Directory -Path $build | Out-Null
Expand-Archive -LiteralPath $SourceArchive -DestinationPath (Join-Path $build 'source')
$source = (Get-ChildItem (Join-Path $build 'source') -Directory | Select-Object -First 1).FullName
$patch = Join-Path $build 'changes.patch'
$patchText = [IO.File]::ReadAllText((Join-Path $project 'compat/neolink-stream3/changes.patch')).Replace("`r`n", "`n")
[IO.File]::WriteAllText($patch, $patchText, [Text.UTF8Encoding]::new($false))
& git -C $source apply --check --unsafe-paths "--directory=$source" $patch
if ($LASTEXITCODE -ne 0) { throw 'Source patch check failed.' }
& git -C $source apply --unsafe-paths "--directory=$source" $patch
if ($LASTEXITCODE -ne 0) { throw 'Source patch failed.' }
Copy-Item (Join-Path $project 'compat/neolink-stream3/Cargo.lock') (Join-Path $source 'Cargo.lock')
Copy-Item (Join-Path $project 'compat/neolink-stream3/README.md') (Join-Path $source 'HOMECAM-MODIFICATION.md')
Copy-Item (Join-Path $project 'compat/neolink-stream3/PkgConfig.cs.txt') (Join-Path $source 'PkgConfig.cs')
Copy-Item $PSCommandPath (Join-Path $source 'build-neolink-stream.ps1')
Copy-Item $patch (Join-Path $source 'homecam-stream3.patch')
$output = [IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Force -Path $output | Out-Null
Compress-Archive -Path (Join-Path $source '*') -DestinationPath (Join-Path $output 'neolink-0.6.2-homecam-stream3-source.zip') -Force

if ($PrebuiltExecutable) {
    if ((Get-FileHash -LiteralPath $PrebuiltExecutable).Hash -ne $lock.neolink.streamExecutableSha256) { throw 'Unexpected prebuilt compatibility executable.' }
    Copy-Item -LiteralPath $PrebuiltExecutable -Destination (Join-Path $output 'neolink.exe')
    return
}

# Build against DLLs already present in the private GStreamer runtime. The Rust
# bindings provide their own FFI declarations, so no system SDK installation is needed.
if (-not $MsvcBin) {
    $vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio/Installer/vswhere.exe'
    $vs = & $vswhere -latest -products '*' -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath
    if (-not $vs) { throw 'MSVC build tools are required.' }
    $toolset = Get-ChildItem (Join-Path $vs 'VC/Tools/MSVC') -Directory | Sort-Object Name -Descending | Select-Object -First 1
    $MsvcBin = Join-Path $toolset.FullName 'bin/Hostx64/x64'
}
$msvc = [IO.Path]::GetFullPath((Join-Path $MsvcBin '../../..'))
$sdk = Join-Path ${env:ProgramFiles(x86)} 'Windows Kits/10'
$sdkVersion = Get-ChildItem (Join-Path $sdk 'Lib') -Directory | Sort-Object Name -Descending | Select-Object -First 1
$env:PATH = $MsvcBin + ';' + $env:PATH
if (-not $env:LIB) { $env:LIB = "$msvc/lib/x64;$sdk/Lib/$($sdkVersion.Name)/ucrt/x64;$sdk/Lib/$($sdkVersion.Name)/um/x64" }
if (-not $env:INCLUDE) { $env:INCLUDE = "$msvc/include;$sdk/Include/$($sdkVersion.Name)/ucrt;$sdk/Include/$($sdkVersion.Name)/shared;$sdk/Include/$($sdkVersion.Name)/um" }
$imports = Join-Path $build 'imports'
New-Item -ItemType Directory -Path $imports | Out-Null
foreach ($name in @('glib-2.0-0','gobject-2.0-0','gio-2.0-0','gstreamer-1.0-0','gstbase-1.0-0','gstapp-1.0-0','gstrtsp-1.0-0','gstrtspserver-1.0-0','gstsdp-1.0-0','gstnet-1.0-0','gstvideo-1.0-0')) {
    $exports = (& (Join-Path $MsvcBin 'dumpbin.exe') /nologo /exports (Join-Path $GStreamerRoot "bin/$name.dll")) -join "`n"
    if ($LASTEXITCODE -ne 0) { throw "Cannot read exports for $name" }
    $symbols = [regex]::Matches($exports, '(?m)^\s+\d+\s+[A-Fa-f0-9]+\s+[A-Fa-f0-9]+\s+(\w+)\s*$') | ForEach-Object { $_.Groups[1].Value }
    if (-not $symbols) { throw "No exports for $name" }
    $definition = Join-Path $imports "$name.def"
    [IO.File]::WriteAllLines($definition, @("LIBRARY $name.dll",'EXPORTS') + $symbols, [Text.UTF8Encoding]::new($false))
    $library = $name.Substring(0,$name.Length-2) + '.lib'
    & (Join-Path $MsvcBin 'lib.exe') /nologo /machine:x64 "/def:$definition" "/out:$(Join-Path $imports $library)" | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "Import library failed for $name" }
}
$csc = Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
& $csc /nologo "/out:$(Join-Path $imports 'pkg-config.exe')" (Join-Path $source 'PkgConfig.cs')
if ($LASTEXITCODE -ne 0) { throw 'Build metadata helper failed.' }
$env:PKG_CONFIG = Join-Path $imports 'pkg-config.exe'
$env:PKG_CONFIG_ALLOW_CROSS = '1'
$env:PATH = (Join-Path $GStreamerRoot 'bin') + ';' + $env:PATH
# MSVC cannot reliably write Cargo's deeply nested output paths below a checkout.
# Keep a unique target directory near TEMP and preserve the caller's environment.
$previousCargoTarget = $env:CARGO_TARGET_DIR
$cargoTarget = Join-Path ([IO.Path]::GetTempPath()) ('hcm-' + [Guid]::NewGuid().ToString('N').Substring(0, 8))
$env:CARGO_TARGET_DIR = $cargoTarget
Push-Location $source
try {
    & cargo build --release --locked -j 2
    if ($LASTEXITCODE -ne 0) { throw 'Neolink source build failed.' }
    & cargo test --release --locked -j 2 -p neolink_core legacy_video
    if ($LASTEXITCODE -ne 0) { throw 'Video routing regression tests failed.' }
    & cargo test --release --locked -j 2 -p neolink_core ack_receive_rate
    if ($LASTEXITCODE -ne 0) { throw 'UDP receive-rate regression tests failed.' }
    Copy-Item (Join-Path $cargoTarget 'release/neolink.exe') (Join-Path $output 'neolink.exe')
} finally {
    Pop-Location
    $env:CARGO_TARGET_DIR = $previousCargoTarget
}
