using System;
using System.Collections.Generic;
using UnityEngine;

public enum GamePhase { Title, Faction, Hub, Character, Playing, Story, Victory, Defeat, Ending, Pause }

/// <summary>
/// 游戏主循环：标题 → 选阵营 → 过剧情 → 实时战斗 → 结算 → 下一关。
/// 场景完全由代码搭建（见 Boot.cs），不需要在编辑器里手动摆物体。
/// </summary>
public class Game : MonoBehaviour
{
    public static Game I;

    public Camera Cam;
    public CameraRig Rig;
    public Hud Hud;
    public Sfx Sfx;
    public Progress Progress;
    public Battle Battle;
    public WorldExplore World;

    public GamePhase Phase = GamePhase.Title;
    public int MapIndex;

    Line[] storyLines;
    int storyIdx;
    float storyT;
    Action storyDone;
    bool storyOpen;
    int pendingPraise = -1;   // 待播的升阶评语（-1 = 没有）

    Texture2D titleBg, endingBg;
    Texture2D playerArt;
    float playerArtAspect = 1f;
    readonly Texture2D[] factionFace = new Texture2D[3];
    static readonly string[] FactionPortraitIds = { "cao_cao", "liu_bei", "sun_quan" };

    string rewardLine = "";
    string rewardItems = "";
    string gradeLine = "";
    GamePhase charReturn = GamePhase.Hub;
    int factionSel = -1;

    public bool StoryOpen => storyOpen;
    public bool Paused => Phase == GamePhase.Pause || Phase == GamePhase.Character;
    public MapDef CurrentMap => DB.Maps[Mathf.Clamp(MapIndex, 0, DB.Maps.Count - 1)];

    void Awake()
    {
        I = this;
        Settings.Load();                       // 画质/音效/键位：玩家设置优先于默认值
        DataDb.EnsureLoaded();                 // 外部数据表：有 JSON 就用，没有就用内置默认值
        Story.LoadOverrides();                 // 剧情覆盖表：活动台词不改代码就能换
        Tex.Init();

        // 界面用图：标题/结局背景 + 三阵营主公头像（网页版都有，移植时漏了）
        titleBg = Tex.LoadRaw("bg/map1_zhuojun.jpg");
        endingBg = Tex.LoadRaw("bg/map7_weihe.jpg");
        factionFace[0] = Tex.LoadRaw("ui/cao_cao.jpg");
        factionFace[1] = Tex.LoadRaw("ui/liu_bei.jpg");
        factionFace[2] = Tex.LoadRaw("ui/sun_quan.jpg");

        // 标题页立绘：抠像后的主角全身像
        playerArt = Tex.Cutout("char/player.jpg", out playerArtAspect);

        var camGo = new GameObject("MainCamera");
        camGo.transform.SetParent(transform, false);
        Cam = camGo.AddComponent<Camera>();
        camGo.AddComponent<AudioListener>();
        Cam.clearFlags = CameraClearFlags.SolidColor;
        Cam.backgroundColor = new Color(0.72f, 0.70f, 0.63f);
        Cam.nearClipPlane = 0.3f;
        Cam.farClipPlane = 220f;
        Cam.fieldOfView = 45f;
        Cam.allowHDR = false;
        camGo.AddComponent<PostFx>();   // 泛光 + 分级 + 暗角

        Sfx = new Sfx(gameObject);
        Progress = new Progress();
        Progress.Load();
        MetaUI.HubChapter = 0;
        MetaUI.ShowQuestPanel = true;
        Hud = new Hud();
        Hud.Init(this);
        Rig = new CameraRig(Cam);
    }

    void Start()
    {
        if (Progress.HasSave && Progress.Faction != null)
        {
            EnterWorld();
            Hud.Toast("野外探索 · WASD 走动，找人交谈");
        }
        else
            Phase = GamePhase.Title;
    }

    void Update()
    {
        // 设置面板开着的时候，游戏输入全部让给它
        if (SettingsUI.Open)
        {
            // 正在等玩家按新键：把按下的键交给面板
            foreach (KeyCode k in System.Enum.GetValues(typeof(KeyCode)))
            {
                if (!Input.GetKeyDown(k)) continue;
                if (SettingsUI.CaptureKey(k)) break;
            }
            if (Input.GetMouseButtonDown(0))
                SettingsUI.Click(GuiMouse(), W, CX);
            // 音量改了立刻生效
            if (Sfx != null) Sfx.RefreshVolume();
            return;
        }

        TouchCtl.Poll(Phase == GamePhase.Playing || Phase == GamePhase.Hub);
        float raw = Mathf.Min(Time.unscaledDeltaTime, 0.05f);

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (Phase == GamePhase.Playing || Phase == GamePhase.Hub) { Phase = GamePhase.Pause; return; }
            if (Phase == GamePhase.Pause)
            {
                Phase = Battle != null ? GamePhase.Playing : GamePhase.Hub;
                return;
            }
            if (Phase == GamePhase.Character) { Phase = charReturn; return; }
            if (Phase == GamePhase.Faction) { Phase = GamePhase.Title; return; }
        }

        if (Bind.Down(SettingsUI.ActPanel, KeyCode.C) && (Phase == GamePhase.Hub || Phase == GamePhase.Playing || Phase == GamePhase.Pause))
        {
            charReturn = Phase == GamePhase.Pause
                ? (Battle != null ? GamePhase.Playing : GamePhase.Hub)
                : Phase;
            Phase = GamePhase.Character;
            return;
        }

        switch (Phase)
        {
            case GamePhase.Title: TickTitle(); break;
            case GamePhase.Faction: TickFaction(); break;
            case GamePhase.Hub: TickHub(raw); break;
            case GamePhase.Character: TickCharacter(); break;
            case GamePhase.Pause: TickPause(); break;
            case GamePhase.Playing: if (Battle != null) Battle.Tick(raw); break;
            case GamePhase.Story:
                TickStory(raw);
                if (World != null && Battle == null) World.Tick(raw);
                break;
            case GamePhase.Victory:
                if (Bind.Down(SettingsUI.ActPanel, KeyCode.C)) { charReturn = GamePhase.Victory; Phase = GamePhase.Character; }
                else if (Space()) AdvanceVictory();
                break;
            case GamePhase.Defeat: TickDefeat(); break;
            case GamePhase.Ending: if (Space()) ToTitle(); break;
        }

        // 空闲时把攒着的升阶评语说出来（评语是剧情，不能和别的剧情叠着播）
        if (pendingPraise >= 0 && !storyOpen && Phase == GamePhase.Hub)
            PlayPendingPraise();
    }

    // ---------------- 流程 ----------------

    void TickTitle()
    {
        // 右下角设置
        if (Input.GetMouseButtonDown(0) && new Rect(W - 150f, 648f, 110f, 40f).Contains(GuiMouse()))
        {
            SettingsUI.OpenPanel();
            return;
        }
        if (Input.GetKeyDown(KeyCode.N)) { factionSel = -1; Phase = GamePhase.Faction; return; }
        if (!Space() && !Input.GetKeyDown(KeyCode.Return)) return;

        if (Progress.HasSave && Progress.Faction != null) EnterWorld();
        else { factionSel = -1; Phase = GamePhase.Faction; }
    }

    void TickFaction()
    {
        if (Input.GetKeyDown(KeyCode.Alpha1)) factionSel = 0;
        if (Input.GetKeyDown(KeyCode.Alpha2)) factionSel = 1;
        if (Input.GetKeyDown(KeyCode.Alpha3)) factionSel = 2;

        if (Input.GetMouseButtonDown(0))
        {
            Vector2 mp = GuiMouse();
            for (int i = 0; i < 3; i++)
                if (new Rect(CX - 400 + i * 270, 180, 250, 380).Contains(mp)) factionSel = i;

            // 定计按钮
            if (factionSel >= 0 && new Rect(CX - 90, 580, 180, 48).Contains(mp))
                ConfirmFaction();
        }

        if (factionSel >= 0 && (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space)))
            ConfirmFaction();
    }

    void ConfirmFaction()
    {
        if (factionSel < 0) return;
        string id = DB.FactionOrder[factionSel];
        Progress.Reset();
        Progress.Data.faction = id;
        Progress.GiveStarterWeapon();
        Progress.Save();
        MapIndex = 0;
        EnterWorld();
        Hud.Toast("已投" + Progress.Faction.Name + " · 野外可走动");
    }

    /// <summary>进入可走动的野外大地图（仙剑式探索）</summary>
    public void EnterWorld()
    {
        if (Battle != null) { Battle.Dispose(); Battle = null; }
        if (World == null) World = new WorldExplore(this);
        else World.RefreshSpotVisibility();
        storyOpen = false;
        Phase = GamePhase.Hub;
    }

    public void EnterBattleFromWorld(int mapIndex, QuestDef q)
    {
        if (World != null) { World.Dispose(); World = null; }
        StartMap(mapIndex, q);
    }

    /// <summary>旧收集副本结算（现以野外拾取为主，保留兼容）</summary>
    public void OnQuestCollectDone(QuestDef q)
    {
        GoHub();
        if (q != null) Hud.Toast("任务完成：" + q.Title);
    }

    /// <summary>声望入账。升阶时君主会亲自说句话 —— 这是"小人物往上走"的正反馈。</summary>
    public void OnRenownGained(int amount)
    {
        if (amount <= 0) return;
        bool up = Career.AddRenown(Progress, amount);
        Progress.Data.renownThisLevel += amount;
        if (!up)
        {
            Progress.Save();
            return;
        }

        int r = Career.RankOf(Progress);
        Progress.Data.renownMark[r] = 1;
        // 升阶奖励：铜钱 + 属性点 + 技能点
        int gold = Career.RankBonus(r);
        int attr = Career.RankAttrPoints(r);
        int sp = Career.RankSkillPoints(r);
        if (gold > 0) Progress.Data.gold += gold;
        if (attr > 0) Progress.Data.attrPts += attr;
        if (sp > 0) Progress.Data.sp += sp;
        Progress.Save();
        SyncPlayerStats();
        Hud.Toast("仕途进阶 · " + Career.Ranks[r].Title);
        // 评语不立刻播：可能正处在别的剧情里，等回到空闲界面再说话
        pendingPraise = r;
    }

    /// <summary>在空闲界面播主公评语（避免和当前剧情打架）</summary>
    void PlayPendingPraise()
    {
        if (pendingPraise < 0 || storyOpen) return;
        int r = pendingPraise;
        pendingPraise = -1;
        var praise = Career.PraiseFor(Progress);
        if (praise == null || praise.Length == 0) return;
        var f = Progress.Faction;
        string who = f != null ? f.Leader : "君主";
        var lines = new Line[]
        {
            new Line("", praise[0]),
            new Line(who, r >= 3 ? "往后的路，你我一同走。" : "还早得很，但路是对的。",
                     f != null ? "npc/" + f.Id + ".jpg" : null),
        };
        PlayStory(lines, null);
    }

    void TickHub(float raw)
    {
        if (World == null) EnterWorld();
        World.Tick(raw);

        if (Input.GetMouseButtonDown(0))
        {
            int hit = World.ClickUi(GuiMouse(), W);
            if (hit == 1)
            {
                charReturn = GamePhase.Hub;
                Phase = GamePhase.Character;
            }
        }
    }

    void TickCharacter()
    {
        if (!Input.GetMouseButtonDown(0)) return;
        int hit = MetaUI.CharacterClick(this, GuiMouse(), CX);
        if (hit == -2) { Phase = charReturn; return; }
        if (hit == -3) { MetaUI.CharTab = 0; return; }
        if (hit == -4) { MetaUI.CharTab = 1; return; }
        if (hit == -5) { MetaUI.CharTab = 2; return; }
        if (hit >= 10 && hit < 13)
        {
            if (Progress.UpgradeEquip(Items.SlotIds[hit - 10], out string msg))
            {
                Progress.Save();
                SyncPlayerStats();
            }
            Hud.Toast(msg);
        }
        else if (hit >= 20 && hit < 23)
        {
            if (Progress.UpgradeSkill(hit - 20))
            {
                Progress.Save();
                Hud.Toast(DB.Skills[hit - 20].Name + " 升至 " + Progress.Data.skillLv[hit - 20] + " 级");
            }
            else Hud.Toast("无法升级");
        }
        else if (hit >= 30 && hit < 33)
        {
            if (Progress.SpendAttr(hit - 30))
            {
                Progress.Save();
                SyncPlayerStats();
                Hud.Toast("属性已分配");
            }
            else Hud.Toast("没有属性点");
        }
        else if (hit >= 40 && hit < 40 + Talents.Nodes.Length)
        {
            int ti = hit - 40;
            if (Talents.Unlock(Progress, ti))
            {
                Progress.Save();
                SyncPlayerStats();
                Hud.Toast(Talents.Nodes[ti].Name + " 升至 " + Talents.Rank(Progress, ti) + " 阶");
            }
            else Hud.Toast("无法点亮该天赋");
        }
        else if (hit >= 100)
        {
            int bi = hit - 100;
            if (bi >= 0 && bi < Progress.Data.bag.Count)
            {
                Progress.EquipItem(Progress.Data.bag[bi]);
                Progress.Save();
                SyncPlayerStats();
                Hud.Toast("已换装");
            }
        }
    }

    void SyncPlayerStats()
    {
        if (Battle == null || Battle.Player == null) return;
        var st = Progress.Stats();
        Battle.Player.Stats = st;
        Battle.Player.MaxHP = st.MaxHP;
        if (Battle.Player.HP > st.MaxHP) Battle.Player.HP = st.MaxHP;
    }

    void TickPause()
    {
        if (!Input.GetMouseButtonDown(0)) return;
        int hit = MetaUI.PauseClick(GuiMouse(), CX);
        if (hit == 0) Phase = Battle != null ? GamePhase.Playing : GamePhase.Hub;
        else if (hit == 1)
        {
            charReturn = Battle != null ? GamePhase.Playing : GamePhase.Hub;
            Phase = GamePhase.Character;
        }
        else if (hit == 2)
        {
            SettingsUI.OpenPanel();
        }
        else if (hit == 3)
        {
            if (Battle != null) GoHub();
            else ToTitle();
        }
    }

    void TickDefeat()
    {
        if (!Input.GetMouseButtonDown(0) && !Input.GetKeyDown(KeyCode.Space) && !Input.GetKeyDown(KeyCode.Return)) return;
        Vector2 mp = GuiMouse();
        if (new Rect(CX - 200, 360, 180, 48).Contains(mp) || Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return))
            StartMap(MapIndex);
        else if (new Rect(CX + 20, 360, 180, 48).Contains(mp))
            GoHub();
    }

    Vector2 GuiMouse()
    {
        float s = S;
        return new Vector2(Input.mousePosition.x / s, (Screen.height - Input.mousePosition.y) / s);
    }

    public void GoHub()
    {
        if (Battle != null) { Battle.Dispose(); Battle = null; }
        storyOpen = false;
        EnterWorld();
        Hud.Toast("返回野外");
    }

    void ToTitle()
    {
        if (Battle != null) { Battle.Dispose(); Battle = null; }
        if (World != null) { World.Dispose(); World = null; }
        storyOpen = false;
        storyLines = null;
        storyDone = null;
        Phase = GamePhase.Title;
    }

    public void StartMap(int index, QuestDef battleQuest = null)
    {
        if (Battle != null) { Battle.Dispose(); Battle = null; }
        if (World != null) { World.Dispose(); World = null; }
        MapIndex = Mathf.Clamp(index, 0, DB.Maps.Count - 1);
        Cam.backgroundColor = CurrentMap.Fog;

        Battle = new Battle(this, CurrentMap, battleQuest);
        Battle.Player.MaxHP = Progress.Stats().MaxHP;

        var intro = battleQuest?.Lines ?? CurrentMap.Intro;
        PlayStory(intro, () =>
        {
            Phase = GamePhase.Playing;
            Hud.Toast(CurrentMap.Subtitle);
        });
    }

    int FirstUncleared()
    {
        for (int i = 0; i < DB.Maps.Count; i++)
            if (!Progress.Data.cleared[i]) return i;
        return DB.Maps.Count - 1;
    }

    public void PlayStory(Line[] lines, Action done)
    {
        if (lines == null || lines.Length == 0)
        {
            Phase = Battle != null ? GamePhase.Playing : GamePhase.Hub;
            done?.Invoke();
            return;
        }
        storyLines = lines;
        storyIdx = 0;
        storyT = 0f;
        storyDone = done;
        storyOpen = true;
        Phase = GamePhase.Story;
    }

    void TickStory(float raw)
    {
        storyT += raw;
        if (storyT < 0.22f) return;
        var line = CurrentLine();
        if (line != null && line.Choices != null && line.Choices.Length > 0)
        {
            if (!Input.GetMouseButtonDown(0)) return;
            int hit = ChoiceHit(GuiMouse());
            if (hit >= 0) PickChoice(line.Choices[hit]);
            return;
        }
        if (!Space()) return;

        storyT = 0f;
        storyIdx++;
        if (storyIdx >= storyLines.Length) CloseStory();
    }

    Line CurrentLine()
    {
        if (storyLines == null || storyIdx < 0 || storyIdx >= storyLines.Length) return null;
        return storyLines[storyIdx];
    }

    int ChoiceHit(Vector2 mp)
    {
        var line = CurrentLine();
        if (line?.Choices == null) return -1;
        float bodyW;
        StoryBody(line, out bodyW, out _);
        for (int i = 0; i < line.Choices.Length; i++)
        {
            if (ChoiceRect(i, bodyW).Contains(mp)) return i;
        }
        return -1;
    }

    Texture2D StoryBody(Line line, out float width, out float height)
    {
        width = 0f;
        height = 0f;
        if (line == null || string.IsNullOrEmpty(line.Img)) return null;
        float asp;
        var body = Tex.Cutout(LeaderArt(line.Img), out asp);
        if (body == null || body == Tex.White || body.height <= 8) return null;
        height = 560f;
        width = height * Mathf.Max(0.2f, asp);
        if (width > 420f)
        {
            width = 420f;
            height = width / Mathf.Max(0.2f, asp);
        }
        return body;
    }

    /// <summary>
    /// 主公立绘随官阶变化：到「柱石」以上，君主换一身倚重你的行头。
    /// （紫袍/赭金的 *_trust 版本由 Tools/gen_npc_variants.py 调色生成）
    /// </summary>
    public string LeaderArt(string path)
    {
        if (string.IsNullOrEmpty(path)) return path;
        if (Career.RankOf(Progress) < 4) return path;   // 柱石起才换
        if (path == "npc/cao_cao.jpg") return "npc/cao_cao_trust.jpg";
        if (path == "npc/liu_bei.jpg") return "npc/liu_bei_trust.jpg";
        return path;
    }

    Rect ChoiceRect(int i, float bodyW)
    {
        float left = bodyW > 8f ? 40f + bodyW + 18f : 80f;
        float w = Mathf.Min(640f, W - left - 40f);
        return new Rect(left, 448f + i * 46f, Mathf.Max(220f, w), 40f);
    }

    void PickChoice(Choice c)
    {
        if (c == null) return;
        if (!string.IsNullOrEmpty(c.Flag))
        {
            // Flags.Set 会顶掉同维度的旧旗号（重刷副本改主意）
            Flags.Set(Progress, c.Flag);
            Progress.Save();
            var fd = Flags.Get(c.Flag);
            if (fd != null) Hud.Toast("旗号 · " + fd.Title);
        }
        var list = new List<Line>();
        for (int i = 0; i <= storyIdx; i++) list.Add(storyLines[i]);
        if (c.Follow != null)
            for (int i = 0; i < c.Follow.Length; i++) list.Add(c.Follow[i]);
        for (int i = storyIdx + 1; i < storyLines.Length; i++) list.Add(storyLines[i]);
        storyLines = list.ToArray();
        storyT = 0f;
        storyIdx++;
        if (storyIdx >= storyLines.Length) CloseStory();
    }

    void CloseStory()
    {
        storyLines = null;
        storyIdx = 0;
        storyOpen = false;
        Phase = Battle != null ? GamePhase.Playing : GamePhase.Hub;
        var done = storyDone;
        storyDone = null;
        done?.Invoke();
    }

    public void OnBattleVictory(Battle b)
    {
        rewardLine = $"经验 +{b.RewardExp}    铜钱 +{b.RewardGold}";
        rewardItems = "";
        for (int i = 0; i < b.ItemRewards.Count; i++)
        {
            var it = b.ItemRewards[i];
            rewardItems += (i > 0 ? "\n" : "") + "【" + Items.RarityNames[it.rarity] + "】" + Items.DisplayName(it) + "  " + Items.Desc(it);
        }
        gradeLine = BattleGrade(b);
        b.ClaimRewards();
        Progress.Data.cleared[MapIndex] = true;
        var bq = b.ActiveQuest ?? Quests.BattleQuestForMap(MapIndex);
        if (bq != null) Quests.Complete(Progress, bq);
        else Progress.Save();
        Phase = GamePhase.Victory;
    }

    /// <summary>
    /// 战后评级：按剩余血量比例给一档评语 + 少量额外奖励。
    /// 「险胜」给得最多 —— 鼓励玩家敢冒险、敢重刷练胆，而不是一次就刷满。
    /// </summary>
    string BattleGrade(Battle b)
    {
        if (b == null || b.Player == null) return "";
        float ratio = b.Player.MaxHP > 0f ? Mathf.Clamp01(b.Player.HP / b.Player.MaxHP) : 0f;
        string grade, note;
        int gold = 0, renown = 0;
        if (ratio >= 0.8f) { grade = "毫发无伤"; note = "这一战赢得干净 —— 兵刃都还没卷刃。"; gold = 30; }
        else if (ratio >= 0.5f) { grade = "从容破敌"; note = "还有大半气力留着，够再打一场。"; gold = 15; }
        else if (ratio >= 0.25f) { grade = "浴血而出"; note = "身上添了伤，但步子没乱。"; gold = 8; }
        else { grade = "险胜"; note = "几乎是爬着回来的 —— 命大，也是命硬。"; gold = 40; renown = 5; }
        if (gold > 0) Progress.Data.gold += gold;
        if (renown > 0) OnRenownGained(renown);
        return $"战后评级　{grade}　铜钱 +{gold}　{note}";
    }

    void AdvanceVictory()
    {
        var map = CurrentMap;
        PlayStory(map.Outro, () =>
        {
            if (MapIndex >= DB.Maps.Count - 1 && Progress.Data.cleared[MapIndex])
                Phase = GamePhase.Ending;
            else
                GoHub();
        });
    }

    public void OnPlayerDefeat()
    {
        Phase = GamePhase.Defeat;
    }

    static bool Space() => Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return) || Input.GetMouseButtonDown(0);

    // ---------------- OnGUI ----------------

    float S => Screen.height / 720f * (Settings.Current != null ? Settings.Current.UiScale : 1f);
    float CX => Screen.width / S * 0.5f;
    float W => Screen.width / S;

    void OnGUI()
    {
        if (Hud == null) return;
        GUI.skin.font = Hud.Font;
        GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(S, S, 1f));

        switch (Phase)
        {
            case GamePhase.Title: DrawTitle(); break;
            case GamePhase.Faction: DrawFaction(); break;
            case GamePhase.Hub:
                if (World != null) World.DrawOverlay(W, CX, Label, Fill);
                break;
            case GamePhase.Character: MetaUI.DrawCharacter(this, W, CX, Label, Fill); break;
            default:
                if (Battle != null) Hud.Draw(Battle);
                else if (World != null) World.DrawOverlay(W, CX, Label, Fill);
                else if (Phase == GamePhase.Story)
                    Fill(new Rect(0, 0, W, 720), new Color(0.05f, 0.045f, 0.04f, 0.96f));
                GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(S, S, 1f));
                if (Phase == GamePhase.Story) DrawStory();
                if (Phase == GamePhase.Victory) DrawVictory();
                if (Phase == GamePhase.Defeat) DrawDefeat();
                if (Phase == GamePhase.Ending) DrawEnding();
                if (Phase == GamePhase.Pause) MetaUI.DrawPause(W, CX, Label, Fill);
                break;
        }

        GUI.matrix = Matrix4x4.identity;
        GUI.color = Color.white;

        // 触屏摇杆与技能按钮：屏幕像素坐标系，压在一切界面之上
        if (TouchCtl.Active && Phase == GamePhase.Playing && !storyOpen)
            Hud.DrawTouchControls();

        // 设置面板：压在最上层
        if (SettingsUI.Open)
        {
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(S, S, 1f));
            SettingsUI.Draw(W, CX, Label, Fill, null);
            GUI.matrix = Matrix4x4.identity;
            GUI.color = Color.white;
        }

        // 帧率显示
        if (Settings.Current != null && Settings.Current.ShowFps)
        {
            GUI.matrix = Matrix4x4.identity;
            GUI.color = Color.white;
            float fps = 1f / Mathf.Max(Time.unscaledDeltaTime, 0.0001f);
            GUI.Label(new Rect(8, 4, 220, 22), fps.ToString("0") + " FPS",
                new GUIStyle(GUI.skin.label) { fontSize = 14, normal = { textColor = new Color(0.9f, 0.88f, 0.8f) } });
        }
    }

    void DrawTitle()
    {
        // 背景：涿郡水墨长卷
        if (titleBg != null && titleBg != Tex.White)
        {
            GUI.DrawTexture(new Rect(0, 0, W, 720), titleBg, ScaleMode.ScaleAndCrop);
            Fill(new Rect(0, 0, W, 720), new Color(0.05f, 0.045f, 0.04f, 0.38f));
        }
        else
        {
            Fill(new Rect(0, 0, W, 720), new Color(0.09f, 0.08f, 0.07f, 1f));
        }

        // 右侧主角立绘（抠像后的全身像，压在标题右侧）
        if (playerArt != null && playerArt != Tex.White)
        {
            float h = 600f;
            float w = h * playerArtAspect;
            GUI.DrawTexture(new Rect(W - w * 0.66f, 720f - h + 26f, w, h), playerArt, ScaleMode.ScaleToFit);
        }

        // 左侧竖排压暗，给标题文字留出可读的底
        Fill(new Rect(0, 0, W * 0.60f, 720), new Color(0.04f, 0.035f, 0.03f, 0.58f));
        Fill(new Rect(W * 0.60f - 2f, 120f, 3f, 470f), new Color(0.80f, 0.63f, 0.33f, 0.85f));

        const float lx = 96f;
        Label(new Rect(lx, 148f, 640f, 84f), "三国 · 乱世行", 58, new Color(0.96f, 0.89f, 0.72f), TextAnchor.MiddleLeft);
        Label(new Rect(lx, 236f, 640f, 30f), "一个不属于这个世道的刀客", 21, new Color(0.84f, 0.79f, 0.70f), TextAnchor.MiddleLeft);
        Label(new Rect(lx, 282f, 620f, 96f),
            "你是不属于这个时代的人。\n一柄刀，一身布衣，从涿郡的桃花里醒来。",
            17, new Color(0.74f, 0.70f, 0.63f), TextAnchor.UpperLeft, true);

        string hint;
        if (Progress.HasSave && Progress.Faction != null)
            hint = $"空格 进入野外（{Progress.Faction.Name} · {Progress.Data.level} 级）    N 重选阵营";
        else
            hint = "空格 择主出征";
        Label(new Rect(lx, 470f, 620f, 34f), hint, 22, new Color(0.98f, 0.84f, 0.48f), TextAnchor.MiddleLeft);
        Label(new Rect(lx, 516f, 640f, 30f),
            "四地探索 · 对话选项 · 副本战斗 · " + SettingsUI.KeyName(Bind.Panel) + " 天赋 · "
            + SettingsUI.KeyName(Bind.Map) + " 天下",
            15, new Color(0.66f, 0.63f, 0.58f), TextAnchor.MiddleLeft);

        // 右下角设置入口
        Rect sb = new Rect(W - 150f, 648f, 110f, 40f);
        Fill(sb, new Color(0.16f, 0.14f, 0.11f, 0.85f));
        Label(sb, "设 置", 18, new Color(0.90f, 0.84f, 0.70f), TextAnchor.MiddleCenter);
    }

    void DrawFaction()
    {
        if (titleBg != null && titleBg != Tex.White)
        {
            GUI.DrawTexture(new Rect(0, 0, W, 720), titleBg, ScaleMode.ScaleAndCrop);
            Fill(new Rect(0, 0, W, 720), new Color(0.04f, 0.04f, 0.035f, 0.70f));
        }
        else
        {
            Fill(new Rect(0, 0, W, 720), new Color(0.07f, 0.065f, 0.06f, 1f));
        }
        Label(new Rect(CX - 400, 84, 800, 50), "择一而事", 40, new Color(0.95f, 0.88f, 0.72f), TextAnchor.MiddleCenter);
        Label(new Rect(CX - 400, 132, 800, 26), "乱世之中，先要有一面旗", 16, new Color(0.74f, 0.71f, 0.64f), TextAnchor.MiddleCenter);

        for (int i = 0; i < DB.FactionOrder.Length; i++)
        {
            var f = DB.Factions[DB.FactionOrder[i]];
            Rect card = new Rect(CX - 400 + i * 270, 180, 250, 380);
            Fill(card, new Color(0.09f, 0.08f, 0.07f, 0.92f));
            Fill(new Rect(card.x, card.y, card.width, 4), f.Color);

            // 主公立绘：取顶部横幅铺满卡面上半部
            if (factionFace[i] != null && factionFace[i] != Tex.White)
            {
                var band = Tex.PortraitBand("ui/" + FactionPortraitIds[i] + ".jpg");
                if (band != null && band != Tex.White)
                    GUI.DrawTexture(new Rect(card.x + 8, card.y + 10, card.width - 16, 150), band, ScaleMode.ScaleAndCrop);
                else
                    GUI.DrawTexture(new Rect(card.x + 8, card.y + 10, card.width - 16, 150), factionFace[i], ScaleMode.ScaleAndCrop);
            }

            Label(new Rect(card.x, card.y + 164, card.width, 50), f.Name, 40, f.Color, TextAnchor.MiddleCenter);
            Label(new Rect(card.x, card.y + 216, card.width, 24), $"{f.Leader} · {f.Title}", 14,
                new Color(0.82f, 0.78f, 0.7f), TextAnchor.MiddleCenter);

            // 武将被动：图标 + 名称 + 说明
            Fill(new Rect(card.x + 14, card.y + 248, card.width - 28, 1), new Color(0.78f, 0.62f, 0.32f, 0.4f));
            GUI.color = f.Color;
            GUI.DrawTexture(new Rect(card.x + 16, card.y + 258, 22, 22), Tex.IconHeavy);
            GUI.color = Color.white;
            Label(new Rect(card.x + 44, card.y + 254, card.width - 56, 26), f.PassiveName, 17,
                new Color(0.98f, 0.9f, 0.7f), TextAnchor.MiddleLeft);
            Label(new Rect(card.x + 14, card.y + 286, card.width - 28, 36), f.PassiveDesc, 13,
                new Color(0.8f, 0.77f, 0.7f), TextAnchor.MiddleCenter, true);
            Label(new Rect(card.x, card.y + 322, card.width, 22), "起手 · " + f.StartWeapon, 13,
                new Color(0.72f, 0.7f, 0.64f), TextAnchor.MiddleCenter);
            string tip = factionSel == i ? "已选 · 按空格定计" : ("按 " + (i + 1) + " 或点选");
            Label(new Rect(card.x, card.y + 344, card.width, 28), tip, 18,
                factionSel == i ? new Color(1f, 0.9f, 0.55f) : new Color(0.95f, 0.8f, 0.45f), TextAnchor.MiddleCenter);
            if (factionSel == i)
                Fill(new Rect(card.x, card.y, 4, card.height), f.Color);
        }

        Rect confirm = new Rect(CX - 90, 580, 180, 48);
        Fill(confirm, factionSel >= 0 ? new Color(0.22f, 0.18f, 0.12f, 0.95f) : new Color(0.12f, 0.11f, 0.1f, 0.7f));
        Label(confirm, factionSel >= 0 ? "定 计" : "请先择主", 20,
            factionSel >= 0 ? new Color(0.98f, 0.88f, 0.55f) : new Color(0.55f, 0.52f, 0.48f), TextAnchor.MiddleCenter);
    }

    void DrawStory()
    {
        if (storyLines == null || storyIdx >= storyLines.Length) return;
        var line = storyLines[storyIdx];

        // 说话的人站在画面左侧，对话框让到他右边，不再用头像牌子。
        float bodyW, bodyH;
        var body = StoryBody(line, out bodyW, out bodyH);
        if (body != null)
            GUI.DrawTexture(new Rect(24f, 700f - bodyH, bodyW, bodyH), body, ScaleMode.ScaleToFit);

        float left = bodyW > 8f ? 40f + bodyW + 18f : 80f;
        Rect panel = new Rect(left, 548, Mathf.Max(280f, W - left - 36f), 140);
        Fill(panel, new Color(0.04f, 0.035f, 0.03f, 0.88f));
        Fill(new Rect(panel.x, panel.y, 3, panel.height), new Color(0.78f, 0.62f, 0.32f));

        float tx = panel.x + 22f;
        float tw = panel.width - 44f;

        if (!string.IsNullOrEmpty(line.Who))
            Label(new Rect(tx, panel.y + 10, 200, 26), line.Who, 20,
                new Color(0.98f, 0.84f, 0.5f), TextAnchor.MiddleLeft);

        Label(new Rect(tx, panel.y + 42, tw, 66), line.Text, 18,
            new Color(0.92f, 0.89f, 0.82f), TextAnchor.UpperLeft, true);

        if (line.Choices != null && line.Choices.Length > 0)
        {
            for (int i = 0; i < line.Choices.Length; i++)
            {
                Rect r = ChoiceRect(i, bodyW);
                Fill(r, new Color(0.12f, 0.1f, 0.08f, 0.92f));
                Fill(new Rect(r.x, r.y, 4, r.height), new Color(0.85f, 0.7f, 0.38f));
                Label(r, line.Choices[i].Text, 17, new Color(0.98f, 0.9f, 0.68f), TextAnchor.MiddleCenter);
            }
            Label(new Rect(panel.x + panel.width - 200, panel.y + panel.height - 26, 180, 20),
                "点击选项", 13, new Color(0.68f, 0.65f, 0.58f), TextAnchor.MiddleRight);
        }
        else
        {
            Label(new Rect(panel.x + panel.width - 160, panel.y + panel.height - 26, 140, 20),
                $"{storyIdx + 1}/{storyLines.Length}  空格继续", 13,
                new Color(0.68f, 0.65f, 0.58f), TextAnchor.MiddleRight);
        }
    }

    void DrawVictory()
    {
        Fill(new Rect(0, 0, W, 720), new Color(0.03f, 0.03f, 0.025f, 0.72f));
        Label(new Rect(CX - 400, 140, 800, 70), "破 敌", 52, new Color(0.96f, 0.84f, 0.55f), TextAnchor.MiddleCenter);
        Label(new Rect(CX - 400, 216, 800, 28), CurrentMap.Ending, 22, new Color(0.9f, 0.86f, 0.76f), TextAnchor.MiddleCenter);
        Label(new Rect(CX - 400, 262, 800, 28), rewardLine, 20, new Color(0.86f, 0.82f, 0.72f), TextAnchor.MiddleCenter);
        if (!string.IsNullOrEmpty(gradeLine))
            Label(new Rect(CX - 400, 300, 800, 28), gradeLine, 17, new Color(0.9f, 0.82f, 0.6f), TextAnchor.MiddleCenter);
        if (!string.IsNullOrEmpty(rewardItems))
            Label(new Rect(CX - 400, 344, 800, 110), rewardItems, 16, new Color(0.85f, 0.8f, 0.68f), TextAnchor.UpperCenter, true);
        Label(new Rect(CX - 400, 560, 800, 28), "空格 回世界地图", 18, new Color(0.85f, 0.82f, 0.74f), TextAnchor.MiddleCenter);
        Label(new Rect(CX - 400, 592, 800, 24), "C 查看人物 / 加点天赋", 15, new Color(0.72f, 0.68f, 0.6f), TextAnchor.MiddleCenter);
    }

    void DrawDefeat()
    {
        Fill(new Rect(0, 0, W, 720), new Color(0.10f, 0.02f, 0.02f, 0.7f));
        Label(new Rect(CX - 400, 240, 800, 70), "身 死 道 消", 46, new Color(0.9f, 0.5f, 0.42f), TextAnchor.MiddleCenter);
        Fill(new Rect(CX - 200, 360, 180, 48), new Color(0.18f, 0.12f, 0.1f, 0.95f));
        Label(new Rect(CX - 200, 360, 180, 48), "再 战", 20, new Color(0.95f, 0.82f, 0.7f), TextAnchor.MiddleCenter);
        Fill(new Rect(CX + 20, 360, 180, 48), new Color(0.14f, 0.12f, 0.1f, 0.95f));
        Label(new Rect(CX + 20, 360, 180, 48), "回地图", 20, new Color(0.9f, 0.84f, 0.72f), TextAnchor.MiddleCenter);
    }

    void DrawEnding()
    {
        if (endingBg != null) GUI.DrawTexture(new Rect(0, 0, W, 720), endingBg, ScaleMode.ScaleAndCrop);
        Fill(new Rect(0, 0, W, 720), new Color(0.03f, 0.03f, 0.025f, 0.66f));

        var f = Progress.Faction;
        string leader = f != null ? f.Leader : "主公";
        int rank = Career.RankOf(Progress);
        var rd = Career.Ranks[rank];

        // 结局按官阶分：站得越高，和君主走得越近
        string title, body;
        if (rank >= 5)
        {
            title = "天下 · 与君同席";
            body = $"{leader}在朝堂上念到你名字的时候，满座寂然。\n" +
                   $"一个从桃林里走出来的布衣，如今能与他对坐论天下。\n" +
                   "河北已平，关中已定，三分之势已成 —— 而这一局棋，已经有你的一半。";
        }
        else if (rank >= 4)
        {
            title = "柱石 · 国之干城";
            body = $"{leader}没有再唤你的名字，而是唤你的职衔。\n" +
                   "河北既定，关中已降，四方却还盯着这个位置。\n" +
                   "你不是最能打的那个，但谁都动不了你守着的这条线。";
        }
        else if (rank >= 3)
        {
            title = "将军 · 独当一面";
            body = $"一纸诏书交到你手上，是独领一军的印信。\n" +
                   $"{leader}说：这一路，你替孤守住。\n" +
                   "天下还三分，而你已经能替一个人守半壁。";
        }
        else
        {
            title = "江湖 · 各有前程";
            body = $"渭水的冰化了，帐中的位置却还轮不到你。\n" +
                   $"{leader}仍在帐中议事，而你站在帐外。\n" +
                   "乱世未定 —— 但你已经不再是那个无名之辈了。";
        }

        // 旗号：这一路自己选出来的分支，叠在官阶之上
        string path = Flags.RoadText(Progress);
        var sb = new System.Text.StringBuilder();
        var taken = Flags.AllTaken(Progress);
        // 主线旗号优先，再补支线留下的痕迹
        for (int pass = 0; pass < 2; pass++)
        {
            for (int i = 0; i < taken.Count; i++)
            {
                bool core = taken[i].Core;
                if ((pass == 0) != core) continue;
                if (string.IsNullOrEmpty(taken[i].Ending)) continue;
                if (sb.Length > 0) sb.Append("\n");
                sb.Append(taken[i].Ending);
            }
        }
        string flagText = sb.ToString();

        Label(new Rect(CX - 420, 72, 840, 58), title, 42, new Color(0.97f, 0.88f, 0.70f), TextAnchor.MiddleCenter);
        Label(new Rect(CX - 400, 132, 800, 24),
            $"最终官阶 · {rd.Title}", 18, new Color(0.98f, 0.84f, 0.5f), TextAnchor.MiddleCenter);

        // 你走的这条路
        Label(new Rect(CX - 420, 160, 840, 24),
            string.IsNullOrEmpty(path) ? "你走的这条路 —— 尚无旗号" : "你走的这条路　" + path,
            16, new Color(0.72f, 0.80f, 0.88f), TextAnchor.MiddleCenter);

        Label(new Rect(CX - 420, 192, 840, 108), body, 18, new Color(0.87f, 0.83f, 0.75f), TextAnchor.UpperCenter, true);

        // 每个旗号补一句：让玩家看见自己的每次选择都留在了这条路上
        if (!string.IsNullOrEmpty(flagText))
            Label(new Rect(CX - 420, 306, 840, 126), flagText, 15, new Color(0.76f, 0.78f, 0.74f), TextAnchor.UpperCenter, true);

        // 一生小结
        var d = Progress.Data;
        string stat = $"声望 {d.renown}　斩敌 {d.totalKills}　等级 {d.level}　官阶 {rd.Title}";
        Label(new Rect(CX - 420, 440, 840, 26), stat, 17, new Color(0.82f, 0.78f, 0.68f), TextAnchor.MiddleCenter);

        // 升阶评语回顾
        var praise = Career.PraiseFor(Progress);
        if (praise != null && praise.Length > 0)
            Label(new Rect(CX - 420, 472, 840, 56), "「" + praise[0] + "」", 18,
                new Color(0.92f, 0.86f, 0.70f), TextAnchor.UpperCenter, true);

        Label(new Rect(CX - 400, 600, 800, 28), "空格 回到标题", 18, new Color(0.8f, 0.77f, 0.7f), TextAnchor.MiddleCenter);
    }

    // ---------------- GUI 小工具 ----------------

    static void Fill(Rect r, Color c)
    {
        GUI.color = c;
        GUI.DrawTexture(r, Tex.White);
        GUI.color = Color.white;
    }

    static void Label(Rect r, string text, int size, Color color, TextAnchor anchor, bool wrap = false)
    {
        var style = new GUIStyle(GUI.skin.label)
        {
            fontSize = size,
            alignment = anchor,
            wordWrap = wrap,
            richText = false,
        };
        style.normal.textColor = color;
        GUI.Label(r, text, style);
    }
}
