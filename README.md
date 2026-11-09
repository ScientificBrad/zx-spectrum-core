# Project 004: Sinclair ZX Spectrum 48K (1982)
## Complete Firmware Reconstruction, Reverse Engineering Analysis & Cycle-Accurate Emulation

**Researcher:** ScientificBrad  
**Series:** Historical Computer Reverse Engineering  
**Project:** 004  
**Target Architecture:** Sinclair ZX Spectrum 48K (Issue 2/3, 1982)  
**Implementation:** C# / .NET 8 (Host OS: Windows 11 x64)  
**Verification Standard:** Genuine Sinclair ROM execution & TAP tape loading via physical signal pulses.

---

## 1. Executive Summary

This project delivers a complete, clean-room reconstruction and cycle-accurate emulator of the legendary **1982 Sinclair ZX Spectrum 48K**, following the rigorous **Reverse Engineering Analysis (REA)** methodology.

### Core Engineering Principles
- **Clean-Room Design:** All components—Z80A CPU, Ferranti ULA, memory contention, 8×5 keyboard matrix, 1-bit beeper, and physical tape loader—are implemented from hardware documentation and forensic ROM disassemblies without importing foreign emulator code.
- **Genuine ROM Execution:** Zero hardcoded boot screens or simulated BASIC outputs. All characters, tokens, expressions, and memory maps originate strictly from authentic Z80 machine code execution of the original 16 KB Sinclair system ROM (`48.rom`).
- **Physical Cassette Loading:** Software in `.tap` format is loaded directly through the ROM's `LD-BYTES` routine (`0x0556`) using physical cycle-accurate edge pulses fed to the EAR bit of port `0xFE`.
- **High Performance:** Over **2,400 FPS** (>48× real-time speed) in headless CLI mode on standard modern x64 hardware; 50.088 Hz frame-locked PAL pacing with real-time WinMM audio during GUI operation.

---

## 2. Emulated Hardware Architecture

```text
┌────────────────────────────────────────────────────────────────────────┐
│                          Spectrum.Emulator                             │
│       (Windows GUI: WinForms GDI+ Blitter, WinMM WaveOut Audio)        │
└───────────────────────────────────┬────────────────────────────────────┘
                                    │
┌───────────────────────────────────▼────────────────────────────────────┐
│                           SpectrumMachine                              │
│              (System Coordinator, Frame Loop, Key Injector)            │
├───────────────────┬───────────────────┬────────────────────────────────┤
│      Z80Cpu       │    FerrantiUla    │          TapePlayer            │
│  (Z80A Processor) │  (Video & Audio)  │   (Physical Signal Pulser)     │
└─────────┬─────────┴─────────┬─────────┴────────────────┬───────────────┘
          │                   │                          │
          └───────────────────┼──────────────────────────┘
                              │
┌─────────────────────────────▼──────────────────────────────────────────┐
│                             SpectrumBus                                │
│       (64 KB Address Space, Port 0xFE Decoding, Contention Delay)     │
├─────────────────────┬─────────────────────┬────────────────────────────┤
│     16 KB ROM       │ 16 KB Contended RAM │    32 KB Uncontended RAM   │
│   0x0000–0x3FFF     │    0x4000–0x7FFF    │        0x8000–0xFFFF       │
└─────────────────────┴─────────────────────┴────────────────────────────┘
```

- **Microprocessor:** Zilog Z80A running at 3.500000 MHz. Implements all base opcodes, `CB`/`ED`/`DD`/`FD`/`DDCB`/`FDCB` prefixes, undocumented flags (XF/YF), and interrupt modes IM 0, IM 1 (`0x0038`), and IM 2.
- **Memory Subsystem:** 64 KB flat address space:
  - `0x0000–0x3FFF`: 16 KB Sinclair ROM (write-protected).
  - `0x4000–0x7FFF`: 16 KB contended lower RAM (display bitmap `0x4000–0x57FF`, attributes `0x5800–0x5AFF`, and system variables `0x5C00–0x5CB6`).
  - `0x8000–0xFFFF`: 32 KB uncontended upper RAM.
- **Ferranti ULA Graphics:**
  - 256×192 active display with non-linear bitmap layout + 320×240 standard border frame.
  - 32×24 colour attribute grid with 8 standard colours, BRIGHT intensity, and FLASH attribute cycling.
  - PAL frame timing: 224 T-states per scanline, 312 lines, exactly 69,888 T-states per frame (~50.088 Hz).
  - Accurate 8-cycle memory contention delays during active raster scan.
- **Keyboard:** 8-row × 5-column active-low matrix mapped to port `0xFE` with modern PC key mapping and automatic token generation.
- **Sound & Storage:** 1-bit speaker output buffered at 44.1 kHz via WinMM WaveOut; physical cycle-accurate pulse synthesizer for cassette tapes.

---

## 3. Verified System ROM

The emulator runs the verified 1982 Sinclair ZX Spectrum 48K system ROM:
- **File:** `roms/48.rom`
- **Size:** 16,384 bytes
- **SHA-1:** `5ea7c2b824672e914525d1d5c419d71b84a426a2`
- **MD5:** `4c42a2f075212361c3117015b107ff68`

---

## 4. Key REA Discoveries & Findings

1. **Dummy Token at 0x0095 (FND-021):**
   The ROM token table begins at `0x0095` with a dummy `?` byte (`0xBF`). Routine `0x0C41` (`INC A`) automatically skips this entry, mapping token `0xA5` to `RND` at `0x0096` and `BORDER` to `0xE7`.
2. **Cassette Tail Pulse Edge Requirement (FND-022):**
   ROM routine `LD-BYTES` reads the 19th checksum byte via `LD-EDGE-2` (`0x05E3`). If tape playback cuts off immediately without emitting a post-checksum closing edge, register `B` wraps from 0x00 to 0xFF, triggering an edge timeout and failing with `Carry=False`. Providing a 1,000 T-state tail pulse guarantees reliable loading.
3. **Sinclair BASIC 6-Byte Numeric Format (FND-023):**
   In tokenized BASIC programs, numbers are not stored merely as ASCII text. Every numeric literal must be immediately followed by `0x0E` and a 5-byte binary format (`[0x0E, 0x00, 0x00, low, high, 0x00]`).
4. **Power-On Memory Sizing (FND-024):**
   The power-on reset loop counts down from `0xFFFF` to `0x4000` writing `0x02` and testing readback. On a 48K machine, physical top of RAM is stored in `P_RAMT` at `0x5CB4` as `0xFFFF`, while `RAMTOP` at `0x5CB2` is initialized to `0xFF57` to protect UDGs.

---

## 5. Software Compatibility

| Title / Software | Media | Load Method | Status | Verified Evidence |
|:---|:---|:---|:---|:---|
| **Sinclair BASIC 48K** | ROM | Cold Boot | **VERIFIED PASS** | Boots in ~81 frames, sizes 48K RAM, renders `(C) 1982 Sinclair Research Ltd` (`screenshots/boot_test.bmp`). |
| **Interactive CLI** | Matrix | Port 0xFE | **VERIFIED PASS** | Types `PRINT 42` and ENTER; renders `42` and report code `0 OK, 0:1` (`screenshots/basic_interactive.bmp`). |
| **The Matrix (1982)** | `.tap` | `LD-BYTES` | **VERIFIED PASS** | Loads 2 blocks via EAR signal, sets `BORDER 0`, renders green digital rain (`screenshots/matrix_game.bmp`). |
| **Lunar Lander (1982)**| `.tap` | `LD-BYTES` | **VERIFIED PASS** | Loads 2 blocks, accepts live keyboard input ('A' thruster, 'P' steering), renders multi-colour landing pad (`screenshots/lunar_lander_game.bmp`). |
| **Project 004 Demo** | `.tap` | `LD-BYTES` | **VERIFIED PASS** | Loads header and data, executes autostart line 10, sets red border (`BORDER 2`) (`screenshots/tape_load.bmp`). |

---

## 6. Building and Running

### Prerequisites
- Windows 10/11 x64
- Microsoft .NET 8.0 SDK

### Build
```powershell
dotnet build -c Release
```

### Run Tests (20 Automated Tests)
```powershell
dotnet test
```

### Launch Interactive GUI
```powershell
dotnet run --project src/Spectrum.Emulator -c Release -- --rom roms/48.rom
```

### Launch Headless Benchmark / Batch Screenshot
```powershell
dotnet run --project src/Spectrum.Emulator -c Release -- --rom roms/48.rom --tape games/matrix.tap --headless --frames 600 --screenshot screenshots/cli_matrix.bmp
```

---

## 7. Project Structure

```text
ZXSpectrum/
├── src/
│   ├── Spectrum.Core/          # Z80A CPU, Ferranti ULA, Bus, Keyboard, Beeper, Tape
│   ├── Spectrum.Diagnostics/   # Disassembler, ExecutionTracer, BmpWriter
│   └── Spectrum.Emulator/      # Windows Forms GUI & CLI runner
├── tests/
│   └── Spectrum.Tests/         # 20 automated unit & integration tests
├── docs/                       # Architecture, Hardware Reference, Building, Compatibility
├── REA/                        # Reverse Engineering Analysis reports and Findings Ledger
├── games/                      # Verified public-domain TAP games (Matrix, Lander, Demo)
├── roms/                       # Sinclair ROM images (git-ignored for clean-room distribution)
├── tools/                      # ROM forensic disassembler and TAP generator scripts
└── screenshots/                # Real emulated framebuffer captures
```

---

## 8. Licence

This project is licensed under the MIT Licence. See [LICENSE](LICENSE) for details.  
Third-party notices and historical hardware attributions are documented in [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md).
