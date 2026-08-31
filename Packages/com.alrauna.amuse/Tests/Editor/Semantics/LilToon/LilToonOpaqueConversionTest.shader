// Executable specification of the lilToon property contract AMUSE consumes
// for the cutout-to-opaque conversion target. It is a purpose-built stand-in
// for deterministic tests, not a pretend lilToon distribution, and contains
// no upstream lilToon source. Distinct from the schema-complete cutout
// source stand-in; this shader carries the canonical opaque defaults used to
// validate the shader swap and exact read-back.
Shader "Hidden/Alrauna/AmuseTests/LilToonOpaqueConversionTest"
{
    Properties
    {
        [HideInInspector] _lilToonVersion ("Version", Int) = 45

        _Invisible ("Invisible", Int) = 0
        _UDIMDiscardCompile ("UDIMDiscardCompile", Int) = 0
        _UDIMDiscardMode ("UDIMDiscardMode", Int) = 0
        _ShiftBackfaceUV ("ShiftBackfaceUV", Int) = 0
        _UseParallax ("UseParallax", Int) = 0
        _UseMain2ndTex ("UseMain2ndTex", Int) = 0
        _UseMain3rdTex ("UseMain3rdTex", Int) = 0
        _AlphaMaskMode ("AlphaMaskMode", Int) = 0
        _UseDither ("UseDither", Int) = 0
        _IDMask1 ("IDMask1", Int) = 0
        _IDMask2 ("IDMask2", Int) = 0
        _IDMask3 ("IDMask3", Int) = 0
        _IDMask4 ("IDMask4", Int) = 0
        _IDMask5 ("IDMask5", Int) = 0
        _IDMask6 ("IDMask6", Int) = 0
        _IDMask7 ("IDMask7", Int) = 0
        _IDMask8 ("IDMask8", Int) = 0
        _IDMaskControlsDissolve ("IDMaskControlsDissolve", Int) = 0
        // Fixture-only vendor prior byte: the B2 counterexample sets it
        // alongside _IDMaskControlsDissolve, and it is deliberately NOT part
        // of the cutout alpha evidence request.
        _IDMaskPrior8 ("IDMaskPrior8", Int) = 0

        _Cutoff ("Cutoff", Range(0,1)) = 0.5
        _Color ("Color", Color) = (1,1,1,1)
        _MainTex ("Texture", 2D) = "white" {}
        _DissolveParams ("DissolveParams", Vector) = (0,0,0.5,0.1)
        _MainTex_ScrollRotate ("ScrollRotate", Vector) = (0,0,0,0)

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
