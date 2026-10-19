# HomeCam Monitor V2 im Microsoft Store

## Maßgeblicher aktueller Stand

18.10.2026: Der Nutzer bestätigt für 2.0.2.0 Upgrade, Windows-Autostart und dessen Deaktivierung, Netzwerkkamera-Aufnahmen, Reolink-Stream ohne DLL-Fehler, Aufnahme/Wiedergabe und Erhalt der Einstellungen. Siehe [VM-Testbericht](store-vm-test-results.md).

Das inzwischen öffentlich veröffentlichte V2.0.0-Release verwendet jedoch andere mpv-/FFmpeg-Dateien und einen vervollständigten Lizenzsatz. Seine GitHub-Release-Daten wurden direkt abgefragt; das portable ZIP stimmt mit dem veröffentlichten SHA-256 `31713a710010a71a3e291103dc6f8bf9456c7b1b00220b65edfbdf5e28bf8b55` überein. Der aktualisierte Komponentenbericht ist für diesen Dateisatz abgeschlossen. Der neue Store-Kandidat 2.0.3.0 übernimmt ihn und behält die bestehenden Store-Anpassungen. Die frühere experimentelle Helper-Umordnung ist darin nicht enthalten. Die Kameraprüfungen von 2.0.2.0 ersetzen keinen erneuten Test der geänderten Medienprogramme.

Support und Datenschutz: **support@dorisundmarcin.de**. [Store-Text](../packaging/store/store-listing-de.md) und [Datenschutzentwurf](../packaging/store/privacy-de.md) liegen vor. Der Anbieter wünscht keine postalische Anschrift; als Kontakt wird die bestätigte E-Mail-Adresse verwendet. Die direkte Texteingabe im Partner Center ist inzwischen bestätigt und gespeichert; eine eigene Datenschutz-URL wird für dieses Formular nicht benötigt. Preis und Länder bleiben offen. Weitere Schritte: erneuter Kamera-/Aufnahmetest, Deinstallationstest, Windows App Certification Kit und abschließende Distributionsprüfung der zusätzlichen Store-Laufzeiten. Das Zertifizierungskit wurde weder im üblichen Installationsordner noch im vorhandenen SDK-Arbeitsbereich gefunden.

**Die folgenden Abschnitte dokumentieren die bisherigen Kandidaten und enthalten historische offene Punkte. Für den aktuellen Stand gelten dieser Abschnitt und der VM-Testbericht.**

Stand: 18.10.2026. Grundlage ist die finale V2.0.0 aus dem Hauptzweig. Das Entwicklerkonto ist bereits vorhanden. Diese Vorbereitung veröffentlicht nichts und bestätigt noch keine Einreichungsreife.

Aktueller VM-Prüfstand und die vom Nutzer bestätigten Ergebnisse stehen im [Store-VM-Testbericht](store-vm-test-results.md). Upgrade, Autostart samt Windows-Deaktivierung und Netzwerkkamera-Aufnahmen wurden für 2.0.1.0 bestätigt. Ein zusätzlich gemeldeter GLib-Ladefehler beim Reolink-Dienst wird im Manifest von 2.0.2.0 korrigiert; der Nutzer bestätigt inzwischen den Streamstart ohne Fehlermeldung.

**Store-App angelegt:** HomeCam Monitor, Store-ID `9P6668481MGG`, Status „Im Entwurf“. Die drei Manifestwerte wurden auf der Produktidentitätsseite abgelesen und in `packaging/store/identity.psd1` hinterlegt. Der reservierte Anzeigename lautet **HomeCam Monitor**; der aktuelle Testkandidat hat Paketversion `2.0.2.0`.

## Paketierung

### Weiterer Store-Kandidat 2.0.1.0

Die Paketversion 2.0.1.0 ergänzt einen Windows-StartupTask (`HomeCamMonitorStartup`), der bei der Installation standardmäßig ausgeschaltet ist. Der Store-Build liest den Windows-Status beim Öffnen der Einstellungen und verwendet für „Mit Windows starten“ `StartupTask.RequestEnableAsync` bzw. `Disable`. Eine Deaktivierung durch den Benutzer oder eine Systemrichtlinie wird nicht überschrieben; die Oberfläche zeigt einen Hinweis und speichert den tatsächlichen Status. Die Desktop-Ausgabe ohne `StoreRelease` behält ihren Registry-Autostart. Die sichtbare Anwendungsversion bleibt 2.0.0.

Der Store-Build verwendet `-p:FinalRelease=true -p:StoreRelease=true`. Die .NET-/WindowsDesktop-Runtime bleibt explizit bei 8.0.32. Für `Microsoft.Windows.SDK.NET.Ref` ist eine eigene Framework-Version 10.0.19041.56 angegeben, damit die .NET-Versionsvorgabe nicht fälschlich auf das SDK-Referenzpaket angewendet wird. Die zusätzlichen Runtime-Dateien und mitgelieferten Hinweise beschreibt `packaging/store/STORE-COMPONENTS.md`.

Validierung: Store-Build und selbstständiges Publish erfolgreich; `tests/StoreStartup` besteht für Aktivieren/Deaktivieren, unveränderte Zustände, Windows-Ablehnung, Benutzer- und Richtliniensperren mit simuliertem Windows-Task. Dabei wird kein echter Autostart geändert. Der vollständige Standardlauf von `tests/V2Isolation` mit `FinalRelease=true` ist nach Bereitstellung des benötigten mpv-Testprogramms erfolgreich (Kameraquellen, Einstellungen, Backup/Passwortschutz, lokale Gateway-Integration und native Oberflächenprüfungen). Die Paketierungs-Fixture bestätigt höheren Versionsstand und standardmäßig deaktivierten StartupTask. Upgrade, Neustart/Anmeldung und das echte Windows-StartupTask-Verhalten sind erst nach Installation dieses neuen Kandidaten in der VM zu bestätigen.

**Signierter Upgrade-Kandidat:** `store-artifacts/win11-vm-test-2.0.1.0/HomeCamMonitor-2.0.1.0-x64.msix`, 346.618.604 Bytes, SHA-256 `7e2fdfaf7858101a4bf26cbccf1cc58d85a6db8b3460b05aaf467615fc5c527e`. Signiert mit dem bereits in der VM akzeptierten Zertifikat `42432D6A7508CC68A32A9F8D759E8A67328B5971`. Der Paketprüfer bestätigt alle 1.867 Payload-Dateien, die Store-Identität, Version, Build-Marker, standardmäßig deaktivierten StartupTask sowie Übereinstimmung der fünf V2-Binaries und 261 GStreamer-Plugins mit dem Audit. Transfer-ZIP: `store-artifacts/HomeCamMonitor-Win11-VM-Test-2.0.1.0.zip`. Kein privater Schlüssel wird mitgegeben. Auf der VM wurde bislang nur Paket 2.0.0.0 installiert; das Upgrade bleibt ausstehend. Die Quellen-/Lizenzfreigabe und Store-Einreichung bleiben offen.

Die bestehende WinForms-Anwendung wird als x64-Desktop-App mit MSIX paketiert. .NET wird selbstständig mitgeliefert. mpv, FFmpeg, Neolink und GStreamer bleiben separate Prozesse bzw. Laufzeitdateien. `runFullTrust` ist für die Desktop-App erforderlich und muss bei der Einreichung begründet werden: Zugriff auf vom Nutzer eingerichtete Netzwerk-Kameras, lokale Wiedergabeprozesse und Aufnahmeverzeichnisse.

Der Paketentwurf unterstützt Windows Desktop ab Windows 10, Version 2004. Das ist die technische Untergrenze des Entwurfs, keine zugesagte Testabdeckung. Die erste Freigabe muss auf tatsächlich unterstützten und getesteten Windows-Versionen basieren.

### Bauen

1. Ein vollständiges, geprüftes finales Publish-Verzeichnis mit `.github/scripts/build-v2-release.ps1` erzeugen. Seine Medien- und Reolink-Dateien müssen zuvor nach dem vorhandenen Buildverfahren vorbereitet sein. Der reine Git-Checkout enthält diese ausführbaren Fremdkomponenten nicht.
   Für den neuen StartupTask-Kandidaten anschließend `.github/scripts/publish-store-app.ps1 -RuntimeTemplate <finales-Publish-Verzeichnis> -Dotnet <SDK-dotnet.exe>` verwenden. Das Skript erzeugt einen neuen Publish-Ordner unter `store-artifacts` und baut die Store-Ausgabe hinein. Diesen Pfad dem Paketierer als `PublishDirectory` übergeben; ein normaler Desktop-Publish wird für den echten MSIX-Bau ohne Store-Build-Marker zurückgewiesen.
2. Die bereits reservierte MSIX-App **HomeCam Monitor** verwenden. Ihre Produktidentität ist in `packaging/store/identity.psd1` hinterlegt.
3. PowerShell 7 und Windows SDK (MakeAppx) verwenden:

```powershell
$identity = Import-PowerShellDataFile ./packaging/store/identity.psd1
./.github/scripts/build-store-package.ps1 @identity -PublishDirectory ./publish-v2-release
```

Ohne Windows SDK bereitet `-StageOnly` lediglich das Paketverzeichnis vor. Der Aufruf setzt ein echtes finales Publish-Verzeichnis voraus und prüft dessen Version. Ausgabe: eindeutiger Unterordner in `store-artifacts`, mit Manifest, Logos aus dem vorhandenen Icon und SHA-256-Inventar. Enthaltene Setup-/Uninstall-Programme werden nicht übernommen; Windows verwaltet MSIX-Installation und Deinstallation.

Das erzeugte MSIX ist unsigniert. Für lokale Installation ein separates Testzertifikat mit passendem Publisher verwenden und dieses gezielt auf dem Testgerät vertrauen. Die Store-Signatur erfolgt durch Microsoft. Das Skript legt kein Zertifikat an und installiert nichts.

## Offene technische Arbeiten vor Einreichung

- **Autostart:** Im Kandidaten 2.0.1.0 umgesetzt und mit simulierten Windows-Zuständen geprüft; echter VM-Test für Aktivierung, Anmeldung, Deaktivierung durch Windows und Status in der Oberfläche noch offen. Der erste Kandidat 2.0.0.0 verwendete noch den bisherigen Registry-Weg.
- **Einstellungen:** MSIX kann Registry- und AppData-Zugriffe virtualisieren. Import, DPAPI-Zugangsdaten, frei gewählter Einstellungsordner, parallele Desktop-Installation und Deinstallation testen. Keine automatische Übernahme aus einer bisherigen Installation versprechen.
- **Aufnahmen und Helfer:** Schreibschutz im WindowsApps-Ordner berücksichtigen. Aufnahmeordner, private Neolink-Konfiguration, GStreamer-Cache und Prozesse nach App-Ende prüfen. Die GStreamer-Registry liegt bereits außerhalb des Programmordners.
- **Native Laufzeiten:** Abhängigkeiten von OpenCvSharpExtern, GStreamer, Neolink, mpv und FFmpeg auf einer sauberen Windows-VM ohne Entwicklerwerkzeuge prüfen; gegebenenfalls passende VC-Laufzeiten korrekt als Abhängigkeit oder erlaubte App-Dateien bereitstellen.
- **Paketprüfung:** MakeAppx, lokale signierte Installation, Upgrade, Deinstallation und Windows App Certification Kit ausführen. Reale RTSP-/ONVIF-, HA- und UID-Verbindungen sowie lokale Bewegungserkennung, Vorlauf, Aufnahme und Fensterverhalten testen.
- **Fremdkomponenten:** V2-spezifische Herkunft, Hashes, korrespondierende Quellen, Änderungen und Lizenzhinweise vollständig prüfen. Der V1-Audit deckt die V2-Binaries nicht ab. Dauerhafte Quellen-Downloads vorsehen; zeitlich begrenzte Actions-Artefakte reichen nicht. Siehe [V2-Komponenten](reolink-v2-components.md) und [Release-Stand](release-v2.0.0.md).

## Store-Eintrag – deutscher Entwurf

**Anzeigename:** HomeCam Monitor (im Partner Center reserviert)

**Kurzbeschreibung:** Kamerabilder im Blick: Liveansicht, lokale Bewegungserkennung und Aufnahmen für Windows.

**Beschreibung:** HomeCam Monitor V2 zeigt deine Kameras in einem kompakten Windows-Fenster oder im Raster mit bis zu vier Kameras. Verbinde kompatible ONVIF-/RTSP-Kameras, Kamerastreams aus Home Assistant oder unterstützte Reolink-Kameras über UID. Home Assistant ist für direkte Netzwerkstreams und Reolink-UID nicht erforderlich.

Verwalte Kameras an einer Stelle, richte lokale Bewegungserkennung ein und speichere bei aktivierter Aufnahme Videos oder Schnappschüsse. Unterstützte Bewegungs- und Personenereignisse hängen vom gewählten Verbindungsweg und der Kamera ab. Eine Personenklassifizierung durch die lokale Bewegungserkennung wird nicht zugesagt.

**Voraussetzungen:** Eigene kompatible Kamera und erreichbarer Stream bzw. unterstützter UID-Weg. Netzwerk, Firewall und Kameraeinstellungen beeinflussen die Verbindung. Keine Kamera-Hardware enthalten. UID-/Relay-Verfügbarkeit wird nicht garantiert.

**Screenshots:** Die sechs vorhandenen Bilder aus `docs/screenshots/v2-2.0.0-*` als Ausgangspunkt nutzen; Format, Maße und Darstellung anhand der aktuellen Partner-Center-Vorgaben prüfen. Nur Beispieldaten verwenden.

**Noch einzutragen:** Preis und Länder, Kategorie, Altersfreigabe-Fragebogen, Support-URL, öffentlich erreichbare Datenschutzerklärung und ggf. zusätzliche Lizenzbedingungen. Der Entwurf legt keine Monetarisierung fest.

## Datenschutz und Prüferzugang

Die Datenschutzerklärung muss anhand des tatsächlichen Pakets erklären, welche Kameraadressen, Zugangsdaten, Aufnahmen und Diagnoseinformationen lokal gespeichert werden und welche Verbindungen zu Kamera, Home Assistant, Reolink-UID-/Relay-Diensten oder anderen Gegenstellen entstehen. Nicht pauschal „keine Datenübertragung“ oder „ausschließlich lokal“ behaupten. Ansprechpartner, Löschung und die tatsächlich verwendeten Drittkomponenten ergänzen.

Für die Zertifizierung eine konkrete Anleitung mit reproduzierbarem Teststream bzw. einer geeigneten Testumgebung bereitstellen. Keine privaten Kamera-Zugangsdaten in Repository, Store-Text oder Prüfanleitung übernehmen. Ohne Kamera müssen Einrichtung, Fehlermeldungen und Bedienoberfläche trotzdem verständlich sein.

## Aktuelle lokale Prüfung

Das .NET SDK 8.0.425 und der finale Laufzeitordner wurden im bisherigen Entwicklungsarbeitsbereich gefunden. Dessen unvollständiges MakeAppx konnte wegen einer fehlenden Side-by-Side-Assemblierung nicht starten. Für den echten Paketbau wurde `Microsoft.Windows.SDK.BuildTools` 10.0.26100.9169 aus dem offiziellen NuGet-Feed lokal im ignorierten `store-artifacts/build-tools` entpackt. Systemweite SDK-Installation oder Sicherheitsänderungen waren dafür nicht erforderlich.

Ein erster echter MSIX-Kandidat wurde erfolgreich mit MakeAppx erzeugt. Sämtliche 1.852 Payload-Dateien wurden anschließend gegen SHA-256-Inventar, Identität, Version und ausgeschlossene Dateien geprüft. Beim Abgleich mit dem neueren V2-Audit wurden fünf Binärhashes und alle 261 GStreamer-Pluginhashes bestätigt. Danach wurde der Kandidat mit den zutreffenden V2-Lizenzhinweisen und Audit-Unterlagen neu gebaut; der endgültige Nachweis steht in `validation.json` neben dem jeweiligen Paket. Der historische Erstkandidat mit V1-Hinweisen ist überholt und darf nicht eingereicht werden.

Die V2-Audit-Unterlagen stammen aus dem lokalen finalen Entwicklungsstand, Prüfdatum 18.10.2026. Ihr `releaseReady` bleibt ausdrücklich `false`. Siehe [V2-Audit](third-party-release-audit-v2.md). Der MSIX-Bau ändert keine Medienprogramme oder persönlichen Kameraeinstellungen. Eine signierte MSIX-Installation, Kamera-Funktionstests im Paket und Windows App Certification Kit stehen weiterhin aus. Das Paket ist nicht veröffentlichungsfertig.

**Aktueller lokaler Kandidat:** `store-artifacts/e609636d5d0641fd9353e6d497ed0a06/HomeCamMonitor-2.0.0.0-x64.msix`, unsigniert, 340.114.276 Bytes. SHA-256: `6eea01937ab73d7be5fdb70ac550a348bd5b32c78a8607c2d566349b95465682`. MakeAppx hat das Paket erfolgreich erstellt. Der unabhängige Paketprüfer `.github/scripts/verify-store-package.ps1` bestätigt alle 1.860 Payload-Hashes, Identität und Version, zutreffende V2-Hinweise sowie die Übereinstimmung der fünf Medien-/Bibliotheksdateien und 261 Plugins mit dem V2-Audit. `validation.json` und `SHA256SUMS.txt` liegen neben dem Paket. Ältere Kandidaten sind überholt.

## Windows-11-VM-Testpaket

Der Nutzer hat eine separate Windows-11-VM für die Installationstests gewählt. Eine Kopie des Kandidaten wurde für diese VM mit einem 30 Tage gültigen selbstsignierten Code-Signing-Zertifikat signiert. Fingerabdruck: `42432D6A7508CC68A32A9F8D759E8A67328B5971`, gültig bis 17.11.2026. Der private Schlüssel ist nicht exportierbar und verbleibt unter `Cert:\CurrentUser\My` auf dem Entwicklungsrechner. In dessen Vertrauensspeicher wurde nichts importiert.

`store-artifacts/win11-vm-test` enthält das signierte MSIX, das öffentliche Zertifikat, aktualisierte Prüfsummen und die [VM-Testanleitung](../packaging/store/VM-Testanleitung.md) mit [Testprotokoll](../packaging/store/Testprotokoll.md). Die VM muss dem öffentlichen Testzertifikat vor der Installation ausdrücklich vertrauen; das ist ein separater Schritt auf der Test-VM. Das signierte Paket hat andere Paket-Prüfsummen als der unsignierte Kandidat, aber denselben Anwendungs-Payload. Eine erfolgreiche Signierung ist noch kein Installationstest. Die Ausführung auf der VM ist noch nicht bestätigt.

**Erster VM-Installationsversuch:** Das entpackte MSIX wurde in der lokalen Hyper-V-VM „Win11“ geöffnet. App Installer zeigt HomeCam Monitor, Version 2.0.0.0; Publisher ist „Unknown“. Die Installation ist deaktiviert, weil Windows das Publisher-Zertifikat nicht verifizieren kann. Der Nutzer hatte einen Zertifikatimport bestätigt; dessen Ziel-Speicher konnte bisher nicht bestätigt werden. Damit ist noch keine Installation erfolgt. Nächster manueller Prüfschritt: exakt zum Signierer gehörendes öffentliches Zertifikat unter **Lokaler Computer → Vertrauenswürdige Personen**, Fingerabdruck wie oben. Keine Sicherheitsprüfung umgehen.

**VM-Nachprüfung nach korrigiertem Zertifikatimport:** App Installer akzeptiert das Zertifikat. Nach ausdrücklicher Installationsbestätigung des Nutzers wurde das MSIX in „Win11“ installiert. Beim erneuten Öffnen bestätigt App Installer „HomeCam Monitor is already installed“, Version 2.0.0.0. Der App-Start und ein tatsächliches Kameralivebild wurden visuell bestätigt. Ein zwischenzeitlicher Offline-Zustand wechselte wieder zur Liveansicht. Es wurde weder ein Kamerabild noch eine Kameraadresse in den Prüfbericht übernommen. Dieser Befund bestätigt Installation, Start und mindestens einen Wiedergabeweg, nicht alle Kameraarten, Aufnahmen, Einstellungen, Upgrade oder Deinstallation.

Der Einstellungsdialog lässt sich öffnen und zeigt Version 2.0.0 sowie vorhandene Kameraeinträge. Die Startoption steht auf „Minimiert starten“, was das zunächst fehlende sichtbare Hauptfenster erklärt. Einstellungen wurden über „Abbrechen“ ohne bewusste Änderungen geschlossen. Das ist noch kein Speichern-/Neustart-Test und keine vollständige Migrationsprüfung.

`tests/StorePackaging.ps1` besteht lokal mit einer ausdrücklich künstlichen Laufzeit-Fixture: geprüft sind Staging, XML-Escaping für Publisher-Werte, Ausschluss des Uninstallers, Logo-Abmessungen, Inventar-Prüfsummen und Zurückweisung eines unvollständigen Laufzeitordners. Diese Prüfung startet keine Kamera-App und führt MakeAppx nicht aus. Die Testdateien liegen ausschließlich im ignorierten `store-artifacts`-Ordner und sind keine auslieferbaren Programme.

## Microsoft-Dokumentation

- [Manuelle Desktop-Paketierung](https://learn.microsoft.com/en-us/windows/msix/desktop/desktop-to-uwp-manual-conversion)
- [Vorbereitung einer Desktop-App für MSIX](https://learn.microsoft.com/en-us/windows/msix/desktop/desktop-to-uwp-prepare)
- [Verhalten paketierter Desktop-Apps](https://learn.microsoft.com/en-us/windows/msix/desktop/desktop-to-uwp-behind-the-scenes)
- [Store-Richtlinien](https://learn.microsoft.com/en-us/windows/apps/publish/store-policies)

## Gespeicherter Partner-Center-Entwurf

Submission 1: 1152921505702163297. Datenschutzhinweise am 18.10.2026 direkt als Text gespeichert und nach erneutem Öffnen verifiziert. Kategorie Dienstprogramme + Tools. Eigenschaften-Status auf der Übersicht: Vollständig. Keine Anschrift eingetragen. Exakter gespeicherter Text: packaging/store/privacy-partner-center.txt. Keine Zertifizierung gestartet. Historische Hinweise auf eine zwingend fehlende Datenschutz-URL sind überholt.

## Paket im Partner-Center-Entwurf

Das unsignierte Store-MSIX 2.0.3.0 wurde vollständig gegen das Payload-Inventar geprüft und in Submission 1152921505702163297 hochgeladen. SHA-256: c95271ac366965cafbd3c36c157b895bd7ee54a9240d753b8293b9bec6c2b7fb. Partner Center hat Version 2.0.3.0, X64, Windows.Desktop akzeptiert. Das ist eine Upload-/Manifestprüfung, kein bestandener VM-Test oder eine Zertifizierung. Der Nutzer meldet den VM-Test ausdrücklich als noch nicht durchgeführt. Kostenloses Store-Angebot vom Nutzer bestätigt.

Deutscher Store-Eintrag: Beschreibung, Kurzbeschreibung, vier Produktfeatures, Entwicklername und sechs Suchbegriffe eingetragen. Vier vorhandene V2-Screenshots mit ausschließlich Beispieldaten hochgeladen (Übersicht, direkte Kamera, lokale Bewegungserkennung, Reolink). Speichern führte zurück zur App-Übersicht.


Preis und Verfügbarkeit als Entwurf gespeichert: Grundpreis USD 0, vorhandene Standardauswahl alle 240 Märkte. Keine Veröffentlichung oder Zertifizierung ausgelöst. Store-Texte und vier Screenshots gespeichert; Statuskontrolle folgt auf Übersicht.


Partner-Center-Übersicht nach dem Speichern bestätigt: Eigenschaften Vollständig, Pakete Vollständig (2.0.3.0 Validated), Store-Einträge Vollständig. Altersfragebogen noch offen; Zertifizierungsschaltfläche deaktiviert.


## Zweisprachiger Kandidat 2.0.4.0

Der Nutzer wünscht zusätzlich eine englische App und einen englischen Store-Eintrag. Oberfläche lokalisiert (449 Texte); Sprache folgt Windows-Anzeigesprache. Deutsche und englische Standardtests bestanden. Details und VM-Paket siehe docs/localization.md. Der bisherige 2.0.3.0-Test ist damit überholt; unmittelbar 2.0.4.0 testen. Englische Store-Beschreibung mit vier aus der englischen Oberfläche gerenderten Screenshots gespeichert. Englische Datenschutzhinweise und neuer Paket-Upload werden ergänzt; keine Zertifizierung gestartet.


Partner Center hat 2.0.4.0 akzeptiert und nach Speichern als aktuelles Paket angezeigt (Validated, Pakete Vollständig); 2.0.3.0 wurde durch Microsoft automatisch als niedrigeres Paket ersetzt. Store-Einträge insgesamt Vollständig. Datenschutzhinweise um die englische Fassung ergänzt. Kostenloses Angebot bleibt bestehen. Öffentliche Zertifizierung weiterhin nicht gestartet.


Support auf Wunsch des Nutzers über https://github.com/kulmi84/HomeCam-Monitor/issues. GitHub-API bestätigt öffentliches Repository und aktivierte Issues. E-Mail bleibt für Datenschutz und vertrauliche Anliegen erhalten. Keine neue App-Version hierfür nötig.


2.0.4.0: Nutzer meldet praktischen VM-Test ohne aufgefallene Probleme. Detailabdeckung wie im Store-VM-Testbericht dokumentiert. Weiter offen: Altersfreigabe, Zertifizierungs-Prüfanleitung und restliche Freigabeprüfungen.


Altersfragebogen nach Funktionsumfang vorbereitet. Eigene Kameraquellen zählen laut eingeblendeter IARC-Hilfe nicht als vom Anbieter empfohlene Online-Inhalte. Vorschau: USK ohne Altersbeschränkung, PEGI 3, ESRB Everyone. Speichern wartet auf explizite Zustimmung des Nutzers zur IARC-Vereinbarung und Volljährigkeitsbestätigung; Kontrollkästchen nicht aktiviert, keine abschließende Speicherung vorgenommen.


IARC: Nutzer hat am 18.10.2026 ausdrücklich zugestimmt. Zustimmung zu Nutzungsbedingungen und Volljährigkeit im Partner Center bestätigt, Altersfreigaben gespeichert. Die Ergebnisansicht zeigt USK ohne Altersbeschränkung, PEGI 3, ESRB Everyone; die endgültige Altersfreigabe-ID steht noch auf Ausstehend.


Zusätzliche Testinformationen gespeichert und nach Neuladen verifiziert (4.509 Zeichen): keine Publisher-Anmeldung, eigene/optionale Kameras, reproduzierbarer lokaler RTSP-Test mit MediaMTX 1.21.2 und synthetischem FFmpeg-Bild, runFullTrust-/Autostart-Begründung sowie ausdrücklich ungeprüfte WACK-/Deinstallationspunkte. Der lokale Test mit dem exakten Paket-FFmpeg hat Publizieren, 3-s-Aufnahme und Dekodierung bestanden, ausschließlich auf 127.0.0.1:18556; Testprozesse anschließend beendet. Veröffentlichungsmodus auf manuell nach Zertifizierung gespeichert.


## Zur Microsoft-Zertifizierung eingereicht

18.10.2026: Submission 1152921505702163297 mit Paket 2.0.4.0 übermittelt. Partner Center zeigt Zertifizierung wird ausgeführt; Übermittlung abgeschlossen und Vorverarbeiten aktiv. Die Seite bestätigt ausdrücklich: Ihr Produkt wird veröffentlicht, wenn Sie auf Jetzt veröffentlichen klicken. Damit keine automatische öffentliche Veröffentlichung. Deutsch/Englisch, kostenlos, Datenschutzhinweise und GitHub-Supportlink enthalten. WACK und separater Deinstallationstest sind in den Prüferhinweisen weiterhin ausdrücklich als nicht durchgeführt dokumentiert.

