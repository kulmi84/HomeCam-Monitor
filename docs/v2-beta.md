# HomeCam Monitor V2 Beta – 2.0.0-beta.37

Historische Entwicklungshinweise: Beta37 ist die bestätigte Grundlage der finalen V2.0.0. Aktuelle Funktionen und Bilder stehen in der [V2.0.0-Anleitung](v2.0.0.md), Paket- und Upgrade-Hinweise in [release-v2.0.0.md](release-v2.0.0.md).

Beta37: Der Einstellungsbereich zeichnet native Felder beim Scrollen gemeinsam gepuffert. Scrollschritte schalten die Sichtbarkeit des Zeichenfensters nicht mehr über WM_SETREDRAW um. Die gesamte Oberfläche wird weiter als feste Einheit verschoben; Kamera- und Bewegungsbereiche behalten ihre Klappfunktion.

Beta36: Bewegungsquellen passen zur Verbindung: Reolink-Kameraereignisse bei UID, externe HA-Sensoren bei Home Assistant und ausschließlich lokale Bildanalyse bei ONVIF/RTSP. Lokale Analyse bleibt bei UID/HA alternativ verfügbar. Die Quelle wird über ihre Bedeutung statt einen festen Dropdown-Index zugeordnet; Kamerawechsel erhalten gespeicherte Sensoren. Kameraeigene Einstellungen werden nicht verändert.

Beta35: Lokale Bildanalyse lässt sich ohne externe HA-Sensoren speichern. HA-Ereignisprüfung greift nur für tatsächlich genutzte externe Bewegung oder Personenereignisse. Der HA-Verbindungstest funktioniert auch nur mit Adresse und Token; lokale Bewegung prüft keine ungenutzte Bewegungsentität.

Beta34: Unter der Bewegungsquelle stehen passende, standardmäßig eingeklappte Einstellungen. Externe HA-Ereignisse und Sensorzuordnung sind aus den Verbindungseinstellungen hierher verschoben. Lokale Analyseparameter und Bereiche stehen im alternativen Klappbereich. Bei Kamera- oder Quellenwechsel wird eingeklappt; gespeicherte Zuordnungen bleiben erhalten. HA-Zugangsdaten bleiben in der Verbindung und unabhängig von der Bewegungsquelle bedienbar.

Beta33: Bewegungserkennung wird nur bei aktiver Kamera mit gesetztem Bewegung-Haken angezeigt. Einstellungen bleiben erhalten. Bei direkten Netzwerkkameras aktiviert der Bewegung-Haken die lokale Bildanalyse.

**Bestätigte gute Beta / aktuelle Referenz: 2.0.0-beta.29 (16.10.2026).** Beta 30 ist zurückgezogen. Details und Installer-Prüfsumme stehen in [stable-beta.md](stable-beta.md).

## Beta 32: Lokale Bewegungserkennung (Idee 1)

**Die bisherige externe Bewegungserkennung bleibt unverändert erhalten und ist für bestehende Kameras weiterhin die Voreinstellung.** Unter der Kamera-/Verbindungsauswahl steht jetzt **Bewegungserkennung – Kameraname** mit zwei Quellen zur Auswahl:

- **Externe Kamera-/HA-Ereignisse (bisher):** wie bisher Kamerabewegung über Reolink-UID oder konfigurierte HA-Sensoren. Reine ONVIF-/RTSP-Bildverbindungen liefern weiterhin keine externen Ereignisse.
- **Lokale Bildanalyse (OpenCV/MOG2):** alternativ für Reolink-UID, Home Assistant und direktes ONVIF/RTSP. Die Kamera muss aktiv sein und „Bewegung“ eingeschaltet bleiben. Die lokale Quelle ersetzt nur die allgemeine Bewegung; konfigurierte HA-Personensensoren bleiben unabhängig davon nutzbar. Kein lokaler Personenklassifikator.

Pro Kamera sind Empfindlichkeit, Mindestfläche einer zusammenhängenden Bewegungsregion, Mindestdauer und Nachlauf einstellbar. Über ein explizit geladenes Vorschaubild können bis zu 16 rechteckige Überwachungsbereiche eingezeichnet werden; Rechtsklick entfernt einen Bereich. Ohne Bereiche gilt das ganze Bild. Die Prozentfläche bezieht sich auf die Vereinigungsfläche der überwachten Bereiche.

Die Analyse nutzt OpenCV/MOG2 mit Schattenfilter, leichter Glättung und Bereinigung der Bewegungsmaske. Einlernen für mindestens 25 Bilder/5 Sekunden und erneutes Einlernen nach Verbindungsabbruch; großflächige Belichtungs-/IR-Wechsel werden ohne Alarm neu eingelernt. Bewegungsdauer und Nachlauf erzeugen ein Ereignis pro zusammenhängender Aktivität. Die vorhandene Markierung, Vordergrund-/Pausenlogik und optionalen Schnappschuss-/Aufnahmeaktionen werden wiederverwendet. Automatische Aufnahmen benötigen zusätzlich den Haken „Aufnahmen“. Direkte Kameras erhalten diese Möglichkeiten nur mit aktivierter lokaler Bewegungserkennung.

Der externe mpv-Prozess stellt keine direkt wiederverwendbare Bildoberfläche bereit. Deshalb liest ein zusätzlicher, begrenzter FFmpeg-Decoder denselben konfigurierten Stream; bei UID wird die vorhandene lokale Bridge genutzt (keine zweite UID-Anmeldung), bei HA der lokale HLS-Endpunkt. Analysiert werden 320 × 180 Pixel bei maximal fünf Bildern/s, ein OpenCV-Thread pro Analyse, nur das neueste Bild in einer Warteschlange der Größe eins. Vorschau und Analyse schreiben keine temporären Bilddateien. Der zusätzliche Decoder verursacht CPU-Last; die volle Eingangsauflösung muss trotzdem dekodiert werden. Der Stream bleibt der vom Nutzer ausgewählte, ohne automatischen Profilwechsel. Lokale Erkennung funktioniert nur während HomeCam Monitor läuft. HLS-Verzögerung und falsche Alarme durch Wetter, Vegetation oder Licht sind weiter möglich; Bereiche/Schwellen müssen zur Szene passen.

Geprüft mit echten nativen OpenCV-Modellen und synthetischen Bildern/Videos: Einlernen, Ruhebild, Mindestdauer/Nachlauf, einzelne Ereignisse, Bereiche, Belichtungswechsel, Alt-Konfigurationen/externe Quellen, direkte Verbindungen, Sicherung/Abbrechen sowie echte FFmpeg-Auswertung und Prozessende. OpenCvSharp 4.13.0.20260627 / OpenCV 4.13.0 werden mit den Lizenzhinweisen ausgeliefert; Windows-Medienkomponenten werden vom offiziellen nativen Runtimepaket benötigt. Beta29 bleibt die bestätigte gute Referenz, Beta30 bleibt zurückgezogen.

## Beta 31: Home-Assistant-Kamera- und Sensorsuche

Unter **Verbindungseinstellungen → Home Assistant → Kameras und Sensoren suchen** werden mit HA-Adresse und bestehendem Langzeit-Token Kamera-Entitäten und binäre Sensoren geladen. Der Dialog zeigt Namen, Entitätskennungen und Verfügbarkeit. Es werden keine Kameranetze gescannt.

- Eindeutige Bewegungs-/Personensensoren desselben HA-Geräts werden vorgeschlagen. Bei mehreren Treffern, fehlender Gerätezuordnung oder fehlenden Registerrechten wird nichts geraten; alle binären Sensoren bleiben per dunklem Dropdown auswählbar. Der Name bzw. die Geräteklasse ist nur ein Vorschlag, keine Bestätigung der Sensorsemantik.
- Als Bildquelle stehen HA-HLS und eine vorhandene/eigene RTSP-/HTTP-Adresse zur Wahl. **Verbindung testen** muss tatsächlich ein Videobild empfangen, bevor eine neue Kamera übernommen werden kann. Kameras, die nur WebRTC/Standbilder unterstützen oder kein HLS bereitstellen, benötigen eine eigene kompatible Streamadresse.
- **Sensoren zu ausgewählter Kamera übernehmen** ordnet die gewählten Sensoren einer vorhandenen HA-Kamera zu; Streamadresse und Verbindungsart bleiben dabei erhalten. Auch ohne HA-Kamera-Entität ist reine Sensorzuordnung möglich. Keine Zuordnung löscht das betreffende Sensorfeld nach ausdrücklicher Übernahme.
- Neue Kameras werden anhand der HA-Entitätskennung bzw. Streamadresse auf Duplikate geprüft. Bestehende Einträge werden ausgewählt statt überschrieben. Erst Speichern im Einstellungsdialog persistiert die Änderungen; Abbrechen verwirft sie.
- Suche und Videotest sind asynchron, zeitlich begrenzt und abbrechbar. Das Schließen beendet laufende Vorgänge. HA muss vom PC erreichbar sein; Zertifikatsausnahmen folgen ausschließlich der bereits vorhandenen HA-Einstellung.

Bei HA-HLS bleibt die Kamera-Entität als dauerhafte Quelle gespeichert. Zur Laufzeit werden frische HA-Streamadressen ermittelt und über einen nur an 127.0.0.1 gebundenen Endpunkt mit zufälliger Sitzungskennung weitergereicht. Bearer-Token und temporäre HLS-Adressen werden weder in Kamera-URLs gespeichert noch protokolliert. Weiterleitungen und fremde Hosts werden abgewiesen; die Weiterleitung ist auf HLS-Pfade desselben HA-Servers begrenzt. Die bestehende Handhabung des globalen HA-Tokens bleibt unverändert. HLS kann mehr Verzögerung als ein direkter RTSP-Stream haben.

Geprüft mit lokalen HA-WebSocket-/HTTP-Testservern: Anmeldung, Antwortzuordnung, fehlende Registerrechte, Abbruch, Gerätezuordnung und Mehrdeutigkeit, Offline-Kameras, Duplikate, bestehende Konfiguration, sichere Laufzeit-Serialisierung sowie echte FFmpeg-Dekodierung über den HLS-Endpunkt. Keine echten HA-Zugangsdaten wurden dafür gelesen oder verändert. Die Fensterdarstellung aus Beta29 bleibt erhalten; Beta30 bleibt zurückgezogen.

## Beta 29: Tatsächliche Originalgröße des Symbols

Die Varianten verwenden jetzt direkt das größere Original aus HomeCamMonitor-Beta.ico statt der kleineren separaten PNG-Datei. Jede ICO-Stufe wird direkt aus ihrem PNG-Frame gelesen; dadurch bleibt insbesondere die 256-Pixel-Stufe vollständig erhalten. Alle Ausgaben werden gegen Frame-Abmessungen und sichtbare Originalgeometrie geprüft.

## Beta 28: Ursprüngliches Logo mit rechteckigem Hintergrund

Der Kreis aus Beta 26 entfällt. Standard ist das ursprüngliche weiße Logo ohne Verkleinerung auf schwarzem Hintergrund mit abgerundeten Ecken. Die PNG-Abmessungen bleiben 256 × 256, die ursprünglichen ICO-Größen 16/24/32/48/64/128/256 Pixel bleiben erhalten. Das transparente Original bleibt unverändert; eine schwarze Variante mit identischem Alphakanal kommt hinzu. V1 bleibt unverändert.

## Beta 27: Kamerasuche – Titelleiste und Herstellerhinweis

Der Suchdialog verwendet die dunkle Windows-Titelleiste und das Anwendungssymbol. Fehlende Herstellerdaten aus Discovery werden als noch nicht abgefragt gekennzeichnet. Der Hinweis erklärt den Abruf mit Kamera-Anmeldung über ONVIF-Profile laden. Leere Herstellerangaben aus GetDeviceInformation überschreiben keine bereits bekannten Angaben; nicht gemeldete und fehlgeschlagene Abfragen werden unterschieden.

## Beta 26: Kontrastreiches Windows-Symbol

Das bestehende weiße Logo hat im V2-Anwendungssymbol eine dunkle, dezent umrandete Fläche. Neun Symbolgrößen von 16 bis 256 Pixeln halten es im hellen und dunklen Windows-Design sichtbar. Anwendung, Setup und Deinstallation verwenden dasselbe Symbol. Die V1-Symbole und das Logo in leeren Kamerafeldern bleiben unverändert.

## Beta 25: Lokale Kamerasuche auf dem Windows-PC

Unter **Verbindungseinstellungen → ONVIF / RTSP → Kameras im Netzwerk suchen** öffnet sich der neue Suchdialog. Es werden weder beim Start der Anwendung noch beim Öffnen des Dialogs automatisch Netzwerke untersucht.

1. Einen aktiven IPv4-Netzwerkadapter und den gewünschten privaten IPv4-Bereich auswählen. Einzeladressen und CIDR /20 bis /32 werden unterstützt; Beispiel `192.168.189.0/24`. Größere Netze sind auf höchstens 4096 Adressen begrenzt.
2. **Kameras im Netzwerk suchen** sendet ONVIF-WS-Discovery-Probes über den ausgewählten Adapter (UDP-Multicast 239.255.255.250:3702). Passende Antworten werden anhand von Anfragekennung, Quelladresse und Geräteadresse geprüft. Name/Modell, Hersteller und IP werden angezeigt, soweit das Gerät sie meldet. Hersteller/Modell werden beim Profilabruf zusätzlich über ONVIF gelesen.
3. Gerät auswählen, erforderliche Kamera-Zugangsdaten eintragen und **ONVIF-Profile laden**. Profil auswählen und **Verbindung testen**. Erst wenn FFmpeg tatsächlich ein Videobild dekodiert hat, ist **Kamera übernehmen** verfügbar. Exitcode 0 ohne Bild reicht nicht aus.
4. Ohne ONVIF-Antwort kann **RTSP-Ports gezielt prüfen** den eingetragenen Bereich untersuchen. Standardports sind 554, 8554 und 10554; bis zu 16 Ports sind auswählbar. Es laufen höchstens 32 Prüfungen gleichzeitig. Ein offener TCP-Port wird als unbestätigter Kandidat angezeigt; eine gültige RTSP-Antwort bestätigt nur den Dienst, noch keinen Stream. Ohne ONVIF muss der gerätespezifische RTSP-Pfad eingetragen und ebenfalls per Videotest bestätigt werden. Es werden keine Passwörter über das Netz ausprobiert.
5. Die Übernahme ergänzt die Kameraliste. Identische ONVIF-Kennungen, Geräteadressen oder RTSP-Adressen führen zur Auswahl des vorhandenen Eintrags; vorhandene Einstellungen werden nicht überschrieben. Erst **Speichern** im Einstellungsdialog übernimmt die Änderung dauerhaft. **Abbrechen** lässt die bestehende Konfiguration unverändert.

Suche, Profilabruf und Videotest sind asynchron und abbrechbar. Das Schließen des Suchfensters beendet laufende Vorgänge; FFmpeg-Testprozesse werden beendet. Passwörter bleiben im Dialog nur im Arbeitsspeicher und werden bei Übernahme mit dem bestehenden Windows-DPAPI-Verfahren geschützt. Rohantworten, Zugangsdaten und FFmpeg-Fehlerausgaben werden nicht gespeichert oder protokolliert. Von Discovery gemeldete Mediendienste dürfen Zugangsdaten nicht an andere Hosts weiterleiten.

WS-Discovery-Multicast bleibt normalerweise im lokalen Netzwerksegment. Firewall, Gast-WLAN, Client-Isolation oder VLAN-Grenzen können Antworten verhindern. Die gezielte Portsuche funktioniert über vorhandene Routen und Freigaben, richtet diese aber nicht ein. Fehlende ONVIF-Unterstützung, ausgeschaltete RTSP-Dienste oder unbekannte RTSP-Pfade können nicht durch einen offenen Port ersetzt werden. Die Suche ist auf private IPv4-Netze und IP-basierte ONVIF-Geräteadressen beschränkt; IPv6 und Bonjour/mDNS wurden nicht ergänzt, da im vorhandenen Stack keine solche Bibliothek verwendet wird und ONVIF die benötigten Profile direkt liefert. Die manuelle Einrichtung bleibt verfügbar.

Geprüft mit lokalen UDP-, HTTP-, ONVIF- und RTSP-Testservern: Discovery-Korrelation und Dubletten, sichere XML-Verarbeitung, Bereichsgrenzen, Abbruch, HTTP/RTSP-Unterscheidung, Geräteinformationen, Begrenzung von Credential-Weitergabe, tatsächliche Videodekodierung und unveränderte vorhandene Kameras. Auf MK-PC20 wurde ein realer lokaler ONVIF-Suchlauf ohne Zugangsdaten ausgeführt; er erhielt keine Antworten. Daraus folgt keine Aussage über die Ursache oder die Erreichbarkeit anderer VLANs.

Protokollreferenzen: [ONVIF Discovery](https://www.onvif.org/wp-content/uploads/2016/12/ONVIF_WG-APG-Application_Programmers_Guide-1.pdf), [RTSP](https://www.rfc-editor.org/rfc/rfc2326/).

## Beta 24: Verbindungseinstellungen standardmäßig eingeklappt

Beim Öffnen der Einstellungen ist der Verbindungsbereich immer eingeklappt. Die Kopfzeile zeigt Kamera und zugeordnete Verbindungsart weiterhin an. Ein Klick öffnet den Bereich; beim nächsten Öffnen des Einstellungsdialogs startet er erneut eingeklappt. Die dunklen Verbindungsbuttons bleiben unverändert.

## Beta 23: Verbindungsbuttons wieder im bisherigen Stil

Die drei Verbindungsbuttons sind wieder einheitlich dunkel, ohne invertierte Farben, weißen Auswahlrahmen oder den Zusatz „Verwendet“. Der geöffnete Reiter wird wie zuvor durch fette Schrift markiert. Der einklappbare Bereich direkt unter der Kameraliste bleibt erhalten; seine Kopfzeile zeigt weiterhin Kamera und tatsächlich zugeordnete Verbindungsart. Der UID-Dienst bleibt unverändert.

## Beta 22: Verbindungseinstellungen direkt unter der Kameraliste

Die Verbindungseinstellungen stehen direkt unter Kameraliste und Sortierknöpfen. Ein Klick auf die Kopfzeile klappt sie ein oder aus. Die Kopfzeile zeigt weiterhin Kamera, zugeordnete Verbindungsart und gegebenenfalls „Kamera deaktiviert“. Der Zustand bleibt beim Wechsel der Kamera innerhalb des Dialogs erhalten.

Die tatsächlich zugeordnete Verbindungsart hat einen hellen Button mit dunkler Schrift und dem Zusatz „Verwendet“. Ein weißer Rahmen markiert den gerade geöffneten Reiter. Das Öffnen eines anderen Reiters ändert die Zuordnung nicht. Die Aufnahmeoptionen bleiben separat sichtbar, wenn sie für die ausgewählte Kamera eingeschaltet sind.

Geprüft: Position vor den allgemeinen Einstellungen, wiederholtes Ein-/Ausklappen mit Kamerawechsel, Farbkontrast nach Anwendung des dunklen Designs, unveränderte Quellen beim Öffnen anderer Reiter sowie bisherige Sortier-, Lösch- und Scrollfunktionen. Der UID-Verbindungsdienst aus Beta 21 bleibt unverändert.

## Beta 21: Begrenzte Live-Pipeline im UID-Verbindungsdienst

Der Nutzer bestätigt einen während der Wiedergabe wachsenden Rückstand von rund 15 Sekunden zwischen HA und UID derselben Kamera. Deshalb wird zusätzlich zur Playerkorrektur aus Beta 20 der Dienst in der Variante `homecam-stream3` mit einer Live-Pipeline ausgeliefert.

- Die Quellen verwenden Zeitformat und Live-Modus statt einer suchbaren Nicht-Live-Quelle. Begrenzte Warteschlangen mit Rückstaukontrolle ersetzen den nicht blockierenden, nur beratenden 50-MB-Puffer. Der Appsrc-Puffer ist auf 2 MiB beziehungsweise 500 ms begrenzt, der Zwischenkanal auf acht Puffer.
- Queue2 verwendet keine Download-Pufferung mehr; sein Zeitlimit sinkt von fünf Sekunden auf 250 ms. Es werden dort keine beliebigen komprimierten Pakete verworfen.
- Beim Einstieg wird nur der jüngste Bildabschnitt ab seinem I-Frame übernommen. Fehlt dieser, wartet der Dienst auf ein neues I-Frame. Nach Verlust von Quellpaketen werden abhängige Bilder erst wieder ab einem I-Frame weitergegeben.

Die tatsächliche Verzögerung über Kamera, Internet und Relay muss beim Nutzer verglichen werden. Lokale Tests ersetzen diese Messung nicht. UID-Anmeldung, Netzwerkfreigaben und Kameraeinstellungen werden nicht geändert. Home Assistant und direkte ONVIF-/RTSP-Verbindungen verwenden diesen Dienst nicht.

Lokaler Integrationstest der Produktionspipeline: 400/400 vollständige RTP-Bilder bei 50-ms-Takt, maximal 72 ms Zeitstempelrückstand und am Ende kein gestauter Appsrc-Puffer. Zusätzlich hat der mitgelieferte mpv 40 Bilder über die echte RTSP-Factory und Loopback-TCP dekodiert. Die bisherigen Tests für Videozuordnung und UDP-ACK-Empfangsrate bleiben erhalten. Diese Werte sind keine Messung der entfernten UID-Kamera.

Referenzen: [GStreamer Appsrc](https://gstreamer.freedesktop.org/documentation/app/appsrc.html), [Queue2](https://gstreamer.freedesktop.org/documentation/coreelements/queue2.html).

## Beta 20: UID-Player behält die Option gegen Zusatzpufferung

Die TCP-Transportoption hatte den Eintrag `fflags=+nobuffer` aus dem Low-Latency-Profil überschrieben. Für UID-Kameras wird dieser Eintrag jetzt wieder ergänzt, ohne TCP zu entfernen. Das gilt für Einzelbild, Viererraster und den periodischen Playerwechsel. HA- und direkte ONVIF-/RTSP-Player behalten ihre bisherigen Optionen. UID-Verbindungsdienst, Anmeldung, Netzwerkwege und Kameraeinstellungen bleiben unverändert.

Geprüft mit den tatsächlich wirksamen Optionen des mitgelieferten mpv sowie einem lokalen H.264-RTSP-Stream. Zwei Vergleichspaare liefen jeweils sieben Sekunden ohne Abbruch; erste Wiedergabe nach rund 0,11–0,23 Sekunden. Der Test belegt die gültigen Optionen und laufende Wiedergabe, keine belastbare Verbesserung der realen UID-Verbindungszeit. Der P2P-/Relay-Aufbau und die Verzögerung über die tatsächliche Kamera müssen im Einsatz verglichen werden. Es wurden keine echten Kameraverbindungen für diese Entwicklung geöffnet.

Playerreferenz: [mpv Low-Latency-Profil](https://github.com/mpv-player/mpv/blob/master/etc/builtin.conf).

Separate Test-Anwendung für Reolink-UID, Home Assistant und direkte ONVIF-/RTSP-Bildverbindungen. Die stabile V1 und ihre Einstellungen bleiben getrennt.

## Beta 19: Kamera löschen

„Kamera löschen“ neben den Sortierknöpfen entfernt die ausgewählte Kamera aus der Liste. Alternativ funktioniert Entf, solange kein Feld bearbeitet wird. Die Änderung wird erst mit „Speichern“ übernommen; „Abbrechen“ verwirft sie. Die Kamera selbst und vorhandene Aufnahmen werden nicht gelöscht.

Die nächste Zeile wird ausgewählt und ihre Verbindung sowie Aufnahmeoptionen geladen. Ohne Kameraauswahl ist der Knopf gesperrt. Tests prüfen das Löschen mittlerer, erster und letzter Einträge, unveränderte Einstellungen und Zugangsdaten der übrigen Kameras sowie das Leeren der gesamten Liste.

## Beta 18: Gesperrte Kamerakästchen sichtbar grau

Gesperrte Kontrollkästchen erhalten eine dunkle graue Fläche mit grauem Rand und gegebenenfalls grauem Häkchen. Die gespeicherte Auswahl bleibt erhalten. Bei ausgeschalteten Kameras sind Bewegung, Person und automatische Aufnahmen gesperrt; „Aktiv“ bleibt bedienbar. Nach dem Einschalten sind unterstützte Optionen wieder verfügbar.

Bei Reolink-UID bleibt Person gesperrt. Bei der derzeit nur bildfähigen ONVIF-/RTSP-Verbindung sind Bewegung, Person und automatische Ereignisaufnahmen gesperrt; manuelle Aufnahmen bleiben möglich. Tests prüfen die tatsächliche graue Darstellung, die Sperren und das Wiederaktivieren sowie den Erhalt gespeicherter Einstellungen.

## Beta 17: Kameras ohne springende Einstellungen sortieren

Beim Verschieben wird die Kameratabelle als eine Aktualisierung behandelt. Scrollposition, Tabellenhöhe, ausgewählter Verbindungsreiter und Aufnahmebereich bleiben stehen; nur die ausgewählte Kamerazeile wechselt ihren Platz. Zwischenzustände beim Entfernen und Einfügen der Zeile lösen keine Auswahl- oder Layoutwechsel mehr aus.

Geprüft mit wiederholtem Verschieben über HA- und UID-Kameras mit unterschiedlichen Aufnahmeoptionen: keine Größen- oder Sichtbarkeitswechsel, gleichbleibende Scrollposition und unveränderte Zuordnung der Kameraeinstellungen und Zugangsdaten.

## Beta 16: Ereigniserkennung ausschließlich pro Kamera

- Der Hauptschalter „Bewegungs- und Personenerkennung aktiv“ entfällt aus den Einstellungen. Ob Ereignisse verarbeitet werden, bestimmen die Spalten „Aktiv“, „Bewegung“ und „Person“ je Kamera sowie die verfügbare Verbindungsart.
- Auch der entsprechende Dauerschalter im Kontextmenü und „Bis manuell aktiviert“ entfallen. Die zeitlich begrenzte Pause für Aktionen bleibt separat verfügbar.
- Ein früher gespeichertes globales „aus“ wird beim Laden oder Importieren ignoriert. Dadurch kann es die pro Kamera gesetzten Häkchen nicht mehr unbemerkt blockieren. Neue Sicherungen enthalten diesen Hauptschalter nicht mehr.
- Reine Personenerkennung bleibt auch ohne aktivierte Bewegung möglich. Deaktivierte Kameras und die derzeit nur bildfähige ONVIF-/RTSP-Verbindung aktivieren keine Ereigniserkennung.

Geprüft: Übernahme alter Einstellungen mit ausgeschaltetem Hauptschalter, reine Personenerkennung, deaktivierte und direkte Kameras, Entfernung aus Einstellungsdialog und Kontextmenü sowie die bisherigen Quell-, Dropdown- und Scrolltests. V1 behält ihre bisherige Bedienung.

## Beta 15: ONVIF / RTSP und dunkle Dropdowns

- Dritte Registerkarte „ONVIF / RTSP“ für direkt vom PC erreichbare Netzwerkkameras. Bestehende Kameras behalten ihre bisherige Verbindungsart; es gibt keinen automatischen Wechsel oder IP-Fallback für UID-Kameras.
- „Netzwerkkamera hinzufügen“ legt einen Eintrag an. Kamera-Benutzer und Passwort eintragen, dann entweder die RTSP-Adresse direkt angeben oder die ONVIF-Adresse einschließlich Port setzen und „ONVIF-Profile laden“ wählen. Ein Profil auswählen und „Für ausgewählte Kamera verwenden“ aktivieren. „Stream testen“ prüft, ob ein echtes Videobild empfangen wird.
- ONVIF liest Gerätezeit, Media-Dienst, Profile und RTSP-Streamadressen über SOAP mit WS-Security-Passwortdigest sowie HTTP-Authentifizierung. Die erste Umsetzung verwendet ONVIF Media 1 / Profile S; Media 2, automatische Gerätesuche, PTZ und ONVIF-Ereignisabonnements sind nicht enthalten.
- **Direkte ONVIF-/RTSP-Verbindungen liefern derzeit nur das Bild.** Bewegung und Person sind dort ausgegraut; automatische Ereignisaufnahmen und deren Vorlauf starten nicht. Manuelle Aufnahmen funktionieren über den Bildstream. HA- und UID-Ereignisse funktionieren für ihre jeweiligen Kameras weiter.
- Je Kamera ist genau eine Verbindungsart aktiv. HA-Adresse, UID-Konfiguration und direkte Kamerakonfiguration bleiben beim Wechsel erhalten. Leere Passwortfelder behalten das gespeicherte Passwort. Sicherungen enthalten weiterhin Zugangsdaten und müssen vertraulich behandelt werden.
- Alle Dropdowns der V2-Einstellungen verwenden eine dunkle Fläche, dunklen Pfeil und dunkle Auswahlliste. Lange Listen haben den schwarzen Scrollbalken der Beta 14. Maus- und Tastaturauswahl werden unterstützt.

Voraussetzung ist vorhandene Erreichbarkeit der Kamera sowie deren aktivierte ONVIF-/RTSP-Dienste. Die Anwendung richtet weder VLAN-Routen noch Firewallfreigaben ein. Für entfernte oder weiter abgeschottete Kameras bleibt Reolink-UID verfügbar.

Geprüft mit einer lokalen ONVIF-Testkamera: WS-Security-Digest, Profil- und Streamauslesen, Wechsel aller drei Quellen, geschützte Passwörter, Sicherung/Rücksicherung und bereinigte Diagnose. Dropdowns wurden geöffnet, gerendert und per Tastatur bedient; die Scroll- und Darstellungstests der bestätigten Beta 14 bestehen weiter. Keine echte Kamera und kein VLAN wurden für die Entwicklung angesprochen oder verändert.

Protokollreferenz: [ONVIF-Spezifikationen](https://www.onvif.org/profiles-specifications-new/).

## Erste Verbindung mit der RLC-410W

1. V2-Beta-Setup installieren. Standardordner: `Program Files\HomeCamMonitor-V2-Beta`.
2. In den Einstellungen die Registerkarte „Reolink“ öffnen und „Reolink-Kamera hinzufügen“ anklicken. Alternativ legt das Aktivieren von „Direkt über Reolink-UID verbinden“ bei leerer Liste automatisch die erste Kamera an.
3. UID aus der Reolink-App unter Geräteinformationen sowie **Benutzername und Passwort der Kamera** eintragen. Dies sind nicht die Zugangsdaten des Reolink-App-Kontos.
4. „Verbindung testen“ wählen und anschließend speichern. „Flüssig“ verwendet den Substream; „Hohe Auflösung“ den Hauptstream.

Die benötigten Verbindungs- und Video-Komponenten sind enthalten. Für die direkte Reolink-Anbindung muss kein zusätzlicher Home-Assistant- oder MQTT-Server eingerichtet werden. UID-Verbindungen verwenden öffentliche P2P-/Relay-Wege. Kamera und PC benötigen dafür passenden Internetzugriff; ein direkter Zugriff auf die private Kamera-IP oder eine Freigabe über die VLAN-Grenze ist nicht erforderlich.

## Beta 14: gemeinsame stabile Scrollfläche und schwarzer Scrollbalken

- Die Kameratabelle scrollt wieder gemeinsam mit den übrigen Einstellungen. Sie zeigt alle Kamerazeilen und hat keinen eigenen Scrollbalken mehr. Ihre Spalten und die Anordnung der Felder bleiben beim Scrollen unverändert.
- Die Seite verwendet eine gemeinsame Inhaltsfläche, die als Ganzes verschoben wird. Native AutoScroll-Kopien und ein Neuanordnen der Felder während des Scrollens entfallen.
- Rechts steht ein schmaler schwarzer Scrollbalken mit abgerundetem dunklem Griff. Mausrad, Ziehen, Klicks auf die Spur und Tastaturbedienung mit Pfeilen, Bild auf/ab sowie Pos1/Ende werden unterstützt.
- Beim Wechsel des Tastaturfokus wird das neue Feld sichtbar gemacht; normale Layoutdurchläufe ändern die Scrollposition nicht. Die flachen Verbindungsreiter und dünnen Rahmen aus Beta 13 bleiben erhalten.

Geprüft: gemeinsames Scrollen von Tabelle und Feldern bei gleichbleibenden Maßen, native Mausradmeldungen im modalen Fenster, Ziehen und Tastaturbedienung des Scrollbalkens, schwarze Spur im gerenderten Bild, Aufnahmeoptionen und Kamerareihenfolge. Die tatsächliche Darstellung am Bildschirm des Nutzers bleibt nach Installation zu bestätigen.

## Beta 13: feste Kameraliste und flache Verbindungsreiter

- Die Kameraliste samt Reihenfolge-Schaltflächen bleibt oben stehen. Nur die Einstellungen darunter scrollen. Die Tabelle wird beim Scrollen weder verschoben noch jedes Mal vollständig neu gezeichnet und verwendet eine eigene Pufferung.
- Wiederholte Layoutdurchläufe ziehen ein unverändert fokussiertes Feld nicht mehr zurück in den sichtbaren Bereich. Ein echter Fokuswechsel über die Tastatur behält das automatische Sichtbarmachen bei.
- Während einer nativen Mausrad- oder Scrollleistenbewegung bleibt das bisherige Bild stehen, bis alle untergeordneten Fenster verschoben sind. Danach wird nur der betroffene Bereich einmal vollständig neu gezeichnet. Der Rest des Fensters wird dabei nicht mehr vorsorglich gelöscht und neu aufgebaut.
- Die Reolink-/Home-Assistant-Auswahl verwendet flache Reiter. Der breite native Windows-Rahmen entfällt; beide Verbindungsbereiche verwenden denselben dünnen Gruppenrahmen wie die übrigen Einstellungen.

Geprüft werden zusätzlich die feste Bildschirmposition und Spaltenbreite der Kameraliste, Mausrad-Scrollen im echten modalen Fenster, ausbleibendes wiederholtes Neuzeichnen der festen Tabelle und das Beibehalten der Scrollposition bei wiederholtem Fokus-Layout. Die Darstellung auf dem konkreten Bildschirm bleibt nach Installation zu bestätigen.

## Beta 12: Aktiv zuerst und vollständiges Einblenden

- „Aktiv“ steht wieder als erste Kameraspalte. „Aufnahmen“ bleibt bei den übrigen Kontrollspalten.
- Das Einstellungsfenster bleibt während der ersten Darstellung unsichtbar, bis auch die verzögerte Windows-Meldung „Shown“ samt endgültiger Größe und Position verarbeitet wurde. Anschließend wird der gesamte Inhalt synchron gezeichnet und vollständig deckend eingeblendet.
- Windows hält danach eine gepufferte Oberfläche für das ganze Fenster vor. Nach Scrollen und Mausradbewegungen wird der vollständige Inhalt sofort neu gezeichnet, damit versetzte Bildteile nicht bis zur nächsten Leerlaufphase stehen bleiben.

Der neue Test verwendet echte modale Einstellungsfenster mit aktivierten Windows-Visual-Styles, jeweils mit und ohne vorbereitete Steuerelemente. Er kontrolliert das erste Zeichnen vor dem deckenden Einblenden sowie Scrollen und ruhendes Neuzeichnen. Die Testfenster bleiben außerhalb des sichtbaren Desktops; gespeicherte Nutzereinstellungen werden dabei nicht verändert. Ob das Flackern auf dem konkreten Bildschirm vollständig behoben ist, muss nach Installation bestätigt werden.

Technischer Hintergrund zur umgeleiteten Fensterdarstellung: [Microsoft – Layered Windows](https://learn.microsoft.com/en-us/windows/win32/winmsg/window-features#layered-windows).

## Beta 11: Kamerareihenfolge, Aufnahmen und Darstellung

- Die Kontrollspalten stehen zusammen: Bewegung, Person, Aktiv und Aufnahmen.
- „Aufnahmen“ zeigt den unteren Aufnahmebereich für die ausgewählte Kamera. Ohne Haken sind automatische Bewegungs- und Personenaufnahmen sowie der Vorlauf für diese Kamera aus; die Aufnahmeoptionen bleiben gespeichert. Manuelle Aufnahmen bleiben verfügbar. Bestehende Einstellungen behalten ihre bisherige Aufnahmefreigabe.
- „Nach oben“ und „Nach unten“ verschieben die ausgewählte Kamera in der Liste und damit im Kameraraster. UID-Zugangsdaten, Sensoren, Aufnahmeoptionen und die Wahl der Startkamera bleiben zugeordnet.
- Der scrollbare Einstellungsbereich und seine Layoutflächen werden gepuffert. Nach dem Scrollen wird der Inhalt vollständig neu gezeichnet. Auch der nicht vorbereitete Öffnungspfad erstellt seine Steuerelemente vor dem Anzeigen.

Geprüft mit fünf Testkameras: Aufnahmebereich beim Kamerawechsel und Umschalten, wiederholtes Verschieben einer UID-Kamera samt geschütztem Passwort, Sicherung/Rücksicherung sowie Scrollen, Größenänderung und ruhendes Neuzeichnen. Die konkrete Darstellung am Bildschirm des Nutzers muss nach Installation bestätigt werden.

## Beta 10: beide Verbindungsarten und aktive Kameras

- Unter „Reolink“ und „Home Assistant“ wird die ausgewählte Kamera eingerichtet. Beide Arten können in derselben Installation für unterschiedliche Kameras laufen. „Direkt über Reolink-UID verbinden“ beziehungsweise „Für ausgewählte Kamera verwenden“ legt die Verbindungsart fest. HA-Streamadresse, Sensorzuordnungen und Reolink-Zugangsdaten bleiben beim Wechsel erhalten.
- Für HA-Kameras eine RTSP-/HTTP-Streamadresse wie in V1 eintragen. HA-Adresse und Langzeit-Token werden für die Bewegungs-/Personenereignisse verwendet. Die Streamadresse wird nicht automatisch aus einer HA-Kamera-Entität ermittelt. Es wird kein zusätzlicher eingehender Ereignis-Port geöffnet.
- Die neue Spalte „Aktiv“ schaltet eine Kamera vollständig aus: Sie wird im Raster und beim Wechsel übersprungen und erhält keine UID-Sitzung, Bewegungs-/Personenaktionen oder Vorlauf-Aufzeichnung. Ihre Konfiguration bleibt gespeichert. Bestehende Kameras sind nach dem Update aktiv.
- „Person“ ist bei UID-Kameras ausgegraut. Bei HA-Kameras ist es nur verfügbar, wenn die Kamera aktiv ist, HA-Ereignisse und Erkennung aktiviert sind und eine Personen-Entität hinterlegt wurde. Eine vorhandene Auswahl bleibt für einen späteren Wechsel zurück zu HA erhalten.

Geprüft: Migration älterer Einstellungen, Sicherung deaktivierter Kameras, Umschalten beider Verbindungsarten ohne Verlust der Streamadresse, Sperren der Personenoption und Ereigniszuordnung nach Verbindungsart/aktivem Zustand. Ein lokaler WebSocket-Test prüft HA-Anmeldung und das Ereignis-Abonnement zusammen mit UID-Kameras. Die bisherigen Tests für UID-Konfiguration, geschützte Passwörter, lokale Ereignisse, Kamera-Isolation und ruhiges Einstellungs-Layout bestehen weiterhin. Die HA-Oberfläche wurde mit Testdaten gerendert; eine vorhandene reale HA-Installation wurde nicht verändert oder geprüft.

## Bewegung und Aufnahmen

Das Aktivieren der UID-Verbindung schaltet Bewegung für diese Kamera und die globale Bewegungserkennung ein. Die Kamera muss selbst Bewegungserkennung aktiviert haben. Das Fenster kann bei Bewegung erscheinen; Snapshot-/Videoaktionen und Anzeigedauer werden wie bisher in den Einstellungen gewählt. Eine laufende Bewegung löst einmal beim Beginn aus, statt dasselbe Ereignis mehrfach zu zählen.

Die direkte Reolink-UID-Anbindung übernimmt **allgemeine Bewegung**. Personenereignisse können über Home Assistant mit einer passenden Personen-Entität verarbeitet werden. Fahrzeugfilter sind nicht implementiert. Bei der RLC-410W hängt die Smart Detection außerdem von der Hardware-Version ab; die Modellbezeichnung allein reicht dafür nicht.

Der Verbindungsstatus wird in den Kamera-Einstellungen angezeigt. Bei Verbindungsabbruch erfolgen neue Versuche. „Verbunden“ beschreibt die Kamera-Verbindung; die Bildübertragung wird zusätzlich vom bestehenden Player überwacht.

Beta 5 zeigt beim Verbindungstest die UID-Suche, P2P-/Relay-Versuche und Anmeldung getrennt an. Der Test wartet bis zu 120 Sekunden. `[UID-REG]` bedeutet, dass die UID-Vermittlung fehlgeschlagen ist; `[AUTH]` bedeutet, dass die Kamera die Anmeldung abgelehnt hat. Es werden ausschließlich feste Diagnosemeldungen angezeigt, keine rohen Ausgaben des Verbindungsdienstes.

## Zugangsdaten und Trennung von V1

Das gespeicherte Kamera-Passwort ist mit Windows DPAPI für das aktuelle Windows-Konto verschlüsselt. Nach Übertragen einer Sicherung auf einen anderen PC oder ein anderes Windows-Konto muss das Passwort erneut eingegeben werden. Kamera-UID und Benutzername werden als Konfiguration gespeichert.

Der externe Verbindungsdienst benötigt während einer Sitzung eine entschlüsselte Konfiguration. Diese liegt in einem ausschließlich für das aktuelle Windows-Konto zugänglichen temporären Sitzungsordner. Beim normalen Stop wird sie entfernt. Nach einem Absturz werden Hilfsprozesse durch Windows beendet; zurückgebliebene Sitzungsdateien werden beim nächsten Start des Verbindungsdienstes aufgeräumt. Hilfsprozess-Ausgaben werden nicht protokolliert.

- Programm: `HomeCamMonitor-V2-Beta.exe`
- Einstellungen, Diagnose und Aufnahmen-Verzeichnisliste: `%LOCALAPPDATA%\HomeCamMonitor-V2-Beta`
- Registry, Deinstallation und Autostart: eigene Einträge unter `HomeCamMonitor-V2-Beta`
- Startmenü: `HomeCam Monitor V2 Beta`
- Aufnahmen: eigene Unterordner `HomeCam Monitor V2 Beta` in Bilder und Videos

V1-/alte Beta-Einstellungen werden nicht automatisch importiert. Der Installer verhindert die Installation in einen Ordner mit der stabilen oder alten Beta-Programmdatei.

## Prüfstand

Build, verschlüsselte Passwortspeicherung, Einstellungen, Sicherung, Ereigniszuordnung, lokaler authentifizierter Ereigniskanal und Abschaltung wurden mit simulierten Kamerameldungen geprüft. Beta 8 erreichte mit der ersten gespeicherten Kamera RTSP 200 und zweimal knapp drei Minuten dekodiertes Video. Dabei blieben einzelne Decoderfehler; ein vollständiger mpv-Dauertest und reale Bewegungssignale sind nicht bestätigt. Die zweite Kamera lehnte im lokalen Test die gespeicherte Anmeldung ab. Einzelheiten und Testgrenzen stehen im Abschnitt zu Beta 8.

Quellen und Fremdkomponenten-Hinweise: `reolink-v2-components.md`. Dieses Paket wurde lokal erstellt und nicht als öffentlicher Release veröffentlicht.

## Korrekturen in Beta 3

Reolink lässt sich direkt bei leerer Kameraliste aktivieren. Das Hinzufügen legt einen Kameraeintrag mit eingeschalteter Bewegung an. Beim Wechsel der Tabellenzelle bleiben UID-Eingaben und Aktivierung erhalten. Die Verbindungskonfiguration wird beim Leerlauf nicht neu geladen, unveränderte Statusmeldungen lösen keine Textänderung aus, und die Breite des Reolink-Bereichs ist fest an die Fensterbreite gebunden.

## Korrektur in Beta 4

Der ständig neu zeichnende Windows-Fenstermodus WS_EX_COMPOSITED wurde für den V2-Einstellungsdialog entfernt. Die normale Doppelpufferung bleibt aktiv. Ein neuer Test zeigt den vollständigen Dialog und zählt native Zeichenereignisse über drei Sekunden; die vorherige Version erzeugte über 100 Ereignisse pro betroffenem Steuerelement, die korrigierte Version nur zwei. Die früheren reinen Positionsprüfungen konnten diesen Fehler nicht erkennen.

## UID-Korrektur in Beta 6

Ein veralteter Eintrag in der Neolink-Suchserverliste zeigte auf `127.0.0.1`. Der daraus mögliche UDP-Verbindungsreset kann die Suche abbrechen. Die lokale Variante `homecam-uid1` ersetzt diesen Eintrag durch einen bereits verwendeten Reolink-Server. Details, Prüfsummen, Änderungsvermerk und vollständige geänderte Neolink-Quellen werden mitgeliefert. Gespeicherte Kamera-Einstellungen bleiben beim Update erhalten.

## Streamstart in Beta 7

Der Player öffnet den lokalen Reolink-Stream erst nach dessen Bereitmeldung durch Neolink. Diese Meldung erfolgt nach Erkennen des Videoformats und Anlegen des RTSP-Endpunkts; eine Kamera-Anmeldung allein reicht dafür nicht. Der Player startet dann unmittelbar über ein Ereignis, auch im Kameraraster. Zuvor begannen die Player-Versuche bereits während UID-Suche und Streamvorbereitung und konnten in zusätzliche Timeout-/Wiederholungszyklen laufen. Solange der erste Videostream vorbereitet wird, erscheint „Verbinde …“.

Diese Änderung beseitigt vorzeitige Player-Aufrufe, garantiert jedoch keine bestimmte Startdauer. Im lokalen Test konnten Haupt- und Substream zeitweise nicht dekodiert werden (RTSP 400, noch kein gültiges Videoformat). Die separat geprüfte Neolink-Vorabversion 0.6.3-rc.2 löste dies im Test ebenfalls nicht und wird nicht ausgeliefert. Die Behebung der Verzögerung innerhalb der Kamera-/Neolink-Streamverarbeitung bleibt offen.

## Streamkorrektur und Kameratrennung in Beta 8

Der eigenständige Neolink-Dienst wurde als `homecam-stream1` aus Quellcode gebaut. Binäre Videodaten mit abweichender Nachrichtennummer werden bei genau einem aktiven Videokonsumenten dieser Kamera-Verbindung zugeordnet. Passende Nummern haben Vorrang; andere Kanäle, XML/Bestätigungen und mehrdeutige Fälle werden nicht umgeleitet. Zwei neue Regressionstests prüfen die Grenzen dieser Zuordnung. UID-Suchen überspringen ungeeignete Zieladressen; der interne Stream-Watchdog wartet 15 statt drei Sekunden.

Jede Kamera erhält einen eigenen Verbindungsdienst und lokalen RTSP-Port. Ein Fehler der zweiten Kamera soll die erste dadurch nicht mit neu starten. Abgelehnte Zugangsdaten werden nicht durch die äußere Wiederholungsschleife fortlaufend neu versucht; nach Korrektur die geänderten Einstellungen speichern. Beim Wiederverwenden unveränderter Einstellungen werden die individuellen Streamadressen beibehalten.

Für das erste Bild eines Reolink-Streams wartet der Player bis zu 30 statt zwölf Sekunden; die Überwachung eines bereits laufenden Bildes bleibt bei zwölf Sekunden. Der reale Test erreichte den ersten dekodierten Fortschritt erst nach 14,8 Sekunden, sodass die bisherige Startgrenze zu früh abbrechen konnte.

Validierung: Die erste gespeicherte Kamera lieferte im Test RTSP 200 und in zwei Läufen jeweils knapp 180 Sekunden dekodiertes Video (489 bzw. 481 Bilder). Im zweiten Lauf lag die längste gemessene Fortschrittspause bei 7,2 Sekunden. Es wurden noch sieben Invalid-Data-Fehlermeldungen gezählt; eine dauerhaft fehlerfreie Bildübertragung ist damit nicht bewiesen. Die direkte Prüfung der mpv-IPC-Überwachung war durch die Ausführungsumgebung blockiert; das ist kein bestandener mpv-Dauertest. Der reale Test der zweiten Kamera endete mit abgelehnter Anmeldung `[AUTH]`, daher ist deren Bildübertragung noch nicht geprüft. Die Fehlertrennung zweier Kameradienste wurde mit einem absichtlich fehlschlagenden Hilfsdienst getestet.


## Beta 9: UID-Empfangsrate

Die neue eigenständige Neolink-Variante `homecam-stream2` korrigiert das Feedback in UDP-Bestätigungen: Der Dienst meldet empfangene Nutzdatenbytes pro Sekunde statt des mittleren Zeitabstands eingehender Bestätigungen. Nur neue, akzeptierte Datenpakete zählen; Wiederholungen erhöhen die Rate nicht. Die Berechnung berücksichtigt die tatsächliche Fensterdauer und begrenzt den Protokollwert bei Überlauf. Drei neue Regressionstests prüfen Berechnung, Fensterwechsel und Grenzwerte. Die Stream-Zuordnung und übrigen Änderungen aus Beta 8 bleiben erhalten.

Die UID-Suche verwendet ausschließlich öffentliche P2P-/Relay-Wege (`cellular` im Neolink-Protokoll). Der Modusname bedeutet keine Mobilfunkpflicht. Lokale Kamera-Erkennung und private Kamera-Adressen werden nicht für Verbindungen verwendet.

Ein gezielter 60-Sekunden-Vergleich decodierte mit der Korrektur 395 Bilder ohne Decoderfehler und mit maximal 0,5 Sekunden Fortschrittspause. Zuvor waren es im entsprechenden Vergleich 147 Bilder, zwei Invalid-Data-Fehler und bis zu 3,1 Sekunden Pause. Die ursprünglichen H.264-Daten waren mit der Korrektur ebenfalls fehlerfrei decodierbar. Der fensterlose mpv-Test im bisherigen low-latency-Modus meldete 0,2 Sekunden maximale Pause über 45 Sekunden Medienzeit. Diese Kurztests ersetzen keine Prüfung der GPU-Ausgabe und der Offline-Erkennung in der installierten Oberfläche. Die zweite Kamera ist durch diesen Vergleich nicht als funktionierend bestätigt.

Der bereinigte Produktions-Build bestand anschließend einen weiteren UID-/Relay-Test: 1.189 decodierte Bilder über 179,9 Sekunden Medienzeit, keine Decoderfehler und maximal 0,6 Sekunden Fortschrittspause. Die erste Kamera war nach 6,7 Sekunden als Stream bereit; die erste Decoder-Fortschrittsmeldung folgte 9,5 Sekunden nach Start der Decoder-Prüfung. Der zweite Kameraeintrag wurde separat erreicht, lehnte jedoch die gespeicherte Anmeldung ab. Dafür müssen die Kamera-Zugangsdaten mit denen des funktionierenden Reolink-Clients abgeglichen werden. Keine Zugangsdaten in Fehlerberichte oder Chats aufnehmen.
