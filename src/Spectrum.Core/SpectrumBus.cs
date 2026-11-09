namespace Spectrum.Core;

/// <summary>
/// Hardware bus coordinating the Z80 CPU, 16 KB ROM, 48 KB RAM,
/// Ferranti ULA display engine, 8x5 keyboard matrix, cassette tape player, and beeper.
/// </summary>
public sealed class SpectrumBus : IBus
{
    private readonly byte[] _rom = new byte[16384];
    public readonly byte[] Ram = new byte[49152]; // 0x4000 - 0xFFFF (48 KB)

    public FerrantiUla Ula { get; } = new();
    public SpectrumKeyboard Keyboard { get; } = new();
    public Beeper Beeper { get; } = new();
    public TapePlayer Tape { get; } = new();

    public Z80Cpu? Cpu { get; set; }

    public int FrameTStates { get; private set; }
    public long TotalFrames { get; private set; }

    public event Action? FrameCompleted;

    public SpectrumBus(byte[]? romData = null)
    {
        if (romData != null)
        {
            LoadRom(romData);
        }
    }

    public void LoadRom(byte[] romData)
    {
        if (romData.Length > _rom.Length)
            throw new ArgumentException("ROM image exceeds 16 KB", nameof(romData));

        Array.Copy(romData, _rom, Math.Min(romData.Length, _rom.Length));
    }

    public void Reset()
    {
        Array.Clear(Ram, 0, Ram.Length);
        FrameTStates = 0;
        TotalFrames = 0;
        Keyboard.Reset();
        Beeper.Clear();
        Tape.Stop();
        Ula.BorderColor = 7;
    }

    public byte Read(ushort address)
    {
        if (address < 0x4000)
        {
            return _rom[address];
        }

        // Lower 16 KB RAM is contended (0x4000 - 0x7FFF)
        if (address < 0x8000)
        {
            int contention = Ula.GetContentionDelay(FrameTStates);
            if (contention > 0)
            {
                AddTStates(contention);
            }
        }

        return Ram[address - 0x4000];
    }

    public void Write(ushort address, byte value)
    {
        // 0x0000 - 0x3FFF: ROM write protection (ignored)
        if (address < 0x4000)
        {
            return;
        }

        // Lower 16 KB RAM is contended (0x4000 - 0x7FFF)
        if (address < 0x8000)
        {
            int contention = Ula.GetContentionDelay(FrameTStates);
            if (contention > 0)
            {
                AddTStates(contention);
            }
        }

        Ram[address - 0x4000] = value;
    }

    public byte In(ushort port)
    {
        // On ZX Spectrum, any even port address (bit 0 = 0) selects the ULA (0xFE)
        if ((port & 0x01) == 0)
        {
            byte high = (byte)(port >> 8);
            byte keyBits = Keyboard.Read(high);

            bool ear = Tape.State == TapeState.Playing ? Tape.EarSignal : false;

            // Bits 0-4: Keyboard matrix
            // Bit 5: Always 1
            // Bit 6: EAR input (0 or 1)
            // Bit 7: Always 1
            byte result = (byte)((keyBits & 0x1F) | (ear ? 0x40 : 0x00) | 0xA0);
            return result;
        }

        // Floating bus fallback: unattached ports return 0xFF
        return 0xFF;
    }

    public void Out(ushort port, byte value)
    {
        if ((port & 0x01) == 0)
        {
            // Bit 0..2: Border colour
            Ula.BorderColor = (byte)(value & 0x07);

            // Bit 4: Beeper speaker state
            Beeper.SpeakerState = (value & 0x10) != 0;
        }
    }

    public void AddTStates(int tStates)
    {
        FrameTStates += tStates;
        Beeper.Step(tStates);
        Tape.Step(tStates);

        // Frame timing: 69,888 T-states per frame
        if (FrameTStates >= FerrantiUla.TStatesPerFrame)
        {
            FrameTStates -= FerrantiUla.TStatesPerFrame;
            TotalFrames++;

            // Trigger ULA 50 Hz frame interrupt
            Cpu?.RequestInterrupt();

            // Render current frame into ULA framebuffer
            Ula.RenderFrame(Ram);

            FrameCompleted?.Invoke();
        }
    }
}
