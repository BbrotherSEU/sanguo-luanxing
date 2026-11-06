"""把绿幕全身像按脚底对齐，拼成等宽帧条。"""
import sys
from PIL import Image

def corners(px, w, h):
    pts = []
    for x, y in ((2, 2), (w - 3, 2), (2, h - 3), (w - 3, h - 3)):
        pts.append(px[x, y][:3])
    n = float(len(pts))
    return tuple(sum(p[i] for p in pts) / n for i in range(3))

def dist(c, k):
    return ((c[0] - k[0]) ** 2 + (c[1] - k[1]) ** 2 + (c[2] - k[2]) ** 2) ** 0.5

def analyze(path):
    im = Image.open(path).convert("RGBA")
    px = im.load()
    w, h = im.size
    key = corners(px, w, h)
    minx, miny, maxx, maxy = w, h, 0, 0
    foot_xs = []
    for y in range(h):
        for x in range(w):
            if dist(px[x, y], key) < 78:
                continue
            if x < minx: minx = x
            if y < miny: miny = y
            if x > maxx: maxx = x
            if y > maxy: maxy = y
    if maxx <= minx:
        return im, key, (0, 0, w - 1, h - 1), w * 0.5
    band = max(2, int((maxy - miny) * 0.12))
    for y in range(maxy - band, maxy + 1):
        for x in range(w):
            if dist(px[x, y], key) >= 78:
                foot_xs.append(x)
    foot = sum(foot_xs) / len(foot_xs) if foot_xs else (minx + maxx) * 0.5
    return im, key, (minx, miny, maxx, maxy), foot

def build(paths, out_path, breath=0):
    items = [analyze(p) for p in paths]
    heights = [b[3] - b[1] for _, _, b, _ in items]
    target = sorted(heights)[len(heights) // 2]
    scaled = []
    for im, key, (x0, y0, x1, y1), foot in items:
        bh = max(1, y1 - y0)
        scale = min(1.2, target / float(bh))
        nw, nh = max(1, int(im.width * scale)), max(1, int(im.height * scale))
        im2 = im.resize((nw, nh), Image.Resampling.LANCZOS)
        box = (int(x0 * scale), int(y0 * scale), int(x1 * scale), int(y1 * scale))
        scaled.append((im2, corners(im2.load(), nw, nh), box, foot * scale))

    half = 0
    top = 0
    for im, _, (x0, y0, x1, y1), foot in scaled:
        half = max(half, foot - x0, x1 - foot)
        top = max(top, foot and (y1 - y0))
    cell_w = int(half * 2 + 24)
    cell_h = int(target * 1.08 + 12)
    strip = Image.new("RGB", (cell_w * len(scaled), cell_h), (0, 255, 0))
    sp = strip.load()
    for i, (im, key, (x0, y0, x1, y1), foot) in enumerate(scaled):
        px = im.load()
        dx = int(i * cell_w + cell_w * 0.5 - foot)
        dy = int(cell_h - 6 - y1)
        if breath and i % 2 == 1:
            dy -= breath
        for y in range(im.height):
            ty = y + dy
            if ty < 0 or ty >= cell_h:
                continue
            for x in range(im.width):
                if dist(px[x, y], key) < 78:
                    continue
                tx = x + dx
                if 0 <= tx < strip.width:
                    r, g, b, a = px[x, y]
                    sp[tx, ty] = (r, g, b)
    strip.save(out_path, "PNG")
    print(out_path, strip.size)

if __name__ == "__main__":
    build(sys.argv[1:-1], sys.argv[-1], breath=5 if "idle" in sys.argv[-1] else 0)
