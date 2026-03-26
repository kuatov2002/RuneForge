Shader "RuneForge/ProceduralFire"
{
    Properties
    {
        _BottomColor ("Bottom Color", Color) = (1.0, 0.3, 0.0, 1)
        _TopColor ("Top Color", Color) = (1.0, 0.9, 0.1, 1)
        _ScrollSpeed ("Scroll Speed", Range(0.5, 5.0)) = 2.0
        _NoiseScale ("Noise Scale", Range(1.0, 20.0)) = 6.0
        _Intensity ("Intensity", Range(0.5, 5.0)) = 1.5
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
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BottomColor;
                float4 _TopColor;
                float _ScrollSpeed;
                float _NoiseScale;
                float _Intensity;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
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
                float b = hash(i + float2(1.0, 0.0));
                float c = hash(i + float2(0.0, 1.0));
                float d = hash(i + float2(1.0, 1.0));

                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            float fbm(float2 p)
            {
                float value = 0.0;
                float amplitude = 0.5;
                float frequency = 1.0;
                for (int i = 0; i < 5; i++)
                {
                    value += amplitude * valueNoise(p * frequency);
                    frequency *= 2.2;
                    amplitude *= 0.45;
                }
                return value;
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float2 uv = IN.uv;
                float time = _Time.y;

                // Scroll UV upward through noise
                float2 noiseUV = uv * _NoiseScale;
                noiseUV.y -= time * _ScrollSpeed;

                // Multiple noise layers for fire turbulence
                float noise1 = fbm(noiseUV);
                float noise2 = fbm(noiseUV * 1.5 + float2(5.3, 1.2));
                float noise3 = valueNoise(noiseUV * 3.0 + float2(-time * 0.5, 0.0));

                float fireNoise = noise1 * 0.6 + noise2 * 0.3 + noise3 * 0.1;

                // Fire shape: narrower at top, wider at bottom
                float centerDist = abs(uv.x - 0.5) * 2.0; // 0 at center, 1 at edges
                float heightFade = 1.0 - uv.y; // 1 at bottom, 0 at top

                // Flame shape mask
                float flameWidth = heightFade * 0.8 + 0.1;
                float flameMask = 1.0 - smoothstep(flameWidth * 0.3, flameWidth, centerDist);

                // Combine noise with flame shape
                float fireFactor = fireNoise * flameMask * heightFade;
                fireFactor = saturate(fireFactor * 1.5);

                // Flickering
                float flicker = sin(time * 8.0) * 0.05 + sin(time * 13.0) * 0.03 + 1.0;
                fireFactor *= flicker;

                // Color gradient: bottom (red-orange) to top (yellow)
                float colorGradient = saturate(fireFactor * 2.0);
                float3 fireColor = lerp(_BottomColor.rgb, _TopColor.rgb, colorGradient);

                // Brighten core
                float core = saturate(fireFactor * 3.0 - 0.5);
                fireColor = lerp(fireColor, float3(1.0, 1.0, 0.8), core * 0.5);

                // Apply intensity
                fireColor *= _Intensity;

                // Alpha: solid at bottom, fades at top and edges
                float alpha = fireFactor;
                // Extra fade at very top
                alpha *= smoothstep(1.0, 0.7, uv.y);

                // Emission glow
                float3 emission = fireColor * fireFactor * 0.5;
                fireColor += emission;

                return half4(fireColor, saturate(alpha));
            }
            ENDHLSL
        }
    }
    FallBack Off
}
