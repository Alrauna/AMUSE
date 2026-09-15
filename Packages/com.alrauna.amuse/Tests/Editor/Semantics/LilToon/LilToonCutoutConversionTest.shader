// Executable specification of the lilToon cutout property contract AMUSE
// consumes for the cutout-to-opaque conversion. It is a purpose-built
// stand-in for deterministic tests, not a pretend lilToon distribution, and
// contains no upstream lilToon source.
Shader "Hidden/Alrauna/AmuseTests/LilToonCutoutConversionTest"
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
        _Color2nd ("Color2nd", Color) = (1,1,1,1)
        _Color3rd ("Color3rd", Color) = (1,1,1,1)
        _Main2ndTex ("Main2ndTex", 2D) = "white" {}
        _Main3rdTex ("Main3rdTex", 2D) = "white" {}
        _Main2ndTex_ST ("Main2ndTexST", Vector) = (1,1,0,0)
        _Main3rdTex_ST ("Main3rdTexST", Vector) = (1,1,0,0)
        _Main2ndTex_ScrollRotate ("Main2ndScrollRotate", Vector) = (0,0,0,0)
        _Main3rdTex_ScrollRotate ("Main3rdScrollRotate", Vector) = (0,0,0,0)
        _Main2ndTexAngle ("Main2ndAngle", Float) = 0
        _Main3rdTexAngle ("Main3rdAngle", Float) = 0
        _Main2ndTex_UVMode ("Main2ndUVMode", Int) = 0
        _Main3rdTex_UVMode ("Main3rdUVMode", Int) = 0
        _Main2ndTexAlphaMode ("Main2ndAlphaMode", Int) = 0
        _Main3rdTexAlphaMode ("Main3rdAlphaMode", Int) = 0
        _Main2ndTex_Cull ("Main2ndCull", Int) = 0
        _Main3rdTex_Cull ("Main3rdCull", Int) = 0
        _Main2ndTexIsDecal ("Main2ndIsDecal", Int) = 0
        _Main3rdTexIsDecal ("Main3rdIsDecal", Int) = 0
        _Main2ndTexIsLeftOnly ("Main2ndIsLeftOnly", Int) = 0
        _Main3rdTexIsLeftOnly ("Main3rdIsLeftOnly", Int) = 0
        _Main2ndTexIsRightOnly ("Main2ndIsRightOnly", Int) = 0
        _Main3rdTexIsRightOnly ("Main3rdIsRightOnly", Int) = 0
        _Main2ndTexShouldCopy ("Main2ndShouldCopy", Int) = 0
        _Main3rdTexShouldCopy ("Main3rdShouldCopy", Int) = 0
        _Main2ndTexShouldFlipMirror ("Main2ndShouldFlipMirror", Int) = 0
        _Main3rdTexShouldFlipMirror ("Main3rdShouldFlipMirror", Int) = 0
        _Main2ndTexShouldFlipCopy ("Main2ndShouldFlipCopy", Int) = 0
        _Main3rdTexShouldFlipCopy ("Main3rdShouldFlipCopy", Int) = 0
        _Main2ndTexIsMSDF ("Main2ndIsMSDF", Int) = 0
        _Main3rdTexIsMSDF ("Main3rdIsMSDF", Int) = 0
        _Main2ndBlendMask ("Main2ndBlendMask", 2D) = "white" {}
        _Main3rdBlendMask ("Main3rdBlendMask", 2D) = "white" {}
        _Main2ndDistanceFade ("Main2ndDistanceFade", Vector) = (0.1,0.01,0,0)
        _Main3rdDistanceFade ("Main3rdDistanceFade", Vector) = (0.1,0.01,0,0)
        _Main2ndDissolveParams ("Main2ndDissolveParams", Vector) = (0,0,0.5,0.1)
        _Main3rdDissolveParams ("Main3rdDissolveParams", Vector) = (0,0,0.5,0.1)
        _AudioLink2Main2nd ("AudioLink2Main2nd", Int) = 0
        _AudioLink2Main3rd ("AudioLink2Main3rd", Int) = 0
        _AlphaMaskMode ("AlphaMaskMode", Int) = 0
        _AlphaMaskScale ("AlphaMaskScale", Float) = 1
        _AlphaMaskValue ("AlphaMaskValue", Float) = 0
        _AlphaMask ("AlphaMask", 2D) = "white" {}
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

        // Fresh cutout render state. The schema is complete because the
        // conversion decision captures these source facts before swapping to
        // the distinct opaque target shader.
        _SrcBlend ("SrcBlend", Float) = 1
        _DstBlend ("DstBlend", Float) = 0
        _AlphaToMask ("AlphaToMask", Float) = 1
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
        Tags { "RenderType" = "TransparentCutout" "Queue" = "AlphaTest" }

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
