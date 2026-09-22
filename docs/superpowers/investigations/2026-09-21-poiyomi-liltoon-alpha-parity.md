# Poiyomi equivalents of the supported lilToon alpha features - parity map

## Privacy note

This record starts with a privacy statement. It describes private Census
Lab fixtures by role only. It names no avatar, renderer, material,
texture, mesh, hierarchy, scene, or folder. It records no machine paths,
no host names, no ports, and no instance names or hashes. It records no
GUIDs (a GUID is an asset identifier inside Unity). The designated locked
denim garment fixture appears by role. Its alpha facts appear as feature
values, never as a per-slot table. Every status claim carries its date.
Property names, mode numbers, and shader line numbers are public facts of
the Poiyomi Toon 9.3.64 shader source. They are not private data.

## 1. Scope and method

Date: 2026-09-21. This is a stage 0 investigation. It changes no
production code and writes no production test.

The question is simple. AMUSE already understands the alpha features of
lilToon materials. What does it need for the same features on Poiyomi
materials?

Trigger. On 2026-09-21 the designated locked denim garment fixture
reported `amuse.renderer.AdmittedMaterialSemanticsUnknown`. The probe in
section 12.2 of the thry-pin recording found the cause. The Poiyomi
frontend accepted the unlocked material but did not prove its alpha. It
answered `UnsupportedFeature: _AlphaPremultiply`.

Terms used in this record:

- Alpha: the see-through amount of one pixel. One means fully solid.
- Proof: a claim shown exactly from captured facts, never guessed.
- Frontend: the code that reads one shader family and answers what a
  material does.
- Diagnostic: a named reason that records what the frontend did not
  prove.
- Mask: an image that controls alpha per pixel.
- Cutoff: a threshold. The shader discards a pixel below it.
- Premultiply: multiply the color of a pixel by its alpha.
- Attestation: the check that a shader source matches pinned name,
  version, and hash facts.
- Lock: the reversible packaging step of the Thry editor tool. It hides
  the full property table of a material.
- Unlock window: the span of an AMUSE build in which locked materials
  appear in unlocked form.
- Canonical opaque material: the fully solid material that AMUSE
  generates for proven triangles.
- Evidence request: the exact list of material facts that one capture
  reads.
- Falsifier: a test case that a wrong implementation must fail.
- Render state: the fixed setup of one draw call. It covers blend, depth
  test, and queue order.
- Blend pair: the two numbers that control how a pixel combines with the
  pixels behind it.
- Queue: the render order number of a material. Lower numbers draw
  first.
- Texel: one image cell of a texture.
- Census: the research package record of build outcomes.

Method. Three evidence layers.

1. The lilToon inventory comes from the committed frontend code and its
   tests. No lilToon fact in this record comes from memory.
2. The Poiyomi audit comes from the committed
   `PoiyomiMaterialSemantics.cs` and `PoiyomiOpaqueConversion.cs` and
   their tests.
3. The vendor facts come from the attested Poiyomi Toon 9.3.64 shader
   source. The Census Lab project has the package installed. Line
   anchors below cite that source. The source hashes stay pinned in the
   repository. This record does not repeat them.

The live probe. One probe ran against the Census Lab editor instance on
2026-09-21. The instance identity check passed first. The probe cloned
the locked fixture, saved the clone as a temporary asset, unlocked the
clone with the embedded era 1 tool, and called the production frontend
entry `PoiyomiMaterialSemantics.AnalyzeBaseMaterial`. It then read the
alpha-relevant property values and deleted the temporary asset. The
probe obeyed the transport discipline. A self-removing update handler
wrote the result into session state, and separate calls polled it. The
probe saw no dialog and left no asset behind. The fixture stayed locked.

The probe result matched section 12.2 exactly. It also recorded the
property facts of the fixture. Section 5 lists them.

## 2. The authoritative lilToon supported-feature inventory

The lilToon side already supports these alpha and transparency
behaviors. Each row names its files and tests. This inventory is the
source of truth for the parity question.

2.1 Base alpha sources. The cutout and transparent frontends prove this
chain: the alpha of the `_MainTex` sample at UV0, times `_Color.a`. A
texture without an alpha channel collapses to the constant `_Color.a`.
The importer theorem guarantees that sample. Files:
`LilToonCutoutMaterialSemantics.cs`,
`LilToonTransparentMaterialSemantics.cs`. Tests:
`LilToonCutoutAlphaTests` (`GateOffMaterial_CompletesAPlainTexturedAlphaTerm`,
`UnassignedMainTex_IsRefusedAndNeverBecomesAConstant`),
`LilToonTransparentAlphaTests` (`NoAlphaSourceImport_CollapsesTheTextureArmToTheTintConstant`,
`ColorAlphaBelowOne_YieldsUniformMustRemainTransparent`).

2.2 Alpha mask modes and blend strengths. The shared mask term
(`LilToonAlphaMaskSemantics.cs`) proves one fixed equation. The source
equation lives in `lil_common_frag.hlsl`. The frontend samples
`_AlphaMask.red` through the `_MainTex` sampler at `uvMain` transformed
by the mask's own `_ST`. It then computes
`saturate(r * _AlphaMaskScale + _AlphaMaskValue)`. Mode 1 replaces the
alpha with the mask. Mode 2 multiplies the running chain with the mask,
through the exact product machinery. Three parameter pairs pass. The
vendor default (scale 1, value 0) passes. The saturated pair (scale 1,
value 1 or higher) passes. The unassigned white default passes, and its
term is a proven constant. Mode 2 with a bound mask and scale 1 passes.
Every other pair refuses, and the refusal names the property. Tests:
`LilToonCutoutAlphaTests` and `LilToonTransparentAlphaTests`
(`AlphaMaskMode1_WithAllWhiteMask_ProvesTriangleDespiteSubUnitColorAlpha`,
`AlphaMaskMode2_WithAllWhiteMask_ProvesTriangle`,
`AlphaMaskMode2_WithMaskHole_ClassifiesTheHoleUnknown`,
`AlphaMaskMode2_WithNonIdentityMaskSt_ProvesTriangle`,
`AlphaMaskMode2_WithScaleNotOneWithAssignedMask_RefusesNamingScale`,
`AlphaMaskUnassigned_Mode2_Scale1Value0_ProvesTriangle`).

2.3 Cutoff and cutout handling. The cutout family declares the cutoff in
the main texture evidence request. Every capture route then splits each
texel into above-cutoff and below-cutoff, and the classification layer
refuses above the twice-margin bound 0.9999. The transparent family
works differently. Its clip rule keeps every finite cutoff of 1 or
below. Files: `LilToonCutoutSourceEligibility.cs` (bound 0.9999, queue
2450), `LilToonTransparentSourceEligibility.cs` (bound 1, queue 2460 or
the Transparent bucket 3000 to 3099). Tests:
`CutoffAtTwiceMargin_CompletesAndProvesCornerTriangle`,
`CutoffAboveTwiceMargin_IsUnknownNamingCutoff`,
`CutoffAtOrBelowOne_ProvesTheCornerTriangle`,
`CutoffAboveOneOrNonFinite_IsUnknownNamingCutoff`.

2.4 Mask and main UV modes and panning. The main UV is UV0 only.
`_MainTex_ST` and `_MainTex_ScrollRotate` must sit at exact identity at
the family boundary. The mask coordinate is uvMain under those gates,
transformed by the mask's own plain affine. Any finite scale and offset
pass there. Non-identity main ST refuses. Tests:
`NonIdentityMainTexStIsRejectedAtTheCutoutAlphaBoundary`,
`EveryNearIdentityNonIdentityMainTexStComponentIsRejectedExactly`,
`EveryNearZeroMainTexScrollRotateComponentIsRejectedExactly`,
`NonZeroScrollRotateComponent_IsUnknownNamingScrollRotate`.

2.5 Layer alpha. The second and third main layers compose onto the
chain by their alpha modes. The four modes are replace, multiply,
saturating sum, and saturating difference. The stage A admission
requires UV0, exact identity scroll, rotate, and angle, every decal flag
off, and the layer dissolve at mode zero. File:
`LilToonLayerAlphaTerm.cs`. Tests:
`AddModeLayer_WithOpaqueChain_ProvesThroughTheDisjunction`,
`MultiplyModeLayer_WithSubUnitTint_IsUniformlyTransparent`,
`SubtractModeLayer_WithZeroTint_PreservesTheMainChain`,
`ReplaceModeLayer_DiscardsTheMainChain`.

2.6 Render-state and queue handling. The conversion eligibility gates
the queue, `RenderType`, depth comparison, depth write, color mask,
depth offset, both blend equations, the ForwardAdd blend, the clip
threshold, the ForwardAdd alpha boost, distance fade, and the subpass
shadow clip. The ForwardAdd premultiply needs a proof of its own. It is
an identity only at an alpha boost of 1 or more, and the boost gate
enforces that bound. Files: `LilToonTransparentSourceEligibility.cs`
(gates 3 to 15), `LilToonCutoutSourceEligibility.cs`. Tests:
`LilToonTransparentSourceEligibilityTests`,
`LilToonCutoutSourceEligibilityTests`,
`AlphaBoostFaBelowOne_IsUnknownNamingTheProperty`,
`SubpassCutoffJustAboveOne_IsUnknownNamingTheProperty`,
`DistanceFadeEnabled_IsUnknownNamingDistanceFade`.

2.7 The depth-test policy slice. The policy accepts a `Less` depth
source under an opt-in choice. The material then converts on a wholly
opaque plan or on a mixed split, with a stated and disclosed
divergence. The proof rule itself does not change. The wrap-blend rule
of the 2026-09-18 two-pass investigation, section 13, stays exact. That
note also records the policy slice with live counts, in sections 16 and
17.

2.8 Animation-relevant sampling. The evidence request derives the
animatable binding names from the texture request's scale-offset kind.
The resolution machinery re-derives per-state answers under the
animation closure. This layer serves both shader families already.

## 3. The Poiyomi 9.3.64 equivalent map

The Poiyomi frontend already covers a real core. The map below marks
each lilToon behavior with its Poiyomi equivalent and its status. The
observed gaps come from the committed code, its tests, and the live
probe of 2026-09-21.

| # | lilToon behavior | Poiyomi 9.3.64 equivalent | Status | Observed gap |
|---|---|---|---|---|
| 1 | Main alpha sample times color alpha | `_MainTex.a * _Color.a` at `_MainTexUV` with own scale-offset, source line 29780 | supported | none. `PoiyomiBaseColorAlphaTests` (`NonForced_MainTexFullColorAlpha_IsTextureAlpha`, `NonForced_MainTexPartialColorAlpha_IsTextureTimesConstant`) |
| 2 | Alpha channel importer theorem | Same shared texture evidence. Alpha claims need no color import | supported | none |
| 3 | Alpha mask replace, bound mask, admitted pair | `_MainAlphaMaskMode` 1, `_AlphaMask.red` through the main sampler, own `_AlphaMask_ST`, `_AlphaMaskUV` channel, `_AlphaMaskPan`, strength, value, invert, source lines 29864 to 29876 | unsupported | `UnsupportedFeature: _AlphaMask` on any bound mask. The evidence request asks for the assignment fact only (`TextureEvidenceKinds.None`) |
| 4 | Alpha mask multiply, bound mask | `_MainAlphaMaskMode` 2, same mask term | unsupported | `UnsupportedFeature: _MainAlphaMaskMode` for every mode except 0 and 1 |
| 5 | Alpha mask with unbound default as a proven constant | Unbound `_AlphaMask` under mode 1 collapses to a constant from strength, value, and invert | supported | none. `PoiyomiAlphaMaskTests` (`ReplaceNoMask_*`) |
| 6 | Mask mode default inertness (the lilToon default is off) | The 9.3.64 declared default of `_MainAlphaMaskMode` is 2 (multiply), not off, source line 75 | unsupported, as a result of row 4 | A material that never touched the mask section refuses with `UnsupportedFeature: _MainAlphaMaskMode` |
| 7 | Mask UV channel selection and panning | `_AlphaMaskUV` selects UV0 to UV3. `_AlphaMaskPan` pans over time. The mask has no separate scale-offset property. The `_AlphaMask_ST` rides the texture assignment | unsupported | Not read as of 2026-09-21. The frontend reads neither `_AlphaMaskUV` nor `_AlphaMaskPan` |
| 8 | Cutout coverage split by cutoff | `clip(alpha - _Cutoff)` in every pass, then cutout-mode binarization `alpha = 1`, source lines 30396 to 30401 | partially supported | The alpha equation never declares the cutoff. A cutout triangle with chain between cutoff and 1 stays unproven. This is safe, but it is a coverage gap against the lilToon cutout behavior |
| 9 | Plain clip transparent handling with a cutoff bound | Fade and transparent presets blend with standard or premultiply pairs. Both pairs sit inside the proven-opaque blend set | supported | none for the blend pairs. `PoiyomiMultipassRuleTests` |
| 10 | ForwardAdd premultiply identity bound | The additive pass reads the `_Add*` blend factors. The alpha boost has no equivalent property | supported | The blend-pair gate covers the additive pass. `IsProvenOpaqueBlend` |
| 11 | Subpass shadow clip and distance fade bounds | No subpass shadow clip exists. `_AlphaDistanceFade`, `_AlphaFresnel`, `_AlphaAngular`, `_AlphaMod`, `_AlphaGlobalMask`, and audio link alpha are gated off exactly | supported, same refusals | none. The gate list covers them (`AlphaFeatureGates`) |
| 12 | Coverage mechanisms | `_AlphaToCoverage`, `_AlphaSharpenedA2C`, `_AlphaDithering`, `_EnableDissolve`, and `_EnableUDIMDiscardOptions` are gated off exactly | supported | none. `AlphaCoverageGateEnabled_IsUnsupportedFeature` |
| 13 | Render-state and queue eligibility | The conversion gates the mode preset state, outlines, premultiply, A2C, depth comparison, blend pairs, additive factors, and the cutoff bound. Queue and `RenderType` normalize through the canonical recipe | supported | The depth-test policy accepts `Less` with disclosure, as on lilToon |
| 14 | Depth-test policy mixed split | The same policy value lives in `PoiyomiOpaqueConversion` gate 7 | supported | `DepthTestDivergence` is carried identically |
| 15 | Animation-relevant sampling | The shared machinery already serves the Poiyomi family through `ClassifyShaderName` | supported | none |
| 16 | Two Pass second family | The frontend attests the Two Pass identity and gates its second blend pair | partially supported | The unlocked Two Pass name never reaches the frontend. See section 7.1 |

## 4. Verified vendor mechanism facts, 2026-09-21

Each fact below was read in the attested 9.3.64 source on 2026-09-21.
The anchors cite that source.

4.1 The alpha chain starts as `mainTexture.a * _Color.a` (line 29780).
The Two Pass second base uses `_TwoPassColor.a` (line 29785).

4.2 The mask block samples `_AlphaMask.red` through the `_MainTex`
sampler at `uv[_AlphaMaskUV]` transformed by `_AlphaMask_ST`, panned by
`_AlphaMaskPan.xy` (line 29865). The mask term is
`saturate(r * _AlphaMaskBlendStrength + (invert ? -_AlphaMaskValue :
_AlphaMaskValue))`, followed by `1 - m` when invert holds (lines 29867
to 29868). The mode map is Off 0, Replace 1, Multiply 2, Add 3,
Subtract 4 (property declaration line 75 and branch lines 29873 to
29876). The algebra matches the lilToon mask term. Two additions exist:
the channel selector and the pan. Two differences exist: the vendor
source restricts no strength-value pair, and the declared default mode
is 2, not 0.

4.3 `_AlphaPremultiply` scales the base color by `saturate(alpha)` in
three passes (lines 30216, 47274, 58331). The alpha value itself never
changes. At alpha exactly 1 the factor is exactly 1. The feature is
therefore an identity on the triangle set that the proof already
accepts.

4.4 `_AlphaForceOpaque` forces alpha to 1 before coverage and clip
(line 30366). The Two Pass shader declares a separate
`_AlphaForceOpaque2` for its second family (Two Pass source, line 862).
The second family splits cutout coverage through `_ModeTwoPass` (Two
Pass source, line 68110).

4.5 The clip `clip(alpha - _Cutoff)` runs in every pass, without
condition. Cutout mode then forces alpha to 1 (lines 30396 to 30401).
This is the split that the lilToon cutout family models. It declares
the cutoff in the capture.

4.6 The `_Mode` presets map to queue, `RenderType`, blend pairs, and
cutoff values. The conversion recipe records exactly this map (source
lines 46 to 54). The fixture uses preset 3 (Fade). Its blend pair is 1
and 10. The proven-opaque blend set already accepts that pair.

4.7 The BeatSaber module toggle `_BSSEnabled` (line 4517) turns on a
post-clip alpha writer. The writer computes
`alpha = alpha * emission.z`. The toggle also rewrites the alpha blend
pair. The frontend never reads this property.

4.8 The declared 9.3.64 property table confirms every anchor of the
task: `_AlphaMask`, `_AlphaMaskUV`, `_AlphaMaskPan`,
`_MainAlphaMaskMode`, `_AlphaMaskBlendStrength`, `_AlphaMaskValue`,
`_AlphaMaskInvert`, `_Cutoff`, `_MainTex`, `_AlphaPremultiply`, plus
`_AlphaForceOpaque`, `_MainIgnoreTexAlpha`, and
`_RgbAlphaMaskChannel`. The mask has no separate scale-offset property.
Unity derives `_AlphaMask_ST` from the texture assignment. The existing
scale-offset evidence kind therefore covers it.

No fact in this section contradicts the 2026-09-20 spec section 3 or
the 2026-09-19 characterization.

## 5. The fixture, observed live, 2026-09-21

The designated locked denim garment fixture carried the lock flag at 1
before the probe. The unlocked clone reported these alpha-relevant
facts. Mask mode 1 (Replace) with strength 1, value 0, invert 0, UV 0,
zero pan, and a bound mask texture. Cutoff 0.001. Premultiply on.
Detail feature on. Mode 3 (Fade) with blend pair 1 and 10. Depth write
on. Depth comparison Less. Outlines off. No coverage or alpha-writer
feature on. Main texture bound at UV0 with zero pan. Color alpha 1.

The production frontend answered: material supported, alpha incomplete.
The alpha output carried `UnsupportedFeature: _AlphaPremultiply`. The
other outputs carried `UnsupportedFeature: _DetailEnabled`. This
matches section 12.2 of the thry-pin recording.

Consequence. Two gaps block the alpha proof of this fixture, in this
order: the premultiply gate, then the bound-mask replace gate. The
depth comparison Less is not a blocker under the policy. The detail
feature does not block the alpha split either. The slot consumes the
alpha output only.

## 6. The gaps, their missing pieces, and the smallest honest slice

Each row below names the pieces that a slice must add across the layers
the repository already has. The layers are the evidence request, the
frontend math, texture identity, capture, classifier expectations,
report strings, census rows, and falsifier candidates.

6.1 Gap A: `_AlphaPremultiply` is refused in the alpha equation.

The gap needs one admission, not new evidence. The alpha equation lists
`_AlphaPremultiply` in its `AlphaFeatureGates`. The conversion
eligibility refuses it again in gate 5. The vendor facts show that the
feature never touches the alpha value. It only scales the color. On the
proof's own premise, alpha equals 1 on every triangle that moves. The
feature is an identity exactly there. The classifier already
establishes that premise before any triangle moves. Report strings and
census rows need no change, because no new refusal member appears.

The smallest honest slice removes `_AlphaPremultiply` from the alpha
feature gates, with a proof comment pinned to the vendor sites. The
same slice keeps the conversion gate closed. RED: a premultiply
material with a proven exactly-one domain completes alpha, and a
premultiply material with a sub-one region stays unknown. Falsifier
candidates: a premultiply material never proves a triangle whose domain
is not exactly one, and the blend-pair rule keeps covering the additive
passes.

6.2 Gap B: the conversion gate 5 premise.

This is the contract-touching decision. The gate comment says that
premultiplication changes how the color is produced, so the blend
predicate cannot excuse it. The vendor source shows the exact
production step. It is `saturate(alpha)`, and that value is exactly 1
on the proof's premise. Closing Gap A for conversion therefore revises
a declared conversion premise. Section 17 of the 2026-09-18
investigation set the shape for this kind of change. It recorded a
stated and disclosed divergence with a report sentence. This slice
lands only after the owner accepts the revision. Until then the shipped
behavior stays safe.

The smallest honest slice, after acceptance: admit premultiply in
`PoiyomiOpaqueConversion` gate 5 under the policy. Carry the disclosure
through the existing divergence report. Pin two falsifiers: a
premultiply conversion with any unproven triangle stays refused, and
the disclosure fires once per prepared slot.

6.3 Gap C: the bound alpha mask in Replace mode.

The pieces all exist on the lilToon side. None exist on the Poiyomi
side. The evidence request must ask for scale-offset, source identity,
and the red channel of `_AlphaMask`. The frontend must gate
`_AlphaMaskUV` to an exact integer 0 to 3. It must gate `_AlphaMaskPan`
to exact zero. It must read the mask scale-offset as a plain affine. It
must admit the strength-value pair (1, 0) and the saturated pair (1, 1
or higher), plus the unbound default. Every other strength-value pair
stays refused, under the deferred threshold-envelope contract, exactly
as lilToon refuses them. The invert transform needs a value shape. With
invert off, the replace term is the red sample. With invert on, it is
`saturate(1 - r)`. The existing saturating-difference shape expresses
that. Texture identity, capture, and the classifier need no change,
because the red field route already serves the lilToon mask. Report
strings and census rows need no change. Falsifier candidates: a mask
hole keeps its triangles unknown or transparent, a wrap-seam-adjacent
domain stays unproven under the wrap-blend rule, and a non-identity
mask ST still proves, as the lilToon test
`AlphaMaskMode2_WithNonIdentityMaskSt_ProvesTriangle` pins.

The smallest honest slice covers Replace mode only, invert included,
with the pair (1, 0) admitted. Together with Gap A, this slice unblocks
the fixture.

6.4 Gap D: Multiply mode and the declared default.

Mode 2 with an unbound mask and the pair (1, 0) leaves the chain
unchanged. The lilToon mask term already has that outcome. Its name is
`MainUnchanged`. Mode 2 with a bound mask and an admitted pair folds
the red sample into the running chain. The exact product machinery
already exists for that fold. Modes 3 and 4 saturate a sum or a
difference, so they refuse. lilToon refuses them for the same reason.
The declared default of 2 makes this slice the highest-reach safe win
after Gap C.

The smallest honest slice admits mode 2 for the unbound (1, 0) case and
for the bound admitted-pair case. RED: a fresh default-shaped material
completes alpha, and a bound hole in multiply mode classifies the hole
unknown.

6.5 Gap E: mask UV channel and pan.

Gap C's gates cover this row. It keeps its own row because the
inventory task names both properties. The frontend did not read
`_AlphaMaskUV` or `_AlphaMaskPan` as of 2026-09-21. The pan gate is
exact zero. The channel gate is an exact integer.

6.6 Gap F: cutout coverage split by cutoff.

The alpha capture must declare the cutoff for cutout-mode materials.
The lilToon cutout request shows the pattern. The interpretation must
then switch the field to the split route. The conversion already gates
the cutoff at 1 or below. The classification layer needs no change,
because the split route exists. Falsifier candidates: a cutout triangle
with chain at or above the cutoff proves, and the same triangle under
transparent mode stays subject to the exact-one rule.

The smallest honest slice is one follow-up slice after Gaps A to D. It
is larger than the others, because it touches the capture request shape
per mode preset.

## 7. New findings from this investigation

7.1 The Two Pass identity is attested but unrouted. `ClassifyShaderName`
in `UnityMaterialSemantics.cs` routes only the plain Toon name to the
Poiyomi family. `TryVerifyPoiyomiIdentity` attests the Two Pass
generated shader with its own pins. The unlock precondition accepts a
Two Pass original too. A locked Two Pass material therefore opens the
window, swaps in the restored clone, and the capture then classifies
the unlocked name as unsupported. The slot refuses as semantics-unknown
and the pair re-locks unchanged. The shipped behavior is safe. But the
S10 identity never runs in production. Date of finding: 2026-09-21.

The smallest honest slice adds the Two Pass name to the routing branch.
It then gates the second family. It reads `_AlphaForceOpaque2` beside
`_AlphaForceOpaque`, and it places the `_ModeTwoPass` cutout split
inside Gap F. Falsifier candidates: a Two Pass material with
force-opaque on the first family and off on the second never completes
alpha, and a plain Toon material still captures without the
second-family scalars.

7.2 The BeatSaber module toggle is ungated. `_BSSEnabled` turns on a
post-clip alpha writer and rewrites the alpha blend pair. The frontend
never reads it. An enabled module can therefore sit inside a claimed
exactly-one alpha. Every other finding in this note errs on the safe
side. This one does not. Date of finding: 2026-09-21.

The smallest honest slice adds `_BSSEnabled` to the alpha coverage
gates and to the evidence request. RED: an enabled module keeps alpha
unknown and names the property. This slice depends on no other gap. It
is a correctness fix, not a parity extension.

7.3 The declared default mask mode is Multiply, not Off. A material
that never touched the mask section carries mode 2. Such a material
refused on 2026-09-21. This fact raises the reach of Gap D. Row 6 of
the map records it.

## 8. Prioritized roadmap

The order below follows user value and risk. Each slice fits one future
implementation prompt with its own RED and GREEN obligations.

1. BeatSaber gate. Tiny and correctness-first. It closes an over-claim
   direction. No contract decision is needed. Section 7.2.
2. Premultiply alpha admission. It unblocks the alpha analysis path of
   the fixture. No contract changes, because the alpha value is
   untouched and the per-triangle premise guards every moved triangle.
   Section 6.1.
3. Mask replace with the admitted pair and invert. It unblocks the
   fixture together with slice 2. It mirrors proven lilToon machinery,
   so the risk is low. Section 6.3.
4. Mask multiply, with the declared default included. It has the
   highest reach after slice 3, because the default mode is multiply.
   Section 6.4.
5. Two Pass routing plus second-family gates. The behavior is safe as
   of 2026-09-21, so this is a coverage extension, not a fix.
   Section 7.1.
6. Cutout coverage split. It closes a coverage gap. The surface is
   larger. Section 6.6.
7. Premultiply conversion admission. It waits for the owner's decision
   about the declared gate premise. Section 6.2.

## 9. Open questions and stop-condition hits

9.1 Stop-condition hit, bounded to slice 7. Closing the conversion gate
5 revises its declared premise. The exact proof rule and the alpha
policy stay untouched. This is the section 17 shape, not a proof rule
change. The owner decides. Sections 4.3 and 6.2 preserve the evidence.

9.2 Open question. The threshold-envelope contract for other
strength-value pairs stays future work on both shader families. This
investigation does not widen it.

9.3 Open question. The thry-pin note, section 4, asks whether the
embedded era 1 tooling contains an auto recovery path. Such a path
unlocks a material whose generated asset was deleted. The question
stays open. The fixture lock flag was intact before this session's
probe.

9.4 Open question. `_RgbAlphaMaskChannel` exists in the property table.
The mask equation always samples red in the attested source. The
property therefore has no alpha-relevant role on this path. A later
session does not need to re-derive this.

9.5 Scope note. The detail feature blocks the base color, emission, and
normal outputs of the fixture. Those outputs sit outside this parity
boundary. The slot consumes the alpha output only, so the fixture's
split does not depend on them.

## 10. Validation of this session

No repository test ran. This is a docs-only session on a branch without
production changes. The live probe of section 5 is the observed
evidence. The repository facts carry their own file and test
references. The probe deleted its temporary asset inside the same
handler, and the fixture stayed locked afterwards. No dialog appeared
and no build ran.
