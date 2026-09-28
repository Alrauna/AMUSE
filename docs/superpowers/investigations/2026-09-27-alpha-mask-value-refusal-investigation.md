# The `_AlphaMaskValue` slot refusals in the Census Lab

Privacy note: This record uses the play-mode console of the Census Lab project and two materials of a private avatar as evidence. It names no avatar, scene, renderer, material, texture, or asset path. It describes the two refused slots by shader family and role, in prose, with no per-renderer or per-slot table. It writes no instance names, hashes, ports, or machine paths.

Date: 2026-09-27.
Branch: `feat/alpha-mask-value-support`.
Base: `main` at `7d8a429`.
Investigation status on 2026-09-27: no production code had changed at the end of the investigation. The evidence came from the pinned Census Lab editor instance and from repository source. Before the live queries, the instance identity was confirmed. `Application.dataPath` matched the Census Lab project and not the dev repository.

## Question

On 2026-09-27, the NDMF console of the Census Lab editor held five AMUSE slot refusal warnings from the latest play-mode build. Two of them name the alpha mask feature and the property `_AlphaMaskValue`. This record answers three questions. Which material state triggers the refusal? Why does the frontend refuse that state? Which part is the support gap for the new branch?

## Console evidence

The five warnings share one report key, `amuse.slotAnalysis.Refusal`. Each title names one renderer slot and its material. The NDMF error report carries a longer description with the refusal reason and the exact shader fact. The console tool returns the title lines only.

The two in-scope reports name two different shader families. One slot holds a transparent-family material on a lower-body clothing renderer. The other slot holds a cutout-family material on a footwear renderer. A console text filter for `AlphaMask` matches these two reports and no others, so only these two carry the alpha mask sentence.

The report code path establishes that sentence. The slot refusal description embeds the exact shader fact that stopped the proof. See `Packages/com.alrauna.amuse/Editor/Build/AmuseReportStrings.cs:383-392`. For an unsupported feature, the fact sentence says that AMUSE has no proven rule for the named feature and property and that it cannot prove alpha. See `Packages/com.alrauna.amuse/Editor/Build/AmuseReports.cs:100-114`. The diagnostic names the feature label "Alpha mask" and the property `_AlphaMaskValue`. See the label map at `Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonMaterialSemantics.cs:623-626`.

The refusal reason in the same description is `AdmittedMaterialSemanticsUnknown`. Slot resolution refuses with that value when the admitted material semantics resolve Unknown. See `Packages/com.alrauna.amuse/Editor/Analysis/AdmittedMaterialStates.cs:315-319`. The 2026-09-26 Main texture investigation observed the same reason on this code path.

The other three warnings name other features. They are out of scope for this record.

## Live state of the refused slots

On 2026-09-27, read-only queries against the pinned Census Lab editor instance measured both slot materials from their project assets.

One refused slot holds a lilToon transparent family material, shader `Hidden/lilToonTransparent`, at the transparent render queue 2460. Its `_AlphaMaskMode` is 2, the Multiply mode. Its `_AlphaMaskScale` is 1. Its `_AlphaMaskValue` is about 0.80. Its `_Cutoff` is 0.001, which this family ignores. Its `_Color.a` is exactly 1. Dither is off, and no ID masks are set. The mask slot holds a 512 by 512 texture with identity scale and offset. The main texture slot holds a 1024 by 1024 texture.

The other refused slot holds a lilToon cutout family material, shader `Hidden/lilToonCutout`, at the cutout render queue 2450. Its `_AlphaMaskMode` is 2. Its `_AlphaMaskScale` is 1. Its `_AlphaMaskValue` is about 0.24. Its `_Cutoff` is about 0.13. Its `_Color.a` is exactly 1. Dither is off, and no ID masks are set. The mask and main texture slots hold assigned textures of the same sizes as the first slot.

Both materials keep the vendor default scale of 1 and dither of 0. Both depart from the vendor defaults only in the mask offset. The vendor default offset is 0. Both observed offsets sit strictly between 0 and 1.

## Vendor reachability

The installed vendor package in the Census Lab project declares the pair as the mask Scale and Offset inputs with defaults 1 and 0. See `BaseShaderResources/ltspass_baker.lilinternal:50-52` and the equivalent shader blocks, for example `Shader/lts_cutout.shader:117-119`. The vendor inspector registers both properties in the Main Color and Alpha Mask blocks. See `Editor/lilInspector/lilMaterialProperties.cs:119-121`. An author reaches a nonzero offset by entering it in the Alpha Mask section of the vendor inspector. Shipped products carry such values, and both refused materials hold them.

The installed vendor shader source matches the pinned equation line for line. See `Includes/lil_common_frag.hlsl:469-473` in the vendor package. The sampled mask red runs through `saturate(red * _AlphaMaskScale + _AlphaMaskValue)`, and mode 2 multiplies the main alpha by the result.

## Code trace

Both lilToon alpha families interpret the mask before their own texture arm. A mask refusal records one Alpha diagnostic and resolves the slot semantics as Unknown. See `Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonTransparentMaterialSemantics.cs:341-350` and `Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonCutoutMaterialSemantics.cs:309-319`.

`LilToonAlphaMaskTerm.Interpret` admits exactly three assigned-mask shapes, all pinned to lilToon 2.3.4. See `Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonAlphaMaskSemantics.cs`.

- The vendor default pair, scale 1 and value 0. The term is the sampled red, so the texture proof machinery owns it.
- The provably saturated pair, scale 1 and value at or above 1. The term is exactly 1 for every sampled red, so mode 2 leaves the main alpha unchanged and mode 1 gives the constant 1.
- The unassigned mask. The declared `white` default samples exactly 1, so the term is the constant `saturate(scale + value)` computed in binary32.

Every other scale and value pair falls into one final branch. The branch refuses with the `UnsupportedFeature` code and names `_AlphaMaskScale` when the scale is not 1, and `_AlphaMaskValue` otherwise. See `Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonAlphaMaskSemantics.cs:269-275`. Both refused materials hold scale 1 with a value between 0 and 1, so both hit the `_AlphaMaskValue` arm.

The refusal is the correct conservative answer. With an assigned mask and a value v in (0, 1), the per-texel term is `saturate(r + v)` with r in [0, 1], so the term fills the interval [v, 1]. Mode 2 multiplies the main alpha by that interval. Proving a triangle opaque needs the composed alpha to stay at or above the opacity bound at every sample. That proof needs two parts. One part is a per-texel predicate over the mask texture, composed with the main alpha envelope. The other part is a binary32 rounding argument for `r * scale + value` that covers fused and unfused evaluation alike. The code names this the deferred threshold-envelope contract. See `Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonAlphaMaskSemantics.cs:269-271`. No family implements that contract, so the frontend answers Unknown, the slot resolution refuses, and the report tells the author that AMUSE proved nothing.

A small offset does not make the term inert, not even when the offset sits above the cutout cutoff. The composed alpha is the product of the main alpha and the term, so cutout coverage still depends on per-texel values. Only the envelope contract can decide it.

## Build identity

The refusing build ran this code. On 2026-09-27, the Census Lab embedded package copy matched repository HEAD byte for byte on the three load-bearing files: `LilToonAlphaMaskSemantics.cs`, `AmuseReports.cs`, and `AmuseReportStrings.cs`.

## Finding

Cause established. Both in-scope slots hold an assigned mask with the vendor-default scale 1 and an offset strictly between 0 and 1. That pair falls into the final refusal branch of `LilToonAlphaMaskTerm.Interpret`, which names `_AlphaMaskValue`. The refusal is correct fail-closed behavior, that is, the safe answer when proof is missing. The support gap is the deferred threshold-envelope contract.

## What support requires

The branch must give the alpha mask term an envelope proof for the general assigned-mask pair. The shape:

1. Compute the mask red envelope over each triangle's sampled domain from the existing texture evidence.
2. Map that envelope through the monotone affine map `saturate(r * scale + value)`. A positive scale keeps the endpoint order. The interval math stays exact, in line with `TriangleAlphaClassifier` and `ExactUvGeometry`.
3. Compose the term envelope with the main alpha envelope, and run the family's existing coverage and opacity tests on the composed bounds.
4. State the binary32 rounding argument for `r * scale + value`, so no threshold comparison flips between fused and unfused evaluation.

The existing frontend tests pin today's refusal, and they must flip with the feature. For example, `Packages/com.alrauna.amuse/Tests/Editor/Semantics/LilToon/LilToonTransparentAlphaTests.cs:1069-1075` holds a scale 1 value 0.5 case with a uniform white mask and expects Unknown at `_AlphaMaskValue`. Under the envelope contract, that configuration proves, because the red envelope is exactly 1 and so the term is exactly 1. The branch writes the new expectations first, observes them fail on current code, and then implements the contract. New falsifier cases must cover the other direction: a mask with a dark texel must keep the composed proof from claiming opaque.

Narrower wins do not cover these two slots. Both slots hold real mask textures, so no default-texture shortcut applies. The scale variant, scale not 1, rides the same contract. Mode 3 and mode 4 stay refused by design. Poiyomi keeps its own admitted strength-value pairs and refuses the rest. See `Packages/com.alrauna.amuse/Editor/Semantics/Poiyomi/PoiyomiMaterialSemantics.cs:1451-1483`. Poiyomi parity is a separate consumer of the same contract family.

## Out of scope

The three other refusal warnings on the same avatar name other features. The `_AlphaMaskMode` refusals for modes 3 and 4 are deliberate. Poiyomi mask coverage is a separate branch. No follow-up record exists on 2026-09-27.
