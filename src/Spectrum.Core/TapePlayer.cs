namespace Spectrum.Core;

public enum TapeState
{
    Stopped,
    Playing,
    Paused
}

/// <summary>
/// Cycle-accurate cassette signal generator.
/// Synthesizes exact physical pulse timings on the EAR input bit
/// allowing genuine Sinclair ROM loading routines (LD-BYTES at 0x0556)
/// to read software without binary injection or traps.
/// </summary>
public sealed class TapePlayer
{
    private TapFile? _tape;
    private int _blockIndex;
    private int _byteIndex;
    private int _bitIndex;
    private int _pulseCount;
    private int _pulseTarget;
    private int _currentPulseDuration;
    private int _tStateCounter;
    private bool _earSignal;

    private enum Phase
    {
        Pilot,
        Sync1,
        Sync2,
        DataHalf1,
        DataHalf2,
        Tail,
        Pause
    }

    private Phase _phase;
    private byte[] _currentBlockRaw = Array.Empty<byte>();

    public TapeState State { get; private set; } = TapeState.Stopped;
    public TapFile? CurrentTape => _tape;
    public bool EarSignal => _earSignal;
    public int CurrentBlockIndex => _blockIndex;
    public int TotalBlocks => _tape?.Blocks.Count ?? 0;

    public void AdvanceBlock()
    {
        _blockIndex++;
        PrepareBlock();
        if (_tape == null || _blockIndex >= _tape.Blocks.Count)
        {
            State = TapeState.Stopped;
        }
    }

    public void InsertTape(TapFile tape)
    {
        _tape = tape;
        Rewind();
    }

    public void Play()
    {
        if (_tape != null && _tape.Blocks.Count > 0)
        {
            State = TapeState.Playing;
            if (_phase == Phase.Pause && _blockIndex >= _tape.Blocks.Count)
            {
                Rewind();
            }
        }
    }

    public void Pause()
    {
        if (State == TapeState.Playing)
            State = TapeState.Paused;
    }

    public void Stop()
    {
        State = TapeState.Stopped;
        _earSignal = false;
    }

    public void Rewind()
    {
        _blockIndex = 0;
        PrepareBlock();
        State = TapeState.Stopped;
        _earSignal = false;
    }

    private void PrepareBlock()
    {
        if (_tape == null || _blockIndex >= _tape.Blocks.Count)
        {
            _currentBlockRaw = Array.Empty<byte>();
            State = TapeState.Stopped;
            return;
        }

        var block = _tape.Blocks[_blockIndex];
        // Reassemble raw block (flag + data + checksum)
        _currentBlockRaw = new byte[block.Data.Length + 2];
        _currentBlockRaw[0] = block.Flag;
        Array.Copy(block.Data, 0, _currentBlockRaw, 1, block.Data.Length);
        _currentBlockRaw[^1] = block.Checksum;

        _byteIndex = 0;
        _bitIndex = 7;
        _pulseCount = 0;
        // Pilot pulse count: 8063 for header (flag=0), 3223 for data (flag!=0)
        _pulseTarget = (block.Flag == 0x00) ? 8063 : 3223;
        _phase = Phase.Pilot;
        _currentPulseDuration = 2168; // Pilot tone pulse duration in T-states
        _tStateCounter = 0;
        _earSignal = false;
    }

    /// <summary>
    /// Advances tape signal synthesis by the given number of T-states.
    /// </summary>
    public void Step(int tStates)
    {
        if (State != TapeState.Playing || _currentBlockRaw.Length == 0)
            return;

        _tStateCounter += tStates;

        while (_tStateCounter >= _currentPulseDuration)
        {
            _tStateCounter -= _currentPulseDuration;
            _earSignal = !_earSignal;

            AdvancePhase();
            if (State != TapeState.Playing)
                break;
        }
    }

    private void AdvancePhase()
    {
        switch (_phase)
        {
            case Phase.Pilot:
                _pulseCount++;
                if (_pulseCount >= _pulseTarget)
                {
                    _phase = Phase.Sync1;
                    _currentPulseDuration = 667; // Sync 1 duration
                }
                break;

            case Phase.Sync1:
                _phase = Phase.Sync2;
                _currentPulseDuration = 735; // Sync 2 duration
                break;

            case Phase.Sync2:
                _phase = Phase.DataHalf1;
                SetDataPulseDuration();
                break;

            case Phase.DataHalf1:
                _phase = Phase.DataHalf2;
                // Keep same duration for second half of bit
                break;

            case Phase.DataHalf2:
                _bitIndex--;
                if (_bitIndex < 0)
                {
                    _bitIndex = 7;
                    _byteIndex++;
                    if (_byteIndex >= _currentBlockRaw.Length)
                    {
                        // Emit a tail pulse to close the final bit measurement
                        _phase = Phase.Tail;
                        _currentPulseDuration = 1000;
                        return;
                    }
                }
                _phase = Phase.DataHalf1;
                SetDataPulseDuration();
                break;

            case Phase.Tail:
                // Block complete: 1 second trailing pause (3,500,000 T-states)
                _phase = Phase.Pause;
                _currentPulseDuration = 3500000;
                _blockIndex++;
                break;

            case Phase.Pause:
                // Move to next block
                if (_blockIndex < (_tape?.Blocks.Count ?? 0))
                {
                    PrepareBlock();
                }
                else
                {
                    State = TapeState.Stopped;
                }
                break;
        }
    }

    private void SetDataPulseDuration()
    {
        byte currentByte = _currentBlockRaw[_byteIndex];
        bool bit = ((currentByte >> _bitIndex) & 1) != 0;
        // '0' bit: 855 T-states; '1' bit: 1710 T-states
        _currentPulseDuration = bit ? 1710 : 855;
    }
}
