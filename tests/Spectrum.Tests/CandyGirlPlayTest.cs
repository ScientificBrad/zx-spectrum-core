using System;
using System.IO;
using Spectrum.Core;
using Spectrum.Diagnostics;
using Xunit;

namespace Spectrum.Tests;

public class CandyGirlPlayTest
{
    [Fact]
    public void TestCandyGirlFullGame()
    {
        string romFile = "roms/48.rom";
        if (!File.Exists(romFile)) romFile = "../../../../../roms/48.rom";
        string tapPath = "games/CANDY_GI.TAP";
        if (!File.Exists(tapPath)) tapPath = "../../../../../games/CANDY_GI.TAP";

        byte[] rom = File.ReadAllBytes(romFile);
        var tap = TapFile.Load(tapPath);
        var machine = new SpectrumMachine(rom);
        machine.Reset();
        machine.FastTapeLoadEnabled = true;
        machine.Ula.VideoRenderingEnabled = false;
        machine.Beeper.Enabled = false;

        // Boot and LOAD ""
        machine.RunFrames(100);
        machine.PressChar('J'); machine.RunFrames(6); machine.ReleaseAllKeys(); machine.RunFrames(6);
        machine.PressChar('"'); machine.RunFrames(6); machine.ReleaseAllKeys(); machine.RunFrames(6);
        machine.PressChar('"'); machine.RunFrames(6); machine.ReleaseAllKeys(); machine.RunFrames(6);
        machine.Tape.InsertTape(tap);
        machine.PressChar('\r'); machine.RunFrames(10); machine.ReleaseAllKeys(); machine.RunFrames(20);
        machine.Tape.Play();

        Console.WriteLine("Loading CANDY_GI.TAP...");
        int frames = 0;
        // With fast tape loading, all 4 blocks load nearly instantly!
        while (machine.Tape.CurrentBlockIndex < machine.Tape.TotalBlocks && frames < 500)
        {
            machine.RunFrames(1);
            frames++;
        }

        Console.WriteLine($"Tape loading done at frame {frames}. CurrentBlockIndex={machine.Tape.CurrentBlockIndex}/{machine.Tape.TotalBlocks}, PC=0x{machine.Cpu.PC:X4}");

        // Now enable video rendering and run 100 frames of the game
        machine.Ula.VideoRenderingEnabled = true;
        for (int f = 0; f < 100; f++)
        {
            machine.RunFrames(1);
        }

        Console.WriteLine($"Game running: PC=0x{machine.Cpu.PC:X4}, SP=0x{machine.Cpu.SP:X4}, Border={machine.Ula.BorderColor}");

        string outPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "../../../../../screenshots/candygirl_game.bmp");
        BmpWriter.SaveBmp(outPath, machine.Ula.Framebuffer, FerrantiUla.TotalWidth, FerrantiUla.TotalHeight);
        Console.WriteLine($"Saved screenshot to {outPath}");
    }
}
