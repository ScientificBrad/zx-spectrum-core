namespace Spectrum.Core;

/// <summary>
/// Reconstructs the Ferranti Uncommitted Logic Array (ULA 6C001E-6 / 7C112E-4)
/// Responsible for video generation, memory contention, border rendering,
/// frame synchronization, and port 0xFE I/O handling.
/// </summary>
public sealed class FerrantiUla
{
    public const int TStatesPerLine = 224;
    public const int TotalScanlines = 312;
    public const int TStatesPerFrame = TStatesPerLine * TotalScanlines; // 69,888

    public const int ScreenWidth = 256;
    public const int ScreenHeight = 192;

    public const int BorderWidth = 32;
    public const int BorderHeight = 24;

    public const int TotalWidth = ScreenWidth + (BorderWidth * 2);   // 320
    public const int TotalHeight = ScreenHeight + (BorderHeight * 2); // 240

    // Framebuffer in 32-bit ARGB format
    public readonly uint[] Framebuffer = new uint[TotalWidth * TotalHeight];

    // Standard Spectrum 16-colour palette (8 normal + 8 bright)
    public static readonly uint[] Palette = new uint[16]
    {
        // Normal colours (0-7)
        0xFF000000, // Black
        0xFF0000C8, // Blue
        0xFFC80000, // Red
        0xFFC800C8, // Magenta
        0xFF00C800, // Green
        0xFF00C8C8, // Cyan
        0xFFC8C800, // Yellow
        0xFFC8C8C8, // White

        // Bright colours (8-15)
        0xFF000000, // Bright Black
        0xFF0000FF, // Bright Blue
        0xFFFF0000, // Bright Red
        0xFFFF00FF, // Bright Magenta
        0xFF00FF00, // Bright Green
        0xFF00FFFF, // Bright Cyan
        0xFFFFFF00, // Bright Yellow
        0xFFFFFFFF  // Bright White
    };

    private byte _borderColor = 7; // Default White

    // Current border colour (0..7)
    public byte BorderColor
    {
        get => _borderColor;
        set
        {
            _borderColor = (byte)(value & 0x07);
            ResetFrameBorders(_borderColor);
        }
    }

    // Per-scanline border colours for authentic CRT tape stripes (312 PAL scanlines)
    private readonly byte[] _lineBorder = new byte[312];

    public void SetLineBorder(int line, byte color)
    {
        byte c = (byte)(color & 0x07);
        if (line >= 0 && line < 312)
        {
            for (int l = line; l < 312; l++)
            {
                _lineBorder[l] = c;
            }
        }
    }

    public void ResetFrameBorders(byte currentBorder)
    {
        Array.Fill(_lineBorder, (byte)(currentBorder & 0x07));
    }

    public bool VideoRenderingEnabled { get; set; } = true;

    // Flash state toggles every 16 frames (~320ms)
    private int _frameCounter;
    private bool _flashPhase;

    // Contention table for 8-T-state window: [6, 5, 4, 3, 2, 1, 0, 0]
    private static readonly int[] ContentionTable = { 6, 5, 4, 3, 2, 1, 0, 0 };

    /// <summary>
    /// Calculates memory contention delay for the specified frame T-state.
    /// Memory contention occurs when the Z80 accesses contended RAM (0x4000-0x7FFF)
    /// during active display rendering (scanlines 64 to 255, T-states 14 to 141 of each line).
    /// </summary>
    public int GetContentionDelay(int frameTState)
    {
        int line = frameTState / TStatesPerLine;
        int lineTState = frameTState % TStatesPerLine;

        // Active display lines are 64 to 255
        if (line >= 64 && line < 256)
        {
            // Pixel fetching happens between line T-state 14 and 141
            if (lineTState >= 14 && lineTState < 142)
            {
                int offset = lineTState - 14;
                return ContentionTable[offset % 8];
            }
        }
        return 0;
    }

    /// <summary>
    /// Renders a complete frame into the 320x240 ARGB framebuffer by reading
    /// real emulated RAM (0x4000-0x57FF for bitmap, 0x5800-0x5AFF for attributes).
    /// </summary>
    public void RenderFrame(byte[] ram)
    {
        _frameCounter++;
        if ((_frameCounter % 16) == 0)
        {
            _flashPhase = !_flashPhase;
        }

        // 1. Render Top Border (24 lines: PAL lines 40 to 63)
        int fbIndex = 0;
        for (int y = 0; y < BorderHeight; y++)
        {
            uint borderRgb = Palette[_lineBorder[40 + y] & 0x07];
            for (int x = 0; x < TotalWidth; x++)
            {
                Framebuffer[fbIndex++] = borderRgb;
            }
        }

        // 2. Render 192 Active Display lines with Left and Right Border (PAL lines 64 to 255)
        for (int y = 0; y < ScreenHeight; y++)
        {
            uint borderRgb = Palette[_lineBorder[64 + y] & 0x07];

            // Left border (32 pixels)
            for (int bx = 0; bx < BorderWidth; bx++)
            {
                Framebuffer[fbIndex++] = borderRgb;
            }

            // Calculate VRAM row address for line Y
            // Format: 0x4000 | ((Y & 0xC0) << 5) | ((Y & 0x07) << 8) | ((Y & 0x38) << 2)
            int rowOffset = ((y & 0xC0) << 5) | ((y & 0x07) << 8) | ((y & 0x38) << 2);
            int attrRowOffset = (y >> 3) * 32;

            for (int col = 0; col < 32; col++)
            {
                byte pixels = ram[rowOffset + col];
                byte attr = ram[0x1800 + attrRowOffset + col]; // 0x5800 - 0x4000 = 0x1800

                byte ink = (byte)(attr & 0x07);
                byte paper = (byte)((attr >> 3) & 0x07);
                bool bright = (attr & 0x40) != 0;
                bool flash = (attr & 0x80) != 0;

                if (bright)
                {
                    ink += 8;
                    paper += 8;
                }

                if (flash && _flashPhase)
                {
                    (ink, paper) = (paper, ink);
                }

                uint inkRgb = Palette[ink];
                uint paperRgb = Palette[paper];

                // Render 8 pixels of character column
                Framebuffer[fbIndex++] = (pixels & 0x80) != 0 ? inkRgb : paperRgb;
                Framebuffer[fbIndex++] = (pixels & 0x40) != 0 ? inkRgb : paperRgb;
                Framebuffer[fbIndex++] = (pixels & 0x20) != 0 ? inkRgb : paperRgb;
                Framebuffer[fbIndex++] = (pixels & 0x10) != 0 ? inkRgb : paperRgb;
                Framebuffer[fbIndex++] = (pixels & 0x08) != 0 ? inkRgb : paperRgb;
                Framebuffer[fbIndex++] = (pixels & 0x04) != 0 ? inkRgb : paperRgb;
                Framebuffer[fbIndex++] = (pixels & 0x02) != 0 ? inkRgb : paperRgb;
                Framebuffer[fbIndex++] = (pixels & 0x01) != 0 ? inkRgb : paperRgb;
            }

            // Right border (32 pixels)
            for (int bx = 0; bx < BorderWidth; bx++)
            {
                Framebuffer[fbIndex++] = borderRgb;
            }
        }

        // 3. Render Bottom Border (24 lines: PAL lines 256 to 279)
        for (int y = 0; y < BorderHeight; y++)
        {
            uint borderRgb = Palette[_lineBorder[256 + y] & 0x07];
            for (int x = 0; x < TotalWidth; x++)
            {
                Framebuffer[fbIndex++] = borderRgb;
            }
        }
    }
}
