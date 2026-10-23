# V2-Fensterdarstellung und getrennte MSIX-Beta

Windows meldet für das minimierte, rahmenlose Kamerafenster eine Clientfläche von 160 × 28 Pixeln. Die Videofläche schrumpfte mit und wurde beim Wiederherstellen sichtbar aufgezogen. Auch der erste Playerstart im bereits minimierten Fenster verwendete diese kleine Fläche.

V2 behält die Videoflächen während des Minimierens bei, überspringt nachlaufende Größenanpassungen und verwendet beim Erststart, bei der Neustart-Abdeckung und beim Auffrischen einer Einzelkamera die normale Wiederherstellungsgröße. Sichtbare Fenster verwenden weiterhin ihre aktuelle Clientfläche. Das gilt auch für Store-Builds; V1 bleibt unverändert.

## Getrennte Beta

`NextBeta=true` wählt die Anwendungsversion `2.0.25-beta.6` und die Datei-/Paketversion `2.0.25.7`. Die stabile `HomeCamV2Version` bleibt unverändert. `NextBeta=true` und `FinalRelease=true` sind nicht gemeinsam zulässig.

Die Beta hat eine eigene EXE, Einstellungen, Registrierung, Aufnahmeordner, MSIX-Identität (`MarcinKulmaczewski.HomeCamMonitor.NextBeta`), Startaufgabe und Taskleistengruppe. Sie kann neben der stabilen Store-App installiert werden. Beta-Updates behalten deren Einstellungen bei. Die stabile Symbol-/Designlogik, Paketbilder und Store-Paketskripte bleiben unverändert.

Die zusätzlichen experimentellen Darstellungsmaßnahmen – abgeschaltete DWM-Animationen und native Videoflächen-Ausrichtung – gelten nur unter `NEXT_BETA`. Die gemeinsame V2-Größenkorrektur funktioniert auch ohne sie.

## Autostart beim ersten Öffnen der Einstellungen

Beta 5 korrigiert eine nicht passende Startaufgaben-Kennung. Im Beta-Manifest steht `HomeCamMonitorNextBetaStartup`; diese Kennung verwendet das Programm jetzt auch beim Lesen und Ändern des Autostartstatus. Die stabile Ausgabe behält `HomeCamMonitorStartup`. Der Abgleich gilt unabhängig von einer vorhandenen Benutzerkonfiguration. Der Paketbau prüft die Kennung gegen das tatsächlich erzeugte Manifest.

## Paketbau

Für ein unsigniertes lokales MSIX werden ein entpacktes, geprüftes V2-2.0.25.0-Laufzeitpaket und ein Windows SDK mit MakeAppx und MakePRI benötigt:

```powershell
.github/scripts/build-next-beta-msix.ps1 -RuntimeTemplate C:/runtime/v2-2.0.25.0 -SdkDirectory 'C:/WindowsSDK/bin/10.0.26100.0/x64'
```

Die Ausgabe liegt unter `../Beta/<Anwendungsversion>/`. Vorhandene Pakete werden nicht überschrieben. Die Signierung erfolgt separat mit einem zum Manifest-Publisher passenden Zertifikat. Zertifikate und private Schlüssel gehören nicht ins Repository. Prüfung eines signierten Testpakets:

```powershell
.github/scripts/verify-next-beta-msix.ps1 -BetaDirectory ../Beta/2.0.25-beta.6 -SdkDirectory 'C:/WindowsSDK/bin/10.0.26100.0/x64'
```

Die Skripte verwenden den geprüften Komponentensatz ohne veränderliche Latest-Downloads. Sie veröffentlichen nichts und führen keine Store-Einreichung durch.

## Prüfung

Die gezielte Windows-CI lädt die festgelegte Laufzeit aus `v2.0.25.0` und kontrolliert ihre veröffentlichte SHA-256-Summe. Ein lokales Testvideo betreibt den echten mpv/D3D11-Player ohne gespeicherte Kamerazugänge:

- `--initial-player <mpv.exe> <Testvideo>` prüft den minimierten Erststart.
- `--restore-player <mpv.exe> <Testvideo> <Ausgabe>` prüft wiederholtes manuelles Wiederherstellen und den Bewegungsablauf, einschließlich nachlaufendem Layout.
- `--restore-layout` prüft native Fensternachrichten und die Videoflächen-Größe.
- Symbol-/Design-, Speicherort- und Darstellungsprüfungen laufen für Beta und Store-Konfiguration.
- Die Autostart-Tests prüfen Manifestkennung, unveränderte Zustände und den Schutz von Benutzer- und Richtlinienentscheidungen, ohne echte Autostarteinstellungen zu verändern.

Vor der Größenkorrektur meldeten Host und Player 160 × 28 statt 640 × 360. Danach bleiben beide beim ersten und bei späteren Aufrufen bei 640 × 360. Der manuelle Nutzertest mit MSIX-Beta 4 bestätigt die Darstellungsänderung.

## Automatisch minimieren nach manuellem Öffnen

Beta 6 ergänzt unter „Bewegung und Aktivitätsanzeige“ die standardmäßig ausgeschaltete Option „Nach manuellem Öffnen automatisch minimieren“ mit eigener Wartezeit von 3 bis 300 Sekunden (Vorgabe: 10). Nach dem Wiederherstellen aus der Taskleiste startet die Wartezeit. Bei Mauszeiger im Fenster oder geöffneten Einstellungen wird sie erneut gestartet; Bedienung startet sie ebenfalls neu. „Immer im Vordergrund“ verhindert die automatische Minimierung. Die Bewegungssteuerung verwendet weiterhin ihre eigene Zeit und Einstellung.

Der Fenstertest `--restore-layout` prüft zusätzlich die neue Option, Wiederherstellen, Mauszeigerschutz, Bedienung, Vorrang von „Immer im Vordergrund“, tatsächliches Minimieren, Speicherung und Zurücksetzen der Anzeigeeinstellungen.
### Beta 6, Update 1: Vordergrund beim manuellen Aufruf

Die eigene Option „Nach manuellem Öffnen im Vordergrund halten“ ist standardmäßig eingeschaltet, auch beim Laden älterer Einstellungen ohne dieses Feld. Sie nutzt dieselbe manuelle Wartezeit wie das separat wählbare automatische Minimieren. Nach Wiederherstellen aus der Taskleiste bleibt das Fenster für diese Dauer über normalen Fenstern. Ohne automatische Minimierung wird danach lediglich der temporäre Vordergrundstatus aufgehoben. Mit automatischer Minimierung wird das Fenster minimiert. Mauszeiger und Einstellungsdialog verschieben den Ablauf weiterhin; Bedienung startet die Zeit neu. Die dauerhafte Option „Immer im Vordergrund“ hat Vorrang.

Auch die nachlaufende Wiederherstellung und das Schließen der Einstellungen berücksichtigen den temporären Vordergrundstatus. Die Tests prüfen zusätzlich das native Windows-Topmost-Bit, beide getrennten Optionen, den Ablauf ohne Minimierung und den eingeschalteten Standardwert nach Zurücksetzen.

Das Update behält die Anwendungsversion `2.0.25-beta.6`, erhöht aber die MSIX-Version auf `2.0.25.7`, damit Windows die vorhandene Beta aktualisieren kann. `-OutputDirectory` ermöglicht einen separaten Ausgabeordner, ohne das bereits signierte ursprüngliche Beta-6-Paket zu überschreiben.