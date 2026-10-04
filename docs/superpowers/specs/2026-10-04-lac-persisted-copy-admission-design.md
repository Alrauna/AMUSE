# Persisted Replacement Copy Admission Design

Date: 2026-10-04.
Status: approved on 2026-10-04. The implementation plan lives at
`docs/superpowers/plans/2026-10-04-lac-persisted-copy-admission.md`.
Scope: admission of Limitex Avatar Compressor (LAC) replacement copies
after NDMF persists them as project assets, and of the LAC bake outputs
NDMF persists beside them. This design extends the 2026-10-03 generated
texture attestation design and corrects one premise of its section 12.
The correction note lives in that file.

## 1. Problem

The 2026-10-03 design admits an in-memory LAC copy that is path-less and
registered. That shape exists only while a build runs. NDMF persists
every in-memory build texture at the end of the pipeline, as a sub-asset
of a container asset under the avatar's NDMF output folder. The
persisted copy then reaches AMUSE through two real product paths:

- The avatar ships as manual bake output. A later build analyzes the
  baked avatar. Every LAC copy in it is an asset-backed sub-asset.
- Tooling or a person re-analyzes the persisted build output.

On both paths the identity gate refuses every copy.
`UnityTextureEvidence.TryGetSourceId` refuses a sub-asset whose producer
marker is not admitted, and `GeneratedTextureAttestation.TryIdentifyProducer`
admits only the two name markers of the Avatar Optimizer. The capture
answers `UnavailableCapture` before any route runs. Every triangle over
a replaced slot answers Unknown. LAC also bakes some lilToon slots into
outputs named with a `_baked` suffix. NDMF persists those beside the
copies, and the same refusal covers them.

## 2. Measured evidence

All facts below were measured or read from source on 2026-10-04. The
investigation record of 2026-10-04 carries the full probes.

- NDMF 1.14.4 persists in-memory build textures in `Finish()`, after the
  last phase. Each texture becomes a sub-asset of one container asset.
  The container's main object is a
  `nadena.dev.ndmf.runtime.SubAssetContainer`.
- LAC 0.9.0 registers only two replacement pairs in the object
  registry: each cloned material, and each compressed copy with its
  source. The bake path names its output `source + "_baked"` and never
  registers a pair. If the compressor later processes a bake output, the
  compressed result carries the `_compressed` suffix and a registered
  pair, and the intermediate bake texture stays unregistered.
- A live probe on a real persisted copy answered: sub-asset true, main
  object type the container, streaming flag set, format BC7, identity
  refused, capture refused with `UnavailableCapture`.
- A live probe on a registered path-less copy of the same shape
  answered: admitted, identified, residency proven, captured through the
  generated route. The version seam read 0.9.0 and admitted it.
- A fresh two-arm build characterization on the probe avatar measured
  the in-build refusal of bake outputs: the arm with the compressor
  reported two texture-read refusals and two proved-nothing slots. The
  arm without the compressor moved 12,136 triangles on one slot. The
  compressor arm moved zero. One bake output was a DXT1 texture whose
  name ends `_baked` and not `_compressed`.

## 3. Scope and non-goals

In scope:

- One new admission branch for the persisted replacement shape: a
  sub-asset of an NDMF container named with an attested LAC suffix,
  under an admitted producer version.
- Confirmation that the persisted shape captures through the generated
  route, with the residency provenance the route already applies.
- A dated characterization gate for the one unmeasured fact: residency
  behavior of a persisted streaming copy in the editor.
- Correction of the code comments that carry the false section 12
  premise.

Non-goals:

- No format allowlist changes. DXT1, DXT5, and BC7 stay admitted. BC5
  and ASTC copies stay refused with `UnsupportedFormat`.
- No admission for the in-build path-less bake output. That object is
  unregistered and path-less, so no stable identity exists for it. Its
  refusal stays, and the upstream path records what would retire it.
- No new identity form, ledger, or duplicate guard.
- No consent surface. The producer rows carry no consent gate.
- No change to shader attestation, frontend selection, the exact-one
  opacity bar, the mip cap policy, or the refusal vocabularies.
- No canonical normal-map import proof for copies. That proof stays
  importer-only.

## 4. Design questions

1. Which conjunction of facts admits a persisted replacement copy
   without trusting one forgeable marker?
2. What identity pins the persisted copy?
3. Which capture route reads it, given that the forced streaming flag
   routes asset-backed textures to the readable-clone route, which can
   never serve a container sub-asset?
4. Which per-fact proofs apply without new code?
5. What upstream behavior would let AMUSE shrink or retire this
   admission?

## 5. Constraints

- Fail closed. Every unmet conjunct refuses with the existing
  vocabulary.
- Never trust bytes. Identity pins asset state, never content hashes.
- Never mutate source assets. All probe and test writes go to
  disposable fixture assets.
- Capture the shipped pixels. The persisted copy is what the shipped
  avatar samples, so the proof must read the copy.
- No absolute paths, no host names, no private identifiers.
- Simplified Technical English for every human-read sentence.

## 6. Approved basis: the persisted replacement shape

The approved basis is a conjunction. A texture is an attested persisted
replacement copy when every fact below holds. Any unmet fact refuses.

1. The texture is a live `Texture2D` and a sub-asset.
2. The container asset's main object type is exactly
   `nadena.dev.ndmf.runtime.SubAssetContainer`. Derived or foreign
   container types refuse, matching the Avatar Optimizer precedent.
3. The name ends with `_compressed` or `_baked`. Both suffixes are
   pinned output names of the characterized producer.
4. The producer package `dev.limitex.avatar-compressor` is installed,
   its version is readable, and the version is in the admitted set of
   the 2026-10-03 design. The set starts with 0.9.0. A new version
   joins only through a dated characterization of both output names.

The format, mip residency, and dimension gates of the capture apply
after admission, unchanged.

## 7. Identity

The persisted copy resolves through the ordinary asset identity form,
`unity-asset:<guid>:<localId>`. The form already exists and is stable
across sessions, because the copy lives on disk. No new form is minted,
no ledger entry is written, and no duplicate guard is needed. The
existing rule stands: an asset-backed texture never carries the
`unity-replacement` form, and a path-less texture never carries the
`unity-asset` form.

## 8. Capture route decision

`GeneratedTextureAttestation.TryIdentifyProducer` gains the branch of
section 6. Every consumer of producer admission then serves the
persisted shape without further changes:

- The identity gate resolves the copy as an ordinary asset.
- `TryIdentifyRouteTexture` returns the producer, so the capture takes
  the generated route. The generated route blits the live resident
  object per level with async readback, which is the shipped-pixel
  proof surface.
- The route order places the generated route before the streaming
  route, so the forced streaming flag never reaches the readable-clone
  route. That matters, because the readable-clone route requires a
  `TextureImporter` on the copy's own file, and a container sub-asset
  carries only the base importer. Admitting the shape at identity while
  leaving the route order alone would refuse downstream. The route
  decision is therefore part of the admission, not a follow-up.

Residency is the one unmeasured fact. The 2026-10-03 design measured
that a runtime copy with the forced flag reads its requested level
loaded. A persisted copy loads from disk, and its high levels may not
be resident. The existing behavior covers this: the mip limit gate runs
before the route, and the route marks a level without evidence when it
cannot prove residency, so the fold degrades that level to Unknown.
The characterization gate below decides whether the admitted copy
actually captures in the editor.

Characterization gate, dated 2026-10-04: before the admission ships,
the plan must measure a persisted DXT5 streaming copy with the
`_compressed` name in the dev editor instance. The gate passes when the
requested level reads loaded and the capture produces evidence for at
least the requested level. If the gate fails, the admission does not
ship. The branch then keeps the refusal and the record gains the
measured counts. No test may be weakened to pass the gate.

## 9. Per-fact proofs

- Source identity: section 7.
- Color interpretation: the existing route-texture branch reads the
  graphics format and the data color space. No change.
- Sampling facts: filter, wrap, and anisotropy read live object state.
  No change. The mirror-wrap refusal stays.
- Sampled alpha exactly one on a no-alpha format: the 2026-10-03 design
  extended this fact to every attested route texture. A persisted DXT1
  copy or bake output gains the provable answer through that extension
  with no new code.
- Canonical normal-map import: stays importer-only. Normal-map facts on
  copies answer conservatively.

## 10. Version seam

No new seam. The persisted branch reads
`ReplacementTextureAttestation.TryReadInstalledProducerVersion` and
`IsVersionAdmitted`, the same session-cached delegate pair the in-memory
admission reads. Tests substitute the provider the same way.

## 11. Refusal and report surface

No new refusal kinds. `UnavailableCapture`, `NonResidentMips`, and
`UnsupportedFormat` cover every path. The report hint already names
both known producers and the version rule. One sentence of the hint
gains the persisted shape, so a reader of a refusal understands that
persistence alone never admits a copy from an uncharacterized version.

## 12. The upstream path, recorded only in code comments

AMUSE files no upstream request. The change sites carry comments.

1. The comment on `ReplacementTextureAttestation` repeats the false
   section 12 premise. The correction states that persistence would
   retire the replacement identity form and the duplicate guard, and
   that a name marker plus a version admission remains either way.
2. The comment on the new producer branch states the shape facts an
   upstream release would add: a dedicated marker distinct from the
   compressed suffix, and registration of bake pairs. Bake registration
   is the fact that would let AMUSE admit the in-build bake output and
   retire its refusal.

## 13. Testing strategy

Tests never install LAC. Tests build the exact conjunct shape with real
NDMF container fixtures, the way the existing producer tests do, and
substitute the version provider. Falsifiers extend the 2026-10-03 set,
which numbered 1 through 7:

8. A `_compressed` sub-asset of an NDMF container with the producer
   absent, unlisted, or unreadable refuses at identity.
9. A `_compressed` sub-asset of a derived or foreign container refuses
   at identity even when every other conjunct holds.
10. A `_baked` sub-asset of an NDMF container admits under an admitted
    version and refuses under an unadmitted one.
11. A path-less registered `_baked` copy in a build still refuses, so
    the in-build refusal stays until bake registration exists.
12. An admitted persisted copy resolves as the ordinary asset identity
    form and never as the replacement form.
13. A persisted BC5 copy refuses with `UnsupportedFormat` even when
    every conjunct holds.
14. An admitted persisted copy captures through the generated route,
    and the forced streaming flag never routes it to the readable-clone
    step.

## 14. Risks and residual limits

- The name suffix is forgeable, now in the persisted form too. The
  conjunction with the exact container type and the pinned version is
  the mitigation. A foreign producer could write a `_compressed` sub-
  asset into an NDMF container while an admitted LAC version is
  installed. The residual risk is accepted and dated here.
- The container type is pinned NDMF API. If NDMF renames or replaces
  it, the admission refuses until a dated characterization admits the
  new type. That is the safe direction.
- Residency of persisted streaming copies is unmeasured until the
  characterization gate runs. Until it passes, the route-level test in
  the plan must not be marked complete.
- Re-analysis of persisted output proves the shipped pixels of that
  output. If the output is stale, the proof describes the stale avatar.
  That is true of every analysis of baked output and is not new here.

## 15. Effect

A LAC-processed avatar that ships as baked output becomes analyzable
when its persisted copies are DXT1, DXT5, or BC7 and the residency
facts hold. Slots whose copies are BC5 or ASTC stay conservative. The
in-build bake output stays refused until the producer registers bake
pairs. Every refusal keeps naming its first failing gate.
