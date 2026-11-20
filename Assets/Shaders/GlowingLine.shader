Shader "Custom/GlowingLine"
{
    Properties
    {
        _CoreColor ("Core Line Color", Color) = (0.75, 0.95, 1.0, 1.0)
        _GlowColor ("Glow Halo Color", Color) = (0.05, 0.70, 1.0, 0.40)
        _CoreWidth ("Core Width Ratio", Range(0.02, 0.5)) = 0.20
        _CoreHardness ("Core Edge Hardness", Range(0.5, 8.0)) = 2.5
        _GlowExponent ("Glow Falloff Exponent", Range(0.5, 5.0)) = 2.0
        _GlowIntensity ("Glow Intensity", Range(0.1, 4.0)) = 1.2
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src Blend", Float) = 5 // SrcAlpha
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst Blend", Float) = 10 // OneMinusSrcAlpha (Transparent)
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 0 // Off
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent+10"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
        }

        Blend [_SrcBlend] [_DstBlend]
        ZWrite Off
        Cull [_Cull]
        Lighting Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
            };

            fixed4 _CoreColor;
            fixed4 _GlowColor;
            float _CoreWidth;
            float _CoreHardness;
            float _GlowExponent;
            float _GlowIntensity;

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // In Unity LineRenderer, uv.y goes from 0.0 (one edge) to 1.0 (opposite edge)
                // dist is 0.0 at center line, 1.0 at outer edges
                float dist = abs(i.uv.y - 0.5) * 2.0;

                // 1. Narrow Core Line: sharp and centered
                float coreHalfWidth = _CoreWidth * 0.5;
                float coreEdge = max(0.002, fwidth(dist) * _CoreHardness);
                float coreFactor = 1.0 - smoothstep(coreHalfWidth - coreEdge, coreHalfWidth + coreEdge, dist);

                // 2. Transparent Glow Halo: smooth falloff from center to edges
                float glowFactor = pow(saturate(1.0 - dist), _GlowExponent);

                // Combine:
                fixed3 rgb = lerp(_GlowColor.rgb, _CoreColor.rgb, coreFactor) * (_GlowIntensity * glowFactor + coreFactor);
                float alpha = saturate(_GlowColor.a * glowFactor + _CoreColor.a * coreFactor);

                return fixed4(rgb, alpha) * i.color;
            }
            ENDCG
        }
    }
    FallBack "Unlit/Transparent"
}

