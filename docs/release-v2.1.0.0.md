# HomeCam Monitor V2 – Version 2.1.0.0

Version 2.1 ergänzt ein frei verschiebbares und skalierbares Bild im Bild im Vollbildmodus und verbessert das Einblenden nach minimiertem Start sowie beim Wiederherstellen aus der Taskleiste.

## Neu in 2.1

1. **Bild im Bild bei Bewegung:** Im Vollbild bleibt die ausgewählte Kamera sichtbar; eine andere Kamera mit Bewegung erscheint als stumme Vorschau mit abgerundeten Ecken.
2. **Position und Größe anpassen:** Rechtsklick → **Bild im Bild → Größe ändern …** öffnet auch ohne Bewegung eine schwarze Vorschau mit HomeCam-Logo. Ziehen verschiebt das Fenster, Ränder und Ecken ändern seine Größe. Ein Klick außerhalb beendet die Anpassung und speichert die Geometrie.
3. **Vorschau bleibt beim Mauszeiger sichtbar:** Solange sich der Mauszeiger darin befindet oder ihr Menü geöffnet ist, pausiert das Ausblenden.
4. **Manuellen Aufruf steuern:** In den Einstellungen lässt sich das automatische Minimieren nach manuellem Öffnen einschalten. Die Wartezeit pausiert beim Mauszeiger im Fenster. Die Option zum vorübergehenden Halten im Vordergrund ist standardmäßig eingeschaltet.
5. **Stabiles Einblenden:** Streams behalten beim ersten Aufruf nach minimiertem Start und beim Wiederherstellen ihre korrekte Größe. Wiederholte Bewegung nach einer Bild-im-Bild-Größenänderung verursacht keinen Fensterfehler mehr.
6. **Über-Dialog:** Links zu GitHub, Anleitung und Fehlerberichten sowie Versionsinformationen, Entwicklerangabe und Hinweise zu Drittkomponenten.

## Update

Die Microsoft-Store-Ausgabe aktualisiert die bestehende HomeCam-Monitor-Installation. Paketidentität, Kamerakonfiguration, gespeicherte Zugangsdaten, Speicherpfade und die Symbole für helle und dunkle Windows-Designs bleiben erhalten. Die separat installierbare Next Beta bleibt eine eigenständige Testausgabe; deren Einstellungen werden nicht automatisch in die stabile Ausgabe übernommen.

Für die GitHub-Ausgabe stehen Setup und portable ZIP bereit. Sie verwenden weiterhin die bestehenden V2-Einstellungen. Eine eigene kompatible Kamera ist erforderlich.

## Bilder

Die neuen Produktbilder zeigen die App-Oberfläche mit fiktiven Kameraszenen ohne Personen und ohne private Kameradaten. Ältere Bilder in der Anleitung bleiben zur Dokumentation erhalten.

## Komponenten

Die geprüften Video- und Reolink-Komponenten entsprechen Version 2.0.25.0. Die vollständigen zugehörigen Quellarchive, Lizenzhinweise und Prüfsummen werden mit den Release-Dateien bereitgestellt. Details: [Komponentenprüfung](third-party-release-audit-v2.md).
