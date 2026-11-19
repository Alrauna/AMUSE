# Foundation for merge-ready atlasing

Privacy note: This record contains public source observations only. It contains no private avatar data, vendor source excerpts, or machine identifiers.

Date: 2026-11-16.
Status on 2026-11-16: Read-only investigation for the 0.2.0 design. Recommendations do not authorize product changes.

## 1. Scope and evidence

Read-only subagents inspected build integration, settings, presets, reporting, and shader coordinate routing. The parent inspected selected source passages before accepting the architectural recommendations. A separate reviewer received only the standalone atlas design. That review did not inspect this record or product source.

This investigation did not run tests, builds, benchmarks, or Unity operations. Source structure establishes the findings below. It does not establish atlas quality, memory savings, or runtime compatibility.

The standalone design is `docs/superpowers/specs/2026-11-16-merge-ready-atlasing-design.md`. That document separates agreed behavior from implementation gates. This record explains the proposed integration with existing code.

Update on 2026-11-16: Later design decisions supersede earlier recommendations in this record. Unset atlas configuration takes the shipped defaults on upgrade. Explicit stored values remain unchanged. Answer 52 places alpha classification after atlas generation wherever feasible. Unsupported alpha separation keeps a valid atlas and its pre-separation render behavior. A rejected atlas conversion uses source-based alpha evidence or preserves the source unchanged. The standalone design owns these decisions.

## 2. Preserve the build lifecycle

Observation on 2026-11-16: `AmusePlatformFinishPlugin.Configure` registers the structural check, animator scope, preparation, application, and final unlock close. The close runs after the animator scope commits. Its recorded writes protect the final references from the animator rebind.

Evidence: `Packages/com.alrauna.amuse/Editor/Build/AmusePlatformFinishPlugin.cs:267-314`. `AlphaSeparationApply.ApplyFinalization` defines the mutation order. `TransientUnlockWindowClose` owns the final reference restoration.

Recommendation: Keep the capture, prepare, revalidate, and apply contract. Compose alpha and atlas outputs before the final write. Include final atlas references in the recorded writes that the close reasserts. Do not add a second material writer after the close.

Do not freeze the pass count as a design invariant. Change pass structure only when a demonstrated lifecycle requirement needs it. The load-bearing rule is consistent evidence and final references, not five named passes.

## 3. Replace the output assumption, not the whole pipeline

Observation on 2026-11-16: `PreparedAlphaSeparation` maps one source material to one opaque result across the avatar. It tracks owned material clones separately from source references.

Evidence: `Packages/com.alrauna.amuse/Editor/Build/AlphaSeparationRecords.cs:185-261`. Mesh clones are created only for surviving split candidates in `AlphaSeparationPreparation.cs:784-795`.

Recommendation: Keep the existing alpha plan as an input to composition. An atlas needs group-scoped placements, generated texture sets, and per-use material assignments. Do not force those relationships into `OpaqueBySource`. One material can need several outputs when visibility or coordinate uses differ.

Use one final renderer assignment as the mutation authority. Give each generated mesh, material, and texture an explicit owner. Revalidation and cleanup must cover dependencies shared across renderers. Do not create separate overlapping ownership lists that can destroy the same output twice.

Extract responsibilities when the atlas path reaches them. Suitable extractions include configuration resolution, final assignment composition, and report assembly. Do not rewrite the build pipeline merely because its files are large. Do not create a general global planner.

## 4. Capture only the evidence that atlas decisions need

Observation on 2026-11-16: `MaterialSemantics` exposes alpha only. `CapturedAnimationEvidence` describes admitted values and per-slot material states. It does not establish shared visibility across surface uses.

Evidence: `Packages/com.alrauna.amuse/Editor/Semantics/MaterialSemantics.cs:1102-1109`. `Packages/com.alrauna.amuse/Editor/Host/CapturedAnimationEvidence.cs`. `Packages/com.alrauna.amuse/Editor/Analysis/AdmittedMaterialStates.cs:261-275` explicitly describes a slot-local operation.

Recommendation: Add bounded atlas facts to the shader frontends and bounded visibility facts to capture. Do not describe the existing material-state closure as a visibility solver. Do not expand the alpha record into a universal shader graph before a real consumer needs one.

The existing affine mapping and triangle classifier can support a final-output opacity check. This is a reuse opportunity, not a proof of transparent appearance preservation. A texture can remain below one alpha value while its transparency changes visibly. Alpha classification also does not establish preservation of normals, colors, or masks.

## 5. Treat mesh remapping as new behavior

Observation on 2026-11-16: Geometry capture reads UV0 and additional sets one through three. Existing alpha separation clones the mesh and changes index assignments. It does not establish a complete recipe for vertex duplication or new coordinate channels.

Evidence: `Packages/com.alrauna.amuse/Editor/Host/UnityRendererGeometryExtractor.cs:102-134`. `Packages/com.alrauna.amuse/Editor/Build/AlphaSeparationPreparation.cs:784-795`.

Recommendation: Inventory every affected vertex attribute before a coordinate rewrite. Preserve attribute dimensions and unused components. A shader can use more than two components of a coordinate attribute. Vertex duplication must preserve skinning, blend-shape frames, normals, tangents, and other data.

A spare channel must come from verified shader routing and existing uses. An empty coordinate array alone is not evidence that the channel is free. Do not assume that a selector exposes every channel that Unity can store. Do not add shader-specific channel choices to the artist interface.

## 6. Resolve configuration once

Observation on 2026-11-16: Preview and build call the same scalar conversion helpers. They separately enumerate the component values that they map. The result fingerprint enumerates those values again.

Evidence: `Packages/com.alrauna.amuse/Editor/DryRun/AlphaSeparationDryRun.cs:370-406`. `Packages/com.alrauna.amuse/Editor/Build/AmusePlatformFinishPass.cs:433-471`. `Packages/com.alrauna.amuse/Editor/Build/PlayModeBuildStore.cs:308-333`.

Recommendation: Capture one immutable configuration snapshot and resolve its execution policy through one path. Preview and build consume that path. Fingerprints derive from the same captured values. Keep artist values separate from target-specific proof decisions when those values differ.

The serialized component remains the stored authority. A snapshot is not another editable configuration store. Do not add reflection, property registries, generated schemas, or per-feature services for this change. Preset parsing still needs an explicit supported schema.

## 7. Proposed UI cuts and retained choices

Observation on 2026-11-16: `_advancedSettingsRevealed` is serialized even though the build does not read it. Its comment states that Reset and Undo intentionally include it.

Evidence: `Packages/com.alrauna.amuse/Runtime/AmuseAvatarOptimizer.cs:223-231`.

Recommendation: Move this presentation state out of the runtime component. Record the change to Reset and Undo behavior. Do not use scene serialization merely to remember an open panel.

Observation on 2026-11-16: Cutout and transparent settings deliberately have different defaults. The README also documents a first-edit backfill behavior for a new component.

Evidence: `Packages/com.alrauna.amuse/Runtime/AmuseAvatarOptimizer.cs:138-158`. `README.md:90-91`.

Recommendation: Keep distinct policy values where the contracts differ. Do not add a synchronized-values marker or a new linking toggle merely to hide duplicated rows. Such a marker introduces another state to migrate. Future migration must not overwrite the first user edit.

Observation on 2026-11-16: `AmuseBuildStatusStore` intentionally stores the editor session's last build, not one result per avatar. The Play-mode record already uses per-avatar identity and a configuration fingerprint.

Evidence: `Packages/com.alrauna.amuse/Editor/Build/AmuseReports.cs:857-880`. `Packages/com.alrauna.amuse/Editor/Build/PlayModeBuildStore.cs:336-350`.

Recommendation: Label the global summary as a session summary. Do not present its atlas memory as the selected avatar's result. Use the existing per-avatar result route for avatar-specific output. Do not create another result cache.

No additional cut to the existing alpha controls is approved by this investigation. The atlas controls remain feature enablement, maximum size, density strength, and the proposed strict sampling choice. The design review can recommend a smaller surface.

## 8. Preset and component migration

Observation on 2026-11-16: `PresetParser` accepts schema version five only. Other versions return `UnknownSchemaVersion`. Adding atlas keys without migration would reject existing user preset files.

Evidence: `Packages/com.alrauna.amuse/Editor/Presets/PresetParser.cs:18-38,70-83`. The README describes manual preset migration.

Recommendation: Define a bounded import migration before adding the next schema. Keep unknown future versions and unknown keys closed. Preserve the meaning of stored alpha values. An older preset has no atlas choice, so filling new values is a product decision, not harmless parsing.

New-component defaults and migrated-component defaults require separate review. Default-on atlasing does not silently decide what an old scene or old preset author accepted. The user must approve the migration contract before implementation.

## 9. Recommendations not accepted as facts

The build investigation proposed using the existing classifier to re-prove packed outputs. That check cannot establish unchanged transparent appearance. The design must retain a separate sampling contract.

The build investigation also described an extra UV channel as a safe default. The accepted rule is narrower. Allocation requires a verified route and evidence about existing uses.

The settings investigation proposed linking cutout and transparent values with a new marker. This record rejects that proposal. Different defaults and migration states make the marker more complex than the duplicated presentation it replaces.

The settings investigation proposed filling old presets with shipped defaults. This record does not accept that migration policy without user approval. It can activate new transformations on existing inputs.

These corrections keep the source observations useful without treating subagent recommendations as verified behavior.

## 10. Vendor coordinate routing

Observation on 2026-11-16: The installed lilToon source fixes main color, alpha-mask, and first normal-map sampling to UV0-derived coordinates. Selected layers, secondary normals, and emission uses can choose mesh channels zero through three. Some selector values instead mean view-dependent coordinates.

Evidence: `Packages/jp.lilxyzw.liltoon/Shader/Includes/lil_common_frag.hlsl:257-283,458-475,569-591`. Layer and secondary-normal selectors appear in `Packages/jp.lilxyzw.liltoon/Shader/lts.shader:57-62,88-93,130-133`. The inspected installed row is lilToon 2.4.1. This does not establish every version in the product range.

Observation on 2026-11-16: The bundled Poiyomi 9.3 shader exposes a main-texture selector for mesh channels zero through three. Other values select procedural spaces. The normal map has a separate selector.

Evidence: `Packages/com.poiyomi.toon/_PoiyomiShaders/Shaders/9.3/Toon/Poiyomi Toon.shader:58-69,9202-9203`. Its vertex path directly copies those channels in several passes.

Recommendation: Add a channel only within the channels that the verified texture route can address. Do not interpret a selector value of four as mesh channel four. Do not assume that adding mesh channels four through seven helps the inspected texture routes.

For lilToon main-texture atlasing, a spare atlas channel alone is insufficient. A supported recipe must remap UV0 and preserve its other uses. Selectable consumers can retain original coordinates on a spare channel. Fixed consumers need a compatible conversion or preserve the candidate unchanged.

The investigation found a separate version trap. Native Poiyomi 10 source strips unused coordinate channels under compiled conditions. The cited code is `Packages/com.poiyomi.toon/_PoiyomiShaders/Shaders/10.0/Toon/Poiyomi Toon.shader:13248-13258`. Searches of the bundled 9.3 file found no corresponding channel-liveness names or guards.

This does not prove every generated locked variant preserves every channel. It does disprove using the native 10 snippet as evidence of that stripping in bundled 9.3. The product supports the bundled 9.3 source, not native 10 by implication. Locked outputs need a verified reconstruction recipe for their actual source.

Do not introduce a vendor unlock and relock call during the build. That recommendation from the vendor investigation conflicts with the existing no-vendor-invocation contract. Reuse verified reconstruction where it supports the conversion. Otherwise preserve the dependent use.

The coordinate investigation did not establish complete selector-animation admission, fallback-shader behavior, every keyword path, or interactions with upstream optimizers. Source inspection alone does not prove that generated coordinates work in a running avatar. Those remain focused verification requirements.
