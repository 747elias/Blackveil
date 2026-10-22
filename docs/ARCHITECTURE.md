# Architektur und Fehlerpfade

Ein benannter `Local\Blackveil.Singleton`-Mutex verhindert mehrere Instanzen pro interaktiver Sitzung. Ein unsichtbares Controller-HWND verarbeitet den globalen Hotkey und Systemereignisse; das Tray-Symbol ist die einzige Bedienoberfläche im inaktiven Zustand.

Beim Aktivieren wird ein eigener STA-Thread mit Nachrichtenpumpe gestartet. Er installiert `WH_KEYBOARD_LL` und `WH_MOUSE_LL`. Jeder native Fehler bricht die Aktivierung ab. Danach werden der Cursor ausgeblendet und pro `Screen.Bounds` randlose schwarze Topmost-Fenster angelegt. Der Controller besitzt alle Ressourcen bereits während ihrer Erstellung, damit auch teilweise Aktivierungen bereinigt werden. Bei Monitorwechseln entstehen zuerst neue Abdeckfenster; alte Fenster werden danach geschlossen. Ein Fehler führt zur vollständigen Deaktivierung. `WM_DPICHANGED` passt die physische Monitorabdeckung erneut an.

## Zwei Hotkey-Pfade

Im inaktiven Zustand löst `RegisterHotKey(MOD_CONTROL | MOD_SHIFT | MOD_NOREPEAT)` die Aktivierung aus. Beim Installieren liest der Hook nur den aktuellen Zustand der Hotkey-Tasten; das bereits gehaltene Q der Aktivierung wird nicht als neue Betätigung gewertet.

Im aktiven Zustand werden alle gewöhnlichen Tastaturereignisse konsumiert. Daher kann Windows den registrierten Hotkey nicht zuverlässig erzeugen. Die getestete `KeyboardGate` erkennt im Hook selbst eine neue Ctrl+Shift+Q-Betätigung und postet die Entsperrnachricht **beim Loslassen von Q**. So kann das verbleibende Auto-Repeat nach dem Entfernen des Hooks nicht sofort wieder aktivieren. Kein Tasteninhalt wird gespeichert oder geloggt. Die Zustandsmenge lebt nur im aktiven Hook-Objekt.

Die Windows-Tests gehen durch die tatsächlichen nativen Hooks, nicht durch einen nachgebauten Test-Hook. Synthetische Eingaben sind dafür bewusst erlaubt; Blackveil stellt keine Sicherheitsgrenze dar.

Nach Entfernen der Hooks werden Key-up-Ereignisse für zuvor gehaltene Tasten und die Hotkey-Tasten gesendet, um vor der Aktivierung durchgereichte Tastenzustände auszugleichen. Insbesondere wird auch Q freigegeben, damit MOD_NOREPEAT die nächste Aktivierung zulässt. Das ändert keine dauerhafte Eingabekonfiguration. UIPI kann dies bei erhöhten Anwendungen einschränken.

## Aufräumen und Grenzen

Deaktivierung ist idempotent: aktiven Wächter stoppen, Hooks auf ihrem Besitzerthread entfernen, alle Fenster schliessen, Cursor-Hide ausgleichen. Der inaktive Zustand hat keine Timer. Der aktive UI-Heartbeat kontrolliert den Hook-Thread; ein unabhängiger Wächter beendet bei fehlender UI-Reaktion nach ca. zehn Sekunden den Prozess, damit Windows seine Ressourcen freigibt. Er läuft ausschliesslich während des Schwarzbilds.

Ein Desktopwechsel wird über `EVENT_SYSTEM_DESKTOPSWITCH` gemeldet; Power-/Sitzungsereignisse führen ebenfalls vorsorglich zur Deaktivierung. Der Hotkey bleibt registriert und kann danach erneut aktivieren. Verlust des Controller-HWND beim Posten einer Entsperrnachricht führt zum Prozessende. Ein Hook kann von Windows nach einem Timeout still entfernt werden; es gibt keine unterstützte API, um dies zuverlässig am Handle abzufragen. Kurze Callbacks, eigener Thread und der parallel registrierte Hotkey begrenzen dieses Risiko, können aber keine Systemgarantie geben.

`try/finally`, `Application.ThreadException` und `AppDomain.UnhandledException` decken behandelbare Fehler ab. Bei hartem Abbruch laufen Handler nicht garantiert; Windows entfernt prozesseigene HWNDs und Hooks. Sicherheitsdesktop, UAC, geschützte Eingaben, erhöhte Prozesse und andere Integritätsstufen bleiben ausserhalb einer vollständigen Garantie. Es gibt weder Treiber noch `BlockInput` noch globale Einstellungsänderungen.
