# V0.5 Beta 4 – Windows-Fenstersymbol

Hauptfenster und Einstellungen verwenden jetzt explizit das vorhandene HomeCamMonitor-Icon als eingebettete Ressource. Zuvor war nur das EXE-Icon gesetzt, während WinForms zur Laufzeit sein Standardsymbol verwendete. Windows kann nun das richtige Fenstersymbol für Taskleiste, Vorschau und Alt+Tab verwenden.

Das Logo selbst bleibt unverändert. EXE, Setup, Startmenü- und Desktop-Verknüpfungen verwenden bereits das gleiche Icon. Die bestehende Installation registriert noch keinen Eintrag unter installierten Apps; dieser Fix ergänzt keinen Deinstallationsmechanismus.
