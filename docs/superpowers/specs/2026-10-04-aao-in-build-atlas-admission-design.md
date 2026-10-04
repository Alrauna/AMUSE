# In-build AAO atlas admission into the alpha evidence capture

Date: 2026-10-04.
Branch: `fix/aao-optimize-texture-analysis`.
Investigation: `docs/superpowers/investigations/2026-10-04-aao-optimize-texture-atlas-refusal-investigation.md`.
Plan: `docs/superpowers/plans/2026-10-04-aao-in-build-atlas-admission.md`.
Design status: approved for implementation on 2026-10-04. No production code changed before this record.

Scope: admission of the in-memory atlas textures that the Optimize Texture setting of Avatar Optimizer (AAO) 1.9.17 generates during an NDMF build, their evidence identity, their capture route, the capture-time origin-material corroboration, the session lifecycle, one report hint sentence, and the code comments that record what an upstream AAO contribution would let AMUSE delete.

## 1. Problem

AAO 1.9.17 generates each texture atlas as a plain in-memory `Texture2D`. The object has no asset path and no NDMF object-registry entry. AMUSE runs at PlatformFinish, before NDMF persists in-memory textures, so the capture's identity gate refuses every atlas with `UnavailableCapture` and the reason clause `has no source identity`. The slot then resolves `MissingTextureEvidence`, no triangle is proven, and the report reads as a `_MainTex` support failure even though the lilToon `_MainTex` semantics are fully supported.

The persisted shape is already admitted. `GeneratedTextureAttestation.TryIdentifyProducer` reads an AAO atlas that NDMF persisted as a sub-asset of an `SubAssetContainer` (`GeneratedTextureAttestation.cs:64-73`). That branch can never fire inside a live build. The LAC admission of 2026-10-03 and 2026-10-04 built the in-memory machinery, but its conjuncts require a registry pair from copy to source, and AAO registers nothing for atlases.

## 2. Non-goals

- No change to the lilToon or Poiyomi frontends. An admitted atlas enters through the existing texture-backed arm.
- No change to the capture gate order, the format allowlist, the mip-limit gate, the dimension gate, or the build-target gate.
- No upstream change to AAO, and no AMUSE ordering edge against the AAO plugin. The upstream ask lives only in code comments (section 8).
- No admission for atlases from uncharacterized AAO versions, for textures without the pinned name markers, or for textures whose slot material does not corroborate the producer.
- No build caching keyed on the new identity form. The 2026-09-19 caching deferral stands.

## 3. Facts this design rests on

Producer shape, measured in the installed AAO 1.9.17 source and dated 2026-10-04:

- `T&O: OptimizeTexture` runs in NDMF Optimizing and mutates already cloned materials in place through `SetTexture` (`Editor/Processors/TraceAndOptimize/OptimizeTexture.cs:470-472`).
- The atlas name is the source name plus `" (AAO UV Packed)"`, or `"AAO Monotone <color> <space>"` for a collapsed solid color (`OptimizeTexture.cs:971`, `:956-964`).
- The atlas is never saved and never registered. The material clone it lands on was registered one hop to the authoring material by the earlier `DupliacteAssets` pass (`Editor/Processors/DupliacteAssets.cs:91-94`).
- Format, wrap, filter, anisotropy, mip bias, streaming flags, and sRGB mirror the source texture. Atlasing requires an identity UV matrix, so the slot's `_MainTex_ST` stays identity.

AMUSE current state on this branch:

- The identity gate refuses every path-less texture except an admitted LAC replacement copy (`Semantics/UnityTextureEvidence.cs:39-48`).
- The route selector `GeneratedTextureAttestation.TryIdentifyRouteTexture` sends container-backed producers and admitted LAC copies to the generated capture route (`GeneratedTextureAttestation.cs:115-136`). The generated route runs before the streaming route (`Host/UnityAlphaFieldEvidence.cs:240-279`), so a mirrored streaming flag is harmless.
- The batch capture resolves shared texture evidence in a pre-pass that calls `TryGetSourceId` per texture, then captures each shared bucket once (`Host/UnityMaterialEvidenceCapture.cs:937-1003`). The per-input live material is `MaterialEvidenceCaptureInput.SourceMaterial` (`:293`).
- `TryProveSampledAlphaIsOne` proves an exact-one sample for route-admitted textures whose format names no alpha channel (`UnityTextureEvidence.cs:176-198`).

## 4. Design

### 4.1 The admission basis

A texture is an admitted in-build AAO atlas when every conjunct holds. Any miss refuses.

| # | Conjunct | Refuses when unmet because |
|---|---|---|
| C1 | The object is a live `Texture2D` with no asset path | an asset-backed texture reads through the ordinary asset rules, not this admission |
| C2 | The name ends with `" (AAO UV Packed)"` or starts with `"AAO Monotone "` | the markers are the pinned 1.9.17 producer shape |
| C3 | The producer package `com.anatawa12.avatar-optimizer` is installed and its version is in the admitted set | an uncharacterized version may change the shape |
| C4 | The capture supplies the slot's live origin material, the registry resolves it one hop to a `Material`, and that material has a non-empty asset path | a name marker alone is weaker evidence than the LAC registry pair, so the registered `DupliacteAssets` clone pair corroborates the producer |

C1 through C3 form the shape predicate. C4 is the corroboration conjunct and runs only at the identity gate.

### 4.2 The two-level predicate split

The shape predicate (C1 to C3) is texture-only. It serves the route selector, the route re-check in `UnityGeneratedTextureEvidence`, the color-interpretation read, and the sampled-alpha-one proof, exactly where the LAC route arm serves today. The full conjunct (C1 to C4) guards the identity gate in `UnityTextureEvidence.TryGetSourceId`.

The split is sound because every capture path passes the identity gate before any route: `UnityAlphaFieldEvidence.TryCapture` resolves the source id at gate 3 (`UnityAlphaFieldEvidence.cs:185-189`) and dispatches routes only afterwards (`:240`). A texture that fails C4 never reaches a route. The texture-only fact reads read live object state, which the persisted branch already treats as sound for name-marker-admitted objects.

### 4.3 The identity form

The identity gate mints `unity-aao-atlas:<instance-id>` from the live object's own instance id. The value is per-build by design and never names a project asset. No claim table and no poison table are needed, because the value derives from the object itself: distinct objects cannot collide, and one object mints one id for the whole build. This contrasts with the source-keyed `unity-replacement:` form, which needs its duplicate guard (`ReplacementTextureIdentity.cs:7-12`). A monotone atlas shared by several materials therefore mints one id under every corroborated slot.

### 4.4 Origin-material threading

`MaterialEvidenceCaptureInput.SourceMaterial` already carries the live slot material into the batch capture. The design threads it to the identity gate:

- `UnityTextureEvidence.TryGetSourceId(Texture, out TextureSourceId, Material originMaterial = null)` gains a trailing optional parameter. Existing callers are unchanged.
- `UnityAlphaFieldEvidence.TryCapture(...)` gains the same trailing optional parameter and forwards it to the identity gate.
- The private `CaptureTexture` in `UnityMaterialEvidenceCapture` gains a required origin-material parameter. The pre-pass passes `input.SourceMaterial`; the shared bucket stores the first material under which the identity resolved; the per-material fallback passes the builder's material, which `CaptureAssignments` fills from `input.SourceMaterial`.
- A foreign material that consumes an admitted atlas without a registry pair gets no identity, so its own slot refuses. The corroborated slots keep their already captured evidence.

### 4.5 The version seam

A new `AaoAtlasTextureAttestation` mirrors the LAC seam: a substitutable `ReadInstalledPackageVersionOrNull` provider, one cached read per capture session, `SetAdmittedVersionsForTests`, `ResetForTests`, and `ClearVersionCacheForSession`. The offline package-manager reader is extracted once and shared with `ReplacementTextureAttestation`, which keeps its seam and behavior unchanged.

The shipped admitted set is `{"1.9.17"}`. The dated characterization is the 2026-09-27 static census plus the 2026-10-04 investigation, both pinned to the installed 1.9.17 source, and the Lab builds that exercised the refusal end to end. A later AAO version refuses until a dated characterization admits it.

### 4.6 Route and lifecycle

The route selector gains one arm after the LAC arm: a shape-admitted `Texture2D` answers `GeneratedTextureProducer.Anatawa12AvatarOptimizer` and takes the generated route. The generated route blit-captures the live object per resident mip, so the mirrored streaming flag and the mirrored format ride along. The persisted-branch name literals move to the shared constants so one definition names the markers.

`ClearVersionCacheForSession` joins the two existing session-clear sites (`AlphaSeparationApply.cs:911-913`, `AmusePlatformFinishPlugin.cs:403-405`). The identity mint keeps no session state, and its doc comment says why.

### 4.7 The report hint

The `amuse.texture.UnavailableCapture:hint` gains one sentence after the uncharacterized-version sentence: an atlas from a characterized version of Avatar Optimizer reads through the generated route, so its slot keeps its proof. The sentence separates the remaining refusals, which name uncharacterized versions or foreign shapes, from the admitted path.

Note dated 2026-10-04: the hint sentence states the corroborated-slot case. A foreign material consuming an admitted atlas still refuses, because the corroboration conjunct binds to the slot material.

## 5. Gates that keep refusing

- The format allowlist refuses a mirrored BC5, ASTC, or crunched format with `UnsupportedFormat`.
- The mip-limit gate refuses an atlas whose levels the active limit removes.
- Unequal or mirror wrap modes refuse at sampling, as today.
- An atlas whose format names no alpha channel, such as DXT1 or RGB24, proves an exact-one sampled alpha through the existing route arm, exactly as a LAC copy does.

## 6. Tests and falsifiers

Tests never install AAO and never read the installed environment. Every test substitutes the version provider and pins the admitted set. Registry fixtures follow the shipped pattern: save `ObjectRegistry.ActiveRegistry`, install `new ObjectRegistry(null)`, register, restore in `finally`.

1. Version: the shipped set contains only `1.9.17`; another version refuses; a null or unreadable version refuses.
2. Name: a path-less texture without a marker refuses, even under an admitted version.
3. Path: an asset-backed texture with a marker never takes the in-memory admission; the persisted branch still governs it. The existing pins `LooseInMemoryTexture_IsRefused` and `TryGetSourceId_SceneOnlyTexture_IsRefused` stay green and unchanged.
4. Origin: the shape holds but no origin material is supplied, so the identity refuses.
5. Origin: the origin material is unregistered, or resolves to a non-asset material, so the identity refuses.
6. Corroboration: a registered clone material plus an admitted atlas mints `unity-aao-atlas:`, and `TryCapture` captures a chain through the generated route with the streaming flag on and off.
7. Format: a BC5 atlas refuses `UnsupportedFormat` while every other conjunct holds.
8. Batch: a registered clone cutout material over an opaque atlas classifies its region `ProvenOpaque` at its own cutoff, and an unregistered clone over the same atlas stays unknown.

## 7. Effect

A lilToon slot whose `_MainTex` is an AAO 1.9.17 atlas becomes analyzable when the atlas format is admitted, the residency fact holds, and the slot material corroborates the producer. Triangles over it prove opaque exactly when the atlas alpha field and the alpha policy say so. Slots over BC5, ASTC, crunched, or uncharacterized-version atlases stay conservative, and each refusal keeps naming its first failing gate.

## 8. The upstream path, recorded only in code comments

No AMUSE change asks AAO to change. Each change site carries one code comment instead. The comment states what an upstream AAO release that improves the attestability of its generated textures would let AMUSE delete, and each fact names the AMUSE code that fact would retire. If and when AMUSE contributes to AAO, the contribution target is registration of each generated atlas in the NDMF object registry, or persistence of the atlases into a build container during the Optimizing phase. Either fact would let AMUSE delete most or all of the following:

- the in-memory admission in `AaoAtlasTextureAttestation`, including its version seam, because the registry pair or the persisted container would carry the producer identity;
- the `unity-aao-atlas:` identity mint in `AaoAtlasIdentity`, because a registered or persisted atlas reads through an existing identity form;
- the AAO arm in `UnityTextureEvidence.TryGetSourceId` and the origin-material corroboration, because the registry would corroborate the texture itself;
- the shape arm in `GeneratedTextureAttestation.TryIdentifyRouteTexture`, replaced by the existing persisted branch.

The persisted-container branch in `GeneratedTextureAttestation` stays either way. Until such a release exists, the comments are the whole upstream ask.

## 9. Risks

- A foreign producer can forge the name markers while an admitted AAO version is installed. The corroboration conjunct requires a registered slot material, so a forged texture must also land on a cloned material. The residual risk is accepted and dated 2026-10-04, matching the dated acceptance in the LAC design.
- An AAO update changes the shape and refuses every atlas until a new dated characterization admits the version. That is the intended fail-closed direction.
- Unity can reuse an instance id after an object is destroyed. The atlas stays referenced by the captured material for the whole capture, so the id stays unique where it is used. The id is per-build and never persisted.
- A monotone atlas shared across slots mints one id. That is correct: one object is one evidence source.

## 10. Verification

RED before GREEN for every behavior change, with the failing test names and messages recorded. New-type tests observe the compile failure as the RED step. Focused EditMode runs cover `AaoAtlasTextureAttestationTests`, `UnityTextureEvidenceTests`, `GeneratedTextureAttestationTests`, `UnityGeneratedTextureEvidenceTests`, `UnityAlphaFieldEvidenceTests`, `UnityMaterialEvidenceCaptureTests`, `ReplacementTextureAttestationTests`, and `AmuseReportStringsTests`. The closing run covers the full `Alrauna.Amuse.Tests.Editor` assembly. A filtered run that reports 0 tests is a failure. A Lab build after merge characterizes the end-to-end effect and is not a merge gate.
