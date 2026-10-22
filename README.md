# Blackveil

Vorübergehender Sicht- und Eingabeschutz für **Windows 11 x64**, geschrieben in C# mit **.NET 10 LTS**. Kein Hauptfenster: Blackveil läuft ausschliesslich im Infobereich. Es benötigt keine Administratorrechte und keine separat installierte .NET-Runtime.

**[Neueste Version herunterladen](https://github.com/747elias/Blackveil/releases/latest)** · [Repository](https://github.com/747elias/Blackveil) · [Workflows](https://github.com/747elias/Blackveil/actions) · [Testplan](docs/TESTPLAN.md)

## Installation und Bedienung

1. `Blackveil-Setup-1.0.0.exe` aus dem Release starten. Die Installation erfolgt pro Benutzer unter `%LOCALAPPDATA%\Programs\Blackveil`, ohne UAC-Erhöhung. Optional «Blackveil mit Windows starten» auswählen.
2. Alternativ `Blackveil.exe` in einem dauerhaften Ordner ablegen oder die portable ZIP entpacken und die EXE starten. Für portablen Autostart darf dieser Pfad anschliessend nicht verändert werden.
3. **Ctrl + Shift + Q** aktiviert schwarze, randlose Fenster auf allen Monitoren. Den Hotkey erneut drücken und **Q loslassen**, um den Modus zu beenden. Gehaltenes Q schaltet nicht mehrfach um.
4. Im Kontextmenü des Tray-Symbols: «Schwarzbild aktivieren», «Mit Windows starten», «Info» mit Version und «Beenden». Während des Schwarzbilds ist das Menü durch den Eingabeschutz nicht erreichbar.

Ein bereits belegter Hotkey verhindert den Start mit einer verständlichen Fehlermeldung. Eine zweite Instanz beendet sich sofort. Ein Autostart zeigt nur das Tray-Symbol und aktiviert kein Schwarzbild.

Die Dateien sind **nicht code-signiert**. Windows SmartScreen kann «Der Computer wurde durch Windows geschützt» anzeigen. Vor dem Ausführen Herkunft und SHA256-Prüfsummen überprüfen. Auf ARM64 ist diese Ausgabe höchstens über Windows-x64-Emulation nutzbar; es gibt kein natives ARM64-Release.

```powershell
Get-FileHash .\Blackveil.exe -Algorithm SHA256
Get-Content .\SHA256SUMS.txt
```

## Deinstallation und Autostart

Zuerst den Schwarzbild-Modus mit dem Hotkey verlassen und Blackveil über das Tray-Menü beenden. Danach unter Windows «Einstellungen → Apps → Installierte Apps → Blackveil» deinstallieren. Der Installer fordert beim laufenden Programm zum Beenden auf. Alle eigenen Run-Einträge und Logs werden entfernt. Andere Run-Werte bleiben erhalten.

Menü und Installer verwenden denselben Wert `Blackveil` unter `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`. Der Inhalt ist `"<vollständiger EXE-Pfad>" --autostart`. Der Installer liest den tatsächlichen Wert für seine Vorauswahl; eine explizite `/TASKS=autostart`-Auswahl wird respektiert. Bei einer portablen Ausgabe vor dem Löschen der EXE «Mit Windows starten» ausschalten; bei Bedarf den genannten Registry-Wert manuell entfernen.

## Lokal bauen und testen

Voraussetzungen: Windows 11, aktuelles **.NET 10 SDK**, für den Installer **Inno Setup 6**. Visual Studio ist optional. SDK-Auswahl: `global.json`. Version: ausschliesslich `Directory.Build.props`.

```powershell
git clone https://github.com/747elias/Blackveil.git
cd Blackveil
dotnet build Blackveil.sln -c Release -warnaserror
dotnet test tests/Blackveil.Core.Tests -c Release --no-build
# Verwendet echte user32-Hooks und simulierte Tasten: nur in einer freien Testsitzung!
dotnet run --project tests/Blackveil.Windows.Tests -c Release --no-build
./scripts/Package.ps1
```

Die Windows-Integrationstests benötigen einen entsperrten interaktiven Desktop und dürfen nicht parallel mit Blackveil laufen. Sie scheitern ausdrücklich, wenn der native Hotkey oder der Eingabepfad nicht funktioniert. Es wird kein Erfolg durch Überspringen dieser Tests vorgetäuscht.

`Package.ps1` publiziert `win-x64` als self-contained Single-File-EXE, erstellt den Installer mit ISCC, die portable ZIP sowie `SHA256SUMS.txt` in `artifacts/release`. Die ZIP enthält zusätzlich README und MIT-Lizenz. Die native .NET-Laufzeit kann beim ersten Start Dateien in einen temporären .NET-Bundle-Ordner extrahieren. Keine Build-Ergebnisse werden eingecheckt.

Unter Linux ist Cross-Build möglich (`dotnet build` und `dotnet publish ... -r win-x64 --self-contained true`); WinForms und die nativen Integrationstests können dort nicht ausgeführt werden. Ein Installer wird auf Windows gebaut.

## GitHub Actions und neue Releases

`build.yml` baut bei jedem Push und Pull Request auf `windows-latest`, behandelt Warnungen als Fehler und führt Logik- sowie native Integrationstests aus. `release.yml` wiederholt diese Prüfung bei `v*`-Tags, installiert bei Bedarf Inno Setup, paketiert, prüft die Installation/Deinstallation und veröffentlicht erst nach Prüfung aller vier Assets. Ein manueller Workflow-Start auf einem Branch prüft die Paketierung, ohne ein Release zu veröffentlichen. Release Notes stammen aus dem passenden Abschnitt der `CHANGELOG.md`. Bei fehlgeschlagenem Upload bleibt ein neu erstelltes Release zunächst ein Entwurf.

Für eine neue Version `Version` in `Directory.Build.props` erhöhen und einen passenden `CHANGELOG.md`-Abschnitt ergänzen. Anschliessend zum Beispiel:

```powershell
git add Directory.Build.props CHANGELOG.md
git commit -m "Release version 1.0.1"
git push origin main
git tag -a v1.0.1 -m "Blackveil 1.0.1"
git push origin v1.0.1
gh run list --workflow release.yml
gh release view v1.0.1 --json url,assets
```

Tag und zentrale Version müssen übereinstimmen. Die Workflows nutzen nur das kurzlebige `GITHUB_TOKEN` mit den nötigen Repository-Rechten. Lokal `gh auth login` verwenden; keine Zugangsdaten in Dateien oder Commits speichern. Einen fehlgeschlagenen Lauf über `gh run view <RUN-ID> --log-failed` untersuchen und nach einer Korrektur eine neue Version veröffentlichen. Ein unverändert fehlgeschlagener Infrastruktur-Lauf kann mit `gh run rerun <RUN-ID> --failed` erneut gestartet werden.

## Technische Grenzen

Blackveil ist **keine Windows-Sperre und keine Sicherheitsgrenze**. Für Schutz vor fremdem Zugriff `Win + L` verwenden, nachdem Blackveil verlassen wurde. **Ctrl + Alt + Entf**, UAC, Sicherheitsdesktop, Windows-Sitzungssperre und andere geschützte Systemfunktionen werden weder umgangen noch blockiert. Ein Wechsel des Desktops, der Sitzung oder des Energiezustands beendet das Schwarzbild vorsorglich; nach dem Aufwachen kann es wieder aktiviert werden.

Low-Level-Hooks unterdrücken gewöhnliche Tastatur- und Mauseingaben im eigenen Eingabedesktop. Pointer-, Touch- und Gestennachrichten werden zusätzlich auf den schwarzen Fenstern verworfen. Windows-/OEM-Gesten, Hardwaretasten, Treiber, Raw-Input-Verbraucher, geschützte Oberflächen und andere Topmost-Fenster liegen ausserhalb einer garantierten vollständigen Abschirmung. Windows kann einen zu langsamen Hook still entfernen; der normale registrierte Hotkey bleibt dann als zusätzlicher Ausweg verfügbar. Die Hooks laufen deshalb in einem separaten, kurzen Nachrichtenpfad ohne Dateizugriffe.

Gegenüber **erhöht gestarteten Anwendungen gelten UIPI/Integritätsgrenzen**. Eingabeunterdrückung und die synthetischen Key-up-Ereignisse zur Bereinigung zuvor gehaltener Tasten können dort eingeschränkt sein. Blackveil läuft `asInvoker`, ohne UIAccess, Treiber oder Änderungen an Sicherheitsmechanismen. Es verwendet kein `BlockInput` und verändert keine dauerhaften Tastatur-, Maus- oder Touch-Einstellungen. Nach dem Verlassen die Hotkey-Modifikatoren loslassen.

Beim normalen Deaktivieren, bei teilweiser Aktivierung und behandelten Fehlern werden Hooks entfernt, Fenster geschlossen und der ausgeglichene Cursor-Hide-Aufruf rückgängig gemacht. Bei einer unerwarteten Hook-Thread-Störung wird der Modus beendet; bei dauerhaft blockierter Oberfläche beendet ein nur im aktiven Modus laufender Wächter den Prozess nach ungefähr zehn Sekunden. Bei hartem Prozessabbruch können keine .NET-Handler garantiert laufen: Windows entfernt die prozesseigenen Hooks, Fenster und Cursor-Zustände. Ein System- oder Treiberstillstand kann von einer Benutzer-App nicht behoben werden.

Im inaktiven Modus existieren keine Polling-Timer. Fehlerlogs liegen unter `%LOCALAPPDATA%\Blackveil\logs`; Dateien älter als 14 Tage werden beim nächsten Logeintrag entfernt. Es werden keine Tasteneingaben, Screenshots oder privaten Inhalte protokolliert.

## Aufbau und UI-Wahl

`src/Blackveil` enthält WinForms, Win32-Interop und die Tray-App, `src/Blackveil.Core` die plattformunabhängige Tastenzustandslogik. `tests/` enthält Unit- und native Integrationstests, `installer/` das Inno-Skript, `scripts/` die gemeinsame Paketierung und `docs/` Architektur und Testplan.

**WinForms** passt zu NotifyIcon, klassischen HWNDs und Low-Level-Nachrichten bei geringer Leerlauflast. **WPF** bietet Layout und Rendering, die schwarze Fenster nicht benötigen, und würde ebenfalls Win32-Interop voraussetzen. **WinUI 3** bietet moderne Controls, bringt für diese reine Tray-App jedoch zusätzliche Windows-App-SDK-/Deployment-Komplexität. Per-Monitor-V2 wird über die .NET-WinForms-Konfiguration und `Application.SetHighDpiMode` vor dem ersten HWND aktiviert.

Lizenz: [MIT](LICENSE).
