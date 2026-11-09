using Spectrum.Core;
using Xunit;

namespace Spectrum.Tests;

public class TapeTests
{
    [Fact]
    public void TapFile_ParsesHeaderBlockCorrectly()
    {
        // Construct a synthetic 19-byte TAP block (17 payload bytes = header)
        byte[] raw = new byte[19];
        raw[0] = 0x00; // Flag: Header
        raw[1] = 0x00; // Type: Program
        // Filename: "TEST      " (10 bytes)
        byte[] name = System.Text.Encoding.ASCII.GetBytes("TEST      ");
        Array.Copy(name, 0, raw, 2, 10);
        // Data length: 500
        raw[12] = 0xF4; raw[13] = 0x01;
        // Autostart line: 10
        raw[14] = 0x0A; raw[15] = 0x00;
        // Variable length: 500
        raw[16] = 0xF4; raw[17] = 0x01;

        // XOR checksum
        byte xor = 0;
        for (int i = 0; i < 18; i++) xor ^= raw[i];
        raw[18] = xor;

        var block = new TapBlock(raw);

        Assert.True(block.IsHeader);
        Assert.True(block.ChecksumValid);
        Assert.Equal("TEST", block.HeaderFilename);
        Assert.Equal(500, block.DataLength);
        Assert.Equal(10, block.Parameter1);
    }

    [Fact]
    public void TapePlayer_GeneratesPilotAndSyncSignals()
    {
        byte[] raw = new byte[4];
        raw[0] = 0x00; // Flag
        raw[1] = 0x42; // Data byte
        raw[2] = 0x55; // Data byte
        raw[3] = (byte)(0x00 ^ 0x42 ^ 0x55); // Checksum

        var tap = new TapFile();
        tap.Blocks.Add(new TapBlock(raw));

        var player = new TapePlayer();
        player.InsertTape(tap);
        player.Play();

        Assert.Equal(TapeState.Playing, player.State);

        // Step through one pilot pulse (2168 T-states)
        bool initialEar = player.EarSignal;
        player.Step(2168);
        Assert.NotEqual(initialEar, player.EarSignal);
    }
}
