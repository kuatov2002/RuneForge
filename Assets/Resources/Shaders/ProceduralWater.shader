Shader "RuneForge/ProceduralWater"
{
    Properties
    {
        _Color ("Base Color", Color) = (0.1, 0.3, 0.5, 1)
        _WaveColor ("Wave Highlight Color", Color) = (0.5, 0.8, 1.0, 1)
        _WaveSpeed ("Wave Speed", Range(0.1, 5.0)) = 1.5
        _WaveScale ("Wave Scale", Range(1.0, 30.0)) = 8.0
        _Opacity ("Opacity", Range(0.0, 1.0)) = 0.75
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
                float4 _WaveColor;
                float _WaveSpeed;
                float _WaveScale;
                float _Opacity;
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
                float3 normalWS = normalize(IN.normalWS);
                float3 viewDirWS = normalize(_WorldSpaceCameraPos.xyz - IN.positionWS);

                float NdotL = saturate(dot(normalWS, lightDir));

                float time = _Time.y * _WaveSpeed;
                float2 worldUV = IN.positionWS.xz * _WaveScale * 0.1;

                // Overlapping sine waves at different angles for ripples
                float wave1 = sin(worldUV.x * 6.0 + time * 1.0) * 0.5 + 0.5;
                float wave2 = sin(worldUV.y * 5.0 + time * 1.3) * 0.5 + 0.5;
                float wave3 = sin((worldUV.x + worldUV.y) * 4.0 + time * 0.9) * 0.5 + 0.5;
                float wave4 = sin((worldUV.x - worldUV.y) * 3.5 + time * 1.1) * 0.5 + 0.5;
                float wave5 = sin(worldUV.x * 8.0 + worldUV.y * 3.0 + time * 1.7) * 0.5 + 0.5;
                float wave6 = sin(worldUV.x * 2.0 - worldUV.y * 7.0 + time * 0.8) * 0.5 + 0.5;

                // Combine waves
                float waves = (wave1 + wave2 + wave3 + wave4 + wave5 + wave6) / 6.0;

                // Create sharper ripple lines
                float ripple = pow(waves, 2.0);
                float highlight = pow(waves, 8.0);

                // Fresnel for edge brightness
                float fresnel = 1.0 - saturate(dot(normalWS, viewDirWS));
                float rim = pow(fresnel, 2.5);

                // Color variation based on wave height
                float3 deepColor = _Color.rgb;
                float3 shallowColor = _WaveColor.rgb;
                float3 waterColor = lerp(deepColor, shallowColor, ripple * 0.4);

                // Lighting
                float3 ambient = float3(0.08, 0.12, 0.18);
                float3 litColor = waterColor * (NdotL * lightColor * 0.6 + ambient);

                // Add wave highlights (specular-like)
                litColor += shallowColor * highlight * 0.6;

                // Fresnel rim
                litColor += _WaveColor.rgb * rim * 0.4;

                // Specular highlight
                float3 halfDir = normalize(lightDir + viewDirWS);
                float spec = pow(saturate(dot(normalWS, halfDir)), 64.0);
                litColor += lightColor * spec * 0.3;

                // Opacity varies slightly with waves and fresnel
                float alpha = lerp(_Opacity * 0.8, _Opacity, rim + ripple * 0.2);

                return half4(litColor, alpha);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
