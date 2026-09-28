Shader "Custom/GreenFilter_HLSL"
{
    Properties
    {
        _MainTex ("Main Texture", 2D) = "white" {}
        _GreenStrength ("Green Strength", Range(0,1)) = 0.5
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        Cull Off
        ZWrite Off
        ZTest Always

        Pass
        {
            Name "GreenFilterPass"

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float _GreenStrength;

            struct Attributes
            {
                float4 vertex : POSITION;
                float2 uv     : TEXCOORD0;
            };

            struct Varyings
            {
                float4 pos : SV_POSITION;
                float2 uv  : TEXCOORD0;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.pos = UnityObjectToClipPos(IN.vertex);
                OUT.uv = IN.uv;
                return OUT;
            }

            fixed4 frag(Varyings IN) : SV_Target
            {
                fixed4 color = tex2D(_MainTex, IN.uv);
                color.rgb = lerp(color.rgb, float3(0, 1, 0), _GreenStrength);
                return color;
            }

            ENDHLSL
        }
    }
    FallBack "Diffuse"
}
