# lilToon TwoPass transparent: conversion refusal and the missing FORWARD_BACK proof

Privacy note: This record characterizes one garment material from the Census Lab
corpus. It names no avatar, renderer, material, texture, hierarchy, or asset
identifier. It records no per-renderer table. Texture facts stay coarse. Every
status claim is dated.

Date: 2026-09-17. Status: stage 0 investigation record. No production code
changed.

Labels: `[SOURCE]` marks a pinned-source or shipped-code fact. `[MEASURED]`
marks a value read from the material asset, the vendor package, or an offline
file decode on this date. `[INFERENCE]` marks a bounded conclusion. `[DECISION
NEEDED]` marks an unresolved product choice.

Method: read of production source on this branch, read of the vendor package
installed in the Census Lab project (lilToon 2.3.4), and offline reads of the
material asset and its assigned mask source file. No Unity run happened. Only
the dev editor instance was available on this date. The Census Lab editor
instance was not running, so no live analysis observation exists. Every
pipeline claim is a source-read result.

## 1. The material

The material is an official lilToon 2.3.4 TwoPass transparent garment
material. Its shader is `Hidden/lilToonTwoPassTransparent` at the default
queue 2460 with `RenderType` `TransparentCutout`. `[MEASURED]`

The identity matches the AMUSE pins. The pinned name, GUID, and canonical
digest in `LilToonSourceAttestation` describe exactly this asset, and the
installed vendor tree is the pinned 2.3.4. `[SOURCE]`

The alpha chain, from vendor source and the material values:

1. Both main texture layers are off. `[MEASURED]`
2. The alpha mask is active in replace mode (`_AlphaMaskMode == 1`) with an
   assigned mask texture and the admitted default pair (scale 1, value 0).
   The fragment alpha becomes the sampled mask red channel
   (`lil_common_frag.hlsl:464-475`). `[SOURCE` + `[MEASURED]`
3. The forward clip is `clip(alpha - _Cutoff)` with `_Cutoff == 0.001`. The
   shadow subpass clips at `_SubpassCutoff == 0.5`. Layer dissolve is off.
   Dither is compiled out for LIL_RENDER 2. The distance fade strength
   component is zero. `[MEASURED]` + `[SOURCE]`

The render state that decides eligibility:

| Fact | Value | Note |
|---|---|---|
| `_ZTest` | 2 (Less) | not the canonical 4 (LEqual) |
| `_ZWrite` | 1 | canonical |
| `_Cull` | 0 (Off) | double sided, deliberately ungated |
| `_PreCull` | 1 (Front) | pre-pass draws back faces |
| `_PreZWrite` | 1 | pre-pass writes depth |
| `_PreZTest` | 4 | canonical |
| `_PreOutType` | 0 | normal pre-pass output arm |
| `_PreColor` | white | no pre-pass tint |
| `_PreCutoff` | 0.001 | pre-pass clip |

## 2. What AMUSE does with this material

Attestation and family selection succeed. The two-pass profile is in the
pinned profile list. Family selection maps the exact shader name to
`CapturedAlphaMaterialFamily.LilToonTransparent`. `[SOURCE]`

Conversion eligibility refuses at gate 5. `EvaluateVerifiedEligibility` reads
`_ZTest == 2` and returns `UnsupportedDepthComparison`. `[SOURCE]`

Every later gate passes against the captured values: depth write, color mask,
depth offset, both base blend equations, the ForwardAdd blend, the clip
threshold, the ForwardAdd boost, distance fade, and the subpass cutoff. Gate 5
is the only refusal. `[MEASURED]` walk on this date

Outcome: the material keeps every triangle. There is no separation. The
refusal is named and safe. No live build log observation exists for this
session.

## 3. Finding A: the refusal is sound, and it is a family-wide coverage wall

The vendor mode converter skips the `_ZTest` normalization exactly and only
for TwoPass. The common tail writes `_ZTest` 4 inside
`if(transparentMode != TransparentMode.TwoPass)`
(`lilMaterialUtils.cs:277-280`). `[SOURCE]`

So `_ZTest != 4` is a normal state for a vendor-converted TwoPass material.

On this material the value is load bearing. The forward pass runs with
`Cull Off`. The pre-pass draws back faces first and writes depth. A back face
drawn by the forward pass sits at exactly the depth the pre-pass wrote.
`ZTest Less` discards that fragment. `ZTest LEqual` would keep it and blend it
a second time. `[INFERENCE]` from the pinned pass state, not a rendered
measurement

Admitting `_ZTest == 2` needs a proof about exact depth equality between
coplanar surfaces. That is per-pixel-neighborhood work, not a gate edit. The
refusal stands for now. The cost is that this whole material configuration
stays unoptimized. This is the conflict F0 section 7.6 predicted.
`[DECISION NEEDED]` for any future narrowing

## 4. Finding B: the FORWARD_BACK proof gap

This is the load-bearing finding.

The S9 roadmap row requires that the FORWARD_BACK pre-pass enters the
all-passes-proven rule, with a RED/GREEN where a TwoPass material with an
unproven pre-pass keeps every triangle (`2026-09-05-0.1.0-implementation-roadmap.md`,
slice S9). `[SOURCE]`

PR 63 shipped the four wrapper attestation pins, family selection, and
near-miss test hygiene. Its file list contains no eligibility, semantics, or
capture change. `[SOURCE]` PR body

As of 2026-09-17 no production file reads any `_Pre*` property. A search over
`Editor/` for the pre-pass property names finds only an attestation comment.
The transparent evidence request is the cutout schema plus three
transparent-only facts (`_AlphaBoostFA`, `_SubpassCutoff`, `_DistanceFade`).
`[MEASURED]` search

Consequences:

- A TwoPass material whose pre-pass equation does not degenerate still
  classifies from the forward equation alone. If `_PreColor.a` is below one,
  or `_PreOutType` is nonzero, or the `_Pre` blend factors are not unit and
  zero at alpha one, a moved triangle rendered differently in the pre-pass.
  The product moves it anyway. `[INFERENCE]` from source reads
- `_Pre*` properties are absent from the evidence request. An animated
  `_Pre*` property is invisible to the animation closure on an admitted
  family. `[INFERENCE]` from source reads

This material happens to be benign. Its `_Pre*` values make the pre-pass
equation degenerate to the forward equation. Production never checks this. The
benign state is an accident of the material, not an enforced precondition.
`[MEASURED]` values, `[INFERENCE]` gap

This is the documented S9 requirement, not a new idea. The missing half is a
correctness gap inside an admitted family, not a coverage wish.

## 5. The mask domain

The assigned mask is a large square 16-bit grayscale source image. The
importer marks it sRGB and compressed. The shader samples the red channel
through the main sampler. Only a stored maximum decodes to exactly one.
`[SOURCE]` importer, `[MEASURED]` file header on this date

An offline decode of the source file gives a bimodal red channel. About half
the texels sit at the stored maximum. About a third sit at zero. A narrow soft
band of a few percent sits between. `[MEASURED]` offline decode. GPU texels
may differ within compression error.

A proven opaque triangle needs every sampled mip texel at the stored maximum
over its domain. That is the shipped whole-domain rule. The maximum region is
large. The zero region is large. Many triangles straddle both. So a real but
partial provable fraction exists once findings A and B are settled.
`[INFERENCE]`

## 6. Next actions

1. Complete the S9 second half. Design and implement the FORWARD_BACK pass
   proof. Gate `_PreColor`, `_PreCutoff`, `_PreOutType`, and the `_Pre` blend
   and depth state in the transparent eligibility and semantics. Add `_Pre*`
   to the evidence request and the animation closure. RED first: a TwoPass
   material with a translucent `_PreColor` keeps every triangle. Add
   falsifiers for each `_Pre` arm.
2. Decide the `_ZTest` policy for TwoPass. Either keep the refusal and
   document the family-wide consequence, or commission the coplanar depth
   equality characterization. `[DECISION NEEDED]`
3. After item 1, run a fresh read-only Census Lab characterization of this
   material. Record observed classification counts and the end-to-end refusal
   line.

Branch: `investigation/liltoon-two-pass-transparent`. This note is
uncommitted. Git authorization stays with the product owner.
