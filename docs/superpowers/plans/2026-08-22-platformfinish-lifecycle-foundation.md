# PlatformFinish Lifecycle Foundation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Establish the smallest authoritative NDMF PlatformFinish foundation of AMUSE. The foundation covers exact fail-closed lifecycle capability and eager immutable host extraction for the current alpha proof path. It adds one bounded prepare/apply operation and an explicit first-mutation failure boundary. Generated assets stay NDMF-owned. The foundation does not implement alpha mutation or DAO cooperation.

**Architecture:** Export one AMUSE NDMF plugin whose sole pass runs in `BuildPhase.PlatformFinish`. Phase authority, rather than Meshia/AAO identity edges, places every normal `Optimizing` producer before the semantic barrier. The pass captures lifecycle facts and runs a dry analysis through eagerly captured immutable renderer values and per-material requested evidence. It retains `BuildContext` only inside the bounded operation. Later concrete mutation can then prepare through `AssetSaver` and cross one explicit apply boundary. Lifecycle and semantic refusal plus explicit unsupported preparation decisions preserve input. Unexpected preparation or apply exceptions propagate to NDMF as build-blocking internal failures.

**Tech Stack:** Unity 2022.3.22f1, C# Editor-only code, NDMF 1.14.4 public plugin/`BuildContext`/`IAssetSaver` APIs, Unity Package Manager `PackageInfo`, NUnit EditMode tests. Existing assemblies: `Alrauna.Amuse.Editor` and `Alrauna.Amuse.Tests.Editor`. No new dependency, assembly, runtime component, SDK assembly reference, reflection dependency, or persistent asset system.

**Spec:** `docs/superpowers/specs/2026-08-22-coexisting-optimizer-lifecycle-design.md`

## Global Constraints

- The repository policy in `.omp/AGENTS.md` and the normative spec apply to every task.
- The supported foundation contract is exact: Unity `2022.3.22f1`, NDMF `1.14.4`, VRChat SDK Base `3.10.4`, VRChat SDK Avatars `3.10.4`, and NDMF platform `nadena.dev.ndmf.vrchat.avatar3`.
- `Application.unityVersion`, `PackageInfo.FindForPackageName`, `BuildContext.PlatformProvider`, `EditorApplication.isPlayingOrWillChangePlaymode`, and public `BuildContext` services are the only lifecycle inputs. Do not infer upload-attempt identity or inspect call stacks/callback inventories.
- NDMF 1.14.4 exposes no public token that separates SDK preprocessing from manual non-Play processing. The supported observable bucket is therefore named `NonPlayNdmfBuild`. It authorizes only this bounded NDMF operation and does not claim any upload attempt. Exact SDK `3.10.4` remains required for the normal-host build-abort contract.
- Positive mutation is unavailable during Apply-on-Play, for missing/unknown versions, and for a non-VRChat platform. It is also unavailable when `AssetSaver`, its current container, `ObjectRegistry`, or `ErrorReport` is unavailable.
- The plugin qualified name is `com.alrauna.amuse`; its one semantic-barrier pass is in `BuildPhase.PlatformFinish`.
- Add no Meshia, AAO, Modular Avatar, VRCFury, TexTransTool, or other producer identity edge. Normal `Optimizing` work is before AMUSE by phase authority.
- Add no edge to the NDMF `Generate portable components` or `CheckMipStreamingPass`. Their characterized behavior is irrelevant to the current alpha theorem.
- Do not rely on type-name order, assembly discovery order, equal callback order, `AfterPass(string)`, tail inspection, or reflection over NDMF internals.
- Keep one bounded operation. Do not carry proof/authorization to another callback and do not add post-mutation validation.
- Live Unity objects are extraction sources or immediate apply targets only. Semantics, proof, planning, and preparation consume AMUSE-owned immutable values.
- Each material carries its own exact primitive evidence request within a batch. Host capture never applies the union of unrelated shader-family requests or enumerates every shader property. All inputs in the bounded batch share one texture cache deduplicated by stable `TextureSourceId`.
- The reusable material layer contains no shader-family semantics, universal shader IR, arbitrary property DSL, or reflection schema. It also contains no provider interface or plugin registry. A second concrete request exercises reuse. It does not create a second production consumer.
- Keep `MaterialSemantics`, `AlphaSemanticsResolver`, `TriangleAlphaClassifier`, `ExactUvGeometry`, and `MeshSeparationPlanner` behaviorally unchanged.
- Do not implement opaque targets, alpha application, DAO types/bridge/Candidates/profiles, reachability/deformation expansion, rollback, transactions, or Apply-on-Play mutation.
- Only `AmusePreparationDecision.Refused(reason)` represents expected unsupported preparation. An exception from preparation or apply is not expected. It must escape the operation so NDMF records a build-blocking internal failure.
- Generated assets use only the current `BuildContext.AssetSaver`. Add no production `AssetDatabase.CreateAsset` call or custom temporary directory.
- Retain every Unity-generated `.meta` sidecar and inspect GUIDs.
- Every reported Unity result requires read-only instance discovery and exact normalized, case-sensitive `Application.dataPath == <repo-root>/Assets`. Never use the Census Lab.
- Inspect the complete `Packages/manifest.json` and `Packages/packages-lock.json` diff before any restore. Restore only prohibited host-only churn under `.omp/AGENTS.md` §Unity package and MCP safety.
- No package manifest, asmdef, research package, CI workflow, project setting, or release file should change. Existing CI is release/listing-only.
- Do not commit, push, open a PR, or execute another task without the authorization required by the implementation session.

---

## File structure map

| File | Change | Responsibility |
| --- | --- | --- |
| `Packages/com.alrauna.amuse/Editor/Build/HostLifecycleCapability.cs` | Create | Immutable lifecycle facts and fail-closed capability. |
| `Packages/com.alrauna.amuse/Editor/Build/AmusePlatformFinishPlugin.cs` | Create | NDMF export, PlatformFinish barrier, build-local diagnostic summary. |
| `Packages/com.alrauna.amuse/Editor/Build/AmuseBuildOperation.cs` | Create | Prepare/apply orchestration and first-mutation boundary. |
| `Packages/com.alrauna.amuse/Editor/Host/UnityMaterialEvidenceCapture.cs` | Create | Typed evidence requests, immutable primitive material/texture evidence, and per-operation texture deduplication. |
| `Packages/com.alrauna.amuse/Editor/Host/UnityRendererAlphaSnapshot.cs` | Create | Eager renderer/mesh/material capture plus separate immediate target. |
| `Packages/com.alrauna.amuse/Editor/Host/UnityAlphaFieldEvidence.cs` | Modify | Capture alpha bytes eagerly; retain no `Texture2D`. |
| `Packages/com.alrauna.amuse/Editor/Host/UnityRendererAlphaAnalysis.cs` | Modify | Analyze immutable snapshots; preserve capture-then-analyze facade. |
| `Packages/com.alrauna.amuse/Editor/Semantics/UnityMaterialSemantics.cs` | Modify | Batch the narrow alpha request and dispatch the captured alpha projection. |
| `Packages/com.alrauna.amuse/Editor/Semantics/Poiyomi/PoiyomiMaterialSemantics.cs` | Modify | Declare exact alpha/attestation evidence and expose the unchanged alpha equation over captured facts. |
| `Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonMaterialSemantics.cs` | Modify | Declare exact alpha/attestation evidence and expose the unchanged alpha equation over captured facts. |
| `Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonSourceAttestation.cs` | Modify | Capture material-dependent evidence before verification. |
| `Packages/com.alrauna.amuse/Tests/Editor/Build/HostLifecycleCapabilityTests.cs` | Create | Pure lifecycle matrix. |
| `Packages/com.alrauna.amuse/Tests/Editor/Build/AmusePlatformFinishPluginTests.cs` | Create | Synthetic NDMF phase/order and state visibility. |
| `Packages/com.alrauna.amuse/Tests/Editor/Build/AmuseBuildOperationTests.cs` | Create | Explicit preparation refusal, unexpected preparation failure, fatal apply, and asset ownership. |
| `Packages/com.alrauna.amuse/Tests/Editor/Host/UnityMaterialEvidenceCaptureTests.cs` | Create | Exact requests, irrelevant-property exclusion, reuse, texture deduplication, and immutability. |
| `Packages/com.alrauna.amuse/Tests/Editor/Host/UnityRendererAlphaSnapshotTests.cs` | Create | Renderer/mesh mutation after capture cannot change proof/plan. |
| `Packages/com.alrauna.amuse/Tests/Editor/Host/UnityAlphaFieldEvidenceTests.cs` | Modify | Prove eager alpha-byte capture. |
| `Packages/com.alrauna.amuse/Tests/Editor/Host/UnityRendererAlphaAnalysisTests.cs` | Modify | Preserve facade/refusal behavior after extraction split. |
| `Packages/com.alrauna.amuse/Tests/Editor/Semantics/UnityMaterialSemanticsTests.cs` | Modify | Prove live wrapper equals capture-then-interpret. |
| New directory/file `.meta` sidecars | Create through Unity import | Preserve Unity asset integrity. |

No research, package, asmdef, CI, project-setting, or release file changes.

## Test execution contract

Before each Unity run, discover instances read-only and select only the exact normalized public `<repo-root>/Assets` instance.

Focused command (replace the example class with the exact class list printed by the applicable step):

```text
run_tests:
  mode: EditMode
  test_names:
    - Alrauna.Amuse.Tests.Editor.Build.HostLifecycleCapabilityTests
  include_failed_tests: true
get_test_job:
  wait_timeout: 60
  include_failed_tests: true
```

Complete command:

```text
run_tests:
  mode: EditMode
  include_failed_tests: true
get_test_job:
  wait_timeout: 60
  include_failed_tests: true
```

Record total, passed, failed, skipped, duration, and relevant Console errors. Green means zero failed, zero unexpected skipped, and no new compiler/unexpected Console error. If the public instance is unavailable, stop and report validation blocked.

---

## Extraction migration classification

| Class | Current code | Decision for this branch |
| --- | --- | --- |
| A — already compatible | `MaterialSemantics`, `AlphaSemanticsResolver`, `TriangleAlphaClassifier`, `ExactUvGeometry`, immutable analysis results, and `MeshSeparationPlanner` | Keep behavior and interfaces unchanged except for feeding them captured host values. |
| B — bounded migration required | `UnityAlphaFieldEvidence` retains `Texture2D`; verified material interpretation rereads `Material`; `UnityRendererAlphaAnalysis` interleaves renderer/mesh/material reads with reasoning | Migrate in Tasks 3–5 to eager alpha bytes, exact requested material/source evidence, renderer/mesh snapshots, and a separate immediate mutation target. |
| C — purpose-specific and unchanged | Poiyomi/lilToon semantic equations, source-attestation rules, alpha resolver/classifier rules, and separation planning | Preserve the algorithms and diagnostics; only replace their live-material access with requested captured evidence. |
| D — future extraction pressure | Reachable swaps/animation, blend-shape/deformation state, multiple-renderer/global planning, generated target textures/materials, atlas/UV rewrite inputs, DAO preservation facts | Record as deferred. Later consumers may request additional primitive evidence through the same small mechanism, but this branch adds no speculative request or semantic abstraction for them. |

Only B is migration scope. This branch does not create a general avatar IR.

---

### Task 1: Exact host and lifecycle capability

**Files:**

- Create: `Packages/com.alrauna.amuse/Editor/Build.meta`
- Create: `Packages/com.alrauna.amuse/Editor/Build/HostLifecycleCapability.cs`
- Create: `Packages/com.alrauna.amuse/Editor/Build/HostLifecycleCapability.cs.meta`
- Create: `Packages/com.alrauna.amuse/Tests/Editor/Build.meta`
- Create: `Packages/com.alrauna.amuse/Tests/Editor/Build/HostLifecycleCapabilityTests.cs`
- Create: `Packages/com.alrauna.amuse/Tests/Editor/Build/HostLifecycleCapabilityTests.cs.meta`

**Interfaces:**

- Consumes: public Unity/Package Manager/NDMF facts listed in Global Constraints.
- Produces:

```csharp
internal enum AmuseBuildPath { NonPlayNdmfBuild, ApplyOnPlay, Unknown }
internal enum HostLifecycleRefusal
{
    None,
    UnsupportedUnityVersion,
    UnsupportedNdmfVersion,
    UnsupportedVrchatSdkBaseVersion,
    UnsupportedVrchatSdkAvatarsVersion,
    UnsupportedPlatform,
    UnsupportedBuildPath,
    MissingBuildContextServices,
}

internal sealed class HostLifecycleFacts
{
    internal string UnityVersion { get; }
    internal string NdmfVersion { get; }
    internal string VrchatSdkBaseVersion { get; }
    internal string VrchatSdkAvatarsVersion { get; }
    internal string PlatformQualifiedName { get; }
    internal AmuseBuildPath BuildPath { get; }
    internal bool HasAssetSaver { get; }
    internal bool HasAssetContainer { get; }
    internal bool HasObjectRegistry { get; }
    internal bool HasErrorReport { get; }
    internal HostLifecycleFacts(
        string unityVersion,
        string ndmfVersion,
        string vrchatSdkBaseVersion,
        string vrchatSdkAvatarsVersion,
        string platformQualifiedName,
        AmuseBuildPath buildPath,
        bool hasAssetSaver,
        bool hasAssetContainer,
        bool hasObjectRegistry,
        bool hasErrorReport);
}

internal sealed class HostLifecycleCapability
{
    internal bool MayUsePositiveMutation { get; }
    internal HostLifecycleRefusal Refusal { get; }
    internal string SupportedAssumption { get; }
    internal static HostLifecycleCapability Evaluate(HostLifecycleFacts facts);
    internal static HostLifecycleCapability CaptureAndEvaluate(BuildContext context);
}
```

- Package lookup names are exactly `nadena.dev.ndmf`, `com.vrchat.base`, and `com.vrchat.avatars`.
- Semantic uncertainty does not appear in these types.

- [ ] **Step 1: Write the failing pure capability matrix**

Add the positive case and one case per refusal:

```csharp
[Test]
public void ExactNonPlayNdmfContractPermitsPositiveMutation()
{
    var result = HostLifecycleCapability.Evaluate(SupportedFacts());
    Assert.That(result.MayUsePositiveMutation, Is.True);
    Assert.That(result.Refusal, Is.EqualTo(HostLifecycleRefusal.None));
    StringAssert.Contains("Unity 2022.3.22f1", result.SupportedAssumption);
}

[Test]
public void ApplyOnPlayRefusesWithLifecycleReason()
{
    var result = HostLifecycleCapability.Evaluate(
        SupportedFacts(buildPath: AmuseBuildPath.ApplyOnPlay));
    Assert.That(result.MayUsePositiveMutation, Is.False);
    Assert.That(result.Refusal, Is.EqualTo(HostLifecycleRefusal.UnsupportedBuildPath));
}
```

Vary one fact at a time: Unity `2022.3.22f2`, NDMF `1.14.5`, each SDK `3.10.5`, generic platform, unknown build path, and each missing service Boolean.

- [ ] **Step 2: Run the focused class**

Run `Alrauna.Amuse.Tests.Editor.Build.HostLifecycleCapabilityTests`.

Expected red: `CS0246` for missing capability types.

- [ ] **Step 3: Implement exhaustive exact evaluation**

Use ordinal equality and enum order above. Capture Apply-on-Play exactly like pinned NDMF:

```csharp
var path = EditorApplication.isPlayingOrWillChangePlaymode
    ? AmuseBuildPath.ApplyOnPlay
    : AmuseBuildPath.NonPlayNdmfBuild;
```

Package lookup returns null when missing or when `PackageInfo` throws `ArgumentException`. `HasAssetContainer` is `context.AssetSaver?.CurrentContainer != null`. Do not inspect paths or concrete saver types.

- [ ] **Step 4: Refresh/import and retain four new `.meta` files**

Expected: only the two directory and two file sidecars appear. No package diff.

- [ ] **Step 5: Re-run the focused class**

Expected green: the exact tuple permits. Every mismatch refuses with its exact reason.

- [ ] **Step 6: Commit**

```bash
git add Packages/com.alrauna.amuse/Editor/Build.meta Packages/com.alrauna.amuse/Editor/Build/HostLifecycleCapability.cs Packages/com.alrauna.amuse/Editor/Build/HostLifecycleCapability.cs.meta Packages/com.alrauna.amuse/Tests/Editor/Build.meta Packages/com.alrauna.amuse/Tests/Editor/Build/HostLifecycleCapabilityTests.cs Packages/com.alrauna.amuse/Tests/Editor/Build/HostLifecycleCapabilityTests.cs.meta
git commit -m "feat: define PlatformFinish lifecycle capability"
```

---

### Task 2: Production PlatformFinish plugin and phase authority

**Files:**

- Create: `Packages/com.alrauna.amuse/Editor/Build/AmusePlatformFinishPlugin.cs`
- Create: `Packages/com.alrauna.amuse/Editor/Build/AmusePlatformFinishPlugin.cs.meta`
- Create: `Packages/com.alrauna.amuse/Tests/Editor/Build/AmusePlatformFinishPluginTests.cs`
- Create: `Packages/com.alrauna.amuse/Tests/Editor/Build/AmusePlatformFinishPluginTests.cs.meta`

**Interfaces:**

- Consumes: Task 1 capability and public NDMF plugin APIs.
- Produces:

```csharp
internal sealed class AmusePlatformFinishState
{
    internal bool HasExecuted { get; set; }
    internal HostLifecycleCapability Lifecycle { get; set; }
    internal int AnalyzedRendererCount { get; set; }
    internal int SemanticallyRefusedRendererCount { get; set; }
    internal int OpaqueCandidateTriangleCount { get; set; }
}

internal sealed class AmusePlatformFinishPlugin : Plugin<AmusePlatformFinishPlugin>
{
    internal const string PluginQualifiedName = "com.alrauna.amuse";
    internal const string BarrierPassName = "AMUSE semantic barrier";
}

internal static class AmusePlatformFinishPass
{
    internal static void Execute(BuildContext context);
}
```

- Task 2 sets only execution/lifecycle. Renderer counts stay zero until Task 5. Lifecycle refusal lives only in `HostLifecycleCapability.Refusal`. Candidate refusal continues to use `RendererAnalysisRefusal`. The two diagnostic domains therefore stay distinct.
- Export with `[assembly: ExportsPlugin(typeof(AmusePlatformFinishPlugin))]`.

- [ ] **Step 1: Add gated synthetic plugin tests**

Export a gated `ZzzAnonymousOptimizingProducerPlugin` in `Optimizing` and an `AfterAmusePlatformFinishObserverPlugin` in PlatformFinish with `AfterPlugin("com.alrauna.amuse")`. They use test-only `BuildContext` states.

Keep their enable flag, disposable arming scope, and a minimal `TestVrchatPlatform : INDMFPlatformProvider` as private nested test helpers in `AmusePlatformFinishPluginTests.cs`. The platform implements only `QualifiedName => WellKnownPlatforms.VRChatAvatar30` and `DisplayName => "AMUSE test VRChat"`, using the interface defaults for everything else. Each synthetic pass returns immediately unless armed. Create a fresh `GameObject`, use `OverrideTemporaryDirectoryScope(null)`, process with `AvatarProcessor.ProcessAvatar(root, TestVrchatPlatform.Instance)`, and destroy the root in `finally`. No SDK type or new fixture utility file is introduced.

```csharp
[Test]
public void PlatformFinishBarrierRunsAfterAnonymousOptimizingProducer()
{
    using var armed = SyntheticPluginScope.Arm();
    using var assets = new OverrideTemporaryDirectoryScope(null);
    var root = new GameObject("AMUSE NDMF phase fixture");
    var context = AvatarProcessor.ProcessAvatar(root, TestVrchatPlatform.Instance);
    Assert.That(context.GetState<ProducerProbe>().Produced, Is.True);
    Assert.That(context.GetState<AmusePlatformFinishState>().HasExecuted, Is.True);
    Assert.That(context.GetState<ObserverProbe>().SawProducerAndAmuse, Is.True);
}
```

The real test wraps processing in `try/finally` and calls `Object.DestroyImmediate(root)` in `finally`. The compact example omits only that cleanup block.

Do not add a test-only production ordering property. The behavioral assertion is that an anonymous `Optimizing` producer is visible to AMUSE without any producer-specific AMUSE constraint.

- [ ] **Step 2: Run the focused class**

Run `Alrauna.Amuse.Tests.Editor.Build.AmusePlatformFinishPluginTests`.

Expected red: `CS0246` for missing AMUSE plugin/state.

- [ ] **Step 3: Implement one PlatformFinish pass**

```csharp
public override string QualifiedName => PluginQualifiedName;
public override string DisplayName => "AMUSE";
protected override void Configure()
{
    InPhase(BuildPhase.PlatformFinish)
        .Run(BarrierPassName, AmusePlatformFinishPass.Execute);
}
```

Use `[RunsOnPlatforms(WellKnownPlatforms.VRChatAvatar30)]`. The pass rejects null, throws on duplicate execution, captures capability, and sets `HasExecuted`. It does not scan/mutate/save/log.

- [ ] **Step 4: Refresh/import, then run Tasks 1 and 2 classes**

Expected green: the deliberately late-named Optimizing producer is before AMUSE. The after-AMUSE observer sees both states. No producer identity edge exists.

- [ ] **Step 5: Commit**

```bash
git add Packages/com.alrauna.amuse/Editor/Build/AmusePlatformFinishPlugin.cs Packages/com.alrauna.amuse/Editor/Build/AmusePlatformFinishPlugin.cs.meta Packages/com.alrauna.amuse/Tests/Editor/Build/AmusePlatformFinishPluginTests.cs Packages/com.alrauna.amuse/Tests/Editor/Build/AmusePlatformFinishPluginTests.cs.meta
git commit -m "feat: register AMUSE PlatformFinish barrier"
```

---

### Task 3: Eager immutable alpha texture evidence

**Files:**

- Modify: `Packages/com.alrauna.amuse/Editor/Host/UnityAlphaFieldEvidence.cs`
- Modify: `Packages/com.alrauna.amuse/Tests/Editor/Host/UnityAlphaFieldEvidenceTests.cs`

**Interfaces:**

- Consumes: existing `UnityTextureEvidence.TryGetSourceId`, supported-format rules, `Texture2D.GetPixels32(0)`, and immutable `AlphaTextureData`.
- Produces the unchanged constructor/provider signatures, but storage becomes `Dictionary<TextureSourceId, AlphaTextureData>`. It retains no `Texture2D`.

```csharp
internal UnityAlphaFieldEvidence(IEnumerable<Texture> textures);
internal static bool TryCapture(
    Texture texture,
    out TextureSourceId source,
    out AlphaTextureData field);
internal bool TryGetAlphaField(
    TextureSourceId source,
    TextureChannel channel,
    out AlphaTextureData field);
```

- Unsupported, destroyed-at-capture, unreadable, mipmapped, malformed, non-`Texture2D`, or unidentifiable inputs. The operation later refuses them as before.

- [ ] **Step 1: Replace delayed-read expectations with failing immutability tests**

```csharp
[Test]
public void TextureDestroyedAfterConstruction_DoesNotChangeCapturedField()
{
    var texture = ImportAsymmetric("destroyed-after-capture");
    Assert.That(UnityTextureEvidence.TryGetSourceId(texture, out var source), Is.True);
    var evidence = new UnityAlphaFieldEvidence(new Texture[] { texture });
    Assert.That(evidence.TryGetAlphaField(source, TextureChannel.Alpha, out var before), Is.True);

    AssetDatabase.DeleteAsset(TempFolder + "/destroyed-after-capture.png");

    Assert.That(evidence.TryGetAlphaField(source, TextureChannel.Alpha, out var after), Is.True);
    AssertSameField(before, after, "Captured alpha must not re-read Texture2D.");
}
```

Add a second test that mutates readable texture pixels after construction and proves lookup remains byte-identical.

- [ ] **Step 2: Run the focused class**

Run `Alrauna.Amuse.Tests.Editor.Host.UnityAlphaFieldEvidenceTests`.

Expected red: the destroyed lookup currently returns false and pixel mutation changes later evidence.

- [ ] **Step 3: Capture using the existing validation block**

Extract:

```csharp
internal static bool TryCapture(
    Texture texture,
    out TextureSourceId source,
    out AlphaTextureData field);
```

Move current source-ID, type, readability, mip, format, size, `GetPixels32`, length, and alpha-copy checks into it unchanged. Call it in the constructor. Make lookup validate source/channel, then only read the immutable dictionary. Task 4 reuses this bounded capture helper only when a material consumer requests immutable alpha-channel evidence.

- [ ] **Step 4: Re-run focused and integration tests**

Run:

```text
Alrauna.Amuse.Tests.Editor.Host.UnityAlphaFieldEvidenceTests
Alrauna.Amuse.Tests.Editor.Host.AlphaEvidenceClassifierIntegrationTests
Alrauna.Amuse.Tests.Editor.Host.RendererAlphaAnalysisIntegrationTests
```

Expected green: format/import/refusal behavior is unchanged. After-capture mutations do not affect evidence.

- [ ] **Step 5: Commit**

```bash
git add Packages/com.alrauna.amuse/Editor/Host/UnityAlphaFieldEvidence.cs Packages/com.alrauna.amuse/Tests/Editor/Host/UnityAlphaFieldEvidenceTests.cs
git commit -m "refactor: capture alpha texture evidence eagerly"
```

---

### Task 4: Requested material evidence capture and pure semantic interpretation

**Files:**

- Create: `Packages/com.alrauna.amuse/Editor/Host/UnityMaterialEvidenceCapture.cs`
- Create: `Packages/com.alrauna.amuse/Editor/Host/UnityMaterialEvidenceCapture.cs.meta`
- Modify: `Packages/com.alrauna.amuse/Editor/Semantics/UnityMaterialSemantics.cs`
- Modify: `Packages/com.alrauna.amuse/Editor/Semantics/Poiyomi/PoiyomiMaterialSemantics.cs`
- Modify: `Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonMaterialSemantics.cs`
- Modify: `Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonSourceAttestation.cs`
- Create: `Packages/com.alrauna.amuse/Tests/Editor/Host/UnityMaterialEvidenceCaptureTests.cs`
- Create: `Packages/com.alrauna.amuse/Tests/Editor/Host/UnityMaterialEvidenceCaptureTests.cs.meta`
- Modify: `Packages/com.alrauna.amuse/Tests/Editor/Semantics/UnityMaterialSemanticsTests.cs`

**Interfaces:**

- Consumes: Task 3 `UnityAlphaFieldEvidence.TryCapture`, current family evidence records, `MaterialSemantics`, and the five existing `UnityTextureEvidence` facts.
- Produces one small typed request/capture mechanism. Property names remain ordinary shader property names. There is no parser, arbitrary expression language, reflection schema, interface/registry, or semantic node model.

```csharp
[Flags]
internal enum TextureEvidenceKinds
{
    None = 0,
    ScaleOffset = 1 << 0,
    SourceIdentity = 1 << 1,
    Sampling = 1 << 2,
    ColorInterpretation = 1 << 3,
    SampledAlphaIsOne = 1 << 4,
    CanonicalNormalMap = 1 << 5,
    AlphaChannel = 1 << 6,
}

internal readonly struct TexturePropertyEvidenceRequest
{
    internal string PropertyName { get; }
    internal TextureEvidenceKinds Evidence { get; }
    internal TexturePropertyEvidenceRequest(
        string propertyName,
        TextureEvidenceKinds evidence);
}

internal sealed class MaterialEvidenceRequest
{
    internal bool ShaderName { get; }
    internal bool ActiveColorSpace { get; }
    internal IReadOnlyCollection<string> PresenceProperties { get; }
    internal IReadOnlyCollection<string> ScalarProperties { get; }
    internal IReadOnlyCollection<string> ColorProperties { get; }
    internal IReadOnlyCollection<string> VectorProperties { get; }
    internal IReadOnlyList<TexturePropertyEvidenceRequest> TextureProperties { get; }
    internal MaterialEvidenceRequest(
        bool shaderName,
        bool activeColorSpace,
        IEnumerable<string> presenceProperties,
        IEnumerable<string> scalarProperties,
        IEnumerable<string> colorProperties,
        IEnumerable<string> vectorProperties,
        IEnumerable<TexturePropertyEvidenceRequest> textureProperties);
    internal static MaterialEvidenceRequest Combine(
        params MaterialEvidenceRequest[] requests);
}

internal readonly struct MaterialEvidenceCaptureInput
{
    internal Material SourceMaterial { get; }
    internal MaterialEvidenceRequest Request { get; }
    internal MaterialEvidenceCaptureInput(
        Material sourceMaterial,
        MaterialEvidenceRequest request);
}

internal sealed class CapturedTextureEvidence
{
    internal bool HasSourceIdentity { get; }
    internal TextureSourceId SourceIdentity { get; }
    internal bool HasSampling { get; }
    internal TextureSampling Sampling { get; }
    internal bool HasColorInterpretation { get; }
    internal TextureColorInterpretation ColorInterpretation { get; }
    internal bool SampledAlphaIsProvenOne { get; }
    internal bool IsCanonicalNormalMap { get; }
    internal bool HasAlphaChannel { get; }
    internal AlphaTextureData AlphaChannel { get; }
}

internal readonly struct CapturedTextureAssignment
{
    internal bool IsAssigned { get; }
    internal TextureEvidenceKinds RequestedEvidence { get; }
    internal bool HasScaleOffset { get; }
    internal Vector2 Scale { get; }
    internal Vector2 Offset { get; }
    internal CapturedTextureEvidence Texture { get; }
}

internal sealed class CapturedMaterialEvidence
{
    internal bool HasShaderName { get; }
    internal string ShaderName { get; }
    internal bool HasActiveColorSpace { get; }
    internal ColorSpace ActiveColorSpace { get; }
    internal IReadOnlyCollection<CapturedTextureEvidence> Textures { get; }
    internal bool HasProperty(string name);
    internal bool TryGetScalar(string name, out float value);
    internal bool TryGetColor(string name, out Color value);
    internal bool TryGetVector(string name, out Vector4 value);
    internal bool TryGetTexture(
        string name, out CapturedTextureAssignment value);
}

internal static class UnityMaterialEvidenceCapture
{
    internal static IReadOnlyList<CapturedMaterialEvidence> Capture(
        IReadOnlyList<MaterialEvidenceCaptureInput> inputs);
}

internal enum CapturedAlphaMaterialFamily { Unsupported, Poiyomi, LilToon }

internal sealed class CapturedAlphaMaterial
{
    internal CapturedAlphaMaterialFamily Family { get; }
    internal CapturedMaterialEvidence Evidence { get; }
    internal PoiyomiSourceEvidence PoiyomiEvidence { get; }
    internal LilToonSourceEvidence LilToonEvidence { get; }
}

internal static class PoiyomiMaterialSemantics
{
    internal static MaterialEvidenceRequest AlphaEvidenceRequest { get; }
    internal static SemanticOutput<ScalarSemanticValue> InterpretVerifiedAlpha(
        CapturedMaterialEvidence evidence);
}

internal static class LilToonMaterialSemantics
{
    internal static MaterialEvidenceRequest AlphaEvidenceRequest { get; }
    internal static SemanticOutput<ScalarSemanticValue> InterpretVerifiedAlpha(
        CapturedMaterialEvidence evidence);
}

internal static class LilToonSourceAttestation
{
    internal static LilToonSourceEvidence GatherSourceEvidence(
        Shader shader,
        CapturedMaterialEvidence evidence);
}

internal static class UnityMaterialSemantics
{
    internal static IReadOnlyList<CapturedAlphaMaterial> CaptureAlphaMaterials(
        IReadOnlyList<Material> materials);
    internal static MaterialSemantics AnalyzeAlphaMaterial(
        CapturedAlphaMaterial captured);
}
```

`PoiyomiMaterialSemantics.GatherSourceEvidence` changes to the same `(Shader shader, CapturedMaterialEvidence evidence)` shape as the lilToon method. It remains private because it has no cross-file caller.

- A typed getter throws `ArgumentException` for an unrequested property. It returns `false` only when the requested property was absent or had the wrong Unity property type. A requested texture property returns `true` with `IsAssigned == false` when the property exists but has no texture. This makes request/consumer drift a developer failure rather than silent semantic uncertainty.
- Shader name and active color space are copied only when requested. The current alpha requests ask for shader name because dispatch and attestation consume it. They do not request active color space because neither current alpha equation reads it.
- `CapturedTextureEvidence` represents primitive host facts, not a texture sample, shader meaning, or optimization conclusion. Two assignments with one stable `TextureSourceId` reference the same `CapturedTextureEvidence` object. Host capture records unidentifiable textures independently and never gives them fabricated identity.
- `PoiyomiMaterialSemantics.AlphaEvidenceRequest` and `LilToonMaterialSemantics.AlphaEvidenceRequest` declare only properties read by their own alpha equations/gates and source-attestation material checks. The operation selects them per material and never combines them merely because both families occur in one batch. Base-color, emission, and normal-only properties, `_Cutoff`, render queue, tags, keywords, matrices, buffers, and every other unconsumed declared property remain absent.
- Purpose-specific Poiyomi/lilToon source hashing, canonicalization, feature extraction, attestation, and semantic conclusions remain in their existing semantics files. The alpha path consumes `CapturedMaterialEvidence` for material fields and runs required file/package reads once inside `CaptureAlphaMaterials`. Those concerns do not move into `Host`.
- Captured outputs contain no `UnityEngine.Object`, delegate, mutable collection, or lazy reader. `AnalyzeAlphaMaterial` returns a `MaterialSemantics` projection with the captured alpha result and the existing `AllUnknown` values for unrelated outputs. `AlphaSemanticsResolver` therefore remains unchanged. Preserve the existing full `AnalyzeBaseMaterial(Material)` behavior and every Poiyomi/lilToon equation, pin, variant, diagnostic code/detail, and output order.
- A later transformation may combine its concrete request with the applicable family request for the same material. Examples are material combining, atlasing, texture-array/control-texture generation, UV-sensitive transforms, and shader-feature simplification. Task 4 registers no property or semantic conclusion for those future transformations. It never spreads a combined request from one material to another material.

- [ ] **Step 1: Write failing request selectivity and reuse tests**

Use `PoiyomiSemanticTest.shader`, which declares `_Color`, `_MainTex`, and deliberately unconsumed `_Cutoff`.

```csharp
[Test]
public void RequestCapturesOnlyNamedEvidence()
{
    var request = new MaterialEvidenceRequest(
        false,
        false,
        Array.Empty<string>(),
        Array.Empty<string>(),
        new[] { "_Color" },
        Array.Empty<string>(),
        Array.Empty<TexturePropertyEvidenceRequest>());
    var evidence = UnityMaterialEvidenceCapture.Capture(new[]
    {
        new MaterialEvidenceCaptureInput(
            NewPoiyomiFixtureMaterial(), request),
    })[0];

    Assert.That(evidence.TryGetColor("_Color", out _), Is.True);
    Assert.Throws<ArgumentException>(
        () => evidence.TryGetScalar("_Cutoff", out _));
}

[Test]
public void SecondRequestAddsPropertyWithoutChangingCaptureMechanism()
{
    var request = new MaterialEvidenceRequest(
        false,
        false,
        Array.Empty<string>(),
        new[] { "_Cutoff" },
        Array.Empty<string>(),
        Array.Empty<string>(),
        Array.Empty<TexturePropertyEvidenceRequest>());
    var evidence = UnityMaterialEvidenceCapture.Capture(new[]
    {
        new MaterialEvidenceCaptureInput(
            NewPoiyomiFixtureMaterial(), request),
    })[0];

    Assert.That(evidence.TryGetScalar("_Cutoff", out var value), Is.True);
    Assert.That(value, Is.EqualTo(0.5f));
}
```

Assert `PoiyomiMaterialSemantics.AlphaEvidenceRequest` does not request `_Cutoff`, `_Mode`, `_SrcBlend`, `_DstBlend`, `_EmissionColor`, `_EmissionStrength`, `_BumpScale`, or `_BumpMapUV`. Assert `LilToonMaterialSemantics.AlphaEvidenceRequest` does not request Poiyomi-only `_AlphaForceOpaque`, `_MainIgnoreTexAlpha`, or `_AlphaToCoverage`. Focused alpha/attestation tests are the positive guard for request coverage. Any property a current consumer reads but the request omits throws rather than returning Unknown. The complete semantic suite proves the untouched non-alpha equations retain their behavior.

- [ ] **Step 2: Write failing per-material request, texture deduplication, and immutability tests**

Import one readable asymmetric RGBA32 texture. Assign it to the `_MainTex` of a Poiyomi fixture and the `_EmissionMap` of a lilToon fixture. Give the two inputs different scalar and texture-property requests while requesting the same texture fact set.

```csharp
var textureFacts =
    TextureEvidenceKinds.SourceIdentity |
    TextureEvidenceKinds.Sampling |
    TextureEvidenceKinds.ColorInterpretation |
    TextureEvidenceKinds.ScaleOffset |
    TextureEvidenceKinds.AlphaChannel;
var poiyomiRequest = new MaterialEvidenceRequest(
    false,
    false,
    Array.Empty<string>(),
    new[] { "_Cutoff" },
    Array.Empty<string>(),
    Array.Empty<string>(),
    new[]
    {
        new TexturePropertyEvidenceRequest("_MainTex", textureFacts),
    });
var lilToonRequest = new MaterialEvidenceRequest(
    false,
    false,
    Array.Empty<string>(),
    new[] { "_UseEmission" },
    Array.Empty<string>(),
    Array.Empty<string>(),
    new[]
    {
        new TexturePropertyEvidenceRequest("_EmissionMap", textureFacts),
    });

var captured = UnityMaterialEvidenceCapture.Capture(new[]
{
    new MaterialEvidenceCaptureInput(poiyomiMaterial, poiyomiRequest),
    new MaterialEvidenceCaptureInput(lilToonMaterial, lilToonRequest),
});

Assert.That(captured[0].TryGetScalar("_Cutoff", out _), Is.True);
Assert.Throws<ArgumentException>(
    () => captured[0].TryGetScalar("_UseEmission", out _));
Assert.That(captured[1].TryGetScalar("_UseEmission", out _), Is.True);
Assert.Throws<ArgumentException>(
    () => captured[1].TryGetScalar("_Cutoff", out _));
Assert.That(captured[0].TryGetTexture("_MainTex", out var main), Is.True);
Assert.Throws<ArgumentException>(
    () => captured[0].TryGetTexture("_EmissionMap", out _));
Assert.That(
    captured[1].TryGetTexture("_EmissionMap", out var emission), Is.True);
Assert.Throws<ArgumentException>(
    () => captured[1].TryGetTexture("_MainTex", out _));
Assert.That(ReferenceEquals(main.Texture, emission.Texture), Is.True);
Assert.That(main.Texture.HasAlphaChannel, Is.True);
```

Then mutate pixels, importer sampling/color interpretation, both material assignments, and scale/offset. Then destroy the live materials and delete the texture asset. Assert every captured primitive and alpha byte remains unchanged. Walk all reachable captured fields and fail on `UnityEngine.Object`. This single test proves request isolation and operation-wide stable-source deduplication together.

- [ ] **Step 3: Run capture and dispatcher tests**

Run:

```text
Alrauna.Amuse.Tests.Editor.Host.UnityMaterialEvidenceCaptureTests
Alrauna.Amuse.Tests.Editor.Semantics.UnityMaterialSemanticsTests
```

Expected red: `CS0246` for the request/captured evidence types.

- [ ] **Step 4: Implement typed request validation and two-pass capture**

Validate a null input list, a null request, blank property names, undefined flags, duplicate names within one category. Also validate the same property requested under incompatible scalar/color/vector/texture categories. A null/destroyed source material is an ordinary unsupported input and produces empty evidence for its request. The operation allows presence-only overlap with a typed request. `Combine` computes deterministic ordinal set union and bitwise-unions repeated texture-property flags. This serves callers that genuinely have multiple consumers of the same material.

Capture in two private passes inside the one static method:

1. For each input, resolve only the requested names of that input with `Shader.FindPropertyIndex`. Read `HasProperty` once per distinct requested name. Validate its `ShaderPropertyType` without enumerating the shader. Copy only its requested scalar/color/vector/texture assignments and scale/offset into transient builders. Scalar accepts Float, Range, or Int. Across all inputs, resolve stable source identities and union requested texture flags by `TextureSourceId`.
2. Capture the operation-wide requested union of each identified texture once through `UnityTextureEvidence` and Task 3. Capture each unidentified assigned texture independently. Construct immutable shared `CapturedTextureEvidence`. Then construct one final material evidence result per input in original order. Never merge property-name requests across different inputs.

Do not call `Shader.GetPropertyCount`, enumerate shader properties, retain transient `Material`/`Texture`, expose builders, add an interface, or add a registration mechanism.

- [ ] **Step 5: Declare the narrow current requests and capture family evidence**

In each existing frontend, build one static alpha request. Derive it from the exact property constants and gate/schema arrays read by its alpha equation and source-attestation material checks. Request presence-only facts for schema checks. Request scalar/color/vector facts only for matching alpha/attestation reads. Request texture flags only for `_MainTex` facts the alpha path consumes. Request `AlphaChannel` only for Poiyomi `_MainTex`, the only current sampled alpha-classifier source. Do not request active color space or base-color/emission/normal-only fields.

`UnityMaterialSemantics.CaptureAlphaMaterials` examines only the shader name of each live material to select the Poiyomi request, the lilToon request, or a private empty request for an unsupported/null material. It creates one aligned `MaterialEvidenceCaptureInput` per material and calls host capture once. Family requests therefore remain isolated while all inputs share texture deduplication. It then gathers only the selected source-evidence family and returns immutable `CapturedAlphaMaterial` values in input order. Do not run the lilToon filesystem/include scan or request lilToon fields for a Poiyomi material, and vice versa. Move material-dependent attestation reads needed by this path to `CapturedMaterialEvidence`. Retain file/package/source reads in purpose-specific code. Defensively copy `LilToonSourceEvidence.CompiledFeatures`; nested canonicalization records already copy their inputs.

Add a semantic-layer batch test with one material per family. Assert the Poiyomi evidence rejects access to lilToon-only `_Invisible`. Assert the lilToon evidence rejects access to Poiyomi-only `_AlphaForceOpaque`. Assert both families still produce their prior alpha results. Do not add a family registry or selector interface. Two explicit shader-name branches match the existing dispatcher style.

- [ ] **Step 6: Convert interpreter helpers to captured evidence**

Extract only each existing alpha equation/helper to `InterpretVerifiedAlpha(CapturedMaterialEvidence)`. Replace its `HasProperty`/typed getters/texture helpers with captured equivalents. Missing or wrong-typed requested values take the same conservative diagnostic branch. The existing full live-material interpreter calls the same captured alpha helper after a one-material alpha request. Its base-color/emission/normal paths remain unchanged. `AnalyzeAlphaMaterial` verifies the captured family evidence, invokes that helper, and creates the alpha-only `MaterialSemantics` projection. No other equation migrates and no semantic type moves into `Host`.

- [ ] **Step 7: Refresh/import and run capture plus all semantic classes**

Run:

```text
Alrauna.Amuse.Tests.Editor.Host.UnityMaterialEvidenceCaptureTests
Alrauna.Amuse.Tests.Editor.Semantics.UnityMaterialSemanticsTests
Alrauna.Amuse.Tests.Editor.Semantics.Poiyomi.PoiyomiMaterialSemanticsTests
Alrauna.Amuse.Tests.Editor.Semantics.Poiyomi.PoiyomiSourceAttestationTests
Alrauna.Amuse.Tests.Editor.Semantics.Poiyomi.PoiyomiTextureEvidenceTests
Alrauna.Amuse.Tests.Editor.Semantics.Poiyomi.PoiyomiBaseColorTests
Alrauna.Amuse.Tests.Editor.Semantics.Poiyomi.PoiyomiAlphaTests
Alrauna.Amuse.Tests.Editor.Semantics.Poiyomi.PoiyomiEmissionTests
Alrauna.Amuse.Tests.Editor.Semantics.Poiyomi.PoiyomiNormalTests
Alrauna.Amuse.Tests.Editor.Semantics.Poiyomi.PoiyomiAdversarialTests
Alrauna.Amuse.Tests.Editor.Semantics.LilToon.LilToonAttestationTests
Alrauna.Amuse.Tests.Editor.Semantics.LilToon.LilToonBaseColorTests
Alrauna.Amuse.Tests.Editor.Semantics.LilToon.LilToonAlphaTests
Alrauna.Amuse.Tests.Editor.Semantics.LilToon.LilToonEmissionTests
Alrauna.Amuse.Tests.Editor.Semantics.LilToon.LilToonNormalTests
Alrauna.Amuse.Tests.Editor.Semantics.LilToon.LilToonAdversarialTests
```

Expected green: each family request satisfies only its consumer reads. `_Cutoff` stays absent until the synthetic request asks for it. Different per-material requests coexist in one batch. Shared texture evidence is reference-identical across that batch. All semantics and diagnostics remain unchanged.

- [ ] **Step 8: Scan the boundary**

```bash
rg -n "GetPropertyCount|GetPropertyName|GetPropertyType" Packages/com.alrauna.amuse/Editor/Host/UnityMaterialEvidenceCapture.cs
rg -n "Material material|material\.(HasProperty|GetFloat|GetColor|GetVector|GetTexture|GetTextureScale|GetTextureOffset)" Packages/com.alrauna.amuse/Editor/Semantics
rg -n "UnityEngine\.Object|Material|Texture2D|Texture\b" Packages/com.alrauna.amuse/Editor/Host/UnityMaterialEvidenceCapture.cs
rg -n "interface I|Registry|Provider|ShaderIr|Expression" Packages/com.alrauna.amuse/Editor/Host/UnityMaterialEvidenceCapture.cs
```

Expected: no shader-property enumeration. Live reads only in host capture and explicit purpose-specific source capture. Captured fields contain no live object. No generic semantic/framework abstraction.

- [ ] **Step 9: Stage exact files and commit**

```bash
git add Packages/com.alrauna.amuse/Editor/Host/UnityMaterialEvidenceCapture.cs Packages/com.alrauna.amuse/Editor/Host/UnityMaterialEvidenceCapture.cs.meta Packages/com.alrauna.amuse/Editor/Semantics/UnityMaterialSemantics.cs Packages/com.alrauna.amuse/Editor/Semantics/Poiyomi/PoiyomiMaterialSemantics.cs Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonMaterialSemantics.cs Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonSourceAttestation.cs Packages/com.alrauna.amuse/Tests/Editor/Host/UnityMaterialEvidenceCaptureTests.cs Packages/com.alrauna.amuse/Tests/Editor/Host/UnityMaterialEvidenceCaptureTests.cs.meta Packages/com.alrauna.amuse/Tests/Editor/Semantics/UnityMaterialSemanticsTests.cs
git commit -m "refactor: capture requested material evidence"
```

---

### Task 5: Immutable renderer extraction and proof-path migration

**Files:**

- Create: `Packages/com.alrauna.amuse/Editor/Host/UnityRendererAlphaSnapshot.cs`
- Create: `Packages/com.alrauna.amuse/Editor/Host/UnityRendererAlphaSnapshot.cs.meta`
- Modify: `Packages/com.alrauna.amuse/Editor/Host/UnityRendererAlphaAnalysis.cs`
- Modify: `Packages/com.alrauna.amuse/Editor/Build/AmusePlatformFinishPlugin.cs`
- Create: `Packages/com.alrauna.amuse/Tests/Editor/Host/UnityRendererAlphaSnapshotTests.cs`
- Create: `Packages/com.alrauna.amuse/Tests/Editor/Host/UnityRendererAlphaSnapshotTests.cs.meta`
- Modify: `Packages/com.alrauna.amuse/Tests/Editor/Host/UnityRendererAlphaAnalysisTests.cs`
- Modify: `Packages/com.alrauna.amuse/Tests/Editor/Build/AmusePlatformFinishPluginTests.cs`

**Interfaces:**

- Consumes: Task 4 captured materials, Task 3 alpha fields, current renderer refusal vocabulary, resolver/classifier/planner, and Task 2 summary.
- Produces:

```csharp
internal sealed class UnityRendererAlphaSnapshot
{
    internal int VertexCount { get; }
    internal IReadOnlyList<Vector3> Positions { get; }
    internal IReadOnlyList<Vector2> Uv0 { get; }
    internal bool HasUv0 { get; }
    internal IReadOnlyList<UnitySubmeshAlphaSnapshot> Submeshes { get; }
    internal IReadOnlyList<CapturedAlphaMaterial> Materials { get; }
}

internal sealed class UnitySubmeshAlphaSnapshot
{
    internal int SubmeshIndex { get; }
    internal int MaterialSlotIndex { get; }
    internal IReadOnlyList<int> Indices { get; }
}

internal sealed class UnityRendererMutationTarget
{
    internal Renderer Renderer { get; }
    internal Mesh ExpectedMesh { get; }
    internal int ExpectedMaterialSlotCount { get; }
}

internal sealed class UnityRendererAlphaExtraction
{
    internal RendererAnalysisRefusal Refusal { get; }
    internal UnityRendererAlphaSnapshot Snapshot { get; }
    internal UnityRendererMutationTarget MutationTarget { get; }
}

internal delegate MaterialSemantics CapturedAlphaMaterialSemanticsResolver(
    CapturedAlphaMaterial material);
internal static UnityRendererAlphaExtraction Capture(Renderer renderer);
internal static RendererAlphaAnalysis Analyze(
    UnityRendererAlphaSnapshot snapshot,
    CapturedAlphaMaterialSemanticsResolver resolveSemantics = null);
```

- Snapshot/result types have no live object. Only `UnityRendererMutationTarget` has renderer/mesh handles, and it is never passed to semantics/proof/planning.
- Preserve `Analyze(Renderer)` as capture followed immediately by pure analysis.
- PlatformFinish enumerates `AvatarRootObject.GetComponentsInChildren<Renderer>(true)`, captures/analyzes immediately, stores only counts, and retains no plan/snapshot/target after return.

- [ ] **Step 1: Add failing snapshot and target-separation tests**

Capture a two-triangle renderer using a captured constant-one semantics fixture. Replace `sharedMesh`, mutate original positions/UV/indices, replace materials, and mutate material values. The old snapshot must still produce two opaque candidates. A fresh capture must observe replacement state.

Field-walk `UnityRendererAlphaSnapshot` and `UnitySubmeshAlphaSnapshot` for forbidden `UnityEngine.Object` types. Separately assert the mutation target holds the exact accepted renderer/mesh.

- [ ] **Step 2: Run renderer classes**

Run:

```text
Alrauna.Amuse.Tests.Editor.Host.UnityRendererAlphaSnapshotTests
Alrauna.Amuse.Tests.Editor.Host.UnityRendererAlphaAnalysisTests
```

Expected red: `CS0246` for extraction/snapshot types.

- [ ] **Step 3: Extract eagerly**

Move live reads through renderer/topology/material validation, positions, UV0, indices, and captured materials into `Capture`. Defensively copy every array/list. Create the mutation target only after acceptance. Refusal returns null snapshot/target and does no semantic work.

Preserve property-block, renderer-type, missing-mesh, slot-mapping, topology, malformed-array, and invalid-index refusal behavior.

- [ ] **Step 4: Make proof/planning pure**

Move resolution/classification/planner composition to `Analyze(UnityRendererAlphaSnapshot)`. Build alpha lookup from `Evidence.Textures` entries of each captured material whose stable source identity and requested alpha channel are present. This overload must not touch `Renderer`, `Mesh`, `Material`, `Texture`, `AssetDatabase`, or `QualitySettings`.

`Capture` passes the accepted `sharedMaterials` array once to `UnityMaterialSemantics.CaptureAlphaMaterials`, preserving one material-capture operation and texture deduplication across all slots. Move the existing fixture seam to the pure side as `CapturedAlphaMaterialSemanticsResolver`. Its production default is `UnityMaterialSemantics.AnalyzeAlphaMaterial`, and tests may return constant semantics from the captured material without any live-material access.

- [ ] **Step 5: Wire PlatformFinish dry analysis**

Lifecycle refusal leaves all renderer counts zero and returns before capture. Permission triggers capture/analysis of each renderer, increments either `AnalyzedRendererCount` or `SemanticallyRefusedRendererCount`, and sums `Plan.OpaqueTriangleCount`; each refused analysis retains its existing `RendererAnalysisRefusal` during execution. No plan/target survives and no mutation occurs.

Extend the synthetic Optimizing producer to replace the mesh/material of a renderer. Use an internal overload `Execute(BuildContext, HostLifecycleFacts)` for injected facts. Do not add a global override. Assert the AMUSE summary reflects produced state, proving ordinary state composition without Meshia installation/identity. Add a separate unsupported-renderer case proving an exact lifecycle permit plus a semantic refusal increments only `SemanticallyRefusedRendererCount`. An unsupported lifecycle increments neither renderer count.

- [ ] **Step 6: Refresh/import and run host/order classes**

Run:

```text
Alrauna.Amuse.Tests.Editor.Host.UnityRendererAlphaSnapshotTests
Alrauna.Amuse.Tests.Editor.Host.UnityRendererAlphaAnalysisTests
Alrauna.Amuse.Tests.Editor.Host.RendererAlphaAnalysisIntegrationTests
Alrauna.Amuse.Tests.Editor.Build.AmusePlatformFinishPluginTests
```

Expected green: facade behavior remains, later live mutations cannot change captured proof, and the operation analyzes anonymous Optimizing output without an identity edge.

- [ ] **Step 7: Verify bounded migration**

```bash
rg -n "Renderer|Mesh|Material|Texture2D|AssetDatabase|QualitySettings" Packages/com.alrauna.amuse/Editor/Analysis Packages/com.alrauna.amuse/Editor/Host/UnityRendererAlphaSnapshot.cs
rg -n "Alrauna\.Amuse\.Research|com\.alrauna\.amuse\.research" Packages/com.alrauna.amuse/Editor/Build Packages/com.alrauna.amuse/Editor/Host
```

Expected: Analysis remains host-object-free. Snapshots contain value structs only. No research dependency.

- [ ] **Step 8: Commit**

```bash
git add Packages/com.alrauna.amuse/Editor/Host/UnityRendererAlphaSnapshot.cs Packages/com.alrauna.amuse/Editor/Host/UnityRendererAlphaSnapshot.cs.meta Packages/com.alrauna.amuse/Editor/Host/UnityRendererAlphaAnalysis.cs Packages/com.alrauna.amuse/Editor/Build/AmusePlatformFinishPlugin.cs Packages/com.alrauna.amuse/Tests/Editor/Host/UnityRendererAlphaSnapshotTests.cs Packages/com.alrauna.amuse/Tests/Editor/Host/UnityRendererAlphaSnapshotTests.cs.meta Packages/com.alrauna.amuse/Tests/Editor/Host/UnityRendererAlphaAnalysisTests.cs Packages/com.alrauna.amuse/Tests/Editor/Build/AmusePlatformFinishPluginTests.cs
git commit -m "refactor: analyze renderers from immutable capture"
```

---

### Task 6: Prepare/apply, fatal failure, and NDMF asset ownership

**Files:**

- Create: `Packages/com.alrauna.amuse/Editor/Build/AmuseBuildOperation.cs`
- Create: `Packages/com.alrauna.amuse/Editor/Build/AmuseBuildOperation.cs.meta`
- Create: `Packages/com.alrauna.amuse/Tests/Editor/Build/AmuseBuildOperationTests.cs`
- Create: `Packages/com.alrauna.amuse/Tests/Editor/Build/AmuseBuildOperationTests.cs.meta`
- Modify: `Packages/com.alrauna.amuse/Tests/Editor/Build/AmusePlatformFinishPluginTests.cs`

**Interfaces:**

- Consumes: lifecycle capability and NDMF `IAssetSaver`/error behavior.
- Produces:

```csharp
internal enum AmuseBuildOperationOutcome
{
    LifecycleRefused,
    PreparationRefused,
    NoMutationRequired,
    Mutated,
}

internal readonly struct AmusePreparationDecision
{
    internal bool IsPrepared { get; }
    internal bool HasMutation { get; }
    internal string RefusalReason { get; }
    internal static AmusePreparationDecision Refused(string reason);
    internal static AmusePreparationDecision NoMutation();
    internal static AmusePreparationDecision Ready();
}

internal sealed class AmuseBuildOperationResult
{
    internal AmuseBuildOperationOutcome Outcome { get; }
    internal HostLifecycleCapability Lifecycle { get; }
    internal string RefusalReason { get; }
}

internal delegate AmusePreparationDecision PrepareAmuseMutation(
    IAssetSaver assetSaver);
internal delegate void ApplyAmuseMutation();

internal static AmuseBuildOperationResult Execute(
    HostLifecycleCapability lifecycle,
    IAssetSaver assetSaver,
    PrepareAmuseMutation prepare,
    ApplyAmuseMutation apply);
```

- Prepare receives only the current asset saver, not avatar root/target. Prepared outputs are closed over locally by apply.
- Apply invocation is the first-mutation boundary. Apply exceptions are not caught. NDMF records `InternalError` and normal build returns unsuccessful.
- Only an explicit `AmusePreparationDecision.Refused(reason)` becomes `PreparationRefused`. Preparation exceptions are not caught. Like apply exceptions, they propagate to NDMF as `InternalError`. Explicit refusal/no-mutation never calls apply. No rollback/transaction.

- [ ] **Step 1: Add failing pure boundary tests**

```csharp
[Test]
public void PreparationCompletesBeforeFirstMutation()
{
    var events = new List<string>();
    var result = AmuseBuildOperation.Execute(
        SupportedCapability(),
        new RecordingAssetSaver(events),
        saver =>
        {
            events.Add("prepare");
            return AmusePreparationDecision.Ready();
        },
        () => events.Add("mutate"));

    Assert.That(events, Is.EqualTo(new[] { "prepare", "mutate" }));
    Assert.That(result.Outcome, Is.EqualTo(AmuseBuildOperationOutcome.Mutated));
}

[Test]
public void LifecycleRefusalNeverInvokesMutation()
{
    var mutated = false;
    var result = AmuseBuildOperation.Execute(
        RefusedCapability(),
        new RecordingAssetSaver(),
        _ => AmusePreparationDecision.Ready(),
        () => mutated = true);
    Assert.That(result.Outcome, Is.EqualTo(AmuseBuildOperationOutcome.LifecycleRefused));
    Assert.That(mutated, Is.False);
}

[Test]
public void ExplicitPreparationRefusalIsOrdinaryAndNeverInvokesApply()
{
    var applied = false;
    var result = AmuseBuildOperation.Execute(
        SupportedCapability(),
        new RecordingAssetSaver(),
        _ => AmusePreparationDecision.Refused("unsupported synthetic input"),
        () => applied = true);

    Assert.That(
        result.Outcome,
        Is.EqualTo(AmuseBuildOperationOutcome.PreparationRefused));
    Assert.That(result.RefusalReason, Is.EqualTo("unsupported synthetic input"));
    Assert.That(applied, Is.False);
}

[Test]
public void UnexpectedPreparationExceptionPropagatesWithoutApplying()
{
    var applied = false;
    var exception = Assert.Throws<InvalidOperationException>(() =>
        AmuseBuildOperation.Execute(
            SupportedCapability(),
            new RecordingAssetSaver(),
            _ => throw new InvalidOperationException(
                "synthetic preparation failure"),
            () => applied = true));

    Assert.That(exception.Message, Is.EqualTo("synthetic preparation failure"));
    Assert.That(applied, Is.False);
}
```

Add no-mutation, null argument, and uncaught apply-exception cases. The two tests above are the required distinction between an expected unsupported decision and an unexpected preparation defect.

- [ ] **Step 2: Run operation tests**

Run `Alrauna.Amuse.Tests.Editor.Build.AmuseBuildOperationTests`.

Expected red: `CS0246` for operation types.

- [ ] **Step 3: Implement the minimal executor**

Validate arguments. Refused lifecycle returns before prepare. Invoke prepare with no catch. Return `PreparationRefused` only when the returned decision is explicitly refused. Otherwise return the no-mutation result. Invoke apply with no catch, then return `Mutated`.

Add no retry, compensation, cleanup system, state machine beyond four outcomes, generic plan base, or persistence wrapper.

- [ ] **Step 4: Add synthetic generated-asset and fatal-failure integration**

Use a gated test PlatformFinish plugin after AMUSE. Success prepare creates a transient `Mesh`, calls `assetSaver.SaveAsset(mesh)`, returns ready. Apply assigns it to a fixture `MeshFilter.sharedMesh`. Process with:

```csharp
using var directory =
    new OverrideTemporaryDirectoryScope("Assets/AMUSE-GeneratedAssetTests");
var context = AvatarProcessor.ProcessAvatar(
    root, TestVrchatPlatform.Instance);
```

Define the same minimal private `TestVrchatPlatform : INDMFPlatformProvider` inside `AmuseBuildOperationTests.cs`. Do not expose or generalize a test fixture API. Initialize test-local `string generatedPath = null` before the `try` of the test. Inside the active prepare callback, call `assetSaver.SaveAsset(mesh)` and immediately record `savedByActiveSaver = assetSaver.GetPersistedAssets().Contains(mesh)` before returning ready. `AvatarProcessor.ProcessAvatar` then calls `BuildContext.Finish`, which disposes the saver. Never access `context.AssetSaver` or any `IAssetSaver` member after `ProcessAvatar` returns. After processing, assert `savedByActiveSaver` is true, `EditorUtility.IsPersistent(mesh)` and `AssetDatabase.Contains(mesh)` are true, assign the normalized `generatedPath = AssetDatabase.GetAssetPath(mesh)`, and assert that it is nonempty, starts with `Assets/AMUSE-GeneratedAssetTests/`, and names an `.asset` container. Assert the processed fixture's `MeshFilter.sharedMesh` is reference-equal to that exact mesh, and assert production has no `AssetDatabase.CreateAsset`. A `finally` block destroys the root and deletes `Assets/AMUSE-GeneratedAssetTests` with `AssetDatabase.DeleteAsset`, then verifies the folder is absent and, when `generatedPath` is nonempty, `AssetDatabase.LoadMainAssetAtPath(generatedPath)` is null.

In a preparation-failure mode, prepare throws `InvalidOperationException("synthetic preparation failure")` before apply. Assert apply was not invoked, `context.Successful == false`, and `context.ErrorReport.Errors` contains an `ErrorSeverity.InternalError` whose rendered message contains that text.

In a separate fatal-apply mode, apply changes one clone-local field then throws `InvalidOperationException("synthetic post-mutation failure")`. Assert `context.Successful == false` and `context.ErrorReport.Errors` contains an entry whose `TheError.Severity` is `ErrorSeverity.InternalError` and whose rendered message contains that text. Do not assert rollback.

Refusal mode asserts renderer/mesh/material references and values remain unchanged.

- [ ] **Step 5: Run operation and PlatformFinish tests**

Run:

```text
Alrauna.Amuse.Tests.Editor.Build.AmuseBuildOperationTests
Alrauna.Amuse.Tests.Editor.Build.AmusePlatformFinishPluginTests
```

Expected green: NDMF owns generated mesh. Explicit refusal preserves input without error. Unexpected preparation fails the build before apply. Fatal apply fails the build after first mutation.

- [ ] **Step 6: Scan scope**

```bash
rg -n "AssetDatabase\.(CreateAsset|AddObjectToAsset)|rollback|transaction|DAO|d4rk|Candidate[AB]|opaque-target-preservation" Packages/com.alrauna.amuse/Editor
```

Expected: no new custom persistence, rollback/transaction, or DAO match.

- [ ] **Step 7: Run the complete public EditMode suite**

Use the complete unfiltered command. Expected: zero failures/unexpected skips and no new compiler/Console error.

- [ ] **Step 8: Inspect repository integrity**

```bash
git diff --check
git status --short --branch
git diff --stat
git diff --cached --stat
git diff -- Packages/manifest.json Packages/packages-lock.json
rg -n "DAO|d4rk|Candidate A|Candidate B|opaque-target-preservation|Shader Toggles|rollback|transaction|plugin registry|resolved tail" Packages/com.alrauna.amuse/Editor Packages/com.alrauna.amuse/Tests/Editor
```

Expected: only planned files/sidecars, no package diff/generated state/GUID churn, and no deferred implementation.

- [ ] **Step 9: Commit**

```bash
git add Packages/com.alrauna.amuse/Editor/Build/AmuseBuildOperation.cs Packages/com.alrauna.amuse/Editor/Build/AmuseBuildOperation.cs.meta Packages/com.alrauna.amuse/Tests/Editor/Build/AmuseBuildOperationTests.cs Packages/com.alrauna.amuse/Tests/Editor/Build/AmuseBuildOperationTests.cs.meta Packages/com.alrauna.amuse/Tests/Editor/Build/AmusePlatformFinishPluginTests.cs
git commit -m "feat: establish AMUSE mutation lifecycle boundary"
```

---

## Final implementation acceptance

After Task 6, review spec and plan line by line. Review only when evidence shows:

1. `com.alrauna.amuse` exports one PlatformFinish barrier pass.
2. Anonymous/Meshia-style Optimizing state is visible without producer identity edges.
3. The design treats current PlatformFinish actors as irrelevant. No tail introspection exists.
4. Exact lifecycle capability fails closed with a reason distinct from semantic refusal.
5. Apply-on-Play positive mutation is unavailable.
6. Each Poiyomi/lilToon material receives only its applicable alpha/attestation evidence request. This holds even when both occur in one capture operation. Unrelated family properties remain absent. The operation may combine multiple concrete requests only for the same material.
7. Stable captured source identity shares texture-channel storage within a material-capture operation. Texture pixels, requested material/source facts, renderer relationships, positions, UV0, topology, and indices stay eager immutable values.
8. Live mutation or destruction after capture cannot alter captured semantics or plan.
9. Existing semantic/classifier/planner behavior remains unchanged.
10. Preparation completes before apply. Apply invocation is the first mutation.
11. Lifecycle refusal and explicit `AmusePreparationDecision.Refused(...)` never invoke apply and preserve input.
12. Unexpected preparation exceptions propagate before apply, and apply exceptions propagate after the first mutation. NDMF treats both as build-blocking and promises no rollback.
13. Generated output uses `BuildContext.AssetSaver`. No custom persistence exists.
14. Complete public EditMode suite passes and package/assembly boundaries remain intact.
15. No DAO, full alpha application, opaque target, universal shader IR, generic optimizer framework, registry, or transaction appears.

The next branch may consume these immutable analyses and the operation boundary to prepare/apply an alpha separation plan. It must add exact opaque-target proof/construction and concrete mesh/material mutation. The foundation pre-authorizes neither.

## Explicitly deferred work

- Complete alpha mesh/material application, opaque/transparent material creation, target coverage proof, and source/output relationship application.
- Reachable material swaps, animated proof-relevant properties, broader deformation/reachability, and new shader families.
- DAO execution ownership, cooperation types, bridge, Candidate A/B, preservation profile, version detection, and Shader Toggles.
- Apply-on-Play mutation, upload-attempt identity, callback inventory, reflection, post-NDMF callbacks, and late authorization.
- Multi-renderer/global planning, atlasing, material combining, UV/control-texture transformations, universal avatar IR, plugin registry, and generic framework.
- This plan does not touch Unity test CI hosting. Current release/listing workflows remain unchanged.
