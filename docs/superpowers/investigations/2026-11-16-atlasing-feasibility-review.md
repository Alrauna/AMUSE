# Focused feasibility review for atlasing

Privacy note: This record contains public source findings and synthetic examples only. It contains no private asset observations or vendor source excerpts.

Date: 2026-11-16.
Status on 2026-11-16: Design investigation only. No implementation is authorized by this record.

## 1. Scope and evidence

This review examines texture acquisition, separate alpha masks, paging, and alpha analysis. Answer 52 later changes the analysis order. It does not remove existing alpha controls.

Two read-only investigations examine texture acquisition and shader equations. The parent examines paging and fallback rules. A separate reviewer receives only the revised design.

No Unity operations, builds, or product tests ran for this review. Source inspection cannot establish output quality or support for a generated shader variant. Two exact arithmetic examples ran in a throwaway interpreter cell. No script file was created.

## 2. Sampling limits

Finding on 2026-11-16: A finite border cannot preserve arbitrary independent textures through a shared full mip chain. The last mip contains one texel. Distinct source constants cannot both equal that texel.

The synthetic example uses source alpha values zero and one. A shared equal-area tail contains one half. That value preserves neither source. This is a counterexample to a general preservation claim, not a simulation of Unity mip generation.

Finding on 2026-11-16: Filtering a baked mask product can differ from multiplying independently filtered masks. The synthetic example uses opposing two-texel patterns. Independent filtering gives one quarter. Filtering their pointwise product gives zero.

Existing analysis does not solve this distinction. `Packages/com.alrauna.amuse/Editor/Analysis/AlphaSamplingPolicy.cs:37-57` changes only the assumed anisotropy. It does not constrain the atlas mip tail. `Packages/com.alrauna.amuse/Editor/Analysis/TriangleAlphaClassifier.cs:144-147` explains the separate conjunction over contributing mip levels.

Recommendation on 2026-11-16: Do not advertise general strict sampling for a packed full mip chain. Keep the requested control unresolved until useful supported cases pass a focused proof. Do not add shader changes, texture arrays, or runtime level limits merely to rescue the control.

Earlier recommendation on 2026-11-16, before answer 52: Treat alpha preservation as an atlas admission constraint. That would preserve dependent source uses when the frontend cannot establish the contract.

Decision update on 2026-11-16, answer 52: Determine alpha after atlasing wherever feasible. Prioritize a valid atlas if separation cannot proceed. Apply the separator's mip-testing rules where necessary. The earlier alpha-preservation admission constraint is superseded. An opacity proof still does not establish unchanged partial transparency.

## 3. Simple paging and fallback

Proposal on 2026-11-16: Use a deterministic rectangle packer without rotation. Start with used-region bounds rather than polygon interlocking. Preserve the option to improve packing later without changing the output contract.

Sort regions by decreasing longest side, then area, then stable capture order. Choose a fixed supported dimension ladder. Search that finite ladder once for one page. Add a page only when the current page cannot fit the next region. An empty page that cannot fit a region refuses that dependent conversion.

Use one placement layout for related maps and admitted material variants. Derive map dimensions and borders before placement. Different resolutions require aligned normalized placements. A map with a larger required border can increase the common placement bounds.

Do not create pages to chase minor savings. Do not repack after output refusal. Empty space from refused uses stays in the surviving allocation estimate. Numeric work, page-count, and memory-growth limits still require public fixtures.

A maximum page dimension does not bound build memory. Source decode buffers, prepared images, generated variants, and compression staging can exceed final texture allocation. Add a bounded temporary-work limit to the existing candidate and avatar growth limits. Release thresholds remain open.

Proposal on 2026-11-16: Track the dependencies of each conversion in existing preparation records. A surface use includes all its admitted states. Do not leave half of a swap conversion or incompatible coordinate assignments active.

If a generated page fails, reject all conversions that require that failed page. Reject any other output that requires one of those rejected conversions. This is a finite removal process. It neither creates pages nor changes a baseline. Unrelated conversions survive.

If atlas generation fails, obtain the ordinary source-based alpha result at most once when needed. Never apply an atlas-derived verdict to restored source textures. Unsupported alpha separation alone does not discard a successful atlas under answer 52.

Generated ownership must include textures as well as meshes and materials. On 2026-11-16, `Packages/com.alrauna.amuse/Editor/Build/AlphaSeparationRecords.cs:185-261` tracks opaque materials by source. That map cannot represent all atlas uses and page dependencies.

## 4. Texture acquisition

Observation on 2026-11-16: Existing capture returns alpha proof data, not general texture pixels. The generated route reads a predicate output and reduces it to verdict bytes. It does not return the effective color or normal image.

Evidence: `Packages/com.alrauna.amuse/Editor/Host/UnityGeneratedTextureEvidence.cs:139-188`. `Packages/com.alrauna.amuse/Editor/Host/SourceImageAlphaReader.cs:10-31` states the disk reader's alpha-only purpose.

Observation on 2026-11-16: The disk route admits PNG files. It rejects grayscale-derived alpha and mismatched decoded dimensions. It accounts for imports that remove alpha. Its caller admits a closed set of uncompressed imported formats.

Evidence: `Packages/com.alrauna.amuse/Editor/Host/SourceImageAlphaReader.cs:46-92,178-207`. `Packages/com.alrauna.amuse/Editor/Host/UnityStreamingTextureEvidence.cs:215-217` owns the imported-format gate.

These rules protect existing alpha evidence. They do not establish faithful RGB reconstruction for every PNG import. They also do not implement the agreed source-file reconstruction at a lower effective imported resolution.

Observation on 2026-11-16: The source records distinguish sRGB and linear interpretation. They also recognize that normal-map imports can store a normal component in alpha. Copying authoring channels without their interpretation can change behavior.

Evidence: `Packages/com.alrauna.amuse/Editor/Semantics/UnityTextureEvidence.cs:162-220`.

Recommendation on 2026-11-16: Keep one acquisition result per effective texture and interpretation. That result supplies full pixel content, dimensions, and its supported meaning. Prefer matching disk content only when its import reconstruction is established. Otherwise use a characterized capture of the effective texture. Preserve the dependent use if neither route exists.

Observation on 2026-11-16: The streaming route already copies an imported source into an owned temporary asset. It changes readability and streaming on that copy, then imports it. It leaves the source import configuration unchanged.

Evidence: `Packages/com.alrauna.amuse/Editor/Host/UnityStreamingTextureEvidence.cs:237-308`.

Recommendation on 2026-11-16: Characterize an owned import copy before writing an importer emulator or adding image libraries. Let Unity apply supported source-format interpretation and the effective resolution limit. Remove lossy compression only on the owned copy. This changes content relative to the existing evidence route, so it needs separate proof. Preserve platform overrides, normal conversion, alpha processing, and custom-importer refusal rules. Include temporary import cost and cleanup in the work limit.

PNG is a useful first characterization case, not an approved PNG-only product scope. A compressed imported format does not automatically forbid reconstructing a new output from its source. Its alpha evidence gate serves a different purpose.

Do not make imported dimension equality a permanent atlas restriction. The agreed density ceiling requires reducing larger disk sources to the effective imported resolution. That reduction needs explicit color, alpha, and normal rules.

No source evidence reviewed on 2026-11-16 establishes a full-pixel capture for generated inputs. Existing identity and alpha capture can inform that work. They do not prove the new capture.

The exact output format set remains open. A working RGBA buffer does not imply an uncompressed shipped texture. Compression, mip generation, normal encoding, and actual output sampling need a focused Unity characterization before a release claim.

## 5. Recommendations rejected from the source review

Earlier rejection on 2026-11-16, before answer 52: The texture investigation proposed classifying only after packing. That conflicted with the selected pre-packing baseline. Answer 52 supersedes that rejection. Post-atlas classification now follows the user's chosen priority. This does not establish the investigation's proposed pixel generation as correct.

It also proposed plain integer box averaging as a general reduction rule. Alpha-only arithmetic does not establish correct sRGB color filtering or tangent-space normal filtering. No general pixel pipeline is accepted from that suggestion.

It proposed omitting generated inputs from initial acquisition. The user accepted a supported effective-texture fallback. No blanket omission is an approved scope cut.

## 6. Separate alpha masks

Observation on 2026-11-16: The installed shader sources expose replace and multiply mask equations. Both inspected paths sample the mask through the main texture sampler. lilToon uses main-derived coordinates with a separate mask transform. Bundled Poiyomi 9.3 exposes a separate mask coordinate selector.

Evidence: `Packages/jp.lilxyzw.liltoon/Shader/Includes/lil_common_frag.hlsl:458-475`. `Packages/com.poiyomi.toon/_PoiyomiShaders/Shaders/9.3/Toon/Poiyomi Toon.shader:29874-29886`.

These observations establish equation shape, not a supported extraction conversion. The common simple candidate has no active sampled mask and no earlier effect that needs main alpha. A recipe can move main alpha into the mask while preserving the remaining alpha multipliers. Its actual filtering, mip selection, animation, and generated shader route still need proof.

An existing replace mask can already separate color resolution from alpha resolution. It does not automatically need another mask. An existing multiply mask needs two independently sampled factors. Baking their product into one image does not generally preserve transparency.

Recommendation on 2026-11-16: Start extraction characterization with a static main-only configuration and an unused mask equation. Preserve tint alpha and every remaining multiplier. If an existing sampled mask prevents that recipe, skip extraction and keep the original equation during ordinary permitted reduction. Do not add another texture slot or a general equation baker.

Locked reconstruction checks texture identity while it reconstructs source meaning. That is an input guarantee, not proof that atlas texture substitutions are safe or permanently forbidden. Destination texture routes still need verified recipes.

Evidence: `Packages/com.alrauna.amuse/Editor/Build/LockedMaterialReconstruction.cs:61-79`.

The mask investigation claimed that matching grids and filter weights establish value preservation for a baked product. The two-texel counterexample in section 2 disproves that claim even with identical weights. An exact-one opacity predicate is a different property. It cannot establish transparent appearance.

The investigation also proposed reusing a one-source-to-one-replacement identity for every atlas output. A source can serve several atlas groups. That proposal does not fit the agreed output relationship. Reuse existing identity discipline, not a conflicting cardinality.

## 7. Isolated review

Review observation on 2026-11-16: A reviewer read only the revised standalone design. It confirmed the need to resolve the default alpha contract. It also found an unstated page assignment rule, per-binding swap retargeting, and the density default.

Accepted corrections on 2026-11-16: Define page assignment per planned output slot and retarget animation per binding. Strength zero remains the proposed density default. The design must distinguish an original slot from its alpha-separated output slots.

The reviewer recommended accepting distant-mip alpha differences. Answer 52 later changes the default contract to atlas-first processing and post-atlas alpha analysis. This does not turn missing evidence into an opacity proof.

The reviewer again recommended cutting normalization. That cut also requires user approval. Its claim that normalization alone requires different map resolutions is not accepted. Source maps can already differ in resolution. Acquisition and compression memory also exist without normalization.

Status on 2026-11-16, after answers 52 and 53: Post-atlas alpha analysis takes priority. Shared opaque and transparent pages are permitted only where necessary. Exact mip-scope mapping and bounded page selection still need resolution. Focused Unity characterization must establish acquisition, extraction, output formats, and useful admission. Measured limits remain a release gate, not an invented number in this review.

## 8. Post-atlas mip scope

Source observation on 2026-11-16: The separator combines the maximum tested level with the minimum tested texture size. A texture below the minimum at level zero contributes no evidence. The cutout and transparent arms retain separate values.

Evidence: `Packages/com.alrauna.amuse/Editor/Host/UnityRendererAlphaAnalysis.cs:346-379`. The stored controls disclose ignored distant levels in `Packages/com.alrauna.amuse/Runtime/AmuseAvatarOptimizer.cs:50-72`.

Proposal on 2026-11-16: Carry source-equivalent resolution through cropping, normalization, and placement. Resolve proof scope per use. A large page does not raise the proof eligibility of a small source. A small crop does not by itself shrink the policy reference size. This requires a mapping rule, not another artist control.

Decision update on 2026-11-16, answer 53: The user permits shared opaque and transparent atlas textures only where necessary. This replaces the proposal to share them unconditionally after one packing pass. Prefer separate pages when the supported process can produce them safely within the limits. Analyze the actual selected final layout. Do not reuse an earlier layout's alpha verdict or introduce an unbounded packing loop. Preserve valid atlasing when separation is unsupported, as answer 52 requires.

Spike update on 2026-11-16: The user later authorized disposable Unity characterization. Results are in `docs/superpowers/investigations/2026-11-16-atlasing-throwaway-spike.md`. That record establishes narrow import, compressed sampling, and final-evidence cases. It proposes one separate candidate followed by one shared fallback. It does not establish a complete packer or authorize product implementation. Earlier source-only limits remain unless the spike explicitly tested them.
