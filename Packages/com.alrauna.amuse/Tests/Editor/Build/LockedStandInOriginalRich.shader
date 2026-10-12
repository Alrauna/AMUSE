// Stand-in for an original shader whose declared table carries the color
// and vector properties the reconstruction moves. Schema only: it resolves
// through the AssetDatabase and fails the pinned Poiyomi identity, exactly
// like the plain original stand-in beside it.
Shader "Hidden/Alrauna/AmuseTests/LockedStandInOriginalRich"
{
    Properties
    {
        _MainTex ("Main Texture", 2D) = "white" {}
        _ShaderOptimizerEnabled ("Shader Optimizer Enabled", Float) = 0
        _Tint ("Tint", Color) = (1, 1, 1, 1)
        _Shear ("Shear", Vector) = (0, 0, 0, 0)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct v2f
            {
                float4 position : SV_POSITION;
            };

            v2f vert(appdata_base input)
            {
                v2f output;
                output.position = UnityObjectToClipPos(input.vertex);
                return output;
            }

            fixed4 frag() : SV_Target
            {
                return fixed4(1.0, 1.0, 1.0, 1.0);
            }
            ENDCG
        }
    }
    Fallback Off
}
