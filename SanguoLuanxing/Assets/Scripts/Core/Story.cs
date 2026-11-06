using UnityEngine;

public class Choice
{
    public string Text;
    public Line[] Follow;
    public string Flag;
    public Choice(string text, Line[] follow = null, string flag = null)
    {
        Text = text; Follow = follow; Flag = flag;
    }
}

public class Line
{
    public string Who;
    public string Text;
    public string Img;      // 对话立绘（相对 assets/ 的路径），null = 旁白
    public Choice[] Choices;
    public Line(string who, string text, string img = null) { Who = who; Text = text; Img = img; }
    public Line Opts(params Choice[] c) { Choices = c; return this; }
}

/// <summary>
/// 剧情文本（与网页版 maps.js 保持一致）。
/// 代码里的台词是内置默认值；StreamingAssets/data/story.json 里出现的段落会覆盖它，
/// 这样活动剧情 / 台词润色不用重新编译就能改。
/// </summary>
public static partial class Story
{
    // 外部覆盖表：段落 id -> 台词
    static System.Collections.Generic.Dictionary<string, Line[]> overrides;

    /// <summary>从外部 JSON 加载剧情覆盖表。没文件就不覆盖。</summary>
    public static void LoadOverrides()
    {
        overrides = null;
        string path = System.IO.Path.Combine(Application.streamingAssetsPath, DataDb.Dir, "story.json");
        if (!System.IO.File.Exists(path)) return;
        try
        {
            string json = System.IO.File.ReadAllText(path);
            var table = JsonUtility.FromJson<StoryTable>(json);
            if (table == null || table.Scenes == null) return;
            overrides = new System.Collections.Generic.Dictionary<string, Line[]>();
            for (int i = 0; i < table.Scenes.Length; i++)
            {
                var s = table.Scenes[i];
                if (s == null || string.IsNullOrEmpty(s.Id) || s.Lines == null) continue;
                overrides[s.Id] = Conv.ToLines(s.Lines);
            }
            Debug.Log("[Story] 外部剧情覆盖 " + overrides.Count + " 段");
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("[Story] 读取 story.json 失败，用内置台词：" + e.Message);
        }
    }

    /// <summary>取一段剧情：外部有就用外部的，没有就用内置的。</summary>
    public static Line[] Get(string id, Line[] fallback)
    {
        if (overrides != null)
        {
            Line[] v;
            if (overrides.TryGetValue(id, out v) && v != null && v.Length > 0) return v;
        }
        return fallback;
    }

    /// <summary>JSON 剧情表：段落 id -> 台词数组</summary>
    [System.Serializable] public class StoryTable
    {
        public StoryScene[] Scenes;
    }

    [System.Serializable] public class StoryScene
    {
        public string Id;
        public StoryLine[] Lines;
    }

    [System.Serializable] public class StoryLine
    {
        public string Who;
        public string Text;
        public string Img;
        public StoryChoice[] Choices;
    }

    [System.Serializable] public class StoryChoice
    {
        public string Text;
        public string Flag;
        public StoryFollow[] Follow;
    }

    /// <summary>
    /// 选项的后续台词。
    /// 单独一个类型、不再往下挂 Choices，是为了打断类型递归：
    /// StoryLine → Choices → Follow → StoryLine 若自成环，JsonUtility 会因为
    /// 序列化深度超过 10 层直接拒绝解析（整个外部覆盖表就都读不进来）。
    /// 跟随台词本身不再带选项，与内置剧情的写法一致。
    /// </summary>
    [System.Serializable] public class StoryFollow
    {
        public string Who;
        public string Text;
        public string Img;
    }

    public static class Conv
    {
        public static Line[] ToLines(StoryLine[] src)
        {
            if (src == null) return null;
            var outp = new Line[src.Length];
            for (int i = 0; i < src.Length; i++) outp[i] = ToLine(src[i]);
            return outp;
        }

        static Line ToLine(StoryLine s)
        {
            if (s == null) return null;
            var l = new Line(s.Who ?? "", s.Text ?? "", string.IsNullOrEmpty(s.Img) ? null : s.Img);
            if (s.Choices != null && s.Choices.Length > 0)
            {
                var cs = new Choice[s.Choices.Length];
                for (int i = 0; i < s.Choices.Length; i++)
                {
                    var c = s.Choices[i];
                    cs[i] = new Choice(c.Text ?? "", c.Follow != null ? ToFollow(c.Follow) : null, c.Flag);
                }
                l.Choices = cs;
            }
            return l;
        }

        static Line[] ToFollow(StoryFollow[] src)
        {
            if (src == null) return null;
            var outp = new Line[src.Length];
            for (int i = 0; i < src.Length; i++)
            {
                var f = src[i];
                outp[i] = f == null ? null
                    : new Line(f.Who ?? "", f.Text ?? "", string.IsNullOrEmpty(f.Img) ? null : f.Img);
            }
            return outp;
        }
    }
}

/// <summary>剧情文本（与网页版 maps.js 保持一致）。
/// 代码里的台词是内置默认值；StreamingAssets/data/story.json 里出现的段落会覆盖它。</summary>
public static partial class Story
{
    static Line N(string t) => new Line("", t);
    static Line Q(string n, string t, string img = null) => new Line(n, t, img);

    // 立绘路径
    const string LiuBei = "npc/liu_bei.jpg";
    const string CaoCao = "npc/cao_cao.jpg";
    const string Suwan = "npc/suwan.jpg";
    const string Afu = "npc/afu.jpg";
    const string Scout = "npc/scout.jpg";
    const string Wine = "npc/wine.jpg";
    const string Refugee = "npc/refugee.jpg";
    const string Guard = "npc/guard.jpg";
    const string Cheng = "char/boss_cheng.jpg";
    const string HuaXiong = "char/boss_huaxiong.jpg";
    const string ZhangHe = "char/boss_zhanghe.jpg";
    const string LvBu = "char/boss_lvbu.jpg";
    const string DongZhuo = "char/boss_dongzhuo.jpg";
    const string YuanShao = "char/boss_yuanshao.jpg";
    const string MaChao = "char/boss_machao.jpg";
    const string LiangJiang = "npc/liangjiang.jpg";
    const string ShuSheng = "npc/shusheng.jpg";
    const string ChuanFu = "npc/chuanfu.jpg";
    const string LaoChuanFu = "npc/laochuanfu.jpg";
    // 去重后的配角立绘（每个角色一张，不再多人共用一张）
    const string ZhaoCang = "npc/zhaocang.jpg";   // 赵仓（斥候出身）
    const string Muren = "npc/muren.jpg";         // 牧人
    const string Fisher = "npc/fisher.jpg";       // 渔翁
    const string Boy = "npc/boy.jpg";             // 孩童
    const string Herd = "npc/herd.jpg";           // 牧童
    const string Scribe = "npc/scribe.jpg";       // 洛阳书吏
    const string Elder = "npc/elder.jpg";         // 老兵
    const string Postman = "npc/postman.jpg";     // 驿卒
    const string Clerk = "npc/clerk.jpg";         // 关吏
    const string Merchant = "npc/merchant.jpg";   // 店主
    const string Veteran = "npc/veteran.jpg";     // 老卒
    const string Weaver = "npc/weaver.jpg";       // 织工
    const string Maiden = "npc/maiden.jpg";       // 织女
    const string Woman = "npc/woman.jpg";         // 妇人

    public static Line[] Q1_World => Get("Q1_World", Def_Q1_World);

    static readonly Line[] Def_Q1_World =
    {
        N("桃林深处，三人正对天盟誓。为首者见你苏醒，拱手相迎。"),
        Q("刘备", "这位壮士，涿郡城外黄巾四起，你若无处可去，可随我等一同护住这方百姓。", LiuBei)
            .Opts(
                new Choice("愿随刘公，护这一方百姓。", new[]
                {
                    Q("刘备", "好。桃园之中，我与关、张二弟已立誓同生共死。今日起，你也算我帐下一员。", LiuBei),
                    Q("刘备", "附近散落着逃难百姓落下的粮草，先帮他们收拢回来。", LiuBei),
                }, "swore"),
                new Choice("容我先在这桃林走一走。", new[]
                {
                    Q("刘备", "也好。乱世里，先看清脚下的路，才走得远。", LiuBei),
                    Q("刘备", "若你愿伸手，便去帮百姓收一收散落的粮草。", LiuBei),
                }, "wander")
            ),
    };

    // 第一章
    public static Line[] T1_Intro => Get("T1_Intro", Def_T1_Intro);

    static readonly Line[] Def_T1_Intro =
    {
        N("建宁年间，天下大旱，饿殍遍野。一句「苍天已死，黄天当立」，点燃了整片中原。"),
        N("你从一阵剧烈的头痛中醒来，身上是粗布短褐，手里攥着一柄不知从何而来的兵刃。"),
        Q("刘备", "这位壮士，涿郡城外黄巾四起，你若无处可去，可随我等一同护住这方百姓。", LiuBei)
            .Opts(
                new Choice("愿随刘公，护这一方百姓。", new[]
                {
                    Q("刘备", "好。桃园之中，我与关、张二弟已立誓同生共死。今日起，你也算我帐下一员。", LiuBei),
                    N("风过桃林，花瓣落在你肩上。远处隐隐有杀声。"),
                }, "swore"),
                new Choice("容我先在这桃林走一走。", new[]
                {
                    Q("刘备", "也好。乱世里，先看清脚下的路，才走得远。", LiuBei),
                    Q("刘备", "若你愿伸手，便去帮百姓收一收散落的粮草。", LiuBei),
                }, "wander")
            ),
    };
    public static Line[] T1_BossIntro => Get("T1_BossIntro", Def_T1_BossIntro);

    static readonly Line[] Def_T1_BossIntro =
    {
        N("桃林尽头，一面残破的黄旗迎风猎猎。"),
        Q("程远志", "哪里来的无名小卒，也敢挡我黄巾大军？", Cheng),
        Q("程远志", "苍天已死！今日便拿你的血，祭我大旗！", Cheng),
    };
    public static Line[] T1_Outro => Get("T1_Outro", Def_T1_Outro);

    static readonly Line[] Def_T1_Outro =
    {
        N("黄旗倒下。程远志庞大的身躯砸进尘土，惊起一群栖鸟。"),
        Q("刘备", "好身手。涿郡暂安，但真正的乱局，才刚开始。", LiuBei),
        N("夜里，你望着满天星斗，第一次觉得，这个时代或许真的能留下你的名字。"),
    };

    // 第二章
    public static Line[] T2_Intro => Get("T2_Intro", Def_T2_Intro);

    static readonly Line[] Def_T2_Intro =
    {
        N("十八路诸侯会于汜水关，却无人敢先出阵。关下，一将横刀立马。"),
        Q("华雄", "诸侯联军？不过是一群缩在营里的鼠辈。", HuaXiong),
        Q("曹操", "此人连斩我数员裨将。帐下可还有人敢战？", CaoCao),
        Q("你", "我去。"),
        N("营中一片哗然。有人递来一碗热酒，你却没有接。"),
    };
    public static Line[] T2_BossIntro => Get("T2_BossIntro", Def_T2_BossIntro);

    static readonly Line[] Def_T2_BossIntro =
    {
        N("关下风起，华雄提刀而来，刀锋上还挂着未干的血。"),
        Q("华雄", "又是一个送死的。报上名来。", HuaXiong),
        Q("你", "一个不该出现在这里的小人物。"),
        Q("华雄", "那就死在这里吧！", HuaXiong),
    };
    public static Line[] T2_Outro => Get("T2_Outro", Def_T2_Outro);

    static readonly Line[] Def_T2_Outro =
    {
        N("华雄落马。汜水关前的风，忽然停了。"),
        N("那碗酒端回来时，尚有余温。营中诸将看你的眼神，从此不同。"),
        Q("曹操", "好一个温酒之勇。此人，我要记住。", CaoCao),
    };

    // 第三章
    public static Line[] T3_Intro => Get("T3_Intro", Def_T3_Intro);

    static readonly Line[] Def_T3_Intro =
    {
        N("长坂坡上，百姓与辎重绵延数里。曹军精锐衔尾而至。"),
        Q("刘备", "百姓随我至此，我不能弃之。", LiuBei),
        Q("张郃", "刘玄德，你带着这些人，一个也走不掉。", ZhangHe),
        N("张飞横矛立于桥头，可曹军绕行而至。桥这一侧，只能交给你。"),
        Q("刘备", "断后一事，我交给你。是要护着百姓过河，还是要随张飞冲阵斩将？", LiuBei)
            .Opts(
                new Choice("先护百姓过河。人比辎重重要。", new[]
                {
                    Q("刘备", "……好。你担得起这份重。", LiuBei),
                    N("你横刀立于桥头，把追兵挡在身后。百姓一个一个从你身侧跑过去。"),
                }, "mercy"),
                new Choice("随张飞冲阵。今日取他首级！", new[]
                {
                    Q("刘备", "好！这一阵，拿下张郃！", LiuBei),
                    N("长矛破风。你跟着冲进烟尘里，身后是张飞震天的吼。"),
                }, "ruthless")
            ),
    };
    public static Line[] T3_BossIntro => Get("T3_BossIntro", Def_T3_BossIntro);

    static readonly Line[] Def_T3_BossIntro =
    {
        N("尘土之中，一杆长枪破雾而出，枪缨如火。"),
        Q("张郃", "能一路杀到这里，你不在曹军名册上。", ZhangHe),
        Q("你", "本来就不在。"),
        Q("张郃", "那就让名册上，多一个死人的名字。", ZhangHe),
    };
    public static Line[] T3_Outro => Get("T3_Outro", Def_T3_Outro);

    static readonly Line[] Def_T3_Outro =
    {
        N("长枪坠地，张郃退入烟尘之中。长坂桥上，百姓仍在向东。"),
        Q("刘备", "若非你断后，今日我便无颜立于天地之间。", LiuBei),
        N("有人低声问你的名字。你终于报了它。"),
    };

    // 第四章
    public static Line[] T4_Intro => Get("T4_Intro", Def_T4_Intro);

    static readonly Line[] Def_T4_Intro =
    {
        N("虎牢关前，诸侯联军再一次停步。关下立着一人一戟，天地都安静下来。"),
        Q("吕布", "谁来送死？", LvBu),
        Q("曹操", "此人非一人可敌。若要一战，须有人先上去。", CaoCao),
        Q("你", "让我去。"),
        N("你握紧兵刃。身后是诸侯，身前是天下第一。"),
    };
    public static Line[] T4_BossIntro => Get("T4_BossIntro", Def_T4_BossIntro);

    static readonly Line[] Def_T4_BossIntro =
    {
        N("赤红披风翻卷，方天画戟映着雷光。"),
        Q("吕布", "有趣。你这把刀，不属于这个世道。", LvBu),
        Q("你", "人也一样。"),
        Q("吕布", "那就让我看看，不属于这个世道的刀，能撑几息！", LvBu),
    };
    public static Line[] T4_Outro => Get("T4_Outro", Def_T4_Outro);

    static readonly Line[] Def_T4_Outro =
    {
        N("画戟落地，震起一地碎石。天下无双的身影，第一次后退了一步。"),
        Q("曹操", "从今日起，天下人会记住你的名字。", CaoCao),
        Q("刘备", "乱世未定，你我却总算走出了第一步。", LiuBei),
        N("你抬头望向关外，那里有更辽阔的疆土，与更深的烽烟。"),
        N("——这不是结局，只是落子的第一手。"),
    };

    // 第五章
    public static Line[] T5_Intro => Get("T5_Intro", Def_T5_Intro);

    static readonly Line[] Def_T5_Intro =
    {
        N("虎牢关破，诸侯联军西向长安。一路上是空城，是焚尽的村舍，是望西而逃的百姓。"),
        N("洛阳城头的大纛换了颜色。守城的西凉军甲胄鲜明，却不见一个活人的气息。"),
        Q("曹操", "董卓已入洛阳。废立天子，焚杀朝士，天下人的怒火，都要烧到这面旗下了。", CaoCao),
        Q("曹操", "城中尚有百姓未走。是烧了这城断了他的后路，还是留他们一条生路？", CaoCao)
            .Opts(
                new Choice("烧城。断董卓后路，也断天下人的念想。", new[]
                {
                    Q("曹操", "狠。像我。", CaoCao),
                    N("火起时你站在城外。城里哭声一片，曹操没有回头。"),
                }, "burn"),
                new Choice("留一条生路。我带兵去把百姓赶出城。", new[]
                {
                    Q("曹操", "妇人之仁……罢了，这一城人，欠你一条命。", CaoCao),
                    N("你挨家挨户敲门，把人往城外赶。等最后一批人出城，火才点起来。"),
                }, "spare")
            ),
        Q("你", "那就在这面旗下，先斩董卓。"),
        N("城门在望。城头之上，紫色蟒袍的身影正俯视着这支队伍。"),
    };
    public static Line[] T5_BossIntro => Get("T5_BossIntro", Def_T5_BossIntro);

    static readonly Line[] Def_T5_BossIntro =
    {
        N("华盖之下，那人倚着软榻，手里把玩着一串玉珠，看都未看你一眼。"),
        Q("董卓", "又是来送死的？本公用过的刀，都还没钝。", DongZhuo),
        Q("你", "你的刀很快。但它只认钱，不认理。"),
        Q("董卓", "哈哈哈！说得极好！这天下，本就是给有钱有势的人准备的！", DongZhuo),
        N("他骤然起身，玉珠散落一地。身后西凉铁骑同时举刀。"),
    };
    public static Line[] T5_Outro => Get("T5_Outro", Def_T5_Outro);

    static readonly Line[] Def_T5_Outro =
    {
        N("玉珠滚到你脚边，停住了。城头的火把一盏接一盏亮起来，照着满地狼藉。"),
        Q("曹操", "好。洛阳既下，西凉残部必然反扑——而河北袁绍，已经在官渡等我们了。", CaoCao),
        Q("刘备", "袁本初河北四州之众，粮秣甲兵皆在他手。这一战，比虎牢关更难。", LiuBei),
        N("你望向东方。平野尽头，隐约可见一线营火连绵百里。"),
    };

    // 第六章（终章）
    public static Line[] T6_Intro => Get("T6_Intro", Def_T6_Intro);

    static readonly Line[] Def_T6_Intro =
    {
        N("官渡。乌巢。曹操用一把火，赌上了整个北方。"),
        N("你伏在坡后。对面的营垒望不到边，青玉色的帅旗在夜风里翻卷。"),
        Q("曹操", "袁绍性缓而多疑，拖下去对我们有利。你去烧他的粮——不必恋战。", CaoCao),
        Q("你", "我只需走到他面前。"),
        N("袁绍的中军大帐忽然亮起。帐帘掀开，一个身着银袍金饰的人缓步走出。"),
    };
    public static Line[] T6_BossIntro => Get("T6_BossIntro", Def_T6_BossIntro);

    static readonly Line[] Def_T6_BossIntro =
    {
        N("袁绍按剑而立，甲叶轻响。他看你的眼神里没有吕布的狂，只有一种沉下来的看重。"),
        Q("袁绍", "你能走到我军帐前，说明天下人还没忘了我四世三公的招牌。", YuanShao),
        Q("你", "我来，是取你的粮。"),
        Q("袁绍", "粮是死的，名声是活的。可惜你两样都拿不走。", YuanShao),
        N("他抽出佩剑，剑鞘上的金饰被火光照得刺眼。身后鼓声骤起。"),
    };
    public static Line[] T6_Outro => Get("T6_Outro", Def_T6_Outro);

    static readonly Line[] Def_T6_Outro =
    {
        N("帅旗落地，火光顺着粮道烧成一片。鼓声停了，只剩风声。"),
        Q("曹操", "河北平了。天下……可望。", CaoCao),
        Q("刘备", "天下可望，是因为脚下这一路，是你走出来的。", LiuBei),
        N("你站在土坡上。远处炊烟散尽，新的一日正在开始。"),
        N("——乱世未终，但你手中的刀，已经有人认得。"),
    };

    // 第七章（终章之终）· 渭水
    public static Line[] T7_Intro => Get("T7_Intro", Def_T7_Intro);

    static readonly Line[] Def_T7_Intro =
    {
        N("河北平了。曹操却没有休整 —— 北方的帘子一放，南方的门就露了出来。"),
        N("潼关外，渭水如带。关中的凉州兵列阵于北，马蹄踏在薄冰上，声音传得很远。"),
        Q("曹操", "河北四州是粮，此战要的是命。关中这些凉州人，打得比袁绍还硬。", CaoCao),
        Q("刘备", "他们不是来替袁绍报仇的，他们是要自立。马超、韩遂，各打各的主意。", LiuBei),
        Q("曹操", "所以要先断其首脑。你去，把关中的旗子探明白。", CaoCao),
        Q("你", "旗子看过了，人心呢？"),
        Q("曹操", "人心……就看你的刀够不够快了。", CaoCao),
        N("关上忽然竖起一面新旗，银底朱字，在风里抖得笔直。"),
    };
    public static Line[] T7_BossIntro => Get("T7_BossIntro", Def_T7_BossIntro);

    static readonly Line[] Def_T7_BossIntro =
    {
        N("银甲白袍，一杆长枪。关下的马先动了，人却像钉在地上。"),
        Q("马超", "从桃林走到潼关，你走了多少年？", MaChao),
        Q("你", "走了一辈子。"),
        Q("马超", "好。那今日这一战，便是你这一辈子的最后一程。", MaChao),
        Q("马超", "我父死在长安，为汉室尽忠；我替父报仇，也算替汉室尽忠。你呢？", MaChao),
        Q("你", "我不替谁尽忠。我只替我自己走完这条路。"),
        Q("马超", "……说得好。那便不必留手。", MaChao),
        N("枪锋一转，渭水的薄冰被踏裂一片。身后，西凉铁骑同时举起长刀。"),
    };
    public static Line[] T7_Outro => Get("T7_Outro", Def_T7_Outro);

    static readonly Line[] Def_T7_Outro =
    {
        N("银甲落地。渭水重新静下来，只听见冰裂与喘息。"),
        Q("马超", "……好枪法。可是你记住：今日你胜我，明日天下人便都知道了。", MaChao),
        Q("你", "知道便好。我本来就不是来扬名的。"),
        N("马超撑着枪站起来，朝你抱拳，然后转身走进关外的风里。"),
        Q("曹操", "他还没走远。追上去斩尽，还是遣使说降？", CaoCao)
            .Opts(
                new Choice("追。今日斩尽，凉州再无强敌。", new[]
                {
                    Q("曹操", "好！孤就喜欢这份狠劲。", CaoCao),
                    N("追骑出关。马蹄声在雪里散了很久，才停。"),
                }, "slay"),
                new Choice("遣使说降。活着回去的马超，比死了的有用。", new[]
                {
                    Q("曹操", "……你这判断，倒比孤冷静。", CaoCao),
                    N("使节追出关外。你不知道结果，但你知道那扇门还开着。"),
                }, "pardon")
            ),
        Q("曹操", "关中诸将各自散去，这一带，暂不必再打了。", CaoCao),
        Q("刘备", "河北已平，关中已定。剩下的……就要看这天下人怎么分了。", LiuBei),
        Q("曹操", "分？", CaoCao),
        Q("刘备", "魏、蜀、吴。三分之势，已经成了。", LiuBei),
        N("你站在渭水边。这一路从桃林走来，脚下这条路，终于走到了头。"),
        N("远处，有人正收拾营帐，准备下一场 —— 也是最后一场 —— 议事。"),
    };

    // ---- 第七章 · 渭水：关中未定 ----

    public static Line[] WeiheLiangjiang => Get("WeiheLiangjiang", Def_WeiheLiangjiang);

    static readonly Line[] Def_WeiheLiangjiang =
    {
        N("潼关下，一名西凉军士把长枪插在地上，盯着你看了很久。"),
        Q("西凉军士", "关中人不认曹操，只认马家的旗。你要打马将军，先过我们这关。", LiangJiang),
        Q("你", "你们跟着马超，是为了报仇，还是为了活？"),
        Q("西凉军士", "……都有。凉州饿死了太多人，谁给饭，我们跟谁。", LiangJiang),
        Q("西凉军士", "不过我劝你一句：马将军不是袁绍。他不会等你烧他的粮。", LiangJiang),
    };

    public static Line[] WeiheShusheng => Get("WeiheShusheng", Def_WeiheShusheng);

    static readonly Line[] Def_WeiheShusheng =
    {
        N("关城一角，一个书吏在潼关旧册上添字，笔尖冻住了。"),
        Q("书吏", "关中这十一年，换了五任太守，每一任都说要安定这里。", ShuSheng),
        Q("你", "安定了吗？"),
        Q("书吏", "安定了死人。户口册上从四十万，写到十万。", ShuSheng),
        Q("书吏", "如今河北归了曹，江南还是孙刘。这本册子，我不知该写谁的名字。", ShuSheng),
        Q("你", "写不会饿死的那些人。"),
        Q("书吏", "……你说得轻巧。可乱世里，能写进册子的人，从来不是最苦的那些。", ShuSheng),
    };

    public static Line[] WeiheChuanfu => Get("WeiheChuanfu", Def_WeiheChuanfu);

    static readonly Line[] Def_WeiheChuanfu =
    {
        N("渭水渡口，一个女子守着一条小船，篙绳冻在手里。"),
        Q("船家女", "关里在打仗，渡口的船都不敢出去了。过了河，就回不来了。", ChuanFu),
        Q("你", "你还守着船？"),
        Q("船家女", "守着。关里有人要过河，我就送他一程。", ChuanFu),
        Q("船家女", "你看着不像关里的人。可你腰上的刀，跟关里人的不一样 —— 磨得太整齐了。", ChuanFu),
        Q("你", "会磨刀的人，才配走到最后一步。"),
        Q("船家女", "……那你过关的时候，替我看一眼渭水的下游。那里埋着三十七个没能过河的人。", ChuanFu),
    };

    public static Line[] WeiheLaochuanfu => Get("WeiheLaochuanfu", Def_WeiheLaochuanfu);

    static readonly Line[] Def_WeiheLaochuanfu =
    {
        N("渡口边一个老人蹲在火堆旁，用树枝在土上划着什么。"),
        Q("老船工", "我在这条水上撑了五十年。关里换了几次旗，我都在。", LaoChuanFu),
        Q("你", "那你记得清楚。"),
        Q("老船工", "我记得清楚。这关啊，不是从关里破的，是从关外破的。", LaoChuanFu),
        Q("老船工", "马将军的兵再硬，也是从西边来的；西边来的马，饿。饿了就不认旗了。", LaoChuanFu),
        Q("你", "你这话，说给谁听？"),
        Q("老船工", "说给肯听的人听。少主公未必肯听，但他身边总有人肯听。", LaoChuanFu),
    };

    public static Line[] WeihePost => Get("WeihePost", Def_WeihePost);

    static readonly Line[] Def_WeihePost =
    {
        N("潼关道旁一座破驿，驿卒正把最后一面旗换成新的。"),
        Q("驿卒", "换了六次了。涿郡、汜水、洛阳、官渡……这一面，是关中。", Postman),
        Q("驿卒", "客官往西还是往东？往东是渭水，往西回洛阳。", Postman),
        Q("驿卒", "这回往哪儿走，别再回头了。回头的人，在这道上有多少，名字没人记得。", Postman),
    };

    public static Line[] WeiheHerder => Get("WeiheHerder", Def_WeiheHerder);

    static readonly Line[] Def_WeiheHerder =
    {
        N("坡上一个牧童赶着牛群，牛踩在薄冰上，蹄声很脆。"),
        Q("牧童", "牛不怕凉州人。他们只怕一样 —— 自己的影子没了。", Herd),
        Q("你", "影子怎么会没了？"),
        Q("牧童", "旗一换，影子就没了。牛认不得旗，就认草。草在哪儿，牛就在哪儿。", Herd),
        Q("牧童", "你要找马将军，往北。牛群不去的地方，就是他的马待的地方。", Herd),
    };

    // 野外闲谈 / 支线
    public static Line[] PeachSuwan => Get("PeachSuwan", Def_PeachSuwan);

    static readonly Line[] Def_PeachSuwan =
    {
        N("亭边立着一位青衣女子，袖中落满桃花。"),
        Q("苏婉", "客官也是避乱至此？这林子里的花，开得太盛，倒不像乱世该有的颜色。", Suwan),
        Q("苏婉", "家父从前在涿县坐堂。他常说：药不是给人续命的，是给人把日子过完的。", Suwan),
        Q("你", "如今黄巾过境，药铺还在吗？"),
        Q("苏婉", "铺子没了。我只剩这点草药知识。你若见到灵草，便多采一些——伤员会用得上。", Suwan),
    };

    public static Line[] PeachAfu => Get("PeachAfu", Def_PeachAfu);

    static readonly Line[] Def_PeachAfu =
    {
        N("村口一个汉子正在扫落花，扫帚停在半空。"),
        Q("阿福", "哟，新来的？桃林这阵子不太平，夜里总能听见马蹄。", Afu),
        Q("阿福", "刘皇叔人好，就是太不会拒绝人。你要是跟着他，可得自己长个心眼。", Afu),
        Q("你", "我记下了。"),
        Q("阿福", "对了，东边亭子里那位苏姑娘，别看她话少，药可熬得比县城大夫还稳。", Afu),
    };

    public static Line[] PeachWeaver => Get("PeachWeaver", Def_PeachWeaver);

    static readonly Line[] Def_PeachWeaver =
    {
        N("桃林尽头一间矮屋，窗前晾着几匹素绢。"),
        Q("织工", "逃难来的人多，寿衣不够用，只好拿素绢顶。", Weaver),
        Q("你", "你还在赶工？"),
        Q("织工", "乱世里，织机不能停。停了，人就以为日子停了。", Weaver),
    };

    public static Line[] PassScout => Get("PassScout", Def_PassScout);

    static readonly Line[] Def_PassScout =
    {
        N("关前雪地上，一名关吏正呵着手，怀里抱着一本过路名册。"),
        Q("关吏", "华雄连斩数将。营里的酒热了又凉，凉了又热，没人敢接那一碗。", Clerk),
        Q("你", "关前虚实如何？"),
        Q("关吏", "我不管虚实，我管名册。可我告诉你：西凉铁骑夜里不熄火。你要去，就走官道，别抄林子——林子里有伏弓。", Clerk),
    };

    public static Line[] PassScoutSecond => Get("PassScoutSecond", Def_PassScoutSecond);

    static readonly Line[] Def_PassScoutSecond =
    {
        N("斥候换了班，从雪地里站起来，膝盖咯吱响。"),
        Q("斥候", "我又绕了一圈。三件事，你记着：西凉骑夜里不熄火；关前埋着伏弓；西营的粮车走得很慢。", Scout),
        Q("你", "粮车慢？"),
        Q("斥候", "慢了就是粮不够。粮不够的军队，再猛也撑不过十日。", Scout),
        Q("斥候", "我只是个斥候，看得见马，看不见天下。你能走到主公帐前，就替我多看一眼。", Scout),
    };

    public static Line[] PassWine => Get("PassWine", Def_PassWine);

    static readonly Line[] Def_PassWine =
    {
        N("营边支着一口小炉，酒香混着雪味。"),
        Q("酒贩", "客官喝一碗？温着的。听说有人要拿这酒去赌命。", Wine)
            .Opts(
                new Choice("喝。就当给自己壮行。", new[]
                {
                    Q("酒贩", "痛快。这酒不贵，贵的是喝完还能活着回来。", Wine),
                    N("酒入喉，胸口倒是热了一寸。"),
                }, "drank"),
                new Choice("先不喝。刀还没出鞘。", new[]
                {
                    Q("酒贩", "也成。酒放着，等你回来——我给你再温一遍。", Wine),
                }, "sober")
            ),
    };

    public static Line[] ChangbanRefugee => Get("ChangbanRefugee", Def_ChangbanRefugee);

    static readonly Line[] Def_ChangbanRefugee =
    {
        N("坡上一个妇人抱着孩子，身边是翻倒的车辕。"),
        Q("难民", "军爷……不是，壮士，前面还能走吗？孩子发烧，我不敢停。", Refugee),
        Q("你", "往东。桥还在，我会挡住后面的人。"),
        Q("难民", "……谢谢。桃花源是假的，可有人肯停下来说话，便也像真的。", Refugee),
    };

    public static Line[] ChangbanBridge => Get("ChangbanBridge", Def_ChangbanBridge);

    static readonly Line[] Def_ChangbanBridge =
    {
        N("桥断在河心，断口处的木头还带着新茬。"),
        N("对岸有人举着一块白布晃了三晃，然后跪下了。"),
        Q("你", "过来。水不深，扶着绳子。"),
        N("他过了河，回头看了看断桥，忽然跪得更深。"),
        Q("过河的人", "我叫赵仓。昨夜辎重陷在这里，主家的人……都没了。", ZhaoCang),
        Q("你", "你会做什么？"),
        Q("过河的人", "我会认路，也会认人。这两样在乱世里，比刀管用。", ZhaoCang),
        Q("你", "那你跟着我。从今天起，你是斥候。"),
        Q("过河的人", "是。"),
    };

    public static Line[] HulaoGuard => Get("HulaoGuard", Def_HulaoGuard);

    static readonly Line[] Def_HulaoGuard =
    {
        N("关下一名军士把矛拄在地上，指节发白。"),
        Q("军士", "温侯在关前走了三圈。马打了个响鼻，三军无声。", Guard),
        Q("军士", "你真要上去？上去的人，到现在还没一个走回来。", Guard),
        Q("你", "那便走一遭。"),
        Q("军士", "……我会给你留一碗水。不论谁回来。", Guard),
    };

    public static Line[] HulaoVeteran => Get("HulaoVeteran", Def_HulaoVeteran);

    static readonly Line[] Def_HulaoVeteran =
    {
        N("关墙下蹲着一个裹着旧毡的老卒，正在数地上的箭。"),
        Q("老卒", "三百一十七支。有箭矢的地方，就有人没回来。", Veteran),
        Q("你", "你还在数？"),
        Q("老卒", "数完才知道，这关不好过。年轻人凭一口气，老卒凭一支箭。", Veteran),
        Q("老卒", "你要打温侯，我劝不动你。但我给你指一条路——他的马，比他先累。", Veteran),
    };

    public static Line[] JiangxiaFisher => Get("JiangxiaFisher", Def_JiangxiaFisher);

    static readonly Line[] Def_JiangxiaFisher =
    {
        N("芦苇荡边，渔翁把网搭在船舷上。"),
        Q("渔翁", "江夏的水浅，鱼却比关里的人诚实。你从桃林来的吧？", Fisher),
        Q("你", "路过。想看看这水能通到哪儿。"),
        Q("渔翁", "往西是桃源，往北的路我不管。天黑前回村，岸边有织女家的灯。", Fisher),
    };

    public static Line[] JiangxiaWeaver => Get("JiangxiaWeaver", Def_JiangxiaWeaver);

    static readonly Line[] Def_JiangxiaWeaver =
    {
        N("村口一架织机，线是浅青的。"),
        Q("织女", "渔火亮了才算到家。你要是往北走，把这话说给路上的人听。", Maiden),
        Q("织女", "乱世里能走的路，都是别人用脚踩出来的。", Maiden),
    };

    public static Line[] GuanduPost => Get("GuanduPost", Def_GuanduPost);

    static readonly Line[] Def_GuanduPost =
    {
        N("官道旁一座小驿，旗子被风吹得只剩半面。"),
        Q("驿卒", "官渡这一带没有城墙，只有草。草里什么都藏得住。", Postman),
        Q("驿卒", "西边长坂还能看见烟。你要回去，就沿着土路，别抄田。", Postman),
    };

    public static Line[] GuanduHerder => Get("GuanduHerder", Def_GuanduHerder);

    static readonly Line[] Def_GuanduHerder =
    {
        N("坡上一个孩子赶着两头牛，牛比人从容。"),
        Q("牧童", "这儿大，大到喊一声，回声要过很久才到。", Herd),
        Q("牧童", "你要找人打架，去西边。这儿的牛不跟人争路。", Herd),
    };

    // ---- 第五章 · 洛阳东郊：宫阙无主 ----

    public static Line[] LuoyangScribe => Get("LuoyangScribe", Def_LuoyangScribe);

    static readonly Line[] Def_LuoyangScribe =
    {
        N("烧了一半的尚书台，一个书吏正在残卷里翻找什么。"),
        Q("书吏", "汉室的典册散了三处了。董卓烧了一次，逃难的人又带走一次。", Scribe),
        Q("你", "你在找什么？"),
        Q("书吏", "找一份能证明这片土地属于谁的册子。可惜——册子可以烧，人不能。", Scribe),
        Q("书吏", "你从关外来的？替我看一眼：如今掌兵的人，心里还有没有这块地？", Scribe),
    };

    public static Line[] LuoyangRefugee => Get("LuoyangRefugee", Def_LuoyangRefugee);

    static readonly Line[] Def_LuoyangRefugee =
    {
        N("城外的粥棚前排着长队，队尾有个孩子在哭。"),
        Q("妇人", "官府昨天开门，今天又关了。说是城里的粮被搜走了。", Woman),
        Q("你", "搜粮的是哪一路？"),
        Q("妇人", "……都是来救我们的。", Woman).Opts(
            new Choice("那就先救人。别的以后再说。", new[]
            {
                N("你从行囊里摸出半囊粟，塞进孩子手里。"),
                Q("妇人", "你这样的人，这乱世里不多。", Woman),
            }, "saved"),
            new Choice("乱世里，人比粮贱。走吧。", new[]
            {
                N("队伍安静了一瞬。没有人骂你，也没有人谢你。"),
            }, "walked")
        ),
    };

    // ---- 第六章 · 官渡：粮道千里 ----

    public static Line[] GuanduBarkeep => Get("GuanduBarkeep", Def_GuanduBarkeep);

    static readonly Line[] Def_GuanduBarkeep =
    {
        N("渡口边一间草棚，主人正把最后一坛酒倒进碗里。"),
        Q("店主", "乌巢的火烧了三天三夜。风里都是焦味，喝酒都压不住。", Merchant),
        Q("你", "粮船还能过来吗？"),
        Q("店主", "船在，人不都在。押船的十几个，昨夜少了三个，说是'投敌'。", Merchant),
        Q("店主", "客官，我在这渡口摆了二十年酒。只学会一件事——乱世里，谁守着渡口，谁就说得算。", Merchant),
    };

    public static Line[] GuanduVeteran => Get("GuanduVeteran", Def_GuanduVeteran);

    static readonly Line[] Def_GuanduVeteran =
    {
        N("土坡后一个老兵在磨刀，磨得很慢。"),
        Q("老兵", "我从凉州跟到河北，打过二十七场。输的场次，早忘了；赢的，也记不清了。", Elder),
        Q("你", "还磨？"),
        Q("老兵", "刀钝了，人就老了。我磨的不是刀，是怕。", Elder),
        Q("老兵", "小子，你如今是什么身份？无名之辈，还是——", Elder).Opts(
            new Choice("还没人给我这个名号。", new[]
            {
                Q("老兵", "那就还在路上。别急着要名号；名号是命换的，不是人给的。", Elder),
            }, "nameless"),
            new Choice("总有一天会有。", new[]
            {
                Q("老兵", "好。那我等着看你那天的样子。到时我给你敬一碗。", Elder),
            }, "ambitious")
        ),
    };
}
