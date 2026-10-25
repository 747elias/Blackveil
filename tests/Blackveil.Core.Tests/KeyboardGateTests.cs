using Blackveil.Core;
using Xunit;

namespace Blackveil.Core.Tests;

public sealed class KeyboardGateTests
{
    [Theory]
    [InlineData(0xA2, 0xA0)]
    [InlineData(0xA3, 0xA1)]
    [InlineData(0xA2, 0xA1)]
    [InlineData(0xA3, 0xA0)]
    public void UnlocksOnceOnReleaseDespiteAutoRepeat(int control, int shift)
    {
        var gate = new KeyboardGate([]);
        Assert.False(gate.Process(control, true));
        Assert.False(gate.Process(shift, true));
        for (int i = 0; i < 100; i++) Assert.False(gate.Process(0x51, true));
        Assert.True(gate.Process(0x51, false));
        Assert.False(gate.Process(0x51, false));
    }
    [Fact]
    public void InitialActivationChordAndItsRepeatsCannotUnlock()
    {
        var gate = new KeyboardGate([0xA2, 0xA0, 0x51]);
        for (int i = 0; i < 100; i++) Assert.False(gate.Process(0x51, true));
        Assert.False(gate.Process(0x51, false));
        Assert.False(gate.Process(0x51, true));
        Assert.True(gate.Process(0x51, false));
    }
    [Fact]
    public void ReleasingModifiersFirstStillUnlocks()
    {
        var gate = new KeyboardGate([0xA2, 0xA0]);
        Assert.False(gate.Process(0x51, true));
        Assert.False(gate.Process(0xA2, false));
        Assert.False(gate.Process(0xA0, false));
        Assert.True(gate.Process(0x51, false));
    }
    [Fact]
    public void OtherKeysAndIncompleteChordsDoNotUnlock()
    {
        var gate = new KeyboardGate([]);
        foreach (int key in Enumerable.Range(1, 255)) { Assert.False(gate.Process(key, true)); Assert.False(gate.Process(key, false)); }
        gate.Process(0xA2, true);
        Assert.False(gate.Process(0x51, true)); Assert.False(gate.Process(0x51, false));
        gate.Process(0xA2, false); gate.Process(0xA0, true);
        Assert.False(gate.Process(0x51, true)); Assert.False(gate.Process(0x51, false));
    }
    [Fact]
    public void AddingModifiersToAlreadyHeldQDoesNotUnlock()
    {
        var gate = new KeyboardGate([]);
        gate.Process(0x51, true); gate.Process(0xA2, true); gate.Process(0xA0, true);
        Assert.False(gate.Process(0x51, true)); Assert.False(gate.Process(0x51, false));
    }
}

public sealed class ConfigurableShortcutTests
{
    [Theory]
    [InlineData(0x4B, 1u, 0xA4, 0)]
    [InlineData(0x78, 3u, 0xA3, 0xA5)]
    [InlineData(0x39, 6u, 0xA2, 0xA1)]
    public void CustomChordUnlocksOnlyOnce(int key, uint modifiers, int first, int second)
    {
        var gate = new KeyboardGate([], new Shortcut(key, modifiers));
        gate.Process(first, true);
        if (second != 0) gate.Process(second, true);
        for (int i = 0; i < 20; i++) Assert.False(gate.Process(key, true));
        Assert.True(gate.Process(key, false));
        Assert.False(gate.Process(key, false));
    }
    [Fact]
    public void ExtraModifiersAndOldShortcutDoNotUnlock()
    {
        var gate = new KeyboardGate([0xA2, 0xA0], new Shortcut(0x4B, 2));
        gate.Process(0x4B, true); Assert.False(gate.Process(0x4B, false));
        gate.Process(0x51, true); Assert.False(gate.Process(0x51, false));
        gate.Process(0xA0, false); gate.Process(0x4B, true); Assert.True(gate.Process(0x4B, false));
    }
    [Theory]
    [InlineData(0x51, 0u)]
    [InlineData(0x51, 4u)]
    [InlineData(0x7B, 2u)]
    [InlineData(0xA2, 2u)]
    [InlineData(0x51, 8u)]
    [InlineData(0x10, 0u)]
    [InlineData(0x1B, 0u)]
    public void AllStructurallyValidKeyboardShortcutsAreAllowed(int key, uint modifiers) => Assert.True(new Shortcut(key, modifiers).IsValid);
    [Theory]
    [InlineData(0, 0u)]
    [InlineData(255, 0u)]
    [InlineData(0x51, 16u)]
    public void InvalidDataIsRejected(int key, uint modifiers) => Assert.False(new Shortcut(key, modifiers).IsValid);
    [Theory]
    [InlineData(0x41, 4u, 0xA0)]
    [InlineData(0x7B, 0u, 0)]
    [InlineData(0x41, 8u, 0x5B)]
    [InlineData(0x10, 0u, 0)]
    public void RelaxedShortcutsUnlockOnRelease(int key, uint modifiers, int modifierKey)
    {
        var gate = new KeyboardGate([], new Shortcut(key, modifiers));
        if (modifierKey != 0) gate.Process(modifierKey, true);
        int physicalKey = key == 0x10 ? 0xA1 : key;
        Assert.False(gate.Process(physicalKey, true));
        Assert.False(gate.Process(physicalKey, true));
        Assert.True(gate.Process(physicalKey, false));
        Assert.False(gate.Process(physicalKey, false));
    }
}
