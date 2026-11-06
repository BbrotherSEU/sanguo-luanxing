using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class ItemDef
{
    public int uid;
    public string slot;
    public string baseName;
    public int level;
    public int rarity;
    public int up;
    public int atk;
    public int def;
    public int hp;
}

/// <summary>装备生成与成长公式（移植 items.js）</summary>
public static class Items
{
    public static readonly string[] SlotIds = { "weapon", "armor", "accessory" };
    public static readonly string[] SlotNames = { "兵器", "甲胄", "随身" };
    public static readonly string[] RarityNames = { "凡品", "良品", "上品", "名器", "神兵" };
    public static readonly float[] RarityMult = { 1.0f, 1.28f, 1.58f, 1.95f, 2.45f };
    public static readonly int[] RarityWeight = { 46, 28, 17, 7, 2 };
    public static readonly Color[] RarityColors =
    {
        new Color(0.725f, 0.698f, 0.639f),
        new Color(0.498f, 0.690f, 0.478f),
        new Color(0.435f, 0.620f, 0.839f),
        new Color(0.851f, 0.647f, 0.361f),
        new Color(0.851f, 0.478f, 0.416f),
    };

    static readonly Dictionary<string, string[]> Names = new Dictionary<string, string[]>
    {
        { "weapon", new[] { "环首刀", "铁枪", "双戟", "青釭剑", "丈八蛇矛", "古锭刀", "大斧" } },
        { "armor", new[] { "皮甲", "锁子甲", "鱼鳞甲", "明光铠", "玄铁重铠" } },
        { "accessory", new[] { "护心镜", "玉佩", "虎符", "兵书", "赤兔铃" } },
    };

    static int uidSeq = 1;

    public static int RandomRarity(float boost = 0f)
    {
        float total = 0f;
        var w = new float[RarityWeight.Length];
        for (int i = 0; i < w.Length; i++)
        {
            w[i] = RarityWeight[i] * (i == 0 ? 1f - boost * 0.6f : 1f + boost * 0.8f);
            total += w[i];
        }
        float roll = UnityEngine.Random.value * total;
        for (int i = 0; i < w.Length; i++)
        {
            roll -= w[i];
            if (roll <= 0f) return i;
        }
        return 0;
    }

    public static ItemDef Make(int level, string slotId = null, int rarityIdx = -1)
    {
        string slot = string.IsNullOrEmpty(slotId)
            ? SlotIds[UnityEngine.Random.Range(0, SlotIds.Length)]
            : slotId;
        int ri = rarityIdx < 0 ? RandomRarity() : rarityIdx;
        float mult = RarityMult[ri];
        int lv = Mathf.Max(1, Mathf.RoundToInt(level));

        string[] pool = Names[slot];
        var item = new ItemDef
        {
            uid = uidSeq++,
            slot = slot,
            baseName = pool[Mathf.Min(pool.Length - 1, Mathf.FloorToInt(lv / 3f))],
            level = lv,
            rarity = ri,
            up = 0,
        };

        if (slot == "weapon") item.atk = Mathf.RoundToInt((3f + lv * 2.6f) * mult);
        if (slot == "armor")
        {
            item.def = Mathf.RoundToInt((2f + lv * 1.7f) * mult);
            item.hp = Mathf.RoundToInt(lv * 7f * mult);
        }
        if (slot == "accessory")
        {
            item.hp = Mathf.RoundToInt((8f + lv * 8f) * mult);
            item.atk = Mathf.RoundToInt(lv * 0.8f * mult);
        }
        return item;
    }

    public static void Stats(ItemDef it, out int atk, out int def, out int hp)
    {
        if (it == null) { atk = def = hp = 0; return; }
        float k = 1f + it.up * 0.09f;
        atk = Mathf.RoundToInt(it.atk * k);
        def = Mathf.RoundToInt(it.def * k);
        hp = Mathf.RoundToInt(it.hp * k);
    }

    public static string DisplayName(ItemDef it)
    {
        if (it == null) return "未着";
        return it.baseName + (it.up > 0 ? " +" + it.up : "");
    }

    public static string Desc(ItemDef it)
    {
        if (it == null) return "";
        Stats(it, out int a, out int d, out int h);
        var parts = new List<string>();
        if (a != 0) parts.Add("攻 +" + a);
        if (d != 0) parts.Add("防 +" + d);
        if (h != 0) parts.Add("体 +" + h);
        return string.Join("  ", parts);
    }

    public static float Score(ItemDef it)
    {
        if (it == null) return 0f;
        Stats(it, out int a, out int d, out int h);
        return a * 2.2f + d * 2.0f + h * 0.5f;
    }

    public static int UpgradeCost(ItemDef it) => Mathf.RoundToInt(60f + it.level * 26f + it.up * 90f);

    public static float UpgradeChance(ItemDef it) => Mathf.Max(0.18f, 0.85f - it.up * 0.12f);
}
