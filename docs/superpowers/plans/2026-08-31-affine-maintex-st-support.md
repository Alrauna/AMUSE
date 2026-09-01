# Affine `_MainTex_ST` support — implementation plan

Implements `docs/superpowers/specs/2026-08-31-affine-maintex-st-support-design.md`
(the accepted design; section references `§n` below point there). This plan does
not reopen any design decision. Historical identity-ST records
(`docs/superpowers/specs/2026-08-30-liltoon-cutout-opaque-conversion-design.md`,
`docs/superpowers/plans/2026-08-30-liltoon-cutout-opaque-conversion.md`) are
read-only context.

| Field | Value |
|---|---|
| Tech Stack | Unity 2022.3.22f1, NDMF (pinned, embedded), NUnit EditMode, pinned lilToon 2.3.4 facts (vendor package **absent** from this project) |
| Production files | 4 changed, 1 added (+meta) — exact map in §FileMap |
| Test files | 7 changed, 2 added (+meta) — exact map in §FileMap |
| Branch | created later, with explicit authorization (§Git) |
| Commit/push | **never** assumed; nothing in this plan stages or commits |

## Base and branch preconditions

1. Work in `/Users/user/Documents/AMUSE`.
2. Implement on a **new focused branch** off `main` **at or after `89cc5be`**
   (merge of PR #41). Suggested name: `feature/affine-maintex-st-support`.
   Branch creation/switching must be authorized at implementation time; if the
   harness owns branch flow, follow it.
3. `Packages/manifest.json` and `Packages/packages-lock.json` carry
   user-owned Unity toolchain/sysroot churn. Never stage, restore, or absorb
   them. Before finishing: inspect the complete diff of both files and confirm
   they differ from `main` only by that churn.
4. Verify `git status --short --branch` before starting; record baseline.
5. Unity MCP gate (before any editor interaction, every session):
   enumerate instances read-only, read `Application.dataPath`, normalize, and
   require an exact match to `<repo-root>/Assets` (no case-only match); pin the
   instance when more than one is reachable. No validation claim without an
   observed run.

## File map (complete)

**Production**

| File | Change |
|---|---|
| `Packages/com.alrauna.amuse/Editor/Analysis/ExactUvGeometry.cs` | add: `HalfUlp(ExactDyadic)` (½ ulp of a float as dyadic), `EncodeToNearestFloat(ExactRational)` (returns `(float, ExactRational error)`), `IsExactlyRepresentable(ExactDyadic)` (binary32 fit incl. exponent range), `ConvexHull(IReadOnlyList<ExactUvPoint>)` (exact monotone chain), `OutwardExpand(ExactUvDomain, ExactRational ex, ExactRational ey)` (hull of every vertex offset by `(±ex, ±ey)`; zero early-out returns the same domain); **and** `CreateTextureScaledDomain` (defined here, `:227`) gains the envelope parameter and inflates by `(X·width·T, Y·height·T)` with `T = domain.TexelScale` |
| `Packages/com.alrauna.amuse/Editor/Analysis/AffineUvTransform.cs` **(+meta)** | new pure static helper: `TryTransform(UvMapping, TriangleAlphaInput, out TriangleAlphaInput transformed, out ExactRational envelopeX, out ExactRational envelopeY, out bool overflow)`. Implements §7 tiers E1/E2/E3/V per axis (E1/E3 normal-`s` guards), corner encode, envelope terms `B_enc`/`B_st`/`B_daz`/`B_noise` (§6.2 constants as named dyadic constants with derivation comments), 2^127 overflow guard, identity short-circuit |
| `Packages/com.alrauna.amuse/Editor/Analysis/TriangleAlphaClassifier.cs` | `Classify` gains a 4th parameter `AlphaUvEnvelope envelope` (new small readonly struct: `ExactRational X, Y; static Zero` — where it lives: this file) and threads it into the `ExactUvGeometry.CreateTextureScaledDomain` calls; the four mode paths keep their signatures and pass the inflated domain through unchanged |
| `Packages/com.alrauna.amuse/Editor/Analysis/AlphaSemanticsResolver.cs` | `IsSupportedMapping` narrows to `mapping.Channel == 0` (doc comment rewritten to cite the design; the "wider floating point is not such a proof" obligation text moves to the implemented tier predicate); `AlphaResolution` stores the `UvMapping`; `Classified(chain, sampling, mapping)` factory gains the parameter; `Classify` short-circuits identity, else calls `AffineUvTransform`, returns `Unknown` on `overflow`, and passes the envelope into the mip loop |

**No other production file changes.** Frontends, capture, admission,
`MeshSeparationPlanner`, `UnityRendererAlphaAnalysis`, apply, conversion — all
unchanged. If implementation reveals a need there, that is a stop condition
(design §16).

**Tests** (all test paths below are relative to
`Packages/com.alrauna.amuse/`)

| File | Change |
|---|---|
| `Tests/Editor/Analysis/ExactUvEnvelopeTests.cs` **(+meta)** | new: geometry member tests (Task 1) |
| `Tests/Editor/Analysis/AffineUvTransformTests.cs` **(+meta)** | new: tier-table and envelope-golden tests (Task 2) |
| `Tests/Editor/Analysis/TriangleAlphaClassifierTests.cs` | migrate every `Classify(...)` call to the new signature (helpers at ~`:471,490,510,529` and direct sites) passing `AlphaUvEnvelope.Zero`; add envelope boundary fixtures (Task 3) |
| `Tests/Editor/Analysis/AlphaSemanticsResolverTests.cs` | rewrite `UnsupportedUvMappingRefuses` to the new boundary (channel-only) per F23 — assertions become: channel ≠ 0 refuses; scale/offset variants resolve; add identity-parity and non-identity classification tests; migrate the nine direct `AlphaResolution.Classified(chain, sampling)` call sites (`:735-876`) to the new factory shape (Task 4) |
| `Tests/Editor/Analysis/AdmittedMaterialStatesTests.cs` | migrate the `ClassifiedResolution` helper (`:1045-1047`, used by `ClassifiedResolutionsNeverMerge`) to the new factory shape; extend the ST-admission block: re-asserted non-identity ST resolves *and* the slot's resolution is now classified (not `UnsupportedUvMapping`) (Task 4) |
| `Tests/Editor/Semantics/Poiyomi/PoiyomiBaseColorAlphaTests.cs` | one consumer test: non-identity ST material → semantic value carries the material's mapping and the resolution classifies (Task 4) |
| `Tests/Editor/Semantics/LilToon/LilToonCutoutAlphaTests.cs` | one consumer test: non-identity ST cutout material classifies; ScrollRotate nonzero still diagnostic-refuses (Task 4) |
| `Tests/Editor/Build/AlphaSeparationPreparationTests.cs` | one full-path scenario + source-preservation assertions (Task 4) |
| `Tests/Editor/Host/AlphaEvidenceClassifierIntegrationTests.cs` | migrate resolver/classifier call shapes if the signature change reaches it (mechanical, `AlphaUvEnvelope.Zero`) |

New-file metas are created together with the files (one logical unit); no
other `.meta`, asset, manifest, or lockfile change.

## Task decomposition

### Task 1 — exact geometry members (RED → GREEN)

1. **RED.** Write `ExactUvEnvelopeTests`: golden cases for `HalfUlp` (normal,
   subnormal-adjacent, power-of-two boundaries — dyadic expectations computed
   by hand from the binary32 definition), `EncodeToNearestFloat` (ties-to-even
   cases; error magnitude ≤ ½ ulp and exact), `IsExactlyRepresentable`
   (24-bit fit, overflow past 2^128, subnormal edge), `ConvexHull` (collinear
   → 2 points, CCW ordering, duplicates removed), `OutwardExpand` (zero
   early-out returns the identical reference; rectangle output for a degenerate
   1–2 vertex domain; inflation monotone: every original vertex inside the
   expanded hull). Run → all fail (members absent).
2. **GREEN.** Implement the five members in `ExactUvGeometry`. Rerun → pass.
3. Run `TriangleAlphaClassifierTests` → unchanged (no production behavior
   touched yet).

### Task 2 — `AffineUvTransform` (RED → GREEN)

1. **RED.** Write `AffineUvTransformTests` (type absent; compile-level RED is
   expected and acceptable for a new type):
   - Tier table (§7) per axis: E1 positive/negative power-of-two scales,
     zero offset, all-normal hulls; E1 boundary refusals into V — hull
     touching `2^-126`, straddling zero, input-subnormal with large scale;
     E2 zero scale both axes and one axis; E3 degenerate axis pointwise
     exact (and pointwise *inexact* → V); E1 normal-`s` guard — the
     subnormal power-of-two scale `s = 2^-127` over an all-normal hull must
     fall to V (F24); degenerate-axis subnormal `c` or `s` → V; V fractional
     scale, fractional and negative offsets.
   - Overflow guard: corner product ≥ 2^127 → `overflow == true`.
   - Envelope golden values (envelope tier): one fully worked fixture per
     term — `B_enc`, `B_st` (including the `2^-125` floor), `B_daz`
     `2^-126·(|s| + max|u|)` (with a subnormal-`s` and a subnormal-`u`
     amplification case), `B_noise`
     `2^-9·(1+|tx|+|ty|)` — asserted as exact rationals computed by hand;
     identity mapping → zero envelope, original triangle reference.
   - Non-identity exact tier → envelope exactly `B_noise` (and nothing else).
2. **GREEN.** Implement `AffineUvTransform` as a pure function. No
   production callers yet (compile-safe; full suite stays at baseline). Rerun
   → pass.

### Task 3 — classifier envelope plumbing (scaffold → RED → GREEN)
1. **Scaffold (behavior-neutral, genuinely necessary):** add the
   `AlphaUvEnvelope` struct and the 4th parameter to
   `TriangleAlphaClassifier.Classify` and to
   `ExactUvGeometry.CreateTextureScaledDomain`; migrate
   every caller (resolver's one call site, all test sites listed in the file
   map) passing `AlphaUvEnvelope.Zero`; `CreateTextureScaledDomain` accepts
   and ignores the envelope until step 3 (marked with a single-line comment
   naming Task 3 step 3 — no dead code beyond the task boundary). Full suite
   → baseline green (identity behavior unchanged through the zero envelope).
2. **RED.** Add envelope fixtures to `TriangleAlphaClassifierTests`
   (envelope non-zero, texture 8×8 with one non-opaque texel unless noted;
   expected outcomes computed by hand from the design's inflated-domain
   semantics):
   - Unit sanity: a fixture whose corners have a known exponent (hence a
     known `TexelScale`, e.g. `T = 4`) with an envelope of exactly half a
     UV texel must reach exactly the boundary cell — this pins the
     `X·width·T` unit conversion (fails under a `X·width` displacement).
   - Point/Clamp: envelope pulls a bordering non-opaque cell into range that
     the un-inflated hull misses → `MustRemainTransparent` (fails: inflation
     not implemented → returns `ProvenOpaque`).
   - Point/Repeat: transform + envelope crossing a period boundary — the
     boundary cell must be a candidate (F21: normalization order).
   - Bilinear/Clamp and Bilinear/Repeat: footprint at one-texel reach with
     inflated hull (F20: footprint width stays one texture texel).
   - All-mip: chain transparent only at mip ≥ 1, non-zero envelope →
     `Unknown`/transparent, never `ProvenOpaque` (F4).
   - Budget: inflated huge-scale Repeat domain over `MaxSupportRegions` →
     `Unknown` (F16).
   - Degenerate: 2-vertex domain inflated to a rectangle classifies; a
     degenerate *mesh* still returns `Unknown` first (F18).
   Expected pre-fix failures: every non-zero-envelope case above returns the
   un-inflated outcome.
3. **GREEN.** Implement inflation in
   `ExactUvGeometry.CreateTextureScaledDomain` via
   `OutwardExpand` (zero early-out preserves identity parity structurally).
   All four mode paths then consume the inflated domain unchanged. Rerun →
   pass, full suite green.

### Task 4 — resolver widening, families, animation, preparation (RED → GREEN)

1. **RED batch** (write all before any production edit; each fails for the
   named reason):
   - `AlphaSemanticsResolverTests`: non-identity mappings resolve and
     classify — scale `(2,2)`/offset `0` (exact tier), scale `(2,2)` +
     offset `(0.5,0.25)` (envelope tier), negative scale `(−2,2)`, zero scale
     `(0,0.5)`, per-mode × wrap matrix reusing the existing
     `Sample(filter, wrap, ...)` helper (F1/F2/F3/F17; all four
     Point/Bilinear × Clamp/Repeat). **Fails:** current gate returns
     `UnsupportedUvMapping` for every non-identity case.
   - `AlphaSemanticsResolverTests`: identity parity — for a fixed mixed
     field and triangle set, identity resolution classifies identically to
     the pre-change oracle values already asserted by
     `SampledAlphaDelegatesToTheClassifier` (F13). **Fails:** only if the
     rewrite disturbs identity (expected to pass only after GREEN; recorded
     as a pinned invariant, verified in the mutation phase).
   - `AlphaSemanticsResolverTests`: `UnsupportedUvMappingRefuses` rewritten —
     channel 1 refuses; scale/offset variants now expected `IsResolved`
     (F23). **Fails:** they refuse today.
   - `AlphaSemanticsResolverTests`: overflow fixture — mapping whose corner
     product ≥ 2^127 → resolved but `Classify` → `Unknown`, never
     `ProvenOpaque` (F11/F12).
   - `PoiyomiBaseColorAlphaTests`: material with `_MainTex` ST
     `(2,2,0.5,0.25)`, pan zero, channel 0 → alpha value complete, mapping
     equals the material's ST, resolution classifies a triangle that is
     opaque only under the true transform (F1/F2, one Poiyomi consumer).
     **Fails:** resolver refuses.
   - `LilToonCutoutAlphaTests`: cutout fixture with `_MainTex` ST
     `(2,1,0,0)`, all gates default → alpha complete and classified;
     ScrollRotate `(0,0.1,0,0)` variant still diagnostic-refuses. **Fails:**
     resolver refuses (one lilToon consumer).
   - Cross-family same-input equality (F15): the SAME captured
     `(UvMapping, TriangleAlphaInput, AlphaMipChain, sampling)` — built once,
     e.g. ST `(2,2,0.5,0.25)` — is classified through the Poiyomi semantic
     value and the lilToon cutout semantic value; the two outcomes must be
     identical for every triangle in a fixed set. **Fails:** only if the
     resolver ever special-cases family.
   - `AdmittedMaterialStatesTests`: re-asserted non-identity ST `(2,3)`
     (existing fixture scale) resolves and the slot's `AlphaResolution` is
     classified — singleton animation (Task's required singleton case).
     **Fails:** resolution is refused today.
   - `AlphaSeparationPreparationTests`: full-path scenario — cutout fixture
     material with non-identity ST such that (a) a triangle transparent at
     identity is proven opaque under the transform and migrates in the plan,
     (b) an ST-animated-at-serialized-default variant prepares, (c) a
     non-singleton `_MainTex_ST` clip refuses (`AnimatedMaterialPropertyNotSingleton`),
     and (d) source material/texture/mesh assets are bit-unchanged afterward
     (structural audit, mirroring the existing no-mutation tests). **Fails:**
     (a) yields no opaque triangles (all `Unknown` through the refused
     resolution); (b) refuses outright.
2. **GREEN (single atomic increment):**
   - `IsSupportedMapping` → channel-only; rewrite its doc comment to the
     implemented §7 predicate with the design reference.
   - `AlphaResolution` stores the mapping; `Classified` gains the parameter;
     `ResolveSampled` passes it.
   - `AlphaResolution.Classify`: identity short-circuit → original triangle +
     zero envelope; else `AffineUvTransform` → `overflow` → per-triangle
     `Unknown`; else loop mips with the envelope.
   - Update the resolver-file obligation comment (identity-only wording) to
     the two-tier contract; no comment elsewhere references identity-ST as a
     gate (grep-verified in Task 6 review).
3. Run the RED batch → all green. Full product suite → green.

### Task 5 — mutation checks (prove the tests bite)

For each falsifier, apply the named wrong implementation locally, run the
mapped test, observe failure, revert. No commits at any point:

| Falsifier | Local mutation | Test that must fail |
|---|---|---|
| F1 scale ignored | `AffineUvTransform` passes `s = 1` | resolver scale test |
| F2 offset ignored | passes `o = 0` | resolver offset test |
| F3 abs scale | `s = |s|` in transform | negative-scale test |
| F4 mip-0-only | resolver applies transform only for `index == 0` | all-mip test |
| F5 wrap-after | normalize domain then inflate after `NormalizeRepeat` | Point/Repeat boundary test |
| F6 untransformed footprint | classifier computes footprint from un-inflated hull | Bilinear envelope tests |
| F7 boundary cell loss | `NormalizeRepeat` uses `FloorDiv` on un-inflated minima | Point/Repeat boundary test |
| F8/F10 exact-real-for-runtime | skip inflation when tier == envelope | envelope boundary tests |
| F9 double-as-exact | compute envelope with `double` rounding | `AffineUvTransformTests` golden values |
| F11 overflow usable | remove 2^127 guard | overflow test |
| F12 failure promoted | map budget overflow to `ProvenOpaque` | budget test |
| F13 identity changed | apply `B_noise` to identity | identity parity test |
| F14 non-singleton admitted | loosen `AdmitVector` equality for `_ST` | existing non-singleton refusal test (must still fail-open) |
| F15 family divergence | no code mutation exists — the resolver is family-blind by construction; the check is the cross-family same-input equality test (Task 4), which any future family-special-casing (e.g. branching on `CapturedAlphaMaterial.Family` inside the resolver) must fail | cross-family same-input equality test |
| F16 complexity-as-empty | budget overflow → skip loop (vacuous) | budget test |
| F17 zero-scale-as-missing-UV | return `Unknown` for E2 axes | zero-scale test |
| F18 degenerate confusion | treat degenerate UV hull as geometry-degenerate | degenerate tests |
| F19 component swap | transpose scale/offset application in `AffineUvTransform` | the `AffineUvTransformTests` tier goldens with scale `(2, 3)` and offset `(0.5, 0.25)` (per-axis crossing), plus the resolver offset test |
| F20 scaled footprint | multiply footprint width by `|s|` | Bilinear one-texel-reach test |
| F21 inflate-after-normalize | reorder in `ClassifyPointRepeat` | Point/Repeat boundary test |
| F22 wider-type | `double` transform in `EncodeToNearestFloat` | geometry encode tests |
| F23 hollowed tests | delete rewritten refusal tests | review check: file map diff shows rewrites, not deletions |
| F24 subnormal DAZ amplification | drop E1/E3 normal-`s` guard | the `s = 2^-127` tier test (must fall to V with `B_daz`) and its `B_daz` golden |

### Task 6 — full validation, diff review, cleanup

1. Focused: `run_tests` EditMode `group_names` for each touched class;
   `get_test_job` to terminal state; assert counts.
2. Full: `run_tests` EditMode `assembly_names:["Alrauna.Amuse.Tests.Editor"]`
   then `["Alrauna.Amuse.Research.Tests.Editor"]`; both 0 failed; console
   clean of new errors/warnings (`read_console get types:[error,warning]`).
3. Source preservation: the Task 4 preparation test's structural audit;
   additionally confirm no asset/`.meta` diff outside the two new-file pairs.
4. Diff review: `git diff` (unstaged only — nothing staged), `git diff --check`,
   `git status --short --branch`; confirm exactly the file map, plus the
   preserved package-file churn; confirm nothing staged.
5. Documentation: this plan and the spec need no update unless a stop
   condition fired; if implementation revealed a spec error, stop and report
   instead of editing silently.

## Test execution protocol

```
<Unity MCP gate — see preconditions>
read_console clear
job = run_tests EditMode group_names:["<full test class name>"]
get_test_job job_id=<job>        # poll to terminal; assert passed/failed counts
read_console get types:[error,warning]
```

Full suites replace `group_names` with `assembly_names`. Never overlap two
test jobs. If Unity is not running, the session stops at the gate and
reports; no validation may be claimed without an observed run.

## Expected pre-fix failures (summary)

- Task 1 RED: geometry member tests fail to compile (members absent) —
  accepted RED for new members.
- Task 2 RED: `AffineUvTransformTests` fail to compile (type absent).
- Task 3 RED: every non-zero-envelope fixture returns the un-inflated
  outcome (concrete wrong outcomes listed per fixture).
- Task 4 RED: resolver non-identity/classify tests fail with
  `UnsupportedUvMapping` refusals; consumer tests fail with Unknown alpha or
  refusal diagnostics; preparation scenario fails with no opaque migration;
  animation-at-serialized variant refuses.

## Git authorization boundary

- Implementation-branch creation and switching MAY be authorized at
  implementation time; this plan assumes nothing.
- NEVER stage, commit, amend, push, open or merge a PR, tag, publish, delete
  branches, or touch remotes/settings. All work remains unstaged in the
  working tree for review. The preserved package-file churn is never staged.

## Expected implementation report

1. Branch name and base commit; Unity instance identity used.
2. Files changed/added vs. the file map; any deviation and why.
3. RED evidence: observed pre-fix failures per task (test names + failure
   modes), GREEN evidence: observed passing runs with counts.
4. Mutation-check table: mutation applied → test failed → reverted (F1–F23).
5. Full-suite results (both assemblies, counts), console state.
6. Diff summary: `git status --short --branch`, `git diff --check` output;
   confirmation that `Packages/manifest.json`/`packages-lock.json` carry only
   the pre-existing churn and nothing is staged.
7. Any stop condition hit, with evidence, or an explicit "none".
