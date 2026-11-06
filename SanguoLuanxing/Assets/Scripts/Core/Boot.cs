using UnityEngine;

/// <summary>
/// 入口：进入 Play 前自动创建 Game 对象，场景由代码生成，无需手动摆放。
/// 对应的空场景由 Assets/Editor/ProjectSetup.cs 自动创建。
/// </summary>
public static class Boot
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Init()
    {
        if (Object.FindObjectOfType<Game>() != null) return;
        var go = new GameObject("Game");
        Object.DontDestroyOnLoad(go);
        go.AddComponent<Game>();
    }
}
