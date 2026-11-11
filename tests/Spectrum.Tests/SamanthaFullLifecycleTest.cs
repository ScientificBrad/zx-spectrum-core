using System;
using System.IO;
using Spectrum.Core;
using Spectrum.Diagnostics;
using Xunit;

namespace Spectrum.Tests;

public class SamanthaFullLifecycleTest
{
    [Fact]
    public void TestSamanthaLifecycle()
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
        machine.Ula.VideoRenderingEnabled = true;
        machine.Beeper.Enabled = false;

        // Boot and LOAD ""
        machine.RunFrames(100);
        machine.PressChar('J'); machine.RunFrames(6); machine.ReleaseAllKeys(); machine.RunFrames(6);
        machine.PressChar('"'); machine.RunFrames(6); machine.ReleaseAllKeys(); machine.RunFrames(6);
        machine.PressChar('"'); machine.RunFrames(6); machine.ReleaseAllKeys(); machine.RunFrames(6);
        machine.Tape.InsertTape(tap);
        machine.PressChar('\r'); machine.RunFrames(10); machine.ReleaseAllKeys(); machine.RunFrames(20);
        machine.Tape.Play();

        // Run until tape blocks are all loaded
        int loadFrames = 0;
        while (machine.Tape.CurrentBlockIndex < machine.Tape.TotalBlocks && loadFrames < 500)
        {
            machine.RunFrames(1);
            loadFrames++;
        }

        Console.WriteLine($"=== Tape Load Finished: {machine.Tape.CurrentBlockIndex}/{machine.Tape.TotalBlocks} blocks ===");
        Assert.Equal(24, machine.Tape.CurrentBlockIndex);
        Assert.Equal(11, machine.RamDisk.Files.Count);

        // Verify all 11 files are present in RAM disk
        Assert.True(machine.RamDisk.Files.ContainsKey("0"));
        Assert.Equal(6912, machine.RamDisk.Files["0"].Length);
        for (int i = 1; i <= 10; i++)
        {
            Assert.True(machine.RamDisk.Files.ContainsKey(i.ToString()));
            Assert.Equal(4162, machine.RamDisk.Files[i.ToString()].Length);
        }

        // Save screenshot of the "TOUCH ME!" title picture (file "0")
        string touchMePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "../../../../../screenshots/samantha_touch_me.bmp");
        BmpWriter.SaveBmp(touchMePath, machine.Ula.Framebuffer, FerrantiUla.TotalWidth, FerrantiUla.TotalHeight);
        Console.WriteLine($"Saved title screenshot to {touchMePath}");

        int[] touchMeFb = (int[])machine.Ula.Framebuffer.Clone();

        // Trace program execution through animation loop
        Console.WriteLine("=== Tracing Animation Loop Execution ===");
        int diffPixels = 0;
        bool sawAnimationLines = false;

        for (int f = 0; f < 30; f++)
        {
            machine.RunFrames(1);
            ushort ppc = (ushort)(machine.Bus.Read(0x5C45) | (machine.Bus.Read(0x5C46) << 8));
            byte subppc = machine.Bus.Read(0x5C47);
            ushort pc = machine.Cpu.PC;

            if (ppc >= 140 && ppc <= 230)
            {
                sawAnimationLines = true;
            }

            for (int i = 0; i < touchMeFb.Length; i++)
            {
                if (machine.Ula.Framebuffer[i] != touchMeFb[i]) diffPixels++;
            }

            if (diffPixels > 0)
            {
                Console.WriteLine($"[Frame {f}] Framebuffer changed! diffPixels={diffPixels}, PPC={ppc}, PC=0x{pc:X4}");
                string animPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "../../../../../screenshots/samantha_animated_transition.bmp");
                BmpWriter.SaveBmp(animPath, machine.Ula.Framebuffer, FerrantiUla.TotalWidth, FerrantiUla.TotalHeight);
                break;
            }
        }

        Assert.True(sawAnimationLines, "BASIC program should execute animation lines 140-230");
        Assert.True(diffPixels > 0, "Framebuffer should visually change when animation frames are loaded");

        // Verify Sinclair BASIC keyboard interaction via BREAK (Caps Shift + Space)
        Console.WriteLine("=== Injecting BREAK (Caps Shift + Space) ===");
        machine.Keyboard.SetKey(SpectrumKey.CapsShift, true);
        machine.Keyboard.SetKey(SpectrumKey.Space, true);

        bool breakReported = false;
        for (int f = 0; f < 30; f++)
        {
            machine.RunFrames(1);
            byte err = machine.Bus.Read(0x5C3A);
            // Error L in Sinclair 48K ROM is code 20 (0x14), stored in ERR_NR as (code - 1) = 19 (0x13) or 0x14
            if (err == 19 || err == 20 || err == 0x13 || err == 0x14)
            {
                Console.WriteLine($"[Frame {f}] BREAK acknowledged! ERR_NR=0x{err:X2}, PC=0x{machine.Cpu.PC:X4}");
                breakReported = true;
                break;
            }
        }
        machine.ReleaseAllKeys();

        Assert.True(breakReported, "BREAK key combination should be recognized by Sinclair ROM");
        Console.WriteLine("Samantha lifecycle test PASSED successfully!");
    }
}
