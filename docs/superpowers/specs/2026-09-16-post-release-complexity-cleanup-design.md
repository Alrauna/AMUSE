# Design: Post-release complexity cleanup for seven audit findings

Date: 2026-09-16. Status: draft specification.

## Privacy note

This document names public product code only. It contains no private avatar,
renderer, material, animation clip, or controller names. It contains no
machine paths, network ports, or instance identifiers. All paths are relative
to `<repo-root>/Packages/com.alrauna.amuse/` unless a path names another root.

## Objective

Remove the over-engineering that the cleanup audit of 2026-09-16 found, in the
seven findings the controller selected. Keep every behavior the correctness
contract protects. Change no classification outcome, no refusal outcome, and
no mutation ordering.

The controlling record is
`docs/superpowers/investigations/2026-09-16-post-0.1.0-pre.2-cleanup-audit.md`.
Its identifiers carry over: CU4, CU5, CU6, CU7, CU9, CU10, CU11.

## Scope and decisions

In scope:

| ID | Cut |
|---|---|
| CU4 | The two-factor product encoding in `ScalarSemanticValue` |
| CU5 | The `AmuseBuildOperation` layer |
| CU6 | The mirror sampling enums and their identity switches |
| CU7 | Hand-written equality on four semantics classes |
| CU9 | The triple block-state scanner |
| CU10 | The hand-rolled `ContainsName` |
| CU11 | The `AmuseComponentPlacement` helper class |

Out of scope, by controller decision of 2026-09-16: CU1, CU2, CU3, CU8. Also
out of scope: correctness defects, security, and performance.

Two audit claims were refined against the tree before this specification was
written:

1. CU4 is stronger than the audit stated. No production code constructs a
   `ProductOfTextureSamples` value. The three lilToon production sites build
   `ProductChain` values only. The only constructor caller is one resolver
   test helper. The pair kind is a dead production encoding.
2. CU7 is narrower than the audit estimated. C# 9 has no record structs, so
   seven of the eleven hash-code sites are structs and stay as they are.
   `ScalarSemanticValue` keeps its hand-written equality because its chain
   arrays need element-wise comparison. A record would compare array
   references. Four classes convert.

## Ground rules

- Every change in this specification preserves behavior. Tests are
  characterization, not RED/GREEN. The repo rule applies: an assertion that
  passes on first run is recorded as characterization, never dressed up as
  RED.
- Fail-closed vocabularies stay closed. A deleted translation layer moves its
  validation to the construction site, never to a silent default.
- A deleted `.cs` file deletes its `.meta` file in the same change. A created
  `.cs` file gets its `.meta` from the Unity refresh.
- No new dependency. No new public API. Production types stay `internal`.
- Each task records observed test counts. A filtered run that reports zero
  tests is a failure.

## Detailed design

### 1. CU4 - one product encoding

`Editor/Semantics/MaterialSemantics.cs`:

- Delete `ScalarSemanticValueKind.ProductOfTextureSamples`.
- Delete the `ProductOfTextureSamples` factory.
- Delete the accessors `GetFirstTextureSample`, `GetFirstChannel`,
  `GetSecondTextureSample`, and `GetSecondChannel`.
- Delete the guard `RequireProduct`.
- Narrow `GetProductMultiplier` to the chain kind.
- Delete `_secondSample` and `_secondChannel` with their constructor
  parameters. Collapse the private constructor overloads that carried them.
- Delete the `ProductOfTextureSamples` arms of `Equals` and `GetHashCode`.
- Remove the pair kind from the exclusion lists of `GetTextureSample` and
  `GetChannel`.
- Update the class doc comment to the chain-only meaning.

`Editor/Analysis/AlphaSemanticsResolver.cs`:

- Delete the `ProductOfTextureSamples` case arm.
- Delete `ResolveProduct`. Its only caller was that arm. `ResolveProductChain`
  is the general lemma and keeps its doc comment.

`Editor/Semantics/LilToon/LilToonCutoutMaterialSemantics.cs` and
`LilToonTransparentMaterialSemantics.cs`:

- Delete the pair-kind case arms of the factor-collection switch. The chain
  arm already covers two factors.

`Tests/Editor/Analysis/AlphaSemanticsResolverTests.cs`:

- The helper `ProductValue` builds the pair kind. It builds a two-factor
  `ProductChain` instead. Its multiplier semantics do not change: the chain
  value is `fl(fl(k * f0) * f1)`, which is the pair value's exact shape.

### 2. CU5 - inline the build operation

Keep `AmusePreparationDecision`. It is the return type of the public method
`PrepareSurvivingSet`, and the seam probe in `AlphaSeparationApplyTests`
records it. It moves from `AmuseBuildOperation.cs` into its own file,
`Editor/Build/AmusePreparationDecision.cs`, unchanged.

Delete from `Editor/Build/AmuseBuildOperation.cs` and its file:
`PrepareAmuseMutation`, `ApplyAmuseMutation`, `AmuseBuildOperationResult`,
`AmuseBuildOperationOutcome`, and the `AmuseBuildOperation` class.

`AlphaSeparationApply.Execute` becomes the direct sequence. The inlined body:

```csharp
internal static void Execute(BuildContext context)
{
    if (context == null) throw new ArgumentNullException(nameof(context));

    var state = context.GetState<AmusePlatformFinishState>();
    var lifecycle = state.Lifecycle;
    if (lifecycle == null)
    {
        throw new ArgumentNullException(
            nameof(state) + "." + nameof(lifecycle));
    }

    if (lifecycle.MayUsePositiveMutation)
    {
        // An unexpected preparation defect is not caught: it propagates so
        // NDMF records a build-blocking InternalError before anything is
        // mutated.
        var decision = PrepareSurvivingSet(
            context, state, out var finalization);

        if (!decision.IsPrepared)
        {
            // Only Refused(reason) may reach this branch. A defaulted struct
            // would otherwise preserve the input while explaining nothing,
            // so it is reported as the preparation defect it is.
            if (string.IsNullOrEmpty(decision.RefusalReason))
            {
                throw new InvalidOperationException(
                    "A preparation refusal must be created through " +
                    "AmusePreparationDecision.Refused(reason).");
            }
        }
        else if (decision.HasMutation)
        {
            // First mutation boundary. An apply defect is not caught either,
            // and nothing is rolled back.
            ApplyFinalization(finalization, state);
        }
    }

    // The summary describes one analyzed run with the counts the
    // applied writes produced, so it is reported here - after the
    // mutation - and only when the barrier reached analysis.
    if (state.ReachedRendererAnalysis)
    {
        AmuseReports.AvatarSummary(
            context.AvatarRootObject,
            state.AnalyzedRendererCount,
            state.AppliedOpaqueTriangleCount,
            state.SemanticallyRefusedRendererCount,
            state.Lifecycle.BuildPath,
            state.AlphaPolicyActive);
    }
}
```

The old null guard on the asset saver tested the deleted delegate plumbing.
The lifecycle gate already requires the saver before the apply pass runs, so
the guard has no production meaning and is not re-created.

Test migration from `Tests/Editor/Build/AmuseBuildOperationTests.cs`:

- Move `PreparationRefusalRequiresAReason` into
  `AlphaSeparationApplyTests`. It tests `AmusePreparationDecision.Refused`,
  which survives.
- Move `ProductionEditorCodePersistsOnlyThroughTheNdmfAssetSaver` into
  `AlphaSeparationApplyTests`. Its assembly anchor becomes
  `typeof(AlphaSeparationApply).Assembly`.
- Delete the rest of the file with its `.meta` file: the pure delegate
  tests, the four NDMF pipeline tests of the deleted seam, the
  `AfterAmuseOperationPlugin` registration, and the local helpers
  `OperationScope`, `RecordingAssetSaver`, `TestVrchatPlatform`,
  `ExpectReportedException`, and `NewTriangleMesh`.

Coverage note, recorded openly: the retired tests observed the delegate seam
itself. The real-pipeline apply tests in `AlphaSeparationApplyTests` already
drive `AlphaSeparationApply.Execute` through the full NDMF lifecycle, and the
seam plugin observes `PrepareSurvivingSet` decisions directly. No observable
production behavior loses coverage. Execute-level refusal ordering loses its
dedicated test because the branch shrinks to the guard above, whose
construction rule keeps its own test.

### 3. CU6 - one sampling vocabulary

`Editor/Analysis/TriangleAlphaClassifier.cs`:

- Delete `AlphaFilterMode`, `AlphaWrapMode`, and `AlphaAnisoMode`.
- Re-type `AlphaSamplingSettings` to the semantics enums and validate at
  construction, mirroring `TextureSampling`:

```csharp
internal readonly struct AlphaSamplingSettings
{
    private const TextureAnisoMode DefaultAniso = TextureAnisoMode.None;

    internal TextureFilterMode FilterMode { get; }
    internal TextureWrapMode WrapMode { get; }
    internal TextureAnisoMode AnisoMode { get; }

    internal AlphaSamplingSettings(
        TextureFilterMode filterMode,
        TextureWrapMode wrapMode)
        : this(filterMode, wrapMode, DefaultAniso)
    {
    }

    internal AlphaSamplingSettings(
        TextureFilterMode filterMode,
        TextureWrapMode wrapMode,
        TextureAnisoMode anisoMode)
    {
        if (!Enum.IsDefined(typeof(TextureFilterMode), filterMode))
        {
            throw new ArgumentOutOfRangeException(nameof(filterMode));
        }
        if (!Enum.IsDefined(typeof(TextureWrapMode), wrapMode))
        {
            throw new ArgumentOutOfRangeException(nameof(wrapMode));
        }
        if (!Enum.IsDefined(typeof(TextureAnisoMode), anisoMode))
        {
            throw new ArgumentOutOfRangeException(nameof(anisoMode));
        }

        FilterMode = filterMode;
        WrapMode = wrapMode;
        AnisoMode = anisoMode;
    }
}
```

- Replace every `AlphaFilterMode`, `AlphaWrapMode`, and `AlphaAnisoMode`
  reference inside the classifier with the semantics enum member of the same
  name.
- Delete the filter re-validation guard in `Classify`. Construction now
  rejects an undefined mode before the classifier sees it. This is the
  fail-closed property moving earlier, not weakening.
- Add `using Alrauna.Amuse.Editor.Semantics;` to the file's usings.

`Editor/Analysis/AlphaSemanticsResolver.cs`:

- Delete `TryMapSampling` with its doc comment.
- In `ResolveSampled`, replace the mapping call and its refusal branch with
  direct construction:

```csharp
var sampling = new AlphaSamplingSettings(
    sample.Sampling.Filter,
    sample.Sampling.Wrap,
    sample.Sampling.Aniso);
```

- Delete `AlphaResolutionFailure.UnsupportedSampling`. Its only reference was
  the deleted branch. The LilToon and Poiyomi diagnostic codes of the same
  name are separate enums and do not change.

Test vocabulary sweep, exact pairs:

| Before | After |
|---|---|
| `AlphaFilterMode.Point` | `TextureFilterMode.Point` |
| `AlphaFilterMode.Bilinear` | `TextureFilterMode.Bilinear` |
| `AlphaFilterMode.Trilinear` | `TextureFilterMode.Trilinear` |
| `AlphaWrapMode.Clamp` | `TextureWrapMode.Clamp` |
| `AlphaWrapMode.Repeat` | `TextureWrapMode.Repeat` |
| `AlphaAnisoMode.None` | `TextureAnisoMode.None` |
| `AlphaAnisoMode.Anisotropic` | `TextureAnisoMode.Anisotropic` |

Files with sites: `Tests/Editor/Analysis/TriangleAlphaClassifierTests.cs`,
`Tests/Editor/Analysis/AlphaSemanticsResolverTests.cs`,
`Tests/Editor/Analysis/AdmittedMaterialStatesTests.cs`,
`Tests/Editor/Build/AlphaSeparationPreparationTests.cs`,
`Tests/Editor/Host/UnityMaterialEvidenceCaptureTests.cs`,
`Tests/Editor/Host/UnityRendererAlphaAnalysisTests.cs`. Each file already
imports the semantics namespace for `UvMapping` or `TextureFilterMode`.

The resolver test at `AlphaSemanticsResolverTests.cs:418-425` builds an
expected `AlphaSamplingSettings` through conditionals. After the merge it
builds `new AlphaSamplingSettings(filter, wrap)` directly.

### 4. CU7 - records for four semantics classes

Convert to `internal sealed record` and delete the hand-written
`Equals`, `Equals(object)`, and `GetHashCode`:

- `TextureSample`
- `ColorSemanticValue`
- `NormalSemanticValue`
- `MaterialSemantics`

The synthesized record equality compares every instance field plus the
equality contract. For each of the four classes, the fields a kind does not
use are construction defaults, so field-wise comparison equals the current
kind-switched comparison. The existing equality assertions in
`MaterialSemanticsTests` are the characterization for this claim.

`ScalarSemanticValue` stays a class with hand-written equality. Its chain
arrays need element-wise comparison. A record would compare array references
and change equality outcomes. The struct types stay structs because C# 9 has
no record structs.

A compile probe gates the conversion: the first class converts alone, Unity
refreshes and compiles, and the focused semantics tests run. The remaining
three convert only after the probe passes. If the probe fails on the Unity
2022.3 compiler, CU7 stops and records the failure. The other findings do not
depend on it.

### 5. CU9 and CU10 - one scanner and one standard call

`Editor/Analysis/AdmittedMaterialStates.cs`:

- Replace `TryGetBlockFloat`, `TryGetBlockColor`, and `TryGetBlockVector`
  with one entry lookup:

```csharp
private static bool TryGetBlockEntry(
    IReadOnlyList<BlockStateEntry> slotBlockEntries,
    string propertyName,
    ShaderPropertyType type,
    out BlockStateEntry entry)
{
    if (slotBlockEntries != null)
    {
        for (var i = 0; i < slotBlockEntries.Count; i++)
        {
            var candidate = slotBlockEntries[i];
            if (string.Equals(
                    candidate.Name, propertyName, StringComparison.Ordinal) &&
                candidate.Type == type)
            {
                entry = candidate;
                return true;
            }
        }
    }

    entry = default;
    return false;
}
```

- The float call site matches `Float`, `Range`, or `Int` through three
  chained lookups. The first match wins, exactly as the single scan did.
  The color and vector call sites match their one type and read
  `entry.ColorValue` or `entry.VectorValue`.

- Delete `ContainsName`. Add `using System.Linq;`. The three call sites
  become `relevance.ScalarProperties.Contains(entry.Name,
  StringComparer.Ordinal)` and the color and vector equivalents.

### 6. CU11 - inline the placement predicate

- Delete `Runtime/AmuseComponentPlacement.cs` with its `.meta` file.
- Delete `Tests/Editor/Runtime/AmuseComponentPlacementTests.cs` with its
  `.meta` file.
- In `Editor/AmuseAvatarOptimizerEditor.cs`, replace
  `AmuseComponentPlacement.IsOnHierarchyRoot(component)` with
  `component.transform.parent == null`. The inspector target is never null,
  so the null arm of the old predicate is unreachable there. The build gate
  keeps its own independent placement check and does not change.

## Verification plan

Per task, in this order:

1. Unity refresh, then compile with zero errors.
2. Focused EditMode filter for the touched test classes. Record observed
   counts. Zero tests reported is a failure.
3. Grep-to-zero check for each deleted symbol:
   `ProductOfTextureSamples`, `GetFirstTextureSample`, `GetFirstChannel`,
   `GetSecondTextureSample`, `GetSecondChannel`, `RequireProduct`,
   `ResolveProduct`, `AmuseBuildOperation`, `AmuseBuildOperationOutcome`,
   `AmuseBuildOperationResult`, `PrepareAmuseMutation`, `ApplyAmuseMutation`,
   `TryMapSampling`, `AlphaFilterMode`, `AlphaWrapMode`, `AlphaAnisoMode`,
   `UnsupportedSampling` within `AlphaResolutionFailure`,
   `TryGetBlockFloat`, `TryGetBlockColor`, `TryGetBlockVector`,
   `ContainsName`, `AmuseComponentPlacement`, `IsOnHierarchyRoot`.
4. Final task: the full `Alrauna.Amuse.Tests.Editor` and
   `Alrauna.Amuse.Research.Tests.Editor` assemblies in one run. Record the
   observed pass counts of both.

Expected net effect, estimated: about 430 production lines removed and about
600 test lines removed. No dependency changes. Behavior-preserving, so no
characterization run on private projects is required. Nothing here needs the
Census Lab.

## Identifier sweep

The sweep runs over this document before it is reported complete. It checks
for an at sign joined to a hexadecimal hash, drive-letter paths,
home-directory paths, four-digit ports, and private asset names.
