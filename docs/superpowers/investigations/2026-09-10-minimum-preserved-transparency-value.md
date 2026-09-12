# Minimum Preserved Transparency Value: Current-State Characterization

Date: 2026-09-10. Base: `main` at `64a71a0`. Branch: `feat/min-preserved-transparency`.

Labels: `[SOURCE]` is a fact read in this repository. `[INFERENCE]` is a deduction. `[RECOMMENDATION]` is a proposed next step.

## 1. Purpose

The user requested a setting named "Minimum Preserved Transparency Value". The exact behavior is under clarification. This note records the state that the design must touch. It changes nothing.

## 2. Policy settings as of the base commit

`[SOURCE]` The runtime component `AmuseAvatarOptimizer` holds four serialized policy fields:

- `_preserveTransparencyMaxMipLevel`, default `4`, `-1` means every level.
- `_preserveTransparencyMinTextureSize`, default `128`, `-1` means every size.
- `_minimumOpaqueCoveragePercent`, default `25`, range `0` through `100`.
- `_ignoreOutOfRangeMaterialSlots`, default `false`.

`[SOURCE]` The inspector draws an "Alpha Separator" foldout with the mip dropdown, the size dropdown, and the coverage slider. Advanced Settings holds the out-of-range slots toggle.

`[SOURCE]` The platform finish plugin maps each stored value once, beside `ProofMipCapFrom`. The mappers are `MinTextureSizeFrom` and `MinimumCoverageFrom`. A negative stored value maps to the permissive extreme.

`[INFERENCE]` A new policy setting follows the same pattern: a serialized field with a doc comment, a public property, one mapping point in the plugin, and one control in the Alpha Separator foldout.

## 3. The capture threshold seam

`[SOURCE]` Evidence capture binarizes every texel against a `cutoffThreshold` float. The readers write `byte.MaxValue` when the sample passes and `0` otherwise:

- `SourceImageAlphaReader.cs:90`
- `UnityStreamingTextureEvidence.cs:196`
- `UnityGeneratedTextureEvidence.cs:166`

`[SOURCE]` The default threshold is `1.0f`. `UnityMaterialEvidenceCapture` stores `CutoutThreshold = 1.0f` per texture and takes the minimum cutoff across textures that share a texture slot. At `1.0f`, only a native `255` sample passes. Generated textures use an exact `== 255` test at `1.0f` and `>= threshold` below it.

`[SOURCE]` Shader frontends supply the material's own cutoff for cutout-style sources. The session caches key on the threshold value, so a changed value re-captures evidence.

`[INFERENCE]` The seam for a user policy value already exists. A blend or fade source captures with threshold `1.0` as of the base commit. A minimum preserved transparency value `V` replaces `1.0` with `V` for those sources. The classifier stays unchanged. The proof then reads "every consulted texel has alpha at or above `V`", and the canonical opaque material renders those triangles at full opacity.

`[SOURCE]` The classifier compares texels against `byte.MaxValue` in the two `AlphaTextureData` fast-path flags and in four support scans (`TriangleAlphaClassifier.cs` lines 181, 331, 406, 484, 699). Binarized input keeps those comparisons exact.

## 4. Candidate meanings

1. **Capture-threshold override.** Texels at or above `V` count as opaque evidence for sources without a shader cutoff. A stored `V` of full opacity reproduces the base behavior. Lowering `V` moves near-opaque triangles onto the canonical opaque material. They then render fully opaque instead of at their true alpha.
2. **Noise floor.** Texels below `V` are ignored entirely. Texels from `V` up to full opacity still disqualify a triangle. Only true full opacity proves opaque elsewhere. This tolerates compression noise near zero but leaves a uniformly faint texture unconverted.
3. **Mip-averaging tolerance.** Coarse levels count an averaged value just below full opacity as opaque. This conflicts with the exact-arithmetic discipline in the classifier plans. An area-averaged source capture already avoids the need for it.

`[RECOMMENDATION]` Meaning 1. It reuses an existing seam, it composes with the mip cap and the size cap, and a full-opacity default preserves every existing build.

## 5. Soundness boundaries

`[INFERENCE]` Cutout sources must keep the shader cutoff. A texel between `V` and the cutoff is discarded at runtime. Claiming it opaque would restore geometry that the author hid. This is the withdrawn-reasoning class documented in the render-state conversion note.

`[INFERENCE]` Lowering `V` relaxes the visual contract by user policy, like the mip cap relaxes consulted evidence by user policy. The setting is the opt-in. A full-opacity default changes no existing build.

## 6. Open design questions

Round 1 asks: the meaning of the value, the source-mode scope, the default, and whether the setting alone is the consent. Later rounds settle the stored type, the inspector control, the boundary comparison, and the report vocabulary for policy-proven triangles.

## 7. Next steps

Settle the open questions in the grilling session. Then present the design, write the spec at `docs/superpowers/specs/`, and plan with RED/GREEN tasks.

## 8. Stray dilution characterization

Added after the grilling rounds on 2026-09-10. This table is the basis for the masked-averaging requirement in the design.

Setup: a 2048 by 2048 alpha field, every texel at 255 except one stray texel at alpha 3, and a polygon whose footprint covers the stray. The capture averages the source per level and clamps at the threshold.

| Level | Region average | Unmasked verdict |
|---|---|---|
| 0 | 3 | witness |
| 1 (2 by 2) | about 204.5 | witness, and no sane opaque bound forgives it |
| 3 (8 by 8) | about 251.9 | only an opaque bound at or below about 98 percent forgives it |
| 5 (32 by 32) | about 254.9 | a 99.7 percent bound forgives it |

`[INFERENCE]` Three consequences settled the design:

1. Erasure must substitute the noise texel as opaque before chain averaging. Erasing only the finest-level witness fails at the first coarser level forever.
2. Substitution is consented exactly when noise texels are sparse. A density guard separates isolated strays from structured faint content.
3. Uniform quantization noise is the opaque bound's territory. Isolated strays are the gate's territory. No use case remains for a mip-averaging tolerance.
