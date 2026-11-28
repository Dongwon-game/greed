Shader "Greed Last/Runner Surface"
{
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

            float4 _RunnerCourse;
            struct Vertex { float4 position : POSITION; float3 normal : NORMAL; fixed4 tint : COLOR; };
            struct Surface
            {
                float4 position : SV_POSITION;
                half3 normal : TEXCOORD0;
                float3 course : TEXCOORD1;
                fixed3 tint : COLOR;
            };

            Surface vert(Vertex input)
            {
                Surface output;
                output.position = UnityObjectToClipPos(input.position);
                output.normal = input.normal;
                output.course = input.position.xyz + _RunnerCourse.xyz;
                output.tint = input.tint.rgb;
                return output;
            }

            fixed4 frag(Surface surface) : SV_Target
            {
                float3 normal = normalize(surface.normal);
                float reflected = saturate(dot(normal, float3(-0.27, 0.52, -0.81)));
                float3 light = GreedCorridorLight(surface.course, normal) + reflected * float3(0.27, 0.23, 0.19);
                return fixed4(surface.tint * light, 1.0);
            }
            ENDCG
        }
    }
}
