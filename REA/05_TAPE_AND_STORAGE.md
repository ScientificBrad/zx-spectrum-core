# REA 05: Cassette Tape Subsystem & Format Specification

**Target:** Sinclair ZX Spectrum 48K Cassette Interface & TAP Format  
**Hardware Interface:** Ferranti ULA Port `0xFE` (EAR bit 6 input, MIC bit 3 / Beeper bit 4 output)  

---

## 1. Physical Cassette Signal Parameters

The Spectrum uses frequency shift keying (FSK) generated through cycle-timed CPU loops:

| Signal Phase | Pulse Duration (T-states) | Frequency | Pulse Count | Duration |
|---|---|---|---|---|
| **Header Pilot Tone** | 2,168 T-states | ~807 Hz | 8,063 pulses | ~5.0 seconds |
| **Data Pilot Tone** | 2,168 T-states | ~807 Hz | 3,223 pulses | ~2.0 seconds |
| **Sync Pulse 1** | 667 T-states | ~2624 Hz | 1 pulse | 190.5 µs |
| **Sync Pulse 2** | 735 T-states | ~2381 Hz | 1 pulse | 210.0 µs |
| **Data Bit '0'** | 855 T-states (2 pulses) | ~2047 Hz | 2 pulses (1710 T) | 488.6 µs |
| **Data Bit '1'** | 1,710 T-states (2 pulses) | ~1023 Hz | 2 pulses (3420 T) | 977.1 µs |

---

## 2. ROM Loader Routine (`LD-BYTES`, Address `0x0556`)

The ROM loader operates by counting the elapsed Z80 T-states between signal edges on bit 6 of port `0xFE`:
1. **Edge Detection:** Waits for bit 6 of port `0xFE` to change state.
2. **Leader Detection:** Measures pulse width; verifies that ~8063 or ~3223 pulses of ~2168 T-states are received.
3. **Sync Discrimination:** Recognizes Sync 1 (667 T) followed by Sync 2 (735 T).
4. **Data Bit Decoding:** A threshold of approximately 1,200 T-states discriminates between a '0' bit (855 T) and a '1' bit (1,710 T).
5. **Checksum Verification:** Every block terminates with an 8-bit XOR parity byte covering the flag byte and all payload bytes. If the final XOR sum is zero, the load succeeds (Carry flag = 1); otherwise `Tape loading error` is thrown.

---

## 3. TAP File Format Structure

A `.tap` file consists of concatenated blocks:
```
+--------------------+--------------------+-------------------------------+--------------------+
| Block Length (2 B) | Flag Byte (1 B)    | Payload Data (N - 2 bytes)    | Checksum Byte (1 B)|
| Little-endian      | 0x00=Head, 0xFF=Dat| Bytes to load into RAM        | XOR parity sum     |
+--------------------+--------------------+-------------------------------+--------------------+
```

### Standard Header Block (17 payload bytes):
- Byte 0: Type (`0` = Program, `1` = Number Array, `2` = Character Array, `3` = Code / Bytes)
- Bytes 1–10: Filename (10 ASCII characters, padded with spaces)
- Bytes 11–12: Data length
- Bytes 13–14: Parameter 1 (Auto-start line for Program, or start address for Code)
- Bytes 15–16: Parameter 2 (Program variable area length)

---

## 4. Expansion Bus Peripheral & RAM Disk Subsystem (`SpectrumRamDisk.cs`)

Certain ZX Spectrum software titles (such as `SAMANTHA.TAP`) were authored to utilise RAM disk expansion hardware (Interface 1 / 128K Silicon Disc) using `SAVE !` and `LOAD !` syntax.

When running on standard 48K Sinclair ROM (`48.rom`), encountering `!` triggers syntax error `C Nonsense in BASIC` (`REPORT-C` at address `0x1C8A` via `RST 08`).
The emulator implements an authentic expansion bus peripheral trap:
1. **Hook:** Traps `RST 08` (`0x0008`) when the return address on the stack is `0x1C8B` and error code is `0x0B` (11).
2. **Variable Table Parsing (`VARS` at `0x5C4B`):** Safely parses Sinclair BASIC variable records bounded by `E_LINE` (`0x5C59`):
   - Simple string variables: identified by bits 5–7 = `010` (`0x40`).
   - Simple numeric variables: bits 5–7 = `011` (`0x60`, 6 bytes).
   - `FOR` loop control variables: bits 5–7 = `111` (`0xE0`, 19 bytes: 1 byte identifier + 18 payload bytes).
   - Array variables: bits 5–7 = `100` (`0x80`) or `110` (`0xC0`).
3. **Execution:** Performs named in-memory transfer for `CODE` and `SCREEN$` blocks, updates `CH_ADD` past the statement, and returns execution cleanly to `0x1BF4` (`STMT-RET`).

---

## 5. Authentic Audio Subsystem (`Beeper.cs` & `WindowsWaveOut.cs`)

The audio subsystem generates genuine 48 kHz 16-bit PCM mono audio:
- **Cassette Audio Monitoring:** Generates real-time audio waveforms directly from `TapePlayer.EarSignal`, producing authentic 807 Hz pilot tones, sync chirps, and data sound waves synchronised in T-state lockstep with CPU execution.
- **1-Bit Beeper:** Port `0xFE` bit 4 speaker states are sampled at 48 kHz (72.9167 T-states per sample) and mixed with tape audio.
- **Lock-Free Ring Buffer:** 16,384-sample power-of-two circular buffer completely eliminates lock contention between the emulation core and the audio output device.
- **Independent Controls:** User-adjustable `BeeperVolume` and `TapeVolume` sliders, master mute (`Ctrl+M`), and turbo loading frame scaling (1×, 4×, 10×, Max).
