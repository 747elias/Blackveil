using System.Runtime.InteropServices;

namespace Blackveil;

internal static class Native
{
    [StructLayout(LayoutKind.Sequential)] internal struct Input { internal uint Type; internal InputUnion Data; }
    [StructLayout(LayoutKind.Explicit)] internal struct InputUnion
    {
        [FieldOffset(0)] internal KeyboardInput Keyboard;
        [FieldOffset(0)] internal MouseInput Mouse;
    }
    [StructLayout(LayoutKind.Sequential)] internal struct KeyboardInput { internal ushort Key, Scan; internal uint Flags, Time; internal nuint Extra; }
    [StructLayout(LayoutKind.Sequential)] internal struct MouseInput { internal int X, Y; internal uint Data, Flags, Time; internal nuint Extra; }
    [DllImport("user32.dll", SetLastError = true)] internal static extern uint SendInput(uint count, Input[] inputs, int size);
    internal static void ReleaseActivationKeys(IEnumerable<ushort> initiallyHeld, Blackveil.Core.Shortcut shortcut)
    {
        Input[] releases = initiallyHeld.Concat(new ushort[] { 0xA0, 0xA1, 0xA2, 0xA3, 0xA4, 0xA5, (ushort)shortcut.Key }).Distinct()
            .Where(key => key is not (0x10 or 0x11 or 0x12)).Select(key => new Input
        { Type = 1, Data = new InputUnion { Keyboard = new KeyboardInput { Key = key,
            Flags = 2u | (key is 0xA3 or 0xA5 or 0x21 or 0x22 or 0x23 or 0x24 or 0x25 or 0x26 or 0x27 or 0x28 or 0x2C or 0x2D or 0x2E or 0x5B or 0x5C or 0x5D or 0x6F or 0x90 ? 1u : 0u) } } }).ToArray();
        if (SendInput((uint)releases.Length, releases, Marshal.SizeOf<Input>()) != releases.Length)
            Log.Write("Key-state cleanup was restricted by Windows (possibly UIPI).");
    }
    internal static void ReleaseActivationButtons(IEnumerable<int> initiallyHeld)
    {
        Input[] releases = initiallyHeld.Select(button => new Input { Type = 0,
            Data = new InputUnion { Mouse = new MouseInput {
                Flags = button switch { 1 => 0x4u, 2 => 0x10u, 4 => 0x40u, _ => 0x100u },
                Data = button switch { 5 => 1u, 6 => 2u, _ => 0u } } } }).ToArray();
        if (releases.Length > 0 && SendInput((uint)releases.Length, releases, Marshal.SizeOf<Input>()) != releases.Length)
            Log.Write("Mouse-state cleanup was restricted by Windows (possibly UIPI).");
    }
    internal const int HotkeyId = 0x424C;
    internal const int ToggleMessage = 0x8001, FailureMessage = 0x8002;
    internal delegate nint HookProc(int code, nuint wParam, nint lParam);
    internal delegate void WinEventProc(nint hook, uint eventType, nint hwnd, int objectId, int childId, uint thread, uint time);
    [DllImport("user32.dll", SetLastError = true)] internal static extern bool RegisterHotKey(nint window, int id, uint modifiers, uint key);
    [DllImport("user32.dll", SetLastError = true)] internal static extern bool UnregisterHotKey(nint window, int id);
    [DllImport("user32.dll", SetLastError = true)] internal static extern nint SetWindowsHookEx(int id, HookProc callback, nint module, uint thread);
    [DllImport("user32.dll", SetLastError = true)] internal static extern bool UnhookWindowsHookEx(nint hook);
    [DllImport("user32.dll")] internal static extern nint CallNextHookEx(nint hook, int code, nuint wParam, nint lParam);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] internal static extern nint GetModuleHandle(string? name);
    [DllImport("user32.dll")] internal static extern short GetAsyncKeyState(int key);
    [DllImport("user32.dll", SetLastError = true)] internal static extern bool PostMessage(nint window, int message, nint wParam, nint lParam);
    [DllImport("user32.dll")] internal static extern nint SetCursor(nint cursor);
    [DllImport("user32.dll", SetLastError = true)] internal static extern bool RegisterTouchWindow(nint hwnd, uint flags);
    [DllImport("user32.dll")] internal static extern bool CloseTouchInputHandle(nint handle);
    [DllImport("user32.dll")] internal static extern bool CloseGestureInfoHandle(nint handle);
    [DllImport("user32.dll")] internal static extern nint SetWinEventHook(uint min, uint max, nint module, WinEventProc callback, uint process, uint thread, uint flags);
    [DllImport("user32.dll")] internal static extern bool UnhookWinEvent(nint hook);
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)] internal static extern uint RegisterWindowMessage(string message);
}
