using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 程序化音效：无需外部音频资源，打击感直接由合成声补足。
/// 后续替换为真实音源时，只要保持 Play(key) 的接口即可。
/// </summary>
public class Sfx
{
    AudioSource src;
    readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();

    public Sfx(GameObject host)
    {
        src = host.AddComponent<AudioSource>();
        src.playOnAwake = false;
        src.spatialBlend = 0f;
        src.volume = Settings.Current != null ? Settings.Current.SfxVolume : 0.9f;

        clips["swing"] = Make("swing", 0.22f, t =>
        {
            float e = Mathf.Exp(-t * 16f) * (1f - Mathf.Exp(-t * 90f));
            return (Noise() * 0.8f + Mathf.Sin(2f * Mathf.PI * 260f * t) * 0.25f) * e;
        });

        clips["hit"] = Make("hit", 0.16f, t =>
        {
            float e = Mathf.Exp(-t * 32f);
            float click = Mathf.Exp(-t * 90f) * Noise() * 0.9f;
            float body = Mathf.Sin(2f * Mathf.PI * 92f * t) * 0.7f * e;
            return click + body;
        });

        clips["crit"] = Make("crit", 0.2f, t =>
        {
            float e = Mathf.Exp(-t * 20f);
            return (Mathf.Sin(2f * Mathf.PI * 190f * t) * 0.7f + Noise() * 0.5f) * e;
        });

        clips["hurt"] = Make("hurt", 0.2f, t =>
        {
            float e = Mathf.Exp(-t * 18f);
            return (Mathf.Sin(2f * Mathf.PI * 132f * t) * 0.75f + Noise() * 0.3f) * e;
        });

        clips["skill"] = Make("skill", 0.35f, t =>
        {
            float f = 240f + 640f * t;
            float e = Mathf.Exp(-t * 5f) * (1f - Mathf.Exp(-t * 40f));
            return Mathf.Sin(2f * Mathf.PI * f * t) * 0.55f * e;
        });

        clips["boss"] = Make("boss", 0.6f, t =>
        {
            float e = Mathf.Exp(-t * 5.5f);
            return (Mathf.Sin(2f * Mathf.PI * 62f * t) * 0.8f + Noise() * 0.15f) * e;
        });

        clips["dash"] = Make("dash", 0.16f, t =>
        {
            float e = Mathf.Exp(-t * 22f);
            return Noise() * e * 0.7f;
        });

        // 脚步声：踩在土路上的闷响。脚步一响，"滑步"立刻变成"走路"。
        clips["step"] = Make("step", 0.11f, t =>
        {
            float e = Mathf.Exp(-t * 34f) * (1f - Mathf.Exp(-t * 220f));
            // 低频"咚" + 一点点鞋底摩擦的沙沙声
            float thud = Mathf.Sin(2f * Mathf.PI * 82f * t) * 0.75f;
            float grit = Noise() * 0.28f * Mathf.Exp(-t * 58f);
            return (thud + grit) * e;
        });

        clips["pickup"] = Make("pickup", 0.28f, t =>
        {
            float e = Mathf.Exp(-t * 12f);
            return (Mathf.Sin(2f * Mathf.PI * 880f * t) * 0.5f + Mathf.Sin(2f * Mathf.PI * 1320f * t) * 0.3f) * e;
        });

        clips["arrow"] = Make("arrow", 0.18f, t =>
        {
            float e = Mathf.Exp(-t * 20f);
            return Noise() * e * 0.55f;
        });

        clips["victory"] = Make("victory", 0.9f, t =>
        {
            float f = t < 0.3f ? 523f : (t < 0.6f ? 659f : 784f);
            return Mathf.Sin(2f * Mathf.PI * f * t) * 0.45f * Mathf.Exp(-t * 2.2f);
        });

        clips["defeat"] = Make("defeat", 0.8f, t =>
        {
            float f = 320f - 180f * t;
            return Mathf.Sin(2f * Mathf.PI * f * t) * 0.4f * Mathf.Exp(-t * 2.5f);
        });
    }

    public void Play(string key, float volume = 1f)
    {
        if (!clips.TryGetValue(key, out var c)) return;
        // 音效音量在设置里调；src.volume 已经是 SfxVolume，这里只乘调用方的相对强度
        src.PlayOneShot(c, Mathf.Clamp01(volume));
    }

    /// <summary>设置里改了音效音量后同步过来。</summary>
    public void RefreshVolume()
    {
        if (src != null && Settings.Current != null) src.volume = Settings.Current.SfxVolume;
    }

    static float Noise() => UnityEngine.Random.value * 2f - 1f;

    static AudioClip Make(string name, float dur, Func<float, float> fn)
    {
        const int rate = 44100;
        int n = Mathf.Max(1, Mathf.RoundToInt(rate * dur));
        var data = new float[n];
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)rate;
            data[i] = Mathf.Clamp(fn(t), -1f, 1f);
        }
        var clip = AudioClip.Create(name, n, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }
}
