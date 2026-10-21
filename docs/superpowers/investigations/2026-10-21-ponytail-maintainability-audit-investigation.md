# Ponytail maintainability audit investigation

Privacy note: This record uses public repository source only. It contains no private observations or machine identifiers.

Status date: 2026-10-21.

## What this repo does

AMUSE analyzes avatar materials during a Unity build. It moves proven opaque triangles to generated opaque materials and preserves source assets.

This audit assumes one desktop avatar build or manual preview at a time in one Unity Editor. Large textures, many material alternatives, and complex animation graphs are within scope.

## Scope and method

The source baseline is `5eeea83`. The investigation branch is `audit/ponytail-maintainability-2026-10-21`.

Six parallel read-only slices cover build lifecycle, proof and host evidence, lilToon, common semantics and Poiyomi, editor policy, and research and test support. The controller covers release packaging, setup, website handlers, and findings across modules. The first workers were cancelled after the owner changed model roles. The replacement workers use the updated roles.

The audit ranks correctness and safety first. It then ranks the cost of the next supported shader version, setting, or behavior change. File size alone is not a finding. Recommendations do not authorize implementation.

Source facts use repository-relative citations. `[INFERENCE]` marks expected future cost or benefit. A source trace is not a runtime result.

## Measured source footprint

These counts include all lines in C# source files, including comments and blank lines. They do not measure complexity or removable code.

Method: recursively enumerate `*.cs` under the two AMUSE package roots and count text lines. Product production includes `Editor/` and `Runtime/`. Research production includes `Editor/`. Each test total includes its package's `Tests/` tree. Dependency packages and generated project files are excluded.

| Source area | C# files | Lines |
| --- | ---: | ---: |
| Product production | 121 | 46674 |
| Product tests | 137 | 103704 |
| Research production | 13 | 1875 |
| Research tests | 24 | 4796 |

## Verification evidence

All observations in this section are dated 2026-10-21.

- An in-memory product archive check found 276 file entries. It found no test entries and no research entries. The production editor assembly definition was present. This checks package membership only. It does not run the release workflow.
- An isolated browser run used the unchanged website menu handlers and source markup. A normal click opened no download. One synthetic change event opened one window after the first menu open. After the second menu open, one synthetic change event opened two windows. This proves listener accumulation in the source handler. It does not establish how the external Fluent library dispatches events on the published site.
- A source counterexample found 48 lilToon property constants. Adding one valid constant would violate the exact-count assertion. This is a source-level counterexample, not a Unity test result.
- No language server is configured in this session. No Unity runner is exposed through the available session tools. No Unity test result is claimed.
- A source-derived model gave two distinct roots the same scene and clone-normalized name. Their keys matched and the second save replaced the first result. This was not Unity execution.
- The controller read the locally resolved NDMF 1.14.8 source. Its Play path temporarily renames the existing object and then restores its name at `Packages/nadena.dev.ndmf/Editor/ApplyOnPlay.cs:84-104`. `Packages/nadena.dev.ndmf/Editor/API/BuildContext.cs:151-152` retains the supplied root. This is host-source evidence for that installed version, not a claim about every supported host.
- A source comparison found that both policy-key builders use the same ordered 19 settings. The two seven-call reset lists also match. No present drift was found in either pair.

## Must fix

These are source-derived reporting defects. No Unity reproduction ran in this session. No unsafe mesh or material mutation was established.

### 1. Stop same-named roots from sharing Play records

- **What this is:** `Packages/com.alrauna.amuse/Editor/Build/PlayModeBuildStore.cs:36-46` identifies a root by its scene and clone-normalized name. The inspector uses that key at `Packages/com.alrauna.amuse/Editor/AmuseAvatarOptimizerEditor.cs:237-240,277-315`.
- **Problem:** Two roots in one scene can have the same name and settings. A saved record for one then passes the other root's key and policy checks. The panel labels the wrong root's result as accurate. Concurrent builds are not required.
- **Fix:** Use a distinct authored-root identity and preserve its match through Play processing. A scene plus sibling-index chain is a small candidate. Verify its host correlation before adopting it. Do not add a serialized marker or use an empty avatar-relative registry path as root identity.
- **If we skip it:** One root can display another root's results. Refresh on either also clears their shared record. Future row features inherit the same ownership error.

The overwrite and load behavior are at `Packages/com.alrauna.amuse/Editor/Build/PlayModeBuildStore.cs:78-102`. A key-model check exposed the collision. The original-to-Play identity remedy still needs Unity validation.

### 2. Reject completed preview rows after their policy changes

- **What this is:** `Packages/com.alrauna.amuse/Editor/AmuseAvatarOptimizerEditor.cs:315-365` creates a run only when none exists, then draws its completed rows. Closing and reopening the foldout does not discard that run.
- **Problem:** Editing a policy leaves those rows visible under the old policy. The approved design requires policy changes to invalidate stale results at `docs/superpowers/specs/2026-10-13-show-material-transformations-design.md:177`. README's Refresh instructions do not remove that requirement.
- **Fix:** Compare the completed run's policy key with the live key before drawing its rows. Reject a stale result and ask for Refresh. Do not start GPU capture on each slider repaint.
- **If we skip it:** Users can read old conversion verdicts beside new settings. Sharing the two key formatters alone does not fix this run-lifetime error.

Most proof settings are mapped once at `Packages/com.alrauna.amuse/Editor/DryRun/AlphaSeparationDryRun.cs:356-390`. Not every setting is frozen. Slot capture still reads `IgnoreOutOfRangeMaterialSlots` live at lines 872 and 891. This mixed lifetime needs a consistent result-validity rule, not another disclaimer.

### 3. Do not hide a closure refusal behind build-time deferral

- **What this is:** `Packages/com.alrauna.amuse/Editor/DryRun/AlphaSeparationDryRun.cs:1120-1154` decides whether an unresolved slot can be evaluated at build time. It receives the slot result and evidence but ignores both.
- **Problem:** A recognized locked current material makes that method return true even when an invalid material-swap value caused the refusal. `Packages/com.alrauna.amuse/Editor/DryRun/DryRunRowBuilder.cs:23-55,117-135` then draws a deferred sentence and can suppress the renderer refusal.
- **Fix:** Base deferral on the actual refusal cause. Preserve permanent closure failures when the current material separately has a build-time capability. Keep valid locked-material and streaming deferrals.
- **If we skip it:** The preview tells users to wait for a build when their invalid swap still prevents proof. Each new deferred capability can hide more causes if material shape continues to override recorded refusal evidence.

The capture records `InvalidSwapValue` at `Packages/com.alrauna.amuse/Editor/Host/UnityAnimationEvidenceCapture.cs:572-595`. Resolution gives closure failure precedence at `Packages/com.alrauna.amuse/Editor/Analysis/RuntimeStateResolution.cs:270-289`. This combined refusal-and-deferral path was traced from source. It was not run in Unity.

## Should fix

### 4. Remove the Poiyomi static initialization cycle

- **What this is:** The interpreter initializes its alpha request at `Packages/com.alrauna.amuse/Editor/Semantics/Poiyomi/PoiyomiAlphaInterpreter.cs:124-125`. That request reads the facade's static schema at line 204. The facade initializes its own requests by reading the interpreter request at `Packages/com.alrauna.amuse/Editor/Semantics/Poiyomi/PoiyomiMaterialSemantics.cs:92-118`.
- **Problem:** `[INFERENCE]` Interpreter-first initialization can enter the facade while the interpreter request is still null. `Packages/com.alrauna.amuse/Editor/Host/MaterialEvidenceRequest.cs:193-197` throws on a null constituent. Host initialization timing matters. No crash was observed.
- **Fix:** Put the alpha schema in the interpreter before its request initializers and retarget its readers. Include the source gatherer at `Packages/com.alrauna.amuse/Editor/Semantics/Poiyomi/PoiyomiSourceAttestation.cs:197-202`. Keep the used material-analysis facade.
- **If we skip it:** A future initialization or entry-path change must preserve an undocumented type-order rule. Removing the initialization dependency is a stronger saving than moving callback helpers for navigation alone.


### 5. Build the slot list once and keep it with its results

- **What this is:** Slot records define which material states and texture facts belong to each slot. `RuntimeStateResolution.ResolveRuntimeStates` constructs them at `Packages/com.alrauna.amuse/Editor/Analysis/RuntimeStateResolution.cs:256-258`.
- **Problem:** Build and preview construct the same list again at `Packages/com.alrauna.amuse/Editor/Build/AmusePlatformFinishPass.cs:652-655` and `Packages/com.alrauna.amuse/Editor/DryRun/AlphaSeparationDryRun.cs:638-641`. A new parser or renderer filter must reach both calls, yet the build comment at lines 660-664 says there is one call.
- **Fix:** Construct the list once and pass it into resolution, or return it with the result. Preserve refusal order and both result contracts. Renderer-wide early refusals have empty slot results. An all-slots-refused result retains per-slot evidence for preview rows.
- **If we skip it:** `[INFERENCE]` A future argument mismatch can attach a refusal to the wrong admitted material. Each successful resolve also repeats list allocation and binding traversal. No performance gain was measured.

The maintenance saving is a structural pairing rule instead of a comment that asks callers to stay in sync. Preparation has a separate valid use at `Packages/com.alrauna.amuse/Editor/Build/AlphaSeparationPreparation.cs:394-397`. Do not delete that use.

### 6. Give policy fingerprint fields one owner

- **What this is:** A fingerprint is a key that identifies the settings used for a result. Play records list the settings at `Packages/com.alrauna.amuse/Editor/Build/PlayModeBuildStore.cs:49-74`.
- **Problem:** Preview lists the same 19 fields again at `Packages/com.alrauna.amuse/Editor/DryRun/AlphaSeparationDryRun.cs:1401-1430`. Adding one setting to only one list can leave either stored build rows or preview cache rows valid under the wrong policy.
- **Fix:** Reuse the existing pure formatter in `PlayModeBuildStore.ComputePolicyFingerprint` from preview and delete its private formatter. Keep the target and caller-specific cache structure. Add no new field-list abstraction or general settings framework.
- **If we skip it:** `[INFERENCE]` Every new setting requires two coordinated key edits. The controller verified that the two lists agree in both fields and order on 2026-10-21. This is a future drift risk, not a present mismatch.

The duplicate method is 31 gross lines. Direct reuse permits about 30 fewer production lines, subject to the final call-site edit. Keep behavioral invalidation checks. Do not add a test that only checks forwarding.

### 7. Give serialized row kinds one typed vocabulary

- **What this is:** Build stores row shape as an integer at `Packages/com.alrauna.amuse/Editor/Build/PlayModeBuildStore.cs:10-20`. Preview defines the meaning in an enum and casts the integer at `Packages/com.alrauna.amuse/Editor/DryRun/MaterialTransformationRow.cs:13-20,152-164`.
- **Problem:** Build writers use numeric literals while the reader relies on enum order. `[INFERENCE]` Inserting or reordering a row kind can change how a same-session stored row is drawn without a compiler error.
- **Fix:** Put the shared enum beside the stored row contract. Give its existing values explicit numbers and use that type at all writers and readers. Keep the serialized numeric values unchanged.
- **If we skip it:** Each new row shape requires the maintainer to recover an undocumented integer protocol. A typed contract removes that repeated work.

### 8. Use the existing approved-identity predicate in both consumers

- **What this is:** Consent suppression and preview cause classification distinguish an identity refusal from a material-state refusal. One predicate owns this distinction at `Packages/com.alrauna.amuse/Editor/Semantics/UnityMaterialSemantics.cs:449-457`.
- **Problem:** The consent scan spells the same rule again at lines 626-635. The spellings agree only after applying the captured-record invariants and the earlier attestation filter.
- **Fix:** Call `IsApprovedIdentityFailure` from the consent scan. Keep the approved-version policy and the material-state refusal distinction unchanged.
- **If we skip it:** `[INFERENCE]` A new refusal kind can make consent suppression and preview cause classification disagree. This is a small cut with useful review leverage because the rule concerns what the user accepts.

The controller checked a Boolean model of the three constructible unattested shapes. It found no mismatch between the predicates. This was not C# execution and does not establish a current bug.

### 9. Reconcile shader measurement status at its owning rows

- **What this is:** Attestation comments record why a shader version is admitted. `Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonSourceAttestation.cs:2728-2738` records one measured 2.4.1 shape.
- **Problem:** The same file claims every Multi digest has at least two measured shapes at lines 2766-2773. Repeated version lists also omit 2.4.1 at `Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonMultiResolution.cs:29-34,124-130` and `Packages/com.alrauna.amuse/Tests/Editor/Build/VerifiedLilToonTestSeams.cs:168-177`.
- **Fix:** State each evidence item's status at its owning row and link to it elsewhere. Correct the two-shape claim and delete repeated version enumerations. Keep separate statuses for regular-family evidence, Multi evidence, and the opaque tuple.
- **If we skip it:** The next patch admission starts by reconstructing which checks ran. `[INFERENCE]` That recurring review cost is larger than the few lines saved by deleting repeated prose.

The owner explicitly allowed pins to change before installed-shape confirmation at `docs/superpowers/specs/2026-10-20-liltoon-2-4-1-admission-design.md:15-19`. Line 29 records completed checks and remaining regular-family and opaque-tuple work. This audit does not revoke that exception or create a new admission gate. Do not infer that a Multi measurement completed the other work.

## Nice to have

### 10. Keep session-cache reset membership in one place

- **What this is:** Build entry and clone sweep clear the same seven caches at `Packages/com.alrauna.amuse/Editor/Build/AmusePlatformFinishPass.cs:206-212` and `Packages/com.alrauna.amuse/Editor/Build/AlphaSeparationApply.cs:1056-1062`.
- **Problem:** A new cache requires two list edits. No present mismatch was found. Missing a sweep clear alone does not prove next-build contamination because entry also clears the caches.
- **Fix:** Put this fixed list in one Build helper and call it at both existing moments. Do not merge the reset moments or create a general cache manager.
- **If we skip it:** Each new cache adds a small change-together rule. This is a modest maintenance cut, not a demonstrated correctness repair.

### 11. Extend the research guard to cover its no-write promise

- **What this is:** The anonymizer promises no file or network access at `Packages/com.alrauna.amuse.research/Editor/Census/CensusAnonymizer.cs:8-15`. The existing guard checks Unity mutation at `Packages/com.alrauna.amuse.research/Tests/Editor/Collection/ResearchSourceApiBanTests.cs:31-59,89-121`.
- **Problem:** A diagnostic could write raw input without changing the returned anonymous records. The existing mutation guard would not detect that added file write or network send.
- **Fix:** Extend the existing guard with direct persistence and transmission calls that violate this promise. Keep the test-source exclusion. Add no export feature or new guard framework.
- **If we skip it:** `[INFERENCE]` Reviewers must remember the no-write promise for each research change without the cheap tripwire used for Unity mutation.

The controller passed synthetic file-write and network-send source snippets through the existing literal and pattern checks. Both produced zero guard hits. Neither snippet was executed. This is a prevention opportunity, not evidence of a present leak or a security barrier against hostile code.

### 12. Replace the deliberate property-count pin with a durable contract

- **What this is:** `Packages/com.alrauna.amuse/Tests/Editor/Semantics/ShaderSemanticsArchitectureTests.cs:73-92` requires exactly 48 lilToon property constants. The plan explicitly requested that count at `docs/superpowers/plans/2026-10-19-shader-semantics-decoupling-and-liltoon-consistency.md:261-268`.
- **Problem:** One valid new constant requires a count edit even when its behavior is correct. The count is deliberate, but no source establishes it as a shader safety limit.
- **Fix:** Recommend a test-policy change to non-empty, named, unique constants, as the Poiyomi sibling already uses. Preserve checks that protect property behavior. Do not replace 48 with another fixed count.
- **If we skip it:** Supported cases that grow this list keep requiring a deliberate test-policy edit. This is a small future cost, not a current product defect.


### 13. Register the website download action once

- **What this is:** `Website/app.js:111-126` adds a menu-item listener each time a row menu opens.
- **Problem:** The code registers an anonymous wrapper but tries to remove the inner function. The wrapper stays registered, so later change events invoke multiple download actions.
- **Fix:** Capture the selected URL synchronously from the row-button click's `event.currentTarget`. Let one stable download listener use that saved URL. The menu item does not own the package URL. Verify the supported Fluent activation event before changing it. No new dependency is needed.
- **If we skip it:** Repeated menu opens retain handlers. The isolated browser run observed one download action after one open and two actions after the second open.

The smoke page used source markup and unchanged handlers without the external Fluent library. It proves listener lifetime behavior. It does not prove that a normal click fails on the published site.

## Rejected cuts and policy risks

- **No non-alpha deletion:** `Packages/com.alrauna.amuse/Editor/Semantics/MaterialSemantics.cs:1102-1109` already exposes only Alpha. The old four-output cut is stale. The vision document explicitly pins its implementation snapshot at lines 7 and 49. Its long-term direction is not dead code.
- **No approved-range policy reversal:** `Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonSourceAttestation.cs:366-375` explicitly records the owner choice to admit approved versions on the build path without exact identity. An unseen patch can change semantics. This is an accepted compatibility risk, not a newly discovered implementation defect. This audit does not recommend an extra digest gate that reverses the choice.
- **No generic preset metadata table:** Existing parser, apply, and match tests protect distinct behavior. A new table, value-kind dispatcher, and reflection checks would add a second language for a small fixed setting set.
- **No collapse of proof branches for line count:** Exact arithmetic, filter rules, family-specific cutoff bounds, and the Unknown outcome carry correctness rules. No runtime or mathematical evidence justifies a cut in this audit.
- **No deletion of the research probe:** Research and tests call the simpler analysis path. Its narrower inputs do not establish redundant product behavior. A proposed parity test needs a concrete shared consumer contract before it earns permanent coverage.
- **No lifecycle shortcut:** Keep the five NDMF passes, live apply revalidation, both cache-reset moments, and unlock-window close ordering.
- **No fixture framework:** Repeated unlock setup has maintenance cost, but the scans did not prove a present cleanup failure. Add no fixture hierarchy or generalized resource manager on that basis.
- **No claim that release automation ran:** Only local package membership was exercised. The research package stayed outside that archive.

## Adversarial review

Two parallel reviewers challenged this draft on 2026-10-21. One checked safety evidence and refusal contracts. One checked maintenance leverage, remedies, and reporting claims.

The revision preserves distinct early-refusal contracts, records the authorized measurement exception, and removes the claim that a missed sweep reset alone contaminates the next build. It adds the root-key collision and the closure-refusal deferral case. It upgrades the preview finding from a disclaimer proposal to stale-result rejection under the recorded contract. It narrows the Poiyomi proposal to schema initialization ownership and corrects the website event-data owner.

The reviewers also confirmed that the property count came from an explicit plan. The report therefore treats its replacement as a small test-policy change, not an accidental author error. No reviewer ran Unity or established a runtime crash.

## Verification targets for later implementation

Preserve existing refusal scope, report rows, and no-mutation paths. Use the existing focused resolver, preview, build-store, plugin, consent, and apply suites. Run the product and research assemblies after a real change.

Behavior checks must catch a plausible wrong result. Cover distinct same-named roots and Play identity correlation. Complete a preview, edit policy, close and reopen the foldout, and reject its stale rows. Combine an invalid swap with a valid build-time material capability and retain the refusal. For slot ownership, check typed bindings and slot attribution. For policy keys, check stale-record rejection and target changes. For row kinds, preserve old numeric payloads and their drawn shapes. Do not add tests that only inspect helper calls or count fields.

Static initialization needs fresh-domain entry checks in both orders. Warmed types can hide the cycle. No such check ran in this investigation.

Measurement-status edits need an evidence record, not a green test substituted for measurement. The research guard needs a synthetic forbidden-call counterexample. The website action needs browser execution with the shipped Fluent runtime before a published-site fix is claimed.

## Verdict

Fix result identity, completed-preview validity, and refusal-cause precedence first. Remove the Poiyomi initialization dependency next. No unsafe build mutation was established in the paths read. The highest-value savings are fewer coordinated edits and fewer hidden caller obligations, not a broad source shrink.

Lean: about 30 duplicate policy-key lines can go without a new abstraction. Other line savings were not established to a reliable net total. No dependency removal was established. No code was deleted by this investigation.

## Record checks

On 2026-10-21, structural checks found 13 numbered findings with all four required parts. All 38 explicit source citations resolved to existing files and valid line ranges. The identifier sweep found no forbidden identifier, machine path, or network endpoint in this changed document. The dated-language and semicolon checks also passed.

The five pre-existing modified configuration and project-setting files matched their in-memory byte fingerprints. The investigation changed no product source, test, or asset.

## Not checked

No Unity tests, GPU capture, avatar build, upload, private project, or published website ran. No dependency was installed and no network source was fetched. The browser smoke excluded Fluent and the listing template engine.

The proof scan read classifier entry and dispatch code, not every filter walk or exact geometry helper. The Build scan did not fully read material reconstruction, conversion, lifecycle gate bodies, or every apply case. The lilToon scan did not fully read every interpreter, eligibility helper, or source-normalization path. The common-semantics scan did not fully read source attestation or every alpha equation. Large test files were sampled around contracts and call sites. Release workflow, setup script, and website source review do not establish hosted execution.
