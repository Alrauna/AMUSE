# Census Record Schema — Design

Branch: `feat/census-record-schema`
Base commit: `de7975e` (`origin/main`)
Date: 2026-08-20
Status: **Approved. Implements increment 1 of the sequence in
`2026-08-20-avatar-census-harness-preparation-design.md` §10.3.**

References in the form §N refer to the avatar census harness preparation design unless they include a document name. This document does not repeat that design.

## 1. Scope

This branch implements the **Anonymize** and **Aggregate** stages of the three-stage census architecture (§4.3). It also implements the record types that all three stages exchange. Here, only its output type represents the **Collect** stage. This output type contains tier 1 records. Tests in this branch construct these records manually. The increment 2 collector will construct them from live Unity objects.

In scope:

- tier 1 observation records, tier 2 anonymized records, tier 3 aggregate report;
- `CensusAnonymizer.Anonymize`, a pure function from tier 1 to tier 2;
- `CensusAggregator.Aggregate`, a pure function from tier 2 to tier 3;
- the research package that contains them;
- unit tests, including the §7.3 non-leakage tests.

Explicitly out of scope and deferred to `feat/census-collector` (§10.3):

- all Unity API use, all AMUSE type references, and the `InternalsVisibleTo` grant (§10.1.2);
- the collector, the calibration fixtures (§7.1), and the harness tally cross-check against `MeshSeparationPlan` (§7.2, third invariant);
- Markdown rendering of tier 3, JSON serialization of any tier, and `docs/research/`.

Production package behavior does not change. Workflows do not change. This branch changes only three items outside the new package. They are this document, `Packages/.gitignore`, and the `packages-lock.json` entry described in §2.

## 2. Package location

The pure records and functions go in a **new second embedded package**, `Packages/com.alrauna.amuse.research/`. §12.2.2 made this decision, and §10.1.1 scheduled it without a deferral condition. Only the friend grant (§10.1.2) waits for increment 2.

The team derived the decision again instead of inheriting it. They used the two tests in §3.4:

- *"Would an AMUSE user be worse off without it?"* No. A census schema is unnecessary weight in a shipped optimizer. Putting it in `com.alrauna.amuse` would ship dead code to every user. It would also require a later migration. *"Would an AMUSE developer?"* Yes. This tool is the trust-critical component that tells AMUSE what to build next.
- *"Would publishing this harm someone or breach a licence?"* No. It is first-party source that processes private data but contains none. §3.4: the data stays private, the code does not.

**The team verified release safety instead of assuming it.** `release.yml:33` builds `Packages/${{ vars.PACKAGE_NAME }}`. `build-listing.yml:61` passes `--current-package-name ${{ vars.PACKAGE_NAME }}`. Both workflows select exactly one named package. Thus, they structurally exclude a second embedded package from the release artifact and VPM listing. No workflow change is necessary. The package declares no `vpmDependencies`. Its `description` states its non-shipping status. Thus, a new reader learns this without reading the workflows (§3.3).

Running Unity revealed two consequences of adding a package. Reading alone did not reveal them. One consequence corrects §3.3.

- **`Packages/.gitignore` needs a new allow-list entry.** It ignores `/*/` and includes packages again by exact name. Thus, `com.alrauna.amuse.research` stays untracked and cannot be committed until the file names it. This branch adds the entry. Tracking does not affect the release scope. The workflows exclude it by building one named package, not because the file is absent.
- **`Packages/packages-lock.json` does gain an entry.** §3.3 says an embedded package "adds zero dependency churn" and needs no `manifest.json` entry. The `manifest.json` statement is correct and confirmed. It is unchanged. The lock file changes because Unity's package resolution records embedded packages. It adds a `com.alrauna.amuse.research` entry with `"source": "embedded"`. This is a real package-configuration change, so the commit includes it. It is not incidental churn that the team must discard.

## 3. Assembly layout

§3.2 sketched one `Alrauna.Amuse.Research.Editor` assembly. This design splits it into two assemblies. This split is the only deliberate structural deviation from the preparation design. It strengthens §4.3 instead of reorganizing it.

| Assembly | Created | Platform | Engine refs | References |
|---|---|---|---|---|
| `Alrauna.Amuse.Research.Census` | this branch | Editor | **none** | none |
| `Alrauna.Amuse.Research.Editor` | increment 2 | Editor | yes | `Alrauna.Amuse.Editor`, `Alrauna.Amuse.Research.Census` |
| `Alrauna.Amuse.Research.Tests.Editor` | this branch | Editor | yes | `Alrauna.Amuse.Research.Census` |

`Alrauna.Amuse.Research.Census` sets `noEngineReferences: true`. Thus, §4.3's claim that Anonymize and Aggregate need no Unity object becomes a compile error. A reviewer no longer must verify this claim by reading. One assembly could not provide this protection. The increment 2 collector needs `UnityEngine`, so the flag would need removal. The next branch would then silently lose the boundary that this branch establishes.

The Census assembly keeps `includePlatforms: ["Editor"]`, although its code is platform-neutral. Thus, no future packaging mistake can put research code into a player build.

The increment 2 friend grant still names `Alrauna.Amuse.Research.Editor` exactly as §4.2 specifies. It still reaches only the collector. §4.3 requires that only Collect sees internals. The assembly boundary now enforces this requirement instead of convention.

The test assembly keeps engine references because the Unity test runner requires them. The important guarantee applies to the tested assembly.

### 3.1 Accessibility

Research package types are `public`. AMUSE production types are `internal` because the distributable package gives users a compatibility promise. The research package is never released, so it gives no such promise. It gains nothing from `internal`. Thus, the research package needs no `InternalsVisibleTo` of its own. The repository keeps AMUSE's existing test-assembly grant and the single documented grant that increment 2 will add.

## 4. Mirrored category enums

The Census assembly cannot reference AMUSE. Thus, it declares enums that mirror AMUSE's vocabulary. The team copied values from the current source, not from memory:

| Census enum | Mirrors | Source |
|---|---|---|
| `RendererRefusal` | `RendererAnalysisRefusal` | `Editor/Host/UnityRendererAlphaAnalysis.cs:15` |
| `AlphaResolutionFailure` | `AlphaResolutionFailure` | `Editor/Analysis/AlphaSemanticsResolver.cs:12` |
| `SeparationDisposition` | `SubmeshSeparationDisposition` | `Editor/Analysis/MeshSeparationPlanner.cs:6` |

Two enums have no AMUSE counterpart and are census concepts:

- `ShaderFamilyAttestation` lists `None`, `Poiyomi`, `LilToon`. The increment 2 collector can derive it from each frontend's `IsSupportedMaterial` flag. Each frontend already returns this flag (`Editor/Semantics/UnityMaterialSemantics.cs`). Thus, it requires no production change (§1).
- `RendererKind` lists `SkinnedMesh`, `Mesh`, `Other`. It uses an enum instead of a type-name string. Thus, a third-party renderer type name can never reach tier 2. `Other` is sufficient because AMUSE refuses all other types as `UnsupportedRendererType`.

### 4.1 These are snapshots, not live synchronization

The mirrored enums are a **snapshot of AMUSE's vocabulary at this commit**. No mechanism tracks AMUSE's enums automatically, and the design deliberately excludes such tracking. A census schema could silently absorb a new AMUSE value. It would then report the value under an existing category or omit it. Either result miscounts data in a tool whose full value is trustworthy counting.

When AMUSE adds a value, the research package must **fail loudly and demand an explicit schema decision**. The decision must determine whether the value creates a new census category or joins an existing category. It must also determine the effects on aggregates and the §6.5 privacy review.

This branch can implement only half of that behavior. This document states the limitation instead of hiding it. By requirement, the Census assembly does not reference AMUSE. Thus, no test here can observe AMUSE's enums. Runtime reflection would violate §12.2.4 and its approved interpretation. Tests can use reflection on the research package's *own* contract, but not to access AMUSE internals.

Therefore:

- **This branch** pins the exact member set and ordinal values of each mirrored enum in `CensusCategorySnapshotTests`. Editing a census enum without updating the pin fails the build. Thus, no census-side change is silent. Each enum has an XML documentation comment. The comment names the AMUSE type that it mirrors and states that it is a snapshot.
- **Increment 2** completes the guarantee because the friend grant makes AMUSE's enums visible at compile time. An exhaustive `switch` maps each AMUSE enum to its census mirror. It has **no default arm that guesses**. An unmapped value throws a named error instead of joining an existing category. A parity test checks that each AMUSE enum and its census mirror have equal member sets. A new AMUSE value then breaks the research package loudly in CI in the same commit. This applies §4.2's compile-time coupling argument to the schema.

Until increment 2 lands, the system **does not detect** drift between AMUSE and the census vocabulary. This is acceptable only because nothing uses these records yet.

## 5. Tier 1 — raw observation records

`ObservedAvatar` → `ObservedRenderer` → `ObservedSubmesh`, plus `CensusObservationSet` as the run root.

Tier 1 deliberately contains identifying data (§5.1). It contains creator name, avatar name, avatar asset path and GUID, renderer hierarchy path, and GameObject name. It also contains raw renderer type name, material name, material asset path and GUID, and raw shader name. A census anomaly is not debuggable if it cannot be traced to a specific material. These records exist only in memory and Lab output. Nothing in this repository writes them elsewhere.

`ObservedRenderer.SubmeshCount` and `ObservedRenderer.TriangleCount` are `int?`. The type implements §5.2's rule. When no mesh is reachable, the value is `null`. A value of `0` is reserved for a mesh that really has none.

### 5.1 Construction invariants

§7.2 requires checks of arithmetic invariants on every record. Constructors enforce the two record-local invariants, so callers cannot build an invalid record:

1. `ProvenOpaqueTriangleCount + MustRemainTransparentTriangleCount + UnknownTriangleCount` equals `TriangleCount`, per submesh.
2. `sum(submesh.TriangleCount)` equals `renderer.TriangleCount` when `Refusal == None`.

Constructors also enforce two structural rules from §5.2:

3. A refused renderer has an empty submesh list.
4. `Refusal == None` requires non-null `SubmeshCount` and `TriangleCount`. By construction, a renderer that AMUSE analyzed successfully has a reachable mesh.

The third §7.2 invariant is the harness tally against `MeshSeparationPlan.OpaqueTriangleCount`. It needs AMUSE and belongs to increment 2.

All collection-valued properties use defensive copies and expose them as `IReadOnlyList<T>`. Thus, a caller cannot change a constructed record by changing the supplied list. `System.Collections.Immutable` is not available in this Unity version's profile. The implementation uses the existing repository idiom: `Array.AsReadOnly` over a private copy.

## 6. Tier 2 — anonymized records

`AnonymizedAvatar` → `AnonymizedRenderer` → `AnonymizedSubmesh`, plus `AnonymizedCensus`.

Identity uses ordinals only (§5.2):

- `Avatar-01` is 1-based and follows input order.
- `Renderer-01-004` is avatar-scoped.
- `Material-01-007` is avatar-scoped, so the records do not show cross-avatar asset sharing.

`MaterialId` is `null` when `HasMaterial` is false. An empty slot has no material. Creating an ID for it would invent a distinct nonexistent material and increase the material count.

`ShaderFamily` is the only free-form string in tier 2. Its value always comes from `Poiyomi`, `LilToon`, and `UnknownFamily-A`, `-B`, … The anonymizer numbers unattested families **globally** by first appearance across the full run. It keys them by raw shader name. The scope is global because §6.1's headline number is the fraction of materials from one unattested family. That is a corpus-level question. §6.5 already reviewed and approved this category. `ShaderFamily` is `null` when `HasMaterial` is false.

All other tier 2 values are enums or numbers. `RendererKind` replaces the tier 1 renderer type name for the reason in §4.

### 6.1 Material consistency

Construction enforces agreement between `HasMaterial`, `MaterialId`, and `ShaderFamily`. A submesh with a material has an identity and a family. A submesh without a material has neither. The anonymizer produces only consistent records. However, the type is public, and a later reader could rebuild a record. Therefore, the type makes corrupt states impossible instead of only leaving them unproduced. Otherwise, a partial record reaches the aggregator and fails on a null dictionary key, far from the original error.

### 6.2 Determinism

`Anonymize` takes no seed. The function assigns ordinals by input position, so equal input produces equal output. It does not use hashing, salting, clocks, machine values, or GUID generation.

The team considered and rejected a seed. Its only purpose would be to make ordinals unpredictable across runs. However, §5.2 says that ordinals are run-local and have no meaning across runs. The §6.5 threat model covers an adversary confirming that held content appears in a published report. Ordinal ordering does not help the adversary do this. Here, anonymization prevents disclosure in exported aggregates. It does not support longitudinal tracking of individual avatars.

The implementation preserves input order everywhere. It never derives output order from hash-table enumeration. First-appearance assignment tables are ordered lists, not dictionaries used for output iteration.

## 7. Tier 3 — aggregate report

`CensusAggregateReport` is one flat immutable object. §5.3 prohibits per-avatar and per-renderer rows. The type has no collection of entity records. It has only counts keyed by category.

Contents:

- population: avatar, renderer, submesh, and distinct-material counts;
- renderer counts by `RendererRefusal` (all seven values) and by `RendererKind`;
- submesh counts and triangle-weighted counts by `AlphaResolutionFailure`, by `SeparationDisposition`, and by shader family;
- the headline triangle split: proven opaque, must remain transparent, unknown, and their sum as an explicit classified-triangle denominator;
- the §6.2 blind spot: the count and triangle weight of submeshes with `Failure == None` that still contain `Unknown` triangles;
- submeshes with no material;
- `AvatarsWithAtLeastOneOpaqueCandidate` against `AvatarCount`. This is the §5.3 example and the only avatar-level statistic. It is a count, never a list.

Per §12.3, the report contains no numeric buckets, histograms, percentiles, or ranges. The team must choose bucket boundaries from real distributions under §6.5 review. It must not invent them in advance.

Category dictionaries contain every enum member, including members with a count of zero. An observed zero is a measurement, not missing information. A stable key set makes reports comparable across runs.

### 7.1 Null versus zero

`TotalRendererTriangleCount` is `long?`. It sums only renderers with a known triangle count. It is `null` exactly when no renderer has a known count. In that case, the denominator is unavailable, so an honest total is impossible. The report also publishes `RenderersWithKnownTriangleCount` and `RenderersWithUnknownTriangleCount`. Thus, every reader gets the denominator that §5.2 requires.

`ClassifiedTriangleCount` is deliberately different. It counts only triangles that AMUSE classified. The gap between the values shows coverage. Refused renderers contribute geometry to the first value but nothing to the second. Collapsing them or defaulting either value to zero causes the miscount that §5.2 identifies as the system's most likely miscount.

## 8. Testing

All tests are EditMode and deterministic. They need no avatar or Unity object, and they run in public CI.

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

§7.3 makes non-leakage provable instead of promised. The test puts distinctive tokens in every identifying field of a tier 1 set. These fields include creator name, avatar name, asset paths, GUIDs, hierarchy paths, GameObject names, material names, and shader names. It runs `Anonymize` and then uses reflection to walk the full tier 2 object graph. It checks public and non-public fields and properties and collects every reachable string.

It then checks two conditions:

1. **No seeded token appears** in any reachable string.
2. **Every reachable string matches the tier 2 allow-list**: `Avatar-NN`, `Renderer-NN-MMM`, `Material-NN-MMM`, `Poiyomi`, `LilToon`, or `UnknownFamily-X`.

Assertion 2 ensures that the test fails when a future contributor adds an identifying field. Assertion 1 alone catches only fields that contain the exact seeded values. Assertion 2 fails for *any* new string that reaches tier 2, including unanticipated data. The contributor must justify the new category under §6.5 and deliberately extend the allow-list. The contributor must never relax the assertion.

`CensusAggregateReportPrivacyTests` uses the same reflective walk on tier 3. It also verifies that no reachable member is a collection of avatar, renderer, or submesh records.

Here, reflection inspects the research package's own types to verify its contract. It is not the reflection that §12.2.4 prohibits. That prohibited reflection accesses AMUSE internals at run time instead of using compile-time integration.

## 9. Stop conditions honoured

The branch reached none of §10.4's stop conditions. It changes no AMUSE analysis behavior, result object, adapter, or evidence provider. It promotes no API and makes no visibility change. It adds no attribution to production analysis. It introduces no reporting, diagnostics, or telemetry framework beyond two pure functions and their exchanged records. It reads no avatar and weakens no anonymization.

## 10. Open questions carried forward

- The AMUSE-side enum parity check is deferred to increment 2 (§4.1). Drift remains undetected until then.
- The design does not resolve whether `MaterialCount` should count materials distinctly per avatar or try to calculate a corpus total. Avatar-scoped IDs make a corpus total unavailable. This is the intended privacy property (§5.2). Therefore, the report gives the per-avatar-distinct sum and identifies it as such.
- Tier 3 bucket boundaries remain deferred to real distributions (§12.3).
