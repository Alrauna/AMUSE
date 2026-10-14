# Edit-Time Streaming Texture In-Memory Decoding and Accurate Deferred Material Status Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Eliminate false material refusals in the inspector preview by enabling in-memory streaming texture decoding via `SourceImageAlphaReader` and accurately classifying deferred build-time materials with preserved submesh polygon counts.

**Architecture:** Add a scoped `DisallowDiskFallbackScope` RAII guard in `UnityStreamingTextureEvidence` to permit in-memory decoding while strictly blocking disk writes. In `AlphaSeparationDryRun`, remove `InterceptStreamingTextures`, wrap capture in `DisallowDiskFallbackScope`, extract submesh polygon counts directly from the shared mesh, and route supported build-time materials to a new `Deferred` status sentence in `RowText`.

**Tech Stack:** C# against Unity 2022.3 APIs, NUnit (Unity Test Framework, EditMode), NDMF.

**Spec:** `docs/superpowers/specs/2026-10-14-preview-material-refusals-streaming-design.md`

## Global Constraints

- Dry run purity: zero disk writes, zero asset creations, zero scene object mutations.
- ASD-STE100 / Simple English: short active sentences, one idea per sentence, no semicolons, no contractions.
- Privacy rules: zero absolute paths, zero private avatar or asset names, zero ports or machine identities. Use relative paths only.
- Fail closed: unresolvable or missing assets defer safely to build time without guessing alpha.
- Attestation checks: locked materials defer only when both `RecognizedLockedIdentity` and `OriginalShaderAttested` are true.

---

### Task 1: Scoped Disk Fallback Guard in `UnityStreamingTextureEvidence`

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/Host/UnityStreamingTextureEvidence.cs`
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Host/UnityStreamingTextureEvidenceTests.cs`

**Interfaces:**
- Produces:
  - `UnityStreamingTextureEvidence.AllowDiskFallback`: internal static `bool` property (default `true`).
  - `UnityStreamingTextureEvidence.DisallowDiskFallbackScope`: internal `readonly struct` implementing `IDisposable`.
  - `UnityStreamingTextureEvidence.DisallowDiskFallback()`: internal static method returning `DisallowDiskFallbackScope`.

- [ ] **Step 1: Write the failing test for `DisallowDiskFallbackScope` and disk write prevention**

In `Packages/com.alrauna.amuse/Tests/Editor/Host/UnityStreamingTextureEvidenceTests.cs`:
```csharp
        [Test]
        public void DisallowDiskFallbackScopeRestoresStateAndBlocksClone()
        {
            Assert.That(UnityStreamingTextureEvidence.AllowDiskFallback, Is.True);
            using (UnityStreamingTextureEvidence.DisallowDiskFallback())
            {
                Assert.That(UnityStreamingTextureEvidence.AllowDiskFallback, Is.False);
            }
            Assert.That(UnityStreamingTextureEvidence.AllowDiskFallback, Is.True);
        }

        [Test]
        public void DisallowingDiskFallbackReturnsFalseWhenSourceImageFailsWithoutWritingAssets()
        {
            var texture = ImportExrStreamingBandTexture("unreadable_streaming_for_guard");
            try
            {
                using (UnityStreamingTextureEvidence.DisallowDiskFallback())
                {
                    var ok = UnityStreamingTextureEvidence.TryCapture(
                        texture,
                        TextureChannel.Alpha,
                        1.0f,
                        AlphaPolicyBounds.Inert,
                        out var chain);
                    Assert.That(ok, Is.False);
                    Assert.That(chain, Is.Null);
                }
                Assert.That(
                    AssetDatabase.IsValidFolder("Assets/Amuse.StreamingReadback"),
                    Is.False,
                    "disallowing disk fallback must not create the temporary streaming folder");
            }
            finally
            {
                AssetDatabase.DeleteAsset(AssetDatabase.GetAssetPath(texture));
            }
        }
```

- [ ] **Step 2: Run test to verify it fails**

Verify compilation failure that `AllowDiskFallback` and `DisallowDiskFallback` do not exist.

- [ ] **Step 3: Implement `AllowDiskFallback` and `DisallowDiskFallbackScope` in `UnityStreamingTextureEvidence`**

In `Packages/com.alrauna.amuse/Editor/Host/UnityStreamingTextureEvidence.cs`:
```csharp
        internal static bool AllowDiskFallback { get; set; } = true;

        internal readonly struct DisallowDiskFallbackScope : IDisposable
        {
            private readonly bool _previous;
            internal DisallowDiskFallbackScope(bool previous) => _previous = previous;
            public void Dispose() => AllowDiskFallback = _previous;
        }

        internal static DisallowDiskFallbackScope DisallowDiskFallback()
        {
            var prev = AllowDiskFallback;
            AllowDiskFallback = false;
            return new DisallowDiskFallbackScope(prev);
        }
```
And insert the guard before line 103 in `TryCapture`:
```csharp
            if (!AllowDiskFallback)
            {
                return false;
            }
```

- [ ] **Step 4: Run test to verify it passes**

Observe both tests passing in `UnityStreamingTextureEvidenceTests`.

- [ ] **Step 5: Commit**

```bash
git add Packages/com.alrauna.amuse/Editor/Host/UnityStreamingTextureEvidence.cs Packages/com.alrauna.amuse/Tests/Editor/Host/UnityStreamingTextureEvidenceTests.cs
git commit -m "feat: add scoped disk fallback guard to UnityStreamingTextureEvidence"
```

---

### Task 2: Deferred Slot Sentence Builder in `RowText`

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/DryRun/MaterialTransformationRow.cs`
- Test: `Packages/com.alrauna.amuse/Tests/Editor/DryRun/MaterialTransformationRowTests.cs`

**Interfaces:**
- Produces:
  - `RowText.DeferredSlotSentence(int total)`: internal static method returning string.

- [ ] **Step 1: Write the failing test for `DeferredSlotSentence`**

In `Packages/com.alrauna.amuse/Tests/Editor/DryRun/MaterialTransformationRowTests.cs`:
```csharp
        [Test]
        public void DeferredSlotSentenceFormatsWithTotalCount()
        {
            var sentence = RowText.DeferredSlotSentence(1234);
            Assert.That(
                sentence,
                Is.EqualTo("Deferred: 0 of out 1234 polygons may be moved (evaluates at build time)"));
        }

        [Test]
        public void DeferredSlotSentenceHandlesZeroCount()
        {
            var sentence = RowText.DeferredSlotSentence(0);
            Assert.That(
                sentence,
                Is.EqualTo("Deferred: 0 of out 0 polygons may be moved (evaluates at build time)"));
        }
```

- [ ] **Step 2: Run test to verify it fails**

Verify compilation failure that `RowText.DeferredSlotSentence` does not exist.

- [ ] **Step 3: Implement `DeferredSlotSentence` in `MaterialTransformationRow.cs`**

In `Packages/com.alrauna.amuse/Editor/DryRun/MaterialTransformationRow.cs`:
```csharp
        internal static string DeferredSlotSentence(int total)
        {
            return "Deferred: 0 of out " +
                total.ToString(CultureInfo.InvariantCulture) +
                " polygons may be moved (evaluates at build time)";
        }
```

- [ ] **Step 4: Run test to verify it passes**

Observe tests passing in `MaterialTransformationRowTests`.

- [ ] **Step 5: Commit**

```bash
git add Packages/com.alrauna.amuse/Editor/DryRun/MaterialTransformationRow.cs Packages/com.alrauna.amuse/Tests/Editor/DryRun/MaterialTransformationRowTests.cs
git commit -m "feat: add deferred slot sentence builder to RowText"
```

---

### Task 3: In-Memory Streaming Decoding, Submesh Extraction, and Deferred Slot Routing in `AlphaSeparationDryRun`

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/DryRun/AlphaSeparationDryRun.cs`
- Test: `Packages/com.alrauna.amuse/Tests/Editor/DryRun/AlphaSeparationDryRunTests.cs`

**Interfaces:**
- Consumes:
  - `UnityStreamingTextureEvidence.DisallowDiskFallback()`
  - `RowText.DeferredSlotSentence(int total)`
  - `RowText.RefusedSlotSentence()`
  - `LockedMaterialIdentity.RecognizedLockedIdentity(Material)`
  - `LockedMaterialIdentity.OriginalShaderAttested(Material)`

- [ ] **Step 1: Write the failing tests in `AlphaSeparationDryRunTests.cs`**

In `Packages/com.alrauna.amuse/Tests/Editor/DryRun/AlphaSeparationDryRunTests.cs`:
```csharp
        [Test]
        public void StreamingPngTextureDecodesInMemoryAndDoesNotRefuse()
        {
            var fixture = NewComputeFixture(255, 0.5f, streaming: true);
            var dryRun = NewDryRun(
                fixture, null, FixtureSelector(), FixtureCapturer());
            Run(dryRun);

            var rows = SlotRows(dryRun);
            Assert.That(rows, Has.Count.EqualTo(1));
            Assert.That(rows[0].Sentence, Does.Not.Contain("Refused"));
            Assert.That(
                rows[0].Sentence,
                Does.Contain("Splittable").Or.Contain("Convertable"));
            Assert.That(
                dryRun.AvatarRows.Any(row => row.Sentence.Contains("streaming mipmaps")),
                Is.False,
                "note must not be emitted when streaming texture decodes in memory");
        }

        [Test]
        public void AttestedLockedMaterialReportsDeferredSentenceWithPolygonCount()
        {
            var root = _scope.Track(NewAvatarRoot("amuse-dry-run-locked-attested"));
            var optimizer = AddOptimizer(root);
            var originalShader = Shader.Find(".poiyomi/Poiyomi Toon");
            var original = _scope.Track(new Material(originalShader));
            var locked = _scope.Track(new Material(original));
            locked.shader = Shader.Find("Hidden/Locked/.poiyomi/Poiyomi Toon");
            locked.SetFloat("_ShaderOptimizerEnabled", 1f);
            locked.SetOverrideTag("OriginalShader", ".poiyomi/Poiyomi Toon");
            var mesh = OneTriangleMesh();
            AddSkinnedRenderer(root, mesh, locked);

            var dryRun = NewDryRun(
                new Fixture
                {
                    Root = root,
                    Optimizer = optimizer,
                    Renderer = null
                },
                null,
                FixtureSelector(),
                FixtureCapturer());
            Run(dryRun);

            var rows = SlotRows(dryRun);
            Assert.That(rows, Has.Count.EqualTo(1));
            Assert.That(
                rows[0].Sentence,
                Is.EqualTo("Deferred: 0 of out 1 polygons may be moved (evaluates at build time)"));
            Assert.That(
                dryRun.RendererRows.Any(
                    row => row.Kind == MaterialTransformationRowKind.Renderer),
                Is.False,
                "renderer refusal box must be suppressed when all slots are deferred");
        }

        [Test]
        public void TrulyUnsupportedShaderReportsRefusedSentence()
        {
            var root = _scope.Track(NewAvatarRoot("amuse-dry-run-unsupported"));
            var optimizer = AddOptimizer(root);
            var shader = Shader.Find("Hidden/InternalErrorShader");
            var material = _scope.Track(new Material(shader));
            AddSkinnedRenderer(root, OneTriangleMesh(), material);

            var dryRun = NewDryRun(
                new Fixture
                {
                    Root = root,
                    Optimizer = optimizer,
                    Renderer = null
                },
                null,
                FixtureSelector(),
                FixtureCapturer());
            Run(dryRun);

            var rows = SlotRows(dryRun);
            Assert.That(rows, Has.Count.EqualTo(1));
            Assert.That(
                rows[0].Sentence,
                Is.EqualTo("Refused: AMUSE currently does not support this material"));
        }
```
Update the existing test `StreamingTextureProducesNoteAndSkipsRead`:
Rename to `StreamingTextureDecodesInMemoryWhenReadable` and update assertions to reflect that authoring PNG streaming textures decode in memory.

- [ ] **Step 2: Run test to verify it fails**

As of 2026-10-14, observe test failure that streaming textures refuse and locked materials report `Refused` with polygon counts of `-1`.

- [ ] **Step 3: Implement in `AlphaSeparationDryRun.cs`**

1. In `RealCapturer()`, remove `InterceptStreamingTextures` so `inner` capture is called under `DisallowDiskFallbackScope`:
```csharp
        private ClosedAlphaMaterialCapturer RealCapturer(Renderer renderer)
        {
            var inner = _capturer ?? DefaultInnerCapturer();
            return (
                IReadOnlyList<Material> materials,
                IReadOnlyList<CapturedAlphaMaterialFamily> families,
                MaterialEvidenceRequest request,
                AlphaPolicyBounds bounds,
                out ClosedAlphaCaptureOutcome outcome) =>
            {
                var effective = EffectiveMaterialMaterialization
                    .MaterializeAdmitted(renderer, materials, out var clones);
                try
                {
                    using (UnityStreamingTextureEvidence.DisallowDiskFallback())
                    {
                        return inner(
                            effective, families, request, bounds, out outcome);
                    }
                }
                finally
                {
                    foreach (var clone in clones)
                    {
                        Object.DestroyImmediate(clone);
                    }
                }
            };
        }
```

2. Add `SubmeshPolygonCount`:
```csharp
        private static int SubmeshPolygonCount(Renderer renderer, int slotIndex)
        {
            Mesh mesh = null;
            if (renderer is SkinnedMeshRenderer smr)
            {
                mesh = smr.sharedMesh;
            }
            else if (renderer is MeshRenderer && renderer.TryGetComponent<MeshFilter>(out var filter))
            {
                mesh = filter.sharedMesh;
            }

            if (mesh == null || slotIndex < 0 || slotIndex >= mesh.subMeshCount)
            {
                return -1;
            }

            if (mesh.GetTopology(slotIndex) != MeshTopology.Triangles)
            {
                return -1;
            }

            return (int)(mesh.GetIndexCount(slotIndex) / 3);
        }
```

3. Add `IsSlotDeferred`:
```csharp
        private static bool IsSlotDeferred(
            Renderer renderer,
            int slotIndex,
            SlotResolutionResult slotResult,
            CapturedMaterialSlotEvidence slotEvidence)
        {
            if (renderer == null || renderer.sharedMaterials == null)
            {
                return false;
            }

            if (slotIndex < 0 || slotIndex >= renderer.sharedMaterials.Length)
            {
                return false;
            }

            var material = renderer.sharedMaterials[slotIndex];
            if (material == null)
            {
                return false;
            }

            if (LockedMaterialIdentity.RecognizedLockedIdentity(material) &&
                LockedMaterialIdentity.OriginalShaderAttested(material))
            {
                return true;
            }

            if (HasStreamingTexture(material))
            {
                return true;
            }

            return false;
        }

        private static bool HasStreamingTexture(Material material)
        {
            if (material == null)
            {
                return false;
            }

            var count = ShaderUtil.GetPropertyCount(material.shader);
            for (var i = 0; i < count; i++)
            {
                if (ShaderUtil.GetPropertyType(material.shader, i) !=
                    ShaderUtil.ShaderPropertyType.TexEnv)
                {
                    continue;
                }

                var propName = ShaderUtil.GetPropertyName(material.shader, i);
                var tex = material.GetTexture(propName);
                if (IsStreamingMipmap(tex))
                {
                    return true;
                }
            }

            return false;
        }
```

4. Update `BuildRows` and `RefusedSlotRow`:
Pass `Renderer renderer` to `BuildRows` and `RefusedSlotRow`.
In `RefusedSlotRow`:
```csharp
        private static MaterialTransformationRow RefusedSlotRow(
            Renderer renderer,
            string rendererPath,
            string rendererTypeName,
            SlotResolutionResult result,
            CapturedMaterialSlotEvidence slot,
            int slotIndex,
            string rendererName)
        {
            var totalTriangles = SubmeshPolygonCount(renderer, slotIndex);
            if (IsSlotDeferred(renderer, slotIndex, result, slot))
            {
                return MaterialTransformationRow.Slot(
                    rendererPath,
                    rendererTypeName,
                    slotIndex,
                    0,
                    totalTriangles,
                    RowText.DeferredSlotSentence(totalTriangles));
            }

            return MaterialTransformationRow.Slot(
                rendererPath,
                rendererTypeName,
                slotIndex,
                -1,
                totalTriangles,
                RowText.RefusedSlotSentence());
        }
```

5. In `ComputeRows()`, update the early refusal block:
Check whether all unresolved slots on the renderer are deferred.
If all are deferred, do not insert `RendererRefusedRow`.
If any slot is truly refused, insert `RendererRefusedRow`.

6. In `Probe()`:
Do not unconditionally add `NoteKind.StreamingMipmaps`.
Emit `NoteKind.StreamingMipmaps` only when a streaming texture fails in-memory decode.

- [ ] **Step 4: Run test to verify it passes**

Observe all tests passing in `AlphaSeparationDryRunTests`.

- [ ] **Step 5: Commit**

```bash
git add Packages/com.alrauna.amuse/Editor/DryRun/AlphaSeparationDryRun.cs Packages/com.alrauna.amuse/Tests/Editor/DryRun/AlphaSeparationDryRunTests.cs
git commit -m "feat: enable in-memory streaming decode and deferred slot reporting in dry run"
```

---

### Task 4: Verify AmuseAvatarOptimizerEditor Rendering and Test Suite

**Files:**
- Modify: `Packages/com.alrauna.amuse/Tests/Editor/AmuseAvatarOptimizerEditorTests.cs`
- Verify: full `Alrauna.Amuse.Tests.Editor` suite

- [ ] **Step 1: Add unit test in `AmuseAvatarOptimizerEditorTests.cs`**

```csharp
        [Test]
        public void DrawTransformationRowRendersDeferredSlotRow()
        {
            var row = MaterialTransformationRow.Slot(
                "Body",
                "UnityEngine.SkinnedMeshRenderer",
                0,
                0,
                100,
                RowText.DeferredSlotSentence(100));

            var root = _scope.Track(new GameObject("amuse-test-editor-root"));
            var renderer = root.AddComponent<SkinnedMeshRenderer>();
            renderer.sharedMaterials = new[] { _scope.Track(new Material(Shader.Find("Hidden/InternalErrorShader"))) };

            Assert.DoesNotThrow(() =>
            {
                CallDrawTransformationRow(row, root.transform);
            });
        }
```

- [ ] **Step 2: Run test to verify it passes**

Verify editor tests pass.

- [ ] **Step 3: Commit**

```bash
git add Packages/com.alrauna.amuse/Tests/Editor/AmuseAvatarOptimizerEditorTests.cs
git commit -m "test: verify inspector rendering of deferred slot rows"
```
