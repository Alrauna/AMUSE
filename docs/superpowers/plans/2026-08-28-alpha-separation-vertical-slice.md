# Poiyomi Alpha-Separation Vertical Slice Implementation Plan

> **Execution:** Inline execution in the working session is this repository's default. Subagent-driven execution requires explicit controller authorization and must not be assumed. Steps use checkbox (`- [ ]`) syntax for tracking.

> **Authorization boundary:** authorization to implement this plan is **not** authorization to stage, commit, push, merge, or open a pull request. Every task ends at an unstaged review checkpoint. The controller decides what is committed and when.

**Goal:** Ship the first nondestructive build-time alpha-separation feature for pinned attested Poiyomi materials, covering both `WhollyOpaqueCandidate` and `Split`, with admitted runtime material swaps and material-swap curve rewriting.

**Architecture:** Three `PlatformFinish` passes — the existing bindings capture, the existing extension-free semantic barrier (which additionally retains a prepared record and prepares opaque material clones), and a new reactivated `AnimatorServicesContext` pass that validates every candidate slot, finalizes against the surviving set, sweeps unreferenced transients, and performs one build-avatar mutation through `AmuseBuildOperation`.

**Tech Stack:** Unity 2022.3.22f1, NDMF 1.14.4, VRChat SDK 3.10.4, NUnit EditMode, pinned Poiyomi Toon 9.3.64 (vendor source absent from this repository).

**Branch:** `feat/alpha-separation-poiyomi-vertical-slice` from `main` at `552b5e9d20727cde3e11603bed62f25c81dacf35`.

**Design:** `docs/superpowers/specs/2026-08-28-alpha-separation-vertical-slice-design.md`. This plan does not restate it; where a rule is load-bearing it is repeated as an invariant or a falsifier, never as prose.

---

## Global Constraints

Every task's requirements implicitly include this section.

### Correctness invariants

- Source meshes, materials, textures, importer settings, animation clips, controllers, prefabs and scenes are **never written**. Only the NDMF build avatar and AMUSE-owned transient objects change.
- **No `IAssetSaver.SaveAsset` call, ever.** `BuildContext.Serialize()` persists assigned generated objects; an eagerly saved then abandoned asset is welded permanently into the shipped container.
- **No build-avatar write until every candidate slot on every renderer has been validated.** Constructing unassigned transient `Mesh`/`Material` objects is not a build-avatar write.
- **Correctness must never depend on pass adjacency.** Pass 3 revalidates every live binding, value and current material against the prepared record.
- Pass 3 **re-reads `renderer.sharedMaterials`** and builds the output array from that live snapshot. No current material is carried from the barrier. There is no `CurrentOpaque` field.
- A mesh clone is created **only** when `plan.RequiresAnySplit` **and** at least one `Split` slot survived preparation.
- Mesh finalization is layout-only against the surviving set: save `mesh.bounds` and every source submesh descriptor's `bounds` **before** raising `subMeshCount`, write indices with `calculateBounds: false`, restore per-submesh bounds via `SetSubMesh(..., MeshUpdateFlags.DontRecalculateBounds)`, then restore `mesh.bounds`. An appended or shrunken submesh inherits its **source** submesh's bounds.
- Curve rewriting matches by **real binding identity** (`path`, `type`, parsed slot) from live `VirtualClip.GetObjectCurveBindings()`, never by clip name, and preserves every keyframe `time` exactly.
- A marker clip carrying a target binding is a **pre-mutation slot-local refusal**. `VirtualClip.SetObjectCurve` begins `if (IsMarkerClip) return;` — a silent no-op — so it must never be relied on.
- `AlreadyOpaque` maps a source material **to itself**; no clone is created and it never enters `CreatedClones`.
- Generated opaque materials are deduplicated **by source material, avatar-wide**.
- The sweep destroys exactly the AMUSE-created transients no surviving slot references. **No reference counting.**
- Unrelated same-length `sharedMaterials` changes made between passes are preserved.
- Generated meshes and materials referenced **only** through a rewritten object curve must survive NDMF serialization.

### Refusal scope

| Scope | Conditions |
|---|---|
| Avatar | existing `AvatarAnimationRefusal` set — unchanged |
| Renderer | material-dependency closure, host structural refusals, unrecognized animated material bindings, additive layers, unnormalized direct blend trees — **unchanged**; plus the four new renderer-scoped members below |
| Slot | every other feature-owned refusal |

Renderer-scoped feature members apply to **all candidate slots of one renderer and nothing else** — never to that renderer's alpha analysis, never to another renderer.

### Reuse, not recreation

- The slot-local prerequisite is **merged** (`914d9db`). `ResolveRuntimeStates` already returns `SlotResolutionResult[] SlotResults`. Reuse it. Do **not** recreate it and do **not** add durable per-slot diagnostics.
- Renderer-scoped material-swap closure and the capture-schema/alpha-relevance split are **merged**. `UnityMaterialSemantics.CaptureRequestForFamily` already returns `Combine(PoiyomiMaterialSemantics.AlphaEvidenceRequest, PoiyomiOpaqueConversion.ConversionEvidenceRequest)` for Poiyomi, so **conversion evidence is already captured**. Do not touch the capture schema.
- Mesh cloning and layout finalization are **characterized** by `MeshCloneFinalizationCharacterizationTests`. Reuse the measured recipe; do not re-derive it.
- `PoiyomiOpaqueConversion` is used exactly as it stands.

### Scope boundaries — do not implement

`IOpaqueConversion`, a shader adapter interface, a conversion registry, a conversion factory, a generic conversion result hierarchy; lilToon conversion; any generalized mutation, animation or mesh IR; a cross-pass transaction framework; reference counting or an asset-lifetime registry; a cache, planner framework or durable slot-diagnostic system; non-readable mesh support; UV repacking, texture modulation, material simplification, profitability modelling; any Census change.

Do not modify: `CapturedAnimationEvidence`, the closed capturer or capture schema, `SlotResolutionResult`, `RendererAnalysisRefusal` (no member or ordering change), `MeshSeparationPlan`, `SubmeshSeparationPlan`, `MeshSeparationPlanner`, `UnityRendererMutationTarget`, `UnityRendererAlphaSnapshot`, `AmuseBuildOperation`, `MaterialSemantics`, `MaterialEvidenceRequest`.

No temporary production vocabulary. In particular, no `SplitNotYetSupported` or any other member that exists only to be deleted by a later task.

### Process

- RED before GREEN for every behavior change: write the failing test, run it, observe the **behavioral** failure (not a compile error), then implement.
- Unity operations use the **public AMUSE project only**, never Census Lab. Instance selection at the start of every task and again after any domain reload, port change or MCP reconnect:
  1. enumerate reachable instances read-only (`mcpforunity://instances`, or the `~/.unity-mcp/unity-mcp-status-*.json` status files);
  2. `set_active_instance` with the intended instance's `Name@hash` — **never a hard-coded port**, because the MCP server falls back to another port after a reload;
  3. confirm identity by executing `Application.dataPath`, normalized (resolve relative and symbolic segments, `/` separators, no trailing separator), and require an **exact** match to `<repo-root>/Assets`. A case-only match is not confirmed identity — stop rather than guess.
- Every new `.cs` gets its Unity-generated `.meta` alongside it as one logical unit; strip trailing whitespace from the three empty scalar lines to match the committed metas.
- After each task inspect `Packages/manifest.json` and `Packages/packages-lock.json`; if churn appears, inspect the complete diff and restore with `git checkout HEAD -- Packages/manifest.json Packages/packages-lock.json` only when it is exactly `com.unity.toolchain.macos-arm64-linux-x86_64`, `com.unity.sysroot`, `com.unity.sysroot.linux-x86_64` and no intentional change shares those files.

### Review checkpoints, not commits

This plan contains **no `git add` and no `git commit` step**. Each task ends with an unstaged review checkpoint:

```bash
git status --porcelain --untracked-files=all      # only intended files; every new .cs has its .meta
git diff                                          # complete diff, inspected in full
git diff --check                                  # expect exit 0
git status --porcelain -- Packages/manifest.json Packages/packages-lock.json
```

Report the focused test result, the changed-file list, and any churn inspected or restored. Then stop for controller review.

---

## File map

### Production

| File | Action | Responsibility |
|---|---|---|
| `Packages/com.alrauna.amuse/Editor/Build/AlphaSeparationRecords.cs` | Create (+ `.meta`) | `PreparedAlphaSeparation`, `PreparedRendererSeparation`, `PreparedSlotSeparation`, `AlphaSeparationSlotRefusal`, and the finalization records of Task 2. Plain records and one enum; no logic. |
| `Packages/com.alrauna.amuse/Editor/Build/AlphaSeparationPreparation.cs` | Create (+ `.meta`) | Barrier-side: conversion relevance resolution, per-slot conversion admission, the single shader-family branch, opaque mapping, material clone creation and naming, mesh clone creation. |
| `Packages/com.alrauna.amuse/Editor/Build/AlphaSeparationApply.cs` | Create (+ `.meta`) | Pass 3: `Execute`, `PrepareSurvivingSet` (validate → finalize → sweep) and `ApplyFinalization`. |
| `Packages/com.alrauna.amuse/Editor/Build/AmusePlatformFinishPlugin.cs` | Modify | `AmusePlatformFinishState.Separation`, `RecordSlotRefusal`/`SlotRefusalCount`, two applied counters; the third pass in `Configure`; the barrier's record retention and preparation call; two capture call sites (`:290`, `:295`). |
| `Packages/com.alrauna.amuse/Editor/Host/UnityAnimationEvidenceCapture.cs` | Modify | `out IReadOnlyList<Material> admittedLiveMaterials` on `Capture`, `CaptureGraph`, `CaptureObserved`, `CaptureGraphForTests` (`:134`), `CaptureObservedForTests` (`:117`). |
| `Packages/com.alrauna.amuse/Editor/Analysis/AdmittedMaterialStates.cs` | Modify | Extract the existing group-and-admit loop into `TryAdmitDerivedEvidence`; `ResolveSlot` calls it. No behavior change. |

### Tests

| File | Action | Falsifiers |
|---|---|---|
| `Packages/com.alrauna.amuse/Tests/Editor/Host/UnityAnimationEvidenceCaptureTests.cs` | Modify | none — **13 capture call sites** at lines 708, 771, 823, 894, 1052, 1086, 1127, 1161, 1182, 1265, 1321, 1453, 1523 |
| `Packages/com.alrauna.amuse/Tests/Editor/Build/AmusePlatformFinishPluginTests.cs` | Modify | none — one capture call site at line 3129; three private seams deleted after extraction. Its **10** fixture-overload call sites (81, 501, 1179, 2023, 2058, 2143, 2199, 2621, 2750, 2831) compile unchanged under the optional-parameter shape |
| `Packages/com.alrauna.amuse/Tests/Editor/Build/VerifiedPoiyomiTestSeams.cs` | Create (+ `.meta`) | none — extracted shared seams |
| `Packages/com.alrauna.amuse/Tests/Editor/Build/AlphaSeparationPreparationTests.cs` | Create (+ `.meta`) | 3, 9, 10, 11, 12 |
| `Packages/com.alrauna.amuse/Tests/Editor/Build/AlphaSeparationApplyTests.cs` | Create (+ `.meta`) | 1, 4, 6, 7, 8, 15, 17, 20. Also hosts the test-local seam plugin, its dedicated `INDMFPlatformProvider` and its `[assembly: ExportsPlugin(...)]` line — nested test types, not production architecture |
| `Packages/com.alrauna.amuse/Tests/Editor/Build/AlphaSeparationSplitTests.cs` | Create (+ `.meta`) | 2, 5, 14, 18 |
| `Packages/com.alrauna.amuse/Tests/Editor/Build/AlphaSeparationPersistenceTests.cs` | Create (+ `.meta`) | 13, 16, and the `SaveAsset` structural guard |

`Tests/Editor/Analysis/AdmittedMaterialStatesTests.cs` and `Tests/Editor/Semantics/Poiyomi/PoiyomiOpaqueConversionTests.cs` reference `UnityAnimationEvidenceCapture` only through `ResolveProofRelevant` and `TextureScaleOffsetSuffix`, whose signatures do not change. **They are not modified.** No other file in the package references the capture entry points.

### Why one new test-support file

`AmusePlatformFinishPluginTests` holds `SelectVerifiedFixtureRequest`, `CaptureVerifiedFixtureMaterials` and `VerifiedAlphaOnly` as `private static`. They encode production's Poiyomi family/request/semantics mapping, so the new test classes cannot use them and copying them would let the copies drift from production. `VerifiedPoiyomiTestSeams` is a mechanical `internal static` extraction of those three members **unchanged** — not a fixture framework, and no other helper is promoted. Everything else each test class needs (meshes, renderers, controllers) stays local, matching the repository's existing per-class-builder pattern.

`PoiyomiFixtureTestBase.CreateVerifiedMaterial()` is already `internal static` and cross-class reusable; use it directly.

---

## Falsifier map

| # | Obligation | Task | File |
|---|---|---|---|
| 1 | wholly opaque slot: material changes, mesh identity does not | 2 | ApplyTests |
| 2 | mixed `Split` slot: transparent indices stay, opaque indices append | 2 | SplitTests |
| 3 | no split anywhere: no mesh clone created | 2 | PreparationTests |
| 4 | planned split later invalidated: clone destroyed, renderer unchanged | 2 | ApplyTests |
| 5 | wholly opaque survives while a split sibling refuses | 2 | SplitTests |
| 6 | every swap value maps to the correct opaque material | 2 | ApplyTests |
| 7 | an unmapped/new value invalidates only its slot | 2 | ApplyTests |
| 8 | marker clip invalidates only affected slots, before mutation | 2 | ApplyTests |
| 9 | mixed Poiyomi/lilToon admitted slot unchanged | 2 | PreparationTests |
| 10 | Poiyomi slot optimizes beside a refused lilToon slot, same renderer | 2 | PreparationTests |
| 11 | conversion-only animation affects admission, not alpha proof | 2 | PreparationTests |
| 12 | `AlreadyOpaque` maps without a clone | 2 | PreparationTests |
| 13 | source materials, meshes and animation assets unchanged | 3 | PersistenceTests |
| 14 | mesh and submesh bounds survive split finalization | 2 | SplitTests |
| 15 | unused transient clones destroyed, referenced ones not | 2 | ApplyTests |
| 16 | assigned generated objects persist through NDMF serialization | 3 | PersistenceTests |
| 17 | no partial mutation before validation completes | 2 | ApplyTests |
| 18 | wrong appended index or wrong curve binding fails | 2 | SplitTests |
| 19 | **prerequisite level** — a post-closure per-slot alpha failure does not eliminate a valid sibling | merged | `AmusePlatformFinishPluginTests.RuntimeStateProductionEntry_PostClosureSlotRefusalKeepsTheValidSibling` (`914d9db`) — re-run, not re-created |
| 20 | live current material used, not barrier state | 2 | ApplyTests |

**Why 19 has no standalone feature-level test.** The merged prerequisite regression is the
direct F19 proof: it establishes that the valid sibling survives runtime-state analysis and
reaches the separation plan. Falsifier 1 is the feature-layer consumer proof — a wholly opaque
candidate that reaches the feature is applied — and falsifier 15 proves only surviving writes
are applied. A second end-to-end F19 fixture through the production-like
`AnimatorServicesContext` lifecycle is not expressible: **[MEASURED]** that lifecycle
materializes a renderer-wide `material.<Property>` float curve as a non-empty
`MaterialPropertyBlock` on the build renderer before the barrier, and
`UnityRendererAlphaAnalysis` refuses property blocks structurally — a conservative false
negative this milestone retains (design §15.2). Reaching the feature layer would require
bypassing a retained production gate, and the feature layer receives only candidate slots, so
it has no representation for an already-refused sibling. Revisit only if real-avatar evidence
justifies it.

---

## Test execution

Focused runs use `run_tests EditMode, group_names: [<test class full name>]` after instance selection and identity confirmation per **Process**.

Full runs, required **immediately after Task 2** and again after Task 3:

```
read_console clear
run_tests EditMode assembly_names:["Alrauna.Amuse.Tests.Editor"]
run_tests EditMode assembly_names:["Alrauna.Amuse.Research.Tests.Editor"]
read_console get types:[error,warning]
```

Expected Console entries and their classification — anything else is a defect:
`InvalidOperationException: synthetic preparation failure` / `synthetic post-mutation failure` (deliberate `AmuseBuildOperationTests` fixtures), `Starting processing for avatar: …` (informational NDMF), `Exception: mprotect returned EACCES` and `MCP-FOR-UNITY: Port …` (MCP tooling noise at domain reload).

---

## Atomicity

**The feature is one atomic task (Task 2).** Conversion preparation, material clone creation, mesh clone creation, the third pass, validation, both dispositions, both sweeps and apply land together. There is no executable checkpoint at which:

- an opaque clone or mesh clone exists without its cleanup path — the sweep is written in the same task as the code that creates clones;
- only one disposition is enabled — `WhollyOpaqueCandidate` and `Split` are implemented and tested in the same task;
- a build-avatar mutation can occur before validation — the first mutation site and the validation that gates it are introduced together.

**Task 2 opens with an internal inert target-API scaffold (Step 1).** It exists so the RED
tests fail *behaviorally* rather than failing to compile, and it is **not** a checkpoint: no
controller review, no commit-shaped milestone, no independently enabled partial feature, and
no temporary vocabulary that a later step deletes. Every symbol it introduces is final in name
and signature; Step 7 replaces bodies only. It registers no pass, creates no clone, records no
refusal, changes no counter and mutates nothing, and Step 2 proves that against the full
product suite before a single test is written against it.

**Task 1 is behavior-neutral and may land independently.** It threads the live pairing out of capture, extracts `TryAdmitDerivedEvidence`, extracts the test seams, and introduces an **inert scaffold**: the barrier retains a `PreparedAlphaSeparation` whose slot mappings are empty and whose `MeshClone` is always `null`. It creates no clone, records no refusal, changes no counter, and nothing consumes it. Its RED tests observe the retained record only. If Task 2 never lands, Task 1 leaves the build behaving exactly as `main` does.

**Task 3 adds no production behavior.** It adds tests and one structural source guard.

Two internal hazards inside Task 1, each neutralized within a single step:

- the capture signature change has 16 call sites across four files (two production, 14 test); the signature and every call site move in one edit, or the assembly does not compile;
- the `TryAdmitDerivedEvidence` extraction must be behavior-preserving, so it lands with no new caller and is validated against the full product assembly before Task 2 adds the conversion caller.

---

## Task 1: Behavior-neutral seams and the inert prepared scaffold

**Files:**
- Create: `Editor/Build/AlphaSeparationRecords.cs` (+ `.meta`)
- Create: `Tests/Editor/Build/VerifiedPoiyomiTestSeams.cs` (+ `.meta`)
- Create: `Tests/Editor/Build/AlphaSeparationPreparationTests.cs` (+ `.meta`)
- Modify: `Editor/Host/UnityAnimationEvidenceCapture.cs`, `Editor/Analysis/AdmittedMaterialStates.cs`, `Editor/Build/AmusePlatformFinishPlugin.cs`
- Modify: `Tests/Editor/Host/UnityAnimationEvidenceCaptureTests.cs`, `Tests/Editor/Build/AmusePlatformFinishPluginTests.cs`

**Interfaces — Produces:**

```csharp
internal sealed class PreparedAlphaSeparation
{
    internal IReadOnlyList<PreparedRendererSeparation> Renderers { get; }
    internal IReadOnlyDictionary<Material, Material> OpaqueBySource { get; }
    internal IReadOnlyList<Material> CreatedClones { get; }
}

internal sealed class PreparedRendererSeparation
{
    internal UnityRendererMutationTarget Target { get; }          // held whole
    internal string RendererPath { get; }
    internal MeshSeparationPlan Plan { get; }
    internal CapturedAnimationEvidence Evidence { get; }
    internal Mesh MeshClone { get; set; }                          // null in Task 1
    internal IReadOnlyList<PreparedSlotSeparation> CandidateSlots { get; }
}

internal sealed class PreparedSlotSeparation
{
    internal SubmeshSeparationPlan Plan { get; }                   // held whole
    internal IReadOnlyDictionary<Material, Material> OpaqueOfAdmitted { get; }
}

internal enum AlphaSeparationSlotRefusal { None }                  // members added in Task 2
```

Slot index is `Plan.SourceMaterialBindingIndex`; disposition is `Plan.Disposition`; ordinals are `Plan.OpaqueTriangleOrdinals` / `Plan.TransparentTriangleOrdinals`; renderer, expected mesh and expected slot count are `Target.Renderer`, `Target.ExpectedMesh`, `Target.ExpectedMaterialSlotCount`. **Nothing is copied out of an existing type.**

`OpaqueBySource` and `OpaqueOfAdmitted` use the default `Dictionary<Material, Material>` comparer. Invariant to state in a comment: keys are live, non-destroyed materials held for one synchronous build, so Unity's overloaded `Equals` cannot collapse two distinct keys, and the sweep runs after every lookup.

On `AmusePlatformFinishState`: `internal PreparedAlphaSeparation Separation { get; set; }` (null when nothing was prepared).

On the capture entry points, index-aligned with `CapturedAnimationEvidence.AdmittedMaterials`, `Array.Empty<Material>()` on every closure-failure path:

```csharp
internal static CapturedAnimationEvidence Capture(
    string rendererPath, IReadOnlyList<Material> currentSlots,
    CommittedControllerGraphResult graph, IPlatformAnimatorBindings bindings,
    out IReadOnlyList<Material> admittedLiveMaterials);
```

```csharp
// AdmittedMaterialStates — extraction only, no behavior change
internal static bool TryAdmitDerivedEvidence(
    CapturedAlphaMaterial material,
    IReadOnlyList<(CapturedFloatBinding Binding, AnimatedPropertyRef Reference)> bindings,
    MaterialEvidenceRequest relevance,
    out CapturedMaterialEvidence derived,
    out RendererAnalysisRefusal refusal);
```

`internal static class VerifiedPoiyomiTestSeams` exposes `SelectVerifiedFixtureRequest`, `CaptureVerifiedFixtureMaterials`, `VerifiedAlphaOnly` — moved verbatim; `AmusePlatformFinishPluginTests` delegates to them.

- [ ] **Step 1: Thread the live pairing out of capture, updating all 16 call sites in one edit**

Definition in `UnityAnimationEvidenceCapture.cs`; call sites at `AmusePlatformFinishPlugin.cs:290,295`, `AmusePlatformFinishPluginTests.cs:3129`, and `UnityAnimationEvidenceCaptureTests.cs:708,771,823,894,1052,1086,1127,1161,1182,1265,1321,1453,1523`.

- [ ] **Step 2: Run the full product assembly to prove the signature change is behavior-neutral**

Run: `run_tests EditMode assembly_names:["Alrauna.Amuse.Tests.Editor"]`
Expected: 1319 passed, 0 failed.

- [ ] **Step 3: Extract `TryAdmitDerivedEvidence` with no new caller**

Move the `GroupByProperty` + `Admit` loop body out of `ResolveSlot` into the new internal method; `ResolveSlot` calls it. No other change.

- [ ] **Step 4: Run the full product assembly to prove the extraction is behavior-neutral**

Run: `run_tests EditMode assembly_names:["Alrauna.Amuse.Tests.Editor"]`
Expected: 1319 passed, 0 failed.

- [ ] **Step 5: Extract the three verified seams**

Create `VerifiedPoiyomiTestSeams` with the three members moved unchanged; replace the originals in `AmusePlatformFinishPluginTests` with calls to it.

- [ ] **Step 6: Run `AmusePlatformFinishPluginTests` to prove the extraction is behavior-neutral**

Run: `run_tests EditMode group_names:["Alrauna.Amuse.Tests.Editor.Build.AmusePlatformFinishPluginTests"]`
Expected: 48 passed, 0 failed.

- [ ] **Step 7: Add the inert record types and the state field**

Create `AlphaSeparationRecords.cs` with the four types above and add
`AmusePlatformFinishState.Separation`, all with no writer. Nothing constructs a record yet, so
the build still behaves exactly as `main`. This exists so Step 9's failure is **behavioral** —
`Separation` is null — rather than a compile error against a type that does not exist.

- [ ] **Step 8: Write the failing scaffold tests**

In `AlphaSeparationPreparationTests`, over NDMF builds of one-renderer avatars whose slots hold verified Poiyomi materials with real triangles:

```csharp
// (a) a candidate renderer produces a retained record
Assert.That(amuse.Separation, Is.Not.Null);
Assert.That(amuse.Separation.Renderers, Has.Count.EqualTo(1));
var prepared = amuse.Separation.Renderers[0];
Assert.That(prepared.Target.Renderer, Is.SameAs(renderer));
Assert.That(prepared.Target.ExpectedMesh, Is.SameAs(mesh));
Assert.That(prepared.RendererPath, Is.Empty);
Assert.That(prepared.Plan.OpaqueTriangleCount, Is.EqualTo(1));
Assert.That(prepared.CandidateSlots, Has.Count.EqualTo(1));
Assert.That(prepared.CandidateSlots[0].Plan.Disposition,
    Is.EqualTo(SubmeshSeparationDisposition.WhollyOpaqueCandidate));

// (b) the scaffold is inert
Assert.That(amuse.Separation.CreatedClones, Is.Empty,
    "the scaffold must not create a clone before its sweep exists");
Assert.That(prepared.MeshClone, Is.Null);
Assert.That(renderer.sharedMaterials, Is.EqualTo(originalMaterials),
    "the scaffold must not mutate the build avatar");
Assert.That(renderer.sharedMesh, Is.SameAs(mesh));

// (c) a renderer with no opaque candidate produces no record
Assert.That(amuse.Separation, Is.Null);
```

- [ ] **Step 9: Run to verify they fail**

Run: `run_tests EditMode group_names:["Alrauna.Amuse.Tests.Editor.Build.AlphaSeparationPreparationTests"]`
Expected: FAIL **behaviorally, never with a compile error** — `amuse.Separation` is null in (a)
because Step 7 added no writer.

- [ ] **Step 10: Retain the inert record in the barrier**

In the renderer loop, when `plan.HasAnyOpaqueCandidates`, build a `PreparedRendererSeparation` from `extraction.MutationTarget`, `rendererPath`, `plan`, `evidence`, and one `PreparedSlotSeparation` per non-`Unchanged` submesh plan with an **empty** mapping and `MeshClone = null`. Append to `state.Separation`, creating it on first use. No clone, no refusal, no counter change.

- [ ] **Step 11: Run to verify they pass**

Run: `run_tests EditMode group_names:["Alrauna.Amuse.Tests.Editor.Build.AlphaSeparationPreparationTests"]`
Expected: PASS.

- [ ] **Step 12: Unstaged review checkpoint**

Run the four commands in **Review checkpoints, not commits**. Report the focused result, the six modified and three created paths, and any churn inspected or restored. Stop for controller review.

---

## Task 2: The complete alpha-separation feature — atomic

Conversion preparation, both clone kinds, the third pass, validation, both dispositions, both sweeps and apply. Nothing here may be split into a separately executable checkpoint.

**Files:**
- Create: `Editor/Build/AlphaSeparationPreparation.cs` (+ `.meta`), `Editor/Build/AlphaSeparationApply.cs` (+ `.meta`)
- Create: `Tests/Editor/Build/AlphaSeparationApplyTests.cs` (+ `.meta`), `Tests/Editor/Build/AlphaSeparationSplitTests.cs` (+ `.meta`)
- Modify: `Editor/Build/AlphaSeparationRecords.cs`, `Editor/Build/AmusePlatformFinishPlugin.cs`, `Tests/Editor/Build/AlphaSeparationPreparationTests.cs`

**Interfaces — Consumes:** everything Task 1 produces.

**Interfaces — Produces:**

```csharp
// The fourth public-fixture seam, on the existing internal AmusePlatformFinishPass.Execute
// fixture overload only. Production passes nothing and runs the real conversion path.
internal delegate bool VerifiedOpaqueConversion(
    Material live, CapturedMaterialEvidence derived,
    out Material opaque, out PoiyomiOpaqueConversionRefusal refusal);

// The finalized writes, produced by preparation and consumed by apply. A plain record of
// what will be written; not a mutation IR and not a transaction.
internal sealed class AlphaSeparationFinalization
{
    internal IReadOnlyList<AlphaSeparationRendererWrite> Writes { get; }
}

internal sealed class AlphaSeparationRendererWrite
{
    internal Renderer Renderer { get; }
    internal Mesh Mesh { get; }                                    // null unless a Split slot survived
    internal Material[] Materials { get; }
    internal IReadOnlyList<AlphaSeparationCurveEdit> CurveEdits { get; }
}

internal readonly struct AlphaSeparationCurveEdit
{
    internal VirtualClip Clip { get; }
    internal EditorCurveBinding Binding { get; }
    internal ObjectReferenceKeyframe[] Curve { get; }
}

internal static class AlphaSeparationApply
{
    internal const string PassName = "AMUSE alpha separation apply";

    internal static void Execute(BuildContext context);

    /// Validate every candidate slot, finalize against the surviving set, sweep every
    /// transient no surviving slot references. Reads and AMUSE-owned transient objects
    /// only: no renderer, no clip and no source asset is written. This is the method
    /// Execute passes to AmuseBuildOperation as its prepare delegate.
    internal static AmusePreparationDecision PrepareSurvivingSet(
        BuildContext context,
        AmusePlatformFinishState state,
        out AlphaSeparationFinalization finalization);

    /// The single build-avatar mutation boundary: curve edits, then sharedMesh,
    /// then sharedMaterials, per renderer in deterministic order.
    internal static void ApplyFinalization(
        AlphaSeparationFinalization finalization,
        AmusePlatformFinishState state);
}
```

`Execute` is `AmuseBuildOperation.Execute(state.Lifecycle, context.AssetSaver, _ => PrepareSurvivingSet(context, state, out finalization), () => ApplyFinalization(finalization, state))`. The asset saver is required by the delegate signature and deliberately never used. `PrepareSurvivingSet` returns `Ready()` when any surviving slot produces a write and `NoMutation()` otherwise; it never returns `Refused`, because every feature refusal is slot-local.

#### The lifecycle route falsifiers 4, 15 and 17 use

`PrepareSurvivingSet` reads the reactivated `AnimatorServicesContext`'s `AnimationIndex`, so it
**cannot** be called after `AvatarProcessor.ProcessAvatar` has returned or from outside a pass
that declares the extension. These three tests therefore drive it from inside a real build,
using the pattern `AnimatorServicesReactivationCharacterizationTests` already establishes: a
**test-local nested plugin confined to its own `INDMFPlatformProvider`**, declared in
`AlphaSeparationApplyTests.cs` with its own `[assembly: ExportsPlugin(...)]` line, exactly as
`BarrierUnderActiveExtensionProbePlugin` and `TestActiveExtensionPlatform` already are in
`AmusePlatformFinishPluginTests.cs`. Its `Configure` declares three `PlatformFinish` passes:

1. `WithRequiredExtension(typeof(AnimatorServicesContext), … AmuseAnimatorBindingsCapture.Execute)`;
2. extension-free — `AmusePlatformFinishPass.Execute(context, SupportedFacts(), …, conversion)`,
   the **real** barrier, which produces `Separation`;
3. `WithRequiredExtension(typeof(AnimatorServicesContext), … SeamPass)` — the seam pass, which
   calls `AlphaSeparationApply.PrepareSurvivingSet(context, state, out var finalization)`
   **exactly once**, records the decision, the finalization, the live `CreatedClones` and
   `MeshClone` references, and digests of every renderer's `sharedMesh` / `sharedMaterials` and
   every target clip's object curves **immediately after prepare returns**, into a static probe
   field — and then **returns without calling `ApplyFinalization`**.

Why each required property holds:

- **runs after the barrier produced `Separation`** — pass 3 is sequenced after pass 2, and NDMF
  deactivates and commits the animator graph between them, exactly as production does;
- **uses the real reactivated context** — pass 3 declares the extension, and the reactivation
  characterization already proves (RED-proven) that a third pass re-enters the same
  `VirtualControllerContext` with a fresh `AnimationIndex` over the committed graph;
- **invokes preparation exactly once, and no production apply intervenes** — the production
  `AmusePlatformFinishPlugin` is `[RunsOnPlatforms(WellKnownPlatforms.VRChatAvatar30)]`, so on
  this dedicated test platform every one of its passes is reported `Incompatible:` and never
  runs. The seam pass is the only caller of `PrepareSurvivingSet` in the build, and
  `ApplyFinalization` is never reached;
- **assertions see the state before any build-avatar write** — the probe's digests are taken
  inside the seam pass between prepare and its return, and nothing in the build writes to the
  renderer afterwards, so the post-`ProcessAvatar` assertions read exactly that state. Clone
  references survive to the test body because Editor Unity objects live until an explicit
  destroy or a domain reload, and no domain reload occurs inside a synchronous build;
  Unity's overloaded `==` reports a swept clone as null.

This adds **no production-only-for-tests hook, reset, registry, instrumentation counter or
alternate animation index**: `PrepareSurvivingSet` is the same production method production
`Execute` calls, and the plugin and platform are test-local nested classes, which are not
production architecture.

**Validation coverage is proven by outcome, not instrumentation:** a fixture whose candidate
slots each fail for a *different* reason must show `state.SlotRefusalCount(...)` equal to 1 for
**every** one of those reasons; a short-circuiting implementation records only the first.

#### Fixture-overload compatibility

**[SOURCE]** `AmusePlatformFinishPass.Execute` has three overloads: `(BuildContext)`,
`(BuildContext, HostLifecycleFacts)`, and the five-parameter fixture overload
`(BuildContext, HostLifecycleFacts, AlphaMaterialRequestSelector, ClosedAlphaMaterialCapturer,
CapturedAlphaMaterialSemanticsResolver)`. Re-swept: the fixture overload has **exactly 10 call
sites**, all in `AmusePlatformFinishPluginTests.cs` — lines 81, 501, 1179, 2023, 2058, 2143,
2199, 2621, 2750, 2831. The other nine `Execute(` occurrences in that file use the two-parameter
or one-parameter overloads and are unaffected.

**Chosen shape: add the fourth delegate as an optional final parameter**,
`VerifiedOpaqueConversion conversion = null`, where `null` means "run the real
`PoiyomiOpaqueConversion` path". This is the smallest shape that compiles: **all 10 existing
call sites compile unchanged**, no forwarding overload is added, and the other two overloads are
untouched. The three existing delegates keep their `ArgumentNullException` guards; the fourth
does not get one, because its null is meaningful.

Consequence to expect at Step 9, not a defect: with `conversion = null`, existing fixtures that
produce opaque candidates now reach the real conversion path, fail attestation on the stand-in
shader, and record slot refusals. Renderer accounting is unchanged and slot refusals live in a
counter no existing test reads, so no existing assertion changes. If any does, that is a real
finding to report rather than an expectation to adjust.

`AlphaSeparationSlotRefusal` gains, slot-scoped: `OpaqueConversionUnsupportedFamily`, `OpaqueConversionRefused`, `ConversionStateNotAdmitted`, `ConversionPropertyOverwrittenAtRuntime`, `MarkerClipCarriesSlotBinding`, `RuntimeMaterialValueNotMapped`, `SlotBindingAbsentFromEvidence`; renderer-scoped: `ConversionBindingUnrecognized`, `ConversionStateUnderAdditiveLayer`, `ConversionStateUnderUnnormalizedDirectBlendTree`, `RendererChangedSincePreparation`. `AmusePlatformFinishState` gains `SlotRefusalCount(AlphaSeparationSlotRefusal)`, `RecordSlotRefusal(AlphaSeparationSlotRefusal)` mirroring `RecordRendererRefusal`, and `AppliedRendererCount` / `AppliedOpaqueTriangleCount`.

**Preparation**, per candidate slot, per admitted index in `CapturedMaterialSlotEvidence.AdmittedMaterialIndices` — never over the whole `AdmittedMaterials` list:

1. `TryAdmitDerivedEvidence(captured, conversionBindings, PoiyomiOpaqueConversion.ConversionEvidenceRequest, …)` → `ConversionStateNotAdmitted`.
2. `PoiyomiOpaqueConversion.ReadEffectiveRenderState(live, out queue, out renderType)` — in the barrier, same pass as the evidence.
3. `PoiyomiOpaqueConversion.GatherConversionSourceEvidence(live.shader, derived)` then `PoiyomiMaterialSemantics.TryVerifyPoiyomiIdentity(...)` → `OpaqueConversionRefused`.
4. `PoiyomiOpaqueConversion.EvaluateVerifiedEligibility(derived, queue, renderType)`.
5. Overwrite rule: for every property in `PoiyomiOpaqueConversion.CanonicalOpaqueProperties` carrying an admitted conversion binding, the admitted value must equal the canonical value → `ConversionPropertyOverwrittenAtRuntime`.
6. `AlreadyOpaque` → map source to itself, no clone. `Convertible` → `PrepareCanonicalOpaqueClone(live)`, deduplicated through `OpaqueBySource`, named `"<source.name> (AMUSE Opaque <n>)"` with `n` its index in `CreatedClones`. `Refused` → drop the slot.

**Every admitted material of a slot must map before that slot is prepared at all.** Conversion relevance is resolved by re-running `UnityAnimationEvidenceCapture.ResolveProofRelevant` against `ConversionEvidenceRequest`, **only for renderers with opaque candidates**; `UnrecognizedMaterialBinding`, `evidence.HasAdditiveLayer` and `evidence.HasUnnormalizedDirectBlendTree` under a conversion-relevant binding each drop **all** candidate slots of that renderer.

The single family branch, the only place shader family is consulted:

```csharp
switch (captured.Family)
{
    case CapturedAlphaMaterialFamily.Poiyomi: /* steps 1-6 */ break;
    default: return AlphaSeparationSlotRefusal.OpaqueConversionUnsupportedFamily;
}
```

Mesh clone creation is gated on `plan.RequiresAnySplit` **and** at least one `Split` slot surviving preparation.

**Validation**, in `PrepareSurvivingSet`, reads only:

1. Per renderer: `Target.Renderer` alive; `sharedMesh` reference-equal to `Target.ExpectedMesh`; `sharedMaterials.Length == Target.ExpectedMaterialSlotCount`; `sharedMesh.subMeshCount == Target.ExpectedMaterialSlotCount`. Mismatch → `RendererChangedSincePreparation` for all its candidate slots. **Then snapshot `renderer.sharedMaterials` once** and use that snapshot for the rest of validation, finalization and apply.
2. Per candidate slot: discover live target bindings via `AnimationIndex.GetClipsForObjectPath(rendererPath)` **materialized to a list first** (it returns the index's own live set), then per clip `GetObjectCurveBindings()` filtered to `binding.path == rendererPath` and `LiveAnimationObservation.TryParseMaterialSlotBinding(binding.propertyName) == slotIndex`.
3. Any target clip with `IsMarkerClip` → `MarkerClipCarriesSlotBinding`.
4. Any target binding whose `(path, type.FullName, propertyName)` triple is absent from `Evidence`'s object bindings → `SlotBindingAbsentFromEvidence`.
5. Every keyframe value **and** `live[slotIndex]` must be a key in `OpaqueOfAdmitted` → `RuntimeMaterialValueNotMapped`. Fewer live values than the captured admitted set is **not** a refusal.

**Finalization** against the surviving set `S`. Appended indexing: for renderer `R` with original slot count `n` and surviving split slots `i₁ < i₂ < … < i_k` in ascending source-slot order, the appended slot for `i_m` is `n + (m − 1)`. One appended submesh and one appended material slot per surviving `Split` slot, never shared. Materials array of length `n + k`: `[i]` for a surviving `WhollyOpaqueCandidate` → `OpaqueOfAdmitted[live[i]]`; `[i]` for a surviving `Split` → `live[i]` unchanged; every other `[i]` → `live[i]`; `[n + m − 1]` → `OpaqueOfAdmitted[live[i_m]]`. Slot `i`'s curve is unchanged; a **new** binding identical except `propertyName = "m_Materials.Array.data[j]"`, inheriting the observed binding's real `Type`, carries identical times and mapped values. Mesh layout on the clone only:

```
save mesh.bounds and every source submesh descriptor's bounds
subMeshCount = n + k                                 // recalculates BOTH bounds levels
for each surviving split slot i_m:
    SetIndices(transparent triples of i_m, MeshTopology.Triangles, i_m,     calculateBounds: false)
    SetIndices(opaque      triples of i_m, MeshTopology.Triangles, n+m-1,   calculateBounds: false)
for each submesh: re-read descriptor, replace only bounds,
    SetSubMesh(index, descriptor, MeshUpdateFlags.DontRecalculateBounds)
mesh.bounds = saved
name = "<sourceMesh.name> (AMUSE Separated <rendererOrdinal>)"
```

Submeshes of `WhollyOpaqueCandidate` and `Unchanged` slots are not rewritten; only their bounds are restored. Index triples come from the plan's ordinals over `UnityRendererAlphaSnapshot.Submeshes[i].Indices`, which are absolute. Base-vertex normalization is expected and characterized.

**Sweep**, still inside `PrepareSurvivingSet`: destroy every material in `CreatedClones` and every `MeshClone` that no surviving slot references. One sweep, no reference counting.

- [ ] **Step 1: Add the inert target-API scaffold**

Introduce every symbol the Task 2 tests compile against, with inert bodies, so that Step 6's
failures are **behavioral** rather than compile errors. Nothing here is a controller
checkpoint, a commit-shaped milestone, or an independently enabled partial feature; it is an
internal step of this task and introduces no temporary vocabulary that a later step deletes.

Introduce, complete and final in name and signature:

- the **complete** `AlphaSeparationSlotRefusal` vocabulary — all seven slot-scoped and all
  four renderer-scoped members listed above, none added or removed later;
- `AlphaSeparationFinalization`, `AlphaSeparationRendererWrite`, `AlphaSeparationCurveEdit`;
- `AlphaSeparationPreparation`'s target entry surface;
- `AlphaSeparationApply.Execute`, `PrepareSurvivingSet`, `ApplyFinalization`;
- the `VerifiedOpaqueConversion` delegate and the internal `AmusePlatformFinishPass.Execute`
  fixture overload extended with `VerifiedOpaqueConversion conversion = null` as an **optional
  final parameter**, so all 10 existing call sites compile untouched and Step 2 runs without a
  compile error;
- `AmusePlatformFinishState.SlotRefusalCount`, `RecordSlotRefusal`, `AppliedRendererCount`,
  `AppliedOpaqueTriangleCount`.

The scaffold must be behavior-neutral, and each of these is a property Step 2 verifies:

- **the third pass is not registered** — `Configure` is untouched, so `Execute` is unreachable
  in a production build;
- preparation creates **no** material clone and **no** mesh clone, and leaves slot mappings
  empty exactly as Task 1's inert record does;
- **no refusal is recorded** and **no counter changes**;
- **no renderer and no clip is mutated**;
- `PrepareSurvivingSet` returns `AmusePreparationDecision.NoMutation()` with an
  `AlphaSeparationFinalization` whose `Writes` is empty;
- `ApplyFinalization` has nothing reachable to apply — with an empty finalization it performs
  no write, and no production path reaches it because the pass is unregistered;
- a production build behaves **exactly** as `main`.

- [ ] **Step 2: Run the full product suite to establish the scaffold's neutrality**

Run: `run_tests EditMode assembly_names:["Alrauna.Amuse.Tests.Editor"]`
Expected: the Task 1 count, 0 failed. A single failure here means the scaffold is not inert
and must be corrected before any test is written against it.

- [ ] **Step 3: Write the failing preparation tests**

Add to `AlphaSeparationPreparationTests`, each asserting on `amuse.Separation` and `amuse.SlotRefusalCount(...)` after an NDMF build:

- **Falsifier 12** — `AlreadyOpaque`: `CreatedClones` is empty and `slot.OpaqueOfAdmitted[source]` `Is.SameAs(source)`.
- **Falsifier 11** — two builds with `material._ZWrite` animated (a) away from and (b) to the material's serialized default: `OpaqueCandidateTriangleCount` identical in both and equal to the unanimated build; (a) refuses `ConversionStateNotAdmitted`, (b) prepares the slot.
- **Falsifier 9** — mixed Poiyomi + lilToon slot: the lilToon material must pass alpha-family selection and the closed capture; assert `evidence.IsClosed`, then `OpaqueConversionUnsupportedFamily` and no candidate slot.
- **Falsifier 10** — Poiyomi and conversion-unsupported lilToon slots on the **same renderer**, closure asserted successful: the Poiyomi slot is prepared with a non-empty mapping while the lilToon slot is refused. Separate renderers cannot falsify renderer-scoped escalation.
- **Falsifier 3** — a candidate renderer with no `Split` disposition: every `MeshClone` is `null`.

- [ ] **Step 4: Write the failing apply tests**

In `AlphaSeparationApplyTests`:

- **Falsifier 1** — wholly opaque slot: `sharedMaterials[0]` is the opaque result; `sharedMesh` `Is.SameAs` the source mesh with `subMeshCount` unchanged.
- **Falsifier 6** — a three-keyframe curve over two distinct admitted materials yields the two corresponding opaque materials at identical times, compared with `"R"` formatting.
- **Falsifier 7** — an unmapped live value invalidates only its slot; the sibling still applies.
- **Falsifier 8** — a special motion carrying a target binding: `MarkerClipCarriesSlotBinding`, its curve unchanged, the source clip asset unchanged, an unaffected slot still applies.
- **Falsifier 20** — a probe pass declared between the barrier and pass 3 replaces one slot's `sharedMaterials[i]` without changing the array length: (a) unmapped material → that slot unchanged, sibling applies; (b) a different already-admitted, already-mapped material → the slot applies using **that** material's opaque result, asserted by reference.
Falsifiers 4, 15 and 17 run through the seam plugin described under **The lifecycle route**: the
seam pass calls `PrepareSurvivingSet` once under an active `AnimatorServicesContext` and never
calls `ApplyFinalization`, and the assertions read the probe it recorded.

- **Falsifier 4** — a planned split later invalidated: the clone reference recorded by the probe is non-null after preparation and reports Unity-destroyed once `PrepareSurvivingSet` has returned; the renderer's mesh, materials and curve digests are unchanged.
- **Falsifier 15** — the sweep destroys exactly the unreferenced clones and leaves referenced ones alive, observed through the probe's retained `CreatedClones` and `MeshClone` references.
- **Falsifier 17** — several candidate slots each failing for a **different** reason: the probe's digests show every renderer's `sharedMesh`, `sharedMaterials` and every clip curve unchanged after prepare returned, **and** `SlotRefusalCount` is 1 for every one of those distinct reasons, which a short-circuiting validator cannot produce.

- [ ] **Step 5: Write the failing split tests**

In `AlphaSeparationSplitTests`, over a fixture mesh with a **nonzero source `baseVertex`** on the split submesh and authored mesh and per-submesh bounds **unrelated to its geometry**:

- **Falsifier 2** — submesh `i` retains exactly the transparent triples and submesh `j` exactly the opaque triples, compared as index sets from `GetIndices`; `sharedMaterials[i]` unchanged, `[j]` the opaque result.
- **Falsifier 5** — a wholly-opaque slot survives while a `Split` sibling refuses.
- **Falsifier 14** — mesh bounds and every submesh's bounds, including untouched submeshes and the appended one.
- **Falsifier 18** — two surviving split slots on one renderer, plus two clips sharing a display name, a decoy binding at another renderer path and a decoy binding at another slot.

- [ ] **Step 6: Run all three classes to verify they fail**

Run each of `AlphaSeparationPreparationTests`, `AlphaSeparationApplyTests`, `AlphaSeparationSplitTests`.
Expected: FAIL **behaviorally, never with a compile error** — every symbol they reference
exists from Step 1. The failures are that preparation produces no mappings, creates no clone
and records no refusal; that the third pass is not registered, so nothing is applied; and that
`PrepareSurvivingSet` returns `NoMutation` with an empty finalization. A compile error here
means Step 1 was incomplete: fix the scaffold, do not weaken the test.

- [ ] **Step 7: Implement the complete feature**

**Replace the inert bodies from Step 1 — add no new type and no new enum member.** Implement
conversion admission and the family branch, the opaque mappings, both clone kinds, validation,
finalization for both dispositions, both sweeps and apply, and register the third pass:

```csharp
sequence.WithRequiredExtension(
    typeof(AnimatorServicesContext),
    inner => inner.Run(AlphaSeparationApply.PassName, AlphaSeparationApply.Execute));
```

- [ ] **Step 8: Run all three classes to verify they pass**

Expected: PASS in all three.

- [ ] **Step 9: Full product and research verification**

```
read_console clear
run_tests EditMode assembly_names:["Alrauna.Amuse.Tests.Editor"]
run_tests EditMode assembly_names:["Alrauna.Amuse.Research.Tests.Editor"]
read_console get types:[error,warning]
```
Expected: 0 failed in both. Confirm the merged `RuntimeStateProductionEntry_PostClosureSlotRefusalKeepsTheValidSibling` still passes. Classify Console against the expected list.

- [ ] **Step 10: Unstaged review checkpoint**

Run the four commands in **Review checkpoints, not commits**. Report both full-suite counts, the Console classification, the changed-file list and any churn inspected or restored. Stop for controller review.

---

## Task 3: Persistence, the `SaveAsset` guard, source preservation and completion audit

Adds no production behavior.

**Files:**
- Create: `Tests/Editor/Build/AlphaSeparationPersistenceTests.cs` (+ `.meta`)

- [ ] **Step 1: Write the dynamic persistence and preservation tests**

- **Falsifier 16** — with a **real** temporary directory (`new OverrideTemporaryDirectoryScope(<temp folder>)`; `null` disables saving — this is the only test that points NDMF's persistence scope at a real temporary directory; the split fixtures additionally create the test-owned `Assets/AmuseTests_AlphaSplit` folder for an importer-backed texture, which current texture evidence requires, and delete it unconditionally), the assigned mesh and materials — **including a material referenced only by a rewritten object curve** — are persistent after the build. What this proves is exactly that: assigned generated objects become persistent and curve-only references are traversed by `Serialize()`. **It does not prove production never called `SaveAsset`**, and no assertion in it may be described as proving that. Teardown deletes the folder unconditionally.
- **Falsifier 13** — source materials' full property sets, the source mesh's characterized state, and the source `AnimationClip` / `AnimatorController` are unchanged after a successful build; the committed clip is asserted **not** reference-equal to the source clip.

- [ ] **Step 2: Write the deterministic `SaveAsset` structural guard**

A source audit over the alpha-separation production files — `AlphaSeparationRecords.cs`, `AlphaSeparationPreparation.cs`, `AlphaSeparationApply.cs`, and `AmusePlatformFinishPlugin.cs` — asserting none contains `SaveAsset`. This is the only assertion that establishes the no-eager-save invariant; it is deterministic, needs no build, and cannot be defeated by a save whose asset happens to be reachable. Precedent: `Alrauna.Amuse.Research.Tests.Editor.Collection.ResearchSourceApiBanTests.ProductionSourceNamesNoMutatingApi` performs the same kind of audit.

- [ ] **Step 3: Run to verify**

Run: `run_tests EditMode group_names:["Alrauna.Amuse.Tests.Editor.Build.AlphaSeparationPersistenceTests"]`
The dynamic tests may pass immediately, since `Serialize()` already traverses renderer and clip references. That is a valid outcome **only** after confirming each would fail against a source-writing implementation; state that confirmation in the checkpoint report. The structural guard must be shown to fail if a `SaveAsset` call is temporarily introduced, then restored.

- [ ] **Step 4: Final full runs**

```
read_console clear
run_tests EditMode assembly_names:["Alrauna.Amuse.Tests.Editor"]
run_tests EditMode assembly_names:["Alrauna.Amuse.Research.Tests.Editor"]
read_console get types:[error,warning]
```
Expected: 0 failed in both.

- [ ] **Step 5: Completion audit**

- Console classified against the expected list; no new or unexplained entry.
- No temporary asset survives: `Assets/` has no leftover test folder.
- `git status --porcelain --untracked-files=all` shows only intended files; every new `.cs` has its `.meta`; no GUID churn on existing `.meta` files.
- `git diff --check` clean; no trailing whitespace, tabs or CRLF in new files.
- Source assets unchanged (falsifier 13 is the automated proof; the `SaveAsset` guard is the structural one).
- Scope audit: no forbidden type introduced; `RendererAnalysisRefusal`, `SlotResolutionResult`, `MeshSeparationPlan`, `MeshSeparationPlanner`, `UnityRendererMutationTarget`, `AmuseBuildOperation`, `MaterialSemantics` and the capture schema unmodified; no temporary production vocabulary anywhere.
- Census Lab not used, not inspected, not modified.

- [ ] **Step 6: Unstaged review checkpoint**

Run the four commands in **Review checkpoints, not commits**. Report both full-suite counts, the audit results and the complete changed-file list. Stop for controller review.

---

## Recorded future refactor pressure — not solved here

1. **Private per-slot refusal reporting.** `ResolvedRuntimeStates.SlotResults` retains each refused slot's reason and this feature adds `AlphaSeparationSlotRefusal` counters, but neither names *which* slot refused. The first consumer needing durable per-slot attribution — a user-facing report or an NDMF error entry — is when a separate diagnostic representation should be designed.
2. **lilToon as the second conversion family.** The single `switch` in `AlphaSeparationPreparation` is the whole extension point. Adding lilToon needs a lilToon conversion evidence request in `UnityMaterialSemantics.CaptureRequestForFamily`, a lilToon conversion implementation, and one new `case` — and nothing in geometry planning, mesh finalization, appended-slot indexing, animation discovery or rewriting. A registry earns its first honest argument at the third family, not the second. Follow-up: `investigate/liltoon-opaque-conversion`.
3. **The prepared-state boundary under a second optimization feature.** `PreparedAlphaSeparation` and `AlphaSeparationFinalization` are feature-specific by design. A second build-time optimization that also mutates renderers in `PlatformFinish` would be the first real evidence about whether a shared prepared/apply boundary is warranted. Do not generalize before that second consumer exists.
