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
            #pragma vertex SpriteVert
            #pragma fragment frag
            #pragma target 3.0
            #pragma multi_compile_instancing
            #pragma multi_compile_local _ PIXELSNAP_ON
            #pragma multi_compile _ ETC1_EXTERNAL_ALPHA
            #include "UnitySprites.cginc"

            fixed _Flash;
            fixed4 _FlashColor, _OutlineColor;
            float _OutlineU, _OutlineV, _LightTop, _LightBottom;

            fixed4 frag(v2f IN) : SV_Target
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
                return body;
            }
            ENDCG
        }
    }
}
