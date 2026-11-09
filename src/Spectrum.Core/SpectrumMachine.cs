namespace Spectrum.Core;

/// <summary>
/// Top-level coordinator for the Sinclair ZX Spectrum 48K hardware emulation.
/// Encapsulates the Z80A CPU, memory bus, Ferranti ULA, keyboard, tape drive, and beeper.
/// </summary>
public sealed class SpectrumMachine
{
    public SpectrumBus Bus { get; }
    public Z80Cpu Cpu { get; }
    public FerrantiUla Ula => Bus.Ula;
    public SpectrumKeyboard Keyboard => Bus.Keyboard;
    public Beeper Beeper => Bus.Beeper;
    public TapePlayer Tape => Bus.Tape;

    public bool IsRomLoaded { get; private set; }

    public SpectrumMachine(byte[]? romData = null)
    {
        Bus = new SpectrumBus(romData);
        Cpu = new Z80Cpu(Bus);
        Bus.Cpu = Cpu;

        if (romData != null)
        {
            IsRomLoaded = true;
        }
    }

    public void LoadRom(byte[] romData)
    {
        Bus.LoadRom(romData);
        IsRomLoaded = true;
        Reset();
    }

    public void LoadRomFile(string path)
    {
        byte[] rom = File.ReadAllBytes(path);
        LoadRom(rom);
    }

    public void Reset()
    {
        Bus.Reset();
        Cpu.Reset();
    }

    /// <summary>
    /// Executes a single CPU instruction and returns the T-states consumed.
    /// </summary>
    public int Step()
    {
        return Cpu.Step();
    }

    /// <summary>
    /// Executes until the specified number of 50 Hz frames have completed.
    /// </summary>
    public void RunFrames(int frameCount)
    {
        long targetFrames = Bus.TotalFrames + frameCount;
        while (Bus.TotalFrames < targetFrames)
        {
            Cpu.Step();
        }
    }

    /// <summary>
    /// Executes until the specified number of T-states have elapsed.
    /// </summary>
    public void RunTStates(long tStates)
    {
        long targetTStates = Cpu.TotalTStates + tStates;
        while (Cpu.TotalTStates < targetTStates)
        {
            Cpu.Step();
        }
    }

    /// <summary>
    /// Maps a standard character to Spectrum matrix keys and applies it.
    /// Supports letters, digits, ENTER, SPACE, and symbols.
    /// </summary>
    public void PressChar(char c)
    {
        switch (char.ToUpperInvariant(c))
        {
            case 'A': Keyboard.SetKey(SpectrumKey.A, true); break;
            case 'B': Keyboard.SetKey(SpectrumKey.B, true); break;
            case 'C': Keyboard.SetKey(SpectrumKey.C, true); break;
            case 'D': Keyboard.SetKey(SpectrumKey.D, true); break;
            case 'E': Keyboard.SetKey(SpectrumKey.E, true); break;
            case 'F': Keyboard.SetKey(SpectrumKey.F, true); break;
            case 'G': Keyboard.SetKey(SpectrumKey.G, true); break;
            case 'H': Keyboard.SetKey(SpectrumKey.H, true); break;
            case 'I': Keyboard.SetKey(SpectrumKey.I, true); break;
            case 'J': Keyboard.SetKey(SpectrumKey.J, true); break;
            case 'K': Keyboard.SetKey(SpectrumKey.K, true); break;
            case 'L': Keyboard.SetKey(SpectrumKey.L, true); break;
            case 'M': Keyboard.SetKey(SpectrumKey.M, true); break;
            case 'N': Keyboard.SetKey(SpectrumKey.N, true); break;
            case 'O': Keyboard.SetKey(SpectrumKey.O, true); break;
            case 'P': Keyboard.SetKey(SpectrumKey.P, true); break;
            case 'Q': Keyboard.SetKey(SpectrumKey.Q, true); break;
            case 'R': Keyboard.SetKey(SpectrumKey.R, true); break;
            case 'S': Keyboard.SetKey(SpectrumKey.S, true); break;
            case 'T': Keyboard.SetKey(SpectrumKey.T, true); break;
            case 'U': Keyboard.SetKey(SpectrumKey.U, true); break;
            case 'V': Keyboard.SetKey(SpectrumKey.V, true); break;
            case 'W': Keyboard.SetKey(SpectrumKey.W, true); break;
            case 'X': Keyboard.SetKey(SpectrumKey.X, true); break;
            case 'Y': Keyboard.SetKey(SpectrumKey.Y, true); break;
            case 'Z': Keyboard.SetKey(SpectrumKey.Z, true); break;

            case '0': Keyboard.SetKey(SpectrumKey.D0, true); break;
            case '1': Keyboard.SetKey(SpectrumKey.D1, true); break;
            case '2': Keyboard.SetKey(SpectrumKey.D2, true); break;
            case '3': Keyboard.SetKey(SpectrumKey.D3, true); break;
            case '4': Keyboard.SetKey(SpectrumKey.D4, true); break;
            case '5': Keyboard.SetKey(SpectrumKey.D5, true); break;
            case '6': Keyboard.SetKey(SpectrumKey.D6, true); break;
            case '7': Keyboard.SetKey(SpectrumKey.D7, true); break;
            case '8': Keyboard.SetKey(SpectrumKey.D8, true); break;
            case '9': Keyboard.SetKey(SpectrumKey.D9, true); break;

            case ' ': Keyboard.SetKey(SpectrumKey.Space, true); break;
            case '\n':
            case '\r': Keyboard.SetKey(SpectrumKey.Enter, true); break;

            // Symbols with Symbol Shift:
            case '"':
                Keyboard.SetKey(SpectrumKey.SymbolShift, true);
                Keyboard.SetKey(SpectrumKey.P, true);
                break;
            case ':':
                Keyboard.SetKey(SpectrumKey.SymbolShift, true);
                Keyboard.SetKey(SpectrumKey.Z, true);
                break;
            case ';':
                Keyboard.SetKey(SpectrumKey.SymbolShift, true);
                Keyboard.SetKey(SpectrumKey.O, true);
                break;
            case '=':
                Keyboard.SetKey(SpectrumKey.SymbolShift, true);
                Keyboard.SetKey(SpectrumKey.L, true);
                break;
            case '+':
                Keyboard.SetKey(SpectrumKey.SymbolShift, true);
                Keyboard.SetKey(SpectrumKey.K, true);
                break;
            case '-':
                Keyboard.SetKey(SpectrumKey.SymbolShift, true);
                Keyboard.SetKey(SpectrumKey.J, true);
                break;
            case '*':
                Keyboard.SetKey(SpectrumKey.SymbolShift, true);
                Keyboard.SetKey(SpectrumKey.B, true);
                break;
            case '/':
                Keyboard.SetKey(SpectrumKey.SymbolShift, true);
                Keyboard.SetKey(SpectrumKey.V, true);
                break;
            case '(':
                Keyboard.SetKey(SpectrumKey.SymbolShift, true);
                Keyboard.SetKey(SpectrumKey.I, true);
                break;
            case ')':
                Keyboard.SetKey(SpectrumKey.SymbolShift, true);
                Keyboard.SetKey(SpectrumKey.U, true);
                break;
        }
    }

    public void ReleaseAllKeys()
    {
        Keyboard.Reset();
    }
}
