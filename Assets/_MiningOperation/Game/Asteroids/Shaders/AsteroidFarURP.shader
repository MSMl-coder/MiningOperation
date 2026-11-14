Shader "Mining Operation/Asteroid Far URP"
{
    Properties
    {
        _BaseColor ("Rock Color", Color) = (0.36, 0.35, 0.35, 1)
        _AmbientStrength ("Ambient", Range(0, 3)) = 1.25
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            Name "AsteroidFar"
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.5
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half _AmbientStrength;
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
                half3 normalWS : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                half4 color : COLOR;
            };
            Varyings Vert(Attributes IN)
            {
                UNITY_SETUP_INSTANCE_ID(IN);
                Varyings OUT;
                float3 positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.positionCS = TransformWorldToHClip(positionWS);
                OUT.positionWS = positionWS;
                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                OUT.color = IN.color;
                return OUT;
            }
            half4 Frag(Varyings IN) : SV_Target
            {
                half3 normalWS = normalize(IN.normalWS);
                Light sun = GetMainLight();
                // A minimum neutral ambient prevents dark silhouettes when scene
                // reflection probes or baked spherical harmonics are not configured.
                half3 ambientLight = max(SampleSH(normalWS), half3(0.19, 0.21, 0.23));
                half3 color = _BaseColor.rgb * IN.color.rgb *
                    (ambientLight * _AmbientStrength +
                    sun.color * saturate(dot(normalWS, sun.direction)));
                float depthMeters = distance(_WorldSpaceCameraPos.xyz, IN.positionWS);
                float haze = saturate((depthMeters - _MO_HazeStart) / max(1.0, _MO_HazeEnd - _MO_HazeStart));
                color = lerp(color, _MO_HazeTint.rgb, haze * _MO_HazeStrength);
                return half4(MixFog(color, ComputeFogFactor(IN.positionCS.z)), 1);
            }
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
