using System.Diagnostics;
using System.Runtime.InteropServices;
using Blackveil;

// Real user32 integration. Run only in an unlocked interactive Windows session.
internal static class Program
{
    [STAThread]
    private static int Main()
    {
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        using var receiver = new Receiver();
        InputHooks? hooks = null;
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
            hooks = new InputHooks(receiver.Handle);
            Chord();
            for (int i = 0; i < 20; i++) Key(0x51, false);
            PumpFor(150);
            Require(receiver.Unlocks == 0, "Unlock waits for Q release");
            Key(0x51, true);
            PumpUntil(() => receiver.Unlocks == 1, "Unlock bypasses suppressed WM_HOTKEY");
            Require(receiver.Hotkeys == 1, "Hook consumed chord before RegisterHotKey");
            hooks.Dispose(); hooks = null;
            Release(); PumpFor(150);
            Chord(); PumpUntil(() => receiver.Hotkeys == 2, "Hotkey restored after unhook");
            Release();
            using var veil = new VeilWindow(Screen.PrimaryScreen!.Bounds, () => throw new InvalidOperationException("Unexpected veil destruction"));
            veil.Show();
            PumpFor(100);
            veil.Close(); Require(!veil.IsDisposed, "Close must be rejected while active");
            veil.CloseVeil();
            Console.WriteLine("PASS: native hotkey, collision, MOD_NOREPEAT, real LL hook unlock, unhook, protected overlay close.");
            return 0;
        }
        catch (Exception e) { Console.Error.WriteLine(e); return 1; }
        finally
        {
            hooks?.Dispose(); Release();
            Native.UnregisterHotKey(receiver.Handle, Native.HotkeyId);
        }
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
