# Alpha Runtime State Envelope Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make a face classifiable `ProvenOpaque` only when it is proven opaque in every admitted runtime state, by observing the committed controller graph, conservatively enumerating a finite set of admitted material states, and re-running the existing alpha proof pipeline across all of them.

**Architecture:** A context-declaring capture pass retains NDMF's `IPlatformAnimatorBindings`; the barrier pass declares no animator extension, so NDMF commits every controller before AMUSE reads it. AMUSE walks the committed real controller graph, eagerly captures animation evidence as immutable values, closes material-swap dependencies before deriving property relevance, admits only finite-exact values and exactly enumerable material swaps, and intersects `ProvenOpaque` per triangle across all admitted states using `AlphaSemanticsResolver` and `TriangleAlphaClassifier` unchanged.

**Tech Stack:** Unity 2022.3.22f1, C# Editor-only code, NDMF 1.14.4 public APIs (`BuildContext`, `AnimatorServicesContext`, `IPlatformAnimatorBindings`, `IVirtualizeAnimatorController`, `IVirtualizeMotion`), `UnityEditor.Animations`, `AnimationUtility`, NUnit EditMode tests, existing `Alrauna.Amuse.Editor` and `Alrauna.Amuse.Tests.Editor` assemblies. No new dependency, assembly, asmdef reference, runtime component, or reflection.

**Spec:** `docs/superpowers/specs/2026-08-23-alpha-runtime-state-envelope-design.md`

## Global Constraints

- `AGENTS.md` and the normative spec apply to every task. The spec's *Non-goals* list is binding.
- **Analysis only.** No AMUSE-authored mesh, material, or build-output mutation. Never modify the source scene or source assets. NDMF's own context lifecycle (re-clone, normalize first-layer weights, re-commit, save) is NDMF's operation, not AMUSE's.
- No caching, fingerprints, or invalidation. No DAO cooperation. No exact Animator reachability solver. No generic animation or runtime-state IR. No provenance adapters.
- **No direct VRChat SDK dependency**, and no reference to the `nadena.dev.ndmf.vrchat` assembly. Reopening this requires reopening the approved spec.
- Never use the Census Lab. Every reported Unity result requires read-only instance discovery and exact normalized, case-sensitive `Application.dataPath == <repo-root>/Assets`.
- **`AlphaSemanticsResolver` and `TriangleAlphaClassifier` remain behaviorally unchanged** unless concrete implementation evidence proves the spec impossible. If that happens, STOP and return to design.
- `MaterialSemantics`, `ExactUvGeometry`, and `MeshSeparationPlanner` also remain behaviorally unchanged.
- **Unsupported or unknown domain behavior returns a named refusal. Unexpected implementation defects propagate.** `catch (Exception) -> skip renderer` is forbidden.
- Live Unity objects are extraction sources only. Proof, admitted-state construction, and classification consume AMUSE-owned immutable values and never call back into `AnimatorController`, `AnimationClip`, `AnimationCurve`, `Material`, `Renderer`, or `Mesh`.
- The admitted-state cap (`4096`) is an **internal bounded-work parameter**, never a public semantic constant. It must not be `public`, must not appear in any public or `internal` API signature, and must not be asserted as a specific number in any test that is not specifically about the budget.
- No reflection over NDMF internals, and no private/internal field access to force a positive path.
- Retain every Unity-generated `.meta` sidecar and inspect GUIDs.
- Inspect the complete `Packages/manifest.json` and `Packages/packages-lock.json` diff before any restore; restore only prohibited host-only churn under `AGENTS.md`.
- No package manifest, asmdef, research package, CI workflow, project setting, or release file changes.
- Do not commit, push, or open a PR without the authorization required by the implementation session.

---

## File structure map

| File | Change | Responsibility |
| --- | --- | --- |
| `Packages/com.alrauna.amuse/Editor/Build/AmuseAnimatorBindingsCapture.cs` | Create | Context-declaring capture pass; retains `IPlatformAnimatorBindings` in build state. |
| `Packages/com.alrauna.amuse/Editor/Build/AmusePlatformFinishPlugin.cs` | Modify | Register the capture pass before the barrier pass; barrier declares no animator extension. |
| `Packages/com.alrauna.amuse/Editor/Host/CommittedControllerGraph.cs` | Create | Enumerate committed controllers; avatar-scoped refusal for unsupported forms. |
| `Packages/com.alrauna.amuse/Editor/Host/CapturedAnimationEvidence.cs` | Create | Immutable animation evidence value types. |
| `Packages/com.alrauna.amuse/Editor/Host/UnityAnimationEvidenceCapture.cs` | Create | Eager capture: curves, behaviours, swap closure, admitted-material evidence. |
| `Packages/com.alrauna.amuse/Editor/Host/StateMachineBehaviourAllowlist.cs` | Create | Version-pinned behaviour type allowlist with recorded justifications. |
| `Packages/com.alrauna.amuse/Editor/Host/UnityMaterialEvidenceCapture.cs` | Modify | Add pure substitution derivations on `CapturedMaterialEvidence`. |
| `Packages/com.alrauna.amuse/Editor/Host/UnityRendererAlphaAnalysis.cs` | Modify | Consume admitted states; intersect per triangle; extended refusals. |
| `Packages/com.alrauna.amuse/Editor/Analysis/AdmittedMaterialStates.cs` | Create | Admitted-state construction, singleton rule, budget, dedup. |
| `Packages/com.alrauna.amuse/Tests/Editor/Host/AnimatorBindingsLifetimeGateTests.cs` | Create | Architecture gate for obligation 6. |
| `Packages/com.alrauna.amuse/Tests/Editor/Host/UnityAnimationCharacterizationTests.cs` | Create | Obligations 1, 2, 3, 4, 8 recorded as observed behavior. |
| `Packages/com.alrauna.amuse/Tests/Editor/Host/CommittedControllerGraphTests.cs` | Create | Enumeration and unsupported-form refusal. |
| `Packages/com.alrauna.amuse/Tests/Editor/Host/UnityAnimationEvidenceCaptureTests.cs` | Create | Eager capture, closure ordering, immutability. |
| `Packages/com.alrauna.amuse/Tests/Editor/Host/StateMachineBehaviourAllowlistTests.cs` | Create | Allowlist and avatar-scoped refusal. |
| `Packages/com.alrauna.amuse/Tests/Editor/Analysis/AdmittedMaterialStatesTests.cs` | Create | Singleton rule, swaps, budget, dedup, intersection. |
| `Packages/com.alrauna.amuse/Tests/Editor/Host/UnityRendererAlphaSnapshotTests.cs` | Modify | Recursive no-live-Unity-object guard. |

---

## Task 1: ARCHITECTURE GATE — `IPlatformAnimatorBindings` lifetime

This is verification obligation 6 and the **first architecture gate**. The entire two-pass observation route depends on it.

**If this task's verification fails, STOP. Do not proceed to Task 2. Do not substitute a mock, a fixture workaround, or a re-activated context. Report the failure as an architectural blocker and return to design.**

**Files:**
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Host/AnimatorBindingsLifetimeGateTests.cs`

**Interfaces:**
- Consumes: nothing.
- Produces: the proven fact that a captured `IPlatformAnimatorBindings` survives `AnimatorServicesContext` deactivation, which every later task assumes.

- [ ] **Step 1: Write the failing gate test**

This must run against the real pinned NDMF lifecycle via `AvatarProcessor.ProcessAvatar`, not a hand-built context. Two synthetic plugins: one pass declares `AnimatorServicesContext` and stores the bindings reference; a later pass declares nothing, so NDMF deactivates and commits before it runs, and then exercises the retained reference.

```csharp
using System;
using System.Collections.Generic;
using nadena.dev.ndmf;
using nadena.dev.ndmf.animator;
using NUnit.Framework;
using UnityEditor.Animations;
using UnityEngine;

[assembly: ExportsPlugin(typeof(
    Alrauna.Amuse.Tests.Editor.Host.AnimatorBindingsLifetimeGateTests.BindingsLifetimeGatePlugin))]

namespace Alrauna.Amuse.Tests.Editor.Host
{
    public sealed class AnimatorBindingsLifetimeGateTests
    {
        internal sealed class GateProbe
        {
            internal IPlatformAnimatorBindings Captured;
            internal bool CaptureRan;
            internal bool ObserveRan;
            internal bool ContextWasInactiveAtObserve;
            internal bool IsSpecialMotionUsable;
            internal bool GetInnateControllersUsable;
            internal int InnateControllerCount;
            internal Exception Failure;
        }

        internal sealed class BindingsLifetimeGatePlugin : Plugin<BindingsLifetimeGatePlugin>
        {
            public override string QualifiedName =>
                "com.alrauna.amuse.tests.bindings-lifetime-gate";

            protected override void Configure()
            {
                InPhase(BuildPhase.PlatformFinish)
                    .Run("capture bindings", Capture)
                    .Then.Run("observe after commit", Observe);
            }

            private static void Capture(BuildContext context)
            {
                if (!SyntheticPluginScope.IsArmed) return;
                var probe = context.GetState<GateProbe>();
                probe.Captured = context.Extension<AnimatorServicesContext>()
                    .ControllerContext.PlatformBindings;
                probe.CaptureRan = true;
            }

            private static void Observe(BuildContext context)
            {
                if (!SyntheticPluginScope.IsArmed) return;
                var probe = context.GetState<GateProbe>();
                probe.ObserveRan = true;

                // The context must be gone: this pass declares no extension.
                try
                {
                    context.Extension<AnimatorServicesContext>();
                    probe.ContextWasInactiveAtObserve = false;
                }
                catch (Exception)
                {
                    probe.ContextWasInactiveAtObserve = true;
                }

                try
                {
                    var motion = new AnimationClip { name = "gate probe clip" };
                    probe.IsSpecialMotionUsable =
                        probe.Captured.IsSpecialMotion(motion) == false;
                    UnityEngine.Object.DestroyImmediate(motion);

                    var innate = new List<(object, RuntimeAnimatorController, bool)>(
                        probe.Captured.GetInnateControllers(context.AvatarRootObject));
                    probe.InnateControllerCount = innate.Count;
                    probe.GetInnateControllersUsable = true;
                }
                catch (Exception exception)
                {
                    probe.Failure = exception;
                }
            }
        }

        [Test]
        public void CapturedBindingsRemainUsableAfterContextDeactivation()
        {
            using var armed = SyntheticPluginScope.Arm();
            using var assets = new OverrideTemporaryDirectoryScope(null);
            var root = new GameObject("AMUSE bindings lifetime gate");

            try
            {
                var context = AvatarProcessor.ProcessAvatar(
                    root, TestVrchatPlatform.Instance);
                var probe = context.GetState<GateProbe>();

                Assert.That(probe.CaptureRan, Is.True, "capture pass did not run");
                Assert.That(probe.ObserveRan, Is.True, "observe pass did not run");
                Assert.That(probe.Captured, Is.Not.Null,
                    "PlatformBindings was not obtainable while the context was active");
                Assert.That(probe.ContextWasInactiveAtObserve, Is.True,
                    "ARCHITECTURE GATE: the animator context was still active in a " +
                    "pass that declares no extension, so NDMF did not commit first");
                Assert.That(probe.Failure, Is.Null,
                    "ARCHITECTURE GATE: the retained bindings threw after " +
                    "deactivation: " + probe.Failure);
                Assert.That(probe.IsSpecialMotionUsable, Is.True,
                    "ARCHITECTURE GATE: IsSpecialMotion did not behave correctly " +
                    "after deactivation");
                Assert.That(probe.GetInnateControllersUsable, Is.True,
                    "ARCHITECTURE GATE: GetInnateControllers threw after deactivation");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }
    }
}
```

Reuse the existing `SyntheticPluginScope`, `OverrideTemporaryDirectoryScope`, and `TestVrchatPlatform` helpers from `AmusePlatformFinishPluginTests.cs`. If `SyntheticPluginScope` does not currently expose `IsArmed`, add that single internal property in this task rather than duplicating the scope type.

- [ ] **Step 2: Run the gate test**

Run the EditMode suite filtered to `AnimatorBindingsLifetimeGateTests`, on the public development project only, after confirming `Application.dataPath == <repo-root>/Assets`.

Expected on first run: FAIL, because the test type and helper visibility do not exist yet.

- [ ] **Step 3: Make the test compile and run**

Add only what the test needs: the `IsArmed` property if missing. Write no production code for later tasks.

- [ ] **Step 4: Run the gate test and record the observed result**

Run: the same filtered EditMode run.
Expected: PASS.

**If it FAILS on any of the four gate assertions, STOP HERE.** Record which assertion failed and the exact exception. Do not adapt the test to make it pass. Do not re-activate the context inside the observe pass to "fix" it — that would restore the marker blind spot the spec exists to remove. Report to the user as an architectural blocker; the design must be revised (another public route to the bindings, a different pass split, or a different observation source).

- [ ] **Step 5: Record the outcome in the spec's obligation list**

Edit `docs/superpowers/specs/2026-08-23-alpha-runtime-state-envelope-design.md`, obligation 6, appending one sentence stating that it was verified, on which date, and by which test. Do not restructure the spec.

- [ ] **Step 6: Commit**

```bash
git add Packages/com.alrauna.amuse/Tests/Editor/Host/AnimatorBindingsLifetimeGateTests.cs \
        Packages/com.alrauna.amuse/Tests/Editor/Build/AmusePlatformFinishPluginTests.cs \
        docs/superpowers/specs/2026-08-23-alpha-runtime-state-envelope-design.md
git commit -m "test: prove animator bindings survive context deactivation"
```

---

## Task 2: Characterize animated material bindings

Verification obligations 2, 3, and 4. These are **characterization tests**: they record what Unity actually does. They must assert observed behavior, never desired behavior.

**Files:**
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Host/UnityAnimationCharacterizationTests.cs`

**Interfaces:**
- Consumes: nothing.
- Produces: the exact `EditorCurveBinding.propertyName` form for per-slot material property animation, which Task 9 uses to map bindings to slots.

- [ ] **Step 1: Write the failing characterization test**

```csharp
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Alrauna.Amuse.Tests.Editor.Host
{
    public sealed class UnityAnimationCharacterizationTests
    {
        [Test]
        public void MaterialPropertyBindingNamesAreRecorded()
        {
            var clip = new AnimationClip { name = "characterization" };
            try
            {
                var slot0 = EditorCurveBinding.FloatCurve(
                    "", typeof(SkinnedMeshRenderer), "material._Cutoff");
                var slot1 = EditorCurveBinding.FloatCurve(
                    "", typeof(SkinnedMeshRenderer), "material[1]._Cutoff");

                AnimationUtility.SetEditorCurve(
                    clip, slot0, AnimationCurve.Constant(0f, 1f, 0.5f));
                AnimationUtility.SetEditorCurve(
                    clip, slot1, AnimationCurve.Constant(0f, 1f, 0.5f));

                var bindings = AnimationUtility.GetCurveBindings(clip)
                    .Select(b => b.propertyName)
                    .OrderBy(n => n)
                    .ToArray();

                // Record exactly what Unity round-trips. If this assertion fails,
                // DO NOT change it to match the code: change the code, and update
                // the spec's obligation 2, because the slot mapping rule is wrong.
                Assert.That(bindings, Is.EqualTo(new[]
                {
                    "material._Cutoff",
                    "material[1]._Cutoff",
                }));
            }
            finally
            {
                Object.DestroyImmediate(clip);
            }
        }

        [Test]
        public void TextureReferenceObjectCurveSupportIsRecorded()
        {
            var clip = new AnimationClip { name = "texture reference characterization" };
            var texture = new Texture2D(1, 1);
            try
            {
                var binding = EditorCurveBinding.PPtrCurve(
                    "", typeof(SkinnedMeshRenderer), "material._MainTex");
                AnimationUtility.SetObjectReferenceCurve(clip, binding, new[]
                {
                    new ObjectReferenceKeyframe { time = 0f, value = texture },
                });

                var round = AnimationUtility.GetObjectReferenceCurveBindings(clip);

                // Records whether Unity accepts a texture-reference object curve on a
                // material property at all. Either outcome is a valid recorded fact;
                // update the spec's obligation 3 with whichever is observed.
                Assert.That(round.Length, Is.EqualTo(1).Or.EqualTo(0));
                TestContext.WriteLine(
                    "obligation 3: texture reference object curve bindings = " +
                    round.Length);
            }
            finally
            {
                Object.DestroyImmediate(clip);
                Object.DestroyImmediate(texture);
            }
        }
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Expected: FAIL — the test file does not exist yet, then compile/run.

- [ ] **Step 3: Run them and record the observed values**

There is no production code in this task. Run the tests, read the actual Unity behavior, and adjust the *expected* values in `MaterialPropertyBindingNamesAreRecorded` to whatever Unity actually produced — this is the one place in the plan where matching the test to observed behavior is correct, because the test's purpose is to record reality.

For obligation 4 (`MaterialPropertyBlock`), add a third test only if it can be observed deterministically in EditMode without entering Play Mode. If it cannot, do not fake it: leave obligation 4 open in the spec and record in this file's header comment that it requires a Play Mode observation, which is out of this plan's scope.

- [ ] **Step 4: Run the full file and verify it passes**

Expected: PASS, with the recorded values.

- [ ] **Step 5: Update the spec's obligations 2, 3, and 4**

Replace each obligation's text with the observed fact and the test that established it, keeping any that could not be observed explicitly open.

- [ ] **Step 6: Commit**

```bash
git add Packages/com.alrauna.amuse/Tests/Editor/Host/UnityAnimationCharacterizationTests.cs \
        docs/superpowers/specs/2026-08-23-alpha-runtime-state-envelope-design.md
git commit -m "test: characterize animated material binding semantics"
```

---

## Task 3: Characterize float blending across sources

Verification obligation 8. Determines whether float admission could later widen. V1 behavior does not change either way.

**Files:**
- Modify: `Packages/com.alrauna.amuse/Tests/Editor/Host/UnityAnimationCharacterizationTests.cs`

**Interfaces:**
- Consumes: nothing.
- Produces: a recorded fact only; no type is produced.

- [ ] **Step 1: Write the failing test**

```csharp
        [Test]
        public void InterpolatingCurveProducesIntermediateValues()
        {
            var curve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

            var mid = curve.Evaluate(0.5f);

            // If a curve interpolates between keyframes, keyframe values alone do
            // not bound the reachable set. This is the direct evidence for the
            // spec's finite-exact rule.
            Assert.That(mid, Is.GreaterThan(0f).And.LessThan(1f));
        }

        [Test]
        public void ConstantTangentCurveTakesOnlyKeyframeValues()
        {
            var curve = new AnimationCurve(
                new Keyframe(0f, 0f) { outTangent = float.PositiveInfinity },
                new Keyframe(1f, 1f) { inTangent = float.PositiveInfinity });

            var mid = curve.Evaluate(0.5f);

            // A stepped curve is admissible under the finite-exact rule because it
            // never leaves its keyframe values.
            Assert.That(mid, Is.EqualTo(0f).Or.EqualTo(1f));
        }
```

- [ ] **Step 2: Run to verify they fail**

Expected: FAIL — methods not present.

- [ ] **Step 3: Add the tests**

No production code. Paste the two methods into the existing characterization class.

- [ ] **Step 4: Run and verify they pass**

Expected: PASS. If `ConstantTangentCurveTakesOnlyKeyframeValues` fails, the stepped-curve admission rule is wrong — STOP and report, because Task 10 depends on it.

- [ ] **Step 5: Update the spec's obligation 8**

Record what was observed for within-curve interpolation. Note that cross-source blending (layer weights, transitions, blend trees) still requires a Play Mode observation and remains open, so the conservative singleton rule stands.

- [ ] **Step 6: Commit**

```bash
git add Packages/com.alrauna.amuse/Tests/Editor/Host/UnityAnimationCharacterizationTests.cs \
        docs/superpowers/specs/2026-08-23-alpha-runtime-state-envelope-design.md
git commit -m "test: characterize curve interpolation bounds"
```

---

## Task 4: Characterize `GetInnateControllers` side effects

Verification obligation 1. Determines whether AMUSE may call it in the barrier pass or must capture in the capture pass.

**Files:**
- Modify: `Packages/com.alrauna.amuse/Tests/Editor/Host/AnimatorBindingsLifetimeGateTests.cs`

**Interfaces:**
- Consumes: the retained bindings from Task 1.
- Produces: the decision of where `GetInnateControllers` is called, which Task 5 depends on.

- [ ] **Step 1: Write the failing test**

Extend `GateProbe` with a second observation and assert that calling `GetInnateControllers` twice yields equal results:

```csharp
            internal int SecondInnateControllerCount;
            internal bool RepeatCallMatched;
```

In `Observe`, after the first call:

```csharp
                    var again = new List<(object, RuntimeAnimatorController, bool)>(
                        probe.Captured.GetInnateControllers(context.AvatarRootObject));
                    probe.SecondInnateControllerCount = again.Count;
                    probe.RepeatCallMatched =
                        again.Count == probe.InnateControllerCount;
```

And a test:

```csharp
        [Test]
        public void RepeatedInnateControllerEnumerationIsStable()
        {
            using var armed = SyntheticPluginScope.Arm();
            using var assets = new OverrideTemporaryDirectoryScope(null);
            var root = new GameObject("AMUSE innate enumeration stability");

            try
            {
                var context = AvatarProcessor.ProcessAvatar(
                    root, TestVrchatPlatform.Instance);
                var probe = context.GetState<GateProbe>();

                Assert.That(probe.Failure, Is.Null);
                Assert.That(probe.RepeatCallMatched, Is.True,
                    "GetInnateControllers is not stable across repeated calls; " +
                    "the barrier pass must not call it directly");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }
```

- [ ] **Step 2: Run to verify it fails**

Expected: FAIL — `RepeatCallMatched` is not defined.

- [ ] **Step 3: Add the probe fields and the second call**

- [ ] **Step 4: Run and record**

Expected: PASS. If it fails, the barrier pass must not call `GetInnateControllers`; instead the capture pass must record the innate controller *keys*, and Task 5 must re-resolve controllers from those keys. Record whichever outcome is observed and follow it in Task 5 — do not assume stability.

- [ ] **Step 5: Update the spec's obligation 1**

- [ ] **Step 6: Commit**

```bash
git add Packages/com.alrauna.amuse/Tests/Editor/Host/AnimatorBindingsLifetimeGateTests.cs \
        docs/superpowers/specs/2026-08-23-alpha-runtime-state-envelope-design.md
git commit -m "test: characterize innate controller enumeration stability"
```

---

## Task 5: Committed-controller enumeration and unsupported-form refusal

Covers verification obligation 5.

**Files:**
- Create: `Packages/com.alrauna.amuse/Editor/Host/CommittedControllerGraph.cs`
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Host/CommittedControllerGraphTests.cs`

**Interfaces:**
- Consumes: `IPlatformAnimatorBindings` from Task 1.
- Produces:
  - `internal enum AvatarAnimationRefusal { None, UnsupportedAnimatorControllerForm, UnsupportedSyncedLayerOverrides, UnrecognizedStateMachineBehaviour }`
  - `internal sealed class CommittedLayer { internal string ControllerName { get; } internal int LayerIndex { get; } internal AnimatorLayerBlendingMode BlendingMode { get; } internal IReadOnlyList<AnimationClip> Clips { get; } internal IReadOnlyList<StateMachineBehaviour> Behaviours { get; } internal bool HasUnnormalizedDirectBlendTree { get; } }`
  - `internal sealed class CommittedControllerGraphResult { internal AvatarAnimationRefusal Refusal { get; } internal IReadOnlyList<CommittedLayer> Layers { get; } }`
  - `internal static CommittedControllerGraphResult CommittedControllerGraph.Enumerate(GameObject avatarRoot, IPlatformAnimatorBindings bindings)`

`CommittedLayer` deliberately holds live `AnimationClip` and `StateMachineBehaviour` references: it is an *enumeration* result consumed only by the eager capture in Task 6, never by proof. Task 17's guard applies to captured evidence, not to this type.

- [ ] **Step 1: Write the failing test**

```csharp
using System.Linq;
using Alrauna.Amuse.Editor.Host;
using NUnit.Framework;
using UnityEditor.Animations;
using UnityEngine;

namespace Alrauna.Amuse.Tests.Editor.Host
{
    public sealed class CommittedControllerGraphTests
    {
        [Test]
        public void OverrideControllerIsRefusedByForm()
        {
            var root = new GameObject("override form");
            var baseController = new AnimatorController();
            var over = new AnimatorOverrideController(baseController);
            try
            {
                var animator = root.AddComponent<Animator>();
                animator.runtimeAnimatorController = over;

                var result = CommittedControllerGraph.Enumerate(
                    root, new StubBindings(root, over));

                Assert.That(result.Refusal, Is.EqualTo(
                    AvatarAnimationRefusal.UnsupportedAnimatorControllerForm));
                Assert.That(result.Layers, Is.Empty);
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(over);
                Object.DestroyImmediate(baseController);
            }
        }

        [Test]
        public void PlainControllerLayersAndClipsAreEnumerated()
        {
            var root = new GameObject("plain controller");
            var controller = new AnimatorController();
            var clip = new AnimationClip { name = "enumerated" };
            try
            {
                controller.AddLayer("L0");
                var machine = controller.layers[0].stateMachine;
                var state = machine.AddState("S0");
                state.motion = clip;

                var result = CommittedControllerGraph.Enumerate(
                    root, new StubBindings(root, controller));

                Assert.That(result.Refusal, Is.EqualTo(AvatarAnimationRefusal.None));
                Assert.That(result.Layers.Count, Is.EqualTo(1));
                Assert.That(result.Layers[0].Clips.Single(), Is.SameAs(clip));
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(clip);
                Object.DestroyImmediate(controller);
            }
        }
    }
}
```

Write `StubBindings` in this test file as a minimal `IPlatformAnimatorBindings` implementation returning one `(key, controller, false)` tuple and `IsSpecialMotion => false`. This is a test double for the *enumeration input*, not for the Task 1 gate, and is permitted.

- [ ] **Step 2: Run to verify it fails**

Expected: FAIL with "CommittedControllerGraph does not exist".

- [ ] **Step 3: Implement enumeration**

Walk `GetInnateControllers`, plus `GetComponentsInChildren<IVirtualizeAnimatorController>(true)` and `GetComponentsInChildren<IVirtualizeMotion>(true)`. For each `RuntimeAnimatorController`: if it is not an `AnimatorController`, return `UnsupportedAnimatorControllerForm`. For each layer, if `syncedLayerIndex >= 0`, return `UnsupportedSyncedLayerOverrides`. Otherwise collect every reachable `AnimationClip` by walking state machines, child state machines, states, and blend trees, and collect state and state-machine behaviours. Set `HasUnnormalizedDirectBlendTree` when any reachable `BlendTree` has `blendType == BlendTreeType.Direct` and `blendParameter` normalization is off; read normalization through the serialized `m_NormalizedBlendValues` property, since `BlendTree` exposes no public accessor in this Unity version. Deduplicate clips by reference.

- [ ] **Step 4: Run and verify it passes**

Expected: PASS.

- [ ] **Step 5: Update the spec's obligation 5 with the observed committed forms**

- [ ] **Step 6: Commit**

```bash
git add Packages/com.alrauna.amuse/Editor/Host/CommittedControllerGraph.cs \
        Packages/com.alrauna.amuse/Tests/Editor/Host/CommittedControllerGraphTests.cs \
        docs/superpowers/specs/2026-08-23-alpha-runtime-state-envelope-design.md
git commit -m "feat: enumerate the committed controller graph"
```

---

## Task 6: Eager immutable animation capture

**Files:**
- Create: `Packages/com.alrauna.amuse/Editor/Host/CapturedAnimationEvidence.cs`
- Create: `Packages/com.alrauna.amuse/Editor/Host/UnityAnimationEvidenceCapture.cs`
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Host/UnityAnimationEvidenceCaptureTests.cs`

**Interfaces:**
- Consumes: `CommittedControllerGraphResult` from Task 5.
- Produces:
  - `internal sealed class CapturedFloatBinding { internal string Path { get; } internal string TypeName { get; } internal string PropertyName { get; } internal bool IsFiniteExact { get; } internal IReadOnlyList<float> Values { get; } }`
  - `internal sealed class CapturedObjectBinding { internal string Path { get; } internal string TypeName { get; } internal string PropertyName { get; } internal IReadOnlyList<int> AdmittedMaterialIndices { get; } }`
  - `internal sealed class CapturedClipEvidence { internal string Name { get; } internal bool IsSpecialMotion { get; } internal IReadOnlyList<CapturedFloatBinding> FloatBindings { get; } internal IReadOnlyList<CapturedObjectBinding> ObjectBindings { get; } }`
  - `internal sealed class CapturedAnimationEvidence { internal IReadOnlyList<CapturedClipEvidence> Clips { get; } internal IReadOnlyList<CapturedAlphaMaterial> AdmittedMaterials { get; } internal IReadOnlyList<string> BehaviourTypeNames { get; } internal bool HasUnnormalizedDirectBlendTree { get; } internal bool HasAdditiveLayer { get; } }`

`AdmittedMaterialIndices` indexes into `CapturedAnimationEvidence.AdmittedMaterials`; **no live `Material` is stored anywhere in captured evidence.** `IsSpecialMotion` is recorded as diagnostic evidence only and must never gate any decision.

- [ ] **Step 1: Write the failing test**

```csharp
using System.Linq;
using Alrauna.Amuse.Editor.Host;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Alrauna.Amuse.Tests.Editor.Host
{
    public sealed class UnityAnimationEvidenceCaptureTests
    {
        [Test]
        public void CapturedEvidenceSurvivesClipMutation()
        {
            var clip = new AnimationClip { name = "mutated later" };
            var binding = EditorCurveBinding.FloatCurve(
                "Body", typeof(SkinnedMeshRenderer), "material._Cutoff");
            AnimationUtility.SetEditorCurve(
                clip, binding, AnimationCurve.Constant(0f, 1f, 0.25f));

            try
            {
                var captured = UnityAnimationEvidenceCapture.CaptureClip(clip, false);

                AnimationUtility.SetEditorCurve(
                    clip, binding, AnimationCurve.Constant(0f, 1f, 0.75f));

                Assert.That(captured.FloatBindings.Single().Values.Single(),
                    Is.EqualTo(0.25f));
            }
            finally
            {
                Object.DestroyImmediate(clip);
            }
        }

        [Test]
        public void InterpolatingCurveIsNotFiniteExact()
        {
            var clip = new AnimationClip { name = "interpolating" };
            var binding = EditorCurveBinding.FloatCurve(
                "Body", typeof(SkinnedMeshRenderer), "material._Cutoff");
            AnimationUtility.SetEditorCurve(
                clip, binding, AnimationCurve.Linear(0f, 0f, 1f, 1f));

            try
            {
                var captured = UnityAnimationEvidenceCapture.CaptureClip(clip, false);

                Assert.That(captured.FloatBindings.Single().IsFiniteExact, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(clip);
            }
        }
    }
}
```

- [ ] **Step 2: Run to verify it fails**

Expected: FAIL with "UnityAnimationEvidenceCapture does not exist".

- [ ] **Step 3: Implement capture**

`CaptureClip(AnimationClip clip, bool isSpecialMotion)` reads `AnimationUtility.GetCurveBindings` and `GetObjectReferenceCurveBindings`, copies every keyframe value into arrays, and sets `IsFiniteExact` true only when every segment is constant: a single key, or every consecutive key pair where `outTangent` and the next `inTangent` are both `float.PositiveInfinity`, or both keys carry the same value. Any `weightedMode` other than `WeightedMode.None` sets `IsFiniteExact` false. All collections are wrapped in `ReadOnlyCollection<T>`.

- [ ] **Step 4: Run and verify it passes**

Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add Packages/com.alrauna.amuse/Editor/Host/CapturedAnimationEvidence.cs \
        Packages/com.alrauna.amuse/Editor/Host/UnityAnimationEvidenceCapture.cs \
        Packages/com.alrauna.amuse/Tests/Editor/Host/UnityAnimationEvidenceCaptureTests.cs
git commit -m "feat: capture animation evidence eagerly as immutable values"
```

---

## Task 7: Structural material-swap discovery

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/Host/UnityAnimationEvidenceCapture.cs`
- Modify: `Packages/com.alrauna.amuse/Tests/Editor/Host/UnityAnimationEvidenceCaptureTests.cs`

**Interfaces:**
- Consumes: `CapturedObjectBinding` from Task 6.
- Produces: `internal static bool UnityAnimationEvidenceCapture.TryParseMaterialSlotBinding(string propertyName, out int slotIndex)`

- [ ] **Step 1: Write the failing test**

```csharp
        [TestCase("m_Materials.Array.data[0]", 0)]
        [TestCase("m_Materials.Array.data[3]", 3)]
        public void MaterialSlotBindingsAreParsed(string property, int expected)
        {
            Assert.That(
                UnityAnimationEvidenceCapture.TryParseMaterialSlotBinding(
                    property, out var slot), Is.True);
            Assert.That(slot, Is.EqualTo(expected));
        }

        [TestCase("m_Materials.Array.size")]
        [TestCase("m_Mesh")]
        [TestCase("material._Cutoff")]
        public void NonSlotBindingsAreNotParsedAsSlots(string property)
        {
            Assert.That(
                UnityAnimationEvidenceCapture.TryParseMaterialSlotBinding(
                    property, out _), Is.False);
        }
```

- [ ] **Step 2: Run to verify it fails**

Expected: FAIL — method not defined.

- [ ] **Step 3: Implement parsing**

Exact ordinal prefix `m_Materials.Array.data[`, exact suffix `]`, and an invariant non-negative integer between them. Reject anything else, including `m_Materials.Array.size`. Do not use a regular expression.

- [ ] **Step 4: Run and verify it passes**

Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add Packages/com.alrauna.amuse/Editor/Host/UnityAnimationEvidenceCapture.cs \
        Packages/com.alrauna.amuse/Tests/Editor/Host/UnityAnimationEvidenceCaptureTests.cs
git commit -m "feat: discover animated material slot assignments"
```

---

## Task 8: Admitted-material dependency closure

**This task carries the ordering requirement the spec makes normative.** Its central test must fail if relevance is derived from the initially assigned material's family.

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/Host/UnityAnimationEvidenceCapture.cs`
- Modify: `Packages/com.alrauna.amuse/Tests/Editor/Host/UnityAnimationEvidenceCaptureTests.cs`

**Interfaces:**
- Consumes: `TryParseMaterialSlotBinding` from Task 7; `UnityMaterialSemantics.CaptureAlphaMaterials`; `MaterialEvidenceRequest.Combine`.
- Produces:
  - `internal sealed class AdmittedMaterialClosure { internal bool IsClosed { get; } internal IReadOnlyList<CapturedAlphaMaterial> AdmittedMaterials { get; } internal MaterialEvidenceRequest RelevanceRequest { get; } }`
  - `internal static AdmittedMaterialClosure UnityAnimationEvidenceCapture.CloseMaterialDependencies(IReadOnlyList<Material> currentSlots, IReadOnlyList<IReadOnlyList<Material>> animatedSlotAssignments)`

- [ ] **Step 1: Write the failing test**

The decisive test: slot 0 currently holds a material of family A, and animation can assign a material of family B. The resulting relevance request must contain a property that **only** family B requests.

```csharp
        [Test]
        public void ClosureUnionsEveryAdmittedFamilyNotOnlyTheInitialOne()
        {
            var initial = new Material(Shader.Find("Unlit/Color"));
            var swapped = new Material(Shader.Find("Unlit/Texture"));
            try
            {
                var closure = UnityAnimationEvidenceCapture.CloseMaterialDependencies(
                    new[] { initial },
                    new IReadOnlyList<Material>[] { new[] { swapped } });

                Assert.That(closure.IsClosed, Is.True);
                Assert.That(closure.AdmittedMaterials.Count, Is.EqualTo(2));

                // The union must cover the swapped material's family too. Deriving
                // relevance from `initial` alone would omit these names.
                var union = closure.RelevanceRequest;
                foreach (var name in SwappedFamilyOnlyProperties())
                {
                    Assert.That(union.ScalarProperties, Contains.Item(name),
                        "relevance filter missed a property contributed only by a " +
                        "material introduced by animation: " + name);
                }
            }
            finally
            {
                Object.DestroyImmediate(initial);
                Object.DestroyImmediate(swapped);
            }
        }

        [Test]
        public void ClosureFailsWhenAnAdmittedAssignmentIsUnresolvable()
        {
            var initial = new Material(Shader.Find("Unlit/Color"));
            try
            {
                var closure = UnityAnimationEvidenceCapture.CloseMaterialDependencies(
                    new[] { initial },
                    new IReadOnlyList<Material>[] { null });

                Assert.That(closure.IsClosed, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(initial);
            }
        }
```

`SwappedFamilyOnlyProperties()` is a test helper returning the scalar property names that the swapped material's attested family requests and the initial material's family does not. Because both shaders above are unattested stock Unity shaders, replace them in implementation with the two attested families the repository already supports, constructing materials through the existing Poiyomi and lilToon fixture helpers in `PoiyomiFixtureTestBase` and `LilToonFixtureTestBase`, and derive the expected names by set-differencing `PoiyomiMaterialSemantics.AlphaEvidenceRequest` and `LilToonMaterialSemantics.AlphaEvidenceRequest`. Compute the difference in the test rather than hard-coding names, so the test cannot drift from the frontends.

- [ ] **Step 2: Run to verify it fails**

Expected: FAIL — `CloseMaterialDependencies` not defined.

- [ ] **Step 3: Implement the closure in the normative order**

```
discover structural material-slot swaps
  -> enumerate every admitted material (current assignment + every keyframe value)
  -> attest/identify every admitted material family
  -> MaterialEvidenceRequest.Combine over every admitted family's alpha request
  -> return the closed union
```

Each step completes before the next begins. `IsClosed` is false when any slot's admitted set cannot be fully enumerated, or when any admitted material's family cannot be attested. When `IsClosed` is false, `RelevanceRequest` must be the empty request and must never be consumed — the caller refuses.

- [ ] **Step 4: Run and verify it passes**

Expected: PASS.

- [ ] **Step 5: Add a guard test that the order cannot be inverted**

```csharp
        [Test]
        public void RelevanceRequestIsEmptyWhenClosureFails()
        {
            var initial = new Material(Shader.Find("Unlit/Color"));
            try
            {
                var closure = UnityAnimationEvidenceCapture.CloseMaterialDependencies(
                    new[] { initial },
                    new IReadOnlyList<Material>[] { null });

                Assert.That(closure.IsClosed, Is.False);
                Assert.That(closure.RelevanceRequest.ScalarProperties, Is.Empty);
                Assert.That(closure.RelevanceRequest.TextureProperties, Is.Empty);
            }
            finally
            {
                Object.DestroyImmediate(initial);
            }
        }
```

- [ ] **Step 6: Run and commit**

```bash
git add Packages/com.alrauna.amuse/Editor/Host/UnityAnimationEvidenceCapture.cs \
        Packages/com.alrauna.amuse/Tests/Editor/Host/UnityAnimationEvidenceCaptureTests.cs
git commit -m "feat: close material dependencies before deriving relevance"
```

---

## Task 9: Proof-relevant binding discovery

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/Host/UnityAnimationEvidenceCapture.cs`
- Modify: `Packages/com.alrauna.amuse/Tests/Editor/Host/UnityAnimationEvidenceCaptureTests.cs`

**Interfaces:**
- Consumes: `AdmittedMaterialClosure` from Task 8; the binding-name form recorded in Task 2.
- Produces: `internal static bool UnityAnimationEvidenceCapture.IsProofRelevant(CapturedFloatBinding binding, string rendererPath, int slotCount, MaterialEvidenceRequest relevance, out int slotIndex)`

- [ ] **Step 1: Write the failing test**

```csharp
        [Test]
        public void BindingIsRelevantOnlyWhenTheClosedUnionRequestsIt()
        {
            var relevance = new MaterialEvidenceRequest(
                shaderName: false,
                activeColorSpace: false,
                presenceProperties: System.Array.Empty<string>(),
                scalarProperties: new[] { "_Cutoff" },
                colorProperties: System.Array.Empty<string>(),
                vectorProperties: System.Array.Empty<string>(),
                textureProperties:
                    System.Array.Empty<TexturePropertyEvidenceRequest>());

            var relevant = new CapturedFloatBinding(
                "Body", nameof(SkinnedMeshRenderer), "material._Cutoff",
                true, new[] { 1f });
            var irrelevant = new CapturedFloatBinding(
                "Body", nameof(SkinnedMeshRenderer), "material._Unrelated",
                true, new[] { 1f });

            Assert.That(UnityAnimationEvidenceCapture.IsProofRelevant(
                relevant, "Body", 1, relevance, out var slot), Is.True);
            Assert.That(slot, Is.Zero);
            Assert.That(UnityAnimationEvidenceCapture.IsProofRelevant(
                irrelevant, "Body", 1, relevance, out _), Is.False);
        }

        [Test]
        public void BindingsOnOtherRendererPathsAreNotRelevant()
        {
            var relevance = new MaterialEvidenceRequest(
                false, false,
                System.Array.Empty<string>(),
                new[] { "_Cutoff" },
                System.Array.Empty<string>(),
                System.Array.Empty<string>(),
                System.Array.Empty<TexturePropertyEvidenceRequest>());

            var elsewhere = new CapturedFloatBinding(
                "Other", nameof(SkinnedMeshRenderer), "material._Cutoff",
                true, new[] { 1f });

            Assert.That(UnityAnimationEvidenceCapture.IsProofRelevant(
                elsewhere, "Body", 1, relevance, out _), Is.False);
        }
```

- [ ] **Step 2: Run to verify it fails**

Expected: FAIL — `IsProofRelevant` not defined.

- [ ] **Step 3: Implement**

Match `Path` against the renderer's avatar-relative path with `StringComparison.Ordinal`. Strip the `material` prefix using the exact form recorded in Task 2, extracting the slot index where the recorded form carries one and defaulting to slot 0 otherwise. Compare the remaining property name against the closed union's scalar, color, and vector names, treating a `.r`/`.g`/`.b`/`.a`/`.x`/`.y`/`.z`/`.w` suffix as naming its parent color or vector property. Return false for any name the union does not request.

- [ ] **Step 4: Run and verify it passes**

Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add Packages/com.alrauna.amuse/Editor/Host/UnityAnimationEvidenceCapture.cs \
        Packages/com.alrauna.amuse/Tests/Editor/Host/UnityAnimationEvidenceCaptureTests.cs
git commit -m "feat: discover proof-relevant animated bindings"
```

---

## Task 10: Finite-exact singleton property analysis

**Files:**
- Create: `Packages/com.alrauna.amuse/Editor/Analysis/AdmittedMaterialStates.cs`
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Analysis/AdmittedMaterialStatesTests.cs`

**Interfaces:**
- Consumes: `CapturedFloatBinding` from Task 6.
- Produces:
  - `internal enum AdmittedPropertyOutcome { Singleton, NotFiniteExact, SourcesDisagree }`
  - `internal static AdmittedPropertyOutcome AdmittedMaterialStates.AdmitProperty(IReadOnlyList<CapturedFloatBinding> bindings, float serializedDefault, out float admittedValue)`

- [ ] **Step 1: Write the failing test**

```csharp
using Alrauna.Amuse.Editor.Analysis;
using Alrauna.Amuse.Editor.Host;
using NUnit.Framework;

namespace Alrauna.Amuse.Tests.Editor.Analysis
{
    public sealed class AdmittedMaterialStatesTests
    {
        private static CapturedFloatBinding Binding(bool finiteExact, params float[] values)
        {
            return new CapturedFloatBinding(
                "Body", "SkinnedMeshRenderer", "material._Cutoff",
                finiteExact, values);
        }

        [Test]
        public void AgreeingSourcesAdmitTheSingleValue()
        {
            var outcome = AdmittedMaterialStates.AdmitProperty(
                new[] { Binding(true, 1f), Binding(true, 1f) }, 1f, out var value);

            Assert.That(outcome, Is.EqualTo(AdmittedPropertyOutcome.Singleton));
            Assert.That(value, Is.EqualTo(1f));
        }

        [Test]
        public void DisagreeingSourcesRefuse()
        {
            var outcome = AdmittedMaterialStates.AdmitProperty(
                new[] { Binding(true, 1f), Binding(true, 0f) }, 1f, out _);

            Assert.That(outcome, Is.EqualTo(AdmittedPropertyOutcome.SourcesDisagree));
        }

        [Test]
        public void DisagreementWithTheSerializedDefaultRefuses()
        {
            var outcome = AdmittedMaterialStates.AdmitProperty(
                new[] { Binding(true, 1f) }, 0f, out _);

            Assert.That(outcome, Is.EqualTo(AdmittedPropertyOutcome.SourcesDisagree));
        }

        [Test]
        public void NonFiniteExactCurveRefuses()
        {
            var outcome = AdmittedMaterialStates.AdmitProperty(
                new[] { Binding(false, 1f) }, 1f, out _);

            Assert.That(outcome, Is.EqualTo(AdmittedPropertyOutcome.NotFiniteExact));
        }

        [Test]
        public void MultipleKeyframeValuesInOneCurveRefuse()
        {
            var outcome = AdmittedMaterialStates.AdmitProperty(
                new[] { Binding(true, 0f, 1f) }, 1f, out _);

            Assert.That(outcome, Is.EqualTo(AdmittedPropertyOutcome.SourcesDisagree));
        }
    }
}
```

- [ ] **Step 2: Run to verify it fails**

Expected: FAIL — `AdmittedMaterialStates` does not exist.

- [ ] **Step 3: Implement the singleton rule**

Return `NotFiniteExact` if any binding is not finite-exact. Otherwise gather every keyframe value from every binding plus the serialized default; if they are not all bit-identical, return `SourcesDisagree`; otherwise return `Singleton` with that value. Compare with `==` on `float`, not with a tolerance: an approximate equality would merge genuinely different states.

- [ ] **Step 4: Run and verify it passes**

Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add Packages/com.alrauna.amuse/Editor/Analysis/AdmittedMaterialStates.cs \
        Packages/com.alrauna.amuse/Tests/Editor/Analysis/AdmittedMaterialStatesTests.cs
git commit -m "feat: admit only singleton finite-exact property values"
```

---

## Task 11: StateMachineBehaviour allowlist and avatar-scoped refusal

**Files:**
- Create: `Packages/com.alrauna.amuse/Editor/Host/StateMachineBehaviourAllowlist.cs`
- Test: `Packages/com.alrauna.amuse/Tests/Editor/Host/StateMachineBehaviourAllowlistTests.cs`

**Interfaces:**
- Consumes: `AvatarAnimationRefusal` from Task 5.
- Produces: `internal static bool StateMachineBehaviourAllowlist.IsAllowed(string typeFullName)`

- [ ] **Step 1: Write the failing test**

```csharp
using Alrauna.Amuse.Editor.Host;
using NUnit.Framework;

namespace Alrauna.Amuse.Tests.Editor.Host
{
    public sealed class StateMachineBehaviourAllowlistTests
    {
        [Test]
        public void UnknownBehaviourTypeIsNotAllowed()
        {
            Assert.That(StateMachineBehaviourAllowlist.IsAllowed(
                "SomeVendor.MysteryBehaviour"), Is.False);
        }

        [Test]
        public void NullOrEmptyTypeNameIsNotAllowed()
        {
            Assert.That(StateMachineBehaviourAllowlist.IsAllowed(null), Is.False);
            Assert.That(StateMachineBehaviourAllowlist.IsAllowed(string.Empty), Is.False);
        }

        [Test]
        public void AllowlistStartsEmpty()
        {
            // The spec requires the allowlist to start empty and grow only with a
            // recorded justification per type. If this fails, a type was added
            // without the justification the spec demands.
            Assert.That(StateMachineBehaviourAllowlist.AllowedTypeNames, Is.Empty);
        }
    }
}
```

- [ ] **Step 2: Run to verify it fails**

Expected: FAIL — type does not exist.

- [ ] **Step 3: Implement the empty allowlist**

An `internal static readonly IReadOnlyCollection<string> AllowedTypeNames` initialized empty, with a file-level comment stating that a type may be added only with a recorded justification that its effect is confined to parameters, layer or playable weights, or state selection, and that this is verification obligation 7. `IsAllowed` returns false for null, empty, and any name not in the set, using `StringComparison.Ordinal`.

- [ ] **Step 4: Run and verify it passes**

Expected: PASS.

- [ ] **Step 5: Wire the avatar-scoped refusal**

In `CommittedControllerGraph.Enumerate`, after collecting behaviours, return `AvatarAnimationRefusal.UnrecognizedStateMachineBehaviour` when any behaviour's `GetType().FullName` is not allowed. Add a test in `CommittedControllerGraphTests` that attaches a locally declared `StateMachineBehaviour` subclass to a state and asserts the avatar-scoped refusal, and asserts that `Layers` is empty so no partial result can be consumed.

- [ ] **Step 6: Run and commit**

```bash
git add Packages/com.alrauna.amuse/Editor/Host/StateMachineBehaviourAllowlist.cs \
        Packages/com.alrauna.amuse/Editor/Host/CommittedControllerGraph.cs \
        Packages/com.alrauna.amuse/Tests/Editor/Host/StateMachineBehaviourAllowlistTests.cs \
        Packages/com.alrauna.amuse/Tests/Editor/Host/CommittedControllerGraphTests.cs
git commit -m "feat: refuse unallowlisted state machine behaviours at avatar scope"
```

---

## Task 12: Structural invalidation

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/Host/UnityRendererAlphaAnalysis.cs`
- Modify: `Packages/com.alrauna.amuse/Tests/Editor/Host/UnityRendererAlphaAnalysisTests.cs`

**Interfaces:**
- Consumes: `CapturedObjectBinding` from Task 6.
- Produces: two new `RendererAnalysisRefusal` members, `AnimatedMeshReplacement` and `AnimatedMaterialSlotCount`.

- [ ] **Step 1: Write the failing test**

```csharp
        [Test]
        public void AnimatedMeshReplacementRefusesTheRenderer()
        {
            var bindings = new[]
            {
                new CapturedObjectBinding(
                    "Body", nameof(SkinnedMeshRenderer), "m_Mesh",
                    System.Array.Empty<int>()),
            };

            Assert.That(
                UnityRendererAlphaAnalysis.StructuralRefusalFor(bindings, "Body"),
                Is.EqualTo(RendererAnalysisRefusal.AnimatedMeshReplacement));
        }

        [Test]
        public void AnimatedSlotCountRefusesTheRenderer()
        {
            var bindings = new[]
            {
                new CapturedObjectBinding(
                    "Body", nameof(SkinnedMeshRenderer), "m_Materials.Array.size",
                    System.Array.Empty<int>()),
            };

            Assert.That(
                UnityRendererAlphaAnalysis.StructuralRefusalFor(bindings, "Body"),
                Is.EqualTo(RendererAnalysisRefusal.AnimatedMaterialSlotCount));
        }

        [Test]
        public void OrdinarySlotSwapIsNotAStructuralRefusal()
        {
            var bindings = new[]
            {
                new CapturedObjectBinding(
                    "Body", nameof(SkinnedMeshRenderer), "m_Materials.Array.data[0]",
                    new[] { 0, 1 }),
            };

            Assert.That(
                UnityRendererAlphaAnalysis.StructuralRefusalFor(bindings, "Body"),
                Is.EqualTo(RendererAnalysisRefusal.None));
        }
```

- [ ] **Step 2: Run to verify it fails**

Expected: FAIL — enum members and method not defined.

- [ ] **Step 3: Implement**

Add the two enum members at the end of `RendererAnalysisRefusal`, preserving the documented rule that declaration order is check order, and add `internal static RendererAnalysisRefusal StructuralRefusalFor(IReadOnlyList<CapturedObjectBinding> bindings, string rendererPath)` matching `m_Mesh` and `m_Materials.Array.size` on the renderer's own path.

- [ ] **Step 4: Run and verify it passes**

Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add Packages/com.alrauna.amuse/Editor/Host/UnityRendererAlphaAnalysis.cs \
        Packages/com.alrauna.amuse/Tests/Editor/Host/UnityRendererAlphaAnalysisTests.cs
git commit -m "feat: refuse renderers with animated structural invalidation"
```

---

## Task 13: Admitted-state product budgeting

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/Analysis/AdmittedMaterialStates.cs`
- Modify: `Packages/com.alrauna.amuse/Tests/Editor/Analysis/AdmittedMaterialStatesTests.cs`

**Interfaces:**
- Consumes: nothing new.
- Produces: `internal static bool AdmittedMaterialStates.TryBudgetProduct(IReadOnlyList<int> perSlotAdmittedCounts, out int productSize)`

The cap is a `private const int` inside `AdmittedMaterialStates`. It must not be `public`, must not be `internal`, and must not appear in any signature. Tests must exercise the *behavior* of exceeding the budget, not the number.

- [ ] **Step 1: Write the failing test**

```csharp
        [Test]
        public void SmallProductsAreBudgeted()
        {
            Assert.That(AdmittedMaterialStates.TryBudgetProduct(
                new[] { 2, 3, 4 }, out var size), Is.True);
            Assert.That(size, Is.EqualTo(24));
        }

        [Test]
        public void OversizedProductsAreRefusedWithoutMaterialization()
        {
            // Deliberately far beyond any plausible bound. The test asserts the
            // refusal behavior, never the specific cap value.
            var counts = new int[64];
            for (var index = 0; index < counts.Length; index++)
            {
                counts[index] = 4;
            }

            Assert.That(AdmittedMaterialStates.TryBudgetProduct(
                counts, out _), Is.False);
        }

        [Test]
        public void BudgetingDoesNotOverflowOnHugeCounts()
        {
            Assert.That(AdmittedMaterialStates.TryBudgetProduct(
                new[] { int.MaxValue, int.MaxValue }, out _), Is.False);
        }
```

- [ ] **Step 2: Run to verify it fails**

Expected: FAIL — method not defined.

- [ ] **Step 3: Implement**

Multiply in a `long` accumulator, returning false as soon as the accumulator exceeds the cap, so no oversized product is ever materialized and no overflow occurs. Any zero count yields a product of zero and is budgeted.

- [ ] **Step 4: Run and verify it passes**

Expected: PASS.

- [ ] **Step 5: Add the refusal member**

Add `AdmittedStateBudgetExceeded` to `RendererAnalysisRefusal` and a test asserting the renderer refuses with it.

- [ ] **Step 6: Commit**

```bash
git add Packages/com.alrauna.amuse/Editor/Analysis/AdmittedMaterialStates.cs \
        Packages/com.alrauna.amuse/Editor/Host/UnityRendererAlphaAnalysis.cs \
        Packages/com.alrauna.amuse/Tests/Editor/Analysis/AdmittedMaterialStatesTests.cs
git commit -m "feat: bound the admitted state product before materialization"
```

---

## Task 14: Immutable evidence substitution

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/Host/UnityMaterialEvidenceCapture.cs`
- Modify: `Packages/com.alrauna.amuse/Tests/Editor/Host/UnityMaterialEvidenceCaptureTests.cs`

**Interfaces:**
- Consumes: `CapturedMaterialEvidence`.
- Produces:
  - `internal CapturedMaterialEvidence CapturedMaterialEvidence.WithScalar(string name, float value)`
  - `internal CapturedMaterialEvidence CapturedMaterialEvidence.WithColor(string name, Color value)`
  - `internal CapturedMaterialEvidence CapturedMaterialEvidence.WithVector(string name, Vector4 value)`

This is the single new pure operation the spec allows: immutable in, immutable out, no live Unity object touched.

- [ ] **Step 1: Write the failing test**

```csharp
        [Test]
        public void ScalarSubstitutionProducesANewEvidenceValue()
        {
            var material = new Material(Shader.Find("Unlit/Color"));
            try
            {
                var request = new MaterialEvidenceRequest(
                    false, false,
                    System.Array.Empty<string>(),
                    new[] { "_Cutoff" },
                    System.Array.Empty<string>(),
                    System.Array.Empty<string>(),
                    System.Array.Empty<TexturePropertyEvidenceRequest>());
                var captured = UnityMaterialEvidenceCapture.Capture(new[]
                {
                    new MaterialEvidenceCaptureInput(material, request),
                })[0];

                var substituted = captured.WithScalar("_Cutoff", 0.25f);

                Assert.That(substituted, Is.Not.SameAs(captured));
                Assert.That(substituted.TryGetScalar("_Cutoff", out var value), Is.True);
                Assert.That(value, Is.EqualTo(0.25f));

                // The source evidence must be untouched by the derivation.
                captured.TryGetScalar("_Cutoff", out var original);
                Assert.That(original, Is.Not.EqualTo(0.25f),
                    "substitution mutated the original evidence instead of " +
                    "deriving a new value");
            }
            finally
            {
                Object.DestroyImmediate(material);
            }
        }

        [Test]
        public void SubstitutingAnUnrequestedPropertyThrows()
        {
            var material = new Material(Shader.Find("Unlit/Color"));
            try
            {
                var request = new MaterialEvidenceRequest(
                    false, false,
                    System.Array.Empty<string>(),
                    new[] { "_Cutoff" },
                    System.Array.Empty<string>(),
                    System.Array.Empty<string>(),
                    System.Array.Empty<TexturePropertyEvidenceRequest>());
                var captured = UnityMaterialEvidenceCapture.Capture(new[]
                {
                    new MaterialEvidenceCaptureInput(material, request),
                })[0];

                // Substituting a property the evidence never requested would
                // silently invent a fact. That is a defect, not a domain outcome.
                Assert.That(() => captured.WithScalar("_NotRequested", 1f),
                    Throws.TypeOf<System.ArgumentException>());
            }
            finally
            {
                Object.DestroyImmediate(material);
            }
        }
```

- [ ] **Step 2: Run to verify it fails**

Expected: FAIL — `WithScalar` not defined.

- [ ] **Step 3: Implement**

Copy the private entry arrays, replace the matching entry's value and set its `HasValue` flag true, and construct a new `CapturedMaterialEvidence` sharing the unchanged arrays by copy. Throw `ArgumentException` when the name was never requested, reusing the existing `Unrequested(name)` helper's message convention.

- [ ] **Step 4: Run and verify it passes**

Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add Packages/com.alrauna.amuse/Editor/Host/UnityMaterialEvidenceCapture.cs \
        Packages/com.alrauna.amuse/Tests/Editor/Host/UnityMaterialEvidenceCaptureTests.cs
git commit -m "feat: derive captured evidence with a substituted property"
```

---

## Task 15: Resolve admitted states and deduplicate exactly

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/Analysis/AdmittedMaterialStates.cs`
- Modify: `Packages/com.alrauna.amuse/Tests/Editor/Analysis/AdmittedMaterialStatesTests.cs`

**Interfaces:**
- Consumes: `AlphaSemanticsResolver.Resolve` (unchanged), `CapturedMaterialEvidence.WithScalar` from Task 14.
- Produces: `internal static IReadOnlyList<AlphaResolution> AdmittedMaterialStates.DistinctResolutions(IReadOnlyList<AlphaResolution> resolutions)`

Dedup is performance-only. The equivalence must be exact or conservative: never merge two resolutions that could classify any triangle differently.

- [ ] **Step 1: Write the failing test**

```csharp
        [Test]
        public void IdenticalUniformResolutionsCollapse()
        {
            var a = AlphaResolution.Uniform(TriangleAlphaOutcome.ProvenOpaque);
            var b = AlphaResolution.Uniform(TriangleAlphaOutcome.ProvenOpaque);

            var distinct = AdmittedMaterialStates.DistinctResolutions(new[] { a, b });

            Assert.That(distinct.Count, Is.EqualTo(1));
        }

        [Test]
        public void DifferentUniformOutcomesDoNotCollapse()
        {
            var opaque = AlphaResolution.Uniform(TriangleAlphaOutcome.ProvenOpaque);
            var transparent = AlphaResolution.Uniform(
                TriangleAlphaOutcome.MustRemainTransparent);

            var distinct = AdmittedMaterialStates.DistinctResolutions(
                new[] { opaque, transparent });

            Assert.That(distinct.Count, Is.EqualTo(2));
        }

        [Test]
        public void ClassifiedResolutionsAreNeverMergedWithUniformOnes()
        {
            var uniform = AlphaResolution.Uniform(TriangleAlphaOutcome.ProvenOpaque);
            var refused = AlphaResolution.Refused(
                AlphaResolutionFailure.SemanticsUnknown);

            var distinct = AdmittedMaterialStates.DistinctResolutions(
                new[] { uniform, refused });

            Assert.That(distinct.Count, Is.EqualTo(2));
        }
```

- [ ] **Step 2: Run to verify it fails**

Expected: FAIL — method not defined.

- [ ] **Step 3: Implement conservative dedup**

Merge two resolutions only when both are uniform with the same outcome, or when both are refused with the same failure. Never merge two classified resolutions, even when their fields look equal — reference-distinct `AlphaTextureData` cannot be proven equivalent cheaply, and keeping them separate costs work but never correctness. Add a code comment stating exactly that.

- [ ] **Step 4: Run and verify it passes**

Expected: PASS.

- [ ] **Step 5: Add a correctness-invariance test**

Assert that classifying a triangle over the deduplicated list yields the same outcome as classifying it over the full list, for a case with duplicate uniform resolutions. This encodes that dedup is performance-only.

- [ ] **Step 6: Commit**

```bash
git add Packages/com.alrauna.amuse/Editor/Analysis/AdmittedMaterialStates.cs \
        Packages/com.alrauna.amuse/Tests/Editor/Analysis/AdmittedMaterialStatesTests.cs
git commit -m "feat: deduplicate admitted resolutions conservatively"
```

---

## Task 16: Per-triangle intersection

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/Host/UnityRendererAlphaAnalysis.cs`
- Modify: `Packages/com.alrauna.amuse/Tests/Editor/Host/UnityRendererAlphaAnalysisTests.cs`

**Interfaces:**
- Consumes: `DistinctResolutions` from Task 15; the existing private `Classify` helper.
- Produces: `internal static TriangleAlphaOutcome[] UnityRendererAlphaAnalysis.IntersectOutcomes(IReadOnlyList<TriangleAlphaOutcome[]> perResolutionOutcomes)`

- [ ] **Step 1: Write the failing test**

```csharp
        [Test]
        public void OnlyTrianglesOpaqueInEveryStateStayOpaque()
        {
            var first = new[]
            {
                TriangleAlphaOutcome.ProvenOpaque,
                TriangleAlphaOutcome.ProvenOpaque,
            };
            var second = new[]
            {
                TriangleAlphaOutcome.ProvenOpaque,
                TriangleAlphaOutcome.MustRemainTransparent,
            };

            var intersected = UnityRendererAlphaAnalysis.IntersectOutcomes(
                new[] { first, second });

            Assert.That(intersected[0], Is.EqualTo(TriangleAlphaOutcome.ProvenOpaque));
            Assert.That(intersected[1], Is.Not.EqualTo(
                TriangleAlphaOutcome.ProvenOpaque));
        }

        [Test]
        public void UnknownInAnyStateRemovesOpacity()
        {
            var first = new[] { TriangleAlphaOutcome.ProvenOpaque };
            var second = new[] { TriangleAlphaOutcome.Unknown };

            var intersected = UnityRendererAlphaAnalysis.IntersectOutcomes(
                new[] { first, second });

            Assert.That(intersected[0], Is.Not.EqualTo(
                TriangleAlphaOutcome.ProvenOpaque));
        }

        [Test]
        public void AnEmptyResolutionSetIsADefectNotAnOpaqueResult()
        {
            // Intersecting nothing must never yield ProvenOpaque by vacuous truth.
            Assert.That(() => UnityRendererAlphaAnalysis.IntersectOutcomes(
                System.Array.Empty<TriangleAlphaOutcome[]>()),
                Throws.TypeOf<System.ArgumentException>());
        }
```

- [ ] **Step 2: Run to verify it fails**

Expected: FAIL — method not defined.

- [ ] **Step 3: Implement**

A triangle is `ProvenOpaque` only when every array reports `ProvenOpaque` at that index. Otherwise it takes `MustRemainTransparent` when every array agrees on that, and `Unknown` in every remaining case. Throw `ArgumentException` for an empty list and for arrays of differing length — both are defects, not domain outcomes.

- [ ] **Step 4: Run and verify it passes**

Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add Packages/com.alrauna.amuse/Editor/Host/UnityRendererAlphaAnalysis.cs \
        Packages/com.alrauna.amuse/Tests/Editor/Host/UnityRendererAlphaAnalysisTests.cs
git commit -m "feat: intersect triangle outcomes across admitted states"
```

---

## Task 17: Recursive no-live-Unity-object evidence guard

Deferred PlatformFinish finding 1.

**Files:**
- Modify: `Packages/com.alrauna.amuse/Tests/Editor/Host/UnityRendererAlphaSnapshotTests.cs`

**Interfaces:**
- Consumes: every captured evidence type from Tasks 6 and 8.
- Produces: `internal static void AssertHasNoUnityObjectFields(Type type)` generalized to walk the whole graph.

- [ ] **Step 1: Write the failing test**

```csharp
        private sealed class NestedHolder
        {
            internal InnerHolder Inner;
        }

        private sealed class InnerHolder
        {
            internal Material Live;
        }

        [Test]
        public void GuardCatchesUnityObjectsNestedBelowTheFirstLevel()
        {
            // The shallow guard passed this. The recursive guard must not.
            Assert.That(() => AssertHasNoUnityObjectFields(typeof(NestedHolder)),
                Throws.InstanceOf<AssertionException>());
        }

        [Test]
        public void CapturedAnimationEvidenceHoldsNoLiveUnityObject()
        {
            AssertHasNoUnityObjectFields(typeof(CapturedAnimationEvidence));
            AssertHasNoUnityObjectFields(typeof(CapturedClipEvidence));
            AssertHasNoUnityObjectFields(typeof(CapturedFloatBinding));
            AssertHasNoUnityObjectFields(typeof(CapturedObjectBinding));
        }
```

- [ ] **Step 2: Run to verify it fails**

Expected: FAIL — `GuardCatchesUnityObjectsNestedBelowTheFirstLevel` passes wrongly under the shallow guard, so it fails the `Throws` assertion.

- [ ] **Step 3: Generalize the guard**

Walk fields recursively with a visited-type set to terminate on cycles, descending into generic arguments and array element types. Fail on any field whose type derives from `UnityEngine.Object`. Skip primitive and `string` types.

- [ ] **Step 4: Run and verify it passes**

Expected: PASS, including the pre-existing snapshot guard tests, which must not regress.

- [ ] **Step 5: Commit**

```bash
git add Packages/com.alrauna.amuse/Tests/Editor/Host/UnityRendererAlphaSnapshotTests.cs
git commit -m "test: walk the whole captured graph in the Unity object guard"
```

---

## Task 18: Named refusal and failure boundary

Deferred PlatformFinish finding I4.

**Files:**
- Modify: `Packages/com.alrauna.amuse/Editor/Build/AmusePlatformFinishPlugin.cs`
- Modify: `Packages/com.alrauna.amuse/Tests/Editor/Build/AmusePlatformFinishPluginTests.cs`

**Interfaces:**
- Consumes: every refusal enum from earlier tasks.
- Produces: extended `AmusePlatformFinishState` counters, one per refusal scope.

- [ ] **Step 1: Write the failing test**

```csharp
        [Test]
        public void AvatarScopedRefusalStopsAnalysisWithoutThrowing()
        {
            using var armed = SyntheticPluginScope.Arm();
            using var assets = new OverrideTemporaryDirectoryScope(null);
            var root = new GameObject("AMUSE avatar refusal fixture");

            try
            {
                var context = AvatarProcessor.ProcessAvatar(
                    root, TestVrchatPlatform.Instance);
                var state = context.GetState<AmusePlatformFinishState>();

                Assert.That(state.HasExecuted, Is.True);
                Assert.That(state.AvatarRefusal, Is.EqualTo(
                    AvatarAnimationRefusal.None));
                Assert.That(state.AnalyzedRendererCount, Is.Zero);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }
```

Add a second test that arms a renderer whose analysis throws a deliberately constructed defect and asserts the exception escapes `ProcessAvatar` rather than being swallowed into a refusal counter.

- [ ] **Step 2: Run to verify it fails**

Expected: FAIL — `AvatarRefusal` not defined.

- [ ] **Step 3: Implement the boundary**

Add `AvatarRefusal` to `AmusePlatformFinishState`. In the barrier pass, when the avatar-scoped refusal is not `None`, record it and analyze no renderer. Per renderer, record the named `RendererAnalysisRefusal` and continue. Add **no** `try`/`catch` around renderer analysis: an unexpected exception must propagate so NDMF records a build-blocking internal failure before anything is mutated.

- [ ] **Step 4: Run and verify it passes**

Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add Packages/com.alrauna.amuse/Editor/Build/AmusePlatformFinishPlugin.cs \
        Packages/com.alrauna.amuse/Tests/Editor/Build/AmusePlatformFinishPluginTests.cs
git commit -m "feat: separate domain refusal from implementation defect"
```

---

## Task 19: Integrate into the PlatformFinish analysis path

**Files:**
- Create: `Packages/com.alrauna.amuse/Editor/Build/AmuseAnimatorBindingsCapture.cs`
- Modify: `Packages/com.alrauna.amuse/Editor/Build/AmusePlatformFinishPlugin.cs`
- Modify: `Packages/com.alrauna.amuse/Tests/Editor/Build/AmusePlatformFinishPluginTests.cs`

**Interfaces:**
- Consumes: every prior task.
- Produces: the wired two-pass configuration.

- [ ] **Step 1: Write the failing end-to-end test**

```csharp
        [Test]
        public void AnimatedSwapToATransparentMaterialRemovesOpacity()
        {
            using var armed = SyntheticPluginScope.Arm();
            using var assets = new OverrideTemporaryDirectoryScope(null);
            var root = new GameObject("AMUSE admitted swap fixture");

            try
            {
                // Build a renderer whose current material proves opaque and whose
                // animated alternative does not. The face must not be counted as an
                // opaque candidate, because it is not opaque in every admitted state.
                var context = AvatarProcessor.ProcessAvatar(
                    root, TestVrchatPlatform.Instance);
                var state = context.GetState<AmusePlatformFinishState>();

                Assert.That(state.OpaqueCandidateTriangleCount, Is.Zero);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }
```

Construct the fixture with the attested Poiyomi or lilToon fixture helpers so both materials resolve through real frontends; a stock Unity shader resolves all-Unknown and would make the test pass vacuously. Assert first that the same renderer *without* the animated swap yields a non-zero opaque candidate count, so the test proves the swap caused the change.

- [ ] **Step 2: Run to verify it fails**

Expected: FAIL — the capture pass does not exist and the barrier ignores animation.

- [ ] **Step 3: Implement the two passes**

In `AmusePlatformFinishPlugin.Configure`:

```csharp
            InPhase(BuildPhase.PlatformFinish)
                .Run(BindingsCapturePassName, AmuseAnimatorBindingsCapture.Execute)
                .Then.Run(BarrierPassName, AmusePlatformFinishPass.Execute);
```

`AmuseAnimatorBindingsCapture` carries `[DependsOnContext(typeof(AnimatorServicesContext))]` and stores the bindings in `AmusePlatformFinishState`. The barrier pass declares no animator extension. Wire the barrier to enumerate the committed graph, capture animation evidence, close dependencies, construct admitted states, resolve, deduplicate, classify, and intersect.

- [ ] **Step 4: Run and verify it passes**

Expected: PASS.

- [ ] **Step 5: Run the complete EditMode suite**

Run the full public EditMode suite and confirm no regression against the recorded baseline. The three NDMF Harmony `mprotect returned EACCES` console entries are the known environment baseline and are not failures.

- [ ] **Step 6: Commit**

```bash
git add Packages/com.alrauna.amuse/Editor/Build/AmuseAnimatorBindingsCapture.cs \
        Packages/com.alrauna.amuse/Editor/Build/AmusePlatformFinishPlugin.cs \
        Packages/com.alrauna.amuse/Tests/Editor/Build/AmusePlatformFinishPluginTests.cs
git commit -m "feat: prove alpha opacity across admitted runtime states"
```

---

## Plan self-review record

**Spec coverage.** Every spec section maps to a task: theorem to Tasks 15–16; relevance-follows-dependency to Task 9; dependency closure to Task 8; observation boundary to Tasks 1, 4, 5, 19; special motions to Task 6 (`IsSpecialMotion` recorded, never gating); analysis-only wording to the global constraints; material swaps to Task 7; property values to Task 10; layer and blend combination to Tasks 5 and 10; behaviours to Task 11; structural invalidation to Task 12; budget to Task 13; proof composition to Tasks 14–16; failure semantics to Task 18; evidence immutability to Task 17; obligations 1–8 to Tasks 1–5 and their spec updates; testing strategy throughout.

**Placeholders.** None. Every code step carries real code. Task 2 Step 3 and Task 4 Step 4 deliberately instruct recording an observed value, which is the defined purpose of a characterization test, not a placeholder.

**Type consistency.** `AvatarAnimationRefusal` (Task 5) is used in Tasks 11 and 18. `CapturedFloatBinding` and `CapturedObjectBinding` (Task 6) are used in Tasks 7, 9, 10, 12, 17. `AdmittedMaterialClosure` (Task 8) feeds Task 9. `WithScalar` (Task 14) feeds Task 15. `DistinctResolutions` (Task 15) feeds Task 16.

**Known open risks carried into execution.**
- Task 2's expected binding-name values are unknown until observed; Task 9's slot mapping depends on them.
- Obligation 4 may not be observable in EditMode and may remain open.
- Task 4's outcome determines whether Task 5 may call `GetInnateControllers` directly.
- Task 5's `m_NormalizedBlendValues` serialized-property access is the only route found for blend-tree normalization in this Unity version and should be re-checked for a public accessor during implementation.
