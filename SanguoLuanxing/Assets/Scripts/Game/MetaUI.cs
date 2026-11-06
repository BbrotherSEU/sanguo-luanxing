using UnityEngine;

/// <summary>
/// 元游戏 UI：世界地图（州郡节点）/ 章节任务 / 人物（属性·天赋·装备）/ 暂停。
/// </summary>
public static class MetaUI
{
    public static int HubChapter = 0;
    public static bool ShowQuestPanel = true;
    public static int CharTab; // 0装备 1加点天赋

    /// <summary>章节节点数：跟着关卡表走。</summary>
    static int ChapterCount => DB.Maps.Count;

    static readonly string[] MapThumbs =
    {
        "bg/map1_zhuojun.jpg", "bg/map2_sishui.jpg", "bg/map3_changban.jpg", "bg/map4_hulao.jpg",
        "bg/map5_luoyang.jpg", "bg/map6_guandu.jpg", "bg/map7_weihe.jpg",
    };
    static readonly Texture2D[] ThumbCache = new Texture2D[7];
    static readonly string[] ChapterNames = { "涿郡 · 桃林", "汜水关", "长坂坡", "虎牢关", "洛阳 · 东郊", "官渡 · 乌巢", "渭水 · 潼关" };
    // 地图节点位置（逻辑分辨率 1280x720 基准，按 CX 居中偏移）
    // 蛇形排布：上排从左到右，下排从右到左 —— 七章都落在可视区里，连线也不打结
    static readonly Vector2[] NodePos =
    {
        new Vector2(-390f, 100f),
        new Vector2(-200f, 100f),
        new Vector2(-10f, 100f),
        new Vector2(180f, 100f),
        new Vector2(180f, 260f),
        new Vector2(-10f, 260f),
        new Vector2(-200f, 260f),
    };

    public static void DrawHub(Game g, float W, float CX, System.Action<Rect, string, int, Color, TextAnchor, bool> label, System.Action<Rect, Color> fill)
    {
        // 地图底图
        if (ThumbCache[0] == null) ThumbCache[0] = Tex.LoadRaw(MapThumbs[0]);
        if (ThumbCache[0] != null && ThumbCache[0] != Tex.White)
        {
            GUI.DrawTexture(new Rect(0, 0, W, 720), ThumbCache[0], ScaleMode.ScaleAndCrop);
            fill(new Rect(0, 0, W, 720), new Color(0.04f, 0.035f, 0.03f, 0.55f));
        }
        else
            fill(new Rect(0, 0, W, 720), new Color(0.08f, 0.07f, 0.06f, 1f));

        // 标题条
        fill(new Rect(0, 0, W, 78), new Color(0.04f, 0.035f, 0.03f, 0.82f));
        label(new Rect(CX - 400, 10, 800, 40), "天 下 · 世 界 地 图", 34, new Color(0.96f, 0.88f, 0.7f), TextAnchor.MiddleCenter, false);
        var fac = g.Progress.Faction;
        string sub = fac != null
            ? $"{fac.Name} · {fac.Leader}　Lv.{g.Progress.Data.level}　铜钱 {g.Progress.Data.gold}　属性点 {g.Progress.Data.attrPts}　天赋点 {g.Progress.Data.talentPts}"
            : "";
        label(new Rect(CX - 460, 48, 920, 22), sub, 13, new Color(0.82f, 0.78f, 0.7f), TextAnchor.MiddleCenter, false);

        // 州郡节点 + 连线暗示（相邻章节相连）
        for (int i = 1; i < ChapterCount; i++)
        {
            Vector2 a = NodePos[i - 1], b = NodePos[i];
            DrawLink(CX + a.x + 70, a.y + 36, CX + b.x + 70, b.y + 36, fill);
        }

        for (int i = 0; i < ChapterCount; i++)
        {
            bool unlocked = Quests.ChapterUnlocked(g.Progress, i);
            bool sel = HubChapter == i && ShowQuestPanel;
            Vector2 np = NodePos[i];
            Rect node = new Rect(CX + np.x, np.y, 140, 110);

            if (ThumbCache[i] == null) ThumbCache[i] = Tex.LoadRaw(MapThumbs[i]);
            fill(node, sel ? new Color(0.18f, 0.15f, 0.1f, 0.95f) : new Color(0.08f, 0.07f, 0.06f, 0.88f));
            if (ThumbCache[i] != null && ThumbCache[i] != Tex.White)
                GUI.DrawTexture(new Rect(node.x + 8, node.y + 8, 124, 52), ThumbCache[i], ScaleMode.ScaleAndCrop);
            fill(new Rect(node.x, node.y, node.width, 3),
                sel ? new Color(1f, 0.86f, 0.45f) : (unlocked ? new Color(0.7f, 0.62f, 0.4f) : new Color(0.3f, 0.28f, 0.25f)));

            label(new Rect(node.x, node.y + 62, node.width, 20), DB.Maps[i].Chapter, 12,
                unlocked ? new Color(0.9f, 0.84f, 0.7f) : new Color(0.5f, 0.48f, 0.44f), TextAnchor.MiddleCenter, false);
            label(new Rect(node.x, node.y + 80, node.width, 22), ChapterNames[i], 14,
                unlocked ? (sel ? new Color(1f, 0.92f, 0.55f) : new Color(0.95f, 0.9f, 0.8f)) : new Color(0.45f, 0.42f, 0.38f),
                TextAnchor.MiddleCenter, false);

            if (!unlocked)
            {
                fill(node, new Color(0.02f, 0.02f, 0.02f, 0.45f));
                // 门槛原因：官阶不够 or 前一章没打完
                int needRank = Career.ChapterRank(i);
                bool rankOk = Career.RankOf(g.Progress) >= needRank;
                string why = rankOk ? "上一章未了" : "需" + Career.Ranks[needRank].Title;
                label(new Rect(node.x, node.y + 34, node.width, 22), why, 14,
                    new Color(0.85f, 0.62f, 0.48f), TextAnchor.MiddleCenter, false);
                label(new Rect(node.x, node.y + 56, node.width, 20), "未解锁", 12,
                    new Color(0.62f, 0.58f, 0.5f), TextAnchor.MiddleCenter, false);
            }
            else if (g.Progress.Data.cleared[i])
                label(new Rect(node.x, node.y + 4, node.width - 6, 16), "已定", 11, new Color(0.95f, 0.82f, 0.45f), TextAnchor.MiddleRight, false);
        }

        label(new Rect(CX - 400, 420, 800, 24), "点击州郡查看任务　·　对话 → 收集 → 副本", 14,
            new Color(0.85f, 0.8f, 0.7f), TextAnchor.MiddleCenter, false);

        // 任务面板
        if (ShowQuestPanel)
            DrawQuestPanel(g, W, CX, label, fill);

        Rect charBtn = new Rect(CX - 90, 660, 180, 36);
        fill(charBtn, new Color(0.18f, 0.15f, 0.11f, 0.95f));
        label(charBtn, "人物 · 天赋", 17, new Color(0.98f, 0.88f, 0.62f), TextAnchor.MiddleCenter, false);
    }

    static void DrawLink(float x0, float y0, float x1, float y1, System.Action<Rect, Color> fill)
    {
        float dx = x1 - x0, dy = y1 - y0;
        float len = Mathf.Sqrt(dx * dx + dy * dy);
        if (len < 1f) return;
        int steps = Mathf.Clamp(Mathf.RoundToInt(len / 8f), 4, 40);
        for (int i = 0; i < steps; i++)
        {
            float t = i / (float)steps;
            float x = Mathf.Lerp(x0, x1, t);
            float y = Mathf.Lerp(y0, y1, t);
            fill(new Rect(x - 2f, y - 2f, 4f, 4f), new Color(0.78f, 0.62f, 0.32f, 0.55f));
        }
    }

    static void DrawQuestPanel(Game g, float W, float CX, System.Action<Rect, string, int, Color, TextAnchor, bool> label, System.Action<Rect, Color> fill)
    {
        Rect panel = new Rect(CX - 420, 448, 840, 200);
        fill(panel, new Color(0.05f, 0.045f, 0.04f, 0.94f));
        fill(new Rect(panel.x, panel.y, panel.width, 3), new Color(0.78f, 0.62f, 0.32f, 0.85f));

        var map = DB.Maps[Mathf.Clamp(HubChapter, 0, DB.Maps.Count - 1)];
        label(new Rect(panel.x + 16, panel.y + 8, 500, 24),
            $"{map.Chapter} · {map.Name}　任务", 18, new Color(0.96f, 0.9f, 0.78f), TextAnchor.MiddleLeft, false);
        label(new Rect(panel.x + 520, panel.y + 10, 300, 22), map.Subtitle, 12,
            new Color(0.7f, 0.66f, 0.58f), TextAnchor.MiddleRight, false);

        var quests = Quests.ForChapter(HubChapter);
        // 三列排布：后期章节有 5~6 条任务，两列会把第三行挤到面板外面
        float qw = 262f, gap = 10f, qh = 72f;
        float startX = panel.x + 16f;
        for (int i = 0; i < quests.Count; i++)
        {
            var q = quests[i];
            bool done = Quests.IsDone(g.Progress, q.Id);
            bool open = Quests.Unlocked(g.Progress, q);
            int col = i % 3, rowi = i / 3;
            Rect row = new Rect(startX + col * (qw + gap), panel.y + 36 + rowi * (qh + 8), qw, qh);
            fill(row, open ? new Color(0.12f, 0.11f, 0.1f, 0.96f) : new Color(0.08f, 0.075f, 0.07f, 0.9f));
            fill(new Rect(row.x, row.y, row.width, 3),
                done ? new Color(0.78f, 0.62f, 0.32f) : (open ? new Color(0.55f, 0.7f, 0.45f) : new Color(0.3f, 0.28f, 0.25f)));

            string kind = q.Kind == QuestKind.Talk ? "对话" : (q.Kind == QuestKind.Collect ? "收集" : "副本");
            label(new Rect(row.x + 8, row.y + 6, 195, 20), $"【{kind}】{q.Title}", 14,
                open ? new Color(0.96f, 0.9f, 0.78f) : new Color(0.5f, 0.48f, 0.44f), TextAnchor.MiddleLeft, false);
            label(new Rect(row.x + 8, row.y + 28, 140, 40), q.Desc, 11,
                new Color(0.72f, 0.68f, 0.6f), TextAnchor.UpperLeft, true);

            // 声望与门槛：让玩家一眼看出"为什么还做不了"
            if (open)
            {
                label(new Rect(row.x + qw - 128, row.y + 6, 120, 18), "声望 +" + q.RenownReward, 11,
                    new Color(0.86f, 0.74f, 0.42f), TextAnchor.MiddleRight, false);
            }
            else
            {
                string reason = Quests.LockReason(g.Progress, q);
                if (!string.IsNullOrEmpty(reason))
                    label(new Rect(row.x + qw - 134, row.y + 6, 126, 18), reason, 11,
                        new Color(0.78f, 0.5f, 0.42f), TextAnchor.MiddleRight, false);
            }

            bool canClick = open && (!done || q.Kind == QuestKind.Battle);
            string state = !open ? "未解锁" : (q.Kind == QuestKind.Battle ? (done ? "再 战" : "进入副本") : (done ? "已完成" : "接 受"));
            Color sc = done && q.Kind != QuestKind.Battle ? new Color(0.95f, 0.82f, 0.5f)
                : (canClick ? new Color(0.98f, 0.88f, 0.55f) : new Color(0.45f, 0.42f, 0.38f));
            Rect btn = new Rect(row.x + qw - 112, row.y + 34, 102, 32);
            fill(btn, canClick ? new Color(0.22f, 0.18f, 0.12f, 0.98f) : new Color(0.11f, 0.1f, 0.09f, 0.9f));
            label(btn, state, 15, sc, TextAnchor.MiddleCenter, false);
        }
    }

    /// <summary>返回：-2人物, -1无, 10+chapter 选章, 100+qi 接任务</summary>
    public static int HubClick(Game g, Vector2 mp, float CX)
    {
        if (new Rect(CX - 90, 660, 180, 36).Contains(mp)) return -2;

        for (int i = 0; i < ChapterCount; i++)
        {
            if (!Quests.ChapterUnlocked(g.Progress, i)) continue;
            Vector2 np = NodePos[i];
            Rect node = new Rect(CX + np.x, np.y, 140, 110);
            if (node.Contains(mp)) return 10 + i;
        }

        if (!ShowQuestPanel) return -1;

        var quests = Quests.ForChapter(HubChapter);
        Rect panel = new Rect(CX - 420, 448, 840, 200);
        // 与 DrawQuestPanel 保持同一套三列几何，否则点按钮会错位
        float qw = 262f, gap = 10f, qh = 72f, startX = panel.x + 16f;
        for (int i = 0; i < quests.Count; i++)
        {
            var q = quests[i];
            if (!Quests.Unlocked(g.Progress, q)) continue;
            bool done = Quests.IsDone(g.Progress, q.Id);
            if (done && q.Kind != QuestKind.Battle) continue;
            int col = i % 3, rowi = i / 3;
            Rect btn = new Rect(startX + col * (qw + gap) + qw - 112, panel.y + 36 + rowi * (qh + 8) + 34, 102, 32);
            if (btn.Contains(mp)) return 100 + i;
        }
        return -1;
    }

    public static void DrawCharacter(Game g, float W, float CX, System.Action<Rect, string, int, Color, TextAnchor, bool> label, System.Action<Rect, Color> fill)
    {
        fill(new Rect(0, 0, W, 720), new Color(0.04f, 0.035f, 0.03f, 0.94f));
        var d = g.Progress.Data;
        var s = g.Progress.Stats();
        var fac = g.Progress.Faction;

        label(new Rect(CX - 400, 20, 800, 36), "人 物", 34, new Color(0.95f, 0.88f, 0.72f), TextAnchor.MiddleCenter, false);
        label(new Rect(CX - 400, 54, 800, 22),
            $"Lv.{d.level}　{(fac != null ? fac.Name + " · " + fac.Leader : "")}　技能点 {d.sp}　属性点 {d.attrPts}　天赋点 {d.talentPts}　铜钱 {d.gold}",
            13, new Color(0.78f, 0.74f, 0.66f), TextAnchor.MiddleCenter, false);

        string[] tabs = { "装备 · 武艺", "加点 · 天赋", "仕 途" };
        for (int t = 0; t < 3; t++)
        {
            Rect tr = new Rect(CX - 250 + t * 170, 82, 150, 32);
            fill(tr, CharTab == t ? new Color(0.2f, 0.17f, 0.12f, 0.98f) : new Color(0.1f, 0.09f, 0.08f, 0.9f));
            label(tr, tabs[t], 15, CharTab == t ? new Color(1f, 0.9f, 0.55f) : new Color(0.7f, 0.66f, 0.58f), TextAnchor.MiddleCenter, false);
        }

        if (CharTab == 0) DrawEquipTab(g, CX, label, fill, d, s, fac);
        else if (CharTab == 1) DrawTalentTab(g, CX, label, fill, d, s);
        else DrawCareerTab(g, CX, label, fill, d);

        Rect back = new Rect(CX - 80, 660, 160, 36);
        fill(back, new Color(0.18f, 0.15f, 0.11f, 0.95f));
        label(back, "返 回", 17, new Color(0.98f, 0.88f, 0.62f), TextAnchor.MiddleCenter, false);
    }

    static void DrawEquipTab(Game g, float CX, System.Action<Rect, string, int, Color, TextAnchor, bool> label, System.Action<Rect, Color> fill, SaveData d, Stats s, FactionDef fac)
    {
        Color frame = new Color(0.72f, 0.58f, 0.28f, 1f);
        Rect left = new Rect(CX - 460, 128, 300, 510);
        UiKit.Panel(fill, left, new Color(0.09f, 0.08f, 0.07f, 0.95f), frame);
        UiKit.Section(fill, label, new Rect(left.x + 14, left.y + 8, 270, 26), "属性", frame, new Color(0.95f, 0.86f, 0.65f));
        string[] stats =
        {
            $"气血  {s.MaxHP:0}", $"攻击  {s.Atk:0}", $"防御  {s.Def:0}",
            $"暴击  {s.Crit * 100f:0.0}%", $"身法  {s.Speed:0.0}",
            $"力量 {d.atrStr}　体质 {d.atrVit}　身法点 {d.atrAgi}",
        };
        for (int i = 0; i < stats.Length; i++)
            label(new Rect(left.x + 18, left.y + 40 + i * 24, 260, 22), stats[i], 14, new Color(0.88f, 0.84f, 0.76f), TextAnchor.MiddleLeft, false);
        if (fac != null)
            label(new Rect(left.x + 18, left.y + 190, 260, 40), $"{fac.PassiveName}\n{fac.PassiveDesc}", 12, new Color(0.75f, 0.7f, 0.6f), TextAnchor.UpperLeft, true);

        label(new Rect(left.x + 14, left.y + 240, 270, 24), "装备（点击淬炼）", 16, new Color(0.95f, 0.86f, 0.65f), TextAnchor.MiddleLeft, false);
        for (int i = 0; i < 3; i++)
        {
            string slot = Items.SlotIds[i];
            var it = g.Progress.Equipped(slot);
            Rect r = new Rect(left.x + 12, left.y + 270 + i * 58, 276, 52);
            fill(r, new Color(0.12f, 0.11f, 0.1f, 0.95f));
            // 品质色条：一眼看出装备品级
            if (it != null)
            {
                UiKit.Tag(fill, new Rect(r.x, r.y, 3f, r.height), Items.RarityColors[it.rarity]);
            }
            Color rc = it != null ? Items.RarityColors[it.rarity] : new Color(0.55f, 0.52f, 0.48f);
            label(new Rect(r.x + 12, r.y + 4, 80, 20), Items.SlotNames[i], 12, new Color(0.7f, 0.66f, 0.58f), TextAnchor.MiddleLeft, false);
            label(new Rect(r.x + 12, r.y + 24, 180, 22), it != null ? Items.DisplayName(it) : "未着", 15, rc, TextAnchor.MiddleLeft, false);
            if (it != null)
                label(new Rect(r.x + 160, r.y + 28, 110, 18), "淬炼 " + Items.UpgradeCost(it), 12, new Color(0.95f, 0.8f, 0.48f), TextAnchor.MiddleRight, false);
        }

        Rect mid = new Rect(CX - 140, 128, 280, 510);
        UiKit.Panel(fill, mid, new Color(0.09f, 0.08f, 0.07f, 0.95f), frame);
        UiKit.Section(fill, label, new Rect(mid.x + 14, mid.y + 8, 250, 26), "行囊（点击装备）", frame, new Color(0.95f, 0.86f, 0.65f));
        if (d.bag == null || d.bag.Count == 0)
            label(new Rect(mid.x + 14, mid.y + 50, 250, 30), "囊中空空", 14, new Color(0.55f, 0.52f, 0.48f), TextAnchor.MiddleLeft, false);
        else
        {
            for (int i = 0; i < Mathf.Min(11, d.bag.Count); i++)
            {
                var it = d.bag[i];
                Rect r = new Rect(mid.x + 10, mid.y + 44 + i * 40, 260, 36);
                fill(r, new Color(0.12f, 0.11f, 0.1f, 0.95f));
                UiKit.Tag(fill, new Rect(r.x, r.y, 3f, r.height), Items.RarityColors[it.rarity]);
                label(new Rect(r.x + 12, r.y + 2, 240, 18), Items.DisplayName(it), 13, Items.RarityColors[it.rarity], TextAnchor.MiddleLeft, false);
                label(new Rect(r.x + 12, r.y + 18, 240, 16), Items.Desc(it), 11, new Color(0.7f, 0.66f, 0.58f), TextAnchor.MiddleLeft, false);
            }
        }

        Rect right = new Rect(CX + 160, 128, 300, 510);
        UiKit.Panel(fill, right, new Color(0.09f, 0.08f, 0.07f, 0.95f), frame);
        UiKit.Section(fill, label, new Rect(right.x + 14, right.y + 8, 270, 26), "武艺（点击升级）", frame, new Color(0.95f, 0.86f, 0.65f));
        Texture2D[] icons = { Tex.IconQ, Tex.IconE, Tex.IconR };
        for (int i = 0; i < 3; i++)
        {
            var sk = DB.Skills[i];
            int lv = d.skillLv[i];
            Rect r = new Rect(right.x + 12, right.y + 50 + i * 120, 276, 108);
            fill(r, new Color(0.12f, 0.11f, 0.1f, 0.95f));
            // 技能图标加个金框，像镶嵌在卷轴上
            if (icons[i] != null)
            {
                Rect ir = new Rect(r.x + 10, r.y + 18, 56, 56);
                fill(new Rect(ir.x - 3f, ir.y - 3f, ir.width + 6f, ir.height + 6f), new Color(0.52f, 0.40f, 0.18f, 0.9f));
                GUI.color = new Color(1f, 0.88f, 0.58f);
                GUI.DrawTexture(ir, icons[i], ScaleMode.ScaleToFit);
                GUI.color = Color.white;
            }
            label(new Rect(r.x + 76, r.y + 14, 180, 24), $"{sk.Name}  Lv.{lv}", 16, new Color(0.96f, 0.9f, 0.78f), TextAnchor.MiddleLeft, false);
            label(new Rect(r.x + 76, r.y + 42, 180, 36), sk.Desc, 12, new Color(0.7f, 0.66f, 0.58f), TextAnchor.UpperLeft, true);
            string up = lv >= 5 ? "已满" : (d.sp > 0 ? "升级" : "缺技能点");
            label(new Rect(r.x + 76, r.y + 78, 180, 22), up, 13,
                lv >= 5 ? new Color(0.55f, 0.52f, 0.48f) : (d.sp > 0 ? new Color(0.98f, 0.84f, 0.48f) : new Color(0.6f, 0.4f, 0.35f)),
                TextAnchor.MiddleLeft, false);
        }
    }

    static void DrawTalentTab(Game g, float CX, System.Action<Rect, string, int, Color, TextAnchor, bool> label, System.Action<Rect, Color> fill, SaveData d, Stats s)
    {
        Color frame = new Color(0.72f, 0.58f, 0.28f, 1f);
        Rect left = new Rect(CX - 460, 128, 340, 510);
        UiKit.Panel(fill, left, new Color(0.09f, 0.08f, 0.07f, 0.95f), frame);
        UiKit.Section(fill, label, new Rect(left.x + 14, left.y + 10, 310, 26), "自由属性加点", frame, new Color(0.95f, 0.86f, 0.65f));
        label(new Rect(left.x + 14, left.y + 42, 310, 22), $"剩余属性点：{d.attrPts}（升级 +3）", 14,
            d.attrPts > 0 ? new Color(0.98f, 0.84f, 0.48f) : new Color(0.65f, 0.6f, 0.55f), TextAnchor.MiddleLeft, false);

        string[] an = { "力量", "体质", "身法" };
        string[] ad = { "攻击 +2.2 / 点", "气血 +12、防御微增", "身法 +0.12、暴击 +0.8%" };
        int[] av = { d.atrStr, d.atrVit, d.atrAgi };
        for (int i = 0; i < 3; i++)
        {
            Rect r = new Rect(left.x + 14, left.y + 80 + i * 100, 312, 88);
            fill(r, new Color(0.12f, 0.11f, 0.1f, 0.95f));
            UiKit.Edge(fill, r, new Color(0.72f, 0.58f, 0.28f, 0.28f), 1f);
            label(new Rect(r.x + 12, r.y + 10, 180, 24), $"{an[i]}  {av[i]}", 18, new Color(0.96f, 0.9f, 0.78f), TextAnchor.MiddleLeft, false);
            label(new Rect(r.x + 12, r.y + 40, 200, 36), ad[i], 13, new Color(0.7f, 0.66f, 0.58f), TextAnchor.UpperLeft, true);
            Rect plus = new Rect(r.x + r.width - 70, r.y + 24, 56, 40);
            fill(plus, d.attrPts > 0 ? new Color(0.22f, 0.18f, 0.12f, 0.98f) : new Color(0.12f, 0.11f, 0.1f, 0.8f));
            UiKit.Edge(fill, plus, new Color(0.72f, 0.58f, 0.28f, d.attrPts > 0 ? 0.7f : 0.2f), 1f);
            label(plus, "+", 28, d.attrPts > 0 ? new Color(1f, 0.9f, 0.5f) : new Color(0.45f, 0.42f, 0.38f), TextAnchor.MiddleCenter, false);
        }

        label(new Rect(left.x + 14, left.y + 400, 310, 80),
            $"当前合计\n气血 {s.MaxHP:0}　攻击 {s.Atk:0}\n防御 {s.Def:0}　暴击 {s.Crit * 100f:0.0}%　身法 {s.Speed:0.0}",
            13, new Color(0.78f, 0.74f, 0.66f), TextAnchor.UpperLeft, true);

        Rect right = new Rect(CX - 100, 128, 560, 510);
        UiKit.Panel(fill, right, new Color(0.09f, 0.08f, 0.07f, 0.95f), frame);
        UiKit.Section(fill, label, new Rect(right.x + 14, right.y + 10, 520, 26), "天赋树", frame, new Color(0.95f, 0.86f, 0.65f));
        label(new Rect(right.x + 14, right.y + 40, 520, 20), $"剩余天赋点：{d.talentPts}（升级 +1）　同列需先点上一层", 13,
            d.talentPts > 0 ? new Color(0.98f, 0.84f, 0.48f) : new Color(0.65f, 0.6f, 0.55f), TextAnchor.MiddleLeft, false);

        string[] cols = { "攻伐", "御守", "身法" };
        for (int c = 0; c < 3; c++)
            label(new Rect(right.x + 30 + c * 175, right.y + 70, 150, 22), cols[c], 15,
                new Color(0.88f, 0.82f, 0.7f), TextAnchor.MiddleCenter, false);

        for (int i = 0; i < Talents.Nodes.Length; i++)
        {
            var n = Talents.Nodes[i];
            int rank = Talents.Rank(g.Progress, i);
            bool can = Talents.CanUnlock(g.Progress, i);
            Rect cell = new Rect(right.x + 20 + n.Col * 175, right.y + 100 + n.Row * 120, 160, 100);
            fill(cell, rank > 0 ? new Color(0.16f, 0.14f, 0.1f, 0.98f) : new Color(0.11f, 0.1f, 0.09f, 0.95f));
            UiKit.Edge(fill, cell, new Color(0.72f, 0.58f, 0.28f, rank > 0 ? 0.55f : 0.22f), 1f);
            fill(new Rect(cell.x, cell.y, cell.width, 3),
                rank > 0 ? new Color(0.85f, 0.7f, 0.35f) : (can ? new Color(0.5f, 0.65f, 0.4f) : new Color(0.3f, 0.28f, 0.25f)));
            label(new Rect(cell.x + 8, cell.y + 10, 144, 22), $"{n.Name}  {rank}/{n.MaxRank}", 15,
                new Color(0.96f, 0.9f, 0.78f), TextAnchor.MiddleLeft, false);
            label(new Rect(cell.x + 8, cell.y + 36, 144, 36), n.EffectText(Mathf.Max(1, rank)), 12,
                new Color(0.72f, 0.68f, 0.6f), TextAnchor.UpperLeft, true);
            string tip = rank >= n.MaxRank ? "已满" : (can ? "点击加点" : "不可用");
            label(new Rect(cell.x + 8, cell.y + 74, 144, 20), tip, 12,
                can ? new Color(0.98f, 0.84f, 0.48f) : new Color(0.5f, 0.48f, 0.44f), TextAnchor.MiddleLeft, false);
        }
    }

    /// <summary>仕途页：六级台阶，看得见自己爬了多高</summary>
    static void DrawCareerTab(Game g, float CX, System.Action<Rect, string, int, Color, TextAnchor, bool> label, System.Action<Rect, Color> fill, SaveData d)
    {
        Color frame = new Color(0.72f, 0.58f, 0.28f, 1f);
        int cur = Career.RankOf(g.Progress);

        // 左：当前官阶与进度
        Rect left = new Rect(CX - 460, 128, 300, 510);
        UiKit.Panel(fill, left, new Color(0.09f, 0.08f, 0.07f, 0.95f), frame);
        UiKit.Section(fill, label, new Rect(left.x + 14, left.y + 8, 270, 26), "当前官阶", frame, new Color(0.95f, 0.86f, 0.65f));

        var rd = Career.Ranks[cur];
        label(new Rect(left.x + 16, left.y + 42, 268, 44), rd.Title, 38, new Color(1f, 0.9f, 0.58f), TextAnchor.MiddleLeft, false);
        label(new Rect(left.x + 16, left.y + 88, 268, 40), rd.Desc, 13, new Color(0.76f, 0.72f, 0.64f), TextAnchor.UpperLeft, true);

        var next = Career.Next(g.Progress);
        if (next == null)
        {
            label(new Rect(left.x + 16, left.y + 140, 268, 26), "位极人臣 · 无可再升", 15, new Color(0.98f, 0.84f, 0.5f), TextAnchor.MiddleLeft, false);
        }
        else
        {
            int need = next.Need, prev = rd.Need;
            float t = Mathf.Clamp01((float)(d.renown - prev) / Mathf.Max(1, need - prev));
            label(new Rect(left.x + 16, left.y + 138, 268, 22), $"距「{next.Title}」", 13, new Color(0.72f, 0.68f, 0.6f), TextAnchor.MiddleLeft, false);
            fill(new Rect(left.x + 16, left.y + 162, 268, 10), new Color(0.08f, 0.07f, 0.06f, 0.9f));
            fill(new Rect(left.x + 16, left.y + 162, 268 * t, 10), new Color(0.84f, 0.66f, 0.30f));
            label(new Rect(left.x + 16, left.y + 176, 268, 22), $"声望 {d.renown} / {need}", 13, new Color(0.88f, 0.8f, 0.56f), TextAnchor.MiddleLeft, false);
        }

        label(new Rect(left.x + 14, left.y + 214, 270, 24), "本阶加成", 16, new Color(0.95f, 0.86f, 0.65f), TextAnchor.MiddleLeft, false);
        string bonus = rd.AtkPct <= 0f && rd.DefPct <= 0f && rd.HpPct <= 0f
            ? "尚无 —— 从无名之辈开始"
            : $"攻击 +{rd.AtkPct * 100f:0}%　防御 +{rd.DefPct * 100f:0}%　气血 +{rd.HpPct * 100f:0}%";
        label(new Rect(left.x + 18, left.y + 242, 264, 40), bonus, 13, new Color(0.8f, 0.76f, 0.68f), TextAnchor.UpperLeft, true);

        label(new Rect(left.x + 14, left.y + 286, 270, 24), "解锁", 16, new Color(0.95f, 0.86f, 0.65f), TextAnchor.MiddleLeft, false);
        label(new Rect(left.x + 18, left.y + 310, 264, 36), rd.Unlock, 13, new Color(0.8f, 0.76f, 0.68f), TextAnchor.UpperLeft, true);

        // 主公评语
        label(new Rect(left.x + 14, left.y + 350, 270, 22), "主公评语", 16, new Color(0.95f, 0.86f, 0.65f), TextAnchor.MiddleLeft, false);
        var praise = Career.PraiseFor(g.Progress);
        string pv = (praise != null && praise.Length > 0) ? praise[0] : "尚未有人评点。";
        Rect pb = new Rect(left.x + 16, left.y + 372, 268, 60);
        fill(pb, new Color(0.12f, 0.11f, 0.1f, 0.95f));
        UiKit.Edge(fill, pb, new Color(0.72f, 0.58f, 0.28f, 0.3f), 1f);
        label(new Rect(pb.x + 10, pb.y + 5, pb.width - 20, pb.height - 10), "「" + pv + "」", 13,
            new Color(0.9f, 0.85f, 0.72f), TextAnchor.UpperLeft, true);

        // 旗号：这一路自己选出来的分支（结局会按它换文案）
        label(new Rect(left.x + 14, left.y + 436, 270, 20), "旗号 · 你走的路", 14, new Color(0.72f, 0.80f, 0.88f), TextAnchor.MiddleLeft, false);
        string road = Flags.RoadText(g.Progress);
        label(new Rect(left.x + 16, left.y + 456, 268, 22), string.IsNullOrEmpty(road) ? "尚无旗号" : road, 13,
            string.IsNullOrEmpty(road) ? new Color(0.55f, 0.52f, 0.48f) : new Color(0.78f, 0.86f, 0.94f),
            TextAnchor.UpperLeft, true);

        // 逐条列出每个节点的两种走法，让玩家看见「另一种可能」
        for (int i = 0; i < Flags.Dims.Length; i++)
        {
            float y = left.y + 480 + i * 17f;
            if (y + 16 > left.y + 506) break;
            var fd = Flags.OfDim(g.Progress, Flags.Dims[i]);
            string dimName = i < Flags.DimNames.Length ? Flags.DimNames[i] : Flags.Dims[i];
            label(new Rect(left.x + 16, y, 100, 16), dimName, 11, new Color(0.6f, 0.58f, 0.53f), TextAnchor.MiddleLeft, false);
            label(new Rect(left.x + 118, y, 166, 16),
                fd != null ? fd.Title : "未定", 11,
                fd != null ? new Color(0.82f, 0.89f, 0.96f) : new Color(0.45f, 0.43f, 0.4f),
                TextAnchor.MiddleLeft, false);
        }

        // 右：六级台阶
        Rect right = new Rect(CX - 140, 128, 600, 510);
        UiKit.Panel(fill, right, new Color(0.09f, 0.08f, 0.07f, 0.95f), frame);
        UiKit.Section(fill, label, new Rect(right.x + 14, right.y + 10, 570, 26), "仕途 · 六级台阶", frame, new Color(0.95f, 0.86f, 0.65f));
        label(new Rect(right.x + 14, right.y + 40, 570, 20), "从市井小民到君主股肫 —— 声望决定你站在第几级", 13,
            new Color(0.72f, 0.68f, 0.6f), TextAnchor.MiddleLeft, false);

        for (int i = 0; i < Career.Ranks.Length; i++)
        {
            var r = Career.Ranks[i];
            bool reached = i <= cur;
            bool isCur = i == cur;
            Rect row = new Rect(right.x + 16, right.y + 70 + i * 72, 568, 64);
            fill(row, isCur ? new Color(0.2f, 0.16f, 0.09f, 0.98f)
                             : (reached ? new Color(0.13f, 0.12f, 0.1f, 0.95f) : new Color(0.08f, 0.075f, 0.07f, 0.9f)));
            // 顶条：已达=金，未达=灰
            fill(new Rect(row.x, row.y, row.width, 3),
                isCur ? new Color(1f, 0.86f, 0.45f) : (reached ? new Color(0.72f, 0.6f, 0.3f, 0.7f) : new Color(0.28f, 0.26f, 0.24f, 0.7f)));

            label(new Rect(row.x + 14, row.y + 10, 120, 26), r.Title, 20,
                reached ? new Color(0.98f, 0.9f, 0.68f) : new Color(0.45f, 0.43f, 0.4f), TextAnchor.MiddleLeft, false);
            label(new Rect(row.x + 14, row.y + 36, 130, 20), i == 0 ? "起始" : "声望 " + r.Need, 12,
                reached ? new Color(0.72f, 0.68f, 0.58f) : new Color(0.42f, 0.4f, 0.37f), TextAnchor.MiddleLeft, false);

            label(new Rect(row.x + 150, row.y + 8, 250, 24), r.Desc, 13,
                reached ? new Color(0.85f, 0.81f, 0.72f) : new Color(0.42f, 0.4f, 0.37f), TextAnchor.MiddleLeft, false);
            label(new Rect(row.x + 150, row.y + 32, 250, 22), reached ? r.Unlock : "未至", 12,
                reached ? new Color(0.72f, 0.74f, 0.6f) : new Color(0.4f, 0.38f, 0.35f), TextAnchor.MiddleLeft, false);

            // 加成小字
            string pct = (r.AtkPct <= 0f && r.DefPct <= 0f && r.HpPct <= 0f) ? "—"
                : $"攻+{r.AtkPct * 100f:0}% 防+{r.DefPct * 100f:0}% 血+{r.HpPct * 100f:0}%";
            label(new Rect(row.x + 420, row.y + 8, 140, 22), pct, 13,
                reached ? new Color(0.86f, 0.76f, 0.46f) : new Color(0.4f, 0.38f, 0.35f), TextAnchor.MiddleLeft, false);

            if (isCur)
            {
                Rect mark = new Rect(row.x + row.width - 64, row.y + 16, 52, 30);
                fill(mark, new Color(0.3f, 0.22f, 0.08f, 0.98f));
                UiKit.Edge(fill, mark, new Color(1f, 0.86f, 0.45f, 0.8f), 1f);
                label(mark, "现", 15, new Color(1f, 0.9f, 0.55f), TextAnchor.MiddleCenter, false);
            }
        }
    }

    public static int CharacterClick(Game g, Vector2 mp, float CX)
    {
        if (new Rect(CX - 80, 660, 160, 36).Contains(mp)) return -2;
        for (int t = 0; t < 3; t++)
            if (new Rect(CX - 250 + t * 170, 82, 150, 32).Contains(mp)) return -3 - t;

        if (CharTab == 0)
        {
            Rect left = new Rect(CX - 460, 128, 300, 510);
            for (int i = 0; i < 3; i++)
                if (new Rect(left.x + 12, left.y + 270 + i * 58, 276, 52).Contains(mp)) return 10 + i;

            Rect mid = new Rect(CX - 140, 128, 280, 510);
            var bag = g.Progress.Data.bag;
            if (bag != null)
            {
                int maxShow = Mathf.Min(11, bag.Count);
                for (int i = 0; i < maxShow; i++)
                    if (new Rect(mid.x + 10, mid.y + 44 + i * 40, 260, 36).Contains(mp)) return 100 + i;
            }

            Rect right = new Rect(CX + 160, 128, 300, 510);
            for (int i = 0; i < 3; i++)
                if (new Rect(right.x + 12, right.y + 50 + i * 120, 276, 108).Contains(mp)) return 20 + i;
        }
        else
        {
            Rect left = new Rect(CX - 460, 128, 340, 510);
            for (int i = 0; i < 3; i++)
            {
                Rect plus = new Rect(left.x + 14 + 312 - 70, left.y + 80 + i * 100 + 24, 56, 40);
                if (plus.Contains(mp)) return 30 + i;
            }
            Rect right = new Rect(CX - 100, 128, 560, 510);
            for (int i = 0; i < Talents.Nodes.Length; i++)
            {
                var n = Talents.Nodes[i];
                Rect cell = new Rect(right.x + 20 + n.Col * 175, right.y + 100 + n.Row * 120, 160, 100);
                if (cell.Contains(mp)) return 40 + i;
            }
        }
        return -1;
    }

    public static void DrawPause(float W, float CX, System.Action<Rect, string, int, Color, TextAnchor, bool> label, System.Action<Rect, Color> fill)
    {
        fill(new Rect(0, 0, W, 720), new Color(0.03f, 0.03f, 0.025f, 0.72f));
        Color frame = new Color(0.72f, 0.58f, 0.28f, 1f);
        // 面板本体：竖长条，纸纹 + 回纹边框
        Rect panel = new Rect(CX - 170f, 150f, 340f, 440f);
        UiKit.Panel(fill, panel, new Color(0.09f, 0.08f, 0.07f, 0.96f), frame);
        label(new Rect(CX - 200, 168, 400, 50), "暂 停", 40, new Color(0.95f, 0.88f, 0.72f), TextAnchor.MiddleCenter, false);
        UiKit.Divider(fill, new Rect(CX - 130f, 224f, 260f, 0f), new Color(0.72f, 0.58f, 0.28f, 0.5f));
        string[] btns = { "继续", "人物 · 天赋", "设 置", "离开 / 回标题" };
        for (int i = 0; i < btns.Length; i++)
        {
            Rect r = new Rect(CX - 110, 280 + i * 70, 220, 52);
            fill(r, new Color(0.14f, 0.12f, 0.1f, 0.95f));
            UiKit.Edge(fill, r, new Color(0.72f, 0.58f, 0.28f, 0.5f), 1f);
            label(r, btns[i], 20, new Color(0.96f, 0.88f, 0.7f), TextAnchor.MiddleCenter, false);
        }
    }

    public static int PauseClick(Vector2 mp, float CX)
    {
        for (int i = 0; i < 4; i++)
            if (new Rect(CX - 110, 280 + i * 70, 220, 52).Contains(mp)) return i;
        return -1;
    }
}
