# REA 02: Zilog Z80A CPU Architecture & Emulation Requirements

**Target:** Zilog Z80A Microprocessor  
**Frequency:** 3.50000 MHz (Clock period: ~285.71 ns per T-state)  
**Word Length:** 8-bit Data Bus, 16-bit Address Bus  

---

## 1. Register Architecture

The Z80 features an extensive register set, including shadow register sets accessible via exchange instructions:

```
Main Register Set        Alternate Register Set
+---------+---------+   +---------+---------+
|  A (8)  |  F (8)  |   |  A' (8) |  F' (8) |   Accumulator & Flags
+---------+---------+   +---------+---------+
|  B (8)  |  C (8)  |   |  B' (8) |  C' (8) |   BC register pair / Counter
+---------+---------+   +---------+---------+
|  D (8)  |  E (8)  |   |  D' (8) |  E' (8) |   DE register pair / Destination
+---------+---------+   +---------+---------+
|  H (8)  |  L (8)  |   |  H' (8) |  L' (8) |   HL register pair / Pointer
+---------+---------+   +---------+---------+

Index & Pointer Registers
+-------------------+
|      IX (16)      |   Index Register X
+-------------------+
|      IY (16)      |   Index Register Y
+-------------------+
|      SP (16)      |   Stack Pointer
+-------------------+
|      PC (16)      |   Program Counter
+-------------------+

Special Registers
+---------+---------+
|  I (8)  |  R (8)  |   Interrupt Vector Base (I) & Memory Refresh (R)
+---------+---------+
```

---

## 2. Flag Register (F) Layout

The 8-bit Flag register contains both standard documented condition codes and undocumented bus bits:

| Bit | Name | Flag | Description |
|---|---|---|---|
| 7 | **S** | Sign | Set if result is negative (MSB = 1) |
| 6 | **Z** | Zero | Set if result is zero |
| 5 | **YF** | Undocumented (Bit 5) | Copy of bit 5 of the ALU result / accumulator |
| 4 | **H** | Half Carry | Set if carry occurred from bit 3 to bit 4 |
| 3 | **XF** | Undocumented (Bit 3) | Copy of bit 3 of the ALU result / accumulator |
| 2 | **P/V**| Parity / Overflow | Parity (even number of 1s in logical ops) or Two's complement overflow in arithmetic |
| 1 | **N** | Add/Subtract | 0 for addition, 1 for subtraction (used by DAA) |
| 0 | **C** | Carry | Set if carry occurred from bit 7 (or borrow) |

---

## 3. Instruction Prefixes & Decoding

The Z80 uses a multi-level instruction decoding scheme based on 1-byte opcodes and prefix extensions:
- **Base (0x00–0xFF):** 8-bit primary opcodes.
- **`0xCB`:** Bit manipulation instructions (`RLC`, `RRC`, `RL`, `RR`, `SLA`, `SRA`, `SLL`, `SRL`, `BIT`, `RES`, `SET`).
- **`0xED`:** Extended instructions (`ADC/SBC 16-bit`, block moves `LDI/LDIR/LDD/LDDR`, block compares `CPI/CPIR`, block I/O `INI/INIR/OUTI/OTIR`, `IM 0/1/2`, `LD I,A / LD A,I / LD R,A / LD A,R`, `RETI/RETN`).
- **`0xDD`:** IX index register replacement prefix. Replaces HL with IX in base and CB instructions.
- **`0xFD`:** IY index register replacement prefix. Replaces HL with IY in base and CB instructions.
- **`0xDDCB d op` / `0xFDCB d op`:** Indexed bit instructions. Displacement byte `d` precedes the opcode byte!

### Prefix Rules:
1. Multiple repeated prefixes: repeated `0xDD` or `0xFD` prefixes do not stack; each consumes 4 T-states and only the last active prefix takes effect before the instruction.
2. If `0xDD` or `0xFD` is followed by an instruction that does not access HL (e.g. `NOP`, `LD A, B`), the prefix is effectively ignored except for adding 4 T-states.
3. Undocumented IX/IY 8-bit halves: `IXH`, `IXL`, `IYH`, `IYL` can be accessed using `0xDD` / `0xFD` on instructions referencing H or L (e.g., `LD IXH, A`, `ADD A, IYL`).

---

## 4. Interrupt Handling

### Interrupt Modes:
- **IM 0:** External device places an instruction (usually `RST n`) on the data bus.
- **IM 1:** CPU automatically executes `RST 38H` (Call to `0x0038`). This is the **standard ZX Spectrum mode**. The Ferranti ULA generates an interrupt every 20ms (50 Hz), triggering `0x0038`.
- **IM 2:** Vectored interrupts. Target address is read from `(I << 8) | data_bus`.

### Interrupt Enable Flip-Flops:
- `IFF1`: Active interrupt enable flag. When reset, maskable interrupts are disabled.
- `IFF2`: Temporary storage used during NMI to preserve `IFF1`.
- `EI` instruction delay: An interrupt cannot be accepted immediately following an `EI` instruction; the interrupt is delayed until after the *next* instruction completes.
