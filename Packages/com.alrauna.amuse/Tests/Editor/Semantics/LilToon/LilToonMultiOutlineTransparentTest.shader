// Purpose-built stand-in for the Multi mode-2 (transparent) eligibility
// fixtures: the outline Multi container shape at the vendor transparent
// state the vendor editor writes when a Multi material switches to
// transparent mode. The declared render state is that vendor transparent
// form (RenderType TransparentCutout at declared queue 2460,
// blend One/OneMinusSrcAlpha, _ZWrite on, _AlphaToMask 0), not the
// outline container's opaque default of Transparent at 2900, because the
// mode-2 fixtures capture the state the vendor writes at transparent mode
// on either container. The property set is byte-identical to the base
// transparent container stand-in's, exactly as the two vendor container
// assets share one property block; the outline property set is live
// container state here, and the declared _OutlineTexHSVG default is the
// written-off tone the pinned derivation writes no keyword for. The
// vendor container name is load-bearing through the Task 5
// mode-consistency gate, whose outline-tone keyword derivation reads the
// captured name. It declares no _PreCutoff and carries no FORWARD_BACK
// pre-pass: Multi transparent is Normal-class only. It is not a pretend
// lilToon distribution, contains no upstream lilToon source, no
// LIL_RENDER define, and no keyword variants.
Shader "Hidden/lilToonMultiOutline"
{
    Properties
    {
        _TransparentMode ("TransparentMode", Float) = 0
        _UseClippingCanceller ("UseClippingCanceller", Float) = 0
        _AsOverlay ("AsOverlay", Float) = 0

        [HideInInspector] _lilToonVersion ("Version", Int) = 45

        // Runtime feature scalars the mode gate and the runtime rows read.
        _AlphaMaskMode ("AlphaMaskMode", Int) = 0
        _UseDither ("UseDither", Int) = 0
        _DissolveParams ("DissolveParams", Vector) = (0,0,0.5,0.1)
        _DistanceFade ("DistanceFade", Vector) = (0.1,0.01,0,0)

        // Shared outline property set, live on this container.
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

        // Transparent-only proof properties, at the vendor defaults.
        _AlphaBoostFA ("AlphaBoostFA", Float) = 10
        _SubpassCutoff ("SubpassCutoff", Range(0,1)) = 0.5

        // The vendor Multi transparent state: blend One/OneMinusSrcAlpha,
        // _ZWrite on, _AlphaToMask off.
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
