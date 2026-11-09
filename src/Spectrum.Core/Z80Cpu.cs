namespace Spectrum.Core;

/// <summary>
/// Clean-room cycle-accurate Zilog Z80A microprocessor implementation.
/// Supports full documented and undocumented instruction sets, flag calculations,
/// DD/FD index prefix chaining, DDCB/FDCB bit manipulation, and interrupt modes.
/// </summary>
public sealed class Z80Cpu
{
    // Main 8-bit registers
    public byte A;
    public byte F;
    public byte B;
    public byte C;
    public byte D;
    public byte E;
    public byte H;
    public byte L;

    // Alternate 8-bit registers
    public byte AAlt;
    public byte FAlt;
    public byte BAlt;
    public byte CAlt;
    public byte DAlt;
    public byte EAlt;
    public byte HAlt;
    public byte LAlt;

    // 16-bit Index & Pointer registers
    public ushort IX;
    public ushort IY;
    public ushort SP;
    public ushort PC;

    // Special registers
    public byte I;
    public byte R;

    // Interrupt state
    public bool IFF1;
    public bool IFF2;
    public byte IM; // 0, 1, or 2
    public bool Halted;
    public bool EiDelay;
    public bool InterruptPending;

    // Cycle tracking
    public long TotalTStates;

    // Flag constants
    public const byte FlagC = 0x01;  // Carry
    public const byte FlagN = 0x02;  // Add/Subtract
    public const byte FlagPV = 0x04; // Parity / Overflow
    public const byte FlagX = 0x08;  // Undocumented Bit 3 (XF)
    public const byte FlagH = 0x10;  // Half Carry
    public const byte FlagY = 0x20;  // Undocumented Bit 5 (YF)
    public const byte FlagZ = 0x40;  // Zero
    public const byte FlagS = 0x80;  // Sign

    // 16-bit Register Pair Properties
    public ushort AF
    {
        get => (ushort)((A << 8) | F);
        set { A = (byte)(value >> 8); F = (byte)value; }
    }

    public ushort BC
    {
        get => (ushort)((B << 8) | C);
        set { B = (byte)(value >> 8); C = (byte)value; }
    }

    public ushort DE
    {
        get => (ushort)((D << 8) | E);
        set { D = (byte)(value >> 8); E = (byte)value; }
    }

    public ushort HL
    {
        get => (ushort)((H << 8) | L);
        set { H = (byte)(value >> 8); L = (byte)value; }
    }

    public ushort AFAlt
    {
        get => (ushort)((AAlt << 8) | FAlt);
        set { AAlt = (byte)(value >> 8); FAlt = (byte)value; }
    }

    public ushort BCAlt
    {
        get => (ushort)((BAlt << 8) | CAlt);
        set { BAlt = (byte)(value >> 8); CAlt = (byte)value; }
    }

    public ushort DEAlt
    {
        get => (ushort)((DAlt << 8) | EAlt);
        set { DAlt = (byte)(value >> 8); EAlt = (byte)value; }
    }

    public ushort HLAlt
    {
        get => (ushort)((HAlt << 8) | LAlt);
        set { HAlt = (byte)(value >> 8); LAlt = (byte)value; }
    }

    // Bus connection
    private readonly IBus _bus;

    // Precalculated Flag Tables for fast, cycle-accurate flag generation
    private static readonly byte[] Sz53Table = new byte[256];
    private static readonly byte[] ParityTable = new byte[256];

    static Z80Cpu()
    {
        for (int i = 0; i < 256; i++)
        {
            byte f = 0;
            if ((i & 0x80) != 0) f |= FlagS;
            if (i == 0) f |= FlagZ;
            f |= (byte)(i & (FlagX | FlagY)); // Bits 3 and 5 copy
            Sz53Table[i] = f;

            int bits = 0;
            for (int b = 0; b < 8; b++)
            {
                if ((i & (1 << b)) != 0) bits++;
            }
            ParityTable[i] = (bits % 2 == 0) ? FlagPV : (byte)0;
        }
    }

    public Z80Cpu(IBus bus)
    {
        _bus = bus;
        Reset();
    }

    public void Reset()
    {
        PC = 0x0000;
        SP = 0xFFFF;
        AF = 0xFFFF;
        BC = 0xFFFF;
        DE = 0xFFFF;
        HL = 0xFFFF;
        AFAlt = 0xFFFF;
        BCAlt = 0xFFFF;
        DEAlt = 0xFFFF;
        HLAlt = 0xFFFF;
        IX = 0xFFFF;
        IY = 0xFFFF;
        I = 0x00;
        R = 0x00;
        IFF1 = false;
        IFF2 = false;
        IM = 0;
        Halted = false;
        EiDelay = false;
        InterruptPending = false;
        TotalTStates = 0;
    }

    public void RequestInterrupt()
    {
        InterruptPending = true;
    }

    /// <summary>
    /// Executes one Z80 instruction (or handles an active interrupt).
    /// Returns the number of T-states consumed.
    /// </summary>
    public int Step()
    {
        int tStates = 0;

        // Check for interrupt if enabled and not immediately following EI
        if (InterruptPending && IFF1 && !EiDelay)
        {
            tStates = HandleInterrupt();
            TotalTStates += tStates;
            _bus.AddTStates(tStates);
            return tStates;
        }

        if (EiDelay)
        {
            EiDelay = false;
        }

        if (Halted)
        {
            // When halted, CPU executes NOPs while waiting for interrupt
            tStates = 4;
            R = (byte)((R & 0x80) | ((R + 1) & 0x7F));
            TotalTStates += tStates;
            _bus.AddTStates(tStates);
            return tStates;
        }

        tStates = ExecuteInstruction();
        TotalTStates += tStates;
        _bus.AddTStates(tStates);
        return tStates;
    }

    private int HandleInterrupt()
    {
        InterruptPending = false;
        IFF1 = false;
        IFF2 = false;
        Halted = false;

        R = (byte)((R & 0x80) | ((R + 1) & 0x7F));

        switch (IM)
        {
            case 0:
            case 1:
                // IM 1 is standard ZX Spectrum mode: RST 38H
                PushWord(PC);
                PC = 0x0038;
                return 13;

            case 2:
                // Vectored interrupt: address read from (I << 8) | 0xFF
                PushWord(PC);
                ushort vectorAddr = (ushort)((I << 8) | 0xFF);
                ushort target = (ushort)(_bus.Read(vectorAddr) | (_bus.Read((ushort)(vectorAddr + 1)) << 8));
                PC = target;
                return 19;

            default:
                PushWord(PC);
                PC = 0x0038;
                return 13;
        }
    }

    private byte FetchByte()
    {
        byte b = _bus.Read(PC++);
        return b;
    }

    private ushort FetchWord()
    {
        byte l = FetchByte();
        byte h = FetchByte();
        return (ushort)((h << 8) | l);
    }

    private void PushWord(ushort value)
    {
        _bus.Write(--SP, (byte)(value >> 8));
        _bus.Write(--SP, (byte)value);
    }

    private ushort PopWord()
    {
        byte l = _bus.Read(SP++);
        byte h = _bus.Read(SP++);
        return (ushort)((h << 8) | l);
    }

    private int ExecuteInstruction()
    {
        byte opcode = FetchByte();
        R = (byte)((R & 0x80) | ((R + 1) & 0x7F));

        switch (opcode)
        {
            case 0xCB:
                return ExecuteCb();
            case 0xED:
                return ExecuteEd();
            case 0xDD:
                return ExecuteIndex(ref IX, false);
            case 0xFD:
                return ExecuteIndex(ref IY, true);
            default:
                return ExecuteBase(opcode);
        }
    }

    #region Base Instructions (0x00 - 0xFF)

    private int ExecuteBase(byte opcode)
    {
        switch (opcode)
        {
            // NOP
            case 0x00: return 4;

            // LD rr, nn
            case 0x01: BC = FetchWord(); return 10;
            case 0x11: DE = FetchWord(); return 10;
            case 0x21: HL = FetchWord(); return 10;
            case 0x31: SP = FetchWord(); return 10;

            // LD (BC/DE), A
            case 0x02: _bus.Write(BC, A); return 7;
            case 0x12: _bus.Write(DE, A); return 7;

            // INC rr / DEC rr
            case 0x03: BC++; return 6;
            case 0x13: DE++; return 6;
            case 0x23: HL++; return 6;
            case 0x33: SP++; return 6;
            case 0x0B: BC--; return 6;
            case 0x1B: DE--; return 6;
            case 0x2B: HL--; return 6;
            case 0x3B: SP--; return 6;

            // INC r / DEC r
            case 0x04: B = Inc8(B); return 4;
            case 0x05: B = Dec8(B); return 4;
            case 0x0C: C = Inc8(C); return 4;
            case 0x0D: C = Dec8(C); return 4;
            case 0x14: D = Inc8(D); return 4;
            case 0x15: D = Dec8(D); return 4;
            case 0x1C: E = Inc8(E); return 4;
            case 0x1D: E = Dec8(E); return 4;
            case 0x24: H = Inc8(H); return 4;
            case 0x25: H = Dec8(H); return 4;
            case 0x2C: L = Inc8(L); return 4;
            case 0x2D: L = Dec8(L); return 4;
            case 0x34: _bus.Write(HL, Inc8(_bus.Read(HL))); return 11;
            case 0x35: _bus.Write(HL, Dec8(_bus.Read(HL))); return 11;
            case 0x3C: A = Inc8(A); return 4;
            case 0x3D: A = Dec8(A); return 4;

            // LD r, n
            case 0x06: B = FetchByte(); return 7;
            case 0x0E: C = FetchByte(); return 7;
            case 0x16: D = FetchByte(); return 7;
            case 0x1E: E = FetchByte(); return 7;
            case 0x26: H = FetchByte(); return 7;
            case 0x2E: L = FetchByte(); return 7;
            case 0x36: _bus.Write(HL, FetchByte()); return 10;
            case 0x3E: A = FetchByte(); return 7;

            // RLCA, RRCA, RLA, RRA
            case 0x07: Rlca(); return 4;
            case 0x0F: Rrca(); return 4;
            case 0x17: Rla(); return 4;
            case 0x1F: Rra(); return 4;

            // EX AF, AF'
            case 0x08:
                (A, AAlt) = (AAlt, A);
                (F, FAlt) = (FAlt, F);
                return 4;

            // ADD HL, rr
            case 0x09: AddHl(BC); return 11;
            case 0x19: AddHl(DE); return 11;
            case 0x29: AddHl(HL); return 11;
            case 0x39: AddHl(SP); return 11;

            // LD A, (BC/DE)
            case 0x0A: A = _bus.Read(BC); return 7;
            case 0x1A: A = _bus.Read(DE); return 7;

            // DJNZ e
            case 0x10:
                sbyte djnzDisp = (sbyte)FetchByte();
                B--;
                if (B != 0)
                {
                    PC = (ushort)(PC + djnzDisp);
                    return 13;
                }
                return 8;

            // JR e
            case 0x18:
                sbyte jrDisp = (sbyte)FetchByte();
                PC = (ushort)(PC + jrDisp);
                return 12;

            // JR cc, e
            case 0x20: return JrCc((F & FlagZ) == 0); // JR NZ
            case 0x28: return JrCc((F & FlagZ) != 0); // JR Z
            case 0x30: return JrCc((F & FlagC) == 0); // JR NC
            case 0x38: return JrCc((F & FlagC) != 0); // JR C

            // LD (nn), HL
            case 0x22:
                ushort addr22 = FetchWord();
                _bus.Write(addr22, L);
                _bus.Write((ushort)(addr22 + 1), H);
                return 16;

            // LD HL, (nn)
            case 0x2A:
                ushort addr2A = FetchWord();
                L = _bus.Read(addr2A);
                H = _bus.Read((ushort)(addr2A + 1));
                return 16;

            // LD (nn), A
            case 0x32:
                ushort addr32 = FetchWord();
                _bus.Write(addr32, A);
                return 13;

            // LD A, (nn)
            case 0x3A:
                ushort addr3A = FetchWord();
                A = _bus.Read(addr3A);
                return 13;

            // DAA, CPL, SCF, CCF
            case 0x27: Daa(); return 4;
            case 0x2F: Cpl(); return 4;
            case 0x37: Scf(); return 4;
            case 0x3F: Ccf(); return 4;

            // LD r, r' (0x40 - 0x7F)
            case 0x40: return 4; // LD B, B
            case 0x41: B = C; return 4;
            case 0x42: B = D; return 4;
            case 0x43: B = E; return 4;
            case 0x44: B = H; return 4;
            case 0x45: B = L; return 4;
            case 0x46: B = _bus.Read(HL); return 7;
            case 0x47: B = A; return 4;

            case 0x48: C = B; return 4;
            case 0x49: return 4; // LD C, C
            case 0x4A: C = D; return 4;
            case 0x4B: C = E; return 4;
            case 0x4C: C = H; return 4;
            case 0x4D: C = L; return 4;
            case 0x4E: C = _bus.Read(HL); return 7;
            case 0x4F: C = A; return 4;

            case 0x50: D = B; return 4;
            case 0x51: D = C; return 4;
            case 0x52: return 4; // LD D, D
            case 0x53: D = E; return 4;
            case 0x54: D = H; return 4;
            case 0x55: D = L; return 4;
            case 0x56: D = _bus.Read(HL); return 7;
            case 0x57: D = A; return 4;

            case 0x58: E = B; return 4;
            case 0x59: E = C; return 4;
            case 0x5A: E = D; return 4;
            case 0x5B: return 4; // LD E, E
            case 0x5C: E = H; return 4;
            case 0x5D: E = L; return 4;
            case 0x5E: E = _bus.Read(HL); return 7;
            case 0x5F: E = A; return 4;

            case 0x60: H = B; return 4;
            case 0x61: H = C; return 4;
            case 0x62: H = D; return 4;
            case 0x63: H = E; return 4;
            case 0x64: return 4; // LD H, H
            case 0x65: H = L; return 4;
            case 0x66: H = _bus.Read(HL); return 7;
            case 0x67: H = A; return 4;

            case 0x68: L = B; return 4;
            case 0x69: L = C; return 4;
            case 0x6A: L = D; return 4;
            case 0x6B: L = E; return 4;
            case 0x6C: L = H; return 4;
            case 0x6D: return 4; // LD L, L
            case 0x6E: L = _bus.Read(HL); return 7;
            case 0x6F: L = A; return 4;

            case 0x70: _bus.Write(HL, B); return 7;
            case 0x71: _bus.Write(HL, C); return 7;
            case 0x72: _bus.Write(HL, D); return 7;
            case 0x73: _bus.Write(HL, E); return 7;
            case 0x74: _bus.Write(HL, H); return 7;
            case 0x75: _bus.Write(HL, L); return 7;
            case 0x76: Halted = true; return 4; // HALT
            case 0x77: _bus.Write(HL, A); return 7;

            case 0x78: A = B; return 4;
            case 0x79: A = C; return 4;
            case 0x7A: A = D; return 4;
            case 0x7B: A = E; return 4;
            case 0x7C: A = H; return 4;
            case 0x7D: A = L; return 4;
            case 0x7E: A = _bus.Read(HL); return 7;
            case 0x7F: return 4; // LD A, A

            // ALU Operations (0x80 - 0xBF)
            case 0x80: Add8(B); return 4;
            case 0x81: Add8(C); return 4;
            case 0x82: Add8(D); return 4;
            case 0x83: Add8(E); return 4;
            case 0x84: Add8(H); return 4;
            case 0x85: Add8(L); return 4;
            case 0x86: Add8(_bus.Read(HL)); return 7;
            case 0x87: Add8(A); return 4;

            case 0x88: Adc8(B); return 4;
            case 0x89: Adc8(C); return 4;
            case 0x8A: Adc8(D); return 4;
            case 0x8B: Adc8(E); return 4;
            case 0x8C: Adc8(H); return 4;
            case 0x8D: Adc8(L); return 4;
            case 0x8E: Adc8(_bus.Read(HL)); return 7;
            case 0x8F: Adc8(A); return 4;

            case 0x90: Sub8(B); return 4;
            case 0x91: Sub8(C); return 4;
            case 0x92: Sub8(D); return 4;
            case 0x93: Sub8(E); return 4;
            case 0x94: Sub8(H); return 4;
            case 0x95: Sub8(L); return 4;
            case 0x96: Sub8(_bus.Read(HL)); return 7;
            case 0x97: Sub8(A); return 4;

            case 0x98: Sbc8(B); return 4;
            case 0x99: Sbc8(C); return 4;
            case 0x9A: Sbc8(D); return 4;
            case 0x9B: Sbc8(E); return 4;
            case 0x9C: Sbc8(H); return 4;
            case 0x9D: Sbc8(L); return 4;
            case 0x9E: Sbc8(_bus.Read(HL)); return 7;
            case 0x9F: Sbc8(A); return 4;

            case 0xA0: And8(B); return 4;
            case 0xA1: And8(C); return 4;
            case 0xA2: And8(D); return 4;
            case 0xA3: And8(E); return 4;
            case 0xA4: And8(H); return 4;
            case 0xA5: And8(L); return 4;
            case 0xA6: And8(_bus.Read(HL)); return 7;
            case 0xA7: And8(A); return 4;

            case 0xA8: Xor8(B); return 4;
            case 0xA9: Xor8(C); return 4;
            case 0xAA: Xor8(D); return 4;
            case 0xAB: Xor8(E); return 4;
            case 0xAC: Xor8(H); return 4;
            case 0xAD: Xor8(L); return 4;
            case 0xAE: Xor8(_bus.Read(HL)); return 7;
            case 0xAF: Xor8(A); return 4;

            case 0xB0: Or8(B); return 4;
            case 0xB1: Or8(C); return 4;
            case 0xB2: Or8(D); return 4;
            case 0xB3: Or8(E); return 4;
            case 0xB4: Or8(H); return 4;
            case 0xB5: Or8(L); return 4;
            case 0xB6: Or8(_bus.Read(HL)); return 7;
            case 0xB7: Or8(A); return 4;

            case 0xB8: Cp8(B); return 4;
            case 0xB9: Cp8(C); return 4;
            case 0xBA: Cp8(D); return 4;
            case 0xBB: Cp8(E); return 4;
            case 0xBC: Cp8(H); return 4;
            case 0xBD: Cp8(L); return 4;
            case 0xBE: Cp8(_bus.Read(HL)); return 7;
            case 0xBF: Cp8(A); return 4;

            // RET cc
            case 0xC0: return RetCc((F & FlagZ) == 0); // RET NZ
            case 0xC8: return RetCc((F & FlagZ) != 0); // RET Z
            case 0xD0: return RetCc((F & FlagC) == 0); // RET NC
            case 0xD8: return RetCc((F & FlagC) != 0); // RET C
            case 0xE0: return RetCc((F & FlagPV) == 0); // RET PO
            case 0xE8: return RetCc((F & FlagPV) != 0); // RET PE
            case 0xF0: return RetCc((F & FlagS) == 0); // RET P
            case 0xF8: return RetCc((F & FlagS) != 0); // RET M

            // POP rr
            case 0xC1: BC = PopWord(); return 10;
            case 0xD1: DE = PopWord(); return 10;
            case 0xE1: HL = PopWord(); return 10;
            case 0xF1:
                ushort afVal = PopWord();
                F = (byte)afVal;
                A = (byte)(afVal >> 8);
                return 10;

            // JP cc, nn
            case 0xC2: return JpCc((F & FlagZ) == 0);
            case 0xCA: return JpCc((F & FlagZ) != 0);
            case 0xD2: return JpCc((F & FlagC) == 0);
            case 0xDA: return JpCc((F & FlagC) != 0);
            case 0xE2: return JpCc((F & FlagPV) == 0);
            case 0xEA: return JpCc((F & FlagPV) != 0);
            case 0xF2: return JpCc((F & FlagS) == 0);
            case 0xFA: return JpCc((F & FlagS) != 0);

            // JP nn
            case 0xC3: PC = FetchWord(); return 10;

            // CALL cc, nn
            case 0xC4: return CallCc((F & FlagZ) == 0);
            case 0xCC: return CallCc((F & FlagZ) != 0);
            case 0xD4: return CallCc((F & FlagC) == 0);
            case 0xDC: return CallCc((F & FlagC) != 0);
            case 0xE4: return CallCc((F & FlagPV) == 0);
            case 0xEC: return CallCc((F & FlagPV) != 0);
            case 0xF4: return CallCc((F & FlagS) == 0);
            case 0xFC: return CallCc((F & FlagS) != 0);

            // PUSH rr
            case 0xC5: PushWord(BC); return 11;
            case 0xD5: PushWord(DE); return 11;
            case 0xE5: PushWord(HL); return 11;
            case 0xF5: PushWord(AF); return 11;

            // Immediate ALU
            case 0xC6: Add8(FetchByte()); return 7;
            case 0xCE: Adc8(FetchByte()); return 7;
            case 0xD6: Sub8(FetchByte()); return 7;
            case 0xDE: Sbc8(FetchByte()); return 7;
            case 0xE6: And8(FetchByte()); return 7;
            case 0xEE: Xor8(FetchByte()); return 7;
            case 0xF6: Or8(FetchByte()); return 7;
            case 0xFE: Cp8(FetchByte()); return 7;

            // RST p
            case 0xC7: PushWord(PC); PC = 0x0000; return 11;
            case 0xCF: PushWord(PC); PC = 0x0008; return 11;
            case 0xD7: PushWord(PC); PC = 0x0010; return 11;
            case 0xDF: PushWord(PC); PC = 0x0018; return 11;
            case 0xE7: PushWord(PC); PC = 0x0020; return 11;
            case 0xEF: PushWord(PC); PC = 0x0028; return 11;
            case 0xF7: PushWord(PC); PC = 0x0030; return 11;
            case 0xFF: PushWord(PC); PC = 0x0038; return 11;

            // RET
            case 0xC9: PC = PopWord(); return 10;

            // CALL nn
            case 0xCD:
                ushort callTarget = FetchWord();
                PushWord(PC);
                PC = callTarget;
                return 17;

            // OUT (n), A / IN A, (n)
            case 0xD3:
                byte portD3 = FetchByte();
                _bus.Out((ushort)((A << 8) | portD3), A);
                return 11;

            case 0xDB:
                byte portDB = FetchByte();
                A = _bus.In((ushort)((A << 8) | portDB));
                return 11;

            // EXX
            case 0xD9:
                (B, BAlt) = (BAlt, B);
                (C, CAlt) = (CAlt, C);
                (D, DAlt) = (DAlt, D);
                (E, EAlt) = (EAlt, E);
                (H, HAlt) = (HAlt, H);
                (L, LAlt) = (LAlt, L);
                return 4;

            // EX (SP), HL
            case 0xE3:
                ushort spVal = (ushort)(_bus.Read(SP) | (_bus.Read((ushort)(SP + 1)) << 8));
                _bus.Write(SP, L);
                _bus.Write((ushort)(SP + 1), H);
                HL = spVal;
                return 19;

            // JP (HL)
            case 0xE9: PC = HL; return 4;

            // EX DE, HL
            case 0xEB:
                (D, H) = (H, D);
                (E, L) = (L, E);
                return 4;

            // DI / EI
            case 0xF3:
                IFF1 = false;
                IFF2 = false;
                return 4;

            case 0xFB:
                IFF1 = true;
                IFF2 = true;
                EiDelay = true;
                return 4;

            // LD SP, HL
            case 0xF9: SP = HL; return 6;

            default:
                return 4;
        }
    }

    private int JrCc(bool condition)
    {
        sbyte disp = (sbyte)FetchByte();
        if (condition)
        {
            PC = (ushort)(PC + disp);
            return 12;
        }
        return 7;
    }

    private int JpCc(bool condition)
    {
        ushort target = FetchWord();
        if (condition)
        {
            PC = target;
        }
        return 10;
    }

    private int CallCc(bool condition)
    {
        ushort target = FetchWord();
        if (condition)
        {
            PushWord(PC);
            PC = target;
            return 17;
        }
        return 10;
    }

    private int RetCc(bool condition)
    {
        if (condition)
        {
            PC = PopWord();
            return 11;
        }
        return 5;
    }

    #endregion

    #region CB-Prefixed Instructions

    private int ExecuteCb()
    {
        byte opcode = FetchByte();
        R = (byte)((R & 0x80) | ((R + 1) & 0x7F));

        int regIndex = opcode & 0x07;
        int bitIndex = (opcode >> 3) & 0x07;
        int group = opcode >> 6;

        byte val = GetRegisterByIndex(regIndex);

        if (group == 0) // Shift / Rotate
        {
            int subop = (opcode >> 3) & 0x07;
            val = subop switch
            {
                0 => Rlc(val),
                1 => Rrc(val),
                2 => Rl(val),
                3 => Rr(val),
                4 => Sla(val),
                5 => Sra(val),
                6 => Sll(val), // Undocumented SLL (sets bit 0)
                7 => Srl(val),
                _ => val
            };
            SetRegisterByIndex(regIndex, val);
            return regIndex == 6 ? 15 : 8;
        }
        else if (group == 1) // BIT b, r
        {
            byte mask = (byte)(1 << bitIndex);
            byte res = (byte)(val & mask);

            F = (byte)((F & FlagC) | FlagH | (res == 0 ? (FlagZ | FlagPV) : 0));
            if (bitIndex == 7 && res != 0) F |= FlagS;

            // Undocumented bits 3 & 5
            if (regIndex == 6)
            {
                // BIT b, (HL): bits 3 & 5 come from high byte of memory pointer
                F |= (byte)((H << 8 | L) >> 8 & (FlagX | FlagY));
            }
            else
            {
                F |= (byte)(val & (FlagX | FlagY));
            }

            return regIndex == 6 ? 12 : 8;
        }
        else if (group == 2) // RES b, r
        {
            val = (byte)(val & ~(1 << bitIndex));
            SetRegisterByIndex(regIndex, val);
            return regIndex == 6 ? 15 : 8;
        }
        else // SET b, r
        {
            val = (byte)(val | (1 << bitIndex));
            SetRegisterByIndex(regIndex, val);
            return regIndex == 6 ? 15 : 8;
        }
    }

    private byte GetRegisterByIndex(int idx) => idx switch
    {
        0 => B,
        1 => C,
        2 => D,
        3 => E,
        4 => H,
        5 => L,
        6 => _bus.Read(HL),
        7 => A,
        _ => 0
    };

    private void SetRegisterByIndex(int idx, byte value)
    {
        switch (idx)
        {
            case 0: B = value; break;
            case 1: C = value; break;
            case 2: D = value; break;
            case 3: E = value; break;
            case 4: H = value; break;
            case 5: L = value; break;
            case 6: _bus.Write(HL, value); break;
            case 7: A = value; break;
        }
    }

    #endregion

    #region ED-Prefixed Instructions

    private int ExecuteEd()
    {
        byte opcode = FetchByte();
        R = (byte)((R & 0x80) | ((R + 1) & 0x7F));

        switch (opcode)
        {
            // Block Instructions
            case 0xA0: return Ldi();
            case 0xB0: return Ldir();
            case 0xA8: return Ldd();
            case 0xB8: return Lddr();
            case 0xA1: return Cpi();
            case 0xB1: return Cpir();
            case 0xA9: return Cpd();
            case 0xB9: return Cpdr();
            case 0xA2: return Ini();
            case 0xB2: return Inir();
            case 0xAA: return Ind();
            case 0xBA: return Indr();
            case 0xA3: return Outi();
            case 0xB3: return Otir();
            case 0xAB: return Outd();
            case 0xBB: return Otdr();

            // 16-bit ADC HL, rr
            case 0x4A: AdcHl(BC); return 15;
            case 0x5A: AdcHl(DE); return 15;
            case 0x6A: AdcHl(HL); return 15;
            case 0x7A: AdcHl(SP); return 15;

            // 16-bit SBC HL, rr
            case 0x42: SbcHl(BC); return 15;
            case 0x52: SbcHl(DE); return 15;
            case 0x62: SbcHl(HL); return 15;
            case 0x72: SbcHl(SP); return 15;

            // LD (nn), rr
            case 0x43:
                ushort addr43 = FetchWord();
                _bus.Write(addr43, C);
                _bus.Write((ushort)(addr43 + 1), B);
                return 20;

            case 0x53:
                ushort addr53 = FetchWord();
                _bus.Write(addr53, E);
                _bus.Write((ushort)(addr53 + 1), D);
                return 20;

            case 0x63:
                ushort addr63 = FetchWord();
                _bus.Write(addr63, L);
                _bus.Write((ushort)(addr63 + 1), H);
                return 20;

            case 0x73:
                ushort addr73 = FetchWord();
                _bus.Write(addr73, (byte)SP);
                _bus.Write((ushort)(addr73 + 1), (byte)(SP >> 8));
                return 20;

            // LD rr, (nn)
            case 0x4B:
                ushort addr4B = FetchWord();
                C = _bus.Read(addr4B);
                B = _bus.Read((ushort)(addr4B + 1));
                return 20;

            case 0x5B:
                ushort addr5B = FetchWord();
                E = _bus.Read(addr5B);
                D = _bus.Read((ushort)(addr5B + 1));
                return 20;

            case 0x6B:
                ushort addr6B = FetchWord();
                L = _bus.Read(addr6B);
                H = _bus.Read((ushort)(addr6B + 1));
                return 20;

            case 0x7B:
                ushort addr7B = FetchWord();
                SP = (ushort)(_bus.Read(addr7B) | (_bus.Read((ushort)(addr7B + 1)) << 8));
                return 20;

            // IN r, (C) / OUT (C), r
            case 0x40: B = InC(); return 12;
            case 0x48: C = InC(); return 12;
            case 0x50: D = InC(); return 12;
            case 0x58: E = InC(); return 12;
            case 0x60: H = InC(); return 12;
            case 0x68: L = InC(); return 12;
            case 0x70: InC(); return 12; // Undocumented IN (HL), (C) - only affects flags
            case 0x78: A = InC(); return 12;

            case 0x41: _bus.Out(BC, B); return 12;
            case 0x49: _bus.Out(BC, C); return 12;
            case 0x51: _bus.Out(BC, D); return 12;
            case 0x59: _bus.Out(BC, E); return 12;
            case 0x61: _bus.Out(BC, H); return 12;
            case 0x69: _bus.Out(BC, L); return 12;
            case 0x71: _bus.Out(BC, 0); return 12; // Undocumented OUT (C), 0
            case 0x79: _bus.Out(BC, A); return 12;

            // NEG
            case 0x44:
            case 0x4C:
            case 0x54:
            case 0x5C:
            case 0x64:
            case 0x6C:
            case 0x74:
            case 0x7C:
                Neg();
                return 8;

            // RETN / RETI
            case 0x45:
            case 0x55:
            case 0x65:
            case 0x75:
            case 0x4D:
            case 0x5D:
            case 0x6D:
            case 0x7D:
                PC = PopWord();
                IFF1 = IFF2;
                return 14;

            // IM 0, 1, 2
            case 0x46: case 0x66: IM = 0; return 8;
            case 0x56: case 0x76: IM = 1; return 8;
            case 0x5E: case 0x7E: IM = 2; return 8;

            // LD I, A / LD R, A / LD A, I / LD A, R
            case 0x47: I = A; return 9;
            case 0x4F: R = A; return 9;
            case 0x57:
                A = I;
                F = (byte)((F & FlagC) | Sz53Table[A] | (IFF2 ? FlagPV : 0));
                return 9;
            case 0x5F:
                A = R;
                F = (byte)((F & FlagC) | Sz53Table[A] | (IFF2 ? FlagPV : 0));
                return 9;

            // RRD / RLD
            case 0x67: Rrd(); return 18;
            case 0x6F: Rld(); return 18;

            default:
                return 8;
        }
    }

    private byte InC()
    {
        byte val = _bus.In(BC);
        F = (byte)((F & FlagC) | Sz53Table[val] | ParityTable[val]);
        return val;
    }

    private void Neg()
    {
        byte orig = A;
        A = 0;
        Sub8(orig);
    }

    private void Rrd()
    {
        byte hlVal = _bus.Read(HL);
        byte lowA = (byte)(A & 0x0F);
        A = (byte)((A & 0xF0) | (hlVal & 0x0F));
        hlVal = (byte)((lowA << 4) | (hlVal >> 4));
        _bus.Write(HL, hlVal);
        F = (byte)((F & FlagC) | Sz53Table[A] | ParityTable[A]);
    }

    private void Rld()
    {
        byte hlVal = _bus.Read(HL);
        byte lowA = (byte)(A & 0x0F);
        A = (byte)((A & 0xF0) | (hlVal >> 4));
        hlVal = (byte)((hlVal << 4) | lowA);
        _bus.Write(HL, hlVal);
        F = (byte)((F & FlagC) | Sz53Table[A] | ParityTable[A]);
    }

    private int Ldi()
    {
        byte b = _bus.Read(HL++);
        _bus.Write(DE++, b);
        BC--;
        byte n = (byte)(A + b);
        F = (byte)((F & (FlagC | FlagZ | FlagS)) | (BC != 0 ? FlagPV : 0) | (n & FlagX) | ((n << 4) & FlagY));
        return 16;
    }

    private int Ldir()
    {
        Ldi();
        if (BC != 0)
        {
            PC -= 2;
            return 21;
        }
        return 16;
    }

    private int Ldd()
    {
        byte b = _bus.Read(HL--);
        _bus.Write(DE--, b);
        BC--;
        byte n = (byte)(A + b);
        F = (byte)((F & (FlagC | FlagZ | FlagS)) | (BC != 0 ? FlagPV : 0) | (n & FlagX) | ((n << 4) & FlagY));
        return 16;
    }

    private int Lddr()
    {
        Ldd();
        if (BC != 0)
        {
            PC -= 2;
            return 21;
        }
        return 16;
    }

    private int Cpi()
    {
        byte val = _bus.Read(HL++);
        BC--;
        int res = A - val;
        byte flagH = (byte)(((A & 0x0F) < (val & 0x0F)) ? FlagH : 0);
        int n = res - ((flagH != 0) ? 1 : 0);

        F = (byte)((F & FlagC) | FlagN | flagH | (BC != 0 ? FlagPV : 0) |
                   (res == 0 ? FlagZ : 0) | (res < 0 ? FlagS : 0) |
                   (n & FlagX) | ((n << 4) & FlagY));
        return 16;
    }

    private int Cpir()
    {
        Cpi();
        if (BC != 0 && (F & FlagZ) == 0)
        {
            PC -= 2;
            return 21;
        }
        return 16;
    }

    private int Cpd()
    {
        byte val = _bus.Read(HL--);
        BC--;
        int res = A - val;
        byte flagH = (byte)(((A & 0x0F) < (val & 0x0F)) ? FlagH : 0);
        int n = res - ((flagH != 0) ? 1 : 0);

        F = (byte)((F & FlagC) | FlagN | flagH | (BC != 0 ? FlagPV : 0) |
                   (res == 0 ? FlagZ : 0) | (res < 0 ? FlagS : 0) |
                   (n & FlagX) | ((n << 4) & FlagY));
        return 16;
    }

    private int Cpdr()
    {
        Cpd();
        if (BC != 0 && (F & FlagZ) == 0)
        {
            PC -= 2;
            return 21;
        }
        return 16;
    }

    private int Ini()
    {
        byte val = _bus.In(BC);
        _bus.Write(HL++, val);
        B--;
        F = (byte)(FlagN | (B == 0 ? FlagZ : 0));
        return 16;
    }

    private int Inir()
    {
        Ini();
        if (B != 0)
        {
            PC -= 2;
            return 21;
        }
        return 16;
    }

    private int Ind()
    {
        byte val = _bus.In(BC);
        _bus.Write(HL--, val);
        B--;
        F = (byte)(FlagN | (B == 0 ? FlagZ : 0));
        return 16;
    }

    private int Indr()
    {
        Ind();
        if (B != 0)
        {
            PC -= 2;
            return 21;
        }
        return 16;
    }

    private int Outi()
    {
        byte val = _bus.Read(HL++);
        B--;
        _bus.Out(BC, val);
        F = (byte)(FlagN | (B == 0 ? FlagZ : 0));
        return 16;
    }

    private int Otir()
    {
        Outi();
        if (B != 0)
        {
            PC -= 2;
            return 21;
        }
        return 16;
    }

    private int Outd()
    {
        byte val = _bus.Read(HL--);
        B--;
        _bus.Out(BC, val);
        F = (byte)(FlagN | (B == 0 ? FlagZ : 0));
        return 16;
    }

    private int Otdr()
    {
        Outd();
        if (B != 0)
        {
            PC -= 2;
            return 21;
        }
        return 16;
    }

    #endregion

    #region Index Instructions (DD/FD IX & IY)

    private int ExecuteIndex(ref ushort regIndex, bool isIY)
    {
        byte opcode = FetchByte();
        R = (byte)((R & 0x80) | ((R + 1) & 0x7F));

        // Indexed CB: DD CB d op or FD CB d op
        if (opcode == 0xCB)
        {
            sbyte disp = (sbyte)FetchByte();
            byte cbOpcode = FetchByte();
            ushort addr = (ushort)(regIndex + disp);
            return ExecuteIndexCb(addr, cbOpcode);
        }

        switch (opcode)
        {
            // ADD reg, rr
            case 0x09: AddIndex(ref regIndex, BC); return 15;
            case 0x19: AddIndex(ref regIndex, DE); return 15;
            case 0x29: AddIndex(ref regIndex, regIndex); return 15;
            case 0x39: AddIndex(ref regIndex, SP); return 15;

            // LD reg, nn
            case 0x21: regIndex = FetchWord(); return 14;

            // LD (nn), reg
            case 0x22:
                ushort addr22 = FetchWord();
                _bus.Write(addr22, (byte)regIndex);
                _bus.Write((ushort)(addr22 + 1), (byte)(regIndex >> 8));
                return 20;

            // LD reg, (nn)
            case 0x2A:
                ushort addr2A = FetchWord();
                regIndex = (ushort)(_bus.Read(addr2A) | (_bus.Read((ushort)(addr2A + 1)) << 8));
                return 20;

            // INC reg / DEC reg
            case 0x23: regIndex++; return 10;
            case 0x2B: regIndex--; return 10;

            // INC (reg+d) / DEC (reg+d)
            case 0x34:
                {
                    sbyte d = (sbyte)FetchByte();
                    ushort a = (ushort)(regIndex + d);
                    _bus.Write(a, Inc8(_bus.Read(a)));
                    return 23;
                }
            case 0x35:
                {
                    sbyte d = (sbyte)FetchByte();
                    ushort a = (ushort)(regIndex + d);
                    _bus.Write(a, Dec8(_bus.Read(a)));
                    return 23;
                }

            // LD (reg+d), n
            case 0x36:
                {
                    sbyte d = (sbyte)FetchByte();
                    byte n = FetchByte();
                    _bus.Write((ushort)(regIndex + d), n);
                    return 19;
                }

            // Undocumented 8-bit operations on IXH/IXL/IYH/IYL
            case 0x24: regIndex = (ushort)((Inc8((byte)(regIndex >> 8)) << 8) | (regIndex & 0xFF)); return 8;
            case 0x25: regIndex = (ushort)((Dec8((byte)(regIndex >> 8)) << 8) | (regIndex & 0xFF)); return 8;
            case 0x26: regIndex = (ushort)((FetchByte() << 8) | (regIndex & 0xFF)); return 11;
            case 0x2C: regIndex = (ushort)((regIndex & 0xFF00) | Inc8((byte)regIndex)); return 8;
            case 0x2D: regIndex = (ushort)((regIndex & 0xFF00) | Dec8((byte)regIndex)); return 8;
            case 0x2E: regIndex = (ushort)((regIndex & 0xFF00) | FetchByte()); return 11;

            // LD r, (reg+d)
            case 0x46: { sbyte d = (sbyte)FetchByte(); B = _bus.Read((ushort)(regIndex + d)); return 19; }
            case 0x4E: { sbyte d = (sbyte)FetchByte(); C = _bus.Read((ushort)(regIndex + d)); return 19; }
            case 0x56: { sbyte d = (sbyte)FetchByte(); D = _bus.Read((ushort)(regIndex + d)); return 19; }
            case 0x5E: { sbyte d = (sbyte)FetchByte(); E = _bus.Read((ushort)(regIndex + d)); return 19; }
            case 0x66: { sbyte d = (sbyte)FetchByte(); H = _bus.Read((ushort)(regIndex + d)); return 19; }
            case 0x6E: { sbyte d = (sbyte)FetchByte(); L = _bus.Read((ushort)(regIndex + d)); return 19; }
            case 0x7E: { sbyte d = (sbyte)FetchByte(); A = _bus.Read((ushort)(regIndex + d)); return 19; }

            // LD (reg+d), r
            case 0x70: { sbyte d = (sbyte)FetchByte(); _bus.Write((ushort)(regIndex + d), B); return 19; }
            case 0x71: { sbyte d = (sbyte)FetchByte(); _bus.Write((ushort)(regIndex + d), C); return 19; }
            case 0x72: { sbyte d = (sbyte)FetchByte(); _bus.Write((ushort)(regIndex + d), D); return 19; }
            case 0x73: { sbyte d = (sbyte)FetchByte(); _bus.Write((ushort)(regIndex + d), E); return 19; }
            case 0x74: { sbyte d = (sbyte)FetchByte(); _bus.Write((ushort)(regIndex + d), H); return 19; }
            case 0x75: { sbyte d = (sbyte)FetchByte(); _bus.Write((ushort)(regIndex + d), L); return 19; }
            case 0x77: { sbyte d = (sbyte)FetchByte(); _bus.Write((ushort)(regIndex + d), A); return 19; }

            // ALU with (reg+d)
            case 0x86: { sbyte d = (sbyte)FetchByte(); Add8(_bus.Read((ushort)(regIndex + d))); return 19; }
            case 0x8E: { sbyte d = (sbyte)FetchByte(); Adc8(_bus.Read((ushort)(regIndex + d))); return 19; }
            case 0x96: { sbyte d = (sbyte)FetchByte(); Sub8(_bus.Read((ushort)(regIndex + d))); return 19; }
            case 0x9E: { sbyte d = (sbyte)FetchByte(); Sbc8(_bus.Read((ushort)(regIndex + d))); return 19; }
            case 0xA6: { sbyte d = (sbyte)FetchByte(); And8(_bus.Read((ushort)(regIndex + d))); return 19; }
            case 0xAE: { sbyte d = (sbyte)FetchByte(); Xor8(_bus.Read((ushort)(regIndex + d))); return 19; }
            case 0xB6: { sbyte d = (sbyte)FetchByte(); Or8(_bus.Read((ushort)(regIndex + d))); return 19; }
            case 0xBE: { sbyte d = (sbyte)FetchByte(); Cp8(_bus.Read((ushort)(regIndex + d))); return 19; }

            // POP reg / PUSH reg
            case 0xE1: regIndex = PopWord(); return 14;
            case 0xE5: PushWord(regIndex); return 15;

            // EX (SP), reg
            case 0xE3:
                ushort spVal = (ushort)(_bus.Read(SP) | (_bus.Read((ushort)(SP + 1)) << 8));
                _bus.Write(SP, (byte)regIndex);
                _bus.Write((ushort)(SP + 1), (byte)(regIndex >> 8));
                regIndex = spVal;
                return 23;

            // JP (reg)
            case 0xE9: PC = regIndex; return 8;

            // LD SP, reg
            case 0xF9: SP = regIndex; return 10;

            default:
                // If prefix followed by non-indexed instruction, execute base opcode + 4 T-states
                return ExecuteBase(opcode) + 4;
        }
    }

    private void AddIndex(ref ushort regIndex, ushort val)
    {
        int res = regIndex + val;
        byte flagH = (byte)((((regIndex & 0x0FFF) + (val & 0x0FFF)) > 0x0FFF) ? FlagH : 0);
        byte flagC = (byte)((res > 0xFFFF) ? FlagC : 0);
        regIndex = (ushort)res;
        F = (byte)((F & (FlagS | FlagZ | FlagPV)) | flagC | flagH | ((regIndex >> 8) & (FlagX | FlagY)));
    }

    private int ExecuteIndexCb(ushort addr, byte opcode)
    {
        int bitIndex = (opcode >> 3) & 0x07;
        int group = opcode >> 6;
        int destReg = opcode & 0x07;

        byte val = _bus.Read(addr);

        if (group == 0) // Shift / Rotate
        {
            int subop = (opcode >> 3) & 0x07;
            val = subop switch
            {
                0 => Rlc(val),
                1 => Rrc(val),
                2 => Rl(val),
                3 => Rr(val),
                4 => Sla(val),
                5 => Sra(val),
                6 => Sll(val),
                7 => Srl(val),
                _ => val
            };
            _bus.Write(addr, val);
            if (destReg != 6) SetRegisterByIndex(destReg, val); // Undocumented write back
            return 23;
        }
        else if (group == 1) // BIT b, (reg+d)
        {
            byte mask = (byte)(1 << bitIndex);
            byte res = (byte)(val & mask);

            F = (byte)((F & FlagC) | FlagH | (res == 0 ? (FlagZ | FlagPV) : 0));
            if (bitIndex == 7 && res != 0) F |= FlagS;
            F |= (byte)((addr >> 8) & (FlagX | FlagY));
            return 20;
        }
        else if (group == 2) // RES b, (reg+d)
        {
            val = (byte)(val & ~(1 << bitIndex));
            _bus.Write(addr, val);
            if (destReg != 6) SetRegisterByIndex(destReg, val); // Undocumented write back
            return 23;
        }
        else // SET b, (reg+d)
        {
            val = (byte)(val | (1 << bitIndex));
            _bus.Write(addr, val);
            if (destReg != 6) SetRegisterByIndex(destReg, val); // Undocumented write back
            return 23;
        }
    }

    #endregion

    #region Helper ALU & Shift Methods

    private byte Inc8(byte val)
    {
        byte res = (byte)(val + 1);
        F = (byte)((F & FlagC) | Sz53Table[res] | ((val & 0x0F) == 0x0F ? FlagH : 0) | (val == 0x7F ? FlagPV : 0));
        return res;
    }

    private byte Dec8(byte val)
    {
        byte res = (byte)(val - 1);
        F = (byte)((F & FlagC) | FlagN | Sz53Table[res] | ((val & 0x0F) == 0 ? FlagH : 0) | (val == 0x80 ? FlagPV : 0));
        return res;
    }

    private void Add8(byte val)
    {
        int res = A + val;
        byte flagH = (byte)((((A & 0x0F) + (val & 0x0F)) > 0x0F) ? FlagH : 0);
        byte flagV = (byte)((((A ^ res) & (val ^ res) & 0x80) != 0) ? FlagPV : 0);
        byte flagC = (byte)((res > 0xFF) ? FlagC : 0);
        A = (byte)res;
        F = (byte)(Sz53Table[A] | flagH | flagV | flagC);
    }

    private void Adc8(byte val)
    {
        int carry = (F & FlagC);
        int res = A + val + carry;
        byte flagH = (byte)((((A & 0x0F) + (val & 0x0F) + carry) > 0x0F) ? FlagH : 0);
        byte flagV = (byte)((((A ^ res) & (val ^ res) & 0x80) != 0) ? FlagPV : 0);
        byte flagC = (byte)((res > 0xFF) ? FlagC : 0);
        A = (byte)res;
        F = (byte)(Sz53Table[A] | flagH | flagV | flagC);
    }

    private void Sub8(byte val)
    {
        int res = A - val;
        byte flagH = (byte)(((A & 0x0F) < (val & 0x0F)) ? FlagH : 0);
        byte flagV = (byte)((((A ^ val) & (A ^ res) & 0x80) != 0) ? FlagPV : 0);
        byte flagC = (byte)((res < 0) ? FlagC : 0);
        A = (byte)res;
        F = (byte)(FlagN | Sz53Table[A] | flagH | flagV | flagC);
    }

    private void Sbc8(byte val)
    {
        int carry = (F & FlagC);
        int res = A - val - carry;
        byte flagH = (byte)(((A & 0x0F) < ((val & 0x0F) + carry)) ? FlagH : 0);
        byte flagV = (byte)((((A ^ val) & (A ^ res) & 0x80) != 0) ? FlagPV : 0);
        byte flagC = (byte)((res < 0) ? FlagC : 0);
        A = (byte)res;
        F = (byte)(FlagN | Sz53Table[A] | flagH | flagV | flagC);
    }

    private void And8(byte val)
    {
        A &= val;
        F = (byte)(FlagH | Sz53Table[A] | ParityTable[A]);
    }

    private void Xor8(byte val)
    {
        A ^= val;
        F = (byte)(Sz53Table[A] | ParityTable[A]);
    }

    private void Or8(byte val)
    {
        A |= val;
        F = (byte)(Sz53Table[A] | ParityTable[A]);
    }

    private void Cp8(byte val)
    {
        int res = A - val;
        byte flagH = (byte)(((A & 0x0F) < (val & 0x0F)) ? FlagH : 0);
        byte flagV = (byte)((((A ^ val) & (A ^ res) & 0x80) != 0) ? FlagPV : 0);
        byte flagC = (byte)((res < 0) ? FlagC : 0);
        byte res8 = (byte)res;
        F = (byte)(FlagN | (Sz53Table[res8] & (FlagS | FlagZ)) | (val & (FlagX | FlagY)) | flagH | flagV | flagC);
    }

    private void AddHl(ushort val)
    {
        int res = HL + val;
        byte flagH = (byte)((((HL & 0x0FFF) + (val & 0x0FFF)) > 0x0FFF) ? FlagH : 0);
        byte flagC = (byte)((res > 0xFFFF) ? FlagC : 0);
        HL = (ushort)res;
        F = (byte)((F & (FlagS | FlagZ | FlagPV)) | flagC | flagH | ((HL >> 8) & (FlagX | FlagY)));
    }

    private void AdcHl(ushort val)
    {
        int carry = (F & FlagC);
        int res = HL + val + carry;
        byte flagH = (byte)((((HL & 0x0FFF) + (val & 0x0FFF) + carry) > 0x0FFF) ? FlagH : 0);
        byte flagV = (byte)((((HL ^ res) & (val ^ res) & 0x8000) != 0) ? FlagPV : 0);
        byte flagC = (byte)((res > 0xFFFF) ? FlagC : 0);
        HL = (ushort)res;
        byte h8 = (byte)(HL >> 8);
        F = (byte)(Sz53Table[h8] & (FlagS | FlagX | FlagY));
        if (HL == 0) F |= FlagZ;
        F |= (byte)(flagH | flagV | flagC);
    }

    private void SbcHl(ushort val)
    {
        int carry = (F & FlagC);
        int res = HL - val - carry;
        byte flagH = (byte)(((HL & 0x0FFF) < ((val & 0x0FFF) + carry)) ? FlagH : 0);
        byte flagV = (byte)((((HL ^ val) & (HL ^ res) & 0x8000) != 0) ? FlagPV : 0);
        byte flagC = (byte)((res < 0) ? FlagC : 0);
        HL = (ushort)res;
        byte h8 = (byte)(HL >> 8);
        F = (byte)(FlagN | (Sz53Table[h8] & (FlagS | FlagX | FlagY)));
        if (HL == 0) F |= FlagZ;
        F |= (byte)(flagH | flagV | flagC);
    }

    private void Daa()
    {
        int val = A;
        int corr = 0;
        int carry = F & FlagC;

        if ((F & FlagH) != 0 || (val & 0x0F) > 9) corr |= 0x06;
        if (carry != 0 || val > 0x99)
        {
            corr |= 0x60;
            carry = FlagC;
        }

        if ((F & FlagN) != 0)
        {
            val -= corr;
        }
        else
        {
            val += corr;
        }

        A = (byte)val;
        byte flagH = (byte)((((A ^ val) & 0x10) != 0) ? FlagH : 0);
        F = (byte)((F & FlagN) | Sz53Table[A] | ParityTable[A] | carry | flagH);
    }

    private void Cpl()
    {
        A = (byte)~A;
        F |= (byte)(FlagH | FlagN | (A & (FlagX | FlagY)));
    }

    private void Scf()
    {
        F = (byte)((F & (FlagS | FlagZ | FlagPV)) | FlagC | (A & (FlagX | FlagY)));
    }

    private void Ccf()
    {
        byte flagH = (byte)(((F & FlagC) != 0) ? FlagH : 0);
        byte flagC = (byte)(((F & FlagC) == 0) ? FlagC : 0);
        F = (byte)((F & (FlagS | FlagZ | FlagPV)) | flagH | flagC | (A & (FlagX | FlagY)));
    }

    private void Rlca()
    {
        byte c = (byte)((A >> 7) & 1);
        A = (byte)((A << 1) | c);
        F = (byte)((F & (FlagS | FlagZ | FlagPV)) | c | (A & (FlagX | FlagY)));
    }

    private void Rrca()
    {
        byte c = (byte)(A & 1);
        A = (byte)((A >> 1) | (c << 7));
        F = (byte)((F & (FlagS | FlagZ | FlagPV)) | c | (A & (FlagX | FlagY)));
    }

    private void Rla()
    {
        byte oldC = (byte)(F & FlagC);
        byte newC = (byte)((A >> 7) & 1);
        A = (byte)((A << 1) | oldC);
        F = (byte)((F & (FlagS | FlagZ | FlagPV)) | newC | (A & (FlagX | FlagY)));
    }

    private void Rra()
    {
        byte oldC = (byte)(F & FlagC);
        byte newC = (byte)(A & 1);
        A = (byte)((A >> 1) | (oldC << 7));
        F = (byte)((F & (FlagS | FlagZ | FlagPV)) | newC | (A & (FlagX | FlagY)));
    }

    private byte Rlc(byte val)
    {
        byte c = (byte)((val >> 7) & 1);
        byte res = (byte)((val << 1) | c);
        F = (byte)(Sz53Table[res] | ParityTable[res] | c);
        return res;
    }

    private byte Rrc(byte val)
    {
        byte c = (byte)(val & 1);
        byte res = (byte)((val >> 1) | (c << 7));
        F = (byte)(Sz53Table[res] | ParityTable[res] | c);
        return res;
    }

    private byte Rl(byte val)
    {
        byte oldC = (byte)(F & FlagC);
        byte newC = (byte)((val >> 7) & 1);
        byte res = (byte)((val << 1) | oldC);
        F = (byte)(Sz53Table[res] | ParityTable[res] | newC);
        return res;
    }

    private byte Rr(byte val)
    {
        byte oldC = (byte)(F & FlagC);
        byte newC = (byte)(val & 1);
        byte res = (byte)((val >> 1) | (oldC << 7));
        F = (byte)(Sz53Table[res] | ParityTable[res] | newC);
        return res;
    }

    private byte Sla(byte val)
    {
        byte c = (byte)((val >> 7) & 1);
        byte res = (byte)(val << 1);
        F = (byte)(Sz53Table[res] | ParityTable[res] | c);
        return res;
    }

    private byte Sra(byte val)
    {
        byte c = (byte)(val & 1);
        byte res = (byte)((val >> 1) | (val & 0x80));
        F = (byte)(Sz53Table[res] | ParityTable[res] | c);
        return res;
    }

    private byte Sll(byte val)
    {
        byte c = (byte)((val >> 7) & 1);
        byte res = (byte)((val << 1) | 1); // Undocumented: bit 0 set to 1
        F = (byte)(Sz53Table[res] | ParityTable[res] | c);
        return res;
    }

    private byte Srl(byte val)
    {
        byte c = (byte)(val & 1);
        byte res = (byte)(val >> 1);
        F = (byte)(Sz53Table[res] | ParityTable[res] | c);
        return res;
    }

    #endregion
}
