namespace Blackveil.Core;

// Independent of Windows so the exact callback decision can be tested.
public sealed class KeyboardGate
{
    private readonly HashSet<int> down = [];
    private bool unlockOnRelease;
    public KeyboardGate(IEnumerable<int> initiallyDown) => down.UnionWith(initiallyDown);

    public bool Process(int key, bool isDown)
    {
        if (!isDown)
        {
            down.Remove(key);
            if (key != 0x51 || !unlockOnRelease) return false;
            unlockOnRelease = false;
            return true;
        }
        bool first = down.Add(key);
        if (first && key == 0x51 &&
            (down.Contains(0x11) || down.Contains(0xA2) || down.Contains(0xA3)) &&
            (down.Contains(0x10) || down.Contains(0xA0) || down.Contains(0xA1))) unlockOnRelease = true;
        return false;
    }
}
