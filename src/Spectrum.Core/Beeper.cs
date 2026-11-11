namespace Spectrum.Core;

/// <summary>
/// Audio sound synthesizer for Sinclair ZX Spectrum 48K.
/// Synthesizes 48 kHz 16-bit PCM mono audio by mixing:
/// 1. Original 1-bit beeper speaker output from Port 0xFE bit 4.
/// 2. Authentic cassette tape monitoring audio from EAR loading pulses.
/// Provides independent volume controls, mute option, and crackle-free lock-free ring buffering.
/// </summary>
public sealed class Beeper
{
    public const int SampleRate = 48000;
    public const int ClockFrequency = 3500000; // 3.5 MHz
    private const double TStatesPerSample = (double)ClockFrequency / SampleRate; // ~72.9167

    private const int BufferCapacity = 16384; // Power of two for mask indexing
    private const int BufferMask = BufferCapacity - 1;
    private readonly short[] _ringBuffer = new short[BufferCapacity];
    private volatile int _writePos;
    private volatile int _readPos;

    private bool _speakerState;
    private bool _tapeSignal;
    private bool _tapePlaying;
    private double _sampleCounter;

    public bool SpeakerState
    {
        get => _speakerState;
        set => _speakerState = value;
    }

    public float BeeperVolume { get; set; } = 0.5f;
    public float TapeVolume { get; set; } = 0.5f;
    public bool Muted { get; set; } = false;
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets the number of generated audio samples currently waiting in the circular ring buffer.
    /// </summary>
    public int AvailableSamples => _writePos - _readPos;

    /// <summary>
    /// Updates the tape monitoring signal state.
    /// </summary>
    public void UpdateTapeSignal(bool isPlaying, bool earSignal)
    {
        _tapePlaying = isPlaying;
        _tapeSignal = earSignal;
    }

    /// <summary>
    /// Updates audio synthesis by the elapsed number of T-states.
    /// Samples speaker and cassette signals at 48 kHz.
    /// </summary>
    public void Step(int tStates)
    {
        if (!Enabled)
            return;

        _sampleCounter += tStates;
        while (_sampleCounter >= TStatesPerSample)
        {
            _sampleCounter -= TStatesPerSample;

            if (Muted)
            {
                WriteSample(0);
                continue;
            }

            // Beeper contributes between -1.0 and +1.0
            float beeperContribution = (_speakerState ? 1.0f : -1.0f) * BeeperVolume;

            // Cassette EAR signal contributes when tape is playing
            float tapeContribution = 0.0f;
            if (_tapePlaying)
            {
                tapeContribution = (_tapeSignal ? 1.0f : -1.0f) * TapeVolume;
            }

            // Mix channels and scale to 16-bit PCM amplitude
            float mixed = (beeperContribution + tapeContribution) * 0.5f;
            short sample = (short)Math.Clamp(mixed * 24000.0f, -32768, 32767);

            WriteSample(sample);
        }
    }

    private void WriteSample(short sample)
    {
        int write = _writePos;
        int read = _readPos;
        // Avoid overrun if audio reader is slow or stopped
        if (write - read < BufferCapacity)
        {
            _ringBuffer[write & BufferMask] = sample;
            _writePos = write + 1;
        }
    }

    /// <summary>
    /// Reads available audio samples into the destination buffer without locks.
    /// Returns the number of samples read.
    /// </summary>
    public int ReadSamples(short[] buffer, int offset, int count)
    {
        int available = _writePos - _readPos;
        if (available <= 0)
            return 0;

        int toRead = Math.Min(count, available);
        int read = _readPos;
        for (int i = 0; i < toRead; i++)
        {
            buffer[offset + i] = _ringBuffer[(read + i) & BufferMask];
        }
        _readPos = read + toRead;
        return toRead;
    }

    public void Clear()
    {
        _writePos = 0;
        _readPos = 0;
        _sampleCounter = 0;
    }
}
