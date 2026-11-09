namespace Spectrum.Core;

/// <summary>
/// Models the 8-row x 5-column keyboard matrix of the Sinclair ZX Spectrum 48K.
/// Keys are active-low and read via I/O port 0xFE with upper address lines A8-A15.
/// </summary>
public sealed class SpectrumKeyboard
{
    // 8 half-rows, 5 bits each (bits 0..4, active low: 0 = pressed, 1 = unpressed)
    // Default is 0x1F (all 5 keys unpressed)
    private readonly byte[] _rows = new byte[8];

    public SpectrumKeyboard()
    {
        Reset();
    }

    public void Reset()
    {
        for (int i = 0; i < 8; i++)
        {
            _rows[i] = 0x1F;
        }
    }

    /// <summary>
    /// Updates the state of an individual key.
    /// </summary>
    public void SetKey(SpectrumKey key, bool pressed)
    {
        int row = (int)key >> 3;
        int bit = (int)key & 0x07;
        if (row >= 0 && row < 8 && bit >= 0 && bit < 5)
        {
            if (pressed)
                _rows[row] &= (byte)~(1 << bit); // 0 = pressed
            else
                _rows[row] |= (byte)(1 << bit);  // 1 = released
        }
    }

    /// <summary>
    /// Reads the keyboard matrix lines based on the upper address byte (port >> 8).
    /// Multiple row lines are combined via bitwise AND.
    /// </summary>
    public byte Read(byte highAddress)
    {
        byte result = 0x1F;

        // A8 low -> Row 0 (SHIFT, Z, X, C, V)
        if ((highAddress & 0x01) == 0) result &= _rows[0];
        // A9 low -> Row 1 (A, S, D, F, G)
        if ((highAddress & 0x02) == 0) result &= _rows[1];
        // A10 low -> Row 2 (Q, W, E, R, T)
        if ((highAddress & 0x04) == 0) result &= _rows[2];
        // A11 low -> Row 3 (1, 2, 3, 4, 5)
        if ((highAddress & 0x08) == 0) result &= _rows[3];
        // A12 low -> Row 4 (0, 9, 8, 7, 6)
        if ((highAddress & 0x10) == 0) result &= _rows[4];
        // A13 low -> Row 5 (P, O, I, U, Y)
        if ((highAddress & 0x20) == 0) result &= _rows[5];
        // A14 low -> Row 6 (ENTER, L, K, J, H)
        if ((highAddress & 0x40) == 0) result &= _rows[6];
        // A15 low -> Row 7 (SPACE, SYMBOL SHIFT, M, N, B)
        if ((highAddress & 0x80) == 0) result &= _rows[7];

        return result;
    }
}

/// <summary>
/// Spectrum key enumeration encoded as (row << 3) | bit.
/// </summary>
public enum SpectrumKey
{
    // Row 0 (A8)
    CapsShift = (0 << 3) | 0,
    Z         = (0 << 3) | 1,
    X         = (0 << 3) | 2,
    C         = (0 << 3) | 3,
    V         = (0 << 3) | 4,

    // Row 1 (A9)
    A         = (1 << 3) | 0,
    S         = (1 << 3) | 1,
    D         = (1 << 3) | 2,
    F         = (1 << 3) | 3,
    G         = (1 << 3) | 4,

    // Row 2 (A10)
    Q         = (2 << 3) | 0,
    W         = (2 << 3) | 1,
    E         = (2 << 3) | 2,
    R         = (2 << 3) | 3,
    T         = (2 << 3) | 4,

    // Row 3 (A11)
    D1        = (3 << 3) | 0,
    D2        = (3 << 3) | 1,
    D3        = (3 << 3) | 2,
    D4        = (3 << 3) | 3,
    D5        = (3 << 3) | 4,

    // Row 4 (A12)
    D0        = (4 << 3) | 0,
    D9        = (4 << 3) | 1,
    D8        = (4 << 3) | 2,
    D7        = (4 << 3) | 3,
    D6        = (4 << 3) | 4,

    // Row 5 (A13)
    P         = (5 << 3) | 0,
    O         = (5 << 3) | 1,
    I         = (5 << 3) | 2,
    U         = (5 << 3) | 3,
    Y         = (5 << 3) | 4,

    // Row 6 (A14)
    Enter     = (6 << 3) | 0,
    L         = (6 << 3) | 1,
    K         = (6 << 3) | 2,
    J         = (6 << 3) | 3,
    H         = (6 << 3) | 4,

    // Row 7 (A15)
    Space       = (7 << 3) | 0,
    SymbolShift = (7 << 3) | 1,
    M           = (7 << 3) | 2,
    N           = (7 << 3) | 3,
    B           = (7 << 3) | 4
}
