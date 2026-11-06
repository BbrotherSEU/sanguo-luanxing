using UnityEngine;

/// <summary>2.5D 相机：跟随 + 创伤抖动 + 命中推镜(FOV punch)</summary>
public class CameraRig
{
    public Camera Cam;
    public float Height = 9.4f, Back = 11.2f, Lead = 2.0f;

    Vector3 pos, target;
    float trauma, t, zoom = 1f, punchT, punchAmt;
    Vector3 kick;

    public CameraRig(Camera cam)
    {
        Cam = cam;
        Cam.fieldOfView = 45f;
        SnapTo(new Vector3(0f, 0f, 15f));
    }

    public void AddTrauma(float v) => trauma = Mathf.Clamp(trauma + v, 0f, 1f);

    /// <summary>命中瞬间镜头后坐：沿命中反方向推一下再弹回</summary>
    public void Kick(Vector3 worldDir, float amount)
    {
        kick += new Vector3(worldDir.x, 0f, worldDir.z) * amount;
    }

    /// <summary>命中瞬间快速推镜（0.2s 收回），强化打击感</summary>
    public void Punch(float v)
    {
        punchAmt = Mathf.Max(punchAmt, v);
        punchT = 0.2f;
    }

    public void SnapTo(Vector3 p)
    {
        pos = new Vector3(p.x, Height, p.z + Back);
        target = new Vector3(p.x, 1.5f, p.z - Lead);
    }

    public void SetZoom(float z)
    {
        zoom = z;
        Cam.fieldOfView = 45f / zoom;
    }

    public void Update(float dt, Vector3 follow)
    {
        pos.x = Util.Damp(pos.x, follow.x, 30f, dt);
        pos.z = Util.Damp(pos.z, follow.z + Back, 30f, dt);
        pos.y = Height;

        target.x = Util.Damp(target.x, follow.x, 30f, dt);
        target.z = Util.Damp(target.z, follow.z - Lead, 30f, dt);
        target.y = 1.5f;

        trauma = Mathf.Max(0f, trauma - dt * 1.5f);
        // trauma² 会让轻击几乎没有抖动，改成带底噪的曲线：轻击也有明确反馈
        float shake = trauma * (0.40f + 0.60f * trauma);
        t += dt * 26f;

        float ox = Mathf.Sin(t * 1.7f) * shake * 0.62f + Mathf.Sin(t * 3.1f) * shake * 0.28f;
        float oy = Mathf.Sin(t * 2.3f) * shake * 0.48f;
        float oz = Mathf.Sin(t * 1.13f) * shake * 0.40f;

        kick = Vector3.Lerp(kick, Vector3.zero, Util.K(11f, dt));

        Cam.transform.position = new Vector3(pos.x + ox + kick.x, pos.y + oy, pos.z + oz + kick.z);
        Cam.transform.LookAt(new Vector3(target.x + ox * 0.6f, target.y + oy * 0.6f, target.z + oz * 0.6f));
        Cam.transform.Rotate(0f, 0f, Mathf.Sin(t * 1.1f) * shake * 0.012f, Space.Self);

        float fov = 45f / zoom;
        if (punchT > 0f)
        {
            punchT = Mathf.Max(0f, punchT - dt);
            fov -= Mathf.Sin((punchT / 0.2f) * Mathf.PI) * 2.6f * punchAmt;
            if (punchT <= 0f) punchAmt = 0f;
        }
        Cam.fieldOfView = fov;
    }
}
