Shader "RuneForge/ProceduralStone"
{
    Properties
    {
        _Color ("Base Color", Color) = (0.4, 0.38, 0.35, 1)
        _Color2 ("Variation Color", Color) = (0.3, 0.28, 0.25, 1)
        _NoiseScale ("Noise Scale", Range(1.0, 50.0)) = 12.0
        _Roughness ("Roughness", Range(0.0, 1.0)) = 0.7
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
                float4 _Color2;
                float _NoiseScale;
                float _Roughness;
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

            float hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float hash31(float3 p)
            {
                p = frac(p * float3(123.34, 456.21, 789.53));
                p += dot(p, p.yzx + 45.32);
                return frac(p.x * p.y * p.z);
            }

            float noise3D(float3 p)
            {
                float3 i = floor(p);
                float3 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);

                float n000 = hash31(i);
                float n100 = hash31(i + float3(1, 0, 0));
                float n010 = hash31(i + float3(0, 1, 0));
                float n110 = hash31(i + float3(1, 1, 0));
                float n001 = hash31(i + float3(0, 0, 1));
                float n101 = hash31(i + float3(1, 0, 1));
                float n011 = hash31(i + float3(0, 1, 1));
                float n111 = hash31(i + float3(1, 1, 1));

                float n00 = lerp(n000, n100, f.x);
                float n01 = lerp(n001, n101, f.x);
                float n10 = lerp(n010, n110, f.x);
                float n11 = lerp(n011, n111, f.x);

                float n0 = lerp(n00, n10, f.y);
                float n1 = lerp(n01, n11, f.y);

                return lerp(n0, n1, f.z);
            }

            float fbm3D(float3 p)
            {
                float value = 0.0;
                float amplitude = 0.5;
                for (int i = 0; i < 5; i++)
                {
                    value += amplitude * noise3D(p);
                    p *= 2.0;
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
                float3 normalWS = normalize(IN.normalWS);
                float3 viewDirWS = normalize(_WorldSpaceCameraPos.xyz - IN.positionWS);

                float NdotL = saturate(dot(normalWS, lightDir));

                float3 samplePos = IN.positionWS * _NoiseScale * 0.1;

                // Large-scale color variation
                float largeNoise = fbm3D(samplePos);

                // Medium-scale detail
                float medNoise = fbm3D(samplePos * 2.5);

                // Fine detail for roughness
                float fineNoise = noise3D(samplePos * 8.0);

                // Color blending between base colors
                float3 stoneColor = lerp(_Color.rgb, _Color2.rgb, largeNoise);

                // Add subtle warm/cool variation
                float3 warmTint = float3(0.05, 0.03, 0.0);
                float3 coolTint = float3(0.0, 0.02, 0.04);
                stoneColor += lerp(coolTint, warmTint, medNoise);

                // Dark crevice lines using step on noise
                float creviceNoise = fbm3D(samplePos * 3.0);
                float crevices = smoothstep(0.35, 0.4, creviceNoise) * smoothstep(0.65, 0.6, creviceNoise);
                crevices = 1.0 - crevices * _Roughness * 0.6;

                // Additional thin dark lines
                float lineNoise = noise3D(samplePos * 6.0);
                float thinLines = 1.0 - (1.0 - smoothstep(0.48, 0.50, lineNoise) * smoothstep(0.52, 0.50, lineNoise)) * _Roughness * 0.4;

                stoneColor *= crevices * thinLines;

                // Roughness modulation on lighting
                float roughnessVar = lerp(1.0, fineNoise, _Roughness * 0.3);

                // Lighting
                float3 ambient = float3(0.1, 0.1, 0.1);
                float3 diffuse = NdotL * lightColor * roughnessVar;
                float3 litColor = stoneColor * (diffuse + ambient);

                // Very subtle specular for wet stone look (inverse roughness)
                float3 halfDir = normalize(lightDir + viewDirWS);
                float spec = pow(saturate(dot(normalWS, halfDir)), lerp(8.0, 64.0, 1.0 - _Roughness));
                litColor += lightColor * spec * (1.0 - _Roughness) * 0.15;

                return half4(litColor, 1.0);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
