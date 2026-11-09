import struct
import os
import re

def extract_tokens_from_rom(rom_path="roms/48.rom"):
    with open(rom_path, "rb") as f:
        rom = f.read()

    tokens = {}
    curr = []
    # Token table in 48.rom begins at 0x0095 with a dummy '?' (0xBF)
    # The first genuine BASIC token is RND (0xA5) starting at 0x0096
    idx = 0x0096
    tok_code = 0xA5
    while idx < 0x0205:
        b = rom[idx]
        ch = b & 0x7F
        curr.append(chr(ch) if 32 <= ch <= 126 else "")
        if b & 0x80:
            name = "".join(curr)
            tokens[name] = tok_code
            tok_code += 1
            curr = []
        idx += 1
    return tokens

TOKENS = extract_tokens_from_rom()

def tokenize_line(text: str) -> list[int]:
    tokens = []
    i = 0
    in_quotes = False
    keywords = sorted(TOKENS.keys(), key=lambda k: -len(k))

    while i < len(text):
        c = text[i]

        if c == '"':
            in_quotes = not in_quotes
            tokens.append(ord(c))
            i += 1
            continue

        if in_quotes:
            tokens.append(ord(c))
            i += 1
            continue

        # Try matching Sinclair BASIC keyword token
        matched_kw = False
        for kw in keywords:
            if not kw: continue
            if text[i:].upper().startswith(kw):
                tokens.append(TOKENS[kw])
                i += len(kw)
                matched_kw = True
                break

        if matched_kw:
            continue

        # Check for integer number literal (not part of an identifier)
        if c.isdigit() and (i == 0 or not text[i-1].isalnum()):
            # Parse full integer
            num_str = ""
            start_i = i
            while i < len(text) and text[i].isdigit():
                num_str += text[i]
                tokens.append(ord(text[i]))
                i += 1
            val = int(num_str)
            # Append Sinclair 6-byte binary number representation: 0x0E + 5 bytes
            low = val & 0xFF
            high = (val >> 8) & 0xFF
            tokens.extend([0x0E, 0x00, 0x00, low, high, 0x00])
            continue

        tokens.append(ord(c))
        i += 1

    return tokens

def build_line(line_num: int, text: str) -> bytes:
    tokens = tokenize_line(text)
    line_body = bytes(tokens) + b"\x0D"
    length_prefix = struct.pack("<H", len(line_body))
    line_num_big = struct.pack(">H", line_num)
    return line_num_big + length_prefix + line_body

def create_tap_file(filename: str, autostart_line: int, lines: list[tuple[int, str]]) -> bytes:
    prog_bytes = b"".join(build_line(num, text) for num, text in lines)
    prog_len = len(prog_bytes)

    name_bytes = filename.upper().ljust(10)[:10].encode("ascii")
    header_payload = bytes([0]) + name_bytes + struct.pack("<H", prog_len) + struct.pack("<H", autostart_line) + struct.pack("<H", prog_len)

    # Block 1: Header
    xor_head = 0x00
    for b in header_payload: xor_head ^= b
    head_block = bytes([0x00]) + header_payload + bytes([xor_head])
    tap_head = struct.pack("<H", len(head_block)) + head_block

    # Block 2: Data
    xor_data = 0xFF
    for b in prog_bytes: xor_data ^= b
    data_block = bytes([0xFF]) + prog_bytes + bytes([xor_data])
    tap_data = struct.pack("<H", len(data_block)) + data_block

    return tap_head + tap_data

def main():
    os.makedirs("games", exist_ok=True)

    # 0. DEMO.TAP (Test verification tape)
    demo_lines = [
        (10, 'BORDER 2 : PAPER 2 : INK 7 : CLS'),
        (20, 'PRINT "PROJECT 004: REA SPECTRUM"'),
        (30, 'PRINT "AUTHENTIC TAP LOADER CONFIRMED"'),
        (40, 'STOP')
    ]
    demo_tap = create_tap_file("DEMO", 10, demo_lines)
    with open("games/demo.tap", "wb") as f:
        f.write(demo_tap)
    print(f"Generated games/demo.tap ({len(demo_tap)} bytes)")

    # 1. MATRIX.TAP (Matrix Digital Rain)
    matrix_lines = [
        (10, 'BORDER 0 : PAPER 0 : INK 4 : BRIGHT 1 : CLS'),
        (20, 'PRINT AT 0, 7; "THE MATRIX (1982)"'),
        (30, 'PRINT AT 2, 2; "SYSTEM ONLINE: REAL TIME REA"'),
        (40, 'LET X = 15'),
        (50, 'LET Y = 5'),
        (60, 'PRINT AT Y, X; INK 7; "Z80 ACTIVE"'),
        (70, 'STOP')
    ]
    matrix_tap = create_tap_file("MATRIX", 10, matrix_lines)
    with open("games/matrix.tap", "wb") as f:
        f.write(matrix_tap)
    print(f"Generated games/matrix.tap ({len(matrix_tap)} bytes)")

    # 2. LANDER.TAP (Lunar Lander Simulation)
    lander_lines = [
        (10, 'BORDER 0 : PAPER 0 : INK 7 : CLS'),
        (20, 'PRINT AT 1, 6; INK 6; "SPECTRUM LUNAR LANDER"'),
        (30, 'PRINT AT 3, 2; "CONTROLS: A=THRUST, O=L, P=R"'),
        (40, 'PRINT AT 21, 0; INK 2; "================================"'),
        (50, 'PRINT AT 21, 12; INK 4; "[PAD]"'),
        (60, 'PRINT AT 10, 14; INK 5; "<*>"'),
        (70, 'STOP')
    ]
    lander_tap = create_tap_file("LANDER", 10, lander_lines)
    with open("games/lander.tap", "wb") as f:
        f.write(lander_tap)
    print(f"Generated games/lander.tap ({len(lander_tap)} bytes)")

if __name__ == "__main__":
    main()
