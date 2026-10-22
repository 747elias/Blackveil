using System.ComponentModel;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.Win32;

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
    internal TrayContext()
    {
        messages = new MessageWindow(this);
        if (!Native.RegisterHotKey(messages.Handle, Native.HotkeyId, 0x4000 | 0x2 | 0x4, 0x51))
        {
            int code = Marshal.GetLastWin32Error();
            messages.DestroyHandle();
            throw new Win32Exception(code, "Ctrl + Shift + Q konnte nicht registriert werden. Der Hotkey ist möglicherweise bereits durch eine andere Anwendung belegt. Beende diese Anwendung und starte Blackveil erneut.");
        }
        icon = Icon.ExtractAssociatedIcon(Environment.ProcessPath!) ?? (Icon)SystemIcons.Application.Clone();
        menu = new ContextMenuStrip();
        menu.Items.Add("Schwarzbild aktivieren", null, (_, _) => Activate());
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
        menu.Items.Add("Info", null, (_, _) => MessageBox.Show($"Blackveil {Assembly.GetExecutingAssembly().GetName().Version?.ToString(3)}\n\nCtrl + Shift + Q: Schwarzbild ein/aus\n\nVorübergehender Sicht- und Eingabeschutz. Keine Windows-Sperre.\nCtrl + Alt + Entf und Sicherheitsdesktop bleiben verfügbar.", "Info – Blackveil", MessageBoxButtons.OK, MessageBoxIcon.Information));
        menu.Items.Add("Beenden", null, (_, _) => ExitThread());
        tray = new NotifyIcon { Icon = icon, Text = "Blackveil – Ctrl + Shift + Q", ContextMenuStrip = menu, Visible = true };
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

    private void Activate()
    {
        if (active || stopping) return;
        try
        {
            // All partial resources belong to Deactivate, including failed form creation.
            hooks = new InputHooks(messages.Handle);
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
                    Native.ReleaseActivationKeys(initiallyHeld);
                    Native.ReleaseActivationButtons(initiallyHeldButtons);
                }
            }
        }
    }

    internal void Stop()
    {
        if (stopping) return;
        stopping = true;
        Deactivate();
        SystemEvents.DisplaySettingsChanged -= DisplayChanged;
        SystemEvents.PowerModeChanged -= PowerChanged;
        SystemEvents.SessionSwitch -= SessionChanged;
        if (desktopHook != 0) { Native.UnhookWinEvent(desktopHook); desktopHook = 0; }
        Native.UnregisterHotKey(messages.Handle, Native.HotkeyId);
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
                if (m.Msg == 0x312 && (int)m.WParam == Native.HotkeyId)
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
