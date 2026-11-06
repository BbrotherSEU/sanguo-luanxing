// 顶点色不受光：天穹、远山剪影、云朵。_FogMix 控制受雾程度（天穹为 0）。
Shader "Sanguo/Unlit"
{
    Properties
    {
        _Color ("Tint", Color) = (1,1,1,1)
        _FogMix ("Fog Mix", Range(0,1)) = 1
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry+20" }

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "UnityCG.cginc"

            fixed4 _Color;
            float _FogMix;

            struct appdata { float4 vertex : POSITION; fixed4 color : COLOR; };
            struct v2f
            {
                float4 pos : SV_POSITION;
                fixed4 col : COLOR;
                UNITY_FOG_COORDS(0)
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.col = v.color * _Color;
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 c = fixed4(i.col.rgb, 1);
                fixed4 f = c;
                UNITY_APPLY_FOG(i.fogCoord, f);
                return lerp(c, f, _FogMix);
            }
            ENDCG
        }
    }
}
