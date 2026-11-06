// 轻量后处理：亮部泛光 + 色彩分级（暗部偏冷/亮部偏暖/提饱和）+ 暗角。
// 内置管线 OnRenderImage 使用，只作用于相机画面，IMGUI 界面不受影响。
Shader "Sanguo/PostFx"
{
    Properties
    {
        _MainTex ("Source", 2D) = "white" {}
        _BloomTex ("Bloom", 2D) = "black" {}
        _Threshold ("Bloom Threshold", Range(0,1)) = 0.88
        _BloomStrength ("Bloom Strength", Range(0,1)) = 0.18
        _Saturation ("Saturation", Range(0.5,1.6)) = 1.06
        _Vignette ("Vignette", Range(0,1)) = 0.26
        _Grade ("Grade Amount", Range(0,1)) = 0.40
    }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always

        CGINCLUDE
        #include "UnityCG.cginc"
        sampler2D _MainTex, _BloomTex;
        float4 _MainTex_TexelSize;
        float _Threshold, _BloomStrength, _Saturation, _Vignette, _Grade;
        float2 _BlurDir;

        struct v2f
        {
            float4 pos : SV_POSITION;
            float2 uv : TEXCOORD0;
        };

        v2f vertImg(appdata_img v)
        {
            v2f o;
            o.pos = UnityObjectToClipPos(v.vertex);
            o.uv = v.texcoord.xy;
            return o;
        }
        ENDCG

        // 0) 亮部提取（1/4 分辨率）
        Pass
        {
            CGPROGRAM
            #pragma vertex vertImg
            #pragma fragment frag
            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 c = tex2D(_MainTex, i.uv);
                float l = dot(c.rgb, float3(0.30, 0.59, 0.11));
                float k = smoothstep(_Threshold, _Threshold + 0.28, l);
                return fixed4(c.rgb * k, 1);
            }
            ENDCG
        }

        // 1) 分离高斯模糊（_BlurDir 指定方向）
        Pass
        {
            CGPROGRAM
            #pragma vertex vertImg
            #pragma fragment frag
            fixed4 frag(v2f i) : SV_Target
            {
                float2 t = _MainTex_TexelSize.xy * _BlurDir * 1.6;
                fixed3 c = tex2D(_MainTex, i.uv).rgb * 0.227027;
                c += tex2D(_MainTex, i.uv + t * 1.3846).rgb * 0.316216;
                c += tex2D(_MainTex, i.uv - t * 1.3846).rgb * 0.316216;
                c += tex2D(_MainTex, i.uv + t * 3.2308).rgb * 0.070270;
                c += tex2D(_MainTex, i.uv - t * 3.2308).rgb * 0.070270;
                return fixed4(c, 1);
            }
            ENDCG
        }

        // 2) 合成：泛光 + 分级 + 暗角
        Pass
        {
            CGPROGRAM
            #pragma vertex vertImg
            #pragma fragment frag
            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 c = tex2D(_MainTex, i.uv);
                c.rgb += tex2D(_BloomTex, i.uv).rgb * _BloomStrength;
                float l = dot(c.rgb, float3(0.30, 0.59, 0.11));
                c.rgb = lerp(float3(l, l, l), c.rgb, _Saturation);
                // 暗部压一点冷色，亮部添一点暖色，画面不那么"平"
                c.rgb += float3(0.010, 0.016, 0.034) * (1 - saturate(l * 2.2)) * _Grade;
                c.rgb += float3(0.030, 0.018, 0.004) * saturate((l - 0.55) * 1.8) * _Grade;
                float2 d = i.uv - 0.5;
                c.rgb *= saturate(1 - dot(d, d) * _Vignette * 2.6);
                return c;
            }
            ENDCG
        }
    }
    Fallback Off
}
