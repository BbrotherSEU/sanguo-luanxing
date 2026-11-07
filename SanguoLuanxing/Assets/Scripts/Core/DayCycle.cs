using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 昼夜循环：驱动太阳角度 / 光色 / 强度，以及天空色与雾色。
/// 时间是抽象的「一天进度」0~1，可以挂到设置里手动拖，也可以在世界里自动流逝。
/// </summary>
public class DayCycle
{
    public Theme Theme;
    public Light Sun;
    public Camera Cam;
    /// <summary>天空穹顶材质：靠 tint 跟着时段变色</summary>
    public Material SkyMat;
    /// <summary>云朵网格的材质列表：跟着时段染暖色</summary>
    public List<Material> CloudMats;
    /// <summary>
    /// 接地阴影材质：强度跟着太阳走。
    /// 夜里没有直射光，影子就该淡到几乎没有 —— 否则夜景里会看到一堆死黑的斑。
    /// </summary>
    public Material ShadowMat;
    /// <summary>
    /// 需要吃方向性环境光的材质（地面/建筑/石头等）。
    /// 天空与云不加入：它们已经由 tint 按时段染过一遍，再叠环境光会二次染色。
    /// </summary>
    public List<Material> AmbMats;

    /// <summary>当前一天进度：0=午夜 0.25=日出 0.5=正午 0.75=日落</summary>
    public float Time01 = 0.32f;
    /// <summary>自动流逝：一整个昼夜的秒数，<=0 表示静止</summary>
    public float DayLength = 240f;
    /// <summary>是否跟随场景自动流逝</summary>
    public bool Auto = true;

    // 一天里的关键光照参数
    static readonly Color[] SkyKeys =
    {
        new Color(0.10f, 0.13f, 0.24f),   // 夜
        new Color(0.42f, 0.34f, 0.42f),   // 黎明前
        new Color(0.86f, 0.62f, 0.48f),   // 日出
        new Color(0.66f, 0.84f, 0.96f),   // 上午
        new Color(0.62f, 0.82f, 0.95f),   // 正午
        new Color(0.78f, 0.72f, 0.78f),   // 午后偏暖
        new Color(0.92f, 0.52f, 0.34f),   // 日落
        new Color(0.24f, 0.22f, 0.38f),   // 暮
    };
    static readonly float[] SkyKeyT = { 0f, 0.20f, 0.26f, 0.38f, 0.50f, 0.66f, 0.76f, 0.84f };

    public void Update(float dt)
    {
        if (!Auto || DayLength <= 0f) return;
        // 累加在 Time01 上，而不是另起一个从 0 起步的累加器。
        // 另起累加器会把外部设好的 Time01（WorldBuilder 给了 0.30 = 上午）冲掉，
        // 场景每次开局都从午夜 0 起步：太阳几乎没光，整个地面被压成近黑。
        Time01 = Mathf.Repeat(Time01 + dt / Mathf.Max(1f, DayLength), 1f);
        Apply();
    }

    /// <summary>
    /// 天空地平线处的颜色（场景烘出来的）。
    /// 雾色直接由它派生 —— 远处的地面才会和天空无缝接上。
    /// </summary>
    public Color Horizon = new Color(0.8f, 0.88f, 0.95f);

    /// <summary>把当前时间的光照写到引擎上</summary>
    public void Apply()
    {
        float u = Time01;
        Color sky = SampleSky(u);
        // 雾色 = 地平线色。天空穹顶在地平线附近就是这个颜色，
        // 地面远处淡入这个颜色，两者才接得上。
        // 之前雾色是 Lerp(天空色, Theme.Fog, 0.35)，和地平线色不是一回事，
        // 于是地面和天空之间有一道可见的色差带。
        Color fog = Color.Lerp(Horizon, sky, 0.30f);

        // 太阳高度：日出日落时贴近地平（阴影拉长），正午最高
        float elev = Mathf.Sin((u - 0.25f) * Mathf.PI * 2f) * 0.5f + 0.5f;   // 0..1
        float azim = Mathf.Lerp(-35f, 35f, (u - 0.25f) * 2f);
        float height = Mathf.Lerp(6f, 62f, elev);
        if (Sun != null)
        {
            Sun.transform.rotation = Quaternion.Euler(height, azim, 0f);
            // 日出日落偏暖，正午偏白；夜里几乎没光
            float dayness = Mathf.SmoothStep(0f, 1f, elev * 1.6f);
            Sun.color = Color.Lerp(new Color(1f, 0.62f, 0.38f), Theme != null ? Theme.Light : Color.white, dayness);
            float baseI = Theme != null ? Theme.LightIntensity : 1.1f;
            Sun.intensity = baseI * Mathf.Lerp(0.06f, 1.12f, dayness);
            Sun.enabled = Sun.intensity > 0.02f;

            // 接地阴影跟着太阳强度走：夜里没直射光，影子淡到几乎没有。
            if (ShadowMat != null && ShadowMat.HasProperty("_Strength"))
                ShadowMat.SetFloat("_Strength", Mathf.Clamp01(Sun.intensity / Mathf.Max(0.05f, baseI * 1.12f)) * 0.92f);
        }

        // 环境光：天顶取天空色，地面取阴影色
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        Color ambSky = Color.Lerp(sky, Color.white, 0.18f);
        Color ambEq = Color.Lerp(fog, Theme != null ? Theme.GrassA : Color.gray, 0.30f) * 0.82f;
        Color ambGnd = (Theme != null ? Theme.ShadowTint : new Color(0.4f, 0.4f, 0.6f)) * 0.50f;
        RenderSettings.ambientSkyColor = ambSky;
        RenderSettings.ambientEquatorColor = ambEq;
        RenderSettings.ambientGroundColor = ambGnd;

        // 同样的三色写进材质：Toon 着色器不读引擎的 ambient 全局量
        // （内置管线走球谐，跟我们算的 Trilight 三色不是一个东西），
        // 自己传最稳，也方便按材质微调。
        if (AmbMats != null)
        {
            for (int i = 0; i < AmbMats.Count; i++)
            {
                var m = AmbMats[i];
                if (m == null) continue;
                if (m.HasProperty("_AmbSky")) m.SetColor("_AmbSky", ambSky);
                if (m.HasProperty("_AmbEq")) m.SetColor("_AmbEq", ambEq);
                if (m.HasProperty("_AmbGnd")) m.SetColor("_AmbGnd", ambGnd);
            }
        }

        RenderSettings.fogColor = fog;
        if (Cam != null) Cam.backgroundColor = sky;

        // 天空穹顶：顶点色是烘死的，用 tint 把整片天空推到当前时段的色调
        if (SkyMat != null)
        {
            // 目标色 / 当前烘焙色的比值，烘焙色取 Theme.Sky（近地平线处）
            Color baked = Theme != null ? Theme.Sky : Color.white;
            Color tint = new Color(
                Mathf.Clamp(sky.r / Mathf.Max(baked.r, 0.01f), 0.15f, 2.2f),
                Mathf.Clamp(sky.g / Mathf.Max(baked.g, 0.01f), 0.15f, 2.2f),
                Mathf.Clamp(sky.b / Mathf.Max(baked.b, 0.01f), 0.15f, 2.2f), 1f);
            if (SkyMat.HasProperty("_Color")) SkyMat.SetColor("_Color", tint);
        }
        // 云：日出日落时染成暖色
        if (CloudMats != null)
        {
            float warm = 1f - Mathf.Abs(elev - 0.5f) * 2f;      // 0 顶 1 地平
            warm = Mathf.Clamp01(warm);
            Color cloudTint = Color.Lerp(Color.white, new Color(1f, 0.72f, 0.55f), warm * 0.75f);
            for (int i = 0; i < CloudMats.Count; i++)
            {
                var m = CloudMats[i];
                if (m != null && m.HasProperty("_Color")) m.SetColor("_Color", cloudTint);
            }
        }
    }

    /// <summary>在关键帧之间插值出天空色</summary>
    public static Color SampleSky(float u)
    {
        u = Mathf.Repeat(u, 1f);
        for (int i = 0; i < SkyKeyT.Length - 1; i++)
        {
            if (u >= SkyKeyT[i] && u <= SkyKeyT[i + 1])
            {
                float k = (u - SkyKeyT[i]) / Mathf.Max(0.0001f, SkyKeyT[i + 1] - SkyKeyT[i]);
                return Color.Lerp(SkyKeys[i], SkyKeys[i + 1], k);
            }
        }
        return SkyKeys[0];
    }

    /// <summary>把时间调到某个时段：0=夜 0.25=日出 0.5=正午 0.75=日落</summary>
    public void SetPhase(float phase01)
    {
        Time01 = Mathf.Repeat(phase01, 1f);
        Auto = false;
        Apply();
    }
}
