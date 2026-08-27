// Original AMUSE test fixture. It reproduces only the Poiyomi 9.3.64 property
// NAMES, types, and defaults that the verified interpreter reads, so tests can
// build a schema-complete material without installing Poiyomi. It deliberately
// contains none of Poiyomi's shading equations; the SubShader is a trivial
// unlit pass whose only job is to compile so a Material can exist. This shader
// never passes source attestation (its source hash is not the pinned hash); it
// is exercised through the InterpretVerifiedMaterial seam and the extraction
// helpers directly.
Shader "Hidden/Alrauna/AmuseTests/PoiyomiSemanticTest"
{
    Properties
    {
        shader_master_label ("AMUSE Poiyomi Test Fixture", Float) = 0
        _ShaderOptimizerEnabled ("Locked", Float) = 0

        // Base color / main texture.
        _Color ("Color", Color) = (1,1,1,1)
        _ColorThemeIndex ("Color Theme Index", Float) = 0
        _MainTex ("Main Texture", 2D) = "white" {}
        _MainTexUV ("Main UV", Float) = 0
        _MainTexPan ("Main Pan", Vector) = (0,0,0,0)
        _MainPixelMode ("Main Pixel Mode", Float) = 0
        _MainTexStochastic ("Main Stochastic", Float) = 0
        _MainIgnoreTexAlpha ("Ignore Main Alpha", Float) = 0

        // Normal.
        [Normal] _BumpMap ("Normal Map", 2D) = "bump" {}
        _BumpMapUV ("Normal UV", Float) = 0
        _BumpMapPan ("Normal Pan", Vector) = (0,0,0,0)
        _BumpMapStochastic ("Normal Stochastic", Float) = 0
        _BumpScale ("Normal Scale", Float) = 1

        // Alpha.
        _AlphaForceOpaque ("Force Opaque", Float) = 1
        _AlphaMod ("Alpha Mod", Float) = 0
        _MainAlphaMaskMode ("Alpha Mask Mode", Float) = 2
        _AlphaMask ("Alpha Mask", 2D) = "white" {}
        _AlphaMaskBlendStrength ("Alpha Mask Blend Strength", Float) = 1
        _AlphaMaskValue ("Alpha Mask Blend Offset", Float) = 0
        _AlphaMaskInvert ("Alpha Mask Invert", Float) = 0
        _AlphaToCoverage ("A2C", Float) = 0
        _AlphaSharpenedA2C ("Sharpened A2C", Float) = 0
        _AlphaDithering ("Alpha Dithering", Float) = 0
        _EnableDissolve ("Enable Dissolve", Float) = 0
        _EnableUDIMDiscardOptions ("Enable UDIM Discard", Float) = 0
        _AlphaDistanceFade ("Alpha Distance Fade", Float) = 0
        _AlphaFresnel ("Alpha Fresnel", Float) = 0
        _AlphaAngular ("Alpha Angular", Float) = 0
        _AlphaAudioLinkEnabled ("Alpha AudioLink", Float) = 0
        _EnableAudioLink ("Enable AudioLink", Float) = 0
        _AlphaGlobalMask ("Alpha Global Mask", Float) = 0
        _AlphaPremultiply ("Alpha Premultiply", Float) = 0

        // Emission (four slots; slot 0 detail).
        _EnableEmission ("Enable Emission 0", Float) = 0
        _EnableEmission1 ("Enable Emission 1", Float) = 0
        _EnableEmission2 ("Enable Emission 2", Float) = 0
        _EnableEmission3 ("Enable Emission 3", Float) = 0
        [HDR] _EmissionColor ("Emission Color", Color) = (1,1,1,1)
        _EmissionStrength ("Emission Strength", Float) = 0
        _EmissionMap ("Emission Map", 2D) = "white" {}
        _EmissionMapUV ("Emission UV", Float) = 0
        _EmissionMapPan ("Emission Pan", Vector) = (0,0,0,0)
        _EmissionColorThemeIndex ("Emission Theme Index", Float) = 0
        _EmissionBaseColorAsMap ("Emission Base Color As Map", Float) = 0
        _EmissionReplace0 ("Emission Replace Base", Float) = 0
        _EmissionFluorescence ("Emission Fluorescence", Float) = 0
        _EmissionCenterOutEnabled ("Emission Center Out", Float) = 0
        _EmissionBlinkingEnabled ("Emission Blinking", Float) = 0
        _ScrollingEmission ("Emission Scrolling", Float) = 0
        _EmissionHueShiftEnabled ("Emission Hue Shift", Float) = 0
        _EnableGITDEmission ("Emission Light Based", Float) = 0
        _EmissionAL0Enabled ("Emission AudioLink", Float) = 0
        _EmissionMask0GlobalMask ("Emission Global Mask", Float) = 0
        _EmissionMaskInvert ("Emission Mask Invert", Float) = 0
        _EmissionMask ("Emission Mask", 2D) = "white" {}

        // BaseColor / Alpha feature-writer gates. These reproduce the pinned
        // Poiyomi 9.3.64 toggle NAMES that the interpreter proves are off before
        // claiming a representable equation; every one defaults off. Grouped by
        // the design's named source-writing groups.
        _MainColorAdjustToggle ("Main Color Adjust", Float) = 0
        _MainHueShiftToggle ("Main Hue Shift", Float) = 0
        _MainHueALCTEnabled ("Main Hue AudioLink", Float) = 0
        _DetailEnabled ("Detail", Float) = 0
        _MainVertexColoringEnabled ("Vertex Coloring", Float) = 0
        _BackFaceEnabled ("Backface", Float) = 0
        _RGBMaskEnabled ("RGBA Mask", Float) = 0
        _DecalEnabled ("Decal 0", Float) = 0
        _DecalEnabled1 ("Decal 1", Float) = 0
        _DecalEnabled2 ("Decal 2", Float) = 0
        _DecalEnabled3 ("Decal 3", Float) = 0
        _EnableAniso ("Anisotropic", Float) = 0
        _MatcapEnable ("Matcap 1", Float) = 0
        _Matcap2Enable ("Matcap 2", Float) = 0
        _Matcap3Enable ("Matcap 3", Float) = 0
        _Matcap4Enable ("Matcap 4", Float) = 0
        _CubeMapEnabled ("Cubemap", Float) = 0
        _EnableFlipbook ("Flipbook", Float) = 0
        _EnableRimLighting ("Rim", Float) = 0
        _EnableRim2Lighting ("Rim 2", Float) = 0
        _EnableDepthRimLighting ("Depth Rim", Float) = 0
        _EnableEnvironmentalRim ("Environmental Rim", Float) = 0
        _GlitterEnable ("Glitter", Float) = 0
        _StylizedSpecular ("Stylized Reflection", Float) = 0
        _EnablePathing ("Pathing", Float) = 0
        _EnableMirrorOptions ("Mirror Options", Float) = 0
        _MirrorTextureEnabled ("Mirror Texture", Float) = 0
        _TextEnabled ("Text", Float) = 0
        _PoiInternalParallax ("Internal Parallax", Float) = 0
        _PoiParallax ("Parallax", Float) = 0
        _VideoEffectsEnable ("Video Effects", Float) = 0
        _EnableTouchGlow ("Touch / Depth FX", Float) = 0
        _VoronoiEnabled ("Voronoi", Float) = 0
        _EnableTruchet ("Truchet", Float) = 0
        _EmissionReplace1 ("Emission 1 Override Base", Float) = 0
        _EmissionReplace2 ("Emission 2 Override Base", Float) = 0
        _EmissionReplace3 ("Emission 3 Override Base", Float) = 0

        // Render-state properties the normalized Alpha equation deliberately
        // IGNORES (never gated): UI preset, cutoff, blend, depth, and outline
        // state. The opaque-conversion capability DOES read most of these, so
        // they reproduce the pinned Poiyomi 9.3.64 declarations exactly: same
        // names, same types, same defaults, same ranges.
        //
        // _Cutoff's upper bound is 1.001, not 1, in the vendor source. That is
        // not a typo to tidy: the vendor's own declared maximum is above 1, and
        // the shader clips with `clip(alpha - _Cutoff)`, so a material sitting
        // at the top of its declared range discards alpha exactly 1. The
        // conversion eligibility boundary tests depend on 1.001 being settable
        // here, and the range must stay vendor-faithful.
        [Enum(Opaque,0,Cutout,1,Fade,2,Transparent,3)] _Mode ("Rendering Mode", Float) = 0
        _Cutoff ("Alpha Cutoff", Range(0, 1.001)) = 0.5

        // Base pass blend state.
        _BlendOp ("RGB Blend Op", Int) = 0
        _SrcBlend ("Src Blend", Float) = 1
        _DstBlend ("Dst Blend", Float) = 0
        _BlendOpAlpha ("Alpha Blend Op", Int) = 0
        _SrcBlendAlpha ("Alpha Source Blend", Int) = 1
        _DstBlendAlpha ("Alpha Destination Blend", Int) = 10

        // ForwardAdd pass blend state. _AddBlendOp is declared ONLY so the
        // conversion tests can vary it and prove it changes nothing: the
        // canonical recipe never writes it, so the unchanged blend operation
        // cancels once the factors are proven equivalent at alpha 1. It is not
        // a conversion-read property and must not enter conversion evidence.
        _AddBlendOp ("RGB Blend Op", Int) = 4
        _AddSrcBlend ("RGB Source Blend", Int) = 1
        _AddDstBlend ("RGB Destination Blend", Int) = 1
        _AddBlendOpAlpha ("Alpha Blend Op", Int) = 4
        _AddSrcBlendAlpha ("Alpha Source Blend", Int) = 0
        _AddDstBlendAlpha ("Alpha Destination Blend", Int) = 1

        // Depth state.
        _ZWrite ("ZWrite", Int) = 1
        _ZTest ("ZTest", Float) = 4

        // Outlines. _EnableOutlines gates the vendor's outline pass with
        // `clip(_EnableOutlines - 0.01)` before any outline colour/alpha work,
        // and also scales the vertex offset.
        [ToggleUI] _EnableOutlines ("Enable Outlines", Float) = 0
        _OutlineBlendOp ("RGB Blend Op", Int) = 0
        _OutlineSrcBlend ("RGB Source Blend", Int) = 1
        _OutlineDstBlend ("RGB Destination Blend", Int) = 0
        _OutlineBlendOpAlpha ("Alpha Blend Op", Int) = 4
        _OutlineSrcBlendAlpha ("Alpha Source Blend", Int) = 1
        _OutlineDstBlendAlpha ("Alpha Destination Blend", Int) = 0
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

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Color;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                return tex2D(_MainTex, i.uv) * _Color;
            }
            ENDCG
        }
    }
}
