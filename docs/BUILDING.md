# Building and Running the ZX Spectrum 48K Emulator

## 1. Prerequisites
- **Operating System:** Windows 10/11 x64.
- **SDK:** Microsoft .NET 8.0 SDK (version 8.0.100 or later).
- **Python (Optional):** Python 3.10+ (for ROM forensic and TAP asset generation scripts).
- **Firmware:** The authentic 16 KB Sinclair ZX Spectrum ROM (`roms/48.rom`).

---

## 2. Compilation

Clone the repository and build using the .NET CLI:

```powershell
# Restore dependencies and build entire solution in Debug mode
dotnet build

# Build in optimized Release mode
dotnet build -c Release
```

The solution comprises:
- `Spectrum.Core`: Core processor, ULA, memory bus, and physical tape emulation.
- `Spectrum.Diagnostics`: Z80 disassembler, instruction execution tracer, BMP framebuffer writer.
- `Spectrum.Emulator`: Windows Forms GUI runner and high-performance headless CLI.
- `Spectrum.Tests`: Complete xUnit test suite.

---

## 3. Running the Test Suite

Execute all 20 automated unit and integration tests:

```powershell
dotnet test
```

To run individual test fixtures:

```powershell
# Verify authentic power-on memory test and cold boot
dotnet test --filter FirmwareBootTests

# Verify interactive BASIC keystroke injection
dotnet test --filter BasicInteractiveTests

# Verify tape pulse synthesis and ROM LD-BYTES loading
dotnet test --filter TapeIntegrationTests

# Verify complete game autostart and rendering
dotnet test --filter GameCompatibilityTests
```

---

## 4. Running the Emulator

### Interactive GUI Mode (Windows 11)
Launch the emulator with the Sinclair ROM:

```powershell
dotnet run --project src/Spectrum.Emulator -c Release -- --rom roms/48.rom
```

Load and auto-insert a tape:

```powershell
dotnet run --project src/Spectrum.Emulator -c Release -- --rom roms/48.rom --tape games/matrix.tap
```

### High-Performance Headless / Batch Mode
Run headless execution for benchmarking, automated tests, or capturing screenshots:

```powershell
dotnet run --project src/Spectrum.Emulator -c Release -- --rom roms/48.rom --tape games/lander.tap --headless --frames 600 --screenshot screenshots/lander_run.bmp
```
Headless execution operates at **>2,000 FPS** (>40× real-time speed) on modern x64 hardware.
