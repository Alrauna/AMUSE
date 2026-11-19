# Merge-ready atlasing for AMUSE 0.2.0

Privacy note: This record contains product design only. It contains no private avatar observations or vendor source excerpts.

Date: 2026-11-16.
Status on 2026-11-16: Engineering closure proposals added for isolated review. User approval remains open. This record does not authorize implementation.
Status on 2026-11-19: Four isolated adversarial reviews completed. Section 17 records the user decisions that followed and the engineering corrections. Section 17 controls where an earlier section is less specific. User approval of the full design remains open. This record does not authorize implementation.
Status on 2026-11-19, later: Historical record. `docs/superpowers/specs/2026-11-19-minimal-atlasing-design.md` controls the 0.2.0 scope. A minimalism review and the user answers of 2026-11-19 changed the order of atlasing and alpha separation and moved many features to later versions. The decisions in this record stay valid input for those later versions, unless the newer design states otherwise.

Delivery draft on 2026-11-16: `docs/superpowers/plans/2026-11-16-0-2-0-atlasing-implementation-plan.md` maps the complete scope to staged outcomes. It is a review draft, not an executable task plan. Its Gate 0 retains the unresolved contracts and approval requirement. No implementation approval follows from that document.

## 1. Purpose

AMUSE 0.2.0 will generate texture atlases that prepare compatible materials for combining in 0.3.0. An atlas stores several image regions in one texture. Merge-ready means that a check in 0.2.0 establishes one shared output material configuration per page set.

The check covers shader behavior, non-texture values, render state, coordinate routing, and admitted animation states. Non-binding configuration agrees across the group. Texture bindings agree among participating uses within each shared material state. Admitted transitions can select different generated bindings only when one shared material state represents all simultaneous uses. Layout agreement alone is insufficient. Compatibility with an undesigned future normalization feature is not evidence. Atlasing does not promise that a future renderer merge will reduce draws.

Texture-memory reduction remains a goal. It is not a mandatory condition for each atlas. Packing can increase memory to preserve detail, sampling protection, or a group that passes the shared-output configuration check. Atlasing alone does not reduce material slots or draw calls.

The default workflow remains add AMUSE, then build. The product must explain preserved inputs and permitted quality differences. Maintenance must not require a general shader interpreter, an Animator reachability solver, or a weighted optimization framework.

## 2. Alternatives and selected direction

The discussion considered three approaches:

- Memory-first atlasing minimizes texture storage. It can fragment groups that later need one material.
- Merge-ready atlasing preserves useful material groups, then reduces waste inside those groups. It can increase memory before combining exists.
- Joint optimization selects alpha splits, atlases, and material merges together. It brings much of 0.3.0 into 0.2.0.

The user selected merge-ready atlasing. Material combining, tint baking, and other material-value normalization remain outside 0.2.0. No fixed memory-savings gate applies. No public extension interface is required.

## 3. Terms and decision order

A surface use identifies geometry, a material slot, and its admitted material states. Admitted states include the defaults and supported animation outcomes. A region is a connected area of used source UV space. UV coordinates map mesh positions to texture positions.

A render group contains surfaces with compatible material-wide behavior. A toggle group contains surfaces with established shared visibility behavior. An atlas group satisfies both conditions. A page set contains aligned atlas outputs for the texture uses of a group.

The decision order is:

1. Capture the effective build avatar after relevant upstream changes.
2. Establish supported material states, coordinate uses, and shared visibility.
3. Acquire supported source images at the effective import-resolution ceiling. Identify coordinate-connected components without alpha cuts or density reduction.
4. Form compatible groups, establish shared content and density references, and resolve the permitted reductions and separate masks. Then assign coordinate routes and plan the shared layout without encoding output textures. Each planned shared page and its variants define one selection unit.
5. Source alpha results can suggest one separate candidate for that unit. Generate its final compressed textures and mip chains, then analyze its proposed opaque outputs.
6. Accept the separate candidate only when its required final proofs and output limits hold. Do not adopt extra splits found during this check.
7. Otherwise generate one shared-layout fallback and analyze its actual final textures. Unsupported separation keeps the valid atlas with pre-separation render behavior.
8. Compose final mesh, material, and animation assignments. Do not revise either candidate after its final analysis.
9. Revalidate live facts, apply consistent references, and report the output.

This order describes dependencies, not a required number of NDMF passes. The implementation must use NDMF ownership and animation facilities. It must not create a parallel build framework.

Compute the ordinary source alpha result at most once when needed for hints or preserved uses. Do not apply it before atlas selection. An accepted atlas owns its final alpha result. A preserved source use retains its ordinary result under the existing controls. Count source analysis and discarded preparation in the work limits.

## 4. Material and visibility grouping

The first supported groups require matching shader behavior and matching non-texture settings. Matching property names alone do not establish matching behavior. Shader source assumptions, keywords, render state, pass structure, and affected features belong to this decision.

Texture contents can differ. Texture bindings and supported spatial controls require an explicit atlas conversion rule. The rule must state which differences the conversion consumes. Unknown differences remain outside the group.

Shared visibility is a grouping fact, not a storage-sharing mechanism. Mutually exclusive outfits still need separate image content. Regions must not overlap merely because their surfaces cannot appear together.

Visibility analysis covers a stated set of patterns. It must account for supported parent activation and renderer enablement. Shared parameter names, menu controls, or clip names do not prove shared visibility. Unknown relationships stay separate. Exact general Animator reachability is not required.

Decision on 2026-11-16: Recognize specific patterns that establish equivalent effective visibility across supported states and transitions. Account for parent activation and renderer enablement. Do not build a general animation-equivalence solver. Missing some genuinely synchronized groups is acceptable. Keep uncertain relationships in separate groups, which can still receive their own supported atlas conversions. Exact recognized patterns and their evidence remain an engineering gate.

One toggle group can require several page sets. Separate toggle groups remain separate in the first implementation. A grouping decision concerns surface uses, not the material asset alone. The same source material can serve surfaces with different visibility.

## 5. UV routing and mesh preservation

The user proposed using the UV selectors of lilToon and Poiyomi. An additional mesh UV channel can provide atlas coordinates where the shader supports that route. Static UV0-only support is not an agreed limit.

An absent mesh attribute does not establish a free channel. A channel can serve a shader effect, animation, generated data, or another build tool. A verified frontend case must declare its coordinate reads and conversion conditions. Evaluate active uses across all admitted states, not only captured defaults. Unknown uses refuse the dependent conversion. Allocation follows bounded declarations, not arbitrary shader interpretation.

An extra channel does not solve every coordinate problem. Each affected texture use needs a supported selector or another verified conversion route. Uses with incompatible mappings cannot silently share one atlas coordinate channel. Unknown procedural, projected, animated, or non-mesh coordinates remain unchanged where needed.

AMUSE preserves original mesh channels unless the transformation explicitly requires their replacement. It preserves skinning, blend shapes, normals, tangents, triangle geometry, winding, and unrelated vertex data. Vertex duplication can separate shared vertices that need different atlas coordinates. It must copy every associated vertex value and preserve the dimensions of existing attributes.

The first packer does not rotate or mirror regions. This avoids new orientation rules for tangent-space normal textures. AMUSE must not recompute original tangents merely because it adds an atlas channel. Normal-map behavior still requires shader-specific verification.

Source inspection on 2026-11-16 found map selectors for mesh channels zero through three in the installed shader rows. Higher selector values can mean procedural coordinates, not extra mesh channels. The inspected texture routes do not expose mesh channels four through seven.

lilToon fixes its main color, alpha-mask, and first normal-map routes to UV0-derived coordinates. Several layer and emission routes have selectors. The bundled Poiyomi 9.3 main texture has a selector. Adding a channel therefore cannot be a universal main-texture solution.

For a supported selectable route, allocate only an established unused addressable channel. For fixed routes, remapping UV0 requires preservation of every other dependent use. Copying original UV0 to a spare channel helps only consumers that can select it. The first release needs explicit recipes, not blanket UV0 refusal or blanket spare-channel allocation.

Decision on 2026-11-16: Select UV channels automatically. Do not expose a manual channel override. Use a verified unused addressable channel for supported selectable routes. Remap UV0 only through a verified conversion that preserves every dependent use. Otherwise, preserve the affected source use. Do not disable shader features to free a channel. The exact version-specific route matrix still requires evidence.

Locked shader reconstruction must establish that the destination route exists in the generated program. Native Poiyomi 10 shader behavior is not evidence for its bundled 9.3 shader. No new vendor invocation during the build is authorized. The code-aware investigation records the source scope and remaining gaps.

Status on 2026-11-19: The locked-material choice for atlas outputs needs a spike. Section 17.1 records the working assumption and the spike result.

## 6. Regions and alpha separation

Packing targets used UV regions rather than whole source image rectangles. Sampling dependencies can require several regions to remain together. Overlapping UVs, wrapping, seams, transforms, and shared vertices need explicit rules.

Decision on 2026-11-16: First remove redundant whole-island integer offsets when every dependent texture use remains equivalent across admitted states. Then consider generating repeated content for genuine repeated spans. Never wrap individual vertices. Non-unit texture transforms and different wrap modes can invalidate a common translation.

Review correction on 2026-11-19: Both installed vendors read raw UV values as data, not only as texture coordinates. The precondition for offset removal and for any UV rewrite therefore covers every UV-value consumer as well as every texture use. Section 17.2 lists the known consumers.

Decisions on 2026-11-16: Leave extreme tiled uses unatlased when they exceed output limits. Do not silently downscale or cut triangles to make them fit. Animated coordinates need a supported mapping across admitted states. Preserve the dependent use when that mapping cannot be established.

Compatible overlapping uses can share one placement and normalize together. Compatibility covers all dependent maps and admitted states. Different output groups can still require separate copies.

Decision on 2026-11-16: Preserving compatible shared placement takes priority over exact density equality between its surface uses. Different physical surface areas do not require duplicate image storage merely to satisfy normalization.

Alpha separation keeps its triangle-level result. A partly transparent island does not force every triangle in that island to remain transparent. Opaque and transparent results have different material behavior.

Decision on 2026-11-16: Prefer region cutting over copying each mixed island in full to both alpha pages. Derive overlap from sampling support across every protected level, not just the coarsest level. Never subdivide triangles for atlasing. Vertex duplication can provide different atlas coordinates without changing triangle geometry. Section 15 specifies the proposed adjacency, crop, overlap, and bounded fallback rules. Their runtime proof and measured limits remain open.

Decision on 2026-11-16, answer 57: A fixed complexity bound can reject separate storage for fragmented regions. Try the shared atlas next. Do not add crop-merging searches or increasingly expensive packing strategies to rescue separation. Establish the bound through measured fixtures.

Decision on 2026-11-16, answer 52: Determine the alpha contract after atlasing wherever feasible. Prioritize a valid atlas when alpha separation cannot proceed. This supersedes the earlier fixed pre-packing alpha baseline and alpha-preservation admission gate.

Analyze final atlas content under the atlas-backed source shader semantics before any opaque conversion. Testing a forced-opaque destination would prove nothing about conversion safety. Missing evidence remains Unknown. Existing alpha acceptance policies retain their meanings. Atlasing does not grant an extra opaque fallback.

Spike finding on 2026-11-16: Alpha-dropping compression can make a transparent input appear fully opaque to the predicate. RGB-only encoding requires a justified opaque conversion after retained-alpha evidence. Do not classify an alpha-dropping output to grant itself permission. Decision on 2026-11-16: Alpha removal is an optional storage optimization, not a requirement for a successful opaque conversion. Retain alpha when its safe removal cannot be established. Alpha-retaining textures can serve a separately verified opaque material conversion. Allocation limits and the shared fallback still apply. Separate layout alone does not establish storage savings. Any supported RGB-only recipe still needs an explicit proof and final checks.

Decision on 2026-11-16, answer 53: Permit opaque and transparent results to share atlas textures only where necessary. Do not make sharing the unconditional default. Prefer separate pages when a supported, bounded process can produce them within the output limits. A separate-page layout must receive its own final alpha analysis. Never transfer a verdict from an earlier shared layout. Do not alternate packing and classification until a desired result appears.

Proposed bounded rule on 2026-11-16: A selection unit contains one planned shared page, all its surface uses, and all their admitted variants. Each use belongs to one unit. Source alpha results suggest membership for at most one separate candidate within that unit. Every required opaque target must pass final analysis across admitted states. Unknown or unadmitted alpha animation excludes its dependent opaque conversion before this check. No repeated search or new artist control is required.

Reject the whole unit's separate candidate if any required proof, batch fit, or output limit fails. Do not move failed regions between proposed alpha slots. Discard its outputs and try the unit's single shared layout. Other units keep their selected results. The shared fallback receives independent final analysis. Unsupported separation retains its pre-separation behavior. Failed shared generation, coordinate conversion, or allocation preserves that unit's source uses. No third layout follows.

Decision on 2026-11-16: Prefer a valid shared opaque and transparent atlas over restoring source textures when separate storage cannot qualify. Increased allocation is acceptable within the growth limits. The shared fallback must independently meet the selected sampling protection. Sharing alpha storage does not itself remove sampling boundaries between packed regions.

Proposed necessity rule on 2026-11-16: Report why the supported separate recipe cannot qualify. Distinguish no supported proposal, failed final alpha evidence, and fit or allocation limits. Missing evidence does not prove that physical separation is impossible. A simpler implementation alone does not establish necessity. The spike exercised acceptance and rejection on fixed synthetic layouts. It did not establish island partitioning or a complete packer.

If alpha separation is unsupported, retain the atlas-backed material's pre-separation render behavior. If atlas generation or coordinate conversion fails, preserve that dependent source use and use its ordinary source-based alpha path. Never apply an atlas-derived verdict to restored source textures.

## 7. Animation and material variants

All admitted material states of a slot must agree on the mesh coordinate assignment. Compatible variants can use different generated textures with matching layouts. The memory estimate must include every retained variant.

AMUSE updates supported material-swap references through the host animation facilities. Retarget each binding path to its use-specific output. A source-wide replacement cannot represent different outputs for different uses. If a binding cannot address the required output, preserve its dependent use. Do not add runtime scripts, animated UV rewrites, or a custom texture-selection system.

Animated properties must retain their meaning after conversion. A conversion must not bake an animated value into a static texture. A new atlas selector must not conflict with an animated selector or coordinate transform.

## 8. Paging and memory

Paging divides an atlas group into several page sets. Captured render behavior establishes compatible groups before paging. Preliminary alpha results can suggest one separate candidate. Only its final texture evidence authorizes an opaque conversion. Extra pages must resolve fit, sampling, or the requested opaque and transparent storage split. They must not chase minor memory gains.

Concrete reasons for extra pages include the maximum output size and an established sampling constraint. A supported map can use a different resolution or format from another map. Related maps retain aligned normalized region placement.

The optimizer must consider actual generated allocation rather than occupied rectangle area alone. Estimates include dimensions, storage formats, compression blocks, mip levels, padding, all maps, and all animation variants. Distinct retained source textures count once. Removing one reference does not save a texture that other outputs still use.

Estimated allocation is not a claim about runtime residency. Hiding an object does not establish that its textures leave video memory. Draw-call savings remain unclaimed until material combining exists.

The design excludes configurable scoring weights and a general search engine. It also rejects a mandatory lowest-memory layout. Use one deterministic packing method within the render and toggle groups. Improve placement and page dimensions without creating new groups for minor memory gains.

Each packing step must place a region, create a required bounded page, or refuse the dependent input. An oversized region does not trigger silent downscaling. Candidate dimensions and retry limits must be finite. A generated-allocation check can reject a candidate. Only the single shared fallback described above can follow a rejected separate candidate.

Proposed rule on 2026-11-16: Each admitted state of a proposed output slot binds one page set. All its states share one placement layout, but their generated texture bindings can differ. Separate candidates propose alpha output slots before final proof. Their opaque conversion remains conditional on that proof. Shared fallback separation can create output slots that retain its layout. Paging alone does not add slots. Place all regions of a proposed slot as a batch. A batch-fit failure rejects the unit's separate candidate. During initial shared-layout planning, a batch that cannot fit an empty page stays unatlased.

Sort batches by decreasing longest region side, then total region area, then stable capture order. Sort regions inside each batch by longest side, then area. Do not rotate them. Use a fixed finite dimension ladder and one placement method. A failed batch trial does not commit partial placement. Add a page only when the current page cannot accept the batch. Derive aligned map dimensions and sampling borders before placement. No polygon interlocking or layout search framework is required.

Within one layout, place compatible shared content once and reference it from each participating batch. Another page can require a counted copy. Fit constraints can require duplication. Exact density equality alone does not justify it.

A maximum page size does not bound the total allocation. Decision on 2026-11-16: Apply automatic growth limits to individual candidates and the avatar aggregate. Establish their values through measured fixtures. No arbitrary growth percentage is agreed. The numeric limits remain a release blocker. The user rejected a mandatory savings gate, so this review does not silently restore one.

Decision on 2026-11-16: Evaluate selection units in stable capture order. Test each proposed output against one running allocation account before acceptance. Count selected outputs and source textures still referenced by preserved or unprocessed uses. Deduplicate source references. A rejected separate candidate can try its one shared fallback. If that also exceeds the guard, preserve the unit's source uses. Do not revisit earlier selections or assume future savings. Accept missing jointly beneficial conversions rather than add coordinated memory planning in 0.2.0.

Temporary work also needs a bound. Source decoding, prepared images, animation variants, and compression staging can exceed final texture allocation. A safe output estimate does not authorize unbounded preparation memory.

## 9. Density normalization

Texel density is a linear measure. It is the square root of texture pixel area over physical surface area, as section 15.4 defines it. Strength zero preserves each region's source density. Decision on 2026-11-16: Full strength reduces above-target regions toward an area-weighted average source density. Regions below the target stay unchanged. Shared placements normalize together even when their surface uses have different densities. Exact equality is not required. Intermediate values change density gradually rather than interpolate UV positions.

Surface area comes from the effective undeformed reference mesh with transform scale included. The current animation pose does not set the target density. Decision on 2026-11-16, answer 55: Each participating surface contributes its physical area to the density target. Compatible shared image content receives one reduction scale and one stored placement.

Decision on 2026-11-16: Prefer the main color map as each use's density reference when its mapping is usable. Otherwise, use its highest-density participating map with a usable measurement. Apply the resulting common reduction scale to dependent maps. Other map resolutions do not independently drive that use's density when main color supplies the reference. This deliberate fallback can give otherwise similar uses different measurements. Report the selected reference when it determines a reduction. Do not claim that different reference-map choices preserve equal color-map density.

Decision on 2026-11-16: Density normalization can reduce region resolution but never increase it. The user rejected redistribution that increases resolution for lower-density regions. Interpolate the common region resolution scale linearly from one to its full-strength scale. Strength zero leaves the scale unchanged. Full strength uses the calculated reduction. Apply the same scale to dependent maps. Do not add curve controls. Strength is not a percentage of memory savings.

Decision on 2026-11-16, answer 56: Exclude regions without a usable surface or UV area from the normalization average. Leave their density unchanged. They can still join an otherwise supported atlas conversion. An unusable measurement does not disable normalization elsewhere.

Decision on 2026-11-16, answer 60: Compute one density target per compatible atlas group before paging or alpha-region cutting. All derived pages use that target. Unrelated groups do not affect each other. Separate groups can retain different densities.

Decision status on 2026-11-16: Retain normalization in the design. Section 15 proposes its exact calculation. Measurement and output proof remain required before implementation approval.

Normalization never deliberately increases texel density, including at nonzero strength. Extra allocation from page dimensions, padding, or duplicated variants is not a density increase. Decision on 2026-11-16: Preserve a dependent use if the requested result cannot fit the output limits. Downsampling alone does not guarantee that every source region fits.

Decision on 2026-11-16: Preserve relative resolutions between color, normal, emission, and existing mask maps. Apply one normalization scale per region. Do not add separate artist sliders per map type. A newly extracted separate alpha mask is the explicit exception. It can retain the acquired main image's effective resolution before normalization, but cannot exceed that import-resolution ceiling. Page rounding and compression alignment do not authorize additional detail loss.

## 10. Sampling contract

Decision on 2026-11-16, answer 52: Ordinary atlasing takes priority over preserving the earlier alpha-separation result. Determine the result from final atlas textures wherever feasible. Accepted mip filtering can change cutout edges and blended transparency. A failed alpha-separation proof does not reject an otherwise supported atlas. It does not authorize an unproved opaque conversion.

Request on 2026-11-16: Add an opt-in control that extracts main-texture alpha into a separate mask when this permits color downsampling. The mask can retain greater resolution than color. This is a requested conversion, not an established preservation guarantee. Its supported shader equations, existing-mask composition, animation behavior, filtering, compression, memory cost, and refusal behavior remain open.

The conversion must not bake dynamic alpha behavior into a static mask. A mask property alone does not establish a supported conversion. Existing alpha masks and layer equations can prevent simple extraction. The control defaults off.

Source finding on 2026-11-16: The inspected vendors borrow the main sampler for alpha masks. A candidate extraction recipe can use an inactive mask path in a static main-only configuration. It must preserve tint alpha and other remaining multipliers. An existing replace mask can already decouple alpha resolution. An existing sampled multiply mask cannot generally collapse into one sampled product without changing transparency. Skip extraction rather than add another texture slot or a general equation baker. These are characterization cases, not established supported conversions.

Decisions on 2026-11-16: Name the opt-in control "Allow Separate Alpha Mask Generation". Preserve the existing mask equation and all admitted animation states. Include all generated textures in the growth guard. Enabling the control does not require extraction everywhere or a saving from every extraction.

Decision on 2026-11-16: Separate-mask generation is best-effort permission, not a preservation requirement. Use it where supported and useful for retaining alpha detail during color reduction. Otherwise continue with the accepted ordinary downsampling. Report when alpha was downsampled rather than retained in a separate mask. This supersedes the earlier unsupported-extraction refusal.

Decision on 2026-11-16: Choosing normalization accepts alpha-detail loss from downsampling, including cutout-edge and blended-transparency changes. Separate-mask generation can retain higher-resolution alpha where supported. Answer 52 moves classification to the final atlas output, after these preparations and output generation.

Padding and edge extension follow the supported sampling requirements. A fixed unexplained border is not a preservation proof. Different maps can require different padding despite aligned placement.

Decision on 2026-11-16: Strict sampling protects the prepared, normalized textures, not the unreduced source. It requires preservation across every mip level that the output can sample. When no supported route meets that condition within the limits, AMUSE preserves the dependent source use. Strict mode does not exceed limits or silently lower its standard. It does not undo explicitly accepted normalization loss.

Decision on 2026-11-16: Strict sampling prevents atlas-induced sampling contamination across every mip that the output can sample. It retains the agreed tolerance for small recompression differences, not pixel-identical output. It also retains explicitly requested normalization loss. Alpha conversion requires its independent final-output proof. The numerical quality tolerance and useful supported strict cases still need evidence.

Finding on 2026-11-16: Padding cannot preserve arbitrary independent images through a shared full mip chain. The final one-texel mip cannot preserve two different source constants. Decision on 2026-11-16: Strict mode can leave more source uses unatlased. Do not add shader-level sampling restrictions to make an atlas qualify. Useful supported strict cases still need a focused proof before the control can ship.

Decision on 2026-11-16, answer 52: Reuse the separator's maximum tested mip and minimum tested texture rules where necessary for atlas alpha analysis. Keep the distinct cutout and transparent policies. Levels outside the selected scope remain accepted risk, not proven safe. If no eligible level remains, alpha evidence stays Unknown and atlasing can still proceed.

Decision on 2026-11-16: Use "coarsest protected mip" for sampling protection and "coarsest tested mip" for alpha analysis. Configure them independently through the same preset choices. Do not equate a tested alpha level with protected image sampling. Minimum tested texture size remains a separate limit. Preset values and the overlap calculation still need definition.

Sampling protection compares the prepared source with the atlas under its pre-separation shader behavior. Alpha conversion is a separate decision under the existing alpha acceptance policy. Do not advertise finite alpha testing as all-mip conversion safety, including in strict sampling mode. A protected image sample can still lie outside the selected alpha-test scope.

Never shorten the selected alpha-test scope to match a smaller protected range. Test the actual final atlas at every eligible level selected by that policy, including unprotected levels. Ordinary filtering there can change alpha under the accepted atlas-first policy. It is not permission to drop stored alpha or replace required evidence with an invented opaque image. If source-equivalent mapping cannot establish the requested test scope, the verdict is Unknown.

Decision on 2026-11-16: Interpret the coarsest protected mip relative to each prepared source map after permitted density reduction. Cropping and atlas dimensions do not reset that reference. Calculate each map's pixel margin from its supported sampling and compression requirements across admitted states. Different map resolutions retain aligned placements. The exact mapping and margin calculation still require fixture evidence.

Decision on 2026-11-16: Resolve minimum tested texture size per texture use at its prepared source-equivalent resolution after permitted density reduction. Do not use atlas page or crop dimensions as the minimum-size reference. A larger page does not make an ineligible small source eligible. A small crop does not reduce the source-relative testing scope. Actual alpha evidence still comes from the final compressed atlas. No eligible levels means Unknown, not atlas rejection. Establish the exact mip mapping with fixtures before implementation.

Atlasing and alpha separation retain separate decisions. A sampling change can be accepted without proving opacity. Texture generation must still preserve coordinate meaning, color interpretation, channel meaning, normal encoding, and admitted animation behavior. Atlas priority does not authorize arbitrary base-level image changes or unsupported shader conversion.

Decision on 2026-11-16: Ordinary atlasing permits small color and shading differences from recompression. Preserve color interpretation and normal-map encoding. Alpha decisions must hold for the actual generated output. Strict sampling rejects output that cannot meet its stronger contract. Do not silently force success through excessive uncompressed allocation.

Decision on 2026-11-16: Output compression defaults to automatic selection, with optional user overrides by texture-type category and material-type category. Material types mean opaque, cutout, and transparent rendering behavior, not shader families. Texture categories are Color, Emission, Normals, and Masks. Emission contains only emission images in 0.2.0, not a general effects category. Emission masks remain Masks. Available formats remain open. Each material-type and texture-type pair has one setting that defaults to Automatic. Do not add competing override layers or precedence rules. Offer only verified formats that preserve the texture role's required data. The "Allow Automatic Compression Fallback" toggle defaults on. When enabled, an override that cannot qualify permits automatic compression selection. Report any substitution. When disabled, honor the applicable override or preserve affected source uses after the existing atlas fallbacks. Neither overrides nor fallback waive color, channel, normal encoding, alpha evidence, or allocation requirements.

Decision on 2026-11-16: Exclude uncompressed formats from generated atlas outputs in 0.2.0, including manual overrides. If no verified compressed format qualifies, use the existing atlas or source fallback. This restriction does not prohibit temporary uncompressed processing buffers, which count toward temporary-work limits.

Decision on 2026-11-16: Allow at most one Automatic compression replacement per generated texture within each existing layout. Preserve crop placement, resolution, and requested sampling protection. Automatic selects one verified alternative, not a sequence of formats. If it cannot qualify, follow the existing shared-atlas or source fallback. An unsuccessful opacity proof alone does not trigger format hunting. Final alpha evidence uses the selected actual output. Count replacement encoding in temporary-work limits.

Decision on 2026-11-16: Select a shared texture's compression category from its planned uses across admitted animation states before encoding. Any transparent use selects Transparent. Otherwise, any cutout use selects Cutout. All other uses select Opaque. This selects storage configuration, not material render behavior. Any output that supplies a pending or granted alpha proof retains required alpha regardless of category, including under Automatic selection. Later opacity discoveries do not trigger repeated encoding or packing. Report the category actually used. Do not add a mixed-material category or extra pages merely to satisfy conflicting overrides. Required alpha evidence and channel preservation still apply.

Decisions on 2026-11-16: Prefer source texture files on disk to avoid recompressing imported compressed pixels. Preserve Unity import interpretation and use the effective imported resolution as the density ceiling. A larger source file does not authorize a resolution upgrade.

Source acquisition must correspond to the effective texture bound at the build stage. When a matching disk source cannot be established, use a supported capture of the effective generated texture. Otherwise preserve the dependent use unchanged. Never replace an upstream generated result with stale source content. Supported source formats and faithful import reconstruction remain engineering questions.

Source finding on 2026-11-16: Existing capture supplies alpha verdicts, not full color and normal images. Its PNG reader and imported-format gate protect alpha evidence. They do not establish a general atlas image reader. The new acquisition must preserve import meaning and support the effective-texture fallback. A larger disk source requires an explicit reduction to the imported resolution ceiling. Output compression and mip generation remain unproved until focused Unity characterization.

Default mip acceptance does not authorize color-space changes or arbitrary base-level resampling differences. The first supported output formats still need an explicit decision. Alpha protection and strict-mode feasibility remain implementation gates.

## 11. User interface and configuration

Atlasing is on by default. Enablement and maximum size have defined meanings. Density strength and strict sampling remain requested controls. Their proposed contracts require the evidence listed in sections 14 and 15. Do not build those controls before their gates close. Removing either requires user approval. The interface does not expose internal packing algorithms.

Decision on 2026-11-16, answers 54 and 59: Density normalization uses an opt-in toggle that defaults off. Atlasing alone does not enable it. Disabled normalization preserves source density. Its initial enabled strength is full strength. Users can reduce that strength. Explain the potential detail loss beside the toggle. Ordinary atlas mip filtering can still change transparency under answer 52.

The interface must explain that normalization reduces detail and can change transparency. Ordinary atlas mip filtering can also change transparency. Atlasing can increase memory before combining. Distinguish accepted quality differences from an opacity proof. Report one primary named refusal per dependent candidate. Include an actionable limit when it is available. Do not create a diagnostics framework.

Decision on 2026-11-16, answer 58: Report the estimated allocation change and its cause when shared storage preserves a compatible group. State that 0.2.0 does not reduce draw calls. Future combining benefits are not savings already achieved.

Existing preview results describe the scene snapshot. Effective build results include upstream transformations. The interface must not present one as the other. A settings change invalidates a result that depends on those settings. Do not build a second preview cache or promise an interactive atlas simulation.

The user permits cuts or rework to existing controls when evidence supports a simpler product. This permission does not choose which controls to remove. The architecture investigation must propose removals, explain behavior changes, and define import and preset migration. No existing setting silently changes meaning.

Decision on 2026-11-16: Retain the existing alpha-separator features and controls. The user uses their accepted approximations to smooth over authoring mistakes. Island analysis does not replace these policies or grant additional flattening permission.

One stored configuration owns each choice. Presets select configuration values. They must not form a second source of truth or overwrite the first user edit.

Decision on 2026-11-16: Unset options take their defaults, including atlasing enabled. This applies to upgraded components and older presets. Explicitly stored choices retain their meaning. Migration must distinguish an absent value from an explicit false value. The user rejected a separate default-off migration policy.

## 12. Implementation structure

The design uses the existing separation between live capture, pure decisions, shader-specific conversion, and controlled build mutation. Atlas decisions consume immutable evidence. Live objects and generated resources stay with the build integration.

Shader frontends own supported texture uses and coordinate conversion rules. They do not need a universal shader program or a general feature graph. Atlas analysis owns region relationships and layout. Build integration owns generated textures, mesh copies, animation references, and final consistency.

The product must use one authority for final output assignments. Alpha separation and atlasing must not independently write competing mesh or material arrays. Expected incompatibility preserves only the dependent use. Programming defects stop the build.

Prepared output must identify its required live facts and owned resources. Revalidation must occur before the first write. Cleanup removes unused AMUSE-owned outputs, not source assets. The boundary does not promise rollback after an internal failure.

Failure removes conversions, not facts. A rejected separate candidate permits its single shared fallback before final selection. After selection, a failed page removes conversions and outputs that require it. Continue removal until no dependent output remains invalid. Do not regenerate images or repack selected pages. Keep all admitted states and coordinate assignments of each surface use consistent. Do not turn a page-local failure into an avatar-wide refusal.

No speculative new assembly, public plug-in interface, shared global planner, or alternate host layer is required. Refactoring must remove a concrete obstacle to the agreed transformation.

## 13. Delivery sequence and proof

Each milestone exercises capture through final build application. The sequence is not a commitment to release partial behavior:

1. Establish one supported shader configuration and one complete atlas conversion.
2. Integrate alpha groups, supported visibility patterns, and compatible material swaps.
3. Add density normalization and independently sized aligned maps.
4. Expand shader configurations through named supported cases.
5. Establish release limits from measured outputs and failure cases.

Permanent tests must protect consumer-visible behavior. They must use the installed vendor implementations where those implementations supply the real source. Pin those versions in the existing test environment. Public fixtures must be deterministic and redistributable.

Required falsifiers include an occupied destination UV channel, an unsupported selector, a UV-dependent non-texture effect, and an animated coordinate input. Other cases include retained source references, a variant that cannot share a layout, overlapping UVs, and a mixed-alpha island. Sampling cases include distant mips, cutout edges, normal maps, and incompatible color interpretation. Mesh cases include skinning, blend shapes, and vertex duplication.

Build-level checks must establish final references, unchanged source assets, cleanup, and local refusal behavior. Visual checks must exercise the real output. Memory estimates need comparison against generated texture allocations. Private observations are supporting evidence, not the correctness oracle.

Release evidence must show a nonempty supported surface for each claimed shader family. At least one public fixture must reduce estimated retained texture allocation against its deduplicated source allocation. This is evidence of packing benefit, not a mandatory savings gate for every candidate or avatar. Show a page set that passes the shared-output configuration check without combining materials. Record separate-candidate acceptance, generation counts, and build-time change against the same fixture without separate-page generation. Do not infer speed from occupancy.

Every default memory increase must have a stated cause and pass the approved growth guard. A fixture aggregate cannot hide an excessive increase in one candidate. Many small increases must not bypass the aggregate guard. No runtime speed claim follows from atlas occupancy or estimated allocation.

Record admitted and preserved cases for public mixed-alpha fixtures. Refusal after generation leaves unused space in surviving pages. Include that space in memory accounting and report it as waste. These observations must expose a route that is safe but admits too little useful content.

The milestone sequence preserves all agreed features. Any proposed cut requires user approval before implementation planning. No milestone is permission to ship a placeholder for an unresolved contract.

## 14. Approval gates

As of 2026-11-16, these engineering contracts and proofs remain open. Settled product choices above are not reopened by this list:

- Verify version-specific UV routes, channel liveness, and automatic channel allocation.
- Prove final-output alpha evidence and the source-equivalent mip mapping. Any RGB-only recipe requires its own proof. Alpha removal is optional.
- Review the section 15 region construction and sampling-support rule. Establish its runtime proof and measured fragmentation bound. Never subdivide triangles.
- Finalize selection-unit construction, packing bounds, temporary-work limits, and measured candidate and aggregate growth thresholds.
- Review the section 15 density calculation across shared uses and map resolutions. Prove the linear scale curve and the rule that a shared placement with an unusable measurement keeps scale one.
- Select initial output formats and verify color, normal encoding, compression, and the accepted quality contract.
- Define and verify recognized visibility patterns and version-specific material compatibility.
- Specify and prove configuration migration that applies defaults only to unset options.
- Establish useful strict-mode cases without shader-level sampling restrictions.

Added on 2026-11-19 after the adversarial review. These items are open:

- Give AMUSE-owned outputs an evidence identity that can never prove alpha one from the output format alone.
- Declare every UV-value consumer per shader row, across UV channels zero through seven.
- Define sampler compatibility for each route, including textures that borrow the main sampler.
- Define whole-material equality capture, including stencil, cull, keywords, tags, and queue.
- Define the apply-time revalidation inventory for atlas outputs.
- Keep Float32 storage for every remapped UV channel in the output mesh. Verify the stored vertex format.
- Establish the UV-rounding bound for one actual vendor route.
- Establish the anisotropic support border with evidence from more than one GPU vendor.
- Add sRGB, non-power-of-two, and coverage-preserving mip fixtures.
- Settle the locked-material route after the relock spike.
- Define each work counter by unit, scope, trip rule, and measuring instrument. Section 17.3 moves the numeric values to a release gate.

The revised region and fallback contracts need an isolated adversarial review. This record still requires user approval before implementation planning.

The first code-blind review completed on 2026-11-16. It read this design alone. It did not inspect the code-aware investigation or product source.

Accepted corrections include a fixed alpha baseline, bounded frontend declarations, finite packing work, one primary refusal, and verifiable merge-group facts. The review's mandatory savings gate conflicts with the selected objective and is not adopted. Its UV0-only alternative conflicts with the requested selector investigation and is not adopted.

The second code-blind review accepted the fixed baseline, terminal refusal, bounded declarations, and finite packing contract. Its remaining concerns include aggregate memory growth, unresolved control contracts, and the scope of a failed shared output. This revision clarifies per-page-set compatibility and evaluation across admitted animation states. These are design corrections, not observations of implemented behavior.

Proposed cuts to normalization, strict sampling, and independent map resolutions remain user decisions. The code-aware investigation is `docs/superpowers/investigations/2026-11-16-atlasing-foundation-investigation.md`. The user approves the revised design before implementation planning.

The focused feasibility record is `docs/superpowers/investigations/2026-11-16-atlasing-feasibility-review.md`. It records analytical limits and source findings. Those findings do not establish runtime support.

The third code-blind review completed on 2026-11-16. It examined the revised sequence and fallback. It identified the default alpha mip conflict, page assignment ambiguity, binding-specific swaps, and the missing density default. This revision records the latter three corrections as explicit rules or proposals. The alpha quality decision remains open. Recommendations to cut normalization or accept distant-mip alpha loss are not adopted without user approval.

The follow-up review on 2026-11-16 found a wording conflict between page assignment and animated texture variants. Each state now binds one page set, while all states of a slot share one layout. Shared content occupies one placement within that layout. The follow-up confirmed the other corrections and left the alpha quality gate open.

Decision update on 2026-11-16: Answer 52 supersedes the pre-packing alpha baseline reviewed above. Default atlasing now takes priority over alpha separation. Answer 53 permits shared opaque and transparent pages only where necessary. The post-atlas order and bounded page-selection method need review before implementation planning. Earlier review conclusions do not establish this revised sequence.

Spike update on 2026-11-16: The user authorized a disposable feasibility spike, not product implementation. Unity characterization established three import-copy cases, two generated-pixel cases, four vendor UV renders, and final compressed alpha evidence on fixed layouts. The proposed separate-first sequence above replaces an unrestricted packing and classification loop with one candidate and one fallback. It remains subject to isolated review and user design approval. Evidence and limits are in `docs/superpowers/investigations/2026-11-16-atlasing-throwaway-spike.md`.

Isolated review on 2026-11-16: The code-blind reviewer confirmed bounded termination but rejected the underspecified candidate lifecycle. Later decisions select region cutting, consistent mip terminology, and a shared atlas before source fallback. Section 15 now gives engineering proposals for review. Earlier reviews do not approve these proposals. RGB-only conversion requires its own proof only if that optional optimization is implemented.

## 15. Engineering closure proposals

Proposal status on 2026-11-16: The following choices are engineering recommendations, not new user requests or measured capabilities. They preserve the agreed product scope. The user delegated engineering choices rather than approving untested safety claims. This section controls where an older proposal above is less specific. Sections 14 and 15 separate rules that can be reviewed on paper from claims that need runtime evidence.

### 15.1 Region construction

Use original mesh topology to identify neighbors. Two triangles are neighbors only when they share a mesh edge by vertex indices and their selected coordinate mapping is continuous across that edge. Coincident positions or matching coordinates alone do not join disconnected geometry. Do not weld seams. Shared placement for compatible overlapping image content is a separate storage decision, not topology.

Evaluate continuity and the required coordinate mapping across all admitted states and dependent maps. A shared crop requires one supported affine placement for all its triangles. If a mapping cannot be represented that way, preserve the dependent use. A whole-island integer translation must pass the existing equivalence rule before constructing crops.

For the shared layout, find connected components within each surface use under this adjacency rule. Shared content can be reused only after map identity, mapping, interpretation, prepared resolution, and all admitted variants agree. Do not infer equivalence from pixel resemblance or a content hash alone.

For the separate candidate, label a triangle as a proposed opaque target only when the source hint permits opaque conversion across every admitted state. Label every other triangle as retained behavior. Remove adjacency across each shared edge where the labels or proposed output slots differ, then find connected components in the remaining adjacency. This cuts region membership, not triangles. Unknown joins retained behavior, not opaque. Each original triangle belongs to exactly one component. An opaque hint remains a proposal until final atlas evidence passes.

Compute the axis-aligned bounds of each component in its source sampling coordinates. Include the complete triangles, not only their vertex neighborhoods. Round bounds outward to pixel boundaries for each prepared map. Never cut geometry at crop or page edges. UV-degenerate triangles can be copied in a shared atlas through a supported mapping, but they cannot gain opaque permission from a missing area proof.

Rectangular crops may overlap in source image space. Copy their original neighboring source content into each overlap. Opaque and retained-behavior crops can contain the same pixels while owning different triangles. That duplication is counted memory, not a reason to merge or move triangle membership. Internal cuts do not use invented opaque pixels or simple edge extrusion.

At an original image boundary, obtain neighboring samples through the established source wrap rule. Clamp repeats the source edge. Repeat uses the source period. Other modes require their own verified recipe. Non-unit transforms, wrap differences, or repeated spans must pass all dependent mappings. A crop that exceeds the bounded materialization or output limits fails its candidate. Do not replace this with vertex-wise wrapping.

### 15.2 Overlap and mip mapping

Define the required border from sampling support, not a fixed number of pixels per mip. Sampling support means every prepared-source pixel that can influence a protected sample. It includes mip construction, bilinear or trilinear filtering, the supported anisotropic footprint, coordinate transforms, and compression blocks.

For each dependent map, map each protected source-equivalent level to the actual output levels that the verified shader sampler can access. Include both levels used by mip blending. Page dimensions alone do not determine this relation. The region's placement scale and the shader's derivative or explicit-level behavior also matter. Unsupported level selection preserves the dependent use.

For each such output level, find the sample footprint of the complete mapped triangle region. Expand it by the verified mip-generation and filtering support. Include every compression block that intersects this support. Map this union back to prepared-source pixel space and round outward. The required overlap is the excess of that bound over the component's unexpanded bound. The frontend recipe must give finite support bounds. An unbounded or unknown bound is not permission to assume zero.

Copy each protected level of a crop from the full prepared-source mip chain at that level. Do not generate the whole mip chain by filtering an already packed base page. That would mix neighboring crops earlier than the intended protected range. Generating lower levels from a cropped neighborhood alone is untested. Kaiser filtering, coverage-preserving mips, and sRGB filtering can make it differ from the full-image chain. At every protected level, include the full required neighborhood and keep independent regions out of the same compression blocks. Placement feasibility includes these level-specific constraints.

Preserve the prepared source's sampling-grid phase at every protected level, not only its filter support. A crop origin is not a new source origin. For a pixel-preserving translation and a regular power-of-two mip grid, the translation must align to that grid at every protected level. Compression blocks can require a stronger alignment. General transforms require their own established grid rule. Reject an unrepresentable phase rather than resample to hide it.

Analytical check on 2026-11-16: An exact one-dimensional box-filter example placed one nonzero source pixel in a 32-pixel image. At mip two, sampling its source block center gave one quarter. A two-pixel image translation followed by page mip generation gave one eighth. A four-pixel translation gave one quarter. This proves a grid-phase constraint for that synthetic filter, not Unity mip correctness. The first interpreter attempt used the wrong center convention and failed its assertion. The corrected boundary-coordinate calculation passed.

At levels beyond every protected requirement on a page, generate one deterministic shared mip tail from the preceding assembled level. Those levels can mix content under the accepted finite-range policy. Do not resolve a collision by letting the last crop overwrite another crop's mip pixels. Strict mode has no unprotected tail. Compatible shared pixels require established equivalence, not write order.

All maps and variants share one normalized coordinate placement. Construct that placement using the most restrictive per-map and per-level requirements. Some borders can be larger than a map needs. If the finite page ladder cannot represent aligned placements without extra resampling or overlap of independent protected blocks, reject the candidate. Do not silently change map resolution to satisfy alignment.

The finite protected range allows the accepted distant-mip contamination outside that range. Strict mode instead includes every accessible level. It does not relax its last levels, introduce a shader clamp, or call a failed full-chain check a success. The one-texel conflict can reject most mixed-content strict candidates.

This is a construction specification, not an observed Unity capability. Required falsifiers include a cut through a gradient, a diagonal thin triangle, a source-edge repeat, trilinear transitions, anisotropic sampling, a compression-block boundary, different map resolutions, and the one-texel tail. Measure both sides of each cut against the same prepared-source reference.

Sampling evidence on 2026-11-17: The follow-up spike established a level-specific bilinear and compression-block copy rule for pixel-preserving translations. It exercised thin regions, Repeat, Clamp, integer island moves, and aligned unequal map resolutions. A periodic grayscale crop reduced encoded image data by about 75 percent with no measured RGB error in the tested full-chain route. This is a narrow candidate for consumers that do not read alpha. BC7 still changed its alpha. The spike also isolated translation-sensitive numerical differences that larger borders did not remove. Coordinate precision needs an explicit bound separate from compression. No new allowance is approved by this evidence. Anisotropic support, actual frontend routes, and required-channel quality limits remain open. G0.2 is not closed. See `docs/superpowers/investigations/2026-11-17-atlasing-sampling-followup-spike.md`.

User decision on 2026-11-18: Permit a separately bounded UV-rounding error in ordinary and strict modes. Reject a route when its bound cannot be established. This does not permit contamination from neighboring regions. This is a policy decision. It is not proof of a bound. No numerical bound is set. The bound must come from measured coordinate precision on a bounded actual shader route, not from the spike diagnostics alone. This decision does not authorize snapping product UVs, shader sampling restrictions, or a periodicity search. G0.2 remains open.

Precision evidence on 2026-11-19: Without anisotropic filtering, a bound held in every measured pixel. The bound uses the specification's 2^-8 subtexel and LOD steps, the measured coordinate difference, and local texel differences. Remapped UV channels need Float32 storage. Half-precision page UVs moved samples by up to four texels. With anisotropic filtering, small derivative differences changed the tap selection on rare pixels. The errors reached about three times that bound. The specification does not define anisotropic taps. A candidate support border equal to the major-axis length plus one coarser-level texel held in all 48 measured cases. These are measurements from one GPU, not approved constants. See `docs/superpowers/investigations/2026-11-19-atlasing-coordinate-precision-spike.md`.

User decision on 2026-11-19: Treat anisotropic tap-selection differences like the accepted compression risk from 2026-11-17. Qualify them through recipe fixtures and per-output quality checks. Do not add a separate anisotropic allowance. This decision does not permit contamination from neighboring regions. The support border must still cover the anisotropic footprint. The decision does not waive the subtexel rounding bound, final alpha proofs, Float32 storage for remapped UVs, or role-specific error limits. No numerical limit is set.

### 15.3 Candidate lifecycle and bounded work

The shared layout defines selection units before separate candidates are attempted. A unit includes every use and admitted variant on that shared page. A material-swap slot cannot straddle units. A separate candidate can create proposed alpha output slots, but it cannot change unit membership or borrow allocation from another unit.

Resolve recipes, effective settings, source identities, and the provisional shared placements before expensive output generation. Count acquisition and source analysis against the work budget even when no candidate survives. Preflight every decode or staging allocation before creating it. Permit bounded per-build reuse of decoded source images keyed by effective texture identity, import interpretation, and resolution. Share decoded inputs, not unit-specific reductions or verdicts. Charge retained buffers to temporary work and release them at build cleanup. No cross-avatar or persistent image cache is required.

Try at most one separate layout and then its one shared layout. Within a layout, each output gets its selected format and at most one Automatic replacement under the existing toggle. A replacement cannot change placement, dimensions, or map resolution. Alpha analysis occurs after the final format is chosen. Its failure cannot request another format or layout beyond the one shared fallback.

Retain alpha in any image whose sampled alpha supplies opaque permission. If either an Automatic choice or a manual format would erase required evidence, treat that format as incompatible. Use the allowed Automatic replacement or reject that candidate. Opaque compression settings do not grant permission to erase alpha first. No RGB-only removal is required for the first complete atlas path.

Release a rejected candidate's owned output textures before generating its fallback. Keep only the prepared inputs that the fallback still needs. Charge the overlap of inputs, encoder buffers, readback buffers, and live outputs to peak temporary work. Track cumulative work separately from peak allocation so sequential failed candidates cannot evade the work bound.

Packing has one finite dimension ladder and one placement method. Component count, total copied pixels, page count, admitted variants, source reads, encodes, and predicate reads have explicit finite limits. Reaching a limit rejects the dependent candidate before further work. Numeric limits require public fixture measurements. This document does not invent safe values without those measurements.

The aggregate allocation account includes all retained effective source textures, including uses outside AMUSE's captured conversion surface. A missing proof that the last source reference is removed means the source remains counted. A generated texture counts once per actual retained allocation, not once per material reference. Any post-selection removal updates references and counts without repacking or assuming newly available budget will be reused.

### 15.4 Density calculation

Measure each usable triangle in the effective undeformed reference mesh. Let A be its physical area with the effective transform scale included. Let P be the absolute area of its mapped coordinates in acquired reference-map pixels at the import-resolution ceiling, before normalization. Its linear density is the square root of the ratio P to A. Use the same deterministic main-color reference rule for every use. A zero, non-finite, or unavailable area is unusable.

Compute the group's target as the physical-area-weighted mean of those linear densities. Count each participating physical triangle once, not once per material variant. Admitted variants use their established common preparation grid. Variants that cannot share that grid do not enter the same atlas conversion. Compute the target before page assignment and alpha cuts.

For each compatible shared placement, compute its own physical-area-weighted mean density over its usable participating triangles. Its full-strength scale is the smaller of one and the group target divided by that placement density. This gives the shared image one scale. Individual surfaces can remain above or fall below the target because compatible shared storage takes priority over exact equality.

A placement with any participating triangle whose normalization measurement is unusable keeps scale one. This preserves its density without duplicating compatible shared content. Unusable triangles do not contribute to either mean. If no group measurement is usable, normalization makes no reduction. Otherwise, interpolate from scale one to the full-strength scale using the selected strength, then apply that same scale to all dependent maps except an explicitly retained-resolution generated alpha mask.

Round reduced dimensions upward to whole pixels, without exceeding the acquired dimensions at the effective import-resolution ceiling. This avoids additional detail loss from rounding. Do not then shrink them to fit a page or compression block. Keep exact mapping metadata for the resulting dimensions. The common placement must still pass the sampling and relative-resolution rules or preserve the dependent use.

Required proof includes repeated references to one image, unequal physical areas, mirrored UVs, degenerate coordinates, multiple material variants, and a shared placement spanning densities on both sides of the target. Validate density and allocation separately. A smaller sampling scale does not prove smaller retained allocation.

### 15.5 Initial supported cases and evidence gates

Use the characterized source-import-copy route as the first acquisition recipe to verify. It is not a universal decoder. Add supported generated-input capture through the same image-evidence boundary. Characterize color interpretation, alpha processing, and normal packing separately. Never reinterpret packed normals as ordinary color.

Use a simple static main-color route in each supported shader family as the first end-to-end fixture. Then verify active dependent maps, admitted swaps, locked reconstruction, and visibility patterns explicitly. Do not claim a shader family is fully supported from the four spike renders. Unsupported routes remain named local refusals, not guessed conversions.

Begin visibility equivalence with proven constant effective visibility and identical captured visibility functions. Such a function includes parent activation and renderer enablement. Identical functions require the same resolved animation sources and the same admitted transition behavior, not just matching parameter or clip names. Different or unknown functions stay separate. Exact host capture rules still require source inspection and vendor-backed tests.

The first format candidates to characterize are BC1 for color without required alpha, BC3 for alpha-bearing color and scalar masks, and BC5 for tangent-space normals. These are candidates, not an approved format menu. Role-specific channel loss, source interpretation, platform support, and actual shader decode behavior can reject them. BC3's existing spike evidence does not verify mask quality or all color cases. Add another format only for a demonstrated supported need, not menu completeness.

Sampling evidence on 2026-11-16: The disposable sampling spike preserved aligned crop samples through protected mip two, including one Repeat boundary and controlled derivative cases. Misalignment, missing neighborhoods, and the shared distant-mip tail failed their comparisons. These observations support the narrow construction, not a general support bound or useful strict-mode packing. BC3 also showed large local color errors against the uncompressed prepared reference. A bounded BC7 comparison reduced those color errors but changed alpha. Neither format receives unconditional acceptance. Keep sampling preservation, compression quality, and final alpha proof as separate gates. The evidence and limits are in `docs/superpowers/investigations/2026-11-16-atlasing-sampling-spike.md`.

Quality acceptance needs a fixed role-specific bound against prepared-source sampling. Normal error is angular error after shader decoding. Color and emission comparisons use the established color interpretation. Masks and alpha also need threshold-sensitive cases. A global average error alone cannot hide a visible local defect. Numeric tolerances require public fixture evidence. Neither ordinary mode nor strict mode is verified until those tolerances are set.

User decision on 2026-11-17: Compression can cause visible shading changes even when generated textures pass their texture-error limits. Qualify shader recipes with public fixtures. Apply texture-output checks to each generated image. Do not add a system that renders each avatar to judge visual quality. A small texture error does not guarantee a small visual change under every light. This acceptance does not waive role-specific error limits, final alpha proofs, or the selected sampling protection. Numeric limits and supported recipes still require evidence. This decision does not approve the full design or authorize product implementation.

Normal and mask evidence on 2026-11-16: The disposable role spike found lower angular error with BC5 than BC3 for its two explicit normal decoders, but both had large local errors on the hard fixture. Averaging encoded normal components was not equivalent to the unit-vector-average reference. Both BC3 and BC7 changed synthetic mask threshold decisions. BC7 also changed fully opaque alpha without crossing the tested threshold. Do not infer opacity from threshold stability or infer decoder support from a format name. Keep role quality and actual final alpha proof separate. No tolerance or vendor recipe is approved by this spike. The evidence and limits are in `docs/superpowers/investigations/2026-11-16-atlasing-normal-mask-spike.md`.

Vendor source review on 2026-11-16: The inspected normal helpers combine sampled red and alpha and apply strength in a branch-specific order. A format name does not establish the sampled packing or active decoder. Main alpha masks are continuous operations whose later coverage consumers differ by shader and pass. Characterize actual decoding, normal strength dependencies, live cutoff state, and all applicable alpha passes before approving a recipe. Separate alpha extraction keeps tint and other remaining multipliers live. It does not bake an existing mask product. The source evidence, rejected shortcuts, and bounded next fixture matrix are in `docs/superpowers/investigations/2026-11-16-atlasing-vendor-role-contracts.md`.

Vendor render evidence on 2026-11-16: The disposable vendor spike matched uncompressed RG and AG normal representations at strengths one half, one, and two in both installed shader families. Compression changed the actual lighting response, especially at strength two. Static alpha extraction with mask multiplication preserved live tint in all eight uncompressed comparisons. Compression changed alpha and some cutout output. Replacement lost the tint factor. A minified opposing-pattern fixture rejected baking independently sampled products in both families. Keep those dependencies and final-output checks in the recipe. The evidence does not approve numeric tolerances, non-forward passes, source import routes, or arbitrary shader states. See `docs/superpowers/investigations/2026-11-16-atlasing-vendor-render-spike.md`.

Migration must keep explicit stored false values and fill only absent atlas options. Use one bounded schema migration and one resolved settings snapshot. Prove old-component deserialization, old presets, explicit false, Reset, Undo, and the first user edit before choosing the serialized representation. Do not add marker fields or a generic settings registry without establishing why ordinary versioned migration cannot preserve the choices.

Migration evidence on 2026-11-17: A disposable native prefab omitted an existing default-true field while retaining a different explicit false. Unity applied the initializer only to the missing field. Native save and reload preserved the first false edit. Editor Reset restored defaults and Undo restored both false choices. The current preset parser separately retained its closed schema-five behavior. Prefer ordinary field initializers plus Reset for new atlas fields. Do not add a new atlas marker without a demonstrated need. The actual new fields, preset migration, prefab variants, scene loading, and domain reload still need their own acceptance evidence. See `docs/superpowers/investigations/2026-11-17-atlasing-migration-spike.md`.

As of 2026-11-16, numeric memory limits, compression quality bounds, exact host visibility capture, serialized absence detection, and useful strict-mode cases still need evidence. They are not more user preference questions. Resolve them through focused investigation and disposable characterization before asking for design approval. No production code is authorized by the recommended construction above.

## 16. Review disposition

Review status on 2026-11-16: Both isolated reviews and their follow-up checks completed. Both reviewers found no remaining material contradiction in their document scope after correction. Neither review is runtime evidence or user approval. Open evidence gates still require focused characterization.

Accepted maintenance corrections: Form groups before computing reductions. Distinguish acquired images before normalization from prepared images after normalization. Permit bounded per-build reuse of decoded inputs without sharing unit-specific decisions.

The recommendation to remove material-type compression settings is not adopted. The user explicitly requested the two-category matrix. The proposed initial formats do not establish the final supported menu or erase that request.

The recommendation to remove density-reference fallback is not adopted. The user selected main color where usable, then the highest-density usable map. Pixels per physical unit remain the measured quantity. The fallback can change the result and is now explicit. Evidence must cover this consequence. It is not a claim of equal color-map density.

The recommendation to remove component-migration evidence is not adopted. Serialized field defaults may provide the simplest solution, but the review did not run Unity deserialization. Verify that path before adding migration machinery. Preset absence and explicit false still require checks. No custom absence detector is mandated.

Accepted sampling corrections: State the label-boundary cut explicitly. Give retained-alpha evidence priority over both Automatic and manual format choices. Exempt a newly extracted alpha mask from color reduction within its source ceiling. Derive overlap from all protected levels. Add sampling-grid phase and deterministic unprotected-tail construction.

The proposed clamp of alpha-tested levels to protected levels is not adopted. The controls intentionally answer different questions. Truncating tests could remove a failing eligible level and grant an opaque result with less evidence. This revision instead states the atlas-first policy and the limited scope of alpha acceptance explicitly. Strict sampling does not silently expand or replace the selected alpha policy.

Maintenance follow-up on 2026-11-16: The reviewer re-read the corrected passages and confirmed that the preparation order is no longer circular. It accepted bounded decode reuse and the documented dispositions for density reference, compression controls, and migration. It found no remaining material definition defect in its scope. Its remaining wording correction now names acquired dimensions as the reduction ceiling. This is document review, not runtime validation.

Sampling follow-up on 2026-11-16: The reviewer re-read the corrected record and accepted the label cut, retained-alpha precedence, separate-mask scale exception, and all-level margins. It withdrew its proposed alpha-scope clamp after checking the declared atlas-first policy. It confirmed that image protection and alpha-test scope are separate and that the remaining evidence gates are explicit. Its final wording correction now names the shared-placement rule for unusable measurements.

## 17. Adversarial review and decisions on 2026-11-19

Review status on 2026-11-19: Four isolated read-only reviews examined this design, the delivery draft, the handoff, and the evidence records. Their lenses were soundness, sampling numerics, vendor routes, and plan governance. They checked claims against checked-out product source and installed vendor source. They found four blockers and about thirty major defects. The reviews are document and source review, not runtime evidence. This section controls where an earlier section is less specific.

### 17.1 User decisions on 2026-11-19

Anisotropy: Atlas routes honor the existing per-platform settings that accept anisotropy differences. Those settings govern alpha consequences. When the setting for the build's sampling group is on, the anisotropic acceptance in section 15.2 applies to alpha proofs. When it is off, atlas alpha analysis keeps the existing conservative rule. An anisotropic level that is not fully opaque then gives Unknown. Engineering reading: image-sampling differences in color, normal, and mask maps stay under the section 15.2 acceptance in both cases, because the settings describe transparency only. Per-output texture checks cannot observe tap selection. Only rendered recipe fixtures qualify anisotropic behavior.

Build targets: Android and iOS support is not required for 0.2.0. Build the feature in preparation for them. In 0.2.0, atlasing runs only when the active build target is Windows. Other targets keep every atlas-dependent source use and report one named refusal. Keep target-specific facts in data keyed by build target: sampling precision constants, output formats and their block sizes, import interpretation, and support evidence. 0.2.0 has one row. Adding a target adds evidence and a row, not a new pipeline. Do not build target abstractions that no evidence needs.

Locked materials: The choice needs a spike. The working assumption on 2026-11-19 is that the vendor's upload lock relocks materials during the build. Section 17.4 records the spike result. Decision on 2026-11-19: Admit atlas uses whose outputs use an unlocked Poiyomi shader, including slots from the transient unlock window, provided that this path is functional. Engineering reading of the condition: the live locked-output check of Milestone 6 passes for an atlas output and for a window slot before release. If it does not pass, these uses are not admitted.

Apply-time fallback: Decision on 2026-11-19: Accept section 17.2 item 11. An atlas use that apply removes restores its source bindings without alpha separation in 0.2.0. Reason: a late fallback needs a second preparation path in apply or a model of combinable mesh edits. Both touch the mesh write, the slot-index curve rewrite, and revalidation. If measured builds show that apply-time removals are common, combinable mesh edits are the candidate for a later version.

Version consent: Atlas conversions admit only exactly attested shader rows. The consent dialog does not extend atlas recipes. A use on a consented but unverified version keeps its source bindings and its ordinary alpha path.

Alpha separator off: Split the gate. Alpha separation and atlasing have independent enable switches. Turning off alpha separation does not disable atlasing. Shared prerequisites run when at least one feature is on. These are the host lifecycle gate, the structural animation graph, and the unlock window. When both features are off, the existing contract holds: nothing is analyzed, mutated, or reported, and the consent dialog never asks. Engineering consequences: the consent dialog serves alpha separation only, so atlasing alone never shows it. The structural graph refusal applies to both features, because atlasing also depends on closed animation evidence.

Presets: An older preset that has no atlas keys leaves the component's atlas values unchanged. This supersedes the 2026-11-16 rule for older presets only. Upgraded components still take field defaults for absent values. The shipped Safe, Normal, and Aggressive configurations carry every atlas field. Each field holds the global default as a placeholder. Per-preset atlas values remain open. A later user decision replaces the placeholders.

### 17.2 Engineering corrections

These corrections are proposals. They do not have runtime evidence. An isolated read-only review checked them against product and vendor source on 2026-11-19. That review found eight majors and no blockers. The text below includes its required changes. Items 20 to 24 are corrections that the review added.

1. Output evidence identity. A fresh AMUSE page has no evidence identity, so every final alpha verdict on it is Unknown. Two existing paths accept an attested route texture without stored alpha. `UnityTextureEvidence.TryProveSampledAlphaIsOne` proves alpha one when the format has no alpha channel. The chain capture admits DXT1 and reads alpha 255 on every texel through its blit. Both vendor interpreters read that flag before they check identity. Add two guards. First, an AMUSE-owned output never proves alpha one from its format. Second, an AMUSE-owned texture whose format has no alpha channel yields no alpha chain, so its verdict is Unknown. Its opaque proof needs stored alpha, as section 15.3 requires. Mint the identity from AMUSE-owned objects in per-build state, not from a name or shape. The session cache rule forbids a new static registry. The existing generated-texture attestation admits names that end in `_baked`, `_compressed`, or ` (AAO UV Packed)`. An AMUSE output must never match those names. Falsifiers: one per guard, one end-to-end case with a transparent source in a BC1 page, and one name-collision case. Revalidation reads the page's own content hash between proof and apply. The dry run never reads AMUSE-owned outputs.
2. UV-value consumers. Some features read UV values as data, not as texture coordinates. Each supported shader row declares its list across channels zero through seven. As of 2026-11-19 the source shows these consumers. lilToon: UDIM discard on channels zero to three, IDMask on channels zero to seven, the backface UV shift on channel zero, glitter, AudioLink, dissolve, the emission, layer, and second normal-map UV modes, and fur noise. Poiyomi: UDIM discard (`_UDIMDiscardUV`), UV Tile Dissolve, vertex masks, glitter, AudioLink emission, dissolve detail noise, parallax that writes the UV array, and stochastic sampling. Poiyomi reads only channels zero to three. A material that enables any listed consumer in any admitted state keeps the affected channel unchanged. The lilToon outline-width mask is a channel-zero route texture with the main transform, not a raw read. Item 5 covers it. Add one falsifier per row entry. Milestone 2 owns the falsifiers for the channel that its first route rewrites.
3. Sampler compatibility. In both vendors, most textures borrow the main texture's sampler. A page that replaces the main texture imposes its wrap, filter, and anisotropy on every texture that borrows that sampler, including preserved textures. Resolve the effective sampler per shader route, not per texture. A named inline sampler fixes wrap and filter by its name, whatever the importer says. Both vendors declare clamp and repeat inline samplers. Atlas group membership requires equal sampling facts: filter mode, wrap per axis, mip bias, anisotropy mode, and anisotropy level. The existing capture has one wrap field, no mip bias, and an anisotropy check by eligibility only. Extend the capture. Mipmap limit group, streaming mips, mip filter, and coverage settings must also be equal [INFERENCE]. The page takes the group's shared sampling facts. Point filtering is unsupported in 0.2.0. Falsifiers: a preserved Repeat-tiled texture that borrows the main sampler, and two members with anisotropy levels one and sixteen.
4. Page-size readers. Poiyomi pixel mode reads the main texture size. Poiyomi sharpened alpha-to-coverage reads the main texture size and reads channel zero directly. Either mode makes the use non-atlasable. The lilToon shadow-mask level floor applies when its level property is above zero. It is a constant in UV units, so a region whose UV scale changes selects another level. Refuse those routes only when the floor is active. Falsifiers: pixel mode, sharpened alpha-to-coverage, and an active floor.
5. Per-pass coordinates. Role contracts record the UV source, the transform, and the sampler per pass, per stage, and per consumer. The lilToon outline fragment stage uses the outline transform. Its vertex stage uses the main transform for the width mask. The lilToon second normal-map scale mask uses the main transform and its own transform. Its own transform must be identity. Require equal transforms per pass and stage, or preserve the use.
6. Final-analysis prerequisites. The lilToon frontend refuses a non-identity main transform and any nonzero main scroll or rotation. A lilToon opaque proof on an atlas therefore needs a mesh UV rewrite with an identity transform. Shader-intrinsic motion preserves the use in both vendors: pan, scroll, rotation, parallax, parallax occlusion, and decals. Each supported Poiyomi row declares its motion properties. Add one falsifier per vendor.
7. Whole-material equality. One capture owns the comparison of all material facts that are not texture bindings. It covers properties, stencil, cull, depth test, color mask, depth offset, keywords, override tags, render queue, disabled shader passes, instancing, global illumination flags, and shader identity including lock state. The merge-ready check is this equality across the page set plus the binding rules in section 1. Add one falsifier per fact. Its observable result is one report line per page set.
8. Revalidation inventory. Before the first write, apply revalidates these facts for each atlas use: a hash of all mesh data that the clone copies, captured material property values, texture identity and a content hash for sources and AMUSE pages, the property-block fingerprint, visibility inputs, the build target, and the settings snapshot. Mesh data includes vertex attributes, indices, blend shapes, bone weights, bind poses, and bounds. A changed fact refuses the dependent use.
9. Property blocks and renderer-wide animation. A property-block entry or animated property on any texture, transform, or selector property of the converted recipe refuses the affected uses. An entry for one slot refuses only that slot. A renderer-wide entry refuses every atlas use on that renderer. The existing refusals for additive layers and for direct blend trees that are not normalized also apply.
10. Existing alpha controls. Folding, coverage, polygon, and opaque-alpha controls apply to the final atlas analysis exactly as they apply to a source analysis. This includes the separate cutout and transparent switches. A fold stays an accepted approximation under its control. It is not a proof. Add a falsifier with a slot that qualifies for folding.
11. Ordinary fallback lifetime. Split classification from preparation. Classify source alpha once. Prepare the ordinary alpha separation only for uses that selection preserves. An apply-time removal of an atlas use restores its source bindings without alpha separation in 0.2.0, and the report names it. Superseded uses emit no ordinary refusal records. This changes section 6, which says a failure uses the ordinary source alpha path. The user accepted this on 2026-11-19. Test: an atlased use creates no opaque clone.
12. Refusals and invariants. Expected outcomes are a closed set of named refusals: a reached limit, an unsupported recipe, a failed final proof, and a missing host capability. A failed readback request is a host-capability refusal, as it is in the existing capture. Invariant checks throw for generation outputs: format, dimensions, and mip count must equal the request. A failed generation never covers a thrown exception. Test: an injected invariant failure reaches the build as an exception.
13. Growth after removal. The growth guard applies to the selected plan. After apply-time removals, report the realized allocation and any overshoot. An overshoot is reported. It does not count as a pass of the growth guard. Do not repack.
14. Float32 output. Every remapped UV channel is Float32 in the output mesh. A remapped Float16 channel becomes Float32. A test reads the built mesh's vertex format. A route that cannot keep Float32 preserves the use.
15. Prepared dimensions. Let M be the coarsest protected mip level, without margins. Density reduction rounds each prepared dimension up to a multiple of two to the power M times the block edge. If that exceeds the acquired ceiling, the placement keeps scale one. A source whose acquired size is not a power of two keeps scale one and keeps its use unless a fixture qualifies its placement.
16. Sampling scope. The rounding bound is stated within an enlarged domain. The support border and the protected level range include the coordinate and level margins. The level margin is assumed, not measured, as of 2026-11-19. Define the comparison in decoded sample space so that sRGB maps have a defined bound. Name the maximum anisotropy ratio that the border assumes as one constant. Unity's highest anisotropic level is 16. Add an sRGB falsifier.
17. Strict mode. As of 2026-11-19, no useful strict-mode case fits the allowed detection rules. The periodic crop of 2026-11-17 needs a periodicity search, which this design forbids. A useful case stays an open gate. If the gate does not close, removing strict mode needs a user decision.
18. Evidence caches. The session caches for alpha chains count as temporary work. Evict a rejected candidate's entries by instance before its fallback. Do not clear the whole cache during a build. Test: source chains survive the eviction.
19. Density definition. Section 15.4 controls. Density is linear: the square root of pixel area over physical area. Section 9 now states this.
20. Alpha separation switches and the separate candidate. The atlas separate candidate and its opaque conversion obey the alpha separation switch and its cutout and transparent sub-switches. When alpha separation is off and atlasing is on, source alpha classification still runs for atlas grouping. No opaque clone is prepared.
21. Closure over a rewritten channel. Rewriting a channel moves every texture that the shader samples from it. Examples are lilToon shadow, bump, and fur masks on the main route. Every consumer of a rewritten channel is a page member, or the use is preserved.
22. Plan order. Milestone 2 rewrites UVs. It owns the UV-value, sampler, and channel-closure checks for its one route. Milestone 6 extends them to the other routes.
23. Import facts beyond the sampler. Item 3 lists them. They need a source check before Gate 0 closes [INFERENCE].
24. Row scoping. Each vendor fact names its shader row. The product targets the Poiyomi 9.3 rows. Features found only in the 10.0 shader do not apply to them.

### 17.3 Gate structure

Gate 0 defines contracts, not numeric work and growth limits. For each counter, Gate 0 states its unit, its scope per unit or per build, its trip rule, and the instrument that measures it. Tests inject limits through a production delegate seam. A release gate sets the numeric values from measurements of production code before release. No invented constant ships.

### 17.4 Locked-material relock spike

Source spike on 2026-11-19: NDMF saves generated objects into its build container at callback order -1025. The vendor upload lock runs later at order 100 and selects materials that have an asset path. It bakes current transforms and UV selectors as literals. A lock failure does not stop the build, and an unlocked output in a player build then renders broken.

Live probe on 2026-11-19, approved by the user: The dev editor ran the installed SDK preprocess chain on two in-memory avatars. The SDK build type was set to Test. A Build and Test uses that value [INFERENCE]. AMUSE converted one Poiyomi 9.3 slot. The shipped AMUSE output was locked. A material that only an animation clip references was locked. VRCFury blocks the vendor lock when no SDK build is active. That covers Play mode and test copies. Those outputs stay unlocked, and the vendor stripper does not run there. See `docs/superpowers/investigations/2026-11-19-atlasing-locked-relock-spike.md`.

Decision on 2026-11-19: The user approved this route, provided that it is functional. Section 17.1 states the condition. Rely on the vendor upload lock, as alpha-separation outputs already do. Keep the live check as a regression fixture behind the existing vendor opt-in gate.
