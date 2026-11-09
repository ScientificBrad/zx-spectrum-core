using Spectrum.Core;
using Xunit;

namespace Spectrum.Tests;

public class MemoryAndUlaTests
{
    [Fact]
    public void Memory_RomIsWriteProtected()
    {
        var bus = new SpectrumBus();
        // Write to ROM region 0x0000 - 0x3FFF
        bus.Write(0x1000, 0xAA);
        Assert.Equal(0, bus.Read(0x1000));
    }

    [Fact]
    public void Memory_RamIsReadWrite()
    {
        var bus = new SpectrumBus();
        // Lower contended RAM
        bus.Write(0x4000, 0x12);
        Assert.Equal(0x12, bus.Read(0x4000));

        // Upper uncontended RAM
        bus.Write(0xC000, 0x34);
        Assert.Equal(0x34, bus.Read(0xC000));
    }

    [Fact]
    public void Ula_ContentionDelay_AppliedInContentionWindow()
    {
        var ula = new FerrantiUla();

        // Line 64 (Active display line)
        // Frame T-state = 64 * 224 + lineTState
        int lineStartT = 64 * 224;

        // Line T-state 14: first pixel fetch cycle -> delay 6
        Assert.Equal(6, ula.GetContentionDelay(lineStartT + 14));
        // Line T-state 15: delay 5
        Assert.Equal(5, ula.GetContentionDelay(lineStartT + 15));
        // Line T-state 20: delay 0
        Assert.Equal(0, ula.GetContentionDelay(lineStartT + 20));

        // Line 10 (Border line) -> no contention
        Assert.Equal(0, ula.GetContentionDelay(10 * 224 + 14));
    }

    [Fact]
    public void Ula_Framebuffer_GeneratesExpectedDimensions()
    {
        var ula = new FerrantiUla();
        byte[] dummyRam = new byte[49152];

        ula.BorderColor = 5; // Cyan
        ula.RenderFrame(dummyRam);

        Assert.Equal(320 * 240, ula.Framebuffer.Length);

        // Top-left corner must be border color
        uint expectedBorderRgb = FerrantiUla.Palette[5];
        Assert.Equal(expectedBorderRgb, ula.Framebuffer[0]);
    }

    [Fact]
    public void KeyboardMatrix_MapsRowsAndBitsAccurately()
    {
        var kbd = new SpectrumKeyboard();

        // Initially all keys released (0x1F)
        Assert.Equal(0x1F, kbd.Read(0xFE)); // A8 low
        Assert.Equal(0x1F, kbd.Read(0xFD)); // A9 low

        // Press 'A' (Row 1, bit 0)
        kbd.SetKey(SpectrumKey.A, true);
        Assert.Equal(0x1E, kbd.Read(0xFD)); // Bit 0 is 0 (0x1E)
        Assert.Equal(0x1F, kbd.Read(0xFE)); // Other rows unaffected

        // Release 'A'
        kbd.SetKey(SpectrumKey.A, false);
        Assert.Equal(0x1F, kbd.Read(0xFD));
    }
}
