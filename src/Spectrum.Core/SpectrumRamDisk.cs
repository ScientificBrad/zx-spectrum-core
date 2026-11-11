namespace Spectrum.Core;

/// <summary>
/// Emulates the ZX Spectrum expansion bus peripheral RAM disk interface (Interface 1 / 128K Silicon Disc).
/// Traps RST 08 (0x0008) when the Sinclair BASIC interpreter throws 'C Nonsense in BASIC' (error 11)
/// on encountering '!' commands (SAVE !, LOAD !, CAT !, ERASE !).
/// Handles named memory storage without modifying or patching original ROM code.
/// </summary>
public sealed class SpectrumRamDisk
{
    private readonly Dictionary<string, byte[]> _storage = new(StringComparer.OrdinalIgnoreCase);

    public bool Enabled { get; set; } = true;
    public IReadOnlyDictionary<string, byte[]> Files => _storage;

    public void Clear()
    {
        _storage.Clear();
    }

    /// <summary>
    /// Checks if the CPU is at RST 08 with an error caused by a '!' command, and executes it.
    /// Returns true if intercepted and handled.
    /// </summary>
    public bool TryIntercept(SpectrumBus bus, Z80Cpu cpu)
    {
        if (!Enabled || cpu.PC != 0x0008)
            return false;

        ushort sp = cpu.SP;
        ushort rst08Caller = (ushort)(bus.Read(sp) | (bus.Read((ushort)(sp + 1)) << 8));

        // In 48K Sinclair ROM, 0x1C8A is REPORT-C (RST 08; DEFB 0x0B). Return address is 0x1C8B.
        if (rst08Caller != 0x1C8B)
            return false;

        byte errorCode = bus.Read(rst08Caller);
        if (errorCode != 11) // 11 = Error 'C' (Nonsense in BASIC)
            return false;

        // Read system variable CH_ADD (0x5C5D)
        ushort chAdd = (ushort)(bus.Read(0x5C5D) | (bus.Read(0x5C5E) << 8));
        if (bus.Read(chAdd) != (byte)'!')
            return false;

        // Parse the RAM disk command
        ushort ptr = (ushort)(chAdd + 1);
        while (bus.Read(ptr) == (byte)' ') ptr++;

        // Read filename: either quoted literal or string variable
        string filename = "";
        byte first = bus.Read(ptr);
        if (first == (byte)'"')
        {
            ptr++;
            int qGuard = 0;
            while (ptr < 0xFFFF && bus.Read(ptr) != (byte)'"' && bus.Read(ptr) != 0x0D && qGuard++ < 100)
            {
                filename += (char)bus.Read(ptr);
                ptr++;
            }
            if (bus.Read(ptr) == (byte)'"') ptr++;
        }
        else if ((first >= (byte)'A' && first <= (byte)'Z') || (first >= (byte)'a' && first <= (byte)'z'))
        {
            char varName = (char)first;
            ptr++;
            if (bus.Read(ptr) == (byte)'$') ptr++;

            // Lookup string variable in Sinclair VARS area (0x5C4B) bounded by E_LINE (0x5C59)
            ushort varsAddr = (ushort)(bus.Read(0x5C4B) | (bus.Read(0x5C4C) << 8));
            ushort eLine = (ushort)(bus.Read(0x5C59) | (bus.Read(0x5C5A) << 8));
            if (eLine < varsAddr || eLine > 0xFF00) eLine = 0xFF00;

            while (varsAddr < eLine)
            {
                byte vType = bus.Read(varsAddr);
                if (vType == 0x80) break; // End of VARS marker

                // Bits 5-7 identify variable class:
                // 010 (0x40): Simple string variable (1 byte name, 2 bytes length, characters)
                if ((vType & 0xE0) == 0x40)
                {
                    ushort vLen = (ushort)(bus.Read((ushort)(varsAddr + 1)) | (bus.Read((ushort)(varsAddr + 2)) << 8));
                    if ((char)((vType & 0x1F) + 'A' - 1) == char.ToUpperInvariant(varName))
                    {
                        char[] chars = new char[vLen];
                        for (int i = 0; i < vLen; i++)
                        {
                            chars[i] = (char)bus.Read((ushort)(varsAddr + 3 + i));
                        }
                        filename = new string(chars);
                        break;
                    }
                    varsAddr += (ushort)(3 + vLen);
                }
                // 011 (0x60): Simple numeric variable (1 byte name, 5 bytes float)
                else if ((vType & 0xE0) == 0x60)
                {
                    varsAddr += 6;
                }
                // 111 (0xE0): FOR loop control variable (1 byte name, 5 val, 5 limit, 5 step, 2 line, 1 stmt = 19 bytes)
                else if ((vType & 0xE0) == 0xE0)
                {
                    varsAddr += 19;
                }
                // 100 (0x80) or 110 (0xC0): Number array or character array (1 byte name, 2 bytes length, data)
                else if ((vType & 0xE0) == 0x80 || (vType & 0xE0) == 0xC0)
                {
                    ushort aLen = (ushort)(bus.Read((ushort)(varsAddr + 1)) | (bus.Read((ushort)(varsAddr + 2)) << 8));
                    varsAddr += (ushort)(3 + aLen);
                }
                // Multi-character numeric variable (letters ending with bit 7 set, 5 bytes float)
                else if ((vType & 0xE0) < 0x40)
                {
                    varsAddr++;
                    while (varsAddr < eLine && (bus.Read(varsAddr) & 0x80) == 0) varsAddr++;
                    varsAddr += 6;
                }
                else
                {
                    varsAddr++;
                }
            }
        }

        while (bus.Read(ptr) == (byte)' ') ptr++;

        // Determine if command is SAVE or LOAD:
        ushort scan = (ushort)(chAdd - 1);
        while (scan > 0x4000 && bus.Read(scan) == (byte)' ') scan--;
        byte cmdToken = bus.Read(scan);
        bool isSave = (cmdToken == 0xF8); // 0xF8 = SAVE token

        byte token = bus.Read(ptr);
        if (token == 0xAA) // SCREEN$
        {
            ptr++;
            if (!isSave && _storage.TryGetValue(filename, out var screenData) && screenData != null)
            {
                for (int i = 0; i < Math.Min(screenData.Length, 6912); i++)
                {
                    bus.Write((ushort)(0x4000 + i), screenData[i]);
                }
            }
        }
        else if (token == 0xAF) // CODE
        {
            ptr++;
            while (bus.Read(ptr) == (byte)' ') ptr++;

            int start = 0;
            while (bus.Read(ptr) >= (byte)'0' && bus.Read(ptr) <= (byte)'9')
            {
                start = start * 10 + (bus.Read(ptr) - (byte)'0');
                ptr++;
            }
            if (bus.Read(ptr) == 0x0E) ptr += 6; // skip Sinclair 5-byte number
            while (bus.Read(ptr) == (byte)' ' || bus.Read(ptr) == (byte)',') ptr++;

            int length = 0;
            while (bus.Read(ptr) >= (byte)'0' && bus.Read(ptr) <= (byte)'9')
            {
                length = length * 10 + (bus.Read(ptr) - (byte)'0');
                ptr++;
            }
            if (bus.Read(ptr) == 0x0E) ptr += 6; // skip Sinclair 5-byte number

            if (isSave)
            {
                byte[] data = new byte[length];
                for (int i = 0; i < length; i++)
                {
                    data[i] = bus.Read((ushort)(start + i));
                }
                _storage[filename] = data;
            }
            else
            {
                if (_storage.TryGetValue(filename, out var data) && data != null)
                {
                    for (int i = 0; i < Math.Min(length, data.Length); i++)
                    {
                        bus.Write((ushort)(start + i), data[i]);
                    }
                }
            }
        }

        // Advance CH_ADD to the end of statement (colon or 0x0D Enter)
        int sGuard = 0;
        while (bus.Read(ptr) != 0x0D && bus.Read(ptr) != (byte)':' && sGuard++ < 300)
        {
            ptr++;
        }

        bus.Write(0x5C5D, (byte)(ptr & 0xFF));
        bus.Write(0x5C5E, (byte)(ptr >> 8));

        // Resume Sinclair ROM execution at 0x1BF4 (STMT-RET) with SP popped back to statement caller
        cpu.PC = 0x1BF4;
        cpu.SP = (ushort)(sp + 10);
        return true;
    }
}
