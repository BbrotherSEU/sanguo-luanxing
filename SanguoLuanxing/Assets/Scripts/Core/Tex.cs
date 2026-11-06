using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// 贴图管线：StreamingAssets 读图 → 绿幕抠像(含描边) → 裁切 → Sprite。
/// 与网页版 assets.js 的 chromaKey / loadMirroredTexture 一一对应。
/// </summary>
public static class Tex
{
    public static Texture2D White, Shadow, Glow, Ring, Slash, Dot, GroundPatch, Spark, Star;
    // 触屏技能图标（id → 贴图）
    public static Texture2D IconLight, IconHeavy, IconDodge, IconQ, IconE, IconR;

    static readonly Dictionary<string, Texture2D> cutCache = new Dictionary<string, Texture2D>();
    static readonly Dictionary<string, Texture2D[]> frameCache = new Dictionary<string, Texture2D[]>();
    static readonly Dictionary<string, float> frameAspCache = new Dictionary<string, float>();
    static readonly Dictionary<string, float> aspectCache = new Dictionary<string, float>();
    static readonly Dictionary<string, Sprite> spriteCache = new Dictionary<string, Sprite>();
    static readonly Dictionary<string, Texture2D> portraitCache = new Dictionary<string, Texture2D>();
    static readonly Dictionary<string, Texture2D> bandCache = new Dictionary<string, Texture2D>();

    public static string Root => Path.Combine(Application.streamingAssetsPath, "assets");

    public static void Init()
    {
        if (White != null) return;
        White = Solid(4, 4, Color.white);
        Dot = Radial(16, Color.white, 2.2f);
        Shadow = Radial(128, new Color(0f, 0f, 0f, 0.85f), 1.6f);
        Glow = Radial(128, Color.white, 2.4f);
        Ring = MakeRing(128, 0.80f, 0.13f);
        Slash = MakeSlash(256);
        Spark = MakeSpark(64);
        Star = MakeStar(64);
        GroundPatch = MakePatch(256);
        MakeSkillIcons();
    }

    /// <summary>地表贴片：边缘不规则的软斑，用来打散平铺地面的均匀感（对应网页版 makeGroundPatchTexture）</summary>
    static Texture2D MakePatch(int size)
    {
        var t = new Texture2D(size, size, TextureFormat.RGBA32, false);
        var px = new Color32[size * size];
        float half = size * 0.5f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f - half) / half;
                float dy = (y + 0.5f - half) / half;
                float ang = Mathf.Atan2(dy, dx);
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                float edge = 0.62f + 0.13f * Mathf.Sin(ang * 3f + 1.3f) + 0.07f * Mathf.Sin(ang * 5f - 0.7f);
                float a = Mathf.Clamp01((edge - r) / 0.30f);
                a = a * a * (3f - 2f * a);
                px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
        }
        t.SetPixels32(px);
        t.wrapMode = TextureWrapMode.Clamp;
        t.filterMode = FilterMode.Bilinear;
        t.Apply(false, false);
        return t;
    }

    // ---------------- 触屏技能图标（程序化水墨风） ----------------

    /// <summary>把图标画到一张 RGBA 图上；brush(x,y,w,h) 决定笔触形状</summary>
    static Texture2D Icon(int size, System.Func<float, float, float, float, bool> brush)
    {
        var t = new Texture2D(size, size, TextureFormat.RGBA32, true);
        var px = new Color32[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                px[y * size + x] = brush(x, y, size, size) ? new Color32(255, 255, 255, 255) : new Color32(0, 0, 0, 0);
        t.SetPixels32(px);
        t.wrapMode = TextureWrapMode.Clamp;
        t.filterMode = FilterMode.Trilinear;
        t.Apply(true, false);
        return t;
    }

    /// <summary>点到线段的距离（用于画笔触）</summary>
    static float SegDist(float px, float py, float ax, float ay, float bx, float by)
    {
        float vx = bx - ax, vy = by - ay;
        float len2 = vx * vx + vy * vy;
        float t = len2 > 0f ? Mathf.Clamp01(((px - ax) * vx + (py - ay) * vy) / len2) : 0f;
        float dx = px - (ax + vx * t), dy = py - (ay + vy * t);
        return Mathf.Sqrt(dx * dx + dy * dy);
    }

    /// <summary>刀锋曲线：x 从 0..w 映射到一条带弧度的斜线</summary>
    static float BladeCurve(float x, float w, float h, float y0, float y1, float bow)
    {
        float t = x / w;
        return h * (y0 + (y1 - y0) * t + bow * Mathf.Sin(t * Mathf.PI));
    }

    static void MakeSkillIcons()
    {
        // 轻击：细长刀锋
        IconLight = Icon(128, (x, y, w, h) =>
        {
            if (x < w * 0.10f || x > w * 0.92f) return false;
            float cy = BladeCurve(x, w, h, 0.12f, 0.84f, 0.10f);
            float halfH = w * 0.055f * (0.35f + 0.65f * (1f - x / w));
            return Mathf.Abs(y - cy) <= halfH;
        });

        // 重击：宽刃 + 刃背高光
        IconHeavy = Icon(128, (x, y, w, h) =>
        {
            if (x < w * 0.12f || x > w * 0.90f) return false;
            float cy = BladeCurve(x, w, h, 0.10f, 0.84f, 0.10f);
            float halfH = w * 0.11f * (0.35f + 0.65f * (1f - x / w));
            if (Mathf.Abs(y - cy) <= halfH) return true;
            return Mathf.Abs(y - (cy - halfH * 0.45f)) <= w * 0.016f && x > w * 0.20f;
        });

        // 闪避：三道向右的残影弧（像风）
        IconDodge = Icon(128, (x, y, w, h) =>
        {
            for (int k = 0; k < 3; k++)
            {
                float x0 = w * (0.10f + k * 0.17f);
                float halfH = w * (0.30f - k * 0.055f);
                if (x < x0 || x >= x0 + w * 0.11f) continue;
                float t = (x - x0) / (w * 0.11f);
                float bulge = halfH * Mathf.Sin(t * Mathf.PI * 0.5f);
                if (Mathf.Abs(y - (h * 0.5f - bulge)) <= w * 0.045f) return true;
            }
            return false;
        });

        // Q 破军突刺：枪身 + 菱形枪尖 + 枪缨
        IconQ = Icon(128, (x, y, w, h) =>
        {
            float cy = h * 0.52f;
            if (y > cy - w * 0.024f && y < cy + w * 0.024f && x > w * 0.24f && x < w * 0.70f) return true;
            if (x >= w * 0.68f && x <= w * 0.95f)
            {
                float t = (x - w * 0.68f) / (w * 0.27f);
                float halfH = w * 0.11f * (1f - t);
                if (Mathf.Abs(y - cy) <= halfH) return true;
            }
            // 枪缨：三笔斜向飘出
            float[,] tassel = { { 0.20f, -0.16f }, { 0.16f, -0.05f }, { 0.18f, 0.06f } };
            for (int i = 0; i < 3; i++)
            {
                float sx = w * (0.26f - tassel[i, 0]);
                float sy = cy + h * tassel[i, 1];
                float tx = w * (0.26f - tassel[i, 0] * 2.1f);
                float ty = cy + h * tassel[i, 1] * 1.9f;
                if (SegDist(x, y, sx, sy, tx, ty) <= w * 0.026f) return true;
            }
            return false;
        });

        // E 横扫千军：带箭头的旋转圆弧
        IconE = Icon(128, (x, y, w, h) =>
        {
            float cx = w * 0.5f, cy = h * 0.5f;
            float dx = x - cx, dy = y - cy;
            float r = Mathf.Sqrt(dx * dx + dy * dy);
            if (Mathf.Abs(r - w * 0.34f) > w * 0.052f) return false;
            float ang = Mathf.Atan2(dy, dx);
            if (ang > 0.45f && ang < 1.75f) return false;
            // 缺口两端各一个箭头
            float[] tips = { 0.45f, 1.75f };
            for (int i = 0; i < 2; i++)
            {
                float bx = cx + Mathf.Cos(tips[i]) * w * 0.34f;
                float by = cy + Mathf.Sin(tips[i]) * w * 0.34f;
                for (int s = 0; s <= 30; s++)
                {
                    float t = s / 30f;
                    float tang = tips[i] + (t - 0.5f) * 1.25f;
                    float rad = t * w * 0.10f;
                    float px = bx + Mathf.Cos(tang) * rad;
                    float py = by + Mathf.Sin(tang) * rad;
                    if (Mathf.Sqrt((x - px) * (x - px) + (y - py) * (y - py)) <= w * 0.038f) return true;
                }
            }
            return true;
        });

        // R 武神降临：陨星 + 地面冲击环 + 光柱
        IconR = Icon(128, (x, y, w, h) =>
        {
            float cx = w * 0.64f, cy = h * 0.26f;
            float dx = x - cx, dy = y - cy;
            if (Mathf.Sqrt(dx * dx + dy * dy) < w * 0.16f) return true;
            for (int s = 0; s <= 20; s++)
            {
                float t = s / 20f;
                float px = cx - w * 0.16f * t;
                float py = cy + w * 0.16f * t;
                if (Mathf.Sqrt((x - px) * (x - px) + (y - py) * (y - py)) <= w * 0.050f * (1f - t * 0.8f)) return true;
            }
            float ex = x - w * 0.42f, ey = y - h * 0.80f;
            float r = Mathf.Sqrt(ex * ex + ey * ey);
            if (Mathf.Abs(r - w * 0.30f) < w * 0.036f) return true;
            return x > w * 0.34f && x < w * 0.50f && y > h * 0.40f && y < h * 0.80f;
        });

        // 上面这套程序化笔触只有 1~2px 宽，压到 68px 的技能按钮里几乎看不见。
        // 有 PNG 就换上（Tools/make_skill_icons.py 生成，带描边和高光）。
        LoadIconPng("ui/icon_q.png", ref IconQ);
        LoadIconPng("ui/icon_e.png", ref IconE);
        LoadIconPng("ui/icon_r.png", ref IconR);
    }

    /// <summary>用 PNG 覆盖程序化图标；文件缺失则保持原样。</summary>
    static void LoadIconPng(string rel, ref Texture2D slot)
    {
        string full = Path.Combine(Root, rel);
        if (!File.Exists(full)) return;
        byte[] bytes = File.ReadAllBytes(full);
        var t = new Texture2D(2, 2, TextureFormat.RGBA32, true);
        if (!t.LoadImage(bytes))
        {
            Debug.LogWarning("[Tex] 图标解码失败: " + full);
            return;
        }
        t.wrapMode = TextureWrapMode.Clamp;
        t.filterMode = FilterMode.Trilinear;
        t.Apply(true, false);
        slot = t;
    }

    // ---------------- 基础图 ----------------

    static Texture2D Solid(int w, int h, Color c)
    {
        var t = new Texture2D(w, h, TextureFormat.RGBA32, false);
        var px = new Color32[w * h];
        Color32 c32 = c;
        for (int i = 0; i < px.Length; i++) px[i] = c32;
        t.SetPixels32(px);
        t.wrapMode = TextureWrapMode.Clamp;
        t.Apply(false, false);
        return t;
    }

    static Texture2D Radial(int size, Color c, float power)
    {
        var t = new Texture2D(size, size, TextureFormat.RGBA32, false);
        var px = new Color32[size * size];
        float half = size * 0.5f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f - half) / half;
                float dy = (y + 0.5f - half) / half;
                float r = Mathf.Clamp01(Mathf.Sqrt(dx * dx + dy * dy));
                float a = Mathf.Pow(1f - r, power);
                Color32 v = c;
                v.a = (byte)(a * c.a * 255f);
                px[y * size + x] = v;
            }
        }
        t.SetPixels32(px);
        t.wrapMode = TextureWrapMode.Clamp;
        t.Apply(false, false);
        return t;
    }

    static Texture2D MakeRing(int size, float radius, float width)
    {
        var t = new Texture2D(size, size, TextureFormat.RGBA32, false);
        var px = new Color32[size * size];
        float half = size * 0.5f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f - half) / half;
                float dy = (y + 0.5f - half) / half;
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                float d = Mathf.Abs(r - radius);
                float a = Mathf.Clamp01(1f - d / width);
                a = a * a;
                px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
        }
        t.SetPixels32(px);
        t.wrapMode = TextureWrapMode.Clamp;
        t.Apply(false, false);
        return t;
    }

    /// <summary>细长火星：沿速度方向拉伸后就是打击火花</summary>
    static Texture2D MakeSpark(int size)
    {
        var t = new Texture2D(size, size, TextureFormat.RGBA32, false);
        var px = new Color32[size * size];
        float half = size * 0.5f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f - half) / half;
                float dy = (y + 0.5f - half) / half;
                float a = Mathf.Exp(-dx * dx * 18f) * Mathf.Exp(-dy * dy * 2.4f);
                px[y * size + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(a) * 255f));
            }
        }
        t.SetPixels32(px);
        t.wrapMode = TextureWrapMode.Clamp;
        t.filterMode = FilterMode.Bilinear;
        t.Apply(false, false);
        return t;
    }

    /// <summary>四角星芒，暴击闪光用</summary>
    static Texture2D MakeStar(int size)
    {
        var t = new Texture2D(size, size, TextureFormat.RGBA32, false);
        var px = new Color32[size * size];
        float half = size * 0.5f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f - half) / half;
                float dy = (y + 0.5f - half) / half;
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                float ang = Mathf.Atan2(dy, dx);
                float spike = Mathf.Abs(Mathf.Cos(ang * 2f));
                float a = Mathf.Clamp01(1f - r / (0.18f + spike * 0.82f));
                a = Mathf.Pow(a, 1.35f);
                px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
        }
        t.SetPixels32(px);
        t.wrapMode = TextureWrapMode.Clamp;
        t.filterMode = FilterMode.Bilinear;
        t.Apply(false, false);
        return t;
    }

    /// <summary>白色新月刀光，使用时用 SpriteRenderer.color 染色</summary>
    static Texture2D MakeSlash(int size)
    {
        var t = new Texture2D(size, size, TextureFormat.RGBA32, false);
        var px = new Color32[size * size];
        float half = size * 0.5f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f - half) / half;
                float dy = (y + 0.5f - half) / half;
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                float ang = Mathf.Atan2(dy, dx);
                float band = 0.78f;
                float thick = 0.15f;
                float a = 0f;
                if (Mathf.Abs(ang) < 0.95f)
                {
                    float taper = Mathf.Cos(ang / 0.95f * Mathf.PI * 0.5f);
                    a = Mathf.Clamp01(1f - Mathf.Abs(r - band) / thick) * taper;
                    a = Mathf.Pow(a, 0.7f);
                }
                px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
        }
        t.SetPixels32(px);
        t.wrapMode = TextureWrapMode.Clamp;
        t.Apply(false, false);
        return t;
    }

    // ---------------- 读图 ----------------

    public static Texture2D LoadRaw(string relPath)
    {
        string full = Path.Combine(Root, relPath);
        if (!File.Exists(full))
        {
            Debug.LogWarning("[Tex] 缺失贴图: " + full);
            return White;
        }
        byte[] bytes = File.ReadAllBytes(full);
        // 必须带 mipmap：地面贴图在镜头里被压缩到原来的几分之一像素，
        // 没有 mip 链就退化成满屏的盐粒噪点。
        var t = new Texture2D(2, 2, TextureFormat.RGBA32, true);
        if (!t.LoadImage(bytes))
        {
            Debug.LogWarning("[Tex] 解码失败: " + full);
            return White;
        }
        t.filterMode = FilterMode.Trilinear;
        t.anisoLevel = 6;
        t.Apply(true, false);
        return t;
    }

    // ---------------- 绿幕抠像（逐像素移植 assets.js） ----------------

    /// <summary>
    /// 整幅抠像：绿幕→透明 + 去绿边 + 描边。返回带 alpha 的像素，不做包围盒裁剪。
    /// cutWatermark：是否抹掉右下角水印区。帧条不能抹（右下角属于最后一帧）。
    /// </summary>
    static Color32[] KeyAll(string relPath, out int w, out int h, bool cutWatermark)
    {
        Texture2D src = LoadRaw(relPath);
        w = src.width; h = src.height;
        Color32[] px = src.GetPixels32();

        // 1) 四角平均背景色
        float kr = 0, kg = 0, kb = 0; int n = 0;
        int[] csx = { 2, w - 8, 2, w - 8 };
        int[] csy = { 2, 2, h - 8, h - 8 };
        for (int c = 0; c < 4; c++)
        {
            for (int y = csy[c]; y < csy[c] + 6; y++)
            {
                for (int x = csx[c]; x < csx[c] + 6; x++)
                {
                    if (x < 0 || y < 0 || x >= w || y >= h) continue;
                    Color32 p = px[y * w + x];
                    kr += p.r; kg += p.g; kb += p.b; n++;
                }
            }
        }
        if (n == 0) n = 1;
        kr /= n; kg /= n; kb /= n;

        const float t0 = 58f, t1 = 122f;
        int wmX = Mathf.FloorToInt(w * 0.87f);
        int wmY = Mathf.FloorToInt(h * 0.12f); // Unity 像素行 0 在图片底部，水印在右下角

        byte[] a = new byte[w * h];
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                int i = y * w + x;
                if (cutWatermark && x > wmX && y < wmY) { a[i] = 0; continue; }   // 水印区裁掉
                Color32 p = px[i];
                float dist = Mathf.Sqrt((p.r - kr) * (p.r - kr) + (p.g - kg) * (p.g - kg) + (p.b - kb) * (p.b - kb));
                float al;
                if (dist < t0) al = 0f;
                else if (dist < t1) al = (dist - t0) / (t1 - t0);
                else al = 1f;
                if (al > 0f && al < 0.94f)
                {
                    int gm = p.g - Mathf.Max(p.r, p.b);
                    if (gm > 2) al *= 1f - Mathf.Min(0.9f, gm / 26f);
                }
                a[i] = (byte)(al * 255f);
            }
        }

        // 2) 去绿边
        for (int i = 0; i < px.Length; i++)
        {
            byte al = a[i];
            if (al > 0 && al < 245)
            {
                Color32 p = px[i];
                byte rb = p.r > p.b ? p.r : p.b;
                if (p.g > rb) { p.g = (byte)Mathf.Min(255, rb + 8); px[i] = p; }
            }
        }

        // 3) 颜色渗漏：透明区 RGB 用邻近不透明像素填上。
        // 不做的话 mipmap 下采样会把绿幕的绿混进边缘，远处角色一圈绿晕。
        for (int pass = 0; pass < 3; pass++)
        {
            Color32[] snap = (Color32[])px.Clone();
            byte[] sa = (byte[])a.Clone();
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    int i = y * w + x;
                    if (sa[i] > 24) continue;
                    int rs = 0, gs = 0, bs = 0, cnt = 0;
                    for (int oy = -1; oy <= 1; oy++)
                    {
                        int ny = y + oy;
                        if (ny < 0 || ny >= h) continue;
                        for (int ox = -1; ox <= 1; ox++)
                        {
                            int nx = x + ox;
                            if (nx < 0 || nx >= w) continue;
                            int j = ny * w + nx;
                            if (sa[j] > 24) { Color32 q = snap[j]; rs += q.r; gs += q.g; bs += q.b; cnt++; }
                        }
                    }
                    if (cnt > 0)
                    {
                        Color32 p = px[i];
                        p.r = (byte)(rs / cnt); p.g = (byte)(gs / cnt); p.b = (byte)(bs / cnt);
                        px[i] = p;
                    }
                }
            }
        }

        // 4) 描边：两圈深褐，内实外虚，缩到屏幕上仍能读出轮廓
        byte[] srcA = (byte[])a.Clone();
        const int OUT = 2;
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                int i = y * w + x;
                if (srcA[i] > 40) continue;
                int hit = 0;
                for (int ring = 1; ring <= OUT && hit == 0; ring++)
                {
                    for (int oy = -ring; oy <= ring && hit == 0; oy++)
                    {
                        int ny = y + oy;
                        if (ny < 0 || ny >= h) continue;
                        for (int ox = -ring; ox <= ring; ox++)
                        {
                            if (Mathf.Max(Mathf.Abs(ox), Mathf.Abs(oy)) != ring) continue;
                            int nx = x + ox;
                            if (nx < 0 || nx >= w) continue;
                            if (srcA[ny * w + nx] > 205) { hit = ring; break; }
                        }
                    }
                }
                if (hit > 0)
                {
                    px[i] = new Color32(38, 30, 22, 0);
                    a[i] = (byte)Mathf.Max(a[i], hit == 1 ? 215 : 105);
                }
            }
        }

        // alpha 写回像素
        for (int i = 0; i < px.Length; i++) px[i].a = a[i];
        if (src != White) Object.Destroy(src);
        return px;
    }

    public static Texture2D Cutout(string relPath, out float aspect)
    {
        if (cutCache.TryGetValue(relPath, out var cached))
        {
            aspect = aspectCache[relPath];
            return cached;
        }

        int w, h;
        Color32[] px = KeyAll(relPath, out w, out h, true);
        byte[] a = new byte[w * h];
        for (int i = 0; i < a.Length; i++) a[i] = px[i].a;

        // 4) 不透明包围盒
        int minX = w, minY = h, maxX = -1, maxY = -1;
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                if (a[y * w + x] > 30)
                {
                    if (x < minX) minX = x;
                    if (x > maxX) maxX = x;
                    if (y < minY) minY = y;
                    if (y > maxY) maxY = y;
                }
            }
        }
        if (maxX <= minX || maxY <= minY) { minX = 0; minY = 0; maxX = w - 1; maxY = h - 1; }
        int padX = Mathf.RoundToInt((maxX - minX) * 0.04f);
        int padY = Mathf.RoundToInt((maxY - minY) * 0.02f);
        minX = Mathf.Max(0, minX - padX); maxX = Mathf.Min(w - 1, maxX + padX);
        minY = Mathf.Max(0, minY - padY); maxY = Mathf.Min(h - 1, maxY + padY);

        int cw = maxX - minX + 1, ch = maxY - minY + 1;
        var outPx = new Color32[cw * ch];
        for (int y = 0; y < ch; y++)
        {
            for (int x = 0; x < cw; x++)
            {
                int sy = minY + y, sx = minX + x;
                outPx[y * cw + x] = px[sy * w + sx];
            }
        }

        var tex = new Texture2D(cw, ch, TextureFormat.RGBA32, true);
        tex.SetPixels32(outPx);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Trilinear;   // 开 mipmap：远处角色不再锯齿闪烁
        tex.anisoLevel = 8;
        tex.Apply(true, false);

        aspect = (float)cw / ch;
        cutCache[relPath] = tex;
        aspectCache[relPath] = aspect;
        return tex;
    }

    /// <summary>
    /// 把一张横向帧条切成 N 张独立贴图，供帧动画用。
    /// 关键：按「原图宽等分」切，而不是裁到不透明包围盒后再等分 ——
    /// 帧条里的绿边宽度并不均匀（人物在格内的水平位置随姿势变化），
    /// 裁包围盒会让切线偏离真实人物，实测轴漂移最大 2000+ px，动画会左右乱跳。
    /// Python 端已把每帧人物按身体轴对齐到格中心，故等分即正确。
    /// </summary>
    public static Texture2D[] CutoutFrames(string relPath, int count, out float frameAspect)
    {
        frameAspect = 1f;
        if (count <= 0) return null;
        string key = relPath + "|x" + count;
        if (frameCache.TryGetValue(key, out var cached))
        {
            frameAspect = frameAspCache[key];
            return cached;
        }

        int sw, sh;
        Color32[] all = KeyAll(relPath, out sw, out sh, false);
        int fw = sw / count;
        if (fw < 2) return null;

        // 垂直方向：所有帧统一用整幅的高度，保证脚底在同一水平线上
        int cropY0 = 0, cropY1 = sh - 1;
        int fh = sh;

        var result = new Texture2D[count];
        for (int f = 0; f < count; f++)
        {
            var px = new Color32[fw * fh];
            for (int y = 0; y < fh; y++)
                System.Array.Copy(all, (cropY0 + y) * sw + f * fw, px, y * fw, fw);

            // 清掉每格边缘 2 列：AI 帧条常在分界处画深色竖线，抠像去不掉
            for (int y = 0; y < fh; y++)
            {
                for (int e = 0; e < 2; e++)
                {
                    px[y * fw + e] = new Color32(0, 0, 0, 0);
                    px[y * fw + fw - 1 - e] = new Color32(0, 0, 0, 0);
                }
            }

            var t = new Texture2D(fw, fh, TextureFormat.RGBA32, true);
            t.SetPixels32(px);
            t.wrapMode = TextureWrapMode.Clamp;
            t.filterMode = FilterMode.Trilinear;
            t.anisoLevel = 8;
            t.Apply(true, false);
            result[f] = t;
        }

        frameAspect = (float)fw / fh;
        frameCache[key] = result;
        frameAspCache[key] = frameAspect;
        return result;
    }

    /// <summary>
    /// 对话立绘：Boss 是绿幕全身像，先抠像去背景再取头肩；主公是成品水彩画，直接取上半身。
    /// 统一裁成 116:150，绘制时按 ScaleToFill 铺满立绘框。
    /// </summary>
    public static Texture2D Portrait(string relPath)
    {
        if (portraitCache.TryGetValue(relPath, out var cached)) return cached;

        bool keyed = relPath.StartsWith("char/");
        Texture2D src = keyed ? Cutout(relPath, out _) : LoadRaw(relPath);

        int w = src.width, h = src.height;
        // 取景高度：抠像后的全身像取顶部 40%（头肩），水彩画取顶部 40%（头部）
        float hf = 0.40f;
        int cropH = Mathf.Max(8, Mathf.RoundToInt(h * hf));
        int cropW = Mathf.Min(w, Mathf.RoundToInt(cropH * 116f / 150f));
        cropH = Mathf.Max(8, Mathf.RoundToInt(cropW * 150f / 116f));
        int x0 = (w - cropW) / 2;
        int y0 = h - cropH;   // Unity 像素行 0 在底部，顶部是 h-1

        var all = src.GetPixels32();
        var px = new Color32[cropW * cropH];
        System.Array.Copy(all, y0 * w + x0, px, 0, cropW * cropH);

        var t = new Texture2D(cropW, cropH, TextureFormat.RGBA32, true);
        t.SetPixels32(px);
        t.wrapMode = TextureWrapMode.Clamp;
        t.filterMode = FilterMode.Trilinear;
        t.Apply(true, false);
        portraitCache[relPath] = t;
        if (!keyed && src != White) Object.Destroy(src);
        return t;
    }

    /// <summary>
    /// 卡面横幅：取立绘顶部 42% 且保留整幅宽度，用于阵营卡（窄幅 Portrait 会把头切掉）。
    /// </summary>
    public static Texture2D PortraitBand(string relPath)
    {
        if (bandCache.TryGetValue(relPath, out var cached)) return cached;

        Texture2D src = LoadRaw(relPath);
        int w = src.width, h = src.height;
        int cropH = Mathf.Max(8, Mathf.RoundToInt(h * 0.42f));
        int y0 = h - cropH;   // 顶部

        var all = src.GetPixels32();
        var px = new Color32[w * cropH];
        System.Array.Copy(all, y0 * w, px, 0, w * cropH);

        var t = new Texture2D(w, cropH, TextureFormat.RGBA32, true);
        t.SetPixels32(px);
        t.wrapMode = TextureWrapMode.Clamp;
        t.filterMode = FilterMode.Trilinear;
        t.Apply(true, false);
        bandCache[relPath] = t;
        if (src != White) Object.Destroy(src);
        return t;
    }

    // ---------------- AI 地面 2×2 镜像无缝 ----------------
    public static Texture2D MirrorGround(string relPath, int tile, float skip = 0.20f)
    {
        string key = relPath + "|mirror|v3";
        if (cutCache.TryGetValue(key, out var cached)) return cached;

        Texture2D src = LoadRaw(relPath);
        Color32[] sp = src.GetPixels32();
        int sw = src.width, sh = src.height;
        // 多裁一点：去掉 AI 水印角 + 削弱原图里过强的车辙条带
        int cw = Mathf.FloorToInt(sw * (1f - skip));
        int ch = Mathf.FloorToInt(sh * (1f - skip));
        int ox = Mathf.FloorToInt(sw * 0.04f);
        int oy = Mathf.FloorToInt(sh * 0.04f);

        int size = tile * 2;
        var outPx = new Color32[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                int mx = x < tile ? x : size - 1 - x;   // 水平镜像
                int my = y < tile ? y : size - 1 - y;   // 垂直镜像
                float fx = ox + mx / (float)(tile - 1) * (cw - 1);
                float fy = oy + my / (float)(tile - 1) * (ch - 1);
                Color32 c = SampleBilinear(sp, sw, sh, fx, fy);

                // 轻微提饱和 + 细砂，不要往中灰压（否则整片地面会糊成雾色）
                float n = Hash01(x * 374761393 + y * 668265263);
                float grain = (n - 0.5f) * 10f;
                outPx[y * size + x] = new Color32(
                    (byte)Mathf.Clamp(c.r * 1.08f + grain, 0f, 255f),
                    (byte)Mathf.Clamp(c.g * 1.05f + grain * 0.85f, 0f, 255f),
                    (byte)Mathf.Clamp(c.b * 0.98f + grain * 0.7f, 0f, 255f),
                    255);
            }
        }

        var tex = new Texture2D(size, size, TextureFormat.RGBA32, true);
        tex.SetPixels32(outPx);
        tex.wrapMode = TextureWrapMode.Repeat;
        tex.filterMode = FilterMode.Trilinear;
        tex.anisoLevel = 8;
        // 保持可读：着色器要用这张图算平均色，把地面低频层的颜色归一化
        tex.Apply(true, false);

        cutCache[key] = tex;
        if (src != White) Object.Destroy(src);
        return tex;
    }

    static float Hash01(int n)
    {
        unchecked
        {
            uint x = (uint)n;
            x ^= x >> 16; x *= 0x7feb352du; x ^= x >> 15; x *= 0x846ca68bu; x ^= x >> 16;
            return (x & 0xFFFFFF) / 16777215f;
        }
    }

    static Color32 SampleBilinear(Color32[] src, int sw, int sh, float fx, float fy)
    {
        int x0 = Mathf.Clamp(Mathf.FloorToInt(fx), 0, sw - 1);
        int y0 = Mathf.Clamp(Mathf.FloorToInt(fy), 0, sh - 1);
        int x1 = Mathf.Min(x0 + 1, sw - 1);
        int y1 = Mathf.Min(y0 + 1, sh - 1);
        float tx = fx - x0, ty = fy - y0;
        Color32 a = src[y0 * sw + x0], b = src[y0 * sw + x1];
        Color32 c = src[y1 * sw + x0], d = src[y1 * sw + x1];
        return new Color32(
            (byte)((a.r + (b.r - a.r) * tx) * (1 - ty) + (c.r + (d.r - c.r) * tx) * ty),
            (byte)((a.g + (b.g - a.g) * tx) * (1 - ty) + (c.g + (d.g - c.g) * tx) * ty),
            (byte)((a.b + (b.b - a.b) * tx) * (1 - ty) + (c.b + (d.b - c.b) * tx) * ty),
            (byte)((a.a + (b.a - a.a) * tx) * (1 - ty) + (c.a + (d.a - c.a) * tx) * ty));
    }

    // ---------------- Sprite ----------------

    public static Sprite SpriteFor(Texture2D tex, float worldHeight)
    {
        string key = tex.GetInstanceID() + "|" + worldHeight.ToString("F2");
        if (spriteCache.TryGetValue(key, out var s)) return s;
        float ppu = tex.height / Mathf.Max(0.01f, worldHeight);
        s = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0f), ppu, 0, SpriteMeshType.FullRect);
        spriteCache[key] = s;
        return s;
    }

    /// <summary>在挂点下建一个 SpriteRenderer 子物体（脚底对齐），带上描边 + 受光渐变的精灵材质</summary>
    public static SpriteRenderer Actor(Transform parent, Sprite sprite, int order)
    {
        var go = new GameObject("spr");
        go.transform.SetParent(parent, false);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sortingOrder = order;
        sr.sharedMaterial = Look.NewSpriteOutlined(sprite);
        return sr;
    }

    /// <summary>中心锚点的方形 Sprite（特效、光环、影子用）</summary>
    public static Sprite CenterSprite(Texture2D tex, float size)
    {
        string key = "c" + tex.GetInstanceID() + "|" + size.ToString("F2");
        if (spriteCache.TryGetValue(key, out var s)) return s;
        float ppu = tex.height / Mathf.Max(0.01f, size);
        s = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), ppu, 0, SpriteMeshType.FullRect);
        spriteCache[key] = s;
        return s;
    }

    /// <summary>程序化天空渐变：文件缺失时的兜底</summary>
    public static Texture2D MakeSkyGradient(Color fog)
    {
        const int w = 16, h = 128;
        var t = new Texture2D(w, h, TextureFormat.RGB24, false);
        Color top = Color.Lerp(fog, new Color(0.72f, 0.80f, 0.88f), 0.55f);
        Color mid = Color.Lerp(fog, new Color(0.90f, 0.86f, 0.76f), 0.35f);
        Color bot = Color.Lerp(fog, new Color(0.62f, 0.55f, 0.42f), 0.4f);
        for (int y = 0; y < h; y++)
        {
            float u = y / (float)(h - 1);
            Color c = u < 0.55f ? Color.Lerp(bot, mid, u / 0.55f) : Color.Lerp(mid, top, (u - 0.55f) / 0.45f);
            for (int x = 0; x < w; x++) t.SetPixel(x, y, c);
        }
        t.wrapMode = TextureWrapMode.Clamp;
        t.Apply(false, false);
        return t;
    }

    /// <summary>程序化土地：文件失败时保证地面有可见纹理</summary>
    public static Texture2D MakeDirtFallback()
    {
        const int S = 256;
        var t = new Texture2D(S, S, TextureFormat.RGB24, true);
        for (int y = 0; y < S; y++)
        for (int x = 0; x < S; x++)
        {
            float n = Hash01(x * 73856093 ^ y * 19349663);
            float n2 = Hash01(x * 19349663 ^ y * 83492791);
            float r = 0.52f + n * 0.18f + n2 * 0.06f;
            float g = 0.42f + n * 0.14f + n2 * 0.05f;
            float b = 0.28f + n * 0.10f;
            t.SetPixel(x, y, new Color(r, g, b));
        }
        t.wrapMode = TextureWrapMode.Repeat;
        t.filterMode = FilterMode.Bilinear;
        t.Apply(true, false);
        return t;
    }

    /// <summary>1 世界单位宽的白条，pivotX=0 时从左往右缩放（血条用）</summary>
    public static Sprite BarSprite(float pivotX)
    {
        string key = "bar" + pivotX.ToString("F2");
        if (spriteCache.TryGetValue(key, out var s)) return s;
        float ppu = White.height / 1f;
        s = Sprite.Create(White, new Rect(0, 0, White.width, White.height), new Vector2(pivotX, 0.5f), ppu, 0, SpriteMeshType.FullRect);
        spriteCache[key] = s;
        return s;
    }
}
