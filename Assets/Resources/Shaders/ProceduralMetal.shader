Shader "RuneForge/ProceduralMetal"
{
    Properties
    {
        _Color ("Base Color", Color) = (0.7, 0.7, 0.75, 1)
        _Metallic ("Metallic", Range(0, 1)) = 0.9
        _Smoothness ("Smoothness", Range(0, 1)) = 0.8
        _ReflectionColor ("Reflection Color", Color) = (0.8, 0.85, 1.0, 1)
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
                float _Metallic;
                float _Smoothness;
                float4 _ReflectionColor;
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

                // Specular - Blinn-Phong
                float3 halfDir = normalize(lightDir + viewDir);
                float NdotH = saturate(dot(normal, halfDir));
                float specPower = exp2(_Smoothness * 12.0);
                float spec = pow(NdotH, specPower) * _Smoothness;
                float3 specColor = lerp(float3(0.04, 0.04, 0.04), _Color.rgb, _Metallic);
                float3 specular = specColor * lightColor * spec * 2.0;

                // Fake environment reflection using view reflection direction
                float3 reflDir = reflect(-viewDir, normal);
                // Gradient sky: bright at top, dark at bottom
                float skyGradient = reflDir.y * 0.5 + 0.5;
                float3 envReflection = lerp(float3(0.05, 0.05, 0.08), _ReflectionColor.rgb, skyGradient);
                envReflection *= _Metallic * _Smoothness;

                // Fresnel - view angle color shift (anisotropic-like effect)
                float NdotV = saturate(dot(normal, viewDir));
                float fresnel = pow(1.0 - NdotV, 4.0);
                float3 fresnelColor = lerp(_Color.rgb, _ReflectionColor.rgb, fresnel * 0.5);

                // Combine
                float3 ambient = fresnelColor * 0.15;
                float3 finalColor = ambient + diffuse * (1.0 - _Metallic * 0.6) + specular + envReflection;

                // Subtle anisotropic color shift
                float aniso = abs(dot(normal, float3(viewDir.x, 0, viewDir.z)));
                finalColor += _ReflectionColor.rgb * aniso * 0.05 * _Metallic;

                return half4(finalColor, 1.0);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
