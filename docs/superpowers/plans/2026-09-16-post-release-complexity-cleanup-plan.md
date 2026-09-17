# Post-Release Complexity Cleanup Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Remove the seven audited over-engineering findings (CU4, CU5, CU6, CU7, CU9, CU10, CU11) with zero behavior change.

**Architecture:** Seven behavior-preserving deletions and inlines across the Semantics, Analysis, Host, Build, and Runtime modules. Each task is self-contained, keeps existing tests green, and ends with a grep-to-zero check for the symbols it removes. The riskiest reshapes (records, build-operation inline) run last behind a compile probe.

**Tech Stack:** C# 9, Unity 2022.3, NUnit (Unity Test Framework EditMode), NDMF API.

**Spec:** `docs/superpowers/specs/2026-09-16-post-release-complexity-cleanup-design.md`

## Global Constraints

- Every change preserves behavior. Tests are characterization. Record observed counts. A filtered run that reports zero tests is a failure.
- Every English text that a human reads uses ASD-STE100 Simplified Technical English: short active sentences, one idea per sentence, no semicolons, and no contractions.
- Fail-closed vocabularies stay closed. Validation moves to the construction site, never to a silent default.
- A deleted `.cs` file deletes its `.meta` file in the same change. A created `.cs` file gets its `.meta` from the Unity refresh.
- No new dependency. No new public API. Production types stay `internal`.
- Never record private avatar, renderer, material, animation clip, or controller names. Never record machine names, user paths, or ports.
- All unit tests use the NUnit constraint model (`Assert.That(actual, Is.EqualTo(expected))`).
- All paths in this plan are relative to `<repo-root>/Packages/com.alrauna.amuse/`.
- Line numbers refer to the tree at the plan date. When earlier edits shift
  later anchors, locate the symbol by name, not by number.
- Base branch: `refactor/post-0.1.0-pre.2-cleanup` from `main` at `26b8581`.

---

### Task 1: One block-state scanner and the standard Contains (CU9, CU10)

**Files:**
- Modify: `Editor/Analysis/AdmittedMaterialStates.cs`
- Test: existing `Tests/Editor/Analysis/AdmittedMaterialStatesTests.cs` (characterization only)

**Interfaces:**
- Consumes: `BlockStateEntry` with `Name`, `Type`, `FloatValue`, `ColorValue`, `VectorValue` in `Alrauna.Amuse.Editor.Host`
- Produces: `private static bool TryGetBlockEntry(IReadOnlyList<BlockStateEntry>, string, ShaderPropertyType, out BlockStateEntry)` in `AdmittedMaterialStates`

- [ ] **Step 1: Record the green baseline**

Refresh Unity and run the EditMode filter `AdmittedMaterialStatesTests`.
Expected: PASS. Record the observed test count.

- [ ] **Step 2: Replace the three scanners with one**

In `Editor/Analysis/AdmittedMaterialStates.cs`, delete `TryGetBlockFloat` (line 876), `TryGetBlockColor` (line 901), and `TryGetBlockVector` (line 924). Insert one helper:

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

- [ ] **Step 3: Update the three call sites**

At the float call site (line 483), replace the call with a chained lookup that keeps first-match order:

```csharp
                    BlockStateEntry blockEntry;
                    if (TryGetBlockEntry(
                            slotBlockEntries, group.PropertyName,
                            ShaderPropertyType.Float, out blockEntry) ||
                        TryGetBlockEntry(
                            slotBlockEntries, group.PropertyName,
                            ShaderPropertyType.Range, out blockEntry) ||
                        TryGetBlockEntry(
                            slotBlockEntries, group.PropertyName,
                            ShaderPropertyType.Int, out blockEntry))
                    {
                        var blockFloat = blockEntry.FloatValue;
                        if (!(blockFloat == serialized))
                        {
```

Keep the body of the surrounding `if` exactly as it is after the `blockFloat` read.

At the color call site (line 518):

```csharp
                    if (TryGetBlockEntry(
                            slotBlockEntries, group.PropertyName,
                            ShaderPropertyType.Color, out var blockEntry))
                    {
                        var blockColor = blockEntry.ColorValue;
```

At the vector call site (line 558):

```csharp
                    if (TryGetBlockEntry(
                            slotBlockEntries, group.PropertyName,
                            ShaderPropertyType.Vector, out var blockEntry))
                    {
                        var blockVector = blockEntry.VectorValue;
```

Keep every statement after each value read unchanged.

- [ ] **Step 4: Replace ContainsName with the standard call**

Add `using System.Linq;` to the file usings. Delete `ContainsName` (line 946). Replace the three call sites (lines 342, 348, 354):

```csharp
if (relevance.ScalarProperties.Contains(
        entry.Name, StringComparer.Ordinal))
```

```csharp
if (relevance.ColorProperties.Contains(
        entry.Name, StringComparer.Ordinal))
```

```csharp
if (relevance.VectorProperties.Contains(
        entry.Name, StringComparer.Ordinal))
```

- [ ] **Step 5: Refresh, compile, verify the characterization holds**

Refresh Unity. Run the EditMode filter `AdmittedMaterialStatesTests`.
Expected: PASS with the count recorded in Step 1.

- [ ] **Step 6: Grep to zero**

Search `Packages/com.alrauna.amuse` for `TryGetBlockFloat`, `TryGetBlockColor`, `TryGetBlockVector`, and `ContainsName`.
Expected: no matches.

- [ ] **Step 7: Commit**

```bash
git add Packages/com.alrauna.amuse/Editor/Analysis/AdmittedMaterialStates.cs
git commit -m "Collapse the block-state scanners and use the standard ordinal Contains"
```

---

### Task 2: Delete the placement helper (CU11)

**Files:**
- Delete: `Runtime/AmuseComponentPlacement.cs` and `Runtime/AmuseComponentPlacement.cs.meta`
- Delete: `Tests/Editor/Runtime/AmuseComponentPlacementTests.cs` and `Tests/Editor/Runtime/AmuseComponentPlacementTests.cs.meta`
- Modify: `Editor/AmuseAvatarOptimizerEditor.cs:24`

**Interfaces:**
- Produces: none. The predicate becomes an inline comparison.

- [ ] **Step 1: Inline the predicate**

In `Editor/AmuseAvatarOptimizerEditor.cs`, replace:

```csharp
            if (AmuseComponentPlacement.IsOnHierarchyRoot(component))
```

with:

```csharp
            if (component.transform.parent == null)
```

The inspector target is never null. The build gate keeps its own placement check.

- [ ] **Step 2: Delete the helper and its tests**

```bash
git rm Packages/com.alrauna.amuse/Runtime/AmuseComponentPlacement.cs \
       Packages/com.alrauna.amuse/Runtime/AmuseComponentPlacement.cs.meta \
       Packages/com.alrauna.amuse/Tests/Editor/Runtime/AmuseComponentPlacementTests.cs \
       Packages/com.alrauna.amuse/Tests/Editor/Runtime/AmuseComponentPlacementTests.cs.meta
```

- [ ] **Step 3: Refresh, compile, run the touched suites**

Refresh Unity. Run the EditMode filters `AmuseAvatarOptimizerEditorTests` and `AmuseAvatarOptimizerTests`.
Expected: PASS on both. Record the observed counts.

- [ ] **Step 4: Grep to zero**

Search `Packages/com.alrauna.amuse` for `AmuseComponentPlacement` and `IsOnHierarchyRoot`.
Expected: no matches.

- [ ] **Step 5: Commit**

```bash
git add Packages/com.alrauna.amuse/Editor/AmuseAvatarOptimizerEditor.cs
git commit -m "Inline the placement predicate into the inspector"
```

---

### Task 3: One product encoding in ScalarSemanticValue (CU4)

**Files:**
- Modify: `Editor/Semantics/MaterialSemantics.cs`
- Modify: `Editor/Analysis/AlphaSemanticsResolver.cs`
- Modify: `Editor/Semantics/LilToon/LilToonCutoutMaterialSemantics.cs`
- Modify: `Editor/Semantics/LilToon/LilToonTransparentMaterialSemantics.cs`
- Modify: `Tests/Editor/Analysis/AlphaSemanticsResolverTests.cs`
- Test: existing `Tests/Editor/Semantics/MaterialSemanticsTests.cs` (characterization only)

**Interfaces:**
- Produces: `ScalarSemanticValueKind` without `ProductOfTextureSamples`
- Produces: `ScalarSemanticValue.GetProductMultiplier()` valid for `ProductChainOfTextureSamples` only
- Keeps: `ScalarSemanticValue.ProductChain(IReadOnlyList<TextureSample>, IReadOnlyList<TextureChannel>, float)`

- [ ] **Step 1: Record the green baseline**

Run the EditMode filters `MaterialSemanticsTests`, `AlphaSemanticsResolverTests`, `LilToonCutoutAlphaTests`, and `LilToonTransparentAlphaTests`.
Expected: PASS on all four. Record the observed counts.

- [ ] **Step 2: Delete the pair encoding in MaterialSemantics.cs**

In `Editor/Semantics/MaterialSemantics.cs`:

1. Delete the enum member `ProductOfTextureSamples` from `ScalarSemanticValueKind` (line 450).
2. Replace the class doc comment (lines 456 to 463) with the chain-only meaning:

```csharp
    /// <summary>
    /// One normalized scalar built as a product chain of sampled texture
    /// terms under one leading constant multiplier. The represented value is
    /// the sampled factors multiplied left to right after the constant:
    /// <c>fl(fl(k * f0) * f1) ...</c>. The multiplier is the leading tint
    /// constant. The resolver owns the product lemmas and fails closed on a
    /// multiplier it cannot prove.
    /// </summary>
```

3. Delete the `_secondSample` and `_secondChannel` fields (lines 468 and 470) and their parameters from every private constructor (lines 479 to 517 and 893 to 917) and from `WithChain` and `WithChildren` (lines 796 to 830). The eleven-argument private constructor becomes nine arguments. The seven-argument forwarding constructor becomes five.
4. Delete the `ProductOfTextureSamples` factory (lines 577 to 595).
5. In `GetTextureSample` (line 599) and `GetChannel` (line 615), remove the `Kind == ScalarSemanticValueKind.ProductOfTextureSamples ||` line from each exclusion list.
6. Delete `GetFirstTextureSample` (line 629), `GetFirstChannel` (line 635), `GetSecondTextureSample` (line 653), and `GetSecondChannel` (line 659).
7. Narrow `GetProductMultiplier` (line 665) to:

```csharp
        internal float GetProductMultiplier()
        {
            if (Kind != ScalarSemanticValueKind.ProductChainOfTextureSamples)
            {
                throw new InvalidOperationException(
                    "A product multiplier is meaningful only for the " +
                    "chain kind.");
            }

            return _multiplier;
        }
```

8. Delete `RequireProduct` (lines 919 to 927).
9. Delete the `ProductOfTextureSamples` arm of `Equals` (lines 947 to 952) and of `GetHashCode` (lines 999 to 1004).

- [ ] **Step 3: Delete the resolver arm and the two-factor lemma**

In `Editor/Analysis/AlphaSemanticsResolver.cs`, delete the case arm at lines 488 to 496 and the whole `ResolveProduct` method with its doc comment (lines 561 to 609). `ResolveProductChain` is the surviving general lemma.

- [ ] **Step 4: Delete the dead case arms in both lilToon files**

In `Editor/Semantics/LilToon/LilToonCutoutMaterialSemantics.cs`, delete the arm at lines 664 to 669:

```csharp
                case ScalarSemanticValueKind.ProductOfTextureSamples:
                    samples.Add(value.GetFirstTextureSample());
                    channels.Add(value.GetFirstChannel());
                    samples.Add(value.GetSecondTextureSample());
                    channels.Add(value.GetSecondChannel());
                    return multiplier * value.GetProductMultiplier();
```

In `Editor/Semantics/LilToon/LilToonTransparentMaterialSemantics.cs`, delete the same shape at lines 743 to 748.

- [ ] **Step 5: Migrate the test helper**

In `Tests/Editor/Analysis/AlphaSemanticsResolverTests.cs`, replace the body of `ProductValue` (lines 1410 to 1418):

```csharp
        private static SemanticOutput<ScalarSemanticValue>
            ProductValue(float multiplier)
        {
            return SemanticOutput<ScalarSemanticValue>.Complete(
                ScalarSemanticValue.ProductChain(
                    new[] { Sample(), MaskSample() },
                    new[] { TextureChannel.Alpha, TextureChannel.Red },
                    multiplier));
        }
```

- [ ] **Step 6: Refresh, compile, verify the characterization holds**

Refresh Unity. Run the EditMode filters from Step 1.
Expected: PASS with the counts recorded in Step 1.

- [ ] **Step 7: Grep to zero**

Search `Packages/com.alrauna.amuse` for `ProductOfTextureSamples`, `GetFirstTextureSample`, `GetFirstChannel`, `GetSecondTextureSample`, `GetSecondChannel`, `RequireProduct`, and `ResolveProduct`.
Expected: no matches.

- [ ] **Step 8: Commit**

```bash
git add Packages/com.alrauna.amuse/Editor/Semantics/MaterialSemantics.cs \
        Packages/com.alrauna.amuse/Editor/Analysis/AlphaSemanticsResolver.cs \
        Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonCutoutMaterialSemantics.cs \
        Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonTransparentMaterialSemantics.cs \
        Packages/com.alrauna.amuse/Tests/Editor/Analysis/AlphaSemanticsResolverTests.cs
git commit -m "Keep one product encoding in ScalarSemanticValue"
```

---

### Task 4: One sampling vocabulary (CU6)

**Files:**
- Modify: `Editor/Analysis/TriangleAlphaClassifier.cs`
- Modify: `Editor/Analysis/AlphaSemanticsResolver.cs`
- Modify: `Tests/Editor/Analysis/TriangleAlphaClassifierTests.cs`
- Modify: `Tests/Editor/Analysis/AlphaSemanticsResolverTests.cs`
- Modify: `Tests/Editor/Analysis/AdmittedMaterialStatesTests.cs`
- Modify: `Tests/Editor/Build/AlphaSeparationPreparationTests.cs`
- Modify: `Tests/Editor/Host/UnityMaterialEvidenceCaptureTests.cs`
- Modify: `Tests/Editor/Host/UnityRendererAlphaAnalysisTests.cs`

**Interfaces:**
- Consumes: `TextureFilterMode`, `TextureWrapMode`, `TextureAnisoMode` from `Alrauna.Amuse.Editor.Semantics`
- Produces: `AlphaSamplingSettings(TextureFilterMode, TextureWrapMode)` and `AlphaSamplingSettings(TextureFilterMode, TextureWrapMode, TextureAnisoMode)`, both validating `Enum.IsDefined` on all three arguments

- [ ] **Step 1: Record the green baseline**

Run the EditMode filters `TriangleAlphaClassifierTests`, `AlphaSemanticsResolverTests`, `AdmittedMaterialStatesTests`, `AlphaSeparationPreparationTests`, `UnityMaterialEvidenceCaptureTests`, and `UnityRendererAlphaAnalysisTests`.
Expected: PASS on all six. Record the observed counts.

- [ ] **Step 2: Re-type AlphaSamplingSettings and delete the mirror enums**

In `Editor/Analysis/TriangleAlphaClassifier.cs`, add `using Alrauna.Amuse.Editor.Semantics;` to the usings. Delete `AlphaFilterMode` (lines 15 to 28), `AlphaWrapMode` (lines 30 to 34), and `AlphaAnisoMode` (lines 42 to 46). Replace `AlphaSamplingSettings` (lines 48 to 75) with:

```csharp
    /// <summary>
    /// The sampling shape the classifier proves under, in the semantics
    /// layer's own closed vocabulary. Construction validates every mode,
    /// so an undefined value fails at the construction site instead of
    /// inside the proof.
    /// </summary>
    internal readonly struct AlphaSamplingSettings
    {
        private const TextureAnisoMode DefaultAniso = TextureAnisoMode.None;

        internal TextureFilterMode FilterMode { get; }
        internal TextureWrapMode WrapMode { get; }
        internal TextureAnisoMode AnisoMode { get; }

        /// <summary>
        /// The common sampling shape: no anisotropy.
        /// </summary>
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

- [ ] **Step 3: Update the classifier references**

In `Editor/Analysis/TriangleAlphaClassifier.cs`, replace every remaining reference by name pair: `AlphaFilterMode.Point` to `TextureFilterMode.Point`, `AlphaFilterMode.Bilinear` to `TextureFilterMode.Bilinear`, `AlphaFilterMode.Trilinear` to `TextureFilterMode.Trilinear`, `AlphaWrapMode.Clamp` to `TextureWrapMode.Clamp`, `AlphaWrapMode.Repeat` to `TextureWrapMode.Repeat`, `AlphaAnisoMode.Anisotropic` to `TextureAnisoMode.Anisotropic`. The comparison sites are in `Classify` (lines 372 to 480).

Delete the filter re-validation guard at lines 1284 to 1290 (`if (sampling.FilterMode != ... ) throw ...`). Construction now rejects an undefined mode before `Classify` runs.

- [ ] **Step 4: Delete the translation in the resolver**

In `Editor/Analysis/AlphaSemanticsResolver.cs`, delete `TryMapSampling` with its doc comment (lines 844 to 900). In `ResolveSampled`, replace:

```csharp
            if (!TryMapSampling(sample.Sampling, out var sampling))
            {
                return AlphaResolution.Refused(
                    AlphaResolutionFailure.UnsupportedSampling);
            }
```

with:

```csharp
            var sampling = new AlphaSamplingSettings(
                sample.Sampling.Filter,
                sample.Sampling.Wrap,
                sample.Sampling.Aniso);
```

Delete the `UnsupportedSampling` member from `AlphaResolutionFailure` (line 18). Its only reference was the deleted branch.

- [ ] **Step 5: Sweep the test vocabulary**

In the six test files named above, apply the same name pairs from Step 3 to every `AlphaFilterMode`, `AlphaWrapMode`, and `AlphaAnisoMode` reference. Each file already imports `Alrauna.Amuse.Editor.Semantics`.

In `Tests/Editor/Analysis/AlphaSemanticsResolverTests.cs` (lines 418 to 425), replace the conditional construction with:

```csharp
                    var expected = new AlphaSamplingSettings(filter, wrap);
```

- [ ] **Step 6: Refresh, compile, verify the characterization holds**

Refresh Unity. Run the EditMode filters from Step 1.
Expected: PASS with the counts recorded in Step 1.

- [ ] **Step 7: Grep to zero**

Search `Packages/com.alrauna.amuse` for `AlphaFilterMode`, `AlphaWrapMode`, `AlphaAnisoMode`, `TryMapSampling`, and `AlphaResolutionFailure.UnsupportedSampling`.
Expected: no matches.

- [ ] **Step 8: Commit**

```bash
git add Packages/com.alrauna.amuse/Editor/Analysis/TriangleAlphaClassifier.cs \
        Packages/com.alrauna.amuse/Editor/Analysis/AlphaSemanticsResolver.cs \
        Packages/com.alrauna.amuse/Tests/Editor/Analysis/TriangleAlphaClassifierTests.cs \
        Packages/com.alrauna.amuse/Tests/Editor/Analysis/AlphaSemanticsResolverTests.cs \
        Packages/com.alrauna.amuse/Tests/Editor/Analysis/AdmittedMaterialStatesTests.cs \
        Packages/com.alrauna.amuse/Tests/Editor/Build/AlphaSeparationPreparationTests.cs \
        Packages/com.alrauna.amuse/Tests/Editor/Host/UnityMaterialEvidenceCaptureTests.cs \
        Packages/com.alrauna.amuse/Tests/Editor/Host/UnityRendererAlphaAnalysisTests.cs
git commit -m "Use the semantics sampling vocabulary in the classifier"
```

---

### Task 5: Records for four semantics classes (CU7)

**Files:**
- Modify: `Editor/Semantics/MaterialSemantics.cs`
- Test: existing `Tests/Editor/Semantics/MaterialSemanticsTests.cs` (characterization only)

**Interfaces:**
- Produces: `TextureSample`, `ColorSemanticValue`, `NormalSemanticValue`, and `MaterialSemantics` as `internal sealed record` types with unchanged constructors, accessors, and factory methods
- Keeps: `ScalarSemanticValue` as a class with hand-written equality

- [ ] **Step 1: Record the green baseline**

Run the EditMode filter `MaterialSemanticsTests`.
Expected: PASS. Record the observed count.

- [ ] **Step 2: Compile probe on TextureSample**

Change the declaration at line 192 from:

```csharp
    internal sealed class TextureSample : IEquatable<TextureSample>
```

to:

```csharp
    internal sealed record TextureSample
```

Delete `Equals(TextureSample)` (lines 215 to 221), `Equals(object)` (lines 223 to 226), and `GetHashCode` (lines 228 to 236).

- [ ] **Step 3: Probe gate**

Refresh Unity and compile.
Expected: zero compile errors. Run the EditMode filter `MaterialSemanticsTests`. Expected: PASS with the Step 1 count.
If the record declaration fails to compile on this Unity 2022.3 compiler, stop this task, record the failure, and leave Task 3 and Task 4 work committed. Do not force the conversion.

- [ ] **Step 4: Convert the remaining three classes**

Apply the same change to `ColorSemanticValue` (line 260, delete equality members at lines 366 to 414), `NormalSemanticValue` (line 1054, delete equality members at lines 1099 to 1117), and `MaterialSemantics` (line 1178, delete equality members at lines 1197 to 1220).

Do not touch `ScalarSemanticValue`. Its chain arrays need element-wise equality. A record would compare array references.

- [ ] **Step 5: Refresh, compile, verify the characterization holds**

Refresh Unity. Run the EditMode filters `MaterialSemanticsTests`, `AlphaSemanticsResolverTests`, `UnityMaterialSemanticsTests`, `LilToonAlphaTests`, and `PoiyomiMaterialSemanticsTests`.
Expected: PASS on all five. Record the observed counts.

- [ ] **Step 6: Commit**

```bash
git add Packages/com.alrauna.amuse/Editor/Semantics/MaterialSemantics.cs
git commit -m "Use records for field-wise semantics value equality"
```

---

### Task 6: Inline the build operation (CU5)

**Files:**
- Create: `Editor/Build/AmusePreparationDecision.cs`
- Delete: `Editor/Build/AmuseBuildOperation.cs` and `Editor/Build/AmuseBuildOperation.cs.meta`
- Modify: `Editor/Build/AlphaSeparationApply.cs`
- Delete: `Tests/Editor/Build/AmuseBuildOperationTests.cs` and `Tests/Editor/Build/AmuseBuildOperationTests.cs.meta`
- Modify: `Tests/Editor/Build/AlphaSeparationApplyTests.cs`

**Interfaces:**
- Keeps: `AmusePreparationDecision` with `Refused(string)`, `NoMutation()`, `Ready()`, `IsPrepared`, `HasMutation`, `RefusalReason`
- Keeps: `AlphaSeparationApply.PrepareSurvivingSet(BuildContext, AmusePlatformFinishState, out AlphaSeparationFinalization)`
- Produces: none. `Execute` runs the sequence directly.

- [ ] **Step 1: Record the green baseline**

Run the EditMode filters `AmuseBuildOperationTests` and `AlphaSeparationApplyTests`.
Expected: PASS on both. Record the observed counts.

- [ ] **Step 2: Move AmusePreparationDecision to its own file**

Create `Editor/Build/AmusePreparationDecision.cs` with the struct moved verbatim from `AmuseBuildOperation.cs` lines 19 to 56:

```csharp
using System;

namespace Alrauna.Amuse.Editor.Build
{
    /// <summary>
    /// The result of preparing an AMUSE mutation. Only an explicit refusal is an
    /// ordinary conservative outcome; an unexpected defect during preparation is
    /// an exception, not a decision.
    /// </summary>
    internal readonly struct AmusePreparationDecision
    {
        private AmusePreparationDecision(
            bool isPrepared,
            bool hasMutation,
            string refusalReason)
        {
            IsPrepared = isPrepared;
            HasMutation = hasMutation;
            RefusalReason = refusalReason;
        }

        internal bool IsPrepared { get; }
        internal bool HasMutation { get; }
        internal string RefusalReason { get; }

        internal static AmusePreparationDecision Refused(string reason)
        {
            if (string.IsNullOrEmpty(reason))
            {
                throw new ArgumentException(
                    "A preparation refusal must explain why AMUSE preserved the input.",
                    nameof(reason));
            }

            return new AmusePreparationDecision(false, false, reason);
        }

        internal static AmusePreparationDecision NoMutation()
        {
            return new AmusePreparationDecision(true, false, null);
        }

        internal static AmusePreparationDecision Ready()
        {
            return new AmusePreparationDecision(true, true, null);
        }
    }
}
```

The block above is the verbatim move of the source body. Only the namespace
wrapper is new.

- [ ] **Step 3: Inline the sequence into AlphaSeparationApply.Execute**

In `Editor/Build/AlphaSeparationApply.cs`, replace the body of `Execute` (lines 32 to 59) and update the class doc reference from `AmuseBuildOperation` to the inlined sequence:

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
                // An unexpected preparation defect is not caught: it propagates
                // so NDMF records a build-blocking InternalError before
                // anything is mutated.
                var decision = PrepareSurvivingSet(
                    context, state, out var finalization);

                if (!decision.IsPrepared)
                {
                    // Only Refused(reason) may reach this branch. A defaulted
                    // struct would otherwise preserve the input while
                    // explaining nothing, so it is reported as the preparation
                    // defect it is.
                    if (string.IsNullOrEmpty(decision.RefusalReason))
                    {
                        throw new InvalidOperationException(
                            "A preparation refusal must be created through " +
                            "AmusePreparationDecision.Refused(reason).");
                    }
                }
                else if (decision.HasMutation)
                {
                    // First mutation boundary. An apply defect is not caught
                    // either, and nothing is rolled back.
                    ApplyFinalization(finalization, state);
                }
            }

            // The summary describes one analyzed run with the counts the
            // applied writes produced, so it is reported here - after the
            // mutation - and only when the barrier reached analysis. Builds
            // the barrier refused or silent-no-op'd keep their own reporting
            // and never gain a summary line.
            if (state.ReachedRendererAnalysis)
            {
                AmuseReports.AvatarSummary(
                    context.AvatarRootObject,
                    state.AnalyzedRendererCount,
                    state.AppliedOpaqueTriangleCount,
                    state.SemanticallyRefusedRendererCount,
                    lifecycle.BuildPath,
                    state.AlphaPolicyActive);
            }
        }
```

Also update the doc comment on `PrepareSurvivingSet` (lines 61 to 68): it no longer names a prepare delegate. It states that `Execute` calls it directly.

- [ ] **Step 4: Delete AmuseBuildOperation**

```bash
git rm Packages/com.alrauna.amuse/Editor/Build/AmuseBuildOperation.cs \
       Packages/com.alrauna.amuse/Editor/Build/AmuseBuildOperation.cs.meta
```

- [ ] **Step 5: Migrate the two surviving tests**

In `Tests/Editor/Build/AlphaSeparationApplyTests.cs`, add these two tests inside the test class:

```csharp
        [Test]
        public void PreparationRefusalRequiresAReason()
        {
            Assert.Throws<ArgumentException>(
                () => AmusePreparationDecision.Refused(null));
            Assert.Throws<ArgumentException>(
                () => AmusePreparationDecision.Refused(string.Empty));
        }
```

Move `ProductionEditorCodePersistsOnlyThroughTheNdmfAssetSaver` verbatim from `AmuseBuildOperationTests.cs` (lines 380 to 432), with one change: the assembly anchor becomes `typeof(AlphaSeparationApply).Assembly`. Keep its doc comment.

- [ ] **Step 6: Delete the old test file**

```bash
git rm Packages/com.alrauna.amuse/Tests/Editor/Build/AmuseBuildOperationTests.cs \
       Packages/com.alrauna.amuse/Tests/Editor/Build/AmuseBuildOperationTests.cs.meta
```

This removes the assembly-level `ExportsPlugin` registration of `AfterAmuseOperationPlugin` with the file. The retired tests observed the deleted delegate seam. The spec records the coverage note.

- [ ] **Step 7: Refresh, compile, verify the characterization holds**

Refresh Unity. Run the EditMode filters `AlphaSeparationApplyTests`, `AlphaSeparationPreparationTests`, and `AmusePlatformFinishPluginTests`.
Expected: PASS on all three. Record the observed counts.

- [ ] **Step 8: Grep to zero**

Search `Packages/com.alrauna.amuse` for `AmuseBuildOperation`, `PrepareAmuseMutation`, and `ApplyAmuseMutation`.
Expected: no matches.

- [ ] **Step 9: Commit**

```bash
git add Packages/com.alrauna.amuse/Editor/Build/AmusePreparationDecision.cs \
        Packages/com.alrauna.amuse/Editor/Build/AmusePreparationDecision.cs.meta \
        Packages/com.alrauna.amuse/Editor/Build/AlphaSeparationApply.cs \
        Packages/com.alrauna.amuse/Tests/Editor/Build/AlphaSeparationApplyTests.cs
git commit -m "Inline the build operation sequence into the apply pass"
```

---

### Task 7: Full-suite validation

**Files:**
- None modified.

**Interfaces:**
- Consumes: all previous tasks.

- [ ] **Step 1: Run both full assemblies**

Refresh Unity. Run the full EditMode assemblies `Alrauna.Amuse.Tests.Editor` and `Alrauna.Amuse.Research.Tests.Editor` in one session.
Expected: PASS on both. Record the observed pass counts of each assembly.

- [ ] **Step 2: Final symbol sweep**

Search `Packages/com.alrauna.amuse` for every deleted symbol: `ProductOfTextureSamples`, `GetFirstTextureSample`, `GetFirstChannel`, `GetSecondTextureSample`, `GetSecondChannel`, `RequireProduct`, `ResolveProduct`, `AmuseBuildOperation`, `PrepareAmuseMutation`, `ApplyAmuseMutation`, `TryMapSampling`, `AlphaFilterMode`, `AlphaWrapMode`, `AlphaAnisoMode`, `TryGetBlockFloat`, `TryGetBlockColor`, `TryGetBlockVector`, `ContainsName`, `AmuseComponentPlacement`, `IsOnHierarchyRoot`.
Expected: no matches.

- [ ] **Step 3: Inspect the diff**

Run `git diff main --stat` and `git diff --check`.
Expected: only the files named in Tasks 1 to 6, plus `.meta` pairs. No whitespace errors.

- [ ] **Step 4: Report**

Report the observed counts of every focused run and both full assemblies. State any deviation from this plan and the reason.
