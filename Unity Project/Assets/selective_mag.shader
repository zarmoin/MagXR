Shader "Custom/selective_mag"
{
    Properties
    {
        _MainTex ("Map Texture", 2D) = "white" {}
        _ZoomFactor ("Zoom Factor", Float) = 1.0
        _LensRadius ("Lens Radius (Screen %)", Range(0.0, 0.5)) = 0.4
        _InwardShift ("Inward Lens Shift", Range(0.0, 0.2)) = 0.03 // Your manual controller slider
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            // Force multi-view/instanced compilation support for modern VR setups
            #pragma multi_compile_fwdbase

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID // Crucial: Tells the GPU to track VR eyes
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 screenPos : TEXCOORD1;
                float4 vertex : SV_POSITION;
                UNITY_VERTEX_OUTPUT_STEREO     // Crucial: Passes eye identity to fragment stage
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float _ZoomFactor;
            float _LensRadius;
            float _InwardShift;

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);             // Grabs eye data at vertex stage
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o); // Links eye info to output struct

                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.screenPos = ComputeScreenPos(o.vertex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i); // Solves unity_StereoEyeIndex for this pixel

                float2 screenUV = i.screenPos.xy / i.screenPos.w;

                // --- THE INWARD SHIFT MATH ---
                // unity_StereoEyeIndex is 0 for Left Eye, 1 for Right Eye.
                // Left Eye (0):  0 * 2 - 1 = -1 -> multiplied by -_InwardShift = +_InwardShift (Moves Right)
                // Right Eye (1): 1 * 2 - 1 = +1 -> multiplied by -_InwardShift = -_InwardShift (Moves Left)
                float eyeDirectionShift = (unity_StereoEyeIndex * 2.0 - 1.0) * -_InwardShift;

                // Define the modified lens center per eye
                float2 screenCenter = float2(0.5 + eyeDirectionShift, 0.5);

                float dist = distance(screenUV, screenCenter);
                float2 finalUV = i.uv;

                if (dist < _LensRadius)
                {
                    finalUV = (i.uv - 0.5) / _ZoomFactor + 0.5;
                }

                return tex2D(_MainTex, finalUV);
            }
            ENDCG
        }
    }
}
