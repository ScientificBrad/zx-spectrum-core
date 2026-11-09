using Spectrum.Core;
using Xunit;

namespace Spectrum.Tests;

public class TestBus : IBus
{
    public readonly byte[] Memory = new byte[65536];
    public readonly List<(ushort Port, byte Value)> OutLog = new();
    public int TStates;

    public byte Read(ushort address) => Memory[address];
    public void Write(ushort address, byte value) => Memory[address] = value;
    public byte In(ushort port) => 0xFF;
    public void Out(ushort port, byte value) => OutLog.Add((port, value));
    public void AddTStates(int tStates) => TStates += tStates;
}

public class Z80CpuTests
{
    [Fact]
    public void Reset_InitialisesRegistersCorrectly()
    {
        var bus = new TestBus();
        var cpu = new Z80Cpu(bus);

        Assert.Equal(0x0000, cpu.PC);
        Assert.Equal(0xFFFF, cpu.SP);
        Assert.False(cpu.IFF1);
        Assert.False(cpu.IFF2);
        Assert.Equal(0, cpu.IM);
        Assert.False(cpu.Halted);
    }

    [Fact]
    public void Arithmetic_Add8_SetsFlagsAccurately()
    {
        var bus = new TestBus();
        var cpu = new Z80Cpu(bus);

        // LD A, 0x0F ; ADD A, 0x01
        bus.Memory[0] = 0x3E;
        bus.Memory[1] = 0x0F;
        bus.Memory[2] = 0xC6;
        bus.Memory[3] = 0x01;

        cpu.Step(); // LD A, 0x0F
        cpu.Step(); // ADD A, 0x01

        Assert.Equal(0x10, cpu.A);
        Assert.True((cpu.F & Z80Cpu.FlagH) != 0, "Half carry must be set");
        Assert.True((cpu.F & Z80Cpu.FlagZ) == 0, "Zero flag must not be set");
        Assert.True((cpu.F & Z80Cpu.FlagC) == 0, "Carry flag must not be set");
    }

    [Fact]
    public void Arithmetic_Sub8_SetsZeroAndSubtractFlags()
    {
        var bus = new TestBus();
        var cpu = new Z80Cpu(bus);

        // LD A, 0x42 ; SUB 0x42
        bus.Memory[0] = 0x3E;
        bus.Memory[1] = 0x42;
        bus.Memory[2] = 0xD6;
        bus.Memory[3] = 0x42;

        cpu.Step();
        cpu.Step();

        Assert.Equal(0, cpu.A);
        Assert.True((cpu.F & Z80Cpu.FlagZ) != 0, "Zero flag must be set");
        Assert.True((cpu.F & Z80Cpu.FlagN) != 0, "Subtract flag must be set");
    }

    [Fact]
    public void Branching_Djnz_LoopsUntilZero()
    {
        var bus = new TestBus();
        var cpu = new Z80Cpu(bus);

        // LD B, 3 ; DEC A ; DJNZ -2
        bus.Memory[0] = 0x06; bus.Memory[1] = 0x03; // LD B, 3
        bus.Memory[2] = 0x3D;                       // DEC A
        bus.Memory[3] = 0x10; bus.Memory[4] = 0xFD; // DJNZ -3 (points to DEC A)

        cpu.A = 10;
        cpu.Step(); // LD B, 3

        int count = 0;
        while (cpu.B > 0 && count++ < 20)
        {
            cpu.Step();
        }

        Assert.Equal(0, cpu.B);
        Assert.Equal(7, cpu.A); // Decremented 3 times
    }

    [Fact]
    public void Indexed_IX_Operations()
    {
        var bus = new TestBus();
        var cpu = new Z80Cpu(bus);

        // LD IX, 0x8000 ; LD (IX+4), 0x55 ; LD A, (IX+4)
        bus.Memory[0] = 0xDD; bus.Memory[1] = 0x21; bus.Memory[2] = 0x00; bus.Memory[3] = 0x80; // LD IX, 0x8000
        bus.Memory[4] = 0xDD; bus.Memory[5] = 0x36; bus.Memory[6] = 0x04; bus.Memory[7] = 0x55; // LD (IX+4), 0x55
        bus.Memory[8] = 0xDD; bus.Memory[9] = 0x7E; bus.Memory[10] = 0x04;                       // LD A, (IX+4)

        cpu.Step(); // LD IX
        Assert.Equal(0x8000, cpu.IX);

        cpu.Step(); // LD (IX+4), 0x55
        Assert.Equal(0x55, bus.Memory[0x8004]);

        cpu.Step(); // LD A, (IX+4)
        Assert.Equal(0x55, cpu.A);
    }

    [Fact]
    public void BitManipulation_CbOpcodes()
    {
        var bus = new TestBus();
        var cpu = new Z80Cpu(bus);

        // LD A, 0x01 ; RLC A
        bus.Memory[0] = 0x3E; bus.Memory[1] = 0x01; // LD A, 1
        bus.Memory[2] = 0xCB; bus.Memory[3] = 0x07; // RLC A

        cpu.Step();
        cpu.Step();

        Assert.Equal(0x02, cpu.A);
        Assert.True((cpu.F & Z80Cpu.FlagC) == 0);

        // SET 7, A
        bus.Memory[4] = 0xCB; bus.Memory[5] = 0xFF; // SET 7, A
        cpu.Step();
        Assert.Equal(0x82, cpu.A);

        // BIT 7, A
        bus.Memory[6] = 0xCB; bus.Memory[7] = 0x7F; // BIT 7, A
        cpu.Step();
        Assert.True((cpu.F & Z80Cpu.FlagZ) == 0, "Bit 7 is set so Z flag must be 0");
    }

    [Fact]
    public void Interrupt_Im1_PushesPcAndVectorsTo0038()
    {
        var bus = new TestBus();
        var cpu = new Z80Cpu(bus);

        cpu.SP = 0xFFFF;
        cpu.PC = 0x1234;
        cpu.IM = 1;
        cpu.IFF1 = true;
        cpu.IFF2 = true;

        cpu.RequestInterrupt();
        int tStates = cpu.Step();

        Assert.Equal(13, tStates);
        Assert.Equal(0x0038, cpu.PC);
        Assert.False(cpu.IFF1);

        // Verify PC was pushed to stack
        ushort poppedPc = (ushort)(bus.Memory[0xFFFD] | (bus.Memory[0xFFFE] << 8));
        Assert.Equal(0x1234, poppedPc);
    }
}
