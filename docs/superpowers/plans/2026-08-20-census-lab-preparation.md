# Census Vendor Reachability Gate — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Prove, through AMUSE's production analysis path, that an installed vendor shader can actually reach `ProvenOpaque`, `MustRemainTransparent`, `MissingTextureEvidence`, and `SemanticsUnknown` — so that a future census aborts on an environment that cannot reach them instead of publishing a misleading near-total `SemanticsUnknown`.

**Architecture:** A probe locates installed attested vendor packages and compares their versions to AMUSE's pinned constants. A set of EditMode tests builds in-memory vendor materials and drives them through `RendererObservationBuilder.Build(renderer, path, families)` — the three-argument production overload, never the semantics seam. Where the vendor package is absent, the probe reports absence and the vendor cases assert nothing, so public CI stays green and honest. Everything lives in the research **test** assembly; no production assembly gains a line.

**Tech Stack:** Unity 2022.3.22f1, C#, NUnit EditMode tests, Unity Test Framework, `UnityEditor.PackageManager.PackageInfo`.

## Global Constraints

Copied from `docs/superpowers/specs/2026-08-20-census-lab-preparation-design.md`. Every task's requirements implicitly include these.

- **No production code changes.** Nothing under `Packages/com.alrauna.amuse/` and nothing under `Packages/com.alrauna.amuse.research/Editor/` is modified. All new code lands under `Packages/com.alrauna.amuse.research/Tests/Editor/`.
- **No attestation change**, including any relaxation for locked materials.
- **No new `InternalsVisibleTo` grant.** The two existing grants in `Packages/com.alrauna.amuse/Editor/AssemblyInfo.cs` already give the research test assembly access to `Alrauna.Amuse.Editor` internals. Use them; add none.
- **No public API promotion** in `com.alrauna.amuse`.
- **No new census category, field, or schema change.**
- **No reflection.** Vendor pins are `internal const` and reachable at compile time; name them directly. The probe references the constants rather than retyping them, so an adapter version bump moves the probe with it. A *test* may pin a literal deliberately — that is what makes a silent pin change visible.
- **The gate uses the production overload** `RendererObservationBuilder.Build(renderer, hierarchyPath, families)`. Never the four-argument `BaseMaterialSemanticsProvider` overload — that one proves counting, this gate proves reachability.
- **No fixture asset.** Every material and mesh is built in memory and destroyed in teardown. Nothing is imported, saved, or written to either project.
- **No persistent Lab mutation.** No saved scene, no generated shader, no imported asset, no project settings change. Never invoke Poiyomi's shader locker.
- **`Assert.Ignore` is forbidden** for vendor absence. Absence is a reported value that a case asserts on, never a skipped test.
- **No telemetry, networking, cloud reporting, avatar discovery, or persistent analytics store.**
- **Unity instance identity must be confirmed before every test run whose result is reported.** `Application.dataPath` must equal `<repo-root>/Assets` for the public project. Case-only matches are unconfirmed identity: stop and report.
- Pinned constants, for reference — never retype the literals, reference the constants:
  - `Alrauna.Amuse.Editor.Semantics.Poiyomi.PoiyomiMaterialSemantics.PoiyomiToonShaderName` = `.poiyomi/Poiyomi Toon`
  - `…PoiyomiMaterialSemantics.PoiyomiPackageName` = `com.poiyomi.toon`
  - `…PoiyomiMaterialSemantics.PoiyomiPackageVersion` = `9.3.64`
  - `Alrauna.Amuse.Editor.Semantics.LilToon.LilToonSourceAttestation.SupportedShaderName` = `lilToon`
  - `…LilToonSourceAttestation.PackageName` = `jp.lilxyzw.liltoon`
  - `…LilToonSourceAttestation.PackageVersion` = `2.3.4`

## Where this runs

Two environments, and the difference is the whole point.

| | Public development project | The Census Lab |
|---|---|---|
| Location | `<repo-root>` | Private, machine-local, never named in the repository |
| Vendor packages | None | Poiyomi 9.3.64, lilToon 2.3.4 |
| Operative cases | Case 6 (absence) | Cases 1–5 |
| Used in | Tasks 1–5 for compile and absence behavior | Task 6 only |

Tasks 1 through 5 are developed and run against the **public project**, where all vendor cases take their absence branch. Task 6 is the Lab run that produces the actual reachability evidence.

## File Structure

| File | Responsibility |
|---|---|
| Create `Packages/com.alrauna.amuse.research/Tests/Editor/Calibration/CensusVendorProbe.cs` | Locate installed attested vendor shaders and their package versions. Returns values; asserts nothing, throws nothing for absence. |
| Create `Packages/com.alrauna.amuse.research/Tests/Editor/Calibration/VendorReachabilityTests.cs` | The six gate cases. |
| Modify `Packages/com.alrauna.amuse.research/Tests/Editor/Collection/CollectorTestScene.cs` | Add one `NewMaterial(Shader, string)` helper. Reuse rather than duplicate the tracked-and-destroyed scene builder. |

Two files, one helper method. If a third production-side file appears necessary, that is a stop condition — see the spec's §9.

---

### Task 1: The vendor probe

**Files:**
- Create: `Packages/com.alrauna.amuse.research/Tests/Editor/Calibration/CensusVendorProbe.cs`
- Test: `Packages/com.alrauna.amuse.research/Tests/Editor/Calibration/VendorReachabilityTests.cs`

**Interfaces:**
- Consumes: `PoiyomiMaterialSemantics` and `LilToonSourceAttestation` internal constants, via the existing `InternalsVisibleTo` grant.
- Produces:
  - `internal enum CensusVendorFamily { Poiyomi, LilToon }`
  - `internal sealed class CensusVendorPresence` with `bool IsInstalled`, `Shader Shader`, `string ExpectedPackageName`, `string ExpectedPackageVersion`, `string InstalledPackageVersion`
  - `internal static CensusVendorPresence CensusVendorProbe.Probe(CensusVendorFamily family)`
  - `internal static IReadOnlyList<CensusVendorPresence> CensusVendorProbe.ProbeAll()`

- [ ] **Step 1: Write the failing test**

Create `VendorReachabilityTests.cs`:

```csharp
using System.Linq;
using Alrauna.Amuse.Research.Tests.Editor.Collection;
using NUnit.Framework;
using UnityEngine;

namespace Alrauna.Amuse.Research.Tests.Editor.Calibration
{
    /// <summary>
    /// Reachability, not counting. Every case here drives the production
    /// overload of RendererObservationBuilder - no semantics provider - so a
    /// pass means AMUSE reaches the outcome in a real project, not that the
    /// collector counts a substituted result correctly. CollectorSeamCountingTests
    /// makes the counting claim; conflating the two would let a census report
    /// near-total SemanticsUnknown and call it a pass.
    /// </summary>
    public sealed class VendorReachabilityTests
    {
        private CollectorTestScene _scene;

        [SetUp]
        public void SetUp() => _scene = new CollectorTestScene();

        [TearDown]
        public void TearDown() => _scene.Destroy();

        [Test]
        public void ProbeReportsBothAttestedFamilies()
        {
            var probed = CensusVendorProbe.ProbeAll();

            Assert.That(probed.Count, Is.EqualTo(2));
            Assert.That(
                probed.Select(p => p.ExpectedPackageName),
                Is.EquivalentTo(
                    new[] { "com.poiyomi.toon", "jp.lilxyzw.liltoon" }));
        }

        [Test]
        public void AnAbsentFamilyReportsAbsenceRatherThanThrowing()
        {
            foreach (var presence in CensusVendorProbe.ProbeAll())
            {
                if (!presence.IsInstalled)
                {
                    Assert.That(presence.Shader, Is.Null);
                    Assert.That(presence.InstalledPackageVersion, Is.Null);
                }
                else
                {
                    Assert.That(presence.Shader, Is.Not.Null);
                }
            }
        }
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Confirm Unity instance identity first, then run the EditMode suite filtered to `VendorReachabilityTests`.

Expected: **compile error**, `CensusVendorProbe` does not exist. A compile failure is the correct first failure here; do not proceed until you have seen it.

- [ ] **Step 3: Write the probe**

Create `CensusVendorProbe.cs`:

```csharp
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using LilToonPins = Alrauna.Amuse.Editor.Semantics.LilToon.LilToonSourceAttestation;
// UnityEditor.PackageManager.PackageInfo is ambiguous with UnityEngine.PackageInfo.
using PackageInfo = UnityEditor.PackageManager.PackageInfo;
using PoiyomiPins = Alrauna.Amuse.Editor.Semantics.Poiyomi.PoiyomiMaterialSemantics;

namespace Alrauna.Amuse.Research.Tests.Editor.Calibration
{
    internal enum CensusVendorFamily
    {
        Poiyomi,
        LilToon,
    }

    /// <summary>
    /// What the probe found, as a value. Absence is a reported state, never an
    /// exception and never a skipped test: a vendor package that has silently
    /// gone missing must fail a census, and Assert.Ignore would report that as
    /// a pass.
    /// </summary>
    internal sealed class CensusVendorPresence
    {
        internal CensusVendorFamily Family { get; }

        /// <summary>Null when the family is not installed.</summary>
        internal Shader Shader { get; }

        internal string ExpectedPackageName { get; }
        internal string ExpectedPackageVersion { get; }

        /// <summary>
        /// Null when the family is not installed, or when the shader resolves
        /// outside any package. Distinguished from a version mismatch on
        /// purpose: they are different findings.
        /// </summary>
        internal string InstalledPackageVersion { get; }

        internal bool IsInstalled => Shader != null;

        internal CensusVendorPresence(
            CensusVendorFamily family,
            Shader shader,
            string expectedPackageName,
            string expectedPackageVersion,
            string installedPackageVersion)
        {
            Family = family;
            Shader = shader;
            ExpectedPackageName = expectedPackageName;
            ExpectedPackageVersion = expectedPackageVersion;
            InstalledPackageVersion = installedPackageVersion;
        }
    }

    /// <summary>
    /// Locates the vendor shaders AMUSE attests, by the exact names AMUSE pins.
    /// <para>
    /// The pins are referenced as constants, never retyped as literals: a
    /// version bump in the adapter must move this probe with it, as a compile-
    /// time fact rather than a stale copy nobody notices.
    /// </para>
    /// </summary>
    internal static class CensusVendorProbe
    {
        internal static IReadOnlyList<CensusVendorPresence> ProbeAll()
        {
            return new[]
            {
                Probe(CensusVendorFamily.Poiyomi),
                Probe(CensusVendorFamily.LilToon),
            };
        }

        internal static CensusVendorPresence Probe(CensusVendorFamily family)
        {
            string shaderName;
            string packageName;
            string packageVersion;

            switch (family)
            {
                case CensusVendorFamily.Poiyomi:
                    shaderName = PoiyomiPins.PoiyomiToonShaderName;
                    packageName = PoiyomiPins.PoiyomiPackageName;
                    packageVersion = PoiyomiPins.PoiyomiPackageVersion;
                    break;
                case CensusVendorFamily.LilToon:
                    shaderName = LilToonPins.SupportedShaderName;
                    packageName = LilToonPins.PackageName;
                    packageVersion = LilToonPins.PackageVersion;
                    break;
                default:
                    // No default guess. A new family must be added here
                    // deliberately, not silently probed as nothing.
                    throw new System.ArgumentOutOfRangeException(
                        nameof(family));
            }

            var shader = Shader.Find(shaderName);
            return new CensusVendorPresence(
                family,
                shader,
                packageName,
                packageVersion,
                InstalledVersionOf(shader));
        }

        private static string InstalledVersionOf(Shader shader)
        {
            if (shader == null)
            {
                return null;
            }

            var assetPath = AssetDatabase.GetAssetPath(shader);
            if (string.IsNullOrEmpty(assetPath))
            {
                return null;
            }

            var package = PackageInfo.FindForAssetPath(assetPath);
            return package?.version;
        }
    }
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Expected in the public project: both tests PASS, with both families reporting `IsInstalled == false`.

- [ ] **Step 5: Commit**

```bash
git add Packages/com.alrauna.amuse.research/Tests/Editor/Calibration
git commit -m "test(research): probe for attested vendor shader installs"
```

---

### Task 2: The version pin case

**Files:**
- Modify: `Packages/com.alrauna.amuse.research/Tests/Editor/Calibration/VendorReachabilityTests.cs`

**Interfaces:**
- Consumes: `CensusVendorProbe.ProbeAll()`, `CensusVendorPresence` from Task 1.
- Produces: nothing new.

This is gate case 1. A VPM update in the Lab that moves Poiyomi off 9.3.64 turns every subsequent census into a measurement of the mismatch. It must fail loudly, here, rather than quietly downstream.

- [ ] **Step 1: Write the failing test**

Append to `VendorReachabilityTests.cs`:

```csharp
        /// <summary>
        /// Gate case 1. Attestation is exact-version: PoiyomiMaterialSemantics
        /// pins 9.3.64 and LilToonSourceAttestation pins 2.3.4, and a mismatch
        /// makes every material of that family unattested. An installed family
        /// at the wrong version is therefore a gate failure, not a census
        /// result.
        /// </summary>
        [Test]
        public void AnInstalledFamilyMatchesTheVersionAmuseAttests()
        {
            foreach (var presence in CensusVendorProbe.ProbeAll())
            {
                if (!presence.IsInstalled)
                {
                    continue;
                }

                Assert.That(
                    presence.InstalledPackageVersion,
                    Is.EqualTo(presence.ExpectedPackageVersion),
                    presence.ExpectedPackageName
                    + " is installed at a version AMUSE does not attest. "
                    + "Every material of this family will be unattested, and a "
                    + "census run would measure the mismatch rather than AMUSE.");
            }
        }
```

- [ ] **Step 2: Run it to verify it passes vacuously in the public project**

Expected: PASS. Both families are absent, so the loop body never runs.

A vacuous pass is not evidence. Prove the assertion is live before trusting it: temporarily change the `Probe` switch's Poiyomi arm to use shader name `"Standard"`, which resolves in any project, and re-run. Expected: the test now FAILS, naming `com.poiyomi.toon` and reporting a null installed version. **Revert the probe change immediately.**

- [ ] **Step 3: Commit**

```bash
git add Packages/com.alrauna.amuse.research/Tests/Editor/Calibration/VendorReachabilityTests.cs
git commit -m "test(research): fail the gate when an installed vendor version drifts off AMUSE's pin"
```

---

### Task 3: The scene helper for vendor materials

**Files:**
- Modify: `Packages/com.alrauna.amuse.research/Tests/Editor/Collection/CollectorTestScene.cs`

**Interfaces:**
- Consumes: the existing `CollectorTestScene` tracked-object list.
- Produces: `internal Material NewMaterial(Shader shader, string name)` — creates a tracked material destroyed in `Destroy()`.

`CollectorTestScene` already has `NewStandardMaterial()`, which hard-codes `Shader.Find("Standard")`. The gate needs the same tracking for an arbitrary vendor shader. One method, not a new helper class.

- [ ] **Step 1: Write the failing test**

Append to `VendorReachabilityTests.cs`:

```csharp
        [Test]
        public void TheSceneHelperTracksAndDestroysAnArbitraryShaderMaterial()
        {
            var material = _scene.NewMaterial(
                Shader.Find("Standard"), "CensusGateProbe");

            Assert.That(material, Is.Not.Null);
            Assert.That(material.name, Is.EqualTo("CensusGateProbe"));

            _scene.Destroy();

            // Unity's overloaded equality reports a destroyed object as null.
            Assert.That(material == null, Is.True);
        }
```

- [ ] **Step 2: Run it to verify it fails**

Expected: **compile error**, `CollectorTestScene` has no `NewMaterial` method.

- [ ] **Step 3: Add the helper**

In `CollectorTestScene.cs`, directly below `NewStandardMaterial()`:

```csharp
        /// <summary>
        /// A tracked material on a caller-supplied shader, for the gate cases,
        /// which need vendor shaders rather than Standard. Tracked and
        /// destroyed exactly as every other object here, so a gate run leaves
        /// no material behind in the Lab.
        /// </summary>
        internal Material NewMaterial(Shader shader, string name)
        {
            var material = new Material(shader) { name = name };
            _created.Add(material);
            return material;
        }
```

- [ ] **Step 4: Run the test to verify it passes**

Expected: PASS. Re-run the full EditMode suite as well — `CollectorTestScene` is shared with the collector tests and must not regress.

- [ ] **Step 5: Commit**

```bash
git add Packages/com.alrauna.amuse.research/Tests/Editor
git commit -m "test(research): track materials on an arbitrary shader in the scene helper"
```

---

### Task 4: The four reachability cases, written to characterize

**Files:**
- Modify: `Packages/com.alrauna.amuse.research/Tests/Editor/Calibration/VendorReachabilityTests.cs`

**Interfaces:**
- Consumes: `CensusVendorProbe`, `CollectorTestScene.NewMaterial`, `CollectorTestScene.NewTriangleMesh`, `CollectorTestScene.NewMeshRenderer`, `CollectorTestScene.NewRoot`, and `RendererObservationBuilder.Build(Renderer, string, CensusShaderFamily)`.
- Produces: nothing new.

These are gate cases 2 through 5. **Expected outcomes are recorded as observed in Task 6, not predicted here.** Write them now with the assertions stated; Task 6 either confirms them in the Lab or replaces them with what was actually observed, and reports the difference.

- [ ] **Step 1: Add the shared observation helper**

Append to `VendorReachabilityTests.cs`, inside the class:

```csharp
        /// <summary>
        /// One renderer, one submesh, one material, through the PRODUCTION
        /// overload. No semantics provider: that is the entire point of this
        /// file.
        /// </summary>
        private ObservedSubmesh ObserveSingleSubmesh(Material material)
        {
            var root = _scene.NewRoot("GateAvatar");
            var go = _scene.NewMeshRenderer(
                root, "GateMesh", _scene.NewTriangleMesh(1), material);

            var observed = RendererObservationBuilder.Build(
                go.GetComponent<MeshRenderer>(),
                "GateMesh",
                new CensusShaderFamily());

            Assert.That(
                observed.Refusal,
                Is.EqualTo(RendererRefusal.None),
                "The gate's own renderer was refused; the case cannot speak to "
                + "vendor reachability at all.");
            Assert.That(observed.Submeshes.Count, Is.EqualTo(1));
            return observed.Submeshes[0];
        }
```

Add these usings to the file:

```csharp
using Alrauna.Amuse.Research.Census;
using Alrauna.Amuse.Research.Collection;
```

- [ ] **Step 2: Write case 2 — `ProvenOpaque`**

```csharp
        /// <summary>
        /// Gate case 2. A default vendor material with opaque colour alpha and
        /// no alpha texture is the simplest thing that should prove opaque. If
        /// this cannot pass, no census result is meaningful, because the
        /// success path is unreachable in the environment being measured.
        /// </summary>
        [Test]
        public void AnOpaqueVendorMaterialReachesProvenOpaque()
        {
            foreach (var presence in CensusVendorProbe.ProbeAll())
            {
                if (!presence.IsInstalled)
                {
                    continue;
                }

                var material = _scene.NewMaterial(
                    presence.Shader, "GateOpaque_" + presence.Family);
                material.SetColor("_Color", new Color(1f, 1f, 1f, 1f));
                material.SetTexture("_MainTex", null);

                var submesh = ObserveSingleSubmesh(material);

                Assert.That(
                    submesh.AlphaFailure,
                    Is.EqualTo(AlphaResolutionFailure.None),
                    presence.Family + " failed alpha resolution on a default "
                    + "opaque material.");
                Assert.That(
                    submesh.ProvenOpaqueTriangleCount,
                    Is.EqualTo(submesh.TriangleCount),
                    presence.Family + " did not prove a fully opaque material "
                    + "opaque.");
            }
        }
```

- [ ] **Step 3: Write case 4 — `MustRemainTransparent`**

Written before case 3 because it needs no texture at all.

```csharp
        /// <summary>
        /// Gate case 4. AlphaSemanticsResolver classifies a CONSTANT alpha
        /// below one as MustRemainTransparent outright - no texture is sampled
        /// and no importer is consulted - so colour alpha alone reaches the
        /// transparent path.
        /// </summary>
        [Test]
        public void AVendorMaterialWithSubUnitAlphaMustRemainTransparent()
        {
            foreach (var presence in CensusVendorProbe.ProbeAll())
            {
                if (!presence.IsInstalled)
                {
                    continue;
                }

                var material = _scene.NewMaterial(
                    presence.Shader, "GateTransparent_" + presence.Family);
                material.SetColor("_Color", new Color(1f, 1f, 1f, 0.5f));
                material.SetTexture("_MainTex", null);

                var submesh = ObserveSingleSubmesh(material);

                Assert.That(
                    submesh.AlphaFailure,
                    Is.EqualTo(AlphaResolutionFailure.None),
                    presence.Family + " failed alpha resolution on a constant "
                    + "sub-unit alpha.");
                Assert.That(
                    submesh.MustRemainTransparentTriangleCount,
                    Is.EqualTo(submesh.TriangleCount),
                    presence.Family + " did not preserve a half-alpha material "
                    + "as transparent.");
            }
        }
```

- [ ] **Step 4: Write case 3 — `MissingTextureEvidence`**

```csharp
        /// <summary>
        /// Gate case 3. A runtime Texture2D is not a project asset, so
        /// AssetDatabase.GetAssetPath returns empty, no TextureImporter exists,
        /// and UnityTextureEvidence can prove nothing about it. This must
        /// surface as MissingTextureEvidence and NOT as SemanticsUnknown:
        /// "understood shader, unseen texture" and "unknown shader" imply
        /// completely different next steps for AMUSE.
        /// </summary>
        [Test]
        public void AVendorMaterialSamplingANonAssetTextureReportsMissingEvidence()
        {
            foreach (var presence in CensusVendorProbe.ProbeAll())
            {
                if (!presence.IsInstalled)
                {
                    continue;
                }

                var texture = new Texture2D(4, 4) { name = "GateRuntimeTexture" };
                try
                {
                    var material = _scene.NewMaterial(
                        presence.Shader, "GateMissing_" + presence.Family);
                    material.SetColor("_Color", new Color(1f, 1f, 1f, 1f));
                    material.SetTexture("_MainTex", texture);

                    var submesh = ObserveSingleSubmesh(material);

                    Assert.That(
                        submesh.AlphaFailure,
                        Is.EqualTo(
                            AlphaResolutionFailure.MissingTextureEvidence),
                        presence.Family + " did not distinguish an unseeable "
                        + "texture from an unknown shader.");
                }
                finally
                {
                    Object.DestroyImmediate(texture);
                }
            }
        }
```

The texture is created and destroyed inline rather than tracked by `CollectorTestScene`, because it must never be an asset and the helper's contract is scene objects. `Object` here is `UnityEngine.Object`.

- [ ] **Step 5: Write case 5 — the locked-material characterization**

```csharp
        /// <summary>
        /// Gate case 5. Poiyomi rejects locked materials before any source
        /// check, and the lock is read from the material property rather than
        /// from the shader, so setting the float reproduces the rejection
        /// without generating a shader or writing anything. THE LOCKER IS NEVER
        /// RUN.
        /// <para>
        /// This characterizes existing behaviour and implements no support for
        /// locked materials. It is the evidence behind the deferred
        /// investigation in the design's section 6: the census cannot currently
        /// distinguish an unknown shader family from a supported-but-locked
        /// vendor material, and a future reader of a census must know that.
        /// </para>
        /// </summary>
        [Test]
        public void ALockedPoiyomiMaterialIsUnattestedAndReportsSemanticsUnknown()
        {
            var presence = CensusVendorProbe.Probe(CensusVendorFamily.Poiyomi);
            if (!presence.IsInstalled)
            {
                Assert.That(presence.Shader, Is.Null);
                return;
            }

            var material = _scene.NewMaterial(presence.Shader, "GateLocked");
            material.SetColor("_Color", new Color(1f, 1f, 1f, 1f));
            material.SetTexture("_MainTex", null);
            material.SetFloat("_ShaderOptimizerEnabled", 1f);

            var submesh = ObserveSingleSubmesh(material);

            Assert.That(
                submesh.AlphaFailure,
                Is.EqualTo(AlphaResolutionFailure.SemanticsUnknown),
                "A locked Poiyomi material was expected to be unattested. If "
                + "this fails, the deferred investigation in the design's "
                + "section 6 needs revisiting, not this test.");
        }
```

- [ ] **Step 6: Run the whole file in the public project**

Expected: every case PASSES vacuously — both families absent, so each loop body is skipped and the locked case takes its `IsInstalled == false` branch. Confirm there are no compile errors and no Console errors matching `Alrauna`.

- [ ] **Step 7: Commit**

```bash
git add Packages/com.alrauna.amuse.research/Tests/Editor/Calibration/VendorReachabilityTests.cs
git commit -m "test(research): assert vendor reachability through the production path"
```

---

### Task 5: Prove case 6 — absence is honest, not silent

**Files:**
- Modify: `Packages/com.alrauna.amuse.research/Tests/Editor/Calibration/VendorReachabilityTests.cs`

**Interfaces:**
- Consumes: everything from Tasks 1–4.
- Produces: nothing new.

Cases 2–5 pass vacuously in the public project. That is correct behavior, and it is also indistinguishable from a broken suite. This task makes the vacuity itself observable so it can never be mistaken for evidence.

- [ ] **Step 1: Add the vacuity report**

This case cannot fail by construction — it reports rather than asserts. That is deliberate, and it is why it is a separate case rather than an assertion folded into another test.

```csharp
        /// <summary>
        /// Gate case 6. In a project with no vendor package, the vendor cases
        /// above assert nothing - correct, and dangerous if unnoticed. This
        /// records which families were actually exercised, so a vacuous run is
        /// visible as vacuous rather than reading as a pass.
        /// <para>
        /// The name is deliberately a warning rather than a description: this
        /// test's row is the only place in a green CI list where the difference
        /// between "the gate passed" and "the gate proved something" is
        /// visible, and a reader scanning names must not be able to miss it.
        /// </para>
        /// <para>
        /// Assert.Ignore is deliberately not used anywhere in this file: an
        /// ignored test in the Lab, where a vendor package might genuinely have
        /// gone missing, reports a pass-shaped result for a condition that must
        /// abort a census.
        /// </para>
        /// </summary>
        [Test]
        public void AGreenRunProvesNothingUnlessThisNamesAnInstalledVendorFamily()
        {
            var installed = CensusVendorProbe.ProbeAll()
                .Where(p => p.IsInstalled)
                .Select(p => p.ExpectedPackageName + " " + p.InstalledPackageVersion)
                .ToList();

            if (installed.Count == 0)
            {
                Assert.Pass(
                    "VENDOR REACHABILITY NOT PROVEN - no attested vendor family "
                    + "is installed. This is the EXPECTED state in the public "
                    + "development project, which installs no vendor shader. "
                    + "Every vendor case in this file asserted nothing. A green "
                    + "run here says only that the gate compiles; it does not "
                    + "establish that AMUSE reaches ProvenOpaque. A census run "
                    + "in this environment must abort rather than report.");
            }

            Assert.Pass(
                "VENDOR REACHABILITY EXERCISED for: "
                + string.Join(", ", installed));
        }
```

- [ ] **Step 2: Run it in the public project**

Expected: PASS, with the message beginning `VENDOR REACHABILITY NOT PROVEN` and naming zero installed families. **Read the message in the test output.** If it names a family, the public project has acquired a vendor package and that is itself a finding to report.

- [ ] **Step 3: Run the complete EditMode suite**

Confirm Unity instance identity, then run everything. Expected: the pre-existing 802 tests still pass, plus the tests added by Tasks 1–5, with zero failures and zero Console entries matching `Alrauna`.

Record the exact before/after counts. A count that does not increase by the number of tests added means tests are not being discovered — investigate before continuing.

- [ ] **Step 4: Commit**

```bash
git add Packages/com.alrauna.amuse.research/Tests/Editor/Calibration/VendorReachabilityTests.cs
git commit -m "test(research): make a vendorless gate run visibly vacuous"
```

---

### Task 6: Run the gate in the Census Lab

**Files:**
- Modify (Lab-side, **not** in this repository): the Lab's `Packages/manifest.json`
- Modify, only if Task 6 observes different outcomes: `Packages/com.alrauna.amuse.research/Tests/Editor/Calibration/VendorReachabilityTests.cs`
- Create: `docs/superpowers/plans/2026-08-20-census-lab-preparation-results.md`

**Interfaces:**
- Consumes: the complete gate from Tasks 1–5.
- Produces: the observed reachability record — the first actual evidence that AMUSE's production path reaches its success outcomes.

This is the only task that touches the Lab. **Everything before it was preparation; this is the measurement.**

- [ ] **Step 1: Add the research package reference to the Lab**

The Lab currently references `com.alrauna.amuse` but **not** `com.alrauna.amuse.research`, so the gate's code is not loaded there at all.

In the Lab's `Packages/manifest.json`, add a second `file:` dependency pointing at `Packages/com.alrauna.amuse.research` in the same working tree the existing `com.alrauna.amuse` entry points at. **Derive that path on this machine from the existing entry; never copy a path from a document.**

Also add a `testables` entry, so the Test Framework discovers tests in a non-embedded local package:

```json
"testables": [
  "com.alrauna.amuse",
  "com.alrauna.amuse.research"
]
```

`testables` is a sibling of `dependencies`, not a member of it.

This is a Lab configuration change, not a repository change. It is permitted, minimal, and reversible: it adds a package reference and changes no asset, scene, prefab, material, or project setting.

- [ ] **Step 2: Confirm which Unity instance you are talking to**

Before any run whose result you will report, enumerate the reachable Unity instances read-only and check `Application.dataPath`.

For this task you want the **Lab**, identified by elimination: its data path is **not** `<repo-root>/Assets`. Derive `<repo-root>` from `git rev-parse --show-toplevel`, normalize both sides — resolve relative and symbolic segments, unify separators to `/`, drop any trailing separator — and compare exactly. A match that differs only by letter case is unconfirmed identity: **stop and report** rather than guessing.

If the Lab instance is not reachable, stop. Do not substitute the public project.

- [ ] **Step 3: Run the gate in the Lab and record what you observe**

Run only `VendorReachabilityTests`.

Record, verbatim, for each test: pass or fail, and the message. In particular record what `TheGateReportsWhichFamiliesItActuallyExercised` names — it should name both `com.poiyomi.toon 9.3.64` and `jp.lilxyzw.liltoon 2.3.4`.

**Do not fix a failure by weakening an assertion.** The expected outcomes in Task 4 were stated in advance and are unproven predictions about vendor default property state. A mismatch is a finding.

- [ ] **Step 4: Reconcile predictions with observations**

For each case that failed, decide which of these it is, and say so explicitly:

| Observation | Meaning | Action |
|---|---|---|
| Outcome differs but is well-defined, e.g. a default Poiyomi material reports `SemanticsUnknown` for a reason other than locking | The prediction was wrong about vendor defaults | Correct the assertion to what was observed, and document why in the test's doc-comment |
| The renderer was refused outright | The gate's own fixture is wrong | Fix the fixture |
| `ProvenOpaque` is unreachable for **both** families | **The census cannot proceed.** Stop and report; do not adjust assertions to make the suite green | Stop condition |
| A version mismatch is reported | The Lab drifted off AMUSE's pins | Stop and report. Do not update AMUSE's pins to match the Lab |

- [ ] **Step 5: Verify the Lab was not mutated**

Confirm, and state as observed rather than assumed:

- no scene was saved;
- no asset was created, imported, or deleted — in particular no generated Poiyomi shader, which would mean the locker ran;
- no project setting changed;
- the only Lab change is Step 1's `manifest.json` edit;
- `Packages/packages-lock.json` in the Lab may have changed as a consequence of Step 1. That is expected. It is a Lab file and is not committed anywhere.

- [ ] **Step 6: Write the results record**

Create `docs/superpowers/plans/2026-08-20-census-lab-preparation-results.md` containing:

- the observed result of every gate case, per vendor family, recorded as observed **before** any assertion is reconciled;
- the installed vendor versions as reported by the probe;
- the full EditMode suite counts before and after, from both projects;
- every prediction that was wrong, and what replaced it;
- whether `ProvenOpaque` is reachable — the single number that decides whether a census may ever run;
- confirmation that the Lab was not mutated beyond the manifest reference;
- what the locked-material case actually showed, as evidence for the design's deferred investigation.

- [ ] **Step 7: Commit**

```bash
git add docs/superpowers/plans/2026-08-20-census-lab-preparation-results.md Packages/com.alrauna.amuse.research/Tests/Editor/Calibration/VendorReachabilityTests.cs
git commit -m "test(research): record observed vendor reachability from the Census Lab"
```

---

## Completion checklist

Before claiming this plan complete:

- [ ] `git status` shows only intended files; no Unity host-toolchain churn in `Packages/manifest.json` or `Packages/packages-lock.json`. If those two files contain **nothing but** generated entries, restore them with `git checkout HEAD -- Packages/manifest.json Packages/packages-lock.json` and report it. Never run that restore if anything else changed in either file.
- [ ] Unstaged and staged diffs inspected separately.
- [ ] Nothing under `Packages/com.alrauna.amuse/` changed.
- [ ] Nothing under `Packages/com.alrauna.amuse.research/Editor/` changed.
- [ ] No new `InternalsVisibleTo` grant.
- [ ] The full EditMode suite ran in the public project and its result was observed, not inferred.
- [ ] The gate ran in the Lab and its result was observed.
- [ ] The Lab was not mutated beyond the `manifest.json` package reference.
- [ ] The results record states what was skipped and why, and names remaining risks.
