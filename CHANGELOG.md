# Changelog: Project 004 - Sinclair ZX Spectrum 48K Reconstruction

All notable changes and milestones for Project 004 are documented below.

## [1.0.0] - 2026-11-09

### Milestone 1: System Initialisation & Clean-Room Architecture
- Created modern .NET 8 solution `Spectrum.sln` with 4 projects:
  - `Spectrum.Core`: Hardware processor, video display, memory bus, keyboard, beeper, and cassette tape emulator.
  - `Spectrum.Diagnostics`: Z80 disassembler, instruction execution tracer, and BMP framebuffer exporter.
  - `Spectrum.Emulator`: Windows Forms GUI host and high-performance headless CLI runner.
  - `Spectrum.Tests`: Complete xUnit automated test suite.
- Configured `.gitignore` to preserve clean-room legal distribution standards, excluding proprietary ROM binaries and build artifacts.
- Established `LICENSE` (MIT) and `THIRD_PARTY_NOTICES.md`.

### Milestone 2: Firmware Archaeology & Reverse Engineering
- Located and verified original 16 KB Sinclair ZX Spectrum system ROM (`roms/48.rom`):
  - SHA-1: `5ea7c2b824672e914525d1d5c419d71b84a426a2`
  - MD5: `4c42a2f075212361c3117015b107ff68`
- Authored `tools/rom_forensics.py` to forensically map all restart vectors, token tables, font data, error messages, and system variable layouts.
- Produced 9 in-depth REA technical reports in `REA/` and address-indexed `FINDINGS_LEDGER.md`.

### Milestone 3: Z80A CPU Implementation & Microarchitectural Verification
- Implemented reusable, clean-room `Z80Cpu.cs`:
  - Main instruction set (8-bit ALU, 16-bit register pairs, load/store, conditional branches).
  - Prefix handlers: `CB` (bit operations), `ED` (extended instructions), `DD`/`FD` (IX/IY indexed), and `DDCB`/`FDCB` indexed bit manipulation.
  - Cycle-exact precomputed flag tables for S, Z, YF, H, XF, P/V, N, and C flags.
  - Interrupt modes IM 0, IM 1 (vector to `0x0038`), IM 2, and non-maskable interrupt (NMI vector to `0x0066`).
- Verified instruction dispatch, push/pop stack operations, relative jump conditions, and ALU calculations via automated unit tests.

### Milestone 4: Ferranti ULA & Contended Memory Subsystem
- Implemented `FerrantiUla.cs`:
  - 256×192 active display with non-linear bitmap address translation (`0x4000–0x57FF`).
  - 32×24 colour attribute decoding (`0x5800–0x5AFF`) with 16-colour palette, BRIGHT intensity, and FLASH attribute phase cycling.
  - Strict PAL raster timing: 224 T-states per line, 312 lines, exactly 69,888 T-states per frame (~50.088 Hz).
  - Accurate 8-cycle memory contention delays during active raster scanning.
- Implemented `SpectrumBus.cs` managing 64 KB memory space:
  - 16 KB write-protected ROM (`0x0000–0x3FFF`).
  - 16 KB contended lower RAM (`0x4000–0x7FFF`).
  - 32 KB uncontended upper RAM (`0x8000–0xFFFF`).
  - Partial I/O port `0xFE` decoding.

### Milestone 5: Audio & Cassette Physical Pulse Synthesis
- Implemented `Beeper.cs`: 1-bit audio buffering at 44.1 kHz via WinMM WaveOut.
- Implemented `TapePlayer.cs` & `TapFile.cs`:
  - Cycle-accurate physical pulse synthesizer generating pilot tone (2,168 T-states), sync pulses (667 & 735 T-states), data bits (855 & 1,710 T-states), and tail pulse (1,000 T-states).
  - Feeds physical EAR signal directly to bit 6 of port `0xFE` for authentic ROM `LD-BYTES` (`0x0556`) execution.

### Milestone 6: Authentic Boot & Game Execution Verification
- Cold boot verification: Simulated power-on memory sizing loop counts down from `0xFFFF` to `0x4000`, initializes system variables (`P_RAMT = 0xFFFF`, `RAMTOP = 0xFF57`, `UDG = 0xFF58`), clears display, and renders `(C) 1982 Sinclair Research Ltd` (`screenshots/boot_test.bmp`).
- Interactive keyboard verification: Injected matrix keystrokes `PRINT 42` + `ENTER`; rendered `42` and `0 OK, 0:1` (`screenshots/basic_interactive.bmp`).
- Game compatibility verification:
  - `The Matrix (1982)`: Autostarts line 10, sets `BORDER 0`, renders green INK rain characters (`screenshots/matrix_game.bmp`).
  - `Lunar Lander (1982)`: Autostarts line 10, responds to real-time player inputs ('A' thruster, 'P' lateral steering), renders landing pad and lunar landscape (`screenshots/lunar_lander_game.bmp`).
- Headless execution verified at over **2,400 FPS** (>48× real-time speed).
- Completed 20 automated unit and integration tests passing with 0 failures and 0 warnings.
