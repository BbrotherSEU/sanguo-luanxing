using UnityEngine;

/// <summary>采集物：地上光环 + 竖光柱 + 实物，远看是光柱，近看是东西。</summary>
public struct LootRig
{
    public Transform Prop;
    public SpriteRenderer Face, Ring, Beam, SparkA, SparkB;
    public float BeamA;
}

public static class LootVisual
{
    public static LootRig Build(Transform parent, string type, Color col, bool tall)
    {
        Color hot = Color.Lerp(col, Color.white, 0.35f);
        var rig = new LootRig();

        var ringGo = new GameObject("ring");
        ringGo.transform.SetParent(parent, false);
        ringGo.transform.localPosition = new Vector3(0f, 0.06f, 0f);
        ringGo.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
        rig.Ring = ringGo.AddComponent<SpriteRenderer>();
        rig.Ring.sprite = Tex.CenterSprite(Tex.Ring, tall ? 3.8f : 2.3f);
        rig.Ring.color = new Color(hot.r, hot.g, hot.b, 0.9f);
        rig.Ring.sortingOrder = 4;

        var pool = new GameObject("pool");
        pool.transform.SetParent(parent, false);
        pool.transform.localPosition = new Vector3(0f, 0.04f, 0f);
        pool.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
        var poolSr = pool.AddComponent<SpriteRenderer>();
        poolSr.sprite = Tex.CenterSprite(Tex.Glow, tall ? 2.8f : 1.8f);
        poolSr.color = new Color(hot.r, hot.g, hot.b, 0.45f);
        poolSr.sortingOrder = 2;

        var beamGo = new GameObject("beam");
        beamGo.transform.SetParent(parent, false);
        float beamH = tall ? 6.4f : 2.8f;
        beamGo.transform.localPosition = new Vector3(0f, beamH * 0.5f, 0f);
        beamGo.transform.localScale = new Vector3(tall ? 0.85f : 0.55f, beamH, 1f);
        rig.Beam = beamGo.AddComponent<SpriteRenderer>();
        rig.Beam.sprite = Tex.CenterSprite(Tex.Glow, 1f);
        rig.BeamA = tall ? 0.42f : 0.28f;
        rig.Beam.color = new Color(hot.r, hot.g, hot.b, rig.BeamA);
        rig.Beam.sortingOrder = 6;

        var prop = new GameObject("prop");
        prop.transform.SetParent(parent, false);
        rig.Prop = prop.transform;

        float asp;
        var tex = Tex.Cutout("item/" + type + ".jpg", out asp);
        if (tex != null && tex != Tex.White && tex.height > 8)
            rig.Face = Tex.Actor(prop.transform, Tex.SpriteFor(tex, tall ? 1.55f : 1.15f), 20);
        else
            BuildProp(prop.transform, type, hot);

        rig.SparkA = Spark(parent, hot, 0.42f);
        rig.SparkB = Spark(parent, Color.Lerp(hot, Color.white, 0.5f), 0.28f);
        return rig;
    }

    public static void Animate(LootRig rig, Vector3 camPos, float phase)
    {
        float bob = Mathf.Sin(phase * 2.4f) * 0.16f;
        if (rig.Prop != null) rig.Prop.localPosition = new Vector3(0f, bob, 0f);

        Vector3 origin = rig.Prop != null ? rig.Prop.position : camPos;
        Vector3 face = camPos - origin;
        face.y = 0f;
        if (face.sqrMagnitude > 0.01f)
        {
            var rot = Quaternion.LookRotation(face, Vector3.up);
            if (rig.Beam != null) rig.Beam.transform.rotation = rot;
            if (rig.Face != null) rig.Face.transform.rotation = rot;
            if (rig.SparkA != null) rig.SparkA.transform.rotation = rot;
            if (rig.SparkB != null) rig.SparkB.transform.rotation = rot;
        }

        if (rig.Beam != null)
        {
            var c = rig.Beam.color;
            c.a = rig.BeamA * (0.72f + 0.28f * Mathf.Sin(phase * 3.1f));
            rig.Beam.color = c;
        }
        if (rig.Ring != null)
        {
            float s = 1f + Mathf.Sin(phase * 2.1f) * 0.12f;
            rig.Ring.transform.localScale = new Vector3(s, s, 1f);
            var c = rig.Ring.color;
            c.a = 0.72f + Mathf.Sin(phase * 2.1f) * 0.2f;
            rig.Ring.color = c;
        }

        Place(rig.SparkA, phase * 1.7f, 0.85f, 1.15f + bob);
        Place(rig.SparkB, phase * 1.7f + 2.6f, 1.15f, 1.85f + bob);

        int order = Util.SortOrder(origin.z, 8);
        if (rig.Face != null) rig.Face.sortingOrder = order;
        if (rig.Beam != null) rig.Beam.sortingOrder = order - 4;
        if (rig.SparkA != null) rig.SparkA.sortingOrder = order + 2;
        if (rig.SparkB != null) rig.SparkB.sortingOrder = order + 2;
    }

    static void Place(SpriteRenderer sr, float ang, float radius, float y)
    {
        if (sr == null) return;
        sr.transform.localPosition = new Vector3(Mathf.Cos(ang) * radius, y, Mathf.Sin(ang) * radius);
        float p = 0.65f + Mathf.Sin(ang * 2f) * 0.35f;
        var c = sr.color;
        c.a = p;
        sr.color = c;
    }

    static SpriteRenderer Spark(Transform parent, Color col, float size)
    {
        var go = new GameObject("spark");
        go.transform.SetParent(parent, false);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = Tex.CenterSprite(Tex.Star != null ? Tex.Star : Tex.Spark, size);
        sr.color = new Color(col.r, col.g, col.b, 0.9f);
        sr.sortingOrder = 30;
        return sr;
    }

    static void BuildProp(Transform parent, string type, Color hot)
    {
        var kit = new MeshKit();
        switch (type)
        {
            case "scroll":
                kit.Box(new Vector3(0f, 0.22f, 0f), new Vector3(0.72f, 0.16f, 0.28f), new Color(0.93f, 0.86f, 0.62f));
                kit.Cylinder(new Vector3(-0.34f, 0.08f, 0f), 0.09f, 0.09f, 0.42f, 6, new Color(0.45f, 0.28f, 0.16f), new Color(0.62f, 0.4f, 0.22f));
                kit.Cylinder(new Vector3(0.34f, 0.08f, 0f), 0.09f, 0.09f, 0.42f, 6, new Color(0.45f, 0.28f, 0.16f), new Color(0.62f, 0.4f, 0.22f));
                kit.Box(new Vector3(0f, 0.34f, 0f), new Vector3(0.16f, 0.08f, 0.34f), new Color(0.82f, 0.18f, 0.16f));
                break;
            case "ore":
                kit.Blob(new Vector3(0f, 0.28f, 0f), new Vector3(0.42f, 0.32f, 0.36f), new Color(0.35f, 0.4f, 0.48f), 3, 6, 0.28f, 3);
                kit.Blob(new Vector3(0.08f, 0.48f, 0.05f), new Vector3(0.16f, 0.22f, 0.14f), new Color(0.45f, 0.9f, 1f), 2, 5, 0.15f, 7);
                break;
            case "coin":
                kit.Cylinder(new Vector3(0f, 0.02f, 0f), 0.28f, 0.28f, 0.08f, 8, new Color(0.72f, 0.48f, 0.16f), new Color(1f, 0.82f, 0.28f));
                kit.Cylinder(new Vector3(0f, 0.1f, 0f), 0.26f, 0.26f, 0.08f, 8, new Color(0.78f, 0.54f, 0.18f), new Color(1f, 0.88f, 0.36f));
                kit.Cylinder(new Vector3(0f, 0.18f, 0f), 0.24f, 0.24f, 0.08f, 8, new Color(0.84f, 0.6f, 0.2f), new Color(1f, 0.92f, 0.48f));
                break;
            default:
                kit.Cylinder(new Vector3(0f, 0f, 0f), 0.05f, 0.03f, 0.55f, 5, new Color(0.28f, 0.55f, 0.24f), new Color(0.4f, 0.7f, 0.3f));
                kit.Blob(new Vector3(0f, 0.62f, 0f), new Vector3(0.32f, 0.22f, 0.32f), new Color(0.35f, 0.82f, 0.32f), 3, 6, 0.2f, 2);
                kit.Blob(new Vector3(0.16f, 0.5f, 0.08f), new Vector3(0.18f, 0.14f, 0.18f), hot, 2, 5, 0.12f, 5);
                kit.Blob(new Vector3(-0.1f, 0.7f, -0.06f), new Vector3(0.1f, 0.08f, 0.1f), new Color(1f, 0.78f, 0.86f), 2, 4, 0.08f, 9);
                break;
        }
        var mesh = kit.Build("loot", false);
        var go = new GameObject("mesh");
        go.transform.SetParent(parent, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = Look.NewUnlit(0.2f);
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
    }
}
