namespace Blackveil.Core;

public sealed record Shortcut(int Key, uint Modifiers)
{
    public static Shortcut Default { get; } = new(0x51, 6);
    public bool IsValid => (Modifiers & ~7u) == 0 && (Modifiers & 3) != 0 &&
        (Key is >= 0x30 and <= 0x39 or >= 0x41 and <= 0x5A or >= 0x70 and <= 0x87) && Key != 0x7B;
    public override string ToString() => string.Join(" + ",
        new[] { (Modifiers & 2) != 0 ? "Ctrl" : null, (Modifiers & 1) != 0 ? "Alt" : null,
            (Modifiers & 4) != 0 ? "Shift" : null,
            Key >= 0x70 ? $"F{Key - 0x6F}" : ((char)Key).ToString() }.Where(s => s is not null));
}
