Shader "RuneForge/ProceduralBuff"
{
    Properties
    {
        _Color ("Color", Color) = (1, 0.6, 0.1, 1)
        _Radius ("Radius", Range(0.1, 2.0)) = 0.8
        _DashCount ("Dash Count", Range(2, 20)) = 8
        _DashRatio ("Dash Ratio", Range(0.1, 0.9)) = 0.5
        _RotateSpeed ("Rotate Speed", Range(0.1, 5.0)) = 1.0
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float _Radius;
                float _DashCount;
                float _DashRatio;
                float _RotateSpeed;
            CBUFFER_END

            #define PI 3.14159265

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionOS : TEXCOORD0;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.positionOS = IN.positionOS.xyz;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float2 centered = IN.positionOS.xz;
                float dist = length(centered);

                // Ring mask with thin width
                float ringWidth = 0.04;
                float ringMask = smoothstep(_Radius - ringWidth, _Radius - ringWidth * 0.5, dist)
                               * (1.0 - smoothstep(_Radius + ringWidth * 0.5, _Radius + ringWidth, dist));

                if (ringMask < 0.01) discard;

                // Dashed pattern based on angle + rotation
                float angle = atan2(centered.y, centered.x) + _Time.y * _RotateSpeed;
                // Normalize angle to 0-1 range
                float angleNorm = frac(angle / (2.0 * PI));
                // Create dash pattern
                float dashPhase = frac(angleNorm * _DashCount);
                float dashMask = step(dashPhase, _DashRatio);

                float3 color = _Color.rgb * 2.0; // Emissive brightness

                float alpha = ringMask * dashMask * 0.7;

                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
