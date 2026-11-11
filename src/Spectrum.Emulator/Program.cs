using System.Security.Cryptography;
using Spectrum.Core;
using Spectrum.Diagnostics;

namespace Spectrum.Emulator;

public static class Program
{
    private const string DefaultRom = "roms/48.rom";
    private const string ExpectedSha1 = "5ea7c2b824672e914525d1d5c419d71b84a426a2";
    private const string ExpectedMd5 = "4c42a2f075212361c3117015b107ff68";

    [STAThread]
    public static int Main(string[] args)
    {
        Console.WriteLine("===================================================================");
        Console.WriteLine("   RESX - ZX Spectrum 48K Emulator (REA Project 004)");
        Console.WriteLine("   Hardware Reconstruction & Cycle-Accurate Emulation Core");
        Console.WriteLine("   Researcher: ScientificBrad | Host: C# / .NET 8 x64");
        Console.WriteLine("===================================================================\n");

        string romPath = DefaultRom;
        string? tapePath = null;
        string? screenshotPath = null;
        bool launchWindow = true;
        int frameCount = 0;

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i].ToLowerInvariant())
            {
                case "--rom":
                    if (i + 1 < args.Length) romPath = args[++i];
                    break;
                case "--tape":
                    if (i + 1 < args.Length) tapePath = args[++i];
                    break;
                case "--screenshot":
                    if (i + 1 < args.Length) screenshotPath = args[++i];
                    break;
                case "--frames":
                    if (i + 1 < args.Length && int.TryParse(args[++i], out int f)) frameCount = f;
                    break;
                case "--window":
                    launchWindow = true;
                    break;
                case "--headless":
                    launchWindow = false;
                    break;
                case "--help":
                case "-h":
                    PrintUsage();
                    return 0;
            }
        }

        // Validate ROM
        if (!File.Exists(romPath))
        {
            // Also check alternate locations relative to AppContext.BaseDirectory and current working directory
            string baseDir = AppContext.BaseDirectory;
            string[] alternates = {
                Path.Combine(baseDir, "roms", "48.rom"),
                Path.Combine(baseDir, "48.rom"),
                Path.Combine(baseDir, "..", "..", "..", "roms", "48.rom"),
                "roms/48.rom",
                "Rom/48.rom",
                "../roms/48.rom",
                "../../roms/48.rom",
                "../../../roms/48.rom"
            };
            foreach (var alt in alternates)
            {
                if (File.Exists(alt))
                {
                    romPath = alt;
                    break;
                }
            }
        }

        if (!File.Exists(romPath))
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"[FATAL] ROM image not found at '{romPath}'.");
            Console.WriteLine("Please place the genuine Sinclair ZX Spectrum 48K ROM (48.rom, 16384 bytes) in 'roms/48.rom'.");
            Console.ResetColor();
            return 1;
        }

        byte[] romBytes = File.ReadAllBytes(romPath);
        if (romBytes.Length != 16384)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"[FATAL] Invalid ROM size: {romBytes.Length} bytes. Expected 16,384 bytes.");
            Console.ResetColor();
            return 2;
        }

        using var sha1 = SHA1.Create();
        string actualSha1 = Convert.ToHexString(sha1.ComputeHash(romBytes)).ToLowerInvariant();
        using var md5 = MD5.Create();
        string actualMd5 = Convert.ToHexString(md5.ComputeHash(romBytes)).ToLowerInvariant();

        Console.WriteLine($"[ROM] Loaded: {romPath} (16,384 bytes)");
        Console.WriteLine($"[ROM] SHA-1:  {actualSha1} {(actualSha1 == ExpectedSha1 ? "(VERIFIED)" : "(NON-STANDARD)")}");
        Console.WriteLine($"[ROM] MD5:    {actualMd5} {(actualMd5 == ExpectedMd5 ? "(VERIFIED)" : "(NON-STANDARD)")}\n");

        var machine = new SpectrumMachine(romBytes);
        machine.Reset();
        Console.WriteLine("[EMU] Virtual hardware initialized: Z80A CPU, Ferranti ULA, 48 KB RAM.");

        if (!string.IsNullOrEmpty(tapePath) && File.Exists(tapePath))
        {
            try
            {
                var tap = TapFile.Load(tapePath);
                machine.Tape.InsertTape(tap);
                Console.WriteLine($"[TAPE] Inserted '{Path.GetFileName(tapePath)}' ({tap.Blocks.Count} blocks).");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[TAPE ERROR] Could not load tape: {ex.Message}");
            }
        }

        if (launchWindow)
        {
            Console.WriteLine("[GUI] Starting Windows Forms display window (640x480 double-buffered 50 Hz)...");
            ApplicationConfiguration.Initialize();
            Application.Run(new SpectrumWindow(machine));
        }
        else
        {
            // Headless execution mode
            int runFrames = frameCount > 0 ? frameCount : 120;
            Console.WriteLine($"[HEADLESS] Executing {runFrames} frames (approx {runFrames / 50.0:F2}s virtual time)...");
            var sw = System.Diagnostics.Stopwatch.StartNew();
            machine.RunFrames(runFrames);
            sw.Stop();
            Console.WriteLine($"[HEADLESS] Completed {runFrames} frames in {sw.ElapsedMilliseconds} ms ({(runFrames / sw.Elapsed.TotalSeconds):F1} emulated FPS).");

            if (!string.IsNullOrEmpty(screenshotPath))
            {
                BmpWriter.SaveBmp(screenshotPath, machine.Ula.Framebuffer, FerrantiUla.TotalWidth, FerrantiUla.TotalHeight);
                Console.WriteLine($"[SCREENSHOT] Saved framebuffer to '{screenshotPath}'.");
            }
        }

        return 0;
    }

    private static void PrintUsage()
    {
        Console.WriteLine("Usage: Spectrum.Emulator [options]");
        Console.WriteLine("Options:");
        Console.WriteLine("  --rom <path>          Path to 16 KB ZX Spectrum 48K ROM (default: roms/48.rom)");
        Console.WriteLine("  --tape <path>         Path to .tap cassette tape image");
        Console.WriteLine("  --window              Launch native Windows GUI window (default)");
        Console.WriteLine("  --headless            Run without GUI window for batch testing / benchmarks");
        Console.WriteLine("  --frames <count>      Number of frames to execute in headless mode (default: 120)");
        Console.WriteLine("  --screenshot <path>   Save final framebuffer to uncompressed BMP file");
        Console.WriteLine("  --help, -h            Show this help information");
    }
}
