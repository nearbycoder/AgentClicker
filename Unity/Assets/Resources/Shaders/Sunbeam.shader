// Light falling through the office window: an additive prism from the window opening along the sun's direction.
// It fades along its length (uv.y: 0 at the window, 1 at the far end) and where its faces are seen edge-on, so it
// reads as a soft shaft rather than a box. A slow drift of noise keeps it from looking like glass.
Shader "AgentClicker/Sunbeam"
{
    Properties
    {
        _Color ("Colour", Color) = (1, 0.9, 0.72, 1)
        _Intensity ("Intensity", Float) = 0.08
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Pass
        {
            Name "Sunbeam"
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
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 positionWS : TEXCOORD2;
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.uv = v.uv;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float3 view = normalize(_WorldSpaceCameraPos - i.positionWS);
                half facing = abs(dot(normalize(i.normalWS + 1e-5), view));
                half soft = facing * facing;
                // MSAA can evaluate a pixel just outside the triangle, where the interpolated uv runs past 0 or 1: clamp it,
                // or pow() of a negative number is NaN and the bloom spreads it across the screen
                half along = saturate(i.uv.y);
                half fade = smoothstep(0.0, 0.08, along) * pow(saturate(1.0 - along), 1.6);
                // slow streaks across the shaft
                half streak = 0.75 + 0.25 * sin(i.uv.x * 23.0 + _Time.y * 0.35) * sin(i.uv.x * 7.0 - _Time.y * 0.2);
                return half4(max(0, _Color.rgb * (_Intensity * soft * fade * streak)), 1);
            }
            ENDHLSL
        }
    }
}
