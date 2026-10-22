# Manueller Testplan

Auf einer freien Windows-11-Testsitzung beginnen. Vor jedem Test wissen, dass Ctrl + Alt + Entf weiterhin funktioniert. Blackveil nie als Ersatz einer Windows-Sperre bewerten. Ergebnisse mit Windows-Version, Release, Hardware und Datum festhalten.

| Bereich | Durchführung | Erwartung |
|---|---|---|
| Tray / Instanz | EXE starten, nochmals starten, Info öffnen | Nur ein Tray-Symbol, kein Hauptfenster, Version 1.0.0 |
| Globaler Hotkey | Notepad, Browser und Vollbild-App fokussieren; Ctrl+Shift+Q drücken | Schwarzbild aus jedem normalen Vordergrundprogramm |
| Wiederholung | Q während des ersten Hotkeys zehn Sekunden halten, loslassen; erneut drücken und loslassen | Einmal an, erst durch zweite Betätigung aus; kein Flackern/Reaktivieren |
| Hook / Hotkey | Linke/rechte Ctrl-/Shift-Kombinationen, Q zuerst und zuletzt loslassen; 20 Zyklen | Jeder gültige neue Hotkey entsperrt; keine hängenden Modifikatoren danach |
| Kollision | In separatem Tool Ctrl+Shift+Q registrieren, Blackveil starten | Verständliche Fehlermeldung; keine Fenster, keine Hooks zurückgelassen |
| Tastatur | Notepad mit ungespeichertem Testtext im Hintergrund; Zeichen, Escape, Alt+F4, Win, Alt+Tab, Multimedia testen | Gewöhnliche Eingaben ändern den Hintergrund nicht; Escape/Alt+F4 beenden nicht; geschützte Systemfunktionen gesondert erfassen |
| Maus | Vorher Klickzähler/Editor öffnen; klicken, doppelklicken, rechtsklicken, scrollen, bewegen | Keine Bedienung dahinter, kein Schliessen; Cursor unsichtbar und danach sichtbar |
| Monitore / DPI | 100 %, 150 %, 200 % kombinieren; negative Koordinaten, Hochformat, primären Monitor wechseln | Jeder Monitor vollständig schwarz, inklusive Taskleiste; keine sichtbaren Ränder |
| Hotplug | Im aktiven und inaktiven Modus Monitor/Dock anschliessen/entfernen, Auflösung ändern | Fenster passen sich an; kein festhängender Eingabeschutz |
| Touch | Auf jedem Touchscreen tippen, doppeltippen, lang drücken, mehrere Finger, wischen, zoomen | Fenster verwerfen Touch/Gesten, kein Durchreichen oder Schliessen; OEM/Systemgesten als Grenze erfassen |
| Stift | Tippen, Hover, Seitentaste, Radierer, Windows Ink | Keine Bedienung darunter; Grenzen geschützter Herstellerfunktionen dokumentieren |
| Sicherheitsdesktop | Ctrl+Alt+Entf; separat UAC auslösen | Systemoberfläche erreichbar; Blackveil umgeht/blockiert nichts, Schwarzbild wird vorsorglich beendet |
| UIPI | Erhöhten Editor und normale App vergleichen | Einschränkungen protokollieren; keine Administratorrechte anfordern |
| Standby / Sitzung | Suspend/Resume, Sperren/Entsperren, RDP-Wechsel | Vorsorglich deaktiviert; anschliessend neuer Hotkey funktionsfähig |
| Fehler / Absturz | Im Debugger Ausnahme während Fenstererstellung auslösen; Hook-Thread und UI-Stillstand prüfen; Prozess im Task-Manager beenden | Keine bleibenden Fenster/Hooks; Cursor zurück; UI-Hänger beendet Prozess nach ca. 10 s |
| Autostart | Installer-Option an/aus, Tray umschalten, Registry vergleichen, ab-/anmelden | Ein HKCU-Wert, korrekte gemeinsame Vorauswahl, Start ohne Schwarzbild |
| Installer / Upgrade | Normaler Benutzer, Pfad mit Leerzeichen; über bestehende Installation installieren | Keine Erhöhung, Version konsistent, laufende Instanz muss beendet werden |
| Deinstallation | Autostart per Menü einschalten, App beenden, deinstallieren | EXE, eigener Run-Wert, Verknüpfung und Logs entfernt; fremde Run-Werte unverändert |
| Leerlauf | Task-Manager mehrere Minuten beobachten | Nahezu 0 % CPU ohne periodische App-Timer; Speichernutzung dokumentieren |

## Automatisierte Nachweise

`dotnet test tests/Blackveil.Core.Tests` prüft die exakte Tastenzustandslogik des Hooks: alle linken/rechten Modifikatoren, Aktivierung mit gehaltenem Q, Wiederholungen, unvollständige Kombinationen und Freigabereihenfolgen.

`dotnet run --project tests/Blackveil.Windows.Tests` registriert einen echten globalen Hotkey mit `MOD_NOREPEAT`, prüft Kollision und Auslösung über `SendInput`, installiert den produktiven Low-Level-Hook und verlangt dessen Entsperrnachricht bei gleichzeitig unterdrücktem `WM_HOTKEY`. Anschliessend wird der Hook entfernt und die normale Hotkey-Auslösung erneut geprüft. Nachgeschaltete Beobachter-Hooks müssen während des Schutzes keine Tastatur-, Klick- oder Radereignisse erhalten. Ein schwarzes Testfenster muss `Close()` abweisen und kontrolliert geschlossen werden können. Drei anschliessende vollständige Tray-App-Zyklen prüfen Aktivierung, Monitorfenster, Escape-Unterdrückung, Q-Zustandsbereinigung und erneute Aktivierung.

Diese Tests belegen keine physische Touch-/Stiftverarbeitung, Bildschirmabdeckung, Mixed-DPI-Hotplug oder Standby. Diese Punkte bleiben auf echter Hardware zu prüfen. Auch ein erfolgreicher Windows-CI-Lauf ersetzt diese Tests nicht.
