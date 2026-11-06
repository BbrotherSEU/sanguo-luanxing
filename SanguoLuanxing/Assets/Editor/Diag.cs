using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 临时诊断工具：批量模式跑一遍，输出「地面/朝向/界面」实拍截图。
/// 用完即删，不属于游戏本体。
/// </summary>
public static class Diag
{
    const string Dir = @"C:\Users\FanBin\Documents\trae_projects\.diag";

    [MenuItem("Diag/Run")]
    public static void RunAll()
    {
        Directory.CreateDirectory(Dir);
        try { EditMode(); }
        catch (Exception e) { Debug.LogError("[Diag] edit-mode failed: " + e); }
        EditorApplication.update += PlayModeTick;
        EditorApplication.playModeStateChanged += OnPlayMode;
        EditorApplication.EnterPlaymode();
    }

    // ---------------- 编辑模式：渲染 3D 场景 ----------------

    static void EditMode()
    {
        Tex.Init();

        var camGo = new GameObject("DiagCam");
        var cam = camGo.AddComponent<Camera>();
        cam.fieldOfView = 45f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.72f, 0.70f, 0.63f);
        cam.nearClipPlane = 0.3f;
        cam.farClipPlane = 220f;
        camGo.AddComponent<PostFx>();   // 与游戏相机一致，截图含后处理

        var map = DB.Maps[2];   // 长坂坡：金色主题，与实机反馈的地图一致
        var arena = Arena.Build(map, camGo.transform);

        float asp;
        var tex = Tex.Cutout("char/player.jpg", out asp);
        var pgo = new GameObject("player");
        var sr = Tex.Actor(pgo.transform, Tex.SpriteFor(tex, 2.6f), 0);
        sr.sortingOrder = Util.SortOrder(0f, 0);
        pgo.transform.position = new Vector3(0f, 0f, 0f);

        var ego = new GameObject("enemy");
        var etex = Tex.Cutout("char/bandit.jpg", out asp);
        var esr = Tex.Actor(ego.transform, Tex.SpriteFor(etex, 2.25f), 0);
        esr.sortingOrder = Util.SortOrder(-4f, 0);
        ego.transform.position = new Vector3(2.2f, 0f, -4f);

        Place(cam, new Vector3(0f, 0f, 15f));
        Shoot(cam, "edit_battle");

        // 朝向检查：世界 +X 落在屏幕哪一侧
        var sp = cam.WorldToScreenPoint(new Vector3(1f, 0f, 0f));
        float center = 1280f * 0.5f;
        Debug.Log($"[Diag] 世界+X → 屏幕x={sp.x:0.0} (中心 {center}) ⇒ {(sp.x < center ? "屏幕左" : "屏幕右")}");
        Debug.Log($"[Diag] 世界+Z → 屏幕y={cam.WorldToScreenPoint(new Vector3(0f, 0f, 1f)).y:0.0} (720) ⇒ " +
                  $"{(cam.WorldToScreenPoint(new Vector3(0f, 0f, 1f)).y < 360f ? "屏幕下" : "屏幕上")}");

        // 贴图检查
        CheckTex("bg/map1_zhuojun.jpg");
        CheckTex("ui/cao_cao.jpg");
        CheckTex("tex/ground_earth.jpg");
        Debug.Log($"[Diag] 图标 IconQ={Tex.IconQ != null} IconE={Tex.IconE != null} IconR={Tex.IconR != null} Patch={Tex.GroundPatch != null}");
    }

    static void Place(Camera cam, Vector3 follow)
    {
        // 与实机探索相机一致：高 10.2、后撤 6、略朝下看
        cam.transform.position = new Vector3(follow.x, 10.2f, follow.z + 6f);
        cam.transform.LookAt(new Vector3(follow.x, 0.4f, follow.z - 4f));
    }

    static void CheckTex(string rel)
    {
        var t = Tex.LoadRaw(rel);
        Debug.Log($"[Diag] {rel} → {t.width}x{t.height} {(t == Tex.White ? "!! 加载失败" : "ok")}");
    }

    static void Shoot(Camera cam, string name)
    {
        var rt = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32);
        cam.targetTexture = rt;
        cam.Render();
        var prev = RenderTexture.active;
        RenderTexture.active = rt;
        var tex = new Texture2D(1280, 720, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
        tex.Apply();
        File.WriteAllBytes(Path.Combine(Dir, name + ".png"), tex.EncodeToPNG());
        RenderTexture.active = prev;
        cam.targetTexture = null;
        Debug.Log($"[Diag] 写出 {name}.png");
    }

    // ---------------- 播放模式：抓界面 ----------------

    static int stage;
    static int frame;

    static void OnPlayMode(PlayModeStateChange s)
    {
        if (s == PlayModeStateChange.EnteredPlayMode) { stage = 1; frame = 0; }
        else if (s == PlayModeStateChange.EnteredEditMode)
        {
            EditorApplication.update -= PlayModeTick;
            EditorApplication.playModeStateChanged -= OnPlayMode;
            Debug.Log("[Diag] 播放模式结束");
            EditorApplication.Exit(0);
        }
    }

    /// <summary>调玩家私有方法触发出招（StartLight / StartHeavy）</summary>
    static void FireAttack(Game g, string method, float arg)
    {
        var p = g.Battle.Player;
        var mi = typeof(PlayerCtrl).GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance);
        if (mi == null) { Debug.LogWarning("[Diag] 找不到 " + method); return; }
        mi.Invoke(p, mi.GetParameters().Length == 0 ? null : new object[] { arg });
        Debug.Log("[Diag] 触发 " + method);
    }

    static void PlayModeTick()
    {    frame++;
        var g = Game.I;
        if (g == null) return;

        if (stage == 1 && frame > 12)
        {
            ScreenCapture.CaptureScreenshot(Path.Combine(Dir, "ui_title.png"));
            Debug.Log("[Diag] 抓标题页");
            g.Phase = GamePhase.Faction;
            stage = 2; frame = 0;
        }
        else if (stage == 2 && frame > 8)
        {
            ScreenCapture.CaptureScreenshot(Path.Combine(Dir, "ui_faction.png"));
            Debug.Log("[Diag] 抓阵营页");
            g.StartMap(0);
            var mi = typeof(Game).GetMethod("CloseStory", BindingFlags.NonPublic | BindingFlags.Instance);
            mi?.Invoke(g, null);
            g.Battle.Player.Pos = new Vector3(0f, 0f, 8f);
            stage = 3; frame = 0;
        }
        else if (stage == 3 && frame > 25)
        {
            // 打一下：验证命中特效与顿帧
            var b = g.Battle;
            b.SpawnWave(new[] { "bandit" }, 2, 1.2f, 6.5f);
            b.HitArc(new Vector3(0f, 0f, 8f), 4f, Mathf.PI, 1f,
                new HitOpts { Aim = 0f, Knock = 4f });
            stage = 4; frame = 0;
        }
        else if (stage == 4 && frame > 4)
        {
            ScreenCapture.CaptureScreenshot(Path.Combine(Dir, "ui_hit.png"));
            Debug.Log("[Diag] 抓命中帧");
            stage = 5; frame = 0;
        }
        else if (stage == 5 && frame > 6)
        {
            ScreenCapture.CaptureScreenshot(Path.Combine(Dir, "ui_battle.png"));
            Debug.Log("[Diag] 抓战斗页");
            // 出招动作验证：直接调 StartLight / StartHeavy，在挥砍中段抓图
            FireAttack(g, "StartLight", 0);
            stage = 6; frame = 0;
        }
        else if (stage == 6 && frame > 9)
        {
            ScreenCapture.CaptureScreenshot(Path.Combine(Dir, "ui_attack1.png"));
            Debug.Log("[Diag] 抓轻击挥砍帧");
            FireAttack(g, "StartHeavy", 0);
            stage = 7; frame = 0;
        }
        else if (stage == 7 && frame > 26)
        {
            ScreenCapture.CaptureScreenshot(Path.Combine(Dir, "ui_attack2.png"));
            Debug.Log("[Diag] 抓重击下劈帧");
            EditorApplication.ExitPlaymode();
            stage = 8;
        }
    }
}
