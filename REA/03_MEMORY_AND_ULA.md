# REA 03: Memory Reconstruction & Ferranti ULA Video Generation

**Target:** Ferranti ULA (6C001E-6 / 7C112E-4) & DRAM Subsystem  
**Resolution:** 256 × 192 pixels with 32 × 24 Attribute grid  
**Total Frame Cycles:** 69,888 T-states (312 scanlines @ 224 T-states/line)  

---

## 1. 64 KB Memory Map

```
0x0000 +---------------------------------------+
       |             16 KB ROM                 | Read-only (writes ignored)
0x3FFF +---------------------------------------+
0x4000 +---------------------------------------+
       |   6144-byte Screen Bitmap (VRAM)      | Contended RAM
0x57FF +---------------------------------------+
0x5800 +---------------------------------------+
       |   768-byte Colour Attributes          | Contended RAM
0x5AFF +---------------------------------------+
0x5B00 +---------------------------------------+
       |  Printer Buffer & System Variables    | Contended RAM
0x7FFF +---------------------------------------+
0x8000 +---------------------------------------+
       |        32 KB Upper RAM                | Uncontended RAM
0xFFFF +---------------------------------------+
```

---

## 2. Sinclair Non-Linear Bitmap Addressing

The Spectrum VRAM (`0x4000–0x57FF`) is laid out non-linearly to simplify row and character cell counter hardware in the Ferranti ULA.

A pixel coordinate `(X, Y)` where `X ∈ [0..255]` and `Y ∈ [0..191]`:
Let `Y` be decomposed into bits:
- `Y[7:6]`: Screen Third (`0..2`)
- `Y[5:3]`: Character Row within Third (`0..7`)
- `Y[2:0]`: Scanline within Character Row (`0..7`)
- `X[7:3]`: Character Column (`0..31`)
- `X[2:0]`: Bit position within byte (`7` down to `0`)

The memory address is formed as:
```text
Bit:   15 14 13 12 11 10  9  8   7  6  5  4  3  2  1  0
Value:  0  1  0 Y7 Y6 Y2 Y1 Y0  Y5 Y4 Y3 X4 X3 X2 X1 X0
```
- In hex: `0x4000 | ((Y & 0xC0) << 5) | ((Y & 0x07) << 8) | ((Y & 0x38) << 2) | (X >> 3)`.

---

## 3. Colour Attribute Format (`0x5800–0x5AFF`)

Each 8×8 pixel character cell has one attribute byte:
Address = `0x5800 + ((Y / 8) * 32) + (X / 8)`.

```text
Bit 7: FLASH  (0 = steady, 1 = flash ink and paper swap every 16 frames / ~0.32 s)
Bit 6: BRIGHT (0 = normal intensity, 1 = high intensity)
Bit 5..3: PAPER colour (0..7)
Bit 2..0: INK colour (0..7)
```

### Colour Palette (RGB Values):

| Index | Colour | Normal RGB | Bright RGB |
|---|---|---|---|
| 0 | Black | `0x00, 0x00, 0x00` | `0x00, 0x00, 0x00` |
| 1 | Blue | `0x00, 0x00, 0xC8` | `0x00, 0x00, 0xFF` |
| 2 | Red | `0xC8, 0x00, 0x00` | `0xFF, 0x00, 0x00` |
| 3 | Magenta | `0xC8, 0x00, 0xC8` | `0xFF, 0x00, 0xFF` |
| 4 | Green | `0x00, 0xC8, 0x00` | `0x00, 0xFF, 0x00` |
| 5 | Cyan | `0x00, 0xC8, 0xC8` | `0x00, 0xFF, 0xFF` |
| 6 | Yellow | `0xC8, 0xC8, 0x00` | `0xFF, 0xFF, 0x00` |
| 7 | White | `0xC8, 0xC8, 0xC8` | `0xFF, 0xFF, 0xFF` |

---

## 4. Ferranti ULA Video Timing & Memory Contention

### PAL Frame Timing:
- **Scanlines:** 312 lines per frame
  - Lines `0–63`: Top Border (64 lines)
  - Lines `64–255`: Active Display (192 lines)
  - Lines `256–311`: Bottom Border & Vertical Blank (56 lines)
- **T-states per line:** 224
- **Total T-states per frame:** 312 × 224 = 69,888

### Contention Timing Pattern:
During active display lines (`64–255`), the ULA fetches 2 bytes (one bitmap byte + one attribute byte) every 8 T-states for 128 T-states per line (from T-state 14336 to 57343):
- If the CPU attempts to access contended memory (`0x4000–0x7FFF`) or perform an I/O read/write to port `0xFE` during active fetching, the ULA holds the CPU `WAIT` line active.
- Delay cycle pattern across the 8 T-state window: `[6, 5, 4, 3, 2, 1, 0, 0]`.
