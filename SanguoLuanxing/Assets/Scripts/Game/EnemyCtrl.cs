using UnityEngine;

/// <summary>敌人 / Boss：巡逻→追击→蓄力→出手→硬直，含 Boss 三套招式与二阶段</summary>
public class EnemyCtrl
{
    public readonly Battle B;
    public readonly UnitDef Def;
    public readonly bool IsBoss;

    public GameObject Go;
    public SpriteRenderer Body, Shadow;
    public bool Destroyed;
    FrameAnim anim;

    public Vector3 Pos, Home, Vel, Knock;
    public float Radius, Height;
    public float HP;
    public string State = "idle";
    public float Aim = Mathf.PI / 2f;
    public bool Dormant;

    float stateT, attackCd, deathT, flash, squash, breath, staggerDur, wanderT, speed;
    Vector3 wander;
    bool hitDone, dead, phase2, summoned;
    Pattern pattern;

    struct Pattern
    {
        public string Name;
        public float Windup, Dur, Range, Arc, Mul, Aoe, Knock;
    }

    public bool Dead => dead;
    public bool Alive => !dead;
    public float MaxHP { get; private set; }

    public EnemyCtrl(Battle battle, UnitDef def, float x, float z, bool boss = false)
    {
        B = battle;
        Def = def;
        IsBoss = boss;
        MaxHP = def.Hp;
        HP = def.Hp;
        Radius = boss ? 0.95f : 0.55f;
        Height = 2.25f * def.Scale;
        speed = def.Speed;
        Pos = new Vector3(x, 0f, z);
        Home = Pos;
        wander = Pos;
        wanderT = Util.Rand(1f, 3f);
        attackCd = Util.Rand(0.3f, 1.2f);
        breath = Util.Rand(0f, 10f);
        Build();
    }

    void Build()
    {
        Go = new GameObject(Def.Id);
        Go.transform.position = Pos;

        float asp;
        var tex = Tex.Cutout("char/" + Def.Sprite + ".jpg", out asp);
        Body = Tex.Actor(Go.transform, Tex.SpriteFor(tex, Height), 0);
        Body.sortingOrder = Util.SortOrder(Pos.z, 0);

        var sh = new GameObject("shadow").transform;
        sh.SetParent(Go.transform, false);
        sh.localPosition = new Vector3(0f, 0.03f, 0f);
        sh.rotation = Quaternion.Euler(-90f, 0f, 0f);
        Shadow = sh.gameObject.AddComponent<SpriteRenderer>();
        Shadow.sprite = Tex.CenterSprite(Tex.Shadow, Height * asp * 1.9f);
        Shadow.color = new Color(0f, 0f, 0f, 0.4f);
        Shadow.sortingOrder = Util.SortOrder(Pos.z, -2);

        // 帧动画：anim/{贴图id}/{状态}@{帧数} 存在即用，缺省回退程序化摆动
        anim = new FrameAnim(Body);
        anim.Load(Def.Sprite, "idle", 2, 2.5f, true, Height);
        anim.Load(Def.Sprite, "walk", 6, 10f, true, Height);
        anim.Load(Def.Sprite, "attack", 4, 10f, false, Height);
        anim.Load(Def.Sprite, "hurt", 2, 8f, false, Height);
        anim.Load(Def.Sprite, "death", 3, 6f, false, Height);
    }

    public void Dispose()
    {
        if (Go != null) Object.Destroy(Go);
    }

    // ---------------- 主循环 ----------------

    public void Update(float dt, PlayerCtrl player)
    {
        if (dead)
        {
            deathT += dt;
            if (anim != null && anim.Any) { anim.Play("death"); anim.Update(dt); }
            Go.transform.position = new Vector3(Pos.x, -deathT * 0.6f, Pos.z);
            float a = Mathf.Max(0f, 1f - deathT * 1.6f);
            Body.color = new Color(1f, 1f, 1f, a);
            Shadow.color = new Color(0f, 0f, 0f, 0.4f * a);
            if (deathT > 0.8f)
            {
                Dispose();
                Destroyed = true;
            }
            return;
        }

        flash = Mathf.Max(0f, flash - dt * 5f);
        squash = Mathf.Max(0f, squash - dt * 3.2f);
        breath += dt * 2f;
        attackCd = Mathf.Max(0f, attackCd - dt);
        stateT += dt;

        float dist = Util.Dist2(Pos.x, Pos.z, player.Pos.x, player.Pos.z);
        float toPlayer = Util.AngleBetween(Pos.x, Pos.z, player.Pos.x, player.Pos.z);

        switch (State)
        {
            case "idle":
                {
                    wanderT -= dt;
                    if (wanderT <= 0f)
                    {
                        wanderT = Util.Rand(2f, 4f);
                        float a = Util.Rand(0f, Mathf.PI * 2f);
                        wander = new Vector3(Home.x + Mathf.Cos(a) * Util.Rand(0f, 3f), 0f,
                                             Home.z + Mathf.Sin(a) * Util.Rand(0f, 3f));
                    }
                    float dx = wander.x - Pos.x, dz = wander.z - Pos.z;
                    float d = Mathf.Sqrt(dx * dx + dz * dz);
                    if (d > 0.4f)
                    {
                        Pos.x += dx / d * speed * 0.35f * dt;
                        Pos.z += dz / d * speed * 0.35f * dt;
                        Aim = Mathf.Atan2(dz, dx);
                    }
                    if (dist < Def.Aggro) Enter("chase");
                    break;
                }

            case "chase":
                {
                    Aim = toPlayer;
                    if (Def.Type == "ranged")
                    {
                        float keepMin = Def.KeepMin > 0f ? Def.KeepMin : 5f;
                        float keepMax = Def.KeepMax > 0f ? Def.KeepMax : 9f;
                        if (dist < keepMin)
                        {
                            Pos.x -= Mathf.Cos(toPlayer) * speed * dt;
                            Pos.z -= Mathf.Sin(toPlayer) * speed * dt;
                        }
                        else if (dist > keepMax)
                        {
                            Pos.x += Mathf.Cos(toPlayer) * speed * dt;
                            Pos.z += Mathf.Sin(toPlayer) * speed * dt;
                        }
                        if (dist < Def.Range && dist > keepMin * 0.8f && attackCd <= 0f) Enter("windup");
                    }
                    else if (IsBoss)
                    {
                        if (dist > Def.Range && attackCd <= 0f) BeginPattern(player);
                        if (dist > Def.Range)
                        {
                            Pos.x += Mathf.Cos(toPlayer) * speed * dt;
                            Pos.z += Mathf.Sin(toPlayer) * speed * dt;
                        }
                    }
                    else if (dist > Def.Range * 0.92f)
                    {
                        Pos.x += Mathf.Cos(toPlayer) * speed * dt;
                        Pos.z += Mathf.Sin(toPlayer) * speed * dt;
                    }
                    else if (attackCd <= 0f) BeginPattern(player);

                    if (dist > Def.Aggro * 1.6f && stateT > 4f) Enter("idle");
                    break;
                }

            case "windup":
                {
                    // 前半段可跟玩家，后半段锁死朝向——侧步可以躲开
                    float w = pattern.Name != null ? pattern.Windup : Def.Windup;
                    if (stateT < w * 0.45f) Aim = toPlayer;
                    if (stateT >= w) ExecuteAttack(player);
                    break;
                }

            case "attack":
                if (stateT > (pattern.Name != null ? pattern.Dur : 0.25f)) Enter("recover");
                break;

            case "recover":
                if (stateT >= (Def.Recover > 0f ? Def.Recover : 0.45f))
                {
                    attackCd = Def.Cd > 0f ? Def.Cd : 1f;
                    pattern = default;
                    Enter("chase");
                }
                break;

            case "stagger":
                if (stateT >= staggerDur) Enter("chase");
                break;

            case "charging":
                {
                    float p = stateT / 0.35f;
                    Pos.x += Mathf.Cos(Aim) * 26f * dt;
                    Pos.z += Mathf.Sin(Aim) * 26f * dt;
                    ChargeHit(player);
                    if (p >= 1f) Enter("recover");
                    break;
                }

            case "slam":
                if (stateT >= 0.18f)
                {
                    B.Fx.Ring(Pos, pattern.Aoe, new Color(1f, 0.6f, 0.35f), 0.5f, 0.5f);
                    B.Rig.AddTrauma(0.6f);
                    B.Sfx.Play("hit", 1f);
                    if (Util.Dist2(Pos.x, Pos.z, player.Pos.x, player.Pos.z) < pattern.Aoe + 0.6f)
                        B.DamagePlayer(Def.Atk * pattern.Mul, Pos.x, Pos.z);
                    B.Fx.Burst(Pos, new Color(1f, 0.63f, 0.29f), 26, 8f);
                    Enter("recover");
                }
                break;
        }

        Pos.x += Knock.x * dt;
        Pos.z += Knock.z * dt;
        float dec = Mathf.Pow(0.0008f, dt);
        Knock.x *= dec; Knock.z *= dec;
        B.Arena.Resolve(ref Pos, Radius, Home);

        if (IsBoss && !phase2 && HP < MaxHP * 0.5f)
        {
            phase2 = true;
            speed *= 1.18f;
            B.Hud.Toast(Def.Name + " 怒气暴涨");
            B.Rig.AddTrauma(0.8f);
            B.Sfx.Play("boss", 1f);
            if (!summoned)
            {
                summoned = true;
                B.SpawnWave(new[] { "bandit", "archer" }, 2, Pos.x, Pos.z + 2f);
            }
        }

        Animate(dt);
        UpdateBossBar();
    }

    void ChargeHit(PlayerCtrl player)
    {
        if (hitDone) return;
        if (Util.Dist2(Pos.x, Pos.z, player.Pos.x, player.Pos.z) < 2.4f)
        {
            hitDone = true;
            B.DamagePlayer(Def.Atk * 1.7f, Pos.x, Pos.z);
            B.Fx.Burst(player.Pos, new Color(1f, 0.69f, 0.38f), 12, 5f);
        }
    }

    void Enter(string s)
    {
        State = s;
        stateT = 0f;
        hitDone = false;
    }

    void BeginPattern(PlayerCtrl player)
    {
        Aim = Util.AngleBetween(Pos.x, Pos.z, player.Pos.x, player.Pos.z);

        if (!IsBoss)
        {
            // 小怪也给预警环，让玩家有反应时间
            float range = Def.Type == "ranged" ? 1.2f : Def.Range + 0.5f;
            B.Fx.Telegraph(Pos, range, new Color(1f, 0.45f, 0.32f, 0.9f), Def.Windup);
            Enter("windup");
            return;
        }

        float dist = Util.Dist2(Pos.x, Pos.z, player.Pos.x, player.Pos.z);
        string[] pool = (Def.Patterns != null && Def.Patterns.Length > 0) ? Def.Patterns : new[] { "cleave" };
        string pick = pool[Util.RandInt(0, pool.Length - 1)];
        if (dist > 9f) pick = "charge";
        if (dist < 2.6f) pick = "cleave";

        if (pick == "cleave")
        {
            pattern = new Pattern { Name = "cleave", Windup = 0.48f, Dur = 0.28f, Range = 3.2f, Arc = 1f, Mul = 1.55f, Knock = 7f };
            B.Fx.Telegraph(Pos, 3.2f, new Color(1f, 0.54f, 0.38f), 0.48f);
        }
        else if (pick == "slam")
        {
            pattern = new Pattern { Name = "slam", Windup = 0.85f, Aoe = 4f, Mul = 1.9f };
            B.Fx.Telegraph(Pos, 4f, new Color(0.88f, 0.28f, 0.2f), 0.85f);
            B.Sfx.Play("boss", 0.8f);
        }
        else
        {
            pattern = new Pattern { Name = "charge", Windup = 0.65f, Dur = 0.32f, Mul = 1.75f };
            B.Fx.Telegraph(Pos + new Vector3(Mathf.Cos(Aim), 0f, Mathf.Sin(Aim)) * 4f, 2.2f, new Color(1f, 0.5f, 0.35f), 0.65f);
            B.Fx.Slash(Pos + new Vector3(Mathf.Cos(Aim), 0f, Mathf.Sin(Aim)) * 6f, Aim, new Color(1f, 0.5f, 0.35f), 10f, 0.65f);
            B.Sfx.Play("boss", 0.8f);
        }
        Enter("windup");
    }

    void ExecuteAttack(PlayerCtrl player)
    {
        if (pattern.Name == "slam") { State = "slam"; stateT = 0f; return; }
        if (pattern.Name == "charge")
        {
            State = "charging";
            stateT = 0f;
            hitDone = false;
            B.Fx.Burst(Pos, new Color(1f, 0.54f, 0.38f), 16, 6f);
            return;
        }

        if (Def.Type == "ranged")
        {
            B.SpawnArrow(Pos, Aim, Def.Atk, Def.ArrowSpeed > 0f ? Def.ArrowSpeed : 9f);
            B.Sfx.Play("arrow", 0.8f);
            B.Fx.Slash(Pos, Aim, new Color(0.85f, 0.91f, 1f), 0.7f, 1.2f);
        }
        else
        {
            float range = pattern.Name != null ? pattern.Range : Def.Range + 0.7f;
            float arc = pattern.Name != null ? pattern.Arc : 0.85f;
            float mul = pattern.Name != null ? pattern.Mul : 1f;
            B.Fx.Slash(Pos + new Vector3(Mathf.Cos(Aim), 0f, Mathf.Sin(Aim)) * 1.2f, Aim,
                new Color(1f, 0.69f, 0.63f), IsBoss ? 1.8f : 0.95f);
            if (B.IsPlayerInArc(Pos.x, Pos.z, range, arc, Aim))
            {
                B.DamagePlayer(Def.Atk * mul, Pos.x, Pos.z);
                B.HitStop = Mathf.Max(B.HitStop, IsBoss ? 0.12f : 0.07f);
                B.Rig.AddTrauma(IsBoss ? 0.55f : 0.38f);
                B.Rig.Punch(IsBoss ? 1.6f : 1.1f);
                B.Fx.Impact(player.Pos + Vector3.up * 0.9f, new Color(1f, 0.55f, 0.4f), IsBoss ? 2.4f : 1.7f);
            }
            B.Sfx.Play("swing", 0.85f);
        }
        State = "attack";
        stateT = 0f;
    }

    void Animate(float dt)
    {
        float sq = squash;

        if (anim != null && anim.Any)
        {
            string want;
            if (State == "windup" || State == "attack" || State == "charging" || State == "slam") want = "attack";
            else if (State == "stagger") want = "hurt";
            else if (State == "chase") want = "walk";
            else want = "idle";
            if (!anim.Has(want)) want = "idle";
            anim.Play(want, want == "attack" && State == "windup" && stateT < dt * 1.5f);
            anim.Update(dt);

            // AI 帧条人物原生朝左，所以朝右时要翻转
            Body.flipX = Mathf.Cos(Aim) > 0.15f;
            // 命中挤压：帧动画分支原本把缩放写死成 1，受击的"变形"反馈整个丢了
            Body.transform.localScale = new Vector3(1f + sq * 0.55f, 1f - sq * 0.75f, 1f);
            Body.transform.localPosition = new Vector3(0f, -Height * sq * 0.18f, 0f);
            Body.transform.localRotation = Quaternion.identity;
            Go.transform.position = new Vector3(Pos.x, 0f, Pos.z);
            Body.sortingOrder = Util.SortOrder(Pos.z, 0);
            Shadow.sortingOrder = Util.SortOrder(Pos.z, -2);
            Shadow.transform.position = new Vector3(Pos.x, 0.03f, Pos.z);

            Body.color = BodyTint();
            return;
        }

        float bob = Mathf.Sin(breath + stateT * 6f) * (State == "chase" ? 0.06f : 0.02f);
        bool faceLeft = Mathf.Cos(Aim) < -0.15f;

        Body.flipX = faceLeft;
        Body.transform.localScale = new Vector3(1f + sq * 0.55f, 1f - sq * 0.75f, 1f);
        Body.transform.localPosition = new Vector3(0f, bob - Height * sq * 0.22f, 0f);

        float lean = 0f;
        if (State == "windup") lean = -0.12f;
        else if (State == "charging") lean = 0.28f;
        else if (State == "stagger") lean = -0.2f;
        Body.transform.localRotation = Quaternion.Euler(0f, 0f, -lean);

        Go.transform.position = new Vector3(Pos.x + (faceLeft ? -lean : lean) * 0.3f, 0f, Pos.z);
        Body.sortingOrder = Util.SortOrder(Pos.z, 0);
        Shadow.sortingOrder = Util.SortOrder(Pos.z, -2);
        Shadow.transform.position = new Vector3(Pos.x, 0.03f, Pos.z);

        Body.color = BodyTint();
    }

    Color BodyTint()
    {
        if (flash > 0f) return HitTint();
        if (State != "windup") return Color.white;
        float w = pattern.Name != null ? pattern.Windup : Def.Windup;
        float u = Mathf.Clamp01(stateT / Mathf.Max(0.08f, w));
        return Color.Lerp(Color.white, new Color(1f, 0.38f, 0.28f), 0.2f + u * 0.8f);
    }

    void UpdateBossBar()
    {
        if (IsBoss) B.Hud.SetBoss(this);
    }

    // ---------------- 受击 ----------------

    public void TakeDamage(float dmg, HitOpts opts)
    {
        if (dead) return;
        float mitigated = dmg * (1f - Def.Def / (Def.Def + 95f));
        float finalDmg = Mathf.Max(1f, Mathf.Round(mitigated * Util.Rand(0.92f, 1.08f)));
        HP -= finalDmg;
        flash = 1f;
        squash = Mathf.Min(0.55f, squash + (opts.Heavy || opts.Crit ? 0.52f : 0.34f));

        Vector3 from = opts.From.HasValue ? opts.From.Value : new Vector3(Pos.x, 0f, Pos.z + 1f);
        float a = Mathf.Atan2(Pos.z - from.z, Pos.x - from.x);
        float kb = (opts.Knock > 0f ? opts.Knock : 3f) / (IsBoss ? 2.2f : 1f);
        Knock.x += Mathf.Cos(a) * kb;
        Knock.z += Mathf.Sin(a) * kb;

        B.Hud.DamageAt(new Vector3(Pos.x, Height * 0.8f, Pos.z), Mathf.RoundToInt(finalDmg),
            opts.Crit ? "crit" : (opts.Skill ? "skill" : ""));
        B.Sfx.Play(opts.Crit ? "crit" : "hit", 0.85f);
        B.Rig.AddTrauma(opts.Crit ? 0.50f : (opts.Heavy ? 0.62f : 0.30f));

        // 打在前摇上一定打断。轻击能粘住薄甲，重甲只吃重击。
        bool interrupt = !IsBoss && State == "windup";
        bool lightStick = !IsBoss && Def.Poise < 0.7f;
        if (!IsBoss && (interrupt || opts.Heavy || lightStick))
        {
            Enter("stagger");
            staggerDur = opts.Heavy ? 0.55f : (interrupt ? 0.42f : 0.32f);
        }
        if (IsBoss && opts.Heavy && Util.Rand(0f, 1f) < 0.45f)
        {
            Enter("stagger");
            staggerDur = 0.42f;
        }

        if (HP <= 0f)
        {
            HP = 0f;
            dead = true;
            deathT = 0f;
            B.OnEnemyDeath(this);
        }
    }

    /// <summary>受击闪色：命中瞬间纯白，随后退回肤色</summary>
        Color HitTint()
    {
        if (flash <= 0f) return Color.white;
        if (flash > 0.55f) return Color.white;
        return Color.Lerp(new Color(1f, 0.62f, 0.48f, 1f), Color.white, flash / 0.55f);
    }

    public void Summarize() { }
}
