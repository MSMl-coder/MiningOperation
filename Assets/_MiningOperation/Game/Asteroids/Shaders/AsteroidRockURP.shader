Shader "Mining Operation/Asteroid Rock URP"
{
    Properties
    {
        _BaseRockColor ("Main rock", Color) = (0.40, 0.38, 0.36, 1)
        _DarkRockColor ("Cracks and lowlands", Color) = (0.12, 0.12, 0.13, 1)
        _BrightRockColor ("Exposed rock", Color) = (0.56, 0.52, 0.45, 1)
        _NoiseScale ("Rock grain frequency", Range(0.002, 0.5)) = 0.045
        _DetailStrength ("Color variation", Range(0, 1)) = 0.68
        _ReliefStrength ("Micro normal relief", Range(0, 1)) = 0.18
        _Roughness ("Roughness", Range(0.1, 1)) = 0.86
        _AmbientStrength ("Ambient strength", Range(0, 3)) = 1.0
        _TriplanarRock ("Rock texture (optional)", 2D) = "white" {}
        _TriplanarScale ("Rock texture scale", Range(0.001,0.5)) = 0.028
        _TriplanarBlend ("Rock texture blend", Range(0,1)) = 0.4
        _CavityStrength ("Dark cavity shading", Range(0,1)) = 0.42
        _FineBumpStrength ("Rock texture relief", Range(0,1)) = 0.36
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Cull Back
        ZWrite On
        Pass
        {
            Name "AsteroidRockForward"
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.5
            #pragma multi_compile_instancing
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            TEXTURE2D(_TriplanarRock);
            SAMPLER(sampler_TriplanarRock);
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseRockColor;
                half4 _DarkRockColor;
                half4 _BrightRockColor;
                float _NoiseScale;
                float _DetailStrength;
                float _ReliefStrength;
                float _Roughness;
                float _AmbientStrength;
                float _TriplanarScale;
                float _TriplanarBlend;
                float _CavityStrength;
                float _FineBumpStrength;
            CBUFFER_END
            float4 _MO_HazeTint;
            float _MO_HazeStart, _MO_HazeEnd, _MO_HazeStrength;
            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 positionOS : TEXCOORD2;
                half3 normalWS : TEXCOORD1;
                half3 normalOS : TEXCOORD3;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            float Hash31(float3 x)
            {
                x = frac(x * 0.1031);
                x += dot(x, x.yzx + 33.33);
                return frac((x.x + x.y) * x.z);
            }
            float Noise3(float3 p)
            {
                float3 i = floor(p);
                float3 f = frac(p);
                f = f*f*(3.0-2.0*f);
                float a = lerp(Hash31(i+float3(0,0,0)), Hash31(i+float3(1,0,0)), f.x);
                float b = lerp(Hash31(i+float3(0,1,0)), Hash31(i+float3(1,1,0)), f.x);
                float c = lerp(Hash31(i+float3(0,0,1)), Hash31(i+float3(1,0,1)), f.x);
                float d = lerp(Hash31(i+float3(0,1,1)), Hash31(i+float3(1,1,1)), f.x);
                return lerp(lerp(a,b,f.y), lerp(c,d,f.y), f.z);
            }
            Varyings Vert(Attributes IN)
            {
                UNITY_SETUP_INSTANCE_ID(IN);
                Varyings OUT;
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);
                OUT.positionOS = IN.positionOS.xyz;
                OUT.positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.positionCS = TransformWorldToHClip(OUT.positionWS);
                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                OUT.normalOS = IN.normalOS;
                OUT.color = IN.color;
                return OUT;
            }
            half4 Frag(Varyings IN) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(IN);
                #if defined(LOD_FADE_CROSSFADE)
                    // Complementary stochastic coverage for source/target LOD meshes.
                    float f = unity_LODFade.x;
                    float coverage = saturate(f >= 0.0 ? f : (1.0 + f));
                    float2 pix = floor(IN.positionCS.xy);
                    float dither = frac(52.9829189 * frac(0.06711056 * pix.x + 0.00583715 * pix.y));
                    clip(coverage - dither);
                #endif
                float3 n = normalize(IN.normalWS);
                float3 q = IN.positionWS * _NoiseScale;
                float coarse = Noise3(q);
                float mid = Noise3(q * 4.1 + 9.8);
                float fine = Noise3(q * 13.7 - 4.2);
                float pattern = saturate(coarse * 0.62 + mid * 0.28 + fine * 0.10);
                half3 rock = lerp(_DarkRockColor.rgb, _BaseRockColor.rgb,
                    smoothstep(0.14, 0.70, pattern));
                rock = lerp(rock, _BrightRockColor.rgb, saturate((pattern - 0.66) * 2.2));
                rock = lerp(_BaseRockColor.rgb, rock, _DetailStrength);
                rock *= lerp(half3(1,1,1), IN.color.rgb, 0.9);
                // Local/object-space triplanar sampling does not swim when an asteroid moves.
                float3 blend = pow(abs(normalize(IN.normalOS)), 4.0);
                blend /= max(0.0001, blend.x + blend.y + blend.z);
                float3 localUV = IN.positionOS * _TriplanarScale;
                half3 textureRock =
                    SAMPLE_TEXTURE2D(_TriplanarRock, sampler_TriplanarRock, localUV.yz).rgb * blend.x +
                    SAMPLE_TEXTURE2D(_TriplanarRock, sampler_TriplanarRock, localUV.xz).rgb * blend.y +
                    SAMPLE_TEXTURE2D(_TriplanarRock, sampler_TriplanarRock, localUV.xy).rgb * blend.z;
                rock *= lerp(half3(1,1,1), textureRock, _TriplanarBlend);
                float mineralShade = dot(textureRock, half3(0.299,0.587,0.114));
                rock *= 1.0 - _CavityStrength * saturate((0.42-mineralShade)*1.8);
                // Small screen-space relief, based on gradients of continuous 3D noise.
                float3 dx = ddx(IN.positionWS);
                float3 dy = ddy(IN.positionWS);
                float3 bump = cross(dy, n) * ddx(mid) + cross(n, dx) * ddy(mid);
                n = normalize(n + bump * (_ReliefStrength * 0.3));
                float3 microBump = cross(dy,n) * ddx(mineralShade) + cross(n,dx) * ddy(mineralShade);
                n = normalize(n + microBump * _FineBumpStrength * 0.14);
                half3 lighting = SampleSH(n) * _AmbientStrength;
                half3 viewDir = SafeNormalize(GetWorldSpaceViewDir(IN.positionWS));
                float4 shadowCoord = TransformWorldToShadowCoord(IN.positionWS);
                Light sun = GetMainLight(shadowCoord);
                half ndl = saturate(dot(n, sun.direction));
                lighting += sun.color * (ndl * sun.distanceAttenuation * sun.shadowAttenuation);
                half3 h = SafeNormalize(sun.direction + viewDir);
                half spec = pow(saturate(dot(n, h)), lerp(100.0, 8.0, _Roughness)) *
                    (1.0 - _Roughness) * sun.shadowAttenuation;
                half3 color = rock * lighting + spec * sun.color * 0.14;
                #ifdef _ADDITIONAL_LIGHTS
                    uint lightCount = GetAdditionalLightsCount();
                    for (uint i = 0; i < lightCount; ++i)
                    {
                        Light L = GetAdditionalLight(i, IN.positionWS);
                        color += rock * L.color * saturate(dot(n, L.direction)) *
                            L.distanceAttenuation * L.shadowAttenuation;
                    }
                #endif
                float depthMeters = distance(_WorldSpaceCameraPos.xyz, IN.positionWS);
                float haze = saturate((depthMeters - _MO_HazeStart) / max(1.0, _MO_HazeEnd - _MO_HazeStart));
                color = lerp(color, _MO_HazeTint.rgb, haze * _MO_HazeStrength);
                color = MixFog(color, ComputeFogFactor(IN.positionCS.z));
                return half4(color, 1);
            }
            ENDHLSL
        }
        Pass
        {
            Name "AsteroidShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ColorMask 0
            ZWrite On
            HLSLPROGRAM
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #pragma target 3.5
            #pragma multi_compile_instancing
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
            float3 _LightDirection;
            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Varyings { float4 positionCS : SV_POSITION; };
            Varyings ShadowVert(Attributes IN)
            {
                UNITY_SETUP_INSTANCE_ID(IN);
                Varyings OUT;
                float3 posWS = TransformObjectToWorld(IN.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(IN.normalOS);
                OUT.positionCS = TransformWorldToHClip(ApplyShadowBias(posWS, normalWS, _LightDirection));
                #if UNITY_REVERSED_Z
                    OUT.positionCS.z = min(OUT.positionCS.z, UNITY_NEAR_CLIP_VALUE * OUT.positionCS.w);
                #else
                    OUT.positionCS.z = max(OUT.positionCS.z, UNITY_NEAR_CLIP_VALUE * OUT.positionCS.w);
                #endif
                return OUT;
            }
            half4 ShadowFrag(Varyings IN) : SV_Target
            {
                #if defined(LOD_FADE_CROSSFADE)
                    float f = unity_LODFade.x;
                    float coverage = saturate(f >= 0.0 ? f : (1.0 + f));
                    float2 pix = floor(IN.positionCS.xy);
                    float dither = frac(52.9829189 * frac(0.06711056 * pix.x + 0.00583715 * pix.y));
                    clip(coverage - dither);
                #endif
                return 0;
            }
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
