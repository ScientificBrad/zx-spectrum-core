using System;
using System.IO;
using Spectrum.Core;
using Spectrum.Diagnostics;
using Xunit;

namespace Spectrum.Tests;

public class JswBypassTest
{
    [Fact]
    public void TestJetSetWillyGameplay()
    {
        string romFile = "roms/48.rom";
        if (!File.Exists(romFile)) romFile = "../../../../../roms/48.rom";
        string tapPath = "games/JETSET.TAP";
        if (!File.Exists(tapPath)) tapPath = "../../../../../games/JETSET.TAP";

        byte[] rom = File.ReadAllBytes(romFile);
        var tap = TapFile.Load(tapPath);
        var machine = new SpectrumMachine(rom);
        machine.Reset();

        machine.Ula.VideoRenderingEnabled = false;
        machine.Beeper.Enabled = false;

        // Boot & LOAD ""
        machine.RunFrames(100);
        machine.PressChar('J'); machine.RunFrames(6); machine.ReleaseAllKeys(); machine.RunFrames(6);
        machine.PressChar('"'); machine.RunFrames(6); machine.ReleaseAllKeys(); machine.RunFrames(6);
        machine.PressChar('"'); machine.RunFrames(6); machine.ReleaseAllKeys(); machine.RunFrames(6);
        machine.Tape.InsertTape(tap);
        machine.PressChar('\r'); machine.RunFrames(10); machine.ReleaseAllKeys(); machine.RunFrames(20);
        machine.Tape.Play();

        // Run until tape finishes loading block 4
        while (machine.Tape.State == TapeState.Playing)
        {
            machine.RunFrames(20);
        }

        Console.WriteLine($"Tape finished. PC=0x{machine.Cpu.PC:X4}, SP=0x{machine.Cpu.SP:X4}");

        // Apply classic bypass POKEs:
        // POKE 33797, 135 (0x8405 = 0x87)
        // POKE 33800, 202 (0x8408 = 0xCA)
        // Or if already loaded past 0x8400, set PC to 0x87CA:
        machine.Cpu.PC = 0x87CA;

        machine.Ula.VideoRenderingEnabled = true;

        // Run 50 frames of title screen / theme tune
        Console.WriteLine("Running title screen...");
        for (int f = 0; f < 50; f++)
        {
            machine.RunFrames(1);
        }

        string titlePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "../../../../../screenshots/jsw_title.bmp");
        BmpWriter.SaveBmp(titlePath, machine.Ula.Framebuffer, FerrantiUla.TotalWidth, FerrantiUla.TotalHeight);
        Console.WriteLine($"Saved title screenshot to {titlePath}");

        // Jump directly to Start the Game entry point (0x88FC)
        Console.WriteLine("Jumping to Start the Game (0x88FC)...");
        machine.Cpu.PC = 0x88FC;

        // Run 100 frames of gameplay
        for (int f = 0; f < 100; f++)
        {
            machine.RunFrames(1);
            if (f % 20 == 0)
            {
                Console.WriteLine($"In-Game Frame {f}: PC=0x{machine.Cpu.PC:X4}, SP=0x{machine.Cpu.SP:X4}, Border={machine.Ula.BorderColor}");
            }
        }

        string gamePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "../../../../../screenshots/jsw_game.bmp");
        BmpWriter.SaveBmp(gamePath, machine.Ula.Framebuffer, FerrantiUla.TotalWidth, FerrantiUla.TotalHeight);
        Console.WriteLine($"Saved game screenshot to {gamePath}");
    }
}
