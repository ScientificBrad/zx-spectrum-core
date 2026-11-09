import hashlib
import os
import sys

ROM_PATH = "roms/48.rom"

def main():
    if not os.path.exists(ROM_PATH):
        print(f"Error: {ROM_PATH} not found.")
        return

    with open(ROM_PATH, "rb") as f:
        rom = f.read()

    size = len(rom)
    sha1 = hashlib.sha1(rom).hexdigest()
    md5 = hashlib.md5(rom).hexdigest()

    print(f"ROM: {ROM_PATH}")
    print(f"Size: {size} bytes ({size // 1024} KB)")
    print(f"SHA-1: {sha1}")
    print(f"MD5: {md5}")

    # Vectors
    vectors = [
        (0x0000, "START / RESET", 8),
        (0x0008, "RST 08H (ERROR-1)", 8),
        (0x0010, "RST 10H (PRINT-A)", 8),
        (0x0018, "RST 18H (GET-CHAR)", 8),
        (0x0020, "RST 20H (NEXT-CHAR)", 8),
        (0x0028, "RST 28H (FP-CALC)", 8),
        (0x0030, "RST 30H (BC-SPACES)", 8),
        (0x0038, "RST 38H (INT Handler - 50Hz ULA)", 8),
        (0x0066, "NMI (Non-Maskable Interrupt)", 8),
    ]

    print("\n--- SYSTEM VECTORS ---")
    for addr, desc, length in vectors:
        b = rom[addr:addr+length]
        hex_b = " ".join(f"{x:02X}" for x in b)
        print(f"0x{addr:04X}: {desc:<30} Bytes: {hex_b}")

    # Inspect Copyright message
    # In ZX Spectrum 48K ROM, copyright string is around 0x153B or after tokens:
    # Let's search for "1982" or "Sinclair"
    print("\n--- STRING SEARCH ---")
    pos = 0
    while True:
        pos = rom.find(b"Sinclair", pos)
        if pos == -1:
            break
        # Extract surrounding string
        start = max(0, pos - 10)
        end = min(len(rom), pos + 30)
        snippet = rom[start:end]
        print(f"Found 'Sinclair' at 0x{pos:04X}: {snippet}")
        pos += 1

    # Extract BASIC Tokens table (usually begins at 0x0095 or 0x00A0 up to 0x028D or around 0x1530)
    # On 48K Spectrum, the token table starts at 0x0095!
    print("\n--- TOKEN TABLE SCAN (at 0x0095) ---")
    idx = 0x0095
    tokens = []
    curr_tok = []
    # Token table ends when tokens end (usually 0x0095 to 0x028D approx)
    # In Sinclair ROM, characters have bit 7 = 0 for body, bit 7 = 1 for last char of word!
    while idx < 0x0230:
        b = rom[idx]
        ch = b & 0x7F
        if 32 <= ch <= 126:
            curr_tok.append(chr(ch))
        else:
            curr_tok.append(f"\\x{ch:02x}")
        if b & 0x80:
            tokens.append((idx - len(curr_tok) + 1, "".join(curr_tok)))
            curr_tok = []
        idx += 1

    print(f"Discovered {len(tokens)} BASIC tokens starting at 0x0095:")
    for addr, t in tokens[:20]:
        print(f"  0x{addr:04X} (Token {0xA5 + tokens.index((addr, t)) if 0xA5 + tokens.index((addr, t)) <= 255 else '?' }): {t}")
    print("  ...")
    for addr, t in tokens[-10:]:
        print(f"  0x{addr:04X}: {t}")

    # Character set bitmap: 0x3D00 to 0x3FFF
    print("\n--- FONT TABLE SCAN (0x3D00 - 0x3FFF) ---")
    font_bytes = rom[0x3D00:0x4000]
    print(f"Font size: {len(font_bytes)} bytes ({len(font_bytes)//8} glyphs)")
    # Character 'A' is at offset ('A' - 32) * 8 = (65 - 32) * 8 = 33 * 8 = 264 (0x108) from 0x3D00
    a_offset = 0x3D00 + (ord('A') - 32) * 8
    print(f"Glyph 'A' at 0x{a_offset:04X}:")
    for r in range(8):
        row = rom[a_offset + r]
        line = "".join("#" if (row & (1 << (7 - bit))) else "." for bit in range(8))
        print(f"  {line} (0x{row:02X})")

    # Error message table
    print("\n--- ERROR MESSAGES (from 0x1391) ---")
    idx = 0x1391
    err_msgs = []
    curr_err = []
    while idx < 0x1530:
        b = rom[idx]
        ch = b & 0x7F
        if 32 <= ch <= 126:
            curr_err.append(chr(ch))
        else:
            curr_err.append(f"\\x{ch:02x}")
        if b & 0x80:
            err_msgs.append((idx - len(curr_err) + 1, "".join(curr_err)))
            curr_err = []
            if len(err_msgs) >= 30:
                break
        idx += 1

    for i, (addr, em) in enumerate(err_msgs):
        print(f"  Code {i:02X} ('{chr(ord('A')+i-1) if i>0 else '0'}'): 0x{addr:04X} -> {em}")

    print("\nForensics scan complete.")

if __name__ == "__main__":
    main()
