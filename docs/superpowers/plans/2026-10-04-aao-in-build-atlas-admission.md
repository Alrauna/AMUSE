# In-build AAO Atlas Admission Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Admit the in-memory atlas textures of Avatar Optimizer (AAO) 1.9.17 Optimize Texture into the alpha evidence capture under a version-pinned, corroboration-gated, fail-closed conjunct, so AAO-processed lilToon slots become analyzable.

**Architecture:** One new admission class (`AaoAtlasTextureAttestation`) with a package-version seam and a shipped admitted set, one new identity mint (`AaoAtlasIdentity`), one AAO arm in the identity gate, one shape arm in the route selector, and the slot's live material threaded from the batch capture to the identity gate as producer corroboration. Every change site carries a code comment naming the upstream AAO contribution that would let AMUSE delete it.

**Tech Stack:** Unity 2022.3.22f1, C# editor-only assembly `Alrauna.Amuse.Editor`, NDMF 1.14.4 public APIs (`ObjectRegistry`, `SubAssetContainer`), NUnit through the Unity Test Framework, EditMode only.

**Spec:** `docs/superpowers/specs/2026-10-04-aao-in-build-atlas-admission-design.md`. The plan argues from the spec; executors read both. Investigation record: `docs/superpowers/investigations/2026-10-04-aao-optimize-texture-atlas-refusal-investigation.md`.

## Global Constraints

- Tests never install AAO and never read the installed environment. Every test substitutes `AaoAtlasTextureAttestation.ReadInstalledPackageVersionOrNull` and pins the admitted set through `SetAdmittedVersionsForTests`. The dev project installs AAO 1.9.17, so an unsubstituted read would silently pass there and fail elsewhere.
- Registry fixtures follow the shipped pattern: save `ObjectRegistry.ActiveRegistry`, set `new ObjectRegistry(null)`, call `ObjectRegistry.RegisterReplacedObject(source, clone)`, restore in `finally`.
- RED before GREEN. Record the failing test names and failure messages before each production edit. A compile failure from a new type is the RED observation for that step. An admission test that passes on first run is a characterization, never a dressed-up RED.
- Never weaken, delete, or skip an existing test. `LooseInMemoryTexture_IsRefused` and `TryGetSourceId_SceneOnlyTexture_IsRefused` stay green and unchanged.
- Stand-in shaders only: `Hidden/Alrauna/AmuseTests/Opaque` and the `Hidden/Alrauna/AmuseTests/LilToon*` family. Vendor shaders are never installed.
- Constraint-model assertions only: `Assert.That(actual, Is.EqualTo(expected))`.
- One production assembly. Test classes mirror production types and folders. File name equals type name.
- Every change site in Tasks 1 to 4 carries the upstream-contribution comment required by spec section 8. The comment names what an upstream AAO release that registers its atlases in the NDMF object registry, or persists them into a build container, would let AMUSE delete.
- No private identifiers, no machine paths, no instance names, no ports in any file.
- Test runner mechanics: after creating or editing files, call `refresh_unity` and wait for compilation, check `read_console` for 0 compile errors, then run the EditMode filter named in the step and poll `get_test_job`. A filtered run that reports 0 tests is a failure. Record observed counts.
- Commit only the files a task names. No staging of unrelated work.

---

### Task 0: Land the design records

**Files:**
- Commit: `docs/superpowers/investigations/2026-10-04-aao-optimize-texture-atlas-refusal-investigation.md`
- Commit: `docs/superpowers/specs/2026-10-04-aao-in-build-atlas-admission-design.md`
- Commit: `docs/superpowers/plans/2026-10-04-aao-in-build-atlas-admission.md`

- [ ] **Step 1: Commit the three documents**

```bash
git add docs/superpowers/investigations/2026-10-04-aao-optimize-texture-atlas-refusal-investigation.md \
  docs/superpowers/specs/2026-10-04-aao-in-build-atlas-admission-design.md \
  docs/superpowers/plans/2026-10-04-aao-in-build-atlas-admission.md
git commit -m "docs: design spec and plan for in-build AAO atlas admission"
```

---

### Task 1: The AAO atlas attestation class and version seam

**Files:**
- Create: `Packages/com.alrauna.amuse/Editor/Semantics/PackageVersionReader.cs`
- Create: `Packages/com.alrauna.amuse/Editor/Semantics/AaoAtlasTextureAttestation.cs`
- Modify: `Packages/com.alrauna.amuse/Editor/Semantics/ReplacementTextureAttestation.cs` (delegate the offline reader to the shared helper)
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Semantics/AaoAtlasTextureAttestationTests.cs`

**Interfaces:**
- Consumes: `RegisteredSourceIdentity.Resolve(UnityEngine.Object)` from `Semantics/RegisteredSourceIdentity.cs`.
- Produces (later tasks rely on these exact names):
  - `AaoAtlasTextureAttestation.ProducerPackageName`, `AtlasNameSuffix`, `MonotoneNamePrefix`
  - `AaoAtlasTextureAttestation.ReadInstalledPackageVersionOrNull` (`Func<string, string>`), `AdmittedVersions`, `SetAdmittedVersionsForTests(params string[])`, `ResetForTests()`, `TryReadInstalledProducerVersion(out string)`, `ClearVersionCacheForSession()`, `IsVersionAdmitted(string)`
  - `AaoAtlasTextureAttestation.TryIdentifyAtlasShape(Texture2D)` — conjuncts C1 to C3
  - `AaoAtlasTextureAttestation.TryIdentifyAtlas(Texture2D, Material)` — conjuncts C1 to C4
  - `PackageVersionReader.ReadInstalledOrNull(string)` — offline `Client.List` read

- [ ] **Step 1: Write the failing tests**

Create `Packages/com.alrauna.amuse/Tests/Editor/Semantics/AaoAtlasTextureAttestationTests.cs`:

```csharp
using System.IO;
using nadena.dev.ndmf;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Alrauna.Amuse.Editor.Semantics;

namespace Alrauna.Amuse.Tests.Editor.Semantics
{
    public sealed class AaoAtlasTextureAttestationTests
    {
        private const string FixtureFolder = "Assets/AmuseTests_AaoAttestation";

        private nadena.dev.ndmf.IObjectRegistry PreviousRegistry { get; set; }

        [SetUp]
        public void ResetAttestationStateBeforeEachTest()
        {
            AaoAtlasTextureAttestation.ResetForTests();
            PreviousRegistry = ObjectRegistry.ActiveRegistry;
            ObjectRegistry.ActiveRegistry = new ObjectRegistry(null);
        }

        [TearDown]
        public void RestoreRegistryAndCleanFixtureFolder()
        {
            ObjectRegistry.ActiveRegistry = PreviousRegistry;
            if (AssetDatabase.IsValidFolder(FixtureFolder))
            {
                AssetDatabase.DeleteAsset(FixtureFolder);
            }
        }

        private static void InstallAdmittedAaoVersion()
        {
            AaoAtlasTextureAttestation.ResetForTests();
            AaoAtlasTextureAttestation.ReadInstalledPackageVersionOrNull =
                _ => "1.9.17";
        }

        private static Texture2D NewAtlas(string name)
        {
            var atlas = new Texture2D(16, 16, TextureFormat.RGBA32, false);
            atlas.name = name;
            return atlas;
        }

        /// <summary>
        /// Creates an asset-backed material, the shape the corroboration
        /// conjunct demands of the registered origin material's source.
        /// </summary>
        private Material NewAssetMaterial(string marker)
        {
            if (!AssetDatabase.IsValidFolder(FixtureFolder))
            {
                AssetDatabase.CreateFolder(
                    "Assets", Path.GetFileName(FixtureFolder));
            }
            var material = new Material(
                Shader.Find("Hidden/Alrauna/AmuseTests/Opaque"));
            AssetDatabase.CreateAsset(
                material, $"{FixtureFolder}/{marker}.mat");
            return material;
        }

        [Test]
        public void ShippedAdmittedSetContainsOnlyTheCharacterizedVersion()
        {
            Assert.That(
                AaoAtlasTextureAttestation.AdmittedVersions,
                Is.EqualTo(new[] { "1.9.17" }),
                "1.9.17 joined on 2026-10-04 through the dated " +
                "characterization; any other version stays refused");
            Assert.That(
                AaoAtlasTextureAttestation.IsVersionAdmitted("1.9.17"),
                Is.True);
            Assert.That(
                AaoAtlasTextureAttestation.IsVersionAdmitted("1.9.18"),
                Is.False);
        }

        [Test]
        public void AtlasShapeRequiresThePinnedNameMarkers()
        {
            InstallAdmittedAaoVersion();
            var wrongName = NewAtlas("MainTex UV Packed");
            try
            {
                Assert.That(
                    AaoAtlasTextureAttestation.TryIdentifyAtlasShape(wrongName),
                    Is.False,
                    "a path-less texture without a pinned marker is not an " +
                    "AAO atlas, whatever its version");
            }
            finally
            {
                Object.DestroyImmediate(wrongName);
            }
        }

        [Test]
        public void AtlasShapeRefusesAnAssetBackedTexture()
        {
            InstallAdmittedAaoVersion();
            if (!AssetDatabase.IsValidFolder(FixtureFolder))
            {
                AssetDatabase.CreateFolder(
                    "Assets", Path.GetFileName(FixtureFolder));
            }
            var assetBacked = new Texture2D(
                4, 4, TextureFormat.RGBA32, false);
            assetBacked.name = "MainTex (AAO UV Packed)";
            AssetDatabase.CreateAsset(
                assetBacked, $"{FixtureFolder}/atlas-asset.asset");
            AssetDatabase.SaveAssets();

            Assert.That(
                AaoAtlasTextureAttestation.TryIdentifyAtlasShape(assetBacked),
                Is.False,
                "an asset-backed texture reads through the asset rules, " +
                "never through the in-memory admission");
        }

        [Test]
        public void AtlasShapeRefusesAnUnadmittedVersion()
        {
            AaoAtlasTextureAttestation.ResetForTests();
            AaoAtlasTextureAttestation.ReadInstalledPackageVersionOrNull =
                _ => "1.9.18";
            var atlas = NewAtlas("MainTex (AAO UV Packed)");
            try
            {
                Assert.That(
                    AaoAtlasTextureAttestation.TryIdentifyAtlasShape(atlas),
                    Is.False,
                    "an uncharacterized producer version keeps refusing");
            }
            finally
            {
                Object.DestroyImmediate(atlas);
            }
        }

        [Test]
        public void CorroborationAcceptsARegisteredAssetBackedOriginMaterial()
        {
            InstallAdmittedAaoVersion();
            var source = NewAssetMaterial("origin-source");
            var clone = new Material(source.shader)
            {
                name = source.name + " build copy",
            };
            var atlas = NewAtlas("MainTex (AAO UV Packed)");
            try
            {
                ObjectRegistry.RegisterReplacedObject(source, clone);

                Assert.That(
                    AaoAtlasTextureAttestation.TryIdentifyAtlas(atlas, clone),
                    Is.True,
                    "a one-hop registry pair to an asset-backed material " +
                    "corroborates the producer");
            }
            finally
            {
                Object.DestroyImmediate(atlas);
                Object.DestroyImmediate(clone);
            }
        }

        [Test]
        public void CorroborationRefusesAnUnregisteredOriginMaterial()
        {
            InstallAdmittedAaoVersion();
            var clone = new Material(
                Shader.Find("Hidden/Alrauna/AmuseTests/Opaque"));
            var atlas = NewAtlas("MainTex (AAO UV Packed)");
            try
            {
                Assert.That(
                    AaoAtlasTextureAttestation.TryIdentifyAtlas(atlas, clone),
                    Is.False,
                    "an unregistered slot material corroborates nothing");
            }
            finally
            {
                Object.DestroyImmediate(atlas);
                Object.DestroyImmediate(clone);
            }
        }

        [Test]
        public void CorroborationRefusesAMemoryOnlySourceMaterial()
        {
            InstallAdmittedAaoVersion();
            var memorySource = new Material(
                Shader.Find("Hidden/Alrauna/AmuseTests/Opaque"));
            var clone = new Material(memorySource.shader);
            var atlas = NewAtlas("MainTex (AAO UV Packed)");
            try
            {
                ObjectRegistry.RegisterReplacedObject(memorySource, clone);

                Assert.That(
                    AaoAtlasTextureAttestation.TryIdentifyAtlas(atlas, clone),
                    Is.False,
                    "the resolved source material must be asset-backed, " +
                    "exactly as the replacement conjunct demands of textures");
            }
            finally
            {
                Object.DestroyImmediate(atlas);
                Object.DestroyImmediate(clone);
                Object.DestroyImmediate(memorySource);
            }
        }

        [Test]
        public void CorroborationRefusesANullOriginMaterial()
        {
            InstallAdmittedAaoVersion();
            var atlas = NewAtlas("MainTex (AAO UV Packed)");
            try
            {
                Assert.That(
                    AaoAtlasTextureAttestation.TryIdentifyAtlas(atlas, null),
                    Is.False,
                    "the shape alone never admits; corroboration is a " +
                    "conjunct, not an option");
            }
            finally
            {
                Object.DestroyImmediate(atlas);
            }
        }
    }
}
```

- [ ] **Step 2: Refresh Unity and run the filter. Expected: FAIL (compile error, `AaoAtlasTextureAttestation` does not exist). Record it.**

- [ ] **Step 3: Create the shared offline version reader**

Create `Packages/com.alrauna.amuse/Editor/Semantics/PackageVersionReader.cs`:

```csharp
using UnityEditor.PackageManager;

namespace Alrauna.Amuse.Editor.Semantics
{
    /// <summary>
    /// Reads an installed package version offline. One shared reader
    /// serves every producer-version seam, so each admission states its
    /// policy and none repeats the blocking mechanics.
    /// </summary>
    internal static class PackageVersionReader
    {
        internal static string ReadInstalledOrNull(string packageName)
        {
            // Offline listing never touches the network, so blocking
            // inside an editor build pass is safe. Unity 2022.3 has no
            // Request.WaitForCompletion, so block on IsCompleted; the
            // wait is bounded so an unresponsive package manager
            // refuses rather than wedging the editor.
            var request = Client.List(true);
            var spins = 0;
            while (!request.IsCompleted && spins < 30000)
            {
                spins++;
                System.Threading.Thread.Sleep(1);
            }
            if (request.Status != StatusCode.Success || request.Result == null)
            {
                return null;
            }
            foreach (var package in request.Result)
            {
                if (package.name == packageName)
                {
                    return package.version;
                }
            }
            return null;
        }
    }
}
```

In `Packages/com.alrauna.amuse/Editor/Semantics/ReplacementTextureAttestation.cs`, delete the private method `ReadVersionFromPackageManager` (lines 133-159 on 2026-10-04) and change the seam default (line 47) to:

```csharp
        internal static Func<string, string> ReadInstalledPackageVersionOrNull
        {
            get;
            set;
        } = PackageVersionReader.ReadInstalledOrNull;
```

Also update the two references to `ReadVersionFromPackageManager` inside `ResetForTests` (line 68) to `PackageVersionReader.ReadInstalledOrNull`.

- [ ] **Step 4: Create the attestation class**

Create `Packages/com.alrauna.amuse/Editor/Semantics/AaoAtlasTextureAttestation.cs`:

```csharp
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Alrauna.Amuse.Editor.Semantics
{
    /// <summary>
    /// Attests the in-memory atlas shape of the Optimize Texture pass
    /// of Avatar Optimizer. Every conjunct must hold; any miss refuses.
    /// </summary>
    // Upstream path (spec 2026-10-04, section 8). A future Avatar
    // Optimizer release that improves the attestability of its
    // generated textures, for example by registering each atlas in the
    // NDMF object registry or by persisting atlases into a build
    // container, would let AMUSE delete this admission almost
    // entirely: the shape predicate, the corroboration conjunct, and
    // the version seam would all retire together with the identity
    // mint and the two arms that consult them. The persisted-container
    // branch in GeneratedTextureAttestation already reads that shape.
    // Until then, this comment is the whole upstream ask.
    internal static class AaoAtlasTextureAttestation
    {
        internal const string ProducerPackageName =
            "com.anatawa12.avatar-optimizer";
        internal const string AtlasNameSuffix = " (AAO UV Packed)";
        internal const string MonotoneNamePrefix = "AAO Monotone ";

        // The admitted set is pinned, characterized data, never grown
        // at runtime. 1.9.17 joined on 2026-10-04: the installed dev
        // and Census Lab copies' source shows the shape (atlases named
        // source plus " (AAO UV Packed)" or "AAO Monotone ...", plain
        // in-memory Texture2D objects, never saved, never registered,
        // format and flags mirrored from the source), and Lab builds
        // exercised the refusal end to end before admission
        // (investigations 2026-09-27 and 2026-10-04).
        private static readonly string[] ProductionAdmittedVersions =
        {
            "1.9.17",
        };

        private static readonly List<string> Admitted =
            new List<string>(ProductionAdmittedVersions);

        internal static Func<string, string> ReadInstalledPackageVersionOrNull
        {
            get;
            set;
        } = PackageVersionReader.ReadInstalledOrNull;

        internal static IReadOnlyList<string> AdmittedVersions => Admitted;

        internal static void SetAdmittedVersionsForTests(
            params string[] versions)
        {
            Admitted.Clear();
            if (versions != null)
            {
                Admitted.AddRange(versions);
            }
        }

        private static string CachedVersion;
        private static bool VersionReadCompleted;

        internal static void ResetForTests()
        {
            Admitted.Clear();
            Admitted.AddRange(ProductionAdmittedVersions);
            ReadInstalledPackageVersionOrNull =
                PackageVersionReader.ReadInstalledOrNull;
            ClearVersionCacheForSession();
        }

        internal static bool TryReadInstalledProducerVersion(
            out string version)
        {
            if (!VersionReadCompleted)
            {
                CachedVersion = ReadInstalledPackageVersionOrNull?.Invoke(
                    ProducerPackageName);
                VersionReadCompleted = true;
            }
            version = CachedVersion;
            return !string.IsNullOrEmpty(version);
        }

        internal static void ClearVersionCacheForSession()
        {
            VersionReadCompleted = false;
            CachedVersion = null;
        }

        internal static bool IsVersionAdmitted(string version)
        {
            return !string.IsNullOrEmpty(version) && Admitted.Contains(version);
        }

        /// <summary>
        /// The shape predicate, conjuncts C1 to C3 of spec 2026-10-04
        /// section 4.1. It is deliberately texture-only: the route
        /// selector and the fact readers consult it, while the identity
        /// gate demands the corroborated form below.
        /// </summary>
        internal static bool TryIdentifyAtlasShape(Texture2D texture)
        {
            if (texture == null)
            {
                return false;
            }
            // Conjunct 1: a live copy with no asset path.
            if (!string.IsNullOrEmpty(AssetDatabase.GetAssetPath(texture)))
            {
                return false;
            }
            // Conjunct 2: the pinned name shape.
            var name = texture.name ?? string.Empty;
            var hasMarker =
                name.EndsWith(AtlasNameSuffix, StringComparison.Ordinal) ||
                name.StartsWith(MonotoneNamePrefix, StringComparison.Ordinal);
            if (!hasMarker)
            {
                return false;
            }
            // Conjunct 3: the producer is installed and its version is
            // admitted.
            return TryReadInstalledProducerVersion(out var version) &&
                   IsVersionAdmitted(version);
        }

        /// <summary>
        /// The full admission, conjuncts C1 to C4 of spec 2026-10-04
        /// section 4.1. The slot's live material must corroborate the
        /// producer: the registry resolves it one hop to an asset-backed
        /// material, the pair the DuplicateAssets pass of Avatar
        /// Optimizer registers for every renderer material it clones.
        /// </summary>
        internal static bool TryIdentifyAtlas(
            Texture2D texture,
            Material originMaterial)
        {
            if (!TryIdentifyAtlasShape(texture))
            {
                return false;
            }
            // Conjunct 4: the origin material corroborates the producer.
            if (originMaterial == null)
            {
                return false;
            }
            if (!(RegisteredSourceIdentity.Resolve(originMaterial)
                    is Material resolved))
            {
                return false;
            }
            return !string.IsNullOrEmpty(
                AssetDatabase.GetAssetPath(resolved));
        }
    }
}
```

- [ ] **Step 5: Refresh Unity, check the console for 0 compile errors, run the filters `AaoAtlasTextureAttestationTests` and `ReplacementTextureAttestationTests`. Expected: PASS. Record the counts.**

- [ ] **Step 6: Commit**

```bash
git add Packages/com.alrauna.amuse/Editor/Semantics/PackageVersionReader.cs \
  Packages/com.alrauna.amuse/Editor/Semantics/AaoAtlasTextureAttestation.cs \
  Packages/com.alrauna.amuse/Editor/Semantics/ReplacementTextureAttestation.cs \
  Packages/com.alrauna.amuse/Tests/Editor/Semantics/AaoAtlasTextureAttestationTests.cs
git commit -m "feat: add the in-build AAO atlas attestation seam"
```

---

### Task 2: The identity mint and the identity-gate arm

**Files:**
- Create: `Packages/com.alrauna.amuse/Editor/Semantics/AaoAtlasIdentity.cs`
- Modify: `Packages/com.alrauna.amuse/Editor/Semantics/UnityTextureEvidence.cs:29-48` (signature plus the AAO arm)
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Semantics/UnityTextureEvidenceTests.cs`

**Interfaces:**
- Consumes: `AaoAtlasTextureAttestation.TryIdentifyAtlas(Texture2D, Material)` from Task 1.
- Produces: `AaoAtlasIdentity.TryMint(Texture2D atlas, out TextureSourceId id)`; `UnityTextureEvidence.TryGetSourceId(Texture, out TextureSourceId, Material originMaterial = null)`.

- [ ] **Step 1: Write the failing tests**

Add to `Packages/com.alrauna.amuse/Tests/Editor/Semantics/UnityTextureEvidenceTests.cs`, inside the existing fixture class (it already has the registry save-and-restore pattern and asset-texture helpers used by `AdmittedCopyCarriesTheReplacementFormThroughTryGetSourceId` at line 150):

```csharp
        [Test]
        public void AdmittedAtlasWithCorroboratedOrigin_MintsTheAtlasIdentity()
        {
            var previousRegistry = ObjectRegistry.ActiveRegistry;
            ObjectRegistry.ActiveRegistry = new ObjectRegistry(null);
            var source = ImportFixtureMaterialAsset("atlas-origin");
            var clone = new Material(source.shader)
            {
                name = source.name + " build copy",
            };
            var atlas = new Texture2D(16, 16, TextureFormat.RGBA32, false);
            atlas.name = "MainTex (AAO UV Packed)";
            try
            {
                ObjectRegistry.RegisterReplacedObject(source, clone);
                AaoAtlasTextureAttestation.ResetForTests();
                AaoAtlasTextureAttestation.ReadInstalledPackageVersionOrNull =
                    _ => "1.9.17";

                var resolved = UnityTextureEvidence.TryGetSourceId(
                    atlas, out var id, originMaterial: clone);

                Assert.That(resolved, Is.True);
                Assert.That(
                    id.Value.StartsWith(
                        "unity-aao-atlas:", StringComparison.Ordinal),
                    Is.True,
                    "an admitted atlas mints the per-build atlas identity");
            }
            finally
            {
                Object.DestroyImmediate(atlas);
                Object.DestroyImmediate(clone);
                AaoAtlasTextureAttestation.ResetForTests();
                ObjectRegistry.ActiveRegistry = previousRegistry;
            }
        }

        [Test]
        public void AdmittedAtlasWithoutOriginMaterial_IsRefused()
        {
            var atlas = new Texture2D(16, 16, TextureFormat.RGBA32, false);
            atlas.name = "MainTex (AAO UV Packed)";
            try
            {
                AaoAtlasTextureAttestation.ResetForTests();
                AaoAtlasTextureAttestation.ReadInstalledPackageVersionOrNull =
                    _ => "1.9.17";

                Assert.That(
                    UnityTextureEvidence.TryGetSourceId(
                        atlas, out _, originMaterial: null),
                    Is.False,
                    "the shape alone never mints an identity");
            }
            finally
            {
                Object.DestroyImmediate(atlas);
                AaoAtlasTextureAttestation.ResetForTests();
            }
        }

        [Test]
        public void AdmittedAtlasWithUnregisteredOrigin_IsRefused()
        {
            var clone = new Material(
                Shader.Find("Hidden/Alrauna/AmuseTests/Opaque"));
            var atlas = new Texture2D(16, 16, TextureFormat.RGBA32, false);
            atlas.name = "MainTex (AAO UV Packed)";
            try
            {
                AaoAtlasTextureAttestation.ResetForTests();
                AaoAtlasTextureAttestation.ReadInstalledPackageVersionOrNull =
                    _ => "1.9.17";

                Assert.That(
                    UnityTextureEvidence.TryGetSourceId(
                        atlas, out _, originMaterial: clone),
                    Is.False,
                    "an unregistered slot material corroborates nothing");
            }
            finally
            {
                Object.DestroyImmediate(atlas);
                Object.DestroyImmediate(clone);
                AaoAtlasTextureAttestation.ResetForTests();
            }
        }
```

If the fixture class has no `ImportFixtureMaterialAsset` helper, add one beside the existing texture-import helper, following the same folder and cleanup pattern:

```csharp
        private static Material ImportFixtureMaterialAsset(string marker)
        {
            if (!AssetDatabase.IsValidFolder(FixtureFolder))
            {
                AssetDatabase.CreateFolder(
                    "Assets", Path.GetFileName(FixtureFolder));
            }
            var material = new Material(
                Shader.Find("Hidden/Alrauna/AmuseTests/Opaque"));
            AssetDatabase.CreateAsset(
                material, $"{FixtureFolder}/{marker}.mat");
            return material;
        }
```

Use the fixture folder constant and teardown that the class already uses, and delete nothing.

- [ ] **Step 2: Refresh Unity and run the filter `UnityTextureEvidenceTests`. Expected: FAIL. First a compile error (no three-parameter `TryGetSourceId`), then, once the overload exists without the arm, the first test fails on `resolved`. Record both observations.**

- [ ] **Step 3: Create the identity mint**

Create `Packages/com.alrauna.amuse/Editor/Semantics/AaoAtlasIdentity.cs`:

```csharp
using System.Globalization;
using UnityEngine;

namespace Alrauna.Amuse.Editor.Semantics
{
    /// <summary>
    /// Mints the in-build identity of an admitted Avatar Optimizer
    /// atlas. The value derives from the live object's own instance id,
    /// so distinct objects cannot collide and one object mints one id
    /// for the whole build. No claim table and no poison table are
    /// needed, unlike the source-keyed replacement identity. The id is
    /// per-build by design; it never names a project asset and needs
    /// no session clear.
    /// </summary>
    // Upstream path (spec 2026-10-04, section 8): an atlas that Avatar
    // Optimizer registers in the NDMF object registry, or persists into
    // a build container, reads through an existing identity form and
    // retires this mint together with the admission.
    internal static class AaoAtlasIdentity
    {
        private const string Prefix = "unity-aao-atlas:";

        internal static bool TryMint(
            Texture2D atlas, out TextureSourceId id)
        {
            id = default;
            if (atlas == null)
            {
                return false;
            }
            id = new TextureSourceId(
                Prefix + atlas.GetInstanceID()
                    .ToString(CultureInfo.InvariantCulture));
            return true;
        }
    }
}
```

- [ ] **Step 4: Add the optional parameter and the arm**

In `Packages/com.alrauna.amuse/Editor/Semantics/UnityTextureEvidence.cs`, change the signature at line 29 to:

```csharp
        internal static bool TryGetSourceId(
            Texture texture,
            out TextureSourceId sourceId,
            Material originMaterial = null)
```

and extend the path-less branch (lines 39-49) so it reads:

```csharp
            if (string.IsNullOrEmpty(AssetDatabase.GetAssetPath(texture)))
            {
                if (texture is Texture2D copy &&
                    ReplacementTextureAttestation.TryIdentifyReplacement(
                        copy, out var replacementSource))
                {
                    return ReplacementTextureIdentity.TryMint(
                        replacementSource, copy.GetInstanceID(), out sourceId);
                }
                // The in-memory Avatar Optimizer atlas admits only with a
                // corroborated origin material (spec 2026-10-04, section
                // 4.1). The route-level shape predicate stays weaker on
                // purpose; this gate is the stricter one every capture
                // passes first.
                // Upstream path (spec 2026-10-04, section 8): an atlas
                // that Avatar Optimizer registers in the NDMF object
                // registry, or persists into a build container, retires
                // this arm and the corroboration threading with it.
                if (texture is Texture2D atlas &&
                    AaoAtlasTextureAttestation.TryIdentifyAtlas(
                        atlas, originMaterial))
                {
                    return AaoAtlasIdentity.TryMint(atlas, out sourceId);
                }
                return false;
            }
```

- [ ] **Step 5: Refresh Unity, check the console for 0 compile errors, run the filter `UnityTextureEvidenceTests`. Expected: PASS, including the unchanged pins `TryGetSourceId_SceneOnlyTexture_IsRefused` and `AdmittedCopyCarriesTheReplacementFormThroughTryGetSourceId`. Record the counts.**

- [ ] **Step 6: Commit**

```bash
git add Packages/com.alrauna.amuse/Editor/Semantics/AaoAtlasIdentity.cs \
  Packages/com.alrauna.amuse/Editor/Semantics/UnityTextureEvidence.cs \
  Packages/com.alrauna.amuse/Tests/Editor/Semantics/UnityTextureEvidenceTests.cs
git commit -m "feat: admit corroborated in-build AAO atlases into the identity gate"
```

---

### Task 3: The route arm and the shared name constants

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/Semantics/GeneratedTextureAttestation.cs:64-73` (constants) and `:115-136` (route arm)
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Semantics/GeneratedTextureAttestationTests.cs`
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Host/UnityGeneratedTextureEvidenceTests.cs`

**Interfaces:**
- Consumes: `AaoAtlasTextureAttestation.AtlasNameSuffix`, `MonotoneNamePrefix`, `TryIdentifyAtlasShape(Texture2D)` from Task 1.
- Produces: `GeneratedTextureAttestation.TryIdentifyRouteTexture` admits a shape-admitted path-less atlas as `GeneratedTextureProducer.Anatawa12AvatarOptimizer`.

- [ ] **Step 1: Write the failing tests**

Add to `Packages/com.alrauna.amuse/Tests/Editor/Semantics/GeneratedTextureAttestationTests.cs` (the class already substitutes the LAC seam in `SetUp`/`TearDown`; call `AaoAtlasTextureAttestation.ResetForTests()` in both so the AAO seam cannot leak between tests):

```csharp
        [Test]
        public void LooseAtlasWithAdmittedVersion_TakesTheGeneratedRoute()
        {
            AaoAtlasTextureAttestation.ResetForTests();
            AaoAtlasTextureAttestation.ReadInstalledPackageVersionOrNull =
                _ => "1.9.17";
            var loose = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            loose.name = "MainTex (AAO UV Packed)";
            try
            {
                var routed = GeneratedTextureAttestation.TryIdentifyRouteTexture(
                    loose, out var producer);

                Assert.That(routed, Is.True);
                Assert.That(
                    producer,
                    Is.EqualTo(GeneratedTextureProducer.Anatawa12AvatarOptimizer));
            }
            finally
            {
                Object.DestroyImmediate(loose);
                AaoAtlasTextureAttestation.ResetForTests();
            }
        }

        [Test]
        public void LooseAtlasWithUnadmittedVersion_RefusesTheRoute()
        {
            AaoAtlasTextureAttestation.ResetForTests();
            AaoAtlasTextureAttestation.ReadInstalledPackageVersionOrNull =
                _ => "1.9.18";
            var loose = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            loose.name = "MainTex (AAO UV Packed)";
            try
            {
                var routed = GeneratedTextureAttestation.TryIdentifyRouteTexture(
                    loose, out var producer);

                Assert.That(routed, Is.False);
                Assert.That(producer, Is.EqualTo(GeneratedTextureProducer.None));
            }
            finally
            {
                Object.DestroyImmediate(loose);
                AaoAtlasTextureAttestation.ResetForTests();
            }
        }
```

Add to `Packages/com.alrauna.amuse/Tests/Editor/Host/UnityGeneratedTextureEvidenceTests.cs`:

```csharp
        [Test]
        public void AdmittedAtlasShape_CapturesDirectlyThroughTheRoute()
        {
            AaoAtlasTextureAttestation.ResetForTests();
            AaoAtlasTextureAttestation.ReadInstalledPackageVersionOrNull =
                _ => "1.9.17";
            var atlas = new Texture2D(16, 16, TextureFormat.RGBA32, false);
            atlas.name = "MainTex (AAO UV Packed)";
            var colors = new Color32[16 * 16];
            for (var i = 0; i < colors.Length; i++)
            {
                colors[i] = new Color32(255, 255, 255, 255);
            }
            atlas.SetPixels32(colors);
            atlas.Apply(false);
            try
            {
                var ok = UnityGeneratedTextureEvidence.TryCapture(
                    atlas,
                    TextureChannel.Alpha,
                    1.0f,
                    null,
                    AlphaPolicyBounds.Inert,
                    0,
                    out var chain);

                Assert.That(ok, Is.True);
                Assert.That(chain, Is.Not.Null);
            }
            finally
            {
                Object.DestroyImmediate(atlas);
                AaoAtlasTextureAttestation.ResetForTests();
            }
        }
```

- [ ] **Step 2: Refresh Unity and run the filters `GeneratedTextureAttestationTests` and `UnityGeneratedTextureEvidenceTests`. Expected: FAIL (the three new tests; the route refuses the path-less atlas today). The existing pins, including `LooseInMemoryTexture_IsRefused`, stay green. Record it.**

- [ ] **Step 3: Move the persisted-branch literals to the shared constants and add the route arm**

In `Packages/com.alrauna.amuse/Editor/Semantics/GeneratedTextureAttestation.cs`, replace the marker test inside `TryIdentifyProducer` (lines 66-68) with:

```csharp
                var textureName = texture.name ?? string.Empty;
                var isAaoTexture =
                    textureName.EndsWith(
                        AaoAtlasTextureAttestation.AtlasNameSuffix,
                        StringComparison.Ordinal) ||
                    textureName.StartsWith(
                        AaoAtlasTextureAttestation.MonotoneNamePrefix,
                        StringComparison.Ordinal);
```

Then extend `TryIdentifyRouteTexture` (lines 115-136) so the AAO arm follows the LAC arm:

```csharp
            if (texture is Texture2D copy &&
                ReplacementTextureAttestation.TryIdentifyReplacement(
                    copy, out _))
            {
                producer = GeneratedTextureProducer.LimitexTextureCompressor;
                return true;
            }
            // The in-memory Avatar Optimizer atlas takes the generated
            // route on the texture-only shape predicate. Every capture
            // reaches a route only after the identity gate admitted the
            // same object under the stricter corroborated conjunct
            // (spec 2026-10-04, section 4.2), so this arm cannot widen
            // the gate.
            // Upstream path (spec 2026-10-04, section 8): an atlas that
            // Avatar Optimizer registers in the NDMF object registry, or
            // persists into a build container, is served by the persisted
            // branch above and retires this arm.
            if (texture is Texture2D atlas &&
                AaoAtlasTextureAttestation.TryIdentifyAtlasShape(atlas))
            {
                producer = GeneratedTextureProducer.Anatawa12AvatarOptimizer;
                return true;
            }
```

- [ ] **Step 4: Refresh Unity, check the console for 0 compile errors, run the filters `GeneratedTextureAttestationTests` and `UnityGeneratedTextureEvidenceTests`. Expected: PASS. Record the counts.**

- [ ] **Step 5: Commit**

```bash
git add Packages/com.alrauna.amuse/Editor/Semantics/GeneratedTextureAttestation.cs \
  Packages/com.alrauna.amuse/Tests/Editor/Semantics/GeneratedTextureAttestationTests.cs \
  Packages/com.alrauna.amuse/Tests/Editor/Host/UnityGeneratedTextureEvidenceTests.cs
git commit -m "feat: route in-build AAO atlases through the generated capture"
```

---

### Task 4: Origin-material threading through the batch capture

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/Host/UnityAlphaFieldEvidence.cs:149-189` (optional parameter, forwarded at the identity gate)
- Modify: `Packages/com.alrauna.amuse/Editor/Host/UnityMaterialEvidenceCapture.cs` (`CaptureTexture`, the pre-pass, both builders)
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Host/UnityAlphaFieldEvidenceTests.cs`
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Host/UnityMaterialEvidenceCaptureTests.cs`

**Interfaces:**
- Consumes: the Task 2 overload `TryGetSourceId(texture, out id, originMaterial)`.
- Produces: `UnityAlphaFieldEvidence.TryCapture(texture, channel, cutoffThreshold, bounds, out source, out chain, out refusal, Material originMaterial = null)`.

- [ ] **Step 1: Write the failing tests**

Add to `Packages/com.alrauna.amuse/Tests/Editor/Host/UnityAlphaFieldEvidenceTests.cs`, beside `AdmittedReplacementCopyCapturesThroughTheGeneratedRoute` (line 1849), following its registry, fixture-folder, and teardown patterns:

```csharp
        [Test]
        public void AdmittedAtlasCapturesWithCorroboratedOriginMaterial()
        {
            var previousRegistry = ObjectRegistry.ActiveRegistry;
            ObjectRegistry.ActiveRegistry = new ObjectRegistry(null);
            var originSource = new Material(
                Shader.Find("Hidden/Alrauna/AmuseTests/Opaque"));
            AssetDatabase.CreateAsset(
                originSource, TempFolder + "/atlas-origin.mat");
            var clone = new Material(originSource.shader)
            {
                name = originSource.name + " build copy",
            };
            var atlas = new Texture2D(16, 16, TextureFormat.RGBA32, true);
            atlas.name = "MainTex (AAO UV Packed)";
            try
            {
                ObjectRegistry.RegisterReplacedObject(originSource, clone);
                AaoAtlasTextureAttestation.ResetForTests();
                AaoAtlasTextureAttestation.ReadInstalledPackageVersionOrNull =
                    _ => "1.9.17";
                var colors = new Color32[16 * 16];
                for (var i = 0; i < colors.Length; i++)
                {
                    colors[i] = new Color32(255, 255, 255, 255);
                }
                atlas.SetPixels32(colors);
                atlas.Apply(true);

                var captured = UnityAlphaFieldEvidence.TryCapture(
                    atlas, TextureChannel.Alpha, 1f, AlphaPolicyBounds.Inert,
                    out var source, out var chain, out var refusal,
                    originMaterial: clone);

                Assert.That(
                    captured, Is.True,
                    "an admitted atlas captures through the generated route, " +
                    "refusal was " + refusal);
                Assert.That(chain, Is.Not.Null);
                Assert.That(
                    source.Value.StartsWith(
                        "unity-aao-atlas:", StringComparison.Ordinal),
                    Is.True);
            }
            finally
            {
                Object.DestroyImmediate(atlas);
                Object.DestroyImmediate(clone);
                AaoAtlasTextureAttestation.ResetForTests();
                ObjectRegistry.ActiveRegistry = previousRegistry;
                DeleteTempFolder();
            }
        }

        [Test]
        public void AdmittedAtlasWithoutOriginMaterial_RefusesAtTheGate()
        {
            var atlas = new Texture2D(16, 16, TextureFormat.RGBA32, false);
            atlas.name = "MainTex (AAO UV Packed)";
            try
            {
                AaoAtlasTextureAttestation.ResetForTests();
                AaoAtlasTextureAttestation.ReadInstalledPackageVersionOrNull =
                    _ => "1.9.17";

                var captured = UnityAlphaFieldEvidence.TryCapture(
                    atlas, TextureChannel.Alpha, 1f, AlphaPolicyBounds.Inert,
                    out _, out _, out var refusal);

                Assert.That(captured, Is.False);
                Assert.That(
                    refusal,
                    Is.EqualTo(TextureCaptureRefusalReason.UnavailableCapture));
            }
            finally
            {
                Object.DestroyImmediate(atlas);
                AaoAtlasTextureAttestation.ResetForTests();
            }
        }

        [Test]
        public void StreamingFlagOnAdmittedAtlasStillTakesTheGeneratedRoute()
        {
            var previousRegistry = ObjectRegistry.ActiveRegistry;
            ObjectRegistry.ActiveRegistry = new ObjectRegistry(null);
            var originSource = new Material(
                Shader.Find("Hidden/Alrauna/AmuseTests/Opaque"));
            AssetDatabase.CreateAsset(
                originSource, TempFolder + "/atlas-streaming.mat");
            var clone = new Material(originSource.shader);
            var atlas = new Texture2D(16, 16, TextureFormat.RGBA32, true);
            atlas.name = "MainTex (AAO UV Packed)";
            try
            {
                ObjectRegistry.RegisterReplacedObject(originSource, clone);
                // AAO mirrors the source streaming flag onto the atlas
                // through the serialized property; do the same here.
                var serialized = new SerializedObject(atlas);
                serialized.FindProperty("m_StreamingMipmaps").boolValue = true;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                AaoAtlasTextureAttestation.ResetForTests();
                AaoAtlasTextureAttestation.ReadInstalledPackageVersionOrNull =
                    _ => "1.9.17";
                var colors = new Color32[16 * 16];
                for (var i = 0; i < colors.Length; i++)
                {
                    colors[i] = new Color32(255, 255, 255, 255);
                }
                atlas.SetPixels32(colors);
                atlas.Apply(true);

                var captured = UnityAlphaFieldEvidence.TryCapture(
                    atlas, TextureChannel.Alpha, 1f, AlphaPolicyBounds.Inert,
                    out _, out var chain, out var refusal,
                    originMaterial: clone);

                Assert.That(
                    captured, Is.True,
                    "the generated route precedes the streaming route, so a " +
                    "mirrored streaming flag never blocks an admitted atlas; " +
                    "refusal was " + refusal);
                Assert.That(chain, Is.Not.Null);
            }
            finally
            {
                Object.DestroyImmediate(atlas);
                Object.DestroyImmediate(clone);
                AaoAtlasTextureAttestation.ResetForTests();
                ObjectRegistry.ActiveRegistry = previousRegistry;
                DeleteTempFolder();
            }
        }
```

Add the format falsifier in the same file, mirroring `Bc5ReplacementCopyRefusesCaptureWithUnsupportedFormat` (line 1891):

```csharp
        [Test]
        public void Bc5AdmittedAtlasRefusesWithUnsupportedFormat()
        {
            // --- Falsifier 7 (spec 2026-10-04, section 6): BC5 refuses
            // with UnsupportedFormat even when every admission conjunct
            // holds. ---
            var previousRegistry = ObjectRegistry.ActiveRegistry;
            ObjectRegistry.ActiveRegistry = new ObjectRegistry(null);
            var originSource = new Material(
                Shader.Find("Hidden/Alrauna/AmuseTests/Opaque"));
            AssetDatabase.CreateAsset(
                originSource, TempFolder + "/atlas-bc5.mat");
            var clone = new Material(originSource.shader);
            var atlas = new Texture2D(16, 16, TextureFormat.RGBA32, false);
            atlas.name = "MainTex (AAO UV Packed)";
            try
            {
                ObjectRegistry.RegisterReplacedObject(originSource, clone);
                EditorUtility.CompressTexture(
                    atlas, TextureFormat.BC5, TextureCompressionQuality.Normal);
                AaoAtlasTextureAttestation.ResetForTests();
                AaoAtlasTextureAttestation.ReadInstalledPackageVersionOrNull =
                    _ => "1.9.17";

                var captured = UnityAlphaFieldEvidence.TryCapture(
                    atlas, TextureChannel.Alpha, 1f, AlphaPolicyBounds.Inert,
                    out _, out _, out var refusal,
                    originMaterial: clone);

                Assert.That(captured, Is.False);
                Assert.That(
                    refusal,
                    Is.EqualTo(TextureCaptureRefusalReason.UnsupportedFormat));
            }
            finally
            {
                Object.DestroyImmediate(atlas);
                Object.DestroyImmediate(clone);
                AaoAtlasTextureAttestation.ResetForTests();
                ObjectRegistry.ActiveRegistry = previousRegistry;
                DeleteTempFolder();
            }
        }
```

Add to `Packages/com.alrauna.amuse/Tests/Editor/Host/UnityMaterialEvidenceCaptureTests.cs`, reusing the class's `NewMaterial`, `ClassifyIsolationRegion`, fixture folder, and teardown:

```csharp
        [Test]
        public void RegisteredCloneOverAdmittedAtlas_ProvesOpaqueAtItsOwnCutoff()
        {
            var originSource = NewMaterial(
                "Hidden/Alrauna/AmuseTests/LilToonCutoutConversionTest");
            AssetDatabase.CreateAsset(
                originSource, TempFolder + "/atlas-batch-source.mat");
            var clone = new Material(originSource.shader)
            {
                name = originSource.name + " build copy",
            };
            var atlas = new Texture2D(16, 16, TextureFormat.RGBA32, false);
            atlas.name = "MainTex (AAO UV Packed)";
            var colors = new Color32[16 * 16];
            for (var i = 0; i < colors.Length; i++)
            {
                colors[i] = new Color32(255, 255, 255, 255);
            }
            atlas.SetPixels32(colors);
            atlas.Apply(false);
            clone.SetTexture("_MainTex", atlas);
            clone.SetFloat("_Cutoff", 0.75f);
            var previous = ObjectRegistry.ActiveRegistry;
            ObjectRegistry.ActiveRegistry = new ObjectRegistry(null);
            try
            {
                ObjectRegistry.RegisterReplacedObject(originSource, clone);
                AaoAtlasTextureAttestation.ResetForTests();
                AaoAtlasTextureAttestation.ReadInstalledPackageVersionOrNull =
                    _ => "1.9.17";

                var captured = UnityMaterialEvidenceCapture.Capture(
                    new[]
                    {
                        new MaterialEvidenceCaptureInput(
                            clone,
                            LilToonCutoutMaterialSemantics.AlphaEvidenceRequest),
                    })[0];

                Assert.That(
                    ClassifyIsolationRegion(captured, 0.75f),
                    Is.EqualTo(TriangleAlphaOutcome.ProvenOpaque),
                    "a registered clone over an admitted opaque atlas " +
                    "proves its region opaque");
            }
            finally
            {
                ObjectRegistry.ActiveRegistry = previous;
                AaoAtlasTextureAttestation.ResetForTests();
                Object.DestroyImmediate(atlas);
                Object.DestroyImmediate(clone);
            }
        }

        [Test]
        public void UnregisteredCloneOverAdmittedAtlas_StaysUnknown()
        {
            var clone = NewMaterial(
                "Hidden/Alrauna/AmuseTests/LilToonCutoutConversionTest");
            var atlas = new Texture2D(16, 16, TextureFormat.RGBA32, false);
            atlas.name = "MainTex (AAO UV Packed)";
            var colors = new Color32[16 * 16];
            for (var i = 0; i < colors.Length; i++)
            {
                colors[i] = new Color32(255, 255, 255, 255);
            }
            atlas.SetPixels32(colors);
            atlas.Apply(false);
            clone.SetTexture("_MainTex", atlas);
            clone.SetFloat("_Cutoff", 0.75f);
            try
            {
                AaoAtlasTextureAttestation.ResetForTests();
                AaoAtlasTextureAttestation.ReadInstalledPackageVersionOrNull =
                    _ => "1.9.17";

                var captured = UnityMaterialEvidenceCapture.Capture(
                    new[]
                    {
                        new MaterialEvidenceCaptureInput(
                            clone,
                            LilToonCutoutMaterialSemantics.AlphaEvidenceRequest),
                    })[0];

                Assert.That(
                    ClassifyIsolationRegion(captured, 0.75f),
                    Is.Not.EqualTo(TriangleAlphaOutcome.ProvenOpaque),
                    "without a registry pair the slot material corroborates " +
                    "nothing, so the region stays unproven");
            }
            finally
            {
                AaoAtlasTextureAttestation.ResetForTests();
                Object.DestroyImmediate(atlas);
            }
        }
```

- [ ] **Step 2: Refresh Unity and run the filters `UnityAlphaFieldEvidenceTests` and `UnityMaterialEvidenceCaptureTests`. Expected: FAIL (the corroborated-path tests refuse at the identity gate today, because `TryCapture` forwards no material; the BC5 falsifier fails because today's refusal is `UnavailableCapture`, not `UnsupportedFormat`). Record the failing names and messages.**

- [ ] **Step 3: Thread the parameter**

In `Packages/com.alrauna.amuse/Editor/Host/UnityAlphaFieldEvidence.cs`, extend the `TryCapture` signature at line 149 with a trailing optional parameter and forward it at the identity gate (line 185):

```csharp
        internal static bool TryCapture(
            Texture texture,
            TextureChannel channel,
            float cutoffThreshold,
            AlphaPolicyBounds bounds,
            out TextureSourceId source,
            out AlphaMipChain chain,
            out TextureCaptureRefusalReason refusal,
            Material originMaterial = null)
```

```csharp
            if (!UnityTextureEvidence.TryGetSourceId(
                    texture2D, out source, originMaterial))
```

In `Packages/com.alrauna.amuse/Editor/Host/UnityMaterialEvidenceCapture.cs`:

1. Give `MaterialBuilder` (line 1477) the live material, assigned where `CaptureAssignments` constructs the builder, beside the fields it already fills:

```csharp
            internal readonly Material SourceMaterial;
```

with `SourceMaterial = input.SourceMaterial,` in the construction site inside `CaptureAssignments`.

2. Give `SharedTextureBuilder` (line 1504) the first material under which the identity resolved, through a new constructor parameter stored as:

```csharp
            internal readonly Material OriginMaterial;
```

3. In the pre-pass (line 949), pass the input's material:

```csharp
                        !UnityTextureEvidence.TryGetSourceId(
                            texture.Texture, out var source,
                            input.SourceMaterial)
```

and pass the same material when constructing the `SharedTextureBuilder` (line 965).

4. In `CaptureTexture` (line 1336), add `Material originMaterial,` after the `AlphaPolicyBounds bounds,` parameter, and pass `originMaterial: originMaterial` to both `UnityAlphaFieldEvidence.TryCapture` calls (lines 1371 and 1383).

5. At the shared capture call (line 995), pass `shared.OriginMaterial`. At the fallback call (line 1030), pass `material.SourceMaterial`.

- [ ] **Step 4: Refresh Unity, check the console for 0 compile errors, run the filters `UnityAlphaFieldEvidenceTests` and `UnityMaterialEvidenceCaptureTests`. Expected: PASS. Record the counts.**

- [ ] **Step 5: Commit**

```bash
git add Packages/com.alrauna.amuse/Editor/Host/UnityAlphaFieldEvidence.cs \
  Packages/com.alrauna.amuse/Editor/Host/UnityMaterialEvidenceCapture.cs \
  Packages/com.alrauna.amuse/Tests/Editor/Host/UnityAlphaFieldEvidenceTests.cs \
  Packages/com.alrauna.amuse/Tests/Editor/Host/UnityMaterialEvidenceCaptureTests.cs
git commit -m "feat: thread the origin material through the texture capture"
```

---

### Task 5: Session clears, hint sentence, and closure

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/Build/AlphaSeparationApply.cs:911-913`
- Modify: `Packages/com.alrauna.amuse/Editor/Build/AmusePlatformFinishPlugin.cs:403-405`
- Modify: `Packages/com.alrauna.amuse/Editor/Build/AmuseReportStrings.cs:315-331`
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Build/AmuseReportStringsTests.cs`

**Interfaces:**
- Consumes: `AaoAtlasTextureAttestation.ClearVersionCacheForSession()` from Task 1.
- Produces: none. Closing task.

- [ ] **Step 1: Write the failing test**

Add to `Packages/com.alrauna.amuse/Tests/Editor/Build/AmuseReportStringsTests.cs`, beside `UnavailableCaptureHint_NamesTheUpstreamReplacement` (line 181):

```csharp
        [Test]
        public void UnavailableCaptureHint_NamesTheCharacterizedAtlasRule()
        {
            var hint = AmuseReportStrings.Get(
                "amuse.texture.UnavailableCapture:hint");

            Assert.That(hint, Does.Contain(
                "characterized version of Avatar Optimizer"),
                "the hint separates admitted atlases from the refusals " +
                "that remain");
            Assert.That(hint, Does.Contain("generated route"));
        }
```

- [ ] **Step 2: Refresh Unity and run the filter `AmuseReportStringsTests`. Expected: FAIL (the hint lacks both phrases). Record it.**

- [ ] **Step 3: Add the session clears and the hint sentence**

In `Packages/com.alrauna.amuse/Editor/Build/AlphaSeparationApply.cs` and `Packages/com.alrauna.amuse/Editor/Build/AmusePlatformFinishPlugin.cs`, extend each of the two clear blocks so they read:

```csharp
            UnityGeneratedTextureEvidence.ClearCache();
            ReplacementTextureIdentity.ClearSession();
            ReplacementTextureAttestation.ClearVersionCacheForSession();
            AaoAtlasTextureAttestation.ClearVersionCacheForSession();
```

The atlas identity mint keeps no session state, and its doc comment says why, so it clears nothing here.

In `Packages/com.alrauna.amuse/Editor/Build/AmuseReportStrings.cs`, inside the `amuse.texture.UnavailableCapture:hint` value (lines 315-331), insert one sentence directly after the sentence that ends `still has no proof here. `:

```
An atlas from a characterized version of Avatar Optimizer reads
through the generated route, so its slot keeps its proof.
```

Keep every other sentence unchanged.

- [ ] **Step 4: Refresh Unity, check the console for 0 compile errors, run the filter `AmuseReportStringsTests`. Expected: PASS. Record the counts.**

- [ ] **Step 5: Commit**

```bash
git add Packages/com.alrauna.amuse/Editor/Build/AlphaSeparationApply.cs \
  Packages/com.alrauna.amuse/Editor/Build/AmusePlatformFinishPlugin.cs \
  Packages/com.alrauna.amuse/Editor/Build/AmuseReportStrings.cs \
  Packages/com.alrauna.amuse/Tests/Editor/Build/AmuseReportStringsTests.cs
git commit -m "feat: name the characterized atlas rule in the capture refusal hint"
```

- [ ] **Step 6: Closure verification**

1. Run the focused EditMode filters `AaoAtlasTextureAttestationTests`, `UnityTextureEvidenceTests`, `GeneratedTextureAttestationTests`, `ReplacementTextureAttestationTests`, `UnityGeneratedTextureEvidenceTests`, `UnityAlphaFieldEvidenceTests`, `UnityMaterialEvidenceCaptureTests`, `AmuseReportStringsTests`. Expected: all PASS. Record the observed counts.
2. Run the full EditMode assembly `Alrauna.Amuse.Tests.Editor`. Expected: 0 failures. Record the observed counts. A run that reports 0 tests is a failure.
3. Run `git diff --check` and confirm it reports nothing.
4. Inspect `git status` and the branch diff. Only the files named by Tasks 0 to 5 may appear.
5. Delete any throwaway fixture folders or scripts created outside the named files.
6. A Lab build after merge characterizes the end-to-end effect on the private corpus avatar. It is not a merge gate. Record its observed counts separately, sanitized, if it runs.
