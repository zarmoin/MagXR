Shader "Zoom/ZoomShader"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
    }
    SubShader
    {
        // No culling or depth
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            sampler2D _MainTex;
            float _ZoomLevel;
            float _eyeR;
            float _shift;
            float _PivotY; //NEW TO ACCOUNT FOR UPWARDS SHIFT

            //THIS IS THE ORIGINAL
            fixed4 frag (v2f i) : SV_Target
            {
                float2 zoomUv = (i.uv / _ZoomLevel) + (0.5f - (1.0f / (2.0f * _ZoomLevel))); //ORIGINAL, USE THIS WHEN ONLY USING LOW RES
                //float2 center = float2(0.5f, _PivotY);  //NEW TO ACCOUNT FOR UPWARDS SHIFT, USES .55f to account for it
                //float2 zoomUv = (i.uv / _ZoomLevel) + (center - (1.0f / (2.0f * _ZoomLevel))); //NEW TO ACCOUNT FOR UPWARDS SHIFT
                float2 shift2 = float2 (_shift, 0.0f);

                float2 left_uv = zoomUv + shift2;
                float2 right_uv = zoomUv - shift2;

                float2 scaledUv = right_uv * _eyeR + left_uv * (1 - _eyeR);

                return tex2D(_MainTex, scaledUv);
            }


            //THIS SHOULD BE REMOVED WITH PIVOTX
            // float2 _Pivot;
            
            // fixed4 frag (v2f i) : SV_Target {
                
            //     float2 zoomUv = (i.uv / _ZoomLevel) + (_Pivot - (_Pivot / _ZoomLevel));

            //     float shiftSign = (_eyeR * -2.0) + 1.0; if righteye this will be -1.0, otherwise it will be 1.0
            //     float2 shiftOffset = float2(_shift * shiftSign, 0.0);

            //     float2 finalUv = zoomUv + shiftOffset;

            //     return tex2D(_MainTex, finalUv);
            // }

            ENDCG
        }
    }
}
