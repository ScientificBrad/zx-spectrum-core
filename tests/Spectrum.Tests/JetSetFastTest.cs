using System;
using System.IO;
using Spectrum.Core;
using Spectrum.Diagnostics;
using Xunit;

namespace Spectrum.Tests;

public class JetSetFastTest
{
    [Fact]
    public void TestJetSetWillyFastLoadAndPlay()
    {
        string romFile = "roms/48.rom";
        if (!File.Exists(romFile)) romFile = "../../../../../roms/48.rom";
        string tapPath = "games/JETSET.TAP";
        if (!File.Exists(tapPath)) tapPath = "../../../../../games/JETSET.TAP";

        byte[] rom = File.ReadAllBytes(romFile);
        var tap = TapFile.Load(tapPath);
        var machine = new SpectrumMachine(rom);
        machine.Reset();
        machine.FastTapeLoadEnabled = true;
        machine.BypassCopyProtection = true;
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

        Console.WriteLine("Loading JETSET.TAP...");
        int frames = 0;
        while (machine.Tape.CurrentBlockIndex < machine.Tape.TotalBlocks && frames < 500)
        {
            machine.RunFrames(1);
            frames++;
        }

        Console.WriteLine($"Tape loading done at frame {frames}. CurrentBlockIndex={machine.Tape.CurrentBlockIndex}/{machine.Tape.TotalBlocks}, PC=0x{machine.Cpu.PC:X4}");

        // Now run 30 frames of title screen
        for (int f = 0; f < 30; f++)
        {
            machine.RunFrames(1);
        }

        Console.WriteLine($"Title screen: PC=0x{machine.Cpu.PC:X4}");
        string titlePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "../../../../../screenshots/jsw_fast_title.bmp");
        BmpWriter.SaveBmp(titlePath, machine.Ula.Framebuffer, FerrantiUla.TotalWidth, FerrantiUla.TotalHeight);

        // Press ENTER to start game
        Console.WriteLine("Pressing ENTER to start the game...");
        machine.PressChar('\r');
        for (int f = 0; f < 50; f++) machine.RunFrames(1);
        machine.ReleaseAllKeys();

        // Willy in bath
        byte initialX = machine.Bus.Read(0x85CE);
        ushort initialAttr = (ushort)(machine.Bus.Read(0x85D3) | (machine.Bus.Read(0x85D4) << 8));
        Console.WriteLine($"Willy at start: X=0x{initialX:X2}, Attr=0x{initialAttr:X4}");

        // Press 'O' to walk Left
        Console.WriteLine("Holding 'O' (walk Left)...");
        machine.PressChar('O');
        for (int f = 0; f < 50; f++)
        {
            machine.RunFrames(1);
            if (f % 10 == 0)
            {
                byte curX = machine.Bus.Read(0x85CE);
                ushort curAttr = (ushort)(machine.Bus.Read(0x85D3) | (machine.Bus.Read(0x85D4) << 8));
                Console.WriteLine($"[Walking Left Frame {f}] X=0x{curX:X2}, Attr=0x{curAttr:X4}");
            }
        }
        machine.ReleaseAllKeys();

        byte movedX = machine.Bus.Read(0x85CE);
        ushort movedAttr = (ushort)(machine.Bus.Read(0x85D3) | (machine.Bus.Read(0x85D4) << 8));
        Console.WriteLine($"Willy after walking Left: X=0x{movedX:X2}, Attr=0x{movedAttr:X4}");

        Assert.Equal(0x5DB4, initialAttr);
        Assert.True(movedAttr < initialAttr, $"Willy should have moved left (initial=0x{initialAttr:X4}, moved=0x{movedAttr:X4})");

        // Run 50 frames of gameplay
        for (int f = 0; f < 50; f++)
        {
            machine.RunFrames(1);
            byte mode = machine.Bus.Read(0x85DF);
            byte y = machine.Bus.Read(0x85CF);
            ushort attr = (ushort)(machine.Bus.Read(0x85D3) | (machine.Bus.Read(0x85D4) << 8));
            byte room = machine.Bus.Read(0x8420);
            byte lives = machine.Bus.Read(0x85CC);
            if (f < 10)
            {
                Console.WriteLine($"[Game Frame {f}] Room={room}, Lives={lives}, Mode(85DF)={mode}, Y(85CF)=0x{y:X2}, Attr(85D3)=0x{attr:X4}");
            }
        }

        Console.WriteLine($"In-game: PC=0x{machine.Cpu.PC:X4}, SP=0x{machine.Cpu.SP:X4}, Room={machine.Bus.Read(0x8420)}, Lives={machine.Bus.Read(0x85CC)}, Border={machine.Ula.BorderColor}");
        string gamePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "../../../../../screenshots/jsw_fast_game.bmp");
        BmpWriter.SaveBmp(gamePath, machine.Ula.Framebuffer, FerrantiUla.TotalWidth, FerrantiUla.TotalHeight);

        Assert.Equal(33, machine.Bus.Read(0x8420)); // The Bathroom
        Assert.Equal(7, machine.Bus.Read(0x85CC));  // 7 lives intact
        Assert.Equal(0, machine.Bus.Read(0x85DF));  // Alive (mode 0)
    }
}
