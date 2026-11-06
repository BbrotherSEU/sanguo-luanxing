using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 全局设置：画质 / 音效 / 操作 / 界面四组。
/// 用 PlayerPrefs 存，改完立刻生效，不用重进游戏。
/// 这是 MMO 客户端的标配，玩家至少要能调音量、画质和键位。
/// </summary>
public class Settings
{
    public const string PrefKey = "sanguo.luanxing.settings.v1";

    // ---------------- 画质 ----------------
    public int Width = 1280;
    public int Height = 720;
    public bool FullScreen;
    public int AntiAliasing = 4;      // 0 关 / 2 / 4 / 8
    public bool Shadows = true;
    public int ShadowLevel = 1;        // 0 低 1 中 2 高
    public int EffectDensity = 2;      // 0 关闭 1 少 2 正常 3 多
    public float ViewDistance = 1f;    // 0.6 ~ 1.6，雾的远近 + 相机拉远量
    public int VSync = 1;
    public int TargetFrame = 60;       // 0 不限，否则锁帧

    // ---------------- 音效 ----------------
    public float MasterVolume = 0.8f;
    public float MusicVolume = 0.7f;
    public float SfxVolume = 0.9f;
    public bool MuteInBackground = true;

    // ---------------- 界面 ----------------
    public float UiScale = 1f;         // 0.8 ~ 1.4
    public bool ShowDamageNumbers = true;
    public bool ShowFps;
    public bool ShowSkillCooldownText = true;

    // ---------------- 氛围 ----------------
    public bool DayCycle = true;       // 场景里太阳是否随时间走
    public float DayPhase = -1f;       // 固定时段 0~1，-1 = 用地区默认

    // ---------------- 操作 ----------------
    // 存 KeyCode 的整数值，Unity 不方便直接序列化 enum 数组，用字典存
    public Dictionary<string, int> Keys = new Dictionary<string, int>();

    public static Settings Current;

    public static void Load()
    {
        var s = new Settings();
        string json = PlayerPrefs.GetString(PrefKey, "");
        if (!string.IsNullOrEmpty(json))
        {
            try { JsonUtility.FromJsonOverwrite(json, s); }
            catch { /* 存档损坏就用默认值 */ }
        }
        s.Keys = new Dictionary<string, int>();
        string kj = PlayerPrefs.GetString(PrefKey + ".keys", "");
        if (!string.IsNullOrEmpty(kj))
        {
            foreach (var line in kj.Split('\n'))
            {
                if (line.Length < 3) continue;
                int eq = line.LastIndexOf('=');
                if (eq <= 0) continue;
                int v;
                if (int.TryParse(line.Substring(eq + 1), out v)) s.Keys[line.Substring(0, eq)] = v;
            }
        }
        s.Normalize();
        s.Apply();
        Current = s;
    }

    public void Save()
    {
        // JsonUtility 只序列化字段，Keys 是字典，单独存一份
        var copy = new Settings();
        copy.Width = Width; copy.Height = Height; copy.FullScreen = FullScreen;
        copy.AntiAliasing = AntiAliasing; copy.Shadows = Shadows; copy.ShadowLevel = ShadowLevel;
        copy.EffectDensity = EffectDensity; copy.ViewDistance = ViewDistance;
        copy.VSync = VSync; copy.TargetFrame = TargetFrame;
        copy.MasterVolume = MasterVolume; copy.MusicVolume = MusicVolume; copy.SfxVolume = SfxVolume;
        copy.MuteInBackground = MuteInBackground;
        copy.UiScale = UiScale; copy.ShowDamageNumbers = ShowDamageNumbers; copy.ShowFps = ShowFps;
        copy.ShowSkillCooldownText = ShowSkillCooldownText;
        copy.DayCycle = DayCycle; copy.DayPhase = DayPhase;
        PlayerPrefs.SetString(PrefKey, JsonUtility.ToJson(copy));

        // 键位表：Action=KeyCode 用 \n 分隔
        var sb = new System.Text.StringBuilder();
        foreach (var kv in Keys) sb.Append(kv.Key).Append('=').Append(kv.Value).Append('\n');
        PlayerPrefs.SetString(PrefKey + ".keys", sb.ToString());
        PlayerPrefs.Save();
    }

    void Normalize()
    {
        Width = Mathf.Clamp(Width, 640, 7680);
        Height = Mathf.Clamp(Height, 360, 4320);
        if (AntiAliasing != 0 && AntiAliasing != 2 && AntiAliasing != 4 && AntiAliasing != 8) AntiAliasing = 4;
        ShadowLevel = Mathf.Clamp(ShadowLevel, 0, 2);
        EffectDensity = Mathf.Clamp(EffectDensity, 0, 3);
        ViewDistance = Mathf.Clamp(ViewDistance, 0.6f, 1.6f);
        MasterVolume = Mathf.Clamp01(MasterVolume);
        MusicVolume = Mathf.Clamp01(MusicVolume);
        SfxVolume = Mathf.Clamp01(SfxVolume);
        UiScale = Mathf.Clamp(UiScale, 0.8f, 1.4f);
        TargetFrame = TargetFrame < 0 ? 60 : TargetFrame;
        if (Keys == null) Keys = new Dictionary<string, int>();
    }

    /// <summary>把设置真正作用到引擎上。改任何选项后调用一次。</summary>
    public void Apply()
    {
        QualitySettings.antiAliasing = AntiAliasing;
        QualitySettings.shadows = !Shadows ? UnityEngine.ShadowQuality.Disable
            : ShadowLevel >= 2 ? UnityEngine.ShadowQuality.All
            : ShadowLevel <= 0 ? UnityEngine.ShadowQuality.Disable
            : UnityEngine.ShadowQuality.HardOnly;
        QualitySettings.vSyncCount = VSync;
        Application.targetFrameRate = TargetFrame;
        AudioListener.volume = MasterVolume;
        ApplyResolution();
        ApplyViewDistance();
    }

    /// <summary>视野距离：改雾和远裁剪面，野外场景重建时也会重设一次。</summary>
    public void ApplyViewDistance()
    {
        // WorldBuilder.Lights 会按当前设置重设雾，这里只处理立刻可见的部分
        if (Camera.main == null) return;
        Camera.main.farClipPlane = 220f * ViewDistance;
    }

    /// <summary>分辨率 / 全屏。切全屏会重建窗口，调用后要等一帧才生效。</summary>
    public void ApplyResolution()
    {
        FullScreenMode mode = FullScreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
        if (Screen.fullScreenMode == mode && Screen.width == Width && Screen.height == Height) return;
        Screen.SetResolution(Width, Height, mode);
    }

    // ---------------- 键位 ----------------

    public KeyCode Key(string action, KeyCode fallback)
    {
        int v;
        if (Keys.TryGetValue(action, out v) && v > 0) return (KeyCode)v;
        return fallback;
    }

    public void SetKey(string action, KeyCode k)
    {
        Keys[action] = (int)k;
    }

    public void ResetKeys(Dictionary<string, KeyCode> defaults)
    {
        Keys.Clear();
        foreach (var kv in defaults) Keys[kv.Key] = (int)kv.Value;
    }
}
