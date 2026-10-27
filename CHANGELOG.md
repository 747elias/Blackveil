# Änderungsprotokoll

## [2.1.1] - 2026-10-27

- Setup mit Wahl zwischen «Nur für mich» und «Für alle Benutzer», passenden Installationspfaden und Windows-Rechteabfrage.
- Neues Erscheinungsbild: dunkle Navigation, helle Inhaltskarten, türkise Akzente und deutlichere Typografie in Einstellungen, Updates und Änderungsfenster.
- Gebrandetes Setup mit eigener Grafik, Willkommensseite und sichtbarem Installationsumfang.
- Updates erkennen Benutzer- und Systeminstallationen; systemweite Updates bleiben systemweit und verwenden die normale UAC-Bestätigung für den Installer. Die Tray-App bleibt asInvoker.
- Persönliche Einstellungen und Autostart bleiben pro Benutzer; bei Systeminstallationen wird Autostart über das Tray-Menü eingerichtet.
- Erweiterte Windows-Tests für Systeminstallation, HKLM-Registrierung, Update und Deinstallation; UI-Screenshots als Workflow-Artefakte.

## [2.1.0] - 2026-10-27

- Update-Einstellungen mit getrennten Schaltern für automatische Suche und automatische Installation.
- Suchen beim Start, alle sechs Stunden, täglich zur Uhrzeit oder alle sieben Tage; Installationen sofort, beim nächsten Start oder täglich zur Uhrzeit.
- Manuelle Suche per Button, auf Wunsch ohne Rückfrage installieren oder vor der Installation fragen.
- Downloads aus dem offiziellen GitHub-Release mit SHA256-Prüfung; stille Benutzerinstallation bzw. Austausch der portablen EXE und automatischer Neustart.
- Fenster mit Änderungen nach einem Update, auch für übersprungene Versionen; Einstellungen und Autostart bleiben erhalten.
- Geplante Updates werden während des Schwarzbilds verschoben. Fehler führen nicht zu einer automatischen Installationsschleife.
- Automatisierte Tests für Zeitpläne, GUI-Einstellungen, Downloads, Prüfsummen sowie echte portable und installierte Updates.

## [2.0.1] - 2026-10-25

- Freie Tastatur-Shortcut-Auswahl: auch Shift ohne Ctrl/Alt, einzelne Tasten, Win, F12 und Sondertasten.
- Manuelle Tastenliste mit Ctrl-/Alt-/Shift-/Win-Checkboxen für vom System abgefangene Eingaben.
- Einzelne Modifikatortasten durch Drücken und Loslassen aufnehmen; Escape ist ebenfalls auswählbar.
- Belegte oder reservierte Shortcuts speichern, mit Hinweis statt Ablehnung. Bei fehlgeschlagener Registrierung bleibt Schwarzbild deaktiviert und die Tray-App zur Korrektur erreichbar.
- Hook-Vergleich und automatisierte Tests für Shift-, Win-, Einzel- und Modifikatortasten erweitert.

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
