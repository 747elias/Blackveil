# Architecture and failure handling

Blackveil is a WinForms tray app targeting .NET 10 LTS and Windows 11 x64. A named `Local\Blackveil.Singleton` mutex permits one instance per interactive session. A hidden controller HWND handles the global shortcut and system events. There is no main window.

## Activation and display coverage

Activation starts a dedicated STA message-pump thread that installs `WH_KEYBOARD_LL` and `WH_MOUSE_LL`. A native startup failure aborts activation. The controller then hides the cursor and creates a borderless black topmost window for every `Screen.Bounds`. Resources belong to the cleanup path as soon as they are created, so partial activation is also cleaned up.

Display changes create replacement overlays before closing the old ones. A failure deactivates the entire mode. Per-Monitor-V2 is enabled before any HWND is created; `WM_DPICHANGED` reapplies physical monitor coverage. The overlays consume pointer, touch and gesture messages, and reject ordinary close requests.

Blackveil covers all displays together. It does not control projector power, display brightness, media playback or audio.

## Two shortcut paths

While inactive, `RegisterHotKey` uses the saved modifiers with `MOD_NOREPEAT`. The default is Ctrl + Shift + Q. The hook seeds its state from keys already held at activation; the initial held Q does not count as a new shortcut press.

While active, ordinary keyboard messages are consumed before Windows can reliably generate `WM_HOTKEY`. `KeyboardGate` therefore recognizes the shortcut inside the hook and posts an unlock message **when its main key is released**. This prevents held-key repeat from immediately reactivating blackout after unhooking. No typed content is retained or logged; only transient key state lives in the active gate.

Integration tests go through the production native hooks. Synthetic input is intentionally accepted: this app is not a security boundary. Observer hooks verify downstream keyboard, click and wheel suppression, then verify input resumes after unhooking.

After hook removal, cleanup sends key-up events for initially held keys and shortcut keys, plus mouse-up events for initially held buttons. Releasing Q also lets `MOD_NOREPEAT` accept the next activation. This changes no persistent input settings. UIPI may restrict synthetic releases across integrity levels.

## Cleanup and limits

Deactivation is idempotent: stop the active watchdog, remove hooks on their owner thread, close overlays and balance cursor hiding. An active-only UI heartbeat checks hook-thread health. A separate watchdog terminates a UI that has stopped responding for about ten seconds so Windows can release process-owned resources. Neither input monitor runs while inactive; an enabled update schedule has its own next-event timer.

`EVENT_SYSTEM_DESKTOPSWITCH`, power changes and session changes deactivate blackout as a precaution. The shortcut remains available afterward. Failure to post an unlock to the controller terminates the process. Windows can silently remove a slow low-level hook; no supported API reliably reports that condition from its handle. Short callbacks, a dedicated thread and the independently registered shortcut reduce this risk without promising a system-level guarantee.

`try/finally`, `Application.ThreadException` and `AppDomain.UnhandledException` handle recoverable failure paths. Hard termination does not guarantee managed handlers run; Windows releases process-owned windows and hooks. The secure desktop, UAC, protected input, elevated applications and other integrity levels cannot be fully shielded. Blackveil uses no drivers, `BlockInput`, UIAccess or persistent input configuration changes.

## Shortcut settings and English UI

Left-clicking the tray icon, or selecting Settings, opens a dialog. The global shortcut is temporarily unregistered so its current combination can be recorded. `ProcessCmdKey` handles recording only in the focused dialog; no extra global recording hook is installed. Escape can itself be recorded. Clicking the recording button again cancels capture. Default changes only the draft; Save persists it.

The controller attempts registration before saving, but since 2.0.1 it also accepts combinations Windows rejects. Invalid data structures and write failures still fail the save. An unavailable shortcut produces a warning, disables blackout and leaves the tray app accessible. Registration is retried when the dialog closes.

Windows registration and the hook share the same `Shortcut` value, including Shift and Win. A modifier-only shortcut ignores its own modifier bit during comparison. The manual picker includes virtual-key codes 8–254. Accepted registration does not guarantee reserved system or OEM combinations work. `settings.json` stores only the shortcut, never a recording of keystrokes.

`AppLanguage.Initialize` selects English UI culture before either normal app startup or update-helper dispatch. It sets the UI default for background threads without changing regional date/time culture, the keyboard layout or the schedule time zone. App-owned strings, embedded release notes and installer resources are English. Windows-owned prompts retain the Windows language. The JSON schema, registry paths and mutex names are unchanged by translation.

## Updates and installation scope

`UpdateCoordinator` schedules checks and installations independently with a next-event WinForms timer. Network operations are asynchronous. The `canInteract` predicate defers checks, prompts and installation while blackout is active; pending work is retried later. The client accepts only stable releases and exact asset URLs from this repository, bounds response/download sizes and verifies SHA256 before execution.

A self-copied helper runs before the main app mutex is acquired. It validates the cached plan, payload, target and parent process identity, waits for the parent to exit, verifies the hash again, then updates and restarts the app. Failed versions are suppressed from automatic retry; manual retry is allowed. Embedded changelog sections provide cumulative What's new content.

Installed mode is detected from the matching HKCU/HKLM uninstall registration and exact install location. Personal installations use `/CURRENTUSER`; system-wide installations use `/ALLUSERS`. Only the system installer is elevated using normal `runas` UAC. The helper and restarted tray app remain under the original account. System updates back up the EXE in user cache, never by writing to Program Files without elevation. If a system update fails, repair may require Setup; portable/personal updates restore the previous EXE on recoverable replacement failures.

Startup uses one per-user HKCU Run value. A system install does not write personal startup for an administrator account used during elevation. Uninstall removes the current account's Run entry only when it targets that installation. System uninstall preserves profile data; personal uninstall removes the current profile's Blackveil data. Switching scope is an explicit uninstall/reinstall operation.
