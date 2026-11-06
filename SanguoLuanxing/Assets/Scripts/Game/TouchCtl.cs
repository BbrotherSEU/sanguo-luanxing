using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 触屏输入：左半屏浮动摇杆（手指落在哪锚点就在哪），右半屏图标按钮。
/// 与键鼠输入并存，由 Battle.ReadInput 合并。
/// </summary>
public static class TouchCtl
{
    public static bool Active;                       // 检测到触屏后启用
    public static Vector2 Joy;                       // 世界系方向：x→世界x，y→世界z
    public static bool JoyVisible;
    public static Vector2 JoyAnchorPx, JoyKnobPx;    // 屏幕像素（y 向上，与 touch.position 一致）
    public static bool Light, Heavy, Dodge, Q, E, R; // 本帧按下

    public class Btn { public string Id; public Vector2 Center; public float R; public Texture2D Icon; public Color Tint; }
    public static readonly List<Btn> Buttons = new List<Btn>();

    static int joyFinger = -1;
    static float btnW, btnH;
    const float JoyRadiusPx = 78f;

    /// <summary>每帧最前调用；playing=false 时只追踪不生效</summary>
    public static void Poll(bool playing)
    {
        Light = Heavy = Dodge = Q = E = R = false;
        if (!Input.touchSupported) return;
        if (Input.touchCount > 0) Active = true;
        if (!Active) return;

        RebuildButtons();

        for (int i = 0; i < Input.touchCount; i++)
        {
            Touch t = Input.GetTouch(i);
            switch (t.phase)
            {
                case TouchPhase.Began:
                    if (!playing) break;
                    if (joyFinger < 0 && t.position.x < Screen.width * 0.5f)
                    {
                        joyFinger = t.fingerId;
                        JoyAnchorPx = JoyKnobPx = t.position;
                        JoyVisible = true;
                        Joy = Vector2.zero;
                    }
                    else
                    {
                        HitButton(t.position);
                    }
                    break;

                case TouchPhase.Moved:
                case TouchPhase.Stationary:
                    if (t.fingerId == joyFinger)
                    {
                        Vector2 d = t.position - JoyAnchorPx;
                        float m = d.magnitude;
                        if (m > JoyRadiusPx) d = d / m * JoyRadiusPx;
                        JoyKnobPx = JoyAnchorPx + d;
                        // 屏幕像素 y 向上。相机在 +z 看向 -z ⇒ 屏幕右 = 世界 -x，屏幕上 = 世界 -z。
                        Joy = new Vector2(-d.x / JoyRadiusPx, -d.y / JoyRadiusPx);
                    }
                    break;

                case TouchPhase.Ended:
                case TouchPhase.Canceled:
                    if (t.fingerId == joyFinger)
                    {
                        joyFinger = -1;
                        JoyVisible = false;
                        Joy = Vector2.zero;
                    }
                    break;
            }
        }
    }

    static void HitButton(Vector2 px)
    {
        for (int i = 0; i < Buttons.Count; i++)
        {
            var b = Buttons[i];
            if (Vector2.Distance(px, b.Center) > b.R * 1.35f) continue;
            switch (b.Id)
            {
                case "light": Light = true; break;
                case "heavy": Heavy = true; break;
                case "dodge": Dodge = true; break;
                case "q": Q = true; break;
                case "e": E = true; break;
                case "r": R = true; break;
            }
            return;
        }
    }

    static void RebuildButtons()
    {
        if (Buttons.Count == 6 && Mathf.Approximately(btnW, Screen.width) && Mathf.Approximately(btnH, Screen.height)) return;
        btnW = Screen.width; btnH = Screen.height;
        Buttons.Clear();
        float w = Screen.width, h = Screen.height;
        // Center 用 y 向上的像素坐标，与 touch.position 一致
        Buttons.Add(new Btn { Id = "light", Center = new Vector2(w * 0.90f, h * 0.22f), R = h * 0.085f, Icon = Tex.IconLight, Tint = new Color(1f, 0.95f, 0.85f) });
        Buttons.Add(new Btn { Id = "heavy", Center = new Vector2(w * 0.79f, h * 0.33f), R = h * 0.070f, Icon = Tex.IconHeavy, Tint = new Color(1f, 0.62f, 0.38f) });
        Buttons.Add(new Btn { Id = "dodge", Center = new Vector2(w * 0.91f, h * 0.45f), R = h * 0.060f, Icon = Tex.IconDodge, Tint = new Color(0.62f, 0.78f, 0.87f) });
        Buttons.Add(new Btn { Id = "q", Center = new Vector2(w * 0.66f, h * 0.20f), R = h * 0.055f, Icon = Tex.IconQ, Tint = new Color(1f, 0.84f, 0.48f) });
        Buttons.Add(new Btn { Id = "e", Center = new Vector2(w * 0.58f, h * 0.33f), R = h * 0.055f, Icon = Tex.IconE, Tint = new Color(1f, 0.85f, 0.58f) });
        Buttons.Add(new Btn { Id = "r", Center = new Vector2(w * 0.72f, h * 0.47f), R = h * 0.055f, Icon = Tex.IconR, Tint = new Color(1f, 0.77f, 0.42f) });
    }
}
