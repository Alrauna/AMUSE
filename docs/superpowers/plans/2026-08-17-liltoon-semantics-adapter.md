# lilToon Material Semantics Adapter Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a conservative Editor-only lilToon 2.3.4 frontend. It produces exact `MaterialSemantics` for the single base opaque target. The plan also extracts the five Unity texture-evidence facts now shared by two frontends.

**Architecture:** Two new lilToon files split by responsibility: source attestation and semantic interpretation. Both the Poiyomi and lilToon frontends consume a new shared `UnityTextureEvidence` class. This plan does not modify the semantic core, the alpha classifier, the separation planner, or the alpha resolver. Poiyomi changes only through behaviour-preserving delegation to the five shared helpers. The plan deletes the helpers that this delegation makes strictly dead.

**Tech Stack:** Unity 2022.3, C#, NUnit EditMode tests, existing `Alrauna.Amuse.Editor` and `Alrauna.Amuse.Tests.Editor` assemblies. No new dependency, assembly, asmdef, or package metadata change.

**Design spec:** `docs/superpowers/specs/2026-08-17-liltoon-semantics-adapter-design.md` (amended 2026-08-18). Read it before Task 1, including the amendment summary. Every behavioural claim in this plan traces to a source line cited there.

## Global Constraints

- Supported lilToon release: `2.3.4`, tag commit `252fd8cfc46106d4967e95b3f2c788418502f227`, package `jp.lilxyzw.liltoon`, MIT.

- Supported shader: name `lilToon`, asset GUID `df12117ecd77c31469c224178886498e`. Pass shader `Hidden/ltspass_opaque`, GUID `61b4f98a5d78b4a4a9d89180fac793fc`. **The plan recognizes nothing else.** Every other shader, lilToon or not, returns `UnsupportedShader`.

- Material shader-format stamp: `_lilToonVersion` exists, is finite, and is **exactly `45f`**. No rounding, no tolerance.

- Three digest pins, all normalized (BOM stripped, CRLF/CR to LF, SHA-256, lowercase hex). **Task 0 measured them** on 2026-08-18 from a scratch `jp.lilxyzw.liltoon@2.3.4` install, and cross-checked them between default and stripped shader settings:
  - `lts.shader` canonical: `5206bec25e82db5f8009b27fcc5ba94d7c41113031d4b6b0a2c25ca324a9c704`
  - `ltspass_opaque.shader` canonical: `6b6c30c1cbe546fe753bcdc77f547441e3f9114ee80e9591bde2b8e6e7e5eb14`
  - `Shader/Includes/**` tree: `6e2dce6cb3073d5e04b569a14df8e0944c93ca408999fb42d7c717050c48fd46`
  These are measurements, not guesses. Never re-derive them from the lilToon repository. Its committed generated shaders are stale relative to the generator of their own tag.

- Everything new is `internal` and lives under `Alrauna.Amuse.Editor.Semantics` or `Alrauna.Amuse.Editor.Semantics.LilToon`.

- `MaterialSemantics.cs` is **not** modified. Any change to it stops the task and returns to architectural review.

- `UnityTextureEvidence` holds **exactly five** methods. No NDMF type, no optimization policy, no shader property name, no `Material` parameter, and no sixth method may enter it.

- Every safety predicate is **positive**: it returns success only for explicitly proven-safe cases, and refuses anything unrecognized. Phrase no predicate as "does this look unsafe?".

- This plan copies no lilToon upstream source file into this repository.

- Unproven behaviour becomes `Unknown` plus one diagnostic. It is never guessed, and additional uncertainty never widens a claim.

- Every new `.cs` and `.shader` file needs its Unity-generated `.meta`. Never hand-write, delete, or regenerate a `.meta` for an existing asset.

- **Do not commit, stage persistently, push, or open a PR at any point in this plan.** Git finalization is a separate approval gate. See "Git discipline".

## File structure

| File | Responsibility |
| --- | --- |
| Create `Packages/com.alrauna.amuse/Editor/Semantics/UnityTextureEvidence.cs` | Exactly five shader-independent Unity texture facts. No property names, no policy. |
| Create `Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonSourceAttestation.cs` | Pinned identity constants, normalized hashing, canonicalization, tree digest, define scan, verification conjunction. |
| Create `Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonMaterialSemantics.cs` | Entry point, verified-material seam, four interpreters, the lilToon-local sampled-range proof, result and diagnostic types. |
| Modify `Packages/com.alrauna.amuse/Editor/Semantics/Poiyomi/PoiyomiMaterialSemantics.cs` | Five method bodies delegate to `UnityTextureEvidence`; strictly dead helpers deleted. Nothing else changes. |
| Create `Packages/com.alrauna.amuse/Tests/Editor/Semantics/UnityTextureEvidenceTests.cs` | Direct coverage of the shared facts. |
| Create `Packages/com.alrauna.amuse/Tests/Editor/Semantics/LilToon/LilToonSemanticTest.shader` | Schema-complete stand-in exposing only the consumed property contract. |
| Create `Packages/com.alrauna.amuse/Tests/Editor/Semantics/LilToon/LilToonFixtureTestBase.cs` | Shared material and texture fixture helpers. |
| Create `Packages/com.alrauna.amuse/Tests/Editor/Semantics/LilToon/LilToonAttestationTests.cs` | Canonicalization, hashing, tree digest, define scan, version exactness, verification conjunction. |
| Create `Packages/com.alrauna.amuse/Tests/Editor/Semantics/LilToon/LilToonBaseColorTests.cs` | BaseColor forms and refusals. |
| Create `Packages/com.alrauna.amuse/Tests/Editor/Semantics/LilToon/LilToonAlphaTests.cs` | Alpha constant and coverage refusals. |
| Create `Packages/com.alrauna.amuse/Tests/Editor/Semantics/LilToon/LilToonNormalTests.cs` | Normal forms, UV composition, define evidence. |
| Create `Packages/com.alrauna.amuse/Tests/Editor/Semantics/LilToon/LilToonEmissionTests.cs` | Emission forms and refusals. |
| Create `Packages/com.alrauna.amuse/Tests/Editor/Semantics/LilToon/LilToonAdversarialTests.cs` | Output independence, malformed input, stripped-feature hazard. |

## Running tests

All test runs use the public `<repo-root>` Unity instance through Unity MCP `run_tests` (EditMode). Before the first run, use read-only MCP discovery to confirm the project root of the connected instance is `<repo-root>`. If no public Unity Editor runs, **do not** substitute the private avatar testbed. Report the blocked validation and stop.

Focused run example:

```text
test_names: Alrauna.Amuse.Tests.Editor.Semantics.LilToon.LilToonBaseColorTests
include_failed_tests: true
```

Full run: all EditMode tests, no filter.

After creating any new file, call Unity MCP `refresh_unity` before running tests so Unity generates the asset and its `.meta`.

## Git discipline

Work on `feat/liltoon-semantics-adapter`, already created from `b53bb17`.

**Do not commit at any point in this plan.** Implementation authorization and Git finalization are separate approval gates. Tasks 0–8 each end with a scope inspection instead of a commit. Task 9 stages temporarily for review and then resets.

```bash
git status --porcelain --untracked-files=all
```

`--untracked-files=all` is required: git does not show new `.cs`, `.shader`, or `.meta` files without it. Confirm that the listed paths are exactly the files that this task and its predecessors were meant to touch. The list must also include their `.meta` siblings. Confirm also that no `Library/`, `Temp/`, `Logs/`, `UserSettings/`, `Packages/manifest.json`, `Packages/packages-lock.json`, or `Packages/vpm-manifest.json` entry appears. Record the delta from the inspection of the previous task.

Nothing enters the staging area before Task 9, and this plan commits nothing at all. `HEAD` must still be `b53bb17` when the plan finishes.

---

### Task 0: Produce the digest pins from a real lilToon install (BLOCKING)

**Executed 2026-08-18. See the recorded outcome at the end of this task.** The pins could not come from the lilToon repository. Its committed `ltspass_opaque.shader` contains `#pragma skip_variants PROBE_VOLUMES_OFF PROBE_VOLUMES_L1 PROBE_VOLUMES_L2`, while `GetSkipVariantsProbeVolumes()` at the same tag returns the empty string. Running the real generator confirmed this. The line disappeared, and the file hash moved `bb5eaf4d…` → `efcb1fc6…`. The committed artifact is stale relative to its own generator and is not reproducible from the pinned source.

This task measures a real install, cross-checks the region rules, and records the constants Task 2 will use. It produces no repository code. **No later task may proceed until it succeeds.**

**Files:** none in this repository. Scratch work goes in the session scratchpad.

**Interfaces:** produces the three digest values consumed by Task 2.

- [x] **Step 1: Get two reference installs**

Install `jp.lilxyzw.liltoon@2.3.4` into a scratch Unity 2022.3 project — **not** the public dev project and **not** the private avatar testbed. Let lilToon run its startup generation with default settings. Copy `Assets/lilToon/Shader/lts.shader`, `Assets/lilToon/Shader/ltspass_opaque.shader`, and the whole `Assets/lilToon/Shader/Includes/` directory to the scratchpad as `install-default/`.

Then open the shader-setting UI of lilToon. Disable at least normal maps, emission, and shadow reception. The last one flips `useBaseShadow`, and this is what exercises the R2 shadow slot. Apply, and copy the same three artifacts to `install-stripped/`.

If you cannot produce a scratch install, report the blocked validation and stop. Do not substitute the private testbed, and do not invent constants.

- [x] **Step 2: Implement the canonicalizer as a scratch script and compute both installs**

Implement exactly the rules of the design spec. Do not simplify them for the script.

Line kinds: **D1** is a valueless `#define <IDENT>` where `IDENT` starts `LIL_FEATURE_`/`LIL_OPTIMIZE_` or equals `LIL_INPUT_OPTIMIZED`. **D2** is `#pragma skip_variants <tokens>`. There is no whitespace-line kind.

- **R1 — region A.** Find each line whose trimmed text is exactly `HLSLINCLUDE`. After it, take the maximal contiguous run where every line is D1 or D2. A blank line does not extend the run. Drop D1 and D2 inside it.

- **R2 — the shadow slot.** Drop a line only when all three conditions hold. The immediately preceding raw line is exactly `#define LIL_PASS_FORWARD`. The line is a `#pragma skip_variants` carrying exactly one keyword, and that keyword is exactly `SHADOW_VERY_HIGH`. Anything else stays hashed. There is no general pragma-prologue region and no blank-line normalization.

- **R3** — apply only to lines matching `^\s*(//)?#include\s+"<path>"\s*$`. Resolve `<path>` against the directory of the shader file itself, and against an **explicitly supplied project root**. Do not use the process working directory. Accept only if exactly one candidate is a file in the `Includes/` tree of that install. Compare with **exact ordinal** path identity, and treat as ambiguous when both candidates resolve to attested files and differ. Replace the line with `Includes/<path relative to the include root>`, `/` separators, and preserved subdirectories, or else leave it byte-identical.

Then normalize (BOM strip, CRLF/CR to LF) and SHA-256. For the include tree, hash every non-`.meta` file under `Includes/`, sort the `(relative path, hash)` pairs ordinally, join as `path:hash` with `\n`, and hash that listing.

- [x] **Step 3: Cross-check the two installs**

Assert all of the following. Any failure stops the milestone.

1. The **canonical** digests of `lts.shader`, `ltspass_opaque.shader`, and the `Includes/**` tree are **identical** between `install-default` and `install-stripped`.

2. The **raw** (non-canonical) hashes of `ltspass_opaque.shader` **differ** between the two installs. Otherwise the stripped install did not actually change anything, and the cross-check proved nothing.
3. In both installs the pass contains exactly one `#define LIL_RENDER` line with value `0`, and it is the only **valued** `#define` there.
4. In both installs, region A of the Shader-scope `HLSLINCLUDE` is empty. Its first line is the valued `LIL_RENDER` define, so `LIL_RENDER` was never a canonicalization candidate.
5. Every `#include` directive in both generated files resolved into the attested tree. Any unresolved include means the install layout is not modelled, so stop.

- [x] **Step 4: Record the constants and gate**

If every assertion holds: record the three canonical digests verbatim in the completion report. Carry them into Task 2 Step 3. They are now measured facts, not guesses.

If any assertion fails: **stop**. Diff the canonicalized texts of the two installs to locate the unmodelled variation. Do **not** widen region A, the R2 shadow slot, or R3 to make the observed files agree. That fits the rule to the sample, and the design forbids exactly that. Report the variation, the diff, and a recommendation, and return to architectural review.

- [x] **Step 5: Scope inspection**

```bash
git status --porcelain --untracked-files=all
```

Expected: only the two documentation files. This task adds no repository content.

#### Recorded outcome (executed 2026-08-18)

**COMPLETE: 14 of 14 assertions passed.** Environment: scratch Unity 2022.3.22f1 project, `jp.lilxyzw.liltoon-2.3.4.zip` release artifact, both states driven through the lilToon `ApplyShaderSetting` call.

| Assertion | Result |
| --- | --- |
| Raw pass hashes differ | PASS — `ea0a9dc9…` vs `95567a43…` |
| Canonical `lts.shader` matches | PASS |
| Canonical `ltspass_opaque.shader` matches | PASS |
| Include-tree digest matches | PASS — 37 files both |
| Exactly one `LIL_RENDER`, value `0` (both) | PASS |
| `LIL_RENDER` is the only valued define (both) | PASS |
| Shader-scope region A empty (both) | PASS — runs `[0, 102]` / `[0, 90]` |
| Every include resolved through R3 (both) | PASS — 3 + 21 directives, 0 unresolved |

Drop accounting: default `1068 → 966` lines (region A 102, slot 0). Stripped: `1057 → 966` (region A 90, slot 1). Canonical texts are byte-identical.

Negative controls ran on the real stripped artifact. Each must change the digest, and each did. The slot pragma relocated one line down, and an identical pragma injected into a pass body. A third control placed an identical pragma after `#define LIL_PASS_FORWARDADD`, and a fourth deleted a constant `skip_variants` line. A fifth redirected an include to a same-basename file outside the tree. Removing a region-A feature define correctly left the digest unchanged.


---

### Task 1: Shared Unity texture evidence

Extract the five texture facts that now have two concrete consumers, and repoint Poiyomi at them. The plan preserves the public-to-tests method names of Poiyomi as delegating wrappers, so its existing tests pass unmodified.

**Files:**

- Create: `Packages/com.alrauna.amuse/Editor/Semantics/UnityTextureEvidence.cs`
- Create: `Packages/com.alrauna.amuse/Tests/Editor/Semantics/UnityTextureEvidenceTests.cs`
- Modify: `Packages/com.alrauna.amuse/Editor/Semantics/Poiyomi/PoiyomiMaterialSemantics.cs` (bodies of `TryGetAssignedTextureSourceId`, `TryGetMainTextureSampling`, `TryGetColorInterpretation`, `TryProveSampledAlphaIsOne`, `IsCanonicalNormalMapImport`, plus removal of the now-unused `TryGetTextureImporter`, `TryMapFilterMode`, `TryMapWrapMode`, `IsAllZeroGuid` privates)

**Interfaces:**

- Consumes: `TextureSourceId`, `TextureSampling`, `TextureFilterMode`, `TextureWrapMode`, `TextureColorInterpretation` (all existing in `MaterialSemantics.cs`).
- Produces:

```csharp
internal static class UnityTextureEvidence
{
    internal static bool TryGetSourceId(Texture texture, out TextureSourceId sourceId);
    internal static bool TryGetSampling(Texture texture, out TextureSampling sampling);
    internal static bool TryGetColorInterpretation(Texture texture, out TextureColorInterpretation interpretation);
    internal static bool TryProveSampledAlphaIsOne(Texture texture);
    internal static bool IsCanonicalNormalMapImport(Texture texture);
}
```

**Exactly five methods.** Each has two concrete consumers with identical contracts. Do not add a sixth. In particular the sampled-`[0,1]`-range proof BaseColor needs is **not** here: it has one consumer and a lilToon-specific justification, so it lives in the lilToon frontend (Task 4).

- [ ] **Step 1: Write the failing tests**

Create `UnityTextureEvidenceTests.cs`:

```csharp
using System.IO;
using Alrauna.Amuse.Editor.Semantics;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Alrauna.Amuse.Tests.Editor.Semantics
{
    public sealed class UnityTextureEvidenceTests
    {
        private const string TempFolder = "Assets/AmuseTests_TexEvidence";

        [SetUp]
        public void SetUp()
        {
            if (!AssetDatabase.IsValidFolder(TempFolder))
            {
                AssetDatabase.CreateFolder("Assets", "AmuseTests_TexEvidence");
            }
        }

        [TearDown]
        public void TearDown()
        {
            if (AssetDatabase.IsValidFolder(TempFolder))
            {
                AssetDatabase.DeleteAsset(TempFolder);
            }
        }

        private static Texture2D Import(
            string name,
            bool sourceHasAlpha,
            System.Action<TextureImporter> configure = null)
        {
            var path = TempFolder + "/" + name + ".png";
            var format = sourceHasAlpha ? TextureFormat.RGBA32 : TextureFormat.RGB24;
            var staging = new Texture2D(4, 4, format, false);
            var pixels = new Color32[16];
            for (var i = 0; i < pixels.Length; i++)
            {
                pixels[i] = new Color32(128, 64, 32, 200);
            }

            staging.SetPixels32(pixels);
            staging.Apply();
            File.WriteAllBytes(path, staging.EncodeToPNG());
            Object.DestroyImmediate(staging);

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.mipmapEnabled = false;
            configure?.Invoke(importer);
            importer.SaveAndReimport();

            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        [Test]
        public void TryGetSourceId_ImportedTexture_ReturnsUnityAssetIdentity()
        {
            var texture = Import("identity", sourceHasAlpha: true);

            Assert.That(
                UnityTextureEvidence.TryGetSourceId(texture, out var sourceId),
                Is.True);

            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                texture, out var guid, out long localId);
            Assert.That(
                sourceId,
                Is.EqualTo(new TextureSourceId(
                    "unity-asset:" + guid.ToLowerInvariant() + ":" + localId)));
        }

        [Test]
        public void TryGetSourceId_SceneOnlyTexture_IsRefused()
        {
            var texture = new Texture2D(2, 2);
            try
            {
                Assert.That(
                    UnityTextureEvidence.TryGetSourceId(texture, out _),
                    Is.False);
            }
            finally
            {
                Object.DestroyImmediate(texture);
            }
        }

        [Test]
        public void TryGetSourceId_Null_IsRefused()
        {
            Assert.That(UnityTextureEvidence.TryGetSourceId(null, out _), Is.False);
        }

        [Test]
        public void TryGetSampling_DefaultImport_IsBilinearRepeat()
        {
            var texture = Import("sampler", sourceHasAlpha: true);
            texture.filterMode = FilterMode.Bilinear;
            texture.wrapMode = UnityEngine.TextureWrapMode.Repeat;

            Assert.That(
                UnityTextureEvidence.TryGetSampling(texture, out var sampling),
                Is.True);
            Assert.That(
                sampling,
                Is.EqualTo(new TextureSampling(
                    TextureFilterMode.Bilinear,
                    Alrauna.Amuse.Editor.Semantics.TextureWrapMode.Repeat)));
        }

        [Test]
        public void TryGetSampling_MipmappedTexture_IsRefused()
        {
            var texture = Import(
                "mipped",
                sourceHasAlpha: true,
                importer => importer.mipmapEnabled = true);

            Assert.That(UnityTextureEvidence.TryGetSampling(texture, out _), Is.False);
        }

        [Test]
        public void TryGetSampling_TrilinearFilter_IsRefused()
        {
            var texture = Import("trilinear", sourceHasAlpha: true);
            texture.filterMode = FilterMode.Trilinear;

            Assert.That(UnityTextureEvidence.TryGetSampling(texture, out _), Is.False);
        }

        [Test]
        public void TryGetSampling_MismatchedWrap_IsRefused()
        {
            var texture = Import("wrapmix", sourceHasAlpha: true);
            texture.wrapModeU = UnityEngine.TextureWrapMode.Clamp;
            texture.wrapModeV = UnityEngine.TextureWrapMode.Repeat;

            Assert.That(UnityTextureEvidence.TryGetSampling(texture, out _), Is.False);
        }

        [Test]
        public void TryGetColorInterpretation_SrgbImport_IsSrgb()
        {
            var texture = Import(
                "srgb",
                sourceHasAlpha: true,
                importer => importer.sRGBTexture = true);

            Assert.That(
                UnityTextureEvidence.TryGetColorInterpretation(texture, out var value),
                Is.True);
            Assert.That(value, Is.EqualTo(TextureColorInterpretation.Srgb));
        }

        [Test]
        public void TryGetColorInterpretation_LinearImport_IsLinear()
        {
            var texture = Import(
                "linear",
                sourceHasAlpha: true,
                importer => importer.sRGBTexture = false);

            Assert.That(
                UnityTextureEvidence.TryGetColorInterpretation(texture, out var value),
                Is.True);
            Assert.That(value, Is.EqualTo(TextureColorInterpretation.Linear));
        }

        [Test]
        public void TryProveSampledAlphaIsOne_SourceWithoutAlpha_IsProven()
        {
            var texture = Import(
                "noalpha",
                sourceHasAlpha: false,
                importer => importer.alphaSource = TextureImporterAlphaSource.None);

            Assert.That(UnityTextureEvidence.TryProveSampledAlphaIsOne(texture), Is.True);
        }

        [Test]
        public void TryProveSampledAlphaIsOne_SourceWithAlpha_IsNotProven()
        {
            var texture = Import(
                "hasalpha",
                sourceHasAlpha: true,
                importer =>
                    importer.alphaSource = TextureImporterAlphaSource.FromInput);

            Assert.That(UnityTextureEvidence.TryProveSampledAlphaIsOne(texture), Is.False);
        }

        [Test]
        public void IsCanonicalNormalMapImport_NormalMapWithoutFlip_IsCanonical()
        {
            var texture = Import(
                "normal",
                sourceHasAlpha: false,
                importer =>
                {
                    importer.textureType = TextureImporterType.NormalMap;
                    importer.flipGreenChannel = false;
                });

            Assert.That(UnityTextureEvidence.IsCanonicalNormalMapImport(texture), Is.True);
        }

        [Test]
        public void IsCanonicalNormalMapImport_FlippedGreen_IsNotCanonical()
        {
            var texture = Import(
                "normalflip",
                sourceHasAlpha: false,
                importer =>
                {
                    importer.textureType = TextureImporterType.NormalMap;
                    importer.flipGreenChannel = true;
                });

            Assert.That(UnityTextureEvidence.IsCanonicalNormalMapImport(texture), Is.False);
        }

        [Test]
        public void SharedClass_ExposesExactlyFivePublicFacts()
        {
            var methods = typeof(UnityTextureEvidence).GetMethods(
                System.Reflection.BindingFlags.Static |
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.DeclaredOnly);
            var names = new System.Collections.Generic.SortedSet<string>();
            foreach (var m in methods)
            {
                if (!m.IsPrivate)
                {
                    names.Add(m.Name);
                }
            }

            Assert.That(
                names,
                Is.EquivalentTo(new[]
                {
                    "TryGetSourceId",
                    "TryGetSampling",
                    "TryGetColorInterpretation",
                    "TryProveSampledAlphaIsOne",
                    "IsCanonicalNormalMapImport",
                }));
        }
    }
}
```

`SharedClass_ExposesExactlyFivePublicFacts` is a guard test, not a coverage test: it fails the moment someone widens the shared class beyond the five contracts that have two proven consumers.

- [ ] **Step 2: Run tests to verify they fail**

Run `refresh_unity`, then the focused run:

```text
test_names: Alrauna.Amuse.Tests.Editor.Semantics.UnityTextureEvidenceTests
```

Expected: compile failure, `UnityTextureEvidence` does not exist.

- [ ] **Step 3: Write the shared class**

Create `UnityTextureEvidence.cs`. Move the four existing Poiyomi method bodies verbatim and add the `Texture`-level sampling core.

```csharp
using System.Globalization;
using UnityEditor;
using UnityEngine;

namespace Alrauna.Amuse.Editor.Semantics
{
    /// <summary>
    /// Shader-independent Unity facts about one texture asset. Every method is
    /// a refusal predicate: it returns false, or the refusing value, whenever
    /// the fact cannot be proven from import state. It holds no shader property
    /// names, no optimization policy, and no NDMF types, and it is not an
    /// extraction framework.
    /// </summary>
    internal static class UnityTextureEvidence
    {
        /// <summary>
        /// Resolves the stable project identity of an assigned texture as
        /// <c>unity-asset:&lt;lowercase-guid&gt;:&lt;invariant-decimal-local-id&gt;</c>.
        /// Identity is never fabricated from instance id, path, name, pixels,
        /// or reference equality.
        /// </summary>
        internal static bool TryGetSourceId(
            Texture texture,
            out TextureSourceId sourceId)
        {
            sourceId = default;
            if (texture == null)
            {
                return false;
            }

            if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                    texture,
                    out var guid,
                    out long localId))
            {
                return false;
            }

            if (string.IsNullOrEmpty(guid) || IsAllZeroGuid(guid))
            {
                return false;
            }

            sourceId = new TextureSourceId(
                "unity-asset:" + guid.ToLowerInvariant() + ":" +
                localId.ToString(CultureInfo.InvariantCulture));
            return true;
        }

        /// <summary>
        /// Supported sampler state: Point or Bilinear filtering, equal
        /// Clamp/Repeat wrap on U and V, no mip chain, no mip bias, and no
        /// anisotropy.
        /// </summary>
        internal static bool TryGetSampling(
            Texture texture,
            out TextureSampling sampling)
        {
            sampling = default;
            if (texture == null)
            {
                return false;
            }

            if (!TryMapFilterMode(texture.filterMode, out var filter))
            {
                return false;
            }

            if (!TryMapWrapMode(texture.wrapModeU, out var wrapU) ||
                !TryMapWrapMode(texture.wrapModeV, out var wrapV) ||
                wrapU != wrapV)
            {
                return false;
            }

            if (texture.mipmapCount > 1 ||
                texture.mipMapBias != 0f ||
                texture.anisoLevel > 1)
            {
                return false;
            }

            sampling = new TextureSampling(filter, wrapU);
            return true;
        }

        /// <summary>
        /// Selects a colour texture's linear/sRGB import interpretation. A
        /// texture with no importer cannot prove a colour meaning.
        /// </summary>
        internal static bool TryGetColorInterpretation(
            Texture texture,
            out TextureColorInterpretation interpretation)
        {
            interpretation = default;
            if (!TryGetImporter(texture, out var importer))
            {
                return false;
            }

            interpretation = importer.sRGBTexture
                ? TextureColorInterpretation.Srgb
                : TextureColorInterpretation.Linear;
            return true;
        }

        /// <summary>
        /// Proves a sampled alpha of exactly one: the source carries no alpha
        /// channel and the importer imports none.
        /// </summary>
        internal static bool TryProveSampledAlphaIsOne(Texture texture)
        {
            if (!TryGetImporter(texture, out var importer))
            {
                return false;
            }

            return !importer.DoesSourceTextureHaveAlpha() &&
                   importer.alphaSource == TextureImporterAlphaSource.None;
        }

        /// <summary>
        /// Recognizes the canonical Unity tangent-space normal-map import: the
        /// normal-map texture type with no green-channel inversion.
        /// </summary>
        internal static bool IsCanonicalNormalMapImport(Texture texture)
        {
            if (!TryGetImporter(texture, out var importer))
            {
                return false;
            }

            return importer.textureType == TextureImporterType.NormalMap &&
                   !importer.flipGreenChannel;
        }

        private static bool TryGetImporter(
            Texture texture,
            out TextureImporter importer)
        {
            importer = null;
            if (texture == null)
            {
                return false;
            }

            var path = AssetDatabase.GetAssetPath(texture);
            if (string.IsNullOrEmpty(path))
            {
                return false;
            }

            importer = AssetImporter.GetAtPath(path) as TextureImporter;
            return importer != null;
        }

        private static bool TryMapFilterMode(
            FilterMode mode,
            out TextureFilterMode filter)
        {
            switch (mode)
            {
                case FilterMode.Point:
                    filter = TextureFilterMode.Point;
                    return true;
                case FilterMode.Bilinear:
                    filter = TextureFilterMode.Bilinear;
                    return true;
                default:
                    filter = default;
                    return false;
            }
        }

        private static bool TryMapWrapMode(
            UnityEngine.TextureWrapMode mode,
            out TextureWrapMode wrap)
        {
            switch (mode)
            {
                case UnityEngine.TextureWrapMode.Clamp:
                    wrap = TextureWrapMode.Clamp;
                    return true;
                case UnityEngine.TextureWrapMode.Repeat:
                    wrap = TextureWrapMode.Repeat;
                    return true;
                default:
                    wrap = default;
                    return false;
            }
        }

        private static bool IsAllZeroGuid(string guid)
        {
            foreach (var c in guid)
            {
                if (c != '0')
                {
                    return false;
                }
            }

            return true;
        }
    }
}
```

The class has no other members. If a body needs a fact that is not one of these five, that fact belongs in the frontend that needs it.

- [ ] **Step 4: Run tests to verify they pass**

Run `refresh_unity`, then the focused run. Expected: PASS, all 14 tests.

- [ ] **Step 5: Repoint Poiyomi**

In `PoiyomiMaterialSemantics.cs`, replace the bodies of the five public-to-tests helpers with delegations and delete the privates made strictly dead by that delegation. Keep every signature, XML doc, and call site exactly as it is. This is the only permitted change to the Poiyomi frontend in this milestone.

```csharp
internal static bool TryGetAssignedTextureSourceId(
    Texture texture,
    out TextureSourceId sourceId)
{
    return UnityTextureEvidence.TryGetSourceId(texture, out sourceId);
}

internal static bool TryGetMainTextureSampling(
    Material material,
    out TextureSampling sampling)
{
    var mainTexture = material.HasProperty(MainTextureProperty)
        ? material.GetTexture(MainTextureProperty)
        : null;
    return UnityTextureEvidence.TryGetSampling(mainTexture, out sampling);
}

internal static bool TryGetColorInterpretation(
    Texture texture,
    out TextureColorInterpretation interpretation)
{
    return UnityTextureEvidence.TryGetColorInterpretation(
        texture, out interpretation);
}

internal static bool TryProveSampledAlphaIsOne(Texture texture)
{
    return UnityTextureEvidence.TryProveSampledAlphaIsOne(texture);
}

internal static bool IsCanonicalNormalMapImport(Texture texture)
{
    return UnityTextureEvidence.IsCanonicalNormalMapImport(texture);
}
```

Delete `TryGetTextureImporter`, `TryMapFilterMode`, `TryMapWrapMode`, and `IsAllZeroGuid` from the Poiyomi file — each is now unreachable. Verify by compiling; if any is still referenced, leave it and record why. Keep `IsFinite` — the equations still use it. `TryGetMainTextureSampling` keeps its "the sampler always comes from `_MainTex`" knowledge in the Poiyomi file, where it belongs.

- [ ] **Step 6: Run the full EditMode suite**

Run `refresh_unity`, then every EditMode test with no filter.

Expected: zero failures and zero skips. Every pre-existing Poiyomi test — `PoiyomiTextureEvidenceTests`, `PoiyomiBaseColorAlphaTests`, `PoiyomiEmissionTests`, `PoiyomiNormalTests`, `PoiyomiAdversarialTests`, `PoiyomiMaterialSemanticsTests` — must pass **unmodified**. If any Poiyomi test needs editing, the extraction is not behaviour-preserving: revert the delegation and stop for review.

- [ ] **Step 7: Scope inspection**

```bash
git status --porcelain --untracked-files=all
```

Expected new paths: `UnityTextureEvidence.cs` and its `.meta`, `UnityTextureEvidenceTests.cs` and its `.meta`. Expected modified path: `PoiyomiMaterialSemantics.cs`. Confirm the Poiyomi diff contains only the five delegations plus the deleted privates:

```bash
git diff -- Packages/com.alrauna.amuse/Editor/Semantics/Poiyomi/PoiyomiMaterialSemantics.cs
```

Do not stage and do not commit.

---

### Task 2: Source attestation

Identity, canonicalization, digests, exact version, and the live `LIL_RENDER` read. There is no family table: one supported shader, everything else refused.

**Files:**

- Create: `Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonSourceAttestation.cs`
- Create: `Packages/com.alrauna.amuse/Tests/Editor/Semantics/LilToon/LilToonAttestationTests.cs`

**Interfaces:**

- Consumes: `LilToonSemanticDiagnostic`, `LilToonSemanticOutput`, `LilToonSemanticDiagnosticCode` — declare these three types first, at the top of `LilToonMaterialSemantics.cs`, exactly as specified in Task 3. They are tiny and have no dependencies.
- Produces:

```csharp
internal sealed class LilToonSourceEvidence
{
    internal LilToonSourceEvidence(
        string shaderName,
        string assetGuid,
        bool hasShaderFormatVersion,
        float shaderFormatVersion,
        bool hasPackage,
        string packageName,
        string packageVersion,
        string passShaderGuid,
        string shaderCanonicalDigest,
        string passCanonicalDigest,
        string includeTreeDigest,
        bool hasRenderMode,
        int renderMode,
        IReadOnlyCollection<string> compiledFeatures);
}

internal static class LilToonSourceAttestation
{
    internal const string SupportedShaderName = "lilToon";
    internal const string SupportedShaderGuid = "df12117ecd77c31469c224178886498e";
    internal const string PassShaderName = "Hidden/ltspass_opaque";
    internal const string PassShaderGuid = "61b4f98a5d78b4a4a9d89180fac793fc";
    internal const string PackageName = "jp.lilxyzw.liltoon";
    internal const string PackageVersion = "2.3.4";
    internal const float ShaderFormatVersion = 45f;
    internal const string ShaderCanonicalDigest = "5206bec25e82db5f8009b27fcc5ba94d7c41113031d4b6b0a2c25ca324a9c704";
    internal const string PassCanonicalDigest = "6b6c30c1cbe546fe753bcdc77f547441e3f9114ee80e9591bde2b8e6e7e5eb14";
    internal const string IncludeTreeDigest = "6e2dce6cb3073d5e04b569a14df8e0944c93ca408999fb42d7c717050c48fd46";

    internal static string ComputeNormalizedSourceHash(string rawSource);
    internal static string Canonicalize(
        string rawShaderSource,
        string shaderDirectory,
        string projectRoot,
        LilToonIncludeTree includeTree);
    internal static string ComputeIncludeTreeDigest(IReadOnlyList<(string RelativePath, string Hash)> files);
    internal static IReadOnlyCollection<string> ScanCompiledFeatures(string passShaderSource);
    internal static bool TryScanRenderMode(string passShaderSource, out int renderMode);
    internal static bool TryVerifyLilToonIdentity(
        LilToonSourceEvidence evidence,
        out LilToonSemanticDiagnostic diagnostic);
    internal static LilToonSourceEvidence GatherSourceEvidence(Material material);
}
```

The three digest constants are the values Task 0 measured on 2026-08-18. Do not substitute any other source for them.

`LilToonIncludeTree` is a small value type produced while digesting the include directory. It answers one question — "does this resolved absolute path name a file inside the attested tree, and what is its path relative to the tree root?" — and is what makes R3 identity-based rather than basename-based:

```csharp
internal sealed class LilToonIncludeTree
{
    internal string RootFullPath { get; }
    internal IReadOnlyList<(string RelativePath, string Hash)> Files { get; }
    internal bool TryGetRelativePath(string fullPath, out string relativePath);
}
```

- [ ] **Step 1: Write the failing tests**

Create `LilToonAttestationTests.cs`:

```csharp
using System.Collections.Generic;
using Alrauna.Amuse.Editor.Semantics.LilToon;
using NUnit.Framework;

namespace Alrauna.Amuse.Tests.Editor.Semantics.LilToon
{
    public sealed class LilToonAttestationTests
    {
        private const string ProjectRoot = "C:/AmuseTests/Project";
        private const string ShaderDir =
            ProjectRoot + "/Packages/jp.lilxyzw.liltoon/Shader";

        /// <summary>
        /// A stand-in attested tree holding two files, one of them nested, so
        /// tests can exercise identity-based include resolution without a real
        /// lilToon install.
        /// </summary>
        private static LilToonIncludeTree Tree()
        {
            return LilToonIncludeTree.ForTests(
                ShaderDir + "/Includes",
                new[]
                {
                    ("lil_common.hlsl", "11"),
                    ("VRC Light Volumes/LightVolumes.cginc", "22"),
                });
        }

        private static string Canon(string source)
        {
            // The project root is an explicit argument; canonicalization must
            // never consult the process working directory.
            return LilToonSourceAttestation.Canonicalize(
                source, ShaderDir, ProjectRoot, Tree());
        }

        private static LilToonSourceEvidence Evidence(
            string shaderName = "lilToon",
            string assetGuid = "df12117ecd77c31469c224178886498e",
            bool hasVersion = true,
            float version = 45f,
            bool hasPackage = true,
            string packageName = "jp.lilxyzw.liltoon",
            string packageVersion = "2.3.4",
            string passGuid = "61b4f98a5d78b4a4a9d89180fac793fc",
            string shaderDigest = null,
            string passDigest = null,
            string includeDigest = null,
            bool hasRenderMode = true,
            int renderMode = 0,
            IReadOnlyCollection<string> features = null)
        {
            return new LilToonSourceEvidence(
                shaderName,
                assetGuid,
                hasVersion,
                version,
                hasPackage,
                packageName,
                packageVersion,
                passGuid,
                shaderDigest ?? LilToonSourceAttestation.ShaderCanonicalDigest,
                passDigest ?? LilToonSourceAttestation.PassCanonicalDigest,
                includeDigest ?? LilToonSourceAttestation.IncludeTreeDigest,
                hasRenderMode,
                renderMode,
                features ?? new string[0]);
        }

        // --- normalized hashing ---

        [Test]
        public void ComputeNormalizedSourceHash_IgnoresBomAndLineEndings()
        {
            const string lf = "float4 c;\nreturn c;\n";
            var crlf = "﻿" + lf.Replace("\n", "\r\n");
            var cr = lf.Replace("\n", "\r");
            var expected =
                LilToonSourceAttestation.ComputeNormalizedSourceHash(lf);

            Assert.That(
                LilToonSourceAttestation.ComputeNormalizedSourceHash(crlf),
                Is.EqualTo(expected));
            Assert.That(
                LilToonSourceAttestation.ComputeNormalizedSourceHash(cr),
                Is.EqualTo(expected));
        }

        [Test]
        public void ComputeNormalizedSourceHash_DetectsContentEdit()
        {
            Assert.That(
                LilToonSourceAttestation.ComputeNormalizedSourceHash("a\n"),
                Is.Not.EqualTo(
                    LilToonSourceAttestation.ComputeNormalizedSourceHash("b\n")));
        }

        // --- canonicalization (R1, R2, R3) ---

        [Test]
        public void Canonicalize_DropsSettingRegionInsideHlslInclude()
        {
            const string withFeatures =
                "SubShader\n{\n    HLSLINCLUDE\n" +
                "        #define LIL_FEATURE_MAIN2ND\n" +
                "        #define LIL_OPTIMIZE_USE_FORWARDADD\n" +
                "        #pragma skip_variants LIGHTMAP_ON DYNAMICLIGHTMAP_ON\n" +
                "        #pragma target 3.5\n" +
                "    ENDHLSL\n}\n";
            const string withoutFeatures =
                "SubShader\n{\n    HLSLINCLUDE\n" +
                "        #pragma target 3.5\n" +
                "    ENDHLSL\n}\n";

            Assert.That(Canon(withFeatures), Is.EqualTo(Canon(withoutFeatures)));
        }

        [Test]
        public void Canonicalize_InjectedFeatureDefineAfterSettingRegion_IsRetained()
        {
            // The setting region ends at the first line that is neither a
            // valueless feature define nor a skip_variants pragma. A same-shaped
            // line after that point is not generator output and stays hashed.
            const string clean =
                "    HLSLINCLUDE\n" +
                "        #define LIL_FEATURE_MAIN2ND\n" +
                "        #pragma target 3.5\n" +
                "    ENDHLSL\n";
            const string injected =
                "    HLSLINCLUDE\n" +
                "        #define LIL_FEATURE_MAIN2ND\n" +
                "        #pragma target 3.5\n" +
                "        #define LIL_FEATURE_BumpMap\n" +
                "    ENDHLSL\n";

            Assert.That(Canon(injected), Is.Not.EqualTo(Canon(clean)));
            Assert.That(Canon(injected), Does.Contain("LIL_FEATURE_BumpMap"));
        }

        [Test]
        public void Canonicalize_InjectedFeatureDefineInPassBody_IsRetained()
        {
            const string clean =
                "Pass\n{\n    HLSLPROGRAM\n" +
                "    #pragma vertex vert\n" +
                "    #include \"Includes/lil_common.hlsl\"\n" +
                "    ENDHLSL\n}\n";
            const string injected =
                "Pass\n{\n    HLSLPROGRAM\n" +
                "    #pragma vertex vert\n" +
                "    #include \"Includes/lil_common.hlsl\"\n" +
                "    #define LIL_FEATURE_EmissionMap\n" +
                "    ENDHLSL\n}\n";

            Assert.That(Canon(injected), Is.Not.EqualTo(Canon(clean)));
            Assert.That(Canon(injected), Does.Contain("LIL_FEATURE_EmissionMap"));
        }

        [Test]
        public void Canonicalize_InjectedSkipVariantsOutsideSettingRegion_IsRetained()
        {
            const string clean =
                "Pass\n{\n    HLSLPROGRAM\n" +
                "    #pragma vertex vert\n" +
                "    #include \"Includes/lil_common.hlsl\"\n" +
                "    ENDHLSL\n}\n";
            const string injected =
                "Pass\n{\n    HLSLPROGRAM\n" +
                "    #pragma vertex vert\n" +
                "    #include \"Includes/lil_common.hlsl\"\n" +
                "    #pragma skip_variants EVIL\n" +
                "    ENDHLSL\n}\n";

            Assert.That(Canon(injected), Is.Not.EqualTo(Canon(clean)));
        }

        [Test]
        public void Canonicalize_ConstantSkipVariantsAfterSettingRegion_IsRetained()
        {
            // GetSkipVariants{Decals,AddLightShadows,ProbeVolumes,AO} return
            // fixed literals, so their emitted lines are stable across settings
            // and must be hashed rather than dropped.
            const string withTail =
                "    HLSLINCLUDE\n" +
                "        #define LIL_FEATURE_MAIN2ND\n" +
                "        #pragma target 3.5\n" +
                "        #pragma skip_variants DECALS_OFF DECALS_3RT\n" +
                "    ENDHLSL\n";
            const string withoutTail =
                "    HLSLINCLUDE\n" +
                "        #define LIL_FEATURE_MAIN2ND\n" +
                "        #pragma target 3.5\n" +
                "    ENDHLSL\n";

            Assert.That(Canon(withTail), Is.Not.EqualTo(Canon(withoutTail)));
        }

        // --- R2: the shadow substitution slot ---

        /// <summary>
        /// The generated Forward prologue, with the shadow slot either filled
        /// or absent. lilToon emits no line at all when useBaseShadow is true,
        /// so the two forms differ by exactly one line.
        /// </summary>
        private static string ForwardPrologue(string shadowSlotLine)
        {
            return
                "    HLSLPROGRAM\n" +
                "            #pragma vertex vert\n" +
                "            #pragma fragment frag\n" +
                "            #pragma multi_compile_fwdbase\n" +
                "            #pragma multi_compile_vertex _ FOG_LINEAR FOG_EXP FOG_EXP2\n" +
                "            #pragma multi_compile_instancing\n" +
                "            #define LIL_PASS_FORWARD\n" +
                shadowSlotLine +
                "\n" +
                "            #include \"Includes/lil_common.hlsl\"\n" +
                "    ENDHLSL\n";
        }

        [Test]
        public void Canonicalize_ShadowSlotPragma_IsDropped()
        {
            // useBaseShadow == false emits the reduced expansion at the slot;
            // useBaseShadow == true emits nothing. Both must canonicalize alike.
            var filled = ForwardPrologue(
                "            #pragma skip_variants SHADOW_VERY_HIGH\n");
            var absent = ForwardPrologue(string.Empty);

            Assert.That(Canon(filled), Is.EqualTo(Canon(absent)));
            Assert.That(Canon(filled), Does.Not.Contain("SHADOW_VERY_HIGH"));
        }

        [Test]
        public void Canonicalize_ShadowSlotAbsentForm_NeedsNoBlankLineRule()
        {
            // Task 0 showed the empty expansion leaves no indentation-only
            // residue, so the absent form must already be canonical.
            var absent = ForwardPrologue(string.Empty);

            Assert.That(Canon(absent), Is.EqualTo(absent.TrimEnd('\n')));
        }

        [Test]
        public void Canonicalize_ShadowPragmaAwayFromSlot_IsRetained()
        {
            // Same pragma, one line further down: not the substitution slot.
            var offSlot =
                "    HLSLPROGRAM\n" +
                "            #pragma vertex vert\n" +
                "            #define LIL_PASS_FORWARD\n" +
                "\n" +
                "            #pragma skip_variants SHADOW_VERY_HIGH\n" +
                "            #include \"Includes/lil_common.hlsl\"\n" +
                "    ENDHLSL\n";
            var without =
                "    HLSLPROGRAM\n" +
                "            #pragma vertex vert\n" +
                "            #define LIL_PASS_FORWARD\n" +
                "\n" +
                "            #include \"Includes/lil_common.hlsl\"\n" +
                "    ENDHLSL\n";

            Assert.That(Canon(offSlot), Is.Not.EqualTo(Canon(without)));
            Assert.That(Canon(offSlot), Does.Contain("SHADOW_VERY_HIGH"));
        }

        [Test]
        public void Canonicalize_ShadowPragmaAfterDifferentDefine_IsRetained()
        {
            var afterOtherDefine =
                "            #define LIL_PASS_FORWARDADD\n" +
                "            #pragma skip_variants SHADOW_VERY_HIGH\n";

            Assert.That(
                Canon(afterOtherDefine), Does.Contain("SHADOW_VERY_HIGH"));
        }

        [Test]
        public void Canonicalize_UnrelatedKeywordAtSlot_IsRetained()
        {
            // Correct anchor, wrong keyword. SHADOW_VERY_HIGH is the entire
            // generator-produced domain at this slot, so anything else is not
            // generator output and must stay hashed.
            var unrelated = ForwardPrologue(
                "            #pragma skip_variants AMUSE_UNRELATED_KEYWORD\n");
            var absent = ForwardPrologue(string.Empty);

            Assert.That(Canon(unrelated), Is.Not.EqualTo(Canon(absent)));
            Assert.That(Canon(unrelated), Does.Contain("AMUSE_UNRELATED_KEYWORD"));
        }

        [Test]
        public void Canonicalize_MultiKeywordPragmaAtSlot_IsRetained()
        {
            // The dedup pass reduces a surviving line to one keyword, so a
            // multi-keyword line at the slot is not generator output either.
            var multi = ForwardPrologue(
                "            #pragma skip_variants SHADOW_HIGH SHADOW_VERY_HIGH\n");
            var absent = ForwardPrologue(string.Empty);

            Assert.That(Canon(multi), Is.Not.EqualTo(Canon(absent)));
        }

        [Test]
        public void Canonicalize_ShadowPragmaInPassBody_IsRetained()
        {
            var injected =
                "    HLSLPROGRAM\n" +
                "            #define LIL_PASS_FORWARD\n" +
                "            #include \"Includes/lil_common.hlsl\"\n" +
                "            #pragma skip_variants SHADOW_VERY_HIGH\n" +
                "    ENDHLSL\n";

            Assert.That(Canon(injected), Does.Contain("SHADOW_VERY_HIGH"));
        }

        [Test]
        public void Canonicalize_RetainsValuedDefines()
        {
            Assert.That(
                Canon("    HLSLINCLUDE\n        #define LIL_RENDER 0\n    ENDHLSL\n"),
                Is.Not.EqualTo(
                    Canon("    HLSLINCLUDE\n        #define LIL_RENDER 2\n    ENDHLSL\n")));
        }

        [Test]
        public void Canonicalize_ShaderScopeRenderDefine_IsNeverASettingCandidate()
        {
            // A valued define is not a region-A line, so the run is empty and
            // LIL_RENDER survives even as the first line of an HLSLINCLUDE —
            // which is exactly its position in ltspass_opaque.lilinternal.
            var canon = Canon(
                "    HLSLINCLUDE\n" +
                "        #define LIL_RENDER 0\n" +
                "        #define LIL_FEATURE_MAIN2ND\n" +
                "    ENDHLSL\n");

            Assert.That(canon, Does.Contain("#define LIL_RENDER 0"));
            Assert.That(canon, Does.Contain("LIL_FEATURE_MAIN2ND"));
        }

        [Test]
        public void Canonicalize_NormalizesAttestedIncludeRegardlessOfPrefix()
        {
            const string relative = "#include \"Includes/lil_common.hlsl\"\n";
            const string packaged =
                "#include \"Packages/jp.lilxyzw.liltoon/Shader/Includes/lil_common.hlsl\"\n";

            Assert.That(Canon(packaged), Is.EqualTo(Canon(relative)));
            Assert.That(Canon(packaged), Does.Contain("Includes/lil_common.hlsl"));
        }

        [Test]
        public void Canonicalize_PreservesSubdirectoryWithinAttestedTree()
        {
            var canon = Canon(
                "#include \"Includes/VRC Light Volumes/LightVolumes.cginc\"\n");

            Assert.That(
                canon,
                Does.Contain("Includes/VRC Light Volumes/LightVolumes.cginc"));
        }

        [Test]
        public void Canonicalize_RedirectedIncludeWithSameBasename_IsNotNormalized()
        {
            // The redirect attack: an identically named file in another
            // directory. Basename matching would canonicalize it to the trusted
            // include while the Includes-tree digest stayed clean.
            const string trusted = "#include \"Includes/lil_common.hlsl\"\n";
            const string redirected = "#include \"Evil/lil_common.hlsl\"\n";

            Assert.That(Canon(redirected), Is.Not.EqualTo(Canon(trusted)));
            Assert.That(Canon(redirected), Does.Contain("Evil/lil_common.hlsl"));
        }

        [Test]
        public void Canonicalize_IncludeEscapingTreeByTraversal_IsNotNormalized()
        {
            const string trusted = "#include \"Includes/lil_common.hlsl\"\n";
            const string escaped =
                "#include \"Includes/../../Evil/Includes/lil_common.hlsl\"\n";

            Assert.That(Canon(escaped), Is.Not.EqualTo(Canon(trusted)));
        }

        [Test]
        public void Canonicalize_DifferentlyCasedIncludePath_IsNotNormalized()
        {
            // Exact ordinal identity: a casing difference cannot silently
            // assume the identity of an attested path, even where the
            // filesystem would resolve both to one file.
            const string trusted = "#include \"Includes/lil_common.hlsl\"\n";
            const string cased = "#include \"Includes/LIL_COMMON.HLSL\"\n";

            Assert.That(Canon(cased), Is.Not.EqualTo(Canon(trusted)));
            Assert.That(Canon(cased), Does.Contain("Includes/LIL_COMMON.HLSL"));
        }

        [Test]
        public void Canonicalize_CommentedIncludeIsNormalizedToo()
        {
            // lilToon rewrites the "Includes prefix textually, so commented
            // include directives vary with install path as well.
            const string relative = "//#include \"Includes/lil_common.hlsl\"\n";
            const string packaged =
                "//#include \"Packages/jp.lilxyzw.liltoon/Shader/Includes/lil_common.hlsl\"\n";

            Assert.That(Canon(packaged), Is.EqualTo(Canon(relative)));
        }

        [Test]
        public void Canonicalize_NonIncludeQuotedStrings_AreUntouched()
        {
            const string source =
                "Fallback \"Includes/lil_common.hlsl\"\n" +
                "CustomEditor \"lilToon.lilToonInspector\"\n";

            Assert.That(Canon(source), Does.Contain("Fallback \"Includes/lil_common.hlsl\""));
        }

        [Test]
        public void Canonicalize_RetainsInjectedPassBody()
        {
            const string clean = "Pass\n{\n    HLSLPROGRAM\n    ENDHLSL\n}\n";
            const string injected =
                "Pass\n{\n    HLSLPROGRAM\n    fd.col.rgb = 0;\n    ENDHLSL\n}\n";

            Assert.That(Canon(injected), Is.Not.EqualTo(Canon(clean)));
        }

        // --- include tree digest ---

        [Test]
        public void ComputeIncludeTreeDigest_IsOrderIndependent()
        {
            var a = new List<(string, string)>
            {
                ("b.hlsl", "22"), ("a.hlsl", "11"),
            };
            var b = new List<(string, string)>
            {
                ("a.hlsl", "11"), ("b.hlsl", "22"),
            };

            Assert.That(
                LilToonSourceAttestation.ComputeIncludeTreeDigest(a),
                Is.EqualTo(
                    LilToonSourceAttestation.ComputeIncludeTreeDigest(b)));
        }

        [Test]
        public void ComputeIncludeTreeDigest_DetectsAddedFile()
        {
            var baseline = new List<(string, string)> { ("a.hlsl", "11") };
            var extra = new List<(string, string)>
            {
                ("a.hlsl", "11"), ("z.hlsl", "99"),
            };

            Assert.That(
                LilToonSourceAttestation.ComputeIncludeTreeDigest(extra),
                Is.Not.EqualTo(
                    LilToonSourceAttestation.ComputeIncludeTreeDigest(baseline)));
        }

        // --- define and render-mode scans ---

        [Test]
        public void ScanCompiledFeatures_ReadsValuelessFeatureSymbolsOnly()
        {
            const string source =
                "        #define LIL_RENDER 0\n" +
                "        #define LIL_FEATURE_NORMAL_1ST\n" +
                "        #define LIL_FEATURE_BumpMap\n" +
                "        //#define LIL_FEATURE_EMISSION_1ST\n" +
                "        #define LIL_PASS_FORWARD\n";

            var features = LilToonSourceAttestation.ScanCompiledFeatures(source);

            Assert.That(features, Contains.Item("LIL_FEATURE_NORMAL_1ST"));
            Assert.That(features, Contains.Item("LIL_FEATURE_BumpMap"));
            Assert.That(features, Does.Not.Contain("LIL_FEATURE_EMISSION_1ST"));
            Assert.That(features, Does.Not.Contain("LIL_RENDER"));
            Assert.That(features, Does.Not.Contain("LIL_PASS_FORWARD"));
        }

        [Test]
        public void ScanCompiledFeatures_NullSource_IsEmpty()
        {
            Assert.That(
                LilToonSourceAttestation.ScanCompiledFeatures(null), Is.Empty);
        }

        [Test]
        public void TryScanRenderMode_SingleDefine_ReadsValue()
        {
            Assert.That(
                LilToonSourceAttestation.TryScanRenderMode(
                    "        #define LIL_RENDER 0\n", out var mode),
                Is.True);
            Assert.That(mode, Is.EqualTo(0));
        }

        [Test]
        public void TryScanRenderMode_TransparentPass_ReadsTwo()
        {
            Assert.That(
                LilToonSourceAttestation.TryScanRenderMode(
                    "#define LIL_RENDER 2\n", out var mode),
                Is.True);
            Assert.That(mode, Is.EqualTo(2));
        }

        [TestCase("")]
        [TestCase("#define LIL_RENDER\n")]
        [TestCase("#define LIL_RENDER x\n")]
        [TestCase("#define LIL_RENDER 0\n#define LIL_RENDER 1\n")]
        public void TryScanRenderMode_AmbiguousOrMissing_IsRefused(string source)
        {
            Assert.That(
                LilToonSourceAttestation.TryScanRenderMode(source, out _),
                Is.False);
        }

        // --- verification conjunction ---

        [Test]
        public void Verify_CanonicalEvidence_Succeeds()
        {
            Assert.That(
                LilToonSourceAttestation.TryVerifyLilToonIdentity(
                    Evidence(), out var diagnostic),
                Is.True);
            Assert.That(diagnostic, Is.Null);
        }

        [TestCase("Standard")]
        [TestCase("Hidden/lilToonCutout")]
        [TestCase("Hidden/lilToonTransparent")]
        [TestCase("_lil/lilToonMulti")]
        [TestCase("lilToon ")]
        public void Verify_UnsupportedShaderName_IsRefused(string name)
        {
            Assert.That(
                LilToonSourceAttestation.TryVerifyLilToonIdentity(
                    Evidence(shaderName: name), out var diagnostic),
                Is.False);
            Assert.That(
                diagnostic.Code,
                Is.EqualTo(LilToonSemanticDiagnosticCode.UnsupportedShader));
            Assert.That(
                diagnostic.Output,
                Is.EqualTo(LilToonSemanticOutput.Material));
        }

        // --- _lilToonVersion exactness ---

        [Test]
        public void Verify_ExactVersion_IsAccepted()
        {
            Assert.That(
                LilToonSourceAttestation.TryVerifyLilToonIdentity(
                    Evidence(version: 45f), out _),
                Is.True);
        }

        [Test]
        public void Verify_NearbyVersionValues_AreRefused()
        {
            var justBelow = 45f - float.Epsilon * 45f;
            var nextAbove = System.BitConverter.Int32BitsToSingle(
                System.BitConverter.SingleToInt32Bits(45f) + 1);
            var justUnder = System.BitConverter.Int32BitsToSingle(
                System.BitConverter.SingleToInt32Bits(45f) - 1);

            foreach (var value in new[]
                     {
                         44f, 46f, 44.999f, 45.001f,
                         justBelow, nextAbove, justUnder,
                         float.NaN, float.PositiveInfinity,
                         float.NegativeInfinity,
                     })
            {
                Assert.That(
                    LilToonSourceAttestation.TryVerifyLilToonIdentity(
                        Evidence(version: value), out var diagnostic),
                    Is.False,
                    "version " + value.ToString("R"));
                Assert.That(
                    diagnostic.Code,
                    Is.EqualTo(LilToonSemanticDiagnosticCode.UnsupportedVersion));
            }
        }

        [Test]
        public void Verify_MissingVersionProperty_IsRefused()
        {
            Assert.That(
                LilToonSourceAttestation.TryVerifyLilToonIdentity(
                    Evidence(hasVersion: false, version: 45f), out var diagnostic),
                Is.False);
            Assert.That(
                diagnostic.Code,
                Is.EqualTo(LilToonSemanticDiagnosticCode.UnsupportedVersion));
        }

        // --- remaining conjuncts ---

        [Test]
        public void Verify_GuidMismatch_IsRefused()
        {
            Assert.That(
                LilToonSourceAttestation.TryVerifyLilToonIdentity(
                    Evidence(assetGuid: new string('0', 32)), out var diagnostic),
                Is.False);
            Assert.That(
                diagnostic.Code,
                Is.EqualTo(LilToonSemanticDiagnosticCode.UnsupportedShader));
        }

        [Test]
        public void Verify_WrongPackageVersion_IsRefused()
        {
            Assert.That(
                LilToonSourceAttestation.TryVerifyLilToonIdentity(
                    Evidence(packageVersion: "2.3.3"), out var diagnostic),
                Is.False);
            Assert.That(
                diagnostic.Code,
                Is.EqualTo(LilToonSemanticDiagnosticCode.UnsupportedVersion));
        }

        [Test]
        public void Verify_LegacyAssetsInstall_SkipsPackageCheck()
        {
            Assert.That(
                LilToonSourceAttestation.TryVerifyLilToonIdentity(
                    Evidence(
                        hasPackage: false,
                        packageName: null,
                        packageVersion: null),
                    out _),
                Is.True);
        }

        [Test]
        public void Verify_EditedIncludeTree_IsRefused()
        {
            Assert.That(
                LilToonSourceAttestation.TryVerifyLilToonIdentity(
                    Evidence(includeDigest: new string('0', 64)),
                    out var diagnostic),
                Is.False);
            Assert.That(
                diagnostic.Code,
                Is.EqualTo(LilToonSemanticDiagnosticCode.ModifiedShaderSource));
            Assert.That(diagnostic.Detail, Does.Contain("Includes"));
        }

        [Test]
        public void Verify_EditedPassAsset_IsRefused()
        {
            Assert.That(
                LilToonSourceAttestation.TryVerifyLilToonIdentity(
                    Evidence(passDigest: new string('0', 64)), out var diagnostic),
                Is.False);
            Assert.That(
                diagnostic.Code,
                Is.EqualTo(LilToonSemanticDiagnosticCode.ModifiedShaderSource));
        }

        [Test]
        public void Verify_EditedMaterialShaderAsset_IsRefused()
        {
            Assert.That(
                LilToonSourceAttestation.TryVerifyLilToonIdentity(
                    Evidence(shaderDigest: new string('0', 64)),
                    out var diagnostic),
                Is.False);
            Assert.That(
                diagnostic.Code,
                Is.EqualTo(LilToonSemanticDiagnosticCode.ModifiedShaderSource));
        }

        [Test]
        public void Verify_MissingDigestEvidence_IsRefused()
        {
            Assert.That(
                LilToonSourceAttestation.TryVerifyLilToonIdentity(
                    Evidence(includeDigest: null), out var diagnostic),
                Is.False);
            Assert.That(
                diagnostic.Code,
                Is.EqualTo(LilToonSemanticDiagnosticCode.MissingSourceEvidence));
        }

        [Test]
        public void Verify_WrongPassShaderGuid_IsRefused()
        {
            Assert.That(
                LilToonSourceAttestation.TryVerifyLilToonIdentity(
                    Evidence(passGuid: new string('0', 32)), out var diagnostic),
                Is.False);
            Assert.That(
                diagnostic.Code,
                Is.EqualTo(LilToonSemanticDiagnosticCode.MissingSourceEvidence));
        }

        // --- live LIL_RENDER ---

        [TestCase(1)]
        [TestCase(2)]
        public void Verify_NonOpaqueRenderMode_IsRefused(int mode)
        {
            Assert.That(
                LilToonSourceAttestation.TryVerifyLilToonIdentity(
                    Evidence(renderMode: mode), out var diagnostic),
                Is.False);
            Assert.That(
                diagnostic.Code,
                Is.EqualTo(
                    LilToonSemanticDiagnosticCode.UnsupportedShaderVariant));
            Assert.That(diagnostic.Detail, Does.Contain("LIL_RENDER"));
        }

        [Test]
        public void Verify_UnreadableRenderMode_IsRefused()
        {
            Assert.That(
                LilToonSourceAttestation.TryVerifyLilToonIdentity(
                    Evidence(hasRenderMode: false, renderMode: 0),
                    out var diagnostic),
                Is.False);
            Assert.That(
                diagnostic.Code,
                Is.EqualTo(
                    LilToonSemanticDiagnosticCode.UnsupportedShaderVariant));
        }
    }
}
```

`Verify_NearbyVersionValues_AreRefused` is the regression test for the `Mathf.RoundToInt` defect: every listed value rounds to 45 or is otherwise malformed, and every one must refuse.

`Canonicalize_RetainsValuedDefines` and `Canonicalize_RetainsInjectedPassBody` are the regression tests for attack vectors 1 and 3 in the design spec's sufficiency argument.

`Canonicalize_InjectedFeatureDefineAfterSettingRegion_IsRetained`, `Canonicalize_InjectedFeatureDefineInPassBody_IsRetained`, and `Canonicalize_InjectedSkipVariantsOutsideSettingRegion_IsRetained` are the region-A boundary regressions: each fails the moment R1 is loosened back to a whole-file rule.

`Canonicalize_RedirectedIncludeWithSameBasename_IsNotNormalized`, `Canonicalize_IncludeEscapingTreeByTraversal_IsNotNormalized`, and `Canonicalize_DifferentlyCasedIncludePath_IsNotNormalized` are the R3 identity regressions: the first two fail if path identity is weakened back to basename matching, the third fails if the trusted map reverts to `OrdinalIgnoreCase`.

`Canonicalize_UnrelatedKeywordAtSlot_IsRetained` and `Canonicalize_MultiKeywordPragmaAtSlot_IsRetained` are the keyword-domain regressions: each fails if R2 is loosened to "any skip_variants after the anchor".

The R2 slot tests are the calibration set. `Canonicalize_ShadowSlotPragma_IsDropped` proves the slot is covered for both `useBaseShadow` values; `Canonicalize_ShadowSlotAbsentForm_NeedsNoBlankLineRule` proves the absent form needs no whitespace normalization, which Task 0 established; and `Canonicalize_ShadowPragmaAwayFromSlot_IsRetained`, `Canonicalize_ShadowPragmaAfterDifferentDefine_IsRetained`, and `Canonicalize_ShadowPragmaInPassBody_IsRetained` prove the anchor is not a licence to drop the same pragma elsewhere. `Canonicalize_ConstantSkipVariantsAfterSettingRegion_IsRetained` keeps the four constant generator expansions hashed.

- [ ] **Step 2: Run tests to verify they fail**

Run `refresh_unity`, then:

```text
test_names: Alrauna.Amuse.Tests.Editor.Semantics.LilToon.LilToonAttestationTests
```

Expected: compile failure, `LilToonSourceAttestation` does not exist.

- [ ] **Step 3: Write the attestation class**

Create `LilToonSourceAttestation.cs`. The three digest constants below are the values Task 0 measured; there is no other valid source for them.

```csharp
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace Alrauna.Amuse.Editor.Semantics.LilToon
{
    /// <summary>
    /// Already-read lilToon identity evidence. Separating extraction from the
    /// identity decision keeps the conjunction deterministically testable
    /// without a live Unity asset or an installed lilToon package.
    /// </summary>
    /// <summary>
    /// The attested lilToon include directory: its root, the digested file
    /// listing, and the one question R3 needs answered — does this resolved
    /// absolute path name a file inside the tree, and where inside it? Answering
    /// by identity rather than by basename is what stops a redirected include
    /// from canonicalizing to a trusted one.
    /// </summary>
    internal sealed class LilToonIncludeTree
    {
        private readonly Dictionary<string, string> _byFullPath;

        internal string RootFullPath { get; }
        internal IReadOnlyList<(string RelativePath, string Hash)> Files { get; }

        private LilToonIncludeTree(
            string rootFullPath,
            IReadOnlyList<(string RelativePath, string Hash)> files,
            Dictionary<string, string> byFullPath)
        {
            RootFullPath = rootFullPath;
            Files = files;
            _byFullPath = byFullPath;
        }

        internal static LilToonIncludeTree Empty()
        {
            return new LilToonIncludeTree(
                null,
                new (string, string)[0],
                new Dictionary<string, string>(PathComparer));
        }

        internal static LilToonIncludeTree Enumerate(
            string includeFolder,
            Func<string, string> readTextOrNull,
            Func<string, string> hash)
        {
            if (string.IsNullOrEmpty(includeFolder) ||
                !Directory.Exists(includeFolder))
            {
                return Empty();
            }

            var root = Path.GetFullPath(includeFolder);
            var files = new List<(string, string)>();
            var byFullPath = new Dictionary<string, string>(PathComparer);

            foreach (var path in Directory.GetFiles(
                         includeFolder, "*", SearchOption.AllDirectories))
            {
                if (path.EndsWith(".meta", StringComparison.Ordinal))
                {
                    continue;
                }

                var text = readTextOrNull(path);
                if (text == null)
                {
                    continue;
                }

                var full = Path.GetFullPath(path);
                var relative = full
                    .Substring(root.Length)
                    .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                    .Replace(Path.DirectorySeparatorChar, '/');

                files.Add((relative, hash(text)));
                byFullPath[full] = relative;
            }

            return new LilToonIncludeTree(root, files, byFullPath);
        }

        /// <summary>Test seam: build a tree without touching the file system.</summary>
        internal static LilToonIncludeTree ForTests(
            string rootPath,
            IReadOnlyList<(string RelativePath, string Hash)> files)
        {
            var root = Path.GetFullPath(rootPath);
            var byFullPath = new Dictionary<string, string>(PathComparer);
            foreach (var file in files)
            {
                byFullPath[Path.GetFullPath(Path.Combine(root, file.RelativePath))] =
                    file.RelativePath;
            }

            return new LilToonIncludeTree(root, files, byFullPath);
        }

        internal bool TryGetRelativePath(string fullPath, out string relativePath)
        {
            relativePath = null;
            return fullPath != null &&
                   _byFullPath.TryGetValue(fullPath, out relativePath);
        }

        // Exact ordinal identity. A casing difference therefore fails to
        // resolve and the line stays unnormalized, so the digest refuses even
        // on a case-insensitive filesystem where both paths name one file. That
        // false negative is deliberate: case-insensitive matching would let
        // Includes/LIL_COMMON.HLSL assume the identity of an attested
        // Includes/lil_common.hlsl. No filesystem detection is added.
        private static StringComparer PathComparer => StringComparer.Ordinal;
    }

    internal sealed class LilToonSourceEvidence
    {
        internal string ShaderName { get; }
        internal string AssetGuid { get; }
        internal bool HasShaderFormatVersion { get; }
        internal float ShaderFormatVersion { get; }
        internal bool HasPackage { get; }
        internal string PackageName { get; }
        internal string PackageVersion { get; }
        internal string PassShaderGuid { get; }
        internal string ShaderCanonicalDigest { get; }
        internal string PassCanonicalDigest { get; }
        internal string IncludeTreeDigest { get; }
        internal bool HasRenderMode { get; }
        internal int RenderMode { get; }
        internal IReadOnlyCollection<string> CompiledFeatures { get; }

        internal LilToonSourceEvidence(
            string shaderName,
            string assetGuid,
            bool hasShaderFormatVersion,
            float shaderFormatVersion,
            bool hasPackage,
            string packageName,
            string packageVersion,
            string passShaderGuid,
            string shaderCanonicalDigest,
            string passCanonicalDigest,
            string includeTreeDigest,
            bool hasRenderMode,
            int renderMode,
            IReadOnlyCollection<string> compiledFeatures)
        {
            ShaderName = shaderName;
            AssetGuid = assetGuid;
            HasShaderFormatVersion = hasShaderFormatVersion;
            ShaderFormatVersion = shaderFormatVersion;
            HasPackage = hasPackage;
            PackageName = packageName;
            PackageVersion = packageVersion;
            PassShaderGuid = passShaderGuid;
            ShaderCanonicalDigest = shaderCanonicalDigest;
            PassCanonicalDigest = passCanonicalDigest;
            IncludeTreeDigest = includeTreeDigest;
            HasRenderMode = hasRenderMode;
            RenderMode = renderMode;
            CompiledFeatures = compiledFeatures
                ?? throw new ArgumentNullException(nameof(compiledFeatures));
        }
    }

    /// <summary>
    /// Attestation for lilToon 2.3.4. lilToon regenerates its shader assets from
    /// per-project settings, so a whole-file hash would refuse legitimate
    /// installs. Instead the two generated assets are hashed after
    /// canonicalizing exactly the regions the generator is proven to vary, the
    /// whole include directory is digested, and the render mode is read from the
    /// live pass rather than inferred from the asset's name.
    /// </summary>
    internal static class LilToonSourceAttestation
    {
        internal const string SupportedShaderName = "lilToon";
        internal const string SupportedShaderGuid =
            "df12117ecd77c31469c224178886498e";
        internal const string PassShaderName = "Hidden/ltspass_opaque";
        internal const string PassShaderGuid =
            "61b4f98a5d78b4a4a9d89180fac793fc";
        internal const string PackageName = "jp.lilxyzw.liltoon";
        internal const string PackageVersion = "2.3.4";
        internal const float ShaderFormatVersion = 45f;
        internal const int OpaqueRenderMode = 0;

        // Measured by Task 0 from a real jp.lilxyzw.liltoon@2.3.4 install and
        // cross-checked between default and stripped shader settings. These are
        // not derivable from the lilToon repository, whose committed generated
        // shaders are stale relative to their own tag's generator.
        internal const string ShaderCanonicalDigest =
            "5206bec25e82db5f8009b27fcc5ba94d7c41113031d4b6b0a2c25ca324a9c704";
        internal const string PassCanonicalDigest =
            "6b6c30c1cbe546fe753bcdc77f547441e3f9114ee80e9591bde2b8e6e7e5eb14";
        internal const string IncludeTreeDigest =
            "6e2dce6cb3073d5e04b569a14df8e0944c93ca408999fb42d7c717050c48fd46";

        internal const string ShaderFormatVersionProperty = "_lilToonVersion";
        private const string IncludeFolderName = "Includes";

        // D1: a valueless define the *LIL_SHADER_SETTING* substitution can emit.
        // A define with a value, such as LIL_RENDER 0, never matches.
        private static readonly Regex SettingDefine = new Regex(
            @"^#define\s+(?:LIL_FEATURE_\w+|LIL_OPTIMIZE_\w+|LIL_INPUT_OPTIMIZED)\s*$",
            RegexOptions.Compiled);

        // D2: variant stripping, emitted by the setting substitution and by the
        // lil_skip_variants_* markers.
        private static readonly Regex SkipVariants = new Regex(
            @"^#pragma\s+skip_variants\s+\S",
            RegexOptions.Compiled);

        // R2 anchor: the fixed terminal line of the BRP lil_multi_compile_forward
        // expansion. The template places lil_skip_variants_{base,outline}_shadows
        // immediately after it, so this line uniquely locates that slot.
        private const string ShadowSlotAnchor = "#define LIL_PASS_FORWARD";

        // R2 keyword domain. GetSkipVariantsShadows() is a fixed literal ending
        // in SHADOW_VERY_HIGH, and UnpackContainer's dedup pass rewrites a
        // surviving skip_variants line to its final keyword alone, so this is
        // the entire set the generator can produce at the slot. It is a closed
        // literal, not a pattern; do not widen it into a variant system.
        private const string ShadowSlotKeyword = "SHADOW_VERY_HIGH";

        // Exactly one keyword, captured for the domain check.
        private static readonly Regex SingleKeywordSkipVariants = new Regex(
            @"^#pragma\s+skip_variants\s+(?<keyword>\w+)\s*$",
            RegexOptions.Compiled);

        private static readonly Regex FeatureDefine = new Regex(
            @"^#define\s+(LIL_FEATURE_\w+)\s*$",
            RegexOptions.Compiled);

        private static readonly Regex RenderDefine = new Regex(
            @"^#define\s+LIL_RENDER\s+(\S+)\s*$",
            RegexOptions.Compiled);

        // R3 matches only a whole-line include directive, live or commented.
        // A quoted string anywhere else is never rewritten.
        private static readonly Regex IncludeDirective = new Regex(
            "^(?<lead>\\s*(?://)?#include\\s+\")(?<path>[^\"]*)(?<tail>\"\\s*)$",
            RegexOptions.Compiled);

        /// <summary>
        /// Normalizes shader source (drop an optional leading UTF-8 BOM, then
        /// convert CRLF and lone CR to LF) and returns the lowercase-hex SHA-256
        /// of its UTF-8 bytes. The rule matches the Poiyomi frontend exactly.
        /// </summary>
        internal static string ComputeNormalizedSourceHash(string rawSource)
        {
            if (rawSource == null)
            {
                throw new ArgumentNullException(nameof(rawSource));
            }

            return Sha256(Normalize(rawSource));
        }

        private static string Normalize(string rawSource)
        {
            if (rawSource.Length > 0 && rawSource[0] == '﻿')
            {
                rawSource = rawSource.Substring(1);
            }

            return rawSource.Replace("\r\n", "\n").Replace("\r", "\n");
        }

        private static string Sha256(string text)
        {
            var bytes = new UTF8Encoding(false).GetBytes(text);
            using (var sha = SHA256.Create())
            {
                var hash = sha.ComputeHash(bytes);
                var builder = new StringBuilder(hash.Length * 2);
                foreach (var value in hash)
                {
                    builder.Append(value.ToString("x2"));
                }

                return builder.ToString();
            }
        }

        /// <summary>
        /// Removes exactly the text lilToon's generator is proven to vary, so
        /// the remainder can be hashed against a pin. R1 drops the
        /// setting-substituted feature block inside an HLSLINCLUDE run; R2
        /// drops the shadow skip-variant expansion at its one substitution
        /// slot, identified by the `#define LIL_PASS_FORWARD` line directly
        /// above it; R3 normalizes an include path only when it provably
        /// resolves into the attested tree. Everything else — pass bodies,
        /// tags, blend state, other pragmas, other includes, blank lines, and
        /// every valued define — is retained, so any hand edit or
        /// custom-shader injection changes the digest.
        /// </summary>
        internal static string Canonicalize(
            string rawShaderSource,
            string shaderDirectory,
            string projectRoot,
            LilToonIncludeTree includeTree)
        {
            if (rawShaderSource == null)
            {
                throw new ArgumentNullException(nameof(rawShaderSource));
            }
            if (includeTree == null)
            {
                throw new ArgumentNullException(nameof(includeTree));
            }

            var lines = Normalize(rawShaderSource).Split('\n');

            // Mark the setting region before emitting, so a same-shaped line
            // outside it can never be dropped.
            var inSettingRegion = new bool[lines.Length];

            for (var i = 0; i < lines.Length; i++)
            {
                // Region A: after HLSLINCLUDE, the maximal run of D1/D2 lines.
                // A blank line does not extend the run, and a valued define
                // ends it immediately — which is why the Shader-scope block
                // holding `#define LIL_RENDER 0` has an empty region A.
                if (!string.Equals(
                        lines[i].Trim(), "HLSLINCLUDE", StringComparison.Ordinal))
                {
                    continue;
                }

                for (var j = i + 1; j < lines.Length; j++)
                {
                    var candidate = lines[j].Trim();
                    if (!SettingDefine.IsMatch(candidate) &&
                        !SkipVariants.IsMatch(candidate))
                    {
                        break;
                    }

                    inSettingRegion[j] = true;
                }
            }

            var builder = new StringBuilder(rawShaderSource.Length);
            var first = true;
            for (var i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                var trimmed = line.Trim();

                if (inSettingRegion[i] &&
                    (SettingDefine.IsMatch(trimmed) || SkipVariants.IsMatch(trimmed)))
                {
                    continue;
                }

                // R2: the shadow substitution slot. All three conditions are
                // required — the anchor line above, a single-keyword
                // skip_variants directive, and the one keyword the generator
                // can actually produce here. An unrelated keyword after the
                // correct anchor stays hashed.
                if (i > 0 &&
                    string.Equals(
                        lines[i - 1].Trim(),
                        ShadowSlotAnchor,
                        StringComparison.Ordinal))
                {
                    var slot = SingleKeywordSkipVariants.Match(trimmed);
                    if (slot.Success &&
                        string.Equals(
                            slot.Groups["keyword"].Value,
                            ShadowSlotKeyword,
                            StringComparison.Ordinal))
                    {
                        continue;
                    }
                }

                if (!first)
                {
                    builder.Append('\n');
                }

                first = false;
                builder.Append(
                    NormalizeIncludeLine(
                        line, shaderDirectory, projectRoot, includeTree));
            }

            return builder.ToString();
        }

        /// <summary>
        /// R3. Rewrites an include directive only when its path is proven to
        /// resolve to a file inside the attested include tree, and preserves the
        /// file's path relative to that tree. A path that resolves outside the
        /// tree, resolves to nothing, or resolves ambiguously is returned
        /// byte-identical, so it contributes its original text to the digest and
        /// the material refuses. Identity, not basename, is what makes a
        /// redirected include detectable.
        /// </summary>
        private static string NormalizeIncludeLine(
            string line,
            string shaderDirectory,
            string projectRoot,
            LilToonIncludeTree includeTree)
        {
            var match = IncludeDirective.Match(line);
            if (!match.Success)
            {
                return line;
            }

            var path = match.Groups["path"].Value;
            string resolved = null;

            // The project root is supplied explicitly. Resolving against "."
            // would couple attestation to the Editor process's working
            // directory, which is not a property of the shader being attested.
            foreach (var candidate in new[]
                     {
                         CombineFullPath(shaderDirectory, path),
                         CombineFullPath(projectRoot, path),
                     })
            {
                if (candidate == null ||
                    !includeTree.TryGetRelativePath(candidate, out var relative))
                {
                    continue;
                }

                if (resolved != null &&
                    !string.Equals(resolved, relative, StringComparison.Ordinal))
                {
                    // Ambiguous: two readings land on different attested files.
                    return line;
                }

                resolved = relative;
            }

            return resolved == null
                ? line
                : match.Groups["lead"].Value +
                  IncludeFolderName + "/" + resolved +
                  match.Groups["tail"].Value;
        }

        private static string CombineFullPath(string baseDirectory, string path)
        {
            if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(baseDirectory))
            {
                return null;
            }

            try
            {
                return Path.GetFullPath(Path.Combine(baseDirectory, path));
            }
            catch (ArgumentException)
            {
                return null;
            }
            catch (NotSupportedException)
            {
                return null;
            }
            catch (PathTooLongException)
            {
                return null;
            }
        }

        /// <summary>
        /// Digests the whole include directory listing. Enumerating the
        /// directory rather than a reachability-derived file list keeps include
        /// closure analysis out of the trusted computing base and also detects
        /// added files.
        /// </summary>
        internal static string ComputeIncludeTreeDigest(
            IReadOnlyList<(string RelativePath, string Hash)> files)
        {
            if (files == null)
            {
                throw new ArgumentNullException(nameof(files));
            }

            var rows = new List<string>(files.Count);
            foreach (var file in files)
            {
                rows.Add(file.RelativePath + ":" + file.Hash);
            }

            rows.Sort(StringComparer.Ordinal);
            return Sha256(string.Join("\n", rows));
        }

        /// <summary>
        /// Collects the valueless <c>LIL_FEATURE_*</c> symbols the resolved pass
        /// defines. A literal line scan over a closed prefix: no conditional
        /// evaluation, no macro expansion, no HLSL grammar. lilToon's setting can
        /// strip a feature while its material property stays set, so an output
        /// that claims such a feature must see it here or stay Unknown.
        /// </summary>
        internal static IReadOnlyCollection<string> ScanCompiledFeatures(
            string passShaderSource)
        {
            var features = new HashSet<string>(StringComparer.Ordinal);
            if (string.IsNullOrEmpty(passShaderSource))
            {
                return features;
            }

            foreach (var rawLine in Normalize(passShaderSource).Split('\n'))
            {
                var match = FeatureDefine.Match(rawLine.Trim());
                if (match.Success)
                {
                    features.Add(match.Groups[1].Value);
                }
            }

            return features;
        }

        /// <summary>
        /// Reads the render mode the resolved pass currently declares. Requires
        /// exactly one <c>#define LIL_RENDER &lt;int&gt;</c>; zero, several, or a
        /// non-integer value cannot establish the fact.
        /// </summary>
        internal static bool TryScanRenderMode(
            string passShaderSource,
            out int renderMode)
        {
            renderMode = 0;
            if (string.IsNullOrEmpty(passShaderSource))
            {
                return false;
            }

            var found = false;
            foreach (var rawLine in Normalize(passShaderSource).Split('\n'))
            {
                var match = RenderDefine.Match(rawLine.Trim());
                if (!match.Success)
                {
                    continue;
                }

                if (found)
                {
                    return false;
                }

                if (!int.TryParse(
                        match.Groups[1].Value,
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture,
                        out renderMode))
                {
                    return false;
                }

                found = true;
            }

            return found;
        }

        internal static bool TryVerifyLilToonIdentity(
            LilToonSourceEvidence evidence,
            out LilToonSemanticDiagnostic diagnostic)
        {
            if (evidence == null)
            {
                throw new ArgumentNullException(nameof(evidence));
            }

            // 1. Shader identity. There is no family table: one supported
            //    shader, everything else refused.
            if (!string.Equals(
                    evidence.ShaderName,
                    SupportedShaderName,
                    StringComparison.Ordinal))
            {
                diagnostic = Material(
                    LilToonSemanticDiagnosticCode.UnsupportedShader,
                    $"shader name '{evidence.ShaderName}'");
                return false;
            }

            if (!string.Equals(
                    evidence.AssetGuid,
                    SupportedShaderGuid,
                    StringComparison.Ordinal))
            {
                diagnostic = Material(
                    LilToonSemanticDiagnosticCode.UnsupportedShader,
                    "shader asset GUID");
                return false;
            }

            // 2. Material shader-format stamp, compared exactly. A malformed or
            //    nearby value must never be normalized into the supported one.
            if (!evidence.HasShaderFormatVersion ||
                float.IsNaN(evidence.ShaderFormatVersion) ||
                float.IsInfinity(evidence.ShaderFormatVersion) ||
                evidence.ShaderFormatVersion != ShaderFormatVersion)
            {
                diagnostic = Material(
                    LilToonSemanticDiagnosticCode.UnsupportedVersion,
                    ShaderFormatVersionProperty);
                return false;
            }

            // 3. Package identity, when installed as a package.
            if (evidence.HasPackage)
            {
                if (!string.Equals(
                        evidence.PackageName, PackageName, StringComparison.Ordinal))
                {
                    diagnostic = Material(
                        LilToonSemanticDiagnosticCode.MissingSourceEvidence,
                        $"package name '{evidence.PackageName}'");
                    return false;
                }

                if (!string.Equals(
                        evidence.PackageVersion,
                        PackageVersion,
                        StringComparison.Ordinal))
                {
                    diagnostic = Material(
                        LilToonSemanticDiagnosticCode.UnsupportedVersion,
                        $"package version '{evidence.PackageVersion}'");
                    return false;
                }
            }

            // 4. Resolved pass asset.
            if (!string.Equals(
                    evidence.PassShaderGuid,
                    PassShaderGuid,
                    StringComparison.Ordinal))
            {
                diagnostic = Material(
                    LilToonSemanticDiagnosticCode.MissingSourceEvidence,
                    PassShaderName);
                return false;
            }

            // 5. Source digests.
            if (!TryMatchDigest(
                    evidence.IncludeTreeDigest,
                    IncludeTreeDigest,
                    IncludeFolderName,
                    out diagnostic) ||
                !TryMatchDigest(
                    evidence.ShaderCanonicalDigest,
                    ShaderCanonicalDigest,
                    SupportedShaderName,
                    out diagnostic) ||
                !TryMatchDigest(
                    evidence.PassCanonicalDigest,
                    PassCanonicalDigest,
                    PassShaderName,
                    out diagnostic))
            {
                return false;
            }

            // 6. Render mode as the current pass declares it, not as the pass
            //    asset's historical name implies.
            if (!evidence.HasRenderMode ||
                evidence.RenderMode != OpaqueRenderMode)
            {
                diagnostic = Material(
                    LilToonSemanticDiagnosticCode.UnsupportedShaderVariant,
                    evidence.HasRenderMode
                        ? $"LIL_RENDER {evidence.RenderMode}"
                        : "LIL_RENDER unreadable");
                return false;
            }

            diagnostic = null;
            return true;
        }

        private static bool TryMatchDigest(
            string actual,
            string expected,
            string detail,
            out LilToonSemanticDiagnostic diagnostic)
        {
            if (string.IsNullOrEmpty(actual))
            {
                diagnostic = Material(
                    LilToonSemanticDiagnosticCode.MissingSourceEvidence,
                    detail);
                return false;
            }

            if (!string.Equals(actual, expected, StringComparison.Ordinal))
            {
                diagnostic = Material(
                    LilToonSemanticDiagnosticCode.ModifiedShaderSource,
                    detail);
                return false;
            }

            diagnostic = null;
            return true;
        }

        /// <summary>
        /// Reads identity evidence from a live material. Unreadable evidence is
        /// omitted rather than guessed, so the conjunction refuses.
        /// </summary>
        internal static LilToonSourceEvidence GatherSourceEvidence(
            Material material)
        {
            var shader = material.shader;
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                shader, out var assetGuid, out long _);

            var hasVersion = material.HasProperty(ShaderFormatVersionProperty);
            var version = hasVersion
                ? material.GetFloat(ShaderFormatVersionProperty)
                : float.NaN;

            var shaderPath = AssetDatabase.GetAssetPath(shader);
            var package = UnityEditor.PackageManager.PackageInfo
                .FindForAssetPath(shaderPath);

            var shaderDirectory = string.IsNullOrEmpty(shaderPath)
                ? null
                : Path.GetDirectoryName(shaderPath);
            var includeFolder = shaderDirectory == null
                ? null
                : Path.Combine(shaderDirectory, IncludeFolderName);

            // Explicit project root; never the process working directory.
            var projectRoot = Directory.GetParent(Application.dataPath)?.FullName;

            var includeTree = LilToonIncludeTree.Enumerate(
                includeFolder, ReadTextOrNull, ComputeNormalizedSourceHash);

            var includeDigest = includeTree.Files.Count == 0
                ? null
                : ComputeIncludeTreeDigest(includeTree.Files);

            var shaderText = ReadTextOrNull(shaderPath);
            var shaderDigest = shaderText == null
                ? null
                : Sha256(Canonicalize(
                    shaderText, shaderDirectory, projectRoot, includeTree));

            var passShader = Shader.Find(PassShaderName);
            string passGuid = null;
            string passDigest = null;
            string passText = null;
            if (passShader != null)
            {
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                    passShader, out passGuid, out long _);
                var passPath = AssetDatabase.GetAssetPath(passShader);
                passText = ReadTextOrNull(passPath);
                if (passText != null)
                {
                    // The pass resolves its own includes relative to its own
                    // directory, which need not be the material shader's.
                    passDigest = Sha256(
                        Canonicalize(
                            passText,
                            Path.GetDirectoryName(passPath),
                            projectRoot,
                            includeTree));
                }
            }

            var hasRenderMode = TryScanRenderMode(passText, out var renderMode);

            return new LilToonSourceEvidence(
                shader.name,
                assetGuid?.ToLowerInvariant(),
                hasVersion,
                version,
                package != null,
                package?.name,
                package?.version,
                passGuid?.ToLowerInvariant(),
                shaderDigest,
                passDigest,
                includeDigest,
                hasRenderMode,
                renderMode,
                ScanCompiledFeatures(passText));
        }

        private static string ReadTextOrNull(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                return null;
            }

            try
            {
                return File.ReadAllText(path, Encoding.UTF8);
            }
            catch (IOException)
            {
                return null;
            }
            catch (UnauthorizedAccessException)
            {
                return null;
            }
        }

        private static LilToonSemanticDiagnostic Material(
            LilToonSemanticDiagnosticCode code,
            string detail)
        {
            return new LilToonSemanticDiagnostic(
                LilToonSemanticOutput.Material,
                code,
                detail);
        }
    }
}
```

Four details matter.

`TryMatchDigest` writes `diagnostic = null` on success so the short-circuiting `||` chain in check 5 leaves the correct value; verify with the three digest-mismatch tests.

`evidence.ShaderFormatVersion != ShaderFormatVersion` is a direct float comparison on purpose — `Mathf.RoundToInt` would accept `44.999f`, which is exactly the defect this task fixes.

`Canonicalize` marks region A in a first pass before emitting anything. Do not fuse the loops into one streaming pass: region membership must be decided by position, and a single pass invites reintroducing the whole-file rule by accident. The R2 slot check reads `lines[i - 1]` from the raw array, not from the emitted output, so an earlier removal can never shift the anchor.

The three digest constants are Task 0's measurements, already substituted. Do not recompute them from the lilToon repository — a guessed constant would make every material refuse and the failure would look like an unrelated bug.

- [ ] **Step 4: Run tests to verify they pass**

Run `refresh_unity`, then the focused run. Expected: PASS.

- [ ] **Step 5: Scope inspection**

```bash
git status --porcelain --untracked-files=all
```

Expected delta from Task 1: `Editor/Semantics/LilToon/` and `Tests/Editor/Semantics/LilToon/` folders with their `.meta` files, `LilToonSourceAttestation.cs`, `LilToonAttestationTests.cs`, and the `LilToonMaterialSemantics.cs` stub holding the three diagnostic types. Do not stage and do not commit.

---

### Task 3: Entry point, result boundary, and test fixture

**Files:**

- Create: `Packages/com.alrauna.amuse/Tests/Editor/Semantics/LilToon/LilToonSemanticTest.shader`
- Create: `Packages/com.alrauna.amuse/Tests/Editor/Semantics/LilToon/LilToonFixtureTestBase.cs`
- Modify: `Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonMaterialSemantics.cs` (diagnostic types already added in Task 2; add the entry point, seam, result type, and four stubs returning `Unknown`)
- Create: `Packages/com.alrauna.amuse/Tests/Editor/Semantics/LilToon/LilToonAdversarialTests.cs` (malformed-input cases only; extended in Task 8)

**Interfaces:**

- Consumes: `LilToonSourceAttestation`, `LilToonSourceEvidence`, `MaterialSemantics`, `SemanticOutput<T>`.
- Produces:

```csharp
internal enum LilToonSemanticOutput { Material, BaseColor, Alpha, Emission, Normal }

internal enum LilToonSemanticDiagnosticCode
{
    UnsupportedShader, UnsupportedShaderVariant, UnsupportedVersion,
    ModifiedShaderSource, MissingSourceEvidence, MissingFeatureCompilation,
    UnsupportedFeature, UnsupportedUv, UnsupportedSampling,
    UnstableTextureIdentity, UnsupportedColorSpace, UnsupportedTextureImport,
}

internal sealed class LilToonSemanticDiagnostic
{
    internal LilToonSemanticOutput Output { get; }
    internal LilToonSemanticDiagnosticCode Code { get; }
    internal string Detail { get; }
    internal LilToonSemanticDiagnostic(
        LilToonSemanticOutput output,
        LilToonSemanticDiagnosticCode code,
        string detail);
}

internal sealed class LilToonSemanticResult
{
    internal bool IsSupportedMaterial { get; }
    internal MaterialSemantics Semantics { get; }
    internal IReadOnlyList<LilToonSemanticDiagnostic> Diagnostics { get; }
}

internal static class LilToonMaterialSemantics
{
    internal static LilToonSemanticResult AnalyzeBaseMaterial(Material material);

    internal static LilToonSemanticResult InterpretVerifiedMaterial(
        Material material,
        ColorSpace activeColorSpace,
        IReadOnlyCollection<string> compiledFeatures);
}
```

`InterpretVerifiedMaterial` is the friend-test seam. It takes the resolved colour space and the compiled feature set explicitly, so deterministic tests exercise every equation without a real lilToon install, a real include hash, or a project colour-space change.

- [ ] **Step 1: Write the test fixture shader**

Create `LilToonSemanticTest.shader`. It exposes only the property contract the interpreters read, with lilToon's own defaults.

```shaderlab
Shader "Hidden/Alrauna/AmuseTests/LilToonSemanticTest"
{
    Properties
    {
        [HideInInspector] _lilToonVersion ("Version", Int) = 45

        _Invisible ("Invisible", Int) = 0
        _ShiftBackfaceUV ("ShiftBackfaceUV", Int) = 0
        _UDIMDiscardCompile ("UDIMDiscard", Int) = 0
        _BackfaceColor ("BackfaceColor", Color) = (0,0,0,0)

        _Color ("Color", Color) = (1,1,1,1)
        _MainTex ("Texture", 2D) = "white" {}
        _MainTex_ScrollRotate ("ScrollRotate", Vector) = (0,0,0,0)
        _MainTexHSVG ("HSVG", Vector) = (0,1,1,1)
        _MainGradationStrength ("GradationStrength", Range(0,1)) = 0
        _MainColorAdjustMask ("AdjustMask", 2D) = "white" {}

        _UseMain2ndTex ("UseMain2nd", Int) = 0
        _UseMain3rdTex ("UseMain3rd", Int) = 0
        _UseParallax ("UseParallax", Int) = 0
        _UsePOM ("UsePOM", Int) = 0
        _UseAudioLink ("UseAudioLink", Int) = 0
        _UseAnisotropy ("UseAnisotropy", Int) = 0

        _UseBumpMap ("UseBumpMap", Int) = 0
        [Normal] _BumpMap ("BumpMap", 2D) = "bump" {}
        _BumpScale ("BumpScale", Range(-10,10)) = 1
        _UseBump2ndMap ("UseBump2nd", Int) = 0

        _UseEmission ("UseEmission", Int) = 0
        [HDR] _EmissionColor ("EmissionColor", Color) = (1,1,1,1)
        _EmissionMap ("EmissionMap", 2D) = "white" {}
        _EmissionMap_ScrollRotate ("ScrollRotate", Vector) = (0,0,0,0)
        _EmissionMap_UVMode ("UVMode", Int) = 0
        _EmissionMainStrength ("MainStrength", Range(0,1)) = 0
        _EmissionBlend ("Blend", Range(0,1)) = 1
        _EmissionBlendMask ("BlendMask", 2D) = "white" {}
        _EmissionBlendMode ("BlendMode", Int) = 1
        _EmissionBlink ("Blink", Vector) = (0,0,3.141593,0)
        _EmissionUseGrad ("UseGrad", Int) = 0
        _EmissionParallaxDepth ("ParallaxDepth", Float) = 0
        _EmissionFluorescence ("Fluorescence", Range(0,1)) = 0
        _AudioLink2Emission ("AudioLink2Emission", Int) = 0

        _UseEmission2nd ("UseEmission2nd", Int) = 0
        _UseReflection ("UseReflection", Int) = 0
        _UseMatCap ("UseMatCap", Int) = 0
        _UseMatCap2nd ("UseMatCap2nd", Int) = 0
        _UseRim ("UseRim", Int) = 0
        _UseRimShade ("UseRimShade", Int) = 0
        _UseGlitter ("UseGlitter", Int) = 0
        _UseBacklight ("UseBacklight", Int) = 0
        _DissolveParams ("DissolveParams", Vector) = (0,0,0.5,0.1)
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" }

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            float4 vert(float4 vertex : POSITION) : SV_POSITION
            {
                return UnityObjectToClipPos(vertex);
            }

            fixed4 frag() : SV_Target
            {
                return fixed4(1, 1, 1, 1);
            }
            ENDCG
        }
    }
}
```

This is an executable specification of the consumed property contract, not a pretend lilToon distribution. It contains no lilToon source.

- [ ] **Step 2: Write the fixture base and failing boundary tests**

Create `LilToonFixtureTestBase.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Alrauna.Amuse.Editor.Semantics;
using Alrauna.Amuse.Editor.Semantics.LilToon;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Alrauna.Amuse.Tests.Editor.Semantics.LilToon
{
    /// <summary>
    /// Shared Editor-test fixture for the verified lilToon interpreter. It
    /// builds a schema-complete stand-in material and disposable texture assets
    /// under one temp folder; no real lilToon package is installed. Equations
    /// are exercised through the verified-material seam, so the stand-in never
    /// needs the pinned include hashes.
    /// </summary>
    public abstract class LilToonFixtureTestBase
    {
        protected const string FixtureShaderName =
            "Hidden/Alrauna/AmuseTests/LilToonSemanticTest";
        protected const string TempFolder = "Assets/AmuseTests_LilToon";

        /// <summary>Every feature symbol a fully compiled lilToon exposes.</summary>
        protected static readonly string[] AllFeatures =
        {
            "LIL_FEATURE_NORMAL_1ST",
            "LIL_FEATURE_BumpMap",
            "LIL_FEATURE_EMISSION_1ST",
            "LIL_FEATURE_EmissionMap",
        };

        private readonly List<UnityEngine.Object> _transient =
            new List<UnityEngine.Object>();

        [SetUp]
        public void BaseSetUp()
        {
            if (!AssetDatabase.IsValidFolder(TempFolder))
            {
                AssetDatabase.CreateFolder("Assets", "AmuseTests_LilToon");
            }
        }

        [TearDown]
        public void BaseTearDown()
        {
            foreach (var obj in _transient)
            {
                if (obj != null)
                {
                    UnityEngine.Object.DestroyImmediate(obj);
                }
            }

            _transient.Clear();

            if (AssetDatabase.IsValidFolder(TempFolder))
            {
                AssetDatabase.DeleteAsset(TempFolder);
            }
        }

        protected T Track<T>(T obj) where T : UnityEngine.Object
        {
            _transient.Add(obj);
            return obj;
        }

        protected Material NewFixtureMaterial()
        {
            var shader = Shader.Find(FixtureShaderName);
            Assert.That(
                shader,
                Is.Not.Null,
                $"Test fixture shader '{FixtureShaderName}' must import.");
            return Track(new Material(shader));
        }

        /// <summary>
        /// Interprets with linear colour space and every feature compiled in,
        /// the configuration under which the traced equations hold.
        /// </summary>
        protected static LilToonSemanticResult Interpret(Material material)
        {
            return LilToonMaterialSemantics.InterpretVerifiedMaterial(
                material, ColorSpace.Linear, AllFeatures);
        }

        protected static LilToonSemanticResult Interpret(
            Material material,
            params string[] compiledFeatures)
        {
            return LilToonMaterialSemantics.InterpretVerifiedMaterial(
                material, ColorSpace.Linear, compiledFeatures);
        }

        protected static IReadOnlyList<LilToonSemanticDiagnostic> DiagnosticsFor(
            LilToonSemanticResult result,
            LilToonSemanticOutput output)
        {
            return result.Diagnostics.Where(d => d.Output == output).ToList();
        }

        protected static void AssertSingleDiagnostic(
            LilToonSemanticResult result,
            LilToonSemanticOutput output,
            LilToonSemanticDiagnosticCode code,
            string detailContains)
        {
            var scoped = DiagnosticsFor(result, output);
            Assert.That(scoped.Count, Is.EqualTo(1), $"{output} diagnostics");
            Assert.That(scoped[0].Code, Is.EqualTo(code));
            Assert.That(scoped[0].Detail, Does.Contain(detailContains));
        }

        /// <summary>
        /// Writes, imports, and returns a tiny texture asset. The default import
        /// yields a supported sampler unless a test opts out through
        /// <paramref name="configure"/>.
        /// </summary>
        protected Texture2D ImportTexture(
            string name,
            Action<TextureImporter> configure = null,
            bool sourceHasAlpha = true)
        {
            var path = TempFolder + "/" + name + ".png";
            var format = sourceHasAlpha
                ? TextureFormat.RGBA32
                : TextureFormat.RGB24;
            var staging = new Texture2D(4, 4, format, false);
            var pixels = new Color32[16];
            for (var i = 0; i < pixels.Length; i++)
            {
                pixels[i] = new Color32(128, 64, 32, 200);
            }

            staging.SetPixels32(pixels);
            staging.Apply();
            File.WriteAllBytes(path, staging.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(staging);

            AssetDatabase.ImportAsset(
                path, ImportAssetOptions.ForceSynchronousImport);

            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.mipmapEnabled = false;
            configure?.Invoke(importer);
            importer.SaveAndReimport();

            var loaded = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            Assert.That(loaded, Is.Not.Null, $"Imported texture '{path}' must load.");
            return loaded;
        }

        protected Texture2D ImportNormalMap(string name)
        {
            return ImportTexture(
                name,
                importer =>
                {
                    importer.textureType = TextureImporterType.NormalMap;
                    importer.flipGreenChannel = false;
                },
                sourceHasAlpha: false);
        }

        protected Texture2D ImportOpaqueColorMap(string name)
        {
            return ImportTexture(
                name,
                importer =>
                {
                    importer.sRGBTexture = true;
                    importer.alphaSource = TextureImporterAlphaSource.None;
                },
                sourceHasAlpha: false);
        }
    }
}
```

Create `LilToonAdversarialTests.cs` with the malformed-input cases:

```csharp
using System;
using Alrauna.Amuse.Editor.Semantics;
using Alrauna.Amuse.Editor.Semantics.LilToon;
using NUnit.Framework;
using UnityEngine;

namespace Alrauna.Amuse.Tests.Editor.Semantics.LilToon
{
    public sealed class LilToonAdversarialTests : LilToonFixtureTestBase
    {
        [Test]
        public void AnalyzeBaseMaterial_Null_Throws()
        {
            Assert.Throws<ArgumentNullException>(
                () => LilToonMaterialSemantics.AnalyzeBaseMaterial(null));
        }

        [Test]
        public void AnalyzeBaseMaterial_DestroyedMaterial_Throws()
        {
            var material = new Material(Shader.Find(FixtureShaderName));
            UnityEngine.Object.DestroyImmediate(material);

            Assert.Throws<ArgumentException>(
                () => LilToonMaterialSemantics.AnalyzeBaseMaterial(material));
        }

        [Test]
        public void AnalyzeBaseMaterial_NonLilToonShader_IsUnsupported()
        {
            var material = NewFixtureMaterial();

            var result = LilToonMaterialSemantics.AnalyzeBaseMaterial(material);

            Assert.That(result.IsSupportedMaterial, Is.False);
            Assert.That(result.Diagnostics.Count, Is.EqualTo(1));
            Assert.That(
                result.Diagnostics[0].Code,
                Is.EqualTo(LilToonSemanticDiagnosticCode.UnsupportedShader));
            Assert.That(result.Semantics.BaseColor.IsComplete, Is.False);
            Assert.That(result.Semantics.Alpha.IsComplete, Is.False);
            Assert.That(result.Semantics.Emission.IsComplete, Is.False);
            Assert.That(result.Semantics.Normal.IsComplete, Is.False);
        }

        [Test]
        public void InterpretVerifiedMaterial_NullFeatures_Throws()
        {
            var material = NewFixtureMaterial();

            Assert.Throws<ArgumentNullException>(
                () => LilToonMaterialSemantics.InterpretVerifiedMaterial(
                    material, ColorSpace.Linear, null));
        }
    }
}
```

- [ ] **Step 3: Run tests to verify they fail**

Run `refresh_unity`, then:

```text
test_names: Alrauna.Amuse.Tests.Editor.Semantics.LilToon.LilToonAdversarialTests
```

Expected: compile failure, `LilToonMaterialSemantics` does not exist.

- [ ] **Step 4: Write the entry point and Unknown stubs**

In `LilToonMaterialSemantics.cs`, below the diagnostic types added in Task 2, add the result type, the entry point, the seam, and four private interpreters that currently return `Unknown` with no diagnostic. Tasks 4 to 7 replace one interpreter each.

```csharp
internal static class LilToonMaterialSemantics
{
    internal static LilToonSemanticResult AnalyzeBaseMaterial(Material material)
    {
        RequireAnalyzableMaterial(material);

        var evidence = LilToonSourceAttestation.GatherSourceEvidence(material);
        if (!LilToonSourceAttestation.TryVerifyLilToonIdentity(
                evidence, out var diagnostic))
        {
            return Unsupported(diagnostic);
        }

        return InterpretVerifiedMaterial(
            material,
            QualitySettings.activeColorSpace,
            evidence.CompiledFeatures);
    }

    internal static LilToonSemanticResult InterpretVerifiedMaterial(
        Material material,
        ColorSpace activeColorSpace,
        IReadOnlyCollection<string> compiledFeatures)
    {
        RequireAnalyzableMaterial(material);
        if (compiledFeatures == null)
        {
            throw new ArgumentNullException(nameof(compiledFeatures));
        }

        var diagnostics = new List<LilToonSemanticDiagnostic>();

        var baseColor = InterpretBaseColor(material, activeColorSpace, diagnostics);
        var alpha = InterpretAlpha(material, diagnostics);
        var emission = InterpretEmission(
            material, activeColorSpace, compiledFeatures, diagnostics);
        var normal = InterpretNormal(material, compiledFeatures, diagnostics);

        return new LilToonSemanticResult(
            true,
            new MaterialSemantics(baseColor, alpha, emission, normal),
            diagnostics);
    }
}
```

Add the supporting members. These mirror `PoiyomiMaterialSemantics`, and the duplication is deliberate — see the design spec's "Deliberately duplicated" section.

```csharp
internal sealed class LilToonSemanticResult
{
    internal bool IsSupportedMaterial { get; }
    internal MaterialSemantics Semantics { get; }
    internal IReadOnlyList<LilToonSemanticDiagnostic> Diagnostics { get; }

    internal LilToonSemanticResult(
        bool isSupportedMaterial,
        MaterialSemantics semantics,
        IReadOnlyList<LilToonSemanticDiagnostic> diagnostics)
    {
        if (semantics == null)
        {
            throw new ArgumentNullException(nameof(semantics));
        }

        if (diagnostics == null)
        {
            throw new ArgumentNullException(nameof(diagnostics));
        }

        IsSupportedMaterial = isSupportedMaterial;
        Semantics = semantics;

        var copy = new LilToonSemanticDiagnostic[diagnostics.Count];
        for (var i = 0; i < diagnostics.Count; i++)
        {
            copy[i] = diagnostics[i]
                ?? throw new ArgumentException(
                    "Diagnostics must not contain null entries.",
                    nameof(diagnostics));
        }

        Diagnostics =
            new ReadOnlyCollection<LilToonSemanticDiagnostic>(copy);
    }
}
```

```csharp
private static void RequireAnalyzableMaterial(Material material)
{
    if (ReferenceEquals(material, null))
    {
        throw new ArgumentNullException(nameof(material));
    }

    // Unity's overloaded equality reports a destroyed object as null.
    if (material == null)
    {
        throw new ArgumentException(
            "The material has been destroyed and cannot be analyzed.",
            nameof(material));
    }

    if (material.shader == null)
    {
        throw new ArgumentException(
            "The material has no shader and cannot be analyzed.",
            nameof(material));
    }
}

private static SemanticOutput<T> RecordUnknown<T>(
    List<LilToonSemanticDiagnostic> diagnostics,
    LilToonSemanticOutput output,
    LilToonSemanticDiagnosticCode code,
    string detail)
    where T : class
{
    diagnostics.Add(new LilToonSemanticDiagnostic(output, code, detail));
    return SemanticOutput<T>.Unknown();
}

private static LilToonSemanticResult Unsupported(
    LilToonSemanticDiagnostic diagnostic)
{
    return new LilToonSemanticResult(
        false,
        AllUnknown(),
        new[] { diagnostic });
}

private static MaterialSemantics AllUnknown()
{
    return new MaterialSemantics(
        SemanticOutput<ColorSemanticValue>.Unknown(),
        SemanticOutput<ScalarSemanticValue>.Unknown(),
        SemanticOutput<ColorSemanticValue>.Unknown(),
        SemanticOutput<NormalSemanticValue>.Unknown());
}
```

The `RequireAnalyzableMaterial` order matters: the null check must precede the destroyed check, because Unity's overloaded equality reports both as null. `LilToonSemanticDiagnostic`'s constructor throws `ArgumentNullException` on a null `detail`, matching the Poiyomi type.

Also add the local gate helpers, which are lilToon-local by the same reasoning:

```csharp
private static string FirstFailedZeroGate(
    Material material,
    params string[] properties)
{
    foreach (var property in properties)
    {
        if (!material.HasProperty(property))
        {
            return property;
        }

        var value = material.GetFloat(property);
        if (!IsFinite(value) || value != 0f)
        {
            return property;
        }
    }

    return null;
}

private static bool TryReadBinary(
    Material material,
    string property,
    out bool isSet)
{
    isSet = false;
    if (!material.HasProperty(property))
    {
        return false;
    }

    var value = material.GetFloat(property);
    if (!IsFinite(value) || (value != 0f && value != 1f))
    {
        return false;
    }

    isSet = value == 1f;
    return true;
}

private static bool IsFinite(float value) =>
    !float.IsNaN(value) && !float.IsInfinity(value);

private static bool IsFinite(Vector2 v) => IsFinite(v.x) && IsFinite(v.y);

private static bool IsFinite(Vector4 v) =>
    IsFinite(v.x) && IsFinite(v.y) && IsFinite(v.z) && IsFinite(v.w);
```

- [ ] **Step 5: Run tests to verify they pass**

Run `refresh_unity`, then the focused run. Expected: PASS, all 4 tests.

- [ ] **Step 6: Scope inspection**

```bash
git status --porcelain --untracked-files=all
```

Expected delta from Task 2: `LilToonSemanticTest.shader`, `LilToonFixtureTestBase.cs`, `LilToonAdversarialTests.cs` and their `.meta` files; `LilToonMaterialSemantics.cs` modified. Do not stage and do not commit.

---

### Task 4: BaseColor

**Files:**

- Create: `Packages/com.alrauna.amuse/Tests/Editor/Semantics/LilToon/LilToonBaseColorTests.cs`
- Modify: `Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonMaterialSemantics.cs` (`InterpretBaseColor`)

**Interfaces:**

- Consumes: `UnityTextureEvidence`, `ColorSemanticValue`, `TextureSample`, `UvMapping`.
- Produces: no new public surface. `InterpretBaseColor` stays private.

**Traced equation** (`lil_common_frag.hlsl:309-359`, `lil_pass_forward_normal.hlsl:263-279,443`):

```
uvMain = lilCalcDoubleSideUV(uv0, facing, _ShiftBackfaceUV)
uvMain = lilCalcUV(uvMain, _MainTex_ST, _MainTex_ScrollRotate)
col    = LIL_SAMPLE_2D_POM(_MainTex, sampler_MainTex, uvMain, ...)
col.rgb= lerp(col.rgb, gradation(toneCorrection(col.rgb, _MainTexHSVG)), adjustMask)
col   *= _Color
albedo = col.rgb
```

- [ ] **Step 1: Write the failing tests**

```csharp
using Alrauna.Amuse.Editor.Semantics;
using Alrauna.Amuse.Editor.Semantics.LilToon;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Alrauna.Amuse.Tests.Editor.Semantics.LilToon
{
    public sealed class LilToonBaseColorTests : LilToonFixtureTestBase
    {
        [Test]
        public void NoMainTex_IsLinearConstantColor()
        {
            var material = NewFixtureMaterial();
            material.SetColor("_Color", new Color(0.5f, 0.25f, 0.75f, 1f));

            var baseColor = Interpret(material).Semantics.BaseColor;

            Assert.That(baseColor.IsComplete, Is.True);
            var value = baseColor.GetCompleteValue();
            Assert.That(value.Kind, Is.EqualTo(ColorSemanticValueKind.Constant));
            var linear = new Color(0.5f, 0.25f, 0.75f, 1f).linear;
            Assert.That(
                value.GetConstantValue(),
                Is.EqualTo(new Vector3(linear.r, linear.g, linear.b)));
        }

        [Test]
        public void MainTexWithWhiteColor_IsPlainTextureSample()
        {
            var material = NewFixtureMaterial();
            var texture = ImportTexture("basecolor");
            material.SetTexture("_MainTex", texture);
            material.SetTextureScale("_MainTex", new Vector2(2f, 3f));
            material.SetTextureOffset("_MainTex", new Vector2(0.25f, 0.5f));

            var baseColor = Interpret(material).Semantics.BaseColor;

            Assert.That(baseColor.IsComplete, Is.True);
            var value = baseColor.GetCompleteValue();
            Assert.That(
                value.Kind,
                Is.EqualTo(ColorSemanticValueKind.TextureSample));

            var sample = value.GetTextureSample();
            Assert.That(sample.Coordinates.Channel, Is.EqualTo(0));
            Assert.That(sample.Coordinates.Scale, Is.EqualTo(new Vector2(2f, 3f)));
            Assert.That(
                sample.Coordinates.Offset,
                Is.EqualTo(new Vector2(0.25f, 0.5f)));
            Assert.That(
                UnityTextureEvidence.TryGetSourceId(texture, out var expectedId),
                Is.True);
            Assert.That(sample.Source, Is.EqualTo(expectedId));
        }

        [Test]
        public void MainTexWithTint_IsTextureTimesConstant()
        {
            var material = NewFixtureMaterial();
            material.SetTexture("_MainTex", ImportTexture("tinted"));
            material.SetColor("_Color", new Color(0.5f, 0.5f, 0.5f, 1f));

            var value = Interpret(material).Semantics.BaseColor.GetCompleteValue();

            Assert.That(
                value.Kind,
                Is.EqualTo(ColorSemanticValueKind.TextureSampleTimesConstant));
            var linear = new Color(0.5f, 0.5f, 0.5f, 1f).linear;
            Assert.That(
                value.GetMultiplier(),
                Is.EqualTo(new Vector3(linear.r, linear.g, linear.b)));
        }

        [Test]
        public void GammaColorSpace_IsUnknown()
        {
            var material = NewFixtureMaterial();

            var result = LilToonMaterialSemantics.InterpretVerifiedMaterial(
                material, ColorSpace.Gamma, AllFeatures);

            Assert.That(result.Semantics.BaseColor.IsComplete, Is.False);
            AssertSingleDiagnostic(
                result,
                LilToonSemanticOutput.BaseColor,
                LilToonSemanticDiagnosticCode.UnsupportedColorSpace,
                "Gamma");
        }

        [TestCase("_Invisible")]
        [TestCase("_ShiftBackfaceUV")]
        [TestCase("_UseParallax")]
        [TestCase("_UsePOM")]
        [TestCase("_UseAudioLink")]
        [TestCase("_UseMain2ndTex")]
        [TestCase("_UseMain3rdTex")]
        [TestCase("_MainGradationStrength")]
        public void EnabledWriter_KeepsBaseColorUnknown(string property)
        {
            var material = NewFixtureMaterial();
            material.SetFloat(property, 1f);

            var result = Interpret(material);

            Assert.That(result.Semantics.BaseColor.IsComplete, Is.False);
            AssertSingleDiagnostic(
                result,
                LilToonSemanticOutput.BaseColor,
                LilToonSemanticDiagnosticCode.UnsupportedFeature,
                property);
        }

        [Test]
        public void NonIdentityToneCorrection_IsUnknown()
        {
            var material = NewFixtureMaterial();
            material.SetVector("_MainTexHSVG", new Vector4(0.1f, 1f, 1f, 1f));

            var result = Interpret(material);

            Assert.That(result.Semantics.BaseColor.IsComplete, Is.False);
            AssertSingleDiagnostic(
                result,
                LilToonSemanticOutput.BaseColor,
                LilToonSemanticDiagnosticCode.UnsupportedFeature,
                "_MainTexHSVG");
        }

        [Test]
        public void AssignedColorAdjustMask_IsUnknown()
        {
            var material = NewFixtureMaterial();
            material.SetTexture("_MainColorAdjustMask", ImportTexture("mask"));

            var result = Interpret(material);

            Assert.That(result.Semantics.BaseColor.IsComplete, Is.False);
            AssertSingleDiagnostic(
                result,
                LilToonSemanticOutput.BaseColor,
                LilToonSemanticDiagnosticCode.UnsupportedFeature,
                "_MainColorAdjustMask");
        }

        [Test]
        public void NonZeroScrollRotate_IsUnsupportedUv()
        {
            var material = NewFixtureMaterial();
            material.SetTexture("_MainTex", ImportTexture("scroll"));
            material.SetVector("_MainTex_ScrollRotate", new Vector4(0.1f, 0f, 0f, 0f));

            var result = Interpret(material);

            Assert.That(result.Semantics.BaseColor.IsComplete, Is.False);
            AssertSingleDiagnostic(
                result,
                LilToonSemanticOutput.BaseColor,
                LilToonSemanticDiagnosticCode.UnsupportedUv,
                "_MainTex_ScrollRotate");
        }

        [Test]
        public void MipmappedMainTex_IsUnsupportedSampling()
        {
            var material = NewFixtureMaterial();
            material.SetTexture(
                "_MainTex",
                ImportTexture("mipped", importer => importer.mipmapEnabled = true));

            var result = Interpret(material);

            Assert.That(result.Semantics.BaseColor.IsComplete, Is.False);
            AssertSingleDiagnostic(
                result,
                LilToonSemanticOutput.BaseColor,
                LilToonSemanticDiagnosticCode.UnsupportedSampling,
                "_MainTex");
        }

        [Test]
        public void SceneOnlyMainTex_IsUnstableIdentity()
        {
            var material = NewFixtureMaterial();
            material.SetTexture("_MainTex", Track(new Texture2D(2, 2)));

            var result = Interpret(material);

            Assert.That(result.Semantics.BaseColor.IsComplete, Is.False);
            AssertSingleDiagnostic(
                result,
                LilToonSemanticOutput.BaseColor,
                LilToonSemanticDiagnosticCode.UnstableTextureIdentity,
                "_MainTex");
        }

        [Test]
        public void BoundedLdrMainTex_ProvesUnitRange()
        {
            var material = NewFixtureMaterial();
            material.SetTexture("_MainTex", ImportTexture("ldr"));

            Assert.That(
                Interpret(material).Semantics.BaseColor.IsComplete,
                Is.True);
        }

        [Test]
        public void FloatFormatMainTex_IsRefused()
        {
            var material = NewFixtureMaterial();
            var hdr = Track(new Texture2D(
                4, 4, TextureFormat.RGBAHalf, false));
            hdr.Apply();
            var path = TempFolder + "/hdrmain.asset";
            AssetDatabase.CreateAsset(hdr, path);
            material.SetTexture(
                "_MainTex", AssetDatabase.LoadAssetAtPath<Texture2D>(path));

            var result = Interpret(material);

            // A half-float format can exceed 1, so lilToneCorrection's saturate
            // calls would not be the identity.
            Assert.That(result.Semantics.BaseColor.IsComplete, Is.False);
            AssertSingleDiagnostic(
                result,
                LilToonSemanticOutput.BaseColor,
                LilToonSemanticDiagnosticCode.UnsupportedTextureImport,
                "_MainTex");
        }

        [Test]
        public void MainTexWithoutImporter_IsRefused()
        {
            var material = NewFixtureMaterial();
            var native = Track(new Texture2D(4, 4, TextureFormat.RGBA32, false));
            native.Apply();
            var path = TempFolder + "/nativemain.asset";
            AssetDatabase.CreateAsset(native, path);
            material.SetTexture(
                "_MainTex", AssetDatabase.LoadAssetAtPath<Texture2D>(path));

            var result = Interpret(material);

            // A native asset has a stable identity and a bounded format, but no
            // TextureImporter, so neither the colour interpretation nor the
            // range can be proven. Unproven evidence must refuse, never pass.
            Assert.That(result.Semantics.BaseColor.IsComplete, Is.False);
            AssertSingleDiagnostic(
                result,
                LilToonSemanticOutput.BaseColor,
                LilToonSemanticDiagnosticCode.UnsupportedTextureImport,
                "_MainTex");
        }
    }
}
```

`FloatFormatMainTex_IsRefused` and `MainTexWithoutImporter_IsRefused` are the fail-closed regression tests for the range proof. If the predicate is ever rewritten as a negative "does this look like HDR?" check, `MainTexWithoutImporter_IsRefused` fails first.

- [ ] **Step 2: Run tests to verify they fail**

Run `refresh_unity`, then:

```text
test_names: Alrauna.Amuse.Tests.Editor.Semantics.LilToon.LilToonBaseColorTests
```

Expected: FAIL. Every assertion of `IsComplete, Is.True` fails because the stub returns `Unknown`, and every `AssertSingleDiagnostic` fails on a zero diagnostic count.

- [ ] **Step 3: Implement `InterpretBaseColor`**

```csharp
private const string ColorProperty = "_Color";
private const string MainTextureProperty = "_MainTex";
private const string MainTexScrollRotateProperty = "_MainTex_ScrollRotate";
private const string MainTexHsvgProperty = "_MainTexHSVG";
private const string MainColorAdjustMaskProperty = "_MainColorAdjustMask";

private static readonly Vector4 IdentityHsvg = new Vector4(0f, 1f, 1f, 1f);

// Every block that writes fd.col.rgb before fd.albedo is copied
// (lil_pass_forward_normal.hlsl:263-443). The alpha-mask, dissolve, dither,
// depth-fade, fur, and premultiply blocks are excluded at compile time by
// LIL_RENDER on the opaque variant and therefore need no material gate.
private static readonly string[] BaseColorWriterGates =
{
    "_Invisible",
    "_ShiftBackfaceUV",
    "_UseParallax",
    "_UsePOM",
    "_UseAudioLink",
    "_UseMain2ndTex",
    "_UseMain3rdTex",
    "_MainGradationStrength",
};

private static SemanticOutput<ColorSemanticValue> InterpretBaseColor(
    Material material,
    ColorSpace activeColorSpace,
    List<LilToonSemanticDiagnostic> diagnostics)
{
    if (activeColorSpace != ColorSpace.Linear)
    {
        return RecordUnknown<ColorSemanticValue>(
            diagnostics,
            LilToonSemanticOutput.BaseColor,
            LilToonSemanticDiagnosticCode.UnsupportedColorSpace,
            activeColorSpace.ToString());
    }

    var writerGate = FirstFailedZeroGate(material, BaseColorWriterGates);
    if (writerGate != null)
    {
        return RecordUnknown<ColorSemanticValue>(
            diagnostics,
            LilToonSemanticOutput.BaseColor,
            LilToonSemanticDiagnosticCode.UnsupportedFeature,
            writerGate);
    }

    // lilToon has no tone-correction toggle: lilToneCorrection always runs when
    // compiled in, so the identity must be proven from the parameter itself.
    if (!material.HasProperty(MainTexHsvgProperty) ||
        material.GetVector(MainTexHsvgProperty) != IdentityHsvg)
    {
        return RecordUnknown<ColorSemanticValue>(
            diagnostics,
            LilToonSemanticOutput.BaseColor,
            LilToonSemanticDiagnosticCode.UnsupportedFeature,
            MainTexHsvgProperty);
    }

    // An assigned adjust mask lerps between corrected and uncorrected colour,
    // a second sample the closed vocabulary cannot express.
    if (material.HasProperty(MainColorAdjustMaskProperty) &&
        material.GetTexture(MainColorAdjustMaskProperty) != null)
    {
        return RecordUnknown<ColorSemanticValue>(
            diagnostics,
            LilToonSemanticOutput.BaseColor,
            LilToonSemanticDiagnosticCode.UnsupportedFeature,
            MainColorAdjustMaskProperty);
    }

    var color = material.GetColor(ColorProperty);
    if (!IsFinite(color.r) || !IsFinite(color.g) || !IsFinite(color.b))
    {
        return RecordUnknown<ColorSemanticValue>(
            diagnostics,
            LilToonSemanticOutput.BaseColor,
            LilToonSemanticDiagnosticCode.UnsupportedFeature,
            ColorProperty);
    }

    var linear = color.linear;
    var tint = new Vector3(linear.r, linear.g, linear.b);

    var texture = material.GetTexture(MainTextureProperty);
    if (texture == null)
    {
        return SemanticOutput<ColorSemanticValue>.Complete(
            ColorSemanticValue.Constant(tint));
    }

    if (!TryGetMainUvMapping(material, out var mapping))
    {
        return RecordUnknown<ColorSemanticValue>(
            diagnostics,
            LilToonSemanticOutput.BaseColor,
            LilToonSemanticDiagnosticCode.UnsupportedUv,
            MainTexScrollRotateProperty);
    }

    if (!UnityTextureEvidence.TryGetSourceId(texture, out var sourceId))
    {
        return RecordUnknown<ColorSemanticValue>(
            diagnostics,
            LilToonSemanticOutput.BaseColor,
            LilToonSemanticDiagnosticCode.UnstableTextureIdentity,
            MainTextureProperty);
    }

    if (!UnityTextureEvidence.TryGetSampling(texture, out var sampling))
    {
        return RecordUnknown<ColorSemanticValue>(
            diagnostics,
            LilToonSemanticOutput.BaseColor,
            LilToonSemanticDiagnosticCode.UnsupportedSampling,
            MainTextureProperty);
    }

    if (!UnityTextureEvidence.TryGetColorInterpretation(
            texture, out var interpretation) ||
        !TryProveSampledColorInUnitRange(texture))
    {
        // lilToneCorrection is the identity only on [0,1]: its saturate calls
        // would clamp anything above 1.
        return RecordUnknown<ColorSemanticValue>(
            diagnostics,
            LilToonSemanticOutput.BaseColor,
            LilToonSemanticDiagnosticCode.UnsupportedTextureImport,
            MainTextureProperty);
    }

    var sample = new TextureSample(sourceId, mapping, sampling);
    var value = tint == Vector3.one
        ? ColorSemanticValue.Texture(sample, interpretation)
        : ColorSemanticValue.TextureTimesConstant(sample, interpretation, tint);
    return SemanticOutput<ColorSemanticValue>.Complete(value);
}

/// <summary>
/// The main UV is always UV0 with _MainTex_ST, valid only at exactly zero
/// scroll and rotate. lilToon has no main-texture channel selector.
/// </summary>
private static bool TryGetMainUvMapping(Material material, out UvMapping mapping)
{
    mapping = default;

    if (!material.HasProperty(MainTexScrollRotateProperty))
    {
        return false;
    }

    var scrollRotate = material.GetVector(MainTexScrollRotateProperty);
    if (!IsFinite(scrollRotate) || scrollRotate != Vector4.zero)
    {
        return false;
    }

    var scale = material.GetTextureScale(MainTextureProperty);
    var offset = material.GetTextureOffset(MainTextureProperty);
    if (!IsFinite(scale) || !IsFinite(offset))
    {
        return false;
    }

    mapping = new UvMapping(0, scale, offset);
    return true;
}

/// <summary>
/// Positively proves that every effective sampled colour value for this
/// texture is finite and confined to [0,1], the range in which
/// lilToneCorrection at _MainTexHSVG = (0,1,1,1) is the identity.
///
/// Only imported formats on the allow-list below succeed. Every other
/// format — signed-normalized, half, float, shared-exponent, BC6H — and every
/// texture whose format or importer cannot be read, refuses. A format Unity
/// adds in a future version is not on the list and therefore refuses. Nothing
/// is clamped, approximated, or assumed bounded.
/// </summary>
private static bool TryProveSampledColorInUnitRange(Texture texture)
{
    if (texture == null)
    {
        return false;
    }

    var path = AssetDatabase.GetAssetPath(texture);
    if (string.IsNullOrEmpty(path) ||
        !(AssetImporter.GetAtPath(path) is TextureImporter))
    {
        return false;
    }

    return BoundedColorFormats.Contains(texture.graphicsFormat);
}

/// <summary>
/// Unsigned-normalized and sRGB formats, whose decoded values are exactly the
/// closed interval [0,1]. Enumerated rather than pattern-matched so that an
/// unrecognized format cannot pass by accident.
/// </summary>
private static readonly HashSet<GraphicsFormat> BoundedColorFormats =
    new HashSet<GraphicsFormat>
    {
        GraphicsFormat.R8_UNorm,
        GraphicsFormat.R8G8_UNorm,
        GraphicsFormat.R8G8B8_UNorm,
        GraphicsFormat.R8G8B8A8_UNorm,
        GraphicsFormat.R8G8B8_SRGB,
        GraphicsFormat.R8G8B8A8_SRGB,
        GraphicsFormat.B8G8R8_UNorm,
        GraphicsFormat.B8G8R8A8_UNorm,
        GraphicsFormat.B8G8R8_SRGB,
        GraphicsFormat.B8G8R8A8_SRGB,
        GraphicsFormat.R16_UNorm,
        GraphicsFormat.R16G16_UNorm,
        GraphicsFormat.R16G16B16_UNorm,
        GraphicsFormat.R16G16B16A16_UNorm,
        GraphicsFormat.A8_UNorm,
        GraphicsFormat.R5G6B5_UNormPack16,
        GraphicsFormat.R4G4B4A4_UNormPack16,
        GraphicsFormat.R5G5B5A1_UNormPack16,
        GraphicsFormat.RGB_DXT1_UNorm,
        GraphicsFormat.RGB_DXT1_SRGB,
        GraphicsFormat.RGBA_DXT3_UNorm,
        GraphicsFormat.RGBA_DXT3_SRGB,
        GraphicsFormat.RGBA_DXT5_UNorm,
        GraphicsFormat.RGBA_DXT5_SRGB,
        GraphicsFormat.R_BC4_UNorm,
        GraphicsFormat.RG_BC5_UNorm,
        GraphicsFormat.RGBA_BC7_UNorm,
        GraphicsFormat.RGBA_BC7_SRGB,
        GraphicsFormat.RGB_ETC_UNorm,
        GraphicsFormat.RGB_ETC2_UNorm,
        GraphicsFormat.RGB_ETC2_SRGB,
        GraphicsFormat.RGB_A1_ETC2_UNorm,
        GraphicsFormat.RGB_A1_ETC2_SRGB,
        GraphicsFormat.RGBA_ETC2_UNorm,
        GraphicsFormat.RGBA_ETC2_SRGB,
        GraphicsFormat.RGBA_ASTC4X4_UNorm,
        GraphicsFormat.RGBA_ASTC4X4_SRGB,
        GraphicsFormat.RGBA_ASTC5X5_UNorm,
        GraphicsFormat.RGBA_ASTC5X5_SRGB,
        GraphicsFormat.RGBA_ASTC6X6_UNorm,
        GraphicsFormat.RGBA_ASTC6X6_SRGB,
        GraphicsFormat.RGBA_ASTC8X8_UNorm,
        GraphicsFormat.RGBA_ASTC8X8_SRGB,
        GraphicsFormat.RGBA_ASTC10X10_UNorm,
        GraphicsFormat.RGBA_ASTC10X10_SRGB,
        GraphicsFormat.RGBA_ASTC12X12_UNorm,
        GraphicsFormat.RGBA_ASTC12X12_SRGB,
    };
```

The file needs `using UnityEngine.Experimental.Rendering;` for `GraphicsFormat`. If any listed member does not exist in the project's Unity version, delete that entry rather than substituting a pattern match — a shorter allow-list refuses more and stays sound.

- [ ] **Step 4: Run tests to verify they pass**

Run `refresh_unity`, then the focused run. Expected: PASS.

- [ ] **Step 5: Scope inspection**

```bash
git status --porcelain --untracked-files=all
```

Expected delta from Task 3: `LilToonBaseColorTests.cs` and its `.meta`; `LilToonMaterialSemantics.cs` modified. Do not stage and do not commit.

---

### Task 5: Alpha

**Files:**

- Create: `Packages/com.alrauna.amuse/Tests/Editor/Semantics/LilToon/LilToonAlphaTests.cs`
- Modify: `Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonMaterialSemantics.cs` (`InterpretAlpha`)

**Interfaces:**

- Consumes: `ScalarSemanticValue`.
- Produces: no new public surface.

**Traced equation** (`lil_pass_forward_normal.hlsl:393-396`, `lil_common_frag_alpha.hlsl:12`): on `LIL_RENDER 0`, `fd.col.a = 1.0` unconditionally, and the entire subpass alpha path is excluded by `#if LIL_RENDER > 0`. Only fragment-removing mechanisms remain relevant.

- [ ] **Step 1: Write the failing tests**

```csharp
using Alrauna.Amuse.Editor.Semantics;
using Alrauna.Amuse.Editor.Semantics.LilToon;
using NUnit.Framework;
using UnityEngine;

namespace Alrauna.Amuse.Tests.Editor.Semantics.LilToon
{
    public sealed class LilToonAlphaTests : LilToonFixtureTestBase
    {
        [Test]
        public void OpaqueVariant_IsConstantOne()
        {
            var material = NewFixtureMaterial();

            var alpha = Interpret(material).Semantics.Alpha;

            Assert.That(alpha.IsComplete, Is.True);
            var value = alpha.GetCompleteValue();
            Assert.That(value.Kind, Is.EqualTo(ScalarSemanticValueKind.Constant));
            Assert.That(value.GetConstantValue(), Is.EqualTo(1f));
        }

        [Test]
        public void OpaqueVariant_IgnoresColorAlphaAndMainTexAlpha()
        {
            var material = NewFixtureMaterial();
            material.SetColor("_Color", new Color(1f, 1f, 1f, 0.25f));
            material.SetTexture("_MainTex", ImportTexture("alphatex"));

            var value = Interpret(material).Semantics.Alpha.GetCompleteValue();

            Assert.That(value.Kind, Is.EqualTo(ScalarSemanticValueKind.Constant));
            Assert.That(value.GetConstantValue(), Is.EqualTo(1f));
        }

        [TestCase("_Invisible")]
        [TestCase("_UDIMDiscardCompile")]
        public void CoverageMechanism_KeepsAlphaUnknown(string property)
        {
            var material = NewFixtureMaterial();
            material.SetFloat(property, 1f);

            var result = Interpret(material);

            Assert.That(result.Semantics.Alpha.IsComplete, Is.False);
            AssertSingleDiagnostic(
                result,
                LilToonSemanticOutput.Alpha,
                LilToonSemanticDiagnosticCode.UnsupportedFeature,
                property);
        }

        [Test]
        public void MissingCoverageProperty_KeepsAlphaUnknown()
        {
            var material = NewFixtureMaterial();
            material.SetFloat("_UDIMDiscardCompile", float.NaN);

            var result = Interpret(material);

            Assert.That(result.Semantics.Alpha.IsComplete, Is.False);
        }
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run `refresh_unity`, then:

```text
test_names: Alrauna.Amuse.Tests.Editor.Semantics.LilToon.LilToonAlphaTests
```

Expected: FAIL, the stub returns `Unknown`.

- [ ] **Step 3: Implement `InterpretAlpha`**

```csharp
// On LIL_RENDER 0 the alpha value is forced to exactly one after every
// alpha-writing block (lil_pass_forward_normal.hlsl:393-396), and the entire
// subpass alpha path is excluded by #if LIL_RENDER > 0. Only mechanisms that
// remove fragments can still change effective coverage.
private static readonly string[] AlphaCoverageGates =
{
    "_Invisible",
    "_UDIMDiscardCompile",
};

private static SemanticOutput<ScalarSemanticValue> InterpretAlpha(
    Material material,
    List<LilToonSemanticDiagnostic> diagnostics)
{
    var coverageGate = FirstFailedZeroGate(material, AlphaCoverageGates);
    if (coverageGate != null)
    {
        return RecordUnknown<ScalarSemanticValue>(
            diagnostics,
            LilToonSemanticOutput.Alpha,
            LilToonSemanticDiagnosticCode.UnsupportedFeature,
            coverageGate);
    }

    return SemanticOutput<ScalarSemanticValue>.Complete(
        ScalarSemanticValue.Constant(1f));
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run `refresh_unity`, then the focused run. Expected: PASS.

- [ ] **Step 5: Scope inspection**

```bash
git status --porcelain --untracked-files=all
```

Expected delta from Task 4: `LilToonAlphaTests.cs` and its `.meta`; `LilToonMaterialSemantics.cs` modified. Do not stage and do not commit.

---

### Task 6: Normal

**Files:**

- Create: `Packages/com.alrauna.amuse/Tests/Editor/Semantics/LilToon/LilToonNormalTests.cs`
- Modify: `Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonMaterialSemantics.cs` (`InterpretNormal`)

**Interfaces:**

- Consumes: `NormalSemanticValue`, `UnityTextureEvidence`, `UvMapping`.
- Produces: no new public surface.

**Traced equation** (`lil_common_frag.hlsl:563-575`):

```
if(_UseBumpMap) {
    normalTex = LIL_SAMPLE_2D_ST(_BumpMap, sampler_MainTex, uvMain)
              = _BumpMap.Sample(sampler_MainTex, uvMain * _BumpMap_ST.xy + _BumpMap_ST.zw)
    normalmap = lilUnpackNormalScale(normalTex, _BumpScale)
}
```

Two facts drive the tests: the UV is an **affine composition** of `_MainTex_ST` then `_BumpMap_ST`, and the sampler comes from **`_MainTex`**, not `_BumpMap`.

- [ ] **Step 1: Write the failing tests**

```csharp
using Alrauna.Amuse.Editor.Semantics;
using Alrauna.Amuse.Editor.Semantics.LilToon;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Alrauna.Amuse.Tests.Editor.Semantics.LilToon
{
    public sealed class LilToonNormalTests : LilToonFixtureTestBase
    {
        private Material NormalMaterial(out Texture2D bump)
        {
            var material = NewFixtureMaterial();
            material.SetTexture("_MainTex", ImportTexture("mainforsampler"));
            bump = ImportNormalMap("bump");
            material.SetTexture("_BumpMap", bump);
            material.SetFloat("_UseBumpMap", 1f);
            return material;
        }

        [Test]
        public void BumpMapDisabled_IsUnmodified()
        {
            var material = NewFixtureMaterial();

            var normal = Interpret(material).Semantics.Normal;

            Assert.That(normal.IsComplete, Is.True);
            Assert.That(
                normal.GetCompleteValue().Kind,
                Is.EqualTo(NormalSemanticValueKind.Unmodified));
        }

        [Test]
        public void BumpMapEnabledWithoutTexture_IsUnmodified()
        {
            var material = NewFixtureMaterial();
            material.SetFloat("_UseBumpMap", 1f);

            var normal = Interpret(material).Semantics.Normal;

            // The "bump" default resolves to (0.5,0.5,1,0.5), which
            // lilUnpackNormalScale maps to exactly (0,0,1).
            Assert.That(normal.IsComplete, Is.True);
            Assert.That(
                normal.GetCompleteValue().Kind,
                Is.EqualTo(NormalSemanticValueKind.Unmodified));
        }

        [Test]
        public void CanonicalBumpMap_IsTangentSpaceNormalMap()
        {
            var material = NormalMaterial(out var bump);

            var normal = Interpret(material).Semantics.Normal;

            Assert.That(normal.IsComplete, Is.True);
            var value = normal.GetCompleteValue();
            Assert.That(
                value.Kind,
                Is.EqualTo(NormalSemanticValueKind.TangentSpaceNormalMap));
            Assert.That(
                UnityTextureEvidence.TryGetSourceId(bump, out var expectedId),
                Is.True);
            Assert.That(value.GetTextureSample().Source, Is.EqualTo(expectedId));
        }

        [Test]
        public void BumpMapUv_ComposesMainThenBumpAffineTransforms()
        {
            var material = NormalMaterial(out _);
            material.SetTextureScale("_MainTex", new Vector2(2f, 4f));
            material.SetTextureOffset("_MainTex", new Vector2(0.1f, 0.2f));
            material.SetTextureScale("_BumpMap", new Vector2(3f, 0.5f));
            material.SetTextureOffset("_BumpMap", new Vector2(0.5f, -0.25f));

            var sample = Interpret(material)
                .Semantics.Normal.GetCompleteValue().GetTextureSample();

            // uv = (uv0 * mainScale + mainOffset) * bumpScale + bumpOffset
            Assert.That(sample.Coordinates.Channel, Is.EqualTo(0));
            Assert.That(
                sample.Coordinates.Scale,
                Is.EqualTo(new Vector2(2f * 3f, 4f * 0.5f)));
            Assert.That(
                sample.Coordinates.Offset,
                Is.EqualTo(new Vector2(
                    0.1f * 3f + 0.5f,
                    0.2f * 0.5f + -0.25f)));
        }

        [Test]
        public void BumpMapSampler_ComesFromMainTexNotBumpMap()
        {
            var material = NormalMaterial(out _);
            material.SetTexture(
                "_MainTex",
                ImportTexture("mippedmain", importer => importer.mipmapEnabled = true));

            var result = Interpret(material);

            Assert.That(result.Semantics.Normal.IsComplete, Is.False);
            AssertSingleDiagnostic(
                result,
                LilToonSemanticOutput.Normal,
                LilToonSemanticDiagnosticCode.UnsupportedSampling,
                "_MainTex");
        }

        [Test]
        public void MissingMainTex_LeavesNormalUnknown()
        {
            var material = NewFixtureMaterial();
            material.SetTexture("_BumpMap", ImportNormalMap("lonebump"));
            material.SetFloat("_UseBumpMap", 1f);

            var result = Interpret(material);

            Assert.That(result.Semantics.Normal.IsComplete, Is.False);
            AssertSingleDiagnostic(
                result,
                LilToonSemanticOutput.Normal,
                LilToonSemanticDiagnosticCode.UnsupportedSampling,
                "_MainTex");
        }

        [TestCase(0.5f)]
        [TestCase(-1f)]
        [TestCase(2f)]
        public void NonUnitBumpScale_IsUnknown(float scale)
        {
            var material = NormalMaterial(out _);
            material.SetFloat("_BumpScale", scale);

            var result = Interpret(material);

            Assert.That(result.Semantics.Normal.IsComplete, Is.False);
            AssertSingleDiagnostic(
                result,
                LilToonSemanticOutput.Normal,
                LilToonSemanticDiagnosticCode.UnsupportedFeature,
                "_BumpScale");
        }

        [TestCase("_UseBump2ndMap")]
        [TestCase("_UseAnisotropy")]
        [TestCase("_UseParallax")]
        [TestCase("_UsePOM")]
        [TestCase("_ShiftBackfaceUV")]
        public void EnabledNormalWriter_IsUnknown(string property)
        {
            var material = NormalMaterial(out _);
            material.SetFloat(property, 1f);

            var result = Interpret(material);

            Assert.That(result.Semantics.Normal.IsComplete, Is.False);
            AssertSingleDiagnostic(
                result,
                LilToonSemanticOutput.Normal,
                LilToonSemanticDiagnosticCode.UnsupportedFeature,
                property);
        }

        [Test]
        public void NonCanonicalNormalImport_IsUnknown()
        {
            var material = NewFixtureMaterial();
            material.SetTexture("_MainTex", ImportTexture("mainok"));
            material.SetTexture("_BumpMap", ImportTexture("notanormal"));
            material.SetFloat("_UseBumpMap", 1f);

            var result = Interpret(material);

            Assert.That(result.Semantics.Normal.IsComplete, Is.False);
            AssertSingleDiagnostic(
                result,
                LilToonSemanticOutput.Normal,
                LilToonSemanticDiagnosticCode.UnsupportedTextureImport,
                "_BumpMap");
        }

        [TestCase("LIL_FEATURE_NORMAL_1ST")]
        [TestCase("LIL_FEATURE_BumpMap")]
        public void StrippedFeature_KeepsNormalUnknown(string missing)
        {
            var material = NormalMaterial(out _);
            var features = System.Array.FindAll(
                AllFeatures, f => f != missing);

            var result = Interpret(material, features);

            Assert.That(result.Semantics.Normal.IsComplete, Is.False);
            AssertSingleDiagnostic(
                result,
                LilToonSemanticOutput.Normal,
                LilToonSemanticDiagnosticCode.MissingFeatureCompilation,
                missing);
        }

        [Test]
        public void StrippedFeature_WithBumpMapDisabled_StaysUnmodified()
        {
            var material = NewFixtureMaterial();

            var result = Interpret(material, new string[0]);

            // Nothing is claimed, so no compile-time evidence is needed.
            Assert.That(result.Semantics.Normal.IsComplete, Is.True);
            Assert.That(
                result.Semantics.Normal.GetCompleteValue().Kind,
                Is.EqualTo(NormalSemanticValueKind.Unmodified));
        }
    }
}
```

`StrippedFeature_KeepsNormalUnknown` is the regression test for the false-positive hazard described in the design spec. It must fail before the implementation exists and pass after.

- [ ] **Step 2: Run tests to verify they fail**

Run `refresh_unity`, then:

```text
test_names: Alrauna.Amuse.Tests.Editor.Semantics.LilToon.LilToonNormalTests
```

Expected: FAIL, the stub returns `Unknown` for every case including the two `Unmodified` ones.

- [ ] **Step 3: Implement `InterpretNormal`**

```csharp
private const string UseBumpMapProperty = "_UseBumpMap";
private const string BumpMapProperty = "_BumpMap";
private const string BumpScaleProperty = "_BumpScale";
private const string NormalFirstFeature = "LIL_FEATURE_NORMAL_1ST";
private const string BumpMapFeature = "LIL_FEATURE_BumpMap";

// Enabled writers that perturb, blend, or re-target the tangent-space normal,
// plus the UV determinants shared with the main sample.
private static readonly string[] NormalWriterGates =
{
    "_UseBump2ndMap",
    "_UseAnisotropy",
    "_UseParallax",
    "_UsePOM",
    "_ShiftBackfaceUV",
};

private static SemanticOutput<NormalSemanticValue> InterpretNormal(
    Material material,
    IReadOnlyCollection<string> compiledFeatures,
    List<LilToonSemanticDiagnostic> diagnostics)
{
    if (!TryReadBinary(material, UseBumpMapProperty, out var useBumpMap))
    {
        return RecordUnknown<NormalSemanticValue>(
            diagnostics,
            LilToonSemanticOutput.Normal,
            LilToonSemanticDiagnosticCode.UnsupportedFeature,
            UseBumpMapProperty);
    }

    var texture = material.GetTexture(BumpMapProperty);

    // Nothing is claimed: the toggle is off, or the "bump" default resolves to
    // (0.5,0.5,1,0.5), which lilUnpackNormalScale maps to exactly (0,0,1).
    if (!useBumpMap || texture == null)
    {
        return SemanticOutput<NormalSemanticValue>.Complete(
            NormalSemanticValue.Unmodified());
    }

    // A claimed feature must be compiled in: lilToon's per-project setting can
    // strip it while _UseBumpMap stays set, which would make a claim false.
    foreach (var feature in new[] { NormalFirstFeature, BumpMapFeature })
    {
        if (!compiledFeatures.Contains(feature))
        {
            return RecordUnknown<NormalSemanticValue>(
                diagnostics,
                LilToonSemanticOutput.Normal,
                LilToonSemanticDiagnosticCode.MissingFeatureCompilation,
                feature);
        }
    }

    var scale = material.HasProperty(BumpScaleProperty)
        ? material.GetFloat(BumpScaleProperty)
        : float.NaN;
    if (!IsFinite(scale) || scale != 1f)
    {
        return RecordUnknown<NormalSemanticValue>(
            diagnostics,
            LilToonSemanticOutput.Normal,
            LilToonSemanticDiagnosticCode.UnsupportedFeature,
            BumpScaleProperty);
    }

    var writerGate = FirstFailedZeroGate(material, NormalWriterGates);
    if (writerGate != null)
    {
        return RecordUnknown<NormalSemanticValue>(
            diagnostics,
            LilToonSemanticOutput.Normal,
            LilToonSemanticDiagnosticCode.UnsupportedFeature,
            writerGate);
    }

    if (!TryGetComposedUvMapping(material, BumpMapProperty, out var mapping))
    {
        return RecordUnknown<NormalSemanticValue>(
            diagnostics,
            LilToonSemanticOutput.Normal,
            LilToonSemanticDiagnosticCode.UnsupportedUv,
            BumpMapProperty);
    }

    // The bump map is sampled with sampler_MainTex, so the sampler state comes
    // from the _MainTex asset, not from _BumpMap.
    var mainTexture = material.GetTexture(MainTextureProperty);
    if (!UnityTextureEvidence.TryGetSampling(mainTexture, out var sampling))
    {
        return RecordUnknown<NormalSemanticValue>(
            diagnostics,
            LilToonSemanticOutput.Normal,
            LilToonSemanticDiagnosticCode.UnsupportedSampling,
            MainTextureProperty);
    }

    if (!UnityTextureEvidence.TryGetSourceId(texture, out var sourceId))
    {
        return RecordUnknown<NormalSemanticValue>(
            diagnostics,
            LilToonSemanticOutput.Normal,
            LilToonSemanticDiagnosticCode.UnstableTextureIdentity,
            BumpMapProperty);
    }

    if (!UnityTextureEvidence.IsCanonicalNormalMapImport(texture))
    {
        return RecordUnknown<NormalSemanticValue>(
            diagnostics,
            LilToonSemanticOutput.Normal,
            LilToonSemanticDiagnosticCode.UnsupportedTextureImport,
            BumpMapProperty);
    }

    return SemanticOutput<NormalSemanticValue>.Complete(
        NormalSemanticValue.TangentSpaceNormalMap(
            new TextureSample(sourceId, mapping, sampling)));
}

/// <summary>
/// Secondary maps sample at <c>uvMain * tex_ST.xy + tex_ST.zw</c>, so their
/// mapping is the composition of the main transform with their own. The
/// composition of two affine maps is affine, so the closed UvMapping expresses
/// it exactly.
/// </summary>
private static bool TryGetComposedUvMapping(
    Material material,
    string textureProperty,
    out UvMapping mapping)
{
    mapping = default;

    if (!TryGetMainUvMapping(material, out var main))
    {
        return false;
    }

    var scale = material.GetTextureScale(textureProperty);
    var offset = material.GetTextureOffset(textureProperty);
    if (!IsFinite(scale) || !IsFinite(offset))
    {
        return false;
    }

    var composedScale = new Vector2(
        main.Scale.x * scale.x,
        main.Scale.y * scale.y);
    var composedOffset = new Vector2(
        main.Offset.x * scale.x + offset.x,
        main.Offset.y * scale.y + offset.y);

    if (!IsFinite(composedScale) || !IsFinite(composedOffset))
    {
        return false;
    }

    mapping = new UvMapping(0, composedScale, composedOffset);
    return true;
}
```

`compiledFeatures.Contains` requires `using System.Linq;` in the file.

- [ ] **Step 4: Run tests to verify they pass**

Run `refresh_unity`, then the focused run. Expected: PASS.

- [ ] **Step 5: Scope inspection**

```bash
git status --porcelain --untracked-files=all
```

Expected delta from Task 5: `LilToonNormalTests.cs` and its `.meta`; `LilToonMaterialSemantics.cs` modified. Do not stage and do not commit.

---

### Task 7: Emission

**Files:**

- Create: `Packages/com.alrauna.amuse/Tests/Editor/Semantics/LilToon/LilToonEmissionTests.cs`
- Modify: `Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonMaterialSemantics.cs` (`InterpretEmission`)

**Interfaces:**

- Consumes: `ColorSemanticValue`, `UnityTextureEvidence`, `UvMapping`.
- Produces: no new public surface.

**Traced equation** (`lil_common_frag.hlsl:1813-1868`):

```
emissionColor  = _EmissionColor
emissionColor *= LIL_GET_EMITEX(_EmissionMap, uvSelected)      // if map assigned
emissionColor *= LIL_GET_EMIMASK(_EmissionBlendMask, uv0)      // if mask assigned
emissionBlend  = _EmissionBlend * lilCalcBlink(_EmissionBlink) * emissionColor.a
col.rgb        = lilBlendColor(col.rgb, emissionColor.rgb, emissionBlend, _EmissionBlendMode)
```

`lilBlendColor` with mode `1` (Add) gives `lerp(dst, dst + src, a) = dst + a * src`, a true additive emission. `lilCalcBlink` is exactly `1` when `_EmissionBlink.x == 0`.

- [ ] **Step 1: Write the failing tests**

```csharp
using Alrauna.Amuse.Editor.Semantics;
using Alrauna.Amuse.Editor.Semantics.LilToon;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Alrauna.Amuse.Tests.Editor.Semantics.LilToon
{
    public sealed class LilToonEmissionTests : LilToonFixtureTestBase
    {
        [Test]
        public void EmissionDisabledAndNoWriters_IsConstantZero()
        {
            var material = NewFixtureMaterial();

            var emission = Interpret(material).Semantics.Emission;

            Assert.That(emission.IsComplete, Is.True);
            var value = emission.GetCompleteValue();
            Assert.That(value.Kind, Is.EqualTo(ColorSemanticValueKind.Constant));
            Assert.That(value.GetConstantValue(), Is.EqualTo(Vector3.zero));
        }

        [TestCase("_UseEmission2nd")]
        [TestCase("_UseReflection")]
        [TestCase("_UseMatCap")]
        [TestCase("_UseMatCap2nd")]
        [TestCase("_UseRim")]
        [TestCase("_UseRimShade")]
        [TestCase("_UseGlitter")]
        [TestCase("_UseBacklight")]
        [TestCase("_UseAudioLink")]
        public void EnabledEmissiveWriter_BlocksZeroClaim(string property)
        {
            var material = NewFixtureMaterial();
            material.SetFloat(property, 1f);

            var result = Interpret(material);

            Assert.That(result.Semantics.Emission.IsComplete, Is.False);
            AssertSingleDiagnostic(
                result,
                LilToonSemanticOutput.Emission,
                LilToonSemanticDiagnosticCode.UnsupportedFeature,
                property);
        }

        [Test]
        public void OpaqueBackfaceColor_BlocksZeroClaim()
        {
            var material = NewFixtureMaterial();
            material.SetColor("_BackfaceColor", new Color(1f, 0f, 0f, 1f));

            var result = Interpret(material);

            Assert.That(result.Semantics.Emission.IsComplete, Is.False);
            AssertSingleDiagnostic(
                result,
                LilToonSemanticOutput.Emission,
                LilToonSemanticDiagnosticCode.UnsupportedFeature,
                "_BackfaceColor");
        }

        [Test]
        public void EmissionWithoutMap_IsScaledConstant()
        {
            var material = NewFixtureMaterial();
            material.SetFloat("_UseEmission", 1f);
            material.SetColor("_EmissionColor", new Color(1f, 0.5f, 0.25f, 0.5f));
            material.SetFloat("_EmissionBlend", 0.5f);

            var value = Interpret(material).Semantics.Emission.GetCompleteValue();

            Assert.That(value.Kind, Is.EqualTo(ColorSemanticValueKind.Constant));
            var linear = new Color(1f, 0.5f, 0.25f, 0.5f).linear;
            var expected =
                new Vector3(linear.r, linear.g, linear.b) * (0.5f * 0.5f);
            Assert.That(value.GetConstantValue(), Is.EqualTo(expected));
        }

        [Test]
        public void EmissionWithOpaqueMap_IsTextureTimesConstant()
        {
            var material = NewFixtureMaterial();
            material.SetFloat("_UseEmission", 1f);
            material.SetColor("_EmissionColor", new Color(1f, 1f, 1f, 0.5f));
            var map = ImportOpaqueColorMap("emissive");
            material.SetTexture("_EmissionMap", map);
            material.SetTextureScale("_EmissionMap", new Vector2(2f, 2f));
            material.SetFloat("_EmissionMap_UVMode", 2f);

            var value = Interpret(material).Semantics.Emission.GetCompleteValue();

            Assert.That(
                value.Kind,
                Is.EqualTo(ColorSemanticValueKind.TextureSampleTimesConstant));
            var sample = value.GetTextureSample();
            // Emission uses direct mapping on the selected channel, not the
            // composed main UV.
            Assert.That(sample.Coordinates.Channel, Is.EqualTo(2));
            Assert.That(sample.Coordinates.Scale, Is.EqualTo(new Vector2(2f, 2f)));
            Assert.That(value.GetMultiplier(), Is.EqualTo(Vector3.one * 0.5f));
        }

        [Test]
        public void EmissionMapWithAlpha_IsUnknown()
        {
            var material = NewFixtureMaterial();
            material.SetFloat("_UseEmission", 1f);
            material.SetTexture(
                "_EmissionMap",
                ImportTexture(
                    "rgbaEmissive",
                    importer =>
                        importer.alphaSource =
                            TextureImporterAlphaSource.FromInput));

            var result = Interpret(material);

            // rgb multiplied by the same sample's alpha is not representable.
            Assert.That(result.Semantics.Emission.IsComplete, Is.False);
            AssertSingleDiagnostic(
                result,
                LilToonSemanticOutput.Emission,
                LilToonSemanticDiagnosticCode.UnsupportedTextureImport,
                "_EmissionMap");
        }

        [TestCase(0f)]
        [TestCase(2f)]
        [TestCase(3f)]
        public void NonAdditiveBlendMode_IsUnknown(float mode)
        {
            var material = NewFixtureMaterial();
            material.SetFloat("_UseEmission", 1f);
            material.SetFloat("_EmissionBlendMode", mode);

            var result = Interpret(material);

            Assert.That(result.Semantics.Emission.IsComplete, Is.False);
            AssertSingleDiagnostic(
                result,
                LilToonSemanticOutput.Emission,
                LilToonSemanticDiagnosticCode.UnsupportedFeature,
                "_EmissionBlendMode");
        }

        [Test]
        public void EnabledBlink_IsUnknown()
        {
            var material = NewFixtureMaterial();
            material.SetFloat("_UseEmission", 1f);
            material.SetVector("_EmissionBlink", new Vector4(1f, 0f, 3.141593f, 0f));

            var result = Interpret(material);

            Assert.That(result.Semantics.Emission.IsComplete, Is.False);
            AssertSingleDiagnostic(
                result,
                LilToonSemanticOutput.Emission,
                LilToonSemanticDiagnosticCode.UnsupportedFeature,
                "_EmissionBlink");
        }

        [TestCase("_EmissionMainStrength")]
        [TestCase("_EmissionFluorescence")]
        [TestCase("_EmissionUseGrad")]
        [TestCase("_AudioLink2Emission")]
        [TestCase("_EmissionParallaxDepth")]
        public void EnabledEmissionModifier_IsUnknown(string property)
        {
            var material = NewFixtureMaterial();
            material.SetFloat("_UseEmission", 1f);
            material.SetFloat(property, 1f);

            var result = Interpret(material);

            Assert.That(result.Semantics.Emission.IsComplete, Is.False);
            AssertSingleDiagnostic(
                result,
                LilToonSemanticOutput.Emission,
                LilToonSemanticDiagnosticCode.UnsupportedFeature,
                property);
        }

        [Test]
        public void AssignedBlendMask_IsUnknown()
        {
            var material = NewFixtureMaterial();
            material.SetFloat("_UseEmission", 1f);
            material.SetTexture("_EmissionBlendMask", ImportTexture("emimask"));

            var result = Interpret(material);

            Assert.That(result.Semantics.Emission.IsComplete, Is.False);
            AssertSingleDiagnostic(
                result,
                LilToonSemanticOutput.Emission,
                LilToonSemanticDiagnosticCode.UnsupportedFeature,
                "_EmissionBlendMask");
        }

        [Test]
        public void RimUvMode_IsUnsupportedUv()
        {
            var material = NewFixtureMaterial();
            material.SetFloat("_UseEmission", 1f);
            material.SetTexture("_EmissionMap", ImportOpaqueColorMap("rimuv"));
            material.SetFloat("_EmissionMap_UVMode", 4f);

            var result = Interpret(material);

            Assert.That(result.Semantics.Emission.IsComplete, Is.False);
            AssertSingleDiagnostic(
                result,
                LilToonSemanticOutput.Emission,
                LilToonSemanticDiagnosticCode.UnsupportedUv,
                "_EmissionMap_UVMode");
        }

        [Test]
        public void EmissionMapSampler_ComesFromEmissionMapNotMainTex()
        {
            var material = NewFixtureMaterial();
            material.SetFloat("_UseEmission", 1f);
            material.SetTexture(
                "_MainTex",
                ImportTexture("mippedmain", importer => importer.mipmapEnabled = true));
            material.SetTexture("_EmissionMap", ImportOpaqueColorMap("cleanemi"));

            var emission = Interpret(material).Semantics.Emission;

            // _MainTex is unsupported for BaseColor but irrelevant to emission.
            Assert.That(emission.IsComplete, Is.True);
        }

        [TestCase("LIL_FEATURE_EMISSION_1ST")]
        [TestCase("LIL_FEATURE_EmissionMap")]
        public void StrippedFeature_KeepsEmissionUnknown(string missing)
        {
            var material = NewFixtureMaterial();
            material.SetFloat("_UseEmission", 1f);
            material.SetTexture("_EmissionMap", ImportOpaqueColorMap("strip"));
            var features = System.Array.FindAll(AllFeatures, f => f != missing);

            var result = Interpret(material, features);

            Assert.That(result.Semantics.Emission.IsComplete, Is.False);
            AssertSingleDiagnostic(
                result,
                LilToonSemanticOutput.Emission,
                LilToonSemanticDiagnosticCode.MissingFeatureCompilation,
                missing);
        }

        [Test]
        public void GammaColorSpaceWithEmissionOn_IsUnknown()
        {
            var material = NewFixtureMaterial();
            material.SetFloat("_UseEmission", 1f);

            var result = LilToonMaterialSemantics.InterpretVerifiedMaterial(
                material, ColorSpace.Gamma, AllFeatures);

            Assert.That(result.Semantics.Emission.IsComplete, Is.False);
        }

        [Test]
        public void GammaColorSpaceWithEmissionOff_IsStillConstantZero()
        {
            var material = NewFixtureMaterial();

            var result = LilToonMaterialSemantics.InterpretVerifiedMaterial(
                material, ColorSpace.Gamma, AllFeatures);

            // A proven zero is independent of the working colour space.
            Assert.That(result.Semantics.Emission.IsComplete, Is.True);
            Assert.That(
                result.Semantics.Emission.GetCompleteValue().GetConstantValue(),
                Is.EqualTo(Vector3.zero));
        }
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run `refresh_unity`, then:

```text
test_names: Alrauna.Amuse.Tests.Editor.Semantics.LilToon.LilToonEmissionTests
```

Expected: FAIL, the stub returns `Unknown`.

- [ ] **Step 3: Implement `InterpretEmission`**

```csharp
private const string UseEmissionProperty = "_UseEmission";
private const string EmissionColorProperty = "_EmissionColor";
private const string EmissionMapProperty = "_EmissionMap";
private const string EmissionMapUvModeProperty = "_EmissionMap_UVMode";
private const string EmissionMapScrollRotateProperty =
    "_EmissionMap_ScrollRotate";
private const string EmissionBlendProperty = "_EmissionBlend";
private const string EmissionBlendModeProperty = "_EmissionBlendMode";
private const string EmissionBlendMaskProperty = "_EmissionBlendMask";
private const string EmissionBlinkProperty = "_EmissionBlink";
private const string BackfaceColorProperty = "_BackfaceColor";
private const string EmissionFirstFeature = "LIL_FEATURE_EMISSION_1ST";
private const string EmissionMapFeature = "LIL_FEATURE_EmissionMap";

// Every traced block after fd.albedo that adds light-independent colour. A
// zero or slot-1 claim is only sound with all of them off.
private static readonly string[] EmissiveWriterGates =
{
    "_UseEmission2nd",
    "_UseReflection",
    "_UseMatCap",
    "_UseMatCap2nd",
    "_UseRim",
    "_UseRimShade",
    "_UseGlitter",
    "_UseBacklight",
    "_UseAudioLink",
};

// Slot-1 modifiers that re-map, animate, or tint the emission term beyond the
// supported colour/map form.
private static readonly string[] EmissionModifierGates =
{
    "_EmissionMainStrength",
    "_EmissionFluorescence",
    "_EmissionUseGrad",
    "_AudioLink2Emission",
    "_EmissionParallaxDepth",
};

private static SemanticOutput<ColorSemanticValue> InterpretEmission(
    Material material,
    ColorSpace activeColorSpace,
    IReadOnlyCollection<string> compiledFeatures,
    List<LilToonSemanticDiagnostic> diagnostics)
{
    var writerGate = FirstFailedZeroGate(material, EmissiveWriterGates);
    if (writerGate != null)
    {
        return RecordUnknown<ColorSemanticValue>(
            diagnostics,
            LilToonSemanticOutput.Emission,
            LilToonSemanticDiagnosticCode.UnsupportedFeature,
            writerGate);
    }

    var backfaceColor = material.GetColor(BackfaceColorProperty);
    if (!IsFinite(backfaceColor.a) || backfaceColor.a != 0f)
    {
        return RecordUnknown<ColorSemanticValue>(
            diagnostics,
            LilToonSemanticOutput.Emission,
            LilToonSemanticDiagnosticCode.UnsupportedFeature,
            BackfaceColorProperty);
    }

    if (!TryReadBinary(material, UseEmissionProperty, out var useEmission))
    {
        return RecordUnknown<ColorSemanticValue>(
            diagnostics,
            LilToonSemanticOutput.Emission,
            LilToonSemanticDiagnosticCode.UnsupportedFeature,
            UseEmissionProperty);
    }

    // Nothing emits: a proven constant zero, independent of colour space.
    if (!useEmission)
    {
        return SemanticOutput<ColorSemanticValue>.Complete(
            ColorSemanticValue.Constant(Vector3.zero));
    }

    if (!compiledFeatures.Contains(EmissionFirstFeature))
    {
        return RecordUnknown<ColorSemanticValue>(
            diagnostics,
            LilToonSemanticOutput.Emission,
            LilToonSemanticDiagnosticCode.MissingFeatureCompilation,
            EmissionFirstFeature);
    }

    if (activeColorSpace != ColorSpace.Linear)
    {
        return RecordUnknown<ColorSemanticValue>(
            diagnostics,
            LilToonSemanticOutput.Emission,
            LilToonSemanticDiagnosticCode.UnsupportedColorSpace,
            activeColorSpace.ToString());
    }

    var modifierGate = FirstFailedZeroGate(material, EmissionModifierGates);
    if (modifierGate != null)
    {
        return RecordUnknown<ColorSemanticValue>(
            diagnostics,
            LilToonSemanticOutput.Emission,
            LilToonSemanticDiagnosticCode.UnsupportedFeature,
            modifierGate);
    }

    // Only Add (1) makes lilBlendColor an additive emission term.
    var blendMode = material.GetFloat(EmissionBlendModeProperty);
    if (!IsFinite(blendMode) || blendMode != 1f)
    {
        return RecordUnknown<ColorSemanticValue>(
            diagnostics,
            LilToonSemanticOutput.Emission,
            LilToonSemanticDiagnosticCode.UnsupportedFeature,
            EmissionBlendModeProperty);
    }

    // lilCalcBlink is exactly one when blink.x is zero.
    var blink = material.GetVector(EmissionBlinkProperty);
    if (!IsFinite(blink) || blink.x != 0f)
    {
        return RecordUnknown<ColorSemanticValue>(
            diagnostics,
            LilToonSemanticOutput.Emission,
            LilToonSemanticDiagnosticCode.UnsupportedFeature,
            EmissionBlinkProperty);
    }

    // An assigned mask multiplies a second sample this equation omits.
    if (material.HasProperty(EmissionBlendMaskProperty) &&
        material.GetTexture(EmissionBlendMaskProperty) != null)
    {
        return RecordUnknown<ColorSemanticValue>(
            diagnostics,
            LilToonSemanticOutput.Emission,
            LilToonSemanticDiagnosticCode.UnsupportedFeature,
            EmissionBlendMaskProperty);
    }

    var blend = material.GetFloat(EmissionBlendProperty);
    var color = material.GetColor(EmissionColorProperty);
    if (!IsFinite(blend) ||
        !IsFinite(color.r) || !IsFinite(color.g) ||
        !IsFinite(color.b) || !IsFinite(color.a))
    {
        return RecordUnknown<ColorSemanticValue>(
            diagnostics,
            LilToonSemanticOutput.Emission,
            LilToonSemanticDiagnosticCode.UnsupportedFeature,
            EmissionColorProperty);
    }

    var linear = color.linear;
    var tint = new Vector3(linear.r, linear.g, linear.b) * (blend * color.a);

    var texture = material.GetTexture(EmissionMapProperty);
    if (texture == null)
    {
        return SemanticOutput<ColorSemanticValue>.Complete(
            ColorSemanticValue.Constant(tint));
    }

    if (!compiledFeatures.Contains(EmissionMapFeature))
    {
        return RecordUnknown<ColorSemanticValue>(
            diagnostics,
            LilToonSemanticOutput.Emission,
            LilToonSemanticDiagnosticCode.MissingFeatureCompilation,
            EmissionMapFeature);
    }

    if (!TryGetEmissionUvMapping(material, out var mapping))
    {
        return RecordUnknown<ColorSemanticValue>(
            diagnostics,
            LilToonSemanticOutput.Emission,
            LilToonSemanticDiagnosticCode.UnsupportedUv,
            EmissionMapUvModeProperty);
    }

    // The emission map declares its own sampler_EmissionMap.
    if (!UnityTextureEvidence.TryGetSampling(texture, out var sampling))
    {
        return RecordUnknown<ColorSemanticValue>(
            diagnostics,
            LilToonSemanticOutput.Emission,
            LilToonSemanticDiagnosticCode.UnsupportedSampling,
            EmissionMapProperty);
    }

    if (!UnityTextureEvidence.TryGetSourceId(texture, out var sourceId))
    {
        return RecordUnknown<ColorSemanticValue>(
            diagnostics,
            LilToonSemanticOutput.Emission,
            LilToonSemanticDiagnosticCode.UnstableTextureIdentity,
            EmissionMapProperty);
    }

    if (!UnityTextureEvidence.TryGetColorInterpretation(
            texture, out var interpretation) ||
        !UnityTextureEvidence.TryProveSampledAlphaIsOne(texture))
    {
        // emissionColor.a scales the blend, so an RGBA map would scale its own
        // emission: rgb times the same sample's alpha is not representable.
        return RecordUnknown<ColorSemanticValue>(
            diagnostics,
            LilToonSemanticOutput.Emission,
            LilToonSemanticDiagnosticCode.UnsupportedTextureImport,
            EmissionMapProperty);
    }

    var sample = new TextureSample(sourceId, mapping, sampling);
    var value = tint == Vector3.one
        ? ColorSemanticValue.Texture(sample, interpretation)
        : ColorSemanticValue.TextureTimesConstant(sample, interpretation, tint);
    return SemanticOutput<ColorSemanticValue>.Complete(value);
}

/// <summary>
/// The emission map selects a UV channel and applies its own ST directly, so it
/// does not compose with the main transform. Mode 4 selects rim UV and is
/// unsupported.
/// </summary>
private static bool TryGetEmissionUvMapping(
    Material material,
    out UvMapping mapping)
{
    mapping = default;

    var rawMode = material.GetFloat(EmissionMapUvModeProperty);
    if (!IsFinite(rawMode))
    {
        return false;
    }

    // Rounding here is safe only because of the `channel != rawMode` guard,
    // which rejects every non-integral value. This is deliberately unlike the
    // _lilToonVersion check, where rounding would silently normalize a
    // malformed value into the supported one; do not copy this shape there.
    var channel = Mathf.RoundToInt(rawMode);
    if (channel < 0 || channel > 3 || channel != rawMode)
    {
        return false;
    }

    var scrollRotate = material.GetVector(EmissionMapScrollRotateProperty);
    if (!IsFinite(scrollRotate) || scrollRotate != Vector4.zero)
    {
        return false;
    }

    var scale = material.GetTextureScale(EmissionMapProperty);
    var offset = material.GetTextureOffset(EmissionMapProperty);
    if (!IsFinite(scale) || !IsFinite(offset))
    {
        return false;
    }

    mapping = new UvMapping(channel, scale, offset);
    return true;
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run `refresh_unity`, then the focused run. Expected: PASS.

- [ ] **Step 5: Scope inspection**

```bash
git status --porcelain --untracked-files=all
```

Expected delta from Task 6: `LilToonEmissionTests.cs` and its `.meta`; `LilToonMaterialSemantics.cs` modified. Do not stage and do not commit.

---

### Task 8: Output independence, full validation, and architecture checkpoint

**Files:**

- Modify: `Packages/com.alrauna.amuse/Tests/Editor/Semantics/LilToon/LilToonAdversarialTests.cs`

**Interfaces:**

- Consumes: everything from Tasks 0 to 7.
- Produces: nothing new.

- [ ] **Step 1: Write the failing independence tests**

Append to `LilToonAdversarialTests.cs`:

```csharp
        [Test]
        public void UnknownBaseColor_DoesNotInvalidateOtherOutputs()
        {
            var material = NewFixtureMaterial();
            material.SetVector("_MainTexHSVG", new Vector4(0.5f, 1f, 1f, 1f));

            var result = Interpret(material);

            Assert.That(result.IsSupportedMaterial, Is.True);
            Assert.That(result.Semantics.BaseColor.IsComplete, Is.False);
            Assert.That(result.Semantics.Alpha.IsComplete, Is.True);
            Assert.That(result.Semantics.Emission.IsComplete, Is.True);
            Assert.That(result.Semantics.Normal.IsComplete, Is.True);
        }

        [Test]
        public void UnknownEmission_DoesNotInvalidateOtherOutputs()
        {
            var material = NewFixtureMaterial();
            material.SetFloat("_UseEmission", 1f);
            material.SetFloat("_EmissionBlendMode", 3f);

            var result = Interpret(material);

            Assert.That(result.Semantics.BaseColor.IsComplete, Is.True);
            Assert.That(result.Semantics.Alpha.IsComplete, Is.True);
            Assert.That(result.Semantics.Emission.IsComplete, Is.False);
            Assert.That(result.Semantics.Normal.IsComplete, Is.True);
        }

        [Test]
        public void UnknownNormal_DoesNotInvalidateOtherOutputs()
        {
            var material = NewFixtureMaterial();
            material.SetTexture("_MainTex", ImportTexture("mainok"));
            material.SetTexture("_BumpMap", ImportTexture("badnormal"));
            material.SetFloat("_UseBumpMap", 1f);

            var result = Interpret(material);

            Assert.That(result.Semantics.BaseColor.IsComplete, Is.True);
            Assert.That(result.Semantics.Alpha.IsComplete, Is.True);
            Assert.That(result.Semantics.Emission.IsComplete, Is.True);
            Assert.That(result.Semantics.Normal.IsComplete, Is.False);
        }

        [Test]
        public void SharedGate_ReportsOneDiagnosticPerAffectedOutput()
        {
            var material = NewFixtureMaterial();
            material.SetFloat("_UseParallax", 1f);

            var result = Interpret(material);

            // _UseParallax gates BaseColor and Normal, but not Alpha or
            // Emission, and each affected output records exactly one reason.
            Assert.That(
                DiagnosticsFor(result, LilToonSemanticOutput.BaseColor).Count,
                Is.EqualTo(1));
            Assert.That(
                DiagnosticsFor(result, LilToonSemanticOutput.Alpha).Count,
                Is.EqualTo(0));
            Assert.That(
                DiagnosticsFor(result, LilToonSemanticOutput.Emission).Count,
                Is.EqualTo(0));
            Assert.That(result.Semantics.Alpha.IsComplete, Is.True);
        }

        [Test]
        public void DefaultMaterial_IsFullyComplete()
        {
            var material = NewFixtureMaterial();

            var result = Interpret(material);

            Assert.That(result.IsSupportedMaterial, Is.True);
            Assert.That(result.Diagnostics, Is.Empty);
            Assert.That(result.Semantics.BaseColor.IsComplete, Is.True);
            Assert.That(result.Semantics.Alpha.IsComplete, Is.True);
            Assert.That(result.Semantics.Emission.IsComplete, Is.True);
            Assert.That(result.Semantics.Normal.IsComplete, Is.True);
        }

        [Test]
        public void Diagnostics_AreImmutable()
        {
            var material = NewFixtureMaterial();
            material.SetFloat("_UseParallax", 1f);

            var result = Interpret(material);

            Assert.That(
                result.Diagnostics,
                Is.InstanceOf<System.Collections.ObjectModel.ReadOnlyCollection<
                    LilToonSemanticDiagnostic>>());
        }
```

`SharedGate_ReportsOneDiagnosticPerAffectedOutput` asserts that `_UseParallax` does **not** gate Emission. If Task 7's implementation gates it, either add it to that assertion with a source citation or remove it from the emission gate list — do not silently loosen the test.

- [ ] **Step 2: Run tests to verify they fail**

Run `refresh_unity`, then:

```text
test_names: Alrauna.Amuse.Tests.Editor.Semantics.LilToon.LilToonAdversarialTests
```

Expected: FAIL only where a gate list needs correction. If all pass immediately, that is acceptable — these are contract tests over already-implemented behaviour — but confirm by temporarily breaking one gate and observing the failure before reverting.

- [ ] **Step 3: Fix any gate-list discrepancy**

Correct the implementation, not the test, unless the traced source disagrees with the test. Record the source file and line for any gate list you change; it goes in the completion report.

- [ ] **Step 4: Run the complete EditMode suite**

Run `refresh_unity`, then every EditMode test with no filter.

Record total, passed, failed, skipped, duration, and any Console errors. Expected: zero failures, zero skips, and every pre-existing classifier, geometry, planner, fixture, semantics, resolver, and Poiyomi test unchanged and green.

- [ ] **Step 5: Architecture-comparison checkpoint**

Verify each item, and stop for review rather than fixing in place if any fails:

- `git diff -- Packages/com.alrauna.amuse/Editor/Semantics/MaterialSemantics.cs` reports no change.
- `git diff -- Packages/com.alrauna.amuse/Editor/Analysis/` reports no change.
- The Poiyomi diff contains only the five delegations plus the strictly dead privates. No behaviour, gate list, constant, or diagnostic changed.
- No Poiyomi test file was modified.
- `UnityTextureEvidence.cs` exposes exactly five methods, and contains no shader property name, no `LIL_` or `_Poi` string, no NDMF type, no diagnostic type, and no `Material` parameter.
- No production file contains a lilToon variant-class enum or a multi-entry lilToon shader table. The researched taxonomy stays in the design document.
- Every safety predicate is a positive allow-list. Grep the lilToon frontend for `!` applied to a "looks unsafe" helper and confirm none exists.
- No file named or containing `IShaderAdapter`, `ShaderAdapterRegistry`, `ShaderProfile`, `ShaderSchema`, or an equivalent generic abstraction exists.
- No `.yaml`, `.json`, or other serialized shader-definition file was added.
- `Packages/manifest.json`, `Packages/packages-lock.json`, `Packages/vpm-manifest.json`, and `Packages/com.alrauna.amuse/package.json` are unchanged.
- Every new `.cs` and `.shader` file has a `.meta`, and no pre-existing `.meta` changed.

Then re-read the design spec's "Pressure on the semantic core" and "Stop-condition findings" tables against what was actually built, and record any divergence in the completion report.

- [ ] **Step 6: Working-tree scope verification**

```bash
git status --porcelain --untracked-files=all
```

Confirm the changed set is exactly: one modified Poiyomi file, three new Editor files plus `.meta`, nine new test artifacts plus `.meta` — `Tests/Editor/Semantics/UnityTextureEvidenceTests.cs` plus the eight files under `Tests/Editor/Semantics/LilToon/`, including `LilToonSemanticTest.shader` — two new folder `.meta` entries, and the two documentation files from the planning turn. Nothing under `Library/`, `Temp/`, `Logs/`, or `UserSettings/`. Nothing is staged.

- [ ] **Step 7: Scope inspection**

```bash
git status --porcelain --untracked-files=all
```

Expected delta from Task 7: `LilToonAdversarialTests.cs` modified. Do not stage and do not commit.

Implementation is now complete and entirely unstaged. **Stop here and report.** Task 9 runs only after the implementation-review gate authorizes it.

---

### Task 9: Git finalization gate (RUNS ONLY AFTER SEPARATE AUTHORIZATION)

This task does not commit either. It produces the exact cached diff a reviewer needs, then restores the unstaged state. Do not begin it on your own initiative.

**Files:** none changed.

- [ ] **Step 1: Confirm the intended file set**

```bash
git status --porcelain --untracked-files=all
```

Write down every path. The intended set is exactly:

- modified: `Packages/com.alrauna.amuse/Editor/Semantics/Poiyomi/PoiyomiMaterialSemantics.cs`
- new: `Editor/Semantics/UnityTextureEvidence.cs`, `Editor/Semantics/LilToon/LilToonSourceAttestation.cs`, `Editor/Semantics/LilToon/LilToonMaterialSemantics.cs`
- new: `Tests/Editor/Semantics/UnityTextureEvidenceTests.cs` and the eight `Tests/Editor/Semantics/LilToon/` files including `LilToonSemanticTest.shader`
- new: the `.meta` sibling of every new file, plus the `.meta` for the two new folders
- new: the two documentation files

Anything else is out of scope. Stop and report rather than staging it.

- [ ] **Step 2: Temporarily stage exactly those paths**

Stage the enumerated paths explicitly. Do not use `git add -A`, `git add .`, or a bare directory add that could sweep in an unlisted file.

- [ ] **Step 3: Inspect the cached diff**

```bash
git diff --cached --check
```

```bash
git diff --cached --stat
```

```bash
git diff --cached
```

Read the complete cached diff, not just the stat. Confirm: no whitespace errors from `--check`; the Poiyomi diff is delegation-only; no `Library/`, `Temp/`, `Logs/`, `UserSettings/`, or manifest path appears; and every `.meta` is a newly added file whose content is Unity-generated. Repository-standard Unity-generated `.meta` whitespace is the only acceptable formatting difference, and it may appear only on new files.

- [ ] **Step 4: Unstage everything**

```bash
git reset
```

- [ ] **Step 5: Confirm nothing remains staged or committed**

```bash
git status --porcelain --untracked-files=all
```

```bash
git log --oneline -1
```

Expected: the same unstaged working set as Step 1, and `HEAD` still at `b53bb17`. Do not push and do not open a PR.

---

## Completion report

Report, per the project completion standard:

- what changed, file by file;
- which tests ran, focused and full, with observed totals and failures;
- which validation was skipped and why;
- remaining risks and unsupported cases, especially every lilToon variant other than the supported target, non-BRP pipelines, and the analysis-timing risk against lilToon's build hook;
- whether the private Unity MCP testbed was used, and confirmation that it was not modified;
- the architecture-checkpoint results from Task 8 Step 5;
- the observed digests from Task 0 and whether the pins are now verified rather than provisional;
- confirmation that `HEAD` is unchanged and nothing is staged.

Do not claim completion from a successful compile or a subagent report. Evidence precedes the claim.
