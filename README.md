# HomeCam Monitor

Kleines Windows-Fenster für dauerhafte Kamera-Livestreams. Es verwendet den MSE-Player von go2rtc wie die bisherige Home-Assistant-Karte, arbeitet unabhängig vom Dashboard und verbindet den ausgewählten Stream bei einem Abbruch automatisch neu.

## Bedienung

1. `HomeCamMonitor.exe` starten.
2. Beim ersten Start für jede gewünschte Kamera einen Namen und die vollständige Streamadresse eintragen.
3. Das Fenster an die gewünschte Stelle ziehen und passend skalieren.

- Das Kamerabild wird rahmenlos ohne Windows-Titelleiste angezeigt.
- Das normale Kamerafenster besitzt abgerundete Ecken wie die Home-Assistant-Karte.
- Die transparenten Web-Symbole unten im Bild wechseln die Kamera, verschieben das Fenster, speichern einen Snapshot, starten Bild-im-Bild, öffnen die Einstellungen oder schließen das Programm.
- **Bild im Bild** verwendet die native Windows-Videodarstellung für die beste Bildqualität. Das Hauptfenster wird dabei ausgeblendet und erscheint wieder, sobald Bild-im-Bild geschlossen wird.
- Erkennt der Watchdog einen stehenden Stream, wird er neu verbunden und das Bild-im-Bild-Fenster automatisch wiederhergestellt.
- **Snapshot** speichert das aktuelle Kamerabild automatisch unter `Bilder\HomeCam Monitor`.
- Das normale Fenster lässt sich wie ein Browserfenster direkt an allen Kanten und Ecken skalieren.
- Doppelklick ins Bild: echtes Vollbild; erneuter Doppelklick stellt die vorherige Fenstergröße wieder her.
- Rechtsklick ins Bild: Snapshot speichern, Stream neu laden, Einstellungen oder Beenden.
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
