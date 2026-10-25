using System.Diagnostics;
using System.Runtime.InteropServices;
using Blackveil;
using Blackveil.Core;
using Shortcut = Blackveil.Core.Shortcut;

// Real user32 integration. Run only in an unlocked interactive Windows session.
internal static class Program
{
    [STAThread]
    private static int Main()
    {
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        using var receiver = new Receiver();
        InputHooks? hooks = null;
        int observedKeys = 0, observedMouse = 0;
        Native.HookProc keyObserver = (code, message, data) => { if (code >= 0) observedKeys++; return Native.CallNextHookEx(0, code, message, data); };
        Native.HookProc mouseObserver = (code, message, data) => { if (code >= 0) observedMouse++; return Native.CallNextHookEx(0, code, message, data); };
        nint observerKeyboard = 0, observerMouse = 0;
        try
        {
            Require(Native.RegisterHotKey(receiver.Handle, Native.HotkeyId, 0x4006, 0x51), "RegisterHotKey");
            Require(!Native.RegisterHotKey(receiver.Handle, Native.HotkeyId + 1, 0x4006, 0x51), "Collision must fail");
            Chord();
            PumpUntil(() => receiver.Hotkeys == 1, "Inactive hotkey via SendInput");
            for (int i = 0; i < 10; i++) Key(0x51, false);
            PumpFor(150);
            Require(receiver.Hotkeys == 1, "MOD_NOREPEAT");
            Release(); PumpFor(150);
            observerKeyboard = Native.SetWindowsHookEx(13, keyObserver, Native.GetModuleHandle(null), 0);
            observerMouse = Native.SetWindowsHookEx(14, mouseObserver, Native.GetModuleHandle(null), 0);
            Require(observerKeyboard != 0 && observerMouse != 0, "Observer hooks");
            hooks = new InputHooks(receiver.Handle);
            Chord();
            for (int i = 0; i < 20; i++) Key(0x51, false);
            PumpFor(150);
            Require(receiver.Unlocks == 0, "Unlock waits for Q release");
            Key(0x51, true);
            PumpUntil(() => receiver.Unlocks == 1, "Unlock bypasses suppressed WM_HOTKEY");
            Require(receiver.Hotkeys == 1, "Hook consumed chord before RegisterHotKey");
            Native.Input[] mouse = [new() { Type = 0, Data = new Native.InputUnion { Mouse = new Native.MouseInput { Flags = 0x2 } } },
                new() { Type = 0, Data = new Native.InputUnion { Mouse = new Native.MouseInput { Flags = 0x4 } } },
                new() { Type = 0, Data = new Native.InputUnion { Mouse = new Native.MouseInput { Flags = 0x800, Data = 120 } } }];
            Require(Native.SendInput(3, mouse, Marshal.SizeOf<Native.Input>()) == 3, "Mouse injection");
            PumpFor(100);
            Require(observedKeys == 0 && observedMouse == 0, "Product hooks must suppress downstream keyboard, clicks and wheel");
            hooks.Dispose(); hooks = null;
            Release(); PumpFor(150);
            Require(observedKeys > 0, "Downstream input resumes after unhook");
            Native.UnhookWindowsHookEx(observerKeyboard); observerKeyboard = 0;
            Native.UnhookWindowsHookEx(observerMouse); observerMouse = 0;
            Chord(); PumpUntil(() => receiver.Hotkeys == 2, "Hotkey restored after unhook");
            Release();
            using var veil = new VeilWindow(Screen.PrimaryScreen!.Bounds, () => throw new InvalidOperationException("Unexpected veil destruction"));
            veil.Show();
            PumpFor(100);
            veil.Close(); Require(!veil.IsDisposed, "Close must be rejected while active");
            veil.CloseVeil();
            Native.UnregisterHotKey(receiver.Handle, Native.HotkeyId);
            string configPath = Path.Combine(Path.GetTempPath(), "Blackveil-tests-" + Guid.NewGuid(), "settings.json");
            var store = new SettingsStore(configPath);
            using (var app = new TrayContext(store))
            {
                for (int cycle = 0; cycle < 3; cycle++)
                {
                    if (cycle == 1) RecordShortcut(app);
                    var selected = app.CurrentShortcut;
                    CustomChord(selected);
                    PumpUntil(() => Application.OpenForms.Count == Screen.AllScreens.Length, "Full app activation covers enumerated monitors");
                    Require(Application.OpenForms.Cast<Form>().All(form => form.TopMost && form.FormBorderStyle == FormBorderStyle.None), "All veils are borderless and topmost");
                    CustomRelease(selected); PumpFor(150);
                    Require(Application.OpenForms.Count == Screen.AllScreens.Length, "Initial chord release must not unlock");
                    Key(0x1B, false); Key(0x1B, true); PumpFor(100);
                    Require(Application.OpenForms.Count > 0, "Escape must not unlock");
                    CustomChord(selected); Key((ushort)selected.Key, true);
                    PumpUntil(() => Application.OpenForms.Count == 0, "Full app unlock and window cleanup");
                    Require((Native.GetAsyncKeyState(selected.Key) & 0x8000) == 0, "Cleanup releases Q for the next MOD_NOREPEAT activation");
                    CustomRelease(selected); PumpFor(100);
                }
                Require(store.Load() == app.CurrentShortcut, "Selected shortcut persists");
                var blocked = new Shortcut(0x4D, 3);
                Require(Native.RegisterHotKey(receiver.Handle, 99, blocked.Modifiers | 0x4000, (uint)blocked.Key), "Reserve conflicting shortcut");
                bool rejected = false;
                try { app.ApplyShortcut(blocked); } catch (System.ComponentModel.Win32Exception) { rejected = true; }
                finally { Native.UnregisterHotKey(receiver.Handle, 99); }
                Require(rejected && app.CurrentShortcut == store.Load() && app.CurrentShortcut.Key == 0x4B, "Collision preserves previous shortcut and settings");
                app.Stop();
            }
            using (var restarted = new TrayContext(store))
            {
                Require(restarted.CurrentShortcut == new Shortcut(0x4B, 3), "Restart restores saved shortcut");
                restarted.Stop();
            }
            Directory.Delete(Path.GetDirectoryName(configPath)!, true);
            Console.WriteLine("PASS: configurable shortcut recording, persistence, restart, collision rollback; native hotkey, collision, MOD_NOREPEAT, real LL hook unlock, unhook, protected overlay close, three full tray-app cycles.");
            return 0;
        }
        catch (Exception e) { Console.Error.WriteLine(e); return 1; }
        finally
        {
            hooks?.Dispose(); Release();
            if (observerKeyboard != 0) Native.UnhookWindowsHookEx(observerKeyboard);
            if (observerMouse != 0) Native.UnhookWindowsHookEx(observerMouse);
            Native.UnregisterHotKey(receiver.Handle, Native.HotkeyId);
            GC.KeepAlive(keyObserver); GC.KeepAlive(mouseObserver);
        }
    }
    private static IEnumerable<Control> Descendants(Control root) => root.Controls.Cast<Control>().SelectMany(child => new[] { child }.Concat(Descendants(child)));
    private static void RecordShortcut(TrayContext app)
    {
        Exception? failure = null;
        int stage = 0, ticks = 0;
        using var driver = new System.Windows.Forms.Timer { Interval = 100 };
        driver.Tick += (_, _) =>
        {
            var dialog = Application.OpenForms.OfType<SettingsWindow>().SingleOrDefault();
            try
            {
                if (++ticks > 50) throw new TimeoutException("Shortcut recording UI");
                if (dialog is null) return;
                if (stage == 2) throw new InvalidOperationException("Settings save did not close the dialog");
                if (stage == 0)
                {
                    dialog.BeginRecording();
                    CustomChord(new Shortcut(0x4B, 3));
                    stage = 1;
                }
                else
                {
                    Require(dialog.Candidate == new Shortcut(0x4B, 3), "GUI captures real Ctrl+Alt+K input");
                    CustomRelease(dialog.Candidate);
                    stage = 2;
                    Descendants(dialog).OfType<Button>().Single(b => b.Text == "Speichern").PerformClick();
                }
            }
            catch (Exception error) { failure = error; driver.Stop(); dialog?.Close(); }
        };
        driver.Start();
        app.ShowSettings();
        driver.Stop();
        if (failure is not null) throw failure;
        Require(app.CurrentShortcut == new Shortcut(0x4B, 3), "GUI applies shortcut");
    }
    private static void CustomChord(Shortcut shortcut)
    {
        if ((shortcut.Modifiers & 2) != 0) Key(0xA2, false);
        if ((shortcut.Modifiers & 1) != 0) Key(0xA4, false);
        if ((shortcut.Modifiers & 4) != 0) Key(0xA0, false);
        Key((ushort)shortcut.Key, false);
    }
    private static void CustomRelease(Shortcut shortcut)
    {
        Key((ushort)shortcut.Key, true);
        Key(0xA0, true); Key(0xA2, true); Key(0xA4, true);
    }
    private static void Chord() { Key(0xA2, false); Key(0xA0, false); Key(0x51, false); }
    private static void Release() { Key(0x51, true); Key(0xA0, true); Key(0xA2, true); }
    private static void Key(ushort key, bool up)
    {
        Native.Input[] input = [new() { Type = 1, Data = new Native.InputUnion { Keyboard = new Native.KeyboardInput { Key = key, Flags = up ? 2u : 0u } } }];
        Require(Native.SendInput(1, input, Marshal.SizeOf<Native.Input>()) == 1, "SendInput (interactive desktop required)");
    }
    private static void PumpFor(int milliseconds)
    {
        var clock = Stopwatch.StartNew();
        while (clock.ElapsedMilliseconds < milliseconds) { Application.DoEvents(); Thread.Sleep(5); }
    }
    private static void PumpUntil(Func<bool> condition, string name)
    {
        var clock = Stopwatch.StartNew();
        while (!condition() && clock.ElapsedMilliseconds < 5000) PumpFor(10);
        Require(condition(), name);
    }
    private static void Require(bool condition, string name) { if (!condition) throw new InvalidOperationException(name); }
    private sealed class Receiver : NativeWindow, IDisposable
    {
        internal int Hotkeys, Unlocks;
        internal Receiver() => CreateHandle(new CreateParams { Caption = "Blackveil native integration tests" });
        protected override void WndProc(ref Message m)
        {
            if (m.Msg == 0x312) Hotkeys++;
            if (m.Msg == Native.ToggleMessage) Unlocks++;
            base.WndProc(ref m);
        }
        public void Dispose() => DestroyHandle();
    }
}
