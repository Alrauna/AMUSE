# Poiyomi Conversion Gate Safety: Design and Specification

Date: 2026-10-09.
Branch plan: fix/latent-bugs-and-impurities. Base: fae7e7e.
No source assets are modified.
Vendor shaders are never installed. Tests use the `Hidden/Alrauna/AmuseTests/*` stand-ins and the verified seams in `Tests/Editor/`.

Privacy note: this document contains no private avatar, renderer, material, scene, or machine identity. Public vendor shader identifiers stay exact.

Labels: `[SOURCE]` is a fact read from cited code lines in this repository.
`[INFERENCE]` is a conclusion from evidence.
Findings come from `docs/superpowers/investigations/2026-10-09-third-latent-bugs-and-architectural-impurities-investigation.md`.

---

## 1. Summary

This design fixes five defects and one cost impurity in the Poiyomi conversion and alpha paths.
The finiteness sweep moves ahead of the already-opaque classification, so a NaN cutoff can no longer admit a material as already opaque.
The canonical outline blend tuple writes become gated, so conversion refuses outline pairs that do not degenerate to identity at alpha one.
The second Two Pass family's own alpha pair enters the blend proof, so the recorded claim rests on properties that govern that pass.
The pinned source digest is memoized by asset path and modification time, so attestation stops re-reading and re-hashing the full shader source per material.
The bound alpha-mask route mirrors its sibling's capture-refusal check, so a failed red capture yields the named mask diagnostic.

Pair order note: this pair runs fifth in the audit execution order, after multi-capture, analysis, host, and build.
Two files in this design are shared with later pairs.
Pair 6 deletes the `threadMaps` argument at the `ScalarProductFold.Fold` call sites in `PoiyomiMaterialSemantics.cs` at position 6.
Pair 7 extracts the canonical skeleton from `PoiyomiOpaqueConversion.cs` at position 7 and fixes the NaN sweep order note on the post-pair-5 text.
This pair lands first on both files.

All changes stay inside the conversion and semantics layer.
They require no new capture routes.
They preserve exact fail-closed classification.

---

## 2. Background and Motivation

`[SOURCE]` Finding 37, observed at `Packages/com.alrauna.amuse/Editor/Semantics/Poiyomi/PoiyomiOpaqueConversion.cs:283-305`.
`EvaluateVerifiedEligibility` classifies the no-op case at step 2 through `IsFunctionallyOpaque`, and runs the finiteness sweep at step 3.
`IsFunctionallyOpaque` reads `_Cutoff` with the comparison `Read(values, "_Cutoff") > 1f` at lines 447-449.
A NaN cutoff fails that comparison, so a material whose only non-canonical value is a NaN `_Cutoff` classifies as `AlreadyOpaque` when the queue and tag match.
The vendor clips with `clip(alpha - _Cutoff)` in every pass, so a non-finite cutoff leaves the clip undefined.
The investigation records the defect as contained, because `AlreadyOpaque` mutates nothing.

`[SOURCE]` Finding 3, observed at `Packages/com.alrauna.amuse/Editor/Semantics/Poiyomi/PoiyomiOpaqueConversion.cs:189-195, 263-395`.
`CanonicalOpaqueTuple` writes `_OutlineSrcBlend = 1`, `_OutlineDstBlend = 0`, `_OutlineSrcBlendAlpha = 1`, `_OutlineDstBlendAlpha = 0`, `_OutlineBlendOp = 0`, and `_OutlineBlendOpAlpha = 4`.
`EvaluateVerifiedEligibility` gates the base RGB pair at step 7, the ForwardAdd factors at step 8, and the clip threshold at step 9.
No step reads any of the six outline blend fields.
The 2026-08-27 render-state design read them but never gated them, because its outline refusal made the fields irrelevant.
The 2026-10-07 outline design deleted that refusal and left no replacement.
The semantics layer never reads the outline blend pair, so a fade material with an additive outline pair passes every alpha gate and every conversion gate, and the clone renders the outline as a solid color-replacing shell instead of an additive glow.

`[SOURCE]` Finding 18, observed at `Packages/com.alrauna.amuse/Editor/Semantics/Poiyomi/PoiyomiMaterialSemantics.cs:1049-1062, 1068-1084, 1637-1680`.
`IsProvenOpaqueBlend` takes four family property names but reads the alpha pair from the hardcoded constants `SrcBlendAlphaProperty` and `DstBlendAlphaProperty` at lines 1657-1663.
The second-family call at lines 1071-1077 passes `SrcBlend2Property`, `DstBlend2Property`, `BlendOp2Property`, and `BlendOpAlpha2Property`, so its recorded alpha proof rests on the first family's pair.
`_SrcBlendAlpha2` and `_DstBlendAlpha2` had zero occurrences in code before this design.
The investigation confirms the pinned Two Pass source declares its own second-pass alpha pair.

`[SOURCE]` Finding 38, observed at `Packages/com.alrauna.amuse/Editor/Semantics/Poiyomi/PoiyomiMaterialSemantics.cs:2610-2640`.
`GatherSourceEvidence` reads the full shader source with `File.ReadAllText` and hashes it with `ComputeNormalizedSourceHash` on every call.
The result is a pure function of an immutable asset.
The investigation records the cost as per material per path, with no memoization.

`[SOURCE]` Finding 39, observed at `Packages/com.alrauna.amuse/Editor/Semantics/Poiyomi/PoiyomiMaterialSemantics.cs:1965-1978, 1988-2103`.
The bound-mask route checks `mask.Texture == null || !mask.Texture.HasSourceIdentity` and refuses with `UnstableTextureIdentity`.
It never consults `RedCaptureRefusal`.
The red field is read in the pair `(1, 0)` arm at lines 1988-2042 and in the affine arm at lines 2071-2103.
The sibling outline route performs the check at lines 1550-1562 with `AlphaCaptureRefusal`, because that route reads the alpha channel.
`CapturedTextureEvidence` carries `AlphaCaptureRefusal` and `RedCaptureRefusal` as independent facts, because the two channels fail independently (`Editor/Host/UnityMaterialEvidenceCapture.cs:383-391`).
The mask request asks for the red channel alone (`Editor/Semantics/Poiyomi/PoiyomiMaterialSemantics.cs:2775-2781`), so the failing channel is red.

Line drift note: the investigation cites `2004-2098` for Finding 39.
The observed route spans `1965-2103` in the same file.
The investigation's mechanism and fix shape are unchanged.

---

## 3. Detailed Design

### 3.1 The Finiteness Sweep Precedes the No-op Classification

In `Packages/com.alrauna.amuse/Editor/Semantics/Poiyomi/PoiyomiOpaqueConversion.cs`, inside `EvaluateVerifiedEligibility`:

Move the finiteness loop from its position after `IsFunctionallyOpaque` to directly before it.
The evaluation order becomes:

1. Schema presence, unchanged.
2. Finiteness. Any NaN or Infinity in the 23 schema values refuses with `ConversionPropertyNotFinite`.
3. `AlreadyOpaque`, unchanged in body.
4. Premultiplication read, unchanged.
5. Coverage, depth, base RGB, ForwardAdd, outline, and clip gates, unchanged in relative order.

Update the method's order comment.
The comment now reads that the finiteness sweep is a data-validity refusal, not a mutation authorization, so it precedes the no-op classification.
Every gate whose only purpose is to authorize mutation still follows the no-op classification.

Rationale: a non-finite value is malformed evidence about the material, not a state conversion might normalize.
Refusing it before the no-op classification changes no convertible material, because a refusal never mutates.
The NaN cutoff case loses its false `AlreadyOpaque` admission and reports `ConversionPropertyNotFinite`.

### 3.2 The Outline Blend Tuple Gate

New refusal member in `PoiyomiOpaqueConversionRefusal`, placed under the effective render-state group after `UnsupportedForwardAddBlendEquation`:

```csharp
UnsupportedOutlineBlendEquation,
```

New gate in `EvaluateVerifiedEligibility`, inserted as step 9 between the ForwardAdd gate and the clip threshold.
The clip threshold becomes step 10.

```csharp
// 9. Outline blend tuple. The recipe writes the six outline blend
//    fields, so the accepted source states must degenerate to the
//    canonical tuple at alpha 1 exactly like the base pass. The RGB
//    pair uses the base-pass argument: Add with a unit source factor
//    and a zero destination factor gives dst := src at alpha 1. The
//    alpha pair uses the alpha-proof vocabulary: with source factor
//    One and the destination factor in the vendor set, both the add
//    and the max operations yield exactly 1 at alpha 1. A material
//    that stores any other pair renders its outline differently from
//    the canonical clone, so it refuses by name.
if (Read(values, "_OutlineBlendOp") !=
        OpaqueConversionFactors.BlendOpAdd ||
    !OpaqueConversionFactors.IsUnitSourceFactorAtAlphaOne(
        Read(values, "_OutlineSrcBlend")) ||
    !OpaqueConversionFactors.IsZeroDestinationFactorAtAlphaOne(
        Read(values, "_OutlineDstBlend")) ||
    Read(values, "_OutlineSrcBlendAlpha") !=
        OpaqueConversionFactors.BlendFactorOne ||
    (Read(values, "_OutlineDstBlendAlpha") !=
        OpaqueConversionFactors.BlendFactorZero &&
     Read(values, "_OutlineDstBlendAlpha") !=
        OpaqueConversionFactors.BlendFactorOne &&
     Read(values, "_OutlineDstBlendAlpha") !=
        OpaqueConversionFactors.BlendFactorOneMinusSrcAlpha) ||
    (Read(values, "_OutlineBlendOpAlpha") !=
        OpaqueConversionFactors.BlendOpAdd &&
     Read(values, "_OutlineBlendOpAlpha") !=
        OpaqueConversionFactors.BlendOpMax))
{
    return PoiyomiOpaqueConversionEligibility.Refused(
        PoiyomiOpaqueConversionRefusal.UnsupportedOutlineBlendEquation);
}
```

Derivation of the admitted set, from `Editor/Semantics/OpaqueConversionFactors.cs:40-49` and the base-pass argument quoted by the investigation:

Outline RGB pair. The canonical write is `One, Zero` with operation `Add`.
The base-pass gate admits `_BlendOp == Add`, a source factor where `IsUnitSourceFactorAtAlphaOne` holds, and a destination factor where `IsZeroDestinationFactorAtAlphaOne` holds.
At alpha 1 a unit source factor evaluates to 1 and a zero destination factor to 0, so the operation receives `Op(src, 0)`.
With `Add` that is `src`, so the blend degenerates to `dst := src` and the canonical write is an identity there.
The admitted set is the cross product:

| Field | Admitted values |
|---|---|
| `_OutlineBlendOp` | `Add` |
| `_OutlineSrcBlend` | `One`, `SrcAlpha` |
| `_OutlineDstBlend` | `Zero`, `OneMinusSrcAlpha` |

Outline alpha pair. The canonical write is `One, Zero` with operation `Max`.
At alpha 1 the pass output must be exactly 1, matching the canonical output.
With source factor `One` the source contribution is `alpha * 1 = 1`.
With destination factor `Zero` the destination contribution is 0, with `One` it is the destination alpha, and with `OneMinusSrcAlpha` it is 0 at alpha 1.
Under `Add` the sum is `1 + dst` and the framebuffer clamp at one yields exactly 1.
Under `Max` the result is `max(1, dst)` and equals 1.
This is exactly the `alphaForcedToOne` set the alpha proof admits for the base pair in `IsProvenOpaqueBlend` (`PoiyomiMaterialSemantics.cs:1666-1676`).
The source factor `SrcAlpha` stays excluded on the alpha side, because the alpha-proof vocabulary requires the proven term to be the constant one.
The admitted set is:

| Field | Admitted values |
|---|---|
| `_OutlineSrcBlendAlpha` | `One` |
| `_OutlineDstBlendAlpha` | `Zero`, `One`, `OneMinusSrcAlpha` |
| `_OutlineBlendOpAlpha` | `Add`, `Max` |

The gate is unconditional.
It does not read `_EnableOutlines`, because conversion evidence carries no outline feature state.
The 2026-10-07 outline design moved every outline feature fact to the alpha semantics, deleted the conversion outline machinery, and a pinned grep-level falsifier, `ConversionEvidenceRequest_CarriesNoOutlineSymbol`, holds that line.
Restoring `_EnableOutlines` to conversion evidence would reverse that decision.
The consequence is a conservative coverage change: an outline-disabled material whose stored outline pair is not in the admitted sets now refuses conversion instead of converting.
The failure direction is safe, because the refusal mutates nothing.
`[INFERENCE]` The affected population is small, because the vendor presets write degenerate pairs and the pinned falsifier test `DisabledOutlines_AreConvertible` uses the canonical defaults.

Invariance test list.
`Tests/Editor/Semantics/Characterization/IrrelevantChangeInvarianceTests.cs:76-81` lists the six outline blend fields as irrelevant to the semantic output.
That claim stays true, because the semantics layer never reads them.
The list is unchanged.
The list gains one comment above the outline entries stating that conversion now gates these fields while the semantics layer does not read them.
The investigation's record states the same (`investigation`, record line for Finding 3).

### 3.3 The Second Family's Alpha Pair Enters the Blend Gate

In `Packages/com.alrauna.amuse/Editor/Semantics/Poiyomi/PoiyomiMaterialSemantics.cs`:

New property constants beside the existing 2-family constants:

```csharp
private const string SrcBlendAlpha2Property = "_SrcBlendAlpha2";
private const string DstBlendAlpha2Property = "_DstBlendAlpha2";
```

`IsProvenOpaqueBlend` gains two parameters, `srcBlendAlphaProperty` and `dstBlendAlphaProperty`, placed after `blendOpAlphaProperty`.
The body reads the alpha pair through those parameters instead of the hardcoded `SrcBlendAlphaProperty` and `DstBlendAlphaProperty`.
Both call sites change:

- The base-family call passes `SrcBlendAlphaProperty` and `DstBlendAlphaProperty`.
- The second-family call passes `SrcBlendAlpha2Property` and `DstBlendAlpha2Property`.

`CreateAlphaEvidenceRequest` adds `SrcBlendAlpha2Property` and `DstBlendAlpha2Property` to the scalar set, directly after `BlendOpAlpha2Property`.
The alpha evidence request then carries every property the second gate reads.

Destination-alpha caveat, recorded in both evidence records:
the pass-two gate comment and the scalar-request comment state the same sentence pair.
The alpha pair proves the pass's own alpha output is exactly one.
The destination factor still decides how that output composes with the framebuffer's alpha channel.
The recorded claim covers the rendered pixel, not the framebuffer alpha channel.

Stand-in shaders.
Both `Tests/Editor/Semantics/Poiyomi/PoiyomiSemanticTest.shader` and `Tests/Editor/Semantics/Poiyomi/PoiyomiTwoPassSemanticTest.shader` declare the second family's blend block.
Each gains the two missing declarations beside `_BlendOpAlpha2`, mirroring the base family's declarations exactly, per the stand-ins' own vendor-faithful rule:

```shaderlab
_SrcBlendAlpha2 ("Alpha Source Blend", Int) = 1
_DstBlendAlpha2 ("Alpha Destination Blend", Int) = 10
```

`[INFERENCE]` The vendor's second pass declares its own pair, and the stand-ins mirror the base family's declared defaults `1` and `10`, exactly as they mirror every other second-family property.
The executor confirms the declared defaults against the pinned source notes when touching the stand-ins.

Failure naming stays as is.
A failed second-family gate records `UnsupportedFeature` naming `SrcBlend2Property`, the family's first property, exactly as the base family names `SrcBlendProperty`.

### 3.4 The Source Digest Memo

In `Packages/com.alrauna.amuse/Editor/Semantics/Poiyomi/PoiyomiMaterialSemantics.cs`:

New private memo and internal helper beside `ComputeNormalizedSourceHash`:

```csharp
private static readonly Dictionary<string, (DateTime LastWriteUtc, string Hash)>
    SourceHashMemo = new(StringComparer.Ordinal);

/// <summary>
/// Reads the shader asset source and returns its normalized hash, at
/// most once per unchanged file. The digest is a pure function of the
/// immutable asset, so the memo key is the asset path and the guard is
/// the file's UTC write time. A failed read stays uncached, so a
/// transient failure costs one retry on the next call.
/// </summary>
internal static bool TryReadNormalizedSourceHashCached(
    string assetPath,
    out string normalizedHash)
{
    var lastWrite = File.GetLastWriteTimeUtc(assetPath);
    if (SourceHashMemo.TryGetValue(assetPath, out var cached) &&
        cached.LastWriteUtc == lastWrite)
    {
        normalizedHash = cached.Hash;
        return true;
    }

    normalizedHash = ComputeNormalizedSourceHash(
        File.ReadAllText(assetPath, Encoding.UTF8));
    SourceHashMemo[assetPath] = (lastWrite, normalizedHash);
    return true;
}
```

`GatherSourceEvidence` keeps its structure and replaces the read-and-hash body inside its existing `try` with a call to the helper:

```csharp
try
{
    if (File.Exists(assetPath) &&
        TryReadNormalizedSourceHashCached(assetPath, out var cachedHash))
    {
        normalizedHash = cachedHash;
        hasReadableSource = true;
    }
}
```

The `IOException` and `UnauthorizedAccessException` catches stay unchanged.
`File.GetLastWriteTimeUtc` throws the same exception family on failure, so a failed attempt stays uncached and keeps today's behavior.

The memo holds at most one entry per distinct shader asset path per Editor session.
Capture runs on the Editor main thread, so the plain dictionary needs no lock.
`GatherConversionSourceEvidence` calls `GatherSourceEvidence` and inherits the memo.

### 3.5 The Named Mask Capture Refusal

In `Packages/com.alrauna.amuse/Editor/Semantics/Poiyomi/PoiyomiMaterialSemantics.cs`, inside `TryInterpretAlphaMask`:

The bound-mask route reads the red field in exactly two arms: the `(1, 0)` arm and the affine `(1, value < 1, invert off)` arm.
The saturating `(1, value >= 1)` arm consults no texel of the mask, so it stays refusal-free.
This mirrors the sibling outline route, whose fast path also precedes its capture-refusal check.

Both red-reading arms gain the same check immediately after their existing main-sampling and parallax gates:

```csharp
if (mask.Texture.RedCaptureRefusal !=
    TextureCaptureRefusalReason.None)
{
    AddDiagnostic(
        diagnostics,
        PoiyomiSemanticOutput.Alpha,
        PoiyomiSemanticDiagnosticCode.UnsupportedFeature,
        AlphaMaskProperty);
    return false;
}
```

The check reads `RedCaptureRefusal`, not `AlphaCaptureRefusal`, because the mask request asks for the red channel alone.
The sibling outline route reads `AlphaCaptureRefusal` because it reads the alpha channel.
The two channels fail independently, so each route names its own channel's refusal.
A failed red capture now refuses with `UnsupportedFeature` naming `_AlphaMask` instead of surfacing as an unnamed failure at the fold.

---

## 4. Verification and Test Plan

Every run uses the Unity Test Runner in EditMode against the dev editor instance with `Application.dataPath == <repo-root>/Assets`.
A filtered run that reports 0 tests is a failure.

1. `CanonicalMaterialWithNonFiniteCutoff_RefusesAsNotFinite` in `PoiyomiOpaqueConversionTests`.
   A canonical opaque material with a NaN `_Cutoff` supplied through the `EvaluateWith` evidence seam refuses with `ConversionPropertyNotFinite`.
   RED today, because today it returns `AlreadyOpaque`.
   The pinned `AlreadyOpaque_WhenEveryCanonicalFactMatches` proves the finite canonical case still classifies as `AlreadyOpaque`.
2. `AdditiveOutlineRgbPair_RefusesWithNamedRefusal` and `AdditiveOutlineAlphaPair_RefusesWithNamedRefusal` in `PoiyomiOpaqueConversionTests`.
   The fade baseline with `_OutlineSrcBlend 1, _OutlineDstBlend 1`, and with `_OutlineSrcBlendAlpha 0, _OutlineDstBlendAlpha 1`, refuses with `UnsupportedOutlineBlendEquation`.
   RED today, because today both convert.
3. `OpaqueEquivalentOutlineRgbPairs_StayConvertible` and `OpaqueEquivalentOutlineAlphaPairs_StayConvertible` in `PoiyomiOpaqueConversionTests`.
   Parametrized over the derived admitted sets in section 3.2.
   These are boundary guards, not RED tests, because conversion admits every admitted pair today.
   They turn RED only if the new gate over-refuses.
4. `DivergentSecondFamilyAlphaPair_RefusesCompleteAlpha` in `PoiyomiMultipassRuleTests`.
   Standard second-family RGB pair with `_SrcBlendAlpha2 = 0` refuses the alpha claim and names `_SrcBlend2`.
   RED today, because today the gate reads the base family's pair and completes.
   `StandardAlphaPairsOnBothPassesStayComplete` gains explicit canonical `_SrcBlendAlpha2` and `_DstBlendAlpha2` sets and must keep passing, proving the threaded gate does not over-refuse.
5. `CachedSourceHash_ReturnsTheFirstHashWhileMtimeIsUnchanged` in `PoiyomiSourceAttestationTests`.
   Rewrite a temp file's content and restore the stored write time.
   The helper returns the first hash.
   RED today, because today the helper does not exist and every call re-reads.
6. `CachedSourceHash_RefreshesWhenFileContentChanges` and `CachedSourceHash_MissingFileThrows` in `PoiyomiSourceAttestationTests`.
   A changed write time yields the new content hash.
   A missing path throws `FileNotFoundException`.
   Guards for correct invalidation and the failure contract.
7. `ReplaceBoundMask_RefusedRedCapture_IsUnsupportedFeatureNamingTheMask` and `ReplaceBoundMask_AffineArm_RefusedRedCapture_IsUnsupportedFeatureNamingTheMask` in `PoiyomiAlphaMaskTests`.
   An assigned mask imported as `ARGB4444` sits outside the capture format allowlist, so the red capture refuses.
   Both red-reading arms report `UnsupportedFeature` naming `_AlphaMask`.
   RED today, because today no diagnostic names the mask.
8. `ReplaceBoundMask_SaturatedPair_IgnoresRefusedRedCapture` in `PoiyomiAlphaMaskTests`.
   The same refused mask with the pair `(1, value >= 1)` stays complete.
   Guard against over-refusal in the arm that consults no texel.
9. Existing guards that must keep passing: `ConversionEvidenceRequest_CarriesNoOutlineSymbol`, `DisabledOutlines_AreConvertible`, `NonFiniteConversionProperty_RefusesForEverySchemaProperty`, `PresetFade_IsConvertible`, `AdditiveSecondPassRefusesCompleteAlpha`, `TwoPassIdentityVerifiesAgainstItsOwnPins`, and the full `IrrelevantChangeInvarianceTests` set.
