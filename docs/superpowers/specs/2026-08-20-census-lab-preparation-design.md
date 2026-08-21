# Census Lab Preparation — Design

Branch: `research/census-lab-preparation`
Base commit: `a2edf38` (`origin/main`, merge of PR #4)
Date: 2026-08-20
Status: Design approved. Implementation scope reduced to the calibration gate. No code
produced on this branch, no AMUSE production change, no Lab change, no Unity MCP use.

## 1. Purpose and scope

Prepare a private research environment capable of running AMUSE analysis against future
consented avatar fixtures, without placing private data or private workflows in this
repository.

This branch does **not** collect avatars, does not run a census, and does not build the
export or invocation machinery a census run will eventually need. It settles the
environment's architecture, states the privacy boundary, and specifies one piece of
implementable work: the **vendor reachability calibration gate**.

The prior branch's finding stands and is the premise here: publicly releasable research
tooling belongs in this repository; only private data lives in the Lab. See
`2026-08-20-avatar-census-harness-preparation-design.md` §2 and §12.2. Nothing in that
document is reopened.

### 1.1 The Lab replaces the former private testbed

AMUSE-Census-Lab is not a second private project alongside the avatar testbed AGENTS.md
describes. It **replaces** it. The former testbed no longer exists.

Consequently AGENTS.md's *Private Unity avatar testbed* section now describes the Lab, and
its rules apply unchanged: read-only inspection preferred, no persistent mutation without
explicit task scope, never a source of publishable fixtures, never a substitute for
deterministic repository tests. A wording pass aligning that section's vocabulary with the
Lab's name is worth doing and is **out of scope here** — it is a policy edit and belongs on
its own `docs/` or `chore/` branch.

### 1.2 The Lab is never identified by a path

The Lab lives wherever its operator put it. No absolute path, drive letter, or home
directory for it appears in this document, in the research package, in tests, or in any
policy file, per AGENTS.md's repository-boundaries rule.

The Lab is identified positively, at run time, the same way the public project is: by its
Unity `Application.dataPath`. The public development project is the Unity project whose
data path equals `<repo-root>/Assets` after normalization; **any other reachable instance is
not the public project**, and that is the whole of the test. Identifying the Lab by
elimination is sufficient and requires inspecting nothing about it.

## 2. Observed state

Recorded on 2026-08-20 by read-only filesystem inspection. No Unity Editor was opened, no
Unity MCP call was made, and nothing in the Lab was written, moved, or deleted.

### 2.1 The repository

| Fact | Value |
|---|---|
| Working tree at branch start | Clean |
| `origin/main` | `a2edf38` |
| Research package | `Packages/com.alrauna.amuse.research`, three assemblies |
| Collect → Anonymize → Aggregate | All three implemented, all unit-tested |
| Arithmetic cross-check against `MeshSeparationPlan` | Implemented, `RendererObservationBuilder.cs:166` |
| Serialization of any tier | **Absent** |
| Invocation surface for the collector | **Absent** |
| Layer 3 asset-manifest mutation proof | **Absent** |
| Vendor reachability gate | **Absent** |

### 2.2 The Lab

| Fact | Value |
|---|---|
| Version control | **None.** No `.git` directory |
| Unity version | 2022.3.22f1, identical to the public project |
| `Assets/` | Effectively empty: an LTCGI gizmo, an LTCGI shader include, a `csc.rsp`, a `.gitkeep`. **No avatars present** |
| AMUSE reference | `com.alrauna.amuse` as a machine-local `file:` dependency on the working tree |
| Research package reference | **Absent** |
| VPM packages | 24, including VRCFury, Modular Avatar, AudioLink, LTCGI, av3emulator, Gesture Manager, Pumkin, and four optimizers |
| Poiyomi | `com.poiyomi.toon` 9.3.64 |
| lilToon | `jp.lilxyzw.liltoon` 2.3.4 |
| Host toolchain churn | `com.unity.toolchain.macos-arm64-linux-x86_64` present in the Lab manifest |

The absent research-package reference is the one hard blocker: the collector's code is not
loaded in the Lab at all. It is a one-line Lab-side configuration change and requires no
repository change.

The host toolchain entry is the exact churn AGENTS.md rules out of *this* repository. In the
Lab it is harmless and stays: the Lab is single-machine and disposable, and the rule exists
to protect contributors on other hosts, of whom the Lab has none.

## 3. Determinations

### 3.1 Standalone Unity project only

Yes. The Lab is a Unity project and nothing else. It is not a workspace, not a monorepo
member, and not a second checkout of this repository.

### 3.2 No git repository

The Lab holds private avatars and purchased vendor packages. A repository there offers
recovery for something designed to be disposable, while creating a redistribution hazard the
moment a remote is added.

Reproducibility is served better by **per-run provenance** than by history: each run copies
`Packages/vpm-manifest.json`, `ProjectSettings/ProjectVersion.txt`, and the AMUSE commit SHA
into its own output directory. That provenance travels with the run and survives the Lab's
deletion, which Lab-local history would not.

This also collapses the "what may be committed" question to a single invariant with no
ignore file to maintain: **the Lab commits nothing, because the Lab is not a repository.**

### 3.3 No source code in the Lab

The Lab contains project configuration and private data. No C#, no editor scripts, no
runner, no committed scene of tooling. Every executable line lives in this repository, in
`Packages/com.alrauna.amuse.research`, and reaches the Lab through the local package
reference.

The test of correct placement is unchanged: if deleting the Lab would lose anything other
than private data and resolved third-party packages, something is in the wrong place.

### 3.4 The package set stays, and becomes provenance

The Lab's 24 packages are kept.

Deleting them was considered and rejected on evidence rather than taste:

- **Installed optimizers do not perturb the measurement.** The collector reads `sharedMesh`
  and `sharedMaterials` on a scene object in Edit mode and never runs a build, so VRCFury,
  Modular Avatar, and the four optimizers are inert unless invoked.
- **A rich package set improves fidelity.** A consented avatar whose shader package is
  missing resolves to a missing-shader material and produces a *false* `SemanticsUnknown`.
  Stripping the Lab would manufacture the exact miscount the census exists to avoid.
- **The Lab is also the integration testbed** (§1.1). A minimal Lab cannot serve that role.

The genuine risk is not breadth but **silent drift**: a routine VPM update between runs
changes what the census means. That is addressed by recording the package set with every run
(§3.2) and by pinning vendor versions in the gate (§5.2), not by deletion.

### 3.5 Recreating the Lab

A documented sequence in the AMUSE README, no script — following the precedent and the
reasoning of `2026-08-19-reproducible-vpm-setup-design.md`, which declined a wrapper around
an already-idempotent official CLI.

```
new Unity project, editor version from the recorded ProjectVersion.txt
restore the recorded vpm-manifest.json      (vpm resolve project .)
add two local package references to a working tree of this repository:
    com.alrauna.amuse
    com.alrauna.amuse.research
install consented avatars
```

The two `file:` references are **derived on the machine doing the recreation** and are never
copied from a recorded value: they encode that machine's checkout location and nothing about
the project. This is the same rule AGENTS.md applies to the repository itself.

Writing that README section is not part of the calibration-gate increment; it is a small
`docs/` change that should accompany the first branch that actually runs the Lab.

## 4. Privacy boundary

### 4.1 Placement

Unchanged from the harness design and restated only for completeness.

| Location | Holds |
|---|---|
| `Packages/com.alrauna.amuse` | Production optimizer |
| `Packages/com.alrauna.amuse.research` | Collection, anonymization, aggregation, schemas, calibration, fixtures |
| `docs/` | Designs, plans, published aggregate reports |
| **The Lab** | Consented avatars, consent records, vendor packages, tier 1 and tier 2 output, any identity mapping |

### 4.2 The output boundary is documented, not enforced in code

A guard making the research package refuse to write outside the Lab was considered and
**rejected**.

The boundary is already architectural without it. The Lab is not a repository, so there is
no commit path out of it; and the repository holds no private data, so there is nothing in
it to leak. Teaching the research package to recognize repository layout would make it aware
of a structure it otherwise never needs to know, to defend against a failure mode that has
not occurred. That is a speculative abstraction, and it trades a real increase in coupling
for a hypothetical protection.

The rule is therefore documented and stated once, here:

> **Tier 1 and tier 2 census output are written inside the Lab and nowhere else. Tier 3, the
> aggregate report, is the only artifact that may leave.**

If a concrete failure mode ever appears — output written into a working tree, a raw record
reaching a diff — the guard becomes justified and can be added then, against a real cause.

### 4.3 What an aggregate report may contain

Governed by §5 and §6.5 of the harness design, unchanged: distributions only, no per-avatar
and no per-renderer rows, and every new category must name the AMUSE decision it informs,
state the smallest population its buckets can hold, and survive the question of what an
adversary holding one avatar learns from it.

One addition specific to the Lab: **a report states its provenance** — Unity version, vendor
package versions, AMUSE commit, and avatar count. A shader-family distribution is
uninterpretable without knowing which shader packages were resolvable when it was measured.
None of those values identifies an avatar or a creator.

## 5. The calibration gate

### 5.1 What the gate is for

A census whose production analysis path cannot reach a success outcome would report
near-total `SemanticsUnknown`. That is a true statement about the environment and a false
statement about AMUSE, and it is indistinguishable from a real result by anyone reading the
report. The gate exists so that this failure aborts the run instead of publishing.

The gate runs through the **production** path — `RendererObservationBuilder.Build(renderer,
hierarchyPath, families)`, the three-argument overload with no semantics provider. It
deliberately does not use the `BaseMaterialSemanticsProvider` seam that
`CollectorSeamCountingTests` uses. That seam proves *counting*; the gate proves
*reachability*. Conflating them is the specific mistake this design prevents.

### 5.2 Vendor attestation is exact-version and source-pinned

Verified in source, and load-bearing for everything below:

| Pin | Location | Value |
|---|---|---|
| Poiyomi package version | `PoiyomiMaterialSemantics.cs:28` | `9.3.64` |
| Poiyomi shader name | `PoiyomiMaterialSemantics.cs:26` | `.poiyomi/Poiyomi Toon` |
| lilToon package version | `LilToonSourceAttestation.cs:221` | `2.3.4` |
| lilToon shader name | `LilToonSourceAttestation.cs:214` | `lilToon` |

Attestation also compares a normalized source hash and a canonical asset GUID, so it fails
closed on any modified or repackaged shader.

The Lab installs exactly these two versions today. That is fortunate rather than
guaranteed, and it is why the version pin is the gate's first case: an ordinary VPM update
in the Lab silently turns every subsequent census into a measurement of the version
mismatch.

### 5.3 The cases

Cases 2 through 5 each build one `MeshRenderer` in a temporary scene, run the production
path, and assert the observed outcome. Cases 1 and 6 build no renderer: they interrogate the
installed packages.

The six form one suite with two operative halves. In the Lab, where the vendor packages are
installed, cases 1 through 5 assert and case 6 does not apply. In public CI, where no vendor
package is installed, case 6 is the operative one and cases 2 through 5 assert nothing — see
§5.5.

| # | Case | Construction | Claim |
|---|---|---|---|
| 1 | Version pin | Read the installed vendor `PackageInfo` version; compare to the pinned constant | Vendor drift fails loudly rather than silently |
| 2 | `ProvenOpaque` | Default vendor material, opaque, no alpha texture | The success path is reachable through production |
| 3 | `MissingTextureEvidence` | Vendor material whose alpha binds a runtime `Texture2D` that is not a project asset, so no `TextureImporter` exists | "Understood shader, unseen texture" is distinguishable |
| 4 | `MustRemainTransparent` | Vendor material with `_Color` alpha set below 1 and no alpha texture | The transparent path is reachable through production |
| 5 | `SemanticsUnknown` via lock | Vendor material with `_ShaderOptimizerEnabled` set to 1 | Characterizes the blind spot recorded in §6 |
| 6 | Vendor absent | Probe finds no attested family | Public CI passes honestly; a census run aborts |

`Unknown` at triangle level is not given a case of its own. It is the outcome of cases 3 and
5 and is already covered in public CI by the existing refusal calibration tests; a separate
case would assert the same thing a third time.

### 5.4 Two choices that keep the gate cheap and non-mutating

**No fixture asset is needed at all.** An earlier revision of this design specified a
committed 4×4 alpha PNG for case 4. Reading the resolver removed the need for it:
`AlphaSemanticsResolver.cs:326` classifies any *constant* alpha below 1 as
`MustRemainTransparent` outright, with no texture involved, and
`UnityTextureEvidence.TryProveSampledAlphaIsOne` proves opacity from importer metadata rather
than from pixels — so a texture would not have been sampled for its texels anyway. Setting a
vendor material's `_Color` alpha below 1 reaches the outcome directly.

Every case therefore builds its material in memory. **The gate imports nothing, commits no
binary asset, and writes nothing to either project.**

**Case 5 sets a material property; it never runs the shader locker.** Poiyomi's lock is read
from the material — `PoiyomiMaterialSemantics.cs:1011-1013` derives it from
`material.GetFloat("_ShaderOptimizerEnabled") != 0f`, and
`PoiyomiMaterialSemantics.cs:929` rejects on it before any source check. Setting that float
on an in-memory material reproduces the rejection exactly, while generating no shader asset
and writing nothing to the Lab. Invoking the real locker would be a persistent testbed
mutation for no additional evidence.

### 5.5 Absence must not be able to masquerade as success

The gate carries two claims that must not collapse into one:

1. *If* an attested vendor family is installed, the production path reaches its success
   outcomes.
2. A census run requires at least one attested vendor family to be installed.

Claim 1 is the test. Claim 2 is a run precondition and belongs to the runner branch, which
does not exist yet.

`Assert.Ignore` is rejected as the absence behavior: an ignored test in the Lab — where a
vendor package might genuinely have gone missing — reports a pass-shaped result for a
condition that should abort a census. The probe instead returns a value, the test asserts
the conditional, and absence is reported rather than skipped.

### 5.6 Expected outcomes are characterized, not predicted

This design does not state what a default Poiyomi material or a default lilToon material
returns from the production path. Predicting it would be guessing about a vendor shader's
default property state, and a gate whose expectations were guessed is worth nothing.

The plan's first task therefore **observes** the outcomes in the Lab and pins them, in the
manner of the existing `Tests/Editor/Semantics/Characterization` suite. If an outcome
differs from the table in §5.3 — for instance if a default vendor material does not reach
`ProvenOpaque` — that is a finding to report, not a defect to code around, and it is exactly
the kind of thing that must surface before any avatar is measured rather than after.

### 5.7 Where the probe lives

In the research **test** assembly, for now.

Its eventual second consumer is the census runner's abort precondition, which does not
exist. Placing it in the production assembly today would create a type whose only caller is
a test — the precise shape the collector design rejected at its §3.1.1 — in anticipation of
a consumer that may be designed differently when it arrives. Moving one `internal static`
class into the production assembly later costs one file move and breaks no API.

## 6. Deferred investigation: attestation failures are indistinguishable

**Recorded, not solved. No implementation, no attestation change, no new census category on
this branch or its increment.**

### 6.1 The finding

Poiyomi rejects locked materials outright: `PoiyomiMaterialSemantics.cs:929` returns false
on `evidence.IsLocked` before any source or schema check. Locking is the normal state for a
distributed avatar, because the shader optimizer is part of the standard upload workflow.

A locked material's shader is also renamed by the locker, so it fails the research
collector's attestation trial and is anonymized into `UnknownFamily-A` — the same bucket a
genuinely unrecognized third-party shader lands in.

The current census therefore **cannot distinguish** three materially different situations:

1. a shader family AMUSE has no adapter for;
2. a supported vendor shader whose material is locked;
3. any other attestation failure — version mismatch, modified source, missing package
   evidence, unreadable shader asset.

A fourth candidate is suspected but **unverified**: lilToon attests the shader name `lilToon`
exactly (`LilToonSourceAttestation.cs:214`), so lilToon materials using a differently-named
transparent or preset variant may also land in the unattested bucket. This has not been
checked and is not asserted; the gate is the cheapest place to find out.

### 6.2 Why it matters, and why it is deferred

The headline census number — the share of avatar geometry AMUSE cannot prove opaque — would
be read as *"AMUSE needs more shader adapters."* If locking dominates the unattested bucket,
the true reading is *"AMUSE needs to handle locked materials,"* which is an entirely
different roadmap.

It is deferred because acting on it now would mean either changing attestation behavior, or
adding a census category, ahead of any evidence about the real distribution. Both are
premature: the safety rule that uncertainty must never increase aggression applies to
roadmap decisions as much as to transforms.

### 6.3 What is preserved

This section is the artifact. Case 5 of the gate (§5.3) demonstrates mechanism 2 concretely,
so the finding rests on observed behavior rather than on this document's assertion, while
implementing nothing and changing no production code.

Anyone interpreting a future census must read the unattested-family share as an **upper
bound on unsupported shader families**, never as a measurement of them.

## 7. Workflow, end to end

Steps 1, 2, and 6 are operator procedure. Steps 4 and 5 need machinery that does not exist
(§8). Only step 3 is implemented by this branch's increment.

1. **Record consent** before any avatar file enters the Lab.
2. **Install the avatar** in the Lab, with the packages it requires.
3. **Run the calibration gate.** A failure aborts here, before anything is measured.
4. **Collect → Anonymize → Aggregate**, in memory, in Edit mode only.
5. **Write tier 3 plus provenance**; tier 1 and tier 2 remain in the Lab.
6. **Remove raw fixtures** when the run is complete.

## 8. Not built, and deliberately so

| Missing piece | Why it waits |
|---|---|
| Tier 1/2/3 serialization | Output format and bucket boundaries must be chosen against real distributions, per harness design §6.5. Serializing before the gate proves counting works on vendor materials risks pinning a schema to a broken measurement. `null`-versus-zero must survive whatever format is chosen — the most likely defect in the whole system |
| Run invocation surface | Nothing to invoke until there is output to produce |
| Layer 3 asset-manifest proof | A run-level obligation with no run to attach to |
| Consent record format | Procedure, not code. Belongs with the first real cohort |
| Minimum avatar diversity threshold | Undefinable before the population is known. A census over three avatars from one creator measures that creator |
| README recreation section | A small `docs/` change belonging to the first branch that actually runs the Lab |

## 9. Constraints on the increment

The gate must not require any of the following. Each is a stop condition: halt and return for
review rather than proceed.

- any change to AMUSE analysis behavior, a result object, a shader adapter, or an evidence
  provider;
- any change to attestation, including relaxing it for locked materials;
- any public API promotion in `com.alrauna.amuse`;
- any visibility change beyond the two `InternalsVisibleTo` grants already present;
- a new census category, field, or schema change;
- telemetry, networking, cloud reporting, automatic avatar discovery, or a persistent
  analytics store;
- private fixture handling in either AMUSE package;
- any persistent mutation of the Lab — no saved scene, no generated shader, no imported
  asset, no project settings change;
- an options object, registry, or provider framework growing out of what should be a probe
  and a set of test cases.

## 10. Validation of this branch

Stated as observed, not as intended.

**Verified by reading source and the filesystem:**

- the vendor version pins, shader names, and the locked-material rejection, at the line
  references given in §5.2 and §6.1;
- that Collect, Anonymize, and Aggregate exist and that the `MeshSeparationPlan` arithmetic
  cross-check is implemented at `RendererObservationBuilder.cs:166`;
- that no serialization, invocation surface, asset manifest, or reachability gate exists in
  the research package;
- that the Lab has no `.git`, holds no avatars, runs Unity 2022.3.22f1, installs Poiyomi
  9.3.64 and lilToon 2.3.4, and does **not** reference the research package.

**Not verified, and not verifiable on this branch:**

- what the production path actually returns for a default vendor material. This is §5.6's
  first task and requires the Lab;
- whether lilToon's transparent variants are attested (§6.1). Suspected, unasserted;
- that the gate passes. The gate does not exist yet.

**Unity MCP:** not used at any point on this branch. **The Lab:** inspected read-only via the
filesystem; not opened, not modified, not written to.

## 11. Decisions summary

| # | Decision |
|---|---|
| 1 | The Lab replaces the former private testbed; AGENTS.md's testbed rules apply to it unchanged |
| 2 | Standalone Unity project, no git repository, no source code |
| 3 | The 24-package set is kept; drift is controlled by per-run provenance, not by deletion |
| 4 | Recreation is a documented sequence, not a script; `file:` paths are derived per machine |
| 5 | The output boundary is documented, **not** enforced by a repository-layout guard |
| 6 | The gate runs the production path, never the semantics seam |
| 7 | Six cases, every material built in memory; no fixture asset; the lock case sets a property and never runs the locker |
| 8 | Vendor absence is reported, never `Assert.Ignore`d |
| 9 | Expected outcomes are characterized in the Lab, not predicted here |
| 10 | The probe lives in the test assembly until a second consumer exists |
| 11 | Indistinguishable attestation failures are recorded as a deferred investigation; nothing is implemented and attestation is unchanged |
