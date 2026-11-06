"""派生第五章 Boss「袁绍」素材。

图像生成接口不可用（鉴权失败），所以从既有 Boss 素材派生：
- 帧条：anim/boss_zhanghe/* → 把紫甲改成青玉 + 保留金饰（袁绍银袍金饰）
- 立绘：char/boss_zhanghe.jpg → 同法换色

绿幕像素必须原样保留（Unity 端靠四角均值抠像），皮肤橙色区间也保留。
"""
import os
import shutil
import numpy as np
from PIL import Image

ROOT = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))),
                    "SanguoLuanxing", "Assets", "StreamingAssets", "assets")
SRC_FRAME = os.path.join(ROOT, "anim", "boss_zhanghe")
DST_FRAME = os.path.join(ROOT, "anim", "boss_yuanshao")
SRC_PORTRAIT = os.path.join(ROOT, "char", "boss_zhanghe.jpg")
DST_PORTRAIT = os.path.join(ROOT, "char", "boss_yuanshao.jpg")

# 紫甲(0.60~0.85) → 青玉(0.44)；紫袍偏品红(0.85~1.0 或 0.95~1.0) → 墨青
JADE = 0.455
DARK_TEAL = 0.49


def hsv_shift(img):
    base = img.convert("RGB")
    rgb = np.array(base).astype(np.float32) / 255.0
    hsv = np.array(base.convert("HSV")).astype(np.float32) / 255.0
    h, s, v = hsv[:, :, 0], hsv[:, :, 1], hsv[:, :, 2]

    r, g, b = rgb[:, :, 0], rgb[:, :, 1], rgb[:, :, 2]
    green = (g > 0.37) & (g - r > 0.12) & (g - b > 0.12)
    # 皮肤：橙/红橙（h 0.02~0.13）且饱和度中等 —— 保留
    skin = (~green) & (h >= 0.0) & (h <= 0.135) & (s > 0.12) & (s < 0.62)
    # 金饰：暖黄（h 0.085~0.17）高饱和 —— 保留
    gold = (~green) & (h >= 0.085) & (h <= 0.17) & (s > 0.35)

    purple = (~green) & (~skin) & (~gold) & (s > 0.16) & (((h >= 0.58) & (h <= 0.80)) | ((h >= 0.90) & (h <= 1.0)))
    magenta = (~green) & (~skin) & (~gold) & (s > 0.16) & (h > 0.80) & (h < 0.90)

    new_h = h.copy()
    new_s = s.copy()
    new_v = v.copy()

    # 紫甲 → 青玉（提亮一点，免得在夜里发黑）
    new_h = np.where(purple, JADE, new_h)
    new_s = np.where(purple, np.clip(s * 0.78, 0, 1), new_s)
    new_v = np.where(purple, np.clip(v * 1.06, 0, 1), new_v)
    # 品红袍 → 墨青
    new_h = np.where(magenta, DARK_TEAL, new_h)
    new_s = np.where(magenta, np.clip(s * 0.85, 0, 1), new_s)
    new_v = np.where(magenta, np.clip(v * 0.94, 0, 1), new_v)

    i = np.floor(new_h * 6.0).astype(int) % 6
    f = new_h * 6.0 - np.floor(new_h * 6.0)
    p = new_v * (1 - new_s)
    q = new_v * (1 - new_s * f)
    t = new_v * (1 - new_s * (1 - f))
    vv = new_v
    out = np.zeros_like(rgb)
    for mask, (cr, cg, cb) in [
        (i == 0, (vv, t, p)), (i == 1, (q, vv, p)), (i == 2, (p, vv, t)),
        (i == 3, (p, q, vv)), (i == 4, (t, p, vv)), (i == 5, (vv, p, q)),
    ]:
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
        hsv_shift(im).save(os.path.join(DST_FRAME, f), quality=92)
        print("frame:", f, im.size)

    im = Image.open(SRC_PORTRAIT).convert("RGB")
    hsv_shift(im).save(DST_PORTRAIT, quality=93)
    print("portrait:", os.path.basename(DST_PORTRAIT), im.size)


if __name__ == "__main__":
    main()
