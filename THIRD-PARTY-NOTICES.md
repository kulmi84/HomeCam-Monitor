# Fremdkomponenten – HomeCam Monitor V1.0.0

Die proprietäre HomeCam-Lizenz von Marcin Kulmaczewski gilt ausschließlich für eigene geschützte HomeCam-Bestandteile. Fremdprogramme, ihre Abhängigkeiten, Änderungen und korrespondierenden Quellen unterliegen ausschließlich ihren jeweiligen Lizenzen. Ihre Nutzung, Bearbeitung und Weitergabe setzt keine HomeCam-Zustimmung voraus, soweit ihre Lizenzen diese Rechte gewähren.

## Tatsächlicher Paketstand

| Komponente | Quellstand | Lizenz / Nachweise im Paket |
|---|---|---|
| mpv | `6c092d978b73a54f8b945e29f88a4015c7dacd89` | Zusammen mit GPLv3-Abhängigkeiten gebaut; Gesamtbinary GPL-3.0-or-later. `licenses/mpv-*`, `licenses/media-dependencies/mpv/` und Versionsbericht. |
| FFmpeg | `a35c8799920a93b99781eab6e70a5795ee7d182b` | GPL-3.0-or-later, laut tatsächlicher Ausgabe von `ffmpeg -L`. `licenses/FFmpeg-binary-LICENSE.txt` und Binary-Lizenzreport. Kein `enable-nonfree`. |
| .NET / Windows Desktop Runtime | Tatsächliche Paketversionen in `licenses/` | Lizenztexte, THIRD-PARTY-NOTICES und Versionsnachweise aus den verwendeten Runtimepaketen. |
| Weitere Bibliotheken | `licenses/source-inventory.json` | Vollständige gesammelte Copyright-/Lizenztexte unter `licenses/media-dependencies/`, einschließlich ergänzender FreeType-, IJG- und Codec-Hinweise. |

Die früher zur Vorbereitung verwendete BtbN-LGPL-FFmpeg-Variante ist nicht die hier ausgelieferte EXE. Die finalen Fremdprogramme stammen aus unserem [Quellen-Build 37641796309](https://github.com/kulmi84/HomeCam-Monitor/actions/runs/37641796309).

FreeType erlaubt alternativ FTL oder GPLv2-or-later. Für die GPLv3-Zusammenstellung wird GPLv2-or-later gewählt. Beide Texte und die zusätzlichen BDF-/PCF-Hinweise sind enthalten. Weitere Bibliotheken behalten ihre eigenen Lizenzbedingungen; einzelne Copyright-Hinweise werden nicht durch eine pauschale HomeCam-Lizenz ersetzt.

## Korrespondierende Quellen

`HomeCam-Media-corresponding-source.tar.zst` enthält die tatsächlichen verwendeten Quellbäume, Abhängigkeiten, Änderungen und Buildrezepte. `source-inventory.json` dokumentiert die Revisionen. `sources-manifest.json` und `SHA256SUMS.txt` verbinden diese Unterlagen mit den ausgelieferten Binaries.

Das Quellenarchiv wird als eigener Download neben Installer und portabler Version angeboten und nicht installiert. Beim offiziellen Release müssen diese Quellen dauerhaft zusammen mit den Binärdownloads bereitgestellt werden. Zeitlich begrenzte GitHub-Actions-Artefakte dienen bis dahin nur zur Prüfung.

HomeCam startet mpv und FFmpeg als getrennte Programme über Prozessaufrufe und gewöhnliche IPC-/Dateischnittstellen. Es bindet keine libmpv-/libavcodec-Bibliothek in den eigenen Programmcode ein. Die technische Einordnung als Zusammenstellung getrennter Programme und die geprüften Nachweise sind in [der Paketprüfung](docs/third-party-release-audit.md) dokumentiert.

## Primärquellen

- [mpv Copyright am verwendeten Stand](https://github.com/mpv-player/mpv/blob/6c092d978b73a54f8b945e29f88a4015c7dacd89/Copyright)
- [FFmpeg Lizenzhinweise](https://ffmpeg.org/legal.html)
- [Verwendetes Windows-Buildrezept](https://github.com/shinchiro/mpv-winbuild-cmake/tree/05a60b3cfd04e3e3b89918f4a27f3dde2935dff2)
- [GNU GPL FAQ zur Zusammenstellung getrennter Programme](https://www.gnu.org/licenses/gpl-faq.en.html#MereAggregation)

Maßgeblich sind die Lizenztexte und Quellen der konkret ausgelieferten Builds.
