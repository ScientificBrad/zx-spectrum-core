using System;
using System.IO;
using Spectrum.Core;
using Xunit;

namespace Spectrum.Tests;

public class KeyboardScanTest
{
    [Fact]
    public void TestKeyboardScanInRom()
    {
        string romFile = "roms/48.rom";
        if (!File.Exists(romFile)) romFile = "../../../../../roms/48.rom";

        byte[] rom = File.ReadAllBytes(romFile);
        var machine = new SpectrumMachine(rom);
        machine.Reset();

        // Boot to BASIC (100 frames)
        machine.RunFrames(100);

        Console.WriteLine($"Booted. PC=0x{machine.Cpu.PC:X4}, IY=0x{machine.Cpu.IY:X4}, FLAGS=0x{machine.Bus.Read(0x5C3B):X2}");

        // Clear FLAGS bit 5
        machine.Bus.Write(0x5C3B, (byte)(machine.Bus.Read(0x5C3B) & ~0x20));
        Console.WriteLine($"Cleared bit 5. FLAGS=0x{machine.Bus.Read(0x5C3B):X2}");

        // Now press Space
        machine.Keyboard.SetKey(SpectrumKey.Space, true);

        // Release all keys and run 10 frames so debounce resets KSTATE
        machine.ReleaseAllKeys();
        machine.RunFrames(10);
        Console.WriteLine($"After 10 idle frames: KSTATE0=0x{machine.Bus.Read(0x5C00):X2}, KSTATE4=0x{machine.Bus.Read(0x5C04):X2}, FLAGS=0x{machine.Bus.Read(0x5C3B):X2}");

        // Now press Space
        Console.WriteLine("Now pressing Space key...");
        machine.Keyboard.SetKey(SpectrumKey.Space, true);

        for (int frame = 1; frame <= 10; frame++)
        {
            machine.RunFrames(1);
            byte f = machine.Bus.Read(0x5C3B);
            byte lastK = machine.Bus.Read(0x5C08);
            byte k0 = machine.Bus.Read(0x5C00);
            byte k4 = machine.Bus.Read(0x5C04);
            Console.WriteLine($"Frame {frame} of Space: FLAGS=0x{f:X2} (bit 5 = {(f & 0x20) != 0}), LAST-K={lastK}, KSTATE0=0x{k0:X2}, KSTATE4=0x{k4:X2}");
        }
    }

    [Fact]
    public void TestPauseWithoutArgument()
    {
        string romFile = "roms/48.rom";
        if (!File.Exists(romFile)) romFile = "../../../../../roms/48.rom";

        byte[] rom = File.ReadAllBytes(romFile);
        var machine = new SpectrumMachine(rom);
        machine.Reset();
        machine.RunFrames(100);

        // Inject line: 10 PAUSE (E5 0D) into PROG
        // PROG is at (0x5C53)
        ushort prog = (ushort)(machine.Bus.Read(0x5C53) | (machine.Bus.Read(0x5C54) << 8));
        machine.Bus.Write(prog, 0x00);
        machine.Bus.Write((ushort)(prog + 1), 0x0A); // line 10
        machine.Bus.Write((ushort)(prog + 2), 0x02);
        machine.Bus.Write((ushort)(prog + 3), 0x00); // len 2
        machine.Bus.Write((ushort)(prog + 4), 0xE5); // PAUSE
        machine.Bus.Write((ushort)(prog + 5), 0x0D); // \r
        machine.Bus.Write((ushort)(prog + 6), 0x80); // end marker
        // Update VARS (0x5C4B) and E_LINE (0x5C59)
        ushort newVars = (ushort)(prog + 6);
        machine.Bus.Write(0x5C4B, (byte)(newVars & 0xFF));
        machine.Bus.Write(0x5C4C, (byte)(newVars >> 8));

        // Now run "RUN" (R then \r)
        machine.PressChar('R'); machine.RunFrames(6); machine.ReleaseAllKeys(); machine.RunFrames(6);
        machine.PressChar('\r'); machine.RunFrames(6); machine.ReleaseAllKeys(); machine.RunFrames(20);

        byte err = machine.Bus.Read(0x5C3A);
        ushort pc = machine.Cpu.PC;
        ushort ppc = (ushort)(machine.Bus.Read(0x5C45) | (machine.Bus.Read(0x5C46) << 8));
        Console.WriteLine($"After RUN: PPC={ppc}, PC=0x{pc:X4}, ERR_NR=0x{err:X2}, Halted={machine.Cpu.Halted}");
    }
}
