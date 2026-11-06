# Ponytail audit remediation design

Date: 2026-11-05
Status: design, revised on 2026-11-05 after two adversarial reviews of the first draft
Input: `docs/superpowers/investigations/2026-11-05-ponytail-audit-investigation.md`, audited commit `9ccfea8` on branch `refactor/audit-2026-11-05`

## Scope

Fourteen findings. Two are soundness fixes. Six are hygiene fixes. Six are small text, test, or config fixes. Work lands on the `refactor/audit-2026-11-05` branch. Decision numbers below map one to one to the finding numbers in the investigation record. No decision changes a pinned attestation constant, a digest, or a refusal enum value. No decision mutates a source asset.

## Decisions

### D1. Stand the repeat pre-filter down above its bound (finding 1)

Amendment, 2026-11-06: the falsifier round refuted the observable-defect mechanism, and the stand-down is dropped. Bit-exact simulation and controller re-derivation show the coordinates the repeat pre-filter consults always convert exactly: float-decoded UVs are dyadic rationals with at most a 36-bit odd core, so the texel conversion loses no bits. The `MaxSupportRegions` window budget caps every separating-axis product near twice 65536, so the rounding stack stays near 1e-10, one order under the 1e-9 separation margin. The mapped walks never consult the pre-filter on transformed coordinates: a nonzero envelope expands the hull past three vertices, and the walk consults the pre-filter only when the hull has exactly three. A stress test with the three-vertex gate artificially bypassed measured 1.22e-10 at a 400 by 163 span, eight times under the margin, with zero flips. D1 therefore lands as two characterization guards in the classifier test file plus a corrected bound comment that closes the open question recorded by D3 of the 2026-10-15 design with this analysis. The falsifier's first-run pass is recorded as characterization, per the repo rule that a first-run pass is never dressed up as RED. The tests make the budget-and-margin dependence load-bearing: a future margin change, a cheaper conversion, or a removed budget cap flips the witness and fails the falsifier.

Original decision text follows, kept for the record of what was planned before the refutation.

Finding: the separating-axis pre-filter is proven safe only for texel coordinates at or below `PreFilterSafeCoordinateBound` (256). The four repeat-family walks pass unwrapped coordinates, which reach the texture width for one-period triangles and about 2^16 for multi-period triangles. A false "separated" answer there drops a witness, and the no-witness arm returns ProvenOpaque.

Sites: `Packages/com.alrauna.amuse/Editor/Analysis/TriangleAlphaClassifier.cs`. The review confirmed the full call-site inventory: eight production call sites. Four clamp sites at lines 436, 596, 1106, and 1256 route through `TryExtractTexelVerticesRelativeToCandidate` and are already bounded. Four repeat sites at lines 768-771, 929-932, 1612-1615, and 1765-1768 use the bound-free `ExtractTexelVertices`. The mapped wrappers `HasMappedWitnessBilinearRepeat` (line 890) and `HasMappedWitnessPointRepeat` (line 1723) delegate to the shared walks, so gating the four repeat sites closes every path.

Decision: in each of the four repeat walks, before the candidate loop runs, measure the largest absolute texel coordinate the walk can hand the pre-filter: the extracted vertex extents plus the candidate window margin of about three texels. When that magnitude exceeds the bound, skip the pre-filter for the whole walk and run the exact intersection test for every candidate instead. Below the bound, nothing changes. The clamp walks keep their existing translated path.

Why: the pre-filter feeds only a keep-or-drop decision before the exact test runs, so skipping it cannot change a verdict or any density bookkeeping. This closes the open question recorded by D3 of the 2026-10-15 design by enforcing the existing bound, with no new math. A whole-walk stand-down was chosen over per-candidate translation because translation adds exact-rational plumbing for a speed gain this round does not need. The exact-test worst case stays bounded by `MaxSupportRegions` (65536). Landing D1 makes one sentence of the comment above the bound false, the sentence that says the repeat walks run it on unwrapped boxes so the bound never caps their magnitudes. That comment is updated in the same task to state the stand-down and to cite this design record as the closure of the 2026-10-15 D3 note.

Test: one falsifier in `Packages/com.alrauna.amuse/Tests/Editor/Analysis/TriangleAlphaClassifierTests.cs`. The fixture builds an `AlphaTextureData` of a 4093-class width directly, which needs no GPU, and uses the file's exact dyadic decoding helpers. The geometry carries unwrapped coordinates near 4000 texels, where the measured error stack of about 1e-7 exceeds the 1e-9 separation margin, so the pre-fix failure is guaranteed rather than marginal. One non-opaque texel sits under the triangle at an exact nonzero distance inside that error stack. Expected before the fix: ProvenOpaque, which is the defect. Expected after: MustRemainTransparent. A clamp-wrap control case at the same magnitudes must classify identically before and after, proving the fix touches only the repeat walks.

### D2. Gate the streaming direct route on uncompressed formats (finding 2)

Finding: the streaming direct route reads authoring PNG bytes and binarizes them at the cutoff. Compressed imports quantize texels to palette levels, so the GPU can sample a value on the other side of the cutoff from the authoring byte. A triangle can then prove opaque falsely.

Sites: `Packages/com.alrauna.amuse/Editor/Host/UnityStreamingTextureEvidence.cs`. The review confirmed one production entry point, the `TryReadSourceAlphaChain` call inside `TryCapture` at line 170. `Packages/com.alrauna.amuse/Editor/Host/SourceImageAlphaReader.cs` gains the format allowlist beside the existing `.png` extension allowlist, with a doc comment naming the formats.

Decision: the direct route runs only when the imported texture format is one of Alpha8, R8, RGB24, RGBA32, or ARGB32. Any other format skips the direct arm at the line-170 entry and takes the clone arm exactly as a direct-route refusal does at the audited commit. No new refusal value. The existing gray-scale refusal and the size gates stay as they are.

Why: for these byte formats the PNG alpha byte equals the imported texel alpha byte, so the direct route stays faithful to the capture contract stated in `Editor/Host/UnityAlphaFieldEvidence.cs` lines 42-43. The gate strictly shrinks the direct route, so it can only remove unsound proofs. The clone arm already decodes compressed and float formats through its copy-and-read path, so the gate adds no new total-refusal case.

Scope note: two residuals stay out of scope because they predate this round and are untouched by the gate. A non-alpha import whose importer alpha source is not none samples 255 regardless of the authoring byte. Normal-map-typed imports with a forced byte-format override can also break byte equality. The deeper-mip divergence between authoring and importer chains stays recorded-open in the 2026-10-09 investigation.

Test: one falsifier in `Packages/com.alrauna.amuse/Tests/Editor/Host/UnityStreamingTextureEvidenceTests.cs`, built on the existing importer fixture helper that writes a PNG and configures its importer. The fixture sets compressed texture compression with block-aligned dimensions and streaming mipmaps, and places a texel band that quantizes across the cutoff. The test asserts three preconditions in the fixture: the imported format is actually compressed, the decoded imported texel crosses the cutoff, and the authoring byte does not. Expected before the fix: the direct route proves opaque from authoring bytes. Expected after: the verdict follows the imported decode, and the test names that concrete verdict.

### D3. Reconcile moved-count documentation and delete the dead overload (finding 3)

Finding: the surviving-plan counting rule is a recorded decision from the 2026-11-01 bimodality investigation and its 2026-11-02 amendment. Three artifacts still describe the abandoned per-write comparison. A dead `CountOpaqueTriangles(write, live, state, index)` overload has zero callers. The `!isChanged` guard in `CollectAppliedSlots` carries a comment that narrates the old rule.

Sites: `Packages/com.alrauna.amuse/Editor/Build/AmusePlatformFinishPlugin.cs` lines 226-243. `Packages/com.alrauna.amuse/Editor/Build/AlphaSeparationApply.cs` lines 1059-1085 (dead overload) and lines 1219-1233 (guard comment). `Packages/com.alrauna.amuse/Tests/Editor/Build/AlphaSeparationApplyTests.cs` lines 6394-6399 (stale test doc comment above `ASelfMappedSurvivingSlotReportsItsProvenMovedTriangles`).

Decision: the counting code stays exactly as it is. The plugin state property doc is rewritten to state the surviving-plan rule and cite the investigation and its amendment. The dead overload is deleted. The stale test doc comment is rewritten to state the behavior the test pins. The guard comment is corrected to name its real effect, which is that it excludes renderers whose every write was a no-op, and keeps its current and true clause about the Split disposition. The guard itself stays.

Why: code is truth, records are history. Wiring the comparison would reverse a deliberate fix and reopen the bimodality defect.

### D4. Join clip identity into the preview controller fingerprint (finding 4)

Finding: the preview cache keys never include animation clip identity. An edit to a separate `.anim` file changes row outcomes without changing any key part, and the stale rows serve with no banner.

Sites: `Packages/com.alrauna.amuse/Editor/DryRun/AlphaSeparationDryRun.cs`, `BuildControllerFingerprint` lines 1914-1931. `Packages/com.alrauna.amuse/Tests/Editor/DryRun/DryRunCacheTests.cs`.

Decision: the controller fingerprint joins `AssetIdentity(clip)` for every committed layer clip the graph walk already holds. The review confirmed those clips are the originals enumerated from the controllers' own layers, so joining their identities does not churn the key across recomputes. The existing clip count stays. Clips without a persistent asset hash as instance ids, which is safe because a domain reload wipes the static cache. Clips stored as controller sub-assets resolve to the controller identity and keep their existing invalidation.

Why: a separate clip file edit must re-key the avatar fingerprint. The build path never reads this cache, so the fix stays in the preview only.

Test: one falsifier in `Packages/com.alrauna.amuse/Tests/Editor/DryRun/DryRunCacheTests.cs`. The test creates its own clip asset and restores the temporary asset folder on teardown, so no committed fixture is touched. It computes rows, modifies the clip file on disk, reimports it, recomputes, and asserts both halves of the contract: the renderer cache misses, and the served fresh rows equal a full recompute of the edited state. Expected before the fix: the cache hits. Expected after: it misses and the fresh rows serve.

### D5. Clear the evidence caches on Refresh and include the streaming cache in the reset (finding 5)

Finding: the two GPU evidence caches are static and clear only at build entry and teardown. The streaming evidence cache is cleared by nothing. Preview-heavy sessions pin hundreds of MiB until a domain reload.

Sites: `Packages/com.alrauna.amuse/Editor/Build/AmuseSessionCaches.cs` (`Clear` and its contract comment), `Packages/com.alrauna.amuse/Editor/Host/UnityStreamingTextureEvidence.cs` (its private static cache container, doc comment at lines 113-118 and declaration at lines 119-123), `Packages/com.alrauna.amuse/Editor/AmuseAvatarOptimizerEditor.cs` lines 242-253 (Refresh), `Packages/com.alrauna.amuse/CHANGELOG.md` (Unreleased section).

Decision: the streaming evidence cache gains a clear method and an emptiness probe, since it has neither at the audited commit. `AmuseSessionCaches.Clear()` gains the streaming cache as its eighth member, appended after the locked-source alignment memo, and the contract comment is updated from seven to eight with the member named in its fixed position. The inspector Refresh calls `AmuseSessionCaches.Clear()` in addition to its existing cache clears. A dated Unreleased changelog bullet records the streaming reset.

Why: Refresh means recompute, so paying a GPU recapture is correct there. Build entry and teardown already call `Clear`, so the streaming cache gains its missing resets with one line. No eviction policy is added, because the audit found no wrong results, only growth. The Refresh-side wiring is verified by code observation rather than a new editor test, which this design records as the verification limit for that half.

Test: `Packages/com.alrauna.amuse/Tests/Editor/Build/AmuseSessionCachesTests.cs` gains a case that populates the streaming cache through its capture path, calls `Clear`, and asserts the emptiness probe. Expected before the fix: the case fails, because the current `Clear` forgets the streaming cache. Expected after: it passes. This makes the case a falsifier for the named wrong implementation, the reset that misses the streaming cache.

### D6. Pin every workflow action by commit SHA (finding 6)

Finding: six actions in the listing workflow and one in the release workflow are pinned by mutable tag under write permissions. The external automation checkout floats on a default branch.

Sites: `.github/workflows/build-listing.yml`, `.github/workflows/release.yml`.

Decision: every mutable tag becomes the full commit SHA of that tag in its upstream repository. The external automation checkout gains a `ref` set to a commit SHA. The four existing SHA pins in the release workflow stay untouched. The SHAs are resolved at execution time with `git ls-remote` against each upstream tag and recorded in the commit message of the task.

Why: one convention, applied everywhere, removes the mutable-reference execution risk from both write-permission paths.

### D7. Changelog count stays eight (finding 7)

Finding: the changelog says the build passes reset eight session caches. At the audited commit the helper clears seven, because the 2026-10-26 removal dropped one member and the changelog missed it.

Sites: `Packages/com.alrauna.amuse/CHANGELOG.md` line 39, `Packages/com.alrauna.amuse/Editor/Build/AmuseSessionCaches.cs`.

Decision: no changelog count edit. D5 in this same round restores the count to eight, which makes the existing line true. D5 carries the contract comment update and the dated bullet. The acceptance criteria check that changelog and code agree after D5 lands.

Why: editing the line to seven and then back to eight would churn the record twice and leave a window where the changelog is false again. One landing order removes the disagreement permanently.

### D8. Add a release concurrency group (finding 8)

Finding: the release workflow derives all names from the package version and has no concurrency guard. Two same-version dispatches race on one tag and one release object.

Site: `.github/workflows/release.yml`.

Decision: a workflow-level `concurrency` group named `release` with `cancel-in-progress: false`. Why false: two same-version runs must serialize, not discard one another.

### D9. Correct the preserve-default tooltip (finding 9)

Finding: the tooltip says the preserve value 0 is the default. The shipped and reset default is 5.

Site: `Packages/com.alrauna.amuse/Editor/AmuseAvatarOptimizerEditor.cs` lines 624-625.

Decision: the sentence takes the same form as its cutout twin, which names the Safe preset value correctly. The review confirmed the twin form is true for this row as well, because the Safe preset sets both preserve fields to 0 and the Normal preset sets 5.

### D10. Replace the vision status pin with a dated statement (finding 10)

Finding: the vision document pins its status statements to a commit and package version from 2026-09-10 while describing later features.

Site: `docs/architecture/vision.md` line 7.

Decision: the pin sentence is replaced with a dated statement. It says the status statements describe the code at the revision date of the document and names that date. The commit and version coupling is dropped. Why: a date-only anchor cannot drift against a moving pin, and it matches the record interpretation rule that records speak for their date.

### D11. Align the lilToon request with the locked version (finding 11)

Finding: the dependency request says 2.4.0 while the locked block records 2.4.1.

Site: `Packages/vpm-manifest.json` lines 15-16.

Decision: the request becomes 2.4.1. Nothing else in the manifest changes.

### D12. Narrow the one copy pin to its count (finding 12)

Finding: exact-equality assertions pin live console emissions, so copy edits break tests with unchanged behavior.

Sites: `Packages/com.alrauna.amuse/Tests/Editor/Build/AmusePlatformFinishPluginTests.cs`.

Decision: the review corrected the population. Exactly one exact-equality assertion pins live emission copy, the `Convertable: 2 out of 2 polygons may be moved` check at lines 8343-8345. It becomes a `Does.Contain` check on its count-bearing prefix. The surviving `Does.Not.Contains("Splittable:")` guard near lines 8336-8339 keeps the prefix discrimination, so the conversion drops no coverage. All other string-literal `Is.EqualTo` sites in that file pin property names and fixture paths, which are contracts rather than copy, and they stay untouched. If any future conversion meets an emission with no count, the fallback is a `Does.Contain` on its discriminating prefix, never a deleted assertion.

Why: the behavior subject is the reported count, not the sentence.

### D13. Best-effort abort sweep on the apply path (finding 13)

Finding: an exception between the transient swap-in and the window close leaves open pairs and AMUSE-owned clones alive. The only cleanup is a test-only helper.

Sites: `Packages/com.alrauna.amuse/Editor/Build/AmusePlatformFinishPass.cs` (the avatar-wide `SwapIn` call at lines 383-386 and the analysis loops after it), `Packages/com.alrauna.amuse/Editor/Build/AlphaSeparationApply.cs` (`SweepUnreferencedClones` at line 1131, already reusable, already called with per-renderer empty lists by the stale-policy path near lines 186-190, and the finalization loop at lines 645-690), `Packages/com.alrauna.amuse/Editor/Build/TransientUnlockWindowClose.cs` (`Execute` at lines 80-165).

Decision: one internal helper composes the two existing halves, the window close `Execute` for reversion and `SweepUnreferencedClones` with per-renderer empty lists for the clone sweep. Both halves are already proven by tests, so nothing is re-implemented. Two catch sites call the helper and then rethrow the original exception: the pass three region after the `SwapIn` call, and the pass four mutation region. The helper wraps each half in its own best-effort catch that logs through `Debug.LogException`, because `Execute` can itself throw on a guard and must never mask the original exception.

Why: reverts and destroys reuse proven paths. Rethrow keeps the fail-loud rule, so no programming failure is absorbed. No refusal value changes. One residual stays and is recorded: an exception thrown inside `SwapIn` itself, while pairs are partially created, is not covered by either catch site. Closing that residual needs a guard inside `SwapIn`, which this round does not build.

Test: a direct unit test constructs a prepared state that holds an open pair and clones, using the existing `SwappedPair` construction pattern from `TransientUnlockWindowCloseTests`, and asserts both are gone after the helper runs. One wiring test uses the existing `TransientUnlockTestKnobs` hooks to throw at a loop boundary, asserts the sweep ran, and asserts the original exception propagates. If no knob can throw at either boundary, the plan records the limit and the helper test stands alone.

### D14. Cite the measurement in the stamp comment (finding 14)

Finding: a Multi test comment states lilToon 2.3.3 carries stamp 44. The repository measurement in commit `b106675` states the 2.3.0 through 2.3.3 artifacts are byte-identical to 2.3.4 at stamp 45. The uncited claim produced a refuted audit candidate.

Site: `Packages/com.alrauna.amuse/Tests/Editor/Semantics/LilToonSourceAttestationTests.cs` lines 410-418.

Decision: the comment is rewritten to separate two facts. It states what the test pins, which is exactness of the stamp compare, and it states what the measurement says about the 2.3.x era, with the commit cited. The test code stays untouched.

## Out of scope

- The refuted strict stamp-ordering candidate. The gate order stays as it is.
- The authoring-versus-imported mip divergence at deeper levels. It stays recorded-open in the 2026-10-09 investigation.
- The pre-existing byte-equality residuals of the direct route for non-alpha and override imports, listed in D2. The gate does not touch them.
- The exception-during-SwapIn residual listed in D13.
- A per-candidate translation rework of the repeat walks for speed.
- NDMF error-hook integration beyond the two catch sites of D13.
- The research package.
- Any pinned attestation constant, digest, or refusal enum value.

## RED/GREEN note

Findings 1, 2, 4, and 5 get falsifier tests observed failing before their fix, for a named wrong implementation: the repeat walk that trusts the pre-filter past the bound, the direct route that trusts authoring bytes for compressed imports, the fingerprint that misses clip file edits, and the reset that forgets the streaming cache. Finding 13 gets a new mechanism test whose first run is expected green, plus a wiring test when the seams allow one. Findings 3, 6, 8, 9, 10, 11, 12, and 14 change text, tests, manifests, or workflow files. RED does not apply to them. For finding 12, the suite pass counts stay identical across the edit, and the diff must show one converted assertion, not a deleted one.

## Acceptance

1. Unity compiles with no errors and no new warnings after each task.
2. The four falsifier tests fail before their fix and pass after, observed in the Unity Test Runner with counts recorded.
3. The full `Alrauna.Amuse.Tests.Editor` and `Alrauna.Amuse.Research.Tests.Editor` assemblies pass at the end, with counts recorded.
4. Grep proves each text site shows its corrected wording.
5. Both workflow files carry only full-SHA `uses:` pins after D6, verified by grep, and the external checkout carries a `ref`.
6. Grep proves the release workflow carries the `concurrency` group of D8 and the manifest request of D11 reads 2.4.1.
7. After D5, grep proves the session cache contract comment names eight caches and the changelog line and the code agree.
8. Every touched file passes the identifier sweep before its commit.
9. The working tree holds only this round's changes plus the pre-existing ProjectSettings churn.
