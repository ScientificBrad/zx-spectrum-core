# Reverse Engineering Analysis (REA): Sinclair ZX Spectrum 48K (1982)

**Researcher:** ScientificBrad  
**Series:** Historical Computer Reverse Engineering  
**Project:** 004  
**Target:** Sinclair ZX Spectrum 48K (1982 Issue 2 / Issue 3)  
**Host Architecture:** C# / .NET 8 on Windows 11 x64  
**Date:** November 2026  

---

## 1. Executive Summary & Mission Scope

The Sinclair ZX Spectrum 48K, released in April 1982 by Sinclair Research Ltd, represents one of the most influential personal computing architectures in history. Engineered under extreme cost constraints by Richard Altwasser (hardware) and Dr. Steve Vickers of Nine Tiles (firmware / BASIC), the system eliminated separate display controllers, sound chips, and complex glue logic by concentrating video display timing, memory multiplexing, audio I/O, and keyboard scanning into a single custom Ferranti Uncommitted Logic Array (ULA - 6C001E-6).

The objective of Project 004 is to conduct a forensic reverse engineering analysis of the genuine 16 KB Sinclair ROM and construct a cycle-accurate, clean-room hardware emulator that boots the verified original firmware, executes Sinclair BASIC, supports cycle-accurate video and sound generation, and loads genuine tape software through signal-level and block-level emulation.

---

## 2. Hardware Architecture Baseline

```
+-------------------------------------------------------------------------+
|                        ZILOG Z80A CPU (3.50000 MHz)                     |
+-------------------------------------------------------------------------+
                                    |
                    +---------------+---------------+
                    |                               |
          +---------v---------+           +---------v---------+
          |   Uncontended     |           |    Contended      |
          |  Upper 32 KB RAM  |           |   Lower 16 KB RAM |
          |   (0x8000-0xFFFF) |           |   (0x4000-0x7FFF) |
          +-------------------+           +---------+---------+
                                                    |
                                          +---------v---------+
                                          |   Ferranti ULA    |
                                          | (Display/VRAM/IO) |
                                          +---------+---------+
                                                    |
      +-----------------+---------------------------+---------------------+
      |                 |                           |                     |
+-----v-----+     +-----v-----+               +-----v-----+         +-----v-----+
| 16 KB ROM |     | Keyboard  |               |  Speaker  |         | Cassette  |
| (0x0000-  |     | 8x5 Matrix|               | (1-bit IO)|         | EAR / MIC |
|  0x3FFF)  |     | Port 0xFE |               | Port 0xFE |         | Port 0xFE |
+-----------+     +-----------+               +-----------+         +-----------+
```

### Key Technical Specifications:
- **Processor:** Zilog Z80A running at 3.50000 MHz (PAL clock 14.00000 MHz / 4).
- **ROM:** 16,384 bytes (16 KB) at `0x0000–0x3FFF`.
- **Contended RAM:** 16,384 bytes at `0x4000–0x7FFF` (contains 6144-byte bitmap + 768-byte attributes + system variables). Shared with ULA.
- **Uncontended RAM:** 32,768 bytes at `0x8000–0xFFFF` (two 16 KB banks, OKI 3732 / TI 4532 DRAMs).
- **Video Display:** 256 × 192 pixels, 32 × 24 colour attribute blocks (8×8 pixels per block), 15 colours (8 normal, 7 bright + black, flash cycling at ~1.56 Hz).
- **Frame Timing:** 312 lines total, 224 T-states per line = 69,888 T-states per frame (~50.088 Hz).
- **Audio:** 1-bit beeper toggled via bit 4 of ULA port `0xFE`.
- **Keyboard:** 40 keys arranged in an 8-row × 5-column matrix, read via port `0xFE` with upper address lines A8–A15 selecting rows.

---

## 3. Investigation Methodology (REA)

1. **Firmware Forensics:** Binary static analysis of `48.rom`, mapping RST vectors, interrupt routines, character generator, token tables, floating-point calculator, and memory size probing.
2. **Clean-Room Z80 Core:** Exhaustive implementation of all official and undocumented Z80 instructions, 8-bit/16-bit ALU flags (including undocumented bits 3 and 5: XF and YF), prefix chaining (DD/FD/ED/CB/DDCB/FDCB), interrupt modes (IM 0, 1, 2), and T-state cycle accounting.
3. **Memory Bus & Contention:** Cycle-accurate arbitration between Z80 and ULA for `0x4000–0x7FFF` during active screen generation.
4. **Ferranti ULA Video Engine:** Bit-accurate non-linear scanline rendering, attribute decoding, border rendering, and 50 Hz frame synchronization.
5. **Authentic Firmware Boot:** Observing cold reset, memory sizing down from 0xFFFF, stack initialization, system variables setup at 0x5C00, screen clearing, and printing the copyright message.
6. **Cassette Subsystem:** TAP image parsing and signal-level EAR bit transition generation matching ROM loader expectations (`LD-BYTES` at 0x0556).
