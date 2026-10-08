# Release-Prüfung HomeCam Monitor V1.0.0

Stand: 07.10.2026. Kein finaler GitHub Release und kein V1.0.0-Tag veröffentlicht.

## Ausgangsbasis und Vorschlag

Die V1.0.0-Vorbereitung liegt in [PR #2](https://github.com/kulmi84/HomeCam-Monitor/pull/2) auf Basis von Beta 22, Commit `7041045392c17e8677bee127b3a76e1b8a9b1cfb`. Der ältere `main`-Stand enthält weiterhin Stable 0.1.2 und eine ältere Beta. Er wurde nicht verändert.

Das Stable-Projekt baut jetzt den vollständigen festgelegten V1-Funktionsumfang. Das interne Compilerkennzeichen `BETA` aktiviert dabei den vorhandenen Code; `RELEASE_V1` wählt die neutrale Produktbezeichnung und das originale HomeCam-Icon aus der Beta. Bestehende Funktionen und die Beta-Projektdatei bleiben erhalten. Neue Funktionen gehören zur V2/Beta-Linie.

## Erledigt und geprüft

- [x] Anwendung, Assembly- und Dateiversion: 1.0.0 / 1.0.0.0.
- [x] Anwendung: `HomeCamMonitor.exe`; Setup: `HomeCamMonitor-v1.0.0-Setup.exe`; portable ZIP: `HomeCamMonitor-v1.0.0-win-x64.zip`.
- [x] Vollständige Beta-22-Funktionen im V1-Projekt; Originales HomeCam-Icon aus der Beta als Fenster- und Setup-Icon.
- [x] Persönliche HA-Vorgabe entfernt; neue Einstellungen enthalten eine leere HA-Adresse. Gespeicherte Adressen bleiben erhalten.
- [x] Bestehende Beta-Installationen erkannt; alte Installationslisten beim Update berücksichtigt. Eigene Dateien und Einstellungen bleiben erhalten. Die bisherigen internen Settings-/Registry-Pfade dienen weiter der Kompatibilität.
- [x] Proprietären Lizenztext aus dem abgestimmten Entwurf vervollständigt, Rechteinhaber **Marcin Kulmaczewski**. Keine erfundene pauschale Haftungsfreistellung. Lizenz vor Installation über **Lizenz anzeigen …** lesbar.
- [x] README, Anleitung und CHANGELOG auf den aktuellen V1-Stand gebracht; historische Beta-Inhalte erhalten.
- [x] mpv und FFmpeg auf feste Downloads mit SHA-256 und Versionsprüfung festgelegt; kein `latest` im neuen Release-Paketbau.
- [x] Hauptlizenztexte, FFmpeg-Binary-Lizenzreport und Lizenz-/Hinweisdateien der tatsächlich verwendeten .NET-Runtime 8.0.31 im Prüfpaket enthalten.
- [x] Lokaler Self-contained-V1-Build erfolgreich.
- [x] Lokale Setup-Prüfung erfolgreich: Neuinstallation, Wiederholung, Beta-Upgrade, Icon/Verknüpfungen, Erhalt eigener Dateien und sichere Deinstallation.
- [x] [GitHub-Windows-Prüflauf 37451607976](https://github.com/kulmi84/HomeCam-Monitor/actions/runs/37451607976) erfolgreich: Oberfläche, Voreinstellungen, Sicherung/Zurücksetzen/Diagnose, Vorlauf, Taskleisten-Vorschau und Setup.
- [x] SHA-256 für lokales Prüfpaket erzeugt.
- [x] [Aktualisierter Windows-Prüflauf 37453224921](https://github.com/kulmi84/HomeCam-Monitor/actions/runs/37453224921) einschließlich Microsoft Defender erfolgreich.
- [x] Finaler Paketbau scheitert ausdrücklich, solange korrespondierende Quellen nicht vollständig geprüft sind. Nur `-ReviewOnly` erzeugt ein entsprechend gekennzeichnetes Prüfpaket.

## Fremdquellen und abschließende Freigabe

- [x] Eigenen mpv-/FFmpeg-Build mit allen tatsächlichen Quellen, Revisionen, Patches und Buildskripten abschließen. Der Quellen-Workflow archiviert den verwendeten Stand; upstream-Bereinigung wird dafür deaktiviert.
- [ ] Eigene Fremdbinaries inklusive Quelleninventar auf Lizenzkompatibilität prüfen. Das neue Cross-Build-Rezept verwendet GPL-Komponenten; das bisherige FFmpeg-Prüfpaket ist LGPLv3. Diese Varianten dürfen nicht verwechselt werden.
- [x] Neu gebaute Fremdprogramme unter Windows mit Aufnahme-/Vorlauf-/Vorschautests geprüft; Binär- und Quellenprüfsummen im gemeinsamen Prüfpaket enthalten.
- [x] Dependency-Copyrights und -Lizenztexte aus dem Quelleninventar in das neue Prüfpaket aufgenommen. Die endgültige Freigabe bleibt ausstehend.
- [ ] Finale Pakete zusammen mit korrespondierenden Quellenarchiven und Quellenmanifest erstellen; alle Prüfsummen kontrollieren.
- [ ] Gegebenenfalls private Entwicklung und öffentliche Downloads trennen. Das aktuelle Repository ist öffentlich; GitHub gestattet Ansehen und Forken im Rahmen seiner [Nutzungsbedingungen](https://docs.github.com/en/site-policy/github-terms/github-terms-of-service#d-user-generated-content). Sichtbarkeit/Namen werden in diesem PR nicht verändert. Nur Quelldateien aus dem Hauptbranch zu löschen entfernt sie nicht aus der Historie.
- [ ] Rechtekette für eigenen Code und Grafiken sowie frühere Lizenzzusagen bestätigen; Änderungen und konkrete Downloadpakete durch den Rechteinhaber prüfen lassen.
- [ ] Installation mit echten Kameras und gegebenenfalls Home Assistant auf dem vorgesehenen Windows-PC abschließend prüfen.
- [ ] Erst nach Prüfung/Freigabe finalen GitHub Release veröffentlichen und offizielle Downloadlinks eintragen.

## Buildbefehle

```powershell
dotnet publish HomeCamMonitor.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -o publish
.github/scripts/prepare-release-components.ps1
.github/scripts/build-release-downloads.ps1 -ReviewOnly
```

Ohne `-ReviewOnly` verlangt der Paketbau geprüfte korrespondierende Quellen und ein passendes `sources-manifest.json`. Es werden weder Tags erstellt noch Releases automatisch veröffentlicht. Der Quellen-Build stellt Fremdbinaries und die zugehörigen Quellen gemeinsam als Prüfmaterial bereit; er ersetzt noch keine finale Freigabe.

## Erfolgreiches gemeinsames Prüfpaket vom 07.10.2026

[Quellen-Build und Windows-Prüfung 37641796309](https://github.com/kulmi84/HomeCam-Monitor/actions/runs/37641796309) sind erfolgreich abgeschlossen. Der Lauf baut die Fremdprogramme, archiviert die verwendeten Quellen und prüft anschließend deren Prüfsummen, Lizenzdateien und die GPLv3-Ausgabe von FFmpeg unter Windows. Vorlauf, minimierte Vorschau, V1-Oberfläche, Installer, Beta-Upgrade und Microsoft Defender sind erfolgreich geprüft.

[Prüfpaket mit Installer, portabler ZIP, korrespondierenden Quellen, Quelleninventar, Prüfsummen und Defender-Bericht](https://github.com/kulmi84/HomeCam-Monitor/actions/runs/37656738717/artifacts/11498617848). Dieses Paket verwendet die selbst gebauten GPLv3-Fremdprogramme. Es bleibt als Review gekennzeichnet; ein erfolgreicher technischer Test ersetzt nicht die abschließende Lizenzkompatibilitäts- und Rechteprüfung oder den Test mit echten Kameras.

Für eine erneute Paketprüfung kann der erfolgreiche Quellen-Lauf wiederverwendet werden. Ein erneuter vollständiger Fremdprogramme-Build ist dafür nicht nötig. Finaler Release, V1-Tag und Merge bleiben ausstehend.
Die abschließende [Windows- und Paketprüfung 37656738717](https://github.com/kulmi84/HomeCam-Monitor/actions/runs/37656738717) ist erfolgreich. Die Versions- und Quellrevisionen im Paket stammen aus den tatsächlichen Binaries und dem Quelleninventar; der Defender-Bericht ist im Download enthalten (Exitcode 0, keine Bedrohungen). 276 Lizenz- und Hinweisdateien sind enthalten. [Kompakte Lizenz- und Prüfnachweise](https://github.com/kulmi84/HomeCam-Monitor/actions/runs/37656738717/artifacts/11499226131) stehen zusätzlich bereit.

SHA-256 des abschließenden Prüfpakets:

- Installer: `cebafe012878e27c8e07a7c8dae935f3a7a99501bfe66aa8e628321e4a6b11c5`
- Portable ZIP: `fad51c21ff1743b3f0a9e5bc222d37daabed0a434d99db0e994cd407227546e9`
- Quellenarchiv: `d4ae852e6f5048e6918a597c538724fb60385eb658f1c93d5ad9ac910acf9d6f`

Die Quellenarchiv-Prüfung bestätigt 1.020 mpv-Einträge, 10.965 FFmpeg-Einträge, 71 graphengine-Einträge und 22 Buildnachweise. `libzimg/graphengine` verweist relativ auf `../graphengine`; der absolute Runner-Pfad wird nicht benötigt. Review-Kennzeichnung und abschließende Freigabepunkte bleiben erhalten.
Das V1-Icon verwendet unverändert die vorhandene Datei HomeCamMonitor-Beta.ico unter dem Stable-Dateinamen. Damit sind Motiv und belegte Fläche in allen Icon-Größen identisch zur Beta; Anwendung, Fenster, Setup, Deinstallation und Verknüpfungen übernehmen dieses Icon.

## Aktualisierung vom 08.10.2026

Die technische Fremdquellen- und Lizenzprüfung ist in [third-party-release-audit.md](third-party-release-audit.md) dokumentiert. Ergänzende FreeType-, IJG- und Codec-Texte wurden aus den tatsächlichen Quellen ergänzt; Windows-Prüflauf 37672184812 hat bestanden. Die früheren offenen Quellenpunkte oben dokumentieren den damaligen Zwischenstand.

Der Paketworkflow bereitet jetzt final benannte Dateien nur für den exakt geprüften Quellen-/Binary-Satz vor. Installer, portable ZIP sowie Quellen und Prüfsummen stehen als getrennte Downloads bereit, sobald der abschließende Paketlauf erfolgreich ist. Marcin hat Installation und korrigiertes Icon des vorherigen Pakets bestätigt. Anwendungscode und Icon werden durch diese Paketänderung nicht verändert.

Öffentliche Veröffentlichung, Tag und Merge bleiben ausstehend. PR #2 zielt jetzt auf main. Dessen Dokumentationshistorie wurde im Prüfbranch ohne Änderung des Dateistands berücksichtigt; die spätere Übernahme ist konfliktfrei vorbereitet. Die Quellen werden beim offiziellen Release dauerhaft als separate Assets angeboten.
## Veröffentlichungsfreigabe vom 08.10.2026

Marcin hat die Übernahme nach main und Veröffentlichung ausdrücklich genehmigt. Alle abschließenden Läufe für Paketcommit e84f3ed54ca7192bf9a4a380afd82d1e8013da20 sind erfolgreich:

- [Windows-Paketprüfung 37735725449](https://github.com/kulmi84/HomeCam-Monitor/actions/runs/37735725449): Installer, Beta-Upgrade, Oberfläche, Vorlauf, minimierte Vorschau, vollständige Lizenztexte und Defender (Exitcode 0, keine Bedrohungen).
- [Release-Prüfung 37735725672](https://github.com/kulmi84/HomeCam-Monitor/actions/runs/37735725672).
- [Windows-Build 37735730109](https://github.com/kulmi84/HomeCam-Monitor/actions/runs/37735730109).

Die finalen Downloads stammen unverändert aus diesem Paketlauf. Spätere Änderungen betreffen ausschließlich Veröffentlichungsdatum und offizielle Links. Installer-SHA-256: 96aef4ae903dae1cd8913aae9b64dce4688ab61bb0ebfd0f567a39286edae418. Portable-SHA-256: 674a49e9cbebae979d8c74321e36d71bf869aa4ddcaf452e5a4f770682134463.

Offizielle Downloadseite: https://github.com/kulmi84/HomeCam-Monitor/releases/tag/v1.0.0. Quellenarchiv, Inventar, Quellenmanifest und Prüfsummen werden dort zusammen mit den Binaries dauerhaft angeboten. Die früheren Abschnitte dokumentieren die Vorbereitung und deren damalige offene Punkte.