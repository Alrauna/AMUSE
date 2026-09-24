# Poiyomi generated material lock state

Privacy note: This record uses a private avatar build as test input. It names no avatar, scene, material, renderer, texture, or asset path. It contains no per-renderer data or exact private counts.

Date: 2026-09-23.
Branch: `fix/poiyomi-generated-material-locking`.
Base: `main` at `b7d405c`.
Investigation status on 2026-09-23: No production code had changed at the end of the investigation. The local SDK preprocess chain locked the generated output. Play mode left it unlocked. No uploaded bundle was built.

## Question

Does an AMUSE-generated canonical Poiyomi material have the vendor lock at the end of a build? A canonical output here is the generated material with AMUSE's opaque settings. The lock needs both a shader name that starts with `Hidden/Locked/` and `_ShaderOptimizerEnabled` equal to `1`.

## Play mode observation

On 2026-09-23, a small number of Play mode runs on a private test avatar reached AMUSE separation, apply, and close. The source material stayed locked afterward.

The generated canonical output stayed assigned. Its shader name was not a locked form. Its lock flag was zero. It held the canonical opaque settings: `_Mode` was `0`, `_AlphaForceOpaque` was `1`, and the `RenderType` tag was `Opaque`.

These runs did not invoke upload or network.

## Code trace

`PoiyomiOpaqueConversion.PrepareCanonicalOpaqueClone` creates a new material from the unlocked source. It writes the opaque settings and keeps the source shader. It does not call Thry or set the lock flag. See `Packages/com.alrauna.amuse/Editor/Semantics/Poiyomi/PoiyomiOpaqueConversion.cs:583-613`.

`AlphaSeparationApply` assigns the canonical clone to final material slots. It records those writes for the close pass. See `Packages/com.alrauna.amuse/Editor/Build/AlphaSeparationApply.cs:323-355,507-545`.

`TransientUnlockWindowClose` sends only each open pair's `UnlockedClone` to the relock delegate. It verifies those clones, then reasserts the recorded canonical writes. It does not verify the canonical clone's lock state. See `Packages/com.alrauna.amuse/Editor/Build/TransientUnlockWindowClose.cs:91-132`.

The plugin runs this close pass last in AMUSE's PlatformFinish sequence. See `Packages/com.alrauna.amuse/Editor/Build/AmusePlatformFinishPlugin.cs:217-229`.

The 2026-09-04 pinned-source record reports that Thry's upload callback scans final renderer material slots and returns early in Play mode. That record treats the callback order relative to NDMF as an inference. It does not report a live upload-path check of this canonical output.

## Finding and limit

The 2026-09-23 Play mode runs confirm the cause. AMUSE creates the canonical output from an unlocked clone. AMUSE relocks only the source clone. The close pass then keeps the canonical output assigned. Thry skips its upload lock callback in Play mode. The generated output therefore stays unlocked in the Play mode result.

On 2026-09-23, the local SDK preprocess chain completed on an in-memory clone. It did not upload or use the network. The first Unity MCP request timed out after 30 seconds. A later execution-history read returned the result.

The result reported `sdk_preprocess=True` and `canonical_output=locked`. It reported unchanged input material shader and lock states. The execution history held repeated successful results for the same request. A scene search found no temporary clone.

On 2026-09-23, the request did not edit production code. No product tests ran. No uploaded bundle was built or checked.

## Regression seam and decision

`UnlockedSlotRunsTheWholePipelineInsideTheWindow` already proves that the canonical clone becomes the final assigned material. The test does not check its lock state. Its `RelockOverride` seam can capture whether the close pass sends the canonical clone to Thry. See `Packages/com.alrauna.amuse/Tests/Editor/Build/TransientUnlockTransformationTests.cs:175-301,736-748`.

The vendor stand-in restores a remembered shader only for materials it unlocked. For a new canonical clone, it only sets the lock flag. A regression that checks both locked shader identity and flag needs an explicit locked-shader result for generated clones. See `Packages/com.alrauna.amuse/Tests/Editor/Build/TransientUnlockVendorStandIn.cs:80-112`.

On 2026-09-23, the local SDK preprocess chain ended with a locked canonical output. It did not inspect an uploaded bundle.
Confirmed on 2026-09-23: The behavior gap is in Play mode. This path leaves the generated output unlocked.

Decision on 2026-09-23: AMUSE must lock generated Poiyomi outputs in Play mode.
Scope: only generated canonical outputs derived from the Poiyomi family. The normal SDK preprocess path already locks the output. This plan leaves that path unchanged.
The design and implementation plan are `docs/superpowers/specs/2026-09-23-poiyomi-generated-material-locking-design.md` and `docs/superpowers/plans/2026-09-23-poiyomi-generated-material-locking-plan.md`.
At this decision point on 2026-09-23, production code had not changed. The implementation plan then awaited review.
Do not run product tests in the Census Lab. Run any regression in the dev editor instance.

## Implementation result

Date: 2026-09-23.

Status on 2026-09-23: The approved change is implemented. During `ApplyOnPlay`, close submits U and each live Poiyomi C in one batch. It checks the locked shader name and lock flag after the batch. A failed lock restores renderer references to L. If graph enumeration succeeds, it also restores committed curves. If enumeration fails, it keeps the remaining AMUSE-owned copies alive. It records the retention refusal. It skips a C that apply already destroyed.

The appended split-curve test failed before its fix. Fallback destroyed C while the committed curve still named it. The swept-output test also failed before its fix. Close submitted destroyed C. Both tests passed after their fixes. The focused run executed 19 tests. All passed. None were skipped.

The final product run followed the last close-pass optimization. It completed 2,230 cases and listed five failures. The tool did not report pass or skip counts. The research assembly run passed 138 tests. None failed or were skipped. The five product failures matched the earlier full run. Each had reproduced in a narrow run.

The console returned zero C# compiler errors. Every Unity test run used the dev editor instance. No upload ran.
