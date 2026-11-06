"""判定各 Boss 帧条是否为同一套剪影换色。
方法：Sobel 边缘图归一化互相关。剪影相同→高相关（说明项目用"换色"区分角色）。"""
import os
import numpy as np
from PIL import Image

ROOT = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))),
                    "SanguoLuanxing", "Assets", "StreamingAssets", "assets", "anim")


def edges(path):
    im = Image.open(path).convert("L").resize((96, 96))
    a = np.array(im).astype(float)
    gx = np.zeros_like(a)
    gy = np.zeros_like(a)
    gx[:, 1:-1] = a[:, 2:] - a[:, :-2]
    gy[1:-1, :] = a[2:, :] - a[:-2, :]
    e = np.sqrt(gx * gx + gy * gy)
    e -= e.mean()
    n = np.linalg.norm(e)
    return e / n if n > 1e-6 else e


names = []
data = {}
for d in sorted(os.listdir(ROOT)):
    p = os.path.join(ROOT, d)
    f = os.path.join(p, "idle@2.jpg")
    if os.path.isdir(p) and os.path.exists(f):
        names.append(d)
        data[d] = edges(f)

print("Boss 帧条剪影互相关（1.0=完全同剪影）")
for i in range(len(names)):
    row = []
    for j in range(len(names)):
        row.append(float((data[names[i]] * data[names[j]]).sum()))
    print(f"  {names[i]:14s} " + " ".join(f"{v:5.2f}" for v in row))
