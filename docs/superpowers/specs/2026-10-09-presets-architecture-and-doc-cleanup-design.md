# Presets, Architecture, and Documentation Cleanup: Design and Specification

Date: 2026-10-09.
Branch plan: fix/latent-bugs-and-impurities. Base: fae7e7e.
No source assets are modified.
All changes stay inside the editor assembly, its tests, and two documentation files.

Privacy note: this document contains no private avatar, renderer, material, scene, or machine identity. Public vendor shader identifiers stay exact. All paths are repository-relative.

Labels: `[SOURCE]` is a fact read from cited code lines in this repository.
`[INFERENCE]` is a conclusion from evidence.
Line numbers were re-read on 2026-10-09 against the branch base. Where an observed line drifted from the investigation record, this document cites the observed line.

---

## 1. Summary

This design fixes the presets and architecture findings of the third latent-bug audit.
It removes five duplications: two renderer analysis pipelines, two conversion outcome mappings, three copies of the normalize and digest rules, two copies of the canonical opaque verification skeleton, and one duplicated test comparer.
It applies the recorded layering ruling for the Semantics clone recipes.
It fixes two documentation drifts in the pass registry and the layering sentence.
It makes one report switch and one parser path fail closed.
It stops two silent rewrites at the inspector and the preset file store.
Each refactor keeps existing tests green. Each behavior fix lands test first.

---

## 2. Background and Motivation

### 2.1 Two Renderer Analysis Pipelines (Finding 19)

`[SOURCE]` `Packages/com.alrauna.amuse/Editor/Host/UnityRendererAlphaAnalysis.cs:613-700`.
The Host probe `Analyze` gathers fields at the structural-refusal density and resolves every material through the private `ResolveFor`.
`ResolveFor` builds the family predicate request, closes a scoped `AlphaFieldProvider` over `material.Evidence`, calls `AlphaSemanticsResolver.Resolve`, and memoizes per material.

`[SOURCE]` `Packages/com.alrauna.amuse/Editor/Analysis/AdmittedMaterialStates.cs:253-352`.
The product path `ResolveSlot` resolves each admitted material with near-identical code: the same `AlphaRequestForFamily` call, the same scoped provider closure over the derived evidence, the same `AlphaSemanticsResolver.Resolve` call.
It adds the derived-evidence admission and the refusal ladder. It memoizes nothing.
A change to one copy leaves every seam-driven test green while the shipped proof path changes.

### 2.2 Conversion Outcome Mapping Shipped Twice (Finding 20)

`[SOURCE]` `Packages/com.alrauna.amuse/Editor/Build/AlphaSeparationPreparation.cs:696-731, 794-812, 877-917`.
The production inline arms map eligibility outcomes to opaque materials. The lilToon arms treat every outcome other than `Convertible` as a refusal and extract `DepthTestDivergence`. The Poiyomi arm switches on three outcomes and extracts two flags.

`[SOURCE]` `Packages/com.alrauna.amuse/Tests/Editor/Build/VerifiedPoiyomiTestSeams.cs:33-77` and `Packages/com.alrauna.amuse/Tests/Editor/Build/VerifiedLilToonTestSeams.cs:394-424`.
Both verified test seams re-implement the same mapping. A new outcome value handled in production leaves the tests green.

### 2.3 Source Normalization and Digest Triplicated (Finding 21)

`[SOURCE]` `Packages/com.alrauna.amuse/Editor/Semantics/NormalizedSourceHash.cs:12-44`.
The canonical class drops an optional leading UTF-8 BOM, folds CRLF and lone CR to LF, and returns the lowercase-hex SHA-256 of the UTF-8 bytes.

`[SOURCE]` `Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonSourceAttestation.cs:942-971`.
The same file keeps a private `Normalize` and a private `Sha256` beside the `ComputeNormalizedSourceHash` method that already delegates to the canonical class.
`[SOURCE]` The private `Normalize` serves three text call sites at lines 1013, 1407, and 1435 that split normalized source into lines for the strip rules. The private `Sha256` serves four digest call sites at lines 1388, 2405, 2429, and 2506. The digest call sites hash already-normalized canonical text, so the copies agree only by that precondition.

### 2.4 Canonical Opaque Verification Skeleton Duplicated across Frontends (Finding 22)

`[SOURCE]` `Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonOpaqueTarget.cs:55-83, 137-166, 244-310` and `Packages/com.alrauna.amuse/Editor/Semantics/Poiyomi/PoiyomiOpaqueConversion.cs:175-205, 471-502, 530-561`.
Both frontends declare the same `CanonicalOpaqueRenderQueue = 2000`, `RenderTypeTagName`, and `CanonicalOpaqueRenderType` constants.
Both `TryFindNonCanonicalFact` methods scan the tuple, then the effective render queue, then the RenderType tag. The lilToon scan adds the `_TransparentMode` conjunct.
Both `PrepareCanonicalOpaqueClone` methods clone, write the tuple, set the queue and the tag, read back, verify the shader, and destroy on failure with the same two exception texts.
The float tuples legitimately differ per family. The queue, tag, and read-back rule is Unity level, not shader level.

### 2.5 Layering Contract Drift (Finding 23)

`[SOURCE]` `Packages/com.alrauna.amuse/Editor/Analysis/AdmittedMaterialStates.cs:4-5` imports Host and Semantics while Host imports Analysis.
`[SOURCE]` `Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonOpaqueTarget.cs:244-310` and `Packages/com.alrauna.amuse/Editor/Semantics/Poiyomi/PoiyomiOpaqueConversion.cs:530-561` create, mutate, and destroy live material clones.
`[SOURCE]` `AGENTS.md:25` says Host is the only module that touches live Unity objects.
`[SOURCE]` `Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonOpaqueTarget.cs:405` exposes the settable static delegate `VerifyTargetIdentity` that swaps production attestation for tests.

### 2.6 Pass Count and Ordinal Documentation Drift (Finding 24)

`[SOURCE]` `AGENTS.md:15-19` says the plugin registers three PlatformFinish passes and lists three. `AGENTS.md:79` repeats "the three passes".
`[SOURCE]` `Packages/com.alrauna.amuse/Editor/Build/AmusePlatformFinishPlugin.cs:211-243` registers five passes in this order: the structural graph check, the animator bindings capture, the semantic barrier, the alpha separation apply, and the transient unlock window close.
`[SOURCE]` `Packages/com.alrauna.amuse/Editor/Build/AlphaSeparationApply.cs:14` calls itself "The third PlatformFinish pass". `Packages/com.alrauna.amuse/Editor/Build/TransientUnlockWindowClose.cs:13-14` calls itself "the fourth and last". `AmusePlatformFinishPlugin.cs:231` calls the close "the fourth and last pass". The real ordinals are fourth and fifth.

### 2.7 Inconsistent Default-Arm Discipline in the Report Switches (Finding 25)

`[SOURCE]` `Packages/com.alrauna.amuse/Editor/Build/AmuseReports.cs:202-207`.
`FeatureSentence` throws `InvalidOperationException` on an unhandled `AlphaUnknownKind`.
`[SOURCE]` `Packages/com.alrauna.amuse/Editor/Build/AmuseReports.cs:435-438`.
`ClosureFailureSentence` returns an empty string on the same condition for `MaterialDependencyClosureFailure`.

### 2.8 Shared Evidence Gates Carries a Frontend-Specific Member (Finding 26)

`[SOURCE]` `Packages/com.alrauna.amuse/Editor/Semantics/EvidenceGates.cs:4` imports the lilToon namespace. The class comment at lines 10-17 claims one definition for all frontends and names "the lilToon unknown-recorder".
`[SOURCE]` `Packages/com.alrauna.amuse/Editor/Semantics/EvidenceGates.cs:152-167` declares `RecordUnknown<T>` over lilToon diagnostic types. Its own comment says the Poiyomi frontend keeps its own analog.
`[SOURCE]` `LilToonAlphaInterpreter.cs:4` and `LilToonMaterialSemantics.cs:10` reach the member through `using static ...EvidenceGates`. The two files hold 48 unqualified call sites.

### 2.9 Smaller Impurities (Finding 27)

`[SOURCE]` `Packages/com.alrauna.amuse/Editor/Host/UnityRendererAlphaAnalysis.cs:206-211, 350-355`. The research-only `BaseMaterialSemanticsProvider` seam ships in the product assembly under a legacy name. Sanctioned, but the name invites deletion.
`[SOURCE]` `Packages/com.alrauna.amuse/Editor/Host/LiveAnimationObservation.cs:197-227`. `IsFiniteExact` compares adjacent keyframe values. A multi-key non-finite curve takes the named non-finite refusal. A single-key curve or an all-equal non-finite run with flat tangents takes the sources-disagree refusal instead. Soundness holds. The refusal name drifts on narrow inputs.
`[SOURCE]` `Packages/com.alrauna.amuse/Editor/Host/EffectiveMaterialMaterialization.cs:201-208`. The block state capture skips a slot whose schema is null. `SchemaFor` synthesizes a schema for any shader by reflection, so the null-schema skip is narrower than a schema gap. The safety net has a small blind spot only.
`[SOURCE]` `Packages/com.alrauna.amuse/Editor/Host/UnityRendererAlphaAnalysis.cs:647-651, 853-858`. The probe `Classify` call omits the extra UV sets because the `extraUvSets` parameter defaults to null. Any uv1 to uv3 layer resolves unknown on the probe path. The product call at `AmusePlatformFinishPlugin.cs:1522-1527` passes `snapshot.ExtraUvSets`, and the snapshot carries the sets.
`[SOURCE]` `Packages/com.alrauna.amuse/Editor/Build/TransientUnlockWindowState.cs:131-140, 177-184, 209-224`. The swap binding records are write-only. The close pass re-derives rewrites by scanning clip values. `TransientUnlockSwapIn.cs:527` is the one production writer. `TransientUnlockWindowCloseTests.cs:880-881` is the one test writer, and the test next to it proves reversion works without any record.
`[SOURCE]` `Packages/com.alrauna.amuse/Tests/Editor/Host/UnityMaterialEvidenceCaptureTests.cs:1278, 1330-1344`. A private reference equality comparer duplicates the production `ReferenceEqualityComparer<T>` at `Editor/Host/ReferenceEqualityComparer.cs:12-27`.
`[SOURCE]` `Packages/.gitignore:2, 11`. Line 11 duplicates the whitelist pattern of line 2.

### 2.10 Preset Parser Lets an Integer Overflow Escape as an Unnamed Exception (Finding 28)

`[SOURCE]` `Packages/com.alrauna.amuse/Editor/Presets/PresetParser.cs:160`.
`TryRead<T>` converts the token with `token.Value<T>()`. For `T = int`, a JSON integer between `int.MaxValue` and `long.MaxValue` parses fine into `JObject` and then throws an unnamed `OverflowException` at the conversion.
`[SOURCE]` `PresetParser.cs:41-47`. `JToken.Parse` already maps JSON syntax errors to `MalformedJson`. The conversion gap is the only unnamed escape.
`[SOURCE]` `Packages/com.alrauna.amuse/Editor/Presets/PresetLoadRefusal.cs:8-18`. The closed refusal vocabulary already carries `ValueOutOfRange`.

### 2.11 Polygon Clamp Cross-Field Invariant Missing at the Parse Boundary (Finding 29)

`[SOURCE]` `Packages/com.alrauna.amuse/Editor/Analysis/AlphaPolicyBounds.cs:56-66`.
`ClampNoise` keeps the noise percent strictly below the opaque percent and forces zero for an opaque percent at or below zero.
`[SOURCE]` `Packages/com.alrauna.amuse/Editor/AmuseAvatarOptimizerEditor.cs:371-389`. The inspector normalizes the drawn clamp with `ClampNoise` and writes the normalized value back.
`[SOURCE]` `Packages/com.alrauna.amuse/Editor/Build/AmusePlatformFinishPlugin.cs:1011-1036`. `PolygonClampPercentFrom` re-normalizes the stored clamp with `ClampNoise` at wiring time.
`[SOURCE]` `Packages/com.alrauna.amuse/Editor/Presets/PresetParser.cs:92-99, 210-246`. The parser range-checks each percent alone and enforces no cross-field pair rule. `PresetApplier.cs:27-34` writes the preset into the component with no pair check.
An applied preset can therefore store a pair the build silently reinterprets.

### 2.12 Preset File Store Path and Partial Failure List (Finding 45)

`[SOURCE]` `Packages/com.alrauna.amuse/Editor/Presets/PresetFileStore.cs:14-60`.
`PresetsFolder` returns `PackageInfo.assetPath + "/Presets"`. `TryLoadAll` loads each preset as a `TextAsset` through `AssetDatabase.LoadAssetAtPath` and returns the partially filled list together with `false` when a file is missing or unparsable.
`[INFERENCE]` A package installed through a file reference outside the project produces a rooted `assetPath`, and the asset database rejects rooted paths. This behavior is an inference from the API contract. It needs one runtime probe on a file-installed package before the fix lands.

### 2.13 Inspector Silent Rewrite (Finding 46)

`[SOURCE]` `Packages/com.alrauna.amuse/Editor/AmuseAvatarOptimizerEditor.cs:281-304, 329-357, 359-370`.
Every control writes its property unconditionally on every repaint.
`DrawMipCapPopup` maps a stored value above 10 to the Mip 10 index and writes 10 back. A stored mip cap of 15 becomes 10.
`DrawMinTextureSizePopup` rounds a stored non-power-of-two size up to the next representable size and writes it back. The index walk stops at the first power of two greater than or equal to the stored value, so a stored 100 rewrites to 128.
`PercentSlider` clamps the stored value into 0 to 100 and writes the clamped value back.
A stored mip cap of 15 stops the classification from consulting mips 11 and above of large textures. That direction is less conservative and happens without user consent, so the rewrite is not cosmetic.
`[INFERENCE]` The unconditional `SerializedProperty` assignment also dirties the object on a plain repaint even when no value changes. The origin scout flagged this nuance for one runtime confirmation.
The two cosmetic items in the same finding, per-repaint GUI style allocations and preset row caching, are out of scope for this pair.

### 2.14 Package Version Reader Misses Indirect Dependencies (Finding 34)

`[SOURCE]` `Packages/com.alrauna.amuse/Editor/Semantics/PackageVersionReader.cs:19`.
The single-argument call `Client.List(true)` reads the offline package list with `includeIndirectDependencies` at its false default. A producer installed only as another package's dependency is absent from the result. Version admission refuses with a producer-not-admitted wording instead of the true state. Over-refusal, not misclassification.

### 2.15 Recorded Deferrals (Findings 35 and 36)

Finding 35, the persisted-copy name-marker admission in `GeneratedTextureAttestation.cs:71-82`, defers with rationale. A sound conjunct needs a durable marker that ties a persisted sub-asset to its AMUSE producer. The in-memory arm has that marker through the session claim ledger. A persisted copy from an earlier build does not, because the claim ledger clears per build. Designing that marker is a prerequisite project, not a cleanup task. The exposure stays bounded while it defers: every route fact is read from the real texture, so no false proof follows.
Finding 36, the material-granular AAO atlas corroboration, defers without a fix mandate. The investigation records it for completeness, and the design comments already declare a temporary admission with an upstream retirement path.
Both deferrals stay recorded here until the repository owner closes them.

---

## 3. Detailed Design

### 3.1 One Per-Material Resolution Rule (Finding 19)

In `Packages/com.alrauna.amuse/Editor/Analysis/AdmittedMaterialStates.cs`, add one internal static method next to the existing per-material extraction:

```csharp
internal static AlphaResolution ResolveAdmittedMaterialSemantics(
    CapturedAlphaMaterial material,
    CapturedAlphaSemantics capturedSemantics,
    AlphaFieldSet alphaFields,
    int maxNoiseTexelPercent)
```

The method builds the family predicate request with `UnityMaterialSemantics.AlphaRequestForFamily`, closes the scoped `AlphaFieldProvider` over `material.Evidence` with the defensive `chain = null` false path, and calls `AlphaSemanticsResolver.Resolve` at the caller's noise bound.
`ResolveSlot` calls it per admitted material and keeps its derived-evidence admission, its refusal ladder, and its no-memoization behavior.
`UnityRendererAlphaAnalysis.ResolveFor` keeps its null-material branch and its memoization and delegates the resolution core to the same method.
Memoization stays probe-side. One definition, two entry points.
This is a behavior-preserving refactor. The existing suites `AdmittedMaterialStatesTests` and `UnityRendererAlphaAnalysisTests` are the safety net and must stay green.

### 3.2 One Conversion Outcome Mapping per Family (Finding 20)

In `Packages/com.alrauna.amuse/Editor/Build/AlphaSeparationPreparation.cs`, add two internal static mapping functions:

```csharp
internal static bool TryMapLilToonOutcome(
    LilToonOpaqueConversionEligibility eligibility,
    Func<Material> createClone,
    out Material opaque,
    out LilToonOpaqueConversionRefusal refusal,
    out bool depthTestDivergence)
```

and

```csharp
internal static bool TryMapPoiyomiOutcome(
    PoiyomiOpaqueConversionEligibility eligibility,
    Material live,
    Material preparedOpaque,
    out Material opaque,
    out PoiyomiOpaqueConversionRefusal refusal,
    out bool depthTestDivergence,
    out bool premultiplyNormalization)
```

The lilToon mapping owns the outcome gate, the refusal, the divergence extraction, and the `preparedOpaque ??` reuse rule. The caller owns the clone recipe as the `createClone` closure, because the production arms attest through the evidence overload while the verified seam pins an explicit stand-in shader. The clone recipe stays at the seam boundary by design.
The Poiyomi mapping owns the same plus the `AlreadyOpaque` arm and the normalization flag. The clone step is identical in production and in the seam, so the mapping calls `PoiyomiOpaqueConversion.PrepareCanonicalOpaqueClone` itself.
The three production inline arms and both verified test seams call these functions. A new outcome value then changes one function and every consumer follows.
This is a behavior-preserving refactor. The suites `AlphaSeparationPreparationTests`, `AlphaSeparationSplitTests`, and the fixture suites behind the seams must stay green.

### 3.3 One Normalize and Digest Rule (Finding 21)

In `Packages/com.alrauna.amuse/Editor/Semantics/NormalizedSourceHash.cs`, expose the normalize half:

```csharp
internal static string NormalizeText(string rawSource)
```

`NormalizeText` drops the optional leading UTF-8 BOM and folds CRLF and lone CR to LF. `Compute` calls `NormalizeText` and then digests.
In `LilToonSourceAttestation.cs`, delete the private `Normalize` and `Sha256`.
The three text-split call sites call `NormalizedSourceHash.NormalizeText`.
The four digest call sites call `NormalizedSourceHash.Compute`, which is value-identical on the already-normalized canonical text they hash.
`ComputeNormalizedSourceHash` keeps delegating to `Compute`.
This is a behavior-preserving refactor. `LilToonSourceAttestationTests` and `LilToonAttestationTests` must stay green.

### 3.4 One Unity-Level Canonical Verification Skeleton (Finding 22)

Create `Packages/com.alrauna.amuse/Editor/Semantics/CanonicalOpaqueVerification.cs` with one internal static class:

- The three Unity-level constants: `CanonicalOpaqueRenderQueue = 2000`, `RenderTypeTagName = "RenderType"`, `CanonicalOpaqueRenderType = "Opaque"`.
- `internal static bool TryFindNonCanonicalFact(Material candidate, IReadOnlyList<(string Property, float Value)> tuple, out string factName)`: the tuple scan, the effective queue read, and the RenderType tag comparison.
- `internal static Material PrepareCanonicalClone(Material source, Shader attestedTarget, IReadOnlyList<(string Property, float Value)> tuple, Action<Material> familyWrites, Func<Material, string> familyScan, string shaderIdentityFailureText)`: the clone, write, queue, tag, read-back, shader-identity, destroy-on-failure skeleton. `attestedTarget` is null for the family that keeps the source shader. `familyWrites` carries the family keyword work and the name clearing. `familyScan` returns the extra non-canonical fact name or null. `shaderIdentityFailureText` carries the per-family shader-identity message, because the two frontends do not share it: lilToon says the clone did not take the attested opaque target shader, and Poiyomi says it did not preserve the source shader. The read-back text is shared verbatim and moves here unchanged. lilToon also destroys on a third failure inside its Multi keyword branch, and that branch stays in the lilToon class.

The family classes keep their float tuples, their shader-level property prechecks, and their public signatures. `LilToonOpaqueTarget.TryFindNonCanonicalFact` calls the shared scan and then applies the `_TransparentMode` conjunct. `PoiyomiOpaqueConversion.TryFindNonCanonicalFact` calls the shared scan alone.
This is a behavior-preserving refactor. `LilToonOpaqueTargetTests` and `PoiyomiOpaqueConversionTests` must stay green. The vendor shaders are never installed. The existing `Hidden/Alrauna/AmuseTests/*` stand-ins drive both skeletons after the extraction.

### 3.5 The Layering Ruling (Finding 23)

Decision, per the shared cross-pair ruling: the clone recipes stay in Semantics.
The repository documentation sentence changes. `AGENTS.md` module layering says Host owns live-object reads and capture. It gains one sentence: the Semantics conversion recipes are sanctioned exceptions that mutate only AMUSE-owned transients.
The ruling names the lilToon test seam: the settable `VerifyTargetIdentity` delegate at `LilToonOpaqueTarget.cs:405` is part of the same sanctioned exception. It swaps production attestation for tests and touches no avatar asset.
The repository owner can overrule this decision. The spec records it as the standing ruling until then.
No code moves in this pair for this finding.

### 3.6 Pass Registry Documentation (Finding 24)

`AGENTS.md` architecture section: replace "registers three PlatformFinish passes" with "registers five PlatformFinish passes". Extend the numbered list to five items in registration order: the structural graph check, the animator bindings capture, the semantic barrier, the alpha separation apply, and the transient unlock window close, each with one plain-English sentence drawn from the registration comments.
Update `AGENTS.md:79` from "the three passes" to "the five passes".
`AlphaSeparationApply.cs:14`: "The third PlatformFinish pass" becomes "The fourth PlatformFinish pass".
`TransientUnlockWindowClose.cs:13-14`: "the fourth and last" becomes "the fifth and last".
`AmusePlatformFinishPlugin.cs:231`: "the fourth and last pass" becomes "the fifth and last pass".
The AGENTS.md edit is limited to the pass-count sentences and the Host-only sentence from section 3.5.

### 3.7 Fail Closed in Both Report Switches (Finding 25)

In `AmuseReports.cs`, replace the `ClosureFailureSentence` default arm:

```csharp
default:
    throw new InvalidOperationException(
        "MaterialDependencyClosureFailure has an unhandled value. " +
        "Every value needs its own report sentence.");
```

The closed refusal vocabulary makes the arm unreachable for every declared value. The throw matches the existing `FeatureSentence` discipline and text pattern.

### 3.8 Evidence Gates Loses the Frontend Member (Finding 26)

Create `Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonUnknownRecord.cs` with one internal static class in the lilToon namespace. Move `RecordUnknown<T>` there unchanged, including its comment.
Delete the member and the lilToon using from `EvidenceGates.cs`. Correct the class comment to drop "the lilToon unknown-recorder".
The two lilToon caller files add `using static ...Semantics.LilToon.LilToonUnknownRecord;` and keep their existing `using static ...Semantics.EvidenceGates;` because both use other gate members. The 48 call sites stay untouched because the method name is unchanged.

### 3.9 The Smaller Impurities, One Task (Finding 27)

1. Rename `BaseMaterialSemanticsProvider` to `ResearchMaterialSemanticsProvider` in `UnityRendererAlphaAnalysis.cs`, with the comment restated so the name says research-only. Update the declaration, the `Capture` parameter, and the type references. The old name has no other references.
2. Add an early non-finite arm to `IsFiniteExact`: when any key value is non-finite, the method answers false before the adjacency loop. A single-key NaN curve and an all-equal infinite run with flat tangents then take the named non-finite refusal instead of the sources-disagree refusal. Every finite-curve verdict stays identical. This is a behavior change on narrow inputs only, and it lands with its own test.
3. In `EffectiveMaterialMaterialization.cs`, restate the skip comment: the skip guards a material or shader without a schema, and `SchemaFor` synthesizes a schema for any shader, so the guard is a null-reference guard, not a schema gap. No behavior change.
4. Pass `snapshot.ExtraUvSets` at the probe `Classify` call and remove the `= null` default from the `extraUvSets` parameter. The product call already passes the sets. The one test caller passes them explicitly. A uv1 to uv3 layer then classifies on the probe path like on the product path. This is a behavior change on the probe path and it lands with its own test.
5. Delete the write-only binding records: the `bindings` field, the `Bindings` property, the `AddBinding` method, the `RewrittenBinding` struct, the `TransientUnlockSwapIn.cs:527` call, and the test scaffolding at `TransientUnlockWindowCloseTests.cs:880-881` including its now-unused local. The assertions of that test stay, because they prove value-based reversion without any record.
6. In `UnityMaterialEvidenceCaptureTests.cs:1278`, use `ReferenceEqualityComparer<object>.Instance` and delete the private comparer class.
7. Delete line 11 of `Packages/.gitignore`.

### 3.10 Range Check Before Integer Conversion (Finding 28)

In `PresetParser.TryRead<T>`, branch the integer case before conversion:

```csharp
if (typeof(T) == typeof(int))
{
    var raw = token.Value<long>();
    if (raw < int.MinValue || raw > int.MaxValue)
    {
        refusal = PresetLoadRefusal.ValueOutOfRange;
        value = default;
        return false;
    }
    value = (T)(object)(int)raw;
    return true;
}
value = token.Value<T>();
```

A JSON integer beyond `long` range already refuses as `MalformedJson` at `JToken.Parse`. A value between `int.MaxValue` and `long.MaxValue` now refuses as `ValueOutOfRange` instead of throwing.
No new enum value is needed.

### 3.11 The Clamp Pair Invariant at Parse Time (Finding 29)

Add one value to the closed vocabulary in `PresetLoadRefusal.cs`:

```csharp
ClampAboveOpaquePercent,
```

In `PresetParser.TryParse`, after the two percent reads succeed, enforce the pair rule that the inspector and the build mapper both encode:

```csharp
if (polygonClamp != 100)
{
    var effectiveOpaque = alphaClamp <= 0 ? 100 : alphaClamp;
    if (polygonClamp >= effectiveOpaque)
    {
        refusal = PresetLoadRefusal.ClampAboveOpaquePercent;
        return false;
    }
}
```

The inert sentinel 100 stays legal in every pair, because both existing mappers read it as inert.
The build mapper `PolygonClampPercentFrom` drops the `ClampNoise` re-normalization and keeps the 0 to 100 banding and the inert mapping of 100 to 0. Its comment now states the parse-time invariant as the contract and names the banding as tamper banding, not invariant enforcement.
The inspector keeps `NormalizePolygonClamp`, because it serves live slider edits, not preset files.
`PresetApplier` needs no check, because every applied preset passed the parser.

### 3.12 Preset File Store Handles the File-Reference Layout (Finding 45)

Precondition: one runtime probe in the dev editor instance records the observed `PackageInfo.FindForAssembly(...).assetPath` shape for this install and whether `AssetDatabase.LoadAssetAtPath` resolves `Presets/safe.json` through it. The probe result decides which branch carries the file-reference case. The plan records the observed values.

The fix in `PresetFileStore`:

- `PresetsFolder` relativizes a rooted `assetPath` against the project root derived from `Application.dataPath`. A rooted path inside the project becomes project-relative and keeps the `TextAsset` route.
- A rooted path outside the project takes a direct-read branch: `File.ReadAllText` on the preset file, then the existing `PresetParser.TryParse`. The parse path stays one parser. The `FileMissing` refusal applies when the file is absent.
- `TryLoadAll` clears `presets` before every failure return after work began. A failed load never returns a partially filled list.

### 3.13 The Inspector Stops the Silent Rewrite (Finding 46)

Every control in `AmuseAvatarOptimizerEditor` writes its property only inside an `EditorGUI.BeginChangeCheck` and `EndChangeCheck` pair. A plain repaint writes nothing.
`DrawMipCapPopup`, `DrawMinTextureSizePopup`, and `PercentSlider` draw from the stored value and write only the user's new selection. The clamp normalization write in `DrawAlphaPolicyControls` is deliberate policy and moves inside the same change check.
When a stored value sits outside the control's representable band, the control draws a `HelpBox` warning that names the stored value, states the nearest legal value, and states that AMUSE writes only on a user change. The banding rules: mip cap outside -1 to 10, texture size not a power of two in 2 to 8192, percent outside 0 to 100.
The inspector never clamps a stored value back into range by itself.
Precondition: the runtime confirmation probe for the `SerializedProperty` dirty nuance runs first and records the observed dirty behavior of a plain repaint. The fix removes the unconditional write regardless of the probe outcome, so the probe confirms the defect and does not gate the fix shape.
The two cosmetic items of Finding 46 stay out of scope.

### 3.14 List Indirect Dependencies Offline (Finding 34)

In `PackageVersionReader`, add one production test seam and pass both flags:

```csharp
internal static Func<bool, bool, RequestList> ListPackages =
    (offline, includeIndirect) => Client.List(offline, includeIndirect);
```

The reader calls `ListPackages(true, true)` and enumerates the result as before. Every admission path heals, because the version lookup sees indirectly installed producers.
Test seam discipline: the tests substitute `ListPackages` and restore it in a finally, matching the production-delegate seam convention.

---

## 4. Verification and Test Plan

1. `PresetParserTests`: `IntegerBeyondIntRangeRefusesValueOutOfRange` parses a preset with `"schemaVersion": 3000000000` and asserts `PresetLoadRefusal.ValueOutOfRange` with no throw.
2. `PresetParserTests`: `ClampAtOrAboveOpaquePercentRefuses`, `ClampBelowOpaquePercentParses`, and `InertClampWithAnyOpaqueParses` pin the pair rule, including `opaquePercent` 0 reading as effective 100 and the inert 100 sentinel staying legal.
3. `PresetFileStoreTests`: `FailedLoadReturnsNoPartialPresets` breaks one shipped file and asserts the returned list is empty on failure. The file-reference branch tests follow the probe: `RootedInsideProjectPathRelativizes` and `RootedOutsideProjectPathReadsFromDisk` use temporary directories and only the branches the probe confirmed.
4. `AmuseAvatarOptimizerEditorTests`: `PlainRepaintDoesNotRewriteStoredMipCap`, `OutOfBandMipCapStaysAndWarns`, `OutOfBandTextureSizeStaysAndWarns`, and `OutOfBandPercentStaysAndWarns` assert the stored serialized values survive a repaint and the warning renders.
5. `AmuseReportStringsTests`: `UnhandledClosureFailureThrows` casts an out-of-range `MaterialDependencyClosureFailure` and asserts `InvalidOperationException`.
6. `LiveAnimationObservationVirtualClipTests`: `SingleNonFiniteKeyIsNotFiniteExact` and `AllEqualInfiniteRunIsNotFiniteExact` pin the narrow-input verdicts that route the refusal to the non-finite name.
7. `UnityRendererAlphaAnalysisTests`: `ProbePathClassifiesThroughExtraUvSets` builds a snapshot with a uv1 layer that decides a triangle and asserts the probe outcome follows it. This test fails before the fix, because the probe resolves the layer unknown.
8. The `ResearchMaterialSemanticsProvider` rename verifies by compilation and by the `UnityRendererAlphaAnalysisTests` run in item 9. No dedicated test applies to a rename.
9. Behavior-preserving refactors 3.1, 3.2, 3.3, 3.4, and 3.9 items 1, 3, 5, 6, 7 verify by the named existing suites staying green: `AdmittedMaterialStatesTests`, `UnityRendererAlphaAnalysisTests`, `AlphaSeparationPreparationTests`, `AlphaSeparationSplitTests`, the two fixture seam suites, `LilToonSourceAttestationTests`, `LilToonAttestationTests`, `LilToonOpaqueTargetTests`, `PoiyomiOpaqueConversionTests`, `TransientUnlockWindowCloseTests`, `TransientUnlockSwapInTests`, `TransientUnlockWindowStateTests`, `UnityMaterialEvidenceCaptureTests`, and the lilToon interpreter and material suites behind the moved recorder.
10. Documentation-only changes verify by reading the final files: the pass count sentence, the five-item list, the two class summaries, the plugin comment, and the amended layering sentence with the sanctioned-exception sentence.
11. `PackageVersionReaderTests`: `ListPackagesReceivesIncludeIndirectTrue` substitutes the `ListPackages` seam and asserts the reader passes `offline: true, includeIndirect: true`. Characterization for the flag value; the over-refusal healing itself needs a fixture package, which the task records as a manual check.

A filtered run that reports 0 tests is a failure. A successful compile is never validation.

---

## 5. Rollout and Touchpoints

Execution order across pairs: 1 multi-capture, 2 analysis, 3 host, 4 build, 5 poiyomi, 6 semantics, 7 presets-architecture. This pair is pair 7.

- `Editor/Build/TransientUnlockWindowClose.cs`: pair 4 changes this file first. This pair touches only the class summary ordinal at the top. The executor re-reads the summary location after pair 4 lands.
- `Editor/Build/TransientUnlockWindowState.cs`: pair 4 fixes the destroy gate and the retention removal contract in the close pass and reads `Slots`, never `Bindings`. This pair's record deletion removes only the write-only binding records. Order 4 then 7 keeps the two edits disjoint.
- `Editor/Semantics/Poiyomi/PoiyomiOpaqueConversion.cs`: pair 5 fixes the NaN sweep order in this file first. This pair's skeleton extraction wraps the post-fix shape. Order 5 then 7.
- `Editor/Semantics/UnityMaterialSemantics.cs`: pairs 1 and 6 own it in different regions. This pair touches it nowhere.
- `AGENTS.md`: this pair alone, limited to the pass-count sentences and the Host-only layering sentence.
