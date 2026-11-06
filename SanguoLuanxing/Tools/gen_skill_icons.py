"""生成三个技能图标：统一底座 + 统一画风。

原来的图标是三张来源不同的扁平图（箭头黄 / 扇形橙 / 剑红），
放在技能栏里不像一套。这里用同一套规则重画：
  - 深色圆底 + 金色外圈 + 顶部高光 + 底部暗影
  - 图标本体：黑描边 + 金橙渐变填充，全套同一笔触
  - 破军突刺 = 前冲的箭簇
  - 横扫千军 = 环形横扫的刀风
  - 武神降临 = 竖立的剑

用法：python gen_skill_icons.py
"""
import math
import os

import numpy as np
from PIL import Image, ImageDraw, ImageFilter

HERE = os.path.dirname(os.path.abspath(__file__))
PROJECT = os.path.abspath(os.path.dirname(HERE))
UI = os.path.join(PROJECT, "Assets", "StreamingAssets", "assets", "ui")
S = 256
C = (S - 1) * 0.5
R = S * 0.46

INK = (26, 20, 16)          # 描边
GOLD_HI = (255, 214, 130)   # 渐变亮端
GOLD_LO = (198, 128, 44)    # 渐变暗端


def base_layer():
    """圆底 + 金圈 + 高光 + 暗影。"""
    yy, xx = np.mgrid[0:S, 0:S].astype(np.float32)
    d = np.sqrt((xx - C) ** 2 + (yy - C) ** 2)

    inner = np.clip((d - R * 0.30) / (R * 0.75), 0, 1)
    body = np.array([24, 20, 17], np.float32)[None, None, :]
    body = body + (np.array([48, 39, 29], np.float32) - body) * (1 - inner)[..., None]

    # 金圈：外深内亮的金属渐变
    ring_t = np.clip((d - R * 0.855) / (R * 0.145), 0, 1)
    ring = np.array([150, 112, 46], np.float32)[None, None, :] * ring_t[..., None]
    ring += np.array([90, 68, 24], np.float32)[None, None, :] * (1 - ring_t)[..., None]
    body = body * (1 - ring_t[..., None]) + ring * ring_t[..., None]

    # 内圈暗线，把图标和底座隔开
    inner_line = np.exp(-((d - R * 0.845) / (S * 0.011)) ** 2)
    body *= (1 - inner_line[..., None] * 0.55)

    # 顶部高光 / 底部暗影
    hl = np.exp(-((d - R * 0.60) / (S * 0.06)) ** 2) * np.clip(-(yy - C) / (R * 0.95), 0, 1)
    body += hl[..., None] * np.array([50, 44, 34], np.float32)[None, None, :]
    sh = np.exp(-((d - R * 0.78) / (S * 0.065)) ** 2) * np.clip((yy - C) / (R * 0.95), 0, 1)
    body *= (1 - sh[..., None] * 0.32)

    alpha = np.clip((R + 1.5 - d) / 2.0, 0, 1)
    return body, alpha


def draw_icon(layer, shape):
    """在 layer 上画图标本体：外扩黑描边 + 金橙渐变填充。

    做法：同一形状画两遍 —— 外扩版当描边，本体版当填充。
    """
    # 填充：形状本体，用金橙渐变。
    # 填充层只关心形状（alpha），颜色由渐变提供，所以画的是单通道。
    fill_mask = Image.new("L", (S, S), 0)
    df = ImageDraw.Draw(fill_mask)
    shape(df, 0, 255)

    grad = np.zeros((S, S, 4), np.float32)
    yy = np.mgrid[0:S, 0:S][0].astype(np.float32)
    t = np.clip((yy - (C - R * 0.55)) / (R * 1.1), 0, 1)
    for i in range(3):
        grad[:, :, i] = GOLD_HI[i] * (1 - t) + GOLD_LO[i] * t
    grad[:, :, 3] = 255
    grad_img = Image.fromarray(grad.astype(np.uint8), "RGBA")
    grad_img.putalpha(fill_mask)

    # 描边：形状外扩一圈纯黑，压在填充下面
    stroke = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    ds = ImageDraw.Draw(stroke)
    shape(ds, 3, INK)
    stroke = stroke.filter(ImageFilter.GaussianBlur(0.7))

    out = Image.alpha_composite(stroke, grad_img)
    layer.alpha_composite(out)


def make(name, shape_fn):
    body, alpha = base_layer()
    layer = Image.fromarray(np.dstack([np.clip(body, 0, 255), alpha * 255]).astype(np.uint8), "RGBA")
    draw_icon(layer, shape_fn)
    layer.save(os.path.join(UI, name))
    print("  %-12s -> %s" % (name, layer.size))


# ---------------------------------------------------------------- 图标形状
def icon_charge(d, grow, col):
    """破军突刺：一支前冲的箭 + 两道速度线。剪影要清楚，细节多反而糊。"""
    cx, cy = C + 8, C - 2
    # 主箭：粗杆 + 大箭头
    tail = cx - 46
    head = cx + 46
    d.line([(tail, cy), (head - 12, cy)], fill=col, width=13 + grow * 2)
    hs = 30 + grow
    d.polygon([(head + 10, cy), (head - 14, cy - hs), (head - 6, cy), (head - 14, cy + hs)], fill=col)
    # 箭尾羽
    d.polygon([(tail + 6, cy), (tail - 14, cy - 17), (tail - 4, cy), (tail - 14, cy + 17)], fill=col)
    # 两道速度线
    for i, off in enumerate((-26, 26)):
        d.line([(tail - 6, cy + off), (tail - 34, cy + off)], fill=col, width=(8 - i * 2) + grow * 2)


def icon_whirl(d, grow, col):
    """横扫千军：环身刀风（开口的粗圆弧 + 几道风刃）。"""
    r = 60 + grow
    cx, cy = C, C + 2
    box = [cx - r, cy - r, cx + r, cy + r]
    # 粗圆弧（留一个缺口，像扫过的轨迹）
    d.arc(box, start=-52, end=232, fill=col, width=20 + grow * 2 + 8)
    # 弧的末端做成箭头
    a = math.radians(232)
    tipx, tipy = cx + math.cos(a) * r, cy + math.sin(a) * r
    hs = 17 + grow
    px, py = -math.sin(a) * hs, math.cos(a) * hs
    d.polygon([(tipx + math.cos(a) * hs * 0.7, tipy + math.sin(a) * hs * 0.7),
               (tipx + px, tipy + py), (tipx - px, tipy - py)], fill=col)
    # 内侧两道风刃
    for i, (rad, st, en) in enumerate(((r - 20, -30, 210), (r - 34, 10, 170))):
        b2 = [cx - rad, cy - rad, cx + rad, cy + rad]
        d.arc(b2, start=st, end=en, fill=col, width=(7 - i * 2) + grow + 4)


def icon_warlord(d, grow, col):
    """武神降临：竖立的剑（剑身 + 护手 + 柄）。"""
    cx = C
    top = C - 62 - grow
    bot = C + 40 + grow
    # 剑身（带血槽的菱形）
    d.polygon([(cx, top), (cx + 11 + grow, top + 20 + grow),
               (cx + 8 + grow, bot - 6), (cx, bot),
               (cx - 8 - grow, bot - 6), (cx - 11 - grow, top + 20 + grow)], fill=col)
    # 护手
    d.rounded_rectangle([cx - 40 - grow, bot - 12 - grow, cx + 40 + grow, bot + 4 + grow],
                        radius=6, fill=col)
    # 柄
    d.rounded_rectangle([cx - 6 - grow, bot + 2, cx + 6 + grow, bot + 30 + grow],
                        radius=4, fill=col)
    # 柄首
    d.ellipse([cx - 10 - grow, bot + 26 + grow, cx + 10 + grow, bot + 46 + grow], fill=col)


if __name__ == "__main__":
    print("生成技能图标 ->", UI)
    make("icon_q.png", icon_charge)
    make("icon_e.png", icon_whirl)
    make("icon_r.png", icon_warlord)
    print("完成。")
