// Stylized cartoon ocean for URP. Unlit, banded toon lighting, scrolling caustic-style foam lines,
// sparkle highlights and gentle vertex swell. No depth/opaque texture is required, so it works on WebGL.
Shader "ShipMayham/StylizedOcean"
{
    Properties
    {
        _ShallowColor ("Shallow Color", Color) = (0.14, 0.72, 0.84, 1)
        _DeepColor ("Deep Color", Color) = (0.03, 0.30, 0.60, 1)
        _HorizonColor ("Horizon Tint", Color) = (0.66, 0.93, 0.96, 1)
        _FoamColor ("Foam Color", Color) = (1, 1, 1, 1)
        _WaveHeight ("Wave Height (m)", Range(0, 1)) = 0.3
        _WaveLength ("Wave Length (m)", Range(2, 60)) = 16
        _WaveSpeed ("Wave Speed", Range(0, 3)) = 0.7
        _PatternScale ("Foam Line Scale", Range(0.05, 2)) = 0.8
        _PatternSpeed ("Foam Line Speed", Range(0, 2)) = 0.22
        _FoamAmount ("Foam Line Amount", Range(0, 1)) = 0.5
        _CrestFoam ("Crest Foam", Range(0, 1)) = 0.5
        _BandCount ("Shading Bands", Range(1, 8)) = 4
        _SparkleStrength ("Sparkle Strength", Range(0, 2)) = 0.9
        _FresnelPower ("Fresnel Power", Range(0.5, 8)) = 3
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" "IgnoreProjector" = "True" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            half4 _ShallowColor;
            half4 _DeepColor;
            half4 _HorizonColor;
            half4 _FoamColor;
            float _WaveHeight;
            float _WaveLength;
            float _WaveSpeed;
            float _PatternScale;
            float _PatternSpeed;
            half _FoamAmount;
            half _CrestFoam;
            float _BandCount;
            half _SparkleStrength;
            half _FresnelPower;
        CBUFFER_END

        // Three crossing swells. Returns height in x and the horizontal gradient in yz (analytic, no extra taps).
        float3 Swell(float2 p, float t)
        {
            float k = 6.2831853 / max(_WaveLength, 0.001);
            const float2 d0 = float2(0.8, 0.6);
            const float2 d1 = float2(-0.55, 0.835);
            const float2 d2 = float2(0.2, -0.98);
            float a0 = k * dot(d0, p) + t * _WaveSpeed * 1.00;
            float a1 = 1.7 * k * dot(d1, p) + t * _WaveSpeed * 1.35 + 1.3;
            float a2 = 2.9 * k * dot(d2, p) + t * _WaveSpeed * 1.80 + 4.1;
            float h = 0.55 * sin(a0) + 0.30 * sin(a1) + 0.15 * sin(a2);
            float2 g = 0.55 * k * cos(a0) * d0 + 0.30 * 1.7 * k * cos(a1) * d1 + 0.15 * 2.9 * k * cos(a2) * d2;
            return float3(h * _WaveHeight, g * _WaveHeight);
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Cull Back
            ZWrite On

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float4 swell : TEXCOORD1;   // x: height, yz: gradient, w: fog factor
            };

            float2 Hash22(float2 p)
            {
                float3 q = frac(float3(p.xyx) * float3(0.1031, 0.1030, 0.0973));
                q += dot(q, q.yzx + 33.33);
                return frac((q.xx + q.yz) * q.zy);
            }

            float Noise(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float a = Hash22(i).x, b = Hash22(i + float2(1, 0)).x, c = Hash22(i + float2(0, 1)).x, d = Hash22(i + float2(1, 1)).x;
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            // Cellular distance field: F2-F1 is ~0 along cell borders, which draws the wobbly foam network.
            float CellEdges(float2 p)
            {
                float2 ip = floor(p), fp = frac(p);
                float f1 = 8.0, f2 = 8.0;
                [unroll] for (int j = -1; j <= 1; j++)
                {
                    [unroll] for (int i = -1; i <= 1; i++)
                    {
                        float2 g = float2(i, j);
                        float2 o = Hash22(ip + g);
                        o = 0.5 + 0.5 * sin(_Time.y * _PatternSpeed * 2.0 + 6.2831 * o);
                        float d = length(g + o - fp);
                        if (d < f1) { f2 = f1; f1 = d; } else if (d < f2) { f2 = d; }
                    }
                }
                return f2 - f1;
            }

            Varyings Vert(Attributes v)
            {
                Varyings o;
                float3 ws = TransformObjectToWorld(v.positionOS.xyz);
                float3 s = Swell(ws.xz, _Time.y);
                ws.y += s.x;
                o.positionWS = ws;
                o.positionCS = TransformWorldToHClip(ws);
                o.swell = float4(s, ComputeFogFactor(o.positionCS.z));
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                // Perturb the swell normal with a little scrolling noise so highlights break up into glints.
                float2 wp = i.positionWS.xz;
                float2 nz = float2(Noise(wp * 0.9 + _Time.y * 0.35), Noise(wp * 0.9 - _Time.y * 0.3 + 7.1)) - 0.5;
                float3 n = normalize(float3(-i.swell.y * 2.5 + nz.x * 0.35, 1.0, -i.swell.z * 2.5 + nz.y * 0.35));
                float3 v = normalize(GetCameraPositionWS() - i.positionWS);
                float ndv = saturate(dot(n, v));
                float fres = pow(1.0 - ndv, _FresnelPower);
                float dist = distance(GetCameraPositionWS(), i.positionWS);

                // Water body: height + slow large-scale drift + distance darkening for depth.
                float drift = 0.5 + 0.5 * sin(wp.x * 0.021 + _Time.y * 0.05) * sin(wp.y * 0.017 + 1.3);
                float heightTone = saturate(0.5 + i.swell.x / max(_WaveHeight, 0.001) * 0.5);
                half3 col = lerp(_DeepColor.rgb, _ShallowColor.rgb, saturate(heightTone * 0.6 + drift * 0.3));
                col = lerp(col, _DeepColor.rgb * 0.82, saturate(dist / 220.0) * 0.55);

                // Banded diffuse from the sun so the shading reads as cartoon steps.
                Light light = GetMainLight();
                float ndl = dot(n, light.direction) * 0.5 + 0.5;
                float bands = floor(ndl * _BandCount) / max(_BandCount - 1.0, 1.0);
                col *= lerp(0.8, 1.14, saturate(bands));

                // Two foam-line layers at different scales + streaks that follow the swell direction.
                float2 uv = wp * _PatternScale;
                float e1 = CellEdges(uv);
                float e2 = CellEdges(uv * 2.3 + 11.7);
                float lines = (1.0 - smoothstep(0.018, 0.085, e1)) * 0.75 + (1.0 - smoothstep(0.02, 0.07, e2)) * 0.35;
                float lineFade = saturate(1.0 - dist / 140.0);                         // far water stays calm and clean
                float streak = smoothstep(0.82, 1.0, sin(dot(wp, float2(0.8, 0.6)) * 0.9 + Noise(wp * 0.15) * 6.0 + _Time.y * 0.6));
                float crest = smoothstep(0.58, 0.92, heightTone) * _CrestFoam;
                float foam = saturate(lines * _FoamAmount * lineFade * (0.4 + 0.6 * heightTone) + crest * (0.55 + 0.45 * streak) + streak * 0.12 * lineFade);
                col = lerp(col, _FoamColor.rgb, foam);

                // Hard sparkle glints on sun-facing slopes.
                float3 h = normalize(light.direction + v);
                float glint = step(0.992, saturate(dot(n, h)));
                col += light.color * glint * _SparkleStrength * (1.0 - foam);

                // Horizon / grazing tint.
                col = lerp(col, _HorizonColor.rgb, saturate(fres * 0.9));
                col = MixFog(col, i.swell.w);
                return half4(col, 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ColorMask R
            ZWrite On

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex DepthVert
            #pragma fragment DepthFrag

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; };

            Varyings DepthVert(Attributes v)
            {
                Varyings o;
                float3 ws = TransformObjectToWorld(v.positionOS.xyz);
                ws.y += Swell(ws.xz, _Time.y).x;
                o.positionCS = TransformWorldToHClip(ws);
                return o;
            }

            half DepthFrag(Varyings i) : SV_Target { return i.positionCS.z; }
            ENDHLSL
        }
    }
    FallBack Off
}
