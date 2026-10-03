# V0.5 Beta 5 – Speicherpfade und Deinstallation

Unter **Speicherpfade** sind vier Ziele unabhängig einstellbar, jeweils mit Pfadfeld und Durchsuchen:

- manuelle Snapshots: bisheriger Bilder-Ordner;
- manuelle Videos: bisheriger Videos-Ordner;
- Bewegungssnapshots und Bewegungsvideos: bisheriger Unterordner Bewegung.

Leere Felder verwenden die bisherigen Standardordner. Vollständige lokale und UNC-Pfade sind möglich. Vorhandene Aufnahmen werden nicht verschoben; laufende Aufnahmen behalten ihr ursprüngliches Ziel. Die Aufbewahrung verfolgt automatische Aufnahmen über eine lokale Dateiliste und löscht keine manuellen oder fremden Dateien in gemeinsam genutzten Ordnern. Alte automatische Aufnahmen im bisherigen Bewegungsordner bleiben über ihr eindeutiges Dateinamensschema erkennbar.

Das Setup registriert **HomeCamMonitor Beta** unter den installierten Windows-Apps, mit unserem Icon und einem Deinstallationsprogramm. Im Startmenü gibt es zusätzlich **Deinstallieren**.

Die Option **Einstellungen ebenfalls löschen** ist standardmäßig ausgeschaltet. Ohne Haken bleibt die Beta-Konfiguration für eine spätere Installation erhalten. Mit Haken werden die Beta-Konfiguration, Diagnoseprotokoll und interne Aufnahmeliste entfernt. Die Stable-Konfiguration bleibt unberührt. Snapshots und Videos werden nie durch die Deinstallation gelöscht. Nur im Paket erfasste Programmdateien und unsere Verknüpfungen werden entfernt; fremde Dateien im Installationsordner bleiben bestehen. Administratorrechte werden bei geschütztem Installationsordner bei Bedarf angefordert.

Prüfungen: vier Pfadfelder, bisherige Standardziele, Aufbewahrungsschutz für manuelle/fremde Dateien, Installation/Update/Deinstallation und Erhalt fremder Dateien und Aufnahmen.
