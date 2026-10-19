# Fremdkomponentenprüfung V2.0.0

Stand: 18. Oktober 2026. **Der technische Quellen- und Lizenznachweis ist abgeschlossen und [V2.0.0 ist veröffentlicht](https://github.com/kulmi84/HomeCam-Monitor/releases/tag/v2.0.0).** Die drei vollständigen Quellenarchive, Manifest und Prüfsummen stehen dauerhaft gemeinsam mit Installer und ZIP bereit. Alle zehn veröffentlichten Anhänge wurden anhand ihrer Größen und SHA-256-Digests mit den geprüften lokalen Dateien abgeglichen.

## Konkreter Dateisatz

Für den öffentlichen Kandidaten wurden mpv und FFmpeg bewusst durch den vollständig belegten Mediensatz aus dem V1-Quellen-Build 37641796309 ersetzt. Beide beruhen auf denselben mpv-/FFmpeg-Quellrevisionen wie die bisherigen V2-Werkzeuge, besitzen aber andere Abhängigkeiten und Binärprüfsummen. Der Wechsel wurde in einem separaten Kandidaten getestet. Die installierte Anwendung und Kameraeinstellungen wurden nicht verändert.

| Datei | SHA-256 |
|---|---|
| mpv.exe | `bb51c0d176ea76c26777cd05f92d65a724ef96253599e5ab3bf0c1af7245ede9` |
| ffmpeg.exe | `0cdeb8e922e75e2f9c39cf85cab79cff2c899df6dc703e77bcb5b849193498d1` |
| tools/neolink/neolink.exe | `e70dbe88731ed37313004d9dfffd0b3862e30b1f353571e51a0da6a387b99bc6` |
| OpenCvSharp.dll | `a26fd4bfff04fe2a351b3b0b417cc9847ef8e10721a8e484e3b33850f510b509` |
| OpenCvSharpExtern.dll | `41e6a418637b7992814a668272e3197306f75c01b151ddb24ac2de183fb9c3f3` |

Der [Komponenten-Lock](../v2-component-review.lock.json) verbindet diese Binaries mit den Quellen und den konkreten Lizenzdateien. Frühere lokale V2-Pakete mit mpv `b519…` und FFmpeg `859f…` erhalten keine rückwirkende Freigabe. Der Paketbau wählt anhand der tatsächlichen Medienhashes entweder die neuen V2-Hinweise oder die getrennten historischen V2-Hinweise. Die V1-Hinweise bleiben unverändert.

## Vollständige Quellenarchive

| Archiv | SHA-256 |
|---|---|
| HomeCam-Media-corresponding-source.tar.zst | `d4ae852e6f5048e6918a597c538724fb60385eb658f1c93d5ad9ac910acf9d6f` |
| neolink-0.6.2-homecam-stream3-corresponding-source.zip | `77ca102539fe547d7fd87b9845296db7595ae172d6cb2c1eed9c72a5b608d51d` |
| HomeCam-GStreamer-1.26.11-corresponding-source.tar.zst | `916c3baab9150627c39b522b53a1600984b544580a6123be276e103cdcde2bc1` |

Die Archive wurden erzeugt bzw. erneut geladen, per SHA-256 geprüft und auf Lesbarkeit geprüft. Sie werden als separate Downloads neben den Binärpaketen angeboten; eine Installation der Quellen ist nicht nötig. Das Medienarchiv und dessen Inventar stimmen mit dem bereits geprüften [V1-Dateisatz](third-party-release-audit.md) überein. Die unterschiedlichen Herkunftsnachweise werden nicht vermischt.

### Neolink

Das vollständige ZIP stimmt in **202 ursprünglichen Quelldateien** mit dem festgelegten Upstream-Archiv plus dem exakten HomeCam-Patch und Cargo-Lock überein. Die hinzugefügte Cargo-Konfiguration verwendet einen relativen Vendor-Pfad. Alle **294 Registry-Pakete und 16.640 Dateien** sind vorhanden und checksumgeprüft. Original-Lizenz, Änderungen, Buildskripte und Buildhinweise sind enthalten. [Quellgleichheit](audit/v2-2.0.0/neolink-source-equivalence.json), [Vendor-Nachweis](audit/v2-2.0.0/neolink-vendor.json), [Archivzuordnung](audit/v2-2.0.0/neolink-source-archive.json).

Neolink und seine HomeCam-Änderungen stehen unter AGPL-3.0-or-later. Die eigene HomeCam-Lizenz nimmt Fremdprogramme, Änderungen, Abhängigkeiten und korrespondierende Quellen ausdrücklich aus. Neolink ist ein separater Prozess und verwendet die mitgelieferten GStreamer-DLLs.

### GStreamer und weitere Laufzeitbestandteile

Alle **1.268 Runtime-Dateien** stimmen bytegenau mit einer frischen Extraktion des offiziellen, checksumgeprüften MSI 1.26.11 überein. Alle **543 DLLs**, einschließlich 261 GStreamer-Plugins und 151 frei0r-Filtern, wurden eindeutig den offiziellen Cerbero-Rezepten zugeordnet. [MSI-Abgleich](audit/v2-2.0.0/gstreamer-official-msi-match.json), [DLL-Zuordnung](audit/v2-2.0.0/gstreamer-runtime-mapping.json).

Das vollständige Quellenpaket enthält:

- alle 85 Tarball-Quellen der aufgelösten 95-Rezepte-Menge, geprüft gegen die offiziellen Rezept-SHA-256;
- zusätzlich GCC 14.2.0 und MinGW-w64 12.0.0, ebenfalls checksumgeprüft;
- acht vollständige Git-Quellarchive, einschließlich Cerbero Commit `cebb549ade5b73048fc29be5a45332246f1c14f6`, libffi, gst-plugins-rs und der fünf Git-Abhängigkeitsrepositorys;
- die exakten Revisionen aller 68 Git-basierten Rust-Pakete sowie **983 eindeutige originale Registry-Quellarchive** aus den Cargo-Locks von gst-plugins-rs und librsvg, jeweils gegen die Lock-Prüfsumme geprüft;
- Original-Lizenzen, Änderungen, Cerbero-Rezepte, Toolchain-Bauregeln und Buildanleitung.

Die drei GCC-/MinGW-Runtime-DLLs wurden zusätzlich bytegenau dem offiziellen Toolchain-Archiv zugeordnet. libgcc und libstdc++ verwenden GPLv3 mit GCC Runtime Library Exception; winpthreads besitzt seine eigenen MIT/BSD-Bedingungen. [Runtime-Provenienz](audit/v2-2.0.0/mingw-binary-provenance.json), [Registry-Archive](audit/v2-2.0.0/gstreamer-registry-archives.json), [Git-Zuordnung](audit/v2-2.0.0/gstreamer-git-vendor-mapping.json).

GStreamer ist in diesem Paket keine ausschließlich unter LGPL stehende Zusammenstellung. Die enthaltenen GPL-, LGPL-, MPL-, MIT-/BSD-Komponenten behalten ihre eigenen Bedingungen. Plugin-Metadaten allein reichen zur Einordnung nicht aus: Beispielsweise registriert sich RTP-ONVIF als `unknown`, während seine konkreten Quellen LGPL-2.1-or-later nennen. `gstpython.dll` ließ sich wegen der optionalen Python-Umgebung nicht laden, ist aber über MSI, Rezept und vollständige gst-python-Quellen erfasst. Es wurde kein systemweites Python nachinstalliert.

Aus den Runtime-Quellen wurden **324** Lizenz-/Copyright-/NOTICE-Dateien übernommen, aus den Git- und Rust-Quellen zusätzlich **2.499**. Alle ursprünglichen Texte bleiben außerdem in den vollständigen Quellenarchiven enthalten. Die gesamte Kandidaten-Lizenzsammlung einschließlich Medien-, NuGet- und OpenCV-Hinweisen umfasst **3.200 Dateien**, deren konkrete Hashes im [Lizenzinventar](audit/v2-2.0.0/approved-license-inventory.json) stehen.

### .NET, NuGet und OpenCV

Der tatsächliche veröffentlichte Dateisatz wurde mit den verwendeten NuGet-Paketen abgeglichen: **241 Runtime-/Bibliotheksdateien** stimmen mit den Originalpaketinhalten überein. System.Memory 4.6.3 und ProtectedData 8.0.0 sind im Abhängigkeitsgraphen erfasst; in der .NET-8-Ausgabe werden dafür Runtime-Implementierungen verwendet. Die ungenutzte OpenCV-FFmpeg-Plugin-DLL bleibt ausdrücklich ausgeschlossen. Paketversionen, Archivprüfsummen und NuGet-Content-Hashes sind getrennt dokumentiert, da NuGet beim Content-Hash Signaturmetadaten ausnimmt. [Payload-Abgleich](audit/v2-2.0.0/nuget-payload-verification.json), [Paketnachweise](audit/v2-2.0.0/nuget-runtime-evidence.json), [NuGet-Definition](https://github.com/NuGet/Home/wiki/Nupkg-Metadata-File).

Die vorhandenen OpenCV-/IPP-Hinweise wurden um die bisher fehlenden Intel-ITT-Alternativtexte und OpenEXR-Autorenhinweise ergänzt. Für ITT wird BSD-3-Clause gewählt; die ursprüngliche Alternative GPL-2.0-only bleibt ebenfalls dokumentiert. Der konkrete native Buildbericht ist enthalten. Für .NET/WindowsDesktop 8.0.32 wurden die Runtime-Lizenzen und Versionsdateien sowie WPF-/WinForms-Fremdhinweise derselben Version ergänzt. System.Memorys MIT-Text stammt aus der im NuGet-Paket festgelegten Repository-Revision.

## Funktion und Freigabeschranke

Der Kandidat bestand die vollständige V2-Funktionsprüfung, wirksame UID-/TCP-/No-buffer-Optionen des tatsächlichen mpv, lokale MOG2-Erkennung mit echtem FFmpeg/OpenCV, echte H.264-Wiedergabe sowie Aufnahme/Remux und PNG-Snapshot. Der unveränderte Neolink-Dienst akzeptiert die generierte Konfiguration und startet RTSP mit der privaten Runtime. Die Tests verwenden synthetische Daten und ersetzen keinen erneuten Latenzvergleich mit einer entfernten echten Kamera.

`.github/scripts/verify-v2-component-set.ps1` prüft die fünf zentralen Binärhashes, sämtliche 1.268 GStreamer-Dateien, alle 3.200 Lizenzdateien und alle drei Quellenarchive. Zusätzliche Runtime-Dateien, fehlende Quellen oder veränderte Dateien werden abgelehnt. Der Paketbau kann mit `-AuditedSourcesDirectory` an diese Prüfung gebunden und in separate Ausgabeordner geleitet werden.

**Öffentliche Bereitstellung abgeschlossen:** Die drei exakten Quellenarchive, Manifest und Prüfsummen sind zusammen mit Installer und ZIP im [V2.0.0-Release](https://github.com/kulmi84/HomeCam-Monitor/releases/tag/v2.0.0) verfügbar. Tag und geprüfter Quellstand: `351d29fe4ad70ffa51b1e936227f24230391b16b`. Der Quellen-/Lizenznachweis gilt für diesen konkreten Dateisatz. Ein bitidentischer Neubau aller Fremdbinaries und eine allgemeine rechtliche Garantie werden nicht behauptet. Veröffentlichungsangaben in den unveränderten Paket-Snapshots dokumentieren den Stand vor dem Upload; maßgeblich für die erfolgte Veröffentlichung ist dieser Bericht.
