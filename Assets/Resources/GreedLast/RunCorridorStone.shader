Shader "Greed Last/Run Corridor Stone"
{
    Properties
    {
        [PerRendererData] _CorridorStart ("Corridor start distance", Float) = 0
        [PerRendererData] _TrapWarning ("Trap timing", Range(0, 1)) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        Pass
        {
            CGPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            #include "CorridorLighting.cginc"

            float _CorridorStart;
            float _TrapWarning;
            struct Vertex
            {
                float4 position : POSITION;
                float3 normal : NORMAL;
                fixed4 color : COLOR;
                float2 course : TEXCOORD0;
            };
            struct Surface
            {
                float4 position : SV_POSITION;
                float3 course : TEXCOORD0;
                float distance : TEXCOORD1;
                half3 normal : TEXCOORD2;
                fixed4 color : COLOR;
            };

            Surface vert(Vertex input)
            {
                Surface result;
                result.position = UnityObjectToClipPos(input.position);
                // Use course coordinates so patterns and lighting follow the stone around turns.
                result.course = float3(input.course.x, input.position.y, input.course.y + _CorridorStart);
                result.distance = -UnityObjectToViewPos(input.position).z;
                result.normal = input.normal;
                result.color = input.color;
                return result;
            }

            float stoneHash(float3 point)
            {
                point = frac(point * 0.1031);
                point += dot(point, point.yzx + 33.33);
                return frac((point.x + point.y) * point.z);
            }

            float stoneNoise(float3 point)
            {
                float3 cell = floor(point);
                float3 blend = frac(point);
                blend = blend * blend * (3.0 - 2.0 * blend);
                float nearA = lerp(stoneHash(cell), stoneHash(cell + float3(1, 0, 0)), blend.x);
                float nearB = lerp(stoneHash(cell + float3(0, 1, 0)), stoneHash(cell + float3(1, 1, 0)), blend.x);
                float farA = lerp(stoneHash(cell + float3(0, 0, 1)), stoneHash(cell + float3(1, 0, 1)), blend.x);
                float farB = lerp(stoneHash(cell + float3(0, 1, 1)), stoneHash(cell + float3(1, 1, 1)), blend.x);
                return lerp(lerp(nearA, nearB, blend.y), lerp(farA, farB, blend.y), blend.z);
            }

            fixed4 frag(Surface surface) : SV_Target
            {
                float3 position = surface.course;
                float3 normal = normalize(surface.normal);
                float3 footprint = fwidth(position);
                float pixelWidth = max(footprint.x, max(footprint.y, footprint.z));
                float mineral = stoneNoise(position * float3(1.80, 4.65, 1.60));
                float flecks = stoneNoise(position * 8.2) - 0.5;
                float sand = stoneNoise(position * 29.0) - 0.5;
                float weathering = 0.87 + mineral * 0.23
                    + flecks * 0.18 * (1.0 - smoothstep(0.04, 0.15, pixelWidth))
                    + sand * 0.12 * (1.0 - smoothstep(0.016, 0.046, pixelWidth));
                float settledDust = smoothstep(0.65, 1.0, normal.y) * smoothstep(0.53, 0.85, mineral);
                float3 color = surface.color.rgb * GreedCorridorLight(position, normal) * weathering;
                color += float3(0.068, 0.049, 0.027) * settledDust;
                color += float3(0.28, 0.15, 0.046) * _TrapWarning;
                color = lerp(color, surface.color.rgb * 1.16, surface.color.a);
                return fixed4(GreedCorridorAir(color, surface.distance), 1.0);
            }
            ENDCG
        }
    }
}
