Shader "RuneForge/ProceduralPoison"
{
    Properties
    {
        _Color ("Base Color", Color) = (0.1, 0.2, 0.05, 0.8)
        _BubbleColor ("Bubble Color", Color) = (0.3, 1.0, 0.1, 1)
        _BubbleSpeed ("Bubble Speed", Range(0.1, 3.0)) = 1.0
        _BubbleScale ("Bubble Scale", Range(1.0, 20.0)) = 8.0
        _Opacity ("Opacity", Range(0, 1)) = 0.75
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
                float4 _BubbleColor;
                float _BubbleSpeed;
                float _BubbleScale;
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

            // Generates circular bubble shapes
            float bubbles(float2 uv, float timeOffset)
            {
                float result = 0.0;
                float2 cellId = floor(uv);
                float2 cellUV = frac(uv);

                for (int x = -1; x <= 1; x++)
                {
                    for (int y = -1; y <= 1; y++)
                    {
                        float2 neighbor = float2(x, y);
                        float2 id = cellId + neighbor;
                        float h = hash(id);

                        // Random bubble center within cell
                        float2 center = float2(hash(id + 0.1), hash(id + 0.2)) * 0.6 + 0.2;

                        // Animate upward with wrapping
                        float speed = (h * 0.5 + 0.5) * _BubbleSpeed;
                        center.y = frac(center.y - _Time.y * speed * 0.3 + timeOffset);

                        float2 diff = cellUV - neighbor - center;
                        float dist = length(diff);

                        // Bubble radius varies
                        float radius = h * 0.12 + 0.06;

                        // Soft circle
                        float bubble = 1.0 - smoothstep(radius * 0.5, radius, dist);

                        // Pulsing
                        bubble *= 0.7 + 0.3 * sin(_Time.y * 2.0 + h * 6.28);

                        result += bubble;
                    }
                }
                return saturate(result);
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

                // World-space UVs
                float2 uv = IN.positionWS.xz * _BubbleScale * 0.1;

                // Dark splotchy base
                float splotch = valueNoise(uv * 3.0) * 0.5 + valueNoise(uv * 6.0) * 0.3;
                float3 baseColor = _Color.rgb * (0.7 + splotch * 0.3);

                // Bubbles - two layers for depth
                float bub1 = bubbles(uv * 1.0, 0.0);
                float bub2 = bubbles(uv * 1.5 + 3.7, 0.5);
                float totalBubbles = saturate(bub1 + bub2 * 0.6);

                float3 surfaceColor = lerp(baseColor, _BubbleColor.rgb, totalBubbles * 0.8);

                // Global pulse
                float pulse = sin(_Time.y * 1.5) * 0.05 + 1.0;
                surfaceColor *= pulse;

                // Lighting
                float NdotL = saturate(dot(normal, lightDir));
                float3 ambient = surfaceColor * 0.4;
                float3 diffuse = surfaceColor * lightColor * NdotL;

                float3 finalColor = ambient + diffuse;

                // Emission from bubbles
                finalColor += _BubbleColor.rgb * totalBubbles * 0.3;

                float alpha = _Opacity;

                return half4(finalColor, alpha);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
