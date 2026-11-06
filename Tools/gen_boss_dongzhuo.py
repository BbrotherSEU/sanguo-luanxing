"""派生第五章 Boss「董卓」素材。

图像生成接口不可用（鉴权失败），所以从既有 Boss 素材派生：
- 帧条：anim/boss_huaxiong/* → 选区换色（血红 → 深紫），绿幕原样保留
- 立绘：char/boss_huaxiong.jpg → 同法换色，输出 char/boss_dongzhuo.jpg

只动「饱和的红」像素（甲胄红），橙色皮肤与金色饰件不动，避免人脸发青。
"""
import os
import shutil
from PIL import Image
import numpy as np

ROOT = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))),
                    "SanguoLuanxing", "Assets", "StreamingAssets", "assets")
SRC_FRAME = os.path.join(ROOT, "anim", "boss_huaxiong")
DST_FRAME = os.path.join(ROOT, "anim", "boss_dongzhuo")
SRC_PORTRAIT = os.path.join(ROOT, "char", "boss_huaxiong.jpg")
DST_PORTRAIT = os.path.join(ROOT, "char", "boss_dongzhuo.jpg")

# 目标色：深紫（帝王紫）
TARGET_HUE = 0.76          # HSV 0..1
TARGET_SAT = 0.62


def is_green(a):
    r, g, b = a[:, :, 0], a[:, :, 1], a[:, :, 2]
    return (g > 95) & (g.astype(int) - r.astype(int) > 30) & (g.astype(int) - b.astype(int) > 30)


def recolor(img):
    """把饱和的红色（甲胄/披风）转成深紫；皮肤(橙)与金饰不动。"""
    base = img.convert("RGB")
    rgb = np.array(base).astype(np.float32) / 255.0
    hsv = np.array(base.convert("HSV")).astype(np.float32) / 255.0
    h, s, v = hsv[:, :, 0], hsv[:, :, 1], hsv[:, :, 2]

    green = is_green(np.array(base))
    # 红色区：色相在 0 附近（红 350°~10°）且饱和度够高；橙色皮肤色相 15°~40° 排除在外
    red = (~green) & (s > 0.38) & ((h < 0.045) | (h > 0.955))

    # 只在红色像素上做色相替换：红(0) → 紫(0.76)
    new_h = np.where(red, TARGET_HUE, h)
    new_s = np.where(red, np.clip(s * (TARGET_SAT / 0.85), 0, 1), s)
    new_v = np.where(red, v * 0.98, v)

    # HSV → RGB
    i = np.floor(new_h * 6.0).astype(int) % 6
    f = new_h * 6.0 - np.floor(new_h * 6.0)
    p = new_v * (1 - new_s)
    q = new_v * (1 - new_s * f)
    t = new_v * (1 - new_s * (1 - f))
    vv, pp, tt = new_v, p, q
    out = np.zeros_like(rgb)
    conds = [
        (i == 0, (vv, tt, pp)), (i == 1, (q, vv, pp)), (i == 2, (pp, vv, tt)),
        (i == 3, (pp, q, vv)), (i == 4, (tt, pp, vv)), (i == 5, (vv, pp, q)),
    ]
    for mask, (cr, cg, cb) in conds:
        out[mask] = np.stack([cr, cg, cb], axis=-1)[mask]
    out[green] = rgb[green]
    return Image.fromarray((np.clip(out, 0, 1) * 255).astype(np.uint8))


def main():
    if os.path.isdir(DST_FRAME):
        shutil.rmtree(DST_FRAME)
    os.makedirs(DST_FRAME, exist_ok=True)

    for f in sorted(os.listdir(SRC_FRAME)):
        if not f.endswith(".jpg"):
            continue
        im = Image.open(os.path.join(SRC_FRAME, f)).convert("RGB")
        recolor(im).save(os.path.join(DST_FRAME, f), quality=92)
        print("frame:", f, im.size)

    im = Image.open(SRC_PORTRAIT).convert("RGB")
    recolor(im).save(DST_PORTRAIT, quality=93)
    print("portrait:", os.path.basename(SRC_PORTRAIT), im.size, "->",
          os.path.basename(DST_PORTRAIT))


if __name__ == "__main__":
    main()
