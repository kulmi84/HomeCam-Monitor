# HomeCam Monitor – Windows-11-VM-Test

Dieses Paket ist ein lokaler Entwicklungskandidat, keine Store-Freigabe. Paketversion 2.0.4.0 ergänzt eine deutsche/englische Oberfläche und behält die mpv-/FFmpeg-Dateien und den vollständigen Lizenzsatz aus dem öffentlich veröffentlichten V2.0.0-Release. Die Sprache folgt der Windows-Anzeigesprache: Deutsch für deutsche Spracheinstellungen, sonst Englisch. Autostart, DLL-Suchpfade und Microsoft-C++-Framework-Abhängigkeit aus dem getesteten 2.0.2.0 bleiben erhalten. Eine experimentelle Änderung der Helper-Anordnung ist nicht enthalten.

Für diese VM genügt ein Upgrade: Das bereits akzeptierte Testzertifikat und das installierte Microsoft-C++-Framework weiterverwenden. Nicht erneut importieren. Nach dem Upgrade direkte Netzwerkkamera und Reolink-UID jeweils mit Livebild, kurzer Aufnahme und Wiedergabe prüfen. Einstellungen nach App-Neustart kontrollieren.

## Vorbereitung

1. Eine Windows-11-x64-VM mit eigenem Testbenutzer verwenden und einen Snapshot erstellen. Keine Produktiv-Einstellungen oder privaten Kamera-Zugangsdaten übernehmen.
2. Das ZIP in die VM kopieren und vollständig entpacken. Enthalten sind das signierte MSIX, ein öffentliches `.cer`-Testzertifikat, Prüfsummen, Dateiinventar und ein vorbereiteter Testbogen. Kein privater Schlüssel wird mitgegeben.
3. Optional mit `Get-FileHash .\HomeCamMonitor-2.0.4.0-x64.msix -Algorithm SHA256` die Paket-Prüfsumme mit `SHA256SUMS.txt` vergleichen.

## Microsoft-C++-Laufzeit

`Microsoft.VCLibs.x64.14.00.Desktop.appx` ist das unveränderte, von Microsoft signierte Desktop-C++-Framework aus `https://aka.ms/Microsoft.VCLibs.x64.14.00.Desktop.appx`. Es ist eine deklarierte Paketabhängigkeit für die nativen Bibliotheken. Falls es noch nicht vorhanden ist, zuerst dieses APPX öffnen und installieren. Falls Windows eine bereits installierte neuere Framework-Version meldet, diese beibehalten und mit HomeCam fortfahren. Dafür ist kein HomeCam-Testzertifikat nötig. Bei späterer Store-Installation löst Windows die Framework-Abhängigkeit auf.

## Testzertifikat in der VM vertrauen

Das selbstsignierte Zertifikat ist 30 Tage gültig. Es muss nur in der Test-VM ausdrücklich als vertrauenswürdig eingerichtet werden. Dadurch kann Windows Pakete dieses Testsignierers installieren. Nicht in die Stammzertifizierungsstellen importieren und nicht auf dem normalen Arbeitsrechner installieren.

1. `HomeCamMonitor-VM-Test.cer` öffnen. Den Fingerabdruck mit `certificate-thumbprint.txt` vergleichen.
2. **Zertifikat installieren → Lokaler Computer → Alle Zertifikate in folgendem Speicher speichern → Vertrauenswürdige Personen (Trusted People)** wählen. Windows verlangt dafür Administratorrechte.
3. Danach das MSIX als der vorgesehene Testbenutzer öffnen und installieren. Alternativ in einer normalen Windows-PowerShell im entpackten Ordner:

```powershell
Add-AppxPackage -Path .\HomeCamMonitor-2.0.4.0-x64.msix -DependencyPath .\Microsoft.VCLibs.x64.14.00.Desktop.appx
```

Auf dem Entwicklungsrechner wurde das Zertifikat nicht in einen Vertrauensspeicher importiert. Entwickler-Modus, Defender, Zertifikatsprüfung oder Firewall müssen für diese Anleitung nicht abgeschaltet werden. Bei einem Installationsfehler den Fehlercode notieren und die Ursache prüfen.

## Funktionsprüfung

Ergebnisse in `Testprotokoll.md` eintragen. Die Kamera-Prüfungen benötigen ein erreichbares Testgerät bzw. einen Teststream; ohne diese Voraussetzung als „nicht geprüft“ markieren.

- **Installation und Start:** Startmenüeintrag „HomeCam Monitor“, Programmstart ohne fehlende DLLs oder zusätzliche .NET-Installation, korrekte Version 2.0.0, verständliche leere Kameraliste und Fehleranzeigen.
- **Einstellungen:** Fensterdarstellung und eine unkritische Einstellung ändern, schließen und erneut starten. Werte bleiben erhalten. Einstellungs-Backup und Wiederherstellung mit reinen Testdaten prüfen.
- **Autostart:** „Mit Windows starten“ einschalten und speichern. In Windows unter Apps → Autostart den Status prüfen. Nach Ab-/Anmeldung muss die App starten. Anschließend in Windows deaktivieren, App-Einstellungen neu öffnen: Der Haken muss ausgeschaltet sein. Erneutes Aktivieren in der App muss auf die Windows-Deaktivierung hinweisen; diese Entscheidung darf nicht automatisch überschrieben werden. Nach dem Test den ursprünglichen Zustand wiederherstellen. Bei Richtlinienverwaltung den Status dokumentieren.
- **Wiedergabe:** Direkten Test-RTSP-/ONVIF-Stream einrichten, Einzelansicht und 4-Kamera-Raster, minimieren/wiederherstellen und Vollbild prüfen.
- **Bewegung und Aufnahmen:** Mit Testkamera lokale Bewegungserkennung, Schnappschuss, Video und Vorlauf testen. Ein Aufnahmeziel außerhalb des WindowsApps-Ordners auswählen und erzeugte Dateien abspielen.
- **Verbindungswege:** Home Assistant und Reolink-UID jeweils nur mit einer passenden Testumgebung prüfen. Netzwerkmodus der VM dokumentieren; NAT oder VLANs können Multicast-Kamerasuche verhindern. Keine Netzwerkeinstellungen auf Verdacht abschalten.
- **Prozessende:** App beenden und im Task-Manager prüfen, ob zugehörige mpv-, FFmpeg- und Neolink-Prozesse enden. Es dürfen keine privaten Zugangsdaten in den Testbogen kopiert werden.
- **Erneute Installation:** App schließen und dasselbe Paket erneut installieren. Das ist ein Wiederholungs-/Reparaturtest, kein Versionsupgrade.
- **Upgrade:** Alle laufenden HomeCam-Monitor-Instanzen schließen. Das neue 2.0.2.0-MSIX öffnen und aktualisieren. Vorhandene Kameraeinträge, unkritische Einstellungen und externe Testaufnahmen anschließend vergleichen. Die Pakete verwenden dieselbe Store-Identität und denselben Testsignierer; ein erneuter Zertifikatimport ist dafür nicht erforderlich.
- **Reolink nach Korrektur:** Reolink aktivieren bzw. die Verbindung testen. Die Meldung über fehlende `glib-2.0-0.dll` darf nicht mehr erscheinen. Danach tatsächlichen UID-Verbindungsaufbau, Livebild und eine Aufnahme prüfen. Ein Netzwerkkamera-Livebild bestätigt nicht den Reolink-UID-Weg.
- **Deinstallation:** Über Windows „Installierte Apps“ entfernen, Startmenüeintrag und Paketregistrierung prüfen. Verhalten der Testeinstellungen und extern gespeicherten Aufnahmen protokollieren.

## Diagnose ohne Zugangsdaten

```powershell
Get-AppxPackage -Name MarcinKulmaczewski.HomeCamMonitor |
  Select-Object Name, Version, Status, PackageFullName
```

Wenn `Add-AppxPackage` eine ActivityID meldet, kann `Get-AppPackageLog -ActivityID <ID>` bei der lokalen Diagnose helfen. Vollständige Logs vor dem Weitergeben auf persönliche Pfade, Kameraadressen und Zugangsdaten prüfen. Für die Rückmeldung reichen zunächst der Fehlercode und der betroffene Prüfschritt.

## Aufräumen

Nach dem Test am einfachsten zum vorherigen VM-Snapshot zurückkehren. Alternativ die App über Windows deinstallieren und anschließend das Zertifikat mit dem passenden Fingerabdruck in `certlm.msc` unter **Vertrauenswürdige Personen → Zertifikate** entfernen. Keine anderen Zertifikate löschen.

Microsoft-Anleitung: https://learn.microsoft.com/en-us/windows/msix/package/sign-msix-package-guide
