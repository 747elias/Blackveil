using System.ComponentModel;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using Blackveil.Core;
using Shortcut = Blackveil.Core.Shortcut;

namespace Blackveil;

internal sealed class TrayContext : ApplicationContext
{
    private const int DisplayMessage = 0x8004, SafeStopMessage = 0x8005;
    private readonly MessageWindow messages;
    private readonly NotifyIcon tray;
    private readonly ContextMenuStrip menu;
    private readonly Icon icon;
    private readonly Native.WinEventProc desktopChanged;
    private nint desktopHook;
    private InputHooks? hooks;
    private List<VeilWindow> windows = [];
    private volatile bool active;
    private bool stopping, cursorHidden, disposed;
    private System.Threading.Timer? watchdog;
    private readonly System.Windows.Forms.Timer heartbeat = new() { Interval = 1000 };
    private long lastHeartbeat;
    private readonly SettingsStore settings;
    private Shortcut shortcut;
    private int hotkeyId = Native.HotkeyId;
    private SettingsWindow? settingsWindow;
    private bool hotkeySuspended;
    private UpdateCoordinator? updates;
    private Form? changesWindow;
    internal void StartUpdates()
    {
        if (updates is not null) return;
        updates = new UpdateCoordinator(() => !active && !stopping, () => ExitThread());
        changesWindow = updates.ShowChanges();
        _ = updates.StartAsync();
    }
    private bool hotkeyAvailable;
    private ToolStripItem? activationItem;
    internal bool CanActivate => hotkeyAvailable;
    internal string? ShortcutWarning => hotkeyAvailable ? null : "Windows konnte den Shortcut nicht registrieren. Er bleibt gespeichert, funktioniert möglicherweise nicht. Schwarzbild bleibt bis zu einer erfolgreichen Registrierung deaktiviert.";
    private void UpdateShortcutStatus()
    {
        if (activationItem is not null) activationItem.Enabled = hotkeyAvailable;
        tray.Text = hotkeyAvailable ? $"Blackveil – {shortcut}" : "Blackveil – Shortcut nicht verfügbar; Einstellungen öffnen";
    }
    internal Shortcut CurrentShortcut => shortcut;
    internal TrayContext(SettingsStore? settings = null)
    {
        this.settings = settings ?? new SettingsStore();
        shortcut = this.settings.Load();
        messages = new MessageWindow(this);
        hotkeyAvailable = Native.RegisterHotKey(messages.Handle, hotkeyId, 0x4000 | shortcut.EffectiveModifiers, (uint)shortcut.Key);
        icon = Icon.ExtractAssociatedIcon(Environment.ProcessPath!) ?? (Icon)SystemIcons.Application.Clone();
        menu = new ContextMenuStrip();
        activationItem = menu.Items.Add("Schwarzbild aktivieren", null, (_, _) => Activate());
        var startup = new ToolStripMenuItem("Mit Windows starten") { CheckOnClick = false };
        startup.Click += (_, _) =>
        {
            try { Startup.Enabled = !Startup.Enabled; startup.Checked = Startup.Enabled; }
            catch (Exception error) { Report("Autostart konnte nicht geändert werden.", error); }
        };
        menu.Items.Add(startup);
        menu.Opening += (_, _) =>
        {
            try { startup.Checked = Startup.Enabled; }
            catch (Exception error) { Report("Autostart konnte nicht gelesen werden.", error); }
        };
        menu.Items.Add("Einstellungen …", null, (_, _) => ShowSettings());
        menu.Items.Add("Info", null, (_, _) => MessageBox.Show($"Blackveil {Assembly.GetExecutingAssembly().GetName().Version?.ToString(3)}\n\n{shortcut}: Schwarzbild ein/aus\n\nVorübergehender Sicht- und Eingabeschutz. Keine Windows-Sperre.\nCtrl + Alt + Entf und Sicherheitsdesktop bleiben verfügbar.", "Info – Blackveil", MessageBoxButtons.OK, MessageBoxIcon.Information));
        menu.Items.Add("Beenden", null, (_, _) => ExitThread());
        tray = new NotifyIcon { Icon = icon, Text = $"Blackveil – {shortcut}", ContextMenuStrip = menu, Visible = true };
        UpdateShortcutStatus();
        tray.MouseClick += (_, e) => { if (e.Button == MouseButtons.Left) ShowSettings(); };
        desktopChanged = (_, _, _, _, _, _, _) => Native.PostMessage(messages.Handle, SafeStopMessage, 0, 0);
        desktopHook = Native.SetWinEventHook(0x20, 0x20, 0, desktopChanged, 0, 0, 0);
        if (desktopHook == 0) { Stop(); throw new InvalidOperationException("Desktop-Wechsel können nicht überwacht werden."); }
        SystemEvents.DisplaySettingsChanged += DisplayChanged;
        SystemEvents.PowerModeChanged += PowerChanged;
        SystemEvents.SessionSwitch += SessionChanged;
        heartbeat.Tick += (_, _) =>
        {
            Interlocked.Exchange(ref lastHeartbeat, Stopwatch.GetTimestamp());
            if (active && hooks?.IsAlive != true) Deactivate();
        };
    }

    internal void ApplyShortcut(Shortcut next)
    {
        if (active) throw new InvalidOperationException("Shortcut kann nur bei ausgeschaltetem Schwarzbild geändert werden.");
        if (!next.IsValid) throw new ArgumentException("Ungültiger Shortcut.");
        if (next == shortcut) return;
        int nextId = hotkeyId == Native.HotkeyId ? Native.HotkeyId + 1 : Native.HotkeyId;
        bool registered = Native.RegisterHotKey(messages.Handle, nextId, next.EffectiveModifiers | 0x4000, (uint)next.Key);
        try { settings.Save(next); }
        catch { if (registered) Native.UnregisterHotKey(messages.Handle, nextId); throw; }
        if (!hotkeySuspended) Native.UnregisterHotKey(messages.Handle, hotkeyId);
        else if (registered) Native.UnregisterHotKey(messages.Handle, nextId);
        hotkeyId = nextId; shortcut = next; hotkeyAvailable = registered;
        UpdateShortcutStatus();
    }

    internal void ShowSettings()
    {
        if (active || stopping) return;
        if (settingsWindow is not null) { settingsWindow.Activate(); return; }
        Native.UnregisterHotKey(messages.Handle, hotkeyId);
        hotkeySuspended = true;
        try
        {
            using var dialog = new SettingsWindow(shortcut, ApplyShortcut, () => ShortcutWarning, updates);
            settingsWindow = dialog;
            dialog.ShowDialog();
        }
        finally
        {
            settingsWindow = null; hotkeySuspended = false;
            if (!stopping)
            {
                hotkeyAvailable = Native.RegisterHotKey(messages.Handle, hotkeyId, shortcut.EffectiveModifiers | 0x4000, (uint)shortcut.Key);
                UpdateShortcutStatus();
            }
        }
    }

    private void Activate()
    {
        if (active || stopping || settingsWindow is not null || updates?.Busy == true || changesWindow is { IsDisposed: false } || !hotkeyAvailable) return;
        try
        {
            // All partial resources belong to Deactivate, including failed form creation.
            hooks = new InputHooks(messages.Handle, shortcut);
            Cursor.Hide(); cursorHidden = true;
            active = true;
            RebuildWindows();
            Interlocked.Exchange(ref lastHeartbeat, Stopwatch.GetTimestamp());
            heartbeat.Start();
            // Active only. A hung UI must not retain an input barrier indefinitely.
            watchdog = new System.Threading.Timer(_ =>
            {
                if (active && Stopwatch.GetElapsedTime(Interlocked.Read(ref lastHeartbeat)) > TimeSpan.FromSeconds(10))
                { Log.Write("UI unresponsive; terminating to release Windows-owned resources"); Environment.Exit(5); }
            }, null, 2000, 2000);
        }
        catch (Exception error) { Deactivate(); Report("Schwarzbild konnte nicht sicher aktiviert werden.", error); }
    }

    private void RebuildWindows()
    {
        if (!active) return;
        List<VeilWindow> old = windows;
        List<VeilWindow> next = [];
        windows = next;
        try
        {
            foreach (Screen screen in Screen.AllScreens)
            {
                var window = new VeilWindow(screen.Bounds, () => Native.PostMessage(messages.Handle, SafeStopMessage, 0, 0));
                next.Add(window);
                window.Show();
                window.Bounds = screen.Bounds;
                window.BringToFront();
            }
            if (next.Count == 0) throw new InvalidOperationException("Kein Monitor verfügbar.");
        }
        catch { Deactivate(); throw; }
        finally { foreach (var window in old) window.CloseVeil(); }
    }

    internal void Deactivate()
    {
        bool hadHooks = hooks is not null;
        ushort[] initiallyHeld = hooks?.InitiallyHeldKeys ?? [];
        int[] initiallyHeldButtons = hooks?.InitiallyHeldButtons ?? [];
        active = false;
        watchdog?.Dispose(); watchdog = null;
        heartbeat.Stop();
        try { hooks?.Dispose(); }
        finally
        {
            hooks = null;
            try { foreach (var window in windows) window.CloseVeil(); }
            finally
            {
                windows.Clear();
                if (cursorHidden) { Cursor.Show(); cursorHidden = false; }
                if (hadHooks)
                {
                    Native.ReleaseActivationKeys(initiallyHeld, shortcut);
                    Native.ReleaseActivationButtons(initiallyHeldButtons);
                }
            }
        }
    }

    internal void Stop()
    {
        if (stopping) return;
        stopping = true;
        updates?.Dispose();
        changesWindow?.Close();
        Deactivate();
        SystemEvents.DisplaySettingsChanged -= DisplayChanged;
        SystemEvents.PowerModeChanged -= PowerChanged;
        SystemEvents.SessionSwitch -= SessionChanged;
        if (desktopHook != 0) { Native.UnhookWinEvent(desktopHook); desktopHook = 0; }
        Native.UnregisterHotKey(messages.Handle, hotkeyId);
        settingsWindow?.Close();
        tray.Visible = false;
    }

    private void DisplayChanged(object? sender, EventArgs e) => Native.PostMessage(messages.Handle, DisplayMessage, 0, 0);
    private void PowerChanged(object sender, PowerModeChangedEventArgs e) => Native.PostMessage(messages.Handle, SafeStopMessage, 0, 0);
    private void SessionChanged(object sender, SessionSwitchEventArgs e) => Native.PostMessage(messages.Handle, SafeStopMessage, 0, 0);
    private static void Report(string text, Exception error)
    { Log.Write(text, error); MessageBox.Show($"{text}\n\n{error.Message}", "Blackveil", MessageBoxButtons.OK, MessageBoxIcon.Error); }
    protected override void ExitThreadCore() { Stop(); base.ExitThreadCore(); }
    protected override void Dispose(bool disposing)
    {
        if (disposing && !disposed)
        {
            Stop(); disposed = true;
            heartbeat.Dispose(); tray.Dispose(); menu.Dispose(); icon.Dispose(); messages.DestroyHandle();
        }
        base.Dispose(disposing);
    }

    private sealed class MessageWindow : NativeWindow
    {
        private readonly TrayContext context;
        internal MessageWindow(TrayContext context)
        { this.context = context; CreateHandle(new CreateParams { Caption = "Blackveil.Controller" }); }
        protected override void WndProc(ref Message m)
        {
            try
            {
                if (m.Msg == 0x312 && (int)m.WParam == context.hotkeyId)
                { if (context.active) context.Deactivate(); else context.Activate(); return; }
                if (m.Msg == Native.ToggleMessage) { if (context.active) context.Deactivate(); return; }
                if (m.Msg is SafeStopMessage or Native.FailureMessage) { context.Deactivate(); return; }
                if (m.Msg == DisplayMessage) { context.RebuildWindows(); return; }
                if (m.Msg == 0x11) { context.Deactivate(); m.Result = 1; return; }
                if (m.Msg == 0x16) { context.ExitThread(); return; }
            }
            catch (Exception error) { context.Deactivate(); Report("Blackveil hat den Schwarzbild-Modus aus Sicherheitsgründen beendet.", error); }
            base.WndProc(ref m);
        }
    }
}
