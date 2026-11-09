import struct
import sys

def create_tap_block(flag: int, payload: bytes) -> bytes:
    # Compute XOR checksum
    xor = flag
    for b in payload:
        xor ^= b
    block_data = bytes([flag]) + payload + bytes([xor])
    length_prefix = struct.pack("<H", len(block_data))
    return length_prefix + block_data

def build_basic_line(line_num: int, tokens: list[int]) -> bytes:
    line_bytes = bytes(tokens) + b"\x0D"
    length_prefix = struct.pack("<H", len(line_bytes))
    line_num_big = struct.pack(">H", line_num)
    return line_num_big + length_prefix + line_bytes

def main():
    # Tokens:
    # 0xE7 = BORDER
    # 0xC8 = PAPER
    # 0xC7 = INK
    # 0xEF = LOAD
    # 0xFB = CLS
    # 0xDF = PRINT
    # 0xEB = FOR
    # 0xCC = TO
    # 0xF3 = NEXT
    # 0xD8 = BEEP
    
    # Line 10: BORDER 2 : PAPER 2 : INK 7 : CLS
    # BORDER (0xE7), ' ', '2', ' ', ':', ' ', PAPER (0xC8), ' ', '2', ' ', ':', ' ', INK (0xC7), ' ', '7', ' ', ':', ' ', CLS (0xFB)
    line10_tokens = [0xE7, ord(' '), ord('2'), ord(' '), ord(':'), ord(' '),
                     0xC8, ord(' '), ord('2'), ord(' '), ord(':'), ord(' '),
                     0xC7, ord(' '), ord('7'), ord(' '), ord(':'), ord(' '),
                     0xFB]
    line10 = build_basic_line(10, line10_tokens)

    # Line 20: PRINT "PROJECT 004: REA SPECTRUM"
    line20_tokens = [0xDF, ord(' '), ord('"')] + [ord(c) for c in "PROJECT 004: REA SPECTRUM"] + [ord('"')]
    line20 = build_basic_line(20, line20_tokens)

    # Line 30: PRINT "AUTHENTIC TAP LOADER CONFIRMED"
    line30_tokens = [0xDF, ord(' '), ord('"')] + [ord(c) for c in "AUTHENTIC TAP LOADER CONFIRMED"] + [ord('"')]
    line30 = build_basic_line(30, line30_tokens)

    program_data = line10 + line20 + line30
    prog_len = len(program_data)

    # Construct Header (17 bytes payload)
    # Byte 0: Type (0 = Program)
    # Bytes 1-10: Filename (10 chars space padded)
    # Bytes 11-12: Data length (little endian)
    # Bytes 13-14: Autostart line (10 = 0x000A)
    # Bytes 15-16: Variable offset (prog_len)
    name_bytes = b"DEMO      "
    header_payload = bytes([0]) + name_bytes + struct.pack("<H", prog_len) + struct.pack("<H", 10) + struct.pack("<H", prog_len)

    header_block = create_tap_block(0x00, header_payload)
    data_block = create_tap_block(0xFF, program_data)

    tap_bytes = header_block + data_block
    out_path = "games/demo.tap"
    with open(out_path, "wb") as f:
        f.write(tap_bytes)

    print(f"Generated valid TAP file at '{out_path}' ({len(tap_bytes)} bytes).")

if __name__ == "__main__":
    main()
