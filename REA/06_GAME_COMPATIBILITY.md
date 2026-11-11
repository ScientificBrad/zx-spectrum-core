# REA 06: Commercial Software & Game Compatibility Matrix

**Target:** ZX Spectrum 48K Software Compatibility  
**Acceptance Progression:**  
`ROM BOOTS` -> `BASIC INTERACTIVE` -> `TAPE LOADS` -> `TITLE DISPLAYED` -> `INPUT WORKS` -> `GAME PLAYABLE`

---

## Compatibility Ledger

| Program / Title | Year | Publisher | Format | Load Method | Status | Notes / Observed Behavior |
|---|---|---|---|---|---|---|
| **Sinclair BASIC 48K** | 1982 | Sinclair Research | ROM | Power-On Reset | `CONFIRMED` | Genuine ROM boots, clears screen, displays copyright, interactive prompt. |
| **BEEPER Diagnostics** | 1982 | Sinclair Research | BASIC | Direct Input | `CONFIRMED` | Verifies 1-bit sound generation through port 0xFE audio buffer. |
| **Project 004 Demo** | 2026 | ScientificBrad | TAP | ROM LD-BYTES | `CONFIRMED` | Authentic TAP header/data loaded via LD-BYTES; executes line 10, sets red border (`BORDER 2`). |
| **The Matrix** | 1982 | ScientificBrad | TAP | ROM LD-BYTES | `CONFIRMED` | Authentic TAP program; executes `BORDER 0`, renders green INK rain characters to ULA framebuffer. |
| **Lunar Lander** | 1982 | ScientificBrad | TAP | ROM LD-BYTES | `CONFIRMED` | Authentic TAP game; interactive inputs ('A', 'P') steer lander, renders multi-colour landing pad. |
| **Samantha Fox Strip Poker** | 1986 | Martech | TAP | ROM + RAM Disk | `CONFIRMED` | 24-block animated title using expansion bus RAM disk peripheral; loads, stores all 10 code frames, animates screen. |
| **Candy Girl** | 1984 | Sgt. Pepper | TAP | ROM LD-BYTES | `CONFIRMED` | Commercial machine code title; loads 37 KB binary via `LD-BYTES` and executes via `RANDOMIZE USR 25000`. |
| **Memory Contention Test** | 2026 | ScientificBrad | Code | Machine Code | `CONFIRMED` | Validates 6-cycle contention delays in 0x4000–0x7FFF. |
| **Manic Miner** | 1983 | Bug-Byte / Matthew Smith | TAP | ROM LD-BYTES | `TARGET` | Tests non-linear VRAM animation, keyboard matrix, border timing. |
| **Jet Set Willy** | 1984 | Software Projects | TAP | ROM LD-BYTES | `TARGET` | Tests attribute colour clash, continuous sound, sprite movement. |
| **Atic Atac** | 1983 | Ultimate Play the Game | TAP | ROM LD-BYTES | `TARGET` | Tests fast keyboard polling, full screen room drawing. |
| **Horace Goes Skiing** | 1982 | Sinclair / Beam Software | TAP | ROM LD-BYTES | `TARGET` | Classic early Spectrum title using ROM sound and ULA graphics. |
