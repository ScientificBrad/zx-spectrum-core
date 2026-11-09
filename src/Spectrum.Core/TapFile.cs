namespace Spectrum.Core;

public enum TapBlockType
{
    Program = 0,
    NumberArray = 1,
    CharArray = 2,
    Bytes = 3,
    Data = 255
}

/// <summary>
/// Represents a single TAP block parsed from a ZX Spectrum .tap image.
/// </summary>
public sealed class TapBlock
{
    public ushort Length { get; }
    public byte Flag { get; }
    public byte[] Data { get; }
    public byte Checksum { get; }
    public bool ChecksumValid { get; }

    // Header-specific decoded properties
    public bool IsHeader => Flag == 0x00 && Data.Length == 17;
    public TapBlockType HeaderType { get; }
    public string HeaderFilename { get; } = "";
    public ushort DataLength { get; }
    public ushort Parameter1 { get; } // Autostart line or start address
    public ushort Parameter2 { get; } // Variable area size

    public TapBlock(byte[] rawBlock)
    {
        if (rawBlock.Length < 2)
            throw new ArgumentException("TAP block too small", nameof(rawBlock));

        Length = (ushort)rawBlock.Length;
        Flag = rawBlock[0];
        Data = new byte[rawBlock.Length - 2];
        Array.Copy(rawBlock, 1, Data, 0, Data.Length);
        Checksum = rawBlock[^1];

        // Compute XOR checksum
        byte xor = Flag;
        for (int i = 0; i < Data.Length; i++)
        {
            xor ^= Data[i];
        }
        ChecksumValid = (xor == Checksum);

        if (IsHeader)
        {
            HeaderType = (TapBlockType)Data[0];
            char[] nameChars = new char[10];
            for (int i = 0; i < 10; i++)
            {
                byte c = Data[1 + i];
                nameChars[i] = (c >= 32 && c <= 126) ? (char)c : ' ';
            }
            HeaderFilename = new string(nameChars).TrimEnd();
            DataLength = (ushort)(Data[11] | (Data[12] << 8));
            Parameter1 = (ushort)(Data[13] | (Data[14] << 8));
            Parameter2 = (ushort)(Data[15] | (Data[16] << 8));
        }
        else
        {
            HeaderType = TapBlockType.Data;
        }
    }
}

/// <summary>
/// Parser and loader for standard ZX Spectrum .tap image files.
/// </summary>
public sealed class TapFile
{
    public List<TapBlock> Blocks { get; } = new();

    public static TapFile Load(string path)
    {
        byte[] bytes = File.ReadAllBytes(path);
        return LoadFromBytes(bytes);
    }

    public static TapFile LoadFromBytes(byte[] bytes)
    {
        var tap = new TapFile();
        int offset = 0;

        while (offset + 2 <= bytes.Length)
        {
            ushort blockLen = (ushort)(bytes[offset] | (bytes[offset + 1] << 8));
            offset += 2;

            if (offset + blockLen > bytes.Length)
                break; // Truncated block

            byte[] blockData = new byte[blockLen];
            Array.Copy(bytes, offset, blockData, 0, blockLen);
            offset += blockLen;

            tap.Blocks.Add(new TapBlock(blockData));
        }

        return tap;
    }
}
