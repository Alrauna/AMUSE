# The locked Poiyomi fixture blocker is the closed decal gate - characterization

## Privacy note

This record opens with a privacy statement. It describes private Census
Lab fixtures by role only. It names no avatar, renderer, material,
texture, mesh, or hierarchy. It records no machine paths, no host
names, no ports, and no instance names or hashes. It records no asset
identifiers. The designated locked denim garment fixture appears by
role, and its alpha facts appear as feature values. The avatar that
carries it appears as the fixture carrier. Every status claim carries
its date. Property names, mode numbers, and vendor line facts are
public facts of the Poiyomi Toon 9.3.64 shader source.

## 1. Scope and method

Date: 2026-09-22. This is a diagnosis session on the parity branch
`feat/poiyomi-alpha-parity` at its head commit. It changes no
production code and writes no production test. The session brief asked
why the Census Lab build of the fixture carrier still shows an error,
after the seven parity slices landed.

The method had five parts. Each part produced the evidence in its own
section.

1. Baseline: the full product and research EditMode suite ran in the
   pinned dev editor instance, after the identity check.
2. Consumption: the Census Lab project's package resolution for
   `com.alrauna.amuse` was inspected.
3. Reproduction: the fixture carrier build ran through the Lab's own
   probe path, and the NDMF console records were read back.
4. Characterization: the fixture material was cloned, the clone was
   unlocked with the vendor lock tool the production unlock window
   uses, the production frontend entry answered for the clone, and the
   clone was deleted in the same handler. The fixture itself stayed
   locked and untouched.
5. Routing: the observed diagnostic was mapped against the parity
   surfaces and against the installed vendor source.

## 2. Baseline, 2026-09-22

The working tree was clean except the user's own session rule files.
The suite ran in the dev editor instance after its identity check.

Observed counts: 2344 of 2351 tests completed, with exactly the five
known baseline failures. The four Avatar Optimizer environment
failures in the merged-consumption class and the known degenerate
scale-offset refusal test. No other failure appeared. The branch is
healthy.

## 3. Consumption, 2026-09-22

The Lab project references both AMUSE packages through file
references into this repository's working tree. There is no package
cache copy. The Lab therefore compiles the parity branch head in
place. Package drift is impossible through this route, and no update
was needed.

## 4. Reproduction, 2026-09-22

The fixture carrier lives only in the Lab editor's open scene state.
No saved scene or prefab in the project references the fixture
material. The saved scene corpus is not the carrier. A throwaway copy
of the live carrier was made and processed through the Lab probe path,
which runs the same preprocess chain the user's build runs. The copy
was deleted after the run, and a sweep confirmed no leftover files.

Observed results:

- The build completed. The probe reported accepted.
- One renderer, with about half a million triangles. All of its
  triangles kept their original material. Zero triangles moved. Zero
  soundness violations.
- The NDMF records for this build carried exactly one AMUSE renderer
  entry: key `amuse.renderer.AdmittedMaterialSemanticsUnknown`, level
  Information. Its sentence says the materials resolved, but every
  triangle answer came back unknown, and the renderer keeps its
  original materials.
- The summary entry `amuse.summary.Title` reported zero renderers
  analyzed, zero triangles moved, and one renderer kept original.
- No texture capture refusal, no slot analysis refusal, and no
  `TransientUnlock*` or `LockedPoiyomi*` refusal appeared.
- The transient unlock window opened, swapped in the restored clone,
  captured through it, and re-locked cleanly.

The user-visible error in the NDMF console is this renderer entry. The
build itself does not fail. AMUSE refuses the renderer as a whole,
fail closed, with a named Information entry.

## 5. Characterization of the unlocked state, 2026-09-22

A clone of the fixture material was unlocked through the same vendor
method the production unlock window calls. The clone was then deleted.
The production frontend entry `AnalyzeBaseMaterial` answered for the
unlocked clone.

The frontend answered: material supported. The alpha output refused
with `UnsupportedFeature: _DecalEnabled`. The base color, emission,
and normal outputs refused with `UnsupportedFeature: _DetailEnabled`.

The unlocked clone carried these feature values:

- Mask mode 1 (Replace), blend strength 1, value 0, invert 0, UV 0,
  zero pan, identity scale-offset, and a bound mask texture. This
  matches the recorded fixture facts.
- Cutoff 0.001. Mode 3 (Fade). Premultiply 1.
- Detail 1. This matches the recorded facts.
- Decal slot 0 enabled, decal slots 1 to 3 disabled, and no decal
  texture bound. This fact was missing from the recorded fixture
  facts.
- Beat Saber module 0, force opaque 0, ignore texture alpha 0.

## 6. Root cause

The alpha feature gate array of the Poiyomi frontend contains
`_DecalEnabled` and its three sibling slots. Any enabled decal makes
the alpha output Unknown. The resolver then refuses the slot, and the
renderer refusal lands as `AdmittedMaterialSemanticsUnknown`.

This gate is a deliberate fail-closed boundary. It predates the parity
roadmap, and none of the seven landed slices touches it. The recorded
fixture facts of 2026-09-21 name the detail feature but not the decal
feature, because the alpha output then refused `_AlphaPremultiply`
first, and a refusal names one property. After the premultiply slice
admitted premultiply, the decal gate became the first refusal and
surfaced.

The parity slices themselves work as designed. The premultiply alpha
admission, the bound replace mask admission, and the unlock window all
behaved correctly in the reproduction. The fixture is blocked one
boundary further than the roadmap reached.

## 7. Vendor facts read on 2026-09-22

The installed 9.3.64 source was read directly, read-only. The decal
block declares `_DecalBlendAlpha`, `_DecalAlphaIntensity`, and a
`Use Decal Alpha` toggle per decal slot. Decal alpha influence is a
real vendor mechanism, so a blanket admission would be unsafe without
its own envelope. No vendor fact in this section contradicts the
pinned note or the attested sources.

## 8. Routing and follow-up candidate

Routing: no product defect on the parity branch. No Lab configuration
defect. Nothing in this session needs a RED/GREEN brief.

Follow-up candidate, not started: a slice that admits a decal slot
whose decal texture is unbound and whose alpha-use flags are proven
zero, or a fuller decal alpha envelope. This revises a coverage
boundary, so it needs its own note row, its own owner decision, and
its own RED and GREEN obligations. The shipped behavior stays safe
until then.

## 9. Validation of this session

No repository test changed and no production code changed. The suite
run of section 2, the reproduction of section 4, and the frontend
probe of section 5 are the observed evidence. The fixture material,
its carrier, and their scene state were never mutated. Every
temporary asset was deleted, and a sweep confirmed the deletion.
