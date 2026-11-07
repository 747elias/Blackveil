# Manual test plan

Use an unlocked, dedicated Windows 11 test session. Know that Ctrl + Alt + Delete remains available before each test. Blackveil must never be assessed as a replacement for Windows locking. Record the app version, Windows build, hardware, date and result.

## Core behavior

| Area | Steps | Expected result |
| --- | --- | --- |
| Tray / one instance | Start the EXE twice; open About. | One tray icon, no main window; About matches the release version. |
| Global shortcut | Focus Notepad, a browser and a full-screen app; press Ctrl + Shift + Q. | Blackout activates from ordinary foreground apps. |
| Key repeat | Hold Q for ten seconds during activation; release; press and release the shortcut again. | Activates once, exits only on the next press; no flickering or repeat activation. |
| Hook / shortcut interaction | Use left/right Ctrl and Shift; release Q first and last; repeat 20 cycles. | Every new valid chord exits; no stuck modifiers afterward. |
| Settings | Left-click the tray; record Ctrl + Alt + K; Save; restart; test Default and Cancel. | Saved combination persists; drafts are discarded; activation and exit share the new shortcut. |
| Unavailable shortcut | Reserve a shortcut using another app, then save it in Blackveil and restart. | Choice is saved, warning appears, blackout stays disabled until registration succeeds, Settings stays accessible. |
| Startup collision | Reserve Ctrl + Shift + Q elsewhere before launching a default-configured Blackveil. | Tray starts with a clear warning; no overlays or input hooks activate. |
| Keyboard | Put an editor with disposable text behind blackout; try typing, Escape, Alt + F4, Win, Alt + Tab and media keys. | Ordinary input does not operate the background; Escape/Alt + F4 do not close overlays. Record protected-system exceptions separately. |
| Mouse | Click, double-click, right-click, scroll and move; also activate during a drag. | No operation behind overlays or accidental closing; cursor hidden, then restored; no stuck buttons afterward. |
| Displays / DPI | Combine 100%, 150% and 200% scaling, negative coordinates, portrait mode and primary-display changes. | Every screen, including taskbars, is black with no visible gaps. |
| Hotplug | Connect/disconnect monitors and docks; change resolution while active and inactive. | Coverage adapts; input interception does not remain stuck. |
| Touch | Tap, double-tap, hold, swipe, pinch and use multiple fingers on each touchscreen. | Overlay discards input without passing it through or closing. Document OEM/system gesture limits. |
| Pen | Test tapping, hover, side buttons, eraser and Windows Ink. | No ordinary operation behind overlays; document protected/vendor behavior. |
| Secure desktop | Press Ctrl + Alt + Delete; separately trigger UAC. | Protected surfaces remain accessible; blackout ends as a precaution. Nothing is bypassed or blocked. |
| UIPI | Compare an elevated editor with an ordinary app. | Record input/cleanup limitations; the tray app does not request elevation. |
| Standby / session | Suspend/resume, lock/unlock and change RDP sessions. | Blackout deactivates; the shortcut works again afterward. |
| Failure / crash | Inject an exception during overlay creation; interrupt the hook thread; hang the UI; kill the process. | No lasting overlays or hooks; cursor restored; UI watchdog terminates after about ten seconds. |
| Startup | Toggle the installer task and tray preference; inspect HKCU; sign out/in. | One shared Run value, correct task preselection, background startup without blackout. |
| Personal setup / upgrade | Use a standard account and a path containing spaces; install over an existing personal copy. | No elevation; matching file/app versions; running copy must exit. |
| Uninstall | Enable startup, quit, uninstall. | Installed EXE and shortcuts removed; current installation's startup entry removed. Personal uninstall removes its data; unrelated Run entries remain. |
| Idle | Observe Task Manager for several minutes with automation off, then with a schedule enabled. | Near-zero idle CPU; no inactive input-polling timers. Record memory use. |

Also test Shift + K, a function key without modifiers, F12, Win combinations, Escape, Tab, OEM/media keys and Shift alone. Selection and saving must be possible even where Windows does not allow the combination to operate. Click the recording button to cancel capture; Escape is a valid candidate.

## Presentation workflow

Connect a projector and laptop, cue and pause a video, activate blackout, speak, reveal, start playback separately, then return to black after the clip. Both displays must be covered together. Ordinary input must be suppressed during blackout. Confirm that Blackveil does not pause playback, mute audio, change brightness or power down the projector. Check that the README describes the same behavior.

## English interface and migration from 2.1.1

On both English and German Windows, install 2.1.2 over a configured German 2.1.1 copy. Check shortcut preservation, startup state, update switches, schedules and installation scope. The existing JSON files must not need migration or be reset.

Verify English text in the tray menu and tooltip; Settings navigation and key labels; capture, save, cancel and warning states; About and version; every update schedule, prompt and status; error messages; and What's new, including entries for skipped versions. Check left/right modifier labels. Regional date/time formatting, time zone and keyboard layout must remain unchanged. Windows-owned UAC prompts may follow the Windows language.

Run Setup without command-line scope overrides: the scope chooser, welcome, license, tasks, progress, finish and uninstall pages must be English, including artwork. Upgrade an installation whose previous installer language was German. Review text wrapping at 100%, 150% and 200% DPI, small screen heights, keyboard navigation and Windows contrast settings.

## Update behavior

Toggle automatic checks and installation independently, save times and restart. With automatic checks off, the manual button must work. With checks on and auto-install off, a new release must produce a Yes/No prompt; No keeps the old app running. With automatic installation on, check the silent update, restart and cumulative What's new window.

Test each check/install schedule independently, including different daily times, standby, daylight saving and the app closed at the due time. A missed daily time with the app closed moves to the next scheduled occurrence. While blackout is active, no prompt or installer may interrupt it; work deferred because of blackout is retried afterward. Test network errors, GitHub rate limiting, truncated downloads and invalid SHA256. An unwritable portable folder must leave the app running with an error status. Preferences and startup must survive successful updates. Closing What's new and restarting must not show it again until a later version or update result warrants it. A failed version must not enter an automatic installation loop; manual retry is allowed.

## Scope, UAC and multiple users

Test personal installation as a standard account without elevation. Test system-wide installation as a standard account with normal consent and with another administrator's credentials. Launch the tray app afterward as the original normal user. Settings and startup must belong to that account, not the administrator used for elevation.

Allow and cancel UAC during a system-wide update; no elevated tray process may remain afterward. Personal updates must not become machine-wide. Test two Windows accounts and multiple active sessions. System uninstall must preserve profile data and must not edit other users' profiles. Quit/uninstall before changing scope, so two installations are not left alongside each other.

## Automated evidence and its limits

`dotnet test tests/Blackveil.Core.Tests` exercises production key-state logic, left/right modifiers, activation with Q held, repeats, incomplete chords, release order, update schedules, release versions and cumulative notes.

`dotnet run --project tests/Blackveil.Windows.Tests` tests real `RegisterHotKey`, collision, `MOD_NOREPEAT` and `SendInput`, then requires the production low-level hook to post unlock while suppressing `WM_HOTKEY`. Observer hooks verify downstream keyboard, click and wheel suppression. It also checks unhooking, protected overlay close, three full tray-app cycles, shortcut recording/persistence/restart, settings, download verification and English UI culture with regional formatting preserved. Actual forms are captured as the `ui-screenshots` workflow artifact.

`./scripts/Test-SetupUi.ps1` opens and captures the scope chooser and welcome page without installing. `./scripts/Test-Installer.ps1` installs, starts and uninstalls the packaged app in a clean test profile. `./scripts/Test-Update.ps1` uses the published German 2.1.1 release to exercise real portable, personal and system-wide updates, restart, the English What's new window, settings/startup preservation and bad-checksum recovery. It uses the current helper to initiate fixture updates; this checks the installation/restart path independently of online update discovery.

CI machine-scope checks run in an already administrative session. They do **not** replace standard-user UAC, alternate-admin-credential or multi-user checks. CI also does **not** establish physical touchscreen/pen behavior, projected image coverage, mixed-DPI hardware hotplug or standby reliability. These require real hardware and remain explicit manual checks.
