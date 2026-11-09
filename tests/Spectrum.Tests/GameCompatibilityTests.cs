using Spectrum.Core;
using Spectrum.Diagnostics;
using Xunit;

namespace Spectrum.Tests;

public class GameCompatibilityTests
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
    public void Game_Matrix_LoadsAndRunsSuccessfully()
    {
        string romFile = FindRomPath();
        if (!File.Exists(romFile)) return;

        string tapPath = "D:/Emulation/Spectrum ZX/games/matrix.tap";
        if (!File.Exists(tapPath)) tapPath = "games/matrix.tap";
        if (!File.Exists(tapPath)) return;

        var tap = TapFile.Load(tapPath);
        Assert.Equal(2, tap.Blocks.Count);

        byte[] rom = File.ReadAllBytes(romFile);
        var machine = new SpectrumMachine(rom);
        machine.Reset();

        // 1. Cold boot to Sinclair BASIC (120 frames)
        machine.RunFrames(120);

        // 2. Clear boot copyright message
        machine.PressChar('\r');
        machine.RunFrames(10);
        machine.ReleaseAllKeys();
        machine.RunFrames(10);

        // 3. Type LOAD ""
        machine.PressChar('J');
        machine.RunFrames(6);
        machine.ReleaseAllKeys();
        machine.RunFrames(6);

        machine.PressChar('"');
        machine.RunFrames(6);
        machine.ReleaseAllKeys();
        machine.RunFrames(6);

        machine.PressChar('"');
        machine.RunFrames(6);
        machine.ReleaseAllKeys();
        machine.RunFrames(6);

        // 4. Insert tape and press Enter
        machine.Tape.InsertTape(tap);
        machine.PressChar('\r');
        machine.RunFrames(10);
        machine.ReleaseAllKeys();

        // 5. Play tape through authentic ROM LD-BYTES
        machine.Tape.Play();

        int maxFrames = 1000;
        int frameCount = 0;
        while (machine.Tape.State == TapeState.Playing && frameCount < maxFrames)
        {
            machine.RunFrames(10);
            frameCount += 10;
        }

        // 6. Run frames of game execution (Matrix rain rendering)
        machine.RunFrames(120);

        // 7. Save verification screenshot
        string screenshotPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "../../../../../screenshots/matrix_game.bmp");
        BmpWriter.SaveBmp(screenshotPath, machine.Ula.Framebuffer, FerrantiUla.TotalWidth, FerrantiUla.TotalHeight);

        // 8. Assert border set to Black (0) by BORDER 0 statement
        Assert.Equal(0, machine.Ula.BorderColor);

        // 9. Assert green ink pixels (Matrix digital rain) rendered
        bool hasGreenInk = false;
        for (int i = 0; i < machine.Ula.Framebuffer.Length; i++)
        {
            uint px = machine.Ula.Framebuffer[i];
            if (px == FerrantiUla.Palette[4] || px == FerrantiUla.Palette[12])
            {
                hasGreenInk = true;
                break;
            }
        }

        Assert.True(hasGreenInk, "Matrix rain with green ink must be visible in framebuffer");
    }

    [Fact]
    public void Game_LunarLander_LoadsAndPlaysSuccessfully()
    {
        string romFile = FindRomPath();
        if (!File.Exists(romFile)) return;

        string tapPath = "D:/Emulation/Spectrum ZX/games/lander.tap";
        if (!File.Exists(tapPath)) tapPath = "games/lander.tap";
        if (!File.Exists(tapPath)) return;

        var tap = TapFile.Load(tapPath);
        Assert.Equal(2, tap.Blocks.Count);

        byte[] rom = File.ReadAllBytes(romFile);
        var machine = new SpectrumMachine(rom);
        machine.Reset();

        // 1. Cold boot to Sinclair BASIC (120 frames)
        machine.RunFrames(120);

        // 2. Clear boot screen
        machine.PressChar('\r');
        machine.RunFrames(10);
        machine.ReleaseAllKeys();
        machine.RunFrames(10);

        // 3. Type LOAD ""
        machine.PressChar('J');
        machine.RunFrames(6);
        machine.ReleaseAllKeys();
        machine.RunFrames(6);

        machine.PressChar('"');
        machine.RunFrames(6);
        machine.ReleaseAllKeys();
        machine.RunFrames(6);

        machine.PressChar('"');
        machine.RunFrames(6);
        machine.ReleaseAllKeys();
        machine.RunFrames(6);

        // 4. Insert tape and press Enter
        machine.Tape.InsertTape(tap);
        machine.PressChar('\r');
        machine.RunFrames(10);
        machine.ReleaseAllKeys();

        // 5. Play tape through authentic ROM LD-BYTES
        machine.Tape.Play();

        int maxFrames = 750;
        int frameCount = 0;
        while (machine.Tape.State == TapeState.Playing && frameCount < maxFrames)
        {
            machine.RunFrames(10);
            frameCount += 10;
        }

        // 6. Run frames for autostart line 10 to execute and render game
        machine.RunFrames(40);

        // 7. Inject player input ('A' to fire thrusters, 'P' to steer right)
        machine.PressChar('A');
        machine.RunFrames(15);
        machine.ReleaseAllKeys();
        machine.PressChar('P');
        machine.RunFrames(10);
        machine.ReleaseAllKeys();
        machine.RunFrames(20);

        // 8. Capture live screenshot
        string screenshotPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "../../../../../screenshots/lunar_lander_game.bmp");
        BmpWriter.SaveBmp(screenshotPath, machine.Ula.Framebuffer, FerrantiUla.TotalWidth, FerrantiUla.TotalHeight);

        // 9. Assert game title, pad, and lander graphics are rendered in framebuffer
        bool hasGamePixels = false;
        for (int i = 0; i < machine.Ula.Framebuffer.Length; i++)
        {
            uint px = machine.Ula.Framebuffer[i];
            if (px == FerrantiUla.Palette[6] || px == FerrantiUla.Palette[4] || px == FerrantiUla.Palette[5])
            {
                hasGamePixels = true;
                break;
            }
        }

        Assert.True(hasGamePixels, "Game title, pad, and lander graphics must be rendered in framebuffer");
    }
}
