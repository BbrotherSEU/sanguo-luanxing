using System.Collections.Generic;
using UnityEngine;

// ---------------- 单位定义 ----------------

public class UnitDef
{
    public string Id;
    public string Name;
    public string Sprite = "bandit";
    public string Title;          // 仅 Boss 有
    public string Type = "melee"; // melee / ranged
    public bool Boss;

    public float Hp = 50f, Atk = 8f, Def = 2f, Speed = 2.2f, Scale = 1f;
    public float Range = 2f, Windup = 0.5f, Recover = 0.45f, Cd = 1.1f, Aggro = 9f;
    public float Exp = 15f, Gold = 8f, Poise = 0.3f;
    public float KeepMin, KeepMax, ArrowSpeed;
    public string[] Patterns;
}

// ---------------- 地图 ----------------

public class PackDef
{
    public float X, Z, R;
    public string[] Types;
}

public class PickupDef
{
    public float X, Z;
    public string Type;
}

public class MapDef
{
    public string Id, Chapter, Name, Subtitle, LevelRange, Decor, Boss, Ending;
    public int RecLevel = 2;
    public Color Fog = new Color(0.72f, 0.70f, 0.63f);
    public Color GroundTint = Color.white;
    public string[] Mobs;
    public PackDef[] Packs;
    public PickupDef[] Pickups;
    public Line[] Intro, BossIntro, Outro;
}

// ---------------- 阵营 ----------------

public class FactionDef
{
    public string Id, Name, Leader, Title, PassiveName, PassiveDesc, StartWeapon;
    public Color Color = Color.white;
    public float DefMul = 1f, AtkMul = 1f, SpeedMul = 1f, QiMul = 1f, CritAdd = 0f, HeavyMul = 1f;
    public string[] Intro;
}

// ---------------- 技能 ----------------

public class SkillDef
{
    public string Id, Key, Name, Desc;
    public float BaseCd, BaseCost, BaseDmg, Radius, Duration;
}

// ---------------- 数据表 ----------------

public static class DB
{
    public static readonly Dictionary<string, UnitDef> Enemies = new Dictionary<string, UnitDef>();
    public static readonly Dictionary<string, UnitDef> Bosses = new Dictionary<string, UnitDef>();
    public static readonly Dictionary<string, FactionDef> Factions = new Dictionary<string, FactionDef>();
    public static readonly Dictionary<string, string> PickupNames = new Dictionary<string, string>();
    public static readonly Dictionary<string, string> PickupDescs = new Dictionary<string, string>();
    public static readonly Dictionary<string, Color> PickupColors = new Dictionary<string, Color>();

    static readonly SkillDef[] SkillsDefault =
    {
        new SkillDef { Id = "charge",  Key = "Q", Name = "破军突刺", Desc = "向前突进，刺穿沿途之敌",
            BaseCd = 5.5f, BaseCost = 22f, BaseDmg = 1.85f },
        new SkillDef { Id = "whirl",   Key = "E", Name = "横扫千军", Desc = "环身横扫，击退周围敌军",
            BaseCd = 8.5f, BaseCost = 30f, BaseDmg = 1.4f, Radius = 3.6f },
        new SkillDef { Id = "warlord", Key = "R", Name = "武神降临", Desc = "九息之内，攻击与身法大幅提升",
            BaseCd = 26f, BaseCost = 45f, BaseDmg = 0f, Duration = 9f },
    };

    /// <summary>外部数据表覆盖的技能表。DataDb 加载 JSON 后写这里。</summary>
    public static SkillDef[] SkillsOverride;

    /// <summary>技能表：优先用外部 JSON，没有就用内置默认值。</summary>
    public static SkillDef[] Skills => SkillsOverride != null && SkillsOverride.Length > 0 ? SkillsOverride : SkillsDefault;

    public static readonly string[] FactionOrder = { "wei", "shu", "wu" };

    public static List<MapDef> Maps { get; private set; }

    static DB()
    {
        BuildEnemies();
        BuildBosses();
        BuildFactions();
        BuildMaps();
        BuildPickups();
    }

    // ---------- 小怪 ----------
    static void BuildEnemies()
    {
        Add(new UnitDef { Id = "bandit", Name = "黄巾乱兵", Sprite = "bandit",
            Hp = 52f, Atk = 9f, Def = 2f, Speed = 2.3f, Scale = 1f,
            Range = 1.9f, Windup = 0.42f, Recover = 0.32f, Cd = 0.95f, Aggro = 9f,
            Exp = 16f, Gold = 9f, Poise = 0.45f });

        Add(new UnitDef { Id = "archer", Name = "黄巾弓手", Sprite = "archer", Type = "ranged",
            Hp = 40f, Atk = 10f, Def = 1f, Speed = 2.4f, Scale = 1f,
            Range = 11f, KeepMin = 5.5f, KeepMax = 9f, Windup = 0.55f, Recover = 0.38f, Cd = 1.55f,
            Exp = 18f, Gold = 10f, Poise = 0.4f, ArrowSpeed = 11f, Aggro = 12f });

        Add(new UnitDef { Id = "brute", Name = "重甲刀盾兵", Sprite = "brute",
            Hp = 135f, Atk = 16f, Def = 11f, Speed = 1.7f, Scale = 1.12f,
            Range = 2.1f, Windup = 0.65f, Recover = 0.42f, Cd = 1.15f, Aggro = 9f,
            Exp = 34f, Gold = 22f, Poise = 0.85f });

        Add(new UnitDef { Id = "rider", Name = "并州铁骑", Sprite = "brute",
            Hp = 175f, Atk = 20f, Def = 14f, Speed = 3.0f, Scale = 1.16f,
            Range = 2.2f, Windup = 0.50f, Recover = 0.38f, Cd = 0.95f, Aggro = 11f,
            Exp = 46f, Gold = 30f, Poise = 0.95f });
    }

    // ---------- Boss ----------
    static void BuildBosses()
    {
        Bosses["cheng"] = new UnitDef { Id = "cheng", Name = "黄巾渠帅 · 程远志", Sprite = "boss_cheng",
            Title = "苍天已死", Boss = true,
            Hp = 430f, Atk = 18f, Def = 8f, Speed = 2.1f, Scale = 1.5f,
            Exp = 190f, Gold = 120f, Poise = 1.4f, Aggro = 14f, Patterns = new[] { "cleave", "slam" } };

        Bosses["huaxiong"] = new UnitDef { Id = "huaxiong", Name = "西凉猛将 · 华雄", Sprite = "boss_huaxiong",
            Title = "关外无人可敌", Boss = true,
            Hp = 760f, Atk = 25f, Def = 15f, Speed = 2.3f, Scale = 1.55f,
            Exp = 320f, Gold = 200f, Poise = 1.8f, Aggro = 14f, Patterns = new[] { "cleave", "slam", "charge" } };

        Bosses["zhanghe"] = new UnitDef { Id = "zhanghe", Name = "魏之名将 · 张郃", Sprite = "boss_zhanghe",
            Title = "巧变之将", Boss = true,
            Hp = 1080f, Atk = 31f, Def = 19f, Speed = 2.7f, Scale = 1.55f,
            Exp = 480f, Gold = 300f, Poise = 2.0f, Aggro = 15f, Patterns = new[] { "cleave", "charge", "slam" } };

        Bosses["lvbu"] = new UnitDef { Id = "lvbu", Name = "天下无双 · 吕布", Sprite = "boss_lvbu",
            Title = "人中吕布", Boss = true,
            Hp = 1600f, Atk = 41f, Def = 25f, Speed = 3.1f, Scale = 1.7f,
            Exp = 800f, Gold = 500f, Poise = 2.6f, Aggro = 16f, Patterns = new[] { "cleave", "charge", "slam" } };

        Bosses["dongzhuo"] = new UnitDef { Id = "dongzhuo", Name = "太师 · 董卓", Sprite = "boss_dongzhuo",
            Title = "凉州暴虐", Boss = true,
            Hp = 2050f, Atk = 48f, Def = 29f, Speed = 2.9f, Scale = 1.7f,
            Exp = 1050f, Gold = 640f, Poise = 2.8f, Aggro = 16f, Patterns = new[] { "cleave", "slam", "charge" } };

        Bosses["yuanshao"] = new UnitDef { Id = "yuanshao", Name = "四世三公 · 袁绍", Sprite = "boss_yuanshao",
            Title = "官渡对敌", Boss = true,
            Hp = 2650f, Atk = 56f, Def = 34f, Speed = 3f, Scale = 1.75f,
            Exp = 1400f, Gold = 850f, Poise = 3.1f, Aggro = 17f, Patterns = new[] { "cleave", "charge", "slam" } };

        Bosses["machao"] = new UnitDef { Id = "machao", Name = "西凉锦马超", Sprite = "boss_machao",
            Title = "神威天将军", Boss = true,
            Hp = 3400f, Atk = 63f, Def = 40f, Speed = 3.2f, Scale = 1.8f,
            Exp = 2000f, Gold = 1200f, Poise = 3.5f, Aggro = 18f, Patterns = new[] { "cleave", "charge", "slam" } };
    }

    static void Add(UnitDef d) => Enemies[d.Id] = d;

    // ---------- 阵营 ----------
    static void BuildFactions()
    {
        Factions["wei"] = new FactionDef
        {
            Id = "wei", Name = "魏", Leader = "曹操", Title = "挟天子 · 令诸侯",
            Color = new Color(0.66f, 0.20f, 0.16f), StartWeapon = "环首刀",
            DefMul = 1.18f, HeavyMul = 1.12f, AtkMul = 1f, SpeedMul = 1f, QiMul = 1f, CritAdd = 0f,
            PassiveName = "霸者之资", PassiveDesc = "防御 +18%，重击伤害 +12%",
            Intro = new[]
            {
                "你睁开眼时，鼻间是尘土与马粪的味道。",
                "远处旌旗蔽日，上书一个「曹」字——那是许都的方向。",
                "乱世不问来处。既然醒了，总得选一边站。",
            },
        };

        Factions["shu"] = new FactionDef
        {
            Id = "shu", Name = "蜀", Leader = "刘备", Title = "汉室宗亲 · 仁义布于四海",
            Color = new Color(0.30f, 0.50f, 0.40f), StartWeapon = "铁枪",
            DefMul = 1f, HeavyMul = 1f, AtkMul = 1.18f, SpeedMul = 1f, QiMul = 1f, CritAdd = 0.06f,
            PassiveName = "仁者之勇", PassiveDesc = "攻击 +18%，暴击率 +6%",
            Intro = new[]
            {
                "你睁开眼时，桃花落满肩头。",
                "树下三人正焚香结义，为首者自称中山靖王之后。",
                "他们说，汉室倾颓，百姓流离——你若愿意，便一同去。",
            },
        };

        Factions["wu"] = new FactionDef
        {
            Id = "wu", Name = "吴", Leader = "孙权", Title = "据江东 · 已历三世",
            Color = new Color(0.29f, 0.43f, 0.58f), StartWeapon = "古锭刀",
            DefMul = 1f, HeavyMul = 1f, AtkMul = 1f, SpeedMul = 1.1f, QiMul = 1.2f, CritAdd = 0.02f,
            PassiveName = "江东之锐", PassiveDesc = "移速 +10%，气力回复 +20%",
            Intro = new[]
            {
                "你睁开眼时，江风灌满衣袖，浪声拍岸。",
                "楼船连天，一个年轻的主公站在船头，问你愿不愿随他看这江东。",
                "这世道没有退路，只有站稳的地方。",
            },
        };
    }

    // ---------- 地图 ----------
    static void BuildMaps()
    {
        Maps = new List<MapDef>();

        Maps.Add(new MapDef
        {
            Id = "zhuojun", Chapter = "第一章", Name = "涿郡 · 桃林",
            Subtitle = "春深，不知乱世将至", LevelRange = "1 - 3", RecLevel = 2,
            Decor = "peach", Boss = "cheng", Ending = "涿郡已定",
            Fog = new Color(0.62f, 0.68f, 0.72f), GroundTint = Color.white,
            Mobs = new[] { "bandit", "archer" },
            Packs = new[]
            {
                new PackDef { X = -8f, Z = 6f, R = 4.5f, Types = new[] { "bandit", "bandit" } },
                new PackDef { X = 9f, Z = 2f, R = 4.5f, Types = new[] { "bandit", "archer" } },
                new PackDef { X = -4f, Z = -4f, R = 4.5f, Types = new[] { "bandit", "bandit", "archer" } },
            },
            Pickups = new[]
            {
                new PickupDef { X = 4f, Z = 12f, Type = "herb" },
                new PickupDef { X = -12f, Z = 10f, Type = "coin" },
                new PickupDef { X = 13f, Z = 8f, Type = "scroll" },
                new PickupDef { X = -14f, Z = -2f, Type = "ore" },
                new PickupDef { X = 6f, Z = -8f, Type = "herb" },
                new PickupDef { X = 12f, Z = -13f, Type = "coin" },
            },
            Intro = Story.T1_Intro,
            BossIntro = Story.T1_BossIntro,
            Outro = Story.T1_Outro,
        });

        Maps.Add(new MapDef
        {
            Id = "sishui", Chapter = "第二章", Name = "汜水关",
            Subtitle = "关外风雪，诸侯按兵不动", LevelRange = "3 - 6", RecLevel = 4,
            Decor = "pass", Boss = "huaxiong", Ending = "汜水关破",
            Fog = new Color(0.63f, 0.55f, 0.455f), GroundTint = new Color(0.78f, 0.78f, 0.77f),
            Mobs = new[] { "bandit", "brute", "archer" },
            Packs = new[]
            {
                new PackDef { X = -9f, Z = 7f, R = 4.5f, Types = new[] { "brute", "bandit" } },
                new PackDef { X = 10f, Z = 3f, R = 5f, Types = new[] { "archer", "archer", "bandit" } },
                new PackDef { X = -3f, Z = -3f, R = 5f, Types = new[] { "brute", "brute" } },
                new PackDef { X = 6f, Z = -9f, R = 4.5f, Types = new[] { "bandit", "archer" } },
            },
            Pickups = new[]
            {
                new PickupDef { X = 0f, Z = 13f, Type = "herb" },
                new PickupDef { X = 14f, Z = 11f, Type = "coin" },
                new PickupDef { X = -13f, Z = 5f, Type = "scroll" },
                new PickupDef { X = -13f, Z = -8f, Type = "ore" },
                new PickupDef { X = 11f, Z = -10f, Type = "herb" },
                new PickupDef { X = 2f, Z = -6f, Type = "coin" },
            },
            Intro = Story.T2_Intro,
            BossIntro = Story.T2_BossIntro,
            Outro = Story.T2_Outro,
        });

        Maps.Add(new MapDef
        {
            Id = "changban", Chapter = "第三章", Name = "长坂坡",
            Subtitle = "一骑当千，尘土遮天", LevelRange = "6 - 9", RecLevel = 7,
            Decor = "ruins", Boss = "zhanghe", Ending = "长坂突围",
            Fog = new Color(0.76f, 0.66f, 0.47f), GroundTint = new Color(0.86f, 0.75f, 0.60f),
            Mobs = new[] { "brute", "archer", "bandit" },
            Packs = new[]
            {
                new PackDef { X = -9f, Z = 8f, R = 5f, Types = new[] { "brute", "brute" } },
                new PackDef { X = 10f, Z = 4f, R = 5f, Types = new[] { "archer", "archer", "bandit" } },
                new PackDef { X = -2f, Z = -3f, R = 5.5f, Types = new[] { "brute", "bandit", "archer" } },
                new PackDef { X = 5f, Z = -10f, R = 5f, Types = new[] { "brute", "brute", "bandit" } },
            },
            Pickups = new[]
            {
                new PickupDef { X = 1f, Z = 13f, Type = "herb" },
                new PickupDef { X = 14f, Z = 9f, Type = "ore" },
                new PickupDef { X = -14f, Z = 4f, Type = "scroll" },
                new PickupDef { X = -12f, Z = -9f, Type = "coin" },
                new PickupDef { X = 12f, Z = -12f, Type = "herb" },
                new PickupDef { X = 0f, Z = -6f, Type = "coin" },
            },
            Intro = Story.T3_Intro,
            BossIntro = Story.T3_BossIntro,
            Outro = Story.T3_Outro,
        });

        Maps.Add(new MapDef
        {
            Id = "hulao", Chapter = "第四章", Name = "虎牢关",
            Subtitle = "天下无双，关前无路", LevelRange = "9 以上", RecLevel = 10,
            Decor = "fortress", Boss = "lvbu", Ending = "虎牢关下",
            Fog = new Color(0.42f, 0.39f, 0.34f), GroundTint = new Color(0.61f, 0.63f, 0.66f),
            Mobs = new[] { "rider", "brute", "archer" },
            Packs = new[]
            {
                new PackDef { X = -9f, Z = 8f, R = 5f, Types = new[] { "rider", "brute" } },
                new PackDef { X = 10f, Z = 4f, R = 5f, Types = new[] { "archer", "archer", "rider" } },
                new PackDef { X = -2f, Z = -3f, R = 5.5f, Types = new[] { "rider", "rider" } },
                new PackDef { X = 5f, Z = -10f, R = 5f, Types = new[] { "rider", "brute", "archer" } },
            },
            Pickups = new[]
            {
                new PickupDef { X = 2f, Z = 13f, Type = "herb" },
                new PickupDef { X = 15f, Z = 10f, Type = "ore" },
                new PickupDef { X = -14f, Z = 5f, Type = "scroll" },
                new PickupDef { X = -12f, Z = -9f, Type = "coin" },
                new PickupDef { X = 12f, Z = -12f, Type = "herb" },
                new PickupDef { X = 0f, Z = -5f, Type = "coin" },
            },
            Intro = Story.T4_Intro,
            BossIntro = Story.T4_BossIntro,
            Outro = Story.T4_Outro,
        });

        Maps.Add(new MapDef
        {
            Id = "luoyang", Chapter = "第五章", Name = "洛阳 · 东郊",
            Subtitle = "宫阙无主，西凉铁骑横行", LevelRange = "12 以上", RecLevel = 13,
            Decor = "fortress", Boss = "dongzhuo", Ending = "洛阳已定",
            Fog = new Color(0.46f, 0.40f, 0.36f), GroundTint = new Color(0.70f, 0.66f, 0.62f),
            Mobs = new[] { "rider", "brute", "rider" },
            Packs = new[]
            {
                new PackDef { X = -10f, Z = 8f, R = 5.5f, Types = new[] { "rider", "rider" } },
                new PackDef { X = 11f, Z = 5f, R = 5.5f, Types = new[] { "brute", "rider", "archer" } },
                new PackDef { X = -3f, Z = -4f, R = 6f, Types = new[] { "rider", "brute", "rider" } },
                new PackDef { X = 6f, Z = -11f, R = 5.5f, Types = new[] { "brute", "archer", "rider" } },
            },
            Pickups = new[]
            {
                new PickupDef { X = 3f, Z = 13f, Type = "herb" },
                new PickupDef { X = 15f, Z = 10f, Type = "ore" },
                new PickupDef { X = -15f, Z = 6f, Type = "scroll" },
                new PickupDef { X = -13f, Z = -9f, Type = "coin" },
                new PickupDef { X = 13f, Z = -13f, Type = "herb" },
                new PickupDef { X = 0f, Z = -6f, Type = "coin" },
            },
            Intro = Story.T5_Intro,
            BossIntro = Story.T5_BossIntro,
            Outro = Story.T5_Outro,
        });

        Maps.Add(new MapDef
        {
            Id = "guandu", Chapter = "第六章", Name = "官渡 · 乌巢",
            Subtitle = "河北四州之众，尽在此处", LevelRange = "15 以上", RecLevel = 16,
            Decor = "wild", Boss = "yuanshao", Ending = "河北既平",
            Fog = new Color(0.55f, 0.50f, 0.45f), GroundTint = new Color(0.78f, 0.74f, 0.62f),
            Mobs = new[] { "rider", "brute", "rider", "archer" },
            Packs = new[]
            {
                new PackDef { X = -11f, Z = 9f, R = 6f, Types = new[] { "rider", "rider", "brute" } },
                new PackDef { X = 12f, Z = 6f, R = 6f, Types = new[] { "rider", "brute", "rider" } },
                new PackDef { X = -4f, Z = -4f, R = 6.5f, Types = new[] { "brute", "rider", "rider" } },
                new PackDef { X = 7f, Z = -12f, R = 6f, Types = new[] { "rider", "archer", "brute" } },
            },
            Pickups = new[]
            {
                new PickupDef { X = 4f, Z = 13f, Type = "herb" },
                new PickupDef { X = 16f, Z = 11f, Type = "ore" },
                new PickupDef { X = -16f, Z = 7f, Type = "scroll" },
                new PickupDef { X = -14f, Z = -10f, Type = "coin" },
                new PickupDef { X = 14f, Z = -13f, Type = "herb" },
                new PickupDef { X = 1f, Z = -6f, Type = "coin" },
            },
            Intro = Story.T6_Intro,
            BossIntro = Story.T6_BossIntro,
            Outro = Story.T6_Outro,
        });

        Maps.Add(new MapDef
        {
            Id = "weihe", Chapter = "第七章", Name = "渭水 · 潼关",
            Subtitle = "关中未定，此战定天下", LevelRange = "18 以上", RecLevel = 19,
            Decor = "pass", Boss = "machao", Ending = "天下三分",
            Fog = new Color(0.50f, 0.54f, 0.60f), GroundTint = new Color(0.72f, 0.76f, 0.82f),
            Mobs = new[] { "rider", "rider", "brute", "archer" },
            Packs = new[]
            {
                new PackDef { X = -12f, Z = 9f, R = 6.5f, Types = new[] { "rider", "rider", "brute" } },
                new PackDef { X = 13f, Z = 6f, R = 6.5f, Types = new[] { "brute", "rider", "archer" } },
                new PackDef { X = -5f, Z = -5f, R = 7f, Types = new[] { "rider", "brute", "rider" } },
                new PackDef { X = 8f, Z = -13f, R = 6.5f, Types = new[] { "archer", "rider", "brute" } },
                new PackDef { X = 0f, Z = 14f, R = 6f, Types = new[] { "rider", "rider" } },
            },
            Pickups = new[]
            {
                new PickupDef { X = 4f, Z = 13f, Type = "herb" },
                new PickupDef { X = 16f, Z = 11f, Type = "ore" },
                new PickupDef { X = -16f, Z = 7f, Type = "scroll" },
                new PickupDef { X = -14f, Z = -10f, Type = "coin" },
                new PickupDef { X = 14f, Z = -13f, Type = "herb" },
                new PickupDef { X = 1f, Z = -6f, Type = "coin" },
            },
            Intro = Story.T7_Intro,
            BossIntro = Story.T7_BossIntro,
            Outro = Story.T7_Outro,
        });
    }

    static void BuildPickups()
    {
        PickupNames["herb"] = "灵草";   PickupDescs["herb"] = "回复 30 气血";     PickupColors["herb"] = new Color(0.56f, 0.82f, 0.54f);
        PickupNames["ore"] = "玄铁";    PickupDescs["ore"] = "体魄 +4（永久）";   PickupColors["ore"] = new Color(0.72f, 0.77f, 0.81f);
        PickupNames["scroll"] = "古卷"; PickupDescs["scroll"] = "技能点 +1";     PickupColors["scroll"] = new Color(0.85f, 0.75f, 0.48f);
        PickupNames["coin"] = "铜钱";   PickupDescs["coin"] = "铜钱 +25";        PickupColors["coin"] = new Color(0.88f, 0.71f, 0.36f);
    }

    // ---------- 技能成长公式 ----------
    public static float SkillDamage(SkillDef s, int lv) => s.BaseDmg * (1f + 0.22f * (lv - 1));
    public static float SkillCooldown(SkillDef s, int lv) => s.BaseCd * (1f - 0.05f * (lv - 1));
    public static float SkillCost(SkillDef s, int lv) => s.BaseCost * (1f + 0.04f * (lv - 1));
    public static float SkillBuffAtk(int lv) => 0.32f + 0.04f * (lv - 1);
    public static float SkillBuffSpeed() => 0.12f;
    public static float SkillBuffDuration(SkillDef s, int lv) => s.Duration + (lv - 1) * 0.6f;
}
