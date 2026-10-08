# V0.4 – gezielte Stream-Wiederverbindung

- Rechtsklick → „Aktuellen Stream neu verbinden“ startet im Einzelbild den aktuellen Stream neu. Im Raster ist die angeklickte Kamera das Ziel; leere Felder bieten keine Wiederverbindung.
- Durchgehende mpv-Überwachung von `time-pos`: 12 Sekunden ohne fortschreitende Wiedergabe lösen eine Wiederverbindung nur des betroffenen Streams aus. Ein unverändertes Motiv bei weiterlaufender Wiedergabe zählt nicht als Stillstand.
- Offline- und Verbindungsanzeigen bleiben erhalten. Gesunde Streams im Raster laufen weiter.
- Die Erkennung prüft den Wiedergabefortschritt. Eine Kamera, die eingefrorene Bildinhalte mit weiterlaufenden Zeitstempeln sendet, wird dadurch nicht erkannt.
