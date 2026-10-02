# lilToon distance fade support design

Date: 2026-10-01. Status: proposed. Branch: `feat/liltoon-distance-fade`.
Base: `main` at `87d7f47`.

Parent context: the 2026-10-01 investigation
(`docs/superpowers/investigations/2026-10-01-liltoon-distance-fade-refusal-investigation.md`).
That record establishes every vendor fact and every gate site this design
rests on. This document turns its "What support requires" section into
decisions.

Privacy note. This document names public vendor facts, repository facts, and
whole-corpus counts. It names no private avatar, renderer, material, path,
machine, instance, port, or hash.

## Scope, set by the product owner

Support the vendor distance fade feature on every lilToon and lilToonMulti
identity AMUSE supports on 2026-10-01. Support means: a material with the
feature active no longer reports an unsupported feature, and no conversion
loses the fade. The identities:

- The transparent family: `Hidden/lilToonTransparent`,
  `Hidden/lilToonOnePassTransparent`, `Hidden/lilToonTwoPassTransparent`,
  `Hidden/lilToonTransparentOutline`,
  `Hidden/lilToonOnePassTransparentOutline`,
  `Hidden/lilToonTwoPassTransparentOutline`.
- The cutout family: `Hidden/lilToonCutout`,
  `Hidden/lilToonCutoutOutline`.
- The Multi containers: `_lil/lilToonMulti`, `Hidden/lilToonMultiOutline`,
  every effective mode.

The opaque family stays target-only. It never reads the feature in this
slice. Poiyomi keeps its own `_AlphaDistanceFade` gate list and sees no
change. The Lite family compiles the feature out and sees no change.

## The support rule

One rule covers every donor family, from the vendor fragment block
(`lil_common_frag.hlsl:2017-2064` at the pin, byte-identical to 1.10.3):

1. A finite zero strength (`.z == 0`) is the inert state. Every arm carries
   the strength as its lerp weight, so the whole feature is an exact no-op.
   Today's proofs run unchanged.
2. A finite nonzero strength retains the material. The color arm runs at
   every render mode, so a converted triangle would lose the fade at some
   camera distance. The material keeps every triangle on itself. The slot
   analysis reports retention as a named, expected outcome.
3. A non-finite component keeps today's refusal path unchanged. The capture
   is broken, the finiteness vocabulary already names it, and the pinned
   tests stay.

Retention is not a missed optimization. The color arm alone forbids moving
any triangle, so the current refusal already donates nothing. This design
changes the classification and the wording, not the extraction outcome.

## Decisions, each with the rejected alternative

### Decision 1: retention rides the Unknown path as a named kind

The alpha semantics still answer Unknown for a retained material. The answer
carries a new `AlphaUnknownKind.FeatureRetention` value, a new
`LilToonSemanticDiagnosticCode.FeatureRetention` value, and a new
`RendererAnalysisRefusal.AdmittedMaterialRetainedByFeature` value.

Analysis. A retained material cannot donate, so a proven alpha answer would
be unused. Proving alpha anyway (the color alpha 1 case) adds machinery with
no consumer. Rejected alternative: return a complete alpha answer plus a
slot-level retention flag. That splits the retention fact across two
channels and risks a consumer reading the alpha answer without the flag.

The slot refusal value changes from `AdmittedMaterialSemanticsUnknown` to
`AdmittedMaterialRetainedByFeature` when the reason kind is retention. The
generic value keeps its meaning for genuine unknowns. The
`RendererAnalysisRefusal` enum is a closed per-scope vocabulary, so one
named member is its designed extension. The per-reason refusal buckets size
from the enum length and need no change.

Rejected alternative: keep `AdmittedMaterialSemanticsUnknown` for retention
and change only the fact sentence. The report would then read "reason:
semantics unknown" for an expected outcome, which misdescribes the state.

### Decision 2: the conversion eligibility vocabulary does not change

`LilToonOpaqueConversionRefusal.UnsupportedDistanceFade` keeps refusing
conversion for a nonzero strength, in the transparent family, the cutout
family (new), and the Multi mirrors. The name stays accurate at its layer:
the conversion is genuinely unsupported, because the moved triangles would
lose the fade. The cutout family gains the same three presence, finiteness,
and strength gates the transparent family already runs.

### Decision 3: one shared gate evaluator

One internal evaluator, `LilToonDistanceFadeSemantics`, reads the captured
vector and answers Inert, Retained, or NonFinite. Both frontends record its
answer through their existing diagnostic flow. Two call sites justify one
helper because the strength rule must not drift between the families.

Rejected alternative: inline the check in each family, as the gates do
today. The transparent family already diverged from nothing; a second copy
doubles the drift risk for a five-line rule.

### Decision 4: the report keeps its key and severity, with a new sentence

`FeatureSentence` gains a retention case. The sentence names the feature,
the property, and the retained outcome. The slot report keeps the
`amuse.slotAnalysis.Refusal` key and its warning severity in this slice.

Analysis. A dedicated report key at a lower severity touches the dedup and
report-count machinery for wording gain only. The 2026-09-29 dedup design
treats the key set as load-bearing. Rejected alternative: new
`amuse.slotAnalysis.Retained` key at information severity. Recorded as a
follow-up candidate, not this slice.

### Decision 5: the stronger transformation stays out

The canonical opaque target could carry the donor's fade state, so moved
triangles keep the color fade. That direction needs the color property
capture, a binary32 identity argument for the color alpha 1 case, and a
position on the vendor build-time feature scan for the generated target. It
needs its own spec. This slice does not capture `_DistanceFadeColor`,
`_DistanceFadeMode`, or the rim properties.

## The gate contract

The evaluator takes the captured evidence and returns one of three answers:

1. `Inert`: the vector is present, finite, and `.z == 0`. The caller proves
   alpha exactly as today.
2. `Retained`: the vector is present, finite, and `.z != 0`. The caller
   records one Alpha diagnostic with the `FeatureRetention` code and the
   property name `_DistanceFade`.
3. `NonFinite`: the vector is present and any component is not finite. The
   caller records one Alpha diagnostic with the `UnsupportedFeature` code
   and the property name, exactly as today.

An absent vector is not the evaluator's concern. The transparent family
already refuses absence at its schema gate, and the cutout family gains the
same treatment at its eligibility gate. The alpha request declares the
vector, so a capture on an attested shader always has it.

The vendor default is `(0.1, 0.01, 0, 0)`, so an untouched material is
Inert. The census scan measured the nonzero population: three
`Hidden/lilToonTransparent` materials, one `Hidden/lilToonTransparentOutline`
material, one `Hidden/lilToonTwoPassTransparent` material, eight Multi
materials at opaque mode, and sixteen opaque materials. The opaque and
opaque-mode Multi materials never reach a conversion and never refuse.

## Cutout correctness

The cutout family neither captures nor gates `_DistanceFade` on the base
commit. The vendor fades cutout color at every render mode and fades cutout
coverage through the dither path (`lilDistanceFadeAlphaOnly`,
`lil_common_frag.hlsl:531-545`). A converting cutout material with a nonzero
strength loses the fade on moved triangles today. This slice closes the
gap: capture the vector, add the semantics gate, add the three eligibility
gates. The census cutout population is zero on 2026-10-01, so the fix is
latent hardening, not a live repair.

## Multi parity

The Multi containers delegate to the regular families by construction. Once
the regular families carry the rule, the Multi modes inherit it:

- Mode 2 (transparent) resolves through the transparent semantics and gains
  retention with no Multi change. The mirrored eligibility rows already
  refuse the nonzero strength.
- Mode 1 (cutout) resolves through the cutout semantics and delegates
  eligibility to the cutout evaluator, so both gates arrive through
  delegation.
- The mode-consistency gate already derives `_FADING_ON` from the strength
  (`LilToonMultiModeGate.cs:519-525`), and its transcription matches the
  vendor (`lilMaterialUtils.cs:359, :416, :506-509`). No change there.

The Multi work in this slice is tests only: one falsifier per mode that a
nonzero strength retains or refuses exactly as its regular twin, and one
falsifier that the keyword state cannot bypass the strength gate.

## Refusal and report vocabulary

New closed-vocabulary members, one report sentence each:

1. `LilToonSemanticDiagnosticCode.FeatureRetention` (lilToon frontend).
2. `AlphaUnknownKind.FeatureRetention` with the factory
   `AlphaUnknownReason.FeatureRetention(feature, property)`.
3. `RendererAnalysisRefusal.AdmittedMaterialRetainedByFeature`.

The report completeness rule stands: a new kind without a sentence fails
the tests. `FeatureSentence` composes the retention sentence inline, in the
style of the `UnsupportedFeature` case.

## Test consequences

Tests that flip, with the plausible wrong implementation each one
falsifies:

1. `DistanceFadeEnabled_IsUnknownNamingDistanceFade` in
   `LilToonTransparentAlphaTests` becomes a retention expectation. It keeps
   its falsifier duty: a gate on `_DistanceFadeColor.a` instead of `.z`
   must still fail it.
2. The cutout capture shape test gains `_DistanceFade` in the vector list.
3. The slot resolution expectation for a faded transparent slot changes to
   the retained refusal value.

Tests that must keep passing unchanged: both non-finite semantics tests,
both transparent eligibility tests, the Multi transparent eligibility
falsifier, and every zero-strength proof test.

New falsifier duties, marked `--- Falsifier N ---` in the plan:

1. A nonzero strength must never donate in any family or mode, including
   Multi cutout mode through delegation.
2. A zero strength must behave exactly as the base commit, in every family.
3. A non-finite strength must refuse with the non-finite vocabulary, not
   retention.
4. The `_FADING_ON` keyword state must not bypass the strength gate on
   Multi.
5. A retained slot must keep its original material with no moved
   triangles and no clone.
