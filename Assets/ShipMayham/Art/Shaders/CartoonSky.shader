// Soft three-colour gradient sky with a warm sun glow and a few drifting cloud bands. Unlit, no textures, WebGL friendly.
Shader "ShipMayham/CartoonSky"
{
    Properties
    {
        _TopColor ("Zenith", Color) = (0.22, 0.50, 0.92, 1)
        _HorizonColor ("Horizon", Color) = (0.74, 0.90, 0.98, 1)
        _BottomColor ("Below Horizon", Color) = (0.60, 0.80, 0.92, 1)
        _SunColor ("Sun Glow", Color) = (1.0, 0.93, 0.72, 1)
        _SunDirection ("Sun Direction", Vector) = (0.45, 0.55, 0.7, 0)
        _CloudColor ("Cloud", Color) = (1, 1, 1, 1)
        _CloudAmount ("Cloud Amount", Range(0, 1)) = 0.55
        _CloudSpeed ("Cloud Speed", Range(0, 0.05)) = 0.006
    }

    SubShader
    {
        Tags { "RenderType" = "Background" "RenderPipeline" = "UniversalPipeline" "Queue" = "Background" "PreviewType" = "Skybox" }
        Cull Off ZWrite Off

        Pass
        {
            Name "Sky"
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _TopColor;
                half4 _HorizonColor;
                half4 _BottomColor;
                half4 _SunColor;
                float4 _SunDirection;
                half4 _CloudColor;
                half _CloudAmount;
                half _CloudSpeed;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 dir : TEXCOORD0; };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.dir = input.positionOS.xyz;
                return output;
            }

            float Hash(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }
            float Noise(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                f = f * f * (3 - 2 * f);
                return lerp(lerp(Hash(i), Hash(i + float2(1, 0)), f.x), lerp(Hash(i + float2(0, 1)), Hash(i + float2(1, 1)), f.x), f.y);
            }

            half4 frag(Varyings input) : SV_Target
            {
                float3 dir = normalize(input.dir);
                float height = dir.y;
                half3 sky = lerp(_HorizonColor.rgb, _TopColor.rgb, pow(saturate(height), 0.55));
                sky = lerp(sky, _BottomColor.rgb, saturate(-height * 6));

                // Cartoon clouds: flattened noise on a plane projected from the view direction, thresholded into soft blobs.
                float2 uv = dir.xz / max(0.12, height + 0.35) * 1.4 + _Time.y * _CloudSpeed * float2(1, 0.4);
                float n = Noise(uv * 1.6) * 0.6 + Noise(uv * 3.7) * 0.3 + Noise(uv * 8.0) * 0.1;
                half cloud = smoothstep(1 - _CloudAmount * 0.75, 1.02 - _CloudAmount * 0.4, n) * saturate(height * 3.5 + 0.1);
                sky = lerp(sky, _CloudColor.rgb, cloud * 0.9);

                float sun = saturate(dot(dir, normalize(_SunDirection.xyz)));
                sky += _SunColor.rgb * (pow(sun, 700) * 2.0 + pow(sun, 18) * 0.25);
                return half4(sky, 1);
            }
            ENDHLSL
        }
    }
}
