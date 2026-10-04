# Attested Build-Replacement Texture Capture Design

Date: 2026-10-03
Status: approved on 2026-10-03, with the section 12 correction that the
upstream ask lives only in code comments at the change sites.
Scope: admission of Limitex Avatar Compressor (LAC) replacement textures into
the alpha evidence capture, their evidence identity, and the code comments
that record the upstream shape which would remove this admission.

## 1. Problem

Avatars with the LAC Texture Compressor component produce materials AMUSE
cannot analyze. The compressor runs in the NDMF Optimizing phase. It clones
every material, then replaces every processed texture with a compressed
in-memory copy. AMUSE runs at PlatformFinish and reads the post-LAC state.

The AMUSE identity gate refuses every replacement copy. The copy has no asset
path, so `UnityTextureEvidence.TryGetSourceId` cannot pin it, and the capture
refuses with `UnavailableCapture` before any other gate. The generated-texture
producer allowlist cannot rescue the copy, because the shipped admission
requires a persisted sub-asset inside an attested container. Every triangle over a
refused texture answers Unknown. No triangle is proven opaque, and the
material is unanalyzable.

## 2. Measured evidence

All facts below are measured or read from source, and the 2026-10-02
investigation record carries the full citations.

- LAC registers one NDMF plugin, `dev.limitex.avatar-compressor.texture`, in
  the Optimizing phase, after Modular Avatar and before TexTransTool and
  Avatar Optimizer. AMUSE runs at PlatformFinish.
- The pass clones every collected material, registers each clone in the NDMF
  object registry, and repoints every renderer slot at the clone. Clones keep
  the source shader, so shader attestation and frontend selection are
  unaffected.
- For each processed texture the pass resizes into a new in-memory
  `Texture2D`, compresses it with `EditorUtility.CompressTexture`, forces the
  streaming mipmaps flag on through serialization, registers the pair of
  source and copy in the object registry, and assigns the copy into the
  cloned material. The copy keeps the source name plus a `_compressed`
  suffix. It is never saved as an asset.
- The desktop format selector produces DXT1, DXT5, BC7, and BC5. The mobile
  selector produces ASTC 4x4, 6x6, and 8x8. Frozen settings can force BC5 and
  ASTC on any platform.
- The copy's mip chain mirrors the source mip count. The copy's color space
  flag mirrors the source for color textures and is forced linear for normal
  maps. Wrap, filter, and anisotropy settings are copied from the source.
- The AMUSE gate order is: channel, texture type, source identity, build
  target, format allowlist, mip residency, dimensions, generated route,
  streaming clone route, direct GPU route. The generated route blit-captures
  the live resident object and re-checks the producer attestation inside
  itself. The route order places the generated route before the streaming
  route.
- `RegisteredSourceIdentity.Resolve` resolves a build-copy object to its
  registered source through a read-only registry lookup. An unregistered
  object answers null.
- A focused characterization run on 2026-10-02 passed 31 of 31 EditMode
  tests across `UnityTextureEvidenceTests` and `GeneratedTextureAttestationTests`.
  `TryGetSourceId_SceneOnlyTexture_IsRefused` pins the exact LAC copy shape
  as refused by the shipped gate.
- The installed LAC version history is unclear upstream. The public main
  manifest read 0.1.0 on 2026-10-02, while the Census Lab project recorded
  0.9.0 installed on 2026-09-27. Admission therefore starts from an empty
  version set and gains only characterized versions.

## 3. Scope and non-goals

In scope:

- One new admission class for build-replacement textures, gated on the
  version-pinned LAC shape approved on 2026-10-03.
- The evidence identity form for replacement copies.
- Route selection and per-fact proof extensions for the new class.
- The package-version seam and its admitted version set.
- The report hint update.
- Code comments at the change sites that record what an upstream LAC
  release would let AMUSE delete. The ask lives in those comments and
  nowhere else.

Non-goals:

- No format allowlist changes. DXT1, DXT5, and BC7 are admitted in the
  shipped allowlist. BC5 and ASTC stay refused with `UnsupportedFormat`.
- No new consent surface. The AAO and VRCFury producer rows carry no consent
  gate, and this row follows that precedent.
- No change to shader attestation, frontend selection, or material-clone
  handling.
- No change to the exact-one opacity bar, the mip cap policy, or the
  refusal vocabularies.
- No canonical normal-map import proof for copies. That proof is
  importer-only and stays refused for copies.

## 4. Design questions

1. What conjunction of facts admits an in-memory replacement copy without
   trusting a single forgeable marker?
2. What identity pins the copy for the evidence layer?
3. Which capture route reads the copy, and what does the forced streaming
   flag do to route selection and residency?
4. Which per-fact proofs extend to copies, and which stay refused?
5. How does AMUSE pin the producer version when the copy has no asset path?
6. What upstream behavior would let AMUSE delete this admission?

## 5. Constraints

- Fail closed. Every unmet conjunct refuses with the existing vocabulary.
- Never trust bytes. Identity pins build instances, never content hashes.
- Never mutate source assets. The registry lookup is read-only.
- Capture the shipped pixels. The copy is what playback samples, so the
  proof must read the copy, never the source.
- No absolute paths, no host names, no private identifiers in this spec.
- Simplified Technical English for every human-read sentence.

## 6. Approved basis: the version-pinned LAC shape

The approved basis is a conjunction. A texture is an attested build
replacement when every fact below holds. Any unmet fact refuses.

1. The texture is a live `Texture2D` with no asset database path.
2. The name ends with `_compressed`.
3. The read-only registry lookup resolves the texture to a source
   `Texture2D` that has an asset path. The source must not itself be an
   in-memory object, so chains of replacements admit one hop only.
4. The producer package `dev.limitex.avatar-compressor` is installed, its
   version is readable, and the version is in the admitted set.

The admitted version set is pinned, characterized data. It started empty.
On 2026-10-03, 0.9.0 joined it through the dated characterization: the
installed Census Lab copy's source shows the admitted shape, meaning copies
named with the source name plus the `_compressed` suffix, registered
source-and-copy pairs in the object registry, the forced streaming mipmaps
flag, and desktop formats DXT1, DXT5, BC7, and BC5; and a Lab build
exercised the refusal path end to end before admission. Any other version
refuses until it is characterized and dated the same way. A version that
cannot be read refuses, following the lilToon baker precedent for an
unreadable version constant.

## 7. Identity for replacement copies

Today `TextureSourceId` has one form, `unity-asset:<guid>:<localId>`. This
design adds a second form, `unity-replacement:<sourceGuid>:<sourceLocalId>`,
where the guid and local id come from the resolved source asset texture.

- The form is minted only by the admission predicate in section 6, so an
  asset-backed texture can never carry it and a path-less texture can carry
  nothing else.
- One LAC pass produces one copy per source texture, and the pass repoints
  every reference at that one copy. So one source id maps to one copy object
  per build. The capture layer enforces the invariant: when a second
  distinct object mints an already-used replacement id, the second claimant
  refuses at mint, and every object carrying that id fails closed at the
  resolution gate through the caller's existing missing-evidence path.
- The id repeats across builds by design. The evidence cache is build-scoped
  and cleared per build, and nothing persists, so the repeat is harmless.

## 8. Capture route

A unified admission predicate replaces the two current attestation call
sites in the route selection and the route's internal re-check. The
predicate answers one of: container-backed generated texture (AAO,
VRCFury), replacement copy (LAC), or none.

- A replacement copy takes the generated route. The route blit-captures the
  live resident object per mip with async readback, which is exactly the
  proof surface: the copy is what the runtime samples.
- The route order already places the generated route before the streaming
  route, so the forced streaming flag on a copy never reaches the clone
  route.
- Characterized on 2026-10-03 by a live probe on a runtime RGBA32 mipmapped
  copy carrying the forced flag: the requested level reads loaded and the
  loaded level reads 0, so the route's residency predicate passes and
  admitted copies capture through the generated route. The earlier
  fail-closed default for this shape is superseded by this measurement.
- Capability gates, mip residency degradation, dimension gates, and the
  session cache keys are unchanged.

## 9. Per-fact proof extensions

- Source identity: section 7.
- Color interpretation: the existing generated-texture branch reads the
  live object's graphics format and data color space. The unified predicate
  extends that branch to replacement copies. LAC copies the source color
  flag, so the read fact matches the source intent.
- Sampling facts: filter, wrap, and anisotropy read live object state and
  need no extension. The shipped refusal of mirror wrap stays.
- Sampled alpha exactly one is importer-only in the shipped code. The
  design extends it to attested route textures, meaning container-backed
  generated textures and replacement copies, whose graphics format has no
  alpha component, measured through the same route, initially DXT1 and
  RGB24. A container-backed clone in a no-alpha format gains a provable
  answer it lacked, and the sampling fact is the same one the RGB24
  exemption rests on. This widening past replacement copies was ruled
  deliberate during the final review on 2026-10-03.
- Format allowlist: unchanged. The copy's `TextureFormat` must be in the
  existing admitted set, so BC5 and ASTC copies refuse with
  `UnsupportedFormat`.
- Canonical normal-map import: stays importer-only. A replacement copy
  cannot prove it, and normal-map facts on copies answer conservatively.

## 10. Package-version seam

`PackageInfo.FindForAssetPath` cannot resolve a producer for a path-less
texture. The design adds one delegate seam.

- A production provider resolves the installed version of
  `dev.limitex.avatar-compressor` through an offline package list request,
  cached once per capture session.
- Tests substitute the provider, following the established
  test-seams-are-production-delegates pattern.
- An unreadable version, a missing package, or an unlisted version refuses.

## 11. Refusal and report surface

- No new refusal kinds. `UnavailableCapture`, `NonResidentMips`, and
  `UnsupportedFormat` cover every new refusal path.
- The shipped `amuse.texture.UnavailableCapture` hint names the Avatar
  Optimizer atlas setting as the only known producer. The update names both
  known producers of in-memory copies: the Avatar Optimizer atlas setting
  for persisted sub-assets, and the LAC Texture Compressor for replacement
  copies admitted by this design.

## 12. The upstream path, recorded only in code comments

AMUSE files no upstream request and writes no external document about this
admission. The change site carries one code comment instead. The comment
states the facts an upstream LAC release would add, and each fact names the
AMUSE code that fact would let AMUSE delete.

1. Copies persisted as sub-assets of a dedicated container asset, as NDMF
   already does for AAO outputs. This retires the replacement class, the
   replacement identity form, and the duplicate guard, because the existing
   container basis admits the copy unchanged.
   Correction 2026-10-04: the stated premise is false. The existing
   container basis does not admit every sub-asset of an NDMF container.
   `GeneratedTextureAttestation.TryIdentifyProducer` also checks a
   producer name marker inside the container, and a `_compressed` copy
   carries no admitted marker. A live probe on a persisted copy answered
   producer refused, and the capture answered `UnavailableCapture`. An
   upstream persistence change would retire the replacement identity form
   and the duplicate guard. It would not retire producer admission. A
   persisted copy still needs a name marker and a version admission. The
   2026-10-04 persisted-copy admission design covers that shape.
2. Continued registration of source-and-copy pairs in the object registry.
   The admission reads that contract.
3. Stable package versions, one line per release. Stable versions keep the
   admitted set maintainable.
4. A documented output shape per release: name suffix, format family, mip
   and color-space mirroring, and the forced streaming flag. A
   characterization run checks that shape version by version.

## 13. Testing strategy

- Tests never install LAC. Tests build the exact conjunct shape with
  in-memory textures, registry registration, and a substituted version
  provider, then run the real admission, identity, route, and proof code.
- Registry registration in tests follows the production call shape, so the
  read-only lookup contract stays load-bearing.

Falsifiers, each a case a plausible wrong implementation must fail:

1. Falsifier 1: an in-memory copy named with the suffix but never
   registered refuses.
2. Falsifier 2: a registered copy without the suffix refuses.
3. Falsifier 3: a registered, suffixed copy with the package absent, or at
   an unlisted or unreadable version, refuses.
4. Falsifier 4: two distinct objects claiming the same source refuse both,
   failing closed through the missing-evidence path at the resolution gate.
5. Falsifier 5: a BC5 or ASTC copy refuses with `UnsupportedFormat` even
   when every other conjunct holds.
6. Falsifier 6: a copy whose residency cannot be proven never blits a
   non-resident level. The level degrades per level, or the copy refuses.
7. Falsifier 7: an asset-backed texture never receives the
   `unity-replacement` identity form, and a path-less texture never
   receives the `unity-asset` form.

## 14. Risks and residual limits

- The name suffix is forgeable. The conjunction with registration, a
  path-less object, and a pinned package version is the mitigation. A
  determined foreign producer could satisfy all four facts. The residual
  risk is accepted for the lifetime of this admission. The section 12
  comment records the upstream shape that would retire it.
- The one-copy-per-source invariant is producer behavior, not a registry
  guarantee. The capture layer enforces it fail-closed.
- The forced streaming flag on a runtime copy is unmeasured. Until
  measured, copies refuse. That refusal is the behavior the 2026-10-02
  investigation observed.
- The producer can change its shape in any release. The pinned version set
  bounds that risk to characterized versions only.

## 15. Effect

With this design, a LAC-processed slot becomes analyzable when its copy is
DXT1, DXT5, or BC7, the residency fact holds, and the material's other
facts already resolve. BC5 and ASTC copies keep their slots conservative.
Every refusal keeps naming its first failing gate, so a future LAC release
that changes shape fails closed with a readable report.
