# Alpha Separator Settings Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a per-texture minimum size cap and a minimum opaque coverage gate to the alpha separation policy, and regroup the three alpha policy settings under a new "Alpha Separator" inspector foldout.

**Architecture:** The size cap rides the existing `GatherAlphaFields` -> `AlphaMipChain.LimitedTo` seam: a pure helper on `AlphaMipChain` finds the largest level passing the size, and `GatherAlphaFields` truncates to the more restrictive of the two caps or drops the field entirely. The coverage gate lives at the top of `AlphaSeparationPreparation.Prepare`'s slot loop beside the marker-clip check and records a new slot-scoped refusal. Both settings are read from `AmuseAvatarOptimizer` once per build in the platform finish plugin.

**Tech Stack:** C# 9 / Unity 2022.3, NUnit (Unity Test Framework EditMode), NDMF API.

**Spec:** `docs/superpowers/specs/2026-09-10-alpha-separator-settings-design.md`

## Global Constraints

- Every English text that a human reads uses ASD-STE100 Simplified Technical English: short active sentences, one idea per sentence, no semicolons, no contractions.
- Never record an absolute or machine-specific path in any document, comment, commit message, or test. Use `<repo-root>`-relative paths.
- Never expose private Census names, paths, GUIDs, per-avatar or per-renderer rows, or fingerprint-like identifiers.
- Never modify source meshes, materials, textures, import settings, animation assets, prefabs, or scenes.
- Preserve exact mathematical proofs: the settings are policy applied after or around proof. They never change what `TriangleAlphaClassifier` computes for a consulted level.
- Fail closed: a texture below the minimum size leaves no consulted field, so its triangles classify Unknown.
- The new defaults (minimum size 128, coverage 25) are confirmed behavior changes per spec section 5. Do not soften them.
- Production types remain internal to `Alrauna.Amuse.Editor` and `Alrauna.Amuse.Runtime`.
- Tests run through Unity MCP on the dev project instance. Use `xd://mcp__unitymcp_run_tests` with `{"testMode":"EditMode", ...}` and poll `xd://mcp__unitymcp_get_test_job`. A filtered run reporting 0 tests is a failure. Record observed counts.
- Discard toolchain churn with `git checkout -- Packages/manifest.json Packages/packages-lock.json` before every commit.
- File name equals type name. One public type per file. Test classes are named `<Type>Tests` and mirror production folders.
- Do not dispatch subagents. Run only the focused tests each step names, plus the full suite where a step says so.

---

### Task 1: Runtime component settings

**Files:**
- Modify: `Packages/com.alrauna.amuse/Runtime/AmuseAvatarOptimizer.cs`
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Runtime/AmuseAvatarOptimizerTests.cs`

**Interfaces:**
- Produces: `AmuseAvatarOptimizer.PreserveTransparencyMinTextureSize` (`int`, default `128`, `-1` means every size).
- Produces: `AmuseAvatarOptimizer.MinimumOpaqueCoveragePercent` (`int`, default `25`, range 0-100).
- Produces: serialized fields `_preserveTransparencyMinTextureSize` and `_minimumOpaqueCoveragePercent`. Later tasks read these names via `serializedObject.FindProperty`.

- [ ] **Step 1: Write the failing tests**

Append to `Packages/com.alrauna.amuse/Tests/Editor/Runtime/AmuseAvatarOptimizerTests.cs`, inside the test class, following the existing style:

```csharp
        [Test]
        public void DefaultPreserveTransparencyMinTextureSizeIs128()
        {
            var go = new GameObject("Root");
            try
            {
                var optimizer = go.AddComponent<AmuseAvatarOptimizer>();
                Assert.That(
                    optimizer.PreserveTransparencyMinTextureSize,
                    Is.EqualTo(128));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void PreserveTransparencyMinTextureSizeSerializedPropertyRoundTrips()
        {
            var go = new GameObject("Root");
            try
            {
                var optimizer = go.AddComponent<AmuseAvatarOptimizer>();
                var serializedObject = new SerializedObject(optimizer);
                var property = serializedObject.FindProperty(
                    "_preserveTransparencyMinTextureSize");
                Assert.That(property, Is.Not.Null);
                property.intValue = 512;
                serializedObject.ApplyModifiedProperties();

                Assert.That(
                    optimizer.PreserveTransparencyMinTextureSize,
                    Is.EqualTo(512));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void DefaultMinimumOpaqueCoveragePercentIs25()
        {
            var go = new GameObject("Root");
            try
            {
                var optimizer = go.AddComponent<AmuseAvatarOptimizer>();
                Assert.That(
                    optimizer.MinimumOpaqueCoveragePercent, Is.EqualTo(25));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void MinimumOpaqueCoveragePercentSerializedPropertyRoundTrips()
        {
            var go = new GameObject("Root");
            try
            {
                var optimizer = go.AddComponent<AmuseAvatarOptimizer>();
                var serializedObject = new SerializedObject(optimizer);
                var property = serializedObject.FindProperty(
                    "_minimumOpaqueCoveragePercent");
                Assert.That(property, Is.Not.Null);
                property.intValue = 40;
                serializedObject.ApplyModifiedProperties();

                Assert.That(
                    optimizer.MinimumOpaqueCoveragePercent, Is.EqualTo(40));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }
```

- [ ] **Step 2: Run the focused tests and verify they fail**

Run the four new tests by name through Unity MCP (`xd://mcp__unitymcp_run_tests`, EditMode, filtered to `Alrauna.Amuse.Tests.Editor` runtime tests), poll with `xd://mcp__unitymcp_get_test_job`.
Expected: compile errors, because the properties do not exist. Record the failure.

- [ ] **Step 3: Implement the fields and properties**

In `Packages/com.alrauna.amuse/Runtime/AmuseAvatarOptimizer.cs`, add after `_preserveTransparencyMaxMipLevel`:

```csharp
        /// <summary>
        /// The smallest texture size the opacity proof consults, in
        /// texels. The value -1 means every size. The proof stops
        /// consulting a texture's mip chain at the level whose width or
        /// height falls below this size. A texture already smaller than
        /// this size has no consulted levels and never converts. The
        /// default of 128 is a rule of thumb that matches normal
        /// viewing distances. The inspector shows it as "Preserve
        /// Transparency Minimum Texture Size".
        /// </summary>
        [SerializeField]
        private int _preserveTransparencyMinTextureSize = 128;

        /// <summary>
        /// The smallest share of proven-opaque triangles a mixed
        /// submesh needs before AMUSE splits it onto a separate opaque
        /// material. Each split adds one runtime draw call, so a small
        /// share can cost more CPU than it saves GPU work. The value 0
        /// always splits when at least one triangle is proven opaque.
        /// The default of 25 percent is a rule of thumb. The inspector
        /// shows it as "Minimum Opaque Coverage Percentage".
        /// </summary>
        [SerializeField]
        [Range(0, 100)]
        private int _minimumOpaqueCoveragePercent = 25;
```

Add after the `PreserveTransparencyMaxMipLevel` property:

```csharp
        /// <summary>
        /// The minimum texture size as stored: -1 for every size, else
        /// the smallest consulted size in texels.
        /// </summary>
        public int PreserveTransparencyMinTextureSize =>
            _preserveTransparencyMinTextureSize;

        /// <summary>
        /// The smallest proven-opaque triangle share, in percent, a
        /// mixed submesh needs before AMUSE splits it. The value 0
        /// splits whenever at least one triangle is proven opaque.
        /// </summary>
        public int MinimumOpaqueCoveragePercent =>
            _minimumOpaqueCoveragePercent;
```

- [ ] **Step 4: Run the focused tests and verify they pass**

Run the same four tests.
Expected: 4 passed, 0 failed. Record the counts.

- [ ] **Step 5: Commit**

```bash
git add Packages/com.alrauna.amuse/Runtime/AmuseAvatarOptimizer.cs Packages/com.alrauna.amuse/Tests/Editor/Runtime/AmuseAvatarOptimizerTests.cs
git commit -m "feat: add minimum texture size and coverage settings to optimizer"
```

---

### Task 2: AlphaMipChain.MaximumLevelAtOrAbove

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/Analysis/AlphaMipChain.cs`
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Analysis/AlphaMipChainTests.cs`

**Interfaces:**
- Consumes: existing `AlphaMipChain` constructor, `Count`, and the test file's `Level(int width, int height, byte value)` helper.
- Produces: `internal int MaximumLevelAtOrAbove(int minimumSize)` on `AlphaMipChain`. Returns the largest level index whose width and height are both at or above `minimumSize`, or `-1` when even mip 0 is smaller. Throws `ArgumentOutOfRangeException` for sizes below 1. Task 3 consumes this method.

- [ ] **Step 1: Write the failing tests**

Append to `Packages/com.alrauna.amuse/Tests/Editor/Analysis/AlphaMipChainTests.cs`, inside the test class:

```csharp
        [Test]
        public void MaximumLevelAtOrAboveReturnsTheLastQualifyingLevel()
        {
            var chain = new AlphaMipChain(new[]
            {
                Level(32, 32, 1), Level(16, 16, 2), Level(8, 8, 3),
                Level(4, 4, 4), Level(2, 2, 5), Level(1, 1, 6),
            });

            Assert.That(chain.MaximumLevelAtOrAbove(8), Is.EqualTo(2));
            Assert.That(chain.MaximumLevelAtOrAbove(9), Is.EqualTo(1));
            Assert.That(chain.MaximumLevelAtOrAbove(32), Is.EqualTo(0));
            Assert.That(chain.MaximumLevelAtOrAbove(1), Is.EqualTo(5));
        }

        [Test]
        public void MaximumLevelAtOrAboveReturnsMinusOneWhenMipZeroIsBelowTheMinimum()
        {
            var chain = new AlphaMipChain(
                new[] { Level(8, 8, 1), Level(4, 4, 2) });

            Assert.That(chain.MaximumLevelAtOrAbove(16), Is.EqualTo(-1));
        }

        [Test]
        public void MaximumLevelAtOrAboveLetsTheSmallerDimensionGovern()
        {
            var chain = new AlphaMipChain(
                new[] { Level(32, 8, 1), Level(16, 4, 2) });

            Assert.That(chain.MaximumLevelAtOrAbove(8), Is.EqualTo(0));
            Assert.That(chain.MaximumLevelAtOrAbove(9), Is.EqualTo(-1));
        }

        [Test]
        public void MaximumLevelAtOrAboveRejectsNonPositiveSizes()
        {
            var chain = new AlphaMipChain(new[] { Level(2, 2, 1) });

            Assert.Throws<ArgumentOutOfRangeException>(
                () => chain.MaximumLevelAtOrAbove(0));
        }
```

- [ ] **Step 2: Run the focused tests and verify they fail to compile**

Run the four new tests through Unity MCP.
Expected: compile error, `MaximumLevelAtOrAbove` does not exist. Record it.

- [ ] **Step 3: Implement the helper**

In `Packages/com.alrauna.amuse/Editor/Analysis/AlphaMipChain.cs`, add after `LimitedTo`:

```csharp
        /// <summary>
        /// The largest level index whose width and height are both at
        /// or above <paramref name="minimumSize"/>, or -1 when even
        /// mip 0 is smaller. Each level halves, so the qualifying
        /// levels always form a prefix of the chain and the returned
        /// index caps the proof's consulted prefix. This is the pure
        /// rule behind the user's "Preserve Transparency Minimum
        /// Texture Size" policy: the smaller dimension governs.
        /// </summary>
        internal int MaximumLevelAtOrAbove(int minimumSize)
        {
            if (minimumSize < 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(minimumSize),
                    "The minimum size must be at least one texel.");
            }

            var last = -1;
            for (var level = 0; level < _levels.Length; level++)
            {
                if (_levels[level].Width >= minimumSize &&
                    _levels[level].Height >= minimumSize)
                {
                    last = level;
                }
                else
                {
                    break;
                }
            }

            return last;
        }
```

- [ ] **Step 4: Run the focused tests and verify they pass**

Expected: 4 passed, 0 failed, and the existing `AlphaMipChainTests` tests still pass. Record the counts.

- [ ] **Step 5: Commit**

```bash
git add Packages/com.alrauna.amuse/Editor/Analysis/AlphaMipChain.cs Packages/com.alrauna.amuse/Tests/Editor/Analysis/AlphaMipChainTests.cs
git commit -m "feat: add minimum-size level lookup to alpha mip chain"
```

---

### Task 3: Size cap in GatherAlphaFields with fixture policy pinning

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/Host/UnityRendererAlphaAnalysis.cs`
- Modify: `Packages/com.alrauna.amuse/Editor/Build/AmusePlatformFinishPlugin.cs`
- Create: `Packages/com.alrauna.amuse/Tests/Editor/Build/FixtureProofScope.cs` plus a `.meta` is generated by Unity; never hand-write meta files.
- Modify (mechanical, see Step 3): the conversion test files that construct `AmuseAvatarOptimizer` components:
  `Tests/Editor/Build/AlphaSeparationApplyTests.cs`,
  `Tests/Editor/Build/AlphaSeparationPreparationTests.cs`,
  `Tests/Editor/Build/AlphaSeparationSplitTests.cs`,
  `Tests/Editor/Build/AlphaSeparationPersistenceTests.cs`,
  `Tests/Editor/Build/AmusePlatformFinishPluginTests.cs`.

**Interfaces:**
- Consumes: `AlphaMipChain.MaximumLevelAtOrAbove(int)` from Task 2; `AmuseAvatarOptimizer.PreserveTransparencyMinTextureSize` from Task 1.
- Produces: `GatherAlphaFields(IReadOnlyList<CapturedAlphaMaterial> materials, int maxMipLevel, int minTextureSize)`; throws `ArgumentOutOfRangeException` when `minTextureSize < 1`. Produces the plugin mapping `MinTextureSizeFrom` (stored value below 1 becomes 1). Produces `FixtureProofScope.PinAllSizes(GameObject root)` used by Tasks 4 and 5.

**Why the fixture pinning is part of this task:** the new default of 128 leaves every fixture texture smaller than 128 texels with no consulted field. Nearly all conversion fixtures use 8x8 or 32x32 textures. The product change and the test adaptation are one behavior coupling, so one review must see both.

- [ ] **Step 1: Change GatherAlphaFields**

In `Packages/com.alrauna.amuse/Editor/Host/UnityRendererAlphaAnalysis.cs`, change the signature and body. The method currently sits near line 580:

```csharp
        internal static IReadOnlyDictionary<
            (TextureSourceId source, TextureChannel channel),
            AlphaMipChain> GatherAlphaFields(
                IReadOnlyList<CapturedAlphaMaterial> materials,
                int maxMipLevel,
                int minTextureSize)
        {
            if (maxMipLevel < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(maxMipLevel),
                    "The mip cap must be a level index of at least zero.");
            }
            if (minTextureSize < 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(minTextureSize),
                    "The minimum texture size must be at least one texel.");
            }
```

Inside the texture loop, replace both `LimitedTo(maxMipLevel)` calls with a shared scope. Keep the existing doc comment and add one sentence: "The minimum texture size drops a texture from the proof entirely when even mip 0 is smaller than the size."

```csharp
                foreach (var texture in material.Evidence.Textures)
                {
                    if (!texture.HasSourceIdentity)
                    {
                        continue;
                    }

                    // Both channels of one texture share level
                    // dimensions, so one size scope governs both.
                    var sizeScope =
                        TextureSizeScope(texture, minTextureSize);
                    if (sizeScope < 0)
                    {
                        // The texture is below the user's minimum
                        // size: no level is consulted, the field stays
                        // absent, and every triangle over it stays
                        // Unknown through MissingTextureEvidence.
                        continue;
                    }

                    var cap = Math.Min(maxMipLevel, sizeScope);
                    var key =
                        (texture.SourceIdentity, TextureChannel.Alpha);
                    if (texture.HasAlphaChannel &&
                        !fields.ContainsKey(key))
                    {
                        fields.Add(
                            key,
                            texture.AlphaChannel.LimitedTo(cap));
                    }

                    var redKey =
                        (texture.SourceIdentity, TextureChannel.Red);
                    if (texture.HasRedChannel &&
                        !fields.ContainsKey(redKey))
                    {
                        fields.Add(
                            redKey,
                            texture.RedChannel.LimitedTo(cap));
                    }
                }
```

Add the private helper beside `GatherAlphaFields`. Use the element type of `material.Evidence.Textures` exactly as declared (confirm with the language server, do not guess):

```csharp
        private static int TextureSizeScope(
            <element type of material.Evidence.Textures> texture,
            int minTextureSize)
        {
            if (texture.HasAlphaChannel)
            {
                return texture.AlphaChannel.MaximumLevelAtOrAbove(
                    minTextureSize);
            }

            if (texture.HasRedChannel)
            {
                return texture.RedChannel.MaximumLevelAtOrAbove(
                    minTextureSize);
            }

            return -1;
        }
```

Update the one internal no-policy call site near line 497 (`Analyze`) to pass `1`:

```csharp
            var fields =
                GatherAlphaFields(snapshot.Materials, int.MaxValue, 1);
```

Run `lsp references` on `GatherAlphaFields` and migrate every remaining caller. Callers outside this task's files are a stop condition: report back instead of improvising.

- [ ] **Step 2: Map the stored policy in the plugin**

In `Packages/com.alrauna.amuse/Editor/Build/AmusePlatformFinishPlugin.cs`, add beside `ProofMipCapFrom` near line 563:

```csharp
        /// <summary>
        /// Maps the optimizer's "Preserve Transparency Minimum Texture
        /// Size" policy to the proof's minimum: a stored -1 (All
        /// Sizes) becomes 1, which every level satisfies. A missing
        /// component also maps to 1. The defensive read mirrors
        /// <see cref="ProofMipCapFrom"/>.
        /// </summary>
        private static int MinTextureSizeFrom(
            Alrauna.Amuse.Runtime.AmuseAvatarOptimizer optimizer)
        {
            if (optimizer == null)
            {
                return 1;
            }

            var stored = optimizer.PreserveTransparencyMinTextureSize;
            return stored < 1 ? 1 : stored;
        }
```

At the read site near line 428, add:

```csharp
            var minTextureSize = MinTextureSizeFrom(optimizer);
```

Thread it into the `GatherAlphaFields` call near line 740:

```csharp
            var fields = UnityRendererAlphaAnalysis.GatherAlphaFields(
                evidence.AdmittedMaterials,
                maxMipLevel,
                minTextureSize);
```

- [ ] **Step 3: Create the fixture policy helper and pin existing tests**

Create `Packages/com.alrauna.amuse/Tests/Editor/Build/FixtureProofScope.cs`. Use the same namespace as the Build test files (`Alrauna.Amuse.Tests.Editor.Build`):

```csharp
using UnityEditor;
using UnityEngine;

namespace Alrauna.Amuse.Tests.Editor.Build
{
    /// <summary>
    /// Fixture policy helpers. Conversion fixtures use small textures
    /// that a real minimum size would leave unproven, so conversion
    /// tests pin the size policy to All Sizes unless the test is
    /// about the size policy itself.
    /// </summary>
    internal static class FixtureProofScope
    {
        /// <summary>The stored value meaning every size.</summary>
        internal const int AllSizes = -1;

        /// <summary>
        /// Pins the optimizer component on this root to All Sizes, so
        /// the fixture proves over the full mip chains.
        /// </summary>
        internal static void PinAllSizes(GameObject root)
        {
            var optimizer =
                root.GetComponent<
                    Alrauna.Amuse.Runtime.AmuseAvatarOptimizer>();
            if (optimizer == null)
            {
                return;
            }

            var serialized = new SerializedObject(optimizer);
            serialized.FindProperty("_preserveTransparencyMinTextureSize")
                .intValue = AllSizes;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
```

Then, in every test file listed above, find each `AddComponent<Alrauna.Amuse.Runtime.AmuseAvatarOptimizer>()` call whose test runs a proof (all of them in these files), and insert immediately after the component-creation statement, using the same root variable the call used:

```csharp
            FixtureProofScope.PinAllSizes(root);
```

Match the receiver variable exactly: some sites use `baselineRoot`, `refusedRoot`, `preparedRoot`, or `child`. Use `grep` with pattern `AddComponent<Alrauna\.Amuse\.Runtime\.AmuseAvatarOptimizer>|AddComponent<AmuseAvatarOptimizer>` per file and work site by site. Skip no site in the five listed files. Test files outside the list that construct the component get the same treatment if their suite run fails on the size policy.

- [ ] **Step 4: Run the Build and Host test groups**

Run `Alrauna.Amuse.Tests.Editor` EditMode filtered to the Build and Host test classes through Unity MCP.
Expected: all pass. A failure in a proof test means a missed pin site. Record the counts.

- [ ] **Step 5: Commit**

```bash
git add Packages/com.alrauna.amuse/Editor/Host/UnityRendererAlphaAnalysis.cs Packages/com.alrauna.amuse/Editor/Build/AmusePlatformFinishPlugin.cs Packages/com.alrauna.amuse/Tests/Editor/Build/
git commit -m "feat: scope the opacity proof by minimum texture size"
```

---

### Task 4: Size policy end-to-end test

**Files:**
- Modify: `Packages/com.alrauna.amuse/Tests/Editor/Build/AlphaSeparationPreparationTests.cs`

**Interfaces:**
- Consumes: `RunMipPolicyArm(Texture2D, string, int?, int?)` after this task extends it; `LilToonCutoutConversionFixtures.ImportExplicitMipmapTexture(string, int, Func<int, byte>)`; `FixtureProofScope.AllSizes`.

- [ ] **Step 1: Extend the arm helper**

In `RunMipPolicyArm` near line 2055, capture the component and add the optional size override. The helper already pins All Sizes through `FixtureProofScope.PinAllSizes(root)` from Task 3; the override re-pins when the arm tests the size policy itself:

```csharp
        private static AmusePlatformFinishState RunMipPolicyArm(
            Texture2D mainTex,
            string armName,
            int? maxMipLevel,
            int? minTextureSize = null)
        {
            var root = new GameObject("AMUSE mip policy " + armName);
            var component =
                root.AddComponent<Alrauna.Amuse.Runtime.AmuseAvatarOptimizer>();
            FixtureProofScope.PinAllSizes(root);
            var serialized = new SerializedObject(component);
            if (maxMipLevel.HasValue)
            {
                serialized.FindProperty("_preserveTransparencyMaxMipLevel")
                    .intValue = maxMipLevel.Value;
            }

            if (minTextureSize.HasValue)
            {
                serialized.FindProperty("_preserveTransparencyMinTextureSize")
                    .intValue = minTextureSize.Value;
            }

            serialized.ApplyModifiedProperties();
```

Keep the remainder of the helper unchanged. Update the existing `PreserveTransparencyMipPolicyScopesTheProofEndToEnd` test only if the helper rename forces it; its arms keep their meaning because the helper pins All Sizes by default.

- [ ] **Step 2: Write the failing test**

Add the test beside `PreserveTransparencyMipPolicyScopesTheProofEndToEnd` near line 1990. The texture is 32x32 with a fade from mip 2 up: mip 0 is 32x32, mip 1 is 16x16, mip 2 is 8x8.

```csharp
        [Test]
        public void PreserveTransparencySizePolicyScopesTheProofEndToEnd()
        {
            using var assets = new OverrideTemporaryDirectoryScope(null);
            var fixtures = new LilToonCutoutConversionFixtures();
            try
            {
                fixtures.BaseSetUp();

                // 32x32 base: every level opaque except mip 2 and
                // coarser, which are fully transparent.
                var earlyHole = fixtures.ImportExplicitMipmapTexture(
                    "size_policy_early_hole",
                    32,
                    mip => mip >= 2 ? (byte)0 : (byte)255);

                // A minimum size of 9 consults the 32x32 and 16x16
                // levels only. The fade at mip 2 leaves the proof
                // scope, so the triangle moves.
                var capped = RunMipPolicyArm(
                    earlyHole, "size cap 9", null, 9);
                Assert.That(
                    capped.SemanticallyRefusedRendererCount, Is.Zero,
                    "fixture precondition: the capped arm must resolve");
                Assert.That(
                    capped.OpaqueCandidateTriangleCount, Is.EqualTo(1),
                    "a fade inside the size-ignored levels must not " +
                    "stop the proof");
                Assert.That(
                    capped.Separation, Is.Not.Null,
                    "the size-capped proof must prepare a conversion");

                // A minimum size of 8 also consults the faded 8x8
                // level, so the proof must stop.
                var consulted = RunMipPolicyArm(
                    earlyHole, "size cap 8", null, 8);
                Assert.That(
                    consulted.OpaqueCandidateTriangleCount, Is.Zero,
                    "a fade inside the consulted levels must stop " +
                    "the proof");
                Assert.That(
                    consulted.Separation, Is.Null,
                    "the size policy limits scope. It never waives " +
                    "the consulted levels");

                // The 32x32 texture is below a 33 minimum on every
                // level. Nothing is consulted and nothing converts.
                var below = RunMipPolicyArm(
                    earlyHole, "size below minimum", null, 33);
                Assert.That(
                    below.OpaqueCandidateTriangleCount, Is.Zero,
                    "a texture below the minimum size must not " +
                    "convert");
                Assert.That(
                    below.Separation, Is.Null,
                    "a texture below the minimum size must prepare " +
                    "nothing");

                // All Sizes restores the full chain: the fade is
                // consulted and the proof stops.
                var allSizes = RunMipPolicyArm(
                    earlyHole, "size all sizes", null,
                    FixtureProofScope.AllSizes);
                Assert.That(
                    allSizes.OpaqueCandidateTriangleCount, Is.Zero,
                    "All Sizes must consult the faded level");
            }
            finally
            {
                fixtures.BaseTearDown();
            }
        }
```

- [ ] **Step 3: Run the focused test and verify it passes for the right reason**

Run `PreserveTransparencySizePolicyScopesTheProofEndToEnd` through Unity MCP.
Expected: passes. This test is a GREEN-with-guard against the Task 3 implementation: temporarily set the plugin mapping to ignore the size (`return 1;`) and re-run. Expected: the first and last arms fail. Restore the mapping and re-run. Expected: passes. Record all three observations.

- [ ] **Step 4: Commit**

```bash
git add Packages/com.alrauna.amuse/Tests/Editor/Build/AlphaSeparationPreparationTests.cs
git commit -m "test: pin minimum texture size proof scope end to end"
```

---

### Task 5: Minimum coverage gate

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/Build/AlphaSeparationRecords.cs`
- Modify: `Packages/com.alrauna.amuse/Editor/Build/AlphaSeparationPreparation.cs`
- Modify: `Packages/com.alrauna.amuse/Editor/Build/AmusePlatformFinishPlugin.cs`
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Build/AlphaSeparationApplyTests.cs`

**Interfaces:**
- Consumes: `AmuseAvatarOptimizer.MinimumOpaqueCoveragePercent` from Task 1; the split fixtures in `AlphaSeparationSplitTests` (`EnsureSplitFolder`, `DeleteSplitFolder`, `ImportSplitAlphaTexture`, `SplitAlphaMaterial`, `CreateSplitSourceMesh`, `CreateOpaqueAndSplitSourceMesh`); the apply-test harness helpers `Track`, `AddRenderer`, `NewController`, `VerifiedTransparentMaterial`, `DestroyCommittedClone`, `DestroyGenerated`, `AlphaSeparationSeamProbe`.
- Produces: `AlphaSeparationSlotRefusal.OpaqueCoverageBelowMinimum` (slot-scoped). Produces `AlphaSeparationPreparation.Prepare(..., int minimumOpaqueCoveragePercent)` — the implementer runs `lsp references` on `Prepare` and `RetainPreparedSeparation` and migrates every caller.

- [ ] **Step 1: Write the failing tests**

Add to `Packages/com.alrauna.amuse/Tests/Editor/Build/AlphaSeparationApplyTests.cs`, beside `IgnoredOutOfRangeBindingAtAppendedSlotRefusesSplitCandidate` near line 1690. Mirror that test's harness exactly: `OverrideTemporaryDirectoryScope`, split fixture folder, tracked assets, `AddRenderer`, seam probe, manual teardown. The split fixture's submesh 0 holds two triangles: one proven opaque, one transparent. Its coverage is 50 percent.

```csharp
        [Test]
        public void CoverageBelowMinimumRefusesTheSplitCandidate()
        {
            using var assets = new OverrideTemporaryDirectoryScope(null);
            var root = new GameObject("AMUSE coverage below minimum");
            var optimizer =
                root.AddComponent<Alrauna.Amuse.Runtime.AmuseAvatarOptimizer>();
            var serialized = new SerializedObject(optimizer);
            serialized.FindProperty("_preserveTransparencyMinTextureSize")
                .intValue = FixtureProofScope.AllSizes;
            serialized.FindProperty("_minimumOpaqueCoveragePercent")
                .intValue = 60;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            AlphaSeparationSeamProbe probe = null;
            AnimatorController controller = null;
            try
            {
                AlphaSeparationSplitTests.EnsureSplitFolder();
                try
                {
                    var texture = Track(
                        AlphaSeparationSplitTests.ImportSplitAlphaTexture(
                            "coverage_low"));
                    var split = Track(
                        AlphaSeparationSplitTests.SplitAlphaMaterial(
                            texture));
                    var transparent = Track(VerifiedTransparentMaterial());
                    var mesh = Track(
                        AlphaSeparationSplitTests.CreateSplitSourceMesh());
                    AddRenderer(
                        root, "split", mesh, split, transparent);

                    // Mirror the ignored-slot test's controller setup.
                    // No clip is needed: the gate is policy over the
                    // plan, not animation evidence.
                    controller = NewController(
                        root, "AMUSE coverage graph", null);

                    var context = AvatarProcessor.ProcessAvatar(
                        root, SeamTestPlatform.Instance);
                    probe = context.GetState<AlphaSeparationSeamProbe>();

                    Assert.That(
                        probe.SlotRefusals(
                            AlphaSeparationSlotRefusal
                                .OpaqueCoverageBelowMinimum),
                        Is.EqualTo(1),
                        "the 50 percent split must refuse below a 60 " +
                        "percent minimum");
                    Assert.That(
                        probe.Decision.HasMutation, Is.False,
                        "the coverage-refused candidate must produce " +
                        "no mesh or material write");
                }
                finally
                {
                    AlphaSeparationSplitTests.DeleteSplitFolder();
                }
            }
            finally
            {
                DestroyCommittedClone(root, controller);
                DestroyGenerated(probe?.State);
                DestroyTracked();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void CoverageAtTheMinimumPreparesTheSplitCandidate()
        {
            // Default coverage is 25. The fixture's 50 percent share
            // sits above it, and equality proceeds by the integer
            // rule. This arm pins the default-driven behavior.
            using var assets = new OverrideTemporaryDirectoryScope(null);
            var root = new GameObject("AMUSE coverage at minimum");
            var optimizer =
                root.AddComponent<Alrauna.Amuse.Runtime.AmuseAvatarOptimizer>();
            var serialized = new SerializedObject(optimizer);
            serialized.FindProperty("_preserveTransparencyMinTextureSize")
                .intValue = FixtureProofScope.AllSizes;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            AlphaSeparationSeamProbe probe = null;
            AnimatorController controller = null;
            try
            {
                AlphaSeparationSplitTests.EnsureSplitFolder();
                try
                {
                    var texture = Track(
                        AlphaSeparationSplitTests.ImportSplitAlphaTexture(
                            "coverage_at"));
                    var split = Track(
                        AlphaSeparationSplitTests.SplitAlphaMaterial(
                            texture));
                    var transparent = Track(VerifiedTransparentMaterial());
                    var mesh = Track(
                        AlphaSeparationSplitTests.CreateSplitSourceMesh());
                    AddRenderer(
                        root, "split", mesh, split, transparent);

                    controller = NewController(
                        root, "AMUSE coverage at graph", null);

                    var context = AvatarProcessor.ProcessAvatar(
                        root, SeamTestPlatform.Instance);
                    probe = context.GetState<AlphaSeparationSeamProbe>();

                    Assert.That(
                        probe.SlotRefusals(
                            AlphaSeparationSlotRefusal
                                .OpaqueCoverageBelowMinimum),
                        Is.Zero,
                        "the 50 percent split must prepare at the 25 " +
                        "percent default");
                    Assert.That(
                        probe.Decision.HasMutation, Is.True,
                        "the split at or above the minimum must apply");
                }
                finally
                {
                    AlphaSeparationSplitTests.DeleteSplitFolder();
                }
            }
            finally
            {
                DestroyCommittedClone(root, controller);
                DestroyGenerated(probe?.State);
                DestroyTracked();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void WhollyOpaqueCandidatesNeverRefuseForCoverage()
        {
            // A 100 percent minimum still admits the wholly opaque
            // submesh and refuses only the mixed one.
            using var assets = new OverrideTemporaryDirectoryScope(null);
            var root = new GameObject("AMUSE coverage wholly opaque");
            var optimizer =
                root.AddComponent<Alrauna.Amuse.Runtime.AmuseAvatarOptimizer>();
            var serialized = new SerializedObject(optimizer);
            serialized.FindProperty("_preserveTransparencyMinTextureSize")
                .intValue = FixtureProofScope.AllSizes;
            serialized.FindProperty("_minimumOpaqueCoveragePercent")
                .intValue = 100;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            AlphaSeparationSeamProbe probe = null;
            AnimatorController controller = null;
            try
            {
                AlphaSeparationSplitTests.EnsureSplitFolder();
                try
                {
                    var texture = Track(
                        AlphaSeparationSplitTests.ImportSplitAlphaTexture(
                            "coverage_wholly"));
                    var split = Track(
                        AlphaSeparationSplitTests.SplitAlphaMaterial(
                            texture));
                    var transparent = Track(VerifiedTransparentMaterial());
                    var mesh = Track(
                        AlphaSeparationSplitTests
                            .CreateOpaqueAndSplitSourceMesh());
                    AddRenderer(
                        root, "split", mesh, split, transparent);

                    controller = NewController(
                        root, "AMUSE wholly opaque coverage graph", null);

                    var context = AvatarProcessor.ProcessAvatar(
                        root, SeamTestPlatform.Instance);
                    probe = context.GetState<AlphaSeparationSeamProbe>();

                    Assert.That(
                        probe.SlotRefusals(
                            AlphaSeparationSlotRefusal
                                .OpaqueCoverageBelowMinimum),
                        Is.EqualTo(1),
                        "only the mixed submesh may refuse for " +
                        "coverage");
                    Assert.That(
                        probe.Decision.HasMutation, Is.True,
                        "the wholly opaque submesh must still convert " +
                        "at a 100 percent minimum");
                }
                finally
                {
                    AlphaSeparationSplitTests.DeleteSplitFolder();
                }
            }
            finally
            {
                DestroyCommittedClone(root, controller);
                DestroyGenerated(probe?.State);
                DestroyTracked();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }
```

If `NewController` requires a non-null clip, pass an empty tracked `AnimationClip` and keep everything else identical. Confirm the harness signatures from source before the first run.

- [ ] **Step 2: Run the focused tests and verify they fail**

Run the three new tests through Unity MCP.
Expected: compile error, `OpaqueCoverageBelowMinimum` does not exist. Record it.

- [ ] **Step 3: Add the refusal member**

In `Packages/com.alrauna.amuse/Editor/Build/AlphaSeparationRecords.cs`, add to the slot-scoped section of `AlphaSeparationSlotRefusal`, after `SlotBindingAbsentFromEvidence` near line 72:

```csharp
        /// <summary>The user's minimum opaque coverage policy gates
        /// this split: the slot's proven-opaque triangle share is
        /// below the configured percentage, so the extra draw call is
        /// not worth the GPU win. This is a policy refusal, not a
        /// proof fact: the classification is untouched, and a lower
        /// setting would let the same proof split.</summary>
        OpaqueCoverageBelowMinimum,
```

- [ ] **Step 4: Add the gate and thread the policy**

In `Packages/com.alrauna.amuse/Editor/Build/AlphaSeparationPreparation.cs`, extend the `Prepare` signature with `int minimumOpaqueCoveragePercent` (last parameter). Run `lsp references` on `Prepare` and migrate every caller, including tests.

Inside the submesh loop, insert immediately after the `Unchanged` skip near line 239 and before the marker-clip walk:

```csharp
                if (submesh.Disposition ==
                        SubmeshSeparationDisposition.Split &&
                    OpaqueCoverageBelow(
                        minimumOpaqueCoveragePercent, submesh))
                {
                    state.RecordSlotRefusal(
                        AlphaSeparationSlotRefusal
                            .OpaqueCoverageBelowMinimum);
                    continue;
                }
```

Add the private predicate beside the loop:

```csharp
        /// <summary>
        /// Whether the split's proven-opaque share is strictly below
        /// the user's minimum. Exact integer arithmetic, and a share
        /// exactly at the minimum proceeds. Unknown and transparent
        /// triangles both count in the denominator, because neither is
        /// proven opaque.
        /// </summary>
        private static bool OpaqueCoverageBelow(
            int minimumOpaqueCoveragePercent,
            SubmeshSeparationPlan submesh)
        {
            var totalTriangles =
                submesh.OpaqueTriangleOrdinals.Count +
                submesh.TransparentTriangleOrdinals.Count;
            return submesh.OpaqueTriangleOrdinals.Count * 100 <
                   minimumOpaqueCoveragePercent * totalTriangles;
        }
```

In `Packages/com.alrauna.amuse/Editor/Build/AmusePlatformFinishPlugin.cs`, add beside `MinTextureSizeFrom`:

```csharp
        /// <summary>
        /// Maps the optimizer's "Minimum Opaque Coverage Percentage"
        /// policy to the gate: a stored value below zero is a defect
        /// against the Range attribute, and the defensive read treats
        /// it as 0, which always splits on at least one proven
        /// triangle. The read mirrors <see cref="ProofMipCapFrom"/>.
        /// </summary>
        private static int MinimumCoverageFrom(
            Alrauna.Amuse.Runtime.AmuseAvatarOptimizer optimizer)
        {
            if (optimizer == null)
            {
                return 0;
            }

            var stored = optimizer.MinimumOpaqueCoveragePercent;
            return stored < 0 ? 0 : stored;
        }
```

At the read site near line 428, add `var minimumOpaqueCoveragePercent = MinimumCoverageFrom(optimizer);`. Thread it through `RetainPreparedSeparation` into `Prepare`. Run `lsp references` on both and migrate every caller.

- [ ] **Step 5: Run the Build test group and verify green**

Run the Build test classes through Unity MCP.
Expected: the three new tests pass and no existing test regresses. If report-string tests enumerate refusal reasons, update their expected sets to include the new member. Record the counts.

- [ ] **Step 6: Commit**

```bash
git add Packages/com.alrauna.amuse/Editor/Build/ Packages/com.alrauna.amuse/Tests/Editor/Build/AlphaSeparationApplyTests.cs
git commit -m "feat: gate alpha splits on minimum opaque coverage"
```

---

### Task 6: Inspector restructure

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/AmuseAvatarOptimizerEditor.cs`

**Interfaces:**
- Consumes: serialized fields `_preserveTransparencyMaxMipLevel`, `_preserveTransparencyMinTextureSize`, `_minimumOpaqueCoveragePercent` from Tasks 1 and the existing mip dropdown code.
- Produces: no API. Verification is compile, suite, and an editor smoke check.

- [ ] **Step 1: Restructure the foldouts**

In `Packages/com.alrauna.amuse/Editor/AmuseAvatarOptimizerEditor.cs`:

Add a state field beside `_advancedOpen` near line 15:

```csharp
        private bool _alphaSeparatorOpen;
```

Replace the `DrawAdvancedSettings();` call near line 48 with:

```csharp
            DrawAlphaSeparator();
            DrawAdvancedSettings();
```

Move the mip dropdown block from `DrawAdvancedSettings` (the `FindProperty` through `property.intValue = ...` near lines 74-111) into a new `DrawMipCapPopup()` method with no serialized-state side effects beyond the property write. Then add:

```csharp
        /// <summary>
        /// The Alpha Separator foldout. It holds the user's policy for
        /// the alpha separation feature: which texture levels the
        /// opacity proof consults, and how big a split must be before
        /// AMUSE pays a draw call for it.
        /// </summary>
        private void DrawAlphaSeparator()
        {
            _alphaSeparatorOpen = EditorGUILayout.Foldout(
                _alphaSeparatorOpen, "Alpha Separator",
                EditorStyles.foldoutHeader);
            if (!_alphaSeparatorOpen)
            {
                return;
            }

            DrawMipCapPopup();
            DrawMinTextureSizePopup();
            DrawCoverageSlider();
            serializedObject.ApplyModifiedProperties();
        }

        private void DrawMinTextureSizePopup()
        {
            var sizeProperty = serializedObject.FindProperty(
                "_preserveTransparencyMinTextureSize");

            // Index 0 is "All Sizes" (stored -1); index i maps to 2^i.
            var sizeOptions = new string[14];
            sizeOptions[0] = "All Sizes";
            for (var option = 1; option < sizeOptions.Length; option++)
            {
                sizeOptions[option] = Mathf.RoundToInt(
                    Mathf.Pow(2, option)).ToString();
            }

            var storedSize = sizeProperty.intValue;
            var sizeIndex = 0;
            if (storedSize > 0)
            {
                sizeIndex = 1;
                while (sizeIndex < sizeOptions.Length - 1 &&
                       Mathf.RoundToInt(Mathf.Pow(2, sizeIndex)) <
                       storedSize)
                {
                    sizeIndex++;
                }
            }

            var selectedSize = EditorGUILayout.Popup(
                new GUIContent(
                    "Preserve Transparency Minimum Texture Size",
                    "A mipmap level is a smaller copy of the texture. " +
                    "This setting sets the smallest level size AMUSE " +
                    "checks. AMUSE stops checking a texture when a " +
                    "level is smaller than this size on either side. " +
                    "A texture smaller than this size on either side " +
                    "is never checked, so its triangles never move." +
                    "\n\n" +
                    "Select All Sizes to check every level of every " +
                    "texture. Select a smaller size to check more " +
                    "levels when a part looks solid far away where it " +
                    "should show through. Select a larger size to " +
                    "check fewer levels when AMUSE moves too few " +
                    "triangles."),
                sizeIndex,
                sizeOptions);
            sizeProperty.intValue = selectedSize == 0
                ? -1
                : Mathf.RoundToInt(Mathf.Pow(2, selectedSize));
        }

        private void DrawCoverageSlider()
        {
            var coverageProperty = serializedObject.FindProperty(
                "_minimumOpaqueCoveragePercent");
            coverageProperty.intValue = EditorGUILayout.IntSlider(
                new GUIContent(
                    "Minimum Opaque Coverage Percentage",
                    "AMUSE moves proven opaque triangles of a mixed " +
                    "material onto a separate opaque material. Each " +
                    "split adds one draw call, and draw calls cost " +
                    "CPU time. This setting sets the smallest share " +
                    "of proven opaque triangles a mixed material " +
                    "needs before AMUSE does the split. The share " +
                    "counts every triangle of that material slot." +
                    "\n\n" +
                    "Use 0 to always split when at least one triangle " +
                    "is proven opaque. Raise the value to skip splits " +
                    "that move too little."),
                Mathf.Clamp(coverageProperty.intValue, 0, 100),
                0, 100);
        }
```

`DrawAdvancedSettings` keeps its foldout, the moved-out controls removed, the "Ignore Out-of-Range Material Slots" property field, and its own `serializedObject.ApplyModifiedProperties();`. Update its doc comment to say it holds the animation-closure tolerance setting.

- [ ] **Step 2: Compile and run the full suite**

Refresh the dev Unity instance, confirm zero compile errors with `xd://mcp__unitymcp_read_console`, then run the full `Alrauna.Amuse.Tests.Editor` EditMode suite.
Expected: 0 compile errors, full suite passes. Record the observed total.

- [ ] **Step 3: Editor smoke check**

With the dev instance, select any object with an `AmuseAvatarOptimizer` component in an open scene, or add one temporarily on a scratch object outside `Assets`. Confirm the inspector shows the "Alpha Separator" foldout above "Advanced Settings", the three controls inside it, and only the toggle in Advanced Settings. Delete the scratch object. Report what you observed. If no scene is available, say so explicitly instead of claiming the check.

- [ ] **Step 4: Commit**

```bash
git add Packages/com.alrauna.amuse/Editor/AmuseAvatarOptimizerEditor.cs
git commit -m "feat: group alpha policy settings under an Alpha Separator foldout"
```

---

### Task 7: Full-suite validation and hygiene

**Files:**
- No production changes. Report only.

**Interfaces:**
- Consumes: everything merged on the branch so far.

- [ ] **Step 1: Hygiene**

```bash
git checkout -- Packages/manifest.json Packages/packages-lock.json
git diff --check
git status --porcelain
```

Expected: empty `git status --porcelain` output, no whitespace findings. Fix any findings in the offending commit's file, amend is not authorized: make a separate `fixup` commit only if needed and say so.

- [ ] **Step 2: Full EditMode suite**

Run the full `Alrauna.Amuse.Tests.Editor` EditMode suite through Unity MCP on the dev instance and poll to completion.
Expected: 0 failures, 0 skipped. Record the observed total, duration, and job id. The total must exceed the pre-branch count of 2055 by the number of new tests; a total of exactly 2055 means the new tests did not run, which is a failure.

- [ ] **Step 3: Report**

Write the report to `.superpowers/sdd/2026-09-10-alpha-separator-settings-plan/task-7-report.md` with: the observed suite total and job id, the diff of new test names, the confirmation that both new defaults (128, 25) ship, and any skipped validation with reasons.
