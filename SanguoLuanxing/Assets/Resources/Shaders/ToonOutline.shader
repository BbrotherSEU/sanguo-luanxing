// Sanguo/Toon + 外扩描边。描边沿 TEXCOORD3 中的平滑法线外扩（MeshKit 写入），
// 按裁剪空间偏移，屏幕上线宽恒定；描边颜色取顶点色压暗，保持同色系的卡通勾线。
Shader "Sanguo/ToonOutline"
{
    Properties
    {
        _Color ("Tint", Color) = (1,1,1,1)
        _ShadowTint ("Shadow Tint", Color) = (0.62,0.58,0.80,1)
        _RimColor ("Rim (a = strength)", Color) = (1,0.96,0.85,0.28)
        _Soft ("Band Softness", Range(0.001,0.3)) = 0.05
        _Wind ("Wind", Float) = 1
        _OutlineColor ("Outline Color", Color) = (0.22,0.16,0.14,1)
        _OutlineWidth ("Outline Width", Float) = 0.28
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

        UsePass "Sanguo/Toon/FORWARD"

        Pass
        {
            Name "OUTLINE"
            Tags { "LightMode"="Always" }
            Cull Front
            ZWrite On

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "UnityCG.cginc"

            fixed4 _OutlineColor;
            float _OutlineWidth, _Wind;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                fixed4 color : COLOR;
                float3 smoothN : TEXCOORD3;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                fixed4 col : COLOR;
                UNITY_FOG_COORDS(0)
            };

            v2f vert(appdata v)
            {
                v2f o;
                float3 wp = mul(unity_ObjectToWorld, v.vertex).xyz;
                float t = _Time.y;
                float s = sin(t * 1.7 + wp.x * 0.35 + wp.z * 0.27) * 0.6 + sin(t * 2.9 + wp.x * 0.9) * 0.25;
                wp += float3(s, 0, s * 0.6) * (1 - v.color.a) * _Wind * 0.14;

                float3 n = dot(v.smoothN, v.smoothN) > 0.01 ? v.smoothN : v.normal;
                float3 wn = normalize(UnityObjectToWorldNormal(n));
                float4 cp = UnityWorldToClipPos(wp);
                float2 cn = mul((float3x3)UNITY_MATRIX_VP, wn).xy;
                float len = max(length(cn), 1e-4);
                cn /= len;
                cn.x *= _ScreenParams.y / _ScreenParams.x;
                // 远处收细，避免远景糊成一团黑
                float w = _OutlineWidth * 0.01 * saturate(30.0 / max(cp.w, 1.0));
                cp.xy += cn * w * cp.w;
                o.pos = cp;
                o.col = fixed4(v.color.rgb * _OutlineColor.rgb, 1);
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 c = i.col;
                UNITY_APPLY_FOG(i.fogCoord, c);
                return c;
            }
            ENDCG
        }

        UsePass "Sanguo/Toon/SHADOWCASTER"
    }
    Fallback Off
}
