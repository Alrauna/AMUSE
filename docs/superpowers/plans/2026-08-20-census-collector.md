# Census Collector Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Convert one explicitly supplied Unity avatar root into tier 1 `ObservedAvatar` census records. Use AMUSE's real analysis pipeline, with no anonymization, aggregation, export, persistence, or discovery.

**Architecture:** A new Editor-only assembly `Alrauna.Amuse.Research.Editor` sits between Unity and the existing Unity-free `Alrauna.Amuse.Research.Census` assembly. `Alrauna.Amuse.Editor` grants internals to it and the research test assembly. Thus, the collector can call `UnityRendererAlphaAnalysis.Analyze`. The tests can directly name AMUSE's enums and semantics types. Traversal is `GetComponentsInChildren<Renderer>(true)` from a caller-supplied root. All counts come from the returned immutable plan. The collector adds no shader, material, or geometry logic.

**Tech Stack:** Unity 2022.3, C# (netstandard2.1 profile), NUnit via Unity Test Framework, EditMode tests only.

**Design:** `docs/superpowers/specs/2026-08-20-census-collector-design.md` **revision 2** (cited as **D §n**). Revision 2 applies the four changes required at architectural review. D §0 lists them. The supporting harness architecture is `docs/superpowers/specs/2026-08-20-avatar-census-harness-preparation-design.md` (**HP §n**).

## Global Constraints

- Branch `feat/census-collector` already exists from `main` at `fc8577d`. The design and plan are committed at `6c64218`. Do not switch branches.
- `Packages/manifest.json` and `Packages/packages-lock.json` have pre-existing, unrelated macOS toolchain changes in the working tree. **Never stage, revert, or commit them.** Always `git add` explicit paths. Never use `git add -A` or `git add .`.
- Baseline gate before any change: EditMode suite **770 passed / 0 failed / 0 skipped**. This plan adds 32 tests. The final gate is **802**.
- Confirm Unity instance identity before each reported test run. Normalized `Application.dataPath` must equal `<repo-root>/Assets`. Never hard-code that path in source, tests, or tooling. Derive it. The MCP connection dropped once during this branch. If a call reports no instance, confirm identity again before continuing. Do not assume that the same editor returned.
- **A plain asset refresh does not detect a newly created embedded package assembly.** After adding the new asmdef, run `UnityEditor.PackageManager.Client.Resolve()`, then refresh. Confirm that `Alrauna.Amuse.Research.Editor` appears in `AppDomain.CurrentDomain.GetAssemblies()` before trusting any test result. A filtered run that reports 0 tests is a failure, not a pass.
- Commit the `.meta` file with each new `.cs` and `.asmdef` file. Never separate or regenerate a `.meta`.
- Do not use the private Census Lab for any task in this plan.
- Add exactly two `InternalsVisibleTo` lines to `Packages/com.alrauna.amuse/Editor/AssemblyInfo.cs`. Name `Alrauna.Amuse.Research.Editor` and `Alrauna.Amuse.Research.Tests.Editor`. **This plan changes no other file in `com.alrauna.amuse`.**
- Do not modify `Alrauna.Amuse.Research.Census` in any task. It keeps `noEngineReferences: true` and zero references.
- **The only public type in `Alrauna.Amuse.Research.Editor` is `AvatarCensusCollector`. Its only public member is `Collect(GameObject, string)`** (D §5.1, review change 4). Everything else is `internal`. Task 5 verifies this.
- **Do not use reflection in production code** (D §5.4, review change 3). Reflection appears only in tests and only compares against a hardcoded literal.
- **Do not add a production type named for calibration or a seam parameter.** The single internal `RendererObservationBuilder.Build` overload of D §7.3 is the only exception (review change 2).
- Permit only **`GetAssetPath` and `AssetPathToGUID`** as `AssetDatabase` members in the research package (D §6.2). Ban all other members. Task 7 enforces this.
- Use namespace `Alrauna.Amuse.Research.Collection` for all new production code. Use namespace `Alrauna.Amuse.Research.Tests.Editor.Collection` for all new tests.
- Match the surrounding code style. Use 4-space indentation and `internal` by default. Add XML doc comments to every type that explain *why*. Add comments that record decisions, not comments that restate code.
- Stop and report if any D §9 stop condition appears.

---

## File Structure

**Created — production** (all under `Packages/com.alrauna.amuse.research/Editor/Collection/`)

| File | Responsibility |
|---|---|
| `Alrauna.Amuse.Research.Editor.asmdef` | Assembly definition |
| `AssemblyInfo.cs` | Grants this assembly's internals to the research test assembly |
| `CensusVocabulary.cs` | The three exhaustive enum mappings and the `RendererKind` mapping. Fully `internal` |
| `CensusShaderFamily.cs` | The explicitly declared two-branch attestation trial, memoized |
| `RendererObservationBuilder.cs` | One `Renderer` → one `ObservedRenderer`; also holds `CensusAssetIdentity` |
| `AvatarCensusCollector.cs` | The one public type: traversal, avatar identity |

**Created — tests** (all under `Packages/com.alrauna.amuse.research/Tests/Editor/Collection/`)

| File | Responsibility |
|---|---|
| `CollectorTestScene.cs` | Shared synthetic GameObject/mesh/material construction and teardown |
| `CensusVocabularyTests.cs` | Grant proof, mapping parity, frontend-set pin, attestation |
| `RendererRefusalCalibrationTests.cs` | The five CI calibration cases of D §7.1 |
| `AvatarCensusCollectorTests.cs` | Traversal, scoping, identity, immutability, no-discovery, minimal surface |
| `CollectorSeamCountingTests.cs` | `ProvenOpaque` and `MissingTextureEvidence` counting; **holds the semantics construction that revision 1 wrongly put in production** |
| `CollectorMutationSafetyTests.cs` | D §7.4 Layer 3 observable proof |
| `ResearchSourceApiBanTests.cs` | D §7.4 Layer 2 source scan |

**Modified**

| File | Change |
|---|---|
| `Packages/com.alrauna.amuse/Editor/AssemblyInfo.cs` | Two `InternalsVisibleTo` lines |
| `Packages/com.alrauna.amuse.research/Tests/Editor/Alrauna.Amuse.Research.Tests.Editor.asmdef` | Add `Alrauna.Amuse.Research.Editor` and `Alrauna.Amuse.Editor` references |

---

### Task 1: Assemblies, both grants, and the first mapping

Proves both friend grants before anything depends on them. A misconfigured grant causes confusing failures in every later task. Therefore, establish it separately first.

**Files:**
- Create: `Packages/com.alrauna.amuse.research/Editor/Collection/Alrauna.Amuse.Research.Editor.asmdef`
- Create: `Packages/com.alrauna.amuse.research/Editor/Collection/AssemblyInfo.cs`
- Create: `Packages/com.alrauna.amuse.research/Editor/Collection/CensusVocabulary.cs`
- Modify: `Packages/com.alrauna.amuse/Editor/AssemblyInfo.cs`
- Modify: `Packages/com.alrauna.amuse.research/Tests/Editor/Alrauna.Amuse.Research.Tests.Editor.asmdef`
- Test: `Packages/com.alrauna.amuse.research/Tests/Editor/Collection/CensusVocabularyTests.cs`

**Interfaces:**
- Consumes: nothing.
- Produces: assembly `Alrauna.Amuse.Research.Editor`, and `internal static Census.RendererRefusal CensusVocabulary.ToCensus(RendererAnalysisRefusal)`.

- [ ] **Step 1: Confirm the Unity instance and record the baseline**

Through Unity MCP `execute_code`:

```csharp
return UnityEngine.Application.dataPath;
```

Expected: a path whose normalized form equals `<repo-root>/Assets`. If it does not, or multiple instances are reachable, **stop and report**. Do not run tests.

Then run the full EditMode suite. Expected: 770 passed, 0 failed, 0 skipped.

- [ ] **Step 2: Create the assembly definition**

Create `Packages/com.alrauna.amuse.research/Editor/Collection/Alrauna.Amuse.Research.Editor.asmdef`:

```json
{
    "name": "Alrauna.Amuse.Research.Editor",
    "rootNamespace": "Alrauna.Amuse.Research.Collection",
    "references": [
        "Alrauna.Amuse.Research.Census",
        "Alrauna.Amuse.Editor"
    ],
    "includePlatforms": [
        "Editor"
    ],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": false,
    "precompiledReferences": [],
    "autoReferenced": false,
    "defineConstraints": [],
    "versionDefines": [],
    "noEngineReferences": false
}
```

- [ ] **Step 3: Add both AMUSE grants**

`Packages/com.alrauna.amuse/Editor/AssemblyInfo.cs` becomes exactly:

```csharp
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Alrauna.Amuse.Tests.Editor")]

// The census collector reads AMUSE's internal analysis results directly rather
// than through reflection: it is first-party, lives in this repository, is
// versioned and compiled together, and gains nothing from a run-time surface
// probe that only re-creates what the compiler already checks. It changes no
// public API and adds no production code. See
// docs/superpowers/specs/2026-08-20-avatar-census-harness-preparation-design.md §4.2.
[assembly: InternalsVisibleTo("Alrauna.Amuse.Research.Editor")]

// The census tests construct MaterialSemantics values to drive AMUSE's existing
// BaseMaterialSemanticsProvider seam, because the public development project
// installs no vendor shader and therefore cannot reach ProvenOpaque any other
// way. The alternative was a permanent calibration class inside the collector's
// production assembly — a hidden extension point whose only caller is a test.
// A test assembly ships in no build, so this is the narrower of the two. See
// docs/superpowers/specs/2026-08-20-census-collector-design.md §3.1.1.
[assembly: InternalsVisibleTo("Alrauna.Amuse.Research.Tests.Editor")]
```

- [ ] **Step 4: Grant the research assembly's internals to its own tests**

Create `Packages/com.alrauna.amuse.research/Editor/Collection/AssemblyInfo.cs`:

```csharp
using System.Runtime.CompilerServices;

// Issued by the research package to its own test assembly. The collector's
// entire surface is one public method, so without this the tests could not
// reach the vocabulary mappings, the attestation trial, or the renderer-level
// seam overload at all.
[assembly: InternalsVisibleTo("Alrauna.Amuse.Research.Tests.Editor")]
```

- [ ] **Step 5: Reference both assemblies from the test assembly**

In `Packages/com.alrauna.amuse.research/Tests/Editor/Alrauna.Amuse.Research.Tests.Editor.asmdef`, change `"references"` to:

```json
    "references": [
        "Alrauna.Amuse.Research.Census",
        "Alrauna.Amuse.Research.Editor",
        "Alrauna.Amuse.Editor"
    ],
```

Leave every other field unchanged.

- [ ] **Step 6: Make Unity see the new assembly**

Through Unity MCP `execute_code`:

```csharp
UnityEditor.PackageManager.Client.Resolve();
UnityEditor.AssetDatabase.Refresh();
return "resolved";
```

Then, use a **separate** call after compilation settles:

```csharp
var names = new System.Collections.Generic.List<string>();
foreach (var a in System.AppDomain.CurrentDomain.GetAssemblies())
{
    var n = a.GetName().Name;
    if (n.StartsWith("Alrauna")) names.Add(n);
}
return string.Join(",", names.ToArray());
```

Expected: the list contains `Alrauna.Amuse.Research.Editor`. If it does not, stop. You cannot trust anything below. Also check the console for compile errors.

- [ ] **Step 7: Write the failing grant test**

Create `Packages/com.alrauna.amuse.research/Tests/Editor/Collection/CensusVocabularyTests.cs`:

```csharp
using Alrauna.Amuse.Editor.Host;
using Alrauna.Amuse.Research.Census;
using Alrauna.Amuse.Research.Collection;
using NUnit.Framework;

namespace Alrauna.Amuse.Research.Tests.Editor.Collection
{
    /// <summary>
    /// Drift detection for the census vocabulary. Every mirror in
    /// CensusCategories is a snapshot of AMUSE's own enums; these tests are the
    /// half of the snapshot contract that watches AMUSE rather than the census,
    /// and they are the reason a new AMUSE value fails loudly in CI instead of
    /// being miscounted in a private run.
    /// </summary>
    public sealed class CensusVocabularyTests
    {
        [Test]
        public void FriendGrantsExposeAmuseInternalsToBothResearchAssemblies()
        {
            // Naming RendererAnalysisRefusal here proves the grant to the test
            // assembly; this file would not compile without it. CensusVocabulary
            // names the same internal enum in its own signature, so it could not
            // compile without the grant to the collector assembly. Asserted as
            // behaviour rather than assumed: if either grant is missing, every
            // later collector test fails for a reason that looks unrelated.
            Assert.That(
                CensusVocabulary.ToCensus(RendererAnalysisRefusal.None),
                Is.EqualTo(RendererRefusal.None));
        }
    }
}
```

- [ ] **Step 8: Run it and verify it fails to compile**

Run the EditMode suite filtered to `Alrauna.Amuse.Research.Tests.Editor`.
Expected: a **compile error** that names `CensusVocabulary`, not a test failure. A compile error is the correct red here. The type does not exist yet.

- [ ] **Step 9: Create the first mapping**

Create `Packages/com.alrauna.amuse.research/Editor/Collection/CensusVocabulary.cs`:

```csharp
using System;
using Alrauna.Amuse.Editor.Host;
using Census = Alrauna.Amuse.Research.Census;

namespace Alrauna.Amuse.Research.Collection
{
    /// <summary>
    /// The census vocabulary's AMUSE-facing half: total mappings from AMUSE's
    /// internal enums onto their census mirrors.
    /// <para>
    /// No mapping has a default arm. An AMUSE value with no census counterpart
    /// throws rather than being folded into an existing category, because a
    /// stopped run is better than a silent miscount — the same rule
    /// <c>CensusAnonymizer.ShaderFamily</c> already applies on the other side of
    /// the pipeline.
    /// </para>
    /// </summary>
    internal static class CensusVocabulary
    {
        internal static Census.RendererRefusal ToCensus(
            RendererAnalysisRefusal refusal)
        {
            switch (refusal)
            {
                case RendererAnalysisRefusal.None:
                    return Census.RendererRefusal.None;
                case RendererAnalysisRefusal.UnsupportedRendererType:
                    return Census.RendererRefusal.UnsupportedRendererType;
                case RendererAnalysisRefusal.MaterialPropertyOverridesPresent:
                    return Census.RendererRefusal
                        .MaterialPropertyOverridesPresent;
                case RendererAnalysisRefusal.MissingMesh:
                    return Census.RendererRefusal.MissingMesh;
                case RendererAnalysisRefusal.UnprovenMaterialSlotMapping:
                    return Census.RendererRefusal.UnprovenMaterialSlotMapping;
                case RendererAnalysisRefusal.UnsupportedTopology:
                    return Census.RendererRefusal.UnsupportedTopology;
                case RendererAnalysisRefusal.MalformedMeshData:
                    return Census.RendererRefusal.MalformedMeshData;
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(refusal),
                        "Unmapped AMUSE renderer refusal: " + refusal);
            }
        }
    }
}
```

- [ ] **Step 10: Refresh and run the test**

Refresh Unity, then run `CensusVocabularyTests`.
Expected: 1 test, PASS. **A run that reports 0 tests is a failure.** Return to Step 6.

- [ ] **Step 11: Inspect the diff, then commit**

```bash
git status --short
```

Expected: the new and modified files above, plus their new `.meta` files. `Packages/manifest.json` and `Packages/packages-lock.json` remain modified and **not staged**.

```bash
git add Packages/com.alrauna.amuse/Editor/AssemblyInfo.cs Packages/com.alrauna.amuse.research/Editor/Collection Packages/com.alrauna.amuse.research/Editor/Collection.meta Packages/com.alrauna.amuse.research/Tests/Editor/Alrauna.Amuse.Research.Tests.Editor.asmdef Packages/com.alrauna.amuse.research/Tests/Editor/Collection Packages/com.alrauna.amuse.research/Tests/Editor/Collection.meta
git commit -m "feat(research): add collector assembly and the AMUSE friend grants"
```

---

### Task 2: The remaining mappings and enum drift detection

Locks the census enums to AMUSE's before any counting code exists. Thus, mapping gaps cause compile-time or test-time failures instead of silent recategorization.

**Files:**
- Modify: `Packages/com.alrauna.amuse.research/Editor/Collection/CensusVocabulary.cs`
- Test: `Packages/com.alrauna.amuse.research/Tests/Editor/Collection/CensusVocabularyTests.cs`

**Interfaces:**
- Consumes: `CensusVocabulary.ToCensus(RendererAnalysisRefusal)` from Task 1.
- Produces:
  - `internal static Census.AlphaResolutionFailure CensusVocabulary.ToCensus(AlphaResolutionFailure)`
  - `internal static Census.SeparationDisposition CensusVocabulary.ToCensus(SubmeshSeparationDisposition)`
  - `internal static Census.RendererKind CensusVocabulary.KindOf(Renderer)`

- [ ] **Step 1: Write the failing drift tests**

Append to `CensusVocabularyTests`, inside the class:

```csharp
        [Test]
        public void RendererRefusalMirrorsAmuse()
        {
            CollectionAssert.AreEquivalent(
                System.Enum.GetNames(typeof(RendererRefusal)),
                System.Enum.GetNames(typeof(RendererAnalysisRefusal)));
        }

        [Test]
        public void AlphaResolutionFailureMirrorsAmuse()
        {
            CollectionAssert.AreEquivalent(
                System.Enum.GetNames(typeof(AlphaResolutionFailure)),
                System.Enum.GetNames(
                    typeof(Alrauna.Amuse.Editor.Analysis
                        .AlphaResolutionFailure)));
        }

        [Test]
        public void SeparationDispositionMirrorsAmuse()
        {
            CollectionAssert.AreEquivalent(
                System.Enum.GetNames(typeof(SeparationDisposition)),
                System.Enum.GetNames(
                    typeof(Alrauna.Amuse.Editor.Analysis
                        .SubmeshSeparationDisposition)));
        }
```

`AlphaResolutionFailure` is deliberately ambiguous between the census and AMUSE namespaces. Therefore, fully qualify the AMUSE side. The census side comes from the file's `using`. It is in `Alrauna.Amuse.Editor.Analysis`, beside `AlphaSemanticsResolver`. It is **not** in `.Semantics`. Do not add a `using` for either.

- [ ] **Step 2: Run and verify failure**

Run `CensusVocabularyTests`.
Expected: the three new tests **fail or the file fails to compile**. If all three pass immediately, that is fine and expected. They compare AMUSE against the census schema, which the schema branch already got right. The red state that matters is Step 4's.

- [ ] **Step 3: Write the failing mapping tests**

Append:

```csharp
        [Test]
        public void EveryAmuseAlphaFailureMaps()
        {
            // Exhaustiveness, checked by driving every value through the
            // mapping. A missing arm throws rather than guessing, so a gap
            // surfaces here rather than as a miscategorized census row.
            foreach (Alrauna.Amuse.Editor.Analysis.AlphaResolutionFailure value
                     in System.Enum.GetValues(
                         typeof(Alrauna.Amuse.Editor.Analysis
                             .AlphaResolutionFailure)))
            {
                Assert.That(
                    System.Enum.IsDefined(
                        typeof(AlphaResolutionFailure),
                        CensusVocabulary.ToCensus(value)),
                    Is.True,
                    "Unmapped: " + value);
            }
        }

        [Test]
        public void EveryAmuseDispositionMaps()
        {
            foreach (Alrauna.Amuse.Editor.Analysis.SubmeshSeparationDisposition
                         value in System.Enum.GetValues(
                             typeof(Alrauna.Amuse.Editor.Analysis
                                 .SubmeshSeparationDisposition)))
            {
                Assert.That(
                    System.Enum.IsDefined(
                        typeof(SeparationDisposition),
                        CensusVocabulary.ToCensus(value)),
                    Is.True,
                    "Unmapped: " + value);
            }
        }
```

- [ ] **Step 4: Run and verify failure**

Expected: compile error that names the two missing `ToCensus` overloads.

- [ ] **Step 5: Implement the remaining mappings**

Add to `CensusVocabulary.cs`. Extend the `using` block with `using Alrauna.Amuse.Editor.Analysis;`, `using Alrauna.Amuse.Editor.Semantics;`, and `using UnityEngine;`. Then add inside the class:

```csharp
        internal static Census.AlphaResolutionFailure ToCensus(
            AlphaResolutionFailure failure)
        {
            switch (failure)
            {
                case AlphaResolutionFailure.None:
                    return Census.AlphaResolutionFailure.None;
                case AlphaResolutionFailure.SemanticsUnknown:
                    return Census.AlphaResolutionFailure.SemanticsUnknown;
                case AlphaResolutionFailure.UnsupportedMultiplier:
                    return Census.AlphaResolutionFailure.UnsupportedMultiplier;
                case AlphaResolutionFailure.UnsupportedUvMapping:
                    return Census.AlphaResolutionFailure.UnsupportedUvMapping;
                case AlphaResolutionFailure.UnsupportedSampling:
                    return Census.AlphaResolutionFailure.UnsupportedSampling;
                case AlphaResolutionFailure.MissingTextureEvidence:
                    return Census.AlphaResolutionFailure.MissingTextureEvidence;
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(failure),
                        "Unmapped AMUSE alpha resolution failure: " + failure);
            }
        }

        internal static Census.SeparationDisposition ToCensus(
            SubmeshSeparationDisposition disposition)
        {
            switch (disposition)
            {
                case SubmeshSeparationDisposition.Unchanged:
                    return Census.SeparationDisposition.Unchanged;
                case SubmeshSeparationDisposition.WhollyOpaqueCandidate:
                    return Census.SeparationDisposition.WhollyOpaqueCandidate;
                case SubmeshSeparationDisposition.Split:
                    return Census.SeparationDisposition.Split;
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(disposition),
                        "Unmapped AMUSE separation disposition: " + disposition);
            }
        }

        /// <summary>
        /// The one mapping that is deliberately not one-to-one. Everything AMUSE
        /// does not analyze collapses to <c>Other</c> rather than throwing,
        /// because an unsupported renderer type is an observation the census
        /// exists to count, not a defect.
        /// </summary>
        internal static Census.RendererKind KindOf(Renderer renderer)
        {
            if (renderer is SkinnedMeshRenderer)
                return Census.RendererKind.SkinnedMeshRenderer;
            if (renderer is MeshRenderer)
                return Census.RendererKind.MeshRenderer;

            return Census.RendererKind.Other;
        }
    }
}
```

The added `using` directives can make `AlphaResolutionFailure` ambiguous inside `CensusVocabulary.cs`. If so, keep the AMUSE `using`. Qualify the census side as `Census.AlphaResolutionFailure`, which the existing `Census` alias already provides.

- [ ] **Step 6: Refresh and run**

Run `CensusVocabularyTests`.
Expected: 6 tests, all PASS. Confirm the count is 6 and not 0.

- [ ] **Step 7: Commit**

```bash
git add Packages/com.alrauna.amuse.research/Editor/Collection/CensusVocabulary.cs Packages/com.alrauna.amuse.research/Tests/Editor/Collection/CensusVocabularyTests.cs
git commit -m "feat(research): map AMUSE analysis vocabulary onto census categories"
```

---

### Task 3: Shader family attestation, declared explicitly

**Files:**
- Create: `Packages/com.alrauna.amuse.research/Editor/Collection/CensusShaderFamily.cs`
- Test: `Packages/com.alrauna.amuse.research/Tests/Editor/Collection/CensusVocabularyTests.cs` (append)

**Interfaces:**
- Consumes: nothing from earlier tasks.
- Produces: `internal sealed class CensusShaderFamily` with `internal Census.ShaderFamilyAttestation Of(Material material)`. Use one instance per collection run. The memo lives and dies with it.

- [ ] **Step 1: Write the failing tests**

Append to `CensusVocabularyTests`:

```csharp
        [Test]
        public void UnattestedMaterialHasNoShaderFamily()
        {
            var material = new UnityEngine.Material(
                UnityEngine.Shader.Find("Standard"));
            try
            {
                Assert.That(
                    new CensusShaderFamily().Of(material),
                    Is.EqualTo(ShaderFamilyAttestation.None));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(material);
            }
        }

        [Test]
        public void NullMaterialHasNoShaderFamily()
        {
            // An empty material slot is an ordinary observation, not an error.
            Assert.That(
                new CensusShaderFamily().Of(null),
                Is.EqualTo(ShaderFamilyAttestation.None));
        }

        [Test]
        public void AmuseDeclaresNoShaderFrontendTheCensusDoesNotMeasure()
        {
            // The census names Poiyomi and lilToon directly in its attestation
            // trial; production depends on no naming convention. This is the
            // other half of that bargain: a pin against a literal, so a third
            // vendor adapter fails here in the commit that adds it and a person
            // decides whether the census should measure it.
            //
            // Blind spot, recorded rather than hidden: a frontend added inside
            // an existing vendor namespace creates no new namespace and would
            // not fail this test.
            var namespaces = new System.Collections.Generic.SortedSet<string>(
                System.StringComparer.Ordinal);
            foreach (var type in typeof(RendererAnalysisRefusal).Assembly
                         .GetTypes())
            {
                if (type.Namespace == null) continue;
                if (!type.Namespace.StartsWith(
                        "Alrauna.Amuse.Editor.Semantics.",
                        System.StringComparison.Ordinal))
                {
                    continue;
                }

                namespaces.Add(type.Namespace);
            }

            CollectionAssert.AreEqual(
                new[]
                {
                    "Alrauna.Amuse.Editor.Semantics.LilToon",
                    "Alrauna.Amuse.Editor.Semantics.Poiyomi",
                },
                namespaces);
        }
```

- [ ] **Step 2: Run and verify failure**

Expected: compile error that names `CensusShaderFamily`.

- [ ] **Step 3: Implement**

Create `Packages/com.alrauna.amuse.research/Editor/Collection/CensusShaderFamily.cs`:

```csharp
using System.Collections.Generic;
using Alrauna.Amuse.Editor.Semantics.LilToon;
using Alrauna.Amuse.Editor.Semantics.Poiyomi;
using UnityEngine;
using Census = Alrauna.Amuse.Research.Census;

namespace Alrauna.Amuse.Research.Collection
{
    /// <summary>
    /// Which AMUSE shader frontend attests a material, or none.
    /// <para>
    /// The two families the census measures are named here, directly, and the
    /// compiler checks them. Nothing is discovered: an earlier draft reflected a
    /// namespace for a method called <c>AnalyzeBaseMaterial</c>, which made the
    /// census's own vocabulary depend on AMUSE's folder and naming conventions,
    /// so a rename that changed nothing semantically could silently change what
    /// was measured. <c>CensusVocabularyTests</c> pins the frontend set instead.
    /// </para>
    /// <para>
    /// AMUSE's own selector runs this same exclusive trial and then discards
    /// which frontend answered, and the census's highest-value number is exactly
    /// that answer. Repeating the trial here costs a second attestation per
    /// distinct material; changing AMUSE to report it would be a production
    /// result-object change made solely so the census could measure something,
    /// which the harness design forbids.
    /// </para>
    /// <para>
    /// Attestation hashes the whole normalized shader source, and avatars repeat
    /// material references across slots and renderers, so the memo removes real
    /// repeated work. It is scoped to one collection run and discarded with it;
    /// it holds material references and must not outlive the run.
    /// </para>
    /// </summary>
    internal sealed class CensusShaderFamily
    {
        private readonly Dictionary<Material, Census.ShaderFamilyAttestation>
            _memo = new Dictionary<Material, Census.ShaderFamilyAttestation>();

        internal Census.ShaderFamilyAttestation Of(Material material)
        {
            // Unity's overloaded equality reports a destroyed object as null.
            // Both frontends throw on a shaderless material, and the answer for
            // one is None either way.
            if (material == null || material.shader == null)
            {
                return Census.ShaderFamilyAttestation.None;
            }

            if (_memo.TryGetValue(material, out var cached))
            {
                return cached;
            }

            // AMUSE's own trial order, restated. No material can be attested by
            // both frontends, so the order affects cost, not the answer.
            var family = Census.ShaderFamilyAttestation.None;
            if (PoiyomiMaterialSemantics
                .AnalyzeBaseMaterial(material).IsSupportedMaterial)
            {
                family = Census.ShaderFamilyAttestation.Poiyomi;
            }
            else if (LilToonMaterialSemantics
                     .AnalyzeBaseMaterial(material).IsSupportedMaterial)
            {
                family = Census.ShaderFamilyAttestation.LilToon;
            }

            _memo[material] = family;
            return family;
        }
    }
}
```

- [ ] **Step 4: Run**

Expected: 9 tests in `CensusVocabularyTests`, all PASS.

If `AmuseDeclaresNoShaderFrontendTheCensusDoesNotMeasure` fails, **stop and report**. This drift signal means D §5.4's assumption no longer holds.

- [ ] **Step 5: Commit**

```bash
git add Packages/com.alrauna.amuse.research/Editor/Collection/CensusShaderFamily.cs Packages/com.alrauna.amuse.research/Tests/Editor/Collection/CensusVocabularyTests.cs
git commit -m "feat(research): record which AMUSE shader frontend attests a material"
```

---

### Task 4: Refused-renderer observation and the null-versus-zero rule

The harness design identifies this as the most likely miscount in the whole system. Therefore, build and prove it before any success path exists.

**Files:**
- Create: `Packages/com.alrauna.amuse.research/Editor/Collection/RendererObservationBuilder.cs`
- Create: `Packages/com.alrauna.amuse.research/Tests/Editor/Collection/CollectorTestScene.cs`
- Create: `Packages/com.alrauna.amuse.research/Tests/Editor/Collection/RendererRefusalCalibrationTests.cs`

**Interfaces:**
- Consumes: `CensusVocabulary.ToCensus`, `CensusVocabulary.KindOf`, `CensusShaderFamily`.
- Produces: `internal static class RendererObservationBuilder` with two overloads that mirror `UnityRendererAlphaAnalysis.Analyze`:
  - `internal static Census.ObservedRenderer Build(Renderer renderer, string hierarchyPath, CensusShaderFamily families)`
  - `internal static Census.ObservedRenderer Build(Renderer renderer, string hierarchyPath, CensusShaderFamily families, BaseMaterialSemanticsProvider semanticsProvider)`
- Produces: `internal static class CensusAssetIdentity` with `PathOf(Object)` and `GuidOf(Object)`.
- Produces test helper `CollectorTestScene` with `NewRoot`, `NewChild`, `NewTriangleMesh`, `NewQuadMesh`, `NewStandardMaterial`, `NewMeshRenderer`, `Destroy`.

- [ ] **Step 1: Write the shared test scene helper**

Create `Packages/com.alrauna.amuse.research/Tests/Editor/Collection/CollectorTestScene.cs`:

```csharp
using System.Collections.Generic;
using UnityEngine;

namespace Alrauna.Amuse.Research.Tests.Editor.Collection
{
    /// <summary>
    /// Synthetic Unity objects for collector tests: built in code, tracked, and
    /// destroyed in teardown. Nothing here is imported, saved, or written to the
    /// project, so the calibration cases need no fixture asset and no avatar.
    /// </summary>
    internal sealed class CollectorTestScene
    {
        private readonly List<Object> _created = new List<Object>();

        internal GameObject NewRoot(string name)
        {
            var root = new GameObject(name);
            _created.Add(root);
            return root;
        }

        internal GameObject NewChild(GameObject parent, string name)
        {
            var child = new GameObject(name);
            child.transform.SetParent(parent.transform, false);
            // Parented objects are destroyed with their root; tracking the root
            // alone is enough and double-tracking would double-destroy.
            return child;
        }

        /// <summary>
        /// A mesh of <paramref name="submeshCount"/> triangle submeshes, one
        /// triangle each, with UV0 present. Vertices are distinct per submesh so
        /// no submesh shares an index range with another.
        /// </summary>
        internal Mesh NewTriangleMesh(int submeshCount)
        {
            var mesh = new Mesh { name = "CensusTestTriangles" };
            var vertices = new Vector3[submeshCount * 3];
            var uv = new Vector2[submeshCount * 3];
            for (var i = 0; i < submeshCount; i++)
            {
                vertices[i * 3] = new Vector3(i, 0f, 0f);
                vertices[i * 3 + 1] = new Vector3(i, 1f, 0f);
                vertices[i * 3 + 2] = new Vector3(i + 1f, 0f, 0f);
                uv[i * 3] = new Vector2(0f, 0f);
                uv[i * 3 + 1] = new Vector2(0f, 1f);
                uv[i * 3 + 2] = new Vector2(1f, 0f);
            }

            mesh.vertices = vertices;
            mesh.uv = uv;
            mesh.subMeshCount = submeshCount;
            for (var i = 0; i < submeshCount; i++)
            {
                mesh.SetTriangles(
                    new[] { i * 3, i * 3 + 1, i * 3 + 2 }, i, false);
            }

            _created.Add(mesh);
            return mesh;
        }

        /// <summary>One submesh of quad topology, which AMUSE refuses.</summary>
        internal Mesh NewQuadMesh()
        {
            var mesh = new Mesh { name = "CensusTestQuad" };
            mesh.vertices = new[]
            {
                new Vector3(0f, 0f, 0f), new Vector3(0f, 1f, 0f),
                new Vector3(1f, 1f, 0f), new Vector3(1f, 0f, 0f),
            };
            mesh.subMeshCount = 1;
            mesh.SetIndices(
                new[] { 0, 1, 2, 3 }, MeshTopology.Quads, 0, false);
            _created.Add(mesh);
            return mesh;
        }

        internal Material NewStandardMaterial()
        {
            var material = new Material(Shader.Find("Standard"))
            {
                name = "CensusTestStandard",
            };
            _created.Add(material);
            return material;
        }

        internal GameObject NewMeshRenderer(
            GameObject parent, string name, Mesh mesh, params Material[] materials)
        {
            var go = NewChild(parent, name);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterials = materials;
            return go;
        }

        internal void Destroy()
        {
            for (var i = _created.Count - 1; i >= 0; i--)
            {
                if (_created[i] != null)
                {
                    Object.DestroyImmediate(_created[i]);
                }
            }

            _created.Clear();
        }
    }
}
```

- [ ] **Step 2: Write the failing calibration tests**

Create `Packages/com.alrauna.amuse.research/Tests/Editor/Collection/RendererRefusalCalibrationTests.cs`:

```csharp
using Alrauna.Amuse.Research.Census;
using Alrauna.Amuse.Research.Collection;
using NUnit.Framework;
using UnityEngine;

namespace Alrauna.Amuse.Research.Tests.Editor.Collection
{
    /// <summary>
    /// The five calibration cases that run without a vendor shader. Each asserts
    /// that the collector <em>counts</em> a known AMUSE outcome correctly; that
    /// the outcome is reachable in a real project is a separate claim, and for
    /// these five the two collapse because the case is constructed directly.
    /// </summary>
    public sealed class RendererRefusalCalibrationTests
    {
        private CollectorTestScene _scene;

        [SetUp]
        public void SetUp() => _scene = new CollectorTestScene();

        [TearDown]
        public void TearDown() => _scene.Destroy();

        private ObservedRenderer Observe(Renderer renderer)
        {
            return RendererObservationBuilder.Build(
                renderer, "Path", new CensusShaderFamily());
        }

        [Test]
        public void UnsupportedRendererTypeHasUnknownCountsNotZero()
        {
            // The single most likely miscount in the system: a refusal with no
            // reachable mesh must record null, never 0. Zero here understates
            // avatar complexity and overstates coverage in every aggregate.
            var root = _scene.NewRoot("Line");
            var renderer = root.AddComponent<LineRenderer>();

            var observed = Observe(renderer);

            Assert.That(
                observed.Refusal,
                Is.EqualTo(RendererRefusal.UnsupportedRendererType));
            Assert.That(observed.SubmeshCount, Is.Null);
            Assert.That(observed.TriangleCount, Is.Null);
            Assert.That(observed.Submeshes, Is.Empty);
            Assert.That(observed.Kind, Is.EqualTo(RendererKind.Other));
        }

        [Test]
        public void MissingMeshHasUnknownCountsNotZero()
        {
            var root = _scene.NewRoot("NoMesh");
            root.AddComponent<MeshFilter>();
            var renderer = root.AddComponent<MeshRenderer>();

            var observed = Observe(renderer);

            Assert.That(
                observed.Refusal, Is.EqualTo(RendererRefusal.MissingMesh));
            Assert.That(observed.SubmeshCount, Is.Null);
            Assert.That(observed.TriangleCount, Is.Null);
        }

        [Test]
        public void UnsupportedTopologyKnowsSubmeshesButNotTriangles()
        {
            // A quad submesh has no triangle count. Any number written there
            // would be an invention, so the honest record is null for triangles
            // and a real count for submeshes.
            var root = _scene.NewRoot("Quads");
            var go = _scene.NewMeshRenderer(
                root, "Quad", _scene.NewQuadMesh(),
                _scene.NewStandardMaterial());

            var observed = Observe(go.GetComponent<MeshRenderer>());

            Assert.That(
                observed.Refusal,
                Is.EqualTo(RendererRefusal.UnsupportedTopology));
            Assert.That(observed.SubmeshCount, Is.EqualTo(1));
            Assert.That(observed.TriangleCount, Is.Null);
        }

        [Test]
        public void PropertyBlockRefusalStillCountsTheMesh()
        {
            var root = _scene.NewRoot("Block");
            var go = _scene.NewMeshRenderer(
                root, "Blocked", _scene.NewTriangleMesh(1),
                _scene.NewStandardMaterial());
            var renderer = go.GetComponent<MeshRenderer>();
            var block = new MaterialPropertyBlock();
            block.SetFloat("_Cutoff", 0.25f);
            renderer.SetPropertyBlock(block);

            var observed = Observe(renderer);

            Assert.That(
                observed.Refusal,
                Is.EqualTo(RendererRefusal.MaterialPropertyOverridesPresent));
            Assert.That(observed.SubmeshCount, Is.EqualTo(1));
            Assert.That(observed.TriangleCount, Is.EqualTo(1));
            Assert.That(observed.Submeshes, Is.Empty);
        }

        [Test]
        public void UnprovenSlotMappingStillCountsTheMesh()
        {
            var root = _scene.NewRoot("Slots");
            var go = _scene.NewMeshRenderer(
                root, "TwoSubmeshesOneMaterial", _scene.NewTriangleMesh(2),
                _scene.NewStandardMaterial());

            var observed = Observe(go.GetComponent<MeshRenderer>());

            Assert.That(
                observed.Refusal,
                Is.EqualTo(RendererRefusal.UnprovenMaterialSlotMapping));
            Assert.That(observed.SubmeshCount, Is.EqualTo(2));
            Assert.That(observed.TriangleCount, Is.EqualTo(2));
        }

        [Test]
        public void UnattestedMaterialAnalyzesToAllUnknownTriangles()
        {
            var root = _scene.NewRoot("Standard");
            var go = _scene.NewMeshRenderer(
                root, "Plain", _scene.NewTriangleMesh(1),
                _scene.NewStandardMaterial());

            var observed = Observe(go.GetComponent<MeshRenderer>());

            Assert.That(observed.Refusal, Is.EqualTo(RendererRefusal.None));
            Assert.That(
                observed.Kind, Is.EqualTo(RendererKind.MeshRenderer));
            Assert.That(observed.SubmeshCount, Is.EqualTo(1));
            Assert.That(observed.TriangleCount, Is.EqualTo(1));
            Assert.That(observed.Submeshes.Count, Is.EqualTo(1));

            var submesh = observed.Submeshes[0];
            Assert.That(submesh.HasMaterial, Is.True);
            Assert.That(
                submesh.AlphaFailure,
                Is.EqualTo(AlphaResolutionFailure.SemanticsUnknown));
            Assert.That(
                submesh.Disposition,
                Is.EqualTo(SeparationDisposition.Unchanged));
            Assert.That(submesh.TriangleCount, Is.EqualTo(1));
            Assert.That(submesh.UnknownTriangleCount, Is.EqualTo(1));
            Assert.That(submesh.ProvenOpaqueTriangleCount, Is.EqualTo(0));
            Assert.That(
                submesh.ShaderFamilyAttestation,
                Is.EqualTo(ShaderFamilyAttestation.None));
        }
    }
}
```

- [ ] **Step 3: Run and verify failure**

Expected: compile error that names `RendererObservationBuilder`.

- [ ] **Step 4: Implement the builder**

Create `Packages/com.alrauna.amuse.research/Editor/Collection/RendererObservationBuilder.cs`:

```csharp
using System;
using System.Collections.Generic;
using Alrauna.Amuse.Editor.Analysis;
using Alrauna.Amuse.Editor.Host;
using UnityEditor;
using UnityEngine;
using Census = Alrauna.Amuse.Research.Census;

namespace Alrauna.Amuse.Research.Collection
{
    /// <summary>
    /// One Unity renderer to one tier 1 <c>ObservedRenderer</c>.
    /// <para>
    /// Every count on the analyzed path comes from the plan AMUSE returned;
    /// nothing is recomputed from geometry. The only Unity state read beyond
    /// what <c>Analyze</c> reads is the shared mesh, for the counts a refused
    /// renderer would otherwise lose, and the shared materials, for tier 1
    /// identity.
    /// </para>
    /// <para>
    /// It never catches an analysis exception. <c>Analyze</c> throws only for a
    /// null or destroyed renderer, neither of which hierarchy traversal can
    /// produce, so an exception means a collector defect — and a census that
    /// records its own defects as data produces a confident wrong number.
    /// </para>
    /// </summary>
    internal static class RendererObservationBuilder
    {
        /// <summary>The production path.</summary>
        internal static Census.ObservedRenderer Build(
            Renderer renderer,
            string hierarchyPath,
            CensusShaderFamily families)
        {
            return Build(renderer, hierarchyPath, families, null);
        }

        /// <summary>
        /// The same observation, with AMUSE's own semantics seam substituted.
        /// <para>
        /// This overload exists because the public development project installs
        /// no vendor shader, so <c>ProvenOpaque</c> and
        /// <c>MissingTextureEvidence</c> are otherwise unreachable in CI and the
        /// collector's counting of them could not be validated at all. It is a
        /// straight pass-through to the two-overload shape
        /// <c>UnityRendererAlphaAnalysis.Analyze</c> already ships for its own
        /// integration tests — not a new extension point. It is internal, no
        /// public caller can name it, and nothing in the collector's own public
        /// surface carries a provider parameter.
        /// </para>
        /// </summary>
        internal static Census.ObservedRenderer Build(
            Renderer renderer,
            string hierarchyPath,
            CensusShaderFamily families,
            BaseMaterialSemanticsProvider semanticsProvider)
        {
            var analysis = semanticsProvider == null
                ? UnityRendererAlphaAnalysis.Analyze(renderer)
                : UnityRendererAlphaAnalysis.Analyze(
                    renderer, semanticsProvider);

            var kind = CensusVocabulary.KindOf(renderer);
            var refusal = CensusVocabulary.ToCensus(analysis.Refusal);

            if (analysis.Refusal != RendererAnalysisRefusal.None)
            {
                CountRefusedMesh(
                    SharedMeshOf(renderer),
                    out var submeshCount,
                    out var triangleCount);

                return new Census.ObservedRenderer(
                    hierarchyPath,
                    renderer.gameObject.name,
                    renderer.GetType().Name,
                    kind,
                    refusal,
                    submeshCount,
                    triangleCount,
                    Array.Empty<Census.ObservedSubmesh>());
            }

            var plan = analysis.Plan;

            // Index-parallel by construction. Asserted rather than assumed,
            // because every count below indexes all three.
            if (plan.Submeshes.Count != analysis.Submeshes.Count ||
                plan.Source.Submeshes.Count != analysis.Submeshes.Count)
            {
                throw new InvalidOperationException(
                    "AMUSE returned mismatched submesh lists; the census cannot "
                    + "align them without guessing.");
            }

            var materials = renderer.sharedMaterials;
            var submeshes = new List<Census.ObservedSubmesh>(
                analysis.Submeshes.Count);
            var totalTriangles = 0;
            var totalOpaque = 0;
            var totalNonOpaque = 0;

            for (var index = 0; index < analysis.Submeshes.Count; index++)
            {
                var record = analysis.Submeshes[index];
                var outcomes = plan.Source.Submeshes[index].Outcomes;

                var opaque = 0;
                var transparent = 0;
                var unknown = 0;
                for (var i = 0; i < outcomes.Count; i++)
                {
                    switch (outcomes[i])
                    {
                        case TriangleAlphaOutcome.ProvenOpaque:
                            opaque++;
                            break;
                        case TriangleAlphaOutcome.MustRemainTransparent:
                            transparent++;
                            break;
                        case TriangleAlphaOutcome.Unknown:
                            unknown++;
                            break;
                        default:
                            throw new ArgumentOutOfRangeException(
                                nameof(renderer),
                                "Unmapped AMUSE triangle outcome: "
                                + outcomes[i]);
                    }
                }

                var material =
                    record.MaterialSlotIndex >= 0 &&
                    record.MaterialSlotIndex < materials.Length
                        ? materials[record.MaterialSlotIndex]
                        : null;

                submeshes.Add(new Census.ObservedSubmesh(
                    record.SubmeshIndex,
                    record.MaterialSlotIndex,
                    record.HasMaterial,
                    material == null ? null : material.name,
                    CensusAssetIdentity.PathOf(material),
                    CensusAssetIdentity.GuidOf(material),
                    material == null || material.shader == null
                        ? null
                        : material.shader.name,
                    families.Of(material),
                    CensusVocabulary.ToCensus(record.Failure),
                    CensusVocabulary.ToCensus(plan.Submeshes[index].Disposition),
                    outcomes.Count,
                    opaque,
                    transparent,
                    unknown));

                totalTriangles += outcomes.Count;
                totalOpaque += opaque;
                totalNonOpaque += transparent + unknown;
            }

            // The load-bearing invariant: the census's own tally, derived from
            // per-triangle outcomes, against a number MeshSeparationPlanner
            // computed independently. A misattribution bug cannot agree with
            // itself across both. Note the asymmetry — AMUSE's transparent count
            // is everything that is not ProvenOpaque, so Unknown is on that side.
            if (totalOpaque != plan.OpaqueTriangleCount ||
                totalNonOpaque != plan.TransparentTriangleCount)
            {
                throw new InvalidOperationException(
                    "Census triangle tally disagrees with the AMUSE separation "
                    + "plan: counted " + totalOpaque + " opaque and "
                    + totalNonOpaque + " non-opaque against the plan's "
                    + plan.OpaqueTriangleCount + " and "
                    + plan.TransparentTriangleCount + ".");
            }

            return new Census.ObservedRenderer(
                hierarchyPath,
                renderer.gameObject.name,
                renderer.GetType().Name,
                kind,
                refusal,
                submeshes.Count,
                totalTriangles,
                submeshes);
        }

        /// <summary>
        /// The counts a refused renderer would otherwise lose. Unknown is
        /// recorded as null and never as zero: zero understates avatar
        /// complexity and overstates coverage in every aggregate downstream.
        /// <para>
        /// A non-triangle submesh has no triangle count, so a mesh containing
        /// one yields a known submesh count and an unknown triangle count rather
        /// than an invented number.
        /// </para>
        /// </summary>
        private static void CountRefusedMesh(
            Mesh mesh, out int? submeshCount, out int? triangleCount)
        {
            if (mesh == null)
            {
                submeshCount = null;
                triangleCount = null;
                return;
            }

            submeshCount = mesh.subMeshCount;

            var triangles = 0;
            for (var index = 0; index < mesh.subMeshCount; index++)
            {
                if (mesh.GetTopology(index) != MeshTopology.Triangles)
                {
                    triangleCount = null;
                    return;
                }

                // GetIndexCount rather than GetIndices: the count is all that is
                // wanted and GetIndices would allocate the whole index buffer.
                triangles += (int)(mesh.GetIndexCount(index) / 3);
            }

            triangleCount = triangles;
        }

        /// <summary>
        /// The one mesh a renderer contributes, reached exactly as AMUSE reaches
        /// it. Never through <c>MeshFilter.mesh</c>, which instantiates a copy as
        /// a side effect of being read.
        /// </summary>
        private static Mesh SharedMeshOf(Renderer renderer)
        {
            if (renderer is SkinnedMeshRenderer skinned)
            {
                return skinned.sharedMesh;
            }

            var filter = renderer.GetComponent<MeshFilter>();
            return filter == null ? null : filter.sharedMesh;
        }
    }

    /// <summary>
    /// Tier 1 asset identity. Only two AssetDatabase members are used and both
    /// are pure reads that import nothing, create nothing, and dirty nothing —
    /// the narrowed form of the harness's banned-API rule, enforced by
    /// <c>ResearchSourceApiBanTests</c>.
    /// <para>
    /// A runtime-constructed or embedded object has no asset path; Unity returns
    /// an empty string there and this normalizes it to null, so a missing
    /// identity reads as missing rather than as an empty asset.
    /// </para>
    /// </summary>
    internal static class CensusAssetIdentity
    {
        internal static string PathOf(UnityEngine.Object target)
        {
            if (target == null)
            {
                return null;
            }

            var path = AssetDatabase.GetAssetPath(target);
            return string.IsNullOrEmpty(path) ? null : path;
        }

        internal static string GuidOf(UnityEngine.Object target)
        {
            var path = PathOf(target);
            if (path == null)
            {
                return null;
            }

            var guid = AssetDatabase.AssetPathToGUID(path);
            return string.IsNullOrEmpty(guid) ? null : guid;
        }
    }
}
```

- [ ] **Step 5: Run the calibration tests**

Run `RendererRefusalCalibrationTests`.
Expected: 6 tests, all PASS.

If `UnsupportedTopologyKnowsSubmeshesButNotTriangles` fails on the refusal value, first check whether AMUSE refuses the quad mesh as `UnprovenMaterialSlotMapping`. The slot check runs before the topology check. The helper binds one material to a one-submesh quad mesh. Therefore, the mapping check passes and the topology check runs. If the observed refusal differs, **report it instead of changing the assertion to match**.

- [ ] **Step 6: Commit**

```bash
git add Packages/com.alrauna.amuse.research/Editor/Collection/RendererObservationBuilder.cs Packages/com.alrauna.amuse.research/Tests/Editor/Collection/CollectorTestScene.cs Packages/com.alrauna.amuse.research/Tests/Editor/Collection/RendererRefusalCalibrationTests.cs
git commit -m "feat(research): observe one renderer, recording unknown counts as null"
```

---

### Task 5: Avatar traversal and the public entry point

**Files:**
- Create: `Packages/com.alrauna.amuse.research/Editor/Collection/AvatarCensusCollector.cs`
- Create: `Packages/com.alrauna.amuse.research/Tests/Editor/Collection/AvatarCensusCollectorTests.cs`

**Interfaces:**
- Consumes: `RendererObservationBuilder.Build` (three-argument overload), `CensusShaderFamily`, `CensusAssetIdentity`.
- Produces: `public static Census.ObservedAvatar AvatarCensusCollector.Collect(GameObject root, string creatorName)`. **This is the assembly's only new public member. It has no seam overload.**

- [ ] **Step 1: Write the failing tests**

Create `Packages/com.alrauna.amuse.research/Tests/Editor/Collection/AvatarCensusCollectorTests.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Reflection;
using Alrauna.Amuse.Research.Census;
using Alrauna.Amuse.Research.Collection;
using NUnit.Framework;
using UnityEngine;

namespace Alrauna.Amuse.Research.Tests.Editor.Collection
{
    public sealed class AvatarCensusCollectorTests
    {
        private CollectorTestScene _scene;

        [SetUp]
        public void SetUp() => _scene = new CollectorTestScene();

        [TearDown]
        public void TearDown() => _scene.Destroy();

        [Test]
        public void CollectsEveryRendererUnderTheRootIncludingInactive()
        {
            // An inactive renderer still ships with the avatar and an animation
            // can re-enable it, so excluding it would understate the avatar.
            var root = _scene.NewRoot("Avatar");
            _scene.NewMeshRenderer(
                root, "Active", _scene.NewTriangleMesh(1),
                _scene.NewStandardMaterial());
            var hidden = _scene.NewMeshRenderer(
                root, "Inactive", _scene.NewTriangleMesh(1),
                _scene.NewStandardMaterial());
            hidden.SetActive(false);

            var observed = AvatarCensusCollector.Collect(root, null);

            Assert.That(observed.Renderers.Count, Is.EqualTo(2));
        }

        [Test]
        public void NeverCollectsRenderersOutsideTheGivenRoot()
        {
            // Scope containment: the collector observes what the caller named
            // and nothing else in the scene.
            var root = _scene.NewRoot("Avatar");
            _scene.NewMeshRenderer(
                root, "Mine", _scene.NewTriangleMesh(1),
                _scene.NewStandardMaterial());

            var other = _scene.NewRoot("SomeoneElse");
            _scene.NewMeshRenderer(
                other, "NotMine", _scene.NewTriangleMesh(1),
                _scene.NewStandardMaterial());

            var observed = AvatarCensusCollector.Collect(root, null);

            Assert.That(observed.Renderers.Count, Is.EqualTo(1));
            Assert.That(
                observed.Renderers[0].GameObjectName, Is.EqualTo("Mine"));
        }

        [Test]
        public void HierarchyPathIsRelativeToTheCollectionRoot()
        {
            // An absolute scene path would leak the structure above the avatar,
            // which is the operator's project rather than the observation.
            var root = _scene.NewRoot("Avatar");
            var body = _scene.NewChild(root, "Body");
            _scene.NewMeshRenderer(
                body, "Face", _scene.NewTriangleMesh(1),
                _scene.NewStandardMaterial());

            var observed = AvatarCensusCollector.Collect(root, null);

            Assert.That(
                observed.Renderers[0].HierarchyPath, Is.EqualTo("Body/Face"));
        }

        [Test]
        public void RendererOnTheRootItselfHasAnEmptyPath()
        {
            var root = _scene.NewRoot("Avatar");
            root.AddComponent<MeshFilter>().sharedMesh =
                _scene.NewTriangleMesh(1);
            root.AddComponent<MeshRenderer>().sharedMaterials =
                new[] { _scene.NewStandardMaterial() };

            var observed = AvatarCensusCollector.Collect(root, null);

            Assert.That(
                observed.Renderers[0].HierarchyPath, Is.EqualTo(string.Empty));
        }

        [Test]
        public void RecordsAvatarNameAndSuppliedCreator()
        {
            var root = _scene.NewRoot("Avatar");

            var observed = AvatarCensusCollector.Collect(root, "Someone");

            Assert.That(observed.AvatarName, Is.EqualTo("Avatar"));
            Assert.That(observed.CreatorName, Is.EqualTo("Someone"));
        }

        [Test]
        public void SceneObjectHasNoAssetIdentity()
        {
            // A scene instance is not an asset. Null is the honest record, and
            // the anonymizer reads no avatar identity field, so it costs nothing
            // downstream.
            var root = _scene.NewRoot("Avatar");

            var observed = AvatarCensusCollector.Collect(root, null);

            Assert.That(observed.AssetPath, Is.Null);
            Assert.That(observed.AssetGuid, Is.Null);
        }

        [Test]
        public void RuntimeMaterialHasNameButNoAssetIdentity()
        {
            var root = _scene.NewRoot("Avatar");
            _scene.NewMeshRenderer(
                root, "Mesh", _scene.NewTriangleMesh(1),
                _scene.NewStandardMaterial());

            var submesh = AvatarCensusCollector
                .Collect(root, null).Renderers[0].Submeshes[0];

            Assert.That(
                submesh.MaterialName, Is.EqualTo("CensusTestStandard"));
            Assert.That(submesh.MaterialAssetPath, Is.Null);
            Assert.That(submesh.MaterialAssetGuid, Is.Null);
            Assert.That(submesh.ShaderName, Is.EqualTo("Standard"));
        }

        [Test]
        public void NullRootIsRejected()
        {
            Assert.Throws<ArgumentNullException>(
                () => AvatarCensusCollector.Collect(null, null));
        }

        [Test]
        public void ObservedListsAreReadOnly()
        {
            var root = _scene.NewRoot("Avatar");
            _scene.NewMeshRenderer(
                root, "Mesh", _scene.NewTriangleMesh(1),
                _scene.NewStandardMaterial());

            var observed = AvatarCensusCollector.Collect(root, null);

            Assert.Throws<NotSupportedException>(
                () => ((IList<ObservedRenderer>)observed.Renderers)
                    .Add(observed.Renderers[0]));
        }

        [Test]
        public void ThePublicSurfaceIsExactlyOneTypeWithOneMethod()
        {
            // Review change 4, asserted rather than promised. A configuration
            // object, a provider parameter, or a second entry point would all
            // show up here.
            var exported = typeof(AvatarCensusCollector).Assembly
                .GetExportedTypes();

            CollectionAssert.AreEqual(
                new[] { typeof(AvatarCensusCollector) }, exported);

            var methods = new List<string>();
            foreach (var method in typeof(AvatarCensusCollector).GetMethods(
                         BindingFlags.Public | BindingFlags.Static |
                         BindingFlags.Instance | BindingFlags.DeclaredOnly))
            {
                methods.Add(method.Name);
            }

            CollectionAssert.AreEqual(new[] { "Collect" }, methods);
        }

        [Test]
        public void NoPublicEntryPointCanCollectWithoutACallerSuppliedRoot()
        {
            // The privacy requirement expressed as a signature: there is no
            // discovery, no scene scan, and no project search. Every public way
            // to produce an ObservedAvatar demands a GameObject the caller named.
            var offenders = new List<string>();
            foreach (var type in typeof(AvatarCensusCollector).Assembly
                         .GetExportedTypes())
            {
                foreach (var method in type.GetMethods(
                             BindingFlags.Public | BindingFlags.Static |
                             BindingFlags.Instance | BindingFlags.DeclaredOnly))
                {
                    if (method.ReturnType != typeof(ObservedAvatar) &&
                        method.ReturnType != typeof(CensusObservationSet))
                    {
                        continue;
                    }

                    var takesRoot = false;
                    foreach (var parameter in method.GetParameters())
                    {
                        if (parameter.ParameterType == typeof(GameObject))
                        {
                            takesRoot = true;
                        }
                    }

                    if (!takesRoot)
                    {
                        offenders.Add(type.FullName + "." + method.Name);
                    }
                }
            }

            CollectionAssert.IsEmpty(offenders);
        }
    }
}
```

- [ ] **Step 2: Run and verify failure**

Expected: compile error that names `AvatarCensusCollector`.

- [ ] **Step 3: Implement**

Create `Packages/com.alrauna.amuse.research/Editor/Collection/AvatarCensusCollector.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Census = Alrauna.Amuse.Research.Census;

namespace Alrauna.Amuse.Research.Collection
{
    /// <summary>
    /// Collect: the one census stage that touches Unity and AMUSE internals.
    /// It turns one caller-supplied avatar root into tier 1 records and stops
    /// there.
    /// <para>
    /// It does not anonymize, aggregate, export, serialize, persist, or
    /// transmit anything, and it opens no window and adds no menu item. Those
    /// are separate stages, and keeping them separate is what lets non-leakage
    /// be a unit test rather than a promise.
    /// </para>
    /// <para>
    /// It also does not discover. There is no zero-argument entry point, no
    /// scene scan, and no project search: the caller names the root, always.
    /// That is the privacy requirement expressed as a signature rather than as
    /// a rule someone has to remember.
    /// </para>
    /// <para>
    /// This method is the entire public surface of the assembly. There is
    /// deliberately no options object, no configuration, and no provider
    /// parameter; the semantics seam the calibration tests need lives one level
    /// down, internal to <c>RendererObservationBuilder</c>.
    /// </para>
    /// <para>
    /// Reads only. The analysis path uses <c>sharedMesh</c> and
    /// <c>sharedMaterials</c> exclusively, and so does everything added here.
    /// </para>
    /// </summary>
    public static class AvatarCensusCollector
    {
        /// <param name="root">
        /// The avatar root. Required; the collector never finds one itself.
        /// </param>
        /// <param name="creatorName">
        /// Tier 1 only, and caller-supplied because Unity has no such field —
        /// it is on neither a GameObject nor an avatar descriptor. Pass null
        /// when it is unknown.
        /// </param>
        public static Census.ObservedAvatar Collect(
            GameObject root, string creatorName)
        {
            if (ReferenceEquals(root, null))
            {
                throw new ArgumentNullException(nameof(root));
            }

            // Unity's overloaded equality reports a destroyed object as null.
            if (root == null)
            {
                throw new ArgumentException(
                    "The avatar root has been destroyed and cannot be observed.",
                    nameof(root));
            }

            // Inactive renderers are included: one still ships with the avatar
            // and an animation can re-enable it. Hierarchy order is
            // deterministic and is what fixes the renderer ordinals downstream.
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            var families = new CensusShaderFamily();
            var observed = new List<Census.ObservedRenderer>(renderers.Length);

            foreach (var renderer in renderers)
            {
                observed.Add(RendererObservationBuilder.Build(
                    renderer,
                    RelativePath(root.transform, renderer.transform),
                    families));
            }

            return new Census.ObservedAvatar(
                root.name,
                creatorName,
                CensusAssetIdentity.PathOf(root),
                CensusAssetIdentity.GuidOf(root),
                observed);
        }

        /// <summary>
        /// The path from the collection root, exclusive of the root itself, so
        /// the record cannot leak the scene structure above the avatar. Empty
        /// for a renderer on the root.
        /// <para>
        /// Sibling GameObjects may share a name, so this is not unique. That is
        /// accepted: it is a debugging hint that nothing downstream indexes by,
        /// and adding sibling indices would sharpen a fingerprint for no
        /// analytical gain.
        /// </para>
        /// </summary>
        private static string RelativePath(Transform root, Transform target)
        {
            if (target == root)
            {
                return string.Empty;
            }

            var segments = new List<string>();
            for (var node = target; node != null && node != root;
                 node = node.parent)
            {
                segments.Add(node.name);
            }

            var path = new StringBuilder();
            for (var index = segments.Count - 1; index >= 0; index--)
            {
                if (path.Length > 0)
                {
                    path.Append('/');
                }

                path.Append(segments[index]);
            }

            return path.ToString();
        }
    }
}
```

- [ ] **Step 4: Run**

Run `AvatarCensusCollectorTests`.
Expected: 11 tests, all PASS.

If `ThePublicSurfaceIsExactlyOneTypeWithOneMethod` fails, an assembly item is public when it must not be. Fix the accessibility, not the test.

- [ ] **Step 5: Commit**

```bash
git add Packages/com.alrauna.amuse.research/Editor/Collection/AvatarCensusCollector.cs Packages/com.alrauna.amuse.research/Tests/Editor/Collection/AvatarCensusCollectorTests.cs
git commit -m "feat(research): collect tier 1 records from an explicit avatar root"
```

---

### Task 6: Counting the success path through the seam

The public development project installs no vendor shader. Therefore, `ProvenOpaque` is unreachable through the production path here. This validates how the collector *counts* that outcome. A real project's ability to *reach* that outcome is a separate claim. It remains a Lab check.

Note this code location. The semantics construction is **in the test file**, not in production. Revision 1 put it in a production `CensusCalibration` class. Review change 2 correctly rejected it as a hidden extension point with only one test caller.

**Files:**
- Create: `Packages/com.alrauna.amuse.research/Tests/Editor/Collection/CollectorSeamCountingTests.cs`

**Interfaces:**
- Consumes: `RendererObservationBuilder.Build(Renderer, string, CensusShaderFamily, BaseMaterialSemanticsProvider)`.
- Produces: nothing consumed by later tasks. **This task creates or modifies no production file.**

- [ ] **Step 1: Write the failing tests**

Create `Packages/com.alrauna.amuse.research/Tests/Editor/Collection/CollectorSeamCountingTests.cs`:

```csharp
using Alrauna.Amuse.Editor.Host;
using Alrauna.Amuse.Research.Census;
using Alrauna.Amuse.Research.Collection;
using NUnit.Framework;
using UnityEngine;
using Semantics = Alrauna.Amuse.Editor.Semantics;

namespace Alrauna.Amuse.Research.Tests.Editor.Collection
{
    /// <summary>
    /// Counting claims for the two outcomes the public project cannot reach
    /// through the production path, because it installs no vendor shader. That
    /// AMUSE reaches them in a real project is a separate reachability claim and
    /// is checked in the Census Lab, not here. Conflating the two would let a
    /// census report near-total SemanticsUnknown — a true statement about the
    /// project and a false one about AMUSE — and call it a pass.
    /// <para>
    /// The substituted semantics are constructed here rather than in the
    /// collector package, so no production type exists whose only purpose is to
    /// be called by a test. The seam itself is AMUSE's own
    /// BaseMaterialSemanticsProvider, used exactly as AMUSE's own integration
    /// tests use it.
    /// </para>
    /// <para>
    /// The Semantics alias is not decoration: AMUSE declares its own
    /// TextureWrapMode, which collides by name with UnityEngine.TextureWrapMode.
    /// </para>
    /// </summary>
    public sealed class CollectorSeamCountingTests
    {
        private CollectorTestScene _scene;

        [SetUp]
        public void SetUp() => _scene = new CollectorTestScene();

        [TearDown]
        public void TearDown() => _scene.Destroy();

        /// <summary>
        /// Alpha only. The other three channels stay Unknown, because the census
        /// measures alpha and a seam that claimed base colour, emission, or
        /// normals would be asserting something it has no basis for.
        /// </summary>
        private static Semantics.MaterialSemantics ConstantOpaque()
        {
            return new Semantics.MaterialSemantics(
                Semantics.SemanticOutput<Semantics.ColorSemanticValue>
                    .Unknown(),
                Semantics.SemanticOutput<Semantics.ScalarSemanticValue>
                    .Complete(Semantics.ScalarSemanticValue.Constant(1f)),
                Semantics.SemanticOutput<Semantics.ColorSemanticValue>
                    .Unknown(),
                Semantics.SemanticOutput<Semantics.NormalSemanticValue>
                    .Unknown());
        }

        /// <summary>
        /// Alpha sampled from a texture identity the evidence provider was never
        /// given. The UV mapping is the identity — channel 0, unit scale, zero
        /// offset — and the sampling is the plainest supported pair, so the only
        /// thing the resolver can refuse on is the missing texture evidence.
        /// </summary>
        private static Semantics.MaterialSemantics AbsentTextureAlpha()
        {
            var sample = new Semantics.TextureSample(
                new Semantics.TextureSourceId(
                    "census-calibration-absent-texture"),
                new Semantics.UvMapping(0, Vector2.one, Vector2.zero),
                new Semantics.TextureSampling(
                    Semantics.TextureFilterMode.Bilinear,
                    Semantics.TextureWrapMode.Clamp));

            return new Semantics.MaterialSemantics(
                Semantics.SemanticOutput<Semantics.ColorSemanticValue>
                    .Unknown(),
                Semantics.SemanticOutput<Semantics.ScalarSemanticValue>
                    .Complete(Semantics.ScalarSemanticValue.Texture(
                        sample, Semantics.TextureChannel.Alpha)),
                Semantics.SemanticOutput<Semantics.ColorSemanticValue>
                    .Unknown(),
                Semantics.SemanticOutput<Semantics.NormalSemanticValue>
                    .Unknown());
        }

        private ObservedRenderer Observe(
            Renderer renderer, Semantics.MaterialSemantics semantics)
        {
            BaseMaterialSemanticsProvider provider = material => semantics;
            return RendererObservationBuilder.Build(
                renderer, "Path", new CensusShaderFamily(), provider);
        }

        [Test]
        public void ConstantOpaqueAlphaCountsEveryTriangleAsProvenOpaque()
        {
            var root = _scene.NewRoot("Avatar");
            var go = _scene.NewMeshRenderer(
                root, "Mesh", _scene.NewTriangleMesh(2),
                _scene.NewStandardMaterial(), _scene.NewStandardMaterial());

            var observed = Observe(
                go.GetComponent<MeshRenderer>(), ConstantOpaque());

            Assert.That(observed.Refusal, Is.EqualTo(RendererRefusal.None));
            Assert.That(observed.TriangleCount, Is.EqualTo(2));
            foreach (var submesh in observed.Submeshes)
            {
                Assert.That(
                    submesh.AlphaFailure,
                    Is.EqualTo(AlphaResolutionFailure.None));
                Assert.That(
                    submesh.ProvenOpaqueTriangleCount,
                    Is.EqualTo(submesh.TriangleCount));
                Assert.That(submesh.UnknownTriangleCount, Is.EqualTo(0));
                Assert.That(
                    submesh.Disposition,
                    Is.EqualTo(SeparationDisposition.WhollyOpaqueCandidate));
            }
        }

        [Test]
        public void MissingTextureEvidenceIsRecordedAsItsOwnFailure()
        {
            // "We understand this shader but cannot see the texture" implies a
            // completely different next step from "we do not understand this
            // shader", so the two must never merge.
            var root = _scene.NewRoot("Avatar");
            var go = _scene.NewMeshRenderer(
                root, "Mesh", _scene.NewTriangleMesh(1),
                _scene.NewStandardMaterial());

            var observed = Observe(
                go.GetComponent<MeshRenderer>(), AbsentTextureAlpha());
            var submesh = observed.Submeshes[0];

            Assert.That(
                submesh.AlphaFailure,
                Is.EqualTo(AlphaResolutionFailure.MissingTextureEvidence));
            Assert.That(
                submesh.UnknownTriangleCount,
                Is.EqualTo(submesh.TriangleCount));
            Assert.That(
                submesh.Disposition,
                Is.EqualTo(SeparationDisposition.Unchanged));
        }
    }
}
```

- [ ] **Step 2: Run**

Run `CollectorSeamCountingTests`.
Expected: 2 tests, PASS. No production change is necessary. Task 4 already built the overload.

If `ConstantOpaqueAlphaCountsEveryTriangleAsProvenOpaque` produces `Unknown` triangles, the semantics construction is wrong. Fix the construction. **Do not weaken the assertion.**

If `MissingTextureEvidenceIsRecordedAsItsOwnFailure` reports `UnsupportedUvMapping` or `UnsupportedSampling` instead, read `Packages/com.alrauna.amuse/Editor/Analysis/AlphaSemanticsResolver.cs`. Find the mapping and sampling pair that it accepts. Use that pair. The case must fail only because the texture is missing.

- [ ] **Step 3: Confirm no production file changed**

```bash
git status --short
```

Expected: exactly one new untracked test file and its `.meta`. **If a file under `Editor/Collection/` appears, the seam has leaked into production.** Undo it.

- [ ] **Step 4: Commit**

```bash
git add Packages/com.alrauna.amuse.research/Tests/Editor/Collection/CollectorSeamCountingTests.cs
git commit -m "test(research): validate proven-opaque counting through the AMUSE semantics seam"
```

---

### Task 7: Mutation safety

**Files:**
- Create: `Packages/com.alrauna.amuse.research/Tests/Editor/Collection/CollectorMutationSafetyTests.cs`
- Create: `Packages/com.alrauna.amuse.research/Tests/Editor/Collection/ResearchSourceApiBanTests.cs`

**Interfaces:**
- Consumes: `AvatarCensusCollector.Collect`, `CollectorTestScene`.
- Produces: nothing consumed by later tasks.

- [ ] **Step 1: Write the observable-proof test**

Create `Packages/com.alrauna.amuse.research/Tests/Editor/Collection/CollectorMutationSafetyTests.cs`:

```csharp
using Alrauna.Amuse.Research.Collection;
using NUnit.Framework;
using UnityEngine;

namespace Alrauna.Amuse.Research.Tests.Editor.Collection
{
    /// <summary>
    /// Layer 3 of mutation safety: the only layer checkable after the fact.
    /// <para>
    /// The accident being guarded against is specific and quiet.
    /// <c>Renderer.material</c>, <c>Renderer.materials</c>, and
    /// <c>MeshFilter.mesh</c> all compile, all read plausibly, and all
    /// instantiate a copy as a side effect — so a collector that used one would
    /// silently modify the avatar it was only supposed to observe.
    /// </para>
    /// </summary>
    public sealed class CollectorMutationSafetyTests
    {
        private CollectorTestScene _scene;

        [SetUp]
        public void SetUp() => _scene = new CollectorTestScene();

        [TearDown]
        public void TearDown() => _scene.Destroy();

        [Test]
        public void CollectingLeavesTheRendererMeshAndMaterialsIdentical()
        {
            var root = _scene.NewRoot("Avatar");
            var mesh = _scene.NewTriangleMesh(2);
            var first = _scene.NewStandardMaterial();
            var second = _scene.NewStandardMaterial();
            var go = _scene.NewMeshRenderer(
                root, "Mesh", mesh, first, second);
            var renderer = go.GetComponent<MeshRenderer>();
            var filter = go.GetComponent<MeshFilter>();

            var submeshCountBefore = mesh.subMeshCount;
            var vertexCountBefore = mesh.vertexCount;
            var hadBlockBefore = renderer.HasPropertyBlock();

            AvatarCensusCollector.Collect(root, null);

            // Reference identity, not equality: an instantiated copy compares
            // equal on content and is exactly the defect being hunted.
            Assert.That(
                ReferenceEquals(filter.sharedMesh, mesh), Is.True,
                "The shared mesh was replaced, which means a copy was "
                + "instantiated.");
            var after = renderer.sharedMaterials;
            Assert.That(after.Length, Is.EqualTo(2));
            Assert.That(ReferenceEquals(after[0], first), Is.True);
            Assert.That(ReferenceEquals(after[1], second), Is.True);
            Assert.That(mesh.subMeshCount, Is.EqualTo(submeshCountBefore));
            Assert.That(mesh.vertexCount, Is.EqualTo(vertexCountBefore));
            Assert.That(
                renderer.HasPropertyBlock(), Is.EqualTo(hadBlockBefore));
        }

        [Test]
        public void CollectingCreatesNoAdditionalMeshOrMaterialObjects()
        {
            var root = _scene.NewRoot("Avatar");
            _scene.NewMeshRenderer(
                root, "Mesh", _scene.NewTriangleMesh(1),
                _scene.NewStandardMaterial());

            var meshesBefore =
                Resources.FindObjectsOfTypeAll<Mesh>().Length;
            var materialsBefore =
                Resources.FindObjectsOfTypeAll<Material>().Length;

            AvatarCensusCollector.Collect(root, null);

            Assert.That(
                Resources.FindObjectsOfTypeAll<Mesh>().Length,
                Is.EqualTo(meshesBefore));
            Assert.That(
                Resources.FindObjectsOfTypeAll<Material>().Length,
                Is.EqualTo(materialsBefore));
        }
    }
}
```

- [ ] **Step 2: Run**

Expected: 2 tests, PASS. They should pass immediately because the collector is read-only. If either fails, Task 4 or 5 has a real defect. Fix the collector, never the assertion.

`CollectingCreatesNoAdditionalMeshOrMaterialObjects` can be flaky if the Editor creates objects concurrently. If it is, **report it**. Propose limiting the count to objects whose `name` starts with `CensusTest`. Do not delete the test.

- [ ] **Step 3: Write the source-scan test**

Create `Packages/com.alrauna.amuse.research/Tests/Editor/Collection/ResearchSourceApiBanTests.cs`:

```csharp
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace Alrauna.Amuse.Research.Tests.Editor.Collection
{
    /// <summary>
    /// Layer 2 of mutation safety, as a test rather than a promise: the research
    /// package's production source may not name a mutating or importing API.
    /// <para>
    /// Tests/ is deliberately out of scope. A calibration case has to call
    /// SetPropertyBlock to construct the very refusal it measures, and a test
    /// has to destroy the objects it created.
    /// </para>
    /// </summary>
    public sealed class ResearchSourceApiBanTests
    {
        /// <summary>
        /// Read-only AssetDatabase lookups are permitted; everything else on
        /// AssetDatabase is not. The blanket ban the harness design first wrote
        /// was broader than the mutation-safety concern that motivated it, and
        /// tier 1 loses its entire purpose without asset identity.
        /// </summary>
        private static readonly string[] AllowedAssetDatabaseMembers =
        {
            "AssetDatabase.GetAssetPath",
            "AssetDatabase.AssetPathToGUID",
        };

        private static readonly string[] BannedLiterals =
        {
            "AssetImporter",
            "TextureImporter",
            "ModelImporter",
            "EditorUtility.SetDirty",
            "Undo.",
            "PrefabUtility.",
            "EditorSceneManager.Save",
            "SetPropertyBlock",
            ".isReadable =",
            "Texture2D.Apply",
            "Object.Destroy",
        };

        /// <summary>
        /// The instantiating property reads, matched on a word boundary. As a
        /// bare substring ".material" also matches ".materialSlotIndex", and a
        /// scan that cries wolf gets weakened or deleted; the boundary makes it
        /// match the accident and not the field. It correspondingly does not
        /// match ".sharedMaterials" or ".sharedMesh", which is exactly the
        /// distinction this layer exists to draw.
        /// </summary>
        private static readonly string[] BannedPatterns =
        {
            @"\.material\b",
            @"\.materials\b",
            @"\.mesh\b",
        };

        [Test]
        public void ProductionSourceNamesNoMutatingApi()
        {
            var root = Path.GetFullPath(
                "Packages/com.alrauna.amuse.research/Editor");
            Assert.That(
                Directory.Exists(root), Is.True,
                "Research package Editor source not found at " + root);

            var files =
                Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories);

            // A mis-globbed path must fail rather than pass vacuously.
            Assert.That(
                files.Length, Is.GreaterThan(0),
                "No source files scanned; the scan proved nothing.");

            var offences = new List<string>();
            foreach (var file in files)
            {
                var text = File.ReadAllText(file);
                var name = Path.GetFileName(file);

                foreach (var banned in BannedLiterals)
                {
                    if (text.Contains(banned))
                    {
                        offences.Add(name + ": " + banned);
                    }
                }

                foreach (var pattern in BannedPatterns)
                {
                    if (Regex.IsMatch(text, pattern))
                    {
                        offences.Add(name + ": " + pattern);
                    }
                }

                foreach (Match match in Regex.Matches(
                             text, @"AssetDatabase\.\w+"))
                {
                    var allowed = false;
                    foreach (var permitted in AllowedAssetDatabaseMembers)
                    {
                        if (match.Value == permitted)
                        {
                            allowed = true;
                        }
                    }

                    if (!allowed)
                    {
                        offences.Add(name + ": " + match.Value);
                    }
                }
            }

            CollectionAssert.IsEmpty(offences);
        }

        [Test]
        public void ProductionSourceHoldsNoCalibrationOrSeamType()
        {
            // Review change 2, asserted rather than promised. The semantics seam
            // is one internal pass-through parameter on
            // RendererObservationBuilder.Build and nothing else; a type that
            // exists only to be called by a test does not belong in production.
            var root = Path.GetFullPath(
                "Packages/com.alrauna.amuse.research/Editor");

            var offences = new List<string>();
            foreach (var file in Directory.GetFiles(
                         root, "*.cs", SearchOption.AllDirectories))
            {
                var name = Path.GetFileNameWithoutExtension(file);
                if (name.IndexOf("Calibration", System.StringComparison
                        .OrdinalIgnoreCase) >= 0)
                {
                    offences.Add(name);
                }
            }

            CollectionAssert.IsEmpty(offences);

            // And the seam appears in exactly one production file.
            var carriers = new List<string>();
            foreach (var file in Directory.GetFiles(
                         root, "*.cs", SearchOption.AllDirectories))
            {
                if (File.ReadAllText(file)
                    .Contains("BaseMaterialSemanticsProvider"))
                {
                    carriers.Add(Path.GetFileName(file));
                }
            }

            CollectionAssert.AreEqual(
                new[] { "RendererObservationBuilder.cs" }, carriers);
        }
    }
}
```

- [ ] **Step 4: Run**

Expected: 2 tests, PASS.

If `ProductionSourceNamesNoMutatingApi` fails, read each reported offense. A hit in a **comment or doc string** is still a hit. Reword the comment instead of weakening the scan. A hit on real code is a genuine defect. **Do not add an exemption without reporting it.**

`RendererObservationBuilder.cs`'s doc comment mentions `MeshFilter.mesh`. That is a bare `MeshFilter.mesh` with no leading dot on `mesh`... It will match `\.mesh\b`. Reword that comment to say "the instantiating `mesh` property". Do not add an exemption.

- [ ] **Step 5: Commit**

```bash
git add Packages/com.alrauna.amuse.research/Tests/Editor/Collection/CollectorMutationSafetyTests.cs Packages/com.alrauna.amuse.research/Tests/Editor/Collection/ResearchSourceApiBanTests.cs
git commit -m "test(research): prove the collector mutates nothing it observes"
```

---

### Task 8: Full gate and branch completion

**Files:**
- Modify: `docs/superpowers/specs/2026-08-20-census-collector-design.md` (validation results section only)

- [ ] **Step 1: Confirm the Unity instance again**

Run the `Application.dataPath` check again. Its normalized value must equal `<repo-root>/Assets`. If it does not, or multiple instances are reachable, **stop**. Do not report results from an unconfirmed instance.

- [ ] **Step 2: Run the complete EditMode suite**

Run the whole suite, not a filter.

Expected: **802 passed, 0 failed, 0 skipped**. This is the 770 baseline plus 32 added:

| Test class | Count |
|---|---|
| `CensusVocabularyTests` | 9 |
| `RendererRefusalCalibrationTests` | 6 |
| `AvatarCensusCollectorTests` | 11 |
| `CollectorSeamCountingTests` | 2 |
| `CollectorMutationSafetyTests` | 2 |
| `ResearchSourceApiBanTests` | 2 |

Record the observed total. A total below 770 means an assembly failed to compile. The run is invalid regardless of its color. **If the total differs from 802, reconcile it against this table before reporting.** Do not only restate the expectation.

- [ ] **Step 3: Check the console**

Read the Unity console for errors and warnings that this branch introduced. Expected: none from `Alrauna.Amuse.Research.*`.

- [ ] **Step 4: Inspect the working tree**

```bash
git status --short && git diff --check && git diff --stat
```

Expected: `Packages/manifest.json` and `Packages/packages-lock.json` still appear as modified and **still uncommitted**. They are pre-existing unrelated changes. Leave them exactly as found. Nothing appears under `Library/`, `Temp/`, `Logs/`, or `UserSettings/`. No `.meta` file appears as deleted or modified.

- [ ] **Step 5: Verify the commit contents**

```bash
git diff main...HEAD --stat
git diff main...HEAD -- Packages/com.alrauna.amuse
```

Expected: only files listed in the File Structure table, plus their `.meta` files. The second command must show **only** `Editor/AssemblyInfo.cs`. Its diff must contain exactly the two added `InternalsVisibleTo` lines and their comments.

- [ ] **Step 6: Review against the design, line by line**

Review D §10's ten decisions and D §0's four review changes. Confirm that the code honors each item. Confirm specifically that:

- no D §9 stop condition was crossed;
- no test claims a vendor frontend was exercised;
- the `Alrauna.Amuse.Research.Census` assembly is unmodified and still declares `noEngineReferences: true` with zero references;
- no production file in the research package contains the word "calibration";
- no reflection appears in any production file;
- the private Census Lab was not used.

- [ ] **Step 7: Record the observed validation in the design document**

Append a `## 11. Validation performed` section to the design document. Include the observed test total, confirmed instance identity, and console state. Explicitly state what was **not** validated. Specifically, `ProvenOpaque` and `MissingTextureEvidence` **reachability** through the production single-argument path remains unproven here. It remains a Census Lab obligation.

- [ ] **Step 8: Commit**

```bash
git add docs/superpowers/specs/2026-08-20-census-collector-design.md
git commit -m "docs: record census collector validation results"
```

- [ ] **Step 9: Report**

State what changed and what validation ran, including its observed numbers. State what was skipped and why. State remaining risks and the recorded gaps of D §8. State that the private Unity MCP testbed was not used or modified.

Then recommend review before unrelated work.
