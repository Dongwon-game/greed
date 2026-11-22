Shader "Greed Last/Run Corridor Stone"
{
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct Vertex { float4 position : POSITION; fixed4 color : COLOR; };
            struct Surface
            {
                float4 position : SV_POSITION;
                float3 world : TEXCOORD0;
                float distance : TEXCOORD1;
                fixed3 color : COLOR;
            };

            Surface vert(Vertex input)
            {
                Surface result;
                result.position = UnityObjectToClipPos(input.position);
                result.world = mul(unity_ObjectToWorld, input.position).xyz;
                result.distance = -UnityObjectToViewPos(input.position).z;
                result.color = input.color.rgb;
                return result;
            }

            fixed4 frag(Surface surface) : SV_Target
            {
                float3 sampleAt = floor(surface.world * 28.0);
                float grain = frac(sin(dot(sampleAt, float3(12.9898, 78.233, 37.719))) * 43758.5453);
                float seams = sin(surface.world.z * 3.1 + sin(surface.world.x * 8.0) * 1.3);
                float lampDepth = abs(frac((surface.world.z - 1.0) / 4.0 + 0.5) - 0.5) * 4.0;
                float glow = saturate(1.0 - lampDepth * 0.62) * smoothstep(1.0, 3.75, abs(surface.world.x));
                float3 color = surface.color * (0.88 + grain * 0.14 + seams * 0.06);
                color += float3(0.10, 0.057, 0.018) * glow;
                float fog = smoothstep(7.0, 52.0, surface.distance);
                return fixed4(lerp(color, float3(0.059, 0.098, 0.102), fog), 1.0);
            }
            ENDCG
        }
    }
}
