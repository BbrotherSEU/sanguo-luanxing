"""修好玩家行走帧条：帧内人物身体轴对齐到格中心，并把朝向统一成朝右。

问题（实测）：
- anim/player/walk@4.png 四帧的人物身体轴在格内摆动 143px（其余素材只有 ~14px），
  而 Unity 端 CutoutFrames 按「原图宽等分」切格、并把格中心当角色原点，
  于是每帧人物在世界空间左右横跳 —— 这就是「滑行」。
- 这份素材原生朝左，而同角色其余立绘（char/player.jpg、anim/player/idle@2、
  walk@6、attack1@4）全部朝右。Unity 端对所有状态用同一套 flipX 逻辑，
  于是「待机→走路→待机」切换时角色会整个翻面 —— 看着就是「朝向一直变」。

做法（沿用 Tools/既有流程与 Tex.cs 注释里的约定：
      「Python 端已把每帧人物按身体轴对齐到格中心，故等分即正确」）：
1. 逐帧切出，按躯干（35%~60% 身高）不透明像素的质量中心算出「身体轴」x。
2. 每帧横向平移，使身体轴落到格中心；空出来的地方补原背景绿。
3. 竖直方向不动（各帧脚底 y 一致，保持「脚底同一水平线」）。
4. 朝向：整条帧条左右镜像，使四帧与角色其余立绘一致，都朝右。
   镜像不改变帧序，步态循环（张开→收拢→张开→收拢）完整保留。

产出仍是等宽帧条，Unity 端 CutoutFrames 不需要改动。
"""
import os
import numpy as np
from PIL import Image

ROOT = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))),
                    "SanguoLuanxing", "Assets", "StreamingAssets", "assets")
SRC = os.path.join(ROOT, "anim", "player", "walk@4.png")
# 原始素材备份放在 Unity 工程之外（Assets 里放 .bak 会被当成资源导入）
BAK = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))),
                   "backup_assets", "player_walk4_orig.png")
FRAMES = 4


def green_mask(a):
    r, g, b = a[:, :, 0].astype(int), a[:, :, 1].astype(int), a[:, :, 2].astype(int)
    return ~((g > 100) & (g - r > 30) & (g - b > 30))


def body_axis(cell):
    """躯干（35%~60% 身高）的不透明像素质量中心 = 身体轴。"""
    m = green_mask(cell)
    ys, xs = np.nonzero(m)
    ymin, ymax = ys.min(), ys.max()
    h = ymax - ymin
    band = m[ymin + int(h * 0.35): ymin + int(h * 0.60)]
    bys, bxs = np.nonzero(band)
    w = band[bys, bxs].astype(float)
    return float((bxs * w).sum() / w.sum()), ymin, ymax


def main():
    if not os.path.exists(BAK):
        os.replace(SRC, BAK)
    im = Image.open(BAK).convert("RGB")
    W, H = im.size
    fw = W // FRAMES
    cells = [np.array(im.crop((f * fw, 0, (f + 1) * fw, H))) for f in range(FRAMES)]

    bg = cells[0][2, 2].tolist()          # 原背景绿（补空位用）

    out = []
    for f, c in enumerate(cells):
        axis, ymin, ymax = body_axis(c)
        dx = int(round(fw / 2 - axis))
        blank = np.zeros_like(c)
        blank[:, :] = bg
        if dx >= 0:
            blank[:, dx:fw] = c[:, :fw - dx]
        else:
            blank[:, :fw + dx] = c[:, -dx:]
        # 整帧左右镜像：与其余立绘统一朝右。镜像后身体轴对称，仍落在格中心。
        blank = blank[:, ::-1]
        out.append(blank)
        print(f"f{f}: 身体轴 {axis:7.1f} → 平移 {dx:+d}px 后 = {axis + dx:.1f}"
              f"（格中心 {fw / 2}）→ 镜像成朝右  脚底 y={ymax}")

    sheet = np.concatenate(out, axis=1)
    Image.fromarray(sheet).save(SRC)
    print("已写出", SRC, sheet.shape)


if __name__ == "__main__":
    main()
