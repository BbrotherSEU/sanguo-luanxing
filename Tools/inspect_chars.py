"""盘点立绘素材：尺寸、是否绿幕、主体占比。
用途：判断第五章新 Boss 能复用哪些既有素材。"""
import os
from PIL import Image
import numpy as np

ROOT = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))),
                    "SanguoLuanxing", "Assets", "StreamingAssets", "assets")


def analyze(path):
    im = Image.open(path).convert("RGB")
    a = np.array(im)
    h, w, _ = a.shape
    r, g, b = a[:, :, 0].astype(int), a[:, :, 1].astype(int), a[:, :, 2].astype(int)
    green = (g > 100) & (g - r > 30) & (g - b > 30)
    fg = ~green
    ys, xs = np.where(fg)
    if len(xs) == 0:
        return f"{w}x{h}  全绿幕（无主体）"
    bbox = (xs.min(), ys.min(), xs.max(), ys.max())
    bw, bh = bbox[2] - bbox[0], bbox[3] - bbox[1]
    fill = fg.mean()
    return (f"{w}x{h}  绿幕={green.mean()*100:.0f}%  主体占框={fill*100:.0f}%  "
            f"bbox={bbox} 主体尺寸={bw}x{bh}")


for sub in ("char", "npc", "ui"):
    d = os.path.join(ROOT, sub)
    if not os.path.isdir(d):
        continue
    print(f"== {sub} ==")
    for f in sorted(os.listdir(d)):
        if f.lower().endswith((".jpg", ".jpeg", ".png")) and not f.endswith(".bak"):
            print(f"  {f:24s} {analyze(os.path.join(d, f))}")
    print()
