namespace Spectrum.Core;

/// <summary>
/// Represents the Sinclair ZX Spectrum hardware bus interface for memory and I/O.
/// </summary>
public interface IBus
{
    /// <summary>
    /// Reads a byte from memory at the specified 16-bit address.
    /// Contention cycles should be applied if accessing contended RAM (0x4000-0x7FFF).
    /// </summary>
    byte Read(ushort address);

    /// <summary>
    /// Writes a byte to memory at the specified 16-bit address.
    /// Writes to ROM (0x0000-0x3FFF) are ignored.
    /// Contention cycles should be applied if accessing contended RAM.
    /// </summary>
    void Write(ushort address, byte value);

    /// <summary>
    /// Reads a byte from an I/O port.
    /// On ZX Spectrum, lower address bit A0 = 0 selects the ULA (port 0xFE).
    /// </summary>
    byte In(ushort port);

    /// <summary>
    /// Writes a byte to an I/O port.
    /// On ZX Spectrum, lower address bit A0 = 0 selects the ULA (port 0xFE).
    /// </summary>
    void Out(ushort port, byte value);

    /// <summary>
    /// Advances the system clock / T-state counter by the given number of cycles.
    /// </summary>
    void AddTStates(int tStates);
}
