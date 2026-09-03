# Pull request archive — pre-recreation history

Pull request titles and descriptions from before the repository recreation on
2026-08-20.

**The numbers below refer to the previous repository and do not correspond to current
pull requests.** Recreation restarted numbering: what this file calls #16 is the design
pull request that is #1 today. Commit hashes quoted in these descriptions predate a
history rewrite and no longer resolve.

These descriptions stay here because they carry per-pull-request validation
evidence that exists nowhere else in the repository. The evidence covers test
counts, Console error counts, and private-testbed attestations. The designs
themselves live under `docs/superpowers/specs/`, and the code history is intact
in git.

---

## #16 — docs: avatar census harness preparation design

state: OPEN  merged: n/a

Design-only branch establishing the architecture for AMUSE research tooling, with the avatar alpha census as the first concrete instance.
**No production code, no package creation, no visibility change, no Unity change.** One file added.

## Corrected vision boundary

The boundary is **public AMUSE source versus private Census Lab data** — not production code versus research code.

```
AMUSE repository        public source: harness, schema, anonymizer,
   |                    aggregator, fixtures, validation, reports
   |  referenced as a local Unity package
   v
AMUSE-Census-Lab        private: avatars, vendor shader packages,
   |                    consent records, raw run output
   v
private research runs
```

Harness source is first-party public code in this repository. The Lab holds no
source. Anyone can delete and recreate it at any time. If deleting it would lose
anything but private data and third-party packages, something is in the wrong
place.

An earlier revision assumed the harness should live outside the repository as a
disposable private tool. §0 records that assumption as invalidated, along with
five others that followed from it.

## Research package decision

Research tooling goes in `Packages/com.alrauna.amuse.research/` — specified here, **not created in this PR**.

Unity compiles only `Assets/` and `Packages/`. The repo reserves `Tools/` for
non-Unity scripts and `Tools/` already holds one. C# there would never compile.
`Assets/` admits no cross-project reference, so the Lab could not consume it
without copying. A second embedded package is the only viable home.

The name is deliberate and stays unchanged: do not rename it to `tools` or
`devtools`. A package named for what it *is* invites contributions with a
purpose. A package named for what it *is not shipped as* invites anything with
nowhere else to go. §3.2.1 sets five governance rules — first-party, never
shipped, never private data, never a dumping ground. Every addition states its
AMUSE purpose.

**Release safety needs no work.** `release.yml` builds
`Packages/${{ vars.PACKAGE_NAME }}` and `build-listing.yml` passes
`--current-package-name`. Both scope to one named package, so release and the
VPM listing already exclude a second package. Unity auto-discovers embedded
packages, so `Packages/manifest.json` needs no entry either.

## Collect → Anonymize → Aggregate

| Stage | Unity? | Internals? |
|---|---|---|
| Collect (Renderer → tier 1) | yes | yes |
| Anonymize (tier 1 → tier 2) | no | no |
| Aggregate (tier 2 → tier 3) | no | no |

Only Collect touches Unity or AMUSE internals. Anonymize and Aggregate are pure
functions over plain records, mirroring the AMUSE analysis/mutation testing
boundary.

This makes **non-leakage provable rather than promised**. Seed tier 1 records
with distinctive fake creator names, paths, and GUIDs. Run Anonymize, and assert
none appears anywhere in tier 2. Deterministic, needs no avatar, runs in CI. The
earlier design could only ask the operator to be careful.

Tier 1 (raw, may contain real identifiers) stays in the Lab. Tier 2 is ordinal-only. Tier 3 is distributions with no per-avatar or per-renderer rows.

## No reflection

Reflection existed because an externally hosted harness cannot be a friend
assembly. With the harness in-repo, that reason is gone. Reflection would cost
a surface-compatibility probe and name-keyed enum handling, and it would lose
compile-time safety. That machinery would re-create at run time, imperfectly,
what the compiler does for free.

For a tool whose value is trustworthy counting, compile-time coupling is a
feature. A rename that breaks the census should break loudly in CI, not
silently in a private run.

## InternalsVisibleTo deferred

§4.2 justifies the friend grant
`[assembly: InternalsVisibleTo("Alrauna.Amuse.Research.Editor")]`, but this PR
**deliberately does not add it**. It lands only when the collector needs it.

It changes no public API, adds no production code and no escape hatch, and
matches the existing `Alrauna.Amuse.Tests.Editor` precedent exactly. The
decision went against public API promotion. It would commit AMUSE to supporting
an analysis API long before the product is ready to.

## Preserved architectural constraints

- No AMUSE analysis change made solely to ease measurement.
- Unknown attribution stays a measurement limitation. The census will hit
  submeshes where `Unknown` triangles carry no recorded reason. The harness
  measures the *size of the blind spot* rather than motivating a change to
  production analysis.
- No telemetry, no network reporting, no persistent analytics store.
- Refused renderers record `null`, never `0` — the most likely miscount in the system, with a dedicated calibration case.

## Next implementation branch

**`feat/census-record-schema`** — tier 1/2/3 record types, `Anonymize`, and `Aggregate` as pure C# over plain records, with unit tests including the non-leakage tests.

It needs no Unity objects, no AMUSE internals, and no visibility change. So it
lands the trust-critical half with full public test coverage and zero coupling
to the production package. `feat/census-collector` follows, and that is where
the friend grant happens.

## Validation

No markdown linter is available in this environment, so the validation used
equivalent structural checks. The checks covered: code fences balanced, all 5
tables well-formed, no placeholders, no trailing whitespace, heading spacing
correct. All 23 `§N.M` cross-references resolve against the 40 headings. A
mechanical check against source at `711a7a8` verified the analysis surface that
the census depends on: 18 members and 4 enum cardinalities.

Not verified, and not verifiable on a design branch: that the harness counts correctly. That requires the harness to exist and is the first gate of the collector increment.

🤖 Generated with [Claude Code](https://claude.com/claude-code)


---

## #15 — Add end-to-end renderer alpha analysis

state: MERGED  merged: 2026-08-20T09:44:48Z

## Summary

- add the first renderer-level AMUSE alpha-analysis vertical slice.
- dispatch current/base materials through the existing Poiyomi and lilToon
  semantics frontends without introducing an adapter registry.
- compose existing alpha semantics, Unity texture evidence, exact triangle
  classification, and MeshSeparationPlanner into one immutable renderer result.
- preserve unsupported or uncertain submeshes/triangles while allowing
  independent proven-opaque geometry to remain useful.
- fail closed on MaterialPropertyBlock overrides.
- characterize Unity mesh readability and non-readable alpha-texture refusal.
- retain source objects unchanged.

## Architecture

The analysis is Editor-only and read-only:

    Renderer
      → shared Mesh + ordered sharedMaterials
      → MaterialSemantics
      → AlphaSemanticsResolver
      → UnityAlphaFieldEvidence
      → exact TriangleAlphaClassifier
      → MeshSeparationPlanner
      → immutable RendererAlphaAnalysis

Supported renderer types:

- SkinnedMeshRenderer
- MeshRenderer + MeshFilter

Renderer-scoped refusal currently includes:

- unsupported renderer type
- MaterialPropertyBlock present
- missing mesh
- unrepresentable material/submesh slot mapping
- non-triangle topology
- malformed mesh data

The analysis runs no NDMF execution and changes no source.

## Proof behavior

Only:

    TriangleAlphaOutcome.ProvenOpaque

may become an opaque candidate.

The analysis preserves unknown or unsupported information.

Missing/non-finite UV0 counts as missing evidence rather than an automatic
failure:

- constant alpha 1 remains provably opaque.
- constant alpha < 1 remains non-opaque.
- UV-dependent texture samples become Unknown.

## Validation

Baseline:

    666 / 666 EditMode tests passed
    0 failed
    0 skipped

Final:

    695 / 695 EditMode tests passed
    0 failed
    0 skipped
    0 console errors from the final test run

29 tests added.

Measured Unity behavior:

- `Mesh.vertices`, `Mesh.uv`, and `Mesh.GetIndices` all return complete data in
  Editor code for a generated imported mesh with `isReadable == false`.
- `Renderer.HasPropertyBlock()` reports both renderer-wide and
  per-material-index property blocks.

Vertical-slice fixture:

- submesh 0: unsupported semantics → preserved.
- submesh 1:
      opaque ordinals [0, 2]
      preserved ordinal [1]
      Split
- total opaque candidates: 2.

Non-readable texture characterization:

- only the material/submesh depending on the non-readable texture refuses with
  MissingTextureEvidence.
- independent sibling geometry still produces opaque candidates and a useful
  partial plan.

Source immutability test verifies the renderer, shared mesh, material,
texture/import state, pixels, UVs, vertices, and indices remain unchanged.

The pre-existing Analysis → no UnityEditor architecture guard remains green.

## Deliberate limitations

This milestone does not implement:

- MaterialPropertyBlock semantics
- animation/material-swap/effective-state analysis
- non-readable texture recovery
- non-triangle topology support
- material/submesh mismatch support
- per-triangle Unknown diagnostics
- NDMF/build execution
- mesh/material mutation
- UI.

The public development project installs neither vendor shader package. The
integration fixture therefore substitutes only vendor source attestation through
the existing verified-material test seam. It uses genuine Poiyomi semantic
interpretation and does not claim to exercise production vendor dispatch.

## Unchanged core

No changes to:

- MeshSeparationPlanner
- TriangleAlphaClassifier
- ExactUvGeometry
- AlphaSemanticsResolver
- MaterialSemantics
- UnityTextureEvidence
- UnityAlphaFieldEvidence
- Poiyomi/lilToon frontend implementations
- package metadata
- asmdefs
- dependency manifests
- CI.

🤖 Generated with [Claude Code](https://claude.com/claude-code)


---

## #14 — Document reproducible VPM dependency setup

state: MERGED  merged: 2026-08-20T06:44:01Z

## Summary

- document the missing once-per-machine VPM repository setup required to discover
  NDMF.
- document deterministic per-clone VPM dependency restoration before the existing
  NDMF standalone bootstrap.
- align `Packages/.gitignore` with the state produced by the VPM resolver so
  clean-clone restoration does not dirty the repository.
- record the clean-environment reproduction and the setup validation.

## Problem

AMUSE correctly declared:

    nadena.dev.ndmf 1.14.4

but a fresh machine could not discover that package without the bd_ VPM
repository in machine-global VPM settings.

That hidden prerequisite did not travel with Git and was not documented.

## Setup

Once per machine:

1. Install the .NET 8 SDK.
2. Install the official VRChat VPM CLI.
3. Check configured VPM repositories.
4. If `dev.nadena.vpm` is absent, add:

       https://vpm.nadena.dev/vpm.json

Per clone:

1. Run:

       vpm resolve project .

2. Confirm:

       Packages/nadena.dev.ndmf/package.json
           name = nadena.dev.ndmf
           version = 1.14.4

3. Run:

       pwsh -NoProfile -File ./Tools/Bootstrap-NdmfStandalone.ps1

4. Open Unity.

## Clean-environment validation

Validation used:

- .NET SDK 8.0.424
- VRChat VPM CLI 0.1.28
- a fresh clone
- isolated VPM settings/cache via a scratch `XDG_DATA_HOME`.

Observed before repository registration:

    Could not get match for nadena.dev.ndmf 1.14.4
    Could not resolve package nadena.dev.ndmf 1.14.4

After registering `dev.nadena.vpm`:

    nadena.dev.ndmf 1.14.4

resolved successfully.

Two consecutive resolves left:

- `Packages/.gitignore` byte-identical
- `Packages/vpm-manifest.json` byte-identical
- Git status clean.

The existing standalone NDMF bootstrap then:

- succeeded on the first run
- reported already bootstrapped on the second
- left Git clean.

## VPM CLI observation

With the tested CLI version, the unsuccessful pre-setup resolve logged errors but returned process exit code 0.

The documented validation therefore checks the resolved `nadena.dev.ndmf/package.json` rather than trusting exit status alone.

`vpm list repos` also normalized/re-cached machine-global VPM settings during
testing, so repository inspection is not assumed to be byte-level read-only.

## Scope

No changes to:

- NDMF version
- VPM dependency declarations
- Unity manifest or lockfile
- production C#
- tests
- asmdefs
- NDMF standalone bootstrap
- CI.

Resolved package contents remain ignored.

## Validation limitation

Unity MCP was not connected during final validation. The public Unity project
could not be positively identified through its `Application.dataPath`, and the
Editor-level V8 check was not run.

This PR claims no Editor package-resolution or compilation result.

The clean-clone VPM restore and NDMF bootstrap passed independent validation.

🤖 Generated with [Claude Code](https://claude.com/claude-code)


---

## #13 — Make development policy platform agnostic

state: MERGED  merged: 2026-08-19T20:04:11Z

## Summary

- derive the AMUSE repository root dynamically instead of relying on a
  machine-specific checkout path.
- identify the public Unity development project through normalized
  `<repo-root>/Assets`.
- make private-testbed selection independent of filesystem location.
- clarify that historical milestone paths and shell details are records, not
  current development policy.
- document the cross-platform PowerShell 7 requirement for the temporary NDMF
  dependency bootstrap.
- ignore common OS-generated filesystem noise across macOS, Windows, and Linux.

## Motivation

AMUSE development moved from Windows to macOS. Previous agent workflows
implicitly relied on the old Windows checkout path before that move.

The policy now derives repository identity from Git and avoids making any
operating system or machine layout canonical.

## Validation

- repository root resolves through `git rev-parse --show-toplevel`.
- `pwsh` is installed and available on PATH.
- `Tools/Bootstrap-NdmfStandalone.ps1` executes successfully on macOS using
  PowerShell 7.
- bootstrap produces no tracked repository changes.
- `git diff --check` passes.
- only `.gitignore`, `AGENTS.md`, and `README.md` changed.
- no tracked file ends up accidentally ignored.
- no Unity/product/package/test files changed.

No Unity test run was required because this branch changes development policy
and documentation only.

🤖 Generated with [Claude Code](https://claude.com/claude-code)

---

## #12 — Add exact Unity texture alpha evidence

state: MERGED  merged: 2026-08-19T16:42:31Z

## Summary

- implement the existing `AlphaFieldProvider` seam for supported Unity `Texture2D` state.
- convert imported texture alpha directly into the existing immutable `AlphaTextureData` consumed by the exact triangle-alpha classifier.
- keep Unity asset access outside the host-neutral Analysis layer.
- add conservative format/state refusal boundaries and end-to-end resolver/classifier coverage.

## Architecture

The milestone adds one host-side production type:

```
Unity Texture2D
    → UnityAlphaFieldEvidence
    → AlphaFieldProvider
    → AlphaSemanticsResolver
    → AlphaTextureData
    → TriangleAlphaClassifier
```

No new semantic IR or evidence type was required.

There are no changes to:

- `MaterialSemantics`
- `AlphaSemanticsResolver`
- `AlphaTextureData`
- `TriangleAlphaClassifier`
- `UnityTextureEvidence`
- Poiyomi or lilToon semantics.

`UnityTextureEvidence` remains at exactly five shared facts.

## Supported v1 domain

The producer positively supports:

- `RGBA32`
- `ARGB32`
- `Alpha8`
- `RGB24`

The contract is predicate equivalence:

```
evidence byte == 255
    iff
effective mip-0 shader alpha == 1
```

rather than arbitrary alpha-value identity.

The design work measured each admitted format against actual shader sampling.

## Conservative refusal

The producer refuses unsupported or unproven states, including:

- non-readable textures
- mipmapped textures
- compressed formats such as DXT5, BC7 and DXT5Crunched
- float/half alpha formats through the `Color32` route
- ARGB4444
- non-`Texture2D` resources
- unknown texture identities
- non-alpha channels.

Measurements found real false-opaque hazards:

- DXT5 / BC7 / DXT5Crunched can turn alpha 254 into 255.
- RGBAHalf `GetPixels32` can turn a value below 1 into byte 255.

Those cases therefore fail closed.

## Import behavior

The producer reads the resulting imported `Texture2D` and does not inspect `TextureImporter` state.

Tests establish that it follows import results for settings that change the
field. The settings include `alphaSource` and resize behavior. It avoids
unnecessary import-history reasoning.

No importer mutation occurs.

## Integration

The integration test exercises:

```
imported Texture2D
    → UnityAlphaFieldEvidence
    → AlphaSemanticsResolver
    → TriangleAlphaClassifier
```

with asymmetric alpha data and real geometry-sensitive outcomes.

It deliberately does not introduce `Material`, shader frontends, or `MeshSeparationPlanner` into this milestone.

## Validation

Baseline:

- 629 / 629 EditMode tests

Implementation:

- Task 2 observed the intended compile RED before the Host producer existed
- initial positive scope: 3 / 3
- complete new host/integration scope: 37 / 37

Final:

- 666 / 666 EditMode tests
- 0 failed
- 0 skipped unexpectedly
- 0 Unity Console errors
- 0 Unity Console warnings

## Known limitation

This first implementation requires `Texture2D.isReadable`.

Readability is disabled for many ordinary imported avatar textures. This
milestone therefore intentionally prioritizes exactness and architectural
validation over broad real-world coverage.

This milestone defers non-readable texture evidence rather than approximating it.

🤖 Generated with [Claude Code](https://claude.com/claude-code)


---

## #11 — Characterize shader semantic frontend behavior

state: MERGED  merged: 2026-08-19T12:06:03Z

## Summary

- compare the merged Poiyomi and lilToon semantic frontends and publish a
  durable architecture/support matrix.
- add cross-adapter characterization for sampler blast radius, uncertainty
  monotonicity, shared Unity texture evidence, neutral-claim gating, and
  irrelevant-state invariance.
- verify Poiyomi Toon 9.3.64 against the exact pinned upstream source and
  confirm a Normal false-positive path.
- conservatively fix that path by proving the existing Normal writer gates
  before returning `Complete(Unmodified)`.
- preserve current semantic-core boundaries without introducing an adapter
  framework, schema, registry, or new IR forms.

## Correctness finding

`PoiyomiMaterialSemantics.InterpretNormal` previously returned
`Complete(Unmodified)` when `_BumpMap` had no assigned value, before evaluating
its existing `NormalFeatureGates`.

Pinned Poiyomi 9.3.64 source verification confirmed `_DetailEnabled` can
independently perturb the tangent-space normal without reading `_BumpMap`.

The regression suite observed the expected pre-fix failure before the
production change.

The fix only moves the existing gate validation before the neutral return.
It does not change the gate list, equations, constants, or diagnostics.

This is strictly conservative:

    previously Complete(Unmodified)
        → Unknown

for newly gated cases.

The characterization RED produced eight failures because the old return
bypassed all eight existing AMUSE safety gates. This does not mean all eight
shader features independently affect Normal in the supported unlocked
Poiyomi variant. `_DetailEnabled` is the source-proven correctness case.

## Architecture findings

- `MaterialSemantics` remains sufficient for both implemented frontends.
- the five `UnityTextureEvidence` facts remain genuinely shared.
- shader attestation remains intentionally shader-specific.
- sampler ownership has materially different output invalidation behavior
  between Poiyomi and lilToon.
- same-sample `rgb × alpha` and coverage-versus-alpha-value remain documented
  semantic pressure rather than speculative IR additions.
- the milestone introduced no adapter interface, registry, schema, expression
  DAG, shared diagnostic framework, or third shader frontend.

## Validation

Baseline before milestone:

- 553 / 553 EditMode tests passed

Neutral-claim regression before fix:

- 44 run
- 36 passed
- 8 failed
- all failures were Poiyomi Normal neutral-claim cases

After the conservative fix:

- neutral-claim characterization: 44 / 44 passed
- post-fix full suite: 621 / 621 passed

Final milestone suite:

- 629 / 629 passed
- 0 failed
- 0 skipped
- 0 Console errors
- 0 Console warnings

## Follow-up

The repository currently has no compile/EditMode CI workflow.

Establishing that gate is intentionally outside this PR and is the recommended
next infrastructure milestone before another major semantic/frontend
expansion.

🤖 Generated with [Claude Code](https://claude.com/claude-code)


---

## #10 — Add lilToon material semantics adapter

state: MERGED  merged: 2026-08-19T10:02:43Z

## Summary

- add conservative lilToon 2.3.4 → MaterialSemantics interpretation for the canonical opaque `lilToon` shader.
- add source attestation for the lilToon generated-shader model. The attestation
  uses measured canonical shader/pass digests, the complete lilToon-owned
  include-tree digest, live `LIL_RENDER`, and compile-time feature evidence.
- extract five proven shared Unity texture-evidence operations and repoint Poiyomi to them without changing Poiyomi semantics.
- support independently proven BaseColor, Alpha, Normal, and Emission outputs while returning Unknown for unsupported or unprovable behavior.
- retain all broader shader-schema, registry, expression-graph, and portability work as future architecture rather than scaffolding it here.

## Safety / scope

- `MaterialSemantics` unchanged
- `Editor/Analysis` unchanged
- no NDMF integration changes
- no adapter registry/interface or shader schema
- no family-wide lilToon support table
- only the attested canonical opaque lilToon target is supported
- unsupported, modified, stripped, or unprovable behavior fails closed.

## Validation

Focused suites:

- LilToonAttestationTests: 56/56
- LilToonAdversarialTests boundary phase: 5/5
- LilToonBaseColorTests: 20/20
- LilToonAlphaTests: 5/5
- LilToonNormalTests: 24/24
- LilToonEmissionTests: 34/34
- LilToonAdversarialTests final: 12/12

Full EditMode suite:

- 553/553 passed
- 0 failed
- 0 skipped
- 0 Console errors
- 0 Console warnings

Task 0 also validated attestation against jp.lilxyzw.liltoon 2.3.4 in a real
isolated Unity 2022.3.22f1 installation. The run used both default and
stripped shader-generation settings.

## Known limitation

The public AMUSE test project intentionally does not install lilToon. The
isolated Task 0 real-package measurement validates the live
`GatherSourceEvidence` filesystem/package path. No in-repository automated
integration test covers that path. The repository suite covers
canonicalization, identity verification, failure cases, render-mode scanning,
feature scanning, and semantic interpretation deterministically.

🤖 Generated with [Claude Code](https://claude.com/claude-code)


---

## #9 — Add alpha semantics resolver

state: MERGED  merged: 2026-08-17T20:53:45Z

Bridges normalized `MaterialSemantics.Alpha` to the existing exact triangle alpha classifier with one shader-independent, Editor-only decision function.
`Resolve` returns exactly one of: a uniform triangle outcome, an exact classifier configuration, or a named material-level refusal.

## What it does

- **Shader-independent bridge.** `AlphaSemanticsResolver.Resolve(SemanticOutput<ScalarSemanticValue>, AlphaFieldProvider)` switches exhaustively over the closed Alpha vocabulary. It names no shader, property, package, version, Unity object type, mesh concept, or render state. A future lilToon adapter therefore uses the same path unchanged.
- **Constant fast paths.** A constant alpha is independent of UV, geometry, sampling, and texture identity. It therefore bypasses the classifier entirely. Exactly `1` is `ProvenOpaque`, anything below `1` is `MustRemainTransparent`, and neither consults evidence.
- **Exact multiplier lemma.** For `alpha = s * k` with the evidence contract bounding `s` to `[0, 1]` (a bound bilinear filtering preserves, being a convex combination):
  - `k == 1` preserves the `s == 1` predicate of the classifier itself — delegate unchanged.
  - `k < 1` (including `0` and negatives) forces `alpha <= max(0, k) < 1` at every reachable sample. The range attestation alone then proves the outcome `MustRemainTransparent`, without reading one texel.
  - `k > 1` would require proving `s == 1/k`, which the classifier cannot express. It would also leave `alpha` above `1`, whose opacity meaning the semantic model deliberately does not define — refused.

  So multiplication needs no classifier change, no input extension, no texture baking, and no floating-point tolerance. Comparisons against `1` are exact, with no epsilon.
- **Predicate-equivalent scalar evidence.** `AlphaFieldProvider` is a lookup over `(TextureSourceId, TextureChannel)`. Its contract states the proof that the classifier consumes, not a storage format. The proof needs a base-level texel domain, read bottom-to-top, with every effective per-texel scalar finite and within `[0, 1]`. Byte `255` marks exactly the texels whose value is exactly `1`, and every other byte stays strictly below `1`. A source therefore need not literally be an uncompressed 8-bit `b/255` field. Monotone hardware decode such as sRGB leaves the partition and the predicate unchanged.
- **Arbitrary RGBA channels.** The classifier never interpreted its bytes as "the alpha channel". Red/Green/Blue/Alpha therefore all work through the same normalized evidence with **zero** classifier change.
- **Exhaustive sampling mapping.** Semantic `Point`/`Bilinear` and `Clamp`/`Repeat` map onto `AlphaFilterMode`/`AlphaWrapMode` explicitly. A fail-closed default arm keeps a future semantic mode out of a wrong classifier mode.

## Conservative restrictions

- **UV0 with identity scale/offset only.** The classifier proof is exact relative to the UV floats the caller hands it. It has no transform input. Pre-computing `uv * Scale + Offset` in `float` would make it prove an exact statement about a domain off by up to one ulp. Non-identity transforms and non-zero UV sets therefore refuse. The deferred extension must prove exactness with exact dyadic/rational arithmetic — wider floating point is not such a proof.
- **Refusal vs. `Unknown`.** Material-level refusals (`SemanticsUnknown`, `UnsupportedMultiplier`, `UnsupportedUvMapping`, `UnsupportedSampling`, `MissingTextureEvidence`) stay distinct from per-triangle classifier outcome `TriangleAlphaOutcome.Unknown`. A refusal yields no triangle outcome at all — `Classify` throws — so no one can misread it as a result. The `AlphaResolution` constructor makes an invalid shape unrepresentable.

## Unchanged

`MaterialSemantics`, `TriangleAlphaClassifier`, `ExactUvGeometry`, `MeshSeparationPlanner`, the Poiyomi adapter, every existing test, the reference fixtures, asmdefs, package metadata, manifests, workflows, and project settings. Two files added, none modified.

## Explicitly deferred

The **live Unity texture-evidence producer** is a separate milestone. The
resolver is complete and fully tested but has no production data source until
host extraction lands. The classifier and separation planner followed the same
sequencing. A future producer may normalize trustworthy source evidence into
`AlphaTextureData` for analysis. That is proof normalization, never texture
baking, and it never reaches the rendered result.

## Validation

- Resolver tests: **39/39 passed**
- Full EditMode suite: **385/385 passed**, 0 failed, **0 skipped**
- Unity Console: **0 errors, 0 warnings**
- Every stage was driven red-first and observed failing for the intended reason before production code.
- Delegation-equivalence tests assert that the per-triangle result of the resolver equals a direct `TriangleAlphaClassifier.Classify` call across all four sampling combinations. That pins the resolver as a configurer, not a second classifier. Classifier `Unknown` for degenerate geometry, absent UV, and the workload cap passes through unchanged.
- Run against the public development project only (`com.alrauna.amuse@0.0.1`, Unity 2022.3.22f1). The private avatar testbed was never connected, inspected, or modified.

Design: `docs/superpowers/specs/2026-08-17-alpha-semantics-resolver-design.md`
Plan: `docs/superpowers/plans/2026-08-17-alpha-semantics-resolver.md`

🤖 Generated with [Claude Code](https://claude.com/claude-code)


---

## #8 — Add Poiyomi material semantics adapter

state: MERGED  merged: 2026-08-17T00:11:18Z

## Summary

Editor-only **Poiyomi material semantics adapter** for AMUSE. Given a material with a shader attested as the pinned **Poiyomi Toon 9.3.64**, it produces normalized, proof-first semantics. The four outputs are **BaseColor, Alpha, Emission, Normal**. The attestation rests on exact shader-asset GUID, package name/version, and normalized source hash.

The adapter is conservative by construction:

- Anything it cannot prove behavior-preserving **fails closed to `Unknown`** with an **output-local diagnostic**. The refusal invalidates the four outputs independently.
- Uncertainty never widens optimization — the adapter emits only proven forms, and refusal is always the safe default.
- A single unsupported shared `_MainTex` sampler invalidates every dependent sample while constant outputs survive. A missing `_MainTex` invents no sampler.
- BaseColor and Emission require linear-light color-space evidence and fail closed under Gamma. Emission-zero is color-space-independent and stays complete.

## Tests

- Focused EditMode tests cover source attestation/identity and texture evidence (identity, UV, sampling, color space). More cover BaseColor, Alpha, Emission, Normal, and adversarial cross-output behavior.
- An **original, schema-only fixture shader** stands in for the Poiyomi property surface so tests run without installing Poiyomi. This repository vendors **no Poiyomi source**.
- Focused Poiyomi suite: **232/232 passing** (0 failed, 0 skipped). Full EditMode suite green. Console clean (0 errors, 0 warnings).

## Repository tooling

Adds **`CLAUDE.md`** containing only `@AGENTS.md`. This is repository agent-harness configuration. It points Claude Code at the existing canonical **`AGENTS.md`** policy instead of duplicating it. The harness then automatically inherits the project agent rules.

## Scope / safety notes

- `.gitignore` is intentionally **not** part of this PR.
- The distributable package stays independently usable. Package metadata gains no Poiyomi or MCP dependency.
- Known coverage limit: locked/optimized Poiyomi materials carry a renamed shader identity. The adapter refuses them (a safe false-negative) while they stay locked.

🤖 Generated with [Claude Code](https://claude.com/claude-code)


---

## #7 — Add normalized material semantics core

state: MERGED  merged: 2026-08-16T07:48:23Z

## Summary

- document the approved fixed-field material semantics design and TDD implementation plan
- add immutable texture sampling, color, scalar, normal, per-output knowledge, and resolved material semantic values
- add wrong-kind, conservative unknown/default, structural equality, and adversarial boundary coverage

## Scope

This milestone is intentionally limited to `BaseColor`, `Alpha`, `Emission`, and narrow `Normal` semantics using constant, texture, and texture-times-constant forms. It does not add shader adapters, render state, transformation capabilities, an expression DAG, classifier/planner changes, or a new assembly/package.

## Validation

- Full EditMode suite: 114 passed, 0 failed, 0 skipped
- Material semantics tests: 24 passed, 0 failed, 0 skipped
- Explicit classifier/planner/reference-fixture subset: 89 passed, 0 failed, 0 skipped
- Unity Console: no errors or warnings
- Forbidden-coupling, whitespace, metadata GUID, documentation, and Git-scope checks passed
- Private Unity testbed was not selected or modified


---

## #6 — Rebrand project as AMUSE

state: MERGED  merged: 2026-08-16T05:43:05Z

## Summary

- pivot the project identity to **AMUSE — Alrauna's Material Understanding & Simplification Engine**
- migrate the embedded package to `com.alrauna.amuse` and rename production/test assemblies and namespaces to `Alrauna.Amuse.*`
- preserve the existing exact alpha classifier, fixtures, and mesh-separation planner as behaviorally unchanged foundational subsystems
- update agent policy, README, architecture vision, Unity project branding, package metadata, and the VPM listing banner
- retain package version `0.0.1`

## Why

The previous identity described only the alpha-material optimization subsystem. AMUSE is the broader proof-first material-understanding and simplification project. The implemented classifier and planner remain valid early components of that architecture.

## Impact

- `com.alrauna.amuse` is a distinct VPM package ID, so installations of the previous package ID will not upgrade automatically.
- consumers of the old C# assembly or namespace identities must migrate to `Alrauna.Amuse.*`.
- the GitHub repository rename, `PACKAGE_NAME` repository-variable update, Pages/listing republish, and local remote update remain external follow-up actions.

## Validation

- NDMF bootstrap completed successfully twice and remained idempotent.
- focused EditMode suites passed: fixtures 8/8, classifier 57/57, planner 24/24.
- fresh post-commit full EditMode suite passed 90/90 with 0 failures and 0 skips.
- Unity Console reported 0 warnings/errors after package re-resolution and refresh.
- test discovery reported 91 entries under `Alrauna.Amuse`.
- the migration preserved all 52 tracked Unity GUIDs with no duplicates.
- current-surface stale-identity search returned no matches outside intentionally historical design documents.
- JSON/asmdef parsing and Git patch whitespace checks passed.
- the private Unity avatar testbed was not selected or modified.


---

## #5 — Add immutable separation planning

state: MERGED  merged: 2026-08-16T02:30:37Z

## Summary

- add a pure, immutable separation planner over normalized triangle topology and classifier outcomes
- preserve source-local triangle ordinals, submesh identity, and explicit material-binding provenance
- fail closed on malformed input while keeping `Unknown` and `MustRemainTransparent` on the transparent side
- document the approved design, invariants, deferred host-adapter responsibilities, and TDD implementation sequence

## Impact

This introduces the non-mutating planning boundary for a later mesh-transformation milestone. It does not create or mutate Unity meshes, materials, renderers, or NDMF state.

## Validation

- planner EditMode tests: 24 passed, 0 failed, 0 skipped
- existing classifier and reference-fixture suites: 65 passed, 0 failed
- complete EditMode suite: 90 passed, 0 failed, 0 skipped
- final Unity Console: 0 errors, 0 warnings
- `git diff --check`
- portable-boundary scan confirmed no Unity, NDMF, renderer, material, Jobs, Burst, or parallel dependencies


---

## #4 — feat: classify exact triangle alpha support

state: MERGED  merged: 2026-08-15T12:55:42Z

## Summary

- add an Editor-only triangle alpha classifier with `ProvenOpaque`, `MustRemainTransparent`, and conservative `Unknown` outcomes
- represent finite float geometry and UV values exactly with canonical dyadics, `BigInteger` lattice coordinates, and rational clipping
- support continuous triangle, line, and point UV domains under Point/Bilinear filtering with Clamp/Repeat wrapping
- bound mixed-texture support enumeration at 65,536 regions while retaining exact uniform-texture fast paths
- add the approved design and implementation plan plus matching Unity metadata

## Exactness and safety

- alpha 255 is the only opaque value. 0-254 remain non-opaque.
- degenerate geometry and intentional missing UV0 return `Unknown`
- malformed or non-finite inputs throw
- Repeat uses mathematical floor division/modulus, including negative exact multiples and adjacent cells
- Bilinear classification uses positive-weight support with exact open boundaries
- no epsilons, sampling grids, `Texture2D`, `Mesh`, NDMF state, or fixture expectations enter production code

## Tests

- direct float-decoder coverage for `+0.0f`, `-0.0f`, a subnormal, a negative value, and an ordinary power of two
- Repeat floor tests for `-period`, one cell below, one cell above, and the last negative cell before zero
- fixture-driven Point/Bilinear and Clamp/Repeat coverage
- collapsed UV point/line, seam, translation, winding, large-offset, workload-cap, alpha-254, and malformed-input cases
- Unity EditMode suite: **66 passed, 0 failed, 0 skipped**
- Unity Console: **0 errors, 0 warnings**

The private Unity avatar testbed was not accessed or modified.


---

## #3 — Add Unity MCP development tooling

state: MERGED  merged: 2026-08-15T11:36:06Z



---

## #2 — Add deterministic reference fixture framework

state: MERGED  merged: 2026-08-15T11:03:41Z

## Summary

- Adds 13 public, deterministic synthetic reference fixtures.
- Separates portable fixture inputs from independent semantic oracles.
- Adds strict fixture-integrity validation.
- Adds disposable, case-local in-memory Unity texture and mesh builders.
- Defines cross-language UV, texture row-order, filtering, wrap, alpha-byte, triangle/index, winding, and continuous closed triangle-domain semantics.
- Keeps mipmaps explicitly out of scope and disables them in constructed textures.
- Intentionally contains no production classifier or optimizer behavior.

## Safety / architecture

- All fixtures are synthetic, minimal, redistributable, deterministic, and human-auditable.
- The fixtures include no private-avatar or private-testbed content.
- Fixture authors write semantic expectations independently and never derive them through Unity sampling.
- The JSON catalogs remain portable by design to a future Blender/Python implementation.
- `Unknown` stays reserved for inputs where no safe supported classification is possible. Known partial transparency remains `MustRemainTransparent`, and integrity validation rejects malformed fixture data.
- Additional uncertainty must never make future optimization more aggressive.

## Validation

- Unity 2022.3.22f1 complete EditMode suite: **9 passed, 0 failed, 0 skipped/inconclusive**.
- Active Unity editor log: **0 C# compiler errors, 0 C# compiler warnings**.
- `git diff --check main...HEAD`: passed.
- Working tree: clean.
- Scope audit: 12 approved changed paths. No package-manifest, dependency, production-package, generated Unity state, CI, release, or listing workflow changes.
- Unity asset audit: all fixture assets have `.meta` partners. 0 duplicate tracked GUIDs and no existing GUID changes.
- The private Unity testbed was neither used nor modified.

## Explicitly deferred

This PR does **not** implement:

- the alpha classifier
- mesh or material transformation
- NDMF optimization passes
- animation or material-state tracing
- shader adapters
- mipmap semantics
- correctness CI.

---

## #1 — Set up NDMF development test environment

state: MERGED  merged: 2026-08-15T03:07:59Z

Summary

Sets up the minimum local development and EditMode test infrastructure needed to start NDMF development.
Declares nadena.dev.ndmf >=1.14.4 <2.0.0-a as a package dependency.
Locks the development project to NDMF 1.14.4.
References NDMF from the package Editor assembly.
Adds a package-local Editor test assembly and one trivial discovery smoke test.

Documents and adds an idempotent bootstrap for the standalone dependencies of NDMF 1.14.4.
Keeps generated dependency DLLs ignored and untracked.

The bootstrap is a temporary workaround for NDMF 1.14.4 packaging its standalone assemblies under Unity-ignored Dependencies~. It verifies the exact package version and layout, then copies the packaged files into a real, ignored Dependencies/ directory. The copy needs no junction or symlink privileges.

Validation

Bootstrap executed twice successfully, and the second run was idempotent.
Exact smoke test: 1 passed, 0 failed, 0 skipped.
Complete EditMode suite: 1 passed, 0 failed, 0 skipped.
Unity smoke, full-suite, and compile-only processes exited successfully.

Compiler errors: 0.
Compiler warnings: 0.
Generated NDMF dependency files remain ignored and contribute zero tracked files.
git diff --check and git diff --cached --check passed.

Scope

Infrastructure only. This PR does not add optimizer behavior, fixtures, avatar transformations, correctness CI, release workflow changes, or private-testbed content.
