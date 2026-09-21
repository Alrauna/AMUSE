// Stand-in for a Thry locked generated shader. Schema only: it carries the
// lock flag property the lock identity reads, and no upstream vendor source.
// The declared name starts with the real locked prefix on purpose; see the
// test helper that loads it.
Shader "Hidden/Locked/Alrauna/AmuseTests/LockedStandIn"
{
    Properties
    {
        _ShaderOptimizerEnabled ("Shader Optimizer Enabled", Float) = 0
        _MainTex ("Main Texture", 2D) = "white" {}
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
