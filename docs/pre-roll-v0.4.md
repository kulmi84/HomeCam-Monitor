# Vorlauf bei Bewegung – V0.4 Beta 2

In den Voreinstellungen sind Snapshot-Vorlauf und Video-Vorlauf unabhängig wählbar: Aus, 1, 3 oder 5 Sekunden. Beide sind standardmäßig aus. Einstellungen gelten global für automatische Bewegungs- und Personenaufnahmen; manuelle Aufnahmen bleiben unverändert.

Nur aktivierte Kameras mit passender Aufnahmeaktion werden gepuffert. Je Kamera entsteht ein zusätzlicher FFmpeg-Stream mit einem begrenzten temporären Ringpuffer (15 Bilder/s, MPEG-4, Qualität 2). Die Option benötigt zusätzliche CPU-Leistung, Netzwerkbandbreite und temporären Speicher. Bei Aus läuft kein Puffer. Puffer werden beim Beenden gelöscht.

Snapshots stammen aus dem ausgewählten Zeitpunkt vor dem Signal. Video enthält die gewählte Vorlaufzeit zusätzlich zur eingestellten Aufnahmedauer nach dem Signal. Während des Pufferaufbaus wird normal ohne Vorlauf aufgenommen. Bei Streamfehlern verbindet sich der Puffer neu. Die zeitliche Zuordnung basiert auf den abgeschlossenen Segmenten; Netzwerk- und Encoderverzögerungen können die Genauigkeit beeinflussen.

Validierung: Einstellungen/Standardwerte, Auswahl des früheren Segments und FFmpeg-Integrationstest mit gepuffertem Snapshot und lesbarer Videoaufnahme.
