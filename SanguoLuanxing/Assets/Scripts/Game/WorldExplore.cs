using System.Collections.Generic;
using UnityEngine;

enum SpotKind { Npc, Collect, Gate, Travel, Flavor }

class WorldSpot
{
    public SpotKind Kind;
    public string QuestId;
    public string Label;
    public string Portrait;
    public Line[] Lines;
    public string CollectType;
    public int GateMap;
    public int TravelTo = -1;
    public Vector3 TravelSpawn;
    public float X, Z;
    public GameObject Go;
    public SpriteRenderer Body, Glow;
    public LootRig Loot;
    public bool Taken;
    public bool Repeatable;
    public float Phase;
}

/// <summary>
/// 仙剑式可走动大地图：桃林野外走动、找人对话、拾取、走近副本入口开战。
/// </summary>
public class WorldExplore
{
    public Game G;
    public Arena Arena;
    public GameObject HeroGo;
    public Vector3 Pos;
    public string Prompt = "";

    SpriteRenderer heroBody, heroShadow;
    FrameAnim anim;
    float aim = -Mathf.PI / 2f;
    float moveTime, breath;
    float speed = 6.2f;
    float height = 2.5f;

    readonly List<WorldSpot> spots = new List<WorldSpot>();
    readonly List<WorldEvents.EvSpot> evSpots = new List<WorldEvents.EvSpot>();
    WorldSpot near;
    WorldEvents.EvSpot nearEv;
    float interactLock;
    int collectHave;
    const float BoundFallback = 22f;
    public int RegionIndex;
    bool mapOpen;

    // 偶遇刷新：进区域先给一个，走一段路再给
    float evTimer;
    System.Random evRng = new System.Random();
    const int MaxEvSpots = 3;

    class RegionInfo
    {
        public string Name, Poem, Decor, Chapter;
        public int Battle = -1;
    }

    static readonly RegionInfo[] Regions =
    {
        new RegionInfo { Name = "涿郡 · 桃源", Poem = "桃花深处，可遇故人", Decor = "peach", Chapter = "第一章", Battle = 0 },
        new RegionInfo { Name = "汜水关外", Poem = "关外风雪，酒尚未温", Decor = "pass", Chapter = "第二章", Battle = 1 },
        new RegionInfo { Name = "长坂坡野", Poem = "秋尘一骑，桥还在东", Decor = "ruins", Chapter = "第三章", Battle = 2 },
        new RegionInfo { Name = "虎牢关下", Poem = "夜火连营，戟指中原", Decor = "fortress", Chapter = "第四章", Battle = 3 },
        new RegionInfo { Name = "洛阳 · 东郊", Poem = "宫阙无主，铁骑横行", Decor = "fortress", Chapter = "第五章", Battle = 4 },
        new RegionInfo { Name = "官渡 · 乌巢", Poem = "粮道千里，一炬可焚", Decor = "wild", Chapter = "第六章", Battle = 5 },
        new RegionInfo { Name = "渭水 · 潼关", Poem = "关中未平，此战定天", Decor = "pass", Chapter = "第七章", Battle = 6 },
        new RegionInfo { Name = "江夏 · 渔村", Poem = "芦花浅水，渔火一点", Decor = "village", Chapter = "闲游" },
        new RegionInfo { Name = "官渡 · 原野", Poem = "平野无边，炊烟数缕", Decor = "wild", Chapter = "闲游" },
    };

    static readonly int[] MapOrder = { 0, 1, 2, 4, 3, 5, 6, 7, 8 };

    static readonly Vector3[] RegionSpawn =
    {
        new Vector3(0f, 0f, 10f),
        new Vector3(0f, 0f, 10f),
        new Vector3(0f, 0f, 10f),
        new Vector3(0f, 0f, 8f),
        new Vector3(0f, 0f, 8f),
        new Vector3(0f, 0f, 8f),
        new Vector3(0f, 0f, 8f),
        new Vector3(0f, 0f, 8f),
        new Vector3(0f, 0f, 8f),
    };

    float WalkBound => Arena != null && Arena.PlayBound > 1f ? Arena.PlayBound : BoundFallback;

    // 探索相机更俯一些，接近仙剑视野
    float savedH, savedB, savedL;

    public WorldExplore(Game g)
    {
        G = g;
        RegionIndex = Mathf.Clamp(g.Progress.Data.region, 0, Regions.Length - 1);
        var map = WorldMap(RegionIndex);

        Arena = Arena.Build(map, g.transform, g.Cam);
        BuildHero();
        BuildSpots();

        savedH = g.Rig.Height;
        savedB = g.Rig.Back;
        savedL = g.Rig.Lead;
        g.Rig.Height = 10.2f;
        g.Rig.Back = 8.0f;
        g.Rig.Lead = 1.8f;
        g.Rig.SnapTo(Pos);
        if (Arena.Theme != null) g.Cam.backgroundColor = Arena.Theme.Sky;
    }

    static MapDef WorldMap(int region)
    {
        var info = Regions[Mathf.Clamp(region, 0, Regions.Length - 1)];
        MapDef src = info.Battle >= 0 ? DB.Maps[info.Battle] : null;
        var theme = Themes.ForDecor(info.Decor);
        return new MapDef
        {
            Id = "world_" + (src != null ? src.Id : info.Decor),
            Chapter = info.Chapter,
            Name = info.Name,
            Subtitle = info.Poem,
            Decor = info.Decor,
            Fog = src != null ? src.Fog : theme.Fog,
            GroundTint = src != null ? src.GroundTint : Color.white,
            Boss = null,
            Mobs = new string[0],
            Packs = new PackDef[0],
            Pickups = new PickupDef[0],
        };
    }

    void BuildHero()
    {
        Pos = RegionSpawn[Mathf.Clamp(RegionIndex, 0, RegionSpawn.Length - 1)];
        HeroGo = new GameObject("ExploreHero");
        HeroGo.transform.position = Pos;

        float asp;
        var tex = Tex.Cutout("char/player.jpg", out asp);
        heroBody = Tex.Actor(HeroGo.transform, Tex.SpriteFor(tex, height), 0);

        var sh = new GameObject("shadow").transform;
        sh.SetParent(HeroGo.transform, false);
        sh.localPosition = new Vector3(0f, 0.03f, 0f);
        sh.rotation = Quaternion.Euler(-90f, 0f, 0f);
        heroShadow = sh.gameObject.AddComponent<SpriteRenderer>();
        heroShadow.sprite = Tex.CenterSprite(Tex.Shadow, 1.8f);
        heroShadow.color = new Color(0f, 0f, 0f, 0.4f);

        anim = new FrameAnim(heroBody);
        anim.Load("player", "idle", 2, 2.5f, true, height);
        anim.Load("player", "walk", 4, 6f, true, height);
        if (!anim.Has("walk")) anim.Load("player", "walk", 6, 10f, true, height);
    }

    void BuildSpots()
    {
        switch (RegionIndex)
        {
            case 0:
                AddNpc("q1_talk", "刘备", "npc/liu_bei.jpg", -6f, 6f, Quests.Get("q1_talk")?.Lines);
                AddNpc("q1_side", "苏婉", "npc/suwan.jpg", 9.5f, 8.2f, Quests.Get("q1_side")?.Lines);
                AddNpc("q1_side2", "阿福", "npc/afu.jpg", -11f, 11.5f, Quests.Get("q1_side2")?.Lines);
                AddFlavor("织工", "npc/weaver.jpg", 6f, 12f, Story.PeachWeaver);
                AddCollects("q1_collect", "herb", 3, new[]
                {
                    new Vector2(5f, 8f), new Vector2(10f, 3f), new Vector2(-11f, 2f),
                    new Vector2(3f, -4f), new Vector2(-8f, -6f),
                });
                AddGate("q1_battle", 0, "桃林战阵", 0f, -16f);
                AddTravel(1, "北上汜水", 0f, 34f, RegionSpawn[1]);
                AddTravel(7, "东去江夏", 34f, 4f, RegionSpawn[7]);
                break;
            case 1:
                AddNpc("q2_talk", "曹操", "npc/cao_cao.jpg", -6f, 8f, Quests.Get("q2_talk")?.Lines);
                AddNpc("q2_side", "酒贩", "npc/wine.jpg", 8f, 6f, Quests.Get("q2_side")?.Lines);
                AddNpc("q2_side2", "斥候", "npc/scout.jpg", 12.5f, -1.5f, Quests.Get("q2_side2")?.Lines);
                AddFlavor("关吏", "npc/clerk.jpg", -12f, 2f, Story.PassScout);
                AddCollects("q2_collect", "scroll", 3, new[]
                {
                    new Vector2(6f, 2f), new Vector2(-10f, -4f), new Vector2(10f, -8f),
                    new Vector2(-8f, 4f),
                });
                AddGate("q2_battle", 1, "汜水关", 0f, -16f);
                AddTravel(0, "南回桃源", 0f, -34f, RegionSpawn[0]);
                AddTravel(2, "东去长坂", 34f, 0f, RegionSpawn[2]);
                break;
            case 2:
                AddNpc("q3_talk", "刘备", "npc/liu_bei.jpg", -8f, 8f, Quests.Get("q3_talk")?.Lines);
                AddNpc("q3_side", "难民", "npc/refugee.jpg", 8f, 4f, Quests.Get("q3_side")?.Lines);
                AddNpc("q3_side2", "赵仓", "npc/zhaocang.jpg", -12f, 6f, Quests.Get("q3_side2")?.Lines);
                AddCollects("q3_collect", "herb", 3, new[]
                {
                    new Vector2(-12f, 6f), new Vector2(10f, 8f), new Vector2(-6f, -6f),
                    new Vector2(4f, -8f),
                });
                AddGate("q3_battle", 2, "长坂坡", 0f, -16f);
                AddTravel(1, "西回汜水", -34f, 0f, RegionSpawn[1]);
                AddTravel(3, "北上虎牢", 0f, 34f, RegionSpawn[3]);
                AddTravel(5, "东去官渡", 34f, 2f, RegionSpawn[5]);
                break;
            case 3:
                AddNpc("q4_talk", "曹操", "npc/cao_cao.jpg", -6f, 6f, Quests.Get("q4_talk")?.Lines);
                AddNpc("q4_side", "军士", "npc/guard.jpg", 7f, 4f, Quests.Get("q4_side")?.Lines);
                AddNpc("q4_side2", "老卒", "npc/veteran.jpg", 12f, -1f, Quests.Get("q4_side2")?.Lines);
                AddCollects("q4_collect", "ore", 3, new[]
                {
                    new Vector2(4f, 10f), new Vector2(-10f, 8f), new Vector2(10f, -6f),
                    new Vector2(-8f, -4f),
                });
                AddGate("q4_battle", 3, "虎牢关", 0f, -14f);
                AddTravel(2, "南回长坂", 0f, -34f, RegionSpawn[2]);
                AddTravel(4, "西入洛阳", -34f, 2f, RegionSpawn[4]);
                break;
            case 4:
                AddNpc("q5_talk", "曹操", "npc/cao_cao.jpg", -6f, 6f, Quests.Get("q5_talk")?.Lines);
                AddNpc("q5_side", "孩童", "npc/boy.jpg", 8f, 5f, Quests.Get("q5_side")?.Lines);
                AddNpc("q5_side2", "书吏", "npc/scribe.jpg", -12f, 8f, Quests.Get("q5_side2")?.Lines);
                AddNpc("q5_side3", "妇人", "npc/woman.jpg", 6f, 11f, Quests.Get("q5_side3")?.Lines);
                AddCollects("q5_collect", "scroll", 3, new[]
                {
                    new Vector2(5f, 9f), new Vector2(-10f, 7f), new Vector2(10f, -5f),
                    new Vector2(-7f, -4f),
                });
                AddGate("q5_battle", 4, "洛阳东郊", 0f, -15f);
                AddTravel(3, "东回虎牢", 34f, 2f, RegionSpawn[3]);
                AddTravel(5, "东去官渡", 34f, -2f, RegionSpawn[5]);
                break;
            case 5:
                AddNpc("q6_talk", "曹操", "npc/cao_cao.jpg", -6f, 7f, Quests.Get("q6_talk")?.Lines);
                AddNpc("q6_side", "牧人", "npc/muren.jpg", 8f, 5f, Quests.Get("q6_side")?.Lines);
                AddNpc("q6_side2", "老兵", "npc/elder.jpg", -12f, 8f, Quests.Get("q6_side2")?.Lines);
                AddNpc("q6_side3", "店主", "npc/merchant.jpg", 7f, 11f, Quests.Get("q6_side3")?.Lines);
                AddCollects("q6_collect", "ore", 3, new[]
                {
                    new Vector2(4f, 10f), new Vector2(-10f, 8f), new Vector2(11f, -6f),
                    new Vector2(-8f, -5f),
                });
                AddGate("q6_battle", 5, "乌巢粮道", 0f, -15f);
                AddTravel(4, "西回洛阳", -34f, 2f, RegionSpawn[4]);
                AddTravel(6, "西去渭水", -34f, -2f, RegionSpawn[6]);
                AddTravel(8, "南去原野", 0f, -34f, RegionSpawn[8]);
                break;
            case 6:
                AddNpc("q7_talk", "曹操", "npc/cao_cao.jpg", -6f, 7f, Quests.Get("q7_talk")?.Lines);
                AddNpc("q7_side", "凉州军士", "npc/liangjiang.jpg", 8f, 5f, Quests.Get("q7_side")?.Lines);
                AddNpc("q7_side2", "书吏", "npc/shusheng.jpg", -12f, 8f, Quests.Get("q7_side2")?.Lines);
                AddNpc("q7_side3", "老船工", "npc/laochuanfu.jpg", 7f, 11f, Quests.Get("q7_side3")?.Lines);
                AddFlavor("船家女", "npc/chuanfu.jpg", 12f, 8f, Story.WeiheChuanfu);
                AddCollects("q7_collect", "ore", 3, new[]
                {
                    new Vector2(4f, 10f), new Vector2(-10f, 8f), new Vector2(11f, -6f),
                    new Vector2(-8f, -5f),
                });
                AddGate("q7_battle", 6, "渭水 · 潼关", 0f, -15f);
                AddTravel(5, "东回乌巢", 34f, 2f, RegionSpawn[5]);
                break;
            case 7:
                AddFlavor("渔翁", "npc/fisher.jpg", 7f, 9f, Story.JiangxiaFisher);
                AddFlavor("织女", "npc/maiden.jpg", -9f, 12f, Story.JiangxiaWeaver);
                AddTravel(0, "西回桃源", -34f, 4f, RegionSpawn[0]);
                break;
            case 8:
                AddFlavor("驿卒", "npc/postman.jpg", -6f, 8f, Story.GuanduPost);
                AddFlavor("牧童", "npc/herd.jpg", 10f, 6f, Story.GuanduHerder);
                AddTravel(2, "西回长坂", -34f, 2f, RegionSpawn[2]);
                AddTravel(5, "北上乌巢", 0f, 34f, RegionSpawn[5]);
                AddTravel(6, "西北去渭水", -34f, -2f, RegionSpawn[6]);
                break;
        }

        RefreshSpotVisibility();
    }

    public void Travel(int region, Vector3 spawn)
    {
        region = Mathf.Clamp(region, 0, Regions.Length - 1);
        if (!RegionOpen(region))
        {
            G.Hud.Toast("此路未开 · " + RegionLockReason(region));
            return;
        }
        for (int i = 0; i < spots.Count; i++)
            if (spots[i].Go != null) Object.Destroy(spots[i].Go);
        spots.Clear();
        // 偶遇点也随区域一起换掉
        for (int i = 0; i < evSpots.Count; i++)
            if (evSpots[i].Go != null) Object.Destroy(evSpots[i].Go);
        evSpots.Clear();
        evTimer = 6f;   // 到新地方先走一会儿再遇事
        if (Arena != null) Arena.Dispose();

        RegionIndex = region;
        G.Progress.Data.region = region;
        G.Progress.Save();
        Arena = Arena.Build(WorldMap(region), G.transform, G.Cam);
        if (Arena.Theme != null) G.Cam.backgroundColor = Arena.Theme.Sky;
        Pos = spawn;
        if (HeroGo != null) HeroGo.transform.position = Pos;
        BuildSpots();
        G.Rig.SnapTo(Pos);
        G.Hud.Toast("进入" + Regions[region].Name);
    }

    bool RegionOpen(int region)
    {
        if (region < 0 || region >= Regions.Length) return false;
        var info = Regions[region];
        // 闲游之地（江夏、原野）不受限制
        if (info.Battle < 0) return true;
        // 战场之地：要先了结上一章的事，且官阶要够
        if (!Quests.ChapterUnlocked(G.Progress, info.Battle)) return false;
        return Career.RankOf(G.Progress) >= Career.ChapterRank(info.Battle);
    }

    /// <summary>为什么这地方还去不成（给地图卡片显示）</summary>
    string RegionLockReason(int region)
    {
        if (region < 0 || region >= Regions.Length) return "";
        var info = Regions[region];
        if (info.Battle < 0) return "前往";
        int need = Career.ChapterRank(info.Battle);
        if (Career.RankOf(G.Progress) < need)
            return "需官阶 " + Career.Ranks[need].Title;
        return "需先了结上一章";
    }

    SpriteRenderer Stand(Transform parent, string path, float worldHeight)
    {
        Texture2D tex = null;
        if (!string.IsNullOrEmpty(path)) tex = Tex.Cutout(path, out _);
        if (tex != null && tex != Tex.White && tex.height > 8)
            return Tex.Actor(parent, Tex.SpriteFor(tex, worldHeight), 0);
        var sr = Tex.Actor(parent, Tex.CenterSprite(Tex.Glow, 0.55f), 0);
        sr.color = new Color(0.45f, 0.72f, 0.55f, 0.8f);
        return sr;
    }

    void AddNpc(string questId, string name, string portrait, float x, float z, Line[] lines)
    {
        portrait = G.LeaderArt(portrait);   // 主公立绘随官阶变化
        var spot = new WorldSpot
        {
            Kind = SpotKind.Npc, QuestId = questId, Label = name,
            Portrait = portrait, Lines = lines, X = x, Z = z,
        };
        spot.Go = new GameObject("npc_" + name);
        spot.Go.transform.position = new Vector3(x, 0f, z);

        spot.Body = Stand(spot.Go.transform, portrait, 2.7f);

        var marker = new GameObject("mark").transform;
        marker.SetParent(spot.Go.transform, false);
        marker.localPosition = new Vector3(0f, 2.95f, 0f);
        var mr = marker.gameObject.AddComponent<SpriteRenderer>();
        mr.sprite = Tex.CenterSprite(Tex.Glow, 0.36f);
        mr.color = new Color(1f, 0.85f, 0.4f, 0.75f);
        spot.Glow = mr;

        spots.Add(spot);
    }

    void AddCollects(string questId, string type, int need, Vector2[] pts)
    {
        int n = Mathf.Min(need + 1, pts.Length); // 多放一点方便捡
        for (int i = 0; i < n; i++)
        {
            var p = pts[i];
            var spot = new WorldSpot
            {
                Kind = SpotKind.Collect, QuestId = questId, CollectType = type,
                Label = DB.PickupNames.TryGetValue(type, out var nm) ? nm : "物资",
                X = p.x, Z = p.y,
            };
            spot.Go = new GameObject("pick_" + type + i);
            spot.Go.transform.position = new Vector3(p.x, 0f, p.y);

            Color col = DB.PickupColors.TryGetValue(type, out var c) ? c : new Color(0.45f, 0.9f, 0.4f);
            spot.Loot = LootVisual.Build(spot.Go.transform, type, col, true);
            spot.Body = spot.Loot.Face;
            spot.Glow = spot.Loot.Ring;
            spots.Add(spot);
        }
    }

    void AddGate(string questId, int mapIndex, string name, float x, float z)
    {
        var spot = new WorldSpot
        {
            Kind = SpotKind.Gate, QuestId = questId, GateMap = mapIndex,
            Label = name, X = x, Z = z,
        };
        spot.Go = new GameObject("gate_" + name);
        spot.Go.transform.position = new Vector3(x, 0f, z);

        // 旗门：竖立光柱 + 环
        var ring = new GameObject("ring").transform;
        ring.SetParent(spot.Go.transform, false);
        ring.localPosition = new Vector3(0f, 0.05f, 0f);
        ring.rotation = Quaternion.Euler(-90f, 0f, 0f);
        spot.Glow = ring.gameObject.AddComponent<SpriteRenderer>();
        spot.Glow.sprite = Tex.CenterSprite(Tex.Ring, 3.2f);
        spot.Glow.color = new Color(0.9f, 0.55f, 0.25f, 0.7f);

        var pillar = new GameObject("pillar").transform;
        pillar.SetParent(spot.Go.transform, false);
        pillar.localPosition = new Vector3(0f, 1.6f, 0f);
        spot.Body = pillar.gameObject.AddComponent<SpriteRenderer>();
        spot.Body.sprite = Tex.CenterSprite(Tex.Glow, 2.4f);
        spot.Body.color = new Color(1f, 0.75f, 0.35f, 0.55f);

        spots.Add(spot);
    }

    /// <summary>刷一个野外偶遇点。放在玩家附近但不太近 —— 得走过去才会撞见。</summary>
    void SpawnEvent()
    {
        if (evSpots.Count >= MaxEvSpots) return;
        var def = WorldEvents.Pick(evRng, G.Progress);
        if (def == null) return;

        float ang = (float)evRng.NextDouble() * Mathf.PI * 2f;
        float dist = 8f + (float)evRng.NextDouble() * 10f;
        float x = Mathf.Clamp(Pos.x + Mathf.Cos(ang) * dist, -WalkBound + 2f, WalkBound - 2f);
        float z = Mathf.Clamp(Pos.z + Mathf.Sin(ang) * dist, -WalkBound + 2f, WalkBound - 2f);

        evSpots.Add(WorldEvents.Spawn(G.transform, new Vector3(x, 0f, z), def, Time.time));
    }

    /// <summary>清理已用掉的偶遇点，并按时间补新的</summary>
    void TickEvents(float dt)
    {
        for (int i = evSpots.Count - 1; i >= 0; i--)
        {
            var e = evSpots[i];
            if (!e.Taken) continue;
            if (e.Go != null) UnityEngine.Object.Destroy(e.Go);
            evSpots.RemoveAt(i);
        }

        evTimer -= dt;
        if (evTimer > 0f) return;
        evTimer = 14f + (float)evRng.NextDouble() * 12f;
        SpawnEvent();
    }

    void TryInteractEvent(WorldEvents.EvSpot e)
    {
        interactLock = 0.35f;
        e.Taken = true;
        if (e.Go != null) e.Go.SetActive(false);

        var def = e.Def;
        G.PlayStory(def.Lines, () =>
        {
            // 记下「遇过」：普通事件不再重复，连锁事件据此解锁
            if (!string.IsNullOrEmpty(def.Id)) G.Progress.MarkEvSeen(def.Id);
            // 收益：声望 + 铜钱 + 经验
            if (def.Renown > 0) G.OnRenownGained(def.Renown);
            if (def.Gold > 0) G.Progress.Data.gold += def.Gold;
            if (def.Exp > 0) G.Progress.GainExp(def.Exp);
            // 事件留下的痕迹（结局里可能被提到）
            if (!string.IsNullOrEmpty(def.SetsFlag))
            {
                G.Progress.SetFlag(def.SetsFlag);
            }
            G.Progress.Save();

            string tip = def.Title;
            if (def.Renown > 0) tip += $"　声望 +{def.Renown}";
            if (def.Gold > 0) tip += $"　铜钱 +{def.Gold}";
            if (def.Exp > 0) tip += $"　经验 +{def.Exp}";
            G.Hud.Toast(tip);
            G.Sfx.Play("pickup", 0.9f);
            G.Phase = GamePhase.Hub;
        });
    }

    void AddFlavor(string name, string portrait, float x, float z, Line[] lines)
    {
        var spot = new WorldSpot
        {
            Kind = SpotKind.Flavor, Label = name, Portrait = portrait,
            Lines = lines, X = x, Z = z, Repeatable = true,
        };
        spot.Go = new GameObject("npc_" + name);
        spot.Go.transform.position = new Vector3(x, 0f, z);
        spot.Body = Stand(spot.Go.transform, portrait, 2.55f);
        var marker = new GameObject("mark").transform;
        marker.SetParent(spot.Go.transform, false);
        marker.localPosition = new Vector3(0f, 2.8f, 0f);
        var mr = marker.gameObject.AddComponent<SpriteRenderer>();
        mr.sprite = Tex.CenterSprite(Tex.Glow, 0.32f);
        mr.color = new Color(0.7f, 0.85f, 0.75f, 0.55f);
        spot.Glow = mr;
        spots.Add(spot);
    }

    void AddTravel(int to, string name, float x, float z, Vector3 spawn)
    {
        var spot = new WorldSpot
        {
            Kind = SpotKind.Travel, Label = name, TravelTo = to,
            TravelSpawn = spawn, X = x, Z = z,
        };
        spot.Go = new GameObject("travel_" + name);
        spot.Go.transform.position = new Vector3(x, 0f, z);

        var ring = new GameObject("ring").transform;
        ring.SetParent(spot.Go.transform, false);
        ring.localPosition = new Vector3(0f, 0.06f, 0f);
        ring.rotation = Quaternion.Euler(-90f, 0f, 0f);
        spot.Glow = ring.gameObject.AddComponent<SpriteRenderer>();
        spot.Glow.sprite = Tex.CenterSprite(Tex.Ring, 2.8f);
        spot.Glow.color = new Color(0.55f, 0.78f, 0.92f, 0.75f);

        var pillar = new GameObject("pillar").transform;
        pillar.SetParent(spot.Go.transform, false);
        pillar.localPosition = new Vector3(0f, 1.5f, 0f);
        spot.Body = pillar.gameObject.AddComponent<SpriteRenderer>();
        spot.Body.sprite = Tex.CenterSprite(Tex.Glow, 2.1f);
        spot.Body.color = new Color(0.65f, 0.86f, 1f, 0.5f);
        spots.Add(spot);
    }

    public void RefreshSpotVisibility()
    {
        collectHave = 0;
        var p = G.Progress;
        for (int i = 0; i < spots.Count; i++)
        {
            var s = spots[i];
            if (s.Taken) { if (s.Go != null) s.Go.SetActive(false); continue; }

            bool show = false;
            if (s.Kind == SpotKind.Npc)
            {
                // 对话 NPC：任务未完成且已解锁时显示
                show = Quests.Unlocked(p, Quests.Get(s.QuestId)) && !Quests.IsDone(p, s.QuestId);
            }
            else if (s.Kind == SpotKind.Collect)
            {
                var q = Quests.Get(s.QuestId);
                show = q != null && Quests.Unlocked(p, q) && !Quests.IsDone(p, s.QuestId);
            }
            else if (s.Kind == SpotKind.Gate)
            {
                var q = Quests.Get(s.QuestId);
                show = q != null && Quests.Unlocked(p, q);
            }
            else if (s.Kind == SpotKind.Flavor)
            {
                show = true;
            }
            else if (s.Kind == SpotKind.Travel)
            {
                show = true;
            }

            if (s.Go != null) s.Go.SetActive(show);
        }
    }

    public void Tick(float dt)
    {
        interactLock = Mathf.Max(0f, interactLock - dt);
        breath += dt;
        if (Arena != null) Arena.Tick();

        if (Bind.Down(SettingsUI.ActMap, KeyCode.M)) mapOpen = !mapOpen;

        bool active = !G.StoryOpen && !G.Paused && !mapOpen;
        if (active) MoveHero(dt);

        BillboardSpots();
        FindNear();
        UpdatePrompt();

        if (active)
        {
            TickEvents(dt);
            if (interactLock <= 0f && nearEv != null)
            {
                if (Bind.Down(SettingsUI.ActDash, KeyCode.Space) || Bind.Down(SettingsUI.ActInteract, KeyCode.L)
                    || Input.GetKeyDown(KeyCode.Return))
                    TryInteractEvent(nearEv);
            }
        }

        if (active && interactLock <= 0f && near != null && near.Go != null && near.Go.activeSelf)
        {
            if (Bind.Down(SettingsUI.ActDash, KeyCode.Space) || Bind.Down(SettingsUI.ActInteract, KeyCode.L)
                || Input.GetKeyDown(KeyCode.Return))
                TryInteract(near);
        }

        G.Rig.Update(dt, Pos);
        AnimateHero(dt);
    }

    void MoveHero(float dt)
    {
        float mx = 0f, mz = 0f;
        // 与战斗相同的屏幕映射：A=+x 世界, D=-x；W=-z, S=+z
        if (Bind.Held(SettingsUI.ActMoveLeft, KeyCode.A)) mx += 1f;
        if (Bind.Held(SettingsUI.ActMoveRight, KeyCode.D)) mx -= 1f;
        if (Bind.Held(SettingsUI.ActMoveUp, KeyCode.W)) mz -= 1f;
        if (Bind.Held(SettingsUI.ActMoveDown, KeyCode.S)) mz += 1f;
        if (TouchCtl.Active && TouchCtl.Joy.sqrMagnitude > 0.04f)
        {
            mx = -TouchCtl.Joy.x;
            mz = -TouchCtl.Joy.y;
        }

        float len = Mathf.Sqrt(mx * mx + mz * mz);
        bool moving = len > 0.08f;
        if (moving)
        {
            mx /= len; mz /= len;
            aim = Mathf.Atan2(mz, mx);
            Pos.x += mx * speed * dt;
            Pos.z += mz * speed * dt;
            moveTime += dt;
        }
        else moveTime = 0f;

        Pos.x = Mathf.Clamp(Pos.x, -WalkBound, WalkBound);
        Pos.z = Mathf.Clamp(Pos.z, -WalkBound, WalkBound);
        if (Arena != null) Arena.Resolve(ref Pos, 0.55f);
        HeroGo.transform.position = Pos;
    }

    void AnimateHero(float dt)
    {
        bool moving = moveTime > 0f;
        if (anim != null && anim.Any)
        {
            anim.Play(moving ? "walk" : "idle");
            anim.Update(dt);
        }

        if (heroBody != null)
        {
            // 走路帧朝左。有迈步动画时不再把整个人压扁，腿才看得清。
            bool stepping = moving && anim != null && anim.Has("walk");
            heroBody.flipX = Mathf.Cos(aim) > 0.05f;
            float wob = stepping ? 1f : 1f + Mathf.Sin(moving ? moveTime * 12f : breath * 2f) * 0.02f;
            heroBody.transform.localScale = new Vector3(wob, wob, 1f);
            heroBody.sortingOrder = Util.SortOrder(Pos.z, 20);

            if (stepping)
            {
                // 和战斗里一样：帧动画管腿，身体的重心起伏由程序化叠加，
                // 相位跟着帧动画走，脚步才不会和身体各摆各的。
                float ph = anim.Phase01;
                float bob = -Mathf.Abs(Mathf.Sin(ph * Mathf.PI * 2f)) * 0.05f;
                heroBody.transform.localPosition = new Vector3(Mathf.Sin(ph * Mathf.PI * 2f) * 0.04f, bob, 0f);
            }
            else
            {
                heroBody.transform.localPosition = Vector3.zero;
            }
        }
        if (heroShadow != null)
        {
            heroShadow.sortingOrder = Util.SortOrder(Pos.z, 0);
            // 影子随身体起落缩放，人才有"踩在地上"的分量
            float lift = heroBody != null ? Mathf.Clamp(heroBody.transform.localPosition.y / 0.09f, -1f, 1f) : 0f;
            heroShadow.transform.localScale = new Vector3(1f + lift * 0.06f, 1f + lift * 0.06f, 1f);
            heroShadow.color = new Color(0f, 0f, 0f, 0.4f - lift * 0.06f);
        }
    }

    void BillboardSpots()
    {
        var cam = G.Cam.transform.position;
        for (int i = 0; i < spots.Count; i++)
        {
            var s = spots[i];
            if (s.Go == null || !s.Go.activeSelf) continue;
            if ((s.Kind == SpotKind.Npc || s.Kind == SpotKind.Flavor) && s.Body != null)
            {
                Vector3 look = cam; look.y = s.Go.transform.position.y;
                Vector3 dir = look - s.Go.transform.position;
                if (dir.sqrMagnitude > 0.01f)
                    s.Go.transform.rotation = Quaternion.LookRotation(dir, Vector3.up);
            }
            if (s.Kind == SpotKind.Collect)
            {
                s.Phase += Time.deltaTime;
                LootVisual.Animate(s.Loot, cam, s.Phase);
            }
            else if (s.Glow != null && s.Kind != SpotKind.Gate)
            {
                s.Phase += Time.deltaTime;
                float a = 0.45f + Mathf.Sin(s.Phase * 3f) * 0.2f;
                var c = s.Glow.color; c.a = a; s.Glow.color = c;
            }
            if ((s.Kind == SpotKind.Npc || s.Kind == SpotKind.Flavor) && s.Glow != null)
                s.Glow.transform.localPosition = new Vector3(0f, 2.9f + Mathf.Sin(breath * 3f) * 0.08f, 0f);
            if ((s.Kind == SpotKind.Npc || s.Kind == SpotKind.Flavor) && s.Body != null)
                s.Body.sortingOrder = Util.SortOrder(s.Z, 12);
        }

        // 偶遇点：光柱呼吸 + 始终面向镜头
        for (int i = 0; i < evSpots.Count; i++)
        {
            var e = evSpots[i];
            if (e.Go == null || !e.Go.activeSelf) continue;
            if (e.Glow != null)
            {
                float a = 0.4f + Mathf.Sin(breath * 2.6f + e.Pos.x) * 0.18f;
                var c = e.Glow.color; c.a = a; e.Glow.color = c;
            }
            if (e.Body != null)
                e.Body.sortingOrder = Util.SortOrder(e.Pos.z, 11);
        }
    }

    void FindNear()
    {
        near = null;
        nearEv = null;
        float best = 2.4f;
        for (int i = 0; i < spots.Count; i++)
        {
            var s = spots[i];
            if (s.Taken || s.Go == null || !s.Go.activeSelf) continue;
            float d = Util.Dist2(Pos.x, Pos.z, s.X, s.Z);
            float range = (s.Kind == SpotKind.Gate || s.Kind == SpotKind.Travel) ? 2.8f
                : (s.Kind == SpotKind.Collect ? 2.6f : 2.2f);
            if (d < range && d < best) { best = d; near = s; }
        }

        // 偶遇点：谁更近就交互谁
        float bestEv = 2.2f;
        for (int i = 0; i < evSpots.Count; i++)
        {
            var e = evSpots[i];
            if (e.Taken || e.Go == null || !e.Go.activeSelf) continue;
            float d = Util.Dist2(Pos.x, Pos.z, e.Pos.x, e.Pos.z);
            if (d < bestEv && d < best) { bestEv = d; nearEv = e; }
        }
    }

    void UpdatePrompt()
    {
        if (nearEv != null)
        {
            Prompt = $"【{nearEv.Def.Title}】空格 / E 查看";
            return;
        }
        if (near == null) { Prompt = ""; return; }
        if (near.Kind == SpotKind.Npc || near.Kind == SpotKind.Flavor)
            Prompt = $"【{near.Label}】空格 / E 交谈";
        else if (near.Kind == SpotKind.Collect) Prompt = $"【{near.Label}】空格 拾取";
        else if (near.Kind == SpotKind.Gate)
        {
            bool done = Quests.IsDone(G.Progress, near.QuestId);
            Prompt = done ? $"【{near.Label}】空格 再次进入副本" : $"【{near.Label}】空格 进入战斗副本";
        }
        else if (near.Kind == SpotKind.Travel)
        {
            bool open = RegionOpen(near.TravelTo);
            Prompt = open ? $"【{near.Label}】空格 前往" : $"【{near.Label}】尚未开通";
        }
    }

    void TryInteract(WorldSpot s)
    {
        interactLock = 0.35f;
        var q = Quests.Get(s.QuestId);
        if (s.Kind == SpotKind.Npc || s.Kind == SpotKind.Flavor)
        {
            var lines = s.Lines ?? q?.Lines;
            if (lines == null || lines.Length == 0)
            {
                if (q != null) Quests.Complete(G.Progress, q);
                RefreshSpotVisibility();
                G.Hud.Toast("交谈完毕");
                return;
            }
            G.PlayStory(lines, () =>
            {
                if (s.Kind == SpotKind.Npc && q != null)
                {
                    Quests.Complete(G.Progress, q);
                    G.Hud.Toast("任务完成：" + q.Title);
                }
                RefreshSpotVisibility();
                G.Phase = GamePhase.Hub;
            });
        }
        else if (s.Kind == SpotKind.Travel)
        {
            Travel(s.TravelTo, s.TravelSpawn);
            return;
        }
        else if (s.Kind == SpotKind.Collect)
        {
            s.Taken = true;
            if (s.Go != null) s.Go.SetActive(false);
            collectHave++;
            G.Hud.Toast($"拾得{s.Label}");
            G.Sfx.Play("pickup", 0.9f);

            if (q != null)
            {
                int need = q.CollectNeed;
                int got = 0;
                for (int i = 0; i < spots.Count; i++)
                    if (spots[i].Kind == SpotKind.Collect && spots[i].QuestId == s.QuestId && spots[i].Taken)
                        got++;
                if (got >= need)
                {
                    Quests.Complete(G.Progress, q);
                    // 藏起同任务剩余采集物
                    for (int i = 0; i < spots.Count; i++)
                        if (spots[i].Kind == SpotKind.Collect && spots[i].QuestId == s.QuestId)
                        {
                            spots[i].Taken = true;
                            if (spots[i].Go != null) spots[i].Go.SetActive(false);
                        }
                    RefreshSpotVisibility();
                    G.Hud.Toast("收集完成：" + q.Title + " · 可去副本入口");
                }
            }
        }
        else if (s.Kind == SpotKind.Gate)
        {
            if (q == null || !Quests.Unlocked(G.Progress, q))
            {
                G.Hud.Toast("尚未解锁");
                return;
            }
            G.EnterBattleFromWorld(s.GateMap, q);
        }
    }

    public void DrawOverlay(float W, float CX, System.Action<Rect, string, int, Color, TextAnchor, bool> label, System.Action<Rect, Color> fill)
    {
        // 顶栏
        fill(new Rect(0, 0, W, 56), new Color(0.04f, 0.035f, 0.03f, 0.72f));
        label(new Rect(24, 8, 560, 24), Regions[RegionIndex].Name + " · 探索", 20, new Color(0.96f, 0.9f, 0.78f), TextAnchor.MiddleLeft, false);
        var d = G.Progress.Data;
        var fac = G.Progress.Faction;
        var rank = Career.Current(G.Progress);
        label(new Rect(24, 32, 760, 18),
            $"{Regions[RegionIndex].Poem}　{(fac != null ? fac.Name : "")}　Lv.{d.level}　铜钱 {d.gold}　官阶 {rank.Title}　声望 {d.renown}",
            13, new Color(0.8f, 0.76f, 0.68f), TextAnchor.MiddleLeft, false);

        DrawQuestTracker(W, label, fill);

        // 当前目标
        string objective = CurrentObjective();
        if (!string.IsNullOrEmpty(objective) && !mapOpen)
        {
            fill(new Rect(CX - 280, 70, 560, 36), new Color(0.05f, 0.04f, 0.03f, 0.7f));
            label(new Rect(CX - 280, 70, 560, 36), objective, 16, new Color(1f, 0.9f, 0.55f), TextAnchor.MiddleCenter, false);
        }

        if (!mapOpen && !G.StoryOpen) DrawLootMarkers(W, label, fill);

        // 交互提示
        if (!string.IsNullOrEmpty(Prompt) && !G.StoryOpen && !mapOpen)
        {
            fill(new Rect(CX - 200, 520, 400, 40), new Color(0.05f, 0.04f, 0.03f, 0.78f));
            label(new Rect(CX - 200, 520, 400, 40), Prompt, 18, new Color(0.98f, 0.9f, 0.65f), TextAnchor.MiddleCenter, false);
        }

        label(new Rect(24, 680, 560, 22), "WASD 走动　空格/E 交互　M 天下　C 人物　Esc 暂停", 13,
            new Color(0.7f, 0.66f, 0.58f), TextAnchor.MiddleLeft, false);

        Rect charBtn = new Rect(W - 170, 660, 150, 36);
        fill(charBtn, new Color(0.18f, 0.15f, 0.11f, 0.92f));
        label(charBtn, "人物 · 天赋", 16, new Color(0.98f, 0.88f, 0.62f), TextAnchor.MiddleCenter, false);
        Rect mapBtn = new Rect(W - 170, 616, 150, 36);
        fill(mapBtn, new Color(0.16f, 0.18f, 0.2f, 0.92f));
        label(mapBtn, mapOpen ? "关闭天下" : "天下 · 地图", 16, new Color(0.75f, 0.88f, 0.95f), TextAnchor.MiddleCenter, false);

        if (mapOpen) DrawRegionMap(W, CX, label, fill);
    }

    void DrawQuestTracker(float W, System.Action<Rect, string, int, Color, TextAnchor, bool> label, System.Action<Rect, Color> fill)
    {
        var p = G.Progress;
        float y = 72f;
        int shown = 0;
        fill(new Rect(W - 268, 64, 250, 148), new Color(0.04f, 0.035f, 0.03f, 0.62f));
        label(new Rect(W - 260, 68, 234, 20), "行程", 14, new Color(0.95f, 0.86f, 0.62f), TextAnchor.MiddleLeft, false);
        for (int i = 0; i < Quests.All.Count && shown < 4; i++)
        {
            var q = Quests.All[i];
            if (!Quests.Unlocked(p, q) || q.Chapter != RegionIndex) continue;
            bool done = Quests.IsDone(p, q.Id);
            string tag = q.Kind == QuestKind.Talk ? "谈" : (q.Kind == QuestKind.Collect ? "拾" : "战");
            string line = (done ? "✓ " : "· ") + "【" + tag + "】" + q.Title;
            label(new Rect(W - 260, y + 22 + shown * 22, 234, 20), line, 12,
                done ? new Color(0.62f, 0.58f, 0.5f) : new Color(0.95f, 0.9f, 0.78f), TextAnchor.MiddleLeft, false);
            shown++;
        }
        if (shown == 0)
            label(new Rect(W - 260, y + 28, 234, 20), "此地已无未了之事", 12, new Color(0.7f, 0.66f, 0.58f), TextAnchor.MiddleLeft, false);
    }

    Rect MapCard(int slot, float cx)
    {
        int col = slot % 5;
        int row = slot / 5;
        float x = cx - 374f + col * 152f;
        float y = 244f + row * 176f;
        return new Rect(x, y, 140f, 156f);
    }

    void DrawRegionMap(float W, float CX, System.Action<Rect, string, int, Color, TextAnchor, bool> label, System.Action<Rect, Color> fill)
    {
        fill(new Rect(0, 0, W, 720), new Color(0.03f, 0.03f, 0.025f, 0.55f));
        fill(new Rect(CX - 400, 96, 800, 520), new Color(0.06f, 0.055f, 0.045f, 0.94f));
        fill(new Rect(CX - 400, 96, 800, 4), new Color(0.78f, 0.62f, 0.32f, 0.9f));
        label(new Rect(CX - 360, 108, 720, 36), "天 下", 28, new Color(0.96f, 0.88f, 0.7f), TextAnchor.MiddleCenter, false);
        label(new Rect(CX - 360, 146, 720, 22), "八方已通。点选州郡，或走到路边蓝光处。", 14, new Color(0.74f, 0.7f, 0.62f), TextAnchor.MiddleCenter, false);

        for (int slot = 0; slot < MapOrder.Length; slot++)
        {
            int i = MapOrder[slot];
            var info = Regions[i];
            bool here = i == RegionIndex;
            bool open = RegionOpen(i);
            Rect r = MapCard(slot, CX);
            fill(r, here ? new Color(0.18f, 0.15f, 0.1f, 0.96f)
                         : (open ? new Color(0.1f, 0.09f, 0.08f, 0.92f) : new Color(0.06f, 0.055f, 0.05f, 0.92f)));
            fill(new Rect(r.x, r.y, r.width, 4), here ? new Color(1f, 0.86f, 0.45f)
                                                      : (open ? new Color(0.7f, 0.62f, 0.4f) : new Color(0.3f, 0.28f, 0.25f)));
            label(new Rect(r.x, r.y + 12, r.width, 22), info.Chapter, 13,
                open ? new Color(0.9f, 0.84f, 0.7f) : new Color(0.5f, 0.48f, 0.44f), TextAnchor.MiddleCenter, false);
            label(new Rect(r.x, r.y + 38, r.width, 36), info.Name, 16,
                open ? new Color(0.98f, 0.92f, 0.78f) : new Color(0.5f, 0.48f, 0.44f), TextAnchor.MiddleCenter, true);
            label(new Rect(r.x + 6, r.y + 76, r.width - 12, 36), info.Poem, 12,
                open ? new Color(0.7f, 0.66f, 0.58f) : new Color(0.42f, 0.4f, 0.37f), TextAnchor.UpperCenter, true);
            string tip = here ? "此地" : (open ? "前往" : RegionLockReason(i));
            label(new Rect(r.x, r.y + 124, r.width, 26), tip, 15,
                here ? new Color(1f, 0.9f, 0.55f) : (open ? new Color(0.75f, 0.88f, 0.95f) : new Color(0.72f, 0.5f, 0.42f)),
                TextAnchor.MiddleCenter, false);
        }
        label(new Rect(CX - 360, 588, 720, 22), "点击州郡前往　·　M 关闭", 13, new Color(0.8f, 0.76f, 0.66f), TextAnchor.MiddleCenter, false);
    }

    void DrawLootMarkers(float W, System.Action<Rect, string, int, Color, TextAnchor, bool> label, System.Action<Rect, Color> fill)
    {
        if (G.Cam == null) return;
        float s = Mathf.Max(0.01f, Screen.height / 720f);
        for (int i = 0; i < spots.Count; i++)
        {
            var spot = spots[i];
            if (spot.Kind != SpotKind.Collect || spot.Taken || spot.Go == null || !spot.Go.activeSelf) continue;
            Vector3 sp = G.Cam.WorldToScreenPoint(new Vector3(spot.X, 4.6f, spot.Z));
            if (sp.z < 0.2f) continue;
            float x = sp.x / s;
            float y = (Screen.height - sp.y) / s;
            if (x < -20f || x > W + 20f || y < 48f || y > 690f) continue;
            Color col = DB.PickupColors.TryGetValue(spot.CollectType, out var c) ? c : new Color(0.5f, 0.95f, 0.45f);
            col = Color.Lerp(col, Color.white, 0.35f);
            fill(new Rect(x - 8f, y - 8f, 16f, 16f), col);
            fill(new Rect(x - 3f, y - 18f, 6f, 10f), new Color(col.r, col.g, col.b, 0.85f));
            label(new Rect(x - 48f, y - 36f, 96f, 18f), spot.Label, 14, new Color(1f, 0.97f, 0.86f), TextAnchor.MiddleCenter, false);
        }
    }

    string NearestLootHint(string questId)
    {
        float best = 999f;
        float dx = 0f, dz = 0f;
        bool found = false;
        for (int i = 0; i < spots.Count; i++)
        {
            var s = spots[i];
            if (s.Kind != SpotKind.Collect || s.Taken || s.QuestId != questId) continue;
            if (s.Go != null && !s.Go.activeSelf) continue;
            float x = s.X - Pos.x, z = s.Z - Pos.z;
            float d = Mathf.Sqrt(x * x + z * z);
            if (d < best) { best = d; dx = x; dz = z; found = true; }
        }
        if (!found) return "";
        if (best < 2.8f) return " · 就在身边";
        string ns = dz > 1.4f ? "北" : (dz < -1.4f ? "南" : "");
        string ew = dx > 1.4f ? "东" : (dx < -1.4f ? "西" : "");
        return " · 往" + ns + ew + " " + Mathf.CeilToInt(best) + " 步";
    }

    string CurrentObjective()
    {
        var p = G.Progress;
        for (int i = 0; i < Quests.All.Count; i++)
        {
            var q = Quests.All[i];
            if (Quests.IsDone(p, q.Id)) continue;
            if (!Quests.Unlocked(p, q)) continue;
            if (q.Id.Contains("_side")) continue;
            if (q.Chapter != RegionIndex) continue;
            if (q.Kind == QuestKind.Talk) return "目标：" + q.Title + " · 找发光的人交谈";
            if (q.Kind == QuestKind.Collect) return "目标：" + q.Title + " · 跟着光柱走" + NearestLootHint(q.Id);
            if (q.Kind == QuestKind.Battle) return "目标：走近【" + DB.Maps[q.MapIndex].Name + "】金光入口";
        }
        for (int i = 0; i < Quests.All.Count; i++)
        {
            var q = Quests.All[i];
            if (Quests.IsDone(p, q.Id) || !Quests.Unlocked(p, q)) continue;
            if (q.Chapter != RegionIndex) return "目标：打开天下地图，前往下一地";
            return "目标：" + q.Title;
        }
        return "各地风物已遍 · 可再入副本历练";
    }

    /// <summary>0 无，1 开人物，2 已消费（地图）</summary>
    public int ClickUi(Vector2 mp, float W)
    {
        if (new Rect(W - 170, 660, 150, 36).Contains(mp)) return 1;
        if (new Rect(W - 170, 616, 150, 36).Contains(mp)) { mapOpen = !mapOpen; return 2; }
        if (mapOpen)
        {
            float CX = W * 0.5f;
            for (int slot = 0; slot < MapOrder.Length; slot++)
            {
                int i = MapOrder[slot];
                if (!MapCard(slot, CX).Contains(mp)) continue;
                if (i == RegionIndex) { mapOpen = false; return 2; }
                Travel(i, RegionSpawn[i]);
                mapOpen = false;
                return 2;
            }
            return 2;
        }
        return 0;
    }

    public void Dispose()
    {
        if (G != null && G.Rig != null)
        {
            G.Rig.Height = savedH > 0.1f ? savedH : 9.4f;
            G.Rig.Back = savedB > 0.1f ? savedB : 11.2f;
            G.Rig.Lead = savedL > 0.1f ? savedL : 2f;
        }
        if (HeroGo != null) Object.Destroy(HeroGo);
        for (int i = 0; i < spots.Count; i++)
            if (spots[i].Go != null) Object.Destroy(spots[i].Go);
        spots.Clear();
        if (Arena != null) Arena.Dispose();
        Arena = null;
    }

    /// <summary>设置里改了昼夜 / 时段后，让当前场景立刻跟着变</summary>
    public void RefreshAmbience()
    {
        var s = Settings.Current;
        if (s == null) return;
        if (Arena != null) Arena.SetAmbience(s.DayCycle, s.DayPhase);
    }
}
