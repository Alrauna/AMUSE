# Poiyomi Strict Attestation and Non-9.3 Removal: Design

Date: 2026-10-08.
Branch plan: topic branch from main.
No source assets are modified.
All mutations remain inside the NDMF build copy.

Privacy note: this document contains no private avatar, renderer, material, scene, or machine identity.
lilToon and Poiyomi shader names are public vendor identifiers.

Labels: `[SOURCE]` is a fact read from cited code lines in this repository.
`[INFERENCE]` is a conclusion from evidence.

---

## 1. Summary

This design removes all artificial support and version transfer fallbacks for Poiyomi shader versions other than 9.3.64.

On 2026-10-07, commit `baac999` introduced recognition for legacy Poiyomi 9.0 (`.poiyomi/Old Versions/9.0/Poiyomi Toon`).
The commit also allowed unverified Poiyomi shaders to bypass attestation checks through user consent.
When granted, AMUSE processed those shaders with 9.3 rules.
Because older Poiyomi versions differ under the hood, this bypass creates visual and functional risk.

This change enforces strict attestation for Poiyomi materials.
AMUSE will support only pinned Poiyomi Toon Shader version 9.3.64.
All other Poiyomi versions will be treated as unattested or unsupported shaders.
They will never trigger a consent dialog popup.
They will never be converted to opaque materials.
They will remain completely untouched.

In addition, this change deletes dead transfer plumbing in locked material handling.
This reduction increases architectural purity and removes dead weight.

---

## 2. Background and Motivation

`[SOURCE]` `Packages/com.alrauna.amuse/Editor/Semantics/Poiyomi/PoiyomiMaterialSemantics.cs:25-46`.
AMUSE pins exact identity constants for Poiyomi Toon Shader version 9.3.64:
- Canonical asset GUID `9444ce77bf4418748b1e8591b9d97f85`.
- Normalized source hash `31f2ff15615c5e2ac9b05fea08b6310731394d1b5a928b16048e7bde8f8b1755`.
- Pinned package version `9.3.64`.
- Required 23-property schema.

`[SOURCE]` `Packages/com.alrauna.amuse/Editor/Semantics/Poiyomi/PoiyomiMaterialSemantics.cs:48-60`.
Commit `baac999` added `PoiyomiLegacy90ShaderName` for `.poiyomi/Old Versions/9.0/Poiyomi Toon`.
`[SOURCE]` `Packages/com.alrauna.amuse/Editor/Semantics/UnityMaterialSemantics.cs:698-706`.
`ClassifyShaderName` routed this legacy name to `CapturedAlphaMaterialFamily.Poiyomi`.
`[SOURCE]` `Packages/com.alrauna.amuse/Editor/Semantics/UnityMaterialSemantics.cs:481-511`.
Because the shader failed 9.3.64 attestation, `CollectTransferConsent` produced a consent subject.
This created the modal popup warning:
"Shader '.poiyomi/Old Versions/9.0/Poiyomi Toon' is not a verified version. AMUSE would treat it with the verified version's rules, which may be wrong."

`[SOURCE]` `Packages/com.alrauna.amuse/Editor/Build/AlphaSeparationPreparation.cs:880-884`.
When consent was granted, `IsGrantedShader` bypassed `TryVerifyPoiyomiIdentity`.
AMUSE then executed the 9.3 alpha equation and the 9.3 opaque clone recipe on 9.0 materials.

Older Poiyomi versions have different internal architectures and property behaviors.
AMUSE possesses no version-specific equations or translation layers for older versions.
Allowing unverified Poiyomi versions to run under 9.3 rules violates AMUSE correctness invariants.
Poiyomi materials must either pass exact 9.3.64 attestation or remain untouched.

---

## 3. Architectural Invariants

1. **Strict Attestation Invariant**:
   A Poiyomi material is admitted for analysis and optimization if and only if it satisfies `PoiyomiMaterialSemantics.TryVerifyPoiyomiIdentity`.
2. **No Transfer Consent for Poiyomi**:
   The Poiyomi family does not participate in the D8 version transfer consent layer.
   Poiyomi materials never generate consent subjects and never trigger consent popups.
3. **Fail-Closed Safety**:
   Any Poiyomi material that is not version 9.3.64 resolves to `Unknown` alpha or slot refusal.
   It produces zero mesh mutations and zero material mutations.
   The avatar build completes with original assets preserved.
4. **Minimal Code & Dead Code Deletion**:
   Code paths that exist solely for Poiyomi transfer consent are deleted.
   No shims or forwarders remain.

---

## 4. Detailed Code Changes

### Part A: Semantics and Classification

#### 1. Delete Legacy 9.0 Constants
- **File**: `Packages/com.alrauna.amuse/Editor/Semantics/Poiyomi/PoiyomiMaterialSemantics.cs`
- Delete `PoiyomiLegacy90ShaderName = ".poiyomi/Old Versions/9.0/Poiyomi Toon"`.
- Delete the associated documentation comments.

#### 2. Remove Legacy 9.0 Shader Classification
- **File**: `Packages/com.alrauna.amuse/Editor/Semantics/UnityMaterialSemantics.cs`
- In `ClassifyShaderName`, delete the check for `PoiyomiLegacy90ShaderName`.
- The shader `.poiyomi/Old Versions/9.0/Poiyomi Toon` falls through to `(CapturedAlphaMaterialFamily.Unsupported, null)`.

#### 3. Exclude Poiyomi from Version Transfer Pre-Scan
- **File**: `Packages/com.alrauna.amuse/Editor/Semantics/UnityMaterialSemantics.cs`
- In `CollectTransferConsent`, filter out Poiyomi families:
  ```csharp
  if (family == CapturedAlphaMaterialFamily.Unsupported ||
      family == CapturedAlphaMaterialFamily.Poiyomi ||
      family == CapturedAlphaMaterialFamily.PoiyomiTwoPass)
  {
      continue;
  }
  ```
- Poiyomi shaders never produce consent subjects.

#### 4. Enforce Strict Attestation in Alpha Semantics
- **File**: `Packages/com.alrauna.amuse/Editor/Semantics/UnityMaterialSemantics.cs`
- In `AnalyzeAlphaMaterialCore`, remove the `verifyIdentity` bypass for `Poiyomi` and `PoiyomiTwoPass`:
  ```csharp
  case CapturedAlphaMaterialFamily.Poiyomi:
  case CapturedAlphaMaterialFamily.PoiyomiTwoPass:
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
  ```
- Even if transfer mode is active for another family, Poiyomi always verifies identity.

---

### Part B: Build Planning and Opaque Conversion

#### 5. Enforce Strict Attestation in Opaque Conversion
- **File**: `Packages/com.alrauna.amuse/Editor/Build/AlphaSeparationPreparation.cs`
- In `ConvertAdmittedMaterial` under `case CapturedAlphaMaterialFamily.Poiyomi`:
  Delete `&& !IsGrantedShader(sourceEvidence.ShaderName, grantedShaderNames)`.
  The condition becomes:
  ```csharp
  if (!PoiyomiMaterialSemantics.TryVerifyPoiyomiIdentity(
          sourceEvidence,
          out var poiIdentityDiagnostic))
  {
      refusedDetail = "SourceIdentity." + poiIdentityDiagnostic.Code;
      return AlphaSeparationSlotRefusal.OpaqueConversionRefused;
  }
  ```

#### 6. Delete Dead Original Shader Transfer Plumbing
- **File**: `Packages/com.alrauna.amuse/Editor/Build/LockedMaterialIdentity.cs`
- Delete `RecordedOriginalShaderName(Material live)`.
- Delete `GrantedAwareOriginalAttestation(Func<Material, bool> baseAttestation, IReadOnlyCollection<string> grantedShaderNames)`.
- Locked materials strictly require `OriginalShaderAttested(material)`.

#### 7. Simplify Build Plugin Integration
- **File**: `Packages/com.alrauna.amuse/Editor/Build/AmusePlatformFinishPlugin.cs`
- In `Execute` around line 496:
  Delete the `grantedAwareLockAttestation` wrapping call.
  Pass `lockedOriginalAttestation` directly to `TransientUnlockAvailability.WindowEligibleForConsent` and downstream calls.

---

## 5. Behavior Matrix Across Poiyomi Versions

| Version | Shader Name | Family | Attestation | Consent Popup | Build Action |
| :--- | :--- | :--- | :--- | :--- | :--- |
| **Poiyomi 9.3.64** | `.poiyomi/Poiyomi Toon` | `Poiyomi` | Verified | None | Optimized normally. |
| **Poiyomi 9.3.64 Two Pass** | `.poiyomi/Poiyomi Toon Two Pass` | `PoiyomiTwoPass` | Verified | None | Analyzed. Conversion refused by design. |
| **Poiyomi 9.0 Legacy** | `.poiyomi/Old Versions/9.0/Poiyomi Toon` | `Unsupported` | Not evaluated | None | Untouched. Slot refused as unsupported. |
| **Other 9.x Unverified** | `.poiyomi/Poiyomi Toon` | `Poiyomi` | Fails | None | Untouched. Triangles resolve to Unknown. |
| **Poiyomi 7.x & 8.x** | Vendor names | `Unsupported` | Not evaluated | None | Untouched. Slot refused as unsupported. |
| **Locked Poiyomi (9.3.64 Original)** | `Hidden/Locked/...` | `Unsupported` | Original passes | None | Unlocks in memory. Optimized normally. |
| **Locked Poiyomi (Non-9.3 Original)** | `Hidden/Locked/...` | `Unsupported` | Original fails | None | Untouched. Renderer analysis refused. |

---

## 6. Test Strategy and Cleanup

### Tests to Update
1. `Packages/com.alrauna.amuse/Tests/Editor/Semantics/UnityMaterialSemanticsTests.cs`:
   - Replace tests asserting that legacy 9.0 selects `Poiyomi` and produces consent subjects.
   - Assert that legacy 9.0 selects `Unsupported` and produces zero consent subjects.
2. `Packages/com.alrauna.amuse/Tests/Editor/Build/LockedMaterialIdentityTests.cs`:
   - Delete tests that specifically exercise `GrantedAwareOriginalAttestation`.
3. `Packages/com.alrauna.amuse/Tests/Editor/Build/AmusePlatformFinishPluginTests.cs`:
   - Update consent test fixtures to use an unverified stand-in of an admitted transfer family instead of Poiyomi 9.0.
4. `Packages/com.alrauna.amuse/Tests/Editor/Build/VersionConsentTests.cs`:
   - Update consent test fixtures to use an unverified stand-in of an admitted transfer family instead of Poiyomi 9.0.

### Regression Test to Add
- Add a test verifying that an unverified Poiyomi material produces zero consent subjects in `CollectTransferConsent`.
- Add a test verifying that an unverified Poiyomi material fails closed with `Unknown` alpha and undergoes no opaque conversion.

---

## 7. Net Code Reduction & Ponytail Impact

This change produces a net deletion of code:
- Deletes `PoiyomiLegacy90ShaderName` and comments (~15 lines).
- Deletes legacy 9.0 classification branch (~10 lines).
- Deletes `GrantedAwareOriginalAttestation` and `RecordedOriginalShaderName` (~40 lines).
- Deletes `grantedAwareLockAttestation` wrapping in plugin (~5 lines).
- Deletes `!IsGrantedShader` bypass from Poiyomi conversion (~4 lines).
- Simplifies `AnalyzeAlphaMaterialCore` logic (~5 lines).
- Net production reduction: approximately 70 lines deleted.

The resulting architecture is simpler, safer, and strictly verifiable.
