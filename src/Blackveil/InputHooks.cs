using System.ComponentModel;
using System.Runtime.InteropServices;
using Blackveil.Core;

namespace Blackveil;

internal sealed class InputHooks : IDisposable
{
    private readonly Thread thread;
    private readonly ManualResetEventSlim ready = new();
    private readonly nint owner;
    private Exception? startupError;
    private nint keyboard, mouse, window;
    private KeyboardGate? gate;
    internal ushort[] InitiallyHeldKeys { get; private set; } = [];
    internal int[] InitiallyHeldButtons { get; private set; } = [];
    private readonly Native.HookProc keyboardProc, mouseProc;
    private int disposed;
    internal bool IsAlive => thread.IsAlive;

    internal InputHooks(nint owner)
    {
        this.owner = owner;
        keyboardProc = KeyboardCallback;
        mouseProc = MouseCallback;
        thread = new Thread(Run) { IsBackground = true, Name = "Blackveil input hooks" };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        if (!ready.Wait(TimeSpan.FromSeconds(5)))
        {
            // A stalled hook owner cannot safely be retained.
            Environment.Exit(2);
            throw new TimeoutException("Eingabeschutz konnte nicht gestartet werden.");
        }
        if (startupError is not null) { Dispose(); throw new InvalidOperationException("Eingabeschutz konnte nicht gestartet werden.", startupError); }
    }

    private void Run()
    {
        HookWindow? pump = null;
        try
        {
            pump = new HookWindow();
            window = pump.Handle;
            InitiallyHeldKeys = Enumerable.Range(8, 247).Where(key => (Native.GetAsyncKeyState(key) & 0x8000) != 0).Select(key => (ushort)key).ToArray();
            InitiallyHeldButtons = new[] { 1, 2, 4, 5, 6 }.Where(key => (Native.GetAsyncKeyState(key) & 0x8000) != 0).ToArray();
            // Generic modifier aliases are not reported by the LL hook; retain only sided variants.
            gate = new KeyboardGate(InitiallyHeldKeys.Where(key => key is not (0x10 or 0x11 or 0x12)).Select(key => (int)key));
            keyboard = Native.SetWindowsHookEx(13, keyboardProc, Native.GetModuleHandle(null), 0);
            if (keyboard == 0) throw new Win32Exception(Marshal.GetLastWin32Error());
            mouse = Native.SetWindowsHookEx(14, mouseProc, Native.GetModuleHandle(null), 0);
            if (mouse == 0) throw new Win32Exception(Marshal.GetLastWin32Error());
            ready.Set();
            Application.Run();
        }
        catch (Exception error)
        {
            startupError = error;
            Log.Write("Hook thread failed", error);
            Native.PostMessage(owner, Native.FailureMessage, 0, 0);
        }
        finally
        {
            RemoveHooks();
            pump?.DestroyHandle();
            ready.Set();
        }
    }

    private nint KeyboardCallback(int code, nuint message, nint data)
    {
        if (code < 0) return Native.CallNextHookEx(0, code, message, data);
        try
        {
            bool down = message is 0x100 or 0x104;
            bool up = message is 0x101 or 0x105;
            if ((down || up) && gate!.Process(Marshal.ReadInt32(data), down))
                if (!Native.PostMessage(owner, Native.ToggleMessage, 0, 0)) Environment.Exit(3);
            return 1; // Includes Ctrl/Shift/Q: the hook posts the unlock itself.
        }
        catch
        {
            if (!Native.PostMessage(owner, Native.FailureMessage, 0, 0)) Environment.Exit(3);
            return 1;
        }
    }

    private static nint MouseCallback(int code, nuint message, nint data) =>
        code < 0 ? Native.CallNextHookEx(0, code, message, data) : 1;

    private void RemoveHooks()
    {
        if (keyboard != 0) { if (!Native.UnhookWindowsHookEx(keyboard)) Environment.Exit(6); keyboard = 0; }
        if (mouse != 0) { if (!Native.UnhookWindowsHookEx(mouse)) Environment.Exit(6); mouse = 0; }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0) return;
        if (window != 0) Native.PostMessage(window, 0x8003, 0, 0);
        if (!thread.Join(TimeSpan.FromSeconds(3))) Environment.Exit(4);
        ready.Dispose();
    }

    private sealed class HookWindow : NativeWindow
    {
        internal HookWindow() => CreateHandle(new CreateParams { Caption = "Blackveil.Hooks", Parent = new nint(-3) });
        protected override void WndProc(ref Message m)
        {
            if (m.Msg == 0x8003) { Application.ExitThread(); return; }
            base.WndProc(ref m);
        }
    }
}
