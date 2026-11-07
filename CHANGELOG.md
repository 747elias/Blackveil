# Changelog

## [2.1.2] - 2026-11-07

- English throughout the app: tray menu, shortcut recording, Settings, About, update prompts, status messages, errors and What's new.
- English-only installer, including scope selection, welcome text and branded artwork. Existing personal and system-wide installations keep their scope and settings.
- Presentation-first README with a visual introduction, clear download choices and the prepare → hide → reveal → continue workflow.
- English documentation, historical changelog entries, packaging messages and repository description.
- English UI is selected independently of Windows display language; regional date/time formats and keyboard layouts are preserved.
- Updated Windows integration checks and screenshots, including migration from the previous German 2.1.1 release.

## [2.1.1] - 2026-10-27

- Setup choice between a personal installation and installation for all users, with appropriate paths and normal Windows elevation.
- Redesigned Settings, Updates and What's new: dark navigation, light cards, teal accents and clearer typography.
- Branded installer with custom artwork, welcome page and visible installation scope.
- Scope-aware updates: system-wide installations stay system-wide and use normal UAC approval for the installer. The tray app remains asInvoker.
- Settings and startup stay personal; users of a system-wide installation enable their own startup preference from the tray menu.
- Extended Windows tests for machine installation, HKLM registration, updating and uninstalling; UI screenshots available as workflow artifacts.

## [2.1.0] - 2026-10-27

- Independent switches for automatic update checks and automatic installation.
- Check at startup, every six hours, daily at a local time or every seven days; install immediately, at next startup or daily at a local time.
- Manual update checks, optional installation without asking, or confirmation before installation.
- Downloads from the official GitHub release with SHA256 verification, silent personal installation or portable EXE replacement, and automatic restart.
- What's new after an update, including changes across skipped versions; preferences and startup entries are preserved.
- Scheduled updates are deferred during blackout. Failures do not cause automatic installation loops.
- Automated tests for schedules, GUI preferences, downloads, checksums and real portable and installed updates.

## [2.0.1] - 2026-10-25

- Flexible shortcut selection, including Shift without Ctrl/Alt, single keys, Win, F12 and special keys.
- Manual key list with Ctrl/Alt/Shift/Win checkboxes for combinations intercepted by the system.
- Record a standalone modifier by pressing and releasing it; Escape can also be selected.
- Save reserved or unavailable shortcuts with a warning instead of rejecting them. If registration fails, blackout stays disabled and the tray app remains accessible for correction.
- Extended hook comparison and automated tests for Shift, Win, single-key and modifier-only shortcuts.

## [2.0.0] - 2026-10-25

- Settings window available by left-clicking the tray icon or selecting Settings in its context menu.
- Record, reset and persist a shortcut.
- Support for Ctrl/Alt/Shift combinations with letters, digits and function keys except F12.
- Check for collisions before applying a shortcut; retain the old shortcut on failure.
- Use the configured shortcut for Windows registration, input hooks, About and the tray tooltip.
- Extended Windows tests for real GUI key recording, persistence, restart and conflict handling.

## [1.0.0] - 2026-10-22

- First release for Windows 11 x64 using .NET 10 LTS and WinForms.
- Tray controls and global Ctrl + Shift + Q hotkey with MOD_NOREPEAT.
- Black monitor overlays with Per-Monitor-V2 and dynamic display changes.
- Separate low-level hooks, an independent unlock path and error cleanup.
- Touch, gesture and pen messages discarded on the overlays.
- Personal installer, shared HKCU startup entry, portable EXE and ZIP.
- Automated logic and native Windows integration tests.
- Unsigned: Windows SmartScreen may display a warning. Blackveil is not a Windows lock and does not block the secure desktop.
