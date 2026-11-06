"""检查角色素材：尺寸、帧条切分、主色分布。用于判断哪些素材可复用来造新角色。"""
import os
from PIL import Image
import numpy as np

ROOT = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))),
                    "SanguoLuanxing", "Assets", "StreamingAssets", "assets")


def dominant_colors(img, top=6):
    a = np.array(img.convert("RGB")).reshape(-1, 3)
    # 去掉接近纯绿幕的像素
    r, g, b = a[:, 0].astype(int), a[:, 1].astype(int), a[:, 2].astype(int)
    mask = ~((g > 110) & (g - r > 40) & (g - b > 40))
    sel = a[mask]
    if len(sel) == 0:
        return []
    # 粗分桶
    q = (sel // 64).astype(int)
    keys = q[:, 0] * 16 + q[:, 1] * 4 + q[:, 2]
    vals, counts = np.unique(keys, return_counts=True)
    order = np.argsort(-counts)[:top]
    out = []
    for i in order:
        k = vals[i]
        out.append(((int(k / 16) * 64 + 32, int((k / 4) % 4) * 64 + 32, int(k % 4) * 64 + 32),
                    int(counts[i])))
    return out


def report(path, label):
    if not os.path.exists(path):
        print(f"  {label}: MISSING")
        return
    im = Image.open(path)
    w, h = im.size
    print(f"  {label}: {w}x{h}  mode={im.mode}")
    for c, n in dominant_colors(im):
        print(f"      rgb{c} x{n}")


print("== char (立绘/本体) ==")
for f in sorted(os.listdir(os.path.join(ROOT, "char"))):
    if f.endswith(".jpg"):
        report(os.path.join(ROOT, "char", f), f)

print()
print("== anim/boss_* 帧条 ==")
for d in sorted(os.listdir(os.path.join(ROOT, "anim"))):
    p = os.path.join(ROOT, "anim", d)
    if not os.path.isdir(p) or not d.startswith("boss"):
        continue
    for f in sorted(os.listdir(p)):
        if "@" in f:
            report(os.path.join(p, f), f"{d}/{f}")
    print()
