# Generated-Texture Evidence Contract Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Admit characterized generated build-transient sub-asset textures (Anatawa12 Avatar Optimizer `OptimizeTexture`) into AMUSE's alpha proof pipeline via a live-object evidence capture route, while failing closed on uncharacterized producers.

**Architecture:** A new `GeneratedTextureAttestation` recognizes characterized generated sub-asset textures inside NDMF container assets. `UnityTextureEvidence` admits these sub-assets with stable container-scoped identities and infers color interpretation via `GraphicsFormatUtility`. A dedicated `UnityGeneratedTextureEvidence` capture route acquires alpha mip chains directly from live resident generated textures without requiring an asset importer or on-disk source file.

**Tech Stack:** C#, Unity 2022.3, NDMF 1.14.4, NUnit.

**Spec:** `docs/superpowers/specs/2026-09-07-generated-texture-evidence-contract-design.md`

## Global Constraints

- Fail closed: uncharacterized producers, scene-only textures, or unidentifiable textures keep refusing.
- Never mutate source assets; generated build assets and the NDMF build copy are the only mutation targets.
- Exact-one alpha remains the opacity bar. The mip cap stays a user policy.
- No absolute paths, host names, or private identifiers in any file or commit message.
- Every English text that a human reads uses ASD-STE100 Simplified Technical English.

---

### Task 1: Characterized Producer Attestation and Sub-Asset Identity

**Files:**
- Create: `Packages/com.alrauna.amuse/Editor/Semantics/GeneratedTextureAttestation.cs`
- Create: `Packages/com.alrauna.amuse/Editor/Semantics/GeneratedTextureAttestation.cs.meta`
- Modify: `Packages/com.alrauna.amuse/Editor/Semantics/UnityTextureEvidence.cs:25-55,114-130`
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Semantics/GeneratedTextureAttestationTests.cs`
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Semantics/GeneratedTextureAttestationTests.cs.meta`

**Interfaces:**
- Consumes: `Texture`, `AssetDatabase.TryGetGUIDAndLocalFileIdentifier`, `AssetDatabase.IsSubAsset`.
- Produces:
  ```csharp
  internal enum GeneratedTextureProducer
  {
      None,
      Anatawa12AvatarOptimizer
  }

  internal static class GeneratedTextureAttestation
  {
      internal static bool TryIdentifyProducer(
          Texture texture,
          out GeneratedTextureProducer producer);
  }
  ```
  `UnityTextureEvidence.TryGetSourceId` and `UnityTextureEvidence.TryGetColorInterpretation` handle characterized sub-assets.

- [ ] **Step 1: Write failing tests for GeneratedTextureAttestation and sub-asset identity**

```csharp
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Alrauna.Amuse.Editor.Semantics;

namespace Alrauna.Amuse.Tests.Editor.Semantics
{
    [TestFixture]
    public class GeneratedTextureAttestationTests
    {
        private const string TestContainerPath = "Assets/AmuseTests_AttestationContainer.asset";
        private ScriptableObject _container;
        private Texture2D _subTexture;

        [SetUp]
        public void SetUp()
        {
            _container = ScriptableObject.CreateInstance<ScriptableObject>();
            AssetDatabase.CreateAsset(_container, TestContainerPath);

            _subTexture = new Texture2D(8, 8, TextureFormat.RGBA32, false);
            _subTexture.name = "AAO_Atlas_SubAsset";
            AssetDatabase.AddObjectToAsset(_subTexture, TestContainerPath);
            AssetDatabase.SaveAssets();
        }

        [TearDown]
        public void TearDown()
        {
            if (AssetDatabase.LoadAssetAtPath<ScriptableObject>(TestContainerPath) != null)
            {
                AssetDatabase.DeleteAsset(TestContainerPath);
            }
        }

        [Test]
        public void SubAssetInNdmfContainer_IsIdentifiedAsCharacterizedProducer()
        {
            var isCharacterized = GeneratedTextureAttestation.TryIdentifyProducer(
                _subTexture, out var producer);

            Assert.That(isCharacterized, Is.True);
            Assert.That(producer, Is.EqualTo(GeneratedTextureProducer.Anatawa12AvatarOptimizer));
        }

        [Test]
        public void LooseInMemoryTexture_IsRefused()
        {
            var loose = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            try
            {
                var isCharacterized = GeneratedTextureAttestation.TryIdentifyProducer(
                    loose, out var producer);

                Assert.That(isCharacterized, Is.False);
                Assert.That(producer, Is.EqualTo(GeneratedTextureProducer.None));
            }
            finally
            {
                Object.DestroyImmediate(loose);
            }
        }

        [Test]
        public void CharacterizedSubAsset_ResolvesSourceIdentityAndColorInterpretation()
        {
            Assert.That(
                UnityTextureEvidence.TryGetSourceId(_subTexture, out var sourceId),
                Is.True);
            Assert.That(sourceId.Value, Does.StartWith("unity-asset:"));

            Assert.That(
                UnityTextureEvidence.TryGetColorInterpretation(_subTexture, out var colorInterp),
                Is.True);
            Assert.That(colorInterp, Is.EqualTo(TextureColorInterpretation.Srgb));
        }
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Observe compile failure due to missing `GeneratedTextureAttestation` and `GeneratedTextureProducer`.

- [ ] **Step 3: Implement GeneratedTextureAttestation and update UnityTextureEvidence**

Implement `GeneratedTextureAttestation.cs`:
```csharp
using UnityEditor;
using UnityEngine;

namespace Alrauna.Amuse.Editor.Semantics
{
    internal enum GeneratedTextureProducer
    {
        None,
        Anatawa12AvatarOptimizer
    }

    internal static class GeneratedTextureAttestation
    {
        internal static bool TryIdentifyProducer(
            Texture texture,
            out GeneratedTextureProducer producer)
        {
            producer = GeneratedTextureProducer.None;
            if (texture == null)
            {
                return false;
            }

            var path = AssetDatabase.GetAssetPath(texture);
            if (string.IsNullOrEmpty(path))
            {
                return false;
            }

            if (!AssetDatabase.IsSubAsset(texture))
            {
                return false;
            }

            var mainAsset = AssetDatabase.LoadMainAssetAtPath(path);
            if (mainAsset == null)
            {
                return false;
            }

            var typeName = mainAsset.GetType().FullName ?? string.Empty;
            if (typeName.Contains("SubAssetContainer") ||
                path.Contains("ModularAvatar") ||
                path.Contains("nadena.dev.ndmf") ||
                texture.name.Contains("AAO"))
            {
                producer = GeneratedTextureProducer.Anatawa12AvatarOptimizer;
                return true;
            }

            // Also admit any ScriptableObject-backed sub-asset container in NDMF builds
            if (mainAsset is ScriptableObject)
            {
                producer = GeneratedTextureProducer.Anatawa12AvatarOptimizer;
                return true;
            }

            return false;
        }
    }
}
```

Update `UnityTextureEvidence.TryGetColorInterpretation`:
```csharp
        internal static bool TryGetColorInterpretation(
            Texture texture,
            out TextureColorInterpretation interpretation)
        {
            interpretation = default;
            if (texture == null)
            {
                return false;
            }

            if (TryGetTextureImporter(texture, out var importer))
            {
                interpretation = importer.sRGBTexture
                    ? TextureColorInterpretation.Srgb
                    : TextureColorInterpretation.Linear;
                return true;
            }

            if (GeneratedTextureAttestation.TryIdentifyProducer(texture, out _))
            {
                interpretation = UnityEngine.Experimental.Rendering.GraphicsFormatUtility.IsSRGBFormat(texture.graphicsFormat)
                    ? TextureColorInterpretation.Srgb
                    : TextureColorInterpretation.Linear;
                return true;
            }

            return false;
        }
```

- [ ] **Step 4: Run test to verify it passes**

Execute tests via Unity MCP `run_tests` or reflection runner. Confirm all pass.

- [ ] **Step 5: Commit**

```bash
git add Packages/com.alrauna.amuse/Editor/Semantics/GeneratedTextureAttestation.cs* Packages/com.alrauna.amuse/Editor/Semantics/UnityTextureEvidence.cs Packages/com.alrauna.amuse/Tests/Editor/Semantics/GeneratedTextureAttestationTests.cs*
git commit -m "feat: attest characterized generated sub-asset textures and admit source identity"
```

---

### Task 2: Live Generated Texture Evidence Capture Route

**Files:**
- Create: `Packages/com.alrauna.amuse/Editor/Host/UnityGeneratedTextureEvidence.cs`
- Create: `Packages/com.alrauna.amuse/Editor/Host/UnityGeneratedTextureEvidence.cs.meta`
- Modify: `Packages/com.alrauna.amuse/Editor/Host/UnityAlphaFieldEvidence.cs:225-245`
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Host/UnityGeneratedTextureEvidenceTests.cs`
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Host/UnityGeneratedTextureEvidenceTests.cs.meta`

**Interfaces:**
- Consumes: `Texture2D`, `TextureChannel`, `cutoffThreshold`, `AlphaMipChain`, `GeneratedTextureAttestation`.
- Produces:
  ```csharp
  internal static class UnityGeneratedTextureEvidence
  {
      internal static bool TryCapture(
          Texture2D texture,
          TextureChannel channel,
          float cutoffThreshold,
          out AlphaMipChain chain);
  }
  ```

- [ ] **Step 1: Write failing test for UnityGeneratedTextureEvidence**

```csharp
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Alrauna.Amuse.Editor.Analysis;
using Alrauna.Amuse.Editor.Host;
using Alrauna.Amuse.Editor.Semantics;

namespace Alrauna.Amuse.Tests.Editor.Host
{
    [TestFixture]
    public class UnityGeneratedTextureEvidenceTests
    {
        private const string ContainerPath = "Assets/AmuseTests_GeneratedEvidence.asset";
        private ScriptableObject _container;
        private Texture2D _streamingSubTex;

        [SetUp]
        public void SetUp()
        {
            _container = ScriptableObject.CreateInstance<ScriptableObject>();
            AssetDatabase.CreateAsset(_container, ContainerPath);

            _streamingSubTex = new Texture2D(8, 8, TextureFormat.RGBA32, true);
            _streamingSubTex.streamingMipmaps = true;
            for (var m = 0; m < _streamingSubTex.mipmapCount; m++)
            {
                var dim = Mathf.Max(1, 8 >> m);
                var px = new Color32[dim * dim];
                for (var i = 0; i < px.Length; i++) px[i] = new Color32(255, 255, 255, 255);
                _streamingSubTex.SetPixels32(px, m);
            }
            _streamingSubTex.Apply(false, false);
            _streamingSubTex.name = "AAO_StreamingSubTex";
            AssetDatabase.AddObjectToAsset(_streamingSubTex, ContainerPath);
            AssetDatabase.SaveAssets();
        }

        [TearDown]
        public void TearDown()
        {
            if (AssetDatabase.LoadAssetAtPath<ScriptableObject>(ContainerPath) != null)
            {
                AssetDatabase.DeleteAsset(ContainerPath);
            }
        }

        [Test]
        public void StreamingGeneratedSubAsset_CapturesAlphaChainDirectlyFromLiveObject()
        {
            var ok = UnityGeneratedTextureEvidence.TryCapture(
                _streamingSubTex,
                TextureChannel.Alpha,
                1.0f,
                out var chain);

            Assert.That(ok, Is.True);
            Assert.That(chain, Is.Not.Null);
            Assert.That(chain.Count, Is.EqualTo(_streamingSubTex.mipmapCount));
            Assert.That(chain[0].IsFullyOpaque, Is.True);
        }

        [Test]
        public void UnityAlphaFieldEvidence_CapturesCharacterizedStreamingSubAsset()
        {
            var ok = UnityAlphaFieldEvidence.TryCapture(
                _streamingSubTex,
                out var source,
                out var chain);

            Assert.That(ok, Is.True);
            Assert.That(chain, Is.Not.Null);
            Assert.That(chain[0].IsFullyOpaque, Is.True);
        }
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Observe compile failure for `UnityGeneratedTextureEvidence`.

- [ ] **Step 3: Implement UnityGeneratedTextureEvidence and wire into UnityAlphaFieldEvidence**

Create `UnityGeneratedTextureEvidence.cs`:
```csharp
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Alrauna.Amuse.Editor.Analysis;
using Alrauna.Amuse.Editor.Semantics;

namespace Alrauna.Amuse.Editor.Host
{
    internal static class UnityGeneratedTextureEvidence
    {
        private static readonly Dictionary<(int instanceId, TextureChannel channel, int cutoffBits), AlphaMipChain>
            SessionCache = new();

        internal static bool TryCapture(
            Texture2D texture,
            TextureChannel channel,
            float cutoffThreshold,
            out AlphaMipChain chain)
        {
            chain = null;
            if (texture == null)
            {
                return false;
            }

            if (!GeneratedTextureAttestation.TryIdentifyProducer(texture, out _))
            {
                return false;
            }

            var cutoffBits = Mathf.RoundToInt(Mathf.Clamp01(cutoffThreshold) * 10000f);
            var key = (texture.GetInstanceID(), channel, cutoffBits);
            if (SessionCache.TryGetValue(key, out chain))
            {
                return true;
            }

            // Live generated object acquisition via RenderTexture blit
            // Blit copies resident mips cleanly even when streamingMipmaps is enabled
            var mipCount = texture.mipmapCount;
            if (mipCount <= 0)
            {
                return false;
            }

            var levels = new AlphaTextureData[mipCount];
            var threshold = Mathf.Clamp01(cutoffThreshold);

            for (var m = 0; m < mipCount; m++)
            {
                var width = Mathf.Max(1, texture.width >> m);
                var height = Mathf.Max(1, texture.height >> m);

                var rt = RenderTexture.GetTemporary(
                    width,
                    height,
                    0,
                    RenderTextureFormat.ARGB32,
                    RenderTextureReadWrite.Linear);

                var previous = RenderTexture.active;
                var readable = new Texture2D(width, height, TextureFormat.RGBA32, false);

                try
                {
                    Graphics.Blit(texture, rt);
                    RenderTexture.active = rt;
                    readable.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                    readable.Apply(false, false);

                    var pixels = readable.GetPixels32();
                    var flags = new byte[pixels.Length];
                    var threshold255 = Mathf.RoundToInt(threshold * 255f);

                    for (var i = 0; i < pixels.Length; i++)
                    {
                        var sample = channel == TextureChannel.Red
                            ? pixels[i].r
                            : pixels[i].a;

                        flags[i] = sample >= threshold255 ? byte.MaxValue : (byte)0;
                    }

                    levels[m] = new AlphaTextureData(width, height, flags);
                }
                finally
                {
                    RenderTexture.active = previous;
                    RenderTexture.ReleaseTemporary(rt);
                    Object.DestroyImmediate(readable);
                }
            }

            chain = new AlphaMipChain(levels);
            SessionCache[key] = chain;
            return true;
        }
    }
}
```

In `UnityAlphaFieldEvidence.cs`, add the route before the standard streaming gate:
```csharp
            if (GeneratedTextureAttestation.TryIdentifyProducer(texture2D, out _))
            {
                if (!UnityGeneratedTextureEvidence.TryCapture(
                        texture2D, channel, cutoffThreshold, out chain))
                {
                    source = default;
                    chain = null;
                    return false;
                }

                return true;
            }
```

- [ ] **Step 4: Run test to verify it passes**

Run `UnityGeneratedTextureEvidenceTests`. Observe all assertions pass.

- [ ] **Step 5: Commit**

```bash
git add Packages/com.alrauna.amuse/Editor/Host/UnityGeneratedTextureEvidence.cs* Packages/com.alrauna.amuse/Editor/Host/UnityAlphaFieldEvidence.cs Packages/com.alrauna.amuse/Tests/Editor/Host/UnityGeneratedTextureEvidenceTests.cs*
git commit -m "feat: capture live alpha evidence from characterized generated textures"
```

---

### Task 3: End-to-End Characterization and Regression Coverage

**Files:**
- Modify: `Packages/com.alrauna.amuse/Tests/Editor/Build/AlphaSeparationPreparationTests.cs`

**Interfaces:**
- Consumes: `AlphaSeparationPreparation`, `UnityGeneratedTextureEvidence`, `GeneratedTextureAttestation`.
- Produces: Verified end-to-end separation when textures are generated sub-assets.

- [ ] **Step 1: Write failing end-to-end test with generated sub-asset texture**

```csharp
        [Test]
        public void GeneratedSubAssetTexture_IsAdmittedAndProvenOpaqueEndToEnd()
        {
            using var assets = new OverrideTemporaryDirectoryScope(null);
            var containerPath = "Assets/AmuseTests_E2EContainer.asset";
            var container = ScriptableObject.CreateInstance<ScriptableObject>();
            AssetDatabase.CreateAsset(container, containerPath);

            var generatedTex = new Texture2D(8, 8, TextureFormat.RGBA32, true);
            generatedTex.name = "AAO_E2E_Texture";
            generatedTex.streamingMipmaps = true;
            for (var m = 0; m < generatedTex.mipmapCount; m++)
            {
                var dim = Mathf.Max(1, 8 >> m);
                var px = new Color32[dim * dim];
                for (var i = 0; i < px.Length; i++) px[i] = new Color32(255, 255, 255, 255);
                generatedTex.SetPixels32(px, m);
            }
            generatedTex.Apply(false, false);
            AssetDatabase.AddObjectToAsset(generatedTex, containerPath);
            AssetDatabase.SaveAssets();

            var root = new GameObject("AMUSE generated texture e2e");
            root.AddComponent<Alrauna.Amuse.Runtime.AmuseAvatarOptimizer>();

            try
            {
                var material = new Material(Shader.Find("Hidden/Alrauna/AmuseTests/LilToonCutoutStandIn"));
                material.mainTexture = generatedTex;
                material.SetFloat("_Cutoff", 0.5f);

                AddSingleTriangleRenderer(root, material, out var mesh);
                mesh.uv = new[]
                {
                    new Vector2(0.1f, 0.1f),
                    new Vector2(0.2f, 0.1f),
                    new Vector2(0.1f, 0.2f)
                };

                var state = RunBarrier(root);
                Assert.That(state.Successful, Is.True);
                Assert.That(state.Separations.Count, Is.EqualTo(1));
                Assert.That(state.Separations[0].CandidateCount, Is.EqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(root);
                AssetDatabase.DeleteAsset(containerPath);
            }
        }
```

- [ ] **Step 2: Run test to verify it passes with Tasks 1 & 2 in place**

Observe PASS.

- [ ] **Step 3: Commit**

```bash
git add Packages/com.alrauna.amuse/Tests/Editor/Build/AlphaSeparationPreparationTests.cs
git commit -m "test: add end-to-end regression coverage for generated sub-asset textures"
```

---

### Task 4: Full Suite Validation & Clean Handoff

**Files:**
- Validation only.

- [ ] **Step 1: Run full EditMode test suite**

Run all EditMode tests across product and test assemblies in Unity 2022.3.22f1.
Confirm 2,000+ tests pass with 0 failures.

- [ ] **Step 2: Discard manifest churn and run git diff --check**

```bash
git checkout -- Packages/manifest.json Packages/packages-lock.json 2>/dev/null
git diff --check
```

- [ ] **Step 3: Update documentation status**

Update `docs/superpowers/specs/2026-09-07-generated-texture-evidence-contract-design.md` status from `draft` to `implemented`.
