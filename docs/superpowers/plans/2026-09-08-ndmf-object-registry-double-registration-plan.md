# NDMF ObjectRegistry Replacement Deduplication Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Deduplicate replacement object registrations in `AlphaSeparationApply.PrepareSurvivingSet` so avatars with multiple renderers or slots sharing a source material do not trigger the NDMF `RegisterReplacedObject must be called before GetReference is called on the new object` exception.

**Architecture:** Maintain a `HashSet<UnityEngine.Object> registeredReplacements` in `PrepareSurvivingSet`. Before calling `ObjectRegistry.RegisterReplacedObject` for a canonical opaque material clone or separated mesh clone, check `registeredReplacements.Add`. If the object was already registered, skip it.

**Tech Stack:** C# against Unity 2022.3, NUnit (Unity Test Framework EditMode), NDMF 1.14.4.

**Spec:** `docs/superpowers/specs/2026-09-08-ndmf-object-registry-double-registration-design.md`

## Global Constraints

- Fail closed: unsupported conditions refuse. Do not bypass NDMF safety invariants.
- Never mutate source assets. Generated build assets and the NDMF build copy are the only mutation targets.
- Do not introduce public API changes. Keep production types internal to `Alrauna.Amuse.Editor`.
- No absolute paths, host names, ports, or private avatar identifiers in any file or commit message.
- Every English text that a human reads uses ASD-STE100 Simplified Technical English.

---

## File Structure

- Modify: `Packages/com.alrauna.amuse/Editor/Build/AlphaSeparationApply.cs:373-404`
  - Initialize `var registeredReplacements = new HashSet<UnityEngine.Object>();`.
  - Guard material replacement registrations with `!registeredReplacements.Add(pair.Value)`.
  - Guard mesh replacement registrations with `!registeredReplacements.Add(write.Mesh)`.
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Build/AlphaSeparationApplyTests.cs`
  - Add test `SharedMaterialAcrossMultipleRenderersRegistersReplacementWithoutDuplication` to prove that multiple renderers sharing a source material and canonical opaque clone register without error.

---

### Task 1: Failing Regression Test for Shared Material Replacement Registration

**Files:**
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Build/AlphaSeparationApplyTests.cs`

**Interfaces:**
- Consumes: `AlphaSeparationSplitTests.CreateSplitTexture`, `AlphaSeparationSplitTests.SplitAlphaMaterial`, `AlphaSeparationSplitTests.CreateSplitSourceMesh`, `SeamTestPlatform.Instance`, `AvatarProcessor.ProcessAvatar`.
- Produces: Failing regression test that reproduces the NDMF `RegisterReplacedObject must be called before GetReference is called on the new object` exception when two renderers share the same source material.

- [ ] **Step 1: Write the failing regression test**

In `Packages/com.alrauna.amuse/Tests/Editor/Build/AlphaSeparationApplyTests.cs`, add the following test method:

```csharp
[Test]
public void SharedMaterialAcrossMultipleRenderersRegistersReplacementWithoutDuplication()
{
    var root = CreateAvatarRoot("shared-material-avatar");
    var texture = Track(AlphaSeparationSplitTests.CreateSplitTexture());
    var split = Track(
        AlphaSeparationSplitTests.SplitAlphaMaterial(texture));
    var meshA = Track(
        AlphaSeparationSplitTests.CreateSplitSourceMesh());
    var meshB = Track(
        AlphaSeparationSplitTests.CreateSplitSourceMesh());
    AddRenderer(root, "rendererA", meshA, split);
    AddRenderer(root, "rendererB", meshB, split);

    var context = AvatarProcessor.ProcessAvatar(
        root, SeamTestPlatform.Instance);
    var probe = context.GetState<AlphaSeparationSeamProbe>();

    Assert.That(probe.Decision.IsPrepared, Is.True);
    Assert.That(probe.Decision.HasMutation, Is.True);
    Assert.That(probe.Finalization.Writes, Has.Count.EqualTo(2));
}
```

- [ ] **Step 2: Run test to verify it fails**

Refresh Unity and run the test:
```json
{"test_mode": "EditMode", "test_filter": "Alrauna.Amuse.Tests.Editor.Build.AlphaSeparationApplyTests.SharedMaterialAcrossMultipleRenderersRegistersReplacementWithoutDuplication"}
```
Expected: FAIL with `System.ArgumentException: RegisterReplacedObject must be called before GetReference is called on the new object`.

- [ ] **Step 3: Commit the failing test**

```bash
git add Packages/com.alrauna.amuse/Tests/Editor/Build/AlphaSeparationApplyTests.cs
git commit -m "test: reproduce double registration of shared material replacement in NDMF object registry"
```

---

### Task 2: Deduplicate Replacement Registrations in PrepareSurvivingSet

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/Build/AlphaSeparationApply.cs:373-404`
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Build/AlphaSeparationApplyTests.cs`

**Interfaces:**
- Consumes: `AlphaSeparationApply.PrepareSurvivingSet`, `ObjectRegistry.RegisterReplacedObject`.
- Produces: Deduplicated replacement registration that registers each unique replacement instance at most once.

- [ ] **Step 1: Implement replacement deduplication**

In `Packages/com.alrauna.amuse/Editor/Build/AlphaSeparationApply.cs`, update lines 373-400 of `PrepareSurvivingSet`:

```csharp
            var registeredReplacements = new HashSet<UnityEngine.Object>();
            foreach (var survivors in rendererSurvivors)
            {
                foreach (var slot in survivors)
                {
                    foreach (var pair in slot.OpaqueOfAdmitted)
                    {
                        if (ReferenceEquals(pair.Key, pair.Value) ||
                            !registeredReplacements.Add(pair.Value))
                        {
                            continue;
                        }

                        ObjectRegistry.RegisterReplacedObject(
                            pair.Key, pair.Value);
                    }
                }
            }

            foreach (var write in writes)
            {
                if (write.Mesh == null ||
                    !registeredReplacements.Add(write.Mesh))
                {
                    continue;
                }

                ObjectRegistry.RegisterReplacedObject(
                    expectedMeshByRenderer[write.Renderer], write.Mesh);
            }
```

- [ ] **Step 2: Run the regression test to verify it passes**

Refresh Unity and run the test:
```json
{"test_mode": "EditMode", "test_filter": "Alrauna.Amuse.Tests.Editor.Build.AlphaSeparationApplyTests.SharedMaterialAcrossMultipleRenderersRegistersReplacementWithoutDuplication"}
```
Expected: PASS.

- [ ] **Step 3: Commit the fix**

```bash
git add Packages/com.alrauna.amuse/Editor/Build/AlphaSeparationApply.cs
git commit -m "fix: deduplicate replacement registrations in NDMF object registry"
```

---

### Task 3: Full Suite Validation and Clean Handoff

**Files:**
- Test: Full EditMode test suite (`Alrauna.Amuse.Tests.Editor`)
- Spec: `docs/superpowers/specs/2026-09-08-ndmf-object-registry-double-registration-design.md`

- [ ] **Step 1: Mark spec status as implemented**

Update `Status: implemented` in `docs/superpowers/specs/2026-09-08-ndmf-object-registry-double-registration-design.md`.

- [ ] **Step 2: Run full EditMode test suite**

Run `run_tests` with empty filter to execute the full 2,044 tests.
Expected: PASS with 0 failed, 0 skipped.

- [ ] **Step 3: Verify diff hygiene**

Run `git diff --check` and discard package manifest toolchain churn if present.

- [ ] **Step 4: Commit spec update**

```bash
git add docs/superpowers/specs/2026-09-08-ndmf-object-registry-double-registration-design.md
git commit -m "docs: mark NDMF object registry replacement deduplication design implemented"
```
