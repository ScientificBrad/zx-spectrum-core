namespace Spectrum.Core;

/// <summary>
/// 1-bit audio sound generator for Sinclair ZX Spectrum 48K.
/// Accumulates speaker state changes from Port 0xFE bit 4 and generates
/// PCM audio samples for real-time sound playback without clicks.
/// </summary>
public sealed class Beeper
{
    public const int SampleRate = 44100;
    public const int ClockFrequency = 3500000; // 3.5 MHz
    private const double TStatesPerSample = (double)ClockFrequency / SampleRate;

    private bool _speakerState;
    private double _sampleCounter;
    private readonly Queue<short> _audioBuffer = new();
    private readonly object _lock = new();

    public bool SpeakerState
    {
        get => _speakerState;
        set => _speakerState = value;
    }

    /// <summary>
    /// Updates the beeper by the elapsed number of T-states.
    /// Converts 1-bit speaker state into 16-bit PCM samples.
    /// </summary>
    public void Step(int tStates)
    {
        _sampleCounter += tStates;
        while (_sampleCounter >= TStatesPerSample)
        {
            _sampleCounter -= TStatesPerSample;
            short sample = _speakerState ? (short)12000 : (short)-12000;
            lock (_lock)
            {
                if (_audioBuffer.Count < SampleRate * 2) // Cap at 2 seconds
                {
                    _audioBuffer.Enqueue(sample);
                }
            }
        }
    }

    /// <summary>
    /// Reads available audio samples into the destination buffer.
    /// Returns the number of samples read.
    /// </summary>
    public int ReadSamples(short[] buffer, int offset, int count)
    {
        lock (_lock)
        {
            int read = 0;
            while (read < count && _audioBuffer.Count > 0)
            {
                buffer[offset + read] = _audioBuffer.Dequeue();
                read++;
            }
            return read;
        }
    }

    public void Clear()
    {
        lock (_lock)
        {
            _audioBuffer.Clear();
            _sampleCounter = 0;
        }
    }
}
