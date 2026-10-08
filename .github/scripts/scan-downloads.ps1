param(
    [string]$DownloadsDirectory = 'beta-downloads',
    [string]$ReportName = 'HomeCamMonitor-Beta-DEFENDER-SCAN.txt'
)
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$downloads = Join-Path $root $DownloadsDirectory
$report = Join-Path $env:RUNNER_TEMP $ReportName
$defender = Get-ChildItem (Join-Path $env:ProgramData 'Microsoft\Windows Defender\Platform') -Directory -ErrorAction SilentlyContinue |
    Sort-Object Name -Descending |
    ForEach-Object { Join-Path $_.FullName 'MpCmdRun.exe' } |
    Where-Object { Test-Path $_ } |
    Select-Object -First 1
if (-not $defender) {
    $fallback = Join-Path $env:ProgramFiles 'Windows Defender\MpCmdRun.exe'
    if (Test-Path $fallback) { $defender = $fallback }
}
if (-not $defender) { throw 'Microsoft Defender ist auf dem GitHub-Runner nicht verfügbar.' }

$output = & $defender -Scan -ScanType 3 -File $downloads 2>&1
$exitCode = $LASTEXITCODE
Write-Host ($output -join [Environment]::NewLine)
@(
    "Scanzeit (UTC): $([DateTime]::UtcNow.ToString('u'))"
    "Exitcode: $exitCode"
    $output
) | Set-Content $report -Encoding UTF8

if ($exitCode -ne 0) { throw "Microsoft Defender meldet einen Fehler oder Fund (Exitcode $exitCode)." }
