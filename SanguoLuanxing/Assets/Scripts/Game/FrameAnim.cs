using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// 帧动画播放器：驱动一个 SpriteRenderer 按状态切换帧序列。
/// 资源约定：StreamingAssets/assets/anim/{角色}/{状态}@{帧数}.png|jpg（横向帧条，等宽切格）。
/// 缺资源时 Has() 为 false，调用方回退到程序化动画。
/// </summary>
public class FrameAnim
{
    class Clip
    {
        public Sprite[] Frames;
        public float Fps;
        public bool Loop;
    }

    // 各状态的播放倍速：走路频率随移动速度变，走快了脚步也得跟着快
    readonly Dictionary<string, float> rates = new Dictionary<string, float>();

    // 全局记录「确实不存在」的路径，避免每个敌人生成时狂扫磁盘造成卡顿
    static readonly HashSet<string> Missing = new HashSet<string>();

    readonly SpriteRenderer sr;
    readonly Dictionary<string, Clip> clips = new Dictionary<string, Clip>();

    Clip cur;
    string curName = "";
    float t;
    int idx = -1;

    public bool Any => clips.Count > 0;
    public string Current => curName;

    public FrameAnim(SpriteRenderer sr) { this.sr = sr; }

    /// <summary>尝试加载一条帧条；文件不存在则跳过。
    /// asState 用于「借用别的帧条」：缺独立素材时，把同一份帧条再挂一个状态名，
    /// 播放速度可以单独调（比如把轻击一段放慢当成三段）。</summary>
    public void Load(string charId, string state, int count, float fps, bool loop, float worldHeight,
                    string asState = null)
    {
        string rel = "anim/" + charId + "/" + state + "@" + count;
        if (Missing.Contains(rel)) return;

        string full = null;
        foreach (var ext in new[] { ".png", ".jpg", ".jpeg" })
        {
            string p = Path.Combine(Tex.Root, rel + ext);
            if (File.Exists(p)) { full = p; break; }
        }
        if (full == null)
        {
            Missing.Add(rel);
            return;
        }

        float asp;
        Texture2D[] frames = Tex.CutoutFrames(rel + Path.GetExtension(full), count, out asp);
        if (frames == null || frames.Length == 0)
        {
            Missing.Add(rel);
            return;
        }

        var sprites = new Sprite[frames.Length];
        for (int i = 0; i < frames.Length; i++)
            sprites[i] = Tex.SpriteFor(frames[i], worldHeight);

        clips[asState ?? state] = new Clip { Frames = sprites, Fps = fps, Loop = loop };
    }

    public bool Has(string state) => clips.ContainsKey(state);

    /// <summary>当前帧序号（-1 表示没在播）</summary>
    public int Index => idx;

    /// <summary>当前 clip 的帧数</summary>
    public int Count => cur != null ? cur.Frames.Length : 0;

    /// <summary>
    /// 当前 clip 内的连续相位 0~1。
    /// 走路的重心起伏要跟脚步对齐，就靠它 —— 帧动画换帧是不连续的，
    /// 用它做插值才能让身体跟着腿一起起落。
    /// </summary>
    public float Phase01
    {
        get
        {
            if (cur == null || cur.Frames.Length == 0) return 0f;
            float f = t * cur.Fps;
            float len = cur.Frames.Length;
            if (cur.Loop) f -= Mathf.Floor(f / len) * len;
            else f = Mathf.Min(f, len - 1f);
            return f / len;
        }
    }

    /// <summary>设置某个状态的播放倍速（1 = 原速）。用来让步频跟着移动速度走。</summary>
    public void SetRate(string state, float rate)
    {
        if (rate <= 0f) rate = 1f;
        rates[state] = rate;
    }

    /// <summary>当前状态的播放倍速</summary>
    public float RateOf(string state) => rates.TryGetValue(state, out var r) ? r : 1f;

    public void Play(string state, bool force = false)
    {
        if (!clips.TryGetValue(state, out var c)) return;
        if (!force && curName == state) return;
        cur = c;
        curName = state;
        t = 0f;
        idx = -1;
        Apply(0);
    }

    public void Update(float dt)
    {
        if (cur == null) return;
        t += dt * RateOf(curName);
        int n = Mathf.FloorToInt(t * cur.Fps);
        int i = cur.Loop ? n % cur.Frames.Length : Mathf.Min(n, cur.Frames.Length - 1);
        Apply(i);
    }

    public bool Finished => cur != null && !cur.Loop && idx >= cur.Frames.Length - 1;

    void Apply(int i)
    {
        if (i == idx || cur == null) return;
        idx = i;
        sr.sprite = cur.Frames[Mathf.Clamp(i, 0, cur.Frames.Length - 1)];
    }
}
