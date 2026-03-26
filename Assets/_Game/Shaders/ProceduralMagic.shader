Shader "RuneForge/ProceduralMagic"
{
    Properties
    {
        _Color ("Color", Color) = (0.2, 0.5, 1.0, 1)
        _Intensity ("Intensity", Range(0.5, 10.0)) = 3.0
        _PulseSpeed ("Pulse Speed", Range(0.1, 5.0)) = 1.5
        _SwirlSpeed ("Swirl Speed", Range(0.1, 5.0)) = 1.0
        _SwirlScale ("Swirl Scale", Range(1.0, 20.0)) = 6.0
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
                float _Intensity;
                float _PulseSpeed;
                float _SwirlSpeed;
                float _SwirlScale;
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
                float3 positionOS : TEXCOORD3;
            };

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

            #define PI 3.14159265

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                OUT.uv = IN.uv;
                OUT.positionOS = IN.positionOS.xyz;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float3 normalWS = normalize(IN.normalWS);
                float3 viewDirWS = normalize(_WorldSpaceCameraPos.xyz - IN.positionWS);

                float time = _Time.y;

                // Use object space for swirl so it looks contained
                float3 objPos = IN.positionOS;

                // Convert to polar-ish coordinates from object center
                float2 centered = objPos.xz;
                float radius = length(centered);
                float angle = atan2(centered.y, centered.x);

                // Swirling distortion
                float swirlTime = time * _SwirlSpeed;
                float swirlAngle = angle + radius * _SwirlScale + swirlTime;
                float2 swirlUV = float2(cos(swirlAngle), sin(swirlAngle)) * radius * _SwirlScale;
                swirlUV += objPos.y * 2.0;

                // Layered swirl noise
                float swirl1 = fbm(swirlUV + float2(swirlTime * 0.3, swirlTime * 0.7));
                float swirl2 = fbm(swirlUV * 1.5 + float2(-swirlTime * 0.5, swirlTime * 0.4));
                float swirlPattern = swirl1 * 0.6 + swirl2 * 0.4;

                // Second layer using world position for more complexity
                float2 worldSwirl = IN.positionWS.xz * _SwirlScale * 0.1;
                float worldAngle = atan2(worldSwirl.y, worldSwirl.x) + time * _SwirlSpeed * 0.5;
                float worldRadius = length(worldSwirl);
                float2 worldSwirlUV = float2(cos(worldAngle), sin(worldAngle)) * worldRadius;
                float worldPattern = fbm(worldSwirlUV + time * 0.2);

                float combinedPattern = saturate(swirlPattern * 0.7 + worldPattern * 0.3);

                // Pulse
                float pulse = sin(time * _PulseSpeed) * 0.3 + 0.7;
                float fastPulse = sin(time * _PulseSpeed * 3.0) * 0.1 + 0.9;

                // Fresnel rim glow
                float fresnel = 1.0 - saturate(dot(normalWS, viewDirWS));
                float rim = pow(fresnel, 2.0);

                // Energy brightness from pattern
                float energy = combinedPattern * pulse * fastPulse;
                float brightSpots = pow(combinedPattern, 3.0) * 2.0;

                // Base magical color
                float3 baseColor = _Color.rgb;
                float3 brightColor = _Color.rgb * _Intensity;
                float3 coreColor = lerp(baseColor, brightColor, energy);

                // Add hot core (white-shifted at peaks)
                float3 hotWhite = lerp(brightColor, float3(1, 1, 1) * _Intensity, brightSpots * 0.3);
                coreColor = lerp(coreColor, hotWhite, brightSpots * 0.5);

                // Rim glow
                float3 rimColor = _Color.rgb * _Intensity * 1.5;
                coreColor += rimColor * rim * pulse;

                // Overall emission intensity
                coreColor *= _Intensity * 0.5;

                // Alpha: more opaque at center, glowing at edges
                float alpha = saturate(0.6 + energy * 0.3 + rim * 0.4);

                return half4(coreColor, alpha);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
