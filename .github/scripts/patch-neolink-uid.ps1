param([Parameter(Mandatory)][string]$Executable,
      [Parameter(Mandatory)][string]$OriginalSource,
      [Parameter(Mandatory)][string]$ModifiedSource)
$ErrorActionPreference = 'Stop'
# HomeCam compatibility change, 2026-10-15. Only this exact upstream binary
# is accepted. No system DNS, hosts file or firewall settings are changed.
$originalHash = '3617f05fd1181fbda4a85a5bc9b3dcc80f157b509fbf1e06819a8b6e467e2823'
$patchedHash = 'fa08400354c8437105bfe01d407c041bd4c9156f4dae63e1598efaded24510c0'
if ((Get-FileHash -LiteralPath $Executable).Hash -ne $originalHash) { throw 'Unexpected Neolink executable; UID patch refused.' }
if ((Get-FileHash -LiteralPath $OriginalSource).Hash -ne 'f305a917abfe0ec4520109425dc2c3d465257aed491efa8d4bd3a7f0cc0a5b2f') { throw 'Unexpected Neolink source archive.' }
$encoding = [Text.Encoding]::GetEncoding(28591)
$bytes = [IO.File]::ReadAllBytes((Resolve-Path -LiteralPath $Executable))
$text = $encoding.GetString($bytes)
$old = 'p2p4.reolink.com'
$replacement = 'p2p1.reolink.com'
$index = $text.IndexOf($old, [StringComparison]::Ordinal)
if ($index -lt 0 -or $text.IndexOf($old, $index + 1, [StringComparison]::Ordinal) -ge 0) { throw 'Expected exactly one hostname occurrence.' }
$patched = $encoding.GetBytes($text.Replace($old, $replacement))
$sha = [Security.Cryptography.SHA256]::Create()
try { $actual = [BitConverter]::ToString($sha.ComputeHash($patched)).Replace('-','').ToLowerInvariant() } finally { $sha.Dispose() }
if ($actual -ne $patchedHash) { throw 'Patched executable hash mismatch.' }

# Ship the complete corresponding modified source and the exact transformation.
Add-Type -AssemblyName System.IO.Compression.FileSystem
[IO.Compression.ZipFile]::OpenRead((Resolve-Path -LiteralPath $OriginalSource)).Dispose()
Copy-Item -LiteralPath $OriginalSource -Destination $ModifiedSource -Force
$archive = [IO.Compression.ZipFile]::Open((Resolve-Path -LiteralPath $ModifiedSource), [IO.Compression.ZipArchiveMode]::Update)
try {
    $entry = @($archive.Entries | Where-Object { $_.FullName.EndsWith('/crates/core/src/bc_protocol/connection/discovery.rs') })
    if ($entry.Count -ne 1) { throw 'Corresponding source file missing.' }
    $reader = [IO.StreamReader]::new($entry[0].Open())
    try { $source = $reader.ReadToEnd() } finally { $reader.Dispose() }
    if ([regex]::Matches($source, [regex]::Escape($old)).Count -ne 1) { throw 'Expected exactly one source hostname.' }
    $name = $entry[0].FullName
    $entry[0].Delete()
    $writer = [IO.StreamWriter]::new($archive.CreateEntry($name).Open(), [Text.UTF8Encoding]::new($false))
    try { $writer.Write($source.Replace($old,$replacement)) } finally { $writer.Dispose() }
    $notice = [IO.StreamWriter]::new($archive.CreateEntry('HOMECAM-MODIFICATION.txt').Open())
    try { $notice.Write('Modified by HomeCam Monitor, 2026-10-15: p2p4.reolink.com is replaced by p2p1.reolink.com in the discovery server table. The former resolves to loopback and can cause a Windows UDP connection reset that terminates discovery. This is an app-local compatibility variant, not an upstream Neolink release. Original upstream source and AGPL license are retained. The accompanying patch-neolink-uid.ps1 verifies and transforms the original Windows release binary; upstream Cargo build instructions also apply to this modified source.') } finally { $notice.Dispose() }
    $script = $archive.CreateEntry('patch-neolink-uid.ps1')
    $stream = $script.Open()
    try { $scriptBytes = [IO.File]::ReadAllBytes($PSCommandPath); $stream.Write($scriptBytes,0,$scriptBytes.Length) } finally { $stream.Dispose() }
} finally { $archive.Dispose() }
[IO.File]::WriteAllBytes((Resolve-Path -LiteralPath $Executable), $patched)
Write-Output 'Neolink homecam-uid1 compatibility change and corresponding source prepared.'
