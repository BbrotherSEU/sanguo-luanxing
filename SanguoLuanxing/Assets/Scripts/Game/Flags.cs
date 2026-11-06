using System.Collections.Generic;

/// <summary>
/// 旗号（选择印记）：关键节点的选择会留下旗号，终局据此给出不同的路。
///
/// 设计要点：
/// - 一个旗号属于一个「维度」（如 长坂 / 洛阳 / 渭水），同维度互斥；
///   玩家重刷副本改主意时，后选会顶掉先选（Flags.Set 里清同维度兄弟）。
/// - Core 的旗号会在结局画面上串成「你走的这条路」；非 Core 的只补一句评语。
/// - 旗号存在 Progress.Data.flagsCsv（| 分隔），由 Game.PickChoice 写入。
/// - 官阶决定结局的「高度」，旗号决定结局的「味道」：两者叠加。
/// </summary>
public class FlagDef
{
    public string Id;
    public string Dim;      // 维度 id：同维度互斥
    public string Title;    // 旗号名（结局里显示）
    public string Alt;      // 同一维度的另一条路（人物页给玩家看「另一种可能」）
    public string Ending;   // 结局里追加的一句
    public bool Core;       // 是否进入「你走的这条路」主线
    public int Order;       // 结局里的排列次序（越小越靠前）
}

public static class Flags
{
    /// 维度顺序 = 故事顺序（桃林 → 汜水 → 长坂 → 洛阳 → 渭水）
    public static readonly string[] Dims = { "origin", "sishui", "changban", "luoyang", "weihe" };

    /// 维度标题（人物页 · 旗号一览用）
    public static readonly string[] DimNames =
    {
        "桃林 · 出处", "汜水 · 一碗酒", "长坂 · 断后", "洛阳 · 一城", "渭水 · 关中",
    };

    public static readonly FlagDef[] Defs =
    {
        // ---- 第一章 · 桃林 ----
        new FlagDef { Id = "swore",    Dim = "origin",   Title = "桃园立誓", Alt = "先走一走", Core = true, Order = 10,
            Ending = "桃园那日你应下的话，一句都没有忘。" },
        new FlagDef { Id = "wander",   Dim = "origin",   Title = "先走一走", Alt = "桃园立誓", Core = true, Order = 11,
            Ending = "你当年没有立誓，只是先走出了桃林。后面的路，都是自己挑的。" },

        // ---- 第二章 · 汜水关 ----
        new FlagDef { Id = "drank",    Dim = "sishui",   Title = "温酒出战", Alt = "刀未出鞘", Core = true, Order = 20,
            Ending = "那碗酒你接了。温酒斩华雄的传闻里，有你的一半。" },
        new FlagDef { Id = "sober",    Dim = "sishui",   Title = "刀未出鞘", Alt = "温酒出战", Core = true, Order = 21,
            Ending = "你没接那碗酒。刀出鞘之前，你不想让任何东西替你壮胆。" },

        // ---- 第三章 · 长坂坡 ----
        new FlagDef { Id = "mercy",    Dim = "changban", Title = "长坂护民", Alt = "长坂斩将", Core = true, Order = 30,
            Ending = "长坂桥上你保的是人，不是粮。史书不记，桥下的人记得。" },
        new FlagDef { Id = "ruthless", Dim = "changban", Title = "长坂斩将", Alt = "长坂护民", Core = true, Order = 31,
            Ending = "长坂桥上你只信手里的刀。记住你的，也先是刀。" },

        // ---- 第五章 · 洛阳 ----
        new FlagDef { Id = "burn",     Dim = "luoyang",  Title = "献策焚城", Alt = "力保洛阳", Core = true, Order = 40,
            Ending = "洛阳的火是你献的策。那把火之后会发生什么，你比谁都清楚。" },
        new FlagDef { Id = "spare",    Dim = "luoyang",  Title = "力保洛阳", Alt = "献策焚城", Core = true, Order = 41,
            Ending = "你替洛阳留了一城百姓，也替自己留了一段说不清的软肋。" },
        // 洛阳粥棚（与「焚城」是不同的一次抉择，故另立维度，两者可以并存）
        new FlagDef { Id = "saved",    Dim = "refugee",  Title = "分粟救人", Alt = "转身就走", Core = false, Order = 45,
            Ending = "半囊粟换来的那一声「你这样的人不多」，你记了很久。" },
        new FlagDef { Id = "walked",   Dim = "refugee",  Title = "转身就走", Alt = "分粟救人", Core = false, Order = 46,
            Ending = "那天你什么也没说就走了。队伍安静了很久，没有人骂你。" },

        // ---- 第六章 · 官渡 ----
        new FlagDef { Id = "ambitious", Dim = "guandu",   Title = "志在有名", Alt = "仍是无名", Core = false, Order = 50,
            Ending = "老兵说要敬你一碗。他后来没机会——你走得太快。" },
        new FlagDef { Id = "nameless", Dim = "guandu",   Title = "仍是无名", Alt = "志在有名", Core = false, Order = 51,
            Ending = "你说还没人给你这个名号。你确实靠本事走到了这里。" },

        // ---- 第七章 · 渭水 ----
        new FlagDef { Id = "pardon",   Dim = "weihe",    Title = "渭水释降", Alt = "渭水斩尽", Core = true, Order = 60,
            Ending = "马超活着回了关中。日后是敌是友，都在你这一念之间。" },
        new FlagDef { Id = "slay",     Dim = "weihe",    Title = "渭水斩尽", Alt = "渭水释降", Core = true, Order = 61,
            Ending = "追骑出关那天，渭水的雪化得比往年早。" },

        // ---- 野外偶遇留下的痕迹（非主线，只在结局里补一句）----
        new FlagDef { Id = "ev_veteran_kept",  Dim = "ev_veteran", Title = "老卒记情", Core = false, Order = 70,
            Ending = "那个磨刀的老卒还活着。你不知道他的名字，可他记得你的。" },
        new FlagDef { Id = "ev_children_saved", Dim = "ev_children", Title = "孩子生还", Core = false, Order = 71,
            Ending = "渡口那边的孩子活下来了。乱世里，这已经算是好消息。" },
        new FlagDef { Id = "ev_wounded_helped", Dim = "ev_wounded", Title = "救过伤者", Core = false, Order = 72,
            Ending = "那个伤者的腿保住了。他后来在道上替你说过话。" },
        new FlagDef { Id = "ev_bundle_returned", Dim = "ev_bundle", Title = "物归原主", Core = false, Order = 73,
            Ending = "那个包袱回到了它主人手里。世上少了一件失物，多了一件归还。" },
    };

    public static FlagDef Get(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        for (int i = 0; i < Defs.Length; i++) if (Defs[i].Id == id) return Defs[i];
        return null;
    }

    /// <summary>落旗号：同维度的互斥旗号会被顶掉（改主意的场景）。</summary>
    public static void Set(Progress p, string id)
    {
        if (p == null || string.IsNullOrEmpty(id)) return;
        var d = Get(id);
        if (d != null)
            for (int i = 0; i < Defs.Length; i++)
                if (Defs[i].Dim == d.Dim && Defs[i].Id != d.Id) p.ClearFlag(Defs[i].Id);
        p.SetFlag(id);
    }

    /// <summary>某个维度当前留下的旗号（没有则 null）。</summary>
    public static FlagDef OfDim(Progress p, string dim)
    {
        if (p == null) return null;
        for (int i = 0; i < Defs.Length; i++)
            if (Defs[i].Dim == dim && p.HasFlag(Defs[i].Id)) return Defs[i];
        return null;
    }

    /// <summary>走过的路（主线维度）：按故事顺序返回，未走过的维度为 null。</summary>
    public static FlagDef[] Road(Progress p)
    {
        var outp = new FlagDef[Dims.Length];
        for (int i = 0; i < Dims.Length; i++) outp[i] = OfDim(p, Dims[i]);
        return outp;
    }

    /// <summary>「桃园立誓 · 长坂护民 · 力保洛阳」这样的一行路（只含主线旗号）。</summary>
    public static string RoadText(Progress p)
    {
        var road = Road(p);
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < road.Length; i++)
        {
            if (road[i] == null) continue;
            if (sb.Length > 0) sb.Append(" · ");
            sb.Append(road[i].Title);
        }
        return sb.ToString();
    }

    /// <summary>所有点亮的旗号（主线 + 支线），按故事顺序 —— 结局逐条追加评语用。</summary>
    public static List<FlagDef> AllTaken(Progress p)
    {
        var outp = new List<FlagDef>();
        if (p == null) return outp;
        for (int i = 0; i < Defs.Length; i++)
            if (p.HasFlag(Defs[i].Id)) outp.Add(Defs[i]);
        outp.Sort((a, b) => a.Order - b.Order);
        return outp;
    }
}
