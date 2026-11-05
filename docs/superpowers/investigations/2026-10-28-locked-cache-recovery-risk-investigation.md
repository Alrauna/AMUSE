# Locked cache recovery risk investigation

Privacy note. This record describes installed public vendor source and sanitized editor state only. It contains no machine identity, no host or account names, no absolute paths, no private asset names, no GUID values, and no vendor source copies. The vendor cache location is named only as the vendor cache root. Material counts for private project content are given as ranges, not exact figures.

Status: 2026-10-28. Investigation only. No code, test, vendor, settings, asset, or plan changes were made. No real lock ran. Both admission gates stayed false. All live observations below came from the controller. The controller ran read-only checks in the pinned dev editor.

Evidence labels. Source means an installed file read at the cited lines. Live means a controller observation dated 2026-10-28. Model means an inference from source. No model claim was exercised at runtime.

## Question

A prior Task 1 safety review claimed this blocker. Cache deletion schedules the recovery callback through the delayed editor call queue. The real-lock teardown does not observe that callback. The vendor could then scan the project and unlock unrelated recoverable materials. This record checks that claim against the installed source. It separates the observed dev-project exposure from the general risk and the final snapshot claim.

## Method

Primary sources only. The installed vendor editor package and the Task 1 harness were read at fixed line ranges. No vendor code was copied into this record. The controller supplied runtime observations. Those were not reproduced here and no trigger probe was built.

## Recovery trigger map

The recovery class sits in Packages/com.poiyomi.thryeditor/Editor/LockedShaderRecovery.cs.

### What queues a pass

Source: LockedShaderRecovery.cs lines 110 to 135. An asset postprocessor hook runs after each import batch. Its queue rule has three parts.

1. Every imported asset path ending in the material extension is collected into a static list. Case does not matter. This list survives between batches until a pass runs.
2. Every deleted asset path that lies under the vendor cache root sets a static full scan flag. The check is a prefix test on the deleted path. Source: Packages/com.poiyomi.thryeditor/Editor/LockedShaderCache.cs lines 55 to 58. The loop stops at the first hit.
3. If either the material list or the full scan flag is set, and no pass is queued, the hook subscribes its run method to the delayed editor call queue.

So the answer is yes. All material imports queue a targeted pass. All cache path deletions queue a full scan. Moved asset paths are ignored by both rules. A single batch can carry both kinds of events. A batch with a cache deletion does a full scan only, because the run method drops the collected material list in that branch.

Source: LockedShaderRecovery.cs lines 139 to 181. The run method unsubscribes itself and clears the queued flag. While the editor compiles or updates assets, it requeues itself and returns. Otherwise it snapshots the flag and the material list and clears both. A full scan then calls RecoverAll over every material in the project. Source: lines 95 to 104. A targeted pass loads only the collected material paths and calls Recover on them.

### Session guard and early exits

Source: LockedShaderRecovery.cs lines 159 to 166. The full scan branch is guarded by a session flag. The flag lives in session state, so it survives script reloads and resets when the editor restarts. The first full scan in an editor session consumes it. Later deletion events in the same session reach the guard and return without work.

Early exits in order. No imported materials and no deletion flag means no queue step at all. A queued pass is not queued twice. A compiling or updating editor defers the pass. A consumed guard stops the full scan. Inside Recover, an empty recoverable list returns before any write call. Source: lines 71 to 92. The catch around the run method logs and swallows exceptions, so a pass never breaks an import. Source: lines 175 to 179.

The menu item bypasses the postprocessor and the guard. It calls RecoverAll directly. Source: lines 187 to 197. The dev editor session flag was observed false on 2026-10-28. Live.

### Orphan predicate

Source: LockedShaderRecovery.cs lines 31 to 40 and Packages/com.poiyomi.thryeditor/Editor/Unity.cs lines 289 to 292. A material is an orphan when its current shader is broken or missing and the lock tags say it was locked. A broken shader means a null shader or a shader whose name equals the Unity internal error shader name. Source: Packages/com.poiyomi.thryeditor/Editor/ShaderOptimizer.cs L3437–L3440.

The lock test for a broken shader reads tags only. Source: ShaderOptimizer.cs L3466–L3488. It resolves the original shader once more and either checks the shared lock tag or asks whether the resolved original is an optimizer shader. The optimizer shader check caches a boolean per shader. Source: ShaderOptimizer.cs L3401–L3405. None of these reads write, tag, dirty, or save persistent assets.

### Original shader resolution

Source: ShaderOptimizer.cs L3000–L3036 and L3044–L3068. Resolution tries two tag lookups. First a shader GUID tag through the asset database. Then an original shader name tag through Shader.Find. Both are pure reads. If both fail and the current shader is broken, resolution returns null. The name guessing branch sits after that return, so recovery can never reach it. Recovery resolves the original only for materials that already passed the orphan test, and it always passes the silent flag.

### What recovery writes

Source: LockedShaderRecovery.cs lines 71 to 92. Recover splits its input into recoverable and unrecoverable. Each unrecoverable material produces one log entry. The entries go through the vendor logger error method, which prints through the ordinary Unity log call with a colored prefix. Source: Packages/com.poiyomi.thryeditor/Editor/Helpers/Logging.cs lines 49 to 57 and 69 to 83. The method name alone does not establish an unexpected error log in NUnit. The callback was not run to measure test-runner behavior.

With at least one recoverable material, Recover calls the vendor unlock entry point with a progress bar on the full scan path. Source: ShaderOptimizer.cs lines 493 to 496 and L908–L1146. The unlock batch does this.

1. It registers editor use once per session. Source: line 910.
2. It keeps only materials that still count as locked. Source: lines 916 to 921.
3. It restores each material inside a suspended import window. The per material unlock call sits at L1032–L1035.
4. It fixes keywords after unlock when that setting is on. It re-links material links. Both default on or apply per config. Source: L1060–L1066 and Packages/com.poiyomi.thryeditor/Editor/Config.cs lines 64 to 65.
5. It resumes imports and refreshes.
6. With the default save setting on, it defers a save of all assets and then trims the cache against its size budget. Source: L1099–L1106 and L1191–L1204. Without it, it trims right away. Source: L1108–L1113. The configured runtime values were not measured.

### Cache write reachability

The unlock batch can delete cache entries only through the budget trim calls named above. The vendor also deletes cache entries from the remove unused menu, the clear menu, and the budget trim. Source: LockedShaderCache.cs lines 299 to 361 and 397 to 402. The lock itself writes sidecar and readme files inside the cache root without an import event. Source: lines 173 and 227.

So recovery reaches the cache through exactly one path. A non-empty recoverable list, then an unlock batch, then a trim. With zero recoverable materials, recovery performs no cache operation at all.

## Minimal conditions for unrelated writes

Model, from the source map above. Recovery can reach the unrelated-material mutation path when all of these hold.

1. A recovery pass runs. Any material import anywhere, or any deletion under the vendor cache root, is enough. The menu item also works.
2. The pass sees at least one material that is locked by tags, has a broken or missing shader, and resolves a present, unbroken original shader.
3. For the full scan path, the session guard must still be false. The targeted import path has no guard.

The unlock path changes material state in memory. Keyword changes, a global save, and cache trimming depend on the active configuration and successful execution. This investigation did not read that configuration or observe a save. With an empty recoverable list, the recovery method returns before the unlock path. It can still emit ordinary log messages about unrecoverable materials.

## Interaction with the Task 1 harness

Source: Packages/com.alrauna.amuse/Tests/Editor/Build/BundledLockedOriginalRealLockTests.cs.

### Drain gap

The harness waits for vendor work before cleanup. It treats shader compilation as pending and inspects the editor update delegate list for three vendor method names. Source: L1009–L1022. The recovery run method subscribes to the delayed editor call queue instead. The static queue flags are plain fields. The drain check sees none of this recovery work. The prior review claim is correct at the source level.

Model. The lock in a case saves its material. That import queues a targeted pass. The teardown then deletes owned fixture and cache assets. Those deletions queue a full scan through the same hook. Both passes are invisible to the drain check. The delayed queue drains on editor updates, so a pass can land between test phases or inside a later test that yields. In a fresh editor session the first deletion event triggers one real full scan, because the guard starts false. Later events in the same session hit the guard.

### Cleanup order

Source: L1024–L1125. The teardown drains vendor update callbacks, then attempts to delete fixture material and texture assets, then deletes owned cache entries or the whole cache root. It then compares the cache against its pre-lock snapshot. Successful fixture deletion removes the owned material before its shader is deleted. A failed deletion does not establish that guarantee. The comparison does not wait for delayed recovery work scheduled by cleanup.

### Byte snapshot claim

Model. The comparison proves cache equality at its observation point. It does not prove that a delayed pass has completed. If that pass finds a recoverable material, it can enter the unlock path after the comparison. Depending on configuration and execution, that path can save materials and trim the cache. An equal cache snapshot alone also does not prove that unrelated materials stayed unchanged.

## Risk classification

### Actual risk now

Live, 2026-10-28. The exact vendor partition found no recoverable material in the dev-project material set. Unrecoverable materials were present. Their names and exact counts are not recorded. Model: with the same material set and values, recovery has no restoration target. It can still log messages, load materials, and consume the session guard. No unrelated material mutation from this callback was observed. This result is not a guarantee after asset changes or concurrent operations.

### General conditional risk

Model. A project with a recoverable orphan and a matching recovery trigger can reach the mutation path. The first cache deletion in an editor session can request the full scan. A subsequent unlock can dirty unrelated materials. It can also save assets and trim the cache, depending on configuration. A pass after cleanup is therefore relevant to source protection and to the final snapshot boundary.

### Byte snapshot claim risk

Model. No recoverable target was found in the observed dev-project state. Recovery itself cannot reach its unlock or cache-trim path if that remains true when it runs. The harness still does not observe the delayed callback. The observed empty partition does not satisfy LA9's separate requirement to finish queued work before claiming final cleanup.

## Live evidence

All live items below were observed by the controller on 2026-10-28 in the pinned dev editor. Persistent assets were read only. One synthetic control used a transient material, then destroyed it.

1. The controller used the same material enumeration and filtering as RecoverAll, then called the actual vendor Partition method. It did not call RecoverAll. The scan had inputs and no null loads. No recoverable materials were found. Unrecoverable materials were present. Live.
2. The partition scan left all scanned serialized material values, their dirty flags, and the accessible material and meta file bytes unchanged. Only returned material paths accessible on disk were hashed, so this is not a proof over all project assets. Live.
3. The recovery session flag, the queued flag, and the full scan flag were all false. No recovery subscription sat on the delayed call queue. Live.
4. A synthetic control proved the scan is not a stub. A transient nonpersistent material with a healthy shader landed in neither partition. The same material with a broken shader and a resolvable original landed exactly in recoverable. With a missing original and a lock tag it landed exactly in unrecoverable. All expected booleans held. The transient was destroyed. No file, folder, or cache entry was created. No recovery pass or unlock ran. Live.
5. After the observations, the cache snapshot matched its baseline including file digests and root metadata. Settings file bytes and serialized player settings were unchanged. Harness file bytes were unchanged. The gate, the owned fixture, and its metadata were absent. No persistent asset was created at any point. Live.
6. Source inspection confirmed that the vendor error log method calls Debug.Log. The method was not invoked as a runtime logging probe.

Not exercised. No real deletion trigger, no real lock, no unlock, no callback roundtrip, and no recovery pass were invoked. Live mutation outcome beyond the partition scan is unknown at runtime.

## Unknowns

1. Whether the delayed call queue drains inside the target test runner between edit mode cases. Source says editor updates drain it. Model only.
2. The active save configuration, cache budget, and later cache pressure were not measured. Source defaults do not establish the loaded configuration or a runtime trim result.
3. Whether a cached entry moved out of the cache root creates an undetected orphan. The move event paths are ignored by the hook. Model only, not exercised.
4. Why the dev project holds unrecoverable orphans at all. Out of scope for this record.
5. Exact behavior of a full scan when the editor is compiling at queue time. Source shows a requeue. Model only.

## Recommendation

Proportionate to the observed empty recoverable partition. The source-mutation concern does not justify an indefinite blanket stop in this dev-project state. Task 1 still needs to close its delayed-work completion gap before claiming LA9 cleanup. The following bounded changes stay inside test lifecycle safety. They do not widen the aligner.

1. Refuse pre-existing recovery work. Before a lock, run the vendor material enumeration and Partition read-only. Refuse an unrelated recoverable target. Keep exclusive operation ownership through cleanup. The empty result is conditional on stable unrelated inputs, not a guarantee about every later project state.
2. Observe the recovery callback on delayCall and its queued work alongside the existing vendor update callbacks. After deleting owned assets, wait for deletion-triggered recovery and any update work it schedules. Keep the existing watchdog and sticky ownership. Do not invoke, unregister, or clear vendor recovery manually.
3. Compare the cache only after those callbacks finish. Keep source-protection checks separate from cache equality. Validate the changed lifecycle with the planned disposable smoke before the larger matrix.

The smallest response is a test-only lifecycle correction. Do not clean the project, change vendor settings, consume the session guard manually, add a product service, or build a general callback framework. An empty partition narrows the source-write risk. It does not remove the need to observe final callback completion.

## Validation limits and review

No real deletion trigger, real lock, unlock, recovery callback roundtrip, or test-runner callback ordering was exercised. These remain implementation smoke requirements, not evidence obtained by this investigation.

Status: 2026-10-28. An independent adversarial reviewer checked this record against the installed recovery, optimizer, cache, and logger source, the test harness, and LA9. No blocking finding remained. The reviewer accepted both the conditional risk assessment and the bounded recommendation. The review did not run Unity or validate a callback roundtrip.

Implementation remains stopped. This record does not authorize a code change. The next implementation step is the bounded Task 1 safety correction and its disposable lifecycle smoke, not a project cleanup or a new aligner rule.
