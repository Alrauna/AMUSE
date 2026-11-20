# Atlasing Milestone 2: Poiyomi Main Color Path Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

Privacy note: This record contains public product planning only. It contains no private asset observations, machine identifiers, or vendor source excerpts.

Date: 2026-11-19.
Status on 2026-11-19: Executable plan for Milestone 2 of `docs/superpowers/plans/2026-11-19-0-2-0-minimal-atlasing-plan.md`. Milestone 1 merged into main on 2026-11-19. An adversarial review on 2026-11-19 found 2 critical and 9 important defects. This revision fixes them. The owner authorized subagent-driven execution on 2026-11-19.
Status on 2026-11-20: Implemented on branch `atlasing-m2-poiyomi-main-color`. One correction to this revision: a review ruling during Task 1 added `EmptySubmesh` after `NonTriangleTopology`, so the refusal enum grew by one member over the list this plan first named. The Task 1 member list below carries the member.

**Goal:** The atlas pass moves the `_MainTex` regions of compatible Poiyomi 9.3 uses into shared AMUSE pages, rewrites UV0, and writes one shared material per page. Every other use keeps its bindings and gets one named refusal.

**Architecture:** `AtlasPass` continues at its analysis point. It captures one immutable record per use (`Host/`). Pure code groups the uses and places their regions on pages (`Analysis/`). The pass checks the three budget numbers, acquires the sources through the existing import copy route, builds and checks each page, builds one mesh per renderer, checks renderer identity, and writes once (`Build/`). Alpha separation behavior does not change. Alpha apply only adds each object it creates to the AMUSE-owned object list (E2).

**Tech Stack:** C#, Unity 2022.3.22f1, NDMF, NUnit EditMode tests through the Unity Test Runner, the installed Poiyomi 9.3 shader.

**Spec:** `docs/superpowers/specs/2026-11-19-minimal-atlasing-design.md`, sections 2, 4 to 10, and 14. Route table facts: `docs/superpowers/investigations/2026-11-19-atlasing-route-tables.md`, section 4. Page facts: `docs/superpowers/investigations/2026-11-19-atlasing-page-fixtures.md`.

## Global Constraints

- Package root for every path below: `Packages/com.alrauna.amuse/`.
- Rows: the four admitted Poiyomi rows of `PoiyomiSourceAttestation`, release and bundled, main and Two Pass. Name, GUID, and digest must match one row. A locked material refuses.
- Member: `_MainTex` on UV0. Neutral: `_ToonRamp`, `_SkinLUT`, `_ClothDFG`, `_DistortionFlowTexture`, `_DistortionFlowTexture1`.
- Allowed local keywords: `BSSBLOOMFOGTYPE_HEIGHT`, `VIGNETTE_MASKED`, `_LIGHTINGMODE_FLAT`, `_RIMSTYLE_POIYOMI`, `_RIM2STYLE_POIYOMI`, `_STOCHASTICMODE_DELIOT_HEITZ`, `_STYLIZEDREFLECTIONMODE_UNITYCHAN`. Any other enabled local keyword that the shader declares refuses the use.
- Required values: `_MainTexUV` 0, `_MainPixelMode` 0, `_MainTexStochastic` 0, `_AlphaSharpenedA2C` 0 or `_AlphaToCoverage` 0, `_RenderingAOBlockerEnabled` 0, `_MainTex_ST` exactly (1, 1, 0, 0), `_MainTexPan` exactly zero.
- Supported page formats: RGBA32, RGB24, DXT1 (BC1), DXT5 (BC3), BC7, each sRGB or linear. The page format is the source graphics format.
- Border at level 0: `max(r, b) * 2^(P-1)`. `r` is 1 for anisotropy level 0 and 16 otherwise. `b` is 4 for compressed formats and 1 otherwise. Alignment grid: `b * 2^(P-1)`.
- Maximum page size is the component value, from 1 to 8192. Pages are tight.
- Remapped UV0 is Float32 in the output mesh. Never subdivide triangles.
- Every page has mip maps and `m_StreamingMipmaps` true.
- Never mutate a source mesh, material, texture, import setting, clip, prefab, or scene.
- Programming and invariant failures throw. Expected outcomes are named `AtlasUseRefusal` values.
- Every report string and comment uses simple English: short active sentences, no semicolons, no contractions.
- Before any Unity MCP call, confirm that `Application.dataPath` belongs to this project. Use only the dev editor instance.
- The owner authorized subagent-driven execution on 2026-11-19. Each task ends with one commit through the eval `commit` namespace, then a task review. Never push. Never stage user-owned files.

## Plan Decisions

These rules are not literal in the design. The plan derives them. The owner can change them at review.

- **E1. Animation facts.** The atlas pass runs after the animator scope closed. `state.StructuralGraph` was enumerated before the scope opened, so it does not hold the curves that alpha apply added. The atlas pass enumerates the graph again with `CommittedControllerGraph.Enumerate(root, state.AnimatorBindings)`, as `TransientUnlockWindowClose` does. A null binding capability or a graph refusal refuses every use with `AnimationGraphUnavailable`. The pass observes each clip with `LiveAnimationObservation.ObserveClip(clip, bindings.IsSpecialMotion(clip), isFromAdditiveLayer)` and keeps the bindings whose path equals the renderer path. A renderer with an `m_Mesh` binding or an `m_Materials.Array.size` binding refuses every use with `AnimatedRendererShape`. A slot with an object binding `m_Materials.Array.data[i]` refuses with `AnimatedMaterialSlot`. A float binding whose property, without the `material.` prefix and without a component suffix such as `.x` or `.r`, starts with a route or condition property name refuses the use with `AnimatedRouteProperty`. A prefix match refuses more than an exact match. That is safe. Spec section 4 condition 3 names `AnimatedPropertyAdmission`. That gate admits an animated property whose every value equals the default. Milestone 2 refuses every animated route property instead. This refuses more than the spec allows. It is a coverage gap, not a false positive. `AnimatedPropertyAdmission` takes alpha material evidence, and the atlas pass must not run the alpha material capture.
- **E2. AMUSE-owned objects.** Spec section 2 says the atlas pass reads the AMUSE-owned object list from state. Task 6 adds `AmusePlatformFinishState.AmuseOwnedObjects`, a set of `UnityEngine.Object`. Alpha apply adds each mesh clone and each canonical material it creates, at the point where it creates them. The atlas pass adds its pages, page materials, and meshes. Cleanup destroys only members of this set.
- **E3. Group key by serialization.** The non-texture material facts come from one walk of the material `SerializedObject`. The walk skips `m_Name` and the `_MainTex` texture reference. Floats enter as their exact bits. Object references enter as their instance ID. So every serialized fact joins the key, including keywords, tags, render queue, disabled passes, instancing, and global illumination flags.
- **E4. Placement.** Shelf packing. Placements are sorted by longest side, then area, then capture order, all descending except capture order. A shelf fills left to right up to the maximum page size. A new shelf starts below. A placement that does not fit the current page starts a new page. Page width is the widest shelf and page height is the sum of shelf heights, each rounded up to the alignment grid.
- **E5. Slots beyond the submesh count.** Unity draws the last submesh again for each extra slot. The uses of the last submesh and of every extra slot refuse with `SlotSharesSubmesh`.
- **E6. Budget limits before Milestone 5.** The production limits delegate returns `AtlasBudgetLimits.Unlimited`, whose `IsUnlimited` is true. `Admit` skips every comparison for it, so no product can overflow. Milestone 5 sets the measured values. Main stays unreleasable until then. The ledger still throws when it passes the prediction.
- **E7. Peak prediction formula.** The pass builds one page at a time. It acquires the sources of one page, builds the page, checks it, and releases the source images before the next page. Per page, the prediction is the sum of these terms:
  - the RGBA32 compose buffer with its full mip chain
  - the page texture bytes
  - for every placement on the page, its raw protected levels and its level 0 `Color32` array
  - the largest import clone texture bytes among the placements, because each clone is released before the next acquisition
  - the check decode: the page level 0 `Color32` array plus the largest decode of all protected levels of one placement source
  The group prediction is the largest page prediction.
- **E8. Refusals reach state only.** Milestone 2 records each use refusal in state. Report lines belong to Milestone 4. Each new `AtlasUseRefusal` member still gets its title, description, and hint strings in `AmuseReportStrings`, because `AmuseReportStringsTests` requires them for every member.
- **E9. Registry.** The pass registers each replaced mesh with `ObjectRegistry.RegisterReplacedObject(old, new)` before it destroys any owned object. Each page material is registered against the material of its first use in capture order. Task 6 test `RegistryResolvesTheSourceMeshAfterCleanup` proves that the build finishes and that the registry resolves the source mesh to the atlas mesh after the owned clone is destroyed. If NDMF throws on the destroyed key, the pass keeps the replaced owned mesh instead of destroying it, and the ledger records the change.
- **E11. Layering.** The route table lives in `Semantics/`. `Analysis/` code returns its own `AtlasPlacementRefusal` values. `Build/` maps them to `AtlasUseRefusal`. The budget admission lives in `Build/`, because it reads the limits delegate.
- **E12. Acquisition failure.** Acquisition runs after planning and budget admission. When one source of a group fails to acquire, the pass withdraws the whole group. Every use of that group refuses with `SourceAcquisitionFailed`. The pass does not plan the group again.
- **E10. Upload check.** The owner builds and uploads the public 8192 fixture avatar. AMUSE tooling cannot log in to VRChat.

## Review Focus

1. Two renderers share one source mesh. Each renderer gets its own output mesh. The source mesh stays unchanged. Task 6 owns `TwoRenderersOnOneSourceMeshGetTwoOutputMeshes`.
2. Duplication makes more than 65535 vertices on a 16-bit index mesh. The output uses 32-bit indices. Task 5 owns `DuplicationPastSixteenBitsPromotesTheIndexFormat`.
3. One material on two renderers with different UV rectangles. Both uses share one placement whose region is the union. Task 2 owns `UsesOfOneSourceShareOnePlacementWithTheUnionRegion`.
4. A source without mip maps, or with fewer levels than the protected count, cannot supply protected levels. The use refuses with `SourceMipChainTooShort`. Task 1 owns `SourceWithFewerLevelsThanProtectedRefuses`.
5. A second build in the same editor session reuses no page, mesh, or material from the first. Task 6 owns `ASecondBuildCreatesFreshOutputs`.

---

### Task 1: Use capture and the Poiyomi route table

**Files:**
- Create: `Editor/Semantics/AtlasRouteTable.cs` (the Poiyomi table as data)
- Create: `Editor/Host/AtlasUseCapture.cs` (one record per renderer slot, or a refusal)
- Create: `Editor/Host/CapturedAtlasUse.cs` (the immutable record)
- Create: `Editor/Semantics/AtlasSamplerFacts.cs`
- Modify: `Editor/Semantics/UnityTextureEvidence.cs` (one new capture method)
- Modify: `Editor/Build/AtlasUseRefusal.cs` (use-scope members)
- Modify: `Editor/Build/AmuseReportStrings.cs` (title, description, and hint for each new member)
- Test: `Tests/Editor/Host/AtlasUseCaptureTests.cs`, `Tests/Editor/Semantics/UnityTextureEvidenceTests.cs`

**Interfaces:**
- Produces:
  - `internal readonly struct AtlasSamplerFacts { FilterMode Filter; TextureWrapMode WrapU; TextureWrapMode WrapV; int AnisoLevel; float MipBias; }` with value equality.
  - `UnityTextureEvidence.TryGetAtlasSampler(Texture texture, out AtlasSamplerFacts facts)`. Only a `Texture2D` with dimension `Tex2D` passes. Point, Bilinear, and Trilinear pass. Clamp and Repeat pass per axis. Mirror and MirrorOnce fail.
  - `internal sealed class AtlasRouteTable` with `string MemberProperty`, `IReadOnlyCollection<string> NeutralProperties`, `IReadOnlyCollection<string> AllowedKeywords`, `IReadOnlyCollection<string> ConditionProperties`, and `bool HoldsRequiredValues(Material material)`. `static AtlasRouteTable PoiyomiMainColor`.
  - `internal sealed class CapturedAtlasUse`: `int RendererIndex`, `int SlotIndex`, `int SubmeshIndex`, `string ShaderRowName`, `int SourceTextureInstanceId`, `GraphicsFormat SourceFormat`, `int SourceWidth`, `int SourceHeight`, `int SourceMipCount`, `AtlasSamplerFacts Sampler`, `AtlasUvRect UvRect`, `string MaterialKey`, `IReadOnlyList<string> AnimatedPropertyNames`, `int CaptureOrder`. No live Unity reference.
  - `internal readonly struct AtlasUvRect { double MinU, MinV, MaxU, MaxV; }`.
  - `internal static class AtlasUseCapture` with `IReadOnlyList<AtlasUseOutcome> Capture(GameObject avatarRoot, CommittedControllerGraphResult graph, IPlatformAnimatorBindings bindings, int protectedMipLevelCount)`. The graph is the one that E1 enumerates after the scope closed. `AtlasUseOutcome` holds `Renderer`, `SlotIndex`, and either a `CapturedAtlasUse` or a refusal. The live renderer stays outside the captured record.
  - New `AtlasUseRefusal` members after `ProtectedMipLevelCountOutOfRange`: `AnimationGraphUnavailable`, `UnsupportedRenderer`, `MissingMesh`, `MissingMaterial`, `AnimatedRendererShape`, `AnimatedMaterialSlot`, `UnattestedShader`, `LockedShader`, `NoRouteTable`, `KeywordNotAllowed`, `RequiredValueNotHeld`, `UnlistedTexture`, `PropertyBlockOnRouteProperty`, `AnimatedRouteProperty`, `MissingMainTexture`, `UnsupportedTextureDimension`, `SourceNotAnAsset`, `UnsupportedFormat`, `UnsupportedWrap`, `NonIdentityTransform`, `MissingRouteChannel`, `UvOutsideUnitSquare`, `SourceNotAligned`, `SourceMipChainTooShort`, `ClampEdgeOnBc7`, `PlacementTooLarge`, `GroupBudget`, `AvatarBudget`, `PeakBudget`, `SourceAcquisitionFailed`, `SlotSharesSubmesh`, `NonTriangleTopology`, `EmptySubmesh`.

- [ ] **Step 1: Write the failing tests**

Each test builds a real Poiyomi material on the GUID-loaded installed shader, as `NewRealPoiyomiOpaqueMaterial` in `AlphaSeparationPreparationTests` does, on a quad renderer under a fixture root. The default fixture is a fresh material with a 64 by 64 imported PNG on `_MainTex`.
- `FreshPoiyomiMaterialWithItsDefaultTexturesIsAdmitted`: the record exists. `SourceWidth` is 64. `UvRect` is the quad rectangle.
- One falsifier per required value, as `[TestCase]` rows: `_MainTexUV` 1, `_MainPixelMode` 1, `_MainTexStochastic` 1, `_AlphaSharpenedA2C` 1 with `_AlphaToCoverage` 1, `_RenderingAOBlockerEnabled` 1. Each gives `RequiredValueNotHeld`.
- `NonAllowedKeywordRefuses`: enable `POI_UDIMDISCARD`. `KeywordNotAllowed`.
- `AllowedKeywordSubsetIsAdmitted`: disable all seven allowed keywords. The record exists.
- `EachAllowedKeywordIsAdmitted`: one `[TestCase]` per allowed keyword. Enable it through `EnableKeyword`. The record exists. An empty allowed list fails these rows.
- `UnlistedAssignedTextureRefuses`: assign a texture to `_MatcapMask` or another texture property outside both lists. `UnlistedTexture`.
- `NonIdentityScaleRefuses` and `NonZeroPanRefuses`: `NonIdentityTransform`.
- `MainTextureOnUv1Refuses`: `_MainTexUV` 1 already covers it. Assert the same refusal on a mesh that has UV1.
- `UvOutsideTheUnitSquareRefuses`: one vertex at U 1.0001. `UvOutsideUnitSquare`. A vertex at exactly 1.0 is admitted.
- `NaNUvRefuses`: one vertex with U NaN. `UvOutsideUnitSquare`. The check is written as `!(u >= 0 && u <= 1)`.
- `MissingUv0Refuses`: `MissingRouteChannel`.
- `NullSlotMaterialRefuses`: `MissingMaterial`. `NullMainTextureRefuses`: `MissingMainTexture`. `RenderTextureSourceRefuses`: `UnsupportedTextureDimension`. `NullMeshRefuses`: `MissingMesh`. `ParticleRendererRefuses`: `UnsupportedRenderer`.
- `SlotPropertyBlockOnTheMainTextureRefusesOnlyThatSlot` and `RendererWidePropertyBlockRefusesEverySlot`: `PropertyBlockOnRouteProperty`.
- `AnimatedMainTextureTransformRefuses`: a clip in a committed graph layer animates `material._MainTex_ST.z`. `AnimatedRouteProperty`.
- `AnimatedConditionPropertyRefuses`: a clip animates `material._MainPixelMode`. `AnimatedRouteProperty`.
- `SwappedSlotRefuses`: a clip swaps slot 0. `AnimatedMaterialSlot`.
- `SwapCurveOnAnAppendedSlotRefuses`: a graph enumerated after alpha apply holds a swap curve on `m_Materials.Array.data[2]` of a renderer with two original slots. Slot 2 refuses with `AnimatedMaterialSlot`. A capture from the graph enumerated before apply would admit it, so this row fails on a stale graph.
- `AnimatedMeshRefusesEveryUse` and `AnimatedSlotCountRefusesEveryUse`: `m_Mesh` and `m_Materials.Array.size` bindings. `AnimatedRendererShape`.
- `LockedMaterialRefuses`: a locked stand-in material whose shader name starts with `Hidden/Locked/`, as the existing locked-material tests build it. `LockedShader`, not `UnattestedShader`.
- `StandardShaderRefuses`: `UnattestedShader`.
- `MirrorWrapRefuses`: `UnsupportedWrap`.
- `CrunchedSourceRefuses` and `R8SourceRefuses`: `UnsupportedFormat`.
- `GeneratedTextureWithoutAnAssetRefuses`: `SourceNotAnAsset`.
- `SourceWithFewerLevelsThanProtectedRefuses`: a source without mip maps and protected count 2. `SourceMipChainTooShort`.
- `CompressedSourceNotOnTheBlockGridRefuses`: dropped on 2026-11-20. An import probe in the dev editor on 2026-11-20 showed that Unity does not produce this source. A 68 by 64 PNG with mip maps and DXT1 imports as uncompressed RGB24 with no resize, and as 64 by 64 DXT1 with the nearest power of two resize. The `SourceNotAligned` check stays in the capture as a fail-closed invariant without a direct test.
- `ExtraSlotsRefuseTheLastSubmeshUses`: a two-submesh mesh with three slots. Slots 1 and 2 refuse with `SlotSharesSubmesh`. Slot 0 is admitted.
- `TransparentSlotAndItsOpaqueCloneHaveDifferentMaterialKeys`: two materials that differ only in render queue and `RenderType` tag.

- [ ] **Step 2: Run the tests and verify that they fail**

Run in the dev editor instance: EditMode, assembly `Alrauna.Amuse.Tests.Editor`, filter `AtlasUseCaptureTests` and `UnityTextureEvidenceTests`.
Expected: compile errors for the missing types.

- [ ] **Step 3: Implement the capture**

Check order per slot, first refusal wins: renderer kind, mesh presence, and topology, renderer shape animation, slot beyond submesh count, slot material presence, swap binding, lock through `LockedMaterialIdentity.Classify`, shader row (classify name, then GUID, then digest through `PoiyomiSourceAttestation.TryReadNormalizedSourceHashCached` and `TryMatchAdmittedSource`), keywords (enabled `LocalKeyword` values that the shader declares), required values, assigned textures, property blocks (`HasTexture`, `HasVector`, `HasFloat` for every route and condition property, renderer-wide first), animated properties (E1), the member texture (presence, dimension, asset, format, wrap, mip count, grid), transform, UV0 presence and the unit square. The material key follows E3.

- [ ] **Step 4: Run the same filters and verify that they pass**

Expected: every test passes. Record the observed counts.

- [ ] **Step 5: Review stop**

Run `git diff --check`. Commit the task. Every later review stop means the same.

### Task 2: Group key and placement

**Files:**
- Create: `Editor/Analysis/AtlasGroupKey.cs`
- Create: `Editor/Analysis/AtlasPlacementPlanner.cs`
- Create: `Editor/Analysis/AtlasPagePlan.cs` (the plan records)
- Test: `Tests/Editor/Analysis/AtlasPlacementPlannerTests.cs`

**Interfaces:**
- Consumes: `CapturedAtlasUse`.
- Produces:
  - `internal sealed class AtlasGroupKey : IEquatable<AtlasGroupKey>` built from shader row, route, source format, sampler, material key, and the sorted animated property names.
  - `internal sealed class AtlasPlacement`: source instance ID, sampler, source rectangle with border in source texels (`SourceX`, `SourceY`, `Width`, `Height`, may cross the source edge), page index, page origin `PageX`, `PageY`, and the use indices it serves.
  - `internal sealed class AtlasPagePlanEntry`: `Width`, `Height`, `GraphicsFormat Format`, `int MipCount`, placements.
  - `internal enum AtlasPlacementRefusal { None, PlacementTooLarge, ClampEdgeOnBc7 }` in `Analysis/` (E11).
  - `internal sealed class AtlasGroupPlan`: key, pages, admitted use indices, and refused use indices with their `AtlasPlacementRefusal`.
  - `AtlasPlacementPlanner.Plan(IReadOnlyList<CapturedAtlasUse> uses, int maximumPageSize, int protectedMipLevelCount)` returns `IReadOnlyList<AtlasGroupPlan>` in capture order of the first use of each group. Pure. No Unity object.
  - Page mip count: the full chain, `floor(log2(max(Width, Height))) + 1`.

- [ ] **Step 1: Write the failing tests**
- `TwoBorderedThousandPlacementsShareOnePageOfAtMost2112`: two whole 1024 RGBA32 sources, `r` 16, P 1. One page. Long side at most 2112.
- `TwoWhole2048SourcesShareOne4160By2080Page`: DXT1, `r` 16, maximum 8192. Page 4160 by 2080.
- `PlacementOriginsAndSizesSitOnTheGrid`: DXT1 with P 2. Every origin and size is a multiple of 8.
- `PlacementThatDoesNotFitAnEmptyPageRefuses`: a whole 4096 source with maximum 4096. `PlacementTooLarge`.
- `FullPageStartsANewPageInTheSameGroup`: five whole 2048 sources, `r` 16, with maximum 4160. The first page holds four placements in two shelves. The fifth placement starts a second page.
- `UsesOfOneSourceShareOnePlacementWithTheUnionRegion`.
- `TwoSourceFormatsLandInDifferentGroups`.
- `DifferentMaterialKeysLandInDifferentGroups`.
- `ClampRegionTouchingTheEdgeOnBc7Refuses` and `ClampRegionInsideTheImageOnBc7IsPlanned`.
- `RepeatRegionTouchingTheEdgeIsPlannedAcrossTheEdge`: the source rectangle starts below 0.
- `PlacementIsDeterministic`: the same input twice gives equal plans. A shuffled input with the same capture order values gives the same plan.

- [ ] **Step 2: Run the tests and verify that they fail**

Filter `AtlasPlacementPlannerTests`. Expected: compile errors.

- [ ] **Step 3: Implement the planner**

Region per placement key: union of the `UvRect` values, then `floor(min * size)` and `ceil(max * size)` in texels, then the border, then outward alignment to the grid. A Clamp axis whose bordered region crosses the source edge on BC7 refuses. Placement follows E4.

- [ ] **Step 4: Run the same filter and verify that it passes**

- [ ] **Step 5: Review stop**

### Task 3: Budget

**Files:**
- Create: `Editor/Analysis/AtlasBudget.cs` (the byte counts and the prediction)
- Create: `Editor/Build/AtlasBudgetLimits.cs` (the limits record and the production delegate)
- Create: `Editor/Build/AtlasBudgetAdmission.cs`
- Create: `Editor/Build/AtlasByteLedger.cs`
- Test: `Tests/Editor/Analysis/AtlasBudgetTests.cs`, `Tests/Editor/Build/AtlasBudgetAdmissionTests.cs`, `Tests/Editor/Build/AtlasByteLedgerTests.cs`

**Interfaces:**
- Consumes: `AtlasGroupPlan`, `CapturedAtlasUse`.
- Produces:
  - `internal readonly struct AtlasBudgetLimits` with `bool IsUnlimited`, `long FactorNumerator`, `long FactorDenominator`, `long AvatarRetainedBytes`, `long PeakTemporaryBytes`, a constructor that takes the four numbers, and `static AtlasBudgetLimits Unlimited`.
  - `internal static Func<AtlasBudgetLimits> AtlasBudgetLimits.Provider`, production default `() => Unlimited` per E6. Tests that replace it restore it in `TearDown`.
  - `AtlasBudget.TextureBytes(int width, int height, GraphicsFormat format, int mipCount)` through `GraphicsFormatUtility.ComputeMipChainSize`.
  - `AtlasBudget.GroupRetainedBytes(AtlasGroupPlan plan)`, `AtlasBudget.SourceBytes(AtlasGroupPlan plan, IReadOnlyList<CapturedAtlasUse> uses)` with each source counted once, `AtlasBudget.PredictPeakBytes(AtlasGroupPlan plan, IReadOnlyList<CapturedAtlasUse> uses, int protectedMipLevelCount)` per E7.
  - `AtlasBudgetAdmission.Admit(IReadOnlyList<AtlasGroupPlan> plans, IReadOnlyList<CapturedAtlasUse> uses, int protectedMipLevelCount, AtlasBudgetLimits limits)` returns one `AtlasUseRefusal` per group in plan order: `None`, `GroupBudget`, `AvatarBudget`, or `PeakBudget`. Integer arithmetic only. The group check compares `retained * denominator` with `source * numerator` in checked 64-bit arithmetic. An overflow is an invariant failure. `Unlimited` skips every comparison.
  - `internal sealed class AtlasByteLedger` with a constructor `AtlasByteLedger(long prediction)`, `Add(long)`, `Release(long)`, and `long Current`. `Add` throws `InvalidOperationException` when `Current` passes the prediction. Unity 2022.3 compiles C# 9, so do not use a primary constructor.

- [ ] **Step 1: Write the failing tests**
- `GroupExactlyAtEachLimitIsAdmitted` and `OneByteMoreRefusesIt`, as rows for the three numbers.
- `GroupThatTheAvatarLimitRefusesDoesNotEnterTheSum`: a later smaller group is admitted.
- `SourceThatAnotherUseReferencesStaysCounted`: two groups with one shared source count it in both.
- `UnlimitedAdmitsAnEightKilotexelGroup`: the production provider admits a group of two 4096 BC7 sources on one 8192 by 4096 page without an overflow.
- `InjectedAllocationAbovePredictionThrows`.
- `TextureBytesMatchesTheHandComputedLength`: 4160 by 2080 DXT1 with 13 levels is 5769096 bytes. Each level rounds up to whole 4 by 4 blocks of 8 bytes. The test also compares the value with `GetRawTextureData().Length` of a real texture of that shape.

- [ ] **Step 2: Run the tests and verify that they fail**

- [ ] **Step 3: Implement the budget, the admission, and the ledger**

`Admit` walks the groups in plan order. It refuses a group that fails the group check first, then the peak check, then the avatar check. Only an admitted group adds its retained bytes to the avatar sum.

- [ ] **Step 4: Run the tests and verify that they pass**

- [ ] **Step 5: Review stop**

### Task 4: Acquisition, page build, and output check

**Files:**
- Modify: `Editor/Host/UnityStreamingTextureEvidence.cs` (extract the clone mechanism, add one acquisition method)
- Create: `Editor/Host/AtlasSourceImage.cs`
- Create: `Editor/Build/AtlasPageBuilder.cs`
- Create: `Editor/Build/AtlasPageCheck.cs`
- Test: `Tests/Editor/Host/UnityStreamingTextureEvidenceTests.cs`, `Tests/Editor/Build/AtlasPageBuilderTests.cs`

**Interfaces:**
- Produces:
  - `UnityStreamingTextureEvidence.TryAcquireAtlasSource(Texture2D source, int protectedMipLevelCount, out AtlasSourceImage image, out TextureCaptureRefusalReason refusal)`. It uses the same clone steps as `TryCapture`. It bypasses the cache and the direct-disk arm. It copies only the protected raw levels. A clone whose graphics format differs from the source is an invariant failure and throws.
  - `AtlasPageBuilder.Build(AtlasPagePlanEntry page, AtlasSamplerFacts sampler, IReadOnlyDictionary<int, AtlasSourceImage> sources, int protectedMipLevelCount, AtlasByteLedger ledger)` returns a `Texture2D`. Steps:
    1. Create the compose texture in RGBA32 with the sRGB or linear flag of the page format, so a linear source stays linear.
    2. Compose level 0 from `Level0` with the wrap rule. Generate mips with `Apply(true)`.
    3. Encode to the page format with `EditorUtility.CompressTexture` at Normal quality. The page `graphicsFormat` must equal the plan format, else throw.
    4. Overwrite each protected level with source blocks. Clamp edge blocks for BC1 and BC3 repeat the edge indices and keep the endpoints. Apply without mip regeneration.
    5. Copy filter, wrap U, wrap V, anisotropy, and mip bias from `sampler`.
    6. Set `m_StreamingMipmaps` true through `SerializedObject`.
  - `AtlasPageCheck.Verify(Texture2D page, AtlasPagePlanEntry plan, IReadOnlyDictionary<int, AtlasSourceImage> sources, int protectedMipLevelCount)`. It decodes every protected level of the page and the source with `GetPixels32` and compares every texel of each placement and its border with the source texel that the wrap mode maps it to, alpha included. A difference throws `InvalidOperationException`. The source decode is released per placement.
  - After `Verify` passes, the pass calls `page.Apply(false, true)`, so the shipped page is not readable. `Verify` needs a readable page, so this order is fixed.

- [ ] **Step 1: Write the failing tests**
- `AcquisitionReturnsTheImportedSizeAndProtectedLevels`: a streaming, non-readable 256 source. The image is 256 by 256 with P levels. The source importer settings are unchanged afterward.
- `AcquisitionLeavesNoTemporaryAsset`: no asset remains under the scratch folder, and `AssetDatabase.IsValidFolder` is false for it.
- Characterization, not RED: the existing `UnityStreamingTextureEvidenceTests` stay green.
- `PageHasMipMapsAndStreamingOn`, as rows for the five formats, each sRGB and linear. The page `graphicsFormat` equals the source format.
- `ProtectedLevelsCopyExactly`: BC7 and DXT1 pages pass the check.
- `PlacementOneBlockOffTheGridFailsTheCheck`: a hand-made plan with an origin 4 texels off. The check throws.
- `Bc1ClampEdgeBorderIsExact`.
- `RepeatBorderCopiesTheOppositeEdge`: RGBA32 source, Repeat, region at the edge.
- `PageCopiesTheSourceSampler`: a Point, Clamp U, Repeat V, anisotropy 4, mip bias 0.5 source. The page has the same five facts.
- `ShippedPageIsNotReadable`: after the pass step, `isReadable` is false.
- `TwoSourcePageStaysInsideItsPrediction`: a real ledger with the E7 prediction for a two-source page. The build and the check do not throw.

- [ ] **Step 2: Run the tests and verify that they fail**

- [ ] **Step 3: Extract the clone helper and implement acquisition, build, and check**

Extract the clone steps of `TryCapture` into one private helper: the sweep, the copy, the two importer flags, the size checks, and the cleanup in `finally`. The cleanup deletes through `AssetDatabase` and refreshes, never from disk alone. `TryCapture` calls the helper with its current behavior. Do not change any `TryCapture` outcome. Acquisition calls the same helper and skips the cache and the direct-disk arm.

- [ ] **Step 4: Run the tests and verify that they pass**

Also run the full `UnityStreamingTextureEvidenceTests` class and the `Semantics` folder tests.

- [ ] **Step 5: Review stop**

### Task 5: Mesh output

**Files:**
- Create: `Editor/Build/AtlasMeshBuilder.cs`
- Test: `Tests/Editor/Build/AtlasMeshBuilderTests.cs`

**Interfaces:**
- Produces: `AtlasMeshBuilder.Build(Mesh current, IReadOnlyList<AtlasSubmeshRemap> remaps)` returns a new `Mesh`. `AtlasSubmeshRemap` holds the submesh index and either no remap or one affine map per axis: `newU = (u * SourceWidth - SourceX + PageX) / PageWidth`, the same for V, in double precision, stored as Float32.
- Rules: a vertex that triangles with different remaps use is duplicated once per extra remap. Duplication copies every vertex attribute byte. The UV0 attribute becomes Float32 with its dimension kept. Only the first two components of remapped vertices change. Index format becomes 32-bit when the vertex count passes 65535. Output submeshes use base vertex 0 with absolute indices. Submesh topology, bounds, bind poses, and the mesh name with an `AMUSE atlas` suffix carry over.
- Method: `Mesh.MeshData` carries the vertex streams and indices only. Build the streams and indices with `Mesh.AcquireReadOnlyMeshData` and `Mesh.AllocateWritableMeshData`. After `ApplyAndDisposeWritableMeshData`, set bone weights with `GetBonesPerVertex`, `GetAllBoneWeights`, and `SetBoneWeights`, expanded for duplicated vertices. Copy `bindposes`. Copy each blend shape frame with `GetBlendShapeFrameVertices` and `AddBlendShapeFrame`, expanded for duplicated vertices.

- [ ] **Step 1: Write the failing tests**
- `EveryAttributeBoneWeightAndBlendShapeSurvives`: a skinned fixture mesh with positions, normals, tangents, colors, UV0 to UV3, bone weights, and two blend shapes with two frames each. Every unchanged attribute equals the source per vertex. UV0 is Float32.
- `DuplicatedVertexCarriesItsBoneWeightsAndBlendShapeRows`: the copy of a shared vertex has the same bone weights and the same delta in every blend shape frame as the original.
- `Float16RouteChannelBecomesFloat32`.
- `RemappedUvErrorStaysInsideTheBound`: for a 4096 placement on an 8192 page, every remapped UV differs from the exact double value by at most `S * 2^-25`, where `S` is the page side.
- `SharedVertexBetweenAtlasedAndKeptSlotsIsDuplicated`: the kept slot keeps its UV values.
- `SharedVertexBetweenTwoPlacementsIsDuplicated`.
- `DuplicationPastSixteenBitsPromotesTheIndexFormat`.
- `SourceMeshIsUnchanged`: the source vertex data bytes are equal before and after.

- [ ] **Step 2: Run the tests and verify that they fail**

- [ ] **Step 3: Implement the builder by the method above**

- [ ] **Step 4: Run the tests and verify that they pass**

- [ ] **Step 5: Review stop**

### Task 6: The atlas path in the pass

**Files:**
- Modify: `Editor/Build/AtlasPass.cs` (continue after `ReachedAtlasAnalysis`)
- Create: `Editor/Build/AtlasWrite.cs` (identity check, write, registry, cleanup)
- Modify: `Editor/Build/AmusePlatformFinishPlugin.cs` (state: `AtlasUseRefusals`, `AtlasPageCount`, `AtlasAdmittedUseCount`, `AmuseOwnedObjects`)
- Modify: `Editor/Build/AlphaSeparationApply.cs` (add each created mesh clone and canonical material to `AmuseOwnedObjects`, nothing else)
- Test: `Tests/Editor/Build/AtlasPassTests.cs`, new `Tests/Editor/Build/AtlasEndToEndTests.cs`

**Interfaces:**
- Consumes: every earlier task.
- Produces on `AmusePlatformFinishState`: `IReadOnlyList<(Renderer renderer, int slot, AtlasUseRefusal cause)> AtlasUseRefusals`, `int AtlasPageCount`, `int AtlasAdmittedUseCount`, `HashSet<UnityEngine.Object> AmuseOwnedObjects`, and `internal void RecordAtlasUseRefusal(Renderer, int, AtlasUseRefusal)`, which throws on `None`.
- `internal static Action<Texture2D, AtlasPagePlanEntry, IReadOnlyDictionary<int, AtlasSourceImage>, int> AtlasPass.PageCheck`, production default `AtlasPageCheck.Verify`. It is the test seam for the injected failure. Tests restore it in `TearDown`.
- `AtlasWrite.Apply(AmusePlatformFinishState state, IReadOnlyList<AtlasRendererOutput> outputs)`. `AtlasRendererOutput` holds the renderer, the mesh and the slot array captured before any page is built, the slot count, the new mesh, and the new slot array. Before the write it compares the captured values with the live renderer. A difference throws. The write sets `sharedMesh` and then `sharedMaterials`. Then it registers per E9. Then it destroys each replaced object that `AmuseOwnedObjects` holds and no renderer references.

- [ ] **Step 1: Write the failing tests**

In `AtlasEndToEndTests`, through `AvatarProcessor.ProcessAvatar` on a test platform that runs the production pass sequence, as `AtlasPassTests.AtlasTestPlatform` does. Each test runs inside `OverrideTemporaryDirectoryScope`. Cleanup deletes generated assets through `AssetDatabase` and refreshes.
- `TwoCompatibleMaterialsShareOnePageWithCorrectRendering`: two quads, two Poiyomi materials with two 64 by 64 sources that differ in color pattern. Assert: one page, one shared page material on both slots, sources unchanged, slot count unchanged, UV0 Float32. Render each quad with an orthographic camera to a 128 by 128 target before and after. The largest per-channel difference is at most 1 of 255.
- `PlacementIsTheSameOnTwoBuilds`.
- `ASecondBuildCreatesFreshOutputs`.
- `TwoRenderersOnOneSourceMeshGetTwoOutputMeshes`.
- `TransparentSlotAndItsOpaqueCloneLandInDifferentGroups`: alpha separation splits one submesh. The atlas pass makes two groups.
- `RefusedAtlasUseKeepsItsAlphaSeparationOutput`: the same avatar with a non-allowed keyword on the source. The slot keeps the opaque clone that alpha separation wrote.
- `InjectedInvariantFailureReachesTheBuild`: `AtlasPass.PageCheck` throws. The build reports an exception.
- `RegistryResolvesTheSourceMeshAfterCleanup`: per E9.
- `AlphaCloneThatThePageReplacesIsDestroyed`: the opaque material and the mesh clone that alpha apply made, and that no renderer references after the write, are destroyed.
- `AcquisitionFailureWithdrawsTheGroup`: per E12.
- Characterization, not RED: `BothSwitchesOffStillChangesNothing`.

In `AtlasPassTests`: `AtlasOnlyBuildOnWindowsReachesAtlasAnalysis` now also asserts `AtlasPageCount` for its empty avatar is 0.

- [ ] **Step 2: Run the tests and verify that they fail**

- [ ] **Step 3: Implement the path**

Order: enumerate the graph (E1), capture uses, record refusals, plan groups, budget admission, then per admitted group and per page: acquire the page sources one at a time inside a `try` with the ledger, build the page, check it, make it non-readable, create one material clone per page, release the source images in `finally`. Then build one mesh per touched renderer, identity check, write, registry (E9), and cleanup. A use refusal never removes alpha separation output, because the pass only rewrites slots it admitted.

- [ ] **Step 4: Run the filters, then both full assemblies**

Expected: 0 failures. A filtered run with 0 tests is a failure.

- [ ] **Step 5: Review stop**

### Task 7: GPU reach fixture, upload fixture, and records

**Files:**
- Create: `Tests/Editor/Build/AtlasGpuReachTests.cs`
- Create: `Tests/Editor/Build/AtlasUploadFixture.cs` (a test-assembly menu item that creates the upload fixture under an untracked scratch folder)
- Modify: `docs/superpowers/plans/2026-11-19-0-2-0-minimal-atlasing-plan.md`
- Modify: `docs/superpowers/investigations/2026-11-18-atlasing-handoff.md`
- Modify: `Packages/com.alrauna.amuse/CHANGELOG.md`

- [ ] **Step 1: Write the reach fixture**

`GpuReachStaysInsideTheBorder`, rows for `r` 1 and 16: sample a black texture with one white texel through the method of the page fixtures record. Fail when any nonzero output lies `r + 0.5` texels or more from the white texel. Pin anisotropic filtering to ForceEnable with `Tests/Editor/Shared/AnisotropicFilteringPin.cs`, so the `r` 16 row really uses anisotropy.

- [ ] **Step 2: Run it and record the observed reach**

- [ ] **Step 3: Write the upload fixture menu item**

It creates a public avatar with a Poiyomi material pair. Each material has a 4096 source and a UV rectangle 4064 texels wide. With the 16-texel border on each side, each placement is 4096 wide, so the page is 8192 by 4096. It writes nothing under tracked folders.

- [ ] **Step 4: Owner step: build and upload the fixture avatar**

The owner builds and uploads the fixture through the VRChat SDK. Record two facts with the date: whether the page streams in the built bundle, and whether VRChat accepts the page. Remove the scratch folder afterward.

- [ ] **Step 5: Update the records and the changelog**

Mark Milestone 2 with the date and the observed counts. Add a CHANGELOG line under `[Unreleased]`. Sweep the changed documents for private identifiers.

- [ ] **Step 6: Review stop**

## Coverage

| Design requirement | Task |
| --- | --- |
| Section 4 conditions 3 to 9 | 1 |
| Section 5 Poiyomi table | 1 |
| Section 6 group key | 1 (material key), 2 |
| Section 7 regions, borders, alignment, pages | 2, 4 |
| Section 8 acquisition, format, page build, streaming, output check | 4 |
| Section 8 Float32 UV and rounding | 5 |
| Section 9 mesh, identity check, write, cleanup | 5, 6 |
| Section 10 budget | 3, 6 |
| Section 14 falsifiers for this row | 1 to 7 |
| Milestone 2 upload checks | 7 |
