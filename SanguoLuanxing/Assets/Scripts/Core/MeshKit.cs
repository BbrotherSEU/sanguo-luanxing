using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 低多边形网格拼装器：所有面硬边（flat shading），颜色走顶点色。
/// 顶点色 alpha = 1 - 风摆权重（见 Sanguo/Toon）。
/// Build 时把同位置顶点的平均法线写进 uv3，供描边外扩用，避免硬边处描边断裂。
/// </summary>
public class MeshKit
{
    readonly List<Vector3> v = new List<Vector3>(4096);
    readonly List<Vector3> n = new List<Vector3>(4096);
    readonly List<Color32> c = new List<Color32>(4096);
    readonly List<int> t = new List<int>(8192);

    /// <summary>当前写入的变换（各形状的局部坐标会先乘以它）</summary>
    public Matrix4x4 M = Matrix4x4.identity;
    /// <summary>当前风摆权重 0..1</summary>
    public float Sway;
    /// <summary>
    /// 保留顶点色自带的 alpha。
    /// 默认 alpha 被当成"风摆权重"覆盖掉（1 - sway），
    /// 但接地阴影要靠 alpha 做中心到边缘的衰减，这时必须原样保留。
    /// </summary>
    public bool KeepAlpha;

    public int Count => v.Count;

    public void Push(Vector3 pos, Quaternion rot, Vector3 scale) => M = Matrix4x4.TRS(pos, rot, scale);
    public void Push(Vector3 pos, float yawDeg) => M = Matrix4x4.TRS(pos, Quaternion.Euler(0f, yawDeg, 0f), Vector3.one);
    public void Pop() { M = Matrix4x4.identity; Sway = 0f; }

    Color32 Col(Color col, float sway)
    {
        if (!KeepAlpha) col.a = 1f - Mathf.Clamp01(sway);
        return col;
    }

    /// <summary>单个三角面；outward 用于自动修正朝向（世界空间里法线应远离该点）</summary>
    public void Tri(Vector3 a, Vector3 b, Vector3 d, Color col, Vector3? inside = null, bool twoSided = false)
    {
        a = M.MultiplyPoint3x4(a);
        b = M.MultiplyPoint3x4(b);
        d = M.MultiplyPoint3x4(d);
        if (inside.HasValue)
        {
            Vector3 ins = M.MultiplyPoint3x4(inside.Value);
            Vector3 nn = Vector3.Cross(b - a, d - a);
            if (Vector3.Dot(nn, (a + b + d) / 3f - ins) < 0f) { var tmp = b; b = d; d = tmp; }
        }
        AddRaw(a, b, d, Col(col, Sway));
        if (twoSided) AddRaw(a, d, b, Col(col, Sway));
    }

    void AddRaw(Vector3 a, Vector3 b, Vector3 d, Color32 col)
    {
        AddRaw(a, b, d, col, col, col);
    }

    void AddRaw(Vector3 a, Vector3 b, Vector3 d, Color32 ca, Color32 cb, Color32 cd)
    {
        Vector3 nn = Vector3.Cross(b - a, d - a);
        float len = nn.magnitude;
        if (len < 1e-8f) return;
        nn /= len;
        int i = v.Count;
        v.Add(a); v.Add(b); v.Add(d);
        n.Add(nn); n.Add(nn); n.Add(nn);
        c.Add(ca); c.Add(cb); c.Add(cd);
        t.Add(i); t.Add(i + 1); t.Add(i + 2);
    }

    /// <summary>四顶点各自上色，颜色在面上插值，避免整格一块纯色。</summary>
    public void QuadColors(Vector3 a, Vector3 b, Vector3 d, Vector3 e, Color ca, Color cb, Color cd, Color ce, Vector3? inside = null)
    {
        TriColors(a, b, d, ca, cb, cd, inside);
        TriColors(a, d, e, ca, cd, ce, inside);
    }

    public void TriColors(Vector3 a, Vector3 b, Vector3 d, Color ca, Color cb, Color cd, Vector3? inside = null)
    {
        a = M.MultiplyPoint3x4(a);
        b = M.MultiplyPoint3x4(b);
        d = M.MultiplyPoint3x4(d);
        if (inside.HasValue)
        {
            Vector3 ins = M.MultiplyPoint3x4(inside.Value);
            Vector3 nn = Vector3.Cross(b - a, d - a);
            if (Vector3.Dot(nn, (a + b + d) / 3f - ins) < 0f)
            {
                var tmp = b; b = d; d = tmp;
                var tc = cb; cb = cd; cd = tc;
            }
        }
        AddRaw(a, b, d, Col(ca, Sway), Col(cb, Sway), Col(cd, Sway));
    }

    public void Quad(Vector3 a, Vector3 b, Vector3 d, Vector3 e, Color col, Vector3? inside = null, bool twoSided = false)
    {
        Tri(a, b, d, col, inside, twoSided);
        Tri(a, d, e, col, inside, twoSided);
    }

    /// <summary>以 center 为中心的长方体（局部坐标）；top 可单独给顶面颜色</summary>
    public void Box(Vector3 center, Vector3 size, Color col, Color? top = null, float yaw = 0f)
    {
        Vector3 h = size * 0.5f;
        Quaternion r = Quaternion.Euler(0f, yaw, 0f);
        Vector3 P(float x, float y, float z) => center + r * new Vector3(x * h.x, y * h.y, z * h.z);
        Vector3 p000 = P(-1, -1, -1), p100 = P(1, -1, -1), p010 = P(-1, 1, -1), p110 = P(1, 1, -1);
        Vector3 p001 = P(-1, -1, 1), p101 = P(1, -1, 1), p011 = P(-1, 1, 1), p111 = P(1, 1, 1);
        Color tc = top ?? col;
        Quad(p010, p110, p111, p011, tc, center);           // 顶
        Quad(p000, p001, p101, p100, col * 0.8f, center);   // 底
        Quad(p000, p100, p110, p010, col, center);
        Quad(p001, p011, p111, p101, col, center);
        Quad(p000, p010, p011, p001, col, center);
        Quad(p100, p101, p111, p110, col, center);
    }

    /// <summary>圆台 / 圆锥（rTop=0）。basePos 为底面中心</summary>
    public void Cylinder(Vector3 basePos, float rBottom, float rTop, float h, int seg, Color col, Color? cap = null, float jitter = 0f, int seed = 0, float yaw = 0f)
    {
        var rng = new System.Random(seed);
        Vector3 inside = basePos + Vector3.up * h * 0.5f;
        var bot = new Vector3[seg];
        var top = new Vector3[seg];
        for (int i = 0; i < seg; i++)
        {
            float a = (i / (float)seg) * Mathf.PI * 2f + yaw * Mathf.Deg2Rad;
            float jb = 1f + (jitter > 0f ? ((float)rng.NextDouble() - 0.5f) * jitter : 0f);
            float jt = 1f + (jitter > 0f ? ((float)rng.NextDouble() - 0.5f) * jitter : 0f);
            Vector3 d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            bot[i] = basePos + d * rBottom * jb;
            top[i] = basePos + Vector3.up * h + d * rTop * jt;
        }
        Color cc = cap ?? col;
        Vector3 tc = basePos + Vector3.up * h;
        for (int i = 0; i < seg; i++)
        {
            int j = (i + 1) % seg;
            if (rTop > 0.0001f)
            {
                Quad(bot[i], bot[j], top[j], top[i], col, inside);
                Tri(tc, top[i], top[j], cc, inside);
            }
            else
            {
                Tri(bot[i], bot[j], tc, col, inside);
            }
            Tri(basePos, bot[j], bot[i], col * 0.75f, inside);
        }
    }

    /// <summary>低模圆团（UV 球，带随机起伏）：树冠、石头、云、草垛</summary>
    public void Blob(Vector3 center, Vector3 radii, Color col, int rings = 4, int seg = 7, float jitter = 0.18f, int seed = 0, Color? bottom = null)
    {
        var rng = new System.Random(seed * 7919 + 17);
        int R = Mathf.Max(2, rings);
        int S = Mathf.Max(3, seg);
        var pts = new Vector3[(R + 1) * S];
        for (int r = 0; r <= R; r++)
        {
            float phi = Mathf.PI * r / R;
            for (int s = 0; s < S; s++)
            {
                float th = Mathf.PI * 2f * s / S + (r % 2) * Mathf.PI / S;
                float j = 1f + ((float)rng.NextDouble() - 0.5f) * 2f * jitter;
                if (r == 0 || r == R) j = 1f;
                pts[r * S + s] = center + new Vector3(
                    Mathf.Sin(phi) * Mathf.Cos(th) * radii.x * j,
                    Mathf.Cos(phi) * radii.y * (r == 0 || r == R ? 1f : j),
                    Mathf.Sin(phi) * Mathf.Sin(th) * radii.z * j);
            }
        }
        Color bc = bottom ?? col * 0.82f;
        for (int r = 0; r < R; r++)
        {
            float k = r / (float)(R - 1 <= 0 ? 1 : R - 1);
            Color rc = Color.Lerp(col, bc, Mathf.Clamp01(k));
            for (int s = 0; s < S; s++)
            {
                int s2 = (s + 1) % S;
                Vector3 a = pts[r * S + s], b = pts[r * S + s2];
                Vector3 d = pts[(r + 1) * S + s], e = pts[(r + 1) * S + s2];
                if (r == 0) Tri(a, e, d, rc, center);
                else if (r == R - 1) Tri(a, b, d, rc, center);
                else Quad(a, b, e, d, rc, center);
            }
        }
    }

    /// <summary>
    /// 中式坡屋顶：两坡 + 山墙三角 + 正脊，四角起翘。
    /// center 为檐口高度处的中心；w 沿 x（屋脊方向），d 沿 z。
    /// </summary>
    public void Roof(Vector3 center, float w, float d, float h, Color col, Color ridge, float yaw = 0f, float curl = 0.35f)
    {
        Quaternion r = Quaternion.Euler(0f, yaw, 0f);
        Vector3 P(float x, float y, float z) => center + r * new Vector3(x, y, z);
        float hw = w * 0.5f, hd = d * 0.5f;
        Vector3 inside = P(0, h * 0.3f, 0);
        // 檐角起翘
        Vector3 e0 = P(-hw - curl * 0.4f, curl, -hd), e1 = P(hw + curl * 0.4f, curl, -hd);
        Vector3 e2 = P(hw + curl * 0.4f, curl, hd), e3 = P(-hw - curl * 0.4f, curl, hd);
        Vector3 m0 = P(-hw * 0.5f, 0f, -hd), m1 = P(hw * 0.5f, 0f, -hd);
        Vector3 m2 = P(hw * 0.5f, 0f, hd), m3 = P(-hw * 0.5f, 0f, hd);
        Vector3 r0 = P(-hw * 0.82f, h, 0f), r1 = P(hw * 0.82f, h, 0f);
        Color c2 = col * 0.88f;
        // 前坡（三段，中间平、两端翘）
        Quad(m0, m1, r1, r0, col, inside);
        Tri(e0, m0, r0, col, inside);
        Tri(m1, e1, r1, col, inside);
        // 后坡
        Quad(m3, r0, r1, m2, c2, inside);
        Tri(e3, r0, m3, c2, inside);
        Tri(m2, r1, e2, c2, inside);
        // 歇山两侧
        Tri(e0, r0, e3, c2 * 0.9f, inside);
        Tri(e1, e2, r1, c2 * 0.9f, inside);
        // 底面（从下往上看檐底）：提亮，檐下不再是死黑楔形
        Quad(e0, e3, e2, e1, col * 0.74f, inside + Vector3.up * 2f);
        // 正脊 + 鸱吻
        Vector3 rc = (r0 + r1) * 0.5f;
        Box(rc + Vector3.up * 0.08f, new Vector3(Vector3.Distance(r0, r1) + 0.3f, 0.18f, 0.2f), ridge, null, yaw);
        Box(r0 + Vector3.up * 0.22f + (r0 - r1).normalized * 0.12f, new Vector3(0.18f, 0.35f, 0.18f), ridge, null, yaw);
        Box(r1 + Vector3.up * 0.22f + (r1 - r0).normalized * 0.12f, new Vector3(0.18f, 0.35f, 0.18f), ridge, null, yaw);
    }

    /// <summary>四角攒尖顶（亭子、塔楼）</summary>
    public void Pyramid(Vector3 center, float w, float h, Color col, float curl = 0.3f, int sides = 4, float yaw = 45f)
    {
        Vector3 apex = center + Vector3.up * h;
        Vector3 inside = center + Vector3.up * h * 0.3f;
        var ring = new Vector3[sides];
        for (int i = 0; i < sides; i++)
        {
            float a = (i / (float)sides) * Mathf.PI * 2f + yaw * Mathf.Deg2Rad;
            ring[i] = center + new Vector3(Mathf.Cos(a) * w * 0.7071f * 1.0f, curl, Mathf.Sin(a) * w * 0.7071f);
        }
        for (int i = 0; i < sides; i++)
        {
            int j = (i + 1) % sides;
            Vector3 mid = (ring[i] + ring[j]) * 0.5f - Vector3.up * curl;
            Tri(ring[i], mid, apex, i % 2 == 0 ? col : col * 0.9f, inside);
            Tri(mid, ring[j], apex, i % 2 == 0 ? col : col * 0.9f, inside);
            Tri(ring[i], ring[j], mid, col * 0.74f, inside + Vector3.up * 3f);
        }
        Blob(apex + Vector3.up * 0.12f, new Vector3(0.14f, 0.2f, 0.14f), col * 0.7f, 2, 5, 0f);
    }

    /// <summary>两点之间的横木 / 栏杆</summary>
    public void Beam(Vector3 a, Vector3 b, float thick, Color col)
    {
        Vector3 d = b - a;
        float len = d.magnitude;
        if (len < 0.01f) return;
        float yaw = -Mathf.Atan2(d.z, d.x) * Mathf.Rad2Deg;
        var save = M;
        Vector3 mid = (a + b) * 0.5f;
        float pitch = Mathf.Atan2(d.y, new Vector2(d.x, d.z).magnitude) * Mathf.Rad2Deg;
        M = save * Matrix4x4.TRS(mid, Quaternion.Euler(0f, yaw, pitch), Vector3.one);
        Box(Vector3.zero, new Vector3(len, thick, thick), col);
        M = save;
    }

    /// <summary>草叶簇：几片双面三角，随风摆</summary>
    public void Tuft(Vector3 p, float h, Color col, int blades, int seed)
    {
        var rng = new System.Random(seed);
        for (int i = 0; i < blades; i++)
        {
            float a = (float)rng.NextDouble() * Mathf.PI * 2f;
            float lean = 0.06f + (float)rng.NextDouble() * 0.12f;
            float hh = h * (0.82f + (float)rng.NextDouble() * 0.28f);
            float w = 0.13f + (float)rng.NextDouble() * 0.07f;
            Vector3 dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            Vector3 side = new Vector3(-dir.z, 0f, dir.x) * w;
            Vector3 b0 = p - side + dir * 0.05f, b1 = p + side + dir * 0.05f;
            Vector3 tip = p + dir * (lean * hh) + Vector3.up * hh;
            Color cc = Color.Lerp(col, col * 1.25f, (float)rng.NextDouble());
            // 底部两点不摆、尖端摆：分别写入顶点色 alpha
            AddBlade(M.MultiplyPoint3x4(b0), M.MultiplyPoint3x4(b1), M.MultiplyPoint3x4(tip), cc * 0.75f, cc, 0.1f);
        }
    }

    void AddBlade(Vector3 a, Vector3 b, Vector3 tip, Color baseCol, Color tipCol, float tipAlpha)
    {
        Vector3 nn = Vector3.Cross(b - a, tip - a).normalized;
        Color32 cb = baseCol; cb.a = 255;
        Color32 ct = tipCol; ct.a = (byte)(Mathf.Clamp01(tipAlpha) * 255f);
        for (int side = 0; side < 2; side++)
        {
            int i = v.Count;
            v.Add(a); v.Add(side == 0 ? b : tip); v.Add(side == 0 ? tip : b);
            Vector3 nrm = side == 0 ? nn : -nn;
            // 草叶法线朝上偏，避免背光面整片发黑
            nrm = (nrm * 0.35f + Vector3.up).normalized;
            n.Add(nrm); n.Add(nrm); n.Add(nrm);
            c.Add(cb); c.Add(side == 0 ? cb : ct); c.Add(side == 0 ? ct : cb);
            t.Add(i); t.Add(i + 1); t.Add(i + 2);
        }
    }

    /// <summary>直接写一个带自定义法线与顶点色的三角（地形用）</summary>
    public void RawTri(Vector3 a, Vector3 b, Vector3 d, Color32 col)
    {
        AddRaw(a, b, d, col);
    }

    /// <param name="smoothNormals">地面用平滑法线，避免每格一个硬折面把草地拉皱。</param>
    public Mesh Build(string name, bool outlineNormals, bool smoothNormals = false)
    {
        var mesh = new Mesh { name = name, indexFormat = IndexFormat.UInt32 };
        mesh.SetVertices(v);
        mesh.SetColors(c);
        mesh.SetTriangles(t, 0, true);

        List<Vector3> averaged = null;
        if (outlineNormals || smoothNormals)
        {
            var map = new Dictionary<Vector3Int, Vector3>(v.Count / 2);
            var keys = new Vector3Int[v.Count];
            for (int i = 0; i < v.Count; i++)
            {
                var k = new Vector3Int(Mathf.RoundToInt(v[i].x * 200f), Mathf.RoundToInt(v[i].y * 200f), Mathf.RoundToInt(v[i].z * 200f));
                keys[i] = k;
                map.TryGetValue(k, out var acc);
                map[k] = acc + n[i];
            }
            averaged = new List<Vector3>(v.Count);
            for (int i = 0; i < v.Count; i++) averaged.Add(map[keys[i]].normalized);
            if (outlineNormals) mesh.SetUVs(3, averaged);
        }
        mesh.SetNormals(smoothNormals && averaged != null ? averaged : n);
        mesh.RecalculateBounds();
        return mesh;
    }

    public void Clear()
    {
        v.Clear(); n.Clear(); c.Clear(); t.Clear();
        M = Matrix4x4.identity;
        Sway = 0f;
        KeepAlpha = false;
    }
}
