# V2 minimized playback and the separate MSIX beta

Windows reports a 160×28 client area for the minimized borderless camera
window. Docking the video host to that area shrank the active mpv render target;
restoring the app briefly showed the small image stretching back to normal.
Starting playback while already minimized also explicitly selected that small
client area, even when later minimization had been fixed.

V2 now retains its child layout while minimized, ignores deferred surface
alignment during that state, and uses the normal restore bounds for initial
playback, restart coverage and single-camera refresh. Normal visible layout
and user resizing continue to use the current client area. These corrections
apply to V2, including final/Store builds; V1 behavior is unchanged.

## Separate beta

Build with `NextBeta=true` to select application version `2.0.25-beta.4` and
file/package version `2.0.25.4`. The stable `HomeCamV2Version` is unchanged.
`NextBeta=true` cannot be combined with `FinalRelease=true`.

The beta has its own executable, settings/registry and recording defaults,
MSIX identity (`MarcinKulmaczewski.HomeCamMonitor.NextBeta`), startup task and
taskbar group. It can coexist with the stable Store app. Existing MSIX beta
settings survive beta updates. The stable icon/theme implementation, package
assets and package-build scripts remain unchanged. The beta-specific window
group prevents grouping the two installed apps together.

The beta also retains the prior experimental startup presentation guards
(disabled DWM transitions and native video-host alignment); those remain
exclusive to `NEXT_BETA`. They are not necessary to enable the shared V2
minimized-size correction.

To build an unsigned local MSIX, supply an extracted, audited V2 2.0.25.0 runtime
and a Windows SDK directory containing MakeAppx and MakePRI:

```powershell
.github/scripts/build-next-beta-msix.ps1 -RuntimeTemplate C:/runtime/v2-2.0.25.0 -SdkDirectory 'C:/WindowsSDK/bin/10.0.26100.0/x64'
```

Outputs are written to `../Beta/<application-version>/`. The script refuses
to overwrite an existing package. Local signing is separate and must use
a certificate matching the manifest publisher; certificates and private keys
are not part of the repository. To verify a signed test package:

```powershell
.github/scripts/verify-next-beta-msix.ps1 -BetaDirectory ../Beta/2.0.25-beta.4 -SdkDirectory 'C:/WindowsSDK/bin/10.0.26100.0/x64'
```

The same prepared, audited runtime is reused; no mutable latest media build is
downloaded by these scripts. No Store submission or release publication is
performed.

## Validation

The Windows workflow downloads the pinned `v2.0.25.0` runtime and validates its
published SHA-256 checksum. Synthetic local video exercises real mpv/D3D11
playback, without saved camera credentials:

- `--initial-player <mpv.exe> <fixture>` checks playback started minimized
  before its first restore.
- `--restore-player <mpv.exe> <fixture> <output>` checks repeated manual and
  motion-path restores, including a pending layout while minimized.
- `--restore-layout` checks native window messages and host dimensions.
- Existing icon/theme, fresh-storage and native-presentation tests remain in
  the focused beta job; the regular Windows build continues to run separately.

Before the fix, the real initial player reported 160×28 instead of 640×360.
After the fix, host and player retain 640×360 across initial and later restores.
Manual testing of MSIX beta 4 confirmed the first-call and restore fixes.
