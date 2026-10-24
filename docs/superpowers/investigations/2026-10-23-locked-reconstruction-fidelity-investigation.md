# Locked-material reconstruction fidelity investigation

Status: 2026-10-23, third revision. Read-only characterization. No product code changed. No Unity run. The first revision integrated three parallel read-only slices. Three parallel adversarial reviews (vendor facts, AMUSE trace, missed false positives) reported four CRITICAL and nineteen MAJOR findings, plus a set of MINOR findings. Merged by cause, the MAJOR findings are sixteen. The second revision integrated all of them. A scoped re-review of the second revision reported two MAJOR and ten MINOR findings. The third revision integrates those.

Privacy note: this record covers AMUSE source, earlier AMUSE records, and public vendor packages. It contains no private data. Vendor packages were read in a scratch folder outside the repository. Vendor paths use the placeholders `<poiyomi-9.3.64>` for the package root of Poiyomi Toon 9.3.64 and `<thryeditor-2.74.x>` for the package root of the standalone thryeditor. `<optimizer-9.3.64>` is short for `<poiyomi-9.3.64>/_PoiyomiShaders/Scripts/ThryEditor/Editor/ShaderOptimizer.cs`. `<shader-9.3>` is short for `<poiyomi-9.3.64>/_PoiyomiShaders/Shaders/9.3/Toon/Poiyomi Toon.shader`. AMUSE paths are relative to `Packages/com.alrauna.amuse/` unless they start with `docs/`.

Line numbers for `<thryeditor-2.74.x>` are 2.74.0 lines. Versions 2.74.0, 2.74.1, and 2.74.2 have one file digest. In 2.74.3, lines after 1253 shift by 2 and lines after 1382 shift by 5. `[MEASURED]`

Evidence tags: `[SOURCE]` is a file and line that was read. `[MEASURED]` is a command that ran for this record. `[INFERENCE]` is a conclusion from cited evidence that no command verified. Every search result in this record is as of 2026-10-23.

Terms: `K8` is the locked-original check in `Editor/Build/LockedMaterialIdentity.cs`. `K9` is the refusal of rename-suffixed animation bindings in `Editor/Host/AnimationBindingResolution.cs`. Both come from the design above. Inversion is the step at window close that swaps `U` back to `L` in slots and curves.

## 1. Question

The design `docs/superpowers/specs/2026-10-23-poiyomi-per-shader-attestation-design.md` records four open findings in locked-material reconstruction (section "Recorded findings for the follow-up branch"). It also records one K9 follow-up: mapping a suffixed animation binding back to its plain property. This record answers three questions for each item:

1. What does the vendor do, and where does AMUSE differ?
2. Can the difference cause a false positive, only a coverage loss, or only an output-fidelity defect?
3. What evidence does a fix need?

The adversarial reviews found more false-positive routes than the four recorded findings. Section 4 adds them. This record does not design a fix.

## 2. The invariant that decides the class

The transient unlocked clone `U` is built from the locked material `L` (`Editor/Build/TransientUnlockSwapIn.cs:245-272`). Evidence capture, preparation, and conversion read `U` (`Editor/Build/AmusePlatformFinishPass.cs:400-404, 557-641`, `Editor/Build/AlphaSeparationPreparation.cs:549-605`). Capture reads plain property names through typed getters (`Editor/Host/UnityMaterialEvidenceCapture.cs:296-298, 320-321, 425-427, 451, 472`). `[SOURCE]`

The build output is as follows. `[SOURCE]` `Editor/Build/TransientUnlockWindowClose.cs:161-205, 214-331`, `Editor/Semantics/CanonicalOpaqueVerification.cs:97-112`, `Editor/Build/AlphaSeparationMaterialConverter.cs:281-287`.

- A transformed slot ships the canonical opaque material `O`. `O` is `new Material(U)` plus the canonical tuple, queue 2000, and `RenderType` `Opaque`. `O` keeps the original shader.
- An untransformed slot ships `L`.
- An `AlreadyOpaque` slot uses `U` as its target. Window close reverts `U` to `L` in every recorded slot and curve.
- On the retention path, `U` ships wherever the inversion could not reach. This happens when a clip or a renderer under the avatar still names `U`.

A proof moves triangles from `L` to `O`. The proof is sound only when every triangle it moves renders opaque under `L`. The proof reads `U`. So the decisive invariant is this:

**Every alpha-relevant effective fact of `U` equals the fact that `L` renders with.**

A gap that breaks this invariant for an alpha-relevant fact can cause a false positive. A gap that touches only facts the proof does not read is output fidelity or coverage loss. `[INFERENCE]` from the trace above.

`L` renders with four kinds of facts. `[SOURCE]` `<optimizer-9.3.64>:726-733, 1091-1095, 1104-1114, 1150-1235, 1239-1250, 1293-1309, 1380-1390, 1602-1607, 1671-1683`.

- **Baked literals.** A property with no `Animated` tag is a constant in the locked shader. A texture is not baked, but its `_ST` and `_TexelSize` values are baked. A property marked `DoNotLock` or `NL` is not baked.
- **Live plain properties.** A property with tag `1` stays live under its plain name. A tag-`2` property whose name ends in `UV` or `Pan` also stays live under its plain name. Any non-empty tag stops the baking.
- **Live suffixed properties.** A tag-`2` property is renamed to `<plain>_<suffix>` in the locked shader code. The lock copies the value to the suffixed name. For a name in the duplicate set, the plain definition line also stays when it is a one-line definition with a one-word type. For example, `_Cutoff`, `_BumpScale`, `_DetailNormalMapScale`, `_Metallic`, and `_Mode` use `Range` or a multi-line definition, so they lose the plain declaration. They act as renames.
- **Structure fixed at lock time.** Each texture assigned at lock gets a `PROP<NAME>` define. This happens before the tag check, so a tag does not prevent it. Enabled keywords become defines. An `//ifex` block is removed by its value at lock, even for an animated property, unless its condition carries `isNotAnimated(...)`. Passes for disabled light modes are removed.

## 3. The vendor rename-suffix rule

3.1. The cleaner `CleanStringForPropertyNames` trims the input and removes spaces. It keeps the bytes A-Z, a-z, 0-9, and the underscore. It writes every other UTF-8 byte as two uppercase hex digits. `[SOURCE]` `<optimizer-9.3.64>:656-671`, `<thryeditor-2.74.x>/Editor/ShaderOptimizer.cs:708-725`.

3.2. `[MEASURED]` A SHA-256 comparison of the extracted cleaner body across the bundled 9.3.64 ThryEditor and thryeditor 2.74.0, 2.74.1, 2.74.2, and 2.74.3 gave one digest. The cleaner did not change.

3.3. The suffix is the cleaner applied to the `thry_rename_suffix` override tag. The fallback is the material name. No numbering or collision check exists. A custom suffix typed in the lock button UI has its spaces replaced by underscores before it is stored. `[SOURCE]` `<optimizer-9.3.64>:701-712`, `<thryeditor-2.74.x>/Editor/ShaderOptimizer.cs:752-763`, `<poiyomi-9.3.64>/_PoiyomiShaders/Scripts/ThryEditor/Editor/Drawers/ThryShaderOptimizerLockButton.cs:71-85`.

3.4. The rename applies only when the `<plain>Animated` tag is exactly `2`. Names that end in `UV` or `Pan` are never renamed. Names in `IllegalPropertyRenames` are duplicated instead (section 2). That set includes `_MainTex`, `_Color`, `_EmissionColor`, `_Cutoff`, `_DstBlend`, and `_Mode`. A renamed or duplicated texture also renames its `_ST` and `_TexelSize` forms. Only thryeditor 2.74.3 also renames `sampler<name>`. Versions 2.74.0 through 2.74.2 do not. `[SOURCE]` `<optimizer-9.3.64>:253-268, 1150-1171`, `<thryeditor-2.74.x>/Editor/ShaderOptimizer.cs:281-302, 1367-1404`, 2.74.3 line 1387.

3.5. In 9.3.64 the rename regex has no left word boundary (`<optimizer-9.3.64>:1289, 1303`). A tag-`2` rename of `<plain>` also rewrites every identifier that ends in `<plain>`. The collateral property gets no copied value, so `L` renders its shader default. thryeditor 2.74.0 adds the boundary and names the `_LTCGI_Metallic` case in a comment (`<thryeditor-2.74.x>/Editor/ShaderOptimizer.cs:1638-1640`). `[MEASURED]` Nine property pairs in the 9.3 Properties block have this shape, for example `_RimLightColor` and `_Ap_RimLightColor`. None is an alpha input as of 2026-10-23.

3.6. thryeditor 2.74 writes the cleaned suffix at lock time into `thry_locked_rename_suffix`. The unlock prefers this tag. It clears the tag right after the shader swap, before it restores textures and renamed values. The vendor comment says the tag protects against a rename or a duplicate while locked. `[SOURCE]` `<thryeditor-2.74.x>/Editor/ShaderOptimizer.cs:84, 1916, 2861-2863, 2900`. `[MEASURED]` A search of the 9.3.64 package for `thry_locked_rename_suffix` found no match.

3.7. The suffix string rule did not change between 9.3.64 and 2.74. Only the persisted input and the word boundary changed.

## 4. Findings

Findings 1 to 4 are the recorded findings of the design. Findings 5 to 8 are new in this record.

### Finding 1: the AMUSE suffix can differ from the lock-time suffix

4.1.1. AMUSE writes the underscore as `5F` (`Editor/Build/LockedMaterialReconstruction.cs:550-573`). The vendor keeps it. Material `Cape_Body` gets the vendor suffix `Cape_Body` and the AMUSE suffix `Cape5FBody`. A custom suffix with a space gets an underscore from the vendor UI (3.3), so AMUSE also misses it. `[SOURCE]`

4.1.2. AMUSE falls back to the current clone name (`:178-180`). A material renamed after a 9.3.64 lock gets a wrong AMUSE suffix even without an underscore. The 9.3.64 vendor unlock has the same weakness, because it also uses the current name (`<optimizer-9.3.64>:2326`). `[SOURCE]` `[INFERENCE]` A build tool that runs before AMUSE and copies `L` can add `(Clone)` to the name. The swap-in comment says the name is load-bearing (`Editor/Build/TransientUnlockSwapIn.cs:254-259`).

4.1.3. A wrong suffix makes `TryPlainName` match nothing. `MoveSuffixedSavedValues` then moves nothing and returns normally (`:175-296`, `:491-507`). The restore verification checks six identity facts and no value (`Editor/Build/TransientUnlockSwapIn.cs:285-348`). The build continues. `[SOURCE]`

4.1.4. `U` then keeps the plain saved entry for each tag-`2` name. `L` renders the `<plain>_<suffix>` value. This covers renamed names and duplicated names alike, because the AMUSE mover has no duplicate-set exclusion (`Editor/Build/LockedMaterialReconstruction.cs:198-200, 219-221, 279-282`). The two values are equal only when nobody changed the suffixed value after the lock. `[SOURCE]` `[INFERENCE]`

4.1.5. While `L` is locked, the Thry inspector keeps the suffixed field editable when its tag survives. It keeps the plain duplicate read-only. `[SOURCE]` `<poiyomi-9.3.64>/_PoiyomiShaders/Scripts/ThryEditor/Editor/EditorStructs/ShaderProperty.cs:278-300, 330-331`.

4.1.6. Alpha-relevant names that can carry tag `2` in the 9.3 shader. `[SOURCE]` `<shader-9.3>:45, 59, 61, 66, 72-79, 160-161, 167, 175, 227, 2132, 2135, 2191, 4596-4598`, `Editor/Semantics/Poiyomi/PoiyomiAlphaInterpreter.cs:126-132, 171-226`.

- Duplicate set: `_MainTex`, `_Color`, `_Cutoff`, `_Mode`.
- Alpha mask: `_AlphaMask` with `_AlphaMask_ST`, `_MainAlphaMaskMode`, `_AlphaMaskBlendStrength`, `_AlphaMaskValue`, `_AlphaMaskInvert`.
- Main alpha: `_MainIgnoreTexAlpha`, `_AlphaForceOpaque`, `_AlphaMod`.
- Coverage and blending: `_AlphaToCoverage`, `_AlphaDithering`, `_AlphaPremultiply`.
- Outline: `_LineColor`, `_OutlineOverrideAlpha`, `_OutlineTexture`, `_OutlineBlendOp`, `_OutlineSrcBlend`, `_OutlineDstBlend`.

`[DoNotAnimate]` blocks only the inspector tag write. The lock reads the tag directly (`<poiyomi-9.3.64>/_PoiyomiShaders/Scripts/ThryEditor/Editor/EditorStructs/ShaderPart.cs:574`, `<optimizer-9.3.64>:1150`). `[SOURCE]` Several listed names carry `[HideInInspector]`: `_MainIgnoreTexAlpha`, `_MainAlphaMaskMode`, `_AlphaMaskBlendStrength`, `_AlphaMaskValue`, `_AlphaMaskInvert`, `_AlphaToCoverage`, and `_AlphaDithering` (`<shader-9.3>:66, 74-78, 167, 175`). They can carry the tag through script writes. Whether the inspector writes the tag for them is not measured. `_AlphaMod` and `_Color` are visible.

4.1.7. Example. `_AlphaMod` has tag `2` on a material named `Cape_Body`. After the lock, the user sets `_AlphaMod_Cape_Body` to -0.3 in the inspector. `L` renders alpha below 1. `U` reads the plain value 0. The zero gate passes (`Editor/Semantics/Poiyomi/PoiyomiAlphaInterpreter.cs:504`). The proof moves the triangles. The conversion gates read some of the same names, for example `_OutlineDstBlend` (`Editor/Semantics/Poiyomi/PoiyomiOpaqueConversion.cs:330-436, 471-493`). A stale gate input lets conversion accept a state that `L` does not use. `[INFERENCE]`

4.1.8. Class: **false-positive capable, unmeasured.** The route is reachable through the vendor inspector if the tag survives the lock and the user edits the suffixed field after the lock. Section 6 items 1 and 2 measure these preconditions. No AMUSE gate detects the route.

4.1.9. The tests cannot see this. `ReconCape-7` gives `ReconCape2D7` under both rules. No test has an underscore name or an `_ST` case. `[MEASURED]` search of `Tests/Editor/Build/LockedMaterialReconstructionTests.cs`.

### Finding 1a: the 2026-09-20 probe saw no `Animated` tag after the lock

4.1a.1. The 2026-09-20 round trip saw the `<property>Animated` tag absent after the lock, at both pins (`docs/superpowers/investigations/2026-09-20-thry-lock-unlock-roundtrip.md`, probe 2). `[SOURCE]`

4.1a.2. The vendor source conflicts with that result. No lock or swap code clears the tag in 9.3.64 or 2.74. Only `DeleteTags` clears it, and only the inspector "Reset Material" action calls it (`<optimizer-9.3.64>:2487-2505`, `<poiyomi-9.3.64>/_PoiyomiShaders/Scripts/ThryEditor/Editor/ThryEditor.cs:552-554`). Both vendor unlocks read the tag after the lock (`<optimizer-9.3.64>:2336-2339`, `<thryeditor-2.74.x>/Editor/ShaderOptimizer.cs:2873-2874`). The tag existed when the lock read it, because the rename happened. `[SOURCE]` The inspector writes the tag with the same `SetOverrideTag` call that a script uses (`<poiyomi-9.3.64>/_PoiyomiShaders/Scripts/ThryEditor/Editor/EditorStructs/ShaderPart.cs:1174`, `<optimizer-9.3.64>:633-638`). So the inspector versus script axis does not explain the result. `[INFERENCE]` A harness artifact is possible.

4.1a.3. If the tag was absent, the vendor unlock skipped the restore too. The 0.37 value after unlock was the plain entry that the lock never changed. The probe never edited the suffixed value. `[SOURCE]` `<optimizer-9.3.64>:752-781, 1602-1607, 2337`.

4.1a.4. If the tag is absent, the AMUSE mover moves nothing for any name (`Editor/Build/LockedMaterialReconstruction.cs:199, 220, 251, 268, 280, 535-547`). The locked inspector then shows the suffixed field read-only (4.1.5). So a false positive then needs a write that does not come from the inspector, as in finding 5. Finding 1a covers more names than finding 1. It is not more likely to occur. `[INFERENCE]`

4.1a.5. Class: false-positive capable, through non-inspector writes only. Section 6 item 1 settles which of findings 1 and 1a applies.

### Finding 2: render queue, `RenderType`, `VRCFallback`, and stripped textures

4.2.1. The vendor saves the render queue and the `RenderType` override tag before each shader swap and writes them back after. The vendor comment says a swap deletes the tag and resets the queue. thryeditor 2.74 also carries `VRCFallback` across the swap. `[SOURCE]` `<optimizer-9.3.64>:1552-1559, 1591-1594, 2346-2354`, `<thryeditor-2.74.x>/Editor/ShaderOptimizer.cs:1919-1924, 1967-1971, 2884-2885, 2896-2898`, `<thryeditor-2.74.x>/Editor/Helpers/MaterialHelper.cs:12-17`. `[MEASURED]` A search of the AMUSE package for `renderQueue` in `LockedMaterialReconstruction.cs`, for `RenderType` there, and for `VRCFallback` anywhere found no match.

4.2.2. `ReadEffectiveRenderState` reads `material.renderQueue` and `GetTag("RenderType", false)` (`Editor/Semantics/EffectiveRenderState.cs:29-30`). An absent override resolves to the SubShader values `Geometry` and `Opaque` (`<shader-9.3>:4671`). For Poiyomi, only `IsFunctionallyOpaque` reads both facts (`Editor/Semantics/Poiyomi/PoiyomiOpaqueConversion.cs:307, 467-499`). So a dropped override can only make `AlreadyOpaque` true. `AlreadyOpaque` uses `U` as the target, and window close reverts `U` to `L` (section 2). No triangle changes material through this route. `[SOURCE]` `[INFERENCE]` This analysis covers Poiyomi only. lilToon eligibility also reads queue and type (`Editor/Semantics/LilToon/LilToonCutoutSourceEligibility.cs:165-178`), but lilToon has no locked path.

4.2.3. At lock, the vendor deletes the texture entry of each `2D` property inside an excluded feature block, when the definition ends in `{ }`. It records the texture asset in a `_stripped_tex_<property>` tag. The 9.3.64 lock also deletes every texture entry with no texture, together with its tiling. 2.74 keeps null entries and deletes entries for properties the shader does not declare. The unlock assigns stripped textures back but not their tiling. `[SOURCE]` `<optimizer-9.3.64>:1557-1580, 1719-1728, 2357-2365`, `<thryeditor-2.74.x>/Editor/ShaderOptimizer.cs:1940-1953`, round trip record section 3 item 2. AMUSE never reads these tags.

4.2.4. Of the alpha textures, only `_OutlineTexture` can be stripped. `_EnableOutlines` carries `[DoNotAnimate]` (`<shader-9.3>:2121`), so the lock bakes it. The lock strips `_OutlineTexture` only when that value is not 1 (`<shader-9.3>:2119`). `U` reads the same baked value. For the value 0, the interpreter returns before it reads the texture (`Editor/Semantics/Poiyomi/PoiyomiAlphaInterpreter.cs:640-654`). A null outline texture reads as factor 1 (`:713-718`). `[SOURCE]`

4.2.5. Class: **output fidelity. No false positive found.** The loss shows on `O`, which copies `U`, and on the retention path. `[INFERENCE]` The exact Unity swap behavior for queue and tags is unmeasured (section 6, item 4).

### Finding 3: duplicated properties

4.3.1. The vendor unlock reads only the properties that the locked shader declares. It skips tag values other than `2`, `UV` and `Pan` endings, and the duplicate set. AMUSE moves every tag-`2` entry whose plain name the original shader declares. `[SOURCE]` `<optimizer-9.3.64>:2328-2339`, `<thryeditor-2.74.x>/Editor/ShaderOptimizer.cs:2866-2879`, `Editor/Build/LockedMaterialReconstruction.cs:175-296`.

4.3.2. For a duplicated name, the locked shader code reads `<plain>_<suffix>` (section 2). So `L` renders the suffixed value. When the AMUSE move runs, it copies the suffixed value onto the plain name, and `U` matches `L`. The vendor unlock keeps the plain value, which can differ after an edit while locked. `[SOURCE]` `<optimizer-9.3.64>:1293-1309, 1602-1607`.

4.3.3. Class: **unplanned divergence that keeps fidelity when the move runs.** The AMUSE comments at `Editor/Build/LockedMaterialReconstruction.cs:43-45` and `:167-169` say that duplicated names have no tag and stay untouched. The vendor source shows that a duplicated name has tag `2` (`<optimizer-9.3.64>:1150-1161`). So the move reaches these names because the tag test passes, not by design. A fix must keep the move for the duplicate set. A fix that copies the vendor skip list would leave stale plain values for `_MainTex`, `_Color`, `_Cutoff`, and `_Mode`. When the move does not run, finding 1 applies to these names (4.1.4). `UV` and `Pan` names have no current-cycle suffixed copy. The mover reaches them only through stale entries from an earlier lock. `[INFERENCE]`

### Finding 4: `thry_locked_rename_suffix` is unread

4.4.1. AMUSE reads only `thry_rename_suffix` (`Editor/Build/LockedMaterialReconstruction.cs:39, 178-180`). `[MEASURED]` A search of the package source for `thry_locked_rename_suffix` found no match.

4.4.2. K8 keys on the row of the original shader, not on the lock tool (`Editor/Build/LockedMaterialIdentity.cs:277-301`). The two 9.3.64 rows admit locked originals (`Editor/Semantics/Poiyomi/PoiyomiSourceAttestation.cs:103-130`). The 2026-09-20 round trip ran the 9.3.64 shaders with 2.74.2 tooling (cell C). `[SOURCE]`

4.4.3. So a thryeditor 2.74 lock of a material on the 9.3.64 release shader reaches reconstruction. When the material was renamed or duplicated after that lock, the vendor unlock uses the tag and AMUSE uses the new name. Finding 1 then applies. `[INFERENCE]`

4.4.4. Class: **false-positive capable, unmeasured,** for 2.74 locks of 9.3.64 release originals. The route needs a rename after the lock and an edit of the suffixed field after the rename. A rename alone leaves the plain and suffixed values equal (4.1.4). K8 hides only locks whose original is a bundled row.

### Finding 5: a baked literal changed after the lock

4.5.1. A property with no tag is a literal in `L` (section 2). Reconstruction reads the saved entry. No AMUSE code compares a saved value with the baked literal. `[SOURCE]` `<optimizer-9.3.64>:1150-1220`, `Editor/Build/LockedMaterialReconstruction.cs:175-296`, `Editor/Build/TransientUnlockSwapIn.cs:285-348`.

4.5.2. Example. The shader reads `poiFragData.alpha = _AlphaForceOpaque ? 1 : poiFragData.alpha;` (`<shader-9.3>:30366`). AMUSE answers a constant 1 when `U` holds `_AlphaForceOpaque` = 1 (`Editor/Semantics/Poiyomi/PoiyomiAlphaInterpreter.cs:483-486`). If a script sets the saved value to 1 after a lock that baked 0, `U` proves opaque and `L` renders the real alpha. `_EnableUDIMDiscardOptions`, `_MainAlphaMaskMode`, `_EnableOutlines`, and `_AlphaMask_ST` give the same error. `[INFERENCE]`

4.5.3. The Thry inspector disables a property that has no tag on a locked material (`<poiyomi-9.3.64>/_PoiyomiShaders/Scripts/ThryEditor/Editor/EditorStructs/ShaderProperty.cs:330-331`). Scripts, the Unity Debug inspector, and other build tools are not blocked. `[SOURCE]` A material variant of a locked parent can also reach the value. That case is not measured.

4.5.4. An animation of a baked property does not make this worse. AMUSE admits a curve only when it equals the value of `U` (`Editor/Analysis/AnimatedPropertyAdmission.cs:20-30`). `L` ignores the curve. So the error is a refusal, a coverage loss. `[SOURCE]`

4.5.5. Earlier records name the mechanism for other paths but not for reconstruction. `docs/superpowers/investigations/2026-09-19-poiyomi-locked-materials-characterization.md` notes it and keeps an open question. `docs/superpowers/investigations/2026-09-19-poiyomi-locked-materials-paths.md` calls post-lock edits render-inert for one path. `[SOURCE]`

4.5.6. Class: **false-positive capable, through non-inspector writes.**

### Finding 6: structure fixed at lock time

4.6.1. The lock adds `PROP<NAME>` for each texture assigned at lock, before the tag check (`<optimizer-9.3.64>:1104-1114`). The alpha mask is sampled only under `#if defined(PROP_ALPHAMASK) || !defined(OPTIMIZER_ENABLED)`. Otherwise `alphaMask = 1` (`<shader-9.3>:29866-29870`). `[SOURCE]`

4.6.2. Example. `_AlphaMask` has tag `1` or `2` and is null at lock. Mode is Replace and Invert is on. Blend strength is 1 and blend offset is 0, the shader defaults (`<shader-9.3>:76-77`). `L` renders `1 - saturate(1)`, which is 0 (`<shader-9.3>:29866-29880`). After the lock, the user assigns a dark mask. The locked inspector allows this for a tagged texture (`<poiyomi-9.3.64>/_PoiyomiShaders/Scripts/ThryEditor/Editor/EditorStructs/ShaderProperty.cs:331`). `U` samples the mask and gives `1 - red`. AMUSE admits Replace with Invert only for these values (`Editor/Semantics/Poiyomi/PoiyomiAlphaInterpreter.cs:1094-1110`). It refuses Multiply with Invert (`:1104-1110`). The proof can say opaque while `L` renders nothing. `[INFERENCE]`

4.6.3. The same holds for an `//ifex` block without an `isNotAnimated` guard. `[MEASURED]` 233 of 1475 `//ifex` lines in the 9.3 shader carry the guard. For example, `//ifex _EnableDissolve==0` (`<shader-9.3>:4798`) has none. A tagged toggle that changes after the lock leaves the block removed in `L` and live in `U`. `[SOURCE]` `<optimizer-9.3.64>:1671-1683`, `<poiyomi-9.3.64>/_PoiyomiShaders/Scripts/ThryEditor/Editor/DefineableCondition.cs:345-357`. Whether an alpha input sits in an unguarded block needs a source scan (section 6, item 8).

4.6.4. Keywords give no new false positive. The defines and the `OriginalKeywords` tag come from one keyword set (`<optimizer-9.3.64>:1091-1095, 1597-1600`). AMUSE restores the set from the tag (`Editor/Build/LockedMaterialReconstruction.cs:128`). The Poiyomi proof reads no keywords. `[MEASURED]` A search of `Editor/Semantics/Poiyomi` for "keyword" found no match.

4.6.5. `_OutlineTexture` has the same `PROP_OUTLINETEXTURE` guard, with a white fallback (`<shader-9.3>:49136, 54412`). Whether it can cause a false positive is not analyzed. No earlier investigation covers texture presence after the lock. `[MEASURED]` A search of `docs/superpowers/investigations/` for `PROP_`, before this record existed, found no match. Two specs note the `PROP_` guard: `docs/superpowers/specs/2026-10-07-poiyomi-outline-alpha-conversion-design.md:74-77` and `docs/superpowers/specs/2026-08-19-semantic-adapter-characterization-design.md:360-363`. The second says its reasoning would invert if AMUSE supported locked materials.

4.6.6. Class: **false-positive capable, unmeasured.** The route is reachable through the vendor inspector for texture presence. Section 6 item 3 measures it.

### Finding 7: the original source can differ from the lock's source

4.7.1. Attestation hashes the original file as it is on disk at build time (`Editor/Build/LockedMaterialIdentity.cs:316-340`). The swap-in checks only that the generated locked shader path exists (`Editor/Build/TransientUnlockSwapIn.cs:341-345`). So attestation proves the source of `U`, not the source that generated `L`. `[SOURCE]`

4.7.2. 9.3.63 and 9.3.64 share GUIDs and differ only in the label line (`docs/superpowers/investigations/2026-10-23-poiyomi-bundled-9-3-source-identity-investigation.md`). That pair is harmless. Older minor versions use other names, which the name tag check refuses. An earlier 9.3 patch release with the same GUID and different alpha code would pass. That history is not measured. `[SOURCE]` `[INFERENCE]`

4.7.3. A property that the material never saved falls into the same route. `U` and `L` both take the shader default. They are equal only when the bytes are equal. `[INFERENCE]`

4.7.4. Class: **false-positive capable** if any 9.3 patch release with the pinned GUID changed the alpha code. Section 6 item 9 settles this.

### Finding 8: other output-fidelity effects

4.8.1. Retention. On the retention path, `U` ships wherever the inversion could not reach (section 2). Stale values, the lost queue and tags, and stripped textures then change the output there. `[SOURCE]` `Editor/Build/TransientUnlockWindowClose.cs:161-186, 400-505`.

4.8.2. Animations on `O`. A renamed property that the proof does not read resolves as irrelevant (`Editor/Host/AnimationBindingResolution.cs:164-167, 184`). So its slot can transform. `O` keeps the original shader, which does not declare `<plain>_<suffix>`. The animation still drives the remainder that keeps `L`. It no longer drives the moved triangles on `O`. `[SOURCE]` `[INFERENCE]`

4.8.3. Material property blocks. `CaptureBlockState` reads each slot's block against the property table of the slot material, which is `U` during capture (`Editor/Host/EffectiveMaterialMaterialization.cs:187-236, 302-335`, `Editor/Build/AmusePlatformFinishPass.cs:629-630`). Block values enter the proof in `Editor/Analysis/AnimatedPropertyAdmission.cs:60-100`. Texture keys enter it in `Editor/Build/AmusePlatformFinishPass.cs:931-964`. A key for a baked plain name reaches the proof, but `L` ignores it. A `<plain>_<suffix>` key is live on `L`, but `U` does not declare it. Both directions break the invariant. How often a build carries a renderer block is not measured. `[SOURCE]` `[INFERENCE]` Class: false-positive capable, frequency unknown.

4.8.4. Collateral renames in 9.3.64 (3.5). Output fidelity only for the alpha inputs as of 2026-10-23.

### Follow-up: mapping a suffixed binding to its plain property

4.9.1. K9 refuses `material.<plain>_<suffix>` bindings for proof-relevant names (`Editor/Host/AnimationBindingResolution.cs:280-330`, underscore check at `:324-325`). The refusal is coverage loss only. `[SOURCE]`

4.9.2. A reliable mapping needs the lock-time suffix, the declared table of the original shader, the `Animated` tags, and the exclusion rules of 3.4. Only the 2.74 tag records the lock-time suffix in a way that survives a rename. `[INFERENCE]`

4.9.3. Ambiguous cases that a mapping must refuse. `[INFERENCE]`

- A renamed 9.3.64 material with no tag.
- A shader that declares both a plain `<other>` and a renamed `<plain>` whose suffixed form equals `<other>`. For example `_HighColor_Power` beside `_HighColor` with suffix `Power`. The lock then gives two declarations of one name (`<optimizer-9.3.64>:1159-1161`).
- Duplicate-set names, `UV` and `Pan` names, and tag values other than `2`.
- Collateral renames in 9.3.64 (3.5).
- Different suffixes in different slots of one renderer.

4.9.4. If finding 1a holds, the `Animated` tags are absent on `L`. The mapping then has no tag input. `[INFERENCE]`

## 5. Summary

Class terms: a false-positive capable route can move triangles that `L` renders transparent. Unmeasured means that no fixture has run the route. Output fidelity means that the shipped look changes but no proof is wrong. Coverage loss means that AMUSE refuses work it could do.

| Item | Class as of 2026-10-23 | Reachable through the vendor inspector |
|---|---|---|
| Finding 1, wrong suffix | False-positive capable, unmeasured | Yes, if the tag survives and the suffixed field is edited |
| Finding 1a, absent tag | False-positive capable | No |
| Finding 2, queue, `RenderType`, `VRCFallback`, stripped textures | Output fidelity | Not applicable |
| Finding 3, duplicated names when the move runs | Unplanned divergence, fidelity-preserving when the move runs | Not applicable |
| Finding 4, unread 2.74 tag | False-positive capable, unmeasured, for 2.74 locks of 9.3.64 originals | Yes, after a rename and a suffixed edit |
| Finding 5, baked literal changed after lock | False-positive capable | No |
| Finding 6, texture presence fixed at lock | False-positive capable, unmeasured | Yes |
| Finding 7, source changed after lock | False-positive capable | Not applicable, needs vendor history |
| Finding 8, retention, animations on `O`, property blocks | Output fidelity, property blocks false-positive capable | Not applicable |
| K9 mapping | Coverage loss as of 2026-10-23, false-positive capable if it guesses | Not applicable |

Findings 1, 5, and 6 affect the released 9.3.64 locked path. Finding 4 needs a thryeditor 2.74 lock. Finding 7 is unproven. Findings 1, 4, and 6 are reachable through the vendor inspector if their stated preconditions hold. The tag must survive the lock (findings 1 and 4). The material must be renamed after a 2.74 lock (finding 4). A suffixed field or a tagged texture must be edited after the lock. These preconditions are not measured. Section 6 items 1 to 3 measure them. The false-positive capable items need a fix decision before more locked-path coverage work.

The common cause: the proof reads `U`, a reconstruction from the saved serialization of `L`. `L` renders from its generated locked shader plus its live properties. Reconstruction does not read the generated shader. `[INFERENCE]`

## 6. Evidence a fix needs

These need a project with the vendor package installed. They mutate materials, so they must not run in the Census Lab project.

1. Read the serialized tag map at four points: after the tag is set, after the lock, after the shader swap, and after a save and reload. This settles findings 1 and 1a.
2. Lock a material that has an underscore in its name and a tag-`2` alpha input. Edit the suffixed value. Compare the render of `L` with the proof outcome. This confirms finding 1 end to end.
3. Assign an `_AlphaMask` to a tagged, null mask after the lock. Compare as in item 2. This confirms finding 6.
4. Measure the render queue, `RenderType`, and `VRCFallback` across `Material.shader` assignment on Unity 2022.3. This settles 4.2.5.
5. Check that `new Material(U)` copies the queue, keywords, and override tags on Unity 2022.3. This decides which facts of `U` reach `O`.
6. Set an untagged alpha input by script after the lock. Compare the render of `L` with the proof outcome. This confirms finding 5.
7. Attach a renderer block with a baked plain key and with a `<plain>_<suffix>` key. Compare as in item 2. This confirms 4.8.3.

These need source reads only.

8. Scan the 9.3 shader for alpha inputs inside `//ifex` blocks without an `isNotAnimated` guard.
9. Read the vendor release history of 9.3 patch releases for GUID reuse and alpha code changes. This settles finding 7.

Vendor-free check options for a later design, not a design:

- Read the generated locked shader of `L`. Its declared names that the original does not declare are the exact `<plain>_<suffix>` names. This needs no tag and no material name.
- Read the baked literals and the `PROP` defines from the generated locked shader of `L`. Compare them with `U`. This covers findings 5 and 6.
- A move-equivalence check after reconstruction: every renamed plain value on `U` equals its suffixed saved entry on `L`.
- A render-state carry check: queue and override tags equal before and after the swap.
- A stripped-texture check against the `_stripped_tex_` tags.
- A conservative option: refuse every locked material until a fix lands. This trades coverage for safety.

Existing test seams: the restore delegate `TransientUnlockRestoreDelegate` (`Editor/Build/TransientUnlockAvailability.cs:24, 55-61`), the internal `MoveSuffixedSavedValues` and `ReadSavedProperties`, and the saved-entry fixture writers in `Tests/Editor/Build/LockedMaterialReconstructionTests.cs:205-315`. No test as of 2026-10-23 asserts that a locked fixture's saved values reach `O`. `[SOURCE]`

## Amendment log

- 2026-10-23: first revision, integrated from three parallel read-only slices.
- 2026-10-23: second revision, integrated three parallel adversarial reviews. Added findings 5 to 8. Corrected section 2, the 1a analysis, the K8 masking claim, the stripped-texture reason, and cites.
- 2026-10-23: third revision, integrated a scoped re-review. Relabeled the unmeasured false-positive classes, finding 3, and the summary claims. Added evidence items 6 and 7, defined terms, and fixed cites.
- 2026-10-24: The locked source alignment design answers the open questions and the vendor-free check options of this record. See docs/superpowers/specs/2026-10-23-locked-source-alignment-design.md.
