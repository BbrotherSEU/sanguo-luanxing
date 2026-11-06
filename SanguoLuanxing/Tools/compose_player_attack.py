"""把生成好的攻击单帧合成等宽帧条，装进 StreamingAssets。

只挑可用的帧：链式生成在姿势跨度大的地方会漂移（人物转回正面 / 回到站姿），
漂移帧直接用会更糟，所以宁可用少几帧。
"""
import os
import shutil
import sys

from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
TMP = os.path.join(HERE, "..", "..", "..", ".tmp_gen", "player_anim")
DST = os.path.join(HERE, "..", "Assets", "StreamingAssets", "assets", "anim", "player")

sys.path.insert(0, HERE)
import align_strip  # noqa: E402

# 输出名 → 参与合成的源帧（顺序即播帧顺序）
#
# 只保留确实可用的一组。attack2 / attack3 的整组生成在姿势跨度大的帧上漂移了
# （人物转回正面、服装变样），所以不落盘，运行时用 attack1 兜底。
PLAN = [
    ("attack1@4", [("attack1", 0), ("attack1", 1), ("attack1", 2), ("attack1", 3)]),
    # 重击：蹲身蓄力 → 举刀过顶 → 下劈 → 收势
    ("heavy@4", [("heavy", 0), ("attack3", 0), ("attack1", 1), ("attack1", 3)]),
]


def main():
    dst = os.path.abspath(DST)
    os.makedirs(dst, exist_ok=True)
    tmpdir = os.path.join(os.path.abspath(TMP), "_compose")
    os.makedirs(tmpdir, exist_ok=True)
    for out_name, srcs in PLAN:
        picked = []
        for group, idx in srcs:
            p = os.path.join(os.path.abspath(TMP), group, "f%d.png" % idx)
            if not os.path.exists(p):
                print("!! missing", p)
                return 1
            # 复制到临时目录，避免 align_strip 读到不同尺寸无关
            q = os.path.join(tmpdir, "%s_%s_%d.png" % (out_name, group, idx))
            shutil.copyfile(p, q)
            picked.append(q)
        out = os.path.join(dst, out_name + ".png")
        align_strip.build(picked, out)
        # 预览
        im = Image.open(out)
        im.resize((im.width // 3, im.height // 3), Image.Resampling.LANCZOS).save(
            os.path.join(tmpdir, out_name + "_prev.png"))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
