# Poiyomi locked materials - characterization

## Status and scope

Investigation record. Dated 2026-09-19. Branch `investigate/poiyomi-locked-materials`, based on `main` at `a3f9dca`. This note makes no production change and writes no test. The companion note `2026-09-19-poiyomi-locked-materials-paths.md` records six solution paths and a recommendation.

Question. What is a locked Poiyomi material? How does it move through the Unity, VRChat SDK, and NDMF build pipeline? What can AMUSE read from one?

Privacy note. No private avatar data and no Census Lab data were used. All vendor evidence comes from public release archives at pinned versions. The work used no live editor instance.

Method. Three researchers worked in parallel on 2026-09-19. Two downloaded vendor archives and verified digests. One mapped this repository. Three independent reviewers then re-verified the load-bearing claims from fresh downloads and from the working tree. Review surfaced corrections. They appear in section 9.

## 1. Pins

| Source | Version | Archive SHA-256 |
|---|---|---|
| `com.poiyomi.toon` | 9.3.64, the AMUSE pin | `42217aa158ea685b8c0f3d9599229aca4a0d5b72ef46a980d8a76f70f0b5a7f6` |
| `com.poiyomi.toon` | 10.0.22, newest release | `82642842af80a3aff5a979faf39be8020d446c7caa9921a82236f67c2dac56b3` |
| `com.poiyomi.thryeditor` | 2.74.2, newest | `3dceff1432cec7348ffa07d5737fc4936a344cf063ef0c84c1af1cd484435dc3` |
| `com.poiyomi.thryeditor` | 2.73.9 | `a47ff221450b5958d949a26d6901680bccf15efd655b20e227aaadea12fad599` |
| `nadena.dev.ndmf` | 1.14.8 | `e1103ea150b9f1d49d415d632ed4da4e46cd8121a1f1f7ea917b155193a1ab83` |
| `d4rkAvatarOptimizer` | 4.6.0 | `8a5e7b1f1732e84ee60b21dc3eebc8c6b76206493ab203627b21808c37d68076` |
| `com.anatawa12.avatar-optimizer` | 1.9.19 | `27647368a021598abd32e62a67410fbe28121786a6338807504f647d02428206` |
| `com.vrcfury.vrcfury` | 1.1429.0 | `82d498b9079b8bfbf37856cae24e6542cca8881de588b1a96fa0dc8a66437295` |

The VPM listing at `https://poiyomi.github.io/vpm/index.json` publishes the Poiyomi digests. Independent downloads on 2026-09-19 reproduced every Poiyomi digest at least twice. `com.poiyomi.toon` 10.0.22 exists on GitHub and declares a VPM dependency on `com.poiyomi.thryeditor >= 2.74.0`. The listing still served 9.3.64 as the newest toon release on 2026-09-19.

Upstream `Thryrallo/ThryEditor` master at `28ecf9d` (2026-02-12) has none of the cache, recovery, or scanner files. The `poiyomi/ThryEditor` fork at `107c5d7` matches the 2.74.2 archive in those files. The fork is the living line.

Shorthand. T64 is the ThryEditor tree embedded in `com.poiyomi.toon` 9.3.64. T2742 and T2739 are `com.poiyomi.thryeditor` 2.74.2 and 2.73.9. The lock code lives in `Editor/ShaderOptimizer.cs` in all three, plus `Editor/LockedShaderCache.cs`, `Editor/LockedShaderRecovery.cs`, and `Editor/MaterialLockScanner.cs` from 2.73.9 onward.

## 2. The lock is a reversible source transform

The Thry ShaderOptimizer lock rewrites the shader source and rebinds the material to the generated shader. No encryption exists anywhere in the path. A search across the T2742 editor tree and the whole T64 tree found no encryption primitive. The only cryptographic primitive in the lock path is an MD5 hash that names cache entries.

The lock is therefore a distribution control, not a cryptographic one. The vendor lock toggle remains the vendor's own user-facing workflow. This note treats the mechanism neutrally.

## 3. Locked material anatomy

The lock never clears property values. `LockApplyShader` edits only texture-environment entries in the material serialization (T2742 `ShaderOptimizer.cs:1928-1953`).

- It deletes entries for stripped textures and entries whose property the generated shader no longer declares.
- 9.3.64 additionally deleted every entry whose texture was null. A 9.3.64-era locked material therefore lost the tiling and offset of empty texture slots.

All floats, colors, and vectors stay. Every original property name and value remains readable on the material component. Each stripped texture GUID is stashed in an override tag named `_stripped_tex_<property>`.

Render state stays live where it matters. The Poiyomi source binds `Blend [_SrcBlend] [_DstBlend]`, `BlendOp [_BlendOp]`, `ZWrite [_ZWrite]`, and `Offset` to properties (`Poiyomi Toon.shader:9940-9950`, Two Pass form at `10057-10067`). The optimizer folds only `Cull` and `ColorMask` to literals (T2742 `ShaderOptimizer.cs:1777-1806`). So `_SrcBlend`, `_DstBlend`, `_BlendOp`, `_ZWrite`, and `_Offset*` remain live material values on a locked material.

Override tags written at lock time:

- `OriginalShader`, the original shader name, and `OriginalShaderGUID`, its asset GUID (T2742 `:1910-1912`).
- `thry_locked_rename_suffix` (T2742 `:1913-1915`, absent from T64).
- `OriginalKeywords`, the pre-lock keyword list (T2742 `:1974`).
- `AllLockedGUIDS`, a reference count (T2742 `:1026`).
- `_stripped_tex_<property>` per stripped texture.
- One `<property>Animated` tag per property, with values empty, `1`, or `2`.

Thry reads these tags back with no validation. Its fallback shader resolution guesses by edit distance and can pick a wrong shader (T2742 `:2805-2836`). The same tag surface is what d4rkAvatarOptimizer and VRCFury already read.

## 4. Generated locked shader

The generated shader keeps the full `Properties` block with original names. The rewrite only renames definitions of properties whose `<property>Animated` tag equals `2` (T2742 `:1648-1662`). A fixed illegal-rename list (`_MainTex`, `_Color`, `_Cutoff`, `_SrcBlend`, `_DstBlend`, `_ZWrite`, and more, T2742 `:281-302`) is duplicated instead. The original declaration stays for Unity plumbing and the HLSL references move to the suffixed name. Names ending in `UV` or `Pan` never rename. So `_MainTex` and `_Color` presence checks pass on a locked shader schema.

The transform bakes every untagged property value into the program body as a literal at each use site (T2742 `:2741-2761`, T64 `:2207-2213`). Floats print in invariant culture. Colors and vectors print as `float4(x,y,z,w)`. The lock injects material keywords as `#define` lines plus `OPTIMIZER_ENABLED`, then clears the material keywords. Disabled `shader_feature` branches are pruned from the source. Non-Unity includes are inlined with `[Inlined]` markers. The transform is a pure text function of the material values, keywords, tags, suffix, shader bytes, optimizer version, and color space. Review judged it deterministic for identical inputs.

Naming has two eras.

- 9.3.64: `Hidden/Locked/<originalName>/<materialGUID>` with an optional local file id tail. The asset sits beside the material under `OptimizedShaders/`.
- 2.73.9 and later: `Hidden/Locked/<originalName>/<contentMD5>`, content-addressed and stable across materials. The asset lives under `Assets/_LockedShaderCache/<...>` with a `.thrylock` sidecar (T2742 `LockedShaderCache.cs:25-51`).

The generated shader is a normal imported project asset. It ships in the avatar bundle through the material reference. The cache folder is a persistent store, not a build exclusion.

The original name is itself multi-segment. A real locked name reads `Hidden/Locked/.poiyomi/Poiyomi Toon/<id>`. Any parser must treat everything between the prefix and the final id segment as the original name.

## 5. Animation after lock

The lock classifies each property by its `<property>Animated` override tag.

- Empty or absent: the value is baked into the source as a literal. A clip that binds the property is a post-lock no-op.
- `1`: the property stays animatable under its original name. Existing clips keep working.
- `2`: the property is renamed everywhere to `<property>_<suffix>`. The suffix comes from the `thry_rename_suffix` tag, or falls back to the cleaned material name (T2742 `:752-755`). The Thry inspector and VRCFury author the already-suffixed binding when they keyframe such a property. Pre-existing clips that bind the plain name silently stop affecting rendering after a lock.

The lock never rewrites animation clips. It only reads them for material collection (T2742 `:3148-3181`).

## 6. Build pipeline position

ThryEditor registers four build callbacks.

1. `LockMaterialsOnUpload`, `IVRCSDKPreprocessAvatarCallback`, order 100. It locks every still-unlocked Thry material on the avatar, collected from renderers, descriptor animation clips, and, from 2.74.2, any Animator component under the avatar root (T2742 `:3208-3234`).
2. `LockMaterialsOnWorldUpload`, `IVRCSDKBuildRequestedCallback`, order 100, for world builds.
3. `StripUnlockedShadersFromBuild`, `IPreprocessShaders`, order 4. It clears the variant data of any shader that declares both `shader_is_using_thry_editor` and `_ShaderOptimizerEnabled` and whose lock-button property default is not 1 (T2742 `:3320-3343`, `:3437-3456`).
4. `VRCAutoAnchor`, order 0.

NDMF 1.14.8 registers its hooks at -11000 (First through Transforming) and -1025 (Optimizing through PlatformFinish). AMUSE runs in PlatformFinish inside the -1025 hook. The lock at order 100 runs after every NDMF phase. AMUSE therefore only ever sees materials that were locked before the build. Upload-time locking is invisible to AMUSE.

Two pipeline facts are load-bearing for any generated AMUSE shader.

- The stripper's locked test keys on the lock-button property default, never on the name. A `Hidden/Locked/` name alone confers no protection. A shader that declares both Thry marker properties with a non-1 default loses all variants in the build.
- A shader that declares neither marker property is never examined by the stripper.

A field failure report, Poiyomi issue 64, records upload-time locks silently not happening for some users, with no public root cause on 2026-09-19.

## 7. Unlock and restore

Unlock ships in every pin (T64 `:453`, T2742 `:2840-2946`). Restore brings back keywords, stripped textures, and renamed-property values from the tags and the generated source. It preserves render type and queue. Thry deletes the locked shader only when no other material uses it.

From 2.73.9, `LockedShaderRecovery.cs` auto-unlocks orphaned locked materials whose generated shader was deleted. `MaterialLockScanner.cs` shows per-material lock state as Unlocked, Locked, or Orphaned.

One known restore imperfection. A stripped texture's tiling and offset are not restored, because the texenv entry was deleted wholesale (T2742 `:2903-2912`).

VRCFury locks materials itself by calling Thry's `SetLockedForAllMaterials` through reflection, and asserts the `Hidden/Locked/` name afterwards. So a programmatic lock surface exists with ecosystem precedent.

## 8. Ecosystem handling

| Tool | Revision | Detection | Handling |
|---|---|---|---|
| d4rkAvatarOptimizer | 4.6.0 | Property presence and value only (`_ShaderOptimizer`, `_ShaderOptimizerEnabled`, `__Baked` equal to 1) | Never skips locked materials. Re-optimizes locked shaders like ordinary HLSL. Rename-animated unsupported, with a documented warning to lock first with Poiyomi's own method |
| anatawa12 AvatarOptimizer | 1.9.19 | None. Only UDIM-discard property names name Poiyomi | Unknown shaders skip texture optimization and property cleanup and refuse material merges |
| VRCFury | 1.1429.0 | `Hidden/Locked/` name prefix, Thry tags, reflected Thry API | Locks materials before cloning, writes suffixed animation bindings, registers generated material GUIDs with Thry, refuses TPS auto-config on locked materials |
| NDMF | 1.14.8 | None | Agnostic. Both hooks run before the order-100 lock |

Relevant field issues on 2026-09-19: AAO issue 1383 records the upload lock landing on AAO build duplicates, by design unfixable upstream. d4rk issues 176 and 182 record rename-animated gaps. d4rk issue 24 records a rejected auto-unlock request. Poiyomi issue 43 fixed the stripper stripping non-Thry shaders. VRCFury documents an `AssetDatabase.Refresh` hazard inside Thry's lock that can roll back not-yet-serialized clips mid-build.

## 9. AMUSE behavior on 2026-09-19, and corrections

A pre-locked material fails family selection first. `ClassifyShaderName` in `Packages/com.alrauna.amuse/Editor/Semantics/UnityMaterialSemantics.cs` matches exact names only. A `Hidden/Locked/` name returns the Unsupported family. The material becomes the `UnattestedMaterial` sentinel, stays admitted, joins no capture batch, and every slot whose admitted set holds it resolves to `RendererAnalysisRefusal.AdmittedMaterialSemanticsUnknown`. The renderer refuses only when no slot resolves (`AmusePlatformFinishPlugin.cs:1047-1087` on the work branch).

A locked material never enters the name-classified capturer batch. The all-or-nothing closure gate with `MaterialDependencyClosureFailed` is therefore not the gate locked materials hit. Locked materials take the per-slot sentinel path.

Facts AMUSE can already use: every alpha-request property name and value survives the lock on the serialization. The GPU texture-evidence route needs no CPU readability and satisfies locked materials' texture references unchanged. `material.GetTag` works regardless of lock state. The capture record has no override-tag and no keyword dimension on 2026-09-19, and adding one is additive. The S10 Two Pass precedent admits a generated vendor identity by fixed digest, but no fixed digest can exist for per-material locked sources.

Corrections to the 2026-09-04 note `2026-09-04-poiyomi-lock-timing.md`:

1. The section 4 sentence that one pre-locked material fails the whole renderer is stale. Current code refuses exactly the slots that can hold the material and refuses the renderer only when nothing resolves.
2. The stripper's locked test keys on the lock-button property default in both eras. The 9.3.64 name-prefix wording survives only as a stale code comment.
3. T64 locks additionally deleted null-texture texenv entries. Retention claims must be era-qualified.

## 10. Open questions on 2026-09-19

- Whether Thry's inspector blocks post-lock edits of baked properties. This affects divergence frequency only.
- Whether the cache garbage collector can delete a locked shader asset mid-build while a tool reads it.
- The exact VPM distribution path for Poiyomi 10.x, and whether 9.3.x-era `OptimizedShaders` folders migrate to the cache on upgrade.
- Live editor observations of a lock and an unlock round trip. All findings above are static source reads, per the source-first method decision of 2026-09-19.

## 11. Sources

- Vendor archives and digests in section 1, fetched from `https://poiyomi.github.io/vpm/index.json` and the GitHub release pages.
- `https://www.poiyomi.com/general/locking`, fetched 2026-09-19. The page matches code on the lock and unlock workflow. Its claim that blend ops and render queue are non-animatable reflects inspector behavior, not the mechanism.
- Issue links: `github.com/anatawa12/AvatarOptimizer/issues/1383`, `github.com/d4rkc0d3r/d4rkAvatarOptimizer/issues/176`, `.../issues/182`, `.../issues/24`, `github.com/poiyomi/PoiyomiToonShader/issues/43`, `.../issues/64`.
- Repository files cited in section 9 at the work branch.
- The prior note `2026-09-04-poiyomi-lock-timing.md`, sections 3 to 6.
