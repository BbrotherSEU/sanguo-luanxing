// 角色精灵：与 Sprites/Default 等价（含 flipX 所需的 _Flip），额外支持受击闪色 _Flash、
// alpha 膨胀描边（_OutlineU/_OutlineV 是 UV 偏移，由 C# 按源图像素算出）与纵向受光渐变。
Shader "Sanguo/Sprite"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _Flash ("Flash", Range(0,1)) = 0
        _FlashColor ("Flash Color", Color) = (1,1,1,1)
        _OutlineColor ("Outline Color", Color) = (0.16,0.12,0.10,1)
        _OutlineU ("Outline U", Range(0,0.06)) = 0
        _OutlineV ("Outline V", Range(0,0.06)) = 0
        _LightTop ("Top Light", Range(0.5,1.4)) = 1.05
        _LightBottom ("Bottom Shade", Range(0.3,1.2)) = 0.8
        [HideInInspector] _RendererColor ("RendererColor", Color) = (1,1,1,1)
        [HideInInspector] _Flip ("Flip", Vector) = (1,1,1,1)
        [PerRendererData] _AlphaTex ("External Alpha", 2D) = "white" {}
        [PerRendererData] _EnableExternalAlpha ("Enable External Alpha", Float) = 0
        _FogMix ("Fog Mix", Range(0,1)) = 1
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" "CanUseSpriteAtlas"="True" }
        Cull Off
        Lighting Off
        ZWrite Off
        Blend One OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex SpriteVertFog
            #pragma fragment frag
            #pragma target 3.0
            #pragma multi_compile_instancing
            #pragma multi_compile_local _ PIXELSNAP_ON
            #pragma multi_compile _ ETC1_EXTERNAL_ALPHA
            #pragma multi_compile_fog
            #include "UnitySprites.cginc"

            fixed _Flash;
            fixed4 _FlashColor, _OutlineColor;
            float _OutlineU, _OutlineV, _LightTop, _LightBottom;
            float _FogMix;

            // 自己写顶点阶段：UnitySprites.cginc 的 SpriteVert 不填 fogCoord，
            // 而片元阶段要用雾就得有雾坐标。
            struct v2fFog
            {
                float4 vertex   : SV_POSITION;
                fixed4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
                float fog : TEXCOORD1;
            };

            v2fFog SpriteVertFog(appdata_t IN)
            {
                v2fFog OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                OUT.vertex = UnityFlipSprite(IN.vertex, _Flip);
                OUT.vertex = UnityObjectToClipPos(OUT.vertex);
                OUT.texcoord = IN.texcoord;
                OUT.color = IN.color * _Color * _RendererColor;

                // 雾因子在顶点阶段算好：1 = 完全没有雾，0 = 雾最浓。
                // 用官方的 UNITY_CALC_FOG_FACTOR，它按当前雾模式（线性/指数）
                // 取对应的公式，换雾模式时这里不用改。
                // 注意不能用 UnitySprites.cginc 的 fogCoord ——
                // 桌面端它存的是裁剪空间 z，移动端存的才是因子，两端语义不同。
                UNITY_CALC_FOG_FACTOR(OUT.vertex.z);
                OUT.fog = saturate(unityFogFactor);

                #ifdef PIXELSNAP_ON
                OUT.vertex = UnityPixelSnap(OUT.vertex);
                #endif

                return OUT;
            }

            fixed4 frag(v2fFog IN) : SV_Target
            {
                fixed4 tex = SampleSpriteTexture(IN.texcoord);
                fixed4 body = tex * IN.color;
                body.rgb = lerp(body.rgb, _FlashColor.rgb, _Flash);
                // 受光渐变：脚底压暗贴地，头顶微亮，角色不飘
                body.rgb *= lerp(_LightBottom, _LightTop, saturate(IN.texcoord.y));

                if (_OutlineU > 0.0001)
                {
                    // 8 邻域 alpha 膨胀：本体之外的环形带填描边色
                    float2 t = float2(_OutlineU, _OutlineV);
                    float da = 0;
                    da = max(da, tex2D(_MainTex, IN.texcoord + float2( t.x, 0)).a);
                    da = max(da, tex2D(_MainTex, IN.texcoord + float2(-t.x, 0)).a);
                    da = max(da, tex2D(_MainTex, IN.texcoord + float2(0,  t.y)).a);
                    da = max(da, tex2D(_MainTex, IN.texcoord + float2(0, -t.y)).a);
                    da = max(da, tex2D(_MainTex, IN.texcoord + float2( t.x,  t.y)).a);
                    da = max(da, tex2D(_MainTex, IN.texcoord + float2(-t.x,  t.y)).a);
                    da = max(da, tex2D(_MainTex, IN.texcoord + float2( t.x, -t.y)).a);
                    da = max(da, tex2D(_MainTex, IN.texcoord + float2(-t.x, -t.y)).a);
                    da *= IN.color.a;
                    float ring = saturate(da - body.a) * _OutlineColor.a;
                    float tot = max(body.a, ring);
                    float mixv = tot > 1e-4 ? ring * (1.0 - body.a) / tot : 0.0;
                    body.rgb = lerp(body.rgb, _OutlineColor.rgb, mixv);
                    body.a = tot;
                }

                body.rgb *= body.a;
                // 雾：远处角色要跟着地面一起淡出，
                // 否则角色在雾里还是清清楚楚的，像浮在空中。
                // 雾因子在顶点阶段算好了（IN.fog：1 = 无雾，0 = 雾最浓）。
                // 只改 rgb 不改 alpha 的话，角色会变成一块"雾色方块"。
                float fogFac = IN.fog;
                // body.rgb 此时已经乘过 alpha，这里要还原成直通色再混雾。
                // alpha 极小时不做除法，直接用原色，避免除出 NaN。
                fixed3 lit = body.a > 1e-4 ? body.rgb / body.a : body.rgb;
                fixed3 fogged = lerp(lit, unity_FogColor.rgb, 1.0 - fogFac);
                body.rgb = lerp(lit, fogged, _FogMix) * body.a;
                body.a *= lerp(1.0, fogFac, _FogMix);
                return body;
            }
            ENDCG
        }
    }
}
