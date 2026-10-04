# AAO Optimize Texture atlases and the in-build source-identity refusal

Privacy note: This record uses one private avatar in the Census Lab project as observed build input. It names no avatar, renderer, material, texture, animation clip, or asset path. Renderers appear by role only and slots by ordinal. It writes no instance names, hashes, ports, or machine paths. Counts are observed counts from the session on 2026-10-04.

Date: 2026-10-04.
Branch: `fix/aao-optimize-texture-analysis`.
Base: `main` at `09f1f3c`.
Investigation status: no production code changed. The work was read-only source inspection, git history reconstruction, and one read-only console read from the pinned Census Lab editor instance after the project identity check passed.

## Question

The Census Lab build reports that AMUSE proved nothing for three material slots, and the report detail reads as if the lilToon `_MainTex` property is unsupported. AMUSE does support lilToon `_MainTex` alpha semantics. This record answers three questions. Why can the current capture not read the textures that the Optimize Texture setting of Avatar Optimizer (AAO) generates? Why did the two earlier support attempts not fix it? What shape would an in-AMUSE fix need, in the style of the LAC admission?

## Method

Three read-only scouts mapped the current AMUSE gates and reporting, the git history of the earlier attempts, and the installed AAO source. Both editor instances were enumerated first. The Census Lab instance matched the Census Lab project, and it was pinned for the console read. The dev project and the Census Lab project both install AAO 1.9.17. Three load-bearing code sites were then re-read by hand from this repository. No build was run and no test was run in this session.

## The observed refusal

The console of the Census Lab instance holds the AMUSE reports of the last completed build on 2026-10-04. Three slots on three renderers of the corpus avatar refused. Each refusal reads `AMUSE proved nothing for material slot N of renderer R. The slot holds M.` One further report reads `AMUSE could not prove any triangle on this renderer`, and the closing report reads `AMUSE finished this avatar`. The semantic barrier pass ran in 17136 ms and the apply pass ran in 55 ms on that build.

Each refused slot also carries a texture-level entry. Its detail names the property `_MainTex`, the channel, the reason `UnavailableCapture`, and the clause `the texture has no source identity`. The attached hint already names the texture-atlas setting of the Trace and Optimize component of Avatar Optimizer as the known producer of that shape. The refused slots are therefore read here as AAO atlas slots. This rests on the pinned producer identification of 2026-09-27, the report hint, and the installed version. The slot textures were not fingerprinted again in this session.

## The producer shape today

Source read of the installed AAO 1.9.17 in the dev project:

- All AAO work runs in the NDMF Optimizing phase. `DupliacteAssets` clones every renderer material early and registers each pair in the NDMF object registry (`Editor/Processors/DupliacteAssets.cs:91-94`).
- `T&O: OptimizeTexture` runs late in that chain (`Editor/OptimizerPlugin.cs:130`). It mutates the already cloned materials in place through `SetTexture` (`Editor/Processors/TraceAndOptimize/OptimizeTexture.cs:470-472`).
- Each atlas is a plain in-memory `Texture2D`. The package contains no asset-saver call at all. The name is the source name plus `" (AAO UV Packed)"`, or `"AAO Monotone <color> <space>"` for a collapsed solid color (`OptimizeTexture.cs:971`, `:956-964`).
- A compressed source keeps its exact format through a block copy (`:886-917`). Any other source goes through a render-texture readback and is recompressed into the source format (`:928-963`, `:970`). Wrap, filter, anisotropy, mip bias, and the streaming mipmap flags are copied from the source (`:918-924`, `:972-977`). The mip count is the smaller of the size-derived count and the source count.
- Atlas eligibility requires an identity UV matrix, so only materials whose `_MainTex_ST` and scroll are identity are atlased (`:433-435`). Mesh UVs are rewritten into the atlas (`:486-536`). Only skinned-mesh submeshes participate (`:307`).
- The atlas texture and the `SetTexture` rewrite are never registered in the object registry. The file has no registry reference at all. The earlier registered pair covers only the material clone, not its textures.

NDMF 1.14.4 persists in-memory build textures only in `Finish()`, after the last phase. So at the AMUSE PlatformFinish capture the atlas is still a path-less in-memory object.

## The refusing chain in AMUSE today

The capture entry is `UnityAlphaFieldEvidence.TryCapture` with a fixed gate order (`Host/UnityAlphaFieldEvidence.cs:149-358`). The third gate resolves a stable source identity through `UnityTextureEvidence.TryGetSourceId` (`:185-189`).

`TryGetSourceId` refuses every texture without an asset path, with one exception: a `Texture2D` admitted by `ReplacementTextureAttestation.TryIdentifyReplacement`, which is the LAC-only in-memory admission (`Semantics/UnityTextureEvidence.cs:39-48`). The AAO admission lives elsewhere, in `GeneratedTextureAttestation.TryIdentifyProducer`, and that predicate requires an asset path, a sub-asset, and an NDMF `SubAssetContainer` main asset before it even checks the AAO name markers (`Semantics/GeneratedTextureAttestation.cs:47-73`). An in-memory atlas fails at the path test. So the in-build AAO atlas refuses at the identity gate with `UnavailableCapture`, and the refusal is pinned by the shipped test `TryGetSourceId_SceneOnlyTexture_IsRefused` and by `GeneratedTextureAttestationTests.LooseInMemoryTexture_IsRefused`, which refuses a path-less texture that carries the exact AAO name marker.

The downstream chain then degrades exactly as observed. The capture refusal is recorded beside the slot. The resolver answers `MissingTextureEvidence`. The material-scoped refusal invalidates only that material and that slot. No triangle is proven, the slot result carries `AdmittedMaterialSemanticsUnknown`, and the slot-level `AMUSE proved nothing` report follows. The affected triangles keep their original material, and the blast radius stays inside that slot.

## Why the report reads as a `_MainTex` support refusal

No report string contains the literal `_MainTex`. The property name arrives as a format argument, because the lilToon frontends request their main texture evidence under `_MainTex` (`LilToonCutoutMaterialSemantics.cs:191-202`, `LilToonTransparentMaterialSemantics.cs:224-232`). The detail sentence then reads `The capture of texture property _MainTex ... refused ... the texture has no source identity` (`AmuseReportStrings.cs:309-314`). A reader concludes that `_MainTex` is unsupported. That conclusion is wrong. The lilToon semantics fully support `_MainTex`: the declared-default arm for an unassigned main texture, the exact-identity `_MainTex_ST` rule, the importer theorem, and the alpha-mask composition all exist in the frontends. The failing fact is the identity of the texture object, not the semantics of the property. The console one-liner does not carry this distinction. The hint that names the real cause sits on the texture-level entry only.

## Why the earlier attempts did not fix it

- Attempt one, 2026-09-07, the generated-texture evidence contract. It admitted the AAO producer, but under the premise that each atlas arrives as a serialized sub-asset of an NDMF container. That premise holds only for a re-analysis of persisted build output. It does not hold inside a live build, because NDMF persists in-memory textures only after the last phase. The shipped AAO branch therefore never fires at PlatformFinish. Its soundness fallout on 2026-09-11, the merged-renderer unbound-evidence repair, was correct and stays.
- Attempt two, 2026-09-26 through 09-29, refusal kept and reporting hardened. The 2026-09-27 record named the atlas pass as the producer, recommended keeping the refusal, and asked the AAO project upstream to register atlas pairs. The shipped result is the hint text that this session still sees in the Lab console.
- The LAC success on 2026-10-03 and 2026-10-04 built the missing machinery: a version-pinned producer admission for in-memory copies, a minted `unity-replacement:` identity, and a generated capture route that runs before the streaming route. Its conjuncts require a registry pair from copy to source, and AAO 1.9.17 registers nothing for atlases. The recipe therefore does not transfer unchanged.

## The gap between the AAO shape and the LAC recipe

| Fact | LAC copy | AAO atlas 1.9.17 |
|---|---|---|
| Asset path at capture | none | none |
| Name marker | `_compressed` suffix | `" (AAO UV Packed)"` suffix or `"AAO Monotone "` prefix |
| Registry pair | copy to source, one hop | none for textures; the slot material resolves one hop to a project material through the `DupliacteAssets` pair |
| Relation to source | re-encoded copy of one texture | transformed domain: pixels and UVs moved, one atlas may cover several sources |
| Producer version seam | installed, admitted set `0.9.0` | none |
| Streaming flag | forced on | copied from source, so usually off |

Two further gates will still refuse some atlases after any identity admission. The format allowlist admits RGBA32, ARGB32, Alpha8, RGB24, DXT1, DXT5, and BC7, so a BC5 or ASTC source format refuses with `UnsupportedFormat`. The mip-limit gate refuses when the atlas carries more levels than the active limit. A mirrored streaming flag is harmless, because the generated route runs before the streaming route.

## Support options, none implemented

1. A version-pinned in-memory AAO admission, in the LAC style: path-less `Texture2D`, the pinned name markers, an installed AAO package under an admitted version set, and the slot material resolving one hop through the registry to a project material as producer corroboration. The capture then reads the live atlas pixels through the generated route under a per-build minted identity. This is the requested bulldoze. Its honest weakness is that a name marker is weaker evidence than the LAC registry pair, so the corroboration conjunct and a dated characterization must carry the trust.
2. A per-build transient identity for unsaved build objects. This was deferred on 2026-09-27 as a new dated spec rather than a fix. Option one is its narrow AAO form.
3. The upstream ask of 2026-09-27, registration of atlas pairs. This stays recorded in code comments and is out of scope for this branch.
4. Report-only improvement of the `_MainTex` phrasing. Useful, small, and no replacement for option one.

## Next step: the feedback loop

Before any production edit, the fix branch needs a red-capable seam test that builds the exact in-memory atlas shape without installing AAO: a path-less `Texture2D` with the pinned name marker, a mirrored format and streaming flag, and a slot material registered one hop to a project material. The test runs the real `TryGetSourceId` and capture gates and asserts the refusal. It must go red only under the dated admission spec, and the existing pins `TryGetSourceId_SceneOnlyTexture_IsRefused` and `LooseInMemoryTexture_IsRefused` must be retired by that spec, never weakened silently. A falsifier set follows the LAC pattern: foreign producer with the same marker, uncharacterized version, registered-chain mismatch, and format and mip refusal shapes.

## Scope and limits

No production code changed and no test ran. The producer facts are pinned to AAO 1.9.17, which both projects install. The AAO lilToon shader-information gate supports lilToon up to `versionValue` 45, so a newer lilToon would stop atlasing rather than change this shape. The console evidence is the last completed Lab build on 2026-10-04. The refused slot textures were not fingerprinted in this session, so the producer attribution carries the 2026-09-27 pinned record plus the report hint as its basis.

## Sources

- `Packages/com.alrauna.amuse/Editor/Host/UnityAlphaFieldEvidence.cs:149-358` — capture gate order.
- `Packages/com.alrauna.amuse/Editor/Semantics/UnityTextureEvidence.cs:39-73` — identity gate and its one in-memory exception.
- `Packages/com.alrauna.amuse/Editor/Semantics/GeneratedTextureAttestation.cs:37-108` — producer allowlist conjuncts.
- `Packages/com.alrauna.amuse/Editor/Semantics/ReplacementTextureAttestation.cs` — the LAC admission recipe.
- `Packages/com.alrauna.amuse/Editor/Build/AmuseReportStrings.cs:306-364` — refusal strings and the atlas hint.
- `Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonCutoutMaterialSemantics.cs:191-212`, `LilToonTransparentMaterialSemantics.cs:219-232` — `_MainTex` evidence requests.
- `Packages/com.anatawa12.avatar-optimizer/Editor/Processors/TraceAndOptimize/OptimizeTexture.cs`, `Editor/Processors/DupliacteAssets.cs:91-94`, `Editor/OptimizerPlugin.cs:88-133` — installed AAO 1.9.17 source.
- Prior records: `2026-09-07-generated-texture-evidence-contract-design.md`, `2026-09-26-maintex-refusal-investigation.md`, `2026-09-27-maintex-build-copy-clone-investigation.md`, `2026-10-02-lac-texture-compressor-investigation.md`, `2026-10-03-lac-generated-texture-attestation-design.md`, `2026-10-04-lac-persisted-copy-admission-design.md`.
- Census Lab console, read-only through the pinned instance on 2026-10-04: three slot refusals, one renderer-level refusal, one closing entry, observed pass times.
