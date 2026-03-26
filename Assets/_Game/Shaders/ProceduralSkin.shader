Shader "RuneForge/ProceduralSkin"
{
    Properties
    {
        _Color ("Skin Color", Color) = (0.8, 0.6, 0.5, 1)
        _SubsurfaceColor ("Subsurface Color", Color) = (0.9, 0.3, 0.2, 1)
        _SubsurfaceStrength ("Subsurface Strength", Range(0.0, 1.0)) = 0.5
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
                float4 _SubsurfaceColor;
                float _SubsurfaceStrength;
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

                float NdotL = dot(normalWS, lightDir);

                // Wrap lighting for softer diffuse (skin looks soft, not harsh)
                float wrapDiffuse = saturate(NdotL * 0.5 + 0.5);
                float wrapDiffuse2 = wrapDiffuse * wrapDiffuse; // softer falloff

                // Fake subsurface scattering
                float sssWrap = saturate(-NdotL * 0.5 + 0.5);
                float sssFresnel = 1.0 - saturate(dot(normalWS, viewDirWS));
                float sssEdge = pow(sssFresnel, 2.0);

                // View-dependent translucency: light shining through thin parts
                float3 backLightDir = normalize(lightDir + normalWS * 0.3);
                float VdotBackLight = saturate(dot(viewDirWS, -backLightDir));
                float translucency = pow(VdotBackLight, 3.0) * _SubsurfaceStrength;

                // Subtle procedural variation for skin texture
                float3 samplePos = IN.positionWS * 15.0;
                float skinNoise = noise3D(samplePos) * 0.08 - 0.04;
                float fineNoise = noise3D(samplePos * 4.0) * 0.03 - 0.015;

                // Base skin color with subtle variation
                float3 skinColor = _Color.rgb + skinNoise + fineNoise;

                // Subsurface color contribution
                float3 sssColor = _SubsurfaceColor.rgb;

                // Combine lighting
                float3 ambient = float3(0.12, 0.10, 0.10);
                float3 diffuse = wrapDiffuse2 * lightColor;

                // SSS: edges get tinted with subsurface color
                float3 subsurface = sssColor * (sssEdge * 0.5 + sssWrap * 0.3 + translucency) * _SubsurfaceStrength;

                float3 litColor = skinColor * (diffuse + ambient);
                litColor += subsurface * lightColor * 0.5;

                // Soft specular (skin has a subtle sheen, not sharp)
                float3 halfDir = normalize(lightDir + viewDirWS);
                float spec = pow(saturate(dot(normalWS, halfDir)), 16.0);
                litColor += lightColor * spec * 0.08;

                // Subtle rim light for depth (warm tint)
                float rim = pow(sssFresnel, 3.0);
                litColor += sssColor * rim * _SubsurfaceStrength * 0.2 * lightColor;

                return half4(litColor, 1.0);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
