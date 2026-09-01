# Shader Frontend Comparison

What AMUSE's two implemented shader semantic frontends — Poiyomi Toon 9.3.64 and
lilToon 2.3.4 — have actually taught us about the architecture.

This is a durable architecture record, not a milestone document. It is derived from
reading both merged implementations at `9c37f22`, and it is the artifact a third
shader frontend should be checked against before it is written.

**Scope note.** This document records *what generalized and what did not*. It does not
describe either shader's equations; those live in the per-adapter design specs. It
proposes no abstraction.

## Supersedes

`docs/superpowers/specs/2026-08-17-liltoon-semantics-adapter-design.md` contains an
earlier version of this comparison, written before implementation. Two of its claims
are falsified by the merged code and are **superseded by this document**:

1. It states `TryReadBinary` is deliberately duplicated because "lilToon's toggles are
   `Int`-typed with different validity ranges." The two implementations are
   **byte-identical**. lilToon's `Int`-typed properties are read through `GetFloat`
   exactly as Poiyomi's are. The duplication is real; the stated justification is not.
2. It presents category D as a uniformly deliberate set of shader-specific choices.
   One structural rule — *prove independent writers off before making a neutral claim* —
   was applied in lilToon and **not** in Poiyomi. That divergence was an inconsistency,
   not a shader difference, and it was a confirmed correctness defect.

Where this document and that spec disagree, this document is current.

## The shape both frontends converged on

Both implement the same five stages:

```
AnalyzeBaseMaterial(Material)
  → RequireAnalyzableMaterial          (null / destroyed / no shader)
  → Gather*SourceEvidence(Material)    (pure read, no decision)
  → TryVerify*Identity(evidence, out diagnostic)
  → InterpretVerifiedMaterial(material, colorSpace, …)
      → four independent output interpreters
  → *SemanticResult { IsSupportedMaterial, MaterialSemantics, IReadOnlyList<Diagnostic> }
```

The separation of **evidence gathering** from the **identity decision** appears in
both, for the same reason in both: it makes the identity conjunction testable without
the real shader installed. This is a confirmed pattern — and notably it is a
*testability* pattern, not a semantics one. Adapter #3 should follow it.

Four members are byte-identical across the two frontends and contain no shader
knowledge: `RequireAnalyzableMaterial`, `FirstFailedZeroGate`, `TryReadBinary`, and
`AllUnknown`. Structurally identical but not byte-identical: `ComputeNormalizedSourceHash`
(same rule, different decomposition), `RecordUnknown<T>`, the result/diagnostic/output
types, and the value-collapse idiom (four color sites comparing the tint per
binary32 component, `tint.x == 1f && tint.y == 1f && tint.z == 1f ? Texture(…)
: TextureTimesConstant(…)`, plus two scalar-alpha sites on `colorAlpha == 1f`),
which appears six times. Unity's aggregate `Vector3` equality is epsilon-based
and is deliberately not used: a near-one tint is a real multiplier.

Both consume **all five** `UnityTextureEvidence` facts.

## Concept classification

| Category | Contents | Status |
| --- | --- | --- |
| **A — semantic core** | `MaterialSemantics`, `SemanticOutput<T>`, the three value types, `TextureSample`, `TextureSourceId`, `UvMapping`, `TextureSampling`, the enums | Used unchanged by both. `UvMapping` absorbed an affine-composition rule it was not designed for. **No change.** |
| **B — generic Unity host evidence** | The five `UnityTextureEvidence` facts | Extracted. Two independent consumers each, identical input contract, failure behavior, and host assumptions. Guarded by `SharedClass_ExposesExactlyFiveSemanticFacts`. |
| **C — shader-family / build knowledge** | Pinned GUIDs, package names/versions, digest constants, canonicalization region rules, the `LIL_FEATURE_*` symbol set, the lilToon shader taxonomy | **Not extracted.** |
| **D — shader-specific interpretation** | Every equation, every gate list, sampler-ownership rules, UV rules, the tone-correction identity proof, emission blend-mode algebra, the `LIL_RENDER` alpha derivation | **Kept wholly separate.** |
| **E — attestation** | Poiyomi's single-asset hash conjunction; lilToon's include tree + canonicalization + digests + live define scans | The two models share **only** the normalized-hash rule. Attestation has no common shape across two shaders. |
| **F — test infrastructure** | Two fixture bases, two stand-in ShaderLab fixtures, texture-import helpers, diagnostic assertion helpers | Duplicated deliberately. The stand-in-fixture pattern is confirmed twice and is the standing convention. |
| **G — repeated pressure** | See below | Documented, not implemented. |

`UnityTextureEvidence`'s reflection guard is the best piece of architectural test
infrastructure in the repository: it fails if a sixth non-private member appears. That
is the model for how future boundaries should be defended — a boundary nobody can
verify is a boundary that erodes.

## Behavior / support matrix

| Behavior | Poiyomi | lilToon | Core representation | Shared evidence | Shader-specific rule | Decision |
| --- | --- | --- | --- | --- | --- | --- |
| Shader identity | exact name + asset GUID | exact name + asset GUID + resolved pass GUID | none | no | yes | duplicate |
| Version identity | package version string | package version **and** `_lilToonVersion` float on the material | none | no | yes | duplicate |
| Generated source | **refused** (locked shaders rejected) | **accepted** after canonicalizing proven-variable regions | none | no | yes | **opposite; keep separate** |
| Compile-time stripping | N/A | `LIL_FEATURE_*` scan; a claim requires the symbol | none | no | yes | lilToon-only |
| Missing texture / default | `null` → constant or `Unmodified` | same, plus a `_UseBumpMap` toggle | `Constant`, `Unmodified` | no | yes | **must gate first — see below** |
| Stable texture identity | shared | shared | `TextureSourceId` | **yes** | no | extracted |
| Color interpretation | shared | shared | `TextureColorInterpretation` | **yes** | no | extracted |
| Sampled range | not required | positive `[0,1]` format allow-list | none | no | yes | lilToon-only, one consumer |
| Sampler ownership | `_MainTex` for **all four** outputs | `_MainTex` (own) BaseColor; `_MainTex` (borrowed) Normal; `_EmissionMap` (own) Emission | `TextureSampling` | fact yes, **ownership no** | yes | keep separate |
| UV channel selection | per-texture float 0–3 | UV0 only (main/normal); enum (emission) | `UvMapping.Channel` | no | yes | duplicate |
| UV transforms | direct ST; pan exactly zero | direct ST; scroll-rotate exactly zero | `UvMapping` | no | yes | duplicate |
| Affine UV composition | not needed | main ∘ bump | expressed exactly | no | yes | **no pressure — core sufficed** |
| Sampling constraints | Point/Bilinear, equal Clamp/Repeat, no mip/bias/aniso | identical | `TextureSampling` | **yes** | no | extracted |
| BaseColor forms | `Constant` \| `Texture` \| `Texture×Constant` | identical | all three | partly | yes | duplicate |
| Alpha proof source | material property + 18 gates | **attested compile-time constant** + 2 gates | `ScalarSemanticValue` | no | yes | **same value, different evidence class** |
| Attestation ↔ feature channel | N/A | lilToon `Alpha` survives *total* feature stripping (measured) | `ScalarSemanticValue` | no | yes | the two evidence channels are proven independent |
| Coverage mechanisms | 5 gates | 2 gates | **none** | no | yes | two producers, no core concept |
| Normal forms | `Unmodified` \| `TangentSpaceNormalMap` | identical | both | partly | yes | duplicate |
| Emission forms | 4 additive slots, slot 0 only | 1 blend, 4 modes, Add only | `ColorSemanticValue` | partly | yes | duplicate |
| Emission sampler | `_MainTex` sampler | `_EmissionMap` own sampler | `TextureSampling` | fact yes | yes | **direct contradiction** |
| Render-mode evidence | material property | compile-time define read from the live pass | none | no | yes | duplicate |
| Output-local invalidation | per-output `Unknown` + scoped diagnostic | identical | `SemanticOutput<T>` | no | no | **confirmed shared** |
| Diagnostics | 10 codes, data not Console | 12 codes, data not Console | none | no | yes | shape identical, sets differ |
| Modified-source refusal | hash mismatch | any of three digest mismatches | none | no | yes | duplicate |

### Consequence: shared-evidence blast radius differs

Because Poiyomi routes every sample through `_MainTex`'s sampler and lilToon does not,
one texture's import state invalidates a different set of outputs in each frontend:

- **Poiyomi** — an unsupported `_MainTex` sampler makes `BaseColor`, `Alpha`,
  `Emission`, and `Normal` all `Unknown`. Only genuinely constant terms survive.
- **lilToon** — the same failure makes `BaseColor` and `Normal` `Unknown`, while
  `Emission` survives (own sampler) and `Alpha` survives (attested, never sampled).

This is the property a new frontend is most likely to get wrong, because it is
invisible unless two texture slots are assigned at once.

## The neutral-claim gating rule

**Rule: an output may only short-circuit to a neutral or zero claim after the
independent mechanisms that could affect it are proven off.**

A neutral claim — `Unmodified`, `Constant(0)`, `Constant(1)` — asserts "nothing here
affects this output". That assertion is unsound if an independent writer is enabled,
regardless of whether the *primary* slot for that output is populated.

Both frontends have four such sites. Seven of the eight gate correctly. The eighth,
`PoiyomiMaterialSemantics.InterpretNormal`, returned `Complete(Unmodified())` as its
first statement, before its writer gates — and that was a **confirmed false positive**,
verified against the pinned upstream source:

`_DetailEnabled` is a `ThryToggle` bound to the `FINALPASS` shader keyword. Enabling
it compiles in `ApplyDetailNormal`, which blends `_DetailNormalMap` into the
tangent-space normal **without reading `_BumpMap` at all**. With `_BumpMap` unassigned
the base normal is Unity's flat `"bump"` default, and the detail blend still perturbs
it. The remaining seven gates were refuted: the RGB-normal writes compile out in the
unlocked variant AMUSE supports, and no decal or parallax code writes the normal.

The lesson generalizes past this one bug: **"the slot is empty" is not evidence that
"the output is unaffected."** Adapter #3 must apply the rule at every neutral-claim
site, and the parity test in
`Tests/Editor/Semantics/Characterization/NeutralClaimGatingTests.cs` exists to enforce it.

**Measured.** The parity test ran 44 cases across both frontends before any fix. Exactly
8 failed — one per `NormalFeatureGates` entry, all at Poiyomi's Normal site — and all 36
other cases passed, confirming by observation that this was the one ungated site of the
eight.

**What those 8 failures do and do not prove.** They prove the old implementation
bypassed *all eight* of its existing safety gates by returning before any of them ran.
They are **not** evidence that all eight shader features independently perturb the
normal. Only `_DetailEnabled` is source-proven to do so in the supported unlocked
variant; the other seven were refuted above. The distinction matters because the fix
deliberately keeps the whole gate set and moves the existing check as a unit, so those
seven now produce a conservative `Unknown` for cases they need not have refused. That
is an accepted false negative — free, and correct if AMUSE ever supports locked
materials, where the RGB-normal writes stop compiling out.

The correction is a pure reordering of the existing gate check above the short-circuit;
no gate was removed or narrowed, and the change is strictly `Complete → Unknown` for
previously unsound cases only. The full EditMode suite passed at 629/629 afterwards, so
no pre-existing test had encoded the defect.

## Repeated pressures on the semantic core

No pressure has yet justified changing `MaterialSemantics`. All are absorbed by
returning `Unknown`.

| Pressure | Producers | Consumers | Classification | Decision |
| --- | --- | --- | --- | --- |
| `sample.rgb × sample.a` (same-sample channel coupling) | **2, independent** | **0** | vocabulary cannot express it | Document. Promote when a *consumer* appears, not a third producer — generality is already established. |
| Coverage versus value | **2, independent** | 0 | vocabulary cannot express it | Strongest core gap found. Both frontends maintain coverage-gate lists; the IR models only the alpha value. Which layer owns coverage — IR, resolver, or planner — is not yet known. |
| Attestation-produced interpretation evidence | 1 (lilToon `CompiledFeatures`) | 1 | belongs to another layer | Document. If a second producer appears, what generalizes is the `InterpretVerifiedMaterial(material, resolvedFacts…)` **seam shape**, not an adapter interface. |
| Borrowed sampler state | 2, opposite directions | — | shader-specific | The *idiom* is confirmed general; the *mapping* is irreducibly per-shader. |
| Declarative gate schema | 2 superficially | — | falsified | The function is byte-identical; what the lists must express is not. lilToon needs "equals `(0,1,1,1)`", "is unassigned", "equals exactly 1", "is provably in `[0,1]`", "`.a == 0`", "`.x == 0`". **No schema.** |
| Premultiplied alpha, non-additive blend modes, layered emission | 2, 1, 2 | 0 | chose not to support | All refuse. |
| Gamma color workflow | 2 | — | chose not to support | Both refuse. |

## What remains duplicated, and until when

| Concept | Extract when |
| --- | --- |
| `FirstFailedZeroGate`, `TryReadBinary`, `RequireAnalyzableMaterial`, `AllUnknown` | adapter #3 confirms byte-identity across **three** producers. Then extract in one pass, only the members that are byte-identical *and* shader-knowledge-free. |
| `IsFinite` overloads | never — trivial |
| `ComputeNormalizedSourceHash` | a third consumer, or extraction of the whole attestation-primitive cluster at once |
| Result / diagnostic / output types | a shared diagnostic framework is justified by **consumers**, not producers; there are none |
| Gate lists and every equation | never — categories C and D |
| Fixture bases and stand-in shaders | never; but the *assertion helpers* should be reconciled so each adapter gets both the `IsSupportedMaterial` check and the diagnostic-count check |
| Sampled `[0,1]` range proof | a second frontend needs the identical contract |
| Attestation models | never — two shaders produced two irreconcilable shapes |

**Do not extract on two producers.** The count is the evidence.

## Future abstraction candidates and the evidence each lacks

| Candidate | Evidence present | Evidence missing |
| --- | --- | --- |
| Attestation-primitive cluster | identical rule, two producers | a third producer, or a demonstrated cost of the current duplication |
| Same-sample `rgb × a` color form | two independent producers | **any consumer** |
| Coverage-versus-value concept | two independent producers | which layer owns it |
| Resolved-facts interpretation seam | one producer | a second producer |
| Declarative gate schema | superficial shape match | a shared predicate vocabulary — currently falsified |
| `IShaderAdapter` / registry | two frontends with the same call shape | **any polymorphic call site** |

The last row is decisive. Both frontends expose `AnalyzeBaseMaterial(Material)` and
could trivially share an interface — and **nothing in the repository dispatches over
adapters.** An interface with two implementations and zero consumers is a speculative
abstraction, not a design.

## Guidance for shader frontend #3

**Learn first, in priority order:**

1. Establish a compile + EditMode CI gate. Adding a third ~1 000-line frontend to a
   repository with no automated test gate compounds risk faster than it adds coverage.
2. Apply the neutral-claim gating rule at every site, and extend the parity test.
3. Respect the sampler blast-radius property; assert it explicitly for the new shader.

**Choose the shader to falsify something.** Poiyomi and lilToon are both
VRChat-ecosystem toon shaders. They already agree on: linear color space required,
Unity `TextureImporter` as the evidence source, exactly-off boolean gates,
`_MainTex`/`_BumpMap`/`_EmissionMap` slot naming, and the four-output decomposition
itself. A third toon shader would confirm little. A frontend stressing a different
axis — a Standard/URP-lit shader, or one with no source available at all — would test
whether the four-output vocabulary is a real abstraction or a toon-shader coincidence.

**Expect to duplicate:** its own attestation model, gate lists, equations, diagnostic
code set, and fixture shader. Copying the byte-identical helpers a third time is
correct; extracting them *at that point* is also correct.

**Write the fixture shader to the consumed contract exactly.** Measured across the two
stand-in fixtures: every one of lilToon's 44 fixture properties is named in production,
while 5 of Poiyomi's 94 are not (`_Mode`, `_Cutoff`, `_SrcBlend`, `_DstBlend`,
`_AlphaMask`). Neither is wrong — the extra Poiyomi properties are what make an
irrelevant-state leak testable at all, and the lilToon fixture's exactness is what makes
its consumed contract self-documenting. The trade-off is worth making deliberately
rather than by accident: a fixture with no unread surface cannot express the
"irrelevant property" invariant, and must fall back to testing irrelevant *changes*
(UV state that nothing samples through), which is what
`IrrelevantChangeInvarianceTests` does for lilToon.

## Notes toward third-party integration

Not a current goal. Recorded because the two frontends make the constraints visible.

- **Attestation is the blocker, not semantics.** Any third-party frontend must ship
  pinned digests it measured itself. No mechanism exists for that, and designing one
  before AMUSE's own adapters are stable would be premature.
- **`UnityTextureEvidence` is the natural first public surface** — small,
  shader-independent, refusal-predicate contract, already guarded. It is `internal`
  and should stay so until an external consumer exists.
- **The four-output vocabulary may not survive contact with a third party.** It has two
  producers, both toon shaders. Publishing it as an extension point would freeze it on
  the weakest evidence in this document.
