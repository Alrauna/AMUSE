# Census collector — design

**Branch:** `feat/census-collector`
**Date:** 2026-08-20
**Status:** approved 2026-08-20 with four architectural changes, applied at revision 2 (§0)
**Predecessors:** `docs/superpowers/specs/2026-08-20-avatar-census-harness-preparation-design.md`
(the harness architecture, cited below as **HP §n**),
`docs/superpowers/specs/2026-08-20-census-record-schema-design.md` (tiers 1–3, commit `eedadf2`)

## 0. Revision 2 — changes required at architectural review

The architectural review approved revision 1 subject to four changes. This revision applies all four below.

| # | Required change | Where |
|---|---|---|
| 1 | Collector architecture unchanged | Nothing to do |
| 2 | Move `CensusCalibration` out of the production assembly; no permanent calibration seam API in the collector package; no hidden runtime extension points | §3.1, §5.1, §7.3 |
| 3 | Replace reflection-based frontend discovery with an explicit declared list; tests must still fail when AMUSE gains a frontend; no dependence on namespace or method reflection conventions | §5.4, §7.2 |
| 4 | Keep the public collector surface minimal; no provider or configuration abstractions unless implementation forces them | §5.1 |

Changes 2 and 4 pull in the same direction, so the revision resolves them together.
Moving calibration into the test assembly requires that assembly to see AMUSE internals.
Once it can, the parity tests can name the AMUSE enums directly.
That **deletes** the public name-list projection that revision 1 needed.
The result is a second friend grant traded for the removal of a production class *and* a public API.
Net surface goes down, not up — see §3.1.

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
The branch promotes no public API. The only production edit anywhere outside the research
package is one `InternalsVisibleTo` line (§3).

## 2. Baseline observed before design

Recorded so review can check the claims rather than trust them.

| Fact | Observed |
|---|---|
| Repository gate | EditMode suite, **770 passed / 0 failed / 0 skipped**, 29.9 s |
| Unity instance | `Application.dataPath` = `<repo-root>/Assets`, an exact same-case match, single connected instance |
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

`Alrauna.Amuse.Research.Editor` references `Alrauna.Amuse.Research.Census` and `Alrauna.Amuse.Editor`.
It is Editor-only and sets `autoReferenced: false`. No new package (HP §3.2.1).

The branch does not touch `Alrauna.Amuse.Research.Census`. It keeps `noEngineReferences: true` and
zero references, and remains Unity-free and AMUSE-free. The collector depends on the census
assembly. The dependency never points back.

### 3.1 The friend grants

`Packages/com.alrauna.amuse/Editor/AssemblyInfo.cs` gains two lines:

```csharp
[assembly: InternalsVisibleTo("Alrauna.Amuse.Research.Editor")]
[assembly: InternalsVisibleTo("Alrauna.Amuse.Research.Tests.Editor")]
```

The first is the grant HP §4.2 specified and HP §10.1 deferred to this branch. It is required:
`RendererAlphaAnalysis`, `SubmeshAlphaAnalysis`, `RendererAnalysisRefusal`, `MeshSeparationPlan`,
`SubmeshSeparationDisposition`, `TriangleAlphaOutcome`, `AlphaResolutionFailure`, and both shader
frontends are all `internal`. Without it the only alternative is reflection, rejected in HP §4.1.

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
compare against one. With the second grant they compare directly, so `CensusVocabulary` becomes
wholly `internal`, and the revision deletes the projection.

Both grantees are first-party assemblies in this repository, versioned and compiled together,
and `Alrauna.Amuse.Tests.Editor` already holds a grant of exactly this shape for exactly this
reason. A test assembly is also strictly narrower in blast radius than a production one: it
ships in no build and no release artifact, and the research package ships in neither anyway.

The cost is plain: AMUSE internals now have three consumers rather than two, which
constrains refactoring a little further. That is the direction HP §4.2 already called
desirable — a rename that breaks the census should break loudly in CI.

The final review approved the single-grant constraint this revises as a deliberate revision.
§12.2.1 records what is and is not true of the grant as shipped.

## 4. Design questions answered

### 4.1 A — avatar traversal

**Decision: explicit root `GameObject`, `GetComponentsInChildren<Renderer>(includeInactive: true)`.**

| Option | Verdict |
|---|---|
| Explicit `GameObject` root | **Chosen.** Zero coupling. Works for a scene instance and for a prefab asset. Testable from synthetic GameObjects with no SDK |
| Animator root traversal | Rejected. Requires an `Animator`, silently excludes avatars without one, and answers a rigging question rather than a scoping one |
| `VRCAvatarDescriptor` | Rejected, and **not implementable here**: `Packages/vpm-manifest.json` declares only `nadena.dev.ndmf`. The public development project has no VRChat SDK, so this path could never be exercised in public CI |

`includeInactive: true`, because an inactive renderer still ships with the avatar, and an
animation can re-enable it. Excluding it would understate avatar complexity in exactly the
direction HP §5.2 warns about.

Hierarchy order from `GetComponentsInChildren` is deterministic, and it is what fixes the
renderer ordinals downstream. If the root has a renderer, the collector includes it.

**No discovery.** There is no zero-argument entry point, no scene scan, no
`AssetDatabase.FindAssets`, no type search. The one public method requires a root the
caller names, which is the privacy requirement expressed as a signature rather than a rule.

### 4.2 B — renderer identity

Tier 1 is the debug layer, so it records what makes an anomaly traceable and nothing else.
`CensusAnonymizer` drops every field here, and none reaches tier 2.

| Field | Source | Note |
|---|---|---|
| `HierarchyPath` | `/`-joined names from the **collection root**, exclusive; `""` for the root itself | Root-relative on purpose — an absolute scene path would leak the structure *above* the avatar, which is the operator's project, not the observation |
| `GameObjectName` | `renderer.gameObject.name` | |
| `RendererTypeName` | `renderer.GetType().Name` | Raw string, therefore tier 1 only |
| `Kind` | Closed mapping (§4.3) | The only renderer-type fact that survives anonymization |

Sibling GameObjects may share a name, so `HierarchyPath` has no uniqueness guarantee. That is
accepted: it is a debugging hint, not a key, and nothing downstream indexes by it. Adding
sibling indices would harden a fingerprint for no analytical gain.

The collector records nothing else — no bounds, no bone counts, no blendshapes, no layer, no tag,
no component inventory (HP §6.4).

### 4.3 C — failure representation

**Decision: reuse the AMUSE vocabularies verbatim. Invent nothing.**

Three total, exhaustive mappings, each with **no default arm**. An AMUSE value with no
census counterpart throws `ArgumentOutOfRangeException` naming the unmapped value, matching
the precedent `CensusAnonymizer.ShaderFamily` already sets.

| AMUSE (internal) | Census | Cardinality |
|---|---|---|
| `RendererAnalysisRefusal` | `RendererRefusal` | 7 |
| `AlphaResolutionFailure` | `AlphaResolutionFailure` | 6 |
| `SubmeshSeparationDisposition` | `SeparationDisposition` | 3 |

`RendererKind` is the one mapping that is not one-to-one: the code tests
`SkinnedMeshRenderer` and `MeshRenderer` explicitly, and everything else is `Other`.
In practice `Other` is unreachable — AMUSE refuses every other type as `UnsupportedRendererType` —
but it is the conservative default, and the collector records it rather than throwing.

The four failure kinds the brief names map onto existing vocabulary without extension:

| Failure | Representation |
|---|---|
| Unsupported renderer | `RendererRefusal.UnsupportedRendererType`, counts `null` |
| Missing mesh | `RendererRefusal.MissingMesh`, counts `null` |
| Missing material | `ObservedSubmesh.HasMaterial == false`; the alpha failure is `SemanticsUnknown`, which is what AMUSE actually produces |
| Analysis exception | **Not represented. It propagates and aborts the run** |

The last is a deliberate refusal to invent: `UnityRendererAlphaAnalysis.Analyze` throws only
for a null or destroyed renderer, neither of which `GetComponentsInChildren` can return.
An exception therefore means a collector defect, and a census that catches its own defects and
records them as data produces a confident wrong number.
HP §7.2 already requires an invariant violation to abort, and that requirement covers exceptions too.
The decision also keeps the census enums pinned to the AMUSE enums, which the drift test (§7.2) depends on.

### 4.4 D — collector tests

Synthetic GameObjects, meshes, and materials constructed in code inside each test and
destroyed in teardown. No avatar, no prefab, no saved asset, no `AssetDatabase` write, no
fixture file, no Lab. Detail in §7.

## 5. What the collector does

### 5.1 Surface

The entire public API of the collector in the research package:

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
provider parameter, no registry, and no second overload.

`creatorName` is a parameter because it is the one tier 1 field Unity cannot supply (§6.3).
The caller passes `null` when it is unknown.
Everything else in the assembly — `CensusVocabulary`, `CensusShaderFamily`,
`RendererObservationBuilder`, `CensusAssetIdentity` — is `internal`.

**The semantics seam is not on this surface.** It sits one level down, as a second overload
of the internal `RendererObservationBuilder.Build`, mirroring the two-overload shape
`UnityRendererAlphaAnalysis.Analyze` already uses for its own integration tests. That is a
pass-through of an existing AMUSE seam at the narrowest scope that can carry it, not a new
extension point, and no public caller can reach or even name it (§7.3).

Per renderer: call `UnityRendererAlphaAnalysis.Analyze(renderer)`, then read the returned
immutable result. The collector adds no shader, material, texture, or geometry logic of its
own — it measures the production pipeline rather than re-deriving it.

The only Unity state it reads that `Analyze` does not is the mesh, for the refused-renderer
counts (§5.3), and the material, for identity (§6).

### 5.2 Counting an analyzed renderer

For `Refusal == None`, all counts come from the returned plan, and the collector recomputes
nothing from geometry:

- per-triangle outcomes: `Plan.Source.Submeshes[i].Outcomes`, tallied into the three
  `ProvenOpaque` / `MustRemainTransparent` / `Unknown` counts.
- disposition: `Plan.Submeshes[i].Disposition`.
- submesh and material-slot indices, `HasMaterial`, and the alpha failure:
  `analysis.Submeshes[i]`.

`analysis.Submeshes`, `Plan.Submeshes`, and `Plan.Source.Submeshes` are index-parallel by
construction. The collector asserts their lengths agree rather than assuming it.

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
a copy as a side effect when the code reads it.

The collector sums triangles as `mesh.GetIndexCount(i) / 3` over triangle-topology submeshes,
using `GetIndexCount` rather than `GetIndices`, so the code allocates no index buffer.
`UnsupportedTopology` is the deliberate asymmetry: a quad submesh has no triangle count, and any
number written there would be an invention.
`null` is the honest answer and the aggregate skips it with its own denominator.

`ObservedRenderer` enforces the analyzed-renderer half of this rule in its constructor already.
The refused half is the responsibility of the collector, and it is the first calibration case (§7.1).

**`MalformedMeshData` is the one row with no test**, here or anywhere in the repository —
AMUSE does not test that refusal either. A measurement during the final review on Unity
2022.3 showed that Unity rejects `Mesh.SetIndices` outright when the index count is not a
multiple of 3 and the topology is `MeshTopology.Triangles` — it logs an error and leaves the
submesh with zero indices rather than storing the malformed buffer.

The four AMUSE `MalformedMeshData` guards are therefore defensive against states the public
`Mesh` API will not produce, and the tests cannot construct a calibration case for them the
way they construct the other six. The branch records the row as untested rather than quietly
dropping it, and the same measurement also makes `GetIndexCount(i) / 3` safe from silent
truncation.

### 5.4 Shader family attestation

`UnityMaterialSemantics.AnalyzeBaseMaterial` runs an exclusive trial — Poiyomi, then
lilToon — and returns only the resulting `MaterialSemantics`, discarding which frontend
answered. `RendererAlphaAnalysis` therefore carries no attestation, and HP §6.1 calls
shader-family coverage the single highest-value number in the census.

**Decision: the collector declares the families it measures explicitly and repeats the trial
through the AMUSE frontends, memoized per distinct `Material`. No reflection runs in
production.**

```csharp
// The explicit declaration. Two named families, in AMUSE's own trial order.
PoiyomiMaterialSemantics.AnalyzeBaseMaterial(m).IsSupportedMaterial  → Poiyomi
LilToonMaterialSemantics.AnalyzeBaseMaterial(m).IsSupportedMaterial  → LilToon
otherwise                                                            → None
```

Revision 1 discovered the frontends by reflecting a namespace for a method named
`AnalyzeBaseMaterial`. Review change 3 rejects that, and the objection is sound: it made the
census vocabulary depend on AMUSE naming and folder conventions, so a rename that
changed nothing semantically could silently change what the census measured.

Production now names the two types directly, and the compiler checks them.

Considered and rejected: changing AMUSE to report the attesting frontend. That is a
production result-object change made solely so the census can measure something, forbidden
by HP §1, §6.2, and §10.4 — the same rule that forbids adding `Unknown` attribution.

The accepted costs, stated plainly:

- **Duplicated work.** Attestation hashes the whole normalized shader source, and this runs it a
  second time per distinct material. Memoization keeps it to once per material rather than once per
  submesh. For a one-shot research run over tens of materials this is seconds, and it buys the most
  important census number without touching production.

- **Duplicated trial order.** The collector re-states "Poiyomi first, then lilToon". If AMUSE
  gained a third frontend, the collector would silently report it as `None` and the census would
  attribute a real family to `UnknownFamily-x`. Detection is the job of §7.2.

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

**The branch narrows the ban to writes.** The research package may call exactly two read-only
members:

- `AssetDatabase.GetAssetPath`
- `AssetDatabase.AssetPathToGUID`

Every other `AssetDatabase` member stays banned, and the §8 Layer 2 CI check enforces the
narrowed rule rather than the blanket one (§7.4).

Justification: HP §8 Layer 2 carries the title *mutation safety*, and both members are pure reads
that import nothing, create nothing, and dirty nothing. The blanket ban was broader than the
concern that motivated it.

Narrowing it preserves the guarantee the layer exists to give
while letting tier 1 do the job intended for tier 1 — HP §5.1: "A census anomaly that
cannot be traced back to a concrete material is not debuggable."

Rejected alternatives: `GetInstanceID` in the GUID position (collision-free but session-local,
so it defeats the entire purpose of tier 1 and puts a non-GUID in a field named Guid),
operator-supplied identity maps (returns to manual discipline the schema branch
deliberately moved into tested code), and shipping name-only keys with the collision
documented (a knowingly wrong number in the first real census).

This amends a decision from a prior reviewed branch, and this section calls the amendment out
for that reason. It is not on the HP §10.4 stop list, but it is the kind of change no one
should make silently.

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

The design records the last two gaps rather than solving them.
`PrefabUtility.GetCorrespondingObjectFromSource` would resolve a scene instance back to its prefab,
but `PrefabUtility.` stays banned and this branch does not widen the amendment beyond what §6.1 forces.
The cost is low, and it is worth stating precisely:
`CensusAnonymizer` reads **no** `ObservedAvatar` identity field — verified in source — so avatar
identity is operator debugging aid only, and a `null` there cannot affect any tier 2 or tier 3 number.

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
| `MissingMesh` | `MeshFilter` with no `sharedMesh` | Refusal mapped; counts **`null`, not `0`** — the second of the two null-versus-zero guards |
| `SemanticsUnknown` | `MeshRenderer`, 2 triangles, Unity Standard material | `Refusal.None`; one submesh; `AlphaFailure == SemanticsUnknown`; all triangles `Unknown`; `Disposition == Unchanged`; `ShaderFamilyAttestation == None` |

`MissingMesh` is a sixth case beyond the five of HP §7.1. It costs three lines and guards the same
null-versus-zero rule from the other direction — an absent mesh rather than an unsupported
renderer — so the branch implements it instead of leaving only the case HP happened to name.

`SetPropertyBlock` appears in the *test* of the third case, never in collector source, and the
§7.4 scan covers `Editor/` only for that reason.

The two vendor-shader cases of HP §7.1 (`ProvenOpaque`, `MissingTextureEvidence`) are
**reachability** claims and remain Lab-only. Their **counting** claims run in CI through the
seam (§7.3).

### 7.2 Drift detection

Four tests, all in the research test assembly, all naming the AMUSE types directly rather than
discovering them. Their job is to make a future AMUSE change fail loudly here rather than
silently miscount in a private run — the compile-time coupling HP §4.2 argued for, extended
to values the compiler alone cannot check.

- **Three enum-parity tests.** `RendererAnalysisRefusal`, `AlphaResolutionFailure`, and
  `SubmeshSeparationDisposition` each have exactly the member names of their census mirror.
  A new AMUSE value fails here **and** throws from the missing default arm of the mapping. With
  the second grant (§3.1) these compare `Enum.GetNames` on both sides directly, so revision 1
  no longer has a public string projection.

- **One frontend-set test.** The set of namespaces directly beneath
  `Alrauna.Amuse.Editor.Semantics` equals the literal
  `{ "Alrauna.Amuse.Editor.Semantics.LilToon", "Alrauna.Amuse.Editor.Semantics.Poiyomi" }`
  — verified as the current set, and AMUSE gives each vendor frontend its own folder and
  namespace, so a third adapter fails this test in the commit that adds it, and a human then
  decides whether the census measures it.

Three further tests in the same class are not drift detection. This section lists them here so §7
accounts for every test that exists:

- **One grant proof.** The tests call `CensusVocabulary.ToCensus` across the assembly boundary, so
  a misconfigured friend grant fails once, by name, rather than as a confusing cascade in
  every later collector test.

- **Two mapping-totality tests.** The tests drive every AMUSE `AlphaResolutionFailure` and
  `SubmeshSeparationDisposition` value through its mapping and check the result to be a defined
  census value. The parity tests compare *names*.
  These prove the *switch* actually handles each one rather than reaching its throwing default arm.

`CensusShaderFamily` adds two more, for an unattested material and for an empty slot,
covering the two answers the public project can reach without a vendor shader.

On the last one, the distinction review change 3 draws is worth keeping sharp. **Production
depends on no convention** — it names two types, and the compiler checks them.

The test is a *snapshot pinned to a literal*, the same device `CensusCategorySnapshotTests`
already uses for the enums. It enumerates to compare against a hardcoded expectation, not to
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

`AvatarCensusCollector` calls the three-argument form and carries no seam parameter anywhere on
its own signature. Nothing named "calibration" exists in production. The revision deletes the
`CensusCalibration` class that revision 1 added, and the semantics construction it held now sits
beside the tests that use it.

What remains in production is one internal overload carrying one extra parameter that is a
straight pass-through to the AMUSE seam itself. That is the narrowest place the seam can live
and still be reachable, it mirrors a shape AMUSE already ships, and no public caller can reach
or name it.

The AMUSE production path remains untouched: no measurement hook, no test-only branch, no
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
than `GetAssetPath` / `AssetPathToGUID`, `AssetImporter`, `TextureImporter`, `ModelImporter`,
`EditorUtility.SetDirty`, `Undo.`, `PrefabUtility.`, `EditorSceneManager.Save`,
`SetPropertyBlock`, `.isReadable =`, `Texture2D.Apply`, `Object.Destroy`, and the
instantiating property reads `.material`, `.materials`, and `.mesh`.

The scan applies the last three as **word-boundary regexes**, not substrings. `.material` as a
substring also matches `.materialSlotIndex`, and a scan that cries wolf gets weakened or
deleted. `\.material\b` matches the accident and not the field. Correspondingly
`\.materials\b` does not match `.sharedMaterials`, and `\.mesh\b` does not match
`.sharedMesh` — which is exactly the distinction the layer exists to draw.

Path resolved as `Path.GetFullPath("Packages/com.alrauna.amuse.research/Editor")` —
**verified to resolve the embedded package**, repo-relative, no absolute path, no drive
letter. The design excludes `Tests/` (§7.1). The scan asserts a non-empty file set,
so a mis-globbed path fails rather than passing vacuously.

`renderer.material` and `renderer.materials` are the specific accidents the brief names, and
they are the reason this is a scan rather than a promise: both compile, both read plausibly,
and both silently instantiate a copy.

**Layer 3 — observable proof, per test.** After every calibration Collect, assert that
`sharedMesh` and each entry of `sharedMaterials` are reference-identical to the objects the
test created, that `HasPropertyBlock()` remains unchanged, and that the `subMeshCount` and
`vertexCount` of the mesh remain unchanged. This catches an instantiating read directly, in the same test
that exercises the path.

The full HP §8 Layer 3 asset manifest — hash every asset in scope before and after, report
`assetManifestUnchanged` — belongs to the census **run**, not the collector. This section names
it here as an obligation this branch defers, so it is not lost.

### 7.5 Arithmetic invariants

Enforced in the collector, so a violation aborts rather than records.

1. `provenOpaque + mustRemainTransparent + unknown == submesh.TriangleCount`
   (already enforced by the `ObservedSubmesh` constructor, and the collector must not defeat it).

2. `sum(submesh.TriangleCount) == renderer.TriangleCount` when `Refusal == None`
   (already enforced by the `ObservedRenderer` constructor).

3. **The collector tally equals the count AMUSE computes independently:**
   `sum(ProvenOpaqueTriangleCount) == Plan.OpaqueTriangleCount` and
   `sum(MustRemainTransparent + Unknown) == Plan.TransparentTriangleCount`.

The third is the load-bearing one and is new to this branch: it checks a number the collector
derived from `Outcomes` against a number `MeshSeparationPlanner` computed on its own, so a
misattribution bug cannot agree with itself. Note the asymmetry — the AMUSE "transparent" count
covers everything not `ProvenOpaque`, so that side includes `Unknown`.

None of the three gets a dedicated negative test, and that is a limitation worth stating
rather than papering over: forcing a violation would mean feeding the collector a fabricated
AMUSE plan, which needs a fake in place of the production analysis and would test the fake.
The code enforces them, and every calibration case exercises them instead.

### 7.6 Privacy and immutability

- **No discovery:** a test asserts the public surface of `Alrauna.Amuse.Research.Collection`
  has no method that can produce an `ObservedAvatar` without a caller-supplied `GameObject`.

- **Scope containment:** a sibling renderer outside the given root, in the same scene, never
  appears in the result.

- **Immutability:** the returned lists are the read-only wrappers the schema already
  guarantees, and a test asserts that a cast to `IList<>` either fails or throws on write.

- **The schema branch already covers non-leakage** in its tier 2 and tier 3 tests and does not
  re-litigate it here. What this branch adds is that the strings those tests protect now come
  from real Unity objects.

### 7.7 Gate

The full EditMode suite, expected at **770 + the tests added here**, zero failures, with the
test count observed and reported rather than inferred. Then a working-tree inspection
confirming only intended files changed and that the pre-existing `Packages/*.json` churn
remains untouched.

## 8. Gaps recorded, not solved

Per the brief: document, do not expand scope.

1. **Unknown attribution (HP §6.2).** A triangle can be `Unknown` on a submesh whose failure
   is `None` and AMUSE records no reason.
   The collector cannot explain it and must not make AMUSE explain it. Measuring the size of the
   blind spot — `Unknown` count on `Failure == None` submeshes — is derivable from the tier 1
   records this branch produces, and belongs to the aggregate, not the collector.

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
  provider

- any public API promotion in `com.alrauna.amuse`

- any AMUSE visibility change beyond the two grants in §3.1

- attribution added to production analysis so the census can measure it

- widening the §6.2 amendment beyond `GetAssetPath` and `AssetPathToGUID`

- a registry, provider framework, or options/configuration object emerging from what should
  be one static method

- catching analysis exceptions and recording them as data (§4.3)

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

## 11. Validation performed

Observed on 2026-08-20 at the end of implementation, not inferred.

### 11.1 Instance identity

`Application.dataPath` reported `<repo-root>/Assets` exactly, same case, from the single
reachable instance. Re-confirmed immediately before the final run. The MCP connection dropped
once mid-branch and identity was re-confirmed rather than assumed on reconnect.

### 11.2 Gate

| | |
|---|---|
| Baseline, before any change | **770 passed / 0 failed / 0 skipped**, 29.9 s |
| Final, complete EditMode suite | **802 passed / 0 failed / 0 skipped**, 35.1 s |
| Added by this branch | **32**, matching the plan's prediction exactly |

Per class: `CensusVocabularyTests` 9, `RendererRefusalCalibrationTests` 6,
`AvatarCensusCollectorTests` 11, `CollectorSeamCountingTests` 2,
`CollectorMutationSafetyTests` 2, `ResearchSourceApiBanTests` 2.

Console: **zero** errors or warnings matching `Alrauna` after the final run. (Unrelated
`mprotect returned EACCES` entries appear on this macOS host during domain reloads. They
carry no file or line and predate this branch.)

### 11.3 The source scan was verified non-vacuous

A passing guard proves nothing until it fails once.
The branch temporarily appended a `renderer.material` read and an
`AssetDatabase.CreateAsset` call to a production file.
`ProductionSourceNamesNoMutatingApi` failed, naming both — `"CensusVocabulary.cs: \.material\b"` and
`"CensusVocabulary.cs: AssetDatabase.CreateAsset"` — and the branch reverted the probe.
The failure confirms that the scan sees the real source of the embedded package and distinguishes `.material` from `.sharedMaterials`.

### 11.4 Review changes, checked in the code

| Change | Evidence |
|---|---|
| 2 — no calibration in production | No production type or file named for calibration; the only occurrence of the word is one doc-comment reference. `BaseMaterialSemanticsProvider` appears in exactly one production file, as one internal overload. Asserted by `ProductionSourceHoldsNoCalibrationOrSeamType` |
| 3 — no reflection in production | `grep` for `System.Reflection`, `GetTypes()`, `GetMethod(`, and `.Assembly` over `Editor/` returns nothing. The frontends are named directly |
| 4 — minimal public surface | `GetExportedTypes()` returns exactly `[AvatarCensusCollector]`, whose only declared public method is `Collect`. Asserted by `ThePublicSurfaceIsExactlyOneTypeWithOneMethod` |
| Census assembly untouched | Zero diff under `Editor/Census/`; still `"references": []` and `noEngineReferences: true` |
| AMUSE package | One file changed, `Editor/AssemblyInfo.cs`, containing exactly the two grant lines and their comments |

### 11.5 Not validated, and why

- **Reachability of `ProvenOpaque` and `MissingTextureEvidence` through the production
  single-argument path.** The tests of §7.3 establish *counting* only, and reaching those outcomes
  needs an attested vendor material, the public project installs no vendor shader, and this
  branch did not use the Census Lab. **This remains a Lab obligation before every census
  run**, and a census whose production path cannot reach `ProvenOpaque` must abort rather
  than report near-total `SemanticsUnknown`.

- **The three arithmetic invariants have no negative test** (§7.5). The code enforces them and
  every calibration case exercises them. Forcing a violation would require faking an AMUSE
  plan and would test the fake.

- **Real-avatar behaviour.** Nothing here observed an avatar. Every fixture is synthetic and
  built in code.

- **CI.** The branch added no workflow gate. It ran its validation locally in the Editor.

### 11.6 Census Lab

**Not used, not accessed, not modified**, at any point on this branch. The branch used Unity MCP
only against the confirmed public development project, and only for identity checks, asset
refreshes, and test runs.

## 12. Final branch review

An independent pass over the finished branch, treating §11 as a claim to re-verify rather
than as a result. Everything below was re-measured, and nothing carries over.

### 12.1 Re-verified

| Check | Result |
|---|---|
| Instance identity | `Application.dataPath` = `<repo-root>/Assets`, `productName` = `AMUSE`, single reachable instance, not in Play Mode |
| Complete EditMode suite | **802 passed / 0 failed / 0 skipped**, 32.6 s — reproduced, not quoted |
| Research assembly alone, console cleared first | **107 passed**, and the console afterwards held **zero** errors, warnings, or logs from the tests |
| Branch position | 10 ahead, **0 behind** `origin/main` (`fc8577d`) after a fresh fetch; `origin/main` is an ancestor of `HEAD`, so the merge is a fast-forward. Never pushed; no upstream configured |
| Editor left clean | After 802 tests: scene not dirty, **zero** surviving `CensusTest*` meshes or materials. The two scene roots are Unity's default camera and light |

### 12.2 Scope

Every file the branch touches lives under `Packages/` or `docs/`. No `Assets/` fixture (the
directory still holds only `.gitkeep`), no workflow, no `package.json`, no `vpm-manifest.json`,
no `packages-lock.json`, no `Library/`, `Temp/`, `Logs/`, or `UserSettings/`.

`Packages/com.alrauna.amuse` changed in exactly one file, `Editor/AssemblyInfo.cs`, purely
additively: two `InternalsVisibleTo` attributes and their comments. **Zero lines of AMUSE
code changed**, so no analysis behaviour, result object, shader adapter, or evidence provider
moved.

`Packages/com.alrauna.amuse.research/Editor/Census/` has a zero-line diff and still declares
`"references": []` with `noEngineReferences: true` — and the Unity-free, AMUSE-free assembly
that the schema branch built remains untouched.

### 12.2.1 The single-grant constraint was intentionally revised

The original brief asked for one grant, to `Alrauna.Amuse.Research.Editor`. The branch
carries two. This is a **deliberate revision of that constraint, approved at final review**,
not a drift from it, and the reasoning is worth stating plainly because the constraint and
its revision look contradictory out of context.

The author wrote the original constraint while a production calibration seam was still expected to exist.
Against that assumption, holding the grant at one was the right call: a second grant *plus*
a production seam would widen both the visibility surface and the runtime API at once.
Review change 2 removed the seam.
Once calibration construction moved into the test assembly, the second grant stopped being an
expansion of anything that ships and became the mechanism that let the production surface shrink.

What is actually true of the finished branch:

- **The branch changed no AMUSE production code for census functionality.** `Editor/AssemblyInfo.cs`
  is the only file touched in `com.alrauna.amuse`, and it gained two attributes and their
  comments. Zero lines of analysis, adapter, or evidence code changed.

- **The research collector has no calibration API.** No production type or file carries a
  calibration name, and `ProductionSourceHoldsNoCalibrationOrSeamType` asserts it. The semantics
  substitution lives in `CollectorSeamCountingTests`.

- **The second grant exists only so the test assembly can reach existing internal behaviour.**
  It grants `Alrauna.Amuse.Research.Tests.Editor` read access to internals AMUSE already had, and
  it adds no member, no hook, and no runtime extension point. A test assembly ships in no
  build and in no release artifact, and the research package ships in neither regardless.

Net effect, measured rather than argued: one additional friend grant to a test assembly,
traded for deleting a production class and four public members (§3.1.1).

### 12.3 Architecture

376 lines of production code across five types: one `public static`, three `internal static`,
one `internal sealed` (the attestation memo). Grepped and confirmed absent: interfaces,
abstract or virtual members, declared delegates, generics, and any type named for options,
configuration, a registry, a factory, a provider, a manager, or a service.

Also grepped and confirmed absent from the whole research package, production and tests
alike: `UnityWebRequest`, `HttpClient`, `System.Net`, sockets, `EditorPrefs`, `PlayerPrefs`,
`Process.Start`, serialization, and every file-writing API.
The only file I/O anywhere is `File.ReadAllText` and `Directory.GetFiles` inside the
source-scan test, which reads the source of this repository itself. **No telemetry, no
networking, no persistence, no private-data storage.**

`AssetDatabase` appears at exactly two call sites, both in `CensusAssetIdentity`, both the
approved read-only members.

### 12.4 Test quality

This review checked vacuity rather than assuming it:

- `ProductionSourceNamesNoMutatingApi` — proven to fail (§11.3).

- `ProductionSourceHoldsNoCalibrationOrSeamType` — carries a positive assertion, that
  `BaseMaterialSemanticsProvider` appears in exactly one named production file, so an empty
  scan fails.

- `AmuseDeclaresNoShaderFrontendTheCensusDoesNotMeasure` — compares to a two-element literal,
  and finding nothing fails.

- `NoPublicEntryPointCanCollectWithoutACallerSuppliedRoot` — asserts a list is empty, which
  would pass vacuously if it examined nothing.
  Measured: it examines exactly **one** method, `AvatarCensusCollector.Collect`.
  It would also pass if someone deleted `Collect`, but
  `ThePublicSurfaceIsExactlyOneTypeWithOneMethod` covers that blind spot, because it asserts
  the surface positively, so the pair is sound though neither test alone is.

Reflection over the built assembly confirms the exported surface is exactly
`[AvatarCensusCollector]` with one declared public method, and that
`CensusVocabulary`, `CensusShaderFamily`, `RendererObservationBuilder`, and
`CensusAssetIdentity` are all internal.

### 12.5 Risks and future work

1. **`MalformedMeshData` has no test and is effectively unreachable** through the public `Mesh`
   API (§5.3). Not a defect introduced here — AMUSE does not test it either — but it means
   one row of the refusal table never executed.

2. **Two AMUSE consumers of internals instead of one.** A rename now breaks the census at
   compile time. That is the intended direction (HP §4.2), but it does constrain refactoring
   further than before.

3. **Attestation runs twice per distinct material** (§5.4). Bounded by the memo and
   acceptable for a one-shot run, and it would matter if the collector ever served per-frame calls,
   which it is not designed for.

4. **The frontend-set pin cannot see a frontend added inside an existing vendor namespace**
   (§7.2).

5. **Reachability of `ProvenOpaque` and `MissingTextureEvidence` remains unproven** through
   the production path (§11.5) and is the first gate of the Lab run.

6. **No CI gate.** Validation is local. The source scan and the drift pins are exactly the
   checks that lose their value when nobody runs them, so wiring the EditMode suite into CI
   is the strongest follow-up available — and it belongs to its own branch.

7. **No invocation surface.** Nothing calls the collector outside tests, and the runner is the
   next phase.

### 12.6 Merge readiness

The branch is complete against its objective and ready to merge as a fast-forward.
`Packages/manifest.json` and `Packages/packages-lock.json` remain modified in the working
tree with the previously characterized macOS toolchain churn — additive only, exactly
`com.unity.toolchain.macos-arm64-linux-x86_64`, `com.unity.sysroot`, and
`com.unity.sysroot.linux-x86_64`, nothing removed.
They are not part of this branch, and this branch deliberately left them untouched.
