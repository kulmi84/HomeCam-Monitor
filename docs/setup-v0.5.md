# V0.5 Beta 2 – Setup

Das selbstentpackende Setup bietet einen frei wählbaren Installationsordner. Standard ist `C:\Program Files\HomeCamMonitor`. Ein bereits gespeicherter Installationspfad wird wieder vorgeschlagen.

Im Startmenü werden für den aktuellen Windows-Benutzer „HomeCamMonitor Beta“ (Programm mit Icon) und „Installationsordner“ eingerichtet. Eine Desktop-Verknüpfung sowie Programmstart nach Installation sind optional. Beta-Einstellungen bleiben im bisherigen Profilordner erhalten. Bei Installation in denselben Ordner wird nur die dort laufende Beta beendet. Andere Dateien im Zielordner werden nicht entfernt.

Für geschützte Zielordner fordert das Setup bei der Installation Administratorrechte an. Nur das Entpacken der Programmdateien wird erhöht ausgeführt; Verknüpfungen, gespeicherter Installationspfad und anschließender Programmstart erfolgen für den ursprünglichen Benutzer.

CI prüft Entpacken in einen Pfad mit Leerzeichen, Wiederholungsinstallation, Erhalt fremder Dateien, Programm-Icon und Verknüpfungsziel. Eine Setup-Vorschau wird als Build-Artefakt gespeichert.
