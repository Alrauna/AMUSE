# Semantics Evidence Robustness: Design and Specification

Date: 2026-10-09.
Branch plan: fix/latent-bugs-and-impurities. Base: fae7e7e.
No source assets are modified.
All changes stay inside the NDMF build copy.

Privacy note: this document contains no private avatar, renderer, material, scene, or machine identity. Public vendor shader identifiers stay exact. All paths are repository-relative.

Labels: `[SOURCE]` is a fact read from cited code lines in this repository during this design session.
`[INFERENCE]` is a conclusion from evidence.
A probe finding is recorded as an observed count with its date.

---

## 1. Summary

This design fixes eleven semantics defects from the third latent-bug audit: Findings 10, 16, 17, 30, 31, 33, 40, 41, 42, 43, and 44.

The fixes close three classes of hole:

- Evidence that can be wrong at the source: the sampled-alpha predicate admits normal-map imports (Finding 10), the anisotropy gate misclassifies level 1 under Forced On (Finding 31), and the base-color and emission tints skip a finiteness check after `Color.linear` (Finding 44).
- Reads that can mix two material states: the lilToon opaque frontend reads the live material after the capture (Finding 16), two lilToon reads treat an absent capture entry as feature off (Finding 17), the transferred-analysis `verifyIdentity` parameter gates one family but not the other (Finding 33), and the attestation profile lookup defaults an unknown shader name to the opaque profile (Finding 43).
- Refusals and cleanup that fall short of the contract: the scalar fold answers a wrong closed form on its false thread (Finding 30), the alpha mask over-refuses two exactly representable constant shapes (Finding 40), the LTCGI token strip matches any Tags line (Finding 41), and the opaque clone recipe leaks the clone on an unexpected throw (Finding 42).

Every change fails closed. No change widens a proof.

---

## 2. Background and Motivation

### Finding 10: Normal-map imports pass the sampled-alpha predicate

`[SOURCE]` `Packages/com.alrauna.amuse/Editor/Semantics/UnityTextureEvidence.cs:201-207, 230` (observed at 199-243 this session).
`TryProveSampledAlphaIsOne` reads the importer and proves sampled alpha one when the source carries no alpha and `alphaSource` is `None`.
`IsCanonicalNormalMapImport` is a separate fact on the same class.
The desktop normal-map conversion swizzles the normal x component into the alpha channel. The sampled alpha of a normal-map import is the normal x component, not one.
`[SOURCE]` `Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonMaterialSemantics.cs:900-902` and `Packages/com.alrauna.amuse/Editor/Semantics/Poiyomi/PoiyomiMaterialSemantics.cs:2303-2306` (investigation citation). The emission-slot proofs in both frontends consume the predicate. A no-alpha source PNG imported as a normal map and assigned to an emission slot passes a fact that is false at playback.
Failure direction: wrongly opaque through the emission proof. Verification label: scout-verified, with a per-texel probe as a fix precondition.

### Finding 16: lilToon opaque frontend mixes snapshot and live reads

`[SOURCE]` `Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonMaterialSemantics.cs:139-145, 177-186, 199-208, 648-669`.
`AnalyzeBaseMaterial` and the friend seam capture once under `LilToonMaterialSemantics.AlphaEvidenceRequest` (lines 141 and 179). The private `InterpretVerifiedMaterial` hands `captured` to alpha only (line 652 reads the captured coverage gates). Base color, emission, and normal read the live `Material`: colors at lines 302 and 838, vectors and scalars through `FirstFailedZeroGate(material, ...)` at lines 258, 721, 797, 1010, `TryReadBinary(material, ...)` at lines 763 and 1001, textures at lines 321, 859, and 1027, and scale and offset in the UV mapping helpers at lines 373-391, 945-969, and 1097-1130.
`[SOURCE]` `Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonCutoutMaterialSemantics.cs:153-183` and `Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonTransparentMaterialSemantics.cs:198-230`. The cutout and transparent frontends read the snapshot throughout.
A mutation between capture and read mixes two material states in one result. Opaque alpha resolves to a constant from the captured gates, so the mix affects base color, emission, and normal facts.
Failure direction: silent incoherence, conservative only by accident.

### Finding 17: two lilToon reads treat absent from capture as feature off

`[SOURCE]` `Packages/com.alrauna.amuse/Editor/Host/UnityMaterialEvidenceCapture.cs:632-678`.
The capture accessors throw `ArgumentException` for a name outside the request. A name inside the request whose property is absent from the material returns false with no value. Both cases are distinct facts.
`[SOURCE]` `Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonLayerAlphaTerm.cs:347-349`. The layer blend-mask read is `evidence.TryGetTexture(blendMaskProperty, out var blendMask) && blendMask.IsAssigned`. A requested-but-absent entry drops the multiplicative mask factor silently. The layer texture read two pages earlier refuses the same shape (lines 281-291), so the file disagrees with itself.
`[SOURCE]` `Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonAlphaInterpreter.cs:237-239`. The main-texture read is `!evidence.TryGetTexture(MainTextureProperty, out var assignment) || !assignment.IsAssigned`. A requested-but-absent `_MainTex` entry takes the declared-default arm and proves a constant.
`[SOURCE]` `Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonAlphaMaskSemantics.cs:257-263`. The mask semantics refuse a requested-but-absent `_AlphaMask` entry with `UnsupportedFeature`. This is the sibling convention.
Both twin evidence requests currently list the names, so both reads are latent. The drift direction is silent misclassification.

### Finding 30: the scalar fold drops an affine map on the false thread path

`[SOURCE]` `Packages/com.alrauna.amuse/Editor/Semantics/MaterialSemantics.cs:1044-1157`.
`ScalarProductFold.Fold` takes a `threadMaps` parameter. The true path allocates the map list. The false path passes null, and every `maps?.Add(...)` drops the factor's affine map. The fold then answers a raw sample chain times a constant.
`[SOURCE]` the four call sites: `Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonAlphaInterpreter.cs:399-409` and `:459-469`, and `Packages/com.alrauna.amuse/Editor/Semantics/Poiyomi/PoiyomiMaterialSemantics.cs:1407-1416` and `:1590-1599`. All four pass `threadMaps: true`.
No live misclassification exists. Nothing at the type level stops the next caller from passing false and receiving a wrong closed form.
Failure direction: latent wrongly opaque.

### Finding 31: the anisotropy gate misclassifies level 1 under Forced On

`[SOURCE]` `Packages/com.alrauna.amuse/Editor/Semantics/UnityTextureEvidence.cs:146-148`.
`TryGetSampling` maps `texture.anisoLevel > 1` to anisotropic and every other level to none.
The Unity documentation special-cases only level 0 under the Forced On quality mode. That implies level 1 samples anisotropically in that mode.
A texture at level 1 with a partially transparent mip then receives the bilinear footprint proof while the hardware averages an elongated footprint.
Failure direction: wrongly opaque. Preconditions: the consuming project sets anisotropic filtering to Forced On. VRChat normally runs Per Texture, where level 1 is genuinely off. Verification label: runtime confirmation required.

### Finding 33: verifyIdentity asymmetry

`[SOURCE]` `Packages/com.alrauna.amuse/Editor/Semantics/UnityMaterialSemantics.cs:436-440, 953-955, 1000-1057, 1082-1085`.
`AnalyzeAlphaMaterialTransferred` passes false at lines 436-440. `AnalyzeAlphaMaterialCore` takes the parameter at lines 953-955. `AnalyzeAlphaMaterial` passes true. The doc sentence says the verification gate is skipped by the parameter, not by a second switch.
The Poiyomi arms call `TryVerifyPoiyomiIdentity` unconditionally (lines 1000-1013). The three lilToon arms wrap their verifies in `verifyIdentity &&` (lines 1024-1057).
The asymmetry fails closed today only because consent never grants Poiyomi names on the transferred path.

### Finding 40: alpha-mask over-refusals

`[SOURCE]` `Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonAlphaMaskSemantics.cs:257-332`.
The unassigned-mask arm computes the exact constant `Mathf.Clamp01(scale + value)`. Mode 1 returns `ConstantTerm(term)`. Mode 2 returns `MainUnchanged()` when the term is at or above one and refuses `UnsupportedFeature` on `_AlphaMaskScale` when the term is below one (lines 257-280).
The assigned-mask path checks source identity and scale and offset (lines 289-308) before it reaches the scale-zero arm (lines 316-330). The scale-zero answer is the exact constant `Mathf.Clamp01(value)` and never reads the texture. An assigned mask with scale zero still demands texture identity.
Both refusals are coverage defects. The arithmetic is exact in binary32: `0 * s` is exactly zero, and `zero + value` is exactly value under both fusion orders.

### Finding 41: LTCGI token strip scope

`[SOURCE]` `Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonSourceAttestation.cs:1265-1281`.
`RemoveAppendedLtcgiTagToken` strips the token from any line that starts with `Tags {` and ends with the token. The R6 proof scopes the strip to a SubShader Tags line. The canonicalization emit loop calls the strip for every line (lines 1173-1175).
`[SOURCE]` `Packages/com.alrauna.amuse/Tests/Editor/Semantics/LilToon/LilToonAttestationTests.cs:617-625`. The pinned test `Canonicalize_AppendedLtcgiTagToken_IsRemoved` drives a bare Tags line with no SubShader wrapper. It bakes the unscoped strip.
Theoretical while the GUID and digest pins hold.

### Finding 42: clone leak on unexpected throw

`[SOURCE]` `Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonOpaqueTarget.cs:231-352` (observed at 231-358 this session).
`PrepareCanonicalOpaqueClone(Material, Shader)` creates the clone with `new Material(source)` and then writes the recipe. Three named failures destroy the clone before throwing: the non-canonical read-back at lines 294-297, the target mismatch at lines 302-305, and the Multi keyword read-back at lines 354-357 inside `WriteMultiModeZeroKeywordSet`.
An unexpected throw between creation and the guaranteed destroy points, for example a capture request defect, orphans the clone.
Failure direction: resource leak on a path the doc promises leak-free.

### Finding 43: profile default

`[SOURCE]` `Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonSourceAttestation.cs:1746-1812`. Observed this session. The investigation cites 1690-1760, and the region drifted.
`ProfileForShaderName` returns the profile whose pinned shader name matches, and `return OpaqueProfile;` when nothing matches. The doc comment admits the default decides which pass asset a gather resolves for a shader that will refuse anyway.
`[SOURCE]` `Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonSourceAttestation.cs:1818-1825`. `GatherSourceEvidenceForShaderName` is the only production caller. It feeds the profile straight into `Gather`.
A future caller that skips the verify gathers opaque-shaped evidence, including filesystem reads, for an unrelated shader.

### Finding 44: Color.linear NaN

`[SOURCE]` `Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonMaterialSemantics.cs:302-313, 841-854`.
The base-color path checks `IsFinite(color.r)`, `color.g`, and `color.b`, then converts with `color.linear` and never checks again. The emission path does the same and then multiplies by `blend * color.a`.
A negative component may produce NaN depending on the Unity implementation. The downstream constant factories validate finiteness, so a post-conversion NaN aborts the analysis with an exception instead of silently poisoning a result.
Whether NaN occurs at all remains Unity-implementation-dependent and unprobed. Verification label: runtime confirmation required.

---

## 3. Detailed Design

The sections follow the execution order inside this pair.

### 3.1 Finding 30: delete the threadMaps parameter

In `Packages/com.alrauna.amuse/Editor/Semantics/MaterialSemantics.cs`:

Delete the `threadMaps` parameter from `ScalarProductFold.Fold`. Allocate the map list unconditionally:

```csharp
var maps = new List<AffineAlphaMap?>();
```

Update the method doc: the fold always threads one map per factor, so an admitted mapped factor never loses its map.

Update the four call sites by removing the `threadMaps: true` argument:
`LilToonAlphaInterpreter.cs:399-409`, `LilToonAlphaInterpreter.cs:459-469`, `PoiyomiMaterialSemantics.cs:1407-1416`, `PoiyomiMaterialSemantics.cs:1590-1599`.

`[INFERENCE]` Deletion is the right half of the investigation's two-part remediation. No call site passes false, so deletion changes no behavior. It removes the wrong-answer option at the type level. Keeping the parameter and refusing mapped factors on the false path would preserve a dead knob that exists only to be refused.

`[INFERENCE]` No RED test exists for this change. Every call site passes true, so no observable defect is reachable today. The plan pins the threading behavior with a characterization test first and states that it is characterization. The compile break after deletion is the change gate.

### 3.2 Finding 17: refuse requested-but-absent names

The capture contract stays untouched: a name outside the request throws as a programming defect. A name inside the request whose property is absent from the material is a domain fact and refuses.

In `Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonAlphaInterpreter.cs:237-239`, split the main-texture read:

```csharp
if (!evidence.TryGetTexture(MainTextureProperty, out var assignment))
{
    return RecordUnknown<ScalarSemanticValue>(
        diagnostics,
        LilToonSemanticOutput.Alpha,
        LilToonSemanticDiagnosticCode.UnsupportedFeature,
        MainTextureProperty);
}

if (!assignment.IsAssigned)
{
    // Declared-default arm. Every attested lilToon source declares
    // _MainTex = "white" {}; the digest pins the Properties block.
    alphaChain = ScalarSemanticValue.Constant(colorAlpha);
}
else
{
    // Existing gates, unchanged.
}
```

The declared-default arm keeps its justification. It now fires only for a declared slot that binds no texture, never for a slot absent from the material.

In `Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonLayerAlphaTerm.cs:347-349`, split the blend-mask read:

```csharp
if (!evidence.TryGetTexture(blendMaskProperty, out var blendMask))
{
    return Refuse(diagnostics, blendMaskProperty);
}

if (blendMask.IsAssigned)
{
    // Existing identity, red-channel, and sampler-borrow checks, unchanged.
}
```

An unassigned blend mask still contributes no factor. An absent entry refuses. This matches the layer texture read in the same method and the mask semantics read in `LilToonAlphaMaskSemantics.cs:257-263`.

### 3.3 Finding 40: admit the exactly representable mask constants

In `Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonAlphaMaskSemantics.cs`:

Add one term kind and one factory:

```csharp
/// <summary>
/// Multiply mode with a provably constant term: the alpha value is the
/// plain main-term value times that constant.
/// </summary>
ConstantMultiplier,
```

```csharp
internal static LilToonAlphaMaskTerm ConstantMultiplierOf(float value)
{
    return new LilToonAlphaMaskTerm(
        LilToonAlphaMaskTermKind.ConstantMultiplier,
        value, default, default, default, default, false,
        default, null);
}
```

Rephrase the unassigned arm (lines 257-280):

```csharp
var term = Mathf.Clamp01(scale + value);
return mode == 1f
    ? ConstantTerm(term)
    : term >= 1f
        ? MainUnchanged()
        : ConstantMultiplierOf(term);
```

Move the scale-zero arm (lines 316-330) above the source-identity and scale-offset checks, directly after the `scale == 1f && value >= 1f` arm. Its answer never reads the texture:

```csharp
if (scale == 0f)
{
    var constant = Mathf.Clamp01(value);
    return mode == 1f
        ? ConstantTerm(constant)
        : constant >= 1f
            ? MainUnchanged()
            : ConstantMultiplierOf(constant);
}
```

Delete the old in-place scale-zero arm.

In `Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonAlphaInterpreter.cs`, compose the new kind at the same point where the mask sample composes over the layered chain, after both layer compositions:

```csharp
if (maskTerm.Kind == LilToonAlphaMaskTermKind.ConstantMultiplier)
{
    alphaChain = ScalarProductFold.Fold(
        alphaChain,
        ScalarSemanticValue.Constant(maskTerm.Constant),
        AlphaMaskModeProperty,
        property => diagnostics.Add(/* existing saturating refusal */),
        threadMaps: true);
    if (alphaChain == null)
    {
        return SemanticOutput<ScalarSemanticValue>.Unknown();
    }
}
```

The fold call carries the same saturating-shape refusal policy as the mask-sample multiply. Section 3.1 deletes its `threadMaps` argument first, so the plan lands this call without the argument.

Update the class doc paragraph that enumerates the admitted shapes. It gains the multiply-mode constant admissions.

### 3.4 Finding 44: re-check finiteness after Color.linear

Probe first, before any code change. A scratch editor script constructs a material tint with one negative component, reads `color.linear`, and records the observed components. The script also probes the emission product shape. Record the observed counts in the plan task. The script is throwaway and never committed.

`[INFERENCE]` The guard lands regardless of the probe outcome. The pre-conversion check cannot bound the conversion or the product, and the downstream factories abort the analysis on a non-finite input today. A named refusal is strictly better than an exception, and the guard costs four comparisons.

In `Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonMaterialSemantics.cs`, base color after line 306:

```csharp
var linear = color.linear;
if (!IsFinite(linear.r) || !IsFinite(linear.g) || !IsFinite(linear.b))
{
    return RecordUnknown<ColorSemanticValue>(
        diagnostics,
        LilToonSemanticOutput.BaseColor,
        LilToonSemanticDiagnosticCode.UnsupportedFeature,
        ColorProperty);
}
var tint = new Vector3(linear.r, linear.g, linear.b);
```

Emission after line 852, on the final product, because a finite times a finite can still overflow to infinity:

```csharp
var linear = color.linear;
var tint = new Vector3(linear.r, linear.g, linear.b) * (blend * color.a);
if (!IsFinite(tint.x) || !IsFinite(tint.y) || !IsFinite(tint.z))
{
    return RecordUnknown<ColorSemanticValue>(
        diagnostics,
        LilToonSemanticOutput.Emission,
        LilToonSemanticDiagnosticCode.UnsupportedFeature,
        EmissionColorProperty);
}
```

`[INFERENCE]` A RED test exists only if the probe shows a non-finite conversion. In that case the plan writes the seam test with the observed input and pins the named refusal. If the probe shows no non-finite conversion in 2022.3, the guard is a defensive invariant. The plan then covers it with the existing finite-path pins and records the probe result, and says so.

### 3.5 Finding 10: exclude normal-map imports from the sampled-alpha predicate

Probe first, before any code change. A scratch editor script imports a generated RGB-only PNG as a normal map, blits the built texture, and reads one texel alpha. The read value is compared against one. The script also records the built graphics format with `importer.sRGBTexture` left true, which answers the secondary sRGB question in the same region. Record the observed values. The script is throwaway.

In `Packages/com.alrauna.amuse/Editor/Semantics/UnityTextureEvidence.cs`, inside the importer arm of `TryProveSampledAlphaIsOne`:

```csharp
if (TryGetTextureImporter(texture, out var importer))
{
    if (importer.textureType == TextureImporterType.NormalMap)
    {
        // The desktop normal-map conversion swizzles the normal x
        // component into the alpha channel. The sampled alpha is that
        // component, not one, whatever the source alpha facts say.
        return false;
    }

    return !importer.DoesSourceTextureHaveAlpha() &&
           importer.alphaSource == TextureImporterAlphaSource.None;
}
```

`[INFERENCE]` The exclusion keys on the texture type, not on `IsCanonicalNormalMapImport`. The swizzle happens for every normal-map import. A flipped green channel changes the sign convention of y, not the presence of x in alpha. Reusing the canonical check would leave flipped imports on a false proof.

Update the method doc: a normal-map import is never proven.

Secondary question, decided by the probe: if the probe shows a stale `sRGBTexture` flag produces an sRGB-named built format on a normal-map import, extend `TryGetColorInterpretation` in the same file to return false for a normal-map import whose built format names sRGB. The refusal arms already name `UnsupportedTextureImport` at both consumers. If the probe shows the conversion always produces a linear format, record that fact in the plan and change nothing.

`[INFERENCE]` The consumers need no change. The lilToon emission proof refuses with `UnsupportedTextureImport` when the predicate turns false. The Poiyomi emission proof refuses the same way. A normal map in an emission slot moves from a false proof to a named refusal. The lilToon bump-map path reads `IsCanonicalNormalMapImport`, a separate fact, and keeps its behavior.

### 3.6 Finding 31: level 1 is mode-dependent

Probe first, before any code change. A scratch editor script sets `QualitySettings.anisotropicFiltering` to Forced On and to Per Texture in turn, samples a level-1 texture through a probe blit with a strongly anisotropic footprint, and compares the readback against a reference sample under known settings. The script records the observed comparison per mode. The script is throwaway.

`[INFERENCE]` Decision: the gate becomes mode-dependent. This picks the first remediation the investigation offers. An unconditional refusal of the bilinear proof at level 1 would tax the common VRChat case, where Per Texture keeps level 1 genuinely off, with a pure coverage loss.

In `Packages/com.alrauna.amuse/Editor/Semantics/UnityTextureEvidence.cs`, replace lines 146-148:

```csharp
var aniso = texture.anisoLevel > 1 ||
            (texture.anisoLevel == 1 && LevelOneSamplesAnisotropically())
    ? TextureAnisoMode.Anisotropic
    : TextureAnisoMode.None;
```

```csharp
/// <summary>
/// The Unity quality modes that sample an anisoLevel 1 texture
/// anisotropically. Forced On forces every level. Enable On Build forces
/// every level in a player build, which is what this evidence describes.
/// Per Texture and Disable respect level 1 as off.
/// </summary>
private static bool LevelOneSamplesAnisotropically()
{
    var mode = QualitySettings.anisotropicFiltering;
    return mode == AnisotropicFiltering.ForcedOn ||
           mode == AnisotropicFiltering.EnableOnBuild;
}
```

`[SOURCE]` `Packages/com.alrauna.amuse/Editor/Semantics/UnityTextureEvidence.cs:141-156`. `TryGetSampling` already returns false for non-Tex2D dimensions and unmappable filter or wrap modes, so the gate change touches only the aniso fact.

`[INFERENCE]` Residual limitation, recorded on purpose. The setting is read at capture time in the editor session. The built avatar runs under the player's own quality mode, which the editor cannot read. VRChat normally runs Per Texture, and the editor project that runs the analysis normally carries the same setting. The probe result decides whether Forced On indeed reaches level 1. If the probe shows level 1 stays bilinear under Forced On, the current gate is correct, the finding closes as refuted, and the plan records that instead of landing a gate change. The Enable On Build classification follows the same build-time forcing semantics and is marked `[INFERENCE]` from the documentation wording.

### 3.7 Finding 41: anchor the LTCGI strip to the SubShader block

In `Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonSourceAttestation.cs`:

Compute the strip scope once, before the emit loop in `AnalyzeCanonicalization`:

```csharp
var ltcgiTagStripScope = ComputeLtcgiTagStripScope(lines);
```

```csharp
/// <summary>
/// Marks the lines where the R6 LTCGI tag strip may run: a Tags line
/// whose innermost open brace scope is a SubShader block. One forward
/// walk tracks brace depth. A comment-only line carries no braces. The
/// anchor may under-strip: a Tags line that keeps its token contributes
/// its original text to the digest and the material refuses. It must
/// never widen the strip beyond SubShader scope.
/// </summary>
private static bool[] ComputeLtcgiTagStripScope(string[] lines)
```

The walk keeps a scope-kind stack. A line whose first token is `SubShader` pushes the SubShader kind once per opening brace it carries. Every other opening brace pushes the plain kind. Every closing brace pops. A `//`-prefixed line contributes no braces. The returned array marks a line when the line starts with `Tags` and the innermost open scope kind is SubShader.

The emit loop replaces the unconditional call:

```csharp
builder.Append(
    NormalizeIncludeLine(
        ltcgiTagStripScope[i]
            ? RemoveAppendedLtcgiTagToken(line, trimmed)
            : line,
        shaderDirectory, projectRoot, includeTree));
```

`[INFERENCE]` Fail direction. A pathological comment or string layout can corrupt the depth count. A corrupted count can only misjudge eligibility. A missed strip keeps the token, the digest covers the original text, and attestation refuses. That is the closed direction. An over-strip would need a token-bearing Tags line outside SubShader scope that the walk believes is inside, and the digest pins make that source shape unreachable while the pins hold.

`[SOURCE]` `Packages/com.alrauna.amuse/Tests/Editor/Semantics/LilToon/LilToonAttestationTests.cs:617-625`. The pinned test `Canonicalize_AppendedLtcgiTagToken_IsRemoved` drives a bare Tags line. It bakes the unscoped strip. Updating it is mandated, not a test weakening. The updated test wraps the Tags line in a minimal SubShader block. New falsifier tests pin retention outside SubShader scope and inside a Pass scope.

### 3.8 Finding 43: return a refusal instead of the opaque profile

In `Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonSourceAttestation.cs`:

Replace `ProfileForShaderName` with the Try pattern and delete the default arm:

```csharp
private static bool TryGetProfileForShaderName(
    string shaderName,
    out LilToonSourceProfile profile)
```

Every pinned name returns its profile with true. The unmatched case returns false with null and no gather work.

Replace `GatherSourceEvidenceForShaderName` (lines 1818-1825) with:

```csharp
internal static bool TryGatherSourceEvidenceForShaderName(
    Shader shader,
    CapturedMaterialEvidence captured,
    out LilToonSourceEvidence evidence,
    out LilToonSemanticDiagnostic refusal)
```

An unmatched name returns false with `UnsupportedShader` naming the shader, before any filesystem access. A matched name returns the same evidence `Gather` always produced.

Update the single production caller, `Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonOpaqueTarget.cs:437-445`:

```csharp
if (!LilToonSourceAttestation.TryGatherSourceEvidenceForShaderName(
        target, evidence, out var targetEvidence, out var diagnostic))
{
    throw new InvalidOperationException(
        "The attested lilToon opaque target failed source " +
        "attestation: " + diagnostic?.Detail);
}
```

The existing verify call and its throw shape stay. The conversion path already treats target attestation as an environment invariant failure.

`[SOURCE]` `Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonSourceAttestation.cs:2305-2311`. `GatherSourceEvidence` gathers the opaque profile unconditionally and keeps its behavior. Its callers are the opaque family capture and the opaque entry seams, and each verifies immediately after. The family-pinned gathers for cutout and transparent gather exactly their own profile and keep their behavior. Only the name-resolving gather had a default arm.

`[SOURCE]` `Packages/com.alrauna.amuse/Tests/Editor/Semantics/LilToon/LilToonOpaqueTargetTests.cs:890-908`. The pinned test `GatherSourceEvidenceForShaderName_UsesTargetShaderName_NotSourceEvidenceShaderName` calls the old signature. The plan updates it to the Try signature. The assertion itself stays true.

### 3.9 Finding 42: try and finally around the clone body

In `Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonOpaqueTarget.cs:231-352`:

```csharp
var clone = new Material(source);
var completed = false;
try
{
    clone.name = string.Empty;
    clone.shader = attestedTarget;
    // ... the whole recipe body, unchanged ...
    completed = true;
    return clone;
}
finally
{
    if (!completed)
    {
        UnityEngine.Object.DestroyImmediate(clone);
    }
}
```

Delete the three now-redundant destroy-before-throw sites: lines 294-297, lines 302-305, and the keyword read-back site at lines 354-357 inside `WriteMultiModeZeroKeywordSet`. Each becomes a bare throw. `DestroyImmediate` on an already-destroyed object is a no-op, so the finally is safe on every path.

Update the exception doc on the inner overload and the doc on `WriteMultiModeZeroKeywordSet`: the finally after clone creation destroys the clone on every throw. The named failures keep their messages.

`[INFERENCE]` No RED test is reachable through compliant inputs. No compliant input throws unexpectedly inside the body, which is the finding's own point: the hazard needs an abnormal failure such as a capture request defect. The plan pins the three named failures as still leak-free with the existing material-count helper in `LilToonOpaqueTargetTests`, states that the pinning is characterization, and treats the structural finally as the change gate.

`[INFERENCE]` Finding 23 ruled that the clone recipes stay in Semantics and that the repository documentation sentence is the artifact to amend. This change keeps the recipe where the ruling sanctions it and hardens its cleanup. It does not move anything.

### 3.10 Finding 16: route the opaque reads through the captured evidence

This is the largest change. It has two halves: a captured fact for the one predicate no evidence kind answers today, and the request plus read rewiring.

#### 3.10.1 The bounded-color-range captured fact

`[SOURCE]` `Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonMaterialSemantics.cs:423-438`. `TryProveSampledColorInUnitRange` reads the asset path, the importer, and `texture.graphicsFormat`, and checks the format against the `BoundedColorFormats` allowlist. It is the one base-color fact with no captured counterpart. The capture is a value snapshot and carries no texture reference, so the frontend cannot run this predicate after the capture.

Move the predicate and the allowlist into `Packages/com.alrauna.amuse/Editor/Semantics/UnityTextureEvidence.cs`:

```csharp
internal static bool TryProveColorValuesInUnitRange(Texture texture)
```

Update the justification comment. The predicate is no longer frontend-local. It is a captured, request-scoped fact with one lilToon consumer today.

In `Packages/com.alrauna.amuse/Editor/Host/UnityMaterialEvidenceCapture.cs`:

- Add the `TextureEvidenceKinds.BoundedColorRange` flag and add it to `AllEvidence`.
- Compute it beside the color interpretation fact at lines 1358-1361:

```csharp
var hasBoundedColorRange =
    (evidence & TextureEvidenceKinds.BoundedColorRange) != 0 &&
    UnityTextureEvidence.TryProveColorValuesInUnitRange(texture);
```

- Add one bool field `colorValuesProvenInUnitRange` to `CapturedTextureEvidence`, placed after `isCanonicalNormalMap`, with the constructor parameter and assignment.

Update the constructor call sites: the production construction site and the three test helpers `AlphaFieldSetTests.cs:83` (positional), `LilToonAlphaMaskSemanticsTests.cs:133` (named), and `LilToonLayerAlphaTermTests.cs:170` (named). The named sites survive the insertion untouched. The positional site gains one argument.

Delete `TryProveSampledColorInUnitRange` and `BoundedColorFormats` from `LilToonMaterialSemantics.cs`.

#### 3.10.2 The full opaque evidence request

In `Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonMaterialSemantics.cs`, add one request constant next to `AlphaEvidenceRequest` at line 506:

```csharp
internal static MaterialEvidenceRequest FullMaterialEvidenceRequest { get; } =
    MaterialEvidenceRequest.Combine(
        AlphaEvidenceRequest,
        new MaterialEvidenceRequest(
            shaderName: false,
            activeColorSpace: false,
            presenceProperties: new[]
            {
                MainTexHsvgProperty,
                MainTexScrollRotateProperty,
                DissolveParamsProperty,
                BumpScaleProperty,
            },
            scalarProperties: BaseColorWriterGates
                .Concat(EmissiveWriterGates)
                .Concat(EmissionModifierGates)
                .Concat(NormalWriterGates)
                .Concat(new[]
                {
                    UseEmissionProperty,
                    EmissionBlendModeProperty,
                    EmissionBlendProperty,
                    EmissionMapUvModeProperty,
                    UseBumpMapProperty,
                    BumpScaleProperty,
                })
                .ToArray(),
            colorProperties: new[]
            {
                ColorProperty,
                BackfaceColorProperty,
                EmissionColorProperty,
            },
            vectorProperties: new[]
            {
                MainTexHsvgProperty,
                MainTexScrollRotateProperty,
                DissolveParamsProperty,
                EmissionBlinkProperty,
                EmissionMapScrollRotateProperty,
            },
            textureProperties: new[]
            {
                new TexturePropertyEvidenceRequest(
                    MainTextureProperty,
                    TextureEvidenceKinds.ScaleOffset |
                    TextureEvidenceKinds.SourceIdentity |
                    TextureEvidenceKinds.Sampling |
                    TextureEvidenceKinds.ColorInterpretation |
                    TextureEvidenceKinds.BoundedColorRange),
                new TexturePropertyEvidenceRequest(
                    MainColorAdjustMaskProperty,
                    (TextureEvidenceKinds)0),
                new TexturePropertyEvidenceRequest(
                    EmissionMapProperty,
                    TextureEvidenceKinds.ScaleOffset |
                    TextureEvidenceKinds.SourceIdentity |
                    TextureEvidenceKinds.Sampling |
                    TextureEvidenceKinds.ColorInterpretation |
                    TextureEvidenceKinds.SampledAlphaIsOne),
                new TexturePropertyEvidenceRequest(
                    EmissionBlendMaskProperty,
                    (TextureEvidenceKinds)0),
                new TexturePropertyEvidenceRequest(
                    BumpMapProperty,
                    TextureEvidenceKinds.ScaleOffset |
                    TextureEvidenceKinds.SourceIdentity |
                    TextureEvidenceKinds.CanonicalNormalMap),
            }));
```

`[SOURCE]` `Packages/com.alrauna.amuse/Editor/Host/UnityMaterialEvidenceCapture.cs:1128-1137`. The capture iterates every requested texture regardless of kinds, so a zero-kinds request still records the assignment and the assigned flag. That is exactly what the two adjust-mask and blend-mask presence checks consume.

`[INFERENCE]` The alpha request keeps its exact content. `InterpretVerifiedAlpha` serves the transferred analysis and reads only the captured coverage gates. Widening the alpha request would pollute every alpha-only consumer. The Poiyomi frontend already carries the same full-versus-alpha split with `FullMaterialEvidenceRequest`, so this follows an existing convention.

#### 3.10.3 The snapshot-routed reads

In `Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonMaterialSemantics.cs`:

- `AnalyzeBaseMaterial` and the friend seam `InterpretVerifiedMaterial(Material, ColorSpace, IReadOnlyCollection<string>)` capture under `FullMaterialEvidenceRequest`.
- The private `InterpretVerifiedMaterial(Material, ColorSpace, IReadOnlyCollection<string>, CapturedMaterialEvidence)` keeps its signature and stops passing `material` down. Base color, emission, and normal take `CapturedMaterialEvidence`.
- Every material read maps to its captured accessor:
  - `material.GetColor(p)` becomes `evidence.TryGetColor(p, out var v)` and refuses `UnsupportedFeature` naming the property when it returns false.
  - `material.GetVector(p)` and `material.GetFloat(p)` become `TryGetVector` and `TryGetScalar` with the same refusal shape.
  - `material.HasProperty(p)` becomes `evidence.HasProperty(p)`.
  - `FirstFailedZeroGate(material, gates)` becomes `FirstFailedZeroGate(evidence, gates)`. The overload exists.
  - `TryReadBinary(material, p, out b)` becomes `TryReadBinary(evidence, p, out b)`. The overload exists.
  - `material.GetTexture(p)` becomes `evidence.TryGetTexture(p, out var a)` with `a.IsAssigned` for the assigned case.
  - `material.GetTextureScale(p)` and `GetTextureOffset(p)` become `a.HasScaleOffset` and `a.Scale`, `a.Offset`, refusing `UnsupportedUv` naming the property when the scale and offset are absent.
- The texture-asset predicates become captured flags:
  - `UnityTextureEvidence.TryGetSourceId(texture, ...)` becomes `a.Texture.HasSourceIdentity` with `a.Texture.SourceIdentity`.
  - `TryGetSampling` becomes `a.Texture.HasSampling` with `a.Texture.Sampling`.
  - `TryGetColorInterpretation` becomes `a.Texture.HasColorInterpretation` with `a.Texture.ColorInterpretation`.
  - `TryProveSampledAlphaIsOne` becomes `a.Texture.SampledAlphaIsProvenOne`.
  - `IsCanonicalNormalMapImport` becomes `a.Texture.IsCanonicalNormalMap`.
  - `TryProveSampledColorInUnitRange` becomes `a.Texture.ColorValuesProvenInUnitRange`.
- The three UV mapping helpers take the evidence and the assignment instead of the material.

Add an internal seam mirroring the cutout and transparent verified seams:

```csharp
internal static LilToonSemanticResult InterpretVerifiedMaterialFromEvidence(
    CapturedMaterialEvidence captured,
    ColorSpace activeColorSpace,
    IReadOnlyCollection<string> compiledFeatures)
```

The doc states the contract: the caller captured under `FullMaterialEvidenceRequest` and established admission upstream.

`[SOURCE]` `Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonAlphaMaskSemanticsTests.cs:133-160`. Synthetic evidence construction follows that helper's pattern, so the new seam is testable without a material.

`[INFERENCE]` Coverage notes. A base color over an assigned `_MainTex` whose material lacks the `_MainTex` ST pair now refuses `UnsupportedUv` where the live read silently treated the pair as identity. That is the closed direction. Every existing base-color, emission, and normal test runs through the friend seam and keeps its outcome, because the stand-in shaders declare the requested properties.

### 3.11 Finding 33: honor the verifyIdentity parameter in every family arm

`[SOURCE]` `Packages/com.alrauna.amuse/Editor/Semantics/UnityMaterialSemantics.cs:1000-1013` (the two Poiyomi arms of `AnalyzeAlphaMaterialCore`) and `:926-940` (`IsAttestedAlphaMaterial`).
Both Poiyomi call sites call `TryVerifyPoiyomiIdentity` and `TryAttestPoiyomiMaterial` unconditionally. The three lilToon arms wrap their verifies in `verifyIdentity &&`. The doc sentence on `AnalyzeAlphaMaterialTransferred` says the verification gate is skipped by the parameter, not by a second switch. The code is false for two of five families.

Design decision, recorded for the repository owner to overrule:
The 2026-10-08 strict-attestation cutover removed a Poiyomi verify bypass on the plain analyze path on purpose. This fix does not reopen it. `AnalyzeAlphaMaterial` passes true, so the analyze path keeps its unconditional verify. `AnalyzeAlphaMaterialTransferred` is the only false caller, and the transferred path never grants Poiyomi names, because consent collection skips Poiyomi families. Gating the two Poiyomi arms by the parameter is behavior-neutral today. It makes the documented parameter contract true for all five families and removes the trap where a future caller that grants Poiyomi names gets silent double verification.

The fix wraps both Poiyomi verify calls in `AnalyzeAlphaMaterialCore` and both Poiyomi arms of `IsAttestedAlphaMaterial` in `verifyIdentity &&`, exactly like the lilToon arms. The executor reads the exact member names at the cited lines before editing.

---

## 4. Verification and Test Plan

All tests run in the Unity Test Runner in EditMode against the dev editor instance with `Application.dataPath == <repo-root>/Assets`. A filtered run that reports 0 tests is a failure.

1. `Fold_WithMappedFactor_KeepsTheMapInTheProductChain` in `Tests/Editor/Semantics/MaterialSemanticsTests.cs`. Characterization, stated as such. Folds a mapped sample and a plain sample and asserts the chain carries the map at index zero and null at index one.
2. `MainTexture_RequestedButAbsentFromEvidence_RefusesInsteadOfDeclaredDefault` in `Tests/Editor/Semantics/LilToon/LilToonAlphaTests.cs`. Synthetic evidence with a `_MainTex` entry whose value is absent. Asserts Unknown with `UnsupportedFeature` naming `_MainTex`. RED against the declared-default arm.
3. `LayerBlendMask_RequestedButAbsentFromEvidence_RefusesInsteadOfDroppingFactor` in `Tests/Editor/Semantics/LilToon/LilToonLayerAlphaTermTests.cs`. Synthetic evidence with a `_Main2ndBlendMask` entry whose value is absent. Asserts a refusal naming `_Main2ndBlendMask`. RED against the dropped-factor arm.
4. `AlphaMask_UnassignedWithSubOneMultiplyConstant_YieldsConstantMultiplier` and `AlphaMask_AssignedZeroScaleWithoutIdentity_YieldsConstantMultiplierWithoutIdentityDemand` in `Tests/Editor/Semantics/LilToon/LilToonAlphaMaskSemanticsTests.cs`. Both RED against today's refusals.
5. `AlphaMask_ConstantMultiplier_ComposesOverTheLayeredAlphaChain` in `Tests/Editor/Semantics/LilToon/LilToonCutoutAlphaTests.cs`. Mode 2 with an unassigned mask and a sub-one constant multiplies the main chain by that constant. RED against today's refusal.
6. `BaseColor_TintNonFiniteAfterLinearConversion_Refuses` and `Emission_TintNonFiniteAfterLinearConversion_Refuses`, written only when the Finding 44 probe shows a non-finite conversion. Otherwise the plan records the probe counts and covers the guard with the existing finite-path pins.
7. `TryProveSampledAlphaIsOne_NormalMapImport_IsNotProven` and `TryProveSampledAlphaIsOne_FlippedGreenNormalMapImport_IsNotProven` in `Tests/Editor/Semantics/UnityTextureEvidenceTests.cs`, using the file's existing `Import` helper. RED: the predicate returns true today for a no-alpha source imported as a normal map.
8. `TryGetSampling_LevelOneUnderForcedOn_IsAnisotropic`, `TryGetSampling_LevelOneUnderPerTexture_IsNone`, `TryGetSampling_LevelOneUnderDisable_IsNone`, and `TryGetSampling_LevelOneUnderEnableOnBuild_IsAnisotropic` in `Tests/Editor/Semantics/UnityTextureEvidenceTests.cs`. Each sets `QualitySettings.anisotropicFiltering` and restores it in a finally. RED: level 1 maps to none today under Forced On and Enable On Build.
9. `Canonicalize_AppendedLtcgiTagTokenInsideSubShader_IsRemoved` (the mandated update of the pinned test), `Canonicalize_AppendedLtcgiTagTokenOutsideSubShaderScope_IsRetained`, and `Canonicalize_PassTagsLineWithLtcgiToken_IsRetained` in `Tests/Editor/Semantics/LilToon/LilToonAttestationTests.cs`.
10. `TryGatherSourceEvidenceForShaderName_UnknownShaderName_ReturnsFalseWithNamedRefusal` in `Tests/Editor/Semantics/LilToonSourceAttestationTests.cs`. RED: the old method returns evidence for any name. The pinned `GatherSourceEvidenceForShaderName_UsesTargetShaderName_NotSourceEvidenceShaderName` test updates to the Try signature with its assertion unchanged.
11. The three named clone-failure leak pins in `Tests/Editor/Semantics/LilToon/LilToonOpaqueTargetTests.cs` using the existing `LoadedMaterialCount` helper. Characterization, stated as such. The finally is the change gate for the unexpected-throw path.
12. `FullMaterialEvidenceRequest_MatchesTheIndependentExactSchema` in `Tests/Editor/Semantics/LilToon/LilToonBaseColorTests.cs`, asserting every category against the literal name list in section 3.10.2, following the `AlphaEvidenceRequest_MatchesTheIndependentExactSchema` pattern in the cutout and transparent suites.
13. `BaseColor_ReadsTheTintFromTheSnapshotNotTheLiveMaterial` in `Tests/Editor/Semantics/LilToon/LilToonBaseColorTests.cs`. Captures a material, mutates `_Color` after the capture, and calls `InterpretVerifiedMaterialFromEvidence`. The result carries the pre-mutation tint. RED by API absence, which is the stated RED shape for a seam that does not exist yet.
14. `TransferredAnalysis_PoiyomiFamily_HonorsVerifyIdentityParameter` in `Tests/Editor/Semantics/UnityMaterialSemanticsTests.cs`. A Poiyomi-family captured material with deliberately stale attestation evidence. The transferred call with `verifyIdentity: false` returns an analysis. The same call with `verifyIdentity: true` refuses `UnattestedShader`. RED: both calls refuse today, which proves the parameter is ignored.
15. The full existing base-color, emission, normal, alpha, mask, layer-term, attestation, opaque-target, and texture-evidence suites stay green after every task.

---

## 5. Rollout and Execution Order

Execution order across pairs: 1 multi-capture, 2 analysis, 3 host, 4 build, 5 poiyomi, 6 semantics, 7 presets-architecture. This pair is 6.

Shared-file touchpoints this pair must respect:

- `Packages/com.alrauna.amuse/Editor/Semantics/UnityMaterialSemantics.cs`: pair 1 edits the request-selection and multi-resolution regions (Findings 1 and 2). Pair 6 edits only the verify arms at lines 1000-1057 for Finding 33. Pair 1 lands first.
- `Packages/com.alrauna.amuse/Editor/Host/UnityMaterialEvidenceCapture.cs`: pair 3 edits the red-channel capture around lines 1386-1398 for Finding 6. Pair 6 adds the `BoundedColorRange` fact at the texture-fact loop around lines 1350-1367 and the `CapturedTextureEvidence` field. Pair 3 lands first. Pair 6's constructor step must also update the positional construction pair 3's new Finding 6 test adds.
- `Packages/com.alrauna.amuse/Tests/Editor/Build/VerifiedLilToonTestSeams.cs` and `Packages/com.alrauna.amuse/Editor/Semantics/UnityMaterialSemantics.cs:563` call `GatherSourceEvidence`, which this design does not change. Finding 43's signature change reaches only `GatherSourceEvidenceForShaderName` and its one production caller in `LilToonOpaqueTarget.cs`. No seam edit is needed.

Order inside this pair: 30, 17, 40, 44, 10, 31, 41, 43, 42, 16, 33. The fold deletion (30) lands before the mask composition (40) uses it. The profile refusal (43) lands before the clone hardening (42) so the outer clone entry throws on the refusal path without a clone. The snapshot routing (16) lands last among the file-heavy tasks because it touches the most files and consumes the predicate change from 10 and the capture kind that 44 and 10 leave stable. The verify-arms fix (33) runs after 16 so the two `UnityMaterialSemantics.cs` edits in this pair do not interleave.

Every task follows the RED to GREEN discipline. A defect whose falsifier cannot reproduce stops work on that defect until the mechanism is understood, as the investigation orders. The three probe findings (10, 31, 44) run their probes before their code changes land, and the plan records the observed counts.
