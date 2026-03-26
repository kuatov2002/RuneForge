Shader "RuneForge/ProceduralLava"
{
    Properties
    {
        _Color ("Crust Color", Color) = (0.15, 0.08, 0.05, 1)
        _CrackColor ("Crack Color", Color) = (1.0, 0.4, 0.0, 1)
        _CrackIntensity ("Crack Intensity", Range(0.5, 5.0)) = 2.5
        _FlowSpeed ("Flow Speed", Range(0.0, 2.0)) = 0.4
        _NoiseScale ("Noise Scale", Range(1.0, 30.0)) = 4.0
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
                float4 _CrackColor;
                float _CrackIntensity;
                float _FlowSpeed;
                float _NoiseScale;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
            };

            float hash2D(float2 p)
            {
                return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
            }

            float valueNoise(float2 p)
            {
                float2 ip = floor(p);
                float2 fp = frac(p);
                fp = fp * fp * (3.0 - 2.0 * fp);

                float a = hash2D(ip);
                float b = hash2D(ip + float2(1, 0));
                float c = hash2D(ip + float2(0, 1));
                float d = hash2D(ip + float2(1, 1));

                return lerp(lerp(a, b, fp.x), lerp(c, d, fp.x), fp.y);
            }

            float fbmNoise(float2 p)
            {
                float val = 0.0;
                float amp = 0.5;
                val += amp * valueNoise(p); p *= 2.01; amp *= 0.5;
                val += amp * valueNoise(p); p *= 2.01; amp *= 0.5;
                val += amp * valueNoise(p); p *= 2.01; amp *= 0.5;
                val += amp * valueNoise(p);
                return val;
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float3 nrm = normalize(IN.normalWS);
                float ndl = saturate(dot(nrm, normalize(float3(0.5, 0.7, -0.3))));

                float t = _Time.y * _FlowSpeed;
                float2 wuv = IN.positionWS.xz * _NoiseScale * 0.1;

                // Two layers of scrolling noise for crack pattern
                float n1 = fbmNoise(wuv * 3.0 + float2(t * 0.3, t * 0.1));
                float n2 = fbmNoise(wuv * 4.5 + float2(-t * 0.2, t * 0.35));

                // Create crack lines from noise derivatives
                float crack1 = saturate(1.0 - abs(n1 - 0.5) * 6.0);
                float crack2 = saturate(1.0 - abs(n2 - 0.45) * 7.0);
                float cracks = saturate(crack1 + crack2 * 0.7);

                // Modulate with slower noise
                float flow = fbmNoise(wuv * 1.5 + float2(t * 0.15, -t * 0.1));
                cracks *= lerp(0.4, 1.0, flow);

                // Colors
                float3 crust = _Color.rgb;
                float3 hot = _CrackColor.rgb * _CrackIntensity;
                float3 veryHot = float3(1.0, 0.85, 0.3) * _CrackIntensity;
                float3 crackCol = lerp(hot, veryHot, cracks * flow);

                // Lit crust
                float3 ambient = float3(0.05, 0.02, 0.01);
                float3 litCrust = crust * (ndl * float3(0.8, 0.85, 1.0) * 0.5 + ambient);

                // Blend crust and cracks
                float3 result = lerp(litCrust, crackCol, cracks);

                // Add glow from cracks
                result += crackCol * cracks * 0.3;

                return half4(result, 1.0);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
