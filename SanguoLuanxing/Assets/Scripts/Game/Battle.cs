using System.Collections.Generic;
using UnityEngine;
// Queue<> lives in System.Collections.Generic

public struct HitOpts
{
    public float Knock;
    public bool Skill, Heavy, Crit;
    public float Aim;
    public Vector3? From;
    public float Up;
}

/// <summary>一场战斗：敌群、Boss、拾取、箭矢、命中判定、顿帧</summary>
public class Battle
{
    public Game G;
    public MapDef Map;
    public Arena Arena;
    public Fx Fx;
    public Sfx Sfx;
    public Hud Hud;
    public CameraRig Rig;
    public Progress Progress;

    public PlayerCtrl Player;
    public EnemyCtrl Boss;
    public readonly List<EnemyCtrl> Enemies = new List<EnemyCtrl>();
    public readonly List<Arrow> Arrows = new List<Arrow>();
    public readonly List<Pickup> Pickups = new List<Pickup>();

    public float HitStop;
    public bool Finished;
    public bool PlayerDead;
    public QuestDef ActiveQuest;
    public bool IsCollectQuest => ActiveQuest != null && ActiveQuest.Kind == QuestKind.Collect;

    public int RewardExp => expGain;
    public int RewardGold => goldGain;
    public int QuestCollectCount => questCollectCount;

    float time, winTimer = -1f;
    bool bossTriggered;
    bool packsClearedToast;
    int expGain, goldGain;
    int renownGain;
    int questCollectCount;
    public readonly List<ItemDef> ItemRewards = new List<ItemDef>();
    readonly List<Pack> packs = new List<Pack>();
    readonly Queue<SpawnReq> spawnQ = new Queue<SpawnReq>();

    class Pack
    {
        public PackDef Def;
        public bool Triggered;
    }

    struct SpawnReq
    {
        public string Id;
        public float X, Z;
    }

    public class Arrow
    {
        public GameObject Go;
        public SpriteRenderer Sr;
        public float X, Z, DX, DZ, Speed, Dmg, Life;
    }

    public class Pickup
    {
        public PickupDef Def;
        public GameObject Go;
        public SpriteRenderer Core, Glow;
        public LootRig Loot;
        public bool Taken;
        public float Phase;
    }

    public Battle(Game g, MapDef map, QuestDef quest = null)
    {
        G = g;
        Map = map;
        ActiveQuest = quest;
        Arena = global::Arena.Build(map, g.transform, g.Cam);
        Fx = new Fx(g.transform);
        Sfx = g.Sfx;
        Hud = g.Hud;
        Rig = g.Rig;
        Progress = g.Progress;

        if (map.Packs != null)
            foreach (var p in map.Packs) packs.Add(new Pack { Def = p });

        Player = new PlayerCtrl(this) { Pos = new Vector3(0f, 0f, 15f) };
        Player.Go.transform.position = Player.Pos;
        Rig.SnapTo(Player.Pos);

        SpawnPickups();
        if (!IsCollectQuest && !string.IsNullOrEmpty(map.Boss) && DB.Bosses.ContainsKey(map.Boss))
            SpawnBoss();
        PrewarmSprites();
        Hud.SetBoss(null);
        if (IsCollectQuest)
            Hud.Toast($"收集 {ActiveQuest.CollectNeed} 份物资（WASD 移动靠近拾取）");
        else
            Hud.Toast("先清扫附近敌军，再寻渠帅");
    }

    /// <summary>开战前把本关会用到的抠像预热掉，避免第一波生成时卡顿</summary>
    void PrewarmSprites()
    {
        float asp;
        Tex.Cutout("char/player.jpg", out asp);
        if (Map.Mobs != null)
            foreach (var id in Map.Mobs)
                if (DB.Enemies.TryGetValue(id, out var d))
                    Tex.Cutout("char/" + d.Sprite + ".jpg", out asp);
        if (!string.IsNullOrEmpty(Map.Boss) && DB.Bosses.TryGetValue(Map.Boss, out var boss))
            Tex.Cutout("char/" + boss.Sprite + ".jpg", out asp);
    }

    // ---------------- 主循环 ----------------

    public void Tick(float rawDt)
    {
        // 收集任务完成短延迟：仍要走 CheckVictory 回地图
        if (Finished)
        {
            if (IsCollectQuest && winTimer >= 0f)
            {
                winTimer -= rawDt;
                if (winTimer <= 0f)
                {
                    winTimer = -1f;
                    G.OnQuestCollectDone(ActiveQuest);
                }
            }
            return;
        }

        float dt = rawDt;
        float fxDt = rawDt;
        if (HitStop > 0f)
        {
            HitStop -= rawDt;
            dt = 0f;                 // 顿帧：世界冻结；特效仍按真实时间播，才看得见爆点
        }

        time += dt;

        if (Player.Dead)
        {
            Player.Update(dt, default, false);
            Fx.Update(fxDt);
            Rig.Update(rawDt, Player.Pos);
            Hud.Sync(this);
            if (!PlayerDead)
            {
                PlayerDead = true;
                G.OnPlayerDefeat();
            }
            return;
        }

        bool active = !G.StoryOpen && !G.Paused;
        if (active)
        {
            var inp = ReadInput();
            // 移动时优先朝移动方向；静止再跟鼠标 / 摇杆，转向更跟手
            if (Mathf.Abs(inp.MX) + Mathf.Abs(inp.MZ) > 0.08f)
                Player.AimToward(inp.MX, inp.MZ, dt);
            else if (TouchCtl.Active && TouchCtl.Joy.sqrMagnitude > 0.04f)
                Player.AimToward(TouchCtl.Joy.x, TouchCtl.Joy.y, dt);
            else
                Player.AimAtMouse(MouseGround(), dt);

            Player.Update(dt, inp, true);
            DrainSpawnQueue();

            for (int i = 0; i < Enemies.Count; i++)
            {
                var e = Enemies[i];
                if (e.Dormant) continue;
                e.Update(dt, Player);
            }

            UpdateArrows(dt);
            Separate();
            UpdatePickups(dt, true);
            UpdatePacks();
            CheckBossTrigger();
        }
        else
        {
            Player.Update(dt, default, false);
        }

        for (int i = Enemies.Count - 1; i >= 0; i--)
        {
            if (Enemies[i].Destroyed) Enemies.RemoveAt(i);
        }

        Rig.Update(rawDt, Player.Pos);
        if (Arena != null) Arena.Tick();
        Fx.Update(fxDt);
        Hud.Sync(this);
        CheckVictory(dt);
    }

    FrameInput ReadInput()
    {
        // 触屏模式下忽略鼠标键：触摸会产生模拟鼠标事件，摇杆滑动会误触发轻击
        bool mouse = !TouchCtl.Active;
        var inp = new FrameInput
        {
            // 相机在 +z 看向 -z：屏幕右 = 世界 -x，屏幕上 = 世界 -z。
            // 所以按 D（屏幕右）必须是世界 -x，否则角色会往屏幕左边跑。
            MX = (Bind.Held(SettingsUI.ActMoveLeft, KeyCode.A) ? 1f : 0f)
               - (Bind.Held(SettingsUI.ActMoveRight, KeyCode.D) ? 1f : 0f),
            MZ = (Bind.Held(SettingsUI.ActMoveDown, KeyCode.S) ? 1f : 0f)
               - (Bind.Held(SettingsUI.ActMoveUp, KeyCode.W) ? 1f : 0f),
            Light = (mouse && Input.GetMouseButtonDown(0)) || Bind.Down(SettingsUI.ActAttack, KeyCode.J),
            Heavy = (mouse && Input.GetMouseButtonDown(1)) || Bind.Down(SettingsUI.ActHeavy, KeyCode.K),
            Dodge = Bind.Down(SettingsUI.ActDash, KeyCode.Space) || Input.GetKeyDown(KeyCode.LeftShift),
            Q = Bind.Down(SettingsUI.ActSkill1, KeyCode.Q),
            E = Bind.Down(SettingsUI.ActSkill2, KeyCode.E),
            R = Bind.Down(SettingsUI.ActSkill3, KeyCode.R),
        };
        if (TouchCtl.Active)
        {
            inp.MX += TouchCtl.Joy.x;
            inp.MZ += TouchCtl.Joy.y;
            inp.Light |= TouchCtl.Light;
            inp.Heavy |= TouchCtl.Heavy;
            inp.Dodge |= TouchCtl.Dodge;
            inp.Q |= TouchCtl.Q;
            inp.E |= TouchCtl.E;
            inp.R |= TouchCtl.R;
        }
        return inp;
    }

    Vector3 MouseGround()
    {
        Ray ray = G.Cam.ScreenPointToRay(Input.mousePosition);
        float t = -ray.origin.y / ray.direction.y;
        if (t <= 0f || Mathf.Abs(ray.direction.y) < 0.0001f) return new Vector3(Player.Pos.x, 0f, Player.Pos.z + 1f);
        return ray.origin + ray.direction * t;
    }

    // ---------------- 判定 ----------------

    public int HitArc(Vector3 origin, float range, float arc, float mul, HitOpts opts)
    {
        float aim = opts.Aim;
        int hits = 0;
        for (int i = 0; i < Enemies.Count; i++)
        {
            var e = Enemies[i];
            if (e.Dead || e.Dormant) continue;
            float d = Util.Dist2(origin.x, origin.z, e.Pos.x, e.Pos.z);
            if (d > range + e.Radius) continue;
            float a = Mathf.Atan2(e.Pos.z - origin.z, e.Pos.x - origin.x);
            if (Util.AngleDiff(a, aim) > arc) continue;

            bool crit = Util.Rand(0f, 1f) < Player.Stats.Crit;
            e.TakeDamage(Player.Atk * mul * (crit ? 1.8f : 1f), new HitOpts
            {
                Crit = crit,
                Heavy = opts.Heavy,
                Skill = opts.Skill,
                Knock = opts.Knock,
                From = new Vector3(origin.x, 0f, origin.z),
            });

            var hitCol = crit ? new Color(1f, 0.82f, 0.32f)
                      : (opts.Heavy ? new Color(1f, 0.68f, 0.38f) : new Color(1f, 0.95f, 0.82f));
            float hitSize = opts.Heavy ? 3.1f : (opts.Skill ? 2.7f : (crit ? 2.3f : 1.9f));
            Fx.Impact(new Vector3(e.Pos.x, 0.85f, e.Pos.z), hitCol, hitSize);
            if (crit)
            {
                Fx.Slash(new Vector3(e.Pos.x, 0f, e.Pos.z), aim + 0.4f, new Color(1f, 0.9f, 0.45f), 1.6f, 0.18f);
                Hud.Flash(new Color(1f, 0.88f, 0.55f), 0.22f);
            }
            hits++;
        }

        if (hits > 0)
        {
            HitStop = opts.Heavy ? 0.11f : (opts.Skill ? 0.10f : (hits > 1 ? 0.07f : 0.045f));
            Rig.AddTrauma(opts.Heavy ? 0.70f : (opts.Skill ? 0.55f : 0.36f));
            Rig.Punch(opts.Heavy ? 2.4f : (opts.Skill ? 1.9f : 1.15f));
            Rig.Kick(new Vector3(-Mathf.Cos(aim), 0f, -Mathf.Sin(aim)), opts.Heavy ? 0.62f : 0.32f);
            if (opts.Skill) Sfx.Play("skill", 0.9f);
        }
        return hits;
    }

    public bool IsPlayerInArc(float ox, float oz, float range, float arc, float aim)
    {
        float d = Util.Dist2(ox, oz, Player.Pos.x, Player.Pos.z);
        if (d > range + Player.Radius) return false;
        float a = Mathf.Atan2(Player.Pos.z - oz, Player.Pos.x - ox);
        return Util.AngleDiff(a, aim) <= arc;
    }

    public void DamagePlayer(float amount, float x, float z) => Player.ReceiveDamage(amount, x, z);

    // ---------------- 生成 ----------------

    void SpawnBoss()
    {
        var def = DB.Bosses[Map.Boss];
        Boss = new EnemyCtrl(this, def, 0f, -15f, true) { Dormant = true };
        Enemies.Add(Boss);
    }

    public void SpawnWave(string[] types, int count, float cx, float cz)
    {
        // 入队分帧生成，避免同帧 new 多个 EnemyCtrl + Cutout 造成顿卡
        for (int i = 0; i < count; i++)
        {
            var id = types[i % types.Length];
            if (!DB.Enemies.ContainsKey(id)) continue;
            float a = Util.Rand(0f, Mathf.PI * 2f);
            float r = Util.Rand(1.2f, 3.2f);
            spawnQ.Enqueue(new SpawnReq { Id = id, X = cx + Mathf.Cos(a) * r, Z = cz + Mathf.Sin(a) * r });
        }
    }

    void DrainSpawnQueue()
    {
        int budget = 1; // 每帧最多 1 个，把卡顿摊开
        while (budget-- > 0 && spawnQ.Count > 0)
        {
            var req = spawnQ.Dequeue();
            if (!DB.Enemies.TryGetValue(req.Id, out var def)) continue;
            Enemies.Add(new EnemyCtrl(this, def, req.X, req.Z));
        }
    }

    public void SpawnArrow(Vector3 pos, float angle, float dmg, float speed)
    {
        var go = new GameObject("arrow");
        go.transform.position = new Vector3(pos.x, 1.15f, pos.z);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = Tex.CenterSprite(Tex.Dot, 0.5f);
        sr.color = new Color(0.85f, 0.91f, 1f);
        sr.sortingOrder = Util.SortOrder(pos.z, 60);

        Arrows.Add(new Arrow
        {
            Go = go, Sr = sr,
            X = pos.x, Z = pos.z,
            DX = Mathf.Cos(angle), DZ = Mathf.Sin(angle),
            Speed = speed, Dmg = dmg, Life = 2.4f,
        });
    }

    void UpdateArrows(float dt)
    {
        for (int i = Arrows.Count - 1; i >= 0; i--)
        {
            var a = Arrows[i];
            a.Life -= dt;
            if (a.Life <= 0f) { KillArrow(a); Arrows.RemoveAt(i); continue; }

            a.X += a.DX * a.Speed * dt;
            a.Z += a.DZ * a.Speed * dt;
            a.Go.transform.position = new Vector3(a.X, 1.15f, a.Z);
            a.Sr.sortingOrder = Util.SortOrder(a.Z, 60);

            if (Mathf.Abs(a.X) > global::Arena.Bound + 2f || Mathf.Abs(a.Z) > global::Arena.Bound + 2f)
            {
                KillArrow(a); Arrows.RemoveAt(i); continue;
            }
            if (Util.Dist2(a.X, a.Z, Player.Pos.x, Player.Pos.z) < 0.85f)
            {
                DamagePlayer(a.Dmg, a.X, a.Z);
                Fx.Burst(new Vector3(a.X, 0f, a.Z), new Color(0.62f, 0.78f, 0.87f), 8, 4f);
                KillArrow(a);
                Arrows.RemoveAt(i);
            }
        }
    }

    static void KillArrow(Arrow a) { if (a.Go != null) Object.Destroy(a.Go); }

    // ---------------- 拾取 ----------------

    void SpawnPickups()
    {
        foreach (var p in Map.Pickups)
        {
            if (!DB.PickupColors.TryGetValue(p.Type, out var col)) col = Color.white;
            var go = new GameObject("pickup_" + p.Type);
            go.transform.position = new Vector3(p.X, 0f, p.Z);

            var rig = LootVisual.Build(go.transform, p.Type, col, false);
            if (rig.Face != null) rig.Face.sortingOrder = Util.SortOrder(p.Z, 30);
            if (rig.Ring != null) rig.Ring.sortingOrder = Util.SortOrder(p.Z, -6);

            Pickups.Add(new Pickup
            {
                Def = p, Go = go, Core = rig.Face, Glow = rig.Ring, Loot = rig,
                Phase = Util.Rand(0f, 6f),
            });
        }
    }

    void UpdatePickups(float dt, bool active)
    {
        for (int i = 0; i < Pickups.Count; i++)
        {
            var p = Pickups[i];
            if (p.Taken) continue;
            p.Phase += dt;
            Vector3 cam = G.Cam != null ? G.Cam.transform.position : p.Go.transform.position + Vector3.back;
            LootVisual.Animate(p.Loot, cam, p.Phase);

            if (!active) continue;
            if (Util.Dist2(p.Def.X, p.Def.Z, Player.Pos.x, Player.Pos.z) < 1.8f)
            {
                p.Taken = true;
                Object.Destroy(p.Go);
                Collect(p);
            }
        }
    }

    void Collect(Pickup p)
    {
        if (IsCollectQuest && p.Def.Type == ActiveQuest.CollectType)
        {
            questCollectCount++;
            string name = DB.PickupNames.TryGetValue(p.Def.Type, out var n) ? n : "物资";
            Hud.Toast($"{name} {questCollectCount}/{ActiveQuest.CollectNeed}");
            Sfx.Play("pickup", 0.9f);
            if (DB.PickupColors.TryGetValue(p.Def.Type, out var c0))
                Fx.Burst(new Vector3(p.Def.X, 0f, p.Def.Z), c0, 12, 4f);
            if (questCollectCount >= ActiveQuest.CollectNeed && !Finished)
            {
                Finished = true;
                winTimer = 0.8f;
                Quests.Complete(Progress, ActiveQuest);
                Hud.Toast("任务完成 · 即将返回地图");
            }
            return;
        }

        switch (p.Def.Type)
        {
            case "herb":
                Player.Heal(30f);
                Hud.Toast("拾得灵草，气血 +30");
                break;
            case "ore":
                Progress.Data.bonusHp += 4;
                Player.MaxHP = Progress.Stats().MaxHP;
                Hud.Toast("拾得玄铁，体魄 +4");
                break;
            case "scroll":
                Progress.Data.sp += 1;
                Hud.Toast("拾得古卷，技能点 +1");
                break;
            case "coin":
                Progress.Data.gold += 25;
                Hud.Toast("拾得铜钱 +25");
                break;
        }
        Sfx.Play("pickup", 0.9f);
        if (DB.PickupColors.TryGetValue(p.Def.Type, out var col))
            Fx.Burst(new Vector3(p.Def.X, 0f, p.Def.Z), col, 12, 4f);
        Progress.Save();
    }

    void UpdatePacks()
    {
        for (int i = 0; i < packs.Count; i++)
        {
            var pack = packs[i];
            if (pack.Triggered) continue;
            if (Util.Dist2(pack.Def.X, pack.Def.Z, Player.Pos.x, Player.Pos.z) < pack.Def.R)
            {
                pack.Triggered = true;
                Hud.Toast("遭遇敌军");
                Sfx.Play("boss", 0.8f);
                SpawnWave(pack.Def.Types, pack.Def.Types.Length, pack.Def.X, pack.Def.Z);
            }
        }
    }

    bool AllPacksCleared()
    {
        for (int i = 0; i < packs.Count; i++)
            if (!packs[i].Triggered) return false;
        if (spawnQ.Count > 0) return false;
        for (int i = 0; i < Enemies.Count; i++)
        {
            var e = Enemies[i];
            if (e.IsBoss) continue;
            if (!e.Dead) return false;
        }
        return true;
    }

    void CheckBossTrigger()
    {
        if (bossTriggered || Boss == null || Boss.Dead) return;

        // 简易任务：先清完巡逻敌军，才允许触发 Boss
        if (!AllPacksCleared())
        {
            if (Util.Dist2(Boss.Pos.x, Boss.Pos.z, Player.Pos.x, Player.Pos.z) < 10f)
                Hud.Toast("先扫清附近敌军");
            return;
        }

        if (!packsClearedToast)
        {
            packsClearedToast = true;
            Hud.Toast("敌军溃散——渠帅现身");
        }

        if (Util.Dist2(Boss.Pos.x, Boss.Pos.z, Player.Pos.x, Player.Pos.z) < 14f)
        {
            bossTriggered = true;
            G.PlayStory(Map.BossIntro, () =>
            {
                if (Boss == null) return;
                Boss.Dormant = false;
                Boss.State = "chase";
                Hud.SetBoss(Boss);
                Sfx.Play("boss", 1f);
                Rig.AddTrauma(0.5f);
            });
        }
    }

    void Separate()
    {
        for (int i = 0; i < Enemies.Count; i++)
        {
            var a = Enemies[i];
            if (a.Dead || a.Dormant) continue;

            for (int j = i + 1; j < Enemies.Count; j++)
            {
                var b = Enemies[j];
                if (b.Dead || b.Dormant) continue;
                float dx = b.Pos.x - a.Pos.x, dz = b.Pos.z - a.Pos.z;
                float d = Mathf.Sqrt(dx * dx + dz * dz);
                float min = a.Radius + b.Radius;
                if (d > 0.001f && d < min)
                {
                    float push = (min - d) / d * 0.5f;
                    a.Pos.x -= dx * push; a.Pos.z -= dz * push;
                    b.Pos.x += dx * push; b.Pos.z += dz * push;
                }
            }

            float px = a.Pos.x - Player.Pos.x, pz = a.Pos.z - Player.Pos.z;
            float pd = Mathf.Sqrt(px * px + pz * pz);
            float pmin = a.Radius + Player.Radius;
            if (pd > 0.001f && pd < pmin)
            {
                // 玩家只占 30%，敌人占 70%：撞进人堆不会被顶得原地抖动
                bool free = Player.FreeMove;
                float push = (pmin - pd) / pd * (free ? 1f : 0.3f);
                if (!free)
                {
                    Player.Pos.x -= px * push;
                    Player.Pos.z -= pz * push;
                }
                float rest = free ? push : push / 0.3f * 0.7f;
                a.Pos.x += px * rest;
                a.Pos.z += pz * rest;
            }
        }
    }

    // ---------------- 结算 ----------------

    public void OnEnemyDeath(EnemyCtrl e)
    {
        expGain += Mathf.RoundToInt(e.Def.Exp);
        goldGain += Mathf.RoundToInt(e.Def.Gold);
        Progress.Data.totalKills++;
        // 斩敌攒声望：Boss 折算更多，小兵一点点。战功是仕途的主要来源。
        renownGain += e.IsBoss ? 45 : 4;
        Sfx.Play("hit", 0.8f);
        Fx.Burst(e.Pos, new Color(1f, 0.77f, 0.42f), 22, 7f);
        Fx.Sparks(e.Pos, new Color(1f, 0.7f, 0.35f), 12, 8f);
        Fx.Ring(e.Pos, 2.1f, new Color(0.84f, 0.29f, 0.24f), 0.55f, 0.4f);

        if (e.IsBoss)
        {
            goldGain += 60;
            int lv = Progress.Data.level;
            for (int i = 0; i < 2; i++) ItemRewards.Add(Items.Make(lv + 1, null, Items.RandomRarity(1f)));
            Hud.SetBoss(null);
            Rig.AddTrauma(0.9f);
        }
        else if (Util.Rand(0f, 1f) < 0.22f)
        {
            ItemRewards.Add(Items.Make(Progress.Data.level, null, Items.RandomRarity(0f)));
        }
    }

    public void OnPlayerDeath()
    {
        Sfx.Play("defeat", 1f);
    }

    void CheckVictory(float dt)
    {
        if (IsCollectQuest) return; // 收集完成在 Tick 开头延迟处理
        if (Finished || Boss == null || !Boss.Dead) return;
        for (int i = 0; i < Enemies.Count; i++)
            if (!Enemies[i].Dead) return;

        if (winTimer < 0f)
        {
            winTimer = 1.5f;
            Hud.Toast("敌军已破");
            return;
        }
        winTimer -= dt;
        if (winTimer <= 0f)
        {
            Finished = true;
            Sfx.Play("victory", 1f);
            G.OnBattleVictory(this);
        }
    }

    public void ClaimRewards()
    {
        Progress.Data.gold += goldGain;
        Progress.GainExp(expGain);
        foreach (var it in ItemRewards) Progress.AddItem(it);
        // 声望入账，可能因此升阶
        if (renownGain > 0) G.OnRenownGained(renownGain);
        Progress.Save();
        expGain = 0;
        goldGain = 0;
        renownGain = 0;
        ItemRewards.Clear();
    }

    public void Dispose()
    {
        foreach (var e in Enemies) e.Dispose();
        Enemies.Clear();
        foreach (var a in Arrows) KillArrow(a);
        Arrows.Clear();
        foreach (var p in Pickups) if (!p.Taken && p.Go != null) Object.Destroy(p.Go);
        Pickups.Clear();
        if (Player != null) Player.Dispose();
        if (Fx != null) Fx.Dispose();
        if (Arena != null) Arena.Dispose();
        if (Hud != null) Hud.SetBoss(null);
    }
}
