namespace Blackveil.Core;

public sealed record Shortcut(int Key, uint Modifiers)
{
    public static Shortcut Default { get; } = new(0x51, 6);
    // Structural validation only. Availability is decided by Windows, not a whitelist.
    public bool IsValid => Key is >= 8 and <= 254 && (Modifiers & ~15u) == 0;
    public static int NormalizeKey(int key) => key switch { 0xA0 or 0xA1 => 0x10, 0xA2 or 0xA3 => 0x11, 0xA4 or 0xA5 => 0x12, _ => key };
    public static uint ModifierBit(int key) => NormalizeKey(key) switch { 0x10 => 4u, 0x11 => 2u, 0x12 => 1u, 0x5B or 0x5C => 8u, _ => 0u };
    public uint EffectiveModifiers => Modifiers & ~ModifierBit(Key);
    public static string KeyName(int key) => key switch
    {
        >= 0x30 and <= 0x39 or >= 0x41 and <= 0x5A => ((char)key).ToString(),
        >= 0x70 and <= 0x87 => $"F{key - 0x6F}",
        8 => "Backspace", 9 => "Tab", 13 => "Enter", 16 => "Shift", 17 => "Ctrl", 18 => "Alt",
        27 => "Escape", 32 => "Space", 33 => "PageUp", 34 => "PageDown", 35 => "End", 36 => "Home",
        37 => "Left", 38 => "Up", 39 => "Right", 40 => "Down", 45 => "Insert", 46 => "Delete",
        0x5B => "Win (links)", 0x5C => "Win (rechts)",
        0xA0 => "Shift (links)", 0xA1 => "Shift (rechts)", 0xA2 => "Ctrl (links)", 0xA3 => "Ctrl (rechts)",
        0xA4 => "Alt (links)", 0xA5 => "Alt (rechts)",
        _ => $"VK 0x{key:X2}"
    };
    public override string ToString() => string.Join(" + ", new[] {
        (EffectiveModifiers & 2) != 0 ? "Ctrl" : null, (EffectiveModifiers & 1) != 0 ? "Alt" : null,
        (EffectiveModifiers & 4) != 0 ? "Shift" : null, (EffectiveModifiers & 8) != 0 ? "Win" : null,
        KeyName(Key) }.Where(s => s is not null));
}
