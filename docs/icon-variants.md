# EXE-Symbole ab 2.0.16.0

Die EXE enthält drei Windows-Symbolgruppen in der Reihenfolge Standard, Weiß transparent, Schwarz transparent. Im Dialog „Anderes Symbol …“ lassen sie sich direkt aus `HomeCamMonitor-V2-Beta.exe` auswählen. Auch die deklarierte Store-Desktopverknüpfung verweist dafür auf die EXE. Separate ICO-Dateien bleiben zusätzlich vorhanden; die App-Einstellungen enthalten keine Logo-/Iconbedienung.

Der Build ergänzt die zwei transparenten Gruppen über eine MSBuild-Aufgabe am erzeugten Windows-Apphost vor Kopieren, Paketierung und Signierung. Die ursprüngliche Standardgruppe sowie Versionsressourcen und Manifest bleiben erhalten. Wiederholte Builds überschreiben dieselben Ressourcen-IDs. `tests/ExeIconResources.ps1` prüft Windows-Aufzählung und -Extraktion aller drei Gruppen sowie die exakten Bilddaten aller 21 Größenbilder.

# Windows-Icons ab 2.0.14.0

Die App enthält keine Logo-/Iconanpassung mehr. Die drei vollständigen ICO-Dateien werden direkt ins Programmverzeichnis kopiert: `HomeCam-Standard.ico`, `HomeCam-Weiss-transparent.ico` und `HomeCam-Schwarz-transparent.ico`. Windows-Verknüpfungen können sie über **Eigenschaften → Verknüpfung → Anderes Symbol … → Durchsuchen …** verwenden. Die Datei `Icons-anpassen.txt` neben der EXE erklärt die Schritte.

Das Fenster- und EXE-Symbol ist fest auf Standard gesetzt. Alte `LogoVariant`-/`IconVariant`-Konfigurationsfelder werden ignoriert. Es gibt keine eigene Taskleisten-ID, keinen automatischen Iconexport und keine Verknüpfungserzeugung aus der App. Der lokale Kandidat 2.0.13.0 wurde verworfen.

Die folgenden Abschnitte dokumentieren frühere Versionen.

# Icon-Auswahl ab Paket 2.0.6.0

## Weißer Vorschau-Rahmen 2.0.10.0

Die Standardvariante (weiß auf schwarzem Hintergrund) erhält ausschließlich in der Einstellungsvorschau einen weißen Außenrahmen von ungefähr 1 mm. Die Rahmenstärke wird anhand der Anzeige-DPI berechnet (4 Pixel bei 96 DPI). Transparente Varianten behalten ihre Vorschau ohne Rahmen. Originalgrafiken und App-Icons bleiben unverändert. Variantenprüfung bestanden und gerenderte Standardvorschau kontrolliert. Ausschließlich lokales VM-Testpaket; keine Veröffentlichung.

Nach Nutzerbestätigung am 19.10.2026 als Store-Update eingereicht (Submission 3, 2.0.10.0). Zertifizierung läuft, öffentliche Veröffentlichung noch ausstehend. Lokale Pakete samt Prüfsummen und Einreichungsnachweis unter `../Releases/2.0.10.0`.

## Lokale Vorschaukorrektur 2.0.9.0

Die Einstellungsvorschau zeichnet das Original mit hochwertiger bikubischer Skalierung und DPI-gerechtem Innenabstand. Transparente Varianten verwenden ein neutrales Schachbrettmuster statt einer vollständig weißen oder dunklen Fläche. Die bestätigten Logo- und ICO-Dateien bleiben unverändert. Die Variantenprüfung ist auf Deutsch und Englisch bestanden; die gerenderten Vorschauen wurden kontrolliert. Das Upgradepaket ist ausschließlich für den lokalen VM-Test bestimmt. Kein Store-Upload und keine Veröffentlichung. Der tatsächliche VM-Test dieses Kandidaten steht noch aus.

Die bestätigte Beta37-Grafik `homecam-monitor-logo-default.png` ist die Quelle für drei geometrisch identische Varianten. `.github/scripts/build-logo-variants.ps1` erzeugt PNGs und mehrstufige ICO-Dateien (16 bis 256 Pixel). Die Anwendung und Store-Bilder verwenden die Standardvariante; Laufzeit-Icon und Kameraplatzhalter folgen der gespeicherten Einstellung `LogoVariant` (0 Standard, 1 Weiß transparent, 2 Schwarz transparent). Fehlende oder ungültige Werte ergeben den Standard. Ein Zurücksetzen der Anzeige setzt auch diese Auswahl zurück.

Tests: `dotnet run --project tests/V2Isolation/V2Isolation.csproj -c Release -p:FinalRelease=true -- --logo-variants`. Prüft Farben/Alpha, Ressourcen, Serialisierung, Zurücksetzen, Auswahl und Vorschau. Separate Lokalisierungsprüfung umfasst 453 Einträge. Installation und visuelle Prüfung auf dem Zielsystem stehen für 2.0.6.0 aus.

Windows verwendet für Startmenü und angeheftete Paket-Verknüpfungen die statischen Paketbilder. Die Laufzeit-Auswahl ändert diese registrierten Bilder nicht. Die automatische Desktopverknüpfung ist weiterhin offen.

Ab 2.0.7.0 bleibt das Standard-App-Icon ausdrücklich auf schwarzem Hintergrund. Die Korrektur betrifft die zusätzliche Windows-Kachel: targetsize-/unplated-Bilder und ein PRI-Index ausschließlich für StoreAssets. Der Index verwendet eine explizite resfiles-Liste, damit die vollständigen Lizenzquellen nicht als Ressourcen interpretiert werden und die Ressourcenpfade Files/StoreAssets erhalten bleiben. Der tatsächliche Effekt auf die blaue Windows-Fläche ist bis zum VM-Test unbestätigt.
