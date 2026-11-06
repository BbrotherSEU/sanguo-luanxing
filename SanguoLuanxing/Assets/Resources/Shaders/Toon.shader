// 卡通色阶光照：顶点色上色，两段明暗 + 高光带 + 边缘光，接收实时阴影与雾。
// 顶点色 alpha < 1 的顶点会随风摆动（1 - a 为摆幅权重），用于树冠与草丛。
Shader "Sanguo/Toon"
{
    Properties
    {
        _Color ("Tint", Color) = (1,1,1,1)
        _ShadowTint ("Shadow Tint", Color) = (0.62,0.58,0.80,1)
        _RimColor ("Rim (a = strength)", Color) = (1,0.96,0.85,0.28)
        _Soft ("Band Softness", Range(0.001,0.3)) = 0.05
        _Wind ("Wind", Float) = 1
        _MainTex ("Detail", 2D) = "white" {}
        _Path ("Path", 2D) = "white" {}
        _Wood ("Wood", 2D) = "white" {}
        _Roof ("Roof", 2D) = "white" {}
        _Stone ("Stone", 2D) = "white" {}
        _TexMix ("Texture Mix", Range(0,1)) = 0
        _TexTile ("Tile", Float) = 0.45
        _PaintMode ("Paint Mode", Float) = 0
        _DetailTex ("Detail Overlay", 2D) = "white" {}
        _DetailTile ("Detail Tile", Float) = 1
        _DetailStrength ("Detail Strength", Range(0,1)) = 0
        _BumpStrength ("Bump Strength", Range(0,2)) = 0
        _RoofTileMul ("Roof Tile Mul", Float) = 2.2
        _WoodTileMul ("Wood Tile Mul", Float) = 1
        _StoneTileMul ("Stone Tile Mul", Float) = 1.3
        _WallTileMul ("Wall Tile Mul", Float) = 1
        _LumRoof ("Roof Mean Lum", Float) = 0.5
        _LumWood ("Wood Mean Lum", Float) = 0.5
        _LumStone ("Stone Mean Lum", Float) = 0.5
        _LumWall ("Wall Mean Lum", Float) = 0.5
        _LumMain ("Main Mean Lum", Float) = 0.5
        _MeanMain ("Main Mean RGB", Color) = (0.5,0.5,0.5,1)
        _MeanDetail ("Detail Mean RGB", Color) = (0.5,0.5,0.5,1)
        _DetailContrast ("Detail Contrast", Float) = 1
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }

        Pass
        {
            Name "FORWARD"
            Tags { "LightMode"="ForwardBase" }

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #pragma multi_compile_fwdbase
            #pragma multi_compile_fog
            #include "UnityCG.cginc"
            #include "Lighting.cginc"
            #include "AutoLight.cginc"

            fixed4 _Color, _ShadowTint, _RimColor;
            float _Soft, _Wind, _TexMix, _TexTile, _PaintMode;
            float _DetailTile, _DetailStrength, _BumpStrength;
            float _RoofTileMul, _WoodTileMul, _StoneTileMul, _WallTileMul;
            float _LumRoof, _LumWood, _LumStone, _LumWall, _LumMain;
            float4 _MeanMain, _MeanDetail;
            float _DetailContrast;
            sampler2D _MainTex, _Path, _Wood, _Roof, _Stone, _DetailTex;

            float3 SampleTri(sampler2D tex, float3 wp, float3 nrm, float s)
            {
                float3 an = abs(nrm);
                // 地面几乎是平的。斜一点就混进侧面投影，草皮会被拉成一条条。
                if (an.y > 0.72)
                {
                    // 地面：斜向旋转采样。正交网格铺在地上，远看是一张方格纸，
                    // 转个角度再叠一点低频扰动，重复感就散开了。
                    float a = 0.62;                       // ≈35°
                    float c = cos(a), si = sin(a);
                    float2 p = float2(wp.x * c - wp.z * si, wp.x * si + wp.z * c);
                    // 加一点低频形变，让"直线网格"变成微微起伏的曲面
                    p += float2(sin(wp.z * 0.11), sin(wp.x * 0.09)) * 0.35f;
                    return tex2D(tex, p * s).rgb;
                }
                float3 w = pow(max(an, 1e-4), 8);
                w /= (w.x + w.y + w.z);
                return tex2D(tex, wp.zy * s).rgb * w.x
                     + tex2D(tex, wp.xz * s).rgb * w.y
                     + tex2D(tex, wp.xy * s).rgb * w.z;
            }

            // 屋顶专用：瓦垄要顺着坡往下走。
            // 三平面投影会把斜屋面拆成两个方向的投影，瓦垄被拉成十字。
            // 这里用法线在水平面上的投影求出坡向，垂直于坡向的就是屋脊方向，
            // 两个轴分别取样，v 按坡度还原真实斜长，瓦片不会被压扁。
            float3 SampleRoof(sampler2D tex, float3 wp, float3 nrm, float s)
            {
                if (abs(nrm.y) < 0.35) return SampleTri(tex, wp, nrm, s);  // 山墙等竖直面
                float2 d = nrm.xz;
                float l = length(d);
                if (l < 0.12) return tex2D(tex, wp.xz * s).rgb;             // 平顶
                float2 slope = d / l;                                       // 坡向（水平）
                float2 ridge = float2(-slope.y, slope.x);                   // 屋脊方向
                float u = dot(wp.xz, ridge) * s;
                float v = dot(wp.xz, slope) * s / max(abs(nrm.y), 0.25);
                return tex2D(tex, float2(u, v)).rgb;
            }

            // 低频细节层：超大尺度的柔和明暗，用来打散平铺的均匀感。
            // 地面用按通道归一化（带色），其余用亮度（只调明暗）。
            float3 ApplyDetail(float3 painted, float3 wp, float3 nrm)
            {
                if (_DetailStrength < 0.001) return painted;
                float3 det = SampleTri(_DetailTex, wp, nrm, _DetailTile);
                if (_PaintMode > 2.5)
                {
                    float3 md = max(_MeanDetail.rgb, float3(1e-3, 1e-3, 1e-3));
                    float3 dr = clamp(det / md, 0.55f, 1.8f);
                    return lerp(painted, painted * dr, _DetailStrength);
                }
                float dl = dot(det, float3(0.30, 0.59, 0.11));
                float v = smoothstep(0.22, 0.9, dl) - 0.5;
                return painted * (1.0 + v * _DetailStrength * 0.42);
            }

            float3 Paint(float3 baseCol, float3 wp, float3 nrm)
            {
                if (_TexMix < 0.01 && _DetailStrength < 0.01) return baseCol;
                float3 detail = SampleTri(_MainTex, wp, nrm, _TexTile);
                if (_PaintMode > 2.5)
                {
                    // 地面 / 岩石：贴图只提供明暗结构，色相完全由顶点色给。
                    // 用中心化而不是除法：除以均值会把对比度压掉（草叶只差几十灰阶，
                    // 除完就只剩 ±5%），中心化保留贴图本来的明暗幅度。
                    // 注意这里不能再走下面那两次 lerp —— detail 已经是 baseCol 乘过系数，
                    // 再 lerp 一次就把对比全衰减掉了。
                    float3 m = max(_MeanMain.rgb, float3(1e-3, 1e-3, 1e-3));
                    float3 dev = (detail - m) / m;                 // 相对均值偏离
                    detail = baseCol * clamp(1.0 + dev * _DetailContrast, 0.30f, 2.0f);
                    return ApplyDetail(detail, wp, nrm);
                }
                else if (_PaintMode > 1.5)
                {
                    // 建筑按顶点色分材质：饱和红/青 = 瓦，几乎无彩 = 石，亮而淡 = 墙，其余 = 木。
                    // 贴图只提供明暗（瓦垄、砖缝、木纹），色相始终由顶点色给，屋顶不会一律变成红瓦。
                    float r = baseCol.r, g = baseCol.g, b = baseCol.b;
                    float mx = max(r, max(g, b));
                    float sat = mx - min(r, min(g, b));
                    float warm = saturate((r - g - 0.30) * 6.0) * saturate((sat - 0.20) * 8.0);
                    float cool = saturate((b - g) * 12.0) * saturate((sat - 0.04) * 12.0);
                    float wRoof = max(warm, cool);
                    float wStone = saturate((0.10 - sat) * 30.0);
                    float wWall = saturate((mx - 0.74) * 12.0) * saturate((0.36 - sat) * 10.0);
                    float wWood = saturate(1.0 - wRoof - wStone - wWall);

                    // 瓦垄要顺着坡面走：三平面投影会把斜屋面拆成两个方向，瓦垄被拉成十字。
                    float3 cRoof = SampleRoof(_Roof, wp, nrm, _TexTile * _RoofTileMul);
                    float3 cStone = SampleTri(_Stone, wp, nrm, _TexTile * _StoneTileMul);
                    float3 cWood = SampleTri(_Wood, wp, nrm, _TexTile * _WoodTileMul);
                    float3 cWall = SampleTri(_Path, wp, nrm, _TexTile * _WallTileMul);
                    float3 L = float3(0.30, 0.59, 0.11);
                    // 每张图平均亮度不同，先各自归一化，否则粉墙会把墙提亮成白板。
                    // 权重加权和为 1，所以整体明暗保持顶点色原样，只有纹理起伏。
                    float ratio = (wRoof * max(dot(cRoof, L), 1e-3) / _LumRoof
                                 + wStone * max(dot(cStone, L), 1e-3) / _LumStone
                                 + wWood * max(dot(cWood, L), 1e-3) / _LumWood
                                 + wWall * max(dot(cWall, L), 1e-3) / _LumWall)
                                / max(wRoof + wStone + wWood + wWall, 1e-3);
                    detail = baseCol * clamp(ratio, 0.05, 2.0);
                }
                // 直接混进贴图颜色。用亮度去乘会把草地乘成一块块脏斑。
                float3 painted = lerp(baseCol, detail, 0.82);
                painted = lerp(baseCol, painted, _TexMix);
                return ApplyDetail(painted, wp, nrm);
            }

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                fixed4 color : COLOR;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 wn : TEXCOORD0;
                float3 wp : TEXCOORD1;
                fixed4 col : COLOR;
                SHADOW_COORDS(2)
                UNITY_FOG_COORDS(3)
            };

            float3 Sway(float3 wp, float w)
            {
                float t = _Time.y;
                float s = sin(t * 1.7 + wp.x * 0.35 + wp.z * 0.27) * 0.6 + sin(t * 2.9 + wp.x * 0.9) * 0.25;
                return float3(s, 0, s * 0.6) * w * _Wind * 0.14;
            }

            v2f vert(appdata v)
            {
                v2f o;
                float3 wp = mul(unity_ObjectToWorld, v.vertex).xyz;
                wp += Sway(wp, 1 - v.color.a);
                o.pos = UnityWorldToClipPos(wp);
                o.wp = wp;
                o.wn = UnityObjectToWorldNormal(v.normal);
                o.col = fixed4(v.color.rgb, 1) * _Color;
                TRANSFER_SHADOW(o);
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float3 n = normalize(i.wn);
                // 无切线凹凸：细节层亮度当高度场，屏幕空间梯度扰动法线（Mikkelsen 曲面梯度）
                if (_BumpStrength > 0.001)
                {
                    float h = dot(SampleTri(_DetailTex, i.wp, n, _DetailTile), float3(0.30, 0.59, 0.11));
                    float3 sx = ddx(i.wp), sy = ddy(i.wp);
                    float hx = ddx(h), hy = ddy(h);
                    float3 r1 = cross(sy, n);
                    float3 r2 = cross(n, sx);
                    float det = dot(sx, r1);
                    float3 grad = sign(det) * (hx * r1 + hy * r2);
                    n = normalize(abs(det) * n - grad * (_BumpStrength * 0.12));
                }
                float3 l = normalize(_WorldSpaceLightPos0.xyz);
                float3 vd = normalize(_WorldSpaceCameraPos.xyz - i.wp);
                float ndl = dot(n, l);
                float atten = SHADOW_ATTENUATION(i);

                float lit = smoothstep(0.0, _Soft, ndl) * smoothstep(0.35, 0.55, atten);
                float hi = smoothstep(0.82, 0.82 + _Soft, ndl) * lit;

                float3 baseCol = Paint(i.col.rgb, i.wp, n);
                float luma = dot(baseCol, float3(0.30, 0.50, 0.20));
                baseCol = saturate(lerp(float3(luma, luma, luma), baseCol, 1.05));
                float3 litCol = baseCol * _LightColor0.rgb;
                float3 shdCol = baseCol * _ShadowTint.rgb;
                float3 c = lerp(shdCol, litCol, lit);
                c += baseCol * hi * 0.10;

                float rim = pow(saturate(1 - dot(n, vd)), 4) * _RimColor.a;
                c += _RimColor.rgb * rim * (0.45 + 0.7 * lit);

                fixed4 col = fixed4(c, 1);
                UNITY_APPLY_FOG(i.fogCoord, col);
                return col;
            }
            ENDCG
        }

        Pass
        {
            Name "SHADOWCASTER"
            Tags { "LightMode"="ShadowCaster" }

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_shadowcaster
            #include "UnityCG.cginc"

            struct v2f { V2F_SHADOW_CASTER; };

            v2f vert(appdata_base v)
            {
                v2f o;
                TRANSFER_SHADOW_CASTER_NORMALOFFSET(o)
                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                SHADOW_CASTER_FRAGMENT(i)
            }
            ENDCG
        }
    }
    Fallback Off
}
