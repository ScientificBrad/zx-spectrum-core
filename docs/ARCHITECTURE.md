# ZX Spectrum 48K Emulator Architecture

## 1. System Overview

This project is a clean-room, cycle-accurate reconstruction of the **1982 Sinclair ZX Spectrum 48K**, implemented in modern C# / .NET 8 on Windows 11 x64.

The architecture strictly segregates the hardware host platform, generic microarchitectural processor cores, and machine-specific system buses.

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

---

## 2. Component Design

### 2.1 Z80Cpu (`Spectrum.Core/Z80Cpu.cs`)
- Fully reusable Zilog Z80A microprocessor implementation.
- Emulates:
  - Base instruction set (8-bit ALU, 16-bit register pairs, load/store, conditional branching).
  - Prefix dispatch: `CB` (bit operations), `ED` (extended instructions), `DD`/`FD` (IX and IY indexing), and combined `DDCB`/`FDCB` indexed bit manipulation.
  - Cycle-exact flag calculations via precomputed lookup tables (Sign, Zero, YF, Half-Carry, XF, Parity/Overflow, Add/Subtract, Carry).
  - Interrupt modes: IM 0, IM 1 (vector to `0x0038`), IM 2.
  - Non-maskable interrupt (NMI vector to `0x0066`).
  - Memory access callbacks for contended bus arbitration.

### 2.2 Ferranti ULA (`Spectrum.Core/FerrantiUla.cs`)
- Reconstructs the 6C001 / 7C001 Ferranti Custom Uncommitted Logic Array:
  - Video resolution: 256×192 active graphic display with full 320×240 standard border framing.
  - Sinclair non-linear bitmap address translation (`0x4000–0x57FF`) and 32×24 colour attribute cell decoding (`0x5800–0x5AFF`).
  - Frame timing: PAL standard (224 T-states per scanline, 312 scanlines per frame, exactly 69,888 T-states per frame at 50.088 Hz).
  - Memory Contention: Exact 8-state contention delay pattern applied whenever the CPU accesses contended memory (`0x4000–0x7FFF`) during active display fetch cycles.
  - Sound: 1-bit speaker output toggling on port `0xFE`.

### 2.3 SpectrumBus (`Spectrum.Core/SpectrumBus.cs`)
- Manages the 64 KB address space:
  - `0x0000–0x3FFF`: 16 KB Sinclair BASIC ROM (read-only; writes are discarded).
  - `0x4000–0x7FFF`: 16 KB contended lower RAM (includes screen bitmap and attributes).
  - `0x8000–0xFFFF`: 32 KB uncontended upper RAM.
- Port decoding: Partial I/O decoding for port `0xFE` (any even I/O address).
- In-flight contention arbitration: Calls `Ula.ApplyContention(address, tstates)` prior to all memory read/write and I/O cycles.

### 2.4 Keyboard Matrix (`Spectrum.Core/SpectrumKeyboard.cs`)
- Implements the authentic 8-row × 5-column active-low keyboard matrix.
- When an `IN A, (0xFE)` instruction is executed, the upper 8 bits of the I/O address (`A8–A15`) select the matrix half-rows.
- Active keys pull the corresponding data bus bits (`D0–D4`) low.

### 2.5 Tape Subsystem (`Spectrum.Core/TapePlayer.cs` & `TapFile.cs`)
- Physical pulse generator operating at cycle accuracy (3.5 MHz resolution):
  - Pilot tone: 2,168 T-states per half-cycle (8,063 pulses for header, 3,223 pulses for data).
  - Sync pulses: First half-wave 667 T-states, second half-wave 735 T-states.
  - Data bits: Bit 0 = two 855 T-state pulses; Bit 1 = two 1,710 T-state pulses.
  - Tail pulse: 1,000 T-state closing edge to allow ROM `LD-EDGE-2` routine to terminate without timeout.
  - Pause: 1,000 ms silence between blocks.
- Supplies physical signal levels directly to the EAR bit (bit 6) of port `0xFE`.
