# End-to-End Alpha Analysis — Design

Milestone: `feat/end-to-end-alpha-analysis`
Date: 2026-08-20
Status: **implemented and validated (2026-08-20). Awaiting Git/PR finalization.**

Revision 2 was approved at architectural review and implemented in full. Every design
decision below stands as written; the sections marked *Measured result* record what the
implementation actually observed. Validation summary:

| | |
|---|---|
| Baseline EditMode (untouched `main`) | 666 / 666 passed, 0 failed, 0 skipped |
| Final EditMode | **695 / 695 passed, 0 failed, 0 skipped**, 27.0s |
| Tests added | 29 |
| Console errors from the final run | 0 |
| Stop conditions fired | **none** |
| Frozen components changed | none |

Revision 2 applied the review rulings: a fail-closed `MaterialPropertyBlock` guard, corrected
missing/non-finite UV dependency semantics, a corrected extra-material rationale, a
characterization-first mesh-readability policy with no speculative exception handling, a
narrowed diagnostic claim, and the removal of the proposed architecture guard because an
equivalent one already exists on `main`.

## Executive decision summary

1. **The natural unit of analysis is one `Renderer`.** `MeshSeparationPlanner` already
   consumes one mesh plus per-submesh *material binding indices*; a Unity `Renderer` is
   exactly the object that binds one mesh to a material slot list. No new "analysis unit"
   concept is invented.
2. **Supported renderer domain for v1: `SkinnedMeshRenderer` and `MeshRenderer` +
   `MeshFilter`.** Both, not for symmetry, but because real VRChat avatars use both and
   the second path costs one `GetComponent` call. Every other `Renderer` subclass refuses.
3. **A renderer carrying a `MaterialPropertyBlock` refuses outright.** A property block can
   override the very values the shader frontends read to prove alpha, so a base-material
   `ProvenOpaque` conclusion could be false for the renderer. The guard is the bare Unity
   fact `renderer.HasPropertyBlock()`; the block's *contents* are never read or
   interpreted. Property-block semantics stay deferred.
4. **Positions come from `Mesh.vertices` in mesh-local space. No transform, no
   `BakeMesh`, no blendshape or bone evaluation.** The classifier consumes positions for
   exactly two purposes — a finiteness check and an exact degeneracy test — and both are
   safe in local space in both directions.
5. **Missing or non-finite UV0 makes UV0 *unavailable*, not the conclusion *unknown*.**
   The triangle is handed to `TriangleAlphaInput.MissingUv0(...)` and the resolution
   decides: a constant alpha of exactly 1 still proves `ProvenOpaque`; a texture-sampled
   alpha that actually needs UV0 becomes `Unknown`. Missing knowledge invalidates only the
   conclusions that depend on it.
6. **Material-slot mapping is supported only when
   `renderer.sharedMaterials.Length == mesh.subMeshCount`.** Not because Unity's behaviour
   is unknown — it is documented — but because `MeshSeparationPlanner` can carry exactly
   one `SourceMaterialBindingIndex` per source submesh and therefore cannot represent the
   extra material passes Unity performs. A `null` slot at a matching count is *not* a
   refusal: it yields Unknown semantics for that submesh alone.
7. **Refusal granularity is the submesh/material-slot pair for everything the resolver can
   express, and the triangle for geometry-local uncertainty.** An unsupported material, an
   unknown alpha equation, or missing texture evidence poisons one submesh and leaves its
   neighbours fully analyzable. Only renderer-scoped facts refuse the whole renderer.
8. **No new diagnostic hierarchy.** The existing `AlphaResolutionFailure` enum names every
   per-submesh refusal this milestone can produce. Frontend diagnostics are deliberately
   *not* propagated, and triangle-level `Unknown` deliberately carries no reason.
9. **One new immutable result type is required**, because no existing type can express
   "why a submesh was preserved at the material/resolver level" or "this renderer produced
   no plan at all". It deliberately does **not** retain a reason for each triangle-local
   `Unknown`; `MeshSeparationPlan.Source.Outcomes` still distinguishes `Unknown` from
   `MustRemainTransparent`, but the cause of a given `Unknown` is not preserved.
10. **One new seam, approved in review**: an
    `internal delegate MaterialSemantics BaseMaterialSemanticsProvider(Material)`,
    mirroring the existing `AlphaFieldProvider` precedent exactly. **No shader adapter
    interface, registry, or framework.** Frontend selection is a two-branch `if`.
11. **Two new production files.** `Editor/Host/UnityRendererAlphaAnalysis.cs` and
    `Editor/Semantics/UnityMaterialSemantics.cs`, each with its Unity-generated `.meta`.
12. **Nothing mutates.** `sharedMesh` and `sharedMaterials` only — never `MeshFilter.mesh`
    or `Renderer.materials`, both of which instantiate copies as a side effect.

## Verified base state

Derived at run time from `git rev-parse --show-toplevel`; no absolute checkout path is
recorded here.

| Check | Result |
|---|---|
| Branch | `feat/end-to-end-alpha-analysis` |
| Base commit | `7f37b11` (`Merge pull request #14 from Alrauna/chore/reproducible-vpm-setup`) |
| `origin/main` at fetch | `7f37b11` — identical |
| Reproducible-VPM-setup PR merged | Yes, PR #14 (`2659d40` documented setup) |
| Platform-agnostic agent policy merged | Yes, PR #13 (`a4ce855`) |
| Texture-alpha-evidence merged | Yes, PR #12 (`6209cf2`) |
| Local `main` fast-forward | `10d25c1` → `7f37b11`, ancestor-verified, clean |
| Working tree at branch creation | Clean |
| `Editor/Host/UnityAlphaFieldEvidence.cs` on main | Present |
| `Tests/Editor/Host/AlphaEvidenceClassifierIntegrationTests.cs` on main | Present |
| Reproducible-VPM setup docs on main | `README.md` + `docs/superpowers/specs/2026-08-19-reproducible-vpm-setup-design.md` |

## Dependency and development-environment state

| Check | Result |
|---|---|
| `Packages/vpm-manifest.json` requirement | `nadena.dev.ndmf` 1.14.4 (locked) |
| Resolved package | `Packages/nadena.dev.ndmf/package.json` reports `nadena.dev.ndmf` `1.14.4` |
| NDMF standalone bootstrap | Already run — `Packages/nadena.dev.ndmf/Dependencies/` present with the six expected assemblies plus licences |
| Working tree after inspection | Clean; no manifest touched |
| VPM repositories | Not inspected, not modified |
| Unity Editor MCP instance | **None reachable.** `execute_code` returned `No Unity Editor instances found`. |

No reproducibility defect was found. Editor-observed validation is therefore **blocked
until a Unity instance is available** and is scheduled as plan tasks. The private avatar
testbed was not contacted, inspected, or used.

## Architecture inventory (read on this branch's base commit)

### Analysis (namespace `Alrauna.Amuse.Editor.Analysis`, **no `UnityEditor` dependency**)

- `ExactUvGeometry.cs` (552 lines) — `ExactRational`, `ExactDyadic`, `ExactInterval`,
  `ExactUvDomain`, `IsDegenerateGeometry`, `CreateTextureScaledDomain`, `NormalizeRepeat`,
  `Intersects`, `FloorDiv`, `FloorMod`.
- `TriangleAlphaClassifier.cs` (610 lines) — `TriangleAlphaOutcome`, `AlphaFilterMode`,
  `AlphaWrapMode`, `AlphaSamplingSettings`, `TriangleAlphaInput`, `AlphaTextureData`,
  `TriangleAlphaClassifier`.
- `AlphaSemanticsResolver.cs` (333 lines) — `AlphaResolutionFailure`, `AlphaFieldProvider`
  (delegate), `AlphaResolution`, `AlphaSemanticsResolver`.
- `MeshSeparationPlanner.cs` (220 lines) — `SubmeshSeparationDisposition`,
  `SubmeshSeparationInput`, `MeshSeparationInput`, `SubmeshSeparationPlan`,
  `MeshSeparationPlan`, `MeshSeparationPlanner`.

### Semantics (namespace `Alrauna.Amuse.Editor.Semantics`)

- `MaterialSemantics.cs` (761) — `TextureSourceId`, `UvMapping`, `TextureFilterMode`,
  `TextureWrapMode`, `TextureSampling`, `TextureSample`, `TextureColorInterpretation`,
  `TextureChannel`, `ColorSemanticValue`, `ScalarSemanticValue`, `NormalSemanticValue`,
  `SemanticOutput<T>`, `MaterialSemantics`. No `UnityEditor`.
- `UnityTextureEvidence.cs` (215) — uses `UnityEditor`; five refusal predicates including
  `TryGetSourceId` and `TryGetSampling`.
- `Poiyomi/PoiyomiMaterialSemantics.cs` (1439) — `AnalyzeBaseMaterial(Material)`,
  `InterpretVerifiedMaterial(Material, ColorSpace)`, `PoiyomiSemanticResult`,
  `PoiyomiSemanticDiagnostic`, `PoiyomiSemanticDiagnosticCode`.
- `LilToon/LilToonMaterialSemantics.cs` (1076) + `LilToonSourceAttestation.cs` (916) —
  `AnalyzeBaseMaterial(Material)`,
  `InterpretVerifiedMaterial(Material, ColorSpace, IReadOnlyCollection<string>)`,
  `LilToonSemanticResult`, `LilToonSemanticDiagnostic`, `LilToonSemanticDiagnosticCode`.

### Host (namespace `Alrauna.Amuse.Editor.Host`)

- `UnityAlphaFieldEvidence.cs` (225) — instance-scoped `AlphaFieldProvider` implementation.
  Constructed from `IEnumerable<Texture>`; resolves identity through
  `UnityTextureEvidence.TryGetSourceId`; never parses `TextureSourceId`.

### Assembly and namespace boundaries

One production assembly `Alrauna.Amuse.Editor` (Editor-only, references `nadena.dev.ndmf`
but uses no NDMF type today), one test assembly `Alrauna.Amuse.Tests.Editor` with
`InternalsVisibleTo`. Boundaries are enforced by namespace and file placement, not by
separate assemblies. **Measured fact: no file under `Editor/Analysis/` uses `UnityEditor`;
four files under `Editor/Semantics/` do; `Editor/Host/` currently does not.**

### Tests inventory

525 `[Test]`/`[TestCase]` attributes across 24 test files. Directly relevant:
`MeshSeparationPlannerTests` (planner contract, including
`EmptyMiddleSubmeshDoesNotShiftBindingProvenance`, `UnknownNeverEntersOpaqueMembership`,
`ReplacingProvenOpaqueCannotIncreaseOpaqueCount`), `TriangleAlphaClassifierTests`,
`AlphaSemanticsResolverTests`, `UnityAlphaFieldEvidenceTests`,
`AlphaEvidenceClassifierIntegrationTests`, the Poiyomi/lilToon fixture suites, and the five
characterization suites.

**An `Analysis` → no-`UnityEditor` architecture guard already exists**, in
`UnityAlphaFieldEvidenceTests` under the "Architecture boundary" region:
`AnalysisNamespace_HasNoDependencyOnTheUnityEditorNamespace`, made non-vacuous by
`UnityEditorDetector_ReportsADirectoryThatDoesDependOnIt`, which points the same
word-boundary `\bUnityEditor\b` detector at `Editor/Semantics`. It also asserts
`fileCount > 0`. This milestone adds no guard; see "Architecture guard".

## Question 1 — the exact current `MeshSeparationPlanner` contract

```csharp
internal static MeshSeparationPlan MeshSeparationPlanner.Create(MeshSeparationInput input)
```

### Input

```csharp
MeshSeparationInput {
    int VertexCount;                                   // >= 0
    IReadOnlyList<SubmeshSeparationInput> Submeshes;   // order is identity
}
SubmeshSeparationInput {
    int SourceMaterialBindingIndex;                    // >= 0, opaque to the planner
    IReadOnlyList<int> Indices;                        // flat vertex-index triples
    IReadOnlyList<TriangleAlphaOutcome> Outcomes;      // one per triangle
    int TriangleCount;                                 // Indices.Count / 3
}
```

Answers to the specific sub-questions:

- **Exact input type:** `MeshSeparationInput`. Constructed eagerly; both constructors copy
  every caller collection, so the input is immutable after construction.
- **Triangle identity:** the *ordinal within its submesh*, `index / 3`. Ordinals restart at
  zero in each submesh (`TriangleOrdinalsRestartWithinEachSourceSubmesh`).
- **Consumes classifier results directly?** It consumes the bare
  `TriangleAlphaOutcome` enum, not `AlphaResolution` and not any classifier result object.
- **Opaque/preserved representation:** two ordinal lists per submesh
  (`OpaqueTriangleOrdinals`, `TransparentTriangleOrdinals`) plus a
  `SubmeshSeparationDisposition` of `Unchanged` / `WhollyOpaqueCandidate` / `Split`.
- **Per mesh, submesh, or material?** Per **mesh**, subdivided by **submesh**, with a
  material binding carried as an opaque integer.
- **Does it know material slots?** Only as `SourceMaterialBindingIndex`. It never
  dereferences it, never compares two bindings, and never groups by it. **It carries
  exactly one binding per source submesh** — the fact that decides Question 2's
  extra-material rule.
- **`Unknown` vs `MustRemainTransparent` downstream:** *indistinguishable in the
  disposition*. The planner's only test is `outcome == ProvenOpaque`; everything else lands
  in `TransparentTriangleOrdinals`. That list is therefore "preserved", not "transparent".
  The distinction is still recoverable from `MeshSeparationPlan.Source`, which retains the
  original `SubmeshSeparationInput.Outcomes`.
- **Malformed input:** throws. `ArgumentNullException` for null input/list/element,
  `ArgumentOutOfRangeException` for a negative vertex count, a negative binding index, an
  undefined outcome value, or an index outside `[0, VertexCount)`, and `ArgumentException`
  for an index count not divisible by three or an outcome count unequal to the triangle
  count. **The planner never degrades; a caller must pre-validate.**
- **Invariants enforced:** complete index triples; outcome/triangle count agreement; every
  index references an existing vertex; every outcome is a defined enum value.
- **Output:** exactly one immutable `MeshSeparationPlan` per call, carrying `Source`, one
  `SubmeshSeparationPlan` per input submesh in input order, `HasAnyOpaqueCandidates`,
  `RequiresAnySplit`, `OpaqueTriangleCount`, `TransparentTriangleCount`.
- **One plan or several operations?** One plan. It is a description, not an operation list.

### The one contract detail that shapes this whole milestone

`SubmeshSeparationPlan.SourceSubmeshIndex` is **assigned positionally by the planner's own
loop counter**, not supplied by the caller. Therefore the plan's submesh index equals the
Unity submesh index *only if every submesh is supplied, in order*. Omitting an
unanalyzable submesh would silently renumber its successors and mis-attribute every
downstream mutation.

This is a contract *constraint*, not a mismatch requiring redesign: it is satisfiable by
always supplying every submesh, with `Unknown` outcomes where nothing could be proven.
**No change to `MeshSeparationPlanner` is proposed, and none is authorized.**

It does force one coarse refusal: a submesh whose topology is not `Triangles` cannot be
represented at all (its index count is generally not divisible by three, and substituting
an empty index list would misreport real geometry as absent). Such a mesh therefore
refuses as a whole. Approved in review for v1. See Question 7.

## Question 2 — the natural unit of renderer analysis

**One `Renderer`.**

The planner's unit is one mesh plus per-submesh material binding indices. Unity's
`Renderer` is precisely the object that supplies both halves: the mesh (via
`SkinnedMeshRenderer.sharedMesh` or `MeshFilter.sharedMesh`) and the ordered material slot
list (`sharedMaterials`). Nothing smaller carries both; nothing larger is needed.

This does **not** imply a stateful `RendererAnalyzer` object. The proposal is one static
entry point returning one immutable result — the same shape as every other component here.

The *sub-unit of refusal and provenance* is the submesh/material-slot pair, because that is
what `SubmeshSeparationInput` already indexes and what `AlphaResolution` is already scoped
to ("A refusal is material-scoped", per the resolver's own documentation).

### Unity topology states and their v1 dispositions

| State | v1 behaviour | Reason |
|---|---|---|
| `renderer.HasPropertyBlock()` | **Refuse whole renderer** (`MaterialPropertyOverridesPresent`) | A block can override the properties the frontends read to prove alpha, so a base-material proof may not hold for this renderer |
| `sharedMaterials.Length == subMeshCount` | **Supported.** Slot *i* ↔ submesh *i* | The one-binding-per-submesh form the planner can represent |
| More materials than submeshes | **Refuse whole renderer** (`UnprovenMaterialSlotMapping`) | Unity's behaviour here is *documented, not unknown*: the last submesh is drawn once with its own material and again for each surplus material. `SubmeshSeparationInput` carries exactly one `SourceMaterialBindingIndex` per source submesh, so AMUSE cannot truthfully represent those additional passes. Widening the planner is not authorized. |
| Fewer materials than submeshes | **Refuse whole renderer** (same) | No repository or authoritative Unity evidence has been established for a complete mapping AMUSE can represent; conservative refusal is retained until it is |
| `null` material in a slot | **Supported.** That submesh only → Unknown | The mapping is still proven; only the semantics are absent |
| Repeated material reference | **Supported.** Semantics memoized per material | Distinct submeshes, one resolution |
| Empty submesh (`indexCount == 0`) | **Supported.** Zero triangles, disposition `Unchanged` | Already covered by `EmptySubmeshRemainsRepresentedWithItsBinding` |
| Vertices shared across submeshes | **Supported and ignored** | Only matters when geometry is actually split; that is the mutation milestone |
| Missing mesh (`sharedMesh == null`, or `MeshRenderer` with no `MeshFilter`) | **Refuse whole renderer** (`MissingMesh`) | |
| Destroyed renderer / mesh / material | Renderer or mesh → refuse; material → that slot is Unknown | Unity's overloaded `==` reports destroyed as null |
| Unsupported renderer class | **Refuse whole renderer** (`UnsupportedRendererType`) | |

### Refusal precedence

Renderer-scoped checks run in a fixed order so results are deterministic and tests are
unambiguous:

1. `UnsupportedRendererType`
2. `MaterialPropertyOverridesPresent`
3. `MissingMesh`
4. `UnprovenMaterialSlotMapping`
5. `UnsupportedTopology`
6. `MalformedMeshData`

## Question 3 — renderer types in v1

**`SkinnedMeshRenderer` and `MeshRenderer` (+ its sibling `MeshFilter`).**

Justification, not symmetry: a VRChat avatar's body and clothing are `SkinnedMeshRenderer`,
while rigid props, accessories, and many world-space attachments are `MeshRenderer`.
Supporting only one would leave a routine part of every real avatar unanalyzable. The
second path is `renderer.GetComponent<MeshFilter>()?.sharedMesh` — one call, one extra
refusal reason, no second extraction algorithm: both paths converge on the same `Mesh`.

Dispatch is `switch (renderer) { case SkinnedMeshRenderer: … case MeshRenderer: … default:
refuse }`. `ParticleSystemRenderer`, `LineRenderer`, `TrailRenderer`, `SpriteRenderer`, and
`BillboardRenderer` derive from `Renderer` directly, not from `MeshRenderer`, so the `is`
test cannot capture them accidentally.

### What is *not* consulted, and why

- **`BakeMesh` — never called.** It allocates and writes a mesh; it is a mutation-shaped
  operation and it is explicitly out of scope.
- **Blendshapes — irrelevant.** They move positions. Positions feed only finiteness and
  degeneracy (Question 12). They never move UV0.
- **Bone weights / bind poses — irrelevant**, for the same reason.
- **`Renderer.transform` — irrelevant**, for the same reason, and consulting it would make
  a material analysis depend on an animatable, non-material property.
- **`Renderer.enabled`, layers, bounds, light probes, sorting — irrelevant** to alpha.
- **`MaterialPropertyBlock` contents — never read.** Only the presence bit
  `HasPropertyBlock()` is consulted, as a fail-closed guard. See below.

### The `MaterialPropertyBlock` guard

A `MaterialPropertyBlock` set on a renderer overrides material property values and texture
references for that renderer's draw. The shader frontends prove alpha from the **base
material's** properties. Therefore:

```
base Material proves alpha == 1
  + renderer property override changes _Color.a or _MainTex
  → a base-only ProvenOpaque conclusion can be false for this renderer
```

That is a false positive, which is a correctness bug, not an acceptable false negative. The
guard is therefore a hard renderer-level refusal:

```csharp
if (renderer.HasPropertyBlock())
    return RendererAlphaAnalysis.Refused(
        RendererAnalysisRefusal.MaterialPropertyOverridesPresent);
```

It reads one boolean. It does **not** call `GetPropertyBlock`, does not enumerate overridden
names, does not compare an override against the base value, and does not attempt to decide
whether a particular override is alpha-relevant. Doing any of that is property-block
semantics, which remains deferred to the effective-state milestone. The refusal is
deliberately coarse: a renderer with a property block that overrides nothing alpha-related
is still refused, which is a false negative and therefore acceptable.

**Verification obligation — discharged (2026-08-20).** `Renderer.HasPropertyBlock()` is
documented as reporting whether a block has been attached via `SetPropertyBlock`. Whether it
also reports a block attached to a *single material index* was the open question, and a hole
there would have been a stop condition.

Measured on Unity 2022.3.22f1 in this project, by
`UnityRendererAlphaAnalysisTests.APropertyBlockRefusesTheWholeRenderer` and
`APerMaterialIndexPropertyBlockAlsoRefuses`:

| Attachment | `HasPropertyBlock()` | Analysis result |
|---|---|---|
| `SetPropertyBlock(block)` | `true` | `MaterialPropertyOverridesPresent`, `Plan == null` |
| `SetPropertyBlock(block, 0)` | **`true`** | `MaterialPropertyOverridesPresent`, `Plan == null` |

**`HasPropertyBlock()` covers index-scoped blocks, so the guard has no hole and the stop
condition did not fire.** Both fixtures attach a real, non-empty block overriding `_Color`,
a property the fixture material genuinely declares.

## Question 4 — Unity mesh triangles → `TriangleAlphaInput`

### Extraction contract

| Fact | Source | Notes |
|---|---|---|
| Vertex count | `mesh.vertexCount` | Also feeds `MeshSeparationInput.VertexCount` |
| Positions | `mesh.vertices` | One call per renderer; local space |
| UV0 | `mesh.uv` | One call per renderer; length 0 or `vertexCount` |
| Submesh count | `mesh.subMeshCount` | |
| Topology | `mesh.GetTopology(submesh)` | Must equal `MeshTopology.Triangles` |
| Indices | `mesh.GetIndices(submesh)` | `applyBaseVertex` defaults to `true`, so returned indices are already absolute |

Triangle ordering is `mesh.GetIndices` order, unmodified: triangle *n* of submesh *s* is
indices `[3n, 3n+1, 3n+2]`. Winding is preserved and never reordered — the planner already
pins this with `SourceIndexTriplesPreserveWindingAndOrder`.

### Validation performed before anything is handed on

Mesh-scoped (failure refuses the whole renderer):

1. `subMeshCount` matches `sharedMaterials.Length` (Question 2).
2. Every submesh's topology is `Triangles`.
3. `positions.Length == vertexCount`.
4. `uv.Length` is either `0` or `vertexCount`.
5. Every index is within `[0, vertexCount)` and every submesh index count is divisible by
   three.

Triangle-scoped:

6. All three positions finite. A non-finite position yields `Unknown` for that triangle.
7. If a UV0 array is present, all three of the triangle's UVs finite. A non-finite UV makes
   **UV0 unavailable for that triangle**, not the conclusion unknown — see Questions 13
   and 14.

Rule 5 is duplicated by `MeshSeparationInput`'s own validation on purpose: the planner
*throws* where renderer analysis must *refuse*, and a refusal is only conservative if it
happens before the throw.

### Deliberate non-behaviours

No UV recomputation, no UV synthesis, no triangulation of other topologies, no index
repair, no vertex welding, no sampling or approximation of geometry, no normalization of
out-of-range UVs (the classifier's exact `NormalizeRepeat` owns that). The exact classifier
remains the sole authority on what a triangle's alpha is.

### Mesh readability and exception policy — characterization precedes policy

`Mesh.isReadable` is `false` for imported model assets without *Read/Write Enabled*, which
is the default and the common case on real avatars.

Unity's documentation states that mesh data access is permitted from Editor code outside
the game/rendering loop even when `isReadable` is `false`, which is the regime EditMode
tests and a build-time NDMF pass both run in. That was **measured before the production
read path was written**, not assumed.

### Measured result (2026-08-20, Unity 2022.3.22f1, this project)

`MeshReadabilityCharacterizationTests.NonReadableImportedMeshCanBeReadFromEditorCode`
imported a generated one-triangle `.obj`, set `ModelImporter.isReadable = false`, confirmed
`Mesh.isReadable == false`, and exercised each read once inside its own
`Assert.DoesNotThrow`. Observed:

```
isReadable=False, vertexCount=3, vertices=3, uv=3, indices=3,
topology=Triangles, subMeshCount=1
```

**All three of `mesh.vertices`, `mesh.uv`, and `mesh.GetIndices(0)` succeeded and returned
complete, correctly sized arrays.** Neither threw, and none returned a short or empty
array. Unity's documented Editor-access behaviour holds here.

### Policy that follows from it

- **Production needs no unreadable-mesh refusal path.** `isReadable == false` is simply not
  an obstacle in the Editor, so there is no `Mesh.isReadable` pre-check anywhere in
  production. Renderer analysis reads plainly and validates rules 3–5 afterwards;
  `MalformedMeshData` remains for genuinely inconsistent data, never as a stand-in for a
  readability check.
- **No exception handling around mesh reads.** There is no `try`/`catch`, and
  `catch (Exception)` appears nowhere. Catching what has been observed not to throw would
  hide the very defect the characterization exists to find.
- **If a future environment does throw**, the characterization fails naming the exact
  operation, and NUnit reports the exact exception type. Implementation stops for
  architectural review at that point rather than adopting a catch policy silently.

## Questions 11 & 12 — coordinate space, and why it is sufficient

**Mesh-local space, exactly as stored in `Mesh.vertices`.**

Positions reach only two places in the classifier:

```csharp
ValidateFinite(triangle.Position0..2);            // throws on NaN/Inf
if (ExactUvGeometry.IsDegenerateGeometry(triangle)) return Unknown;
```

`IsDegenerateGeometry` is an exact zero-cross-product test over the three positions decoded
to dyadic rationals. Positions influence **nothing else** — not the UV domain, not the
texel support, not the sampled predicate. `CreateTextureScaledDomain` reads only
`Uv0`/`Uv1`/`Uv2`.

Both error directions are safe:

- *Local non-degenerate, deformed degenerate.* Analysis proceeds and may prove opacity.
  The deformed triangle collapses to zero area and rasterizes nothing, so its membership in
  an opaque group cannot change any rendered pixel.
- *Local degenerate, deformed non-degenerate.* Analysis returns `Unknown` and the triangle
  is preserved. Conservative by construction.

Local space is additionally the only choice consistent with the milestone's state model:
world space would make the result depend on `Transform`, and deformed space would make it
depend on bone poses and blendshape weights — all animatable state this milestone
explicitly does not analyze. A material conclusion that changed when an avatar moved would
be incoherent.

## Questions 13 & 14 — UV0 rules

### The dependency rule

UV0 is an *input the resolution may or may not need*. Its absence therefore invalidates
only the conclusions that depend on it:

| Alpha resolution | UV0 present | UV0 unavailable |
|---|---|---|
| `Uniform(ProvenOpaque)` — constant alpha exactly 1 | `ProvenOpaque` | **`ProvenOpaque`** — a constant cannot vary across a surface, so no UV is needed |
| `Uniform(MustRemainTransparent)` — constant alpha < 1, or a sub-unit multiplier | `MustRemainTransparent` | `MustRemainTransparent` |
| `Classified(field, sampling)` — texture-sampled alpha | classifier decides | **`Unknown`** — the classifier's `!triangle.HasUv0` arm, because this equation genuinely needs UV0 |
| Refused | never classified | never classified — all triangles `Unknown` |

This falls out of the existing components without a new branch: `AlphaResolution.Classify`
short-circuits uniform resolutions before consulting geometry, and
`TriangleAlphaClassifier.Classify` returns `Unknown` for `!HasUv0`. Renderer analysis must
therefore *not* pre-empt the decision.

### Mesh states

| Mesh state | Behaviour |
|---|---|
| `mesh.uv.Length == vertexCount` | UV0 present; `TriangleAlphaInput.WithUv0(...)` |
| `mesh.uv.Length == 0` | UV0 unavailable for every triangle; `TriangleAlphaInput.MissingUv0(...)`; the resolution decides per the table above. **Not a refusal, and not an automatic `Unknown`.** |
| `mesh.uv.Length` anything else | Mesh-level `MalformedMeshData` refusal |
| A triangle's UV contains NaN or ±Inf | UV0 unavailable **for that triangle only**; `TriangleAlphaInput.MissingUv0(...)`; the resolution decides. The finiteness pre-check exists because the classifier throws on non-finite UVs, not because non-finite implies Unknown. |
| UVs far outside [0,1] | Not our concern. The classifier's `NormalizeRepeat` and `MaxSupportRegions` guard already return `Unknown` when the support region is too large |

Only UV channel 0 is read, because `AlphaSemanticsResolver.IsSupportedMapping` accepts only
`Channel == 0` with unit scale and zero offset. Reading any other channel would be work
with no consumer.

### The one place the dependency rule cannot be applied symmetrically

Positions have no "unavailable" form: `TriangleAlphaInput` requires three of them, and
`AlphaResolution` does not expose whether it is uniform. A triangle with a **non-finite
position** therefore yields `Unknown` even under a constant-alpha resolution that would not
have consulted geometry at all. That is a false negative on malformed data, which is the
acceptable direction. Expressing it correctly would need either a `MissingPositions` form
on `TriangleAlphaInput` or an `IsUniform` accessor on `AlphaResolution` — both are changes
to approved-frozen components, so **the asymmetry is accepted and documented rather than
fixed.**

## Question 15 — topology and malformed-state matrix

| Condition | Scope | Result |
|---|---|---|
| Renderer is null or destroyed | Renderer | `ArgumentNullException` / `ArgumentException` (a caller defect, matching the frontends' `RequireAnalyzableMaterial` precedent) |
| Renderer type unsupported | Renderer | `UnsupportedRendererType`, no plan |
| `renderer.HasPropertyBlock()` | Renderer | `MaterialPropertyOverridesPresent`, no plan |
| `sharedMesh == null` / no `MeshFilter` / destroyed mesh | Renderer | `MissingMesh`, no plan |
| `subMeshCount != sharedMaterials.Length` | Renderer | `UnprovenMaterialSlotMapping`, no plan |
| Any submesh topology ≠ `Triangles` | Renderer | `UnsupportedTopology`, no plan |
| Position/UV array length inconsistent, index out of range, or index count not a multiple of 3 | Renderer | `MalformedMeshData`, no plan |
| `subMeshCount == 0` | Renderer | Supported; a plan with zero submeshes (`EmptyMeshIsAValidNoOp` already covers the planner side) |
| Slot material null or destroyed | Submesh | Semantics all-Unknown → `AlphaResolutionFailure.SemanticsUnknown`; `HasMaterial == false` records the Unity fact |
| Material shader unsupported by both frontends | Submesh | `SemanticsUnknown` |
| Alpha equation not representable | Submesh | `SemanticsUnknown` |
| Alpha multiplier > 1 | Submesh | `UnsupportedMultiplier` |
| Non-zero UV offset/scale, or UV channel ≠ 0 | Submesh | `UnsupportedUvMapping` |
| Sampler outside Point/Bilinear × Clamp/Repeat | Submesh | `UnsupportedSampling` |
| Texture non-readable, mipmapped, compressed, wrong format, or unidentifiable | Submesh | `MissingTextureEvidence` |
| Submesh with zero indices | Submesh | Zero triangles, `Unchanged` |
| Missing UV0 array | **Triangle** (every triangle) | UV0 unavailable; the resolution decides |
| Non-finite UV on one triangle | **Triangle** | UV0 unavailable for it; the resolution decides |
| Non-finite position on one triangle | **Triangle** | `Unknown` (no "unavailable positions" form exists) |
| Degenerate triangle | **Triangle** | `Unknown` (classifier's own rule) |
| UV support region above `MaxSupportRegions` | **Triangle** | `Unknown` (classifier's own rule) |

Defensive guards for states Unity cannot naturally construct (an out-of-range index in an
imported mesh, for instance) are code, not tests. They are reviewed, not mocked.

## Questions 5, 16, 17 — Material → `MaterialSemantics` orchestration

### What already exists

Both frontends expose the identical shape:

```csharp
PoiyomiSemanticResult PoiyomiMaterialSemantics.AnalyzeBaseMaterial(Material);
LilToonSemanticResult LilToonMaterialSemantics.AnalyzeBaseMaterial(Material);
// each: { bool IsSupportedMaterial; MaterialSemantics Semantics; IReadOnlyList<…Diagnostic> Diagnostics }
```

Each performs its **own** shader identity attestation internally (shader name, GUID,
version, normalized source hash) and, when unsupported, returns `IsSupportedMaterial ==
false` with an all-`Unknown` `MaterialSemantics` plus one diagnostic. Both throw for a
null, destroyed, or shaderless material.

### The decision, approved in review: one explicit two-branch dispatch, no framework

```csharp
// Editor/Semantics/UnityMaterialSemantics.cs
internal static MaterialSemantics AnalyzeBaseMaterial(Material material)
{
    if (material == null || material.shader == null) return AllUnknown();

    var poiyomi = PoiyomiMaterialSemantics.AnalyzeBaseMaterial(material);
    if (poiyomi.IsSupportedMaterial) return poiyomi.Semantics;

    // An unsupported lilToon result is itself all-Unknown, which is exactly the
    // correct answer for a material neither frontend attests.
    return LilToonMaterialSemantics.AnalyzeBaseMaterial(material).Semantics;
}
```

That is the whole of frontend selection. Explicitly **not** proposed: `IShaderAdapter`, an
adapter registry, a shader registry, dependency injection, a provider framework, or a
priority/ordering policy. Two supported families are not evidence for a framework, and the
attestation each frontend already performs makes an external dispatch table redundant —
adding one would create a *second* place where "is this a Poiyomi material" is decided, and
the two could disagree. The trial order is irrelevant to correctness because attestation is
exclusive: no material can be attested by both.

Cost: one wasted attestation per non-Poiyomi material. Mitigated by memoizing per material
within one analysis run (Performance, below). If a third family ever lands, this becomes a
three-branch `if` — and *that* is when a registry earns its first honest argument.

`null`-slot handling lives here rather than at the call site because the frontends throw on
null and the correct answer is `AllUnknown()`, which is a semantics fact.

## Questions 18 & 19 — the public-project vendor-shader limitation

### The limitation, stated exactly

The public development project contains **no** Poiyomi or lilToon vendor package. The
existing suites work around this with schema-complete stand-in shaders
(`PoiyomiSemanticTest.shader`, `LilToonSemanticTest.shader`) driven through each
frontend's documented `InterpretVerifiedMaterial` friend-test seam, which skips
attestation. `AnalyzeBaseMaterial` on a stand-in material therefore returns
`IsSupportedMaterial == false`, correctly.

Consequently, in the public project:

- **Truthfully exercisable:** that `UnityMaterialSemantics.AnalyzeBaseMaterial` returns
  all-`Unknown` for a non-vendor, null, destroyed, or shaderless material — the real
  refusal path, on real Unity objects, with no substitution.
- **Not exercisable:** that a real vendor material dispatches to its frontend and yields
  proven alpha. That requires a vendor package. It is a **production capability, not a
  tested capability**, in the public suite.

Adding vendor packages, vendoring shader sources, weakening attestation, or fabricating a
"nearly-canonical" shader are all explicitly refused. Using the private testbed as the
oracle is refused.

### The one substituted link, and the approved seam

The deterministic integration fixture exercises:

```
real Renderer → real Mesh → real submeshes → real material slots
    → [SUBSTITUTED: attestation only] →
real PoiyomiMaterialSemantics interpreter → real AlphaSemanticsResolver
    → real UnityAlphaFieldEvidence → real Texture2D asset
    → real TriangleAlphaClassifier → real MeshSeparationPlanner → real plan
```

The substituted `MaterialSemantics` is not hand-written: it is produced by the **real**
`PoiyomiMaterialSemantics.InterpretVerifiedMaterial` seam over a real stand-in `Material`,
so the semantics under test are genuine interpreter output. Only the *attestation* step is
bypassed, and it is bypassed by the seam the previous milestone already built and
documented for exactly this reason.

Injecting that requires the renderer analysis to accept a semantics source:

```csharp
internal delegate MaterialSemantics BaseMaterialSemanticsProvider(Material material);

internal static RendererAlphaAnalysis Analyze(Renderer renderer)
    => Analyze(renderer, UnityMaterialSemantics.AnalyzeBaseMaterial);

internal static RendererAlphaAnalysis Analyze(
    Renderer renderer, BaseMaterialSemanticsProvider semanticsProvider);
```

**Approved in architectural review.** The justification on record:

1. **It is the repository's own established pattern, not a new one.**
   `AlphaSemanticsResolver` already takes `AlphaFieldProvider`, a delegate with exactly one
   production implementation, for exactly this reason.
2. **It is shader-agnostic.** Its signature mentions no shader, no frontend, no property
   name, and no registration.
3. **The default overload keeps production honest.** `Analyze(renderer)` does the real
   dispatch. Nothing in production reaches the second overload.

Every test using the second overload states in its own summary that frontend attestation is
substituted. **No test claims a vendor frontend was exercised.**

### The exact stand-in fixture state, read from repository source

Determined by reading `PoiyomiMaterialSemantics.InterpretAlpha`, `TryInterpretMainSample`,
`TryGetSupportedUvMapping`, `TryGetMainTextureSampling`, `FirstFailedZeroGate`,
`TryReadBinary`, `PoiyomiSemanticTest.shader`, and the existing `PoiyomiAlphaTests`. For
`Alpha` to resolve to `ScalarSemanticValue.Texture(sample, TextureChannel.Alpha)` — the
form that produces `AlphaResolution.Classified` and therefore a real per-triangle proof —
the material must satisfy all of:

| Requirement | Stand-in shader default | Action needed |
|---|---|---|
| `_AlphaToCoverage`, `_AlphaSharpenedA2C`, `_AlphaDithering`, `_EnableDissolve`, `_EnableUDIMDiscardOptions` exactly `0` | all `0` | none |
| `_AlphaForceOpaque` binary and `0` | **`1`** | **`SetFloat("_AlphaForceOpaque", 0f)`** |
| `_MainAlphaMaskMode` exactly `0` | **`2`** | **`SetFloat("_MainAlphaMaskMode", 0f)`** |
| the other 22 `AlphaFeatureGates` exactly `0` | all `0` | none |
| `_MainIgnoreTexAlpha` binary and `0` | `0` | none |
| `_Color.a` finite and **exactly `1`** (otherwise the value becomes `TextureTimesConstant` with a sub-unit multiplier, which the resolver answers `Uniform(MustRemainTransparent)` without reading one texel) | `(1,1,1,1)` | set explicitly for clarity |
| `_MainTex` assigned to a texture with a resolvable asset identity | `"white"` builtin, unassigned | **`SetTexture("_MainTex", importedTexture)`** |
| `_MainTexUV` an exact integer in `[0,3]`; `0` for the resolver's supported mapping | `0` | none |
| `_MainTexPan` exactly `(0,0,0,0)` | `(0,0,0,0)` | none |
| `_MainPixelMode`, `_MainTexStochastic` exactly `0` | both `0` | none |
| texture scale `(1,1)` and offset `(0,0)` (the resolver's `IsSupportedMapping`) | Unity defaults | set explicitly for clarity |
| `UnityTextureEvidence.TryGetSampling` supported: Point/Bilinear filter, equal Clamp/Repeat wrap, `mipmapCount == 1`, `mipMapBias == 0`, `anisoLevel <= 1` | — | the texture import already sets Point/Clamp with mipmaps off |

The resulting `TextureSample.Sampling` reflects the texture's *actual* import, so the
integration fixture's Point/Clamp texture produces `(Point, Clamp)`, matching the recipe
`AlphaEvidenceClassifierIntegrationTests` already proves.

Because this state is now written down exactly, the integration test is a **composition
test that may pass on its first run**, and that will be recorded truthfully. Deliberately
underconfiguring the fixture to manufacture a RED step is not done.

## Questions 6, 20, 21 — texture collection and provider lifetime

**One `UnityAlphaFieldEvidence` per `Analyze` call**, constructed once, before any submesh
is resolved, from the textures referenced by the renderer's own materials.

Gathering, without shader knowledge and without scanning anything:

```csharp
foreach (var material in renderer.sharedMaterials)        // may contain nulls
    foreach (var propertyName in material.GetTexturePropertyNames())
        candidate = material.GetTexture(propertyName);      // may be null
```

- `Material.GetTexturePropertyNames()` enumerates the *shader's own declared* texture
  properties. It is shader-agnostic: no AMUSE code names a property.
- The result is a **superset** of what the alpha semantics will ask for. That is correct
  and cheap: `UnityAlphaFieldEvidence` only stores identity → `Texture2D` at construction
  and reads pixels lazily in `TryGetAlphaField`. An unused candidate costs one dictionary
  entry. A texture the semantics need but that was somehow not gathered simply refuses with
  `MissingTextureEvidence` — fail-closed either way.
- Nulls, non-`Texture2D` values, and duplicates are already handled by the existing
  constructor ("skipped rather than rejected"; "the first wins and the duplicate is not an
  error"), so no filtering is added here.
- Identity comes from `UnityTextureEvidence.TryGetSourceId` inside that constructor, so the
  identity rule can never disagree with the one the frontends used.

Explicitly not done: parsing `TextureSourceId`, reverse-scanning `AssetDatabase` by GUID,
building a project-wide or global texture registry, scanning the project, or adding a
persistent cache. No new texture-evidence abstraction is introduced and
`UnityAlphaFieldEvidence` is not modified.

Per-renderer rather than per-material because it deduplicates textures shared between slots
and keeps object count at one per analysis. It is discarded when `Analyze` returns; nothing
outlives the call.

## Questions 7, 22, 23, 24 — composition, refusal granularity, and what survives

### Flow, per renderer

1. Reject a null or destroyed renderer as a caller defect.
2. Renderer-scoped refusals in the fixed precedence order of Question 2:
   unsupported type → property block present → missing mesh → slot-count mismatch →
   non-triangle topology → malformed mesh data. Any of these returns a refusal with no
   plan.
3. Build the texture evidence provider from the renderer's materials.
4. For each **distinct** material (memoized), obtain `MaterialSemantics`, then
   `AlphaSemanticsResolver.Resolve(semantics.Alpha, evidence.TryGetAlphaField)` → one
   `AlphaResolution` per material.
5. For each submesh *i*:
   - if slot *i*'s resolution refused → every triangle gets `Unknown`;
   - otherwise, for each triangle:
     - non-finite position → `Unknown`;
     - no UV0 array, or a non-finite UV → `resolution.Classify(MissingUv0(p0, p1, p2))`;
     - otherwise → `resolution.Classify(WithUv0(p0, p1, p2, uv0, uv1, uv2))`.
   - Build `SubmeshSeparationInput(i, indices, outcomes)`.
6. `MeshSeparationPlanner.Create(new MeshSeparationInput(mesh.vertexCount, submeshes))`.
7. Return the plan plus one per-submesh record.

`AlphaResolution.Classify` is never called on a refused resolution — it throws by design,
and the `Unknown` substitution happens before it can.

### Granularity

The smallest unit poisoned by uncertainty is:

- the **triangle**, for non-finite positions, degeneracy, oversized support, and for
  UV-dependent equations when UV0 is unavailable;
- the **submesh/slot**, for every `AlphaResolutionFailure`;
- the **renderer**, only for the six conditions in Question 2's precedence list.

So the scenarios the milestone asks about resolve as:

| Scenario | Outcome |
|---|---|
| Material A supported, material B unsupported | A's submeshes analyzed normally, including `ProvenOpaque` and `Split`. B's submeshes preserved with `SemanticsUnknown`. One plan covers both. |
| Mesh lacks UV0 entirely | Constant-alpha materials still prove `ProvenOpaque`; texture-sampled materials yield `Unknown`. Per-material resolutions still succeed and are still reported. |
| One triangle has a non-finite UV | Only that triangle loses UV0. A constant-alpha proof is unaffected; a sampled proof yields `Unknown` for it alone. |
| One alpha texture non-readable, another complete | The non-readable slot refuses with `MissingTextureEvidence`; the other yields real opaque candidates; `HasAnyOpaqueCandidates` is `true`. |

`Unknown` can never become `ProvenOpaque` through aggregation: the only writer of
`ProvenOpaque` into an outcome array is `AlphaResolution.Classify` on a *resolved*
resolution, and the planner's aggregation is a filter on `== ProvenOpaque`, never a merge,
vote, or majority. `ReplacingProvenOpaqueCannotIncreaseOpaqueCount` already pins the
planner half of that.

### The two places granularity is coarser than ideal

1. **A non-triangle topology in any submesh refuses the whole renderer**, even though the
   sibling submeshes are individually analyzable. Forced by the planner's positional
   `SourceSubmeshIndex`: omitting the submesh renumbers its successors and mis-attributes
   downstream mutation, and substituting an empty index list reports real geometry as
   absent. Approved for v1.
2. **A property block refuses the whole renderer**, even if it overrides nothing
   alpha-relevant. Deliberate: narrowing it requires reading and interpreting the block,
   which is the deferred effective-state work.

## Questions 8, 25, 26 — the renderer-level result

Existing types cannot express three things this milestone produces:

1. **Why a submesh was preserved at the material/resolver level.** `MeshSeparationPlan`
   records *that* triangles are in `TransparentTriangleOrdinals`, never *why*, and cannot
   distinguish "proven non-opaque" from "unsupported shader" from "unreadable texture".
2. **A renderer that produced no plan at all.** `MeshSeparationPlan` has no empty or
   refused form; `null` alone carries no reason.
3. **Whether a slot had a material.** A null slot and an unsupported shader both reduce to
   `SemanticsUnknown`; the Unity fact is not recoverable from the resolver's vocabulary.

Proposed, minimal:

```csharp
internal enum RendererAnalysisRefusal
{
    None,
    UnsupportedRendererType,
    MaterialPropertyOverridesPresent,
    MissingMesh,
    UnprovenMaterialSlotMapping,
    UnsupportedTopology,
    MalformedMeshData,
}

internal sealed class SubmeshAlphaAnalysis
{
    internal int SubmeshIndex { get; }                 // == plan positional index
    internal int MaterialSlotIndex { get; }            // == SourceMaterialBindingIndex
    internal bool HasMaterial { get; }
    internal AlphaResolutionFailure Failure { get; }   // None when resolved
}

internal sealed class RendererAlphaAnalysis
{
    internal RendererAnalysisRefusal Refusal { get; }              // None ⇔ Plan != null
    internal MeshSeparationPlan Plan { get; }                      // null when refused
    internal IReadOnlyList<SubmeshAlphaAnalysis> Submeshes { get; } // empty when refused
}
```

Deliberately absent: any reference to the live `Renderer`, `Mesh`, or `Material` (the
caller passed them in and still holds them; storing live Unity objects in an immutable
analysis result invites use-after-destroy); any per-triangle diagnostic; any severity,
message, or free-form string; any nesting beyond one level. `RendererAnalysisRefusal` has
exactly six refusal members plus `None`, matching Question 2's precedence list one for one.

The result is immutable by the repository's existing convention: readonly properties, the
submesh list copied and exposed through `Array.AsReadOnly`, invariant checked in the
constructor (`Refusal == None` exactly when `Plan != null`), matching `AlphaResolution`'s
own "resolved exactly when it has no failure" guard.

## Questions 9 & 27 — diagnostics and provenance, and the limits of the claim

**Reuse `AlphaResolutionFailure` verbatim; add no second vocabulary.**

### What the result does explain

`SubmeshAlphaAnalysis.Failure` explains **material- and resolver-level** refusal, and only
that:

| Wanted distinction | Expressed as |
|---|---|
| `UnsupportedShader` | `SemanticsUnknown` (+ `HasMaterial` when the slot was empty) |
| `UnknownAlphaSemantics` | `SemanticsUnknown` |
| `MissingTextureEvidence` | `MissingTextureEvidence` |
| `UnsupportedTopology` | `RendererAnalysisRefusal.UnsupportedTopology` |
| `MalformedRendererMapping` | `RendererAnalysisRefusal.UnprovenMaterialSlotMapping` |
| Property overrides present | `RendererAnalysisRefusal.MaterialPropertyOverridesPresent` |

### What the result does **not** explain

**The result does not carry a reason for every preserved triangle.** A triangle can be
`Unknown` on a submesh whose `Failure` is `None`, for any of:

- UV0 unavailable while the alpha equation needs it (missing array or non-finite UV);
- a non-finite position;
- exact degeneracy;
- the classifier's `MaxSupportRegions` workload refusal;
- any other classifier-local uncertainty.

None of these is recorded anywhere in `SubmeshAlphaAnalysis`. **This limitation is accepted
for v1.** Any earlier wording claiming the result always explains why geometry was
preserved is withdrawn.

What *is* recoverable: `MeshSeparationPlan.Source` retains the original
`SubmeshSeparationInput.Outcomes`, so a consumer can still distinguish `Unknown` from
`MustRemainTransparent` per triangle. It cannot recover *which* of the five causes produced
a given `Unknown`. No new diagnostic hierarchy is added to close that gap.

### Two further accepted losses

- **Unsupported shader vs. representable shader with an unprovable alpha equation** both
  read `SemanticsUnknown`. Separating them means propagating frontend diagnostics — two
  closed enums with no common type. Unifying them is a *generalized diagnostics framework*,
  an explicit stop condition, with no consumer until there is a UI. **Deferred.**
- **Non-readable vs. compressed vs. mipmapped texture** all read `MissingTextureEvidence`.
  The previous milestone already ruled that splitting it is a change to
  `AlphaSemanticsResolver`'s enum belonging to the milestone that has a consumer.
  **Deferred.**

The renderer analysis writes nothing to the Unity Console. Diagnostics are data.

## Questions 10 & 28 — non-readable textures: measure, do not fix

`UnityAlphaFieldEvidence` is **not modified**. No importer is touched, no `isReadable` is
toggled, no source file is read, no GPU readback, no texture copy, no temporary render
target, no second evidence provider, no widened format list.

The refusal path is already established at the resolver level by
`AlphaEvidenceClassifierIntegrationTests.UnsupportedTexture_RefusesWithMissingTextureEvidence`.
What is new is the **renderer-level blast radius**.

### Measured result (2026-08-20, Unity 2022.3.22f1, this project)

`RendererAlphaAnalysisIntegrationTests.ANonReadableAlphaTextureRefusesOnlyItsOwnSubmesh`
built one renderer with two slots over the standard asymmetric fixture mesh: slot 0's
material sampling a **non-readable** 4x4 RGBA32 texture, slot 1's sampling an otherwise
identical **readable** one. Both materials resolved through the real Poiyomi interpreter and
the real `AlphaSemanticsResolver`. Observed, and now asserted:

| Question | Measured answer |
|---|---|
| Where does the refusal emerge? | Inside `AlphaSemanticsResolver.Resolve`, when it calls `UnityAlphaFieldEvidence.TryGetAlphaField`, which returns `false` at `if (!texture.isReadable) return false;`. Material-scoped, before any triangle is examined. |
| What shape does it take? | `AlphaResolution.IsResolved == false`, `Failure == MissingTextureEvidence`, surfaced as `result.Submeshes[0].Failure`. |
| Is the whole renderer refused? | **No.** `result.Refusal == None`; a plan is still produced. |
| Is only that submesh affected? | **Yes.** Submesh 0's disposition is `Unchanged` and its `OpaqueTriangleOrdinals` is empty. |
| Do independent parts survive? | **Yes.** Submesh 1 reports `Failure == None` and yields `OpaqueTriangleOrdinals == [0, 2]`. |
| Does a useful partial plan still exist? | **Yes.** `Plan != null`, `HasAnyOpaqueCandidates == true`, `OpaqueTriangleCount == 2`. |

**The mechanism is confirmed: a non-readable texture costs exactly the submeshes bound to
the materials that sample it, and nothing more.** `UnityAlphaFieldEvidence` was not
modified, no importer state was toggled to evade the refusal, and the fixture creates the
refusal deliberately.

**This design does not choose the next branch.** What the measurement establishes is the
*mechanism* only. It says nothing about how often real avatar content hits it.

## Question 29 — mutation-safety argument

| Risk | Mitigation |
|---|---|
| `MeshFilter.mesh` instantiates a copy on read | **Never accessed.** `MeshFilter.sharedMesh` only. |
| `Renderer.materials` instantiates copies on read | **Never accessed.** `Renderer.sharedMaterials` only. |
| `SkinnedMeshRenderer.BakeMesh` writes a mesh | **Never called.** |
| `Renderer.GetPropertyBlock(block)` writes into the caller's block and needs one allocated | **Never called.** Only the `HasPropertyBlock()` boolean is read. |
| Importer writes | No `TextureImporter`, `ModelImporter`, or `AssetImporter` is opened in production code. |
| Asset writes | No `AssetDatabase.CreateAsset`, `SaveAssets`, `ImportAsset`, or `Refresh`. |
| Scene writes | No `GameObject`, `Component`, or `Transform` is created, destroyed, enabled, or modified. |
| Property writes | Every Unity member accessed is a getter. |
| Hidden allocation-with-side-effect | `mesh.vertices`, `mesh.uv`, `mesh.GetIndices`, `Material.GetTexturePropertyNames`, and `Material.GetTexture` all return copies or references without mutating their source. |

Enforced by test, not only by review: the integration fixture snapshots the source
`Renderer`, `Mesh`, `Material`, and `Texture2D` before analysis and asserts structural
equality afterwards, including the imported texture's `AssetDatabase` dependency hash.

Tests may create temporary `GameObject`s, `Mesh`es, and asset folders; every fixture tears
down in `[TearDown]`, following the existing `PoiyomiFixtureTestBase` and
`AlphaEvidenceClassifierIntegrationTests` pattern.

## Performance observations

Measured from source, not optimized speculatively:

| Cost | Disposition |
|---|---|
| `mesh.vertices` and `mesh.uv` each allocate a full array copy per call | Called **once each per renderer**, held in locals. Never per submesh, never per triangle. |
| `mesh.GetIndices(i)` allocates per submesh | Called once per submesh. The allocation-free `GetIndices(List<int>, int)` overload exists and is **not** used in v1 — no measurement justifies it yet. Recorded. |
| Shader attestation hashes the entire shader source | Real, repeated, and avoidable: a `Dictionary<Material, MaterialSemantics>` **local to one `Analyze` call** memoizes it. Repeated material references across slots are common on avatars. |
| `AlphaSemanticsResolver.Resolve` per material | Memoized alongside the semantics — one `AlphaResolution` per distinct material, reused across every submesh bound to it. |
| `UnityAlphaFieldEvidence.TryGetAlphaField` calls `GetPixels32` on every invocation | With the per-material memo, one read per distinct material that samples a texture. Two *different* materials sharing one texture still read it twice. **Observed, not fixed** — fixing it means caching inside the provider, and the provider is out of scope. |
| Two renderers sharing one mesh re-extract it | **Observed, not fixed.** No cross-renderer cache. |
| The property-block guard | One boolean read, before any allocation. |

No global cache, no static cache, no persistent cache, no cache surviving `Analyze`. Every
memo is a local dictionary discarded on return.

## Test strategy

All EditMode, all in `Alrauna.Amuse.Tests.Editor`, all deterministic, all self-cleaning.
Meshes are built procedurally (`new Mesh()`, always readable) except where import behaviour
is the subject.

### `Tests/Editor/Host/MeshReadabilityCharacterizationTests.cs` — runs first

Direct Unity behaviour only, no AMUSE production code: import a generated one-triangle
`.obj`, set `ModelImporter.isReadable = false`, and exercise `mesh.vertices`, `mesh.uv`,
and `mesh.GetIndices(0)` individually, asserting each does not throw and returns complete
data. A throw fails the test naming the exact operation and exception type and **stops
implementation for review**. The test does not re-read a member it is characterizing as
part of a later assertion.

### `Tests/Editor/Semantics/UnityMaterialSemanticsTests.cs`

Truthful about its limits: a non-vendor stand-in material, a null material, a destroyed
material, and a shaderless material each yield all-`Unknown` semantics without throwing.
The suite's summary states in text that real vendor dispatch cannot be exercised in the
public project.

### `Tests/Editor/Host/UnityRendererAlphaAnalysisTests.cs` — contract and refusal matrix

1. Supported `SkinnedMeshRenderer`, one submesh, constant alpha 1 → every triangle
   `ProvenOpaque`, disposition `WhollyOpaqueCandidate`.
2. Same for `MeshRenderer` + `MeshFilter`.
3. Multiple submeshes with distinct material slots → per-slot dispositions;
   `SourceMaterialBindingIndex` equals the slot index for every submesh.
4. Supported submesh beside an unsupported one → the supported submesh still yields opaque
   candidates; the unsupported one reports `SemanticsUnknown` and `Unchanged`.
5. Repeated material across two submeshes → both analyzed, identical outcomes.
6. `null` material slot → `HasMaterial == false`, `SemanticsUnknown`, `Unchanged`.
7. `sharedMaterials.Length != subMeshCount`, both directions →
   `UnprovenMaterialSlotMapping`, `Plan == null`.
8. **Property block attached → `MaterialPropertyOverridesPresent`, `Plan == null`.** A real
   block with one simple override is set so `HasPropertyBlock()` is genuinely true; its
   contents are never inspected by production or asserted by the test.
9. **Property block attached to a single material index** → the same refusal, verifying
   `HasPropertyBlock()` covers per-index blocks. If it does not, this is a stop condition.
10. Mesh with no UV0 + constant alpha 1 → still `ProvenOpaque`
    (`MissingUv0DoesNotBlockUvIndependentConstantProof`).
11. Non-finite UV on one triangle + constant alpha 1 → still `ProvenOpaque` for every
    triangle, proving the same dependency rule per triangle.
12. Non-triangle topology → `UnsupportedTopology`, `Plan == null`.
13. `SkinnedMeshRenderer` with `sharedMesh == null`, and `MeshRenderer` with no
    `MeshFilter` → `MissingMesh`.
14. Unsupported renderer class (`LineRenderer`) → `UnsupportedRendererType`.
15. Destroyed material in a slot → treated as a missing material, not an exception.
16. Empty submesh among populated ones → represented, `Unchanged`, no binding shift.
17. Determinism: `Analyze` twice over one renderer → structurally equal results.

### `Tests/Editor/Host/RendererAlphaAnalysisIntegrationTests.cs` — the vertical slice

The highest-value fixture (below), the two UV-dependent cases that need a real sampled
equation (missing UV0 → `Unknown`; non-finite UV → `Unknown` for that triangle only), the
non-readable-texture characterization, and the source-immutability assertions.

### Pass-immediately tests, declared honestly

The mesh-readability characterization, the topology and unsupported-type refusals, and the
integration composition test are expected to pass on first run. They are recorded as
characterization or composition checks, not dressed as RED steps. Genuine RED/GREEN is
reserved for genuinely new production behaviour.

## Highest-value integration fixture

Asymmetric on every axis that could silently compensate for a wiring error:

```
Texture   4x4 RGBA32, uncompressed, no mips, Point/Clamp,
          exactly one non-opaque texel at (0,0)      ← reuses the proven recipe
Mesh      2 submeshes, different triangle counts
Renderer  SkinnedMeshRenderer with 2 material slots, no property block

slot 0 → stand-in material, semantics NOT interpreted  (all-Unknown)
         submesh 0 = 2 triangles whose UVs lie in the fully opaque region
slot 1 → stand-in material configured per the table in Questions 18 & 19,
         semantics via the real InterpretVerifiedMaterial seam
         submesh 1 = 3 triangles:
             triangle 0 → UV region [0.5,0.9]²   → ProvenOpaque
             triangle 1 → inside texel (0,0)     → MustRemainTransparent
             triangle 2 → UV region [0.5,0.9]²   → ProvenOpaque
```

Expected: `Refusal == None`; submesh 0 `Unchanged` with `SemanticsUnknown`; submesh 1
`Split` with `OpaqueTriangleOrdinals == [0, 2]` and `TransparentTriangleOrdinals == [1]`;
`OpaqueTriangleCount == 2`; `HasAnyOpaqueCandidates == true`; `RequiresAnySplit == true`.

Why it cannot pass while wired wrongly:

- **Slot/submesh mapping** — the *opaque-looking* geometry is on the *unsupported* slot. A
  swapped mapping inverts both dispositions and fails.
- **Submesh indexing** — the two submeshes have different triangle counts, so any
  off-by-one in ordinals or index ranges fails.
- **Row orientation** — the single non-opaque texel is at a corner; a vertical flip moves
  it away from triangle 1's UVs and turns a `MustRemainTransparent` into `ProvenOpaque`,
  which the test asserts against explicitly.
- **UV mapping** — triangle 1 sits wholly inside one texel; any scale or offset error
  escapes it.
- **Uniform-texture short-circuit** — the texture is deliberately mixed, so
  `AlphaTextureData.IsFullyOpaque` cannot bypass geometry.

Immediately after, and on the same fixture: every source object is asserted unchanged.

## Architecture guard

**No new guard is added.** An equivalent, already non-vacuous guard exists on `main` in
`Tests/Editor/Host/UnityAlphaFieldEvidenceTests.cs`:

- `AnalysisNamespace_HasNoDependencyOnTheUnityEditorNamespace` scans
  `Editor/Analysis/**/*.cs` for `\bUnityEditor\b` and asserts zero hits, plus
  `fileCount > 0` so it cannot pass vacuously;
- `UnityEditorDetector_ReportsADirectoryThatDoesDependOnIt` is a permanent positive control
  pointing the same detector at `Editor/Semantics`, which genuinely uses `UnityEditor`.

It derives the package path from `Application.dataPath`'s parent, so it is already
platform-agnostic. Duplicating it was the proposal in revision 1; that task is **dropped**.

The remaining at-risk boundaries are held by the design and verified in review:
`MeshSeparationPlanner` gains no change at all, `TriangleAlphaClassifier` gains none,
`MaterialSemantics` gains none, `TextureSourceId` is never parsed, and no shader property
name enters `Host` or `Analysis`.

## Non-goals

Effective-state analysis of any kind — `Animator`, `AnimationClip`, material swaps,
**`MaterialPropertyBlock` contents**, visibility, VRCFury, SPS, NDMF-generated state,
runtime mutation. Only the current/base material state visible in the renderer snapshot is
analyzed, and the result says nothing about later states. A renderer that carries a
property block is refused outright rather than analyzed under an assumption. Future state
analysis should *wrap* this renderer-level analysis, not be folded into it.

Also out: mesh or material generation; any source mutation; an NDMF pass or build hook; an
avatar component; user-facing UI or preview; profitability; non-readable texture support;
mipmapped, compressed, or float texture support; UV channels other than 0; UV transforms;
coverage/clip semantics; multi-renderer or avatar-wide aggregation; cross-renderer caching;
CI changes; dependency changes.

## Stop conditions

**Final outcome: no stop condition fired at any point, in design or implementation.** The
two that were live during implementation both resolved favourably and are recorded above:
non-readable meshes read cleanly in the Editor (Question 4), and `HasPropertyBlock()` covers
index-scoped blocks (Question 3). Nothing required a material change to
`MaterialSemantics`, `AlphaSemanticsResolver`, `TriangleAlphaClassifier`,
`MeshSeparationPlanner`, or `UnityAlphaFieldEvidence`; no shader adapter framework; no
vendor packages; no weakened attestation; no testbed oracle; no `BakeMesh`; no generalized
diagnostics; no NDMF execution.

The one seam previously escalated — `BaseMaterialSemanticsProvider` — is **approved**, as
are the explicit two-branch dispatch, the whole-renderer refusal for non-triangle topology,
the strict slot-count equality, and the ruling that no planner redesign is authorized.

Implementation must stop and escalate if any of these becomes necessary mid-flight:

1. any mesh read throws on a non-readable mesh in the Editor (record the exact operation and
   exception type; **do not choose a catch policy**, and never write `catch (Exception)`);
2. `renderer.HasPropertyBlock()` does not report a block attached to a single material
   index, leaving the guard with a hole;
3. the renderer/submesh model cannot be expressed without changing
   `MeshSeparationPlanner`'s input, output, or index semantics;
4. distinguishing the refusals the result needs requires changing `AlphaResolutionFailure`;
5. the two-branch dispatch cannot stay a two-branch dispatch;
6. mesh extraction requires touching an importer or any writing API;
7. non-readable texture or mesh support looks necessary to make the milestone useful;
8. any per-triangle or per-submesh diagnostic beyond the two enums above is needed;
9. the integration fixture cannot be made to pass without changing a production file
   outside `Editor/Host/`.

## Risks

1. **The property-block guard may refuse a great deal of real content.** Property blocks are
   common in avatar tooling. The refusal is correct, but the milestone's real-world reach
   may be narrower than the fixtures suggest. Measuring that frequency needs real avatars
   and is explicitly out of scope here.
2. ~~**`HasPropertyBlock()` per-index coverage is unverified.**~~ **Resolved 2026-08-20.**
   Measured: `SetPropertyBlock(block, 0)` sets `HasPropertyBlock()` to `true`, so the guard
   sees index-scoped blocks and the stop condition did not fire. The test remains in the
   suite so a future Unity change surfaces as a named failure.
3. ~~**Mesh readability in the Editor is expected to succeed but is unmeasured.**~~
   **Resolved 2026-08-20.** Measured in this project on Unity 2022.3.22f1: `vertices`,
   `uv`, and `GetIndices` all return complete data on a mesh with `isReadable == false`.
   Production therefore carries no readability pre-check and no exception handling. The
   characterization test remains in the suite so a future Unity or platform change that
   breaks this surfaces as a named failure rather than as silently malformed analysis.
4. **The strict slot-count rule may refuse more than expected** on real avatars. Cheap to
   measure later; expensive to get wrong now.
5. **`SemanticsUnknown` will dominate** public results, because the public project can
   attest no vendor shader. Reading that as "AMUSE proves little" would be a
   misinterpretation of a project-configuration limit, not a capability limit.
6. **Non-triangle topology refuses whole renderers.** Rare, conservative, approved.
7. **Triangle-level `Unknown` carries no reason.** Accepted for v1; it will be felt first
   by whatever builds a user-facing explanation.

## Deferred work

`MaterialPropertyBlock` semantics and the rest of effective-state analysis; frontend
diagnostic propagation; splitting `MissingTextureEvidence` into actionable causes;
per-triangle `Unknown` reasons; material/submesh count-mismatch support; non-triangle
topology support; per-submesh survival past a topology refusal; non-readable texture and
mesh evidence; UV transforms and higher UV channels; cross-renderer and cross-mesh caching;
allocation-free index reads; avatar-wide aggregation; vertex-sharing analysis for actual
geometry splitting; the NDMF pass, avatar component, and mutation executor.

## Criteria for choosing the next branch

This milestone produces the evidence; it does not make the choice.

Choose **`feat/non-readable-alpha-evidence`** when, on real avatar content inspected
read-only in the private testbed, `MissingTextureEvidence` is the *dominant* refusal **and**
the materials producing it already resolve their alpha semantics — that is, texture
readability is the binding constraint and removing it would convert refusals into proofs.

Choose **build/NDMF integration instead** when refusals are dominated by
`SemanticsUnknown`, because a non-readable evidence route would then unlock nothing: the
alpha equation is unproven before any texture is consulted.

Consider **effective-state / property-block analysis first** if
`MaterialPropertyOverridesPresent` turns out to refuse a large share of real renderers,
since no texture or shader work would reach that content at all.

The public characterization in this milestone established only the *mechanism*, and it did
establish it: the texture refusal is submesh-scoped and partial plans survive it, measured
and asserted rather than argued. The *frequency* judgement — which of
`MissingTextureEvidence`, `SemanticsUnknown`, and `MaterialPropertyOverridesPresent`
actually dominates on real content — requires real avatars and must be gathered in a
separate, explicitly scoped, read-only task. Nothing in the public suite can stand in for
that, because the public project can attest no vendor shader and carries no real avatar.

## Question 31 — what is deferred to the NDMF / product integration milestone

This branch deliberately stops at immutable analysis. The next product step remains:

```
AMUSE avatar component → NDMF build host → this renderer-level analysis
    → immutable plan → generated non-destructive mutation
```

Deferred to that milestone, none of it started here: the NDMF pass and build-phase hook;
NDMF context plumbing and generated-object ownership; the avatar root component and any UI
or preview; walking an avatar to select renderers; generated meshes, materials, and
renderers; the vertex-sharing and index-rewriting work that actually executing a `Split`
requires; profitability policy; and the decision about whether a plan is worth applying.
This milestone's contribution to that step is the reusable reasoning path and the proof
that it composes.

## Design question index

| # | Question | Section |
|---|---|---|
| 1 | `MeshSeparationPlanner` input today | Question 1 |
| 2 | Its immutable output | Question 1 |
| 3 | Natural unit of renderer analysis | Question 2 |
| 4 | Renderer types supported | Question 3 |
| 5 | Renderer → Mesh | Question 3 |
| 6 | Mesh readability required? | Question 4, "Mesh readability and exception policy" |
| 7 | Submesh ↔ material slot | Question 2 table |
| 8 | Valid / unsupported / malformed states | Question 15 |
| 9 | Deterministic triangle enumeration | Question 4 |
| 10 | Indices → `TriangleAlphaInput` | Question 4 |
| 11 | Coordinate space | Questions 11 & 12 |
| 12 | Why it is sufficient | Questions 11 & 12 |
| 13 | UV0 extraction | Questions 13 & 14 |
| 14 | Missing / incomplete / non-finite UV0 | Questions 13 & 14 |
| 15 | Non-triangle topology | Question 15 |
| 16 | Material → `MaterialSemantics` | Questions 5, 16, 17 |
| 17 | Frontend selection without a registry | Questions 5, 16, 17 |
| 18 | Can the public project exercise vendor dispatch? | Questions 18 & 19 |
| 19 | Which link is substituted | Questions 18 & 19 |
| 20 | Supplying textures to the evidence provider | Questions 6, 20, 21 |
| 21 | Provider lifetime | Questions 6, 20, 21 |
| 22 | Classifier outcomes → planner input | Questions 7, 22, 23, 24 |
| 23 | Refusal granularity | Questions 7, 22, 23, 24 |
| 24 | Do supported submeshes survive neighbours? | Questions 7, 22, 23, 24 |
| 25 | Is a new result type necessary? | Questions 8, 25, 26 |
| 26 | What existing types cannot carry | Questions 8, 25, 26 |
| 27 | Failure provenance, and its limits | Questions 9 & 27 |
| 28 | Non-readable texture refusal shape | Questions 10 & 28 |
| 29 | Does analysis mutate anything? | Question 29 |
| 30 | Evidence deciding the next branch | Criteria for choosing the next branch |
| 31 | Deferred to NDMF / product integration | Question 31 |
