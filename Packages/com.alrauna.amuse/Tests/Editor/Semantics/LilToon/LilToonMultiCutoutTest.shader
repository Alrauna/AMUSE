// Purpose-built stand-in for the Multi mode-1 (cutout) eligibility
// fixtures: the base Multi container shape at the vendor cutout state the
// vendor editor writes when a Multi material switches to cutout mode -
// RenderType TransparentCutout at declared queue 2450 (AlphaTest), blend
// One/Zero, _AlphaToMask 1. The property set mirrors the shared block both
// Multi containers declare: the eighteen canonical opaque recipe
// properties, the version and cutoff source facts, the three Multi
// scalars, the runtime feature scalars the mode gate and the mode-1 rows
// read, and the outline property set, which is inert residue on this
// container. The vendor container name is load-bearing through the Task 5
// mode-consistency gate, whose outline-tone keyword derivation reads the
// captured name, and the task fixtures must not depend on stand-in
// defaults outside this declared state. It is not a
// pretend lilToon distribution, contains no upstream lilToon source, no
// LIL_RENDER define, and no keyword variants.
Shader "_lil/lilToonMulti"
{
    Properties
    {
        _TransparentMode ("TransparentMode", Float) = 0
        _UseClippingCanceller ("UseClippingCanceller", Float) = 0
        _AsOverlay ("AsOverlay", Float) = 0

        [HideInInspector] _lilToonVersion ("Version", Int) = 45

        // Runtime feature scalars the mode gate and the mode-1 rows read.
        _AlphaMaskMode ("AlphaMaskMode", Int) = 0
        _UseDither ("UseDither", Int) = 0
        _DissolveParams ("DissolveParams", Vector) = (0,0,0.5,0.1)
        _DistanceFade ("DistanceFade", Vector) = (0.1,0.01,0,0)

        // Shared outline property set. Both containers declare it; live
        // container state on the outline container, inert residue here.
        _OutlineTex ("OutlineTex", 2D) = "white" {}
        _OutlineTex_ScrollRotate ("OutlineTexScrollRotate", Vector) = (0,0,0,0)
        _OutlineTexHSVG ("OutlineTexHSVG", Vector) = (0,1,1,1)
        _OutlineColor ("OutlineColor", Color) = (1,1,1,1)
        _OutlineWidth ("OutlineWidth", Float) = 0.08
        _OutlineDeleteMesh ("OutlineDeleteMesh", Int) = 0
        _OutlineDisableInVR ("OutlineDisableInVR", Int) = 0
        _OutlineZTest ("OutlineZTest", Int) = 2
        _OutlineZWrite ("OutlineZWrite", Int) = 1
        _OutlineCull ("OutlineCull", Int) = 1
        _OutlineColorMask ("OutlineColorMask", Int) = 15

        _Cutoff ("Cutoff", Range(0,1)) = 0.5

        // The vendor Multi cutout state: blend One/Zero, _AlphaToMask 1.
        _SrcBlend ("SrcBlend", Float) = 1
        _DstBlend ("DstBlend", Float) = 0
        _AlphaToMask ("AlphaToMask", Float) = 1
        _ZWrite ("ZWrite", Float) = 1
        _ZTest ("ZTest", Float) = 4
        _OffsetFactor ("OffsetFactor", Float) = 0
        _OffsetUnits ("OffsetUnits", Float) = 0
        _ColorMask ("ColorMask", Float) = 15
        _SrcBlendAlpha ("SrcBlendAlpha", Float) = 1
        _DstBlendAlpha ("DstBlendAlpha", Float) = 0
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
