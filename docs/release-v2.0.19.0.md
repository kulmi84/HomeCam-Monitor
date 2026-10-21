# HomeCam Monitor V2 – 2.0.19.0

Die Microsoft-Store-Ausgabe verwendet automatisch passende Taskleistensymbole
für den hellen und dunklen Windows-Modus.

Das Store-Paket enthält transparente weiße Taskleistensymbole für dunkle und
transparente schwarze Symbole für helle Windows-Taskleisten. Windows wählt sie
über `altform-unplated` bzw. `altform-lightunplated` aus. Beide Varianten liegen
in 15 Zielgrößen von 16 bis 256 Pixel vor und werden in `resources.pri` indiziert.

In den App-Einstellungen gibt es keine manuelle Symbolauswahl. Die App
überschreibt weder ihre Paket-App-ID noch Explorer-Relaunch-Eigenschaften.
Vorhandene Kamera-, Aufnahme- und Speichereinstellungen bleiben erhalten.

## Hinweise zum Update

Bei einer alten Verknüpfung kann Neolink mit dem Windows-Fehler 0xc0000022
scheitern, während der Start über das Startmenü funktioniert. In diesem Fall
HomeCam schließen, die alte Taskleistenanheftung lösen und HomeCam aus dem
Startmenü erneut an die Taskleiste anheften. Eine Desktopverknüpfung zur
registrierten Store-App lässt sich über `Win+R` → `shell:AppsFolder` →
HomeCam Monitor V2 → **Verknüpfung erstellen** anlegen.

Die lokale Ausgabe enthält weiterhin drei aus der EXE bzw. separaten ICO-Dateien
auswählbare Windows-Verknüpfungssymbole. Der automatische Hell/Dunkel-Wechsel
betrifft die Taskleistengrafiken des Store-Pakets.

## Geprüft

Der Nutzer bestätigt in der Windows-VM den Start über das Startmenü und die neu
angeheftete Taskleistenverknüpfung einschließlich Reolink sowie den automatischen
Icon-Wechsel. Alle 5.085 MSIX-Payload-Prüfsummen, Paketidentität, Versionsnummern,
30 transparente Taskleistengrafiken und beide Varianten im Windows-Ressourcenindex
sind geprüft. Die Entfernung alter Symbolfelder und der entsprechenden
Bedienelemente ist auf Deutsch und Englisch geprüft.

Microsoft-Dokumentation:
https://learn.microsoft.com/de-de/windows/apps/design/iconography/app-icon-construction
https://github.com/microsoft/WindowsAppSDK/issues/2730
