"""程序化生成场景贴图（地面 + 建筑），全部无缝平铺。

关键约定：地面 / 岩石贴图做成「近中性」——只提供明暗结构，色相由顶点色给。
这样同一张草地图在桃林是粉绿、秋野是黄褐、暮色是暗灰绿，不会一片死绿。

产出（写入 Assets/StreamingAssets/assets/tex/）：
  wall_plaster.jpg       夯土抹灰墙：横向层理 + 剥落露砖 + 水渍 + 裂缝
  roof_tile_neutral.jpg  中性灰瓦：筒瓦弧面 + 瓦缝 + 叠压阴影，运行时由顶点色染色
  rock_neutral.jpg       中性岩面：斑驳 + 裂纹 + 颗粒，用作崖壁 / 石构件 / 墙基
  ground_grass_fine.jpg  中性草叶：密而细的草叶，地面主层
  ground_road_dirt.jpg   中性夯土：碎石 + 车辙，路面主层

用法：python gen_scene_tex.py
"""
import os

import numpy as np
from PIL import Image, ImageDraw, ImageFilter

HERE = os.path.dirname(os.path.abspath(__file__))
PROJECT = os.path.abspath(os.path.dirname(HERE))
OUT = os.path.join(PROJECT, "Assets", "StreamingAssets", "assets", "tex")
S = 1024


def pnoise(w, h, cells, rng):
    """周期性值噪声：cells=(cx, cy)，左右上下自动接缝。"""
    cx, cy = (cells, cells) if isinstance(cells, (int, float)) else cells
    cx, cy = max(1, int(cx)), max(1, int(cy))
    g = rng.random((cy, cx)).astype(np.float32)
    xs = np.arange(w, dtype=np.float32) * cx / w
    ys = np.arange(h, dtype=np.float32) * cy / h
    x0 = np.floor(xs).astype(np.int32)
    y0 = np.floor(ys).astype(np.int32)
    x1 = (x0 + 1) % cx
    y1 = (y0 + 1) % cy
    fx = xs - x0
    fy = ys - y0
    fx = fx * fx * (3 - 2 * fx)
    fy = fy * fy * (3 - 2 * fy)
    a = g[np.ix_(y0, x0)]
    b = g[np.ix_(y0, x1)]
    c = g[np.ix_(y1, x0)]
    d = g[np.ix_(y1, x1)]
    top = a * (1 - fx)[None, :] + b * fx[None, :]
    bot = c * (1 - fx)[None, :] + d * fx[None, :]
    return top * (1 - fy)[:, None] + bot * fy[:, None]


def fbm(w, h, base, octaves, rng, gain=0.5):
    total = np.zeros((h, w), np.float32)
    amp, norm = 1.0, 0.0
    cells = base
    for _ in range(octaves):
        total += pnoise(w, h, cells, rng) * amp
        norm += amp
        amp *= gain
        cells = (cells[0] * 2, cells[1] * 2) if isinstance(cells, tuple) else cells * 2
    return total / max(norm, 1e-5)


def blur(a, radius):
    """环绕模糊，保证接缝一致。"""
    if radius <= 0:
        return a
    pad = int(radius * 3)
    big = np.pad(a, pad, mode="wrap")
    img = Image.fromarray(big.astype(np.uint8), "L").filter(ImageFilter.GaussianBlur(radius))
    return np.asarray(img, np.float32)[pad:pad + a.shape[0], pad:pad + a.shape[1]]


def wrapped_draw(draw, fn):
    """在 3x3 平铺上绘制，只保留中间，得到无缝图案。"""
    for ox in (-S, 0, S):
        for oy in (-S, 0, S):
            fn(draw, ox, oy)


def save(name, rgb):
    rgb = np.clip(rgb, 0, 255).astype(np.uint8)
    img = Image.fromarray(rgb, "RGB")
    path = os.path.join(OUT, name)
    d = os.path.dirname(path)
    if d and not os.path.isdir(d):
        os.makedirs(d, exist_ok=True)
    img.save(path, quality=93, subsampling=0)
    arr = np.asarray(img, np.float32) / 255.0
    lum = float((arr * np.array([0.30, 0.59, 0.11], np.float32)).sum(-1).mean())
    std = float((arr * np.array([0.30, 0.59, 0.11], np.float32)).sum(-1).std())
    print("  %-26s %dx%d  mean-lum=%.3f  std=%.4f" % (name, S, S, lum, std))


# ---------------------------------------------------------------- 夯土抹灰墙
def plaster():
    """墙是建筑最大的一块面，必须有明暗结构，否则就是一块白板。
    做法：横向夯土层理（贴图 ~1.8m 一铺 → 每层 9cm）+ 剥落露砖 + 水渍 + 裂缝。"""
    rng = np.random.default_rng(20261104)
    layers = 20
    ly = np.arange(S, dtype=np.float32) * layers / S
    li = np.floor(ly).astype(np.int32) % layers
    fy = ly - np.floor(ly)

    # 每层泥料本身的明暗差
    ljit = (rng.random(layers).astype(np.float32) - 0.5) * 0.22
    lum = np.ones((S, S), np.float32) + ljit[li][:, None]

    # 层缝：层底压暗、层顶稍亮，横向连续才像夯出来的
    seam_a = np.clip(1.0 - fy / 0.075, 0, 1)[:, None]
    seam_b = np.clip((fy - 0.92) / 0.08, 0, 1)[:, None]
    lum *= 1.0 - seam_a * 0.26 - seam_b * 0.12

    # 层纹不规则：横向拉长的噪声把层缝推歪一点
    wob = fbm(S, S, (3, 26), 3, rng) - 0.5
    lum *= 1.0 + wob * 0.15

    grain = fbm(S, S, (70, 70), 2, rng) - 0.5
    stain = np.clip((fbm(S, S, (3, 3), 4, rng) - 0.55) * 1.8, 0, 1)
    lum *= 1.0 + grain * 0.09 - stain * 0.13

    gray = Image.fromarray(np.clip(lum * 255, 0, 255).astype(np.uint8), "L").convert("RGB")

    # 剥落露砖：不规则斑块 + 羽化边缘。砖必须比粉墙暗，否则斑块会变成白饼。
    # 数量要少：贴图 1.8 米一铺，斑块多了每面墙都长成"豹纹"。
    patch = Image.new("L", (S, S), 0)
    dp = ImageDraw.Draw(patch)
    blobs = []
    for _ in range(3):
        cx, cy = rng.uniform(0, S), rng.uniform(0, S)
        rw, rh = rng.uniform(45, 85), rng.uniform(34, 64)
        pts = []
        for i in range(15):
            a = i / 15 * np.pi * 2
            rr = rng.uniform(0.7, 1.0)
            pts.append((cx + np.cos(a) * rw * rr, cy + np.sin(a) * rh * rr))
        blobs.append((pts, rw, rh))
        for ox, oy in ((-S, -S), (-S, 0), (-S, S), (0, -S), (0, 0), (0, S), (S, -S), (S, 0), (S, S)):
            dp.polygon([(px + ox, py + oy) for px, py in pts], fill=255)
    mask = blur(np.asarray(patch, np.float32) / 255.0, 14.0)

    # 青砖层：整幅画砖缝，再用斑块 mask 裁进去，砖缝才不会跑到墙上
    brick = Image.new("RGB", (S, S), (118, 121, 122))
    db = ImageDraw.Draw(brick)
    step = 64   # 整除 1024，砖缝才能无缝平铺
    for gy in range(0, S + step, step):
        db.line([(0, gy), (S, gy)], fill=(84, 87, 90), width=4)
    row = 0
    for gy in range(0, S + step, step):
        off = (row % 2) * step // 2
        for gx in range(-step, S + step, step):
            db.line([(gx + off, gy), (gx + off, gy + step)], fill=(84, 87, 90), width=4)
        row += 1
    barr = np.asarray(brick, np.float32) / 255.0

    arr = np.asarray(gray, np.float32) / 255.0
    gray_l = arr.mean(-1)
    # 斑块内换成青砖，边缘留一圈暗边（剥落的深度）
    lum2 = gray_l * (1.0 - mask * 0.92) + barr.mean(-1) * mask * 0.92
    rim = np.clip(mask - blur(mask, 3.0), 0, 1)
    lum2 *= 1.0 - rim * 0.18

    # 细裂纹：短折线，低对比，只在近处看得到
    layer = Image.new("L", (S, S), 0)
    dc = ImageDraw.Draw(layer)
    for _ in range(34):
        x, y = rng.uniform(0, S), rng.uniform(0, S)
        ang = rng.uniform(0, np.pi * 2)
        pts = [(x, y)]
        for _ in range(int(rng.integers(5, 11))):
            ang += rng.normal(0, 0.55)
            step = rng.uniform(10, 26)
            x += np.cos(ang) * step
            y += np.sin(ang) * step
            pts.append((x, y))
        alpha = int(rng.uniform(38, 74))
        w = 1 if rng.random() < 0.7 else 2
        wrapped_draw(dc, lambda dd, ox, oy, p=pts, a=alpha, ww=w:
                     dd.line([(px + ox, py + oy) for px, py in p], fill=a, width=ww))
    cracks = blur(np.asarray(layer, np.float32) / 255.0, 1.0)
    lum2 *= 1.0 - cracks * 0.34

    # 墙体偏暖白：色相最终由顶点色给，这里只留一点物理真实感
    rgb = np.dstack([
        lum2 * 238,
        lum2 * 229,
        lum2 * 212,
    ])
    save("wall_plaster.jpg", rgb)


# ---------------------------------------------------------------- 中性岩面
def rock_neutral():
    """崖壁用。中性灰，色相交给顶点色（青石 / 暖石 / 苔石都靠顶点色区分）。
    岩石的体积感靠"块面明暗"表达，不画显式裂纹线 —— 线会像涂鸦。"""
    rng = np.random.default_rng(20261111)
    base = 150.0

    # 大中小三级斑驳：大块是岩体起伏，中块是石面，小块是颗粒
    big = fbm(S, S, (3, 3), 5, rng)
    mid = fbm(S, S, (7, 7), 4, rng)
    fine = fbm(S, S, (18, 18), 3, rng)
    grain = fbm(S, S, (60, 60), 2, rng)
    lum = 1.0 + (big - 0.5) * 0.42 + (mid - 0.5) * 0.28 + (fine - 0.5) * 0.15 + (grain - 0.5) * 0.06

    # 块面：把中频噪声的极值处提亮、极小值处压暗，形成"有棱有面"的观感
    mid_s = (mid - mid.mean()) / max(mid.std(), 1e-4)
    lum *= 1.0 + np.clip(mid_s, 0, 3) * 0.10 - np.clip(-mid_s, 0, 3) * 0.13

    rgb = np.dstack([lum * base] * 3)
    img = Image.fromarray(np.clip(rgb, 0, 255).astype(np.uint8), "RGB")

    # 崩落碎屑：贴着岩石表面的浅色小点
    dd = ImageDraw.Draw(img)
    for _ in range(260):
        x, y = rng.uniform(0, S), rng.uniform(0, S)
        r = rng.uniform(1.0, 2.6)
        t = int(base * rng.uniform(0.86, 1.14))
        wrapped_draw(dd, lambda d2, ox, oy, xx=x, yy=y, rr=r, tt=t:
                     d2.ellipse([xx + ox - rr, yy + oy - rr, xx + ox + rr, yy + oy + rr], fill=(tt, tt, tt)))
    save("rock_neutral.jpg", np.asarray(img, np.float32))



# ---------------------------------------------------------------- 灰瓦（顶点色染色）
def roof_tile():
    rng = np.random.default_rng(20261105)
    cols, rows = 8, 12
    x = np.arange(S, dtype=np.float32)
    y = np.arange(S, dtype=np.float32)
    uc = x * cols / S
    vr = y * rows / S
    ci = np.floor(uc).astype(np.int32) % cols
    ri = np.floor(vr).astype(np.int32) % rows
    u = (uc - np.floor(uc))[None, :]
    v = (vr - np.floor(vr))[:, None]

    # 每块瓦的明暗差异（周期表，平铺后不跳）
    jit = rng.normal(0, 0.05, (rows, cols)).astype(np.float32)
    tj = jit[ri][:, ci]

    barrel = 0.64 + 0.36 * np.power(np.sin(np.pi * u), 0.55)     # 筒瓦弧面
    spec = 0.09 * np.exp(-((u - 0.48) / 0.085) ** 2)            # 弧顶高光
    edge = np.minimum(u, 1 - u)
    groove = 1.0 - 0.5 * np.clip(1.0 - edge / 0.032, 0, 1)      # 窄瓦缝
    # 瓦片底缘是圆弧：每列的排缝在中线处往下坠一点
    vv = (v - 0.05 * np.sin(np.pi * u)) % 1.0
    lip = 0.16 * np.exp(-((vv - 0.955) / 0.018) ** 2)           # 瓦唇亮口
    shadow = 1.0 - 0.42 * np.clip(1.0 - vv / 0.055, 0, 1) ** 1.4
    slope = 0.94 + 0.07 * v                                     # 檐口略亮

    lum = (barrel + spec + lip) * groove * shadow * slope * (1.0 + tj)
    lum = np.clip(lum, 0.16, 1.25)

    # 细瓦面颗粒 + 竖向雨水痕
    grain = fbm(S, S, (90, 90), 2, rng) - 0.5
    wet = fbm(S, S, (2, 10), 3, rng) - 0.5
    lum *= 1.0 + grain * 0.05 + wet * 0.055

    base = np.array([176, 172, 166], np.float32)                 # 近中性灰，色相由顶点色给
    rgb = base[None, None, :] * lum[..., None]
    # 瓦面不是纯灰：高光处略暖，缝里略冷
    rgb[..., 0] += lum * 6.0
    rgb[..., 2] -= lum * 4.0
    rgb *= 1.0 + (rng.random((S, S, 1)).astype(np.float32) - 0.5) * 0.02
    save("roof_tile_neutral.jpg", rgb)


# ---------------------------------------------------------------- 草地面（中性灰）
def grass_field(name, base, pal, dry, seed, clumps=900, amp=1.0):
    """通用草地：低频色块 + 草丛簇 + 枯斑。
    贴图做成中性灰：着色器把它归一化成"相对明暗"再乘回顶点色，
    所以草的色相由 Theme.GrassA/B 给，这里只负责结构和明暗。

    关键：草要画成"一簇一簇"而不是均匀撒细叶。
    细叶在 5 米开外就被 mipmap 抹平，簇 + 暗影才在游戏视距里读得出来。"""
    rng = np.random.default_rng(seed)
    big = fbm(S, S, (3, 3), 4, rng)
    mid = fbm(S, S, (10, 10), 3, rng)
    grain = fbm(S, S, (64, 64), 2, rng)

    lum = 1.0 + (big - 0.5) * 0.24 * amp + (mid - 0.5) * 0.16 * amp + (grain - 0.5) * 0.06
    drymask = np.clip((big - 0.55) * 2.4, 0, 1)
    v = base * lum + drymask * dry
    rgb = np.dstack([v, v, v])
    img = Image.fromarray(np.clip(rgb, 0, 255).astype(np.uint8), "RGB")

    # 簇底的暗影：远看时一片片的深浅就来自这里。这是草地最主要的可读特征。
    shadow = Image.new("L", (S, S), 0)
    ds = ImageDraw.Draw(shadow)
    for _ in range(clumps):
        x, y = rng.uniform(0, S), rng.uniform(0, S)
        r = rng.uniform(20, 46)
        for ox, oy in ((-S, -S), (-S, 0), (-S, S), (0, -S), (0, 0), (0, S), (S, -S), (S, 0), (S, S)):
            ds.ellipse([x + ox - r, y + oy - r * 0.5, x + ox + r, y + oy + r * 0.5],
                       fill=int(rng.uniform(140, 235)))
    smask = blur(np.asarray(shadow, np.float32) / 255.0, 11.0)
    arr = np.asarray(img, np.float32) * (1.0 - smask[..., None] * 0.34)
    img = Image.fromarray(np.clip(arr, 0, 255).astype(np.uint8), "RGB")

    # 簇里的草叶：一簇约 20~40 片，长短不一，向同一侧倒。
    # 草叶本身对比要低 —— 高频细节在游戏视距里只会变成噪点，
    # 真正让人读出"草"的是簇影和大块起伏。
    layer = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    d = ImageDraw.Draw(layer)
    for _ in range(clumps):
        cx, cy = rng.uniform(0, S), rng.uniform(0, S)
        spread = rng.uniform(16, 34)
        dirx = rng.normal(0, 0.45)
        for _ in range(int(rng.integers(14, 32))):
            x = cx + rng.normal(0, spread * 0.5)
            y = cy + rng.normal(0, spread * 0.22)
            h = rng.uniform(14, 34)
            lean = dirx + rng.normal(0, 0.22)
            g = pal[int(rng.integers(0, len(pal)))]
            a = int(rng.uniform(90, 190))
            w = 2 if rng.random() < 0.5 else 1
            pts = [(x, y), (x + np.sin(lean) * h * 0.6, y - h), (x + np.sin(lean) * h, y - h * 0.55)]
            wrapped_draw(d, lambda dd, ox, oy, p=pts, c=g, aa=a, ww=w:
                         dd.line([(px + ox, py + oy) for px, py in p], fill=(c, c, c, aa), width=ww))
    # 不画白花：中性灰贴图上白点会变成刺眼的亮斑，彩色的花在着色器里也拿不到颜色
    img = Image.alpha_composite(img.convert("RGBA"), layer).convert("RGB")
    arr = np.asarray(img, np.float32)

    # 大尺度起伏：贴图内也带低频明暗，远看才不会是一层均匀噪点。
    # 用 1~2 格的噪声当"草皮起伏"，在 3 米一铺的尺度下就是 1~2 米的大块深浅。
    swell = fbm(S, S, (1, 1), 3, rng) - 0.5
    swell2 = fbm(S, S, (2, 2), 3, rng) - 0.5
    arr *= 1.0 + (swell * 0.38 + swell2 * 0.22)[..., None] * amp
    save(name, arr)


def grass_fine():
    grass_field(
        "ground_grass_fine.jpg",
        base=150.0,
        pal=[92, 108, 150, 84, 132],
        dry=-18,
        seed=20261106,
        clumps=850,
    )


def grass_autumn():
    grass_field(
        "ground_grass_autumn.jpg",
        base=148.0,
        pal=[104, 118, 142, 96, 132],
        dry=-16,
        seed=20261108,
        clumps=780,
    )


def grass_dusk():
    grass_field(
        "ground_grass_dusk.jpg",
        base=142.0,
        pal=[100, 112, 126, 92, 120],
        dry=-12,
        seed=20261109,
        clumps=700,
        amp=0.85,
    )


def grass_snow():
    """雪地：没有草叶，只有起伏、枯梗和冰晶。中性灰，色相交给顶点色。"""
    rng = np.random.default_rng(20261110)
    base = 200.0
    big = fbm(S, S, (3, 3), 4, rng)
    mid = fbm(S, S, (9, 9), 3, rng)
    grain = fbm(S, S, (56, 56), 2, rng)
    lum = 1.0 + (big - 0.5) * 0.11 + (mid - 0.5) * 0.07 + (grain - 0.5) * 0.035
    v = base * lum
    rgb = np.dstack([v, v, v])
    img = Image.fromarray(np.clip(rgb, 0, 255).astype(np.uint8), "RGB")
    layer = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    d = ImageDraw.Draw(layer)
    # 雪里露出的枯草梗
    for _ in range(1500):
        x, y = rng.uniform(0, S), rng.uniform(0, S)
        h = rng.uniform(7, 18)
        lean = rng.normal(0, 0.35)
        a = int(rng.uniform(45, 105))
        tone = int(rng.uniform(130, 168))
        pts = [(x, y), (x + np.sin(lean) * h * 0.6, y - h)]
        wrapped_draw(d, lambda dd, ox, oy, p=pts, aa=a, tt=tone:
                     dd.line([(px + ox, py + oy) for px, py in p], fill=(tt, tt, tt, aa), width=1))
    # 冰晶亮点
    for _ in range(320):
        x, y = rng.uniform(0, S), rng.uniform(0, S)
        r = rng.uniform(0.8, 1.9)
        wrapped_draw(d, lambda dd, ox, oy, xx=x, yy=y, rr=r:
                     dd.ellipse([xx + ox - rr, yy + oy - rr, xx + ox + rr, yy + oy + rr],
                                fill=(255, 255, 255, int(rng.uniform(150, 230)))))
    img = Image.alpha_composite(img.convert("RGBA"), layer).convert("RGB")
    save("ground_grass_snow.jpg", np.asarray(img, np.float32))


# ---------------------------------------------------------------- 夯土路面
def road_dirt():
    """夯土路：中性灰。色相由 Theme.Path 给，这里只负责车辙和碎石明暗。"""
    rng = np.random.default_rng(20261107)
    base = 168.0
    big = fbm(S, S, (3, 3), 4, rng)
    mid = fbm(S, S, (9, 9), 3, rng)
    grain = fbm(S, S, (80, 80), 2, rng)

    lum = 1.0 + (big - 0.5) * 0.18 + (mid - 0.5) * 0.10 + (grain - 0.5) * 0.06
    # 两条车辙：贴图 u 横跨路宽，v 沿路方向
    u = np.arange(S, dtype=np.float32)[None, :] / S
    rut = np.exp(-((u - 0.33) / 0.075) ** 2) + np.exp(-((u - 0.67) / 0.075) ** 2)
    rut = np.clip(rut, 0, 1)
    lum = lum * (1.0 - rut * 0.13)

    v = base * lum
    rgb = np.dstack([v, v, v])

    img = Image.fromarray(np.clip(rgb, 0, 255).astype(np.uint8), "RGB")
    d = ImageDraw.Draw(img)

    def pebble(x, y, rx, ry, tone):
        box = [x - rx, y - ry, x + rx, y + ry]
        hl = [box[0] + rx * 0.24, box[1] + ry * 0.20, box[2] - rx * 0.22, box[3] - ry * 0.48]
        sh = [box[0] + rx * 0.20, box[1] + ry * 0.42, box[2] - rx * 0.20, box[3] - ry * 0.45]
        for ox, oy in ((-S, -S), (-S, 0), (-S, S), (0, -S), (0, 0), (0, S), (S, -S), (S, 0), (S, S)):
            d.ellipse([sh[0] + ox, sh[1] + oy, sh[2] + ox, sh[3] + oy], fill=(tone - 46,) * 3)
            d.ellipse([box[0] + ox, box[1] + oy, box[2] + ox, box[3] + oy], fill=(tone, tone - 8, tone - 20))
            if rx > 3.2:
                d.ellipse([hl[0] + ox, hl[1] + oy, hl[2] + ox, hl[3] + oy], fill=(tone + 20,) * 3)

    # 碎石：颗粒放大、密度降低，靠近了才看得清，远处不变成噪点
    for _ in range(300):
        x, y = rng.uniform(0, S), rng.uniform(0, S)
        rx = rng.uniform(3.0, 7.5)
        ry = rx * rng.uniform(0.6, 0.95)
        pebble(x, y, rx, ry, int(rng.uniform(150, 196)))
    for _ in range(18):
        x, y = rng.uniform(0, S), rng.uniform(0, S)
        rx = rng.uniform(9, 14)
        ry = rx * rng.uniform(0.55, 0.8)
        pebble(x, y, rx, ry, int(rng.uniform(158, 188)))

    # 细砂粒：低对比小点，让土面不平
    for _ in range(2000):
        x, y = rng.uniform(0, S), rng.uniform(0, S)
        tone = int(rng.uniform(118, 202))
        wrapped_draw(d, lambda dd, ox, oy, xx=x, yy=y, cc=tone:
                     dd.point((xx + ox, yy + oy), fill=(cc, cc - 4, cc - 10)))

    arr = np.asarray(img, np.float32)
    arr = arr * 0.97 + np.asarray(blur(arr[..., 0], 1.2), np.float32)[..., None] * 0.03
    save("ground_road_dirt.jpg", arr)


# ---------------------------------------------------------------- 宣纸底纹
def ui_paper():
    """UI 面板的纸纹：纤维 + 云斑 + 轻微污渍。做成很淡的灰白，靠 UI 叠加显影。"""
    rng = np.random.default_rng(20261112)
    base = 210.0
    # 纤维：横向拉伸的高频噪声
    fibre = fbm(S, S, (2, 90), 3, rng) - 0.5
    # 云斑：宣纸的厚薄不均
    cloud = fbm(S, S, (4, 4), 4, rng) - 0.5
    # 细颗粒
    grain = fbm(S, S, (120, 120), 2, rng) - 0.5
    v = base + fibre * 26 + cloud * 20 + grain * 9
    # 几处淡黄渍，像旧纸
    stain = np.clip((fbm(S, S, (3, 3), 3, rng) - 0.58) * 3.2, 0, 1)
    rgb = np.dstack([v + stain * 12, v + stain * 6, v - stain * 10])
    save("ui/ui_paper.jpg", np.clip(rgb, 0, 255))


if __name__ == "__main__":
    os.makedirs(OUT, exist_ok=True)
    print("生成场景贴图 ->", OUT)
    plaster()
    roof_tile()
    rock_neutral()
    grass_fine()
    grass_autumn()
    grass_dusk()
    grass_snow()
    road_dirt()
    ui_paper()
    print("完成。")
