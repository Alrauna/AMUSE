# Ponytail maintainability design

Privacy note: This record uses public repository source and synthetic verification cases. It contains no private observations or machine identifiers.

Status: 2026-10-21, finalized after six parallel adversarial reviews and scoped safety and latent-bug confirmation. The owner decisions and preflight observations are recorded below. Source baseline: `5eeea83`. Investigation: `docs/superpowers/investigations/2026-10-21-ponytail-maintainability-audit-investigation.md`.

## Intent

Fix the three reporting defects in the investigation. Remove hidden ordering rules and repeated rule ownership that make the next change unsafe or expensive. Prefer one existing owner over a new abstraction. Source size is not an acceptance target.

The owner requested a reviewed spec, a reviewed plan, subagent-driven execution, and a separate commit for each finalized change. This request authorizes these changes. Stop for an unresolved owner choice. A review finding is not permission to change product policy.

## Goals and non-goals

All 13 numbered investigation findings are in scope. Sections PTM1 through PTM13 map to the same numbers. The owner also approved the diagnosed test-only baseline corrections on 2026-10-21. Each change must stand alone and leave working behavior behind. The PTM decision prefix avoids identifiers owned by earlier designs.

Do not change alpha mathematics, shader support ranges, attestation pins, consent policy, source assets, the five NDMF passes, live apply revalidation, or transient unlock-close ordering. Do not add packages, a settings framework, a cache registry, a fixture framework, serialized avatar markers, or a new research export path. Do not fold unrelated website issues into the download fix.

The five pre-existing modified configuration and project-setting files remain user-owned. Do not stage them or alter their bytes. Do not access private projects for tests. The research package must not enter the product archive or listing.

## Constraints

- Unity 2022.3.22f1 and the existing C# language and assembly configuration stay unchanged.
- Use existing libraries only. No new dependency is required.
- Analysis remains pure. Host owns live Unity evidence. Build owns lifecycle and mutation. Preview remains read-only.
- Unproven triangles stay unproven. Unknown facts invalidate only dependent conclusions.
- Keep each named refusal, its scope, and its precedence unless PTM3 corrects the display of that refusal.
- Programming and invariant failures throw. Never turn them into unsupported results.
- Keep serialized row-kind numeric values 0 through 4 unchanged.
- New Unity files and their `.meta` files form one change. Existing metadata identifiers stay unchanged.
- Before any Unity write, broad operation, or reported validation, verify the exact intended `Application.dataPath` and pin the dev editor instance.
- Use public synthetic fixtures. Never record machine paths, private identifiers, endpoint identifiers, or raw private observations.
- Human-facing English uses short active sentences. No contractions or semicolons in prose.
- Commits include explicit task paths only. Never push, publish, merge, or change repository settings.

## Design decisions

### Validation prerequisite: portable texture-capture tests

Correct the two diagnosed host-test assumptions before the product changes. Delete the RGB24 assertion that exact Sample support must be false. Keep its full-chain exact-alpha checks.

Make the ARGB32 fixture enforce the existing production contract. If its exact graphics format supports Sample, retain its maximum-versus-submaximum capture checks. Otherwise, invoke the real capture entry and assert `UnavailableCapture` with no returned evidence. Do not skip the test, bypass a capability gate, add a format exemption, or change production behavior. Update both test comments so they claim only the checks that run on that host.

The baseline failures are observed evidence. A corrected test that passes on its first run is characterization of the existing contract, not a claimed RED/GREEN production fix. Review and commit these test corrections separately.

### PTM1. Bind Play results to one authored root

`PlayModeBuildStore.ComputeAvatarKey(Transform)` must distinguish roots, not names. Clone-name normalization is not identity. A sibling-index chain alone is also not identity because a reorder or replacement can assign the old position to another same-named root.

Use a Unity-owned stable scene-object identity where it is available. A saved scene root must retain its identity through the supported NDMF Play processing path. An unsaved or transient root must never fall back to scene name plus object name. A local session identity may serve it only while ownership can be proved. A root without proved ownership neither saves nor loads a Play record. Refresh must not clear another root's record. Fall back to the existing preview rather than borrow another root's record.

Keep ownership logic inside the existing store. Do not add a persistent avatar marker, a parallel root registry, or a migration shim for the old name key. Change the session-key prefix for the new key format so old name-based records become misses. `SaveOutcome`, lookup, and Refresh must use the same root identity rule.

Before finalizing the implementation interface, verify identity behavior for saved and unsaved synthetic roots through the installed host path. The saved-root check must span Play mode entry and exit with scene reload enabled and disabled. A raw instance ID alone cannot establish the post-Play match. Stop if maintaining the existing Play-result experience requires an owner choice. Do not claim that one installed NDMF source proves all supported hosts.

Acceptance:

- Two same-named roots with equal settings never share rows or clear each other's records. Run this case for saved roots and for unsaved or identity-less roots.
- Reordering, renaming, destroying, or replacing a root never transfers ownership to another root.
- Duplicate scene names and additive scenes do not collide.
- A record saved inside the host's temporary rename window loads under the restored saved root after leaving Play mode. The host's fake sibling never matches or clears that record.
- A root without a proved correlation produces a cache miss, not an accurate label.

### PTM2. Reject a preview after its policy or target changes

The existing dry-run object owns the policy key captured when it begins. Capture that key and the active target before all early banner returns. Reuse the shared formatter from PTM6. Expose one validity check for the inspector and the stepping driver. One private invalidated flag records the first observed mismatch and never clears inside that run. Do not expose mutable policy fields or add a policy object framework.

Compare live policy and target with the captured key before a step publishes rows or writes a cache entry. Also compare before the inspector draws any retained result. A mismatch invalidates the run. Cancel pending work through the existing cancellation path. The sticky policy-invalidated flag takes display precedence over the ordinary cancelled state and its Compute button. Show: "Settings changed. Select Refresh to compute new results."

Do not automatically restart GPU capture on repaint. Refresh clears the old run and follows the existing explicit compute flow. Foldout close and reopen must not make stale rows valid. Returning settings to their old values must not resurrect a run invalidated during computation.

Capture `IgnoreOutOfRangeMaterialSlots` with the other policy values. Stop reading that setting live inside a run. This prevents one result from mixing policy snapshots. Do not expand this change into live material, graph, or texture watching.

Acceptance covers completed results, in-flight work, every early banner and structural-refusal return, an unsupported target, a target switch, foldout reopen, and a policy change between renderer steps. Include AMUSE-off, separator-off, RootRequired, and structural refusal. Also observe a mismatch, restore the original settings, and verify that the old run remains invalid. Unchanged policy must keep the existing cache-hit behavior.

### PTM3. Let refusal cause control build-time deferral

`IsSlotDeferred` must use the unresolved result and the captured slot evidence. A locked current material or an unreadable streaming texture is not sufficient by itself. A recorded permanent closure failure takes precedence over either capability. A recorded permanent failure on an admitted swap material must not be replaced by a promise about the current material.

Keep deferral local to the slot. Use existing refusal, offender, unknown-reason, and capture facts where they identify the cause. A deferral is allowed only when the recorded blocker is the capability that build time can supply. Do not infer that all failures on a recognized material are temporary.

Preserve actual locked-source deferrals when their recorded causes support them. Preserve renderer-wide refusal rows when any unresolved slot has a recorded permanent failure. An all-deferred renderer may retain its existing omission of the renderer refusal row. Do not change classification or permit mutation because a display row is deferred.

Acceptance combines an invalid swap with a valid locked current material, an invalid swap with streaming, a recorded permanent admitted-swap refusal with a deferred-capable current material, a valid locked-only case, mixed sibling slots, and an early renderer refusal with no slot results.

Owner decision on 2026-10-21: fix recorded causes only. Keep the first-blocker resolver. Do not resolve further admitted states or add evidence to discover a later hidden blocker. A later permanent material failure can remain undiscovered until the build. Locked deferral is confirmed from the current material only. A locked swap value keeps the refused display when the recorded evidence cannot prove its recovery. Do not describe a deferred row as proof that the whole slot will succeed at build time.

The traced streaming-only missing-field path keeps the slot resolved with an unproven `MissingTextureEvidence` resolution. It does not reach `IsSlotDeferred`. Keep the avatar streaming note and that resolved-row behavior. The existing streaming arm may remain only for `AdmittedMaterialSemanticsUnknown` with a null `UnknownReason`, non-empty slot `CaptureRefusals`, and an unreadable streaming current material. A seam-injected example is characterization, not proof of a production path. Do not add a streaming deferral producer or a new evidence carrier.

### PTM4. Remove Poiyomi's static schema cycle

Move `AlphaRequiredSchemaProperties` from `PoiyomiMaterialSemantics` to `PoiyomiAlphaInterpreter`, before its request initializers. The interpreter builds its requests from its own schema. Retarget every schema reader, including `PoiyomiSourceAttestation`. Delete the old field. Keep the used facade and its full requests.

This removes an initialization dependency. Use Unity's bundled Mono in two fresh standalone processes to load the compiled product assembly and local managed dependencies. Enter the interpreter request first in one process and the facade request first in the other. Check the required schema on both requests. This pure managed probe calls no live Unity interface. Do not substitute warmed editor types for fresh-process proof. The preflight observations below record the before-fix result. They do not establish an Editor startup or build crash.

### PTM5. Return slot evidence with resolution results

Add the constructed `IReadOnlyList<CapturedMaterialSlotEvidence>` to `RuntimeStateResolution.ResolvedRuntimeStates` as `Slots`. Construct it once at the existing point after renderer-scoped refusal checks. Return that exact list with its parallel per-slot results. Build and preview read `resolved.Slots` instead of calling `MaterialSlotsFor` again.

Renderer-scoped early refusals keep empty `Slots`, empty slot results, and their existing refusal. An all-slots-refused outcome keeps both slot evidence and slot results. Successful and mixed outcomes keep their existing material and result order. Preparation keeps its separate valid `MaterialSlotsFor` use.

Use an empty shared array for early results. Do not construct a slot list before an early refusal just to fill the new property. Do not copy the list or add a generic result container. Update obsolete comments about slot attribution.

Delete the preview's `_inFlightSlots` field, assignment, and reset. The retained resolved result owns those slots. Drop the redundant `slots` parameter from `DryRunRowBuilder.BuildResolvedRefusalRows`, which already receives the result. `BuildRows` keeps its slot parameter and receives `resolved.Slots`.

Acceptance protects typed renderer bindings, material-swap alternatives, closure-failed slot position, sibling-slot independence, and refusal order. Tests assert consumer-visible attribution and outcomes, not reference equality or helper call counts.

### PTM6. Give policy-key fields one owner

Preview calls `PlayModeBuildStore.ComputePolicyFingerprint(component, activeTarget)` and deletes its private formatter. The existing store formatter owns target and setting values. Keep caller-specific cache keys and the preview's controller and graph fingerprints.

Build already references `DryRun.RowText` by qualified name and preview already imports Build. This change adds no new dependency direction, lifecycle call, or live capability. Do not move the shared sentence text or add a settings module for directional tidiness. Keep the store's existing policy-key formatting unchanged. The preview key may change because its cache is session-local.

Use real stale-record and target-change tests. Do not add a forwarding-equality test or assert the key's field count.

### PTM7. Use one typed row-kind contract

Move `MaterialTransformationRowKind` beside `SerializedTransformationRow` in `PlayModeBuildStore.cs`. Set explicit values: `Banner = 0`, `Note = 1`, `Renderer = 2`, `Slot = 3`, `Errored = 4`. Change the DTO field to that enum. Migrate all writers, readers, numeric comparisons, and test fixtures in the same change. Include the apply, plugin, build-store, inspector, and preview tests. Remove the old enum and cast.

`JsonUtility` must retain the old integer payload format. Verify deserialization of numeric payloads and the resulting row shapes. Do not add a second wire enum, conversion layer, or legacy alias. Do not broaden corrupt-payload behavior beyond what this typed cutover requires.

### PTM8. Reuse the approved-identity predicate

Call `UnityMaterialSemantics.IsApprovedIdentityFailure` from the consent scan instead of spelling its rule again. Keep the earlier attestation filter. Preserve approval ranges, identity-versus-state refusal, consent subjects, and granted-name behavior.

Use the existing regular, Multi identity-refused, Multi state-refused, out-of-range, and unsupported fixtures as behavior protection. The investigation found no mismatch for constructible records. This change must not create a new approval rule.

Preserve the captured-record construction invariant. Regular families carry no Multi resolution. Multi records carry their Multi resolution. The existing predicate replacement is equivalent under that invariant. Do not broaden consent to malformed combinations or add a new admission gate.

### PTM9. Correct measurement-status ownership

Correct the blanket two-settings-shape claim in `LilToonSourceAttestation`. Record status at the owning measured rows. State that the 2.4.1 Multi row has the recorded single-shape evidence. Keep regular-family, Multi, and opaque-tuple status separate. Remove repeated admitted-version enumerations in `LilToonMultiResolution` and the verified test seam. Refer to the owning row instead.

Do not change pins, remeasure vendor assets, revoke the owner-approved admission exception, or imply outstanding measurements ran. The 2026-10-20 admission design remains the authority for that exception. This is a documentation correction in source, not a new admission gate.

### PTM10. Share the fixed session-cache reset list

Introduce one internal Build helper with one `Clear` method for the existing seven cache resets in their existing order. Keep its build-entry call before the lifecycle-refusal return. Keep its clone-sweep call at the existing teardown point. Keep both moments and all other teardown behavior. Do not create registration, discovery, lifecycle ownership, or a cache-manager abstraction.

Existing two-build and apply cleanup behavior must remain intact. A helper-call or reset-member-count test is not useful proof. A missed sweep reset alone does not establish next-build contamination.

### PTM11. Extend the research no-write tripwire

Extend `ResearchSourceApiBanTests` only. Keep production-source scope and test-source exclusion. Keep read-only asset lookups and existing Unity mutation checks.

Add direct persistence and transmission sinks that violate the existing promise. Cover file writes and appends, writable streams, preference/session writes, HTTP clients, Unity web requests, and socket send paths. Use a small fixed set of literal or regular-expression checks in the existing test. Extract a test-local source-check function only to exercise synthetic forbidden and allowed source snippets through the same checks.

Do not execute the forbidden snippets. Assert that each representative forbidden form is reported and read-only forms remain allowed. This is a tripwire for accidental changes, not a sandbox or proof against hostile code. Do not add a parser dependency, an export path, or production guard code.

### PTM12. Remove the incidental property count

In `LilToonPropertiesMatchShaderConventions`, delete the exact-48 assertion. Keep the existing naming and uniqueness checks and a non-empty discovered set, matching the sibling Poiyomi convention. Do not replace 48 with another count. Keep behavior tests for the properties and shader requests unchanged.

This explicitly supersedes the fixed-count test instruction in the 2026-10-19 decoupling plan. The count was deliberate, not an author mistake. It does not define a shader safety limit. Do not add a source-text or field-count regression test for the deletion.

### PTM13. Register one website download handler

Keep the existing menu and row buttons. Store the selected package URL synchronously from the row-button click's `event.currentTarget` on every activation, even when the menu is already open. Register one stable download action outside the row-open handler. Read the stored URL when the action runs. Keep the selected menu available after switching rows. A row-button click must not also trigger the document's outside-menu close handler. Do not retain the opener event, read a nested SVG target's dataset, or read package data from the menu item.

Use the Fluent menu item's `change` event. The pinned 2.6.1 runtime emitted it for both real mouse and Enter activation in the preflight probe. The runtime exists only at the CDN URL in `Website/index.html`, not as a vendored file. The owner authorized this read-only fetch on 2026-10-21. Do not also bind click. Open at most one download per activation. Open one row's menu, activate a second row's button while it remains open, then activate Download once. The one window must use the second row's URL.

Use the existing URL data and destination behavior. Open the new tab with `noopener` because the action does not need an opener relationship. Do not add a download framework, dependency, URL policy, template engine, or unrelated modal fixes. A local handler-only smoke is not proof of Fluent integration. Do not claim the hosted listing ran without observing it.

## Architecture and change order

The plan uses independently reviewable change groups rather than one large rewrite:

The approved test-only baseline correction is the first prerequisite commit.

1. Play record ownership, with host-correlation proof before adopting the identity.
2. Shared policy key and preview validity, in one change because they share the snapshot contract.
3. Cause-aware preview deferral.
4. Poiyomi initialization ownership.
5. Slot evidence and result ownership.
6. Typed row kinds.
7. Shared consent predicate.
8. Measurement-status comments.
9. Fixed cache-reset membership.
10. Research source tripwire.
11. Property-count test policy.
12. Website listener lifetime.

The parent owns integration, test execution, reviews, and commits. Workers do not run builds, tests, lint, or formatters while another task is in flight. Do not dispatch overlapping implementations. Independent review lenses run in parallel.

## Verification and acceptance gate

For each behavior bug, reproduce the named wrong behavior before the production fix. Keep a small deterministic regression test for the plausible consumer-visible failure. Pure moves use existing behavior tests and a runtime smoke, not wiring assertions.

Run focused EditMode filters and observe non-zero counts. Run the full `Alrauna.Amuse.Tests.Editor` and `Alrauna.Amuse.Research.Tests.Editor` assemblies after the changes. Existing test failures must be reported and diagnosed, not weakened. Use the dev editor instance only. No test may run in the private Lab project.

Smoke the actual inspector for stale-policy rejection and same-root Play-result ownership. Exercise a synthetic build and observe source preservation, refusal attribution, and transient cleanup. Run the fresh-process Poiyomi checks in both orders. Exercise the download menu in a real browser with the shipped Fluent runtime. Include repeated opens, a second row while the menu is open, nested button content, and keyboard activation.

At each finalized change, update the relevant user documentation or changelog after runtime proof, run whitespace checks, inspect the task diff, and commit only its explicit files. Do not bump a package version. Record observed counts and limits. Sweep changed documents and comments for forbidden identifiers before commit.

A missing runner, unproved root correlation, external-access permission, or a new product-policy choice is a stop gate. Finish reachable document work, then ask for the exact missing prerequisite. Do not ship an unverified fallback or silently drop a finding.

## Review record

Six recovered parallel reviewers completed their first pass on 2026-10-21. They covered investigation accuracy, safety and security, architecture, ponytail, latent bugs, and builder readiness. The revisions add sticky invalidation precedence, an explicit policy-revert case, saved and unsaved root ownership cases, the host rename-window case, slot-owner cleanup, typed test fixtures, truthful test comments, and the second-row menu case. They also correct the existing bidirectional Build and preview type references and separate seam-only streaming characterization from production behavior. Scoped safety and latent-bug checks confirmed that all raised findings were addressed. Builder readiness was confirmed. No blocking spec finding remained on 2026-10-21. The plan receives the same review plus explicit spec coverage review.

No remediation implementation is claimed by this record. The source reviewers ran no Unity tests or browser checks. The controller's targeted runtime observations appear below.

## Preflight observations and owner gate

On 2026-10-21, the controller verified the dev editor instance against this repository's exact `Application.dataPath`. An existing bridge was available without a package install.

The baseline EditMode run selected both AMUSE assemblies. Its job ended with failed status. Progress recorded 2972 completed tests out of 2979 discovered tests. It returned no aggregate result, so no pass count is claimed. The uncapped failure list contained:

- `Alrauna.Amuse.Tests.Editor.Host.UnityAlphaFieldEvidenceTests.AnRgbOnlyFormatSamplesAlphaExactlyOneAtEveryLevel`. The host reported exact Sample support where the test expected none.
- `Alrauna.Amuse.Tests.Editor.Host.UnityAlphaFieldEvidenceTests.ReadableArgb32_IsAdmittedAndSeparatesMaximumFromSubmaximum`. The observed result was false where the test expected true.

No product source had changed before this run. The run changed lockfile ordering and one project setting. The controller restored the captured pre-run state. All five user-owned files then matched their pre-run byte fingerprints. No test was rerun to confirm either failure.

Six parallel spec-review jobs failed at the configured model provider before they produced reviews. None is recorded as an approval. Two source-analysis workers returned useful identity and deferral traces. They did not run Unity.

The deferral trace exposed a scope choice in PTM3. Slot resolution retains only the first blocking material. A locked current material can therefore hide a later permanent swap-material failure from the captured result. The owner chose the narrow recorded-cause correction on 2026-10-21. Broader blocker discovery is out of scope.

On 2026-10-21, the owner requested diagnosis of both baseline failures before proceeding. The owner also authorized explicit per-job model overrides to recover the failed parallel reviews. Saved role settings remain unchanged. The plan and implementation have not started.

### Baseline diagnosis on 2026-10-21

The RGB24 failure is a host-specific test assertion, not an observed opacity error. `Tests/Editor/Host/UnityAlphaFieldEvidenceTests.cs:1914-1919` requires exact Sample support to be false. The production gate allows either value for RGB24 at `Editor/Host/UnityAlphaFieldEvidence.cs:1008-1017`. The behavioral checks after that assertion still require every captured alpha value to be exactly opaque. These paths are relative to the product package.

A diagnostic probe created two temporary in-memory textures and inspected capabilities. It did not invoke either failing test, import an asset, or run alpha capture. The dev editor used the OpenGLCore backend and the StandaloneWindows64 target. Async readback and the R8 render and read gates were true. Exact Sample support was true for RGB24 and false for ARGB32.

The ARGB32 fixture unconditionally expects capture at `Tests/Editor/Host/UnityAlphaFieldEvidenceTests.cs:596-605`. Production deliberately requires exact source-format Sample support for alpha-bearing textures at `Editor/Host/UnityAlphaFieldEvidence.cs:289-300,978-1017`. The observed unsupported ARGB32 sampling fact explains a required refusal on this backend. No evidence justifies a new ARGB32 exemption or a compatible-format substitution. The existing fixture's source-identity assertion passed before its reported failure.

The owner approved the test-only correction on 2026-10-21. Remove the incidental RGB24 capability assertion and keep all alpha-output assertions. Make the ARGB32 fixture enforce the capture contract for both capability states. When exact sampling is unavailable, assert the named refusal and absence of evidence. When it is available, retain maximum-versus-submaximum alpha checks. Do not skip the test or change production admission.

The model-override retry did not start workers. The task tool rejected `model` for review agents despite its published schema. The controller reported that tool mismatch. The owner then approved trying supported session-only routing. No saved role setting changed.

On 2026-10-21, temporary overrides routed the two review roles to the already assigned task model. All six replacement review jobs completed. No permanent model-role setting was changed or saved.

### Fresh-process initialization probe on 2026-10-21

A temporary reflection loader used the unchanged compiled product assembly and Unity's bundled Mono. Each order ran in a separate process. The loader explicitly entered the first type initializer and checked the required alpha schema on the interpreter and facade requests. It called no live Unity interface and installed no dependency.

| First entry | Exit code | Observed result |
| --- | ---: | --- |
| Interpreter | 1 | Two nested type-initialization failures ended in `ArgumentNullException`. |
| Facade | 0 | Both request schemas were available with the required properties. |

Reuse this isolated mechanism after PTM4. These results establish the pure managed initialization failure, not an observed Unity Editor startup or avatar-build failure.

### Fluent runtime probe on 2026-10-21

The owner authorized read-only CDN access. An isolated browser page loaded the existing pinned Fluent runtime with the source menu markup and unchanged handler block. Window actions were intercepted. No real package download, dependency install, vendoring, or hosted-site run occurred. [Pinned package metadata](https://unpkg.com/@fluentui/web-components@2.6.1/package.json) identifies the runtime version.

Both a real mouse click and Enter emitted `change`. The unchanged handler then showed this listener-lifetime behavior:

| Activation | Intercepted window actions | Selected result |
| --- | ---: | --- |
| Mouse after first menu open | 1 | First row |
| Enter in the same open menu | 1 | First row |
| Mouse after the menu closed and opened for the second row | 2 | Both rows |

The actual Fluent style makes the nested SVG ignore pointer events. A direct SVG-target click was therefore not a valid user action in this probe. Use the row button and its real rendered content in the final browser check. The URL still belongs to `event.currentTarget`, not to an incidental event target.

The controller observed a screenshot and closed the probe tab. This proves the original listener accumulation with the real pinned runtime. It does not prove the future fix or the hosted listing.
