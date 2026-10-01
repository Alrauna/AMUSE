# Decision record: the longest-horizon path for lilToonMulti support

Date: 2026-09-30. Status: decision session complete on 2026-09-30. This record
is the only file the session created. No production code changed. No spec or
plan was authored. No test ran. No Unity instance was touched.

Work branch: `research/liltoon-multi-path`, cut from `main` at `728079c`
(`728079c8304829c6dad8d5a68b4a77abcc8b9a7b`, the merge of PR #120), the
expected base. The tree was clean at session start.

Privacy note: this record is sanitized. It names public vendor facts,
repository facts, and aggregate corpus counts only. It names no private
avatar, renderer, material, texture, hierarchy, path, machine, instance,
port, or hash. Corpus observations appear only as whole-corpus counts, never
as per-avatar or per-renderer rows.

## 1. Question and scope

Three decisions settle the longest-horizon path for lilToonMulti support:

1. Q1: does Multi attach to one unified lilToon semantic core, or live in a
   separate Multi-specific semantic subtree?
2. Q2: if unified, which shader is the lead (core of understanding) and which
   is the follow (architectural addition)?
3. Q3: which lilToon versions does the Multi path anchor to at landing?

This record recommends one answer per question, states the evidence that
would reverse each answer, and names the falsifier-style cases each wrong
choice would fail. It writes no production code and authors no spec.

Labels: `[SOURCE]` marks a pinned upstream or repository fact with a file and
line. `[MEASURED]` marks the 2026-09-30 corpus observation recorded in the
merged companion notes. `[INFERENCE]` marks a bounded conclusion.
`[DECISION]` marks this session's recommendation. `[OPEN]` marks an unresolved
question for future work.

## 2. Method and pins

- Upstream pin: lilToon tag `2.3.4`, commit
  `252fd8cfc46106d4967e95b3f2c788418502f227`. Every upstream cite is
  file:line relative to `Assets/lilToon/` at that commit.
- This session re-read the load-bearing upstream facts first-hand at the pin
  rather than restating the companion notes: the base container asset
  (`ltsmulti.shader`), the keyword remap include
  (`Shader/Includes/lil_replace_keywords.hlsl`), the Multi feature baking
  (`Shader/Includes/lil_common.hlsl`), the regular pass asset
  (`Shader/ltspass_opaque.shader`), the Multi outline container
  (`Shader/ltsmulti_o.shader`), the input-buffer branch
  (`Shader/Includes/lil_common_input.hlsl`), the editor mode map and keyword
  derivation (`Editor/lilMaterialUtils.cs`), the startup migration
  (`Editor/lilStartup.cs`), the manager fields (`Editor/lilShaderManager.cs`),
  and the Multi settings writer (`Editor/lilToonSetting.cs`).
- Repository reads: the three merged 2026-09-30 companion notes,
  `docs/architecture/vision.md`, F0
  (`2026-08-30-liltoon-family-applicability.md`), the four shipped
  regular-family semantic types, `LilToonSourceAttestation.cs`, the
  2026-09-07 generator-shape attestation spec, the 2026-09-29 abstraction
  audit, and `UnityMaterialSemantics.cs`.
- Method constraint honored: no test run, no build, no Unity run. Q3's
  verdicts therefore stay pre-measurement and say so.

## 3. Settled baseline (not re-litigated)

These facts are settled by the merged 2026-09-30 notes and this session
builds on them without re-deriving them:

1. The non-outline cutout equation is the screen-derivative coverage
   transform plus discard at
   `Shader/Includes/lil_pass_forward_normal.hlsl:402-403`; the outline copy
   runs the same equation on outline alpha at `:232-233`. F0's line-236
   citation is the recorded finding, not a live dispute.
   `[SOURCE]`
2. The Multi containers regenerate under both `ApplyShaderSetting` overloads
   and under the per-build apply and restore cycles, from
   `BaseShaderResources/*.lilinternal` templates with token substitution.
   `LIL_IGNORE_SHADERSETTING` decouples compiled Multi behavior from the
   rewritten bytes. The varying regions are the feature block, the
   `*LIL_SHADER_SETTING_MULTI*` block, the SRP version, the skip-variant
   sets, the light-mode names, and the version constant. `[SOURCE]`
3. The observed corpus is 136 materials: 127 on `_lil/lilToonMulti`, 9 on
   `Hidden/lilToonMultiOutline`, all `_TransparentMode` 0, all queue 2000,
   and zero materials on the refraction, fur, and gem containers.
   `[MEASURED 2026-09-30]`
4. The F0 roadmap row 7 prerequisite (the regular cutout slice shipped) is
   discharged per the outline note's section 10, dated 2026-09-30. The
   cutout and transparent alpha theorems are shipped production types with
   measured attestation pins. `[SOURCE]`

## 4. Q1: one core or a separate Multi subtree

### 4.1 The two architectures, framed concretely

**Unified.** One lilToon semantic core: the shipped types stay the only
owners of lilToon alpha meaning. `LilToonMaterialSemantics`,
`LilToonCutoutMaterialSemantics`, and `LilToonTransparentMaterialSemantics`
keep their theorems. A Multi attachment layer resolves Multi evidence into
identities the core already attests, adjusts the container deltas, and
refuses what it cannot resolve. `LilToonSourceAttestation` grows a second
profile shape for the containers; the profile rows sit beside the existing
nine. `UnityMaterialSemantics.ClassifyShaderName` routes the two populated
Multi names to the resolution layer instead of to `Unsupported`.

**Separate.** A Multi-specific subtree (for example
`Editor/Semantics/LilToonMulti/`) with its own semantic types per container
and mode, its own evidence request vocabulary, its own attestation profile
kind, and its own diagnostics. The existing lilToon types never see a Multi
material. The hub gains a family member (or several) that routes into the
subtree, exactly like the Poiyomi family does.

### 4.2 What is literally shared at the pin, and what is structurally different

Literally shared — one vendor file, one equation, one evidence surface:

| Behavior | Evidence at the pin |
|---|---|
| Terminal include chain | Every Multi forward pass includes `Includes/lil_pipeline_brp.hlsl` then `Includes/lil_common.hlsl` then `Includes/lil_pass_forward.hlsl` (`ltsmulti.shader:759-763`, `ltsmulti_o.shader:759-766`); regular passes include the same chain without the remap step (`ltspass_opaque.shader:797-801`). The dispatcher routes the non-Lite family to `Includes/lil_pass_forward_normal.hlsl`. `[SOURCE]` |
| Alpha equation | `lil_pass_forward_normal.hlsl` is one file for both families. Same effective `LIL_RENDER` compiles the same branch: opaque `fd.col.a = 1.0` (`:396`), cutout coverage transform plus discard (`:402-403`), transparent clip (`:411`). `[SOURCE]` |
| Coverage machinery | The alpha-mask, dissolve, and dither expansions live in `Includes/lil_common_frag.hlsl` for both families; only the carrier of the feature define differs (`LIL_FEATURE_*` baked in regular assets vs keyword-derived in Multi), and the Multi input define is hard-set in the asset, so the guard is equivalent. `[SOURCE]` |
| Property surface | `_Cutoff`, `_SubpassCutoff`, `_Color`, `_MainTex`, and the full `_Outline*` set are the same properties; the base and outline Multi containers share a byte-identical property block. `[SOURCE]` |
| Shadow and outline pass machinery | Both families compile outline behavior from the same `LIL_OUTLINE` copy of the same forward file; the regular asset declares outline passes this way at `ltspass_opaque.shader:838-866`. `[SOURCE]` |
| Texture and sampler declarations | Shared, outside the `LIL_MULTI` input branch (`Includes/lil_common_input.hlsl:832-897`). `[SOURCE]` |

Structurally different — the evidence model, not the math:

| Difference | Evidence at the pin |
|---|---|
| Mode carrier | Regular: the shader asset is the mode (`#define LIL_RENDER 0` at `ltspass_opaque.shader:636-640`; zero mode keywords). Multi: mode is keyword-derived at include time (`Includes/lil_replace_keywords.hlsl:57-63` remaps `UNITY_UI_CLIP_RECT` to 2, `UNITY_UI_ALPHACLIP` to 1, else 0), and the editor derives those keywords from `_TransparentMode` at edit time (`Editor/lilMaterialUtils.cs:397-399`); the assets declare them as `shader_feature_local` (`ltsmulti_o.shader:757-766`). The shader name proves the container, not the behavior. `[SOURCE]` |
| `_Use*` toggles | Under `LIL_MULTI`, `Includes/lil_common.hlsl:27-46` bakes about nineteen `_Use*` toggles to literal `true`. They are compile-time family facts, not material state; the regular family gates on them as captured values. `[SOURCE]` |
| `LIL_IGNORE_SHADERSETTING` | `Includes/lil_replace_keywords.hlsl:6` defines it; `Includes/lil_common.hlsl:19-21` consumes it; the per-material feature scan skips Multi (`Editor/lilToonSetting.cs:1019-1021`). Compiled feature state comes from keywords and hard defines, never from the rewritten `*LIL_SHADER_SETTING*` block. `[SOURCE]` |
| Per-project token substitution | Multi assets regenerate per project and per build; one new varying region, the `*LIL_SHADER_SETTING_MULTI*` block, has a closed domain of `LIL_OPTIMIZE_*` and integration defines (`Editor/lilToonSetting.cs:550-566`). `[SOURCE]` |
| Container edges | `_UseOutline` on the base container has exactly one reader: the one-shot 1.2.7 migration that swaps between `ltsmulti` and `ltsmulti_o` by the flag (`Editor/lilStartup.cs:185-193`). `_UseClippingCanceller` feeds `LIL_MULTI_SHOULD_CLIPPING` at render time (`Includes/lil_common.hlsl:46`). `_AsOverlay` drives editor-time pass enables (`Editor/lilMaterialUtils.cs:400-404`). Queue and RenderType defaults differ per container (`Editor/lilMaterialUtils.cs:184-266`). The regular transparent `FORWARD_BACK` pre-pass and `_PreCutoff` have no Multi counterpart. `[SOURCE]` |

Reading: the theorem *content* is shared; the theorem *premises'* evidence
model is different. That is the exact shape of "same core, different front
door", and the wrong shape to duplicate the math for.

### 4.3 Where the mode-consistency gate lives, and what a wrong gate costs

The mode-consistency gate is the obligation that `_TransparentMode`, the
keyword set, the `_AsOverlay` pass-enables, and the blend/queue state agree
before any proof runs. It exists in both architectures; the question is
where.

**Unified:** the gate is one function in the Multi attachment layer. Its
inputs are Multi evidence; its output is an effective regular identity or a
named refusal. The core never runs on unresolved Multi evidence. A wrong
gate produces one defect class — a wrong premise fed to sound theorems —
localized to one surface with one input list. Fix it once; every container
and mode benefits.

**Separate:** the gate must exist inside every Multi semantic type, because
each type independently decides what mode it is looking at. The gate logic
spreads across per-container, per-mode types. This is the repository's
already-measured failure mode: the 2026-09-29 audit names family-internal
duplication as live risk R8, with six byte-identical copies of one helper
across two frontends as measured on 2026-09-29. A wrong gate in one copy has
the same severity
as in the unified case, but the probability multiplies with the copy count,
and the copies drift silently.

### 4.4 The shader-specificity rule under each architecture

The rule: shader-specific behavior stays with its frontend unless materially
different consumers justify a shared abstraction. Two families (Poiyomi,
lilToon) press on the shared seams (`FirstFailedZeroGate` copies, the render
state reader duplicated across the two frontends, per-family request
tables). The comparison doctrine says: extract across families only when a
third family confirms byte-identity.

Multi does not add cross-family pressure. It is a second producer *inside*
the lilToon family, pressing on lilToon-internal seams only. Under the
separate architecture, Multi would manufacture a third and fourth copy of
gate logic, evidence requests, and canonicalization code inside lilToon,
worsening R8 while contributing nothing to the cross-family extraction
question — the vendor itself shares the math in one include file. Under the
unified architecture, lilToon-internal reuse is justified by the strongest
form the rule recognizes: the theorem inputs are literally the same bytes
(same include file, same compiled branch at the same effective
`LIL_RENDER`). The unified answer therefore does not bend the rule; the
separate answer violates its spirit inside the family. `[INFERENCE]`

### 4.5 Growth: what multiplies, what refuses cleanly

**Unified.** A new vendor version adds include-tree rows in the existing
pattern and, if the remap table changes, one remap-table pin in the
attachment layer. A new container adds one resolution row, one enumerated
container-delta record, and one attestation profile per container (the
profiles are already per-container because the variant containers prune
their input sets). Unknown modes, unknown keywords, unknown containers, and
unattested versions all fail closed inside the attachment layer.

**Separate.** Every new version re-derives or copies the alpha semantics per
subtree; every new container duplicates gate arrays and request tables; the
refusal vocabulary bifurcates. The subtree count grows linearly with the
container count, and each new subtree re-answers questions the core already
answered. `[INFERENCE]`

The observed corpus sharpens this: the first Multi consumer needs exactly
the opaque theorems that already exist and are already attested
(`[MEASURED 2026-09-30]`). A separate subtree would re-attest and re-prove
what the unified path reuses for free, for a population that is entirely
mode 0.

### 4.6 Recommendation

`[DECISION]` Unified. One lilToon semantic core; the Multi containers attach
to it through a resolution layer. Not staged: there is no separate-first
milestone that pays for itself, because the first Multi slice needs only
reuse plus one resolver plus one attestation shape.

Evidence that would reverse this recommendation:

1. A Multi container whose compiled alpha behavior diverges from its mapped
   regular identity in a way the resolution layer cannot gate — for example
   the vendor changing the remap include to derive `LIL_RENDER` from
   something other than material keywords.
2. A real consumer that needs Multi facts the core cannot express, forcing
   core API growth that serves only Multi.
3. The vendor unifying the families in the opposite direction (making the
   regular assets keyword-driven too), dissolving the boundary this design
   rests on.

### 4.7 Falsifier-style cases the wrong choice (separate) would fail

- F-Q1-1: identical evidence (same textures, `_Color`, `_Cutoff`, queue) on
  a mode-0 Multi material and on an attested `lilToon` material must yield
  the same proven-opaque triangle set. Any drift between two semantic stacks
  shows as a classification difference on identical evidence.
- F-Q1-2: a vendor edit to `lil_pass_forward_normal.hlsl` must invalidate
  both families' proofs through one attestation surface (the include-tree
  digest). A separate subtree that re-pins or re-derives enforcement leaves
  one family provable against bytes the other family never checked.
- F-Q1-3: a mode-0 Multi material carrying a stale alpha-mask keyword
  (inconsistent mode/keyword state) must refuse. A copied gate that checks
  captured scalars instead of keyword evidence admits the inconsistent
  material, because the Multi family does not carry that scalar state.

## 5. Q2: which shader leads, and which follows

### 5.1 The terms, operational

- **Lead (core):** owns the theorems, the value-algebra premises, and the
  attestation identity model. Evidence and proofs are stated against it.
- **Follow (addition):** a resolution layer that turns its own evidence —
  shader name, `_TransparentMode`, keyword set, animation-reachable
  keywords — into an identity the core already understands, then reuses
  everything downstream unchanged. Its own facts (container deltas, queue
  defaults, keyword derivation) stay inside the layer.

### 5.2 Where the theorems live as of 2026-09-30

Everything lives on the regular side, shipped and attested:

- `LilToonMaterialSemantics`: the opaque constant-one theorem, stated
  against the attested base identity and `LIL_RENDER 0`, which compiles the
  alpha path out (its gate comments cite the forced-alpha block and the
  compile-time exclusion).
- `LilToonCutoutMaterialSemantics`: the cutout coverage-transform theorem,
  stated against `Hidden/lilToonCutout`, `LIL_RENDER 1`, with every optional
  coverage feature proven off and a `MaxProvableCutoff` bound.
- `LilToonTransparentMaterialSemantics`: the transparent theorem, stated
  against `Hidden/lilToonTransparent`, `LIL_RENDER 2`, with the
  transparent-only post-clip writers proven identities at unit alpha.
- `LilToonTransparentSourceEligibility` and the cutout counterpart: gate
  tables whose rows cite the per-mode compiled-out blocks.
- `LilToonSourceAttestation`: nine profiles, each carrying a `RenderMode`
  field read by a scan that requires exactly one
  `#define LIL_RENDER <int>` in the pass asset.

Zero Multi premises exist in production. `[SOURCE]`

### 5.3 Both shapes argued

**Regular leads, Multi follows.** The Multi layer resolves container plus
keyword evidence into an effective regular identity and refuses where it
cannot: modes 3-6, missing or inconsistent keyword evidence,
`_UseClippingCanceller` enabled, and containers with no regular counterpart
(refraction, fur, gem). The theorems, the eligibility evaluators, the
attestation discipline, and the evidence requests are reused unchanged. The
new code is one resolver, one Multi profile shape, and one refusal
vocabulary.

**Multi leads, regular follows.** The core restates its theorems against the
keyword-parametrized superset: identity becomes `(container, keyword set)`,
with regular assets the degenerate case of baked modes. What it costs:

1. Every theorem premise is written against bytes and pass assets — the
   strongest evidence model the repository has. Restating three theorem
   statements, two eligibility evaluators' gate tables (about sixty
   comment-cited rows), nine profile mode fields, the render-mode scan
   contract, and the captured-evidence scalar tables against keyword state
   *weakens* the regular claims to serve a corpus that is 100 percent mode
   0. `[INFERENCE]`
2. The measured attestation pins for the regular family would need a new
   profile model and re-measurement. Pure churn, zero coverage gain.
3. The mode-consistency gate becomes a core concern for every material,
   including regular materials whose keyword set is definitionally
   irrelevant, instead of a boundary of one layer.

The asymmetry decides it: moving theorems costs all of the above; keeping
them costs nothing.

### 5.4 Decision, by the five questions asked

1. **Theorem location.** All theorems and their premises live on the
   regular side as of 2026-09-30 (section 5.2); nothing lives on the Multi
   side. The
   rewrite cost is the full premise surface named in 5.3; the keep cost is
   zero. `[SOURCE]`
2. **Gate boundary.** Regular-leads puts the entire mode-consistency gate in
   one layer. Multi-leads spreads it into the core. `[INFERENCE]`
3. **Horizon at the pin.** Upstream signals:
   - The regular family is the complete family: 47 of the 52 manager fields
     are regular-family identities against 5 Multi fields
     (`Editor/lilShaderManager.cs:8-64`); every outline, tessellation, and
     lite variant is regular-only. `[SOURCE]`
   - The vendor's own Multi migration moves materials *between* Multi
     containers, not from regular to Multi
     (`Editor/lilStartup.cs:185-193`). `[SOURCE]`
   - The remap include's removed-keywords section retires the
     `LIL_MULTI_OUTLINE` keyword mapping, and outline state moved to
     container identity — Multi behavior migrated *toward* asset-carried
     identity, the regular model, not toward deeper keyword
     parametrization. `[SOURCE]` for the removed line; `[INFERENCE]` for
     the direction.
   - The `_TransparentMode` enum is 0..6 at the pin; a new mode extends a
     table in the attachment layer under regular-leads, and extends the
     core identity model under Multi-leads. `[SOURCE]` for the enum;
     `[INFERENCE]` for the cost placement.
   Does the regular family survive a 2.4? Nothing at the pin suggests it
   does not: it is the default path of the mode map, the container
   templates, and the generator. Does the Multi mode matrix grow? Plausibly,
   and regular-leads absorbs that growth as table rows. Marked
   `[INFERENCE]` throughout: vendor direction is read from code state, not
   from statements.
4. **Where visual equivalence breaks, and who gates it.** Every enumerated
   delta is gated inside the follower; none leaks into the core:
   - keyword-carried mode: the mode-consistency gate.
   - baked `_Use*` toggles: the follower supplies the family facts as
     proven constants where a core request names a toggle that is not
     material state in Multi, and refuses otherwise.
   - absent `FORWARD_BACK`/`_PreCutoff` and absent one-pass identity: the
     follower only ever produces the Normal-class transparent mapping, so
     the two-pass and one-pass questions never arise; nothing to refuse.
   - `_UseOutline` on the base container: gated by the follower (inert
     residue per the outline note's section 6).
   - `_UseClippingCanceller`: gated by the follower (render-time consumer).
   - queue/RenderType edges: container defaults live in the follower's
     profile rows; the core eligibility evaluators keep receiving
     effective values.
   - the three animation-derived keywords the vendor force-enables: they
     remap to color features only (`GEOM_TYPE_LEAF` to rim-light direction,
     `EFFECT_HUE_VARIATION` to tone correction), alpha-irrelevant, and the
     follower's keyword partition marks them inert; on the NDMF play-mode
     path the vendor preprocess returns early, so they do not even appear.
     `[SOURCE]` for the keyword remaps; `[INFERENCE]` for alpha-irrelevance,
     consistent with F0 section 7.13.
5. **Refusal locality.** Regular-leads refuses every Multi-only unsupported
   case inside the addition with Multi-named refusal values, per the closed
   refusal enum policy. The core refusal vocabulary never widens.
   Multi-leads widens core refusals or fabricates core identities that
   cannot be proven. `[INFERENCE]`

### 5.5 Recommendation

`[DECISION]` Regular leads; Multi follows. The core of understanding stays
stated against the regular identities where B2, T1, and the shipped
semantics already state their premises. Multi becomes a projection.

Evidence that would reverse this recommendation:

1. The vendor derives Multi mode from something not captured as material
   or keyword state at NDMF time, making the projection under-determined.
2. The vendor deletes the regular family or the pass assets, removing the
   lead's attestation surface.
3. A Multi container gains a behavior with no regular counterpart that a
   real consumer needs proven — forcing theorem work that has no home in
   the lead.

### 5.6 The follower layer's exact interface

Inputs — immutable capture, per material:

1. Exact shader name and container GUID (container proof).
2. `_TransparentMode` captured scalar, with animation reachability resolved
   by the existing per-slot state machinery.
3. Keyword set, partitioned by the pinned remap table:
   - mode keywords: `UNITY_UI_ALPHACLIP`, `UNITY_UI_CLIP_RECT`;
   - alpha-relevant feature keywords: `_COLOROVERLAY_ON` (alpha mask),
     `GEOM_TYPE_BRANCH_DETAIL` (dissolve), `ETC1_EXTERNAL_ALPHA` (dither),
     `_FADING_ON` (distance fade, alpha-writing at `LIL_RENDER 2`);
   - alpha-irrelevant feature keywords (shadow, rim, backlight, emission,
     normal, anisotropy, reflection, matcap, glitter, parallax, audiolink,
     `_DETAIL_MULX2`);
   - animation-derived color keywords (`GEOM_TYPE_LEAF`,
     `EFFECT_HUE_VARIATION`), tolerated present or absent.
4. `_AsOverlay` pass-enable state (the ShadowCaster enable the editor
   writes).
5. `_UseClippingCanceller` and `_UseOutline` scalars.
6. Container profile facts: queue and RenderType defaults, pruned input
   set, from the Multi attestation profile.

Outputs:

1. `Resolved`: an effective regular identity
   (`LilToon` / `Hidden/lilToonCutout` / `Hidden/lilToonTransparent`
   class), plus a container-delta record carrying the baked-feature facts
   and the container queue/RenderType row that downstream eligibility
   consumes. The core semantic types, requests, and theorems run unchanged
   from here.
2. `Refused`: a closed Multi resolution refusal enum, one value per named
   case, for example: mode outside the admitted set, mode refused on this
   container, keyword/mode mismatch, alpha-relevant feature keyword set
   where the mapped theorem refuses it, clipping canceller enabled,
   unsupported container, attestation failed, missing keyword evidence.
   Each value carries its own report key.

Refused at this layer, never surfaced as a core-family failure.

### 5.7 Core artifacts that must not change when Multi lands

1. The three semantic types' theorem statements and premises, including
   `MaxProvableCutoff` and the compile-time-exclusion comments.
2. The nine existing attestation profiles, their digests, the
   `AdmittedPackageVersions` rows, and the `TryScanRenderMode` contract for
   regular pass assets.
3. The `MaterialSemantics` value algebra.
4. The three families' `MaterialEvidenceRequest` shapes. If Multi needs a
   Multi-only scalar, it rides the container-delta record, never a widened
   core request.
5. `ResolveCanonicalTargetShaderName` and the regular conversion recipes.

### 5.8 Falsifier-style cases the wrong choice (Multi leads) would fail

- F-Q2-1: landing Multi support must not modify any artifact in section
  5.7. The wrong choice fails at the first profile field or gate row it
  must rewrite.
- F-Q2-2: a Multi-only unsupported case (mode 4 fur) must refuse with a
  Multi-named value inside the addition. The wrong choice surfaces it as a
  widened core refusal or a core identity that exists only to fail.
- F-Q2-3: the mode-consistency gate must appear nowhere in the regular
  eligibility evaluators. The wrong choice adds keyword gating to materials
  whose keyword set is definitionally empty — observable as new gate rows
  in the regular evaluators.
- F-Q2-4: every regular-family attestation pin keeps reproducing across
  the two generator shapes after Multi lands. The wrong choice re-measures
  pins for zero coverage gain.

## 6. Q3: version anchoring

### 6.1 The question

Does the Multi path anchor at 2.3.4 with 2.3.0 through 2.3.3 following via
include-tree rows, or land on 2.3.4 only?

### 6.2 Facts in play

- The existing attestation anchors 2.3.4 and admits 2.3.0-2.3.3 through
  per-version include-tree digest rows, with 2.3.1-2.3.3 sharing one row.
  That measurement covered the regular shader and pass assets; it did not
  measure Multi assets except the 2.3.3-2.3.4 pair. `[SOURCE]`
- The Multi shader, meta, and editor files are byte-identical between 2.3.3
  and 2.3.4; the only include difference is one additional-light-mode
  constant line. `[SOURCE]`, from the merged outline note section 8.
- Committed `.shader` bytes at the vendor tag are stale relative to the
  tag's own generator; pins must be measured from installed shapes.
  `[SOURCE]`, from the merged open-questions note section 3.
- The `*LIL_SHADER_SETTING_MULTI*` block adds a new closed vocabulary
  (`LIL_OPTIMIZE_*` plus integration defines) to the canonicalization
  domain. `[SOURCE]`, verified in `Editor/lilToonSetting.cs:550-566` this
  session.
- The Multi behavior contract is the remap include: its mode derivation and
  feature table are what the follower's keyword partition pins. Any change
  to that file is a behavior change regardless of asset bytes. `[SOURCE]`
  for the file's role; `[INFERENCE]` for treating it as the contract.

### 6.3 Recommendation

`[DECISION]` Anchor at 2.3.4 at landing. Backfill earlier versions through
measured rows, following the existing pattern, with two additions:

1. Each backfilled version row requires an installed-shape measurement of
   that version's Multi facts: the five container canonical digests under
   the extended canonicalizer, and a digest of the remap include. The
   shared include-tree row is reused, but the regular rows' byte-identity
   evidence is not inherited for Multi assets. Never admit an unmeasured
   row.
2. 2.3.3 is the cheapest first backfill: the merged evidence already
   establishes byte-identity of the Multi assets and editor files with
   2.3.4, so its measurement run is a confirmation. 2.3.0 through 2.3.2
   follow when measured.

Materials on unmeasured versions keep the existing per-build consent path
for recognized shader sources until their row lands. The Multi keyword
derivation makes a wrong-version admit strictly more dangerous than in the
regular family — the mode evidence is keyword state — so the consent gate
is the right interim boundary. `[INFERENCE]`

Per-version follower cost: one measurement run per version (container
digests plus remap-include digest), one row per version in the Multi
profile table, zero code changes when the remap table is unchanged, and a
follower-layer re-derivation if it is not. The version gate refuses
cleanly: an unadmitted version fails attestation before any resolution
runs.

### 6.4 Falsifier-style cases the wrong choices would fail

- F-Q3-1: an install on an unmeasured version whose Multi container bytes
  differ from every pinned row must refuse at attestation. Admitting it
  through the regular family's include-tree row alone fails this case.
- F-Q3-2: a settings-shape change that alters only the
  `*LIL_SHADER_SETTING_MULTI*` block must produce the same canonical
  digest (the shape-agreement property, extended to the Multi block).
- F-Q3-3: a vendor edit to the remap include in a backfilled version must
  refuse that version's row until re-measured, even when every container
  digest is unchanged.

### 6.5 Pre-measurement list

Before any Multi digest pin lands, the measurement task must:

1. Measure from installed shapes, never from committed tag bytes.
2. Extend the canonicalizer with the `*LIL_SHADER_SETTING_MULTI*` region
   over the closed `LIL_OPTIMIZE_*` and integration define domain, keeping
   everything else retained; verify shape agreement across at least two
   real settings shapes, as the 2026-09-07 spec did for the regular block.
3. Pin per container, not per family: five containers with pruned input
   sets are five digests.
4. Replace the `LIL_RENDER` scan for Multi profiles with the pinned
   keyword-derivation contract (container plus keyword-to-mode table),
   since Multi assets declare no `LIL_RENDER`.
5. Reproduce the existing regular pins in the same run, so the extended
   canonicalizer is proven not to have disturbed them.

## 7. Findings recorded this session

1. The remap include's removed-keywords section retires the
   `LIL_MULTI_OUTLINE` keyword mapping (`lil_replace_keywords.hlsl`,
   removed-mappings comment block). Read first-hand on 2026-09-30 at the
   pin. Recorded as a vendor-direction signal in section 5.4. No repo
   change needed. `[SOURCE]`
2. The committed base container asset at the pin carries an expanded
   `LIL_OPTIMIZE_*` block in its shader-scope HLSL include block —
   consistent with the settled fact that committed bytes are stale
   generator output, and confirming the Multi block is a real generated
   region in the installed shape. `[SOURCE]`

## 8. Next steps

1. First real consumer: the mode-0 slice on both populated containers —
   `_lil/lilToonMulti` and `Hidden/lilToonMultiOutline` — the 136 observed
   materials. This matches the outline note's section 9 option (b) and this
   record's unified, regular-leads architecture. `[MEASURED 2026-09-30]`
   `[DECISION]`
2. Next artifact: a design spec for the follower layer — the resolver, the
   Multi attestation profile shape, the refusal vocabulary, and the queue/
   RenderType canonical row — stating every gate from sections 5.6 and 6.5.
   Per session boundary, this record does not author it.
3. Then: the digest measurement task per section 6.5, then RED/GREEN tasks.
   The falsifier cases in sections 4.7, 5.8, and 6.4 are candidates for the
   spec's falsifier set.

## 9. Status

Dated 2026-09-30. All three recommendations are `[DECISION]` records of
this session, grounded in the pinned source read first-hand on 2026-09-30
and the merged notes of the same date. Q3's backfill sequencing is
pre-measurement by design: no measurement ran in this session, and none
was authorized. Nothing in this record changes production behavior.
