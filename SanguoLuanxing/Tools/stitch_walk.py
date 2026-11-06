"""Stitch four green-screen walk poses into an equal-cell strip.

Feet share one baseline and the torso stays centered, so the in-game
frame cutter does not hop or slide the character.
"""
import os
from PIL import Image

SRC = r"C:\Users\FanBin\.cursor\projects\c-Users-FanBin-Documents-trae-projects-r\assets"
OUT = r"C:\Users\FanBin\Documents\trae_projects\r\unity\SanguoLuanxing\Assets\StreamingAssets\assets\anim\player\walk@4.png"


def key_green(im):
    px = im.load()
    w, h = im.size
    for y in range(h):
        for x in range(w):
            r, g, b, a = px[x, y]
            if g > 80 and g > r + 24 and g > b + 24:
                excess = min(g - r, g - b)
                if excess > 60 and g > 130:
                    px[x, y] = (0, 0, 0, 0)
                else:
                    al = max(0, 255 - int(max(0, excess - 24) * 7))
                    cap = max(r, b)
                    px[x, y] = (r, min(g, cap + 8), b, al)
    return im


def measure(im):
    px = im.load()
    w, h = im.size
    minx, miny, maxx, maxy = w, h, 0, 0
    for y in range(h):
        for x in range(w):
            if px[x, y][3] > 40:
                if x < minx:
                    minx = x
                if y < miny:
                    miny = y
                if x > maxx:
                    maxx = x
                if y > maxy:
                    maxy = y
    bh = max(1, maxy - miny + 1)
    y0 = miny + int(bh * 0.32)
    y1 = miny + int(bh * 0.70)
    sx = n = 0
    for y in range(y0, y1 + 1):
        for x in range(minx, maxx + 1):
            if px[x, y][3] > 80:
                sx += x
                n += 1
    cx = (sx / n) if n else (minx + maxx) * 0.5
    return cx, maxy, bh, cx - minx, maxx - cx


def main():
    target_h = 920
    prepared = []
    max_left = 0.0
    max_right = 0.0
    for i in range(1, 5):
        im = Image.open(os.path.join(SRC, "walk_%d.jpg" % i)).convert("RGBA")
        im = key_green(im)
        cx, foot, bh, left, right = measure(im)
        scale = target_h / float(bh)
        nw = max(1, int(round(im.width * scale)))
        nh = max(1, int(round(im.height * scale)))
        sim = im.resize((nw, nh), Image.Resampling.LANCZOS)
        prepared.append((sim, cx * scale, foot * scale))
        max_left = max(max_left, left * scale)
        max_right = max(max_right, right * scale)

    pad = 16
    cell_w = int(max_left + max_right + pad * 2)
    cell_h = target_h + 18
    anchor = max_left + pad
    strip = Image.new("RGB", (cell_w * 4, cell_h), (0, 255, 0))
    for i, (sim, cx, foot) in enumerate(prepared):
        dest_cx = cell_w * i + anchor
        dest_foot = cell_h - 2
        paste_x = int(round(dest_cx - cx))
        paste_y = int(round(dest_foot - foot))
        strip.paste(sim, (paste_x, paste_y), sim)
        print("frame", i + 1, "paste", paste_x, paste_y, "cell", cell_w)

    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    strip.save(OUT, "PNG")
    print("wrote", OUT, strip.size)


if __name__ == "__main__":
    main()
