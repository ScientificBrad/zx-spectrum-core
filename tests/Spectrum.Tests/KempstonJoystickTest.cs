using Spectrum.Core;
using Xunit;

namespace Spectrum.Tests;

public class KempstonJoystickTest
{
    [Fact]
    public void TestKempstonPort1F()
    {
        var bus = new SpectrumBus();
        bus.Reset();

        // When idle, Kempston returns 0x00 (not 0xFF)
        Assert.Equal(0x00, bus.In(0x1F));

        // Test individual directions
        bus.KempstonJoystick = 0x01; // Right
        Assert.Equal(0x01, bus.In(0x1F));

        bus.KempstonJoystick = 0x02; // Left
        Assert.Equal(0x02, bus.In(0x1F));

        bus.KempstonJoystick = 0x04; // Down
        Assert.Equal(0x04, bus.In(0x1F));

        bus.KempstonJoystick = 0x08; // Up
        Assert.Equal(0x08, bus.In(0x1F));

        bus.KempstonJoystick = 0x10; // Fire
        Assert.Equal(0x10, bus.In(0x1F));

        // Combined directions
        bus.KempstonJoystick = 0x13; // Fire + Left + Right
        Assert.Equal(0x13, bus.In(0x1F));

        // Reset clears joystick
        bus.Reset();
        Assert.Equal(0x00, bus.In(0x1F));
    }
}
