# Optimizer merge integration test implementation plan

Privacy note: This plan uses public synthetic fixtures and sanitized scene traits. It contains no private asset data or identifiers.

Date: 2026-09-12.
Status on 2026-09-12: Tasks 1 through 5 executed on their approved branches and projects; the Task 6 case set landed as mask darkening, streaming refusal, material swap, component survival, and the disable control. The inactive-geometry and 32-bit cases became vendor-interaction findings and returned to the repair plan. The DAO sequence ran in the disposable integration project: both cases passed. Findings are recorded in the investigation addendum.

Goal: Expose the confirmed predicate-isolation defect through focused regressions and real optimizer builds before changing production behavior.

Architecture: Keep capture tests in the existing host test class. Repair the existing AAO fixture and oracle. Add test-only observation at NDMF phase boundaries and a DAO case that uses the real SDK sequence.

Tech stack: Unity 2022.3.22f1, NUnit EditMode tests, NDMF 1.14.4, AAO 1.9.17, DAO 4.5.4, and lilToon 2.3.4.

Spec: [Optimizer merge integration design](../specs/2026-09-12-optimizer-merge-integration-design.md).
Evidence: [Executed investigation](../investigations/2026-09-12-optimizer-merge-evidence-isolation.md).

## Execution brief

The inspected consumer branch is `chore/harden-integration-testing` at `578b5eb`. Its local integration base is `main` at `d05171d`.

Allowed mutations for this plan are test code, public synthetic fixtures, test-only assembly configuration, and related test documentation. Do not change production behavior, vendor packages, private assets, or source scenes.

The generic capture defect is already on `main`. Keep its regression and repair separate from consumer-branch harness changes. Use a focused prerequisite branch from the latest authorized local `main` for the generic repair. Do not switch a tool-managed worktree through shell commands.

Do not stage, commit, push, publish, or create a pull request without explicit authorization. Do not restore unrelated player configuration changes from the investigation.

Before execution, make sure that the base and working changes still match this brief. If they differ, update the brief from inspected source.

## Global constraints

- Core regressions use redistributable stand-in shaders and real capture logic.
- Real optimizer tests use installed optimizers and real attested lilToon shaders.
- Never replace merging, alpha fields, classification, or application with mocks.
- No product tests run in the Census Lab project.
- Before Unity operations, enumerate instances and require an exact normalized `Application.dataPath` match.
- Run DAO only in a disposable public integration project. Its optimizer deletes its project-level generated output folder.
- Missing required packages, zero tests, skipped mandatory cases, and unsuccessful builds fail the required profile.
- Preserve the existing release exclusions for tests and the research package.
- Keep each fixture and its Unity metadata together.
- Use inline execution unless the user separately authorizes subagents.

## File map

| File | Work |
| --- | --- |
| `Packages/com.alrauna.amuse/Tests/Editor/Host/UnityMaterialEvidenceCaptureTests.cs` | Add shared-source predicate regressions |
| `Packages/com.alrauna.amuse/Tests/Editor/Host/UnityAnimationEvidenceCaptureTests.cs` | Replace capture-count assumptions only when the repair changes them |
| `Packages/com.alrauna.amuse/Tests/Editor/Build/AaoMergedConsumptionTests.cs` | Replace invalid geometry, add mixed-mode cases, and replace the UV oracle |
| `Packages/com.alrauna.amuse/Tests/Editor/Build/FixtureAvatarIdentity.cs` | Reuse descriptor creation and add valid fixture-layer setup only where required |
| `Packages/com.alrauna.amuse/Tests/Editor/Build/OptimizerMergeObservation.cs` | Add a test-only NDMF observer with explicit fixture opt-in |
| `Packages/com.alrauna.amuse/Tests/Editor/Build/DaoMergedConsumptionTests.cs` | Add the real SDK-sequence comparison in the disposable project |
| `Packages/com.alrauna.amuse/Tests/Editor/AmuseAvatarOptimizerInformationTests.cs` | Replace registry lookup with observable component survival |
| `Packages/com.alrauna.amuse/Tests/Editor/Alrauna.Amuse.Tests.Editor.asmdef` | Change only references needed by the selected test profile |

Create `.meta` files with Unity for new files. Do not edit source meshes, vendor shaders, or importer configuration outside owned fixture assets.

Production files are read-only during this plan. The later repair plan must cover request selection, shared capture, and field lookup together.

## Task 1: Add the small shared-source regression

Target: `UnityMaterialEvidenceCaptureTests.cs`.
Consumes: `MaterialEvidenceCaptureInput`, family requests, `UnityMaterialEvidenceCapture.Capture`, and `TriangleAlphaClassifier.Classify`.
Produces: A permanent failing behavior test for the shared-texture defect.

Add `using Alrauna.Amuse.Editor.Analysis;` to the test file.

Add these helpers inside the existing test class. Use the existing `NewMaterial` ownership and `TempFolder` teardown.

```csharp
private static Texture2D CreateIsolationTexture()
{
    var path = TempFolder + "/predicate-isolation.asset";
    Assert.That(AssetDatabase.LoadMainAssetAtPath(path), Is.Null);
    var container = ScriptableObject.CreateInstance<
        nadena.dev.ndmf.runtime.SubAssetContainer>();
    AssetDatabase.CreateAsset(container, path);

    var texture = new Texture2D(
        128, 128, TextureFormat.RGBA32, false, true)
    {
        name = "Synthetic (AAO UV Packed)",
        filterMode = FilterMode.Point,
        wrapMode = TextureWrapMode.Clamp
    };
    var pixels = new Color32[128 * 128];
    for (var y = 0; y < 128; y++)
    for (var x = 0; x < 128; x++)
    {
        var alpha = (byte)(x < 42 ? 0 : x < 84 ? 128 : 255);
        pixels[y * 128 + x] = new Color32(255, 255, 255, alpha);
    }
    texture.SetPixels32(pixels);
    texture.Apply(false, false);
    AssetDatabase.AddObjectToAsset(texture, container);
    AssetDatabase.SetMainObject(container, path);
    AssetDatabase.SaveAssetIfDirty(container);
    Assert.That(
        GeneratedTextureAttestation.TryIdentifyProducer(texture, out _),
        Is.True,
        "The fixture must enter the generated capture route.");
    return texture;
}

private static TriangleAlphaOutcome ClassifyIsolationRegion(
    CapturedMaterialEvidence material, float left)
{
    Assert.That(material.TryGetTexture("_MainTex", out var assignment),
        Is.True);
    Assert.That(assignment.Texture.HasAlphaChannel, Is.True,
        "Missing evidence is not this regression.");
    var triangle = TriangleAlphaInput.WithUv0(
        Vector3.zero, Vector3.right, Vector3.up,
        new Vector2(left, 0.1f),
        new Vector2(left + 0.1f, 0.1f),
        new Vector2(left, 0.2f));
    return TriangleAlphaClassifier.Classify(
        triangle,
        assignment.Texture.AlphaChannel[0],
        new AlphaSamplingSettings(AlphaFilterMode.Point, AlphaWrapMode.Clamp),
        AlphaUvEnvelope.Zero);
}
```

Add this regression. It uses separate requests, so it cannot pass through a repair that changes only request unioning.

```csharp
[Test]
public void SharedCutoutTextureDoesNotProveTransparentPartialAlpha()
{
    var texture = CreateIsolationTexture();
    var transparent = NewMaterial(
        "Hidden/Alrauna/AmuseTests/LilToonTransparentConversionTest");
    var cutout = NewMaterial(
        "Hidden/Alrauna/AmuseTests/LilToonCutoutConversionTest");
    transparent.SetTexture("_MainTex", texture);
    transparent.SetFloat("_Cutoff", 0.01f);
    cutout.SetTexture("_MainTex", texture);
    cutout.SetFloat("_Cutoff", 0.25f);
    var transparentInput = new MaterialEvidenceCaptureInput(
        transparent, LilToonTransparentMaterialSemantics.AlphaEvidenceRequest);
    var cutoutInput = new MaterialEvidenceCaptureInput(
        cutout, LilToonCutoutMaterialSemantics.AlphaEvidenceRequest);

    var alone = UnityMaterialEvidenceCapture.Capture(
        new[] { transparentInput })[0];
    Assert.That(ClassifyIsolationRegion(alone, 0.4f),
        Is.EqualTo(TriangleAlphaOutcome.MustRemainTransparent));
    Assert.That(ClassifyIsolationRegion(alone, 0.75f),
        Is.EqualTo(TriangleAlphaOutcome.ProvenOpaque));

    foreach (var reverse in new[] { false, true })
    {
        var inputs = reverse
            ? new[] { cutoutInput, transparentInput }
            : new[] { transparentInput, cutoutInput };
        var captured = UnityMaterialEvidenceCapture.Capture(inputs);
        var subject = captured[reverse ? 1 : 0];
        Assert.That(ClassifyIsolationRegion(subject, 0.05f),
            Is.EqualTo(TriangleAlphaOutcome.MustRemainTransparent));
        Assert.That(ClassifyIsolationRegion(subject, 0.4f),
            Is.EqualTo(TriangleAlphaOutcome.MustRemainTransparent));
        Assert.That(ClassifyIsolationRegion(subject, 0.75f),
            Is.EqualTo(TriangleAlphaOutcome.ProvenOpaque));
    }
}
```

Execution steps:

1. Add the helpers and test without production edits.
2. Run the exact test through the public EditMode runner.
3. Require a failure on the partial-alpha outcome, not shader lookup or capture admission.
4. Record the control outcome and the observed failure.
5. Add a separate test with cutout thresholds `0.25` and `0.75` on one shared texture.
6. Require partial alpha to satisfy only the lower cutoff predicate.
7. Run both material orders and retain their distinct outcome assertions.

The code above is planned permanent test code. The investigation executed its equivalent capture and classifier experiment through reflection. It did not compile or run this new test method.

## Task 2: Exercise the real renderer-wide request union

Target: `AaoMergedConsumptionTests.cs` before enabling AAO.
Consumes: Real installed lilToon shaders, a valid VRChat fixture, and production NDMF processing.
Produces: A mixed-slot regression for the request-union boundary and final mesh application.

Use distinct textures with identical authored alpha regions. Put transparent and cutout materials on different slots of one renderer. This removes shared-source reuse from the first case.

Build a transparent-only control first. Require the partial triangle to remain and the full-alpha triangle to convert. Then add the cutout slot without changing the transparent texture, geometry, or configuration.

Use these existing production entries:

```csharp
var context = AvatarProcessor.ProcessAvatar(
    root, AmbientPlatform.DefaultPlatform);
Assert.That(context, Is.Not.Null);
Assert.That(context.Successful, Is.True,
    "The fixture must complete the real build.");
```

Do not call `MaterialEvidenceRequest.Combine` directly as the permanent renderer regression. The caller must select and combine requests itself. A hand-built combined request only characterizes the internal mechanism.

Execution steps:

1. Replace the current zero-cutoff assumption with synthetic positive cutoff values.
2. Build the mixed-slot fixture with no optimizer component.
3. Compare every output triangle with the independent oracle from Task 3.
4. Require the partial triangle to fail on the pre-fix code.
5. Reverse the material-slot order and repeat.
6. Share the source texture and repeat to expose downstream field lookup.
7. Add a material alternative that uses a different predicate on the same texture.
8. Require the slot to remain safe in every admitted material state.

The unit tests must not duplicate production request routing inside a fake capturer. Use real production capture here. Reserve verified source-identity substitution for core tests that do not claim vendor integration.

## Task 3: Replace the fixture geometry and oracle

Target: `AaoMergedConsumptionTests.cs` and `FixtureAvatarIdentity.cs`.
Consumes: The synthetic alpha regions and existing fixture methods.
Produces: A valid AAO merge input and complete per-triangle output accounting.

Change `CreateTexturedRenderer` to supply actual skinning. Use the same root transform as the bone in the smallest fixture. Give each triangle distinct positions. Retain the larger scene-shaped rig for Task 6.

Use these values after assigning the fixture vertices and indices:

```csharp
mesh.bindposes = new[] { Matrix4x4.identity };
var weights = new BoneWeight[mesh.vertexCount];
var normals = new Vector3[mesh.vertexCount];
for (var i = 0; i < mesh.vertexCount; i++)
{
    weights[i] = new BoneWeight { boneIndex0 = 0, weight0 = 1f };
    normals[i] = Vector3.forward;
}
mesh.boneWeights = weights;
mesh.normals = normals;
mesh.RecalculateBounds();
renderer.bones = new[] { root.transform };
renderer.rootBone = root.transform;
renderer.probeAnchor = root.transform;
renderer.localBounds = mesh.bounds;
```

For triangle number `i`, place its vertices at `(4*i, 0, 0)`, `(4*i+1, 0, 0)`, and `(4*i, 1, 0)`. Partition those triangles among the source renderers. Keep source transforms at identity for the small fixture.

Create an explicit controller state with a real synthetic clip. Attach it through the VRChat descriptor layer that NDMF reads. Do not rely on an empty root controller or default SDK layers that the fixture does not control.

The oracle must perform these operations:

1. Store each authored triangle as an unordered triple of root-space positions and an expected label.
2. Read every output triangle with its actual material slot.
3. Transform its vertices to the same root space.
4. Match it to exactly one authored triangle within a fixed small positional tolerance.
5. Reject missing, repeated, and unexpected matches.
6. For each expected transparent triangle, require a non-AMUSE-opaque output assignment.
7. For each positive opaque control, require an applied opaque assignment.
8. After DAO, inspect the final shader and render state rather than only AMUSE clone identity.

Do not use UV coordinates or generated object names as triangle identity. Keep the matcher fixture-local. Do not add a production provenance system.

Replace `SamplesOnlyOpaqueTexels` and the lower-bound triangle count. Remove the unsuccessful-build exception in the harness. Replace the material availability probes with outcome checks or include their conditions as fixture preconditions.

## Task 4: Prove actual AAO merging and texture transformation

Target: `AaoMergedConsumptionTests.cs` and new test-only `OptimizerMergeObservation.cs`.
Consumes: The valid fixture and its oracle.
Produces: Required AAO cases with non-vacuous phase observations.

Register a test-only NDMF observer with a fixture marker. With no marker, the observer must do nothing. Keep the marker and observer inside the excluded test assembly.

Use the real phase constraints:

```csharp
InPhase(BuildPhase.Optimizing)
    .AfterPlugin("com.anatawa12.avatar-optimizer");
InPhase(BuildPhase.PlatformFinish)
    .AfterPlugin("com.alrauna.amuse");
```

Attach observation callbacks to those sequences. At the first boundary, record only synthetic geometry, material modes, texture references, UVs, and renderer partitions. At the second boundary, run the triangle oracle.

Configure the installed AAO component through serialized fixture fields. The component type is public, but the vendor does not promise a scripting API for its configuration.

```csharp
var serialized = new SerializedObject(traceAndOptimize);
var merge = serialized.FindProperty("mergeSkinnedMesh");
var textures = serialized.FindProperty("optimizeTexture");
Assert.That(merge, Is.Not.Null);
Assert.That(textures, Is.Not.Null);
merge.boolValue = true;
textures.boolValue = false;
serialized.ApplyModifiedPropertiesWithoutUndo();
```

Pin the package version before using those fields. A missing field fails the version-specific fixture instead of selecting a default.

Run three controlled cases:

1. Disable merging and keep the other relevant configuration fixed.
2. Enable merging and disable texture optimization.
3. Enable merging and texture optimization.

For the merge cases, require formerly separate geometry identities to appear in one renderer before AMUSE. Require both cutout and transparent modes in that merged renderer.

For the texture case, use small disjoint UV islands that satisfy AAO packing limits. Keep each island inside one tile. Require a changed texture reference or changed UVs, then establish the intended generated capture route.

Start the real packing fixture with 512-by-512 textures. Require the packed output to remain at or above the configured minimum texture size. Put the coarse-mip witness in a level that the configured capture policy actually consults.

AAO packing rejects islands whose padded size prevents a smaller atlas. If packing does not occur, reduce synthetic island extent. Do not weaken the transformation assertion.

Run the partial-alpha, interior-hole, and consulted-coarse-mip cases. Require the real AAO case to fail on an incorrect triangle outcome before any production fix. A fixture-admission failure is not the required RED evidence.

## Task 5: Compare the real DAO and combined pipelines

Target: New `DaoMergedConsumptionTests.cs` in the required integration profile.
Consumes: The public fixture, oracle, and phase observations.
Produces: Source-derived order confirmed by actual callback execution.

Run this task only in a disposable public integration project. Use installed local package versions when available. Ask before dependency installation or network restoration. Do not clone private project assets.

The installed NDMF Apply on Play code invokes the SDK callback dispatcher. Use that same installed entry rather than calling only `Optimize()`:

```csharp
VRC.SDKBase.Editor.BuildPipeline.VRCBuildPipelineCallbacks
    .OnPreprocessAvatar(buildCopy);
```

Read its live signature before implementation. Require the dispatcher success result when the installed signature returns one. Use the existing NDMF observations to establish AMUSE completion and inspect the output after dispatch for DAO changes.

Match the observed DAO configuration: mesh merging enabled, static property writes disabled, different-property material merging disabled, and same-dimension texture merging disabled. Explicitly establish the play-mode enable flag for the play-mode case.

Execution steps:

1. Build an AMUSE-only control and run the complete triangle oracle.
2. Run the SDK sequence with DAO and require actual geometry merging.
3. Run the SDK sequence with AAO and DAO together.
4. Compare the oracle after AMUSE and again after DAO.
5. Require expected callback order in the Modular Avatar profile.
6. Exercise the play-mode path separately from EditMode SDK preprocessing.
7. Make sure that no upload or publication operation runs.

Do not treat the no-Modular-Avatar equal-order case as supported without separate evidence. Do not add a production ordering adapter as part of this test task.

## Task 6: Add the scene-shaped case and acceptance controls

Targets: The existing fixture and both integration test classes.
Consumes: The small causal cases from Tasks 1–5.
Produces: Coverage of the observed complexity without private content.

Add these independent fixture extensions:

- A small multi-bone rig with nontrivial weights and bind poses.
- Blend-shape and visibility transitions in committed controllers.
- Inactive geometry and several material slots.
- Shared material references and separate main and mask textures.
- A saturated multiply mask that leaves main alpha unchanged.
- A non-inert mask that changes the expected alpha outcome.
- Unreadable streaming imports for synthetic texture sources.
- A generated texture with actual persisted producer recognition.
- One synthetic 72,000-vertex mesh with 32-bit indices.
- A material-swap case with different predicate requirements.

Use one test per distinct failure mechanism. Do not create a Cartesian product of every configuration.

Add source-preservation checks before processing the build copy. Compare mesh channels and indices, material properties, importer configuration, and controller curves afterward. Make sure that teardown removes only test-owned objects.

Replace the registry-only AAO adapter test with an actual build test. The AMUSE component must survive until its pass, including when the component's enabled flag is false. Separately test the serialized AMUSE disable control. Do not infer survival from registry registration.

Make sure that the product loads with AAO absent. Keep required optimizer-profile dependency checks distinct from the core test profile.

## Task 7: Produce the RED evidence and repair brief

This task is an approval boundary, not authorization to edit production.

Run focused tests through Unity Test Runner. Example filter for the new core regression:

```json
{
  "mode": "EditMode",
  "test_names": [
    "Alrauna.Amuse.Tests.Editor.Host.UnityMaterialEvidenceCaptureTests.SharedCutoutTextureDoesNotProveTransparentPartialAlpha"
  ],
  "include_failed_tests": true,
  "include_details": true
}
```

Use explicit test names for required integration runs. Report counts from the completed result, not the discovered assembly total. If the tool returns no final result object, report that limit and obtain the runner result before claiming a pass.

The evidence packet must contain:

1. A shared-source capture failure with a passing isolated control.
2. A real mixed-renderer failure with distinct source textures.
3. An actual AAO merge failure on the same triangle contract.
4. A successful fixture build for each claimed behavioral failure.
5. The DAO comparison with its actual execution order.
6. Source-preservation and teardown results.
7. Exact installed public tool versions and the exercised build path.

Prepare a separate production repair plan after these results. Its scope must include the following existing boundaries:

- `UnityAnimationEvidenceCapture` and `UnityMaterialSemantics` must preserve the per-material capture contract.
- `UnityMaterialEvidenceCapture` must not reuse a thresholded field under a different predicate.
- `UnityRendererAlphaAnalysis.GatherAlphaFields`, the build provider, and `AdmittedMaterialStates.ResolveSlot` must not collapse distinct fields.

Before changing an exported symbol, use LSP references when available. Update all callers and test seams in the approved repair. Preserve the existing distinction between alpha relevance and conversion-only facts.

Do not prescribe a new general predicate representation from these tests alone. If the repair needs a changed semantic contract, present that decision for approval.

## After the approved repair

The repair implementation must turn the same RED tests green. Do not replace their expected outcomes or weaken merge and positive-control assertions.

Then run the focused integration matrix and both full EditMode assemblies. Run the supported SDK and play-mode paths. Require observed results rather than a compile-only claim.

Finally, inspect the original private visual scenario with user authorization. Preserve the source scene and assets. Record only sanitized results. The public tests do not replace this final visual acceptance.

## Stop conditions

Stop production work and return evidence when any of these conditions holds:

- The real AAO fixture does not merge or fails before classification.
- The public failure differs from the private observation in a material way.
- A required source or test environment cannot be obtained through authorized means.
- Correct isolation requires a changed transformation contract or a new subsystem.
- DAO execution would delete output in a non-disposable project.
- A required test is skipped or returns zero executed cases.
- The selected editor path does not exactly match the intended project.

The investigation already identified an environment risk: the public console contains native-detour and audio-plugin errors. Diagnose those separately if they block valid builds. Do not add AMUSE production workarounds to hide them.

## Expected final report

Report the changed files, the exact failing and passing behaviors, observed counts, source-preservation results, and remaining manual acceptance. Distinguish fixture repair from the generic production prerequisite. State that no private assets or identifiers entered the repository.
