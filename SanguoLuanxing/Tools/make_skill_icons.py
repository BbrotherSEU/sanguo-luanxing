"""生成技能图标（PNG），替代 Unity 端 1px 程序化笔触。

问题
----
Unity 端 Tex.MakeSkillIcons 用逐像素笔刷画图标：线条只有 1~2px 宽，
在 68x68 的按钮里几乎看不见，用户反馈"技能还是只有文字，没有图标"。

做法
----
先在 512 画布上把图形画成"遮罩"（黑白剪影），再由遮罩派生三层：
    1. 描边层：遮罩做 MaxFilter 膨胀 → 深色轮廓
    2. 主体层：遮罩本身 → 主色
    3. 高光层：遮罩整体上移几像素再与自身求交 → 顶部提亮
最后缩到 128 输出。这样不管图形多细，边缘都是干净的一圈描边，
压在深色按钮上也能一眼认出。

图形语义：
    Q 破军突刺 —— 长枪直刺（枪身 + 菱形枪尖 + 飘动枪缨）
    E 横扫千军 —— 旋转刀弧（弧线 + 双箭头）
    R 武神降临 —— 陨石坠落 + 地面冲击环 + 光柱
"""
import math
import os

from PIL import Image, ImageDraw, ImageFilter

S = 512          # 绘制分辨率
OUT = 128        # 输出分辨率

HERE = os.path.dirname(os.path.abspath(__file__))
OUT_DIR = os.path.join(HERE, "..", "Assets", "StreamingAssets", "assets", "ui")
PREVIEW = os.path.join(HERE, "..", "..", "..", ".tmp_gen", "skill_icons.png")


def mask():
    return Image.new("L", (S, S), 0)


def poly(d, pts):
    d.polygon([(x, y) for x, y in pts], fill=255)


def tapered(d, p0, p1, w0, w1):
    """带锥度的笔触：两端宽度不同，尖的一端收成锋。"""
    (x0, y0), (x1, y1) = p0, p1
    dx, dy = x1 - x0, y1 - y0
    L = math.hypot(dx, dy) or 1.0
    nx, ny = -dy / L, dx / L
    poly(d, [(x0 + nx * w0, y0 + ny * w0),
             (x1 + nx * w1, y1 + ny * w1),
             (x1 - nx * w1, y1 - ny * w1),
             (x0 - nx * w0, y0 - ny * w0)])


def shape_q():
    """破军突刺：长枪向前直刺。"""
    m = mask()
    d = ImageDraw.Draw(m)
    cy = 300
    # 枪缨：身后三笔飘带
    for (bx, by, tx, ty) in ((200, 236, 96, 150), (186, 292, 78, 268), (192, 352, 92, 404)):
        tapered(d, (bx, by), (tx, ty), 22, 5)
    # 枪身
    d.rectangle([140, cy - 15, 352, cy + 15], fill=255)
    # 菱形枪尖
    poly(d, [(344, cy - 62), (470, cy), (344, cy + 62), (382, cy)])
    return m


def ring_arc(d, cx, cy, r, a0, a1, w0, w1, n=90):
    """沿圆弧扫出一段带锥度的笔画：起点宽 w0，末端收成 w1。"""
    for i in range(n):
        t0, t1 = i / n, (i + 1) / n
        p0 = math.radians(a0 + (a1 - a0) * t0)
        p1 = math.radians(a0 + (a1 - a0) * t1)
        tapered(d, (cx + math.cos(p0) * r, cy + math.sin(p0) * r),
                (cx + math.cos(p1) * r, cy + math.sin(p1) * r),
                w0 + (w1 - w0) * t0, w0 + (w1 - w0) * t1)


def shape_e():
    """横扫千军：一整圈旋斩刀弧 + 末端箭头。"""
    m = mask()
    d = ImageDraw.Draw(m)
    cx, cy, r = 268, 268, 150
    a0, a1 = -86, 168
    ring_arc(d, cx, cy, r, a0, a1, 44, 16)
    # 末端箭头（沿切线方向）
    a = math.radians(a1)
    bx, by = cx + math.cos(a) * r, cy + math.sin(a) * r
    tx, ty = -math.sin(a), math.cos(a)
    nx, ny = math.cos(a), math.sin(a)
    poly(d, [(bx + tx * 96, by + ty * 96),
             (bx - nx * 62 + tx * 4, by - ny * 62 + ty * 4),
             (bx + nx * 62 + tx * 4, by + ny * 62 + ty * 4)])
    return m


def shape_r():
    """武神降临：长剑自天而降插进地面，碎片四溅。"""
    m = mask()
    d = ImageDraw.Draw(m)
    # 剑身（上宽下收成锋）
    poly(d, [(232, 150), (280, 150), (262, 344), (250, 344)])
    # 护手
    d.rectangle([160, 138, 352, 176], fill=255)
    # 剑柄
    d.rectangle([240, 52, 272, 142], fill=255)
    # 柄首
    d.ellipse([222, 10, 290, 66], fill=255)
    # 落地冲击：四向碎片
    for a_deg, ln in ((-152, 96), (-118, 70), (-62, 70), (-28, 96)):
        a = math.radians(a_deg)
        tapered(d, (262 + math.cos(a) * 26, 356 + math.sin(a) * 16),
                (262 + math.cos(a) * ln, 356 + math.sin(a) * (ln * 0.5)), 24, 6)
    return m


def render(shape, body, accent, edge, hi):
    """把剪影变成"描边 + 主体 + 高光"的图标。"""
    m = shape
    # 描边：膨胀后减去本体
    grown = m.filter(ImageFilter.MaxFilter(15))
    outline = Image.new("L", (S, S), 0)
    outline.paste(grown, (0, 0), ImageChopsSub(grown, m))
    # 高光：整体上移后与本体相交
    up = m.transform((S, S), Image.AFFINE, (1, 0, 0, 0, 1, -18))
    hilite = ImageChopsMul(m, up)

    out = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    out.paste(edge + (255,), (0, 0), outline)
    out.paste(body + (255,), (0, 0), m)
    out.paste(accent + (255,), (0, 0), hilite)
    return out


def ImageChopsSub(a, b):
    from PIL import ImageChops
    return ImageChops.subtract(a, b)


def ImageChopsMul(a, b):
    from PIL import ImageChops
    return ImageChops.multiply(a, b)


ICONS = {
    "icon_q": (shape_q, (255, 214, 120), (255, 160, 70), (44, 30, 18), (255, 246, 214)),
    "icon_e": (shape_e, (255, 226, 150), (255, 158, 64), (40, 28, 18), (255, 248, 222)),
    "icon_r": (shape_r, (255, 226, 158), (255, 146, 62), (44, 28, 16), (255, 246, 216)),
}


def main():
    out = os.path.abspath(OUT_DIR)
    os.makedirs(out, exist_ok=True)
    tiles = []
    for name, (fn, body, accent, edge, hi) in ICONS.items():
        im = render(fn(), body, accent, edge, hi).resize((OUT, OUT), Image.Resampling.LANCZOS)
        path = os.path.join(out, name + ".png")
        im.save(path)
        print("wrote", path)
        tiles.append(im)

    # 预览：叠在 HUD 实际的深色按钮底上
    B = 68
    prev = Image.new("RGB", (B * len(tiles) + 12 * (len(tiles) + 1), B + 10), (20, 17, 15))
    x = 12
    for im in tiles:
        small = im.resize((B - 10, B - 10), Image.Resampling.LANCZOS)
        prev.paste(small, (x + 5, 5), small)
        x += B + 12
    p = os.path.abspath(PREVIEW)
    os.makedirs(os.path.dirname(p), exist_ok=True)
    prev.save(p)
    print("preview ->", p)


if __name__ == "__main__":
    main()
