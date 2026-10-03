# LAC Attested Replacement Texture Capture Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Admit Limitex Avatar Compressor (LAC) replacement copies into the
alpha evidence capture under a version-pinned, fail-closed conjunct, so
LAC-processed materials become analyzable.

**Architecture:** One new admission class (`ReplacementTextureAttestation`)
with a package-version seam and a shipped-empty admitted set. One new
identity form (`unity-replacement:<guid>:<localId>`) minted by a session
ledger that poisons duplicate claims. The existing generated capture route
gains the class through one unified predicate. Per-fact proofs extend at
their existing seams. The retirement path lives only as a code comment at
the admission site.

**Tech Stack:** Unity 2022.3.22f1 editor assembly (`Alrauna.Amuse.Editor`),
NUnit EditMode tests (`Alrauna.Amuse.Tests.Editor`), NDMF `ObjectRegistry`.

**Spec:** `docs/superpowers/specs/2026-10-03-lac-generated-texture-attestation-design.md`
- The plan argues from the spec. Executors read both. Spec section numbers
  below refer to that file.

## Global Constraints

- Fail closed. Every unmet admission conjunct refuses with the existing
  refusal vocabulary. No new refusal kinds anywhere in this plan.
- The admitted version set ships empty. No version joins without a dated
  characterization. Tests inject versions through seams only.
- No format allowlist changes. DXT1, DXT5, BC7 admitted; BC5 and ASTC
  refuse `UnsupportedFormat` at capture.
- No consent surface, no package manifest changes, no shader attestation
  changes.
- Report strings follow Simplified Technical English: short sentences, no
  contractions, no semicolons.
- Production types stay `internal`. File name equals type name. Test
  classes mirror production folders, one per production type. Method names
  are behavior sentences. Falsifiers carry the marker
  `--- Falsifier N: ... ---` and a no-op guard so later changes cannot
  silence them.
- No absolute paths, no host names, no ports, no private identifiers in
  code, comments, commits, or test fixtures.
- Tests never install LAC. Registry fixtures use the established pattern:
  save `ObjectRegistry.ActiveRegistry`, set `new ObjectRegistry(null)`,
  register, restore in `finally`.
- Test runner mechanics: after creating files, refresh Unity and wait for
  compile, then run a focused EditMode filter through the dev editor
  instance after the `Application.dataPath` identity check. A focused run
  that reports 0 tests is a failure. A successful compile is never
  validation. Record observed counts.
- Git boundary: work on the topic branch
  `feat/lac-generated-texture-attestation`. Commit after each task's tests
  pass. Never push, never open a PR, without explicit authorization.

## Shared fixture shape (Tasks 2, 3, 4, 5)

An admitted replacement copy needs four facts. The fixture helper builds
all four; tasks then remove one fact per falsifier. Put the helper in each
test class that needs it (the classes are independent).

```csharp
private const string TempFolder = "Assets/AmuseTests_ReplacementTexture";

private static Texture2D ImportSourceAsset(string name)
{
    if (!AssetDatabase.IsValidFolder(TempFolder))
    {
        AssetDatabase.CreateFolder("Assets", "AmuseTests_ReplacementTexture");
    }
    var blank = new Texture2D(4, 4, TextureFormat.RGBA32, false);
    var png = ImageConversion.EncodeToPNG(blank);
    UnityEngine.Object.DestroyImmediate(blank);
    File.WriteAllBytes($"{TempFolder}/{name}.png", png);
    AssetDatabase.ImportAsset($"{TempFolder}/{name}.png");
    return AssetDatabase.LoadAssetAtPath<Texture2D>($"{TempFolder}/{name}.png");
}

private static void DeleteTempFolder()
{
    if (AssetDatabase.IsValidFolder(TempFolder))
    {
        AssetDatabase.DeleteAsset(TempFolder);
    }
}

private static Texture2D MakeAdmittedCopy(Texture2D source)
{
    var copy = new Texture2D(4, 4, TextureFormat.RGBA32, false);
    copy.name = source.name + "_compressed";
    return copy;
}
```

Registration, version injection, and restore ride every test:

```csharp
private string _previousVersion;
private IReadOnlyList<string> _previousAdmitted;
private ObjectRegistry _previousRegistry;

[SetUp]
public void StoreSeams()
{
    _previousRegistry = ObjectRegistry.ActiveRegistry;
    ObjectRegistry.ActiveRegistry = new ObjectRegistry(null);
    ReplacementTextureAttestation.SetAdmittedVersionsForTests("0.9.0");
    ReplacementTextureAttestation.ReadInstalledPackageVersionOrNull =
        _ => "0.9.0";
}

[TearDown]
public void RestoreSeams()
{
    ReplacementTextureAttestation.ResetForTests();
    ObjectRegistry.ActiveRegistry = _previousRegistry;
    DeleteTempFolder();
}
```

---

### Task 1: Version seam and the shipped-empty admitted set

**Files:**
- Create: `Packages/com.alrauna.amuse/Editor/Semantics/ReplacementTextureAttestation.cs`
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Semantics/ReplacementTextureAttestationTests.cs`

**Interfaces:**
- Consumes: `UnityEditor.PackageManager.Client.List(offline: true)`,
  `IRequest.WaitForCompletion()`.
- Produces: `ProducerPackageName`, `ReplacementNameSuffix`,
  `ReadInstalledPackageVersionOrNull` (settable `Func<string, string>`),
  `AdmittedVersions`, `SetAdmittedVersionsForTests(params string[])`,
  `ResetForTests()`, `TryReadInstalledProducerVersion(out string)`,
  `IsVersionAdmitted(string)`. Tasks 2-5 consume these names exactly.

- [ ] **Step 1: Write the failing tests**

```csharp
using System;
using System.Collections.Generic;
using NUnit.Framework;
using Alrauna.Amuse.Editor.Semantics;

namespace Alrauna.Amuse.Tests.Editor.Semantics
{
    public sealed class ReplacementTextureAttestationTests
    {
        [Test]
        public void ShippedAdmittedSetIsEmptyAndRefusesEveryVersion()
        {
            Assert.That(
                ReplacementTextureAttestation.AdmittedVersions, Is.Empty,
                "the set ships empty; a version joins only after a " +
                "dated characterization");
            Assert.That(
                ReplacementTextureAttestation.IsVersionAdmitted("0.9.0"),
                Is.False);
        }

        [Test]
        public void InjectedVersionIsAdmittedAndOtherVersionIsRefused()
        {
            ReplacementTextureAttestation.SetAdmittedVersionsForTests("0.9.0");
            Assert.That(
                ReplacementTextureAttestation.IsVersionAdmitted("0.9.0"),
                Is.True);
            Assert.That(
                ReplacementTextureAttestation.IsVersionAdmitted("0.1.0"),
                Is.False);
        }

        [Test]
        public void NullOrEmptyVersionIsNeverAdmitted()
        {
            ReplacementTextureAttestation.SetAdmittedVersionsForTests("0.9.0");
            Assert.That(
                ReplacementTextureAttestation.IsVersionAdmitted(null), Is.False);
            Assert.That(
                ReplacementTextureAttestation.IsVersionAdmitted(""), Is.False);
        }

        [Test]
        public void ReadableProviderAnswerSatisfiesTheInstalledVersionRead()
        {
            ReplacementTextureAttestation.ReadInstalledPackageVersionOrNull =
                _ => "1.2.3";
            Assert.That(
                ReplacementTextureAttestation.TryReadInstalledProducerVersion(
                    out var version),
                Is.True);
            Assert.That(version, Is.EqualTo("1.2.3"));
        }

        [Test]
        public void UnreadableInstalledVersionRefusesTheRead()
        {
            ReplacementTextureAttestation.ReadInstalledPackageVersionOrNull =
                _ => null;
            Assert.That(
                ReplacementTextureAttestation.TryReadInstalledProducerVersion(
                    out _),
                Is.False,
                "an unreadable version refuses, following the lilToon " +
                "baker precedent");
        }

        [Test]
        public void ResetForTestsRestoresTheEmptySetAndTheProductionProvider()
        {
            ReplacementTextureAttestation.SetAdmittedVersionsForTests("0.9.0");
            ReplacementTextureAttestation.ReadInstalledPackageVersionOrNull =
                _ => "9.9.9";
            ReplacementTextureAttestation.ResetForTests();
            Assert.That(
                ReplacementTextureAttestation.AdmittedVersions, Is.Empty);
            ReplacementTextureAttestation.ReadInstalledPackageVersionOrNull =
                null;
            // With the production provider restored, the delegate no
            // longer answers "9.9.9" for every name.
            ReplacementTextureAttestation.TryReadInstalledProducerVersion(
                out var version);
            Assert.That(version, Is.Not.EqualTo("9.9.9"));
        }
    }
}
```

- [ ] **Step 2: Refresh Unity and run the focused filter. Expected: FAIL
  (compile error, type `ReplacementTextureAttestation` does not exist).
  Record the observed failure.**

- [ ] **Step 3: Write the implementation**

```csharp
using System;
using System.Collections.Generic;
using UnityEditor.PackageManager;
using UnityEngine;

namespace Alrauna.Amuse.Editor.Semantics
{
    /// <summary>
    /// Attests the Limitex Avatar Compressor replacement-copy shape.
    /// Every conjunct must hold; any miss refuses.
    /// </summary>
    // Upstream path (spec 2026-10-03, section 12). This admission is a
    // special case a future LAC release could delete. It retires when LAC
    // persists each copy as a sub-asset of a dedicated container asset, as
    // NDMF already does for AAO outputs, keeps registering source-and-copy
    // pairs in the object registry, publishes stable package versions, and
    // documents its per-release output shape. Until then this class, the
    // unity-replacement identity form, and the duplicate guard carry the
    // contract.
    internal static class ReplacementTextureAttestation
    {
        internal const string ProducerPackageName =
            "dev.limitex.avatar-compressor";
        internal const string ReplacementNameSuffix = "_compressed";

        private static readonly List<string> Admitted = new List<string>();

        internal static Func<string, string> ReadInstalledPackageVersionOrNull
        {
            get;
            set;
        } = ReadVersionFromPackageManager;

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

        internal static void ResetForTests()
        {
            Admitted.Clear();
            ReadInstalledPackageVersionOrNull = ReadVersionFromPackageManager;
        }

        internal static bool TryReadInstalledProducerVersion(
            out string version)
        {
            version = ReadInstalledPackageVersionOrNull?.Invoke(
                ProducerPackageName);
            return !string.IsNullOrEmpty(version);
        }

        internal static bool IsVersionAdmitted(string version)
        {
            return !string.IsNullOrEmpty(version) && Admitted.Contains(version);
        }

        private static string ReadVersionFromPackageManager(string packageName)
        {
            // Offline listing never touches the network, so blocking inside
            // an editor build pass is safe.
            var request = Client.List(true);
            request.WaitForCompletion();
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

- [ ] **Step 4: Refresh Unity and run the focused filter. Expected: PASS
  (6/6). Record the observed count.**

- [ ] **Step 5: Commit**

```bash
git add Packages/com.alrauna.amuse/Editor/Semantics/ReplacementTextureAttestation.cs \
  Packages/com.alrauna.amuse/Tests/Editor/Semantics/ReplacementTextureAttestationTests.cs
git commit -m "feat: add the LAC producer version seam with an empty admitted set"
```

---

### Task 2: The conjunct admission predicate

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/Semantics/ReplacementTextureAttestation.cs`
- Move: `Packages/com.alrauna.amuse/Editor/Build/RegisteredSourceIdentity.cs`
  to `Packages/com.alrauna.amuse/Editor/Semantics/RegisteredSourceIdentity.cs`,
  namespace `Alrauna.Amuse.Editor.Build` to `Alrauna.Amuse.Editor.Semantics`.
  Semantics must not depend on Build. Grep `RegisteredSourceIdentity`
  across `Editor/` and `Tests/`, and update each `using` to the new
  namespace. No body change: the read-only lookup contract stays byte for
  byte.
- Test: `Tests/Editor/Semantics/ReplacementTextureAttestationTests.cs`

**Interfaces:**
- Consumes: Task 1 names; `RegisteredSourceIdentity.Resolve` (now
  `Alrauna.Amuse.Editor.Semantics`); `AssetDatabase.GetAssetPath`,
  `AssetDatabase.TryGetGUIDAndLocalFileIdentifier` through the shared
  fixture.
- Produces: `internal static bool TryIdentifyReplacement(
  Texture texture, out Texture2D source)`. `source` is the asset-backed
  resolved source; `null` on refusal. Tasks 3 and 4 consume this exact
  signature.

- [ ] **Step 1: Move `RegisteredSourceIdentity.cs` to Semantics, update the
  namespace and every `using`, run the focused filter
  `RegisteredSourceIdentityTests`. Expected: PASS unchanged (2/2). This is
  a refactor gate: any failure here stops the task.**

- [ ] **Step 2: Write the failing tests**

```csharp
[Test]
public void RegisteredSuffixedPathlessCopyWithAdmittedVersionIsAdmitted()
{
    var source = ImportSourceAsset("admitted");
    var copy = MakeAdmittedCopy(source);
    try
    {
        ObjectRegistry.RegisterReplacedObject(source, copy);
        Assert.That(
            ReplacementTextureAttestation.TryIdentifyReplacement(
                copy, out var resolvedSource),
            Is.True);
        Assert.That(resolvedSource, Is.EqualTo(source),
            "the admission names the resolved asset-backed source");
    }
    finally
    {
        UnityEngine.Object.DestroyImmediate(copy);
    }
}

[Test]
public void UnsuffixedRegisteredCopyIsRefused()
{
    // --- Falsifier 2: a registered copy without the suffix refuses. ---
    var source = ImportSourceAsset("nosuffix");
    var copy = new Texture2D(4, 4, TextureFormat.RGBA32, false);
    copy.name = source.name;
    try
    {
        ObjectRegistry.RegisterReplacedObject(source, copy);
        Assert.That(
            ReplacementTextureAttestation.TryIdentifyReplacement(
                copy, out _),
            Is.False);
    }
    finally
    {
        UnityEngine.Object.DestroyImmediate(copy);
    }
}

[Test]
public void SuffixedUnregisteredCopyIsRefused()
{
    // --- Falsifier 1: the suffix alone never admits. ---
    var source = ImportSourceAsset("lonely");
    var copy = MakeAdmittedCopy(source);
    try
    {
        Assert.That(
            ReplacementTextureAttestation.TryIdentifyReplacement(
                copy, out _),
            Is.False,
            "a name marker without a registry registration refuses");
    }
    finally
    {
        UnityEngine.Object.DestroyImmediate(copy);
    }
}

[Test]
public void CopyOfInMemorySourceIsRefusedOneHopOnly()
{
    var memorySource = new Texture2D(4, 4, TextureFormat.RGBA32, false);
    var copy = new Texture2D(4, 4, TextureFormat.RGBA32, false);
    copy.name = "chained_compressed";
    try
    {
        ObjectRegistry.RegisterReplacedObject(memorySource, copy);
        Assert.That(
            ReplacementTextureAttestation.TryIdentifyReplacement(
                copy, out _),
            Is.False,
            "the source must be asset-backed; chains admit one hop only");
    }
    finally
    {
        UnityEngine.Object.DestroyImmediate(copy);
        UnityEngine.Object.DestroyImmediate(memorySource);
    }
}

[Test]
public void UninstalledProducerOrUnlistedVersionRefusesTheCopy()
{
    // --- Falsifier 3: registration plus suffix without the version
    // conjunct refuses. ---
    var source = ImportSourceAsset("noversion");
    var copy = MakeAdmittedCopy(source);
    try
    {
        ObjectRegistry.RegisterReplacedObject(source, copy);
        ReplacementTextureAttestation.SetAdmittedVersionsForTests();
        Assert.That(
            ReplacementTextureAttestation.TryIdentifyReplacement(
                copy, out _),
            Is.False,
            "the shipped-empty set refuses every copy");
        ReplacementTextureAttestation.ReadInstalledPackageVersionOrNull =
            _ => null;
        ReplacementTextureAttestation.SetAdmittedVersionsForTests("0.9.0");
        Assert.That(
            ReplacementTextureAttestation.TryIdentifyReplacement(
                copy, out _),
            Is.False,
            "an unreadable installed version refuses");
    }
    finally
    {
        UnityEngine.Object.DestroyImmediate(copy);
    }
}

[Test]
public void AssetBackedTextureNeverEntersTheReplacementClass()
{
    // No-op guard: the fixture itself must be asset-backed, or this test
    // proves nothing.
    var source = ImportSourceAsset("guarded");
    Assert.That(AssetDatabase.GetAssetPath(source), Is.Not.Empty);
    Assert.That(
        ReplacementTextureAttestation.TryIdentifyReplacement(
            source, out _),
        Is.False);
}
```

- [ ] **Step 3: Refresh Unity and run the focused filter. Expected: FAIL
  (`TryIdentifyReplacement` does not exist). Record it.**

- [ ] **Step 4: Implement the conjunction**

```csharp
internal static bool TryIdentifyReplacement(Texture texture, out Texture2D source)
{
    source = null;
    if (!(texture is Texture2D copy))
    {
        return false;
    }
    // Conjunct 1: a live copy with no asset path.
    if (!string.IsNullOrEmpty(AssetDatabase.GetAssetPath(copy)))
    {
        return false;
    }
    // Conjunct 2: the pinned name shape.
    var name = copy.name ?? string.Empty;
    if (!name.EndsWith(ReplacementNameSuffix, StringComparison.Ordinal))
    {
        return false;
    }
    // Conjunct 3: the registry names an asset-backed source, one hop only.
    if (!(RegisteredSourceIdentity.Resolve(copy) is Texture2D resolved))
    {
        return false;
    }
    if (string.IsNullOrEmpty(AssetDatabase.GetAssetPath(resolved)))
    {
        return false;
    }
    // Conjunct 4: the producer is installed and its version is admitted.
    if (!TryReadInstalledProducerVersion(out var version) ||
        !IsVersionAdmitted(version))
    {
        return false;
    }
    source = resolved;
    return true;
}
```

Add `using UnityEditor;` and the System namespace as needed.

- [ ] **Step 5: Refresh Unity and run the focused filter. Expected: PASS
  (12/12: 6 from Task 1, 6 new). Record it.**

- [ ] **Step 6: Commit**

```bash
git add Packages/com.alrauna.amuse/Editor/Semantics/ReplacementTextureAttestation.cs \
  Packages/com.alrauna.amuse/Editor/Semantics/RegisteredSourceIdentity.cs \
  Packages/com.alrauna.amuse/Tests/Editor/Semantics/ReplacementTextureAttestationTests.cs
git commit -m "feat: admit the version-pinned LAC replacement-copy shape"
```

---

### Task 3: The replacement identity form, the session ledger, and the poison guard

**Files:**
- Create: `Packages/com.alrauna.amuse/Editor/Semantics/ReplacementTextureIdentity.cs`
- Modify: `Packages/com.alrauna.amuse/Editor/Semantics/UnityTextureEvidence.cs`
  (`TryGetSourceId`, lines 19-51 as of 2026-10-03)
- Modify: `Packages/com.alrauna.amuse/Editor/Host/AlphaFieldSet.cs`
  (`TryGetFor`, lines 168+ as of 2026-10-03)
- Modify: `Packages/com.alrauna.amuse/Editor/Build/AmusePlatformFinishPlugin.cs:403`
  and `Packages/com.alrauna.amuse/Editor/Build/AlphaSeparationApply.cs:910`:
  add `ReplacementTextureIdentity.ClearSession();` beside each existing
  `UnityGeneratedTextureEvidence.ClearCache();`
- Test: `Tests/Editor/Semantics/ReplacementTextureIdentityTests.cs`, plus
  additions to `Tests/Editor/Semantics/UnityTextureEvidenceTests.cs` and
  `Tests/Editor/Host/AlphaFieldSetTests.cs`

**Interfaces:**
- Consumes: Task 2 `TryIdentifyReplacement`; `AssetDatabase.TryGetGUIDAndLocalFileIdentifier`.
- Produces: `internal static bool TryMint(Texture2D source, int copyInstanceId, out TextureSourceId id)`;
  `internal static bool IsUsable(TextureSourceId id)`;
  `internal static void ClearSession()`. The id form is exactly
  `"unity-replacement:" + guid.ToLowerInvariant() + ":" + localId`.
  Task 4 and Task 5 consume `TryMint` through `TryGetSourceId` only.

- [ ] **Step 1: Write the failing tests**

`ReplacementTextureIdentityTests.cs`:

```csharp
using NUnit.Framework;
using UnityEngine;
using Alrauna.Amuse.Editor.Semantics;

namespace Alrauna.Amuse.Tests.Editor.Semantics
{
    public sealed class ReplacementTextureIdentityTests
    {
        private ObjectRegistry _previousRegistry;

        [SetUp]
        public void StoreRegistry()
        {
            _previousRegistry = ObjectRegistry.ActiveRegistry;
            ObjectRegistry.ActiveRegistry = new ObjectRegistry(null);
        }

        [TearDown]
        public void RestoreRegistry()
        {
            ObjectRegistry.ActiveRegistry = _previousRegistry;
            ReplacementTextureIdentity.ClearSession();
        }

        [Test]
        public void MintedIdentityNamesTheSourceAsset()
        {
            // The registry lookup needs an asset-backed source, so this
            // test imports one. The folder helper mirrors the shared
            // fixture shape.
            var source = ImportSourceAsset("mint");
            var copy = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            copy.name = source.name + "_compressed";
            try
            {
                ObjectRegistry.RegisterReplacedObject(source, copy);
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                    source, out var guid, out var localId);
                var minted = ReplacementTextureIdentity.TryMint(
                    source, copy.GetInstanceID(), out var id);
                Assert.That(minted, Is.True);
                Assert.That(
                    id.Value, Is.EqualTo(
                        "unity-replacement:" + guid.ToLowerInvariant() +
                        ":" + localId));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(copy);
                DeleteTempFolder();
            }
        }

        [Test]
        public void SameCopyRemintingOneSourceStaysUsable()
        {
            var source = ImportSourceAsset("remint");
            var copy = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            copy.name = source.name + "_compressed";
            try
            {
                ObjectRegistry.RegisterReplacedObject(source, copy);
                ReplacementTextureIdentity.TryMint(
                    source, copy.GetInstanceID(), out var id);
                Assert.That(
                    ReplacementTextureIdentity.TryMint(
                        source, copy.GetInstanceID(), out _),
                    Is.True);
                Assert.That(
                    ReplacementTextureIdentity.IsUsable(id), Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(copy);
                DeleteTempFolder();
            }
        }

        [Test]
        public void SecondCopyClaimingOneSourcePoisonsTheIdentityForBoth()
        {
            // --- Falsifier 4: two distinct objects claiming one source
            // refuse both, and the refusal names the identity fact. ---
            var source = ImportSourceAsset("contested");
            var copyA = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            copyA.name = source.name + "_compressed";
            var copyB = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            copyB.name = source.name + "_compressed";
            try
            {
                ObjectRegistry.RegisterReplacedObject(source, copyA);
                ObjectRegistry.RegisterReplacedObject(source, copyB);
                Assert.That(
                    ReplacementTextureIdentity.TryMint(
                        source, copyA.GetInstanceID(), out var idA),
                    Is.True);
                Assert.That(
                    ReplacementTextureIdentity.TryMint(
                        source, copyB.GetInstanceID(), out _),
                    Is.False,
                    "the second claimant refuses at mint time");
                Assert.That(
                    ReplacementTextureIdentity.IsUsable(idA), Is.False,
                    "the first claimant loses usability too");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(copyA);
                UnityEngine.Object.DestroyImmediate(copyB);
                DeleteTempFolder();
            }
        }

        [Test]
        public void ClearSessionDropsEveryMintedClaim()
        {
            var source = ImportSourceAsset("cleared");
            var copy = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            copy.name = source.name + "_compressed";
            try
            {
                ObjectRegistry.RegisterReplacedObject(source, copy);
                ReplacementTextureIdentity.TryMint(
                    source, copy.GetInstanceID(), out var id);
                ReplacementTextureIdentity.ClearSession();
                Assert.That(
                    ReplacementTextureIdentity.TryMint(
                        source, copy.GetInstanceID(), out _),
                    Is.True,
                    "a cleared session accepts the claim again");
                Assert.That(
                    ReplacementTextureIdentity.IsUsable(id), Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(copy);
                DeleteTempFolder();
            }
        }
    }
}
```

The `ImportSourceAsset`/`DeleteTempFolder` helpers mirror the shared
fixture shape. Falsifier 7's second half, a path-less texture never
receiving the `unity-asset` form, stays pinned by the existing shipped
test `TryGetSourceId_SceneOnlyTexture_IsRefused`; this task must keep it
passing and adds no duplicate.

`UnityTextureEvidenceTests.cs` additions (inside the existing fixture, so
the established import helpers are in scope):

```csharp
[Test]
public void AssetBackedTextureNeverCarriesTheReplacementForm()
{
    // --- Falsifier 7, first half: the shipped forms never cross. ---
    var texture = Import("forms", sourceHasAlpha: true);
    try
    {
        Assert.That(
            UnityTextureEvidence.TryGetSourceId(texture, out var id),
            Is.True);
        Assert.That(
            id.Value.StartsWith("unity-asset:", StringComparison.Ordinal),
            Is.True);
    }
    finally
    {
        // Existing fixture teardown handles the imported asset.
    }
}

[Test]
public void AdmittedCopyCarriesTheReplacementFormThroughTryGetSourceId()
{
    var previousRegistry = ObjectRegistry.ActiveRegistry;
    ObjectRegistry.ActiveRegistry = new ObjectRegistry(null);
    var previousVersion =
        ReplacementTextureAttestation.ReadInstalledPackageVersionOrNull;
    var source = Import("replacement-source", sourceHasAlpha: true);
    var copy = new Texture2D(4, 4, TextureFormat.RGBA32, false);
    copy.name = source.name + "_compressed";
    try
    {
        ObjectRegistry.RegisterReplacedObject(source, copy);
        ReplacementTextureAttestation.SetAdmittedVersionsForTests("0.9.0");
        ReplacementTextureAttestation.ReadInstalledPackageVersionOrNull =
            _ => "0.9.0";
        Assert.That(
            UnityTextureEvidence.TryGetSourceId(copy, out var id),
            Is.True);
        Assert.That(
            id.Value.StartsWith(
                "unity-replacement:", StringComparison.Ordinal),
            Is.True);
    }
    finally
    {
        UnityEngine.Object.DestroyImmediate(copy);
        ReplacementTextureAttestation.ResetForTests();
        ReplacementTextureAttestation.ReadInstalledPackageVersionOrNull =
            previousVersion;
        ObjectRegistry.ActiveRegistry = previousRegistry;
    }
}
```

`AlphaFieldSetTests.cs` addition (the poison guard, asserted through the
one consumer gate):

```csharp
[Test]
public void PoisonedReplacementIdentityFailsClosedAtTheConsumerGate()
{
    // Build the set through the existing WithField seam, then poison the
    // id through the ledger, then assert TryGetFor refuses.
    var sourceId = new TextureSourceId("unity-replacement:poisoned:1");
    var chain = CapturedAlphaFieldBagLevelChainOrNull();
    var set = AlphaFieldSet.WithField(
        sourceId, TextureChannel.Alpha, 1f,
        AlphaPolicyBounds.Inert, chain);
    ReplacementTextureIdentity.ClearSession();
    // Poison without a mint: the ledger must answer unusable for an id it
    // has seen poisoned even when this test never minted it.
    ReplacementTextureIdentity.PoisonForTests(sourceId);
    Assert.That(
        set.TryGetFor(
            /* evidence: build through the existing helper this file
               already uses for TryGetFor tests */,
            null, sourceId, TextureChannel.Alpha, out _),
        Is.False,
        "a poisoned identity fails closed with the missing-evidence path");
}
```

The evidence helper line reuses the exact construction the existing
`TryGetFor` tests in that file use; keep the file's own established
builder, do not invent a new one. Add
`internal static void PoisonForTests(TextureSourceId id)` to the ledger as
a test seam in this task.

- [ ] **Step 2: Refresh Unity and run the focused filters
  (`ReplacementTextureIdentityTests`, `UnityTextureEvidenceTests`,
  `AlphaFieldSetTests`). Expected: FAIL (types and members missing).
  Record it.**

- [ ] **Step 3: Implement**

`ReplacementTextureIdentity.cs`:

```csharp
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Alrauna.Amuse.Editor.Semantics
{
    /// <summary>
    /// Mints and guards the replacement-copy identity form. One source
    /// maps to one copy per build: a second distinct claimant poisons the
    /// identity, and a poisoned identity fails closed at the resolution
    /// gate. The session clears per build beside the generated-route cache.
    /// </summary>
    internal static class ReplacementTextureIdentity
    {
        private const string Prefix = "unity-replacement:";

        private static readonly Dictionary<string, int> Claimants =
            new Dictionary<string, int>();
        private static readonly HashSet<string> Poisoned =
            new HashSet<string>();

        internal static bool TryMint(
            Texture2D source, int copyInstanceId, out TextureSourceId id)
        {
            id = default;
            if (source == null ||
                !AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                    source, out var guid, out var localId) ||
                string.IsNullOrEmpty(guid))
            {
                return false;
            }
            var value = Prefix + guid.ToLowerInvariant() + ":" + localId;
            if (Poisoned.Contains(value))
            {
                return false;
            }
            if (Claimants.TryGetValue(value, out var knownInstanceId))
            {
                if (knownInstanceId != copyInstanceId)
                {
                    Poisoned.Add(value);
                    return false;
                }
                id = new TextureSourceId(value);
                return true;
            }
            Claimants[value] = copyInstanceId;
            id = new TextureSourceId(value);
            return true;
        }

        internal static bool IsUsable(TextureSourceId id)
        {
            return !Poisoned.Contains(id.Value);
        }

        internal static void PoisonForTests(TextureSourceId id)
        {
            Poisoned.Add(id.Value);
        }

        internal static void ClearSession()
        {
            Claimants.Clear();
            Poisoned.Clear();
        }
    }
}
```

`UnityTextureEvidence.TryGetSourceId`: insert this branch after the null
guard and before the existing sub-asset logic, so the shipped
`unity-asset` path keeps priority for anything path-backed:

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
    return false;
}
```

`AlphaFieldSet.TryGetFor`: insert as the first statement after the
argument guards, before `chain = null;`:

```csharp
if (!ReplacementTextureIdentity.IsUsable(source))
{
    chain = null;
    return false;
}
```

This is the one consumer gate the class doc names, so a poisoned identity
dies in the caller's existing `MissingTextureEvidence` path. Add the
`using Alrauna.Amuse.Editor.Semantics;` if absent.

Wire `ReplacementTextureIdentity.ClearSession();` beside both
`UnityGeneratedTextureEvidence.ClearCache();` call sites.

- [ ] **Step 4: Refresh Unity and run the three focused filters. Expected:
  PASS. Record observed counts per filter.**

- [ ] **Step 5: Commit**

```bash
git add Packages/com.alrauna.amuse/Editor/Semantics/ReplacementTextureIdentity.cs \
  Packages/com.alrauna.amuse/Editor/Semantics/UnityTextureEvidence.cs \
  Packages/com.alrauna.amuse/Editor/Host/AlphaFieldSet.cs \
  Packages/com.alrauna.amuse/Editor/Build/AmusePlatformFinishPlugin.cs \
  Packages/com.alrauna.amuse/Editor/Build/AlphaSeparationApply.cs \
  Packages/com.alrauna.amuse/Tests/Editor/Semantics/ReplacementTextureIdentityTests.cs \
  Packages/com.alrauna.amuse/Tests/Editor/Semantics/UnityTextureEvidenceTests.cs \
  Packages/com.alrauna.amuse/Tests/Editor/Host/AlphaFieldSetTests.cs
git commit -m "feat: mint and guard the unity-replacement identity form"
```

---

### Task 4: Route admission and the residency characterization

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/Semantics/GeneratedTextureAttestation.cs`
  (add enum member `LimitexTextureCompressor`, add
  `TryIdentifyRouteTexture(Texture, out GeneratedTextureProducer)`)
- Modify: `Packages/com.alrauna.amuse/Editor/Host/UnityAlphaFieldEvidence.cs:240`
  (route gate)
- Modify: `Packages/com.alrauna.amuse/Editor/Host/UnityGeneratedTextureEvidence.cs:90`
  (the route's internal re-check)
- Test: `Tests/Editor/Host/UnityAlphaFieldEvidenceTests.cs`

**Interfaces:**
- Consumes: Task 2 `TryIdentifyReplacement` through
  `GeneratedTextureAttestation.TryIdentifyRouteTexture`.
- Produces: `internal static bool TryIdentifyRouteTexture(
  Texture texture, out GeneratedTextureProducer producer)`, true for
  container-backed producers and for admitted replacement copies, with
  `producer` set accordingly. Both route call sites consult only this
  method.

- [ ] **Step 1: Characterize the forced streaming flag before writing the
  route test. Run this once through `execute_code` in the dev editor
  instance after the `Application.dataPath` check, then destroy the
  texture. Record the exact values in the task notes.**

```csharp
var t = new Texture2D(8, 8, TextureFormat.RGBA32, true);
var so = new SerializedObject(t);
so.FindProperty("m_StreamingMipmaps").boolValue = true;
so.ApplyModifiedPropertiesWithoutUndo();
var report = "flag=" + t.streamingMipmaps
    + " requestedLoaded=" + t.IsRequestedMipmapLevelLoaded()
    + " loadedLevel=" + t.loadedMipmapLevel
    + " activeLimit=" + t.activeMipmapLimit
    + " mipCount=" + t.mipmapCount;
UnityEngine.Object.DestroyImmediate(t);
return report;
```

- Expected reading (spec section 8): if `requestedLoaded=True` and
  `loadedLevel=0`, the route's residency predicate passes and the capture
  proceeds. If not, copies refuse `UnavailableCapture`, which is the
  behavior observed on 2026-10-02 and is safe: record the values, skip
  Step 4's capture test assertion change, and instead pin the refusal in a
  test asserting `TryCapture` fails with `UnavailableCapture` for the
  forced-flag copy. Either reading satisfies the spec; the refusal reading
  ends the plan's route-arrival work and keeps everything else.

- [ ] **Step 2: Write the failing test (continuation-of-route reading)**

```csharp
[Test]
public void AdmittedReplacementCopyCapturesThroughTheGeneratedRoute()
{
    var previousRegistry = ObjectRegistry.ActiveRegistry;
    ObjectRegistry.ActiveRegistry = new ObjectRegistry(null);
    var source = ImportSourceAsset("route");
    var copy = new Texture2D(16, 16, TextureFormat.RGBA32, true);
    copy.name = source.name + "_compressed";
    try
    {
        ObjectRegistry.RegisterReplacedObject(source, copy);
        ReplacementTextureAttestation.SetAdmittedVersionsForTests("0.9.0");
        ReplacementTextureAttestation.ReadInstalledPackageVersionOrNull =
            _ => "0.9.0";
        // Fill so the chain is nontrivial, then mark the copy admitted by
        // minting through the identity path TryCapture consults.
        var colors = new Color32[16 * 16];
        for (var i = 0; i < colors.Length; i++)
        {
            colors[i] = new Color32(255, 255, 255, 255);
        }
        copy.SetPixels32(colors);
        copy.Apply(true);

        var captured = UnityAlphaFieldEvidence.TryCapture(
            copy, TextureChannel.Alpha, 1f, AlphaPolicyBounds.Inert,
            out _, out var chain, out var refusal);
        Assert.That(
            captured, Is.True,
            "an admitted copy captures through the generated route, " +
            "refusal was " + refusal);
        Assert.That(chain, Is.Not.Null);
    }
    finally
    {
        UnityEngine.Object.DestroyImmediate(copy);
        ReplacementTextureAttestation.ResetForTests();
        ObjectRegistry.ActiveRegistry = previousRegistry;
        DeleteTempFolder();
    }
}
```

Reuse this file's existing `ImportSourceAsset`/temp-folder helper names or
add the shared fixture shape if the file lacks them.

- [ ] **Step 3: Refresh Unity and run the focused filter. Expected: FAIL
  (in the shipped code the identity gate refuses with `UnavailableCapture`
  before any route runs). Record it.**

- [ ] **Step 4: Implement the unified route predicate**

In `GeneratedTextureAttestation.cs`:

```csharp
internal static bool TryIdentifyRouteTexture(
    Texture texture, out GeneratedTextureProducer producer)
{
    producer = GeneratedTextureProducer.None;
    if (texture == null)
    {
        return false;
    }
    if (TryIdentifyProducer(texture, out producer))
    {
        return true;
    }
    if (texture is Texture2D copy &&
        ReplacementTextureAttestation.TryIdentifyReplacement(
            copy, out _))
    {
        producer = GeneratedTextureProducer.LimitexTextureCompressor;
        return true;
    }
    producer = GeneratedTextureProducer.None;
    return false;
}
```

Add `LimitexTextureCompressor` to `GeneratedTextureProducer`. Change both
route gates to `TryIdentifyRouteTexture`:
`UnityAlphaFieldEvidence.cs` at the generated-route branch, and
`UnityGeneratedTextureEvidence.TryCapture` at its internal re-check. Keep
the check before the streaming branch, so the forced flag never reaches the
clone route (spec section 8).

Also pin the residency rule in a pure characterization test, marked as
characterization because it pins existing behavior:

```csharp
[Test]
public void StreamingResidencyRuleCharacterization()
{
    // Characterization: pins the existing pure predicate, passes on
    // first run, and guards the route against reading a non-resident
    // level.
    // --- Falsifier 6: a copy whose residency cannot be proven never
    // blits a non-resident level. ---
    Assert.That(
        UnityGeneratedTextureEvidence.IsStreamingMipmapResident(
            true, true, 0), Is.True);
    Assert.That(
        UnityGeneratedTextureEvidence.IsStreamingMipmapResident(
            true, false, 0), Is.False);
    Assert.That(
        UnityGeneratedTextureEvidence.IsStreamingMipmapResident(
            true, true, 2), Is.False);
    Assert.That(
        UnityGeneratedTextureEvidence.IsStreamingMipmapResident(
            false, false, 0), Is.True);
}
```

- [ ] **Step 5: Refresh Unity and run the focused filter. Expected: PASS.
  If Step 1 produced the refusal reading, run the refusal-pinning test
  instead and expect PASS there. Record the observed count.**

- [ ] **Step 6: Commit**

```bash
git add Packages/com.alrauna.amuse/Editor/Semantics/GeneratedTextureAttestation.cs \
  Packages/com.alrauna.amuse/Editor/Host/UnityAlphaFieldEvidence.cs \
  Packages/com.alrauna.amuse/Editor/Host/UnityGeneratedTextureEvidence.cs \
  Packages/com.alrauna.amuse/Tests/Editor/Host/UnityAlphaFieldEvidenceTests.cs
git commit -m "feat: route admitted replacement copies through the generated capture"
```

---

### Task 5: Per-fact proofs for replacement copies

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/Semantics/UnityTextureEvidence.cs`
  (`TryGetColorInterpretation` generated branch, `TryProveSampledAlphaIsOne`)
- Test: `Tests/Editor/Semantics/UnityTextureEvidenceTests.cs`

**Interfaces:**
- Consumes: `GeneratedTextureAttestation.TryIdentifyRouteTexture`;
  `UnityEngine.Experimental.Rendering.GraphicsFormatUtility`.
- Produces: no new members. Behavior change only inside the two existing
  methods.

- [ ] **Step 1: Write the failing tests**

```csharp
[Test]
public void AdmittedCopyReadsColorInterpretationFromItsGraphicsFormat()
{
    // Fixture mirrors AdmittedCopyCarriesTheReplacementForm's setup:
    // registered copy, admitted version injected. The copy keeps the
    // source's sRGB flag because LAC re-encodes with the source color
    // space.
    var copy = MakeRegisteredAdmittedCopy("colorspace");
    try
    {
        Assert.That(
            UnityTextureEvidence.TryGetColorInterpretation(
                copy, out var interpretation),
            Is.True);
        Assert.That(
            interpretation,
            Is.EqualTo(TextureColorInterpretation.Srgb));
    }
    finally
    {
        UnityEngine.Object.DestroyImmediate(copy);
    }
}

[Test]
public void NoAlphaCopyProvesSampledAlphaExactlyOne()
{
    var copy = MakeRegisteredAdmittedCopy("noalpha", TextureFormat.DXT1);
    try
    {
        Assert.That(
            UnityTextureEvidence.TryProveSampledAlphaIsOne(copy), Is.True,
            "a DXT1 copy carries no alpha channel, so the sampler " +
            "answers exactly one");
    }
    finally
    {
        UnityEngine.Object.DestroyImmediate(copy);
    }
}

[Test]
public void AlphaBearingCopyStaysUnprovenForSampledAlphaOne()
{
    var copy = MakeRegisteredAdmittedCopy("withalpha", TextureFormat.BC7);
    try
    {
        Assert.That(
            UnityTextureEvidence.TryProveSampledAlphaIsOne(copy), Is.False);
    }
    finally
    {
        UnityEngine.Object.DestroyImmediate(copy);
    }
}

[Test]
public void UnadmittedPathlessTextureProvesNothing()
{
    var bare = new Texture2D(4, 4, TextureFormat.DXT1, false);
    try
    {
        Assert.That(
            UnityTextureEvidence.TryProveSampledAlphaIsOne(bare), Is.False);
        Assert.That(
            UnityTextureEvidence.TryGetColorInterpretation(
                bare, out _),
            Is.False);
    }
    finally
    {
        UnityEngine.Object.DestroyImmediate(bare);
    }
}
```

`MakeRegisteredAdmittedCopy` is the shared fixture shape extended with an
optional compress step:

```csharp
private static Texture2D MakeRegisteredAdmittedCopy(
    string sourceName, TextureFormat format = TextureFormat.RGBA32)
{
    var source = ImportSourceAsset(sourceName);
    // Always create uncompressed, then compress: CompressTexture expects an
    // uncompressed input, and a native-DXT1 construction skips SetPixels.
    var copy = new Texture2D(4, 4, TextureFormat.RGBA32, false);
    copy.name = source.name + "_compressed";
    if (format != TextureFormat.RGBA32)
    {
        EditorUtility.CompressTexture(
            copy, format, TextureCompressionQuality.Normal);
    }
    ObjectRegistry.RegisterReplacedObject(source, copy);
    return copy;
}
```

The test class needs the shared fixture section's seam `SetUp`/`TearDown`
(version injection and registry save-restore), because admission requires
an injected admitted version.

- [ ] **Step 2: Refresh Unity and run the focused filter. Expected: FAIL
  (both proofs refuse the copy in the shipped code). Record it.**

- [ ] **Step 3: Implement**

`TryGetColorInterpretation`: extend the generated branch condition from

```csharp
if (GeneratedTextureAttestation.TryIdentifyProducer(texture, out _))
```

to

```csharp
if (texture is Texture2D texture2D &&
    GeneratedTextureAttestation.TryIdentifyRouteTexture(
        texture2D, out _))
```

The body stays unchanged: it already reads the live object's graphics
format and data color space, which is the copy's own fact.

`TryProveSampledAlphaIsOne`: after the importer branch, add:

```csharp
// A replacement copy has no importer. Its format names the sampled
// channels: a format with no alpha component samples exactly one, the
// same fact the RGB24 exemption rests on. This covers DXT1 and RGB24
// copies, and is equally sound for a BC5 copy, because missing channels
// sample as one at runtime; the capture allowlist still refuses BC5 for
// chain-based facts, which is a separate question.
if (texture is Texture2D copy &&
    GeneratedTextureAttestation.TryIdentifyRouteTexture(copy, out _) &&
    !UnityEngine.Experimental.Rendering.GraphicsFormatUtility
        .HasAlphaComponent(copy.graphicsFormat))
{
    return true;
}
return false;
```

- [ ] **Step 4: Refresh Unity and run the focused filter. Expected: PASS.
  Record it. Then add the format-allowlist falsifier to
  `UnityAlphaFieldEvidenceTests` and run it:**

```csharp
[Test]
public void Bc5ReplacementCopyRefusesCaptureWithUnsupportedFormat()
{
    // --- Falsifier 5: BC5 refuses with UnsupportedFormat even when every
    // other conjunct holds. ---
    var previousRegistry = ObjectRegistry.ActiveRegistry;
    ObjectRegistry.ActiveRegistry = new ObjectRegistry(null);
    var source = ImportSourceAsset("bc5");
    var copy = new Texture2D(4, 4, TextureFormat.RGBA32, false);
    copy.name = source.name + "_compressed";
    try
    {
        ObjectRegistry.RegisterReplacedObject(source, copy);
        EditorUtility.CompressTexture(
            copy, TextureFormat.BC5, TextureCompressionQuality.Normal);
        ReplacementTextureAttestation.SetAdmittedVersionsForTests("0.9.0");
        ReplacementTextureAttestation.ReadInstalledPackageVersionOrNull =
            _ => "0.9.0";
        var captured = UnityAlphaFieldEvidence.TryCapture(
            copy, TextureChannel.Alpha, 1f, AlphaPolicyBounds.Inert,
            out _, out _, out var refusal);
        Assert.That(captured, Is.False);
        Assert.That(
            refusal, Is.EqualTo(TextureCaptureRefusalReason.UnsupportedFormat));
    }
    finally
    {
        UnityEngine.Object.DestroyImmediate(copy);
        ReplacementTextureAttestation.ResetForTests();
        ObjectRegistry.ActiveRegistry = previousRegistry;
        DeleteTempFolder();
    }
}
```

- [ ] **Step 5: Commit**

```bash
git add Packages/com.alrauna.amuse/Editor/Semantics/UnityTextureEvidence.cs \
  Packages/com.alrauna.amuse/Tests/Editor/Semantics/UnityTextureEvidenceTests.cs \
  Packages/com.alrauna.amuse/Tests/Editor/Host/UnityAlphaFieldEvidenceTests.cs
git commit -m "feat: extend color and no-alpha proofs to admitted replacement copies"
```

---

### Task 6: Report hint update and full validation

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/Build/AmuseReportStrings.cs`
  (`amuse.texture.UnavailableCapture:hint`, around lines 315-321)
- Test: `Tests/Editor/Build/AmuseReportStringsTests.cs` (the hint test at
  lines 187-188)

**Interfaces:**
- Consumes: the existing report-string keys. No key changes.

- [ ] **Step 1: Update the failing test first. The existing hint test
  asserts the Avatar Optimizer wording; extend it to require both
  producers:**

```csharp
Assert.That(hint, Does.Contain(
    "replaced the material texture with an in-memory copy"));
Assert.That(hint, Does.Contain("Trace and Optimize"));
Assert.That(hint, Does.Contain("LAC Texture Compressor"),
    "the hint names both known producers of in-memory copies");
```

- [ ] **Step 2: Refresh Unity and run the focused filter
  `AmuseReportStringsTests`. Expected: FAIL (the hint lacks the LAC
  sentence). Record it.**

- [ ] **Step 3: Update the hint string. New sentences, Simplified
  Technical English, appended after the existing Avatar Optimizer
  sentences:**

```
The LAC Texture Compressor component produces the same shape. A version
of that package joins the admitted set only after a dated
characterization, so a copy from an uncharacterized version still has no
proof here.
```

- [ ] **Step 4: Refresh Unity and run the focused filter. Expected: PASS.
  Record it.**

- [ ] **Step 5: Full validation. Refresh Unity, then run the complete
  `Alrauna.Amuse.Tests.Editor` assembly, then the
  `Alrauna.Amuse.Research.Tests.Editor` assembly. Expected: 0 failures in
  both. Record observed counts. Then run `git diff --check` and inspect
  the full diff for unintended changes.**

- [ ] **Step 6: Commit**

```bash
git add Packages/com.alrauna.amuse/Editor/Build/AmuseReportStrings.cs \
  Packages/com.alrauna.amuse/Tests/Editor/Build/AmuseReportStringsTests.cs
git commit -m "feat: name both in-memory-copy producers in the capture hint"
```

---

## Stop conditions

- The Step 1 residency probe of Task 4 shows a non-resident forced flag:
  pin the refusal reading, finish Task 4 with the refusal test, and stop
  the route-arrival work. Everything already merged stays fail-closed.
- Any falsifier cannot be made to fail for the named wrong
  implementation: stop and report. The plan's RED evidence is the point.
- The admission needs a second identity hop, a registry write, or a new
  refusal kind: stop. That is a spec change, and the spec's fail-closed
  contract forbids it.
- Any test in the full `Alrauna.Amuse.Tests.Editor` or research run
  regresses: stop and report the observed count before touching anything.

## Effect check (spec section 15)

After Task 4, a DXT1, DXT5, or BC7 replacement copy with a forced
streaming flag either captures through the generated route or refuses with
a named refusal. After Task 5, a no-alpha copy additionally proves sampled
alpha exactly one. The admitted set still ships empty, so no production
build changes behavior until a version joins the set through a dated
characterization; the tests prove the machinery, and the
characterization run is the follow-up that arms it.
