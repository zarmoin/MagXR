Shader "Custom/selective_mag"
{
    Properties
    {
        // Unity automatically passes the active eye's frame buffer into _MainTex
        [HideInInspector] _MainTex ("Base (RGB)", 2D) = "white" {} 
        
        // We leave these open but remove default assignments so C# can drive them globally on the GPU
        _InwardShift ("Inward Lens Shift", Range(0.0, 0.2)) = 0.03 
    }
    SubShader
    {
        // Set queue to overlay to make sure it draws on top of all 3D geometries and elements
        Tags { "RenderType"="Opaque" "Queue"="Overlay" }
        LOD 100
        
        // Disable depth writing and testing so it treats the image as a screen-space effect pass
        ZTest Always 
        ZWrite Off 
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            
            // Force multi-view and single-pass instanced array compilation support for modern VR headsets
            #pragma multi_compile_fwdbase

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID // Crucial: Tracks the current VR eye index array layer
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 screenUV : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO     // Crucial: Passes eye identity tracking down to fragment stage
            };

            // CRUCIAL: Replaces standard sampler2D to support Single Pass Instanced (2D Array eye textures)
            UNITY_DECLARE_SCREENSPACE_TEXTURE(_MainTex);
            
            // Driven globally from your C# Update loop via Shader.SetGlobalFloat
            float _ZoomFactor;
            float _LensRadius;
            float _InwardShift;

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);             
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o); 

                o.vertex = UnityObjectToClipPos(v.vertex);
                
                // For a fullscreen blit quad, ComputeScreenPos matches clip space coordinates perfectly
                float4 screenPos = ComputeScreenPos(o.vertex);
                o.screenUV = screenPos.xy / screenPos.w;
                
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i); // Resolves unity_StereoEyeIndex for this current eye's pixel pass

                // --- STEREOSCOPIC LENS CENTER CONVERGENCE MATH ---
                // unity_StereoEyeIndex is 0 for Left Eye, 1 for Right Eye.
                // Left Eye (0):  0 * 2 - 1 = -1 -> multiplied by -_InwardShift = +_InwardShift (Shifts Center Right)
                // Right Eye (1): 1 * 2 - 1 = +1 -> multiplied by -_InwardShift = -_InwardShift (Shifts Center Left)
                float eyeDirectionShift = (unity_StereoEyeIndex * 2.0 - 1.0) * -_InwardShift;
                float2 lensCenter = float2(0.5 + eyeDirectionShift, 0.5);

                // Calculate distance based on the fullscreen pixel position relative to the adjusted eye center
                float dist = distance(i.screenUV, lensCenter);
                float2 finalUV = i.screenUV;

                if (dist < _LensRadius)
                {
                    // Magnify outwards from the designated lens convergence center (not the generic center 0.5)
                    finalUV = (i.screenUV - lensCenter) / _ZoomFactor + lensCenter;
                }

                // Sample from the correct Left or Right eye texture array slice using the VR-aware macro
                return UNITY_SAMPLE_SCREENSPACE_TEXTURE(_MainTex, finalUV);
            }
            ENDCG
        }
    }
}
