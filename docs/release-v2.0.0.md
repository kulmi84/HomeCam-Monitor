# HomeCam Monitor V2 – 2.0.0

Am 17.10.2026 wurde die letzte Beta (2.0.0-beta.37) vom Nutzer als gut bestätigt und als Grundlage für die finale V2.0.0 gewählt. Die finale Ausgabe ändert Versionsnummer und sichtbare Produktbezeichnung; Kameraanbindung, Bewegungserkennung, Einstellungen und Fensterdarstellung bleiben auf diesem Stand.

## Installation und vorhandene Einstellungen

`HomeCamMonitor-V2-2.0.0-Setup.exe` aktualisiert eine vorhandene V2-Installation im bisherigen Programmordner. Kameraeinträge, geschützte Zugangsdaten und Aufnahmen bleiben erhalten. V1 verwendet weiterhin eigene Einstellungen und einen anderen Installationsordner.

Die technischen V2-Dateinamen, Registry-Schlüssel und Speicherordner behalten ihre bisherigen Namen mit `V2-Beta`. Das ist für die Übernahme vorhandener Installationen beabsichtigt und keine Beta-Versionsnummer. Die Oberfläche zeigt **HomeCam Monitor V2**, Version **2.0.0**.

Der Installer beendet eine laufende Anwendung im ausgewählten Zielordner vor dem Update. Er startet die neue Version nur auf Wunsch. Die portable ZIP enthält dieselben Programmdateien; eine laufende Anwendung muss vor dem manuellen Entpacken geschlossen werden.

## Funktionen und Bilder

Die [V2.0.0-Anleitung mit Bildern](v2.0.0.md) erklärt Reolink-UID, Home Assistant, ONVIF/RTSP, Kamerasuche, externe Ereignisse oder lokale OpenCV/MOG2-Bewegungserkennung sowie Aufnahmen pro Kamera. Die neuen Einstellungsbilder stammen aus der finalen Oberfläche und verwenden erfundene Kameradaten. Die lokale Analyse bleibt bei 320 × 180 Pixeln und maximal fünf Bildern pro Sekunde; kleine oder weit entfernte Bewegungen können je nach Bereich und Mindestfläche übersehen werden.

![V2.0.0: Kameraverwaltung und Bewegungsquelle](screenshots/v2-2.0.0-overview.png)

![Finaler Installer, Version 2.0.0](screenshots/v2-2.0.0-setup.png)

## Validierung

Die finale Ausgabe bestand lokal die vollständigen V2-Funktionstests, die native Einstellungs- und Scrollprüfung sowie die Bewegungserkennung mit echtem FFmpeg und OpenCV. Geprüft wurden außerdem Installation, wiederholtes Update, Deinstallation, Schutz des V1-Ordners, Erhalt von Benutzerdateien und das Beenden einer laufenden Testanwendung beim Update. Die Bereinigung alter Beta-Verknüpfungen lässt fremde Verknüpfungen erhalten. Der Defender-Scan des finalen lokalen Installers wurde abgeschlossen; es wurden keine zugehörigen Erkennungsdatensätze gefunden. Der V1-Build blieb erfolgreich.

## Paketstand und öffentliche Veröffentlichung

Der geprüfte Release-Kandidat verwendet den akzeptierten V2-Anwendungsstand mit dem vollständig belegten GPL-Mediensatz für mpv/FFmpeg. Dieser bewusste Wechsel wurde erneut mit V2-Funktionstests, echter Wiedergabe, Aufnahme/Snapshot und lokaler MOG2-Analyse geprüft. Neolink und OpenCV bleiben bytegleich. `package-inventory.json`, das Komponenten-Lockfile und `SHA256SUMS.txt` dokumentieren den konkreten Dateisatz. Frühere lokale Pakete bleiben davon getrennt.

Die [technische V2-Prüfung der Fremdkomponenten](third-party-release-audit-v2.md) ist für den festgelegten Kandidaten abgeschlossen. Der Bericht vom 18. Oktober 2026 dokumentiert die exakten Dateien und vollständigen Quellenarchive. Vor einem öffentlichen GitHub-Binärrelease müssen diese drei Archive dauerhaft gemeinsam mit Installer, ZIP, Quellenmanifest und Prüfsummen bereitgestellt werden. Die Veröffentlichung selbst ist ein separater Schritt und wird durch den lokalen Prüfbericht nicht vorgetäuscht.

Build: `.github/scripts/build-v2-release.ps1`; `HomeCamMonitor.V2.Beta.csproj` mit `-p:FinalRelease=true`. Für den geprüften Kandidaten `-RuntimeTemplate` auf dessen vorbereiteten Dateisatz und `-AuditedSourcesDirectory` auf die drei festgelegten Quellenarchive setzen. Die Prüfung lehnt abweichende Binaries, fehlende Quellen und veränderte Lizenz-/Runtime-Dateien ab. Separate `-PublishDirectory`-/`-DownloadsDirectory`-Ordner bewahren die bisherigen lokalen Pakete. Der Aufruf ohne Komponentenprüfung bleibt für historische lokale/CI-Pakete erhalten; er erzeugt keine nachträgliche Quellenfreigabe. Zeitlich begrenzte CI-Artefakte ersetzen den dauerhaften öffentlichen Quellenrelease nicht.
