# V0.5 Beta 1 – Setup

Das selbstentpackende Setup bietet einen frei wählbaren Installationsordner. Standard ist `%LOCALAPPDATA%\Programs\HomeCamMonitor-Beta`. Ein bereits gespeicherter Installationspfad wird wieder vorgeschlagen; eine bestehende Installation im alten Ordner `C:\github_mk\HomeCamMonitor-Beta` wird ebenfalls berücksichtigt.

Im Startmenü werden für den aktuellen Windows-Benutzer „HomeCamMonitor Beta“ (Programm mit Icon) und „Installationsordner“ eingerichtet. Eine Desktop-Verknüpfung sowie Programmstart nach Installation sind optional. Beta-Einstellungen bleiben im bisherigen Profilordner erhalten. Bei Installation in denselben Ordner wird nur die dort laufende Beta beendet. Andere Dateien im Zielordner werden nicht entfernt.

Der Zielordner muss für den aktuellen Benutzer beschreibbar sein. Das Setup installiert für den aktuellen Benutzer und fordert keine Administratorrechte an.

CI prüft Entpacken in einen Pfad mit Leerzeichen, Wiederholungsinstallation, Erhalt fremder Dateien, Programm-Icon und Verknüpfungsziel. Eine Setup-Vorschau wird als Build-Artefakt gespeichert.
