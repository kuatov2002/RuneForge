Shader "RuneForge/ProceduralGold"
{
    Properties
    {
        _Color ("Gold Color", Color) = (1.0, 0.84, 0.0, 1)
        _SparkleSpeed ("Sparkle Speed", Range(0.5, 10.0)) = 3.0
        _SparkleIntensity ("Sparkle Intensity", Range(0, 5)) = 2.5
        _Metallic ("Metallic", Range(0, 1)) = 0.95
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
                float _SparkleSpeed;
                float _SparkleIntensity;
                float _Metallic;
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

            float hash3(float3 p)
            {
                return frac(sin(dot(p, float3(127.1, 311.7, 74.7))) * 43758.5453);
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
                float3 viewDir = normalize(_WorldSpaceCameraPos.xyz - IN.positionWS);

                // Diffuse
                float NdotL = saturate(dot(normal, lightDir));
                float3 diffuse = _Color.rgb * lightColor * NdotL;

                // Strong specular for gold
                float3 halfDir = normalize(lightDir + viewDir);
                float NdotH = saturate(dot(normal, halfDir));
                float spec = pow(NdotH, 256.0) * _Metallic;
                float3 specular = _Color.rgb * lightColor * spec * 2.0;

                // Fresnel
                float NdotV = saturate(dot(normal, viewDir));
                float fresnel = pow(1.0 - NdotV, 3.0);
                float3 fresnelColor = lerp(_Color.rgb, float3(1.0, 0.95, 0.7), fresnel * 0.4);

                // Sparkle effect - random points in world space that flash over time
                float3 sparklePos = floor(IN.positionWS * 15.0); // grid of sparkle cells
                float sparkleRand = hash3(sparklePos);

                // Each sparkle point has its own phase
                float sparklePhase = sparkleRand * 6.2831;
                float sparkleTime = sin(_Time.y * _SparkleSpeed + sparklePhase);
                // Only show sparkle when sin wave is very high (brief flash)
                float sparkle = saturate((sparkleTime - 0.85) * 8.0);
                // Only some cells sparkle
                sparkle *= step(0.7, sparkleRand);

                float3 sparkleColor = float3(1.0, 0.95, 0.8) * sparkle * _SparkleIntensity;

                // Fake environment reflection
                float3 reflDir = reflect(-viewDir, normal);
                float envBright = reflDir.y * 0.5 + 0.5;
                float3 envRefl = _Color.rgb * envBright * _Metallic * 0.4;

                // Combine
                float3 ambient = fresnelColor * 0.2;
                float3 finalColor = ambient + diffuse * (1.0 - _Metallic * 0.5) + specular + envRefl + sparkleColor;

                // Emission from sparkles
                float3 emission = sparkleColor * 0.5;
                finalColor += emission;

                return half4(finalColor, 1.0);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
