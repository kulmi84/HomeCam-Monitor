# Fremdkomponenten – HomeCam Monitor V2.0.0

Die HomeCam-Lizenz gilt ausschließlich für eigene HomeCam-Bestandteile. Fremdprogramme, Bibliotheken, Änderungen und ihre Quellen behalten ihre jeweiligen Lizenzen und Rechte. Maßgeblich sind die Originaltexte unter `licenses/`.

## Geprüfter lokaler Paketstand

Dieser Hinweis beschreibt die derzeitige V2-Zusammenstellung. Er ist keine Freigabe für deren öffentlichen Vertrieb. Der [V2-Prüfbericht](docs/third-party-release-audit-v2.md) nennt noch offene Nachweise. Der V1-Prüfbericht gilt ausschließlich für die dort festgelegten Binärdateien.

| Komponente | Stand | Lizenz / Nachweise |
|---|---|---|
| FFmpeg | N-127203-ga35c879992-20261005, SHA-256 `859f02a1b007989d3642d0702850030e5bfd169e647786bde6064a1acf578d05` | Tatsächliches `ffmpeg -L`: LGPL-3.0-or-later. Dieser Build ist nicht der geprüfte V1-GPL-Build. |
| mpv | SHA-256 `b519b1f7b616a79c73877f857d6448da4f3544e7e3405bac41cbacae4665f766` | Lizenztexte und Copyright unter `licenses/mpv-*`; vollständige Quell-/Buildzuordnung dieses konkreten Binärstands noch offen. |
| Neolink | 0.6.2, HomeCam stream3; upstream `e47a0d5734b0a06b53bd8d78be9ca2f6b8396480` | AGPL-3.0-or-later; eigenständiges, modifiziertes Programm. Quellarchiv unter `licenses/neolink-0.6.2-homecam-stream3-source.zip`; Änderungshinweise in `compat/neolink-stream3/README.md` im HomeCam-Quellpaket. |
| GStreamer | 1.26.11, MSVC x86-64 | Unterschiedliche Komponentenlizenzen: LGPL, GPL, MPL, MIT/X11 und BSD. Nicht das gesamte ausgelieferte Pluginpaket ist ausschließlich LGPL. Vollständige Quellen-/Lizenzzuordnung noch offen. |
| OpenCvSharp4 / runtime.win | 4.13.0.20260627; Wrapper `b161e7e012f5101f6d5dc68a835c59db6cc88b18` | Apache-2.0; OpenCV 4.13.0, contrib und eingebundene Fremdkomponenten behalten ihre eigenen Bedingungen. Originaltexte, NOTICE, Intel IPP/IW 2022.2.0 und Buildinformationen unter `licenses/opencvsharp-4.13.0/`. |
| MQTTnet | 4.3.7.1207 | MIT; `licenses/MQTTnet-LICENSE.txt`. |
| System.Security.Cryptography.ProtectedData | 8.0.0 | MIT; `licenses/ProtectedData-LICENSE.txt`. |
| System.Memory | 4.6.3, transitive NuGet-Abhängigkeit | MIT; als Abhängigkeit im tatsächlichen `.deps.json` erfasst. Paket-/Runtimezuordnung im V2-Audit berücksichtigen. |
| .NET / Windows Desktop Runtime | 8.0.32, win-x64 | Original-Lizenzen und Third-Party-Hinweise aus den tatsächlichen Runtimepaketen; Vollständigkeitsprüfung für V2 noch offen. |

HomeCam startet mpv, FFmpeg und Neolink als getrennte Prozesse. Neolink verwendet die mitgelieferten GStreamer-DLLs. Die Originalbedingungen der jeweiligen Programme und Bibliotheken werden dadurch nicht ersetzt.

Korrespondierende Quellen müssen beim öffentlichen Release dauerhaft neben den Binärdownloads angeboten und über Prüfsummen dem konkreten Paket zugeordnet werden. Ein Lizenzhinweis oder ein Link auf einen veränderlichen Hauptzweig ersetzt diese Zuordnung nicht.

Primärquellen: [Neolink](https://github.com/QuantumEntangledAndy/neolink/tree/e47a0d5734b0a06b53bd8d78be9ca2f6b8396480), [GStreamer-Lizenzen](https://gstreamer.freedesktop.org/documentation/frequently-asked-questions/licensing.html), [Cerbero 1.26.11](https://gitlab.freedesktop.org/gstreamer/cerbero/-/tree/1.26.11), [FFmpeg](https://ffmpeg.org/legal.html).
