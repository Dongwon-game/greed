Shader "Greed Last/Runner Surface"
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

            struct Vertex { float4 position : POSITION; float3 normal : NORMAL; fixed4 tint : COLOR; };
            struct Surface { float4 position : SV_POSITION; half3 normal : TEXCOORD0; fixed3 tint : COLOR; };

            Surface vert(Vertex input)
            {
                Surface output;
                output.position = UnityObjectToClipPos(input.position);
                output.normal = UnityObjectToWorldNormal(input.normal);
                output.tint = input.tint.rgb;
                return output;
            }

            fixed4 frag(Surface surface) : SV_Target
            {
                half3 normal = normalize(surface.normal);
                half key = saturate(dot(normal, half3(-0.48, 0.77, -0.42)));
                half reflected = saturate(dot(normal, half3(0.76, 0.14, -0.64)));
                half3 light = half3(0.58, 0.62, 0.63) + key * half3(0.53, 0.43, 0.31)
                    + reflected * half3(0.11, 0.09, 0.07);
                return fixed4(surface.tint * light, 1.0);
            }
            ENDCG
        }
    }
}
