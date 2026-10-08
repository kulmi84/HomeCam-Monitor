# Changelog

## V1.0.0 – vorbereitet, noch nicht veröffentlicht

Basis des festgelegten Funktionsumfangs: `0.5.0-beta.22`, Commit `7041045392c17e8677bee127b3a76e1b8a9b1cfb`. Der erfolgreiche Windows-Build ist [hier dokumentiert](https://github.com/kulmi84/HomeCam-Monitor/actions/runs/37339036318). Ein Veröffentlichungsdatum wird erst nach Freigabe eingetragen.

### Enthaltener Funktionsumfang

- Rahmenloser Kameramonitor für RTSP/HTTP/HTTPS mit Einzelbild und 2×2-Raster, Vollbild, gespeicherter Fensterposition und Windows-Autostart.
- Automatische Wiederverbindung und regelmäßiger Stream-Refresh mit Bildübergabe.
- Manuelle Snapshots und MKV-Videoaufnahmen; getrennte konfigurierbare Aufnahmeordner.
- Direkte Home-Assistant-Verbindung mit Bewegungs- und Personensensoren je Kamera, Aktivitätsanzeige, Vordergrundreaktion und zeitweiser Pause der Bewegungsaktionen.
- Automatische Bewegungsaufnahmen und Personen-Snapshots, Aufbewahrungsfrist und Snapshot-/Video-Vorlauf.
- Einstellungsbackup und Wiederherstellung einschließlich Kameras, Speicherpfaden und HA-Token.
- Gezieltes Zurücksetzen von Fensterposition/Größe, Anzeige/Bedienung, Kameras oder allen Einstellungen mit Bestätigung; Aufnahmen bleiben erhalten.
- Diagnoseexport mit bereinigten Einstellungen, Versions- und Streaminformationen.
- Setup mit wählbarem Installationsordner, Aktualisierung, Deinstallation und Startmenü-Icon; minimierter Start mit Taskleisten-Vorschau.

### Veröffentlichungsvorbereitung

- README und ergänzende Anleitung auf den vorgesehenen V1.0.0-Funktionsstand ausgerichtet.
- Vorbereiteten proprietären Lizenzentwurf mit abgestimmter Zustimmungspflicht für Paketweitergabe übernommen; Rechteinhaber: Marcin Kulmaczewski.
- Selbst gebaute mpv-/FFmpeg-Binaries mit tatsächlichen Quellen, Abhängigkeiten, Buildrezepten und vollständigen gesammelten Lizenzhinweisen dokumentiert.
- Stable-Projekt übernimmt den vollständigen Beta-22-Funktionsumfang mit Version 1.0.0, neutraler Produktbezeichnung und unverändertem Originalicon aus der Beta.
- Neuer V1-Installer erkennt Beta-Installationen und berücksichtigt bestehende Installationslisten; Einstellungen und eigene Dateien bleiben erhalten.
- Persönliche HA-Standardadresse durch leere Vorgabe ersetzt; vorhandene Konfigurationen behalten ihre Werte.
- Feste Fremdkomponenten-Downloads, Versions-/Prüfsummenprüfung, Runtime-Lizenzunterlagen, Lizenzanzeige im Setup und SHA-256-Paketmanifest ergänzt.
- Finaler Paketbau ist an den geprüften Quellen-Build und dessen exakte Binary-/Quellenprüfsummen gebunden; Installer, portable Version und Quellen werden getrennt angeboten.
- Windows-Prüflauf für V1.0.0 erfolgreich; eigener Fremdquellen-Build ergänzt.

V1.0.0 ist erst nach Abschluss der [Release-Prüfliste](docs/release-v1.0.0.md) freigegeben. Neue Funktionen gehören in die V2/Beta-Linie. Frühere Beta-Änderungen bleiben in der [README](README.md#historische-beta-dokumentation-und-entwicklung) erhalten.
