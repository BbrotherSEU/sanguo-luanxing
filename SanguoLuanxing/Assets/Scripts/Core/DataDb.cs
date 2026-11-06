using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 数据驱动层：从 StreamingAssets/data/ 读 JSON 表。
/// 设计原则：读不到就用内置默认值，游戏照常能跑。
/// 这样数值、敌人、道具可以在不改代码的情况下调整，也方便后面接后台下发。
/// </summary>
public static class DataDb
{
    /// <summary>数据目录（相对 StreamingAssets）</summary>
    public const string Dir = "data";

    static bool loaded;

    /// <summary>哪些表成功从外部文件加载了（用于调试显示）</summary>
    public static readonly List<string> Loaded = new List<string>();

    // ---------------- JSON 可序列化的中间结构 ----------------
    // JsonUtility 不认 Color / 数组初始值，外部表用这些 DTO 承载，再转成运行时的定义类。

    [System.Serializable] public class UnitDto
    {
        public string Id, Name, Sprite, Title, Type;
        public bool Boss;
        public float Hp = 50f, Atk = 8f, Def = 2f, Speed = 2.2f, Scale = 1f;
        public float Range = 2f, Windup = 0.5f, Recover = 0.45f, Cd = 1.1f, Aggro = 9f;
        public float Exp = 15f, Gold = 8f, Poise = 0.3f;
        public float KeepMin, KeepMax, ArrowSpeed;
        public string[] Patterns;

        public UnitDef ToDef()
        {
            return new UnitDef
            {
                Id = Id, Name = Name, Sprite = Sprite ?? "bandit", Title = Title,
                Type = Type ?? "melee", Boss = Boss,
                Hp = Hp, Atk = Atk, Def = Def, Speed = Speed, Scale = Scale,
                Range = Range, Windup = Windup, Recover = Recover, Cd = Cd, Aggro = Aggro,
                Exp = Exp, Gold = Gold, Poise = Poise,
                KeepMin = KeepMin, KeepMax = KeepMax, ArrowSpeed = ArrowSpeed,
                Patterns = Patterns,
            };
        }
    }

    [System.Serializable] public class SkillDto
    {
        public string Id, Key, Name, Desc;
        public float BaseCd, BaseCost, BaseDmg, Radius, Duration;

        public SkillDef ToDef()
        {
            return new SkillDef { Id = Id, Key = Key, Name = Name, Desc = Desc,
                BaseCd = BaseCd, BaseCost = BaseCost, BaseDmg = BaseDmg, Radius = Radius, Duration = Duration };
        }
    }

    [System.Serializable] public class FactionDto
    {
        public string Id, Name, Leader, Title, PassiveName, PassiveDesc, StartWeapon;
        public float R = 1f, G = 1f, B = 1f;
        public float DefMul = 1f, AtkMul = 1f, SpeedMul = 1f, QiMul = 1f, CritAdd = 0f, HeavyMul = 1f;
        public string[] Intro;

        public FactionDef ToDef()
        {
            return new FactionDef
            {
                Id = Id, Name = Name, Leader = Leader, Title = Title,
                PassiveName = PassiveName, PassiveDesc = PassiveDesc, StartWeapon = StartWeapon,
                Color = new Color(R, G, B, 1f),
                DefMul = DefMul, AtkMul = AtkMul, SpeedMul = SpeedMul, QiMul = QiMul,
                CritAdd = CritAdd, HeavyMul = HeavyMul,
                Intro = Intro,
            };
        }
    }

    public static void EnsureLoaded()
    {
        if (loaded) return;
        loaded = true;

        // 敌人
        var enemies = ReadUnits("enemies");
        if (enemies != null && enemies.Length > 0)
        {
            DB.Enemies.Clear();
            for (int i = 0; i < enemies.Length; i++)
                if (enemies[i] != null && !string.IsNullOrEmpty(enemies[i].Id)) DB.Enemies[enemies[i].Id] = enemies[i].ToDef();
            Loaded.Add("enemies");
        }

        // Boss
        var bosses = ReadUnits("bosses");
        if (bosses != null && bosses.Length > 0)
        {
            DB.Bosses.Clear();
            for (int i = 0; i < bosses.Length; i++)
                if (bosses[i] != null && !string.IsNullOrEmpty(bosses[i].Id)) DB.Bosses[bosses[i].Id] = bosses[i].ToDef();
            Loaded.Add("bosses");
        }

        // 技能
        var skills = ReadSkills("skills");
        if (skills != null && skills.Length > 0)
        {
            var defs = new SkillDef[skills.Length];
            for (int i = 0; i < skills.Length; i++) defs[i] = skills[i].ToDef();
            DB.SkillsOverride = defs;
            Loaded.Add("skills");
        }

        // 阵营
        var factions = ReadFactions("factions");
        if (factions != null && factions.Length > 0)
        {
            DB.Factions.Clear();
            for (int i = 0; i < factions.Length; i++)
                if (factions[i] != null && !string.IsNullOrEmpty(factions[i].Id)) DB.Factions[factions[i].Id] = factions[i].ToDef();
            Loaded.Add("factions");
        }
    }

    /// <summary>
    /// JsonUtility 只认顶层对象，直接喂数组会报 "Return type must represent an object type"。
    /// 数据文件保持成纯数组（方便手写），读进来先包一层 {"items": [...]} 再解析。
    /// </summary>
    static string WrapArray(string json)
    {
        string t = json.TrimStart();
        if (t.StartsWith("[")) return "{\"items\":" + json + "}";
        return json;
    }

    static UnitDto[] ReadUnits(string name)
    {
        string json = ReadJson(name);
        if (json == null) return null;
        try
        {
            var w = JsonUtility.FromJson<UnitTable>(WrapArray(json));
            return (w != null && w.items != null && w.items.Length > 0) ? w.items : null;
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("[DataDb] 读取 " + name + ".json 失败，用内置默认值：" + e.Message);
            return null;
        }
    }

    static SkillDto[] ReadSkills(string name)
    {
        string json = ReadJson(name);
        if (json == null) return null;
        try
        {
            var w = JsonUtility.FromJson<SkillTable>(WrapArray(json));
            return (w != null && w.items != null && w.items.Length > 0) ? w.items : null;
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("[DataDb] 读取 " + name + ".json 失败，用内置默认值：" + e.Message);
            return null;
        }
    }

    static FactionDto[] ReadFactions(string name)
    {
        string json = ReadJson(name);
        if (json == null) return null;
        try
        {
            var w = JsonUtility.FromJson<FactionTable>(WrapArray(json));
            return (w != null && w.items != null && w.items.Length > 0) ? w.items : null;
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("[DataDb] 读取 " + name + ".json 失败，用内置默认值：" + e.Message);
            return null;
        }
    }

    /// <summary>读原始 JSON 文本。文件不存在或为空都返回 null。</summary>
    static string ReadJson(string name)
    {
        string path = System.IO.Path.Combine(Application.streamingAssetsPath, Dir, name + ".json");
        if (!System.IO.File.Exists(path)) return null;
        try
        {
            string json = System.IO.File.ReadAllText(path);
            return string.IsNullOrEmpty(json.Trim()) ? null : json;
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("[DataDb] 读取 " + name + ".json 失败，用内置默认值：" + e.Message);
            return null;
        }
    }

    [System.Serializable] public class UnitTable { public UnitDto[] items; }
    [System.Serializable] public class SkillTable { public SkillDto[] items; }
    [System.Serializable] public class FactionTable { public FactionDto[] items; }
}
