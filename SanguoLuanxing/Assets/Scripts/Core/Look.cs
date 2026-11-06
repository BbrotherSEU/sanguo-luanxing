using UnityEngine;

/// <summary>场景配色：一套 Theme 决定地表、植被、水、天光与氛围粒子。</summary>
public class Theme
{
    public string Id;
    public Color GrassA, GrassB, GrassDry, Path, Sand, Cliff, Peak;
    public Color Rock, Leaf1, Leaf2, Leaf3, Trunk, Blossom1, Blossom2;
    public Color Flower1 = new Color(1f, 0.86f, 0.3f), Flower2 = Color.white, Flower3 = new Color(0.95f, 0.45f, 0.5f);
    public Color WaterDeep, WaterShallow;
    public Color Sky, Fog, Light, ShadowTint;
    public Color Wall = new Color(0.93f, 0.88f, 0.78f), Wood = new Color(0.52f, 0.30f, 0.20f);
    public Color RoofA = new Color(0.33f, 0.38f, 0.48f), RoofB = new Color(0.72f, 0.24f, 0.18f);
    public Color Stone = new Color(0.66f, 0.64f, 0.60f), Lacquer = new Color(0.78f, 0.20f, 0.16f);
    public float LightIntensity = 1.15f;
    public Vector3 LightEuler = new Vector3(50f, 150f, 0f);
    public float FogStart = 34f, FogEnd = 100f;
    public float PeakAmount;
    public float SnowCaps;
    public float TreeDensity = 1f, GrassDensity = 1f, FlowerDensity = 1f;
    public string[] Trees = { "round" };
    public string Particles = "";
}

public static class Themes
{
    public static readonly Theme Village = new Theme
    {
        Id = "village",
        GrassA = new Color(0.45f, 0.74f, 0.31f), GrassB = new Color(0.60f, 0.83f, 0.36f), GrassDry = new Color(0.74f, 0.80f, 0.38f),
        Path = new Color(0.88f, 0.76f, 0.54f), Sand = new Color(0.94f, 0.87f, 0.64f),
        Cliff = new Color(0.62f, 0.55f, 0.46f), Peak = new Color(0.40f, 0.62f, 0.34f),
        Rock = new Color(0.66f, 0.65f, 0.63f),
        Leaf1 = new Color(0.33f, 0.64f, 0.28f), Leaf2 = new Color(0.45f, 0.74f, 0.30f), Leaf3 = new Color(0.26f, 0.52f, 0.30f),
        Trunk = new Color(0.50f, 0.34f, 0.24f),
        Blossom1 = new Color(1f, 0.70f, 0.80f), Blossom2 = new Color(1f, 0.84f, 0.90f),
        WaterDeep = new Color(0.20f, 0.54f, 0.80f), WaterShallow = new Color(0.46f, 0.84f, 0.90f),
        Sky = new Color(0.66f, 0.84f, 0.96f), Fog = new Color(0.80f, 0.89f, 0.95f),
        Light = new Color(1f, 0.96f, 0.86f), ShadowTint = new Color(0.64f, 0.62f, 0.84f),
        Trees = new[] { "peach", "round", "peach", "round", "bamboo" },
        Particles = "petal", GrassDensity = 1.1f, FlowerDensity = 1.3f, TreeDensity = 0.8f,
    };

    public static readonly Theme Wild = new Theme
    {
        Id = "wild",
        GrassA = new Color(0.38f, 0.66f, 0.29f), GrassB = new Color(0.52f, 0.76f, 0.33f), GrassDry = new Color(0.66f, 0.70f, 0.34f),
        Path = new Color(0.80f, 0.66f, 0.46f), Sand = new Color(0.90f, 0.82f, 0.60f),
        Cliff = new Color(0.56f, 0.50f, 0.44f), Peak = new Color(0.30f, 0.52f, 0.30f),
        Rock = new Color(0.60f, 0.60f, 0.60f),
        Leaf1 = new Color(0.26f, 0.54f, 0.26f), Leaf2 = new Color(0.38f, 0.66f, 0.28f), Leaf3 = new Color(0.20f, 0.44f, 0.30f),
        Trunk = new Color(0.46f, 0.31f, 0.22f),
        Blossom1 = new Color(1f, 0.72f, 0.82f), Blossom2 = new Color(1f, 0.86f, 0.92f),
        WaterDeep = new Color(0.18f, 0.48f, 0.72f), WaterShallow = new Color(0.40f, 0.78f, 0.84f),
        Sky = new Color(0.62f, 0.80f, 0.92f), Fog = new Color(0.74f, 0.86f, 0.88f),
        Light = new Color(1f, 0.95f, 0.84f), ShadowTint = new Color(0.58f, 0.60f, 0.80f),
        Trees = new[] { "round", "pine", "round", "peach" },
        Particles = "leaf", TreeDensity = 1.4f, GrassDensity = 1.2f,
    };

    public static readonly Theme PeachGrove = new Theme
    {
        Id = "peachgrove",
        GrassA = new Color(0.47f, 0.72f, 0.32f), GrassB = new Color(0.62f, 0.80f, 0.40f), GrassDry = new Color(0.86f, 0.72f, 0.70f),
        Path = new Color(0.86f, 0.72f, 0.54f), Sand = new Color(0.92f, 0.84f, 0.64f),
        Cliff = new Color(0.60f, 0.52f, 0.46f), Peak = new Color(0.42f, 0.60f, 0.36f),
        Rock = new Color(0.66f, 0.64f, 0.64f),
        Leaf1 = new Color(0.36f, 0.62f, 0.30f), Leaf2 = new Color(0.46f, 0.70f, 0.32f), Leaf3 = new Color(0.30f, 0.52f, 0.30f),
        Trunk = new Color(0.46f, 0.30f, 0.24f),
        Blossom1 = new Color(1f, 0.66f, 0.78f), Blossom2 = new Color(1f, 0.82f, 0.88f),
        WaterDeep = new Color(0.20f, 0.52f, 0.78f), WaterShallow = new Color(0.44f, 0.80f, 0.88f),
        Sky = new Color(0.86f, 0.80f, 0.90f), Fog = new Color(0.92f, 0.86f, 0.90f),
        Light = new Color(1f, 0.94f, 0.88f), ShadowTint = new Color(0.68f, 0.60f, 0.82f),
        Trees = new[] { "peach", "peach", "peach", "round" },
        Particles = "petal", FlowerDensity = 1.4f,
    };

    public static readonly Theme Snow = new Theme
    {
        Id = "snow",
        GrassA = new Color(0.90f, 0.93f, 0.97f), GrassB = new Color(0.82f, 0.88f, 0.94f), GrassDry = new Color(0.64f, 0.70f, 0.56f),
        Path = new Color(0.66f, 0.60f, 0.56f), Sand = new Color(0.74f, 0.76f, 0.78f),
        Cliff = new Color(0.52f, 0.54f, 0.62f), Peak = new Color(0.97f, 0.98f, 1f),
        Rock = new Color(0.54f, 0.56f, 0.62f),
        Leaf1 = new Color(0.20f, 0.42f, 0.40f), Leaf2 = new Color(0.26f, 0.50f, 0.44f), Leaf3 = new Color(0.16f, 0.34f, 0.36f),
        Trunk = new Color(0.40f, 0.30f, 0.26f),
        Blossom1 = new Color(0.95f, 0.40f, 0.42f), Blossom2 = new Color(1f, 0.62f, 0.62f),
        WaterDeep = new Color(0.30f, 0.50f, 0.70f), WaterShallow = new Color(0.62f, 0.82f, 0.92f),
        Sky = new Color(0.76f, 0.83f, 0.92f), Fog = new Color(0.84f, 0.88f, 0.94f),
        Light = new Color(0.96f, 0.97f, 1f), ShadowTint = new Color(0.62f, 0.68f, 0.90f),
        Wall = new Color(0.82f, 0.76f, 0.66f),
        Trees = new[] { "pine", "pine", "dead" },
        Particles = "snow", PeakAmount = 1f, SnowCaps = 1f, GrassDensity = 0.35f, FlowerDensity = 0.15f, TreeDensity = 1.1f,
    };

    public static readonly Theme Golden = new Theme
    {
        Id = "golden",
        GrassA = new Color(0.80f, 0.74f, 0.38f), GrassB = new Color(0.88f, 0.80f, 0.44f), GrassDry = new Color(0.74f, 0.62f, 0.36f),
        Path = new Color(0.86f, 0.72f, 0.52f), Sand = new Color(0.94f, 0.86f, 0.62f),
        Cliff = new Color(0.70f, 0.56f, 0.42f), Peak = new Color(0.62f, 0.62f, 0.36f),
        Rock = new Color(0.70f, 0.64f, 0.58f),
        Leaf1 = new Color(0.94f, 0.56f, 0.24f), Leaf2 = new Color(0.97f, 0.74f, 0.30f), Leaf3 = new Color(0.78f, 0.40f, 0.22f),
        Trunk = new Color(0.46f, 0.32f, 0.24f),
        Blossom1 = new Color(0.98f, 0.62f, 0.30f), Blossom2 = new Color(1f, 0.80f, 0.40f),
        WaterDeep = new Color(0.22f, 0.50f, 0.70f), WaterShallow = new Color(0.50f, 0.80f, 0.84f),
        Sky = new Color(0.96f, 0.82f, 0.64f), Fog = new Color(0.96f, 0.86f, 0.72f),
        Light = new Color(1f, 0.88f, 0.70f), ShadowTint = new Color(0.70f, 0.58f, 0.78f),
        LightEuler = new Vector3(38f, 130f, 0f),
        Trees = new[] { "autumn", "autumn", "round", "dead" },
        Particles = "leaf", FlowerDensity = 0.6f, GrassDensity = 1.3f,
    };

    public static readonly Theme Dusk = new Theme
    {
        Id = "dusk",
        GrassA = new Color(0.44f, 0.50f, 0.36f), GrassB = new Color(0.52f, 0.54f, 0.40f), GrassDry = new Color(0.58f, 0.50f, 0.40f),
        Path = new Color(0.62f, 0.54f, 0.46f), Sand = new Color(0.70f, 0.62f, 0.52f),
        Cliff = new Color(0.46f, 0.42f, 0.44f), Peak = new Color(0.36f, 0.38f, 0.40f),
        Rock = new Color(0.48f, 0.46f, 0.48f),
        Leaf1 = new Color(0.30f, 0.40f, 0.30f), Leaf2 = new Color(0.40f, 0.46f, 0.30f), Leaf3 = new Color(0.24f, 0.32f, 0.28f),
        Trunk = new Color(0.36f, 0.26f, 0.22f),
        Blossom1 = new Color(0.86f, 0.36f, 0.30f), Blossom2 = new Color(0.94f, 0.56f, 0.40f),
        WaterDeep = new Color(0.18f, 0.28f, 0.44f), WaterShallow = new Color(0.40f, 0.48f, 0.62f),
        Sky = new Color(0.62f, 0.48f, 0.58f), Fog = new Color(0.66f, 0.52f, 0.58f),
        Light = new Color(1f, 0.74f, 0.52f), ShadowTint = new Color(0.46f, 0.42f, 0.68f),
        LightIntensity = 1.05f, LightEuler = new Vector3(30f, 120f, 0f), FogStart = 26f, FogEnd = 85f,
        Stone = new Color(0.56f, 0.54f, 0.54f),
        Trees = new[] { "dead", "pine", "dead" },
        Particles = "ember", FlowerDensity = 0.2f, GrassDensity = 0.8f, TreeDensity = 0.6f,
    };

    /// <summary>副本战场的配色按关卡装饰类型选取</summary>
    public static Theme ForDecor(string decor)
    {
        switch (decor)
        {
            case "peach": return PeachGrove;
            case "pass": return Snow;
            case "ruins": return Golden;
            case "fortress": return Dusk;
            case "village": return Village;
            default: return Wild;
        }
    }
}

/// <summary>材质工厂：所有自定义着色器放在 Resources/Shaders 下，保证打包后 Shader.Find 可用。</summary>
public static class Look
{
    static Material sprite, additive, alphaFx, card;

    static Shader Find(string name)
    {
        // Resources/Shaders 下的着色器用 Load，打包后 Shader.Find 不一定找得到
        string res = name.StartsWith("Sanguo/") ? "Shaders/" + name.Substring("Sanguo/".Length) : name;
        var s = Resources.Load<Shader>(res);
        if (s == null) s = Shader.Find(name);
        if (s == null)
        {
            Debug.LogWarning("[Look] 找不到着色器 " + name + "，回退到 Sprites/Default");
            s = Shader.Find("Sprites/Default");
        }
        return s;
    }

    /// <summary>角色精灵（带受击闪色）。每个角色用独立实例以便单独闪白。</summary>
    public static Material NewSprite() => new Material(sprite != null ? sprite : (sprite = new Material(Find("Sanguo/Sprite"))));

    /// <summary>植被立绘卡共享材质：只享受光渐变，不描边（树冠描边会糊成一团）。</summary>
    public static Material CardSprite => card != null ? card : (card = NewSprite());

    /// <summary>按精灵在图集里的实际像素尺寸给一圈描边（约 2 屏幕像素）。</summary>
    public static Material NewSpriteOutlined(Sprite sp)
    {
        var m = NewSprite();
        if (sp == null || sp.texture == null) return m;
        // 864px 立绘在镜头里约 160px 高：1 texel 只有 0.14 屏幕像素，
        // 描边必须给到 12~16 texel（换成 UV 偏移）才看得见。
        float px = Mathf.Clamp(sp.rect.width * 0.016f, 6f, 18f);
        if (m.HasProperty("_OutlineU")) m.SetFloat("_OutlineU", px / sp.texture.width);
        if (m.HasProperty("_OutlineV")) m.SetFloat("_OutlineV", px / sp.texture.height);
        return m;
    }

    public static Material Additive => additive != null ? additive : (additive = new Material(Find("Sanguo/Additive")));

    public static Material AlphaFx => alphaFx != null ? alphaFx : (alphaFx = new Material(Find("Sprites/Default")));

    public static Material NewToon(Theme t, bool outline, float outlineWidth = 0.28f)
    {
        var m = new Material(Find(outline ? "Sanguo/ToonOutline" : "Sanguo/Toon"));
        if (t != null && m.HasProperty("_ShadowTint")) m.SetColor("_ShadowTint", t.ShadowTint);
        if (outline && m.HasProperty("_OutlineWidth")) m.SetFloat("_OutlineWidth", outlineWidth);
        if (outline && m.HasProperty("_OutlineColor")) m.SetColor("_OutlineColor", new Color(0.30f, 0.22f, 0.20f));
        // 环境光权重：阴面吃天光才不会死黑。0.85 是通用值，
        // 地面/建筑另外调（见 WorldBuilder.PaintMaterials）。
        if (m.HasProperty("_AmbStrength")) m.SetFloat("_AmbStrength", 0.85f);
        return m;
    }

    /// <summary>单张卡通贴图：用明暗去调顶点色，地区主色还在。detail 为可选高频细节层（草叶/碎石）。</summary>
    /// <param name="mode">0 = 直接混贴图颜色；3 = 贴图只给相对明暗，色相全部由顶点色给。</param>
    /// <param name="contrast">mode 3 下的明暗对比倍率。1 = 贴图原样，1.8 = 草叶更分明。</param>
    public static Material NewPainted(Theme t, bool outline, float outlineWidth, Texture2D tex, float mix, float tile,
        Texture2D detail = null, float detailTile = 1f, float detailStrength = 0f, float bump = 0f, float mode = 0f,
        float contrast = 1f)
    {
        var m = NewToon(t, outline, outlineWidth);
        if (tex == null) return m;
        Tile(tex);
        if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", tex);
        if (m.HasProperty("_TexMix")) m.SetFloat("_TexMix", mix);
        if (m.HasProperty("_TexTile")) m.SetFloat("_TexTile", tile);
        if (m.HasProperty("_PaintMode")) m.SetFloat("_PaintMode", mode);
        // mode 3：贴图按自身均值中心化，得到"相对明暗"，乘到顶点色上。
        // 这样贴图可以做成中性灰，色相完全由主题/顶点色决定。
        if (mode > 2.5f && m.HasProperty("_MeanMain")) m.SetColor("_MeanMain", MeanColor(tex));
        if (mode > 2.5f && m.HasProperty("_DetailContrast")) m.SetFloat("_DetailContrast", contrast);
        if (detail != null && m.HasProperty("_DetailTex"))
        {
            Tile(detail);
            m.SetTexture("_DetailTex", detail);
            if (m.HasProperty("_DetailTile")) m.SetFloat("_DetailTile", detailTile);
            if (m.HasProperty("_DetailStrength")) m.SetFloat("_DetailStrength", detailStrength);
            if (m.HasProperty("_BumpStrength")) m.SetFloat("_BumpStrength", bump);
            // mode 3：低频层也带色（水彩地面），着色器需要它的均值做归一化
            if (mode > 2.5f && m.HasProperty("_MeanDetail")) m.SetColor("_MeanDetail", MeanColor(detail));
        }
        return m;
    }

    /// <summary>房子：按顶点色在墙、木、瓦、石之间选贴图。贴图只提供明暗，色相由顶点色决定。</summary>
    public static Material NewArch(Theme t, float outlineWidth, Texture2D wall, Texture2D wood, Texture2D roof, Texture2D stone, float mix, float tile)
    {
        var m = NewToon(t, true, outlineWidth);
        Tile(wall); Tile(wood); Tile(roof); Tile(stone);
        if (m.HasProperty("_Path")) m.SetTexture("_Path", wall);
        if (m.HasProperty("_Wood")) m.SetTexture("_Wood", wood);
        if (m.HasProperty("_Roof")) m.SetTexture("_Roof", roof);
        if (m.HasProperty("_Stone")) m.SetTexture("_Stone", stone);
        if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", wall);
        if (m.HasProperty("_TexMix")) m.SetFloat("_TexMix", mix);
        if (m.HasProperty("_TexTile")) m.SetFloat("_TexTile", tile);
        if (m.HasProperty("_PaintMode")) m.SetFloat("_PaintMode", 2f);
        // 每张图的平均亮度交给着色器归一化，粉墙、灰瓦、木纹的明暗尺度才对得上
        if (m.HasProperty("_LumWall")) m.SetFloat("_LumWall", MeanLum(wall));
        if (m.HasProperty("_LumWood")) m.SetFloat("_LumWood", MeanLum(wood));
        if (m.HasProperty("_LumRoof")) m.SetFloat("_LumRoof", MeanLum(roof));
        if (m.HasProperty("_LumStone")) m.SetFloat("_LumStone", MeanLum(stone));
        return m;
    }

    /// <summary>取 mip 缩略图的平均亮度（线性空间，和着色器里 tex2D 采到的值一致）。</summary>
    static float MeanLum(Texture2D tex)
    {
        if (tex == null || tex.width < 8) return 0.5f;
        int mip = Mathf.Clamp(tex.mipmapCount - 5, 0, tex.mipmapCount - 1);
        Color[] px;
        try { px = tex.GetPixels(mip); }
        catch { return 0.5f; }
        float sum = 0f;
        for (int i = 0; i < px.Length; i++)
        {
            Color c = px[i];
            sum += 0.30f * Mathf.GammaToLinearSpace(c.r)
                 + 0.59f * Mathf.GammaToLinearSpace(c.g)
                 + 0.11f * Mathf.GammaToLinearSpace(c.b);
        }
        return Mathf.Max(sum / Mathf.Max(px.Length, 1), 0.05f);
    }

    /// <summary>取 mip 缩略图的平均 RGB（线性空间，和着色器 tex2D 采到的值同一空间）。
    /// 着色器用它把贴图归一化成"相对明暗"，再乘回顶点色。</summary>
    static Color MeanColor(Texture2D tex)
    {
        if (tex == null || tex.width < 8) return new Color(0.5f, 0.5f, 0.5f, 1f);
        int mip = Mathf.Clamp(tex.mipmapCount - 5, 0, tex.mipmapCount - 1);
        Color[] px;
        try { px = tex.GetPixels(mip); }
        catch { return new Color(0.5f, 0.5f, 0.5f, 1f); }
        float r = 0f, g = 0f, b = 0f;
        for (int i = 0; i < px.Length; i++)
        {
            Color c = px[i];
            r += Mathf.GammaToLinearSpace(c.r);
            g += Mathf.GammaToLinearSpace(c.g);
            b += Mathf.GammaToLinearSpace(c.b);
        }
        float n = Mathf.Max(px.Length, 1);
        return new Color(
            Mathf.Max(r / n, 0.02f),
            Mathf.Max(g / n, 0.02f),
            Mathf.Max(b / n, 0.02f), 1f);
    }

    static void Tile(Texture2D tex)
    {
        if (tex == null) return;
        tex.wrapMode = TextureWrapMode.Repeat;
        tex.filterMode = FilterMode.Bilinear;
        tex.anisoLevel = 4;
    }

    public static Material NewWater(Theme t)
    {
        var m = new Material(Find("Sanguo/Water"));
        if (m.HasProperty("_Deep")) m.SetColor("_Deep", t.WaterDeep);
        if (m.HasProperty("_Shallow")) m.SetColor("_Shallow", t.WaterShallow);
        if (m.HasProperty("_ShadowTint")) m.SetColor("_ShadowTint", t.ShadowTint);
        return m;
    }

    public static Material NewUnlit(float fogMix)
    {
        var m = new Material(Find("Sanguo/Unlit"));
        if (m.HasProperty("_FogMix")) m.SetFloat("_FogMix", fogMix);
        return m;
    }
}

/// <summary>
/// 相机后处理：亮部泛光 + 色彩分级 + 暗角。挂在相机上即可，UI(OnGUI) 不受影响。
/// 着色器缺失时自动禁用，不影响任何渲染。
/// </summary>
public class PostFx : MonoBehaviour
{
    Material mat;

    void OnEnable()
    {
        var sh = Resources.Load<Shader>("Shaders/PostFx");
        if (sh != null) mat = new Material(sh);
    }

    void OnRenderImage(RenderTexture src, RenderTexture dst)
    {
        if (mat == null) { Graphics.Blit(src, dst); return; }
        int w = Mathf.Max(1, src.width / 4), h = Mathf.Max(1, src.height / 4);
        var rt0 = RenderTexture.GetTemporary(w, h, 0, src.format);
        var rt1 = RenderTexture.GetTemporary(w, h, 0, src.format);
        Graphics.Blit(src, rt0, mat, 0);
        for (int i = 0; i < 2; i++)
        {
            mat.SetVector("_BlurDir", new Vector2(1f, 0f));
            Graphics.Blit(rt0, rt1, mat, 1);
            mat.SetVector("_BlurDir", new Vector2(0f, 1f));
            Graphics.Blit(rt1, rt0, mat, 1);
        }
        mat.SetTexture("_BloomTex", rt0);
        Graphics.Blit(src, dst, mat, 2);
        RenderTexture.ReleaseTemporary(rt0);
        RenderTexture.ReleaseTemporary(rt1);
    }
}
