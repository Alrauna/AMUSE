# Whole-repo ponytail audit, 2026-11-05

This record covers only tracked public repository content. It names no private avatar, machine, instance, or account. All status claims speak for 2026-11-05.

## Scope and method

- Audited commit: `9ccfea8` on branch `refactor/audit-2026-11-05`, created from `main` on 2026-11-05.
- Method: five parallel read-only audit slices (Build passes, Analysis math, shader Semantics, Host capture with DryRun preview and Runtime, tests with repo config). One adversarial review pass followed, with two reviewers. Each reviewer re-derived every finding from source and tried to falsify it.
- No tests were run and no editor session was opened. Every claim below is source-derived. Line numbers are valid at the audited commit.
- Load assumed: one user, one desktop Unity editor, one build at a time. Dozens of renderers, up to a few hundred material slots, meshes up to about 100k triangles, textures up to 4096 by 4096. Editor sessions last hours. No servers and no multi-process concurrency. Release is a manual dispatch by a maintainer.
- Order of importance: correct, safe, holds under load, tested, fast, lean.

## What the audit checked and found healthy

- Apply revalidates captured evidence against live state before every write: policy, renderer identity, mesh shape, slot counts, effective animation state, evidence coverage, and window-clone property blocks.
- Refusal discipline holds. Every recorder throws on `None`. No programming failure is laundered into a named refusal.
- The clone sweep destroys exactly the unreferenced clones on refusal paths. Refused slots destroy their own pending clones.
- lilToon and Poiyomi version gates parse canonically and refuse on parse failure. Boundary cases are pinned by tests.
- Frontend selection matches shader names by exact ordinal comparison. Near-miss mimic names fall to Unsupported.
- The Two Pass rule lives in one place. No second implementation exists.
- Locked material reconstruction reads the locked clone and refuses on disagreement. Culture-invariant parsing is used throughout the Semantics slice.
- GPU capture gates formats through a closed allowlist. Blit color space is documented and monotone-safe for the proof. Mip residency is gated.
- Animation evidence fails closed on additive layers that carry proof-relevant bindings. The event scan caps depth.
- The settings-changed latch matches the README contract. Play mode results require a fingerprint match.
- The runtime component is data only, its defaults match the README, and it implements `INDMFEditorOnly`.
- Research package isolation holds: ban tests exist, release artifacts cannot reach it.
- The release zip self-checks for test content and requires the production assembly.
- The package ignore whitelist matches tracked reality. Manifest versions are consistent.
- Test soundness mapping found no uncovered risky branch: consent gate, attestation mismatch, apply revalidation, clone sweep, cache keys, classifier edges, seam wrapping, locked reconstruction, window close, and preset schema refusal all have behavior-named tests.

## Findings

### Must fix

#### 1. Repeat-wrap alpha pre-filter runs past its proven coordinate bound

- Where: `Packages/com.alrauna.amuse/Editor/Analysis/TriangleAlphaClassifier.cs` lines 33, 27-32, 768-771, 929-932, 1612-1615, 1765-1768, 1446-1449, 1516-1517, 431-438, 591-598, 1101-1108, 1251-1258. `Packages/com.alrauna.amuse/Editor/Analysis/ExactUvGeometry.cs` lines 441-470.
- What this is: the separating-axis pre-filter is proven safe only for texel-unit coordinates at or below magnitude 256 (`PreFilterSafeCoordinateBound`). The four clamp walks translate coordinates per candidate and honor the bound. The four repeat walks and the mapped repeat walks pass unwrapped coordinates straight in.
- Problem: after repeat normalization, one-period triangles carry coordinates up to the texture width. A 4096-class texture sits 16 times past the bound. A triangle spanning several periods reaches about 2^16, which is 256 times past the bound. At magnitude 2^12 the conversion error stack is about 1e-7 against the 1e-9 separation margin. A false "separated" answer drops the candidate before the exact intersection test runs. The no-witness arm returns ProvenOpaque, so the triangle moves to the opaque material while the GPU still samples the non-opaque texel. That is a false positive, the one failure direction the product contract forbids. No upstream gate refuses Repeat wrap or large widths. The reviewer confirmed no gate and confirmed mapped walks share the same helpers.
- Record status: design item D3 in `docs/superpowers/specs/2026-10-15-deferred-minors-remediation-design.md` records this exact defect as an open question and defers the margin derivation. The audit quantifies it. Realistic load crosses it, because Repeat wrap on 4k textures is common avatar content.
- Fix: stand the pre-filter down above the bound. When the box coordinates exceed 256 on a repeat or mapped walk, skip the pre-filter and run the exact intersection test. The pre-filter is an optimization, so every verdict stays the same and only large spans cost more time. Alternatively translate per candidate like the clamp paths do. Add one falsifier test above the bound: a 4093-wide repeat texture, unwrapped coordinates near 4000, one near-boundary non-opaque texel, expecting MustRemainTransparent.
- If we skip it: near-boundary texels on repeat-wrapped masks can prove opaque falsely and render holes after conversion.

#### 2. Streaming capture reads the authoring PNG, not the imported compressed texels

- Where: `Packages/com.alrauna.amuse/Editor/Host/UnityStreamingTextureEvidence.cs` lines 168-182. `Packages/com.alrauna.amuse/Editor/Host/SourceImageAlphaReader.cs` lines 52-54, 57-70, 72, 80-81, 88-92, 120-135. `Packages/com.alrauna.amuse/Editor/Host/UnityAlphaFieldEvidence.cs` lines 42-43, 264-266, 267-271.
- What this is: for a streaming texture, capture tries the direct route first. It reads the authoring `.png` bytes from disk and binarizes them at the material cutoff. Only a refusal falls back to the clone arm, which reads the importer decode.
- Problem: the runtime samples the imported compressed texture. BC3, DXT5, and BC7 quantize a texel to a palette level. A source texel of 130 can decode to about 125 on the GPU. With a 0.5 cutoff, the proof saw an opaque byte and the GPU discards the texel. Triangles whose consulted texels sit in that band move to the opaque material and render holes. There is no format gate on the direct route. The capture contract comment states values come from the GPU-decoded imported representation, which the direct route violates for compressed imports. Every streaming test fixture imports uncompressed, so no test catches it. Investigation Finding 5 of 2026-10-09 flagged this class. The 2026-10-10 fix landed only the gray-scale refusal. No record blesses the compressed-import case.
- Fix: one condition. Gate the direct arm on an uncompressed imported format such as RGBA32, ARGB32, Alpha8, or RGB24. Compressed imports ride the clone arm. Add one test with a compressed import near a cutoff that expects the direct route to refuse.
- If we skip it: rare false-opaque conversions on streaming cutout textures. Wrong direction again.

### Should fix

#### 3. Moved-count documentation and dead code contradict the recorded decision

- Where: `Packages/com.alrauna.amuse/Editor/Build/AmusePlatformFinishPlugin.cs` lines 226-243. `Packages/com.alrauna.amuse/Editor/Build/AlphaSeparationApply.cs` lines 749-753, 755-772, 1059-1085, 1217-1231. `Packages/com.alrauna.amuse/Tests/Editor/Build/AlphaSeparationApplyTests.cs` lines 6394-6399 and 6518-6526. `Packages/com.alrauna.amuse/Editor/Build/AmuseReportStrings.cs` lines 415-417.
- What this is: the counting behavior is a dated decision. The 2026-11-01 bimodality investigation and its 2026-11-02 amendment fixed an undercount by summing the proven ordinals of every surviving candidate, with no per-write comparison. The code matches that decision.
- Problem: three artifacts still describe the abandoned rule. The plugin state doc at lines 226-243 promises the per-write comparison. The dead twin overload `CountOpaqueTriangles(write, live, state, index)` at lines 1059-1085 has zero callers and its doc narrates a comparison its body does not implement. The test doc comment at lines 6394-6399 is stale against the pinned assertion, which states the opposite on purpose. The `!isChanged` guard in `CollectAppliedSlots` is mostly inert because marking happens before it, though it can still fire for a renderer whose only write was a no-op.
- Fix: update the plugin doc to the surviving-plan rule. Delete the dead overload, about 27 lines. Refresh the stale test doc comment. Decide the guard and say why in one comment.
- If we skip it: the next reader wires the dead comparison back, reverses a deliberate fix, and reopens the bimodality defect. Or copies the dead overload as the real path.

#### 4. Preview cache ignores animation clip edits in separate .anim files

- Where: `Packages/com.alrauna.amuse/Editor/DryRun/AlphaSeparationDryRun.cs` lines 1914-1931, 1933-1948, 1956-2015, 2022-2046, 277-293, 614-619, 627-640. `Packages/com.alrauna.amuse/Tests/Editor/DryRun/DryRunCacheTests.cs` lines 59-233.
- What this is: preview renderer rows cache on an avatar fingerprint plus a renderer body. The controller part joins controller file identity and clip count only. No clip content enters any key part.
- Problem: edit a float keyframe such as a material cutoff in a separate `.anim` file and reopen the inspector. The old rows serve with no banner. Recompile wipes the static cache, so the reopen path is the stale one. Clips stored as controller sub-assets do invalidate with the controller file. Material-swap keyframes re-key through the live probe. The build path never reads this cache, so the harm is a silent wrong preview.
- Fix: join clip file identity into the controller fingerprint. One loop. Pin it with a cache test that edits a clip and asserts a recompute.
- If we skip it: the preview contradicts scene state exactly while users edit, and the README promises the preview runs the same proof.

#### 5. Preview-only sessions grow the evidence caches without bound

- Where: `Packages/com.alrauna.amuse/Editor/Host/UnityAlphaFieldEvidence.cs` lines 63-69, 73-78, 334, 371. `Packages/com.alrauna.amuse/Editor/Host/UnityGeneratedTextureEvidence.cs` lines 27-28, 33-37, 260. `Packages/com.alrauna.amuse/Editor/Build/AmuseSessionCaches.cs` lines 29-38. `Packages/com.alrauna.amuse/Editor/AmuseAvatarOptimizerEditor.cs` lines 242-253.
- What this is: two static evidence caches store full mip chains. One 4096 chain is about 21 MiB. Only build entry and sweep teardown clear them. The inspector Refresh clears the preview cache and the play store only. A third cache, the streaming evidence cache, is cleared by nothing, not even the session reset.
- Problem: preview two avatars with dozens of 4k textures and never build. Hundreds of MiB stay pinned until a domain reload. Re-editing a texture adds a second full chain under its new key. No wrong results, pure growth.
- Fix: call the existing clear methods from the Refresh path, or bound the caches. Wire the streaming cache into the session reset at the same time.
- If we skip it: editor memory balloons across preview-heavy sessions.

#### 6. Mutable action pins in workflows that hold write permissions

- Where: `.github/workflows/build-listing.yml` lines 17-20, 40, 44-48, 52, 67, 71, 78. `.github/workflows/release.yml` line 86.
- What this is: the Pages listing workflow pins six actions by mutable major tag while holding `pages: write` and `id-token: write`. Its external automation checkout passes no ref, so it floats on that repository's default branch. The release workflow pins four actions by commit SHA but runs the unitypackage action by mutable tag inside the job with `contents: write`.
- Problem: a moved or hijacked tag executes attacker code with those permissions. The repo's own release workflow proves the SHA-pin convention. The two files disagree about applying it.
- Fix: pin every `uses:` by full commit SHA. Pin the external automation checkout to a ref.
- If we skip it: the release and Pages deployment stay one tag move away from code execution with write tokens.

#### 7. Changelog claims eight session caches, the code clears seven

- Where: `Packages/com.alrauna.amuse/CHANGELOG.md` line 39. `Packages/com.alrauna.amuse/Editor/Build/AmuseSessionCaches.cs` lines 26-38.
- What this is: the reset helper clears seven caches and says so in its contract comment. The changelog says eight.
- Problem: history settles it. The helper was created with seven on 2026-10-22. An eighth cache was added on 2026-10-24 and the changelog was updated. The eighth cache was removed on 2026-10-26, the code comment was updated, and the changelog was not. The code is right at HEAD.
- Fix: one changelog sentence.
- If we skip it: the reset list this entry advertises disagrees with itself, and a future reader may restore a phantom cache.

#### 8. Release workflow has no concurrency guard

- Where: `.github/workflows/release.yml`, whole file. `.github/workflows/build-listing.yml` lines 23-25 show the in-repo pattern.
- What this is: all release artifact and tag names derive from the package version. The workflow has no `concurrency` key.
- Problem: two dispatches for the same version race on the same tag and release object. Nothing serializes them.
- Fix: add a workflow-level concurrency group with `cancel-in-progress: false`.
- If we skip it: a half-published release needs hand cleanup.

### Nice to have

#### 9. Inspector tooltip states the wrong default

- Where: `Packages/com.alrauna.amuse/Editor/AmuseAvatarOptimizerEditor.cs` lines 624-625. `Packages/com.alrauna.amuse/Runtime/AmuseAvatarOptimizer.cs` lines 97 and 251.
- What this is: the tooltip says the preserve value 0 "is the default". The shipped and reset default is 5. The cutout twin tooltip states the Safe preset value correctly.
- Fix: one sentence in the tooltip.
- If we skip it: users learn the wrong default in the one place that explains flattening.

#### 10. vision.md pins current status to a stale commit and version

- Where: `docs/architecture/vision.md` line 7.
- What this is: the doc says its current-status statements describe commit `bd5fa9e` with package version `0.1.0-pre.1`, dated 2026-09-10. The doc describes features from `0.1.0-pre.9` and later, and the package is at `0.1.0-pre.10`.
- Fix: update or drop the pin and date the snapshot instead.
- If we skip it: the direction doc drifts further every release.

#### 11. vpm-manifest requests lilToon 2.4.0 while the locked block records 2.4.1

- Where: `Packages/vpm-manifest.json` lines 15-16 and 58-59.
- What this is: the dependency request and the locked version differ. Every other package matches. The setup doc calls the locked block the canonical environment, and the suite start check fails on any installed-versus-locked mismatch.
- Problem: a re-resolve that honors the request installs 2.4.0 and trips the start check. If VCC treats the request as a minimum, the harm is only that loud failure.
- Fix: request 2.4.1.
- If we skip it: fresh clones and re-resolves fail the suite start until someone edits the manifest.

#### 12. Report tests pin human-facing copy

- Where: `Packages/com.alrauna.amuse/Tests/Editor/Build/AmusePlatformFinishPluginTests.cs` around line 6469, against `Packages/com.alrauna.amuse/Editor/Build/AmuseReportStrings.cs` lines 62-65.
- What this is: 21 substring and 6 exact-equality string assertions pin live console emissions. A copy edit breaks them while behavior stays the same. An earlier candidate example turned out to be a store round-trip that pins no production copy.
- Fix: assert count-bearing substrings where the copy is not itself the subject.
- If we skip it: a handful of churn-only failures per copy edit. Nothing incorrect ships.

#### 13. No production cleanup for AMUSE transients when a build aborts

- Where: `Packages/com.alrauna.amuse/Editor/Build/TransientUnlockSwapIn.cs` lines 281-308. `Packages/com.alrauna.amuse/Editor/Build/AlphaSeparationApply.cs` lines 645-690. `Packages/com.alrauna.amuse/Tests/Editor/Build/AmuseGeneratedOutputCleanup.cs`.
- What this is: an exception between swap-in and the window close leaves open pairs, unlocked clones, and mesh clones in memory, plus asset-saver output on disk. No production path cleans them. The cleanup helper that exists is test-only.
- Problem: the failed build copy is discarded, so nothing ships wrong. The cost is session memory and orphan generated folders per failed build.
- Fix: a best-effort abort sweep on the error path that reverts open pairs and destroys AMUSE-owned transients.
- If we skip it: each failed build leaves session cruft behind.

#### 14. Test comment asserts an unmeasured stamp fact

- Where: `Packages/com.alrauna.amuse/Tests/Editor/Semantics/LilToonSourceAttestationTests.cs` lines 410-418.
- What this is: a comment states that lilToon 2.3.3 carries the 44 shader-format stamp. The repository's own measurement record from commit `b106675` states the opposite: the 2.3.0 to 2.3.3 artifacts are byte-identical to 2.3.4 and carry stamp 45. No measurement supports the comment.
- Problem: an adversarial reviewer followed this comment into a false finding. A future maintainer can do the same and reorder the stamp gate on a false premise.
- Fix: correct or remove the comment and cite the measurement record.
- If we skip it: a one-line trap for the next attestation change.

## Refuted during adversarial review

A candidate finding claimed the strict lilToon attestation can never admit its 2.3.0 to 2.3.3 rows because genuine 2.3.x materials carry stamp 44 and the stamp compare precedes the version gate. The reviewer refuted the premise. Commit `b106675` measured the 2.3.0 to 2.3.3 official artifacts as byte-identical to 2.3.4 at stamp 45. Green tests pin loose installs admitted through the 2.3.0 row at stamp 45. The stamp-44 claim traces to the uncited comment listed as finding 14. The structural facts about gate ordering were correct, the empirical premise was not. The candidate was dropped.

## Not checked

- Vendor package sources under `Packages/` beyond single symbol reads. They are untracked by design.
- No editor session was opened. Findings 1, 2, and 4 are source-derived. Their falsifier tests must run in the Unity Test Runner before any fix is called verified.
- The upstream lilToon 2.3.x artifacts themselves. No network access was used.
- Third-party release action behavior on an existing tag or release.
- VCC resolution semantics for dependency request versions.
- Sampled, not read in full: the 4802-line locked-source alignment file, the attestation parser middle sections, most of the 125k test lines, the preview row-builder tail, and the texture identity file lines 174-421.

## Verdict

Two soundness exposures need fixes first: the repeat pre-filter bound in finding 1 and the streaming format gate in finding 2. Both fixes are small and local. Each needs one falsifier test. The next six should-fix items are hygiene with clear owners.
