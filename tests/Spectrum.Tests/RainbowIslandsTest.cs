using System;
using System.IO;
using Spectrum.Core;
using Xunit;

namespace Spectrum.Tests;

public class RainbowIslandsTest
{
    [Fact]
    public void TestRainbowIslandsLoad()
    {
        string romFile = "roms/48.rom";
        if (!File.Exists(romFile)) romFile = "../../../../../roms/48.rom";
        string tapPath = "games/Rainbow Islands.tap";
        if (!File.Exists(tapPath)) tapPath = "../../../../../games/Rainbow Islands.tap";

        byte[] rom = File.ReadAllBytes(romFile);
        var tap = TapFile.Load(tapPath);
        var machine = new SpectrumMachine(rom);
        machine.Reset();
        machine.FastTapeLoadEnabled = true;
        machine.BypassCopyProtection = false;
        machine.Ula.VideoRenderingEnabled = true;
        machine.Beeper.Enabled = false;

        Console.WriteLine($"Rainbow Islands total blocks: {tap.Blocks.Count}");

        // Boot (60 frames)
        machine.RunFrames(60);

        // Track when IM changes to 2
        machine.PressChar('\r'); machine.RunFrames(6); machine.ReleaseAllKeys(); machine.RunFrames(20);
        machine.PressChar('J'); machine.RunFrames(6); machine.ReleaseAllKeys(); machine.RunFrames(10);
        machine.PressChar('"'); machine.RunFrames(6); machine.ReleaseAllKeys(); machine.RunFrames(10);
        machine.PressChar('"'); machine.RunFrames(6); machine.ReleaseAllKeys(); machine.RunFrames(10);

        machine.Tape.InsertTape(tap);
        machine.PressChar('\r'); machine.RunFrames(6); machine.ReleaseAllKeys(); machine.RunFrames(20);
        machine.Tape.Play();

        int prevIM = machine.Cpu.IM;
        while (machine.Tape.CurrentBlockIndex < 6)
        {
            machine.Step();
            if (machine.Cpu.IM != prevIM)
            {
                Console.WriteLine($"IM CHANGED from {prevIM} to {machine.Cpu.IM} at PC=0x{machine.Cpu.PC:X4}, SP=0x{machine.Cpu.SP:X4}, Block={machine.Tape.CurrentBlockIndex}");
                prevIM = machine.Cpu.IM;
            }
            if (machine.Cpu.PC >= 0x8306 && machine.Cpu.PC <= 0x830A && machine.Tape.CurrentBlockIndex >= 5)
            {
                Console.WriteLine($"Reached wait loop at PC=0x{machine.Cpu.PC:X4}, IM={machine.Cpu.IM}, I=0x{machine.Cpu.I:X2}");
                break;
            }
        }

        // Stop the tape player because World 1 is loaded!
        machine.Tape.Stop();
        Console.WriteLine($"Tape stopped at block {machine.Tape.CurrentBlockIndex}");

        Console.WriteLine("Memory at 0x8300..0x8320:");
        for (ushort a = 0x8300; a <= 0x8320; a++)
        {
            Console.WriteLine($"0x{a:X4}: {machine.Bus.Read(a):X2}");
        }

        Console.WriteLine($"Current PC: 0x{machine.Cpu.PC:X4}, SP: 0x{machine.Cpu.SP:X4}, Top of stack: 0x{machine.Bus.Read(machine.Cpu.SP) | (machine.Bus.Read((ushort)(machine.Cpu.SP + 1)) << 8):X4}");

        // Now press Space to start the game!
        Console.WriteLine("Pressing Space to start Rainbow Islands...");
        machine.PressChar(' ');

        // Run 200 frames to let game unpack/draw title and attract mode
        for (int f = 0; f < 200; f++)
        {
            machine.RunFrames(1);
            if (f % 20 == 0)
            {
                Console.WriteLine($"Frame {f}: PC=0x{machine.Cpu.PC:X4}, SP=0x{machine.Cpu.SP:X4}, Border={machine.Ula.BorderColor}");
            }
        }

        Console.WriteLine($"Final state: PC=0x{machine.Cpu.PC:X4}, SP=0x{machine.Cpu.SP:X4}, Border={machine.Ula.BorderColor}");
        Assert.True(machine.Cpu.PC >= 0x4000, "Rainbow Islands should execute in RAM game code");
        Assert.True(machine.Cpu.PC != 0x0000 && machine.Cpu.PC != 0x0038, "Game should not crash/reset");

        string scrPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "../../../../../screenshots/rainbow_ingame.bmp");
        Spectrum.Diagnostics.BmpWriter.SaveBmp(scrPath, machine.Ula.Framebuffer, FerrantiUla.TotalWidth, FerrantiUla.TotalHeight);
    }
}
