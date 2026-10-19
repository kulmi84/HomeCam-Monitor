# Standardpfade der finalen V2 ab Paket 2.0.11.0

Für eine neue finale V2-Installation gelten diese Vorgaben. Die Windows-Spezialordner werden für den angemeldeten Benutzer aufgelöst, einschließlich möglicher Windows-Ordnerumleitungen.

| Verwendung | Standardpfad |
| --- | --- |
| Manuelle Snapshots | Bilder/HomeCam Monitor V2 |
| Manuelle Videos | Videos/HomeCam Monitor V2 |
| Bewegungs-Snapshots | Bilder/HomeCam Monitor V2/Bewegung |
| Bewegungs-Videos | Videos/HomeCam Monitor V2/Bewegung |
| Einstellungen | %LOCALAPPDATA%/HomeCamMonitor-V2 |

Bei englischer Oberfläche heißt der Unterordner für Bewegung wie bisher `Motion`.

Die bisherigen Beta-Namen waren über die interne Kanalidentität auch in der finalen Ausgabe aktiv. Die sichtbaren Standardordner sind jetzt von dieser internen Identität getrennt. Registry-/Autostartkennung und Aufzeichnungsverwaltung behalten ihre bisherige Identität, damit Upgrades vorhandene Daten weiterfinden.

Explizit gewählte Einstellungsordner behalten Vorrang. Ohne explizite Auswahl wird zunächst eine vorhandene `settings.json` im finalen Standardordner berücksichtigt, danach die vorhandene V2-Beta-Konfiguration. Nur ohne vorhandene Konfiguration beginnt die finale Version im neuen Standardordner. Ein leerer alter Beta-Ordner allein gilt nicht als Konfiguration. Bestehende Dateien werden nicht verschoben. Gespeicherte Aufnahmepfade werden beibehalten. Tatsächliche Beta-Builds behalten ihre bisherigen Vorgaben.

Prüfung: `dotnet run --project tests/V2Isolation/V2Isolation.csproj -c Release -p:FinalRelease=true -- --storage-defaults`. Die Prüfung deckt alle fünf Pfadfelder der frischen Einstellungsoberfläche, die Auswahl alter/neuer Konfigurationen, benutzerdefinierte Ordner und gespeicherte Aufnahmepfade ab. Auch auf Englisch und ohne FinalRelease ausgeführt. Die Lokalisierungsprüfung mit 453 Einträgen ist bestanden. Ein tatsächlicher Neuinstallationslauf in der VM ist separat zu bestätigen.

Ein zusätzlich gestarteter vollständiger V2Isolation-Lauf besteht die Pfad-/Initialisierungsprüfungen, Kameraaktivierung, Quellenwechsel und Bewegungszuordnung, bricht danach aber in `TestDarkDropdown` ab: Die vorhandene Logoauswahl in `Program.cs` ist weiterhin eine normale `ComboBox`, während diese Prüfung ausschließlich `DarkComboBox` zulässt. Dieser unveränderte Fehler gehört nicht zur Pfadkorrektur. Der Gesamtlauf wird daher nicht als bestanden geführt; Protokoll unter `store-artifacts/storage-defaults-regression.log`.
