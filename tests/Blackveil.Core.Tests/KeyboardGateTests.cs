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
