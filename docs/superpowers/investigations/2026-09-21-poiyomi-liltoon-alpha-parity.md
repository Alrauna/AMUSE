# Poiyomi equivalents of the supported lilToon alpha features - parity map

## Privacy note

This record opens with a sanitization statement. It characterizes private
Census Lab fixtures by role only. It names no avatar, renderer, material,
texture, mesh, hierarchy, scene, or folder identifier. It records no machine
paths, no host names, no ports, and no instance identifiers or hashes. It
records no GUIDs. The designated locked denim garment fixture is named by
role, and its alpha-relevant material facts appear as feature values, never
as a per-slot table. Every status claim below is dated. Vendor property
names, mode numbers, and shader line anchors are public facts of the
attested Poiyomi Toon 9.3.64 source and are not private data.

## 1. Scope and method

Date: 2026-09-21. This is a stage 0 investigation. It makes no production
change and writes no production test. The question: what would AMUSE need,
feature by feature, to support on Poiyomi the alpha behaviors it already
supports on lilToon?

Trigger. On 2026-09-21 the designated locked denim garment fixture reported
`amuse.renderer.AdmittedMaterialSemanticsUnknown`. The same-day probe in
section 12.2 of the thry-pin recording showed the production Poiyomi
frontend admits the unlocked material and returns an incomplete alpha
output with the diagnostic `UnsupportedFeature: _AlphaPremultiply`.

Method. Three evidence layers.

1. The authoritative lilToon inventory comes from the committed frontend
   code and its tests. No lilToon fact below rests on memory.
2. The Poiyomi frontend audit comes from the committed
   `PoiyomiMaterialSemantics.cs` and `PoiyomiOpaqueConversion.cs` and their
   tests.
3. The vendor mechanism facts come from the attested Poiyomi Toon 9.3.64
   shader source, read in the Census Lab project where the package is
   installed. Line anchors cite that source. The source digests stay
   pinned in the repository and are not repeated here.

The live probe. One read-mostly probe ran against the Census Lab editor
instance on 2026-09-21, after the instance identity check passed. The probe
cloned the locked fixture, persisted the clone, unlocked the clone through
the embedded era 1 tool, drove the production frontend entry
`PoiyomiMaterialSemantics.AnalyzeBaseMaterial`, read the alpha-relevant
property values, and deleted the temporary asset. The probe followed the
transport discipline: a self-removing update handler wrote the result into
session state, and separate calls polled it. The probe observed no dialog
and left no asset behind. The fixture kept its locked state.

The probe result matched section 12.2 exactly and extended it with the
fixture's property facts, recorded in section 5.

## 2. The authoritative lilToon supported-feature inventory

The lilToon side supports these alpha and transparency behaviors. File and
test references are part of each row. The inventory is the parity source
of truth.

2.1 Base alpha sources. The cutout and transparent frontends prove the
plain `_MainTex` alpha sample at UV0 times `_Color.a`. The importer
theorem collapses a texture without an alpha channel to the constant
`_Color.a`. Files: `LilToonCutoutMaterialSemantics.cs`,
`LilToonTransparentMaterialSemantics.cs`. Tests:
`LilToonCutoutAlphaTests` (`GateOffMaterial_CompletesAPlainTexturedAlphaTerm`,
`UnassignedMainTex_IsRefusedAndNeverBecomesAConstant`),
`LilToonTransparentAlphaTests` (`NoAlphaSourceImport_CollapsesTheTextureArmToTheTintConstant`,
`ColorAlphaBelowOne_YieldsUniformMustRemainTransparent`).

2.2 Alpha mask modes and blend strengths. The shared mask term
(`LilToonAlphaMaskSemantics.cs`) proves the pinned equation of
`lil_common_frag.hlsl`: sample `_AlphaMask.red` through the `_MainTex`
sampler at `uvMain` transformed by the mask's own `_ST`, then
`saturate(r * _AlphaMaskScale + _AlphaMaskValue)`. Mode 1 replaces the
alpha. Mode 2 multiplies the running chain, folded through the exact
product machinery. The admitted parameter pairs are the vendor default
(scale 1, value 0), the provably saturated pair (scale 1, value at least
1), and the unassigned white default, whose term is a proven constant.
Mode 2 with an assigned mask and scale 1 is proven. Every other pair
refuses and names the property. Tests:
`LilToonCutoutAlphaTests` and `LilToonTransparentAlphaTests`
(`AlphaMaskMode1_WithAllWhiteMask_ProvesTriangleDespiteSubUnitColorAlpha`,
`AlphaMaskMode2_WithAllWhiteMask_ProvesTriangle`,
`AlphaMaskMode2_WithMaskHole_ClassifiesTheHoleUnknown`,
`AlphaMaskMode2_WithNonIdentityMaskSt_ProvesTriangle`,
`AlphaMaskMode2_WithScaleNotOneWithAssignedMask_RefusesNamingScale`,
`AlphaMaskUnassigned_Mode2_Scale1Value0_ProvesTriangle`).

2.3 Cutoff and cutout handling. The cutout family declares the cutoff in
the main texture evidence request, so every capture route binarizes the
alpha field by the cutoff and the classification layer refuses above the
twice-margin bound 0.9999. The transparent family uses the plain clip and
admits any finite cutoff of at most 1. Files:
`LilToonCutoutSourceEligibility.cs` (bound 0.9999, queue 2450),
`LilToonTransparentSourceEligibility.cs` (bound 1, queue 2460 or the
Transparent bucket 3000 to 3099). Tests:
`CutoffAtTwiceMargin_CompletesAndProvesCornerTriangle`,
`CutoffAboveTwiceMargin_IsUnknownNamingCutoff`,
`CutoffAtOrBelowOne_ProvesTheCornerTriangle`,
`CutoffAboveOneOrNonFinite_IsUnknownNamingCutoff`.

2.4 Mask and main UV modes and panning. The main UV is UV0 only, with
`_MainTex_ST` and `_MainTex_ScrollRotate` gated to exact identity at the
family boundary. The mask coordinate is uvMain under those gates,
transformed by the mask's own plain affine, which is admitted at any
finite scale and offset. Non-identity main ST refuses. Tests:
`NonIdentityMainTexStIsRejectedAtTheCutoutAlphaBoundary`,
`EveryNearIdentityNonIdentityMainTexStComponentIsRejectedExactly`,
`EveryNearZeroMainTexScrollRotateComponentIsRejectedExactly`,
`NonZeroScrollRotateComponent_IsUnknownNamingScrollRotate`.

2.5 Layer alpha. The second and third main layers compose onto the chain
by their alpha modes: replace, multiply, saturating sum, saturating
difference. The stage A admission requires UV0, exact identity scroll,
rotate, and angle, every decal flag off, and the layer dissolve at mode
zero. File: `LilToonLayerAlphaTerm.cs`. Tests:
`AddModeLayer_WithOpaqueChain_ProvesThroughTheDisjunction`,
`MultiplyModeLayer_WithSubUnitTint_IsUniformlyTransparent`,
`SubtractModeLayer_WithZeroTint_PreservesTheMainChain`,
`ReplaceModeLayer_DiscardsTheMainChain`.

2.6 Render-state and queue handling. The conversion eligibility gates the
queue, `RenderType`, depth comparison, depth write, color mask, depth
offset, both blend equations, the ForwardAdd blend, the clip threshold,
the ForwardAdd alpha boost, distance fade, and the subpass shadow clip.
The ForwardAdd premultiply is an identity only at an alpha boost of at
least 1, and the boost gate enforces that bound. Files:
`LilToonTransparentSourceEligibility.cs` (gates 3 to 15),
`LilToonCutoutSourceEligibility.cs`. Tests:
`LilToonTransparentSourceEligibilityTests`,
`LilToonCutoutSourceEligibilityTests`,
`AlphaBoostFaBelowOne_IsUnknownNamingTheProperty`,
`SubpassCutoffJustAboveOne_IsUnknownNamingTheProperty`,
`DistanceFadeEnabled_IsUnknownNamingDistanceFade`.

2.7 The depth-test policy slice. Under the opt-in policy a `Less` source
converts on a wholly opaque plan or on a mixed split, with a stated and
disclosed divergence. The proof rule itself is unchanged: the wrap-blend
rule of the 2026-09-18 two-pass investigation, section 13, stays exact.
Recorded in the same note, sections 16 and 17, with live counts.

2.8 Animation-relevant sampling. The evidence request derives the
animatable binding names from the texture request's scale-offset kind.
The resolution machinery re-derives per-state answers under the
animation closure. This layer is family-blind and already serves both
frontends.

## 3. The Poiyomi 9.3.64 equivalent map

The Poiyomi frontend already covers a real core. The map below marks each
lilToon-supported behavior with its Poiyomi equivalent and its status.
Observed diagnostics come from the committed code, its tests, and the live
probe of 2026-09-21.

| # | lilToon behavior | Poiyomi 9.3.64 equivalent | Status | Observed gap |
|---|---|---|---|---|
| 1 | Main alpha sample times color alpha | `_MainTex.a * _Color.a` at `_MainTexUV` with own scale-offset, source line 29780 | supported | none. `PoiyomiBaseColorAlphaTests` (`NonForced_MainTexFullColorAlpha_IsTextureAlpha`, `NonForced_MainTexPartialColorAlpha_IsTextureTimesConstant`) |
| 2 | Alpha channel importer theorem | Same shared texture evidence. Alpha claims need no color import | supported | none |
| 3 | Alpha mask replace, assigned mask, admitted pair | `_MainAlphaMaskMode` 1, `_AlphaMask.red` through the main sampler, own `_AlphaMask_ST`, `_AlphaMaskUV` channel, `_AlphaMaskPan`, strength, value, invert, source lines 29864 to 29876 | unsupported | `UnsupportedFeature: _AlphaMask` on any bound mask. The evidence request asks assignment only (`TextureEvidenceKinds.None`) |
| 4 | Alpha mask multiply, assigned mask | `_MainAlphaMaskMode` 2, same mask term | unsupported | `UnsupportedFeature: _MainAlphaMaskMode` for every mode except 0 and 1 |
| 5 | Alpha mask with unbound default as a proven constant | Unbound `_AlphaMask` under mode 1 collapses to a constant from strength, value, and invert | supported | none. `PoiyomiAlphaMaskTests` (`ReplaceNoMask_*`) |
| 6 | Mask mode default inertness (lilToon default is off) | The 9.3.64 declared default of `_MainAlphaMaskMode` is 2 (multiply), not off, source line 75 | unsupported as a consequence of row 4 | A material that never touched the mask section refuses with `UnsupportedFeature: _MainAlphaMaskMode` |
| 7 | Mask UV channel selection and panning | `_AlphaMaskUV` selects UV0 to UV3. `_AlphaMaskPan` pans over time. The mask has no separate scale-offset property. The `_AlphaMask_ST` rides the texture assignment | unsupported | Not read as of 2026-09-21. The frontend reads neither `_AlphaMaskUV` nor `_AlphaMaskPan` |
| 8 | Cutout coverage binarization by cutoff | `clip(alpha - _Cutoff)` in every pass, then cutout-mode binarization `alpha = 1`, source lines 30396 to 30401 | partially supported | The alpha equation never declares the cutoff. A cutout triangle with chain between cutoff and 1 stays unproven. Conservative, but a coverage defect against the lilToon cutout parity |
| 9 | Plain clip transparent handling with cutoff bound | Fade and transparent presets blend with standard or premultiply pairs, both inside the proven-opaque blend set | supported | none for the blend pairs. `PoiyomiMultipassRuleTests` |
| 10 | ForwardAdd premultiply identity bound | The additive pass reads `_Add*` blend factors. The alpha boost has no equivalent property | supported | The blend-pair gate covers the additive pass. `IsProvenOpaqueBlend` |
| 11 | Subpass shadow clip and distance fade bounds | No subpass shadow clip. `_AlphaDistanceFade`, `_AlphaFresnel`, `_AlphaAngular`, `_AlphaMod`, `_AlphaGlobalMask`, audio link alpha are gated off exactly | supported (refusal parity) | none. The gate list covers them (`AlphaFeatureGates`) |
| 12 | Coverage mechanisms | `_AlphaToCoverage`, `_AlphaSharpenedA2C`, `_AlphaDithering`, `_EnableDissolve`, `_EnableUDIMDiscardOptions` gated off exactly | supported | none. `AlphaCoverageGateEnabled_IsUnsupportedFeature` |
| 13 | Render-state and queue eligibility | The conversion gates the mode preset state, outlines, premultiply, A2C, depth comparison, blend pairs, additive factors, and the cutoff bound. Queue and RenderType normalize through the canonical recipe | supported | The depth-test policy admits `Less` with disclosure, as on lilToon |
| 14 | Depth-test policy mixed split | Same policy value lives in `PoiyomiOpaqueConversion` gate 7 | supported | `DepthTestDivergence` carried identically |
| 15 | Animation-relevant sampling | Family-blind machinery already serves the Poiyomi family through `ClassifyShaderName` | supported | none |
| 16 | Two Pass second family | The frontend attests the Two Pass identity and gates its second blend pair | partially supported | The unlocked Two Pass name never reaches the frontend. See section 7.1 |

## 4. Verified vendor mechanism facts, 2026-09-21

Each fact below was read in the attested 9.3.64 source on this date. The
anchors cite that source.

4.1 The alpha chain starts as `mainTexture.a * _Color.a` (line 29780).
The Two Pass second base uses `_TwoPassColor.a` (line 29785).

4.2 The mask block samples `_AlphaMask.red` through the `_MainTex`
sampler at `uv[_AlphaMaskUV]` transformed by `_AlphaMask_ST`, panned by
`_AlphaMaskPan.xy` (line 29865). The mask term is
`saturate(r * _AlphaMaskBlendStrength + (invert ? -_AlphaMaskValue :
_AlphaMaskValue))`, followed by `1 - m` when invert holds (lines 29867 to
29868). The mode map is Off 0, Replace 1, Multiply 2, Add 3, Subtract 4
(property declaration line 75 and branch lines 29873 to 29876). This is
the same algebra the lilToon mask term proves, with two additions: the
channel selector and the pan, and two differences: the strength-value
pair has no admitted-pair restriction in the vendor source, and the
declared default mode is 2, not 0.

4.3 `_AlphaPremultiply` scales the base color by
`saturate(alpha)` in three passes (lines 30216, 47274, 58331). The alpha
value itself is never modified. At alpha exactly 1 the factor is exactly
1, so the feature is an identity exactly on the triangle set the proof
already admits.

4.4 `_AlphaForceOpaque` forces alpha to 1 before coverage and clip (line
30366). The Two Pass shader declares a separate `_AlphaForceOpaque2` for
its second family (Two Pass source, line 862), and the second family
binarizes cutout through `_ModeTwoPass` (Two Pass source, line 68110).

4.5 The clip `clip(alpha - _Cutoff)` runs unconditionally in every pass,
then cutout mode forces alpha to 1 (lines 30396 to 30401). This is the
binarization the lilToon cutout family models by declaring the cutoff in
the capture.

4.6 The `_Mode` presets map to queue, `RenderType`, blend pairs, and
cutoff values exactly as the conversion recipe records (source lines 46
to 54). The fixture's preset 3 (Fade) pairs `SrcBlend 1, DstBlend 10`,
which the proven-opaque blend set already accepts.

4.7 The BeatSaber module toggle `_BSSEnabled` (line 4517) enables a
post-clip alpha writer `alpha = alpha * emission.z` and rewrites the
alpha blend pair. The frontend never reads this property.

4.8 The declared 9.3.64 property table confirms every anchor of the task:
`_AlphaMask`, `_AlphaMaskUV`, `_AlphaMaskPan`, `_MainAlphaMaskMode`,
`_AlphaMaskBlendStrength`, `_AlphaMaskValue`, `_AlphaMaskInvert`,
`_Cutoff`, `_MainTex`, `_AlphaPremultiply`, plus `_AlphaForceOpaque`,
`_MainIgnoreTexAlpha`, and `_RgbAlphaMaskChannel`. The mask has no
separate scale-offset property. Unity derives `_AlphaMask_ST` from the
texture assignment, so the existing scale-offset evidence kind covers it.

No fact in this section contradicts the 2026-09-20 spec section 3 or the
2026-09-19 characterization.

## 5. The fixture, observed live, 2026-09-21

The designated locked denim garment fixture carried the lock flag at 1
before the probe. The unlocked clone reported these alpha-relevant facts:
mask mode 1 (Replace) with strength 1, value 0, invert 0, UV 0, zero pan,
and a bound mask texture. Cutoff 0.001. Premultiply on. Detail feature
on. Mode 3 (Fade) with blend pair 1 and 10, depth write on, depth
comparison Less, outlines off, no coverage or alpha-writer feature on,
main texture bound at UV0 with zero pan, color alpha 1.

The production frontend answered: material supported, alpha incomplete,
with `UnsupportedFeature: _AlphaPremultiply` on the alpha output and
`UnsupportedFeature: _DetailEnabled` on the other outputs. This matches
section 12.2 of the thry-pin recording.

Consequence. Two gaps block this fixture's alpha proof, in order: the
premultiply gate, then the bound-mask replace gate. The depth comparison
Less is not a blocker under the policy. The detail feature does not block
the alpha split, because the slot consumes the alpha output only.

## 6. The gaps, their missing pieces, and the smallest honest slice

Each row names what a slice must add across the layers the repository
already has: evidence request, frontend math, texture identity, capture,
classifier expectations, report strings, census rows, and falsifier
candidates.

6.1 Gap A: `_AlphaPremultiply` refused in the alpha equation.

Missing pieces. The alpha equation's `AlphaFeatureGates` names
`_AlphaPremultiply`. The conversion eligibility gate 5 refuses it by
name. The alpha value is untouched by the feature, so the alpha equation
needs a proof-shaped admission, not new evidence: the feature is an
identity exactly where the per-triangle premise alpha equals 1 holds,
which the classifier already establishes before any triangle moves. The
conversion gate needs the owner's decision, because its comment declares
the opposite premise. Report strings and census rows need no change,
because no new refusal member appears.

Smallest honest slice. Remove `_AlphaPremultiply` from the alpha feature
gates with a proof comment pinned to the vendor sites. Keep the
conversion gate closed in the same slice. RED: a premultiply material
with a proven exactly-one domain completes alpha and a premultiply
material with a sub-one region stays unknown. Falsifier candidates: a
premultiply material never proves a triangle whose domain is not exactly
one, and the alpha boost of the additive passes stays covered by the
blend-pair rule.

6.2 Gap B: the conversion gate 5 premise.

This is the contract-touching decision. The gate's comment says
premultiplication changes how RGB is produced, so the blend predicate
cannot excuse it. The vendor source shows the production is
`saturate(alpha)`, which is exactly 1 on the proof's premise. Closing Gap
A for conversion, not just analysis, therefore revises a declared
conversion premise. Section 17 of the 2026-09-18 investigation is the
precedent shape: a stated, disclosed divergence with a report sentence.
This slice lands only after the owner accepts the revision. Until then
the shipped behavior stays conservative.

Smallest honest slice, after acceptance. Admit premultiply in
`PoiyomiOpaqueConversion` gate 5 under the policy, carry the disclosure
through the existing divergence report, and pin falsifiers: a premultiply
conversion with any unproven triangle stays refused, and the disclosure
fires once per prepared slot.

6.3 Gap C: the bound alpha mask in Replace mode.

Missing pieces. All of them exist on the lilToon side and none on the
Poiyomi side. The evidence request must ask scale-offset, source
identity, and the red channel for `_AlphaMask`. The frontend must gate
`_AlphaMaskUV` to an exact integer 0 to 3, gate `_AlphaMaskPan` to exact
zero, read the mask scale-offset as a plain affine, and admit the
strength-value pair (1, 0) and the saturated pair (1, at least 1), plus
the unbound default. The invert transform needs a value shape. With
invert off the replace term is the red sample. With invert on it is
`saturate(1 - r)`, which the existing saturating-difference shape
expresses. Other strength-value pairs stay refused under the deferred
threshold-envelope contract, exactly as lilToon refuses them. Texture
identity, capture, and the classifier need no change, because the red
field route already serves the lilToon mask. Report strings and census
rows need no change. Falsifier candidates: a mask hole keeps its
triangles unknown or transparent, a wrap-seam-adjacent domain stays
unproven under the wrap-blend rule, and a non-identity mask ST still
proves, as the lilToon test `AlphaMaskMode2_WithNonIdentityMaskSt_ProvesTriangle`
pins.

Smallest honest slice. Replace mode only, invert included, pair (1, 0)
admitted. This is the slice that unblocks the fixture together with Gap
A.

6.4 Gap D: Multiply mode and the declared default.

Missing pieces. Mode 2 with an unbound mask and the pair (1, 0) leaves
the chain unchanged, which is the `MainUnchanged` outcome the lilToon
mask term already has. Mode 2 with a bound mask and an admitted pair
folds the red sample into the running chain through the existing exact
product machinery. Modes 3 and 4 saturate a sum or difference and refuse,
the same refusal lilToon makes. The declared default of 2 makes this
slice the highest-reach conservative win after Gap C.

Smallest honest slice. Admit mode 2 for the unbound (1, 0) case and the
bound admitted-pair case. RED: a fresh default-shaped material completes
alpha, and a bound hole in multiply mode classifies the hole unknown.

6.5 Gap E: mask UV channel and pan.

Covered inside Gap C's gates. Recorded as its own row because the
inventory task names them: the frontend did not read `_AlphaMaskUV` or
`_AlphaMaskPan` as of 2026-09-21. The pan gate is exact zero. The channel
gate is an exact integer.

6.6 Gap F: cutout binarization by cutoff.

Missing pieces. The alpha capture must declare the cutoff for cutout-mode
materials the way the lilToon cutout request does, and the interpretation
must switch the field to the binarized route. The conversion already
gates the cutoff at most 1. The classification layer needs no change,
because the binarized route exists. Falsifier candidates: a cutout
triangle with chain at or above the cutoff proves, and the same triangle
under transparent mode stays subject to the exact-one rule.

Smallest honest slice. One follow-up slice after Gaps A to D. It is
larger than the others because it touches the capture request shape per
mode preset.

## 7. New findings from this investigation

7.1 The Two Pass identity is attested but unrouted. `ClassifyShaderName`
in `UnityMaterialSemantics.cs` routes only the plain Toon name to the
Poiyomi family. `TryVerifyPoiyomiIdentity` attests the Two Pass
generated shader with its own pins, and the unlock precondition accepts a
Two Pass original. A locked Two Pass material therefore opens the window,
swaps in the restored clone, and the capture then classifies the
unlocked name as unsupported. The slot refuses as semantics-unknown and
the pair re-locks unchanged. The shipped behavior is conservative, but
the S10 identity never runs in production. Date of finding: 2026-09-21.

Smallest honest slice. Add the Two Pass name to the routing branch, then
gate the second family: read `_AlphaForceOpaque2` beside
`_AlphaForceOpaque`, and gate `_ModeTwoPass` cutout binarization inside
Gap F. Falsifier candidates: a Two Pass material with force-opaque on
the first family and off on the second never completes alpha, and a
plain Toon material still captures without the second-family scalars.

7.2 The BeatSaber module toggle is ungated. `_BSSEnabled` enables a
post-clip alpha writer and rewrites the alpha blend pair. The frontend
never reads it, so an enabled module can sit inside a claimed
exactly-one alpha. This is the aggressive direction, unlike every other
finding in this note. Date of finding: 2026-09-21.

Smallest honest slice. Add `_BSSEnabled` to the alpha coverage gates and
to the evidence request. RED: an enabled module keeps alpha unknown and
names the property. This slice is independent of every other gap and is
a correctness fix, not a parity extension.

7.3 The declared default mask mode is Multiply, not Off. A material that
never touched the mask section carries mode 2 and refused on 2026-09-21.
This fact raises the reach of Gap D and is recorded in row 6 of the map.

## 8. Prioritized roadmap

Ordered by user value and risk. Each slice is sized to one future
implementation prompt with its own RED and GREEN obligations.

1. BeatSaber gate. Tiny, correctness-first, closes an over-claim
   direction. No contract decision needed. Section 7.2.
2. Premultiply alpha admission. Unblocks the fixture's alpha analysis
   path. No contract change, because the alpha value is untouched and
   the per-triangle premise already guards every moved triangle.
   Section 6.1.
3. Mask replace with the admitted pair and invert. Unblocks the fixture
   together with slice 2, mirrors proven lilToon machinery, low risk.
   Section 6.3.
4. Mask multiply, including the declared default. Highest reach after
   slice 3, because the default mode is multiply. Section 6.4.
5. Two Pass routing plus second-family gates. Conservative as of
   2026-09-21, so it is a coverage extension, not a fix. Section 7.1.
6. Cutout binarization. Coverage defect closure, larger surface.
   Section 6.6.
7. Premultiply conversion admission. Blocked on the owner's decision
   about the declared gate premise. Section 6.2.

## 9. Open questions and stop-condition hits

9.1 Stop-condition hit, bounded to slice 7. Closing the conversion gate
5 revises its declared premise. The exact proof rule and the alpha
policy stay untouched, so this is the section 17 shape and not a proof
rule change. The owner decides. The evidence is preserved in sections
4.3 and 6.2.

9.2 Open question. The threshold-envelope contract for other
strength-value pairs stays future work on both frontends. This
investigation does not widen it.

9.3 Open question. Whether the embedded era 1 tooling contains an auto
recovery path that unlocks materials whose generated asset was deleted
remains open, as recorded in the thry-pin note, section 4. The fixture
lock flag was verified intact before this session's probe.

9.4 Open question. `_RgbAlphaMaskChannel` exists in the property table.
The mask equation always samples red in the attested source, so the
property has no alpha-relevant role on this path. Recorded so a later
session does not re-derive it.

9.5 Scope note. The detail feature blocks the fixture's base color,
emission, and normal outputs. Those outputs are outside this parity
boundary, and the slot consumes the alpha output only, so the fixture's
split does not depend on them.

## 10. Validation of this session

No repository test ran, because this is a docs-only session on a branch
without production changes. The live probe of section 5 is the observed
evidence. The repository facts carry their own file and test references.
The temp asset the probe created was deleted inside the same handler,
and the fixture stayed locked afterwards. No dialog appeared and no
build ran.
