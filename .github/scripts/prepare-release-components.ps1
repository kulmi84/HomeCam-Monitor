param([string]$Publish = 'publish', [string]$Cache = 'component-cache')
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$publishPath = Join-Path $root $Publish
$cachePath = Join-Path $root $Cache
$lock = Get-Content (Join-Path $root 'third-party.lock.json') -Raw | ConvertFrom-Json
New-Item -ItemType Directory -Force -Path $cachePath, (Join-Path $publishPath 'licenses') | Out-Null
foreach ($name in @('mpv', 'ffmpeg')) {
    $entry = $lock.$name
    $extension = if ($name -eq 'mpv') { '.7z' } else { '.zip' }
    $archive = Join-Path $cachePath ($name + $extension)
    if (-not (Test-Path -LiteralPath $archive)) { Invoke-WebRequest $entry.url -OutFile $archive }
    if ((Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash -ne $entry.sha256) {
        throw "$name-Prüfsumme stimmt nicht. Kein Paket mit anderen Binaries erzeugen."
    }
    $expanded = Join-Path $cachePath $name
    New-Item -ItemType Directory -Force -Path $expanded | Out-Null
    if ($name -eq 'mpv') {
        & 7z x $archive "-o$expanded" -y | Out-Null
        if ($LASTEXITCODE -ne 0) { throw 'mpv-Paket konnte nicht entpackt werden.' }
    } else { Expand-Archive -LiteralPath $archive -DestinationPath $expanded -Force }
    $exe = @(Get-ChildItem -LiteralPath $expanded -Filter "$name.exe" -Recurse)
    if ($exe.Count -ne 1) { throw "Genau eine $name.exe erwartet." }
    Copy-Item -LiteralPath $exe[0].FullName -Destination (Join-Path $publishPath "$name.exe")
    # mpv.exe is a Windows GUI-subsystem binary; explicitly wait and redirect
    # its CLI output rather than relying on PowerShell's native pipeline.
    $stdout = Join-Path $cachePath "$name-stdout.txt"
    $stderr = Join-Path $cachePath "$name-stderr.txt"
    $versionArgument = if ($name -eq 'mpv') { '--version' } else { '-version' }
    $process = Start-Process -FilePath $exe[0].FullName -ArgumentList $versionArgument -WindowStyle Hidden -RedirectStandardOutput $stdout -RedirectStandardError $stderr -Wait -PassThru
    $versionOutput = @(Get-Content -LiteralPath $stdout) + @(Get-Content -LiteralPath $stderr)
    if ($process.ExitCode -ne 0 -or ($versionOutput -join "`n") -notmatch [regex]::Escape($entry.version)) {
        throw "$name-Version stimmt nicht mit third-party.lock.json überein."
    }
    $versionOutput | Set-Content (Join-Path $publishPath "licenses/$name-version.txt") -Encoding utf8
    if ($name -eq 'ffmpeg') {
        $copyright = @(Get-ChildItem -LiteralPath $expanded -Filter LICENSE.txt -Recurse)
        if ($copyright.Count -ne 1) { throw 'FFmpeg-Lizenz fehlt oder ist mehrdeutig.' }
        Copy-Item -LiteralPath $copyright[0].FullName -Destination (Join-Path $publishPath 'licenses/FFmpeg-binary-LICENSE.txt')
        $licenseOutput = & $exe[0].FullName -L 2>&1
        if (($licenseOutput -join "`n") -notmatch 'Lesser General Public License' -or
            ($licenseOutput -join "`n") -notmatch 'version 3') { throw 'FFmpeg-Lizenz stimmt nicht mit der geprüften LGPLv3-Variante überein.' }
        $licenseOutput | Set-Content (Join-Path $publishPath 'licenses/FFmpeg-binary-license-report.txt') -Encoding utf8
    }
}
Copy-Item (Join-Path $root 'licenses/*') (Join-Path $publishPath 'licenses') -Recurse -Force
foreach ($file in @('LICENSE.txt', 'THIRD-PARTY-NOTICES.md', 'CHANGELOG.md', 'third-party.lock.json')) {
    Copy-Item -LiteralPath (Join-Path $root $file) -Destination $publishPath
}
# Read the exact runtime-pack versions used by this published application.
$deps = Get-Content (Join-Path $publishPath 'HomeCamMonitor.deps.json') -Raw | ConvertFrom-Json
$assets = Get-Content (Join-Path $root 'obj/project.assets.json') -Raw | ConvertFrom-Json
$packageRoots = @($assets.packageFolders.PSObject.Properties.Name)
$runtimePacks = @($deps.libraries.PSObject.Properties.Name | Where-Object { $_ -like 'runtimepack.*' })
foreach ($pack in $runtimePacks) {
    $parts = $pack -replace '^runtimepack\.', '' -split '/'
    $package = $parts[0].ToLowerInvariant()
    $version = $parts[1]
    $folder = $packageRoots | ForEach-Object { Join-Path $_ "$package/$version" } | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
    if (-not $folder) { throw "Runtime-Lizenzpaket fehlt: $pack" }
    $license = @(Get-ChildItem -LiteralPath $folder -File | Where-Object { $_.Name -match '^LICENSE(\.TXT)?$' })
    if ($license.Count -ne 1) { throw "Runtime-Lizenz fehlt: $pack" }
    Copy-Item -LiteralPath $license[0].FullName -Destination (Join-Path $publishPath "licenses/$package-$version-LICENSE.txt")
    foreach ($notice in Get-ChildItem -LiteralPath $folder -File | Where-Object { $_.Name -match 'THIRD-PARTY|versions\.txt$' }) {
        Copy-Item -LiteralPath $notice.FullName -Destination (Join-Path $publishPath "licenses/$package-$version-$($notice.Name)")
    }
    "$package $version" | Add-Content (Join-Path $publishPath 'licenses/runtime-versions.txt') -Encoding utf8
}
