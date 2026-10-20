# HomeCam Monitor V2 – 2.0.12.0

Dieses Update vereinheitlicht die Versionsnummer in der App, dem Microsoft-Store-Paket und dem GitHub-Release auf **2.0.12.0**. Die Buildskripte lesen die gemeinsame Versionsangabe aus `Directory.Build.props`; abweichende App-/Store-Versionen werden beim Paketbau und in der Paketprüfung abgewiesen.

- Neue finale Installationen verwenden `HomeCam Monitor V2` unter Bilder und Videos sowie `%LOCALAPPDATA%\HomeCamMonitor-V2` für Einstellungen. Gespeicherte Pfade und vorhandene V2-/Beta-Konfigurationen bleiben erhalten.
- Die Logo-/Iconvorschau zeichnet geglättete Konturen mit Innenabstand. Transparente Varianten erscheinen auf einem Schachbrett. Die schwarze Standardvorschau erhält einen weißen Rahmen von ungefähr 1 mm, angepasst an die Windows-Anzeigeskalierung.
- Die Logoauswahl verwendet wie die übrigen Auswahllisten die dunkle Darstellung.
- Enthalten sind die deutschen/englischen Oberflächentexte, die drei Iconvarianten und die bisherigen Store-Korrekturen für Desktopverknüpfung, Autostart und Medien-Hilfsprogramme.

## Einzelkamera und Vierer-Raster

![HomeCam Monitor V2: Einzelkamera mit Bedienleiste](screenshots/v2-single-camera.png)

![HomeCam Monitor V2: Vierer-Raster mit drei Kameras und einem freien Feld](screenshots/v2-four-camera-grid.png)

## Installation und Upgrade

Der GitHub-Download enthält eine portable Windows-x64-ZIP und ein Windows-Setup. Bestehende V2-Einstellungen werden weiterverwendet. Die interne Dateikennung `HomeCamMonitor-V2-Beta` bleibt für die Upgrade-Kompatibilität erhalten; die sichtbare finale Version lautet 2.0.12.0. V1 bleibt getrennt.

Die Store-Ausgabe verwendet dieselbe Version und wird durch Microsoft geprüft und verteilt. GitHub-Setup und MSIX dürfen nicht in denselben Installationsordner entpackt werden. Für die Store-Ausgabe erfolgt das Upgrade über den Microsoft Store.

Ein Upgrade behält bereits gespeicherte Aufnahmeordner bei. Die neuen Standardpfade lassen sich mit einem frischen Windows-Testbenutzer prüfen. Vorhandene Einstellungen dafür nicht löschen.

[Anleitung mit Bildern](v2.0.0.md) · [Standardpfade und Upgradeverhalten](storage-defaults-v2.md) · [Iconvarianten](icon-variants.md)

Die verwendeten Medienkomponenten und ihre Quellen/Lizenzen bleiben im Paket dokumentiert. Quellenarchive sind im weiterhin verfügbaren [Komponentenrelease V2.0.0](https://github.com/kulmi84/HomeCam-Monitor/releases/tag/v2.0.0) hinterlegt.
