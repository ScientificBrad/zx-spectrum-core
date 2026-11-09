using Spectrum.Core;
using Spectrum.Diagnostics;
using Xunit;

namespace Spectrum.Tests;

public class BasicInteractiveTests
{
    private static string FindRomPath()
    {
        string[] candidates = {
            "roms/48.rom",
            "../roms/48.rom",
            "../../roms/48.rom",
            "../../../roms/48.rom",
            "../../../../roms/48.rom",
            "../../../../../roms/48.rom",
            "D:/Emulation/Spectrum ZX/roms/48.rom",
            "D:/Emulation/ZXSpectrum/roms/48.rom"
        };
        foreach (var path in candidates)
        {
            if (File.Exists(path)) return path;
        }
        return "roms/48.rom";
    }

    [Fact]
    public void Basic_InteractiveKeypress_TypesAndExecutesCommand()
    {
        string romFile = FindRomPath();
        if (!File.Exists(romFile)) return;

        byte[] rom = File.ReadAllBytes(romFile);
        var machine = new SpectrumMachine(rom);
        machine.Reset();

        // 1. Run until boot finishes and "(C) 1982..." is displayed (120 frames)
        machine.RunFrames(120);

        // 2. Press ENTER to clear boot message and enter BASIC editor
        machine.PressChar('\r');
        machine.RunFrames(10);
        machine.ReleaseAllKeys();
        machine.RunFrames(10);

        // 3. Type 'P' (in K-mode, 'P' expands to token 'PRINT')
        machine.PressChar('P');
        machine.RunFrames(6);
        machine.ReleaseAllKeys();
        machine.RunFrames(6);

        // 4. Type ' ' (Space)
        machine.PressChar(' ');
        machine.RunFrames(6);
        machine.ReleaseAllKeys();
        machine.RunFrames(6);

        // 5. Type '4'
        machine.PressChar('4');
        machine.RunFrames(6);
        machine.ReleaseAllKeys();
        machine.RunFrames(6);

        // 6. Type '2'
        machine.PressChar('2');
        machine.RunFrames(6);
        machine.ReleaseAllKeys();
        machine.RunFrames(6);

        // 7. Press ENTER to execute "PRINT 42"
        machine.PressChar('\r');
        machine.RunFrames(20);
        machine.ReleaseAllKeys();
        machine.RunFrames(20);

        // 8. Capture screenshot
        string screenshotPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "../../../../../screenshots/basic_interactive.bmp");
        BmpWriter.SaveBmp(screenshotPath, machine.Ula.Framebuffer, FerrantiUla.TotalWidth, FerrantiUla.TotalHeight);

        // Verify that black ink pixels exist in top screen area where "42" is printed
        // Active screen starts at y=24, line 0 is y=24..31
        bool found42Ink = false;
        for (int y = 24; y < 40; y++)
        {
            int rowStart = y * FerrantiUla.TotalWidth;
            for (int x = 32; x < 100; x++)
            {
                if (machine.Ula.Framebuffer[rowStart + x] == FerrantiUla.Palette[0]) // Black ink
                {
                    found42Ink = true;
                    break;
                }
            }
            if (found42Ink) break;
        }

        Assert.True(found42Ink, "Result of PRINT 42 must be rendered on the upper display");
    }
}
