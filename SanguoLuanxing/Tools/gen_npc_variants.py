# -*- coding: utf-8 -*-
"""生成 NPC 立绘变体，消除「一张立绘被多个角色复用」的观感问题。

图像生成 API 在本环境不可用，所以新素材全部用 PIL 对现有立绘做「调色 / 镜像 / 缩体」合成。

关键教训（第一版踩过的坑）：
- **不能靠色相区间区分皮肤和衣服**：褐色的粗布袍和肤色在 HSV 上几乎重合，
  用「h∈8..48 就算皮肤」会把整件袍子当成皮肤保留下来 —— 结果衣服根本没变色。
- 正确做法：从人脸位置取一小块做种子，用「与种子色相近」的规则做连通扩散，
  只有真正连成一片的脸/手才会被保护，袍子则正常参与调色。
- 绿幕像素必须原样保留：运行时 Tex.KeyAll 靠四角背景色取样抠像，背景偏色会出色晕。
- 不能直接刷纯色：会把褶皱阴影压平，人物变成剪影。用 HSV 重映射，保留明度 V。
- 孩童/牧童用「缩小主体」：抠掉绿幕取包围盒，缩到 70% 再贴回绿幕画布，
  人物在世界里就明显比别的 NPC 矮一截。

用法：python Tools/gen_npc_variants.py
"""
import os
import uuid
import numpy as np
from PIL import Image

ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..",
                    "Assets", "StreamingAssets", "assets")


def load(rel):
    return Image.open(os.path.join(ROOT, rel)).convert("RGB")


def save(img, rel, quality=92):
    path = os.path.join(ROOT, rel)
    os.makedirs(os.path.dirname(path), exist_ok=True)
    img.save(path, quality=quality)
    write_meta(path + ".meta")


def write_meta(path):
    """Unity 的 .meta：StreamingAssets 里的文件也会生成，GUID 必须唯一。"""
    if os.path.exists(path):
        return
    guid = uuid.uuid4().hex
    with open(path, "w", encoding="utf-8") as f:
        f.write("fileFormatVersion: 2\n")
        f.write("guid: %s\n" % guid)
        f.write("DefaultImporter:\n")
        f.write("  externalObjects: {}\n")
        f.write("  userData:\n")
        f.write("  assetBundleName:\n")
        f.write("  assetBundleVariant:\n")


def to_hsv(arr):
    a = arr.astype(np.float32) / 255.0
    r, g, b = a[..., 0], a[..., 1], a[..., 2]
    mx = a.max(axis=-1)
    mn = a.min(axis=-1)
    v = mx
    d = mx - mn
    s = np.where(mx > 1e-5, d / np.maximum(mx, 1e-5), 0.0)
    h = np.zeros_like(v)
    nz = d > 1e-5
    idx = (mx == r) & nz
    h[idx] = ((g - b)[idx] / d[idx]) % 6.0
    idx = (mx == g) & nz
    h[idx] = ((b - r)[idx] / d[idx]) + 2.0
    idx = (mx == b) & nz
    h[idx] = ((r - g)[idx] / d[idx]) + 4.0
    h = (h * 60.0) % 360.0
    return h, s, v


def to_rgb(h, s, v):
    c = v * s
    x = c * (1 - np.abs((h / 60.0) % 2 - 1))
    m = v - c
    z = np.zeros_like(v)
    seg = (h // 60).astype(int) % 6
    r = np.select([seg == 0, seg == 1, seg == 2, seg == 3, seg == 4, seg == 5], [c, x, z, z, x, c])
    g = np.select([seg == 0, seg == 1, seg == 2, seg == 3, seg == 4, seg == 5], [x, c, c, x, z, z])
    b = np.select([seg == 0, seg == 1, seg == 2, seg == 3, seg == 4, seg == 5], [z, z, x, c, c, x])
    return np.stack([r + m, g + m, b + m], axis=-1)


def green_mask(a):
    """绿幕（含边缘半透明像素）——这些像素必须原样保留，否则人物边缘会出色晕。"""
    h, s, v = to_hsv(a)
    return (h >= 50) & (h <= 185) & (s > 0.16) & (v > 0.14)


def face_skin_mask(a):
    """自动定位人脸取得肤色，再按「与肤色高度接近」筛出所有皮肤（含手）。

    褐色布衣与肤色在 HSV 上很接近，靠「色相区间」判断会把整件袍子当成皮肤；
    这里改成用脸部颜色作基准、并把明度差卡得很紧（衣服的明度与脸差得远），
    于是袍子自然被排除，手臂这种同样的肤色则会被保留。
    """
    from scipy import ndimage

    a = a.astype(np.float32)
    r, g, b = a[..., 0], a[..., 1], a[..., 2]
    y = 0.299 * r + 0.587 * g + 0.114 * b
    cr = (r - y) * 0.713 + 128
    cb = (b - y) * 0.564 + 128
    cand = (cr > 133) & (cr < 182) & (cb > 77) & (cb < 130) & (y > 90)
    cand &= ~green_mask(a)

    # 人脸在画面上部 24% 以内
    hgt = a.shape[0]
    top = np.zeros_like(cand)
    top[: int(hgt * 0.24), :] = True
    cand_top = cand & top

    lab, n = ndimage.label(cand_top)
    if n == 0:
        return np.zeros(a.shape[:2], bool)
    sizes = ndimage.sum(cand_top, lab, range(1, n + 1))
    face = (lab == (int(np.argmax(sizes)) + 1))
    if face.sum() < 60:
        return np.zeros(a.shape[:2], bool)

    ys, xs = np.where(face)
    fh, fs, fv = to_hsv(a[ys, xs])
    sh, ss, sv = np.median(fh), np.median(fs), np.median(fv)

    h, s, v = to_hsv(a)
    dh = np.abs(h - sh)
    dh = np.minimum(dh, 360 - dh)
    near = (dh < 13) & (np.abs(s - ss) < 0.14) & (np.abs(v - sv) < 0.22)
    near &= ~green_mask(a)

    # 所有与肤色高度接近的块都算皮肤（手、脖子也是同一种肤色）
    skin = near
    # 脸周围再放宽一圈，避免脸颊边缘被当成衣服染色
    skin = ndimage.binary_dilation(skin, iterations=1)
    skin |= face
    return skin


def recolor(img, hue, sat_mul=1.0, sat_add=0.0, val_mul=1.0, val_add=0.0,
            dark_hue=None, dark_val_mul=1.0, gold_hue=(38, 60)):
    """把「非皮肤、非金饰、非绿幕」的部分整体拉到目标色相，保留明度层次。

    dark_hue：给暗部（甲胄等）单独一套色相，让 armor 和袍子有层次差。
    金饰判定要卡得很紧（高明度 + 高饱和 + 窄色相），否则褐色布衣会被当成金饰保留。
    """
    a = np.asarray(img)
    h, s, v = to_hsv(a)

    bg = green_mask(a)
    skin = face_skin_mask(a)
    gold = (h >= gold_hue[0]) & (h <= gold_hue[1]) & (s > 0.45) & (v > 0.45) & (~skin)

    nh, ns, nv = h.copy(), s.copy(), v.copy()
    cloth = (~bg) & (~skin) & (~gold)
    nh[cloth] = hue
    ns[cloth] = np.clip(s[cloth] * sat_mul + sat_add, 0.0, 1.0)
    nv[cloth] = np.clip(v[cloth] * val_mul + val_add, 0.0, 1.0)

    if dark_hue is not None:
        dark = cloth & (v < 0.45)
        nh[dark] = dark_hue
        ns[dark] = np.clip(s[dark] * sat_mul + sat_add, 0.0, 1.0)
        nv[dark] = np.clip(v[dark] * dark_val_mul, 0.0, 1.0)

    out = to_rgb(nh, ns, nv)
    out[bg] = a[bg].astype(np.float32) / 255.0
    return Image.fromarray((np.clip(out, 0, 1) * 255).astype(np.uint8))


def flip(img):
    return img.transpose(Image.FLIP_LEFT_RIGHT)


def shrink_subject(img, factor):
    """把绿幕里的主体缩小并贴回原尺寸画布底部（做出「矮一头」的效果）。"""
    a = np.asarray(img)
    bg = green_mask(a)
    ys, xs = np.where(~bg)
    if len(xs) == 0:
        return img
    x0, x1, y0, y1 = xs.min(), xs.max(), ys.min(), ys.max()
    subject = img.crop((x0, y0, x1 + 1, y1 + 1))
    w, hgt = subject.size
    nh = max(1, int(hgt * factor))
    nw = max(1, int(w * factor))
    subject = subject.resize((nw, nh), Image.LANCZOS)
    bg_color = tuple(int(c) for c in a[2, 2])
    canvas = Image.new("RGB", img.size, bg_color)
    canvas.paste(subject, ((img.size[0] - nw) // 2, img.size[1] - nh))
    return canvas


def main():
    def rc(rel, *args, **kw):
        return recolor(load(rel), *args, **kw)

    # ---- 阿福系（桃林乡野：扫花汉子）----
    save(rc("npc/afu.jpg", 78, sat_mul=0.95, sat_add=0.05, val_mul=1.0),
         "npc/muren.jpg")                       # 牧人：蓑衣土黄
    save(flip(rc("npc/afu.jpg", 196, sat_mul=0.55, sat_add=0.04, val_mul=0.95)),
         "npc/fisher.jpg")                     # 渔翁：青灰蓑衣 + 镜像
    save(shrink_subject(rc("npc/afu.jpg", 208, sat_mul=0.62, sat_add=0.05, val_mul=0.96), 0.70),
         "npc/boy.jpg")                        # 孩童：矮一截的少年
    save(shrink_subject(flip(rc("npc/afu.jpg", 26, sat_mul=0.80, sat_add=0.05, val_mul=0.92)), 0.66),
         "npc/herd.jpg")                       # 牧童：更矮 + 镜像

    # ---- 斥候系 ----
    save(rc("npc/scout.jpg", 205, sat_mul=0.85, sat_add=0.06, val_mul=0.94),
         "npc/zhaocang.jpg")                   # 赵仓：冷青蓝
    save(rc("npc/scout.jpg", 42, sat_mul=0.60, sat_add=0.04, val_mul=0.88),
         "npc/scribe.jpg")                     # 洛阳书吏：墨褐

    # ---- 军士系 ----
    save(rc("npc/guard.jpg", 34, sat_mul=0.72, sat_add=0.04, val_mul=0.86,
            dark_hue=28, dark_val_mul=1.05),
         "npc/elder.jpg")                      # 老兵（官渡）：旧甲赭黄
    save(rc("npc/guard.jpg", 168, sat_mul=0.66, sat_add=0.05, val_mul=0.95,
            dark_hue=172, dark_val_mul=1.06),
         "npc/postman.jpg")                    # 驿卒：青绿甲
    save(rc("npc/guard.jpg", 228, sat_mul=0.52, sat_add=0.04, val_mul=0.90,
            dark_hue=232, dark_val_mul=1.08),
         "npc/clerk.jpg")                      # 关吏：墨蓝正襟

    # ---- 酒贩系 ----
    save(rc("npc/wine.jpg", 224, sat_mul=0.72, sat_add=0.05, val_mul=0.94),
         "npc/merchant.jpg")                   # 店主：靛蓝袍
    save(rc("npc/wine.jpg", 30, sat_mul=0.70, sat_add=0.04, val_mul=0.84),
         "npc/veteran.jpg")                    # 老卒（虎牢）：苍褐旧衣

    # ---- 苏婉系（女子）----
    save(rc("npc/suwan.jpg", 38, sat_mul=0.40, sat_add=0.03, val_mul=0.97),
         "npc/weaver.jpg")                     # 织工（桃林）：素米白
    save(flip(rc("npc/suwan.jpg", 338, sat_mul=0.38, sat_add=0.05, val_mul=0.96)),
         "npc/maiden.jpg")                     # 织女（江夏）：藕粉 + 镜像
    save(rc("npc/suwan.jpg", 172, sat_mul=0.45, sat_add=0.04, val_mul=0.92),
         "npc/woman.jpg")                      # 妇人（洛阳）：灰青

    print("npc variants done")


if __name__ == "__main__":
    main()
