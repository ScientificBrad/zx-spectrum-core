using System.IO;
using Spectrum.Core;
using Xunit;

namespace Spectrum.Tests;

public class QuickLoadTest
{
    [Fact]
    public void TestQuickLoadEnqueuesAndTypes()
    {
        string romFile = "roms/48.rom";
        if (!File.Exists(romFile)) romFile = "../../../../../roms/48.rom";
        byte[] rom = File.ReadAllBytes(romFile);

        var machine = new SpectrumMachine(rom);
        machine.Reset();

        // Create a dummy TAP
        var tap = new TapFile();
        tap.Blocks.Add(new TapBlock(new byte[19]));
        machine.Tape.InsertTape(tap);

        // Boot 60 frames
        machine.RunFrames(60);

        // Call QuickLoad()
        machine.QuickLoad();

        // Run 30 frames for the automated keystrokes to type
        machine.RunFrames(30);

        // Tape should have attempted play
        Assert.Equal(TapeState.Playing, machine.Tape.State);
    }
}
