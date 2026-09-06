# HomeCam Monitor

Kleines Windows-Fenster für dauerhafte Kamera-Livestreams. Es arbeitet unabhängig vom Home-Assistant-Dashboard und startet den ausgewählten Stream automatisch neu, wenn zehn Sekunden lang kein Bildfortschritt erkannt wird.

## Bedienung

1. `HomeCamMonitor.exe` starten.
2. Beim ersten Start für jede gewünschte Kamera einen Namen und die vollständige Streamadresse eintragen.
3. Das Fenster an die gewünschte Stelle ziehen und passend skalieren.

- Über die Kameraknöpfe am oberen Fensterrand zwischen **Einfahrt**, **Garten** und weiteren Kameras wechseln.
- Doppelklick ins Bild: Fensterrahmen ein-/ausblenden.
- Rechtsklick ins Bild: Stream neu laden, Einstellungen oder Beenden.
- Fensterposition und Größe werden beim Beenden gespeichert.
- Kameras können in den Einstellungen jederzeit ergänzt, geändert oder gelöscht werden.
- Unterstützt werden direkte RTSP-, HTTP- und HTTPS-Streams. `onvif://` ist keine abspielbare Video-Adresse.
- Vorbelegte Beispiele: Einfahrt `192.168.189.206`, Garten `192.168.189.207`.

Die lokale Konfiguration liegt unter `%LOCALAPPDATA%\HomeCamMonitor\settings.json`.

## Automatischer Windows-Build

Unter **Actions → Windows-Build → Run workflow** lässt sich jederzeit eine portable Windows-Version erstellen. Anschließend steht im abgeschlossenen Lauf das ZIP-Artefakt `HomeCamMonitor-win-x64` zum Download bereit. Es enthält die Anwendung einschließlich .NET- und VLC-Laufzeit.

## Lokaler Build

Voraussetzung: .NET 8 SDK.

```powershell
dotnet restore
dotnet publish HomeCamMonitor.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -o publish
```

Danach `publish\HomeCamMonitor.exe` starten. Der komplette Ordner `publish` wird benötigt.
