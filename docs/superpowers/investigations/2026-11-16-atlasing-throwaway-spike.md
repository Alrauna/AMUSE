# Throwaway atlasing spike

Privacy note: This record contains public synthetic fixture results only. It contains no private asset observations or vendor source excerpts.

Date: 2026-11-16.
Status on 2026-11-16: The authorized disposable spike completed. Product implementation and the full design remain unapproved.

## 1. Scope

The spike ran in the dev editor with Unity 2022.3.22f1. The editor was pinned after its asset directory matched the repository exactly. No operations ran in the private editor.

Code compiled in memory through the editor execution tool. No script file or assembly was added. Synthetic PNG assets existed only inside `Assets/AmuseThrowawayAtlasSpike`. Each import run deleted that owned folder in a finally block. Rendering used transient textures, meshes, and materials without scene objects.

The spike characterized three import cases, two generated-pixel cases, four vendor UV renders, and fixed-layout alpha evidence. These are characterization observations, not Unity Test Runner test counts. No build, production test suite, general packer, or complete avatar conversion ran.

## 2. Source import reconstruction

Observation on 2026-11-16: Each synthetic disk image had a source size of 256 square pixels. A Standalone override limited its imported size to 128 square pixels. Unity first imported an uncompressed RGBA32 baseline, then a DXT5 source.

The spike copied the source image and import metadata through AssetDatabase.CopyAsset. Unity reimported that disk image without compression on the owned copy. This did not decompress the imported DXT5 pixels. The copy retained the size override and semantic settings. Both general compression and the explicit platform format changed. A general-flag-only change was not tested.

| Case | Reconstructed size | Mip count | Different base-level pixels from baseline | Source file and import metadata unchanged |
| --- | --- | --- | --- | --- |
| sRGB color | 128 by 128 | 8 | 0 | Yes |
| Normal-map import | 128 by 128 | 8 | 0 | Yes |
| Removed input alpha | 128 by 128 | 8 | 0 | Yes |

The color fixture contained gradients in red and green, plus two alpha bands. The normal fixture contained a shallow two-axis gradient. Equality used exact bytes with no tolerance. The comparison covered the complete base level after Unity reimported the source image. It did not compare every mip against the baseline. The byte checks compared each synthetic source file and its metadata before and after copying.

Recommendation on 2026-11-16: Use an owned import copy as the first acquisition route to develop. This evidence supports the tested PNG settings only. It does not establish every source format, platform override, custom importer, alpha processing mode, or normal-map configuration.

The reconstructed RGBA32 normal copy contained Unity's packed channel representation, not ordinary RGB normals. Its first pixel had red 255, green 110, blue 110, and alpha 110. It matched the uncompressed normal baseline. Treating those pixels as color would corrupt their meaning. Normal decoding, reduction, re-encoding, and compressed normal sampling still require dedicated characterization.

## 3. Effective generated pixels

Observation on 2026-11-16: The spike copied two transient textures through a linear render target. Both contained the same stored color and alpha. Their source color interpretation differed.

| Source interpretation | Stored red byte | Captured linear red byte | Expected linear red byte | Captured alpha byte |
| --- | --- | --- | --- | --- |
| Linear | 128 | 128 | 128 | 96 |
| sRGB | 128 | 55 | 55 | 96 |

Readback returned all four channels for the two constant RGBA32 cases. Assertions checked red and alpha only. The observed sRGB result was red 55, green 13, blue 134, and alpha 96. The linear result matched the stored channels. Green and blue had no independent assertions. The red comparison falsifies a raw-byte copy that ignores sRGB interpretation. Normal capture, HDR, all compressed inputs, residency handling, and arbitrary reductions remain unproved.

## 4. Compressed vendor UV routes

Observation on 2026-11-16: A synthetic atlas contained separate red and green regions. Unity generated its mip chain and compressed it to DXT5. A transient mesh carried distinct coordinates in UV0 and UV1.

The spike rendered the installed lilToon shader and the bundled Poiyomi 9.3 shader. Both used simple unlit configurations. Center-pixel readback established these routes:

- lilToon sampled red through original UV0 and green after UV0 remapping.
- Poiyomi sampled UV0's red region with its main selector at zero and UV1's green region with that selector at one.

All four color assertions passed. The Poiyomi green channel read 254 rather than 255. The assertion allowed the accepted small output difference. Its thresholds would reject the opposite region color. No separate negative-control execution was run.

The first Poiyomi attempt selected pass zero. Source inspection showed that pass was EarlyZ with ColorMask zero. Selecting the named Base pass corrected the harness. This was a spike error, not a product defect.

These observations establish sampled compressed output on the tested main-texture routes. They do not establish channel liveness, all active texture uses, locked shader conversion, animation, tangent-space normal behavior, or mesh attribute preservation.

## 5. Final-output alpha evidence

Observation on 2026-11-16: Direct asynchronous readback from DXT5 failed because the backend did not support compressed readback. The corrected route sampled each compressed mip through AMUSE's existing alpha predicate shader into an uncompressed render target. The production triangle classifier consumed those final predicate bytes through reflection. No classifier code was changed.

The fixture compared a shared 512 by 256 DXT5 page with separate 256 square pages. Its first separate opaque page used DXT1. A later falsifier exposed circular proof in that choice. The corrected evidence run used DXT5 for both separate pages. Each region was constant, so this case did not test complex island packing.

| Layout | Measured compressed texture data bytes |
| --- | --- |
| Shared DXT5 page, including mips | 174800 |
| Separate DXT5 pages, retaining alpha for proof | 174816 |
| Initial DXT1 and DXT5 pages, not conversion safety evidence | 131112 |

These are retained texture data sizes, not measured runtime residency or temporary peak allocation. They do not establish release growth limits.

Format falsifier on 2026-11-16: A constant input alpha byte of 96 became 255 after DXT1 compression. The predicate then reported opaque. DXT5 retained alpha 96 and reported non-opaque. An alpha-dropping format cannot establish its own opacity permission. The corrected candidate run retained alpha during final analysis. Both required opaque checks still passed.

Recommendation on 2026-11-16: Keep alpha in validation inputs. Treat RGB-only encoding as a later opaque conversion, not an atlas proof shortcut. A safe production sequence for that encoding remains unresolved. The smaller initial allocation is a storage opportunity, not a validated optimization. The alpha-retaining separate layout slightly increased storage in this fixture.

The configured maximum tested level was four. A source-equivalent size of 256 and a minimum size of 128 reduced the admitted scope to levels zero and one. The shared opaque-region triangle passed both eligible levels. The separate opaque triangle also passed both. The separate transparent triangle remained transparent at both levels.

A source-equivalent size of 64 yielded no eligible evidence even on the large shared page. The harness returned Unknown without classifying that use. This exercised the proposed eligibility rule, not a production implementation of the mapping.

A wider diagnostic scope included levels zero through eight with a minimum size of one. The shared-region triangle passed through level seven and failed at level eight, whose page dimensions were two by one. The page had ten levels. A separate diagnostic read of level nine, its one-texel tail, returned alpha byte 127. These observations confirm the distant-mip conflict on actual generated output.

The rejection fixture gave a proposed opaque page an unextended transparent half. Its triangle passed levels zero through six and failed levels seven and eight. This deliberately invalid candidate tests stale-verdict rejection. It is not a recommended edge-extension recipe.

## 6. Bounded selection proposal

Observation on 2026-11-16: The harness selected separate output when its required final opacity checks passed. It selected the shared result when the alternate candidate failed its final checks. Each scenario considered one separate candidate and one shared candidate. It did not iterate.

The harness generated both layouts for comparison. It used explicit rectangle placements. It did not discover islands, implement rectangle placement, solve material variants, or exercise the NDMF apply boundary.

The rejection scenario used deliberately invalid edge content, not an observed failure of a correct packer. No realistic separate-page failure or memory-limit fallback was exercised.

Recommendation on 2026-11-16: Try one separate candidate from source alpha hints, then generate shared fallback only if needed. Source results suggest membership but never authorize final opacity. Require all proposed opaque targets to pass final analysis across admitted states. Do not adopt extra splits discovered during candidate validation.

Reject a separate candidate that fails required proof or output limits. Generate and analyze the shared fallback independently. Preserve its pre-separation behavior wherever opacity remains unsupported. Never carry the rejected layout's verdict into the fallback. Do not revise layouts after their own final analysis.

This changes the earlier single-layout proposal. It adds one bounded fallback, not a general optimization loop. Sequential generation is a proposed way to avoid retaining both candidate allocations. The spike did not measure that lifecycle or its peak memory. Shared image preparation must also fit the work limit.

Open on 2026-11-16: Define mixed-island membership, slot batches, and shared-region duplication for the separate candidate. Establish exact mip mapping when output maps have different densities. Fixed-layout success does not settle those questions.

## 7. Cleanup and remaining gates

Observation on 2026-11-16: A final editor check found neither the temporary asset folder nor its folder metadata. Each snippet released render targets and destroyed its transient meshes, textures, and materials in finally blocks. No production source file was edited. No source avatar asset was used.

The Console check before alpha readback contained no errors or warnings. The unsupported direct readback then produced two expected backend error entries. Those entries were not cleared. Later predicate readback and generated-pixel assertions completed successfully. No claim of an error-free Console follows from this spike.

Remaining on 2026-11-16: Separate alpha-mask conversion, normal reduction, useful strict-sampling cases, visibility grouping, material compatibility, and migration need further design work. The full UV matrix, supported output formats, numerical work limits, and growth thresholds remain release gates. This spike establishes a narrow feasible route, not implementation readiness.

The design is `docs/superpowers/specs/2026-11-16-merge-ready-atlasing-design.md`. Isolated review must examine the revised bounded sequence before user design approval and implementation planning.

## 8. Isolated reviews

Review outcome on 2026-11-16: An evidence reviewer read only this record. The review prompted clearer fixture content, source reimport, normal packing, UV1 sampling, assertion coverage, and mip accounting. It also prompted explicit limits on the synthetic fallback and unmeasured peak memory. The review's decompression premise was rejected because Unity reimported the original disk image. The record now states that distinction. The reviewer acknowledged the corrections without another file review.

Design review outcome on 2026-11-16: A separate reviewer read only the design. It accepted bounded termination but found missing rejection scope, terminal fallback, aggregate ordering, and retained-alpha output rules. The design now proposes selection per planned shared page and its variants. It also defines source preservation after failed fallback and stable aggregate accounting. The safe RGB-only conversion and mixed-island membership remain unresolved. The revised wording was not given another isolated review.

Status on 2026-11-16: The spike is complete, including cleanup and document checks. It supports focused implementation planning only after the remaining design contracts receive approval. It does not itself approve those contracts.
