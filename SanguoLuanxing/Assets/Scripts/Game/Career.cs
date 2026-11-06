using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 仕途：玩家从市井小民到君主股肫的成长线。
///
/// 玩法定位：三国题材里小人物最容易缺的是"存在感"。
/// 这套系统让战斗与任务不再只是刷数值，而是攒「声望」，
/// 声望决定官阶，官阶决定属性加成与能否触达后期主线（定天下）。
/// 每升一阶，君主会亲自加一句评语，玩家能感到自己在往上走。
/// </summary>
public class RankDef
{
    public string Id;
    public string Title;      // 官阶名
    public string Desc;
    public int Need;          // 需要的声望
    /// <summary>该阶给的属性加成（累加）</summary>
    public float AtkPct, DefPct, HpPct;
    /// <summary>君主评语</summary>
    public string[] Praise;
    /// <summary>升到此阶时解锁的内容提示（告诉玩家"往上走"有什么用）</summary>
    public string Unlock;
}

public static class Career
{
    public static readonly RankDef[] Ranks =
    {
        new RankDef { Id = "commoner", Title = "白身", Need = 0,
            Desc = "无名之辈，尚在市井之间。",
            Unlock = "可接取乡野委托",
            Praise = new[] { "你还没个名姓。但你来了，就比大多数人强。" } },

        new RankDef { Id = "yishi", Title = "义士", Need = 40,
            Desc = "乡里有口口相传：此人可信。",
            AtkPct = 0.04f, DefPct = 0.04f, HpPct = 0.03f,
            Unlock = "可受正式军令，出战有功",
            Praise = new[] { "听说你在桃林里救过的人，自己都没饭吃。",
                             "义字当头，先给你个义士的名号。" } },

        new RankDef { Id = "qishi", Title = "骑士", Need = 120,
            Desc = "能领百人，敢独骑入阵。",
            AtkPct = 0.09f, DefPct = 0.08f, HpPct = 0.07f,
            Unlock = "可独自领兵，接取军功悬赏",
            Praise = new[] { "百人之中，你能先动。这便是骑士。",
                             "别人还在等号令，你已经冲出去了。" } },

        new RankDef { Id = "jiangjun", Title = "将军", Need = 260,
            Desc = "独当一面，可领一军。",
            AtkPct = 0.16f, DefPct = 0.14f, HpPct = 0.12f,
            Unlock = "可统军一方，得以参与定天下之役",
            Praise = new[] { "孤给你一军，你替孤守住这条线。",
                             "你已经不是我的部曲，是我的将军。" } },

        new RankDef { Id = "zhangwu", Title = "柱石", Need = 480,
            Desc = "国之干城，君主倚重。",
            AtkPct = 0.25f, DefPct = 0.22f, HpPct = 0.18f,
            Unlock = "可与主公共议国是，结局不同",
            Praise = new[] { "满朝文武，能与我共谋天下的，只有你。",
                             "你若在，我便无忧。" } },

        new RankDef { Id = "chengxiang", Title = "丞相爱才", Need = 850,
            Desc = "与君共谋，终成大业。",
            AtkPct = 0.38f, DefPct = 0.34f, HpPct = 0.26f,
            Unlock = "可与主公并肩，共享天下",
            Praise = new[] { "天下将定。这一局棋，你我执黑执白都可。",
                             "你从市井走到这里，不比那些世家子差半分。" } },
    };

    /// <summary>某阶的索引</summary>
    public static int IndexOf(string id)
    {
        for (int i = 0; i < Ranks.Length; i++) if (Ranks[i].Id == id) return i;
        return 0;
    }

    /// <summary>是否已到达第 index 阶（含）</summary>
    public static bool AtLeast(Progress p, int index) => RankOf(p) >= index;

    /// <summary>升到指定阶所给的铜钱（升阶奖励）</summary>
    public static int RankBonus(int index)
    {
        if (index <= 0) return 0;
        return 60 * index * index;   // 60 / 240 / 540 / 960 / 1500
    }

    /// <summary>升阶时额外给的属性点</summary>
    public static int RankAttrPoints(int index) => index <= 0 ? 0 : index * 2;

    /// <summary>升阶时额外给的技能点</summary>
    public static int RankSkillPoints(int index) => index <= 0 ? 0 : index;

    /// <summary>是否已到达第 index 阶（按序号，0 起）</summary>
    public static bool Reached(Progress p, int index) => RankOf(p) >= index;

    /// <summary>通往第 index 阶还差多少声望（已达成返回 0）</summary>
    public static int RenownTo(Progress p, int index)
    {
        if (index <= 0 || index >= Ranks.Length) return 0;
        return Mathf.Max(0, Ranks[index].Need - (p?.Data?.renown ?? 0));
    }

    /// <summary>第 n 章需要的官阶（0 起）。越往后越要求玩家真的往上走。</summary>
    /// <remarks>
    /// 按实际可获得声望标定：打完上一章主线大约能到上一阶，
    /// 想再上一阶就得去做支线任务或重刷副本。
    /// 终章（第七章）刻意只要求柱石：终章自身的任务才是把声望推到
    /// 「丞相爱才」的最后一段路 —— 若这里就卡丞相爱才会形成死锁。
    /// </remarks>
    public static int ChapterRank(int chapter)
    {
        switch (chapter)
        {
            case 0: return 0;   // 涿郡：白身即可
            case 1: return 1;   // 汜水：义士
            case 2: return 2;   // 长坂：骑士
            case 3: return 3;   // 虎牢：将军
            case 4: return 3;   // 洛阳：将军
            case 5: return 4;   // 官渡：柱石 —— 只有国之干城才配参与定天下之役
            default: return 4;  // 渭水：柱石（终章任务自己把声望推到丞相爱才）
        }
    }

    public static int RankOf(Progress p)
    {
        int renown = p?.Data?.renown ?? 0;
        int r = 0;
        for (int i = 0; i < Ranks.Length; i++) if (renown >= Ranks[i].Need) r = i;
        return r;
    }

    public static RankDef Current(Progress p) => Ranks[RankOf(p)];

    public static RankDef Next(Progress p)
    {
        int r = RankOf(p);
        return r + 1 < Ranks.Length ? Ranks[r + 1] : null;
    }

    /// <summary>加声望。返回是否因此升阶（用于弹提示）。</summary>
    public static bool AddRenown(Progress p, int amount)
    {
        if (p == null || amount <= 0) return false;
        int before = RankOf(p);
        p.Data.renown += amount;
        int after = RankOf(p);
        return after > before;
    }

    /// <summary>升阶时君主说的话</summary>
    public static string[] PraiseFor(Progress p)
    {
        var r = Current(p);
        return r != null && r.Praise != null ? r.Praise : new string[0];
    }

    /// <summary>把官阶加成加进属性</summary>
    public static void ApplyToStats(Progress p, ref Stats s)
    {
        var r = Current(p);
        if (r == null) return;
        s.Atk *= 1f + r.AtkPct;
        s.Def *= 1f + r.DefPct;
        s.MaxHP *= 1f + r.HpPct;
    }
}
