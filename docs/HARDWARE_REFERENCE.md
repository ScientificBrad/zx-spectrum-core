# Sinclair ZX Spectrum 48K Hardware Reference

## 1. Technical Specifications

| Parameter | Specification |
|:---|:---|
| **Release Year** | 1982 |
| **Manufacturer** | Sinclair Research Ltd |
| **CPU** | Zilog Z80A @ 3.500000 MHz (PAL) |
| **Video Display Processor** | Ferranti Custom ULA (6C001 / 7C001) |
| **System ROM** | 16 KB (0x0000–0x3FFF) |
| **System RAM** | 48 KB (0x4000–0xFFFF) |
| **Contended Memory** | 0x4000–0x7FFF (Lower 16 KB RAM) |
| **Uncontended Memory** | 0x8000–0xFFFF (Upper 32 KB RAM) |
| **Resolution** | 256 × 192 pixels (active graphics) |
| **Attribute Grid** | 32 × 24 character cells (8×8 pixels per attribute cell) |
| **Palette** | 8 base colours with 2 brightness levels (16 total colours) |
| **Frame Timing** | 312 lines, 224 T-states/line, 69,888 T-states/frame (~50.088 Hz) |
| **Sound** | 1-bit internal beeper / speaker via port 0xFE |
| **Storage Interface** | Standard cassette audio I/O via 3.5mm EAR/MIC jacks (~1,500 baud) |

---

## 2. Memory Map & Video Layout

```text
+-----------------------+ 0xFFFF
|                       |
|   32 KB Upper RAM     | Uncontended RAM (0x8000–0xFFFF)
|                       |
+-----------------------+ 0x8000
|                       |
|   Colour Attributes   | 768 bytes (0x5800–0x5AFF)
|   Bitmap Graphics     | 6,144 bytes (0x4000–0x57FF)
|   Lower 16 KB RAM     | Contended RAM (0x4000–0x7FFF)
+-----------------------+ 0x4000
|                       |
|   16 KB Sinclair ROM  | 16,384 bytes (0x0000–0x3FFF)
|                       | Read-only system firmware
+-----------------------+ 0x0000
```

### Video Bitmap Addressing
The 6,144-byte bitmap (`0x4000–0x57FF`) uses non-linear interlacing:
- Address format: `010H HSSS RRRV VVVV`
  - `H` (bits 12-11): Third of the screen (0, 1, or 2)
  - `S` (bits 10-8): Pixel scanline within 8-pixel character row (0–7)
  - `R` (bits 7-5): Character row within screen third (0–7)
  - `V` (bits 4-0): Character column (0–31)

### Attribute Byte Layout (`0x5800–0x5AFF`)
Each byte controls one 8×8 pixel block:
- **Bit 7**: FLASH (1 = alternate INK and PAPER every 16 frames / ~0.32s)
- **Bit 6**: BRIGHT (1 = high intensity colours)
- **Bits 5-3**: PAPER (background colour index 0–7)
- **Bits 2-0**: INK (foreground colour index 0–7)

---

## 3. I/O Port 0xFE

Port `0xFE` is decoded whenever address bit `A0` is 0.

### Read Cycle (`IN A, (0xFE)`)
- **Address Bits A8–A15**: Select keyboard half-rows (active-low):
  - `0xFE`: `SHIFT`, `Z`, `X`, `C`, `V`
  - `0xFD`: `A`, `S`, `D`, `F`, `G`
  - `0xFB`: `Q`, `W`, `E`, `R`, `T`
  - `0xF7`: `1`, `2`, `3`, `4`, `5`
  - `0xEF`: `0`, `9`, `8`, `7`, `6`
  - `0xDF`: `P`, `O`, `I`, `U`, `Y`
  - `0xBF`: `ENTER`, `L`, `K`, `J`, `H`
  - `0x7F`: `SPACE`, `SYM SHIFT`, `M`, `N`, `B`
- **Data Bus Bits**:
  - `D0–D4`: Key states (0 = pressed, 1 = released).
  - `D6`: Cassette EAR input signal.

### Write Cycle (`OUT (0xFE), A`)
- **Bits 0–2**: Border colour (0–7).
- **Bit 3**: MIC output (cassette tape recording).
- **Bit 4**: Speaker beeper toggle (1 = on, 0 = off).

---

## 4. Memory & I/O Contention
The ULA accesses contended RAM (`0x4000–0x7FFF`) during the active display area.
When the CPU accesses this memory region during scanlines 64 through 255 and between cycle offsets 14388 and 56980 of each frame, the ULA delays CPU execution according to the contention table:
```csharp
int[] ContentionDelays = { 6, 5, 4, 3, 2, 1, 0, 0 };
```
The exact delay added depends on `(cycle_offset) % 8`.
