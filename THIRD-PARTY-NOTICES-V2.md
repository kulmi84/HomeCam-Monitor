# Fremdkomponenten – HomeCam Monitor V2.0.0

Die HomeCam-Lizenz gilt ausschließlich für eigene HomeCam-Bestandteile. Fremdprogramme, Bibliotheken, Änderungen und korrespondierende Quellen behalten ihre jeweiligen Lizenzbedingungen und Rechte. Ihre Nutzung, Bearbeitung und Weitergabe erfordert keine HomeCam-Zustimmung, soweit die jeweiligen Lizenzen diese Rechte gewähren. Maßgeblich sind die Originaltexte unter `licenses/`.

Dieser Hinweis gilt für den V2-Kandidaten mit dem belegten GPL-Mediensatz. Frühere lokale V2-Pakete mit abweichenden mpv-/FFmpeg-Dateien sind im getrennten Hinweis `THIRD-PARTY-NOTICES-V2-LOCAL-LEGACY.md` beschrieben. Der Paketbau unterscheidet die Dateisätze anhand ihrer SHA-256-Werte.

| Komponente | Tatsächlicher Stand | Bedingungen und Nachweise |
|---|---|---|
| mpv | SHA-256 `bb51c0d176ea76c26777cd05f92d65a724ef96253599e5ab3bf0c1af7245ede9`, Quellrevision `6c092d978b73a54f8b945e29f88a4015c7dacd89` | GPL-3.0-or-later für diese Zusammenstellung. Originaltexte und Abhängigkeitshinweise unter `licenses/media-dependencies/`. |
| FFmpeg | SHA-256 `0cdeb8e922e75e2f9c39cf85cab79cff2c899df6dc703e77bcb5b849193498d1`, Quellrevision `a35c8799920a93b99781eab6e70a5795ee7d182b` | GPL-3.0-or-later, enable-gpl und enable-version3, ohne enable-nonfree. Tatsächlicher Lizenzbericht unter `licenses/`. FreeType wird unter seiner GPLv2-or-later-Alternative verwendet. |
| Neolink | 0.6.2, HomeCam stream3; upstream `e47a0d5734b0a06b53bd8d78be9ca2f6b8396480`; Binary SHA-256 `e70dbe88731ed37313004d9dfffd0b3862e30b1f353571e51a0da6a387b99bc6` | AGPL-3.0-or-later. Separates modifiziertes Programm; vollständige Änderungen, Buildanleitung und Lizenz im Neolink-Quellpaket. Registry-Abhängigkeiten behalten ihre eigenen Lizenzen. |
| GStreamer | 1.26.11, MSVC x86-64; offizieller MSI SHA-256 `31cbc21fa0950b5c1e79c80959b2799805cb05a7a35953a13a9f790776137605` | Unterschiedliche Komponentenlizenzen, darunter LGPL, GPL, MPL, MIT/X11 und BSD. GPL-Plugins und weitere Filter sind enthalten. Einzelne Plugin-Selbstauskünfte sind unvollständig; entscheidend sind die konkreten Quellen und Originaltexte. |
| GStreamer: GCC-/MinGW-Laufzeit | GCC 14.2.0, MinGW-w64 12.0.0 | libgcc/libstdc++ unter GPLv3 mit GCC Runtime Library Exception; winpthreads unter seinen MIT/BSD-Bedingungen. Originalquellen, Ausnahmen und Buildrezepte im GStreamer-Quellpaket. |
| OpenCvSharp4 / runtime.win | 4.13.0.20260627; Wrapper `b161e7e012f5101f6d5dc68a835c59db6cc88b18` | Apache-2.0. OpenCV/contrib 4.13.0 und die eingebundenen Fremdkomponenten behalten ihre eigenen Bedingungen. Lizenz-/NOTICE-Dateien, Intel IPP/IW und tatsächliche Buildinformationen unter `licenses/opencvsharp-4.13.0/`. Für Intel ITT wird die BSD-3-Clause-Alternative gewählt; beide ursprünglichen Alternativtexte werden erhalten. Die ungenutzte OpenCV-FFmpeg-Plugin-DLL wird nicht ausgeliefert. |
| MQTTnet | 4.3.7.1207 | MIT; `licenses/MQTTnet-LICENSE.txt`. |
| System.Security.Cryptography.ProtectedData | 8.0.0 | MIT; ursprüngliche Lizenz und Fremdhinweise unter `licenses/nuget/` und `licenses/ProtectedData-LICENSE.txt`. |
| System.Memory | 4.6.3, transitive NuGet-Abhängigkeit | MIT; `licenses/System.Memory-LICENSE.txt`. Die .NET-8-Runtime liefert die zur Laufzeit verwendete Implementierung; dieses NuGet-Paket fügt dem veröffentlichten .NET-8-Dateisatz keine eigene DLL hinzu. |
| .NET / Windows Desktop Runtime | 8.0.32, win-x64 | Original-Lizenzen, Runtime-Versionsdateien und Fremdhinweise unter `licenses/nuget/`, einschließlich WPF-/WinForms-Hinweisen aus den entsprechenden 8.0.32-Quellen. |

## Korrespondierende Quellen

Diese vollständigen Quellenpakete gehören dauerhaft neben Installer und portable ZIP:

- `HomeCam-Media-corresponding-source.tar.zst`: tatsächliche mpv-/FFmpeg-Quellen, Abhängigkeiten, Änderungen und Buildrezepte des Quellen-Builds 37641796309.
- `neolink-0.6.2-homecam-stream3-corresponding-source.zip`: exakter modifizierter Neolink-Quellbaum, Cargo-Lock, alle 294 Registry-Abhängigkeiten, Original-Lizenzen und Buildhinweise.
- `HomeCam-GStreamer-1.26.11-corresponding-source.tar.zst`: Runtime-Quellen und Abhängigkeiten, Git-Quellen an den festgelegten Revisionen, GCC-/MinGW-Quellen sowie Cerbero-Rezepte, Änderungen und Buildanleitungen.

Das Quellenmanifest und SHA-256-Prüfsummen verbinden die Archive mit dem konkreten Paket. Die Quellen sind separate Downloads und müssen nicht mitinstalliert werden. Zeitlich begrenzte CI-Artefakte ersetzen ihre dauerhafte Bereitstellung beim öffentlichen Release nicht.

HomeCam startet mpv, FFmpeg und Neolink als eigenständige Prozesse und verwendet gewöhnliche IPC-/Stream-Schnittstellen. Neolink lädt die GStreamer-Bibliotheken. Die HomeCam-Lizenz schränkt die Rechte an diesen Programmen und Bibliotheken nicht ein. Der [V2-Prüfbericht](docs/third-party-release-audit-v2.md) dokumentiert den konkreten Paketstand und die technischen Nachweise. Eine bitidentische Rekonstruktion sämtlicher Fremdbinaries wird nicht behauptet.
