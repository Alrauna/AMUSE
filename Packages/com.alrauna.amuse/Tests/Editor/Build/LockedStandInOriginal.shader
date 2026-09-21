// Stand-in for the original shader a locked material names. Schema only: it
// resolves through the AssetDatabase and fails the pinned Poiyomi identity,
// which is exactly the unattested direction the pre-check must observe.
Shader "Hidden/Alrauna/AmuseTests/LockedStandInOriginal"
{
    Properties
    {
        _MainTex ("Main Texture", 2D) = "white" {}
        _ShaderOptimizerEnabled ("Shader Optimizer Enabled", Float) = 0
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
