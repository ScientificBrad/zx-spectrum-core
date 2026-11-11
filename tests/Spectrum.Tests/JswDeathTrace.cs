using System;
using System.IO;
using Spectrum.Core;
using Xunit;

namespace Spectrum.Tests;

public class JswDeathTrace
{
    [Fact]
    public void TraceWillyDeath()
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
        machine.FastTapeLoadEnabled = true;
        machine.BypassCopyProtection = true;

        machine.RunFrames(100);
        machine.PressChar('J'); machine.RunFrames(6); machine.ReleaseAllKeys(); machine.RunFrames(6);
        machine.PressChar('"'); machine.RunFrames(6); machine.ReleaseAllKeys(); machine.RunFrames(6);
        machine.PressChar('"'); machine.RunFrames(6); machine.ReleaseAllKeys(); machine.RunFrames(6);
        machine.Tape.InsertTape(tap);
        machine.PressChar('\r'); machine.RunFrames(10); machine.ReleaseAllKeys(); machine.RunFrames(20);
        machine.Tape.Play();

        // Run until tape is done
        while (machine.Tape.State == TapeState.Playing)
        {
            machine.RunFrames(20);
        }

        Console.WriteLine($"Tape done at PC=0x{machine.Cpu.PC:X4}");

        // Press ENTER to start game
        Console.WriteLine("Pressing ENTER to start the game...");
        machine.PressChar('\r');
        for (int f = 0; f < 50; f++) machine.RunFrames(1);
        machine.ReleaseAllKeys();

        for (int f = 0; f < 2; f++)
        {
            long endT = machine.Cpu.TotalTStates + FerrantiUla.TStatesPerFrame;
            while (machine.Cpu.TotalTStates < endT)
            {
                ushort pc = machine.Cpu.PC;
                if (pc >= 0x9637 && pc <= 0x9642)
                {
                    Console.WriteLine($"PC=0x{pc:X4}: A=0x{machine.Cpu.A:X2}, B=0x{machine.Cpu.B:X2}, IX=0x{machine.Cpu.IX:X4}");
                }
                machine.Step();
            }
        }
    }
}
