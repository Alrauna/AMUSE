# lilToonMulti full-parity support design

Date: 2026-09-30. Status: Stage A (mode 0, both containers) implemented
and validated as of 2026-10-01. The full-suite failure set is identical to
the base commit's pre-existing items. Stages B and C pending.

Parent context: the decision record of this date
(`docs/superpowers/investigations/2026-09-30-liltoon-multi-support-path.md`),
the three merged 2026-09-30 characterization notes, F0
(`2026-08-30-liltoon-family-applicability.md`), B2 (cutout alpha
semantics), T1 (transparent alpha semantics), and the S8/S9 outline
support work. The architecture questions are settled there: unified
semantic core, regular leads, Multi follows.

Privacy note. This document names public vendor facts, repository
facts, and whole-corpus counts. It names no private avatar, renderer,
material, path, machine, instance, port, or hash.

## Scope, set by the product owner

Full parity between the supported regular lilToon identities and the
Multi identities that map onto them. The Census Lab corpus is
evidence, not the completeness criterion. The corpus happens to hold
only mode 0 materials; parity is not mode 0 only.

The parity set. Every Multi identity whose nearest regular identity is
supported as of 2026-09-30:

| Multi identity | Regular counterpart | Regular family as of 2026-09-30 |
|---|---|---|
| `_lil/lilToonMulti` mode 0 | `lilToon` | LilToon |
| `_lil/lilToonMulti` mode 1 | `Hidden/lilToonCutout` | LilToonCutout |
| `_lil/lilToonMulti` mode 2 | `Hidden/lilToonTransparent` | LilToonTransparent |
| `Hidden/lilToonMultiOutline` mode 0 | `Hidden/lilToonOutline` | LilToon |
| `Hidden/lilToonMultiOutline` mode 1 | `Hidden/lilToonCutoutOutline` | LilToonCutout |
| `Hidden/lilToonMultiOutline` mode 2 | `Hidden/lilToonTransparentOutline` | LilToonTransparent |

Six identities. The three specialized containers refuse, exactly as
their regular counterparts refuse:

- `Hidden/lilToonMultiRefraction` refuses (GrabPass representation).
- `Hidden/lilToonMultiFur` refuses modes 4 and 5 (generated shell
  geometry).
- `Hidden/lilToonMultiGem` refuses (additive background accumulation).

`RefractionBlur` and `FurTwoPass` have no Multi container at the pin,
so parity needs nothing there. `[SOURCE]`, vendor mode map,
`Editor/lilMaterialUtils.cs:184-266`.

## Premises agreed with the product owner

1. Unified core, regular leads, Multi follows. The follower layer is
   the only Multi-aware code outside the attestation profiles and the
   hub routing.
2. The theorem premises of the three shipped regular families do not
   change. The follower feeds them resolved evidence or refuses.
3. Version anchor is 2.3.4 at landing. Earlier versions flow through
   the existing per-build consent path until their rows are measured
   and backfilled.
4. Full parity means the six identities above admit, gate, and convert
   on the same terms as their regular counterparts. A regular
   capability that has no Multi structural counterpart (one-pass,
   two-pass) needs no Multi work, because Multi transparent is always
   Normal-class. `[SOURCE]`, mapping note section 7 item 4.
5. The two open product decisions are settled in this document, each
   with its load-bearing analysis and the rejected alternative.

## The mode-consistency gate

The follower owns one new proof obligation: the material's Multi
state must agree before any regular theorem runs.

Captured state per material:

1. `_TransparentMode` scalar, with animation reachability resolved by
   the existing per-slot machinery.
2. The material keyword set.
3. `_AsOverlay`, `_UseClippingCanceller`, `_UseOutline` scalars.
4. Effective render queue and RenderType tag.
5. Container identity from the exact shader name and GUID.

The gate rules:

1. The keyword set must be a state the pinned vendor derivation
   produces from the captured scalars. The derivation table is the
   one at the pin: mode keywords from `_TransparentMode`
   (`Editor/lilMaterialUtils.cs:397-399`), feature keywords from the
   captured feature scalars (`:455-468`), alpha mask
   `_COLOROVERLAY_ON` from mode non-zero and `_AlphaMaskMode`
   (`:466`), dither `ETC1_EXTERNAL_ALPHA` from mode one and
   `_UseDither` (`:399`), distance fade `_FADING_ON` from the
   parallax usage rows (`:468`), dissolve `GEOM_TYPE_BRANCH_DETAIL`
   from the layer dissolve rows. A keyword state the table cannot
   derive refuses as mismatch. `[SOURCE]`
2. `_TransparentMode` must be animation-invariant in the admitted
   set. The float is inert at render time (no HLSL reads it), but a
   slot that reaches a different mode value breaks agreement between
   the scalar and the keyword-derived behavior. The existing exact
   agreement rule for relevant animated values handles this; the
   gate refuses the slot.
3. `_UseClippingCanceller` must be 0. It has a render-time consumer
   (`LIL_MULTI_SHOULD_CLIPPING`, `Shader/Includes/lil_common.hlsl:46`),
   and the regular families carry no matching gate to inherit.
4. `_AsOverlay` must be 0. See the decision section.
5. The three animation-derived color keywords (`GEOM_TYPE_LEAF`,
   `EFFECT_HUE_VARIATION`) are tolerated present or absent. They map
   to rim-light direction and tone correction, which are not alpha
   facts. On the NDMF play-mode path the vendor preprocess returns
   early and they never appear. `[SOURCE]`,
   `Editor/lilMaterialUtils.cs:476-498`,
   `External/Editor/VRChatModule.cs:55-57`.

## Per-mode gates and reused theorems

### Mode 0, both containers

The opaque theorem is reuse without change. `LIL_RENDER 0` compiles
the alpha path out and forces alpha to one
(`Shader/Includes/lil_pass_forward_normal.hlsl:396`). The outline
container at mode 0 forces outline alpha to one (`:228-229`), so the
S8 outline-alpha question is moot and the same-asset target carries
the outline passes. The gate surface is:

- The opaque coverage gates (fragment removal mechanisms), unchanged
  from `LilToonMaterialSemantics`.
- The mode-consistency gate.
- Queue and RenderType gates admitting the vendor Multi opaque forms:
  explicit 2000, or the vendor written state (RenderType override
  empty, queue −1, which resolves to the container default). The
  observed corpus sits at explicit 2000 on both containers.
  `[SOURCE]`, `Editor/lilMaterialUtils.cs:44-52`;
  `[MEASURED 2026-09-30]`.

### Mode 1, base container (cutout)

The cutout theorem is reuse: coverage is the cutout transform of the
plain main sample times tint alpha, every optional coverage feature
proven off, above `MaxProvableCutoff`. The equation is the pinned
one at `:402-403` for both families. The Multi adjustments:

- Feature state is keyword-carried. The runtime gates stay on the
  captured scalars, exactly as the regular cutout family gates.
  `_AlphaMaskMode` off proves the alpha mask off; `_UseDither` off
  proves dither off; the layer dissolve scalars off prove dissolve
  off. The keywords ride only in the consistency gate, because the
  vendor derives them from these same scalars.
- `MaxProvableCutoff` and the cutoff evidence carry over unchanged.
- Queue gate: the vendor cutout form writes 2450 and RenderType
  TransparentCutout, blend One/Zero, `_AlphaToMask` 1. The gate
  admits the vendor state and the explicit-value equivalent.
  `[SOURCE]`, vendor mode map.

### Mode 2, base container (transparent)

The transparent theorem is reuse with one structural simplification:
Multi transparent has no `FORWARD_BACK` pre-pass and no `_PreCutoff`,
so the TwoPass and OnePass questions never arise. The identity is
Normal-class only. The gates:

- Every cutout-shared coverage feature gate from T1.
- The transparent-only post-clip writers: the ForwardAdd premultiply
  (present, because `FORWARD_ADD` is always in the Multi container),
  the subpass shadow clip (`_SubpassCutoff`, unconditional in both
  families), and distance fade. Distance fade is keyword-carried in
  Multi: `_FADING_ON` compiles `LIL_FEATURE_DISTANCE_FADE`, and at
  `LIL_RENDER 2` that block writes alpha after the clip
  (`Shader/Includes/lil_common_frag.hlsl:2048`). The T1 gate on the
  `_DistanceFade` strength stays the runtime gate.
- Queue gate: the vendor transparent form writes 2460, with the
  worlds override writing 3000. The gate admits the vendor forms.
  `[SOURCE]`, `Editor/lilMaterialUtils.cs:337-351`.

### Outline container modes 1 and 2

The regular outline identities are supported (S8, S9), so parity
requires the outline rows. The outline copy of the forward file
compiles the same equations on outline alpha: mode 1 clips and
discards on outline alpha (`:232-233`), mode 2 clips (`:236`). The
outline-alpha gates mirror the T2 gate rows over the shared
`_Outline*` property set, which is byte-identical between the Multi
containers and carries the same defaults. `_UseOutline` is live
container state on the outline container and inert residue on the
base container; the gate treats it per container. `[SOURCE]`,
outline note sections 3 and 4.

## Capture extension

Today no production code reads material keyword state or pass-enable
state as evidence. The capture request model
(`MaterialEvidenceRequest`) is property-shaped. The follower needs:

1. One new evidence fact on the request and captured evidence: the
   material keyword set. Captured as an immutable sorted list.
2. No pass-enable capture in this slice. `_AsOverlay` is gated as a
   scalar, which the existing property capture already carries.

This is the audit's R2 surface named honestly: one new field on
`MaterialEvidenceRequest` and on `CapturedMaterialEvidence`, one new
typed source-evidence record for Multi on `CapturedAlphaMaterial`.

## Hub and family routing

One new family member, `LilToonMulti`, exists only between
classification and resolution:

1. `ClassifyShaderName` maps the two supported container names to
   `LilToonMulti` with a combined Multi evidence request. The
   specialized container names stay in the unsupported path and gain
   named refusals at the resolver.
2. After capture and attestation, the resolver runs once per
   material. On success, the stored family becomes the resolved
   regular family, and every downstream switch
   (`AlphaRequestForFamily`, conversion dispatch, canonical
   properties) sees only the three regular families. On refusal, the
   material carries the named Multi refusal and follows the existing
   unsupported path.
3. The resolver is one function with one input record. The audit's
   twin-dispatch risk (R1) is met by keeping resolution out of the
   per-family switches entirely.

## Attestation

1. The canonicalizer extends to the `*LIL_SHADER_SETTING_MULTI*`
   region. The `SettingDefine` vocabulary already covers the block's
   valueless define forms; the region detection must learn the block.
   Everything else stays retained. `[SOURCE]`,
   `Editor/Semantics/LilToon/LilToonSourceAttestation.cs:687-689,
   1067-1071`.
2. Two profiles, one per supported container: name, GUID, canonical
   source digest, measured from installed shapes across at least two
   real settings shapes. The `LIL_RENDER` scan does not apply; the
   profile's mode evidence is the pinned derivation contract.
3. Version rows: 2.3.4 only at landing. The include tree is shared
   with the existing rows. Earlier versions stay on the consent path.
4. The specialized containers carry no digests. They refuse by
   container identity before attestation matters.

## Conversion recipe

1. Target is the source asset itself. The clone stays on its
   container, carries mode-0 keyword state written explicitly
   through the keyword API, and takes the base canonical property
   writes.
2. Canonical queue and tags, decided this session: the clone writes
   RenderType Opaque and queue 2000 explicitly. Load-bearing
   analysis: the regular canonical targets (`lilToon`,
   `Hidden/lilToonOutline`) all declare Opaque/2000, so explicit
   writes are exact effective-state parity with every regular
   target. The vendor form (override empty, queue −1) resolves per
   container to 2000 or 2900, which would make one recipe produce
   two states and would move the observed outline population from
   2000 to 2900. Vendor round-trip fidelity is not a preservation
   requirement, because clones are NDMF build-copy transients, never
   source assets. Appearance is neutral either way for opaque
   ZWrite-on geometry. Rejected alternative documented here.
3. The keyword set on the clone is exactly the mode-0 set the
   derivation produces: mode keywords off, alpha feature keywords
   off, the always-on color feature set per vendor derivation.
4. The pass-enable state question is out of slice scope. `_AsOverlay`
   gates at 0.

## The `_AsOverlay` decision, with analysis

`_AsOverlay` 1 is the editor-disabled ShadowCaster state
(`Editor/lilMaterialUtils.cs:400-404`). Decision: refuse in this
slice with a named value, and gate `_AsOverlay == 0`.

Load-bearing analysis:

1. Population: 0 of 136 observed materials set it
   (`[MEASURED 2026-09-30]`).
2. Failure mode if silently mishandled: the clone re-enables shadow
   casting and new shadows appear. The failure is visible, which
   makes refusal the conservative side of an asymmetric risk.
3. Cost to support: pass-enable capture (new evidence kind), a
   characterization proving the clone path preserves the pass-enable
   list, a revalidation row, and a clone write. Moderate scope, no
   observed consumer.
4. The gate costs one scalar check the existing capture already
   carries.

A Stage 0 characterization measures whether `new Material` copies the
pass-enable list, so the follow-up branch starts from a measured
fact.

## Refusal vocabulary

New named values in a closed Multi refusal enum, one report key each:

1. Mode outside the admitted set (3 to 6 on a supported container).
2. Specialized container (refraction, fur, gem).
3. Keyword and mode mismatch.
4. Clipping canceller enabled.
5. Overlay pass enable unsupported (`_AsOverlay` 1).
6. Attestation failed for the container.
7. Missing keyword evidence.

Transport: a refused Multi material answers all-Unknown alpha and
carries one new shared cause kind, `AlphaUnknownKind.UnsupportedMultiState`,
whose feature field names the refusal value in words, with its own
report sentence in `AmuseReportStrings.cs`. The existing report
completeness tests enforce the sentence, exactly as they do for the
three existing kinds. No regular-family refusal value changes meaning;
the shared cause vocabulary grows by one additive kind, nothing else.

## Test strategy

Two new schema-only stand-in shaders under the test hidden namespace,
shaped like the containers: no `LIL_RENDER` define, the Multi keyword
declarations, the `For Multi` scalar block. Tests substitute
attestation through the existing seams and run real production logic.

Falsifiers, one per plausible wrong implementation:

1. A resolver that trusts the shader name alone without keyword
   evidence fails the fixture where mode keywords disagree with
   `_TransparentMode`.
2. A resolver that reuses regular evidence requests without the
   keyword set fails the capture fixture for a Multi material.
3. A conversion that leaves the clone at container-default tags fails
   the outline-container clone fixture (clone must land at 2000,
   Opaque, not 2900).
4. A conversion that forgets the keyword write fails the fixture
   where the clone must render mode 0.
5. A mode-1 gate copied from the regular family that gates absent
   Multi scalar state fails the keyword-carried feature fixture.
6. A mode-2 gate that demands a `_PreCutoff` analogue or refuses the
   missing pre-pass fails the Normal-class transparent fixture.
7. An outline row that treats outline alpha as proven from base
   alpha fails the mode-1 outline fixture.
8. A resolver that admits mode 4 because the base theorem compiled
   fails the fur-container refusal fixture.
9. A canonicalizer that retains the Multi settings block fails the
   shape-agreement fixture (settings-only change must not move the
   digest).
10. An attestation that admits a container by the regular include
    tree alone fails the wrong-version fixture.

## Stages and checkpoints

Stage 0 pins, no product behavior change:

1. Canonicalizer Multi region with the falsifier above.
2. Digest measurement for the two containers from installed shapes,
   two settings shapes, in a throwaway project outside this
   repository.
3. Characterization: what `new Material` inherits for tags on each
   container, and whether it copies the pass-enable list. Dated
   sanitized record.
4. The admitted keyword-state table per identity, consolidated from
   the pinned derivation, as the gate's closed input domain.

Stage A: mode 0 on both containers, end to end. Capture extension,
resolver, refusal enum, attestation profiles, hub routing, recipe,
falsifiers 1 to 4 and 9 to 10.

Stage B: mode 1 on both containers. Cutout gates, eligibility rows,
falsifiers 5 and 7.

Stage C: mode 2 on both containers. Transparent gates, eligibility
rows, falsifier 6.

Each stage ends with the full product suite green. The product owner
re-runs the Lab characterization after Stage C as a regression check;
the corpus holds no mode 1 or mode 2 materials to classify.

## Risks

1. Stage 0 may pin a derivation-table entry that contradicts a gate
   premise here, most plausibly the `_FADING_ON` row, which the
   vendor derives from parallax usage. A contrary pin changes the
   gate table, not the architecture.
2. The digest measurement may find a third varying region in the
   Multi assets. The canonicalizer gains that region or the version
   refuses; the shape-agreement falsifier catches it.
3. The two-phase family routing touches the hub. The change is one
   branch, one resolution point, and the full suite runs at Stage A.
4. `new Material` may drop keyword state or pass-enable state. The
   Stage 0 characterization answers both before Stage A recipe work;
   a dropped keyword list means the recipe writes it explicitly,
   which the recipe already does.

## Open items

None blocking. Two follow-ups live outside this slice: the pass-enable
support branch after the Stage 0 characterization, and the measured
version-row backfills for 2.3.0 through 2.3.3.
