using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 卡通仙侠场景：顶点色低模 + Toon 色阶，按 Theme 拼出桃林/关隘/秋野/关城。
/// 可走区域保持近乎平坦，起伏只做外圈山势，不破坏 2.5D 移动。
/// </summary>
public class WorldBuilder
{
    public readonly List<Obstacle> Obstacles = new List<Obstacle>();
    public Theme Theme;

    Transform root;
    Camera cam;
    MapDef map;
    MeshKit kit;
    readonly List<Mesh> meshes = new List<Mesh>();
    readonly List<Transform> clouds = new List<Transform>();
    readonly List<Puff> puffs = new List<Puff>();
    readonly List<Card> cards = new List<Card>();
    readonly Dictionary<string, Sprite> folioSpr = new Dictionary<string, Sprite>();
    readonly HashSet<string> folioMiss = new HashSet<string>();
    int fires;
    Material toon, toonOut, waterMat, grassMat, pathMat, cliffMat, rockMat, archMat;
    Material skyMat;
    DayCycle cycle;
    bool battlefield;
    bool wide;
    float mapHalf = 28f;
    public float PlayBound = 21f;
    float t;
    int seed;

    class Puff
    {
        public Transform Tr;
        public SpriteRenderer Sr;
        public Vector3 Vel, Pos;
        public float Life, Max, Spin, Size;
        public Color Col;
    }

    /// <summary>立绘树、草丛。只绕竖直轴朝向镜头，不拧顶点，画上去的叶子不会被拉歪。</summary>
    class Card
    {
        public Transform Tr;
        public float Phase;
        public bool Sway;
    }

    public void Build(Transform parent, MapDef map, Camera cam)
    {
        this.map = map;
        this.cam = cam;
        Theme = Themes.ForDecor(map.Decor);
        battlefield = map.Packs != null && map.Packs.Length > 0;
        wide = map.Id != null && map.Id.StartsWith("world_");
        mapHalf = wide ? 46f : 28f;
        PlayBound = wide ? 38f : 21f;
        root = parent;
        kit = new MeshKit();
        seed = Hash(map.Id);
        toon = Look.NewToon(Theme, false);
        toonOut = Look.NewToon(Theme, true, battlefield ? 0.18f : 0.26f);
        waterMat = Look.NewWater(Theme);
        PaintMaterials();

        Lights();
        Sky();
        Terrain();
        PathRibbon();
        if (HasWater()) River();
        Rocks();
        Trees();
        Grass();
        PathDressing();
        Architecture();
        Landmarks();
        Scenes();
        Ambient();
    }

    public void Tick()
    {
        t += Time.deltaTime;
        if (cycle != null) cycle.Update(Time.deltaTime);
        FaceCards();
        if (cam != null)
        {
            var cp = cam.transform.position;
            for (int i = 0; i < clouds.Count; i++)
            {
                var c = clouds[i];
                if (c == null) continue;
                var p = c.position;
                p.x += 0.35f * Time.deltaTime;
                if (p.x > cp.x + 40f) p.x = cp.x - 40f;
                c.position = p;
            }
        }
        for (int i = puffs.Count - 1; i >= 0; i--)
        {
            var p = puffs[i];
            p.Life -= Time.deltaTime;
            if (p.Life <= 0f || p.Tr == null)
            {
                if (p.Tr != null) Object.Destroy(p.Tr.gameObject);
                puffs.RemoveAt(i);
                continue;
            }
            p.Pos += p.Vel * Time.deltaTime;
            p.Vel.x += Mathf.Sin(t * 1.7f + p.Spin) * 0.4f * Time.deltaTime;
            p.Tr.position = p.Pos;
            p.Tr.Rotate(Vector3.up, p.Spin * Time.deltaTime, Space.World);
            float u = 1f - p.Life / p.Max;
            float a = u < 0.15f ? u / 0.15f : (u > 0.75f ? 1f - (u - 0.75f) / 0.25f : 1f);
            p.Sr.color = new Color(p.Col.r, p.Col.g, p.Col.b, p.Col.a * a);
            if (Theme.Particles == "snow") p.Pos.y = Mathf.Max(0.4f, p.Pos.y);
        }
        if (puffs.Count < PuffCap()) SpawnPuff();
    }

    public void Dispose()
    {
        for (int i = 0; i < meshes.Count; i++)
            if (meshes[i] != null) Object.Destroy(meshes[i]);
        meshes.Clear();
        if (skyMat != null) { Object.Destroy(skyMat); skyMat = null; }
        if (cycle != null && cycle.CloudMats != null)
        {
            for (int i = 0; i < cycle.CloudMats.Count; i++)
                if (cycle.CloudMats[i] != null) Object.Destroy(cycle.CloudMats[i]);
            cycle.CloudMats = null;
        }
        for (int i = 0; i < puffs.Count; i++)
            if (puffs[i].Tr != null) Object.Destroy(puffs[i].Tr.gameObject);
        puffs.Clear();
        clouds.Clear();
        cards.Clear();
        fires = 0;
    }

    /// <summary>设置里改了昼夜 / 时段后立即生效</summary>
    public void SetAmbience(bool autoCycle, float phase01)
    {
        if (cycle == null) return;
        cycle.Auto = autoCycle;
        if (!autoCycle && phase01 >= 0f) cycle.Time01 = phase01;
        cycle.Apply();
        // 雾的远近也按视野设置重算一次
        float vd = Settings.Current != null ? Settings.Current.ViewDistance : 1f;
        RenderSettings.fogStartDistance = (wide ? 52f : Theme.FogStart) * vd;
        RenderSettings.fogEndDistance = (wide ? 128f : Theme.FogEnd) * vd;
    }

    // ---------------- 光 / 天 ----------------

    void Lights()
    {
        var go = new GameObject("sun");
        go.transform.SetParent(root, false);
        var dir = go.AddComponent<Light>();
        dir.type = LightType.Directional;
        dir.color = Theme.Light;
        dir.intensity = Theme.LightIntensity * 1.12f;
        dir.shadows = LightShadows.Soft;
        dir.shadowStrength = 0.45f;
        dir.transform.rotation = Quaternion.Euler(Theme.LightEuler);

        // 昼夜循环接管光照：太阳角度、光色、强度随时间走
        cycle = new DayCycle
        {
            Theme = Theme,
            Sun = dir,
            Cam = cam,
            Auto = Settings.Current == null || Settings.Current.DayCycle,
            DayLength = 300f,
            Time01 = 0.30f,
        };
        if (Settings.Current != null && Settings.Current.DayPhase >= 0f)
            cycle.Time01 = Settings.Current.DayPhase;
        // 登记吃环境光的材质：地面/路面/山石/建筑。
        // 天空与云不加（它们已由 tint 染过一遍，再叠会二次染色）。
        cycle.AmbMats = new List<Material>();
        foreach (var m in new[] { grassMat, pathMat, cliffMat, rockMat, archMat, toon, toonOut })
            if (m != null) cycle.AmbMats.Add(m);
        cycle.Apply();

        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        // 视野距离设置：0.6 拉近（雾更近，看得清）~ 1.6 推远（雾更远，看得广）
        float vd = Settings.Current != null ? Settings.Current.ViewDistance : 1f;
        RenderSettings.fogStartDistance = (wide ? 52f : Theme.FogStart) * vd;
        RenderSettings.fogEndDistance = (wide ? 128f : Theme.FogEnd) * vd;
        if (cam != null)
        {
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Theme.Sky;
            cam.farClipPlane = 220f * vd;
        }
    }

    void Sky()
    {
        kit.Clear();
        const int rings = 7, seg = 16;
        const float rad = 92f;
        for (int r = 0; r < rings; r++)
        {
            float p0 = Mathf.PI * 0.48f * r / rings;
            float p1 = Mathf.PI * 0.48f * (r + 1) / rings;
            Color c0 = SkyCol(p0), c1 = SkyCol(p1);
            for (int s = 0; s < seg; s++)
            {
                float a0 = s * Mathf.PI * 2f / seg;
                float a1 = (s + 1) * Mathf.PI * 2f / seg;
                Vector3 A = Dome(rad, p0, a0), B = Dome(rad, p0, a1);
                Vector3 C = Dome(rad, p1, a1), D = Dome(rad, p1, a0);
                Color col = Color.Lerp(c0, c1, 0.5f);
                // 朝内
                kit.Quad(A, D, C, B, col, Vector3.up * 20f);
            }
        }
        // 天空单独一份材质：昼夜循环靠它的 _Color 染色，不影响共享的地面材质
        skyMat = new Material(toon);
        // 天空不与环境光叠加：它已经由 DayCycle 用 tint 按时段染过一遍，
        // 再吃一份环境光会变成二次染色，天空发灰发脏。
        SetAmb(skyMat, 0f);
        Emit("sky", kit.Build("sky", false), skyMat, false);
        if (cycle != null) cycle.SkyMat = skyMat;

        var rng = new System.Random(seed ^ 11);
        int n = Theme.Id == "dusk" ? 3 : 6;
        var cloudMats = new List<Material>();
        for (int i = 0; i < n; i++)
        {
            kit.Clear();
            float x = (float)(rng.NextDouble() * 70 - 35);
            float z = -18f - (float)rng.NextDouble() * 16f;
            float y = 16f + (float)rng.NextDouble() * 6f;
            Color cloud = Color.Lerp(Color.white, Theme.Sky, 0.25f);
            if (Theme.Id == "dusk") cloud = new Color(1f, 0.72f, 0.58f);
            kit.Sway = 0.05f;
            kit.Blob(new Vector3(x, y, z), new Vector3(4.5f, 1.3f, 2.2f), cloud, 3, 7, 0.22f, seed + i);
            kit.Blob(new Vector3(x + 2.4f, y - 0.3f, z), new Vector3(3.2f, 1.0f, 1.7f), cloud * 0.96f, 3, 6, 0.2f, seed + i + 3);
            kit.Sway = 0f;
            var mesh = kit.Build("cloud" + i, false);
            // 云单独一份材质：昼夜循环按时段染暖色
            var cm = new Material(toon);
            // 云同天空：不叠环境光，避免被二次染色
            SetAmb(cm, 0f);
            var tr = Emit("cloud" + i, mesh, cm, false).transform;
            cloudMats.Add(cm);
            clouds.Add(tr);
        }
        if (cycle != null)
        {
            cycle.CloudMats = cloudMats;
            // 天空和云的材质刚建好，重刷一次光照与色调
            cycle.Apply();
        }
    }

    static Vector3 Dome(float r, float phi, float ang)
        => new Vector3(Mathf.Cos(ang) * Mathf.Cos(phi) * r, Mathf.Sin(phi) * r + 4f, Mathf.Sin(ang) * Mathf.Cos(phi) * r);

    Color SkyCol(float phi)
    {
        float u = Mathf.Clamp01(phi / (Mathf.PI * 0.48f));
        if (Theme.Id == "dusk")
            return u < 0.4f
                ? Color.Lerp(new Color(0.95f, 0.55f, 0.38f), Theme.Sky, u / 0.4f)
                : Color.Lerp(Theme.Sky, new Color(0.28f, 0.22f, 0.42f), (u - 0.4f) / 0.6f);
        return Color.Lerp(Color.Lerp(Theme.Fog, Theme.Sky, 0.35f), Color.Lerp(Theme.Sky, Color.white, 0.35f), u);
    }

    // ---------------- 地形 ----------------

    void PaintMaterials()
    {
        float ow = battlefield ? 0.16f : 0.22f;
        // 地面主层贴图只提供"相对明暗"，色相由 Theme.GrassA/B 的顶点色给，
        // 所以每个地区用同一套结构、不同的季节配色即可，不必各画一张彩色草地。
        string grassFile = "tex/ground_grass_fine.jpg";
        string soft = "tex/ground_soft_meadow.jpg";
        if (Theme.Id == "snow") { grassFile = "tex/ground_grass_snow.jpg"; soft = "tex/ground_soft_snow.jpg"; }
        else if (Theme.Id == "golden") { grassFile = "tex/ground_grass_autumn.jpg"; soft = "tex/ground_soft_autumn.jpg"; }
        else if (Theme.Id == "dusk") { grassFile = "tex/ground_grass_dusk.jpg"; soft = "tex/ground_soft_dusk.jpg"; }
        bool softOk = System.IO.File.Exists(System.IO.Path.Combine(Tex.Root, soft));
        // 软色水彩做超大尺度的低频色块，用来打散平铺的均匀感
        Texture2D gMacro = softOk ? Tex.MirrorGround(soft, 256, 0.06f) : null;
        Texture2D grass = Tex.LoadRaw(grassFile);
        if (grass == Tex.White) grass = Tex.LoadRaw("tex/cartoon_grass.jpg");
        Texture2D path = Tex.LoadRaw("tex/ground_road_dirt.jpg");
        Texture2D rock = Tex.LoadRaw("tex/rock_neutral.jpg");
        Texture2D wood = Tex.LoadRaw("tex/cartoon_wood.jpg");
        Texture2D roof = Tex.LoadRaw("tex/roof_tile_neutral.jpg");
        Texture2D stone = Tex.LoadRaw("tex/cartoon_stone.jpg");
        Texture2D wall = Tex.LoadRaw("tex/wall_plaster.jpg");
        bool ok = grass != Tex.White && path != Tex.White && rock != Tex.White && wood != Tex.White
                  && roof != Tex.White && stone != Tex.White && wall != Tex.White;
        if (!ok)
        {
            grassMat = pathMat = cliffMat = toon;
            rockMat = archMat = toonOut;
            return;
        }
        // _TexTile 是"每米重复几次"：0.22 ≈ 4.5 米一铺。
        // 太小（0.17）会把 1024px 摊到 6 米，草叶只剩几厘米、远看全被 mipmap 抹平；
        // 太大则重复得慢，30 米内就能看出贴图在重复。4.5 米是两者之间的折中。
        // 低频层 0.05 ≈ 20 米一铺，水彩图自带不规则色块，负责大尺度变化、打散平铺感。
        grassMat = Look.NewPainted(Theme, false, ow, grass, 0.80f, 0.22f, gMacro, 0.05f, 0.62f, 0f, 3f, 1.25f);
        pathMat = Look.NewPainted(Theme, false, ow, path, 0.72f, 0.26f, null, 1f, 0f, 0f, 3f, 1.2f);
        cliffMat = Look.NewPainted(Theme, false, ow, rock, 0.82f, 0.30f, rock, 0.5f, 0.25f, 0.7f, 3f, 1.2f);
        rockMat = Look.NewPainted(Theme, true, ow, rock, 0.88f, 0.42f, rock, 0.9f, 0.25f, 0.7f, 3f, 1.2f);
        // 墙、瓦、木、石四套贴图共用一份材质，靠顶点色分类；
        // 贴图只给明暗细节，色相仍由 Theme 控制（青瓦/红瓦/粉墙各自不同）。
        // _TexTile 0.34 ≈ 3 米一铺：夯土层理、露砖斑块在这个尺度下才像"墙"，
        // 而不是一堆挤在一起的小格子。
        archMat = Look.NewArch(Theme, ow, wall, wood, roof, stone, 0.85f, 0.34f);

        // 环境光权重：地面占画面最大，环境光给足才不会在背光面发死。
        // 石头/建筑略低（本来就该硬朗一点），路面中等。
        SetAmb(grassMat, 1.00f);
        SetAmb(pathMat, 0.90f);
        SetAmb(cliffMat, 0.95f);
        SetAmb(rockMat, 0.80f);
        SetAmb(archMat, 0.75f);
    }

    /// <summary>设置材质的方向性环境光权重（着色器 _AmbStrength）。</summary>
    void SetAmb(Material m, float v)
    {
        if (m != null && m.HasProperty("_AmbStrength")) m.SetFloat("_AmbStrength", v);
    }

    void Terrain()
    {
        int n = wide ? 72 : 64;
        float half = mapHalf;
        float step = (half * 2f) / n;
        var h = new float[n + 1, n + 1];
        var col = new Color[n + 1, n + 1];
        var kind = new int[n + 1, n + 1];
        for (int iz = 0; iz <= n; iz++)
        for (int ix = 0; ix <= n; ix++)
        {
            float x = -half + ix * step;
            float z = -half + iz * step;
            h[ix, iz] = HeightAt(x, z);
            col[ix, iz] = GroundCol(x, z);
            kind[ix, iz] = GroundKind(x, z);
        }
        EmitGround(n, half, step, h, col, kind, 0, grassMat, "ground");
        EmitGround(n, half, step, h, col, kind, 1, pathMat, "path");
        EmitGround(n, half, step, h, col, kind, 2, cliffMat, "cliff");
    }

    void EmitGround(int n, float half, float step, float[,] h, Color[,] col, int[,] kind, int want, Material mat, string name)
    {
        kit.Clear();
        for (int iz = 0; iz < n; iz++)
        for (int ix = 0; ix < n; ix++)
        {
            int g = 0, p = 0, s = 0;
            CountKind(kind[ix, iz], ref g, ref p, ref s);
            CountKind(kind[ix + 1, iz], ref g, ref p, ref s);
            CountKind(kind[ix, iz + 1], ref g, ref p, ref s);
            CountKind(kind[ix + 1, iz + 1], ref g, ref p, ref s);
            int pick = p >= 2 ? 1 : (s >= 2 ? 2 : 0);
            if (pick == 1) pick = 0; // 路铺在草上，单独用平滑条带画，避免格子台阶
            if (pick != want) continue;
            float x0 = -half + ix * step, z0 = -half + iz * step;
            float x1 = x0 + step, z1 = z0 + step;
            Vector3 a = new Vector3(x0, h[ix, iz], z0);
            Vector3 b = new Vector3(x1, h[ix + 1, iz], z0);
            Vector3 c = new Vector3(x1, h[ix + 1, iz + 1], z1);
            Vector3 d = new Vector3(x0, h[ix, iz + 1], z1);
            kit.QuadColors(a, b, c, d, col[ix, iz], col[ix + 1, iz], col[ix + 1, iz + 1], col[ix, iz + 1], new Vector3(0f, -8f, 0f));
        }
        if (kit.Count > 0) Emit(name, kit.Build(name, false, true), mat, true);
    }

    static void CountKind(int k, ref int grass, ref int path, ref int stone)
    {
        if (k == 1) path++;
        else if (k == 2) stone++;
        else grass++;
    }

    int GroundKind(float x, float z)
    {
        if (OnPath(x, z)) return 1;
        // 与 HeightAt 用同一条"可走区"边界，避免石头层和地形起伏对不上
        float edge = Mathf.InverseLerp(PlayBound, mapHalf, Mathf.Max(Mathf.Abs(x), Mathf.Abs(z)));
        if (edge > 0.42f) return 2;
        if (Theme.SnowCaps > 0.5f && HeightAt(x, z) > 1.6f) return 2;
        return 0;
    }

    float HeightAt(float x, float z)
    {
        float ax = Mathf.Abs(x), az = Mathf.Abs(z);
        // 起伏从可走区之外才开始。
        // 2.5D 俯视下角色是贴地走的，可走区一旦有起伏，角色就会陷进地面或悬空，
        // 而且战斗是横版手感（角色 y 恒为 0），起伏会直接影响操作。
        // 所以让可走区保持绝对平坦，起伏只出现在视野中景（相机看向的前方/外圈），
        // 那才是"中景层次"真正起作用的地方。
        float edgeIn = PlayBound;
        float edge = Mathf.InverseLerp(edgeIn, mapHalf, Mathf.Max(ax, az));
        float n = Fbm(x * 0.12f, z * 0.12f);
        float hills = edge * edge * (2.2f + n * 2.8f + Theme.PeakAmount * 1.4f);
        if (OnPath(x, z)) return 0.03f + edge * 0.4f;
        if (OnRiver(x, z)) return -0.08f;
        // 中景缓坡：低频（0.055）保证坡度平缓，出可走区后逐渐长起来，
        // 让画面里的中景带不是一块平板。
        float roll = Fbm(x * 0.055f + 31.7f, z * 0.055f - 17.3f);
        float roll2 = Fbm(x * 0.021f - 8.1f, z * 0.021f + 5.5f);
        float mid = (roll - 0.5f) * 0.52f + (roll2 - 0.5f) * 0.26f;
        return 0.02f + n * 0.045f + mid * edge + hills;
    }

    /// <summary>
    /// 物件贴地高度：取该点的地形高度，再往下沉一点点让底部扎进地里，
    /// 避免看起来像浮在坡面上。
    /// 之前所有树/草/石头都直接放 y=0，外圈地形高 0.5~1.2m，
    /// 于是外圈的石头埋进山坡、草悬空 —— 这里统一按地形落位。
    /// </summary>
    float GroundY(float x, float z, float sink = 0.02f)
        => HeightAt(x, z) - sink;

    /// <summary>该点的地面高度（对外：角色贴地走用）。</summary>
    public float GroundHeight(float x, float z) => HeightAt(x, z);

    Color GroundCol(float x, float z)
    {
        if (OnRiver(x, z)) return Theme.Sand;
        if (OnPath(x, z)) return Theme.Path;
        float n = Fbm(x * 0.07f + 8f, z * 0.07f);
        Color g = Color.Lerp(Theme.GrassA, Theme.GrassB, n);
        if (n > 0.72f) g = Color.Lerp(g, Theme.GrassDry, 0.45f);
        float edgeIn = wide ? 34f : 17f;
        float edge = Mathf.InverseLerp(edgeIn, mapHalf, Mathf.Max(Mathf.Abs(x), Mathf.Abs(z)));
        if (edge > 0.2f) g = Color.Lerp(g, Theme.Cliff, (edge - 0.2f) * 0.85f);
        if (Theme.SnowCaps > 0.5f && HeightAt(x, z) > 1.6f) g = Color.Lerp(g, Theme.Peak, 0.8f);
        return g;
    }

    bool HasWater() => Theme.Id == "peachgrove" || Theme.Id == "village" || Theme.Id == "wild";

    bool OnRiver(float x, float z)
    {
        if (!HasWater()) return false;
        float rz = -1.8f + Mathf.Sin(x * 0.16f) * 2.4f + Mathf.Sin(x * 0.41f) * 0.6f;
        float reach = wide ? 36f : 20f;
        return Mathf.Abs(z - rz) < 1.55f && Mathf.Abs(x) < reach;
    }

    bool OnPath(float x, float z)
    {
        // 南北官道
        float zlim = wide ? 36f : 20f;
        if (Mathf.Abs(x) < 2.25f && Mathf.Abs(z) < zlim && !OnRiver(x, z)) return true;
        if (Theme.Id == "peachgrove" && Mathf.Abs(z - 8f) < 1.65f && x > -2f && x < 10f) return true;
        if (Theme.Id == "fortress" && Mathf.Abs(x) < 2.6f) return true;
        return false;
    }

    void PathRibbon()
    {
        float end = wide ? 34f : 18f;
        Road(new Vector3(0f, 0f, -end), new Vector3(0f, 0f, end), 4.5f);
        if (Theme.Id == "peachgrove")
            Road(new Vector3(-2f, 0f, 8f), new Vector3(10f, 0f, 8f), 3.3f);
    }

    void Road(Vector3 from, Vector3 to, float width)
    {
        kit.Clear();
        Vector3 dir = to - from;
        float len = dir.magnitude;
        if (len < 0.1f) return;
        dir /= len;
        Vector3 side = new Vector3(-dir.z, 0f, dir.x);
        const int seg = 32;
        Color mid = Theme.Path;
        Color edge = Color.Lerp(Theme.Path, Theme.GrassA, 0.42f);
        for (int i = 0; i < seg; i++)
        {
            float t0 = i / (float)seg, t1 = (i + 1) / (float)seg;
            Vector3 p0 = from + dir * (len * t0);
            Vector3 p1 = from + dir * (len * t1);
            if (OnRiver(p0.x, p0.z) || OnRiver(p1.x, p1.z)) continue;
            float y = 0.07f;
            Vector3 a = p0 - side * width * 0.5f; a.y = y;
            Vector3 b = p0; b.y = y;
            Vector3 c = p1; c.y = y;
            Vector3 d = p1 - side * width * 0.5f; d.y = y;
            Vector3 e = p0 + side * width * 0.5f; e.y = y;
            Vector3 f = p1 + side * width * 0.5f; f.y = y;
            kit.QuadColors(a, b, c, d, edge, mid, mid, edge, new Vector3(0f, -4f, 0f));
            kit.QuadColors(b, e, f, c, mid, edge, edge, mid, new Vector3(0f, -4f, 0f));
        }
        if (kit.Count > 0) Emit("road", kit.Build("road", false, true), pathMat, true);
    }

    void River()
    {
        kit.Clear();
        float span = wide ? 36f : 20f;
        int n = wide ? 40 : 28;
        for (int i = 0; i < n; i++)
        {
            float x0 = -span + span * 2f * i / n;
            float x1 = -span + span * 2f * (i + 1) / n;
            float z0 = -1.8f + Mathf.Sin(x0 * 0.16f) * 2.4f;
            float z1 = -1.8f + Mathf.Sin(x1 * 0.16f) * 2.4f;
            const float w = 1.5f;
            Vector3 a = new Vector3(x0, 0.05f, z0 - w);
            Vector3 b = new Vector3(x1, 0.05f, z1 - w);
            Vector3 c = new Vector3(x1, 0.05f, z1 + w);
            Vector3 d = new Vector3(x0, 0.05f, z0 + w);
            float shore = (i == 0 || i == n - 1) ? 1f : 0.15f;
            kit.Quad(a, b, c, d, new Color(shore, 1f, 1f, 1f), new Vector3(0f, -2f, 0f));
        }
        Emit("water", kit.Build("water", false), waterMat, false);

        kit.Clear();
        const float inner = 1.45f, outer = 2.85f;
        for (int i = 0; i < n; i++)
        {
            float x0 = -span + span * 2f * i / n;
            float x1 = -span + span * 2f * (i + 1) / n;
            float z0 = -1.8f + Mathf.Sin(x0 * 0.16f) * 2.4f;
            float z1 = -1.8f + Mathf.Sin(x1 * 0.16f) * 2.4f;
            Color sand = Color.Lerp(Theme.Sand, Theme.GrassA, 0.25f);
            Shore(x0, z0, x1, z1, inner, outer, sand);
            Shore(x0, z0, x1, z1, -outer, -inner, sand);
        }
        if (kit.Count > 0) Emit("shore", kit.Build("shore", false, true), pathMat, true);
    }

    void Shore(float x0, float z0, float x1, float z1, float w0, float w1, Color col)
    {
        Vector3 a = new Vector3(x0, 0.045f, z0 + w0);
        Vector3 b = new Vector3(x1, 0.045f, z1 + w0);
        Vector3 c = new Vector3(x1, 0.045f, z1 + w1);
        Vector3 d = new Vector3(x0, 0.045f, z0 + w1);
        kit.Quad(a, b, c, d, col, new Vector3(0f, -2f, 0f));
    }

    // ---------------- 植被 ----------------

    void Trees()
    {
        kit.Clear();
        var rng = new System.Random(seed ^ 0xC0FFEE);
        int n = Mathf.RoundToInt((wide ? 52f : 26f) * Theme.TreeDensity * (battlefield ? 0.7f : 1f));
        int placed = 0, tries = 0;
        float reach = wide ? 34f : 20f;
        var spots = new List<Vector2>(n);
        bool anyMesh = false;
        while (placed < n && tries < n * 18)
        {
            tries++;
            float x = (float)(rng.NextDouble() * reach * 2f - reach);
            float z = (float)(rng.NextDouble() * reach * 2f - reach);
            if (!TryPlant(rng, spots, ref placed, ref anyMesh, x, z, reach, null)) continue;
            if (placed >= n || rng.NextDouble() > 0.62) continue;
            string kind = Theme.Trees[rng.Next(Theme.Trees.Length)];
            float gap = kind == "bamboo" ? 2.1f : 3.3f;
            for (int k = 0; k < 2 && placed < n; k++)
            {
                float ang = (float)(rng.NextDouble() * Mathf.PI * 2f);
                float dist = gap + (float)rng.NextDouble() * 1.2f;
                TryPlant(rng, spots, ref placed, ref anyMesh, x + Mathf.Cos(ang) * dist, z + Mathf.Sin(ang) * dist, reach, kind);
            }
        }
        if (anyMesh && kit.Count > 0) Emit("trees", kit.Build("trees", true), toonOut, true);
    }

    bool TryPlant(System.Random rng, List<Vector2> spots, ref int placed, ref bool anyMesh, float x, float z, float reach, string forceKind)
    {
        if (KeepClear(x, z, 2.6f) || NearPack(x, z, 3.0f)) return false;
        if (OnPath(x, z) || OnRiver(x, z)) return false;
        if (Mathf.Max(Mathf.Abs(x), Mathf.Abs(z)) > reach) return false;
        string kind = forceKind ?? Theme.Trees[rng.Next(Theme.Trees.Length)];
        float gap = kind == "bamboo" ? 1.9f : 3.05f;
        if (NearSpot(spots, x, z, gap)) return false;
        float h = TreeHeight(kind, rng);
        // 按地形高度落位：树根要贴着坡面，否则外圈的树会浮空或半截埋进山
        var p = new Vector3(x, GroundY(x, z, 0.05f), z);
        if (!PlaceTree(kind, p, h))
        {
            Tree(kind, p, rng.Next());
            anyMesh = true;
        }
        spots.Add(new Vector2(x, z));
        Obstacles.Add(new Obstacle(x, z, kind == "bamboo" ? 0.32f : 0.55f));
        placed++;
        return true;
    }

    void Tree(string kind, Vector3 p, int s)
    {
        var rng = new System.Random(s);
        float h = 2.5f + (float)rng.NextDouble() * 1.6f;
        switch (kind)
        {
            case "peach": Peach(p, h, s); break;
            case "pine": Pine(p, h, s); break;
            case "bamboo": Bamboo(p, h, s); break;
            case "autumn": RoundTree(p, h, s, Theme.Leaf1, Theme.Leaf2); break;
            case "dead": Dead(p, h, s); break;
            default: RoundTree(p, h, s, Theme.Leaf1, Theme.Leaf2); break;
        }
    }

    void Peach(Vector3 p, float h, int s)
    {
        kit.Sway = 0f;
        kit.Cylinder(p, 0.16f, 0.09f, h * 0.52f, 6, Theme.Trunk, Theme.Trunk, 0.12f, s);
        kit.Sway = 0.28f;
        kit.Blob(p + Vector3.up * (h * 0.68f), new Vector3(1.7f, 1.15f, 1.7f), Theme.Blossom1, 4, 8, 0.08f, s);
        kit.Blob(p + Vector3.up * (h * 0.9f) + new Vector3(0.5f, 0f, 0.2f),
            new Vector3(1.15f, 0.9f, 1.15f), Theme.Blossom2, 3, 6, 0.2f, s + 2);
        kit.Sway = 0.4f;
        kit.Blob(p + Vector3.up * (h * 0.62f) + new Vector3(-0.5f, 0f, 0.3f),
            new Vector3(0.7f, 0.5f, 0.7f), Theme.Leaf2, 3, 5, 0.18f, s + 5);
        kit.Sway = 0f;
    }

    void Pine(Vector3 p, float h, int s)
    {
        kit.Sway = 0f;
        kit.Cylinder(p, 0.15f, 0.09f, h * 0.42f, 6, Theme.Trunk, Theme.Trunk, 0.08f, s);
        kit.Sway = 0.25f;
        int layers = 4;
        for (int i = 0; i < layers; i++)
        {
            float y = h * 0.32f + i * h * 0.2f;
            float r = 1.35f - i * 0.26f;
            kit.Cylinder(p + Vector3.up * y, r, 0.08f, h * 0.26f, 7, Theme.Leaf1, Theme.Leaf3, 0.14f, s + i);
        }
        kit.Sway = 0f;
    }

    void RoundTree(Vector3 p, float h, int s, Color a, Color b)
    {
        kit.Sway = 0f;
        kit.Cylinder(p, 0.18f, 0.1f, h * 0.5f, 6, Theme.Trunk, Theme.Trunk, 0.1f, s);
        kit.Sway = 0.28f;
        kit.Blob(p + Vector3.up * (h * 0.72f), new Vector3(1.5f, 1.2f, 1.5f), a, 4, 7, 0.08f, s);
        kit.Blob(p + Vector3.up * (h * 0.9f) + new Vector3(0.4f, 0f, -0.2f),
            new Vector3(1.0f, 0.8f, 1.0f), b, 3, 6, 0.18f, s + 4);
        kit.Sway = 0f;
    }

    void Bamboo(Vector3 p, float h, int s)
    {
        var rng = new System.Random(s);
        kit.Sway = 0.15f;
        int stalks = 3 + rng.Next(3);
        for (int i = 0; i < stalks; i++)
        {
            Vector3 o = p + new Vector3((float)(rng.NextDouble() - 0.5f) * 0.7f, 0f, (float)(rng.NextDouble() - 0.5f) * 0.7f);
            float hh = h * (0.85f + (float)rng.NextDouble() * 0.5f);
            kit.Cylinder(o, 0.05f, 0.04f, hh, 5, new Color(0.42f, 0.62f, 0.32f), Theme.Leaf2, 0.05f, s + i);
            kit.Sway = 0.7f;
            kit.Blob(o + Vector3.up * hh, new Vector3(0.35f, 0.28f, 0.35f), Theme.Leaf2, 2, 5, 0.15f, s + i + 9);
            kit.Sway = 0.15f;
        }
        kit.Sway = 0f;
    }

    void Dead(Vector3 p, float h, int s)
    {
        kit.Sway = 0f;
        kit.Cylinder(p, 0.14f, 0.05f, h * 0.9f, 5, Theme.Trunk, Theme.Trunk, 0.2f, s);
        kit.Cylinder(p + Vector3.up * (h * 0.55f), 0.05f, 0.02f, h * 0.45f, 4, Theme.Trunk, Theme.Trunk, 0.15f, s + 1, 35f);
        kit.Sway = 0f;
    }

    void Grass()
    {
        kit.Clear();
        var rng = new System.Random(seed ^ 91);
        string main = GrassFile();
        int n = Mathf.RoundToInt((wide ? 300f : 170f) * Theme.GrassDensity * (battlefield ? 0.62f : 1f));
        float reach = wide ? 33f : 19f;
        var cells = new HashSet<int>();
        bool anyMesh = false;
        kit.Sway = 0.4f;
        int patches = Mathf.Max(6, n / 11);
        float flower = Theme.Id == "peachgrove" ? 0.34f : (Theme.Id == "village" ? 0.2f : 0.08f * Theme.FlowerDensity);
        for (int p = 0; p < patches; p++)
        {
            float cx = (float)(rng.NextDouble() * reach * 2f - reach);
            float cz = (float)(rng.NextDouble() * reach * 2f - reach);
            if (OnPath(cx, cz) || OnRiver(cx, cz) || KeepClear(cx, cz, 1.5f)) continue;
            int blades = 7 + rng.Next(6);
            for (int i = 0; i < blades; i++)
            {
                float ang = (float)(rng.NextDouble() * Mathf.PI * 2f);
                float dist = (float)rng.NextDouble() * 2.15f;
                float x = cx + Mathf.Cos(ang) * dist;
                float z = cz + Mathf.Sin(ang) * dist;
                if (OnPath(x, z) || OnRiver(x, z) || KeepClear(x, z, 1.15f)) continue;
                if (!cells.Add(CellKey(x, z))) continue;
                float h = 0.78f + (float)rng.NextDouble() * 0.42f;
                bool bloom = rng.NextDouble() < flower;
                string file = bloom ? "tex/foliage/grass_flower.jpg" : main;
                // 草按地形高度落位：贴着坡面长，否则外圈的草会浮在半空
                if (!PlaceCard(file, new Vector3(x, GroundY(x, z, 0.01f), z), h, true, -8, 0f))
                {
                    Color c = Color.Lerp(Theme.GrassA, Theme.GrassB, (float)rng.NextDouble());
                    kit.Tuft(new Vector3(x, GroundY(x, z, 0.01f), z), h * 0.85f, c, 5, rng.Next());
                    anyMesh = true;
                }
            }
        }
        kit.Sway = 0f;
        if (anyMesh && kit.Count > 0) Emit("grass", kit.Build("grass", false), toon, false);
    }

    float TreeHeight(string kind, System.Random rng)
    {
        float u = (float)rng.NextDouble();
        switch (kind)
        {
            case "pine": return 6.6f + u * 1.8f;
            case "bamboo": return 5.4f + u * 1.3f;
            case "dead": return 4.8f + u * 1.3f;
            default: return 5.0f + u * 1.4f;
        }
    }

    string TreeFile(string kind)
    {
        if (Theme.SnowCaps > 0.5f && (kind == "pine" || kind == "round")) return "tex/foliage/tree_snow.jpg";
        switch (kind)
        {
            case "peach": return "tex/tree_peach.jpg";
            case "pine": return "tex/foliage/tree_pine.jpg";
            case "bamboo": return "tex/foliage/tree_bamboo.jpg";
            case "autumn": return "tex/foliage/tree_autumn.jpg";
            case "dead": return "tex/foliage/tree_dead.jpg";
            default: return "tex/foliage/tree_round.jpg";
        }
    }

    string GrassFile()
    {
        switch (Theme.Id)
        {
            case "snow": return "tex/foliage/grass_snow.jpg";
            case "golden": return "tex/foliage/grass_dry.jpg";
            case "dusk": return "tex/foliage/grass_dusk.jpg";
            default: return "tex/foliage/grass_green.jpg";
        }
    }

    bool PlaceTree(string kind, Vector3 p, float h) => PlaceCard(TreeFile(kind), p, h, false, 1, h * 0.38f);

    bool PlaceCard(string rel, Vector3 p, float height, bool sway, int bias, float shadow)
    {
        var spr = FolioSprite(rel, height);
        if (spr == null) return false;
        var go = new GameObject("card");
        go.transform.SetParent(root, false);
        // 用 p.y：调用方已经把物件按地形高度落位了，这里再抹平就会浮空
        go.transform.position = new Vector3(p.x, p.y, p.z);
        if (cam != null)
        {
            Vector3 f = cam.transform.forward;
            f.y = 0f;
            if (f.sqrMagnitude > 1e-6f)
                go.transform.rotation = Quaternion.LookRotation(-f.normalized, Vector3.up);
        }
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = spr;
        sr.sharedMaterial = Look.CardSprite;
        sr.sortingOrder = Util.SortOrder(p.z, bias);
        cards.Add(new Card { Tr = go.transform, Phase = p.x * 1.7f + p.z * 0.8f, Sway = sway });
        if (shadow > 0.2f)
        {
            var sh = new GameObject("shade");
            sh.transform.SetParent(root, false);
            // 阴影跟着物件的地面高度，不然物件贴坡后影子会浮在坡面上方
            sh.transform.position = new Vector3(p.x, p.y + 0.04f, p.z);
            sh.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);
            var srr = sh.AddComponent<SpriteRenderer>();
            srr.sprite = Tex.CenterSprite(Tex.Shadow, shadow);
            srr.color = new Color(0f, 0f, 0f, 0.28f);
            srr.sortingOrder = Util.SortOrder(p.z, -24);
        }
        return true;
    }

    /// <summary>建筑贴地阴影：软黑椭圆，让房子/亭子"坐"在地上，不再像浮着的贴纸。</summary>
    void BuildShadow(Vector3 p, float radius, float alpha)
    {
        var sh = new GameObject("bshade");
        sh.transform.SetParent(root, false);
        // 建筑几何都以 p 为基准往上传，所以影子贴着 p 的地面高度放。
        // 建筑都在内圈、地形几乎平（±4.5cm），这里用 p.y 就够了。
        sh.transform.position = new Vector3(p.x, p.y + 0.04f, p.z);
        sh.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);
        var sr = sh.AddComponent<SpriteRenderer>();
        sr.sprite = Tex.CenterSprite(Tex.Shadow, radius);
        sr.color = new Color(0f, 0f, 0f, alpha);
        sr.sortingOrder = Util.SortOrder(p.z, -24);
    }

    Sprite FolioSprite(string rel, float height)
    {
        if (folioMiss.Contains(rel)) return null;
        height = Mathf.Max(0.4f, Mathf.Round(height * 10f) / 10f);
        string key = rel + "@" + height.ToString("F1");
        if (folioSpr.TryGetValue(key, out var cached)) return cached;
        if (!System.IO.File.Exists(System.IO.Path.Combine(Tex.Root, rel)))
        {
            folioMiss.Add(rel);
            return null;
        }
        var tex = Tex.Cutout(rel, out _);
        if (tex == null || tex == Tex.White || tex.height < 24)
        {
            folioMiss.Add(rel);
            return null;
        }
        var spr = Tex.SpriteFor(tex, height);
        folioSpr[key] = spr;
        return spr;
    }

    void FaceCards()
    {
        if (cam == null || cards.Count == 0) return;
        Vector3 f = cam.transform.forward;
        f.y = 0f;
        if (f.sqrMagnitude < 1e-6f) return;
        Quaternion face = Quaternion.LookRotation(-f.normalized, Vector3.up);
        for (int i = 0; i < cards.Count; i++)
        {
            var c = cards[i];
            if (c.Tr == null) continue;
            if (!c.Sway) { c.Tr.rotation = face; continue; }
            float roll = Mathf.Sin(t * 1.55f + c.Phase) * 4.2f;
            c.Tr.rotation = face * Quaternion.Euler(0f, 0f, roll);
        }
    }

    static bool NearSpot(List<Vector2> spots, float x, float z, float d)
    {
        float d2 = d * d;
        for (int i = 0; i < spots.Count; i++)
        {
            float dx = spots[i].x - x, dz = spots[i].y - z;
            if (dx * dx + dz * dz < d2) return true;
        }
        return false;
    }

    static int CellKey(float x, float z)
    {
        int ix = Mathf.RoundToInt(x / 0.82f);
        int iz = Mathf.RoundToInt(z / 0.82f);
        return ix * 73856093 ^ iz * 19349663;
    }

    void PathDressing()
    {
        kit.Clear();
        float end = wide ? 30f : 16f;
        for (float z = -end; z <= end; z += 1.45f)
        {
            if (OnRiver(0f, z)) continue;
            float s = 0.16f + Mathf.Abs(Mathf.Sin(z * 1.7f)) * 0.1f;
            // 路边石按官道高度落位（官道在 OnPath 分支里单独算高度）
            float gl = GroundY(-2.55f, z, s * 0.2f);
            float gr = GroundY(2.55f, z + 0.45f, s * 0.2f);
            kit.Blob(new Vector3(-2.55f, gl + s * 0.28f, z), new Vector3(s, s * 0.42f, s * 0.75f), Theme.Rock, 2, 5, 0.16f, (int)(z * 9f));
            kit.Blob(new Vector3(2.55f, gr + s * 0.26f, z + 0.45f), new Vector3(s * 0.85f, s * 0.38f, s * 0.7f), Theme.Cliff, 2, 5, 0.14f, (int)(z * 9f + 4f));
        }
        if (kit.Count > 0) Emit("pathstones", kit.Build("pathstones", true), rockMat != null ? rockMat : toonOut, true);
    }

    void Rocks()
    {
        kit.Clear();
        var rng = new System.Random(seed ^ 33);
        int n = battlefield ? 8 : (wide ? 26 : 14);
        float reach = wide ? 36f : 22f;
        for (int i = 0; i < n; i++)
        {
            float x = (float)(rng.NextDouble() * reach * 2f - reach);
            float z = (float)(rng.NextDouble() * reach * 2f - reach);
            if (KeepClear(x, z, 4f) || OnPath(x, z) || OnRiver(x, z) || NearPack(x, z, 3f)) continue;
            float s = 0.35f + (float)rng.NextDouble() * 0.7f;
            Color c = Color.Lerp(Theme.Rock, Theme.Cliff, (float)rng.NextDouble() * 0.5f);
            // 按地形高度落位：外圈山坡高 0.5~1.2m，固定 y 会把石头埋进坡里
            float gy = GroundY(x, z, s * 0.15f);
            kit.Blob(new Vector3(x, gy + s * 0.35f, z), new Vector3(s, s * 0.55f, s * 0.8f), c, 3, 6, 0.22f, rng.Next());
            if (Theme.SnowCaps > 0.5f)
                kit.Blob(new Vector3(x, gy + s * 0.7f, z), new Vector3(s * 0.45f, s * 0.18f, s * 0.4f), Theme.Peak, 2, 5, 0.1f, rng.Next());
            Obstacles.Add(new Obstacle(x, z, s * 0.55f));
        }
        Emit("rocks", kit.Build("rocks", true), rockMat, true);
    }

    // ---------------- 建筑 ----------------

    void Architecture()
    {
        kit.Clear();
        switch (Theme.Id)
        {
            case "peachgrove":
            case "village":
                if (!battlefield)
                {
                    House(new Vector3(-12.5f, 0f, 11.5f), 18f, 1f, seed + 1);
                    House(new Vector3(-8.2f, 0f, 14.8f), -12f, 0.85f, seed + 2);
                    Pavilion(new Vector3(9.5f, 0f, 8.2f));
                    Well(new Vector3(-5.5f, 0f, 9.2f));
                    Lantern(new Vector3(2.4f, 0f, 6.5f));
                    Lantern(new Vector3(-2.4f, 0f, 6.5f));
                    Bridge();
                }
                else
                {
                    Pavilion(new Vector3(11f, 0f, -8f));
                    Lantern(new Vector3(3f, 0f, 10f));
                }
                break;
            case "snow":
                Gatehouse(new Vector3(0f, 0f, -11f));
                Watch(new Vector3(-13f, 0f, -10f));
                Watch(new Vector3(13f, 0f, -10f));
                break;
            case "golden":
                RuinWall(new Vector3(-10f, 0f, -6f), 8f, 70f);
                RuinWall(new Vector3(9f, 0f, 8f), 6f, -20f);
                Pillar(new Vector3(-6f, 0f, 4f), 1.8f, true);
                Pillar(new Vector3(5.5f, 0f, -3f), 2.4f, false);
                break;
            case "dusk":
                Fortress();
                break;
            case "wild":
                if (!battlefield)
                {
                    Well(new Vector3(-6f, 0f, 8f));
                    Lantern(new Vector3(2.2f, 0f, 7f));
                    Lantern(new Vector3(-2.2f, 0f, 7f));
                    House(new Vector3(11f, 0f, 12f), 20f, 0.8f, seed + 4);
                }
                break;
        }
        if (wide && !battlefield)
        {
            Pavilion(new Vector3(8.5f, 0f, 26f));
            Lantern(new Vector3(6f, 0f, 24f));
            Lantern(new Vector3(-6f, 0f, 24f));
        }
        Emit("arch", kit.Build("arch", true), archMat, true);
    }

    void House(Vector3 p, float yaw, float sc, int s)
    {
        float w = 3.4f * sc, d = 2.7f * sc, h = 2.15f * sc;
        Quaternion r = Quaternion.Euler(0f, yaw, 0f);
        Color wood = Theme.Wood;
        Color woodDark = Color.Lerp(Theme.Wood, Color.black, 0.28f);
        // 石脚（砖石基座）：粉墙落在石头上了，房子才站得住
        kit.Box(p + Vector3.up * (0.20f * sc), new Vector3(w * 1.06f, 0.40f * sc, d * 1.06f), Theme.Stone, Theme.Stone, yaw);
        kit.Box(p + Vector3.up * (h * 0.5f), new Vector3(w, h, d), Theme.Wall, Color.Lerp(Theme.Wall, Color.white, 0.12f), yaw);
        // 檐下木枋 + 墙裙：一条横木把白墙断开，墙面才有层次
        kit.Box(p + Vector3.up * (h - 0.14f * sc), new Vector3(w * 1.03f, 0.16f * sc, d * 1.03f), woodDark, null, yaw);
        kit.Box(p + Vector3.up * (0.56f * sc), new Vector3(w * 1.02f, 0.12f * sc, d * 1.02f), woodDark, null, yaw);
        kit.Roof(p + Vector3.up * h, w * 1.18f, d * 1.18f, 1.35f * sc, Theme.RoofB, Theme.Lacquer, yaw, 0.28f * sc);

        // 门：门框 + 门板 + 门簪，比贴一块木板有层次
        float fz = d * 0.5f + 0.06f * sc;
        kit.Box(p + r * new Vector3(0f, 0.72f * sc, fz), new Vector3(0.58f * sc, 1.40f * sc, 0.07f), woodDark, null, yaw);
        kit.Box(p + r * new Vector3(0f, 0.72f * sc, fz + 0.05f * sc), new Vector3(0.42f * sc, 1.24f * sc, 0.05f), wood, null, yaw);
        kit.Box(p + r * new Vector3(0f, 1.48f * sc, fz), new Vector3(0.74f * sc, 0.14f * sc, 0.09f), woodDark, null, yaw);
        for (int i = 0; i < 2; i++)
        {
            float sx = (i == 0 ? -1f : 1f) * 0.37f * sc;
            kit.Box(p + r * new Vector3(sx, 0.72f * sc, fz), new Vector3(0.12f * sc, 1.40f * sc, 0.09f), woodDark, null, yaw);
        }
        // 窗：窗框 + 窗棂 + 窗纸
        float wx = -0.85f * sc, wy = 1.12f * sc;
        kit.Box(p + r * new Vector3(wx, wy, d * 0.5f + 0.04f * sc), new Vector3(0.60f * sc, 0.56f * sc, 0.05f), new Color(0.86f, 0.84f, 0.74f), null, yaw);
        kit.Box(p + r * new Vector3(wx, wy, d * 0.5f + 0.07f * sc), new Vector3(0.50f * sc, 0.46f * sc, 0.04f), new Color(0.93f, 0.91f, 0.82f), null, yaw);
        for (int i = 0; i < 3; i++)
        {
            float ox = (i - 1) * 0.17f * sc;
            kit.Box(p + r * new Vector3(wx + ox, wy, d * 0.5f + 0.09f * sc), new Vector3(0.035f * sc, 0.46f * sc, 0.03f), woodDark, null, yaw);
        }
        kit.Box(p + r * new Vector3(wx, wy, d * 0.5f + 0.09f * sc), new Vector3(0.50f * sc, 0.035f * sc, 0.03f), woodDark, null, yaw);
        Obstacles.Add(new Obstacle(p.x, p.z, 1.7f * sc));
        BuildShadow(p, 2.3f * sc, 0.30f);
    }

    void Pavilion(Vector3 p)
    {
        const float w = 3.4f, h = 2.7f;
        Vector3[] c =
        {
            new Vector3(-1.2f, 0f, -1.2f), new Vector3(1.2f, 0f, -1.2f),
            new Vector3(1.2f, 0f, 1.2f), new Vector3(-1.2f, 0f, 1.2f),
        };
        kit.Box(p + Vector3.up * 0.08f, new Vector3(w, 0.16f, w), Theme.Wood);
        for (int i = 0; i < 4; i++)
            kit.Cylinder(p + c[i], 0.11f, 0.09f, h, 6, Theme.Wood, Theme.Wood);
        kit.Pyramid(p + Vector3.up * h, w * 1.35f, 2.1f, Theme.RoofA, 0.38f);
        Obstacles.Add(new Obstacle(p.x, p.z, 1.5f));
        BuildShadow(p, 2.3f, 0.28f);
    }

    void Well(Vector3 p)
    {
        kit.Cylinder(p, 0.55f, 0.55f, 0.45f, 8, Theme.Stone, Theme.Path);
        kit.Cylinder(p + Vector3.up * 0.45f, 0.58f, 0.5f, 0.12f, 8, Theme.Stone, Theme.Stone);
        kit.Box(p + Vector3.up * 1.15f, new Vector3(1.3f, 0.08f, 0.1f), Theme.Wood);
        kit.Cylinder(p + new Vector3(-0.55f, 0.45f, 0f), 0.05f, 0.05f, 0.75f, 5, Theme.Wood, Theme.Wood);
        kit.Cylinder(p + new Vector3(0.55f, 0.45f, 0f), 0.05f, 0.05f, 0.75f, 5, Theme.Wood, Theme.Wood);
        Obstacles.Add(new Obstacle(p.x, p.z, 0.7f));
        BuildShadow(p, 1.0f, 0.25f);
    }

    void Lantern(Vector3 p)
    {
        kit.Cylinder(p, 0.12f, 0.12f, 1.15f, 6, Theme.Wood, Theme.Wood);
        kit.Box(p + Vector3.up * 1.35f, new Vector3(0.42f, 0.5f, 0.42f), new Color(0.95f, 0.78f, 0.42f));
        kit.Pyramid(p + Vector3.up * 1.62f, 0.55f, 0.28f, Theme.RoofB, 0.08f, 4, 45f);
    }

    void Bridge()
    {
        for (int i = 0; i < 7; i++)
        {
            float x = -1.8f + i * 0.6f;
            float z = -1.8f + Mathf.Sin(x * 0.16f) * 2.4f;
            kit.Box(new Vector3(x, 0.18f, z), new Vector3(0.58f, 0.1f, 2.4f), Theme.Wood);
        }
        kit.Beam(new Vector3(-1.8f, 0.7f, -3.1f), new Vector3(1.8f, 0.7f, -0.6f), 0.08f, Theme.Wood);
        kit.Beam(new Vector3(-1.8f, 0.7f, -0.5f), new Vector3(1.8f, 0.7f, 2.0f), 0.08f, Theme.Wood);
    }

    void Gatehouse(Vector3 p)
    {
        kit.Box(p + new Vector3(-3.2f, 2.1f, 0f), new Vector3(2.4f, 4.2f, 2.6f), Theme.Stone, Theme.Peak, 0f);
        kit.Box(p + new Vector3(3.2f, 2.1f, 0f), new Vector3(2.4f, 4.2f, 2.6f), Theme.Stone, Theme.Peak, 0f);
        kit.Box(p + new Vector3(0f, 3.6f, 0f), new Vector3(4.2f, 1.4f, 2.2f), Theme.Stone, Theme.Peak, 0f);
        kit.Roof(p + new Vector3(-3.2f, 4.3f, 0f), 2.8f, 3.0f, 1.1f, Theme.RoofA, Theme.Lacquer, 0f, 0.2f);
        kit.Roof(p + new Vector3(3.2f, 4.3f, 0f), 2.8f, 3.0f, 1.1f, Theme.RoofA, Theme.Lacquer, 0f, 0.2f);
        Obstacles.Add(new Obstacle(p.x - 3.2f, p.z, 1.3f));
        Obstacles.Add(new Obstacle(p.x + 3.2f, p.z, 1.3f));
        BuildShadow(p + new Vector3(-3.2f, 0f, 0f), 2.1f, 0.30f);
        BuildShadow(p + new Vector3(3.2f, 0f, 0f), 2.1f, 0.30f);
    }

    void Watch(Vector3 p)
    {
        kit.Cylinder(p, 1.1f, 0.95f, 4.2f, 8, Theme.Stone, Theme.Peak, 0.05f, seed);
        kit.Pyramid(p + Vector3.up * 4.2f, 2.4f, 1.3f, Theme.RoofA, 0.2f);
        Obstacles.Add(new Obstacle(p.x, p.z, 1.2f));
        BuildShadow(p, 1.7f, 0.28f);
    }

    void RuinWall(Vector3 p, float len, float yaw)
    {
        kit.Box(p + Vector3.up * 0.9f, new Vector3(len, 1.8f, 0.55f), Theme.Stone, Theme.Rock, yaw);
        kit.Box(p + Quaternion.Euler(0f, yaw, 0f) * new Vector3(len * 0.3f, 0.45f, 0.8f),
            new Vector3(1.2f, 0.5f, 0.7f), Theme.Rock, null, yaw + 25f);
        Obstacles.Add(new Obstacle(p.x, p.z, len * 0.28f));
        BuildShadow(p, len * 0.42f, 0.22f);
    }

    void Pillar(Vector3 p, float h, bool broken)
    {
        kit.Cylinder(p, 0.28f, 0.24f, broken ? h * 0.55f : h, 7, Theme.Stone, Theme.Stone, 0.08f, seed);
        if (broken)
            kit.Box(p + new Vector3(0.6f, 0.22f, 0.2f), new Vector3(0.9f, 0.35f, 0.4f), Theme.Stone, null, 40f);
        Obstacles.Add(new Obstacle(p.x, p.z, 0.45f));
    }

    void Fortress()
    {
        // 三面围墙，南面开口
        WallSeg(new Vector3(-16f, 0f, 0f), 0.7f, 28f, 90f);
        WallSeg(new Vector3(16f, 0f, 0f), 0.7f, 28f, 90f);
        WallSeg(new Vector3(0f, 0f, -16f), 0.7f, 32f, 0f);
        Watch(new Vector3(-16f, 0f, -16f));
        Watch(new Vector3(16f, 0f, -16f));
        Watch(new Vector3(-16f, 0f, 14f));
        Watch(new Vector3(16f, 0f, 14f));
        // 旗杆
        kit.Cylinder(new Vector3(0f, 0f, -8f), 0.08f, 0.06f, 5.2f, 5, Theme.Wood, Theme.Wood);
        kit.Box(new Vector3(0.55f, 4.6f, -8f), new Vector3(1.1f, 0.7f, 0.08f), Theme.Lacquer);
    }

    void WallSeg(Vector3 p, float thick, float len, float yaw)
    {
        kit.Box(p + Vector3.up * 1.6f, new Vector3(len, 3.2f, thick * 2f), Theme.Stone, Theme.Rock, yaw);
        if (Mathf.Abs(yaw) < 1f) { Obstacles.Add(new Obstacle(p.x, p.z, 2.2f)); }
        else
        {
            for (float z = -12f; z <= 12f; z += 4f)
                Obstacles.Add(new Obstacle(p.x, p.z + z, 1.1f));
        }
    }

    void Scenes()
    {
        kit.Clear();
        switch (Theme.Id)
        {
            case "peachgrove": PeachScene(); break;
            case "village": VillageScene(); break;
            case "snow": PassScene(); break;
            case "golden": RuinScene(); break;
            case "dusk": FortScene(); break;
            default: WildScene(); break;
        }
        if (kit.Count > 0) Emit("scene", kit.Build("scene", true), toonOut, true);
    }

    bool Open(float x, float z, float r = 1.3f)
    {
        if (Mathf.Abs(x) < 2.5f) return false;
        if (OnPath(x, z) || OnRiver(x, z)) return false;
        if (KeepClear(x, z, r) || NearPack(x, z, r)) return false;
        return true;
    }

    void TryTreeAt(string kind, float x, float z, float h)
    {
        if (!Open(x, z, 1.8f)) return;
        // 按地形高度落位，和 Trees() 里的做法一致
        if (PlaceTree(kind, new Vector3(x, GroundY(x, z, 0.05f), z), h))
            Obstacles.Add(new Obstacle(x, z, 0.5f));
    }

    void PeachScene()
    {
        float row = wide ? 16f : 12f;
        int n = wide ? 5 : 3;
        for (int i = 0; i < n; i++)
        {
            float z = -4f + i * 4.4f;
            TryTreeAt("peach", -row, z, 5.3f);
            TryTreeAt("peach", row, z, 5.6f);
        }
        if (Open(row + 1f, -2f, 1.6f)) Shrine(new Vector3(row + 1.2f, 0f, -2f));
        if (!battlefield && Open(-7.4f, 10.5f, 1f)) Bench(new Vector3(-7.4f, 0f, 10.5f), 80f);
        if (!battlefield && Open(7.4f, 10.5f, 1f)) Bench(new Vector3(7.4f, 0f, 10.5f), -80f);
        if (HasWater()) ReedLine();
    }

    void VillageScene()
    {
        ReedLine();
        Boat(new Vector3(8.2f, 0f, -2.4f), 16f);
        Boat(new Vector3(-10f, 0f, -0.6f), -8f);
        if (Open(5.5f, 1.2f, 1f)) Pier(new Vector3(5.5f, 0f, -1.5f));
        if (Open(-13f, 9f, 1.2f)) Rack(new Vector3(-13f, 0f, 9f), 15f);
        if (Open(12f, 8f, 1.4f)) Fire(new Vector3(12f, 0f, 8f));
        if (Open(-8f, -8f, 1f)) Crate(new Vector3(-8f, 0f, -8f));
    }

    void PassScene()
    {
        if (Open(-12f, 8f, 1.6f)) Tent(new Vector3(-12f, 0f, 8f), 20f, new Color(0.72f, 0.74f, 0.78f));
        if (Open(11.5f, 6f, 1.6f)) Tent(new Vector3(11.5f, 0f, 6f), -10f, new Color(0.62f, 0.66f, 0.72f));
        if (Open(7.5f, 9f, 1.3f)) Fire(new Vector3(7.5f, 0f, 9f));
        if (Open(-5.4f, -7f, 0.8f)) Banner(new Vector3(-5.4f, 0f, -7f), new Color(0.75f, 0.18f, 0.16f));
        if (Open(5.4f, -7f, 0.8f)) Banner(new Vector3(5.4f, 0f, -7f), new Color(0.75f, 0.18f, 0.16f));
        if (Open(-9f, 4.5f, 0.8f)) Crate(new Vector3(-9f, 0f, 4.5f));
        if (Open(13f, 3.5f, 0.8f)) Crate(new Vector3(13f, 0f, 3.5f));
        if (Open(-8.5f, 1.2f, 1f)) Barricade(new Vector3(-8.5f, 0f, 1.2f), 18f);
        if (Open(8.2f, -2.2f, 1f)) Barricade(new Vector3(8.2f, 0f, -2.2f), -12f);
        TryTreeAt("pine", -15f, -4f, 7.2f);
        TryTreeAt("pine", 15f, 2f, 6.8f);
        TryTreeAt("dead", -14f, 12f, 5.2f);
    }

    void RuinScene()
    {
        if (Open(11f, -5f, 1.5f)) Cart(new Vector3(11f, 0f, -5f), 28f, true);
        if (Open(-9f, 9f, 0.8f)) Stele(new Vector3(-9f, 0f, 9f));
        if (Open(6f, 7f, 1.2f)) Cart(new Vector3(6f, 0f, 7f), -40f, true);
        Scorch(new Vector3(4.5f, 0f, -7f), 1.6f, 1.1f);
        Scorch(new Vector3(-6f, 0f, 2f), 1.3f, 0.9f);
        Scorch(new Vector3(2f, 0f, 5f), 1.1f, 0.8f);
        if (Open(-12f, -4f, 0.7f)) Banner(new Vector3(-12f, 0f, -4f), new Color(0.55f, 0.28f, 0.16f));
        if (Open(13f, 8f, 0.8f)) Crate(new Vector3(13f, 0f, 8f));
        TryTreeAt("autumn", -14f, 4f, 5.6f);
        TryTreeAt("autumn", 15f, -8f, 5.2f);
        TryTreeAt("dead", 14f, 11f, 4.8f);
    }

    void FortScene()
    {
        if (Open(-6f, 6f, 1.5f)) Tent(new Vector3(-6f, 0f, 6f), 8f, new Color(0.42f, 0.28f, 0.24f));
        if (Open(8f, 7f, 1.5f)) Tent(new Vector3(8f, 0f, 7f), -16f, new Color(0.38f, 0.24f, 0.22f));
        if (Open(4.2f, 8.5f, 1.1f)) Fire(new Vector3(4.2f, 0f, 8.5f));
        float[] zs = { 6f, 1f, -5f };
        for (int i = 0; i < zs.Length; i++)
        {
            if (Open(-4.6f, zs[i], 0.6f)) Torch(new Vector3(-4.6f, 0f, zs[i]));
            if (Open(4.6f, zs[i], 0.6f)) Torch(new Vector3(4.6f, 0f, zs[i]));
        }
        if (Open(-10f, -6f, 0.8f)) Banner(new Vector3(-10f, 0f, -6f), Theme.Lacquer);
        if (Open(10f, -6f, 0.8f)) Banner(new Vector3(10f, 0f, -6f), Theme.Lacquer);
        if (Open(9f, -2f, 0.9f)) Rack(new Vector3(9f, 0f, -2f), 90f);
        if (Open(-11f, 2f, 0.7f)) Crate(new Vector3(-11f, 0f, 2f));
        TryTreeAt("dead", -14f, 8f, 5f);
        TryTreeAt("dead", 14f, -8f, 4.6f);
    }

    void WildScene()
    {
        if (HasWater()) ReedLine();
        if (Open(12f, 8f, 1f)) Hay(new Vector3(12f, 0f, 8f), 1f);
        if (Open(14.5f, 5.5f, 1f)) Hay(new Vector3(14.5f, 0f, 5.5f), 0.8f);
        if (Open(-13f, 9f, 1f)) Hay(new Vector3(-13f, 0f, 9f), 1.1f);
        if (Open(12f, -5f, 0.8f)) Scarecrow(new Vector3(12f, 0f, -5f));
        if (Open(-12f, -7f, 1f)) Woodpile(new Vector3(-12f, 0f, -7f));
        Millet(new Vector3(wide ? 18f : 13f, 0f, -2f));
        TryTreeAt("round", -15f, -6f, 5.4f);
        TryTreeAt("pine", 16f, 11f, 6.4f);
    }

    void ReedLine()
    {
        var rng = new System.Random(seed ^ 77);
        float span = wide ? 26f : 15f;
        for (float x = -span; x <= span; x += 2.6f)
        {
            float rz = -1.8f + Mathf.Sin(x * 0.16f) * 2.4f;
            for (int side = -1; side <= 1; side += 2)
            {
                if (rng.NextDouble() < 0.3) continue;
                float z = rz + side * (2.25f + (float)rng.NextDouble() * 0.35f);
                if (OnPath(x, z) || KeepClear(x, z, 1.3f) || NearPack(x, z, 1.2f)) continue;
                float h = 1.35f + (float)rng.NextDouble() * 0.45f;
                // 芦苇贴着河岸的地面长（河岸高度随地形变化）
                var rp = new Vector3(x, GroundY(x, z, 0.01f), z);
                if (!PlaceCard("tex/foliage/reed.jpg", rp, h, true, -6, 0.35f))
                    ReedMesh(rp, rng.Next());
            }
        }
    }

    void ReedMesh(Vector3 p, int s)
    {
        var rng = new System.Random(s);
        float saved = kit.Sway;
        kit.Sway = 0.45f;
        int n = 4 + rng.Next(3);
        for (int i = 0; i < n; i++)
        {
            Vector3 o = p + new Vector3((float)(rng.NextDouble() - 0.5f) * 0.45f, 0f, (float)(rng.NextDouble() - 0.5f) * 0.3f);
            float h = 1.1f + (float)rng.NextDouble() * 0.55f;
            Color c = Color.Lerp(Theme.GrassDry, Theme.Leaf2, (float)rng.NextDouble());
            kit.Cylinder(o, 0.02f, 0.012f, h, 4, c, c, 0f, s + i);
        }
        kit.Sway = saved;
    }

    void Shrine(Vector3 p)
    {
        kit.Box(p + Vector3.up * 0.18f, new Vector3(2.2f, 0.36f, 1.5f), Theme.Stone, Theme.Path);
        kit.Box(p + Vector3.up * 0.7f, new Vector3(1.5f, 0.7f, 0.7f), Theme.Wood, Theme.Wall);
        kit.Roof(p + Vector3.up * 1.05f, 2.3f, 1.7f, 0.7f, Theme.RoofB, Theme.Lacquer, 0f, 0.16f);
        kit.Cylinder(p + new Vector3(0f, 0.36f, 0.85f), 0.08f, 0.05f, 0.35f, 5, Theme.Lacquer, Theme.Lacquer);
        Obstacles.Add(new Obstacle(p.x, p.z, 1.1f));
    }

    void Bench(Vector3 p, float yaw)
    {
        Quaternion r = Quaternion.Euler(0f, yaw, 0f);
        kit.Box(p + Vector3.up * 0.42f, new Vector3(1.5f, 0.1f, 0.42f), Theme.Wood, null, yaw);
        kit.Box(p + r * new Vector3(-0.6f, 0.2f, 0f), new Vector3(0.1f, 0.4f, 0.4f), Theme.Wood, null, yaw);
        kit.Box(p + r * new Vector3(0.6f, 0.2f, 0f), new Vector3(0.1f, 0.4f, 0.4f), Theme.Wood, null, yaw);
    }

    void Boat(Vector3 p, float yaw)
    {
        kit.Box(p + Vector3.up * 0.16f, new Vector3(2.3f, 0.22f, 0.72f), Theme.Wood, Theme.Path, yaw);
        Quaternion r = Quaternion.Euler(0f, yaw, 0f);
        kit.Box(p + r * new Vector3(1.15f, 0.2f, 0f), new Vector3(0.45f, 0.16f, 0.42f), Theme.Wood, null, yaw);
        kit.Box(p + r * new Vector3(-1.15f, 0.2f, 0f), new Vector3(0.4f, 0.16f, 0.4f), Theme.Wood, null, yaw);
        kit.Cylinder(p + r * new Vector3(0.2f, 0.2f, 0f), 0.04f, 0.03f, 1.3f, 4, Theme.Wood, Theme.Wood);
    }

    void Pier(Vector3 p)
    {
        for (int i = 0; i < 4; i++)
            kit.Box(p + new Vector3(0f, 0.12f, -i * 0.55f), new Vector3(1.3f, 0.1f, 0.48f), Theme.Wood);
        kit.Cylinder(p + new Vector3(-0.55f, 0f, -0.2f), 0.06f, 0.05f, 0.7f, 4, Theme.Wood, Theme.Wood);
        kit.Cylinder(p + new Vector3(0.55f, 0f, -1.4f), 0.06f, 0.05f, 0.7f, 4, Theme.Wood, Theme.Wood);
    }

    void Rack(Vector3 p, float yaw)
    {
        Quaternion r = Quaternion.Euler(0f, yaw, 0f);
        kit.Cylinder(p + r * new Vector3(-0.8f, 0f, 0f), 0.05f, 0.04f, 1.5f, 4, Theme.Wood, Theme.Wood);
        kit.Cylinder(p + r * new Vector3(0.8f, 0f, 0f), 0.05f, 0.04f, 1.5f, 4, Theme.Wood, Theme.Wood);
        kit.Beam(p + r * new Vector3(-0.8f, 1.35f, 0f), p + r * new Vector3(0.8f, 1.35f, 0f), 0.06f, Theme.Wood);
        kit.Box(p + r * new Vector3(0f, 1.05f, 0f), new Vector3(0.7f, 0.45f, 0.08f), new Color(0.86f, 0.82f, 0.72f), null, yaw);
    }

    void Tent(Vector3 p, float yaw, Color cloth)
    {
        Quaternion r = Quaternion.Euler(0f, yaw, 0f);
        Vector3 P(float x, float y, float z) => p + r * new Vector3(x, y, z);
        Vector3 a = P(-1.35f, 0.05f, -1.05f), b = P(1.35f, 0.05f, -1.05f);
        Vector3 c = P(1.35f, 0.05f, 1.05f), d = P(-1.35f, 0.05f, 1.05f);
        Vector3 rl = P(-1.25f, 1.25f, 0f), rr = P(1.25f, 1.25f, 0f);
        kit.Quad(a, b, rr, rl, cloth, p + Vector3.up);
        kit.Quad(d, c, rr, rl, cloth * 0.9f, p + Vector3.up);
        kit.Tri(a, rl, d, cloth * 0.82f, p + Vector3.up);
        kit.Cylinder(P(-1.15f, 0f, -0.85f), 0.04f, 0.03f, 1.15f, 4, Theme.Wood, Theme.Wood);
        kit.Cylinder(P(1.15f, 0f, 0.85f), 0.04f, 0.03f, 1.15f, 4, Theme.Wood, Theme.Wood);
        Obstacles.Add(new Obstacle(p.x, p.z, 1.15f));
    }

    void Banner(Vector3 p, Color cloth)
    {
        kit.Cylinder(p, 0.05f, 0.04f, 3.1f, 5, Theme.Wood, Theme.Wood);
        kit.Box(p + new Vector3(0.55f, 2.45f, 0f), new Vector3(1.05f, 1.15f, 0.06f), cloth);
        kit.Box(p + Vector3.up * 3.15f, new Vector3(0.16f, 0.16f, 0.16f), Theme.Lacquer);
        Obstacles.Add(new Obstacle(p.x, p.z, 0.25f));
    }

    void Crate(Vector3 p)
    {
        kit.Box(p + Vector3.up * 0.32f, new Vector3(0.7f, 0.64f, 0.55f), Theme.Wood, Theme.Path);
        kit.Box(p + new Vector3(0.45f, 0.22f, 0.4f), new Vector3(0.5f, 0.4f, 0.4f), Theme.Wood);
        Obstacles.Add(new Obstacle(p.x, p.z, 0.45f));
    }

    void Cart(Vector3 p, float yaw, bool broken)
    {
        float tilt = broken ? 18f : 0f;
        kit.Box(p + Vector3.up * 0.55f, new Vector3(1.8f, 0.35f, 1.05f), Theme.Wood, Theme.Path, yaw + tilt);
        Quaternion r = Quaternion.Euler(0f, yaw, 0f);
        kit.Cylinder(p + r * new Vector3(-0.55f, 0.28f, 0.62f), 0.28f, 0.28f, 0.1f, 8, Theme.Trunk, Theme.Trunk, 0f, 0, 90f);
        kit.Cylinder(p + r * new Vector3(0.6f, 0.28f, -0.62f), 0.28f, 0.28f, 0.1f, 8, Theme.Trunk, Theme.Trunk, 0f, 1, 90f);
        if (broken)
            kit.Box(p + r * new Vector3(1.3f, 0.2f, 0.4f), new Vector3(0.7f, 0.18f, 0.45f), Theme.Wood, null, yaw + 50f);
        Obstacles.Add(new Obstacle(p.x, p.z, 1.05f));
    }

    void Stele(Vector3 p)
    {
        kit.Box(p + Vector3.up * 0.15f, new Vector3(1.1f, 0.3f, 0.7f), Theme.Stone);
        kit.Box(p + Vector3.up * 1.15f, new Vector3(0.7f, 1.7f, 0.28f), Theme.Stone, Theme.Peak);
        Obstacles.Add(new Obstacle(p.x, p.z, 0.55f));
    }

    void Scorch(Vector3 p, float rx, float rz)
    {
        var c = Color.Lerp(Theme.Cliff, new Color(0.18f, 0.12f, 0.1f), 0.55f);
        kit.Quad(
            p + new Vector3(-rx, 0.07f, -rz),
            p + new Vector3(rx, 0.07f, -rz),
            p + new Vector3(rx, 0.07f, rz),
            p + new Vector3(-rx, 0.07f, rz),
            c, new Vector3(p.x, -3f, p.z));
    }

    void Barricade(Vector3 p, float yaw)
    {
        Quaternion r = Quaternion.Euler(0f, yaw, 0f);
        kit.Beam(p + r * new Vector3(-0.7f, 0.15f, 0f), p + r * new Vector3(0.7f, 0.85f, 0f), 0.1f, Theme.Trunk);
        kit.Beam(p + r * new Vector3(-0.7f, 0.85f, 0.15f), p + r * new Vector3(0.7f, 0.15f, 0.15f), 0.1f, Theme.Trunk);
        Obstacles.Add(new Obstacle(p.x, p.z, 0.7f));
    }

    void Torch(Vector3 p)
    {
        kit.Cylinder(p, 0.06f, 0.05f, 1.35f, 5, Theme.Wood, Theme.Wood);
        kit.Box(p + Vector3.up * 1.45f, new Vector3(0.22f, 0.28f, 0.22f), new Color(1f, 0.62f, 0.28f));
        if (fires < 2) FireLight(p + Vector3.up * 1.5f);
    }

    void Fire(Vector3 p)
    {
        kit.Cylinder(p, 0.55f, 0.48f, 0.12f, 8, Theme.Stone, Theme.Stone);
        kit.Beam(p + new Vector3(-0.28f, 0.12f, 0f), p + new Vector3(0.22f, 0.38f, 0.08f), 0.07f, Theme.Trunk);
        kit.Beam(p + new Vector3(0.2f, 0.1f, -0.1f), p + new Vector3(-0.1f, 0.36f, 0.12f), 0.07f, Theme.Trunk);
        kit.Blob(p + Vector3.up * 0.48f, new Vector3(0.26f, 0.34f, 0.26f), new Color(1f, 0.46f, 0.16f), 2, 5, 0.08f, seed);
        Obstacles.Add(new Obstacle(p.x, p.z, 0.5f));
        if (fires < 2) FireLight(p + Vector3.up * 0.6f);
    }

    void FireLight(Vector3 p)
    {
        fires++;
        var go = new GameObject("firelight");
        go.transform.SetParent(root, false);
        go.transform.position = p;
        var l = go.AddComponent<Light>();
        l.type = LightType.Point;
        l.color = new Color(1f, 0.58f, 0.28f);
        l.range = 6.5f;
        l.intensity = 1.25f;
        l.shadows = LightShadows.None;
    }

    void Hay(Vector3 p, float sc)
    {
        kit.Blob(p + Vector3.up * (0.45f * sc), new Vector3(0.7f * sc, 0.45f * sc, 0.55f * sc), Theme.GrassDry, 3, 6, 0.08f, seed);
        kit.Blob(p + Vector3.up * (0.85f * sc), new Vector3(0.45f * sc, 0.32f * sc, 0.4f * sc), Theme.Leaf2, 2, 5, 0.06f, seed + 1);
        Obstacles.Add(new Obstacle(p.x, p.z, 0.45f * sc));
    }

    void Scarecrow(Vector3 p)
    {
        kit.Cylinder(p, 0.05f, 0.04f, 1.7f, 4, Theme.Wood, Theme.Wood);
        kit.Beam(p + new Vector3(-0.45f, 1.25f, 0f), p + new Vector3(0.45f, 1.25f, 0f), 0.06f, Theme.Wood);
        kit.Blob(p + Vector3.up * 1.75f, new Vector3(0.18f, 0.2f, 0.16f), Theme.Path, 2, 5, 0.05f, seed);
        kit.Box(p + Vector3.up * 1.2f, new Vector3(0.55f, 0.45f, 0.12f), Theme.RoofB);
    }

    void Woodpile(Vector3 p)
    {
        kit.Beam(p + new Vector3(-0.4f, 0.12f, 0f), p + new Vector3(0.5f, 0.2f, 0.1f), 0.12f, Theme.Trunk);
        kit.Beam(p + new Vector3(-0.3f, 0.28f, -0.1f), p + new Vector3(0.45f, 0.32f, 0.05f), 0.12f, Theme.Trunk);
        kit.Beam(p + new Vector3(-0.35f, 0.42f, 0.05f), p + new Vector3(0.3f, 0.48f, -0.05f), 0.11f, Theme.Wood);
        Obstacles.Add(new Obstacle(p.x, p.z, 0.5f));
    }

    void Millet(Vector3 p)
    {
        for (int i = 0; i < 4; i++)
        {
            float x = p.x + (i - 1.5f) * 0.7f;
            if (!Open(x, p.z, 0.5f)) continue;
            kit.Box(new Vector3(x, 0.45f, p.z), new Vector3(0.35f, 0.9f, 2.4f), Theme.GrassDry, Theme.Leaf2);
        }
    }

    void Landmarks()
    {
        // 额外小摆设：石灯、木桩、草垛，让路点可读
        kit.Clear();
        if (Theme.Id == "peachgrove" && !battlefield)
        {
            for (int i = 0; i < 4; i++)
            {
                float z = 4f - i * 4.5f;
                FencePost(new Vector3(-2.1f, 0f, z));
                FencePost(new Vector3(2.1f, 0f, z));
            }
        }
        if (Theme.Id == "golden")
        {
            kit.Blob(new Vector3(8f, 0.5f, -11f), new Vector3(1.1f, 0.7f, 0.8f), new Color(0.55f, 0.38f, 0.22f), 3, 6, 0.2f, 4);
        }
        if (kit.Count > 0) Emit("props", kit.Build("props", true), archMat, true);
    }

    void FencePost(Vector3 p)
    {
        kit.Cylinder(p, 0.07f, 0.06f, 0.85f, 5, Theme.Wood, Theme.Wood);
    }

    // ---------------- 氛围粒子 ----------------

    void Ambient()
    {
        for (int i = 0; i < PuffCap() / 2; i++) SpawnPuff(true);
    }

    int PuffCap()
    {
        int cap;
        switch (Theme.Particles)
        {
            case "petal": cap = 28; break;
            case "leaf": cap = 18; break;
            case "snow": cap = 36; break;
            case "ember": cap = 16; break;
            default: cap = 8; break;
        }
        // 特效密度设置：0 关 / 1 少 / 2 正常 / 3 多
        int density = Settings.Current != null ? Settings.Current.EffectDensity : 2;
        switch (density)
        {
            case 0: return 0;
            case 1: return Mathf.Max(1, cap / 3);
            case 3: return cap * 2;
            default: return cap;
        }
    }

    void SpawnPuff(bool anywhere = false)
    {
        if (string.IsNullOrEmpty(Theme.Particles)) return;
        var go = new GameObject("puff");
        go.transform.SetParent(root, false);
        var sr = go.AddComponent<SpriteRenderer>();
        bool star = Theme.Particles == "ember";
        sr.sprite = Tex.CenterSprite(star ? Tex.Star : Tex.Glow, 1f);
        sr.material = Look.Additive;
        Color col;
        Vector3 pos, vel;
        float size;
        switch (Theme.Particles)
        {
            case "petal":
                col = Color.Lerp(Theme.Blossom1, Theme.Blossom2, Util.Rand(0f, 1f));
                col.a = 0.85f;
                size = Util.Rand(0.22f, 0.4f);
                pos = new Vector3(Util.Rand(-18f, 18f), Util.Rand(2.5f, 7f), Util.Rand(-16f, 16f));
                vel = new Vector3(Util.Rand(-0.4f, 0.2f), Util.Rand(-0.55f, -0.2f), Util.Rand(-0.2f, 0.2f));
                break;
            case "leaf":
                col = Color.Lerp(Theme.Leaf1, Theme.Leaf2, Util.Rand(0f, 1f));
                col.a = 0.8f;
                size = Util.Rand(0.25f, 0.45f);
                pos = new Vector3(Util.Rand(-18f, 18f), Util.Rand(3f, 8f), Util.Rand(-16f, 16f));
                vel = new Vector3(Util.Rand(-0.6f, -0.1f), Util.Rand(-0.4f, -0.15f), Util.Rand(-0.2f, 0.2f));
                break;
            case "snow":
                col = new Color(0.95f, 0.97f, 1f, 0.9f);
                size = Util.Rand(0.12f, 0.22f);
                pos = new Vector3(Util.Rand(-20f, 20f), Util.Rand(6f, 12f), Util.Rand(-18f, 18f));
                vel = new Vector3(Util.Rand(-0.3f, 0.15f), Util.Rand(-1.1f, -0.5f), Util.Rand(-0.15f, 0.15f));
                break;
            default:
                col = new Color(1f, 0.55f, 0.28f, 0.8f);
                size = Util.Rand(0.12f, 0.22f);
                pos = new Vector3(Util.Rand(-16f, 16f), Util.Rand(0.4f, 1.2f), Util.Rand(-16f, 16f));
                vel = new Vector3(Util.Rand(-0.15f, 0.15f), Util.Rand(0.6f, 1.4f), Util.Rand(-0.15f, 0.15f));
                break;
        }
        if (anywhere) pos.y = Util.Rand(1f, 6f);
        go.transform.position = pos;
        go.transform.localScale = Vector3.one * size;
        sr.color = col;
        sr.sortingOrder = 50;
        puffs.Add(new Puff
        {
            Tr = go.transform, Sr = sr, Pos = pos, Vel = vel, Col = col, Size = size,
            Max = Util.Rand(3.5f, 7f), Life = anywhere ? Util.Rand(1f, 5f) : Util.Rand(3.5f, 7f),
            Spin = Util.Rand(-80f, 80f),
        });
    }

    // ---------------- 工具 ----------------

    bool KeepClear(float x, float z, float r)
    {
        if (Mathf.Abs(x) < 3.6f && z > 6f) return true;
        if (Util.Dist2(x, z, 0f, 12f) < r + 1.5f) return true;
        if (Util.Dist2(x, z, 0f, -13f) < r + 1.4f) return true;
        if (Util.Dist2(x, z, 18f, 2f) < r + 1.6f) return true;
        if (Util.Dist2(x, z, -18f, 2f) < r + 1.6f) return true;
        if (Util.Dist2(x, z, -6f, 6f) < r + 1.3f) return true;
        if (Util.Dist2(x, z, 9.5f, 8.2f) < r + 1.8f) return true;
        return false;
    }

    bool NearPack(float x, float z, float extra)
    {
        if (map.Packs == null) return false;
        for (int i = 0; i < map.Packs.Length; i++)
        {
            var p = map.Packs[i];
            if (Util.Dist2(x, z, p.X, p.Z) < p.R + extra) return true;
        }
        return false;
    }

    GameObject Emit(string name, Mesh mesh, Material mat, bool shadows)
    {
        meshes.Add(mesh);
        var go = new GameObject(name);
        go.transform.SetParent(root, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = mat;
        mr.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
        mr.receiveShadows = shadows;
        return go;
    }

    static float Fbm(float x, float z)
    {
        float n = Mathf.Sin(x * 1.7f + z * 0.9f) * 0.5f + Mathf.Sin(x * 0.6f - z * 1.3f) * 0.35f;
        n += Mathf.Sin(x * 3.1f + z * 2.4f) * 0.15f;
        return n * 0.5f + 0.5f;
    }

    static int Hash(string s)
    {
        int h = 17;
        if (s == null) return h;
        for (int i = 0; i < s.Length; i++) h = h * 31 + s[i];
        return h;
    }
}
