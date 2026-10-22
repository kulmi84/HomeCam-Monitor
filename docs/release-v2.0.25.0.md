# HomeCam Monitor V2 2.0.25.0 – feste Icon-Datei und gezielte Shell-Cache-Aktualisierung

Der Nutzer bestätigte im unveränderten hellen Windows-Modus den Cache-Test:
Phase 1 Schwarz, Phase 2 Weiß, Phase 3 Schwarz. Dabei blieben Dateiname und
Fenstergruppen-ID gleich. Nur die ICO-Datei wurde überschrieben und Explorer
über den betreffenden Bildcache-Eintrag informiert.

Die Store-App verwendet jetzt eine feste `Taskbar/Current.ico` unter ihrem
physischen Paketdatenordner LocalState. Dieser Pfad ist auch für Explorer
auflösbar und enthält keine Paketversionsnummer. Vor dem Überschreiben ermittelt sie
den bisherigen Shell-Bildindex, kopiert die vollständige passende ICO-Datei und
benachrichtigt die Shell mit SHUpdateImage und SHCNE_UPDATEITEM. Der Inhalt
wechselt, Dateiname und laufende Fenstergruppe bleiben gleich.

Die bisherige 500-ms-Trennung/Wiederzuordnung ist entfernt. Beim Designwechsel
werden keine Gruppeneigenschaften neu gesetzt. Nur die native Fensterikone und
die feste ICO-Datei werden aktualisiert. Die Datei bleibt beim Schließen für
Anheftungen erhalten; ihr Pfad enthält keine Paketversionsnummer.

Für diesen Wechsel der Icon-Quelle wird einmalig die stabile Fenstergruppen-ID
`MarcinKulmaczewski.HomeCamMonitor.ThemedTaskbar` eingeführt. Alte Anheftungen
der Vorgängerversion gegebenenfalls lösen und die laufende App neu anheften.
Die registrierte Paket-/Prozess-App-ID und die Aktivierung über AppsFolder
bleiben unverändert. Angeheftete Dateien werden nicht von der App bearbeitet.

Automatisch geprüft: komplette ICO-Dateiinhalte in beiden Farben über wiederholte
Wechsel, unveränderter Ressourcenpfad und Gruppen-ID während dieser Wechsel,
Shell-Bildindex und Aktualisierungsaufrufe, HWND-Neuerstellung, Dispose,
Fortbestehen der Icon-Datei nach Fensterschließung und Paketaktivierung.

22.10.2026: Der Nutzer bestätigt den sichtbaren Designwechsel mit dem exakten
signierten Paket 2.0.25.0 und die Funktion nach Windows-Neustart. Ein gesonderter
erneuter Kamera-/Aufnahmetest und Start über eine neue Anheftung wurden dabei
nicht ausdrücklich berichtet.
