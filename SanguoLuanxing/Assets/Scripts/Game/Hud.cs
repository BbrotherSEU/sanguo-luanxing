using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 运行时 HUD：气血/气力/精力、技能冷却、Boss 血条、飘字、提示。
/// 用 IMGUI 是为了零资源依赖；换成 uGUI 只需替换 Draw 的实现。
/// </summary>
public class Hud
{
    class Pop
    {
        public Vector3 World;
        public int Value;
        public float T, Max, Drift;
        public string Style;
    }

    Game G;
    Font font;
    GUIStyle sLabel, sSmall, sBig, sCenter, sTitle;

    float toastT;
    string toastText = "";
    float flashA;
    Color flashCol = Color.white;
    EnemyCtrl boss;
    readonly List<Pop> pops = new List<Pop>();

    public Font Font => font;
    public float Scale { get; private set; } = 1f;
    float CX => Screen.width / Scale * 0.5f;
    float W => Screen.width / Scale;

    public void Init(Game g)
    {
        G = g;
        font = Font.CreateDynamicFontFromOSFont(
            new[] { "Microsoft YaHei", "微软雅黑", "SimHei", "黑体", "PingFang SC", "SimSun" }, 20);
        if (font == null) Debug.LogWarning("[Hud] 未找到中文字体，退化为默认字体");
    }

    public void Toast(string msg)
    {
        toastText = msg;
        toastT = 1.9f;
    }

    public void SetBoss(EnemyCtrl e) => boss = e;

    /// <summary>全屏闪色：暴击金闪 / 受伤红闪</summary>
    public void Flash(Color c, float a)
    {
        flashCol = c;
        flashA = Mathf.Max(flashA, a);
    }

    void DrawFlash()
    {
        if (flashA <= 0f) return;
        flashA = Mathf.Max(0f, flashA - Time.unscaledDeltaTime * 3.4f);
        Fill(new Rect(0, 0, W, 720), new Color(flashCol.r, flashCol.g, flashCol.b, flashA));
    }

    public void DamageAt(Vector3 world, int value, string style)
    {
        if (pops.Count > 48) pops.RemoveAt(0);
        pops.Add(new Pop
        {
            World = world, Value = value, T = 0.95f, Max = 0.95f,
            Drift = Util.Rand(-28f, 28f), Style = style,
        });
    }

    public void Sync(Battle b)
    {
        float dt = Time.unscaledDeltaTime;
        if (toastT > 0f) toastT -= dt;
        for (int i = pops.Count - 1; i >= 0; i--)
        {
            pops[i].T -= dt;
            if (pops[i].T <= 0f) pops.RemoveAt(i);
        }
        if (b != null && b.Boss != null && b.Boss.Destroyed) boss = null;
    }

    // ---------------- 绘制 ----------------

    public void Draw(Battle b)
    {
        EnsureStyles();
        Scale = Screen.height / 720f;
        GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(Scale, Scale, 1f));
        GUI.skin.font = font;

        DrawFlash();
        DrawInfo(b);
        DrawVitals(b.Player);
        DrawSkills(b.Player);
        DrawBoss();
        if (!TouchCtl.Active) DrawHints();   // 触屏时不占屏幕显示键盘提示
        DrawPops();
        DrawToast();
        // 不重置 GUI.matrix：调用方（Game.OnGUI）会在同一缩放坐标系里继续画剧情/结算层
        GUI.color = Color.white;
    }

    void EnsureStyles()
    {
        if (sLabel != null) return;
        sLabel = new GUIStyle(GUI.skin.label) { fontSize = 16, alignment = TextAnchor.MiddleLeft };
        sLabel.normal.textColor = new Color(0.94f, 0.91f, 0.84f);
        sSmall = new GUIStyle(sLabel) { fontSize = 13, alignment = TextAnchor.MiddleCenter };
        sBig = new GUIStyle(sLabel) { fontSize = 22, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
        sCenter = new GUIStyle(sLabel) { fontSize = 15, alignment = TextAnchor.MiddleCenter };
        sTitle = new GUIStyle(sLabel) { fontSize = 54, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
    }

    void DrawInfo(Battle b)
    {
        var d = G.Progress.Data;
        var map = b != null && b.Map != null ? b.Map : G.CurrentMap;
        string head = map.Chapter + "  " + map.Name;
        if (b != null && b.IsCollectQuest)
            head = "任务 · " + b.ActiveQuest.Title;
        Text(new Rect(24, 18, 420, 26), head, sLabel, new Color(0.98f, 0.93f, 0.80f));
        Text(new Rect(24, 44, 300, 22), $"{G.Progress.Faction?.Name ?? "无"}  ·  等级 {d.level}", sLabel);

        float expRatio = d.level > 0 ? Mathf.Clamp01((float)d.exp / Mathf.Max(1, G.Progress.ExpNeeded())) : 0f;
        Fill(new Rect(24, 70, 200, 6), new Color(0.1f, 0.09f, 0.07f, 0.85f));
        Fill(new Rect(24, 70, 200 * expRatio, 6), new Color(0.72f, 0.62f, 0.32f));

        // 仕途：官阶 + 声望进度。小人物往上走的刻度条。
        DrawCareer(d);

        if (b != null && b.IsCollectQuest)
            Text(new Rect(232, 64, 320, 20),
                $"收集 {b.QuestCollectCount}/{b.ActiveQuest.CollectNeed}　铜钱 {d.gold}",
                sLabel, new Color(0.95f, 0.88f, 0.55f));
        else
            Text(new Rect(232, 64, 320, 20),
                $"铜钱 {d.gold}   属性点 {d.attrPts}   斩敌 {d.totalKills}",
                sLabel, new Color(0.9f, 0.86f, 0.74f));
    }

    /// <summary>仕途条：官阶名 + 到下一阶的声望进度</summary>
    void DrawCareer(SaveData d)
    {
        var rank = Career.Current(G.Progress);
        if (rank == null) return;
        var next = Career.Next(G.Progress);
        // 官阶徽记
        Fill(new Rect(24, 84, 76, 22), new Color(0.16f, 0.13f, 0.08f, 0.9f));
        UiKit.Edge(Fill, new Rect(24, 84, 76, 22), new Color(0.72f, 0.58f, 0.28f, 0.7f), 1f);
        Text(new Rect(24, 84, 76, 22), rank.Title, sSmall, new Color(0.98f, 0.88f, 0.62f), TextAnchor.MiddleCenter);

        if (next == null)
        {
            Text(new Rect(108, 84, 360, 22), "位极人臣 · 与君共定天下", sSmall, new Color(0.98f, 0.84f, 0.5f));
            return;
        }
        int cur = d.renown, need = next.Need;
        int prev = rank.Need;
        float t = Mathf.Clamp01((float)(cur - prev) / Mathf.Max(1, need - prev));
        Fill(new Rect(108, 91, 150, 6), new Color(0.1f, 0.09f, 0.07f, 0.85f));
        Fill(new Rect(108, 91, 150 * t, 6), new Color(0.82f, 0.66f, 0.30f));
        Text(new Rect(264, 84, 300, 22), $"声望 {cur}/{need} → {next.Title}", sSmall, new Color(0.86f, 0.82f, 0.72f));
    }

    void DrawVitals(PlayerCtrl p)
    {
        if (p == null) return;
        float x = 24f, y = 596f, w = 300f, h = 16f;

        Bar(x, y, w, h, p.HP / Mathf.Max(1f, p.MaxHP), new Color(0.78f, 0.20f, 0.17f), new Color(0.11f, 0.08f, 0.07f, 0.9f));
        Text(new Rect(x + 6, y - 1, 280, h), $"气血 {Mathf.CeilToInt(p.HP)} / {Mathf.CeilToInt(p.MaxHP)}", sSmall, new Color(1f, 0.95f, 0.9f));

        Bar(x, y + 24, w, 12, p.Qi / p.MaxQi, new Color(0.85f, 0.66f, 0.28f), new Color(0.11f, 0.08f, 0.07f, 0.9f));
        Text(new Rect(x + 6, y + 23, 280, 12), $"气力 {Mathf.RoundToInt(p.Qi)}", sSmall);

        Bar(x, y + 44, w, 10, p.Stam / p.MaxStam, new Color(0.45f, 0.66f, 0.42f), new Color(0.11f, 0.08f, 0.07f, 0.9f));
        Text(new Rect(x + 6, y + 43, 280, 11), $"精力 {Mathf.RoundToInt(p.Stam)}", sSmall);

        if (G.Progress.Data.attrPts > 0 || G.Progress.Data.talentPts > 0 || G.Progress.Data.sp > 0)
            Text(new Rect(x, y + 58, w, 22), "有可分配点数 · 按 C 打开人物/天赋", sSmall, new Color(1f, 0.86f, 0.5f));
    }

    void DrawSkills(PlayerCtrl p)
    {
        if (p == null) return;
        // 图标为主：大图标 + 角标键位/等级，不再铺中文名
        const float bw = 68f, bh = 68f, gap = 16f;
        float total = bw * 3 + gap * 2;
        float x0 = CX - total * 0.5f;
        float y = 616f;

        Texture2D[] icons = { Tex.IconQ, Tex.IconE, Tex.IconR };
        // 键位角标跟着玩家设置走
        KeyCode[] bindKeys = { Bind.Skill1, Bind.Skill2, Bind.Skill3 };

        for (int i = 0; i < 3; i++)
        {
            var s = DB.Skills[i];
            int lv = G.Progress.Data.skillLv[i];
            Rect r = new Rect(x0 + i * (bw + gap), y, bw, bh);

            Fill(r, new Color(0.08f, 0.07f, 0.06f, 0.88f));
            float cd = p.Cooldowns[i];
            bool ready = cd <= 0f;

            var icon = icons[i];
            if (icon != null)
            {
                float pad = 8f;
                GUI.color = ready ? new Color(1f, 0.90f, 0.62f) : new Color(0.42f, 0.40f, 0.36f);
                GUI.DrawTexture(new Rect(r.x + pad, r.y + pad, bw - pad * 2f, bh - pad * 2f - 4f), icon, ScaleMode.ScaleToFit);
                GUI.color = Color.white;
            }

            // 边框：可用时是金边，不可用时暗下来，和面板的装饰件一致
            UiKit.Edge(Fill, r, ready ? new Color(0.72f, 0.58f, 0.28f, 0.7f) : new Color(0.4f, 0.38f, 0.34f, 0.45f), 1f);

            // 键位角标：画个小方块底，字才看得清
            Rect kb = new Rect(r.x + 3, r.y + 2, 17, 15);
            Fill(kb, new Color(0.05f, 0.05f, 0.04f, 0.8f));
            Text(new Rect(kb.x, kb.y - 1, kb.width, kb.height), SettingsUI.KeyName(bindKeys[i]),
                 sSmall, new Color(0.98f, 0.9f, 0.7f));
            Text(new Rect(r.x + bw - 28, r.y + bh - 18, 26, 14), "L" + lv, sSmall, new Color(0.78f, 0.74f, 0.66f));

            if (!ready)
            {
                float maxCd = Mathf.Max(0.01f, DB.SkillCooldown(s, lv));
                float ratio = Mathf.Clamp01(cd / maxCd);
                // 自下而上的冷却扫条
                Fill(new Rect(r.x, r.y + bh * (1f - ratio), bw, bh * ratio), new Color(0f, 0f, 0f, 0.62f));
                Text(r, cd.ToString("0.0"), sBig, new Color(1f, 0.92f, 0.72f));
            }
            else if (p.Qi < DB.SkillCost(s, lv))
            {
                Fill(r, new Color(0.35f, 0.08f, 0.06f, 0.35f));
            }

            Fill(new Rect(r.x, r.y + r.height - 3, r.width, 3),
                 ready ? new Color(0.95f, 0.78f, 0.4f) : new Color(0.45f, 0.32f, 0.18f));
        }
    }

    void DrawBoss()
    {
        if (boss == null || boss.Destroyed) return;
        float w = 460f, x = CX - w * 0.5f, y = 30f;
        Text(new Rect(x, y - 22, w, 20), boss.Def.Name + "  「" + (boss.Def.Title ?? "") + "」", sCenter, new Color(1f, 0.88f, 0.72f));
        Fill(new Rect(x, y, w, 12), new Color(0.08f, 0.06f, 0.05f, 0.9f));
        Fill(new Rect(x, y, w * Mathf.Clamp01(boss.HP / Mathf.Max(1f, boss.MaxHP)), 12), new Color(0.76f, 0.24f, 0.20f));
        Fill(new Rect(x, y, w, 1), new Color(1f, 0.9f, 0.7f, 0.35f));
    }

    void DrawHints()
    {
        // 提示里的按键跟着玩家的键位设置走，改了键不用背
        string mv = SettingsUI.KeyName(Bind.MoveUp) + SettingsUI.KeyName(Bind.MoveDown)
                   + SettingsUI.KeyName(Bind.MoveLeft) + SettingsUI.KeyName(Bind.MoveRight);
        Text(new Rect(CX + 80, 640, 320, 18), mv + " 移动   左键轻击   右键重击", sSmall.RightAligned(), new Color(0.8f, 0.78f, 0.72f, 0.85f));
        Text(new Rect(CX + 80, 660, 320, 18),
            SettingsUI.KeyName(Bind.Dash) + "闪避  " + SettingsUI.KeyName(Bind.Skill1) + "/"
            + SettingsUI.KeyName(Bind.Skill2) + "/" + SettingsUI.KeyName(Bind.Skill3) + "技能  "
            + SettingsUI.KeyName(Bind.Panel) + "人物  Esc暂停",
            sSmall.RightAligned(), new Color(0.8f, 0.78f, 0.72f, 0.85f));
    }

    void DrawPops()
    {
        if (G.Cam == null) return;
        foreach (var p in pops)
        {
            Vector3 sp = G.Cam.WorldToScreenPoint(p.World);
            if (sp.z < 0f) continue;
            float t = 1f - p.T / Mathf.Max(0.01f, p.Max);
            float pop = t < 0.18f ? (1.35f + (1f - t / 0.18f) * 0.45f) : 1f;
            float x = sp.x / Scale + p.Drift * t;
            float y = (Screen.height - sp.y) / Scale - EaseOutCubic(t) * 58f;

            Color c;
            int size;
            string text = p.Value.ToString();
            switch (p.Style)
            {
                case "crit": c = new Color(1f, 0.62f, 0.22f); size = 34; text = "暴 " + text; break;
                case "skill": c = new Color(1f, 0.86f, 0.5f); size = 26; break;
                case "self": c = new Color(1f, 0.42f, 0.36f); size = 22; break;
                case "miss": c = new Color(0.78f, 0.84f, 0.94f); size = 20; text = "落空"; break;
                default: c = new Color(1f, 0.96f, 0.88f); size = 21; break;
            }
            float alpha = Mathf.Clamp01(p.T / 0.32f);
            c.a = alpha;
            int fs = Mathf.RoundToInt(size * pop);
            var style = new GUIStyle(sBig) { fontSize = fs };
            var shadow = new GUIStyle(style);
            shadow.normal.textColor = new Color(0f, 0f, 0f, alpha * 0.75f);
            GUI.Label(new Rect(x - 79f, y - 13f, 160f, 36f), text, shadow);
            GUI.Label(new Rect(x - 80f, y - 14f, 160f, 36f), text, style.With(c));
        }
    }

    void DrawToast()
    {
        if (toastT <= 0f) return;
        float a = Mathf.Clamp01(toastT / 0.5f);
        var style = new GUIStyle(sBig) { fontSize = 22 };
        Text(new Rect(0, 120, W, 30), toastText, style.With(new Color(1f, 0.92f, 0.78f, a)));
    }

    // ---------------- 触屏控件（屏幕像素坐标，GUI.matrix = identity 时调用） ----------------

    public void DrawTouchControls()
    {
        float h = Screen.height;

        // 浮动摇杆：锚点在手指落点，旋钮随手指偏移
        if (TouchCtl.JoyVisible)
        {
            Vector2 a = TouchCtl.JoyAnchorPx, k = TouchCtl.JoyKnobPx;
            const float ar = 78f, kr = 30f;
            GUI.color = new Color(1f, 1f, 1f, 0.28f);
            GUI.DrawTexture(new Rect(a.x - ar, h - a.y - ar, ar * 2f, ar * 2f), Tex.Ring);
            GUI.color = new Color(1f, 1f, 1f, 0.55f);
            GUI.DrawTexture(new Rect(k.x - kr, h - k.y - kr, kr * 2f, kr * 2f), Tex.Dot);
        }

        // 技能按钮：暗色圆底 + 图标贴图（不写字）
        var btns = TouchCtl.Buttons;
        for (int i = 0; i < btns.Count; i++)
        {
            var b = btns[i];
            float x = b.Center.x, y = h - b.Center.y;
            GUI.color = new Color(0f, 0f, 0f, 0.38f);
            GUI.DrawTexture(new Rect(x - b.R, y - b.R, b.R * 2f, b.R * 2f), Tex.Dot);
            float ir = b.R * 0.72f;
            GUI.color = new Color(b.Tint.r, b.Tint.g, b.Tint.b, 0.85f);
            GUI.DrawTexture(new Rect(x - ir, y - ir, ir * 2f, ir * 2f), b.Icon);
        }
        GUI.color = Color.white;
    }

    // ---------------- 小工具 ----------------

    static void Fill(Rect r, Color c)
    {
        GUI.color = c;
        GUI.DrawTexture(r, Tex.White);
        GUI.color = Color.white;
    }

    static void Bar(float x, float y, float w, float h, float ratio, Color fg, Color bg)
    {
        Fill(new Rect(x, y, w, h), bg);
        Fill(new Rect(x, y, w * Mathf.Clamp01(ratio), h), fg);
        Fill(new Rect(x, y, w, 1), new Color(1f, 1f, 1f, 0.18f));
    }

    static void Text(Rect r, string s, GUIStyle style) => GUI.Label(r, s, style);
    static void Text(Rect r, string s, GUIStyle style, Color c) => GUI.Label(r, s, style.With(c));
    static void Text(Rect r, string s, GUIStyle style, Color c, TextAnchor align) =>
        GUI.Label(r, s, style.With(c).With(align));
    static float EaseOutCubic(float t) { t = Mathf.Clamp01(t); return 1f - (1f - t) * (1f - t) * (1f - t); }
}

public static class StyleExt
{
    public static GUIStyle With(this GUIStyle src, Color c)
    {
        var s = new GUIStyle(src);
        s.normal.textColor = c;
        return s;
    }

    public static GUIStyle RightAligned(this GUIStyle src)
    {
        var s = new GUIStyle(src);
        s.alignment = TextAnchor.MiddleRight;
        return s;
    }

    public static GUIStyle With(this GUIStyle src, TextAnchor align)
    {
        var s = new GUIStyle(src);
        s.alignment = align;
        return s;
    }
}
