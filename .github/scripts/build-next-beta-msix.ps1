param([string]$Dotnet = 'dotnet', [string]$RuntimeTemplate = '', [string]$SdkDirectory = '', [switch]$Resume)
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
[xml]$project = Get-Content -LiteralPath (Join-Path $root 'HomeCamMonitor.V2.Beta.csproj') -Raw
$betaProperties = $project.Project.PropertyGroup | Where-Object AssemblyName -eq 'HomeCamMonitor-V2-NextBeta'
$version = [string]$betaProperties.Version
$packageVersion = [string]$betaProperties.FileVersion
if ($version -notmatch '^2\.0\.25-beta\.\d+$' -or $packageVersion -notmatch '^2\.0\.25\.\d+$') { throw 'Explicit NextBeta versions required.' }
$identity = Import-PowerShellDataFile (Join-Path $root 'packaging/store/identity.psd1')
$betaIdentity = $identity.IdentityName + '.NextBeta'
$output = Join-Path (Split-Path $root) "Beta/$version"
$runtime = Join-Path $output 'runtime'
if ((Test-Path -LiteralPath $output) -and -not $Resume) { throw "Preserve existing beta output: $output" }
if (Test-Path -LiteralPath (Join-Path $output "HomeCamMonitor-NextBeta-$packageVersion-x64.msix")) { throw 'A built beta package already exists; increase the explicit beta version instead of overwriting it.' }
New-Item -ItemType Directory -Path $runtime -Force | Out-Null
# Reuse the already extracted, audited components without touching Beta 1.
$previous = if ($RuntimeTemplate) { (Resolve-Path -LiteralPath $RuntimeTemplate).Path } else { Join-Path (Split-Path $root) 'Beta/2.0.25-beta.1/payload' }
if (-not (Test-Path -LiteralPath $previous)) { throw 'Pass -RuntimeTemplate with the extracted, audited V2 2.0.25.0 runtime.' }
if (-not $Resume) { Get-ChildItem -LiteralPath $previous | Where-Object {
    $_.Name -notlike 'HomeCamMonitor-V2-NextBeta*' -and $_.Name -notlike 'HomeCamMonitor-V2-Beta*' -and $_.Name -notlike 'README*' -and
    $_.Name -notin @('HomeCamMonitor.StoreBuild.json', 'AppxManifest.xml', 'AppxSignature.p7x', 'AppxBlockMap.xml', '[Content_Types].xml', 'resources.pri', 'StoreAssets', '.homecam-v2-installed-files', '.homecam-nextbeta-installed-files')
} | Copy-Item -Destination $runtime -Recurse -Force }
& $Dotnet publish (Join-Path $root 'HomeCamMonitor.V2.Beta.csproj') -c Release -r win-x64 --self-contained true -p:NextBeta=true -p:StoreRelease=true -p:NuGetAudit=false --ignore-failed-sources -o $runtime
if ($LASTEXITCODE -ne 0) { throw 'Beta MSIX publish failed.' }
# Derive a beta-only builder from the stable implementation. The stable script,
# manifest template and icon/resource generation remain byte-for-byte intact.
$builder = [IO.File]::ReadAllText((Join-Path $PSScriptRoot 'build-store-package.ps1'))
$builder = $builder.Replace('$root = (Resolve-Path (Join-Path $PSScriptRoot ''../..'')).Path', '$root = ''' + $root.Replace("'", "''") + "'")
$builder = $builder.Replace('HomeCamMonitor-V2-Beta', 'HomeCamMonitor-V2-NextBeta')
$builder = $builder.Replace("Join-Path `$root 'assets/HomeCamMonitor-V2-NextBeta.ico'", "Join-Path `$root 'assets/HomeCamMonitor-V2-Beta.ico'")
$builder = $builder.Replace('$info.ProductVersion -ne $PackageVersion', '$info.ProductVersion -ne ''' + $version + "'")
$builder = $builder.Replace("'HomeCam Monitor V2'", "'HomeCam Monitor V2 Next Beta'")
$builder = $builder.Replace('HomeCamMonitorStartup', 'HomeCamMonitorNextBetaStartup')
$iconTest = [IO.File]::ReadAllText((Join-Path $root 'tests/ExeIconResources.ps1'))
$iconTest = $iconTest.Replace('$root = (Resolve-Path (Join-Path $PSScriptRoot ''..'')).Path', '$root = ''' + $root.Replace("'", "''") + "'")
$iconTest = $iconTest.Replace('$version = & (Join-Path $root ''.github/scripts/get-v2-version.ps1'')', '$version = ''' + $packageVersion + "'")
$iconTest = $iconTest.Replace('$info.ProductVersion -ne $version', '$info.ProductVersion -ne ''' + $version + "'")
$iconTestPath = Join-Path $output 'test-icons.generated.ps1'
[IO.File]::WriteAllText($iconTestPath, $iconTest, [Text.UTF8Encoding]::new($false))
$builder = $builder.Replace('(Join-Path $root ''tests/ExeIconResources.ps1'')', "'$($iconTestPath.Replace("'", "''"))'")
$templateLine = '$template = [IO.File]::ReadAllText((Join-Path $root ''packaging/store/AppxManifest.xml.template''))'
$builder = $builder.Replace($templateLine, $templateLine + "`n" + '$template = $template.Replace(''HomeCamMonitor-V2-Beta'', ''HomeCamMonitor-V2-NextBeta'').Replace(''HomeCam Monitor V2'', ''HomeCam Monitor V2 Next Beta'').Replace(''HomeCamMonitorStartup'', ''HomeCamMonitorNextBetaStartup'')')
$builderPath = Join-Path $output 'build-msix.generated.ps1'
[IO.File]::WriteAllText($builderPath, $builder, [Text.UTF8Encoding]::new($false))
$sdk = if ($SdkDirectory) { (Resolve-Path -LiteralPath $SdkDirectory).Path } else { Join-Path $root 'store-artifacts/build-tools/sdk/bin/10.0.26100.0/x64' }
& $builderPath -PublishDirectory $runtime -IdentityName $betaIdentity -Publisher $identity.Publisher -PublisherDisplayName $identity.PublisherDisplayName -PackageVersion $packageVersion -MakeAppx (Join-Path $sdk 'makeappx.exe')
if (-not $?) { throw 'Beta package build failed.' }
# Locate this uniquely identified output; do not select a stable package.
$candidates = Get-ChildItem -LiteralPath (Join-Path $root 'store-artifacts') -Directory | Where-Object {
    $manifest = Join-Path $_.FullName 'payload/AppxManifest.xml'
    (Test-Path -LiteralPath $manifest) -and ([xml](Get-Content -LiteralPath $manifest -Raw)).Package.Identity.Name -eq $betaIdentity -and
    ([xml](Get-Content -LiteralPath $manifest -Raw)).Package.Identity.Version -eq $packageVersion
}
$stage = $candidates | Sort-Object LastWriteTime -Descending | Select-Object -First 1
if (-not $stage) { throw 'Separate beta staging output not found.' }
$startupTests = Join-Path $output 'startup-check'
& $Dotnet build (Join-Path $root 'tests/StoreStartup/StoreStartup.csproj') -c Release -p:NextBeta=true -p:NuGetAudit=false -o $startupTests --ignore-failed-sources
if ($LASTEXITCODE -ne 0) { throw 'Autostart-Prüfung konnte nicht gebaut werden.' }
& (Join-Path $startupTests 'StoreStartup.exe') --manifest (Join-Path $stage.FullName 'payload/AppxManifest.xml')
if ($LASTEXITCODE -ne 0) { throw 'Autostart-Kennung passt nicht zum Beta-Paketmanifest.' }
Copy-Item -LiteralPath (Join-Path $stage.FullName "HomeCamMonitor-$packageVersion-x64.msix") -Destination (Join-Path $output "HomeCamMonitor-NextBeta-$packageVersion-x64.msix")
Copy-Item -LiteralPath (Join-Path $stage.FullName 'inventory.json') -Destination $output
@{ identity = $betaIdentity; publisher = $identity.Publisher; packageVersion = $packageVersion; productVersion = $version; stage = $stage.FullName } | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $output 'beta-package.json') -Encoding utf8
Write-Output "Beta MSIX prepared in $output; signing and verification required."
