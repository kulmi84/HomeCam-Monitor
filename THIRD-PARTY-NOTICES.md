# Fremdkomponenten – V1.0.0-Vorbereitung

Die proprietäre HomeCam-Lizenz gilt ausschließlich für eigene geschützte HomeCam-Bestandteile. Rechte zum Nutzen, Ändern und Weitergeben von Fremdkomponenten und deren Quellen werden nicht eingeschränkt.

**Status:** Technische Prüfung des Workflows in Commit `7041045392c17e8677bee127b3a76e1b8a9b1cfb`. Die Lizenzunterlagen des endgültigen Installers sind noch nicht vollständig. Dieses Dokument ersetzt keine vollständigen Lizenztexte oder korrespondierenden Quellen.

## Komponenten und Befund

| Komponente | Verwendung / Bezugsquelle | Lizenzstatus und fehlende Nachweise |
|---|---|---|
| mpv | Separates `mpv.exe`, IPC; [shinchiro/mpv-winbuild-cmake](https://github.com/shinchiro/mpv-winbuild-cmake) | mpv standardmäßig GPLv2 oder später; LGPL-Variante nur bei entsprechender Buildkonfiguration und kompatiblen Abhängigkeiten. Konkrete Binary-Lizenz noch nachzuweisen. Workflow kopiert nur EXE, keine Lizenz-/Copyright-Dateien oder Quellen. |
| FFmpeg | Separates `ffmpeg.exe` für Aufnahmen und Snapshots; [BtbN/FFmpeg-Builds](https://github.com/BtbN/FFmpeg-Builds) | Gewähltes Asset: `ffmpeg-master-latest-win64-lgpl.zip`. FFmpeg grundsätzlich LGPLv2.1 oder später; GPL-Optionen ändern die Lizenz, nonfree-Konfigurationen können Weitergabe ausschließen. Assetname allein ist kein Nachweis. Workflow übernimmt höchstens eine LICENSE-Datei; fehlt sie, bricht er nicht ab. |
| .NET 8 / Windows Desktop Runtime | Self-contained .NET-8-Windows-Paket | .NET-Code überwiegend MIT; vollständige Copyright-/Third-Party-Hinweise der tatsächlich mitgelieferten Runtime beachten. [Runtime-Lizenz](https://github.com/dotnet/runtime/blob/main/LICENSE.TXT), [Runtime-Hinweise](https://github.com/dotnet/runtime/blob/main/THIRD-PARTY-NOTICES.TXT), [Windows-Desktop-Lizenz](https://github.com/dotnet/windowsdesktop/blob/main/LICENSE.TXT). Exakte Runtimeversion im finalen Paket festhalten. |
| Eingebaute Bibliotheken von mpv/FFmpeg | Bestandteil der ausgelieferten Fremdbinaries | Tatsächliche Abhängigkeiten, Versionen, Copyrights und Lizenztexte anhand Buildkonfiguration und Quellen erfassen. Nicht pauschal unter HomeCam lizenzieren. |

HomeCam startet die Fremdprogramme separat; daraus allein folgt keine abschließende Bewertung sämtlicher Lizenzpflichten. Die eigenen Rechteinhaber bleiben die jeweiligen Projektmitwirkenden und Bibliotheksautoren.

## Vor Auslieferung zu vervollständigen

1. Für mpv und FFmpeg konkrete Releases und Assets fixieren, statt `releases/latest` zu verwenden. Download-URL, Assetname, SHA-256, Version, Quellrevisionen und Buildkonfiguration dokumentieren.
2. Lizenz jedes tatsächlichen Builds und aller eingebauten Bibliotheken bestimmen. Versionsausgaben und Buildinformationen aufbewahren; nonfree-Komponenten vor Auslieferung ausschließen oder klären.
3. Vollständige Lizenz- und Copyright-Texte unter `licenses/` übernehmen und fehlende Pflichtdateien beim Paketbau als Fehler behandeln.
4. Exakt korrespondierende Quellen einschließlich Abhängigkeiten, Patches und erforderlicher Buildskripte gemäß den jeweiligen Lizenzen verfügbar machen. Ein allgemeiner Homepage-Link oder ein aktueller Quellbranch genügt nicht als Nachweis für eine ältere Binary.
5. Die passende .NET-Runtimeversion und ihre Lizenz-/Third-Party-Dateien erfassen.
6. HomeCam-Lizenz und dieses Dokument zusammen mit vollständigen Fremdlizenztexten in Setup und portablem Paket mitliefern. Auch Quellenpakete dürfen keiner HomeCam-Zustimmungspflicht unterliegen.
7. Finales Paket auspacken und Vollständigkeit, Dateiversionen, Prüfsummen sowie Quellbezug prüfen. Erst danach Release-Freigabe.

Es wird bewusst kein leeres oder unvollständiges `licenses/`-Verzeichnis als erfüllter Nachweis angelegt.

## Primärquellen

- [mpv Copyright](https://github.com/mpv-player/mpv/blob/master/Copyright): Standardlizenz, LGPL-Modus und Bedeutung eingebauter Bibliotheken.
- [FFmpeg License and Legal Considerations](https://ffmpeg.org/legal.html): LGPL/GPL und korrespondierende Quellen.
- [BtbN Buildvarianten](https://github.com/BtbN/FFmpeg-Builds#readme): LGPL-, GPL- und nonfree-Pakete.
- [GNU GPL FAQ – Aggregation](https://www.gnu.org/licenses/gpl-faq.en.html#MereAggregation): getrennte Programme und Zusammenstellung.

Diese Verweise dienen der Prüfung. Maßgeblich für die Auslieferung sind die Lizenztexte und Quellen der konkret gewählten Builds.
