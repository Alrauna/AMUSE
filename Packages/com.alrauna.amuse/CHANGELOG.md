# Changelog

All notable changes to AMUSE are documented in this file.

## [Unreleased] - 2026-10-21

### Added
- Support for the 9.3 main and Two Pass shaders that Poiyomi 10.x packages bundle. The support covers unlocked materials and locked materials whose recorded original is a 9.3.64 release shader or one of the two bundled 9.3 rows. The bundled locked rows follow the same reconstruction contract as the release rows.

### Fixed
- A material animation binding of a proof-relevant property with a rename suffix now refuses its renderer. The rule is shader-independent and also covers lilToon. The binding name is a proof-relevant property name followed by an underscore and a suffix, for example `material._Color_<suffix>`. Bindings such as a texture's `_TexelSize` or `_HDR` on a texture whose scale-offset AMUSE reads also refuse.
- A locked Poiyomi material refuses unless its recorded original shader name tag equals the resolved shader name. An orphan lock with no recorded original keeps its earlier behavior.
- 2026-10-29. Locked-source alignment proves when the Thry 2.74.3 rules require a compiler guard to stay. Nested lookahead stays inside the proved frame's current branch.
- 2026-10-29. Locked-source alignment shares one validated Properties scan for multiline display strings, rename records, and Gamma attributes. Malformed Properties text refuses instead of guessing material metadata.
- 2026-10-29. The Thry 2.74.3 alignment model matches property reads inside local initializers. It replays the vendor occurrence edits before recording a bake. Literal format follows surviving declarations and stays isolated between block trials.
- 2026-10-29. A single texture sampler declaration no longer creates a false live-use fact in the Thry 2.74.3 alignment model. Real texture reads and exact rename checks stay intact.
- 2026-10-30. Thry 2.74.3 alignment keeps the stream order of frame delimiters when a wrapper that opened inside a preprocessor frame ends after its else or close line. The close is matched once. A frame that the stream leaves without a close refuses.
- 2026-10-30. The locked rebuild accepts the live value of a program-unused operand when its declared default fails the removed condition and the live value holds every removed condition that reads the operand. Real Thry 2.74.3 locks of the bundled 9.3 shader carry such operands, and those locks refused before.
- 2026-10-31. The Thry 2.74.3 alignment model matches a later occurrence of a baked property on one line when another property's unprocessed occurrence sits between the uses. The model splices the recorded print of the first occurrence and the exact replay verifies the line. Real Thry 2.74.3 locks of the bundled 9.3 shader carry such lines, and those lines refused before.
- 2026-11-01. The Thry 2.74.3 alignment model matches a later occurrence of a live property kept under its identifier spelling when another property's unprocessed literal sits between the uses. The model keeps the recorded identifier kind and the exact replay verifies the line. A texture sampling line with two name uses and three sibling bakes carried this shape, and the whole locked family that contained it refused before.
- 2026-11-01. Every analyzed play build reports one untouchable census entry. The entry names how many material slots with how many triangles stayed fully on their original materials with nothing movable. Kept slots print no other console line, so two builds that differ only in silently kept slots left no trace to compare. The census entry closes that gap.
- 2026-11-01. The moved triangle count treats a slot that holds a transient unlock clone as moved even when the applied write returns the same clone instance. The window swap in already replaced the locked original with the reconstruction, so the same-instance write is an expected no-op and the slot's proven triangles still moved. Before, the count depended on material instance identity across the window cycle, so the same avatar reported two different totals in different editor sessions while shipping the same result.
- Play mode results are keyed by the proved global identity of each avatar root. Same-named roots, twins of one prefab, and roots in scenes with duplicate names keep separate results. A rename or a reorder keeps the record. A root without a proved identity, such as an avatar in an unsaved scene or a copy made during Play, holds no record.
- The Show Material Transformations preview rejects a run after any setting change. This includes a change made while background work runs. Changing the setting back does not restore the rejected results. Select Refresh to compute new results.
- The Show Material Transformations preview keeps a recorded refusal cause on the slot row. A closure failure or another named permanent cause now keeps the refusal even when the current material is locked or holds an unreadable streaming texture. The deferred display stays for the valid locked current material and the unnamed streaming capture refusal.
- Poiyomi material semantics no longer fail on static initialization order. The alpha schema now lives in the alpha interpreter before its request initializers. A fresh process that enters the interpreter first initializes the alpha and full requests without error.
- The package listing download menu no longer stacks one download action per menu open. The menu item now holds one stable action that reads the package address stored by the last row-button click. The row button stores its own address on every activation, including when the menu is already open, and a row click no longer also closes the open menu. The download opens in a new tab without an opener relationship.
- 2026-11-05. The locked rebuild verifies a baked texel size by the shape of the baked text alone. The literal must round trip as a float4. Its first two components must equal the reciprocal print of its two size components. The size components must be positive integers. The check no longer compares the bake against a fresh print of the texture on the values snapshot. A texture that changed size or identity after the lock no longer refuses its material. The rebuild also gains one texture identity check. For every texture property, the texture identity that the clone snapshot resolves must equal the texture identity that the locked original snapshot resolves for the same name through the aligned rename mapping. A disagreement refuses with one fixed sentence that names no property. A locked build cycle after the change ran clean. The previously refused slots converted, and the locked cycle total matched the unlocked baseline for the same content.

- 2026-11-06. The build passes and the inspector Refresh button reset the streaming evidence cache with the other session caches through the shared reset helper. Before, the reset list omitted the streaming cache, so a second build or a refresh in one editor session could serve a capture taken before that reset.

### Changed
- AMUSE rebuilds a locked Poiyomi material from its generated locked shader when its recorded original is a 9.3.64 release shader or one of the two bundled 9.3 rows. The build aligns the generated text with the attested original shader text. An edit after the lock that changes what the locked material renders is reproduced on the rebuild, or the build refuses the slot with a named cause. A locked material refuses when a renderer that shows it carries a material property block. A locked material refuses when an animation binds a property that the locked shader bakes, renames, or ignores. The refusal covers every slot of that material on every renderer that shows it. A locked material whose animations bind renamed, baked, or unused properties stays refused until a later curve rewrite. That refusal is a known coverage gap.
- Poiyomi attestation checks the shader file. The check covers the shader name, the file GUID, and the canonical digest. The package name and the package version no longer take part.
- An old 9.x shader file can share a pinned GUID with different bytes. Such a file reports a modified source in the conversion detail.
- The research probe finds the admitted shader file. The probe no longer looks up only the 9.3.64 shader name.
- Stored transformation rows now carry the typed `MaterialTransformationRowKind` through the whole record path. The build pass, the apply pass, the store reader, and the tests name the kind instead of a raw number. The stored numbers 0 through 4 stay unchanged, so records stored earlier in this session still load and draw the same rows.
- Resolved runtime states carry their material slot evidence beside the per-slot results. The build pass and the preview read the paired evidence from the result and no longer derive the slot list a second time. A renderer-wide early refusal keeps empty slot evidence and empty per-slot results. Preparation keeps its own independent slot derivation.
- Capture tests for RGB24 and ARGB32 no longer pin a specific backend's sampling capability. RGB24 keeps its source-format Sample support exemption, and its test checks full-chain opaque alpha. ARGB32 follows the exact sampling gate. Without exact support, its test asserts the real capture refusal with no evidence. With exact support, it checks 255, which passes the opaque predicate, and 128 and 254, which do not.
- The consent scan calls the shared approved-identity predicate instead of spelling its rule again. The earlier attestation filter stays in place. Consent subjects, granted names, and the approved-version flag keep their behavior.
- The build passes reset the eight session caches through one shared helper. The fixed call list now lives in one place. The build-entry reset before the lifecycle refusal and the clone-sweep reset after teardown keep their separate moments and their order.
- The lilToon property contract test no longer pins an exact constant count. The durable contract is at least one property constant, underscore-prefixed values, and unique values. A valid new property constant needs no test edit.

## [0.1.0-pre.10] - 2026-10-21

### Added
- Dry run note for a shader version inside the supported range that does not verify against its measured source. The note names the cause and says the triangles stay unproven.

### Changed
- lilToon 2.4.1 is a measured version. Exact attestation rows now cover 2.3.0 through 2.4.1. Multi container rows cover it too.

## [0.1.0-pre.9] - 2026-10-20

### Added
- Cutout Separator and Transparent Separator sub-switches below the Alpha Separator switch. Presets store both switches.

### Changed
- Preset schema version moves from 4 to 5. The new feature keys are `cutoutSeparator` and `transparentSeparator`. A schema 4 file refuses with the schema version message.
- lilToon 2.3.0 through any version below 2.5 are approved without a consent prompt. The build asks once per build for versions outside that range.

## [0.1.0-pre.8] - 2026-10-19

### Added
- Exact rational polygon alpha classification proving opacity across UV coordinates.
- Non-destructive mesh splitting separating proven-opaque triangles to canonical opaque materials.
- Shader frontends for lilToon (versions 2.3.0 to 2.4.0, with version-aware Multi container rows) and Poiyomi Toon Shader (version 9.3.64, including Two Pass).
- EditMode DryRun preview inspecting avatar materials and expected polygon splits.
- PlayMode build record store caching transformation results for inspection.
- Optimization presets: Safe, Normal, and Aggressive.
- Consent gate prompting before processing unverified shader versions.

### Requirements
- Unity 2022.3.22f1 or compatible 2022.3 LTS release.
- NDMF (Non-Destructive Modular Framework) version 1.14.4 or higher.
- VRChat Avatar 3.0 SDK version 3.10.4 or higher.

### Known Limitations
- Finding 4: Deep material property revalidation is deferred during the Apply pass.
- Finding 9: Large texture dry-runs execute synchronous GPU readbacks that cause brief editor hitches.
