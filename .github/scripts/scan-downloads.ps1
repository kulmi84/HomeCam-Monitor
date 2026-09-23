$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$downloads = Join-Path $root 'beta-downloads'
$report = Join-Path $downloads 'DEFENDER-SCAN.txt'
$defender = Join-Path $env:ProgramFiles 'Windows Defender\MpCmdRun.exe'

if (-not (Test-Path $defender)) {
    'Microsoft Defender ist auf dem GitHub-Runner nicht verfügbar; Scan wurde nicht ausgeführt.' |
        Set-Content $report -Encoding UTF8
    exit 0
}

$output = & $defender -Scan -ScanType 3 -File $downloads 2>&1
$exitCode = $LASTEXITCODE
@(
    "Scanzeit (UTC): $([DateTime]::UtcNow.ToString('u'))"
    "Exitcode: $exitCode"
    $output
) | Set-Content $report -Encoding UTF8

if ($exitCode -ne 0) { throw "Microsoft Defender meldet einen Fehler oder Fund (Exitcode $exitCode)." }
