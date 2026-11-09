using Spectrum.Core;

namespace Spectrum.Diagnostics;

/// <summary>
/// Execution tracer with circular or bounded logging capabilities.
/// Records PC, instruction disassembly, CPU registers, and flags without altering semantics.
/// </summary>
public sealed class ExecutionTracer
{
    private readonly List<string> _traceEntries = new();
    private readonly int _maxEntries;

    public ExecutionTracer(int maxEntries = 5000)
    {
        _maxEntries = maxEntries;
    }

    public void Trace(Z80Cpu cpu, Func<ushort, byte> memoryReader)
    {
        if (_traceEntries.Count >= _maxEntries)
        {
            _traceEntries.RemoveAt(0);
        }

        var (disasm, _) = Z80Disassembler.Disassemble(memoryReader, cpu.PC);

        string flags = $"{( (cpu.F & Z80Cpu.FlagS) != 0 ? 'S' : '-' )}" +
                       $"{( (cpu.F & Z80Cpu.FlagZ) != 0 ? 'Z' : '-' )}" +
                       $"{( (cpu.F & Z80Cpu.FlagY) != 0 ? '5' : '-' )}" +
                       $"{( (cpu.F & Z80Cpu.FlagH) != 0 ? 'H' : '-' )}" +
                       $"{( (cpu.F & Z80Cpu.FlagX) != 0 ? '3' : '-' )}" +
                       $"{( (cpu.F & Z80Cpu.FlagPV) != 0 ? 'V' : '-' )}" +
                       $"{( (cpu.F & Z80Cpu.FlagN) != 0 ? 'N' : '-' )}" +
                       $"{( (cpu.F & Z80Cpu.FlagC) != 0 ? 'C' : '-' )}";

        string entry = $"PC:{cpu.PC:X4} | {disasm,-18} | AF:{cpu.AF:X4} BC:{cpu.BC:X4} DE:{cpu.DE:X4} HL:{cpu.HL:X4} SP:{cpu.SP:X4} IX:{cpu.IX:X4} IY:{cpu.IY:X4} | [{flags}] | T:{cpu.TotalTStates}";
        _traceEntries.Add(entry);
    }

    public void ExportToFile(string path)
    {
        string? dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }
        File.WriteAllLines(path, _traceEntries);
    }

    public IReadOnlyList<string> Entries => _traceEntries;
}
