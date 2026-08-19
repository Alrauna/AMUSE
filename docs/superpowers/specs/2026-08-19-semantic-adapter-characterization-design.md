# Semantic Adapter Characterization Design

Date: 2026-08-19
Branch: `test/semantic-adapter-characterization`
Base: `9c37f22` (`origin/main`, lilToon adapter milestone merged as PR #10)

## Executive decision summary

AMUSE now has two materially different shader frontends. This milestone reads them
as evidence and produces knowledge plus targeted regression coverage. It proposes
**no new abstraction, no semantic-core change, and no shader adapter #3.**

| Decision | Outcome |
| --- | --- |
| Introduce a shared adapter abstraction now | **No.** No candidate clears the bar. |
| Change `MaterialSemantics` | **No.** Every observed pressure is absorbed by `Unknown`. |
| Extract more shared code | **No.** Five byte-identical members are eligible on contract grounds but not on value; trigger is recorded instead. |
| Add characterization/metamorphic tests | **Yes.** Six focused additions, each answering a named architectural question. |
| Build a differential-rendering harness | **No.** Deferred, with explicit preconditions. Rejected as a CI gate. |
| Production code change | **One candidate correctness defect found**, described below. It is reported, not fixed, in this design. |

The single most important finding is **not** an abstraction opportunity. It is that
the two adapters made **opposite decisions on the same structural question**, and
comparing them exposes what looks like a false positive in the Poiyomi frontend —
the defect class AGENTS.md ranks as a correctness bug rather than a coverage gap.

## Relationship to the lilToon design document

`docs/superpowers/specs/2026-08-17-liltoon-semantics-adapter-design.md` already
contains a Poiyomi↔lilToon comparison, an A/B/C/D classification, a pressure table,
and a "what the second adapter taught us" list. **That work is not repeated here.**

Two things make a separate document necessary:

1. **Location.** That analysis lives inside a per-adapter implementation spec. It
   will be superseded and forgotten when adapter #3 arrives. The durable comparison
   belongs in `docs/architecture/`.
2. **It was written before implementation.** Several of its claims are predictions.
   This design verifies them against the merged code and **corrects two**.

### Corrections to the lilToon design document

| Claim in the lilToon spec | Merged-code finding |
| --- | --- |
| `TryReadBinary` is deliberately duplicated because "lilToon's toggles are `Int`-typed with different validity ranges" | **False.** The two implementations are byte-identical. lilToon's `Int`-typed properties are read through `GetFloat` exactly as Poiyomi's are. The duplication is real; the stated justification is not. |
| Category D covers "every equation… kept wholly separate" as a uniformly deliberate choice | **Incomplete.** One structural rule — *prove independent writers off before making a neutral claim* — was applied in lilToon and **not** in Poiyomi. The divergence is not a shader difference; it is an inconsistency. |

Both corrections were only visible by reading the two implementations side by side.
That is this milestone's justification.

## Current source-of-truth inventory

Read in full at `9c37f22`. Line counts are the merged files.

| File | Lines | Role |
| --- | --- | --- |
| `Editor/Semantics/MaterialSemantics.cs` | 761 | Closed immutable vocabulary. Zero shader knowledge, zero Unity Editor types. |
| `Editor/Semantics/UnityTextureEvidence.cs` | 215 | The five shared host facts. Takes `Texture`, never `Material`. |
| `Editor/Semantics/Poiyomi/PoiyomiMaterialSemantics.cs` | 1432 | Attestation + interpretation + result/diagnostic types, one file. |
| `Editor/Semantics/LilToon/LilToonSourceAttestation.cs` | 916 | Include tree, canonicalization, digests, feature/render scans, identity conjunction. |
| `Editor/Semantics/LilToon/LilToonMaterialSemantics.cs` | 1076 | Interpretation + result/diagnostic types. |
| `Tests/Editor/Semantics/**` | ~3 900 | 11 test files; ~215 test methods across both frontends plus shared evidence. |

Supporting facts established during research:

- **There is no test CI.** `.github/workflows/` contains only `build-listing.yml` and
  `release.yml`. No workflow compiles the project or runs EditMode tests. Any
  proposal to "gate this in CI" currently has no CI to gate into.
- **The only reachable Unity Editor is the private avatar testbed.** Read-only
  package enumeration returned 78 packages including `com.vrchat.avatars 3.10.4`,
  `com.vrcfury.vrcfury`, `jp.lilxyzw.liltoon 2.3.4`, `com.llealloo.audiolink`, and
  `at.pimaker.ltcgi`, with `com.alrauna.amuse` resolved as `Local`. This is the
  private testbed described in AGENTS.md, not the public development project.
  `com.poiyomi.toon` is **not** present as a package.

## Poiyomi ↔ lilToon architecture comparison

### What is structurally identical

Both frontends implement the same five-stage shape:

```
AnalyzeBaseMaterial(Material)
  → RequireAnalyzableMaterial          (null / destroyed / no shader)
  → Gather*SourceEvidence(Material)    (pure read, no decision)
  → TryVerify*Identity(evidence, out diagnostic)
  → InterpretVerifiedMaterial(material, colorSpace, …)
      → four independent output interpreters
  → *SemanticResult { IsSupportedMaterial, MaterialSemantics, IReadOnlyList<Diagnostic> }
```

The separation of *evidence gathering* from the *identity decision* is present in
both, and in both it exists for the same reason: it makes the conjunction testable
without the real shader installed. That is a genuine, twice-confirmed architectural
pattern — and notably it is a **testability** pattern, not a semantics one.

Byte-identical members, verified by direct diff:

| Member | Contains shader knowledge? |
| --- | --- |
| `RequireAnalyzableMaterial(Material)` | No |
| `FirstFailedZeroGate(Material, params string[])` | No — the *lists* carry the knowledge, the function does not |
| `TryReadBinary(Material, string, out bool)` | No |
| `AllUnknown()` | No |

Structurally identical but not byte-identical:

- `ComputeNormalizedSourceHash` — same rule (strip BOM, CRLF/CR → LF, SHA-256
  lowercase hex), different decomposition. lilToon splits it into `Normalize` +
  `Sha256`; Poiyomi inlines both.
- `RecordUnknown<T>` / `AddDiagnostic` — same behavior, different diagnostic types.
- `{Adapter}SemanticResult` / `{Adapter}SemanticDiagnostic` / `{Adapter}SemanticOutput` —
  identical shape; `SemanticOutput` enums have identical members in identical order.
- The value-collapse idiom, appearing six times across the two files:
  `tint == Vector3.one ? Texture(sample, interp) : TextureTimesConstant(sample, interp, tint)`.

Both frontends consume **all five** `UnityTextureEvidence` facts. Poiyomi reaches
four of them through thin wrappers that now add nothing but a doc comment
(`TryGetAssignedTextureSourceId`, `TryGetColorInterpretation`,
`TryProveSampledAlphaIsOne`, `IsCanonicalNormalMapImport`). Only
`TryGetMainTextureSampling` earns its wrapper, because "the sampler always comes
from `_MainTex`" is Poiyomi knowledge.

### What genuinely differs

| Concern | Poiyomi 9.3.64 | lilToon 2.3.4 | Why it differs |
| --- | --- | --- | --- |
| Source form | One handwritten `.shader`, distributed as-is | Two `.shader` assets **regenerated per project** from settings | Upstream build model |
| Attestation anchor | Whole-file SHA-256 + GUID + package + unlocked check + property schema | Canonicalized-remainder digest of 2 assets + whole-include-directory digest + live `LIL_RENDER` read + `_lilToonVersion` float stamp | Generated source makes whole-file hashing refuse legitimate installs |
| Generated source | **Refused outright** (`_ShaderOptimizerEnabled`) | **Normalized and accepted** | Opposite conclusions from the same problem |
| Attestation → interpretation | Boolean gate only | **Data channel.** `CompiledFeatures` flows into `InterpretVerifiedMaterial` | Compile-time stripping is invisible in material state |
| Interpretation inputs | `(Material, ColorSpace)` | `(Material, ColorSpace, IReadOnlyCollection<string>)` | Consequence of the row above |
| Sampler ownership | `_MainTex` sampler for **all four** outputs | `_MainTex` own for BaseColor; `_MainTex` borrowed for Normal; `_EmissionMap` own for Emission | Shader declaration |
| UV model | Per-texture channel selector (`_*UV`, 0–3), pan must be exactly zero, direct ST | UV0 only for main/normal, scroll-rotate must be zero; **affine composition** main∘bump; own channel enum for emission | Shader design |
| Alpha derivation | Material property `_AlphaForceOpaque`, then `_Color.a × _MainTex.a`, 18 gates | `Constant(1)` from **attested `LIL_RENDER == 0`**, 2 coverage gates | Same semantic value, different evidence class |
| BaseColor gates | 40 exact-off names | 8 exact-off names + 3 value proofs (HSVG identity, no adjust mask, provable `[0,1]` range) | lilToon's tone correction has no toggle |
| Emission model | Four additive slots, slot 0 only | One blend, four modes, Add only | Shader design |
| Range proof | Not required | Required; positive allow-list of 46 `GraphicsFormat` values | `lilToneCorrection` saturates |
| Diagnostic codes | 10 | 12 (adds `UnsupportedShaderVariant`, `MissingFeatureCompilation`) | New refusal categories |

## Concept classification

Categories follow the lilToon spec's A–E scheme, extended with F and G as the task
requires. **Verified against merged code**, not carried over.

### A — Semantic-core concepts

`MaterialSemantics`, `SemanticOutput<T>`, `ColorSemanticValue`, `ScalarSemanticValue`,
`NormalSemanticValue`, `TextureSample`, `TextureSourceId`, `UvMapping`,
`TextureSampling`, and the five enums.

Used unchanged by both frontends. `UvMapping` absorbed an affine-composition rule it
was not designed for. **No change proposed.**

### B — Generic Unity host evidence

The five `UnityTextureEvidence` facts. Two independent consumers each, identical
input contract (`Texture`; null → false), identical failure behavior, identical host
assumptions. Guarded by `SharedClass_ExposesExactlyFiveSemanticFacts`, a reflection
test that fails if a sixth non-private member appears. **This guard is the single
best piece of architectural test infrastructure in the repository** and is the model
for how future boundaries should be defended.

### C — Shader-family / build knowledge

Pinned GUIDs, package names and versions, digest constants, canonicalization region
rules (R1/R2/R3), the shadow-slot keyword literal, the `LIL_FEATURE_*` symbol set,
the `_lilToonVersion` stamp value, the 65-shader lilToon taxonomy. **Not extracted.**

### D — Shader-specific semantic interpretation

Every equation, every gate list, sampler-ownership rules, UV rules, the tone-correction
identity proof and its range predicate, emission blend-mode algebra, the `LIL_RENDER`
alpha derivation. **Kept wholly separate.**

### E — Attestation

Poiyomi: single-asset hash conjunction. lilToon: include tree + canonicalization +
two digests + live define scans. The two models share **only** the normalized-hash
rule. Attestation has no common shape across two shaders; assuming it has one is the
mistake this category exists to prevent.

### F — Test / diagnostic infrastructure

Two near-parallel fixture bases (`PoiyomiFixtureTestBase`, `LilToonFixtureTestBase`)
with a purpose-built stand-in ShaderLab fixture each, temp-folder asset lifecycle,
`ImportTexture(name, configure, sourceHasAlpha)` helpers, and diagnostic assertion
helpers. The **assertion helpers differ**: Poiyomi's `AssertUnsupportedOutput` also
asserts `IsSupportedMaterial == true`, while lilToon's `AssertSingleDiagnostic`
additionally asserts the diagnostic *count* is exactly one. Neither is wrong; they
check different things, and neither adapter gets the other's check. This is a real
gap, not a naming difference.

Both stand-in shaders are executable specifications of the consumed property contract
and contain no upstream source. That pattern is confirmed twice and should be treated
as the standing convention for adapter #3.

### G — Repeated pressure, not ready for abstraction

Listed in full under "Repeated semantic pressures" below.

## Behavior / support matrix

Durable comparison. Records **semantic behavior and proof requirements**, not
property dumps. This table is the artifact adapter #3 should be checked against.

| Behavior | Poiyomi | lilToon | Core representation | Shared evidence | Shader-specific rule | Repeated pressure | Decision |
| --- | --- | --- | --- | --- | --- | --- | --- |
| Shader identity | Exact name `.poiyomi/Poiyomi Toon` + asset GUID | Exact name `lilToon` + asset GUID + resolved pass GUID | none | no | yes | naming a supported shader | duplicate |
| Version identity | Package version string | Package version **and** `_lilToonVersion` float on the material | none | no | yes | two independent version channels | duplicate |
| Generated source | Refused (`_ShaderOptimizerEnabled` locked) | Accepted after canonicalizing proven-variable regions | none | no | yes | — | **opposite; keep separate** |
| Compile-time stripping | N/A | `LIL_FEATURE_*` scan; claim requires the symbol | none | no | yes | one producer | lilToon-only |
| Missing texture / default | `null` → constant term (BaseColor/Emission) or `Unmodified` (Normal) | same, plus `_UseBumpMap` toggle | `Constant`, `Unmodified` | no | yes | **neutral-claim gating (see below)** | **inconsistent — investigate** |
| Stable texture identity | `UnityTextureEvidence.TryGetSourceId` | same | `TextureSourceId` | **yes** | no | — | extracted |
| Color interpretation | `TryGetColorInterpretation` | same | `TextureColorInterpretation` | **yes** | no | — | extracted |
| Sampled range | not required | positive `[0,1]` format allow-list | none | no | yes | one consumer | lilToon-only |
| Sampler ownership | `_MainTex` for all four outputs | `_MainTex` (own) BaseColor; `_MainTex` (borrowed) Normal; `_EmissionMap` Emission | `TextureSampling` | fact yes, **ownership no** | yes | which slot owns a sampler | **characterize (T3)** |
| UV channel selection | per-texture float 0–3 | UV0 only (main/normal); enum (emission) | `UvMapping.Channel` | no | yes | — | duplicate |
| UV transforms | direct ST; pan exactly zero | direct ST; scroll-rotate exactly zero | `UvMapping.Scale/Offset` | no | yes | "animated UV must be inert" | duplicate |
| Affine UV composition | not needed | main ∘ bump | `UvMapping` expresses exactly | no | yes | **none — core sufficed** | no action |
| Sampling constraints | Point/Bilinear, equal Clamp/Repeat, no mip/bias/aniso | identical | `TextureSampling` | **yes** | no | — | extracted |
| BaseColor forms | `Constant` \| `Texture` \| `Texture×Constant` | identical | all three | partly | yes | — | duplicate |
| Alpha proof source | material property + 18 gates | **attested compile-time constant** + 2 gates | `ScalarSemanticValue` | no | yes | same value, different evidence class | **key finding** |
| Coverage mechanisms | `_AlphaToCoverage`, `_AlphaSharpenedA2C`, `_AlphaDithering`, `_EnableDissolve`, `_EnableUDIMDiscardOptions` | `_Invisible`, `_UDIMDiscardCompile` | **none** | no | yes | **two producers, no core concept** | document |
| Normal forms | `Unmodified` \| `TangentSpaceNormalMap` | identical | both | partly | yes | — | duplicate |
| Normal sampler coupling | `_MainTex` sampler | `_MainTex` sampler | `TextureSampling` | fact yes | yes | **borrowed sampler is a real cross-shader idiom** | document |
| Emission forms | slot 0 only, `Constant(0)` when off | Add blend only, `Constant(0)` when off | `ColorSemanticValue` | partly | yes | layered emission refused twice | duplicate |
| Emission sampler coupling | `_MainTex` sampler | `_EmissionMap` own sampler | `TextureSampling` | fact yes | yes | **direct contradiction** | **characterize (T3)** |
| Render-mode evidence | material property | compile-time define read from live pass | none | no | yes | — | duplicate |
| Output-local invalidation | per-output `Unknown` + scoped diagnostic | identical | `SemanticOutput<T>` | no | no | — | **confirmed shared; test (T2)** |
| Unsupported behavior | `Unknown` + diagnostic, never guess | identical | `SemanticOutput<T>.Unknown` | no | no | — | confirmed shared |
| Diagnostics | 10 codes, data not Console | 12 codes, data not Console | none | no | yes | shape identical, sets differ | duplicate |
| Modified-source refusal | hash mismatch → `ModifiedShaderSource` | any of three digests mismatch → same | none | no | yes | — | duplicate |

## Finding: the neutral-claim gating asymmetry

There are eight places across the two frontends where an output short-circuits to a
**neutral or zero claim** — a `Complete` value asserting "nothing here affects this
output". Each such claim is only sound if the independent mechanisms that could
affect the output are already proven off.

| Site | Gates proven before the neutral claim? |
| --- | --- |
| Poiyomi BaseColor, unassigned `_MainTex` | yes |
| Poiyomi Alpha, `_AlphaForceOpaque` (`:459`) | yes — coverage gates first |
| Poiyomi Emission, `!slot0Enabled` (`:604`) | yes — explicitly commented as the reason |
| Poiyomi Normal, unassigned `_BumpMap` (`:754`) | **no** |
| lilToon BaseColor, unassigned `_MainTex` | yes |
| lilToon Alpha, `Constant(1)` | yes — coverage gates first |
| lilToon Emission, `!useEmission` (`:590`) | yes — explicitly commented as the reason |
| lilToon Normal, `!useBumpMap \|\| texture == null` (`:838`) | yes — gates at `:823`, explicitly commented |

Seven of eight gate first. `PoiyomiMaterialSemantics.InterpretNormal` returns
`Complete(NormalSemanticValue.Unmodified())` as its **first statement**, before
`NormalFeatureGates` (`_DetailEnabled`, `_RGBMaskEnabled`, `_DecalEnabled0..3`,
`_PoiInternalParallax`, `_PoiParallax`) are evaluated.

lilToon reached the opposite conclusion deliberately and carries two regression tests
for it: `EnabledNormalWriter_WithBumpMapDisabled_IsUnknown` and
`EnabledSecondNormal_WithNoFirstTexture_IsUnknown`. Poiyomi's nearest test,
`NormalWriterEnabled_IsUnsupportedFeature`, builds its material via
`AssignedNormalMaterial()` — the bump map is always assigned, so the short-circuit
path is never exercised with a writer enabled. `MissingBumpMap_IsUnmodified` uses a
default material with every writer already off.

**If any Poiyomi normal-writer feature perturbs the tangent-space normal without
`_BumpMap` being assigned — `_DetailEnabled` with a detail normal map is the obvious
candidate — then a material with that feature on and no `_BumpMap` yields
`Normal = Complete(Unmodified)` while the rendered normal is perturbed. That is a
false positive.**

This design does **not** assert the defect is real. It asserts that:

1. the structural asymmetry exists and is verified;
2. the safe ordering is already established as this codebase's convention, 7:1;
3. no test covers the asymmetric path;
4. confirming or refuting it requires reading the pinned Poiyomi 9.3.64 source, which
   is not installed in either the public project or the reachable testbed.

Per AGENTS.md's tension ordering, correctness outranks this milestone's "no
production refactoring" preference. The plan therefore opens with a **source
verification task**, and makes the fix conditional on its outcome.

## Task 1 verification — the defect is confirmed

Performed 2026-08-19 against the pinned upstream source, read-only, outside this
repository. No upstream source was copied into AMUSE and Poiyomi was not installed.

### Source provenance, independently verified

`poiyomi/PoiyomiToonShader` at commit `e125e1c33cbfb860f59330799dd4d10a1097242d`
(commit message: `9.3.64`), file
`_PoiyomiShaders/Shaders/9.3/Toon/Poiyomi Toon.shader` — a single flattened
2 749 684-byte, 66 805-line asset.

| Check | Upstream | AMUSE pin | Match |
| --- | --- | --- | --- |
| `.meta` GUID | `9444ce77bf4418748b1e8591b9d97f85` | `CanonicalShaderGuid` | **yes** |
| AMUSE-normalized SHA-256 (strip BOM, CRLF/CR → LF) | `31f2ff15615c5e2ac9b05fea08b6310731394d1b5a928b16048e7bde8f8b1755` | `CanonicalNormalizedSourceHash` | **yes** |

Both pins reproduce exactly, so the trace below is against byte-for-byte the source
AMUSE attests. This also independently validates the Poiyomi attestation constants,
which had never been checked against upstream from inside this repository.

### Attested variant

AMUSE refuses locked materials (`_ShaderOptimizerEnabled != 0` → `UnsupportedShader`),
so the supported variant is the **unlocked** shader. In that file:

- `#define OPTIMIZER_ENABLED` — **0 occurrences**. Every
  `#if defined(PROP_X) || !defined(OPTIMIZER_ENABLED)` guard is therefore **true**.
- `#define PROP_` — **0 occurrences**. Every bare `#if defined(PROP_X)` guard without
  the `OPTIMIZER_ENABLED` escape is therefore **false**.

That asymmetry decides five of the eight gates.

### Complete enumeration of normal writers

Exhaustive search for assignments to `tangentSpaceNormal` finds exactly **two**
mechanisms in the whole file: the detail normal, and the RGB-mask normals via
`RGBABlendNormals`. Nothing else — no decal path and no parallax path writes it.

### Per-gate classification

| Gate | Outcome | Evidence |
| --- | --- | --- |
| `_DetailEnabled` | **CONFIRMED INDEPENDENT WRITER** | See chain below. |
| `_RGBMaskEnabled` | does not affect normal *in the attested variant* | All four writes (`:20285`, `:20298`, `:20313`, `:20329`) sit inside `#if defined(PROP_RGBNORMAL{R,G,B,A})`, and the enclosing function body is itself wrapped in the same disjunction (`:20242`). With no `PROP_` defined, the entire body compiles out. |
| `_DecalEnabled` | does not affect normal | No decal code writes `tangentSpaceNormal` anywhere in the file. |
| `_DecalEnabled1` | does not affect normal | Same. |
| `_DecalEnabled2` | does not affect normal | Same. |
| `_DecalEnabled3` | does not affect normal | Same. |
| `_PoiInternalParallax` | does not affect normal when `_BumpMap` is unassigned | No direct write; parallax perturbs UVs only. An unassigned `_BumpMap` binds Unity's `"bump"` default, which is flat at every UV, so a UV change cannot perturb the result. |
| `_PoiParallax` | does not affect normal when `_BumpMap` is unassigned | Same. |

### The confirmed chain for `_DetailEnabled`

```
:128    [HideInInspector][ThryToggle(FINALPASS)]_DetailEnabled ("Enable", Float) = 0
:10018  #pragma shader_feature FINALPASS
:29152  poiMesh.tangentSpaceNormal = UnpackScaleNormal(… _BumpMap …, _BumpScale);
:29158  #if defined(FINALPASS) && !defined(UNITY_PASS_SHADOWCASTER) && !defined(POI_PASS_OUTLINE)
:29159      ApplyDetailNormal(poiMods, poiMesh);
:20140      half3 detailNormal = UnpackScaleNormal(… _DetailNormalMap …);
:20142      poiMesh.tangentSpaceNormal = BlendNormals(detailNormal, poiMesh.tangentSpaceNormal);
```

`_DetailEnabled` is not a runtime branch. It is a **`ThryToggle` bound to the shader
keyword `FINALPASS`**: setting the property to 1 enables the keyword, which compiles
in the `ApplyDetailNormal` call. There is no runtime `if (_DetailEnabled)` guard
because the keyword *is* the gate. Inside, the write at `:20142` is guarded only by
`#if defined(PROP_DETAILNORMALMAP) || !defined(OPTIMIZER_ENABLED)`, which is true
unlocked.

**Nothing in that chain reads `_BumpMap`.** Line `:29152` establishes only the *base*
value; when `_BumpMap` is unassigned that base is the flat `"bump"` default, and the
detail blend at `:20142` still perturbs it.

**Therefore:** a material with `_BumpMap` unassigned, `_DetailEnabled = 1`,
`_DetailNormalMap` assigned, and a non-zero effective
`_DetailNormalMapScale * detailMask.g` renders with a perturbed tangent-space normal,
while `PoiyomiMaterialSemantics.InterpretNormal` returns
`Complete(NormalSemanticValue.Unmodified())` from its first statement. **That is a
false positive — a correctness defect, not a coverage gap.**

### Consequences for Task 8

- **Task 8 runs.** One gate confirms the path, which is the review's stated condition.
- **Task 8 reorders only. It must not remove any gate.** `_RGBMaskEnabled` is
  classified "does not affect normal" *solely because* `PROP_RGBNORMAL*` is undefined
  in the unlocked shader — that is, the RGB-normal feature is inert in the only
  variant AMUSE supports. That reason is contingent on Thry's locking semantics and
  would invert if AMUSE ever supported locked materials. Keeping the gate is free and
  conservative; removing it would trade a false negative for a future false positive.
- The parallax rows are conditional on `_BumpMap` being unassigned, which is exactly
  the branch under repair. They are sound as written and need no separate action.

## Repeated semantic pressures

Classified as the task requires: (1) vocabulary cannot express it; (2) chose not to
support it yet; (3) belongs in another layer; (4) shader-specific, keep it so.

### `sample.rgb * sample.a` — same-sample channel coupling

**Class 1.** `ColorSemanticValue` has `Constant`, `TextureSample`, and
`TextureSampleTimesConstant`. There is no form in which a color term is scaled by the
**alpha channel of the same sample**. Both frontends hit this at emission and both
refuse identically, by requiring `TryProveSampledAlphaIsOne` on the emission map.

Producers: 2 (independent, same shape). Consumers: **0**. No analysis in the
repository would use such a form if it existed.

**Decision: document, do not implement.** Adding a fourth `ColorSemanticValueKind`
with zero consumers would be speculative. Promote when a consumer appears, not when a
third producer does — the pressure is already confirmed general.

### Coverage versus value — stronger than previously recorded

**Class 1**, and previously under-credited. The lilToon spec logs cutout as a
lilToon-only pressure. The merged code shows the *coverage-gate concept* in **both**
frontends: each maintains a list of mechanisms that remove fragments without changing
the alpha value, and each checks that list on every alpha path including the
short-circuit.

Poiyomi: `_AlphaToCoverage`, `_AlphaSharpenedA2C`, `_AlphaDithering`,
`_EnableDissolve`, `_EnableUDIMDiscardOptions`.
lilToon: `_Invisible`, `_UDIMDiscardCompile`.

So AMUSE has *twice* discovered that "the alpha value" and "the fragment's coverage"
are different facts, and *twice* handled it by refusing. `MaterialSemantics` models
only the value. `AlphaSemanticsResolver` consumes the value.

**Decision: document as the strongest core-vocabulary gap.** Still do not implement —
coverage semantics belong to the transformation/planning layer's safety argument as
much as to the IR, and which layer owns it is exactly what is not yet known.

### Attestation-produced interpretation evidence — new, one producer

**Class 3.** lilToon's attestation does not merely gate; it *emits* `CompiledFeatures`
into interpretation. Poiyomi's attestation is a pure boolean. This is the first place
in AMUSE where a proof stage produces typed evidence for a later stage rather than a
verdict.

One producer. **Decision: document.** If adapter #3 also needs it, the thing that
generalizes is the `InterpretVerifiedMaterial(material, resolvedFacts…)` seam shape —
not an adapter interface.

### Borrowed sampler state — two producers, opposite directions

**Class 4, with a caveat.** Poiyomi samples every map with `_MainTex`'s sampler.
lilToon borrows `_MainTex`'s sampler for `_BumpMap` but uses `_EmissionMap`'s own.
The *idiom* "a texture is sampled through another texture's sampler" is confirmed in
two independent shaders, so it is not a Poiyomi artifact. The *mapping* of which slot
borrows from which is irreducibly shader-specific.

**Decision: keep shader-specific; characterize the observable consequence (T3).**

### Exact-off gate lists

**Class 4.** Confirmed as the lilToon spec concluded. The function is byte-identical;
what the lists must express is not. lilToon needs "equals `(0,1,1,1)`", "is
unassigned", "equals exactly 1", "is provably in `[0,1]`", "`.a == 0`", "`.x == 0`" —
predicates a zero-gate schema cannot carry. **No schema. Two frontends is not enough
evidence to design that vocabulary.**

### Duplicated infrastructure cluster

**Class 2.** Four byte-identical members plus one same-rule-different-shape member
plus three structurally identical types. Every one is eligible on contract grounds.
None is extracted, because extracting them produces a "shared adapter utility"
surface — the shader-framework seed this milestone exists to resist.

**Trigger, recorded explicitly:** when adapter #3 lands, extract in one pass exactly
those members that are **byte-identical across all three** and contain **zero shader
knowledge**. Not before. The count is the evidence; do not extract on two.

## Proposed characterization and metamorphic tests

Each states the architectural question it answers. Tests that answer no question were
not proposed.

| ID | Test | Question it answers |
| --- | --- | --- |
| **T1** | Neutral-claim gating parity, both adapters, all four outputs | *Is a neutral or zero claim ever made without proving independent writers off?* Currently 1 of 8 sites fails. |
| **T2** | Uncertainty monotonicity, both adapters | *Does removing evidence ever produce a more informative claim?* Nothing in the repository tests this today. |
| **T3** | Shared-evidence blast radius characterization | *Which outputs does one texture's import state invalidate, per adapter?* The two answers differ and neither is pinned. |
| **T4** | Irrelevant-change structural invariance | *Does irrelevant material state leak into semantic values?* One ad-hoc instance exists (`RenderStateProperties_DoNotChangeAlpha`); the property is not stated generally. |
| **T5** | Cross-adapter shared-evidence agreement | *Is `UnityTextureEvidence` genuinely one contract, or two coincidences that happen to match?* No test crosses the adapter boundary today. |
| **T6** | lilToon unattested-entry refusal parity | *Can interpretation be reached without attestation?* Poiyomi proves it cannot; lilToon's nearest test fails at the first check only. |

### T1 — neutral-claim gating parity

For each adapter and each output that can short-circuit to a neutral claim: enable one
independent writer gate, remove the slot's texture, assert the output is **not**
`Complete`. Table-driven over the existing gate name arrays.

Expected on first run: lilToon green (already covered), Poiyomi **red at Normal**.
The red is the point — it is the regression test for the candidate defect, and it must
fail before any fix and pass after.

### T2 — uncertainty monotonicity

Start from a fully-`Complete` material. Apply one evidence-removal mutation at a time.
Assert for every output: either the value is **structurally equal** to the baseline, or
it is `Unknown`. Never a different `Complete` value, never `Unknown → Complete`.

Mutation set, deliberately explicit rather than generated:

- remove the texture importer (native `.asset` texture);
- make the sampler unsupported (mipmaps on);
- remove color-interpretation evidence;
- lilToon only: remove each `LIL_FEATURE_*` symbol from the compiled set;
- lilToon only: pass the empty feature set.

The lilToon feature-removal case is the sharpest: it is the one mutation that models
"the shader was configured to strip this", and today only two instances of it are
tested (`StrippedFeature_KeepsEmissionUnknown`, `StrippedFeature_KeepsNormalUnknown`).

**No property-based or combinatorial framework.** An explicit list, iterated.

### T3 — shared-evidence blast radius

Assign `_MainTex`, `_BumpMap`, and the emission map on a canonical material, then make
`_MainTex`'s sampler unsupported. Assert per adapter exactly which outputs survive:

- **Poiyomi:** BaseColor, Alpha, Emission, and Normal all become `Unknown` — every
  sample routes through `TryGetMainTextureSampling`. Only genuinely constant terms
  survive. (`UnsupportedSharedMainSampler_InvalidatesEverySample_ConstantSurvives`
  partially covers this; the emission-map case is the addition.)
- **lilToon:** BaseColor and Normal become `Unknown`; **Emission stays `Complete`**
  because it uses its own sampler. Alpha stays `Complete` because it is attested.

This is the single clearest behavioral difference between the two frontends and it is
currently unpinned in lilToon. It is also the property adapter #3 is most likely to get
wrong, because it is invisible unless two slots are assigned at once.

### T4 — irrelevant-change structural invariance

For a canonical fully-`Complete` material, mutate an explicit short list of properties
that no gate list and no equation reads, and assert the whole `MaterialSemantics`
compares **equal** (the type implements structural equality throughout).

This doubles as a gate-list coverage check: if a mutation *does* change the output,
either the property is genuinely read — fine, remove it from the list — or a gate list
is reading something it should not.

Keep the list short and hand-picked. A generated sweep over every shader property is
the combinatorial infrastructure this milestone declines to build.

### T5 — cross-adapter shared-evidence agreement

One new test class outside both adapter namespaces. For each of the five shared facts,
construct the texture state that makes it refuse, feed it through the slot in **each**
adapter that consumes it, and assert both refuse — each with its own diagnostic code.

This is the only proposed test that crosses the frontend boundary, and it is the only
way to detect the failure mode where one frontend silently stops depending on a shared
fact. It intentionally does **not** assert the two adapters produce the same
diagnostic code; they legitimately do not.

### T6 — lilToon unattested-entry refusal parity

Poiyomi has `PublicEntry_UnattestedSchemaCompleteShader_IsRefusedBeforeInterpretation`.
lilToon's nearest equivalent, `AnalyzeBaseMaterial_NonLilToonShader_IsUnsupported`,
fails at check 1 (shader name) and therefore proves much less. Add the parity test: a
material whose shader name and `_lilToonVersion` are right but whose digests are not
must be refused with all four outputs `Unknown` and exactly one material-scoped
diagnostic, without any output interpreter running.

### Invariants deliberately **not** codified

- **Output locality as a general property.** Both adapters already have concrete
  instances (lilToon has all four systematically; Poiyomi has three ad-hoc). A generic
  restatement adds no information.
- **Determinism of diagnostic ordering.** Already asserted in both.
- **Immutability / defensive copy.** Already asserted in both.
- **Any property-based or generative harness.** No concrete test has yet shown a need
  that an explicit list cannot meet.

## Differential-rendering feasibility

### The question a rendering oracle would answer

Not "is the semantic value right" in general. Specifically: **are the hand-traced
shader-source claims true?** Those claims are the least-tested and highest-risk part
of both frontends, because they were established by reading HLSL and are asserted
nowhere executable. Examples: `lilToneCorrection` at `_MainTexHSVG = (0,1,1,1)` is the
identity on `[0,1]`; `lilUnpackNormalScale` at scale 1 is the canonical DXT5nm unpack;
`_AlphaForceOpaque` really overrides `_MainTex` alpha; `LIL_RENDER 0` really forces
alpha to one after every writer.

### Assessment

| Consideration | Finding |
| --- | --- |
| Comparison target exists? | **No.** `ColorSemanticValue` is a description, not a program. Comparing it to a render requires a *reference evaluator* — sample the texture, apply interpretation, apply the multiplier — which is new production code this milestone forbids. |
| Shaders available publicly? | **No.** Poiyomi and lilToon cannot be redistributed into this repository or fetched in CI. Any real-shader harness is private-testbed-bound. |
| Testbed suitability | AGENTS.md forbids the private testbed as the primary correctness oracle. A rendering harness that only runs there is precisely that. |
| What would be rendered | Both frontends' equations are refusal-dominated. A refusal produces **no value**, so most proof obligations are unobservable by rendering. |
| CI host | **There is no test CI at all.** Adding a GPU-dependent gate before a compile gate exists inverts the cheapest-gate-first principle in AGENTS.md. |
| Determinism | RenderTexture readback across graphics APIs, GPU precision, compression, shader-compilation timing, and headless rasterizer differences all argue for tolerances — and a tolerance-based test cannot distinguish "AMUSE is right" from "close enough". |

### Recommendation: **defer; reject as a CI gate**

Reject outright as mandatory CI. Do **not** build a proof-of-concept in this milestone —
the honest scoping is that a useful proof-of-concept requires a semantic evaluator,
and building one to answer a feasibility question would be building the thing.

Revisit only when **all three** hold:

1. a deterministic EditMode/compile CI gate exists, so a rendering gate has something
   to be layered on;
2. a semantic evaluator exists for a real consumer reason (baking, atlasing, material
   combining), so the comparison target is a by-product rather than test scaffolding;
3. a redistributable shader with the traced constructs exists — the AMUSE stand-in
   fixture shaders are the natural candidate, since they can be given real
   `lilToneCorrection`-shaped math without shipping upstream source.

Condition 3 is the interesting one: it would make the *fixture* the oracle rather than
the real shader, which sidesteps both redistribution and testbed dependence — at the
cost of testing our transcription of the math rather than the shader itself. That is a
weaker but genuinely public proof, and it is the form worth reconsidering later.

**The cheaper substitute available now is CI.** Establishing a compile + EditMode gate
would retire more risk per unit of effort than any rendering work, and its absence is
the largest validation gap found during this research.

## What should remain duplicated

| Concept | Until |
| --- | --- |
| `FirstFailedZeroGate`, `TryReadBinary`, `RequireAnalyzableMaterial`, `AllUnknown` | adapter #3 confirms byte-identity across three |
| `IsFinite` overloads | indefinitely — trivial |
| `ComputeNormalizedSourceHash` | a third consumer, or extraction of the whole attestation-primitive cluster at once |
| Result / diagnostic / output types | a shared diagnostic framework is justified by consumers, not producers; there are none |
| Gate lists and every equation | permanently — this is category C/D |
| Fixture test bases and stand-in shaders | permanently; but the *assertion helpers* should be reconciled (each adapter should get both checks) |
| Sampled `[0,1]` range proof | a second frontend needs the identical contract |
| Attestation models | permanently — two shaders produced two irreconcilable shapes |

## Future abstraction candidates

Listed with the evidence each still lacks. None is proposed now.

| Candidate | Evidence present | Evidence missing |
| --- | --- | --- |
| Attestation-primitive cluster (hash rule + digest helpers) | identical rule, two producers | a third producer, or a reason the current duplication has cost something |
| Same-sample `rgb × a` color form | two independent producers | **any consumer** |
| Coverage-versus-value concept | two independent producers | which layer owns it — IR, resolver, or planner |
| Resolved-facts interpretation seam | one producer (lilToon `CompiledFeatures`) | a second producer |
| Declarative gate schema | superficial shape match | a shared predicate vocabulary; currently falsified |
| `IShaderAdapter` / registry | two frontends with the same call shape | **any polymorphic call site.** Nothing in the repository dispatches over adapters. |

The last row is decisive. Both frontends expose `AnalyzeBaseMaterial(Material)` and
could trivially share an interface — and there is **no caller**. An interface with two
implementations and zero consumers is the textbook speculative abstraction.

## Explicit non-goals

Reaffirmed and not violated by this design: `IShaderAdapter`; adapter registry or
factory; `ShaderSchema`; serialized shader profiles; YAML/JSON shader definitions;
generic shader interpreter; expression DAG; feature graph; HLSL parser; shader
transpiler; shader portability; feature transplantation; NDMF integration; animation or
state tracing; atlasing; material combining; optimization-planner changes; shader
adapter #3; any change to `MaterialSemantics`.

## Stop conditions

| Condition | Status |
| --- | --- |
| A characterization test cannot be written without changing `MaterialSemantics` | **Not fired.** All six proposed tests use existing surfaces. |
| A proposed shared abstraction lacks two identical consumers | **Not fired** — nothing is proposed for extraction. |
| Research reveals a correctness defect | **FIRED, and since CONFIRMED.** The Poiyomi neutral-claim gating asymmetry is a real false positive via `_DetailEnabled`. See "Task 1 verification". Task 8 is authorized and required. |
| A test requires production code to become testable | **Not fired.** Both `InterpretVerifiedMaterial` seams are sufficient. |
| Differential rendering requires new production code | **FIRED.** A reference evaluator would be required. This is why the recommendation is defer, not proof-of-concept. |
| Work expands into planning, animation, NDMF, atlasing | **Not fired.** |
| The milestone starts designing adapter #3's framework | **Not fired.** |

Two stop conditions fired. Both are handled by narrowing, not by widening scope.

### Execution outcomes (2026-08-19)

All ten tasks completed on the public AMUSE development Editor
(`Application.dataPath = E:/AI/Git/AMUSE/Assets`, Unity 2022.3.22f1). The private
testbed was not used.

| Stop condition | Observed outcome |
| --- | --- |
| A characterization test cannot be written without changing `MaterialSemantics` | Did not fire. All six tests used existing surfaces; the core was not touched. |
| A proposed shared abstraction lacks two identical consumers | Did not fire. Nothing was extracted. |
| Research reveals a correctness defect | Fired and **resolved**. 8 RED cases observed, fix applied as a pure reordering, 8 GREEN, full suite 629/629. |
| A test requires production code to become testable | Did not fire. Both `InterpretVerifiedMaterial` seams sufficed; the one cross-frontend test reached lilToon through its public fixture shader plus the static seam, needing no new base class. |
| Differential rendering requires new production code | Fired at design time; no rendering work was attempted, per the recommendation. |
| Work expands into planning, animation, NDMF, atlasing | Did not fire. |
| The milestone starts designing adapter #3's framework | Did not fire. |

| Measurement | Result |
| --- | --- |
| Baseline suite before any change | 553 passed / 0 failed |
| Task 7 RED, before the fix | 44 run, 36 passed, **8 failed** — all `PoiyomiNeutralClaimGatingTests.Normal_NoBumpMap_WriterEnabled_IsNotClaimed`, one per gate |
| Task 7 GREEN, after the fix | 44 passed / 0 failed |
| Final full suite | **629 passed / 0 failed** (553 pre-existing, all still passing, plus 76 new) |
| Unity console after final run | 0 errors, 0 warnings |

Every design prediction that execution could test was confirmed: the Poiyomi blast
radius is 4-of-4 and lilToon's is 2-of-4 with `Emission` and `Alpha` surviving;
monotonicity holds in both frontends across every mutation including total feature
stripping; all five shared facts are still consumed by both frontends.

One design claim was **overstated** and is corrected here: the design said lilToon's
`AnalyzeBaseMaterial_NonLilToonShader_IsUnsupported` "proves much less" than the Poiyomi
analogue. In fact it already asserted a single diagnostic and all four outputs
`Unknown`. The Task 6 addition is therefore narrower than described — it adds the
material-scoping assertion and, more usefully, the direct contrast against the same
material interpreting fully through the seam. The gap was real but smaller than stated.

## Implications for shader adapter #3

**What must be learned before starting it** — in priority order:

1. **Resolve the neutral-claim gating question.** Until it is settled, the codebase
   contains two contradictory answers to a rule adapter #3 must follow.
2. **Establish a compile + EditMode CI gate.** Adding a third 1 000-line frontend to a
   repository with no automated test gate compounds risk faster than it adds coverage.
3. **Pin the sampler blast-radius behavior (T3).** It is the property most likely to be
   silently wrong in a new frontend.

**What adapter #3 should be chosen to falsify.** Poiyomi and lilToon are both
VRChat-ecosystem toon shaders. They agree on: linear color space required, Unity
`TextureImporter` as the evidence source, exactly-off boolean gates, `_MainTex`/
`_BumpMap`/`_EmissionMap` slot naming, and the four-output decomposition itself. A
third toon shader would confirm little. A frontend that stresses a *different* axis —
Standard/URP-lit, or a shader with no source available at all — would test whether the
four-output vocabulary is a real abstraction or a toon-shader coincidence.

**What must still be duplicated in adapter #3**: its own attestation model, its own
gate lists, its own equations, its own diagnostic code set, its own fixture shader.
Copying the byte-identical helpers a third time is correct; extracting them at that
point is also correct. Both are fine — what is not fine is extracting them now, on two.

## Implications for eventual third-party integration

Not a current goal; recorded because the two adapters make the constraints visible.

- **Attestation is the blocker, not semantics.** Any third-party shader frontend must
  ship pinned digests it measured itself. There is no mechanism for that today, and
  designing one before AMUSE's own adapters are stable would be premature.
- **`UnityTextureEvidence` is the natural first public surface.** It is small, shader-
  independent, has a refusal-predicate contract, and is already guarded by a
  reflection test. It is `internal` and should stay so until an external consumer
  exists.
- **The four-output vocabulary may not survive contact with a third party.** It has two
  producers, both toon shaders. Publishing it as an extension point would freeze it on
  the weakest evidence in this document.

## Deferred work

- Verifying and, if confirmed, fixing the Poiyomi normal gating defect (Task 1 of the
  plan gates this).
- Reconciling the two fixture bases' assertion helpers so each adapter gets both the
  `IsSupportedMaterial` check and the diagnostic-count check.
- Removing Poiyomi's four now-pure delegation wrappers, or documenting why they stay.
- A compile + EditMode CI gate.
- Extraction of the duplicated helper cluster — on adapter #3, in one pass.
- Same-sample `rgb × a`, coverage semantics, premultiplied alpha, non-additive blend
  modes, layered emission — all still `Unknown`.
- Differential rendering, under the three preconditions above.
