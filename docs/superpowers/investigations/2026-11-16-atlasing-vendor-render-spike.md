# Disposable vendor normal and alpha render spike

Privacy note: This record contains public synthetic fixture results only. It contains no private asset observations or vendor source excerpts.

Date: 2026-11-16.
Status on 2026-11-16: The authorized vendor characterization completed. Product code did not change. No format, quality tolerance, or production implementation is approved by this record.

## 1. Scope and isolation

The reachable dev editor asset directory matched the repository asset directory exactly before execution. Each executable batch also checked that exact directory before creating objects. No operation ran in the private editor.

The spike used Unity 2022.3.22f1 in the dev project. It rendered the actual installed lilToon and bundled Poiyomi 9.3 shader surfaces. It used new transient materials, not source materials or locked material reconstruction. Textures came from public synthetic arrays. Compression used the editor encoder at quality 100. This is not a disk-source acquisition or importer-packing test.

Each batch created an isolated preview scene. It contained a camera, one directional light, and a quad with explicit normals, tangents, and UVs. The camera rendered to a linear floating-point target with 64 pixels on each axis. Readback used a floating-point texture. The shader instances and diagnostic shader were transient. No asset, scene file, project setting, or product source changed.

The camera used forward rendering, no multisample antialiasing, and no occlusion culling. The quad faced the camera. The light rotation was 25 degrees horizontally and minus 35 degrees vertically in the fixture's chosen axes. Normal comparisons used the same fixed setup within each family. They measure rendered lighting response, not angular error or equal lighting between shader families.

The controlling source review is `docs/superpowers/investigations/2026-11-16-atlasing-vendor-role-contracts.md`.

## 2. Normal fixtures

The normal image had horizontal component `0.65 * sin(0.11 * x)` and vertical component `0.45 * cos(0.13 * y)`. The positive third component followed the inspected packed-normal reconstruction. Eight-bit encoding stored the horizontal pair in either red and green, with alpha one, or alpha and green, with red one. The images had mip chains. These normal comparisons used the full-size quad without an intentional minification sweep.

The reference was uncompressed RG storage. Candidate outputs were uncompressed RG, uncompressed AG, compressed BC5 RG, and compressed BC3 AG. Strength values were one half, one, and two. All twelve uncompressed representation controls produced exactly equal rendered images across both shader families.

Observation on 2026-11-16: Both proposed packed representations reached compatible real vendor behavior in these fixtures. This does not prove all import routes, shader variants, or normal recipes.

| Shader family | Strength | Output | Maximum RGB difference | RGB root mean square difference |
| --- | --- | --- | --- | --- |
| lilToon | 0.5 | BC5 RG | 0.002372 | 0.000670 |
| lilToon | 0.5 | BC3 AG | 0.005491 | 0.001121 |
| lilToon | 1 | BC5 RG | 0.006094 | 0.001432 |
| lilToon | 1 | BC3 AG | 0.012558 | 0.002523 |
| lilToon | 2 | BC5 RG | 0.134329 | 0.006354 |
| lilToon | 2 | BC3 AG | 0.188501 | 0.009378 |
| Poiyomi | 0.5 | BC5 RG | 0.012876 | 0.003135 |
| Poiyomi | 0.5 | BC3 AG | 0.026590 | 0.004521 |
| Poiyomi | 1 | BC5 RG | 0.022832 | 0.005054 |
| Poiyomi | 1 | BC3 AG | 0.048483 | 0.008125 |
| Poiyomi | 2 | BC5 RG | 0.206927 | 0.011088 |
| Poiyomi | 2 | BC3 AG | 0.319339 | 0.015121 |

These values compare the final rendered channels against the uncompressed reference. They are not texture-channel error, angular error, or proposed acceptance thresholds. The different lighting configurations prevent a quality ranking between shader families.

### Controls and dependent consumers

An intentionally wrong image put the encoded horizontal component into both red and alpha. It changed the rendered result at every tested strength in both families. Changing strength from one to one half or two also changed each vendor response. The controls establish that the normal route was active.

A second normal consumer tested lilToon's second normal with its red-channel strength mask and Poiyomi's detail normal with its green-channel strength mask. The second strength was two. Switching the mask from zero to one changed the output in each family. Replacing that dependent normal with BC5 changed the final lighting too. A standalone decoder comparison cannot replace this consumer check.

A corrected raw-sampling probe observed alpha exactly one for the BC5 RG image and red exactly one for the BC3 AG image. The variable component ranged from approximately 0.176471 to 0.823529 in both. These are sampled runtime texture results for explicitly constructed images. They are not a guarantee about source importers.

## 3. Separate alpha extraction

The source image had constant RGB and a horizontal alpha ramp. The tint alpha stayed live at 0.6. The extraction candidate replaced main alpha with one and stored the source alpha in the mask's red channel. It selected mask multiplication, not replacement. The remaining tint multiplier stayed in the material.

The fixture tested transparent and cutout surfaces in each family. Cutout used a live cutoff of 0.3. The comparisons used tiling one and four. For lilToon, the mask inherited main-derived coordinates and kept its own transform at identity. For Poiyomi, the main and mask transforms both carried the selected tiling. This distinction is part of the recipe.

The source and candidate kept identical chosen render state. Destination blending was disabled to expose fragment output against a clear target. Alpha-to-coverage and dithering were disabled. This does not prove compositing over arbitrary backgrounds, multisample coverage, shadows, outlines, or alpha-only subpasses.

Observation on 2026-11-16: All eight uncompressed extraction comparisons matched exactly in RGB and alpha. The four replacement-mode negative controls changed alpha. Both cutout negative controls also changed which samples had nonzero alpha. Replacement loses the live tint factor in this setup. Multiplication preserved it.

### Compression remains a separate loss

One comparison compressed both the original main image and the extracted outputs to BC3. This compared two differently compressed representations. A separate comparison measured the final extracted output against the uncompressed prepared source. Only the latter is a prepared-to-final quality measurement.

| Prepared-to-final output | Tiling | Maximum alpha difference | Samples with changed nonzero-alpha membership |
| --- | --- | --- | --- |
| lilToon transparent | 1 | 0.009412 | 64 |
| lilToon transparent | 4 | 0.007059 | 0 |
| lilToon cutout | 1 | 0.125002 | 64 |
| lilToon cutout | 4 | 0.187500 | 0 |
| Poiyomi transparent | 1 | 0.009412 | 64 |
| Poiyomi transparent | 4 | 0.007059 | 0 |
| Poiyomi cutout | 1 | 0 | 0 |
| Poiyomi cutout | 4 | 0 | 0 |

Each image had 4096 samples. Nonzero-alpha membership means the returned alpha was greater than zero. It is not a general measurement of geometry, surface area, or discard coverage. A zero in this table does not prove unchanged continuous alpha before the shader's cutoff operation.

Observation on 2026-11-16: Exact uncompressed extraction did not imply exact compressed output. The alpha mask's red storage and the original main alpha storage have different compression behavior. lilToon's derivative-based cutout response exposed changes that the selected Poiyomi cutout fixture did not. The compression comparison does not approve the recipe for arbitrary thresholds or admitted animation states.

## 4. Mask order and product falsifiers

A constant red sample of 0.4 isolated affine preparation from filtering. The lilToon fixture used scale minus two and offset 1.25. The Poiyomi fixture used strength two, value 0.25, and inversion. Both produced alpha approximately 0.45, as the inspected equations predict. Perturbed offset controls produced zero instead. This verifies narrow parameter ordering in the real rendered paths.

A separate fixture used opposing alternating main-alpha and mask-red patterns. Their pointwise product was zero everywhere. At tiling one, the separately sampled equation and the baked-product image matched at the sampled centers. At tiling four, independent filtering produced alpha approximately 0.248043 in both families. The baked product stayed zero. All samples differed.

This is a real vendor render falsifier for product baking. A base-level match can hide a lower-resolution failure. The value differs slightly from the ideal arithmetic result of one quarter because the prepared mip chain uses eight-bit storage. No new tolerance follows from this observation.

## 5. Final compressed opacity control

A varied RGB fixture with alpha one tested BC3 and BC7 through the actual transparent vendor routes at both tilings. All eight outputs retained sampled alpha exactly one. Color still changed.

This does not contradict the earlier synthetic mask fixture where BC7 changed fully opaque alpha. The image content, compression blocks, and recipe differ. The result shows that some concrete outputs preserve opacity. It does not establish a format-wide guarantee. No AMUSE triangle classifier ran in this spike. Any product opacity claim still requires evidence from the selected final output and the configured alpha policy.

## 6. Harness corrections and verification

The first raw-channel probe did not declare the main texture as a material property. Its blit returned unrelated default sampling values. A constant-input diagnostic exposed the failure. Declaring the property made the same diagnostic return the expected quantized input. The corrected probe then measured the packed textures. The initial four raw-channel rows are discarded. They do not affect the vendor render comparisons, which bound textures on actual vendor material properties.

One diagnostic attempt failed C# compilation because local names conflicted with lambda parameters. That attempt did not execute. Shader diagnostics also reported generated line-directive warnings for the transient sampling shader. Its corrected constant-input and packed-channel controls passed. These warnings are not a clean-compilation claim.

Observation on 2026-11-16: The substantive batches produced 80 image comparisons, plus two absolute mask-order observations and four corrected raw-channel observations. Two earlier setup comparisons confirmed that both vendor surfaces responded to normal changes. Assertions checked exact uncompressed controls, active negative controls, expected mask values, sampled packing constants, final alpha observations, and cleanup. All checked assertions passed. These are characterization checks, not a product test suite.

All eight executed batches closed their preview scenes, destroyed every owned Unity object, and restored the previous active render target. Each returned zero surviving owned objects. No temporary files or asset folders were created.

The final Console query returned the same two compressed asynchronous-readback errors recorded before this spike. The spike used render-target readback. The Console was not cleared. There is no error-free Console claim.

## 7. Design disposition

Recommendation on 2026-11-16: Keep one narrow normal recipe per verified consumer path. Its contract includes packing, strength, sampling, and dependent masks. BC5 remains the better measured candidate in these fixtures. Do not replace an angular quality policy with this one lighting setup or assume strength-one bounds cover strength two.

Use multiplication as the first extraction candidate when it preserves the original tint order. Keep the mask-generation control opt-in. Accept the candidate only after its final compressed output meets the chosen role contract. Do not bake active sampled mask products. Do not introduce shader changes, a format search, or another user setting to rescue a failing case.

Remaining on 2026-11-16: Normal reduction and variance policy, numerical quality bounds, source acquisition combined with these vendor recipes, applicable non-forward passes, locked output routes, admitted animation states, anisotropy and mip protection, useful strict-mode packing, and measured memory or work limits. These results close narrow feasibility questions. They do not close those release gates or authorize production implementation.
