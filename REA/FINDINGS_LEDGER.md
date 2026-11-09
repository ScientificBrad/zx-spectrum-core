# REA Findings Ledger: ZX Spectrum 48K Firmware & Architecture

**Project:** 004  
**Subject:** Sinclair ZX Spectrum 48K ROM (`48.rom`, 16,384 bytes)  
**Status Taxonomy:** `CONFIRMED` | `REPRODUCED` | `HYPOTHESIS` | `UNVERIFIED` | `DISPROVED`

---

## Findings Index

| ID | Address / Area | Category | Status | Summary |
|---|---|---|---|---|
| FND-001 | `0x0000–0x0007` | Cold Boot Vector | `CONFIRMED` | Disables interrupts (`DI`), zeroes A (`XOR A`), sets DE=0xFFFF, jumps to `0x11CB` (START). |
| FND-002 | `0x0008–0x000F` | RST 08H | `CONFIRMED` | Error restart handler. Loads syntax pointer `X_PTR` from `CH_ADD` and jumps to `0x0053`. |
| FND-003 | `0x0010–0x0017` | RST 10H | `CONFIRMED` | `PRINT-A`: Core character output dispatch jump to `0x15F2`. Writes to channel/stream. |
| FND-004 | `0x0018–0x001F` | RST 18H | `CONFIRMED` | `GET-CHAR`: Fetches next character from BASIC program at `(CH_ADD)`. |
| FND-005 | `0x0020–0x0027` | RST 20H | `CONFIRMED` | `NEXT-CHAR`: Increments `CH_ADD` pointer and fetches next non-whitespace character. |
| FND-006 | `0x0028–0x002F` | RST 28H | `CONFIRMED` | `FP-CALC`: Bytecode-driven floating-point calculator interpreter jump to `0x335B`. |
| FND-007 | `0x0030–0x0037` | RST 30H | `CONFIRMED` | `BC-SPACES`: Reserves BC bytes of workspace memory at `WORKSP` (jumps to `0x169E`). |
| FND-008 | `0x0038–0x0040` | RST 38H / INT | `CONFIRMED` | Maskable interrupt service routine executed 50 times/sec. Increments 3-byte `FRAMES` at `0x5C78`. Scans keyboard. |
| FND-009 | `0x0066–0x006D` | NMI Vector | `CONFIRMED` | Non-maskable interrupt vector. Checks for peripheral expansion / interface ROM return. |
| FND-010 | `0x0095–0x0204` | Token Table | `CONFIRMED` | Contains 96 Sinclair BASIC token strings with bit 7 set on terminal character of each keyword. |
| FND-011 | `0x028E–0x031B` | Keyboard Scanning | `CONFIRMED` | `KEY-SCAN`: Probes 8 half-rows via port `0xFE`, returns row/column keycode or debounced key state. |
| FND-012 | `0x04C2–0x053E` | Cassette Save | `CONFIRMED` | `SA-BYTES`: Generates pilot tone (8063 or 3223 pulses) and data pulses via MIC/Beeper on port `0xFE`. |
| FND-013 | `0x0556–0x0604` | Cassette Load | `CONFIRMED` | `LD-BYTES`: Measures edge timings on EAR bit 6 of port `0xFE`. Validates block type and XOR checksum. |
| FND-014 | `0x11CB–0x1264` | Startup / RAM Probe | `CONFIRMED` | Memory check loops downward from `0xFFFF` writing `0x02` and checking readback to find `P_RAMT` (RAMTOP). |
| FND-015 | `0x1265–0x12A8` | Sysvar Initialisation | `CONFIRMED` | Copies default system variables, initializes stack at `P_RAMT`, and opens stream channels. |
| FND-016 | `0x12A9–0x1384` | Main Execution Loop | `CONFIRMED` | Clears screen, displays copyright message `(C) 1982 Sinclair Research Ltd`, enters BASIC line editor. |
| FND-017 | `0x1391–0x1531` | Error Message Table | `CONFIRMED` | Contains 32 error message strings (`0 OK` through `9 FN without DEF`). Bit 7 set on last char. |
| FND-018 | `0x1540–0x155A` | Copyright String | `CONFIRMED` | String: `\x7f 1982 Sinclair Research Ltd` with terminal byte `0xE4` (`d` with bit 7 set). |
| FND-019 | `0x3D00–0x3FFF` | Character Font Set | `CONFIRMED` | 768 bytes comprising 96 8×8 matrix character glyphs (ASCII 32 ' ' through 127 '©'). |
| FND-020 | `0x5C00–0x5CB6` | System Variables | `CONFIRMED` | 182-byte system variable area placed in lower contended RAM right above attribute memory (`0x5B00`). |
| FND-021 | `0x0095–0x0096` | Dummy Token Prefix | `CONFIRMED` | Byte 0x0095 holds `0xBF` (`?`), an internal dummy entry. Routine `0x0C41` increments A, skipping this character, so token `0xA5` maps to `RND` at `0x0096`. |
| FND-022 | `0x05CD–0x05E3` | Cassette Tail Pulse | `CONFIRMED` | ROM `LD-BYTES` routine requires a trailing closing edge pulse (~1000 T-states) following the 19th byte (XOR checksum); without it, `LD-EDGE-2` counter in register B times out with `Carry=False`. |
| FND-023 | `0x198B–0x1B56` | Numeric Encoding | `CONFIRMED` | In tokenized Sinclair BASIC, numeric literals must be followed by `0x0E` and a 5-byte binary representation; plain ASCII causes the expression evaluator to halt with error `C Nonsense in BASIC`. |
| FND-024 | `0x5CB2–0x5CB4` | RAMTOP Sysvars | `CONFIRMED` | Firmware cold boot sets physical top of RAM `P_RAMT` at `0x5CB4` to `0xFFFF`. `RAMTOP` at `0x5CB2` is set to `0xFF57` (protecting 168 bytes for UDGs at `0xFF58`). |
| FND-025 | `0x1A62 / 0x2294` | BORDER Command Dispatch | `CONFIRMED` | Token `0xE7` (`BORDER`) dispatches through command table class 6 to `0x2294`, which calls `0x1E94` to parse integer argument, validates `< 8`, and executes `OUT (0xFE), A`. |

---

## Detailed Anomaly & Behaviour Observations

### Observation O-01: RAM Sizing Algorithm (`0x11DC–0x11E5`)
The firmware probe starts at `0xFFFF` with `LD H, D` and `LD L, E` (where DE = `0xFFFF`), writes `0x02` into `(HL)`, and tests if the value reads back as `0x02`.
- On a 48K Spectrum, this test succeeds immediately at `0xFFFF`, setting `P_RAMT = 0xFFFF`.
- On a 16K Spectrum, writes above `0x7FFF` fail (floating bus / open pullups), so the loop counts down until `0x7FFF`, finding 16 KB RAM.

### Observation O-02: Floating Bus Behavior on Port Read
When reading an unmapped I/O port or port `0xFF`, the CPU data bus floats. Due to the Ferranti ULA sharing the data bus to fetch display bitmap and attribute bytes, reading `0xFF` during active video generation returns the video or attribute byte currently on the ULA bus.

### Observation O-03: Cassette Loader Edge Timeout
In `LD-BYTES` (`0x0556`), bit reading counts down `B` from `0xFF` during edge detection in `LD-EDGE-2` (`0x05E3`). If tape signal transitions stop immediately upon transmission of the checksum bit, `B` wraps from `0x00` down to `0xFF` and triggers the carry-clearing error exit at `0x053F`. Providing a simulated post-checksum tail transition pulse guarantees a deterministic `Carry=True` completion.

### Observation O-04: Sinclair BASIC Command Table Indexing
Command tokens start at `0xCE` (`STEP` in token table, but class table offsets start at `0x1A48`). Command execution at `0x1B44` performs `SUB 0xCE`, then uses the result to look up the offset in `0x1A48`. BORDER (`0xE7 - 0xCE = 0x19`) correctly resolves to offset `0x94`, pointing to entry `0x1AF5` (`class 0x06, handler 0x2294`). Token `0xE8` (`CONTINUE`) resolves to `0x1E5F`.
