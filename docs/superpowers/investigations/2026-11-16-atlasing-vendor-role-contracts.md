# Vendor normal and mask contracts for atlasing

Privacy note: This record contains public source findings and synthetic arithmetic only. It contains no private asset observations or vendor source excerpts.

Date: 2026-11-16.
Status on 2026-11-16: Focused source investigation. No product implementation, Unity operation, build, or product test ran for this review. No format or quality tolerance is approved.

## 1. Scope and evidence

Two independent read-only investigations examined the installed lilToon and bundled Poiyomi shader surfaces. The parent checked the core decoder, sample, and mask equations directly. Source inspection establishes possible shader behavior. It does not establish the active compiled variant, imported channel values, compression quality, or rendered equivalence.

The dependency record pins lilToon 2.4.1 and Poiyomi package 10.0.29 in `Packages/vpm-manifest.json:15-17,30-32`. The Poiyomi target is the bundled 9.3 surface, not the native 10.0 shader. The installed bundled surface labels itself 9.3.67. That label is not a new AMUSE attestation or a promise that every historical shader version has the same code.

For the references below, the Poiyomi surface is `Packages/com.poiyomi.toon/_PoiyomiShaders/Shaders/9.3/Toon/Poiyomi Toon.shader`. The lilToon includes are under `Packages/jp.lilxyzw.liltoon/Shader/Includes/`.

## 2. Normals are a shader contract, not a format label

Source observation on 2026-11-16: Both inspected helpers have a packed-normal branch that combines red and alpha before extracting signed horizontal components. They apply normal strength before reconstructing the positive third component from the clamped squared horizontal length. Neither helper normalizes that result locally.

- lilToon: `lil_common_functions.hlsl:166-180`.
- Poiyomi: the surface at lines 6421-6451.

The branch can accept an RG representation if sampled alpha is one. It can accept an AG representation if sampled red is one. These are conditions on actual sampled channels. A format name or an authoring image does not establish them.

The shaders also have compile-time alternatives. lilToon has a direct RGB branch and an ASTC-specific channel path. Poiyomi has direct RGB and ASTC branches. Poiyomi's ASTC branch reconstructs the third component before scaling the horizontal pair. Do not merge those branches into one universal decoder. Selecting an uncompressed texture does not select the direct RGB branch. The shader compile definitions select it.

The preceding spike used explicit RG and AG decoders at strength one. Its angular results remain valid for those defined fixtures. They do not establish the complete vendor decoding path, non-unit strength behavior, or normal reduction policy. See `docs/superpowers/investigations/2026-11-16-atlasing-normal-mask-spike.md`.

### Strength and dependent masks

lilToon's first normal samples through the main sampler and uses main-derived coordinates. Its second normal can select a mesh UV set and uses a separate linear Repeat sampler. A red-channel scale mask multiplies the second normal strength before decoding and blending. Evidence: `lil_common_frag.hlsl:564-595`.

Poiyomi's main normal sample borrows the main sampler. It can use its own UV selector, transform, pan, and stochastic route. Its detail normal feeds a blend before the final world normal is normalized. Evidence: the surface at lines 29142-29191, 20129-20152, and 6453-6457.

Recommendation on 2026-11-16: Measure error at the actual consumer. A normal strength mask is not just an unrelated scalar image. Changes in that mask can change the decoded direction. Keep such dependencies within the supported recipe. Do not create a general shader graph to model them.

An interpreter falsifier used a unit normal with horizontal component 0.6 and strength two. Reconstructing the third component after scaling gives a different final direction from retaining the original third component. After normalization, the directions differ by 33.690068 degrees. This is arithmetic evidence for the importance of order. It is not a Unity render result or a proposed quality tolerance.

## 3. Alpha masks are not themselves threshold tests

Source observation on 2026-11-16: Both main alpha mask paths sample red and can replace, multiply, add, or subtract alpha. Their affine mask preparation differs.

| Consumer | Mask preparation | Sampling relationship |
| --- | --- | --- |
| lilToon | Multiply the sample by scale, add value, then clamp to zero through one | Main-derived coordinates plus the mask transform, through the main sampler |
| Poiyomi | Multiply by strength, add a signed offset, clamp, then invert if enabled | Selected coordinates plus the mask transform and pan, through a macro that requests the main sampler |

Evidence: `lil_common_frag.hlsl:458-475` and the Poiyomi surface at lines 29874-29886. Poiyomi's offset sign changes before clamping when inversion is enabled. Inversion then occurs after clamping. The sampler request is explicit at lines 4900-4903. The resolved engine macro and backend behavior still need runtime confirmation.

A threshold-sensitive contract concerns the later consumer, not every scalar image. Poiyomi's inspected forward fragment clips final alpha against the live cutoff at line 30406. Force-opaque behavior, alpha-to-coverage preparation, and dithering can run before that clip. Cutout can reset surviving alpha afterward. Evidence: lines 30376-30411. Do not generalize this one inspected pass to all passes or all vendor variants.

lilToon's inspected forward cutout path uses a derivative-based alpha ramp and discards zero coverage. Its alpha-only path instead clips against the cutoff directly. The transparent paths can use different cutoff properties. Evidence: `lil_pass_forward_normal.hlsl:394-412` and `lil_common_frag_alpha.hlsl:58-114`. A single synthetic comparison at one half cannot stand in for those consumers. Characterize forward coverage and the applicable alpha subpasses separately.

Recommendation on 2026-11-16: Use the actual admitted material state and animation states. Do not assume the inspector's mode defaults are the live cutoff, blend, depth, or alpha-to-coverage state. Preserve the captured state instead of resetting it to a preset. Threshold stability, continuous transparency quality, and exact opacity remain separate properties.

## 4. Separate alpha extraction stays narrow

The initial characterization candidate moves only the main image alpha into a separately sampled mask. Keep tint alpha and other remaining runtime multipliers in the shader. Do not bake material values into the image. Do not add a general equation baker.

Start with a static main-only configuration whose existing sampled mask is inactive. Disable unrelated effects in the public fixture so the candidate equation is isolated. Match the source mapping, sampler behavior, mip construction, and material state. Test the destination equation and tint order with the real shader before accepting the recipe. A source equation alone does not prove this conversion.

An existing replace mask can already separate color and alpha resolution. An existing multiply mask still needs its independently filtered factor. Do not bake two independently sampled images into a product merely to free a slot.

A two-sample arithmetic falsifier checked opposing patterns. Filtering each input at their midpoint and then multiplying gives one quarter. Multiplying corresponding samples first and filtering the result gives zero. The interpreter assertions passed. Matching image dimensions and filters does not remove this difference.

Existing AMUSE alpha semantics describe proof dependencies, not a complete image-conversion recipe. lilToon's mask interpretation explicitly separates replacement, multiplication, affine mapping, and constants in `Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonAlphaMaskSemantics.cs:52-95`. Poiyomi separately interprets replacement and multiplication in `Packages/com.alrauna.amuse/Editor/Semantics/Poiyomi/PoiyomiAlphaInterpreter.cs:868-1010,1090-1194`. Reuse their evidence discipline. Do not treat an alpha proof as permission to alter the full shader image pipeline.

## 5. Smallest next characterization

Recommendation on 2026-11-16: Use one disposable public-fixture harness. Keep its scope fixed before execution.

1. Render each actual vendor's simple normal route from the same prepared normal reference. Test the two proposed packed representations at strengths one half, one, and two. Verify actual sampled channels and the active desktop decoding behavior. Compare the final normal response with controlled tangent frames. Include a negative control with incorrect packing or strength order.
2. Test one active normal-strength mask or detail blend per family. This checks that the quality comparison includes a dependent scalar consumer rather than only a standalone decoder.
3. Render a static main-alpha extraction candidate before and after conversion. Keep tint alpha live and include a non-unit value. Test continuous transparency and cutoff behavior independently. Preserve the source render state.
4. Add one vendor-specific mask-order falsifier. Use values that distinguish affine preparation, clamping, and inversion. Include the opposing-pattern product control to reject accidental mask baking.
5. Check final compressed alpha separately. A correct cutoff image does not establish exact opacity. Any later AMUSE opacity claim must use the real final output and the chosen alpha policy.

Use candidate formats already under investigation. Do not expand into a format menu, extra UI, every shader effect, or a new sampling framework. Unsupported routes preserve their dependent source use. This matrix establishes narrow recipes only. It does not establish universal vendor support.

## 6. Disposition and limits

Accepted on 2026-11-16: Require the actual decoder branch, channel product, strength order, sampler dependency, and final consumer in role characterization. Preserve live material state. Keep tint values live. Separate threshold behavior from continuous masks and opacity.

Rejected on 2026-11-16: Treat an uncompressed upload as a test of a different compile branch. Reset material state from inspector mode defaults. Bake tint alpha into the extracted image. Infer BC7 normal packing from a format name. The Poiyomi investigator confirmed these corrections.

The lilToon investigation also proposed folding tint and affine mask values into image texels. That proposal is rejected. It does not establish filtering equivalence or preserve live multipliers. The accepted candidate extracts main image alpha into a previously inactive mask route. It does not bake an active mask into the main image. All applicable pass consumers still need proof.

Remaining on 2026-11-16: Actual vendor renders, compiled-variant confirmation, normal variance and reduction policy, numeric role tolerances, generated alpha-mask recipes, useful strict-mode cases, and measured memory and work limits. No new control follows from this investigation.

Verification on 2026-11-16: The parent directly inspected the cited core shader equations and current alpha interpretation. Three interpreter assertions passed for the strength-order and filter-product falsifiers. No Unity operation or product test ran. This record does not approve production implementation.

Runtime update on 2026-11-16: The authorized bounded vendor spike completed after this source review. It verified narrow packed-normal, live-tint extraction, mask-order, and minified product falsifiers. It also exposed compression loss in actual vendor output. Results and remaining limits are in `docs/superpowers/investigations/2026-11-16-atlasing-vendor-render-spike.md`. This update does not approve every source-reviewed route.
