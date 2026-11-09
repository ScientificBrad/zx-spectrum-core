namespace Spectrum.Diagnostics;

/// <summary>
/// Disassembler for Z80 microprocessor instructions.
/// Decodes opcodes, prefixes (DD, FD, ED, CB, DDCB, FDCB), displacements, and operands.
/// </summary>
public static class Z80Disassembler
{
    public static (string Disassembly, int Length) Disassemble(Func<ushort, byte> readByte, ushort address)
    {
        byte op = readByte(address);

        if (op == 0xCB)
        {
            byte cbOp = readByte((ushort)(address + 1));
            return ($"CB {cbOp:X2}  {DisassembleCb(cbOp)}", 2);
        }

        if (op == 0xED)
        {
            byte edOp = readByte((ushort)(address + 1));
            return ($"ED {edOp:X2}  {DisassembleEd(edOp, readByte, (ushort)(address + 2))}", 2);
        }

        if (op == 0xDD || op == 0xFD)
        {
            string reg = (op == 0xDD) ? "IX" : "IY";
            byte nextOp = readByte((ushort)(address + 1));
            if (nextOp == 0xCB)
            {
                sbyte d = (sbyte)readByte((ushort)(address + 2));
                byte cbOp = readByte((ushort)(address + 3));
                return ($"{op:X2} CB {d:X2} {cbOp:X2}  BITOP ({reg}+{d})", 4);
            }
            if (nextOp == 0x21) // LD reg, nn
            {
                ushort imm = (ushort)(readByte((ushort)(address + 2)) | (readByte((ushort)(address + 3)) << 8));
                return ($"{op:X2} 21 {imm:X4}  LD {reg}, 0x{imm:X4}", 4);
            }
            return ($"{op:X2} {nextOp:X2}  {reg} op 0x{nextOp:X2}", 2);
        }

        // Standard opcodes
        switch (op)
        {
            case 0x00: return ("NOP", 1);
            case 0x01: return ($"LD BC, 0x{ReadWord(readByte, address + 1):X4}", 3);
            case 0x02: return ("LD (BC), A", 1);
            case 0x03: return ("INC BC", 1);
            case 0x04: return ("INC B", 1);
            case 0x05: return ("DEC B", 1);
            case 0x06: return ($"LD B, 0x{readByte((ushort)(address + 1)):X2}", 2);
            case 0x07: return ("RLCA", 1);
            case 0x08: return ("EX AF, AF'", 1);
            case 0x09: return ("ADD HL, BC", 1);
            case 0x0A: return ("LD A, (BC)", 1);
            case 0x0B: return ("DEC BC", 1);
            case 0x0C: return ("INC C", 1);
            case 0x0D: return ("DEC C", 1);
            case 0x0E: return ($"LD C, 0x{readByte((ushort)(address + 1)):X2}", 2);
            case 0x0F: return ("RRCA", 1);

            case 0x10: return ($"DJNZ 0x{(ushort)(address + 2 + (sbyte)readByte((ushort)(address + 1))):X4}", 2);
            case 0x11: return ($"LD DE, 0x{ReadWord(readByte, address + 1):X4}", 3);
            case 0x12: return ("LD (DE), A", 1);
            case 0x13: return ("INC DE", 1);
            case 0x14: return ("INC D", 1);
            case 0x15: return ("DEC D", 1);
            case 0x16: return ($"LD D, 0x{readByte((ushort)(address + 1)):X2}", 2);
            case 0x17: return ("RLA", 1);
            case 0x18: return ($"JR 0x{(ushort)(address + 2 + (sbyte)readByte((ushort)(address + 1))):X4}", 2);
            case 0x19: return ("ADD HL, DE", 1);
            case 0x1A: return ("LD A, (DE)", 1);
            case 0x1B: return ("DEC DE", 1);
            case 0x1C: return ("INC E", 1);
            case 0x1D: return ("DEC E", 1);
            case 0x1E: return ($"LD E, 0x{readByte((ushort)(address + 1)):X2}", 2);
            case 0x1F: return ("RRA", 1);

            case 0x20: return ($"JR NZ, 0x{(ushort)(address + 2 + (sbyte)readByte((ushort)(address + 1))):X4}", 2);
            case 0x21: return ($"LD HL, 0x{ReadWord(readByte, address + 1):X4}", 3);
            case 0x22: return ($"LD (0x{ReadWord(readByte, address + 1):X4}), HL", 3);
            case 0x23: return ("INC HL", 1);
            case 0x28: return ($"JR Z, 0x{(ushort)(address + 2 + (sbyte)readByte((ushort)(address + 1))):X4}", 2);
            case 0x2A: return ($"LD HL, (0x{ReadWord(readByte, address + 1):X4})", 3);

            case 0x31: return ($"LD SP, 0x{ReadWord(readByte, address + 1):X4}", 3);
            case 0x32: return ($"LD (0x{ReadWord(readByte, address + 1):X4}), A", 3);
            case 0x3A: return ($"LD A, (0x{ReadWord(readByte, address + 1):X4})", 3);
            case 0x3E: return ($"LD A, 0x{readByte((ushort)(address + 1)):X2}", 2);

            case 0x76: return ("HALT", 1);
            case 0xAF: return ("XOR A", 1);
            case 0xC3: return ($"JP 0x{ReadWord(readByte, address + 1):X4}", 3);
            case 0xC9: return ("RET", 1);
            case 0xCD: return ($"CALL 0x{ReadWord(readByte, address + 1):X4}", 3);
            case 0xD3: return ($"OUT (0x{readByte((ushort)(address + 1)):X2}), A", 2);
            case 0xDB: return ($"IN A, (0x{readByte((ushort)(address + 1)):X2})", 2);
            case 0xF3: return ("DI", 1);
            case 0xFB: return ("EI", 1);

            default:
                return ($"OP 0x{op:X2}", 1);
        }
    }

    private static ushort ReadWord(Func<ushort, byte> readByte, int addr)
    {
        return (ushort)(readByte((ushort)addr) | (readByte((ushort)(addr + 1)) << 8));
    }

    private static string DisassembleCb(byte op)
    {
        string[] regs = { "B", "C", "D", "E", "H", "L", "(HL)", "A" };
        int reg = op & 7;
        int bit = (op >> 3) & 7;

        if (op < 0x40) return $"SHIFT {op >> 3}, {regs[reg]}";
        if (op < 0x80) return $"BIT {bit}, {regs[reg]}";
        if (op < 0xC0) return $"RES {bit}, {regs[reg]}";
        return $"SET {bit}, {regs[reg]}";
    }

    private static string DisassembleEd(byte op, Func<ushort, byte> readByte, ushort addr)
    {
        return op switch
        {
            0xA0 => "LDI",
            0xB0 => "LDIR",
            0xA1 => "CPI",
            0xB1 => "CPIR",
            0x47 => "LD I, A",
            0x57 => "LD A, I",
            0x4F => "LD R, A",
            0x5F => "LD A, R",
            0x56 => "IM 1",
            0x5E => "IM 2",
            0x46 => "IM 0",
            0x45 => "RETN",
            0x4D => "RETI",
            _ => $"ED 0x{op:X2}"
        };
    }
}
