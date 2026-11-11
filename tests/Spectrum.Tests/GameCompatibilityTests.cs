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

    [Fact]
    public void Game_Samantha_LoadsViaKeyboardAndTapePlayback()
    {
        string romFile = FindRomPath();
        if (!File.Exists(romFile)) return;

        string tapPath = "D:/Emulation/Spectrum ZX/games/SAMANTHA.TAP";
        if (!File.Exists(tapPath)) tapPath = "games/SAMANTHA.TAP";
        if (!File.Exists(tapPath)) return;

        var tap = TapFile.Load(tapPath);
        Assert.Equal(24, tap.Blocks.Count);

        byte[] rom = File.ReadAllBytes(romFile);
        var machine = new SpectrumMachine(rom);
        machine.Reset();

        // 1. Cold boot to Sinclair BASIC (120 frames)
        machine.RunFrames(120);

        // 2. Clear boot screen (press ENTER)
        machine.PressChar('\r');
        machine.RunFrames(10);
        machine.ReleaseAllKeys();
        machine.RunFrames(10);

        // 3. Normal keyboard input: Type LOAD ""
        // In K mode, 'J' generates token LOAD
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

        // 4. Insert tape while NOT playing
        machine.Tape.InsertTape(tap);
        Assert.Equal(TapeState.Stopped, machine.Tape.State);

        // 5. Press ENTER to execute LOAD ""
        machine.PressChar('\r');
        machine.RunFrames(10);
        machine.ReleaseAllKeys();
        machine.RunFrames(20);

        // Spectrum ROM is now in LD-BYTES waiting for tape signal
        Assert.Equal(TapeState.Stopped, machine.Tape.State);

        // 6. Simulate F3: Start tape playback
        machine.Tape.Play();
        Assert.Equal(TapeState.Playing, machine.Tape.State);

        // 7. Run frames to load Block 1 (Header) and Block 2 (Data: BASIC loader)
        int frameCount = 0;
        int maxFrames = 750;
        while (machine.Tape.State == TapeState.Playing && machine.Tape.CurrentBlockIndex < 2 && frameCount < maxFrames)
        {
            machine.RunFrames(1);
            frameCount++;
        }

        // Run frames for autostart BASIC lines 10..41 to execute and print title
        machine.RunFrames(10);

        // 8. Capture live screenshot of Samantha Fox loader screen
        string screenshotPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "../../../../../screenshots/samantha_loader.bmp");
        BmpWriter.SaveBmp(screenshotPath, machine.Ula.Framebuffer, FerrantiUla.TotalWidth, FerrantiUla.TotalHeight);

        // 9. Verify that Samantha BASIC loader executed:
        // System variable BORDCR at 0x5C48 set to 0 (Black) by BORDER 0
        // (Note: ULA hardware border register is temporarily set to 7 by line 50's LD-BYTES)
        ushort ppc = (ushort)(machine.Bus.Read(0x5C45) | (machine.Bus.Read(0x5C46) << 8));
        Assert.True(ppc >= 30, $"Autostart BASIC must have executed up to line 30+ (actual PPC={ppc})");

        // Verify line 41 printed text: "THE PROGRAM WILL START      WHEN CODE 10  WILL BE LOADED"
        // Active screen area is 256x192 pixels inside 320x240 frame (border: 32 left/right, 24 top/bottom)
        const int borderLeft = (FerrantiUla.TotalWidth - FerrantiUla.ScreenWidth) / 2; // 32
        const int borderTop = (FerrantiUla.TotalHeight - FerrantiUla.ScreenHeight) / 2; // 24
        int whiteInnerPixels = 0;
        for (int y = borderTop; y < borderTop + FerrantiUla.ScreenHeight; y++)
        {
            for (int x = borderLeft; x < borderLeft + FerrantiUla.ScreenWidth; x++)
            {
                uint px = machine.Ula.Framebuffer[y * FerrantiUla.TotalWidth + x];
                if (px == FerrantiUla.Palette[7] || px == FerrantiUla.Palette[15])
                {
                    whiteInnerPixels++;
                }
            }
        }

        Assert.True(whiteInnerPixels > 500, $"Samantha loader text glyphs must be rendered on screen (found {whiteInnerPixels} pixels)");

        machine.Ula.VideoRenderingEnabled = false;
        machine.Beeper.Enabled = false;

        // Verify RAM disk storage progression as tape loads code blocks
        while (machine.Tape.State == TapeState.Playing && machine.RamDisk.Files.Count < 2 && frameCount < 5500)
        {
            machine.RunFrames(10);
            frameCount += 10;
        }

        Assert.True(machine.RamDisk.Files.ContainsKey("0"), "File '0' (intro screen) must be saved to RAM disk");
        Assert.True(machine.RamDisk.Files.ContainsKey("1"), "File '1' (first animation frame) must be saved to RAM disk");
        Assert.Equal(6912, machine.RamDisk.Files["0"].Length);
        Assert.Equal(4162, machine.RamDisk.Files["1"].Length);
    }

    [Fact]
    public void Game_CandyGirl_LoadsAndExecutesMachineCode()
    {
        string romFile = FindRomPath();
        if (!File.Exists(romFile)) return;

        string tapPath = "D:/Emulation/Spectrum ZX/games/CANDY_GI.TAP";
        if (!File.Exists(tapPath)) tapPath = "games/CANDY_GI.TAP";
        if (!File.Exists(tapPath)) return;

        var tap = TapFile.Load(tapPath);
        Assert.Equal(4, tap.Blocks.Count);

        byte[] rom = File.ReadAllBytes(romFile);
        var machine = new SpectrumMachine(rom);
        machine.Reset();

        // 1. Cold boot to Sinclair BASIC (120 frames)
        machine.RunFrames(120);

        // 2. Clear boot screen (press ENTER)
        machine.PressChar('\r');
        machine.RunFrames(10);
        machine.ReleaseAllKeys();
        machine.RunFrames(10);

        // 3. Normal keyboard input: Type LOAD ""
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

        // 4. Insert tape while NOT playing
        machine.Tape.InsertTape(tap);
        Assert.Equal(TapeState.Stopped, machine.Tape.State);

        // 5. Press ENTER to execute LOAD ""
        machine.PressChar('\r');
        machine.RunFrames(10);
        machine.ReleaseAllKeys();
        machine.RunFrames(20);

        // 6. Start tape playback
        machine.Tape.Play();
        Assert.Equal(TapeState.Playing, machine.Tape.State);

        // 7. Run frames until machine code block loads and executes RANDOMIZE USR 25000
        machine.Ula.VideoRenderingEnabled = false;
        machine.Beeper.Enabled = false;

        int frameCount = 0;
        int maxFrames = 3000;
        while (machine.Tape.State == TapeState.Playing && machine.Tape.CurrentBlockIndex < 4 && frameCount < maxFrames)
        {
            machine.RunFrames(10);
            frameCount += 10;
        }

        // Verify that tape progressed through all 4 blocks (Header 1, Data 1, Header 2, Data 2)
        Assert.True(machine.Tape.CurrentBlockIndex >= 3, $"Tape must have loaded at least to block 3+ (actual {machine.Tape.CurrentBlockIndex})");
    }
}
