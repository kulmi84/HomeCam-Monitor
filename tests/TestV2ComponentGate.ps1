$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$fixture = Join-Path $root ('reolink-component-cache/gate-test-' + [Guid]::NewGuid().ToString('N'))
$runtime = Join-Path $fixture 'runtime'
$sources = Join-Path $fixture 'sources'
New-Item -ItemType Directory -Force -Path (Join-Path $runtime 'tools/gstreamer'), $sources | Out-Null
$binary = Join-Path $runtime 'component.bin'
$archive = Join-Path $sources 'source.tar'
[IO.File]::WriteAllText($binary, 'expected')
[IO.File]::WriteAllText($archive, 'source')
$gstInventory = Join-Path $fixture 'gst.json'
$licenseInventory = Join-Path $fixture 'licenses.json'
'{"files":[]}' | Set-Content -LiteralPath $gstInventory
'[]' | Set-Content -LiteralPath $licenseInventory
$review = @{
    schemaVersion = 1; componentSourceCoverageVerified = $true
    binaries = @(@{ path = 'component.bin'; sha256 = (Get-FileHash $binary).Hash })
    sources = @(@{ file = 'source.tar'; sha256 = (Get-FileHash $archive).Hash })
    gstreamerInventory = $gstInventory.Substring($root.Length + 1)
    licenseInventory = $licenseInventory.Substring($root.Length + 1)
}
$lock = Join-Path $fixture 'review.json'
$review | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $lock
$gate = Join-Path $root '.github/scripts/verify-v2-component-set.ps1'
function Invoke-Gate { & $gate -Runtime $runtime -SourcesDirectory $sources -AuditLock $lock }
function Expect-Failure([scriptblock]$Action, [string]$Expected) {
    try { & $Action } catch { if ($_.Exception.Message.Contains($Expected)) { return }; throw }
    throw "Expected audit rejection: $Expected"
}
Invoke-Gate | Out-Null
[IO.File]::WriteAllText($binary, 'tampered')
Expect-Failure { Invoke-Gate } 'component.bin'
[IO.File]::WriteAllText($binary, 'expected')
Move-Item -LiteralPath $archive -Destination ($archive + '.saved')
Expect-Failure { Invoke-Gate } 'source.tar'
Move-Item -LiteralPath ($archive + '.saved') -Destination $archive
[IO.File]::WriteAllText((Join-Path $runtime 'tools/gstreamer/unreviewed.dll'), 'extra')
Expect-Failure { Invoke-Gate } 'Unexpected GStreamer'
Write-Output 'Component gate: valid set accepted; tampered binary, missing source and extra runtime rejected.'
