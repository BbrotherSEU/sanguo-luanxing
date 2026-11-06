using System.Collections.Generic;
using UnityEngine;

public struct Obstacle
{
    public float X, Z, R;
    public Obstacle(float x, float z, float r) { X = x; Z = z; R = r; }
}

/// <summary>战场 / 大地图场景。视觉交给卡通 WorldBuilder，碰撞仍用圆障碍。</summary>
public class Arena
{
    public const float Bound = 21f;
    public float PlayBound = 21f;

    public List<Obstacle> Obstacles = new List<Obstacle>();
    public MapDef Map;
    public Theme Theme;
    GameObject root;
    WorldBuilder world;

    public static Arena Build(MapDef map, Transform parent, Camera cam = null)
    {
        var a = new Arena { Map = map };
        a.root = new GameObject("Arena_" + map.Id);
        a.root.transform.SetParent(parent, false);
        a.world = new WorldBuilder();
        a.world.Build(a.root.transform, map, cam);
        a.Theme = a.world.Theme;
        a.PlayBound = a.world.PlayBound > 1f ? a.world.PlayBound : Bound;
        a.Obstacles = a.world.Obstacles;
        return a;
    }

    public void Tick()
    {
        if (world != null) world.Tick();
    }

    /// <summary>设置里改了昼夜 / 时段后，让场景光照立刻跟着变</summary>
    public void SetAmbience(bool autoCycle, float phase01)
    {
        if (world != null) world.SetAmbience(autoCycle, phase01);
    }

    /// <summary>该点的地面高度。角色要贴着地面走，否则有起伏的地形上会陷进去或悬空。</summary>
    public float GroundHeight(float x, float z)
        => world != null ? world.GroundHeight(x, z) : 0f;

    public void Resolve(ref Vector3 pos, float radius, Vector3? home = null)
    {
        float b = PlayBound > 1f ? PlayBound : Bound;
        pos.x = Mathf.Clamp(pos.x, -b, b);
        pos.z = Mathf.Clamp(pos.z, -b, b);
        for (int i = 0; i < Obstacles.Count; i++)
        {
            Obstacle c = Obstacles[i];
            float dx = pos.x - c.X, dz = pos.z - c.Z;
            float d = Mathf.Sqrt(dx * dx + dz * dz);
            float min = c.R + radius;
            if (d > 0.0001f && d < min)
            {
                float push = (min - d) / d;
                pos.x += dx * push;
                pos.z += dz * push;
            }
        }
        if (home.HasValue)
        {
            Vector3 h = home.Value;
            if (Util.Dist2(pos.x, pos.z, h.x, h.z) > 24f) { pos.x = h.x; pos.z = h.z; }
        }
    }

    public void Dispose()
    {
        if (world != null) world.Dispose();
        world = null;
        if (root != null) Object.Destroy(root);
        root = null;
        Obstacles.Clear();
    }
}
