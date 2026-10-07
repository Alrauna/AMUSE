# Census Lab refusal coverage investigation: support designs for the refused features

Date: 2026-10-07. Branch: `feat/census-refusal-coverage`. Base: `main` at `035d5b2`.

Privacy note: this record is sanitized. It names no private avatar, renderer, material, texture, scene, asset folder, instance name, or test-job identifier. The avatar is "the Census Lab avatar under test". Renderers and materials are named by role. Aggregate entry and renderer counts are observed counts at the same level as the 2026-10-06 audit. Public vendor shader identifiers stay exact. Machine references name roles only, for example the Census Lab editor instance.

Labels: `[SOURCE]` marks a fact read at cited lines of this tree on this branch. `[INFERENCE]` marks a conclusion. This record consolidates six parallel investigations from 2026-10-07. Each investigation re-located its citations by symbol on this tree. Line numbers from the 2026-10-06 audit were not reused.

## 1. Verdict

The play-mode build of the avatar under test refuses in six places. Five of the six are supportable with in-repo work or with named external evidence. One family is a genuinely necessary refusal today. No support design widens a claim. Every design keeps `MustRemainTransparent` absorbing, keeps classification fail-closed, and moves nothing that is not proven.

| Item | Refusal on the avatar under test | Verdict | Proposed action |
|---|---|---|---|
| S1 closure scope | `MaterialDependencyClosureFailed` on 5 renderers, including the body-skinned mesh | supportable in repo | implement |
| S2 crunched capture | `UnsupportedFormat` on 4 entries, format `DXT1Crunched` | supportable in repo | implement with one decision |
| S3 mask envelope | `_AlphaMaskValue` refusals on 4 renderers | supportable in repo | implement |
| S4 old Poiyomi | 2 entries, locked and unlocked 9.0 forms | supportable once pinned data exists | gather evidence, then implement |
| S5 unknown families | 21 `AdmittedMaterialSemanticsUnknown` entries | split verdict, see section 8 | gather evidence, then decide |
| S6 conversion arms | 4 `OpaqueConversionRefused` entries | no support gap; observability gap | small in-repo fix, then one Lab read |

## 2. Ground truth against this base

PR number 137 (`fb05a6e`) is on this base. It fixed the report-text defects of the 2026-10-06 audit. Conversion refusals now name the refused material. Closure refusals name the renderer and the way. Format refusals name the format and the texture. Mask properties carry feature labels. [SOURCE: `git show fb05a6e`, and `ConversionRefusedSlotNamesItsMaterial` at `Tests/Editor/Build/AlphaSeparationPreparationTests.cs:5358-5407`]

PR number 137 changed no semantics. Its Poiyomi change is three feature-label map entries. [SOURCE: `Editor/Semantics/Poiyomi/PoiyomiMaterialSemantics.cs:989-991`]

## 3. S1: scope animation-closure failures to their slots

### Mechanism today

`MaterialDependencyClosureFailure` has five members: `None`, `MissingCurrentMaterial`, `SlotOutOfRange`, `InvalidSwapValue`, `UnattestedMaterial`. [SOURCE: `Editor/Host/CapturedAnimationEvidence.cs:8-15`]

On any failure way, the capturer returns a wholly empty evidence record. It keeps only the renderer-wide graph facts. [SOURCE: the `Failed` helper in `Editor/Host/UnityAnimationEvidenceCapture.cs`, and test `ClosureFailureRetainsGraphFactsButNoPartialEvidence` at `Tests/Editor/Host/UnityAnimationEvidenceCaptureTests.cs:1179-1203`]

Ways 1 to 3 are provably facts about one slot:

1. `MissingCurrentMaterial`. One slot has no current material. The loop refuses the whole renderer. [SOURCE: `UnityAnimationEvidenceCapture.cs:514-516`]
2. `SlotOutOfRange`. One swap binding names a slot index at or above the slot count. Tolerance on already scopes this: it records the index and closes the renderer. [SOURCE: `UnityAnimationEvidenceCapture.cs:529-545`]
3. `InvalidSwapValue`. One keyframe value of one slot binding is null or not a material. [SOURCE: `UnityAnimationEvidenceCapture.cs:547-554`]

Way 4, `UnattestedMaterial`, is the closed-batch capture refusal. It is renderer-scoped by the capturer contract, which is all-or-nothing and names no material. [SOURCE: `UnityAnimationEvidenceCapture.cs:604-612, 642-648`, contract doc near `:113-125`]

The plugin gate refuses renderer-wide on `!IsClosed`. It returns empty `SlotResults`. This discards per-slot analysis for every slot, including slots whose materials are attested and classifiable. [SOURCE: `Editor/Build/AmusePlatformFinishPlugin.cs:1122-1126`, empty results at `:1289-1295`]

This violates the codebase principle that unattestation is a fact about the slots that can hold a material, never about the renderer. [SOURCE: `UnityAnimationEvidenceCapture.cs:558-562`]

### Design

Ways 1 to 3 become per-slot capture facts. Way 4 stays renderer-scoped. Three surgical change groups:

Evidence shape in `CapturedAnimationEvidence`:

- Add `SlotClosureFailures`, a list of `{ SlotIndex, Failure }` records sorted by slot index.
- `CurrentMaterialIndices` gains `-1` as the documented sentinel for "no current material admitted".
- `ClosureFailure` narrows to `{ None, UnattestedMaterial }`. The three slot ways stop being representable renderer-wide. No new refusal member is invented. [INFERENCE on shape]
- Ways 1 to 3 return the partial-but-closed pairing of admitted materials. Way 4 keeps today's `Failed`.

Capture changes in `CaptureObserved`:

- Way 1 records the sentinel and the failure, then continues.
- Way 2 records `{ slot, SlotOutOfRange }` and skips the binding. Its values are not admitted.
- Way 3 records `{ slot, InvalidSwapValue }` and skips the binding without admitting its values.
- The clip-copy pass drops bindings whose slot carries a failure record. This preserves one invariant: the retained binding set is exactly the admitted set provenance. No retained binding has unadmitted values. [SOURCE: existing mirror for foreign and tolerance-skipped bindings near `:689-696`]

Plugin changes in `ResolveRuntimeStates`:

- The gate refuses renderer-wide only on way 4.
- `MaterialSlotsFor` skips the `-1` sentinel.
- The per-slot loop refuses a slot that carries a failure record, with `MaterialDependencyClosureFailed` in slot position, and continues. The refusal enum stays closed. The way rides the report-evidence path, not the enum.
- Renderer-wide checks above the loop run unchanged over the retained clips.

Apply changes in `AlphaSeparationApply`:

- The appended-slot guard at `:601-611` gains one conjunct: refuse an appended index that appears in `SlotClosureFailures`. It reuses `AlphaSeparationSlotRefusal.SlotBindingAbsentFromEvidence`. This guard is mandatory, see the hazard below. [SOURCE: `Editor/Build/AlphaSeparationApply.cs:601-611`]

Report changes:

- Failed slots get entries through the existing per-slot refusal template. The way sentence from `ClosureFailureSentence` folds into the evidence sentence. Renderer-wide closure entries shrink to way 4.

### False-positive safety

The hazard that matters: today, an out-of-range binding forces a renderer refusal, so no split ever appends a slot. Under partial capture, a surviving split appends a slot at an index an out-of-range binding may address. After the write, the animation could swap the appended opaque slot to a transparent source or to null. The extended apply guard closes exactly this path. [SOURCE: guard at `:601-611`, INFERENCE on the hazard]

Every animation fact that can influence a healthy slot T is either retained renderer-wide (float bindings, structural bindings, graph facts, block state) or is T-scoped and revalidated at apply (T's own swap curves and live current). A failed slot S cannot smuggle a material into T's domain, because admission is per-binding and T's proof domain is exactly T's admitted set. [SOURCE: `AlphaSeparationRecords.cs:311-316`; INFERENCE on completeness]

One nuance is sound: with S's materials excluded from the relevance request, a binding whose property only S's family requested downgrades from renderer-wide refusal to irrelevant. The binding could only influence S's proof inputs. S keeps its originals. [INFERENCE]

### Tests

Re-specify five existing tests that pin the all-or-nothing contract, for example `FailedClosureExposesNoPartialEvidence`. Keep way-4 and tolerance tests unchanged. Add nine RED tests: three capture tests, one retained-binding invariant test, three plugin tests, two apply tests. The apply tests must include the appended-index hazard end to end. Public reference fixtures cannot cover this. The fixtures carry geometry outcomes only, no clips. [SOURCE: `Tests/Editor/ReferenceFixtures/ReferenceFixtureData.cs:8-75`]

### Follow-up, not this slice

The capturer contract could name the failing ordinal, which would scope way 4 per material too. This changes the `ClosedAlphaMaterialCapturer` contract. Recorded as a separate candidate slice. [SOURCE: delegate doc near `UnityAnimationEvidenceCapture.cs:113-125`]

## 4. S2: admit crunched DXT capture

### Mechanism today

The format gate is step 4 of the capture order, before any GPU call. The allowlist admits exactly RGBA32, ARGB32, Alpha8, RGB24, DXT1, DXT5, BC7. Crunched is the dated exception. The comment demands durable exercise through the R8 path. [SOURCE: `Editor/Host/UnityAlphaFieldEvidence.cs:196-220, 817-850`]

Capture never CPU-reads the texture. The route blits the resident GPU texture through the predicate shader into an `R8_UNorm` target and reads the destination bytes back. [SOURCE: `UnityAlphaFieldEvidence.cs:470-530, 586-627`]

### The distinguishing fact

Exactly one code fact separates plain DXT1 captures from crunched refusals: allowlist membership. Crunch is a storage-only format. The editor transcodes crunched data to plain DXT blocks at load. The GPU samples ordinary BC data. There is no crunched sampling path. Capture and playback share one runtime representation. [SOURCE: gate comment `:827-828`; INFERENCE on the transcode, corroborated by the 14-configuration GPU agreement measurement in `docs/superpowers/investigations/2026-08-27-runtime-texture-evidence.md:194-214`]

### Design

- Admit exactly `DXT1Crunched` and `DXT5Crunched` in `IsAdmittedFormat`. Keep every ETC and ETC2 crunched variant refused. [SOURCE: build-target gate `:858-871`]
- `DXT1Crunched` inherits the DXT1 no-alpha ground. `DXT5Crunched` inherits the DXT5 exact-integer alpha-block ground after the load-time transcode.
- The format-refusal hint gains the two names.
- One unpinned machine fact must be measured by a fail-loud test: the imported crunched `graphicsFormat` and its `Sample` support. If exact support fails, the source-sampling gate needs a named crunched exemption with the measured-substitution argument, like the RGB24 precedent. Never a general fallback. [SOURCE: `UnityAlphaFieldEvidence.cs:906-963`]

### The one real hazard and the decision

A crunched streaming texture can reach the streaming clone route. Its CPU fallback arm decodes through `GetPixels`, and the CPU decoder rounds 254 up to 255 at the exact-one boundary. That arm can claim exactly-one where playback samples below one. This hazard class already exists for admitted DXT5 and BC7 streaming textures. Admitting crunched extends its population. Options:

1. Accept. The class pre-exists. The png and tga source arm normally captures first.
2. Scope admission to non-streaming textures. A conjunct before the streaming branch keeps the hazard population unchanged.
3. Characterize the clone arm for crunched and pin it.

Option 2 is the most conservative and costs the least evidence. It is the recommendation. [SOURCE: `Editor/Host/UnityStreamingTextureEvidence.cs:35-44, 178-206`; measured rounding note at `UnityAlphaFieldEvidenceTests.cs:382`]

### Tests

The home is `Tests/Editor/Host/UnityAlphaFieldEvidenceTests.cs`, which already imports real assets in pinned formats and already owns a crunched fixture that asserts refusal. Flip `ReadableCrunchedDxt5_Refuses` into a capture characterization. Add the `DXT1Crunched` sibling. Add a mipmapped chain fixture. Add the machine-fact pin. The exhaustive allowlist test grows from seven to nine names. Reference fixtures cannot cover this. Runtime-created crunched fixtures may feed the research calibration suite only. [SOURCE: `UnityAlphaFieldEvidenceTests.cs:383-389, 768-812`; `AlphaEvidenceCharacterizationTests.cs:21-39`]

## 5. S3: Poiyomi alpha-mask envelope parity

### Mechanism today

The pinned vendor equation for a bound mask is `saturate(mask.r * strength + value)`, with invert flipping the value sign. `TryInterpretAlphaMask` admits a bound mask only at (1, 0) and at the saturated pair (1, value at least 1). Every other pair refuses, including the avatar's (1, 0.4). [SOURCE: `Editor/Semantics/Poiyomi/PoiyomiMaterialSemantics.cs:1466-1472, 1511, 1820-1837`]

Red-channel evidence for the mask is already captured. The gap is the admission rule alone. [SOURCE: request at `PoiyomiMaterialSemantics.cs:2484-2488`]

### The machinery exists

The lilToon family already admits every assigned mask pair through the mapped-sample algebra: `AffineAlphaMap` evaluates `saturate(r * scale + value)` exactly on rationals with one rounding step and bounds the term on any red interval. The resolver first decides two uniform arms from the endpoint evaluations. Otherwise it classifies per triangle by asking whether the triangle's sampled domain contains a texel below full red. [SOURCE: `Editor/Semantics/AffineAlphaMap.cs:5-65`, `Editor/Analysis/AlphaSemanticsResolver.cs:583-627`, `Editor/Analysis/TriangleAlphaClassifier.cs:359-502`]

### Design

Arithmetic, derived from code: at strength 1 and value v in (0,1), the term is `saturate(r + v)`. Opacity requires the stored red byte to reach the rounding boundary of 1. In exact terms, red at or above `1 - v` is sufficient. For v = 0.4, byte 153 proves opaque and byte 152 does not. The predicate is sufficient, never necessary. It errs in the safe direction.

Changes confined to `TryInterpretAlphaMask`:

- Hoist the bound-mask preamble gates above the pair dispatch: mask UV exact-integer gate, zero-pan gate, mask ST gate, source-identity gate, and the borrowed-sampler and parallax gates.
- Replace the final refusal for strength 1, value in (0,1), invert off: construct the mask sample exactly as the (1, 0) arm does and return the mapped term, `MappedTexture(maskSample, Red, FromBinary32(1, v))` for Replace, and the mapped factor threaded into the product fold for Multiply.
- Invert on with value in (0,1) keeps refusing. Its opacity predicate is an upper bound, red at most v. The mapped lattice answers it unsoundly or not at all. A named refusal is the correct outcome. [SOURCE: `AlphaSemanticsResolver.cs:776-810`; INFERENCE on the trap]

The key correctness trap is already handled structurally. Poiyomi samples the mask with the mask own affine, zero pan, riding the main sampler. The mapping is built from the mask own captured ScaleOffset. The resolver transforms each triangle in mask-UV space. [SOURCE: pinned comment at `PoiyomiMaterialSemantics.cs:1494-1496`, construction near `:1720`, resolver transform at `AlphaSemanticsResolver.cs:392-402`]

### Known ceiling

This is a boolean-witness proof. A triangle proves opaque only when every reachable texel of its footprint stores full red. With v = 0.4, texels at bytes 153 to 254 are provably opaque in isolation but sit inside witness footprints, so their triangles stay Unknown. A magnitude proof over the red interval would close this. It needs a decode attestation: the red capture contract is decode-proof for the exactly-one predicate only, and an sRGB mask decodes non-linearly. The magnitude arm is a separate slice with its own decision. [SOURCE: capture decode note at `UnityAlphaFieldEvidence.cs:131-135`; INFERENCE on the sRGB hazard]

### Tests

Extend `Tests/Editor/Semantics/Poiyomi/PoiyomiAlphaMaskTests.cs` on the existing stand-in pattern: replace mode at v = 0.4 proves a bound triangle opaque, a mask with a hole classifies that triangle Unknown, a non-identity mask ST proves through the mask own transform, multiply mode carries the map through the fold, invert still refuses, and a mip level that witnesses keeps Unknown. Five existing negative rows flip from refuse to admit. The flip is legitimate: the rows gain stronger mapped-proof assertions instead of refusal assertions. [SOURCE: negative rows at `PoiyomiAlphaMaskTests.cs:745-750`]

## 6. S6: conversion-refusal arms and the material name

### Status at this base

The `<missing>` defect is closed. The emitter now passes the refused slot material. The resolver names the authoring asset through the registry. A test pins the behavior. [SOURCE: `Editor/Build/AlphaSeparationPreparation.cs:429-434`, `Editor/Build/AmuseReports.cs:240-243`, test at `AlphaSeparationPreparationTests.cs:5358-5407`]

### Arms

Seven arms produce `OpaqueConversionRefused` at this base. Their gates:

1. lilToon Multi eligibility. Evidence-dependent.
2. lilToon conversion seam. Test-only. Production passes null.
3. lilToon attestation. Necessary.
4. lilToon eligibility. Mostly necessary.
5. Poiyomi conversion seam. Test-only.
6. Poiyomi identity re-attestation. Near-unreachable. It repeats the classification conjunction.
7. Poiyomi eligibility gates. Per-material. NotFinite, ZTest, blend state, ForwardAdd, and cutoff are necessary. Outlines and alpha-to-coverage are evidence-dependent. Schema-absent is unreachable after attestation.

The most plausible arm for the avatar case is 7. It is the only per-material discriminator among attested current Poiyomi materials whose siblings converted. [SOURCE: `AlphaSeparationPreparation.cs:663-664, 700-701, 736-737, 752-753, 801-802, 831-832, 859-860`; INFERENCE on ranking]

The cutoff gate is genuinely necessary. Converting a material whose cutoff discards source-invisible triangles would make invisible geometry visible. That is the visibility form of the cardinal sin. [SOURCE: `ClipThresholdDiscardsOpaqueAlpha` semantics at `:859-860`]

### Residual gap and fix

The seven arms still collapse in text. The inner refusal token is discarded at the emitter, so the detail renders the bare enum. The minimal fix: pass the inner per-gate refusal token through the emitter existing detail parameter. [SOURCE: `AlphaSeparationPreparation.cs:838-860`, `Editor/Build/AmuseReports.cs:265-267`]

After that fix, one Lab read disambiguates the avatar case deterministically: read the four named materials' conversion facts, `_EnableOutlines`, `_AlphaToCoverage`, `_ZTest`, blend scalars, `_Cutoff`, and schema presence. That read is a read-only Census Lab session and a separate prerequisite step.

## 7. S4: old Poiyomi 9.0 attestation

### Mechanism today

Lock classification reads two signals and the recorded original tags. The blocker for the locked form is exactly the pinned-identity conjunction. For the unlocked form, the exact-name map answers Unsupported before any semantic read. Both forms refuse with no consent subject. [SOURCE: `Editor/Build/LockedMaterialIdentity.cs:39-47, 128-155, 265-305`; `Editor/Semantics/Poiyomi/PoiyomiMaterialSemantics.cs:2186-2292`; `Editor/Semantics/UnityMaterialSemantics.cs:686-793`]

The audit counterfactual holds in code. The same values on an admitted name resolve `AlreadyOpaque` with no entry. The refusal is a version gate, not a semantic wall. [SOURCE: `Editor/Semantics/Poiyomi/PoiyomiOpaqueConversion.cs:173-176, 275-300`]

### Design and missing evidence

A Poiyomi identity row is five pinned fields: exact shader name, asset GUID, package name and version, normalized SHA-256 digest, and required schema. Two rows exist today, plain and Two Pass. The row mechanism has an in-repo precedent. All five identity consumers funnel into the one conjunction, so admission touches one function plus the name map. [SOURCE: `PoiyomiMaterialSemantics.cs:27-46, 2186-2292`; `Editor/Semantics/NormalizedSourceHash.cs:11-47`]

Missing artifacts, all obtainable from the vendor 9.0 artifact installed with the sanctioned package in the Census Lab project:

1. Exact declared names, including sibling variants.
2. The shipped asset GUID, or proof that the GUID is not channel-stable. If it is not stable, the locked-form admission needs a weaker named conjunction of name, digest, and schema. That is a design decision.
3. The normalized digest of the 9.0 source.
4. The package tuple decision. An `Old Versions` path may report the containing package version, which is noise. The row may need a package-free form.
5. A schema drift check: confirm 9.0 property behavior matches every property the frontend reads. Property drift can only lose coverage, never invent a claim, because every read gate fails closed. [SOURCE: `Editor/Semantics/EvidenceGates.cs:21-107`; INFERENCE on drift direction]

Tests: literal evidence rows for accept and refuse cases, a pin-protection test so a silent repin is impossible, and stand-in shaders declared as the 9.0 name through the existing test shader writer. [SOURCE: `Tests/Editor/Shared/TestShaderWriter.cs`; `Tests/Editor/Semantics/LilToon/LilToonAttestationTests.cs:2539-2543`]

## 8. S5: unsupported shader families

### Mechanism today

Selection is one exact-name map. An unsupported name becomes an empty sentinel that answers all-Unknown and refuses its own slots. Sibling slots keep their own proofs. No render-state fallback exists anywhere in selection or analysis. The only already-opaque route is Poiyomi-only, decided by an exact 25-fact comparison against the vendor pinned Opaque preset. [SOURCE: `Editor/Semantics/UnityMaterialSemantics.cs:686-801, 1085-1088`; `Editor/Host/UnityAnimationEvidenceCapture.cs:564-603`; `Editor/Semantics/EffectiveRenderState.cs:19-27`; `PoiyomiOpaqueConversion.cs:139-171, 296-300`]

### Why no generic route exists

A shader-agnostic render-state rule is unsound. Fragment discard is invisible to material render state. A shader with opaque blending, depth write, and an opaque queue can still clip every fragment. And property names mean nothing without a pinned schema. [SOURCE: vision sections on material meaning and shader support; INFERENCE grounded in `EffectiveRenderState.cs:19-27`]

What generalizes honestly is the per-family shape: one pinned identity row, one pinned opaque-preset conjunction, an alpha-exactly-1 answer, and a self-map conversion arm that writes nothing. This never moves a triangle. It only removes unnecessary refusals for materials whose visible image cannot depend on alpha. [SOURCE: `AlphaSeparationPreparation.cs:868-871` and its doc `:66-75`; `AlphaSeparationApply.cs:425-426`]

### Per-family verdicts

- Unity `Standard`. Needs external evidence. Pin the opaque conjunction over keywords and scalars, plus a per-Unity-version identity row. The pin set is a maintenance treadmill across admitted Unity patch releases. [INFERENCE on schema, verified before pinning]
- `VRChat/Mobile/Standard Lite`. Needs external evidence. Same shape. Identity measured from the installed SDK, pinned to the measured SDK version. [INFERENCE on schema]
- Gem lilToon variant. Needs external evidence plus a source-level proof that its alpha interpretation matches the pinned family equation. Without that proof the refusal is correct. A fork can change semantics silently while keeping names. [SOURCE: `Editor/Semantics/LilToon/LilToonSourceAttestation.cs:149-161, 1724-1737`]
- Camera-gadget system shaders. Genuinely necessary refusal today. Nothing is knowable in this repo. No pin can hold without a stability guarantee across tool versions. After S1 lands, these renderers refuse per slot as unsupported families instead of renderer-wide on closure. [INFERENCE]

### Coverage reality on the avatar under test

The audit records that these renderers kept everything original. It does not record the live render states of their materials. Every family admission converts a refusal only when the material instance matches the pinned opaque preset. Non-preset instances keep a named, specific refusal. [SOURCE: audit sections 3 and 5; INFERENCE on instance states]

## 9. Necessary refusals after full support

Three refusals on the avatar under test remain necessary under every design above:

1. Poiyomi invert-on mask with value in (0,1). The opacity predicate is an upper bound the algebra cannot prove.
2. The conversion cutoff gate. Converting would make source-invisible triangles visible.
3. The camera-gadget system shaders, until a vendor attestation with a stability guarantee exists.

## 10. Coverage priority on the avatar under test

Ranked by real proof movement, largest first:

1. S1. The body-skinned mesh is the single largest loss. Slot scoping also frees the four gadget quads for per-slot analysis.
2. S3. Four renderers with transparent-mode gradation masks. Real transparent-to-opaque movement.
3. S2. Two bodysuit-UV slots on the merged mesh, four refusals.
4. S6 fix, then the Lab read. Four slots on the merged mesh.
5. S4. Two entries. The materials are already opaque, so support removes refusals, not triangles.
6. S5. Refusal-noise removal for opaque-preset instances. No triangle movement on this avatar.

## 11. Limits

All six investigations are read-only code work. No Unity instance was touched. No measurement ran. The two unpinned machine facts are named in sections 4 and 7. The S6 arm identification and the S4/S5 pinned rows require the read-only Census Lab sessions described above. The audit-level per-renderer facts stay at audit aggregation. No per-renderer table exists in this record.

## 12. Decisions taken on 2026-10-07

1. Scope decision for S2: admit crunched on all routes and characterize the streaming clone arm for crunched in the same slice. The characterization must pin the measured CPU decode behavior and follow the repo precedent for the same hazard class on admitted formats.
2. Timing decision: the read-only Census Lab evidence session runs before implementation. It collects the 9.0 identity row, the four conversion-refused materials' conversion facts, the Standard and Standard Lite identity facts, and the crunched sampling-capability fact.
3. Implementation order on this branch: S1, S3, S2, S6 fix, S4 after its row exists, then the way-4 ordinal scoping follow-up. S5 frontends follow their evidence.

## 13. Census Lab evidence, collected 2026-10-07

All probes ran read-only in the Census Lab editor instance against the corpus prefab folder. Every output below is aggregate at the audit's own level. No private name, path, or instance identity appears.

### S4: the 9.0 source facts

The sanctioned vendor package in the Census Lab project installs `.poiyomi/Old Versions/9.0/Poiyomi Toon`. Measured read-only on 2026-10-07:

- The declared shader name resolves; the install ships no sibling variant under the 9.0 folder.
- All 24 required conversion schema properties are present in the 9.0 source. None is missing.
- The containing package tuple equals the current package's. The package evidence check therefore behaves identically for a 9.0 source and a verified source, so the row needs no special package form.

Design consequence, implemented on this branch: the 9.0 name classifies to the Poiyomi family with the plain request, and the source stays deliberately unpinned. The D8 pre-scan produces the transfer-consent subject for it. A build that grants the consent treats the source with the verified version's rules in capture, analysis, conversion, and the lock's original-attestation gate. An unconsented build refuses at the identity conjunction, fail closed.

### S6: the conversion-refusal arm on the avatar under test

The corpus carries 21 current-Poiyomi material instances in one prefab file. The conversion-relevant facts:

- `_EnableOutlines`: 15 instances at 0, 6 at 1. Every other eligibility input is canonical: `_ZTest` 4 everywhere, `_AlphaToCoverage` 0 everywhere, additive blend ops, no cutoff above one.
- The refused slots on the merged mesh are the outlines gate. The per-gate detail this branch now emits names it as `OpaqueConversionRefused: OutlinesEnabled` on the next build.
- The same sweep confirms the S3 target: one instance carries `_AlphaMaskValue` 0.4 at strength 1, three carry invert on, and the audited gradation-mask material is real.

### S5: identity facts for the Standard family

- Unity `Standard` ships as a real asset with a stable GUID and a computable normalized digest. Its schema matches the inferred conjunction: `_Mode`, `_Cutoff`, `_SrcBlend`, `_DstBlend`, `_ZWrite`, and the three alpha keywords. The pinned row is obtainable.
- `VRChat/Mobile/Standard Lite` ships in the SDK package with its own GUID and digest, but none of the Standard property names exist. Its frontend needs its own source-read property set, exactly as the S5 investigation required before pinning.

### S2: the crunched sampling facts

- An imported crunched texture reports graphics format `RGBA_DXT5_SRGB` after the load-time transcode, and the editor reports exact `Sample` support for it. The source-sampling gate needs no exemption; the fail-loud characterization test pins both facts.
- The CPU decode measurement: on this editor the CPU crunch decoder decodes a representable submaximum exactly, matching the GPU route. The earlier "254 rounds up to 255" note does not reproduce; the clone-arm hazard extension for crunched is measured empty, and the equivalence test pins it fail-loud.

## 14. Residual verification and open decisions, as of 2026-10-07

1. Validated on the dev editor instance on this date: the S1, S3, S2, S6, and S4 slices. The full product EditMode assembly passed 2483 of 2483 with 2 by-design inconclusive d4rk integration rows, the research assembly passed 138 of 138, the S4-affected groups passed 336 of 336 after the dev editor instance restarted, and the eight named new or changed tests passed 8 of 8. One mid-run abort and nine environmental failures occurred while the dev editor instance was restarting; the re-run after the restart was clean.
2. The conversion-side grant for a consent-granted unverified source has no direct in-repo test, because no harness reaches the real conversion route without a verified source. The next authorized Census Lab build exercises it end to end; the transferred-capture tests pin the same grant semantics on the capture side.
3. Open decisions, unchanged: the S5 Standard and Standard Lite frontends (pins now obtainable), the gem lilToon variant's source-level equivalence proof, the way-4 capturer ordinal scoping, and the S3 magnitude extension with its decode attestation.

## 15. Adversarial review amendments, 2026-10-07

Four parallel adversarial reviews ran against the committed slices. All four verdicts are correct: no false-positive vector, no consent bypass, and no broken invariant. The findings below amend this record, and the fixes were applied after the reviewed snapshot was committed.

1. Section 4's hazard statement is superseded by measurement. The committed characterization on the dev editor instance decodes a representable submaximum exactly through the CPU decoder and through the GPU route alike, so the streaming clone-arm hazard extension for crunched is measured empty. Section 4's premise that the same hazard class already exists for admitted DXT5 and BC7 streaming textures is not verified by any test in the tree and stands recorded as an assumption, not a fact.
2. Section 5's test paragraph overclaimed. The committed change moves exactly two negative rows, (1, 0.5) and (1, -0.25), into dedicated mapped tests; it does not flip five rows. The main-sampler discrimination test, the non-identity mask ST test, and the mip-witness test named there exist now and were added in the review-fix pass.
3. The S1 design section says a failed slot's materials leave the relevance request. The committed capture does not exclude them: failed-slot materials stay admitted, so their properties stay in the relevance union. The committed behavior is strictly more refusing than the described design, never less safe.
4. The all-slots-failed renderer entry names its first failed slot's way again, instead of printing the bare refusal.
5. Known scope, documented: an unconsented material that only an animation swap can bring into a slot is never assigned at build start, so the consent pre-scan never offers it a subject. It classifies to the Poiyomi family, fails the closed capture batch, and refuses renderer-wide, where base refused only its own slots. Fail-closed, never a false positive, and the same shape the consent layer already applies to every other unverified supported-family source. Extending the pre-scan to swap-curve materials is a future design decision.
6. Review-fix pass, applied: a discriminating apply test deletes the live phantom curve in the probe pass, so only the slot-failure guard conjunct can produce the refusal; the lock attestation gate gained grant-composition tests including the window-eligibility pair; the mipmapped crunched fixture pins its imported format; the streaming crunched test is rescoped to the source-image route it actually exercises; the allowlist doc comment now states the clone-arm pin accurately.
