# Third Latent Bugs and Architectural Impurities Investigation

Date: 2026-10-09. Branch: `fix/latent-bugs-and-impurities`. Base: `fae7e7e` (the secondary audit fix landed here).

Privacy note: this record is sanitized. It names no private avatar, renderer, material, texture, scene, asset folder, instance name, or test-job identifier. Machine references name roles only, for example the dev editor instance. All paths are repository-relative.

---

## 1. Executive Summary

A third codebase audit ran on 2026-10-08 and 2026-10-09.
The audit used eight parallel read-only subagent scouts, one per module boundary: Build, Analysis, Semantics root, Semantics/LilToon, Semantics/Poiyomi, Host, editor periphery, and a cross-module architecture scan.
The integrator then verified the high-severity claims against the source before acceptance. Nine of the ten high-priority findings carry the Verified label. Finding 10 carries the weaker Scout-verified label with a runtime probe as a fix precondition.
The integrator corrected two scout claims and merged two related claims into one finding.
The audit produced ten high-priority latent defects, thirteen medium defects, and nineteen compact lower-severity findings and impurities.
Section 2 treats the high-priority defects in full. Section 3 treats the medium defects in full and adds five compact medium entries. Section 4 keeps the nine detailed impurities and adds the nineteen compact entries.
Each detailed finding cites exact files and lines, states the failure direction, and gives a falsifier. Each compact entry cites the mechanism and the fix shape in brief.
An adversarial review of this record completed on 2026-10-09. Four parallel reviewers checked the record against the code and against the original scout reports. Section 8 records their verdicts. The integrator applied every correction the same day.

Failure direction vocabulary in this record:

- Wrongly opaque: a triangle that must stay transparent can move to an opaque material. This direction can corrupt the visible avatar.
- Wrongly transparent: a triangle stays on the source material. This direction is conservative, but a false proof claim is still a defect in the proof core.
- Coverage defect: the transformation refuses more than its contract requires.
- Build fatal: the defect throws and blocks the avatar build.

Verification labels in this record:

- Verified: the integrator read the cited lines in this session.
- Scout-verified: the citing scout quoted the lines and the integrator checked the surrounding wiring.
- Runtime confirmation: the claim needs one execution probe before a fix lands.

---

## 2. High-Priority Latent Defects

### Finding 1: Multi Conversion Reads a Schema the Capture Never Requested

- Paths:
  - `Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonMultiResolution.cs:68-89`
  - `Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonMultiSourceEligibility.cs:219-221, 277-296`
  - `Packages/com.alrauna.amuse/Editor/Host/UnityMaterialEvidenceCapture.cs:622-634`
  - `Packages/com.alrauna.amuse/Editor/Build/AlphaSeparationPreparation.cs:639-703`
- Symbols: `LilToonMultiResolution.MultiEvidenceRequest`, `LilToonMultiSourceEligibility.EvaluateVerifiedEligibility`, `CapturedMaterialEvidence.TryGetScalar`
- Verification: Verified.
- Failure condition:
  The Multi family captures under `MultiEvidenceRequest` alone. That request unions three alpha requests and the mode-gate request. It names none of the eighteen recipe properties.
  `EvaluateVerifiedEligibility` reads `EligibilitySchema`, and that schema starts with `LilToonOpaqueTarget.RecipeSchemaProperties`.
  `TryGetScalar` throws for a name outside the capture request. The throw carries the message "Property was not requested."
  `LilToonMultiSourceEligibility.ConversionEvidenceRequest` exists for exactly this union. Its own comment says capture and conversion reads cannot drift apart. No production call site uses it. Only `LilToonMultiSourceEligibilityTests.cs:147-148` uses it.
  A resolved, fully proven Multi material therefore reaches eligibility and the schema loop throws.
  The same throw also fires earlier, in admission and in the runtime overwrite loop, when a recipe property is animated. The animated path reads the recipe names against the same un-widened evidence.
- Failure direction: Build fatal. Every admissible Multi conversion crashes the build with a programming-defect exception instead of converting or refusing.
- Falsifier: Run the platform pass on an avatar with one default Multi material in cutout mode. Classification admits it. `ConvertAdmittedMaterial` throws on the first recipe name.
- Remediation: Add `LilToonMultiSourceEligibility.ConversionEvidenceRequest` to the `MultiEvidenceRequest` union. Add a test that captures a Multi material under the production request and runs the eligibility evaluator without a throw.

### Finding 2: Multi Materials Get No Per-Family Cutoff Predicate

- Paths:
  - `Packages/com.alrauna.amuse/Editor/Semantics/UnityMaterialSemantics.cs:820-830, 404-416`
  - `Packages/com.alrauna.amuse/Editor/Host/UnityMaterialEvidenceCapture.cs:1186-1211`
  - `Packages/com.alrauna.amuse/Editor/Semantics/Poiyomi/PoiyomiMaterialSemantics.cs:1355-1362`
- Symbols: `UnityMaterialSemantics.AlphaRequestForFamily`, `UnityMaterialSemantics.AlphaPredicateRequestFor`, `UnityMaterialEvidenceCapture.DeclaresCutoffFor`
- Verification: Verified (the switch cases and the Poiyomi precedent). The downstream classification direction needs runtime confirmation.
- Failure condition:
  `AlphaRequestForFamily` has cases for the Poiyomi families and the regular lilToon families. It has no `LilToonMulti` case. The default arm returns null.
  `AlphaPredicateRequestFor` returns that null for Multi. `DeclaresCutoffFor` treats a null predicate as "keep the union request declaration".
  The union contains the cutout member, and the cutout member declares the `_Cutoff` binarization for `_MainTex`.
  A Multi material resolved to transparent mode therefore gets a cutoff-binarized `_MainTex` field. The transparent theorem needs the exact-255 field. The Poiyomi frontend guards exactly this hazard with a capture-threshold check. The lilToon frontends have no equivalent check.
- Failure direction: Wrongly opaque. An alpha 0.5 texel with `_Cutoff` 0.3 stores 255, the resolver reads 255 as exactly one, and the triangle classifies proven opaque. The defect acts at classification time, before Finding 1 can crash the boundary.
- Falsifier: Capture a Multi container in transparent mode with alpha 0.5 texels and `_Cutoff` 0.3. Compare the served field key against the resolved transparent request. Then classify one triangle over those texels.
- Remediation: Add the `LilToonMulti` case to `AlphaRequestForFamily` and gate the resolved mode like the Poiyomi frontend does. Confirm at runtime whether the current resolver over-refuses or misclassifies, and pin the observed behavior with a test.

### Finding 3: Poiyomi Conversion Rewrites the Outline Blend Tuple without a Gate

- Paths:
  - `Packages/com.alrauna.amuse/Editor/Semantics/Poiyomi/PoiyomiOpaqueConversion.cs:190-196, 263-395`
  - `Packages/com.alrauna.amuse/Editor/Semantics/Poiyomi/PoiyomiMaterialSemantics.cs:1430-1568, 2695-2805`
  - `docs/superpowers/specs/2026-08-27-render-state-opaque-conversion-design.md` (section 5.3)
  - `docs/superpowers/specs/2026-10-07-poiyomi-outline-alpha-conversion-design.md` (D1, D6, section 9)
- Symbols: the canonical outline tuple writes, `PoiyomiOpaqueConversion.EvaluateVerifiedEligibility`
- Verification: Verified by repository-wide grep. Production code touches `_OutlineSrcBlend`, `_OutlineDstBlend`, and `_OutlineBlendOp` only in the recipe tuple writes. No eligibility gate and no evidence request reads them.
- Failure condition:
  The 2026-08-27 design refused outline-enabled conversion. Its rationale said the outline blend fields are irrelevant once no outline fragment survives.
  The 2026-10-07 outline design deleted that refusal. No gate replaced it.
  The conversion recipe writes `_OutlineSrcBlend = 1`, `_OutlineDstBlend = 0`, and the alpha and blend-op partners. These properties drive the outline pass blending on the pinned shader. A user can set them in the shader UI.
  A fade material with an additive outline pair passes every alpha gate and every conversion gate. The clone then renders the outline as a solid color-replacing shell instead of an additive glow.
  `IrrelevantChangeInvarianceTests.cs:76-82` lists these fields as today's irrelevant set. The test asserts semantic-output invariance, and that claim stays true after a conversion-side admission gate, because the semantics layer never reads the outline blend pair. The list itself need not change. At most a comment should note that conversion now gates these fields.
- Failure direction: Wrongly opaque. The moved triangles render differently from the source.
- Falsifier: Prepare a canonical opaque clone from an attested material with an additive outline pair and an opaque line color. Compare the outline rendering of source and clone.
- Remediation: Gate the outline blend fields. Admit only pairs that degenerate to identity at alpha one, as the base-pass gate does. Refuse the rest with a named refusal.

### Finding 4: The Bilinear Clamp Pre-Filter Is Not One-Sided for Large UV Coordinates

- Paths:
  - `Packages/com.alrauna.amuse/Editor/Analysis/TriangleAlphaClassifier.cs:1282-1350, 1004-1118, 1172-1177`
  - `Packages/com.alrauna.amuse/Editor/Analysis/TriangleAlphaClassifier.cs:820-930` (the repeat contrast)
- Symbols: `ConservativeBilinearSupportOverlapsTriangle`, `EdgeSeparates`, `ExtractTexelVertices`, `ClassifyBilinearClamp`, `HasMappedWitnessBilinearClamp`
- Verification: Verified.
- Failure condition:
  The clamp walks use a double-precision separating-axis test to skip exact intersection checks. The test uses absolute margins of 1e-9 and 1e-12.
  `ExtractTexelVertices` converts exact rational domain vertices to doubles. The absolute rounding error of that conversion grows with the coordinate magnitude without a bound.
  The repeat walks normalize the domain first. `NormalizeRepeat` keeps the coordinates within one texture period, so the repeat paths stay safe.
  The clamp walks never normalize. A triangle with UV coordinates far outside zero to one produces coordinates in the millions of texel units. The rounding error then exceeds the margins, and the test can report separation for a box the triangle truly covers.
  The walk then skips a candidate whose footprint intersects the triangle. A witness texel never reaches the exact test. The walk returns proven opaque.
  The `isBoundary` bypass protects only border texels. It does not protect interior texels of a large-coordinate triangle.
- Failure direction: Wrongly opaque. This is the worst direction in the proof core.
- Falsifier: Classify one triangle against an 8 by 8 clamp texture with one sub-opaque texel. Use UV vertices near plus and minus 16 million texel units, then the same shape shifted by 16 texels. The large run must return the same verdict as the small run. Today it does not.
- Remediation: Translate the pre-filter inputs per candidate so the coordinates stay small, or compute a proven error bound and skip the pre-filter when the coordinate magnitude exceeds it. Keep the repeat normalization as the model.

### Finding 5: The Streaming Route Decodes the Authoring Image, not the Imported Representation

- Paths:
  - `Packages/com.alrauna.amuse/Editor/Host/SourceImageAlphaReader.cs:16, 72, 106-128`
  - `Packages/com.alrauna.amuse/Editor/Host/UnityStreamingTextureEvidence.cs:95-101`
- Symbols: `SourceImageAlphaReader.TryReadSourceAlphaChain`
- Verification: Verified.
- Failure condition:
  The streaming route tries the disk reader before the clone route. The reader decodes the raw .png or .tga bytes with `ImageConversion.LoadImage`.
  The reader consults exactly one importer fact: `alphaSource == None` turns the alpha samples into 1.0.
  Unity also replaces the alpha channel with RGB grayscale when the import uses `TextureImporterAlphaSource.FromGrayScale`. The reader does not handle that mode. It returns the file's real alpha bytes while playback samples the grayscale luminance as alpha.
  A fully opaque file over mid-gray RGB then captures an all-255 chain while playback samples about 0.5.
  The repository itself pins the divergence model. `UnityAlphaFieldEvidenceTests.cs:843-849` asserts that the field route reports the generated alpha for a FromGrayScale import, not the input alpha. The two routes therefore disagree for this importer mode by the repository's own test.
  The width and height check catches importer downscale at mip 0. Importer mip filtering can still diverge from the box-average chain at coarser levels.
- Failure direction: Wrongly opaque.
- Falsifier: Import one streaming PNG with alpha source FromGrayScale, dark gray RGB, and opaque alpha. Compare this route's chain against the clone route's chain for the same asset.
- Remediation: Refuse the route when `alphaSource` is not `FromTextureContent`, or restrict the route to imports the reader can reproduce exactly. The clone route stays the fallback.

### Finding 6: Red-Channel Evidence under an Active Policy Breaks Two Contracts at Once

- Paths:
  - `Packages/com.alrauna.amuse/Editor/Host/UnityMaterialEvidenceCapture.cs:1370-1395`
  - `Packages/com.alrauna.amuse/Editor/Analysis/AlphaSemanticsResolver.cs:21-40`
  - `Packages/com.alrauna.amuse/Editor/Analysis/TriangleAlphaClassifier.cs:432-446, 500-528`
  - `Packages/com.alrauna.amuse/Editor/Host/Shaders/AmuseRedExactOne.shader:13-15`
- Symbols: the red `TryCapture` call, `AlphaResolution` field contract, `DecideMapped`
- Verification: Verified. The red channel captures with threshold 1.0 and the caller's active bounds. The alpha channel carries the caller's bounds too, and the capture core forces inert bounds inside `TryCapture` only when the cutoff threshold is below one, at `UnityAlphaFieldEvidence.cs:329-331`. The forcing at `AlphaFieldSet.cs:55-57` applies to the alpha field key only. The red field key carries the active bounds with no force. So the active bounds reach the red field whenever the user moves the opaque percent below 100.
- Failure condition, part A:
  The Analysis mapped contract says byte 255 marks exactly the texels whose value is exactly 1.
  Under an active policy, byte 255 marks red at or above the opaque bound. A texel at 0.85 stores 255 under an 0.80 bound.
  The no-witness mapped arm evaluates the affine map at exactly 1. A map with scale 2 and offset -1 then proves opaque for a texel whose true mapped value is 0.7.
- Failure condition, part B:
  The predicate shader comment says the sRGB transfer is decided by the import. A `Texture2D.Load` fetch applies only the Unorm expansion. The sRGB to linear transfer happens at the sampler on the desktop graphics APIs.
  lilToon samples the alpha mask through a standard sampler, so a default sRGB mask import linearizes at playback. The capture compares raw stored bytes against the policy bound. The two spaces disagree below 1.0, and the disagreement is optimistic.
- Failure direction: Wrongly opaque for both parts. Part A needs an active policy and a mapped red resolution. Part B needs an sRGB mask import and an active policy or noise bound.
- Falsifier, part A: capture a red mask whose every texel sits at or above the bound but below 1.0, under a bound below 255. Resolve a mapped sample over it and observe the no-witness opaque proof.
- Falsifier, part B: blit the red predicate with bound 0.8 over a stored byte 217 of an sRGB mask. Compare against a sampled fetch of the same texel. The sample linearizes to about 0.7.
- Remediation: Capture the mapped red field under inert bounds, or carry the bound semantics into Analysis and make the mapped arm bound-aware. Assert the field contract at the seam so no future capture can break it silently. Confirm part B with the one-texel probe before fixing.

### Finding 7: The Saturating Sum and Disjunction Folds Prove Transparency from a False Lemma

- Paths:
  - `Packages/com.alrauna.amuse/Editor/Analysis/AlphaSemanticsResolver.cs:236-290, 352-368, 700-756`
- Symbols: `AlphaResolution.Or`, `AlphaResolution.Classify`, `ResolveSaturatingSum`
- Verification: Verified.
- Failure condition:
  The Or fold documentation claims the sum reaches exactly one exactly when either factor reaches one. The claim is false. The sum reaches one whenever the factors add to at least one. Two complementary masks prove the point: each factor dips below one, and the sum is identically one.
  The classified arm returns transparent when both factors classify transparent. Each factor carries a sub-one witness, but the witnesses can sit at different texels. The arm asserts a sub-one witness for the sum that no one proved.
  The uniform shortcut returns the other factor's resolution when one factor is a uniformly transparent constant. The same false assertion follows, because the constant can push every sum sample over one.
  Two methods earlier, the same file states the correct rule: cross-field correlation between two sampled terms is unknowable, so no per-triangle proof exists without an additive classifier. The fold contradicts its own epistemology.
- Failure direction: Wrongly transparent, with a false proof claim in the absorbing slot. No corruption follows, because a wrongly transparent triangle stays on the source material. The coverage loss is real on the shipping lilToon layer alpha mode 3. A false claim in the proof core is a defect even in the safe direction.
- Falsifier: Classify one triangle under the disjunction of two complementary classified masks whose sum is identically one. The fold returns transparent while the true outcome is proven opaque.
- Remediation: Return unknown from the absorbing arm unless a common-point additive argument exists. Keep the proven-opaque arm, which is sound. Update the pinned test that bakes the current behavior in.

### Finding 8: The Masked Streaming Chain Rewrites Coarse Mips to Policy Intent

- Paths:
  - `Packages/com.alrauna.amuse/Editor/Host/SourceImageAlphaReader.cs:89-92, 120-136`
  - `Packages/com.alrauna.amuse/Editor/Host/SourceImageMaskedChain.cs:28-100`
  - `Packages/com.alrauna.amuse/Editor/Host/UnityStreamingTextureEvidence.cs:164-167`
- Symbols: `SourceImageMaskedChain.Build`
- Verification: Verified.
- Failure condition:
  Under an active policy with no shader cutoff, the streaming reader takes the masked route. The route decodes mip 0 once and derives every coarser level by averaging only the texels the policy keeps.
  The GPU mip chain averages every texel, including the texels below the noise bound. Excluding them raises the coarse averages.
  The clone route states the opposite in its own comment: its coarser levels arrive pre-averaged, so each texel resolves at its own level.
  The two routes therefore produce different evidence for the same texture. Which route serves depends on the file format. The reader's class comment promises predicate equivalence with the effective representation at every captured mip. At most one route can honor that promise, and the masked route is the optimistic one.
- Failure direction: Wrongly opaque at coarse mips. Preconditions: active policy with a noise bound, no shader cutoff, a decodable .png or .tga streaming texture, hardware selecting a coarse level.
- Falsifier: Build a 4 by 4 RGBA chain with one transparent texel under a noise policy. Compare the masked route's level-1 verdict against the imported mip's own verdict on the clone route.
- Remediation: Make the masked route's coarse levels describe effective content, or refuse the masked route under an active policy and keep the clone route.

### Finding 9: Apply Revalidates the Mesh by Reference Identity only

- Paths:
  - `Packages/com.alrauna.amuse/Editor/Build/AlphaSeparationPreparation.cs:529-534`
  - `Packages/com.alrauna.amuse/Editor/Build/AlphaSeparationApply.cs:148-153, 694-713`
- Symbols: `AlphaSeparationApply.PrepareSurvivingSet`, `AlphaSeparationApply.FinalizeClone`
- Verification: Verified.
- Failure condition:
  Preparation instantiates the mesh clone at barrier time. Apply revalidates the renderer mesh with a reference equality check against the expected source mesh, plus the submesh count.
  A same-phase pass can mutate the mesh contents in place without replacing the reference. That edit passes every check. Apply then ships the barrier-time index buffers reordered by barrier-time triangle ordinals. The foreign edit disappears, and the moved triangles can be the wrong ones.
  The comment in `FinalizeClone` says nothing rewrote the indices because a replaced mesh was already refused. The code protects only the replacement case, exactly as the comment admits.
- Failure direction: Silent wrong geometry on the built avatar.
- Falsifier: Insert a test pass between barrier and apply that appends one triangle to the expected mesh in place. Observe the shipped clone without the appended triangle.
- Remediation: Revalidate a cheap geometry fingerprint at apply, for example the per-submesh index counts, before finalizing the clone. Document the threat model in the comment.

### Finding 10: Normal-Map Imports Pass the Sampled-Alpha-Is-One Predicate

- Paths:
  - `Packages/com.alrauna.amuse/Editor/Semantics/UnityTextureEvidence.cs:201-207, 230`
  - `Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonMaterialSemantics.cs:900-902`
  - `Packages/com.alrauna.amuse/Editor/Semantics/Poiyomi/PoiyomiMaterialSemantics.cs:2303-2306`
- Symbols: `UnityTextureEvidence.TryProveSampledAlphaIsOne`, `IsCanonicalNormalMapImport`
- Verification: Scout-verified. One runtime probe is advised before the fix.
- Failure condition:
  The predicate reads the source file's alpha and the import alpha source. For a normal map import, the desktop build swizzles the tangent-space normal into the texture channels, with the x component in the alpha channel.
  The sampled alpha of a normal map therefore equals the normal x component. It is not 1, even when the source file has no alpha channel.
  The same class exposes `IsCanonicalNormalMapImport` as a separate fact, so the two predicates can disagree about one texture.
  The emission-slot proofs consume this fact in both frontends. A no-alpha source PNG imported as a normal map and assigned to an emission slot passes a fact that is false at runtime.
- Failure direction: Wrongly opaque through the emission proof.
- Falsifier: Import an RGB-only PNG as a normal map. The predicate returns true. Blit the built texture and read one texel alpha. The read value matches the normal x component, not 1.
- Scope notes from verification: the x-in-alpha encoding is the documented default on desktop platforms in 2022.3. Mobile-style XYZ encodings and BC5 imports keep alpha at 1, so the conclusion coincidentally holds there. The per-texel probe stays a precondition for the fix. The secondary question about a stale sRGB flag on normal map imports is open in the same code region.
- Remediation: Return false from the predicate for normal map imports. Confirm the exact 2022.3 desktop format behavior with one probe first. Also check whether `TryGetColorInterpretation` should refuse the stale sRGB flag on normal map imports in the same change.

---

## 3. Medium Defects

### Finding 11: Nullable Object Curve Dereferenced in Apply Validation

- Path: `Packages/com.alrauna.amuse/Editor/Build/AlphaSeparationApply.cs:651`
- The slot validation loop dereferences `target.Curve` without a null check. The curve-edit loop at lines 370-372 guards the same value. The discovery loop at lines 244-263 stores the curve unguarded. NDMF declares `GetObjectCurve` as nullable, and the transient unlock code guards the same call at `TransientUnlockSwapIn.cs:184-187` and `:496-498`.
- A null curve here is a null reference exception and a build-blocking internal failure. The bindings come from the same clip's cache and nothing mutates clips between discovery and validation, so the null case is latent today.
- Remediation: Guard the null and empty curve the same way the sibling loop does. Runtime confirmation: whether Unity returns null for an enumerated binding was not reproduced from source alone.

### Finding 12: Structural Graph Check Runs Outside the Consent Gates

- Paths: `Packages/com.alrauna.amuse/Editor/Build/AmusePlatformFinishPlugin.cs:209-211, 398-410`, `Packages/com.alrauna.amuse/Editor/Build/AmuseStructuralGraphCheck.cs:16-30`
- The structural pass registers unconditionally, before every gate. It walks the full committed controller graph for every avatar build in a project with the package installed. It reports avatar refusals for avatars that never opted in.
- The plugin's own contract says an avatar without the component is a total silent no-op. The feature switch promises nothing analyzed, nothing mutated, nothing reported. The current wiring breaks both sentences, and an avatar that the lifecycle would refuse still receives an earlier refusal line.
- Remediation: Gate the structural pass on the same consent surface as the rest of the pipeline, or reduce it to a silent pre-check whose result is only read behind the gate.

### Finding 13: Renderer Path Never Revalidated at Apply

- Paths: `Packages/com.alrauna.amuse/Editor/Build/AlphaSeparationApply.cs:241-243, 380`
- Apply discovers curves and writes appended bindings through the barrier-time renderer path. No revalidation recalculates the transform path or the renderer type name.
- A same-phase reparent makes apply address a dead path, or address a different renderer that now owns the old path. In the second case, apply can rewrite a foreign renderer's material swap curve when the swap values happen to match this slot's mapping.
- The origin scout marked this finding as needing runtime confirmation of the cross-renderer rewrite reachability. The flag survives into this record.
- Remediation: Recalculate the transform path at apply and refuse the renderer when it changed. The mesh identity check already refuses the replacement case, so this closes the sibling gap.

### Finding 14: Generated-Texture Session Cache Has No Content Fingerprint

- Path: `Packages/com.alrauna.amuse/Editor/Host/UnityGeneratedTextureEvidence.cs:23-24, 96-106`
- The session cache keys a chain by instance id, channel, cutoff, bounds, and mip limit. A hit returns the stored chain before any content check.
- In-place pixel mutation of the same instance, or instance id reuse after destroy and recreate, serves stale evidence. The per-build cache clears contain the usual case. The hazard is a mid-session mutation or an editor with disabled domain reload.
- Remediation: Add a cheap content fingerprint to the key, for example a content hash of mip 0, or narrow the cache lifetime to one capture scope.

### Finding 15: Blanket Catch Absorbs Decoder Defects

- Path: `Packages/com.alrauna.amuse/Editor/Host/SourceImageAlphaReader.cs:149-153`
- One bare catch wraps the whole decode and returns route unavailable. A defect inside the reader then falls through to the clone route with no console signal.
- The repository invariant says programming failures must throw and never masquerade as unsupported input.
- Remediation: Catch only the decode failure types the route legitimately refuses. Let invariant violations propagate.

### Finding 16: lilToon Opaque Frontend Mixes Snapshot and Live Reads

- Paths: `Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonMaterialSemantics.cs:139-145, 177-186, 199-208, 648-669`, `Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonCutoutMaterialSemantics.cs:153-183`, `Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonTransparentMaterialSemantics.cs:198-230`
- The opaque analysis captures once, then reads base color, textures, scale and offset, and importer facts from the live material after the capture. Alpha reads the snapshot only. A mutation between capture and read mixes two material states in one result.
- The cutout and transparent frontends read the snapshot throughout. They live in their own files. Opaque alpha resolves to a constant from the gates, so the mix affects base color, emission, and normal facts.
- Remediation: Route the opaque reads through the captured evidence like the sibling frontends, or capture the extra facts inside the same snapshot.

### Finding 17: Two lilToon Reads Treat Absent from Capture as Feature Off

- Paths: `Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonLayerAlphaTerm.cs:352-395`, `Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonAlphaInterpreter.cs:258-283`
- The layer alpha term drops a multiplicative mask factor when the blend mask name is absent from the capture. The main texture read takes the declared-default arm when `_MainTex` is absent. Every sibling read refuses on an absent name.
- Both twin requests currently list the names, so both reads are latent. The drift direction is silent misclassification.
- Remediation: Refuse on absent names, matching the sibling reads and the fail-closed convention.

### Finding 18: Poiyomi Two Pass Gates the Second Alpha Pair with the First Family's Values

- Paths: `Packages/com.alrauna.amuse/Editor/Semantics/Poiyomi/PoiyomiMaterialSemantics.cs:1070-1085, 1637-1665`
- `IsProvenOpaqueBlend` takes four family properties but reads the alpha pair from hardcoded constants. `_SrcBlendAlpha2` and `_DstBlendAlpha2` have zero occurrences in code. The pinned investigation notes confirm the second pass declares its own pair.
- The RGB degeneration is checked independently, so the visible opacity still holds. The recorded proof rests on a property that does not govern the second pass.
- Remediation: Thread the second family's alpha pair through the gate. State the destination-alpha caveat in the evidence record.

### Finding 28: Preset Parser Lets an Integer Overflow Escape as an Unnamed Exception

- Path: `Packages/com.alrauna.amuse/Editor/Presets/PresetParser.cs:160`
- The parser converts JSON tokens to integers without a range guard. A preset value beyond the integer range produces an unnamed overflow exception instead of a named `PresetLoadRefusal`.
- Failure direction: Fail open against the closed refusal convention. The refusal enum exists and covers the malformed cases.
- Remediation: Range-check before conversion and return the matching refusal.

### Finding 29: The Polygon Clamp Cross-Field Invariant Is Missing at the Parse and Apply Boundary

- Paths: `Packages/com.alrauna.amuse/Editor/Presets/PresetParser.cs:227-240`, `Packages/com.alrauna.amuse/Editor/Presets/PresetApplier.cs:27-32`, `Packages/com.alrauna.amuse/Editor/AmuseAvatarOptimizerEditor.cs:365-389`, `Packages/com.alrauna.amuse/Editor/Build/AmusePlatformFinishPlugin.cs:1011-1036`
- The inspector normalizes the polygon clamp against the opaque percent. The build-side policy mapper re-normalizes it again at wiring time. The parser and the applier enforce no cross-field invariant.
- An applied preset can therefore store a field pair the build silently reinterprets. Two modules enforce the invariant, one module can violate it, and the build layer compensates.
- Remediation: Enforce the pair invariant once, at parse time, and delete the duplicate compensation where the repo allows.

### Finding 30: The Scalar Fold Drops an Affine Map on the False Thread Path

- Paths: `Packages/com.alrauna.amuse/Editor/Semantics/MaterialSemantics.cs:1044-1157`
- Symbols: `ScalarProductFold.Fold`
- The fold takes a `threadMaps` parameter. The true path documents that an admitted mapped factor never loses its map. The false path silently drops the map and answers a raw sample times a constant.
- All four current call sites pass true, so no live misclassification exists. The operator answers a wrong closed form instead of refusing, and nothing at the type level stops the next caller.
- Failure direction: Latent wrongly opaque once a future caller passes false with a mapped input.
- Remediation: Make the false path refuse mapped factors, or delete the parameter and thread maps everywhere.

### Finding 31: The Anisotropy Gate Misclassifies Level 1 under Forced-On Filtering

- Path: `Packages/com.alrauna.amuse/Editor/Semantics/UnityTextureEvidence.cs:146-148`
- The gate maps `anisoLevel > 1` to anisotropic. The Unity documentation special-cases only level 0 under the Forced On quality mode, which implies level 1 is anisotropically sampled in that mode.
- A texture at level 1 with a partially transparent mip can then receive the bilinear footprint proof while the hardware averages an elongated footprint.
- Failure direction: Wrongly opaque. Preconditions: the consuming project sets anisotropic filtering to Forced On. VRChat normally runs Per Texture, where level 1 is genuinely off.
- Verification: Runtime confirmation required.
- Remediation: Treat level 1 as mode-dependent, or refuse the bilinear proof for level 1 when the quality mode is unknowable.

### Finding 32: The Transient Unlock Clone Destroy Gate Checks Curves only

- Paths: `Packages/com.alrauna.amuse/Editor/Build/TransientUnlockWindowClose.cs:118-128, 296-360, 359-412`, `Packages/com.alrauna.amuse/Editor/Build/TransientUnlockWindowState.cs:103-107`
- The destroy gate sweeps committed curves for clone references. It never sweeps renderer slot arrays outside the recorded pairs. A foreign pass that assigned the clone to an unrecorded slot survives the gate, and the close destroys the clone. The built avatar then holds a missing material reference.
- The same block removes a pair even when retention fired, against the removal contract in the state class.
- Remediation: Sweep all renderer slot arrays in the gate. Delay the pair removal on retention.

---

## 4. Architectural Impurities

### Finding 19: Two Renderer Analysis Pipelines

- Paths: `Packages/com.alrauna.amuse/Editor/Analysis/AdmittedMaterialStates.cs:230-360`, `Packages/com.alrauna.amuse/Editor/Host/UnityRendererAlphaAnalysis.cs:614-689`
- The product build resolves slots through `AdmittedMaterialStates.ResolveSlot`. The tests and the census calibrate through `UnityRendererAlphaAnalysis.Analyze`. The two copies carry near-identical field scoping and resolution. Memoization is one-sided: the probe memoizes, the product path re-resolves.
- A change to one copy keeps every seam-driven test green while the shipped proof path changes. Census calibration silently invalidates the same way.
- Remediation: Make the Host probe call the product resolver, or extract the shared rule. Keep one definition and two entry points.

### Finding 20: Conversion Outcome Mapping Shipped Twice

- Paths: `Packages/com.alrauna.amuse/Editor/Build/AlphaSeparationPreparation.cs:877-907`, `Packages/com.alrauna.amuse/Tests/Editor/Build/VerifiedPoiyomiTestSeams.cs:33-77`, `Packages/com.alrauna.amuse/Tests/Editor/Build/VerifiedLilToonTestSeams.cs:408-424`
- The production inline arm maps eligibility outcomes to opaque materials. Both verified test seams re-implement the same mapping. The seam documentation itself warns against a second copy.
- Tests drive the seam. The build runs the inline arm. A new outcome value handled in production leaves the tests green.
- Remediation: Point the seams at the production mapping, or move the mapping into one shared function both call.

### Finding 21: Source Normalization and Digest Triplicated

- Paths: `Packages/com.alrauna.amuse/Editor/Semantics/NormalizedSourceHash.cs:17-44`, `Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonSourceAttestation.cs:942-967`
- The canonical class computes the normalized SHA-256 digest. The lilToon attestation keeps a private copy of the same normalize rule and the same digest rule, in the file that also delegates to the canonical class. Today the copies agree only because the canonical source is already normalized.
- A future normalization change then splits the include-tree digests from the shader and pass digests inside one attestation.
- Remediation: Delete the private pair and call `NormalizedSourceHash.Compute` everywhere in the file.

### Finding 22: Canonical Opaque Verification Skeleton Duplicated across Frontends

- Paths: `Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonOpaqueTarget.cs:55-83, 137-166, 244-305`, `Packages/com.alrauna.amuse/Editor/Semantics/Poiyomi/PoiyomiOpaqueConversion.cs:172-205, 471-500, 529-561`
- Both frontends carry identical render queue constants, near-identical non-canonical fact scans, identical read-back failure text, and the same clone, write, verify, destroy skeleton. The lilToon scan adds a transparent-mode conjunct, clone-name clearing, and a Multi keyword branch. The float tuples legitimately differ per family. The queue, tag, and read-back rule is Unity-level, not shader-level.
- A change applied to one frontend never surfaces in the other, and no cross-family test fails.
- Remediation: Extract the Unity-level skeleton into one shared seam. Keep the per-family float tuples as data.

### Finding 23: Layering Contract Drift

- Evidence: `Packages/com.alrauna.amuse/Editor/Analysis/AdmittedMaterialStates.cs:4-5` imports Host and Semantics while Host imports Analysis. `Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonOpaqueTarget.cs:231-352` and `Packages/com.alrauna.amuse/Editor/Semantics/Poiyomi/PoiyomiOpaqueConversion.cs:529-561` create, mutate, and destroy live material clones. The repository documentation says only Host touches live Unity objects.
- The purity of Analysis holds. The cycle concentrates in the evidence records living in Host while resolution lives in Analysis. The clone generation in Semantics is either a stale documentation sentence or a misplaced recipe.
- Remediation: Decide and record the ruling. Either move the clone recipe behind a Build or Host boundary, or correct the documentation sentence. Name the dependency direction the repository wants and enforce it in the layering note.

### Finding 24: Pass Count and Ordinal Documentation Drift

- Paths: repository `AGENTS.md` architecture section, `Packages/com.alrauna.amuse/Editor/Build/AmusePlatformFinishPlugin.cs:201-243`, `Packages/com.alrauna.amuse/Editor/Build/AlphaSeparationApply.cs:14-15`, `Packages/com.alrauna.amuse/Editor/Build/TransientUnlockWindowClose.cs:13-14`
- The documentation says the plugin registers three passes. The plugin registers five. Two pass summaries call themselves third and fourth. The real order makes them fourth and fifth.
- Remediation: Update the repository documentation and the two class summaries. Order words in load-bearing comments must match the registration order.

### Finding 25: Inconsistent Default-Arm Discipline in the Report Switches

- Path: `Packages/com.alrauna.amuse/Editor/Build/AmuseReports.cs:202-207, 435-438`
- `FeatureSentence` throws on an unknown enum value. `ClosureFailureSentence` returns an empty string on the same condition.
- A future closure failure value then renders an empty sentence with no error anywhere. The throw exists to prevent exactly that.
- Remediation: Throw in both switches. The closed refusal vocabularies make the default arm unreachable in practice.

### Finding 26: Shared Evidence Gates Carries a Frontend-Specific Member

- Path: `Packages/com.alrauna.amuse/Editor/Semantics/EvidenceGates.cs:4, 8-17, 152-167`
- The class comment claims one definition for all frontends. The file imports the lilToon namespace for `RecordUnknown`, and the comment on that member says the Poiyomi frontend keeps its own analog.
- Remediation: Move the frontend-specific recorder out, or correct the class comment. The class-level claim must match the members.

### Finding 27: Smaller Impurities

- `Packages/com.alrauna.amuse/Editor/Host/UnityRendererAlphaAnalysis.cs:206-211, 349-353`: the research-only semantics provider seam ships in the product assembly under a legacy name. Sanctioned, but the name invites deletion.
- `Packages/com.alrauna.amuse/Editor/Host/LiveAnimationObservation.cs:197-228`: the finite-exact check compares adjacent keyframe values, so a named non-finite refusal fires for multi-key curves. A single-key curve or an all-equal non-finite run with flat tangents takes the sources-disagree refusal instead. Soundness holds. The refusal name drifts on narrow inputs.
- `Packages/com.alrauna.amuse/Editor/Host/EffectiveMaterialMaterialization.cs:202-206`: the block state capture skips slots whose material or shader is null. The schema lookup synthesizes a schema for any shader by reflection, so the skip is narrower than a schema gap. The safety net has a small blind spot only.
- `Packages/com.alrauna.amuse/Editor/Host/UnityRendererAlphaAnalysis.cs:616-620`: the probe path classifies without the extra UV sets. Any uv1 to uv3 layer resolves unknown on the probe path. Conservative, but the defaulted parameter invites the same omission in new callers.
- `Packages/com.alrauna.amuse/Editor/Build/TransientUnlockWindowState.cs:128-140, 209-224`: the swap binding records are write-only. The close pass re-derives rewrites by scanning clip values. Dead state that future maintainers will assume is load-bearing.
- `Packages/com.alrauna.amuse/Tests/Editor/Host/UnityMaterialEvidenceCaptureTests.cs:1330-1336`: a private reference equality comparer duplicates the production one.
- `Packages/.gitignore`: one duplicate whitelist line for the VRChat core package pattern.

### Compact Findings 33 to 51

These entries came from the same scout audits. The integrator checked their wiring only where the text says so. Each names the mechanism and the fix shape.

- Finding 33: `Packages/com.alrauna.amuse/Editor/Semantics/UnityMaterialSemantics.cs:1000-1013, 1024-1057`. The transferred-analysis `verifyIdentity` parameter gates the lilToon verifies but not the Poiyomi verifies. The parameter documentation claims one rule for all families. Fail-closed today only because consent never grants Poiyomi names. Fix: honor the parameter in every family arm.
- Finding 34: `Packages/com.alrauna.amuse/Editor/Semantics/PackageVersionReader.cs:19`. The offline package list call omits indirect dependencies, so a producer installed only as another package's dependency refuses admission. Over-refusal, not misclassification. The 2022.3 request type implements no dispose interface, so no cleanup fix exists. Fix: request indirect dependencies.
- Finding 35: `Packages/com.alrauna.amuse/Editor/Semantics/GeneratedTextureAttestation.cs:71-82`. The persisted-copy arm admits on the name suffix plus an installed admitted producer version, without the registry conjunct the in-memory arm requires. The arm still requires a sub-asset inside a container, so the exposure is narrower than any named texture. Bounded exposure: every route fact is still read from the real texture. Fix: add the registry or shape conjunct.
- Finding 36: `Packages/com.alrauna.amuse/Editor/Semantics/AaoAtlasTextureAttestation.cs:153-172`. The atlas corroboration is material-granular. It does not tie this texture to atlas production. The design comments already declare a temporary admission with an upstream retirement path. Recorded for completeness.
- Finding 37: `Packages/com.alrauna.amuse/Editor/Semantics/Poiyomi/PoiyomiOpaqueConversion.cs:284-305, 420-463`. The NaN sweep runs after the already-opaque classification. A `_Cutoff` of NaN fails the greater-than comparison and admits the material as already opaque. Contained: nothing mutates. Fix: run the finiteness sweep first.
- Finding 38: `Packages/com.alrauna.amuse/Editor/Semantics/Poiyomi/PoiyomiMaterialSemantics.cs:2595-2640`. Attestation reads and hashes the full pinned shader source once per material per path, with no memoization of a pure function of an immutable asset. Cost only. Fix: cache by asset path and mtime.
- Finding 39: `Packages/com.alrauna.amuse/Editor/Semantics/Poiyomi/PoiyomiMaterialSemantics.cs:2004-2098`. The bound-mask route skips the capture-refusal check its sibling routes perform, so a failed red capture yields a generic refusal instead of the named mask diagnostic. Fix: mirror the sibling check.
- Finding 40: `Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonAlphaMaskSemantics.cs:258-272, 300-332`. Two mask cases over-refuse: an unassigned mask with a sub-one constant factor, and an assigned mask with scale zero that still demands texture identity. Coverage defects only. Fix: admit the exactly representable constants.
- Finding 41: `Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonSourceAttestation.cs:1262-1281`. The LTCGI token strip matches any Tags line, not only the SubShader line its proof scopes. Theoretical while the GUID and digest pins hold. Fix: anchor the match to the SubShader block.
- Finding 42: `Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonOpaqueTarget.cs:231-352`. The clone creation promises no leaks for three named failures. An unexpected throw before the guaranteed destroy points, for example a capture request defect, orphans the clone. Fix: wrap the body after creation in try and finally.
- Finding 43: `Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonSourceAttestation.cs:1690-1760`. The profile lookup defaults unknown shader names to the opaque profile. The family verify refuses them afterward. A future caller that skips the verify gathers opaque-shaped evidence for an unrelated shader. Fix: return a refusal value instead of a default profile.
- Finding 44: `Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonMaterialSemantics.cs:302-313, 841-854`. The base color and emission paths check finiteness before `Color.linear` but never after. A negative component may produce NaN depending on the Unity implementation. The downstream constant factories validate finiteness, so a post-conversion NaN aborts the analysis with an exception instead of silently poisoning a result. Whether NaN occurs at all remains Unity-implementation-dependent and unprobed. Runtime confirmation required. Fix: re-check after the conversion and refuse the tint.
- Finding 45: `Packages/com.alrauna.amuse/Editor/Presets/PresetFileStore.cs:58-60`. The store resolves shipped presets through the package asset path. A package installed through a file reference produces an absolute path that the asset loader rejects. This path behavior is an inference from the API contract. It needs one runtime probe on a file-installed package. A failed load also returns a partially filled list. Fix: handle the file-reference layout and clear the list on failure.
- Finding 46: `Packages/com.alrauna.amuse/Editor/AmuseAvatarOptimizerEditor.cs:83-87, 292-346, 471-493`. The inspector silently rewrites stored out-of-range values on a plain repaint. A stored mip cap of 15 becomes 10. Classification then stops consulting mips 11 and above of large textures. That direction is less conservative and happens without user consent, so the rewrite is not cosmetic. The rewrite path also carries a SerializedProperty setter nuance that the origin scout flagged for one runtime confirmation. Two further items are cosmetic: per-repaint GUI style allocations, and preset row caching. Fix: stop the silent rewrite or surface it, batch the styles.
- Finding 47: `Packages/com.alrauna.amuse/Editor/Build/TransientUnlockWindowClose.cs:116, 290-323`. The close re-enumerates committed clips per pair although the pass already materialized the list once. Duplicate walks and two clip universes in one pass. Fix: pass the materialized list down.
- Finding 48: `Packages/com.alrauna.amuse/Editor/Analysis/AlphaSemanticsResolver.cs:404-409`, `Packages/com.alrauna.amuse/Editor/Analysis/AffineUvTransform.cs:118-124`. Two byte-identical identity predicates decide whether the resolver transforms and whether the transform transforms. A future tolerance added to one silently desynchronizes the envelope from the transform. The same family of impurity extends further: the bit length, rational conversion, and dyadic helpers are hand-rolled in three files, `ExactUvGeometry.cs` around line 424, `AffineUvTransform.cs` around line 205, and `AffineAlphaMap.cs` around line 100, with only comments to keep their normalization behaviors apart. Fix: one predicate and one helper set, all consumers shared.
- Finding 49: `Packages/com.alrauna.amuse/Editor/Analysis/TriangleAlphaClassifier.cs:679-682, 940-944, 1179-1183, 1533-1536` against `TriangleAlphaClassifierTests.cs` around line 1140. Four method summaries say equality refuses at the density bound. The code and the pinned test implement at-or-under. Documentation drift on a policy boundary that invites a wrong flip. Fix: correct the four summaries.
- Finding 50: `Packages/com.alrauna.amuse/Editor/Analysis/AlphaSemanticsResolver.cs:546-573, 563-573, 640-660`. A NaN multiplier takes three different code paths. The constant arm refuses as unsupported. The scaled-sample arm treats it as below one. The product chain arm treats it as exactly one and can resolve a NaN alpha as proven opaque. The public factory validates finiteness today, so the third path is unreachable through it. The inconsistent fail-open fallback remains a defect for any future construction path. Fix: refuse NaN in every arm.
- Finding 51: `Packages/com.alrauna.amuse/Editor/Analysis/ExactUvGeometry.cs:455` against `Packages/com.alrauna.amuse/Editor/Analysis/TriangleAlphaClassifier.cs:991-1002`. The exponent seed of -1 in the texture-scaled domain construction is load-bearing. BigInteger division truncates, so the half-texel computation is exact only when the texel scale is at least 2. The seed guarantees that. A cleanup to the integer maximum makes the half texel zero, collapses the repeat interval to an empty range, and returns false proven opaque. The seed carries no comment. Fix: document the seed as load-bearing and pin it with a test on integer UV coordinates.

---

## 5. Corrections the Integrator Applied to the Scout Claims

- The Analysis scout raised a conditional about the red-field capture bounds: if the capture always pinned inert bounds, its finding would reduce to a documentation defect. The integrator read the capture wiring and resolved the conditional to the defect side. The alpha channel carries caller bounds with an inert force only under a cutoff below one. The red channel carries the caller's active bounds with no force. Finding 6 part A is therefore a live defect, not a documentation note.
- The Semantics root scout and the Host scout each reported the red-field bound space problem separately, with different severities. The integrator merged both into Finding 6. One fix covers both parts.
- The lilToon Multi scout reported the missing predicate case and the missing capture schema as independent defects. The integrator kept them separate as Findings 1 and 2 because they act at different lifecycle points. Finding 2 misclassifies at classification time. Finding 1 crashes at conversion admission time.
- One scout claim about a pinned test was narrowed. The pinned saturating sum test covers the mixed-fields case. The unsound arm in Finding 7 is the both-transparent case. The adversarial review located the pinned test and confirmed that it bakes the absorbing arm. The fix must update it.
- Finding 23 merges three scout claims: the lilToon layering note, the architecture scan namespace cycle, and the architecture scan live-clone note. One ruling covers all three. The merge dropped one detail that this sentence restores: `LilToonOpaqueTarget.cs:424-431` exposes a process-global mutable delegate that swaps production attestation for tests. That seam is part of the same ruling.
- Finding 32 keeps its Section 3 placement although the origin scout called it low. A destroyed clone that a built avatar still references is a missing-material corruption, which the integrator judged medium. The record retitled the finding to name the transient unlock window. The window covers every recognized locked material, not only Multi.
- Finding 40 carries the scout's medium label in compact form. The scout itself bounded the finding as over-refusal with no misclassification, so the compact register keeps the substance.

---

## 6. Traced and Found Sound

The scouts recorded explicit sound statements for the areas they cleared. The integrator spot-checked the highest-risk statements. Bullets marked Integrator carry observations the integrator made directly; the rest summarize the scouts.

- The exact rational and dyadic core in `ExactUvGeometry` and `AffineUvTransform`: decode, encode, hull, clip, interval semantics, repeat normalization. One landmine survives inside this core as Finding 51: the load-bearing exponent seed carries no comment.
- The attestation conjunctions in both frontends: name, GUID, package, digest, lock, and schema arms all fail closed.
- Property-name parity between every interpreter read and both twin evidence requests on the regular lilToon paths.
- Integrator: the apply pass internal ordering: validation before finalization, sweep before registration, curve edits before mesh, mesh before materials. The integrator read the whole apply pass in this session and confirmed the order.
- The lifecycle gate version grammar and the consent flow, including the consent-never-overrides-refusal rule.
- Resource lifetimes in the GPU capture core: temporary render textures and clone materials release on every exception path.
- The preset subsystem schema against the three shipped preset files, with no drift.
- Meta file pairing across all production and test directories, with no orphans.

---

## 7. Suggested Order of Work

1. Findings 1 and 2 together. Both are Multi capture plumbing. The fix is small, and Finding 1 removes a guaranteed build crash.
2. Findings 3, 5, and 10. Bounded wrong-opaque defects with named gates.
3. Finding 6. One seam, two contract breaks, needs the part B probe first.
4. Finding 4. Proof walk soundness. The fix needs care and a dedicated falsifier test.
5. Finding 7. Proof discipline plus pinned test updates.
6. Findings 9, 11, 12, and 13. Build ordering and gating gaps.
7. Findings 14 to 18. Medium defects, each small.
8. Findings 19 to 27. The architecture dedup and documentation batch.

Every fix follows the RED to GREEN discipline. A defect in this record that a falsifier cannot reproduce stops work on that defect until the mechanism is understood.

---

## 8. Adversarial Review Record

Four parallel reviewers checked this record on 2026-10-09 against the working tree at `fae7e7e`. Two reviewers verified the high-priority findings against the code. One reviewer verified the medium, architecture, and compact findings plus the correction section. One auditor compared this record against the eight original scout reports. The integrator applied every correction on the same day.

### Verdict Summary

| Reviewer | Scope | Confirmed | Correction needed | Refuted |
|---|---|---|---|---|
| First code reviewer | Findings 1 to 5 | 5 | 0 | 0 |
| Second code reviewer | Findings 6 to 10 | 5 | 0 | 0 |
| Third reviewer, detailed findings | Findings 11 to 32 | 17 | 5 | 0 |
| Third reviewer, compact entries | Findings 33 to 49 | 16 | 1 | 0 |
| Third reviewer, correction section | Section 5 bullets | 3 | 1 partial | 0 |
| Fidelity auditor | Whole record | 6 clean areas | 14 issues | 0 |

No finding of this record was refuted. No failure direction was wrong after the corrections below.

### Corrections the Review Produced

- Finding 3: the invariance test asserts semantic-output invariance. That claim stays true after a conversion-side admission gate. The test list need not change. The record now says so.
- Finding 6: the verification narrative cited convenience overloads for the inert pinning. The production pinning sits inside the capture core, conditional on a cutoff below one. The red-channel defect is unaffected. The record now states the correct site.
- Finding 10: the review added the platform scope notes. Desktop normal map encoding puts the normal x component in the alpha channel. Mobile-style encodings and BC5 imports keep alpha at 1, so the predicate conclusion coincidentally holds there. The record now carries the note.
- Finding 11: the review corrected the guard citations. The real null guards sit in the transient unlock code at two other sites. The record now cites them and marks the null case latent.
- Finding 16: the review corrected the capture and dispatcher citations and located the sibling frontends in their own files. The record now cites the right ranges.
- Finding 19: the review found memoization one-sided between the two pipelines. The record now says so.
- Finding 22: the review found the fact scans near-identical rather than identical. The lilToon scan adds three conjuncts. The record now says so.
- Finding 26: the review corrected two line citations. The record now cites them.
- Finding 27: the review corrected two sub-bullets. The finite-exact check does compare adjacent keyframe values, so the named refusal fires for multi-key curves. The block-state skip fires on null material or shader, and the schema lookup synthesizes schemas for any shader. The record now carries both corrections.
- Finding 32: the review corrected the removal-contract citation. The record now cites it.
- Finding 34: the review found that the 2022.3 request type implements no dispose interface. The record dropped the disposal half of the fix.
- Finding 35: the review narrowed the exposure sentence. The arm still requires a container sub-asset plus an admitted version. The record now says so.
- Finding 44: the review corrected the citations and the consequence. Downstream factories validate finiteness, so a post-conversion NaN aborts the analysis instead of silently poisoning it. The record now carries the corrected consequence.
- Fidelity: the auditor found two scout findings missing from the first draft of this record. The integrator added them as Findings 50 and 51. The auditor found a wrong Multi qualifier on the transient unlock finding, a reversed direction claim on the inspector finding, missing runtime-confirmation flags on three findings, an undocumented three-way merge behind Finding 23, one wrong test path, and several wording drifts. The integrator fixed all of them and documented the placement decisions in Section 5.

### Remaining Runtime Probes

These claims need one execution probe before their fixes land. The list is the record's own, confirmed by the review.

- Finding 2: whether the transparent resolution misclassifies or over-refuses under the binarized field.
- Finding 6 part B: the one-texel Load against sample comparison for an sRGB mask.
- Finding 10: the per-texel blit probe for the alpha equals normal-x claim.
- Finding 11: whether Unity can return null for an enumerated object curve binding.
- Finding 13: the cross-renderer rewrite reachability after a reparent.
- Finding 31: anisotropic sampling of level 1 under Forced On.
- Finding 44: whether `Color.linear` produces NaN for negative components in 2022.3.
- Finding 45: the asset path shape for a file-installed package.
- Finding 46: the SerializedProperty setter equality nuance in the silent rewrite path.
