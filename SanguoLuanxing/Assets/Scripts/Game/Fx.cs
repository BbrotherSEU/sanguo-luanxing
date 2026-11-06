using System.Collections.Generic;
using UnityEngine;

/// <summary>Sprite 版特效：刀光 / 地面环 / 粒子 / 光柱 / 闪光。全部池化，不产生 GC。</summary>
public class Fx
{
    enum Mode { Slash, Ring, Flash, Pillar, Particle, Spin, Spark, Star, Ghost }

    class Piece
    {
        public GameObject Go;
        public SpriteRenderer Sr;
        public Mode Mode;
        public float T, Dur, Size0, Size1;
        public Vector3 Pos, Vel;
        public Color Col = Color.white;
        public float Angle;
        public float SpinDir;
        public Vector3 Scale3 = Vector3.one;
    }

    readonly Transform root;
    readonly List<Piece> live = new List<Piece>();
    readonly List<Piece> pool = new List<Piece>();
    readonly Dictionary<Mode, Sprite> spriteCache = new Dictionary<Mode, Sprite>();

    public Fx(Transform parent)
    {
        var go = new GameObject("fx");
        go.transform.SetParent(parent, false);
        root = go.transform;
    }

    // ---------------- 对外接口 ----------------

    public void Slash(Vector3 pos, float angle, Color color, float size, float dur = 0.24f)
    {
        var p = Take(Mode.Slash, color, size, size * 1.08f, dur, pos + Vector3.up * 1.15f, Util.SortOrder(pos.z, 80));
        p.Go.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);
        p.Go.transform.Rotate(Vector3.up, -angle * Mathf.Rad2Deg, Space.World);
        p.Angle = angle;
    }

    public void Ring(Vector3 pos, float radius, Color color, float dur = 0.45f, float from = 0.55f)
    {
        var p = Take(Mode.Ring, color, radius * 2f * from, radius * 2f, dur, pos + Vector3.up * 0.04f, Util.SortOrder(pos.z, -30));
        p.Go.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);
    }

    public void Flash(Vector3 pos, Color color, float size, float dur = 0.3f)
    {
        Take(Mode.Flash, color, size * 0.8f, size, dur, pos + Vector3.up * 1.1f, Util.SortOrder(pos.z, 60));
    }

    public void Pillar(Vector3 pos, Color color, float height, float width, float dur = 0.7f)
    {
        var p = Take(Mode.Pillar, color, width, width, dur, pos, Util.SortOrder(pos.z, 40));
        p.Size0 = width;
        p.Size1 = height;
        p.Go.transform.localScale = new Vector3(width, 0.01f, 1f);
        p.Go.transform.position = pos + Vector3.up * (height * 0.5f);
    }

    public void Burst(Vector3 pos, Color color, int count, float speed)
    {
        for (int i = 0; i < count; i++)
        {
            float a = Util.Rand(0f, Mathf.PI * 2f);
            float s = speed * Util.Rand(0.55f, 1.35f);
            var p = Take(Mode.Particle, color, Util.Rand(0.18f, 0.34f), 0.05f, Util.Rand(0.28f, 0.5f),
                pos + new Vector3(0f, Util.Rand(0.5f, 1.5f), 0f), Util.SortOrder(pos.z, 70));
            p.Vel = new Vector3(Mathf.Cos(a) * s, Util.Rand(0.2f, 1.2f), Mathf.Sin(a) * s);
        }
    }

    /// <summary>
    /// 命中冲击点：白爆 + 星芒 + 火花射线 + 地面环。多层叠在 ~100ms 内同时亮起。
    /// </summary>
    public void Impact(Vector3 pos, Color color, float size, float dur = 0.32f)
    {
        Vector3 p = pos + Vector3.up * 1.15f;
        Take(Mode.Flash, Color.white, size * 0.55f, size * 1.35f, dur * 0.38f, p, Util.SortOrder(pos.z, 94));
        Take(Mode.Flash, color, size * 0.35f, size * 0.95f, dur * 0.72f, p, Util.SortOrder(pos.z, 93));
        var star = Take(Mode.Star, Color.white, size * 0.55f, size * 1.6f, dur * 0.28f, p, Util.SortOrder(pos.z, 96));
        star.Go.transform.rotation = Quaternion.Euler(0f, 0f, Util.Rand(0f, 90f));
        Ring(pos, size * 1.35f, new Color(color.r, color.g, color.b, 0.95f), dur * 0.85f, 0.18f);
        Burst(pos, color, 10, size * 3.2f);
        Sparks(pos, color, 8, size * 5.5f);
    }

    public void Sparks(Vector3 pos, Color color, int count, float speed)
    {
        for (int i = 0; i < count; i++)
        {
            float a = Util.Rand(0f, Mathf.PI * 2f);
            float s = speed * Util.Rand(0.55f, 1.4f);
            var p = Take(Mode.Spark, Color.Lerp(Color.white, color, 0.45f),
                Util.Rand(0.35f, 0.7f), 0.05f, Util.Rand(0.16f, 0.32f),
                pos + new Vector3(0f, Util.Rand(0.7f, 1.4f), 0f), Util.SortOrder(pos.z, 95));
            p.Vel = new Vector3(Mathf.Cos(a) * s, Util.Rand(1.2f, 3.5f), Mathf.Sin(a) * s);
            p.Scale3 = new Vector3(0.22f, 1f, 1f);
        }
    }

    /// <summary>残影：闪避 / 突刺拖尾</summary>
    public void Afterimage(SpriteRenderer src, float dur = 0.22f)
    {
        if (src == null || src.sprite == null) return;
        var p = Take(Mode.Ghost, new Color(1f, 0.92f, 0.72f, 0.55f), 1f, 1f, dur, src.transform.position, src.sortingOrder - 1);
        p.Sr.sprite = src.sprite;
        p.Sr.flipX = src.flipX;
        p.Go.transform.rotation = src.transform.rotation;
        p.Go.transform.localScale = src.transform.lossyScale;
        p.Scale3 = src.transform.lossyScale;
    }

    /// <summary>横扫用的旋转刀弧：两片反向旋转的 Slash，比单纯 Ring 更有「扫」的感觉</summary>
    public void Spin(Vector3 pos, float radius, Color color, float dur = 0.4f, float dir = 1f)
    {
        var p = Take(Mode.Spin, color, radius * 1.6f, radius * 2.2f, dur, pos + Vector3.up * 1.05f, Util.SortOrder(pos.z, 85));
        p.SpinDir = dir;
        p.Go.transform.rotation = Quaternion.Euler(20f, 0f, 0f);
    }

    /// <summary>蓄力预警环：持续到 hit 时刻，比「闪一下就消失」的 telegraph 更可读</summary>
    public void Telegraph(Vector3 pos, float radius, Color color, float dur)
    {
        Ring(pos, radius, color, dur, 0.15f);
        Take(Mode.Flash, new Color(color.r, color.g, color.b, 0.7f), radius * 0.2f, radius * 1.05f, dur,
            pos + Vector3.up * 0.08f, Util.SortOrder(pos.z, -25));
    }

    // ---------------- 更新 ----------------

    public void Update(float dt)
    {
        for (int i = live.Count - 1; i >= 0; i--)
        {
            var p = live[i];
            p.T += dt;
            float u = p.Dur <= 0.0001f ? 1f : Mathf.Clamp01(p.T / p.Dur);

            switch (p.Mode)
            {
                case Mode.Particle:
                    p.Vel += new Vector3(0f, -2.2f, 0f) * dt;
                    p.Pos += p.Vel * dt;
                    p.Vel *= Mathf.Pow(0.02f, dt);
                    p.Go.transform.position = p.Pos;
                    break;

                case Mode.Spark:
                    p.Vel += new Vector3(0f, -8f, 0f) * dt;
                    p.Pos += p.Vel * dt;
                    p.Go.transform.position = p.Pos;
                    float len = Mathf.Lerp(p.Size0, p.Size1, u);
                    var look = p.Vel.sqrMagnitude > 0.01f ? p.Vel.normalized : Vector3.right;
                    p.Go.transform.rotation = Quaternion.LookRotation(look, Vector3.up) * Quaternion.Euler(90f, 0f, 0f);
                    p.Go.transform.localScale = new Vector3(0.18f, len, 1f);
                    break;

                case Mode.Star:
                    p.Go.transform.localScale = Vector3.one * Mathf.Lerp(p.Size0, p.Size1, 1f - Mathf.Pow(1f - u, 2f));
                    p.Go.transform.Rotate(Vector3.forward, 240f * dt, Space.Self);
                    break;

                case Mode.Ghost:
                    p.Go.transform.localScale = p.Scale3;
                    break;

                case Mode.Ring:
                case Mode.Flash:
                case Mode.Slash:
                    p.Go.transform.localScale = Vector3.one * Mathf.Lerp(p.Size0, p.Size1, u);
                    break;

                case Mode.Spin:
                    p.Go.transform.localScale = Vector3.one * Mathf.Lerp(p.Size0, p.Size1, u);
                    p.Go.transform.Rotate(Vector3.up, p.SpinDir * 720f * dt, Space.World);
                    break;

                case Mode.Pillar:
                    float grow = Mathf.Clamp01(u / 0.25f);
                    p.Go.transform.localScale = new Vector3(p.Size0, Mathf.Max(0.01f, p.Size1 * grow), 1f);
                    break;
            }

            float alpha = p.Mode == Mode.Particle ? 1f - u : Mathf.Pow(1f - u, 1.4f);
            p.Sr.color = new Color(p.Col.r, p.Col.g, p.Col.b, p.Col.a * alpha);

            if (u >= 1f)
            {
                p.Go.SetActive(false);
                live.RemoveAt(i);
                pool.Add(p);
            }
        }
    }

    // ---------------- 池 ----------------

    Piece Take(Mode mode, Color color, float size0, float size1, float dur, Vector3 pos, int order)
    {
        Piece p;
        if (pool.Count > 0)
        {
            p = pool[pool.Count - 1];
            pool.RemoveAt(pool.Count - 1);
            p.Go.SetActive(true);
        }
        else
        {
            p = new Piece();
            p.Go = new GameObject("fxpiece");
            p.Go.transform.SetParent(root, false);
            p.Sr = p.Go.AddComponent<SpriteRenderer>();
        }

        if (!spriteCache.TryGetValue(mode, out var spr))
        {
            spr = Tex.CenterSprite(TextureFor(mode), 1f);
            spriteCache[mode] = spr;
        }
        p.Sr.sprite = spr;
        bool add = mode == Mode.Flash || mode == Mode.Spark || mode == Mode.Star || mode == Mode.Slash || mode == Mode.Spin;
        p.Sr.sharedMaterial = add ? Look.Additive : Look.AlphaFx;
        p.Sr.sortingOrder = order;
        p.Mode = mode;
        p.T = 0f;
        p.Dur = dur;
        p.Size0 = size0;
        p.Size1 = size1;
        p.Pos = pos;
        p.Vel = Vector3.zero;
        p.Col = color;
        p.Go.transform.position = pos;
        p.Go.transform.rotation = Quaternion.identity;
        p.Go.transform.localScale = Vector3.one * size0;
        p.Sr.color = color;
        live.Add(p);
        return p;
    }

    static Texture2D TextureFor(Mode mode)
    {
        switch (mode)
        {
            case Mode.Slash:
            case Mode.Spin: return Tex.Slash;
            case Mode.Ring: return Tex.Ring;
            case Mode.Flash: return Tex.Glow;
            case Mode.Particle: return Tex.Dot;
            case Mode.Spark: return Tex.Spark != null ? Tex.Spark : Tex.Glow;
            case Mode.Star: return Tex.Star != null ? Tex.Star : Tex.Glow;
            default: return Tex.White;
        }
    }

    public void Dispose()
    {
        live.Clear();
        pool.Clear();
        if (root != null) Object.Destroy(root.gameObject);
    }
}
