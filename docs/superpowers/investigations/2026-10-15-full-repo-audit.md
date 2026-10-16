# Full Repository Audit Investigation

Date: 2026-10-15

Revision note: first issue 2026-10-15. A parallel adversarial review the same day verified every finding against source: 27 findings confirmed, 6 carried corrected line ranges, 2 were de-rated (13 and 14), none were wrong. This issue folds in all review corrections. The review added the Coverage gaps subsection and the severity note in Caveat 1.

## Status and scope

- Branch: `audit/full-repo-audit-2026-10-15`
- Base: `899d537085d434c33aae28adde96e1d145dff871` on `main`
- Intended repository change: this investigation document only
- Production source, tests, research sources, assets, package state, CI, and prior records: unchanged
- Unity, the Census Lab project, private avatars, and external fixtures: not used

This investigation audits the whole repository for three questions:

1. Does the code match the declared architecture?
2. Which latent bugs can change behavior?
3. Which code wastes build time or editor time?

It reports findings. It applies nothing. It plans nothing.

## Privacy note

This investigation reads only public repository source. It records no private avatar, no private identifier, and no machine-specific fact.

## Prior records

The 2026-10-13 ponytail audit record predates later remediation work. A spot check on 2026-10-15 confirmed that five of seven sampled findings from that record no longer exist in current source. One sampled finding, `AnalyzeBaseMaterial` on the two shader frontends, still exists and appears in five files. This investigation reports current code only. It does not repeat historical findings unless the current source still shows them.

## Method

Six parallel read-only scouts scanned the repository on 2026-10-15:

- architectural purity across the whole production tree
- latent bugs in `Editor/Build/`, `Editor/DryRun/`, `Editor/Presets/`, and the Editor root files
- latent bugs in `Editor/Host/` and `Editor/Analysis/`
- latent bugs in `Editor/Semantics/`
- performance across all production code
- test soundness in `Tests/Editor/` plus the research package

Every finding cites a file and line range that a scout read on 2026-10-15 and that a reviewer re-read on 2026-10-15. The controller merged four cross-slice duplicates into single findings. The severity and confidence labels come from the reporting scout, adjusted by the review where noted.

## Severity meanings

- high: plausible wrong behavior, or a declared-rule violation with a real consumer
- medium: latent risk, an unclear contract, or measurable waste
- low: opportunity, contract drift, or a narrow trigger

Confidence states how sure the reviewer is that the evidence means what the finding says. Confidence does not measure severity.

## Finding summary

| # | Cat | Sev | Short name | Primary file |
|---|-----|-----|------------|--------------|
| 1 | bug | med | Shared streaming cache can serve the wrong cutoff | `Host/UnityStreamingTextureEvidence.cs` |
| 2 | bug | med | Opaque lilToon answer omits the IDMask gates | `Semantics/LilToon/LilToonMaterialSemantics.cs` |
| 3 | bug | med | Source-image block test and imported bytes can disagree | `Host/SourceImageMaskedChain.cs` |
| 4 | bug | med | No content revalidation between prepare and apply | `Build/AlphaSeparationApply.cs` |
| 5 | pure | med | Analysis and Host form a namespace dependency cycle | `Analysis/RuntimeStateResolution.cs` |
| 6 | pure | med | Streaming capture writes and reimports authoring assets | `Host/UnityStreamingTextureEvidence.cs` |
| 7 | pure | med | Avatar-level refusal reason is a free string | `Build/AmusePreparationDecision.cs` |
| 8 | perf | high | Triangle-invariant exact math recomputed per mip and per state | `Analysis/AlphaSemanticsResolver.cs` |
| 9 | perf | high | GPU alpha capture has no cache and reads past the cap | `Host/UnityAlphaFieldEvidence.cs` |
| 10 | perf | high | lilToon attestation re-hashes the include tree per material | `Semantics/UnityMaterialSemantics.cs` |
| 11 | perf | med | Exact window walk allocates per texel and lacks a pre-filter | `Analysis/ExactUvGeometry.cs` |
| 12 | perf | med | Density policy doubles the exact walk when active | `Analysis/TriangleAlphaClassifier.cs` |
| 13 | test | low | Rendering test never renders | `Tests/Editor/AmuseAvatarOptimizerEditorTests.cs` |
| 14 | bug | low | Exclusively clip-referenced locked materials never unlock | `Build/AmusePlatformFinishPlugin.cs` |
| 15 | bug | low | Play-mode rows show the unfolded plan | `Build/AmusePlatformFinishPlugin.cs` |
| 16 | bug | low | Moved-triangle counts over-claim for no-op slots | `Build/AlphaSeparationApply.cs` |
| 17 | bug | low | Stale-policy guard bypasses the build-target seam | `Build/AlphaSeparationApply.cs` |
| 18 | bug | low | Dry-run cancel leaks the cancellation source | `DryRun/AlphaSeparationDryRun.cs` |
| 19 | bug | low | Applied-slot fallback would mark refused slots applied | `Build/AlphaSeparationApply.cs` |
| 20 | bug | low | TGA sources never decode on the source-image route | `Host/SourceImageAlphaReader.cs` |
| 21 | bug | low | Null layer state machine crashes the graph walk | `Host/CommittedControllerGraph.cs` |
| 22 | bug | med | Blanket catch in the play-record loader hides defects | `Build/PlayModeBuildStore.cs` |
| 23 | bug | low | `verifyIdentity` flag honored only by one family, inverted | `Semantics/UnityMaterialSemantics.cs` |
| 24 | bug | low | Evidence-API catches are a deliberate policy with a drift trap | `Semantics/LilToon/LilToonMultiModeGate.cs` |
| 25 | pure | low | Extension-activity detection by exact message text | `Build/AmusePlatformFinishPlugin.cs` |
| 26 | pure | low | Slot refusal rows flatten every cause to one sentence | `Build/AmusePlatformFinishPlugin.cs` |
| 27 | pure | low | Poiyomi digest memo trusts mtime and skips the clearing rule | `Semantics/Poiyomi/PoiyomiMaterialSemantics.cs` |
| 28 | pure | low | Process-global disk-fallback switch | `Host/UnityStreamingTextureEvidence.cs` |
| 29 | perf | low | Whole-submesh classification duplicated per state (documented) | `Analysis/AdmittedMaterialStates.cs` |
| 30 | perf | low | Inspector repaint reparses the play record every repaint | `Editor/AmuseAvatarOptimizerEditor.cs` |
| 31 | perf | low | Affine mapping decoded inside the per-triangle loop | `Analysis/AffineUvTransform.cs` |
| 32 | perf | low | Red-field contract assertion rescans every texel | `Host/UnityMaterialEvidenceCapture.cs` |
| 33 | perf | low | Version read spins up to 30 s and clears its cache per build | `Semantics/PackageVersionReader.cs` |
| 34 | test | low | Signature-only pin on a private editor method | `Tests/Editor/AmuseAvatarOptimizerEditorTests.cs` |
| 35 | test | low | "of out" typo pinned as spec | `Editor/DryRun/MaterialTransformationRow.cs` |
| 36 | test | low | Research census memo scoped per avatar, not per run | research `Collection/CensusShaderFamily.cs` |
| 37 | test | low | Retired census category carries no retirement note | research `Census/CensusCategories.cs` |

Paths in the table drop the `Packages/com.alrauna.amuse/` prefix. Research paths are relative to `Packages/com.alrauna.amuse.research/Editor/`.

## Findings

### Architectural purity

**5. Analysis and Host form a namespace dependency cycle; Analysis orchestrates live capture.** The product has one production editor assembly. Analysis and Host are namespaces inside it, and they reference each other: `Analysis/RuntimeStateResolution.cs:78-79` calls `UnityRendererAlphaAnalysis.StructuralRefusalFor`, `:89` calls `UnityAnimationEvidenceCapture.ResolveProofRelevant`, `:140-143` calls `UnityRendererAlphaAnalysis.GatherAlphaFields`, which is the GPU capture entry. `:340-350` calls `UnityRendererAlphaAnalysis.Classify`. `Analysis/AdmittedMaterialStates.cs:4,843-844,1040-1046` uses Host types and defaults the resolver to `UnityMaterialSemantics.AnalyzeAlphaMaterial` at `:315`. Host imports Analysis in the other direction (`Host/AlphaFieldSet.cs:3`, `Host/SourceImageMaskedChain.cs:2`, `Host/UnityRendererAlphaAnalysis.cs:4`, among others). The declared layering is a DAG: Build calls every module, Host touches Unity, Analysis is pure. The code instead has two mutually referential module namespaces, and the proof core triggers texture capture mid-proof. Purity of Analysis holds by discipline only. Severity medium. Confidence high.

**6. The streaming capture route creates, reimports, and deletes assets in the authoring project.** `Host/UnityStreamingTextureEvidence.cs:43` pins the folder `Assets/Amuse.StreamingReadback`. `:153` copies the asset into it. `:166-168` sets `isReadable` and `streamingMipmaps` on the clone importer and calls `SaveAndReimport()`. `:239-248` deletes the clone in a `finally`. This is the only production site outside the apply path and the sanctioned conversion recipes that writes and reimports project assets, and it sits in capture. A crashed build can leave an imported clone behind. The capture engine's own contract at `Host/UnityAlphaFieldEvidence.cs:21-22` says "it opens no TextureImporter, writes no asset, and never changes an import setting", so the route also breaks the documented invariant. Related: the dry-run disables this route (`DryRun/AlphaSeparationDryRun.cs:974`), so a preview can refuse captures a real build accepts. Severity medium. Confidence high on behavior; the design round must decide whether the team classifies the route as sanctioned.

**7. The avatar-level preparation refusal reason is a free string.** `Build/AmusePreparationDecision.cs:26-37` stores `RefusalReason` as `string`. The apply caller passes an enum `ToString()` (`Build/AlphaSeparationApply.cs:190-192`). A preparation caller concatenates a prefix with a detail string into a report record (`Build/AlphaSeparationPreparation.cs:575-578`). Every smaller scope uses a closed enum. The one decision that gates the single mutation does not. Severity medium. Confidence high.

**25. Extension-activity detection compares exact exception message text.** `Build/AmusePlatformFinishPlugin.cs:404-421` catches an exception with an exact-type filter and a message that must ordinal-equal a fixed NDMF diagnostic string, and treats that as "extension inactive". A wording change in NDMF fails the build with the wrong arm. A failure-mode change silently drops to the legacy capture shape. Severity low. Confidence medium.

**26. Slot refusal rows flatten every renderer refusal cause into one sentence.** `Build/AmusePlatformFinishPlugin.cs:1490-1504` emits a fixed "Refused: AMUSE currently does not support this material" row per slot for any renderer refusal, regardless of the named cause and regardless of whether the slot holds a material. The apply-side row builders carry the same copy defect as finding 35 (`Build/AlphaSeparationApply.cs:1175`, `:1179`) but emit no fixed refusal row. The console surface reports the exact cause; the persistent play-mode view does not. Severity low. Confidence high.

**27. The Poiyomi digest memo trusts file mtime and skips the clearing rule.** `Semantics/Poiyomi/PoiyomiMaterialSemantics.cs:2488-2514` caches the normalized source digest keyed by asset path and validates only `LastWriteUtc` equality before reuse. The digest pins the identity conjunction. Every sibling session cache is cleared per build (`Build/AlphaSeparationApply.cs:1047-1050`, `Build/AmusePlatformFinishPlugin.cs:457-460`); no clear exists for this memo. A content change inside filesystem timestamp resolution, such as an in-place shader rewrite, attests modified source against the pinned digest. Severity low. Confidence high on mechanism.

**28. A process-global switch controls the disk-fallback capture route.** `Host/UnityStreamingTextureEvidence.cs:56-70` exposes `internal static bool AllowDiskFallback` with a scope struct. A leaked scope or an unscoped assignment changes evidence availability for later real builds in the same Editor session. Current callers all use the `using` scope. Severity low. Confidence high on mechanism.

### Latent bugs

**1. The shared streaming cache can serve the wrong cutoff.** `Host/UnityStreamingTextureEvidence.cs:95` quantizes the cutoff to one part in ten thousand for the cache key, while the chain binarizes by the exact float at `:224-226`. Two materials that share one streaming texture with cutoffs in the same bucket, whose byte thresholds straddle an integer, get one chain for both predicates. Worked example, re-verified by the review: cutoffs 0.50196 and 0.50205 both land in bucket 5020; byte 128 decodes to about 0.50196, which passes the first cutoff and fails the second. The second material can receive false opaque evidence, and a triangle can then convert while playback discards that fragment. The generated route keys the exact value (`Host/UnityGeneratedTextureEvidence.cs:104-106`); this cache is the odd one out. Severity medium. Confidence high on mechanism. The trigger domain is narrow.

**2. The opaque lilToon answer omits the IDMask fragment-removal gates.** `Semantics/LilToon/LilToonMaterialSemantics.cs:478-482` gates only `_Invisible` and `_UDIMDiscardCompile`. The cutout sibling gates the full fragment-removal set including `_IDMask1..8` (`LilToonCutoutMaterialSemantics.cs:65-81`), and the transparent sibling gates the same set (`LilToonTransparentMaterialSemantics.cs:87-102`). If the vendor does not compile the IDMask discard out at `LIL_RENDER == 0`, an IDMask material is answered as fully opaque while the vendor discards fragments. That is a false ProvenOpaque on the proof core's most sensitive output. No comment in the opaque file asserts the compile-out claim; its own rule text concedes that fragment-removal mechanisms change coverage. Severity medium. Confidence medium.

**3. The source-image block test and the imported bytes can disagree within half a byte.** `Host/SourceImageMaskedChain.cs:91-99` compares exact integer block sums against bounds. The imported chain that playback and the GPU route consult stores per-mip box averages rounded to 8 bits, and its predicate compares the stored byte (`Host/Shaders/AmuseAlphaExactOne.shader:88-93`). A block whose exact average sits below a bound but rounds to it resolves erased on one route and present on the other. Worked examples, re-verified by the review: bytes {5, 6} with noise bound 6 sum to 11, below 12, so the source route erases while the imported chain keeps byte 6; bytes {127, 128} against opaque bound 128 diverge the same way. The erased-texel path feeds density substitution in the classifier, so the divergence touches proof outcomes for streaming textures. Severity medium. Confidence high on the arithmetic.

**4. Apply revalidates identity and shape but not serialized material content.** `Build/AlphaSeparationApply.cs:231-247` re-checks mesh reference, slot count, and submesh counts. `:292-297` re-checks block state, which reads only renderer property-block overrides (`Host/EffectiveMaterialMaterialization.cs:187-236`). `:384-402` and `:724-778` re-check captured bindings. Nothing re-reads the material property values that the opaque clones were derived from at barrier time (`Build/AlphaSeparationPreparation.cs:820-905`). A same-reference in-place mutation of an admitted material between the barrier and apply ships a stale clone. Whether any NDMF pass runs in that window was not verifiable in this repository. Severity medium. Confidence medium.

**14. Exclusively clip-referenced locked materials never unlock.** `Build/AmusePlatformFinishPlugin.cs:585-588` scans only `renderer.sharedMaterials` when deciding consent, and consent is granted only when the window opens (`:622-626`). The swap-in guard returns before any swap without consent (`Build/TransientUnlockSwapIn.cs:76-90`). The clip-only discovery pass at `:150-176` does fire on avatars where at least one assigned locked material attests. But an avatar whose locked materials are all referenced only by animation clips never opens the window, so the documented clip-only capability never fires there. The outcome is conservative. The documented contract and the gate disagree. Severity low. Confidence high.

**15. Play-mode rows describe the unfolded plan.** `Build/AmusePlatformFinishPlugin.cs:1011-1013` records the barrier's unfolded plan, while the preserve fold happens on a local copy inside `Build/AlphaSeparationPreparation.cs:131-169`. A folded slot reports an unconverted splittable sentence, and `movedTriangles` records against unfolded numbers (`Build/AlphaSeparationApply.cs:1104-1108`). Users tuning the preserve gate see a view that contradicts the applied result. Severity low. Confidence high.

**16. Moved-triangle counts over-claim for surviving no-op slots.** `Build/AlphaSeparationApply.cs:1050-1094` keys applied slots on the renderer having any write and adds every surviving candidate slot. `:1096-1110` then sets `movedTriangles = provenTriangles` for each such row, and the rebuild path mirrors this at `:1152`. An already-opaque admitted material maps to itself (`Build/AlphaSeparationPreparation.cs:1136-1144`), so such a slot can report movement that never happened. Severity low. Confidence high.

**17. The apply stale-policy guard bypasses the build-target test seam.** The barrier captures the target through `AmusePlatformFinishPlugin.ActiveBuildTarget()` (`Build/AmusePlatformFinishPlugin.cs:689-690`), which honors the test override at `:294-299`. The apply guard reads `EditorUserBuildSettings.activeBuildTarget` directly (`Build/AlphaSeparationApply.cs:150`). The seam's doc names the barrier pass as its reader. Production behavior is unaffected as observed on 2026-10-15. Severity low. Confidence high.

**18. Dry-run cancellation leaks the cancellation source for in-flight work.** `DryRun/AlphaSeparationDryRun.cs:436-455` waits a bounded timeout, then `:457-480` disposes the source only when the in-flight task is already complete. Inspector close and assembly reload cancel with a 200 ms timeout (`Editor/AmuseAvatarOptimizerEditor.cs:66,72`), so a running classification abandons the source undisposed. Severity low. Confidence high.

**19. The applied-slot fallback would mark refused candidates applied if ever reached.** `Build/AlphaSeparationApply.cs:1073-1076` falls back to all prepared candidates when the surviving-candidate list is missing or short, and `:1078-1090` then marks those rows applied. The fallback is unreachable on current paths, but any future path that reaches it converts refused slots into moved-triangle rows. Severity low. Confidence high.

**20. The source-image route can never decode TGA.** `Host/SourceImageAlphaReader.cs:52-54` admits `.png` and `.tga`, but the decode at `:80-84` uses `ImageConversion.LoadImage`, which Unity documents for PNG and JPG data. TGA always fails and falls to the readable-clone route, which concedes its coarser levels arrive pre-averaged (`Host/UnityStreamingTextureEvidence.cs:184-190`). Degraded fidelity, fail-closed direction. Severity low. Confidence high.

**21. A committed controller layer with a null state machine crashes the graph walk.** `Host/CommittedControllerGraph.cs:117-127` iterates layers. `EnumerateLayer` passes `layer.stateMachine` into `WalkStateMachine` at `:199-206`, which dereferences `machine.behaviours` at `:211-213`. Nothing null-checks. A malformed scripted controller is a malformed-asset input and should produce a named refusal, not a `NullReferenceException`. Severity low. Confidence medium.

**22. The play-record loader blanket-catches every exception.** `Build/PlayModeBuildStore.cs:80-94` wraps deserialization in a bare `catch` that erases the stored record and returns none. The inspector consumes this load path to decide what the play-mode view shows. A serializer contract defect becomes a silent fallback to the dry-run view instead of a visible defect. The repo rule says programming defects throw. No test exercises a corrupt payload. Severity medium, raised from the scout's low by the review under the rubric: a declared-rule violation with a real consumer, whose wrong behavior still requires a pre-existing defect. Confidence high.

**23. The `verifyIdentity` flag is honored only by the Poiyomi arms and inverted there.** `Semantics/UnityMaterialSemantics.cs:887-929`: the Poiyomi arms return `verifyIdentity && TryVerifyPoiyomiIdentity(...)` at `:903-905` and `:911-914`, so `false` classifies an attested Poiyomi material as unattested. The lilToon arms at `:915-925` ignore the flag and always verify. Both current call sites pass `true` (`:336`, `:464`). A contract trap, not a soundness hole. Severity low. Confidence high.

**24. Evidence-API `ArgumentException` catches are a deliberate policy with a drift trap.** `Semantics/LilToon/LilToonMultiModeGate.cs:648-749` catches request refusals and answers "absent". The site comments declare this intentional and vendor-mirrored. The request lists are built from the same consulted arrays (`LilToonMultiResolution.cs:68-84`), so a consulted-but-unrequested name cannot arise without editing the shared source. The trap: future edits that break that pairing would silently read as feature-off instead of throwing. Severity low. Confidence medium.

**35. A user-visible copy defect is pinned as spec.** `Editor/DryRun/MaterialTransformationRow.cs:189`, `:196`, `:201` produce "of out"; the sibling at `:206` says "out of". `Tests/Editor/DryRun/MaterialTransformationRowTests.cs:287-342` pins the sentences verbatim; five of the seven verbatim pins carry "of out". Fixing the visible text requires touching production plus the pins. Severity low. Confidence high.

### Performance

**8. Triangle-invariant exact math is recomputed per mip level and per admitted state.** `Analysis/RuntimeStateResolution.cs:329-352` classifies the whole submesh once per admitted state. `Analysis/AlphaSemanticsResolver.cs:446-467` classifies once per mip level inside one state. Every call re-runs position finiteness and degeneracy checks that decode positions into BigIntegers (`Analysis/TriangleAlphaClassifier.cs:320-329,430-439`, `Analysis/ExactUvGeometry.cs:223-245`), and re-decodes the same six UV floats per window dispatch (`ExactUvGeometry.cs:430-443`). Degeneracy and finiteness depend only on the triangle. With the default mip cap of four (`Runtime/AmuseAvatarOptimizer.cs:42`) that is five duplicated exact-geometry passes per state, times the state count, on every build path. Severity high. Confidence high.

**9. The GPU alpha-field route has no cache and reads every resident mip level, then discards levels above the cap.** `Host/UnityAlphaFieldEvidence.cs:317-356` blits and reads back every resident level of every texture; non-resident levels get a placeholder without readback. The generated and streaming routes cache; this route does not. The per-renderer batch dedup at `Host/UnityMaterialEvidenceCapture.cs:944-1002` lives inside one capture call. The user's mip cap applies only afterwards at `Host/UnityRendererAlphaAnalysis.cs:777-812`. A texture shared by several renderers is read back once per renderer, and with the default cap on a 4k texture most read-back levels are thrown away. Severity high. Confidence high.

**10. lilToon source attestation re-reads and re-hashes the whole include tree per material.** `Semantics/UnityMaterialSemantics.cs:494-560` gathers per material index. Each gather enumerates the include folder, hashes every file, canonicalizes the shader and pass files, and re-splits the pass text per scan (`Semantics/LilToon/LilToonSourceAttestation.cs:2493-2494`, `:69`, `:1459`, `:1487`); its hash call is uncached. Thirty lilToon materials re-hash the same include tree thirty times per renderer batch. The Poiyomi sibling already demonstrates the memo pattern (`Semantics/Poiyomi/PoiyomiMaterialSemantics.cs:2488-2514`). Severity high. Confidence high.

**11. The exact window walk allocates fresh lists per candidate texel, and the point paths lack the bilinear pre-filter.** `Analysis/ExactUvGeometry.cs:511-543` copies the hull and runs up to four clip passes per intersection, each allocating, with rational boundary math normalizing through `BigInteger.GreatestCommonDivisor` (`:41`, `:766-789`). The bilinear paths prune with a double-precision SAT pre-filter (`Analysis/TriangleAlphaClassifier.cs:838-851`); the point paths at `:588-604` and `:1496-1513` call `Intersects` for every non-opaque texel with no pre-filter, bounded by the 65536 support-region cap (`:285`). Severity medium. Confidence high.

**12. The density policy pre-pass repeats the full exact walk when active.** `Analysis/TriangleAlphaClassifier.cs:693-727` walks the whole candidate window including opaque texels, then the main walks at `:588-604` and `:1496-1513` repeat the same intersections. The bilinear variant has the same doubling at `:1044-1090`. Only active when the density cap is nonzero, which the default policy avoids. Severity medium. Confidence high.

**33. The producer-version read spins up to 30 seconds per producer and its cache clears every build.** `Semantics/PackageVersionReader.cs:39-45` blocks on a completion poll with a sleep loop bounded at 30 seconds. `AdmittedProducerVersions` caches per session, but both producers' caches clear on every build (`Build/AlphaSeparationApply.cs:1049-1050`, `Build/AmusePlatformFinishPlugin.cs:459-460`). An unresponsive package manager stalls a build pass for up to a minute total. Severity low. Confidence high on mechanism.

**29. Whole-submesh classification is duplicated per admitted state by documented decision.** `Analysis/AdmittedMaterialStates.cs:176-207` declines to merge resolutions, even reference-identical chains, and a test pins the non-merge. The code documents the trade-off. The design round must weigh the change against that documented refusal. Severity low. Confidence high on behavior.

**30. The inspector reparses the play record every repaint.** `Editor/AmuseAvatarOptimizerEditor.cs:243-272` deserializes the record JSON and rebuilds every row per repaint while the foldout is open. Rows are few. Severity low. Confidence high.

**31. The affine mapping decodes per-material scale and offset inside the per-triangle loop.** `Analysis/AffineUvTransform.cs:35-49` decodes four constants per call, and the resolver calls it per mip per triangle on mapped materials (`Analysis/AlphaSemanticsResolver.cs:440-446`). Severity low. Confidence high.

**32. The red-field contract assertion rescans every texel of every captured red chain.** `Host/UnityMaterialEvidenceCapture.cs:1452-1473` triple-loops every mip and texel after each capture, per renderer. Measurable CPU next to finding 9. Severity low. Confidence high.

### Test soundness and research

**13. The "rendering" test asserts only fixture construction and never renders.** `Tests/Editor/AmuseAvatarOptimizerEditorTests.cs:269-278` builds a row and asserts the sentence it just passed in. `DrawTransformationRow` is never invoked. The deferred row's draw path is in fact covered elsewhere in the same file through real host-window rendering (`:414-455`, `:462-505`), and the factory behavior is pinned at `Tests/Editor/DryRun/MaterialTransformationRowTests.cs:212-230`. The defect is a redundant test whose name promises coverage it does not provide. Severity low, de-rated from the scout's medium by the review. Confidence high.

**34. A reflection test pins only a private method's signature.** `Tests/Editor/AmuseAvatarOptimizerEditorTests.cs:256-267` asserts a two-parameter signature and nothing else. Real rendering behavior is exercised elsewhere in the same file. Severity low. Confidence high.

**36. The research census attestation memo is scoped per avatar, not per run.** research `Editor/Collection/CensusShaderFamily.cs:29-31` promises run-scoped reuse, but `Editor/Collection/AvatarCensusCollector.cs:67` constructs a fresh memo per avatar, so corpus-scale runs re-hash the same vendor shader sources for every avatar. Cost only. Severity low. Confidence high.

**37. A retired census category keeps no retirement record at its definition.** research `Editor/Census/CensusCategories.cs:39` retains `MaterialPropertyOverridesPresent` with no note, while a sibling retired value carries one at `:51-57`. `Editor/Census/CensusAggregator.cs:179-181` seeds every member, so published aggregates carry a permanent zero row that reads as an observation. Severity low. Confidence high.

## Caveats the design round must resolve

1. Findings 1, 2, and 3 are the only findings that can flip a proof outcome toward conversion. They sit at medium, not high, because each needs a narrow trigger domain: two cutoffs straddling a byte boundary in one quantization bucket (1), an unresolved vendor compile-out premise (2), and block averages within half a byte of a bound (3). A confirmed flip on a plausible avatar would be high. Any design that touches them must keep `MustRemainTransparent` absorbing and Unknown absorbing. Finding 2's resolution needs the pinned vendor source facts or a conservative gate addition; a code-only argument cannot settle it.
2. Finding 4 (prepare-to-apply content revalidation) depends on an external question: whether NDMF can interleave another plugin's pass between the barrier and apply. The design round must name the verified NDMF ordering facts before choosing a fix.
3. Finding 6 sits on a documented contract that the route itself breaks. The design round must decide between repairing the contract text and moving the route out of the authoring asset space. Both are owner decisions with different cost.
4. Findings 8 through 12 are pure performance. They must not change any classification outcome. Exact-math refactors in `Analysis/` need the existing fixture matrix as the outcome oracle, and the repository's RED/GREEN rule applies.
5. Findings 15, 16, and 26 are reporting defects on the play-mode surface. A fix changes what users see, so sentence pins in tests move with them deliberately, not silently.
6. Finding 35 changes user-visible text that tests pin as spec. The pin was written from the code; the fix must update both and record that the pin was corrected.
7. The research findings (36, 37) affect census validity, not the product. The research package never ships. They are recorded for completeness.
8. This investigation proposes no scope beyond the findings above. Archival records from 2026-10-08 through 2026-10-14 already cover adjacent safety rounds; this document reports only what current source still shows on 2026-10-15.

## Coverage gaps

The review named five areas no slice claimed to read. They are recorded as gaps, not findings:

- `Runtime/AmuseAvatarOptimizer.cs` and the runtime assembly: the serialized policy knobs that the whole proof reads.
- `.github/workflows/`: the release job enforces the research-exclusion and test-exclusion guarantees.
- `Tools/Bootstrap-NdmfStandalone.ps1`: the NDMF pin machinery every finding assumes.
- The shipped preset JSON values under `Editor/Presets/`: a nonzero density cap or a large mip cap there silently activates findings 11 and 12.
- `Tests/Editor/ReferenceFixtures/`: the outcome oracles that Caveat 4 relies on were not audited.

## Unverified leads

- Poiyomi outline-alpha fold ignores the `_OutlineOverrideAlpha` value (`Semantics/Poiyomi/PoiyomiMaterialSemantics.cs:1445-1454`). Vendor semantics cannot be checked in this repository.
- Opaque lilToon `_UseDither` non-gating rests on an uncited compile-out claim (`Semantics/LilToon/LilToonMaterialSemantics.cs:471-476`).
- Unassigned `_OutlineTexture` treated as sampling exactly one (`Semantics/Poiyomi/PoiyomiMaterialSemantics.cs:1497-1501`) assumes a white default the repository cannot confirm.
- `PackageVersionReader` comment claims Unity 2022.3 lacks a synchronous completion API. If false, the spin loop duplicates a platform feature.
- Cutoff-field precision asymmetry across capture routes (GPU route captures under inert bounds; source routes binarize by cutoff). Each direction is individually sound; no consumer requiring cross-route comparability was found.
- `ClonePersistRestoreVerify` destroys a saved clone on restore mismatch; whether the NDMF asset container prunes the dangling entry was outside read scope.
- 16-bit PNG sources decode with rounding that can differ by one step from the importer's downscale. Same consequence family as finding 3.
- BC-compressed imports add block-quantization error on top of mip rounding, plausibly widening finding 3.
- A research anonymizer key concatenates GUID, path, and name with spaces; a theoretical collision merges two materials into one ordinal. No real collision shown.
- `PlayModeBuildStore.SaveOutcome` uses `state.SamplingTarget != 0` as an unset sentinel; no valid target value of zero was confirmed or excluded.

## Coverage

- `Editor/Build/` (all 19 files): `AmusePlatformFinishPlugin.cs`, `AlphaSeparationApply.cs`, `AlphaSeparationPreparation.cs`, `AlphaSeparationRecords.cs`, `AmuseStructuralGraphCheck.cs`, `AmuseAnimatorBindingsCapture.cs`, `TransientUnlockSwapIn.cs`, `TransientUnlockWindowClose.cs`, `TransientUnlockWindowState.cs`, `TransientUnlockAvailability.cs`, `HostLifecycleCapability.cs`, `PlatformAlphaSamplingPolicy.cs`, `LockedMaterialIdentity.cs`, `LockedMaterialReconstruction.cs`, `AmusePreparationDecision.cs`, `PlayModeBuildStore.cs`, `VersionConsentDialog.cs`, `AmuseReports.cs`, `AmuseReportStrings.cs`
- `Editor/Host/` (all 19 files): full reads of `UnityRendererAlphaAnalysis.cs`, `UnityAlphaFieldEvidence.cs`, `UnityStreamingTextureEvidence.cs`, `UnityGeneratedTextureEvidence.cs`, `SourceImageAlphaReader.cs`, `SourceImageMaskedChain.cs`, `UnityMaterialEvidenceCapture.cs`, `EffectiveMaterialMaterialization.cs`, `UnityAnimationEvidenceCapture.cs`, `LiveAnimationObservation.cs`, `CapturedAnimationEvidence.cs`, `CommittedControllerGraph.cs`, `AlphaFieldSet.cs`, `BehaviourIdentity.cs`, `BlockStateEntry.cs`, `UnityRendererAlphaSnapshot.cs`, `TextureCaptureRefusal.cs`, `TextureCaptureRefusalReason.cs`, `ReferenceEqualityComparer.cs`, plus the two predicate shaders
- `Editor/Analysis/` (all 11 files): `TriangleAlphaClassifier.cs`, `ExactUvGeometry.cs`, `AffineUvTransform.cs`, `AlphaMipChain.cs`, `AlphaPolicyBounds.cs`, `AlphaSemanticsResolver.cs`, `AlphaSamplingPolicy.cs`, `RuntimeStateResolution.cs`, `AdmittedMaterialStates.cs`, `MeshSeparationPlanner.cs`, `SlotTransformDecision.cs`
- `Editor/Semantics/` (all 40 files): the value algebra, `UnityMaterialSemantics.cs`, `UnityTextureEvidence.cs`, the attestation and identity files, both `Poiyomi/` files, and every `LilToon/` module
- `Editor/DryRun/` (3 files), `Editor/Presets/` (5 files), the 4 Editor root files: `AlphaSeparationDryRun.cs`, `DryRunCache.cs`, `MaterialTransformationRow.cs`, the preset parser and store, `AmuseAvatarOptimizerEditor.cs`, and the remaining root files
- `Tests/Editor/` (sampled broadly, prioritizing Build and Analysis suites) and the research package's collection and census sources with their test suites
- Not read, recorded as gaps: see "Coverage gaps" above.
