# V0.5 Beta 2 – Setup

Das selbstentpackende Setup bietet einen frei wählbaren Installationsordner. Standard ist `C:\Program Files\HomeCamMonitor`. Ein bereits gespeicherter Installationspfad wird wieder vorgeschlagen.

Bei aktivierter Option „Startmenü-Einträge erstellen“ (Standard: an) werden im Startmenü für den aktuellen Windows-Benutzer „HomeCamMonitor Beta“ (Programm mit Icon) und „Installationsordner“ eingerichtet. Eine Desktop-Verknüpfung sowie Programmstart nach Installation sind optional. Beta-Einstellungen bleiben im bisherigen Profilordner erhalten. Bei Installation in denselben Ordner wird nur die dort laufende Beta beendet. Andere Dateien im Zielordner werden nicht entfernt.

Für geschützte Zielordner fordert das Setup bei der Installation Administratorrechte an. Nur das Entpacken der Programmdateien wird erhöht ausgeführt; Verknüpfungen, gespeicherter Installationspfad und anschließender Programmstart erfolgen für den ursprünglichen Benutzer.

CI prüft Entpacken in einen Pfad mit Leerzeichen, Wiederholungsinstallation, Erhalt fremder Dateien, Programm-Icon und Verknüpfungsziel. Eine Setup-Vorschau wird als Build-Artefakt gespeichert.

Beta 9 erkennt vorhandene Installationen anhand gespeicherter Setup-/Deinstallationseinträge und der Programmdatei im ausgewählten, Standardordner. Das Setup bietet Aktualisieren und Deinstallieren an. Bei alten Paketen ohne sichere Installationsliste muss zunächst aktualisiert werden.

„Einstellungen zurücksetzen“ ist standardmäßig aus und benötigt eine Bestätigung. Nach erfolgreicher Installation werden nur Beta-Einstellungen gelöscht; vorhandene Aufnahmen, Sicherungsdateien und die Aufbewahrungsdateiliste bleiben erhalten. Beim nächsten Programmstart werden frische Voreinstellungen erzeugt, ohne alte Stable-Einstellungen erneut zu importieren. Eine Deinstallation bietet das Löschen der Einstellungen ebenfalls separat an.

Beta 10 enthält keinen fest eingebauten persönlichen Entwicklungsordner mehr. Die Erkennung nutzt nur das ausgewählte Ziel, vorhandene Registry-Einträge und den Standardordner. Bei der Deinstallation werden beide Installationsvermerke immer entfernt, unabhängig davon, ob Einstellungen behalten werden. Ein vorhandener benutzerdefinierter Einstellungsordner bleibt bei deaktiviertem Löschen der Einstellungen erhalten.

Beta 11 zeigt unten links die Programmversion als reine Information an. Die Anzeige verwendet denselben Versionswert wie der Windows-Deinstallationseintrag.

In Beta 11 liegen Version, Speichern und Abbrechen in einem festen Fußbereich des Einstellungsfensters. Nur die Einstellungsgruppen scrollen. Beim Öffnen bleibt das gesamte Fenster innerhalb der Arbeitsfläche des jeweiligen Bildschirms, einschließlich Abstand zur Taskleiste.

Beta 12 trennt das Scrollfenster vom automatisch bemessenen Inhalt. Damit umfasst die Scrollstrecke auch die gesamte letzte BETA-Gruppe und deren unteren Abstand. Eine Windows-Prüfung scrollt bei normaler und kleiner Fensterhöhe bis zur letzten Gruppe und kontrolliert deren vollständige Sichtbarkeit.

Eine dezente dunkelgraue Trennlinie kennzeichnet den festen Fußbereich.

Beta 13 verkleinert den festen Fußbereich auf eine Zeile: Version links, Abbrechen und Speichern rechts auf gleicher Höhe. Die Trennlinie und der vollständige Scrollbereich bleiben erhalten.

Beta 14 ergänzt beim Sichern der Einstellungen den Rechnernamen im vorgeschlagenen Dateinamen, zum Beispiel `HomeCamMonitor-Einstellungen_PC-NAME_2026-10-03_15-35-00.json`. Der Name bleibt im Speichern-Dialog frei änderbar. Inhalt und Wiederherstellung bestehender Sicherungen bleiben unverändert.

Beta 15 bietet im Bereich Bewegung und Aktivitätsanzeige die Checkbox „Minimiert starten“. Diese zusätzliche Bedienmöglichkeit ist mit der vorhandenen Auswahl „Beim Start“ synchronisiert und verwendet denselben gespeicherten Wert. Sie funktioniert unabhängig von der Bewegungserkennung. Bei neuen Einstellungen bleibt sie ausgeschaltet. Während der Bearbeitung stellt Ausschalten die vorherige Startauswahl wieder her.
