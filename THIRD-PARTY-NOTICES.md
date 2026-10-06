# Fremdkomponenten – HomeCam Monitor V1.0.0

Die proprietäre HomeCam-Lizenz gilt ausschließlich für eigene geschützte HomeCam-Bestandteile. Die Rechte an Fremdkomponenten, einschließlich Änderungen, Weitergabe und korrespondierender Quellen, richten sich nach deren eigenen Lizenzen.

## Geprüfter Stand

Der V1-Paketbau verwendet feste Bezugsadressen und SHA-256 aus `third-party.lock.json`. Versionen und Lizenzberichte werden vor dem Verpacken geprüft. Die folgenden Angaben beziehen sich auf das derzeitige **Prüfpaket**, nicht auf einen bereits freigegebenen Download.

| Komponente | Konkret verwendeter Stand | Lizenz und Nachweis |
|---|---|---|
| mpv | shinchiro, Asset `mpv-x86_64-20261006-git-6c092d978b.7z`; Quellrevision `6c092d978b` | mpv-Haupttexte unter `licenses/mpv/`. Die tatsächliche Gesamtvariante hängt von Buildoptionen und eingebauten Bibliotheken ab; deren vollständiger Quell- und Lizenznachweis ist noch offen. |
| FFmpeg | BtbN, Asset `ffmpeg-N-127203-ga35c879992-win64-lgpl.zip` vom 05.10.2026 | Die konkrete EXE meldet **LGPL Version 3 oder später**. Volltexte unter `licenses/ffmpeg/`; die Ausgabe von `ffmpeg -L` wird mitgeliefert. Nicht pauschal als LGPLv2.1 bezeichnen. |
| .NET / Windows Desktop Runtime | Self-contained Windows x64; im geprüften Build Runtime **8.0.31**, SDK **8.0.425** | Die tatsächlichen Paketversionen werden aus Projektassets und Runtimeabhängigkeiten ermittelt. LICENSE, THIRD-PARTY-NOTICES und Versionsliste aus den passenden NuGet-Runtimepaketen werden dem Download beigefügt. |
| Eingebaute Bibliotheken | Abhängigkeiten der jeweiligen mpv-/FFmpeg-Binaries | Eigene Copyrights und Lizenztexte erforderlich. Die HomeCam-Lizenz beansprucht keine Rechte an ihnen. |

HomeCam startet mpv und FFmpeg als getrennte Programme. Die Fremdprogramme werden nicht unter der proprietären HomeCam-Lizenz angeboten.

## Korrespondierende Quellen

Die vorhandenen Fremdbinary-Downloads enthalten keinen von uns vollständig nachgewiesenen Satz der tatsächlich verwendeten Abhängigkeitsquellen, Patches und Buildskripte. Deshalb bleibt `sourceCoverageVerified` im Lockfile **false** und der finale Paketbau gesperrt. Allgemeine Links auf aktuelle Quellbranches ersetzen diesen Nachweis nicht.

Ein separater Workflow baut die Fremdprogramme mit archivierten tatsächlichen Quellen. Er erhält die beim Build verwendeten, gegebenenfalls gepatchten Quellbäume und sammelt Revisionen, Lizenztexte sowie Buildanweisungen. Dieser neue Build verwendet GPL-Komponenten; sein FFmpeg darf daher nicht mit dem oben beschriebenen LGPL-Prüfpaket gleichgesetzt werden. Vor Übernahme sind die vollständigen Lizenzbedingungen, Quellenabdeckung und Windows-Funktionstests zu prüfen.

Der finale Paketbau verlangt ein passendes `sources-manifest.json`, überprüfte Quellenabdeckung und SHA-256 für die Quellenarchive. Die korrespondierenden Quellen müssen zusammen mit den offiziellen Binärdownloads verfügbar sein und dürfen keiner HomeCam-Zustimmungspflicht unterliegen. GitHub-Prüfartefakte mit begrenzter Aufbewahrungszeit sind kein dauerhafter Release-Quellendownload.

## Paketinhalt und Freigabe

Setup und portable ZIP enthalten die HomeCam-Lizenz, dieses Dokument, Anleitung und Fremdlizenztexte. Das Setup zeigt die HomeCam-Lizenz vor der Installation auf Wunsch an. Pakete mit `-ReviewOnly` tragen eine eindeutige Prüfkennzeichnung.

Noch offen sind der vollständige Quellen- und Dependency-Lizenznachweis sowie die Prüfung der endgültigen Fremdbinaries. Eine vollständige Release-Freigabe wird erst nach diesen Prüfungen dokumentiert; siehe [Release-Prüfung](docs/release-v1.0.0.md).

## Primärquellen

- [mpv Copyright am verwendeten Quellstand](https://github.com/mpv-player/mpv/blob/6c092d978b/Copyright)
- [FFmpeg License and Legal Considerations](https://ffmpeg.org/legal.html)
- [BtbN Buildvarianten am verwendeten Buildstand](https://github.com/BtbN/FFmpeg-Builds/blob/9acad4a9ef1583096af7836cc1e9c8cbcb4d3950/README.md)
- [mpv Windows-Buildrezept am verwendeten Stand](https://github.com/shinchiro/mpv-winbuild-cmake/tree/05a60b3cfd04e3e3b89918f4a27f3dde2935dff2)
- [GNU GPL FAQ zur Zusammenstellung getrennter Programme](https://www.gnu.org/licenses/gpl-faq.en.html#MereAggregation)

Maßgeblich sind die Lizenztexte und Quellen der konkret ausgelieferten Builds.
