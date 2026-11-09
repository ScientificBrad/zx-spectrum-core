# REA 07: Undocumented Z80 & Ferranti ULA Hardware Anomalies

**Project:** 004  
**Scope:** Documented vs. Physical Silicon Hardware Discrepancies  

---

## 1. Undocumented Z80 Flag Register Bits (XF / Bit 3, YF / Bit 5)

In physical Z80 silicon, internal data bus bits 3 and 5 are leaked into the F register during 8-bit arithmetic, logical operations, and certain 16-bit block instructions:
- `BIT b, (HL)` sets XF and YF from the high-order byte of the memory pointer `HL`.
- `CPI / CPIR / CPD / CPDR` calculate flag bits based on `(A - (HL)) - H_flag`, where bit 1 is set if `(A - (HL) - H)` bit 1 is 1, and bit 3 is taken from bit 3 of `A - (HL) - H`.

Our Z80 emulator implements full lookup tables and bit manipulation for XF and YF to guarantee full compliance with Z80 qualification tests (ZEXDOC/ZEXALL).

---

## 2. Undocumented DDCB / FDCB Instructions

When the Z80 executes a `0xDD 0xCB d op` or `0xFD 0xCB d op` sequence:
- The displacement byte `d` is always read first, followed by the opcode `op`.
- For opcodes that normally operate on register `(HL)` and store into register `r` (`0x00–0x3F`, `0x80–0xFF`):
  - In addition to modifying `(IX+d)` or `(IY+d)`, the silicon also copies the modified byte into the register encoded in bits 0–2 (`B`, `C`, `D`, `E`, `H`, `L`, `A`).
  - Example: `0xDD 0xCB 0x05 0x06` (`RLC (IX+5)`) simultaneously copies the result into register `B`.

---

## 3. ULA Floating Bus Reading

When reading an unused I/O port or port `0xFF`:
- If the ULA is actively rendering the display (between scanlines 64 and 255, and between cycle 14336 and 57343):
  - The CPU reads whichever byte the ULA has placed onto the memory bus (either a bitmap byte or attribute byte).
- If the ULA is in the border or vertical blanking period:
  - The floating bus returns `0xFF` (pulled high by bus termination resistors).
- Several Spectrum games (such as *Arkanoid*, *Sidewize*, and *Cobra*) rely on the floating bus to detect raster beam position without using interrupts.
