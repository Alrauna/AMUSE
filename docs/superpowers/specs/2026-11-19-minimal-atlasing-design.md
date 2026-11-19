# Minimal atlasing for AMUSE 0.2.0

Privacy note: This record contains product design only. It contains no private avatar observations, machine identifiers, or vendor source excerpts.

Date: 2026-11-19.
Status on 2026-11-19: This design controls the 0.2.0 atlasing scope. It replaces `docs/superpowers/specs/2026-11-16-merge-ready-atlasing-design.md` for 0.2.0. The older design stays as a historical record. Its decisions about deferred features stay valid input for later versions. The user approved this design on 2026-11-19. Each milestone still needs an approved executable plan before implementation.

Source of the change: A minimalism review on 2026-11-19 found one root cause of the complexity in the older design. That design ran the atlas first and then ran the alpha proof on pages that AMUSE made. That made a loop between atlasing and alpha separation. The loop needed a separate candidate, a shared fallback, an evidence identity for AMUSE outputs, a split of classification from preparation, and cache eviction. The user accepted the order change and the deferrals of the review on 2026-11-19. Section 3 records the answers.

## 1. Purpose

0.2.0 builds the atlas infrastructure. It is not the full atlas feature set. It must prove four things on real vendor shaders:

1. AMUSE can move the texture regions of compatible materials into shared pages without wrong rendering.
2. AMUSE can rewrite mesh UVs without loss of any other mesh data.
3. AMUSE can state, per page set, that one shared material configuration serves all its uses. This is the merge-ready check that 0.3.0 material combining needs.
4. AMUSE keeps the shipped alpha separation result on every use.

Atlasing alone does not reduce material slots or draw calls. Memory can grow. The report says so. No memory-savings gate applies to each page set.

## 2. Pipeline position

Atlasing is one new pass. It runs after the five existing passes, after `TransientUnlockWindowClose`. It runs without an NDMF extension.

The dependency goes one way. The atlas pass reads the avatar that alpha separation left. Alpha separation knows nothing about atlasing. The atlas pass reads only these values from `AmusePlatformFinishState`: the lifecycle result, the structural graph result, and the list of AMUSE-owned objects. It never reads `PreparedAlphaSeparation`.

Why this order is safe:

- Alpha separation proves opacity on source evidence, as in 0.1.0. The source check of 2026-11-19 shows that every opaque output that alpha separation can make sets output alpha to one in every pass. So an atlas page cannot make the opaque proof wrong. Alpha separation refuses to convert Poiyomi Two Pass, so no Two Pass opaque output exists in 0.2.0.
- Transparent and cutout slots keep alpha in their pages. Mip filtering and compression can change their transparency. The user accepted that change for ordinary atlasing on 2026-11-16 (answer 52).
- An opaque clone and a transparent slot have different render state. So they fall into different groups and get separate pages with no extra rule.
- No pass after the atlas pass rebinds animation state. So the atlas write is the final word.

Data flow: the live avatar after alpha separation, then captured evidence per use, then pure grouping and placement, then owned pages, mesh, and materials, then one write after an identity check, then report lines.

## 3. User decisions on 2026-11-19

- Order: Alpha separation runs first. Atlasing runs afterward. This replaces answers 52 and 53 of 2026-11-16 for 0.2.0. No alpha proof runs on an AMUSE page.
- Scope: Both shader families stay in 0.2.0. The features in section 12 move to later versions.
- Presets: Atlas fields live on the component only in 0.2.0 development. Preset files stay at schema five. The user expects preset atlas keys to come later (2026-11-19). They wait until pre-release work fixes the atlas fields, so that the preset schema changes only once.
- Play mode: Atlasing runs in Play mode under the same gate as alpha separation, `HostLifecycleCapability.MayUsePositiveMutation`.

Decisions that stay valid from the older design:

- Atlasing is on by default. Unset fields take their defaults through field initializers and Reset. An explicit false stays false.
- Alpha separation and atlasing have independent enable switches. The shared prerequisites run when at least one feature is on. These are the lifecycle gate, the structural animation graph, and the consent dialog. When both switches are off, nothing is analyzed, mutated, or reported. One consent dialog serves both features (user decision, 2026-11-19). AMUSE adds no second dialog. The dialog asks at most once per build. When only atlasing is on, it lists only the unverified host versions. A decline keeps both features idle.
- Atlas uses admit only exactly attested shader rows. Version consent does not extend them.
- Atlasing runs only on the Windows build target in 0.2.0. Other targets keep every source use and report one named refusal.
- A bounded UV-rounding error is permitted (2026-11-18). It does not permit contamination from neighboring regions.
- Anisotropic tap differences follow the accepted compression risk (2026-11-19). The border still covers the anisotropic footprint.
- Compression can cause visible shading changes even when a texture passes its error limit (2026-11-17). Fixtures qualify each recipe. Each generated page gets an output check. AMUSE does not render each avatar.
- Never subdivide triangles. Never change source assets. Never disable a shader feature to make a use fit.

Decisions that this design makes moot:

- Section 17.2 item 11 of the older design. The atlas pass never removes the work of alpha separation. A refused atlas use keeps the alpha separation output that it had.
- The relock admission of section 17.4 of the older design. Locked sources and slots from the transient unlock window are refused in 0.2.0. The live relock check stays as a regression fixture for alpha separation outputs.
- The per-platform anisotropy settings for alpha. The atlas pass makes no alpha proof.

## 4. Use and admission

A use is one material slot of one renderer. The atlas pass admits a use only when every condition below holds. Otherwise the use keeps its current bindings and gets one named refusal.

1. The atlas switch is on and the lifecycle gate permits positive mutation.
2. The build target is Windows. Reuse the existing target check in `Build/PlatformAlphaSamplingPolicy.cs`. Keep target constants together beside it.
3. The slot has one static material. No clip swaps the material of that slot. Reuse `CapturedAnimationEvidence` and `AnimatedPropertyAdmission`.
4. The material uses an exactly attested shader row with a route table (section 5). The shader is not locked.
5. No property block entry and no animated property targets a route property: a member texture, its transform, or the UV selector. A renderer-wide entry refuses every use on that renderer.
6. Every member texture is an asset on disk with an import setting that the acquisition supports.
7. Every member has identity tiling and offset and no shader-intrinsic motion.
8. Every UV value of the triangles of the use on the route channel lies inside the unit square.
9. The material state meets the feature conditions of the row (section 5). Every assigned texture is on the member list or the neutral list.

Refusals are one closed enum, `AtlasUseRefusal`, next to `AlphaSeparationSlotRefusal`. Expected outcomes fall into three kinds: unsupported input, a reached limit, and a missing host capability. Programming and invariant failures throw. A generated page whose format, size, or mip count differs from the request is an invariant failure.

## 5. Route table

Each attested shader row has one route table. 0.2.0 has one route per row: the main color texture on UV0. The table has three parts:

- Members: the textures that move to the page. In 0.2.0 this is `_MainTex` only.
- Neutral: assigned texture properties that no reachable code samples through the route channel.
- Feature conditions: for Poiyomi, an allowed list of local feature keywords plus required property values. For lilToon, required property values only. lilToon gates its features with uniform switches.

Rule: An assigned texture that is on neither list refuses the use. A feature keyword outside the allowed list refuses the use. A required value that does not hold refuses the use. No clip and no property block may target a condition property. An allowed list is complete by construction, so a feature that the review missed cannot pass. This rule replaces the separate UV-consumer, channel-closure, page-size-reader, and per-pass rules of the older design. A pinned shader digest cannot gain an unlisted feature.

The tables come from the source review of 2026-11-19: `docs/superpowers/investigations/2026-11-19-atlasing-route-tables.md`. Each entry gets one falsifier. The Poiyomi rows are the 9.3 rows. Features that exist only in the 10.0 shader do not apply. lilToon Multi has no table in 0.2.0 and refuses with a name.

## 6. Group key and merge-ready check

Two uses join one group when their group keys are equal. The key holds:

- The shader row and the route.
- The graphics format of the member sources, including its sRGB or linear form. The page uses that format (section 8).
- The sampler facts of the route: filter mode, wrap mode per axis, mip bias, anisotropy level, and the inline sampler when the shader names one. Most vendor textures borrow the main sampler. So the page sampler must equal the sampler that those textures expect.
- Every non-texture material fact, built by enumeration of all shader properties, plus keywords, override tags, render queue, disabled passes, instancing, and global illumination flags. Enumeration means that no fact can be forgotten. A stricter key only makes more groups. That is safe.
- The identity of every neutral texture binding.
- The names of animated material properties on the renderer. Equal names do not prove equal animation. The 0.3.0 merge must check animation itself.

A page set is the set of pages of one group. The key equality is the merge-ready check. The report prints one line per page set.

Visibility does not split groups in 0.2.0. A later split by visibility makes subsets of a page set. Subsets keep the same key and pages. So a later split cannot make a 0.2.0 page set invalid.

## 7. Regions, placement, and pages

- A placement holds one source texture region. Its key is the member texture identity and the sampler facts. Its region is the union of the UV bounding rectangles of all uses in the group that share that key.
- The border of a placement is `max(r, b) * 2^(P-1)` texels at level 0. `r` is 1 for anisotropy level 0 and 16 for any other level. `b` is the block size: 4 for compressed formats and 1 for uncompressed formats. `P` is the protected level count. The fixtures record `docs/superpowers/investigations/2026-11-19-atlasing-page-fixtures.md` gives the measured reach.
- Border texels inside the source image are copies of the source texels beyond the region. At a source image edge, they follow the source wrap mode. Repeat uses the source period. Clamp repeats the edge. Other wrap modes refuse.
- Placement origins and sizes align to `b * 2^(P-1)` texels at level 0. A source whose size at level `P-1` is not a multiple of `b` refuses its uses.
- Pages are tight. The page width and height are the packing bounds, rounded up to the alignment grid. Neither may exceed the maximum page size. The maximum page size is at most 8192, which is the VRChat SDK texture limit. The SDK checks only importer textures, so it cannot catch a generated page. AMUSE enforces the limit itself. Placements are sorted by longest side, then area, then capture order. One deterministic placement method places them without rotation. A placement that does not fit an empty page of the maximum size refuses its uses. A full page starts a new page in the same group.
- Each page gets one AMUSE-owned material clone. All uses on that page share the clone. This is merge-ready by construction.

## 8. Sampling, acquisition, and output

- Acquisition reuses the owned import copy route in `Host/UnityStreamingTextureEvidence.cs`. Add a method that returns the raw level data and the level 0 texels of the readable copy as `Color32`. Do not write a second clone mechanism. The acquired size is the effective imported size. A copy whose format differs from the source format is an invariant failure.
- Page assembly works on raw bytes and `Color32` only. It never uses `Color[]`. A fixture measured `GetPixels` at 16 bytes per texel, which is 1 GiB for one 8192 x 8192 level. `GetPixels32` measured 4 bytes per texel.
- Page build: compose the page in RGBA32, generate its mip chain with Unity, and encode it in the page format. Then overwrite every protected level with raw blocks copied from the source chains. A block copy at an aligned origin is exact. A second encode is not exact for BC7.
- Every page has mip maps and turns on streaming mip maps through its serialized `m_StreamingMipmaps` property. The VRChat SDK reports an error for importer textures with mip maps but without streaming. A generated page starts with streaming off.
- Clamp edge borders: uncompressed formats copy the edge texels. BC1 and BC3 border blocks keep the edge block endpoints and repeat the edge column, row, or corner indices. That is exact. A BC7 use that needs a Clamp edge border refuses with a name.
- Remapped UV channels are Float32 in the output mesh. A Float16 route channel becomes Float32.
- The UV rounding bound is `S * 2^-25` page texels, with `S` the larger page dimension. A fixture observed the bound and nothing above it for page sizes from 512 to 8192.
- The border assumes one named maximum anisotropy ratio of 16, which is the highest anisotropic level in Unity. The evidence comes from one GPU. The release notes state that limit.
- Format table: the page format is the source graphics format. Supported formats are RGBA32, RGB24, BC1, BC3, and BC7, each in its sRGB or linear form. Other formats refuse the use. So a page never drops alpha that its source has.
- Each generated page gets one output check: at every protected level, every decoded texel of a placement and its border equals the source texel that the source wrap mode maps it to. The check includes the alpha channel, because Poiyomi reflection can use alpha for color in an opaque output. The Color bound is 0, because protected levels are exact copies. A page that fails its check is an invariant failure, because the copy cannot produce a difference.

## 9. Mesh, material, and write

- The atlas pass clones the current mesh of the renderer. That mesh can be the source mesh or the clone that alpha separation made. When the replaced mesh is AMUSE-owned and no renderer references it after the write, the atlas pass destroys it through the owned-object list.
- A vertex shared by triangles that need different route values is duplicated. This happens between an atlased and a kept slot, or between slots on different placements. Duplication copies every vertex attribute, bone weight, and blend shape frame. Only the route channel of atlased triangles changes.
- Before the write, the pass checks that the `sharedMesh` reference, the `sharedMaterials` array, and the slot count of the renderer equal the captured values. A difference is an invariant failure, because no other code runs between capture and write.
- The write sets `sharedMesh` and then `sharedMaterials`. Nothing edits animation clips.
- Cleanup destroys unused AMUSE-owned outputs. It never touches source assets.

## 10. Budget

One budget has three byte numbers. All three use the same unit and the same size function:

- Unit: bytes, as a 64-bit integer. Comparisons use integers only.
- Texture bytes: `GraphicsFormatUtility.ComputeMipChainSize` of the width, height, graphics format, and mip count. This is a pure function of the four facts. It is not a profiler reading, so it is the same on every machine.
- Source facts are the effective imported values of the loaded source texture. Streaming does not reduce them. Page facts are the requested page size, the output format, and the mip count.

1. Group retained bytes.
   - Scope: one group.
   - Value: the sum of the texture bytes of all pages of the group.
   - Limit: a rational factor times the sum of the texture bytes of the distinct member sources of the group. Each source identity counts once. Sources count as retained, because proof that a source has no other reference is not available.
   - Trip rule: the value is greater than the limit. The whole group refuses its uses. The check runs after placement and before acquisition, so no page exists yet.
2. Avatar retained bytes.
   - Scope: one build of one avatar.
   - Value: a running sum of the group retained bytes of admitted groups, in capture order.
   - Limit: a fixed byte count.
   - Trip rule: a group whose bytes would make the sum greater than the limit refuses its uses. Its bytes do not enter the sum. Later groups still check against the sum. Capture order makes the result deterministic. A page that later fails its output check keeps its bytes in the sum. That is conservative.
3. Peak temporary bytes.
   - Scope: one group. The pass handles groups one at a time and releases all temporaries of a group before the next group starts.
   - Value: a prediction of the largest number of bytes that the pass holds at one time for the group. The prediction is a pure function of the sizes and formats in the plan. It counts import clones as texture bytes, managed arrays as length times element size, and pages under construction as texture bytes. The Milestone 1 plan fixes the formula with the acquisition code.
   - Limit: a fixed byte count.
   - Trip rule: the prediction is greater than the limit. The group refuses its uses before acquisition starts.
   - Instrument: a byte ledger in the pass adds each allocation that the pass makes and subtracts each release. A ledger value above the prediction is an invariant failure and throws. Allocations inside Unity, such as importer and encoder internals, are outside the ledger. The release notes state this limit.

A refusal from any of the three numbers names the number. The report line for the group states the value and the limit.

Tests inject the limits through one production delegate. The release gate sets the numeric values from measurements of production code on public fixtures. No invented value ships.

## 11. Settings, interface, and report

- Three serialized fields on the component: enable atlasing (default on), maximum page size (default 8192), and protected mip level count (default 1). The user chose the two numeric defaults on 2026-11-19. A maximum page size above 8192 is not accepted. The inspector limits the field to 8192, and the pass refuses a larger serialized value with a named refusal. Field initializers and Reset give the defaults.
- Add the three fields to the three existing setting lists: the dry run, the build pass, and the Play-mode fingerprint in `Build/PlayModeBuildStore.cs`. Do not add a settings snapshot type.
- The report prints one line per page set and one line per refused use. The page set line states the merge-ready result and the allocation change with its cause. It states that 0.2.0 does not reduce draw calls.
- The dry run shows no atlas rows. Its label states that the preview excludes atlasing.

## 12. Deferred features

Each row keeps a path open for a later version.

| Feature | 0.2.0 behavior | The open path |
| --- | --- | --- |
| Density normalization and separate alpha mask | Pages keep the acquired size | Image preparation takes a target size per placement |
| Strict sampling | Removed | The border function takes the protected level count |
| Compression override matrix | One internal format function | A later matrix overrides that function |
| Visibility and toggle groups | Groups ignore visibility | A later split makes safe subsets |
| Material swaps and animated variants | Swapped slots are refused | Outputs are recorded per use. `AlphaSeparationCurveEdit` shows the retarget pattern |
| UV selector routes on spare channels | The channel of the route is rewritten | A route row names its channel |
| Normal, emission, and mask roles | An unlisted assigned map refuses the use | A role is one member entry plus one real-shader fixture |
| Locked sources and unlock-window slots | Refused with a name | The relock fixture stays |
| Generated textures without an asset | Refused with a name | Acquisition takes a second source |
| UV island regions, offset removal, repeated spans | One rectangle per placement. UV outside the unit square refuses | The layout input is a list of regions |

The older design records the user decisions for each deferred feature.

## 13. Gate 0

These items must close before product work starts:

1. Closed on 2026-11-19 from source review. See `docs/superpowers/investigations/2026-11-19-atlasing-route-tables.md`. The route is `_MainTex` on UV0 for both families. Poiyomi uses an allowed keyword list. lilToon uses required values. The ban list entries from section 17.2 item 2 of the older design are covered: they are outside the Poiyomi allowed list, they are lilToon required values, or the assigned-texture rule refuses them.
2. Closed on 2026-11-19 with no blocking finding. The source check is `docs/superpowers/investigations/2026-11-19-atlasing-opaque-alpha-source-check.md`. The alpha channel in the section 8 output check follows from it.
3. Closed on 2026-11-19 by fixture on a test platform. See `docs/superpowers/investigations/2026-11-19-atlasing-gate-zero-fixtures.md`. Vendor passes that select only on the VRChat platform did not run.
4. Closed on 2026-11-19 by fixture for the BC7 CPU path. See the same record.
5. Closed on 2026-11-19 by fixture. The atlas pass sees appended slots at their final indices. See the same record.
6. Closed on 2026-11-19 from source review. `AmuseStructuralGraphCheck` runs first in the phase with no gate. It stores `StructuralGraph` and `AvatarRefusal` on `AmusePlatformFinishState` and reports nothing. In 0.1.0 only the barrier reports the refusal, and only when alpha separation is on. In 0.2.0 the barrier reports it when at least one feature is on, so the refusal gets one entry. The atlas pass reads the result of the shared gates from state.
7. Closed on 2026-11-19 from source review. The alpha proof type `TextureSampling` stays unchanged. Its single wrap field and its anisotropy eligibility are proof facts, and a change to them would change alpha refusals. The atlas pass gets its own sampler record and one new capture method in `Semantics/UnityTextureEvidence.cs`. The record holds the filter mode, the wrap mode per axis, the anisotropy level as an integer, and the mip bias as an exact float. It reads them from the `Texture` object. Point, Bilinear, and Trilinear filters are captured. Clamp and Repeat wrap are captured per axis. Mirror wrap and other modes fail the capture, and the use refuses. The page copies all four facts from its group, so textures that borrow the main sampler keep their sampling. Anisotropy level zero has a footprint ratio of one on the Windows target. That is the documented desktop exception that `AlphaSamplingPolicy.ConservativeDesktop` already uses. A nonzero level has the named maximum ratio of section 8, because the quality setting can force anisotropy. The mip bias changes only which level the hardware selects. It does not change the footprint inside a level. So the bias is a key fact only and does not enter the border function.
8. Closed on 2026-11-19 by fixtures and user choice. See `docs/superpowers/investigations/2026-11-19-atlasing-page-fixtures.md`. The page format is the source format. Protected levels are block copies, so the Color bound is 0. The border function is in section 7. Pages are tight, not a power-of-two ladder. The protected mip default is 1. The maximum page size default is 8192, after a spike on 2026-11-19. Two checks stay open for a real build and upload in Milestone 2, which makes the first page: that a generated page streams in the built bundle, and that VRChat accepts an 8192 page.
9. Closed on 2026-11-19 by definition in section 10. The numeric limits stay open until the release gate.
10. Closed on 2026-11-19. The user approved this design. Next is the executable plan for Milestone 1.

## 14. Required falsifiers

- A transparent slot and its opaque clone land in different groups.
- A refused atlas use keeps its alpha separation output.
- One falsifier per route table entry, and one for an assigned texture on neither list.
- A preserved Repeat texture that borrows the main sampler.
- A property block entry on a route property for one slot, and one for the whole renderer.
- A mesh with every vertex attribute, bone weights, and blend shapes. The built mesh keeps all of them and stores the route channel as Float32.
- UV outside the unit square refuses the use.
- Two sources of different graphics formats land in different groups.
- A placement origin one block off the alignment grid fails the output check. This guards the block copy.
- A BC1 Clamp source whose region touches the image edge gets an exact border. The same case on BC7 refuses with its name.
- A GPU reach fixture measures the reach on the test machine. It fails when the reach is `r + 0.5` texels or more for either value of `r`. The first texel beyond the border has its center `r + 0.5` texels outside the region, so a smaller reach stays inside the border.
- Two bordered 1024 placements share one page of no more than 2112 texels on the long side, not a 4096 page.
- A serialized maximum page size of 8193 refuses with its name. 8192 is accepted.
- Two whole 2048 sources with `r = 16` share one 4160 x 2080 page under the default maximum.
- Every generated page has mip maps and streaming mip maps on.
- A non-Windows target refuses with one named refusal.
- A source texture that another use still references stays counted.
- A group exactly at each of the three limits is admitted, and one byte more refuses it.
- A group that the avatar limit refuses does not enter the sum, and a later smaller group is admitted.
- An injected allocation above the peak prediction throws.
- An injected invariant failure reaches the build as an exception.
- Atlasing on and alpha separation off with verified versions runs atlasing without the consent dialog. With an unverified host version, the one shared dialog asks once. Both features on ask once. Both off analyzes, mutates, and reports nothing.
