Shader "Mining Operation/Space Gradient Sky"
{
    Properties
    {
        _SkyTop ("Upper sky", Color) = (0.02,0.07,0.14,1)
        _SkyMiddle ("Horizon", Color) = (0.24,0.39,0.44,1)
        _SkyBottom ("Lower sky", Color) = (0.055,0.14,0.24,1)
        _NebulaIntensity ("Nebula haze", Range(0,1)) = 0.24
        _StarIntensity ("Stars", Range(0,1)) = 0.12
    }
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Off ZWrite Off
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            float4 _SkyTop, _SkyMiddle, _SkyBottom;
            float _NebulaIntensity, _StarIntensity;
            struct appdata { float4 vertex : POSITION; };
            struct v2f { float4 pos : SV_POSITION; float3 dir : TEXCOORD0; };
            v2f vert(appdata v) { v2f o; o.pos = TransformObjectToHClip(v.vertex.xyz); o.dir = v.vertex.xyz; return o; }
            float hash31(float3 p) { p = frac(p * 0.1031); p += dot(p,p.yzx+33.33); return frac((p.x+p.y)*p.z); }
            float noise3(float3 p)
            {
                float3 i = floor(p), f = frac(p);
                f = f*f*(3-2*f);
                float x00 = lerp(hash31(i),hash31(i+float3(1,0,0)),f.x);
                float x10 = lerp(hash31(i+float3(0,1,0)),hash31(i+float3(1,1,0)),f.x);
                float x01 = lerp(hash31(i+float3(0,0,1)),hash31(i+float3(1,0,1)),f.x);
                float x11 = lerp(hash31(i+float3(0,1,1)),hash31(i+float3(1,1,1)),f.x);
                return lerp(lerp(x00,x10,f.y),lerp(x01,x11,f.y),f.z);
            }
            half4 frag(v2f i) : SV_Target
            {
                float3 d = normalize(i.dir);
                float h = smoothstep(-0.65,0.80,d.y);
                float3 sky = lerp(_SkyBottom.rgb,_SkyMiddle.rgb,smoothstep(-0.85,0.15,d.y));
                sky = lerp(sky,_SkyTop.rgb,smoothstep(-0.05,0.86,d.y));
                float cloud = noise3(d*5.5+13.0)*0.62 + noise3(d*13.0+2.3)*0.38;
                float halo = pow(saturate(1-abs(d.y+0.15)),2.3);
                sky += _NebulaIntensity*halo*saturate(cloud-0.36)*float3(0.19,0.22,0.20);
                float3 starCell = floor(d*370.0);
                float star = step(0.9988,hash31(starCell)) * pow(saturate(hash31(starCell+7.1)),3);
                sky += star * _StarIntensity * float3(0.85,0.91,1.0);
                return half4(sky,1);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
