# Game & Software Compatibility

## 1. Overview
The emulator strictly executes authentic software images without BIOS hooks or simulated shortcuts. Programs are loaded via the original Sinclair ROM tape loading routine (`LD-BYTES` at address `0x0556`) using physical cycle-accurate edge pulses fed through the emulated EAR input on port `0xFE`.

---

## 2. Verified Titles

| Title | Media Format | Loading Method | Autostart | Status | Notes |
|:---|:---|:---|:---|:---|:---|
| **The Matrix (1982)** | `.tap` (2 blocks) | Authentic `LD-BYTES` | Line 10 | **VERIFIED PASS** | Matrix digital rain effect with green INK pixels, Black border (`BORDER 0`), full BASIC interpreter token dispatch. |
| **Lunar Lander (1982)** | `.tap` (2 blocks) | Authentic `LD-BYTES` | Line 10 | **VERIFIED PASS** | Interactive lander simulator with thruster inputs (`A`), lateral steering (`P`), and multi-colour attribute graphics. |
| **Project 004 Demo** | `.tap` (2 blocks) | Authentic `LD-BYTES` | Line 10 | **VERIFIED PASS** | Test verification program changing border to red (`BORDER 2`) and printing REA confirmation banners. |

---

## 3. Tape Loading Mechanics

### ROM Routine Execution Path
1. The user (or interactive test injector) types `LOAD ""` and presses Enter.
2. The Sinclair BASIC interpreter vectors to `0x075A` (`SA-CONTRL` / `LD-CONTRL`), which prepares IX and DE for the tape block header.
3. The ROM calls `0x0556` (`LD-BYTES`):
   - Waits for EAR transition signal via `0x05E7` (`LD-EDGE-1`) and `0x05E3` (`LD-EDGE-2`).
   - Verifies the pilot tone (~8,063 pulses for header, ~3,223 pulses for data).
   - Detects the sync pulses (667 T-states and 735 T-states).
   - Loops reading 8 bits per byte, updating XOR parity checksum.
   - Requires a trailing tail pulse (~1,000 T-states) after the checksum to prevent `LD-EDGE-2` counter timeout (`B` decrementing to 0).
   - Returns with `Carry=True` on successful checksum match.
4. If the loaded block is a program with an autostart line set (`NEWPPC` <= 9999), the interpreter sets `PPC` and immediately enters line execution.
