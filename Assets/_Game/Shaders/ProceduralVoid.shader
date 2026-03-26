Shader "RuneForge/ProceduralVoid"
{
    Properties
    {
        _Color ("Void Color", Color) = (0.1, 0.02, 0.15, 1)
        _StarColor ("Star Color", Color) = (0.9, 0.8, 1.0, 1)
        _SwirlSpeed ("Swirl Speed", Range(0.1, 3.0)) = 0.5
        _DistortionStrength ("Distortion Strength", Range(0, 0.5)) = 0.15
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float4 _StarColor;
                float _SwirlSpeed;
                float _DistortionStrength;
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
                float3 normalWS : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
            };

            // Hardcoded light for top-down game
            static const float3 lightDir = normalize(float3(0.5, 0.7, -0.3));
            static const float3 lightColor = float3(0.8, 0.85, 1.0);

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
                for (int i = 0; i < 4; i++)
                {
                    value += amplitude * valueNoise(p);
                    p *= 2.1;
                    amplitude *= 0.5;
                }
                return value;
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                OUT.uv = IN.uv;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float3 normal = normalize(IN.normalWS);
                float time = _Time.y * _SwirlSpeed;

                // World-space UVs
                float2 uv = IN.positionWS.xz * 0.5;

                // Animated noise-based UV distortion (swirling effect)
                float2 distortOffset;
                distortOffset.x = fbm(uv * 2.0 + float2(time * 0.7, time * 0.3));
                distortOffset.y = fbm(uv * 2.0 + float2(-time * 0.4, time * 0.6) + 5.0);
                float2 distortedUV = uv + (distortOffset - 0.5) * _DistortionStrength;

                // Deep void base color with swirling dark patterns
                float voidPattern = fbm(distortedUV * 3.0 + time * 0.1);
                float3 baseColor = _Color.rgb * (0.5 + voidPattern * 0.5);

                // Add deep purple/blue swirl streaks
                float swirl = fbm(distortedUV * 4.0 - time * 0.15);
                float3 swirlColor = float3(0.15, 0.03, 0.25) * swirl;
                baseColor += swirlColor;

                // Star sparkle points
                float2 starGrid = distortedUV * 20.0;
                float2 starId = floor(starGrid);
                float2 starUV = frac(starGrid);
                float starHash = hash(starId);

                // Only some cells have stars
                float hasStar = step(0.82, starHash);

                // Star center position (random within cell)
                float2 starCenter = float2(hash(starId + 0.1), hash(starId + 0.2)) * 0.6 + 0.2;

                // Slow drift
                starCenter.x += sin(time * 0.5 + starHash * 6.28) * 0.05;
                starCenter.y += cos(time * 0.3 + starHash * 3.14) * 0.05;

                float starDist = length(starUV - starCenter);
                float star = (1.0 - smoothstep(0.0, 0.06, starDist)) * hasStar;

                // Twinkle
                float twinkle = sin(time * 3.0 + starHash * 20.0) * 0.5 + 0.5;
                star *= twinkle;

                float3 starContrib = _StarColor.rgb * star * 2.0;

                // Subtle lighting (void should be mostly self-lit)
                float NdotL = saturate(dot(normal, lightDir));
                float3 lit = baseColor * (0.7 + NdotL * 0.3 * lightColor);

                float3 finalColor = lit + starContrib;

                // Emission for the void glow
                finalColor += baseColor * 0.1;

                return half4(finalColor, 1.0);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
