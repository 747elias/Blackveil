# Änderungsprotokoll

## [2.0.0] - 2026-10-25

- Einstellungsfenster per Linksklick auf das Tray-Symbol oder über «Einstellungen» im Kontextmenü.
- Shortcut durch Drücken aufnehmen, auf Standard zurücksetzen und dauerhaft speichern.
- Unterstützt Ctrl-/Alt-/Shift-Kombinationen mit Buchstaben, Ziffern und F-Tasten (ausser F12).
- Kollisionsprüfung vor Übernahme; bisheriger Shortcut bleibt bei Fehlern erhalten.
- Konfigurierter Shortcut gilt für Windows-Registrierung, Eingabe-Hook, Info und Tray-Hinweis.
- Erweiterte Windows-Tests für echte GUI-Tastenaufnahme, Speicherung, Neustart und Konfliktbehandlung.

## [1.0.0] - 2026-10-22

- Erste Version für Windows 11 x64 mit .NET 10 LTS und WinForms.
- Tray-Steuerung und globaler Hotkey Ctrl + Shift + Q mit MOD_NOREPEAT.
- Schwarze Monitorfenster mit Per-Monitor-V2 und dynamischen Monitorwechseln.
- Separate Low-Level-Hooks, eigener Entsperrpfad und Bereinigung bei Fehlern.
- Touch-, Gesten- und Stiftnachrichten werden auf den Fenstern verworfen.
- Benutzerinstallation, gemeinsamer HKCU-Autostart, portable EXE und ZIP.
- Automatisierte Logik- und native Windows-Integrationstests.
- Unsigned: Windows SmartScreen kann eine Warnung anzeigen. Blackveil ist keine Windows-Sperre und blockiert keinen Sicherheitsdesktop.
