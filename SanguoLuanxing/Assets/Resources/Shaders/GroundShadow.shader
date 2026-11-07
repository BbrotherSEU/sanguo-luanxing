// 接地阴影：贴在物件脚下的软暗斑。
// 顶点色 alpha 画衰减（中心浓、边缘 0），_Strength 是全局强度，
// 由昼夜循环驱动 —— 夜里没有直射光，影子就该淡掉。
// 用乘法混合（DstColor Zero）而不是盖一层黑：地面被压暗但不失真，
// 草地还是绿的，不会灰成脏色。
// 队列排在 Transparent 之前（Transparent-50），保证影子压在所有立绘卡下面。
Shader "Sanguo/GroundShadow"
{
    Properties
    {
        _Strength ("Strength", Range(0,1.5)) = 1
        _FogStart ("Fog Start", Float) = 40
        _FogEnd ("Fog End", Float) = 100
    }
    SubShader
    {
        Tags { "Queue"="Transparent-50" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Cull Off
        ZWrite Off
        Blend DstColor Zero

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #pragma multi_compile_fog
            #include "UnityCG.cginc"

            float _Strength;
            float _FogStart;
            float _FogEnd;

            struct appdata
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                fixed4 col : COLOR;
                float fog : TEXCOORD1;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.col = v.color;
                // 自己按雾的起止距离算衰减：
                // fogCoord 在桌面端是原始 z、在移动端才是因子，不能直接用；
                // 这里用视空间距离自己算，跨平台一致。
                float3 wp = mul(unity_ObjectToWorld, v.vertex).xyz;
                float dist = distance(wp, _WorldSpaceCameraPos);
                o.fog = saturate((_FogEnd - dist) / max(1.0, _FogEnd - _FogStart));
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // 顶点色的 alpha 就是这片影子的浓度（中心浓、边缘 0）
                float a = saturate(i.col.a) * _Strength;
                // 远处地面已经被雾冲淡，影子也要跟着淡掉，否则远处会看到一排黑斑
                a *= i.fog;
                // 越靠近中心压得越暗；边缘回到 1（完全不改变原色）
                float kr = 1.0 - a * 0.62f;
                // 蓝通道压得少一点，阴影偏冷，和环境光的冷色一致
                float kb = 1.0 - a * 0.52f;
                return fixed4(kr, kr, kb, 1.0);
            }
            ENDCG
        }
    }
}
