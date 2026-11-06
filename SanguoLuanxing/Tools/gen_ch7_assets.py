# -*- coding: utf-8 -*-
"""生成第七章「定天下」的美术素材。

图像生成 API 在本环境不可用，所以新素材全部用 PIL 对现有素材做「调色合成」。

调色方式：转 HSV 后按色相分段重映射，**保留明度 V**，这样布料的褶皱阴影不会糊成
一块死色（第一版直接刷纯色导致人物变成剪影，已废弃）。

1. 终章 Boss「西凉锦马超」：以 boss_huaxiong 为底 —— 红披风 → 银白袍，
   黑甲 → 冷钢蓝，金饰保留；动画帧同一套规则，立绘与战斗形象一致。
2. 终章地图缩略图 bg/map7_weihe.jpg：bg/map2_sishui.jpg 冷色分级。
3. 新 NPC 立绘：西凉军士 / 书吏 / 船家女 / 老船工（由现有立绘改色）。

绿幕背景必须原样保留：运行时抠像靠四角背景色取样，背景偏色会出现色晕。
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


def recolor(img, mode):
    """按色相分段重映射，保留明度与部分饱和度。"""
    a = np.asarray(img)
    h, s, v = to_hsv(a)

    bg = (h >= 60) & (h <= 175) & (s > 0.35) & (v > 0.25)   # 绿幕
    skin = (h >= 10) & (h <= 46) & (s > 0.12) & (s < 0.68) & (v > 0.28)
    gold = (h >= 30) & (h <= 58) & (s > 0.32) & (v > 0.28)
    # 披风：红棕系。注意动画帧里的披风是暗红（S≈0.15、V≈0.17），
    # 只按「高饱和红」挑会漏掉它 —— 所以用「红棕色相 + 偏暗」来认。
    cape = (((h < 34) | (h > 332)) & (~skin) & (~gold)) | \
           (((h < 16) | (h > 340)) & (s > 0.38) & (v > 0.25))

    nh, ns, nv = h.copy(), s.copy(), v.copy()

    if mode == "machao":
        # 披风 → 银白：提亮暗部，不然暗红压成深蓝就看不出是白袍了
        nh[cape] = 212.0
        ns[cape] = np.clip(s[cape] * 0.20, 0.02, 0.10)
        nv[cape] = np.clip(0.52 + v[cape] * 0.46, 0.0, 1.0)
        # 甲胄 → 冷钢蓝
        armor = (~bg) & (~skin) & (~gold) & (~cape) & (v < 0.42) & (s < 0.62)
        nh[armor] = 216.0
        ns[armor] = np.clip(s[armor] * 1.3 + 0.16, 0.0, 0.55)
        nv[armor] = np.clip(v[armor] * 0.92 + 0.05, 0.0, 1.0)
        # 金属高光等其余部分整体偏冷
        rest = (~bg) & (~skin) & (~gold) & (~cape) & (~armor)
        nh[rest] = (nh[rest] + 20.0) % 360.0
        ns[rest] = np.clip(ns[rest] * 0.9, 0.0, 1.0)
    elif mode == "steel":
        # 西凉军士：披风冷白，甲胄更蓝
        nh[cape] = 208.0
        ns[cape] = np.clip(s[cape] * 0.30, 0.06, 0.24)
        nv[cape] = np.clip(0.28 + v[cape] * 0.55, 0.0, 1.0)
        armor = (~bg) & (~skin) & (~gold) & (~cape) & (v < 0.45) & (s < 0.62)
        nh[armor] = 214.0
        ns[armor] = np.clip(s[armor] * 1.2 + 0.14, 0.0, 0.55)
        rest = (~bg) & (~skin) & (~gold) & (~cape) & (~armor)
        nh[rest] = (nh[rest] + 16.0) % 360.0
    elif mode == "bluegrey":
        # 书吏：整体转青灰、降饱和
        cloth = (~bg) & (~skin)
        nh[cloth] = (nh[cloth] * 0.35 + 195.0) % 360.0
        ns[cloth] = np.clip(ns[cloth] * 0.55, 0.0, 0.55)
    elif mode == "pale":
        # 船家女：素白衣，保留一点点青
        cloth = (~bg) & (~skin)
        nh[cloth] = (nh[cloth] * 0.30 + 205.0) % 360.0
        ns[cloth] = np.clip(ns[cloth] * 0.35, 0.0, 0.30)
    elif mode == "ash":
        # 老船工：苍青土色
        cloth = (~bg) & (~skin)
        nh[cloth] = (nh[cloth] * 0.45 + 178.0) % 360.0
        ns[cloth] = np.clip(ns[cloth] * 0.60, 0.0, 0.60)
    elif mode == "coldbg":
        # 地图缩略图：黄昏 → 雪夜关外
        out = a.astype(np.float32) / 255.0
        out[..., 0] = out[..., 0] * 0.68 + 0.03
        out[..., 1] = out[..., 1] * 0.78 + 0.06
        out[..., 2] = out[..., 2] * 1.00 + 0.12
        lum = out.mean(axis=-1)
        out[..., 0] = np.clip(out[..., 0] * 0.82 + lum * 0.18, 0, 1)
        out[..., 1] = np.clip(out[..., 1] * 0.86 + lum * 0.14, 0, 1)
        out[..., 2] = np.clip(out[..., 2] * 0.90 + lum * 0.10, 0, 1)
        return Image.fromarray((out * 255).astype(np.uint8))

    out = to_rgb(nh, ns, nv)
    out[bg] = a[bg].astype(np.float32) / 255.0
    return Image.fromarray((np.clip(out, 0, 1) * 255).astype(np.uint8))


def main():
    # 1) 终章 Boss：马超（银甲白袍）
    save(recolor(load("char/boss_huaxiong.jpg"), "machao"), "char/boss_machao.jpg")

    src_dir = os.path.join(ROOT, "anim", "boss_huaxiong")
    dst_dir = os.path.join(ROOT, "anim", "boss_machao")
    os.makedirs(dst_dir, exist_ok=True)
    for fn in sorted(os.listdir(src_dir)):
        if not fn.endswith(".jpg"):
            continue
        img = Image.open(os.path.join(src_dir, fn)).convert("RGB")
        save(recolor(img, "machao"), "anim/boss_machao/" + fn)
    write_meta(dst_dir + ".meta")

    # 2) 终章地图缩略图
    save(recolor(load("bg/map2_sishui.jpg"), "coldbg"), "bg/map7_weihe.jpg", quality=90)

    # 3) 新 NPC 立绘
    save(recolor(load("npc/guard.jpg"), "steel"), "npc/liangjiang.jpg")
    save(recolor(load("npc/scout.jpg"), "bluegrey"), "npc/shusheng.jpg")
    save(recolor(load("npc/suwan.jpg"), "pale"), "npc/chuanfu.jpg")
    save(recolor(load("npc/wine.jpg"), "ash"), "npc/laochuanfu.jpg")

    print("ch7 assets done")


if __name__ == "__main__":
    main()
