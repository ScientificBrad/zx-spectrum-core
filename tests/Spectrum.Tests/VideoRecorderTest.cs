using System;
using System.Diagnostics;
using System.IO;
using Spectrum.Core;
using Xunit;

namespace Spectrum.Tests;

public class VideoRecorderTest
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

    private static string FindTapPath(string name)
    {
        string[] candidates = {
            $"games/{name}",
            $"../games/{name}",
            $"../../games/{name}",
            $"../../../games/{name}",
            $"../../../../games/{name}",
            $"../../../../../games/{name}",
            $"D:/Emulation/Spectrum ZX/games/{name}",
            $"D:/Emulation/ZXSpectrum/games/{name}"
        };
        foreach (var path in candidates)
        {
            if (File.Exists(path)) return path;
        }
        return $"games/{name}";
    }

    [Fact]
    public void GenerateLoadingVideoWithAuthenticAudio()
    {
        string romFile = FindRomPath();
        string tapPath = FindTapPath("SAMANTHA.TAP");
        if (!File.Exists(romFile) || !File.Exists(tapPath)) return;

        byte[] rom = File.ReadAllBytes(romFile);
        var tap = TapFile.Load(tapPath);
        var machine = new SpectrumMachine(rom);
        machine.Reset();

        machine.Ula.VideoRenderingEnabled = true;
        machine.Beeper.Enabled = true;
        machine.Beeper.BeeperVolume = 0.5f;
        machine.Beeper.TapeVolume = 0.8f; // Clear, authentic cassette screech

        string tempDir = Path.GetTempPath();
        string videoRaw = Path.Combine(tempDir, "spectrum_video.raw");
        string audioRaw = Path.Combine(tempDir, "spectrum_audio.raw");
        string outputMp4 = "D:/Emulation/Spectrum ZX/screenshots/spectrum_loading.mp4";

        if (File.Exists(videoRaw)) File.Delete(videoRaw);
        if (File.Exists(audioRaw)) File.Delete(audioRaw);

        using (var videoStream = new FileStream(videoRaw, FileMode.Create, FileAccess.Write, FileShare.None, 65536))
        using (var audioStream = new FileStream(audioRaw, FileMode.Create, FileAccess.Write, FileShare.None, 65536))
        using (var videoWriter = new BinaryWriter(videoStream))
        using (var audioWriter = new BinaryWriter(audioStream))
        {
            byte[] frameBytes = new byte[320 * 240 * 4];
            short[] sampleBuffer = new short[4096];

            void CaptureFrame()
            {
                // Copy Ula Framebuffer (uint ARGB / BGRA in little-endian byte order)
                Buffer.BlockCopy(machine.Ula.Framebuffer, 0, frameBytes, 0, frameBytes.Length);
                videoWriter.Write(frameBytes);

                // Drain audio samples
                int avail = machine.Beeper.AvailableSamples;
                while (avail > 0)
                {
                    int toRead = Math.Min(avail, sampleBuffer.Length);
                    int read = machine.Beeper.ReadSamples(sampleBuffer, 0, toRead);
                    for (int i = 0; i < read; i++)
                    {
                        audioWriter.Write(sampleBuffer[i]);
                    }
                    avail -= read;
                }
            }

            // 1. Cold boot (40 frames = 0.8 seconds)
            for (int f = 0; f < 40; f++)
            {
                machine.RunFrames(1);
                CaptureFrame();
            }

            // 2. Clear boot screen (press ENTER)
            machine.PressChar('\r');
            for (int f = 0; f < 10; f++) { machine.RunFrames(1); CaptureFrame(); }
            machine.ReleaseAllKeys();
            for (int f = 0; f < 10; f++) { machine.RunFrames(1); CaptureFrame(); }

            // 3. Type LOAD "" (J, "", "")
            machine.PressChar('J');
            for (int f = 0; f < 8; f++) { machine.RunFrames(1); CaptureFrame(); }
            machine.ReleaseAllKeys();
            for (int f = 0; f < 8; f++) { machine.RunFrames(1); CaptureFrame(); }

            machine.PressChar('"');
            for (int f = 0; f < 8; f++) { machine.RunFrames(1); CaptureFrame(); }
            machine.ReleaseAllKeys();
            for (int f = 0; f < 8; f++) { machine.RunFrames(1); CaptureFrame(); }

            machine.PressChar('"');
            for (int f = 0; f < 8; f++) { machine.RunFrames(1); CaptureFrame(); }
            machine.ReleaseAllKeys();
            for (int f = 0; f < 8; f++) { machine.RunFrames(1); CaptureFrame(); }

            // 4. Insert tape and press ENTER
            machine.Tape.InsertTape(tap);
            machine.PressChar('\r');
            for (int f = 0; f < 10; f++) { machine.RunFrames(1); CaptureFrame(); }
            machine.ReleaseAllKeys();
            for (int f = 0; f < 20; f++) { machine.RunFrames(1); CaptureFrame(); }

            // 5. Start tape playback
            machine.Tape.Play();

            // 6. Record authentic loading sequence:
            // ~1,100 frames (~22 seconds of authentic 50 FPS loading)
            // Shows:
            // - Header pilot tone with flashing red/cyan border stripes
            // - "Program: FOX" printed to screen
            // - BASIC loader data block transferring with authentic screech
            // - BASIC executes: sets BORDER 0, prints "THE PROGRAM WILL START WHEN CODE 10 WILL BE LOADED"
            // - Header for "0": pilot tone + "Bytes: 0" printed to screen
            // - High speed data transfer of 6,912 bytes with audio chirps
            for (int f = 0; f < 1100; f++)
            {
                machine.RunFrames(1);
                CaptureFrame();
            }
        }

        // 7. Use ffmpeg to encode raw video + audio into crisp MP4
        string ffmpegArgs = $"-y -f rawvideo -pix_fmt bgra -video_size 320x240 -framerate 50 -i \"{videoRaw}\" " +
                            $"-f s16le -ar 48000 -ac 1 -i \"{audioRaw}\" " +
                            $"-vf \"scale=640:480:flags=neighbor\" " +
                            $"-c:v libx264 -preset fast -crf 20 -pix_fmt yuv420p " +
                            $"-c:a aac -b:a 192k \"{outputMp4}\"";

        var psi = new ProcessStartInfo("ffmpeg", ffmpegArgs)
        {
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var proc = Process.Start(psi)!;
        proc.WaitForExit(30000);

        // Clean up temporary raw files
        if (File.Exists(videoRaw)) File.Delete(videoRaw);
        if (File.Exists(audioRaw)) File.Delete(audioRaw);

        Assert.True(File.Exists(outputMp4), "Output MP4 video file must exist");
        var fi = new FileInfo(outputMp4);
        Assert.True(fi.Length > 100000, $"Video file must be non-trivial size (actual {fi.Length} bytes)");
    }
}
