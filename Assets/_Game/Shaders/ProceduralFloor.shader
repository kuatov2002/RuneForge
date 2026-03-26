Shader "RuneForge/ProceduralFloor"
{
    Properties
    {
        _Color ("Tile Color A", Color) = (0.45, 0.4, 0.35, 1)
        _Color2 ("Tile Color B", Color) = (0.35, 0.3, 0.27, 1)
        _GridColor ("Grid Line Color", Color) = (0.15, 0.12, 0.1, 1)
        _TileSize ("Tile Size", Float) = 1.0
        _GridWidth ("Grid Width", Range(0.01, 0.15)) = 0.05
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
                float4 _GridColor;
                float _TileSize;
                float _GridWidth;
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

                // World-space tiling
                float2 worldUV = IN.positionWS.xz / _TileSize;
                float2 tileIndex = floor(worldUV);
                float2 tileUV = frac(worldUV);

                // Checkerboard pattern
                float checker = fmod(abs(tileIndex.x) + abs(tileIndex.y), 2.0);
                float3 tileColor = lerp(_Color.rgb, _Color2.rgb, checker);

                // Grid lines
                float2 gridDist = min(tileUV, 1.0 - tileUV);
                float gridLine = 1.0 - smoothstep(0.0, _GridWidth, min(gridDist.x, gridDist.y));

                // Dirt/wear noise variation
                float noise1 = valueNoise(IN.positionWS.xz * 3.0);
                float noise2 = valueNoise(IN.positionWS.xz * 7.5);
                float dirt = noise1 * 0.7 + noise2 * 0.3;
                dirt = dirt * 0.15 - 0.075; // subtle variation

                // Per-tile slight color variation
                float tileVariation = hash(tileIndex) * 0.08 - 0.04;

                float3 baseColor = tileColor + dirt + tileVariation;
                float3 finalBaseColor = lerp(baseColor, _GridColor.rgb, gridLine);

                // Lighting
                float NdotL = saturate(dot(normal, lightDir));
                float3 ambient = finalBaseColor * 0.3;
                float3 diffuse = finalBaseColor * lightColor * NdotL;

                float3 finalColor = ambient + diffuse;

                return half4(finalColor, 1.0);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
