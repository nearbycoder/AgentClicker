// The sky outside the office window: a gradient from the horizon to the zenith by view direction, haze over the
// skyline, a glow and a soft disc where the sun is, and a few stars at night. Drawn on a backdrop beyond the city.
// TimeOfDay sets the colours every couple of game minutes.
Shader "AgentClicker/Sky"
{
    Properties
    {
        _Zenith ("Zenith", Color) = (0.35, 0.6, 0.95, 1)
        _Horizon ("Horizon", Color) = (0.75, 0.86, 0.97, 1)
        _Haze ("Haze (below the horizon)", Color) = (0.62, 0.7, 0.8, 1)
        _SunDir ("Direction to the sun", Vector) = (-0.6, 0.5, -0.3, 0)
        _SunColor ("Sun colour", Color) = (1, 0.92, 0.75, 1)
        _SunStrength ("Sun strength", Float) = 1
        _Stars ("Stars", Float) = 0
        _HorizonY ("Horizon height (world)", Float) = 1.5
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry+10" "RenderPipeline" = "UniversalPipeline" }
        Pass
        {
            Name "Sky"
            Tags { "LightMode" = "UniversalForward" }
            ZWrite On
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Zenith, _Horizon, _Haze, _SunColor;
                float4 _SunDir;
                half _SunStrength, _Stars;
                float _HorizonY;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                return o;
            }

            float Hash(float3 p)
            {
                p = frac(p * 0.3183099 + 0.1);
                p *= 17.0;
                return frac(p.x * p.y * p.z * (p.x + p.y + p.z));
            }

            half4 frag(Varyings i) : SV_Target
            {
                float3 toPoint = i.positionWS - _WorldSpaceCameraPos;
                float3 dir = normalize(toPoint);
                // elevation as seen from the room (the backdrop is far, so this is close to the true view angle)
                float up = (i.positionWS.y - _HorizonY) / max(1.0, length(toPoint.xz));
                float t = saturate(up * 1.6);
                half3 sky = lerp(_Horizon.rgb, _Zenith.rgb, pow(max(t, 1e-4), 0.7));
                // haze thickens towards and below the horizon
                sky = lerp(sky, _Haze.rgb, saturate(-up * 3.0 + 0.15) * 0.85);

                float3 sunDir = normalize(_SunDir.xyz);
                float d = saturate(dot(dir, sunDir));
                half glow = pow(max(d, 1e-4), 8.0) * 0.35 + pow(max(d, 1e-4), 64.0) * 0.6;
                half disc = smoothstep(0.9994, 0.9998, d);
                sky += _SunColor.rgb * _SunStrength * (glow + disc * 2.5);

                // a few faint stars once the sky is dark: a point somewhere in some of the cells of a fine grid, drawn as a
                // soft dot a pixel or two across
                float3 g = dir * 260.0;
                float3 cell = floor(g);
                float h = Hash(cell);
                float3 at = cell + 0.25 + 0.5 * float3(Hash(cell + 17.0), Hash(cell + 31.0), Hash(cell + 47.0));
                half star = step(0.985, h) * smoothstep(0.35, 0.05, length(g - at)) * saturate(up * 4.0);
                sky += star * _Stars * (0.5 + 0.8 * frac(h * 97.0));
                return half4(max(0, sky), 1);
            }
            ENDHLSL
        }
    }
}
