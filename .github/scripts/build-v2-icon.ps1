# Keep the confirmed Beta37 artwork and generate all three matching variants.
# Do not decode the legacy V1 ICO frames: they are not the approved artwork.
& (Join-Path $PSScriptRoot 'build-logo-variants.ps1')
