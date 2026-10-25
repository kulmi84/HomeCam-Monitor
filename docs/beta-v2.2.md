# HomeCam Monitor V2 – Entwicklung Beta 2.2

Stand: 24.10.2026. Auf Nutzerauftrag basiert die neue Beta auf der stabilen Version **2.1.0.0**.

- Quellbasis: Commit `053e3206652f94a25f2b16a2cd9cff996676366e` (Version 2.1 mit anpassbarem Bild im Bild und stabiler Fensterdarstellung vorbereiten).
- Entwicklungsbranch: `codex/beta-v2.2`.
- Erste Produktversion: `2.2.0-beta.1`; Assembly-/Datei-/Paketversion: `2.2.0.1`.
- Build: `HomeCamMonitor.V2.Beta.csproj` mit `NextBeta=true`. Die stabile Konfiguration behält `HomeCamV2Version=2.1.0.0`.
- Die vorhandene NextBeta-Identität bleibt erhalten: `HomeCamMonitor-V2-NextBeta`. Bestehende NextBeta-Einstellungen gehören damit auch zur neuen Testreihe; stabile V2-Einstellungen werden nicht automatisch übernommen.

Die erste Beta übernimmt den Funktionsumfang der 2.1 einschließlich verschiebbarem und skalierbarem Bild im Bild, Pausieren des Ausblendens beim Mauszeiger, manueller Fenstersteuerung und Über-Dialog. Weitere Funktionen sind noch nicht festgelegt.

## Bauen

```powershell
dotnet build HomeCamMonitor.V2.Beta.csproj -c Release -p:NextBeta=true
```

Der Installer-Build `.github/scripts/build-next-beta.ps1` liest die Beta-Version aus dem Projekt und verwendet die Komponenten aus `../Releases/2.1.0.0/HomeCamMonitor-V2-2.1.0.0-win-x64.zip`. Neue Pakete landen unter `../Beta/2.2.0-beta.1/`; vorhandene Ausgaben werden nicht überschrieben.

Für MSIX unterstützt `.github/scripts/build-next-beta-msix.ps1` ebenfalls die neue Versionsreihe und die bisherige separate NextBeta-Paketidentität. Ein neues Paket muss vor Weitergabe gesondert gebaut und geprüft werden.

## Prüfung der Entwicklungsbasis

Der Release-Build mit `NextBeta=true` und vorhandenen NuGet-Abhängigkeiten (`--no-restore`) war erfolgreich: keine Fehler, eine bestehende Warnung CS0168 in `Program.cs`. Die erzeugte DLL meldet Produktversion `2.2.0-beta.1` und Dateiversion `2.2.0.1`. Die Auswertung mit `FinalRelease=true` ergibt weiterhin `2.1.0.0`. Die drei angepassten Beta-Build-/Prüfskripte bestehen die PowerShell-Syntaxprüfung.

Ein regulärer Restore konnte die persönliche NuGet.Config aufgrund eingeschränkter Zugriffsrechte nicht lesen; der Build nutzte deshalb die bereits vorhandenen Abhängigkeiten. Installer und MSIX wurden für diese Vorbereitung noch nicht erzeugt. Ein Kameratest wurde nicht durchgeführt.

## Lokale Store-Ausgabe

Anschließend auf Nutzerauftrag gebaut: `../Beta/2.2.0-beta.1/HomeCamMonitor-NextBeta-2.2.0.1-x64.msix`. Der Build mit Zugriff auf die vorhandene NuGet-Konfiguration war erfolgreich. Das Paket verwendet die separate NextBeta-Identität und das vorhandene Testzertifikat. Windows-Signaturprüfung, 5102 Nutzdatei-Prüfsummen, Komponenten-/Lizenzinventar, Symbole, tatsächliches Autostartmanifest sowie vorhandene Prüfungen für Speicherpfade, Darstellung, Bild im Bild und 473 Übersetzungen bestanden. Prüfsummen, Starter, öffentliches Zertifikat und Testprotokoll liegen beim Paket. Installation und manueller Kameratest stehen aus.
