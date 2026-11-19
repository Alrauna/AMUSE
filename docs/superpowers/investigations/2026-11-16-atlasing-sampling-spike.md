# Disposable atlas sampling spike

Privacy note: This record contains public synthetic fixture results only. It contains no private asset observations or vendor source excerpts.

Date: 2026-11-16.
Status on 2026-11-16: The authorized sampling spike completed. Product implementation and the full design remain unapproved.

## 1. Question and scope

Can an aligned crop preserve prepared-source samples through a finite protected mip range, including compression blocks and source Repeat boundaries?

The spike ran in the dev editor after its asset directory matched the repository asset directory exactly. The graphics backend was Direct3D11. The project used linear color space and forced anisotropic filtering. No operation ran in the private editor.

Code and the sampling shader compiled in memory. The spike created no script files, imported assets, scene objects, or persistent outputs. It used transient textures, a material, a shader, and a floating-point render target. It did not change product code or project settings.

This is sampling characterization, not a Unity Test Runner run. It does not establish a complete frontend, packer, avatar conversion, quality policy, or memory limit.

## 2. Fixture and comparison

The source was a linear RGBA32 image, 128 pixels square. Red repeated a 13-pixel ramp. Green increased vertically. Blue alternated by three-pixel horizontal and five-pixel vertical bands. Alpha changed from approximately one quarter to one at the horizontal midpoint. Unity generated the source mip chain.

The first sample region covered source coordinates from 32 to 96 on both axes. A crop from 16 to 112 supplied a 16-pixel neighborhood around that region. It occupied a 96-pixel square at destination coordinate 64 on a page 256 pixels square. The resulting translation was 48 pixels on both axes. That translation aligns to the four-pixel compression grid through protected mip two.

At protected levels zero, one, and two, the harness copied the corresponding source mip neighborhood into that page level. It did not regenerate these levels from the assembled base page. Outside the copied crop, the page contained contrasting magenta with zero alpha. Later levels used a deterministic four-sample box reduction of the preceding page level. The copied source levels were Unity-generated. Only the unprotected page tail used the harness reduction.

A transient GPU shader compared source and page sampling. It returned the absolute difference in each RGBA channel. Readback used a linear floating-point target and a floating-point CPU texture. Every render sampled a grid of 129 by 129 positions inside the region. The source and page coordinates and derivatives represented the same source-pixel footprint.

The explicit-level comparisons used levels zero, one half, one, one and one half, and two. Additional diagnostics used levels seven and eight. Three explicit derivative cases used source-pixel vectors:

- Horizontal one, vertical one.
- Horizontal four, vertical one.
- A diagonal vector of two by two, with a perpendicular vector of negative one half by one half.

These are controlled derivative cases. They do not prove the backend's exact anisotropic tap count, a general footprint bound, or actual vendor shader sampling behavior.

## 3. Packing and negative controls

Observation on 2026-11-16: The aligned crop had zero measured RGBA difference at every protected explicit level and every derivative case. This held before compression and after independently compressing both the source and page to DXT5, also called BC3.

A second fixture sampled across the source Repeat boundary. Its horizontal source interval ran from 112 to 144. The copied neighborhood ran from 96 to 160 and wrapped through the source period at each protected mip. The vertical region and neighborhood stayed unchanged. Its page translation was negative 32 pixels horizontally and positive 48 vertically. It also had zero measured difference in all protected comparisons, before and after BC3 compression.

The negative controls changed only the construction under test. Their differences confirm that the comparison was not an always-zero result.

| Construction | Reference and output | Sample level | Maximum channel error |
| --- | --- | --- | --- |
| Aligned crop with neighborhood | RGBA32 | 2 | 0 |
| Two-pixel misalignment with page-generated mips | RGBA32 | 2 | 0.280385 |
| Two-pixel misalignment | BC3 on both sides | 0 | 0.629252 |
| No neighborhood | RGBA32 | 0 | 0.437507 |
| Aligned crop with shared tail | RGBA32 | 7 | 0.546685 |

Errors use the normalized zero-to-one channel range. The misaligned crop retained the same source neighborhood but moved its destination origin from 64 to 66. Its base pixels still matched before compression. Its uncompressed mip one also matched. Differences began at the fractional level between one and two. Compression exposed the changed block phase even at level zero.

The no-neighborhood control copied only the sample region. Bilinear filtering reached the contrasting page background near its edges. At level zero, 512 sample positions differed. At level two, two thousand positions differed.

The shared tail changed all sampled positions at levels seven and eight. This layout cannot satisfy strict sampling. The spike did not find or test a useful strict-mode packing case. A 16-pixel neighborhood is sufficient for these measured cases only. It is not a general border formula.

## 4. Compression is a separate quality gate

Zero difference between two independently compressed aligned images does not establish fidelity to their uncompressed prepared image. A second comparison kept the source uncompressed and compressed only the aligned page.

Observation on 2026-11-16: BC3 produced large local color errors on this synthetic high-frequency pattern. A bounded BC7 comparison used the editor encoder at quality 100. It reduced these measured color errors but introduced alpha errors where BC3 preserved this fixture's alpha. Neither format is approved by these observations.

| Output format | Sample level | Maximum channel error | Maximum alpha error | RGBA root mean square error |
| --- | --- | --- | --- | --- |
| BC3 | 0 | 0.719173 | 0 | 0.070344 |
| BC3 | 1 | 0.589426 | 0 | 0.107190 |
| BC3 | 2 | 0.321965 | 0 | 0.045135 |
| BC7 | 0 | 0.061769 | 0.011780 | 0.005511 |
| BC7 | 1 | 0.135836 | 0.036316 | 0.022667 |
| BC7 | 2 | 0.093019 | 0.007843 | 0.019037 |

The BC3 maximum error at level zero came from color, not alpha. This fixture combines independent color variation inside compression blocks. The result is a falsifier for unconditional BC3 acceptance, not a claim that all ordinary images have this error.

The BC7 result is evidence for considering one bounded replacement recipe. It does not establish a supported format menu, threshold-safe alpha, an opacity verdict, or a quality tolerance. The spike did not run AMUSE's alpha classifier. Final alpha proof must still consume the actual final encoded output.

## 5. Engineering consequences

Recommendation on 2026-11-16: Keep the per-level source-neighborhood construction, sampling-grid phase rule, and block alignment admission. The measured positive cases support that narrow route. Keep independent tests for geometric placement and compression quality. Do not treat compressed-to-compressed equality as a quality check against prepared images.

Do not promise that strict mode can pack arbitrary independent content. Preserve a source use when its full accessible chain cannot satisfy the contract. This spike does not settle how often strict mode is useful.

Retain the bounded Automatic replacement policy. Do not turn a quality failure into an open-ended format search. A candidate that fails both permitted recipes must keep its source use. Numeric quality tolerances still need role-specific fixtures and an explicit acceptance decision.

## 6. Cleanup, checks, and remaining work

Observation on 2026-11-16: All four execution batches reported zero surviving owned Unity objects after their finally blocks. Each restored the prior active render target. No temporary asset folder was needed.

Interpreter assertions checked every aligned protected result and derivative result for zero difference. They also checked the misalignment and missing-neighborhood falsifiers and cleanup results. All assertions passed. These assertions validate the reported synthetic measurements, not product behavior.

The final Console read returned the two compressed asynchronous-readback errors already recorded by the earlier spike. This spike used render-target readback instead. The Console was not cleared. No error-free Console claim follows.

Remaining on 2026-11-16: Test diagonal triangle boundaries, all supported wrap modes, unequal map resolutions, sRGB recipes, normal decoding, threshold-sensitive masks, final alpha classification, and actual vendor sampling routes. The derivative comparisons do not replace a verified anisotropic support bound. Useful strict-mode cases, quality tolerances, and memory or work limits remain open. No timing or peak-allocation benchmark ran.

The controlling design is `docs/superpowers/specs/2026-11-16-merge-ready-atlasing-design.md`. The earlier acquisition and vendor-route evidence is `docs/superpowers/investigations/2026-11-16-atlasing-throwaway-spike.md`.
