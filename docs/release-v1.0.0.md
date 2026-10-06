# Release-Prüfung HomeCam Monitor V1.0.0

Stand: 06.10.2026. Kein finaler GitHub Release und kein V1.0.0-Tag veröffentlicht.

## Ausgangsbasis und Vorschlag

Die V1.0.0-Vorbereitung liegt in [PR #2](https://github.com/kulmi84/HomeCam-Monitor/pull/2) auf Basis von Beta 22, Commit `7041045392c17e8677bee127b3a76e1b8a9b1cfb`. Der ältere `main`-Stand enthält weiterhin Stable 0.1.2 und eine ältere Beta. Er wurde nicht verändert.

Das Stable-Projekt baut jetzt den vollständigen festgelegten V1-Funktionsumfang. Das interne Compilerkennzeichen `BETA` aktiviert dabei den vorhandenen Code; `RELEASE_V1` wählt die neutrale Produktbezeichnung und das Stable-Icon. Bestehende Funktionen und die Beta-Projektdatei bleiben erhalten. Neue Funktionen gehören zur V2/Beta-Linie.

## Erledigt und geprüft

- [x] Anwendung, Assembly- und Dateiversion: 1.0.0 / 1.0.0.0.
- [x] Anwendung: `HomeCamMonitor.exe`; Setup: `HomeCamMonitor-v1.0.0-Setup.exe`; portable ZIP: `HomeCamMonitor-v1.0.0-win-x64.zip`.
- [x] Vollständige Beta-22-Funktionen im V1-Projekt; Stable-Icon als Fenster- und Setup-Icon.
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
- [x] Finaler Paketbau scheitert ausdrücklich, solange korrespondierende Quellen nicht vollständig geprüft sind. Nur `-ReviewOnly` erzeugt ein entsprechend gekennzeichnetes Prüfpaket.

## Fremdquellen und abschließende Freigabe

- [ ] Eigenen mpv-/FFmpeg-Build mit allen tatsächlichen Quellen, Revisionen, Patches und Buildskripten abschließen. Der Quellen-Workflow archiviert den verwendeten Stand; upstream-Bereinigung wird dafür deaktiviert.
- [ ] Eigene Fremdbinaries inklusive Quelleninventar auf Lizenzkompatibilität prüfen. Das neue Cross-Build-Rezept verwendet GPL-Komponenten; das bisherige FFmpeg-Prüfpaket ist LGPLv3. Diese Varianten dürfen nicht verwechselt werden.
- [ ] Neu gebaute Fremdprogramme auf Windows mit den vorhandenen Aufnahme-/Vorlauf-/Vorschautests prüfen, anschließend feste Binär- und Quellenprüfsummen übernehmen.
- [ ] Vollständige Dependency-Copyrights und -Lizenztexte aus dem Quelleninventar in die finalen Downloadpakete aufnehmen.
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
