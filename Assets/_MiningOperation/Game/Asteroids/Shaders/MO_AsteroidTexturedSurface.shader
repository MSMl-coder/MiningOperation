Shader "Mining Operation/Asteroid Textured Surface"
{
    Properties
    {
        [MainTexture] _BaseMap ("Albedo RGB (sRGB)", 2D) = "white" {}
        [Normal] _NormalMap ("Normal (linear)", 2D) = "bump" {}
        _MaskMap ("Mask R=AO G=Roughness B=Metallic A=Height", 2D) = "white" {}
        [MainColor] _BaseColor ("Tint", Color) = (1,1,1,1)
        _TextureFrequency ("Triplanar repetitions across radius", Range(0.5, 16)) = 2
        _TriplanarSharpness ("Projection sharpness", Range(1,12)) = 4
        _NormalStrength ("Normal strength", Range(0,2)) = 0.8
        _SmoothnessMultiplier ("Smoothness multiplier", Range(0,2)) = 1
        _MetallicMultiplier ("Metallic multiplier", Range(0,2)) = 1
        _AOInfluence ("AO influence", Range(0,1)) = 0.7
        _AmbientFloor ("Ambient floor", Range(0,0.4)) = 0.085
        _Exposure ("Surface exposure", Range(0.4,1.7)) = 1.0
        _ColorVariation ("Per-asteroid tonal variation", Range(0,0.3)) = 0.08
        _DetailFadeStart ("Normal detail fade pixel footprint", Range(0.002,0.1)) = 0.022
        [HideInInspector] _MO_ObjectRadius ("Object Radius", Float) = 1
        [HideInInspector] _MO_SurfaceSeed ("Seed", Float) = 0
        [HideInInspector] _MO_LODVisibility ("LOD Visibility", Range(0,1)) = 1
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }
        LOD 250
        Pass
        {
            Name "UniversalForward"
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceData.hlsl"

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            TEXTURE2D(_NormalMap); SAMPLER(sampler_NormalMap);
            TEXTURE2D(_MaskMap); SAMPLER(sampler_MaskMap);

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                float _TextureFrequency, _TriplanarSharpness, _NormalStrength;
                float _SmoothnessMultiplier, _MetallicMultiplier, _AOInfluence;
                float _ColorVariation, _DetailFadeStart, _AmbientFloor, _Exposure;
                float _MO_ObjectRadius, _MO_SurfaceSeed, _MO_LODVisibility;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 positionOS : TEXCOORD1;
                float3 normalOS : TEXCOORD2;
                float4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                VertexPositionInputs vp = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = vp.positionCS;
                output.positionWS = vp.positionWS;
                output.positionOS = input.positionOS.xyz;
                output.normalOS = input.normalOS;
                output.color = input.color;
                return output;
            }

            float3 SeedShift(float seed)
            {
                // These offsets are independent of LOD and use only the persisted asteroid seed.
                float v = seed * 731.173;
                return frac(float3(v * 1.37, v * 2.71, v * 3.17)) * 16.0;
            }
            half4 SampleTriplanarAlbedo(float3 p, float3 w)
            {
                half4 a = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, p.zy);
                half4 b = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, p.xz);
                half4 c = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, p.xy);
                return a * w.x + b * w.y + c * w.z;
            }
            half4 SampleTriplanarMask(float3 p, float3 w)
            {
                half4 a = SAMPLE_TEXTURE2D(_MaskMap, sampler_MaskMap, p.zy);
                half4 b = SAMPLE_TEXTURE2D(_MaskMap, sampler_MaskMap, p.xz);
                half4 c = SAMPLE_TEXTURE2D(_MaskMap, sampler_MaskMap, p.xy);
                return a * w.x + b * w.y + c * w.z;
            }
            float3 SampleTriplanarNormal(float3 p, float3 w, float3 n, float strength)
            {
                // Blend slopes in object space, not tangent frames; avoids UV seams and
                // preserves the same surface detail on near/far geometry.
                float3 nx = UnpackNormal(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, p.zy));
                float3 ny = UnpackNormal(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, p.xz));
                float3 nz = UnpackNormal(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, p.xy));
                float3 slope = w.x * float3(0, nx.y, nx.x) +
                               w.y * float3(ny.x, 0, ny.y) +
                               w.z * float3(nz.x, nz.y, 0);
                return normalize(n + slope * strength);
            }
            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float threshold = frac(dot(floor(input.positionCS.xy), float2(0.75487766, 0.56984029)));
                clip(_MO_LODVisibility - threshold);
                float3 baseNormalOS = normalize(input.normalOS);
                float3 weights = pow(abs(baseNormalOS), max(1, _TriplanarSharpness));
                weights /= max(1e-5, weights.x + weights.y + weights.z);
                float3 pos = input.positionOS / max(0.001, _MO_ObjectRadius);
                float3 p = pos * _TextureFrequency + SeedShift(_MO_SurfaceSeed);
                half3 albedo = SampleTriplanarAlbedo(p, weights).rgb * _BaseColor.rgb;
                half4 mask = SampleTriplanarMask(p, weights);
                float tone = 1.0 + (frac(_MO_SurfaceSeed * 17.131) - 0.5) * 2.0 * _ColorVariation;
                albedo *= tone * _Exposure;
                // Avoid oversharpened microtexture at distant LODs: mipmaps and
                // footprint-based normal fade use the very same texture on each LOD.
                float footprint = length(fwidth(pos * _TextureFrequency));
                float normalFade = saturate((_DetailFadeStart * 5.0 - footprint) /
                    max(0.0001, _DetailFadeStart * 4.0));
                float3 normalOS = SampleTriplanarNormal(p, weights, baseNormalOS,
                    _NormalStrength * normalFade);
                float3 normalWS = normalize(TransformObjectToWorldNormal(normalOS));
                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.normalWS = normalWS;
                inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                inputData.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                inputData.bakedGI = max(SampleSH(normalWS), half3(_AmbientFloor, _AmbientFloor, _AmbientFloor));
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                inputData.shadowMask = half4(1, 1, 1, 1);
                SurfaceData surfaceData = (SurfaceData)0;
                surfaceData.albedo = albedo;
                surfaceData.metallic = saturate(mask.b * _MetallicMultiplier);
                surfaceData.specular = half3(0.04, 0.04, 0.04);
                surfaceData.smoothness = saturate((1.0 - mask.g) * _SmoothnessMultiplier);
                surfaceData.normalTS = half3(0, 0, 1);
                surfaceData.occlusion = lerp(1.0, mask.r, saturate(_AOInfluence));
                surfaceData.alpha = 1.0;
                return UniversalFragmentPBR(inputData, surfaceData);
            }
            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ZWrite On
            ColorMask 0
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex DepthVert
            #pragma fragment DepthFrag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                float _TextureFrequency, _TriplanarSharpness, _NormalStrength;
                float _SmoothnessMultiplier, _MetallicMultiplier, _AOInfluence;
                float _ColorVariation, _DetailFadeStart, _AmbientFloor, _Exposure;
                float _MO_ObjectRadius, _MO_SurfaceSeed, _MO_LODVisibility;
            CBUFFER_END
            struct DepthAttributes { float4 positionOS : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct DepthVaryings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };
            DepthVaryings DepthVert(DepthAttributes input)
            {
                DepthVaryings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }
            half4 DepthFrag(DepthVaryings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                clip(_MO_LODVisibility - frac(dot(floor(input.positionCS.xy), float2(0.75487766, 0.56984029))));
                return half4(0,0,0,0);
            }
            ENDHLSL
        }
        // Streamed rock renderers disable shadow casting during LOD handoff.
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
    }
    Fallback "Universal Render Pipeline/Lit"
    CustomEditor "MiningOperation.Asteroids.Editor.MOTexturedAsteroidShaderGUI"
}
