Shader "MiningOperation/GasVolumeURP"
{
    Properties
    {
        _GasColorA ("Core Color", Color) = (0.16, 0.50, 0.68, 1)
        _GasColorB ("Outer Color", Color) = (0.48, 0.73, 0.81, 1)
        _GasOpacity ("Density", Range(0, 1)) = 0.2
        _GasNoiseScale ("Noise Scale", Range(0.25, 3)) = 1
        _GasBrightness ("Brightness", Range(0.25, 2)) = 1
        _GasContrast ("Density Variation", Range(0, 1)) = 0.35
        _GasDrift ("Drift", Range(0, 1)) = 0.3
        _GasSteps ("Ray Steps", Range(6, 24)) = 10
        _GasLightDirection ("Light Direction", Vector) = (-0.42, 0.71, -0.56, 0)
        _GasLightColor ("Light Color", Color) = (1, 1, 1, 1)
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Transparent" "Queue" = "Transparent+10" }
        Pass
        {
            Name "GasVolume"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _GasColorA;
                half4 _GasColorB;
                float _GasOpacity;
                float _GasNoiseScale;
                float _GasBrightness;
                float _GasContrast;
                float _GasDrift;
                float _GasSteps;
                float4 _GasLightDirection;
                half4 _GasLightColor;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 color : COLOR;
                float4 cloud : TEXCOORD1; // local center xyz, radius w
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionOS : TEXCOORD0;
                float4 cloud : TEXCOORD1;
                float2 visual : TEXCOORD2;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.positionOS = input.positionOS.xyz;
                output.cloud = input.cloud;
                output.visual = input.color.rg; // density multiplier, seed offset
                return output;
            }

            float Hash3(float3 p)
            {
                return frac(sin(dot(p, float3(127.1, 311.7, 74.7))) * 43758.5453);
            }
            float ValueNoise(float3 p)
            {
                float3 i = floor(p);
                float3 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float a = lerp(Hash3(i), Hash3(i + float3(1, 0, 0)), f.x);
                float b = lerp(Hash3(i + float3(0, 1, 0)), Hash3(i + float3(1, 1, 0)), f.x);
                float c = lerp(Hash3(i + float3(0, 0, 1)), Hash3(i + float3(1, 0, 1)), f.x);
                float d = lerp(Hash3(i + float3(0, 1, 1)), Hash3(i + float3(1, 1, 1)), f.x);
                return lerp(lerp(a, b, f.y), lerp(c, d, f.y), f.z);
            }

            half4 Frag(Varyings input, FRONT_FACE_TYPE face : FRONT_FACE_SEMANTIC) : SV_Target
            {
                float3 center = input.cloud.xyz;
                float radius = max(input.cloud.w, 0.001);
                float3 origin = TransformWorldToObject(_WorldSpaceCameraPos.xyz);
                float3 ray = normalize(input.positionOS - origin);
                float3 relative = origin - center;
                float b = dot(relative, ray);
                float c = dot(relative, relative) - radius * radius;
                float discriminant = b * b - c;
                clip(discriminant - 0.000001);

                bool inside = dot(relative, relative) < radius * radius;
                bool front = IS_FRONT_VFACE(face, true, false);
                // Rasterize only the entry face when outside and only the exit face inside.
                if ((front && inside) || (!front && !inside)) discard;

                float root = sqrt(max(discriminant, 0));
                float enter = max(0, -b - root);
                float exit = -b + root;
                if (exit <= enter) discard;
                int steps = clamp((int)_GasSteps, 6, 24);
                float stepLength = (exit - enter) / steps;
                float3 integratedColor = 0;
                float transmission = 1.0;
                float3 seed = float3(input.visual.y * 17.7, input.visual.y * 51.3, input.visual.y * 29.1);
                float3 drift = float3(0.014, -0.009, 0.012) * (_Time.y * _GasDrift);
                float3 lightDir = normalize(_GasLightDirection.xyz + float3(0.0001, 0, 0));

                [loop]
                for (int i = 0; i < 24; ++i)
                {
                    if (i >= steps) break;
                    float t = enter + (i + 0.5) * stepLength;
                    float3 q = (relative + ray * t) / radius;
                    float n = ValueNoise(q * (3.4 * _GasNoiseScale) + seed + drift);
                    float distortedRadius = length(q) + (n - 0.5) * 0.26 * _GasContrast;
                    float radial = 1.0 - smoothstep(0.46, 0.99, distortedRadius);
                    if (radial <= 0.0001) continue;
                    float wisps = 0.5 + 0.5 * sin(dot(q, float3(11.0, 8.4, 13.1)) + seed.x);
                    float structure = lerp(0.68, saturate(n * 0.85 + wisps * 0.25), _GasContrast);
                    float density = radial * structure * input.visual.x;
                    float extinction = density * _GasOpacity * (stepLength / radius) * 3.6;
                    float alpha = 1.0 - exp(-extinction);

                    float lit = 0.70 + 0.35 * saturate(dot(q, lightDir) + 0.35);
                    float3 tint = lerp(_GasColorA.rgb, _GasColorB.rgb, saturate(n * 0.7 + length(q) * 0.3));
                    integratedColor += transmission * alpha * tint * lit * _GasBrightness * _GasLightColor.rgb;
                    transmission *= (1.0 - alpha);
                    if (transmission < 0.02) break;
                }
                float opacity = saturate(1.0 - transmission);
                clip(opacity - 0.001);
                return half4(integratedColor / max(opacity, 0.001), opacity);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
