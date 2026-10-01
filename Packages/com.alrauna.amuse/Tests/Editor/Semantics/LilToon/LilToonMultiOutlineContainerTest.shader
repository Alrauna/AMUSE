// Purpose-built stand-in for the Multi conversion-recipe fixtures: the
// outline Multi container shape. Identical schema to the base container
// stand-in - the eighteen canonical opaque recipe properties, the mode
// scalar, and the two gate scalars - so the same fixture discipline applies
// on both containers. The declared render state is the vendor
// outline-container form: RenderType Transparent at declared queue 2900
// (Transparent-100). That declared state is the point of the fixture: a
// conversion recipe that leaves the clone at container defaults produces
// Transparent/2900 where the canonical facts are Opaque/2000. It is not a
// pretend lilToon distribution and contains no upstream lilToon source, no
// LIL_RENDER define, and no keyword variants.
Shader "Hidden/Alrauna/AmuseTests/LilToonMultiOutlineContainerTest"
{
    Properties
    {
        _TransparentMode ("TransparentMode", Float) = 0
        _UseClippingCanceller ("UseClippingCanceller", Float) = 0
        _AsOverlay ("AsOverlay", Float) = 0

        // Canonical opaque conversion tuple (B1 9; spec 9.1).
        _SrcBlend ("SrcBlend", Float) = 1
        _DstBlend ("DstBlend", Float) = 0
        _AlphaToMask ("AlphaToMask", Float) = 0
        _ZWrite ("ZWrite", Float) = 1
        _ZTest ("ZTest", Float) = 4
        _OffsetFactor ("OffsetFactor", Float) = 0
        _OffsetUnits ("OffsetUnits", Float) = 0
        _ColorMask ("ColorMask", Float) = 15
        _SrcBlendAlpha ("SrcBlendAlpha", Float) = 1
        _DstBlendAlpha ("DstBlendAlpha", Float) = 10
        _BlendOp ("BlendOp", Float) = 0
        _BlendOpAlpha ("BlendOpAlpha", Float) = 0
        _SrcBlendFA ("SrcBlendFA", Float) = 1
        _DstBlendFA ("DstBlendFA", Float) = 1
        _SrcBlendAlphaFA ("SrcBlendAlphaFA", Float) = 0
        _DstBlendAlphaFA ("DstBlendAlphaFA", Float) = 1
        _BlendOpFA ("BlendOpFA", Float) = 4
        _BlendOpAlphaFA ("BlendOpAlphaFA", Float) = 4
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent-100" }

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
