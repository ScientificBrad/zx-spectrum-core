# REA 01: Sinclair ZX Spectrum 48K ROM Analysis

**Artifact:** `roms/48.rom`  
**Size:** 16,384 bytes (16 KB)  
**SHA-1:** `5ea7c2b824672e914525d1d5c419d71b84a426a2`  
**MD5:** `4c42a2f075212361c3117015b107ff68`  
**Authors:** Dr. Steve Vickers & Richard Altwasser (Nine Tiles / Sinclair Research, 1982)

---

## 1. ROM Memory Segmentation Map

| Address Range | Size | Functional Region |
|---|---|---|
| `0x0000–0x00BF` | 192 B | Restart (RST) vectors, interrupt entry points, NMI handler |
| `0x0095–0x0204` | 368 B | Sinclair BASIC token table (keywords) |
| `0x0205–0x028D` | 137 B | Keyboard decoding lookup table |
| `0x028E–0x03B4` | 295 B | Keyboard scanning and debouncing routines (`KEY-SCAN`) |
| `0x03B5–0x04C1` | 269 B | Beeper and sound generation (`BEEPER`) |
| `0x04C2–0x0555` | 148 B | Tape save routine (`SA-BYTES`) |
| `0x0556–0x0604` | 175 B | Tape load and verify routine (`LD-BYTES`) |
| `0x0605–0x0EAC` | 2,216 B | Screen drawing, scrolling, token expansion, `PRINT-A` channel output |
| `0x0EAC–0x11CA` | 799 B | Math subroutines (integer division, multiplication, float conversion) |
| `0x11CB–0x12A8` | 222 B | System initialization, RAM sizing, and cold boot |
| `0x12A9–0x1390` | 232 B | Main execution loop & BASIC command line interpreter |
| `0x1391–0x1531` | 417 B | Error message string table (A–Z, 0–9) |
| `0x1532–0x155A` | 41 B | Initial channel and stream information table, copyright string |
| `0x155B–0x1B27` | 1,485 B | BASIC syntax checker and statement execution dispatch |
| `0x1B28–0x247F` | 2,392 B | Expression evaluator and operator precedence parser |
| `0x2480–0x32C3` | 3,652 B | Mathematical functions (Trigonometric, Log, Exp, Sqr, Matrix) |
| `0x32C4–0x3CFF` | 2,620 B | Floating-point calculator interpreter (`FP-CALC`, RST 28h) |
| `0x3D00–0x3FFF` | 768 B | 8×8 Character Font Generator (96 printable characters) |

---

## 2. Restart (RST) Table

| Vector | Address | Handler | Primary Operation |
|---|---|---|---|
| `RST 00H` | `0x0000` | START | Power-on reset: `DI; XOR A; LD DE, 0xFFFF; JP 0x11CB` |
| `RST 08H` | `0x0008` | ERROR-1 | Error restart: points `X_PTR` to `(CH_ADD)`, jumps to `0x0053` |
| `RST 10H` | `0x0010` | PRINT-A | Prints character in register `A` to current stream (`0x15F2`) |
| `RST 18H` | `0x0018` | GET-CHAR | Fetches next character from `(CH_ADD)` into `A`, skips spaces |
| `RST 20H` | `0x0020` | NEXT-CHAR | Advances `CH_ADD` pointer by 1 and calls `GET-CHAR` |
| `RST 28H` | `0x0028` | FP-CALC | Enters floating-point calculator bytecode engine (`0x335B`) |
| `RST 30H` | `0x0030` | BC-SPACES | Allocates `BC` bytes of workspace memory at `WORKSP` |
| `RST 38H` | `0x0038` | INT | Frame interrupt handler: increments `FRAMES`, scans keyboard |
| `NMI` | `0x0066` | NMI | Non-maskable interrupt: vectors to external peripheral or returns |

---

## 3. BASIC Token Encoding

Sinclair BASIC stores tokens as single byte values from `0xA5` (165) to `0xFF` (255):
- `0xA5`: `RND`
- `0xA6`: `INKEY$`
- `0xA7`: `PI`
- `0xA8`: `FN`
- `0xA9`: `POINT`
- `0xAA`: `SCREEN$`
- `0xAB`: `ATTR`
- `0xAC`: `AT`
- `0xAD`: `TAB`
- `0xAE`: `VAL$`
- `0xAF`: `CODE`
- `0xB0`: `VAL`
- `0xB1`: `LEN`
- `0xB2`: `SIN`
- `0xC7`: `INK`
- `0xC8`: `PAPER`
- `0xC9`: `FLASH`
- `0xCA`: `BRIGHT`
- `0xDF`: `PRINT`
- `0xE6`: `NEW`
- `0xE7`: `BORDER`
- `0xEA`: `REM`
- `0xEB`: `FOR`
- `0xEC`: `GO TO`
- `0xED`: `GO SUB`
- `0xEE`: `INPUT`
- `0xEF`: `LOAD`
- `0xF0`: `LIST`
- `0xF1`: `LET`
- `0xF2`: `PAUSE`
- `0xF3`: `NEXT`
- `0xF4`: `POKE`
- `0xF6`: `RUN`
- `0xFF`: `COPY`

---

## 4. Cold Boot Initialisation Flow (`0x11CB`)

1. **`0x11CB` (START):**
   - Initialize stack pointer `SP` to point to temporary workspace.
   - Set interrupt mode 1 (`IM 1`).
   - Set I register to `0x3F` (high byte of interrupt vector table).
   - Turn border white / cyan / border colour initialization via port `0xFE`.
2. **`0x11DC` (RAM Sizing):**
   - Starts at `DE = 0xFFFF`.
   - Writes `0x02` to `(HL)`, reads back. If matches, RAM exists at this address.
   - On 48K machine, `0xFFFF` matches, setting RAMTOP = `0xFFFF`.
3. **`0x1219` (Sysvar Initialization):**
   - Sets stack pointer to RAMTOP (`0xFFFF`).
   - Clears sysvars `0x5C00..0x5CB6`.
   - Configures channels `K` (keyboard), `S` (screen), `P` (printer).
4. **`0x12A9` (Main Entry):**
   - Clears screen with `CALL 0x0DAF` (`CLS`).
   - Opens channel 'S' (upper screen).
   - Prints copyright notice `(C) 1982 Sinclair Research Ltd` at bottom of screen.
   - Enters keyboard polling loop awaiting user command.
