"""对比各 Boss 的帧条：判断它们是同一套素材换色，还是各自不同的画。"""
import os
import hashlib
from PIL import Image
import numpy as np

ROOT = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))),
                    "SanguoLuanxing", "Assets", "StreamingAssets", "assets", "anim")


def sig(path):
    im = Image.open(path).convert("RGB")
    a = np.array(im).astype(float)
    h, w, _ = a.shape
    # 缩到统一尺寸做粗比较
    small = np.array(im.resize((64, 64))).astype(float)
    # 去掉绿幕主导的像素
    r, g, b = small[:, :, 0], small[:, :, 1], small[:, :, 2]
    mask = ~((g > 110) & (g - r > 40) & (g - b > 40))
    if mask.sum() < 50:
        return None
    # 用形状（亮度）与颜色分布做签名
    lum = small.mean(axis=2)
    return {
        "size": (w, h),
        "shape": lum[mask],
        "col": small[mask],
        "md5": hashlib.md5(im.tobytes()).hexdigest()[:10],
    }


groups = {}
for d in sorted(os.listdir(ROOT)):
    p = os.path.join(ROOT, d)
    if not os.path.isdir(p):
        continue
    f = os.path.join(p, "idle@2.jpg")
    if not os.path.exists(f):
        continue
    s = sig(f)
    if s:
        groups[d] = s

names = list(groups)
print("角色 idle@2 尺寸 / md5:")
for n in names:
    g = groups[n]
    print(f"  {n:14s} {g['size'][0]}x{g['size'][1]}  md5={g['md5']}")

print()
print("两两差异（shape 亮度 RMSE + 平均色差）:")
for i in range(len(names)):
    for j in range(i + 1, len(names)):
        a, b = groups[names[i]], groups[names[j]]
        if a["shape"].shape != b["shape"].shape:
            continue
        rmse = float(np.sqrt(((a["shape"] - b["shape"]) ** 2).mean()))
        cd = float(np.abs(a["col"].mean(axis=0) - b["col"].mean(axis=0)).mean())
        print(f"  {names[i]:12s} vs {names[j]:12s}  lumRMSE={rmse:6.2f}  colDiff={cd:6.2f}")
