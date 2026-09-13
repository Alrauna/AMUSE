# Optimizer merging and alpha evidence isolation

Privacy note: This record contains sanitized observations. Private objects appear only by role. Private geometry counts use ranges. This record contains no private assets, names, paths, or identifiers.

Date: 2026-09-12.
Status on 2026-09-12: A public synthetic probe reproduces an AMUSE capture defect. Permanent capture regressions landed on the prerequisite branch `test/capture-predicate-isolation` (worktree from `main` at `d05171d`) and fail for the confirmed reason. The approved integration plan executed through the disposable-project DAO sequence: both DAO cases passed. The exact private build remains untraced.

Branch inspected: `chore/harden-integration-testing` at `578b5eb`.
Local integration base: `main` at `d05171d`.

## Result

AMUSE can apply a cutout capture threshold to a transparent material after renderer merging. A cutout threshold decides whether a fragment survives clipping. It does not establish full opacity for blended transparency.

The public probe supplies alpha bytes `0`, `128`, and `255`. Transparent-only capture preserves the partial-alpha triangle. A combined cutout request changes that triangle to `ProvenOpaque`. Zero-alpha texels remain non-opaque. The probe uses inert alpha policy bounds.

A second probe case keeps separate material requests but shares one texture between transparent and cutout materials. It produces the same false-positive classification.

These are confirmed AMUSE defects. They do not require an AAO algorithm defect or missing texture evidence. The live scene supplies relevant preconditions. A full private build trace must still connect the mechanism to the reported output.

The user approved a layered test design on 2026-09-12. The design combines capture regressions, renderer tests, and real optimizer pipeline tests. No production fix was implemented during this investigation.

## Evidence levels

- User report: AAO automatic skinned-mesh merging triggers visible over-conversion. DAO does not trigger the observed problem.
- Source inspection: Installed optimizer code establishes transformation order and capture behavior.
- Live observation: Read-only probes establish source-scene configuration and broad asset traits.
- Executed experiment: Public synthetic capture and classification reproduce a false positive.
- Unproven connection: The exact post-AAO private material batch was not captured during this investigation.

The user report is accepted. The investigation does not require another run to confirm that the visible problem exists.

## Installed sources

The inspected installations report these versions:

| Tool | Version |
| --- | --- |
| Unity | 2022.3.22f1 |
| AAO | 1.9.17 |
| DAO | 4.5.4 |
| NDMF | 1.14.4 |
| lilToon | 2.3.4 |
| Modular Avatar in the Census Lab project | 1.18.2 |

The following public source files match between the public project and the Census Lab project:

- AAO `Editor/OptimizerPlugin.cs`.
- AAO `Editor/Processors/TraceAndOptimize/AutoMergeSkinnedMesh.cs`.
- AAO `Editor/Processors/TraceAndOptimize/OptimizeTexture.cs`.
- DAO `Editor/AvatarBuildHook.cs`.
- DAO `Editor/d4rkAvatarOptimizer.cs`.

The inspected AMUSE capture, transparent semantics, build plugin, and runtime component files also match between the two projects.

Older source audits cite other optimizer versions. They do not override these installed versions.

## What differs between AAO and DAO

### Build order

AAO registers its main sequence in NDMF `Optimizing`. AMUSE captures and applies alpha separation in `PlatformFinish`. Thus, AMUSE sees AAO output.

DAO registers an SDK preprocess callback rather than an NDMF plugin. With Modular Avatar, its callback runs after the NDMF optimization callback. Without Modular Avatar, the two callbacks declare equal order. Equal order does not establish a reliable relative sequence.

For the installed Census Lab configuration, source establishes this SDK callback order:

```text
AAO mesh and texture changes
  -> AMUSE capture, classification, and apply
  -> DAO optimization
```

This is a source-derived order, not an observed callback trace for the reported play-mode build. DAO can also skip play-mode optimization through its own configuration.

Consequently, a successful DAO result does not prove that AMUSE supports DAO-merged input. DAO can merge only after AMUSE finishes. Direct `AvatarProcessor.ProcessAvatar` tests do not execute the separate DAO callback.

DAO also deletes and recreates its generated output folder at optimization start. Avatar cloning does not isolate that project-level operation. Its integration tests require a disposable public project. See `GetTrashBinLocation` and `ClearTrashBin` in the installed DAO source, lines 557–596.

Sources:

- [AAO plugin sequence](../../../Packages/com.anatawa12.avatar-optimizer/Editor/OptimizerPlugin.cs), lines 59–148.
- [AMUSE pass sequence](../../../Packages/com.alrauna.amuse/Editor/Build/AmusePlatformFinishPlugin.cs), lines 160–183.
- [NDMF SDK callbacks](../../../Packages/nadena.dev.ndmf/Editor/VRChat/BuildFrameworkPreprocessHook.cs), lines 26–99.
- [DAO callback and play-mode gates](../../../Packages/d4rkpl4y3r.d4rkavataroptimizer/Editor/AvatarBuildHook.cs), lines 9–78.

### Mesh and material handling

AAO automatic merging requires actual bones, a root bone, compatible renderer state, and suitable dependency information. It groups compatible renderers and creates a merged renderer. Its merge copies vertex data and remaps submeshes, material-slot bindings, blend shapes, and component references.

AAO can merge repeated material slots. It then removes unused material data and runs texture optimization. Texture optimization can replace textures and remap UV coordinates. These later operations are separate from vertex merging.

DAO combines compatible mesh data, bones, indices, and material slots. Its merge preserves UV0 x and y while it can encode extra data in UV0 z. Its later material stage can also create texture arrays or generated shaders when the configuration permits those operations.

The observed DAO configuration enables mesh merging but disables static shader-property writes, different-property material merging, and same-dimension texture merging. Do not generalize this result to every DAO preset.

Sources:

- [AAO automatic merge admission](../../../Packages/com.anatawa12.avatar-optimizer/Editor/Processors/TraceAndOptimize/AutoMergeSkinnedMesh.cs), lines 38–164.
- [AAO merge data and mappings](../../../Packages/com.anatawa12.avatar-optimizer/Editor/Processors/SkinnedMeshes/MergeSkinnedMeshProcessor.cs), lines 271–408.
- [AAO slot merging](../../../Packages/com.anatawa12.avatar-optimizer/Editor/Processors/TraceAndOptimize/MergeMaterialSlots.cs), lines 19–105.
- [AAO texture replacement and UV mapping](../../../Packages/com.anatawa12.avatar-optimizer/Editor/Processors/TraceAndOptimize/OptimizeTexture.cs), lines 581–622 and 843–1079.
- [AAO unused texture removal](../../../Packages/com.anatawa12.avatar-optimizer/Editor/Processors/TraceAndOptimize/RemoveUnusedMaterialProperties.cs), lines 12–52.
- [DAO operation sequence](../../../Packages/d4rkpl4y3r.d4rkavataroptimizer/Editor/d4rkAvatarOptimizer.cs), lines 146–233.
- [DAO mesh combination](../../../Packages/d4rkpl4y3r.d4rkavataroptimizer/Editor/d4rkAvatarOptimizer.cs), lines 5468–6047.

## Live source-scene characterization

The Census Lab editor instance was pinned after an exact `Application.dataPath` match. Every live Census probe was read-only. The scene was outside play mode and already dirty. The investigation did not save it or start a build.

Observed traits on 2026-09-12:

- Tens of skinned renderers, with bones and blend shapes.
- Multiple material slots and shared material references.
- Inactive geometry and attached animation controllers.
- Both cutout and transparent lilToon materials on the avatar.
- No inspected source renderer that contains both of those material modes.
- Positive cutoff values on the inspected regular transparent materials. None of those inspected materials uses zero cutoff.
- Separate main and mask textures, with a saturated multiply-mask configuration present.
- Compressed, unreadable streaming textures among the inspected transparent materials.
- No active mipmap limit among those inspected main textures.
- Visibility and blend-shape curves in attached root and descriptor controllers.
- No material-swap or numeric material curves found in those attached controllers.

The last observation does not exclude controllers that other tools add later. It does not establish the complete post-build animation state.

AAO enables automatic merging, texture optimization, material-slot shuffling, and MMD compatibility. No explicit AAO Merge Skinned Mesh component was found in the active scene. The tests must exercise Trace and Optimize rather than substitute explicit merging.

The observed AMUSE configuration uses inert alpha clamps. It retains the mip cap of 4, minimum texture size of 128, and material coverage threshold of 25 percent.

## Confirmed defect chain

### Request union changes the sampling predicate

`UnityAnimationEvidenceCapture` combines capture requests across the admitted materials of a renderer. It passes one combined request to the closed material capture.

`MaterialEvidenceRequest.Combine` keeps a named cutoff when another request has no cutoff declaration. `UnityMaterialSemantics.TryCaptureClosedAlphaMaterials` then applies that combined request to every material.

A lilToon cutout request declares `_Cutoff` for `_MainTex`. A transparent request deliberately declares no cutoff. Their combined request declares `_Cutoff`, including when capture reads the transparent material.

The generated and streaming routes use that threshold to mark partial alpha as opaque evidence. The transparent interpreter later consumes the evidence as plain alpha. The threshold meaning no longer matches the consumer.

Sources:

- [Renderer-wide request union](../../../Packages/com.alrauna.amuse/Editor/Host/UnityAnimationEvidenceCapture.cs), lines 389–479.
- [Cutoff merge rule](../../../Packages/com.alrauna.amuse/Editor/Host/UnityMaterialEvidenceCapture.cs), lines 126–200.
- [One request applied to each material](../../../Packages/com.alrauna.amuse/Editor/Semantics/UnityMaterialSemantics.cs), lines 165–206.
- [Transparent request](../../../Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonTransparentMaterialSemantics.cs), lines 169–192.
- [Cutout request](../../../Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonCutoutMaterialSemantics.cs), lines 137–161.

### Shared-texture capture loses the predicate distinction

`UnityMaterialEvidenceCapture.Capture` groups assignments by `TextureSourceId`. It unions their evidence flags and selects the minimum cutoff. It then gives the resulting captured texture to every assignment of that source.

Thus, per-material requests alone do not fix shared-texture contamination. The capture reuse key must preserve every fact that changes a captured predicate.

Source: [Shared capture grouping](../../../Packages/com.alrauna.amuse/Editor/Host/UnityMaterialEvidenceCapture.cs), lines 803–858.

### Field lookup can erase a later correction

`GatherAlphaFields` keeps the first chain for each source and channel. The build supplies this renderer-wide provider to every admitted material resolution.

If capture starts producing distinct chains, this lookup must not collapse them again. A fix that changes only request selection or only capture caching is incomplete.

Sources:

- [Field aggregation](../../../Packages/com.alrauna.amuse/Editor/Host/UnityRendererAlphaAnalysis.cs), lines 590–662.
- [Build provider](../../../Packages/com.alrauna.amuse/Editor/Build/AmusePlatformFinishPlugin.cs), lines 900–933.
- [Per-state resolution with the shared provider](../../../Packages/com.alrauna.amuse/Editor/Analysis/AdmittedMaterialStates.cs), lines 198–260.

### Capture routes differ

The generated route reads the shader output alpha channel value when a cutoff applies. The streaming route compares decoded alpha against the cutoff. The ordinary non-streaming GPU route instead retains exact-one capture under that cutoff.

The first synthetic probe accidentally used the ordinary route. It did not reproduce the false positive. After explicit container persistence and a positive producer check, the generated route reproduced it.

This control is evidence that a texture name alone does not establish which capture route a test exercises.

Sources:

- [Generated predicate interpretation](../../../Packages/com.alrauna.amuse/Editor/Host/UnityGeneratedTextureEvidence.cs), lines 193–215.
- [Streaming predicate interpretation](../../../Packages/com.alrauna.amuse/Editor/Host/UnityStreamingTextureEvidence.cs), lines 172–213.
- [Ordinary GPU route](../../../Packages/com.alrauna.amuse/Editor/Host/UnityAlphaFieldEvidence.cs), lines 375–387.

## Executed public experiment

The user authorized a bounded disposable probe under `Assets/AmuseMixedCaptureProbe`. The probe refused an existing folder. It created no production scripts and deleted only its own folder.

Fixture:

- A generated 128 by 128 RGBA32 texture inside an NDMF `SubAssetContainer`.
- Three vertical alpha regions with bytes `0`, `128`, and `255`.
- Point filtering, clamp wrapping, and one mip level.
- A transparent material with cutoff `0.01`.
- A cutout material with cutoff `0.25`.
- Real family requests, real material capture, and real triangle classification.
- A nondegenerate triangle wholly inside the partial-alpha region.

This capture probe does not invoke source attestation, an optimizer, or final mesh application. The later integration tests must cover those boundaries.

Observed results on 2026-09-12:

| Probe case | Captured flags for zero, partial, and full alpha | Partial triangle |
| --- | --- | --- |
| Transparent request alone | `0, 0, 255` | `MustRemainTransparent` |
| Transparent material with combined request | `0, 255, 255` | `ProvenOpaque` |
| Separate requests with a shared texture | `0, 255, 255` | `ProvenOpaque` |

The combined request declared `_Cutoff` for `_MainTex`. Capture read `0.01` from the transparent material. The generated-producer check returned true. The contract assertion returned false.

After the experiment, the probe folder was absent and the public active scene was clean. No probe source file remains.

## Existing harness assessment

The existing `AaoMergedConsumptionTests` filter completed four tests. Its status was failed. The tool reported one named failure but returned no final result object. This record does not infer a final pass count.

The merged-consumption test reported zero analyzed renderers and two `AdmittedMaterialSemanticsUnknown` refusals. Its diagnostic output showed two separate fixture renderers. The run did not reach the reported over-conversion failure.

The harness has these specific gaps:

1. Its meshes have no bones, weights, or root bone. AAO automatic merge admission requires bones and a root bone.
2. It never asserts that merging occurred.
3. Both materials use transparent semantics. The confirmed mixed-mode failure is absent.
4. The texture has only binary alpha. It omits an explicit partial-alpha region.
5. It checks final UV x values against the original image layout. AAO can replace the texture and remap those UVs.
6. It permits an unsuccessful NDMF build instead of requiring a valid fixture and successful execution.
7. Its geometry accounting permits lost triangles. It requires only at least three triangles from a six-triangle input.
8. Its material capture probes mostly assert availability. They do not compare the captured predicate with an independent alpha expectation.
9. It silently removes required coverage through `Assert.Ignore` when dependencies are absent.
10. Its zero-cutoff justification conflicts with the inspected live source state. Its claim that cutoff `0.5` exceeds the transparent proof bound also conflicts with the source bound of `1`.

Source: [Existing harness](../../../Packages/com.alrauna.amuse/Tests/Editor/Build/AaoMergedConsumptionTests.cs), especially lines 84–131, 292–399, and 403–520.

The console also contained NDMF native-detour errors and an incompatible audio-plugin error. They are environment findings. This investigation did not prove that either caused the harness refusal.

## What not to delete blindly

The branch-only production addition is the small AAO component-information registration and its assembly configuration. It does not explain the capture defect. Its registry test proves registration, not component survival or correct merging.

Assess that registration through observable build behavior before removing it. Test package loading with AAO both present and absent. A conditional source block does not itself prove that an unconditional assembly reference is optional.

The missing-evidence refusal work is already on `main`. Its safety invariant remains valid. This investigation supplies no reason to remove it as a substitute for correcting predicate isolation.

The earlier diagnosis also compared a texture-wide opaque fraction with a moved-triangle fraction. Those fractions measure different domains. They do not alone prove that classification ignored the texture.

The earlier missing-sample explanation is not established by this reproduction. The new failure uses present evidence with the wrong meaning. Do not treat the earlier record as a completed root-cause proof for this issue.

## Next work

Use the [test design](../specs/2026-09-12-optimizer-merge-integration-design.md) and the [implementation plan](../plans/2026-09-12-optimizer-merge-integration-plan.md).

The generic capture defect is a prerequisite already present on `main`. Keep its fix separate from branch-specific optimizer test repair. Do not select a broad shader representation or an optimizer-specific refusal to hide the defect.

The final causal gate requires a valid AAO build that actually merges mixed material modes. The final user-facing gate requires the original private visual scenario after the fix.

## Workspace note

On 2026-09-12, the public player configuration changed during the editor session. The changes disable automatic graphics API selection for iOS and legacy blend-shape weight clamping. The investigation did not restore or include those changes in its document work.

## Implementation session addendum, 2026-09-12

Status on 2026-09-12: The approved test plan executed through its test-only boundary. Production code is unchanged. The findings below are observed results, dated this session.

1. Capture regressions: four permanent cases on `test/capture-predicate-isolation` fail on the confirmed partial-alpha false positive, in both request orders, with a passing transparent-only control inside the same fixture class. The full fixture class ran: 33 cases, 29 passed, and the 4 intended failures are the regressions.
2. Build-target gate: the GPU alpha capture admits only `StandaloneWindows64`. A fresh project defaulting to another target refuses every capture with `UnavailableCapture` before the format gate. Integration runs must pass `-buildTarget StandaloneWindows64`.
3. Consumer-branch harness: the merged-consumption tests now build valid skinned fixtures, carry a real committed clip through the descriptor Base layer, and verify the built avatar through a root-space triangle-identity oracle with exact accounting. Fifteen cases pass, including real AAO merging, mixed-slot predicate isolation in four orders, a shared-texture cross-renderer case, a higher-cutoff alternative, mask darkening, streaming-texture refusal, component survival, the serialized disable control, and an animated material swap.
4. AAO merge requirements: the vendor categorization includes renderer bounds, so fixtures must share one explicit bound box before Avatar Optimizer merges two renderers.
5. AAO texture packing: on this vendor version the packed atlas is not classifiable by the shipped capture. The pinned contract is conservative refusal by the named all-unknown bucket with zero movement. Whether the mechanism is the loose-texture attestation refusal, a format refusal, or a wrapper object is open for the repair plan.
6. Vendor defect candidate: Avatar Optimizer 1.9.17 empties a mesh past 65,535 vertices during its own passes. An AMUSE-only build keeps the same mesh intact and analyzes it. The exact boundary is not bisected.
7. Vendor behavior: Avatar Optimizer's garbage collector removes a build-time-inactive renderer even when a committed layer curve toggles it. The observed-private "inactive geometry" may relate. Needs vendor-side characterization.
8. Upstream note: NDMF 1.14.4's `VRChatPlatformAnimatorBindings.CommitControllers` dereferences null descriptor layer arrays on commit while tolerating them during virtualization. A fresh descriptor carries null arrays. Fixtures materialize both arrays.
9. Pre-existing failures: the consumer branch carries 13 failing tests in `AlphaSeparationPreparationTests` and `AmusePlatformFinishPluginTests`. A stashed-baseline run reproduced the identical list, so they predate this session's work.
10. Editor stability: the dev editor instance crashed once during the session after repeated native `mprotect` exceptions. A crash dump moved out of the repository. The instance was relaunched and re-verified before further work.

The repair plan must cover request selection, shared capture, and field lookup together, and must decide the AAO 32-bit and packed-atlas interactions against the conservative-refusal contract.
