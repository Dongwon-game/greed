Shader "Greed Last/Runner Ground Shadow"
{
    Properties
    {
        _MainTex ("Soft edge", 2D) = "white" {}
        _Color ("Tint", Color) = (0, 0, 0, 0.5)
    }
    SubShader
    {
        Tags { "Queue"="Transparent-20" "RenderType"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            fixed4 _Color;

            fixed4 frag(v2f_img input) : SV_Target
            {
                return fixed4(_Color.rgb, _Color.a * tex2D(_MainTex, input.uv).a);
            }
            ENDCG
        }
    }
}
