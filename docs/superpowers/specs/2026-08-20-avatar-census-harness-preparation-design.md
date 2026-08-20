# Avatar Census Harness — Preparation Design

Branch: `research/avatar-census-harness-preparation`
Base commit: `711a7a8` (`origin/main`)
Date: 2026-08-20
Status: **Revision 3 — final. Architecture reviewed and approved. No code produced, no package created, no Unity change, nothing staged or committed.**

## 0. Revision history and invalidated assumptions

Revision 3 applies architectural review. The direction of revision 2 was approved without
structural change; revision 3 records the governance rules for the research package (§3.2.1),
adds contributor and agent placement guidance (§3.4), tightens the privacy rule for new
report categories (§6.5), and closes the branch (§12). No architectural decision was
reversed.

Revision 1 was written under an incorrect premise: that the census harness should live
outside this repository as a disposable private tool. The corrected vision is that **AMUSE
is the source repository for all publicly releasable AMUSE source code**, research tooling
included, and that the real boundary is **public source code versus private user data** —
not production code versus research code.

| Revision 1 assumption | Status | Replacement |
|---|---|---|
| Harness lives only in the private project and is never committed | **Invalidated** | Harness source is first-party public code in this repository (§2, §3) |
| Harness is disposable and dies with its host project | **Invalidated** | Only the *execution environment and its private data* are disposable. Nothing important exists only in the Lab |
| The spec must be a rebuild recipe because the code will be lost | **Invalidated** | The code is version-controlled. The spec returns to being a design document |
| Reflection is required, with a surface-compatibility probe | **Invalidated** | Reflection was a consequence of externality. With the harness in-repo, a friend assembly is cleaner (§4) |
| No code is required in this repository | **Invalidated** | Code belongs here. §9 sequences it |
| Fixtures must be code-constructed because assets do not survive | **Superseded** | Still code-constructed, but for determinism and reviewability, which are better reasons |
| Anonymous IDs are run-local only | **Retained** | Still correct: the Lab is recreated, so no cross-run entity identity exists |
| Aggregate report is the sole permitted export | **Retained and strengthened** | Now produced by publicly reviewable, unit-tested code rather than operator discipline (§5) |
| Mutation-safety layering | **Retained** | Layer 2 becomes a CI check rather than a manual grep (§7) |
| Refused-renderer null-versus-zero counting rule | **Retained** | Unchanged; still the most likely miscount (§5.2) |

What survives unchanged from revision 1: the finding that every required measurement is
already readable from existing result objects with no production change; the refused-renderer
counting rule; the material-ordinal anonymization scope; and the observation that
`ProvenOpaque` is unreachable through the production entry point in a project with no vendor
shader installed.

## 1. Purpose

Establish the correct architecture for AMUSE research tooling, using the avatar alpha census
as the first concrete instance. This branch does not finish the census system, does not scan
avatars, and does not choose the next product feature.

The harness **observes** AMUSE behaviour. It must not distort AMUSE architecture to make
measurement easier. Where the census wants information AMUSE does not record, the correct
outcome is to *measure the size of the blind spot* and let AMUSE decide on its own merits
whether to record more — never to add a measurement hook to production analysis.

## 2. The boundary

```
AMUSE repository            public source: harness, schema, anonymizer,
   |                        aggregator, fixtures, validation, reports
   |  referenced as a local Unity package
   v
AMUSE-Census-Lab            private: avatars, vendor shader packages,
   |                        consent records, raw run output
   v
private research runs
```

**Belongs in this repository:** traversal and collection logic, the record schema,
anonymization, aggregation, reporting, deterministic fixture generation, calibration tests,
mutation-safety checks, and published aggregate reports.

**Belongs only in the Census Lab:** avatar files, private or purchased shader packages,
consent records, raw run output, and any identity mapping. None of these may enter this
repository, in any commit, ever.

The Lab is disposable. Nothing that matters may exist only there.

## 3. Repository organization

### 3.1 The Unity compilation constraint

Unity compiles C# under `Assets/` and under packages in `Packages/`. It does not compile a
top-level `Tools/` directory — which is why the existing `Tools/Bootstrap-NdmfStandalone.ps1`
is a PowerShell script and not C#. Census collection needs live `Renderer`, `Mesh`,
`Material`, and `TextureImporter` access, so it must run inside the Editor and therefore
must be compiled Unity code.

`Assets/` is also unavailable: assets cannot be referenced across Unity projects, so a
harness in `<repo-root>/Assets/` could never be consumed by the Lab without copying, which
would duplicate the source of truth.

That leaves exactly one viable home: **a second embedded package under `Packages/`**, which
the Lab references locally through the same mechanism it already uses for the main package.

### 3.2 Recommended structure

The smallest addition that fits:

```
Packages/com.alrauna.amuse/            (unchanged, distributable)
Packages/com.alrauna.amuse.research/   (new, never released)
    package.json
    Editor/
        Census/
        Alrauna.Amuse.Research.Editor.asmdef
    Tests/Editor/
        Census/
        Alrauna.Amuse.Research.Tests.Editor.asmdef
docs/research/                          (new, created when the first report exists)
```

Rationale for each choice:

- **One `research` package, not one package per concern.** Benchmarks, compatibility
  tooling, and future experimental analysis share every relevant property: first-party,
  not shipped, needs package internals. A single package means one friend-assembly grant
  and one asmdef rather than a package per idea. `Census/` is a folder inside it. This is
  the *less* structured option, not a premature framework.
- **No `Tools/Census/`.** `Tools/` is correctly reserved for non-Unity scripts and already
  holds one. Adding a C# subtree there would produce code Unity never compiles.
- **No `docs/design/`.** `docs/architecture/` and `docs/superpowers/specs/` already cover
  design. A third design location is unnecessary separation.
- **`docs/research/` only when a report exists.** Creating an empty directory for a future
  artifact is exactly the speculative scaffolding AGENTS.md forbids.

### 3.2.1 Research package governance

The name `com.alrauna.amuse.research` is deliberate and is **not** to be changed to `tools`
or `devtools`. It is a first-party research and development package, not a generic utility
bucket, and the distinction is load-bearing: a package named for what it *is* invites
contributions with a purpose, while a package named for what it *is not shipped as* invites
anything that has nowhere else to go.

It may eventually contain avatar census tooling, compatibility analysis, benchmarks,
validation utilities, and experimental AMUSE analysis tooling.

Governance rules, which apply to every future addition:

1. **First-party source code.** Not a vendor drop, not copied from a reference project
   without understanding and attribution.
2. **Never shipped as a VPM package.** §3.3 shows this is already enforced by release
   automation; it must stay that way.
3. **Never contains private user data.** No avatars, no consent records, no raw run output,
   no identity mappings. Those live in the Lab (§2, §9).
4. **Never a dumping ground.** A utility with no AMUSE development or research purpose does
   not belong here merely because it is inconvenient elsewhere.
5. **Each addition states its purpose.** A new subtree should be introducible in one
   sentence naming the AMUSE question it helps answer. If that sentence cannot be written,
   the addition is not ready.

Rule 4 is the one most likely to erode. It erodes gradually, by single reasonable-looking
exceptions, so it is stated as a rule rather than left to judgement.

### 3.3 Release safety is already enforced

`release.yml` builds `Packages/${{ vars.PACKAGE_NAME }}` and `build-listing.yml` passes
`--current-package-name ${{ vars.PACKAGE_NAME }}`. Both operate on one named package, so a
second package under `Packages/` is structurally excluded from release and from the VPM
listing with **no workflow change**. Embedded packages are auto-discovered by Unity, so
`Packages/manifest.json` needs no entry either — the research package adds zero dependency
churn.

This should be stated explicitly in the research package's `package.json` description, and
the package should carry no `vpmDependencies`, so its non-shipping status is legible to a
human reading it cold.

### 3.4 Where things belong — contributor and agent guidance

Four locations, one question each. Answer them in order and stop at the first yes.

| Location | Belongs there when | Examples |
|---|---|---|
| `Packages/com.alrauna.amuse/` | An AMUSE **user** needs it at runtime or build time for the optimizer to do its job | Analysis, semantics, planners, NDMF integration, host extraction |
| `Packages/com.alrauna.amuse.research/` | It is **reusable public tooling that helps understand, validate, benchmark, or improve AMUSE**, and no user needs it installed | Census collector, anonymizer, aggregator, benchmarks, compatibility probes |
| **AMUSE-Census-Lab** | It is **private data or a third-party package**, and publishing it would be unsafe, unlicensed, or a privacy breach | Avatars, vendor shader packages, consent records, raw measurements, identity mappings |
| `docs/` | It is a **decision, a report, or historical context** rather than executable behaviour | Design specs, architecture vision, published aggregate reports |

The two tests that resolve almost every real case:

- **"Would an AMUSE user be worse off without it?"** Yes → production package. No, but an
  AMUSE *developer* would → research package.
- **"Would publishing this harm someone or breach a licence?"** Yes → Lab, and only the Lab.
  This test outranks every other consideration. Research code that *processes* private data
  is still public code; the data is what stays private.

Guidance for the ambiguous cases:

- **Code that only ever runs against private avatars still belongs in the repository.** The
  boundary is public source versus private data, not production versus research. The Lab
  holds no source.
- **A test fixture belongs wherever the code it tests belongs**, and must be synthetic,
  redistributable, and deterministic. A fixture that cannot be synthesized publicly is a
  signal to re-scope the test, not to import private content.
- **Something genuinely spanning two locations is a design smell.** Split it: the pure part
  goes public, the private part becomes input data. §4.3's three-stage split is that
  principle applied once already.
- **When still unsure, ask before creating.** A misplaced file in the production package
  reaches users; a misplaced file in the Lab is lost when the Lab is recreated; a misplaced
  private asset in the repository is a permanent history problem.

The last point is not symmetric, and that asymmetry should drive the decision: two of the
three failure modes are recoverable, and the third is not.

## 4. Assembly and visibility architecture

### 4.1 Reflection is no longer the right answer

Revision 1 chose reflection because an external assembly cannot be a friend. That reason is
gone. Reflection now costs a surface-compatibility probe, name-keyed enum handling, and the
loss of compile-time safety — machinery whose entire purpose is to re-create, at run time
and imperfectly, what the compiler already does for free within one repository.

### 4.2 Recommendation: one friend-assembly grant

Add to `Packages/com.alrauna.amuse/Editor/AssemblyInfo.cs`:

```csharp
[assembly: InternalsVisibleTo("Alrauna.Amuse.Research.Editor")]
```

Architectural justification, since visibility changes require one:

- **It changes no public API.** The distributable package's public surface stays empty.
  No consumer of AMUSE observes any difference. This is strictly narrower than the
  alternative of promoting analysis types to `public`, which would commit AMUSE to
  supporting an analysis API years before the product is ready to.
- **It adds no production code and no escape hatch.** Nothing in the analysis path changes.
  There is no measurement hook, no test-only branch, no diagnostic expansion.
- **The precedent already exists and is the same shape.** `Alrauna.Amuse.Tests.Editor` is
  already a friend assembly for exactly this reason: first-party, same repository, versioned
  together, compiled together.
- **It makes drift a compile error.** Renaming an internal breaks the census in CI, in the
  same commit, loudly. Under reflection the same rename produces a run-time abort at best
  and a silent miscount at worst. For a tool whose entire value is trustworthy counting,
  compile-time coupling is a feature.

The accepted cost is that internals gain a second consumer, constraining refactoring. That
is already true of the test assembly, and it is the desirable direction: a rename that
breaks the census should break loudly here rather than silently in a private run.

### 4.3 Only the collector needs internals

This is the load-bearing structural point of revision 2. The harness decomposes into three
stages, and only the first touches Unity or AMUSE internals:

| Stage | Input | Output | Needs Unity? | Needs internals? |
|---|---|---|---|---|
| **Collect** | Live `Renderer` objects | Tier 1 raw records | Yes | Yes |
| **Anonymize** | Tier 1 records | Tier 2 records | No | No |
| **Aggregate** | Tier 2 records | Tier 3 report | No | No |

Anonymize and Aggregate are pure functions over plain records. They need no Unity object, no
avatar, and no friend access, so they are fully unit-testable in CI on synthetic input. This
mirrors AMUSE's own testing boundary — analysis separated from mutation — applied to the
harness itself, and it is what lets §6 prove non-leakage rather than promise it.

## 5. Data model

Three tiers, with a deliberate trust boundary between each.

### 5.1 Tier 1 — raw private record

Produced by Collect. Lives only in the Lab. **May contain real identifiers** — asset paths,
material names, shader names — because it is the debugging artifact, and a census anomaly
that cannot be traced back to a concrete material is not debuggable. It never leaves the
Lab and never enters a commit.

### 5.2 Tier 2 — anonymized intermediate

Produced by Anonymize, a pure function. Ordinal identity only:

- `Avatar-NN`
- `Renderer-NN-MMM` — avatar-scoped
- `Material-NN-MMM` — **avatar-scoped**, so cross-avatar asset sharing is not recorded,
  because shared-asset patterns identify purchased assets and creators
- `UnknownFamily-A`, `UnknownFamily-B` — for unattested shader families (§6.1)

Ordinals are run-local and carry no meaning across runs, because the Lab may be recreated
between them. Only aggregates compare longitudinally.

Structure, per renderer:

```json
{
  "id": "Renderer-01-004",
  "rendererType": "SkinnedMeshRenderer",
  "refusal": "None",
  "submeshCount": 6,
  "triangleCount": 12840,
  "submeshes": [
    {
      "index": 0,
      "materialSlotIndex": 0,
      "materialId": "Material-01-007",
      "shaderFamily": "Poiyomi",
      "hasMaterial": true,
      "alphaResolutionFailure": "None",
      "disposition": "Split",
      "triangleCount": 2140,
      "provenOpaque": 2100,
      "mustRemainTransparent": 40,
      "unknown": 0
    }
  ]
}
```

**The refused-renderer counting rule.** A refused `RendererAlphaAnalysis` carries no plan and
an empty submesh list. Its counts must then be read from the mesh, and recorded as **`null`,
never `0`**, when no mesh is reachable — `UnsupportedRendererType`, `MissingMesh`. Every
aggregate over these fields skips `null` and publishes its own denominator. A census that
silently averages refusals in as zero understates avatar complexity and overstates coverage.
This is the most likely miscount in the whole system and it gets a dedicated calibration case.

### 5.3 Tier 3 — public-safe aggregate report

Produced by Aggregate, a pure function. Distributions only. **No per-avatar and no
per-renderer rows** — an exact renderer or triangle count is a strong fingerprint for anyone
holding the avatar. Avatar-level variation appears only as bucketed distributions, e.g.
"avatars with at least one opaque candidate: 7 of 12".

Formats: JSON for tiers 1 and 2, Markdown for tier 3. No CSV, no dashboards, no telemetry,
no persistent store. JSON re-aggregates without re-running; Markdown reads.

## 6. What the harness measures

The design rule: **every measured field must map to a decision AMUSE could make.** Data that
is merely easy to collect is excluded by name, so that exclusion is a reviewable choice
rather than an oversight.

### 6.1 Tier 1 — directly drives prioritization

- **Renderer refusal distribution**, over all seven `RendererAnalysisRefusal` values. If
  `MaterialPropertyOverridesPresent` blocks a large fraction of renderers, property-block
  semantics is named as the next milestone by evidence rather than intuition.
- **`AlphaResolutionFailure` distribution at submesh level, triangle-weighted.** Separates
  "we do not understand this shader" (`SemanticsUnknown`) from "we understand it but cannot
  see the texture" (`MissingTextureEvidence`). Those imply completely different next steps.
- **Shader family coverage.** The single highest-value number: what fraction of materials
  are attested by Poiyomi, by lilToon, and by neither. Poiyomi and lilToon are public
  products and may be named. Unattested families are grouped by the anonymizer into
  `UnknownFamily-A`, `-B`, … so the aggregate can say "one unattested family accounts for
  22% of materials" — which names the next adapter's *size* — without disclosing a shader
  name that may be private or custom. This reuses the existing anonymizer; it is not a new
  mechanism. Adding this category — or any future identifying category — is subject to §6.5.
- **Triangle-weighted outcome split.** The headline: what fraction of real avatar geometry
  can AMUSE currently prove opaque.
- **Separation disposition distribution** — `Unchanged`, `WhollyOpaqueCandidate`, `Split`.
  Measures the *shape* of the opportunity, not just its size: a result that is almost all
  `Split` implies very different transformation cost than one that is mostly
  `WhollyOpaqueCandidate`.

### 6.2 The Unknown-attribution blind spot

`SubmeshAlphaAnalysis` deliberately records no reason for a triangle-level `Unknown`. On a
submesh whose failure is `None`, an `Unknown` triangle may come from unavailable UV0, a
non-finite position, degeneracy, or the classifier's own workload refusal — and AMUSE
records which nowhere.

The census will hit this and be unable to explain it. The correct response is **not** to add
attribution to production analysis so the census can measure it — that is exactly the
distortion this design forbids. Instead the harness measures **the size of the blind spot**:
the count and triangle-weight of `Unknown` outcomes on submeshes with `Failure == None`. If
that number is small, the gap does not matter. If it is large, that is honest evidence for
AMUSE to record reasons on its own merits, decided in its own design review.

### 6.3 Tier 2 — needed to interpret tier 1

Renderer type distribution; submesh count per renderer; distinct materials per avatar;
`HasMaterial` false rate. Each of these changes how a tier 1 number should be read.

### 6.4 Explicitly not collected

Vertex counts, bone counts, blendshape counts, texture resolutions and formats, avatar file
sizes, hierarchy depth, component inventories, and per-avatar totals of any kind. All are
easy; none currently maps to an AMUSE decision; several are fingerprints. If a later
question needs one, it is added then, with its decision named.

### 6.5 Privacy review for new report categories

The three-tier split (§5) is not self-enforcing. Anonymization protects *identifiers*, but a
**category** can identify without naming: a bucket narrow enough to hold one avatar, one
creator, or one purchased asset discloses it regardless of the label applied.

The philosophy stays as designed — raw private debugging data stays in the Lab, and public
reports carry only what an AMUSE decision needs. What this section adds is that the
philosophy is checked at the point a category is *added*, not only when data is exported.

Before any new category enters tier 2 or tier 3, state:

1. **Which AMUSE decision it informs.** No decision, no category. This is §6's rule applied
   to privacy rather than to relevance, and the two filters agree far more often than not.
2. **The smallest population a bucket could hold.** A category that can resolve to a single
   avatar, creator, or asset is an identifier in disguise. Prefer coarser buckets, and
   report a minimum bucket population alongside the distribution.
3. **What an adversary holding one avatar learns.** The realistic attacker already possesses
   content and is testing whether it appears in the census. Categories that let them confirm
   it must be coarsened or dropped.
4. **Whether tier 3 needs it at all.** A category can legitimately live in tier 2 for
   analysis and be aggregated away before tier 3. Tier 2 is not published; tier 3 is.

The existing shader-family category passes: it informs adapter prioritization, its buckets
are shader families rather than materials, and `UnknownFamily-A` confirms nothing about any
individual avatar. A hypothetical "materials with unusual property counts" category would
fail on 2 and 3 despite being trivially anonymizable.

When a category cannot be made safe, **the measurement is dropped, not the anonymization
weakened** — already a stop condition in §10.4.

## 7. How the harness proves itself trustworthy

### 7.1 Calibration fixtures — the Collect stage

Seven cases, constructed in code from primitive meshes and materials, never from an avatar,
never stored as assets. Five run in public CI as EditMode tests. Two require a vendor shader
and therefore run only in the Lab.

Each case carries two separable claims: **counting** — does the harness record this outcome
correctly — and **reachability** — can the production entry point produce this outcome in a
real project. The two are validated in different places.

| Case | Construction | Counting | Reachability |
|---|---|---|---|
| `UnsupportedRendererType` | `LineRenderer` on an empty GameObject | CI | CI |
| `UnsupportedTopology` | Mesh with one `MeshTopology.Quads` submesh | CI | CI |
| `MaterialPropertyOverridesPresent` | `MeshRenderer` + `SetPropertyBlock` carrying one value | CI | CI |
| `UnprovenMaterialSlotMapping` | 2-submesh mesh bound to 1 shared material | CI | CI |
| `SemanticsUnknown` | `MeshRenderer`, 2 triangles, plain Unity Standard material | CI | CI |
| `ProvenOpaque` | Constant alpha of exactly 1, no alpha texture | CI, via the semantics seam | **Lab**, vendor material |
| `MissingTextureEvidence` | Alpha bound to a runtime-constructed `Texture2D` that is not a project asset, so `UnityTextureEvidence` finds no `TextureImporter` | CI, via the semantics seam | **Lab**, vendor material |

The five refusal cases need no material semantics at all, so both claims collapse into one CI
test. Only the last two split, because the public project installs no vendor shader.

The first case is the one that catches the §5.2 null-versus-zero bug: it asserts
`submeshCount` and `triangleCount` are `null` and not `0`.

**Counting versus reachability are separate claims.** The harness's *counting* of a success
path can be validated in public CI without any vendor shader, by driving Collect through the
`BaseMaterialSemanticsProvider` seam AMUSE already exposes for its own integration tests —
this is a pass-through of an existing seam, not a new escape hatch. What that cannot
establish is that the *production* single-argument path reaches `ProvenOpaque` in a real
project. That is a reachability check, it runs in the Lab, and it must pass before every
census run. If vendor shaders are absent, the census **aborts** rather than skipping: a
census reporting near-total `SemanticsUnknown` because nothing was installed is a true
statement about the project and a false statement about AMUSE.

### 7.2 Arithmetic invariants

Checked on every record, calibration and real. Any violation aborts the run.

- `provenOpaque + mustRemainTransparent + unknown == submesh.triangleCount`, per submesh.
- `sum(submesh.triangleCount) == renderer.triangleCount` when `refusal == "None"`.
- The harness's own tally of `ProvenOpaque` equals `MeshSeparationPlan.OpaqueTriangleCount`,
  and likewise for the transparent count.

The third is the valuable one: it checks the harness's per-triangle tally against a number
AMUSE computed independently, so a misattribution bug cannot agree with itself.

### 7.3 Non-leakage becomes a test, not a promise

Because Anonymize is a pure function in this repository, non-leakage is provable by unit
test: construct tier 1 records seeded with distinctive fake private strings — creator names,
paths, GUIDs, shader names — run Anonymize, and assert that **no seeded string appears
anywhere in the tier 2 output**. A matching test asserts that tier 3 contains no per-entity
row. These are deterministic, need no avatar, and run in CI.

This is the single largest improvement over revision 1, which could only ask the operator to
be careful.

## 8. Mutation safety

**Layer 1 — the analyzed path is already read-only.** `UnityRendererAlphaAnalysis` reads
`sharedMesh` and `sharedMaterials` exclusively, precisely because `MeshFilter.mesh` and
`Renderer.materials` instantiate copies as a side effect of being read. It never calls
`GetPropertyBlock`, never bakes, imports, writes, or creates an asset. Collect adds nothing
to this path: it calls `Analyze` and reads the returned immutable result.

**Layer 2 — banned API list, now a CI check** over the research package source rather than a
manual grep, because the source is in this repository: `AssetDatabase.`, `AssetImporter`,
`TextureImporter`, `ModelImporter`, `EditorUtility.SetDirty`, `Undo.`, `PrefabUtility.`,
`EditorSceneManager.Save`, `SetPropertyBlock`, `.isReadable =`, `Texture2D.Apply`,
`renderer.material`, `renderer.materials`, `MeshFilter.mesh`, and `Object.Destroy` against
anything the harness did not itself create.

**Layer 3 — observable proof.** Before the run, record a manifest of `(path, size, mtime)`
for every asset in scope; after the run, recompute and diff; the report states
`assetManifestUnchanged`. Edit Mode only — never Play Mode, where animators and scripts can
mutate — and the scene is discarded without saving.

Layers 1 and 2 are promises. Layer 3 is the only one checkable after the fact, so it is
mandatory and its result appears in the report.

## 9. Census Lab structure

The Lab is a private Unity project that:

- references `Packages/com.alrauna.amuse` and `Packages/com.alrauna.amuse.research` from a
  working tree of this repository as local packages — never as copies;
- installs the vendor shader packages;
- holds avatars under recorded consent, plus the consent records themselves;
- holds tier 1 raw output and any identity mapping;
- is under no obligation to persist, and may be deleted and recreated freely.

It contains **no harness source**. Everything it holds is either private data or a
third-party package. If deleting the Lab would lose anything else, something has been put in
the wrong place.

## 10. Recommendations and next steps

### 10.1 Recommended repository changes

1. Add `Packages/com.alrauna.amuse.research/` with `Editor/` and `Tests/Editor/` asmdefs, a
   `package.json` with no `vpmDependencies` and a description stating it is never released.
2. Add one `InternalsVisibleTo("Alrauna.Amuse.Research.Editor")` line to
   `Packages/com.alrauna.amuse/Editor/AssemblyInfo.cs` — **only when the collector lands**,
   not before.
3. Create `docs/research/` only when the first aggregate report exists.
4. No workflow changes. Release and listing automation already scope to one named package.

### 10.2 Should this branch continue?

Yes, as a design branch, and it should end here. Its concern is architecture, and that
concern is now complete. Implementation belongs on separate branches, per AGENTS.md's one
coherent concern per branch.

### 10.3 Recommended implementation sequence

Two narrow vertical increments, deliberately ordered so the visibility decision is deferred
until it is actually needed:

**Increment 1 — `feat/census-record-schema`.** The tier 1/2/3 record types, Anonymize, and
Aggregate. Pure C#, no Unity objects, no AMUSE internals, **no visibility change at all**.
Fully unit-tested, including the §7.3 non-leakage tests. This lands the trust-critical half
of the system with complete public test coverage and zero coupling to the package.

**Increment 2 — `feat/census-collector`.** The Unity collector, the friend-assembly grant,
the five CI calibration cases, and the arithmetic invariants. This is where the visibility
change happens, and by then increment 1 has already proven the contract it feeds.

Only after both: `research/real-avatar-alpha-census`, the actual run, in the Lab.

### 10.4 Stop conditions for the implementation branches

Stop for review if any of these appears necessary:

- a change to any AMUSE analysis behaviour, result object, shader adapter, or evidence
  provider;
- any public API promotion;
- any visibility change beyond the single documented friend grant;
- attribution added to production analysis so the census can measure it — §6.2 forbids this
  and requires measuring the blind spot instead;
- a generic reporting, diagnostics, or telemetry framework emerging from what should be two
  pure functions;
- the harness needing to read private avatar data before consent is recorded;
- anonymization proving impossible for some measurement, in which case the measurement is
  dropped rather than the anonymization weakened.

## 11. Validation of this preparation branch

No code was produced, so validation is correspondingly narrow, stated honestly:

- **Verified:** the analysis surface the census depends on resolves in source at `711a7a8` —
  18 members and 4 enum cardinalities, checked mechanically.
- **Verified:** `release.yml` and `build-listing.yml` scope to a single `PACKAGE_NAME`, so a
  second package is excluded from release without any workflow change.
- **Verified:** `Tools/` is tracked and holds a PowerShell script, confirming it as the
  non-Unity script location and unsuitable for compiled harness code.
- **Verified:** no production file changed; no code added.
- **Not verified, and cannot be on this branch:** that the harness counts correctly. That
  requires the harness to exist and §7 to run. It is the first gate of increment 2.

## 12. Branch completion

This is a design branch and it ends here. Its concern — establishing the architecture for
AMUSE research tooling — is complete and reviewed.

### 12.1 Completion state

| | |
|---|---|
| Design document | Finalized at revision 3 |
| Implementation sequence | Documented (§10.3) |
| Production code | None written |
| Package created | None. `com.alrauna.amuse.research` is specified, not scaffolded |
| `InternalsVisibleTo` added | None. Deferred to the collector increment by design |
| Unity project changes | None |
| Workflow changes | None, and none needed (§3.3) |
| Census Lab | Not created, not accessed, not modified |
| Unity MCP | Not used at any point on this branch |

### 12.2 Decisions finalized and not to be reopened without new evidence

1. The harness lives in this repository as first-party public source; only private data
   lives in the Lab (§2).
2. `Packages/com.alrauna.amuse.research/`, name kept, governed by §3.2.1.
3. Three-stage separation — **Collect → Anonymize → Aggregate** — with only Collect touching
   Unity or AMUSE internals (§4.3). The separation is not to be weakened, and private-data
   handling does not move into the repository.
4. **No reflection-based harness** (§4.1).
5. **No production API promotion** (§4.2).
6. **Friend assembly only when collector implementation begins**, never earlier (§10.1).
7. **No AMUSE analysis change made solely to make census measurement easier** (§1, §6.2).
8. **Unknown attribution stays a measurement limitation.** The harness measures the size of
   the blind spot; it does not motivate changing production analysis (§6.2).
9. **No telemetry, no network reporting, no persistent analytics store.** Output is local
   files; the harness performs no network access of any kind.
10. Refused renderers record `null`, never `0` (§5.2).

### 12.3 Open questions carried to the implementation branches

These are deliberately unresolved here, because resolving them without code would be guessing:

- The consent recording procedure and its retention period (§10.4 gates the run, not the
  format).
- Minimum avatar diversity before results are reportable. A census over three avatars from
  one creator measures that creator, not the ecosystem, and no threshold is defensible
  before the population is known.
- Whether `MalformedMeshData`, `UnsupportedMultiplier`, `UnsupportedUvMapping`, and
  `UnsupportedSampling` warrant calibration cases. None is currently known to be reachable
  through the production entry point on well-formed avatar content; the census itself is the
  cheapest way to find out.
- The tier 2 → tier 3 aggregation's exact bucket boundaries, which §6.5 requires be chosen
  against real distributions rather than in advance.

### 12.4 Next branch

`feat/census-record-schema` — the tier 1/2/3 record types, `Anonymize`, and `Aggregate` as
pure C# over plain records, with unit tests including the §7.3 non-leakage tests.

It needs no Unity objects, no AMUSE internals, and **no visibility change**, so it can land
the trust-critical half of the system with full public test coverage and zero coupling to the
production package. The friend-assembly grant waits for `feat/census-collector` after it.
