# Full Repo Audit Remediation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Execute the 32 findings the remediation spec selects, each with its own contract (conservative fix, outcome-identity perf, or reporting/contract hygiene).

**Architecture:** Five NDMF passes in one editor assembly; the plan touches `Host/` capture routes, `Analysis/` proof core, `Semantics/` lilToon and Poiyomi frontends, `Build/` passes, and the mirrored test assemblies. Finding 5 re-cuts the Analysis-to-Host call direction before finding 8 builds on its seam.

**Tech Stack:** C# / Unity 2022.3.22f1, NDMF 1.14.4, NUnit via Unity Test Runner (EditMode). Tests run through the Unity MCP `run_tests` tool; there is no CLI test runner.

**Spec:** `docs/superpowers/specs/2026-10-15-full-repo-audit-remediation-design.md` (normative; its decision 14 was amended on 2026-10-15 after the plan review measured the GPU contents-hash conflict). Investigation: `docs/superpowers/investigations/2026-10-15-full-repo-audit.md`.

**Revision note:** first issue 2026-10-15. Three parallel adversarial reviews the same day returned 3 blockers (task 3 invented a verdict enum and an illegal byte state; task 6 missed a seventh Host call and could not compile its own delegate shapes; task 20's continuation opened an `ObjectDisposedException` race) plus systematic phantom-symbol corrections in test snippets. This second issue fixes all of them, replaces unobservable counting seams with observable or source-inspection proofs, and completes the Files lists and global constraints.

## Global Constraints

- All paths below are relative to `Packages/com.alrauna.amuse/` unless prefixed `Tests/` (same root).
- Branch: `audit/full-repo-audit-2026-10-15`. Commit after every task's green run. Stage only paths the task names, plus the `.meta` of any file a task creates or deletes (Unity tracks one `.meta` per asset; an unstaged `.meta` leaves untracked state and a staged-later `.meta` churns GUIDs).
- No push, no pull request, no tag, no release, no publication. Never stage generated Unity state or unrelated user-owned changes.
- Contracts from the spec: contract 1 = findings 1, 2, 3, 14 (RED before green, conservative direction); contract 2 = findings 8, 9, 10, 11, 12, 30, 31, 32, 33 (outcome-identity at the stated boundaries); contract 3 = the rest (hygiene, pins move deliberately).
- No new production types except: the `PreparationRefusal` enum (task 18), the one `AvatarAnimationRefusal` member (task 23), and the named internal delegate types of task 6 (authorized by spec decision 7's injected-boundary shape). No new dependency. No attestation constant changes.
- Where a counting seam does not exist, a task never builds one. Contract-2 proof is outcome identity through the existing suites; the performance claim is recorded from the code shape, per the capture suite's own "confirmed by review rather than by instrumentation" precedent (`Tests/Editor/Host/UnityMaterialEvidenceCaptureTests.cs:789-793`).
- A filtered test run that reports 0 tests is a failure. Record observed counts in the task result.
- After creating or deleting any file: Unity refresh (MCP `refresh_unity`) before compiling tests.
- Every user-visible sentence is simple English: short active sentences, no contractions.
- Before each commit: identifier sweep on changed files (at-sign plus hex, drive letters, home paths, ports). Any hit is a defect.
- Stop conditions: see the spec's "Validation and stop conditions". A red test whose failure is not the task's named behavior is a stop, not a fix-by-test-edit.

Test-run notation used below:

- `RUN <filter>` = MCP `run_tests`, EditMode, `filter: <filter>`, then `get_test_job` to completion. Record passed/failed counts.
- `REFRESH` = MCP `refresh_unity`, wait for readiness.

---

### Task 1: Finding 1 — streaming cache keys on exact cutoff and route provenance

**Files:**
- Modify: `Editor/Host/UnityStreamingTextureEvidence.cs:95-106` (key build), `:119` (gate value source)
- Test: `Tests/Editor/Host/UnityStreamingTextureEvidenceTests.cs`

**Interfaces:**
- Produces: cache key tuple `(guid, source ticks, meta ticks, channel, exactCutoff, bounds, diskFallbackAllowed)` — task 8 replaces the last element with the request parameter.
- Consumes: the suite's direct-call pattern `UnityStreamingTextureEvidence.TryCapture(texture, channel, cutoff, bounds, out chain)` inside a `DisallowDiskFallback()` scope (`UnityStreamingTextureEvidenceTests.cs:315-331` pattern), with a fixture modeled on `ImportStreamingStrayTexture` / `ImportExrStreamingBandTexture`.

- [ ] **Step 1: Prepare the fixture and write the failing test**

The fixture texture needs at least one texel at alpha byte 128 (a 2×1 PNG with bytes 128 and 0 works; import it streaming like the suite's existing fixtures). Then:

```csharp
[Test]
public void TwoCutoffsInOneBucketStraddlingByte128CaptureSeparateChains()
{
    AlphaTextureData first; AlphaTextureData second;
    using (DisallowDiskFallback())
    {
        UnityStreamingTextureEvidence.TryCapture(Texture, TextureChannel.Alpha, 0.50196f, Bounds, out first);
        UnityStreamingTextureEvidence.TryCapture(Texture, TextureChannel.Alpha, 0.50205f, Bounds, out second);
    }
    // byte 128 decodes to ~0.50196: it passes the first cutoff, fails the second.
    Assert.That(second.GetAlpha(0, 0), Is.Not.EqualTo(first.GetAlpha(0, 0)),
        "the quantized key served one chain to both cutoffs");
}
```

- [ ] **Step 2: RUN `UnityStreamingTextureEvidenceTests.TwoCutoffsInOneBucketStraddlingByte128CaptureSeparateChains`**

Expected: FAIL — both cutoffs land in bucket 5020 today, so the fields match. Record the RED.

- [ ] **Step 3: Implement**

In the key build (`:95-106`): replace `cutoffBits` with the exact clamped float `Mathf.Clamp01(cutoff)`, and add a `bool diskFallbackAllowed` element read from the same value the capture gate at `:119` consults. Clamp before keying, mirroring `UnityGeneratedTextureEvidence.cs:104-106`.

- [ ] **Step 4: RUN the full `UnityStreamingTextureEvidenceTests` class**

Expected: PASS, 0 failures. Any other test failing means the key change broke a sharing expectation — stop and report.

- [ ] **Step 5: Commit** — `fix: key streaming capture cache on exact cutoff and route provenance (finding 1)` (stage the new fixture asset and its `.meta`)

---

### Task 2: Finding 2 — opaque lilToon gates the IDMask fragment-removal set

**Files:**
- Modify: `Editor/Semantics/LilToon/LilToonMaterialSemantics.cs:470-482` (`AlphaCoverageGates`)
- Test: `Tests/Editor/Semantics/LilToon/LilToonAlphaTests.cs`

**Interfaces:**
- Consumes: `LilToonFixtureTestBase.NewFixtureMaterial()` and `Interpret(material)` (`LilToonFixtureTestBase.cs:67-70,299-303`); the cutout suite's IDMask property names and `[TestCase("_IDMask1", 1f)]` rows (`LilToonCutoutAlphaTests.cs:661-670`) as the pattern.

- [ ] **Step 1: Write the failing test**

```csharp
[Test]
public void OpaqueMaterialWithIdMaskValueSetAnswersUnknownNotConstantOpaque()
{
    var material = NewFixtureMaterial();          // base-class helper, opaque lilToon
    material.SetFloat(IdMaskOnePropertyName, 1f); // same property name the cutout suite sets
    var answer = Interpret(material);
    Assert.That(answer.IsUnknown, Is.True,
        "IDMask removes fragments at runtime; an unset gate answered constant opaque");
}
```

- [ ] **Step 2: RUN the new test.** Expected: FAIL — the opaque gates hold only `_Invisible` and `_UDIMDiscardCompile`, so the answer is constant opaque. Record the RED.

- [ ] **Step 3: Implement**

Extend `AlphaCoverageGates` with the same IDMask entries the cutout array carries (`LilToonCutoutMaterialSemantics.cs:65-81`): `_IDMask1..8` and `_IDMaskControlsDissolve`. No other gate changes.

- [ ] **Step 4: RUN the full lilToon semantics classes (`LilToonAlphaTests`, `LilToonCutoutAlphaTests`, `LilToonAdversarialTests`)**

Expected: PASS. New refusals on IDMask materials are the intended conservative direction.

- [ ] **Step 5: Commit** — `fix: gate opaque lilToon alpha on the IDMask fragment-removal set (finding 2)`

---

### Task 3: Finding 3 — conservative block test in the masked source chain

**Files:**
- Modify: `Editor/Host/SourceImageMaskedChain.cs:90-99` (`BuildBlock`)
- Test: `Tests/Editor/Analysis/SourceImageMaskedChainTests.cs`

**Interfaces:**
- Produces: block rule — erase iff `sum <= (NoiseBound - 1) * count`; opaque iff `sum >= OpaqueBound * count`; else the existing mid verdict byte 0. The verdict space already has exactly three values and gains no state: erased is `AlphaTextureData.ErasedFlag` (byte 1), opaque is 255, mid is 0. The classifier's substitution skip key is `ErasedFlag` (1) (`TriangleAlphaClassifier.cs:603`), so a mid block stays a consulted witness. Both predicate shaders and the red-field assertion admit only 0/1/255; no new byte value may appear.
- Consumes: the suite's `SourceImageMaskedChain.Build(...)` entry with a small source buffer, as existing tests drive it.

- [ ] **Step 1: Write the failing test**

```csharp
[Test]
public void BlockAverageWithinHalfStepOfNoiseBoundIsNotErased()
{
    // 2x1 source {5, 6}: exact average 5.5, noise bound 6. The imported
    // chain stores round(5.5) = 6, which is not below the bound. The
    // source route must agree: no erase, no opaque witness, verdict 0.
    byte[] source = { 5, 6 };
    var chain = SourceImageMaskedChain.Build(source, width: 2, height: 1, bounds: BoundsWith(noiseByte: 6, opaqueByte: 128));
    Assert.That(chain.GetAlpha(0, 0), Is.EqualTo(0),
        "a block whose rounded-up average reaches the bound is a live witness, not erased");
}
```

Adapt the argument names to the real `Build` signature; the suite shows the pattern.

- [ ] **Step 2: RUN it.** Expected: FAIL — today `11 < 12` erases the block (flag byte 1). Record the RED.

- [ ] **Step 3: Implement**

Integer rule in `BuildBlock` (`:90-99`): erase iff `count > 0 && sum <= (noiseBound - 1) * count` (bound 0 keeps today's disabled meaning); opaque iff `sum >= opaqueBound * count`; else 0. Block size 1 and sums exactly at `bound * count` keep today's verdicts. Add no state.

- [ ] **Step 4: RUN `SourceImageMaskedChainTests` and `TriangleAlphaClassifierTests`**

Expected: PASS. Every existing erased pin is a count-1 or strictly-below-band block, so no pin moves (verified in review against `SourceImageMaskedChainTests` and `AlphaPolicyBoundsTests.cs:85-88`).

- [ ] **Step 5: RUN one fixture-driven classification case** touching the erased-texel path (`AlphaEvidenceClassifierIntegrationTests`). Expected: PASS, same outcome.

- [ ] **Step 6: Commit** — `fix: make masked-source block erase conservative under store rounding (finding 3)`

---

### Task 4: Finding 14 — consent eligibility sees swap-curved materials

**Files:**
- Modify: `Editor/Build/AmusePlatformFinishPlugin.cs:585-588` (eligibility call)
- Test: `Tests/Editor/Build/TransientUnlockTransformationTests.cs`

**Interfaces:**
- Consumes: `AssignedAndSwapCurvedMaterials(context, graph)` (`AmusePlatformFinishPlugin.cs:1291`), the production helper the consent-subject scan at `:565-566` already uses; the suite's clip-swap fixtures (`TransientUnlockTestLifecycle.AddMaterialSwapAnimation`, `LockedConvertibleMaterialWithOriginal`) and the existing clip-only test at `TransientUnlockTransformationTests.cs:694` as the fixture model.
- Note: the production helper `WindowEligibleForConsent(IEnumerable<Material>, Func<Material, bool>)` lives in `TransientUnlockAvailability.cs:73-76`; the fix changes the plugin's call argument, not the helper.

- [ ] **Step 1: Write the failing test**

Drive the plugin's execute path (or the eligibility path it calls) with an avatar whose locked material appears only in a swap curve, mirroring `AClipOnlyLockedMaterialGetsItsCloneAndRemaps` at `:694`:

```csharp
[Test]
public void LockedMaterialReferencedOnlyByAClipMakesTheWindowEligible()
{
    // Today the eligibility call scans sharedMaterials only, so a locked
    // material that lives only in a swap curve never opens the window and
    // the documented clip-only unlock never fires.
    var eligible = ExecuteThroughThePluginPathWithClipOnlyLockedMaterial();
    Assert.That(eligible, Is.True, "the clip-only locked material must make the window eligible");
}
```

- [ ] **Step 2: RUN it.** Expected: FAIL. Record the RED.

- [ ] **Step 3: Implement**

At `:585-588`, replace `AllAssignedMaterials(context)` with `AssignedAndSwapCurvedMaterials(context, graph)`. No other gate changes; the swap-in keeps its own identity and attestation re-checks.

- [ ] **Step 4: RUN `TransientUnlockTransformationTests` and `TransientUnlockSwapInTests`.** Expected: PASS; the widened window only enables the documented path.

- [ ] **Step 5: Commit** — `fix: open the transient unlock window for clip-only locked materials (finding 14)`

---

### Task 5: Finding 22 — measured corrupt-data split in the play-record loader

**Files:**
- Modify: `Editor/Build/PlayModeBuildStore.cs:80-94` (`TryLoad`)
- Test: `Tests/Editor/Build/PlayModeBuildStoreTests.cs`

**Interfaces:**
- Consumes: the suite's seeding pattern — a `PlayModeBuildRecord` serialized through the store, avatar keys via `ComputeAvatarKey`, fingerprints via `ComputePolicyFingerprint`; the stored key is the store's `SessionPrefix + avatarKey`.
- No new production test surface: the propagate arm is proven by source inspection (the narrowed catch admits only `ArgumentException`; every other exception type propagates by construction), matching the 2026-08-19 evidence-plan precedent for unreachable branches.

- [ ] **Step 1: Write the corrupt-payload matrix test**

```csharp
[Test]
public void CorruptStoredPayloadsReadAsNoRecordAndEraseTheKey()
{
    foreach (var payload in new[] { "not json at all", "", "{\"r\":0.5", "[1,2,3]", "{\"r\":0.5}junk", "null" })
    {
        SessionState.SetString(StoreKey(), payload);
        var record = PlayModeBuildStore.TryLoad(AvatarKey(), Fingerprint());
        Assert.That(record, Is.Null, $"corrupt data is data, not a defect: '{payload}'");
        Assert.That(SessionState.GetString(StoreKey()), Is.Empty, $"the corrupt payload is erased: '{payload}'");
    }
}

[Test]
public void AWrongTypedFieldReadsAsNoRecordThroughTheFingerprintGuard()
{
    SessionState.SetString(StoreKey(), "{\"policyFingerprint\":null}");
    Assert.That(PlayModeBuildStore.TryLoad(AvatarKey(), Fingerprint()), Is.Null);
}
```

- [ ] **Step 2: RUN both.** Expected: PASS today (the blanket catch swallows everything). This is the recorded characterization the spec requires before the split — the observable corrupt-data behavior does not change with the fix. Record the counts and label them characterization, not RED.

- [ ] **Step 3: Implement the measured split**

Replace the bare `catch` with `catch (ArgumentException)` around the deserialize call only. Keep the empty-string guard. Every other exception propagates. Measured facts recorded in the spec decision 11 and re-recorded in the task result: malformed JSON, junk suffix, array payload, and the `"null"` literal throw `ArgumentException`; empty input throws `NullReferenceException` and is already guarded; wrong-typed fields return a default record that the fingerprint check rejects.

- [ ] **Step 4: RUN the full `PlayModeBuildStoreTests`.** Expected: PASS, the matrix green.

- [ ] **Step 5: Commit** — `fix: corrupt play-record payloads read as data, defects throw (finding 22)`

---

### Task 6: Finding 5 — Analysis stops calling Host; orchestrators inject

**Files:**
- Modify: `Editor/Analysis/RuntimeStateResolution.cs` — call sites `:78-79` (StructuralRefusalFor), `:89-96` (ResolveProofRelevant, `out` parameter), `:114-116` (IsUnrecognizedObjectMaterialBinding), `:140-143` (GatherAlphaFields, hoisted), `:340-350` (Classify/IntersectOutcomes), `:415-421` (TryParseMaterialSlotBindingFor). `:304-306` is a Host *type* reference, not a call: no edit.
- Modify: `Editor/Analysis/AdmittedMaterialStates.cs:843-844` (constant threads as value), `:1040-1046` (type reference, no edit), `:315` (Semantics default stays, permitted)
- Modify: `Editor/Build/AmusePlatformFinishPlugin.cs:842,956` and `Editor/DryRun/AlphaSeparationDryRun.cs:590,674` (injectors; the orchestrators already call `MaterialSlotsFor`'s Host parse at `:848-849` / `:600-601`)
- Modify: `Editor/Host/UnityAnimationEvidenceCapture.cs` and `Editor/Host/UnityRendererAlphaAnalysis.cs` (delegate-typed members exposing the existing internal statics)
- Modify: `Tests/Editor/Build/AmusePlatformFinishPluginTests.cs:5047,5115,7302` (direct `ResolveRuntimeStates` callers pass the same method groups), `Tests/Editor/Build/AlphaSeparationPreparationTests.cs:6851` (direct `ResolveSlot` caller); sweep both files for further callers of the changed signatures
- Test: `Tests/Editor/Analysis/RuntimeStateResolutionTests.cs:36,59`, `Tests/Editor/Analysis/AdmittedMaterialStatesTests.cs`, `Tests/Editor/Host/UnityMaterialEvidenceCaptureTests.cs`

**Interfaces:**
- Produces: five named internal delegate types declared at the Analysis boundary (house style follows `AlphaMaterialRequestSelector` and `AlphaFieldProvider`, `Editor/Host/UnityAnimationEvidenceCapture.cs:60-64`, `Editor/Analysis/AlphaSemanticsResolver.cs:41-44`):
  1. `TryStructuralRefusalFor` over the captured snapshot,
  2. `TryResolveProofRelevant(CapturedFloatBinding binding, string suffixPath, MaterialEvidenceRequest request, out AnimatedPropertyRef reference, string propertyName)` — the `out` parameter requires a named delegate, not `Func`,
  3. `IsUnrecognizedObjectMaterialBinding`-shaped predicate,
  4. `Classify` delegate over the snapshot inputs,
  5. `IntersectOutcomes` delegate,
  plus two values: `AlphaFieldSet` (hoisted gather result, already a `ResolveSlot` parameter) and the `TextureScaleOffsetSuffix` const string. Build and DryRun pass the same Host method groups they call today.

- [ ] **Step 1: Characterization run before any edit**

RUN `RuntimeStateResolutionTests`, `AdmittedMaterialStatesTests`, `UnityRendererAlphaAnalysisTests`, `AlphaEvidenceClassifierIntegrationTests`. Record counts. These are the outcome oracle for the whole task.

- [ ] **Step 2: Hoist the capture call**

Move the `GatherAlphaFields` call out of `RuntimeStateResolution` (`:140-143`) into the two orchestrators, which gather per renderer before calling resolve and pass the `AlphaFieldSet` value in. The gather keeps its position relative to the resolve call, so capture still happens once per renderer before per-slot work.

- [ ] **Step 3: Inject the remaining calls as named delegates**

Convert the six remaining Host calls (`StructuralRefusalFor`, `ResolveProofRelevant`, `IsUnrecognizedObjectMaterialBinding`, `Classify`, `IntersectOutcomes`, `TryParseMaterialSlotBindingFor`) to the five delegate parameters plus the threaded constant (the slot-binding parse and its siblings may share one delegate shape if their signatures allow; otherwise it is a sixth named delegate). The binding loop and first-unrecognized early refusal stay in Analysis. Each classify delegate instance is created once per resolve call, never per submesh or per mip. Delegate parameters get no `null` defaults — a default would keep a Host reference inside Analysis.

- [ ] **Step 4: REFRESH, then RUN the four oracle suites from step 1 plus the four Build suites touched in Files**

Expected: PASS with identical counts. Any outcome diff is a spec stop condition.

- [ ] **Step 5: Verify the acceptance grep**

Search `Editor/Analysis/` for the bare Host type names (`UnityAnimationEvidenceCapture|UnityRendererAlphaAnalysis|UnityMaterialEvidenceCapture|UnityAlphaFieldEvidence|EffectiveMaterialMaterialization|UnityStreamingTextureEvidence|UnityGeneratedTextureEvidence|SourceImageAlphaReader`) — bare names, because split-line calls evade dot patterns. Classify every hit: an accepted record-type/`using` reference (spec decision 7) or a remaining call (defect). Expected: zero call-position hits. Record the grep output, the hit classification, and the injected-boundary list (five delegates or six, per the shared-shape decision, plus two values) in the task result.

- [ ] **Step 6: Commit** — `refactor: inject Host functions into Analysis at the orchestrator boundary (finding 5)` (stage all ten files the task touched, including the two test files)

---

### Task 7: Finding 8 — triangle-invariant exact math runs once per triangle

**Files:**
- Modify: `Editor/Analysis/AlphaSemanticsResolver.cs:437-467` (hoist out of the mip loop), `Editor/Analysis/RuntimeStateResolution.cs:329-352` (thread through states)
- Modify: `Editor/Analysis/TriangleAlphaClassifier.cs:320-329,430-439` (accept precomputed validity), `Editor/Analysis/ExactUvGeometry.cs` (expose a reusable decode result)
- Test: `Tests/Editor/Analysis/AlphaSemanticsResolverTests.cs`, `Tests/Editor/Analysis/RuntimeStateResolutionTests.cs`, `Tests/Editor/Analysis/TriangleAlphaClassifierTests.cs`, `Tests/Editor/Analysis/ExactUvGeometryTests.cs`

**Interfaces:**
- Consumes: task 6's classify delegate — its inputs gain the precomputed validity record; one delegate allocation per resolve call still holds.
- Produces: a per-triangle validity value carrying position finiteness and the degeneracy verdict, computed once per triangle per state.

- [ ] **Step 1: Characterization run** — RUN the four Analysis suites. Record counts. These are the outcome oracle.

- [ ] **Step 2: Implement the hoist**

Compute position finiteness and `IsDegenerateGeometry` once per triangle per state; pass the result through the mip loop into the classify entry task 6 added. Per-mip UV decodes stay per-mip (they depend on the level's window). No outcome changes. There is no counting seam for exact-geometry work and none is built; the once-per-triangle property is proven by source inspection, recorded in the task result with the hoisted statements cited.

- [ ] **Step 3: RUN the four suites from step 1.** Expected: PASS, identical counts.

- [ ] **Step 4: Commit** — `perf: compute triangle validity once per triangle, not per mip level (finding 8)`

---

### Task 8: Finding 28 — the disk-fallback switch becomes request state

**Files:**
- Modify: `Editor/Host/UnityMaterialEvidenceCapture.cs:81` (`MaterialEvidenceRequest` gains `AllowDiskFallback`, default true), `Editor/Host/UnityAlphaFieldEvidence.cs:98,216` (signature), `Editor/Host/UnityStreamingTextureEvidence.cs:56-70,95-106,119` (static and scope struct die; gate and the task-1 key element read the parameter)
- Modify: `Editor/DryRun/AlphaSeparationDryRun.cs` (the `DisallowDiskFallback` scope at `:974` becomes the request field)
- Test: `Tests/Editor/Host/UnityStreamingTextureEvidenceTests.cs`, `Tests/Editor/DryRun/AlphaSeparationDryRunTests.cs`

**Interfaces:**
- Produces: `MaterialEvidenceRequest.AllowDiskFallback` (bool, default true) flowing to the streaming gate and key; the DryRun sets it per request. Task 9 adds the raw mip cap to the same request.

- [ ] **Step 1: Write the failing test**

```csharp
[Test]
public void ARequestDisallowingFallbackNeverReadsAFallbackCapturedChain()
{
    // Build order: one capture with fallback allowed caches a chain.
    // A second, disallowing request must capture fresh by its own route,
    // not serve the fallback-provenance cache entry (task 1 keyed it).
    var strict = CaptureDisallowedAfterAFallbackCapture();
    Assert.That(strict.CameFromDiskFallback, Is.False);
}
```

Assert provenance through the returned chain's route marker or the source-image decode the disallowed route takes; the suite's fixture patterns supply the capture.

- [ ] **Step 2: RUN it.** Expected: FAIL today — the process-global is the only carrier and one-valued, so the second request silently shares the first route's entry. Record the RED.

- [ ] **Step 3: Implement**

Add the field to `MaterialEvidenceRequest`; thread it through both `TryCapture` signatures to the streaming gate at `:119` and the key element from task 1; delete `AllowDiskFallback` and its scope struct; the DryRun sets the request field where it built the scope. Default true keeps behavior as observed on 2026-10-15.

- [ ] **Step 4: RUN `UnityStreamingTextureEvidenceTests`, `UnityAlphaFieldEvidenceTests`, `AlphaSeparationDryRunTests`.** Expected: PASS.

- [ ] **Step 5: Commit** — `refactor: bound the disk-fallback switch to the capture request (finding 28)`

---

### Task 9: Finding 9 — GPU alpha route caches and caps before readback

**Files:**
- Modify: `Editor/Host/UnityAlphaFieldEvidence.cs:317-356` (`TryCaptureChain` cap + cache fill), `Editor/Host/UnityMaterialEvidenceCapture.cs` (cap from the optimizer policy into the request)
- Modify: the two per-build clear blocks (`Editor/Build/AmusePlatformFinishPlugin.cs:456-460`, `Editor/Build/AlphaSeparationApply.cs:1047-1050`) — anchor by symbol, tasks 10 and 11 also append here
- Test: `Tests/Editor/Host/UnityAlphaFieldEvidenceTests.cs`, `Tests/Editor/Host/UnityMaterialEvidenceCaptureTests.cs`, `Tests/Editor/Host/UnityRendererAlphaAnalysisTests.cs`

**Interfaces:**
- Consumes: task 8's request (adds `RawMipCap`).
- Produces: session cache keyed on `(instance identity, channel, threshold, bounds, active mipmap limit, update count, RawMipCap)`. The generated route's contents-hash element is dropped per the amended spec decision 14: the GPU route has no CPU-side pixels, so hashing would cost a readback and defeat the cache on every hit. The chain is stored already truncated to the cap.

- [ ] **Step 1: Write the failing tests**

```csharp
[Test]
public void LevelsAboveTheRawCapArriveAsPlaceholdersNotEvidence()
{
    var chain = CaptureWithCap(1);          // fixture texture with > 2 levels
    Assert.That(chain.Count, Is.EqualTo(2), "cap 1 keeps levels 0 and 1");
    Assert.That(chain.Level(1).IsLevelWithoutEvidence || IsEvidencePresent(chain.Level(1)), Is.True);
    // Today the chain carries texture.mipmapCount levels.
}

[Test]
public void ASecondCaptureOfTheSameTextureServesTheCachedChainInstance()
{
    var first = CaptureWithCap(2);
    var second = CaptureWithCap(2);
    Assert.That(second, Is.SameAs(first), "the cache must serve the stored instance");
}
```

The first test's today-failure: the returned chain has `texture.mipmapCount` levels. The second's today-failure: each capture builds a fresh `AlphaMipChain`.

- [ ] **Step 2: RUN both.** Expected: both FAIL. Record the RED.

- [ ] **Step 3: Implement**

Add the session cache (mirror `UnityGeneratedTextureEvidence.cs:24-28` minus the contents hash); truncate to the raw cap before storing; join both per-build clear blocks (memory hygiene and route parity; the update count in the key carries correctness). The gather-side `LimitedTo` composition is idempotent (verified in spec review); the consumer-boundary result stays identical.

- [ ] **Step 4: RUN `UnityAlphaFieldEvidenceTests`, `UnityMaterialEvidenceCaptureTests`, `UnityRendererAlphaAnalysisTests`, plus one fixture-driven classification case.** Expected: PASS with identical outcomes at the post-`LimitedTo` boundary.

- [ ] **Step 5: Commit** — `perf: cache GPU alpha fields and cap readback at the policy mip cap (finding 9)`

---

### Task 10: Finding 10 — lilToon include-tree attestation memo

**Files:**
- Modify: `Editor/Semantics/LilToon/LilToonSourceAttestation.cs:2476-2581` (memo around the gather; `LilToonIncludeTree` is nested in this file at `:22`, there is no separate file)
- Modify: the two per-build clear blocks (same blocks as task 9; anchor by symbol)
- Test: `Tests/Editor/Semantics/LilToon/LilToonAttestationTests.cs`

**Interfaces:**
- Produces: the memoized gather keeps the same injectable `readTextOrNull`/`hash` delegates `LilToonIncludeTree.Enumerate` already accepts, so the include-tree test suite can drive it.

- [ ] **Step 1: Write the failing test**

```csharp
[Test]
public void AGatheredShaderSourceIsMemoizedUntilTheBuildBoundary()
{
    var first = GatherSourceEvidence(fixtureShader);
    var second = GatherSourceEvidence(fixtureShader);
    Assert.That(second, Is.SameAs(first), "one gather serves repeated materials on one shader");
}
```

`GatherSourceEvidence` wraps the suite's existing synthetic-shader gather path. No read counter is built; the memo's single-instance identity is the observable.

- [ ] **Step 2: RUN it.** Expected: FAIL (every gather rebuilds the evidence). Record the RED.

- [ ] **Step 3: Implement**

Memo keyed on shader asset identity plus include-folder write times, mirroring the corrected Poiyomi shape (`PoiyomiMaterialSemantics.cs:2488-2514`) — never mtime-only on the shader file alone. Join both clear blocks. Hash inputs unchanged, so digests are unchanged.

- [ ] **Step 4: RUN `LilToonAttestationTests`, `LilToonIncludeTreeTests`, and the lilToon semantics classes from task 2.** Expected: PASS; identical digests.

- [ ] **Step 5: Commit** — `perf: memoize lilToon include-tree attestation per shader identity (finding 10)`

---

### Task 11: Finding 27 — the Poiyomi memo joins the per-build clear list

**Files:**
- Create: `Tests/Editor/Semantics/Poiyomi/PoiyomiAttestationTests.cs` (verified absent; stage the generated `.meta` with it)
- Modify: `Editor/Semantics/Poiyomi/PoiyomiMaterialSemantics.cs:2488-2514` (add `ClearSourceHashMemo`), the two per-build clear blocks (anchor by symbol)

- [ ] **Step 1: Write the failing test**

```csharp
[Test]
public void TheSourceHashMemoClearsAtTheBuildBoundary()
{
    var first = TryReadNormalizedSourceHashCached(fixturePath, out var seedDigest);
    ClearSourceHashMemo();
    var second = TryReadNormalizedSourceHashCached(fixturePath, out var reDigest);
    Assert.That(second, Is.True);
    // After a clear the memo must re-read; equality of digests on unchanged
    // files and the cleared memo's re-read are the observable contract.
    Assert.That(InternalMemoIsEmptyAfterClear(), Is.True, "the clear must drop the memo entry");
}
```

Expose the memo's cleared state through the new `ClearSourceHashMemo`'s return value (true when any entry was dropped) rather than a test-only reader.

- [ ] **Step 2: RUN it.** Expected: FAIL — no clear exists. Record the RED.

- [ ] **Step 3: Implement** the clear method and the two call-site appends next to the sibling cache clears.

- [ ] **Step 4: REFRESH (new file), RUN the new suite plus the Poiyomi semantics suites.** Expected: PASS.

- [ ] **Step 5: Commit** — `fix: clear the Poiyomi source digest memo at the build boundary (finding 27)` (stage the new test file and its `.meta`)

---

### Task 12: Finding 11 — point paths reuse the bilinear pre-filter and stop reallocating per texel

**Files:**
- Modify: `Editor/Analysis/TriangleAlphaClassifier.cs:588-604,1496-1513` (pre-filter before `Intersects`), `Editor/Analysis/ExactUvGeometry.cs:511-543` (reuse buffers)
- Test: `Tests/Editor/Analysis/TriangleAlphaClassifierTests.cs`, `Tests/Editor/Analysis/ExactUvGeometryTests.cs`

- [ ] **Step 1: Characterization run** — RUN `TriangleAlphaClassifierTests`, `ExactUvGeometryTests`, `AlphaSemanticsResolverTests`. Record counts.

- [ ] **Step 2: Implement**

Apply the double-precision SAT pre-filter the bilinear paths use (`:838-851`) before each exact intersection on both point paths. Reuse the hull and clip buffers in `Intersects` (clear and refill; no per-call allocation). Conservative direction: the pre-filter may only skip texels the exact walk would reject. No counting seam exists and none is built; the pre-filter's application is proven by source inspection and outcome identity.

- [ ] **Step 3: RUN the three suites from step 1.** Expected: PASS, identical counts.

- [ ] **Step 4: Commit** — `perf: pre-filter point-path windows and reuse exact-walk buffers (finding 11)`

---

### Task 13: Finding 12 — the density pre-pass merges into the main walk

**Files:**
- Modify: `Editor/Analysis/TriangleAlphaClassifier.cs:693-727,1044-1090` (merge), main walks at `:588-604,1496-1513`
- Test: `Tests/Editor/Analysis/TriangleAlphaClassifierTests.cs`

- [ ] **Step 1: Characterization run** — RUN `TriangleAlphaClassifierTests` including its density-policy cases. Record counts. This is the equivalence oracle.

- [ ] **Step 2: Implement**

Collect substitution candidates during the main walk; run the fallback re-walk only when a candidate could flip the verdict. A single pass must not change which texels count as witnesses when substitution flips mid-walk — the fallback covers exactly that case.

- [ ] **Step 3: RUN the suite from step 1.** Expected: PASS, identical counts (the existing density cases are the outcome oracle; no new benchmark or counter).

- [ ] **Step 4: Commit** — `perf: fold the density substitution pre-pass into the main window walk (finding 12)`

---

### Task 14: Finding 31 — the affine mapping decodes once per resolution

**Files:**
- Modify: `Editor/Analysis/AffineUvTransform.cs:35-49` (split decode from transform), `Editor/Analysis/AlphaSemanticsResolver.cs` (decode at resolution construction; anchor inside the region task 7 rewrote, by symbol not line)
- Test: `Tests/Editor/Analysis/AffineUvTransformTests.cs`, `Tests/Editor/Analysis/AlphaSemanticsResolverTests.cs`

- [ ] **Step 1: Implement**

Decode scale/offset once at resolution construction; `TryTransform` consumes the decoded record. `DecodeFloat` has no injection point and none is built; the once-per-resolution property is proven by source inspection (the decode moves out of the per-triangle call path), and outcomes are proven by the suites.

- [ ] **Step 2: RUN `AffineUvTransformTests` and `AlphaSemanticsResolverTests`.** Expected: PASS, identical counts.

- [ ] **Step 3: Commit** — `perf: decode the affine mapping once per resolution (finding 31)`

---

### Task 15: Finding 32 — the red-field assertion runs once per chain

**Files:**
- Modify: `Editor/Host/UnityMaterialEvidenceCapture.cs:1417,1452-1473` (move the assertion to chain production/cache fill)
- Test: `Tests/Editor/Host/UnityMaterialEvidenceCaptureTests.cs`

- [ ] **Step 1: Write the characterization test**

```csharp
[Test]
public void AViolatingRedChainStillThrows()
{
    Assert.Throws<InvalidOperationException>(() => CaptureViolatingRedChain()); // suite precedent :1219-1237
}
```

- [ ] **Step 2: RUN it.** Expected: PASS today — recorded as characterization; the throw-on-violation contract must survive the move. No count seam exists and none is built; the once-per-chain property is proven by source inspection (the assertion call moves from the per-renderer capture path to the dedup/cache-fill point), recorded in the task result.

- [ ] **Step 3: Implement** the move. Assertion semantics (throw on violation, same exception type) unchanged.

- [ ] **Step 4: RUN `UnityMaterialEvidenceCaptureTests`.** Expected: PASS, identical counts.

- [ ] **Step 5: Commit** — `perf: validate red chains once at cache fill, not per renderer (finding 32)`

---

### Task 16: Finding 33 — verify the sync API, then replace or annotate the spin

**Files:**
- Modify: `Editor/Semantics/PackageVersionReader.cs:19-63` (replacement or corrected comment)
- Test: `Tests/Editor/Semantics/PackageVersionReaderTests.cs` (exists; it stubs the `ListPackages` seam at `:15-19` — modify, not create)

- [ ] **Step 1: Verify** — reflect or read the Unity 2022.3 package manager API (`UnityEditor.PackageManager.Requests`) for a synchronous completion member. Record the finding in the task result. Use the MCP `unity_reflect` tool.

- [ ] **Step 2: Branch on the verification**

If a synchronous member exists: drive it through the existing `ListPackages` seam with a test asserting no sleep-loop use, run RED (today's loop is the only path), replace the loop, run green, commit `perf: read producer versions synchronously (finding 33)`.

If none exists: correct the comment at `:30-46` to state the real reason for the loop and its 30-second bound, RUN the version-reading suites, record the finding as partially remediated per spec decision 13, and commit `docs: correct the version-reader comment on the spin bound (finding 33)`.

- [ ] **Step 3: RUN any suite touching `AdmittedProducerVersions`.** Expected: PASS. The per-build clears stay (spec decision 13).

---

### Task 17: Finding 30 — the editor caches the parsed record, keeping the fingerprint check live

**Files:**
- Modify: `Editor/AmuseAvatarOptimizerEditor.cs:243-272` (extract the parse into an internal helper; cache `(rawRecordString, parsedRows)`; the live fingerprint comparison stays on every repaint and is never cached)
- Test: `Tests/Editor/AmuseAvatarOptimizerEditorTests.cs`

**Interfaces:**
- Produces: an internal parse helper with invalidation on raw-string change. The fingerprint must keep flipping the result to null on a platform switch even when the record string is unchanged (`ComputePolicyFingerprint` includes `activeTarget`, `PlayModeBuildStore.cs:57`), so only the `JsonUtility` parse is cached.

- [ ] **Step 1: Write the failing test**

```csharp
[Test]
public void TheParseCacheInvalidatesOnRecordChangeButNotOnFingerprintAlone()
{
    var rows1 = ParseCached(rawRecord);
    var rows2 = ParseCached(rawRecord);
    Assert.That(rows2, Is.SameAs(rows1), "the same record string reuses the parse");
    var rows3 = ParseCached(otherRecord);
    Assert.That(rows3, Is.Not.SameAs(rows1), "a changed record string reparses");
}
```

`ParseCached` is the extracted internal helper, called from the inspector path.

- [ ] **Step 2: RUN it.** Expected: FAIL (no helper/cache exists; each call reparses). Record the RED.

- [ ] **Step 3: Implement** the extraction and cache; keep the fingerprint comparison live in the inspector path.

- [ ] **Step 4: RUN `AmuseAvatarOptimizerEditorTests`.** Expected: PASS.

- [ ] **Step 5: Commit** — `perf: cache the parsed play record, keep the fingerprint check live (finding 30)`

---

### Task 18: Finding 7 — a closed refusal enum at the preparation boundary

**Files:**
- Modify: `Editor/Build/AmusePreparationDecision.cs:24-37` (enum + factory), `Editor/Build/AlphaSeparationApply.cs:190-192` (pass the enum), `Editor/Build/AlphaSeparationPreparation.cs` (call sites)
- Test: `Tests/Editor/Build/AlphaSeparationPreparationTests.cs`

**Interfaces:**
- Produces: `internal enum PreparationRefusal { None, StaleSamplingPolicy, OpaqueConversionRefused, ... }` with one member per existing string producer; `AmusePreparationDecision.Refused(PreparationRefusal reason, string detail = null)`; the existing outcome property stays `IsPrepared` (`AmusePreparationDecision.cs:22`).

- [ ] **Step 1: Enumerate producers** — grep the current string producers (`SamplingPolicyChanged.ToString()` at `AlphaSeparationApply.cs:191-192`, the concatenated report detail at `AlphaSeparationPreparation.cs:575-578`) and list the enum members they map to. Record the mapping in the task result.

- [ ] **Step 2: Write the failing test**

```csharp
[Test]
public void ThePreparationDecisionCarriesANamedRefusalNotAFreeString()
{
    var decision = AmusePreparationDecision.Refused(PreparationRefusal.StaleSamplingPolicy);
    Assert.That(decision.Refusal, Is.EqualTo(PreparationRefusal.StaleSamplingPolicy));
    Assert.That(AmusePreparationDecision.Refused(PreparationRefusal.None).IsPrepared, Is.True);
}
```

- [ ] **Step 3: RUN it.** Expected: FAIL (no enum exists). Record the RED.

- [ ] **Step 4: Implement**: add the enum, the factory, and switch the apply caller from `ToString()` to the enum value. Report-edge display text keeps the detail string parameter. No refusal outcome changes.

- [ ] **Step 5: RUN `AlphaSeparationPreparationTests` and `AlphaSeparationApplyTests`.** Expected: PASS, identical outcomes.

- [ ] **Step 6: Commit** — `fix: carry preparation refusals as a closed enum (finding 7)`

---

### Task 19: Finding 17 — apply reads the build target through the shared seam

**Files:**
- Modify: `Editor/Build/AlphaSeparationApply.cs:150`, `Editor/Build/AmusePlatformFinishPlugin.cs:293-306` (seam doc names both consumers)
- Test: `Tests/Editor/Build/AlphaSeparationApplyTests.cs`

- [ ] **Step 1: Write the failing test**

```csharp
[Test]
public void TheApplyStalePolicyGuardHonorsThePinnedBuildTarget()
{
    AmusePlatformFinishPlugin.ActiveBuildTargetForTests = TestTarget;
    // Drive apply's staleness guard with a captured target of TestTarget
    // while the editor's live target differs. Expected: no spurious refusal.
}
```

- [ ] **Step 2: RUN it.** Expected: FAIL today (apply reads the live editor target at `:150`). Record the RED.

- [ ] **Step 3: Implement**: `AlphaSeparationApply.cs:150` calls `AmusePlatformFinishPlugin.ActiveBuildTarget()`; update the seam doc comment to name both consumers.

- [ ] **Step 4: RUN `AlphaSeparationApplyTests`.** Expected: PASS.

- [ ] **Step 5: Commit** — `fix: apply's stale-policy guard reads the shared build-target seam (finding 17)`

---

### Task 20: Finding 18 — the cancellation source is disposed safely

**Files:**
- Modify: `Editor/DryRun/AlphaSeparationDryRun.cs:436-490` (side continuation + disposed guard on `Cancel`)
- Test: `Tests/Editor/DryRun/AlphaSeparationDryRunTests.cs`

**Interfaces:**
- Produces: the CTS is disposed exactly once, by a side continuation (`TaskContinuationOptions.ExecuteSynchronously`) attached where the in-flight task is created at `:674-676`. `Cancel()` guards its `_cts.Cancel()` against `ObjectDisposedException` (a disposed flag or a narrow catch), because the continuation can dispose between task completion and the next `Cancel()` during editor teardown. `_inFlightTask` stays a raw field — no wrapper type.

- [ ] **Step 1: Write the failing test**

```csharp
[Test]
public void CancellingWhileInFlightDisposesTheSourceWhenTheTaskCompletes()
{
    StartClassification();      // long-running in-flight task through the public entry
    CancelWithZeroTimeout();    // abandons the task, per today's editor path
    CompleteInFlightTask();     // let the continuation run
    Assert.That(TokenIsDisposed(), Is.True, "the continuation owns disposal");
    Assert.DoesNotThrow(() => CancelAgain(), "a later Cancel must not throw on the disposed source");
}
```

`TokenIsDisposed()` reflects the private `_cts` field and probes `cts.Token` — it throws `ObjectDisposedException` after dispose. Deterministic, zero production plumbing.

- [ ] **Step 2: RUN it.** Expected: FAIL (the source is abandoned undisposed today). Record the RED.

- [ ] **Step 3: Implement** the continuation and the `Cancel()` guard. The completed-path dispose in `ClearInFlight` may stay (`Dispose` is idempotent).

- [ ] **Step 4: RUN `AlphaSeparationDryRunTests`.** Expected: PASS.

- [ ] **Step 5: Commit** — `fix: dispose the dry-run cancellation source safely after in-flight completion (finding 18)`

---

### Task 21: Finding 19 — the unreachable fallback throws

**Files:**
- Modify: `Editor/Build/AlphaSeparationApply.cs:1073-1076`
- Test: `Tests/Editor/Build/AlphaSeparationApplyTests.cs`

- [ ] **Step 1: Write the failing test**

```csharp
[Test]
public void AMissingSurvivingCandidateListIsAnInvariantFailureNotASilentApply()
{
    var state = NewStateWithoutSurvivingCandidates(); // state fixture with the list null
    Assert.Throws<InvalidOperationException>(() => CollectAppliedSlotsForTest(state));
}
```

Adapt to the real method shape; `InvalidOperationException` is the production throw this task adds.

- [ ] **Step 2: RUN it.** Expected: FAIL (the fallback returns prepared candidates today). Record the RED.

- [ ] **Step 3: Implement**: replace the fallback with a throw carrying a named message; no current path reaches it (verified in the investigation).

- [ ] **Step 4: RUN `AlphaSeparationApplyTests`.** Expected: PASS.

- [ ] **Step 5: Commit** — `fix: missing surviving-candidate list throws instead of applying refused slots (finding 19)`

---

### Task 22: Finding 20 — the source-image route admits PNG only

**Files:**
- Modify: `Editor/Host/SourceImageAlphaReader.cs:52-56` (admission list + comment)
- Test: `Tests/Editor/Host/SourceImageAlphaReaderTests.cs`

- [ ] **Step 1: Verify routing first** — read `UnityStreamingTextureEvidence.cs:113-118` and confirm an unadmitted extension falls to the readable-clone route (behavior as observed on 2026-10-15). Record the trace in the task result. If the routing differs, stop and report per the spec.

- [ ] **Step 2: Write the failing test**

```csharp
[Test]
public void TgaSourcesAreNotAdmittedByTheSourceImageRoute()
{
    var admitted = TryAdmit(".tga", out _);   // the inline admission at :52-56, extracted or mirrored
    Assert.That(admitted, Is.False, "LoadImage cannot decode TGA; the route must not advertise it");
}
```

If admission stays inline, test through the public capture entry with a TGA fixture and assert the clone-route fallback.

- [ ] **Step 3: RUN it.** Expected: FAIL. Record the RED.

- [ ] **Step 4: Implement**: remove `.tga` from the admitted list; comment states `LoadImage` decodes PNG and JPG only and TGA rides the clone route.

- [ ] **Step 5: RUN `SourceImageAlphaReaderTests`.** Expected: PASS.

- [ ] **Step 6: Commit** — `fix: stop advertising TGA on the source-image alpha route (finding 20)`

---

### Task 23: Finding 21 — a named refusal for a null layer state machine

**Files:**
- Modify: `Editor/Host/CommittedControllerGraph.cs:11,57-66,117-127,189-236` (new enum member, null checks at the two dereference sites)
- Modify: `Editor/Build/AmuseReportStrings.cs` (three table entries)
- Test: `Tests/Editor/Host/CommittedControllerGraphTests.cs`

**Interfaces:**
- Produces: new `AvatarAnimationRefusal` member `MalformedControllerLayer`. Report keys are `amuse.avatar.MalformedControllerLayer`, `amuse.avatar.MalformedControllerLayer:description`, `amuse.avatar.MalformedControllerLayer:hint` (`AvatarKey` builds `Prefix + "avatar." + cause`, `AmuseReportStrings.cs:17,743-746`). All three entries are mandatory in-task: the dry-run arm throws on a missing key (`AlphaSeparationDryRun.cs:277-280`) and the barrier arm falls back to a generic sentence (`AmusePlatformFinishPlugin.cs:542-545`).

- [ ] **Step 1: Write the failing test**

```csharp
[Test]
public void AControllerLayerWithoutAStateMachineRefusesByName()
{
    var controller = NewControllerWithNullStateMachineLayer();
    var result = CommittedControllerGraph.Enumerate(controller);
    Assert.That(result.Refusal, Is.EqualTo(AvatarAnimationRefusal.MalformedControllerLayer));
    Assert.That(AmuseReportStrings.Has(AmuseReportStrings.AvatarKey(AvatarAnimationRefusal.MalformedControllerLayer)), Is.True,
        "the named refusal must have its report table entry");
}
```

- [ ] **Step 2: RUN it.** Expected: FAIL with `NullReferenceException`. Record the RED.

- [ ] **Step 3: Implement**: add the enum member; null-check at `EnumerateLayer` (`:199-206`) and the child recursion (`:231-236`), returning the named refusal; add the three table entries.

- [ ] **Step 4: RUN `CommittedControllerGraphTests` and the report-strings suite.** Expected: PASS.

- [ ] **Step 5: Commit** — `fix: refuse malformed controller layers by name instead of crashing (finding 21)`

---

### Task 24: Finding 23 — one meaning for `verifyIdentity`

**Files:**
- Modify: `Editor/Semantics/UnityMaterialSemantics.cs:887-929`
- Test: `Tests/Editor/Semantics/UnityMaterialSemanticsTests.cs`

**Interfaces:**
- Consumes: the transferred-analysis public entry the suite already drives with this knob (`TransferredAnalysis_PoiyomiFamily_HonorsVerifyIdentityParameter`, `UnityMaterialSemanticsTests.cs:1275`). `IsAttestedAlphaMaterial` is private and stays private.

- [ ] **Step 1: Write the failing test**

```csharp
[Test]
public void TransferredAnalysisSkipsVerificationForBothFamiliesWhenAskedit()
{
    var poiyomi = NewUnattestedPoiyomiFamilyMaterial();   // suite's existing fixture pattern
    var lilToon = NewUnattestedLilToonFamilyMaterial();
    Assert.That(AnalyzeTransferred(poiyomi, verifyIdentity: false).IsUnattestedShader, Is.False,
        "false means skip verification, not never attested");
    Assert.That(AnalyzeTransferred(lilToon, verifyIdentity: false).IsUnattestedShader, Is.False);
}
```

Adapt to the real entry the `:1275` test calls. The RED needs no attested fixture: the Poiyomi arms (`:903-905,909-911`) answer false/UnattestedShader for `verifyIdentity: false` today.

- [ ] **Step 2: RUN it.** Expected: FAIL on the Poiyomi arm. Record the RED.

- [ ] **Step 3: Implement**: every arm becomes `!verifyIdentity || TryVerify...(material)`.

- [ ] **Step 4: RUN `UnityMaterialSemanticsTests` and both frontend suites.** Expected: PASS; both live callers pass `true`, so outcomes are unchanged.

- [ ] **Step 5: Commit** — `fix: make verifyIdentity mean skip-verification in every frontend arm (finding 23)`

---

### Task 25: Finding 25 — verify a typed NDMF activity signal, then replace or document

**Files:**
- Modify: `Editor/Build/AmusePlatformFinishPlugin.cs:404-421` (replacement or site comment)
- Test: the suite file holding the animation-index tests (locate by searching Tests for `TryGetAnimationIndex` usage)

- [ ] **Step 1: Verify** — read `Packages/nadena.dev.ndmf/` source for a typed signal distinguishing "extension not active" from other failures (an `IsActive`-style check, a specific exception type, or an optional accessor on `BuildContext`/`AnimatorServicesContext`). Record the finding in the task result.

- [ ] **Step 2: Branch on the verification**

If a typed signal exists: write a test that the barrier picks the extension-free arm without relying on the message text, run RED, replace the catch, run green, commit `fix: detect animator-services inactivity through the typed NDMF signal (finding 25)`.

If none exists: keep the catch, add a site comment stating the exact-match coupling to the NDMF diagnostic string and the two drift directions, record the finding as partially remediated per spec decision 12, and commit `docs: document the extension-detection message coupling (finding 25)`.

- [ ] **Step 3: RUN the animation-evidence suites.** Expected: PASS.

---

### Task 26: Finding 15 — play-mode rows describe the folded plan

**Files:**
- Modify: `Editor/Build/AmusePlatformFinishPlugin.cs:993` (`RetainPreparedSeparation` returns the `PreparedRendererSeparation` or null), `:1011-1013` (record `prepared.Plan`)
- Test: `Tests/Editor/Build/AmusePlatformFinishPluginTests.cs`

**Interfaces:**
- Consumes: `PreparedRendererSeparation.Plan` (`AlphaSeparationRecords.cs:250-293`), which `Prepare` already fills with the folded plan (`AlphaSeparationPreparation.cs:154-168`, `:686-689`). No new field, no signature change beyond the private retain helper's return.
- Note: when the prepared result is null (nothing survived preparation), the recorder records no slot rows for that renderer; the existing refusal path already covers refused renderers.

- [ ] **Step 1: Write the failing test**

```csharp
[Test]
public void AFoldedSlotRecordsTheConvertedOutcomeNotTheUnfoldedOne()
{
    var rows = ExecutePlayModeBuildWithPreserveGateFoldingOneSlot();
    var foldedRow = RowFor(rows, foldedSlot);
    Assert.That(foldedRow.Sentence, Is.Not.Contains("Splittable:"),
        "the folded slot must not read as an unconverted splittable slot");
}
```

- [ ] **Step 2: RUN it.** Expected: FAIL (rows come from the caller's unfolded plan at `:1011-1013`). Record the RED.

- [ ] **Step 3: Implement**: `RetainPreparedSeparation` returns the prepared result; the recorder consumes `prepared.Plan` and records nothing when null.

- [ ] **Step 4: RUN `AmusePlatformFinishPluginTests` plus `MaterialTransformationRowTests`.** Expected: PASS; moved pins listed in the task result.

- [ ] **Step 5: Commit** — `fix: play-mode rows record the folded plan apply executes (finding 15)`

---

### Task 27: Finding 16 — moved-triangle counts count changed slots

**Files:**
- Modify: `Editor/Build/AlphaSeparationApply.cs:1050-1110,1152`
- Test: `Tests/Editor/Build/AlphaSeparationApplyTests.cs`

- [ ] **Step 1: Write the failing test**

```csharp
[Test]
public void ASelfMappedSurvivingSlotReportsZeroMovedTriangles()
{
    // Renderer with two slots: one converts, one is already-opaque and
    // maps to itself. The record must report movement only for the
    // converted slot.
    Assert.That(RowFor(SelfMappedSlot).MovedTriangles, Is.EqualTo(0));
    Assert.That(RowFor(ConvertedSlot).MovedTriangles, Is.EqualTo(ProvenCount));
}
```

- [ ] **Step 2: RUN it.** Expected: FAIL (any renderer write marks all surviving slots applied at `:1050-1094`). Record the RED.

- [ ] **Step 3: Implement**: key `movedTriangles = provenTriangles` on the slot's material actually changing (apply already computes changed slots for `CountOpaqueTriangles`); mirror in the rebuild path at `:1152`.

- [ ] **Step 4: RUN `AlphaSeparationApplyTests`.** Expected: PASS; moved pins listed in the task result.

- [ ] **Step 5: Commit** — `fix: moved-triangle counts count only slots whose material changed (finding 16)`

---

### Task 28: Finding 26 — per-cause refusal rows; duplicated builders die

**Files:**
- Modify: `Editor/Build/AmusePlatformFinishPlugin.cs:1490-1504` (keyed sentence), `Editor/Build/AlphaSeparationApply.cs:1175,1179` (builders die, call sites switch to `RowText`)
- Test: `Tests/Editor/Build/AlphaSeparationApplyTests.cs`, `Tests/Editor/DryRun/MaterialTransformationRowTests.cs`

- [ ] **Step 1: Write the failing test**

```csharp
[Test]
public void ARefusedSlotRowCarriesTheNamedCause()
{
    var rows = RecordRefusedRenderer(RendererAnalysisRefusal.MissingMesh, slotCount: 2);
    Assert.That(rows.All(r => r.Sentence.Contains("mesh")),
        "the row names the cause; it is not the generic unsupported-material sentence");
}
```

Adapt the refusal value and expected substring to the shared table's real sentence for that cause.

- [ ] **Step 2: RUN it.** Expected: FAIL (fixed generic sentence at `:1490-1504`). Record the RED.

- [ ] **Step 3: Implement**: slot rows read the per-cause renderer table entries via the existing sentence builders; the generic sentence remains only behind the corrupt-key guard. Delete the duplicated apply-side builders; their call sites use the shared builders. The apply-side "of out" copy dies with them (task 29 fixes the surviving shared builders).

- [ ] **Step 4: RUN the two suites.** Expected: PASS; moved pins listed in the task result.

- [ ] **Step 5: Commit** — `fix: play-mode refusal rows name the cause through the shared table (finding 26)`

---

### Task 29: Finding 35 — the visible copy becomes "out of"

**Files:**
- Modify: `Editor/DryRun/MaterialTransformationRow.cs:189,196,201`
- Test: `Tests/Editor/DryRun/MaterialTransformationRowTests.cs:287-342` (pins move)

- [ ] **Step 1: Update the pins first** (RED by pin): change the five "of out" pins to "out of". RUN the suite. Expected: FAIL — production still emits the old text. Record the RED and note in the task result that the pins were corrected deliberately (the pins were written from the code).

- [ ] **Step 2: Implement**: fix the three builder strings at `:189`, `:196`, `:201`.

- [ ] **Step 3: RUN the full `MaterialTransformationRowTests`.** Expected: PASS.

- [ ] **Step 4: Commit** — `fix: correct the row copy to "out of" and its pins (finding 35)`

---

### Task 30: Finding 6 — the capture contract names the sanctioned route; stale clones get swept

**Files:**
- Modify: `Editor/Host/UnityAlphaFieldEvidence.cs:21-22` (contract text), `Editor/Host/UnityStreamingTextureEvidence.cs:141-166` (sweep before create)
- Test: `Tests/Editor/Host/UnityStreamingTextureEvidenceTests.cs`

- [ ] **Step 1: Write the failing test**

```csharp
[Test]
public void AStaleCloneFromACrashedRunIsDeletedBeforeANewCloneIsMade()
{
    CreateStaleClone(folder, CloneName("stale"));   // matches the route's own naming pattern
    CreateForeignAsset(folder, "unrelated.png");    // must survive
    CaptureStreamingChainThroughThePublicEntry();
    Assert.That(AssetDatabase.LoadAssetAtPath(ClonePath("stale")), Is.Null);
    Assert.That(AssetDatabase.LoadAssetAtPath(ForeignPath), Is.Not.Null);
}
```

Adapt the clone-name helper to the route's real naming pattern; the foreign-asset guard proves the sweep never touches content the route does not own.

- [ ] **Step 2: RUN it.** Expected: FAIL (no sweep exists). Record the RED.

- [ ] **Step 3: Implement**: sweep files matching the route's own clone naming pattern in the pinned folder before creating a clone; never touch anything else. Amend the contract text at `:21-22` to name the streaming-clone route as the sanctioned exception, in the style of the Semantics recipes note.

- [ ] **Step 4: RUN `UnityStreamingTextureEvidenceTests` and `UnityAlphaFieldEvidenceTests`.** Expected: PASS.

- [ ] **Step 5: Commit** — `fix: amend the capture contract for the streaming route and sweep stale clones (finding 6)`

---

### Task 31: Findings 13, 34 — delete tests without a behavior sentence

**Files:**
- Modify: `Tests/Editor/AmuseAvatarOptimizerEditorTests.cs:256-281` (two tests and their orphaned region die)
- Delete: the two tests' `.meta` needs no action (same file), but record the count delta

- [ ] **Step 1: Confirm the duplicates** — the deferred sentence is pinned in `Tests/Editor/DryRun/MaterialTransformationRowTests.cs:337-345`, and the real draw path is exercised in the same editor file's host-window tests. Record the confirmation.

- [ ] **Step 2: Delete** `DrawTransformationRowRendersDeferredSlotRow` and `DrawTransformationRowAcceptsAvatarRoot`.

- [ ] **Step 3: REFRESH, RUN `AmuseAvatarOptimizerEditorTests`.** Expected: PASS with two fewer tests; record the count delta.

- [ ] **Step 4: Commit** — `test: delete the fixture-echo and signature-only editor tests (findings 13, 34)`

---

### Task 32: Final validation and acceptance

**Files:** none created; validation only.

- [ ] **Step 1: REFRESH, then RUN the full `Alrauna.Amuse.Tests.Editor` assembly.** Record observed counts. Expected: 0 failures; the only expected count delta is task 31's two deletions plus any deliberately moved pins.

- [ ] **Step 2: Acceptance greps and record checks** — record outputs:
  1. No call-position Host references under `Editor/Analysis/` (task 6's bare-name pattern list, with hits classified).
  2. Attestation safety: `git diff f929332..HEAD -- Packages/com.alrauna.amuse/Editor/Semantics` plus the touched Host and Build files shows moved lines and no changed pinned constants (sweep every changed file's diff for pinned-value edits — a `*Attest*` pathspec glob misses the nested attestation files).
  3. `git status --porcelain` shows nothing untracked beyond user-owned state.
  4. The round's record holds: dated decisions for findings 2, 4, 24, 29, and the dry-run half of 6 (spec acceptance 2), and RED-before-green entries for findings 1, 2, 3, 14 (spec acceptance 3), read from the task results, not assumed.

- [ ] **Step 3: Identifier sweep** on every file the round changed; fix any hit before the final report.

- [ ] **Step 4: Report** — executed findings vs the spec tally, observed counts, dropped or partially remediated findings with their recorded reason, moved-pin list, and the one spec deviation recorded in decision 14's amendment.
