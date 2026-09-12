# Texture evidence failure soundness implementation plan

Date: 2026-09-11. Base branch: feat/min-preserved-transparency at
7c981d5. Spec:
docs/superpowers/specs/2026-09-11-texture-evidence-failure-soundness-design.md.
Investigation:
docs/superpowers/investigations/2026-09-11-alpha-separator-merged-renderer-conversion.md.

Scope: make missing or failed texture evidence fail closed in the
alpha resolution. Allowed mutations: the lilToon transparent semantics
resolution path, the material evidence capture, the refusal enum if a
new named reason is required, and their tests. No shader changes. No
changes to the mask composition algebra. No changes to the inert byte
identity contract. No commit, push, or pull request without explicit
authorization.

## Background

A lab avatar with the Merge Skinned Mesh option of the third party
optimizer enabled moved about nine tenths of the dress polygons to
opaque materials with every slider inert. The captured chain holds only
about 13 percent opaque texels, and the moved polygons sample 18 to 37
percent partial texels, so the classification cannot have consulted
that chain. The first texture slot of the captured evidence is empty.
The working hypothesis is an unbound sample that resolves to a constant
of full opacity instead of failing closed.

## Task 1: resolver RED, missing main chain

Write the failing resolver test first. Fixture: the transparent
stand-in material with an alpha mask, mask mode 2, scale 1, value 1, a
main texture whose alpha holds opaque and hole texels, and a main
evidence chain that is absent. The resolution must be unknown, and a
triangle over hole texels must not prove. The plausible wrong
implementation is the current one: the missing sample falls back to a
constant of full opacity, so the triangle proves. Observe RED, then
proceed.

- [ ] Step 1: write the resolver test and observe RED

## Task 2: fail closed in the resolution

Locate where an unbound sample resolves to a constant. Grep the
resolution path for the fallback that substitutes a full-opacity
constant for a missing sample. Replace it: a required sample with no
bound chain makes the alpha resolution unknown. Keep the existing
unknown plumbing that already exists for unsupported features. Observe
GREEN on Task 1.

- [ ] Step 1: remove the constant fallback on the required sample path
- [ ] Step 2: observe GREEN on the Task 1 test

## Task 3: named capture refusal

Give the capture a named refusal for a texture it cannot capture, in
the existing refusal enum style, and surface it through the slot
resolution so the report can name the reason. The material keeps its
original materials. Observe RED on a new capture-level test that feeds
an uncapturable texture, then implement.

- [ ] Step 1: write the capture refusal test and observe RED
- [ ] Step 2: implement the named refusal and observe GREEN

## Task 4: end to end barrier RED then GREEN

Extend the barrier harness: one merged-renderer style build where one
material's evidence is unavailable. Expected after the fix: zero moved
triangles for that material, the material kept original, the refusal
named in the report. Observe RED before Task 2 lands if the harness
runs after the resolver fix, else run GREEN as confirmation.

- [ ] Step 1: write the barrier test
- [ ] Step 2: confirm the expected outcome after the fix

## Task 5: mip residency adoption

Adopt the residency gate the predicate probe already models: consult
resident levels, mark non-resident consulted levels unknown, and keep
polygons that need them on the original materials. Decide with the
spec: per-level degradation, not a renderer refusal.

- [ ] Step 1: thread the residency gate into the production capture
- [ ] Step 2: unit test a limited-mip texture and observe the
  degradation

## Task 6: full validation

1. Refresh Unity; confirm zero compile errors.
2. Run the full product assembly. Expected: every test passes. Record
   the observed counts.
3. Run the research assembly. Expected: every test passes. Record the
   observed counts.
4. `git diff --check` clean.
5. Identifier sweep over every changed file: an at sign joined to a
   hexadecimal hash, drive letter paths, home directory paths, four
   digit ports, and private asset names. Every hit is a defect.

Manual validation after the automated rounds: rebuild the lab avatar
with the Merge Skinned Mesh option enabled and the sliders inert. The
summary must report zero moved triangles for the affected materials.

## Stop conditions

Stop and report if the constant fallback turns out to live in shared
code that other families depend on, if the fail closed change breaks
the inert byte identity contract in ways the guards detect, or if the
fix requires shader changes. Those are design decisions, not
implementation steps.

## Git boundary

No staging, commit, push, or pull request without explicit
authorization from the user in the session.
