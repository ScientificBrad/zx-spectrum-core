using System;
using System.IO;
using Spectrum.Core;
using Xunit;

namespace Spectrum.Tests;

public class JswVramCheck
{
    [Fact]
    public void CheckVramBytes()
    {
        string romFile = "roms/48.rom";
        if (!File.Exists(romFile)) romFile = "../../../../../roms/48.rom";
        string tapPath = "games/JETSET.TAP";
        if (!File.Exists(tapPath)) tapPath = "../../../../../games/JETSET.TAP";

        byte[] rom = File.ReadAllBytes(romFile);
        var tap = TapFile.Load(tapPath);
        var machine = new SpectrumMachine(rom);
        machine.Reset();
        machine.Ula.VideoRenderingEnabled = true;
        machine.Beeper.Enabled = false;

        machine.RunFrames(100);
        machine.PressChar('J'); machine.RunFrames(6); machine.ReleaseAllKeys(); machine.RunFrames(6);
        machine.PressChar('"'); machine.RunFrames(6); machine.ReleaseAllKeys(); machine.RunFrames(6);
        machine.PressChar('"'); machine.RunFrames(6); machine.ReleaseAllKeys(); machine.RunFrames(6);
        machine.Tape.InsertTape(tap);
        machine.PressChar('\r'); machine.RunFrames(10); machine.ReleaseAllKeys(); machine.RunFrames(20);
        machine.Tape.Play();

        while (machine.Tape.State == TapeState.Playing) machine.RunFrames(20);

        // Bypass code check: jump to 0x87CA (title screen / game setup)
        machine.Cpu.PC = 0x87CA;
        machine.RunFrames(5);

        // Jump to Start Game
        machine.Cpu.PC = 0x88FC;

        // Run 80 frames into the game
        for (int f = 0; f < 80; f++)
        {
            machine.RunFrames(1);
            byte room = machine.Bus.Read(0x8420);
            byte lives = machine.Bus.Read(0x85CC);
            byte mode = machine.Bus.Read(0x85DF);
            byte y = machine.Bus.Read(0x85D7);
            byte x = machine.Bus.Read(0x85D8);
            byte dir = machine.Bus.Read(0x85D9);
            ushort attr = (ushort)(machine.Bus.Read(0x85DB) | (machine.Bus.Read(0x85DC) << 8));
            byte cf_y = machine.Bus.Read(0x85CF);
            ushort d3_attr = (ushort)(machine.Bus.Read(0x85D3) | (machine.Bus.Read(0x85D4) << 8));
            if (f == 5)
            {
                string f5Path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "../../../../../screenshots/jsw_frame5.bmp");
                Spectrum.Diagnostics.BmpWriter.SaveBmp(f5Path, machine.Ula.Framebuffer, FerrantiUla.TotalWidth, FerrantiUla.TotalHeight);
            }
        }

        Console.WriteLine($"=== Memory Check at Frame 20 ===");
        Console.WriteLine($"Attr at 0x59B4 (VRAM Attr Row 13 Col 20): 0x{machine.Bus.Read(0x59B4):X2}");
        Console.WriteLine($"Attr at 0x5DB4 (Buffer Attr): 0x{machine.Bus.Read(0x5DB4):X2}");

        Console.Write("Bitmap at 0x48B4..0x48BD: ");
        for (int i = 0; i < 16; i++)
        {
            Console.Write($"{machine.Bus.Read((ushort)(0x48B4 + i)):X2} ");
        }
        Console.WriteLine();

        Console.Write("Buffer at 0x68B4..0x68BD: ");
        for (int i = 0; i < 16; i++)
        {
            Console.Write($"{machine.Bus.Read((ushort)(0x68B4 + i)):X2} ");
        }
        Console.WriteLine();
    }
}
