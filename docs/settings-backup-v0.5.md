# V0.5 Beta 7 – Einstellungen sichern und wiederherstellen

Im unteren Bereich der Einstellungen stehen **Einstellungen sichern …** und **Einstellungen wiederherstellen …**.

Die JSON-Sicherung enthält die gespeicherte Konfiguration einschließlich Kameraadressen, Sensoren, HA-Token, Speicherpfaden, Darstellung, Startverhalten und Fensterposition. Ungespeicherte Änderungen zuerst mit Speichern übernehmen. Aufnahmen und interne Dateilisten sind nicht Bestandteil der Sicherung.

Beim Wiederherstellen werden Format, Kameras, Werte und Pfade geprüft. Auch bisherige settings.json-Dateien können eingelesen werden. Vor dem Ersetzen erscheint eine Rückfrage; Abbrechen oder eine ungültige Datei verändert die Konfiguration nicht. Bei laufenden Aufnahmen muss zunächst deren Ende abgewartet werden.

Die Konfiguration wird atomar gespeichert und im laufenden Programm übernommen. Fensterpositionen werden für die vorhandenen Monitore angepasst. Startverhalten und Größe des Einstellungsdialogs gelten beim nächsten Öffnen bzw. Programmstart. Die Wiederherstellung löscht keine Foto- oder Videodateien; die konfigurierte Aufbewahrung für automatische Aufnahmen bleibt aktiv.

Die Sicherungsdatei enthält auch den HA-Token und gegebenenfalls Zugangsdaten in Kameraadressen.

Prüfungen: vollständiger JSON-Rundlauf, Import bisheriger settings.json, leere Kameraliste, ungültige Dateien und Darstellung beider Schaltflächen.
