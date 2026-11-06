// 卡通水面：不透明、顶点轻微起伏、流动的白色水纹条带，接收阴影与雾。
// 顶点色 r 通道为岸边系数（1 = 贴岸），岸边自动变浅并出现泡沫线。
Shader "Sanguo/Water"
{
    Properties
    {
        _Deep ("Deep", Color) = (0.20,0.52,0.78,1)
        _Shallow ("Shallow", Color) = (0.42,0.80,0.90,1)
        _Foam ("Foam", Color) = (0.95,0.98,1,1)
        _Flow ("Flow Dir (xz)", Vector) = (0.4,0,1,0)
        _ShadowTint ("Shadow Tint", Color) = (0.62,0.62,0.82,1)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry+10" }

        Pass
        {
            Tags { "LightMode"="ForwardBase" }

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fwdbase
            #pragma multi_compile_fog
            #include "UnityCG.cginc"
            #include "Lighting.cginc"
            #include "AutoLight.cginc"

            fixed4 _Deep, _Shallow, _Foam, _ShadowTint;
            float4 _Flow;

            struct appdata { float4 vertex : POSITION; fixed4 color : COLOR; };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 wp : TEXCOORD0;
                fixed4 col : COLOR;
                SHADOW_COORDS(1)
                UNITY_FOG_COORDS(2)
            };

            v2f vert(appdata v)
            {
                v2f o;
                float3 wp = mul(unity_ObjectToWorld, v.vertex).xyz;
                float t = _Time.y;
                wp.y += sin(wp.x * 0.8 + t * 1.6) * 0.04 + sin(wp.z * 1.1 - t * 1.3) * 0.035;
                o.pos = UnityWorldToClipPos(wp);
                o.wp = wp;
                o.col = v.color;
                TRANSFER_SHADOW(o);
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float t = _Time.y;
                float2 p = i.wp.xz;
                float2 f = normalize(_Flow.xz + 1e-4);
                float along = dot(p, f);
                float across = dot(p, float2(-f.y, f.x));

                float shore = saturate(i.col.r);
                float3 c = lerp(_Deep.rgb, _Shallow.rgb, shore * 0.85);

                // 流动水纹：两层错位正弦，阈值化成卡通条带
                float w1 = sin(along * 1.4 - t * 2.2 + sin(across * 0.9 + t * 0.7) * 1.6);
                float w2 = sin(along * 2.3 - t * 3.1 + across * 1.7);
                float streak = step(0.93, w1 * 0.6 + w2 * 0.4 + 0.12);
                float foamLine = step(0.78, shore + sin(along * 3.0 - t * 2.0) * 0.06);
                c = lerp(c, _Foam.rgb, saturate(streak * 0.55 + foamLine * 0.85));

                float atten = SHADOW_ATTENUATION(i);
                float lit = smoothstep(0.35, 0.55, atten);
                c = lerp(c * _ShadowTint.rgb, c * (0.65 + 0.35 * _LightColor0.rgb), lit);

                fixed4 col = fixed4(c, 1);
                UNITY_APPLY_FOG(i.fogCoord, col);
                return col;
            }
            ENDCG
        }
    }
    Fallback Off
}
