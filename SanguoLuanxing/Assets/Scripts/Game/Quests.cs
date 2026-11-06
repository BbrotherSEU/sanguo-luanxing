using System.Collections.Generic;
using UnityEngine;

public enum QuestKind { Talk, Collect, Battle }

public class QuestDef
{
    public string Id;
    public int Chapter;          // 0..3 对应四章
    public QuestKind Kind;
    public string Title;
    public string Desc;
    public string[] Requires;    // 前置任务 Id
    public Line[] Lines;         // Talk / 进战前剧情
    public int CollectNeed = 3;  // Collect 目标数
    public string CollectType = "herb";
    public int MapIndex;         // Battle 关联关卡
    public int GoldReward;
    public int ExpReward;
    public int RenownReward;     // 声望：仕途官阶的来源
    public int MinRank = -1;     // 需要的最低官阶（-1 = 不限）
}

/// <summary>世界地图任务表：对话 → 收集 → 解锁副本战斗</summary>
public static class Quests
{
    public static readonly List<QuestDef> All = new List<QuestDef>();
    static readonly Dictionary<string, QuestDef> ById = new Dictionary<string, QuestDef>();

    static Quests()
    {
        // ---- 第一章 涿郡 ----
        Add(new QuestDef
        {
            Id = "q1_talk", Chapter = 0, Kind = QuestKind.Talk,
            Title = "桃林结义", Desc = "与刘备一席谈，听闻黄巾之乱。",
            Lines = Story.Q1_World,
            GoldReward = 20, ExpReward = 30,
        });
        Add(new QuestDef
        {
            Id = "q1_side", Chapter = 0, Kind = QuestKind.Talk,
            Title = "落英与药", Desc = "亭中苏婉，愿听一听这桃林里的药与人。",
            Requires = new[] { "q1_talk" },
            Lines = Story.PeachSuwan,
            GoldReward = 18, ExpReward = 22,
        });
        Add(new QuestDef
        {
            Id = "q1_side2", Chapter = 0, Kind = QuestKind.Talk,
            Title = "阿福的忠告", Desc = "村口扫花的汉子，有话要对你说。",
            Requires = new[] { "q1_side" },
            Lines = Story.PeachAfu,
            GoldReward = 22, ExpReward = 26,
        });
        Add(new QuestDef
        {
            Id = "q1_collect", Chapter = 0, Kind = QuestKind.Collect,
            Title = "收拢粮草", Desc = "在桃林中拾取 3 份逃难百姓落下的粮草。",
            Requires = new[] { "q1_talk" },
            CollectNeed = 3, CollectType = "herb",
            GoldReward = 20, ExpReward = 25,
        });
        Add(new QuestDef
        {
            Id = "q1_battle", Chapter = 0, Kind = QuestKind.Battle,
            Title = "清剿桃林", Desc = "清除附近黄巾，再寻渠帅程远志。",
            Requires = new[] { "q1_collect" },
            MapIndex = 0,
            Lines = Story.T1_Intro,
        });

        // ---- 第二章 汜水 ----
        Add(new QuestDef
        {
            Id = "q2_talk", Chapter = 1, Kind = QuestKind.Talk,
            Title = "关下军议", Desc = "听闻华雄连斩数将，诸侯无人敢战。",
            Requires = new[] { "q1_battle" },
            Lines = new[]
            {
                new Line("", "汜水关外，雪压旌旗。十八路诸侯扎营，却无人出阵。"),
                new Line("曹操", "此人连斩我数员裨将。帐下可还有人敢战？", "npc/cao_cao.jpg"),
                new Line("你", "先探明关前虚实，再作决断。"),
                new Line("曹操", "去吧。探子的军报散落在关前，捡回来我便给你看清楚。", "npc/cao_cao.jpg"),
            },
            GoldReward = 28, ExpReward = 40,
        });
        Add(new QuestDef
        {
            Id = "q2_side", Chapter = 1, Kind = QuestKind.Talk,
            Title = "温酒一盏", Desc = "营边酒贩还温着一碗酒。喝，或不喝。",
            Requires = new[] { "q2_talk" },
            Lines = Story.PassWine,
            GoldReward = 12, ExpReward = 18,
        });
        Add(new QuestDef
        {
            Id = "q2_side2", Chapter = 1, Kind = QuestKind.Talk,
            Title = "关前斥候", Desc = "雪地里有个呵着手的斥候，他比谁都清楚关前的样子。",
            Requires = new[] { "q2_side" },
            Lines = Story.PassScoutSecond,
            GoldReward = 26, ExpReward = 34,
        });
        Add(new QuestDef
        {
            Id = "q2_collect", Chapter = 1, Kind = QuestKind.Collect,
            Title = "收集军报", Desc = "拾取关前斥候留下的 3 卷军报。",
            Requires = new[] { "q2_talk" },
            CollectNeed = 3, CollectType = "scroll",
            GoldReward = 30, ExpReward = 40,
        });
        Add(new QuestDef
        {
            Id = "q2_battle", Chapter = 1, Kind = QuestKind.Battle,
            Title = "汜水关战", Desc = "出阵汜水关，斩华雄。",
            Requires = new[] { "q2_collect" },
            MapIndex = 1,
            Lines = Story.T2_Intro,
        });

        // ---- 第三章 长坂 ----
        Add(new QuestDef
        {
            Id = "q3_talk", Chapter = 2, Kind = QuestKind.Talk,
            Title = "长坂难民", Desc = "百姓随军溃退，刘备不忍弃之。",
            Requires = new[] { "q2_battle" },
            Lines = new[]
            {
                new Line("", "长坂坡上，百姓与辎重绵延数里。秋风把旗吹成一条线。"),
                new Line("刘备", "百姓随我至此，我不能弃之。", "npc/liu_bei.jpg"),
                new Line("你", "我去收拢失散的伤药与干粮。"),
                new Line("刘备", "坡上还有未走的人。你若遇见，便告诉他们——桥还在。", "npc/liu_bei.jpg"),
            },
            GoldReward = 40, ExpReward = 55,
        });
        Add(new QuestDef
        {
            Id = "q3_side", Chapter = 2, Kind = QuestKind.Talk,
            Title = "桥还在", Desc = "坡上逃难的妇人还在等一句能走的话。",
            Requires = new[] { "q3_talk" },
            Lines = Story.ChangbanRefugee,
            GoldReward = 20, ExpReward = 28,
        });
        Add(new QuestDef
        {
            Id = "q3_side2", Chapter = 2, Kind = QuestKind.Talk,
            Title = "断桥之后", Desc = "桥断在河上，桥那头还有人在等。",
            Requires = new[] { "q3_side" },
            Lines = Story.ChangbanBridge,
            GoldReward = 34, ExpReward = 46,
        });
        Add(new QuestDef
        {
            Id = "q3_collect", Chapter = 2, Kind = QuestKind.Collect,
            Title = "救济伤药", Desc = "拾取 3 份散落的伤药。",
            Requires = new[] { "q3_talk" },
            CollectNeed = 3, CollectType = "herb",
            GoldReward = 40, ExpReward = 55,
        });
        Add(new QuestDef
        {
            Id = "q3_battle", Chapter = 2, Kind = QuestKind.Battle,
            Title = "长坂突围", Desc = "一骑当千，挡住张郃追兵。",
            Requires = new[] { "q3_collect" },
            MapIndex = 2,
            Lines = Story.T3_Intro,
        });

        // ---- 第四章 虎牢 ----
        Add(new QuestDef
        {
            Id = "q4_talk", Chapter = 3, Kind = QuestKind.Talk,
            Title = "虎牢前夜", Desc = "诸侯会盟，共议吕布之患。",
            Requires = new[] { "q3_battle" },
            Lines = new[]
            {
                new Line("", "虎牢关下，夜火连营。传闻吕布已至。连营的犬都不叫了。"),
                new Line("曹操", "温侯之勇，冠绝天下。壮士可愿探一探虚实？", "npc/cao_cao.jpg"),
                new Line("你", "先摸清关前布防。"),
                new Line("曹操", "关前斥候留下的标记还在。你去收一收，便知道他从哪条路下来。", "npc/cao_cao.jpg"),
            },
            GoldReward = 55, ExpReward = 80,
        });
        Add(new QuestDef
        {
            Id = "q4_side", Chapter = 3, Kind = QuestKind.Talk,
            Title = "关下一碗水", Desc = "守关军士把矛拄在地上，等一句不是送死的话。",
            Requires = new[] { "q4_talk" },
            Lines = Story.HulaoGuard,
            GoldReward = 25, ExpReward = 36,
        });
        Add(new QuestDef
        {
            Id = "q4_side2", Chapter = 3, Kind = QuestKind.Talk,
            Title = "数箭的老卒", Desc = "关墙下有个老卒在数箭。数完才知道这关不好过。",
            Requires = new[] { "q4_side" },
            Lines = Story.HulaoVeteran,
            GoldReward = 40, ExpReward = 55,
        });
        Add(new QuestDef
        {
            Id = "q4_collect", Chapter = 3, Kind = QuestKind.Collect,
            Title = "探关布防", Desc = "收集 3 份关前斥候标记。",
            Requires = new[] { "q4_talk" },
            CollectNeed = 3, CollectType = "ore",
            GoldReward = 55, ExpReward = 75,
        });
        Add(new QuestDef
        {
            Id = "q4_battle", Chapter = 3, Kind = QuestKind.Battle,
            Title = "虎牢决战", Desc = "关前无路，与吕布一战。",
            Requires = new[] { "q4_collect" },
            MapIndex = 3,
            Lines = Story.T4_Intro,
        });

        // ---- 第五章 洛阳 ----
        Add(new QuestDef
        {
            Id = "q5_talk", Chapter = 4, Kind = QuestKind.Talk,
            Title = "西入洛阳", Desc = "诸侯联军西向长安，洛阳已在西凉军手里。",
            Requires = new[] { "q4_battle" },
            Lines = new[]
            {
                new Line("", "虎牢关破，诸侯联军一路西行。路过的村子烧成焦炭，水井里填满了人。"),
                new Line("曹操", "董卓已入洛阳。废立天子，焚杀朝士——天下人的怒火，都要烧到那面旗下了。", "npc/cao_cao.jpg"),
                new Line("你", "先探清洛阳城外的布防。"),
                new Line("曹操", "东郊的守军换防时丢下的军报还在，去收一收，我给你看清楚洛阳的虚实。", "npc/cao_cao.jpg"),
            },
            GoldReward = 70, ExpReward = 100,
        });
        Add(new QuestDef
        {
            Id = "q5_side", Chapter = 4, Kind = QuestKind.Talk,
            Title = "宫墙下的孩子", Desc = "有孩子躲在宫墙的缺口里，不敢出来。",
            Requires = new[] { "q5_talk" },
            Lines = new[]
            {
                new Line("", "东郊的断墙下有个孩子，怀里抱着一只死掉的鸟。"),
                new Line("你", "城里的人呢？"),
                new Line("孩子", "都进宫了。进去的人，没有再出来过。", "npc/boy.jpg"),
                new Line("你", "那你为什么在这儿？"),
                new Line("孩子", "我在等他们回来。你能不能……也去看看？", "npc/boy.jpg"),
                new Line("", "你把身上的干粮分了一半给他。"),
            },
            GoldReward = 30, ExpReward = 45,
        });
        Add(new QuestDef
        {
            Id = "q5_side2", Chapter = 4, Kind = QuestKind.Talk,
            Title = "尚书台的残卷", Desc = "烧了一半的尚书台，有个书吏在找一份能证明归属的册子。",
            Requires = new[] { "q5_side" },
            Lines = Story.LuoyangScribe,
            GoldReward = 48, ExpReward = 66,
        });
        Add(new QuestDef
        {
            Id = "q5_side3", Chapter = 4, Kind = QuestKind.Talk,
            Title = "粥棚前的人", Desc = "洛阳城外的粥棚，队伍尽头有个孩子在哭。",
            Requires = new[] { "q5_side2" },
            Lines = Story.LuoyangRefugee,
            GoldReward = 52, ExpReward = 70,
        });
        Add(new QuestDef
        {
            Id = "q5_collect", Chapter = 4, Kind = QuestKind.Collect,
            Title = "东郊军报", Desc = "拾取 3 份洛阳城外的守军部署图。",
            Requires = new[] { "q5_talk" },
            CollectNeed = 3, CollectType = "scroll",
            GoldReward = 70, ExpReward = 95,
        });
        Add(new QuestDef
        {
            Id = "q5_battle", Chapter = 4, Kind = QuestKind.Battle,
            Title = "洛阳之战", Desc = "攻破东郊，斩董卓。",
            Requires = new[] { "q5_collect" },
            MapIndex = 4,
            Lines = Story.T5_Intro,
        });

        // ---- 第六章 官渡 ----
        Add(new QuestDef
        {
            Id = "q6_talk", Chapter = 5, Kind = QuestKind.Talk,
            Title = "官渡对敌", Desc = "袁绍据河北四州，曹操要赌上整个北方。",
            Requires = new[] { "q5_battle" },
            Lines = new[]
            {
                new Line("", "洛阳既下，西凉残部四散奔逃。曹操却在帐中盯着河北舆图，一夜未眠。"),
                new Line("曹操", "袁绍据河北四州，粮秣甲兵皆在他手。这一战，比虎牢关更难。", "npc/cao_cao.jpg"),
                new Line("刘备", "他性缓而多疑，拖下去对我们有利——可拖不起。", "npc/liu_bei.jpg"),
                new Line("你", "那就直取乌巢。烧他的粮。"),
                new Line("曹操", "他的粮道边，还留着旧年行伍的标记。你去收一收，便知道该往哪走。", "npc/cao_cao.jpg"),
            },
            GoldReward = 90, ExpReward = 130,
        });
        Add(new QuestDef
        {
            Id = "q6_side", Chapter = 5, Kind = QuestKind.Talk,
            Title = "牧人的车辙", Desc = "牧人说他年年都在乌巢放牧，从没见过那么多粮车。",
            Requires = new[] { "q6_talk" },
            Lines = new[]
            {
                new Line("", "官渡原野上，牧人赶着羊从两军之间穿过去，谁也没有拦他。"),
                new Line("你", "你不怕？"),
                new Line("牧人", "怕什么。年年都在这儿放牧。粮车一年比一年多，去年过了三千辆，今年……", "npc/muren.jpg"),
                new Line("你", "今年怎么了？"),
                new Line("牧人", "今年他们不烧粮了。他们挖了很深的坑，把粮埋起来，还盖了草。", "npc/muren.jpg"),
                new Line("你", "埋起来？"),
                new Line("牧人", "谁把粮食埋了，谁就是不想打仗。田里的草，都被压出车辙了。", "npc/muren.jpg"),
            },
            GoldReward = 35, ExpReward = 50,
        });
        Add(new QuestDef
        {
            Id = "q6_side2", Chapter = 5, Kind = QuestKind.Talk,
            Title = "磨刀的老兵", Desc = "一个从凉州打到河北的老兵，问你如今是什么身份。",
            Requires = new[] { "q6_side" },
            Lines = Story.GuanduVeteran,
            GoldReward = 42, ExpReward = 58,
        });
        Add(new QuestDef
        {
            Id = "q6_side3", Chapter = 5, Kind = QuestKind.Talk,
            Title = "渡口的最后一坛酒", Desc = "官渡渡口只剩一坛酒，店主说乱世里守着渡口的人说了算。",
            Requires = new[] { "q6_side2" },
            Lines = Story.GuanduBarkeep,
            GoldReward = 55, ExpReward = 72,
        });
        Add(new QuestDef
        {
            Id = "q6_collect", Chapter = 5, Kind = QuestKind.Collect,
            Title = "乌巢粮道", Desc = "收集 3 份袁绍的粮道标记。",
            Requires = new[] { "q6_talk" },
            CollectNeed = 3, CollectType = "ore",
            GoldReward = 90, ExpReward = 125,
        });
        Add(new QuestDef
        {
            Id = "q6_battle", Chapter = 5, Kind = QuestKind.Battle,
            Title = "官渡决战", Desc = "火烧乌巢，直取袁绍。",
            Requires = new[] { "q6_collect" },
            MapIndex = 5,
            Lines = Story.T6_Intro,
        });

        // ---- 第七章 渭水 ----
        Add(new QuestDef
        {
            Id = "q7_talk", Chapter = 6, Kind = QuestKind.Talk,
            Title = "关中未平", Desc = "河北刚定，曹操已举兵西向，要取关中。",
            Requires = new[] { "q6_battle" },
            Lines = new[]
            {
                new Line("", "河北平了。曹操却没有休整 —— 北方的帘子一放，南方的门就露了出来。"),
                new Line("曹操", "河北四州是粮，此战要的是命。关中这些凉州人，打得比袁绍还硬。", "npc/cao_cao.jpg"),
                new Line("刘备", "他们不是来替袁绍报仇的，他们是要自立。马超、韩遂，各打各的主意。", "npc/liu_bei.jpg"),
                new Line("曹操", "所以要先断其首脑。你去，把关中的旗子探明白。", "npc/cao_cao.jpg"),
                new Line("你", "旗子看过了，人心呢？"),
                new Line("曹操", "人心……就看你的刀够不够快了。", "npc/cao_cao.jpg"),
                new Line("曹操", "关中各部的旗号散在营外。你去收一收，我便知道该往哪走。", "npc/cao_cao.jpg"),
            },
            GoldReward = 130, ExpReward = 180,
        });
        Add(new QuestDef
        {
            Id = "q7_side", Chapter = 6, Kind = QuestKind.Talk,
            Title = "关下的凉州兵", Desc = "关下一名西凉军士说，他们跟马超只是为了活。",
            Requires = new[] { "q7_talk" },
            Lines = Story.WeiheLiangjiang,
            GoldReward = 52, ExpReward = 72,
        });
        Add(new QuestDef
        {
            Id = "q7_side2", Chapter = 6, Kind = QuestKind.Talk,
            Title = "潼关旧册", Desc = "一个书吏在旧册上添字，册上的户数从四十万写到了十万。",
            Requires = new[] { "q7_side" },
            Lines = Story.WeiheShusheng,
            GoldReward = 66, ExpReward = 88,
        });
        Add(new QuestDef
        {
            Id = "q7_side3", Chapter = 6, Kind = QuestKind.Talk,
            Title = "渡口的老船工", Desc = "撑了五十年船的老人说：马超的兵，是从西边来的。",
            Requires = new[] { "q7_side2" },
            Lines = Story.WeiheLaochuanfu,
            GoldReward = 78, ExpReward = 100,
        });
        Add(new QuestDef
        {
            Id = "q7_collect", Chapter = 6, Kind = QuestKind.Collect,
            Title = "关中的旗", Desc = "收集 3 份关中各部的旗号标记。",
            Requires = new[] { "q7_talk" },
            CollectNeed = 3, CollectType = "ore",
            GoldReward = 130, ExpReward = 175,
        });
        Add(new QuestDef
        {
            Id = "q7_battle", Chapter = 6, Kind = QuestKind.Battle,
            Title = "渭水 · 潼关", Desc = "关前一战，斩锦马超。",
            Requires = new[] { "q7_collect" },
            MapIndex = 6,
            Lines = Story.T7_Intro,
        });
    }

    static void Add(QuestDef q)
    {
        // 没单独配声望就按「种类 + 章节」给一个默认档位：
        // 对话给一点、收集给一点、副本给大头 —— 越往后越贵。
        if (q.RenownReward <= 0) q.RenownReward = DefaultRenown(q.Chapter, q.Kind);
        // 官阶门槛按章节自动挂上：后面的仗，白身是打不下来的
        if (q.MinRank < 0) q.MinRank = Career.ChapterRank(q.Chapter);
        All.Add(q);
        ById[q.Id] = q;
    }

    static int DefaultRenown(int chapter, QuestKind kind)
    {
        // 刻意压得不高：主线的量刚好够走到下一阶，想要更高就得去做支线或重刷副本。
        int baseVal = kind == QuestKind.Talk ? 8 : (kind == QuestKind.Collect ? 14 : 30);
        int step = kind == QuestKind.Talk ? 4 : (kind == QuestKind.Collect ? 6 : 8);
        return baseVal + chapter * step;
    }

    public static QuestDef Get(string id) =>
        !string.IsNullOrEmpty(id) && ById.TryGetValue(id, out var q) ? q : null;

    public static List<QuestDef> ForChapter(int chapter)
    {
        var list = new List<QuestDef>();
        for (int i = 0; i < All.Count; i++)
            if (All[i].Chapter == chapter) list.Add(All[i]);
        return list;
    }

    public static bool IsDone(Progress p, string id) => p != null && p.QuestDone(id);

    public static bool Unlocked(Progress p, QuestDef q)
    {
        if (q == null) return false;
        // 官阶门槛：位置越高，越要玩家真爬上去才给开门
        if (q.MinRank >= 0 && Career.RankOf(p) < q.MinRank) return false;
        if (q.Requires == null || q.Requires.Length == 0) return true;
        for (int i = 0; i < q.Requires.Length; i++)
            if (!IsDone(p, q.Requires[i])) return false;
        return true;
    }

    /// <summary>为什么还没解锁（给 UI 显示原因）</summary>
    public static string LockReason(Progress p, QuestDef q)
    {
        if (q == null) return "";
        if (q.MinRank >= 0 && Career.RankOf(p) < q.MinRank)
            return "需官阶 " + Career.Ranks[q.MinRank].Title;
        if (q.Requires != null)
            for (int i = 0; i < q.Requires.Length; i++)
                if (!IsDone(p, q.Requires[i]))
                    return "需先完成「" + (Get(q.Requires[i])?.Title ?? q.Requires[i]) + "」";
        return "";
    }

    public static bool ChapterUnlocked(Progress p, int chapter)
    {
        if (chapter <= 0) return true;
        // 上一章战斗任务完成才开下一章
        string prevBattle = "q" + chapter + "_battle";
        if (!IsDone(p, prevBattle)) return false;
        // 还要官阶够：白身打不了官渡
        return Career.RankOf(p) >= Career.ChapterRank(chapter);
    }

    public static void Complete(Progress p, QuestDef q)
    {
        if (q == null || p == null) return;
        bool first = !p.QuestDone(q.Id);
        p.MarkQuestDone(q.Id);
        if (first)
        {
            if (q.GoldReward > 0) p.Data.gold += q.GoldReward;
            if (q.ExpReward > 0) p.GainExp(q.ExpReward);
        }
        if (q.Kind == QuestKind.Battle && q.MapIndex >= 0 && q.MapIndex < p.Data.cleared.Length)
            p.Data.cleared[q.MapIndex] = true;
        p.Save();
        // 声望单独走 Game：升阶要弹提示、播君主评语。
        // 副本可以重刷：重刷只给一半声望（练兵也有功），不重复给经验与铜钱。
        if (q.RenownReward > 0 && Game.I != null)
        {
            int give = first ? q.RenownReward : q.RenownReward / 2;
            if (give > 0) Game.I.OnRenownGained(give);
        }
    }

    public static QuestDef BattleQuestForMap(int mapIndex)
    {
        for (int i = 0; i < All.Count; i++)
            if (All[i].Kind == QuestKind.Battle && All[i].MapIndex == mapIndex)
                return All[i];
        return null;
    }

    /// <summary>收集任务用的轻量地图（无敌军）</summary>
    public static MapDef MakeCollectMap(QuestDef q)
    {
        var baseMap = DB.Maps[Mathf.Clamp(q.Chapter, 0, DB.Maps.Count - 1)];
        var pickups = new PickupDef[q.CollectNeed + 2];
        var spots = new[]
        {
            new Vector2(4f, 10f), new Vector2(-10f, 8f), new Vector2(12f, 4f),
            new Vector2(-6f, -4f), new Vector2(8f, -8f), new Vector2(-12f, -2f),
        };
        for (int i = 0; i < pickups.Length; i++)
        {
            var s = spots[i % spots.Length];
            pickups[i] = new PickupDef
            {
                X = s.x + (i > 5 ? 2f : 0f),
                Z = s.y,
                Type = i < q.CollectNeed ? q.CollectType : "coin",
            };
        }
        return new MapDef
        {
            Id = "quest_" + q.Id,
            Chapter = baseMap.Chapter,
            Name = baseMap.Name,
            Subtitle = q.Title,
            LevelRange = baseMap.LevelRange,
            Decor = baseMap.Decor,
            Fog = baseMap.Fog,
            GroundTint = baseMap.GroundTint,
            Boss = null,
            Mobs = new string[0],
            Packs = new PackDef[0],
            Pickups = pickups,
            Intro = null,
            Outro = null,
            Ending = q.Title,
        };
    }
}

/// <summary>天赋节点</summary>
public class TalentDef
{
    public string Id, Name, Desc;
    public int MaxRank = 3;
    public int Col, Row; // UI 布局
    public System.Func<int, string> EffectText;
}

public static class Talents
{
    public static readonly TalentDef[] Nodes =
    {
        new TalentDef { Id = "blade", Name = "锋锐", Desc = "攻击力提升", Col = 0, Row = 0,
            EffectText = r => $"攻击 +{r * 3}" },
        new TalentDef { Id = "cruel", Name = "残心", Desc = "暴击率提升", Col = 0, Row = 1,
            EffectText = r => $"暴击 +{r * 2}%" },
        new TalentDef { Id = "rage", Name = "战意", Desc = "重击伤害提升", Col = 0, Row = 2,
            EffectText = r => $"重击 +{r * 6}%" },

        new TalentDef { Id = "iron", Name = "铁骨", Desc = "气血上限提升", Col = 1, Row = 0,
            EffectText = r => $"气血 +{r * 18}" },
        new TalentDef { Id = "guard", Name = "坚壁", Desc = "防御提升", Col = 1, Row = 1,
            EffectText = r => $"防御 +{r * 2}" },
        new TalentDef { Id = "endure", Name = "不屈", Desc = "受击减伤", Col = 1, Row = 2,
            EffectText = r => $"减伤 +{r * 3}%" },

        new TalentDef { Id = "wind", Name = "疾风", Desc = "移动速度提升", Col = 2, Row = 0,
            EffectText = r => $"身法 +{r * 3}%" },
        new TalentDef { Id = "breath", Name = "吐纳", Desc = "气力回复提升", Col = 2, Row = 1,
            EffectText = r => $"回气 +{r * 8}%" },
        new TalentDef { Id = "focus", Name = "凝神", Desc = "技能冷却缩短", Col = 2, Row = 2,
            EffectText = r => $"冷却 -{r * 4}%" },
    };

    public static int Rank(Progress p, int index)
    {
        if (p?.Data?.talentRank == null || index < 0 || index >= p.Data.talentRank.Length) return 0;
        return p.Data.talentRank[index];
    }

    public static bool CanUnlock(Progress p, int index)
    {
        if (p == null || index < 0 || index >= Nodes.Length) return false;
        if (p.Data.talentPts <= 0) return false;
        int r = Rank(p, index);
        if (r >= Nodes[index].MaxRank) return false;
        // 同列上一行需至少 1 点（第一行无前置）
        var n = Nodes[index];
        if (n.Row > 0)
        {
            for (int i = 0; i < Nodes.Length; i++)
                if (Nodes[i].Col == n.Col && Nodes[i].Row == n.Row - 1 && Rank(p, i) < 1)
                    return false;
        }
        return true;
    }

    public static bool Unlock(Progress p, int index)
    {
        if (!CanUnlock(p, index)) return false;
        if (p.Data.talentRank == null || p.Data.talentRank.Length != Nodes.Length)
            p.Data.talentRank = new int[Nodes.Length];
        p.Data.talentPts -= 1;
        p.Data.talentRank[index] += 1;
        return true;
    }

    public static void ApplyToStats(Progress p, ref Stats s)
    {
        if (p?.Data?.talentRank == null) return;
        int[] r = p.Data.talentRank;
        // blade / cruel / rage
        if (r.Length > 0) s.Atk += r[0] * 3f;
        if (r.Length > 1) s.Crit += r[1] * 0.02f;
        if (r.Length > 2) s.HeavyMul += r[2] * 0.06f;
        // iron / guard / endure（减伤用 Def 近似）
        if (r.Length > 3) s.MaxHP += r[3] * 18f;
        if (r.Length > 4) s.Def += r[4] * 2f;
        if (r.Length > 5) s.Def += r[5] * 1.5f;
        // wind / breath / focus
        if (r.Length > 6) s.Speed *= 1f + r[6] * 0.03f;
        if (r.Length > 7) s.QiMul *= 1f + r[7] * 0.08f;
        // focus: 冷却在技能侧读 Progress.TalentCdMul
    }

    public static float CdMul(Progress p)
    {
        int r = Rank(p, 8);
        return 1f - r * 0.04f;
    }
}
