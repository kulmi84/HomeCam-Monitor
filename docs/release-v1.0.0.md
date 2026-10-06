# Release-Prüfung HomeCam Monitor V1.0.0

Stand: 06.10.2026. Noch kein finaler GitHub Release und kein V1.0.0-Tag.

## Geprüfte Ausgangsbasis

- `main`: `f48fe064e4bfab4feba44b28d6724d50f0432506`, Stable-Projekt 0.1.2, Beta-Projekt 0.2.0-beta.45. README beschreibt bereits neuere Funktionen.
- Vorgesehener V1-Funktionsstand: `feature/setup-v0.5.0`, `7041045392c17e8677bee127b3a76e1b8a9b1cfb`, 0.5.0-beta.22.
- [Windows-Build 37339036318](https://github.com/kulmi84/HomeCam-Monitor/actions/runs/37339036318) hat für diesen Commit den Status **success**. Dieser Befund betrifft den Vorabstand und ist kein Nachweis eines fertigen V1.0.0-Pakets.
- Keine GitHub Releases in der abgefragten Release-Liste vorhanden.
- Keine neuen Funktionen und keine Änderung des funktionierenden Anwendungscodes in diesem Dokumentationsvorschlag.

## Vorbereitete Dokumente

- README auf V1.0.0-Vorbereitung ausgerichtet; vorhandene Beta-Historie erhalten.
- CHANGELOG mit festgelegtem Funktionsumfang, ohne vorgetäuschtes Veröffentlichungsdatum.
- LICENSE.txt aus dem vorhandenen Entwurf vom 03.10.2026 mit der später abgestimmten Zustimmungspflicht für Paketweitergabe; Forks/Weiterentwicklung/Codeverwendung ausdrücklich klargestellt.
- Rechteinhaber nach Bestätigung: **Marcin Kulmaczewski** (kulmi84).
- Aktuelle Anleitung und Fremdkomponenten-Befund ergänzt.

## Noch offene Release-Hindernisse

- [ ] Den Beta-22-Funktionsstand als Basis der finalen V1.0.0-Version integrieren. Nicht lediglich das ältere Stable-Projekt auf 1.0.0 umnummerieren: Viele V1-Funktionen stehen noch unter `BETA`.
- [ ] Programm-, Assembly-, Datei- und Setupversionen sowie Paketnamen konsistent auf V1.0.0 setzen; vorhandene Einstellungen und Installationen beim Übergang erhalten.
- [ ] Persönliche HA-Beispieladresse durch neutrale Vorgabe ersetzen und Neuinstallation prüfen.
- [ ] Lizenz-Prüffassung abschließend prüfen/freigeben; keine erfundene Haftungsregel einsetzen. Rechtekette von eigenem Code, Snippets und Grafiken sowie frühere Lizenzzusagen prüfen.
- [ ] mpv-/FFmpeg-Binaries fixieren und vollständige Lizenztexte, Copyrights, exakte Quellen sowie Buildinformationen bereitstellen; Details in [THIRD-PARTY-NOTICES.md](../THIRD-PARTY-NOTICES.md).
- [ ] Lizenzunterlagen in sämtliche Downloadpakete aufnehmen und Paketinhalt prüfen.
- [ ] Vorgehen für privaten Quellcode und öffentliche Downloads abstimmen/umsetzen. Derzeit ist das gesamte Quellcode-Repository öffentlich. GitHub erlaubt dort Ansehen und Forken gemäß seinen [Nutzungsbedingungen](https://docs.github.com/en/site-policy/github-terms/github-terms-of-service#d-user-generated-content); eine eigene Lizenz kann diese Plattformrechte nicht pauschal ausschließen. Quelldateien nur im Hauptbranch zu löschen entfernt sie nicht aus der Historie.
- [ ] Finale Windows-Build-, UI-, Aufnahme-/Vorlauf- und Setupprüfungen auf dem endgültigen Commit erfolgreich ausführen.
- [ ] Neuinstallation auf einem sauberen Windows-PC prüfen: erster Start, Icons, Startmenü, Standardpfade, Update, Erhalt von Einstellungen und Aufnahmen, Deinstallation.
- [ ] SHA-256-Prüfsummen für Installer, portable ZIP und Quellenpakete erstellen; Paketinhalt und Downloadlinks prüfen.
- [ ] Änderungen und Pakete durch den Rechteinhaber prüfen lassen.
- [ ] Erst nach Freigabe den finalen GitHub Release V1.0.0 veröffentlichen und offizielle Downloads in README eintragen.

## Review-Umfang

Dieser Vorschlag ändert ausschließlich Dokumentation und den Lizenzentwurf auf Basis des aktuellen Setup-Branches. Er verändert weder `main` noch den Anwendungscode oder bestehende Installer. Er enthält noch keine fertig geprüften V1.0.0-Binaries. Historische Dokumente behalten ihre Versionsbezeichnungen; die neue Anleitung verlinkt sie als Referenz.
