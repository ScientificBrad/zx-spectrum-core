using System;
using System.IO;
using Spectrum.Core;
using Xunit;

namespace Spectrum.Tests;

public class DebugKeyInterruptTest
{
    [Fact]
    public void TraceKeyInterrupt()
    {
        string romFile = "roms/48.rom";
        if (!File.Exists(romFile)) romFile = "../../../../../roms/48.rom";

        byte[] rom = File.ReadAllBytes(romFile);
        var machine = new SpectrumMachine(rom);
        machine.Reset();

        // Boot
        machine.RunFrames(100);
        machine.ReleaseAllKeys();
        machine.RunFrames(10);

        Console.WriteLine($"Booted. PC=0x{machine.Cpu.PC:X4}, IY=0x{machine.Cpu.IY:X4}, FLAGS=0x{machine.Bus.Read(0x5C3B):X2}");

        // Clear bit 5
        machine.Bus.Write(0x5C3B, 0x00);

        // Press Space
        machine.Keyboard.SetKey(SpectrumKey.Space, true);

        // Now run 1 frame, stepping instruction by instruction, and see what happens at 0x02BF
        long targetT = machine.Cpu.TotalTStates + FerrantiUla.TStatesPerFrame;
        bool inInterrupt = false;
        int count02bf = 0;

        while (machine.Cpu.TotalTStates < targetT)
        {
            ushort pc = machine.Cpu.PC;
            if (pc == 0x0038)
            {
                inInterrupt = true;
                Console.WriteLine($"[T={machine.Cpu.TotalTStates}] Interrupt entered at 0x0038! IY=0x{machine.Cpu.IY:X4}, SP=0x{machine.Cpu.SP:X4}");
            }
            if (inInterrupt)
            {
                if (pc == 0x02BF)
                {
                    count02bf++;
                    Console.WriteLine($"  [T={machine.Cpu.TotalTStates}] 0x02BF called!");
                }
                else if (pc == 0x028E)
                {
                    Console.WriteLine($"  [T={machine.Cpu.TotalTStates}] 0x028E (KEY-SCAN) called!");
                }
                else if (pc == 0x02C2)
                {
                    Console.WriteLine($"  [T={machine.Cpu.TotalTStates}] 0x02C2 RET NZ! F=0x{machine.Cpu.F:X2} (Z={(machine.Cpu.F & Z80Cpu.FlagZ) != 0})");
                }
                else if (pc >= 0x02D8 && pc <= 0x0310)
                {
                    Console.WriteLine($"  [T={machine.Cpu.TotalTStates}] PC=0x{pc:X4}, A=0x{machine.Cpu.A:X2}, HL=0x{machine.Cpu.HL:X4}, DE=0x{machine.Cpu.DE:X4}, F=0x{machine.Cpu.F:X2}");
                }
                else if (pc == 0x0038 + 0x10) // near end of interrupt
                {
                    //
                }
            }
            if (pc == 0x15DE) // back in BASIC editor loop
            {
                if (inInterrupt)
                {
                    inInterrupt = false;
                    Console.WriteLine($"Interrupt exited. FLAGS=0x{machine.Bus.Read(0x5C3B):X2}, LAST-K={machine.Bus.Read(0x5C08)}");
                }
            }

            machine.Step();
        }

        Console.WriteLine($"End of frame. FLAGS=0x{machine.Bus.Read(0x5C3B):X2}");
    }
}
