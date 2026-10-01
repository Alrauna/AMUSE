// Purpose-built stand-in for the Multi mode-consistency gate fixtures. It
// declares exactly the two scalar facts the gate reads, so a fixture that
// sets the scalars genuinely carries them through the production capture.
// It is not a pretend lilToon distribution and contains no upstream
// lilToon source.
Shader "Hidden/Alrauna/AmuseTests/LilToonMultiModeGateTest"
{
    Properties
    {
        _UseClippingCanceller ("UseClippingCanceller", Float) = 0
        _AsOverlay ("AsOverlay", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" }

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            float4 vert(float4 vertex : POSITION) : SV_POSITION
            {
                return UnityObjectToClipPos(vertex);
            }

            fixed4 frag() : SV_Target
            {
                return fixed4(1, 1, 1, 1);
            }
            ENDCG
        }
    }
}
