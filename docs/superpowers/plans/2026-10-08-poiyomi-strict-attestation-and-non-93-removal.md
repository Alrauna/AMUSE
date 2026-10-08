# Poiyomi Strict Attestation and Non-9.3 Removal Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Strip out all artificial support and version transfer fallbacks for Poiyomi shader versions other than 9.3.64, enforcing strict attestation across the AMUSE pipeline.

**Architecture:** Poiyomi is excluded from the D8 version transfer consent layer. The legacy 9.0 shader name is removed from semantic classification. Alpha analysis and opaque conversion strictly enforce verified source identity. Dead original-shader transfer plumbing in locked material handling is deleted.

**Tech Stack:** C# (Unity 2022.3), Unity Test Framework (EditMode), NDMF.

**Spec:** `docs/superpowers/specs/2026-10-08-poiyomi-strict-attestation-and-non-93-removal-design.md`

## Global Constraints

- Use simple English. Short active sentences. One idea per sentence.
- No semicolons in prose.
- No contractions.
- Never record absolute or machine-specific paths. Use repository-relative paths only.
- Never record host names, user account names, home-directory paths, ports, or Unity MCP instance names.
- Date every status claim. As of 2026-10-08.
- Never stage, commit, push, or modify Git history without explicit user authorization.
- Never run tests or execute MCP operations against Census Lab. The dev editor instance with `Application.dataPath == <repo-root>/Assets` is the only authorized test instance.

---

### Task 1: Remove Legacy 9.0 Constants and Semantic Classification

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/Semantics/Poiyomi/PoiyomiMaterialSemantics.cs:48-61`
- Modify: `Packages/com.alrauna.amuse/Editor/Semantics/UnityMaterialSemantics.cs:691-706`
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Semantics/UnityMaterialSemanticsTests.cs:879-904`

**Interfaces:**
- Consumes: `UnityMaterialSemantics.ClassifyShaderName(string shaderName)`
- Produces: `CapturedAlphaMaterialFamily.Unsupported` for `.poiyomi/Old Versions/9.0/Poiyomi Toon`

- [ ] **Step 1: Write the failing test**

In `Packages/com.alrauna.amuse/Tests/Editor/Semantics/UnityMaterialSemanticsTests.cs`, update `Legacy90ShaderName_SelectsThePoiyomiFamily`:

```csharp
        [Test]
        public void Legacy90ShaderName_SelectsUnsupportedFamily()
        {
            const string legacy90Name = ".poiyomi/Old Versions/9.0/Poiyomi Toon";
            using var scope = new TestShaderScope();
            var shader = scope.WriteShader(
                "legacy90-poiyomi.shader",
                legacy90Name,
                PoiyomiProperties());

            var selected = UnityMaterialSemantics
                .TrySelectAlphaMaterialRequests(new[] { shader });

            Assert.That(selected, Is.False);
        }
```

- [ ] **Step 2: Run test to verify it fails**

Run test in Unity Test Runner: `UnityMaterialSemanticsTests.Legacy90ShaderName_SelectsUnsupportedFamily`
Expected result: FAIL because `ClassifyShaderName` still classifies `.poiyomi/Old Versions/9.0/Poiyomi Toon` as `CapturedAlphaMaterialFamily.Poiyomi`.

- [ ] **Step 3: Write minimal implementation**

1. In `Packages/com.alrauna.amuse/Editor/Semantics/Poiyomi/PoiyomiMaterialSemantics.cs`, delete lines 48-61:
```csharp
        // The vendor's own old-version copy of the 9.0 shader, shipped inside
        // the current package under "Old Versions/9.0". ...
        internal const string PoiyomiLegacy90ShaderName =
            ".poiyomi/Old Versions/9.0/Poiyomi Toon";
```

2. In `Packages/com.alrauna.amuse/Editor/Semantics/UnityMaterialSemantics.cs`, delete lines 691-706:
```csharp
            if (string.Equals(
                    shaderName,
                    PoiyomiMaterialSemantics.PoiyomiLegacy90ShaderName,
                    StringComparison.Ordinal))
            {
                return (
                    CapturedAlphaMaterialFamily.Poiyomi,
                    PoiyomiMaterialSemantics.AlphaEvidenceRequest);
            }
```

- [ ] **Step 4: Run test to verify it passes**

Run test in Unity Test Runner: `UnityMaterialSemanticsTests.Legacy90ShaderName_SelectsUnsupportedFamily`
Expected result: PASS.

- [ ] **Step 5: Verify git status (commit on authorization)**

Inspect diff:
```bash
git diff Packages/com.alrauna.amuse/Editor/Semantics/Poiyomi/PoiyomiMaterialSemantics.cs Packages/com.alrauna.amuse/Editor/Semantics/UnityMaterialSemantics.cs Packages/com.alrauna.amuse/Tests/Editor/Semantics/UnityMaterialSemanticsTests.cs
```

---

### Task 2: Exclude Poiyomi from Version Transfer Pre-Scan & Harden Alpha Analysis

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/Semantics/UnityMaterialSemantics.cs:481-486,1013-1040`
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Semantics/UnityMaterialSemanticsTests.cs:906-930`

**Interfaces:**
- Consumes: `UnityMaterialSemantics.CollectTransferConsent(IEnumerable<Material> materials)`
- Produces: Empty consent subjects and granted names for any Poiyomi material.
- Produces: Strict attestation verification in `AnalyzeAlphaMaterialCore` for Poiyomi families.

- [ ] **Step 1: Write the failing tests**

In `Packages/com.alrauna.amuse/Tests/Editor/Semantics/UnityMaterialSemanticsTests.cs`, update `Legacy90Material_ProducesTheTransferConsentSubject` and add `UnverifiedPoiyomi_ProducesZeroTransferConsentSubjects`:

```csharp
        [Test]
        public void Legacy90Material_ProducesZeroTransferConsentSubjects()
        {
            const string legacy90Name = ".poiyomi/Old Versions/9.0/Poiyomi Toon";
            var material = NewMaterial(
                "legacy90-consent.shader",
                legacy90Name,
                PoiyomiProperties());

            var consent = UnityMaterialSemantics.CollectTransferConsent(
                new[] { material });

            Assert.That(consent.Subjects.Count, Is.EqualTo(0));
            Assert.That(consent.GrantedShaderNames.Count, Is.EqualTo(0));
        }

        [Test]
        public void UnverifiedPoiyomi_ProducesZeroTransferConsentSubjects()
        {
            var material = NewMaterial(
                "unverified-poiyomi-consent.shader",
                PoiyomiMaterialSemantics.PoiyomiToonShaderName,
                PoiyomiProperties());

            var consent = UnityMaterialSemantics.CollectTransferConsent(
                new[] { material });

            Assert.That(consent.Subjects.Count, Is.EqualTo(0));
            Assert.That(consent.GrantedShaderNames.Count, Is.EqualTo(0));
        }
```

- [ ] **Step 2: Run tests to verify failure**

Run tests in Unity Test Runner: `UnityMaterialSemanticsTests.Legacy90Material_ProducesZeroTransferConsentSubjects` and `UnverifiedPoiyomi_ProducesZeroTransferConsentSubjects`
Expected result: FAIL because unverified Poiyomi currently produces a transfer consent subject.

- [ ] **Step 3: Write minimal implementation**

1. In `Packages/com.alrauna.amuse/Editor/Semantics/UnityMaterialSemantics.cs`, update `CollectTransferConsent` lines 481-486:
```csharp
                var family = IdentifyFamily(material);
                if (family == CapturedAlphaMaterialFamily.Unsupported ||
                    family == CapturedAlphaMaterialFamily.Poiyomi ||
                    family == CapturedAlphaMaterialFamily.PoiyomiTwoPass)
                {
                    continue;
                }
```

2. In `Packages/com.alrauna.amuse/Editor/Semantics/UnityMaterialSemantics.cs`, update `AnalyzeAlphaMaterialCore` lines 1014-1040:
```csharp
                case CapturedAlphaMaterialFamily.Poiyomi:
                    if (!PoiyomiMaterialSemantics.TryVerifyPoiyomiIdentity(
                            captured.PoiyomiEvidence, out _))
                    {
                        return UnknownWithShaderReason(
                            AlphaUnknownKind.UnattestedShader,
                            captured.ShaderName);
                    }

                    alpha = PoiyomiMaterialSemantics.InterpretVerifiedAlpha(
                        captured.Evidence, out unknownReason);
                    break;
                case CapturedAlphaMaterialFamily.PoiyomiTwoPass:
                    if (!PoiyomiMaterialSemantics.TryVerifyPoiyomiIdentity(
                            captured.PoiyomiEvidence, out _))
                    {
                        return UnknownWithShaderReason(
                            AlphaUnknownKind.UnattestedShader,
                            captured.ShaderName);
                    }

                    alpha = PoiyomiMaterialSemantics.InterpretVerifiedTwoPassAlpha(
                        captured.Evidence, out unknownReason);
                    break;
```

- [ ] **Step 4: Run tests to verify they pass**

Run tests in Unity Test Runner: `UnityMaterialSemanticsTests.Legacy90Material_ProducesZeroTransferConsentSubjects` and `UnverifiedPoiyomi_ProducesZeroTransferConsentSubjects`
Expected result: PASS.

- [ ] **Step 5: Verify git status (commit on authorization)**

Inspect diff:
```bash
git diff Packages/com.alrauna.amuse/Editor/Semantics/UnityMaterialSemantics.cs Packages/com.alrauna.amuse/Tests/Editor/Semantics/UnityMaterialSemanticsTests.cs
```

---

### Task 3: Enforce Strict Attestation in Opaque Conversion

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/Build/AlphaSeparationPreparation.cs:878-888`
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Build/AlphaSeparationPreparationTests.cs`

**Interfaces:**
- Consumes: `AlphaSeparationPreparation.ConvertAdmittedMaterial`
- Produces: `AlphaSeparationSlotRefusal.OpaqueConversionRefused` for unverified Poiyomi regardless of `grantedShaderNames`.

- [ ] **Step 1: Write the failing test**

In `Packages/com.alrauna.amuse/Tests/Editor/Build/AlphaSeparationPreparationTests.cs`, add `UnverifiedPoiyomi_RefusesConversionEvenWhenShaderNameGranted`:

```csharp
        [Test]
        public void UnverifiedPoiyomi_RefusesConversionEvenWhenShaderNameGranted()
        {
            var material = NewMaterial(
                "unverified-poi-granted.shader",
                PoiyomiMaterialSemantics.PoiyomiToonShaderName,
                PoiyomiProperties());
            var captured = CaptureAdmittedPoiyomiMaterial(material);
            var granted = new[] { PoiyomiMaterialSemantics.PoiyomiToonShaderName };

            var refusal = ConvertAdmittedMaterialDirect(
                material,
                captured,
                grantedShaderNames: granted,
                out var opaque,
                out var detail);

            Assert.That(refusal, Is.EqualTo(AlphaSeparationSlotRefusal.OpaqueConversionRefused));
            Assert.That(detail, Does.StartWith("SourceIdentity."));
            Assert.That(opaque, Is.Null);
        }
```

- [ ] **Step 2: Run test to verify it fails**

Run test in Unity Test Runner: `AlphaSeparationPreparationTests.UnverifiedPoiyomi_RefusesConversionEvenWhenShaderNameGranted`
Expected result: FAIL because `IsGrantedShader` currently bypasses identity verification for Poiyomi.

- [ ] **Step 3: Write minimal implementation**

In `Packages/com.alrauna.amuse/Editor/Build/AlphaSeparationPreparation.cs`, update lines 877-888:
```csharp
                        var sourceEvidence =
                            PoiyomiOpaqueConversion
                                .GatherConversionSourceEvidence(
                                    live.shader, derived);
                        if (!PoiyomiMaterialSemantics
                                .TryVerifyPoiyomiIdentity(
                                    sourceEvidence,
                                    out var poiIdentityDiagnostic))
                        {
                            refusedDetail =
                                "SourceIdentity." +
                                poiIdentityDiagnostic.Code;
                            return AlphaSeparationSlotRefusal
                                .OpaqueConversionRefused;
                        }
```

- [ ] **Step 4: Run test to verify it passes**

Run test in Unity Test Runner: `AlphaSeparationPreparationTests.UnverifiedPoiyomi_RefusesConversionEvenWhenShaderNameGranted`
Expected result: PASS.

- [ ] **Step 5: Verify git status (commit on authorization)**

Inspect diff:
```bash
git diff Packages/com.alrauna.amuse/Editor/Build/AlphaSeparationPreparation.cs Packages/com.alrauna.amuse/Tests/Editor/Build/AlphaSeparationPreparationTests.cs
```

---

### Task 4: Delete Dead Locked Original Shader Transfer Plumbing

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/Build/LockedMaterialIdentity.cs:202-252`
- Modify: `Packages/com.alrauna.amuse/Editor/Build/AmusePlatformFinishPlugin.cs:496-509`
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Build/LockedMaterialIdentityTests.cs:107-250`

**Interfaces:**
- Consumes: `LockedMaterialIdentity.OriginalShaderAttested(Material live)`
- Produces: Deletion of `GrantedAwareOriginalAttestation` and `RecordedOriginalShaderName`.

- [ ] **Step 1: Write the updated test and remove obsolete tests**

In `Packages/com.alrauna.amuse/Tests/Editor/Build/LockedMaterialIdentityTests.cs`:
1. Remove `GrantedOriginalName` constant.
2. Remove test methods:
   - `GrantedAwareOriginalAttestation_NullOrEmptyGrantPreservesBase`
   - `GrantedAwareOriginalAttestation_GrantAdmittedWhenOriginalMatches`
   - `GrantedAwareOriginalAttestation_BaseAttestationRunsFirst`
   - `GrantedAwareOriginalAttestation_UnlocksWindowWhenGranted`
3. Add `LockedPoiyomi_UnverifiedOriginalShaderAlwaysRefuses`:

```csharp
        [Test]
        public void LockedPoiyomi_UnverifiedOriginalShaderAlwaysRefuses()
        {
            var material = Track(new Material(Shader.Find("Unlit/Color")));
            material.shader = Shader.Find("Unlit/Color");
            material.SetFloat(
                LockedMaterialIdentity.OptimizerEnabledPropertyName, 1f);
            material.SetOverrideTag(
                LockedMaterialIdentity.OriginalShaderTagName,
                ".poiyomi/Old Versions/9.0/Poiyomi Toon");
            material.SetOverrideTag(
                LockedMaterialIdentity.OriginalShaderGuidTagName,
                "0123456789abcdef0123456789abcdef");

            var attests = LockedMaterialIdentity.OriginalShaderAttested(material);
            var refusal = LockedMaterialIdentity.PreCheckRefusal(material);

            Assert.That(attests, Is.False);
            Assert.That(
                refusal,
                Is.EqualTo(RendererAnalysisRefusal.LockedPoiyomiOriginalShaderUnattested));
        }
```

- [ ] **Step 2: Delete dead code in `LockedMaterialIdentity.cs` and `AmusePlatformFinishPlugin.cs`**

1. In `Packages/com.alrauna.amuse/Editor/Build/LockedMaterialIdentity.cs`, delete lines 202-252 (`RecordedOriginalShaderName` and `GrantedAwareOriginalAttestation`).
2. In `Packages/com.alrauna.amuse/Editor/Build/AmusePlatformFinishPlugin.cs`, update lines 496-509:
```csharp
            // The unlock window no longer carries its own per-build
            // consent subject. V2 and V4 passed live observation on
            // 2026-09-21, so per spec section 12 the gate moved to the
            // D8 pattern: an eligible build opens the window without
            // asking. Eligibility is the attestation gate alone: a
            // locked material whose original shader passes attestation
            // opens the window.
            var windowEligible =
                TransientUnlockAvailability.WindowEligibleForConsent(
                    avatarRoot,
                    lockedOriginalAttestation);
```

- [ ] **Step 3: Run test to verify it passes**

Run test in Unity Test Runner: `LockedMaterialIdentityTests.LockedPoiyomi_UnverifiedOriginalShaderAlwaysRefuses`
Expected result: PASS.

- [ ] **Step 4: Verify git status (commit on authorization)**

Inspect diff:
```bash
git diff Packages/com.alrauna.amuse/Editor/Build/LockedMaterialIdentity.cs Packages/com.alrauna.amuse/Editor/Build/AmusePlatformFinishPlugin.cs Packages/com.alrauna.amuse/Tests/Editor/Build/LockedMaterialIdentityTests.cs
```

---

### Task 5: Clean Up Transfer Consent Integration Tests

**Files:**
- Modify: `Packages/com.alrauna.amuse/Tests/Editor/Build/AmusePlatformFinishPluginTests.cs:428-575`
- Modify: `Packages/com.alrauna.amuse/Tests/Editor/Build/VersionConsentTests.cs:238-345`

**Interfaces:**
- Consumes: `LilToonSourceAttestation.CutoutShaderName`
- Produces: Clean integration tests verifying transfer consent with admitted transfer shaders (lilToon).
- Produces: Regression assertions verifying unverified Poiyomi produces no consent subjects.

- [ ] **Step 1: Update `VersionConsentTests.cs` to use lilToon cutout stand-in**

In `Packages/com.alrauna.amuse/Tests/Editor/Build/VersionConsentTests.cs`:
Replace occurrences of `PoiyomiMaterialSemantics.PoiyomiLegacy90ShaderName` with `LilToonSourceAttestation.CutoutShaderName`.
Update `StandInShaderText` to define lilToon cutout properties:
```csharp
        private static string StandInShaderText(string shaderName)
        {
            return "Shader \"" + shaderName + "\"\n" +
                "{\n    Properties\n    {\n" +
                "        _MainTex (\"Main\", 2D) = \"white\" {}\n" +
                "        _Color (\"Color\", Color) = (1,1,1,1)\n" +
                "        _Cutoff (\"Cutoff\", Range(0, 1)) = 0.5\n" +
                "    }\n    SubShader { Pass {} }\n}\n";
        }
```

- [ ] **Step 2: Update `AmusePlatformFinishPluginTests.cs` transfer consent tests**

In `Packages/com.alrauna.amuse/Tests/Editor/Build/AmusePlatformFinishPluginTests.cs`:
In `D8TransferConsent_AssignedLegacy90AndTwoPass_GrantsBothAndPermitsBoth` and `D8TransferConsent_DeclinedLegacy90AndTwoPass_RefusesAndPerformsNoMutations`:
Rename and update to use `LilToonSourceAttestation.CutoutShaderName` and `LilToonSourceAttestation.TransparentShaderName`.
Add regression test `PoiyomiMaterials_NeverProduceTransferConsentSubjects`:

```csharp
        [Test]
        public void PoiyomiMaterials_NeverProduceTransferConsentSubjects()
        {
            var legacyShader = WriteSwapConsentStandIn(
                "swap-unsupported-poi.shader",
                ".poiyomi/Old Versions/9.0/Poiyomi Toon",
                PoiyomiStandInProperties());
            var assigned = new Material(legacyShader);
            var mesh = TriangleMesh();
            var root = new GameObject("AMUSE unverified poi consent fixture");
            var renderer = root.AddComponent<SkinnedMeshRenderer>();
            renderer.sharedMesh = mesh;
            renderer.sharedMaterials = new[] { assigned };

            var consent = UnityMaterialSemantics.CollectTransferConsent(
                new[] { assigned });

            Assert.That(consent.Subjects.Count, Is.EqualTo(0));
            Assert.That(consent.GrantedShaderNames.Count, Is.EqualTo(0));
            UnityEngine.Object.DestroyImmediate(root);
        }
```

- [ ] **Step 3: Run full product test assembly**

Run the full `Alrauna.Amuse.Tests.Editor` test suite.
Expected result: ALL tests PASS. Zero regressions.

- [ ] **Step 4: Final verification and identifier sweep**

Run the identifier sweep on all changed files:
Verify no absolute paths, ports, private asset names, or machine identities exist.

- [ ] **Step 5: Verify git status (commit on authorization)**

Inspect diff:
```bash
git diff
```
