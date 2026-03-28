Shader "RuneForge/ProceduralPlayerZone"
{
    Properties
    {
        _Color ("Color", Color) = (0.2, 0.5, 1.0, 1)
        _Radius ("Radius", Range(0.1, 2.0)) = 1.0
        _EdgeSoftness ("Edge Softness", Range(0.01, 1.0)) = 0.3
        _PulseSpeed ("Pulse Speed", Range(0.1, 5.0)) = 1.5
        _Alpha ("Alpha", Range(0, 1)) = 0.3
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
                float _EdgeSoftness;
                float _PulseSpeed;
                float _Alpha;
            CBUFFER_END

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

            float hash(float2 p)
            {
                return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
            }

            float valueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float a = hash(i);
                float b = hash(i + float2(1, 0));
                float c = hash(i + float2(0, 1));
                float d = hash(i + float2(1, 1));
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

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

                // Radial gradient: dense center -> transparent edges
                float gradient = 1.0 - smoothstep(0.0, _Radius, dist);

                if (gradient < 0.01) discard;

                // Soft edge falloff
                float softEdge = smoothstep(_Radius, _Radius - _EdgeSoftness, dist);

                // Subtle noise for organic feel
                float2 noiseUV = centered * 3.0 + _Time.y * 0.1;
                float noise = valueNoise(noiseUV) * 0.15;

                // Pulse alpha ±0.1
                float pulse = sin(_Time.y * _PulseSpeed) * 0.1;

                float3 color = _Color.rgb;
                // Slight brightness at center
                color += _Color.rgb * gradient * 0.3;

                float alpha = (_Alpha + pulse + noise) * softEdge * gradient;
                alpha = saturate(alpha);

                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
