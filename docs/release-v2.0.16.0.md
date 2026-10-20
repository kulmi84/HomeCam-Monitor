# HomeCam Monitor V2 – 2.0.16.0

Dieses Update vereinfacht die Windows-Iconauswahl und korrigiert übernommene Reolink-Helferpfade. App, Windows-Setup und Microsoft-Store-Paket verwenden einheitlich **2.0.16.0**.

- **Drei Symbole direkt aus der EXE:** Unter **Eigenschaften → Verknüpfung → Anderes Symbol …** `HomeCamMonitor-V2-Beta.exe` auswählen. Windows zeigt Standard (Weiß auf Schwarz), Weiß transparent und Schwarz transparent. Jede Variante enthält sieben Größen von 16 bis 256 Pixeln.
- **Aufgeräumte Einstellungen:** Die Logo-/Iconauswahl mit Vorschau und Zusatzschaltflächen entfällt. Die separaten ICO-Dateien und eine Anleitung bleiben zusätzlich im Programmverzeichnis verfügbar.
- **Reolink-Pfade nach Updates:** Noch vorhandene Neolink-Pfade aus alten WindowsApps-Paketversionen werden auf den Helfer der aktuellen Installation umgestellt. Eigene externe Helfer bleiben erhalten.
- **Klare Startfehler:** Zugriff verweigert oder eine fehlende Laufzeit werden als lokale Helferfehler gemeldet; diese Fehler lösen keine endlosen Neustartversuche mehr aus.

Bestehende Kameraeinstellungen, Kennwörter und Speicherpfade bleiben erhalten. Für das Update ist kein Zurücksetzen erforderlich. Die interne Dateikennung `HomeCamMonitor-V2-Beta` bleibt aus Kompatibilitätsgründen bestehen.

## Einzelkamera und Vierer-Raster

<img src="screenshots/v2-single-camera.png" alt="HomeCam Monitor V2: Einzelkamera mit Bedienleiste" width="50%">

<img src="screenshots/v2-four-camera-grid.png" alt="HomeCam Monitor V2: Vierer-Raster mit drei Kameras und einem freien Feld" width="50%">

## Installation und Upgrade

GitHub bietet Windows-x64-Setup, portable ZIP und den zugehörigen HomeCam-Quellcode. Die Store-Ausgabe verwendet dieselbe Versionsnummer; ihre Freigabe und Verteilung erfolgen durch Microsoft. Store-Pakete und GitHub-ZIP nicht in denselben Installationsordner entpacken.

Die Iconauswahl ändert das Symbol der jeweiligen Windows-Verknüpfung. Anwendung und registriertes Startmenü-/Store-Paketlogo verwenden weiterhin die Standardvariante. Bereits angeheftete Symbole gegebenenfalls lösen und neu anheften.

Die drei vollständigen Fremdkomponenten-Quellenarchive werden unverändert mit SHA-256-Nachweis bereitgestellt. Die Hinweise und Quellen/Lizenzen der Medienkomponenten bleiben Bestandteil des Pakets.

[Anleitung mit Bildern](v2.0.0.md) · [Windows-Icons](windows-icons.txt) · [Standardpfade und Upgradeverhalten](storage-defaults-v2.md) · [Komponentenprüfung](third-party-release-audit-v2.md)
