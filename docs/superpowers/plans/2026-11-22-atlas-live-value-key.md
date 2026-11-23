# Atlas material key counts only live values: Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

Privacy note: this plan describes a private Census Lab avatar by role only. It names no avatar, renderer, material, or texture.

**Goal:** Two material slots share an atlas group when every value that can change their render is equal. Values that the shader never reads stop splitting groups.

**Architecture:** The material key walker in `AtlasUseCapture` gets two tiers. The generic tier applies to every shader row and drops only fields that Unity semantics make inert. The reviewed tier applies to lilToon 2.4.1 rows. A pinned live value model names the properties that each static feature switch turns dead, and the uniforms that the attested source declares. Two oracles test the model against the installed lilToon: a compile reflection check and a render perturbation check.

**Tech Stack:** C# for Unity 2022.3, NUnit EditMode tests, installed lilToon 2.4.1.

**Spec:** this plan. The user request of 2026-11-22: "only values the shader actually reads count".

## Global Constraints

- Soundness: the page material is `new Material(firstMaterial)` (`Editor/Build/AtlasPass.cs`, `NewPageMaterial`). So every member of a group must render the same as the first member with the first member's values. A value may leave the key only when it cannot change any pass of any member.
- A feature switch value always stays in the key. Only the values that the switch turns dead may leave.
- A dead family stays live when its switch is animated, or when a property block on the renderer names the switch.
- Unknown means live. A shader row with no reviewed model uses only the generic tier.
- Never refuse more than today. This change only removes key tokens and only removes `UnlistedTexture` refusals.
- Every English string uses simple English: short sentences, no semicolons, no contractions.
- Docs and tests name no private asset. Lab results are ranges.

## Review Focus

1. A value that a dead family lists but a live feature also reads. Expected: it stays live. The render oracle in Task 3 turns every other allowed feature on, so such a read changes pixels.
2. A stale saved value whose name the shader declares as a uniform outside its Properties block. Expected: it stays in the key. Task 1 pins the Unity fact, and Task 2 pins the declared uniform set.
3. VRChat shader fallback. When a viewer blocks shaders, VRChat copies named properties to a fallback material. Expected: these names always count. Task 2 covers it.
4. A switch that an animation clip turns on at runtime. Expected: the family stays live, and the slot is not refused. Task 2 covers it.
5. A material variant or a material with an explicit render queue. Expected: an explicit queue equal to the shader queue shares the key. A different queue does not. Task 1 covers it.

## Findings (2026-11-22, read-only)

- In the Census Lab editor instance, two lilToon 2.4.1 opaque materials that the user expected to merge differ in these ways.
  - Live differences: the alpha mask, the matcap blend mask, the outline width mask, the rim color texture scale, and `_LightDirectionOverride`. These keep the pair apart after this plan too.
  - Dead or inert differences: `_SubpassCutoff`, `_GlitterVRParallaxStrength`, `_Reflectance` at float noise level, tiling on unassigned `_Main2ndTex` and `_DissolveMask`, `m_CustomRenderQueue` 2000 against -1 with the shader queue 2000, and stale `_DetailAlbedoMap`, `_DetailMask`, and `_DetailNormalMap` entries from an earlier shader.
  - The key walks saved properties by array index. One stale entry shifts every later index, so the key differs in most tokens.
- In the dev editor instance, a probe material with a saved value `_Foo = 1` moved to a shader that declares `float _Foo;` but has no `_Foo` in its Properties block. An early probe read red 1. Task 1 repeated the probe on 2026-11-22 with a control and found that the stale value does not reach the uniform. The early reading was most likely the magenta error shader. The plan keeps the conservative rule: a stale entry is dead only when the shader declares no uniform of that name. This rule is sound whether or not Unity binds a stale value.
- `ShaderData.Pass.CompileVariant` in Unity 2022.3 returns `VariantCompileInfo` with `ConstantBuffers` (`Name`, `Fields`) and `TextureBindings`. Task 2 uses it as an oracle, not at build time.
- lilToon 2.4.1 gates its features with uniform switches such as `if(_UseRim)` (`Shader/Includes/lil_common_frag.hlsl:1645`). It declares uniforms in `lil_common_input.hlsl`, `lil_common_input_base.hlsl`, and `lil_common_input_opt.hlsl`. It declares about 510 Properties on the outline opaque row.
- `_SubpassCutoff` is read only when `LIL_RENDER == 2` (`lil_common_frag_alpha.hlsl:100-114`). So it is dead on the opaque rows.
- `_Reflectance` is read inside `if(_UseReflection)` (`lil_common_frag.hlsl:1425-1446`) and by the gem pass, which no admitted row uses.
- The VRChat fallback page (creators.vrchat.com/avatars/shader-fallback-system, read 2026-11-22) lists the copied properties: `_MainTex`, `_MetallicGlossMap`, `_SpecGlossMap`, `_BumpMap`, `_ParallaxMap`, `_OcclusionMap`, `_EmissionMap`, `_DetailMask`, `_DetailAlbedoMap`, `_DetailNormalMap`, `_Color`, `_EmissionColor`, `_SpecColor`, `_Cutoff`, `_Glossiness`, `_GlossMapScale`, `_SpecularHighlights`, `_GlossyReflections`, `_SmoothnessTextureChannel`, `_Metallic`, `_BumpScale`, `_Parallax`, `_OcclusionStrength`, `_DetailNormalMapScale`, `_UVSec`, `_Mode`, `_SrcBlend`, `_DstBlend`, `_ZWrite`. It also copies `_Ramp` for Toon fallbacks and `_MatCap` for MatCap fallbacks. The `toonstandard` and `toonstandardoutline` tags copy every same-named property.

## File Structure

- Modify `Packages/com.alrauna.amuse/Editor/Host/AtlasUseCapture.cs`: the key walker, `ResolveRouteTextures`, and `CaptureSlot`.
- Create `Packages/com.alrauna.amuse/Editor/Semantics/AtlasLiveValueModel.cs`: the model type and the dead set computation.
- Create `Packages/com.alrauna.amuse/Editor/Semantics/AtlasDeadValueFamily.cs`: one switch and the values it turns dead.
- Create `Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonLiveValues.cs`: the reviewed lilToon 2.4.1 data.
- Create `Packages/com.alrauna.amuse/Editor/Semantics/VRChatFallbackProperties.cs`: the pinned fallback names.
- Modify `Packages/com.alrauna.amuse/Editor/Semantics/AtlasRouteTable.cs`: a `LiveValues` member.
- Tests: `Tests/Editor/Host/AtlasUseCaptureTests.cs`, new `Tests/Editor/Semantics/LilToonLiveValuesTests.cs`, and new `Tests/Editor/Semantics/LilToonLiveValuesRenderTests.cs`.
- Docs: `README.md` atlas section and `Packages/com.alrauna.amuse/CHANGELOG.md`.

---

### Task 1: Generic tier of the key

**Files:**
- Modify: `Editor/Host/AtlasUseCapture.cs` (`MaterialKeyOf`, `MaterialKeyWalker`)
- Test: `Tests/Editor/Host/AtlasUseCaptureTests.cs`

**Interfaces:**
- Produces: `MaterialKeyOf(Material material, ISet<string> skippedTextureProperties, ISet<string> deadProperties)`. In this task every caller passes an empty dead set.

- [ ] **Step 1: Write the tests.**
  - `StaleSavedValueReachesAnUndeclaredUniform` (characterization, Unity fact). Create shader X with `_Foo` in Properties, and shader Y that declares `float _Foo;` without a Properties entry. Both output `_Foo` as red. Use `ShaderUtil.CreateShaderAsset`. Set `_Foo = 1` on a material of X, then switch it to Y. Blit it to a 4 x 4 `RenderTexture`. Assert red is 1 and `HasProperty("_Foo")` is false.
  - `ExplicitQueueEqualToTheShaderQueueLeavesTheKey`: two admitted lilToon fixtures. Set one `renderQueue` to `shader.renderQueue`. Assert that the serialized `m_CustomRenderQueue` differs (no-op guard) and the `MaterialKey` values are equal.
  - `--- Falsifier 1: ExplicitQueueDifferentFromTheShaderQueueChangesTheKey ---`: the same with queue 2001. Keys differ.
  - `InvalidKeywordLeavesTheKey`: call `EnableKeyword("AMUSE_TEST_UNDECLARED")` on one fixture. Assert that the serialized `m_InvalidKeywords` holds it (no-op guard) and the keys are equal.
  - `--- Falsifier 2: StaleSavedEntryStillChangesTheKeyWithoutAReviewedModel ---`: the `LilToonMainColor` route (an older attested version) with a stale `_DetailMask` float written through `SerializedObject`. Keys differ.
  - `SavedPropertyOrderDoesNotShiftLaterTokens`: one fixture with a stale entry that sorts before `_MainTex`. Assert that the key token for `_MainTex` is the same string in both keys.
- [ ] **Step 2: Run the five new tests.** Expected: the characterization test passes. The two equality tests and the order test fail on the current walker. The falsifiers pass.
- [ ] **Step 3: Implement.**
  - The walker skips the `m_Name`, `m_CustomRenderQueue`, `m_InvalidKeywords`, and `m_SavedProperties` subtrees in the generic walk.
  - It appends `queue=i<material.renderQueue>;`. The shader reference stays in the key, so equal effective queues give equal page materials.
  - It appends saved properties by name, after the generic walk, in four sections: `m_TexEnvs`, `m_Ints`, `m_Floats`, `m_Colors`. Each section is sorted by ordinal name.
  - Token shapes:
    - `t:<name>=<member|null|o<id>>,<scale bits>,<offset bits>;`
    - `i:<name>=<int>;`
    - `f:<name>=<bits>;`
    - `c:<name>=<r,g,b,a bits>;`
  - A name in `deadProperties` adds no token.
  - Read the arrays through `SerializedObject`, as today. Keep both texture name shapes (`.first` and `.first.name`).
  - Keep the `InvalidOperationException` for an unknown serialized type in the generic walk.
- [ ] **Step 4: Run the five tests plus `AtlasUseCaptureTests` and `AtlasEndToEndTests`.** Expected: all pass. If an existing test pins an old token shape, update the literal only.
- [ ] **Step 5: Commit** `feat(atlas): key saved properties by name and use the effective queue`.

### Task 2: Reviewed live value model for lilToon 2.4.1

**Files:**
- Create: `Editor/Semantics/AtlasDeadValueFamily.cs`, `Editor/Semantics/AtlasLiveValueModel.cs`, `Editor/Semantics/LilToon/LilToonLiveValues.cs`, `Editor/Semantics/VRChatFallbackProperties.cs`
- Modify: `Editor/Semantics/AtlasRouteTable.cs`, `Editor/Host/AtlasUseCapture.cs`
- Test: `Tests/Editor/Semantics/LilToonLiveValuesTests.cs`, `Tests/Editor/Host/AtlasUseCaptureTests.cs`

**Interfaces:**
- Produces:
  - `internal sealed class AtlasDeadValueFamily`, with members:
    - `string Name`
    - `IReadOnlyList<string> Switches`: the property names that decide the family.
    - `Func<Material, string, bool> IsOff`, which takes the material and the shader row name.
    - `IReadOnlyList<string> Properties`: exact names, with no prefixes.
  - `internal sealed class AtlasLiveValueModel`, with members:
    - `IReadOnlyList<AtlasDeadValueFamily> Families`
    - `IReadOnlyCollection<string> DeclaredUniforms`
    - `ISet<string> DeadProperties(Material material, string shaderRowName, Func<string, bool> switchIsDynamic)`
  - `AtlasRouteTable.LiveValues`, of type `AtlasLiveValueModel`. It is null on every table except `LilToonReviewedMainColor`.
  - `VRChatFallbackProperties.Names`, of type `IReadOnlyCollection<string>`.
- Consumes: `MaterialKeyOf(..., deadProperties)` from Task 1.

- [ ] **Step 1: Review the source and write the data.** Write `LilToonLiveValues.cs` from the lilToon 2.4.1 source under `Packages/jp.lilxyzw.liltoon/Shader/`.
  - Every family gets an XML doc line that cites the file and line of its gate.
  - `DeclaredUniforms`:
    - every name declared by a uniform line (`float`, `float2`, `float3`, `float4`, `half`, `half4`, `int`, `uint`, `lilBool`) in `Includes/lil_common_input*.hlsl`, in all `#if` branches;
    - every name in a `TEXTURE2D`, `TEXTURECUBE`, `TEXTURE2D_ARRAY`, or `SAMPLER` declaration;
    - every Properties name of the admitted rows.
  - The starting families come from the Properties order. The review confirms or splits each one:

    |Family|Off when|Properties|
    |---|---|---|
    |Never read on admitted rows|always|`_lilToonVersion`, `_TransparentMode`, `_BaseColor`, `_IDMaskCompile`, `_UDIMDiscardCompile`, `_TessEdge`, `_TessStrength`, `_TessShrink`, `_TessFactorMax`, `_DummyProperty`, plus each name that the review finds in no admitted pass|
    |Transparent only|the row is opaque|`_SubpassCutoff`, plus each `LIL_RENDER == 2` name|
    |Outline|the row has no outline pass|every `_Outline*` name of the table above|
    |Main2nd|`_UseMain2ndTex == 0`|`_Color2nd` and every `_Main2nd*` name except the switch|
    |Main3rd|always on admitted uses, because the switch is required to be 0|`_Color3rd` and every `_Main3rd*` name|
    |Glitter|the same, with `_UseGlitter`|every `_Glitter*` name|
    |AudioLink|the same, with `_UseAudioLink`|every `_AudioLink*` name|
    |Parallax|the same, with `_UseParallax`|`_UsePOM`, `_ParallaxMap`, `_Parallax`, `_ParallaxOffset`|
    |Dissolve|`_DissolveParams.x` greater than -0.5 and less than 0.5|`_DissolveMask`, `_DissolveNoiseMask`, `_DissolveNoiseMask_ScrollRotate`, `_DissolveNoiseStrength`, `_DissolveColor`, `_DissolvePos`|
    |Dither|`_UseDither == 0`|`_DitherTex`, `_DitherMaxValue`|
    |Alpha mask|`_AlphaMaskMode == 0`|`_AlphaMask`, `_AlphaMaskScale`, `_AlphaMaskValue`|
    |Bump|`_UseBumpMap == 0`|`_BumpMap`, `_BumpScale`|
    |Bump 2nd|`_UseBump2ndMap == 0`|`_Bump2ndMap`, `_Bump2ndMap_UVMode`, `_Bump2ndScale`, `_Bump2ndScaleMask`|
    |Anisotropy|`_UseAnisotropy == 0`|every `_Anisotropy*` name|
    |Backlight|`_UseBacklight == 0`|every `_Backlight*` name|
    |Shadow|`_UseShadow == 0`|every `_Shadow*`, `_Shadow2nd*`, and `_Shadow3rd*` name that the review finds only under the switch|
    |Rim shade|`_UseRimShade == 0`|every `_RimShade*` name|
    |Reflection|`_UseReflection == 0`|`_Smoothness`, `_SmoothnessTex`, `_Metallic`, `_MetallicGlossMap`, `_Reflectance`, `_GSAAStrength`, `_ApplySpecular`, `_ApplySpecularFA`, `_SpecularToon`, `_SpecularNormalStrength`, `_SpecularBorder`, `_SpecularBlur`, `_ApplyReflection`, every `_Reflection*` name|
    |MatCap|`_UseMatCap == 0`|every `_MatCap*` name that is not `_MatCap2nd*`|
    |MatCap 2nd|`_UseMatCap2nd == 0`|every `_MatCap2nd*` name|
    |Rim|`_UseRim == 0`|every `_Rim*` name that is not `_RimShade*`|
    |Emission|`_UseEmission == 0`|every `_Emission*` name that is not `_Emission2nd*`, and `_egci`, `_egai`, `_egc0` to `_egc7`, `_ega0` to `_ega7`|
    |Emission 2nd|`_UseEmission2nd == 0`|every `_Emission2nd*` name, and the matching `_e2g*` names|
    |Main gradation|`_MainGradationStrength == 0`|`_MainGradationTex`|

  - The data lists exact names. A family never holds its own switch.
  - A family drops a name that the review finds read outside its switch. That name stays live.
  - A texture name in a family also removes its TexEnv scale and offset, because the token holds both.
  - The comparisons use `!= 0f`, so NaN counts as on.
- [ ] **Step 2: Write the tests** in `LilToonLiveValuesTests` and `AtlasUseCaptureTests`.
  - `DeclaredUniformsCoverEveryCompiledName`:
    - For each admitted lilToon row shader, compile each pass with `ShaderData.Pass.CompileVariant`, using `ShaderType.Vertex` and `ShaderType.Fragment`, `ShaderCompilerPlatform.D3D`, and `BuildTarget.StandaloneWindows64`.
    - Use these keyword sets: `{}`, `{DIRECTIONAL, SHADOWS_SCREEN, LIGHTPROBE_SH}`, `{DIRECTIONAL, LIGHTPROBE_SH, VERTEXLIGHT_ON}`, `{POINT}`, `{SPOT}`, `{SHADOWS_DEPTH}`, `{SHADOWS_CUBE}`.
    - Assert that every field of every constant buffer whose name does not start with `Unity`, and every texture binding whose name does not start with `unity_`, is in `DeclaredUniforms`. A failure lists the missing names.
  - `FamiliesNameOnlyDeclaredProperties`: every family name exists as a Properties name of at least one admitted row.
  - `FamiliesNeverHoldASwitch`: no family property is in any family's `Switches`.
  - `--- Falsifier 3: DeadFamilyValueLeavesTheKey ---`: two reviewed fixtures with `_UseRim` 0 and different `_RimColor`. Keys are equal. A third fixture with `_UseRim` 1 and the same difference gives different keys.
  - `--- Falsifier 4: AnimatedSwitchKeepsTheFamilyLive ---`: the `_UseRim` 0 pair plus a clip that drives `material._UseRim`. Keys differ. The slot is admitted, not refused.
  - `PropertyBlockOnASwitchKeepsTheFamilyLive`: the same with a `MaterialPropertyBlock` that sets `_UseRim`.
  - `StaleUndeclaredEntryLeavesTheKey`: a stale `_AmuseTestStale` float on one fixture. Keys are equal.
  - `--- Falsifier 5: StaleEntryWithADeclaredUniformNameChangesTheKey ---`: pick a name from `DeclaredUniforms` that is no Properties name of the row, and fail the test if none exists. Write it stale. Keys differ.
  - `--- Falsifier 6: FallbackNameAlwaysCounts ---`: a stale `_Glossiness` float on one fixture. Keys differ.
  - `NullFallbackTextureEqualsAnAbsentOne`: a stale `_DetailAlbedoMap` TexEnv with a null texture and scale 2. Keys are equal.
  - `--- Falsifier 7: ToonStandardFallbackTagKeepsEveryValue ---`: `SetOverrideTag("VRCFallback", "toonstandard")` on both fixtures, with `_UseRim` 0 and different `_RimColor`. Keys differ.
- [ ] **Step 3: Run the new tests.** Expected: the data tests and falsifiers 3, 4, 5, and 7 fail before the integration. Record the observed failures.
- [ ] **Step 4: Implement.**
  - `AtlasLiveValueModel.DeadProperties` returns the union of `Properties` over each family where:
    - `IsOff(material, row)` holds, and
    - no switch satisfies `switchIsDynamic`.
  - `CaptureSlot` builds `switchIsDynamic` from two sources:
    - `animation.AnimatedMaterialPropertyNames`, with the same prefix rule as `AnimatedRouteProperty`;
    - the renderer property blocks, through `BlockNamesRoute`.
  - The walker, when the table has a model, does three things:
    - It skips each dead name.
    - It skips each saved name that is not a Properties name of the shader, is not in `DeclaredUniforms`, and is not in `VRChatFallbackProperties.Names`.
    - It writes a fallback-name TexEnv whose texture is null as no token.
  - When `material.GetTag("VRCFallback", false)` starts with `toonstandard`, the dead set is empty and no stale name is skipped.
- [ ] **Step 5: Run `LilToonLiveValuesTests`, `AtlasUseCaptureTests`, and `AtlasEndToEndTests`.** Expected: all pass.
- [ ] **Step 6: Commit** `feat(atlas): drop dead lilToon 2.4.1 values from the material key`.

### Task 3: Render oracle for the dead families

**Files:**
- Test: `Tests/Editor/Semantics/LilToonLiveValuesRenderTests.cs`

**Interfaces:**
- Consumes: `LilToonLiveValues` families, `AtlasLiveValueModel.DeadProperties`.

- [ ] **Step 1: Write `DeadFamilyValuesDoNotChangeTheRender`**, with one `TestCaseSource` case per family and per admitted opaque and outline row.
  - Scene:
    - a camera that renders a lit sphere to a 64 x 64 ARGB32 `RenderTexture`;
    - one directional light with soft shadows onto a receiver plane, and one point light, so that ForwardBase, ForwardAdd, and ShadowCaster all run;
    - the outline pass on outline rows.
  - Material: the reviewed fixture with every switch that Task 2 does not require to be 0 turned on, except the switch of the family under test.
  - Render once. Then set every family property to a different value:
    - a float gets value + 0.37;
    - a color or vector gets each channel + 0.37;
    - a texture gets a 4 x 4 noise texture.
  - Render again. Assert that the two readbacks are equal byte for byte.
  - No-op guard: assert that `DeadProperties` holds every perturbed name, and that the family switch of the case is off.
- [ ] **Step 2: Write `--- Falsifier 8: LiveFamilyValueChangesTheRender ---`**: the Rim family with `_UseRim` 1 and the same perturbation. The readbacks differ.
- [ ] **Step 3: Run the file.** Expected: every case passes. A failing family case is a data defect. Move each name that the failure names out of the family in `LilToonLiveValues.cs`, then rerun. Never loosen the comparison.
- [ ] **Step 4: Commit** `test(atlas): render oracle for dead lilToon values`.

### Task 4: Dead textures are not unlisted

**Files:**
- Modify: `Editor/Host/AtlasUseCapture.cs` (`ResolveRouteTextures`)
- Test: `Tests/Editor/Host/AtlasUseCaptureTests.cs`

**Interfaces:**
- Consumes: `AtlasLiveValueModel.DeadProperties` from Task 2.

- [ ] **Step 1: Write the tests.**
  - `DeadFamilyTextureAdmitsAndLeavesTheKey`: a reviewed fixture with `_UseGlitter` 0 and an assigned `_GlitterShapeTex`. Expect admitted, and a key equal to that of a fixture with no `_GlitterShapeTex`.
  - `--- Falsifier 9: LiveUnlistedTextureStillRefuses ---`: an assigned `_Emission2ndMap` with `_UseEmission2nd` 1. Expect `UnlistedTexture`.
- [ ] **Step 2: Run them.** Expected: the first fails with `UnlistedTexture`, and the falsifier passes.
- [ ] **Step 3: Implement.** `ResolveRouteTextures` takes the dead set. It computes the dead set once, before the walk. A dead texture goes to `KeySkipped` and never to `Unlisted`. A companion route still decides its own texture first.
- [ ] **Step 4: Run `AtlasUseCaptureTests`.** Expected: all pass.
- [ ] **Step 5: Commit** `feat(atlas): dead lilToon textures do not refuse a slot`.

### Task 5: Docs and the Lab check

- [ ] **Step 1: Update the docs.**
  - `README.md` atlas section: two slots share a page when every value that their shader can read is equal. Name the inert values: values for features that are off, saved values that the shader does not declare, the saved queue when it equals the shader queue, and keywords the shader does not declare.
  - `CHANGELOG.md`: one `Changed` entry, dated.
- [ ] **Step 2: Run the full `Alrauna.Amuse.Tests.Editor` and `Alrauna.Amuse.Research.Tests.Editor` assemblies.** Expected: 0 failures. Record the counts.
- [ ] **Step 3: Census Lab check, read-only.**
  - Check that `Application.dataPath` ends with `/AMUSE-Census-Lab/Assets`.
  - Diff the group keys of the user's pair, as on 2026-11-22.
  - Expected: only the live differences of the findings remain. Report them by property name, with no asset names.
- [ ] **Step 4: Sweep the changed docs for identifiers.**
- [ ] **Step 5: Commit** `docs(atlas): describe live value grouping`.

## Assumptions and contingencies

- The review covers only lilToon 2.4.1. Other admitted lilToon versions and Poiyomi rows use the generic tier. Poiyomi gets its own model only after a separate review.
- Material variant bookkeeping (`m_Parent`, `m_ModifiedSerializedProperties`, `m_LockedProperties`) stays in the key. This plan did not establish how Unity stores variant values.
- lilToon builds its feature set from the project shader setting. `DeclaredUniforms` reads all `#if` branches, so it is a superset for every setting.
- If `CompileVariant` cannot compile a lilToon pass in EditMode, the uniform test fails with the compiler message. Then stop and report it. Do not drop the test.
