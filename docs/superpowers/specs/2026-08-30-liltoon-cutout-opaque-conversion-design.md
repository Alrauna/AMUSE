# lilToon Regular Cutout → Canonical Opaque Conversion — Design

| | |
|---|---|
| Branch | `feature/liltoon-cutout-opaque-conversion` |
| Created from | `main`, verified equal to `origin/main` (0 ahead, 0 behind) |
| Base SHA | `a0b46e716e811ab5010dc33c6f805b55463b7e53` |
| Working tree at branch creation | two user-owned Unity toolchain-churn modifications (`Packages/manifest.json`, `Packages/packages-lock.json`, `com.unity.toolchain.macos-arm64-linux-x86_64` add, inspected additive-only); untouched by this work |
| Unity / NDMF | 2022.3.22f1 / NDMF (pinned, embedded); lilToon 2.3.4 is **not installed** in this project |
| Census Lab | not used, not inspected, not modified |
| Method note | no C# LSP server is available in the planning session; all cross-referencing was done by exhaustive repository search. Every current-source citation below was read from the checked-out tree at the base SHA |

Claims are tagged **[SOURCE]** (read from the checked-out repository at the base SHA),
**[B1]/[B2]/[F0]** (measured or source-pinned fact from the merged investigations, cited
by section), **[INFERENCE]**, or **[DECISION]** (a choice this design makes that the
controller may overturn).

---

## 1. Problem statement and supported contract

AMUSE's alpha separation converts triangles *proven visually opaque* on an alpha-mode
material to an appended submesh rendered with a canonical opaque material. The merged
vertical slice supports exactly one source family: Poiyomi Toon 9.3.64. Every lilToon
material whose shader is not exactly `lilToon` is unselectable today, fails the
renderer's material-dependency closure, and refuses the renderer
(`UnityMaterialSemantics.cs:114-129,242-263`).

B1 (`2026-08-30-liltoon-opaque-characterization.md`) measured the cutout
source/target identities, digests, clone/shader-assignment behavior, queue and
`RenderType` transitions, and the 18-property canonical recipe. B2
(`2026-08-30-liltoon-cutout-alpha-semantics.md`) derived the restricted cutout alpha
theorem, the complete gate set, the extended evidence request, and the
callback-independence verdict. F0 (`2026-08-30-liltoon-family-applicability.md`)
classified all 52 material-entry identities; exactly one is P (primary support):
regular no-outline cutout. The controlling investigation
(`2026-08-30-liltoon-opaque-conversion.md`) recorded R1–R6, the honest integration
shape, and the falsifiers.

This design is the production successor: the smallest complete implementation of

```
Hidden/lilToonCutout  →  prove triangles opaque (B2 theorem)
                      →  append on AMUSE-generated canonical `lilToon` opaque material
                      →  existing mesh-separation pipeline
```

**Supported contract.** For a triangle of submesh *S* rendered with material *M* under
the attested regular no-outline lilToon 2.3.4 cutout source, the conversion is applied
iff B2 §5's restricted theorem proves *T* `ProvenOpaque` and the conversion eligibility
of §9 holds. Proven triangles render on an AMUSE-generated clone carrying the canonical
opaque recipe; unproven triangles remain on the source cutout material, unchanged.
Fully discarded cutout domains are **not removed** — they stay on the source material
and are never reinterpreted as `ProvenOpaque` (fixed controller decision 5).

## 2. Exact positive profile (allowlist)

| Axis | Supported value |
|---|---|
| Package | `jp.lilxyzw.liltoon` `2.3.4` (format stamp `_lilToonVersion == 45`) |
| Source shader | `Hidden/lilToonCutout`, GUID `85d6126cae43b6847aff4b13f4adb8ec` [B1 §5] |
| Source pass | `Hidden/ltspass_cutout`, GUID `ad219df2a46e841488aee6a013e84e36`, `#define LIL_RENDER 1` [B1 §5] |
| Source digests | material `c83d73a2…178`, pass `ecd1caed…e92`, include tree `6e2dce6c…fd46` [B1 §6] |
| Target shader | `lilToon`, GUID `df12117ecd77c31469c224178886498e` (the already-attested opaque identity) |
| Outline | none (no-outline source; outline families are different shader identities) |
| Pipeline | regular, non-Lite, non-Tessellation, non-Multi |
| Cutoff eligibility | `_Cutoff <= 0.9999` (non-finite refuses) |
| UV support | identity `_MainTex_ST == (1,1,0,0)` on channel 0 only |

Everything else refuses. Positive support is allowlist-based: a second pinned identity
added to the lilToon frontend, not a widened recognition rule.

## 3. Non-goals

- No `IOpaqueConversion`, adapter/plugin interface, registry, dynamic discovery,
  render-state IR, `SurfaceMode` hierarchy, generic shader-transformation framework,
  or public third-party extension API. Two families parameterize the demonstrated
  facts; nothing more [controlling §12–§13].
- No support, attestation, or recipe for: any outline family, transparent
  Normal/OnePass/TwoPass, Lite, Tessellation, Multi, overlays, Gem, Fur (all
  variants), Refraction/RefractionBlur, fake shadow, outline-only, or
  `.lilcontainer`-generated identities [F0 §7.18 dispositions].
- No affine `_MainTex_ST` support, no cutoff-margin extension (constant raw alpha
  below 1), no trilinear/anisotropic/mirrored sampling, no UDIM/IDMask tile-level
  proofs, no dither/alpha-mask/dissolve-active proofs [B2 §11 rows 5–8].
- No base-material semantics widening: `AnalyzeBaseMaterial` continues to route
  cutout materials through the opaque lilToon attestation, which refuses, leaving
  base color/emission/normal all-Unknown. This slice adds only the alpha-separation
  path.
- No upload-time (Outcome B) validation: the restricted proof is NDMF-complete
  [B2 §9; fixed controller decision 6].
- No texture, keyword, or pass-enable writes by the recipe [B1 §9; controlling §6].
- No polished per-family diagnostics beyond the existing deterministic refusal
  vocabulary; safe refusal may not be deferred, polish may.

## 4. Current-state reconstruction

Verified against the checked-out tree at the base SHA. The investigations' citations
reproduce with at most ±1 line drift; no contradiction was found. Load-bearing facts:

1. **Family selection is exact-name and duplicated.** `IdentifyFamily`
   (`UnityMaterialSemantics.cs:242-263`) matches `PoiyomiMaterialSemantics.PoiyomiToonShaderName`
   or `LilToonSourceAttestation.SupportedShaderName` (`"lilToon"`), else `Unsupported`.
   `CaptureAlphaMaterials` (`:95-139`) duplicates the same name→request mapping at
   `:114-129` (R2).
2. **Capture schema per family.** `CaptureRequestForFamily` (`:290-302`): Poiyomi =
   `Combine(PoiyomiMaterialSemantics.AlphaEvidenceRequest,
   PoiyomiOpaqueConversion.ConversionEvidenceRequest)` (`:285-288`); lilToon = its
   alpha request only. Closed capture attests every admitted material via
   `IsAttestedAlphaMaterial` (`:304-319`) → `LilToonSourceAttestation.TryVerifyLilToonIdentity`.
   Mixed-family renderers already combine per-family alpha relevance through
   `MaterialEvidenceRequest.Combine(alphaRequests)`
   (`UnityAnimationEvidenceCapture.cs:356-360`).
3. **Attestation pins one identity.** `LilToonSourceAttestation.cs:323-344` pins
   name/GUID, `Hidden/ltspass_opaque`/GUID, package 2.3.4, format 45, `OpaqueRenderMode 0`,
   and the three digests. `GatherSourceEvidence` (`:1238-1328`) resolves the fixed pass
   with `Shader.Find(PassShaderName)` (`:1283`); `TryVerifyLilToonIdentity`
   (`:1078-1202`) conjuncts name, GUID, format, package, pass GUID, canonicalization
   provenance (`:895-934`, expecting exactly two pass removed regions with the official
   setting record), digests (`:1204-1228`), and `RenderMode == OpaqueRenderMode`
   (`:1190-1198`). `TryScanRenderMode` (`:856-893`) requires exactly one integer
   `#define LIL_RENDER` (R3).
4. **Opaque-lilToon alpha interpretation is a constant-1 theorem.**
   `LilToonMaterialSemantics.AlphaEvidenceRequest` (`:482-496`) requests
   `_lilToonVersion`, `_Invisible`, `_UDIMDiscardCompile` only;
   `InterpretVerifiedAlpha`/`InterpretAlpha` (`:505-533`) return
   `ScalarSemanticValue.Constant(1f)` behind two coverage gates — valid only under the
   attested `LIL_RENDER 0` premise (R4).
5. **Preparation is Poiyomi-hard-coded around the family branch (R1).**
   `AlphaSeparationPreparation.Prepare` (`:65-331`) resolves conversion relevance
   against `PoiyomiOpaqueConversion.ConversionEvidenceRequest` (`:93-123`, request at
   `:104`); `ConvertAdmittedMaterial` (`:363-479`) refuses
   `captured.Family != CapturedAlphaMaterialFamily.Poiyomi` (`:378-382`), admits
   derived evidence against the Poiyomi request (`:390-398`, request at `:393`),
   checks the runtime-overwrite rule against `PoiyomiOpaqueConversion.CanonicalOpaqueProperties`
   (`:400-421`), reads effective render state (`:441-442`), attests and evaluates
   eligibility (`:446-458`), and maps `AlreadyOpaque → live` / `Convertible → clone`
   (`:459-475`). `RegisterPreparedOpaque` (`:490-503`) deduplicates avatar-wide.
6. **The conversion seam is Poiyomi-typed (R6).** `VerifiedOpaqueConversion`
   (`AlphaSeparationPreparation.cs:26-31`) returns `out PoiyomiOpaqueConversionRefusal`;
   flows through `AmusePlatformFinishPlugin.cs:223,320,693` and the test seams
   (`VerifiedPoiyomiTestSeams.cs:34-65`, `AlphaSeparationPreparationTests.cs:656,898`,
   `AlphaSeparationApplyTests.cs:263-278`).
7. **The Poiyomi clone validation assumes shader preservation (R5).**
   `PoiyomiOpaqueConversion.PrepareCanonicalOpaqueClone` (`PoiyomiOpaqueConversion.cs:531-561`)
   throws when `clone.shader != source.shader` (`:553-558`) — wrong for a conversion
   whose entire point is a shader swap.
8. **Everything downstream of `Material → Material` mapping is shader-agnostic.**
   Dispositions (`MeshSeparationPlanner.cs:195-199`: `Unchanged` / `WhollyOpaqueCandidate`
   / `Split`), prepared records (`AlphaSeparationRecords.cs:106-260`, including the
   `"<source.name> (AMUSE Opaque n)"` naming at `:180-181`), pass-3 validation /
   finalization / apply (`AlphaSeparationApply.cs:32,53,391,473,547`), curve rewriting,
   sweep of `CreatedClones` only, and avatar-wide dedup.
9. **The resolution layer already proves what B2 needs.** `AlphaSemanticsResolver`
   refuses `multiplier > 1` (`UnsupportedMultiplier`), maps `== 1` to sampled
   classification and `< 1` to uniform `MustRemainTransparent` (`:272-298`), admits
   only identity UV0 (`IsSupportedMapping`, `:338-345`), only Point/Bilinear ×
   Clamp/Repeat (`:353-387`), and classifies every mip with footprint and wrap
   arithmetic (`:178-204`). The exact-singleton admission machinery refuses
   non-singleton, non-finite, and disagreeing bindings
   (`AdmittedMaterialStates.cs:279-305,545-549`), including derived `_MainTex_ST`
   texture-scale-offset components (`:418-453`).
10. **Fixture and seam conventions.** `LilToonFixtureTestBase`
    (`Tests/Editor/Semantics/LilToon/LilToonFixtureTestBase.cs`) builds schema-complete
    stand-in materials under `Assets/AmuseTests_LilToon` — no vendor package;
    `VerifiedPoiyomiTestSeams` substitutes family/request/capture/conversion for
    stand-ins; `AlphaSeparationPersistenceTests` audits production sources for the
    `SaveAsset` token (`AuditedProductionFiles`, `:438-486`) and digests source mesh /
    material property state around real builds.

**Stop-condition check (§19):** all five load-bearing premises hold in current source;
none triggers a stop (§19).

## 5. End-to-end data flow (unchanged pipeline, second family)

```
NDMF PlatformFinish barrier (AmusePlatformFinishPass.Execute, production: all seams null)
  per renderer:
    closed capture (UnityAnimationEvidenceCapture.Capture)
      family selection per material        ← R2: one name→family mapping, +LilToonCutout
      alphaRelevance = Combine(family alpha requests)   ← cutout request joins the union
      captureSchema  = Combine(family capture requests) ← cutout = cutoutAlpha + conversion
      closed capture + attestation (IsAttestedAlphaMaterial → cutout identity verify)
    runtime-state resolution per slot (AdmittedMaterialStates.ResolveSlot)
      cutout alpha value → AlphaSemanticsResolver → per-triangle outcomes
    geometry capture + classification (UnityRendererAlphaAnalysis, MeshSeparationPlanner)
    preparation (AlphaSeparationPreparation.Prepare)
      conversion relevance against Combine(families' conversion requests present)  ← R1
      per slot, per admitted material (ConvertAdmittedMaterial):
        LilToon        → map to itself (attested opaque; no conversion facts)   [DECISION]
        LilToonCutout  → family admission → family overwrite rule → cutout
                         attestation → eligibility → canonical clone (or seam)
        Poiyomi        → unchanged Poiyomi path
        anything else  → OpaqueConversionUnsupportedFamily (slot-local)
      avatar-wide dedup (RegisterPreparedOpaque, unchanged)
later pass (AlphaSeparationApply.Execute)
  PrepareSurvivingSet (pass-3, family-agnostic) → FinalizeClone → ApplyFinalization
  sweep destroys CreatedClones only
```

No new pipeline stage, pass, record type, or planning concept. The mesh clone, appended
slot indexing, curve rewrite, validation, sweep, and dedup are reused as-is.

## 6. R1–R6 decisions

**R1 — per-family conversion facts.** [DECISION]
`AlphaSeparationPreparation` gains two private total functions over
`CapturedAlphaMaterialFamily`:

- `ConversionRequestForFamily(family)`: `Poiyomi → PoiyomiOpaqueConversion.ConversionEvidenceRequest`;
  `LilToonCutout → LilToonOpaqueConversion.ConversionEvidenceRequest`;
  `LilToon → null` (map-to-self needs no conversion evidence).
- `CanonicalPropertiesForFamily(family)`: the matching `CanonicalOpaqueProperties`;
  `LilToon → empty`.

The renderer-level conversion-relevance loop resolves each binding against
`MaterialEvidenceRequest.Combine` of the conversion requests of the
conversion-capable families actually present among the renderer's admitted materials
({Poiyomi, LilToonCutout} ∩ present). A renderer with no conversion-capable member
skips the loop entirely (its only mapping is identity; nothing can be overwritten).
Bindings are bucketed per family by re-resolution against that family's own request;
`ConvertAdmittedMaterial` admits and overwrite-checks each material against its own
family's bucket. `UnrecognizedMaterialBinding`, additive-layer, and unnormalized
blend-tree refusals remain renderer-wide, computed from the union — identical to
today when only Poiyomi is present (falsifier: Task 4 routing regression).

**R2 — single family selection.** [DECISION]
`UnityMaterialSemantics` gains one private `ClassifyShaderName(string)` returning
`(family, alphaRequest)`; both `IdentifyFamily` and `CaptureAlphaMaterials` consume it.
The map gains the exact name `Hidden/lilToonCutout` → new
`CapturedAlphaMaterialFamily.LilToonCutout` with
`LilToonCutoutMaterialSemantics.AlphaEvidenceRequest`.

**Family model.** [DECISION] A third enum member, not a variant field:
`CapturedAlphaMaterialFamily { Unsupported, Poiyomi, LilToon, LilToonCutout }`.
Routing (`AlphaRequestForFamily`, `CaptureRequestForFamily`, `IsAttestedAlphaMaterial`,
`AnalyzeAlphaMaterial`, `ConvertAdmittedMaterial`, per-family conversion facts) stays a
pure total function of the family; no hidden shader-name conditionals; the ordinary
opaque lilToon value and its requests are untouched (B2 falsifiers 10–11).

**R3 — profile-parameterized attestation.** [DECISION]
`LilToonSourceAttestation` gains a private readonly profile record
`LilToonSourceProfile { ShaderName, ShaderGuid, PassShaderName, PassShaderGuid,
RenderMode, ShaderCanonicalDigest, PassCanonicalDigest }` and two instances:
the existing `OpaqueProfile` (current pins, unchanged) and

| Pin | Value | Source |
|---|---|---|
| ShaderName | `Hidden/lilToonCutout` | [B1 §5] |
| ShaderGuid | `85d6126cae43b6847aff4b13f4adb8ec` | [B1 §5] |
| PassShaderName | `Hidden/ltspass_cutout` | [B1 §5] |
| PassShaderGuid | `ad219df2a46e841488aee6a013e84e36` | [B1 §5] |
| RenderMode | `1` | [B1 §5; B2 §3.1] |
| ShaderCanonicalDigest | `c83d73a26ab86e933f8cacb8c71307d8715fcc1693cdc08d209011bb0f836178` | [B1 §6] |
| PassCanonicalDigest | `ecd1caedc99c4569fb17898de16ce2025c21e2d191e06532098370a1291bfe92` | [B1 §6] |

Package name/version and format-version pins are shared (one lilToon 2.3.4 frontend);
`IncludeTreeDigest 6e2dce6c…fd46` is shared (one `Shader/Includes` tree) [B1 §6].
`GatherSourceEvidence` and the identity conjunction take the profile internally; the
public opaque entry points keep their exact signatures and behavior, and two new
entries appear: `GatherCutoutSourceEvidence(shader, evidence)` (resolves
`Shader.Find("Hidden/ltspass_cutout")`) and
`TryVerifyLilToonCutoutIdentity(evidence, out diagnostic)`. The canonicalization
provenance conjunction is unchanged — B2 §5 clause 1 attests that the cutout pass text
satisfies it; Task 1's tests pin this. Attestation mismatch fails closed with a
diagnostic; there is no name-only fallback. Measured-digest provenance is preserved:
cutout digests enter as B1-measured constants with the same never-re-derive remark the
opaque pins carry [B1 §4, §6].

**R4 — the constant-1 theorem never sees cutout.** [DECISION]
`AnalyzeAlphaMaterial`'s `LilToonCutout` arm calls the new
`LilToonCutoutMaterialSemantics.InterpretVerifiedCutoutAlpha`; the opaque
`LilToonMaterialSemantics.InterpretVerifiedAlpha` is unreachable for the cutout family
by construction. The alpha evidence request stays per family: opaque lilToon's request
is byte-for-byte unchanged and not widened [B2 §6, gap-8 observation recorded, no
action].

**R5 — family-specific clone validation.** [DECISION]
`LilToonOpaqueConversion.PrepareCanonicalOpaqueClone(Material source, Shader attestedTarget)`
clones, assigns `attestedTarget`, writes the recipe, and validates: 18 recipe scalars,
queue 2000, `RenderType=Opaque`, and `clone.shader == attestedTarget` (reference).
Production resolves the target itself, gathers the opaque target source profile, and
requires the complete `TryVerifyLilToonIdentity` conjunction: shader name/GUID, package
version, canonical shader/pass/include-tree digests, supported variant set, and
`LIL_RENDER 0`. An unresolvable or unattested target is an
`InvalidOperationException` (invariant failure — the attested environment regressed,
not an unsupported material). The Poiyomi shader-preservation check is intentionally
not portable [controlling §6]. The `(source, Shader)` core keeps the recipe CI-runnable
on stand-in shaders; the production wrapper adds the source attestation. Same
throw-on-disagreement, destroy-first policy as Poiyomi.

**R6 — honest two-family seam.** [DECISION]
Rename `VerifiedOpaqueConversion` → `VerifiedPoiyomiConversion` (same shape, typed
`PoiyomiOpaqueConversionRefusal`); add `VerifiedLilToonConversion` of the identical
shape typed `LilToonOpaqueConversionRefusal`. `Prepare`, the plugin overloads, and
`RetainPreparedSeparation` carry both (production passes both null). The test-side
seam gains a lilToon counterpart mirroring `VerifiedPoiyomiTestSeams`: family/request
selection for the cutout stand-in, capture without vendor attestation, and the real
conversion step minus the source-identity check (with the stand-in shader passed as
`attestedTarget`). All references were enumerated
(`AlphaSeparationPreparation.cs:26,72,370`; `AmusePlatformFinishPlugin.cs:223,320,693`;
`VerifiedPoiyomiTestSeams.cs:34`; `AlphaSeparationPreparationTests.cs:656,898`;
`AlphaSeparationApplyTests.cs:263-278`) — a finite, mechanical cutover.

## 7. Attestation profile (summary)

See §6 R3. Failure boundary: any mismatch of name, GUID, format stamp, package,
pass identity, provenance, digests, or render mode → cutout attestation refuses →
`IsAttestedAlphaMaterial` false → the closed capture refuses the renderer batch
(`MaterialDependencyClosureFailure`) — exactly today's treatment of unattestable
lilToon variants, now scoped to "everything but the two pinned identities". Ordinary
opaque attestation and semantics are unchanged and re-pinned by the existing
`LilToonAttestationTests`, which must stay green unmodified.

## 8. Cutout alpha theorem, evidence, and refusal lattice

### 8.1 Positive theorem (B2 §5, adopted verbatim)

A triangle is `ProvenOpaque` iff, under the attested cutout profile:

1. source identity/digests/`LIL_RENDER 1` attested (§7);
2. captured scalars, finite: `_Color.a == 1`; `_Cutoff <= 0.9999` (non-finite
   refuses); `_Invisible == 0`;
   `_ShiftBackfaceUV == 0`; `_UseParallax == 0`; `_UseMain2ndTex == 0`;
   `_UseMain3rdTex == 0`; `_AlphaMaskMode == 0`; `_DissolveParams.x == 0`;
   `_UseDither == 0`; `_UDIMDiscardCompile == 0`; `_UDIMDiscardMode == 0`;
   `_IDMask1..8 == 0`; `_IDMaskControlsDissolve == 0` (the adversarial-review gate:
   with it `1` the vertex IDMask path can force chain alpha to 0 even at dissolve
   mode 0 [B2 §3.3.8, §5 clause 2]);
3. UV domain identity: `_MainTex_ST == (1,1,0,0)` and
   `_MainTex_ScrollRotate == (0,0,0,0)`;
4. texture evidence: `_MainTex` `Texture2D`, admitted format, full mip residency,
   Point/Bilinear, `wrapU == wrapV ∈ {Clamp, Repeat}`, alpha captured by GPU readback
   for **every** mip;
5. per-triangle classification over the exact hull with footprint/wrap arithmetic
   finds every intersecting texel alpha `== 255` at every level;
6. animation closure: every requested property singleton-admitted against the
   material's own serialized default; any unrecognized proof-relevant binding refuses
   the renderer.

Then `a ≡ 1` in FORWARD, FORWARD_ADD, and SHADOW_CASTER; `T(1) = 1` for
`_Cutoff ≤ 0.9999`; full coverage under any `_AlphaToMask`/MSAA; the shadow
`clip(1 − c)` keeps. `_Cutoff` is a captured theorem scalar (B2 §5 clause 2): the
classification layer refuses above `0.9999` — no provable triangle, per B2 §10 —
and conversion eligibility re-checks the same bound as the mutation-authorizing
gate (§9.3; B2 gap 4).

### 8.2 Evidence request (B2 §6 with the load-bearing `_Cutoff` correction)

`LilToonCutoutMaterialSemantics.AlphaEvidenceRequest` — a new object; the opaque
request is untouched:

- scalars: `_lilToonVersion`, `_Invisible`, `_UDIMDiscardCompile`, `_UDIMDiscardMode`,
  `_ShiftBackfaceUV`, `_UseParallax`, `_UseMain2ndTex`, `_UseMain3rdTex`,
  `_AlphaMaskMode`, `_UseDither`, `_IDMask1` … `_IDMask8`, `_IDMaskControlsDissolve`,
  `_Cutoff`;
- colors: `_Color` (per-component `.a` bindings recognized and singleton-admitted);
- vectors: `_DissolveParams`, `_MainTex_ScrollRotate`;
- textures: `_MainTex` with `ScaleOffset | SourceIdentity | Sampling | AlphaChannel`
  (the same kind set Poiyomi's alpha request uses for `_MainTex`);
- shaderName: true.

`_MainTex_ST` is not a vector request — it rides the texture request's `ScaleOffset`
kind, which also derives the animatable `_MainTex_ST` binding name
(`UnityAnimationEvidenceCapture.cs:480-497`).

`_Cutoff` rides the alpha request even though conversion also reads it: B2 §5
clause 2 carries it as a captured theorem scalar and §8 names its non-singleton
admission as proof-refusing, the classification layer needs it to refuse
`> 0.9999` before any triangle is called proven, and both families' capture
schemas then carry it, so a union-admitted `_Cutoff` binding on a mixed renderer
admits against every admitted material instead of failing on absent evidence.

### 8.3 Interpretation and refusal lattice

`InterpretVerifiedCutoutAlpha(captured)` returns, in order: Unknown-with-diagnostic on
the first failed §8.1-2 gate or on non-identity `_MainTex_ScrollRotate`; otherwise
`ScalarSemanticValue.TextureSampleTimesConstant(_MainTex alpha sample at identity UV0,
_Color.a)`. Everything below the interpretation is the existing resolver:

| Condition | Verdict | Mechanism |
|---|---|---|
| Any §8.1-2 gate nonzero / scroll-rotate nonzero | Unknown (slot refuses; never partially proven) | interpretation gate [B2 §10] |
| `_Cutoff > 0.9999` or non-finite | Unknown → slot refuses (`AdmittedMaterialSemanticsUnknown`) — no provable triangle, B2 §10 | interpretation gate |
| `_Color.a == 1` | texture classification | resolver `== 1` arm |
| `_Color.a ∈ (0,1)` | uniform `MustRemainTransparent` | multiplier lemma (`:289-298`) [B2 §10] |
| `_Color.a > 1` | `UnsupportedMultiplier` refusal | resolver (`:278-282`) |
| `_Color.a` non-finite (static or animated) | Unknown with a diagnostic — never the resolver's uniform-transparent fallthrough | interpretation finite check (mirrors Poiyomi `:700-708`) |
| Non-identity `_MainTex_ST`, channel ≠ 0 | `UnsupportedUvMapping` refusal | `IsSupportedMapping` |
| Trilinear/aniso, mismatched/unsupported wrap | `UnsupportedSampling` refusal | `TryMapSampling` |
| Format/streaming/readback failure | `MissingTextureEvidence` refusal, never inferred | provider contract |
| Transparent texel in any mip footprint | `MustRemainTransparent` | level merge (`:184-197`) |
| Degenerate triangle / NaN UV | `Unknown` (false negative by design) | classifier |
| Region-complexity overflow | `Unknown` (`MaxSupportRegions`) | exact-UV machinery |
| Non-singleton animation on any requested property | slot proof refused | exact-singleton admission |

Programming and invariant failures stay failures: no gate, refusal, or unknown is
produced by catching an exception.

## 9. Conversion recipe and eligibility

### 9.1 Canonical recipe (B1 §9; controlling §6)

Target: the attested opaque `lilToon` asset. Explicit queue `2000`;
`RenderType` override tag `Opaque` (both written and validated — deterministic
canonical state even though B1 measured the shader swap already producing the
effective values [B1 §8, §10]). Eighteen scalar writes, all `[MEASURED]` in B1 §9 with
exact read-back:

```
_SrcBlend=1  _DstBlend=0  _AlphaToMask=0  _ZWrite=1  _ZTest=4
_OffsetFactor=0  _OffsetUnits=0  _ColorMask=15
_SrcBlendAlpha=1  _DstBlendAlpha=10  _BlendOp=0  _BlendOpAlpha=0
_SrcBlendFA=1  _DstBlendFA=1  _SrcBlendAlphaFA=0  _DstBlendAlphaFA=1
_BlendOpFA=4  _BlendOpAlphaFA=4
```

No texture, keyword, or pass-enable writes. No `_Outline*` properties (no-outline
slice). `_Cutoff`, `_Color`, alpha-mask/dither/dissolve properties are **not written**:
`LIL_RENDER 0` excludes the alpha path on the target at compile time
[`SOURCE` `LilToonMaterialSemantics.cs:472-480`]. `_Cutoff` still enters conversion
relevance as an eligibility-only scalar (§9.2); the other omitted properties constrain
the alpha proof and are not conversion-relevant.

### 9.2 Conversion evidence request

`LilToonOpaqueConversion.ConversionEvidenceRequest` — scalar schema: the 18 recipe
properties plus `_Cutoff` (eligibility-read, never written). No colors, vectors, or
textures. Conversion-relevance animation is therefore exactly: recipe properties
(overwrite rule) and `_Cutoff`; a non-singleton `_Cutoff` refuses even earlier, at
alpha admission (`AnimatedMaterialPropertyNotSingleton`), because §8.2 now requests
it there too — the conversion-stage refusal remains as defense in depth. The cutout
capture schema is `Combine(cutoutAlphaRequest, conversionRequest)`, which also
carries `_lilToonVersion` for source-evidence gathering.

### 9.3 Eligibility gates (all evaluated on admitted derived evidence + live render state)

Order is load-bearing, mirroring Poiyomi: schema → finiteness → mutation-authorizing
gates. There is **no** `AlreadyOpaque` outcome — an attested cutout source is never
canonical-opaque; the attested opaque lilToon family maps to itself before conversion
is ever consulted (§10). `LilToonOpaqueConversionOutcome { Refused, Convertible }`.

| # | Gate | Refusal member |
|---|---|---|
| 1 | all 19 conversion scalars present | `ConversionPropertyAbsent` |
| 2 | all 19 finite | `ConversionPropertyNotFinite` |
| 3 | effective render queue is the canonical cutout default `2450` | `UnsupportedRenderQueue` |
| 4 | effective `RenderType == "TransparentCutout"` | `UnsupportedRenderType` |
| 5 | `_ZTest == 4` | `UnsupportedDepthComparison` |
| 6 | `_ZWrite == 1` | `UnsupportedDepthWrite` |
| 7 | `_ColorMask == 15` | `UnsupportedColorMask` |
| 8 | `_OffsetFactor == 0 && _OffsetUnits == 0` | `UnsupportedDepthOffset` |
| 9 | `_BlendOp == 0`, `_SrcBlend ∈ {1,5}`, `_DstBlend ∈ {0,10}` | `UnsupportedBlendEquation` |
| 10 | `_BlendOpAlpha == 0`, `_SrcBlendAlpha ∈ {1,5}`, `_DstBlendAlpha ∈ {0,10}` | `UnsupportedAlphaBlendEquation` |
| 11 | `_SrcBlendFA ∈ {1,5}`, `_DstBlendFA == 1`, `_BlendOpFA == 4`, `_BlendOpAlphaFA == 4` | `UnsupportedForwardAddBlendEquation` |
| 12 | `_Cutoff <= 0.9999f` (non-finite values already refused by gate 2) | `ClipThresholdDiscardsOpaqueAlpha` |

Rationale. [DECISION] Gates 9–10 are the alpha-1 degeneration predicate: at output
alpha exactly 1, unit source factors evaluate to 1 and zero destination factors to 0,
so the accepted source states are blend-equivalent to the canonical tuple the clone
writes; anything outside the classes (e.g. `DstColor` source, subtract op) would
change the moved triangles' contribution and refuses. Gates 3–4 deliberately admit
the normal cutout `2450`/`TransparentCutout` state so the intended `2450 → 2000`
normalization remains valid, but refuse custom queue or tag overrides whose ordering
or classification intent the alpha proof cannot preserve. Gates 5–8 and the FA ops
are exact-canonical: source states that observably change depth, channel, offset, or
FA-lighting behavior are refused rather than silently normalized — cheap false
negatives on exotic materials, never false positives. `_SrcBlendAlphaFA`/
`_DstBlendAlphaFA` are written but **not gated**: the attested cutout FORWARD_ADD
declares its alpha blend pair as the literal `Zero One` [B2 §3.1], so the properties
are unused by the compiled pass and normalization is inert. `_AlphaToMask` is written
but not gated: full coverage holds under any state at `a ≡ 1` [B2 §3.4, §7]; fresh
cutout default 1 is thereby admitted (the common case). The `0.9999` constant is the
controller-fixed twice-margin gate [B2 §3.4, §14.1; fixed decision 2]; Poiyomi's
`<= 1` rule is deliberately not reused.
Gate 12 deliberately duplicates the interpretation's cutoff gate (§8.3): the two
checks authorize different layers — classification ("no provable triangle") versus
mutation ("no authorized clone") — and B2 locates the bound in both places (§5
clause 2 theorem scalar; §11 gap 4 conversion gate). The eligibility copy is also
what the verified seam exercises on stand-in shaders.

Outline vacuity: there is no outline gate because outline state is carried by shader
asset identity — every outline family is a different shader name that family selection
never admits (§11). The same holds for Lite/Tess/Multi/Gem/Fur/Refraction/overlay/
fake-shadow/outline-only/container identities.

## 10. Mixed-family behavior

- **Sibling slots.** A Poiyomi slot and a lilToon-cutout slot on one renderer route,
  admit, overwrite-check, and convert independently through their own facts; a refused
  slot never corrupts a sibling (existing per-slot architecture; fixed decision 7).
- **Same-slot mixed admitted sets of supported families** (material-swap animation
  alternating Poiyomi and lilToon-cutout materials) map completely — each admitted
  value converts through its own family — realizing controlling §11's coverage
  expansion. [DECISION] Enabled: it falls out of per-family routing, and refusing it
  would require an extra gate with no soundness basis.
- **Attested ordinary opaque `lilToon` admitted values map to themselves**
  (`case LilToon: opaque = live`), mirroring Poiyomi's `AlreadyOpaque`; no clone, no
  conversion facts consulted. [DECISION] Required by controlling §14 falsifier 2 (a
  cutout slot whose swap set reaches a canonical lilToon material must map, not
  refuse). Consequence, stated for review: a renderer whose slots are *purely*
  attested opaque lilToon changes from slot refusal
  (`OpaqueConversionUnsupportedFamily`) to preparing with an identity mapping — the
  same outcome an attested opaque Poiyomi slot already produces today
  (`WhollyOpaqueCandidate`, no clone unless another slot splits, no visible change).
  Ordinary opaque-lilToon *analysis* — attestation, alpha request, constant-1 value,
  capture schema — is byte-for-byte unchanged (B2 falsifiers 10–11).
- **Any unsupported family value in an admitted set** (any non-`lilToon`/
  non-`Hidden/lilToonCutout` lilToon identity) is `Unsupported` at family selection →
  material-dependency closure refuses the renderer — unchanged, and it is the vacuity
  guard that outline/transparent/Lite/Tess/Multi/specialized identities can never
  reach conversion (they are refused one stage earlier, renderer-wide).
- **Union alpha admission stays sound on mixed renderers.** `_Cutoff` is in the
  cutout alpha request (§8.2), so a `material._Cutoff` binding on a renderer mixing
  Poiyomi and cutout materials resolves renderer-wide under the combined alpha
  relevance and singleton-admits against both families' captured evidence — both
  capture schemas carry `_Cutoff`. Without it, the cutout material's evidence would
  lack the property and admission would refuse the slot on absent evidence.

## 11. Failure boundaries

| Boundary | Scope | Existing/new |
|---|---|---|
| Unselectable family (all lilToon identities except the two pinned) | renderer-wide closure refusal | existing |
| Unrecognized conversion binding; additive layer; unnormalized blend tree (conversion-capable families present) | renderer-wide, candidate slots only | existing shape, union request |
| Cutout attestation mismatch | renderer-wide closure refusal | new profile, existing boundary |
| Alpha gate nonzero / scroll-rotate nonzero | slot alpha resolution refuses | new gate set, existing boundary |
| Non-singleton / non-finite proof-relevant or conversion-relevant animation | slot refuses (`AnimatedMaterialPropertyNotSingleton`, `UnsupportedAnimationCurveForm`, `ConversionStateNotAdmitted`) | existing machinery |
| Recipe property animated away from canonical | slot refuses (`ConversionPropertyOverwrittenAtRuntime`) | existing rule, per-family list |
| Eligibility gate failure | slot refuses (`OpaqueConversionRefused`) | existing member |
| Clone read-back disagreement | `InvalidOperationException`, clone destroyed, build-blocking internal failure | existing policy |
| Marker clip / unmapped runtime value / changed renderer | slot refuses | existing |

Refusal vocabulary is extended only by the new `LilToonOpaqueConversionRefusal`
members; `AlphaSeparationSlotRefusal` is unchanged (its `OpaqueConversionUnsupportedFamily`
doc comment is corrected — it no longer means "every lilToon material").

## 12. Lifecycle and callback argument

Adopted from B2 §9 without modification: the restricted theorem is invariant under
lilToon's callback-100 (`VRChatModule`) shader regeneration. `LIL_RENDER` is fixed per
pass asset; the core equation (main sample, `_Color` multiply, cutout transform,
shadow clip) is unconditional; the core uniforms are always retained by the input
scan; every optional alpha/coverage feature is refused at its runtime gate, so a
stripped-or-enabled compile difference never reaches the proof; `LIL_INPUT_OPTIMIZED`
touches only light-volume paths. Therefore the proof is **NDMF-complete**; Outcome B
(upload-time validation) is not a prerequisite for this slice [fixed decision 6]. The
converted clone references the stable committed opaque asset identity; regeneration
changes file contents, not identity. Apply-on-Play remains unavailable for positive
mutation — unchanged. Task 6 pins the invariance with the compilation-variant and
callback-100 falsifiers (controlling §14 test 10; coverage items 13–14).

## 13. Source-material and Unity-asset safety

- The source material is never written: `new Material(source)` is the only
  relationship; clones are AMUSE-owned transients.
- Nothing is saved. Persistence is NDMF assignment (`BuildContext.Serialize()`);
  the clone is left unnamed by the conversion and named by
  `PreparedAlphaSeparation.RegisterOpaque` (`"<source.name> (AMUSE Opaque n)"`) —
  unchanged. No `SaveAsset` anywhere in new/modified production files; the
  `AlphaSeparationPersistenceTests` structural audit list is extended to the new
  files (the established structural guard is the only practical protection for this
  invariant).
- Sweep destroys `CreatedClones` only; a source material can never be swept.
- A refused slot destroys its pending clones before continuation (existing).
- The implementation creates no Unity asset beyond two public synthetic test
  fixture shaders added by the plan's test tasks
  (`LilToonCutoutConversionTest.shader`, `LilToonOpaqueConversionTest.shader`,
  each with its `.meta` as one unit — no production or authoring asset is touched).
  Runtime test materials and textures are created under the existing
  `Assets/AmuseTests_*` temp folders and deleted in teardown, as today. No scene,
  prefab, import setting, or Census Lab file is created or modified.
- Any future Unity MCP operation requires exact instance identity first:
  `Application.dataPath == <repo-root>/Assets`, no case-only match, pinned instance
  when multiple are reachable.

## 14. Test strategy

Public synthetic fixtures only; no vendor package; no Census Lab. Seams:
`LilToonFixtureTestBase` (stand-in materials/textures), a new lilToon verified
conversion seam mirroring `VerifiedPoiyomiTestSeams` (real eligibility, real recipe,
real read-back; identity checks substituted), and the existing
`PreparationTestPlatform`/`AvatarProcessor.ProcessAvatar` NDMF path for end-to-end
builds. The 22 required coverage items map to tasks in the implementation plan
(`2026-08-30-liltoon-cutout-opaque-conversion.md` §Falsifier map). Texture-evidence
cases (items 4–5, 8–10, 15) run at two layers: deterministic resolver-seam tests with
synthetic mip chains (existing `AlphaSemanticsResolverTests` infrastructure) plus one
imported-mipmap integration case per discriminating shape (existing
`UnityAlphaFieldEvidence` test pattern). Behavioral contracts preferred; the only
source-text test is the extended `SaveAsset` structural audit, which is the
established and only practical guard for that invariant.

## 15. Deferred work (recorded, not designed)

- Affine `_MainTex_ST` (exact dyadic/rational representability obligation already
  documented in `AlphaSemanticsResolver.cs:327-337`).
- Cutoff-margin extension (constant raw alpha `c ≥ cutoff + 5×10⁻⁵` provable) [B2 §11
  row 6].
- Wider sampling vocabularies; UDIM/IDMask tile-aware proofs; dither-active materials.
- Outline (source+target attestation, outline-alpha theorem, seam characterization)
  [F0 roadmap row 3]; transparent Normal/OnePass/TwoPass [rows 4–5]; Lite [row 6];
  Multi lifecycle [row 7]; Tessellation [row 8]; specialized-mode formal refusals
  [row 9]; refraction narrow state [row 10].
- Generated optional alpha features (alpha-mask/dither/dissolve active): always
  requires Outcome B unless independently proven.
- Per-slot polished diagnostics; `.lilcontainer` policy decision [F0 §13.3].

## 16. Explicit falsifiers

Each is executable in the plan; each fails a named plausible wrong implementation:

1. Cutout family selection returns the cutout alpha request and the combined capture
   schema; opaque lilToon's requests are reference-identical to today (fails widened
   recognition / mutated shared request).
2. Poiyomi routing regression: conversion relevance, admission, overwrite rule, and
   conversion step still route through `PoiyomiOpaqueConversion` facts; all existing
   Poiyomi tests green unmodified (fails R1 mis-routing).
3. Fully opaque texture, all mips, `_Color.a = 1` → `ProvenOpaque`; `_Color.a = 0.8`
   variant → uniform `MustRemainTransparent` (fails `a > cutoff` re-derivation and
   ignoring `_Color.a`).
4. Cutoff boundary: `0.9999` proven; `1.0` and `1.001` produce no provable triangle —
   Unknown at the classification layer, slot refuses
   (`AdmittedMaterialSemanticsUnknown`), nothing applied (fails Poiyomi `<= 1`
   reuse, plain-`clip` semantics, and any implementation that classifies a fully
   discarded domain proven); Task 3 separately pins the duplicate conversion gate.
5. Non-finite cutoff refuses (fails `NaN`-falls-through implementations).
6. One transparent texel only in a high mip → `MustRemainTransparent` (fails mip-0-only
   checks).
7. Bilinear footprint dilation vs point filtering; repeat seam vs clamp (fails
   hull-only classification).
8. Non-singleton animation on each proof-relevant and conversion-relevant property
   refuses (fails live-value reads).
9. Each optional alpha/coverage path refuses independently, including the
   `_IDMaskControlsDissolve = 1` + `_IDMaskPrior8 = 1` counterexample that renders
   nothing and must never be proven (fails compiled-feature-only gating).
10. Compilation-variant invariance: identical verdict across committed and widened
    feature-define sets for gate-off materials (fails callback-dependence).
11. Unsupported texture evidence never becomes opaque (fails default-to-opaque).
12. Identity-ST positive case; non-identity/animated-ST refusal (fails affine leak).
13. Clone recipe read-back: every canonical fact reads back; wrong target/queue/tag
    throws and destroys (fails silent normalization).
14. Source material property-for-property unchanged after conversion (fails in-place
    mutation).
15. Poiyomi and lilToon-cutout slots coexist; mixed supported admitted sets map;
    unsupported family value still refuses (fails silent over-acceptance).
16. Representative unsupported families refuse (cutout-outline, transparent, Lite,
    Tess, Multi, Gem, Fur, Refraction, fake-shadow, outline-only stand-ins) — vacuity
    guard (fails name-tolerant recognition).
17. End-to-end NDMF: preparation, generated-material persistence via assignment, mesh
    separation, appended slot assignment, curve rewrite, apply, sweep (fails
    off-pipeline shortcuts).
18. No `SaveAsset`/authoring-asset mutation (structural audit + structural digests).

## 17. Stop conditions and their current status

| Premise | Status at base SHA |
|---|---|
| B1 source/target identity & canonical recipe | holds — current opaque pins equal B1 §6 target digests; B1 §9 matrix matches the controlling §6 recipe [SOURCE] |
| B2 alpha equation & callback independence | holds — B2 §3/§5/§9 verified against pinned source; adopted verbatim [B2] |
| Mixed-family slot mappings preservable | holds — mapping is `Dictionary<Material, Material>` per slot; family-agnostic downstream [SOURCE] |
| Source-material immutability | holds — `new Material` only; sweep scoped to `CreatedClones` [SOURCE] |
| Generated-material validation after shader replacement | holds — B1 §7/§9 measured read-back across the swap; R5 validates expected identity [B1] |
| Cutout attestation data complete/consistent | holds — B1 §5/§6 supplies every pin; no contradiction in current source [SOURCE] |

No stop condition triggered. No contradiction between the four investigations and the
checked-out source was found (citation drift ≤ 1 line in three places, content
identical).

## 18. Self-review

- The design adds one enum member, one frontend profile, one semantics file, one
  conversion file, per-family fact lookups, and a second typed seam — the exact shape
  controlling §12 authorized, and nothing from §3's non-goals.
- Every refusal added is at least as conservative as the state it replaces; the only
  coverage expansions are the pinned cutout family and attested-opaque map-to-self,
  both controller-reviewed decisions recorded in §10.
- The recipe, gates, evidence request, and theorem are transcriptions of measured and
  source-pinned facts, with the two judgment calls (blend/depth gate set §9.3;
  `_BlendOpAlphaFA` gating) marked [DECISION] and individually falsifiable.
