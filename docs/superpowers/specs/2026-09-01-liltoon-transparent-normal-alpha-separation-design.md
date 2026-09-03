# lilToon Regular Transparent Normal → Canonical Opaque Separation — Design

| | |
|---|---|
| **Status** | **ACCEPTED.** Both prerequisites closed by the controller-authorized scratch probe on 2026-09-01 (§18, evidence in **[T1 §3.4, §9.4]**). Implementation is authorized only after a separate implementation plan is written and approved. |
| Branch | `investigate/liltoon-transparent-normal-alpha-separation` |
| Created from | `main`, verified equal to `origin/main` (0 ahead, 0 behind) |
| Base SHA | `a3c547b6064b20709289a1062c11b7fd72818568` (merge commit of PR #42, "Support affine `_MainTex_ST` mappings") |
| Working tree at branch creation | two pre-existing user-owned Unity toolchain/sysroot modifications (`Packages/manifest.json`, `Packages/packages-lock.json`), inspected additive-only, untouched by this work |
| Investigation | `docs/superpowers/investigations/2026-09-01-liltoon-transparent-normal-alpha-separation.md` (cited below as **T1**) |
| Unity / lilToon | Unity 2022.3.22f1. lilToon 2.3.4 is **not installed** in this project and must not be. |
| Census Lab | not used, not inspected, not modified |

Each claim carries a tag. **[SOURCE]** marks a pinned vendor source or a fact read
from the checked-out repository at the base SHA. **[T1]/[B1]/[B2]/[F0]** marks an
established fact of a merged or companion investigation, cited by section.
**[MEASURED]**, **[INFERENCE]**, and **[DECISION]** (a choice this design makes,
which the controller may overturn) and **[DECISION NEEDED]** complete the set. No
`[PREREQUISITE]` items remain (§18).

---

## 1. Problem statement and supported contract

The AMUSE alpha separation moves triangles *proven visually opaque* off an
alpha-mode material onto an appended submesh, which renders with an
AMUSE-generated canonical opaque material. Two source families ship today:
Poiyomi Toon 9.3.64, and lilToon 2.3.4 regular no-outline **cutout**. Every
other lilToon identity is `Unsupported`, fails the material-dependency closure
of the renderer, and refuses the renderer (`UnityMaterialSemantics.cs:244-289`).
`[SOURCE]`

This design is the third source family:

```
Hidden/lilToonTransparent  →  prove triangles opaque (T1 §9 theorem)
                           →  append on the SAME AMUSE-generated canonical `lilToon`
                              opaque material the cutout slice already produces
                           →  existing mesh-separation pipeline, unchanged
```

**Supported contract.** Take a triangle *T* of submesh *S*, rendered with
material *M*, under the attested regular no-outline lilToon 2.3.4 Transparent
**Normal** source. The conversion applies iff the T1 §9.1 theorem proves *T*
`ProvenOpaque` **and** the §9 source eligibility holds. A proven triangle
renders on an AMUSE-generated clone that carries the canonical opaque recipe.
Every other triangle stays on the unmodified source material. AMUSE never
removes fully-clipped domains and never reinterprets them as `ProvenOpaque`.

**What the contract does *not* promise.** Draw order relative to other
materials in the 2000–2460 band changes, as it already does for the shipped
cutout slice. Under the Conservative policy this is an explicitly permitted
representation change ("transparent-vs-opaque implementation when a surface is
proven visually opaque"). Users that need the original ordering use
compatibility-strict mode or a per-material exclusion. `[DECISION]`

## 2. Exact positive profile (allowlist)

| Axis | Supported value |
|---|---|
| Package | `jp.lilxyzw.liltoon` `2.3.4`, format stamp `_lilToonVersion == 45` |
| Source shader | `Hidden/lilToonTransparent`, GUID `165365ab7100a044ca85fc8c33548a62` **[T1 §4.1]** |
| Source pass | `Hidden/ltspass_transparent`, GUID `2683fad669f20ec49b8e9656954a33a8`, `#define LIL_RENDER 2` **[T1 §4.1]** |
| Source material digest | `ea247d3cd6ecb09ad4aeefdcad37480c0dffa40d594a3b457624097f2372ba13` **[MEASURED — T1 §3.4]** |
| Source pass digest | `700a607661f2cc43550452795d8eae0634509dbd07b4e8c381d9412fcc52517f` **[MEASURED — T1 §3.4]** |
| Include-tree digest | `6e2dce6cb3073d5e04b569a14df8e0944c93ca408999fb42d7c717050c48fd46` (shared, already pinned) |
| Target shader | `lilToon`, GUID `df12117ecd77c31469c224178886498e` — the already-attested opaque identity, **unchanged** |
| Transparent mode | **Normal** only |
| Outline | none |
| Pipeline | regular — non-Lite, non-Tessellation, non-Multi |
| Source queue / `RenderType` | exactly 2460 / `TransparentCutout` |
| Cutoff eligibility | `_Cutoff <= 1` (family-specific, **not** the cutout `0.9999`) **[T1 §9.2]** |
| ForwardAdd premultiply | `_AlphaBoostFA >= 1` **[T1 §5.3]** |
| Distance fade | `_DistanceFade.z == 0` **[T1 §5.4]** |
| Subpass shadow clip | finite `_SubpassCutoff <= 1` **[MEASURED — T1 §9.4]** |
| UV support | exact identity `_MainTex_ST == (1,1,0,0)` on channel 0, `_MainTex_ScrollRotate == (0,0,0,0)` |

Positive support is **allowlist-based**: a third pinned identity inside the existing
lilToon frontend, never a widened recognition rule.

## 3. Non-goals

- No `IOpaqueConversion`, `ISourceEligibility`, adapter interface, plugin
  registry, dynamic discovery, render-state IR, `RenderSemantics` type,
  `SurfaceMode` hierarchy, pass model, declarative gate schema, planner,
  conversion framework, or third-party extension API.
  **[T1 §13.4]** audits each of these against a second consumer and refuses
  every one.
- No support, attestation, or recipe for: Transparent OnePass, Transparent
  TwoPass, any outline family, OutlineOnly, Lite, Tessellation, Multi, Overlay,
  Gem, Fur (all variants), Refraction, RefractionBlur, FakeShadow, or
  `.lilcontainer` identities.
- No affine `_MainTex_ST` for lilToon, no arbitrary UV channels, no
  trilinear/anisotropic/mirrored/asymmetric sampling, no UDIM- or
  IDMask-aware proofs, no dissolve/alpha-mask-active proofs, no cutoff-margin
  extension for constant alpha below one.
- No change to `AnalyzeBaseMaterial`: transparent materials keep routing
  through the opaque lilToon attestation, which refuses, leaving
  BaseColor/Emission/Normal all-`Unknown`. This slice adds only the
  alpha-separation path.
- No upload-time ("Outcome B") validation — the theorem is NDMF-complete **[T1 §8.3]**.
- No texture, keyword, or pass-enable writes.
- No behavioral change to the target: the recipe, target attestation, clone
  preparation, and clone validation move unchanged. The target *request*
  narrows by one property (`_Cutoff` → source modules). See §10 **[T1 §11]**.
- No new mesh, planning, apply, persistence, dedup, or curve-rewrite concept.

## 4. Current-state reconstruction

Read from the checked-out tree at the base SHA. Load-bearing facts:

1. **Family selection is one exact-name map with two consumers.**
   `UnityMaterialSemantics.ClassifyShaderName` (`:244-280`) is the single map.
   Both `IdentifyFamily` (`:282-289`) and `CaptureAlphaMaterials` (`:96-127`)
   go through it. The cutout design had to fix the R2 duplication. The fix
   already exists. `[SOURCE]`
2. **`CapturedAlphaMaterialFamily`** (`:11-17`) has four members today:
   `Unsupported`, `Poiyomi`, `LilToon`, `LilToonCutout`. `[SOURCE]`
3. **Attestation is profile-parameterized.** `LilToonSourceProfile`
   (`:373-400`) is a private 7-field record. `OpaqueProfile` (`:402-410`) and
   `CutoutProfile` (`:412-420`) are static instances. `Verify` (`:1183`) and
   `Gather` (`:1388`) take a profile. `TryVerifyLilToonCutoutIdentity`
   (`:1168-1173`) and `GatherCutoutSourceEvidence` (`:1358-1363`) are two-line
   wrappers. `[SOURCE]`
4. **Cutout alpha semantics is a self-contained sibling file.**
   `LilToonCutoutMaterialSemantics` holds its own gate array (`:70-90`), its
   own exact evidence request (`:100-142`), and one interpretation function
   (`:211-366`) ending in `Texture(...)` / `TextureTimesConstant(...)`.
   `[SOURCE]`
5. **`LilToonOpaqueConversion` mixes target and source responsibilities.**
   Target: `CanonicalOpaqueTuple` (`:154-174`), queue/`RenderType` constants
   (`:180-184`), `ReadEffectiveRenderState` (`:497-506`),
   `TryFindNonCanonicalFact` (`:515-547`), both `PrepareCanonicalOpaqueClone`
   overloads (`:602-700`). Source: `MaxProvableCutoff` `0.9999` (`:195`),
   `SupportedCutoutRenderQueue` 2450 / `SupportedCutoutRenderType`
   (`:183-184`), and `EvaluateVerifiedEligibility` gates 3–12 (`:263-432`).
   Shared: `IsUnitSourceFactorAtAlphaOne` / `IsZeroDestinationFactorAtAlphaOne`
   (`:453-463`). `[SOURCE]`
6. **Preparation dispatches on a closed enum**, one `case` per family
   (`AlphaSeparationPreparation.cs:455-573` `LilToon` / `LilToonCutout`,
   `:573-702` `Poiyomi`), plus two family lookups (`:703-735`). The
   verified-conversion test seams are two explicit typed delegates
   (`:26-45`). `[SOURCE]`
7. **The resolution layer is family-blind and already sufficient.**
   `AlphaSemanticsResolver` refuses `multiplier > 1`, maps `== 1` to sampled
   classification and `< 1` to uniform `MustRemainTransparent` (`:304-347`),
   admits UV0 only (`:383-386`), Point/Bilinear × Clamp/Repeat only
   (`:394-428`), and classifies every mip. The `AffineUvTransform` from
   PR #42 is family-blind machinery and is **not** authorization for lilToon.
   `[SOURCE]`
8. **Everything downstream of `Material → Material` is shader-agnostic**:
   `MeshSeparationPlanner`, `AlphaSeparationRecords`, `AlphaSeparationApply`,
   dedup, curve rewriting, and the clone sweep. `[SOURCE]`

**Contradiction check.** No statement in B1, B2, F0, the cutout design, or the
affine design contradicts the current tree or T1. T1 **narrows** two prior
statements. This section records them so the design does not inherit them
silently:

- F0 §7.4 said the transparent alpha equation is "richer
  (fade/dither/dissolve layers)" than cutout. Partly false at the pinned
  version: **dither is compiled out for `LIL_RENDER 2`** and **depth fade is
  dead code**. The genuinely new writers are ForwardAdd premultiply, distance
  fade, and the subpass shadow clip. **[T1 §5.5, §6]**
- F0 §7.4 said "FORWARD_ADD participation must carry over (it exists on both
  source and plain-opaque target)". True, and now stronger: the four
  referenced pass declarations are **byte-identical** between the two pass
  assets. **[T1 §4.3]**

## 5. End-to-end data flow (unchanged pipeline, third source family)

```
NDMF PlatformFinish barrier (AmusePlatformFinishPass.Execute; production: all seams null)
  per renderer:
    closed capture (UnityAnimationEvidenceCapture.Capture)
      family selection per material          ← +LilToonTransparent, one map, exact name
      alphaRelevance = Combine(family alpha requests)     ← transparent request joins
      captureSchema  = Combine(family capture requests)   ← transparentAlpha + conversion
      closed capture + attestation (IsAttestedAlphaMaterial → transparent identity verify)
    runtime-state resolution per slot (AdmittedMaterialStates.ResolveSlot)
      transparent alpha value → AlphaSemanticsResolver → per-triangle outcomes
    geometry capture + classification (UnityRendererAlphaAnalysis, MeshSeparationPlanner)
    preparation (AlphaSeparationPreparation.Prepare)
      per slot, per admitted material (ConvertAdmittedMaterial):
        LilToon            → maps to itself (attested opaque; no conversion facts)
        LilToonCutout      → cutout eligibility  → LilToonOpaqueTarget clone
        LilToonTransparent → transparent eligibility → LilToonOpaqueTarget clone   ← NEW
        Poiyomi            → unchanged
        anything else      → OpaqueConversionUnsupportedFamily (slot-local)
      avatar-wide dedup (RegisterPreparedOpaque, unchanged)
later pass (AlphaSeparationApply.Execute)
  PrepareSurvivingSet (pass-3, family-agnostic) → FinalizeClone → ApplyFinalization
  sweep destroys CreatedClones only
```

No new pipeline stage, pass, record type, planning concept, or host capability.
**[T1 §8.1, §13.1]**

## 6. Source attestation profile

A third `LilToonSourceProfile` instance. **[DECISION]** **[T1 §13.2]**

| Field | Value |
|---|---|
| `shaderName` | `Hidden/lilToonTransparent` |
| `shaderGuid` | `165365ab7100a044ca85fc8c33548a62` |
| `passShaderName` | `Hidden/ltspass_transparent` |
| `passShaderGuid` | `2683fad669f20ec49b8e9656954a33a8` |
| `renderMode` | `2` (new constant `TransparentRenderMode`) |
| `shaderCanonicalDigest` | `ea247d3cd6ecb09ad4aeefdcad37480c0dffa40d594a3b457624097f2372ba13` **[MEASURED]** |
| `passCanonicalDigest` | `700a607661f2cc43550452795d8eae0634509dbd07b4e8c381d9412fcc52517f` **[MEASURED]** |

Shared, unchanged: `PackageName`, `PackageVersion`, `ShaderFormatVersion`,
`IncludeTreeDigest`, the canonicalization rules, `TryScanRenderMode`, and the
provenance conjunction. Two new wrappers:
`TryVerifyLilToonTransparentIdentity(evidence, out diagnostic)` and
`GatherTransparentSourceEvidence(shader, evidence)`.

**Failure boundary.** Any mismatch of name, GUID, format stamp, package, pass
identity, canonicalization provenance, either digest, or `LIL_RENDER != 2` →
attestation refuses → the material is not admitted → the renderer batch
refuses. There is no name-only fallback, and near-miss vendor names
(`Hidden/lilToonOnePassTransparent`, `Hidden/lilToonTwoPassTransparent`,
`Hidden/lilToonTransparentOutline`) never reach the profile because
`ClassifyShaderName` is exact-ordinal. **[T1 §4.2]**

## 7. Alpha theorem (adopted from T1 §9.1)

This design adopts the theorem verbatim and does not restate it in full. Its
seven clauses: (1) identity. (2) The twelve-plus cutout-shared gates exactly
zero, finite, plus `_DissolveParams.x == 0`. (3) The three transparent-only
gates plus `_SubpassCutoff <= 1`. (4) Exact-identity `_MainTex_ST` and zero
`_MainTex_ScrollRotate` per binary32 component. (5) Texture
format/residency/filter/wrap and a measured per-mip alpha field. (6) Exact
per-triangle classification over every mip with footprint and wrap arithmetic,
and `_Color.a == 1`. (7) Exact-singleton animation closure over every property
in clauses 2–4 and 6.

Three deltas from the cutout theorem that the implementation must not lose:

| Delta | Cutout | Transparent | Consequence if copied wrongly |
|---|---|---|---|
| Alpha site | `saturate((a−c)/max(fwidth(a),1e−4)+0.5)` then `if(a==0) discard` | plain `clip(a − _Cutoff)` | reusing `0.9999` silently refuses `c ∈ (0.9999, 1]`. Reusing the *coverage* reading would mis-model partial coverage that does not exist here |
| Dither | compiled, must be gated off | **compiled out** (`LIL_RENDER == 1` only) | gating it is a free false negative. *Reading* it as active would be a modeling error |
| Post-clip writers | none | ForwardAdd premultiply, distance fade, subpass shadow clip | omitting any is a **false positive** |

`[T1 §5, §6, §9.2]`

### 7.1 Neutral-claim gating

`a ≡ 1` is a neutral claim. Per the standing rule
(`docs/architecture/shader-frontend-comparison.md`), the design asserts it
only after every independent writer in T1 §6 rows 1–23 is proven off, proven
dead, or proven an identity at `a ≡ 1`. The interpretation function must
therefore evaluate **all** gates before constructing any value, exactly as
`LilToonCutoutMaterialSemantics.InterpretCutoutAlpha` does (`:215-291` before
`:294`). The design extends the parity test in
`Tests/Editor/Semantics/Characterization/NeutralClaimGatingTests.cs` with the
transparent site.

## 8. Evidence request

A new `LilToonTransparentMaterialSemantics.AlphaEvidenceRequest`, exact by contract — no
fewer and no more. **[DECISION]**

| Kind | Properties |
|---|---|
| `shaderName` | `true` |
| `activeColorSpace` | `false` |
| scalars | `_lilToonVersion`, `_Invisible`, `_UDIMDiscardCompile`, `_UDIMDiscardMode`, `_ShiftBackfaceUV`, `_UseParallax`, `_UseMain2ndTex`, `_UseMain3rdTex`, `_AlphaMaskMode`, `_IDMask1`…`_IDMask8`, `_IDMaskControlsDissolve`, `_Cutoff`, **`_AlphaBoostFA`**, **`_SubpassCutoff`** |
| colors | `_Color` |
| vectors | `_DissolveParams`, `_MainTex_ScrollRotate`, **`_DistanceFade`** |
| textures | `_MainTex` with `ScaleOffset \| SourceIdentity \| Sampling \| AlphaChannel` |

Deltas from the cutout request (`LilToonCutoutMaterialSemantics.cs:100-142`):
**+3** (`_AlphaBoostFA`, `_SubpassCutoff`, `_DistanceFade`), **−1**
(`_UseDither`). The pinned `LIL_RENDER 2` source compiles the `_UseDither`
runtime path out entirely **[T1 §6 row 16]** — controller-approved
`[DECISION]`, so the request omits `_UseDither`. The positive
`_UseDither = 1` falsifier (§14 row 7) stays precisely because the property
is *absent* from the request: it proves that an active authored dither toggle
still converts, which the omission makes observable.

`_MainTex_ST` is deliberately **not** a vector request: it rides the
`ScaleOffset` kind of the texture request, which also derives the animatable
binding name — unchanged from the cutout design.

The opaque lilToon request and the cutout request are **not** widened. Each family
selects its own request object.

**Three-way request ownership** `[DECISION]`. The current single 19-property
conversion request mixes one source-eligibility property (`_Cutoff`) into the
schema of the target (`LilToonOpaqueConversion.cs:199-237,703-712`). The
split separates them:

| Request | Owner | Contents |
|---|---|---|
| `RecipeEvidenceRequest` | `LilToonOpaqueTarget` | `shaderName` + the **18** recipe properties, presence and scalar, projected directly from `CanonicalOpaqueTuple` |
| `SourceEvidenceRequest` | `LilToonCutoutSourceEligibility` | scalar/presence `_Cutoff` |
| `SourceEvidenceRequest` | `LilToonTransparentSourceEligibility` | scalar/presence `_Cutoff`, `_AlphaBoostFA`, `_SubpassCutoff` and vector `_DistanceFade` |

The design **deletes** `BuildConversionSchema` (`:703-712`). The recipe schema
is now exactly the property names of the tuple, with no `+1` slot, so the
"derived rather than retyped so the two cannot drift" invariant becomes an
identity instead of a concatenation.

`AlphaSeparationPreparation.ConversionRequestForFamily` returns, per family, a once-built
`Combine(LilToonOpaqueTarget.RecipeEvidenceRequest, <family>SourceEligibility.SourceEvidenceRequest)`.
That single object continues to drive **both** the derived-evidence subset handed to
`EvaluateVerifiedEligibility` (`AlphaSeparationPreparation.cs:476-479`) and the
renderer-wide animation-singleton buckets (`:140-142,212-215`), so no
proof-relevant property loses its animation closure — `_Cutoff` keeps its
conversion-side singleton admission through the combined object, in addition to
the alpha-side admission it already has from
`LilToonCutoutMaterialSemantics.AlphaEvidenceRequest:97-126` and its transparent
sibling.

Capture schema for the family:
`Combine(LilToonTransparentMaterialSemantics.AlphaEvidenceRequest, ConversionRequestForFamily(LilToonTransparent))`,
mirroring `LilToonCaptureRequest` (`UnityMaterialSemantics.cs:321-324`).

## 9. Source eligibility

A new
`LilToonTransparentSourceEligibility.EvaluateVerifiedEligibility(evidence,
effectiveRenderQueue, effectiveRenderType)`, a pure function over
already-captured, already-admitted evidence plus the two non-property facts.
It runs no capture and touches no live material. **[DECISION]**

Gate order is load-bearing and mirrors the rationale of the cutout function
(schema, then finiteness over every captured scalar, then the
mutation-authorizing gates — a NaN capture must never dress itself up as a
plausible named refusal):

| # | Gate | Rule | Refusal member |
|---|---|---|---|
| 1 | Schema | every conversion-read scalar present | `ConversionPropertyAbsent` |
| 2 | Finiteness | every captured scalar finite | `ConversionPropertyNotFinite` |
| 3 | Effective queue | `== 2460` | `UnsupportedRenderQueue` |
| 4 | Effective `RenderType` | `== "TransparentCutout"` | `UnsupportedRenderType` |
| 5 | `_ZTest` | `== 4` (LEqual) | `UnsupportedDepthComparison` |
| 6 | `_ZWrite` | `== 1` | `UnsupportedDepthWrite` |
| 7 | `_ColorMask` | `== 15` | `UnsupportedColorMask` |
| 8 | Depth offset | `_OffsetFactor == 0 && _OffsetUnits == 0` | `UnsupportedDepthOffset` |
| 9 | Base RGB blend | `_BlendOp == Add`, `_SrcBlend ∈ {One, SrcAlpha}`, `_DstBlend ∈ {Zero, OneMinusSrcAlpha}` | `UnsupportedBlendEquation` |
| 10 | Base alpha blend | same shape on `_BlendOpAlpha`/`_SrcBlendAlpha`/`_DstBlendAlpha` | `UnsupportedAlphaBlendEquation` |
| 11 | ForwardAdd blend | `_SrcBlendFA ∈ {One, SrcAlpha}`, `_DstBlendFA == One`, `_BlendOpFA == Max`, `_BlendOpAlphaFA == Max` | `UnsupportedForwardAddBlendEquation` |
| 12 | Clip threshold | `_Cutoff <= 1` | `ClipThresholdDiscardsOpaqueAlpha` |
| 13 | **ForwardAdd premultiply** | `_AlphaBoostFA >= 1` | **`UnsupportedForwardAddAlphaBoost`** (new) |
| 14 | **Distance fade** | `_DistanceFade.z == 0` (vector finite) | **`UnsupportedDistanceFade`** (new) |
| 15 | **Subpass shadow clip** | `_SubpassCutoff <= 1` (finite) **[T1 §9.4]** | **`UnsupportedSubpassCutoff`** (new) |

Gates 1–11 are the merged cutout rules **unchanged** — including the fact that gate 9
already admits `OneMinusSrcAlpha`, which is exactly the transparent canonical value
**[T1 §7]**. Gates 12–15 are family-specific.

Deliberately ungated, with reasons: `_AlphaToMask` (full coverage at `a ≡ 1`
under any value), `_SrcBlendAlphaFA`/`_DstBlendAlphaFA` (the pass declares the
literal `Zero One` alpha pair regardless), `_Cull`/`_ZClip`/stencil
(`[_Property]`-driven identically in both pass assets, copied by the clone),
`_UseDither` (compiled out). **[T1 §4.4, §5.6, §6]**

## 10. Target preparation — behavior unchanged, schema narrowed

The design reuses the *behavior* of the target without change: the 18-property
canonical tuple, queue 2000, `RenderType=Opaque`, the shader-level
property-declaration check before any clone exists, `new Material(source)` →
shader swap → tuple writes → queue → tag → full canonical re-read
(`TryFindNonCanonicalFact`) → target-identity check, with `DestroyImmediate`
on failure and an `InvalidOperationException` rather than a refusal. The flow
saves nothing and leaves the clone unnamed. **[T1 §11]**

**The earlier "byte-for-byte reuse" claim is withdrawn.** `[DECISION]` The
recipe, the clone writer, and the read-back checks move byte-for-byte, but the
*request* of the module does not: the current `ConversionEvidenceRequest`
carries 19 properties, and the nineteenth, `_Cutoff`, is source-eligibility
evidence rather than target evidence (`LilToonOpaqueConversion.cs:199-214,703-712`
— the code literally builds the schema as "tuple + 1"). It moves to the two
source modules (§8). The accurate claim is: **the target recipe and clone path
move unchanged. The target request loses exactly one property.**

The recipe is valid for this source because the Opaque branch of the vendor
itself writes the identical 18-tuple when converting a Transparent material
(`lilMaterialUtils.cs:38-60,63-65,266-292`), and because the four pass
declarations of the target are byte-identical to those of the source
**[T1 §4.3]**. **The tuple still must not be re-derived from the upstream git
tag** (`LilToonOpaqueConversion.cs:124`). The probe re-confirmed it from the
installed package alongside the digests, and also ran the real clone path end
to end against a transparent source **[T1 §9.4]**.

The only non-canonical source default is `_DstBlend` (10 → 0), versus the
cutout `_AlphaToMask` (1 → 0) **[T1 §4.4]**. Both are recipe writes. Nothing
changes.

## 11. Architectural split

**[DECISION]** Split `LilToonOpaqueConversion` into **three responsibility
modules plus one small shared support file**, by ownership rather than by file
count. This is a move of existing code plus one sibling — **no interface, no
registry, no render-state IR, no pass IR, no mode-parameterized eligibility
engine**.

| New file | Contents | Provenance |
|---|---|---|
| `Editor/Semantics/LilToon/LilToonOpaqueTarget.cs` | **Target only**: `CanonicalOpaqueTuple` + `CanonicalOpaqueProperties`, `CanonicalOpaqueRenderQueue`, `RenderTypeTagName`, `CanonicalOpaqueRenderType`, `RecipeEvidenceRequest` + `RecipeSchemaProperties` (**18**, projected from the tuple), `ReadEffectiveRenderState`, `TryFindNonCanonicalFact`, both `PrepareCanonicalOpaqueClone` overloads | moved. Request narrowed by one property. `BuildConversionSchema` deleted |
| `Editor/Semantics/LilToon/LilToonOpaqueConversionResult.cs` | **Shared eligibility support**: `LilToonOpaqueConversionOutcome`, `LilToonOpaqueConversionRefusal` (+3 members), `LilToonOpaqueConversionEligibility`, `IsUnitSourceFactorAtAlphaOne`, `IsZeroDestinationFactorAtAlphaOne`, and the `BlendOp*`/`BlendFactor*`/`LEqualDepthComparison`/`ColorMaskAll`/`DepthWriteOn` constants | moved verbatim. **Exactly two consumers today**, both source-eligibility modules |
| `Editor/Semantics/LilToon/LilToonCutoutSourceEligibility.cs` | `SupportedCutoutRenderQueue` 2450, `SupportedCutoutRenderType`, `MaxProvableCutoff` `0.9999`, `SourceEvidenceRequest` (`_Cutoff`), `EvaluateVerifiedEligibility` | moved verbatim + owns `_Cutoff` |
| `Editor/Semantics/LilToon/LilToonTransparentSourceEligibility.cs` | `SupportedTransparentRenderQueue` 2460, `SupportedTransparentRenderType`, `MaxProvableCutoff` `1f`, `SourceEvidenceRequest` (`_Cutoff`, `_AlphaBoostFA`, `_SubpassCutoff`, `_DistanceFade`), the three new gates, `EvaluateVerifiedEligibility` | new, §9 |

**Ownership rules this split enforces** `[DECISION]`:

- No source-eligibility *predicate*, *constant*, *result type*, or *request
  property* lives in `LilToonOpaqueTarget`. The target module answers exactly
  one question: *what is the canonical opaque material, and did I build it
  correctly?*
- No target recipe value, clone operation, or read-back check lives in a source module.
- The two source modules each own their queue, `RenderType`, cutoff bound, and
  gate set. They read the 18 recipe-named properties **off the source
  material** through the combined request (§8). Sharing a property *name* with
  the recipe does not make a source render-state fact target evidence.

**Why a fourth file rather than duplication.** The shared support file holds
exact, source-independent predicates and the Unity enum constants they compare
against. It has two real consumers today, so it passes the second-consumer
test on present evidence rather than on anticipation. Duplicating the two
four-line predicates would have been acceptable. The constant table would not,
and splitting predicates from the constants they read is worse than either.
**It is a constants-and-result-types file and must stay one**: if a future
change would add a mode parameter, a gate table, a dispatch, or a shared
`Evaluate*` body to it, that change is out of scope and returns to the
controller. **[T1 §13.4]**

`LilToonOpaqueConversion.cs` is **deleted** — a clean cutover, no alias, no re-export, no
`[Obsolete]` shim. Its ~15 call sites (preparation, tests, seams) move to the new type
names in the same change.

**Why this split and not another.** Adding a transparent branch inside the
cutout eligibility function would place two different cutoff bounds and two
different queue constants in one control flow — the "large mode switch inside
cutout-specific eligibility" that the brief of the controller forbids.
Extracting a *generic* eligibility abstraction fails the second-consumer test:
the two gate sets share eleven predicates and differ in four, and nothing
dispatches over them polymorphically. Extracting the *target* passes the test
on measured evidence: two source families, one identical recipe, one identical
clone path. **[T1 §13.4]**

**What stays where it is.** `LilToonSourceAttestation` (+1 profile, +2
wrappers), `UnityMaterialSemantics` (+1 enum member, +1 arm in five closed
switches), `AlphaSeparationPreparation` (+1 `case`, +2 map entries, the
per-family `Combine`), and the two typed test-seam delegates (unchanged shape
— the transparent path reuses `VerifiedLilToonConversion`, since both lilToon
sources share the refusal vocabulary).

## 12. Exact file map

**Added**

| Path | Purpose |
|---|---|
| `Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonOpaqueTarget.cs` (+ `.meta`) | §11 target module |
| `Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonOpaqueConversionResult.cs` (+ `.meta`) | §11 shared eligibility support (result types, blend predicates, Unity enum constants) |
| `Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonCutoutSourceEligibility.cs` (+ `.meta`) | §11 |
| `Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonTransparentSourceEligibility.cs` (+ `.meta`) | §9 |
| `Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonTransparentMaterialSemantics.cs` (+ `.meta`) | §7, §8 |
| `Packages/com.alrauna.amuse/Tests/Editor/Semantics/LilToon/LilToonTransparentAlphaTests.cs` (+ `.meta`) | §14 rows 1–14, 19–20 |
| `Packages/com.alrauna.amuse/Tests/Editor/Semantics/LilToon/LilToonTransparentSourceEligibilityTests.cs` (+ `.meta`) | §14 rows 15–17 |

**Modified**

| Path | Change |
|---|---|
| `Editor/Semantics/LilToon/LilToonSourceAttestation.cs` | `+TransparentShaderName/Guid`, `+TransparentPassShaderName/Guid`, `+TransparentRenderMode = 2`, `+TransparentShaderCanonicalDigest`, `+TransparentPassCanonicalDigest`, `+TransparentProfile`, `+TryVerifyLilToonTransparentIdentity`, `+GatherTransparentSourceEvidence` |
| `Editor/Semantics/UnityMaterialSemantics.cs` | `+CapturedAlphaMaterialFamily.LilToonTransparent`, one arm each in `ClassifyShaderName`, `BuildCapturedAlphaMaterials`, `AlphaRequestForFamily`, `CaptureRequestForFamily`, `IsAttestedAlphaMaterial`, `AnalyzeAlphaMaterial`, `+LilToonTransparentCaptureRequest` |
| `Editor/Build/AlphaSeparationPreparation.cs` | `+case CapturedAlphaMaterialFamily.LilToonTransparent`, `ConversionRequestForFamily` returns a per-family `Combine(RecipeEvidenceRequest, SourceEvidenceRequest)` (§8), `+` entry in `CanonicalPropertiesForFamily`, `LilToonOpaqueConversion` → split-type renames |
| `Tests/Editor/Semantics/Characterization/NeutralClaimGatingTests.cs` | `+` transparent site |
| `Tests/Editor/Semantics/LilToon/LilToonFixtureTestBase.cs` | `+` a schema-complete stand-in transparent material (adds `_AlphaBoostFA`, `_SubpassCutoff`, `_DistanceFade`, `_DistanceFadeColor` to the fixture shader) |
| Existing `LilToonOpaqueConversion*` test files and seams | renamed to the split types |
| `docs/architecture/shader-frontend-comparison.md` | one row recording the second *source family inside one frontend* and the measured target-sharing evidence |

**Deleted**

| Path | Reason |
|---|---|
| `Editor/Semantics/LilToon/LilToonOpaqueConversion.cs` (+ `.meta`) | fully superseded by the §11 split, a clean cutover |

Unity `.meta` files move with their assets as one unit. No GUID churn on
surviving files. `Packages/manifest.json` and `Packages/packages-lock.json`
are **not** touched.

## 13. Clean-cutover requirements

- The design deletes `LilToonOpaqueConversion`. It does not deprecate it. No
  type alias, no `[Obsolete]`, no partial-class bridge, no re-export.
- The same change migrates every call site. The build must not compile with a
  mixed set of names.
- No refusal member is left unreachable, and the design adds no unreachable
  outcome member — the outcome enum still has no `AlreadyOpaque`, because an
  attested transparent source is never canonical-opaque, exactly as for
  cutout.
- The cutout gate list, cutout request, and cutout bound are **not**
  parameterized, widened, or shared with the transparent ones. The only shared
  code is the result types of the §11 support file, two exact blend
  predicates, and the Unity enum constants.
- `LilToonOpaqueTarget` must not gain a source-eligibility predicate, constant,
  result type, or request property. A reviewer can check this mechanically: the
  target file contains no `Refusal`, no `BlendFactor`, and no `_Cutoff`.
- The opaque lilToon and Poiyomi paths are byte-for-byte unaffected.

## 14. Public test and falsifier matrix

Public deterministic synthetic fixtures only — no vendor package, no Census Lab, no real
avatar. Each row names the **exact incorrect implementation it falsifies**. Rows marked
▲ are the ones a copy of the cutout suite would not contain.

| # | Test | Falsifies |
|---|---|---|
| 1 | Transparent texel present in **exactly one** mip level (mip 0 fully opaque) → `MustRemainTransparent`. A matching all-opaque control → `ProvenOpaque` | an implementation that classifies mip 0 only |
| 2 | Point × Clamp, Point × Repeat, Bilinear × Clamp, Bilinear × Repeat with a transparent texel one half-texel outside the hull. Bilinear refuses, and point proves. A hull crossing the repeat seam with a transparent texel at the wrapped coordinate refuses | hull-only classification without footprint dilation. Missing wrap normalization |
| 3 | `_Color.a = 0.8` with a fully-opaque `_MainTex` → uniform `MustRemainTransparent`. The `_Color.a = 1` control → `ProvenOpaque`. `_Color.a = 1.5` → `UnsupportedMultiplier`. `_Color.a = NaN` → interpretation refusal | ignoring `_Color.a`. Routing a non-finite multiplier into the uniform fallthrough of the resolver |
| 4 ▲ | `_Cutoff = 1.0` → **`ProvenOpaque`**. `_Cutoff = 0.9999` → `ProvenOpaque`. `_Cutoff = 1.001` (the top of the declared range) → refusal | an implementation that copies the cutout `0.9999` bound (fails the `1.0` case). An implementation that models the cutout `fwidth` coverage transform here (would call `1.001` partial rather than fully discarded) |
| 5 ▲ | `_SubpassCutoff = 1.0` → **`ProvenOpaque`**. `_SubpassCutoff = 0.5` (the shipped default) → `ProvenOpaque`. `_SubpassCutoff = nextafter(1f, ∞)` → refusal. `_SubpassCutoff = NaN` → refusal | omitting the subpass shadow condition entirely. Treating the SHADOW_CASTER pass as identical to that of the target. Picking a bound tighter than the measured slice-15 result and silently losing the whole default population |
| 6 | `_AlphaMaskMode ∈ {1,2,3,4}`, `_DissolveParams.x = 1`, and `_IDMaskControlsDissolve = 1` with `_IDMaskPrior8 = 1` and all `_IDMask* = 0` — each independently → refusal | gating on the compiled feature set rather than runtime material state. The B2 adversarial counterexample |
| 7 ▲ | `_UseDither = 1` with everything else at the positive baseline → **`ProvenOpaque`** (provably inert on this family) | a verbatim copy of the cutout gate array (which would refuse) — this row is what makes the copy detectable |
| 8 ▲ | `_DistanceFade = (0.1, 0.01, 0.5, 0)` → refusal. The `_DistanceFade.z = 0` control → `ProvenOpaque`. `_DistanceFade = (…, NaN)` → refusal | omitting the only post-clip alpha writer. Gating on `_DistanceFadeColor.a` instead of `.z` (which would leave the RGB arm divergent) |
| 9 | Depth-fade-to-alpha: assert the compiled-feature scan of the attestation never reports `LIL_FEATURE_DEPTH_FADE`, and that no `_DepthFade*` property is in the evidence request | an implementation that adds speculative `_DepthFade*` gates, or one that assumes the block is live and mis-models it |
| 10 | `_UseMain2ndTex = 1` with `_Main2ndTexAlphaMode ∈ {1,2,3,4}`, and the same for the 3rd layer → refusal each | missing the `LIL_RENDER != 0` layer alpha writers |
| 11 ▲ | `_AlphaBoostFA = 0.5` → refusal. `_AlphaBoostFA = 1` → `ProvenOpaque`. `_AlphaBoostFA = 10` (default) → `ProvenOpaque`. `_AlphaBoostFA = NaN` → refusal | omitting the ForwardAdd premultiply gate — i.e. **treating ForwardAdd as if it were the base pass** |
| 12 ▲ | A material whose shader name is `Hidden/lilToonOnePassTransparent`, otherwise byte-identical evidence → `Unsupported`, never admitted | name matching by prefix/substring/`Contains("Transparent")`. Grouping by `LIL_RENDER 2`, queue 2460, or `RenderType` |
| 13 ▲ | The same for `Hidden/lilToonTwoPassTransparent` | as row 12. Also an implementation that admits on pass-asset identity alone (both share `Hidden/ltspass_transparent`) |
| 14 | `_MainTex_ST = (1,1,0,0.0001)`, `(2,1,0,0)`, and `_MainTex_ScrollRotate = (0,0,0.0001,0)` → `UnsupportedUv` each. The exact identity control → `ProvenOpaque` | delegating lilToon ST to the family-blind affine resolver from PR #42. Using the epsilon-based `Vector2`/`Vector4` equality of Unity instead of per-binary32-component tests |
| 15 | Non-singleton or disagreeing animation on **each** of `_Color`, `_Cutoff`, `_AlphaBoostFA`, `_SubpassCutoff`, `_DistanceFade`, `_DissolveParams`, `_AlphaMaskMode`, `_MainTex_ScrollRotate`, `_MainTex_ST`, and each clause-2 gate → slot refusal | reading live material values instead of captured, admitted evidence. A request that omits a proof-relevant property (which would make its binding *unrecognized* rather than *refused*, or worse, invisible) |
| 16 | Compilation-variant invariance: identical gate-off evidence yields an identical verdict. This holds whether the stand-in pass source declares all features or only an unrelated superset | a verdict that depends on the define set rather than runtime gates — i.e. a broken callback-100 invariance claim |
| 17 | Eligibility: custom queue 2475, custom `RenderType` `"Transparent"`, `_ZWrite = 0`, `_ZTest = 8` (Always), `_ColorMask = 7`, `_OffsetFactor = -1`, `_BlendOp = Sub`, `_DstBlend = SrcAlpha`, `_BlendOpFA = Add`, `_DstBlendFA = Zero` — each independently → the exactly-named refusal. Plus a source with `SetShaderPassEnabled("ForwardAdd", false)` whose clone must read back the same disabled pass | silently normalizing authored render state that the alpha proof does not preserve. A `_BlendOpFA = Add` acceptance would double-composite ForwardAdd against the base pass. A clone path that drops the pass-enable state of the source |
| 18 | Prepared-clone contract: the clone carries `lilToon`, all 18 canonical values read back, queue 2000, `RenderType=Opaque`. A target shader missing a recipe property throws before any clone exists. A read-back disagreement throws after `DestroyImmediate` | a wrong opaque target. An incomplete recipe. Converting a read-back failure into a conservative refusal |
| 19 | Mutation audit around a real NDMF build. The SHA of the serialized properties of the source material, the source mesh, every texture and its import settings, every animation clip, every prefab, and the scene stay unchanged. Only `CreatedClones` and the generated mesh differ. The existing `SaveAsset`-token production-file audit extends to the new files | any write to authoring assets. Any speculative persistence |
| 20 | Locality: a renderer carries an admitted Poiyomi slot and an admitted lilToon-cutout slot next to one refused transparent slot. Both admitted siblings still convert. An all-`Unknown` outcome never becomes `ProvenOpaque` (unsupported format, streamed mips, missing readback, degenerate triangle, NaN UV, region overflow) | family uncertainty spreading renderer-wide. Defaulting missing evidence to opaque |
| 21 | Regression parity: the full existing Poiyomi, opaque-lilToon, and cutout-lilToon suites pass unchanged. The design mutates neither the opaque nor the cutout evidence request | a shared/widened request object. A parameterized gate list leaking transparent rules into cutout |

No test in this matrix asserts source text, plumbing, or an incidental default.

**One required falsifier has no behavioral test, by construction.** "Base-pass
premultiply omitted or applied at the wrong point" is unfalsifiable *at the
proof point*: the base `LIL_PREMULTIPLY` is `rgb *= a`, which at `a ≡ 1` is
the identity, so no admitted material can distinguish an implementation that
models it from one that ignores it. Row 11 discharges the obligation: it
falsifies the *ForwardAdd* premultiply — the only premultiply site whose value
at `a ≡ 1` is not an identity **[T1 §5.3]** — and the §7 delta table records
the base site as a proven identity rather than an unmodelled one. The design
explicitly refuses a test that only asserts the source text of that site.

## 15. Validation protocol

1. Focused new classes: `LilToonTransparentAlphaTests`,
   `LilToonTransparentSourceEligibilityTests`.
2. Focused affected classes: `LilToonCutoutAlphaTests`, `LilToonBaseColorTests`,
   `LilToonEmissionTests`, `AlphaSeparationPreparationTests`,
   `AlphaSeparationApplyTests`, `AlphaSemanticsResolverTests`,
   `TriangleAlphaClassifierTests`, `NeutralClaimGatingTests`,
   `AlphaSeparationPersistenceTests`.
3. Full product EditMode suite and full research EditMode suite.
4. Unity Console inspected for errors and warnings after every Unity run.
5. Source-preservation and teardown verified (row 19).
6. Staged and unstaged diffs inspected **separately**, plus `git diff --check`.
   Confirmation that `Packages/manifest.json` and `Packages/packages-lock.json`
   remain untouched and unstaged, and that the flow includes no Unity
   `Library/`, `Temp/`, `Logs/`, or `UserSettings/` state.

A successful compile is not validation. A passing-run claim requires an
executed, observed run.

## 16. Mixed-family behavior

- **Sibling slots.** Poiyomi, lilToon-cutout, and lilToon-transparent slots on
  one renderer route, admit, overwrite-check, and convert through their own
  facts. A refused slot is slot-local.
- **Mixed animation-reachable material set on one slot.** An attested opaque
  `lilToon` material in the set maps to itself and participates in the
  completeness check without conversion, unchanged.
- **Dedup.** `RegisterPreparedOpaque` is avatar-wide and family-agnostic. Two
  different transparent sources produce two clones. One source reached twice
  produces one.
- **Capture union.** `MaterialEvidenceRequest.Combine` over the per-family
  alpha requests is unchanged. The transparent request simply joins the union.

## 17. Lifecycle and callback argument

Adopted from **[T1 §8.3]** without modification: the theorem is invariant
under callback-100 shader regeneration. `LIL_RENDER 2` is fixed per pass asset
and never rewritten. The core equation is unconditional. Every optional
mechanism is proven off, dead, or an identity. The one new feature dependency
(`LIL_FEATURE_DISTANCE_FADE`) is invariant in both directions. Upload-time
validation is not a prerequisite. The uploaded per-avatar shader artifact is
not digest-attested, and this design does not claim it is.

## 18. Prerequisites — closed

### P1 — transparent canonical digests `[CLOSED — MEASURED]`

Measured from an installed official 2.3.4 package in a throwaway Unity
2022.3.22f1 project outside AMUSE, using a byte-identical copy of the
`LilToonSourceAttestation` of AMUSE itself, not a reimplementation. The same
run first reproduced **all five** digests AMUSE already pins (opaque material,
opaque pass, include tree, cutout material, cutout pass), which is what
licenses trusting the two new values. Identical across two independent Editor
sessions.

| Constant | Value |
|---|---|
| `TransparentShaderCanonicalDigest` | `ea247d3cd6ecb09ad4aeefdcad37480c0dffa40d594a3b457624097f2372ba13` |
| `TransparentPassCanonicalDigest` | `700a607661f2cc43550452795d8eae0634509dbd07b4e8c381d9412fcc52517f` |

The probe also confirmed *why* the pass digest cannot come from upstream
bytes: lilToon rewrites every `ltspass_*.shader` at import (variant-stripping
pragmas driven by installed packages), while material-entry shaders stay
untouched. The canonicalizer of AMUSE removes exactly those two regions.
**[T1 §3.4]**

### P2 — `SHADOW_CASTER` subpass condition `[CLOSED — MEASURED]`

Controller-selected option **(a), measure**. `UnityDitherMask3D` (4×4×16,
`Alpha8`, `Point`, `Repeat`) slice 15 is uniformly alpha `1`, with slice-0,
channel, and 1-ulp controls. The exact `lilSampleDither(…, alpha = 1)`
expression returned `1` at all 16 positions. At `a ≡ 1` the clip is therefore
`clip(1 - _SubpassCutoff)`, which by the sign-preservation argument of §9.2
keeps iff `_SubpassCutoff <= 1`.

Independently confirmed behaviorally: a defaults-plus-opaque-texture
Transparent Normal material and the real canonical clone that AMUSE generated
from it rendered **bit-identically** (0 differing pixels of 65536, max
channel difference `0.00000000`) with hard directional shadows on a Standard
receiver, while a `_Color.a = 0.5` control diverged on 3954 pixels.
**[T1 §9.4]**

Fallbacks (b) `_SubpassCutoff <= 0` and (c) `shadowCastingMode == Off` are **not shipped**.

### Residual observation, not a blocker

The §9.4 measurement ran on Metal / Gamma, against an installed texture whose
`filterMode` is `Point`. Slice 14 is **not** all-ones (`0 1 1 1 …`), so the
result is **not** filter-mode independent and the design makes no such claim.
The dither texture is a fixed engine asset and the measured values are exact
`0`/`1`, so the design *expects* no API or color-space dependence — but that
is `[INFERENCE]`, not measurement. The observation covered one graphics API,
one color space, and one filter mode.

### Stop conditions during implementation

Stop, preserve evidence, and return to the controller if the implementer
observes any of these:

1. The §18 digests, re-derived on the install of the implementer, disagree
   with the two measured constants (the canonicalization assumption in
   T1 §3.2 would then fail).
2. A `_DitherMaskLOD` slice-15 alpha below `1` appears on any supported target
   path (the §18 residual `[INFERENCE]` about API/color-space independence
   would fail, and gate 15 would revert to option (b) or (c)).
3. Any of gates 1–11 needs a different rule for this source than for cutout
   (the shared-target evidence would then be weaker than T1 §11 established).
4. The clone path needs any change for a transparent source (T1 §11 falsified).
5. The design cannot do the §11 split without introducing an interface, a
   registry, or a mode parameter.
6. A proof-relevant fact is not expressible by the existing capture and
   exact-singleton admission.
7. The implementer cannot write any §14 falsifier as a deterministic public
   synthetic test.

## 19. Deferred work (recorded, not designed)

The design defers: affine `_MainTex_ST` support for lilToon (reopening paths
in the affine design §11). Wider sampling vocabularies. UDIM- and IDMask-aware
proofs. Dissolve/alpha-mask-active proofs. Cutoff-margin proofs for constant
alpha below one. Transparent OnePass (blocked on its target-identity decision,
F0 §7.5). Transparent TwoPass (refusal-leaning). Overlay (`ltsover`,
Normal-class, blocked on a product decision). Lite transparent. Outline
transparent. Tessellation transparent. Multi. **[T1 §14]**

## 20. Git authorization boundary

This branch is authorized for branch creation/switching and for writing exactly the two
documents of this research task. **Not authorized:** staging, committing, amending,
pushing, opening or merging a PR, rebasing, stashing, discarding changes, deleting
branches, rewriting history, changing remotes or repository settings, or publishing. The
pre-existing `Packages/manifest.json` and `Packages/packages-lock.json` modifications are
user-owned. The flow must never stage or include them. Implementation of this
design requires a separate, explicitly authorized branch and an approved plan.
**No plan is written yet, by instruction.**

## 21. Expected implementation report

When someone implements this design, the report must state: the branch and the
base SHA. It must confirm that the implementer transcribed the §18 constants
exactly as measured. It must give the exact supported and refused profile as
shipped, and the file map actually produced, including the
`LilToonOpaqueConversion.cs` deletion and every migrated call site. It must
give per-class test counts for the §15 list with pass/fail, and the Unity
Console state. It must give the source-preservation and teardown evidence from
falsifier 19. It must give separately inspected staged and unstaged diffs plus
`git diff --check`. It must confirm that the two package files remain
untouched and unstaged and that the diff set includes no Unity-generated
state. It must also state explicitly that the work used no Census Lab data and
no private avatar data.

## 22. Self-review

- **Does the design match the investigation?** Yes, clause for clause. §2
  mirrors T1 §2 and §4.1. §7 adopts T1 §9.1. §8 adds exactly the three
  properties T1 §5.3/§5.4/§5.6 proved load-bearing and drops exactly the one
  T1 §6 proved inert. §9 gates 12–15 are the four family deltas of T1.
  §10 rests on T1 §11. §11 rests on T1 §13.
- **Does it erase any unresolved question?** No. Measurement closed both
  prerequisites (§18), and the status header, §2, §6, and §9 gate 15 say so.
  §18 records the residual single-API/single-filter-mode scope of the P2
  measurement rather than rounding it off. Of the five `[DECISION NEEDED]`
  items in T1 §15, the design resolves the two measurement decisions (option
  (a) for P2, digests measured rather than pre-registered). The controller
  ratified three design choices — the `<= 1` cutoff bound, admitting
  `_UseDither == 1`, and the ownership split — and §8, §9, and §11 carry them
  as `[DECISION]`.
- **Is any abstraction speculative?** The only extraction is the target
  module, justified by two measured consumers sharing one measured-identical
  recipe. §3 refuses every other candidate, and T1 §13.4 audits them.
- **Would a copied cutout implementation pass this suite?** No — rows 4, 5, 7, 8, 11, 12,
  13 each fail it. Rows 7 and 4 fail it in the *positive* direction, which a
  refusal-only suite would miss.
- **Could this design ship as a smaller change?** Only by skipping the §11
  split, which the brief forbids, or by omitting a gate, which would be a
  false positive.
