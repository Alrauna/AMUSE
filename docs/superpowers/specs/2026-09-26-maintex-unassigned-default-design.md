# Unassigned MainTex declared-default proof for the lilToon families

Privacy note: This design comes from a Census Lab characterization on 2026-09-26. The characterization names no avatar, renderer, material, or texture, and this document repeats none of them. The observed slots are described by shader family and state.

Date: 2026-09-26.
Branch: `feat/maintex-alpha-blend-support`.
Base: `main` at `ef51424`.
Investigation: `docs/superpowers/investigations/2026-09-26-maintex-refusal-investigation.md`.
Status on 2026-09-26: design awaiting implementation. No production code had changed.

## Problem

Two lilToon material slots refused with `AdmittedMaterialSemanticsUnknown`, each naming the Main texture feature, property `_MainTex`. One slot holds a lilToon Transparent family material whose `_MainTex` is unassigned. The other holds a lilToon Cutout family material whose `_MainTex` is an assigned streaming BC7 texture; its refusal is the separate build-time capture failure, addressed by the report change below.

Today both lilToon alpha frontends turn an unassigned `_MainTex` into Unknown at one guard. The guard is correct as far as it goes: the frontends have no rule for the unassigned case. The refused slot then proves nothing, and an honest provable value is recorded as Unknown. This design adds the missing rule.

## The theorem

Every attested lilToon variant source declares the main texture with a white default. The pinned outline wrapper source shows the exact declaration:

```
[MainTexture]   _MainTex                    ("Texture", 2D) = "white" {}
```

The canonical SHA-256 digests in `LilToonSourceAttestation` pin each attested variant source, and those digests cover the Properties block that declares this default. A vendor change to the default breaks the digest and the family loses attestation before this fact can go stale.

Unity binds a texture property's declared default when a material assigns no texture. Playback therefore samples alpha exactly one at every texel of an unassigned `_MainTex`, and the sample is coordinate-independent: scroll, rotate, scale, and offset cannot change it. The transparent alpha reduces to the constant `_Color.a`. The cutout alpha reduces to the cutout transform of `_Color.a`, composed with the alpha mask term when one runs.

This is the same collapse the frontends already perform for a texture whose import proves its alpha is exactly one: `SampledAlphaIsProvenOne` collapses the main sample to `ScalarSemanticValue.Constant(colorAlpha)`. The new arm reuses that value shape.

Trust level: the declarations are measured from the pinned sources. That playback binds the declared default is standard Unity material behavior, not separately measured in this project. The Census Lab can confirm it in characterization: the transparent slot that caused this design renders translucent at `_Color.a` near 0.05, which is the white default times the tint.

## Approach decision

Three approaches were considered.

Approach A, chosen: a declared-default arm inside each lilToon alpha frontend. Family attestation already pins the source that declares the white default, so an unassigned main collapses to the tint constant. No capture, request, or resolver change.

Approach B, rejected: capture a live declared-default fact per material and compare it at interpretation. It touches the closed evidence schema and both families' plumbing for a fact the digest attestation already guarantees.

Approach C, rejected: materialize a virtual white texture assignment in the shared capture or resolver layer. It invents sampler and scale facts for a texture that has none and widens a shared seam for one consumer, against the repository rule that shader-specific behavior stays shader-specific.

## Design

### The declared-default arm

The arm replaces the failing guard at one site in each alpha frontend: `LilToonCutoutMaterialSemantics` and `LilToonTransparentMaterialSemantics`. It runs after every existing gate, in the existing gate order: coverage gates, mask interpretation, dissolve, scroll and rotate, cutoff, tint alpha. Nothing upstream changes.

When the captured evidence holds no assignment for `_MainTex`, the arm refuses a sampled alpha mask, and otherwise completes the alpha as the tint constant.

A sampled alpha mask refuses with the existing `UnsupportedSampling` code naming `_MainTex`. The reason is exact: a mask sample borrows `_MainTex`'s captured sampler facts, and an unassigned main has none. The observed material runs no mask, and mode 1 with an unassigned mask already returns through the mask's own constant arm before the texture arm. A later design can attest the default sampler state and lift this refusal; this one refuses.

The arm does not read `_MainTex_ST`. A constant sample is coordinate-independent, so scale and offset cannot change the value. The identity gate stays in the assigned arm, and `_MainTex_ST` animation still refuses earlier at runtime-state resolution. Both keep their current behavior.

After the arm sets the constant, the method continues through the unchanged layer composition and mask composition. Nothing downstream distinguishes which branch produced the constant.

Reach is full, per the repository rule that unnecessary refusal is a coverage defect. A cutout material with `_Color.a` of 1 inside the provable cutoff bound proves triangles opaque. A transparent material with `_Color.a` of 1 proves triangles opaque at exactly one. A `_Color.a` below one yields must-stay-transparent or discarded values through the unchanged classifier. The transparent slot from the investigation, at `_Color.a` near 0.05, moves from Unknown to an honest must-stay-transparent record; it hides no optimization loss, because that value can never prove opaque.

### Attestation and provenance

The design adds no new pinned constant and no live read. The white default is a property of the pinned sources, and the digests in `LilToonSourceAttestation` enforce it: any drift breaks attestation and the family answers all-Unknown before the arm can run. The arm carries a load-bearing doc comment stating the theorem and pointing at the digests, following the repository rule that doc comments state why.

### Report change

`AmuseReports.TextureCaptureRefusal` gains two arguments in its report data: the refusal reason family name and whether the refused texture had a source identity. The three texture-capture description strings and their hints gain the same two fields, so every reason family reads the same shape. The title keys stay per-reason as they are today.

This change separates the investigation's open second cause on the next Lab build. A refusal whose texture has no source identity points at a build-copy clone; a refusal with a source identity points at the capture route or the build context.

### Safety inheritance

An object-reference curve that animates `material._MainTex` already refuses the whole renderer with `UnrecognizedAnimatedMaterialBinding` at runtime-state resolution, before any frontend runs, because `_MainTex` is in the alpha relevance request. A swap that assigns a real texture at runtime therefore never reaches the new arm. This design changes none of that machinery.

## Testing

Product tests run in the dev editor instance through the Test Runner, EditMode mode. Tests never run in the Census Lab.

Each frontend gets a RED pair and one falsifier, driven through the verified seams and the stand-in shaders under `Hidden/Alrauna/AmuseTests/`:

- An unassigned main with `_Color.a` below one resolves uniform must-stay-transparent for the transparent family. RED today: the guard returns Unknown.
- An unassigned main at unit tint resolves the corner triangle proven-opaque. RED today.
- The cutout pair mirrors both, with the cutoff comparison deciding the outcome.
- A provider that throws if any texel is consulted backs every resolution, falsifying an implementation that secretly builds a texture sample for the unassigned case.
- A cutout falsifier pairs an unassigned main with a sampled alpha mask and asserts the `UnsupportedSampling` diagnostic naming `_MainTex`. RED today with a different diagnostic code, so the falsifier guards the refusal reason, not just the refusal.

The report change gets a RED test in the existing report test class through `ErrorReport.CaptureErrors`, asserting the message names the reason family and states the identity fact for both identity states.

After the focused runs, the full `Alrauna.Amuse.Tests.Editor` assembly runs, and observed counts are recorded. A filtered run that reports zero tests is a failure.

## Non-goals

- The Poiyomi frontend. It answers `_MainTex` questions through its own attested source; if evidence appears there, it applies pressure on this seam independently.
- The opaque family's color equation, which has its own texture arm and its own evidence.
- A sampled alpha mask over an unassigned main. It refuses by name until a design attests the default sampler.
- Widening animated `_MainTex_ST` behavior. It refuses at resolution today and keeps doing so.
- The build-time capture refusal itself. The report change instruments it; the fix belongs to a later design with the evidence the new report produces.

## Risks and limits

The one external assumption is that playback samples a texture property's declared default when the material assigns none. It is Unity's documented material behavior and matches the observed Lab rendering, but this project has not measured it directly. If it ever fails, the arm proves a value that playback does not produce. The mitigation is the same as for the importer theorem: a characterization in the Census Lab compares a proven-opaque unassigned slot against the rendered output before upload-path consent widens.

The digests enforce the theorem only for attested variants. A new vendor version re-attests through the existing process before the family admits anything.
