using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 野外偶遇：走在路上会撞见的小事。
///
/// 这是"小人物"的存在感来源 —— 你不是天生的英雄，
/// 你是靠一桩桩小事、一个个选择，被这个世界记住的。
/// 事件给的东西不多，但每一样都跟仕途挂钩：声望、铜钱、经验、偶尔的机遇。
/// </summary>
public class WorldEvents
{
    public enum EvKind { Help, Find, Ambush, Merchant, Veteran, Shrine }

    public class EvDef
    {
        public EvKind Kind;
        public string Id;         // 事件 id（存档用，兼作连锁的节点名）
        public string Title;      // 事件名
        public string Text;       // 描述
        public int Renown;        // 声望
        public int Gold;
        public int Exp;
        public string Portrait;   // 立绘（可空）
        public Line[] Lines;      // 触发的对话
        public int Cooldown = 60; // 冷却（秒）
        /// <summary>前置事件 id：只有玩家遇过它，这个事件才会出现（回访型偶遇）。</summary>
        public string After;
        /// <summary>被这个事件触发后记下的旗号（写入 flagsCsv，作为永久痕迹）。</summary>
        public string SetsFlag;
    }

    /// <summary>事件点：一个可交互的世界物件</summary>
    public class EvSpot
    {
        public EvDef Def;
        public GameObject Go;
        public SpriteRenderer Body;
        public SpriteRenderer Glow;
        public Vector3 Pos;
        public float T;       // 出现时间
        public bool Taken;
    }

    public static readonly List<EvDef> Defs = new List<EvDef>();

    static WorldEvents()
    {
        // —— 助人：乱世里最不值钱、也最值钱的选择 ——
        Defs.Add(new EvDef
        {
            Kind = EvKind.Help,
            Id = "ev_wounded",
            Title = "路边的伤者",
            Text = "一个受伤的人靠在路边，还有气。",
            Renown = 12, Gold = 0, Exp = 20,
            Lines = new[]
            {
                new Line("", "路边一个人靠着一株歪树，腿上缠着已经被血浸透的布。"),
                new Line("你", "还能走吗？"),
                new Line("伤者", "……能。你要是有药，我给你磕头。没有的话，也不必停。"),
                new Line("你", "我有药。"),
                new Line("伤者", "……你和我见过的那些人不一样。他们也是人，只是眼里只有事。"),
            },
        });

        Defs.Add(new EvDef
        {
            Kind = EvKind.Help,
            Id = "ev_children",
            Title = "逃难的孩子",
            Text = "两个孩子抱着一个空口袋，蹲在路中间。",
            Renown = 10, Exp = 16,
            Lines = new[]
            {
                new Line("", "两个孩子蹲在路中间，怀里抱着一个空口袋，谁也不说话。"),
                new Line("你", "多久没吃了？"),
                new Line("孩子", "……三天。娘说，往南走就有粥棚。"),
                new Line("你", "往南是关外，粥棚早烧了。往北去吧，那边有个渡口，还有人煮东西。"),
                new Line("孩子", "真的？"),
                new Line("你", "真的。我走过。"),
            },
        });

        // —— 拾遗：小人物的钱大多是这样来的 ——
        Defs.Add(new EvDef
        {
            Kind = EvKind.Find,
            Id = "ev_bundle",
            Title = "道旁的包袱",
            Text = "草里露出一个包袱的一角。",
            Renown = 0, Gold = 45, Exp = 12,
            Lines = new[]
            {
                new Line("", "草里露出一个包袱的一角，布是旧的，铜扣却是新的。"),
                new Line("你", "……先人失物，若能还回去，是这一笔。"),
                new Line("你", "算了。我连自己的下一顿饭都还没着落。"),
            },
        });

        Defs.Add(new EvDef
        {
            Kind = EvKind.Find,
            Id = "ev_blade",
            Title = "断刃",
            Text = "半截断刃插在土里，刃口还是亮的。",
            Renown = 0, Gold = 0, Exp = 45,
            Lines = new[]
            {
                new Line("", "土里插着半截断刃，刃口还亮着 —— 是被硬生生崩断的。"),
                new Line("你", "能崩断它的东西，力气不小。"),
                new Line("你", "这刃还能用。磨一磨，够用了。"),
            },
        });

        // —— 埋伏：不是所有好事都等着你 ——
        Defs.Add(new EvDef
        {
            Kind = EvKind.Ambush,
            Id = "ev_bow",
            Title = "林中的弓弦",
            Text = "树影里有东西在动，弓弦已经拉满了。",
            Renown = 6, Gold = 35, Exp = 40,
            Lines = new[]
            {
                new Line("", "树影里一支箭搭在弦上，正对着你的喉咙。"),
                new Line("劫匪", "身上有钱么？有就留下，人可以走。"),
                new Line("你", "钱没有。命也给你留着，但我得看看你有多少人。"),
            },
        });

        Defs.Add(new EvDef
        {
            Kind = EvKind.Ambush,
            Id = "ev_barricade",
            Title = "拒马后的人",
            Text = "道口的拒马后面有人在喊：此路不通。",
            Renown = 8, Gold = 25, Exp = 30,
            Lines = new[]
            {
                new Line("", "道口横着拒马，后面的人喊：此路不通，报个名号。"),
                new Line("你", "无名之辈。"),
                new Line("那人", "……无名之辈，敢过这条道？"),
                new Line("你", "无名之辈，才没人盯着。"),
            },
        });

        // —— 义商：乱世里也有不肯发 wartime 财的人 ——
        Defs.Add(new EvDef
        {
            Kind = EvKind.Merchant,
            Id = "ev_teastall",
            Title = "路边的茶摊",
            Text = "一个老兵在路边支了个摊，卖的是热水。",
            Renown = 5, Gold = 20, Exp = 15,
            Lines = new[]
            {
                new Line("", "路边支着个小摊，锅里烧着水，摊主是个瘸腿的老兵。"),
                new Line("摊主", "一碗热水两个钱。走了这么久的路，你比我更渴。"),
                new Line("你", "两文钱在眼下是两顿粮。"),
                new Line("摊主", "那就欠着。等你哪天发达了，记得这锅里还有热水。"),
            },
        });

        // —— 老兵：给小人物指路的人 ——
        Defs.Add(new EvDef
        {
            Kind = EvKind.Veteran,
            Id = "ev_veteran",
            Title = "路边的老卒",
            Text = "一个老兵在路边磨刀，磨得很慢。",
            Renown = 15, Exp = 35,
            Lines = new[]
            {
                new Line("", "一个老兵在路边磨刀，磨得很慢，像在做一件不着急的事。"),
                new Line("老兵", "刀钝了，人就老了。你知道你现在最缺什么么？"),
                new Line("你", "钱？"),
                new Line("老兵", "名号。钱花完就没了，名号是拿命换的，换来就一直在。"),
                new Line("老兵", "往北走，那边有贵人。贵人不知道你是谁，可他会知道你能干什么。"),
            },
        });

        // —— 破庙：偶尔的一点运气 ——
        Defs.Add(new EvDef
        {
            Kind = EvKind.Shrine,
            Id = "ev_shrine",
            Title = "荒废的山神庙",
            Text = "小庙的神像歪了，供桌上还有一点没烧完的香。",
            Renown = 8, Gold = 30, Exp = 25,
            Lines = new[]
            {
                new Line("", "荒庙里神像歪着，供桌上一层灰，只有香炉里还有半炷香。"),
                new Line("你", "……神也不管事了。"),
                new Line("你", "也好。都不管事，就没人问我前程了。"),
            },
        });

        // ============ 连锁：先帮过的人，后来还会再出现 ============
        // 「路边老卒」→「老卒回访」：你听过他一句话，他记着。
        Defs.Add(new EvDef
        {
            Kind = EvKind.Veteran,
            Id = "ev_veteran_return",
            After = "ev_veteran",
            Title = "回访的老卒",
            Text = "又是那个磨刀的老兵，这回他先认出了你。",
            Renown = 20, Gold = 30, Exp = 40,
            SetsFlag = "ev_veteran_kept",
            Lines = new[]
            {
                new Line("", "那个磨刀的老兵还在原地，刀却已经磨好了。"),
                new Line("老兵", "是你。上回你说你在桃林里，我记住了。"),
                new Line("你", "我只是没走开。"),
                new Line("老兵", "没走开，在这世道上比冲在前面的更难得。"),
                new Line("老兵", "拿着这个 —— 我年轻时攒的护身符。别嫌旧。"),
                new Line("", "他塞过来一块磨得发亮的旧铁片，然后转身走了。"),
            },
        });

        // 「逃难的孩子」→「孩子的回信」：你指的路，救了两条命。
        Defs.Add(new EvDef
        {
            Kind = EvKind.Help,
            Id = "ev_children_return",
            After = "ev_children",
            Title = "孩子的回信",
            Text = "一个半大的孩子跑过来，手里攥着东西。",
            Renown = 22, Gold = 0, Exp = 30,
            SetsFlag = "ev_children_saved",
            Lines = new[]
            {
                new Line("", "一个半大的孩子跑过来，跑得气喘吁吁，怀里抱着个布包。"),
                new Line("孩子", "你是……那天指路的那个！"),
                new Line("你", "你娘呢？"),
                new Line("孩子", "在渡口熬过来了。她说这包米是谢你的，你拿着。"),
                new Line("孩子", "她还让我带句话：'路上的人都说不清自己姓什么，可我记得你的脸。'"),
            },
        });

        // 「路边的伤者」→「伤者的报答」：你停过的那一次，他记了一路。
        Defs.Add(new EvDef
        {
            Kind = EvKind.Help,
            Id = "ev_wounded_return",
            After = "ev_wounded",
            Title = "伤者的报答",
            Text = "一个走得一瘸一拐的人拦住你，塞给你一样东西。",
            Renown = 18, Gold = 40, Exp = 30,
            SetsFlag = "ev_wounded_helped",
            Lines = new[]
            {
                new Line("", "一个人一瘸一拐地拦在你面前，然后单膝跪下。"),
                new Line("伤者", "我这条腿是你给的。上回你把药都给了我，连名字都没留。"),
                new Line("你", "起来。"),
                new Line("伤者", "我在道上认得几个跑腿的。这点东西你拿着，往北走用得上。"),
                new Line("", "他塞过来一小袋铜钱和一张写了字的口信。"),
            },
        });

        // 「道旁的包袱」→「失主寻来」：东西还回去，才叫一笔。
        Defs.Add(new EvDef
        {
            Kind = EvKind.Find,
            Id = "ev_bundle_owner",
            After = "ev_bundle",
            Title = "寻来的失主",
            Text = "一个老妇人沿路打听那个包袱的下落。",
            Renown = 16, Gold = 0, Exp = 25,
            SetsFlag = "ev_bundle_returned",
            Lines = new[]
            {
                new Line("", "一个老妇人沿着路打听，问有没有人见过一个旧布包。"),
                new Line("你", "……什么样的？"),
                new Line("老妇人", "铜扣是新的。我儿子的东西， 他死在长坂上了。"),
                new Line("", "你把包袱取出来，递过去。"),
                new Line("老妇人", "……你替我收着，还等我找。"),
                new Line("老妇人", "我不识字，也不会说话。可我记得你的样子。"),
            },
        });
    }

    /// <summary>生成一个事件点的视觉对象。返回 spot 由调用方（WorldExplore）持有。</summary>
    public static EvSpot Spawn(Transform parent, Vector3 pos, EvDef def, float now)
    {
        var spot = new EvSpot { Def = def, Pos = pos, T = now };

        var go = new GameObject("ev_" + def.Title);
        go.transform.SetParent(parent, false);
        go.transform.position = pos;
        spot.Go = go;

        var ring = new GameObject("ring").transform;
        ring.SetParent(go.transform, false);
        ring.localPosition = new Vector3(0f, 0.05f, 0f);
        ring.rotation = Quaternion.Euler(-90f, 0f, 0f);
        spot.Glow = ring.gameObject.AddComponent<SpriteRenderer>();
        spot.Glow.sprite = Tex.CenterSprite(Tex.Ring, 1.7f);
        spot.Glow.color = new Color(0.95f, 0.85f, 0.45f, 0.55f);

        var pillar = new GameObject("pillar").transform;
        pillar.SetParent(go.transform, false);
        pillar.localPosition = new Vector3(0f, 1.1f, 0f);
        spot.Body = pillar.gameObject.AddComponent<SpriteRenderer>();
        spot.Body.sprite = Tex.CenterSprite(Tex.Glow, 1.5f);
        spot.Body.color = new Color(1f, 0.88f, 0.5f, 0.4f);

        return spot;
    }

    /// <summary>
    /// 挑一个事件。
    ///
    /// 规则：
    /// - 连锁事件（After 非空）只有在「前置事件已经遇过」且「自己还没遇过」时才可能出现；
    ///   一旦可能，优先级高于普通事件（回访比随机事件更该被看见）。
    /// - 已经遇过的普通事件不再重复出现。
    /// </summary>
    public static EvDef Pick(System.Random rng, Progress p)
    {
        if (Defs.Count == 0) return null;

        // 1) 先看有没有「回访」事件可以触发
        var chain = new List<EvDef>();
        for (int i = 0; i < Defs.Count; i++)
        {
            var d = Defs[i];
            if (string.IsNullOrEmpty(d.After)) continue;
            if (p != null && (p.EvSeen(d.Id) || !p.EvSeen(d.After))) continue;
            chain.Add(d);
        }
        if (chain.Count > 0) return chain[rng.Next(chain.Count)];

        // 2) 没有回访就按稀有度随机（避开已经遇过的）
        int r = rng.Next(100);
        EvKind kind;
        if (r < 30) kind = EvKind.Help;
        else if (r < 50) kind = EvKind.Find;
        else if (r < 68) kind = EvKind.Ambush;
        else if (r < 82) kind = EvKind.Merchant;
        else if (r < 93) kind = EvKind.Veteran;
        else kind = EvKind.Shrine;

        var pool = new List<EvDef>();
        for (int i = 0; i < Defs.Count; i++)
        {
            var d = Defs[i];
            if (!string.IsNullOrEmpty(d.After)) continue;             // 连锁只在上面触发
            if (p != null && p.EvSeen(d.Id)) continue;
            if (d.Kind == kind) pool.Add(d);
        }
        if (pool.Count > 0) return pool[rng.Next(pool.Count)];

        // 3) 同类的都遇过了，退回到任意未遇过的事件
        var rest = new List<EvDef>();
        for (int i = 0; i < Defs.Count; i++)
        {
            var d = Defs[i];
            if (!string.IsNullOrEmpty(d.After)) continue;
            if (p != null && p.EvSeen(d.Id)) continue;
            rest.Add(d);
        }
        if (rest.Count > 0) return rest[rng.Next(rest.Count)];
        return Defs[rng.Next(Defs.Count)];
    }
}
