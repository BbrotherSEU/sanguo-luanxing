"""派生第五、六章的世界地图缩略图。

图像生成接口不可用（只返回 default.jpeg 占位图），所以从既有地图素材派生：
- map5_luoyang.jpg  ← map4_hulao.jpg（关城构图）做调色 + 镜像 + 局部替换
- map6_guandu.jpg   ← map3_changban.jpg（野外草原构图）做调色 + 裁切

派生思路：
- 洛阳：紫色调（对应 Dusk 主题）+ 镜像，避免与虎牢关雷雨蓝调撞色
- 官渡：金黄旱地调 + 纵向裁切压扁，突出「平野无边、粮道千里」

用法：python gen_map_thumbs.py
"""
import os
from PIL import Image, ImageEnhance
import numpy as np

ROOT = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))),
                    "SanguoLuanxing", "Assets", "StreamingAssets", "assets", "bg")


def grade(img, shadow, highlight, sat=1.0, contrast=1.0):
    """按阴影/高光两端做色相偏移（HSV 空间的色相环插值）。"""
    base = img.convert("RGB")
    hsv = np.array(base.convert("HSV")).astype(np.float32) / 255.0
    # PIL 的 HSV 是 RGB 三通道布局：[H, S, V] 各占一个通道
    h = hsv[:, :, 0]
    s = hsv[:, :, 1]
    v = hsv[:, :, 2]

    lum = v
    # 暗部往 shadow 靠，亮部往 highlight 靠
    t = np.clip(lum * 1.15, 0.0, 1.0)
    h_new = (shadow[0] + (highlight[0] - shadow[0]) * t) / 360.0
    # 保留一部分原色相，避免整图死板
    h = (h * 0.35 + h_new * 0.65) % 1.0
    s = np.clip(s * sat, 0.0, 1.0)
    v = np.clip(v * contrast, 0.0, 1.0)

    out = np.dstack([h * 255.0, s * 255.0, v * 255.0]).astype(np.uint8)
    return Image.fromarray(out, "HSV").convert("RGB")


def vignette(img, strength=0.32):
    w, h = img.size
    yy, xx = np.mgrid[0:h, 0:w]
    cx, cy = w / 2.0, h / 2.0
    d = np.sqrt(((xx - cx) / cx) ** 2 + ((yy - cy) / cy) ** 2) / 1.414
    mask = np.clip(1.0 - strength * (d ** 2.2), 0.0, 1.0)
    rgb = np.array(img).astype(np.float32)
    rgb *= mask[..., None]
    return Image.fromarray(np.clip(rgb, 0, 255).astype(np.uint8), "RGB")


def make_luoyang():
    src = Image.open(os.path.join(ROOT, "map4_hulao.jpg")).convert("RGB")
    img = src.transpose(Image.FLIP_LEFT_RIGHT)
    # 紫调：阴影偏靛，高光偏品红 —— 对应洛阳的宫墙与残火
    img = grade(img, (268, 0.50), (318, 0.45), sat=1.06, contrast=1.06)
    img = vignette(img, 0.30)
    img = ImageEnhance.Sharpness(img).enhance(1.15)
    out = os.path.join(ROOT, "map5_luoyang.jpg")
    img.save(out, quality=92)
    print("map5_luoyang.jpg", os.path.getsize(out))


def make_guandu():
    src = Image.open(os.path.join(ROOT, "map3_changban.jpg")).convert("RGB")
    w, h = src.size
    # 纵向压扁一点，横向裁掉两侧，把视线收到中部的桥与旗
    img = src.crop((int(w * 0.08), int(h * 0.10), int(w * 0.92), int(h * 0.92)))
    img = img.resize((1024, 576), Image.LANCZOS)
    # 旱地金黄调：阴影偏暖褐，高光偏麦黄
    img = grade(img, (28, 0.55), (46, 0.40), sat=1.12, contrast=1.10)
    img = vignette(img, 0.26)
    img = ImageEnhance.Sharpness(img).enhance(1.2)
    out = os.path.join(ROOT, "map6_guandu.jpg")
    img.save(out, quality=92)
    print("map6_guandu.jpg", os.path.getsize(out))


if __name__ == "__main__":
    make_luoyang()
    make_guandu()
