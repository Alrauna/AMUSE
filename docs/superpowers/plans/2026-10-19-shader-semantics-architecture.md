# Shader Semantics Architecture Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Formalize compiled C# as the sole source of truth for shader semantics, consolidate duplicate Poiyomi property literals, and enforce architectural invariants through automated tests.

**Architecture:** Maintain all shader semantic analysis, evidence schemas, and value algebra in compiled C#. Create `PoiyomiProperties.cs` to eliminate duplicate property string literals across Poiyomi semantic files while preserving existing test seams through forwarding aliases. Add automated architectural invariant tests in `ShaderSemanticsArchitectureTests.cs` to prevent runtime data file drift and guarantee property naming integrity.

**Tech Stack:** C# 9.0, Unity 2022.3 Editor API, NUnit constraint model (`Assert.That`).

**Spec:** `docs/superpowers/specs/2026-10-19-shader-semantics-architecture-design.md`

## Global Constraints

- Retain compiled C# as the sole source of truth for shader semantics.
- No runtime JSON loaders, no asset deserializers, and no dynamic expression interpreters.
- Compacting static attestation tables in `LilToonSourceAttestation.cs` into binary spans is deferred to a future change.
- No changes to mathematical formulas, rational intervals, or UV classification logic in `Editor/Analysis/`.
- No changes to `docs/architecture/vision.md`.
- No changes to the runtime component `AmuseAvatarOptimizer.cs`.
- No changes to Unity scene files, prefabs, materials, or asset GUIDs.
- No new external package dependencies.
- Zero behavior changes to existing material classification, refusal reporting, or opaque conversion recipes.
- Simple English / ASD-STE100 rules apply to all documentation: short active sentences, one idea per sentence, no semicolons, and no contractions.
- Never record machine-specific paths, hostnames, usernames, ports, or at-sign-hash identifiers.

---

### Task 1: Create `PoiyomiProperties.cs` and Retarget `PoiyomiMaterialSemantics.cs`

**Files:**
- Create: `Packages/com.alrauna.amuse/Editor/Semantics/Poiyomi/PoiyomiProperties.cs`
- Modify: `Packages/com.alrauna.amuse/Editor/Semantics/Poiyomi/PoiyomiMaterialSemantics.cs:35-90`
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Semantics/Poiyomi/PoiyomiMaterialSemanticsTests.cs`
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Semantics/Poiyomi/PoiyomiCutoutSplitTests.cs`

**Interfaces:**
- Consumes: Nothing
- Produces: `Alrauna.Amuse.Editor.Semantics.Poiyomi.PoiyomiProperties` internal static class with 22 string constants:
  `ShaderOptimizerEnabled`, `Mode`, `TwoPassMode`, `Cutoff`, `AlphaForceOpaque`, `MainTex`, `Color`, `MainTexUv`, `MainTexPan`, `EnableOutlines`, `OutlineOverrideAlpha`, `LineColor`, `OutlineTexture`, `OutlineTextureUv`, `OutlineTexturePan`, `SrcBlend`, `DstBlend`, `BlendOp`, `SrcBlendAlpha`, `DstBlendAlpha`, `BlendOpAlpha`, `AlphaToCoverage`.
- Preserves on `PoiyomiMaterialSemantics`: Forwarding constants for `OutlineEnabledProperty`, `OutlineOverrideAlphaProperty`, `LineColorProperty`, `OutlineTextureProperty`, `OutlineTextureUvProperty`, and `OutlineTexturePanProperty`.

- [ ] **Step 1: Write `PoiyomiProperties.cs`**

Create `Packages/com.alrauna.amuse/Editor/Semantics/Poiyomi/PoiyomiProperties.cs`:

```csharp
namespace Alrauna.Amuse.Editor.Semantics.Poiyomi
{
    /// <summary>
    /// The Poiyomi Toon property names that more than one module reads.
    /// Each spelling matches the verified Poiyomi Toon 9.3.64 shader definition.
    /// A property that only one module reads retains its local spelling.
    /// </summary>
    internal static class PoiyomiProperties
    {
        internal const string ShaderOptimizerEnabled = "_ShaderOptimizerEnabled";
        internal const string Mode = "_Mode";
        internal const string TwoPassMode = "_ModeTwoPass";
        internal const string Cutoff = "_Cutoff";
        internal const string AlphaForceOpaque = "_AlphaForceOpaque";
        internal const string MainTex = "_MainTex";
        internal const string Color = "_Color";
        internal const string MainTexUv = "_MainTexUV";
        internal const string MainTexPan = "_MainTexPan";
        internal const string EnableOutlines = "_EnableOutlines";
        internal const string OutlineOverrideAlpha = "_OutlineOverrideAlpha";
        internal const string LineColor = "_LineColor";
        internal const string OutlineTexture = "_OutlineTexture";
        internal const string OutlineTextureUv = "_OutlineTextureUV";
        internal const string OutlineTexturePan = "_OutlineTexturePan";
        internal const string SrcBlend = "_SrcBlend";
        internal const string DstBlend = "_DstBlend";
        internal const string BlendOp = "_BlendOp";
        internal const string SrcBlendAlpha = "_SrcBlendAlpha";
        internal const string DstBlendAlpha = "_DstBlendAlpha";
        internal const string BlendOpAlpha = "_BlendOpAlpha";
        internal const string AlphaToCoverage = "_AlphaToCoverage";
    }
}
```

- [ ] **Step 2: Retarget `PoiyomiMaterialSemantics.cs`**

Modify `Packages/com.alrauna.amuse/Editor/Semantics/Poiyomi/PoiyomiMaterialSemantics.cs`:
1. Delete private constants: `ShaderOptimizerEnabledProperty`, `MainTextureProperty`, `ColorProperty`, `MainTexUvProperty`, `MainTexPanProperty`, `ModeProperty`, `TwoPassModeProperty`.
2. Convert the six internal outline constants into forwarding aliases to `PoiyomiProperties`:
```csharp
        internal const string OutlineEnabledProperty =
            PoiyomiProperties.EnableOutlines;
        internal const string OutlineOverrideAlphaProperty =
            PoiyomiProperties.OutlineOverrideAlpha;
        internal const string LineColorProperty =
            PoiyomiProperties.LineColor;
        internal const string OutlineTextureProperty =
            PoiyomiProperties.OutlineTexture;
        internal const string OutlineTextureUvProperty =
            PoiyomiProperties.OutlineTextureUv;
        internal const string OutlineTexturePanProperty =
            PoiyomiProperties.OutlineTexturePan;
```
3. In `RequiredSchemaProperties` array (lines 64-76), retarget shared properties to `PoiyomiProperties.*` while keeping all emission properties intact:
```csharp
        private static readonly string[] RequiredSchemaProperties =
        {
            "shader_master_label",
            PoiyomiProperties.ShaderOptimizerEnabled,
            PoiyomiProperties.MainTex,
            PoiyomiProperties.Color,
            "_BumpMap",
            "_EmissionMap",
            "_EnableEmission",
            "_EnableEmission1",
            "_EnableEmission2",
            "_EnableEmission3",
        };
```
4. In `AlphaRequiredSchemaProperties` array (lines 78-84), retarget deleted private constants to `PoiyomiProperties.*`:
```csharp
        internal static readonly string[] AlphaRequiredSchemaProperties =
        {
            "shader_master_label",
            PoiyomiProperties.ShaderOptimizerEnabled,
            PoiyomiProperties.MainTex,
            PoiyomiProperties.Color,
        };
```
5. In methods `DeclaresCutoutPreset`, `TryInterpretMainSample`, `TryGetSupportedUvMapping`, and `TryGetMainTextureSampling`, replace deleted private constant usages with `PoiyomiProperties.*`.

- [ ] **Step 3: Run existing unit tests to verify compilation and passing status**

Run Poiyomi semantics tests in Unity Test Runner:
Expected: PASS (all tests in `PoiyomiMaterialSemanticsTests` and `PoiyomiCutoutSplitTests` pass).

- [ ] **Step 4: Commit Task 1**

```bash
git add Packages/com.alrauna.amuse/Editor/Semantics/Poiyomi/PoiyomiProperties.cs Packages/com.alrauna.amuse/Editor/Semantics/Poiyomi/PoiyomiProperties.cs.meta Packages/com.alrauna.amuse/Editor/Semantics/Poiyomi/PoiyomiMaterialSemantics.cs
git commit -m "refactor(semantics): consolidate shared Poiyomi properties into PoiyomiProperties"
```

---

### Task 2: Retarget `PoiyomiAlphaInterpreter.cs`, `PoiyomiSourceAttestation.cs`, and `PoiyomiOpaqueConversion.cs`

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/Semantics/Poiyomi/PoiyomiAlphaInterpreter.cs:13-75,135-150`
- Modify: `Packages/com.alrauna.amuse/Editor/Semantics/Poiyomi/PoiyomiSourceAttestation.cs:63-65,120-130,210-225`
- Modify: `Packages/com.alrauna.amuse/Editor/Semantics/Poiyomi/PoiyomiOpaqueConversion.cs:170-220,360-500`
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Semantics/Poiyomi/PoiyomiAlphaMaskTests.cs`
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Semantics/Poiyomi/PoiyomiOpaqueConversionTests.cs`
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Semantics/Poiyomi/PoiyomiAttestationTests.cs`

**Interfaces:**
- Consumes: `Alrauna.Amuse.Editor.Semantics.Poiyomi.PoiyomiProperties`
- Produces: Retargeted callers with deleted duplicate private constants.

- [ ] **Step 1: Retarget `PoiyomiAlphaInterpreter.cs`**

In `Packages/com.alrauna.amuse/Editor/Semantics/Poiyomi/PoiyomiAlphaInterpreter.cs`:
1. Delete private constants that duplicate `PoiyomiProperties`:
   `ShaderOptimizerEnabledProperty`, `MainTexUvProperty`, `AlphaForceOpaqueProperty`, `ModeProperty`, `TwoPassModeProperty`, `CutoffProperty`, `SrcBlendProperty`, `DstBlendProperty`, `BlendOpProperty`, `SrcBlendAlphaProperty`, `DstBlendAlphaProperty`, `BlendOpAlphaProperty`, `ColorProperty`, `LineColorProperty`, `MainTexPanProperty`, `MainTextureProperty`, `OutlineEnabledProperty`, `OutlineOverrideAlphaProperty`, `OutlineTextureProperty`, `OutlineTextureUvProperty`, `OutlineTexturePanProperty`.
2. Retarget their references across `PoiyomiAlphaInterpreter.cs` to `PoiyomiProperties.*`.
3. In `FeatureLabels` dictionary (lines 137-142), replace outline string literals with `PoiyomiProperties.EnableOutlines`, `OutlineOverrideAlpha`, `LineColor`, `OutlineTexture`, `OutlineTextureUv`, `OutlineTexturePan`.
4. In `AlphaCoverageGates` array (line 62), replace `"_AlphaToCoverage"` with `PoiyomiProperties.AlphaToCoverage`.
5. Keep single-use constants (`AlphaForceOpaque2Property`, `IgnoreMainTexAlphaProperty`, `MainAlphaMaskModeProperty`, `AlphaMaskBlendStrengthProperty`, `AlphaMaskValueProperty`, `AlphaMaskInvertProperty`, `AlphaMaskUvProperty`, `SrcBlend2Property`, `TwoPassColorProperty`, `AlphaMaskPanProperty`, `AlphaMaskProperty`, `OutlineAlphaDistanceFadeProperty`, `OutlineAudioLinkColorProperty`) as local private constants.

- [ ] **Step 2: Retarget `PoiyomiSourceAttestation.cs`**

In `Packages/com.alrauna.amuse/Editor/Semantics/Poiyomi/PoiyomiSourceAttestation.cs`:
1. Delete `private const string ShaderOptimizerEnabledProperty = "_ShaderOptimizerEnabled"`.
2. In methods `TryVerifyPoiyomiIdentity` (line 123) and `GatherSourceEvidence` (line 217), replace `ShaderOptimizerEnabledProperty` with `PoiyomiProperties.ShaderOptimizerEnabled`.

- [ ] **Step 3: Retarget `PoiyomiOpaqueConversion.cs`**

In `Packages/com.alrauna.amuse/Editor/Semantics/Poiyomi/PoiyomiOpaqueConversion.cs`:
1. In `CanonicalOpaqueTuple` array (lines 172-192), replace string literals for shared properties with `PoiyomiProperties.*`:
- `("_Mode", 0f)` -> `(PoiyomiProperties.Mode, 0f)`
- `("_AlphaForceOpaque", 1f)` -> `(PoiyomiProperties.AlphaForceOpaque, 1f)`
- `("_BlendOp", 0f)` -> `(PoiyomiProperties.BlendOp, 0f)`
- `("_BlendOpAlpha", 4f)` -> `(PoiyomiProperties.BlendOpAlpha, 4f)`
- `("_Cutoff", 0f)` -> `(PoiyomiProperties.Cutoff, 0f)`
- `("_SrcBlend", 1f)` -> `(PoiyomiProperties.SrcBlend, 1f)`
- `("_DstBlend", 0f)` -> `(PoiyomiProperties.DstBlend, 0f)`
- `("_SrcBlendAlpha", 1f)` -> `(PoiyomiProperties.SrcBlendAlpha, 1f)`
- `("_DstBlendAlpha", 1f)` -> `(PoiyomiProperties.DstBlendAlpha, 1f)`
- `("_AlphaToCoverage", 0f)` -> `(PoiyomiProperties.AlphaToCoverage, 0f)`
2. In `ShaderOptimizerEnabledProperty` (lines 214 and 624), replace with `PoiyomiProperties.ShaderOptimizerEnabled`.
3. In methods `AnalyzeEligibility` and `RefusalFromEvidence` (lines 366-496), replace property string literals for `_BlendOp`, `_SrcBlend`, `_DstBlend`, `_Cutoff`, `_Mode`, and `_AlphaForceOpaque` with `PoiyomiProperties.*`.
4. Keep single-use conversion property literals (`_AddSrcBlend`, `_AddDstBlend`, `_AddSrcBlendAlpha`, `_AddDstBlendAlpha`, `_ZWrite`, `_ZTest`, `_AlphaPremultiply`, `_OutlineSrcBlend`, `_OutlineDstBlend`, `_OutlineSrcBlendAlpha`) local to the conversion logic.

- [ ] **Step 4: Run existing Poiyomi tests to verify compilation and passing status**

Run `PoiyomiAlphaMaskTests`, `PoiyomiOpaqueConversionTests`, and `PoiyomiAttestationTests`.
Expected: PASS.

- [ ] **Step 5: Commit Task 2**

```bash
git add Packages/com.alrauna.amuse/Editor/Semantics/Poiyomi/PoiyomiAlphaInterpreter.cs Packages/com.alrauna.amuse/Editor/Semantics/Poiyomi/PoiyomiSourceAttestation.cs Packages/com.alrauna.amuse/Editor/Semantics/Poiyomi/PoiyomiOpaqueConversion.cs
git commit -m "refactor(semantics): retarget Poiyomi callers to PoiyomiProperties"
```

---

### Task 3: Create Architectural Invariant Tests (`ShaderSemanticsArchitectureTests.cs`)

**Files:**
- Create: `Packages/com.alrauna.amuse/Tests/Editor/Semantics/ShaderSemanticsArchitectureTests.cs`

**Interfaces:**
- Consumes: `PoiyomiProperties`, `LilToonProperties`, `LilToonSourceAttestation`, `PoiyomiSourceAttestation`
- Produces: 4 automated architectural invariant unit tests.

- [ ] **Step 1: Write `ShaderSemanticsArchitectureTests.cs`**

Create `Packages/com.alrauna.amuse/Tests/Editor/Semantics/ShaderSemanticsArchitectureTests.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Alrauna.Amuse.Editor.Semantics;
using Alrauna.Amuse.Editor.Semantics.LilToon;
using Alrauna.Amuse.Editor.Semantics.Poiyomi;
using NUnit.Framework;
using UnityEditor.PackageManager;

namespace Alrauna.Amuse.Tests.Editor.Semantics
{
    [TestFixture]
    internal sealed class ShaderSemanticsArchitectureTests
    {
        private static readonly Regex HexGuidRegex =
            new("^[0-9a-fA-F]{32}$", RegexOptions.Compiled);
        private static readonly Regex HexSha256Regex =
            new("^[0-9a-fA-F]{64}$", RegexOptions.Compiled);

        [Test]
        public void SemanticsDirectoryContainsZeroRuntimeDataFiles()
        {
            var packageInfo = PackageInfo.FindForAssembly(
                typeof(UnityMaterialSemantics).Assembly);
            var packageRoot = packageInfo != null
                ? packageInfo.resolvedPath
                : Path.GetFullPath("Packages/com.alrauna.amuse");
            var semanticsDir = Path.Combine(packageRoot, "Editor", "Semantics");
            Assert.That(Directory.Exists(semanticsDir), Is.True,
                "Semantics directory must exist.");

            var forbiddenExtensions = new HashSet<string>(
                StringComparer.OrdinalIgnoreCase)
            {
                ".json", ".yaml", ".yml", ".xml", ".txt"
            };

            var discoveredFiles = Directory.GetFiles(
                semanticsDir, "*.*", SearchOption.AllDirectories)
                .Where(p => forbiddenExtensions.Contains(Path.GetExtension(p)))
                .ToArray();

            Assert.That(discoveredFiles, Is.Empty,
                "Semantics directory must not contain runtime data files.");
        }

        [Test]
        public void PoiyomiPropertiesMatchShaderConventions()
        {
            var fields = typeof(PoiyomiProperties)
                .GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
                .Where(f => f.IsLiteral && !f.IsInitOnly && f.FieldType == typeof(string))
                .ToArray();

            Assert.That(fields, Is.Not.Empty);

            var values = new HashSet<string>(StringComparer.Ordinal);
            foreach (var field in fields)
            {
                var value = (string)field.GetValue(null);
                Assert.That(value, Is.Not.Null.And.Not.Empty);
                Assert.That(value.StartsWith("_", StringComparison.Ordinal), Is.True,
                    $"Property '{field.Name}' value '{value}' must start with an underscore.");
                Assert.That(values.Add(value), Is.True,
                    $"Duplicate property value detected: '{value}' in field '{field.Name}'.");
            }
        }

        [Test]
        public void LilToonPropertiesMatchShaderConventions()
        {
            var fields = typeof(LilToonProperties)
                .GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
                .Where(f => f.IsLiteral && !f.IsInitOnly && f.FieldType == typeof(string))
                .ToArray();

            Assert.That(fields, Is.Not.Empty);

            var values = new HashSet<string>(StringComparer.Ordinal);
            foreach (var field in fields)
            {
                var value = (string)field.GetValue(null);
                Assert.That(value, Is.Not.Null.And.Not.Empty);
                Assert.That(value.StartsWith("_", StringComparison.Ordinal), Is.True,
                    $"Property '{field.Name}' value '{value}' must start with an underscore.");
                Assert.That(values.Add(value), Is.True,
                    $"Duplicate property value detected: '{value}' in field '{field.Name}'.");
            }
        }

        [Test]
        public void AttestationProfilesContainValidGuidsAndHashes()
        {
            // Verify LilToon source attestation constants
            Assert.That(HexGuidRegex.IsMatch(LilToonSourceAttestation.SupportedShaderGuid),
                Is.True, "LilToon SupportedShaderGuid must be a 32-character hex string.");
            Assert.That(HexGuidRegex.IsMatch(LilToonSourceAttestation.PassShaderGuid),
                Is.True, "LilToon PassShaderGuid must be a 32-character hex string.");

            // Verify Poiyomi source attestation constants
            Assert.That(HexGuidRegex.IsMatch(PoiyomiSourceAttestation.CanonicalShaderGuid),
                Is.True, "Poiyomi CanonicalShaderGuid must be a 32-character hex string.");
            Assert.That(HexGuidRegex.IsMatch(PoiyomiSourceAttestation.TwoPassCanonicalShaderGuid),
                Is.True, "Poiyomi TwoPassCanonicalShaderGuid must be a 32-character hex string.");
            Assert.That(HexSha256Regex.IsMatch(PoiyomiSourceAttestation.CanonicalNormalizedSourceHash),
                Is.True, "Poiyomi CanonicalNormalizedSourceHash must be a 64-character hex string.");
            Assert.That(HexSha256Regex.IsMatch(PoiyomiSourceAttestation.TwoPassCanonicalNormalizedSourceHash),
                Is.True, "Poiyomi TwoPassCanonicalNormalizedSourceHash must be a 64-character hex string.");
        }
    }
}
```

- [ ] **Step 2: Run new architectural tests in Unity Test Runner**

Run `ShaderSemanticsArchitectureTests`.
Expected: 4/4 PASS.

- [ ] **Step 3: Run full Semantics test suite in Unity Test Runner**

Run all tests under `Alrauna.Amuse.Tests.Editor` in folder `Tests/Editor/Semantics/`.
Expected: All tests PASS.

- [ ] **Step 4: Commit Task 3**

```bash
git add Packages/com.alrauna.amuse/Tests/Editor/Semantics/ShaderSemanticsArchitectureTests.cs Packages/com.alrauna.amuse/Tests/Editor/Semantics/ShaderSemanticsArchitectureTests.cs.meta
git commit -m "test(semantics): add automated architectural invariant tests"
```
