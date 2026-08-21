# Census Vendor Reachability Gate — Observed Results

Branch: `research/census-lab-preparation`
Date: 2026-08-20
Plan: `docs/superpowers/plans/2026-08-20-census-lab-preparation.md`
Design: `docs/superpowers/specs/2026-08-20-census-lab-preparation-design.md`

Everything below was observed. Nothing is inferred, and where a prediction was wrong the
prediction is stated alongside what replaced it.

## 1. The decisive answer

**`ProvenOpaque` is reachable through AMUSE's production analysis path.** A census may
proceed, on Poiyomi content, in this environment.

`MustRemainTransparent` is reachable too. `MissingTextureEvidence` is **not**, by the route
tried, and lilToon reaches nothing at all. Both are recorded below as findings rather than
fixed.

## 2. Environment

Both Unity instances were identified by `Application.dataPath`, normalized and compared
exactly, before any run whose result is reported here.

| | Public development project | Census Lab |
|---|---|---|
| `Application.dataPath` | `<repo-root>/Assets` — exact, same-case match | not `<repo-root>/Assets` |
| MCP instance | `AMUSE@d5617927`, port 6402 | `AMUSE-Census-Lab@13495ff7`, port 6400 |
| Unity | 2022.3.22f1 | 2022.3.22f1 |
| Poiyomi | absent | `com.poiyomi.toon` **9.3.64** |
| lilToon | absent | `jp.lilxyzw.liltoon` **2.3.4** |

Both installed vendor versions match AMUSE's pins exactly, so gate case 1 passes and every
observation below is about AMUSE rather than about a version mismatch.

## 3. Gate results

Ten tests. **Lab: 10 passed, 0 failed, 0 skipped.** **Public project: 812 passed, 0 failed,
0 skipped**, from an 802 baseline measured before any change on this branch.

Case 6 reported, in the Lab:

> `VENDOR REACHABILITY EXERCISED for: com.poiyomi.toon 9.3.64, jp.lilxyzw.liltoon 2.3.4`

and in the public project:

> `VENDOR REACHABILITY NOT PROVEN - no attested vendor family is installed. This is the
> EXPECTED state in the public development project […] A green run here says only that the
> gate compiles; it does not establish that AMUSE reaches ProvenOpaque.`

## 4. What the production path actually returns

Measured in the Lab through `RendererObservationBuilder.Build(renderer, path, families)` —
the production overload, no semantics provider — on a one-triangle mesh with one material.

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

Four, all recorded rather than coded around. This is why the design required characterization
instead of prediction; every one of these would have been a false assertion shipped as a gate.

### 5.1 `_Color` alpha does not drive Poiyomi's alpha

**Predicted:** setting `_Color.a` below 1 reaches `MustRemainTransparent`.
**Observed:** it changes nothing; the material still proves opaque.

**Cause,** read from source after observing: `PoiyomiMaterialSemantics.InterpretAlpha` checks
`_AlphaForceOpaque` first and returns `Constant(1f)` immediately when it is set. It defaults
to **1** on a fresh material. Colour alpha is never consulted.

This is correct behaviour, not a defect: a forced-opaque material is opaque. It does mean the
naive gate would have passed *without ever running the alpha equation* — a green result
proving less than it appeared to.

**Replaced by:** `PoiyomiReachesProvenOpaqueThroughTheProductionPath`, which asserts both the
forced-opaque path and the computed path with the force flag off, so the equation actually
runs.

### 5.2 One alpha gate is non-zero by default

**Observed:** of AMUSE's 28 alpha gates (5 coverage, 23 feature), exactly one is non-zero on
an untouched Poiyomi material: **`_MainAlphaMaskMode = 2`**. Every other gate defaults to 0.

So reaching the real alpha equation needs exactly two property writes — `_AlphaForceOpaque=0`
and `_MainAlphaMaskMode=0` — not the 28-property scrub first attempted. The test sets those
two and explains why, rather than duplicating AMUSE's private gate list.

### 5.3 `MissingTextureEvidence` is not reachable this way

**Predicted:** a runtime `Texture2D` in `_MainTex` yields `MissingTextureEvidence`.
**Observed:** `SemanticsUnknown`, with the shader still attested as Poiyomi.

**Cause:** a runtime texture is not a project asset, so it has no `TextureImporter`, and
Poiyomi's own `InterpretAlpha` requires that import evidence to build a texture sample at
all. It returns `Unknown` at the *semantics* layer, so `AlphaSemanticsResolver` never runs
and never gets to report `MissingTextureEvidence`.

**Consequence, and it is a real gap:** production reachability of `MissingTextureEvidence`
is **unproven**. `CollectorSeamCountingTests` proves the census *counts* it correctly when it
occurs; nothing yet proves AMUSE *produces* it from a real material. Reaching it plausibly
requires a texture that **is** a project asset — so the adapter can read its filter, wrap,
and importer — whose alpha field the resolver still cannot supply.

**Replaced by:** `ANonAssetTextureIsRefusedBySemanticsBeforeTheResolverSeesIt`, which asserts
the observed `SemanticsUnknown` and carries the gap in its doc-comment, with a message that
tells a future reader the gap has closed if the assertion ever starts failing.

### 5.4 lilToon is not attested at all

**Predicted:** lilToon behaves like Poiyomi, since the Lab has exactly the pinned 2.3.4.
**Observed:** no lilToon material is attested in any configuration tried. Every one reports
`SemanticsUnknown` with `ShaderFamilyAttestation.None`.

**Likely cause,** stated as a hypothesis and not verified: lilToon regenerates its shader
assets from per-project settings, and `LilToonSourceAttestation` digests those generated
assets. The Lab carries a `ProjectSettings/lilToonSetting.json` dated well before this work,
so its generated shaders can legitimately differ from the pinned digests. Confirming this
would mean tracing the digest computation against this install, which is out of scope here.

**Replaced by:** `LilToonIsNotAttestedInThisEnvironmentDespiteMatchingItsPin`, which asserts
the observed state and says in its message that if it ever fails, that is good news and it
should become a positive reachability assertion.

**This is the most consequential finding for census interpretation.** A census run in this
environment measures **zero lilToon coverage**, and that is a statement about AMUSE in this
environment, not about any avatar. lilToon is one of the two most common VRChat avatar shader
families; a census reporting it as unsupported would badly misdirect the roadmap.

## 6. The deferred investigation now has evidence

The design's §6 recorded, from source alone, that the census cannot distinguish an unknown
shader family from a supported-but-locked vendor material. That is now **observed**, not
argued:

- a locked Poiyomi material reports `AlphaFailure = SemanticsUnknown` and
  `ShaderFamilyAttestation = None`;
- an untouched lilToon material reports **exactly the same pair**.

Two completely different situations — a supported shader whose material is locked, and a
supported shader whose install AMUSE cannot attest — are byte-identical in the census record,
and a genuinely unknown third-party shader would be identical again.

Both tests assert the attestation value specifically, so the indistinguishability is pinned
rather than described.

**Nothing was implemented and no attestation behaviour was changed.** The rule for reading
any future census stands, and is now evidenced: *the unattested-family share is an upper
bound on unsupported shader families, never a measurement of them.*

One refinement the data supports: `ShaderFamilyAttestation` **does** separate a third case —
`_AlphaForceOpaque=0` with default gates gives `SemanticsUnknown` while attestation stays
`Poiyomi`. So "attested shader, unsupported alpha feature" is already distinguishable in the
existing record from "unattested anything". No new category is needed for that split.

## 7. Lab mutation

**Authorized and performed:** one change to the Lab's `Packages/manifest.json` — the
`com.alrauna.amuse.research` local `file:` reference, derived from the existing
`com.alrauna.amuse` entry rather than copied from any document, plus a `testables` array so
the Test Framework discovers tests in a non-embedded local package. The complete diff is two
hunks and nothing else.

`Packages/packages-lock.json` changed as a consequence of the resolve. Expected, and stated in
the plan.

**Verified unchanged after the run:**

- `Assets/` tree byte-identical to the pre-run inspection — the same LTCGI gizmo, LTCGI shader
  include, and `csc.rsp`;
- **zero shaders under `Assets/`**, so Poiyomi's locker never ran;
- zero assets matching the gate's naming;
- no scene open, no scene dirty, never in Play Mode.

**One thing I could not verify, stated plainly:** `ProjectSettings/ProjectSettings.asset` was
written during the session, at 22:38:30, around the final gate run. It contains no reference
to this work — only the pre-existing `productName` and VRChat define symbols — so there is no
evidence of a substantive change, and Unity writes this file routinely on domain reload.
**But I cannot prove its content is unchanged, because no baseline existed to compare against.**

That is a direct consequence of design decision §3.2 (the Lab is not a repository), and it is
exactly the hole the design's §8 assigns to the unbuilt Layer 3 asset manifest. This run is a
concrete argument for building it before real avatars are involved: "was the Lab mutated?"
must be answerable after the fact, and today it is not.

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

The version-pin assertion was proved non-vacuous rather than assumed: pointing the probe's
Poiyomi arm at `"Standard"` made it fail with `Expected: "9.3.64" But was: null`, and the
probe was reverted to a byte-identical file.

Unity host-toolchain churn (`com.unity.toolchain.macos-arm64-linux-x86_64` plus its
`com.unity.sysroot*` dependencies) reappeared in `Packages/manifest.json` and
`Packages/packages-lock.json` on **every** Unity reconnect during this branch. Each time the
full diff was inspected, found to contain nothing but those generated entries, and restored
per AGENTS.md. None of it was ever staged.

## 9. What a census still needs

Unchanged from the design's §8, plus what this run added:

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

The last two are the ones a census cannot honestly ignore. lilToon especially: with it
unattested, a census over real avatars would report a large unsupported share that says more
about AMUSE's lilToon adapter than about the avatars measured.
