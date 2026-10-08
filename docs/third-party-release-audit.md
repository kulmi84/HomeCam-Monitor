# Technische Quellen- und Lizenzprüfung für V1.0.0

Stand: 08.10.2026. Diese Prüfung erlaubt die Erstellung final benannter Downloadpakete; sie veröffentlicht keinen Release und bestätigt keine fremden Rechte am eigenen HomeCam-Code.

## Exakter Quellen-/Binary-Satz

Geprüfter Quellen-Build: [37641796309](https://github.com/kulmi84/HomeCam-Monitor/actions/runs/37641796309), HomeCam-Commit `cb43bf6cd1f48ac04b4f228026a6e412be042015`, Buildrezept `05a60b3cfd04e3e3b89918f4a27f3dde2935dff2`.

| Datei | SHA-256 |
|---|---|
| mpv.exe | `bb51c0d176ea76c26777cd05f92d65a724ef96253599e5ab3bf0c1af7245ede9` |
| ffmpeg.exe | `0cdeb8e922e75e2f9c39cf85cab79cff2c899df6dc703e77bcb5b849193498d1` |
| HomeCam-Media-corresponding-source.tar.zst | `d4ae852e6f5048e6918a597c538724fb60385eb658f1c93d5ad9ac910acf9d6f` |
| source-inventory.json | `a30900b1d93d66e4c3be169de3b2ac6e7a011aaa44ca63b8b3875d0cfef8f3c6` |

Das Archiv enthält die tatsächlichen, gegebenenfalls gepatchten Quellen einschließlich Dependencies und Buildskripten. Die lokale Archivprüfung ergab 1.020 mpv-, 10.965 FFmpeg-, 71 graphengine-Einträge und 22 Buildnachweise. Der graphengine-Link ist relativ (`../graphengine`); ein absoluter Runner-Pfad wird nicht benötigt. Das Quelleninventar dokumentiert den Zustand bei Sammlung; seine damals noch offene Lizenzprüfung wird durch dieses spätere Dokument ergänzt.

## Paketlizenzen und Programmgrenzen

Die tatsächliche FFmpeg-Ausgabe bestätigt GPLv3-or-later, `enable-gpl` und `enable-version3`, ohne `enable-nonfree`. mpv wird zusammen mit diesen GPLv3-Abhängigkeiten angeboten. FreeType wird unter seiner GPLv2-or-later-Alternative verwendet, die eine GPLv3-Zusammenstellung erlaubt.

276 gesammelte Copyright-/Lizenzdateien werden übernommen. Zusätzlich werden FreeTypes `docs/FTL.TXT`, `docs/GPLv2.TXT`, `src/bdf/README`, `src/pcf/README`, `libjpeg/README.ijg` sowie `aom/PATENTS`, `libvpx/PATENTS` und `libjxl/PATENTS` aus dem exakten Quellenarchiv übernommen. Ihr SHA-256 wird in `licenses/supplemental-licenses.json` dokumentiert. [Windows-Paketprüfung 37672184812](https://github.com/kulmi84/HomeCam-Monitor/actions/runs/37672184812) mit diesen Ergänzungen ist erfolgreich.

HomeCam verwendet externe ProcessStartInfo-Aufrufe, mpv-IPC über Named Pipes sowie gewöhnliche Datei-/Stream-Schnittstellen. Die eigenen nativen Imports betreffen Windows-Systembibliotheken; eine Einbindung von libmpv/libavcodec ist nicht vorhanden. Daraus folgt unsere technische Einordnung als Zusammenstellung eigenständiger Programme, entsprechend den Kriterien der [GNU GPL FAQ](https://www.gnu.org/licenses/gpl-faq.en.html#MereAggregation). Diese Einordnung bezieht sich auf den geprüften Programmstand. Die HomeCam-Lizenz nimmt Fremdprogramme und deren Quellen ausdrücklich von den eigenen Zustimmungspflichten aus.

## Finaler Paketbau und Veröffentlichung

Der Windows-Paketworkflow akzeptiert für final benannte Pakete ausschließlich den oben festgelegten Quellen-Build und alle vier SHA-256-Werte. Erst danach setzt er im temporären Paket-Lockfile `sourceCoverageVerified` und erzeugt das Quellenmanifest. Historische Fremddownloads aus dem Root-Lockfile erhalten dadurch keine nachträgliche Freigabe.

Installer und portable ZIP enthalten Fremdlizenztexte, Versionsnachweise und das Quellenmanifest. Das Quellenarchiv bleibt ein separater Download. Vor einer offiziellen Veröffentlichung müssen Quellenarchiv, Inventar, Manifest und Prüfsummen dauerhaft als Release-Assets bereitgestellt werden. GitHub-Actions-Aufbewahrung von 30 Tagen ist dafür nicht ausreichend.

Marcin hat die Installation und das korrigierte Icon bestätigt. Automatische Windows-Prüfungen decken Oberfläche, Vorlauf, minimierte Vorschau, Setup/Beta-Upgrade und Defender ab. Eine öffentliche Veröffentlichung, ein Tag und ein Merge erfolgen erst nach abschließender Freigabe der konkreten Downloadpakete.
