namespace Blackveil.Core;

// Independent of Windows so the exact callback decision can be tested.
public sealed class KeyboardGate
{
    private readonly HashSet<int> down = [];
    private bool unlockOnRelease;
    private readonly Shortcut shortcut;
    public KeyboardGate(IEnumerable<int> initiallyDown, Shortcut? shortcut = null)
    {
        this.shortcut = shortcut ?? Shortcut.Default;
        if (!this.shortcut.IsValid) throw new ArgumentException("Ungültiger Shortcut.", nameof(shortcut));
        down.UnionWith(initiallyDown);
    }
    private bool Held(int generic, int left, int right) => down.Contains(generic) || down.Contains(left) || down.Contains(right);

    private bool MatchesKey(int key) => shortcut.Key is 0x10 or 0x11 or 0x12
        ? Shortcut.NormalizeKey(key) == shortcut.Key : key == shortcut.Key;

    public bool Process(int key, bool isDown)
    {
        if (!isDown)
        {
            down.Remove(key);
            if (!MatchesKey(key) || !unlockOnRelease) return false;
            unlockOnRelease = false;
            return true;
        }
        bool first = down.Add(key);
        uint modifiers = (Held(0x11, 0xA2, 0xA3) ? 2u : 0u) |
            (Held(0x10, 0xA0, 0xA1) ? 4u : 0u) | (Held(0x12, 0xA4, 0xA5) ? 1u : 0u) | (down.Contains(0x5B) || down.Contains(0x5C) ? 8u : 0u);
        if (first && MatchesKey(key) && (modifiers & ~Shortcut.ModifierBit(shortcut.Key)) == shortcut.EffectiveModifiers) unlockOnRelease = true;
        return false;
    }
}
