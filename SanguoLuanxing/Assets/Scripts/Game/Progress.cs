using System;
using System.Collections.Generic;
using UnityEngine;

public struct Stats
{
    public float MaxHP, Atk, Def, Crit, Speed, QiMul, HeavyMul;
}

[Serializable]
public class SaveData
{
    public string faction = "";
    public int level = 1;
    public int exp;
    public int gold;
    public int sp;
    public int attrPts;       // 自由属性点
    public int talentPts;     // 天赋点
    public int atrStr;        // 力量 → 攻击
    public int atrVit;        // 体质 → 气血
    public int atrAgi;        // 身法 → 速度/暴击
    public int[] talentRank = new int[9];
    public string questDoneCsv = "";
    public string flagsCsv = "";
    public string evSeenCsv = "";   // 已经遇过的野外偶遇 id（| 分隔），用于事件连锁
    public int region;
    public int totalKills;
    public int bonusHp, bonusAtk, bonusDef;
    public ItemDef weapon, armor, accessory;
    public List<ItemDef> bag = new List<ItemDef>();
    public int[] skillLv = { 1, 1, 1 };
    public bool[] cleared = new bool[4];
    public int renown;           // 声望：决定仕途官阶
    public int renownThisLevel;  // 本级已攒的声望（用于显示进度条）
    public int[] renownMark = new int[6];   // 各官阶的升阶标记，防重复发奖
}

/// <summary>存档 / 等级 / 属性汇总</summary>
public class Progress
{
    const string Key = "sanguo.luanxing.save.v2";

    public SaveData Data = new SaveData();

    public bool HasSave => PlayerPrefs.HasKey(Key) || PlayerPrefs.HasKey("sanguo.luanxing.save.v1");

    public bool Load()
    {
        string raw = PlayerPrefs.GetString(Key, "");
        if (string.IsNullOrEmpty(raw))
            raw = PlayerPrefs.GetString("sanguo.luanxing.save.v1", "");
        if (string.IsNullOrEmpty(raw)) return false;
        try
        {
            Data = JsonUtility.FromJson<SaveData>(raw) ?? new SaveData();
            Normalize();
            return true;
        }
        catch (Exception e)
        {
            Debug.LogWarning("[Progress] 存档损坏: " + e.Message);
            return false;
        }
    }

    void Normalize()
    {
        if (Data.weapon != null && Data.weapon.uid == 0) Data.weapon = null;
        if (Data.armor != null && Data.armor.uid == 0) Data.armor = null;
        if (Data.accessory != null && Data.accessory.uid == 0) Data.accessory = null;
        if (Data.bag == null) Data.bag = new List<ItemDef>();
        Data.bag.RemoveAll(i => i == null || i.uid == 0);
        if (Data.skillLv == null || Data.skillLv.Length != 3) Data.skillLv = new[] { 1, 1, 1 };
        // 关卡数会随剧情扩充，存档里的通关标记数组长度要跟着走。
        // 只扩容不缩容，并把旧标记搬过去 —— 否则新增一章就会把玩家已定的关卡清空。
        if (Data.cleared == null) Data.cleared = new bool[DB.Maps.Count];
        else if (Data.cleared.Length != DB.Maps.Count)
        {
            var old = Data.cleared;
            Data.cleared = new bool[DB.Maps.Count];
            for (int i = 0; i < old.Length && i < Data.cleared.Length; i++) Data.cleared[i] = old[i];
        }
        if (Data.renownMark == null || Data.renownMark.Length != Career.Ranks.Length)
            Data.renownMark = new int[Career.Ranks.Length];
        if (Data.talentRank == null || Data.talentRank.Length != 9) Data.talentRank = new int[9];
        if (Data.questDoneCsv == null) Data.questDoneCsv = "";
        if (Data.flagsCsv == null) Data.flagsCsv = "";
        if (Data.evSeenCsv == null) Data.evSeenCsv = "";
        Data.region = Mathf.Clamp(Data.region, 0, 8);
        // 旧档补发：已有等级但从未拿过属性/天赋点
        if (Data.level > 1 && Data.attrPts == 0 && Data.atrStr == 0 && Data.atrVit == 0 && Data.atrAgi == 0)
            Data.attrPts = (Data.level - 1) * 3;
        if (Data.level > 1 && Data.talentPts == 0)
        {
            bool anyTalent = false;
            for (int i = 0; i < Data.talentRank.Length; i++)
                if (Data.talentRank[i] > 0) { anyTalent = true; break; }
            if (!anyTalent) Data.talentPts = Data.level - 1;
        }

        // 一次性：清空任务/关卡进度，保留等级与养成（世界地图任务链）
        const string CampaignFlag = "sanguo.campaign.reset.v4";
        if (!PlayerPrefs.HasKey(CampaignFlag))
        {
            ResetCampaign();
            PlayerPrefs.SetInt(CampaignFlag, 1);
            Save();
        }
    }

    /// <summary>重置任务与关卡通关；保留等级、经验、铜钱、装备、属性/天赋点</summary>
    public void ResetCampaign()
    {
        Data.questDoneCsv = "";
        Data.flagsCsv = "";
        Data.evSeenCsv = "";
        Data.region = 0;
        Data.cleared = new bool[DB.Maps.Count];
        Data.totalKills = 0;
    }

    /// <summary>野外偶遇是否已经遇过（用于事件连锁）。</summary>
    public bool EvSeen(string id)
    {
        if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(Data.evSeenCsv)) return false;
        var parts = Data.evSeenCsv.Split('|');
        for (int i = 0; i < parts.Length; i++)
            if (parts[i] == id) return true;
        return false;
    }

    public void MarkEvSeen(string id)
    {
        if (string.IsNullOrEmpty(id) || EvSeen(id)) return;
        Data.evSeenCsv = string.IsNullOrEmpty(Data.evSeenCsv) ? id : Data.evSeenCsv + "|" + id;
    }

    public bool HasFlag(string id)
    {
        if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(Data.flagsCsv)) return false;
        var parts = Data.flagsCsv.Split('|');
        for (int i = 0; i < parts.Length; i++)
            if (parts[i] == id) return true;
        return false;
    }

    public void SetFlag(string id)
    {
        if (string.IsNullOrEmpty(id) || HasFlag(id)) return;
        Data.flagsCsv = string.IsNullOrEmpty(Data.flagsCsv) ? id : Data.flagsCsv + "|" + id;
    }

    /// <summary>清掉某个旗号（改主意时用）。</summary>
    public void ClearFlag(string id)
    {
        if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(Data.flagsCsv)) return;
        var parts = Data.flagsCsv.Split('|');
        var kept = new List<string>();
        for (int i = 0; i < parts.Length; i++)
            if (parts[i] != id && !string.IsNullOrEmpty(parts[i])) kept.Add(parts[i]);
        Data.flagsCsv = string.Join("|", kept.ToArray());
    }

    public bool QuestDone(string id)
    {
        if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(Data.questDoneCsv)) return false;
        var parts = Data.questDoneCsv.Split('|');
        for (int i = 0; i < parts.Length; i++)
            if (parts[i] == id) return true;
        return false;
    }

    public void MarkQuestDone(string id)
    {
        if (string.IsNullOrEmpty(id) || QuestDone(id)) return;
        Data.questDoneCsv = string.IsNullOrEmpty(Data.questDoneCsv) ? id : Data.questDoneCsv + "|" + id;
    }

    public void Save() => PlayerPrefs.SetString(Key, JsonUtility.ToJson(Data));

    public void Reset()
    {
        Data = new SaveData();
        PlayerPrefs.DeleteKey(Key);
        PlayerPrefs.DeleteKey("sanguo.luanxing.save.v1");
    }

    public FactionDef Faction
    {
        get
        {
            if (string.IsNullOrEmpty(Data.faction)) return null;
            return DB.Factions.TryGetValue(Data.faction, out var f) ? f : null;
        }
    }

    public void GiveStarterWeapon()
    {
        var f = Faction;
        if (f == null) return;
        var item = Items.Make(1, "weapon", 0);
        item.baseName = f.StartWeapon;
        Data.weapon = item;
        // 开局送一点点数，立刻能体验加点
        if (Data.attrPts < 3) Data.attrPts = 3;
        if (Data.talentPts < 1) Data.talentPts = 1;
    }

    public int ExpNeeded() => Mathf.RoundToInt(110f * Mathf.Pow(1.22f, Data.level - 1));

    public Stats Stats()
    {
        FactionDef f = Faction;
        float atkMul = f != null ? f.AtkMul : 1f;
        float defMul = f != null ? f.DefMul : 1f;
        float speedMul = f != null ? f.SpeedMul : 1f;
        float qiMul = f != null ? f.QiMul : 1f;
        float critAdd = f != null ? f.CritAdd : 0f;
        float heavyMul = f != null ? f.HeavyMul : 1f;

        int eqAtk = 0, eqDef = 0, eqHp = 0;
        foreach (var slotItem in new[] { Data.weapon, Data.armor, Data.accessory })
        {
            Items.Stats(slotItem, out int a, out int d, out int h);
            eqAtk += a; eqDef += d; eqHp += h;
        }

        int lv = Data.level;
        var s = new Stats
        {
            MaxHP = Mathf.Round(120f + 30f * (lv - 1) + Data.bonusHp + eqHp + Data.atrVit * 12f),
            Atk = Mathf.Round((12f + 3.6f * (lv - 1) + Data.bonusAtk + eqAtk + Data.atrStr * 2.2f) * atkMul),
            Def = Mathf.Round((4f + 1.3f * (lv - 1) + Data.bonusDef + eqDef + Data.atrVit * 0.4f) * defMul),
            Crit = 0.05f + critAdd + Data.atrAgi * 0.008f,
            Speed = (6.4f + Data.atrAgi * 0.12f) * speedMul,
            QiMul = qiMul,
            HeavyMul = heavyMul,
        };
        Talents.ApplyToStats(this, ref s);
        Career.ApplyToStats(this, ref s);   // 官阶加成：小人物往上走，看得见
        return s;
    }

    public int GainExp(int amount)
    {
        Data.exp += amount;
        int gained = 0;
        while (Data.exp >= ExpNeeded())
        {
            Data.exp -= ExpNeeded();
            Data.level += 1;
            Data.sp += 1;
            Data.attrPts += 3;
            Data.talentPts += 1;
            gained++;
            if (Data.level > 40) { Data.exp = 0; break; }
        }
        return gained;
    }

    public bool SpendAttr(int which)
    {
        // 0=力量 1=体质 2=身法
        if (Data.attrPts <= 0) return false;
        Data.attrPts -= 1;
        if (which == 0) Data.atrStr++;
        else if (which == 1) Data.atrVit++;
        else if (which == 2) Data.atrAgi++;
        else { Data.attrPts++; return false; }
        return true;
    }

    public ItemDef EquipItem(ItemDef item)
    {
        ItemDef prev = null;
        switch (item.slot)
        {
            case "weapon": prev = Data.weapon; Data.weapon = item; break;
            case "armor": prev = Data.armor; Data.armor = item; break;
            case "accessory": prev = Data.accessory; Data.accessory = item; break;
        }
        int idx = Data.bag.FindIndex(i => i.uid == item.uid);
        if (idx >= 0) Data.bag.RemoveAt(idx);
        if (prev != null) Data.bag.Add(prev);
        return prev;
    }

    public bool AutoEquipBetter(ItemDef item)
    {
        ItemDef cur = CurrentInSlot(item.slot);
        if (cur == null || Items.Score(item) > Items.Score(cur))
        {
            EquipItem(item);
            return true;
        }
        return false;
    }

    public void AddItem(ItemDef item)
    {
        if (!AutoEquipBetter(item)) Data.bag.Add(item);
    }

    ItemDef CurrentInSlot(string slot)
    {
        switch (slot)
        {
            case "weapon": return Data.weapon;
            case "armor": return Data.armor;
            case "accessory": return Data.accessory;
        }
        return null;
    }

    public bool UpgradeSkill(int index)
    {
        if (Data.sp <= 0) return false;
        if (Data.skillLv[index] >= 5) return false;
        Data.sp -= 1;
        Data.skillLv[index] += 1;
        return true;
    }

    public bool UpgradeEquip(string slot, out string msg)
    {
        msg = "";
        ItemDef it = CurrentInSlot(slot);
        if (it == null) { msg = "此位未着装备"; return false; }
        int cost = Items.UpgradeCost(it);
        if (Data.gold < cost) { msg = "铜钱不足"; return false; }
        Data.gold -= cost;
        if (UnityEngine.Random.value < Items.UpgradeChance(it))
        {
            it.up += 1;
            msg = Items.DisplayName(it) + " 淬炼成功";
            return true;
        }
        msg = Items.DisplayName(it) + " 淬炼失败";
        return false;
    }

    public ItemDef Equipped(string slot) => CurrentInSlot(slot);
}
