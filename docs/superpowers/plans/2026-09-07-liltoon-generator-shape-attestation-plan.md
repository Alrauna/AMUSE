# lilToon generator shape attestation - slice plan

Date: 2026-09-07. Spec: `docs/superpowers/specs/2026-09-07-liltoon-generator-shape-attestation-design.md`.
Base: `main` at `a88e9c0`. One PR through the automerge gate.

**Goal:** a lilToon 2.3.4 install regenerated with LTCGI and AudioLink
attests like the shipped bytes, while every hand edit and every other version
still refuses.

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonSourceAttestation.cs`
  (R4, R5, R6, verifier adaptation, vocabulary, pins, comments)
- Modify: `Packages/com.alrauna.amuse/Tests/Editor/Semantics/LilToon/LilToonAttestationTests.cs`
  (shape tests, falsifiers, updated injected-shape cases)
- Check: `Packages/com.alrauna.amuse/Tests/Editor/Semantics/LilToon/LilToonAdversarialTests.cs`
  (only if a falsifier row lands there by convention)

## Tasks

### T1 - Vocabulary and rules RED/GREEN
1. RED: `SkipVariantVocabulary` does not exist; tests referencing it fail to
   compile. Write the shape tests first against the intended API, observe the
   compile failure as RED, then implement.
2. GREEN R5: `Canonicalize_VocabularySkipVariantsLine_IsRemoved` for a
   dedup-leftover line inside and outside a setting region.
3. GREEN R4: `Canonicalize_LtcgiDefineBeforePassForwardAnchor_IsRemoved`.
4. GREEN R6: `Canonicalize_AppendedLtcgiTagToken_IsRemoved`.
5. Falsifiers (must stay green): F1 unknown tag token, F2 unknown skip token,
   F3 valued LTCGI define, F4 LTCGI define away from the anchor.
6. Property: `Canonicalize_DefaultShape_And_GeneratorShape_Agree` - one
   synthetic source in two shapes canonicalizes to one string.

### T2 - Verifier adaptation RED/GREEN
1. RED: a record run ending in the `_MIXED_LIGHTING_SUBTRACTIVE` skip record
   fails `TryVerifyOfficialSettingRecord` today.
2. GREEN: the run passes when every skip record token is in the vocabulary;
   unknown tokens and descending generator order still fail.

### T3 - Pin re-measurement
1. Extract the skip vocabulary from the shipped bytes and the generator
   literals; pin it as a sorted constant.
2. Invoke the production canonicalizer in the editor over both real shapes for
   all nine profile files. Record per-file digests; require agreement.
3. Replace the nine profile digest pins. Update the canonicalization doc
   comment, the S8/S9 measurement comments, and the zip provenance comment.

### T4 - Validation
1. Focused `Alrauna.Amuse.Tests.Editor.Semantics.LilToon` filter, observed
   counts.
2. Full EditMode suite, both assemblies, observed counts.
3. The identity path on the real Census Lab install is user-side validation:
   AMUSE in the Lab must stop listing lilToon shaders as unverified. This is
   the S12 prerequisite check and stays with the user.

## Stop conditions
A pin disagreement across shapes, a falsifier that cannot hold, or a URP shape
requirement stops the slice and returns to the user.
