using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 首次打开工程时自动完成：建空场景、加入 BuildSettings、设置 Gamma 色彩空间与窗口参数。
/// 场景本身是空的——所有内容由 Boot.cs 在 Play 时用代码搭建，避免手写 .unity YAML。
/// </summary>
[InitializeOnLoad]
public static class ProjectSetup
{
    const string ScenePath = "Assets/Scenes/Main.unity";
    static bool _done;

    static ProjectSetup()
    {
        EditorApplication.delayCall += Run;
    }

    static void Run()
    {
        if (_done) return;
        _done = true;
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;

        // 线性空间会让脚本生成的贴图与网页版观感不一致，这里统一用 Gamma
        PlayerSettings.colorSpace = ColorSpace.Gamma;
        PlayerSettings.companyName = "LuanXing";
        PlayerSettings.productName = "三国·乱世行";
        PlayerSettings.defaultScreenWidth = 1280;
        PlayerSettings.defaultScreenHeight = 720;
        PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
        PlayerSettings.resizableWindow = true;

        if (!File.Exists(ScenePath))
        {
            Directory.CreateDirectory("Assets/Scenes");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log("[ProjectSetup] 已创建 Assets/Scenes/Main.unity，直接按 Play 即可运行。");
        }
        if (EditorBuildSettings.scenes.Length == 0 || EditorBuildSettings.scenes[0].path != ScenePath)
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        if (SceneManager.GetActiveScene().path != ScenePath && !SceneManager.GetActiveScene().isDirty)
            EditorSceneManager.OpenScene(ScenePath);
    }
}
