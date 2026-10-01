// Purpose-built stand-in for the Multi conversion-recipe fixtures: the base
// Multi container shape. It declares exactly the schema the canonical clone
// recipe and the mode gate read - the eighteen canonical opaque recipe
// properties, the mode scalar, and the two gate scalars - so a fixture that
// sets them genuinely carries them through the production capture and the
// recipe writes read back. It is not a pretend lilToon distribution and
// contains no upstream lilToon source. It declares no LIL_RENDER define and
// no keyword variants: a material's keyword set is material state the recipe
// writes through the keyword API, exactly as the Task 5 gate stand-in
// treats it. The declared render state is the vendor base-container form:
// RenderType Opaque at the default geometry queue.
Shader "Hidden/Alrauna/AmuseTests/LilToonMultiContainerTest"
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
