using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 设置面板：画质 / 音效 / 操作 / 界面。
/// 全部用 IMGUI 手绘，和工程现有 UI 风格一致。
/// 改动即时生效；点"应用并保存"才落盘，"恢复默认"只改内存。
/// </summary>
public static class SettingsUI
{
    public static bool Open;
    public static int Tab; // 0画质 1音效 2操作 3界面

    static readonly string[] Tabs = { "画 质", "音 效", "操 作", "界 面", "氛 围" };
    static readonly string[] PhaseNames = { "深夜", "黎明", "日出", "上午", "正午", "午后", "日落", "入夜" };
    static readonly float[] PhaseValues = { 0.02f, 0.22f, 0.26f, 0.38f, 0.50f, 0.66f, 0.76f, 0.86f };
    static readonly string[] QualityNames = { "关闭", "2x", "4x", "8x" };
    static readonly int[] QualityValues = { 0, 2, 4, 8 };
    static readonly string[] ShadowNames = { "低", "中", "高" };
    static readonly string[] EffectNames = { "关闭", "少", "正常", "多" };
    static readonly string[] ResNames = { "1280 x 720", "1600 x 900", "1920 x 1080", "2560 x 1440", "窗口自适应" };
    static readonly int[] ResW = { 1280, 1600, 1920, 2560, 0 };
    static readonly int[] ResH = { 720, 900, 1080, 1440, 0 };

    // 操作页的键位表（动作名 -> 默认键）
    public const string ActMoveUp = "move_up";
    public const string ActMoveDown = "move_down";
    public const string ActMoveLeft = "move_left";
    public const string ActMoveRight = "move_right";
    public const string ActAttack = "attack";
    public const string ActHeavy = "heavy";
    public const string ActSkill1 = "skill1";
    public const string ActSkill2 = "skill2";
    public const string ActSkill3 = "skill3";
    public const string ActDash = "dash";
    public const string ActInteract = "interact";
    public const string ActMap = "map";
    public const string ActPanel = "panel";

    public static readonly Dictionary<string, KeyCode> Defaults = new Dictionary<string, KeyCode>
    {
        { ActMoveUp, KeyCode.W },
        { ActMoveDown, KeyCode.S },
        { ActMoveLeft, KeyCode.A },
        { ActMoveRight, KeyCode.D },
        { ActAttack, KeyCode.J },
        { ActHeavy, KeyCode.K },
        { ActSkill1, KeyCode.Q },
        { ActSkill2, KeyCode.E },
        { ActSkill3, KeyCode.R },
        { ActDash, KeyCode.Space },
        { ActInteract, KeyCode.L },
        { ActMap, KeyCode.M },
        { ActPanel, KeyCode.C },
    };

    static readonly string[] ActionNames = { ActMoveUp, ActMoveDown, ActMoveLeft, ActMoveRight, ActAttack, ActHeavy, ActSkill1, ActSkill2, ActSkill3, ActDash, ActInteract, ActMap, ActPanel };
    static readonly string[] ActionLabels =
    {
        "移动 上", "移动 下", "移动 左", "移动 右",
        "普通攻击", "重攻击", "技能 一", "技能 二", "技能 三",
        "翻滚 / 闪避", "交互 / 拾取", "天下地图", "角色面板",
    };

    static int capturing = -1;   // 正在等玩家按新键的动作索引，-1 = 没在捕获

    public static void OpenPanel()
    {
        Open = true;
        capturing = -1;
    }

    public static void Close()
    {
        Open = false;
        capturing = -1;
    }

    // 捕获按键：面板开着的时候由 Game 转发 Input
    public static bool CaptureKey(KeyCode k)
    {
        if (capturing < 0) return false;
        if (k == KeyCode.Escape) { capturing = -1; return true; }
        if (Settings.Current != null && capturing < ActionNames.Length)
        {
            Settings.Current.SetKey(ActionNames[capturing], k);
            Settings.Current.Save();
        }
        capturing = -1;
        return true;
    }

    public static void Draw(float W, float CX, System.Action<Rect, string, int, Color, TextAnchor, bool> label,
        System.Action<Rect, Color> fill, System.Action<Rect, string, int, Color, TextAnchor, bool> button)
    {
        if (!Open) return;
        var s = Settings.Current;
        if (s == null) return;

        fill(new Rect(0, 0, W, 720), new Color(0.02f, 0.02f, 0.018f, 0.82f));

        float pw = 760f, ph = 560f;
        float px = CX - pw * 0.5f, py = 80f;
        Color frame = new Color(0.72f, 0.58f, 0.28f, 1f);
        UiKit.Panel(fill, new Rect(px, py, pw, ph), new Color(0.10f, 0.09f, 0.075f, 0.97f), frame);

        label(new Rect(px + 26f, py + 18f, 400f, 40f), "设 置", 30, new Color(0.95f, 0.88f, 0.70f), TextAnchor.MiddleLeft, false);

        // 页签
        for (int i = 0; i < Tabs.Length; i++)
        {
            Rect r = new Rect(px + 26f + i * 92f, py + 66f, 88f, 34f);
            bool on = Tab == i;
            fill(r, on ? new Color(0.30f, 0.24f, 0.13f, 0.95f) : new Color(0.15f, 0.14f, 0.12f, 0.8f));
            if (on)
            {
                fill(new Rect(r.x, r.y, r.width, 2f), new Color(0.72f, 0.58f, 0.28f, 0.95f));
                // 选中页签两侧加回纹短线，和整体边框呼应
                fill(new Rect(r.x - 4f, r.y + 4f, 2f, r.height - 8f), new Color(0.72f, 0.58f, 0.28f, 0.5f));
                fill(new Rect(r.xMax + 2f, r.y + 4f, 2f, r.height - 8f), new Color(0.72f, 0.58f, 0.28f, 0.5f));
            }
            label(r, Tabs[i], 18, on ? new Color(0.98f, 0.90f, 0.68f) : new Color(0.70f, 0.66f, 0.58f), TextAnchor.MiddleCenter, false);
        }
        UiKit.Divider(fill, new Rect(px + 26f, py + 108f, pw - 52f, 0f), new Color(0.72f, 0.58f, 0.28f, 0.5f));

        float cx = px + 40f, cy = py + 116f;
        switch (Tab)
        {
            case 0: DrawQuality(s, cx, cy, label, fill); break;
            case 1: DrawAudio(s, cx, cy, label, fill); break;
            case 2: DrawKeys(s, cx, cy, label, fill); break;
            case 3: DrawInterface(s, cx, cy, label, fill); break;
            default: DrawAmbience(s, cx, cy, label, fill); break;
        }

        // 底部：恢复默认 / 恢复键位 / 关闭
        Rect rb = new Rect(px + 40f, py + ph - 62f, 150f, 42f);
        fill(rb, new Color(0.16f, 0.15f, 0.13f, 0.95f));
        UiKit.Edge(fill, rb, new Color(0.72f, 0.58f, 0.28f, 0.45f), 1f);
        label(rb, "恢复默认", 17, new Color(0.80f, 0.76f, 0.66f), TextAnchor.MiddleCenter, false);

        Rect kb = new Rect(px + 202f, py + ph - 62f, 150f, 42f);
        fill(kb, new Color(0.16f, 0.15f, 0.13f, 0.95f));
        UiKit.Edge(fill, kb, new Color(0.72f, 0.58f, 0.28f, 0.45f), 1f);
        label(kb, "恢复键位", 17, new Color(0.80f, 0.76f, 0.66f), TextAnchor.MiddleCenter, false);

        Rect cb = new Rect(px + pw - 200f, py + ph - 62f, 160f, 42f);
        fill(cb, new Color(0.34f, 0.26f, 0.13f, 0.95f));
        UiKit.Edge(fill, cb, new Color(0.82f, 0.66f, 0.32f, 0.8f), 1f);
        label(cb, "关闭", 18, new Color(0.98f, 0.90f, 0.70f), TextAnchor.MiddleCenter, false);
    }

    // ---------------- 画质 ----------------
    static void DrawQuality(Settings s, float x, float y,
        System.Action<Rect, string, int, Color, TextAnchor, bool> label, System.Action<Rect, Color> fill)
    {
        float ry = y;
        // 分辨率
        int res = 4;
        for (int i = 0; i < ResNames.Length; i++) if (ResW[i] == s.Width && ResH[i] == s.Height) res = i;
        ry = Row(label, fill, x, ry, "分辨率", ResNames[res]);
        ry = Row(label, fill, x, ry, "显示模式", s.FullScreen ? "全屏" : "窗口");
        ry = Row(label, fill, x, ry, "抗锯齿", QualityNames[IndexOf(QualityValues, s.AntiAliasing)]);
        ry = Row(label, fill, x, ry, "阴影", s.Shadows ? ShadowNames[Mathf.Clamp(s.ShadowLevel, 0, 2)] : "关闭");
        ry = Row(label, fill, x, ry, "特效密度", EffectNames[s.EffectDensity]);
        ry = Row(label, fill, x, ry, "视野距离", Mathf.RoundToInt(s.ViewDistance * 100f) + "%");
        ry = Row(label, fill, x, ry, "垂直同步", s.VSync > 0 ? "开启" : "关闭");
        ry = Row(label, fill, x, ry, "帧率上限", s.TargetFrame <= 0 ? "不限" : s.TargetFrame + " FPS");
    }

    // ---------------- 音效 ----------------
    static void DrawAudio(Settings s, float x, float y,
        System.Action<Rect, string, int, Color, TextAnchor, bool> label, System.Action<Rect, Color> fill)
    {
        float ry = y;
        ry = Bar(label, fill, x, ry, "主音量", s.MasterVolume, v => { s.MasterVolume = v; AudioListener.volume = v; });
        ry = Bar(label, fill, x, ry, "音乐", s.MusicVolume, v => s.MusicVolume = v);
        ry = Bar(label, fill, x, ry, "音效", s.SfxVolume, v => s.SfxVolume = v);
        ry = Row(label, fill, x, ry, "后台静音", s.MuteInBackground ? "开启" : "关闭");
    }

    // ---------------- 界面 ----------------
    static void DrawInterface(Settings s, float x, float y,
        System.Action<Rect, string, int, Color, TextAnchor, bool> label, System.Action<Rect, Color> fill)
    {
        float ry = y;
        ry = Bar(label, fill, x, ry, "界面缩放", (s.UiScale - 0.8f) / 0.6f, v => s.UiScale = 0.8f + v * 0.6f);
        ry = Row(label, fill, x, ry, "伤害数字", s.ShowDamageNumbers ? "显示" : "隐藏");
        ry = Row(label, fill, x, ry, "冷却数字", s.ShowSkillCooldownText ? "显示" : "隐藏");
        ry = Row(label, fill, x, ry, "帧率显示", s.ShowFps ? "显示" : "隐藏");
    }

    // ---------------- 氛围 ----------------
    static void DrawAmbience(Settings s, float x, float y,
        System.Action<Rect, string, int, Color, TextAnchor, bool> label, System.Action<Rect, Color> fill)
    {
        float ry = y;
        ry = Row(label, fill, x, ry, "昼夜流转", s.DayCycle ? "开启" : "固定");
        int pi = 0;
        for (int i = 0; i < PhaseValues.Length; i++)
            if (Mathf.Abs(s.DayPhase - PhaseValues[i]) < 0.06f) pi = i;
        ry = Row(label, fill, x, ry, "时段", s.DayCycle ? "自动流转" : PhaseNames[pi]);
        label(new Rect(x, ry, 520f, 60f),
            "「固定」时太阳停住，点时段可逐段切换到深夜 / 日出 / 正午 / 日落等光照。\n「新的一天」会重新开始流转。",
            14, new Color(0.56f, 0.53f, 0.48f), TextAnchor.UpperLeft, false);
    }

    // ---------------- 操作 ----------------
    static void DrawKeys(Settings s, float x, float y,
        System.Action<Rect, string, int, Color, TextAnchor, bool> label, System.Action<Rect, Color> fill)
    {
        float ry = y;
        for (int i = 0; i < ActionNames.Length; i++)
        {
            KeyCode k = s.Key(ActionNames[i], Defaults[ActionNames[i]]);
            label(new Rect(x, ry, 280f, 32f), ActionLabels[i], 17, new Color(0.74f, 0.70f, 0.62f), TextAnchor.MiddleLeft, false);
            Rect lr = new Rect(x + 300f, ry, 220f, 32f);
            bool listening = capturing == i;
            fill(lr, listening ? new Color(0.42f, 0.32f, 0.14f, 0.95f) : new Color(0.16f, 0.15f, 0.13f, 0.85f));
            label(lr, listening ? "按任意键…" : KeyName(k), 17,
                listening ? new Color(0.98f, 0.90f, 0.66f) : new Color(0.86f, 0.82f, 0.72f), TextAnchor.MiddleCenter, false);
            ry += 34f;
        }
        label(new Rect(x, ry + 6f, 520f, 24f), "点一下右侧按键框，再按新键即可改键", 14,
            new Color(0.56f, 0.53f, 0.48f), TextAnchor.MiddleLeft, false);
    }

    // ---------------- 通用行 ----------------
    static float Row(System.Action<Rect, string, int, Color, TextAnchor, bool> label, System.Action<Rect, Color> fill,
        float x, float y, string name, string value)
    {
        label(new Rect(x, y, 280f, 32f), name, 18, new Color(0.74f, 0.70f, 0.62f), TextAnchor.MiddleLeft, false);
        Rect r = new Rect(x + 300f, y, 220f, 32f);
        fill(r, new Color(0.16f, 0.15f, 0.13f, 0.85f));
        label(r, value, 17, new Color(0.90f, 0.86f, 0.76f), TextAnchor.MiddleCenter, false);
        return y + 38f;
    }

    static float Bar(System.Action<Rect, string, int, Color, TextAnchor, bool> label, System.Action<Rect, Color> fill,
        float x, float y, string name, float t, System.Action<float> set)
    {
        label(new Rect(x, y, 280f, 32f), name, 18, new Color(0.74f, 0.70f, 0.62f), TextAnchor.MiddleLeft, false);
        Rect bg = new Rect(x + 300f, y + 9f, 220f, 14f);
        fill(bg, new Color(0.10f, 0.10f, 0.09f, 0.9f));
        fill(new Rect(bg.x, bg.y, bg.width * Mathf.Clamp01(t), bg.height), new Color(0.72f, 0.56f, 0.26f, 0.95f));
        fill(new Rect(bg.x + bg.width * Mathf.Clamp01(t) - 2f, bg.y - 3f, 4f, bg.height + 6f), new Color(0.95f, 0.86f, 0.60f));
        label(new Rect(x + 530f, y, 90f, 32f), Mathf.RoundToInt(t * 100f) + "%", 16, new Color(0.86f, 0.82f, 0.72f), TextAnchor.MiddleLeft, false);
        return y + 38f;
    }

    static int IndexOf(int[] arr, int v)
    {
        for (int i = 0; i < arr.Length; i++) if (arr[i] == v) return i;
        return 0;
    }

    public static string KeyName(KeyCode k)
    {
        switch (k)
        {
            case KeyCode.Space: return "空格";
            case KeyCode.Return: return "回车";
            case KeyCode.Escape: return "Esc";
            case KeyCode.LeftShift: return "左Shift";
            case KeyCode.RightShift: return "右Shift";
            case KeyCode.LeftControl: return "左Ctrl";
            case KeyCode.LeftAlt: return "左Alt";
            case KeyCode.Tab: return "Tab";
            case KeyCode.Alpha1: return "1";
            case KeyCode.Alpha2: return "2";
            case KeyCode.Alpha3: return "3";
            case KeyCode.Alpha4: return "4";
            case KeyCode.Alpha5: return "5";
            case KeyCode.Alpha6: return "6";
            case KeyCode.Alpha7: return "7";
            case KeyCode.Alpha8: return "8";
            case KeyCode.Alpha9: return "9";
            case KeyCode.Alpha0: return "0";
            case KeyCode.UpArrow: return "↑";
            case KeyCode.DownArrow: return "↓";
            case KeyCode.LeftArrow: return "←";
            case KeyCode.RightArrow: return "→";
        }
        return k.ToString();
    }

    // ---------------- 点击 ----------------
    public static int Click(Vector2 mp, float W, float CX)
    {
        if (!Open) return -1;
        var s = Settings.Current;
        float pw = 760f, ph = 560f;
        float px = CX - pw * 0.5f, py = 80f;

        // 页签
        for (int i = 0; i < Tabs.Length; i++)
        {
            if (new Rect(px + 26f + i * 92f, py + 66f, 88f, 34f).Contains(mp)) { Tab = i; capturing = -1; return -1; }
        }

        // 底部按钮
        if (new Rect(px + 40f, py + ph - 62f, 150f, 42f).Contains(mp))
        {
            ResetDefaults(s);
            s.Save();
            return -1;
        }
        if (new Rect(px + 202f, py + ph - 62f, 150f, 42f).Contains(mp))
        {
            s.ResetKeys(Defaults);
            s.Save();
            return -1;
        }
        if (new Rect(px + pw - 200f, py + ph - 62f, 160f, 42f).Contains(mp))
        {
            s.Save();
            Close();
            return -2;
        }

        // 各页的值控件
        float cx = px + 40f, cy = py + 116f;
        switch (Tab)
        {
            case 0: return ClickQuality(s, mp, cx, cy);
            case 1: return ClickAudio(s, mp, cx, cy);
            case 2: return ClickKeys(mp, cx, cy);
            case 3: return ClickInterface(s, mp, cx, cy);
            default: return ClickAmbience(s, mp, cx, cy);
        }
    }

    static int ClickAmbience(Settings s, Vector2 mp, float x, float y)
    {
        // 昼夜开关
        if (new Rect(x + 300f, y + RowY(0), 220f, 32f).Contains(mp))
        {
            s.DayCycle = !s.DayCycle;
            if (!s.DayCycle && s.DayPhase < 0f) s.DayPhase = 0.30f;
            ApplyAmbience();
            return -1;
        }
        // 时段切换
        if (new Rect(x + 300f, y + RowY(1), 220f, 32f).Contains(mp))
        {
            s.DayCycle = false;
            int pi = 0;
            for (int i = 0; i < PhaseValues.Length; i++)
                if (Mathf.Abs(s.DayPhase - PhaseValues[i]) < 0.06f) pi = i;
            pi = (pi + 1) % PhaseValues.Length;
            s.DayPhase = PhaseValues[pi];
            ApplyAmbience();
            return -1;
        }
        return -1;
    }

    /// <summary>改了时段就让当前场景立刻跟着变</summary>
    static void ApplyAmbience()
    {
        if (Game.I != null && Game.I.World != null) Game.I.World.RefreshAmbience();
    }

    static float RowY(int i) { return 116f + 38f * i; }

    static int ClickQuality(Settings s, Vector2 mp, float x, float y)
    {
        for (int i = 0; i < 8; i++)
        {
            Rect r = new Rect(x + 300f, y + RowY(i), 220f, 32f);
            if (!r.Contains(mp)) continue;
            switch (i)
            {
                case 0:
                    {
                        int res = 4;
                        for (int k = 0; k < ResNames.Length; k++) if (ResW[k] == s.Width && ResH[k] == s.Height) res = k;
                        res = (res + 1) % ResNames.Length;
                        if (ResW[res] > 0) { s.Width = ResW[res]; s.Height = ResH[res]; s.ApplyResolution(); }
                        break;
                    }
                case 1: s.FullScreen = !s.FullScreen; s.ApplyResolution(); break;
                case 2:
                    {
                        int[] aa = { 0, 2, 4, 8 };
                        int idx = IndexOf(QualityValues, s.AntiAliasing);
                        s.AntiAliasing = aa[(idx + 1) % aa.Length];
                        QualitySettings.antiAliasing = s.AntiAliasing;
                        break;
                    }
                case 3:
                    if (!s.Shadows) { s.Shadows = true; }
                    else if (s.ShadowLevel < 2) s.ShadowLevel++;
                    else { s.Shadows = false; s.ShadowLevel = 0; }
                    s.Apply();
                    break;
                case 4: s.EffectDensity = (s.EffectDensity + 1) % 4; break;
                case 5:
                    s.ViewDistance = s.ViewDistance >= 1.59f ? 0.6f : s.ViewDistance + 0.25f;
                    break;
                case 6: s.VSync = s.VSync > 0 ? 0 : 1; s.Apply(); break;
                case 7:
                    int[] fps = { 30, 60, 0 };
                    int fi = 0;
                    if (s.TargetFrame == 60) fi = 1; else if (s.TargetFrame == 0) fi = 2;
                    s.TargetFrame = fps[(fi + 1) % fps.Length];
                    s.Apply();
                    break;
            }
            return -1;
        }
        return -1;
    }

    static int ClickAudio(Settings s, Vector2 mp, float x, float y)
    {
        for (int i = 0; i < 4; i++)
        {
            Rect r = new Rect(x + 300f, y + RowY(i), 220f, 32f);
            if (!r.Contains(mp)) continue;
            if (i == 3) s.MuteInBackground = !s.MuteInBackground;
            return -1;
        }
        // 音量条：点击位置直接设定
        for (int i = 0; i < 3; i++)
        {
            Rect bg = new Rect(x + 300f, y + RowY(i) + 9f, 220f, 14f);
            if (!bg.Contains(mp)) continue;
            float t = Mathf.Clamp01((mp.x - bg.x) / bg.width);
            if (i == 0) { s.MasterVolume = t; AudioListener.volume = t; }
            else if (i == 1) s.MusicVolume = t;
            else s.SfxVolume = t;
            return -1;
        }
        return -1;
    }

    static int ClickInterface(Settings s, Vector2 mp, float x, float y)
    {
        for (int i = 0; i < 3; i++)
        {
            Rect r = new Rect(x + 300f, y + RowY(i), 220f, 32f);
            if (!r.Contains(mp)) continue;
            if (i == 0) s.ShowDamageNumbers = !s.ShowDamageNumbers;
            else if (i == 1) s.ShowSkillCooldownText = !s.ShowSkillCooldownText;
            else s.ShowFps = !s.ShowFps;
            return -1;
        }
        // UI 缩放条
        Rect bg = new Rect(x + 300f, y + RowY(0) + 9f, 220f, 14f);
        if (bg.Contains(mp)) { s.UiScale = 0.8f + Mathf.Clamp01((mp.x - bg.x) / bg.width) * 0.6f; return -1; }
        return -1;
    }

    static int ClickKeys(Vector2 mp, float x, float y)
    {
        for (int i = 0; i < ActionNames.Length; i++)
        {
            if (new Rect(x + 300f, y + i * 34f, 220f, 32f).Contains(mp))
            {
                capturing = i;
                return -1;
            }
        }
        return -1;
    }

    static void ResetDefaults(Settings s)
    {
        // 键位不动 —— 玩家专门改过的东西不该被"恢复默认"顺手抹掉
        s.Width = 1280; s.Height = 720; s.FullScreen = false;
        s.AntiAliasing = 4; s.Shadows = true; s.ShadowLevel = 1;
        s.EffectDensity = 2; s.ViewDistance = 1f; s.VSync = 1; s.TargetFrame = 60;
        s.MasterVolume = 0.8f; s.MusicVolume = 0.7f; s.SfxVolume = 0.9f; s.MuteInBackground = true;
        s.UiScale = 1f; s.ShowDamageNumbers = true; s.ShowFps = false; s.ShowSkillCooldownText = true;
        s.DayCycle = true; s.DayPhase = -1f;
        s.Apply();
    }
}
