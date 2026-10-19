# Store-Kandidat – Windows-11-VM-Tests

## Standardpfade bei Neuinstallation 2.0.11.0 (19.10.2026)

Korrigiert die Beta-Bezeichnungen der frischen finalen Installation: Bilder/Videos unter `HomeCam Monitor V2`, Einstellungen unter `%LOCALAPPDATA%\HomeCamMonitor-V2`. Vorhandene explizite Einstellungsordner, alte V2-Beta-Konfigurationen und gespeicherte Aufnahmepfade werden weiterverwendet. Kein automatisches Verschieben vorhandener Dateien. Details und Testergebnisse in `storage-defaults-v2.md`. Gezielte Prüfungen Deutsch/Englisch, unveränderte Beta-Vorgaben und Lokalisierung bestanden. Zusätzlicher Gesamtlauf durch vorhandenen Dropdown-Styling-Testfehler abgebrochen; nicht als bestanden geführt. Tatsächlicher VM-Neuinstallationstest dieses Pakets steht aus. Lokale Ablage unter `../Releases/2.0.11.0`; noch keine Store-Übermittlung von 2.0.11.0. Die zuvor eingereichte Version bleibt 2.0.10.0.

## Weißer Rahmen in der Vorschau 2.0.10.0 (19.10.2026)

Auf Nutzerwunsch erhält die schwarze Standardvorschau einen weißen Außenrahmen von ungefähr 1 mm, skaliert anhand der Anzeige-DPI. Die transparenten Varianten bleiben ohne Rahmen. Variantenprüfung bestanden; gerenderte Vorschau kontrolliert. Ausschließlich lokaler VM-Kandidat. Installation und Sichtprüfung in der VM noch offen; keine Store-Übermittlung oder Veröffentlichung dieses Kandidaten.

Nachtrag 19.10.2026: Der Nutzer bestätigt die Version mit „perfekt“ und beauftragt die Store-Aktualisierung sowie eine lokale Ablage. Dies bestätigt die gewünschte Darstellung, ersetzt aber keinen vollständigen erneuten Kamera-/Deinstallationstest. Lokales Archiv: `../Releases/2.0.10.0`, einschließlich signiertem VM-ZIP, separatem unsigniertem Store-MSIX und SHA-256-Prüfsummen. Das Store-MSIX besteht die vollständige Prüfung aller 5.045 Payload-Dateien; SHA-256 `8e06d69d8270b9cfb59cbe270e29fa59f83a8718260b0b9a590fb094a1a9d012`.

Store-Update am 19.10.2026: Partner Center hat das Paket 2.0.10.0 als „Validated“ gespeichert. Prüferhinweise auf 2.0.10.0 aktualisiert und nach Neuladen bestätigt. Submission 3 (`1152921505702168323`) zur Zertifizierung übermittelt; Übersicht bestätigt „Zertifizierung wird ausgeführt“, Übermittlung abgeschlossen und Vorverarbeiten in Bearbeitung. Die bisherige manuelle Veröffentlichungsoption bleibt erhalten: Nach erfolgreicher Zertifizierung ist „Jetzt veröffentlichen“ erforderlich. Noch keine öffentliche Veröffentlichung von 2.0.10.0 bestätigt. Nachweis und Status: `../Releases/2.0.10.0/Store-Einreichung-2.0.10.0.png` und `Store-Status.json`.

## Lokaler Vorschau-Fix 2.0.9.0 (19.10.2026)

Auf Nutzerwunsch ausschließlich lokal für die VM vorbereitet. Die Logo-/Iconvorschau verwendet nun hochwertige Skalierung, DPI-gerechten Innenabstand und ein neutrales Schachbrett für transparente Varianten. Die Logo- und ICO-Quelldateien bleiben unverändert. Variantenprüfungen auf Deutsch und Englisch bestanden; gerenderte Vorschauen kontrolliert. Installation, Upgrade und Sichtprüfung dieses Kandidaten in der VM sind noch nicht bestätigt. Keine Store-Übermittlung oder Veröffentlichung dieses Kandidaten.

Stand: 18.10.2026. Testumgebung: separate lokale Hyper-V-VM „Win11“. Keine Kameraadressen, Zugangsdaten oder Kamerabilder werden in diesem Bericht gespeichert.

## Neuer Kandidat nach Veröffentlichung der Komponentenquellen

2.0.3.0 übernimmt die veröffentlichten, auditgeprüften mpv-/FFmpeg-Dateien und 3.200 freigegebenen Lizenzdateien. Das öffentliche portable V2.0.0-ZIP wurde gegen den von GitHub gemeldeten SHA-256 geprüft. Der Neolink-Dateisatz und die Store-Korrekturen aus 2.0.2.0 bleiben erhalten. Die früher versuchte Helper-Umordnung gehört nicht zu diesem Kandidaten. Wiedergabe und Aufnahme mit direkter Kamera sowie Reolink müssen wegen der geänderten Medienprogramme erneut bestätigt werden; bisherige VM-Ergebnisse gelten weiterhin ausdrücklich für 2.0.2.0.

Signiertes MSIX: `store-artifacts/win11-vm-test-2.0.3.0/HomeCamMonitor-2.0.3.0-x64.msix`, 351.966.856 Bytes, SHA-256 `2686742a1ec8ee092b2732096574dc1613ca76de75141f75e7fbb55531777583`. MakeAppx erfolgreich. Der Paketprüfer bestätigt alle 5.023 Payload-Dateien, Identität/Version, fünf Komponentenbinaries, 261 Plugins und 3.200 Lizenzdateien. Das bereits in der VM akzeptierte Testzertifikat wird weiterverwendet. Transferpaket: `store-artifacts/HomeCamMonitor-Win11-VM-Test-2.0.3.0.zip`. VM-Installation und Funktion dieses neuen Kandidaten sind noch nicht bestätigt; keine Store-Einreichung erfolgt.

**Zeitliche Klarstellung des Nutzers:** Die zuletzt übermittelten GLib-/GObject-/GIO-/GStreamer-Fehlerbilder stammen aus dem Stand **vor 2.0.2.0**. Der Nutzer bestätigt inzwischen für 2.0.2.0: keine Fehlermeldung mehr und der Stream startet. Daraus darf kein Fehlschlag der dort enthaltenen Manifest-Korrektur abgeleitet werden. Eine vorsorglich erstellte 2.0.3.0-Variante mit anderer Helper-Anordnung wurde zurückgestellt und nicht als Testpaket ausgegeben; der reguläre Quell-/Paketierungsstand ist wieder 2.0.2.0. Der Streamstart des gelieferten 2.0.2.0-Kandidaten ist damit vom Nutzer bestätigt.

| Paket | Prüfung | Ergebnis / Nachweis |
|---|---|---|
| 2.0.0.0 | MSIX-Installation und Start | Visuell bestätigt nach manuellem Zertifikatimport. App Installer bestätigt die Installation. |
| 2.0.0.0 | Livebild, Einstellungen | Visuell bestätigt; sichtbare Anwendungsversion 2.0.0. |
| 2.0.1.0 | Upgrade | Vom Nutzer als „einwandfrei“ bestätigt. |
| 2.0.1.0 | Windows-Autostart und Respektieren der Windows-Deaktivierung | Beide vom Nutzer als passend bestätigt, nach Anleitung zur Ab-/Anmeldung und Deaktivierung. |
| 2.0.1.0 | Aufnahmen einer Netzwerkkamera | Vom Nutzer als in Ordnung bestätigt. Kein automatischer Rückschluss auf Reolink-UID-Aufnahmen. |
| 2.0.1.0 | Neolink / Reolink-UID | Fehler im Nutzerscreenshot: Windows meldet fehlende `glib-2.0-0.dll` für `neolink.exe`. |
| 2.0.2.0 | MSIX-Ladersuchpfad für GStreamer | Vom Nutzer bestätigt: keine Fehlermeldung mehr, Stream startet. |

## Neolink-Fehler und Korrektur

Die vermeintlich fehlende DLL ist im geprüften 2.0.1.0-Paket unter `tools/gstreamer/bin/glib-2.0-0.dll` enthalten (SHA-256 `52ffcad1def38b1dfaa8a4aeb7abedf3f24b0825ff16cc168d25152a010eb4a4`). Der Desktop-Start setzt diesen Ordner bisher nur in `PATH`; die Standardsuchfolge für DLLs paketierter Prozesse berücksichtigt diesen Eintrag nicht wie ein unpaketierter Prozess.

Paket 2.0.2.0 ergänzt deshalb `uap6:LoaderSearchPathOverride` auf Paketebene. Enthalten sind der Paketstamm (`FolderPath=""`) und `tools\gstreamer\bin`. Der Stamm bleibt ausdrücklich erhalten, damit die nativen Anwendungsbibliotheken weiterhin gefunden werden. Es werden keine Windows-Systemdateien, globalen Suchpfade oder Kameraeinstellungen geändert; die Runtime-Dateien bleiben unverändert.

Die Paketierungs-Fixture prüft beide Suchpfade und das Vorhandensein von GLib. MakeAppx und die Prüfung des fertigen Pakets folgen im Build. Das bestätigt die Deklaration und Paketbytes, ersetzt aber keinen tatsächlichen Neolink-Start unter MSIX.

Die statische Importprüfung mit Microsoft `dumpbin` bestätigt die direkte GLib-Abhängigkeit von Neolink sowie die Abhängigkeit von `VCRUNTIME140.dll` innerhalb GLib. Deshalb deklariert 2.0.2.0 außerdem das offizielle Framework `Microsoft.VCLibs.140.00.UWPDesktop`, mindestens 14.0.33321.0. Das unveränderte x64-APPX von Microsoft liegt dem VM-Transferpaket bei. Es werden keine Runtime-DLLs aus dem Windows-Systemordner kopiert.

Die transitive statische Importprüfung ab `neolink.exe` hat 22 Paketmodule untersucht und unter Einbeziehung des offiziellen VCLibs-Frameworks sowie der Systemmodule des Entwicklungsrechners keine unaufgelösten Importe gefunden. Das Microsoft-APPX hat eine gültige Microsoft-Authenticode-Signatur. Diese Prüfung ersetzt weder den tatsächlichen DLL-Lader der VM noch den Kamera-/UID-Verbindungstest.

**Korrigierter signierter Kandidat erstellt:** `store-artifacts/win11-vm-test-2.0.2.0/HomeCamMonitor-2.0.2.0-x64.msix`, 346.618.751 Bytes, SHA-256 `7d36b84c6427b1e9eac06ac8fd485f5ba5945ee160c1e6c942ac0179792ea6fb`. MakeAppx erfolgreich; sämtliche 1.867 Payload-Dateien, DLL-Suchpfade, GLib-Datei, VC++-Framework-Deklaration und bisherige Store-/Startup-Identität sind geprüft. Das ZIP `store-artifacts/HomeCamMonitor-Win11-VM-Test-2.0.2.0.zip` enthält das signierte MSIX, das offizielle Microsoft-VCLibs-APPX, öffentliche Zertifikat, Prüfsummen und Anleitung. Kein privater Schlüssel wird übertragen. Der Nutzer hat den Reolink-Streamstart ohne Fehlermeldung in der VM bestätigt; Aufnahmen über diesen Weg sind noch separat zu prüfen.

Microsoft-Primärquellen:

- [DLL-Suchfolge](https://learn.microsoft.com/en-us/windows/win32/dlls/dynamic-link-library-search-order)
- [Paket-Erweiterung für zusätzliche DLL-Verzeichnisse](https://learn.microsoft.com/en-us/windows/apps/desktop/modernize/desktop-to-uwp-extensions)

## Weiter offen

Der Nutzer hat auf die Bitte, Reolink-Aufnahme/Wiedergabe und Kameraeinträge/Einstellungen nach App-Neustart zu prüfen, mit „passt alles“ geantwortet. Diese Prüfungen werden für 2.0.2.0 als vom Nutzer bestanden bestätigt geführt.

Weiter offen: getestete Deinstallation und Windows App Certification Kit. Quellen-/Lizenzfreigabe, öffentliche Datenschutzerklärung und vollständige Store-Metadaten sowie die öffentliche Store-Einreichung bleiben offen.

Nutzer-Rückmeldung zum neuen Kandidaten 2.0.3.0: noch nicht getestet. Die früher bestätigten Funktionsprüfungen dürfen weiterhin nicht auf diese Paketbytes übertragen werden.


Nutzer meldet bei 2.0.4.0 einen Windows-Firewall-Dialog für neolink. Der im Paket enthaltene, auditgeprüfte Neolink-Helfer ist nicht separat Authenticode-signiert. Die Abfrage ist mit Netzwerkkommunikation dieses Helfers vereinbar. Daraus folgt kein bestandener Kameratest. Empfehlung: gezielte Freigabe im vertrauenswürdigen privaten Netz; keine pauschale Freigabe öffentlicher Netze.


## Nutzer-Rückmeldung zu 2.0.4.0

Am 18.10.2026 meldet der Nutzer: „bei der 2.0.4.0 ist mir nichts negatives aufgefallen“. Das wird als unauffälliger praktischer VM-Test dieses Kandidaten festgehalten. Es ist keine separate Bestätigung jeder Zeile des ausführlichen Testbogens, einer englischen Windows-VM, eines Deinstallationstests oder eines WACK-Laufs. Paketidentität und Prüfsumme stehen in docs/localization.md. Keine neuen Fehler gemeldet; keine erneute Paketversion allein aufgrund dieser Rückmeldung erforderlich.

## Rückmeldung zu 2.0.7.0 und Desktopkorrektur

Der Nutzer bestätigt das Logo in 2.0.7.0 als richtig; die Desktopverknüpfung fehlt weiterhin. 2.0.8.0 ergänzt eine desktop7:Extension der Kategorie windows.shortcut für "HomeCam Monitor V2.lnk" im Benutzerdesktop mit der bestätigten Standard-ICO-Datei. Syntax und Icon-Datei werden im Pakettest geprüft, das SDK akzeptiert die Deklaration. App-EXE, App-DLL und die drei Basis-Store-Bilder wurden per SHA-256 gegen den bestätigten Kandidaten 2.0.7.0 verglichen und sind identisch. Ob die Verknüpfung beim Upgrade erstellt wird und startet, muss in der Windows-11-VM bestätigt werden.

## Store-Einreichung 2.0.8.0

Am 18.10.2026 wurde HomeCam Monitor V2 im Partner Center zusätzlich als Produktname reserviert und für den Dashboardnamen sowie beide Store-Sprachen (de-DE/en-US) gewählt. Das unveränderte Store-MSIX 2.0.8.0 wurde frisch hochgeladen und mit Status Validated gespeichert. Prüferhinweise wurden auf 2.0.8.0 aktualisiert und nach erneutem Laden bestätigt. Kostenloser USD-Basispreis 0 und manuelle Veröffentlichung blieben erhalten. Nach Übermittlung zeigte die Übersicht Zertifizierung wird ausgeführt, Übermittlung abgeschlossen und Vorverarbeitung in Bearbeitung. Eine öffentliche Veröffentlichung erfolgt erst über Jetzt veröffentlichen. Nachweis: store-artifacts/store-certification-submitted-2.0.8.0.png.
