# Deutsche und englische Oberfläche

Die V2-Ausgabe verwendet `CultureInfo.CurrentUICulture`: deutschsprachige Windows-Oberflächen (`de-*`) erhalten deutsche Texte; andere Anzeigesprachen erhalten Englisch. Zahlen- und Datumsformate bleiben an die regionale Windows-Kultur gebunden. Es werden keine Windows-Spracheinstellungen verändert.

`UiText.T` übersetzt feste Anzeigetexte anhand des eingebetteten Katalogs `assets/Strings.en.tsv`. `UiText.F` übersetzt Vorlagen vor der Formatierung. Der Katalog enthält 449 Einträge und wird mit `WithCulture=false` in die Hauptassembly eingebettet. Fehlende Übersetzungen fallen auf den deutschen Ausgangstext zurück. Kameranamen aus Benutzereinstellungen, Zugangsdaten, Protokollwerte, JSON-Feldnamen und Ordneridentitäten bleiben unverändert.

Der Store-Kandidat 2.0.4.0 deklariert `de-de` und `en-us`. Er behält Identität, Windows-StartupTask, DLL-Ladepfade, Microsoft-VCLibs-Abhängigkeit und den auditgeprüften Mediensatz aus 2.0.3.0.

## Prüfung

- Vollständiger `V2Isolation`-Standardlauf unter deutscher und englischer UI-Kultur erfolgreich, einschließlich Kameraaktivierung, Wechsel zwischen den drei Quellen, ONVIF, HA-Testserver, Reolink-Hilfsprozess-Simulation, Passwortschutz, Backup, Aufnahmen-/Sortieroberfläche, Discovery und lokalem Gateway.
- `--localization` prüft 449 Katalogeinträge, Platzhalter, Dialogbeschriftungen in `de-DE`, `en-US`, `en-GB` und den englischen Fallback in `fr-FR`, Statusmeldungen und unveränderte Einstellungen.
- Store-Startup-Simulation und Paketierungs-Fixture erfolgreich. Keine echten Windows-Autostarteinstellungen geändert.
- Englische Screenshots werden mit `--english --docs-screenshots` aus synthetischen Kameradaten gerendert; Ausgabe unter `docs/screenshots/en/`. Version der Testassembly entspricht im FinalRelease der sichtbaren Anwendungsversion 2.0.0.

Die automatisierten Prüfungen ersetzen keinen VM-Test dieses Pakets mit echten Kameras und keinen WACK-Lauf. Es wurde noch keine Zertifizierung gestartet.

## VM-Kandidat

Signiertes Paket: `store-artifacts/win11-vm-test-2.0.4.0/HomeCamMonitor-2.0.4.0-x64.msix`, 351.985.006 Bytes. SHA-256: `f4c0a9dcd6aecddfb20c4d36a21034de08f6cf6e63a4c7a01f99c76ac5fc429a`.

Transferpaket: `store-artifacts/HomeCamMonitor-Win11-VM-Test-2.0.4.0.zip`. Vorhandenes VM-Zertifikat weiterverwenden. 2.0.3.0 muss nicht vorher installiert oder separat getestet werden. Nach Upgrade Livebild, Aufnahme/Wiedergabe und Erhalt der Kameraeinträge prüfen; wenn eine englische Windows-Testumgebung vorhanden ist, dort zusätzlich englische Beschriftungen prüfen.
