# Census Vendor Reachability Gate — Observed Results

Branch: `research/census-lab-preparation`
Date: 2026-08-20
Plan: `docs/superpowers/plans/2026-08-20-census-lab-preparation.md`
Design: `docs/superpowers/specs/2026-08-20-census-lab-preparation-design.md`

Everything below was observed. Nothing is inferred. When a prediction was wrong, this document states the prediction and what replaced it.

## 1. The decisive answer

**`ProvenOpaque` is reachable through AMUSE's production analysis path.** A census may proceed on Poiyomi content in this environment.

`MustRemainTransparent` is also reachable. The tried route does **not** reach `MissingTextureEvidence`, and lilToon reaches nothing. The findings below record both results without fixes.

## 2. Environment

Before each reported run, we identified both Unity instances by `Application.dataPath`. We normalized and compared each path exactly.

| | Public development project | Census Lab |
|---|---|---|
| `Application.dataPath` | `<repo-root>/Assets` — exact, same-case match | not `<repo-root>/Assets` |
| MCP instance | the public development instance | the private Census Lab instance |
| Unity | 2022.3.22f1 | 2022.3.22f1 |
| Poiyomi | absent | `com.poiyomi.toon` **9.3.64** |
| lilToon | absent | `jp.lilxyzw.liltoon` **2.3.4** |

Both installed vendor versions exactly match AMUSE's pins. Therefore, gate case 1 passes. Every observation below concerns AMUSE, not a version mismatch.

## 3. Gate results

Ten tests ran. **Lab: 10 passed, 0 failed, 0 skipped.** **Public project: 812 passed, 0 failed, 0 skipped.** The baseline was 802 before any change on this branch.

Case 6 gave this report in the Lab:

> `VENDOR REACHABILITY EXERCISED for: com.poiyomi.toon 9.3.64, jp.lilxyzw.liltoon 2.3.4`

and in the public project:

> `VENDOR REACHABILITY NOT PROVEN - no attested vendor family is installed. This is the
> EXPECTED state in the public development project […] A green run here says only that the
> gate compiles; it does not establish that AMUSE reaches ProvenOpaque.`

## 4. What the production path actually returns

We measured these results in the Lab through `RendererObservationBuilder.Build(renderer, path, families)`. This is the production overload without a semantics provider. We used a one-triangle mesh with one material.

### 4.1 Poiyomi 9.3.64, shader `.poiyomi/Poiyomi Toon`

| Material | failure | attestation | opaque | transparent | unknown |
|---|---|---|---|---|---|
| untouched default | None | Poiyomi | **1** | 0 | 0 |
| `_Color.a=1`, no texture | None | Poiyomi | **1** | 0 | 0 |
| `_Color.a=0.5`, no texture | None | Poiyomi | **1** | 0 | 0 |
| `_Color.a=1`, runtime texture | None | Poiyomi | **1** | 0 | 0 |
| all four `_Mode` values × the above | None | Poiyomi | **1** | 0 | 0 |
| `_ShaderOptimizerEnabled=1` (locked) | SemanticsUnknown | **None** | 0 | 0 | 1 |
| `_AlphaForceOpaque=0`, defaults otherwise | SemanticsUnknown | Poiyomi | 0 | 0 | 1 |
| `_AlphaForceOpaque=0`, `_MainAlphaMaskMode=0`, `_Color.a=1` | None | Poiyomi | **1** | 0 | 0 |
| `_AlphaForceOpaque=0`, `_MainAlphaMaskMode=0`, `_Color.a=0.5` | None | Poiyomi | 0 | **1** | 0 |
| `_AlphaForceOpaque=0`, `_MainAlphaMaskMode=0`, runtime `_MainTex` | SemanticsUnknown | Poiyomi | 0 | 0 | 1 |

### 4.2 lilToon 2.3.4, shader `lilToon`

| Material | failure | attestation |
|---|---|---|
| untouched default | SemanticsUnknown | **None** |
| `_Color.a=1`, no texture | SemanticsUnknown | **None** |
| `_Color.a=0.5`, no texture | SemanticsUnknown | **None** |
| `_Color.a=1`, runtime texture | SemanticsUnknown | **None** |

## 5. Predictions that were wrong

Four predictions were wrong. We recorded all four instead of coding around them. This is why the design required characterization instead of prediction. Each prediction could have become a false assertion shipped as a gate.

### 5.1 `_Color` alpha does not drive Poiyomi's alpha

**Predicted:** Setting `_Color.a` below 1 reaches `MustRemainTransparent`.
**Observed:** It changes nothing. The material still proves opaque.

**Cause:** Source review after the observation showed that `PoiyomiMaterialSemantics.InterpretAlpha` checks `_AlphaForceOpaque` first. It immediately returns `Constant(1f)` when that property is set. The property defaults to **1** on a fresh material. The code never checks colour alpha.

This behavior is correct, not a defect. A forced-opaque material is opaque. However, the naive gate would have passed without ever running the alpha equation. This green result would prove less than it appeared to prove.

**Replaced by:** `PoiyomiReachesProvenOpaqueThroughTheProductionPath` asserts both the forced-opaque path and the computed path with the force flag off. Thus, the equation runs.

### 5.2 One alpha gate is non-zero by default

**Observed:** Of AMUSE's 28 alpha gates (5 coverage, 23 feature), exactly one is non-zero on an untouched Poiyomi material: **`_MainAlphaMaskMode = 2`**. Every other gate defaults to 0.

Thus, the real alpha equation needs exactly two property writes: `_AlphaForceOpaque=0` and `_MainAlphaMaskMode=0`. It does not need the 28-property scrub first attempted. The test sets those two and explains why. It does not duplicate AMUSE's private gate list.

### 5.3 `MissingTextureEvidence` is not reachable this way

**Predicted:** A runtime `Texture2D` in `_MainTex` yields `MissingTextureEvidence`.
**Observed:** It yields `SemanticsUnknown`, while the shader remains attested as Poiyomi.

**Cause:** A runtime texture is not a project asset, so it has no `TextureImporter`. Poiyomi's own `InterpretAlpha` requires that import evidence before it can build any texture sample. It returns `Unknown` at the *semantics* layer. Therefore, `AlphaSemanticsResolver` never runs and cannot report `MissingTextureEvidence`.

**Consequence, which is a real gap:** Production reachability of `MissingTextureEvidence` is **unproven**. `CollectorSeamCountingTests` proves that the census *counts* it correctly when it occurs. However, nothing yet proves that AMUSE *produces* it from a real material. A plausible route requires a texture that **is** a project asset. The adapter can then read its filter, wrap, and importer. However, the resolver still cannot supply its alpha field.

**Replaced by:** `ANonAssetTextureIsRefusedBySemanticsBeforeTheResolverSeesIt` asserts the observed `SemanticsUnknown`. Its doc-comment records the gap. Its message tells a future reader that the gap has closed if the assertion starts failing.

### 5.4 lilToon is not attested at all

**Predicted:** lilToon behaves like Poiyomi because the Lab has exactly the pinned 2.3.4.
**Observed:** No tried configuration attests a lilToon material. Each material reports `SemanticsUnknown` with `ShaderFamilyAttestation.None`.

**Likely cause:** This cause is a hypothesis and is not verified. lilToon regenerates its shader assets from per-project settings. `LilToonSourceAttestation` digests those generated assets. The Lab has a `ProjectSettings/lilToonSetting.json` dated well before this work. Therefore, its generated shaders can legitimately differ from the pinned digests. Confirmation requires tracing the digest computation against this install, which is out of scope here.

**Replaced by:** `LilToonIsNotAttestedInThisEnvironmentDespiteMatchingItsPin` asserts the observed state. Its message says that failure is good news and should cause conversion to a positive reachability assertion.

**This is the most consequential finding for census interpretation.** A census in this environment measures **zero lilToon coverage**. This result describes AMUSE in this environment, not any avatar. lilToon is one of the two most common VRChat avatar shader families. A census that reports it as unsupported would badly misdirect the roadmap.

## 6. The deferred investigation now has evidence

From source alone, the design's §6 recorded that the census cannot distinguish two cases. These cases are an unknown shader family and a supported-but-locked vendor material. The observations now confirm this result:

- a locked Poiyomi material reports `AlphaFailure = SemanticsUnknown` and
  `ShaderFamilyAttestation = None`;
- an untouched lilToon material reports **exactly the same pair**.

These situations are completely different. One is a supported shader with a locked material. The other is a supported shader whose install AMUSE cannot attest. However, they are byte-identical in the census record. A genuinely unknown third-party shader would also be identical.

Both tests specifically assert the attestation value. Thus, the tests pin the indistinguishability instead of only describing it.

**We implemented nothing and changed no attestation behaviour.** The rule for reading any future census still applies and now has evidence. *The unattested-family share is an upper bound on unsupported shader families, never a measurement of them.*

The data supports one refinement. `ShaderFamilyAttestation` **does** separate a third case. `_AlphaForceOpaque=0` with default gates gives `SemanticsUnknown`, while attestation stays `Poiyomi`. Thus, the existing record distinguishes "attested shader, unsupported alpha feature" from "unattested anything". This split needs no new category.

## 7. Lab mutation

**Authorized and performed:** We made one change to the Lab's `Packages/manifest.json`. We added the `com.alrauna.amuse.research` local `file:` reference and a `testables` array. We derived the reference from the existing `com.alrauna.amuse` entry, not from any document. The array lets the Test Framework discover tests in a non-embedded local package. The complete diff has two hunks and nothing else.

`Packages/packages-lock.json` changed because of the resolve. The plan expected and stated this change.

**Verified unchanged after the run:**

- `Assets/` tree byte-identical to the pre-run inspection. It contained the same LTCGI gizmo, LTCGI shader include, and `csc.rsp`;
- **zero shaders under `Assets/`**, so Poiyomi's locker never ran;
- zero assets matching the gate's naming;
- no scene open, no scene dirty, never in Play Mode.

**One thing I could not verify:** Unity wrote `ProjectSettings/ProjectSettings.asset` during the session at 22:38:30, around the final gate run. It contains no reference to this work. It contains only the pre-existing `productName` and VRChat define symbols. Therefore, there is no evidence of a substantive change. Unity also routinely writes this file on domain reload. **However, I cannot prove that its content is unchanged because no baseline existed for comparison.**

That gap directly results from design decision §3.2, which states that the Lab is not a repository. It is exactly the gap that design §8 assigns to the unbuilt Layer 3 asset manifest. This run gives a concrete reason to build it before real avatars are involved. "Was the Lab mutated?" must be answerable after the fact. Today, it is not.

## 8. Validation summary

| | |
|---|---|
| Public project, before this branch | **802 passed / 0 failed / 0 skipped**, 32.9 s |
| Public project, after | **812 passed / 0 failed / 0 skipped**, 32.3 s |
| Tests added | **10** |
| Census Lab, gate only | **10 passed / 0 failed / 0 skipped**, 2.6 s |
| Console entries matching `Alrauna` | **zero** errors, **zero** warnings, both projects |
| Production code changed | **none** |
| `Packages/com.alrauna.amuse/` changed | **none** |
| `Packages/com.alrauna.amuse.research/Editor/` changed | **none** |
| New `InternalsVisibleTo` grants | **none** |
| Attestation changed | **none** |
| New census categories, fields, or schema changes | **none** |
| Fixture assets committed | **none** — every material and mesh is built in memory |

We proved that the version-pin assertion was non-vacuous instead of assuming it. Pointing the probe's Poiyomi arm at `"Standard"` made it fail with `Expected: "9.3.64" But was: null`. We then reverted the probe to a byte-identical file.

Unity host-toolchain churn (`com.unity.toolchain.macos-arm64-linux-x86_64` plus its `com.unity.sysroot*` dependencies) reappeared in `Packages/manifest.json` and `Packages/packages-lock.json`. This occurred on **every** Unity reconnect during this branch. Each time, we inspected the full diff and found only those generated entries. We restored them according to AGENTS.md. None of them was ever staged.

## 9. What a census still needs

The gaps from design §8 remain unchanged. This run added more gaps:

| Gap | Status |
|---|---|
| Tier 1/2/3 serialization | Not built |
| Run invocation surface | Not built |
| Layer 3 asset-manifest proof | Not built — and §7 above is a concrete argument for it |
| Consent record format | Not defined |
| Minimum avatar diversity threshold | Not defined |
| README recreation section | Not written |
| **lilToon attestation in a real install** | **New. Blocks meaningful lilToon coverage** |
| **Production reachability of `MissingTextureEvidence`** | **New. Unproven** |

A census cannot honestly ignore the last two gaps. lilToon is especially important. Without attestation, a census over real avatars would report a large unsupported share. That result says more about AMUSE's lilToon adapter than about the measured avatars.
