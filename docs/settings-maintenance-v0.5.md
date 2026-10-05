# Einstellungen zurücksetzen und Diagnose – V0.5.0 Beta 21

Im Einstellungsbereich „Einstellungen sichern, zurücksetzen und Diagnose“ stehen zwei neue Funktionen bereit.

## Gezieltes Zurücksetzen

Die Auswahlliste bietet:

- **Fensterposition und Größe:** Hauptfenster und Größe des Einstellungsfensters zurücksetzen; Kameraauswahl und Raster bleiben erhalten.
- **Anzeige und Bedienung:** Bedienleiste, Beschriftungen, Logo, Außenrahmen, Startverhalten/Autostart und Darstellung bei Bewegung auf Installationsvorgaben zurücksetzen. Kameras, Sensoren und Aufnahmeoptionen bleiben erhalten.
- **Kameraeinstellungen:** Kameraliste, Sensorzuordnung und Home-Assistant-Verbindung einschließlich Token entfernen; Kameraauswahl und Raster/Startkamera zurücksetzen. Speicherpfade und Aufnahmevorgaben bleiben erhalten.
- **Alle Einstellungen:** dieselben Standardwerte wie bei einer neuen Installation.

Eine Sicherheitsabfrage erklärt die Auswahl. Standardantwort ist „Nein“. Nach Bestätigung gelten die Werte sofort; ungespeicherte Änderungen im Dialog werden verworfen. Während einer manuellen oder automatischen Aufnahme ist das Zurücksetzen gesperrt. Bereits vorhandene Aufnahmedateien und der extern gewählte Einstellungsordner werden bei keiner Auswahl gelöscht. Vorher kann die vorhandene Sicherungsfunktion verwendet werden.

## Diagnose exportieren

„Diagnose exportieren …“ schreibt eine JSON-Datei in einen frei gewählten Speicherort. Enthalten sind HomeCam-/Windows-Version, Prozessorarchitektur, relevante numerische und boolesche Einstellungen, bereinigte Kameraadressen (Protokoll/Host/Port), aktivierte Sensorarten und Aufnahmeaktionen, aktueller Anzeige-/Aufnahmestatus und Stream-Ereignisse der laufenden Sitzung.

Stream-Ereignisse unterscheiden Start, erste Bildbereitschaft, Start-/Verbindungsfehler, unerwartetes Ende, Stillstand/Verbindungsabbruch und manuelles Neuverbinden. Zähler gelten seit Programmstart; die Historie ist auf die letzten 200 Ereignisse begrenzt. Die Diagnose zeichnet keine Bilder auf und schreibt nicht dauerhaft im Hintergrund auf die Festplatte. Die abschaltbare BETA-Fensterprotokollierung bleibt unabhängig.

HA-Token, Kameranamen, lokale Pfade, Sensorbezeichnungen, Benutzername/Passwort in URLs, URL-Pfade, Parameter und Fragmente werden nicht exportiert. Rohprotokolle, Ausnahme-Meldungen und Prozessargumente werden nicht übernommen. Hosts/IP-Adressen bleiben zur Fehlersuche enthalten. Die Datei ist eine Diagnose, keine wiederherstellbare Einstellungssicherung.
