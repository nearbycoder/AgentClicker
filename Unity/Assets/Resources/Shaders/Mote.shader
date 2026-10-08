// Dust drifting in the sunbeam, all on the GPU: the mesh is a batch of quads whose positions (0-1 in the beam's box)
// and speeds are baked into the vertices; the vertex shader moves each speck through the box, wraps it round, faces
// it to the camera and fades it near the box's edges so the wrap can't be seen. The object's transform is the box.
Shader "AgentClicker/Mote"
{
    Properties
    {
        _Color ("Colour", Color) = (1, 0.93, 0.8, 1)
        _Intensity ("Intensity", Float) = 1
        _Size ("Size (m)", Float) = 0.012
        _Drift ("Drift speed", Float) = 1
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent+1" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Pass
        {
            Name "Mote"
            Tags { "LightMode" = "UniversalForward" }
            Blend One One
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _Intensity;
                float _Size, _Drift;
            CBUFFER_END

            // positionOS: the speck's starting point in the box (0-1); uv: the quad corner; uv2: drift (xyz) and a phase (w)
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; float4 seed : TEXCOORD1; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; half alpha : TEXCOORD1; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                float t = _Time.y * _Drift;
                float3 p = frac(v.positionOS.xyz + v.seed.xyz * t) - 0.5;
                float3 edge = 0.5 - abs(p);
                half fade = saturate(min(edge.x, min(edge.y, edge.z)) * 6.0);
                float3 world = TransformObjectToWorld(p);
                float3 right = UNITY_MATRIX_V[0].xyz, up = UNITY_MATRIX_V[1].xyz;
                float size = _Size * (0.55 + v.seed.w * 0.9);
                world += (right * (v.uv.x - 0.5) + up * (v.uv.y - 0.5)) * size;
                o.positionCS = TransformWorldToHClip(world);
                o.uv = v.uv;
                half twinkle = 0.55 + 0.45 * sin(_Time.y * (0.7 + v.seed.w * 1.6) + v.seed.w * 40.0);
                o.alpha = fade * twinkle;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float r = length(saturate(i.uv) - 0.5) * 2.0;
                half d = saturate(1.0 - r);
                d *= d;
                return half4(_Color.rgb * (_Intensity * i.alpha * d), 1);
            }
            ENDHLSL
        }
    }
}
