// Executable specification of the lilToon regular Transparent Normal property
// contract AMUSE consumes for the transparent-to-opaque conversion. It is a
// purpose-built stand-in for deterministic tests, not a pretend lilToon
// distribution, and contains no upstream lilToon source.
Shader "Hidden/Alrauna/AmuseTests/LilToonTransparentConversionTest"
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
        // Declared but deliberately NOT part of the transparent alpha
        // evidence request: LIL_RENDER 2 compiles the runtime dither path
        // out entirely, so an authored toggle is inert here (design §8).
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
        _IDMaskPrior8 ("IDMaskPrior8", Int) = 0

        _Cutoff ("Cutoff", Range(0,1)) = 0.5
        _Color ("Color", Color) = (1,1,1,1)
        _MainTex ("Texture", 2D) = "white" {}
        _DissolveParams ("DissolveParams", Vector) = (0,0,0.5,0.1)
        _MainTex_ScrollRotate ("ScrollRotate", Vector) = (0,0,0,0)

        // Transparent-only proof properties, at the vendor defaults.
        _AlphaBoostFA ("AlphaBoostFA", Float) = 10
        _SubpassCutoff ("SubpassCutoff", Range(0,1)) = 0.5
        _DistanceFade ("DistanceFade", Vector) = (0.1,0.01,0,0)
        _DistanceFadeColor ("DistanceFadeColor", Color) = (0,0,0,1)

        // Fresh transparent render state. Differs from the cutout stand-in
        // in exactly one default: _DstBlend is OneMinusSrcAlpha, which gate 9
        // already admits (T1 §4.4, §7).
        _SrcBlend ("SrcBlend", Float) = 1
        _DstBlend ("DstBlend", Float) = 10
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
        Tags { "RenderType" = "TransparentCutout" "Queue" = "AlphaTest+10" }

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
