# Follow-up: the coverage-gate report with a missing renderer name after the alpha-mask sync

Privacy note: this record uses the play-mode console of the Census Lab project, read-only queries against the pinned Census Lab editor instance, and repository source as evidence. It names no avatar, scene, renderer, material, texture, or asset path. It describes renderers by role in prose. It writes no instance names, hashes, ports, or machine paths. Exact triangle counts appear as ranges. One console text quotation is edited to keep a renderer name out; the edit is marked.

Date: 2026-09-28.
Branch: `feat/alpha-mask-value-support`.
Base: `main` at `7d8a429`.
Observed branch commit count on 2026-09-28: 11 commits ahead of the base (`git rev-list --count`); the sync brief said 12. The merge base matches the stated base.
Investigation status on 2026-09-28: no production code changed in this session. This record is a follow-up to `docs/superpowers/investigations/2026-09-27-alpha-mask-value-refusal-investigation.md`.

## Question

On 2026-09-28 the human reported that, after syncing this branch into the Census Lab project and running a play-mode build, the NDMF console showed what looked like a reporting bug on the skinned mesh renderer that carries the transparent-family lower-body clothing slot from the 2026-09-27 record. This record answers three questions. Which report fired, and what exactly does it say? Why does it say that? Is the branch the cause?

## Console evidence

Before the queries, the reachable instances were enumerated read-only, the Census Lab instance was pinned by exact identity, and its `Application.dataPath` was confirmed to match the Census Lab project and not the dev repository. Every later call went to that pinned instance.

The NDMF error report store held exactly one error report from the last play-mode build, containing 12 errors. Seven were AMUSE reports. Observed AMUSE composition:

- One renderer-level slot-mapping report on an accessory renderer whose mesh part count differs from its material slot count.
- Three slot-analysis refusals: two name an unsupported shader family, one names an unsupported feature on the main texture property. These are the three other-feature refusals the 2026-09-27 record declared out of scope.
- One texture-capture refusal naming an in-memory texture with no source identity, on the same renderer as the unsupported-feature refusal.
- One slot-separation refusal at the minimum-opaque-coverage gate. Its title renders, with one name removed: "AMUSE left material slot 0 of renderer '[renderer name omitted]' unchanged.", where the renderer name position shows the literal text `<missing>`. The description reads: "The proven-opaque share of slot 0 of renderer '[renderer name omitted]' is below your minimum opaque coverage setting. Reason: OpaqueCoverageBelowMinimum. The slot keeps its original material."
- One avatar summary reporting that AMUSE analyzed a mid-twenties count of renderers and moved a five-digit count of triangles to opaque materials.

The two `_AlphaMaskValue` slot-analysis refusals from the 2026-09-27 record are gone. A console-wide text filter for the alpha mask property name matches zero reports. That is exactly the clearing the 2026-09-27 record predicted for this branch.

The coverage-gate report is the one the human flagged. Its retained NDMF object reference identifies the renderer as the skinned mesh renderer that carries the transparent-family lower-body clothing material, matching the role description in the 2026-09-27 record. The reference points at the destroyed build-copy renderer, so after play mode ends the console's object link is dead and the report text is the only identity carrier. That is why a missing renderer name in the text matters.

## Build identity and staleness

On 2026-09-28 the Census Lab embedded package copy matched the branch checkout byte for byte across the whole `Editor` tree and `package.json` (`diff -r --brief` reported no differences). The Lab's NDMF copy is version 1.14.4, the same version the dev repository pins.

The NDMF report store resets when a new build starts in a new frame, so the single stored report is the last build's complete output. Its contents confirm the branch ran: the two alpha-mask refusals are absent, and a coverage-gate separation report exists that this avatar cannot produce under main, because on main this slot refuses earlier. The stale-console explanation is ruled out.

## What the branch changed

`git diff main...HEAD --stat -- Packages/com.alrauna.amuse/Editor` lists 8 files, all in `Analysis/` and `Semantics/` (`AlphaSemanticsResolver.cs`, `TriangleAlphaClassifier.cs`, `AffineAlphaMap.cs`, `LilToonAlphaMaskSemantics.cs`, `LilToonCutoutMaterialSemantics.cs`, `LilToonTransparentMaterialSemantics.cs`, `MaterialSemantics.cs`). The diff touches no file in `Editor/Build/` and no report string. Any report difference is a downstream effect of classification changes, not a text change.

## Code trace

The flagged report's emission site is the minimum-opaque-coverage gate in the preparation pass, `Packages/com.alrauna.amuse/Editor/Build/AlphaSeparationPreparation.cs:263-275`. When a planned split's proven-opaque share fails the gate, the pass records the refusal and calls `AmuseReports.SlotSeparationRefusal(target.Renderer, submesh.SourceMaterialBindingIndex, AlphaSeparationSlotRefusal.OpaqueCoverageBelowMinimum)`. The fourth parameter, `rendererName`, is not passed. Its declared default is null, at `Packages/com.alrauna.amuse/Editor/Build/AmuseReports.cs:145-155`.

NDMF renders a null substitution as the literal text `<missing>`. See `Packages/nadena.dev.ndmf/Editor/ErrorReporting/InlineError.cs:30-33` in the `AddContext` argument walk. The report template takes the renderer name at position two, at `Packages/com.alrauna.amuse/Editor/Build/AmuseReportStrings.cs:456-464`. So the title and description both print `renderer '<missing>'`.

The gate itself is policy, not proof: `OpaqueCoverageBelow` at `Packages/com.alrauna.amuse/Editor/Build/AlphaSeparationPreparation.cs:536-545` refuses the split when `opaque * 100 < minimumPercent * total`. The minimum is the author's "Minimum Opaque Coverage (Per Material)" slider, `Packages/com.alrauna.amuse/Editor/AmuseAvatarOptimizerEditor.cs:186-190`. A refused split keeps the slot's original material, which the report states.

The renderer-name plumbing is inconsistent across the same report family. Within `AlphaSeparationPreparation.cs`, the renderer-wide refusal helper passes the name at `:567-573`, while three direct sites omit it: the coverage gate at `:270-274`, the marker-clip refusal at `:330-334`, and the conversion refusal at `:438-441`. The apply pass passes `renderer.gameObject.name` at `Packages/com.alrauna.amuse/Editor/Build/AlphaSeparationApply.cs:202-207` and `:275-280`, and omits it at `:164-168` only where the renderer may itself be null. The transient-unlock sites pass the name at `Packages/com.alrauna.amuse/Editor/Build/TransientUnlockSwapIn.cs:553-555` and `Packages/com.alrauna.amuse/Editor/Build/TransientUnlockWindowClose.cs:139-147`. Slot-analysis refusals pass the name at `Packages/com.alrauna.amuse/Editor/Build/AmusePlatformFinishPlugin.cs:681-687`, which is why the three surviving slot refusals render their renderer names.

## Why main never showed it

On main, the transparent-family clothing slot refuses inside the mask term at `Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonAlphaMaskSemantics.cs` (the fall-through refusal the branch deleted), resolves as admitted-semantics-unknown, and stops at the slot-analysis report of `AmusePlatformFinishPlugin.cs:681-687`. The 2026-09-27 record observed exactly that on 2026-09-27. A slot that refuses at analysis never reaches the preparation pass, so on main the coverage-gate report is unreachable for this avatar.

Under the branch, the slot classifies per triangle, the plan proves a share of its triangles opaque, and the split reaches the author's coverage gate. The gate refuses the split because the proven share is below the author's minimum. The report is substantively accurate: slot index, reason, and outcome are correct. Only the renderer name placeholder is wrong-looking.

## Classification

Pre-existing on main, newly surfaced by this branch's reachability. The missing `rendererName` argument at the three preparation sites and the one apply site exists unchanged on main; the branch touches no build-pass or report code. On main the defect is latent for this avatar because the flagged slot refuses earlier. The branch's classification unlock lets the slot reach preparation, which makes the pre-existing gap visible. This is not a branch code defect and not a Lab-environment artifact. The report's substantive content is accurate under both revisions.

Two companion observations, both out of scope. First, the renderer-level slot-mapping report on the accessory renderer: its emission check at `Packages/com.alrauna.amuse/Editor/Host/UnityRendererAlphaAnalysis.cs:575-577` compares material slot count to mesh part count at capture time, the branch cannot change either count, and the same report appears under main for this avatar. Second, the surviving three-feature refusal set differs in composition from the 2026-09-27 list (two unsupported-shader-family refusals and one unsupported-feature refusal today, against two deliberate mode refusals and one other on 2026-09-27); no conclusion is drawn from that shift.

## Minimal correction, proposed and not applied

Pass the renderer name at the three `AlphaSeparationPreparation.cs` sites that omit it, mirroring the existing `:567-573` pattern: `target.Renderer != null ? target.Renderer.gameObject.name : null`. The `AlphaSeparationApply.cs:164-168` site can keep null when the renderer is gone; a name does not exist there.

Required RED/GREEN evidence. RED: an EditMode test in `Tests/Editor/Build/` that prepares a split whose proven-opaque share is below the minimum, captures the NDMF report store, and asserts the coverage-gate report's formatted title and description contain the prepared renderer's game object name. On current code the assertion fails because the text carries `<missing>`; that failure is the observed RED. GREEN: the one-line argument additions make the same test pass. Falsifier: a prepared renderer whose name is empty must still render a usable report (the empty name renders as an empty substitution, never as `<missing>`), and a null renderer must keep rendering `<missing>` rather than throwing.

## Status

On 2026-09-28: diagnosis complete; root cause identified as the pre-existing missing `rendererName` argument at `AlphaSeparationPreparation.cs:270-274`, made visible by the branch's classification unlock; no production code changed; the correction proposal above awaits an implementation decision.
