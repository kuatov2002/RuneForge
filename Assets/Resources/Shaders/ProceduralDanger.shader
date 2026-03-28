Shader "RuneForge/ProceduralDanger"
{
    Properties
    {
        _Color ("Color", Color) = (1, 0.2, 0.1, 1)
        _InnerRadius ("Inner Radius", Range(0, 1)) = 0.3
        _OuterRadius ("Outer Radius", Range(0, 1)) = 0.9
        _FillProgress ("Fill Progress", Range(0, 1)) = 0.0
        _StripeCount ("Stripe Count", Range(1, 20)) = 8
        _ScrollSpeed ("Scroll Speed", Range(0.1, 10.0)) = 2.0
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
                float _InnerRadius;
                float _OuterRadius;
                float _FillProgress;
                float _StripeCount;
                float _ScrollSpeed;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionOS : TEXCOORD1;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                OUT.positionOS = IN.positionOS.xyz;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                // Use object-space XZ for radial calculation (cylinder is centered at origin)
                float2 centered = IN.positionOS.xz;
                float dist = length(centered);

                // Ring mask: visible between inner and outer radius
                float ringMask = smoothstep(_InnerRadius - 0.02, _InnerRadius, dist)
                               * (1.0 - smoothstep(_OuterRadius, _OuterRadius + 0.02, dist));

                if (ringMask < 0.01) discard;

                // Fill progress: ring fills from outer edge inward
                // Normalize dist within ring range
                float ringNorm = 1.0 - saturate((dist - _InnerRadius) / max(_OuterRadius - _InnerRadius, 0.001));
                float fillMask = step(ringNorm, _FillProgress);

                // Animated stripes based on angle
                float angle = atan2(centered.y, centered.x);
                float stripePattern = sin((angle * _StripeCount) + _Time.y * _ScrollSpeed * 6.28);
                float stripeMask = step(0.0, stripePattern);

                // Combine: stripes in unfilled area, solid in filled area
                float patternAlpha = lerp(stripeMask * 0.6, 1.0, fillMask);

                // Flash when fill complete (FillProgress >= 0.95)
                float flash = 0.0;
                if (_FillProgress >= 0.95)
                {
                    // 2 flashes in 0.1s = 20Hz
                    flash = step(0.5, frac(_Time.y * 20.0)) * 0.4;
                }

                float3 color = _Color.rgb * (1.0 + flash + fillMask * 0.5);

                // Emission glow
                color += _Color.rgb * fillMask * 0.8;

                float alpha = ringMask * patternAlpha * 0.6;
                alpha += flash * ringMask;
                alpha = saturate(alpha);

                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
