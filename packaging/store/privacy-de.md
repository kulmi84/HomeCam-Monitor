# Datenschutzhinweise für HomeCam Monitor

Entwurf, Stand 18. Oktober 2026. Vor der Veröffentlichung sind die Rechtsgrundlagen und die öffentlich erreichbare URL zu ergänzen. Die Kontaktangabe erfolgt nach Vorgabe des Anbieters per E-Mail.

## Kontakt

Anbieter der Anwendung: Marcin Kulmaczewski. Kontakt für Datenschutz und Support: support@dorisundmarcin.de.

## Verwendung der Anwendung

HomeCam Monitor verbindet sich mit den von Ihnen eingerichteten Kameras und optional mit Ihrer Home-Assistant-Instanz. Verarbeitet werden insbesondere Kameraadressen und -namen, Verbindungsdaten, Zugangsdaten, Einstellungen sowie die empfangenen Bild-, Ton- und Ereignisdaten. Bei aktivierten Aufnahmen entstehen Video- oder Bilddateien. Die lokale Bewegungserkennung verarbeitet Kamerabilder auf Ihrem Windows-Gerät.

Die untersuchte Anwendung enthält keinen eigenen Telemetrie- oder Uploaddienst des Anbieters. Verbindungen zu eingerichteten Gegenstellen sind für die ausgewählten Funktionen erforderlich. Dabei werden diesen Gegenstellen unter anderem Ihre Netzwerkadresse und die für die Verbindung erforderlichen Daten bekannt. Ob die Verbindung verschlüsselt ist, hängt vom verwendeten Protokoll und der Gegenstelle ab.

## Kameras, Home Assistant und UID-Verbindungen

Direkte Kamera-Verbindungen laufen zu den von Ihnen gewählten Kameraadressen. Die optionale Home-Assistant-Anbindung verwendet die konfigurierte Serveradresse und das hinterlegte Zugriffstoken. Bei Reolink-UID-Verbindungen können Vermittlungs- oder Relay-Dienste des Kameraherstellers beteiligt sein. Für die Verarbeitung durch diese Dienste gelten zusätzlich deren eigene Datenschutzinformationen. Eine ausschließlich auf Ihr Heimnetz beschränkte Übertragung wird für UID-Verbindungen nicht zugesagt.

## Speicherung auf Ihrem Gerät

Einstellungen werden im verwendeten Einstellungsordner gespeichert. Kamera-Passwörter, die über die dafür vorgesehenen Passwortfelder erfasst werden, werden mit dem Windows-Datenschutzdienst für das aktuelle Benutzerkonto geschützt. Dies bedeutet keine Verschlüsselung aller Einstellungen: Das Home-Assistant-Token und gegebenenfalls in Streamadressen enthaltene Zugangsdaten können in den Einstellungsdateien lesbar sein. Für den Reolink-Hilfsprozess wird vorübergehend eine zugriffsbeschränkte Verbindungsdatei mit Zugangsdaten angelegt; sie wird beim regulären Beenden entfernt.

Exportierte Einstellungs-Backups können sensible Verbindungsdaten enthalten. Schützen Sie diese Dateien vor fremdem Zugriff. Aufnahmen werden im gewählten Aufnahmeordner gespeichert. Bei einem Netzwerkordner oder einem durch andere Software synchronisierten Ordner können diese Dateien auch auf anderen Geräten oder bei einem von Ihnen gewählten Cloudanbieter gespeichert werden.

## Diagnose und Support

Diagnosedateien werden auf Ihre Veranlassung exportiert. Der Export entfernt bestimmte Geheimnisse, kann jedoch weiterhin technische Angaben und Kamera-Hostnamen oder IP-Adressen enthalten. Prüfen Sie ihn vor der Weitergabe. Wenn Sie uns per E-Mail kontaktieren, erhalten wir die von Ihnen mitgeteilten Kontaktdaten, Inhalte und Anhänge zur Bearbeitung Ihrer Anfrage. Senden Sie keine Kamera-Passwörter oder Zugriffstoken.

## Aufbewahrung und Löschung

Sie können eingerichtete Verbindungen in der Anwendung entfernen und lokal gespeicherte Einstellungen, Backups sowie Aufnahmen in ihren jeweiligen Ordnern löschen. Die optionale Aufbewahrungsfunktion gilt für die von ihr verwalteten automatischen Aufnahmen. Eine Deinstallation entfernt nicht zwangsläufig selbst gewählte Einstellungs- und Aufnahmeordner. Löschen Sie solche Dateien bei Bedarf gesondert.

## Ihre Rechte

Für Fragen zur Verarbeitung durch den Anbieter und zur Ausübung Ihrer datenschutzrechtlichen Rechte erreichen Sie uns unter support@dorisundmarcin.de. Dazu gehören nach Maßgabe der anwendbaren Vorschriften Auskunft, Berichtigung, Löschung, Einschränkung, Datenübertragbarkeit und Widerspruch sowie das Beschwerderecht bei einer Datenschutzaufsichtsbehörde. Für Daten bei Ihrem Kamerahersteller oder einem anderen von Ihnen genutzten Dienst wenden Sie sich an den jeweiligen Verantwortlichen.
