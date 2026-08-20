# Census Record Schema — Design

Branch: `feat/census-record-schema`
Base commit: `de7975e` (`origin/main`)
Date: 2026-08-20
Status: **Approved. Implements increment 1 of the sequence in
`2026-08-20-avatar-census-harness-preparation-design.md` §10.3.**

Section references of the form §N without a document name refer to the avatar census harness
preparation design, which this document does not restate.

## 1. Scope

This branch implements the **Anonymize** and **Aggregate** stages of the three-stage census
architecture (§4.3), plus the record types all three stages exchange. The **Collect** stage is
represented here only by its output type — tier 1 records — which this branch constructs by
hand in tests and which the increment 2 collector will construct from live Unity objects.

In scope:

- tier 1 observation records, tier 2 anonymized records, tier 3 aggregate report;
- `CensusAnonymizer.Anonymize`, a pure function from tier 1 to tier 2;
- `CensusAggregator.Aggregate`, a pure function from tier 2 to tier 3;
- the research package that holds them;
- unit tests, including the §7.3 non-leakage tests.

Explicitly out of scope, deferred to `feat/census-collector` (§10.3):

- any Unity API use, any AMUSE type reference, and the `InternalsVisibleTo` grant (§10.1.2);
- the collector, the calibration fixtures (§7.1), and the cross-check of the harness tally
  against `MeshSeparationPlan` (§7.2, third invariant);
- Markdown rendering of tier 3, JSON serialization of any tier, and `docs/research/`.

No production package behaviour changes. No workflow changes. The only files outside the new
package that this branch touches are this document, `Packages/.gitignore`, and the
`packages-lock.json` entry described in §2.

## 2. Package location

The pure records and functions go in a **new second embedded package**,
`Packages/com.alrauna.amuse.research/`. This was decided in §12.2.2 and scheduled in §10.1.1
with no deferral condition; only the friend grant (§10.1.2) waits for increment 2.

The decision was re-derived rather than inherited, using §3.4's own two tests:

- *"Would an AMUSE user be worse off without it?"* No. A census schema is inert weight inside
  a shipped optimizer, and placing it in `com.alrauna.amuse` would ship dead code to every
  user and require a migration later. *"Would an AMUSE developer?"* Yes — this is the
  trust-critical half of the tool that tells AMUSE what to build next.
- *"Would publishing this harm someone or breach a licence?"* No. It is first-party source
  that processes private data without containing any. §3.4: the data stays private, the code
  does not.

**Release safety was verified, not assumed.** `release.yml:33` builds
`Packages/${{ vars.PACKAGE_NAME }}` and `build-listing.yml:61` passes
`--current-package-name ${{ vars.PACKAGE_NAME }}`. Both scope to exactly one named package,
so a second embedded package is structurally excluded from the release artifact and from the
VPM listing with no workflow change. The package declares no `vpmDependencies` and states its
non-shipping status in its own `description`, so a human reading it cold learns this without
reading the workflows (§3.3).

Two consequences of adding a package were found by running Unity rather than by reading, and
one of them corrects §3.3.

- **`Packages/.gitignore` needs a new allow-list entry.** It ignores `/*/` and re-includes
  packages by exact name, so `com.alrauna.amuse.research` is untracked — and therefore
  uncommittable — until it is named there. The entry is added in this branch. Being tracked
  does not affect release scope: exclusion comes from the workflows building one named
  package, not from the file being absent.
- **`Packages/packages-lock.json` does gain an entry.** §3.3 says an embedded package "adds
  zero dependency churn" and needs no `manifest.json` entry. The `manifest.json` half is
  correct and was confirmed: it is unchanged. The lock file is not — Unity's package resolve
  records embedded packages, adding a `com.alrauna.amuse.research` entry with
  `"source": "embedded"`. This is a real package-configuration change and belongs in the
  commit; it is not incidental churn to be discarded.

## 3. Assembly layout

§3.2 sketched a single `Alrauna.Amuse.Research.Editor` assembly. This design splits it in two.
The split is the one deliberate structural deviation from the preparation design, and the
reason is that it strengthens §4.3 rather than reorganizing it.

| Assembly | Created | Platform | Engine refs | References |
|---|---|---|---|---|
| `Alrauna.Amuse.Research.Census` | this branch | Editor | **none** | none |
| `Alrauna.Amuse.Research.Editor` | increment 2 | Editor | yes | `Alrauna.Amuse.Editor`, `Alrauna.Amuse.Research.Census` |
| `Alrauna.Amuse.Research.Tests.Editor` | this branch | Editor | yes | `Alrauna.Amuse.Research.Census` |

`Alrauna.Amuse.Research.Census` sets `noEngineReferences: true`. §4.3's claim that Anonymize
and Aggregate need no Unity object then stops being a promise a reviewer must verify by
reading, and becomes a compile error. A single assembly could not offer this: increment 2's
collector needs `UnityEngine`, so the flag would have to be cleared, and the boundary that
this branch exists to establish would be silently lost in the branch that follows it.

`includePlatforms: ["Editor"]` is kept on the Census assembly even though its code is
platform-neutral, so no research code can enter a player build under any future packaging
mistake.

The friend grant in increment 2 still names `Alrauna.Amuse.Research.Editor` exactly as §4.2
specifies, and still reaches only the collector — which is §4.3's requirement that only
Collect sees internals, now enforced by assembly boundary rather than by convention.

The test assembly keeps engine references because the Unity test runner requires them; the
guarantee that matters is on the assembly under test.

### 3.1 Accessibility

Research package types are `public`. AMUSE production types are `internal` because the
distributable package makes a compatibility promise to users; the research package is never
released, so it makes no such promise and gains nothing from `internal`. This also means the
research package needs no `InternalsVisibleTo` of its own — the only friend grant in the
repository stays the one AMUSE already has for its test assembly, plus the single documented
grant increment 2 will add.

## 4. Mirrored category enums

The Census assembly cannot reference AMUSE, so it declares its own enums mirroring AMUSE's
vocabulary. Values were copied from the current source, not from memory:

| Census enum | Mirrors | Source |
|---|---|---|
| `RendererRefusal` | `RendererAnalysisRefusal` | `Editor/Host/UnityRendererAlphaAnalysis.cs:15` |
| `AlphaResolutionFailure` | `AlphaResolutionFailure` | `Editor/Analysis/AlphaSemanticsResolver.cs:12` |
| `SeparationDisposition` | `SubmeshSeparationDisposition` | `Editor/Analysis/MeshSeparationPlanner.cs:6` |

Two enums have no AMUSE counterpart and are census concepts:

- `ShaderFamilyAttestation` — `None`, `Poiyomi`, `LilToon`. Derivable by the increment 2
  collector from the `IsSupportedMaterial` flag each frontend already returns
  (`Editor/Semantics/UnityMaterialSemantics.cs`), so it requires no production change (§1).
- `RendererKind` — `SkinnedMesh`, `Mesh`, `Other`. An enum rather than a type name string, so
  a third-party renderer type name can never reach tier 2. `Other` is sufficient because
  AMUSE refuses everything else as `UnsupportedRendererType`.

### 4.1 These are snapshots, not live synchronization

The mirrored enums are a **snapshot of AMUSE's vocabulary at this commit**. There is no
mechanism, and deliberately no intent, to track AMUSE's enums automatically. A census schema
that silently absorbed a new AMUSE value would report it under an existing category or drop
it, and either outcome is a miscount in a tool whose entire value is trustworthy counting.

The required behaviour when AMUSE adds a value is that the research package **fails loudly and
demands an explicit schema decision** — is the new value a new census category, or does it
fold into an existing one, and what does that do to the aggregates and to §6.5's privacy
review?

This branch can only implement half of that, and the limitation is stated rather than papered
over. The Census assembly has no reference to AMUSE, by requirement, so no test here can
observe AMUSE's enums. Reaching them by runtime reflection would violate §12.2.4 and the
approved reading of it — reflection is acceptable in tests over the research package's *own*
contract, not as a route into AMUSE internals.

Therefore:

- **This branch** pins each mirrored enum's exact member set and ordinal values in
  `CensusCategorySnapshotTests`. Editing a census enum without updating the pin fails the
  build, so no census-side change is silent. Each enum carries an XML doc comment naming the
  AMUSE type it mirrors and stating that it is a snapshot.
- **Increment 2** completes the guarantee, where the friend grant makes AMUSE's enums visible
  at compile time. The collector's mapping from each AMUSE enum to its census mirror is an
  exhaustive `switch` **with no default arm that guesses**: an unmapped value throws a named
  error rather than being folded into an existing category. A parity test asserts each AMUSE
  enum and its census mirror have equal member sets. A new AMUSE value then breaks the
  research package in CI, in the same commit, loudly — which is the §4.2 argument for
  compile-time coupling applied to the schema.

Until increment 2 lands, drift between AMUSE and the census vocabulary is **undetected**. That
is acceptable only because nothing consumes these records yet.

## 5. Tier 1 — raw observation records

`ObservedAvatar` → `ObservedRenderer` → `ObservedSubmesh`, plus `CensusObservationSet` as the
run root.

Tier 1 deliberately carries identifying data (§5.1): creator name, avatar name, avatar asset
path and GUID, renderer hierarchy path and GameObject name, raw renderer type name, material
name, material asset path and GUID, and raw shader name. A census anomaly that cannot be
traced to a concrete material is not debuggable. These records exist in memory and in Lab
output only; nothing in this repository writes them anywhere.

`ObservedRenderer.SubmeshCount` and `ObservedRenderer.TriangleCount` are `int?`. §5.2's rule is
implemented at the type level: when no mesh is reachable the value is `null`, and `0` is
reserved for a mesh that genuinely has none.

### 5.1 Construction invariants

§7.2 requires arithmetic invariants be checked on every record. The two that are record-local
are enforced in the constructors, so an invalid record cannot be built:

1. `ProvenOpaqueTriangleCount + MustRemainTransparentTriangleCount + UnknownTriangleCount`
   equals `TriangleCount`, per submesh.
2. `sum(submesh.TriangleCount)` equals `renderer.TriangleCount` when `Refusal == None`.

Two structural rules from §5.2 are enforced alongside them:

3. A refused renderer carries an empty submesh list.
4. `Refusal == None` requires non-null `SubmeshCount` and `TriangleCount`. A renderer AMUSE
   analyzed successfully has a reachable mesh by construction.

The third §7.2 invariant — the harness tally against `MeshSeparationPlan.OpaqueTriangleCount`
— needs AMUSE and belongs to increment 2.

All collection-valued properties are defensively copied and exposed as `IReadOnlyList<T>`, so a
caller mutating the list it passed cannot alter a constructed record. `System.Collections.Immutable`
is not available on this Unity version's profile; `Array.AsReadOnly` over a private copy is the
existing repository idiom and is used here.

## 6. Tier 2 — anonymized records

`AnonymizedAvatar` → `AnonymizedRenderer` → `AnonymizedSubmesh`, plus `AnonymizedCensus`.

Identity is ordinal only (§5.2):

- `Avatar-01` — 1-based, in input order.
- `Renderer-01-004` — avatar-scoped.
- `Material-01-007` — avatar-scoped, so cross-avatar asset sharing is not recorded.

`MaterialId` is `null` when `HasMaterial` is false. An empty slot has no material, and
fabricating an id for one would invent a distinct material that does not exist and inflate the
material count.

`ShaderFamily` is the only free-form string in tier 2, and is provably drawn from
`Poiyomi`, `LilToon`, and `UnknownFamily-A`, `-B`, … Unattested families are numbered
**globally**, in first-appearance order over the whole run, keyed by raw shader name — global
because §6.1's headline number is the fraction of materials one unattested family accounts
for, which is a corpus-level question. §6.5 already reviewed this category and passed it.
`ShaderFamily` is `null` when `HasMaterial` is false.

Everything else in tier 2 is an enum or a number. `RendererKind` replaces the tier 1 renderer
type name for the reason in §4.

### 6.1 Material consistency

`HasMaterial`, `MaterialId`, and `ShaderFamily` are enforced to agree at
construction: a submesh with a material carries both an identity and a family, and one
without carries neither. The anonymizer already produced only consistent records, but the
type is public and a later reader could rebuild one, so the corrupting states are made
unrepresentable rather than merely unproduced. Without this, a half-populated record reaches
the aggregator and fails on a null dictionary key, a long way from the mistake.

### 6.2 Determinism

`Anonymize` takes no seed. Ordinals are assigned by position in the input, so equal input
produces equal output by construction, and no hashing, salting, clock, machine value, or
GUID generation appears anywhere in the function.

A seed was considered and rejected. Its only purpose would be to make ordinals unpredictable
across runs, but §5.2 already establishes that ordinals are run-local and carry no meaning
across runs, and the threat model in §6.5 is an adversary confirming that content they hold
appears in a published report — which ordinal ordering does not help them do. The purpose of
anonymization here is preventing disclosure in exported aggregates, not enabling longitudinal
tracking of individual avatars.

The implementation preserves input order everywhere and never derives output ordering from
hash-table enumeration. First-appearance assignment tables are ordered lists, not dictionaries
iterated for output.

## 7. Tier 3 — aggregate report

`CensusAggregateReport` is one flat immutable object. §5.3 forbids per-avatar and per-renderer
rows, and the type has no collection of entity records at all — only counts keyed by category.

Contents:

- population: avatar, renderer, submesh, and distinct-material counts;
- renderer counts by `RendererRefusal` (all seven values) and by `RendererKind`;
- submesh counts and triangle-weighted counts by `AlphaResolutionFailure`, by
  `SeparationDisposition`, and by shader family;
- the headline triangle split — proven opaque, must remain transparent, unknown — and their
  sum as an explicit classified-triangle denominator;
- the §6.2 blind spot: the count and triangle weight of submeshes with `Failure == None` that
  still carry `Unknown` triangles;
- submeshes with no material;
- `AvatarsWithAtLeastOneOpaqueCandidate` against `AvatarCount` — the §5.3 example, and the
  only avatar-level statistic, expressed as a count and never as a list.

Per §12.3, no numeric buckets, histograms, percentiles, or ranges appear. Bucket boundaries
are to be chosen against real distributions under §6.5 review, not invented in advance.

Category dictionaries carry every enum member including those with a count of zero. An
observed zero is a measurement, not missing information, and a stable key set makes the report
comparable across runs.

### 7.1 Null versus zero

`TotalRendererTriangleCount` is `long?`. It sums only renderers with a known triangle count,
and is `null` exactly when no renderer had one — the denominator is unavailable, so there is
no honest total. It is published alongside `RenderersWithKnownTriangleCount` and
`RenderersWithUnknownTriangleCount`, so every reader has the denominator §5.2 requires.

This is deliberately distinct from `ClassifiedTriangleCount`, which counts only triangles AMUSE
actually classified. The gap between the two *is* the coverage story: refused renderers
contribute geometry to the first and nothing to the second. Collapsing them, or defaulting
either to zero, is the miscount §5.2 calls the most likely in the whole system.

## 8. Testing

All tests are EditMode, deterministic, need no avatar and no Unity object, and run in public
CI.

| Test file | Covers |
|---|---|
| `CensusCategorySnapshotTests` | §4.1 — exact member set and ordinal of every mirrored enum |
| `CensusObservationTests` | tier 1 construction, the four invariants of §5.1, defensive copying |
| `AnonymizedRecordTests` | tier 2 construction, the material-consistency invariant of §6.1, defensive copying, read-only report views |
| `CensusAnonymizerTests` | ordinal assignment, null material id and shader family, unknown-family lettering, determinism |
| `CensusAnonymizerNonLeakageTests` | §7.3 — the trust-critical test |
| `CensusAggregatorTests` | arithmetic over categories and triangle weights, blind spot, opaque-candidate avatar count |
| `CensusAggregatorNullVersusZeroTests` | §7.1 above — refusals never average in as zero |
| `CensusAggregatorEmptyTests` | empty run: zero counts, all keys present, null total |
| `CensusAggregateReportPrivacyTests` | §5.3 — no per-entity rows reachable from tier 3 |

### 8.1 The non-leakage test

§7.3 makes non-leakage provable rather than promised. The test seeds a tier 1 set with
distinctive tokens in every identifying field — creator name, avatar name, asset paths, GUIDs,
hierarchy paths, GameObject names, material names, shader names — runs `Anonymize`, then walks
the entire tier 2 object graph by reflection, over public and non-public fields and
properties, collecting every reachable string.

It then asserts two things:

1. **No seeded token appears** in any reachable string.
2. **Every reachable string matches the tier 2 allow-list** — `Avatar-NN`,
   `Renderer-NN-MMM`, `Material-NN-MMM`, `Poiyomi`, `LilToon`, or `UnknownFamily-X`.

Assertion 2 is the one that satisfies the requirement that the test fail when a future
contributor adds an identifying field. Assertion 1 alone would only catch fields carrying the
specific values this test happened to seed; assertion 2 fails on *any* new string reaching
tier 2, including one carrying data nobody anticipated. The correct response to that failure
is to justify the new category under §6.5 and extend the allow-list deliberately — never to
relax the assertion.

`CensusAggregateReportPrivacyTests` applies the same reflective walk to tier 3 and additionally
asserts that no reachable member is a collection of avatar, renderer, or submesh records.

Reflection here inspects the research package's own types to verify its own contract. It is
not the reflection §12.2.4 rules out, which is reaching into AMUSE internals at run time in
place of compile-time integration.

## 9. Stop conditions honoured

None of §10.4's stop conditions was reached. This branch changes no AMUSE analysis behaviour,
result object, adapter, or evidence provider; promotes no API; makes no visibility change;
adds no attribution to production analysis; introduces no reporting, diagnostics, or telemetry
framework beyond two pure functions and the records they exchange; reads no avatar; and
weakens no anonymization.

## 10. Open questions carried forward

- The AMUSE-side enum parity check is deferred to increment 2 (§4.1). Drift is undetected
  until then.
- Whether `MaterialCount` should count materials distinctly per avatar or attempt a corpus
  total is unresolved by design: avatar-scoped ids make a corpus total unavailable, which is
  the intended privacy property (§5.2), so the report states the per-avatar-distinct sum and
  names it as such.
- Tier 3 bucket boundaries remain deferred to real distributions (§12.3).
