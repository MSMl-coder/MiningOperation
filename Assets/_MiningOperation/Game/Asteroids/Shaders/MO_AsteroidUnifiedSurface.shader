Shader "Mining Operation/Asteroid Unified Surface"
{
    Properties
    {
        [Header(Surface colors)]
        _RockBase ("Rock base", Color) = (0.59, 0.54, 0.48, 1)
        _RockSecondary ("Rock variation", Color) = (0.42, 0.40, 0.38, 1)
        _IceBase ("Ice base", Color) = (0.73, 0.83, 0.89, 1)
        _IceSecondary ("Ice variation", Color) = (0.42, 0.63, 0.74, 1)
        _IceBlend ("Ice blend (0 rock / 1 ice)", Range(0,1)) = 0
        [Header(Macro noise in normalized local coordinates)]
        _MacroFrequency ("Macro frequency", Range(0.3, 12)) = 2.1
        _MacroContrast ("Macro contrast", Range(0, 3)) = 0.85
        _MacroStrength ("Macro color strength", Range(0, 1)) = 0.36
        _MacroOctaves ("Macro noise octaves", Range(1, 4)) = 2
        [Enum(Smooth,0,Ridged,1,Billow,2,Cellular,3)] _NoiseMode ("Macro noise type", Float) = 0
        _MacroWarp ("Macro domain warp", Range(0, 1)) = 0.12
        _MacroNormalStrength ("Macro normal strength", Range(0, 2)) = 0.22
        [Header(Crater layer)]
        [Enum(Off,0,SoftBowl,1,SharpRim,2,Eroded,3,IceDepression,4)] _CraterMode ("Crater profile", Float) = 2
        _CraterFrequency ("Crater scale / frequency", Range(0.45, 8)) = 1.35
        _CraterDensity ("Crater probability", Range(0, 1)) = 0.48
        _CraterRadius ("Crater radius", Range(0.35, 1.2)) = 1.0
        _CraterDepth ("Crater bowl depth", Range(0, 1)) = 0.38
        _CraterRimHeight ("Crater rim height", Range(0, 1)) = 0.26
        _CraterRimSharpness ("Crater rim sharpness", Range(1, 14)) = 6
        _CraterSecondary ("Secondary small craters", Range(0, 1)) = 0.10
        _CraterColorStrength ("Crater color strength", Range(0, 1)) = 0.28
        _CraterNormalStrength ("Crater normal strength", Range(0, 2)) = 0.75
        _CraterRoughnessStrength ("Crater roughness influence", Range(0, 1)) = 0.25
        [Header(Fine noise)]
        _DetailFrequency ("Detail frequency", Range(2, 100)) = 19
        _DetailStrength ("Detail color strength", Range(0, 1)) = 0.12
        _DetailNormalStrength ("Micro-relief strength", Range(0, 1)) = 0.10
        [Enum(Smooth,0,Ridged,1,Billow,2,Cellular,3)] _MicroNoiseType ("Micro noise type", Float) = 0
        _MicroRoughnessStrength ("Micro roughness variation", Range(0, 1)) = 0.14
        _StrataFrequency ("Layer / strata frequency", Range(0.2, 25)) = 6
        _StrataStrength ("Layer / strata strength", Range(0, 1)) = 0.0
        _IceCrackStrength ("Ice crack contrast", Range(0, 1)) = 0.10
        [Header(Lighting and geology)]
        _CavityColor ("Cavity / crater color", Color) = (0.29, 0.30, 0.33, 1)
        _CavityStrength ("Cavity shading", Range(0, 1)) = 0.20
        _CavityContrast ("Cavity contrast", Range(0.25, 3)) = 1.15
        _CavityRoughnessStrength ("Cavity roughness influence", Range(0, 1)) = 0.20
        _Smoothness ("Smoothness", Range(0,1)) = 0.13
        _Metallic ("Metallic", Range(0,1)) = 0.0
        _AmbientFill ("Ambient shadow fill", Range(0, 1)) = 0.33
        _LightWrap ("Light terminator softness", Range(0, 1)) = 0.48
        _DarkSideFill ("Dark-side brightness", Range(0, 1)) = 0.28
        _RimTint ("Rim light tint", Color) = (0.65, 0.83, 0.95, 1)
        _RimStrength ("Rim strength", Range(0, 1)) = 0.12
        _RimPower ("Rim width", Range(1, 10)) = 3.5
        [HideInInspector] _MO_SurfaceSeed ("Per-asteroid seed", Float) = 0
        [HideInInspector] _MO_ObjectRadius ("Geometry radius", Float) = 1
        [HideInInspector] _MO_LODVisibility ("LOD visibility", Range(0,1)) = 1
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        LOD 300
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fragment _ _LOD_FADE_CROSSFADE
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/LODCrossFade.hlsl"

            CBUFFER_START(UnityPerMaterial)
            float4 _RockBase, _RockSecondary, _IceBase, _IceSecondary, _CavityColor, _RimTint;
            float _IceBlend, _MacroFrequency, _MacroContrast, _MacroStrength, _MacroWarp, _MacroOctaves, _NoiseMode, _MacroNormalStrength;
            float _DetailFrequency, _DetailStrength, _DetailNormalStrength, _StrataFrequency, _StrataStrength, _IceCrackStrength;
            float _MicroNoiseType, _MicroRoughnessStrength;
            float _CraterMode, _CraterFrequency, _CraterDensity, _CraterRadius, _CraterDepth, _CraterRimHeight;
            float _CraterRimSharpness, _CraterSecondary, _CraterColorStrength, _CraterNormalStrength, _CraterRoughnessStrength;
            float _CavityStrength, _Smoothness, _Metallic, _AmbientFill, _LightWrap, _DarkSideFill, _RimStrength, _RimPower;
            float _CavityContrast, _CavityRoughnessStrength;
            float _MO_ObjectRadius, _MO_LODVisibility, _MO_SurfaceSeed;
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
                float3 normalWS : TEXCOORD1;
                float3 positionOS : TEXCOORD2;
                float3 normalOS : TEXCOORD3;
                half4 color : COLOR;
                float4 shadowCoord : TEXCOORD4;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            float Hash31(float3 p)
            {
                p = frac(p * 0.1031);
                p += dot(p, p.yzx + 33.33);
                return frac((p.x + p.y) * p.z);
            }
            float ValueNoise(float3 p)
            {
                float3 i = floor(p), f = frac(p);
                f = f*f*(3.0 - 2.0*f);
                return lerp(
                    lerp(lerp(Hash31(i), Hash31(i+float3(1,0,0)), f.x),
                         lerp(Hash31(i+float3(0,1,0)), Hash31(i+float3(1,1,0)), f.x), f.y),
                    lerp(lerp(Hash31(i+float3(0,0,1)), Hash31(i+float3(1,0,1)), f.x),
                         lerp(Hash31(i+float3(0,1,1)), Hash31(i+float3(1,1,1)), f.x), f.y), f.z);
            }

            // Returns a continuous 3D cellular F1 distance without texture coordinates.
            // The nearest 8 hashed cell centers are sufficient for artistic surface noise.
            float CellularF1(float3 p)
            {
                float3 cell = floor(p);
                float3 f = frac(p);
                float3 side = step(0.5, f) * 2.0 - 1.0;
                float best = 4.0;
                [unroll] for (int x = 0; x < 2; x++)
                [unroll] for (int y = 0; y < 2; y++)
                [unroll] for (int z = 0; z < 2; z++)
                {
                    float3 g = cell + float3(x * side.x, y * side.y, z * side.z);
                    float3 center = g + float3(
                        Hash31(g + 3.13), Hash31(g + 17.29), Hash31(g + 39.71)) * 0.65 + 0.175;
                    best = min(best, length(p - center));
                }
                return saturate(best);
            }

            float NoiseByType(float3 p, float mode)
            {
                if (mode > 2.5) return 1.0 - CellularF1(p);
                float n = ValueNoise(p);
                if (mode > 1.5) return abs(n * 2.0 - 1.0);       // billow
                if (mode > 0.5) return 1.0 - abs(n * 2.0 - 1.0); // ridge
                return n;
            }

            // Bowl and rim are independent output masks. Hash-based crater occupancy
            // prevents a repeated checkerboard. All coordinates are local/object-based.
            float2 CraterField(float3 p, float density, float radiusScale, float mode)
            {
                float3 baseCell = floor(p);
                float3 side = step(0.5, frac(p)) * 2.0 - 1.0;
                float strongestBowl = 0.0;
                float strongestRim = 0.0;
                [unroll] for (int x = 0; x < 2; x++)
                [unroll] for (int y = 0; y < 2; y++)
                [unroll] for (int z = 0; z < 2; z++)
                {
                    float3 g = baseCell + float3(x * side.x, y * side.y, z * side.z);
                    float occupancy = Hash31(g + 41.75);
                    if (occupancy > density) continue;
                    float3 center = g + float3(
                        Hash31(g + 13.53), Hash31(g + 29.81), Hash31(g + 61.17)) * 0.64 + 0.18;
                    float radius = lerp(0.29, 0.55, Hash31(g + 97.21)) * radiusScale;
                    float3 delta = p - center;
                    if (mode > 3.5) delta.y *= 0.83; // broader ice depressions
                    float d = length(delta) / max(radius, 0.05);
                    if (d > 1.30) continue;
                    float exponent = mode > 1.5 && mode < 2.5 ? 1.0 : 1.85;
                    float bowl = pow(saturate(1.0 - d * d), exponent);
                    float rim = exp(-pow((d - 0.98) * _CraterRimSharpness, 2.0));
                    if (mode > 2.5 && mode < 3.5)
                    {
                        float erosion = ValueNoise(p * 7.4 + g * 1.7);
                        bowl *= lerp(.80, 1.20, erosion);
                        rim *= lerp(.42, 1.05, erosion);
                    }
                    if (mode > 3.5) rim *= 0.26;
                    strongestBowl = max(strongestBowl, bowl);
                    strongestRim = max(strongestRim, rim);
                }
                return float2(strongestBowl, strongestRim);
            }

            Varyings Vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                VertexPositionInputs pos = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = pos.positionCS;
                o.positionWS = pos.positionWS;
                o.shadowCoord = GetShadowCoord(pos);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.normalOS = v.normalOS;
                o.positionOS = v.positionOS.xyz;
                o.color = v.color;
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                #if defined(_LOD_FADE_CROSSFADE)
                    LODFadeCrossFade(i.positionCS);
                #endif
                // Screen-space stable dithering for the near/far handoff.
                float2 ip = floor(i.positionCS.xy);
                float threshold = frac(dot(ip, float2(0.75487766, 0.56984029)));
                clip(_MO_LODVisibility - threshold);
                float radius = max(_MO_ObjectRadius, 0.0001);
                float3 objectPosition = i.positionOS / radius;
                float3 seedOffset = _MO_SurfaceSeed * float3(11.79, 23.43, 47.27);
                float3 normWS = normalize(i.normalWS);

                // 1. MacroMask: large scale color + large-scale relief, no repeated UVs.
                float3 macroCoord = objectPosition * _MacroFrequency + seedOffset;
                float3 warpVector = float3(ValueNoise(macroCoord * .73 + 19.31),
                    ValueNoise(macroCoord * .71 + 43.73), ValueNoise(macroCoord * .76 + 77.13)) - .5;
                macroCoord += warpVector * _MacroWarp;
                float macroAccum = 0.0, weightSum = 0.0, weight = 1.0;
                float3 octaveP = macroCoord + 7.5;
                [unroll] for (int octave = 0; octave < 4; ++octave)
                {
                    if (octave < (int)round(_MacroOctaves))
                    {
                        macroAccum += NoiseByType(octaveP, _NoiseMode) * weight;
                        weightSum += weight;
                    }
                    octaveP = octaveP * 2.17 + 11.31;
                    weight *= .47;
                }
                float macroMask = saturate((macroAccum / max(.001, weightSum) - .5) * _MacroContrast + .5);

                // 2. CraterMask: independent cellular bowls/rims, plus optional smaller impacts.
                // Derivative-based fade keeps distant bodies from shimmering/flickering.
                float surfacePixel = max(length(ddx(objectPosition)), length(ddy(objectPosition)));
                float craterFade = saturate(1.0 - surfacePixel * _CraterFrequency * 1.45);
                float2 craters = float2(0, 0);
                if (_CraterMode > .5 && _CraterDensity > .001 && craterFade > .001)
                {
                    float3 craterP = objectPosition * _CraterFrequency + seedOffset * .65 + 13.79;
                    craters = CraterField(craterP, _CraterDensity, _CraterRadius, _CraterMode);
                    if (_CraterSecondary > .001)
                    {
                        float2 small = CraterField(craterP * 2.65 + 27.41,
                            saturate(_CraterDensity * _CraterSecondary * .65),
                            max(.30, _CraterRadius * .65), _CraterMode);
                        craters = max(craters, small * _CraterSecondary * .55);
                    }
                    craters *= craterFade;
                }
                float craterBowlMask = saturate(craters.x);
                float craterRimMask = saturate(craters.y);

                // 3. MicroMask: fine grain; attenuated according to screen footprint.
                float detailPixel = surfacePixel * _DetailFrequency;
                float detailFade = saturate(1.0 - detailPixel * 1.6);
                float microMask = NoiseByType(objectPosition * _DetailFrequency + seedOffset + 35.7,
                    _MicroNoiseType);
                float microSigned = (microMask - .5) * 2.0 * detailFade;

                // 4. CavityMask: geometric curvature and crater basins are composed,
                // but cavity shading never *creates* crater shapes on its own.
                float3 radial = normalize(i.positionOS);
                float geometricCavity = saturate(1.0 - dot(normalize(i.normalOS), radial));
                float cavityMask = pow(saturate(geometricCavity * .72 + craterBowlMask * .8),
                    max(.25, _CavityContrast));

                float3 rock = lerp(_RockBase.rgb, _RockSecondary.rgb, macroMask * _MacroStrength);
                float3 ice = lerp(_IceBase.rgb, _IceSecondary.rgb, macroMask * _MacroStrength);
                float3 albedo = lerp(rock, ice, saturate(_IceBlend));
                albedo *= 1.0 + microSigned * _DetailStrength;
                float strata = sin(objectPosition.y * _StrataFrequency + dot(warpVector, float3(1.333, 1.333, 1.333)));
                albedo *= 1.0 - saturate(strata) * _StrataStrength * .20;
                if (_IceBlend > .001 && _IceCrackStrength > .001)
                {
                    float crack = abs(ValueNoise(objectPosition * 6.7 + seedOffset + 5.83) - .5);
                    float crackLine = 1.0 - smoothstep(.014, .065, crack);
                    albedo *= 1.0 - crackLine * _IceCrackStrength * _IceBlend * .26;
                }
                albedo *= lerp(1.0, saturate(dot(i.color.rgb, float3(.3333, .3333, .3333)) * 1.15), .2);
                float craterTint = saturate(craterBowlMask * _CraterColorStrength +
                    cavityMask * _CavityStrength);
                albedo = lerp(albedo, _CavityColor.rgb, craterTint);
                albedo = max(0.01, albedo + _CraterRimHeight * craterRimMask * .05);

                // Unified authored height. Derivatives perturb normals without requiring
                // more triangles or UV textures. Craters are not actual mesh displacement.
                float height = (macroMask - .5) * _MacroNormalStrength * .065
                    + (-craterBowlMask * _CraterDepth + craterRimMask * _CraterRimHeight)
                        * _CraterNormalStrength * .18
                    + microSigned * _DetailNormalStrength * .018;
                float3 dpdx = ddx(i.positionWS);
                float3 dpdy = ddy(i.positionWS);
                float3 r1 = cross(dpdy, normWS);
                float3 r2 = cross(normWS, dpdx);
                float det = dot(dpdx, r1);
                float3 bump = (ddx(height) * r1 + ddy(height) * r2) * radius;
                normWS = normalize(normWS - bump / max(abs(det), .00001) * (det < 0 ? -1.0 : 1.0));

                float localSmoothness = saturate(_Smoothness
                    - abs(microSigned) * _MicroRoughnessStrength * .18
                    - craterBowlMask * _CraterRoughnessStrength * .25
                    - cavityMask * _CavityRoughnessStrength * .20);
                Light mainLight = GetMainLight(i.shadowCoord);
                // Wrapped diffuse extends the soft terminator onto the unlit side.
                // This is a controllable visual choice for legible space rocks, not a
                // replacement for the main light or for actual cast shadows.
                float geometricNdl = dot(normWS, mainLight.direction);
                float wrap = saturate(_LightWrap);
                float ndl = saturate((geometricNdl + wrap) / (1.0 + wrap));
                float3 ambient = max(SampleSH(normWS), float3(0.13, 0.13, 0.13) * _AmbientFill);
                ambient += mainLight.color.rgb * saturate(_DarkSideFill) * (1.0 - ndl) * 0.24;
                float3 diffuse = mainLight.color.rgb * ndl * mainLight.distanceAttenuation * mainLight.shadowAttenuation;
                #ifdef _ADDITIONAL_LIGHTS
                    uint lightCount = GetAdditionalLightsCount();
                    for (uint idx = 0u; idx < lightCount; ++idx)
                    {
                        Light l = GetAdditionalLight(idx, i.positionWS);
                        diffuse += l.color.rgb * saturate(dot(normWS, l.direction)) * l.distanceAttenuation * l.shadowAttenuation;
                    }
                #endif
                float3 viewDir = normalize(GetWorldSpaceViewDir(i.positionWS));
                float3 halfVec = normalize(viewDir + mainLight.direction);
                float specPow = lerp(12.0, 96.0, localSmoothness);
                float3 spec = mainLight.color.rgb * pow(saturate(dot(normWS, halfVec)), specPow) *
                              lerp(0.028, 0.15, localSmoothness) * (0.2 + 0.8*_Metallic) * ndl;
                float fresnel = pow(1.0 - saturate(dot(normWS, viewDir)), _RimPower);
                float3 rim = _RimTint.rgb * fresnel * _RimStrength;
                return half4(max(0, albedo * (ambient + diffuse) + spec + rim), 1.0);
            }
            ENDHLSL
        }
        // Depth prepass must respect exactly the same dither threshold as ForwardLit.
        // Using the stock Lit DepthOnly pass made fully faded near meshes still write
        // opaque depth and occlude their far representation in depth-primed URP.
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask 0
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex DepthVert
            #pragma fragment DepthFrag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            // Same UnityPerMaterial layout as ForwardLit (SRP Batcher consistency).
            CBUFFER_START(UnityPerMaterial)
            float4 _RockBase, _RockSecondary, _IceBase, _IceSecondary, _CavityColor, _RimTint;
            float _IceBlend, _MacroFrequency, _MacroContrast, _MacroStrength, _MacroWarp, _MacroOctaves, _NoiseMode, _MacroNormalStrength;
            float _DetailFrequency, _DetailStrength, _DetailNormalStrength, _StrataFrequency, _StrataStrength, _IceCrackStrength;
            float _MicroNoiseType, _MicroRoughnessStrength;
            float _CraterMode, _CraterFrequency, _CraterDensity, _CraterRadius, _CraterDepth, _CraterRimHeight;
            float _CraterRimSharpness, _CraterSecondary, _CraterColorStrength, _CraterNormalStrength, _CraterRoughnessStrength;
            float _CavityStrength, _Smoothness, _Metallic, _AmbientFill, _LightWrap, _DarkSideFill, _RimStrength, _RimPower;
            float _CavityContrast, _CavityRoughnessStrength;
            float _MO_ObjectRadius, _MO_LODVisibility, _MO_SurfaceSeed;
            CBUFFER_END

            struct DepthAttributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
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
                float2 pixel = floor(input.positionCS.xy);
                float threshold = frac(dot(pixel, float2(0.75487766, 0.56984029)));
                clip(_MO_LODVisibility - threshold);
                return half4(0, 0, 0, 0);
            }
            ENDHLSL
        }
        // Standalone authored asteroid instances may still cast shadows.
        // Streamed asteroids disable shadow casting on the renderer when using
        // near/far dithering: otherwise stock shadow caster ignores LOD visibility.
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
    }
    FallBack "Universal Render Pipeline/Lit"
    CustomEditor "MiningOperation.Asteroids.Editor.MOLayeredAsteroidShaderGUI"
}
