Shader "MiningOperation/GroundFogFastURP"
{
    Properties
    {
        _GasColorA ("Dense Color", Color) = (0.16, 0.25, 0.28, 1)
        _GasColorB ("Edge Color", Color) = (0.24, 0.33, 0.36, 1)
        _GasOpacity ("Opacity", Range(0,1)) = 0.2
        _GasBrightness ("Brightness", Range(0.1,2)) = 1
        _GasNoiseScale ("Detail Scale", Range(0.25,3)) = 1
        _GasDrift ("Drift", Range(0,1)) = 0.1
        _GasContrast ("Contrast", Range(0,1)) = 0.6
        _FogBreakup ("Breakup", Range(0,1)) = 0.8
        _FogCutouts ("Cutouts", Range(0,1)) = 0.55
        _FogNoiseTex ("Fog Detail Noise (repeat)", 2D) = "white" {}
        _GasLightDirection ("Directional Light", Vector) = (-0.42, 0.71, -0.56, 0)
        _GasLightColor ("Light Color", Color) = (0.5, 0.6, 0.65, 1)
    }

    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent+10" }
        Pass
        {
            Name "GroundFogFastSurface"
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Back

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_FogNoiseTex);
            SAMPLER(sampler_FogNoiseTex);

            CBUFFER_START(UnityPerMaterial)
                half4 _GasColorA;
                half4 _GasColorB;
                float _GasOpacity;
                float _GasBrightness;
                float _GasNoiseScale;
                float _GasDrift;
                float _GasContrast;
                float _FogBreakup;
                float _FogCutouts;
                float4 _GasLightDirection;
                half4 _GasLightColor;
            CBUFFER_END

            float4 _FogLightPosRange[4];
            float4 _FogLightColors[4];
            int _FogLightCount;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float4 color : COLOR;
                float2 uv : TEXCOORD1;
            };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.uv = input.uv;
                output.color = input.color;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                // One texture lookup, one transparent surface. No ray steps and
                // no nested 3D ellipsoids that repeatedly shade the same pixels.
                float2 noiseUV = input.uv * (4.0 * _GasNoiseScale) +
                    _Time.y * _GasDrift * float2(.003, -.002);
                half noise = SAMPLE_TEXTURE2D(_FogNoiseTex, sampler_FogNoiseTex, noiseUV).r;
                half detail = lerp(1.0h, smoothstep(
                    lerp(.1h, .45h, (half)_FogCutouts),
                    lerp(.45h, .75h, (half)_FogCutouts), noise), (half)_FogBreakup);
                half opacity = saturate(input.color.a * (half)_GasOpacity * 2.5h * detail);
                clip(opacity - 0.006h);

                half3 lighting = _GasLightColor.rgb * .8h;
                [unroll] for (int i = 0; i < 4; i++)
                {
                    if (i >= _FogLightCount) break;
                    float range = max(1.0, _FogLightPosRange[i].w);
                    float3 delta = input.positionWS - _FogLightPosRange[i].xyz;
                    half attenuation = saturate(1.0 - length(delta) / range);
                    lighting += _FogLightColors[i].rgb * attenuation * attenuation;
                }

                half tint = saturate(input.color.r * .65h + noise * .35h);
                half3 color = lerp(_GasColorA.rgb, _GasColorB.rgb, tint);
                color *= lighting * (half)_GasBrightness;
                return half4(color, opacity);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
