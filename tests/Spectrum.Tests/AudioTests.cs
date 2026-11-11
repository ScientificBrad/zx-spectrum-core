using System;
using Spectrum.Core;
using Xunit;

namespace Spectrum.Tests;

public class AudioTests
{
    [Fact]
    public void Beeper_Produces48kHzSamples_WhenTStatesAccumulate()
    {
        var beeper = new Beeper();
        beeper.Enabled = true;

        // 1 frame of Spectrum execution = 69,888 T-states (~20 ms)
        // At 48 kHz, 20 ms = 960 audio samples (69888 / 72.9167 ~= 958.46)
        beeper.Step(69888);

        Assert.True(beeper.AvailableSamples >= 950 && beeper.AvailableSamples <= 970,
            $"Expected ~960 samples for 69,888 T-states at 48 kHz, got {beeper.AvailableSamples}");
    }

    [Fact]
    public void Beeper_Mute_GeneratesZeroSamples()
    {
        var beeper = new Beeper();
        beeper.Enabled = true;
        beeper.Muted = true;
        beeper.SpeakerState = true;

        beeper.Step(7291); // ~100 samples
        int count = beeper.AvailableSamples;
        Assert.True(count > 0);

        short[] buffer = new short[count];
        int read = beeper.ReadSamples(buffer, 0, count);
        Assert.Equal(count, read);

        for (int i = 0; i < read; i++)
        {
            Assert.Equal(0, buffer[i]);
        }
    }

    [Fact]
    public void Beeper_VolumeControls_ScaleIndependently()
    {
        var beeper = new Beeper();
        beeper.Enabled = true;
        beeper.SpeakerState = true;
        beeper.BeeperVolume = 1.0f;
        beeper.TapeVolume = 0.0f;

        beeper.Step(100);
        short[] buf1 = new short[1];
        beeper.ReadSamples(buf1, 0, 1);
        short maxVol = buf1[0];

        beeper.BeeperVolume = 0.5f;
        beeper.Step(100);
        short[] buf2 = new short[1];
        beeper.ReadSamples(buf2, 0, 1);
        short halfVol = buf2[0];

        Assert.True(maxVol > 0);
        Assert.True(halfVol > 0);
        Assert.True(Math.Abs(maxVol - halfVol * 2) < 200, "Half volume should be approximately 50% amplitude");
    }

    [Fact]
    public void Beeper_TapeMonitoring_AlternatesWithEarPulses()
    {
        var beeper = new Beeper();
        beeper.Enabled = true;
        beeper.BeeperVolume = 0.0f;
        beeper.TapeVolume = 1.0f;

        // Simulate cassette pilot tone: 2168 T-states high, 2168 T-states low
        beeper.UpdateTapeSignal(isPlaying: true, earSignal: true);
        beeper.Step(2168);

        beeper.UpdateTapeSignal(isPlaying: true, earSignal: false);
        beeper.Step(2168);

        int count = beeper.AvailableSamples;
        Assert.True(count >= 50);

        short[] buffer = new short[count];
        beeper.ReadSamples(buffer, 0, count);

        // First half should be positive, second half should be negative
        Assert.True(buffer[5] > 0, $"First pulse should be positive, was {buffer[5]}");
        Assert.True(buffer[count - 5] < 0, $"Second pulse should be negative, was {buffer[count - 5]}");
    }

    [Fact]
    public void Beeper_ReadSamples_DrainsCircularBufferCorrectly()
    {
        var beeper = new Beeper();
        beeper.Enabled = true;

        beeper.Step(7291); // Generates ~100 samples
        int available = beeper.AvailableSamples;
        Assert.True(available > 0);

        short[] buf = new short[available];
        int read = beeper.ReadSamples(buf, 0, available);
        Assert.Equal(available, read);
        Assert.Equal(0, beeper.AvailableSamples);
    }
}
