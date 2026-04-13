#!/usr/bin/env python3
"""Generate MindMapApp icon assets (PNG + ICO) using only Python stdlib."""
import struct, zlib, math, os

# ── Palette (matches app colours) ─────────────────────────────
BG     = (15,  23,  42)   # #0F172A
BG2    = (30,  41,  59)   # #1E293B
CENTER = (99, 102, 241)   # #6366F1
GLOW   = (129, 140, 248)  # lighter indigo
LINE   = (148, 163, 184)  # #94A3B8
NODES  = [
    ( 16, 185, 129),  # #10B981
    (245, 158,  11),  # #F59E0B
    (239,  68,  68),  # #EF4444
    (139,  92, 246),  # #8B5CF6
    (  6, 182, 212),  # #06B6D4
]

# ── Drawing primitives ─────────────────────────────────────────
def clamp(v): return max(0, min(255, int(round(v))))

def blend(base, col, a):
    return (clamp(base[0] + (col[0] - base[0]) * a),
            clamp(base[1] + (col[1] - base[1]) * a),
            clamp(base[2] + (col[2] - base[2]) * a))

def draw_circle(pixels, W, H, cx, cy, r, color, alpha=1.0):
    y0, y1 = max(0, int(cy - r - 1)), min(H, int(cy + r + 2))
    for y in range(y0, y1):
        dy = y + 0.5 - cy
        hw = math.sqrt(max(0.0, (r + 0.5)**2 - dy * dy))
        x0, x1 = max(0, int(cx - hw)), min(W, int(cx + hw) + 1)
        for x in range(x0, x1):
            d = math.sqrt((x + 0.5 - cx)**2 + dy * dy)
            a = max(0.0, min(1.0, r + 0.5 - d)) * alpha
            if a > 0.004:
                idx = y * W + x
                pixels[idx] = blend(pixels[idx], color, a)

def draw_line(pixels, W, H, x0, y0, x1, y1, color, thick, alpha=1.0):
    dx, dy = x1 - x0, y1 - y0
    length = math.sqrt(dx * dx + dy * dy)
    if length < 0.5: return
    steps = max(2, int(length * 2))
    r = thick / 2.0
    for i in range(steps + 1):
        t = i / steps
        draw_circle(pixels, W, H, x0 + dx * t, y0 + dy * t, r, color, alpha)

def draw_rounded_rect(pixels, W, H, x, y, w, h, r, color, alpha=1.0):
    for py in range(max(0, int(y)), min(H, int(y + h) + 1)):
        for px in range(max(0, int(x)), min(W, int(x + w) + 1)):
            qx = max(x + r, min(x + w - r, px + 0.5)) - (px + 0.5)
            qy = max(y + r, min(y + h - r, py + 0.5)) - (py + 0.5)
            d  = math.sqrt(qx * qx + qy * qy)
            a  = max(0.0, min(1.0, r - d + 0.5)) * alpha
            if a > 0.004:
                idx = py * W + px
                pixels[idx] = blend(pixels[idx], color, a)

# ── Icon layout (designed on 256×256 grid) ────────────────────
CX, CY = 128, 128
CR = 46          # central node radius
CHILDREN = [     # (x, y, radius) in 256 space
    (200,  75, 28),
    (212, 148, 26),
    (192, 215, 24),
    ( 58,  82, 24),
    ( 52, 192, 22),
]

def make_icon(SIZE):
    pixels = [BG] * (SIZE * SIZE)
    s = SIZE / 256.0

    # Rounded background card
    pad, rr = 10 * s, 38 * s
    draw_rounded_rect(pixels, SIZE, SIZE, pad, pad,
                      SIZE - 2*pad, SIZE - 2*pad, rr, BG2)

    # Connection lines (drawn behind nodes)
    line_w = max(0.6, 2.4 * s)
    for nx, ny, nr in CHILDREN:
        angle = math.atan2((ny - CY), (nx - CX))
        lx0 = (CX + math.cos(angle) * CR) * s
        ly0 = (CY + math.sin(angle) * CR) * s
        lx1 = (nx - math.cos(angle) * nr) * s
        ly1 = (ny - math.sin(angle) * nr) * s
        draw_line(pixels, SIZE, SIZE, lx0, ly0, lx1, ly1, LINE, line_w, 0.55)

    # Child nodes
    for i, (nx, ny, nr) in enumerate(CHILDREN):
        draw_circle(pixels, SIZE, SIZE, nx * s, ny * s, nr * s, NODES[i])

    # Central node + inner glow
    draw_circle(pixels, SIZE, SIZE, CX * s, CY * s, CR * s, CENTER)
    draw_circle(pixels, SIZE, SIZE, CX * s, CY * s, CR * 0.55 * s, GLOW, 0.45)

    return pixels

# ── PNG encoder ───────────────────────────────────────────────
def to_png(pixels, SIZE):
    def chunk(name, data):
        body = name + data
        return struct.pack('>I', len(data)) + body \
             + struct.pack('>I', zlib.crc32(body) & 0xffffffff)
    raw = b''
    for y in range(SIZE):
        raw += b'\x00'
        for x in range(SIZE):
            r, g, b = pixels[y * SIZE + x]
            raw += bytes([r, g, b])
    ihdr = struct.pack('>IIBBBBB', SIZE, SIZE, 8, 2, 0, 0, 0)
    return (b'\x89PNG\r\n\x1a\n'
            + chunk(b'IHDR', ihdr)
            + chunk(b'IDAT', zlib.compress(raw, 6))
            + chunk(b'IEND', b''))

# ── Main ──────────────────────────────────────────────────────
SIZES   = [16, 32, 48, 256]
OUT_DIR = os.path.dirname(os.path.abspath(__file__))

png_map = {}
for sz in SIZES:
    print(f"  {sz}×{sz} ...", flush=True)
    png_map[sz] = to_png(make_icon(sz), sz)
    with open(os.path.join(OUT_DIR, f'icon_{sz}.png'), 'wb') as f:
        f.write(png_map[sz])

with open(os.path.join(OUT_DIR, 'icon.png'), 'wb') as f:
    f.write(png_map[256])

# ── ICO ───────────────────────────────────────────────────────
ICO_SIZES = [16, 32, 48, 256]
count     = len(ICO_SIZES)
header    = struct.pack('<HHH', 0, 1, count)
dir_off   = 6 + 16 * count
directory = b''
images    = b''
for sz in ICO_SIZES:
    data = png_map[sz]
    w = sz if sz < 256 else 0
    h = sz if sz < 256 else 0
    directory += struct.pack('<BBBBHHII', w, h, 0, 0, 1, 32, len(data), dir_off)
    dir_off   += len(data)
    images    += data

with open(os.path.join(OUT_DIR, 'icon.ico'), 'wb') as f:
    f.write(header + directory + images)

print("Done.")
