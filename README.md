# HomeCam Monitor

<p align="center">
  <img src="assets/homecam-monitor-logo.png" alt="HomeCam Monitor Logo" width="128">
</p>

Kleines Windows-Fenster für dauerhafte Kamera-Livestreams. Die mpv-Video-Engine rendert RTSP-Streams hochwertig per Direct3D 11, arbeitet unabhängig vom Dashboard und verbindet den ausgewählten Stream bei einem Abbruch automatisch neu.

## Bedienung

1. `HomeCamMonitor.exe` starten.
2. Beim ersten Start für jede gewünschte Kamera einen Namen und die vollständige Streamadresse eintragen.
3. Das Fenster an die gewünschte Stelle ziehen und passend skalieren.

- Das Kamerabild wird rahmenlos ohne Windows-Titelleiste angezeigt.
- Das normale Kamerafenster besitzt abgerundete Ecken wie die Home-Assistant-Karte.
- Die frei im Bild liegenden Symbole wechseln die Kamera, verschieben das Fenster, speichern einen Snapshot, öffnen die Einstellungen oder schalten auf Vollbild.
- Der Stream wird spätestens alle fünf Minuten kurz neu verbunden, damit keine zunehmende Zeitverzögerung entsteht. Bei einem mpv-Abbruch erfolgt die Wiederverbindung automatisch.
- **Snapshot** speichert das aktuelle Kamerabild automatisch unter `Bilder\HomeCam Monitor`.
- Das normale Fenster lässt sich wie ein Browserfenster direkt an allen Kanten und Ecken skalieren.
- Doppelklick ins Bild: echtes Vollbild; erneuter Doppelklick stellt die vorherige Fenstergröße wieder her.
- Fensterposition und Größe werden beim Beenden gespeichert.
- Kameras können in den Einstellungen jederzeit ergänzt, geändert oder gelöscht werden.
- Unterstützt werden direkte RTSP-, HTTP- und HTTPS-Streams. `onvif://` ist keine abspielbare Video-Adresse.
- Vorbelegte Beispiele: Einfahrt und Garten über go2rtc auf `192.168.9.8:8554`.

Die lokale Konfiguration liegt unter `%LOCALAPPDATA%\HomeCamMonitor\settings.json`.

## Automatischer Windows-Build

Unter **Actions → Windows-Build → Run workflow** lässt sich jederzeit eine portable Windows-Version erstellen. Anschließend steht im abgeschlossenen Lauf das ZIP-Artefakt `HomeCamMonitor-win-x64` zum Download bereit. Es enthält die Anwendung einschließlich .NET-Laufzeit und `mpv.exe`.

## Lokaler Build

Voraussetzung: .NET 8 SDK.

```powershell
dotnet restore
dotnet publish HomeCamMonitor.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -o publish
```

Danach `publish\HomeCamMonitor.exe` starten. Der komplette Ordner `publish` wird benötigt.
