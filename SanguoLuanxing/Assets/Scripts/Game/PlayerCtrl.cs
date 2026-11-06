using UnityEngine;

public struct FrameInput
{
    public float MX, MZ;
    public bool Light, Heavy, Dodge, Q, E, R;
}

/// <summary>玩家：状态机 + 打击反馈（前踏 / 顿帧 / 推镜 / 挤压）全部移植自 player.js</summary>
public class PlayerCtrl
{
    struct LightData
    {
        public float Dur, Hit, Dmg, Range, Arc, Knock;
        public LightData(float dur, float hit, float dmg, float range, float arc, float knock)
        { Dur = dur; Hit = hit; Dmg = dmg; Range = range; Arc = arc; Knock = knock; }
    }

    // (持续, 命中帧, 伤害倍率, 范围, 扇形, 击退)
    // 命中帧对齐序列帧：轻击 4 帧 @11fps，第 2 帧才是刀锋到位的那一帧
    static readonly LightData[] Light =
    {
        new LightData(0.36f, 0.14f, 1.00f, 2.75f, 1.2f, 2.6f),
        new LightData(0.38f, 0.15f, 1.08f, 2.75f, 1.2f, 2.8f),
        new LightData(0.55f, 0.21f, 1.55f, 3.05f, 1.35f, 5.0f),
    };

    static readonly LightData Heavy = new LightData(0.78f, 0.42f, 2.2f, 2.9f, 1.05f, 7.5f);

    public readonly Battle B;
    public GameObject Go;
    public SpriteRenderer Body, Shadow, Aura;
    FrameAnim anim;

    public Vector3 Pos, Vel, Knock;
    public float Radius = 0.62f, Height = 2.6f, Aim = -Mathf.PI / 2f;

    public Stats Stats;
    public float MaxHP, HP, MaxQi = 100f, Qi = 100f, MaxStam = 100f, Stam = 100f;

    public string State = "idle";
    float attackT, dashT, hurtT, invuln, buff, buffAtk, buffSpeed, skillT, moveTime, breath, flash;
    public int AttackStep, SkillIndex = -1, SkillLv = 1, ChargeHits, WhirlPulses;
    public float[] Cooldowns = { 0f, 0f, 0f };
    public Vector3 DashDir;
    bool attackBuffered, hitDone, dead;
    float comboReset, trailT;
    SkillDef skillData;

    public bool Dead => dead;
    public bool Alive => !dead;
    public float Atk => Stats.Atk * (1f + (buff > 0f ? buffAtk : 0f));
    public float Speed => Stats.Speed * (1f + (buff > 0f ? buffSpeed : 0f));

    // 上一帧的落脚标记：帧动画每循环一次要走两趟（左右脚各一次），
    // 所以用"是否刚跨过半个循环"来判断该不该响脚步。
    int lastStepPhase;
    public bool FreeMove => State == "dash" || State == "skill";

    public PlayerCtrl(Battle battle)
    {
        B = battle;
        var stats = B.Progress.Stats();
        Stats = stats;
        MaxHP = stats.MaxHP;
        HP = stats.MaxHP;
        Build();
    }

    void Build()
    {
        Go = new GameObject("Player");
        Go.transform.position = Pos;

        float asp;
        var tex = Tex.Cutout("char/player.jpg", out asp);
        Body = Tex.Actor(Go.transform, Tex.SpriteFor(tex, Height), 0);
        Body.sortingOrder = Util.SortOrder(0f, 0);

        var sh = new GameObject("shadow").transform;
        sh.SetParent(Go.transform, false);
        sh.localPosition = new Vector3(0f, 0.03f, 0f);
        sh.rotation = Quaternion.Euler(-90f, 0f, 0f);
        Shadow = sh.gameObject.AddComponent<SpriteRenderer>();
        Shadow.sprite = Tex.CenterSprite(Tex.Shadow, 1.9f);
        Shadow.color = new Color(0f, 0f, 0f, 0.42f);
        Shadow.sortingOrder = Util.SortOrder(0f, -2);

        var au = new GameObject("aura").transform;
        au.SetParent(Go.transform, false);
        au.localPosition = new Vector3(0f, 0.05f, 0f);
        au.rotation = Quaternion.Euler(-90f, 0f, 0f);
        Aura = au.gameObject.AddComponent<SpriteRenderer>();
        Aura.sprite = Tex.CenterSprite(Tex.Glow, 3f);
        Aura.color = new Color(1f, 0.77f, 0.42f, 0.5f);
        Aura.sortingOrder = Util.SortOrder(0f, -28);
        Aura.enabled = false;

        // 帧动画：anim/player/{状态}@{帧数} 存在即用，缺省回退程序化摆动
        anim = new FrameAnim(Body);
        // 只接同一套立绘的待机和走路。旧的攻击帧是另一个人，接进来会在出招时换脸。
        anim.Load("player", "idle", 2, 2.2f, true, Height);
        // 走路帧 4 帧一个循环（=2 步），步长约 1.2 世界单位。
        // 按设计速度 6.4 算，脚不滑地需要 ~10.6 fps —— 6fps 会让脚迈得比地面慢，
        // 走起来像贴地平移。
        anim.Load("player", "walk", 4, 11f, true, Height);
        if (!anim.Has("walk")) anim.Load("player", "walk", 6, 10f, true, Height);

        // 攻击帧：轻击 4 帧 @11fps，命中帧（第 2 帧）正好落在 Light[].Hit 的时刻。
        // 二/三段没有独立素材时借用一段：二段原速、三段放慢，撑满各自更长的挥砍时间。
        anim.Load("player", "attack1", 4, 11f, false, Height);
        anim.Load("player", "attack1", 4, 11f, false, Height, "attack2");
        anim.Load("player", "attack1", 4, 8f, false, Height, "attack3");
        // 重击 4 帧 @6fps：蹲身蓄力 → 举刀过顶 → 下劈（命中）→ 收势
        anim.Load("player", "heavy", 4, 6f, false, Height);
    }

    public void Dispose()
    {
        if (Go != null) Object.Destroy(Go);
    }

    // ---------------- 主循环 ----------------

    public void Update(float dt, FrameInput input, bool active)
    {
        if (dead)
        {
            flash = Mathf.Max(0f, flash - dt * 3f);
            if (anim != null && anim.Has("death")) { anim.Play("death"); anim.Update(dt); }
            else if (anim != null && anim.Has("idle")) anim.Play("idle");
            ApplyVisual();
            return;
        }

        for (int i = 0; i < 3; i++) Cooldowns[i] = Mathf.Max(0f, Cooldowns[i] - dt);
        invuln = Mathf.Max(0f, invuln - dt);
        buff = Mathf.Max(0f, buff - dt);
        comboReset = Mathf.Max(0f, comboReset - dt);
        flash = Mathf.Max(0f, flash - dt * 6f);

        Stam = Mathf.Clamp(Stam + 24f * dt, 0f, MaxStam);
        Qi = Mathf.Clamp(Qi + 6.5f * Stats.QiMul * dt, 0f, MaxQi);

        if (active) HandleInput(dt, input);

        ApplyMotion(dt);
        Animate(dt);

        Aura.enabled = buff > 0f;
        if (Aura.enabled)
        {
            Aura.transform.localScale = Vector3.one * (1f + Mathf.Sin(breath * 6f) * 0.06f);
            Aura.color = new Color(1f, 0.77f, 0.42f, 0.35f + Mathf.Sin(breath * 5f) * 0.15f);
        }
    }

    void HandleInput(float dt, FrameInput input)
    {
        if (State == "hurt")
        {
            hurtT -= dt;
            if (hurtT <= 0f) State = "idle";
            return;
        }

        if (State == "dash")
        {
            dashT -= dt;
            if (dashT <= 0f) State = "idle";
            return;
        }

        if (State == "skill") { UpdateSkill(dt); return; }

        if (State == "attack" || State == "heavy")
        {
            attackT += dt;
            Drift(dt, input.MX, input.MZ);   // 出招也能挪步，减少"粘地"感

            LightData data = State == "attack" ? Light[AttackStep] : Heavy;
            if (!hitDone && attackT >= data.Hit)
            {
                hitDone = true;
                if (Strike(data, State == "heavy" ? Stats.HeavyMul : 1f, false) == 0)
                    MissFeedback(data.Range, Aim, State == "heavy" ? 1.85f : 1.3f);
            }
            if (input.Light) attackBuffered = true;
            if (hitDone && TryDodge(input)) return;

            if (attackT >= data.Dur)
            {
                if (attackBuffered && State == "attack" && AttackStep < 2)
                {
                    StartLight(AttackStep + 1);
                }
                else
                {
                    State = "idle";
                    attackBuffered = false;
                    comboReset = 0.55f;
                }
            }
            return;
        }

        if (TryDodge(input)) return;
        if (TrySkill(input)) return;

        if (input.Light)
        {
            StartLight(comboReset > 0f ? Mathf.Min(2, AttackStep + 1) : 0);
            return;
        }
        if (input.Heavy) { StartHeavy(); return; }

        if (Mathf.Abs(input.MX) > 0.01f || Mathf.Abs(input.MZ) > 0.01f)
        {
            float l = Mathf.Sqrt(input.MX * input.MX + input.MZ * input.MZ);
            float dx = input.MX / l, dz = input.MZ / l;
            float k = Util.K(24f, dt);
            Vel.x += (dx * Speed - Vel.x) * k;
            Vel.z += (dz * Speed - Vel.z) * k;
            moveTime += dt;
        }
        else
        {
            float dec = Mathf.Pow(0.00002f, dt);
            Vel.x *= dec; Vel.z *= dec;
            moveTime = 0f;
        }

        // 步频跟着实际移动速度走：跑得快脚就该迈得勤，否则会出现"平移滑步"。
        // 基准是设计速度 6.4（Progress.Stats().Speed 的初始值）。
        if (anim != null && anim.Has("walk"))
        {
            float spd = Mathf.Sqrt(Vel.x * Vel.x + Vel.z * Vel.z);
            anim.SetRate("walk", Mathf.Clamp(spd / 6.4f, 0.45f, 1.9f));
        }
    }

    void Drift(float dt, float mx, float mz)
    {
        float len = Mathf.Sqrt(mx * mx + mz * mz);
        if (len < 0.01f)
        {
            float dec = Mathf.Pow(0.00005f, dt);
            Vel.x *= dec; Vel.z *= dec;
            return;
        }
        float v = Speed * 0.3f;
        float k = Util.K(16f, dt);
        Vel.x += ((mx / len) * v - Vel.x) * k;
        Vel.z += ((mz / len) * v - Vel.z) * k;
        moveTime += dt;
    }

    bool TryDodge(FrameInput input)
    {
        if (!input.Dodge) return false;
        if (Stam < 26f) return false;
        Stam -= 26f;
        State = "dash";
        dashT = 0.26f;
        invuln = 0.32f;
        DashDir = new Vector3(Mathf.Cos(Aim), 0f, Mathf.Sin(Aim));
        B.Sfx.Play("dash", 0.8f);
        B.Fx.Ring(Pos, 1.4f, new Color(0.62f, 0.78f, 0.87f), 0.35f, 0.5f);
        B.Fx.Afterimage(Body, 0.28f);
        return true;
    }

    bool TrySkill(FrameInput input)
    {
        if (input.Q) return CastSkill(0);
        if (input.E) return CastSkill(1);
        if (input.R) return CastSkill(2);
        return false;
    }

    bool CastSkill(int i)
    {
        var s = DB.Skills[i];
        int lv = B.Progress.Data.skillLv[i];
        if (Cooldowns[i] > 0f) { B.Hud.Toast("尚在蓄势"); return true; }
        float cost = DB.SkillCost(s, lv);
        if (Qi < cost) { B.Hud.Toast("气力不足"); return true; }

        Qi -= cost;
        Cooldowns[i] = DB.SkillCooldown(s, lv) * Talents.CdMul(B.Progress);
        SkillIndex = i;
        skillT = 0f;
        SkillLv = lv;
        skillData = s;
        State = "skill";
        hitDone = false;
        ChargeHits = 0;
        trailT = 0f;
        WhirlPulses = 0;
        B.Sfx.Play("skill", 0.9f);

        if (s.Id == "whirl")
        {
            skillT = 0.4f;
            WhirlPulse(DB.SkillDamage(s, lv) * 0.95f);
            B.Fx.Spin(Pos, s.Radius, new Color(1f, 0.85f, 0.58f), 0.45f, 1f);
            B.Fx.Spin(Pos, s.Radius * 0.72f, new Color(1f, 0.94f, 0.78f), 0.36f, -1f);
            B.Rig.AddTrauma(0.4f);
        }
        else if (s.Id == "warlord")
        {
            buff = DB.SkillBuffDuration(s, lv);
            buffAtk = DB.SkillBuffAtk(lv);
            buffSpeed = DB.SkillBuffSpeed();
            skillT = 0.6f;
            B.Fx.Ring(Pos, 4.4f, new Color(1f, 0.81f, 0.5f), 0.7f, 0.5f);
            B.Fx.Pillar(Pos, new Color(1f, 0.82f, 0.48f), 8f, 3.4f, 0.7f);
            B.Fx.Flash(Pos, new Color(1f, 0.82f, 0.48f), 5.2f, 0.35f);
            B.Fx.Burst(Pos, new Color(1f, 0.77f, 0.42f), 16, 8f);
            int hits = B.HitArc(Pos, 4.2f, Mathf.PI, 0.85f,
                new HitOpts { Knock = 7.5f, Skill = true, Up = 1.2f, Aim = Aim });
            if (hits == 0) MissFeedback(4.2f, Aim, 1.8f);
            B.Rig.AddTrauma(0.8f);
            B.Sfx.Play("boss", 0.9f);
            B.Hud.Toast("武神降临");
        }
        else
        {
            skillT = 0.36f;
            DashDir = new Vector3(Mathf.Cos(Aim), 0f, Mathf.Sin(Aim));
            B.Fx.Ring(Pos, 1.6f, new Color(1f, 0.82f, 0.5f), 0.3f, 0.6f);
        }
        return true;
    }

    void UpdateSkill(float dt)
    {
        var s = skillData;
        skillT -= dt;

        if (s.Id == "charge")
        {
            float dur = 0.36f;
            float p = Mathf.Clamp01(1f - skillT / dur);
            Pos.x += DashDir.x * 18f * dt;
            Pos.z += DashDir.z * 18f * dt;

            trailT -= dt;
            if (trailT <= 0f)
            {
                trailT = 0.045f;
                B.Fx.Slash(Pos - DashDir * 0.9f, Aim, new Color(1f, 0.82f, 0.48f), 1.15f);
                B.Fx.Afterimage(Body, 0.16f);
            }

            float[] marks = { 0.28f, 0.7f };
            while (ChargeHits < marks.Length && p >= marks[ChargeHits])
            {
                int idx = ChargeHits;
                ChargeHits++;
                B.Fx.Ring(Pos, 1.8f, new Color(1f, 0.82f, 0.48f), 0.28f, 0.5f);
                // 突刺是穿过去的，两段判定都可能擦空；只在第一段报「落空」，免得更吵
                int hits = Strike(new LightData(0f, 0f,
                    DB.SkillDamage(s, SkillLv) * (idx == 0 ? 1f : 0.65f),
                    3.2f, 1.15f, idx == 0 ? 6.5f : 4f), 1f, true);
                if (hits == 0 && idx == 0) MissFeedback(3.2f, Aim, 1.5f);
            }
        }
        else if (s.Id == "whirl")
        {
            if (WhirlPulses < 1 && skillT <= 0.2f)
            {
                WhirlPulses = 1;
                WhirlPulse(DB.SkillDamage(s, SkillLv) * 0.7f);
                B.Fx.Spin(Pos, s.Radius * 0.9f, new Color(1f, 0.88f, 0.66f), 0.32f, -1f);
            }
        }

        if (skillT <= 0f)
        {
            State = "idle";
            hitDone = false;
            SkillIndex = -1;
            ChargeHits = 0;
            WhirlPulses = 0;
        }
    }

    void WhirlPulse(float mul)
    {
        var s = DB.Skills[1];
        int hits = B.HitArc(Pos, s.Radius, Mathf.PI, mul, new HitOpts { Knock = 5.5f, Skill = true, Up = 1.6f, Aim = Aim });
        if (hits == 0) MissFeedback(s.Radius, Aim, 1.6f);
        B.Fx.Ring(Pos, s.Radius, new Color(1f, 0.85f, 0.58f), 0.45f, 0.5f);
        B.Fx.Burst(Pos, new Color(1f, 0.85f, 0.58f), 8, 7f);
        B.Rig.AddTrauma(0.3f);
    }

    void StartLight(int step)
    {
        FaceNearest(3.4f);
        State = "attack";
        AttackStep = step;
        attackT = 0f;
        hitDone = false;
        attackBuffered = false;
        if (anim != null) anim.Play("attack" + (step + 1), true);
        B.Sfx.Play("swing", 0.9f);
        // 刀光挪到命中帧再出（见 Strike），保证视觉和判定同帧
    }

    void StartHeavy()
    {
        if (Stam < 12f) { B.Hud.Toast("精力不足"); return; }
        Stam -= 12f;
        FaceNearest(3.6f);
        State = "heavy";
        attackT = 0f;
        hitDone = false;
        attackBuffered = false;
        if (anim != null) anim.Play("heavy", true);
        B.Sfx.Play("swing", 1f);
        B.Fx.Ring(Pos, 1.8f, new Color(1f, 0.69f, 0.38f), 0.3f, 0.6f);
    }

    void FaceNearest(float reach)
    {
        float best = reach * reach;
        EnemyCtrl hit = null;
        for (int i = 0; i < B.Enemies.Count; i++)
        {
            var e = B.Enemies[i];
            if (e.Dead || e.Dormant) continue;
            float dx = e.Pos.x - Pos.x, dz = e.Pos.z - Pos.z;
            float d = dx * dx + dz * dz;
            if (d >= best) continue;
            best = d;
            hit = e;
        }
        if (hit != null) Aim = Mathf.Atan2(hit.Pos.z - Pos.z, hit.Pos.x - Pos.x);
    }

    /// <summary>判定 + 前踏半步 + 刀光（打击感最直接的来源）。返回命中数，0 表示落空。</summary>
    int Strike(LightData data, float mulBonus, bool skill)
    {
        float a = Aim;
        // 前踏：Knock 以 pow(0.0008,dt) 衰减，位移 ≈ lunge/7.13，所以数值要比直觉大
        float lunge = State == "heavy" ? 9.0f : 5.5f;
        Knock.x += Mathf.Cos(a) * lunge;
        Knock.z += Mathf.Sin(a) * lunge;

        // 刀光在命中帧出现：视觉与伤害对齐才有力
        if (!skill)
        {
            float sz = State == "heavy" ? 1.85f : (AttackStep == 2 ? 1.5f : 1.12f);
            var p = Pos + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 1.15f;
            B.Fx.Slash(p, a, new Color(1f, 0.97f, 0.88f), sz);
            B.Fx.Slash(p, a + 0.18f, new Color(1f, 0.82f, 0.45f), sz * 0.72f, 0.16f);
        }

        return B.HitArc(Pos, data.Range, data.Arc, data.Dmg * mulBonus,
            new HitOpts { Knock = data.Knock, Skill = skill, Heavy = State == "heavy", Aim = a });
    }

    /// <summary>落空也要给反馈：漂一个「落空」+ 一道淡灰刀风。
    /// 没有这个的话，玩家分不清是没够到距离、还是判定根本没生效。</summary>
    void MissFeedback(float range, float aim, float size)
    {
        var p = Pos + new Vector3(Mathf.Cos(aim), 0f, Mathf.Sin(aim)) * (range * 0.62f);
        B.Fx.Slash(new Vector3(p.x, 0.9f, p.z), aim, new Color(0.80f, 0.83f, 0.90f, 0.45f), size * 0.8f, 0.2f);
        B.Hud.DamageAt(new Vector3(p.x, 1.25f, p.z), 0, "miss");
    }

    void ApplyMotion(float dt)
    {
        Pos.x += Knock.x * dt;
        Pos.z += Knock.z * dt;
        float dec = Mathf.Pow(0.0008f, dt);
        Knock.x *= dec; Knock.z *= dec;

        if (State == "dash")
        {
            Pos.x += DashDir.x * 15f * dt;
            Pos.z += DashDir.z * 15f * dt;
        }
        else if (State != "skill")
        {
            Pos.x += Vel.x * dt;
            Pos.z += Vel.z * dt;
        }
        B.Arena.Resolve(ref Pos, Radius, null);
    }

    void Animate(float dt)
    {
        // 有攻击帧就让帧动画接管出招：程序化的整体前后倾只能算"晃了一下"，
        // 玩家看不出刀砍出去没有。闪避/受击/施法仍走程序化（那几种没有帧素材）。
        string want = null;
        bool frameDriven = false;
        if (State == "attack") { want = anim != null && anim.Has("attack" + (AttackStep + 1)) ? "attack" + (AttackStep + 1) : "attack1"; frameDriven = true; }
        else if (State == "heavy") { want = "heavy"; frameDriven = true; }
        else if (State != "dash" && State != "hurt" && State != "skill" && !dead)
            want = moveTime > 0.02f && anim != null && anim.Has("walk") ? "walk" : "idle";

        // 帧动画只负责"换哪一帧"，身体的重心起伏由程序化叠加上去。
        // 之前这里是直接 return，把 localPosition/localScale 全写死成 0/1，
        // 于是走路时身体纹丝不动 —— 腿在动、人像滑冰，这正是"滑行"的另一半原因。
        bool walking = false;
        if (anim != null && anim.Any && want != null && anim.Has(want))
        {
            anim.Play(want);
            anim.Update(dt);
            Body.flipX = Mathf.Cos(Aim) > 0.15f;
            walking = want == "walk";
        }
        else if (anim != null && anim.Has("idle"))
        {
            anim.Play("idle");
            anim.Update(0f);
            Body.flipX = Mathf.Cos(Aim) > 0.15f;
        }

        breath += dt * 2.2f;
        float bob = 0f;
        float lean = 0f;
        float stretch = 1f;
        float sway = 0f;

        if (State == "attack" || State == "heavy")
        {
            LightData data = State == "attack" ? Light[AttackStep] : Heavy;
            float p = Mathf.Clamp01(attackT / data.Dur);
            float swing = Mathf.Sin(p * Mathf.PI);
            lean = swing * 0.16f;
            bob = -swing * 0.05f;
            stretch = 1f + swing * 0.03f;
            Body.transform.localRotation = Quaternion.Euler(0f, 0f, -swing * 0.12f);
        }
        else if (State == "dash")
        {
            float p = 1f - Mathf.Clamp01(dashT / 0.26f);
            bob = -Mathf.Sin(p * Mathf.PI) * 0.1f;
            stretch = 1f + Mathf.Sin(p * Mathf.PI) * 0.12f;
            lean = 0.2f;
            Body.transform.localRotation = Quaternion.identity;
        }
        else if (State == "hurt")
        {
            lean = -0.22f;
            bob = -0.04f;
            Body.transform.localRotation = Quaternion.identity;
        }
        else if (walking)
        {
            // 走路：腿由帧动画负责，身体的重心跟着脚步一起起落。
            // 相位取帧动画自己的进度，两边才不会各走各的（那会变成原地弹跳）。
            float ph = anim.Phase01;
            // 一个步循环里身体起落两次（左右脚各一次）
            bob = -Mathf.Abs(Mathf.Sin(ph * Mathf.PI * 2f)) * 0.05f;
            // 只做上下起伏，不做左右平移：整体左右挪会让身体在两张腿之间荡，
            // 看着像原地摇摆而不是在走路。
            lean = 0.07f;
            Body.transform.localRotation = Quaternion.identity;

            // 脚步声踩在落脚点上：帧动画半循环走一步，跨过就响一次。
            int sp = Mathf.FloorToInt(ph * 2f);
            if (sp != lastStepPhase)
            {
                lastStepPhase = sp;
                B.Sfx.Play("step", 0.35f);
            }
        }
        else if (moveTime > 0f)
        {
            // 没有帧素材时的程序化兜底：左右微摆 + 上下起伏
            bob = Mathf.Abs(Mathf.Sin(moveTime * 10f)) * 0.09f;
            sway = Mathf.Sin(moveTime * 10f) * 0.055f;
            lean = 0.07f;
            Body.transform.localRotation = Quaternion.identity;
        }
        else
        {
            Body.transform.localRotation = Quaternion.identity;
        }

        // 朝屏幕右（世界 -x）。所有立绘素材原生都朝右，朝左才需要 flipX 翻转。
        bool faceLeft = Mathf.Cos(Aim) < -0.15f;
        // 没有立绘时仍按旧的程序化朝向
        if (!(anim != null && anim.Any && want != null && anim.Has(want)))
            Body.flipX = (anim != null && anim.Has("idle")) ? Mathf.Cos(Aim) > 0.15f : faceLeft;
        Body.transform.localScale = new Vector3(1f, stretch, 1f);
        Body.transform.localPosition = new Vector3(sway, bob, 0f);

        float lx = (faceLeft ? -lean : lean) * 0.4f;
        Go.transform.position = new Vector3(Pos.x + lx, 0f, Pos.z);

        Body.sortingOrder = Util.SortOrder(Pos.z, 0);
        Shadow.sortingOrder = Util.SortOrder(Pos.z, -2);
        Shadow.transform.position = new Vector3(Pos.x, 0.03f, Pos.z);
        // 影子跟着身体起伏缩放：人抬高时影子略放大变淡，落地时收紧。
        // 这是判断"有没有踩实"的关键视觉线索，缺了就像踩在云上。
        float lift = Mathf.Clamp(bob / 0.09f, -1f, 1f);
        Shadow.transform.localScale = new Vector3(1f + lift * 0.06f, 1f + lift * 0.06f, 1f);
        Shadow.color = new Color(0f, 0f, 0f, 0.42f - lift * 0.06f);
        Aura.sortingOrder = Util.SortOrder(Pos.z, -28);
        Aura.transform.position = new Vector3(Pos.x, 0.05f, Pos.z);

        ApplyVisual();
    }

    void ApplyVisual()
    {
        if (flash > 0f) Body.color = Color.Lerp(new Color(1f, 0.45f, 0.40f, 1f), Color.white, Mathf.Clamp01(flash));
        else Body.color = Color.white;
    }

    // ---------------- 受击 ----------------

    public void ReceiveDamage(float amount, float sourceX, float sourceZ)
    {
        if (dead || invuln > 0f) return;
        float dmg = Mathf.Max(1f, Mathf.Round(amount * (1f - Stats.Def / (Stats.Def + 90f))));
        HP -= dmg;
        flash = 1f;
        invuln = 0.42f;
        State = "hurt";
        hurtT = 0.3f;

        float a = Mathf.Atan2(Pos.z - sourceZ, Pos.x - sourceX);
        Knock.x += Mathf.Cos(a) * 6f;
        Knock.z += Mathf.Sin(a) * 6f;

        B.HitStop = Mathf.Max(B.HitStop, 0.08f);
        B.Rig.AddTrauma(0.72f);
        B.Rig.Punch(1.4f);
        B.Rig.Kick(new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)), 0.55f);
        B.Sfx.Play("hurt", 0.9f);
        B.Hud.DamageAt(new Vector3(Pos.x, 1.6f, Pos.z), Mathf.RoundToInt(dmg), "self");
        B.Hud.Flash(new Color(0.88f, 0.16f, 0.13f), 0.30f);
        B.Fx.Burst(Pos, new Color(0.84f, 0.29f, 0.24f), 8, 4.6f);
        B.Fx.Impact(new Vector3(Pos.x, 0.9f, Pos.z), new Color(1f, 0.42f, 0.34f), 2.0f);

        if (HP <= 0f)
        {
            HP = 0f;
            dead = true;
            State = "dead";
            Body.color = new Color(0.55f, 0.5f, 0.5f, 1f);
            B.OnPlayerDeath();
        }
    }

    public void Heal(float amount) => HP = Mathf.Min(MaxHP, HP + amount);

    public void AimAtMouse(Vector3 ground, float dt)
    {
        float dx = ground.x - Pos.x, dz = ground.z - Pos.z;
        if (Mathf.Abs(dx) + Mathf.Abs(dz) < 0.18f) return;
        float want = Mathf.Atan2(dz, dx);
        Aim = Util.AngleLerp(Aim, want, Util.K(18f, dt));
    }

    /// <summary>移动时优先朝向移动方向，比纯跟鼠标更跟手</summary>
    public void AimToward(float dx, float dz, float dt)
    {
        if (Mathf.Abs(dx) + Mathf.Abs(dz) < 0.05f) return;
        float want = Mathf.Atan2(dz, dx);
        Aim = Util.AngleLerp(Aim, want, Util.K(22f, dt));
    }
}
