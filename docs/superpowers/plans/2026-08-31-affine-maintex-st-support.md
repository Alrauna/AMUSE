# Affine `_MainTex_ST` support — implementation plan

This plan implements `docs/superpowers/specs/2026-08-31-affine-maintex-st-support-design.md`. That document is the accepted design, revision 3. Section references `§n` below point to it. This plan does not reopen any design decision. The historical identity-ST records (`docs/superpowers/specs/2026-08-30-liltoon-cutout-opaque-conversion-design.md`, `docs/superpowers/plans/2026-08-30-liltoon-cutout-opaque-conversion.md`) are read-only context.

Revision 3 of this plan tracks the design repair:

- `AlphaUvEnvelope` moves into Task 1, so every GREEN checkpoint compiles.
- The tier tests follow the path-independent Lemma P predicate.
- The family boundary adds one lilToon frontend gate.
- The identity assertion becomes field-level bit equality.
- The falsifier set gains F25 and F26.

| Field | Value |
|---|---|
| Tech Stack | Unity 2022.3.22f1, NDMF (pinned, embedded), NUnit EditMode, pinned lilToon 2.3.4 and Poiyomi 9.3.64 facts (neither vendor package present in this project) |
| Production files | 5 changed, 1 added (+meta) — exact map in §FileMap |
| Test files | 6 changed, 2 added (+meta) — exact map in §FileMap |
| Branch | created later, with explicit authorization (§Git) |
| Commit/push | **never** assumed. Nothing in this plan stages or commits |

## Base and branch preconditions

1. Work in the repository root.
2. Implement on a **new focused branch** off `main` **at or after `89cc5be`** (the merge of PR #41). Suggested name: `feature/affine-maintex-st-support`. Branch creation and switching require authorization at implementation time. If the harness owns branch flow, follow that flow.
3. `Packages/manifest.json` and `Packages/packages-lock.json` carry user-owned Unity toolchain/sysroot churn. Never stage, restore, or absorb them. Before you finish, inspect the complete diff of both files. Confirm they differ from `main` only by that churn.
4. Run `git status --short --branch` before you start. Record the baseline.
5. Unity MCP gate (before any editor interaction, every session):
   - Enumerate the instances read-only.
   - Read `Application.dataPath`, and normalize the path.
   - Require an exact match to `<repo-root>/Assets` (a case-only match does not count).
   - Pin the instance when more than one is reachable.
   No validation claim stands without an observed run.
6. Before Task 4, re-check the Poiyomi source facts that the architecture uses (design §3.2, stop condition S2). Fetch `_PoiyomiShaders/Shaders/9.3/Toon/Poiyomi Toon.shader` at commit `e125e1c33cbfb860f59330799dd4d10a1097242d`. Confirm the SHA-256 `31f2ff…b1755` equals `PoiyomiMaterialSemantics.CanonicalNormalizedSourceHash`. Confirm the cited lines still read as §3.2 quotes them. Never add vendor bytes to the repository.

## File map (complete)

**Production**

| File | Task | Change |
|---|---|---|
| `Packages/com.alrauna.amuse/Editor/Analysis/ExactUvGeometry.cs` | 1 | Add `AlphaUvEnvelope` and six members. `AlphaUvEnvelope` is a small readonly struct: `ExactRational X, Y`, `static Zero`. **Declare it here, in Task 1, because `AffineUvTransform` returns it in Task 2.** The members: `HalfUlp(ExactDyadic)` gives ½ ulp of a binary32 value as a dyadic. `EncodeToNearestFloat(ExactRational)` returns `(float value, ExactRational error)`. `IsExactBinary32(ExactDyadic)` checks the 24-bit significand fit **and** the exponent range. `IsNormalOrZeroBinary32(ExactDyadic)` checks zero, or `2^-126 ≤ |v| ≤ (2−2^-23)·2^127`. `ConvexHull(IReadOnlyList<ExactUvPoint>)` is an exact monotone chain with a **denominator-correct orientation test**. The test cross-multiplies denominators. It must not reuse `Cross`, which differences numerators only and is correct solely for the unit-denominator vertices that the current pipeline produces. `OutwardExpand(ExactUvDomain, AlphaUvEnvelope)` gives the hull of every vertex offset by `(±X, ±Y)`. The zero early-out returns the same domain object |
| `Packages/com.alrauna.amuse/Editor/Analysis/ExactUvGeometry.cs` | 3 | `CreateTextureScaledDomain` gains an `AlphaUvEnvelope` parameter. It inflates by `(X·width·T, Y·height·T)` with `T = domain.TexelScale`, strictly before `NormalizeRepeat` |
| `Packages/com.alrauna.amuse/Editor/Analysis/AffineUvTransform.cs` **(+meta)** | 2 | New pure static helper: `bool TryTransform(UvMapping, TriangleAlphaInput, out TriangleAlphaInput transformed, out AlphaUvEnvelope envelope)`. It returns `false` **iff** the §6.1 step 4 overflow guard trips. The caller then returns per-triangle `Unknown`. Every axis lands in a tier, so no other failure mode exists. The helper implements §7 (Lemma P + E1/E2/E3/V per axis) with `IsExactBinary32`/`IsNormalOrZeroBinary32`, the corner encode, the envelope terms `B_enc`/`B_st`/`B_daz`, and the identity short-circuit (verbatim field copy). The §6.2 constants become named dyadic constants with derivation comments. The comments cover the `2^-125` result-flush floor and the `+2^-126` offset-flush sub-term of `B_daz`. **No family-fragment term exists.** For every admitted consumer, `F ≡ 0` by design §3.2. The exact tier envelope is therefore exactly `AlphaUvEnvelope.Zero` |
| `Packages/com.alrauna.amuse/Editor/Analysis/TriangleAlphaClassifier.cs` | 3 | `Classify` gains a 4th parameter `AlphaUvEnvelope envelope`. The method threads it into the four `ExactUvGeometry.CreateTextureScaledDomain` call sites (`:238`, `:290`, `:355`, `:453`). The four mode paths keep their signatures and pass the inflated domain through unchanged |
| `Packages/com.alrauna.amuse/Editor/Analysis/AlphaSemanticsResolver.cs` | 4 | `IsSupportedMapping` narrows to `mapping.Channel == 0`. Rewrite its doc comment to cite the design: the "wider floating point is not such a proof" obligation text becomes a pointer to the implemented Lemma P predicate. `AlphaResolution` stores the `UvMapping`. The `Classified(chain, sampling, mapping)` factory gains the parameter. `Classify` short-circuits identity **before** any tier work. Otherwise it calls `AffineUvTransform`, returns `Unknown` when the guard trips, and passes the envelope into the mip loop |
| `Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonCutoutMaterialSemantics.cs` | 4 | Add `MainTexStProperty = "_MainTex_ST"` (diagnostic detail only). Add one gate in `InterpretCutoutAlpha`, immediately after the `HasScaleOffset` check (`:321-328`): a non-identity `assignment.Scale`/`assignment.Offset` records `LilToonSemanticDiagnosticCode.UnsupportedUv` with detail `_MainTex_ST` and returns `Unknown` (design §6.1 step 5, C4). **Compare identity per component with exact binary32 tests** (`Scale.x != 1f \|\| Scale.y != 1f \|\| Offset.x != 0f \|\| Offset.y != 0f`). Do not use Unity's epsilon-based `Vector2 ==`/`!=` here. It admits near-identity ST into the family-blind resolver. Rewrite the `:284-291` comment, which currently delegates the refusal to the resolver. Scope: the cutout **alpha** interpretation only |
| `Packages/com.alrauna.amuse/Editor/Semantics/MaterialSemantics.cs` | 4 | Doc-comment only. State the C4 emission invariant on `UvMapping`: a frontend may emit a non-identity mapping for an alpha-relevant sample only under one condition. Its attested source must prove the sampler coordinate is the binary32 affine image. The proof must exclude further unbounded fragment arithmetic. No code change |

**No other production file changes** *within Tasks 1-4.* Capture, admission, `MeshSeparationPlanner`, `UnityRendererAlphaAnalysis`, apply, conversion, the other lilToon mapping builders (`LilToonMaterialSemantics.cs:381, 831, 1004`), and every Poiyomi frontend gate stay unchanged. If implementation reveals a need there, that is stop condition S5 (design §16).

**Tests** (all test paths below are relative to `Packages/com.alrauna.amuse/`)

| File | Task | Change |
|---|---|---|
| `Tests/Editor/Analysis/ExactUvEnvelopeTests.cs` **(+meta)** | 1 | New: geometry member and `AlphaUvEnvelope` tests |
| `Tests/Editor/Analysis/AffineUvTransformTests.cs` **(+meta)** | 2 | New: tier-table, envelope-golden, and identity-parity tests |
| `Tests/Editor/Analysis/TriangleAlphaClassifierTests.cs` | 3 | Migrate every `Classify(...)` call to the new signature, and pass `AlphaUvEnvelope.Zero` (helpers at ~`:471, :490, :510, :529` and direct sites). Add the envelope boundary fixtures |
| `Tests/Editor/Analysis/AlphaSemanticsResolverTests.cs` | 3, 4 | Task 3 migrates the **three** three-argument `TriangleAlphaClassifier.Classify` oracle calls (`:316-317`, `:397-398`, `:477-478`) to pass `AlphaUvEnvelope.Zero`. Task 4 rewrites `UnsupportedUvMappingRefuses` to the channel-only boundary (F23). Task 4 adds identity-parity and non-identity classification tests. Task 4 migrates the nine direct `AlphaResolution.Classified(chain, sampling)` sites (`:735-876`) |
| `Tests/Editor/Analysis/AdmittedMaterialStatesTests.cs` | 4 | Migrate the `ClassifiedResolution` helper (`:1045-1047`, used by `ClassifiedResolutionsNeverMerge`) to the new factory shape. Extend the ST-admission block **within the fixture's reality**. `ForcedOpaqueMaterialWithScaleOffset` (`:859`) is a forced-opaque Poiyomi material. Its alpha is a *constant*, so its resolution is uniform. The re-assertion tests keep asserting admission only. The expectations stay unchanged, and the widened resolver now exercises them. Add one texture-backed non-identity-ST Poiyomi case. The case asserts that the slot resolution `IsResolved` holds and is not uniform. Use the same texture-import helper that `PoiyomiBaseColorAlphaTests` uses. If that harness cannot import textures, cover the classification claim in `AlphaSemanticsResolverTests`/`PoiyomiBaseColorAlphaTests` only. Record the reduction in the report |
| `Tests/Editor/Semantics/Poiyomi/PoiyomiBaseColorAlphaTests.cs` | 4 | One consumer test. A non-identity ST material gives a complete alpha value. The mapping equals the material's ST. The resolution classifies a triangle that is opaque only under the true transform |
| `Tests/Editor/Semantics/LilToon/LilToonCutoutAlphaTests.cs` | 4 | Non-identity `_MainTex` scale/offset gives alpha `Unknown` with one `UnsupportedUv` diagnostic naming `_MainTex_ST` (the new C4 gate, F15). Cover a far-from-identity fixture, and add the parameterized `EveryNearIdentityNonIdentityMainTexStComponentIsRejectedExactly` with four near-identity component cases (`scale.x = 1.000005f`, `scale.y = 1.000005f`, `offset.x = 0.000005f`, `offset.y = 0.000005f`, all others at exact identity). The cases sit inside Unity's approximate-equality ball. The identity ST fixture stays unchanged (parity). The existing `ScrollRotate` refusal test stays as-is |
| `Tests/Editor/Build/AlphaSeparationPreparationTests.cs` | 4 | One full-path **Poiyomi** scenario, one lilToon non-identity boundary scenario, and source-preservation assertions |

Verified unaffected (grep-audited): `Tests/Editor/Host/AlphaEvidenceClassifierIntegrationTests.cs`. It uses only `AlphaSemanticsResolver.Resolve(alpha, …)` (`:111`) and the unchanged instance `resolution.Classify(triangle)` (`:150, :166, :182, :185, :301`). No changed signature reaches it. No edit is required.

### Task 6 - controller-authorized exact-comparison repair (2026-09-01)

A Task 6 diff review examined the lilToon cutout `_MainTex_ScrollRotate` gate. The gate was written `scrollRotate != Vector4.zero`. Unity 2022.3's `Vector3`/`Vector4` `==`/`!=` compare squared Euclidean distance against `0.00001f * 0.00001f`. A near-zero component thus passed a gate documented as exact. lilToon evaluates `lilRotateUV(outuv, uv_sr.z + uv_sr.w * LIL_TIME) + frac(uv_sr.xy * LIL_TIME)`. In that expression, `z` is a static rotation, `x`/`y`/`w` are time-varying, and magnitude establishes nothing. A production audit found seven more proof-relevant aggregate comparisons with the same root cause. The controller authorized this file-map expansion. The expansion is bounded to these eight comparisons.

| File | Former comparison | Now |
|---|---|---|
| `Editor/Semantics/LilToon/LilToonCutoutMaterialSemantics.cs` | cutout `scrollRotate != Vector4.zero` | four exact `!= 0f` component tests |
| `Editor/Semantics/LilToon/LilToonMaterialSemantics.cs` | `_MainTexHSVG != IdentityHsvg` | four exact per-component tests against `IdentityHsvg` |
| `Editor/Semantics/LilToon/LilToonMaterialSemantics.cs` | main `scrollRotate != Vector4.zero` | four exact `!= 0f` component tests |
| `Editor/Semantics/LilToon/LilToonMaterialSemantics.cs` | base-color `tint == Vector3.one` | `tint.x == 1f && tint.y == 1f && tint.z == 1f` |
| `Editor/Semantics/LilToon/LilToonMaterialSemantics.cs` | emission `scrollRotate != Vector4.zero` | four exact `!= 0f` component tests |
| `Editor/Semantics/LilToon/LilToonMaterialSemantics.cs` | emission `tint == Vector3.one` | exact three-component test |
| `Editor/Semantics/Poiyomi/PoiyomiMaterialSemantics.cs` | base-color `tint == Vector3.one` | exact three-component test |
| `Editor/Semantics/Poiyomi/PoiyomiMaterialSemantics.cs` | emission `tint == Vector3.one` | exact three-component test |

Diagnostic codes, details, gate ordering, finite checks, output vocabulary, and `-0.0f` admission stay unchanged. The repair introduced no helper, no tolerance abstraction, and no comparison framework.

Eight ordinary `[Test]` methods defend the repair. Each method loops over its components with case-specific assertion messages:

| File | Test |
|---|---|
| `Tests/Editor/Semantics/LilToon/LilToonCutoutAlphaTests.cs` | `EveryNearZeroMainTexScrollRotateComponentIsRejectedExactly` |
| `Tests/Editor/Semantics/LilToon/LilToonBaseColorTests.cs` | `EveryNearIdentityMainTexHsvgComponentIsRefusedExactly` |
| `Tests/Editor/Semantics/LilToon/LilToonBaseColorTests.cs` | `EveryNearZeroMainTexScrollRotateComponentIsRefusedExactly` |
| `Tests/Editor/Semantics/LilToon/LilToonBaseColorTests.cs` | `NearOneMainTexTintStaysAnExactTextureMultiplier` |
| `Tests/Editor/Semantics/LilToon/LilToonEmissionTests.cs` | `EveryNearZeroEmissionMapScrollRotateComponentIsRefusedExactly` |
| `Tests/Editor/Semantics/LilToon/LilToonEmissionTests.cs` | `NearOneEmissionTintStaysAnExactTextureMultiplier` |
| `Tests/Editor/Semantics/Poiyomi/PoiyomiBaseColorAlphaTests.cs` | `PoiyomiBaseColorTests.NearOneTintStaysAnExactTextureMultiplier` |
| `Tests/Editor/Semantics/Poiyomi/PoiyomiEmissionTests.cs` | `NearOneTintStaysAnExactTextureMultiplier` |

Each test asserts two fixture preconditions before the behavioral assertion. The value differs from identity under exact binary32 comparison. Unity's aggregate operator still reports the value equal. Scroll/rotate and HSVG fixtures perturb one component by `5e-6`. The lilToon/Poiyomi base-color and lilToon emission tint fixtures use serialized `0.999999` sRGB red, which decodes to `0.9999979` linear. The Poiyomi emission fixture uses `_EmissionStrength` `1.000003f` on a white color.

The emission scroll/rotate gate lives inside `TryGetEmissionUvMapping`. Its caller reports `UnsupportedUv` with detail `_EmissionMap_UVMode`. The tests pin that existing detail. They do not change it.

Final expected product-assembly count after this repair: **1556** (1548 plus the eight new methods). Research assembly is unaffected at **138**.

The implementation creates each new-file meta together with its file (one logical unit). Nothing else changes: no `.meta`, no asset, no manifest, and no lockfile.

## Task decomposition

The dependency order makes every GREEN checkpoint compile. `AlphaUvEnvelope` (Task 1) precedes `AffineUvTransform`, which returns it (Task 2). The classifier parameter (Task 3) precedes the resolver call that supplies it (Task 4).

### Task 1 — `AlphaUvEnvelope` and exact geometry members (RED → GREEN)

1. **RED.** Write `ExactUvEnvelopeTests` with golden cases:
   - `HalfUlp`: normal, subnormal-adjacent, and power-of-two boundaries. Compute the dyadic expectations by hand from the binary32 definition.
   - `EncodeToNearestFloat`: ties-to-even cases. The error magnitude is ≤ ½ ulp and exact.
   - `IsExactBinary32`: 24-bit fit, 25-bit miss, overflow past the binary32 maximum finite value, subnormal edge.
   - `IsNormalOrZeroBinary32`: zero true, `2^-126` true, `2^-127` false, maximum finite true, above it false.
   - `ConvexHull`: collinear input gives 2 points, CCW ordering, and duplicates removed. Add a mixed non-unit-denominator case with vertices of differing reduced denominators. The case proves that the orientation test is denominator-correct.
   - `AlphaUvEnvelope.Zero`: both components exactly zero.
   - `OutwardExpand`: a zero envelope returns the identical domain reference. A degenerate domain that is a single point or an axis-aligned segment gives rectangle output. The hull is the exact Minkowski sum, so a diagonal segment yields the exact hexagon. The hexagon is tighter than a bounding rectangle. The existing two-point fixture is axis-aligned and remains valid. The inflation is monotone: every original vertex stays inside the expanded hull.
   Run the tests. All fail (type and members absent).
2. **GREEN.** Implement `AlphaUvEnvelope` and the six members in `ExactUvGeometry`. Rerun → pass.
3. Run `TriangleAlphaClassifierTests` → unchanged (no production behavior touched yet).

### Task 2 — `AffineUvTransform` (RED → GREEN)

1. **RED.** Write `AffineUvTransformTests`. The type is absent, so compile-level RED is expected and acceptable for a new type:
   - Tier table (§7) per axis:
     - E1: positive and negative power-of-two scales with zero offset over all-normal hulls.
     - E1 boundary refusals into V: a hull touching `2^-126`, a hull straddling zero, a hull containing a subnormal with a large scale, and an image leaving the normal range.
     - E1 normal-`s` guard: the subnormal power-of-two scale `s = 2^-127` over an all-normal hull falls to V (F24).
     - E2: zero scale on both axes and on one axis, **with a subnormal nonzero `o` falling to V** (F24).
     - E3 exact: a degenerate axis satisfying Lemma P (e.g. `c = 0.25`, `s = 3`, `o = 0.5`).
     - E3 refusals into V, three cases: non-representable `c·s`, non-representable `c·s + o`, and subnormal `c`/`s`/`o` or subnormal `c·s`.
     - V: fractional scale, fractional offsets, and negative offsets on non-degenerate axes.
   - **F26** compensating-rounding degenerate axis. Use design §15 F26's constructed case: `c = 1 + 2^-12`, `s = 1 + 2^-12`, `o = 3·2^-24`. The exact `c·s = 1 + 2^-11 + 2^-24` needs 25 significand bits. The axis must therefore be **V** with a non-zero envelope. The round-to-nearest serial evaluation returns the exact `c·s + o = 1 + 2^-11 + 2^-22`.
   - Asymmetric per-axis crossing golden (F19): scale `(2, 3)`, offset `(0.5, 0.25)`. The expected transformed corners are per-axis-distinct, and the envelopes are per-axis-distinct. The test fails any component swap.
   - Overflow guard: an exact corner product or sum ≥ 2^127 → `TryTransform` returns false.
   - Envelope golden values (envelope tier). Assert them as exact rationals computed by hand. Give one fully worked fixture per term: `B_enc`, `B_st` (include a case that exercises the `2^-125` result-flush floor), and `B_daz` `2^-126·(|s| + max|u| + 1)` (with a subnormal-`s` case, a subnormal-`u` case, and a subnormal-`o` case).
   - **F25** cancellation goldens (design §15 F25). Use the adjacent-float hull `u ∈ {1, 1+2^-23}` with `s = 2^20, o = −2^20` (fixture A: `M_exact = 2^-3`, `P = 2^20 + 2^-3`, `B_st ≈ 2^-2` UV). Use it also with `s = 2^40, o = −2^40` (fixture B: `M_exact = 2^17`, `P = 2^40 + 2^17`, `B_st ≈ 2^18` UV). Assert the envelope equals `B_enc + B_st + B_daz` with `B_st = 2^-22·(P + M_exact) + 2^-125` and `P = max|u·s|`. An implementation with `M_exact` alone yields ≈ `2^-24` UV on fixture A. It fails by a factor of ≈ 2^22.
   - Exact tier → envelope exactly `AlphaUvEnvelope.Zero` (no family-fragment term exists).
   - Identity parity (F13). For two fixtures, the identity mapping returns `AlphaUvEnvelope.Zero` and a `TriangleAlphaInput` whose `HasUv0` matches and whose fifteen float fields are **bit-identical** under `BitConverter.SingleToInt32Bits`. Fixture one has a hull that contains exactly `0.0f`. That input would fall to V through the general path. Fixture two contains `−0.0f`. The general path re-encodes it to `+0.0f`. Deliberately do **not** assert reference identity: `TriangleAlphaInput` is a readonly struct.
2. **GREEN.** Implement `AffineUvTransform` as a pure function. It has no production callers yet. The state is compile-safe, and the full suite stays at baseline. Rerun → pass.

### Task 3 — classifier envelope plumbing (scaffold → RED → GREEN)

1. **Scaffold (behavior-neutral, genuinely necessary).** Add the 4th parameter to `TriangleAlphaClassifier.Classify`. Add the envelope parameter to `ExactUvGeometry.CreateTextureScaledDomain`. Migrate every caller, and pass `AlphaUvEnvelope.Zero`. The callers: the resolver's one call site, the `TriangleAlphaClassifierTests` helpers and direct sites, and the three three-argument oracle calls in `AlphaSemanticsResolverTests` at `:316-317`, `:397-398`, `:477-478`. `CreateTextureScaledDomain` accepts and ignores the envelope until step 3. Mark that with a single-line comment naming Task 3 step 3. No dead code goes beyond the task boundary. Run the full suite → baseline green (identity behavior unchanged through the zero envelope).
2. **RED.** Add envelope fixtures to `TriangleAlphaClassifierTests`. The envelope is non-zero. The texture is 8×8 with one non-opaque texel, unless noted. Compute the expected outcomes by hand from the design's inflated-domain semantics:
   - Unit sanity: a fixture with corners of known exponent (hence a known `TexelScale`, e.g. `T = 4`) and an envelope of exactly half a UV texel reaches exactly the boundary cell. This pins the `X·width·T` unit conversion. It fails under an `X·width` displacement.
   - Point/Clamp: the envelope pulls a bordering non-opaque cell into range that the un-inflated hull misses → `MustRemainTransparent` (fails: inflation not implemented → returns `ProvenOpaque`).
   - Point/Repeat: transform + envelope crossing a period boundary. The boundary cell must be a candidate (F21: normalization order).
   - Bilinear/Clamp and Bilinear/Repeat: footprint at one-texel reach with inflated hull (F20: footprint width stays one texture texel).
   - Budget: an inflated huge-scale Repeat domain over `MaxSupportRegions` → `Unknown` (F16).
   - Degenerate: an axis-aligned 2-vertex domain inflates to its rectangle, and a diagonal segment inflates to its exact hexagon. A texel inside the bounding rectangle but outside the hexagon stays `ProvenOpaque`. A degenerate *mesh* still returns `Unknown` first (F18).
   Task 3 proves per-level envelope semantics only. Task 4 proves that one transformed triangle and its envelope apply to every mip. Expected pre-fix failures: every non-zero-envelope case above returns the un-inflated outcome.
3. **GREEN.** Implement inflation in `ExactUvGeometry.CreateTextureScaledDomain` with `OutwardExpand`. The zero early-out preserves identity parity structurally. All four mode paths then consume the inflated domain unchanged. Rerun → pass, full suite green.

### Task 4 — resolver widening, family boundary, animation, preparation (RED → GREEN)

1. **RED batch.** Write all tests before any production edit. Each fails for the named reason:
   - `AlphaSemanticsResolverTests`: non-identity mappings resolve and classify. Cases: scale `(2,2)`/offset `0` (exact tier), scale `(2,2)` + offset `(0.5,0.25)` (envelope tier), negative scale `(−2,2)`, zero scale `(0,0.5)`, and a per-mode × wrap matrix. Reuse the existing `Sample(filter, wrap, …)` helper (F1/F2/F3/F17, all four Point/Bilinear × Clamp/Repeat). **Fails:** the current gate returns `UnsupportedUvMapping` for every non-identity case.
   - `AlphaSemanticsResolverTests`: identity parity. For a fixed mixed field and triangle set, identity resolution classifies identically to the pre-change oracle values. `SampledAlphaDelegatesToTheClassifier` already asserts those values (F13). Include a triangle whose hull contains `0.0f`.
   - `AlphaSemanticsResolverTests`: rewrite `UnsupportedUvMappingRefuses`. Channel 1 refuses. The scale/offset variants now expect `IsResolved` (F23). **Fails:** they refuse today.
   - `AlphaSemanticsResolverTests`: overflow fixture. A mapping whose exact corner product ≥ 2^127 → resolved, but `Classify` → `Unknown`, never `ProvenOpaque` (F11/F12).
   - `AlphaSemanticsResolverTests`: **F25** end-to-end cancellation. Use the adjacent-float hull `u ∈ {1, 1+2^-23}` with `s = 2^20`, `o = −2^20` over a texture. The texture's non-opaque texel lies inside the correct `≈2^-2` UV envelope and outside an `M_exact`-derived one. Expected outcome: `MustRemainTransparent`. The `s = 2^40` variant never gives `ProvenOpaque` (`Unknown` via the region budget is acceptable).
   - `AlphaSemanticsResolverTests`: **F4 all-mip envelope propagation**. Use a non-identity mapping and a triangle with a non-zero envelope. Mip 0 remains opaque under correct transformed classification. Mip 1 or later has a non-opaque texel that only the envelope reaches. Direct classifier controls prove the transformed triangle is `ProvenOpaque` with `AlphaUvEnvelope.Zero` and `MustRemainTransparent` with the computed envelope. The resolver assertion is exactly `MustRemainTransparent`. It must fail if a future implementation transforms or applies the envelope only at `index == 0`.
   - `PoiyomiBaseColorAlphaTests`: a material with `_MainTex` ST `(2,2,0.5,0.25)`, pan zero, channel 0, sampling-mode gates and parallax off. The alpha value is complete. The mapping equals the material's ST. The resolution classifies a triangle that is opaque only under the true transform (F1/F2). **Fails:** the resolver refuses.
   - `LilToonCutoutAlphaTests`: a cutout fixture with `_MainTex` ST `(2,1,0,0)`, all other gates default. The result is alpha `Unknown` with exactly one `UnsupportedUv` diagnostic naming `_MainTex_ST` (F15). **Fails:** today the alpha value is *complete*, and the refusal happens later in the resolver. Both the completeness and the diagnostic assertions fail. Second test: the identity cutout fixture is unchanged.
   - `AdmittedMaterialStatesTests`: the re-asserted non-identity ST `(2,3,4,5)` singleton case still admits through the widened resolver. Add the texture-backed non-identity Poiyomi case from the file map (resolution resolved and not uniform). **Fails:** the texture-backed case's resolution is refused today.
   - `AlphaSeparationPreparationTests`: full-path **Poiyomi** scenario. The scenario is a texture-backed Poiyomi slot over a fully-opaque mipmap chain with non-identity ST. Assertions:
     - (a) A triangle transparent at identity is proven opaque under the transform and migrates in the plan.
     - (b) An ST-animated-at-serialized-default variant prepares.
     - (c) A non-singleton `_MainTex_ST` clip refuses (`AnimatedMaterialPropertyNotSingleton`).
     - (d) Source material/texture/mesh assets are bit-unchanged afterward (a structural audit that mirrors the existing no-mutation tests).
     Plus the boundary scenario: the same shape on a lilToon cutout slot with non-identity ST prepares **zero** opaque candidates. **Fails:** (a) yields no opaque triangles (all `Unknown` through the refused resolution). (b) refuses outright. If the harness cannot express a texture-backed Poiyomi alpha slot with an imported mipmap chain, stop and report (design §16 S6). Do not invent host seams.
2. **GREEN (single atomic increment):**
   - `IsSupportedMapping` → channel-only. Rewrite its doc comment to the implemented Lemma P predicate with the design reference.
   - `AlphaResolution` stores the mapping. `Classified` gains the parameter. `ResolveSampled` passes it.
   - `AlphaResolution.Classify`: identity short-circuit first (original triangle + `AlphaUvEnvelope.Zero`). Otherwise `AffineUvTransform`, and a guard trip gives per-triangle `Unknown`. Otherwise loop the mips with the envelope.
   - `LilToonCutoutMaterialSemantics`: add the C4 non-identity-ST gate. Rewrite the `:284-291` comment that delegates the refusal to the resolver.
   - `MaterialSemantics`: add the C4 emission-invariant doc comment on `UvMapping`.
   - Update the resolver-file obligation comment (identity-only wording) to the two-tier contract. No comment elsewhere may still describe identity ST as the resolver's gate (grep-verified in Task 6).
3. Run the RED batch → all green. Full product suite → green.

### Task 5 — mutation checks (prove the tests bite)

For each falsifier: apply the named wrong implementation locally. Run the mapped test. Observe the failure. Revert. Make no commits at any point:

| Falsifier | Local mutation | Test that must fail |
|---|---|---|
| F1 scale ignored | `AffineUvTransform` passes `s = 1` | resolver scale test |
| F2 offset ignored | passes `o = 0` | resolver offset test |
| F3 abs scale | `s = |s|` in transform | negative-scale test |
| F4 mip-0-only | resolver applies the transform only for `index == 0` | all-mip test |
| F5 wrap-after | **Withdrawn** — equivalence-audited (see note below the table) | — |
| F6 untransformed footprint | classifier computes the footprint from the un-inflated hull | Bilinear envelope tests |
| F7 boundary cell loss | **Withdrawn** — equivalence-audited (see note below the table) | — |
| F8/F10 exact-real-for-runtime | skip inflation when tier == envelope | envelope boundary tests |
| F9 double-as-exact | compute the envelope with `double` rounding | `AffineUvTransformTests` goldens |
| F11 overflow usable | remove the 2^127 guard | overflow test |
| F12 failure promoted | map budget overflow to `ProvenOpaque` | budget test |
| F13 identity changed | delete the identity short-circuit (identity then reaches tier selection) | identity parity tests (the `0.0f`-hull fixture falls to V and inflates, and the `−0.0f` fixture loses bit equality) |
| F14 non-singleton admitted | loosen `AdmitVector` equality for `_ST` | existing non-singleton refusal test |
| F15 family boundary erased | delete the lilToon cutout non-identity-ST gate | `LilToonCutoutAlphaTests` non-identity test (alpha becomes complete and classifies) and the preparation boundary scenario (non-zero opaque candidates) |
| F16 complexity-as-empty | budget overflow → skip the loop (vacuous) | budget test |
| F17 zero-scale-as-missing-UV | return `Unknown` for E2 axes | zero-scale test |
| F18 degenerate confusion | treat a degenerate UV hull as geometry-degenerate | degenerate tests |
| F19 component swap | transpose scale/offset application in `AffineUvTransform` | asymmetric golden `(2,3)`/`(0.5,0.25)` + resolver offset test |
| F20 scaled footprint | **Structural** — no executable mutation. Classifier/geometry APIs carry no ST scale (see note below the table) | — |
| F21 inflate-after-normalize | **Withdrawn** — equivalence-audited (see note below the table) | — |
| F22 wider-type | `double` transform in `EncodeToNearestFloat` | the double-rounding fixture `EncodeToNearestFloatDoesNotDoubleRoundAboveBinary32Midpoint` |
| F23 hollowed tests | delete the rewritten refusal tests | review check: file map diff shows rewrites, not deletions |
| F24 subnormal DAZ amplification | drop the E1/E3 normality guards and the `B_daz` offset sub-term | the `s = 2^-127` tier test, the subnormal-`o` E2/E3 tier tests, and the `B_daz` goldens |
| F25 ideal-domain envelope | compute `B_st` as `2^-22·2·M_exact` (drop `P`) | the two cancellation goldens and the resolver cancellation outcome test |
| F26 path-dependent exactness | restore the serial `fl(fl(c·s)+o) == c·s+o` check as E3's predicate | the compensating-rounding tier test (admitted as E3 with a zero envelope) |

**Withdrawn rows (controller equivalence audit, 2026-09-01):** F5, F7, and F21 are non-executable. `NormalizeRepeat` translates a domain by exact integer periods. `OutwardExpand` commutes with translation. Inflate-before and inflate-after normalization are therefore outcome-identical for every input. The choice of the floor offset from inflated or uninflated minima is also outcome-identical. F5 ran exactly as written before withdrawal. It passed the full `TriangleAlphaClassifierTests` class (66/66). The existing Repeat boundary tests remain positive behavioral coverage. The plan invents no replacement mutations.

Remaining executable mutation rows after F4: **16** (F6, F8/F10 as one merged row, F9, F11, F12, F13, F14, F15, F16, F17, F18, F19, F22, F24, F25, F26). F5/F7/F21 are withdrawn. F20 is reclassified structural. The 2026-09-01 continuation completed all 16 rows with hash-verified reverts. No executable rows remain. Three structural checks have no executable mutation: the resolver's family-blind signature (design F15 second half), F20's ST-scale-free footprint API, and F23's rewrite-not-delete rule.

### Task 6 — full validation, diff review, cleanup

1. Focused: `run_tests` EditMode `group_names` for each touched class. Run `get_test_job` to terminal state. Assert the counts.
2. Full: `run_tests` EditMode `assembly_names:["Alrauna.Amuse.Tests.Editor"]`, then `["Alrauna.Amuse.Research.Tests.Editor"]`. Both assemblies end with 0 failed. The console stays clean of new errors and warnings (`read_console get types:[error,warning]`). Final counts after the near-identity ST repair and the controller-authorized exact-comparison repair: product assembly **1556** passed. That is 1544, plus the four `EveryNearIdentityNonIdentityMainTexStComponentIsRejectedExactly` cases, plus the eight exact-comparison methods. Research assembly **138** passed. `LilToonCutoutAlphaTests` **55**, `LilToonBaseColorTests` **23**, `LilToonEmissionTests` **36**, `PoiyomiBaseColorTests` **59**, `PoiyomiEmissionTests` **44**, `AlphaSeparationPreparationTests` **28**.
3. Source preservation: run the Task 4 preparation scenario's structural audit. Also confirm no asset or `.meta` diff outside the two new-file pairs.
4. Diff review: run `git diff` (unstaged only — nothing staged), `git diff --check`, and `git status --short --branch`. Confirm exactly the file map, plus the preserved package-file churn. Confirm nothing is staged.
5. Signature audit: `grep -n "Resolve(" Packages/com.alrauna.amuse/Editor/Analysis/AlphaSemanticsResolver.cs` shows no family or material parameter on `AlphaSemanticsResolver.Resolve`. The family-blindness invariant holds at the signature level. The compatibility boundary therefore lives only in the frontends (design §11).
6. Comment audit: grep the analysis and semantics folders for stale text that describes identity ST as the resolver's gate. The only remaining non-identity refusal text must be the new lilToon gate.
7. Documentation: this plan and the spec need no update unless a stop condition fired. If implementation revealed a spec error, stop and report. Do not edit silently.

## Test execution protocol

```
<Unity MCP gate — see preconditions>
read_console clear
job = run_tests EditMode group_names:["<full test class name>"]
get_test_job job_id=<job>        # poll to terminal; assert passed/failed counts
read_console get types:[error,warning]
```

Full suites replace `group_names` with `assembly_names`. Never overlap two test jobs. If Unity is not running, the session stops at the gate and reports. No validation claim stands without an observed run.

## Expected pre-fix failures (summary)

- Task 1 RED: `AlphaUvEnvelope` and the geometry member tests fail to compile (type and members absent). This is the accepted RED for new members.
- Task 2 RED: `AffineUvTransformTests` fails to compile (type absent).
- Task 3 RED: every non-zero-envelope fixture returns the un-inflated outcome (concrete wrong outcomes listed per fixture).
- Task 4 RED: resolver non-identity/classify/cancellation tests fail with `UnsupportedUvMapping` refusals. The Poiyomi consumer test fails with a refused resolution. The lilToon non-identity tests fail because the alpha value is still complete and carries no `UnsupportedUv` diagnostic. The four near-identity component cases fail this way against an epsilon-based `Vector2` gate. Each reports `IsComplete` `Expected: False But was: True`. The preparation Poiyomi scenario fails with no opaque migration. The animation-at-serialized variant refuses.

## Git authorization boundary

- The controller MAY authorize implementation-branch creation and switching at implementation time. This plan assumes nothing.
- NEVER stage, commit, amend, push, open or merge a PR, tag, publish, delete branches, or touch remotes/settings. All work remains unstaged in the working tree for review. Never stage the preserved package-file churn.

## Expected implementation report

1. Branch name and base commit. The Unity instance identity used. The Poiyomi source re-check result (precondition 6).
2. Files changed or added, compared with the file map. Any deviation and its reason.
3. RED evidence: observed pre-fix failures per task (test names + failure modes). GREEN evidence: observed passing runs with counts.
4. Mutation-check table: mutation applied → test failed → reverted (F1–F26). The three structural checks (F15, F20, F23) have no executable mutation. Report their audit evidence instead.
5. Full-suite results (both assemblies, counts) and console state.
6. Diff summary: `git status --short --branch` and `git diff --check` output. Confirm `Packages/manifest.json`/`packages-lock.json` carry only the pre-existing churn. Confirm nothing is staged.
7. Any stop condition hit (S1–S6), with evidence, or an explicit "none".
