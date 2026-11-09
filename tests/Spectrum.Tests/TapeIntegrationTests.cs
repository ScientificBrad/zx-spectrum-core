using Spectrum.Core;
using Spectrum.Diagnostics;
using Xunit;

namespace Spectrum.Tests;

public class TapeIntegrationTests
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
    public void Tape_RomLoader_LoadsTapFileSuccessfully()
    {
        string romFile = FindRomPath();
        if (!File.Exists(romFile)) return;

        string tapPath = "games/demo.tap";
        if (!File.Exists(tapPath))
        {
            // Try relative paths
            string[] tapCandidates = {
                "games/demo.tap",
                "../games/demo.tap",
                "../../games/demo.tap",
                "../../../games/demo.tap",
                "../../../../games/demo.tap",
                "../../../../../games/demo.tap",
                "D:/Emulation/Spectrum ZX/games/demo.tap",
                "D:/Emulation/ZXSpectrum/games/demo.tap"
            };
            foreach (var candidate in tapCandidates)
            {
                if (File.Exists(candidate))
                {
                    tapPath = candidate;
                    break;
                }
            }
        }

        if (!File.Exists(tapPath)) return;

        var tap = TapFile.Load(tapPath);
        Assert.Equal(2, tap.Blocks.Count);

        byte[] rom = File.ReadAllBytes(romFile);
        var machine = new SpectrumMachine(rom);
        machine.Reset();

        // 1. Boot machine to BASIC (120 frames)
        machine.RunFrames(120);

        // 2. Clear boot screen with Enter
        machine.PressChar('\r');
        machine.RunFrames(10);
        machine.ReleaseAllKeys();
        machine.RunFrames(10);

        // 3. Type 'J' (Token 'LOAD' in K-mode)
        machine.PressChar('J');
        machine.RunFrames(6);
        machine.ReleaseAllKeys();
        machine.RunFrames(6);

        // 4. Type '\"'
        machine.PressChar('"');
        machine.RunFrames(6);
        machine.ReleaseAllKeys();
        machine.RunFrames(6);

        // 5. Type '\"'
        machine.PressChar('"');
        machine.RunFrames(6);
        machine.ReleaseAllKeys();
        machine.RunFrames(6);

        // 6. Insert tape
        machine.Tape.InsertTape(tap);

        // 7. Press ENTER to start LOAD "" command
        machine.PressChar('\r');
        machine.RunFrames(10);
        machine.ReleaseAllKeys();

        // 8. Start tape playback
        machine.Tape.Play();

        // 9. Run frames until tape playback completes (approx 450 frames)
        int maxFrames = 600;
        int frameCount = 0;
        while (machine.Tape.State == TapeState.Playing && frameCount < maxFrames)
        {
            machine.RunFrames(10);
            frameCount += 10;
        }

        // Run an additional 30 frames for autostart line 10 to execute
        machine.RunFrames(30);

        // 10. Capture screenshot of loaded program execution
        string screenshotPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "../../../../../screenshots/tape_load.bmp");
        BmpWriter.SaveBmp(screenshotPath, machine.Ula.Framebuffer, FerrantiUla.TotalWidth, FerrantiUla.TotalHeight);

        // Verify tape completed
        Assert.Equal(TapeState.Stopped, machine.Tape.State);

        // Verify that the border changed (autostart line 10 set BORDER 2 = Red)
        Assert.Equal(2, machine.Ula.BorderColor);
    }
}
