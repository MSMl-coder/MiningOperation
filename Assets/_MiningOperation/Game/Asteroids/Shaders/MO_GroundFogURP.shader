Shader "MiningOperation/GroundFogURP"
{
    Properties
    {
        _GasColorA ("Dense Color", Color) = (0.09, 0.17, 0.20, 1)
        _GasColorB ("Edge Color", Color) = (0.14, 0.22, 0.24, 1)
        _GasOpacity ("Optical Density", Range(0, 1)) = 0.22
        _GasNoiseScale ("Noise Scale", Range(0.25, 3)) = 1
        _GasBrightness ("Brightness", Range(0.25, 2)) = 1
        _GasContrast ("Density Variation", Range(0, 1)) = 0.6
        _GasDrift ("Drift", Range(0, 1)) = 0.12
        _GasSteps ("Ray Steps", Range(6, 24)) = 16
        _FogTopSoftness ("Top Softness", Range(0.15, 0.95)) = 0.7
        _GasLightDirection ("Directional Light", Vector) = (-0.42, 0.71, -0.56, 0)
        _GasLightColor ("Light Color", Color) = (0.5, 0.6, 0.65, 1)
    }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Transparent" "Queue" = "Transparent+10" }
        Pass
        {
            Name "GroundFog"
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
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _GasColorA;
                half4 _GasColorB;
                float _GasOpacity;
                float _GasNoiseScale;
                float _GasBrightness;
                float _GasContrast;
                float _GasDrift;
                float _GasSteps;
                float _FogTopSoftness;
                float4 _GasLightDirection;
                half4 _GasLightColor;
            CBUFFER_END

            // MaterialPropertyBlock data, set by the owning spawn point.
            float4 _FogLightPosRange[4];
            float4 _FogLightColors[4];
            int _FogLightCount;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 color : COLOR;
                float4 ellipsoid : TEXCOORD1; // World center XZ, horizontal radius, half-height
                float4 centerY : TEXCOORD2; // World center Y
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float4 ellipsoid : TEXCOORD1;
                float centerY : TEXCOORD2;
                float2 visual : TEXCOORD3;
            };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.ellipsoid = input.ellipsoid;
                output.centerY = input.centerY.x;
                output.visual = input.color.rg;
                return output;
            }

            float Hash3(float3 p)
            {
                return frac(sin(dot(p, float3(127.1, 311.7, 74.7))) * 43758.5453);
            }
            float ValueNoise(float3 p)
            {
                float3 i = floor(p), f = frac(p);
                f = f * f * (3 - 2 * f);
                float a = lerp(Hash3(i), Hash3(i + float3(1,0,0)), f.x);
                float b = lerp(Hash3(i + float3(0,1,0)), Hash3(i + float3(1,1,0)), f.x);
                float c = lerp(Hash3(i + float3(0,0,1)), Hash3(i + float3(1,0,1)), f.x);
                float d = lerp(Hash3(i + float3(0,1,1)), Hash3(i + float3(1,1,1)), f.x);
                return lerp(lerp(a,b,f.y), lerp(c,d,f.y), f.z);
            }

            half4 Frag(Varyings input, FRONT_FACE_TYPE face : FRONT_FACE_SEMANTIC) : SV_Target
            {
                float3 center = float3(input.ellipsoid.x, input.centerY, input.ellipsoid.y);
                float3 radii = float3(max(1, input.ellipsoid.z), max(0.01, input.ellipsoid.w), max(1, input.ellipsoid.z));
                float3 origin = _WorldSpaceCameraPos.xyz;
                float3 ray = normalize(input.positionWS - origin);
                float3 o = (origin - center) / radii;
                float3 d = ray / radii;
                float a = dot(d,d), b = dot(o,d), c = dot(o,o)-1;
                float discriminant = b*b - a*c;
                clip(discriminant - 0.0000001);
                bool inside = c < 0;
                bool front = IS_FRONT_VFACE(face, true, false);
                if ((inside && front) || (!inside && !front)) discard;

                float root = sqrt(max(0, discriminant));
                float enter = max(0, (-b - root)/a);
                float exit = (-b + root)/a;
                if (exit <= enter) discard;

                // Clip fog behind opaque asteroid geometry, station hulls, etc.
                float2 uv = GetNormalizedScreenSpaceUV(input.positionCS);
                float rawDepth = SampleSceneDepth(uv);
                #if UNITY_REVERSED_Z
                    if (rawDepth > 0.000001)
                #else
                    if (rawDepth < 0.999999)
                #endif
                {
                    float3 opaqueWS = ComputeWorldSpacePosition(uv, rawDepth, UNITY_MATRIX_I_VP);
                    float opaqueT = dot(opaqueWS - origin, ray);
                    exit = min(exit, opaqueT);
                }
                if (exit <= enter) discard;

                int steps = clamp((int)_GasSteps,6,24);
                float stepLength = (exit-enter)/steps;
                float transmittance = 1;
                float3 colorSum = 0;
                float3 seed = float3(input.visual.y*17.7, input.visual.y*51.3, input.visual.y*29.1);
                float3 drift = float3(.012,0,.008)*(_Time.y * _GasDrift);
                float3 sunDir = normalize(_GasLightDirection.xyz + float3(.0001,0,0));
                [loop]
                for (int i=0;i<24;i++)
                {
                    if (i>=steps) break;
                    float t = enter + (i+.5)*stepLength;
                    float3 worldP = origin + ray*t;
                    float3 q = (worldP-center)/radii;
                    float radial = length(q.xz);
                    float noisy = ValueNoise(float3(q.x*3.4,q.y*2.1,q.z*3.4)*_GasNoiseScale + seed + drift);
                    float edge = 1-smoothstep(.60,1.0,radial+(noisy-.5)*_GasContrast*.22);
                    float normalizedHeight = saturate(q.y*.5+.5);
                    float heightFade = 1-smoothstep(1-_FogTopSoftness,1,normalizedHeight);
                    float bottomFade = smoothstep(0,.15,normalizedHeight);
                    float density = edge*heightFade*bottomFade*lerp(.68,saturate(noisy*1.35),_GasContrast)*input.visual.x;
                    if (density<.0001) continue;
                    float extinction = density*_GasOpacity*(stepLength/max(radii.y*2,1))*2.8;
                    float alpha = 1-exp(-extinction);
                    float3 lighting = _GasLightColor.rgb * (.45 + .35*saturate(dot(sunDir,float3(0,1,0))));
                    [unroll]
                    for(int l=0;l<4;l++)
                    {
                        if(l>=_FogLightCount) break;
                        float range = max(1,_FogLightPosRange[l].w);
                        float distanceToLight = length(worldP-_FogLightPosRange[l].xyz);
                        float atten = saturate(1-distanceToLight/range);
                        atten *= atten;
                        lighting += _FogLightColors[l].rgb * atten;
                    }
                    float3 tint = lerp(_GasColorA.rgb,_GasColorB.rgb,saturate(noisy*.65+radial*.3));
                    colorSum += transmittance*alpha*tint*lighting*_GasBrightness;
                    transmittance *= (1-alpha);
                    if(transmittance<.02) break;
                }
                float opacity = saturate(1-transmittance);
                clip(opacity-.001);
                return half4(colorSum/max(opacity,.001),opacity);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
