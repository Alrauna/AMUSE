# lilToon Attestation Hardening Design

**Date:** 2026-08-21

**Status:** Approved for implementation, including the review refinements recorded below

**Branch:** `fix/liltoon-attestation-hardening` from `origin/main` at `36f8e588d8d3e540cb510e938e66673bae160b25`

## Decision

Keep the existing standalone lilToon 2.3.4 identity pins and R1/R2/R3 canonicalization output unchanged. Add a second, pre-canonicalization evidence layer that records every R1 region and every third-party integration activator occurrence from the raw generated sources. The standalone verifier accepts only the closed language that the official 2.3.4 generator can emit at the exact supported source locations, with all three external activators absent.

The implementation has one recognizer for R1. It returns both:

1. the same canonical text `Canonicalize` returns today; and
2. an immutable provenance record describing what R1 removed and where it found it.

`Canonicalize` remains as the existing test seam and delegates to that analysis. `GatherSourceEvidence` retains the analysis records for both the material shader and the opaque pass. `TryVerifyLilToonIdentity` validates those records before a matching canonical digest can authorize semantic interpretation.

This is a fixed standalone profile check, not a registry or general preprocessor. A future exact LTCGI, AudioLink, or external VRC Light Volumes profile can consume the same raw record and add its own characterized activation/source-closure predicate. This branch accepts none of them.

## Problem and root cause

R1 currently recognizes a broad syntactic language:

- a valueless `LIL_FEATURE_*` define;
- a valueless `LIL_OPTIMIZE_*` define;
- `LIL_INPUT_OPTIMIZED`; or
- any non-empty `#pragma skip_variants` line;

when it appears in the maximal matching run immediately after an exact `HLSLINCLUDE` line.

The recognizer then discards the matching lines. The resulting digest proves the retained canonical remainder, but no evidence proves the provenance of the erased lines.

That creates a false-positive channel. For example, placing any of these in the pass's setting run leaves the existing standalone pass digest unchanged:

```hlsl
#define LIL_FEATURE_VRCLIGHTVOLUMES
#define LIL_FEATURE_AUDIOLINK_PACKAGE
#define LIL_FEATURE_LTCGI
#define LIL_FEATURE_AMUSE_UNKNOWN
#define LIL_OPTIMIZE_AMUSE_UNKNOWN
```

The first three can activate external package source. The last two demonstrate the general defect: a modified generator can hide an unknown token in the erased region without changing the accepted digest. The existing unordered `CompiledFeatures` set does not repair this because it is not an identity conjunct and loses count and position.

The root cause is therefore not a bad digest pin and not R1 canonicalization itself. It is the missing proof that every erased record belongs to the closed official generator-emittable language for the selected profile.

### Meaning and limit of provenance

“Provenance” in this design is a membership proof, not a historical reconstruction. Layer 2 proves that the erased variation belongs to the closed official lilToon 2.3.4 generator-emittable language and satisfies the selected profile's trust constraints. It does not prove that a particular live generator invocation historically produced the source, nor does it reconstruct every compile symbol, Unity setting, or environment input that may have existed at generation time.

The security property is narrower and stronger than that historical claim: every accepted erased record is bounded to characterized official output, every external activator satisfies the selected profile, and all existing source, include-tree, digest, identity, and semantic evidence still matches. All of those checks remain conjunctive.

## Scope

This design hardens only the current canonical upstream lilToon 2.3.4 BRP base-opaque standalone profile:

- shader `lilToon`, GUID `df12117ecd77c31469c224178886498e`;
- opaque pass `Hidden/ltspass_opaque`, GUID `61b4f98a5d78b4a4a9d89180fac793fc`;
- package `jp.lilxyzw.liltoon` version `2.3.4` when package evidence exists;
- material shader-format stamp exactly `45f`;
- `LIL_RENDER` exactly `0`;
- the existing base, pass, and include-tree pins, unchanged.

No integrated profile, external source closure, NDMF ordering rule, shader-family expansion, semantic equation, census behavior, or package dependency is added.

## Considered approaches

### 1. Structured extraction plus fixed validation — selected

Have the existing R1 walk produce canonical text and an ordered raw record together. Validate the record against the exact official 2.3.4 grammar and the standalone activation rule.

This preserves the pins, prevents parser drift, retains count/location evidence, and leaves a concrete activation record reusable by later official integration profiles.

### 2. Pin raw removed-region digests — rejected

Pinning the default and stripped raw runs would close those two witnesses but refuse the many other legitimate combinations of lilToon settings that R1 intentionally supports. Enumerating every combination would be unbounded and would turn ordinary settings into profile proliferation.

### 3. Add an independent activator scanner only — rejected

A scanner for only the three known activators would close the known package channels but still accept unknown `LIL_FEATURE_*`, `LIL_OPTIMIZE_*`, and `skip_variants` records erased by R1. A separate scanner would also duplicate the R1 boundary logic and could drift from canonicalization.

## Layer-2 representation

The existing source-attestation file gains small immutable evidence values; names may be adjusted during implementation, but their information content is fixed by this design.

```csharp
internal enum LilToonRemovedRecordKind
{
    Define,
    SkipVariants,
}

internal readonly struct LilToonRemovedRecord
{
    internal int LineIndex { get; }
    internal int OffsetInRegion { get; }
    internal LilToonRemovedRecordKind Kind { get; }
    internal string Text { get; }
}

internal sealed class LilToonRemovedRegion
{
    internal int HlslIncludeOrdinal { get; }
    internal int HlslIncludeLineIndex { get; }
    internal IReadOnlyList<LilToonRemovedRecord> Records { get; }
}

internal readonly struct LilToonActivatorOccurrence
{
    internal int LineIndex { get; }
    internal string Identifier { get; }
    internal string Text { get; }
}

internal sealed class LilToonCanonicalizationAnalysis
{
    internal string CanonicalSource { get; }
    internal IReadOnlyList<LilToonRemovedRegion> RemovedRegions { get; }
    internal IReadOnlyList<LilToonActivatorOccurrence> Activators { get; }
}
```

Line indices are zero-based indices into the normalized raw source. `Text` is the trimmed raw line. The original indentation is not trust-relevant, but internal whitespace and trailing tokens are: official generation emits exact trimmed forms. Every exact `HLSLINCLUDE` produces a region record even if its R1 run is empty. This preserves the distinction between the pass's empty shader-scope block and its populated SubShader setting block.

Every collection property is backed by a private defensive copy wrapped in a genuinely read-only collection. No caller-owned list is retained, and no backing array is exposed through an interface that a caller can cast back to an array and mutate.

The activation scan records define directives for these exact identifiers anywhere in either raw generated source, including valued or otherwise unexpected forms:

- `LIL_FEATURE_VRCLIGHTVOLUMES`
- `LIL_FEATURE_AUDIOLINK_PACKAGE`
- `LIL_FEATURE_LTCGI`

It ignores comments and ordinary identifier mentions. Canonical digest evidence continues to reject other malformed or executable text outside R1.

`LilToonSourceEvidence` carries one analysis record for the material shader and one for the pass shader, in addition to its existing fields. A missing analysis is missing source evidence. `CompiledFeatures` remains unchanged for output-local Emission and Normal interpretation.

## One R1 source of truth

Introduce an internal analysis method with the current canonicalization inputs:

```csharp
internal static LilToonCanonicalizationAnalysis AnalyzeCanonicalization(
    string rawShaderSource,
    string shaderDirectory,
    string projectRoot,
    LilToonIncludeTree includeTree);
```

It performs the current normalization and R1 region marking once. While emitting, it records every R1 region and record, applies the existing R1 removal, applies R2 exactly as today, and applies R3 exactly as today.

The existing method remains:

```csharp
internal static string Canonicalize(
    string rawShaderSource,
    string shaderDirectory,
    string projectRoot,
    LilToonIncludeTree includeTree)
{
    return AnalyzeCanonicalization(
        rawShaderSource, shaderDirectory, projectRoot, includeTree)
        .CanonicalSource;
}
```

This is a behavioral-preservation requirement, not an opportunity to rewrite R1/R2/R3. Existing canonicalization tests and all three digest constants remain unchanged.

Provenance validation is deliberately separate from canonicalization. Invalid raw evidence still produces the same canonical text it produced before, which permits a regression test to prove that the old digest alone would accept the witness. Identity verification then refuses the associated provenance.

## Closed official 2.3.4 generator domain

The closed domain comes from the pinned upstream `BuildShaderSettingString(shaderSetting, isFile: false)`, `BuildShaderSettingStringMulti`, and the pinned `UnpackContainer` skip-variant replacement/deduplication. It is not defined by the `LIL_FEATURE_*` or `LIL_OPTIMIZE_*` prefixes.

### Exact define identifiers

The generator can place exactly 109 define identifiers in the R1 setting run: the following 100 feature-setting identifiers, five optimizer identifiers, three package/bundled-integration identifiers, and `LIL_INPUT_OPTIMIZED`.

The 100 feature-setting identifiers, in generator order, are:

```text
LIL_FEATURE_ANIMATE_MAIN_UV
LIL_FEATURE_MAIN_TONE_CORRECTION
LIL_FEATURE_MAIN_GRADATION_MAP
LIL_FEATURE_MAIN2ND
LIL_FEATURE_MAIN3RD
LIL_FEATURE_DECAL
LIL_FEATURE_ANIMATE_DECAL
LIL_FEATURE_LAYER_DISSOLVE
LIL_FEATURE_ALPHAMASK
LIL_FEATURE_SHADOW
LIL_FEATURE_RECEIVE_SHADOW
LIL_FEATURE_SHADOW_3RD
LIL_FEATURE_SHADOW_LUT
LIL_FEATURE_RIMSHADE
LIL_FEATURE_EMISSION_1ST
LIL_FEATURE_EMISSION_2ND
LIL_FEATURE_ANIMATE_EMISSION_UV
LIL_FEATURE_ANIMATE_EMISSION_MASK_UV
LIL_FEATURE_EMISSION_GRADATION
LIL_FEATURE_NORMAL_1ST
LIL_FEATURE_NORMAL_2ND
LIL_FEATURE_ANISOTROPY
LIL_FEATURE_REFLECTION
LIL_FEATURE_MATCAP
LIL_FEATURE_MATCAP_2ND
LIL_FEATURE_RIMLIGHT
LIL_FEATURE_RIMLIGHT_DIRECTION
LIL_FEATURE_GLITTER
LIL_FEATURE_BACKLIGHT
LIL_FEATURE_PARALLAX
LIL_FEATURE_POM
LIL_FEATURE_CLIPPING_CANCELLER
LIL_FEATURE_DISTANCE_FADE
LIL_FEATURE_AUDIOLINK
LIL_FEATURE_AUDIOLINK_VERTEX
LIL_FEATURE_AUDIOLINK_LOCAL
LIL_FEATURE_DISSOLVE
LIL_FEATURE_DITHER
LIL_FEATURE_IDMASK
LIL_FEATURE_UDIMDISCARD
LIL_FEATURE_OUTLINE_TONE_CORRECTION
LIL_FEATURE_OUTLINE_RECEIVE_SHADOW
LIL_FEATURE_ANIMATE_OUTLINE_UV
LIL_FEATURE_FUR_COLLISION
LIL_FEATURE_MainGradationTex
LIL_FEATURE_MainColorAdjustMask
LIL_FEATURE_Main2ndTex
LIL_FEATURE_Main2ndBlendMask
LIL_FEATURE_Main2ndDissolveMask
LIL_FEATURE_Main2ndDissolveNoiseMask
LIL_FEATURE_Main3rdTex
LIL_FEATURE_Main3rdBlendMask
LIL_FEATURE_Main3rdDissolveMask
LIL_FEATURE_Main3rdDissolveNoiseMask
LIL_FEATURE_AlphaMask
LIL_FEATURE_BumpMap
LIL_FEATURE_Bump2ndMap
LIL_FEATURE_Bump2ndScaleMask
LIL_FEATURE_AnisotropyTangentMap
LIL_FEATURE_AnisotropyScaleMask
LIL_FEATURE_AnisotropyShiftNoiseMask
LIL_FEATURE_ShadowBorderMask
LIL_FEATURE_ShadowBlurMask
LIL_FEATURE_ShadowStrengthMask
LIL_FEATURE_ShadowColorTex
LIL_FEATURE_Shadow2ndColorTex
LIL_FEATURE_Shadow3rdColorTex
LIL_FEATURE_RimShadeMask
LIL_FEATURE_BacklightColorTex
LIL_FEATURE_SmoothnessTex
LIL_FEATURE_MetallicGlossMap
LIL_FEATURE_ReflectionColorTex
LIL_FEATURE_ReflectionCubeTex
LIL_FEATURE_MatCapTex
LIL_FEATURE_MatCapBlendMask
LIL_FEATURE_MatCapBumpMap
LIL_FEATURE_MatCap2ndTex
LIL_FEATURE_MatCap2ndBlendMask
LIL_FEATURE_MatCap2ndBumpMap
LIL_FEATURE_RimColorTex
LIL_FEATURE_GlitterColorTex
LIL_FEATURE_GlitterShapeTex
LIL_FEATURE_EmissionMap
LIL_FEATURE_EmissionBlendMask
LIL_FEATURE_EmissionGradTex
LIL_FEATURE_Emission2ndMap
LIL_FEATURE_Emission2ndBlendMask
LIL_FEATURE_Emission2ndGradTex
LIL_FEATURE_ParallaxMap
LIL_FEATURE_AudioLinkMask
LIL_FEATURE_AudioLinkLocalMap
LIL_FEATURE_DissolveMask
LIL_FEATURE_DissolveNoiseMask
LIL_FEATURE_OutlineTex
LIL_FEATURE_OutlineWidthMask
LIL_FEATURE_OutlineVectorTex
LIL_FEATURE_FurNoiseMask
LIL_FEATURE_FurMask
LIL_FEATURE_FurLengthMask
LIL_FEATURE_FurVectorTex
```

The five optimizer identifiers, in generator order, are:

```text
LIL_OPTIMIZE_APPLY_SHADOW_FA
LIL_OPTIMIZE_USE_FORWARDADD
LIL_OPTIMIZE_USE_FORWARDADD_SHADOW
LIL_OPTIMIZE_USE_VERTEXLIGHT
LIL_OPTIMIZE_USE_LIGHTMAP
```

The three compile/package-dependent identifiers occupying the next generator slot are:

```text
LIL_FEATURE_VRCLIGHTVOLUMES
LIL_FEATURE_VRCLIGHTVOLUMES_WITHOUTPACKAGE
LIL_FEATURE_AUDIOLINK_PACKAGE
```

The two Light Volumes forms are mutually exclusive in official generation. The first and third activate external package source and are forbidden by the standalone profile. `LIL_FEATURE_VRCLIGHTVOLUMES_WITHOUTPACKAGE` selects source already covered by the lilToon include-tree digest and remains a legitimate standalone generator variation.

`LIL_INPUT_OPTIMIZED` is the only define that can follow the skip-variant records. `LIL_FEATURE_LTCGI` is intentionally absent from the 109-identifier R1 domain: official 2.3.4 emits it outside R1 at two forward-program positions. Any R1 occurrence is therefore both an unknown generator record and a forbidden standalone activator.

### Exact skip-variant records

After substitution and the official de-duplication pass, R1 can contain only these exact trimmed lines, in this order when present:

```text
#pragma skip_variants _REFLECTION_PROBE_BOX_PROJECTION
#pragma skip_variants LIGHTPROBE_SH
#pragma skip_variants _MIXED_LIGHTING_SUBTRACTIVE
```

They correspond respectively to reflection disabled, vertex-light optimization disabled, and lightmap optimization disabled. Multi-keyword lines, different keywords, reordered lines, duplicates, or different whitespace forms are not official final output and refuse even though current R1 would erase them.

### Structural grammar

The standalone setting record must satisfy all of these rules:

1. The material shader contains no R1 regions and no activator occurrence.
2. The pass contains exactly two `HLSLINCLUDE` regions. Region 0 is empty because its next line is the valued `#define LIL_RENDER 0`. Region 1 contains the setting record.
3. Every region-1 define has the exact trimmed form `#define <identifier>` and follows the fixed generator order above. No identifier appears twice.
4. `LIL_FEATURE_Main2ndDissolveNoiseMask`, `LIL_FEATURE_Main3rdDissolveNoiseMask`, and `LIL_FEATURE_DissolveNoiseMask` are always present; upstream emits them unconditionally.
5. `LIL_FEATURE_DECAL`, `LIL_FEATURE_ANIMATE_DECAL`, and `LIL_FEATURE_LAYER_DISSOLVE` require `LIL_FEATURE_MAIN2ND` or `LIL_FEATURE_MAIN3RD`.
6. `LIL_FEATURE_RECEIVE_SHADOW`, `LIL_FEATURE_SHADOW_3RD`, and `LIL_FEATURE_SHADOW_LUT` require `LIL_FEATURE_SHADOW`.
7. `LIL_FEATURE_ANIMATE_EMISSION_UV`, `LIL_FEATURE_ANIMATE_EMISSION_MASK_UV`, and `LIL_FEATURE_EMISSION_GRADATION` require `LIL_FEATURE_EMISSION_1ST` or `LIL_FEATURE_EMISSION_2ND`.
8. `LIL_FEATURE_RIMLIGHT_DIRECTION` requires `LIL_FEATURE_RIMLIGHT`; `LIL_FEATURE_POM` requires `LIL_FEATURE_PARALLAX`; and `LIL_FEATURE_AUDIOLINK_VERTEX` and `LIL_FEATURE_AUDIOLINK_LOCAL` require `LIL_FEATURE_AUDIOLINK`.
9. The two Light Volumes identifiers are mutually exclusive. The standalone profile additionally forbids the external form and `LIL_FEATURE_AUDIOLINK_PACKAGE`.
10. The three exact skip-variant records follow all generator defines, in their fixed order. Their presence is the exact inverse of `LIL_FEATURE_REFLECTION`, `LIL_OPTIMIZE_USE_VERTEXLIGHT`, and `LIL_OPTIMIZE_USE_LIGHTMAP`, respectively.
11. Optional `LIL_INPUT_OPTIMIZED` is last.
12. No other record, empty bridge, malformed directive, duplicate, reordering, or additional token is accepted.

These checks characterize membership in the generator-emittable language without reconstructing HLSL preprocessing, lilToon's entire setting object, or the historical generator environment.

## Standalone activation invariant

The exact acceptance property is:

> A source pair is eligible for the standalone lilToon 2.3.4 profile only when the base source has no R1-removed record, the pass has the exact two-region structure above, the pass setting record is a valid official 2.3.4 generator record, and neither raw source defines `LIL_FEATURE_VRCLIGHTVOLUMES`, `LIL_FEATURE_AUDIOLINK_PACKAGE`, or `LIL_FEATURE_LTCGI` anywhere. It must then satisfy every existing name, GUID, version, package, include-tree, canonical digest, `LIL_RENDER`, and output-local semantic check.

The checks are conjunctive. A valid provenance record cannot compensate for a digest mismatch, and matching canonical pins cannot compensate for invalid provenance.

Standalone profile validation runs after the cheap shader/version/package/pass identity checks and before digest acceptance and semantic interpretation. Failure behavior is:

- missing analysis record: `MissingSourceEvidence`;
- any of the three known external activators: `UnsupportedShaderVariant`, naming the identifier;
- unknown, malformed, duplicated, relocated, out-of-order, or structurally impossible R1 evidence: `ModifiedShaderSource`, naming canonicalization provenance for the relevant source.

No diagnostic enum or logging subsystem is added.

## Future official integration profiles

The raw evidence shape does not encode "all integrations absent". It records exact activator identifiers, counts, line positions, and removed-region positions. Only the standalone validator imposes absence.

A later characterized profile can reuse the same evidence by adding a concrete predicate such as:

- LTCGI-only: exact base tag/digest, exactly two forward-position `LIL_FEATURE_LTCGI` occurrences, no R1 LTCGI occurrence, and AudioLink-package/external-Light-Volumes absent;
- AudioLink: exact official R1 AudioLink-package occurrence at the generator slot and its attested external source/provenance closure;
- external VRC Light Volumes: exact official R1 occurrence at the generator slot and its attested external source/provenance closure;
- a characterized combination: the exact union of those activation facts plus every required external closure and earlier macro input.

Those validators and source closures are not implemented now. No enum of future profiles, provider interface, registry, plugin API, or generalized dependency manager is introduced.

## Data flow

```text
raw lts.shader ── AnalyzeCanonicalization ── canonical text ── SHA-256
       │                    └─────────────── provenance record
       │
raw opaque pass ─ AnalyzeCanonicalization ─ canonical text ── SHA-256
                            └─────────────── provenance + activators

identity/version/package/pass checks
              ↓
standalone provenance predicate
              ↓
existing include/base/pass pins + LIL_RENDER
              ↓
existing output-local semantic interpretation
```

## Test design

All production behavior is test-driven. Tests use synthetic public strings and exact token records; no upstream shader file or private fixture is copied into the repository.

### Positive characterization

- the exact 103-record default standalone setting sequence, independently confirmed against genuine official 2.3.4 output on 2026-08-21, is accepted;
- the exact 91-record stripped-settings sequence is accepted, including the corresponding existing R2 shadow-slot variation;
- targeted positive witnesses cover default `LIL_FEATURE_PARALLAX` with its dependent `LIL_FEATURE_POM`, plus legitimate standalone records not exercised by the default state: `LIL_FEATURE_CLIPPING_CANCELLER` and `LIL_OPTIMIZE_USE_FORWARDADD_SHADOW`, all in exact generator order;
- a legal `LIL_OPTIMIZE_USE_LIGHTMAP` state is accepted only with its exact inverse skip-variant behavior: enabling it removes `#pragma skip_variants _MIXED_LIGHTING_SUBTRACTIVE`, while disabling it requires that pragma;
- additional legitimate official subsets exercise `LIL_INPUT_OPTIMIZED` and bundled `LIL_FEATURE_VRCLIGHTVOLUMES_WITHOUTPACKAGE`;
- canonical output for existing R1/R2/R3 cases remains byte-identical;
- all three digest pin constants remain byte-identical;
- the existing BaseColor, Alpha, Emission, and Normal suites remain green.

The 91-record sequence differs from default by the characterized setting changes: first/second Normal, first BumpMap source, first/second Emission, first EmissionMap source, shadow reception, outline shadow reception, and backlight are disabled; the three nested emission-control defines consequently disappear. The count falls by twelve. R2 separately removes the generated `SHADOW_VERY_HIGH` slot line. The genuine-package end-to-end witness corrected the earlier 102/90 count characterization to 103/91 because official default and stripped output both contain `LIL_FEATURE_POM`; the grammar and digest pins did not change.

### Negative characterization

- each of `LIL_FEATURE_VRCLIGHTVOLUMES`, `LIL_FEATURE_AUDIOLINK_PACKAGE`, and `LIL_FEATURE_LTCGI` hidden in region 1 refuses;
- a known external activator duplicated or moved to a different position in region 1 refuses;
- an activator elsewhere in either raw source refuses, independent of the canonical digest result;
- unknown `LIL_FEATURE_AMUSE_UNKNOWN` and `LIL_OPTIMIZE_AMUSE_UNKNOWN` records refuse;
- an unknown or multi-keyword `skip_variants` record refuses;
- a known internal token duplicated, reordered, placed in region 0, or given non-generator whitespace refuses;
- each structural implication and each mandatory unconditional token is covered by a focused refusal test;
- a missing provenance record refuses as missing source evidence.

At least one test for every hidden known/unknown witness first asserts:

```csharp
Assert.That(mutated.CanonicalSource, Is.EqualTo(clean.CanonicalSource));
```

and then proves that the mutated provenance causes `TryVerifyLilToonIdentity` to return false while all existing pin fields remain valid. This explicitly demonstrates the old false-positive hole rather than merely testing a new helper in isolation.

Existing wrong-version, wrong-identity/GUID, modified canonical source, modified include tree, redirected include, derivative/custom shader, differently named variant, and unknown-shader tests remain unchanged.

## Expected implementation files

Production implementation is expected to modify only:

- `Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonSourceAttestation.cs`
- `Packages/com.alrauna.amuse/Tests/Editor/Semantics/LilToon/LilToonAttestationTests.cs`

No new Unity asset is required, so no `.meta` change is expected. `LilToonMaterialSemantics.cs`, `UnityMaterialSemantics.cs`, package metadata, census code, and semantic tests should remain untouched unless implementation reveals a contradiction and returns to review.

## Validation

Implementation validation will require:

1. focused `LilToonAttestationTests` red/green cycles;
2. the complete lilToon semantic test set;
3. the complete public EditMode suite;
4. inspection of unstaged and staged diffs;
5. confirmation that the three existing pins are unchanged;
6. confirmation that no host-specific Unity toolchain/sysroot churn is included.

Before any Unity result is reported, the public instance must be selected by exact normalized `Application.dataPath == <repo-root>/Assets`. The Census Lab is not required and must not be modified.

## Risks and explicit assumptions

- The 109-identifier domain and grouping rules are pinned to official lilToon 2.3.4. A later upstream version must be separately characterized.
- The setting record validation intentionally produces false negatives for modified generators, even when their output would be semantically harmless.
- Exact trimmed directive forms are part of provenance. A third-party writer that reformats an otherwise equivalent line is not official generator output and refuses.
- Existing filesystem atomicity/TOCTOU assumptions are unchanged. This branch does not invent NDMF snapshot ordering.
- SHA-256 collision resistance remains an existing cryptographic assumption.

## Evidence anchors

- `docs/superpowers/specs/2026-08-21-liltoon-attestation-investigation-design.md`
- `docs/superpowers/specs/2026-08-17-liltoon-semantics-adapter-design.md`
- `docs/superpowers/plans/2026-08-17-liltoon-semantics-adapter.md`
- `Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonSourceAttestation.cs`
- `Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonMaterialSemantics.cs`
- `Packages/com.alrauna.amuse/Editor/Semantics/UnityMaterialSemantics.cs`
- `Packages/com.alrauna.amuse/Tests/Editor/Semantics/LilToon/LilToonAttestationTests.cs`
- official lilToon 2.3.4 setting generator at commit `252fd8cfc46106d4967e95b3f2c788418502f227`
- official lilToon 2.3.4 container importer at the same commit

## Out of scope

- positive LTCGI, AudioLink package, or external VRC Light Volumes support;
- external integration source-closure characterization;
- arbitrary custom shader or VRCFury derivative support;
- a generic integration/profile/provider framework;
- a general HLSL parser or preprocessor;
- NDMF pass ordering or an AMUSE NDMF plugin;
- census schema, aggregation, or runner changes;
- Poiyomi, MissingTextureEvidence, or unrelated cleanup;
- changing canonical pins or weakening existing refusals.
