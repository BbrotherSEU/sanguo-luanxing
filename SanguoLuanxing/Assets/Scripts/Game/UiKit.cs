using UnityEngine;

/// <summary>
/// UI 装饰件：统一的底板、边框、分隔线、角饰。
/// 面板全是纯色块会显得很"程序"，加一层纸纹和回纹边框就有古卷的味道了。
/// </summary>
public static class UiKit
{
    static Texture2D paper;
    static bool paperTried;

    /// <summary>宣纸底纹（缺失时返回 null，调用方跳过）</summary>
    public static Texture2D Paper
    {
        get
        {
            if (!paperTried)
            {
                paperTried = true;
                paper = Tex.LoadRaw("tex/ui/ui_paper.jpg");
                if (paper == Tex.White) paper = null;
            }
            return paper;
        }
    }

    /// <summary>底板：纸纹 + 暗底 + 双线边框 + 四角回纹</summary>
    public static void Panel(System.Action<Rect, Color> fill, Rect r, Color body, Color frame, bool corners = true)
    {
        // 纸纹：整块铺一张宣纸，让纯色底不再死板
        var p = Paper;
        if (p != null)
        {
            GUI.color = new Color(1f, 1f, 1f, 0.055f);
            GUI.DrawTexture(r, p, ScaleMode.StretchToFill);
            GUI.color = Color.white;
        }
        fill(r, body);

        // 双线边框：外线粗、内线细，中间留一道暗缝
        Color outer = new Color(frame.r, frame.g, frame.b, 0.92f);
        Color inner = new Color(frame.r, frame.g, frame.b, 0.42f);
        Edge(fill, r, outer, 2f);
        Edge(fill, Inset(r, 5f), inner, 1f);

        if (corners) Corners(fill, r, frame);
    }

    /// <summary>四条边描线</summary>
    public static void Edge(System.Action<Rect, Color> fill, Rect r, Color c, float w)
    {
        fill(new Rect(r.x, r.y, r.width, w), c);
        fill(new Rect(r.x, r.yMax - w, r.width, w), c);
        fill(new Rect(r.x, r.y, w, r.height), c);
        fill(new Rect(r.xMax - w, r.y, w, r.height), c);
    }

    /// <summary>四角回纹：短线拼出中式边框的角</summary>
    public static void Corners(System.Action<Rect, Color> fill, Rect r, Color frame)
    {
        float len = 16f, w = 3f;
        Color c = new Color(frame.r, frame.g, frame.b, 0.88f);
        // 左上
        fill(new Rect(r.x + 4f, r.y + 4f, len, w), c);
        fill(new Rect(r.x + 4f, r.y + 4f, w, len), c);
        fill(new Rect(r.x + 4f + len, r.y + 4f, 5f, w), c);
        // 右上
        fill(new Rect(r.xMax - 4f - len, r.y + 4f, len, w), c);
        fill(new Rect(r.xMax - 4f - w, r.y + 4f, w, len), c);
        fill(new Rect(r.xMax - 4f - len - 5f, r.y + 4f, 5f, w), c);
        // 左下
        fill(new Rect(r.x + 4f, r.yMax - 4f - w, len, w), c);
        fill(new Rect(r.x + 4f, r.yMax - 4f - len, w, len), c);
        fill(new Rect(r.x + 4f + len, r.yMax - 4f - w, 5f, w), c);
        // 右下
        fill(new Rect(r.xMax - 4f - len, r.yMax - 4f - w, len, w), c);
        fill(new Rect(r.xMax - 4f - w, r.yMax - 4f - len, w, len), c);
        fill(new Rect(r.xMax - 4f - len - 5f, r.yMax - 4f - w, 5f, w), c);
    }

    /// <summary>分隔线：中间一段实线，两端渐隐</summary>
    public static void Divider(System.Action<Rect, Color> fill, Rect r, Color c, float h = 1f)
    {
        float y = r.y + (r.height > 0.01f ? r.height * 0.5f : 0f);
        float hh = r.height > 0.01f ? r.height : h;
        fill(new Rect(r.x, y, r.width, hh), new Color(c.r, c.g, c.b, c.a * 0.28f));
        fill(new Rect(r.x + r.width * 0.18f, y, r.width * 0.64f, hh), c);
    }

    /// <summary>小标题：前面一条短竖线 + 文字</summary>
    public static void Section(System.Action<Rect, Color> fill, System.Action<Rect, string, int, Color, TextAnchor, bool> label,
        Rect r, string text, Color accent, Color textColor)
    {
        fill(new Rect(r.x, r.y + 3f, 3f, 20f), accent);
        label(new Rect(r.x + 10f, r.y, r.width - 10f, 26f), text, 16, textColor, TextAnchor.MiddleLeft, false);
    }

    /// <summary>内缩矩形</summary>
    public static Rect Inset(Rect r, float d)
    {
        return new Rect(r.x + d, r.y + d, r.width - d * 2f, r.height - d * 2f);
    }

    /// <summary>品质角标：颜色块 + 高光边</summary>
    public static void Tag(System.Action<Rect, Color> fill, Rect r, Color c)
    {
        fill(r, c);
        fill(new Rect(r.x, r.y, r.width, 1f), new Color(1f, 1f, 1f, 0.35f));
        fill(new Rect(r.x, r.yMax - 1f, r.width, 1f), new Color(0f, 0f, 0f, 0.3f));
    }
}
