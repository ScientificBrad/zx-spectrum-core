namespace Spectrum.Diagnostics;

/// <summary>
/// Writes 32-bit ARGB framebuffer data to uncompressed Windows BMP files.
/// </summary>
public static class BmpWriter
{
    public static void SaveBmp(string path, uint[] framebuffer, int width, int height)
    {
        int rowBytes = width * 3;
        int padding = (4 - (rowBytes % 4)) % 4;
        int stride = rowBytes + padding;
        int imageSize = stride * height;
        int fileSize = 54 + imageSize;

        byte[] bmp = new byte[fileSize];

        // BMP Header
        bmp[0] = 0x42; // 'B'
        bmp[1] = 0x4D; // 'M'
        BitConverter.GetBytes(fileSize).CopyTo(bmp, 2);
        BitConverter.GetBytes(54).CopyTo(bmp, 10); // Offset to pixel data

        // DIB Header (BITMAPINFOHEADER)
        BitConverter.GetBytes(40).CopyTo(bmp, 14); // Header size
        BitConverter.GetBytes(width).CopyTo(bmp, 18);
        BitConverter.GetBytes(height).CopyTo(bmp, 22); // Positive height = bottom-up
        BitConverter.GetBytes((short)1).CopyTo(bmp, 26); // Planes
        BitConverter.GetBytes((short)24).CopyTo(bmp, 28); // 24 bpp (BGR)
        BitConverter.GetBytes(0).CopyTo(bmp, 30); // Compression (BI_RGB)
        BitConverter.GetBytes(imageSize).CopyTo(bmp, 34);

        // Pixel data (BMP is stored bottom-up)
        int dstOffset = 54;
        for (int y = height - 1; y >= 0; y--)
        {
            int srcRow = y * width;
            for (int x = 0; x < width; x++)
            {
                uint argb = framebuffer[srcRow + x];
                bmp[dstOffset++] = (byte)(argb & 0xFF);         // Blue
                bmp[dstOffset++] = (byte)((argb >> 8) & 0xFF);  // Green
                bmp[dstOffset++] = (byte)((argb >> 16) & 0xFF); // Red
            }
            for (int p = 0; p < padding; p++)
            {
                bmp[dstOffset++] = 0;
            }
        }

        string? dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        File.WriteAllBytes(path, bmp);
    }
}
