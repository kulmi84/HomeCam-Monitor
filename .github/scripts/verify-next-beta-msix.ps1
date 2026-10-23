param([Parameter(Mandatory=$true)][string]$BetaDirectory, [string]$SdkDirectory = '')
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$beta = (Resolve-Path -LiteralPath $BetaDirectory).Path
$metadata = Get-Content -LiteralPath (Join-Path $beta 'beta-package.json') -Raw | ConvertFrom-Json
$identity = Import-PowerShellDataFile (Join-Path $root 'packaging/store/identity.psd1')
if ($metadata.identity -eq $identity.IdentityName -or $metadata.identity -ne $identity.IdentityName + '.NextBeta') { throw 'Beta identity overlaps stable Store identity.' }
$script = [IO.File]::ReadAllText((Join-Path $PSScriptRoot 'verify-store-package.ps1'))
$script = $script.Replace('$root = (Resolve-Path (Join-Path $PSScriptRoot ''../..'')).Path', '$root = ''' + $root.Replace("'", "''") + "'")
$identityLine = '$identity = Import-PowerShellDataFile (Join-Path $root ''packaging/store/identity.psd1'')'
$script = $script.Replace($identityLine, $identityLine + "`n" + '$identity.IdentityName += ''.NextBeta''')
$script = $script.Replace('HomeCamMonitor-V2-Beta', 'HomeCamMonitor-V2-NextBeta').Replace('HomeCam Monitor V2', 'HomeCam Monitor V2 Next Beta').Replace('HomeCamMonitorStartup', 'HomeCamMonitorNextBetaStartup')
$script = $script.Replace("Join-Path `$root 'assets/HomeCamMonitor-V2-NextBeta.ico'", "Join-Path `$root 'assets/HomeCamMonitor-V2-Beta.ico'")
if (-not $metadata.productVersion) { throw 'Beta product version metadata missing.' }
$script = $script.Replace('$info.ProductVersion -ne $PackageVersion', '$info.ProductVersion -ne ''' + $metadata.productVersion + "'")
$iconTest = Join-Path $beta 'test-icons.generated.ps1'
$script = $script.Replace('(Join-Path $root ''tests/ExeIconResources.ps1'')', "'$($iconTest.Replace("'", "''"))'")
$scriptPath = Join-Path $beta 'verify-msix.generated.ps1'
[IO.File]::WriteAllText($scriptPath, $script, [Text.UTF8Encoding]::new($false))
$package = Join-Path $beta "HomeCamMonitor-NextBeta-$($metadata.packageVersion)-x64.msix"
& $scriptPath -Package $package -Inventory (Join-Path $beta 'inventory.json') -PackageVersion $metadata.packageVersion
if (-not $?) { throw 'Beta package verification failed.' }
$sdk = if ($SdkDirectory) { (Resolve-Path -LiteralPath $SdkDirectory).Path } else { Join-Path $root 'store-artifacts/build-tools/sdk/bin/10.0.26100.0/x64' }
& (Join-Path $sdk 'signtool.exe') verify /pa /v $package
if ($LASTEXITCODE -ne 0) { throw 'Beta signature is not trusted or signature validation failed.' }
Write-Output 'PASS: separate Beta MSIX identity, app versions, resources, media/license inventory and trusted signature.'
