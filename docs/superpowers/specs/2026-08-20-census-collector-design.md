# Census collector — design

**Branch:** `feat/census-collector`
**Date:** 2026-08-20
**Status:** approved 2026-08-20 with four architectural changes, applied at revision 2 (§0)
**Predecessors:** `docs/superpowers/specs/2026-08-20-avatar-census-harness-preparation-design.md`
(the harness architecture, cited below as **HP §n**),
`docs/superpowers/specs/2026-08-20-census-record-schema-design.md` (tiers 1–3, commit
`eedadf2`)

## 0. Revision 2 — changes required at architectural review

Revision 1 was approved subject to four changes. All are applied below.

| # | Required change | Where |
|---|---|---|
| 1 | Collector architecture unchanged | Nothing to do |
| 2 | Move `CensusCalibration` out of the production assembly; no permanent calibration seam API in the collector package; no hidden runtime extension points | §3.1, §5.1, §7.3 |
| 3 | Replace reflection-based frontend discovery with an explicit declared list; tests must still fail when AMUSE gains a frontend; no dependence on namespace or method reflection conventions | §5.4, §7.2 |
| 4 | Keep the public collector surface minimal; no provider or configuration abstractions unless implementation forces them | §5.1 |

Changes 2 and 4 turned out to pull in the same direction and are resolved together. Moving
calibration into the test assembly requires that assembly to see AMUSE internals; once it
can, the parity tests can name AMUSE's enums directly, which **deletes** the public name-list
projection revision 1 needed. The result is a second friend grant traded for the removal of
a production class *and* a public API. Net surface goes down, not up — see §3.1.

## 1. Scope

This branch implements **Collect**, the first and only stage of the census pipeline that
touches Unity or AMUSE internals (HP §4.3):

```
Unity objects  →  Collect  →  ObservedAvatar  →  CensusAnonymizer  →  CensusAggregator
```

In scope: the collector, the single friend-assembly grant, the enum mappings and their
drift detection, the calibration cases, the arithmetic invariants, and the validation
layers of HP §7 and §8.

Out of scope, and deliberately so: anonymization, aggregation, export, serialization,
persistence, network, telemetry, any Editor window or menu item, and any avatar discovery.
The collector is a function. Whatever eventually invokes it in the Lab is a later branch.

No AMUSE analysis behaviour, result object, shader adapter, or evidence provider changes.
No public API is promoted. The only production edit anywhere outside the research package
is one `InternalsVisibleTo` line (§3).

## 2. Baseline observed before design

Recorded so review can check the claims rather than trust them.

| Fact | Observed |
|---|---|
| Repository gate | EditMode suite, **770 passed / 0 failed / 0 skipped**, 29.9 s |
| Unity instance | `Application.dataPath` = `/Users/user/Documents/AMUSE/Assets`, exactly `<repo-root>/Assets`, single connected instance |
| Assemblies loaded | `Alrauna.Amuse.Editor`, `Alrauna.Amuse.Tests.Editor`, `Alrauna.Amuse.Research.Census`, `Alrauna.Amuse.Research.Tests.Editor` |
| Working tree | `Packages/manifest.json` and `Packages/packages-lock.json` modified — the previously characterized macOS `toolchain`/`sysroot` churn. Pre-existing and unrelated; **left untouched**, not reverted, not staged |
| Private Census Lab | **Not used, not accessed, not modified.** Nothing in this design requires it |

## 3. Assembly and visibility

```
Packages/com.alrauna.amuse.research/
  Editor/
    Census/      Alrauna.Amuse.Research.Census.asmdef      (exists, unchanged)
    Collection/  Alrauna.Amuse.Research.Editor.asmdef      (new)
  Tests/Editor/  Alrauna.Amuse.Research.Tests.Editor.asmdef (reference added)
```

`Alrauna.Amuse.Research.Editor` references `Alrauna.Amuse.Research.Census` and
`Alrauna.Amuse.Editor`; Editor-only; `autoReferenced: false`. No new package (HP §3.2.1).

`Alrauna.Amuse.Research.Census` is not touched. It keeps `noEngineReferences: true` and
zero references, and remains Unity-free and AMUSE-free. The collector depends on the
census assembly; the dependency never points back.

### 3.1 The friend grants

`Packages/com.alrauna.amuse/Editor/AssemblyInfo.cs` gains two lines:

```csharp
[assembly: InternalsVisibleTo("Alrauna.Amuse.Research.Editor")]
[assembly: InternalsVisibleTo("Alrauna.Amuse.Research.Tests.Editor")]
```

The first is the grant HP §4.2 specified and HP §10.1 deferred to this branch. It is
required: `RendererAlphaAnalysis`, `SubmeshAlphaAnalysis`, `RendererAnalysisRefusal`,
`MeshSeparationPlan`, `SubmeshSeparationDisposition`, `TriangleAlphaOutcome`,
`AlphaResolutionFailure`, and both shader frontends are all `internal`. Without it the only
alternative is reflection, rejected in HP §4.1.

The second is new at revision 2 and exists **because** of review change 2.

### 3.1.1 Why the second grant is the smaller surface

Revision 1 kept the AMUSE grant at one by putting a `CensusCalibration` class in the
production research assembly: it constructed AMUSE `MaterialSemantics` values behind
signatures naming only census types, so the test assembly never had to see AMUSE. Review
change 2 rejects that, correctly — it is a permanent, hidden extension point living in
production code whose only caller is a test.

The alternative is to let the *test* assembly see AMUSE, which is what a test assembly is
for. That single change removes two things from production:

| | Revision 1 | Revision 2 |
|---|---|---|
| AMUSE friend grants | 1 | 2 |
| Production calibration class | `CensusCalibration` (7 members) | **none** |
| Public API in the research package | `AvatarCensusCollector` + `CensusVocabulary` (4 public name lists) | **`AvatarCensusCollector.Collect` only** |
| Seam parameter on the public surface | none | none |

The public name lists existed only so tests that could not name an AMUSE enum could still
compare against one. With the second grant they compare directly, so `CensusVocabulary`
becomes wholly `internal` and the projection is deleted.

Both grantees are first-party assemblies in this repository, versioned and compiled together,
and `Alrauna.Amuse.Tests.Editor` already holds a grant of exactly this shape for exactly this
reason. A test assembly is also strictly narrower in blast radius than a production one: it
ships in no build and no release artifact, and the research package ships in neither anyway.

The cost is stated plainly: AMUSE internals now have three consumers rather than two, which
constrains refactoring a little further. That is the direction HP §4.2 already called
desirable — a rename that breaks the census should break loudly in CI.

## 4. Design questions answered

### 4.1 A — avatar traversal

**Decision: explicit root `GameObject`, `GetComponentsInChildren<Renderer>(includeInactive: true)`.**

| Option | Verdict |
|---|---|
| Explicit `GameObject` root | **Chosen.** Zero coupling. Works for a scene instance and for a prefab asset. Testable from synthetic GameObjects with no SDK |
| Animator root traversal | Rejected. Requires an `Animator`, silently excludes avatars without one, and answers a rigging question rather than a scoping one |
| `VRCAvatarDescriptor` | Rejected, and **not implementable here**: `Packages/vpm-manifest.json` declares only `nadena.dev.ndmf`. The public development project has no VRChat SDK, so this path could never be exercised in public CI |

`includeInactive: true`, because an inactive renderer still ships with the avatar and can
be re-enabled by an animation. Excluding it would understate avatar complexity in exactly
the direction HP §5.2 warns about.

Hierarchy order from `GetComponentsInChildren` is deterministic, and it is what fixes the
renderer ordinals downstream. The root's own renderer, if any, is included.

**No discovery.** There is no zero-argument entry point, no scene scan, no
`AssetDatabase.FindAssets`, no type search. The one public method requires a root the
caller names, which is the privacy requirement expressed as a signature rather than a rule.

### 4.2 B — renderer identity

Tier 1 is the debug layer, so it records what makes an anomaly traceable and nothing else.
Every field here is dropped by `CensusAnonymizer`; none reaches tier 2.

| Field | Source | Note |
|---|---|---|
| `HierarchyPath` | `/`-joined names from the **collection root**, exclusive; `""` for the root itself | Root-relative on purpose — an absolute scene path would leak the structure *above* the avatar, which is the operator's project, not the observation |
| `GameObjectName` | `renderer.gameObject.name` | |
| `RendererTypeName` | `renderer.GetType().Name` | Raw string, therefore tier 1 only |
| `Kind` | Closed mapping (§4.3) | The only renderer-type fact that survives anonymization |

Sibling GameObjects may share a name, so `HierarchyPath` is not guaranteed unique. That is
accepted: it is a debugging hint, not a key, and nothing downstream indexes by it. Adding
sibling indices would harden a fingerprint for no analytical gain.

Nothing else is recorded — no bounds, no bone counts, no blendshapes, no layer, no tag,
no component inventory (HP §6.4).

### 4.3 C — failure representation

**Decision: reuse AMUSE's vocabularies verbatim; invent nothing.**

Three total, exhaustive mappings, each with **no default arm**. An AMUSE value with no
census counterpart throws `ArgumentOutOfRangeException` naming the unmapped value, matching
the precedent `CensusAnonymizer.ShaderFamily` already sets.

| AMUSE (internal) | Census | Cardinality |
|---|---|---|
| `RendererAnalysisRefusal` | `RendererRefusal` | 7 |
| `AlphaResolutionFailure` | `AlphaResolutionFailure` | 6 |
| `SubmeshSeparationDisposition` | `SeparationDisposition` | 3 |

`RendererKind` is the one mapping that is not one-to-one: `SkinnedMeshRenderer` and
`MeshRenderer` are tested explicitly and everything else is `Other`. `Other` is
unreachable in practice — AMUSE refuses every other type as `UnsupportedRendererType` —
but it is the conservative default and is recorded rather than thrown on.

The four failure kinds the brief names map onto existing vocabulary without extension:

| Failure | Representation |
|---|---|
| Unsupported renderer | `RendererRefusal.UnsupportedRendererType`, counts `null` |
| Missing mesh | `RendererRefusal.MissingMesh`, counts `null` |
| Missing material | `ObservedSubmesh.HasMaterial == false`; the alpha failure is `SemanticsUnknown`, which is what AMUSE actually produces |
| Analysis exception | **Not represented. It propagates and aborts the run** |

The last is a deliberate refusal to invent. `UnityRendererAlphaAnalysis.Analyze` throws only
for a null or destroyed renderer, neither of which `GetComponentsInChildren` can return. An
exception therefore means a collector defect, and a census that catches its own defects and
records them as data produces a confident wrong number. HP §7.2 already requires an invariant
violation to abort; this is the same rule applied to exceptions. It also keeps the census
enums pinned to AMUSE's, which the drift test (§7.2) depends on.

### 4.4 D — collector tests

Synthetic GameObjects, meshes, and materials constructed in code inside each test and
destroyed in teardown. No avatar, no prefab, no saved asset, no `AssetDatabase` write, no
fixture file, no Lab. Detail in §7.

## 5. What the collector does

### 5.1 Surface

The entire public API of the research package's collector:

```csharp
namespace Alrauna.Amuse.Research.Collection
{
    public static class AvatarCensusCollector
    {
        public static ObservedAvatar Collect(GameObject root, string creatorName);
    }
}
```

One type, one method, two arguments. No options object, no builder, no configuration, no
provider parameter, no registry, and no second overload. `creatorName` is a parameter
because it is the one tier 1 field Unity cannot supply (§6.3); the caller passes `null` when
it is unknown. Everything else in the assembly — `CensusVocabulary`, `CensusShaderFamily`,
`RendererObservationBuilder`, `CensusAssetIdentity` — is `internal`.

**The semantics seam is not on this surface.** It sits one level down, as a second overload
of the internal `RendererObservationBuilder.Build`, mirroring the two-overload shape
`UnityRendererAlphaAnalysis.Analyze` already uses for its own integration tests. That is a
pass-through of an existing AMUSE seam at the narrowest scope that can carry it, not a new
extension point, and no public caller can reach or even name it (§7.3).

Per renderer: call `UnityRendererAlphaAnalysis.Analyze(renderer)`, then read the returned
immutable result. The collector adds no shader, material, texture, or geometry logic of its
own — it measures the production pipeline rather than re-deriving it. The only Unity state it
reads that `Analyze` does not is the mesh, for the refused-renderer counts (§5.3), and the
material, for identity (§6).

### 5.2 Counting an analyzed renderer

For `Refusal == None`, all counts come from the returned plan; nothing is recomputed from
geometry:

- per-triangle outcomes: `Plan.Source.Submeshes[i].Outcomes`, tallied into the three
  `ProvenOpaque` / `MustRemainTransparent` / `Unknown` counts;
- disposition: `Plan.Submeshes[i].Disposition`;
- submesh and material-slot indices, `HasMaterial`, and the alpha failure:
  `analysis.Submeshes[i]`.

`analysis.Submeshes`, `Plan.Submeshes`, and `Plan.Source.Submeshes` are index-parallel by
construction; the collector asserts their lengths agree rather than assuming it.

### 5.3 Counting a refused renderer — the null-versus-zero rule

HP §5.2 names this the most likely miscount in the system. A refused analysis carries no
plan, so counts come from the mesh, and are `null` — never `0` — when the mesh is not
reachable.

| Refusal | Mesh reachable | `SubmeshCount` | `TriangleCount` |
|---|---|---|---|
| `UnsupportedRendererType` | no | `null` | `null` |
| `MissingMesh` | no | `null` | `null` |
| `MaterialPropertyOverridesPresent` | yes | `subMeshCount` | summed |
| `UnprovenMaterialSlotMapping` | yes | `subMeshCount` | summed |
| `MalformedMeshData` | yes | `subMeshCount` | summed |
| `UnsupportedTopology` | yes | `subMeshCount` | **`null`** |

The collector reaches the mesh the same way AMUSE does — `SkinnedMeshRenderer.sharedMesh`,
otherwise `MeshFilter.sharedMesh` — and never through `MeshFilter.mesh`, which instantiates
a copy as a side effect of being read.

Triangles are summed as `mesh.GetIndexCount(i) / 3` over triangle-topology submeshes, using
`GetIndexCount` rather than `GetIndices` so no index buffer is allocated. `UnsupportedTopology`
is the deliberate asymmetry: a quad submesh has no triangle count, and any number written
there would be an invention. `null` is the honest answer and the aggregate skips it with its
own denominator.

`ObservedRenderer` enforces the analyzed-renderer half of this rule in its constructor
already; the refused half is the collector's responsibility and is the first calibration
case (§7.1).

### 5.4 Shader family attestation

`UnityMaterialSemantics.AnalyzeBaseMaterial` runs an exclusive trial — Poiyomi, then
lilToon — and returns only the resulting `MaterialSemantics`, discarding which frontend
answered. `RendererAlphaAnalysis` therefore carries no attestation, and HP §6.1 calls
shader-family coverage the single highest-value number in the census.

**Decision: the collector declares the families it measures explicitly and repeats the trial
through AMUSE's own frontends, memoized per distinct `Material`. No reflection runs in
production.**

```csharp
// The explicit declaration. Two named families, in AMUSE's own trial order.
PoiyomiMaterialSemantics.AnalyzeBaseMaterial(m).IsSupportedMaterial  → Poiyomi
LilToonMaterialSemantics.AnalyzeBaseMaterial(m).IsSupportedMaterial  → LilToon
otherwise                                                            → None
```

Revision 1 discovered the frontends by reflecting a namespace for a method named
`AnalyzeBaseMaterial`. Review change 3 rejects that, and the objection is sound: it made the
census's own vocabulary depend on AMUSE's naming and folder conventions, so a rename that
changed nothing semantically could silently change what the census measured. Production now
names the two types directly, and the compiler checks them.

Considered and rejected: changing AMUSE to report the attesting frontend. That is a
production result-object change made solely so the census can measure something, forbidden
by HP §1, §6.2, and §10.4 — the same rule that forbids adding `Unknown` attribution.

The accepted costs, stated plainly:

- **Duplicated work.** Attestation hashes the whole normalized shader source, and this runs
  it a second time per distinct material. Memoization keeps it to once per material rather
  than once per submesh. For a one-shot research run over tens of materials this is
  seconds, and it buys the census's most important number without touching production.
- **Duplicated trial order.** The collector re-states "Poiyomi first, then lilToon". If
  AMUSE gained a third frontend, the collector would silently report it as `None` and the
  census would attribute a real family to `UnknownFamily-x`. Detection is §7.2's job.

## 6. Identity capture and the banned-API amendment

### 6.1 The conflict

Tier 1 `ObservedSubmesh` carries `MaterialName`, `MaterialAssetPath`, and
`MaterialAssetGuid`, and `CensusAnonymizer.MaterialKey` builds the avatar-scoped material
ordinal from `GUID + path + name`. HP §8 Layer 2 bans `AssetDatabase.` outright in the
research package.

Both cannot hold. Without asset identity the key degrades to the material *name*, and two
distinct materials both named `Body` collapse into one ordinal — understating "distinct
materials per avatar" in tier 2 and every aggregate derived from it. That is a miscount, not
merely lost debuggability.

### 6.2 Amendment

**The ban is narrowed to writes.** The research package may call exactly two read-only
members:

- `AssetDatabase.GetAssetPath`
- `AssetDatabase.AssetPathToGUID`

Every other `AssetDatabase` member stays banned, and the §8 Layer 2 CI check enforces the
narrowed rule rather than the blanket one (§7.4).

Justification: HP §8 Layer 2 is titled *mutation safety*, and both members are pure reads
that import nothing, create nothing, and dirty nothing. The blanket ban was broader than the
concern that motivated it. Narrowing it preserves the guarantee the layer exists to give
while letting tier 1 do the job tier 1 was designed for — HP §5.1: "A census anomaly that
cannot be traced back to a concrete material is not debuggable."

Rejected alternatives: `GetInstanceID` in the GUID position (collision-free but
session-local, so it defeats tier 1's entire purpose and puts a non-GUID in a field named
Guid); operator-supplied identity maps (returns to manual discipline the schema branch
deliberately moved into tested code); and shipping name-only keys with the collision
documented (a knowingly wrong number in the first real census).

This amends a decision from a prior reviewed branch and is called out here for that reason.
It is not on the HP §10.4 stop list, but it is the kind of change that should not be made
silently.

### 6.3 What is captured, and what cannot be

| Field | Source | Gap |
|---|---|---|
| `MaterialName` | `material.name` | — |
| `MaterialAssetPath` | `AssetDatabase.GetAssetPath(material)` | Empty for a runtime-constructed or embedded material; **normalized to `null`** (verified: a `new Material(...)` returns `""`) |
| `MaterialAssetGuid` | `AssetPathToGUID` of that path | `null` when the path is |
| `ShaderName` | `material.shader.name` | `null` when the material or shader is null |
| `AvatarName` | `root.name` | — |
| `AssetPath` / `AssetGuid` | `GetAssetPath(root)` | **Populated only when the root is itself an asset.** A scene instance yields `null` |
| `CreatorName` | caller-supplied | **Not derivable from Unity.** Not on a `GameObject`, and not on a `VRCAvatarDescriptor` either |

The last two gaps are recorded rather than solved. `PrefabUtility.GetCorrespondingObjectFromSource`
would resolve a scene instance back to its prefab, but `PrefabUtility.` stays banned and this
branch does not widen the amendment beyond what §6.1 forces. The cost is low and is worth
stating precisely: `CensusAnonymizer` reads **no** `ObservedAvatar` identity field — verified
in source — so avatar identity is operator debugging aid only, and a `null` there cannot
affect any tier 2 or tier 3 number.

## 7. Validation

### 7.1 Calibration cases — counting

The five CI cases of HP §7.1, constructed from primitive meshes and materials, never from an
avatar, never stored as assets.

| Case | Construction | Asserts |
|---|---|---|
| `UnsupportedRendererType` | `LineRenderer` on an empty GameObject | Refusal mapped; `SubmeshCount` and `TriangleCount` are **`null`, not `0`**; no submesh records |
| `UnsupportedTopology` | Mesh with one `MeshTopology.Quads` submesh | Refusal mapped; `SubmeshCount == 1`; `TriangleCount` is `null` (§5.3) |
| `MaterialPropertyOverridesPresent` | `MeshRenderer` + `SetPropertyBlock` with one value | Refusal mapped; counts read from the mesh, not `null` |
| `UnprovenMaterialSlotMapping` | 2-submesh mesh bound to 1 shared material | Refusal mapped; counts read from the mesh |
| `SemanticsUnknown` | `MeshRenderer`, 2 triangles, Unity Standard material | `Refusal.None`; one submesh; `AlphaFailure == SemanticsUnknown`; all triangles `Unknown`; `Disposition == Unchanged`; `ShaderFamilyAttestation == None` |

`SetPropertyBlock` appears in the third case's *test*, never in collector source, and the
§7.4 scan covers `Editor/` only for that reason.

The two vendor-shader cases of HP §7.1 (`ProvenOpaque`, `MissingTextureEvidence`) are
**reachability** claims and remain Lab-only. Their **counting** claims run in CI through the
seam (§7.3).

### 7.2 Drift detection

Four tests, all in the research test assembly, all naming AMUSE's types directly rather than
discovering them. Their job is to make a future AMUSE change fail loudly here rather than
silently miscount in a private run — the compile-time coupling HP §4.2 argued for, extended
to values the compiler alone cannot check.

- **Three enum-parity tests.** `RendererAnalysisRefusal`, `AlphaResolutionFailure`, and
  `SubmeshSeparationDisposition` each have exactly the member names of their census mirror.
  A new AMUSE value fails here **and** throws from the mapping's missing default arm. With
  the second grant (§3.1) these compare `Enum.GetNames` on both sides directly; revision 1's
  public string projection is gone.
- **One frontend-set test.** The set of namespaces directly beneath
  `Alrauna.Amuse.Editor.Semantics` equals the literal
  `{ "Alrauna.Amuse.Editor.Semantics.LilToon", "Alrauna.Amuse.Editor.Semantics.Poiyomi" }`
  — verified as the current set. AMUSE gives each vendor frontend its own folder and
  namespace, so a third adapter fails this test in the commit that adds it, and a human then
  decides whether the census measures it.

On the last one, the distinction review change 3 draws is worth keeping sharp. **Production
depends on no convention** — it names two types, and the compiler checks them. The test is a
*snapshot pinned to a literal*, the same device `CensusCategorySnapshotTests` already uses for
the enums; it enumerates in order to compare against a hardcoded expectation, not in order to
decide behaviour. If the pin ever disagrees with reality, a person resolves it.

Its one blind spot, recorded rather than hidden: a frontend added *inside* an existing vendor
namespace would not create a new namespace and would not fail this test. Nothing short of
parsing `UnityMaterialSemantics`'s method body catches that, and a source-parsing test would
be more fragile than the thing it guards.

### 7.3 The seam

HP §7.1 requires CI to validate `ProvenOpaque` counting through the
`BaseMaterialSemanticsProvider` seam AMUSE already exposes for its own integration tests.
The public project installs no vendor shader, so that outcome is otherwise unreachable here.

**The seam lives entirely in the test assembly.** With the §3.1 grant, the tests construct
the `MaterialSemantics` values themselves and pass them to the internal overload:

```csharp
// internal to Alrauna.Amuse.Research.Editor, mirroring
// UnityRendererAlphaAnalysis.Analyze's own two-overload shape
internal static ObservedRenderer Build(
    Renderer renderer, string hierarchyPath, CensusShaderFamily families);
internal static ObservedRenderer Build(
    Renderer renderer, string hierarchyPath, CensusShaderFamily families,
    BaseMaterialSemanticsProvider semanticsProvider);
```

`AvatarCensusCollector` calls the three-argument form and carries no seam parameter anywhere
on its own signature. Nothing named "calibration" exists in production; revision 1's
`CensusCalibration` class is deleted, and the semantics construction it held now sits beside
the tests that use it.

What remains in production is one internal overload carrying one extra parameter that is a
straight pass-through to AMUSE's own seam. That is the narrowest place the seam can live and
still be reachable, it mirrors a shape AMUSE already ships, and no public caller can reach or
name it. AMUSE's production path is untouched: no measurement hook, no test-only branch, no
diagnostic expansion.

**Counting is not reachability.** These tests establish that the collector *counts*
`ProvenOpaque` and `MissingTextureEvidence` correctly. That AMUSE *reaches* those outcomes
through the production single-argument path in a real project is a separate claim, it needs a
vendor shader, and it stays a Census Lab obligation before every census run.

### 7.4 Mutation safety

**Layer 1 — inherited.** `UnityRendererAlphaAnalysis` reads `sharedMesh` and
`sharedMaterials` only. Collect calls `Analyze` and reads the immutable result. The one
addition, the refused-renderer mesh read (§5.3), uses `sharedMesh` and `GetIndexCount` and
allocates no buffer.

**Layer 2 — source scan, as a CI test.** An EditMode test reads every `.cs` file under
`Packages/com.alrauna.amuse.research/Editor/` and fails on any of: `AssetDatabase.` other
than `GetAssetPath` / `AssetPathToGUID`; `AssetImporter`; `TextureImporter`; `ModelImporter`;
`EditorUtility.SetDirty`; `Undo.`; `PrefabUtility.`; `EditorSceneManager.Save`;
`SetPropertyBlock`; `.isReadable =`; `Texture2D.Apply`; `Object.Destroy`; and the
instantiating property reads `.material`, `.materials`, and `.mesh`.

The last three are matched as **word-boundary regexes**, not substrings. `.material` as a
substring also matches `.materialSlotIndex`, and a scan that cries wolf gets weakened or
deleted; `\.material\b` matches the accident and not the field. Correspondingly
`\.materials\b` does not match `.sharedMaterials`, and `\.mesh\b` does not match
`.sharedMesh` — which is exactly the distinction the layer exists to draw.

Path resolved as `Path.GetFullPath("Packages/com.alrauna.amuse.research/Editor")` —
**verified to resolve the embedded package**, repo-relative, no absolute path, no drive
letter. `Tests/` is excluded by design (§7.1). The scanned file set is asserted non-empty,
so a mis-globbed path fails rather than passing vacuously.

`renderer.material` and `renderer.materials` are the specific accidents the brief names, and
they are the reason this is a scan rather than a promise: both compile, both read plausibly,
and both silently instantiate a copy.

**Layer 3 — observable proof, per test.** After every calibration Collect, assert that
`sharedMesh` and each entry of `sharedMaterials` are reference-identical to the objects the
test created, that `HasPropertyBlock()` is unchanged, and that the mesh's `subMeshCount` and
`vertexCount` are unchanged. This catches an instantiating read directly, in the same test
that exercises the path.

The full HP §8 Layer 3 asset manifest — hash every asset in scope before and after, report
`assetManifestUnchanged` — belongs to the census **run**, not the collector. It is named here
as this branch's deferred obligation so it is not lost.

### 7.5 Arithmetic invariants

Enforced in the collector, so a violation aborts rather than records.

1. `provenOpaque + mustRemainTransparent + unknown == submesh.TriangleCount`
   (already enforced by the `ObservedSubmesh` constructor; the collector must not defeat it).
2. `sum(submesh.TriangleCount) == renderer.TriangleCount` when `Refusal == None`
   (already enforced by the `ObservedRenderer` constructor).
3. **The collector's own tally equals AMUSE's independent count:**
   `sum(ProvenOpaqueTriangleCount) == Plan.OpaqueTriangleCount` and
   `sum(MustRemainTransparent + Unknown) == Plan.TransparentTriangleCount`.

The third is the load-bearing one and is new to this branch: it checks a number the collector
derived from `Outcomes` against a number `MeshSeparationPlanner` computed on its own, so a
misattribution bug cannot agree with itself. Note the asymmetry — AMUSE's "transparent" count
is everything not `ProvenOpaque`, so `Unknown` is included on that side.

None of the three gets a dedicated negative test, and that is a limitation worth stating
rather than papering over: forcing a violation would mean feeding the collector a fabricated
AMUSE plan, which needs a fake in place of the production analysis and would test the fake.
They are enforced in code and exercised by every calibration case instead.

### 7.6 Privacy and immutability

- **No discovery:** a test asserts the public surface of `Alrauna.Amuse.Research.Collection`
  has no method that can produce an `ObservedAvatar` without a caller-supplied `GameObject`.
- **Scope containment:** a sibling renderer outside the given root, in the same scene, never
  appears in the result.
- **Immutability:** the returned lists are the read-only wrappers the schema already
  guarantees; a test asserts a cast to `IList<>` either fails or throws on write.
- **Non-leakage is already covered** by the schema branch's tier 2 and tier 3 tests and is
  not re-litigated here. What this branch adds is that the strings those tests protect are
  now populated from real Unity objects.

### 7.7 Gate

The full EditMode suite, expected at **770 + the tests added here**, zero failures, with the
test count observed and reported rather than inferred. Then a working-tree inspection
confirming only intended files changed and that the pre-existing `Packages/*.json` churn is
untouched.

## 8. Gaps recorded, not solved

Per the brief: document, do not expand scope.

1. **Unknown attribution (HP §6.2).** A triangle can be `Unknown` on a submesh whose failure
   is `None` and AMUSE records no reason. The collector cannot explain it and must not make
   AMUSE explain it. Measuring the size of the blind spot — `Unknown` count on
   `Failure == None` submeshes — is derivable from the tier 1 records this branch produces,
   and belongs to the aggregate, not the collector.
2. **Attesting frontend not reported by AMUSE** (§5.4). Costs a duplicated trial, and the
   drift pin cannot see a frontend added inside an existing vendor namespace (§7.2).
3. **Scene-instance avatars carry no asset identity** (§6.3). Zero downstream effect.
4. **`CreatorName` has no Unity source** (§6.3). Caller-supplied or `null`.
5. **HP §8 Layer 3 asset manifest** is a run-level obligation, deferred (§7.4).
6. **No invocation surface.** Nothing in this branch calls the collector outside tests. The
   Lab entry point is a later branch, as is any export.

## 9. Stop conditions

Halt and return for review if implementation appears to require any of:

- a change to AMUSE analysis behaviour, any result object, a shader adapter, or an evidence
  provider;
- any public API promotion in `com.alrauna.amuse`;
- any AMUSE visibility change beyond the two grants in §3.1;
- attribution added to production analysis so the census can measure it;
- widening the §6.2 amendment beyond `GetAssetPath` and `AssetPathToGUID`;
- a registry, provider framework, or options/configuration object emerging from what should
  be one static method;
- catching analysis exceptions and recording them as data (§4.3);
- the private Census Lab.

## 10. Decisions summary

| # | Decision |
|---|---|
| 1 | Explicit `GameObject` root; `GetComponentsInChildren<Renderer>(true)`; no discovery, no Animator, no VRChat coupling |
| 2 | One public type with one public method, `Collect(GameObject, string creatorName)`. Everything else in the assembly is `internal` |
| 3 | Two `InternalsVisibleTo` grants — the collector assembly and the research test assembly — traded for the removal of a production calibration class and the whole public parity API (§3.1.1) |
| 4 | Failure vocabulary reused verbatim; three exhaustive mappings, no default arm; exceptions propagate |
| 5 | Refused-renderer counts `null` never `0`; `UnsupportedTopology` gets a `null` triangle count |
| 6 | Shader family via an explicitly declared two-branch trial; no reflection in production; drift caught by a literal namespace-set pin in tests |
| 7 | The `AssetDatabase` ban is narrowed to writes; `GetAssetPath` and `AssetPathToGUID` permitted (amends HP §8) |
| 8 | The AMUSE semantics seam lives in the test assembly; production keeps only one internal pass-through overload on `RendererObservationBuilder`, mirroring AMUSE's own shape |
| 9 | Mutation safety in three layers, with the source scan as a CI test over `Editor/` only |
| 10 | The collector's triangle tally is cross-checked against `MeshSeparationPlan`'s independent count |
