using System;
using System.IO;
using Spectrum.Core;
using Spectrum.Diagnostics;
using Xunit;

namespace Spectrum.Tests;

public class SamanthaTraceTest
{
    [Fact]
    public void TraceSamanthaAfterTapeEnd()
    {
        string romFile = "roms/48.rom";
        if (!File.Exists(romFile)) romFile = "../../../../../roms/48.rom";
        string tapPath = "games/SAMANTHA.TAP";
        if (!File.Exists(tapPath)) tapPath = "../../../../../games/SAMANTHA.TAP";

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

        Console.WriteLine($"Starting tape load: Total blocks = {machine.Tape.TotalBlocks}");

        ushort lastPpc = 0;
        int frames = 0;
        while (machine.Tape.CurrentBlockIndex < machine.Tape.TotalBlocks && frames < 500)
        {
            machine.RunFrames(1);
            frames++;
            ushort curPpc = (ushort)(machine.Bus.Read(0x5C45) | (machine.Bus.Read(0x5C46) << 8));
            if (curPpc != lastPpc)
            {
                Console.WriteLine($"[Frame {frames}] Block={machine.Tape.CurrentBlockIndex}/{machine.Tape.TotalBlocks}, PPC changed to {curPpc}");
                lastPpc = curPpc;
            }
        }

        Console.WriteLine($"Tape load finished at frame {frames}. CurrentBlockIndex={machine.Tape.CurrentBlockIndex}/{machine.Tape.TotalBlocks}, TapeState={machine.Tape.State}");
        Console.WriteLine($"RAM Disk stored files: {machine.RamDisk.Files.Count}");
        foreach (var kvp in machine.RamDisk.Files)
        {
            Console.WriteLine($"  File '{kvp.Key}': {kvp.Value.Length} bytes");
        }

        // Run 50 frames to let BASIC reach line 120 / 130
        for (int f = 0; f < 50; f++)
        {
            machine.RunFrames(1);
            ushort curPpc = (ushort)(machine.Bus.Read(0x5C45) | (machine.Bus.Read(0x5C46) << 8));
            byte curSub = machine.Bus.Read(0x5C47);
            ushort pc = machine.Cpu.PC;
            Console.WriteLine($"[After Load Frame {f:D2}] PC=0x{pc:X4}, PPC={curPpc}, SUBPPC={curSub}, FLAGS=0x{machine.Bus.Read(0x5C3B):X2}");
        }

        ushort ppc = (ushort)(machine.Bus.Read(0x5C45) | (machine.Bus.Read(0x5C46) << 8));
        byte subppc = machine.Bus.Read(0x5C47);
        byte flags = machine.Bus.Read(0x5C3B);
        ushort iy = machine.Cpu.IY;
        Console.WriteLine($"After load: PC=0x{machine.Cpu.PC:X4}, SP=0x{machine.Cpu.SP:X4}, IY=0x{iy:X4}, PPC={ppc}, SUBPPC={subppc}, FLAGS=0x{flags:X2}, IFF1={machine.Cpu.IFF1}, IM={machine.Cpu.IM}");

        // Save screenshot of what is currently on screen
        machine.Ula.VideoRenderingEnabled = true;
        machine.RunFrames(1);
        string screenPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "../../../../../screenshots/samantha_at_title.bmp");
        BmpWriter.SaveBmp(screenPath, machine.Ula.Framebuffer, FerrantiUla.TotalWidth, FerrantiUla.TotalHeight);
        Console.WriteLine($"Saved title screenshot to {screenPath}");

        // Now simulate pressing SPACE or ENTER to exit PAUSE
        Console.WriteLine("Injecting SPACE key press...");
        machine.PressChar(' ');
        for (int f = 0; f < 20; f++)
        {
            machine.RunFrames(1);
            ppc = (ushort)(machine.Bus.Read(0x5C45) | (machine.Bus.Read(0x5C46) << 8));
            flags = machine.Bus.Read(0x5C3B);
            Console.WriteLine($"  [Frame +{f}] PC=0x{machine.Cpu.PC:X4}, PPC={ppc}, FLAGS=0x{flags:X2}");
        }
        machine.ReleaseAllKeys();

        // Run another 50 frames
        for (int f = 0; f < 50; f++)
        {
            machine.RunFrames(1);
            ppc = (ushort)(machine.Bus.Read(0x5C45) | (machine.Bus.Read(0x5C46) << 8));
            if (f % 10 == 0 || ppc != 130)
            {
                Console.WriteLine($"  [After Release Frame +{f}] PC=0x{machine.Cpu.PC:X4}, PPC={ppc}");
            }
        }
    }
}
