# V0.5 Beta 9 – Standardpfade korrigiert

Bewegungssnapshots verwenden den Windows-Bilderordner mit `HomeCam Monitor/Bewegung`; Bewegungsvideos bleiben im Videoordner. Dies gilt für normale Aufnahmen, Vorlauf und die Anzeige in den Einstellungen. Auch der bisher als vollständiger Pfad gespeicherte Video-Standard wird für Snapshots korrigiert. Andere individuelle Pfade sowie vorhandene Aufnahmen bleiben erhalten.

Das x64-Setup verwendet bei einer Neuinstallation `C:\Program Files\HomeCamMonitor`. Die fest eingebaute Rückfallprüfung für `C:\github_mk\HomeCamMonitor-Beta` entfällt. Ein vom Setup gespeicherter Installationsordner wird weiterhin vorgeschlagen. Das Löschen der Programmeinstellungen löscht diesen unabhängigen Installationsvermerk nicht.

Windows-CI prüft Snapshot-Standard, Korrektur des alten Standards, unveränderte individuelle Pfade und Videoziele sowie den Setup-Standard. Bestehende Setup-, Aufnahme- und Oberflächentests laufen weiter.

Zusätzlich bietet das Setup bei vorhandener Installation Deinstallation und optionales Zurücksetzen der Einstellungen an. Startmenü-Einträge sind per Checkbox wählbar (Standard an). CI prüft Installationserkennung, sichere Vorgaben, optionale Startmenü-Verknüpfungen und Erhalt von Aufnahmen beim Zurücksetzen.
