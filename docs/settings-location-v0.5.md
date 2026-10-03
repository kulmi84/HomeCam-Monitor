# V0.5 Beta 8 – Einstellungsordner wählen

Unter **Speicherpfade → Einstellungen** ist der Ordner für settings.json mit Pfadfeld und Durchsuchen wählbar. Standard bleibt `%LOCALAPPDATA%\HomeCamMonitor-Beta`.

Beim Speichern werden die aktuellen Einstellungen zuerst vollständig und atomar in den neuen Ordner geschrieben. Erst dann wird der gewählte Ordner für spätere Starts registriert und die alte Einstellungsdatei entfernt. Kann der alte Ordner nicht bereinigt werden, bleibt dort eine alte Kopie; verwendet wird der neue Ordner. Gibt es am Ziel schon eine settings.json, fragt die Anwendung vor dem Ersetzen nach. Ein fehlgeschlagener Wechsel lässt den bisherigen Speicherort aktiv.

Die gespeicherte Ordnerauswahl bleibt bei Setup-Updates und Deinstallation ohne Einstellungen-löschen erhalten. Beim Deinstallieren mit Einstellungen-löschen wird auch settings.json im gewählten Ordner entfernt. Aufnahmen und andere Dateien werden nicht verschoben oder gelöscht. Diagnoseprotokoll und interne Aufnahmeliste bleiben im bisherigen lokalen AppData-Ordner.

Einstellungsdateien werden weiterhin über Sichern/Wiederherstellen exportiert und importiert; Wiederherstellen schreibt in den aktuell ausgewählten Einstellungsordner. Der Startverweis auf diesen Ordner liegt benutzerspezifisch in der Registry. Ist ein gewählter externer Ordner nicht erreichbar, startet die Anwendung mit einer Fehlermeldung statt die Konfiguration mit Standardwerten zu überschreiben.

Prüfungen: erfolgreicher Wechsel samt Übernahme, Wiederfinden der registrierten Ordnerauswahl, fehlgeschlagener Wechsel und Wiederherstellung einer bereits vorhandenen Zieldatei.
