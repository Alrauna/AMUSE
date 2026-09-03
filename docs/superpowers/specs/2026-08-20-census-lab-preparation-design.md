# Census Lab Preparation — Design

Branch: `research/census-lab-preparation`
Base commit: `a2edf38` (`origin/main`, merge of PR #4)
Date: 2026-08-20
Status: Design approved. Implementation scope: only the calibration gate. This branch produced no code, no AMUSE production change, no Lab change, and no Unity MCP use.

## 1. Purpose and scope

Prepare a private research environment that can run AMUSE analysis against future consented avatar fixtures. Keep private data and private workflows out of this repository.

This branch does not collect avatars and does not run a census. It also does not build the export or invocation machinery that a census run will need later. It defines the architecture of the environment and states the privacy boundary. It also specifies one piece of work that is ready for implementation: the **vendor reachability calibration gate**.

The finding from the prior branch stands and is the premise here: publicly releasable research tooling belongs in this repository. Only private data lives in the Lab. See `2026-08-20-avatar-census-harness-preparation-design.md` §2 and §12.2. Nothing in that document is reopened.

### 1.1 The Lab replaces the former private testbed

AMUSE-Census-Lab is not a second private project alongside the avatar testbed that AGENTS.md describes. It **replaces** the testbed. The former testbed no longer exists.

Thus the *Private Unity avatar testbed* section of AGENTS.md now describes the Lab. Its rules apply unchanged: read-only inspection preferred, no persistent mutation without explicit task scope, never a source of publishable fixtures, never a substitute for deterministic repository tests. A wording pass that aligns the vocabulary of that section with the Lab name is worth doing. It is **out of scope here**: it is a policy edit, and it belongs on its own `docs/` or `chore/` branch.

### 1.2 The Lab is never identified by a path

The Lab lives wherever its operator put it. No absolute path, drive letter, or home directory for the Lab appears in this document, in the research package, in tests, or in any policy file. This follows the repository-boundaries rule of AGENTS.md.

The Lab is identified positively at run time, the same way as the public project: by its Unity `Application.dataPath`. The public development project is the Unity project whose data path equals `<repo-root>/Assets` after normalization. **Any other reachable instance is not the public project**, and that is the whole of the test. Identification by elimination is enough, and it never examines the Lab itself.

## 2. Observed state

Recorded on 2026-08-20 by read-only filesystem inspection. No Unity Editor was opened, no Unity MCP call was made, and nothing in the Lab was written, moved, or deleted.

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

The absent research-package reference is the one hard blocker: the collector code is not loaded in the Lab at all. It is a one-line configuration change in the Lab, and it needs no repository change.

The host toolchain entry is the exact churn that AGENTS.md rules out of *this* repository. In the Lab it is harmless, and it stays. The Lab is single-machine and disposable. The rule protects contributors on other hosts, and the Lab has none.

## 3. Determinations

### 3.1 Standalone Unity project only

Yes. The Lab is a Unity project and nothing else. It is not a workspace, not a member of a monorepo, and not a second checkout of this repository.

### 3.2 No git repository

The Lab holds private avatars and purchased vendor packages. A repository there offers recovery for a disposable thing. It also creates a redistribution hazard the moment someone adds a remote.

**Per-run provenance** serves reproducibility better than history. Each run copies `Packages/vpm-manifest.json`, `ProjectSettings/ProjectVersion.txt`, and the AMUSE commit SHA into its own output directory. That provenance travels with the run. Deleting the Lab does not destroy it. Lab-local history would not survive.

This also reduces the "what may be committed" question to a single invariant, with no ignore file to maintain: **the Lab commits nothing, because the Lab is not a repository.**

### 3.3 No source code in the Lab

The Lab contains project configuration and private data. No C#, no editor scripts, no runner, and no committed scene of tooling. Every executable line lives in this repository, in `Packages/com.alrauna.amuse.research`, and reaches the Lab through the local package reference.

The test of correct placement is unchanged. If deleting the Lab would lose anything other than private data and resolved third-party packages, something is in the wrong place.

### 3.4 The package set stays, and becomes provenance

The 24 packages stay in the Lab.

This design considered deletion and rejected it on evidence, not on taste:

- **Installed optimizers do not disturb the measurement.** The collector reads `sharedMesh` and `sharedMaterials` on a scene object in Edit mode and never runs a build. VRCFury, Modular Avatar, and the four optimizers are thus inert unless someone invokes them.
- **A rich package set improves fidelity.** A consented avatar whose shader package is absent resolves to a missing-shader material and produces a *false* `SemanticsUnknown`. Stripping the Lab would produce exactly the miscount that the census exists to avoid.
- **The Lab is also the integration testbed (§1.1).** A minimal Lab cannot serve that role.

The real risk is not breadth but **silent drift**: a routine VPM update between runs changes what the census means. The design addresses that risk by recording the package set with every run (§3.2) and by pinning vendor versions in the gate (§5.2), not by deletion.

### 3.5 Recreating the Lab

Recreation is a documented sequence in the AMUSE README, not a script. This follows the precedent and the reasoning of `2026-08-19-reproducible-vpm-setup-design.md`, which declined a wrapper around an official CLI that is already idempotent.

```
new Unity project, editor version from the recorded ProjectVersion.txt
restore the recorded vpm-manifest.json      (vpm resolve project .)
add two local package references to a working tree of this repository:
    com.alrauna.amuse
    com.alrauna.amuse.research
install consented avatars
```

The operator derives the two `file:` references on the machine that does the recreation. Never copy them from a recorded value. They encode where that machine holds its checkout, and nothing about the project. This is the same rule that AGENTS.md applies to the repository itself.

Writing that README section is not part of the calibration-gate increment. It is a small `docs/` change, and it should accompany the first branch that actually runs the Lab.

## 4. Privacy boundary

### 4.1 Placement

Unchanged from the harness design, restated here only for completeness.

| Location | Holds |
|---|---|
| `Packages/com.alrauna.amuse` | Production optimizer |
| `Packages/com.alrauna.amuse.research` | Collection, anonymization, aggregation, schemas, calibration, fixtures |
| `docs/` | Designs, plans, published aggregate reports |
| **The Lab** | Consented avatars, consent records, vendor packages, tier 1 and tier 2 output, any identity mapping |

### 4.2 The output boundary is documented, not enforced in code

This design considered a guard that would make the research package refuse writes outside the Lab, and **rejected** it.

The boundary is already architectural without the guard. The Lab is not a repository, so no commit path leaves it. The repository holds no private data, so it has nothing to leak. A layout-aware guard would teach the research package about a structure that it otherwise never needs to know. It would defend against a failure mode that has not occurred. That is a speculative abstraction, and it trades a real increase in coupling for a hypothetical protection.

The design therefore documents the rule and states it once, here:

> **Tier 1 and tier 2 census output are written inside the Lab and nowhere else. Tier 3, the aggregate report, is the only artifact that may leave.**

If a concrete failure mode appears later (output written into a working tree, or a raw record in a diff), the guard becomes justified. The design can add it then, against a real cause.

### 4.3 What an aggregate report may contain

§5 and §6.5 of the harness design govern this, unchanged: distributions only, no per-avatar rows, and no per-renderer rows. Every new category must name the AMUSE decision that it informs. It must state the smallest population that its buckets can hold. It must survive this question: what does an adversary who holds one avatar learn from it?

One addition is specific to the Lab: **a report states its provenance**. Provenance is the Unity version, the vendor package versions, the AMUSE commit, and the avatar count. No one can interpret a shader-family distribution without knowing which shader packages were resolvable when it was measured. None of those values identifies an avatar or a creator.

## 5. The calibration gate

### 5.1 What the gate is for

A census whose production analysis path cannot reach a success outcome would report near-total `SemanticsUnknown`. That statement is true about the environment and false about AMUSE. A reader of the report cannot tell it apart from a real result. The gate exists so that this failure aborts the run instead of publishing.

The gate runs through the **production** path: `RendererObservationBuilder.Build(renderer, hierarchyPath, families)`, the three-argument overload with no semantics provider. It deliberately does not use the `BaseMaterialSemanticsProvider` seam that `CollectorSeamCountingTests` uses. That seam proves *counting*. The gate proves *reachability*. Conflating them is the specific mistake that this design prevents.

### 5.2 Vendor attestation is exact-version and source-pinned

Verified in source, and load-bearing for everything below:

| Pin | Location | Value |
|---|---|---|
| Poiyomi package version | `PoiyomiMaterialSemantics.cs:28` | `9.3.64` |
| Poiyomi shader name | `PoiyomiMaterialSemantics.cs:26` | `.poiyomi/Poiyomi Toon` |
| lilToon package version | `LilToonSourceAttestation.cs:221` | `2.3.4` |
| lilToon shader name | `LilToonSourceAttestation.cs:214` | `lilToon` |

Attestation also compares a normalized source hash and a canonical asset GUID, so it fails closed on any modified or repackaged shader.

The Lab installs exactly these two versions today. That is fortunate, not guaranteed. The version pin is therefore the first case of the gate. An ordinary VPM update in the Lab silently makes every later census measure the version mismatch.

### 5.3 The cases

Cases 2 through 5 each build one `MeshRenderer` in a temporary scene, run the production path, and assert the observed outcome. Cases 1 and 6 build no renderer: they examine the installed packages.

The six cases form one suite with two operative halves. In the Lab, where the vendor packages are installed, cases 1 through 5 assert and case 6 does not apply. In public CI, where no vendor package is installed, case 6 is the operative one, and cases 2 through 5 assert nothing. See §5.5.

| # | Case | Construction | Claim |
|---|---|---|---|
| 1 | Version pin | Read the installed vendor `PackageInfo` version and compare it to the pinned constant | Vendor drift fails loudly rather than silently |
| 2 | `ProvenOpaque` | Default vendor material, opaque, no alpha texture | The success path is reachable through production |
| 3 | `MissingTextureEvidence` | Vendor material whose alpha binds a runtime `Texture2D` that is not a project asset, so no `TextureImporter` exists | "Understood shader, unseen texture" is distinguishable |
| 4 | `MustRemainTransparent` | Vendor material with `_Color` alpha set below 1 and no alpha texture | The transparent path is reachable through production |
| 5 | `SemanticsUnknown` via lock | Vendor material with `_ShaderOptimizerEnabled` set to 1 | Characterizes the blind spot recorded in §6 |
| 6 | Vendor absent | Probe finds no attested family | Public CI passes honestly. A census run aborts |

`Unknown` at triangle level gets no case of its own. It is the outcome of cases 3 and 5. The existing refusal calibration tests already cover it in public CI. A separate case would assert the same thing a third time.

### 5.4 Two choices that keep the gate cheap and non-mutating

**The gate needs no fixture asset at all.** An earlier revision of this design specified a committed 4×4 alpha PNG for case 4. A close read of the resolver removed the need for it. `AlphaSemanticsResolver.cs:326` classifies any *constant* alpha below 1 as `MustRemainTransparent` outright, with no texture involved. `UnityTextureEvidence.TryProveSampledAlphaIsOne` proves opacity from importer metadata, not from pixels. The gate would not sample the texels of that texture anyway. Setting the `_Color` alpha of a vendor material below 1 reaches the outcome directly.

Every case therefore builds its material in memory. **The gate imports nothing, commits no binary asset, and writes nothing to either project.**

**Case 5 sets a material property. It never runs the shader locker.** The lock state of Poiyomi is read from the material: `PoiyomiMaterialSemantics.cs:1011-1013` derives it from `material.GetFloat("_ShaderOptimizerEnabled") != 0f`, and `PoiyomiMaterialSemantics.cs:929` rejects on it before any source check. Setting that float on an in-memory material reproduces the rejection exactly. It generates no shader asset and writes nothing to the Lab. Invoking the real locker would mutate the Lab persistently, for no additional evidence.

### 5.5 Absence must not be able to masquerade as success

The gate carries two claims, and they must not collapse into one:

1. *If* an attested vendor family is installed, the production path reaches its success outcomes.
2. A census run requires at least one attested vendor family to be installed.

Claim 1 is the test. Claim 2 is a run precondition, and it belongs to the runner branch. That branch does not exist yet.

The design rejects `Assert.Ignore` as the absence behavior. In the Lab, where a vendor package can genuinely go missing, an ignored test reports a pass-shaped result for a condition that must abort a census. The probe instead returns a value, the test asserts the conditional, and the run reports absence instead of skipping it.

### 5.6 Expected outcomes are characterized, not predicted

This design does not state what a default Poiyomi material or a default lilToon material returns from the production path. A prediction here would be a guess about the default property state of a vendor shader. A gate with guessed expectations is worth nothing.

The first task of the plan therefore **observes** the outcomes in the Lab and pins them, in the manner of the existing `Tests/Editor/Semantics/Characterization` suite. If an outcome differs from the table in §5.3 (for example, a default vendor material does not reach `ProvenOpaque`), that is a finding to report, not a defect to code around. This kind of result must surface before the first avatar is measured, not after.

### 5.7 Where the probe lives

In the research **test** assembly, for now.

Its eventual second consumer is the abort precondition in the census runner, which does not exist. Placement in the production assembly today would create a type whose only caller is a test. That is the precise shape that the collector design rejected at its §3.1.1. It would also anticipate a consumer that may be designed differently when it arrives. Moving one `internal static` class into the production assembly later costs one file move and breaks no API.

## 6. Deferred investigation: attestation failures are indistinguishable

**Recorded, not solved. No implementation, no attestation change, and no new census category on this branch or its increment.**

### 6.1 The finding

Poiyomi rejects locked materials outright: `PoiyomiMaterialSemantics.cs:929` returns false on `evidence.IsLocked` before any source or schema check. Locking is the normal state for a distributed avatar, because the shader optimizer is part of the standard upload workflow.

The locker also renames the shader of a locked material. The renamed shader fails the attestation trial of the research collector, and the collector anonymizes it into `UnknownFamily-A`. A genuinely unrecognized third-party shader lands in the same bucket.

The current census therefore **cannot distinguish** three materially different situations:

1. a shader family for which AMUSE has no adapter.
2. a supported vendor shader whose material is locked.
3. any other attestation failure: version mismatch, modified source, missing package evidence, or an unreadable shader asset.

A fourth candidate is suspected but **unverified**. lilToon attests the shader name `lilToon` exactly (`LilToonSourceAttestation.cs:214`). lilToon materials that use a differently named transparent or preset variant may also land in the unattested bucket. No one checked this, and this document does not assert it. The gate is the cheapest place to find out.

### 6.2 Why it matters, and why it is deferred

The headline census number is the share of avatar geometry that AMUSE cannot prove opaque. Readers would read it as *"AMUSE needs more shader adapters."* If locking dominates the unattested bucket, the true reading is *"AMUSE needs to handle locked materials."* That is an entirely different roadmap.

The design defers it because action now would mean either a change to attestation behavior or a new census category, ahead of any evidence about the real distribution. Both are premature. The safety rule that uncertainty must never increase aggression applies to roadmap decisions as much as to transforms.

### 6.3 What is preserved

This section is the artifact. Case 5 of the gate (§5.3) shows mechanism 2 concretely. The finding thus rests on observed behavior, not on what this document asserts. It implements nothing and changes no production code.

Anyone who interprets a future census must read the unattested-family share as an **upper bound on unsupported shader families**, never as a count of them.

## 7. Workflow, end to end

Steps 1, 2, and 6 are operator procedure. Steps 4 and 5 need machinery that does not exist (§8). The increment on this branch implements only step 3.

1. **Record consent** before any avatar file enters the Lab.
2. **Install the avatar** in the Lab, with the packages it requires.
3. **Run the calibration gate.** A failure aborts here, before anything is measured.
4. **Collect → Anonymize → Aggregate**, in memory, in Edit mode only.
5. **Write tier 3 plus provenance.** Tier 1 and tier 2 remain in the Lab.
6. **Remove raw fixtures** when the run is complete.

## 8. Not built, and deliberately so

| Missing piece | Why it waits |
|---|---|
| Tier 1/2/3 serialization | Choose the output format and the bucket boundaries against real distributions, per harness design §6.5. If serialization lands before the gate proves that counting works on vendor materials, the schema can get pinned to a broken measurement. Whatever format is chosen must keep the `null`-versus-zero distinction. That distinction is the most likely defect in the whole system |
| Run invocation surface | Nothing to invoke until there is output to produce |
| Layer 3 asset-manifest proof | A run-level obligation with no run to attach to |
| Consent record format | Procedure, not code. Belongs with the first real cohort |
| Minimum avatar diversity threshold | No one can define it before the population is known. A census over three avatars from one creator measures that creator |
| README recreation section | A small `docs/` change for the first branch that actually runs the Lab |

## 9. Constraints on the increment

The gate must not require any of the following. Each is a stop condition: halt and return for review, rather than proceed.

- any change to AMUSE analysis behavior, a result object, a shader adapter, or an evidence provider.
- any change to attestation, including any relaxation for locked materials.
- any public API promotion in `com.alrauna.amuse`.
- any visibility change beyond the two `InternalsVisibleTo` grants that are already present.
- a new census category, field, or schema change.
- telemetry, networking, cloud reporting, automatic avatar discovery, or a persistent analytics store.
- private fixture handling in either AMUSE package.
- any change that persists in the Lab: no saved scene, no generated shader, no imported asset, and no project settings change.
- an options object, a registry, or a provider framework that grows out of what should be a probe and a set of test cases.

## 10. Validation of this branch

Stated as observed, not as intended.

**Verified by reading source and the filesystem:**

- the vendor version pins, the shader names, and the locked-material rejection, at the line references in §5.2 and §6.1.
- Collect, Anonymize, and Aggregate exist, and the `MeshSeparationPlan` arithmetic cross-check is implemented at `RendererObservationBuilder.cs:166`.
- no serialization, invocation surface, asset manifest, or reachability gate exists in the research package.
- the Lab has no `.git`, holds no avatars, runs Unity 2022.3.22f1, installs Poiyomi 9.3.64 and lilToon 2.3.4, and does **not** reference the research package.

**Not verified, and not verifiable on this branch:**

- what the production path actually returns for a default vendor material. This is the first task of §5.6, and it requires the Lab.
- whether the transparent variants of lilToon are attested (§6.1). Suspected, not asserted.
- that the gate passes. The gate does not exist yet.

**Unity MCP:** not used at any point on this branch. **The Lab:** inspected read-only through the filesystem. Not opened, not modified, not written to.

## 11. Decisions summary

| # | Decision |
|---|---|
| 1 | The Lab replaces the former private testbed. The testbed rules of AGENTS.md apply to it unchanged |
| 2 | Standalone Unity project, no git repository, no source code |
| 3 | The 24-package set is kept. Drift is controlled by per-run provenance, not by deletion |
| 4 | Recreation is a documented sequence, not a script. The machine that recreates derives the `file:` paths |
| 5 | The output boundary is documented, **not** enforced by a repository-layout guard |
| 6 | The gate runs the production path, never the semantics seam |
| 7 | Six cases, and every material is built in memory. No fixture asset. The lock case sets a property and never runs the locker |
| 8 | Vendor absence is reported, never skipped with `Assert.Ignore` |
| 9 | Expected outcomes are characterized in the Lab, not predicted here |
| 10 | The probe lives in the test assembly until a second consumer exists |
| 11 | Indistinguishable attestation failures are recorded as a deferred investigation. Nothing is implemented, and attestation is unchanged |
