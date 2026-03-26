Shader "RuneForge/ProceduralIce"
{
    Properties
    {
        _Color ("Base Color", Color) = (0.1, 0.2, 0.4, 1)
        _RimColor ("Rim Color", Color) = (0.7, 0.9, 1.0, 1)
        _RimPower ("Rim Power", Range(0.5, 8.0)) = 3.0
        _Opacity ("Opacity", Range(0.0, 1.0)) = 0.7
        _NoiseScale ("Noise Scale", Range(1.0, 50.0)) = 10.0
        _NoiseSpeed ("Noise Speed", Range(0.0, 2.0)) = 0.3
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }

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
                float4 _RimColor;
                float _RimPower;
                float _Opacity;
                float _NoiseScale;
                float _NoiseSpeed;
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

            // Simple hash-based noise
            float hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float noise2D(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);

                float a = hash21(i);
                float b = hash21(i + float2(1.0, 0.0));
                float c = hash21(i + float2(0.0, 1.0));
                float d = hash21(i + float2(1.0, 1.0));

                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            float fbm(float2 p)
            {
                float value = 0.0;
                float amplitude = 0.5;
                for (int i = 0; i < 4; i++)
                {
                    value += amplitude * noise2D(p);
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

                // Main light
                float NdotL = saturate(dot(normalWS, lightDir));
                float3 diffuse = lightColor * NdotL;

                // Fresnel rim
                float fresnel = 1.0 - saturate(dot(normalWS, viewDirWS));
                float rim = pow(fresnel, _RimPower);

                // Animated internal crystalline noise
                float time = _Time.y * _NoiseSpeed;
                float2 noiseUV = IN.positionWS.xz * _NoiseScale * 0.1;
                float n1 = fbm(noiseUV + float2(time * 0.7, time * 0.3));
                float n2 = fbm(noiseUV * 1.5 + float2(-time * 0.5, time * 0.6));
                float crystalPattern = saturate(n1 * 0.6 + n2 * 0.4);

                // Sine-based sparkle highlights
                float sparkle = sin(IN.positionWS.x * _NoiseScale + time * 2.0)
                              * sin(IN.positionWS.z * _NoiseScale * 1.3 + time * 1.7)
                              * sin(IN.positionWS.y * _NoiseScale * 0.8 + time * 1.1);
                sparkle = pow(saturate(sparkle), 4.0) * 0.5;

                // Mix base color with highlights
                float3 baseColor = _Color.rgb;
                float3 highlight = float3(0.8, 0.95, 1.0);
                float3 surfaceColor = lerp(baseColor, highlight, crystalPattern * 0.5 + sparkle);

                // Apply lighting
                float3 ambient = float3(0.1, 0.15, 0.2);
                float3 litColor = surfaceColor * (diffuse + ambient);

                // Add rim glow
                litColor += _RimColor.rgb * rim * 1.5;

                // Add crystalline highlights on top
                litColor += highlight * sparkle * 0.3;

                // Opacity varies with fresnel for icy look
                float alpha = lerp(_Opacity * 0.7, _Opacity, fresnel);

                return half4(litColor, alpha);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
