# Zusätzliche Komponenten der MSIX-Ausgabe

Der Store-Build verwendet für den Windows-StartupTask das Target `net8.0-windows10.0.19041.0` und das Microsoft-Targetingpaket `Microsoft.Windows.SDK.NET.Ref` 10.0.19041.56. Mitgeliefert werden dadurch `Microsoft.Windows.SDK.NET.dll` und `WinRT.Runtime.dll`. Die .NET-/WindowsDesktop-Laufzeit bleibt bei 8.0.32.

`WinRT.Runtime.dll` meldet Version 2.2.0.48161 und Quellrevision `8649ee3eeb2445ca2a36d80d878ef60b96a6c65d`. Die MIT-Lizenz wurde aus dem Microsoft-CsWinRT-Repository an dieser Revision übernommen. Das SDK-Paket benennt als Lizenzquelle `https://aka.ms/WinSDKLicenseURL`; die dort gelieferte RTF-Datei und die Original-NuGet-Metadaten liegen unter `licenses/windows-sdk-net-ref/`.

Diese Ergänzung dokumentiert Herkunft und mitgelieferte Hinweise. Die abschließende Komponenten-/Distributionsprüfung bleibt offen; der bisherige V2-Audit deckt diese zusätzlichen Dateien noch nicht als freigegeben ab. Medienprogramme und GStreamer-Plugins werden beim Store-Build nicht ausgetauscht und müssen weiterhin zu den im V2-Audit festgelegten Hashes passen.

Primärquelle der WinRT-Lizenz: https://github.com/microsoft/CsWinRT/blob/8649ee3eeb2445ca2a36d80d878ef60b96a6c65d/LICENSE

Ab Store-Kandidat 2.0.3.0 wird der veröffentlichte Mediensatz verwendet. Die vollständigen korrespondierenden Quellen von mpv/FFmpeg, Neolink und GStreamer sowie deren Prüfsummen stehen unter https://github.com/kulmi84/HomeCam-Monitor/releases/tag/v2.0.0 bereit. Der Paketbau prüft sämtliche 3.200 freigegebenen Lizenzdateien zusätzlich zu den fünf Komponentenbinaries und 261 Plugins. Diese Prüfung ist keine rechtliche Bewertung der gesamten Store-Distribution.
