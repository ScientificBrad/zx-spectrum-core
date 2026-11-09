# REA 04: ZX Spectrum 48K Firmware Execution & Cold Boot

**Target:** Firmware Startup Sequence (`48.rom`, `0x0000–0x1384`)  
**Methodology:** Cycle-accurate execution trace & symbol mapping  

---

## 1. Startup Flowchart

```
Power-On / Reset (0x0000)
       |
       v
  DI; XOR A
  LD DE, 0xFFFF
  JP 0x11CB (START)
       |
       v
  Initialize Stack Pointer (temp)
  Set Interrupt Mode 1 (IM 1)
  Set I Register = 0x3F
  Set Border Colour to White (Port 0xFE = 7)
       |
       v
  RAM Sizing Loop (0x11DC)
  Count down from 0xFFFF writing 0x02 to (HL)
  and reading back until valid RAM byte found.
  Stores result in P_RAMT (0x5C38) = 0xFFFF
       |
       v
  Clear System Variables (0x5C00..0x5CB6)
  Set SP = 0xFFFF (RAMTOP)
  Initialize Streams & Channels (K, S, P)
  Initialize BORDER, ATTR_P, CHARS (0x3C00)
       |
       v
  Clear Screen (CLS, 0x0DAF / 0x0D6B)
  Fill VRAM 0x4000..0x57FF with 0x00
  Fill Attributes 0x5800..0x5AFF with 0x38 (White Paper, Black Ink)
       |
       v
  Display Banner:
  "(C) 1982 Sinclair Research Ltd" at bottom of screen
       |
       v
  Enable Interrupts (EI)
  Enter Main Keyboard & BASIC Command Loop (0x12A9)
```

---

## 2. Key System Variables (`0x5C00–0x5CB6`)

| Address | Name | Length | Purpose |
|---|---|---|---|
| `0x5C00` | `KSTATE` | 8 bytes | Keyboard debounce and key scan states |
| `0x5C38` | `P_RAMT` | 2 bytes | Highest RAM address found during startup (`0xFFFF` for 48K) |
| `0x5C3A` | `CHARS` | 2 bytes | Pointer to character font set minus 256 (`0x3C00`) |
| `0x5C3C` | `RASP` | 1 byte | Beeper length |
| `0x5C3D` | `PIP` | 1 byte | Beeper pitch |
| `0x5C3E` | `ERR_NR` | 1 byte | Error number (0xFF = OK) |
| `0x5C3F` | `FLAGS` | 1 byte | Mode flags (bit 5: screen clear required, bit 3: L mode) |
| `0x5C48` | `BORDCR` | 1 byte | Border colour * 8 |
| `0x5C49` | `ATTR_V` | 1 byte | Attribute under cursor |
| `0x5C4D` | `ATTR_P` | 1 byte | Permanent attribute colours (`0x38` = White paper, Black ink) |
| `0x5C51` | `VARS` | 2 bytes | Pointer to start of BASIC variables area |
| `0x5C53` | `DEST` | 2 bytes | Address of variable being assigned |
| `0x5C57` | `CHANS` | 2 bytes | Address of channel data |
| `0x5C59` | `CURCHL` | 2 bytes | Address of current channel |
| `0x5C5D` | `CH_ADD` | 2 bytes | Address of next character to be interpreted |
| `0x5C5F` | `X_PTR` | 2 bytes | Address of character preceding syntax error marker |
| `0x5C78` | `FRAMES` | 3 bytes | Frame counter incremented every 20ms by RST 38H interrupt |
| `0x5C80` | `UDG` | 2 bytes | Pointer to User Defined Graphics (`0xFF58`) |
