using UnityEngine;

public static class Util
{
    public static float Dist2(float ax, float az, float bx, float bz)
    {
        float dx = bx - ax, dz = bz - az;
        return Mathf.Sqrt(dx * dx + dz * dz);
    }

    public static float AngleBetween(float ax, float az, float bx, float bz)
    {
        return Mathf.Atan2(bz - az, bx - ax);
    }

    /// <summary>把角度差折到 [-PI, PI] 再取绝对值，避免绕圈误差</summary>
    public static float AngleDiff(float a, float b)
    {
        float d = Mathf.Abs(a - b);
        while (d > Mathf.PI) d = Mathf.Abs(d - Mathf.PI * 2f);
        return d;
    }

    /// <summary>弧度角平滑插值（走最短弧）</summary>
    public static float AngleLerp(float from, float to, float t)
    {
        float d = Mathf.DeltaAngle(from * Mathf.Rad2Deg, to * Mathf.Rad2Deg) * Mathf.Deg2Rad;
        return from + d * Mathf.Clamp01(t);
    }

    public static float Rand(float a, float b) => Random.Range(a, b);
    public static int RandInt(int a, int b) => Random.Range(a, b + 1);

    /// <summary>帧率无关的平滑系数：等价于 JS 里的 1 - exp(-rate*dt)，但低帧率下不会过冲</summary>
    public static float K(float rate, float dt) => 1f - Mathf.Exp(-rate * dt);

    public static float Damp(float cur, float target, float rate, float dt)
        => cur + (target - cur) * K(rate, dt);

    public static Vector2 Damp2(Vector2 cur, Vector2 target, float rate, float dt)
        => Vector2.Lerp(cur, target, K(rate, dt));

    public static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);

    /// <summary>按 z 深度排序：离镜头越近（z 越大）越靠前，避免角色互相穿插</summary>
    public static int SortOrder(float z, int bias = 0) => bias + Mathf.RoundToInt(-z * 8f) + 400;
}
